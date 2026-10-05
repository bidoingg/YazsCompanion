// The mod menu's BADGES page (0.13.0, BUILDS tab -> BADGES, or the build editor's Badges row): the badge advice of the run
// setup screen, previewed for the survivor in focus as team leader - the 27 badges in the game's own 8 x 4 grid order with
// their icons, levels and the very diamonds the run setup screen draws (at the configured size), a legend, the summary rows
// as the screen shows them and the WHY of the badge in focus. On the right: "Preview for" (game mode and difficulty, this
// session only), what the run setup screen draws (= the ADVICE tab's row), its size (70 - 200 %), the one-click EQUIP ADVICE
// button (the user's OD1: off by default, switched on here). On your own build a press on a badge cycles it: PIN (gold, it
// takes a slot first, in pin order) -> NEVER (rust) -> left to the advice; CLEAR PINS empties both. Presets and Auto show the
// advice only ("MAKE MY OWN BUILD" copies the build); a build pack's pins are read-only. Every change rebuilds the page at
// once (only the diamonds that changed stamp again) and the header says SAVED.
//
// "Equipped" on the page = the selection of this session's last run setup visit, else what the run setup screen holds, else
// none (the caption says so). Over the pause menu the page shows the run being played: its leader, mode and difficulty, its
// badges as "equipped", read-only (THIS RUN: 3 of 4 as advised).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CT = GamePlayer.CharacterType;

namespace YazsCompanion
{
    internal static partial class Menu
    {
        static bool _badgesPage, _badgesFromEditor;
        static int _pvModeIx = -1, _pvDiff = 1;                    // "Preview for": session only, never saved
        static int _pgFocus = -1;
        static LoadoutAdvice _pgAdvice; static LoadoutView _pgView; static TextMeshProUGUI _pgWindow;
        static readonly Dictionary<int, string> _pgStates = new Dictionary<int, string>();     // the diamonds as drawn: a changed one stamps
        static readonly Dictionary<int, string> _pgDesc = new Dictionary<int, string>();       // the game's level text per badge, read once per open
        static readonly string[] ModeKeys = { "Normal", "Endless", "Hardcore", "Extermination", "OneHit", "Infinite", "BossRush" };

        static string ModeLabel(string mode) { return mode == "OneHit" ? "One Hit" : mode == "BossRush" ? "Boss Rush" : mode; }
        static string PreviewLabel(string mode, int diff) { return ModeLabel(mode) + " " + Loadout.RomanOf(diff); }

        static string LoadoutText(LoadoutDetail d) { return d == LoadoutDetail.NumbersAndReason ? "Numbers + reason" : Words(d.ToString()); }
        static string LoadoutHelp(LoadoutDetail d)
        {
            return (d == LoadoutDetail.Off ? "OFF: nothing is drawn on the run setup screen (the advice is still written to the log). "
                : d == LoadoutDetail.Numbers ? "NUMBERS: the diamonds and frames only. "
                : d == LoadoutDetail.Full ? "FULL: NUMBERS + REASON, plus close calls and a Training Yard hint (YARD / UNLOCK). "
                : "NUMBERS + REASON (default): ")
                + "The run setup screen numbers the badges worth equipping for the leader and run you picked (1 = most worth it), frames the ones not equipped yet, marks equipped ones that are not advised, explains the badge under the cursor and lists the swaps under CHOSEN BADGES. "
                + "Advice only: it never equips a badge unless you switch on the EQUIP ADVICE button on the BADGES page (BUILDS tab), which previews it for every survivor.";
        }

        // the preview's mode and difficulty: the last run setup visit of this session, else Normal I
        static void PreviewFor(out string mode, out int diff)
        {
            if (_pvModeIx < 0)
            {
                var last = LoadoutUi.Last;
                int ix = last == null ? 0 : Array.IndexOf(ModeKeys, last.Mode);
                _pvModeIx = Math.Max(0, ix); _pvDiff = last == null ? 1 : Math.Max(1, Math.Min(5, last.Difficulty));
            }
            mode = ModeKeys[_pvModeIx]; diff = _pvDiff;
        }

