// The verdict on the screen, built from the card prefab's own geometry (canvas 3840x2160 units,
// every card root is 832x1462, centred; the game's hover frame is that rect inset 5; the NEW/UPGRADE
// ribbon overhangs the bottom edge; ~170 units of free band lie below the card before the divider).
//
//  - recommended card: a gold frame on exactly the game's selection rect, with a small diamond on each
//    corner (the game's own motif), and a diamond-tipped ribbon reading RECOMMENDED hanging under the card
//    like the game's NEW / UPGRADE ribbon;
//  - every card: one short reason line under the card, in the card's own font ("2ND  new ability").
// All of it is parented to the card root, so it rises with a hovered card and disappears with the screen.
// Nothing here captures clicks. Colours and primitives are the shared ones in Ui.cs.
using System;
using UnityEngine;
using TMPro;

namespace YazsCompanion
{
    internal static class Badge
    {
        const string FrameName = "YazsFrame", RibbonName = "YazsRibbon", ReasonName = "YazsReason";

        // geometry in canvas units, relative to the card root (832 x 1462)
        // fonts sized like the card's own description text (~44 units); the band below the card is ~220 units deep
        const float FrameInset = 5f, FrameThick = 6f, CornerDiamond = 30f;
        const float RibbonH = 66f, RibbonW = 480f, RibbonTip = 40f;      // ribbon centre sits at -48 for scale 1
        const float ReasonH = 70f, ReasonW = 800f;                        // reason centre sits at -134 for scale 1
        const float RibbonFont = 42f, ReasonFont = 40f;

        static readonly string[] Ordinal = { "", "", "2ND", "3RD", "4TH", "5TH", "6TH" };

