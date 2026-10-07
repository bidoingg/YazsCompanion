// WHY on highlight (0.14.0, roadmap item 3 of the 10-05 review): while a card of a selection screen is selected - the mouse
// over it, or the pad's focus on it - a band under the cards says the rest of its verdict in plain words (WhyText.cs, pure):
//     WHY   Experiment 21 goes first: The Medic build's main ability  /  Evolution locked in the Skill Tree  /  ...
//     CLOSE CALL   Either works - Experiment 21 is a hair ahead  /  Kinetic is 44% of your damage
// The game tells a card it is selected through OnSelected - an override of its own on each card class (Skill, Item,
// Military, Hashtag, SOS: five functions in GameAssembly.dll), all post-fixed; and OnDeselected, whose five overrides are
// ONE function (identical-code folding, rva 0x8c8bc0: it resets the team panel's colours and the tag toolbar's highlights -
// research\review_1005\impl\c4\rva_share_out.txt, dis_card_select.txt): a detour on any one of them fires for all five and
// two would stack on one address, so exactly one is patched and its post-fix reads nothing but the card's pointer.
// The band appears in the frame the card is selected (once the screen has flown in: 0.6 s after the cards came, 0.35 s after
// a reroll filled them anew), goes 0.08 s after the card is deselected unless another card is selected meanwhile (moving
// from card to card swaps the words without a flicker), and with the screen. Where it goes is the band manager's
// (ScreenBand.Why, pure): the band under the cards - clear of the selected card's lines as its hover animation grows it, of
// the action hint's line (RerollHint.cs) and of the team panel, above or below the game's divider rule over the buttons, never
// across it - or the band under the action buttons when that one holds more of it; at least 15 px, up to three lines, never
// over a card's text, a button or the divider's diamond. 0.15.0 (C15-12, the user's decision Q4): on a screen wider than 16:9
// whose side wings WideMenus opened, a PANEL in the wing beside the selected card instead - up to four reasons at the cards' own
// reason size, one reason a line (a wrapped one grows the panel upward: 0.15.x, C-M1), its bottom on the card's reason line, clear of
// the cards, the buttons, the divider, the team panel and the skip reward (ScreenBand.Side, pure); 16:9, the Steam Deck and
// WideMenus off keep the band. Also Q4: the band's
// lines are measured as drawn, so the end diamond no longer stands over the last glyph. One '[why] ...' line per card and
// offer with what was drawn ('(side wing left, header + 3 lines, 20.5 px, 3 of 3 shown ...'); the [card] lines are not touched.
// [General] ShowWhy switches it (with ShowBadges).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YazsCompanion
{
    /// <summary>The selection screens measured in view units (top-left origin, y down: LoadoutLayout's R4) - the band manager's
    /// eyes, shared by the action hint and the WHY band.</summary>
    internal static class SelectBands
    {
        public const float RibbonOverhang = 40f;        // the game's NEW / UPGRADE ribbon hangs about 40 units under a card
        public const float HoverGrow = 1.075f;          // the card's Selected animation grows its root about 7 % (the prefab rests at 0.95)
        public const float RestingRootScale = 0.95f;

        /// <summary>A rect in the view's space.</summary>
        public static R4 Rect(RectTransform rt, RectTransform view, Il2CppStructArray<Vector3> corners)
        {
            rt.GetWorldCorners(corners);
            var bl = view.InverseTransformPoint(corners[0]); var tr = view.InverseTransformPoint(corners[2]);
            var vr = view.rect;
            float x0 = Mathf.Min(bl.x, tr.x), x1 = Mathf.Max(bl.x, tr.x), y0 = Mathf.Min(bl.y, tr.y), y1 = Mathf.Max(bl.y, tr.y);
            return new R4(x0 - vr.xMin, vr.yMax - y1, x1 - vr.xMin, vr.yMax - y0);
        }

        static bool Shown(Component c) { try { return c != null && c.gameObject.activeInHierarchy; } catch { return false; } }
        static bool Shown(GameObject g) { try { return g != null && g.activeInHierarchy; } catch { return false; } }

        /// <summary>The cards' lowest line (y down; NaN: no card): each card, the game's ribbon under it and our RECOMMENDED ribbon
        /// and reason line. The card <paramref name="selected"/> is taken at its hover size (its lines drop as the Selected
        /// animation grows it about its centre). <paramref name="span"/>: the cards' horizontal extent, <paramref name="anchor"/>:
        /// the selected card's centre (NaN: none).</summary>
        public static float CardsBottom(List<Card> cards, RectTransform view, Il2CppStructArray<Vector3> corners, IntPtr selected)
        {
            R4 span; float anchor; return CardsBottom(cards, view, corners, selected, out span, out anchor);
        }

        public static float CardsBottom(List<Card> cards, RectTransform view, Il2CppStructArray<Vector3> corners, IntPtr selected, out R4 span, out float anchor)
        {
            float lowest = float.NaN; anchor = float.NaN; span = new R4();
            float sx0 = float.MaxValue, sx1 = float.MinValue;
            if (cards == null) return lowest;
            float rest = RestScale(cards, selected);
            foreach (var c in cards)
            {
                try
                {
                    var root = c.Button == null ? null : c.Button.transform.TryCast<RectTransform>();
                    if (root == null || !root.gameObject.activeInHierarchy) continue;
                    var r = Rect(root, view, corners);
                    float low = r.Y1 + RibbonOverhang;
                    foreach (var n in new[] { "YazsReason", "YazsRibbon" })
                    {
                        var t = root.Find(n); var rt = t == null ? null : t.TryCast<RectTransform>();
                        if (rt != null && rt.gameObject.activeInHierarchy) low = Mathf.Max(low, Rect(rt, view, corners).Y1);
                    }
                    if (selected != IntPtr.Zero && c.Button.Pointer == selected)
                    {
                        float now = root.localScale.y, cy = (r.Y0 + r.Y1) / 2f;
                        float k = now > 0.05f ? Mathf.Max(1f, HoverGrow * rest / now) : HoverGrow;
                        low = cy + (low - cy) * k;
                        anchor = (r.X0 + r.X1) / 2f;
                    }
                    lowest = float.IsNaN(lowest) ? low : Mathf.Max(lowest, low);
                    sx0 = Mathf.Min(sx0, r.X0); sx1 = Mathf.Max(sx1, r.X1);
                }
                catch { }
            }
            if (sx1 > sx0) span = new R4(sx0, 0, sx1, 1);
            return lowest;
        }

        // the cards at rest: the scale of an unselected one (the prefab's 0.95 when the selected card is alone)
        static float RestScale(List<Card> cards, IntPtr selected)
        {
            float rest = float.MaxValue;
            if (selected != IntPtr.Zero)
                foreach (var c in cards)
                {
                    try { if (c.Button == null || c.Button.Pointer == selected || !c.Button.gameObject.activeInHierarchy) continue; float sc = c.Button.transform.localScale.y; if (sc > 0.05f) rest = Mathf.Min(rest, sc); } catch { }
                }
            return rest == float.MaxValue ? RestingRootScale : rest;
        }

        /// <summary>Each card's extent (0.15.0, C15-12: what the WHY panel in a wing keeps clear of): its rect, the game's ribbon under it,
        /// our RECOMMENDED ribbon and reason line; the card <paramref name="selected"/> at its hover size, grown about its centre as
        /// CardsBottom grows it. <paramref name="anchor"/>: the selected card's centre, <paramref name="low"/>: its lowest line (NaN:
        /// no card selected among them).</summary>
        public static List<R4> CardRects(List<Card> cards, RectTransform view, Il2CppStructArray<Vector3> corners, IntPtr selected, out float anchor, out float low)
        {
            var list = new List<R4>(); anchor = float.NaN; low = float.NaN;
            if (cards == null) return list;
            float rest = RestScale(cards, selected);
            foreach (var c in cards)
            {
                try
                {
                    var root = c.Button == null ? null : c.Button.transform.TryCast<RectTransform>();
                    if (root == null || !root.gameObject.activeInHierarchy) continue;
                    var r = Rect(root, view, corners);
                    float x0 = r.X0, x1 = r.X1, y1 = r.Y1 + RibbonOverhang;
                    foreach (var n in new[] { "YazsReason", "YazsRibbon" })
                    {
                        var t = root.Find(n); var rt = t == null ? null : t.TryCast<RectTransform>();
                        if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                        var q = Rect(rt, view, corners); x0 = Mathf.Min(x0, q.X0); x1 = Mathf.Max(x1, q.X1); y1 = Mathf.Max(y1, q.Y1);
                    }
                    var e = new R4(x0, r.Y0, x1, y1);
                    if (selected != IntPtr.Zero && c.Button.Pointer == selected)
                    {
                        float now = root.localScale.y, cx = (r.X0 + r.X1) / 2f, cy = (r.Y0 + r.Y1) / 2f;
                        float k = now > 0.05f ? Mathf.Max(1f, HoverGrow * rest / now) : HoverGrow;
                        e = new R4(cx + (e.X0 - cx) * k, cy + (e.Y0 - cy) * k, cx + (e.X1 - cx) * k, cy + (e.Y1 - cy) * k);
                        anchor = cx; low = e.Y1;
                    }
                    list.Add(e);
                }
                catch { }
            }
            return list;
        }

        /// <summary>The whole screen (the root canvas) in the view's space: wider than the view on a screen wider than 16:9.</summary>
        public static R4 CanvasRect(RectTransform view, Il2CppStructArray<Vector3> corners)
        {
            try
            {
                var canvas = view.GetComponentInParent<Canvas>(); var crt = canvas == null ? null : canvas.rootCanvas.transform.TryCast<RectTransform>();
                if (crt != null) return Rect(crt, view, corners);
            }
            catch { }
            return new R4(0, 0, view.rect.width, view.rect.height);
        }

        /// <summary>The bottom of the screen (the root canvas) in the view's space.</summary>
        public static float CanvasBottom(RectTransform view, Il2CppStructArray<Vector3> corners)
        {
            try
            {
                var canvas = view.GetComponentInParent<Canvas>(); var crt = canvas == null ? null : canvas.rootCanvas.transform.TryCast<RectTransform>();
                if (crt != null) return Rect(crt, view, corners).Y1;
            }
            catch { }
            return view.rect.height;
        }

        /// <summary>The lowest y the player sees: the screen's bottom, or the view's when the game's frame covers what lies under it
        /// (a screen taller than 16:9 with the frame shown - [General] WideMenus hides it).</summary>
        public static float VisibleBottom(UIGameplayUpgradeSelection sel, RectTransform view, Il2CppStructArray<Vector3> corners)
        {
            float canvas = CanvasBottom(view, corners), own = view.rect.height;
            if (canvas <= own + 1f) return canvas;
            try
            {
                var hud = sel.GetComponentInParent<UIGameplay>();
                var border = hud == null ? null : hud.borderGameObject;
                if (border != null && border.activeInHierarchy)
                {
                    var piece = border.transform.Find("Border (2)");          // the bottom bar (anchored at the frame's bottom)
                    if (piece == null || piece.gameObject.activeSelf) return own;
                }
            }
            catch { return own; }
            return canvas;
        }

        /// <summary>The action buttons shown, with their count plates (the number in the diamond hangs under the button).</summary>
        public static List<R4> Buttons(UIGameplayUpgradeSelection sel, RectTransform view, Il2CppStructArray<Vector3> corners)
        {
            var list = new List<R4>();
            Button[] bs = new Button[4];
            try { bs[0] = sel.rerollButton; } catch { }
            try { bs[1] = sel.lockdownUpgradeButton; } catch { }
            try { bs[2] = sel.banishButton; } catch { }
            try { bs[3] = sel.skipButton; } catch { }
            foreach (var b in bs) { try { if (Shown(b)) list.Add(Rect(b.transform.TryCast<RectTransform>(), view, corners)); } catch { } }
            TextMeshProUGUI[] counts = new TextMeshProUGUI[3];
            try { counts[0] = sel.rerollButtonCountText; } catch { }
            try { counts[1] = sel.lockdownButtonCountText; } catch { }
            try { counts[2] = sel.banishButtonCountText; } catch { }
            foreach (var t in counts) { try { if (Shown(t)) list.Add(Rect(t.rectTransform, view, corners)); } catch { } }
            return list;
        }

        /// <summary>What else the WHY band keeps clear of: the action hint's line, the team panel, the FREE labels, the skip reward,
        /// the divider's centre diamond.</summary>
        public static List<R4> Obstacles(UIGameplayUpgradeSelection sel, RectTransform view, Il2CppStructArray<Vector3> corners)
        {
            var list = new List<R4>();
            var hint = RerollHint.LineRect; if (!hint.Empty) list.Add(hint);
            try { var f = sel.rerollFreeLabel; if (Shown(f)) list.Add(Rect(f.transform.TryCast<RectTransform>(), view, corners)); } catch { }
            try { var f = sel.banishFreeLabel; if (Shown(f)) list.Add(Rect(f.transform.TryCast<RectTransform>(), view, corners)); } catch { }
            list.AddRange(Panels(sel, view, corners));
            // the game's divider over the action buttons: its centre diamond (the rule itself splits the band, Dividers)
            try { var d = view.Find("Buttons/Decor"); if (d != null && d.gameObject.activeInHierarchy) list.Add(Rect(d.TryCast<RectTransform>(), view, corners)); } catch { }
            return list;
        }

        /// <summary>What the action hint's line keeps clear of too (0.14.0, R1): the team panel and the skip reward when it shows.</summary>
        public static List<R4> Panels(UIGameplayUpgradeSelection sel, RectTransform view, Il2CppStructArray<Vector3> corners)
        {
            var list = new List<R4>();
            try { var p = sel.statisticsPanel; if (Shown(p)) list.Add(Rect(p.transform.TryCast<RectTransform>(), view, corners)); } catch { }
            try
            {
                var t = sel.skipRewardText; var g = sel.skipRewardCanvasGroup;
                if (Shown(t) && (g == null || g.alpha > 0.01f)) list.Add(Rect(t.rectTransform, view, corners));
            }
            catch { }
            return list;
        }

        /// <summary>The game's divider rule between the cards and the action buttons (Buttons/Line of the selection screens, a grey
        /// rule 6 units thick with a diamond at its centre - measured at 1902 - 1906 units on the PC's level-up screen): the WHY band
        /// sits above it or below it, never across it.</summary>
        public static List<R4> Dividers(RectTransform view, Il2CppStructArray<Vector3> corners)
        {
            var list = new List<R4>();
            try { var l = view.Find("Buttons/Line"); if (l != null && l.gameObject.activeInHierarchy) list.Add(Rect(l.TryCast<RectTransform>(), view, corners)); } catch { }
            return list;
        }

        /// <summary>Screen pixels per view unit.</summary>
        public static float UnitPx(RectTransform view)
        {
            try
            {
                var canvas = view.GetComponentInParent<Canvas>(); if (canvas == null) return 1f;
                var root = canvas.rootCanvas;
                var crt = root.transform.TryCast<RectTransform>();
                float h = crt == null ? 0 : crt.rect.height, px = root.pixelRect.height;
                if (!(h > 0) || !(px > 0)) return 1f;
                float cs = root.transform.lossyScale.y, vs = view.lossyScale.y;
                return px / h * (cs > 0 && vs > 0 ? vs / cs : 1f);
            }
            catch { return 1f; }
        }
    }

    internal static class WhyUi
    {
        const string BlockName = "YazsWhy";
        const float EntranceDelay = 0.6f, RerollDelay = 0.35f, HideDelay = 0.08f;
        const float CardFont = 40f, CardScale = 0.77f, Tip = 36f, PadUnits = 22f;      // the band's ceiling of 22 px: CardTextSize.WhyPreferPx

        static UIGameplayUpgradeSelection _sel; static IntPtr _selPtr;
        static Screen _screen; static List<Card> _cards; static string _clock = "";
        static float _readyAt = -1f;                     // the screen has flown in from then on
        static IntPtr _selected;                         // the card button the game reports selected (OnSelected), Zero after OnDeselected
        // the pause walk's hover tour (Advisor.DebugTourDue, 10-07 review C-m7 / C-m8): the card of the last '[why]' line and when; the selection
        internal static IntPtr LastSaid; internal static float LastSaidAt = -1f;
        internal static IntPtr SelectedNow { get { return _selected; } }
        static Card _on;                                 // the card the band speaks for (null: none)
        static float _placeAt = -1f, _hideAt = -1f;
        static bool _shown;
        static RectTransform _block; static TextMeshProUGUI _text; static CanvasGroup _group;
        static readonly HashSet<IntPtr> _said = new HashSet<IntPtr>();       // the cards of this offer whose band is logged
        static bool _noRoomSaid, _geoSaid, _firstSelect, _warned, _sideSaid;
        static readonly Il2CppStructArray<Vector3> _corners = new Il2CppStructArray<Vector3>(4);

        static bool On { get { try { return Plugin.ShowBadges.Value && Plugin.ShowWhy.Value; } catch { return false; } } }
        static IntPtr Ptr(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase o) { try { return o == null ? IntPtr.Zero : o.Pointer; } catch { return IntPtr.Zero; } }

        // ---------------------------------------------------------------- the game's side
        /// <summary>From Advisor, once the cards are ranked (and again when a reroll or a banish filled the screen anew).</summary>
        public static void OnOffer(UIGameplayUpgradeSelection sel, Screen screen, List<Card> cards, string clock, bool replaced)
        {
            try
            {
                var p = Ptr(sel);
                if (_selPtr != p) { Hide(); _block = null; _text = null; _group = null; }
                _sel = sel; _selPtr = p; _screen = screen; _cards = cards; _clock = clock ?? "";
                _said.Clear(); _noRoomSaid = false; _geoSaid = false; _sideSaid = false; _on = null; _placeAt = -1f; _hideAt = -1f;
                _readyAt = Time.realtimeSinceStartup + (replaced ? RerollDelay : EntranceDelay);
                if (!On) { Hide(); return; }
                var c = CardAt(_selected);
                if (c != null) Show(c); else Hide();
            }
            catch (Exception e) { Warn("offer", e); }
        }

        /// <summary>A card was selected (the post-fix of its class's OnSelected).</summary>
        public static void Selected(UIPowerupButtonBase b)
        {
            var p = Ptr(b);
            if (p == IntPtr.Zero) return;
            _selected = p; _hideAt = -1f;
            if (!_firstSelect)
            {
                _firstSelect = true;
                string cls = "a card"; try { cls = b.GetIl2CppType().Name; } catch { }
                Plugin.Logger.LogInfo("[why] the game reports a card selected (" + cls + ".OnSelected) - the WHY band follows the selection");
            }
            if (_cards == null || !On) return;
            if (_on != null && Ptr(_on.Button) == p && (_shown || _placeAt > 0f)) return;     // the same card again: its band stands (or is due)
            try { var c = CardAt(p); if (c != null) Show(c); }
            catch (Exception e) { Warn("select", e); }
        }

        /// <summary>A card was deselected (the post-fix of the one OnDeselected body all card classes share: the pointer only).</summary>
        public static void Deselected(UIPowerupButtonBase b)
        {
            var p = Ptr(b);
            if (p == IntPtr.Zero) return;
            if (p == _selected) _selected = IntPtr.Zero;
            if (_on != null && _on.Button != null && Ptr(_on.Button) == p) _hideAt = Time.realtimeSinceStartup + HideDelay;
        }

        /// <summary>From Advisor: the screen closed (the pick, a skip). The game's last selection is kept: a chained level-up opens the
        /// same screen with the same buttons, and the next OnSelected - or OnDeselected - says where it is.</summary>
        public static void Close() { Hide(); _sel = null; _selPtr = IntPtr.Zero; _cards = null; _on = null; _placeAt = -1f; _hideAt = -1f; }

        /// <summary>The HUD went away with the scene.</summary>
        public static void Forget() { _shown = false; _block = null; _text = null; _group = null; _sel = null; _selPtr = IntPtr.Zero; _cards = null; _on = null; _selected = IntPtr.Zero; _placeAt = -1f; _hideAt = -1f; }

        /// <summary>From the GameMaster.Update post-fix: two float compares a frame.</summary>
        public static void Tick()
        {
            if (_hideAt > 0f && Time.realtimeSinceStartup >= _hideAt) { _hideAt = -1f; _on = null; Hide(); }
            if (_placeAt > 0f && Time.realtimeSinceStartup >= _placeAt) { _placeAt = -1f; Place(); }
        }

        /// <summary>The action hint's line came, moved or went: the band is laid out again (the next frame).</summary>
        public static void Relayout() { if (_on != null && _shown && _placeAt < 0f) _placeAt = Time.realtimeSinceStartup + 0.01f; }

        static Card CardAt(IntPtr p)
        {
            if (p == IntPtr.Zero || _cards == null) return null;
            foreach (var c in _cards) if (c.Button != null && Ptr(c.Button) == p) return c;
            return null;
        }

        static void Show(Card c)
        {
            _on = c; _hideAt = -1f;
            float now = Time.realtimeSinceStartup;
            if (now < _readyAt) { _placeAt = _readyAt; return; }      // the screen is still flying in
            _placeAt = -1f;
            Place();
        }

        static void Warn(string what, Exception e)
        {
            if (_warned) return;
            _warned = true;
            Plugin.Logger.LogWarning("[why] " + what + " failed (" + e.GetType().Name + " " + e.Message + ") - said once a session; the band stays off for that card");
        }

        // ---------------------------------------------------------------- the band
        static WhyIn InputOf(Card c)
        {
            Card first = null, second = null;
            foreach (var k in _cards) { if (k.Rank == 1) first = k; else if (k.Rank == 2) second = k; }
            return new WhyIn
            {
                Name = c.Name, Rank = c.Rank, Score = c.Score, Shown = Wording.Safe(c.Display ?? c.Reason), Say = c.Say, Why = c.Why,
                FirstName = first != null ? first.Name : null, FirstScore = first != null ? first.Score : double.NaN, FirstShown = first != null ? Wording.Safe(first.Display ?? first.Reason) : null,
                SecondName = second != null ? second.Name : null, SecondScore = second != null ? second.Score : double.NaN,
                FirstSay = first != null ? first.Say : null, FirstWhy = first != null ? first.Why : null, Lend = Ranker.LendNames,
            };
        }

        static void Place()
        {
            var c = _on; var sel = _sel;
            if (!On) { Hide(); return; }
            if (c == null || sel == null) return;
            try
            {
                if (!sel.gameObject.activeInHierarchy) return;
                var view = sel.transform.TryCast<RectTransform>(); if (view == null) return;
                var block = WhyText.Block(InputOf(c));
                var items = new List<string>();
                foreach (var it in block.Items) { string s = Wording.Safe(Names.Text(it)); if (s.Length > 0) items.Add(s); }
                if (items.Count == 0) { Hide(); Said(c, "nothing to add to its own line"); return; }

                // the geometry: the cards (the selected one at its hover size), the buttons, what to keep clear of
                R4 span; float anchor;
                float cardsBottom = SelectBands.CardsBottom(_cards, view, _corners, Ptr(c.Button), out span, out anchor);
                var buttons = SelectBands.Buttons(sel, view, _corners);
                var obstacles = SelectBands.Obstacles(sel, view, _corners);
                float bottom = SelectBands.VisibleBottom(sel, view, _corners);
                float unitPx = SelectBands.UnitPx(view);
                float x0 = span.Empty ? 0 : span.X0, x1 = span.Empty ? view.rect.width : span.X1;
                foreach (var b in buttons) { x0 = Mathf.Min(x0, b.X0); x1 = Mathf.Max(x1, b.X1); }
                if (float.IsNaN(anchor)) anchor = (x0 + x1) / 2f;

                float reasonPx = ReasonPx(c, unitPx);
                if (!Ensure(view)) { Said(c, "no label to clone"); return; }
                if (!_shown && _group != null) _group.alpha = 0f;          // measured live, shown only once placed (all in this frame)
                _block.gameObject.SetActive(true);

                // 0.15.0 (C15-12): on a screen wider than 16:9 whose wings WideMenus opened, a panel in the wing beside the card
                if (SidePanel(c, view, block, buttons, obstacles, reasonPx, unitPx)) return;

                // the text: the cards' own size (their reason line) within 15 - 22 px; a "Card text size" picked in the menu moves the
                // 22 px ceiling as much as it moves the reason lines against their Auto (0.15.0, C15-06: CardTextSize.WhyPx)
                float font = CardTextSize.WhyPx(reasonPx, SizeFactor()) / unitPx, minFont = ScreenBand.MinPx / unitPx;
                var widths = Widths(items, font, out float leadW, out float sepW, block.Lead);
                float total = leadW + widths.Sum() + sepW * Math.Max(0, items.Count - 1);
                float chrome = 2f * (TipOf(font) / 2f + PadUnits * font / 30f);
                int want = Mathf.Clamp(Mathf.CeilToInt(total / Mathf.Max(1f, (x1 - x0) - chrome)), 1, ScreenBand.MaxLines);
                var spot = ScreenBand.Why(new WhyBandIn
                {
                    View = new R4(0, 0, view.rect.width, view.rect.height), Bottom = bottom, CardsBottom = cardsBottom, SpanX0 = x0, SpanX1 = x1,
                    Buttons = buttons.ToArray(), Obstacles = obstacles.ToArray(), Rules = SelectBands.Dividers(view, _corners).ToArray(), AnchorX = anchor, FontUnits = font, MinFontUnits = minFont, Want = want,
                });
                if (spot.At == "none")
                {
                    Hide();
                    if (!_noRoomSaid) { _noRoomSaid = true; Plugin.Logger.LogInfo("[why] " + _screen + " " + _clock + ": no room for the band - " + spot.Bands); }
                    return;
                }
                if (Math.Abs(spot.Font - font) > 0.01f) { widths = Widths(items, spot.Font, out leadW, out sepW, block.Lead); font = spot.Font; }
                float tip = TipOf(font), pad = PadUnits * font / 30f;
                chrome = 2f * (tip / 2f + pad);
                float indent = leadW;                                         // every line starts after the lead (a hanging indent)
                float room = spot.Seg.W - chrome - ScreenBand.Slack(font);    // the text's room: the end diamonds and the pads stay outside it
                var lines = WhyText.Pack(widths, 0f, sepW, room - indent, spot.Lines);
                // each line measured as drawn, the separators' spaces included (Q4: the summed pieces came out short and the last glyph
                // stood under the end diamond); a line still too long loses items from its end
                float widest = ScreenBand.FitLines(lines, items, Separator, s => Width(s, font), indent, room);
                if (lines.Count == 0) { Hide(); Said(c, "no item fits the band (" + spot.Seg.Size + ")"); return; }

                // the words: the lead, then the items; later lines hang under the first item
                var sb = new System.Text.StringBuilder();
                string sep = "<color=" + Theme.DimHex + ">" + Separator + "</color>";
                sb.Append("<b><color=").Append(Theme.GoldHex).Append('>').Append(block.Lead).Append("</color></b><indent=").Append(indent.ToString("0.#", CultureInfo.InvariantCulture)).Append('>');
                var drawn = new List<string>();
                for (int li = 0; li < lines.Count; li++)
                {
                    if (li > 0) sb.Append('\n');
                    for (int j = 0; j < lines[li].Count; j++)
                    {
                        int i = lines[li][j];
                        if (j > 0) sb.Append(sep);
                        sb.Append(items[i]); drawn.Add(items[i]);
                    }
                }
                sb.Append("</indent>");

                float width = ScreenBand.BlockWidth(widest, chrome, font, spot.Seg.W), height = ScreenBand.BlockHeight(lines.Count, font, spot.Pad);
                float left = ScreenBand.Left(spot.Seg, anchor, width), top = spot.Seg.Y0 + Mathf.Max(0f, (spot.Seg.H - height) / 2f);
                Draw(view, left, top, width, height, font, 0.5f, sb.ToString());
                Log(c, block, drawn, spot.At, lines.Count, font * unitPx, items.Count, width, height, left, top, spot.Bands);
            }
            catch (Exception e) { Warn("the band", e); Hide(); }
        }

        const string Separator = "  /  ";

        // 0.15.0 (C15-12, the user's decision Q4 of 10-06): the WHY in a PANEL in the wing beside the selected card, on a screen wider
        // than 16:9 whose wings WideMenus opened - the lead's line, then up to four reasons at the cards' own reason size (20.5 px on the PC), one reason a line,
        // a long one wrapped with its next line a little further in (ScreenBand.Side / Wrap), the panel growing upward for it (0.15.x, C-M1 of
        // the 10-07 review: up to 0.15.0 a wrapped reason took a reason's place - 24 of 104 live panels dropped one). Its words: WhyBlock.SideItems,
        // the "vs #1" sentence without the first card's own line (it stands under that card). False: no wings (16:9; 16:10 - the Steam
        // Deck -; WideMenus off; a frame-keeping menu) or none wide enough - the band under the cards then.
        static bool SidePanel(Card c, RectTransform view, WhyBlock block, List<R4> buttons, List<R4> obstacles, float reasonPx, float unitPx)
        {
            float wing;
            if (!WideMenus.RunWings(out wing)) return false;
            var items = new List<string>();
            foreach (var it in block.SideItems) { string s = Wording.Safe(Names.Text(it)); if (s.Length > 0) items.Add(s); }
            if (items.Count == 0) return false;
            float anchor, low;
            var cards = SelectBands.CardRects(_cards, view, _corners, Ptr(c.Button), out anchor, out low);
            float font = ScreenBand.SidePx(reasonPx) / unitPx, tip = TipOf(font), pad = PadUnits * font / 30f, chrome = 2f * (tip / 2f + pad);
            var vr = view.rect;
            var all = new List<R4>(buttons); all.AddRange(obstacles); all.AddRange(SelectBands.Dividers(view, _corners));
            var side = ScreenBand.Side(new WhySideIn
            {
                View = new R4(0, 0, vr.width, vr.height), Canvas = SelectBands.CanvasRect(view, _corners), Cards = cards.ToArray(), AnchorX = anchor, ReasonBottom = low,
                Obstacles = all.ToArray(), FontUnits = font, TipUnits = tip, ChromeUnits = chrome,
            });
            if (side.At == "none") { SideSaid(wing, side.Wings); return false; }

            // the words: the lead on its own line (the review of 10-06: as a hanging indent "CLOSE CALL" took five ems off every line),
            // then one reason a line at the panel's whole width, a reason's own next line further in
            _text.fontSize = font;
            var memo = new Dictionary<string, float>();
            Func<string, float> measure = s => { float w; if (!memo.TryGetValue(s, out w)) { w = Width(s, font); memo[s] = w; } return w; };
            float leadW = measure("<b>" + block.Lead + "</b>"), cont = ScreenBand.ContEms * font;
            var lines = ScreenBand.Wrap(items, measure, side.Width - chrome - ScreenBand.Slack(font), cont, side.Lines, ScreenBand.SideMaxItems);
            if (lines.Count == 0) { SideSaid(wing, side.Wings + "; its first reason does not fit " + side.Width.ToString("0") + " units"); return false; }
            var sb = new System.Text.StringBuilder();
            sb.Append("<b><color=").Append(Theme.GoldHex).Append('>').Append(block.Lead).Append("</color></b>");
            var drawn = new List<string>(); int last = -1;
            foreach (var l in lines)
            {
                sb.Append('\n');
                if (l.Cont) sb.Append("<indent=").Append(cont.ToString("0.#", CultureInfo.InvariantCulture)).Append('>').Append(l.Text).Append("</indent>");
                else sb.Append(l.Text);
                if (l.Item != last) { drawn.Add(items[l.Item]); last = l.Item; }
            }
            float widest = Mathf.Max(leadW, ScreenBand.Widest(lines, 0f, cont));
            float width = ScreenBand.BlockWidth(widest, chrome, font, side.Width), height = ScreenBand.BlockHeight(lines.Count + 1, font);
            float left = side.Left ? side.Room.X1 - width : side.Room.X0, top = side.Room.Y1 - height;      // beside the card, its bottom on the reason line
            Draw(view, left, top, width, height, font, 1f - ScreenBand.TipFromTop(font) / height, sb.ToString());
            Log(c, block, drawn, side.At, lines.Count + 1, font * unitPx, items.Count, width, height, left, top, "wings " + wing.ToString("0") + " units a side: " + side.Wings);
            return true;
        }

        // the wings were open but held no panel: said once an offer
        static void SideSaid(float wing, string why)
        {
            if (_sideSaid) return;
            _sideSaid = true;
            Plugin.Logger.LogInfo("[why] " + _screen + " " + _clock + ": no panel in the wings (" + wing.ToString("0") + " units a side: " + why + ") - the band under the cards");
        }

        // the block at (left, top) of the view (y down) with its chrome for the font: the plate, the gold rule along its top, the end
        // diamonds at <tipAy> of its height (0.5: the band's middle; the side panel: its first line), the text inside the diamonds and
        // the pads; shown (faded in the first time)
        static void Draw(RectTransform view, float left, float top, float width, float height, float font, float tipAy, string text)
        {
            float tip = TipOf(font), pad = PadUnits * font / 30f;
            var vr = view.rect;
            _block.anchoredPosition = new Vector2(left + vr.xMin - vr.center.x, (vr.yMax - (top + height)) - vr.center.y);
            _block.sizeDelta = new Vector2(width, height);
            Ui.Stretch(_block.Find("Bar").TryCast<RectTransform>(), tip / 2, 0, tip / 2, 0);
            var rule = _block.Find("Rule").TryCast<RectTransform>(); rule.sizeDelta = new Vector2(-tip, Mathf.Max(3f, 0.1f * font));
            for (int i = 0; i < 2; i++)
            {
                var d = _block.Find(i == 0 ? "TipL" : "TipR").TryCast<RectTransform>();
                d.sizeDelta = new Vector2(tip * 0.7071f, tip * 0.7071f);
                d.anchorMin = d.anchorMax = new Vector2(i, Mathf.Clamp01(tipAy));
            }
            Ui.Stretch(_text.rectTransform, tip / 2 + pad, 0, tip / 2 + pad, 0);
            _text.fontSize = font;
            _text.text = text;
            bool appear = !_shown;
            _block.gameObject.SetActive(true); _block.SetAsLastSibling();
            _shown = true;
            if (appear && _group != null) { _group.alpha = 0f; Fx.Run("why:in", 0f, 0.12f, k => { if (_group != null) _group.alpha = Fx.Smooth(k); }); }
            else if (_group != null) { Fx.Cancel("why:in"); _group.alpha = 1f; }
        }

        // what was drawn, once per card and offer; where (every band or wing measured) with the offer's first
        static void Log(Card c, WhyBlock block, List<string> drawn, string at, int lines, float px, int total, float width, float height, float left, float top, string where)
        {
            if (!_said.Add(Ptr(c.Button))) return;
            bool geo = !_geoSaid; _geoSaid = true;
            LastSaid = Ptr(c.Button); LastSaidAt = Time.realtimeSinceStartup;
            if (geo) Shots.Later(0.4f, "why");      // 10-07 review (C-m7): the offer's first WHY as drawn ([Debug] Screenshots)
            Plugin.Logger.LogInfo("[why] " + _screen + " " + _clock + ": #" + c.Rank + " " + c.Name + " " + c.Score.ToString("0.00", CultureInfo.InvariantCulture) + " - " + block.Lead
                + " '" + string.Join("' | '", drawn) + "' (" + at + ", " + LinesSaid(at, lines) + ", " + px.ToString("0.#", CultureInfo.InvariantCulture) + " px, "
                + drawn.Count + " of " + total + " shown" + (geo ? "; " + width.ToString("0") + " x " + height.ToString("0") + " units at " + left.ToString("0") + "," + top.ToString("0") + "; " + where : "") + ")");
        }

        /// <summary>The [why] line's count: the band's lines ("2 lines", its lead inline), the side panel's as "header + 3 lines" (0.15.x, the
        /// 10-07 review: "4 lines" counted the WHY / CLOSE CALL header, so a full panel read "5 lines" against the user's "up to 4 lines").</summary>
        static string LinesSaid(string at, int lines)
        {
            if (at != null && at.StartsWith("side", StringComparison.Ordinal)) { int n = Math.Max(0, lines - 1); return "header + " + n + " line" + (n == 1 ? "" : "s"); }
            return lines + " line" + (lines > 1 ? "s" : "");
        }

        static void Said(Card c, string why)
        {
            if (c == null || !_said.Add(Ptr(c.Button))) return;
            LastSaid = Ptr(c.Button); LastSaidAt = Time.realtimeSinceStartup;
            Plugin.Logger.LogInfo("[why] " + _screen + " " + _clock + ": #" + c.Rank + " " + c.Name + " - no band: " + why);
        }

        static float TipOf(float font) { return Tip * font / CardFont * 1.2f; }

        // the "Card text size" pick against Auto on this screen (1 on Auto), from the screen's model of the cards
        static float SizeFactor()
        {
            float setting = 0f; try { setting = Plugin.BadgeScale.Value; } catch { }
            if (!(setting > 0f)) return 1f;
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            return CardTextSize.Factor(setting, CardTextSize.ReasonPx1(w, h), CardTextSize.CanvasUnits(w, h));
        }

        // the cards' reason line in pixels (the band's preferred size): the drawn line's font and scale, else the card's own 40 x 0.77
        static float ReasonPx(Card c, float unitPx)
        {
            try
            {
                var root = c.Button == null ? null : c.Button.transform;
                var t = root == null ? null : root.Find("YazsReason"); var tmp = t == null ? null : t.GetComponent<TextMeshProUGUI>();
                var view = _sel.transform;
                if (tmp != null && view.lossyScale.y > 0) return Mathf.Max(tmp.fontSize, Badge.ReasonFontNow) * (tmp.transform.lossyScale.y / view.lossyScale.y) * unitPx;      // 0.15.0: as sized, not as a long line shrank (Badge.Fit)
            }
            catch { }
            return CardFont * CardScale * unitPx;
        }

        // the width of each item, of the lead and of the separator at the font (view units), from the label's own measure
        static List<float> Widths(List<string> items, float font, out float leadW, out float sepW, string lead)
        {
            _text.fontSize = font;
            var list = new List<float>(items.Count);
            foreach (var it in items) list.Add(Width(it, font));
            leadW = Width("<b>" + lead + "</b>", font) + font * 0.6f;
            sepW = Width("x" + Separator + "x", font) - Width("xx", font);      // between two glyphs: TMP leaves trailing spaces out of a width (Q4)
            return list;
        }

        static float Width(string s, float font)
        {
            float w = 0f;
            try { w = _text.GetPreferredValues(s).x; } catch { }
            if (!(w > 0f)) w = 0.46f * font * s.Length;          // the card font's average glyph, should the measure fail
            return w;
        }

        // the band's objects under the screen's view, built once per screen and kept
        static bool Ensure(RectTransform view)
        {
            if (_block != null && _text != null) { try { if (_block.gameObject != null) return true; } catch { } }
            var t = view.Find(BlockName);
            if (t != null)
            {
                _block = t.TryCast<RectTransform>();
                var tt = _block.Find("Text"); _text = tt == null ? null : tt.GetComponent<TextMeshProUGUI>();
                _group = _block.GetComponent<CanvasGroup>();
                return _text != null;
            }
            var template = Template();
            if (template == null) return false;
            var r = Ui.NewRect(BlockName, view);
            try { var le = r.gameObject.AddComponent(Il2CppType.Of<LayoutElement>()).TryCast<LayoutElement>(); if (le != null) le.ignoreLayout = true; } catch { }
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0f, 0f);
            try { _group = r.gameObject.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false; } catch { _group = null; }
            Ui.Image(r, "Bar", Theme.Plate);
            var rule = Ui.Image(r, "Rule", Theme.Gold); rule.anchorMin = new Vector2(0, 1); rule.anchorMax = new Vector2(1, 1); rule.pivot = new Vector2(0.5f, 1); rule.anchoredPosition = Vector2.zero;
            Ui.Diamond(r, "TipL", 0, 0.5f, Tip, Theme.Gold);
            Ui.Diamond(r, "TipR", 1, 0.5f, Tip, Theme.Gold);
            var text = Ui.CloneText(template, r, "Text");
            if (text == null) { UnityEngine.Object.Destroy(r.gameObject); return false; }
            text.fontStyle = FontStyles.Normal; text.alignment = TextAlignmentOptions.Left; text.characterSpacing = 0f; text.color = Theme.Cream;
            try { text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Overflow; text.lineSpacing = 0f; } catch { }
            _block = r; _text = text;
            r.gameObject.SetActive(false);
            return true;
        }

        // the label to clone: a card's reason line (the card font), else the card's own description
        static TextMeshProUGUI Template()
        {
            if (_cards != null)
                foreach (var c in _cards)
                {
                    try { var t = c.Button == null ? null : c.Button.transform.Find("YazsReason"); var tmp = t == null ? null : t.GetComponent<TextMeshProUGUI>(); if (tmp != null) return tmp; } catch { }
                }
            if (_cards != null)
                foreach (var c in _cards)
                {
                    try { var tmp = Ui.FindLabel(c.Button.transform, null); if (tmp != null) return tmp; } catch { }
                }
            return null;
        }

        static void Hide()
        {
            _placeAt = -1f;
            Fx.Cancel("why:in");
            try { if (_block != null) _block.gameObject.SetActive(false); } catch { }
            _shown = false;
        }

        // ---------------------------------------------------------------- the hooks
        /// <summary>Every card class's own OnSelected (each a function of its own in GameAssembly.dll); a class a game build lacks is
        /// simply not there.</summary>
        internal static List<MethodBase> SelectTargets()
        {
            var list = new List<MethodBase>();
            Type[] types;
            try { types = typeof(UIPowerupButtonBase).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }
            catch { types = new Type[0]; }
            foreach (var t in types)
            {
                try
                {
                    if (t == null || !t.IsSubclassOf(typeof(UIPowerupButtonBase))) continue;
                    var m = AccessTools.DeclaredMethod(t, "OnSelected", Type.EmptyTypes);
                    if (m != null) list.Add(m);
                }
                catch { }
            }
            return list;
        }

        /// <summary>The ONE OnDeselected to patch: the skill card's (its body is every card class's - see the head of the file).</summary>
        internal static MethodBase DeselectTarget() { try { return AccessTools.DeclaredMethod(typeof(UIPowerupButtonSkill), "OnDeselected", Type.EmptyTypes); } catch { return null; } }

        /// <summary>At load: which of the hooks took ("[why] hooks: OnSelected UIPowerupButtonSkill ok, ... | OnDeselected ... ok").</summary>
        public static void LogHooks()
        {
            var parts = new List<string>(); bool all = true;
            foreach (var m in SelectTargets()) { bool ok = Ours(m); all &= ok; parts.Add(m.DeclaringType.Name.Replace("UIPowerupButton", "") + (ok ? " ok" : " not patched")); }
            var d = DeselectTarget(); bool dok = d != null && Ours(d); all &= dok;
            string line = "[why] hooks: OnSelected " + (parts.Count > 0 ? string.Join(", ", parts) : "none found") + " | OnDeselected (one body for every card class) " + (d == null ? "missing" : dok ? "ok" : "not patched");
            if (all && parts.Count > 0) Plugin.Logger.LogInfo(line); else Plugin.Logger.LogWarning(line);
        }

        static bool Ours(MethodBase m)
        {
            try { var info = Harmony.GetPatchInfo(m); return info != null && info.Postfixes.Any(p => p.owner == Plugin.GUID); } catch { return false; }
        }
    }

    // ---- the selection of a card: each card class's own OnSelected (five functions), and the one OnDeselected they share ----
    [HarmonyPatch]
    static class P_CardSelected
    {
        static bool Prepare() { return WhyUi.SelectTargets().Count > 0; }
        static IEnumerable<MethodBase> TargetMethods() { return WhyUi.SelectTargets(); }
        static void Postfix(UIPowerupButtonBase __instance) { WhyUi.Selected(__instance); }
    }

    [HarmonyPatch]
    static class P_CardDeselected
    {
        static bool Prepare() { return WhyUi.DeselectTarget() != null; }
        static MethodBase TargetMethod() { return WhyUi.DeselectTarget(); }
        static void Postfix(UIPowerupButtonBase __instance) { WhyUi.Deselected(__instance); }
    }
}