        static void StepPreview(int d)
        {
            string m; int df; PreviewFor(out m, out df);
            int i = _pvModeIx * 5 + (_pvDiff - 1) + d, n = ModeKeys.Length * 5;
            i = ((i % n) + n) % n;
            _pvModeIx = i / 5; _pvDiff = i % 5 + 1;
        }

        static void OpenBadges(bool fromEditor)
        {
            _badgesPage = true; _badgesFromEditor = fromEditor; _pgFocus = -1; _pgStates.Clear(); _pgDesc.Clear();
            _focusKey = ""; _dirty = true;
            try { if (Plugin.Verbose.Value) Plugin.Logger.LogInfo("[menu] badges page opened for " + Builds.Survivors[_survivor]); } catch { }
        }

        static void CloseBadges()
        {
            _badgesPage = false; _pgAdvice = null; _pgView = null; _pgWindow = null;
            _focusKey = _badgesFromEditor ? "ed:badges" : "lo:open";
            _dirty = true;
        }

        /// <summary>The preview walk: the cell of the first advised badge on the page.</summary>
        static string FirstAdvisedCell()
        {
            var p = _pgAdvice == null ? null : _pgAdvice.Picks.FirstOrDefault(x => !x.Forced);
            return p == null ? "" : "lo:cell:" + p.Badge.Id;
        }

        // ---------------------------------------------------------------- the advice for a survivor (page, card sentence, editor row)
        sealed class PageRun { public string Leader = "", Mode = "Normal"; public int Difficulty = 1; public List<int> Equipped = new List<int>(); }

        // over the pause menu: the run being played (its leader, mode, difficulty and badges)
        static PageRun RunOnPause()
        {
            if (MainMenu() != null || PauseMenu() == null) return null;
            try
            {
                using (G.Cache())
                {
                    var snap = G.Read();
                    var lead = snap.Squad.FirstOrDefault(s => s.Leader) ?? snap.Squad.FirstOrDefault();
                    if (lead == null) return null;
                    var r = new PageRun { Leader = lead.Name, Mode = snap.Mode, Difficulty = Math.Max(1, snap.Ctx.Difficulty) };
                    var badges = LoadoutState.RunBadges();
                    if (badges != null) r.Equipped.AddRange(badges.Select(x => x.Key.Id));
                    return r;
                }
            }
            catch { return null; }
        }

        static LoadoutAdvice AdviceFor(string who, Build b, string mode, int diff, IList<int> equipped, out Dictionary<int, int> levels)
        {
            levels = new Dictionary<int, int>();
            // in its own try: the page is built inside Menu.Tick, whose catch closes the whole menu - a badge the game changed
            // must cost the page its advice ("not readable here"), not the menu
            try
            {
                if (!LoadoutState.EnsureFacts()) return null;
                var open = new Dictionary<int, bool>(); int raised;
                LoadoutState.ReadLevels(levels, open, out raised);
                var inp = LoadoutState.Input(who, b, mode, diff, LoadoutState.SlotsNow(), levels, open, null, null, equipped);
                return Loadout.Recommend(inp, LoadoutState.All, Knowledge.Current, 0);
            }
            catch (Exception e) { LoadoutState.Once("menu:advice:" + e.GetType().Name, "the BADGES page could not work out the advice: " + e.GetBaseException().Message, true); return null; }
        }

        /// <summary>The BUILDS card's description gains one sentence: "Badges as leader (Normal II): 1 Gunner L2 · 2 Critical L2 ...".</summary>
        static string BadgeSentence(string who, Build b)
        {
            try
            {
                string mode; int diff; PreviewFor(out mode, out diff);
                Dictionary<int, int> levels;
                var a = AdviceFor(who, b, mode, diff, null, out levels);
                if (a == null) return "";
                var picks = a.Picks.Where(p => !p.Forced).ToList();
                if (picks.Count == 0) return "   Badges as leader (" + PreviewLabel(mode, diff) + "): " + (a.Note.Length > 0 ? a.Note : "none to advise") + ".";
                return "   Badges as leader (" + PreviewLabel(mode, diff) + "): " + string.Join(" · ", picks.Select(p => p.Rank + " " + LoadoutState.DisplayName(p.Badge) + " L" + Math.Max(1, p.Level) + (p.Source == "pin" ? " (pinned)" : ""))) + ".";
            }
            catch { return ""; }
        }