        public static void Show(Card c)
        {
            try
            {
                var root = c.Button.transform.TryCast<RectTransform>();
                if (root == null) return;
                bool best = c.Rank == 1;
                float s = Scale(root);

                var frame = Frame(root);
                frame.gameObject.SetActive(best);

                var ribbon = Ribbon(root, c.Button, s);
                if (ribbon != null) ribbon.gameObject.SetActive(best);
                if (best) Entrance(frame, ribbon, "badge:" + c.Button.Pointer);

                var reason = Reason(root, c.Button, s);
                if (reason != null)
                {
                    string prefix = best ? "" : "<b>" + (c.Rank < Ordinal.Length ? Ordinal[c.Rank] : "#" + c.Rank) + "</b>   ";
                    reason.text = prefix + Clean(Names.Text(c.Reason));      // the rules' words; a name another mod lends in place of the game's
                    reason.color = best ? Theme.Cream : (c.Score < 1 ? Theme.Rust : Theme.Grey);
                    reason.gameObject.SetActive(true);
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[badge] " + e.Message); }
        }

        // the offer appears: the gold frame settles onto the card, the ribbon unfolds from its middle, its tips ping
        static void Entrance(RectTransform frame, RectTransform ribbon, string key)
        {
            try
            {
                Fx.Cancel(key);
                CanvasGroup group = null;
                try { group = frame.GetComponent<CanvasGroup>(); if (group == null) { group = frame.gameObject.AddComponent(Il2CppInterop.Runtime.Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false; } } catch { }
                Fx.Run(key + ":frame", 0.12f, 0.45f, k =>
                {
                    float s = 1f + 0.05f * (1f - Fx.OutCubic(k));
                    frame.localScale = new Vector3(s, s, 1f);
                    if (group != null) group.alpha = Fx.Smooth(k * 1.6f);
                });
                if (ribbon == null) return;
                Fx.Run(key + ":ribbon", 0.32f, 0.4f, k => { ribbon.localScale = new Vector3(Mathf.Max(0.0001f, Fx.OutBack(k)), 1f, 1f); }, () =>
                {
                    foreach (var name in new[] { "TipL", "TipR" })
                    {
                        var tip = ribbon.Find(name); var rt = tip == null ? null : tip.TryCast<RectTransform>();
                        if (rt != null) Fx.Ping(key + ":" + name, rt, RibbonTip * 1.45f, 4f, Theme.Gold, false, 0f, 0.6f, 2.6f, false);
                    }
                });
            }
            catch { }
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
            var f = Ui.Frame(root, FrameName, FrameInset, FrameThick, Theme.Gold, CornerDiamond);
            f.SetAsLastSibling();
            return f;
        }

        // ---- RECOMMENDED ribbon under the card, diamond tips like the game's NEW / UPGRADE label ----
        // s = size multiplier (1 on a desktop monitor); the ribbon hangs 15 units under the card at any size
        static RectTransform Ribbon(RectTransform root, UIPowerupButtonBase b, float s)
        {
            var existing = root.Find(RibbonName);
            RectTransform r = existing != null ? existing.TryCast<RectTransform>() : null;
            TextMeshProUGUI text = null;
            if (r == null)
            {
                var template = Template(b);
                if (template == null) return null;
                r = Ui.NewRect(RibbonName, root);
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f); r.pivot = new Vector2(0.5f, 0.5f);
                var bar = Ui.Image(r, "Bar", Theme.Plate); Ui.Stretch(bar, RibbonTip / 2, 0, RibbonTip / 2, 0);
                var rule = Ui.Image(r, "Rule", Theme.Gold); rule.anchorMin = new Vector2(0, 0); rule.anchorMax = new Vector2(1, 0); rule.pivot = new Vector2(0.5f, 0);
                rule.anchoredPosition = Vector2.zero; rule.sizeDelta = new Vector2(-RibbonTip, 4f);
                Ui.Diamond(r, "TipL", 0, 0.5f, RibbonTip, Theme.Gold);
                Ui.Diamond(r, "TipR", 1, 0.5f, RibbonTip, Theme.Gold);
                text = CardText(template, r, "Text");
                if (text != null)
                {
                    Ui.Stretch(text.rectTransform, RibbonTip, 0, RibbonTip, 0);
                    text.text = "RECOMMENDED"; text.color = Theme.GoldText; text.fontStyle = FontStyles.Bold;
                    text.alignment = TextAlignmentOptions.Center; text.characterSpacing = 4f;
                }
            }
            else { var t = r.Find("Text"); if (t != null) text = t.GetComponent<TextMeshProUGUI>(); }
            r.anchoredPosition = new Vector2(0f, -(RibbonH * s / 2 + 15f)); r.sizeDelta = new Vector2(RibbonW * s, RibbonH * s);
            if (text != null) text.fontSize = RibbonFont * s;
            return r;
        }

        // ---- one reason line under the card (under the ribbon on the recommended card) ----
        static TextMeshProUGUI Reason(RectTransform root, UIPowerupButtonBase b, float s)
        {
            var existing = root.Find(ReasonName);
            TextMeshProUGUI text = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
            if (text == null)
            {
                var template = Template(b);
                if (template == null) { Once("none:" + TypeName(b), "[badge] no label template on " + TypeName(b) + " (not even a label with a font under the card): no ribbon and no reason on these cards"); return null; }
                text = CardText(template, root, ReasonName);
                if (text == null) return null;
                var rt0 = text.rectTransform;
                rt0.anchorMin = rt0.anchorMax = new Vector2(0.5f, 0f); rt0.pivot = new Vector2(0.5f, 0.5f);
                text.alignment = TextAlignmentOptions.Center; text.fontStyle = FontStyles.Normal;
            }
            var rt = text.rectTransform;
            // ribbon (RibbonH*s) + 15 above it + 18 gap + half the reason height: -134 at s = 1, the accepted 0.3.1 placement
            rt.anchoredPosition = new Vector2(0f, -(RibbonH * s + 33f + ReasonH * s / 2)); rt.sizeDelta = new Vector2(Mathf.Min(ReasonW * s, 900f), ReasonH * s);
            text.fontSize = ReasonFont * s;
            return text;
        }

        // one canvas unit is Screen.height / canvas height pixels (a third of a pixel on the Deck): enlarge the ribbon and
        // the reason line until the reason font is at least MinTextPx tall, capped so both stay inside the band under the
        // card (about 220 units); BadgeScale in the config overrides the automatic value
        const float MinTextPx = 16f, MaxScale = 1.3f;
        static float Scale(RectTransform root)
        {
            float fixedScale = 0; try { fixedScale = Plugin.BadgeScale.Value; } catch { }
            if (fixedScale > 0) return Mathf.Clamp(fixedScale, 0.5f, MaxScale);
            try
            {
                var comp = root.GetComponentInParent(Il2CppInterop.Runtime.Il2CppType.Of<Canvas>());
                var canvas = comp == null ? null : comp.TryCast<Canvas>();
                if (canvas != null && canvas.rootCanvas != null) canvas = canvas.rootCanvas;
                var crt = canvas == null ? null : canvas.transform.TryCast<RectTransform>();
                float h = crt == null ? 0 : crt.rect.height; if (h <= 0) return 1f;
                float px = ReasonFont * UnityEngine.Screen.height / h;
                return Mathf.Clamp(MinTextPx / px, 1f, MaxScale);
            }
            catch { return 1f; }
        }

        // ---- primitives ----
        // The label the ribbon and the reason line are cloned from: the card's own short description, per card class.
        // 0.12.2 (F02): the game's patch of 2026-09-23 gave the rescue cards a class of their own (UIPowerupButtonSOS, no
        // longer a skill card), and the chain below did not know it - the rescue cards lost their ribbon and reason line
        // and logged a warning per card per offer. The chain knows it now; a class it has never seen falls back to the
        // first active label with a font under the card (said once per class and session), and CheckCardClasses at load
        // names any card class the chain does not know, so the next such patch shows in the first log line.
        // The rescue card class is only named inside its own small methods (SosType, SosLabel): an interop generated from a game
        // build before that patch has no such type, and a static field naming it would fail the whole class's initializer - every
        // badge and reason line of every screen, not just the rescue cards'. Without it, rescue cards fall back to their first label.
        static Type[] _known;
        static bool _noSos;

        static Type[] Known()
        {
            if (_known != null) return _known;
            var list = new System.Collections.Generic.List<Type> { typeof(UIPowerupButtonSkill), typeof(UIPowerupButtonItem), typeof(UIPowerupButtonMilitary), typeof(UIPowerupButtonHashtag) };
            try { list.Add(SosType()); } catch { _noSos = true; }
            return _known = list.ToArray();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static Type SosType() { return typeof(UIPowerupButtonSOS); }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static TextMeshProUGUI SosLabel(UIPowerupButtonBase b)
        {
            var sos = b.TryCast<UIPowerupButtonSOS>();
            if (sos == null) return null;
            return sos.shortDescription != null ? sos.shortDescription : sos.className;
        }

        static TextMeshProUGUI Template(UIPowerupButtonBase b)
        {
            var skill = b.TryCast<UIPowerupButtonSkill>(); if (skill != null && skill.shortDescription != null) return skill.shortDescription;
            var item = b.TryCast<UIPowerupButtonItem>(); if (item != null && item.itemDescription != null) return item.itemDescription;
            var mil = b.TryCast<UIPowerupButtonMilitary>(); if (mil != null && mil.militaryTrainingDescription != null) return mil.militaryTrainingDescription;
            var tag = b.TryCast<UIPowerupButtonHashtag>();
            if (tag != null) { if (tag.hashtagShortDescriptionText != null) return tag.hashtagShortDescriptionText; if (tag.hashtagDescriptionText != null) return tag.hashtagDescriptionText; }
            if (!_noSos)
            {
                TextMeshProUGUI label = null;
                try { label = SosLabel(b); }
                catch (Exception e) { _noSos = true; Once("nosos", "[badge] the rescue card class is not in this game build's interop (" + e.GetType().Name + "): rescue cards borrow their first label"); }
                if (label != null) return label;
            }
            var any = FirstLabel(b);
            if (any != null) Once("fallback:" + TypeName(b), "[badge] " + TypeName(b) + ": no known label template, using the card's first label '" + any.name + "'");
            return any;
        }

        // the last resort: the first active label with a font under the card, in hierarchy order - never one of our own
        static TextMeshProUGUI FirstLabel(UIPowerupButtonBase b)
        {
            try
            {
                var all = b.GetComponentsInChildren(Il2CppInterop.Runtime.Il2CppType.Of<TextMeshProUGUI>(), false);
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i].TryCast<TextMeshProUGUI>(); if (t == null) continue;
                    try
                    {
                        if (t.font == null || !t.gameObject.activeInHierarchy) continue;
                        if (t.name.StartsWith("Yazs", StringComparison.Ordinal)) continue;
                        var up = t.transform.parent; if (up != null && up.name.StartsWith("Yazs", StringComparison.Ordinal)) continue;
                    }
                    catch { continue; }
                    return t;
                }
            }
            catch { }
            return null;
        }

        static readonly System.Collections.Generic.HashSet<string> _said = new System.Collections.Generic.HashSet<string>();
        static void Once(string key, string warning) { if (_said.Add(key)) Plugin.Logger.LogWarning(warning); }
        static string TypeName(UIPowerupButtonBase b) { try { return b.GetIl2CppType().Name; } catch { return "a card"; } }

        /// <summary>At load: the game's card classes (every UIPowerupButtonBase subclass the interop assembly has) against the
        /// ones Template knows. One info line when all are known, one warning per class that is not - its cards would
        /// fall back to their first label.</summary>
        public static void CheckCardClasses()
        {
            try
            {
                Type[] types;
                try { types = typeof(UIPowerupButtonBase).Assembly.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; }
                var names = new System.Collections.Generic.List<string>(); int unknown = 0;
                foreach (var t in types)
                {
                    if (t == null || !t.IsSubclassOf(typeof(UIPowerupButtonBase))) continue;
                    bool known = false; foreach (var k in Known()) if (k.IsAssignableFrom(t)) { known = true; break; }
                    names.Add(t.Name.Replace("UIPowerupButton", ""));
                    if (!known) { unknown++; Plugin.Logger.LogWarning("[badge] card class " + t.Name + " has no label template: its cards borrow their first label (worth a look)"); }
                }
                names.Sort(StringComparer.Ordinal);
                Plugin.Logger.LogInfo("[badge] card classes: " + string.Join(", ", names) + (unknown == 0 ? " - all have a label template" : " - " + unknown + " without"));
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[badge] card classes not checked: " + e.Message); }
        }

        // a card label clone on one line, cut with an ellipsis if it is ever too long
        static TextMeshProUGUI CardText(TextMeshProUGUI template, RectTransform parent, string name)
        {
            var text = Ui.CloneText(template, parent, name);
            if (text != null) { try { text.overflowMode = TextOverflowModes.Ellipsis; } catch { } }
            return text;
        }

        static string Clean(string s) { return (s ?? "").Replace("<", "").Replace(">", ""); }
    }
}
