// The reroll hint on the rescue (SOS) screen (0.12.2). In a round of Steam Deck logs the player rerolled rescue cards
// five times on four screens until the survivor the readout's SOS row named came up, and chose a hint for it: when the
// survivor the Companion rates best among those who could still come beats every card on offer by a clear margin
// (RerollCall in Synergy.cs, pure - the offline bench replays it) and the game still has a reroll for the screen, the
// game's Reroll button gets the recommended card's gold frame and a line stands over it, in the band between the cards'
// reason lines and the button:
//     REROLL  -  Tank would rate higher (5.9 vs 4.6)
// Who "could still come" follows how the game draws the cards (PowerupReferences.GetRandomCharacterUnlocks: the classes'
// UnlockCharacterPowerup cards, minus the cards shown before a reroll - the screen's _preRerollCharacters): every class
// unlocked in the profile that is not on the squad, not on the cards and not held back after a reroll, scored like the
// SOS cards (Ranker.Recruitable, shared with the readout's SOS row); where the class's own unlock card can be found its
// IsAvailable() must agree (a check that refuses every class is not trusted, and the log says so). The reroll count is
// the game's own for that screen: what it hands SetActionButtonsInteractivity (hooked below), else the number on the
// Reroll button, else the team's "Rerolls available" statistic; a FREE reroll counts too. A reroll fills the screen
// again (Advisor's replaced offer): the hint is judged again; the pick takes it away. One '[squad] reroll hint: ...'
// line per evaluation, with the scores. The drawn names go through Names (another mod's names); the log keeps the game's.
// Placed 0.6 s after the cards came (the screen has flown in by then), measured from what is on screen: over the Reroll
// button when the band up to the lowest card line is tall enough, else under it, else the frame alone. Nothing here
// takes clicks. Motion only on appear: the frame settles, the line unfolds from its left tip and types on, the tip pings.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CT = GamePlayer.CharacterType;

namespace YazsCompanion
{
    internal static class RerollHint
    {
        const string FrameName = "YazsRerollFrame", LineName = "YazsRerollHint";
        // canvas units at scale 1 (the card badges' sizes: the reason line's font, the ribbon's tips)
        const float Font = 40f, LineH = 62f, Tip = 36f, Pad = 26f, MaxW = 1800f, Gap = 8f, PlaceDelay = 0.6f;
        const float MinTextPx = 16f, MaxScale = 1.3f;

        sealed class Avail { public bool Button, Interactable, Free; public int Label = -1, Stat = -1; }
        sealed class Inputs { public List<Recruit> Offered, Possible; public double Liberate = -1, RecruitValue; public Avail Rr; public string Pool = ""; }

        static UIGameplayUpgradeSelection _sel;          // the rescue screen being advised (null: none)
        static IntPtr _selPtr;
        static List<Card> _cards;
        static Inputs _in;
        static RerollCall _call;
        static string _text;                             // the line while the hint is wanted, null = hidden
        static float _placeAt = -1f;
        static bool _shown;
        static int _gameCount = -1;                      // the count the game handed the screen for this offer, -1 = not seen
        static IntPtr _hookPtr; static int _hookFrame = -9, _hookCount = -1; static bool _hookSaid;