        /// <summary>The editor's Badges row: the pins in order, then "auto" for the slots the advice fills ("Gunner · Power · auto · auto").</summary>
        static string BadgesValue(string who, Build b)
        {
            try
            {
                var all = LoadoutState.EnsureFacts() ? LoadoutState.All : null;
                Func<string, string> nm = r => { string how; var f = all == null ? null : Loadout.Resolve(r, all, out how); return f != null ? LoadoutState.DisplayName(f) : r; };
                var parts = b.Badges.Select(nm).ToList();
                int slots = LoadoutState.SlotsNow();
                while (parts.Count < slots) parts.Add("auto");
                string s = string.Join(" · ", parts.Take(Math.Max(slots, b.Badges.Count)));
                if (b.SkipBadges.Count > 0) s += "   <color=" + Theme.DimHex + ">never: " + string.Join(", ", b.SkipBadges.Select(nm)) + "</color>";
                return s;
            }
            catch { return "auto"; }
        }

        static int PinState(Build b, BadgeFacts f, IList<BadgeFacts> all)
        {
            if (b == null) return 0;
            string how;
            if (b.Badges.Any(r => Loadout.Resolve(r, all, out how) == f)) return 1;
            if (b.SkipBadges.Any(r => Loadout.Resolve(r, all, out how) == f)) return 2;
            return 0;
        }

