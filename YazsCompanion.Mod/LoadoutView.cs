// What the run setup screen shows for one advice (0.13.0), as data: a mark per badge (the truth table of SPEC 2.1), the
// slot mirror, the swaps (equipped out <-> advised in) with the keep margin, the summary rows under CHOSEN BADGES, the
// WHY line per badge - and the EQUIP plan: the clicks that would turn the equipped set into the advised one (removes
// first, then adds; never a forced or a locked badge; never past the slots) with its UNDO. LoadoutUi.cs draws it,
// LoadoutEquip.cs (the optional one-click button, off by default) presses the game's own badge button along the plan.
//
// The advice itself (Loadout.Recommend) ignores the equipped set; the keep margin lives here: a swap is shown only when
// it gains at least max(badgeRules.swapMargin, swapMarginShare x the equipped badge's points). Once a pair fails, every
// later pair fails too (they are paired lowest equipped with highest advised), and the equipped badge stays - "KEEP · close"
// - carrying the number of the advised badge it stands in for, so the numbers stay 1..n with no gaps.
//
// Pure C# (no game types), linked into the offline bench.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace YazsCompanion
{
    internal enum MarkKind { None, Keep, KeepClose, Equip, SwapOut, Forced, Close, Pinned }

    /// <summary>[Advice] LoadoutHint: what the run setup screen draws. Off still logs.</summary>
    internal enum LoadoutDetail { Off, Numbers, NumbersAndReason, Full }

    internal sealed class BadgeMark
    {
        public int Id = -1;                         // -1 = an empty slot
        public MarkKind Kind;
        public int Number;                          // 0 = no number
        public bool Frame, Pin, Hollow, Equipped, Locked, Rated = true, Advised;
        public string Tag = "";                     // KEEP, EQUIP, SWAP OUT, KEEP · close, QUEST, CLOSE CALL, PINNED, LOCKED, NOT RATED, #7 of 18
        public string Why1 = "", Why2 = "";         // the WHY line: Why1 = "<b>#1 EQUIP</b>  reason", Why2 = the dim second line
        public bool Drawn { get { return Kind != MarkKind.None; } }
        public override string ToString() { return Id + ":" + Kind + (Number > 0 ? "#" + Number : ""); }
    }

    internal sealed class EquipStep
    {
        public int Id;
        public bool Add;
        public override string ToString() { return (Add ? "+" : "-") + Id; }
    }

    /// <summary>An EQUIP (or UNDO) run of the one-click button (OD1, off by default), one press at a time: what the selection must
    /// be before each press - a click by hand in between stops the run - and the UNDO of the presses made so far. Pure:
    /// LoadoutEquip.cs reads the live selection, checks its own live guards, presses the game's badge button and logs each press.</summary>
    internal sealed class EquipRun
    {
        public readonly List<int> Start;
        public readonly List<EquipStep> Steps;
        public readonly List<int> Forced;
        public readonly Func<int, bool> Unlocked;
        public readonly int Slots;
        public int Done;
        public string Stopped = "";                 // why the run stopped early ("" = it did not)

        public EquipRun(IList<int> start, IList<EquipStep> steps, ICollection<int> forced, Func<int, bool> unlocked, int slots)
        {
            Start = (start ?? new int[0]).ToList(); Steps = (steps ?? new EquipStep[0]).ToList(); Forced = (forced ?? new int[0]).ToList(); Unlocked = unlocked; Slots = slots;
        }

        public bool Finished { get { return Done >= Steps.Count && Stopped.Length == 0; } }
        public bool Running { get { return Done < Steps.Count && Stopped.Length == 0; } }
        /// <summary>The selection the presses made so far lead to.</summary>
        public List<int> Expected { get { return LoadoutView.Apply(Start, Steps.Take(Done), Forced, Unlocked, Slots); } }

        static bool SameSet(IEnumerable<int> a, IEnumerable<int> b) { return new HashSet<int>(a ?? new int[0]).SetEquals(b ?? new int[0]); }

        /// <summary>The next press, or null when the run is over or must stop: the live selection is not what the presses so far lead
        /// to (the player clicked), or the press would remove a forced badge, add a locked one, go past the slots or toggle the
        /// wrong way. <see cref="Stopped"/> then says why.</summary>
        public EquipStep Next(IList<int> live)
        {
            if (!Running) return null;
            live = live ?? new int[0];
            if (!SameSet(live, Expected)) { Stopped = "the selection changed by hand before press " + (Done + 1); return null; }
            var s = Steps[Done];
            if (!s.Add && Forced.Contains(s.Id)) { Stopped = "press " + (Done + 1) + " would remove the forced badge " + s.Id; return null; }
            if (s.Add && Unlocked != null && !Unlocked(s.Id)) { Stopped = "press " + (Done + 1) + " would add the locked badge " + s.Id; return null; }
            if (s.Add && live.Count >= Slots) { Stopped = "press " + (Done + 1) + " would go past the " + Slots + " slots"; return null; }
            if (s.Add == live.Contains(s.Id)) { Stopped = "press " + (Done + 1) + " would toggle badge " + s.Id + " the wrong way"; return null; }
            return s;
        }

        /// <summary>After a press: <paramref name="live"/> = the selection the game shows now (it must be what the press leads to).</summary>
        public void Pressed(IList<int> live)
        {
            if (!Running) return;
            Done++;
            if (!SameSet(live, Expected)) Stopped = "press " + Done + " did not take (the selection is " + string.Join(",", live ?? new int[0]) + ")";
        }

        /// <summary>The run that takes back the presses made so far (offered until the screen closes).</summary>
        public EquipRun Undo() { return new EquipRun(Expected, LoadoutView.UndoOf(Steps.Take(Done).ToList()), Forced, Unlocked, Slots); }
    }

    internal sealed class SummaryItem { public string Text = ""; public bool Equipped; }

    internal sealed class SummaryRow
    {
        public string Kind = "";                    // equip | swap | match | quest | close | free | none | yard | unlock
        public string Label = "";                   // "EQUIP", "SWAP OUT", ... or a whole sentence when there are no items
        public readonly List<SummaryItem> Items = new List<SummaryItem>();
        public string Sep = "·";

        /// <summary>The row as text; rich = equipped names in <paramref name="equippedHex"/>, missing ones in <paramref name="missingHex"/>.</summary>
        public string Text(bool rich = false, string equippedHex = "#FFFFFF", string missingHex = "#F5C752")
        {
            if (Items.Count == 0) return Label;
            var parts = Items.Select(i => rich ? "<color=" + (i.Equipped ? equippedHex : missingHex) + ">" + i.Text + "</color>" : i.Text);
            return Label + "  " + string.Join(" " + Sep + " ", parts);
        }
        public override string ToString() { return Text(); }
    }

    internal sealed class LoadoutView
    {
        public LoadoutAdvice Advice;
        public LoadoutDetail Detail;
        public readonly Dictionary<int, BadgeMark> Marks = new Dictionary<int, BadgeMark>();       // every badge of the advice
        public readonly List<BadgeMark> Grid = new List<BadgeMark>();                              // in badgeSortOrder (the grid's order)
        public readonly List<BadgeMark> Slots = new List<BadgeMark>();                             // one per slot, in the equipped order; empty slots last
        public readonly List<KeyValuePair<int, int>> Swaps = new List<KeyValuePair<int, int>>();   // equipped out -> advised in
        public readonly List<KeyValuePair<int, int>> Kept = new List<KeyValuePair<int, int>>();    // equipped kept -> the advised badge it stands in for (gain under the margin)
        public readonly List<int> Free = new List<int>();                                          // advised badges for free slots (nothing to swap out)
        public readonly List<int> Target = new List<int>();                                        // the set the plan leads to
        public readonly List<EquipStep> Plan = new List<EquipStep>(), Undo = new List<EquipStep>();
        public readonly List<SummaryRow> Rows = new List<SummaryRow>();
        public readonly List<int> Equipped = new List<int>(), Forced = new List<int>();
        public bool Matches;
        public int SlotCount, AsAdvised, AdvisedCount;

        public BadgeMark MarkOf(int id) { BadgeMark m; return Marks.TryGetValue(id, out m) ? m : null; }

        /// <summary>The WHY line of a badge: { line 1 (rich: the tag in bold), line 2 (dim) }; empty below NumbersAndReason.</summary>
        public string[] WhyText(int id) { var m = MarkOf(id); return m == null ? new[] { "", "" } : new[] { m.Why1, m.Why2 }; }

        /// <summary>The state of one badge on the screen (SPEC 2.1). CLOSE CALL marks come only with Full; a locked badge is never
        /// advised (Recommend makes sure; asking for it here gives None).</summary>
        public static MarkKind StateOf(bool advised, bool equipped, bool forced, bool locked, bool pinned, bool keptClose, bool closeCall, LoadoutDetail detail)
        {
            if (detail == LoadoutDetail.Off) return MarkKind.None;
            if (forced) return MarkKind.Forced;
            if (advised && locked) return MarkKind.None;
            if (advised)
            {
                if (closeCall) return detail == LoadoutDetail.Full ? MarkKind.Close : MarkKind.None;     // the equipped badge stands in for it
                if (pinned) return MarkKind.Pinned;
                return equipped ? MarkKind.Keep : MarkKind.Equip;
            }
            if (equipped) return keptClose ? MarkKind.KeepClose : MarkKind.SwapOut;
            if (closeCall) return detail == LoadoutDetail.Full ? MarkKind.Close : MarkKind.None;
            return MarkKind.None;
        }

        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static string F2(double x) { return Loadout.Fixed(x, 2); }

        /// <summary>The view of an advice against the equipped set. <paramref name="name"/> = the badge's display name (default: the
        /// short name; the UI passes the game's localized name without " Badge"), <paramref name="className"/> = Names.Class at draw
        /// time, <paramref name="sep"/> = "·" or a fallback when the font lacks it.</summary>
        public static LoadoutView Of(LoadoutAdvice a, IList<int> equipped, IList<int> forced, int slots, LoadoutDetail detail, Knowledge k,
            Func<BadgeFacts, string> name = null, Func<string, string> className = null, string sep = "·")
        {
            k = k ?? Knowledge.Current;
            name = name ?? (b => b.Short);
            className = className ?? (c => c);
            sep = string.IsNullOrEmpty(sep) ? "·" : sep;
            var rules = k.BadgeRules;
            var v = new LoadoutView { Advice = a, Detail = detail, SlotCount = Math.Max(0, slots) };
            v.Equipped.AddRange((equipped ?? new int[0]).Distinct());
            v.Forced.AddRange(a.Picks.Where(p => p.Forced).Select(p => p.Badge.Id));
            foreach (var f in forced ?? new int[0]) if (!v.Forced.Contains(f)) v.Forced.Add(f);
            var advised = a.Picks.Where(p => !p.Forced).ToList();
            v.AdvisedCount = advised.Count;
            v.AsAdvised = advised.Count(p => v.Equipped.Contains(p.Badge.Id));
            Func<int, double> scoreOf = id => { var r = a.RowOf(id); return r != null ? r.Score : 0; };

            // swaps: the lowest-scored equipped badge that is not advised out for the highest advised one that is not equipped, in turn
            var outs = v.Equipped.Where(id => !v.Forced.Contains(id) && a.PickOf(id) == null).Select((id, i) => new { id, i, s = scoreOf(id) })
                .OrderBy(x => x.s).ThenBy(x => x.i).Select(x => x.id).ToList();
            var ins = advised.Where(p => !v.Equipped.Contains(p.Badge.Id)).ToList();
            bool failed = false;
            for (int i = 0; i < ins.Count; i++)
            {
                var p = ins[i];
                if (i >= outs.Count) { v.Free.Add(p.Badge.Id); continue; }
                int o = outs[i];
                double gain = (Loadout.Centi(p.Score) - Loadout.Centi(scoreOf(o))) / 100.0;
                double need = Math.Max(rules.SwapMargin, rules.SwapMarginShare * scoreOf(o));
                bool pin = p.Source == "pin";                            // the player's own pin is never second-guessed
                if (!pin && (failed || gain < need - 1e-9)) { failed = true; v.Kept.Add(new KeyValuePair<int, int>(o, p.Badge.Id)); }
                else v.Swaps.Add(new KeyValuePair<int, int>(o, p.Badge.Id));
            }
            v.Matches = advised.Count > 0 && v.AsAdvised == advised.Count;

            // the set the plan leads to: the equipped badges, minus the swapped-out ones, plus the swapped-in and the free-slot ones
            foreach (var id in v.Equipped) if (!v.Swaps.Any(s => s.Key == id)) v.Target.Add(id);
            foreach (var s in v.Swaps) v.Target.Add(s.Value);
            foreach (var id in v.Free) v.Target.Add(id);
            Func<int, bool> unlocked = id => { var r = a.RowOf(id); return r != null && r.Unlocked; };
            v.Plan.AddRange(EquipPlan(v.Equipped, v.Target, v.Forced, unlocked, v.SlotCount));
            v.Undo.AddRange(UndoOf(v.Plan));

            // marks
            var ranked = a.All.Where(r => r.Unlocked && r.Rated).ToList();
            var closeTo = new HashSet<int>(a.Picks.Where(p => p.Close && p.CloseTo != null).Select(p => p.CloseTo.Id));
            foreach (var row in a.Rows)
            {
                int id = row.Badge.Id;
                var pick = a.PickOf(id);
                bool isAdvised = pick != null && !pick.Forced, isEquipped = v.Equipped.Contains(id), isForced = v.Forced.Contains(id);
                var kept = v.Kept.FirstOrDefault(x => x.Key == id);
                bool keptClose = v.Kept.Any(x => x.Key == id), closeCall = v.Kept.Any(x => x.Value == id) || (!isAdvised && !isEquipped && closeTo.Contains(id));
                var m = new BadgeMark { Id = id, Equipped = isEquipped, Locked = !row.Unlocked, Rated = row.Rated, Advised = isAdvised };
                m.Kind = StateOf(isAdvised, isEquipped, isForced, !row.Unlocked, pick != null && pick.Source == "pin", keptClose, closeCall, detail);
                // an equipped badge with no advised badge to make room for (no slots left to advise, a short advice) stays unmarked
                if (m.Kind == MarkKind.SwapOut && !v.Swaps.Any(x => x.Key == id)) m.Kind = MarkKind.None;
                switch (m.Kind)
                {
                    case MarkKind.Keep: m.Number = pick.Rank; break;
                    case MarkKind.Equip: m.Number = pick.Rank; m.Frame = true; break;
                    case MarkKind.Pinned: m.Number = pick.Rank; m.Pin = true; m.Frame = !isEquipped; break;
                    case MarkKind.KeepClose: m.Number = a.PickOf(kept.Value).Rank; m.Hollow = true; break;
                    case MarkKind.SwapOut: case MarkKind.Forced: case MarkKind.Close: m.Hollow = true; break;
                }
                // the tag says the state even when nothing is drawn for it (the WHY line explains every badge)
                if (isForced) m.Tag = "QUEST";
                else if (isAdvised && v.Kept.Any(x => x.Value == id)) m.Tag = "CLOSE CALL";
                else if (isAdvised && pick.Source == "pin") m.Tag = "#" + pick.Rank + " PINNED";
                else if (isAdvised) m.Tag = "#" + pick.Rank + (isEquipped ? " KEEP" : " EQUIP");
                else if (keptClose) m.Tag = "#" + a.PickOf(kept.Value).Rank + " KEEP " + sep + " close";
                else if (isEquipped && v.Swaps.Any(x => x.Key == id)) m.Tag = "SWAP OUT";
                else if (isEquipped) m.Tag = "KEEP";
                else if (!row.Unlocked) m.Tag = "LOCKED";
                else if (!row.Rated) m.Tag = "NOT RATED";
                else if (closeCall) m.Tag = "CLOSE CALL";
                else m.Tag = "#" + (ranked.IndexOf(row) + 1) + " of " + ranked.Count;
                if (detail >= LoadoutDetail.NumbersAndReason) WhyOf(v, m, row, pick, name, className, sep, k);
                v.Marks[id] = m; v.Grid.Add(m);
            }
            // the slot mirror: numbered for what stays, rust for what goes; forced and empty slots get nothing
            foreach (var id in v.Equipped)
            {
                var gm = v.MarkOf(id);
                var sm = new BadgeMark { Id = id, Equipped = true };
                if (gm != null && (gm.Kind == MarkKind.Keep || gm.Kind == MarkKind.KeepClose || gm.Kind == MarkKind.Pinned || gm.Kind == MarkKind.SwapOut))
                { sm.Kind = gm.Kind; sm.Number = gm.Number; sm.Hollow = gm.Hollow; sm.Pin = gm.Pin; sm.Tag = gm.Tag; }
                v.Slots.Add(sm);
            }
            while (v.Slots.Count < v.SlotCount) v.Slots.Add(new BadgeMark { Id = -1 });
            if (detail >= LoadoutDetail.NumbersAndReason) SummaryOf(v, name, className, sep, k);
            return v;
        }

        static string Clip(string text, int max)
        {
            if (text.Length <= max) return text;
            int cut = text.LastIndexOf(' ', Math.Max(0, max - 3));
            return (cut > max / 2 ? text.Substring(0, cut) : text.Substring(0, max - 3)).TrimEnd(' ', ',', ':') + "...";
        }
        /// <summary>The visible characters of a rich text (tags removed).</summary>
        public static string Visible(string rich) { return Regex.Replace(rich ?? "", "<[^>]*>", ""); }

        static void WhyOf(LoadoutView v, BadgeMark m, LoadoutRow row, LoadoutPick pick, Func<BadgeFacts, string> name, Func<string, string> className, string sep, Knowledge k)
        {
            var a = v.Advice;
            var shape = a.Input != null ? a.Input.Shape : null;
            string why = pick != null ? pick.Why : Loadout.WhyOf(row, a, k);
            int room = 60 - m.Tag.Length - 2;
            m.Why1 = "<b>" + m.Tag + "</b>  " + Clip(why, Math.Max(10, room));
            // 0.14.0 (A1): "level 2 · score 6.8", "replaces Thunder (+17 score)" - up to 0.13.0 "L2 · 6.82 points", "in for Thunder (+17.15)"
            string L = "level " + Math.Max(row.Level, 1);
            string s = " " + sep + " ";
            Func<int, string> nm = id => { var r = a.RowOf(id); return r != null ? name(r.Badge) : id.ToString(IC); };
            if (v.Forced.Contains(row.Badge.Id)) { m.Why2 = pick != null && pick.Why2.Length > 0 ? pick.Why2 : "the game keeps it in for this run"; return; }
            if (pick != null)
            {
                var sw = v.Swaps.FirstOrDefault(x => x.Value == row.Badge.Id);
                var kp = v.Kept.FirstOrDefault(x => x.Value == row.Badge.Id);
                if (v.Kept.Any(x => x.Value == row.Badge.Id))
                    m.Why2 = "#" + pick.Rank + " by score, but your " + nm(kp.Key) + " is only " + Behind((Loadout.Centi(pick.Score) - Loadout.Centi(a.RowOf(kp.Key).Score)) / 100.0) + " behind - keep it";      // "only 0.3 score behind"
                else if (pick.Source == "pin") m.Why2 = Loadout.PinnedOn(shape) + s + L;
                else if (v.Swaps.Any(x => x.Value == row.Badge.Id)) m.Why2 = L + s + "replaces " + nm(sw.Key) + " (" + Gain((Loadout.Centi(pick.Score) - Loadout.Centi(a.RowOf(sw.Key).Score)) / 100.0) + ")";
                else if (v.Free.Contains(row.Badge.Id)) m.Why2 = L + s + "into a free slot";
                else m.Why2 = L + s + "score " + F1(pick.Score);
                if (pick.Close && pick.CloseTo != null && !v.Kept.Any(x => x.Value == row.Badge.Id)) m.Why2 += s + "close call: " + name(pick.CloseTo) + " (score " + F1(a.RowOf(pick.CloseTo.Id).Score) + ")";
                return;
            }
            if (v.Kept.Any(x => x.Key == row.Badge.Id))
            {
                var kp = v.Kept.First(x => x.Key == row.Badge.Id); var ap = a.PickOf(kp.Value);
                m.Why2 = L + s + nm(kp.Value) + " would add only " + Gain((Loadout.Centi(ap.Score) - Loadout.Centi(row.Score)) / 100.0) + " - keep it";
                return;
            }
            if (v.Swaps.Any(x => x.Key == row.Badge.Id))
            {
                // 0.15.x (C-m6 of the 10-07 review): line 1 led with the badge's own merit ("SWAP OUT  survival picks always help"), which
                // argued for keeping it; it leads with the comparison now ("SWAP OUT  Critical scores more here (+1.5)"), the merit on line 2
                var sw = v.Swaps.First(x => x.Key == row.Badge.Id); var ap = a.PickOf(sw.Value);
                m.Why1 = "<b>" + m.Tag + "</b>  " + Clip(nm(sw.Value) + " scores more here (" + Plus((Loadout.Centi(ap.Score) - Loadout.Centi(row.Score)) / 100.0) + ")", Math.Max(10, room));
                m.Why2 = L + s + Clip(why, 44);
                return;
            }
            if (!row.Rated) { m.Why2 = "the Companion cannot read its effect"; return; }
            if (!row.Unlocked)
            {
                var u = a.Unlocks.FirstOrDefault(x => x.Row == row);
                m.Why2 = "unlock it in the " + className(row.Badge.Owner) + " tree (rank " + row.Badge.Rank + ")" + (u != null ? (u.Pinned ? s + Loadout.PinnedOn(shape) : s + "would be #" + u.WouldBe) : "");
                return;
            }
            // the reason itself is on line 1 already ("#16 of 18  nothing in this run deals Electric"): line 2 says what it means
            if (row.Score <= 0.05) { m.Why2 = L + s + "never advised (score " + F1(row.Score) + ")"; return; }
            if (row.Skipped) { m.Why2 = shape != null && shape.Auto ? "never on the " + shape.BuildName + " build" : "you marked it never on your build"; return; }
            if (m.Equipped) { m.Why2 = L + s + "fills a free slot"; return; }
            var last = a.Picks.LastOrDefault(p => !p.Forced);
            m.Why2 = L + s + "score " + F1(row.Score) + (last != null ? " (the #" + last.Rank + " badge scores " + F1(last.Score) + ")" : "");
        }

        // a badge's score on the WHY line: one decimal; a gain or a gap, "+17 score" from 10 up, "+0.9 score" under it
        static string F1(double x) { return x.ToString("0.0", IC); }
        static string Gain(double d) { return Plus(d) + " score"; }
        static string Plus(double d) { return "+" + (Math.Abs(d) >= 10 ? Math.Round(d).ToString("0", IC) : d.ToString("0.0", IC)); }
        static string Behind(double d) { return d < 0.05 ? "a hair" : d.ToString("0.0", IC) + " score"; }

        static void SummaryOf(LoadoutView v, Func<BadgeFacts, string> name, Func<string, string> className, string sep, Knowledge k)
        {
            var a = v.Advice;
            var advised = a.Picks.Where(p => !p.Forced).ToList();
            var r1 = new SummaryRow { Kind = "equip", Label = "EQUIP", Sep = sep };
            foreach (var p in advised) r1.Items.Add(new SummaryItem { Text = p.Rank + " " + name(p.Badge), Equipped = v.Equipped.Contains(p.Badge.Id) });
            if (advised.Count == 0)
            {
                r1.Kind = "none";
                r1.Label = a.Notes.Contains("no badge slots") ? "NO BADGE SLOTS" : a.Notes.Contains("the quest fixes every badge") ? "THE QUEST FIXES EVERY BADGE"
                    : a.Notes.Contains("no badges unlocked yet") ? "UNLOCK BADGES IN THE TRAINING YARD FIRST" : "NO BADGE TO ADVISE";      // ~20 em: fits the Deck's row at its 15 px floor
            }
            v.Rows.Add(r1);
            if (advised.Count > 0)
            {
                SummaryRow r2;
                if (v.Matches) r2 = new SummaryRow { Kind = "match", Label = "YOUR BADGES MATCH THE ADVICE", Sep = sep };
                else if (v.Swaps.Count > 0)
                {
                    r2 = new SummaryRow { Kind = "swap", Label = "SWAP OUT", Sep = sep };
                    foreach (var s in v.Swaps) r2.Items.Add(new SummaryItem { Text = name(a.RowOf(s.Key).Badge), Equipped = true });
                }
                else if (v.Kept.Count > 0)
                {
                    r2 = new SummaryRow { Kind = "close", Label = "CLOSE CALLS - KEEP", Sep = sep };
                    foreach (var s in v.Kept) r2.Items.Add(new SummaryItem { Text = name(a.RowOf(s.Key).Badge), Equipped = true });
                }
                else r2 = new SummaryRow { Kind = "free", Label = "NOTHING TO SWAP OUT - FILL THE FREE SLOTS", Sep = sep };
                v.Rows.Add(r2);
            }
            if (v.Detail < LoadoutDetail.Full) return;
            var u = a.Unlock;
            if (u != null)
                v.Rows.Add(new SummaryRow { Kind = "unlock", Sep = sep, Label = "UNLOCK  " + name(u.Row.Badge) + " (" + className(u.Row.Badge.Owner) + " tree) " + (u.Pinned ? "- " + Loadout.PinnedOn(a.Input != null ? a.Input.Shape : null) : "would be #" + u.WouldBe) });
            else if (a.Level != null)
            {
                // 0.14.0 (A1): "TRAINING YARD  Tough to level 3: best use of 3 Tank points" (it read "YARD Tough 2>3: +3.4 for 3 Tank points")
                string head = "TRAINING YARD  " + name(a.Level.Badge) + " to level " + a.Level.To, pts = a.Level.Cost + " " + className(a.Level.Badge.Owner) + " points";
                string label = head + ": best use of " + pts;
                v.Rows.Add(new SummaryRow { Kind = "yard", Sep = sep, Label = label.Length <= 64 ? label : head + " (" + pts + ")" });
            }
        }

        // ------------------------------------------------------------------------------------------------ the EQUIP plan (OD1)
        /// <summary>The clicks from <paramref name="equipped"/> to <paramref name="target"/>: removes first (in the equipped order),
        /// then adds (in the target order). Never removes a forced badge, never adds a locked one, never goes past the slots; every
        /// prefix of the plan is a valid selection. The game's own badge button toggles, so a step is one press.</summary>
        public static List<EquipStep> EquipPlan(IList<int> equipped, IList<int> target, ICollection<int> forced, Func<int, bool> unlocked, int slots)
        {
            var plan = new List<EquipStep>();
            var cur = (equipped ?? new int[0]).Distinct().ToList();
            forced = forced ?? new int[0];
            foreach (var id in cur.ToList())
                if (!target.Contains(id) && !forced.Contains(id)) { plan.Add(new EquipStep { Id = id, Add = false }); cur.Remove(id); }
            foreach (var id in target)
            {
                if (cur.Contains(id) || forced.Contains(id)) continue;
                if (unlocked != null && !unlocked(id)) continue;
                if (cur.Count >= slots) break;
                plan.Add(new EquipStep { Id = id, Add = true }); cur.Add(id);
            }
            return plan;
        }

        /// <summary>The inverse of a plan: the badges it added come out (last first), then the ones it removed go back in (in order).</summary>
        public static List<EquipStep> UndoOf(IList<EquipStep> plan)
        {
            var undo = new List<EquipStep>();
            foreach (var s in plan.Where(x => x.Add).Reverse()) undo.Add(new EquipStep { Id = s.Id, Add = false });
            foreach (var s in plan.Where(x => !x.Add)) undo.Add(new EquipStep { Id = s.Id, Add = true });
            return undo;
        }

        /// <summary>What the game's badge button would make of the selection, press by press (it refuses to remove a forced badge, to
        /// add past the slots, and LoadoutEquip never sends a locked one).</summary>
        public static List<int> Apply(IList<int> equipped, IEnumerable<EquipStep> steps, ICollection<int> forced, Func<int, bool> unlocked, int slots)
        {
            var cur = (equipped ?? new int[0]).ToList();
            forced = forced ?? new int[0];
            foreach (var s in steps)
            {
                if (s.Add) { if (!cur.Contains(s.Id) && cur.Count < slots && (unlocked == null || unlocked(s.Id))) cur.Add(s.Id); }
                else if (cur.Contains(s.Id) && !forced.Contains(s.Id)) cur.Remove(s.Id);
            }
            return cur;
        }

        /// <summary>The one-click button's run along this view's plan (see <see cref="EquipRun"/>).</summary>
        public static EquipRun RunOf(LoadoutView v, Func<int, bool> unlocked) { return new EquipRun(v.Equipped, v.Plan, v.Forced, unlocked, v.SlotCount); }

        // ------------------------------------------------------------------------------------------------ log text
        /// <summary>"equipped #7: Leveling 2.10, Speed 3.23, Growth 2.39, Thunder 0.00: 0 of 4 as advised | swap Thunder>Gunner +17.15, ...
        /// | keep - | 8 clicks to match"</summary>
        public string EquippedLine()
        {
            var a = Advice;
            Func<int, string> sh = id => { var r = a.RowOf(id); return r != null ? r.Badge.Short : id.ToString(IC); };
            Func<int, double> sc = id => { var r = a.RowOf(id); return r != null ? r.Score : 0; };
            Func<int, int, string> gain = (o, i) => "+" + F2((Loadout.Centi(a.PickOf(i).Score) - Loadout.Centi(sc(o))) / 100.0);
            string eq = Equipped.Count == 0 ? "none" : string.Join(", ", Equipped.Select(id => sh(id) + " " + F2(sc(id))));
            return "equipped #" + a.Number + ": " + eq + ": " + AsAdvised + " of " + AdvisedCount + " as advised"
                + " | swap " + (Swaps.Count == 0 ? "-" : string.Join(", ", Swaps.Select(s => sh(s.Key) + ">" + sh(s.Value) + " " + gain(s.Key, s.Value))))
                + " | keep " + (Kept.Count == 0 ? "-" : string.Join(", ", Kept.Select(s => sh(s.Key) + " (" + sh(s.Value) + " " + gain(s.Key, s.Value) + ")")))
                + (Free.Count > 0 ? " | free slots " + string.Join(", ", Free.Select(sh)) : "")
                + " | " + Plan.Count + " click" + (Plan.Count == 1 ? "" : "s") + " to match";
        }

        /// <summary>"marks 16,17,20,18 | frames 16,17,20,18 | swap-outs 15,6,9,1 | kept - | close -" (marks = the advised badges in
        /// priority order whose number is drawn; empty with Off).</summary>
        public string DrawnMarks()
        {
            Func<IEnumerable<int>, string> ids = l => { var x = l.ToList(); return x.Count == 0 ? "-" : string.Join(",", x); };
            var advised = Advice.Picks.Where(p => !p.Forced).Select(p => p.Badge.Id);
            return "marks " + ids(Detail == LoadoutDetail.Off ? Enumerable.Empty<int>() : advised)
                + " | frames " + ids(Advice.Picks.Where(p => !p.Forced && MarkOf(p.Badge.Id) != null && MarkOf(p.Badge.Id).Frame).Select(p => p.Badge.Id))
                + " | swap-outs " + ids(Grid.Where(m => m.Kind == MarkKind.SwapOut).Select(m => m.Id))
                + " | kept " + ids(Grid.Where(m => m.Kind == MarkKind.KeepClose).Select(m => m.Id))
                + " | close " + ids(Grid.Where(m => m.Kind == MarkKind.Close).Select(m => m.Id))
                + (Forced.Count > 0 ? " | quest " + ids(Forced) : "");
        }
    }
}
