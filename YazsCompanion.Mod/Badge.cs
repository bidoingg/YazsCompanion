// The verdict on the screen, built from the card prefab's own geometry (canvas 3840x2160 units,
// every card root is 832x1462, centred; the game's hover frame is that rect inset 5; the NEW/UPGRADE
// ribbon overhangs the bottom edge; ~170 units of free band lie below the card before the divider).
//
//  - recommended card: a gold frame on exactly the game's selection rect, with a small diamond on each
//    corner (the game's own motif), and a diamond-tipped ribbon reading RECOMMENDED hanging under the card
//    like the game's NEW / UPGRADE ribbon;
//  - every card: one short reason line under the card, in the card's own font ("2ND  new ability").
// All of it is parented to the card root, so it rises with a hovered card and disappears with the screen.
// Nothing here captures clicks.
using System;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YazsCompanion
{
    internal static class Badge
    {
        const string FrameName = "YazsFrame", RibbonName = "YazsRibbon", ReasonName = "YazsReason";

        // geometry in canvas units, relative to the card root (832 x 1462)
        // fonts sized like the card's own description text (~44 units); the band below the card is ~220 units deep
        const float FrameInset = 5f, FrameThick = 6f, CornerDiamond = 30f;
        const float RibbonY = -48f, RibbonH = 66f, RibbonW = 480f, RibbonTip = 40f;
        const float ReasonY = -134f, ReasonH = 70f, ReasonW = 800f;
        const float RibbonFont = 42f, ReasonFont = 40f;

        static readonly Color Gold = new Color(0.96f, 0.78f, 0.32f, 1f);
        static readonly Color GoldText = new Color(0.98f, 0.84f, 0.42f, 1f);
        static readonly Color RibbonBg = new Color(0.05f, 0.045f, 0.04f, 0.94f);
        static readonly Color ReasonPick = new Color(0.93f, 0.88f, 0.72f, 1f);
        static readonly Color ReasonGrey = new Color(0.66f, 0.66f, 0.66f, 1f);
        static readonly Color ReasonLow = new Color(0.80f, 0.45f, 0.40f, 1f);

        static readonly string[] Ordinal = { "", "", "2ND", "3RD", "4TH", "5TH", "6TH" };

        public static void Show(Card c)
        {
            try
            {
                var root = c.Button.transform.TryCast<RectTransform>();
                if (root == null) return;
                bool best = c.Rank == 1;

                var frame = Frame(root);
                frame.gameObject.SetActive(best);

                var ribbon = Ribbon(root, c.Button);
                if (ribbon != null) ribbon.gameObject.SetActive(best);

                var reason = Reason(root, c.Button);
                if (reason != null)
                {
                    string prefix = best ? "" : "<b>" + (c.Rank < Ordinal.Length ? Ordinal[c.Rank] : "#" + c.Rank) + "</b>   ";
                    reason.text = prefix + Clean(c.Reason);
                    reason.color = best ? ReasonPick : (c.Score < 1 ? ReasonLow : ReasonGrey);
                    reason.gameObject.SetActive(true);
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[badge] " + e.Message); }
        }

        public static void Hide(UIPowerupButtonBase b)
        {
            try
            {
                var t = b.transform;
                foreach (var n in new[] { FrameName, RibbonName, ReasonName }) { var x = t.Find(n); if (x != null) x.gameObject.SetActive(false); }
            }
            catch { }
        }

        // ---- gold frame on the game's own selection rect ----
        static RectTransform Frame(RectTransform root)
        {
            var existing = root.Find(FrameName);
            if (existing != null) { var e = existing.TryCast<RectTransform>(); e.SetAsLastSibling(); return e; }
            var f = NewRect(FrameName, root);
            Stretch(f, FrameInset, FrameInset, FrameInset, FrameInset);
            Edge(f, "Top", 0, 1, 1, 1, new Vector2(0, -FrameThick / 2), new Vector2(0, FrameThick));
            Edge(f, "Bottom", 0, 0, 1, 0, new Vector2(0, FrameThick / 2), new Vector2(0, FrameThick));
            Edge(f, "Left", 0, 0, 0, 1, new Vector2(FrameThick / 2, 0), new Vector2(FrameThick, 0));
            Edge(f, "Right", 1, 0, 1, 1, new Vector2(-FrameThick / 2, 0), new Vector2(FrameThick, 0));
            Diamond(f, "TL", 0, 1, CornerDiamond, Gold);
            Diamond(f, "TR", 1, 1, CornerDiamond, Gold);
            Diamond(f, "BL", 0, 0, CornerDiamond, Gold);
            Diamond(f, "BR", 1, 0, CornerDiamond, Gold);
            f.SetAsLastSibling();
            return f;
        }

        // ---- RECOMMENDED ribbon under the card, diamond tips like the game's NEW / UPGRADE label ----
        static RectTransform Ribbon(RectTransform root, UIPowerupButtonBase b)
        {
            var existing = root.Find(RibbonName);
            if (existing != null) return existing.TryCast<RectTransform>();
            var template = Template(b);
            if (template == null) return null;
            var r = NewRect(RibbonName, root);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f); r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(0f, RibbonY); r.sizeDelta = new Vector2(RibbonW, RibbonH);
            var bar = Image(r, "Bar", RibbonBg); Stretch(bar, RibbonTip / 2, 0, RibbonTip / 2, 0);
            var rule = Image(r, "Rule", Gold); rule.anchorMin = new Vector2(0, 0); rule.anchorMax = new Vector2(1, 0); rule.pivot = new Vector2(0.5f, 0);
            rule.anchoredPosition = Vector2.zero; rule.sizeDelta = new Vector2(-RibbonTip, 4f);
            Diamond(r, "TipL", 0, 0.5f, RibbonTip, Gold);
            Diamond(r, "TipR", 1, 0.5f, RibbonTip, Gold);
            var text = CloneText(template, r, "Text");
            if (text != null)
            {
                Stretch(text.rectTransform, RibbonTip, 0, RibbonTip, 0);
                text.text = "RECOMMENDED"; text.color = GoldText; text.fontSize = RibbonFont; text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center; text.characterSpacing = 4f;
            }
            return r;
        }

        // ---- one reason line under the card (under the ribbon on the recommended card) ----
        static TextMeshProUGUI Reason(RectTransform root, UIPowerupButtonBase b)
        {
            var existing = root.Find(ReasonName);
            if (existing != null) return existing.GetComponent<TextMeshProUGUI>();
            var template = Template(b);
            if (template == null) { Plugin.Logger.LogWarning("[badge] no label template on " + b.GetIl2CppType().Name); return null; }
            var text = CloneText(template, root, ReasonName);
            if (text == null) return null;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, ReasonY); rt.sizeDelta = new Vector2(ReasonW, ReasonH);
            text.fontSize = ReasonFont; text.alignment = TextAlignmentOptions.Center; text.fontStyle = FontStyles.Normal;
            return text;
        }

        // ---- primitives ----
        static TextMeshProUGUI Template(UIPowerupButtonBase b)
        {
            var skill = b.TryCast<UIPowerupButtonSkill>(); if (skill != null && skill.shortDescription != null) return skill.shortDescription;
            var item = b.TryCast<UIPowerupButtonItem>(); if (item != null && item.itemDescription != null) return item.itemDescription;
            var mil = b.TryCast<UIPowerupButtonMilitary>(); if (mil != null && mil.militaryTrainingDescription != null) return mil.militaryTrainingDescription;
            return null;
        }

        // clone a card label to inherit font, material and canvas settings; strip everything but the text
        static TextMeshProUGUI CloneText(TextMeshProUGUI template, RectTransform parent, string name)
        {
            var go = UnityEngine.Object.Instantiate(template.gameObject, parent);
            go.name = name;
            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                var comp = comps[i];
                if (comp.TryCast<TextMeshProUGUI>() == null && comp.TryCast<RectTransform>() == null && comp.TryCast<CanvasRenderer>() == null)
                    UnityEngine.Object.Destroy(comp);
            }
            for (int i = go.transform.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(go.transform.GetChild(i).gameObject);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (text == null) { UnityEngine.Object.Destroy(go); return null; }
            var rt = text.rectTransform; rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity;
            text.enableAutoSizing = false; text.richText = true; text.raycastTarget = false;
            try { text.enableWordWrapping = false; } catch { }
            try { text.overflowMode = TextOverflowModes.Ellipsis; } catch { }
            text.text = "";
            go.SetActive(true);
            return text;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent(Il2CppType.Of<RectTransform>()).TryCast<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity; rt.localPosition = Vector3.zero;
            return rt;
        }

        static RectTransform Image(RectTransform parent, string name, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent(Il2CppType.Of<Image>()).TryCast<Image>();
            img.color = color; img.raycastTarget = false;
            return rt;
        }

        static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
        }

        static void Edge(RectTransform f, string name, float ax0, float ay0, float ax1, float ay1, Vector2 pos, Vector2 size)
        {
            var rt = Image(f, name, Gold);
            rt.anchorMin = new Vector2(ax0, ay0); rt.anchorMax = new Vector2(ax1, ay1); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static void Diamond(RectTransform parent, string name, float ax, float ay, float size, Color color)
        {
            var rt = Image(parent, name, color);
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(size * 0.7071f, size * 0.7071f);
            rt.localRotation = Quaternion.Euler(0, 0, 45f);
        }

        static string Clean(string s) { return (s ?? "").Replace("<", "").Replace(">", ""); }
    }
}