        // ---------------------------------------------------------------- the page
        static void BuildBadges()
        {
            var run = RunOnPause();
            if (run != null) { int ix = Array.FindIndex(Builds.Survivors, s => string.Equals(s, run.Leader, StringComparison.OrdinalIgnoreCase)); if (ix >= 0) _survivor = ix; }
            string who = Builds.Survivors[_survivor];
            var b = Builds.For(who);
            bool own = b != null && b.Custom, pack = b != null && b.Pack.Length > 0, readOnly = run != null;
            string mode; int diff;
            if (run != null) { mode = run.Mode; diff = run.Difficulty; } else PreviewFor(out mode, out diff);
            List<int> equipped; string eqFrom;
            if (run != null) { equipped = run.Equipped; eqFrom = "this run's badges"; }
            else if (LoadoutUi.Last != null) { equipped = LoadoutUi.Last.Equipped.ToList(); eqFrom = "equipped = your badges at the last run setup visit"; }
            else { equipped = LoadoutState.EquippedNow(); eqFrom = equipped.Count > 0 ? "equipped = the run setup screen's badges" : "your equipped badges show after the first visit to the run setup screen"; }

            Text(_body, "Who", 120, 350, 1800, 70, 50f, Theme.Grey, "<color=" + Theme.GoldHex + ">" + Who(who).ToUpperInvariant() + "</color>   badges as team leader   <color=" + Theme.DimHex + ">"
                + (b == null ? "Auto" : b.Name) + "</color>", TextAlignmentOptions.Left, true);
            Dictionary<int, int> levels;
            var a = AdviceFor(who, b, mode, diff, equipped, out levels);
            if (a == null)
            {
                Text(_body, "None", 120, 460, 3400, 200, 46f, Theme.Cream, "The badges are read from the game: they are not readable here yet. Open the run setup screen once (Play, then the team leader, the arena and the mode) and come back.", TextAlignmentOptions.TopLeft, false, true);
                Btn(_body, "lo:done", 120, 700, 600, 116, "DONE", null, CloseBadges, () => "Back.");
                return;
            }
            // the page always explains (WHY, summary rows) - with Off / Numbers the window says the screen itself does not
            var detail = Plugin.AdviceLoadout.Value;
            var shown = detail < LoadoutDetail.NumbersAndReason ? LoadoutDetail.NumbersAndReason : detail;
            int slots = LoadoutState.SlotsNow();
            var v = LoadoutView.Of(a, equipped, new int[0], slots, shown, Knowledge.Current, LoadoutState.DisplayName, s => Names.Class(s), "·");
            _pgAdvice = a; _pgView = v;
            var all = LoadoutState.All;

            // ---- the grid: the game's 8 x 4 order (badgeSortOrder), the diamonds at the run setup screen's size
            float gx = 120f, gy = 436f, cell = 196f, stepXY = 210f;
            float unitPx = 1f; try { var crt = _go.transform.TryCast<RectTransform>(); unitPx = UnityEngine.Screen.height / Mathf.Max(1f, crt.rect.height); } catch { }
            float msize = LoadoutLayout.MarkerUnits(143f, unitPx, Plugin.LoadoutSize.Value);
            var fresh = new Dictionary<int, string>();
            int i = 0;
            foreach (var f in all)
            {
                int col = i % 8, row = i / 8; i++;
                var facts = f;
                int lvl; levels.TryGetValue(f.Id, out lvl);
                int pin = PinState(b, f, all);
                bool locked = lvl < 1;
                var c = Plate(_body, "lo:cell:" + f.Id, gx + col * stepXY, gy + row * stepXY, cell, cell, locked ? new Color(1f, 1f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.9f));
                Sprite icon = null; try { icon = LoadoutState.IconOf(f.Ref as GameplayBadgeBase); } catch { }
                if (icon != null) Pic(c.Rt, "Icon", 40, 26, 116, 116, icon, locked ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white);
                // NEVER goes on the level row: appended to the name it was cut to "Elemental - N..." in the 184-unit cell
                string lvlText = pin == 2 ? "NEVER" + (locked ? "" : "  L" + lvl) : locked ? "LOCKED" : "L" + lvl + " / " + f.Max;
                Text(c.Rt, "Lvl", 14, 6, 168, 34, 28f, pin == 2 ? Theme.Rust : locked ? Theme.Grey : Theme.Cream, lvlText, TextAlignmentOptions.Left, true);
                Text(c.Rt, "Name", 6, 146, cell - 12, 40, 30f, pin == 2 ? Theme.Rust : Theme.White, LoadoutState.DisplayName(f), TextAlignmentOptions.Center, true);
                var m = v.MarkOf(f.Id);
                if (m != null && m.Frame) Ui.Frame(c.Rt, "Frame", -4f, 4f, Theme.Gold, 22f);
                if (m != null && m.Kind != MarkKind.None)
                {
                    var um = Ui.Marker(c.Rt, "Mark", msize, _template);
                    um.Set(m.Kind, m.Number, m.Pin || pin == 1);
                    string st = um.State; fresh[f.Id] = st;
                    string was; bool changed = !_pgStates.TryGetValue(f.Id, out was) || was != st;
                    if (changed) PageStamp(um, f.Id, _pgStates.Count == 0 ? fresh.Count : 0);
                }
                c.Desc = () => CellDesc(facts, levels);
                c.Focused = () => { _pgFocus = facts.Id; RenderWindow(); };
                c.Press = () => CyclePin(who, facts);
            }
            _pgStates.Clear(); foreach (var kv in fresh) _pgStates[kv.Key] = kv.Value;

            // ---- the legend, real mini diamonds (under the last grid row: a game patch with more badges adds a row)
            float ly = gy + Math.Max(4, (all.Count + 7) / 8) * stepXY + 6f, lx = gx;
            Legend(ref lx, ly, MarkKind.Equip, 1, true, "advised, not equipped yet");
            Legend(ref lx, ly, MarkKind.Keep, 2, false, "advised, equipped");
            Legend(ref lx, ly, MarkKind.SwapOut, 0, false, "swap out");
            Legend(ref lx, ly, MarkKind.Forced, 0, false, "the quest's");
            Legend(ref lx, ly, MarkKind.Close, 0, false, "close call (Full)");
            string rule = readOnly ? "The run being played: read-only."
                : own ? "Press a badge: PIN it (gold tick, it takes a slot first) - NEVER (rust) - back to the advice."
                : pack ? "This build comes from " + b.Pack + ": its pins are read-only."
                : "Pins belong to your own build - MAKE MY OWN BUILD copies this one.";
            Text(_body, "Caption", gx, ly + 92f, 1680, 150, 38f, Theme.Grey, eqFrom + ".\n" + rule, TextAlignmentOptions.TopLeft, false, true);

            // ---- the right side: preview for, the screen's text, the settings
            float rx = 1960f, rw = 1760f, y = 352f, rh = 118f, step = 132f;
            if (run != null)
            {
                int k = v.AsAdvised, n = v.AdvisedCount;
                Text(_body, "ThisRun", rx, y + 84f, rw, 80, 50f, Theme.GoldText, "THIS RUN: " + k + " of " + n + " as advised   <color=" + Theme.DimHex + ">" + PreviewLabel(mode, diff) + "</color>", TextAlignmentOptions.Left, true);
            }
            else
                Cycler(_body, "lo:preview", rx, y + 84f, rw, rh, "Preview for", "skull", () => { string pm; int pd; PreviewFor(out pm, out pd); return PreviewLabel(pm, pd); },
                    d => { StepPreview(d); _focusKey = "lo:preview"; _dirty = true; },
                    () => "The game mode and difficulty the advice is worked out for (this page only, not saved). It starts at the last run setup screen of this session.", 760f);
            y += 84f + step;
            var window = Box(_body, "Window", rx, y, rw, 560, new Color(0f, 0f, 0f, 0.5f));
            Ui.Frame(window, "Edge", 0, 3, Theme.GoldRule, 18f);
            _pgWindow = Text(window, "Text", 40, 26, rw - 80, 510, 40f, Theme.White, "", TextAlignmentOptions.TopLeft, false, true);
            y += 560f + 28f;
            Cycler(_body, "lo:hint", rx, y, rw, rh, "On the run setup screen", "eye", () => LoadoutText(Plugin.AdviceLoadout.Value),
                d => { Plugin.AdviceLoadout.Value = Next(Plugin.AdviceLoadout.Value, d); _focusKey = "lo:hint"; _dirty = true; }, () => LoadoutHelp(Plugin.AdviceLoadout.Value), 760f);
            y += step;
            Cycler(_body, "lo:size", rx, y, rw, rh, "Size", null, () => Mathf.RoundToInt(Plugin.LoadoutSize.Value * 100f) + " %" + (Mathf.Abs(Plugin.LoadoutSize.Value - 1f) < 0.01f ? "  (automatic)" : ""),
                d => { Plugin.LoadoutSize.Value = SizeStep(Plugin.LoadoutSize.Value, d); _focusKey = "lo:size"; _dirty = true; },
                () => "How large the run setup screen's diamonds, WHY line and summary are drawn. 100 % is the automatic size: the text is 1.9 % of the screen's height and never under 15 pixels; the diamonds are 40 % of a badge button (more on small screens, so their number stays readable), at most 60 %. The diamonds here are at that size.", 760f);
            y += step;
            Cycler(_body, "lo:equip", rx, y, rw, rh, "One-click EQUIP ADVICE", "chevrons", () => Plugin.AdviceLoadoutEquip.Value ? "On" : "Off",
                d => { Plugin.AdviceLoadoutEquip.Value = !Plugin.AdviceLoadoutEquip.Value; _focusKey = "lo:equip"; _dirty = true; },
                () => "ON: the run setup screen shows an EQUIP ADVICE button next to CHOSEN BADGES. A click on it" + (string.IsNullOrWhiteSpace(Plugin.AdviceLoadoutEquipKey.Value) ? "" : " (or " + Plugin.AdviceLoadoutEquipKey.Value.Trim() + ")") + " presses the game's own badge buttons for you along the advice - removes first, then adds; never a quest's badge, never a locked one - exactly as clicks by hand would (the game saves the selection as usual). UNDO until the screen closes. OFF (default): advice only.", 760f);
            y += step;
            if (readOnly) Btn(_body, "lo:done", rx, y, rw, rh, "DONE", null, CloseBadges, () => "Back.");
            else
            {
                float bw = (rw - 30f) / 2f;
                if (own)
                    Btn(_body, "lo:clear", rx, y, bw, rh, "CLEAR PINS", null, () =>
                    {
                        b.Badges.Clear(); b.SkipBadges.Clear(); Builds.Save(); SettingSaved(Builds.LastSaveOk); PageLog(who, b, mode, diff);
                        _focusKey = "lo:clear"; _dirty = true;
                    }, () => "Remove every pin and every NEVER mark from your build: the advice decides every slot again.");
                else if (!pack)
                    Btn(_body, "lo:own", rx, y, bw, rh, "MAKE MY OWN BUILD", "gear", () =>
                    {
                        var mine = Builds.EnsureCustom(who); Builds.Select(who, mine.Id); SettingSaved(Builds.LastSaveOk);
                        Plugin.Logger.LogInfo("[menu] badges page: " + who + " now follows " + mine.Name + (Builds.LastSaveOk ? " (saved)" : " (builds.json was NOT written)"));
                        _focusKey = "lo:own"; _dirty = true;
                    }, () => "Copy the build " + Who(who) + " follows into your own build, then pin badges here (or edit it on the BUILDS tab).");
                Btn(_body, "lo:done", rx + bw + 30f, y, bw, rh, "DONE", null, CloseBadges, () => _badgesFromEditor ? "Back to your build." : "Back to the builds.");
            }
            if (_pgFocus < 0) { var first = a.Picks.FirstOrDefault(p => !p.Forced); _pgFocus = first != null ? first.Badge.Id : -1; }
            RenderWindow();
        }