        static bool On { get { try { return Plugin.AdviceRerollHint.Value; } catch { return true; } } }
        static IntPtr Ptr(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase o) { try { return o == null ? IntPtr.Zero : o.Pointer; } catch { return IntPtr.Zero; } }
        static string F2(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

        // ---------------------------------------------------------------- the game's side
        /// <summary>From Advisor, once the rescue cards are ranked (and again when a reroll or a banish filled the screen anew).</summary>
        public static void OnOffer(UIGameplayUpgradeSelection sel, List<Card> cards, Snapshot s, bool replaced)
        {
            try
            {
                var p = Ptr(sel);
                // the game may hand the screen its count just before it fills the cards: keep that one, else wait for the next
                _gameCount = _hookPtr == p && Time.frameCount - _hookFrame <= 1 ? _hookCount : -1;
                _sel = sel; _selPtr = p; _cards = cards; _call = null;
                if (!On)
                {
                    Hide();
                    Plugin.Logger.LogInfo("[squad] reroll hint: not shown - off ([Advice] RerollHint = false)");
                    return;
                }
                var inp = new Inputs { Offered = new List<Recruit>(), RecruitValue = s.Ctx.RecruitValue, Rr = Read(sel) };
                foreach (var c in cards)
                {
                    if (c.Recruit != null) inp.Offered.Add(c.Recruit);
                    else if (c.Kind == "liberate") inp.Liberate = Math.Max(inp.Liberate, c.Score);
                }
                inp.Possible = Possible(sel, s, inp.Offered, out inp.Pool);
                _in = inp;
                Decide(replaced ? "the cards were replaced" : null, true);
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[squad] reroll hint: not judged - " + e.Message); Hide(); }
        }

        /// <summary>From the SetActionButtonsInteractivity post-fix: the game's reroll count for the screen. Decided again when
        /// it changes; logged when that turns the hint on or off.</summary>
        public static void OnButtons(UIGameplayUpgradeSelection sel, int numRerolls)
        {
            var p = Ptr(sel);
            _hookPtr = p; _hookFrame = Time.frameCount; _hookCount = numRerolls;
            if (p != _selPtr || _in == null || numRerolls == _gameCount) return;
            _gameCount = numRerolls;
            try
            {
                if (!_hookSaid) { _hookSaid = true; Plugin.Logger.LogInfo("[squad] reroll hint: the rescue screen hands its reroll count to SetActionButtonsInteractivity (" + numRerolls + ") - that count comes first from here on"); }
                if (On) Decide("the game's count for the screen is " + numRerolls, false);
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[squad] reroll hint: " + e.Message); }
        }

        /// <summary>From Advisor: a selection screen closed (the pick, a skip) - the hint goes with it.</summary>
        public static void Close() { Hide(); _sel = null; _selPtr = IntPtr.Zero; _cards = null; _in = null; _call = null; _gameCount = -1; }

        /// <summary>The HUD went away with the scene: nothing of ours is left to hide (the unlock cards are looked up again next run).</summary>
        public static void Forget() { _placeAt = -1f; _text = null; _shown = false; _sel = null; _selPtr = IntPtr.Zero; _cards = null; _in = null; _call = null; _gameCount = -1; _hookPtr = IntPtr.Zero; _unlockCards = null; }

        /// <summary>From the GameMaster.Update post-fix (it ticks while the screen holds the game): one float compare a frame.</summary>
        public static void Tick()
        {
            if (_placeAt < 0f || Time.realtimeSinceStartup < _placeAt) return;
            _placeAt = -1f;
            Place();
        }

        // what the game says about rerolls on this screen right now
        static Avail Read(UIGameplayUpgradeSelection sel)
        {
            var a = new Avail();
            Button b = null;
            try { b = sel.rerollButton; } catch { }
            try { a.Button = b != null && b.gameObject.activeInHierarchy; } catch { }
            try { a.Interactable = b != null && b.interactable; } catch { }
            try
            {
                var t = sel.rerollButtonCountText; int n;
                if (t != null && t.gameObject.activeInHierarchy && int.TryParse((t.text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) a.Label = n;
            }
            catch { }
            try { var f = sel.rerollFreeLabel; a.Free = f != null && f.activeInHierarchy; } catch { }
            try
            {
                var gm = GameplayMaster.s_instance; var ts = gm == null ? null : gm.teamStatistics;
                if (ts != null) a.Stat = (int)Math.Round(ts.GetStatisticValue(PlayerStatistic.EType.TeamNumRerolls));
            }
            catch { }
            return a;
        }

        static bool CanReroll(Avail a, int game, out string text)
        {
            int n = game >= 0 ? game : a.Label >= 0 ? a.Label : a.Stat;
            string from = game >= 0 ? "the screen's count" : a.Label >= 0 ? "the Reroll button's count" : a.Stat >= 0 ? "the team's Rerolls available" : "no count readable";
            text = (n >= 0 ? n + " (" + from : "? (" + from) + (a.Free ? ", a FREE reroll" : "") + (a.Button ? "" : ", no Reroll button shown")
                + (a.Stat >= 0 && game < 0 && a.Label >= 0 && a.Stat != a.Label ? ", team statistic " + a.Stat : "") + ")";
            return a.Button && (n > 0 || a.Free || (n < 0 && a.Interactable));
        }

        // who could still come: unlocked, not on the squad, not on the cards, not held back after a reroll, and - where the
        // class's unlock card is found - available by the game's own check
        static List<Recruit> Possible(UIGameplayUpgradeSelection sel, Snapshot s, List<Recruit> offered, out string note)
        {
            var onCards = new HashSet<int>(offered.Select(r => r.Class));
            var held = new HashSet<int>();
            try
            {
                var rescue = sel.TryCast<UIGameplayCharacterRescue>();
                var pre = rescue == null ? null : rescue._preRerollCharacters;
                foreach (var p in G.Each(pre))
                {
                    ClassProperties cp = null; try { if (p != null) cp = p.targetClassProperties; } catch { }
                    if (cp != null) { int c = (int)cp.characterType; if (!onCards.Contains(c)) held.Add(c); }
                }
            }
            catch { }
            var list = Ranker.Recruitable(s, cls => onCards.Contains((int)cls) || held.Contains((int)cls));
            var parts = new List<string>();
            if (held.Count > 0) parts.Add("held back after a reroll: " + string.Join(", ", held.Select(c => G.ClassName((CT)c))));
            // the game's own check on each class's unlock card
            var refused = new List<Recruit>(); int asked = 0;
            foreach (var r in list)
            {
                var card = UnlockCard(r.Class); if (card == null) continue;
                bool ok = true; try { ok = card.IsAvailable(); asked++; } catch { continue; }
                if (!ok) refused.Add(r);
            }
            if (asked == 0 && list.Count > 0) parts.Add("no unlock card to ask the game about");
            else if (refused.Count > 0 && refused.Count == list.Count) parts.Add("the game's IsAvailable() refused all " + list.Count + " - not trusted, kept");
            else if (refused.Count > 0) { parts.Add("the game's IsAvailable() drops " + string.Join(", ", refused.Select(r => r.Name))); list = list.Where(r => !refused.Contains(r)).ToList(); }
            note = parts.Count > 0 ? " (" + string.Join("; ", parts) + ")" : "";
            return list;
        }

        // each class's UnlockCharacterPowerup, found once a session in the game's powerup lists (asset data)
        static Dictionary<int, PowerupBase> _unlockCards;
        static PowerupBase UnlockCard(int cls)
        {
            if (_unlockCards == null)
            {
                _unlockCards = new Dictionary<int, PowerupBase>();
                try
                {
                    var refs = PowerupReferences.Get;
                    if (refs != null)
                        foreach (var list in new[] { refs.skillPowerups, refs.stillAvailableSkillPowerups })
                            foreach (var p in G.Each(list))
                            {
                                if (p == null || p.TryCast<UnlockCharacterPowerup>() == null) continue;
                                ClassProperties cp = null; try { cp = p.targetClassProperties; } catch { }
                                if (cp != null && !_unlockCards.ContainsKey((int)cp.characterType)) _unlockCards[(int)cp.characterType] = p;
                            }
                }
                catch { }
            }
            PowerupBase found; return _unlockCards.TryGetValue(cls, out found) ? found : null;
        }

        // ---------------------------------------------------------------- the verdict
        static void Decide(string when, bool always)
        {
            if (_in == null) return;
            string rr; bool can = CanReroll(_in.Rr, _gameCount, out rr);
            var call = RerollCall.Decide(_in.Offered, _in.Liberate, _in.Possible, can, rr, _in.RecruitValue);
            bool flipped = _call == null || _call.Show != call.Show;
            _call = call;
            if (always || flipped) Log(call, rr, when);
            if (!call.Show) { Hide(); return; }
            string text = LineText(call);
            if (_shown && text == _text) return;           // the same words on screen already (a count that moved, still > 0)
            _text = text; _placeAt = Time.realtimeSinceStartup + (_shown ? 0.05f : PlaceDelay);
        }

        static void Log(RerollCall c, string rr, string when)
        {
            var sb = new System.Text.StringBuilder("[squad] reroll hint: ");
            if (when != null) sb.Append("(").Append(when).Append(") ");
            sb.Append(c.Show ? "SHOWN - " : "not shown - ").Append(c.Why);
            sb.Append(" | rerolls ").Append(rr);
            var on = new List<string>();
            foreach (var r in _in.Offered.OrderBy(r => r, Comparer<Recruit>.Create(Recruit.Compare))) on.Add(r.Name + " " + F2(Math.Round(r.Score, 2)));
            if (_in.Liberate >= 0) on.Add("Liberate " + F2(Math.Round(_in.Liberate, 2)));
            sb.Append(" | on the cards: ").Append(on.Count > 0 ? string.Join(", ", on) : "none");
            var could = _in.Possible.OrderBy(r => r, Comparer<Recruit>.Create(Recruit.Compare)).Select(r => r.Name + " " + F2(Math.Round(r.Score, 2))).ToList();
            sb.Append(" | could still come: ").Append(could.Count > 0 ? string.Join(", ", could) : "nobody").Append(_in.Pool);
            sb.Append(" | margin ").Append(F2(RerollCall.Margin)).Append(", recruit value ").Append(F2(_in.RecruitValue));
            Plugin.Logger.LogInfo(sb.ToString());
        }

        // REROLL - Tank would rate higher (5.9 vs 4.6); a second survivor that clears the margin too: "Tank or SWAT"
        static string LineText(RerollCall c)
        {
            var who = c.Better.Take(2).Select(r => Names.Class((CT)r.Class)).ToList();
            string names = who.Count > 1 ? who[0] + " or " + who[1] : who.Count == 1 ? who[0] : Names.Class((CT)c.Best.Class);
            string nums = Math.Round(c.Best.Score, 1).ToString("0.0", CultureInfo.InvariantCulture) + " vs " + Math.Round(c.Offered, 1).ToString("0.0", CultureInfo.InvariantCulture);
            return "<b><color=" + Theme.GoldHex + ">REROLL</color></b><color=" + Theme.DimHex + ">  -  </color>" + names.Replace("<", "").Replace(">", "")
                + " would rate higher <color=" + Theme.DimHex + ">(" + nums + ")</color>";
        }

        // ---------------------------------------------------------------- drawing
        static void Hide()
        {
            _placeAt = -1f; _text = null;
            bool was = _shown; _shown = false;
            if (!was || _sel == null) return;
            Fx.Cancel("reroll:");
            try
            {
                var b = _sel.rerollButton; var f = b == null ? null : b.transform.Find(FrameName); if (f != null) f.gameObject.SetActive(false);
                var l = _sel.transform.Find(LineName); if (l != null) l.gameObject.SetActive(false);
            }
            catch { }
        }

        static void Place()
        {
            if (_text == null || _sel == null) return;
            try
            {
                var sel = _sel;
                if (!sel.gameObject.activeInHierarchy) return;
                Button button = null; try { button = sel.rerollButton; } catch { }
                var brt = button == null ? null : button.transform.TryCast<RectTransform>();
                var view = sel.transform.TryCast<RectTransform>();
                if (brt == null || view == null) { Plugin.Logger.LogInfo("[squad] reroll hint: not drawn - the screen has no Reroll button"); return; }
                bool first = !_shown;
                float s = Scale(view);

                // the gold frame on the button (the recommended card's, a size smaller)
                var frame = Frame(brt);

                // where the line fits, measured in the view's space: between the lowest card line and the button's top
                var corners = new Il2CppStructArray<Vector3>(4);
                float bx, bBottom, bTop;
                Bounds(brt, view, corners, out bx, out bBottom, out bTop);
                float lowest = float.MaxValue;
                if (_cards != null)
                    foreach (var c in _cards)
                    {
                        try
                        {
                            var root = c.Button == null ? null : c.Button.transform.TryCast<RectTransform>();
                            if (root == null || !root.gameObject.activeInHierarchy) continue;
                            // the card itself, and the game's NEW / UPGRADE ribbon that overhangs its bottom edge (about 40 units)
                            float x0, lo, hi; Bounds(root, view, corners, out x0, out lo, out hi); lowest = Mathf.Min(lowest, lo - 40f);
                            foreach (var n in new[] { "YazsReason", "YazsRibbon" })
                            {
                                var t = root.Find(n); var rt = t == null ? null : t.TryCast<RectTransform>();
                                if (rt != null && rt.gameObject.activeInHierarchy) { Bounds(rt, view, corners, out x0, out lo, out hi); lowest = Mathf.Min(lowest, lo); }
                            }
                        }
                        catch { }
                    }
                float floor = bTop;
                try
                {   // a FREE reroll label standing over the button is kept clear too
                    var fl = sel.rerollFreeLabel; var frt = fl == null || !fl.activeInHierarchy ? null : fl.transform.TryCast<RectTransform>();
                    if (frt != null) { float x0, lo, hi; Bounds(frt, view, corners, out x0, out lo, out hi); if (lo >= bTop - 4f) floor = Mathf.Max(floor, hi); }
                }
                catch { }
                float h = LineH * s, band = lowest == float.MaxValue ? h + 2 * Gap : lowest - floor, below = 0f;
                string where; float y, k = 1f;
                if (band >= 0.72f * h + 2 * Gap) { k = Mathf.Min(1f, (band - 2 * Gap) / h); y = floor + (band - h * k) / 2f; where = "over the Reroll button"; }
                else
                {
                    try
                    {
                        var canvas = view.GetComponentInParent<Canvas>(); var crt = canvas == null ? null : canvas.rootCanvas.transform.TryCast<RectTransform>();
                        if (crt != null) { float x0, lo, hi; Bounds(crt, view, corners, out x0, out lo, out hi); below = bBottom - lo; }
                    }
                    catch { }
                    if (below >= 0.72f * h + 2 * Gap) { k = Mathf.Min(1f, (below - 2 * Gap) / h); y = bBottom - Gap - h * k; where = "under the Reroll button (the band over it is " + band.ToString("0") + " units)"; }
                    else { y = 0f; where = null; }
                }

                var line = view.Find(LineName); var lrt = line == null ? null : line.TryCast<RectTransform>();
                if (where == null)
                {
                    if (lrt != null) lrt.gameObject.SetActive(false);
                    if (first) Plugin.Logger.LogInfo("[squad] reroll hint: drawn as the frame alone - no room for the line (over the button " + band.ToString("0") + " units, under it " + below.ToString("0") + ", the line needs " + (0.72f * h + 2 * Gap).ToString("0") + ")");
                    _shown = true;
                    if (first) Entrance(frame, null, null, true);
                    return;
                }
                if (lrt == null) lrt = BuildLine(view, brt);
                if (lrt == null) { Plugin.Logger.LogInfo("[squad] reroll hint: not drawn - no label to clone for the line"); return; }
                var textT = lrt.Find("Text"); var text = textT == null ? null : textT.GetComponent<TextMeshProUGUI>();
                float sk = s * k, tip = Tip * sk, pad = Pad * sk;
                text.fontSize = Font * sk;
                text.text = _text;
                float tw = 0f; try { tw = text.preferredWidth; } catch { }
                if (!(tw > 0f)) tw = 22f * sk * _text.Length * 0.5f;
                float w = Mathf.Min(MaxW * s, tw + tip + 2 * pad + 4f);
                var bar = lrt.Find("Bar").TryCast<RectTransform>(); Ui.Stretch(bar, tip / 2, 0, tip / 2, 0);
                var rule = lrt.Find("Rule").TryCast<RectTransform>(); rule.sizeDelta = new Vector2(-tip, Mathf.Max(3f, 4f * sk));
                foreach (var n in new[] { "TipL", "TipR" }) { var d = lrt.Find(n).TryCast<RectTransform>(); d.sizeDelta = new Vector2(tip * 0.7071f, tip * 0.7071f); }
                Ui.Stretch(text.rectTransform, tip / 2 + pad, 0, tip / 2 + pad, 0);
                // anchored at the view's centre (pivot bottom-left): the offset from the rect's centre is the point in view space
                var vr = view.rect;
                lrt.anchoredPosition = new Vector2(bx - vr.center.x, y - vr.center.y);
                lrt.sizeDelta = new Vector2(w, h * k);
                lrt.gameObject.SetActive(true); lrt.SetAsLastSibling();
                if (first)
                    Plugin.Logger.LogInfo("[squad] reroll hint: drawn " + where + " - " + w.ToString("0") + " x " + (h * k).ToString("0") + " units, font " + (Font * sk).ToString("0.#") + " (x" + sk.ToString("0.00") + "), " + (band >= 0 ? "band " + band.ToString("0") + " units between the cards' lowest line and the button" : "the cards reach below the button's top"));
                _shown = true;
                Entrance(frame, lrt, text, first);
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[squad] reroll hint: not drawn - " + e.Message); }
        }

        // a rect's left edge, bottom and top in the view's space
        static void Bounds(RectTransform rt, RectTransform view, Il2CppStructArray<Vector3> corners, out float left, out float bottom, out float top)
        {
            rt.GetWorldCorners(corners);
            var bl = view.InverseTransformPoint(corners[0]); var tr = view.InverseTransformPoint(corners[2]);
            left = Mathf.Min(bl.x, tr.x); bottom = Mathf.Min(bl.y, tr.y); top = Mathf.Max(bl.y, tr.y);
        }

        static RectTransform Frame(RectTransform button)
        {
            var t = button.Find(FrameName); var f = t == null ? null : t.TryCast<RectTransform>();
            if (f == null) { f = Ui.Frame(button, FrameName, 2f, 4f, Theme.Gold, 24f); OutOfLayout(f); }
            f.gameObject.SetActive(true); f.SetAsLastSibling();
            return f;
        }

        static RectTransform BuildLine(RectTransform view, RectTransform button)
        {
            var template = Template(button);
            if (template == null) return null;
            var r = Ui.NewRect(LineName, view);
            OutOfLayout(r);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0f, 0f);
            Ui.Image(r, "Bar", Theme.Plate);
            var rule = Ui.Image(r, "Rule", Theme.Gold); rule.anchorMin = new Vector2(0, 0); rule.anchorMax = new Vector2(1, 0); rule.pivot = new Vector2(0.5f, 0); rule.anchoredPosition = Vector2.zero;
            Ui.Diamond(r, "TipL", 0, 0.5f, Tip, Theme.Gold);
            Ui.Diamond(r, "TipR", 1, 0.5f, Tip, Theme.Gold);
            var text = Ui.CloneText(template, r, "Text");
            if (text == null) { UnityEngine.Object.Destroy(r.gameObject); return null; }
            try { text.overflowMode = TextOverflowModes.Ellipsis; } catch { }
            text.fontStyle = FontStyles.Normal; text.alignment = TextAlignmentOptions.Center; text.characterSpacing = 0f; text.color = Theme.Cream;
            return r;
        }

        // the label to clone: the Reroll button's own caption (the action row's font), else a card's reason line
        static TextMeshProUGUI Template(RectTransform button)
        {
            try
            {
                var all = button.GetComponentsInChildren(Il2CppType.Of<TextMeshProUGUI>(), false);
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i].TryCast<TextMeshProUGUI>(); if (t == null) continue;
                    try
                    {
                        if (t.font == null || !t.gameObject.activeInHierarchy || t.name.StartsWith("Yazs", StringComparison.Ordinal)) continue;
                        if (!(t.text ?? "").Any(char.IsLetter)) continue;      // the count in the diamond
                    }
                    catch { continue; }
                    return t;
                }
            }
            catch { }
            if (_cards != null)
                foreach (var c in _cards)
                {
                    try { var t = c.Button == null ? null : c.Button.transform.Find("YazsReason"); var tmp = t == null ? null : t.GetComponent<TextMeshProUGUI>(); if (tmp != null) return tmp; } catch { }
                }
            return Ui.FindLabel(button.parent, null);
        }

        // a layout group on the button or its row must leave our pieces where we put them
        static void OutOfLayout(RectTransform rt)
        {
            try { var le = rt.gameObject.AddComponent(Il2CppType.Of<LayoutElement>()).TryCast<LayoutElement>(); if (le != null) le.ignoreLayout = true; } catch { }
        }

        // the card badges' rule: the text at least MinTextPx tall, at most x1.3 (BadgeScale in the config overrides it)
        static float Scale(RectTransform view)
        {
            float fixedScale = 0; try { fixedScale = Plugin.BadgeScale.Value; } catch { }
            if (fixedScale > 0) return Mathf.Clamp(fixedScale, 0.5f, MaxScale);
            try
            {
                var canvas = view.GetComponentInParent<Canvas>();
                var crt = canvas == null ? null : canvas.rootCanvas.transform.TryCast<RectTransform>();
                float h = crt == null ? 0 : crt.rect.height; if (h <= 0) return 1f;
                return Mathf.Clamp(MinTextPx / (Font * UnityEngine.Screen.height / h), 1f, MaxScale);
            }
            catch { return 1f; }
        }

        // on appear: the frame settles onto the button, the line unfolds from its left tip, types on, and the tip pings once;
        // a new verdict on a shown hint (after a reroll) only types its words on again
        static void Entrance(RectTransform frame, RectTransform line, TextMeshProUGUI text, bool full)
        {
            try
            {
                if (full)
                {
                    Fx.Cancel("reroll:");
                    if (frame != null)
                    {
                        CanvasGroup g = null;
                        try { g = frame.GetComponent<CanvasGroup>(); if (g == null) { g = frame.gameObject.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>(); g.blocksRaycasts = false; g.interactable = false; } } catch { }
                        Fx.Run("reroll:frame", 0.05f, 0.45f, k => { float sc = 1f + 0.08f * (1f - Fx.OutCubic(k)); frame.localScale = new Vector3(sc, sc, 1f); if (g != null) g.alpha = Fx.Smooth(k * 1.6f); });
                    }
                    if (line != null)
                        Fx.Run("reroll:line", 0.15f, 0.4f, k => { line.localScale = new Vector3(Mathf.Max(0.0001f, Fx.OutBack(k)), 1f, 1f); }, () =>
                        {
                            var tip = line.Find("TipL"); var rt = tip == null ? null : tip.TryCast<RectTransform>();
                            if (rt != null) Fx.Ping("reroll:ping", rt, Tip * 1.45f, 4f, Theme.Gold, false, 0f, 0.6f, 2.6f, false);
                        });
                }
                else if (line != null) line.localScale = Vector3.one;
                if (text != null) Fx.Type("reroll:type", text, full ? 0.3f : 0.05f, 70f);
            }
            catch { }
        }
    }

    // the game hands the rescue screen its reroll / banish counts (its action buttons are refreshed through this override):
    // the count the hint trusts first. The pointer test in OnButtons is all a call costs while no rescue screen is advised.
    [HarmonyPatch(typeof(UIGameplayCharacterRescue), nameof(UIGameplayCharacterRescue.SetActionButtonsInteractivity))]
    static class P_RescueActions { static void Postfix(UIGameplayCharacterRescue __instance, int numRerolls) { RerollHint.OnButtons(__instance, numRerolls); } }
}
