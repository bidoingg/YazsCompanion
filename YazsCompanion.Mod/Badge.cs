// The verdict on the screen, built from the card prefab's own geometry (canvas 3840x2160 units,
// every card root is 832x1462, centred; the game's hover frame is that rect inset 5; the NEW/UPGRADE
// ribbon overhangs the bottom edge; about 200 canvas units - 260 on the card - lie below it before the divider rule).
//
//  - recommended card: a gold frame on exactly the game's selection rect, with a small diamond on each
//    corner (the game's own motif), and a diamond-tipped ribbon reading RECOMMENDED hanging under the card
//    like the game's NEW / UPGRADE ribbon;
//  - every card: one short reason line under the card, in the card's own font ("2ND   The Rifleman build's main ability";
//    "AVOID   ..." in place of the card's place on a card scored under 1, 0.14.0). Since 0.14.0 (A1) the line is the card's
//    plain words (Card.Display, Wording.cs), not the ranking's headline, which the log keeps; a [shown] line logs what was drawn.
// 0.14.0 (A1): the ribbon steps aside while the game shows its own "Skill Tree 3 / 5" label under the hovered recommended card
// (the label sat right under it): Tick follows the label's fade. And the size follows the card's own scale (the card is drawn
// at 0.77 of the canvas): on the Steam Deck the line came out at about 12 px, now about 13.
// 0.16.0 (C16-11g): the gold frame's bottom edge stops either side of the game's TIER / NEW / RECRUIT plate (Cut) - the plate being
// the card's class label's plate 'Content/AddonDescription' (r1 fix of 10-08; r1 measured the portrait's level diamond).
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
        // fonts sized like the card's own description text (~44 units); the band below the card is ~260 units deep (on the card)
        const float FrameInset = 5f, FrameThick = 6f, CornerDiamond = 30f;
        const float RibbonH = 66f, RibbonW = 480f, RibbonTip = 40f;      // ribbon centre sits at -48 for scale 1
        const float ReasonH = 70f, ReasonW = 800f;                        // reason centre sits at -134 for scale 1
        const float RibbonFont = 42f, ReasonFont = 40f;

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
                // 0.16.0 (C16-11g): the frame's bottom edge stops either side of the game's plate on the card's bottom edge (TIER / NEW /
                // RECRUIT), as the game's own highlight passes behind it - measured now and again 0.5 s later, once the plate has settled
                if (best) { Cut(frame, root, c.Button); CutAgain(frame, root, c.Button); }

                var ribbon = Ribbon(root, c.Button, CardTextSize.RibbonScale(s));        // 0.15.0: no larger than x1.3 - a larger size goes to the words
                if (ribbon != null) ribbon.gameObject.SetActive(best);
                if (best) { Entrance(frame, ribbon, "badge:" + c.Button.Pointer); Follow(c.Button, ribbon); }

                var reason = Reason(root, c.Button, s);
                if (reason != null)
                {
                    // "2ND", or (0.14.0, B4) "AVOID" on a card scored under 1: said in words, the dull red is the second cue
                    string prefix = best ? "" : Synergy.ReasonPrefix(c.Rank, c.Score);
                    reason.text = prefix + Text(c);
                    Fit(reason, s);
                    reason.color = best ? Theme.Cream : (c.Score < 1 ? Theme.Rust : Theme.Grey);
                    reason.gameObject.SetActive(true);
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[badge] " + e.Message); }
        }

        /// <summary>The line a card's reason shows (without its "2ND" / "AVOID"): its plain words (0.14.0, A1; the headline when it has
        /// none), with the names another mod lends in place of the game's, in the glyphs the card font has. The [shown] log line
        /// logs exactly this.</summary>
        public static string Text(Card c) { return Wording.Safe(Names.Text(c.Display ?? c.Reason)); }

        // ---- 0.14.0 (A1): the RECOMMENDED ribbon yields to the game's "Skill Tree 3 / 5" label on the hovered recommended card.
        // That label (UIPowerupButtonSkill.skillTreeCurrentLevelGameObject) hangs right under the card where the ribbon is. The game
        // never switches it off again: its card Animator fades the label's CanvasGroup in on select and out on deselect, and after
        // the first hover the object stays active at alpha 0. So the ribbon follows that alpha, frame by frame from the HUD's tick:
        // ribbon alpha = 1 - label alpha (no Harmony hook of its own). The gold frame and the reason line stay.
        static UIPowerupButtonSkill _follow;       // the recommended card while it is a skill card (null: none)
        static GameObject _ribbon; static CanvasGroup _ribbonGroup;       // held once: a per-frame .gameObject would allocate a wrapper each frame
        static GameObject _label; static CanvasGroup _labelGroup; static bool _labelRead, _tickWarned;
        static bool _steppedSaid;                  // 0.15.0 (C15-08): the '[badge] ribbon stepped aside' proof line, once a session

        static void Follow(UIPowerupButtonBase b, RectTransform ribbon)
        {
            Forget();
            if (ribbon == null) return;
            try
            {
                var skill = b.TryCast<UIPowerupButtonSkill>(); if (skill == null) return;
                var g = ribbon.GetComponent<CanvasGroup>();
                if (g == null) { g = ribbon.gameObject.AddComponent(Il2CppInterop.Runtime.Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>(); if (g == null) return; g.blocksRaycasts = false; g.interactable = false; }
                g.alpha = 1f;
                _follow = skill; _ribbon = ribbon.gameObject; _ribbonGroup = g;
            }
            catch { Forget(); }
        }

        /// <summary>Once a frame from the HUD's tick (P_HudTick): nothing unless a recommended skill card's ribbon is on screen.</summary>
        public static void Tick()
        {
            if (_cutAt > 0f && Time.realtimeSinceStartup >= _cutAt) CutDue();      // 0.16.0 (C16-11g): the second measure with Motion off
            if (_follow == null) return;
            try
            {
                if (_ribbon == null || _ribbonGroup == null || !_ribbon.activeInHierarchy) return;
                if (!_labelRead)
                {
                    _labelRead = true;
                    _label = _follow.skillTreeCurrentLevelGameObject;
                    _labelGroup = _label == null ? null : _label.GetComponent<CanvasGroup>();
                }
                float a = _label != null && _label.activeInHierarchy ? (_labelGroup != null ? _labelGroup.alpha : 1f) : 0f;
                float want = Mathf.Clamp01(1f - a);
                if (Mathf.Abs(_ribbonGroup.alpha - want) > 0.004f) _ribbonGroup.alpha = want;
                // 0.15.0 (C15-08): proof from a sent log that the ribbon yields on this screen - once a session, not verbose
                if (want < 0.5f && !_steppedSaid)
                {
                    _steppedSaid = true;
                    Plugin.Logger.LogInfo("[badge] ribbon stepped aside for the Skill Tree label (ribbon alpha " + want.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ")");
                }
            }
            catch (Exception e)
            {
                if (!_tickWarned) { _tickWarned = true; Plugin.Logger.LogWarning("[badge] the ribbon cannot follow the Skill Tree label (" + e.GetType().Name + " " + e.Message + "): it stays as it is"); }
                Forget();
            }
        }

        /// <summary>No recommended card to follow any more (its screen went, the HUD went); the ribbon is shown whole again.</summary>
        public static void Forget()
        {
            try { if (_ribbonGroup != null) _ribbonGroup.alpha = 1f; } catch { }
            _follow = null; _ribbon = null; _ribbonGroup = null; _label = null; _labelGroup = null; _labelRead = false;
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
                if (_follow != null && b != null && _follow.Pointer == b.Pointer) Forget();
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

        // ---- 0.16.0 (C16-11g, C-m9 of the 10-07 review): the frame's bottom edge cut round the game's plate ----
        // The frame stays the card root's last child (drawn over the card art), so since 0.13.0 its bottom edge - a 6-unit bar along the
        // frame's bottom, the frame being the root inset 5 units - ran through the plate the game hangs on the card's bottom edge: TIER I
        // on a weapon card, NEW on an ability, RECRUIT on a rescue card (series 1007b at 3440 x 1440; the game's own highlight passes
        // behind its plate). Now the edge stops CutGap units either side of the plate (ScreenBand.EdgeCut, pure: G13 on the bench): the
        // frame's 'Bottom' child spans the left piece, a second child 'Bottom2' the right one, both anchored in fractions of the frame's
        // width, so the frame's entrance scale does not move the cut. The corner diamonds stay. Which object is the plate is read per
        // card class (PlateOf); item, military and hashtag cards have none known and keep the whole edge. A plate not found, hidden or
        // not on the edge: the edge whole. Measured when the offer shows the frame and again CutSettle later (the plate's layout may
        // settle after the offer's first frame); the second measure logs one '[badge] plate: ...' proof line per card class and outcome
        // a session (not verbose), naming the object measured.
        // r1 fix (10-08): 0.16.0 r1 took powerupLevelObject - in all 20 r1 runs that logged a plate line, skill and rescue cards alike,
        // '544x224 units, y 212..436 (the frame's bottom edge -726..-720) - no overlap, edge whole': that object ('Content/PowerupLevel')
        // is the level diamond on the portrait, and the C_rescue shot still showed the gold line through RECRUIT. The plate is the
        // image 'Content/AddonDescription' (sprite GenericStatBoost_Rarity_Base_01, 400 x 150 at (-0.9, -707): x -201 .. 199, y -782 ..
        // -632 in the root's units, astride the edge), and the label written on it - TIER I / NEW / RECRUIT - is the card's className
        // field (the field order of both cards' serialized scripts in the game's level2 scene, read offline; the card Animator's clips
        // never move it). So PlateOf takes className's parent. The decision and the line are ScreenBand's (PlateCut / PlateLine, pure:
        // G13 on the bench with these prefab numbers). A plate not cut says, once per card class, what does lie on the edge (DumpEdge).
        const float CutGap = 10f, CutSettle = 0.5f;
        const string CutKey = "badge:cut:", Bottom2Name = "Bottom2";
        static readonly Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3> _corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
        static readonly System.Collections.Generic.HashSet<string> _plateSaid = new System.Collections.Generic.HashSet<string>();
        static bool _cutWarned;
        // the second measure while [General] Motion is off (Fx runs a tween at once then): from Tick
        static RectTransform _cutFrame, _cutRoot; static UIPowerupButtonBase _cutCard; static float _cutAt = -1f;

        /// <summary>The frame's bottom edge on the recommended card <paramref name="b"/> (root <paramref name="root"/>): cut round the game's plate
        /// where the plate sits on it with room either side, else whole.</summary>
        public static void Cut(RectTransform frame, RectTransform root, UIPowerupButtonBase b) { Cut(frame, root, b, false); }

        static void Cut(RectTransform frame, RectTransform root, UIPowerupButtonBase b, bool say)
        {
            RectTransform bottom = null;
            try
            {
                if (frame == null || root == null || b == null) return;
                var bt = frame.Find("Bottom"); bottom = bt == null ? null : bt.TryCast<RectTransform>();
                if (bottom == null) return;
                string cls = TypeName(b);
                var rr = root.rect;
                RectTransform plate = null;
                try { plate = PlateOf(b); } catch { plate = null; }
                if (plate == null)
                {
                    Whole(frame, bottom);
                    if (say && SayPlate(cls + ":none", "[badge] plate: " + cls + " - no plate object known, the frame is drawn whole")) DumpEdge(root, cls);
                    return;
                }
                if (!plate.gameObject.activeInHierarchy)
                {
                    Whole(frame, bottom);
                    // a card without its plate (the object known, switched off - e.g. a rescue screen's Liberate card, if it has none): no dump
                    if (say) SayPlate(cls + ":hidden", "[badge] plate: " + cls + " " + plate.name + " not shown - the frame is drawn whole");
                    return;
                }
                // the plate and the frame's bottom bar in the root's units (the card's own scale and its hover growth cancel)
                plate.GetWorldCorners(_corners);
                var p0 = root.InverseTransformPoint(_corners[0]); var p2 = root.InverseTransformPoint(_corners[2]);
                float px0 = Mathf.Min(p0.x, p2.x), px1 = Mathf.Max(p0.x, p2.x), py0 = Mathf.Min(p0.y, p2.y), py1 = Mathf.Max(p0.y, p2.y);
                float a1, b0;
                string outcome = ScreenBand.PlateCut(rr.xMin, rr.xMax, rr.yMin, FrameInset, FrameThick, px0, px1, py0, py1, CutGap, out a1, out b0);
                float x0 = rr.xMin + FrameInset, x1 = rr.xMax - FrameInset;
                if (outcome == "cut") Pieces(frame, bottom, (a1 - x0) / (x1 - x0), (b0 - x0) / (x1 - x0));
                else Whole(frame, bottom);
                if (!say) return;
                if (SayPlate(cls + ":" + outcome, ScreenBand.PlateLine(cls, plate.name, outcome, rr.xMin, rr.xMax, rr.yMin, FrameInset, FrameThick, px0, px1, py0, py1, a1, b0)) && outcome != "cut")
                    DumpEdge(root, cls);
            }
            catch (Exception e)
            {
                try { if (frame != null && bottom != null) Whole(frame, bottom); } catch { }
                if (!_cutWarned) { _cutWarned = true; Plugin.Logger.LogWarning("[badge] the frame's edge not cut round the card's plate (" + e.GetType().Name + " " + e.Message + "): drawn whole - said once a session"); }
            }
        }

        // the second measure, CutSettle after the first: an Fx timer (key 'badge:cut:' + the card's pointer); with Motion off Fx would run
        // it at once, so Tick does it then
        static void CutAgain(RectTransform frame, RectTransform root, UIPowerupButtonBase b)
        {
            try
            {
                string key = CutKey + b.Pointer;
                Fx.Cancel(key);
                if (Fx.On) { Fx.Run(key, CutSettle, 0.01f, k => { if (k >= 1f) Cut(frame, root, b, true); }); return; }
                _cutFrame = frame; _cutRoot = root; _cutCard = b; _cutAt = Time.realtimeSinceStartup + CutSettle;
            }
            catch { }
        }

        static void CutDue()
        {
            var f = _cutFrame; var r = _cutRoot; var b = _cutCard;
            _cutAt = -1f; _cutFrame = null; _cutRoot = null; _cutCard = null;
            Cut(f, r, b, true);
        }

        /// <summary>The proof line, once a session per key (card class and outcome); true when it was said now.</summary>
        static bool SayPlate(string key, string line) { if (!_plateSaid.Add(key)) return false; Plugin.Logger.LogInfo(line); return true; }

        // r1 fix (10-08), the plan's 'one-time dump': a plate not cut (none known, off the edge, too wide) - what DOES lie on the
        // frame's bottom bar, once a session per card class: every active child of the card root and of its 'Content' whose rect reaches
        // the bar (name, size, x and y in the root's units; ours left out), so the next live log names the plate if the game moves it.
        static readonly System.Collections.Generic.HashSet<string> _dumped = new System.Collections.Generic.HashSet<string>();
        static void DumpEdge(RectTransform root, string cls)
        {
            if (root == null || !_dumped.Add(cls)) return;
            try
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var rr = root.rect;
                float e0 = rr.yMin + FrameInset, e1 = e0 + FrameThick;
                var found = new System.Collections.Generic.List<string>();
                var content = root.Find("Content");
                foreach (var parent in new[] { root, content })
                {
                    if (parent == null) continue;
                    for (int i = 0; i < parent.childCount && found.Count < 12; i++)
                    {
                        var t = parent.GetChild(i); var rt = t == null ? null : t.TryCast<RectTransform>();
                        if (rt == null || !rt.gameObject.activeInHierarchy || rt.name.StartsWith("Yazs", StringComparison.Ordinal)) continue;
                        rt.GetWorldCorners(_corners);
                        var q0 = root.InverseTransformPoint(_corners[0]); var q2 = root.InverseTransformPoint(_corners[2]);
                        float y0 = Mathf.Min(q0.y, q2.y), y1 = Mathf.Max(q0.y, q2.y), x0 = Mathf.Min(q0.x, q2.x), x1 = Mathf.Max(q0.x, q2.x);
                        if (!(y0 < e1 && y1 > e0)) continue;
                        found.Add((parent == root ? "" : "Content/") + rt.name + " " + (x1 - x0).ToString("0", inv) + "x" + (y1 - y0).ToString("0", inv) + " at x " + x0.ToString("0", inv) + ".." + x1.ToString("0", inv) + ", y " + y0.ToString("0", inv) + ".." + y1.ToString("0", inv));
                    }
                }
                Plugin.Logger.LogInfo("[badge] plate dump: " + cls + " - on the frame's bottom edge " + e0.ToString("0", inv) + ".." + e1.ToString("0", inv) + ": "
                    + (found.Count == 0 ? "nothing of the card's" : string.Join("; ", found)) + " (once a session per card class)");
            }
            catch (Exception e) { Plugin.Logger.LogInfo("[badge] plate dump: " + cls + " - not read (" + e.GetType().Name + ")"); }
        }

        // the edge whole: 'Bottom' across the frame, 'Bottom2' hidden
        static void Whole(RectTransform frame, RectTransform bottom)
        {
            bottom.anchorMin = new Vector2(0f, 0f); bottom.anchorMax = new Vector2(1f, 0f);
            var t = frame.Find(Bottom2Name); if (t != null) t.gameObject.SetActive(false);
        }

        // the edge in two pieces: 'Bottom' from the frame's left to <a> of its width, 'Bottom2' (made once, as Ui.Frame makes 'Bottom', and
        // drawn right after it) from <b> to its right
        static void Pieces(RectTransform frame, RectTransform bottom, float a, float b)
        {
            a = Mathf.Clamp01(a); b = Mathf.Clamp01(b);
            bottom.anchorMin = new Vector2(0f, 0f); bottom.anchorMax = new Vector2(a, 0f);
            var t = frame.Find(Bottom2Name); var right = t == null ? null : t.TryCast<RectTransform>();
            if (right == null)
            {
                right = Ui.Edge(frame, Bottom2Name, b, 0, 1, 0, new Vector2(0, FrameThick / 2), new Vector2(0, FrameThick), Theme.Gold);
                try { right.SetSiblingIndex(bottom.GetSiblingIndex() + 1); } catch { }
            }
            right.anchorMin = new Vector2(b, 0f); right.anchorMax = new Vector2(1f, 0f);
            right.gameObject.SetActive(true);
        }

        /// <summary>The plate the game hangs on the card's bottom edge (TIER / NEW / RECRUIT): the image its className label is written on
        /// ('Content/AddonDescription'), on a skill card and on a rescue card; null on any other card class (none known).</summary>
        static RectTransform PlateOf(UIPowerupButtonBase b)
        {
            TextMeshProUGUI label = null;
            var skill = b.TryCast<UIPowerupButtonSkill>(); if (skill != null) label = skill.className;
            else
            {
                if (_noSos) return null;
                try { label = SosPlateLabel(b); } catch { _noSos = true; return null; }
            }
            var up = label == null ? null : label.transform.parent;
            return up == null ? null : up.TryCast<RectTransform>();
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
            // ribbon (RibbonH*s) + 15 above it + 18 gap + half the reason height: -134 at s = 1, the accepted 0.3.1 placement; 0.15.0: under
            // the ribbon at its own size (x1.3 at most, CardTextSize.RibbonScale), so a larger reason line keeps clear of the divider rule
            float rs = CardTextSize.RibbonScale(s);
            rt.anchoredPosition = new Vector2(0f, -(RibbonH * rs + 33f + ReasonH * s / 2)); rt.sizeDelta = new Vector2(Mathf.Min(ReasonW * s, 900f), ReasonH * s);
            text.fontSize = ReasonFont * s;
            ReasonFontNow = ReasonFont * s;
            return text;
        }

        /// <summary>The reason line's font as sized (card units) before Fit shrank a long one: the WHY band and panel take this size,
        /// not the selected card's shrunk one.</summary>
        public static float ReasonFontNow { get; private set; }

        // 0.15.0 (the review of 10-06): a line wider than its rect - 900 units at most, 14 ems at the Deck's x1.56 - shrinks to fit, down
        // to the 15 px floor, before the ellipsis cuts it. Ranker already sizes the words to the room (LineChars); this is the margin.
        static bool _fitSaid;
        static void Fit(TextMeshProUGUI t, float s)
        {
            try
            {
                float full = ReasonFont * s, room = t.rectTransform.sizeDelta.x;
                t.fontSize = full;
                if (!(room > 0)) return;
                float w = t.GetPreferredValues(t.text).x;
                if (!(w > room)) return;
                float min = _px1 > 0 ? ReasonFont * Mathf.Min(s, ScreenBand.MinPx / _px1) : full * 0.9f;
                float fit = Mathf.Max(min, full * room / w * 0.995f);
                t.fontSize = fit;
                if (!_fitSaid)
                {
                    _fitSaid = true;
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    Plugin.Logger.LogInfo("[badge] a reason line shrank to fit its card: " + (_px1 * s).ToString("0.0", inv) + " -> " + (_px1 * fit / ReasonFont).ToString("0.0", inv) + " px ("
                        + w.ToString("0", inv) + " units in " + room.ToString("0", inv) + (fit <= min + 0.01f ? FloorWords(_px1 * fit / ReasonFont) + " - the rest ends in an ellipsis" : "") + "; said once a session)");
                }
            }
            catch { }
        }

        /// <summary>The visible characters a card's reason line holds on this screen at the "Card text size" set now (CardTextSize.LineChars,
        /// from the screen's model): Ranker sizes each card's words to it before the badges are drawn.</summary>
        public static int LineChars()
        {
            try
            {
                float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height, px1 = CardTextSize.ReasonPx1(w, h);
                return CardTextSize.LineChars(CardTextSize.Scale(Plugin.BadgeScale.Value, px1, CardTextSize.CanvasUnits(w, h)), px1);
            }
            catch { return Wording.DeckWidth; }
        }

        // one canvas unit is canvas pixel height / canvas height pixels (a third of a pixel on the Deck), and the card is drawn at
        // a fraction of the canvas: its parents' scale times its own resting 0.95 (the prefab: 0.95 x 0.9 x 0.9 = 0.77). That gives
        // the reason line's height at s = 1; the s itself is CardTextSize's (ScreenBand.cs): Auto enlarges the ribbon and the reason
        // line until the reason is 16 px, capped so both stay inside the band under the card (about 200 canvas units from the card's
        // bottom to the divider rule) - x1.3, x1.6 on a canvas taller than 16:9 (the Deck) -, a size picked in the menu (BadgeScale,
        // "Card text size") is that share of Auto on this screen (at most x1.85, the ribbon held at x1.3), and either way the reason
        // never reads under 15 px, the WHY band's floor.
        // 0.14.0 (A1): up to 0.13.0 the card's own scale was left out - about 21 px on a 1440p monitor either way, but s = 1.2 and
        // about 12 px on the Deck (canvas 3840 x 2400 on 800 pixels); then s = 1.3 there, about 13 px. The root's own scale is not
        // read: the card's Selected animation grows it ~7 % on hover, and the badges are built while cards may still be moving.
        // 0.15.0 (C15-06): the Deck at x1.56, 16 px; a picked size is measured too (the floor), and a canvas that cannot be measured
        // falls back to the screen's model instead of s = 1.
        const float RestingRootScale = 0.95f;
        static string _scaleSaid;
        static float _px1;                         // the reason line's pixels at s = 1 as last measured (Fit's 15 px floor)
        static float Scale(RectTransform root)
        {
            float setting = 0; try { setting = Plugin.BadgeScale.Value; } catch { }
            float px1 = 0f, h = 0f, chain = RestingRootScale, pixels = UnityEngine.Screen.height;
            try
            {
                var comp = root.GetComponentInParent(Il2CppInterop.Runtime.Il2CppType.Of<Canvas>());
                var canvas = comp == null ? null : comp.TryCast<Canvas>();
                if (canvas != null && canvas.rootCanvas != null) canvas = canvas.rootCanvas;
                var crt = canvas == null ? null : canvas.transform.TryCast<RectTransform>();
                h = crt == null ? 0 : crt.rect.height;
                if (h > 0)
                {
                    float measured = canvas.pixelRect.height; if (measured > 0) pixels = measured;
                    float canvasScale = canvas.transform.lossyScale.y;
                    var up = root.parent;
                    if (up != null && canvasScale > 0) chain = up.lossyScale.y / canvasScale * RestingRootScale;
                    if (!(chain > 0.05f) || chain > 4f) chain = RestingRootScale;
                    px1 = ReasonFont * chain * pixels / h;
                }
            }
            catch { px1 = 0f; }
            bool modelled = !(px1 > 0);
            if (modelled) px1 = CardTextSize.ReasonPx1(UnityEngine.Screen.width, UnityEngine.Screen.height);
            float canvasH = h > 0 ? h : CardTextSize.CanvasUnits(UnityEngine.Screen.width, UnityEngine.Screen.height);
            float s = CardTextSize.Scale(setting, px1, canvasH);
            _px1 = px1;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string key = UnityEngine.Screen.width + "x" + UnityEngine.Screen.height + "/" + h.ToString("0") + "/" + setting.ToString("0.00", inv);
            if (key != _scaleSaid)
            {
                _scaleSaid = key;
                bool floor = px1 * s <= ScreenBand.MinPx + 0.01f && s > 1f;
                Plugin.Logger.LogInfo("[badge] scale s=" + s.ToString("0.00", inv) + " px=" + (px1 * s).ToString("0.0", inv)
                    + " (screen " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height + ", " + (modelled ? "canvas not measured: modelled" : "canvas height " + h.ToString("0") + ", card scale " + chain.ToString("0.00", inv))
                    + "; Card text size " + CardTextSize.Label(setting) + (setting > 0 ? " of Auto" : "") + (floor ? FloorWords(px1 * s) : "") + "; ribbon x" + CardTextSize.RibbonScale(s).ToString("0.00", inv)
                    + "; a line holds " + CardTextSize.LineChars(s, px1) + " characters)");
            }
            return s;
        }

        /// <summary>", at the 15 px floor" - or, where the x2 cap (CardTextSize.FloorCap) stops short of it (a screen under 1024 x 768: live
        /// 10-06 480 x 640 logged "at the 15 px floor" at 7.7 px, the 10-07 review), ", under the 15 px floor (the scale stops at x2)".</summary>
        static string FloorWords(float px)
        {
            return px < ScreenBand.MinPx - 0.05f ? ", under the 15 px floor (the scale stops at x" + CardTextSize.FloorCap.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + ")" : ", at the 15 px floor";
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

        // 0.16.0 (C16-11g): the label on the rescue card's plate (RECRUIT; its parent is the plate) - named only here, as SosLabel does
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static TextMeshProUGUI SosPlateLabel(UIPowerupButtonBase b)
        {
            var sos = b.TryCast<UIPowerupButtonSOS>();
            return sos == null ? null : sos.className;
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
    }
}