        // a legend entry: a real mini diamond and its words
        static void Legend(ref float x, float y, MarkKind kind, int number, bool frame, string words)
        {
            // a stand-in badge button (a dark tile, the gold frame when the state has one) with the diamond on its top-right corner
            var host = Box(_body, "Legend" + kind, x, y + 14f, 60f, 60f, new Color(1f, 1f, 1f, 0.08f));
            if (frame) Ui.Frame(host, "Frame", -3f, 3f, Theme.Gold, 12f);
            var m = Ui.Marker(host, "Mark", 40f, _template);
            m.Set(kind, number, false);
            float tw = words.Length * 15f + 20f;
            Text(_body, "LegendText" + kind, x + 96f, y + 20f, tw, 50f, 30f, Theme.Grey, words, TextAlignmentOptions.Left, false);
            x += 96f + tw;
        }

        static void PageStamp(UiMarker m, int id, int index)
        {
            var root = m.Root; var turn = m.Turn; var group = m.Group;
            if (!Fx.On) return;
            string key = "menu.badges:" + id;
            Fx.Cancel(key);
            Fx.Run(key, 0.05f + 0.06f * index, 0.32f, k =>
            {
                float s = Mathf.LerpUnclamped(2.0f, 1f, Fx.OutBack(k));
                root.localScale = new Vector3(s, s, 1f);
                if (group != null) group.alpha = Mathf.Clamp01(k * 3.5f);
                turn.localRotation = Quaternion.Euler(0, 0, 180f * (1f - Fx.OutCubic(k)));
            });
        }

        // the footer of a cell: the game's own words for the badge at its level, then the advice's
        static string CellDesc(BadgeFacts f, Dictionary<int, int> levels)
        {
            string text;
            if (!_pgDesc.TryGetValue(f.Id, out text))
            {
                text = "";
                try { var g = f.Ref as GameplayBadgeBase; if (g != null) text = LoadoutState.LevelText(g) ?? ""; } catch { }
                text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "").Replace("\n", "  ").Trim();
                _pgDesc[f.Id] = text;
            }
            int lvl; levels.TryGetValue(f.Id, out lvl);
            var m = _pgView == null ? null : _pgView.MarkOf(f.Id);
            string why = m == null ? "" : LoadoutView.Visible(Names.Text(m.Why1)) + (m.Why2.Length > 0 ? "  (" + Names.Text(m.Why2) + ")" : "");
            return LoadoutState.DisplayName(f).ToUpperInvariant() + (lvl >= 1 ? " L" + lvl + " / " + f.Max : " - locked (" + Names.Class(f.Owner) + " tree, rank " + f.Rank + ")")
                + (text.Length > 0 ? ": " + text : "") + (why.Length > 0 ? "   " + why : "");
        }

        // the text window: the summary rows as the run setup screen shows them, then the WHY of the badge in focus
        static void RenderWindow()
        {
            if (_pgWindow == null || _pgView == null) return;
            try
            {
                var lines = new List<string>();
                var detail = Plugin.AdviceLoadout.Value;
                lines.Add("<size=30><color=" + Theme.DimHex + ">" + (detail == LoadoutDetail.Off ? "THE ADVICE  -  THE RUN SETUP SCREEN SHOWS NOTHING (OFF)"
                    : detail == LoadoutDetail.Numbers ? "THE ADVICE  -  THE RUN SETUP SCREEN SHOWS ONLY THE DIAMONDS (NUMBERS)" : "AS ON THE RUN SETUP SCREEN") + "</color></size>");
                foreach (var r in _pgView.Rows) lines.Add(LoadoutUi.RowText(r));
                if (_pgAdvice != null && _pgAdvice.Note.Length > 0) lines.Add("<color=" + Theme.DimHex + ">" + _pgAdvice.Note + "</color>");
                var m = _pgFocus >= 0 ? _pgView.MarkOf(_pgFocus) : null;
                if (m != null)
                {
                    var f = LoadoutState.All == null ? null : LoadoutState.All.FirstOrDefault(x => x.Id == _pgFocus);
                    lines.Add("");
                    lines.Add("<size=30><color=" + Theme.DimHex + ">WHY  -  " + (f != null ? LoadoutState.DisplayName(f).ToUpperInvariant() : "") + "</color></size>");
                    lines.Add(Names.Text(m.Why1));
                    if (m.Why2.Length > 0) lines.Add("<color=" + Theme.DimHex + ">" + Names.Text(m.Why2) + "</color>");
                }
                _pgWindow.text = string.Join("\n", lines);
            }
            catch { }
        }

        static void CyclePin(string who, BadgeFacts f)
        {
            if (RunOnPause() != null) { SetDesc("The run being played: read-only."); return; }
            var b = Builds.For(who);
            if (b == null || !b.Custom) { SetDesc(b != null && b.Pack.Length > 0 ? "This build comes from " + b.Pack + ": its pins are read-only." : "Pins belong to your own build - MAKE MY OWN BUILD copies this one."); return; }
            var all = LoadoutState.All; if (all == null) return;
            int state = PinState(b, f, all);
            string how;
            b.Badges.RemoveAll(r => Loadout.Resolve(r, all, out how) == f);
            b.SkipBadges.RemoveAll(r => Loadout.Resolve(r, all, out how) == f);
            if (state == 0)
            {
                if (b.Badges.Count >= Builds.MaxBadgeRefs) { SetDesc("At most " + Builds.MaxBadgeRefs + " pins."); return; }
                b.Badges.Add(f.Short);
            }
            else if (state == 1) b.SkipBadges.Add(f.Short);
            Builds.Save(); SettingSaved(Builds.LastSaveOk);
            string mode; int diff; PreviewFor(out mode, out diff);
            PageLog(who, b, mode, diff);
            _pgFocus = f.Id; _focusKey = "lo:cell:" + f.Id; _dirty = true;
        }

        static void PageLog(string who, Build b, string mode, int diff)
        {
            Plugin.Logger.LogInfo("[menu] badges page: " + who + " (" + b.Name + "), preview " + mode + " " + Loadout.RomanOf(diff) + ", pins " + (b.Badges.Count > 0 ? string.Join(", ", b.Badges) : "-")
                + ", never " + (b.SkipBadges.Count > 0 ? string.Join(", ", b.SkipBadges) : "-") + (Builds.LastSaveOk ? " (saved)" : " (builds.json was NOT written)"));
        }
    }
}
