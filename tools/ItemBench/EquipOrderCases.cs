// 0.16.1 EQUIP ADVICE in the advice's order, and by pad (the user's report of 10-11: "if I just single click that, it doesn't seem
// to select in the right order, especially if I already have 1 or 2 equipped. Also no easy way to nav to the auto-equip button on
// steam deck/controlled nav"; research\roadmap_1010\impl\equip_investigation.md). The game's list rules (UIViewRunSetup.OnClickBadge /
// RefreshSelectedBadgeButtons, machine code of 1.0.2): slot i shows the selection's entry i, an add APPENDS, a remove COMPACTS (the
// later slots shift left), a forced badge never moves - modelled here on its own (Click), not with LoadoutView.Apply.
//   EO1  the five logged rows (Deck 10-11 / 10-10 x2, PC 10-07 / 10-10): 0.16.0's plan ends 1 3 4 2 / 2 3 4 1 / 3 4 1 2 / 4 2 1 3 /
//        4 3 1 2; the ordered plan ends 1 2 3 4 in 6 / 8 / 8 / 8 / 8 presses (= the fewest, by a search), the same set;
//   EO2  0, 1, 2, 3 and 4 slots pre-filled (every row over 7 badges, 4 slots): the slots read 1..4, the set of 0.16.0's plan, the
//        fewest presses, removes first, every prefix a valid selection; from empty slots the plan is 0.16.0's own (4 adds);
//   EO3  an advised badge already in another slot (#4 alone, #2 then #1, #3 last): the exact presses;
//   EO4  a slot holding a badge the advice drops (swapped out, kept close by the margin - the close one carries the number it stands
//        in for -, a short advice's unmarked badge after the numbered ones: D3); a matching set in another order: no plan (D1);
//   EO5  a quest's forced badge: never pressed, stays where the game keeps it (every position, 311 rows), the rest in order;
//   EO6  an equipped badge that could not be put back (locked) is never taken out for the order: the plain plan instead;
//   EO7  the UNDO: the exact row back (every row of EO2; 0.16.0's put back the set in another order 780 times in 1,100); after a
//        click by hand stopped the run, from what the game shows;
//   EO8  the 4-press timing (Deck 10-11: 4 presses 0.12 s apart from empty slots): each press is applied and read back before the
//        next, so the gap only spaces the sounds; the longest plan (8 presses) takes 0.84 s;
//   EO9  the log's words: the slots' numbers ("#1 #2 #3 #4", Q, -) and the advice's order;
//   EO10 the pad (source read): Y = the game's TabToggle without a pause override, 'pad' / 'key' in the pressed line, the seven views
//        that read TabToggle themselves block it, A on the focused plate (a Button with Automatic navigation, never on a click
//        frame), '· Y' on the caption in pad mode, no press while the screen takes no input or the mod menu holds the menus;
//        review 2: the focus is tracked every frame (a run included) and a click's selection given up (SetSelectedGameObject(null)),
//        A / Submit read only on the pad's focus, the cfg texts say what presses the plate (not "empty = ... the controller's Y only");
//   EO11 review 2, the input as data (EquipInput, pure): a click or a tap selects the plate (Selectable.OnPointerDown) - the selection
//        is given up whether the input module runs before or after the tick, so the next A / Enter / Space presses nothing (the
//        lane's rule would have started the UNDO); the d-pad's (or the arrow keys') focus does count; TabToggle from the keyboard
//        (T on the PC's Rewired map) is refused, from the pad it is 'pad'; A never on a click frame; a fallback tick's focus does not count.
// Pure: hand-made advices (badge ids and scores of the logs; no game data), so it runs in --no-data (the public CI) as well.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class EquipOrderCases
    {
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static Knowledge _k;
        static Knowledge K { get { return _k ?? (_k = Knowledge.FromJson(Knowledge.DefaultJson)); } }
        static string J(IEnumerable<int> l) { var x = l.ToList(); return x.Count == 0 ? "empty" : string.Join(",", x); }
        static string P(IEnumerable<EquipStep> l) { return string.Join(" ", l); }

        // ================================================================ the game, on its own
        /// <summary>One press of the game's grid button (OnClickBadge): a forced badge does nothing, a selected one comes out (the later
        /// slots shift left), another goes in at the end while there is a free slot.</summary>
        static List<int> Click(IList<int> sel, int id, ICollection<int> forced, int slots)
        {
            var s = sel.ToList();
            if (forced.Contains(id)) return s;
            if (s.Contains(id)) s.Remove(id); else if (s.Count < slots) s.Add(id);
            return s;
        }

        /// <summary>The plan pressed on the game: the end row, and whether every press toggled the way its step says, no prefix went past
        /// the slots and no forced badge was pressed.</summary>
        static List<int> Press(IList<int> start, IEnumerable<EquipStep> plan, ICollection<int> forced, int slots, out bool clean)
        {
            clean = true; var s = start.ToList();
            foreach (var st in plan)
            {
                if (st.Add == s.Contains(st.Id) || forced.Contains(st.Id)) clean = false;
                s = Click(s, st.Id, forced, slots);
                if (s.Count > slots) clean = false;
            }
            return s;
        }

        static long Key(IList<int> l) { long k = l.Count; foreach (var id in l) k = k * 128 + id + 1; return k; }

        /// <summary>The fewest presses from one row to another under the game's rules (breadth-first over the badges given).</summary>
        static int Fewest(IList<int> start, IList<int> goal, IList<int> universe, ICollection<int> forced, int slots, int limit = 12)
        {
            long g = Key(goal);
            var seen = new Dictionary<long, int> { { Key(start), 0 } };
            var q = new Queue<List<int>>(); q.Enqueue(start.ToList());
            while (q.Count > 0)
            {
                var s = q.Dequeue(); long ks = Key(s); int d = seen[ks];
                if (ks == g) return d;
                if (d >= limit) continue;
                foreach (var b in universe) { var t = Click(s, b, forced, slots); long kt = Key(t); if (!seen.ContainsKey(kt)) { seen[kt] = d + 1; q.Enqueue(t); } }
            }
            return -1;
        }

        // ================================================================ hand-made advices
        /// <summary>An advice as Loadout.Recommend leaves it: the rows (every badge named, level 1 unless locked), the forced picks (rank 0)
        /// and the advised ones in rank order (1..n).</summary>
        static LoadoutAdvice Advice(int[] advised, Dictionary<int, double> scores, int[] forced = null, int[] locked = null)
        {
            forced = forced ?? new int[0]; locked = locked ?? new int[0];
            var a = new LoadoutAdvice { Number = 1 };
            var ids = scores.Keys.Concat(advised).Concat(forced).Distinct().OrderBy(x => x).ToList();
            foreach (var id in ids)
            {
                double sc; scores.TryGetValue(id, out sc);
                a.Rows.Add(new LoadoutRow { Badge = new BadgeFacts { Id = id, Sort = id, Short = "B" + id }, Level = locked.Contains(id) ? 0 : 1, Score = sc, Rated = true });
            }
            a.All.AddRange(a.Rows.OrderByDescending(r => r.Score).ThenBy(r => r.Badge.Id));
            foreach (var id in forced) a.Picks.Add(new LoadoutPick { Badge = a.RowOf(id).Badge, Level = 1, Rank = 0, Source = "forced", Score = a.RowOf(id).Score });
            int rank = 0;
            foreach (var id in advised) a.Picks.Add(new LoadoutPick { Badge = a.RowOf(id).Badge, Level = 1, Rank = ++rank, Source = "score", Score = a.RowOf(id).Score });
            return a;
        }
        static Func<int, bool> Unlocked(LoadoutAdvice a) { return id => { var r = a.RowOf(id); return r != null && r.Unlocked; }; }
        static LoadoutView View(LoadoutAdvice a, IList<int> equipped, int[] forced = null, int slots = 4)
        {
            return LoadoutView.Of(a, equipped, forced ?? new int[0], slots, LoadoutDetail.Numbers, K);
        }

        static readonly Dictionary<int, double> S = new Dictionary<int, double> { { 10, 9.0 }, { 11, 8.0 }, { 12, 7.0 }, { 13, 6.0 }, { 14, 1.0 }, { 15, 0.5 } };

        // ================================================================ the cases
        public static int Run()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.16.1 EQUIP ADVICE in the advice's order, and by pad (the 10-11 Deck report) - EquipOrderCases.cs");
            Logged();
            Prefilled();
            Moved();
            Dropped();
            Quest();
            Locked();
            Undo();
            Timing();
            Words();
            Pad();
            Input();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        /// <summary>One view's EQUIP against the game: the end row, its numbers, 0.16.0's row (the plain plan) and the fewest presses.</summary>
        sealed class Outcome { public List<int> Row, Old; public string Ranks, OldRanks; public int Presses, Fewest; public bool Clean, SameSet, InOrder; public LoadoutView V; }
        static Outcome Equip(LoadoutAdvice a, int[] equipped, int[] forced = null, int slots = 4, IList<int> universe = null)
        {
            forced = forced ?? new int[0];
            var v = View(a, equipped, forced, slots);
            bool clean; var row = Press(equipped, v.Plan, forced, slots, out clean);
            bool c2; var old = Press(equipped, LoadoutView.EquipPlan(v.Equipped, v.Target, v.Forced, Unlocked(a), slots), forced, slots, out c2);
            var u = universe ?? a.Rows.Select(r => r.Badge.Id).ToList();
            return new Outcome
            {
                V = v, Row = row, Old = old, Ranks = v.SlotsText(row), OldRanks = v.SlotsText(old), Presses = v.Plan.Count, Clean = clean,
                SameSet = new HashSet<int>(row).SetEquals(old), InOrder = v.InOrder(row), Fewest = Fewest(equipped, row, u, forced, slots),
            };
        }

        static void Logged()
        {
            var rows = new[]
            {
                Tuple.Create("Deck 10-11 00:59 Medic (before the slots were emptied by hand)", new[] { 1, 0, 18, 20 }, new[] { 0, 16, 18, 20 }, new Dictionary<int, double> { { 1, 0.0 }, { 0, 13.31 }, { 16, 10.54 }, { 18, 8.85 }, { 20, 6.33 } }, "#1 #3 #4 #2", 6),
                Tuple.Create("Deck 10-10 20:23 Engineer", new[] { 0, 16, 18, 20 }, new[] { 1, 0, 18, 20 }, new Dictionary<int, double> { { 0, 11.48 }, { 16, 0.0 }, { 18, 8.85 }, { 20, 6.33 }, { 1, 18.50 } }, "#2 #3 #4 #1", 8),
                Tuple.Create("Deck 10-10 18:40 Medic", new[] { 13, 18, 6, 20 }, new[] { 0, 16, 18, 20 }, new Dictionary<int, double> { { 13, 0.0 }, { 18, 8.85 }, { 6, 4.40 }, { 20, 6.33 }, { 0, 10.64 }, { 16, 10.54 } }, "#3 #4 #1 #2", 8),
                Tuple.Create("PC 10-07 20:33 Tank (the one press from full slots in the logs: it ended 20,18,19,6)", new[] { 20, 18, 16, 0 }, new[] { 19, 18, 6, 20 }, new Dictionary<int, double> { { 20, 5.99 }, { 18, 6.82 }, { 16, 3.43 }, { 0, 3.36 }, { 19, 12.93 }, { 6, 6.24 } }, "#4 #2 #1 #3", 8),
                Tuple.Create("PC 10-10 11:48 Pyro", new[] { 16, 17, 20, 6 }, new[] { 13, 18, 6, 20 }, new Dictionary<int, double> { { 16, 2.84 }, { 17, 3.94 }, { 20, 6.33 }, { 6, 6.47 }, { 13, 7.83 }, { 18, 7.08 } }, "#4 #3 #1 #2", 8),
            };
            foreach (var r in rows)
            {
                var a = Advice(r.Item3, r.Item4); var o = Equip(a, r.Item2);
                Check("EO1", r.Item1 + ": 0.16.0 ended '" + r.Item5 + "'; now '#1 #2 #3 #4' in " + r.Item6 + " presses (the fewest), the same badges",
                    o.OldRanks == r.Item5 && o.Ranks == "#1 #2 #3 #4" && o.Row.SequenceEqual(r.Item3) && o.Presses == r.Item6 && o.Fewest == r.Item6 && o.Clean && o.SameSet,
                    J(r.Item2) + " -> " + P(o.V.Plan) + " -> " + J(o.Row) + " (was " + J(o.Old) + " = " + o.OldRanks + ")");
            }
            var pc = Equip(Advice(rows[3].Item3, rows[3].Item4), rows[3].Item2);
            Check("EO1", "the PC 10-07 press, replayed with 0.16.0's plan, ends 20,18,19,6 - what its log line says", pc.Old.SequenceEqual(new[] { 20, 18, 19, 6 }), J(pc.Old));
        }

        static readonly int[] U = { 10, 11, 12, 13, 14, 15, 16 };
        static IEnumerable<int[]> Rows(int[] universe, int n)
        {
            if (n == 0) { yield return new int[0]; yield break; }
            foreach (var first in universe)
                foreach (var rest in Rows(universe.Where(x => x != first).ToArray(), n - 1))
                    yield return new[] { first }.Concat(rest).ToArray();
        }

        static void Prefilled()
        {
            var sc = U.ToDictionary(i => i, i => 10.0 - (i - 10) * 1.3);
            var a = Advice(new[] { 10, 11, 12, 13 }, sc);
            int total = 0, worst = 0, matched = 0;
            for (int n = 0; n <= 4; n++)
            {
                int count = 0, same = 0; var bad = new List<string>(); int most = 0;
                foreach (var eq in Rows(U, n))
                {
                    count++;
                    var o = Equip(a, eq, null, 4, U);
                    // the advised set already equipped (in any order): no plan, no button (D1) - the order changes only with an EQUIP that
                    // changes the set
                    if (LoadoutView.EquipPlan(o.V.Equipped, o.V.Target, o.V.Forced, Unlocked(a), 4).Count == 0)
                    {
                        same++;
                        if (o.V.Plan.Count != 0 && bad.Count < 4) bad.Add(J(eq) + " matches the advice's set, yet a plan " + P(o.V.Plan));
                        continue;
                    }
                    bool removesFirst = o.V.Plan.SkipWhile(s => !s.Add).All(s => s.Add);
                    bool prefixes = true;
                    for (int i = 0; i <= o.V.Plan.Count; i++) { bool c; var cur = Press(eq, o.V.Plan.Take(i), new int[0], 4, out c); if (cur.Count > 4 || !c) prefixes = false; }
                    var order = LoadoutView.WantedOrder(o.V);
                    if (!(o.Row.SequenceEqual(order) && o.InOrder && o.SameSet && o.Presses == o.Fewest && o.Clean && removesFirst && prefixes) && bad.Count < 4)
                        bad.Add(J(eq) + " -> " + P(o.V.Plan) + " -> " + J(o.Row) + " (" + o.Ranks + ", fewest " + o.Fewest + ")");
                    most = Math.Max(most, o.Presses); worst = Math.Max(worst, o.Presses);
                }
                total += count; matched += same;
                Check("EO2", n + " slot" + (n == 1 ? "" : "s") + " pre-filled (" + count + " rows over 7 badges, advice 10,11,12,13): the slots read #1 #2 #3 #4, the same badges as 0.16.0's plan, the fewest presses (at most " + most + "), removes first, every prefix valid"
                    + (same > 0 ? "; the " + same + " rows that hold the four advised badges already (any order): no plan (D1)" : ""),
                    bad.Count == 0, string.Join("; ", bad));
            }
            var empty = Equip(a, new int[0]);
            var oldPlan = LoadoutView.EquipPlan(new int[0], empty.V.Target, new int[0], Unlocked(a), 4);
            Check("EO2", "from empty slots (the user's workaround on the Deck, 3 of 3 sessions): the plan of 0.16.0 itself - 4 adds in rank order",
                P(empty.V.Plan) == P(oldPlan) && P(empty.V.Plan) == "+10 +11 +12 +13" && empty.Row.SequenceEqual(new[] { 10, 11, 12, 13 }), P(empty.V.Plan));
            Check("EO2", "the sweep: " + total + " rows (" + matched + " of them the advised set in some order: 4! = 24), the longest plan " + worst + " presses (0.16.0: at most 8 too, B1)", total == 1100 && matched == 24 && worst <= 8, total + " rows, " + matched + " matched, " + worst);
        }

        static void Moved()
        {
            var a = Advice(new[] { 10, 11, 12, 13 }, S);
            var cases = new[]
            {
                Tuple.Create("#4 alone in slot 1", new[] { 13 }, "-13 +10 +11 +12 +13", "#4 #1 #2 #3"),
                Tuple.Create("#2 then #1", new[] { 11, 10 }, "-11 +11 +12 +13", "#2 #1 #3 #4"),
                Tuple.Create("#1 #2 in place, #3 missing, #4 in slot 3", new[] { 10, 11, 13 }, "-13 +12 +13", "#1 #2 #4 #3"),
                Tuple.Create("#3 in slot 1, #1 #2 after it", new[] { 12, 10, 11 }, "-12 +12 +13", "#3 #1 #2 #4"),
                Tuple.Create("#1 in place, the others missing", new[] { 10 }, "+11 +12 +13", "#1 #2 #3 #4"),
            };
            foreach (var c in cases)
            {
                var o = Equip(a, c.Item2);
                Check("EO3", c.Item1 + " (" + J(c.Item2) + "): '" + c.Item3 + "' -> #1 #2 #3 #4 (0.16.0: '" + c.Item4 + "')",
                    P(o.V.Plan) == c.Item3 && o.Ranks == "#1 #2 #3 #4" && o.OldRanks == c.Item4 && o.Presses == o.Fewest && o.Clean, P(o.V.Plan) + " -> " + o.Ranks + " | 0.16.0 " + o.OldRanks);
            }
        }

        static void Dropped()
        {
            var a = Advice(new[] { 10, 11, 12, 13 }, S);
            var x = Equip(a, new[] { 12, 14 });
            Check("EO4", "#3 + a badge the advice drops (14): 14 out, '#1 #2 #3 #4' in 6 presses (0.16.0: '#3 #1 #2 #4')",
                P(x.V.Plan) == "-12 -14 +10 +11 +12 +13" && x.Ranks == "#1 #2 #3 #4" && x.OldRanks == "#3 #1 #2 #4" && x.Fewest == 6, P(x.V.Plan) + " -> " + x.Ranks);
            var full = Equip(a, new[] { 14, 15, 11, 10 });
            Check("EO4", "two dropped badges in front of #2 #1: '#1 #2 #3 #4', the fewest presses", full.Ranks == "#1 #2 #3 #4" && full.Presses == full.Fewest && full.Clean, P(full.V.Plan) + " -> " + full.Ranks + " (fewest " + full.Fewest + ")");
            // kept close: 14 (5.8) stands in for #4 (13, 6.0) - a gain of 0.2 under max(1.0, 15 %); 15 is swapped for #3
            var close = new Dictionary<int, double> { { 10, 9.0 }, { 11, 8.0 }, { 12, 7.0 }, { 13, 6.0 }, { 14, 5.8 }, { 15, 0.5 } };
            var ac = Advice(new[] { 10, 11, 12, 13 }, close);
            var kc = Equip(ac, new[] { 14, 15, 10, 11 });
            Check("EO4", "a close call kept (14 stands in for #4) while 15 makes room for #3: '#1 #2 #3 #4' with 14 as #4 (0.16.0: '#4 #1 #2 #3')",
                kc.V.Kept.Count == 1 && kc.V.Kept[0].Key == 14 && kc.V.RankOf(14) == 4 && kc.Ranks == "#1 #2 #3 #4" && kc.Row.SequenceEqual(new[] { 10, 11, 12, 14 }) && kc.OldRanks == "#4 #1 #2 #3" && kc.Presses == kc.Fewest,
                P(kc.V.Plan) + " -> " + J(kc.Row) + " = " + kc.Ranks + " | 0.16.0 " + kc.OldRanks);
            var keep = View(ac, new[] { 12, 14, 10, 11 });
            Check("EO4", "the set already as advised but out of order (a close call kept, '#3 #4 #1 #2'): no plan, no button (D1)", keep.Plan.Count == 0 && keep.Kept.Count == 1, P(keep.Plan));
            var matched = View(a, new[] { 13, 12, 11, 10 });
            var three = View(Advice(new[] { 10, 11, 12 }, S), new[] { 12, 11, 10 }, null, 3);
            Check("EO4", "all advised badges equipped in reverse (4 slots, 3 slots): MATCH, no plan (D1)", matched.Matches && matched.Plan.Count == 0 && three.Matches && three.Plan.Count == 0,
                matched.Plan.Count + " / " + three.Plan.Count + " presses");
            // a short advice: 2 advised, 4 slots; 15 (0.5) makes room for #1, 14 stays unmarked and goes after the numbered ones (D3)
            var shortA = Advice(new[] { 10, 11 }, S);
            var sh = Equip(shortA, new[] { 14, 11, 15 });
            Check("EO4", "a short advice (2 advised, 4 slots): the unmarked badge kept after #1 #2 (D3): '#1 #2 -'",
                sh.Ranks == "#1 #2 -" && sh.Row.SequenceEqual(new[] { 10, 11, 14 }) && sh.InOrder && sh.Presses == sh.Fewest && sh.SameSet, P(sh.V.Plan) + " -> " + sh.Ranks);
        }

        static void Quest()
        {
            var a = Advice(new[] { 10, 11, 12 }, S, new[] { 50 });
            var f = new[] { 50 };
            var first = Equip(a, new[] { 50, 12 }, f);
            var after = Equip(a, new[] { 12, 50 }, f);
            Check("EO5", "the quest's badge in slot 1 + #3: 'Q #1 #2 #3' (0.16.0 'Q #3 #1 #2'); after a saved badge (12,50): 'Q #1 #2 #3' - Q moves up as #3 leaves, it is never pressed",
                first.Ranks == "Q #1 #2 #3" && first.OldRanks == "Q #3 #1 #2" && after.Ranks == "Q #1 #2 #3" && !first.V.Plan.Any(s => s.Id == 50) && !after.V.Plan.Any(s => s.Id == 50) && first.Clean && after.Clean,
                P(first.V.Plan) + " -> " + first.Ranks + " | " + P(after.V.Plan) + " -> " + after.Ranks);
            var u5 = new[] { 10, 11, 12, 13, 14 };
            var sc = u5.ToDictionary(i => i, i => 10.0 - (i - 10) * 1.3);
            var aq = Advice(new[] { 10, 11, 12 }, sc, new[] { 50 });
            var universe = u5.Concat(new[] { 50 }).ToList();
            int count = 0, same = 0; var bad = new List<string>();
            for (int n = 0; n <= 3; n++)
                foreach (var eq in Rows(u5, n))
                    for (int pos = 0; pos <= n; pos++)
                    {
                        var row = eq.Take(pos).Concat(new[] { 50 }).Concat(eq.Skip(pos)).ToArray();
                        count++;
                        var o = Equip(aq, row, f, 4, universe);
                        if (LoadoutView.EquipPlan(o.V.Equipped, o.V.Target, o.V.Forced, Unlocked(aq), 4).Count == 0) { same++; if (o.V.Plan.Count != 0 && bad.Count < 4) bad.Add(J(row) + " matches, yet " + P(o.V.Plan)); continue; }
                        bool ok = o.Row.Contains(50) && o.Row.Where(id => id != 50).SequenceEqual(LoadoutView.WantedOrder(o.V)) && o.InOrder && o.SameSet && o.Presses == o.Fewest && o.Clean && !o.V.Plan.Any(s => s.Id == 50);
                        if (!ok && bad.Count < 4) bad.Add(J(row) + " -> " + P(o.V.Plan) + " -> " + J(o.Row));
                    }
            Check("EO5", "the quest's badge in every slot (" + count + " rows; " + same + " hold the advised set already: no plan, D1): never pressed, the others read #1 #2 #3, the fewest presses",
                count == 311 && same == 24 && bad.Count == 0, string.Join("; ", bad));
        }

        static void Locked()
        {
            // 30 is equipped but reads locked (level 0: no row would let it back in) and stays unmarked; the order would need it out
            var sc = new Dictionary<int, double>(S) { { 30, 6.5 } };
            var a = Advice(new[] { 10, 11 }, sc, null, new[] { 30 });
            var v = View(a, new[] { 14, 30, 11 });
            var plain = LoadoutView.EquipPlan(v.Equipped, v.Target, v.Forced, Unlocked(a), 4);
            var ordered = LoadoutView.OrderedPlan(v.Equipped, LoadoutView.WantedOrder(v), v.Forced, Unlocked(a), 4);
            bool clean; var row = Press(new[] { 14, 30, 11 }, v.Plan, new int[0], 4, out clean);
            Check("EO6", "a badge that could not be put back (30, locked) is never taken out for the order: the plain plan, 30 stays",
                P(v.Plan) == P(plain) && P(plain) == "-14 +10" && ordered.Any(s => s.Id == 30 && !s.Add) && row.Contains(30) && clean, P(v.Plan) + " (the ordered one would be " + P(ordered) + ")");
        }

        static void Undo()
        {
            var sc = U.ToDictionary(i => i, i => 10.0 - (i - 10) * 1.3);
            var a = Advice(new[] { 10, 11, 12, 13 }, sc);
            int count = 0, lostBefore = 0; var bad = new List<string>();
            for (int n = 0; n <= 4; n++)
                foreach (var eq in Rows(U, n))
                {
                    count++;
                    var v = View(a, eq);
                    bool c1, c2, c3;
                    var after = Press(eq, v.Plan, new int[0], 4, out c1);
                    var back = Press(after, v.Undo, new int[0], 4, out c2);
                    var old = LoadoutView.EquipPlan(v.Equipped, v.Target, v.Forced, Unlocked(a), 4);
                    var oldBack = Press(Press(eq, old, new int[0], 4, out c3), LoadoutView.UndoOf(old), new int[0], 4, out c3);
                    if (!oldBack.SequenceEqual(eq)) lostBefore++;
                    if ((!back.SequenceEqual(eq) || !c2) && bad.Count < 4) bad.Add(J(eq) + " -> " + J(after) + " -> undo " + P(v.Undo) + " -> " + J(back));
                }
            Check("EO7", "EQUIP then UNDO gives back the exact row (" + count + " rows; 0.16.0's UNDO put back the set in another order " + lostBefore + " times)", bad.Count == 0 && lostBefore == 780, string.Join("; ", bad));
            // the run's own UNDO, after a click by hand stopped it: from what the game shows
            var deck = Advice(new[] { 0, 16, 18, 20 }, new Dictionary<int, double> { { 1, 0.0 }, { 0, 13.31 }, { 16, 10.54 }, { 18, 8.85 }, { 20, 6.33 }, { 12, 1.0 } });
            var dv = View(deck, new[] { 1, 0, 18, 20 });
            var run = LoadoutView.RunOf(dv, Unlocked(deck)); var live = new List<int> { 1, 0, 18, 20 };
            for (int i = 0; i < 2; i++) { var st = run.Next(live); live = Click(live, st.Id, new int[0], 4); run.Pressed(live); }
            live = Click(live, 12, new int[0], 4);                                  // the player clicks Gamble in between
            var stop = run.Next(live);
            var undo = run.Undo(live);
            var l2 = live.ToList();
            for (var st = undo.Next(l2); st != null; st = undo.Next(l2)) { l2 = Click(l2, st.Id, new int[0], 4); undo.Pressed(l2); }
            Check("EO7", "a click by hand after press 2 stops the run; its UNDO starts from what the game shows (the hand's badge included) and puts back 1,0,18,20",
                stop == null && run.Stopped.Contains("by hand") && undo.Finished && l2.SequenceEqual(new[] { 1, 0, 18, 20 }), J(live) + " -> undo " + P(undo.Steps) + " -> " + J(l2));
            var exp = run.Undo();
            Check("EO7", "without the game's row (unreadable) the UNDO starts from what the presses made", exp.Start.SequenceEqual(new[] { 0, 20 }) && Press(exp.Start, exp.Steps, new int[0], 4, out bool c4).SequenceEqual(new[] { 1, 0, 18, 20 }), J(exp.Start) + " " + P(exp.Steps));
        }

        static void Timing()
        {
            // Deck 10-11 01:00:00.644: the slots emptied by hand, then EQUIP pressed by mouse (a touch) - 4 presses 0.12 s apart
            var deck = Advice(new[] { 0, 16, 18, 20 }, new Dictionary<int, double> { { 1, 0.0 }, { 0, 13.31 }, { 16, 10.54 }, { 18, 8.85 }, { 20, 6.33 } });
            var v = View(deck, new int[0]);
            var run = LoadoutView.RunOf(v, Unlocked(deck)); var live = new List<int>(); var seen = new List<string>();
            for (var st = run.Next(live); st != null; st = run.Next(live)) { live = Click(live, st.Id, new int[0], 4); run.Pressed(live); seen.Add(J(live)); }
            Check("EO8", "the Deck 10-11 press from empty slots: 4 presses, each read back before the next (0 / 0,16 / 0,16,18 / 0,16,18,20), the slots #1 #2 #3 #4 - as logged",
                run.Finished && run.Done == 4 && string.Join(" / ", seen) == "0 / 0,16 / 0,16,18 / 0,16,18,20" && v.SlotsText(live) == "#1 #2 #3 #4" && v.InOrder(live), string.Join(" / ", seen));
            // the presses are synchronous in the game (OnClickBadge saves and refreshes the slots before it returns): the run only reads the
            // selection between presses, so any gap gives the same row - the gap is LoadoutEquip's 0.12 s (sounds apart)
            string src = Src("LoadoutEquip.cs");
            float gap = -1f;
            if (src != null) { var m = System.Text.RegularExpressions.Regex.Match(src, @"const float Gap = ([0-9.]+)f;"); if (m.Success) gap = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); }
            int longest = 8;
            Check("EO8", "the gap between presses is 0.12 s (LoadoutEquip.cs): the longest ordered plan (8 presses) takes " + (gap > 0 ? ((longest - 1) * gap).ToString("0.00", CultureInfo.InvariantCulture) : "?") + " s from the first press to the last",
                src == null || Math.Abs(gap - 0.12f) < 1e-6, src == null ? "source not found - skipped" : null);
        }

        static void Words()
        {
            var a = Advice(new[] { 10, 11, 12 }, S, new[] { 50 });
            var v = View(a, new[] { 50, 12 }, new[] { 50 });
            Check("EO9", "the slots' numbers: 'Q #3', 'Q #1 #2 #3', '#3 #1 #2', '#1 #2 -', 'empty'",
                v.SlotsText(new[] { 50, 12 }) == "Q #3" && v.SlotsText(new[] { 50, 10, 11, 12 }) == "Q #1 #2 #3" && v.SlotsText(new[] { 12, 10, 11 }) == "#3 #1 #2" && v.SlotsText(new[] { 10, 11, 14 }) == "#1 #2 -" && v.SlotsText(new int[0]) == "empty");
            Check("EO9", "the advice's order: Q anywhere, numbers rising, unmarked after them ('#1 Q #2 -' yes; '#2 #1', '- #1' no)",
                v.InOrder(new[] { 10, 50, 11, 14 }) && v.InOrder(new int[0]) && !v.InOrder(new[] { 11, 10 }) && !v.InOrder(new[] { 14, 10 }));
        }

        static string Src(string file)
        {
            try
            {
                string mod = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod", file));
                return File.Exists(mod) ? File.ReadAllText(mod) : null;
            }
            catch { return null; }
        }

        static void Pad()
        {
            string s = Src("LoadoutEquip.cs");
            if (s == null) { Console.WriteLine("  (EO10 skipped: LoadoutEquip.cs not found next to the bench)"); return; }
            Check("EO10", "Y = the game's TabToggle, read as the screen reads its Cancel (no pause override); the source from EquipInput.Decide, told by a keyboard key down",
                s.Contains("const string PadAction = \"TabToggle\"") && s.Contains("GameMaster.GetButtonDown(action, false)") && s.Contains("tab = Action(PadAction)") && s.Contains("EquipInput.Decide(") && s.Contains("(tab || submit) && Menu.KeyboardHeld()"));
            string[] views = { "_uiViewMainMenu", "_uiViewChooseHero", "_uiViewChooseArena", "_uiViewItemsPoolMainMenu", "_uiViewRunHistory", "_uiViewSaveProfile", "_uiViewGameHub" };
            Check("EO10", "Y is not taken while one of the seven views that read TabToggle themselves is live (the main menu only while interactive)",
                views.All(x => s.Contains("m => m." + x) || s.Contains("var v = m." + x)) && s.Contains("isActiveAndEnabled") && s.Contains("v._isInteractive") && s.Contains("how.Contains(\"TabToggle\")"),
                string.Join(", ", views.Where(x => !s.Contains(x))));
            Check("EO10", "the plate is a Button in the screen's Automatic navigation, A on it presses while the pad's focus is there (never on a mouse-click frame: UISubmit fires on the left button too)",
                s.Contains("AddComponent(Il2CppType.Of<Button>())") && s.Contains("Navigation.Mode.Automatic") && s.Contains("_padFocus && !click) submit = Action(\"UISubmit\")"));
            // review 2 (click-focus-then-A-undoes): a click or a tap selects the plate (Selectable.OnPointerDown) - in 0.16.0 the input module
            // cleared that selection on the same tap; now Track gives it up, every frame the plate is shown, a run included (the click that
            // started the run selected it), and only the pad's focus lights the ring and lets A / Submit press
            int frameAt = s.IndexOf("public static void Frame(", StringComparison.Ordinal);
            int trackAt = frameAt < 0 ? -1 : s.IndexOf("if (Shown) Track();", frameAt, StringComparison.Ordinal);
            int runAt = frameAt < 0 ? -1 : s.IndexOf("if (_running)", frameAt, StringComparison.Ordinal);
            Check("EO10", "review 2: the focus is tracked before a run's early return, a click's selection of the plate is given up (SetSelectedGameObject(null)), the ring and A follow the pad's focus only",
                trackAt > 0 && runAt > trackAt && s.Contains("EquipInput.Focus(selected, _selected, _padFocus, frame - _mouseFrame, seenLast, out pad, out release)") && s.Contains("es.SetSelectedGameObject(null)")
                && s.Contains("Focus(pad);") && !s.Contains("bool focused = Focused();"), "Track at " + trackAt + ", the run's return at " + runAt);
            Check("EO10", "'· Y' on the caption in pad mode (the key otherwise), placed again when the mode changes",
                s.Contains("if (_pad) return \"Y\"") && s.Contains("GameMaster.Get.UsesPad") && s.Contains("LoadoutUi.Dirty()"));
            Check("EO10", "no press while the screen takes no input or the mod menu holds the game's menus; the pressed and done lines carry the slots",
                s.Contains("Menu.Blocking") && s.Contains("LoadoutState.CanInteract(view)") && s.Contains("\" -> \" + Ids(planned)") && s.Contains("the advice's order"));
            string menu = Src("Menu.cs");
            Check("EO10", "Menu.KeyboardHeld is internal (the source of a TabToggle / Submit press)", menu == null || menu.Contains("internal static bool KeyboardHeld()"));
            // review 2 (tabtoggle-keyboard-T-unlabelled): the cfg texts say what presses the plate - not "empty = mouse / touch / the
            // controller's Y only" (T, the game's TabToggle key, pressed it unlabelled; it is refused now), and the BADGES help names A
            string plugin = Src("Plugin.cs"), badges = Src("Menu.Badges.cs");
            Check("EO10", "review 2: [Advice] LoadoutEquipKey's text says what presses the plate with no key set (a click or a touch, Y, A / Enter / Space on the pad's or arrow keys' focus; the game's TabToggle key does not); the BADGES help names A",
                plugin == null || badges == null || (!plugin.Contains("empty = mouse / touch / the controller's Y only") && plugin.Contains("empty = no key, the default: a click or a touch, a controller's Y, or A - Enter / Space - once the d-pad or the arrow keys have brought the focus to the button")
                    && plugin.Contains("does not press it") && badges.Contains("or Y on a controller (or A once the d-pad has reached the button)")));
        }

        // ================================================================ review 2: the input as data (EquipInput)
        /// <summary>LoadoutEquip.Frame's input half, frame by frame: Track (EquipInput.Focus; a release clears the event system's
        /// selection), then the reads Frame makes (UISubmit only on the pad's focus and not on a click frame) and EquipInput.Decide. The
        /// event system is the test's: <see cref="Selected"/> is what it selects, set before the tick (it ran first) or after (it ran last).</summary>
        sealed class PlateSim
        {
            public bool Selected; public int Releases;
            bool _sel, _pad; int _mouse = -1000, _tick = -1000;
            public bool PadFocus { get { return _pad; } }
            public EquipPress Tick(int frame, bool mouse = false, bool click = false, bool onPlate = false, bool cfgKey = false, bool tab = false, bool submit = false, bool keyboard = false)
            {
                bool seenLast = _tick == frame - 1; _tick = frame;
                if (mouse || click) _mouse = frame;
                bool pad, release;
                EquipInput.Focus(Selected, _sel, _pad, frame - _mouse, seenLast, out pad, out release);
                if (release) { Selected = false; Releases++; }
                _sel = Selected; _pad = pad;
                bool sub = submit && _pad && !click;
                if (!onPlate && !cfgKey && !tab && !sub) return new EquipPress();
                return EquipInput.Decide(onPlate, onPlate ? "touch" : "", click, cfgKey, tab, sub, (tab || sub) && keyboard, _pad);
            }
        }

        static void Input()
        {
            // the user's EQUIP by tap (Deck 10-10 18:41 / 20:23, 10-11 01:00: 'pressed by mouse'); the input module selects the plate on the press
            foreach (var moduleFirst in new[] { false, true })
            {
                var t = new PlateSim();
                for (int f = 1; f < 10; f++) t.Tick(f);
                if (moduleFirst) t.Selected = true;
                var tap = t.Tick(10, mouse: true, click: true, onPlate: true);
                if (!moduleFirst) t.Selected = true;
                t.Tick(11, mouse: true);                             // the button comes up
                bool ring = false; for (int f = 12; f < 40; f++) { t.Tick(f); ring |= t.PadFocus; }
                var a = t.Tick(40, submit: true); var enter = t.Tick(41, submit: true, keyboard: true);
                Check("EO11", "a tap on EQUIP (the input module " + (moduleFirst ? "before" : "after") + " the screen's tick): pressed by touch, the plate's selection given up, no ring, the next A and the next Enter press nothing (the lane's rule: UNDO)",
                    tap.Go && tap.Source == "touch" && t.Releases == 1 && !t.Selected && !ring && !a.Go && !enter.Go && a.Refused.Length == 0,
                    "tap " + tap + ", releases " + t.Releases + ", A " + a + ", Enter " + enter);
            }
            // a tap whose press and release fall in one frame, the module after the tick: the next frame's tick has no mouse event, yet gives it up
            var q = new PlateSim(); for (int f = 1; f < 10; f++) q.Tick(f);
            q.Tick(10, mouse: true, click: true, onPlate: true); q.Selected = true; q.Tick(11); q.Tick(12);
            Check("EO11", "a tap within one frame (the module after the tick): given up on the next frame (" + EquipInput.ClickFrames + " frames of grace)", q.Releases == 1 && !q.Selected && !q.PadFocus);
            // the d-pad (or the arrow keys) bring the focus: it counts; A presses as 'pad', Enter as 'key'
            var d = new PlateSim(); for (int f = 1; f < 10; f++) d.Tick(f);
            d.Selected = true; d.Tick(10);
            var pa = d.Tick(11, submit: true); var pe = d.Tick(12, submit: true, keyboard: true);
            Check("EO11", "the d-pad's focus counts (the ring): A -> 'pad' (A on the focused button), Enter after the arrow keys -> 'key' (Submit on the focused button)",
                d.PadFocus && d.Releases == 0 && pa.Source == "pad" && pa.How == "A on the focused button" && pe.Source == "key" && pe.How == "Submit on the focused button", pa + " | " + pe);
            // the pad's focus, then a tap on the plate: the tap presses, and its selection is given up like any click's
            var tap2 = d.Tick(13, mouse: true, click: true, onPlate: true); d.Tick(14, mouse: true);
            var after = d.Tick(30, submit: true);
            Check("EO11", "the pad's focus, then a tap on the plate: pressed by touch, then no focus - the next A presses nothing", tap2.Source == "touch" && !d.Selected && !d.PadFocus && !after.Go, tap2 + " | " + after);
            // after a click, the d-pad brings the focus back: it counts again
            d.Selected = true; d.Tick(31); var back = d.Tick(32, submit: true);
            Check("EO11", "after the click the d-pad brings the focus back: it counts again (A -> 'pad')", d.PadFocus && back.Source == "pad", back.ToString());
            // A on a mouse-click frame, with the pad's focus: never (the game's UISubmit fires on the left button too)
            var cf = EquipInput.Decide(false, "", true, false, false, true, false, true);
            Check("EO11", "A / Submit on a frame with a mouse click: nothing, with the pad's focus too", !cf.Go && cf.Refused.Length == 0, cf.ToString());
            // TabToggle: from the pad 'pad' whatever the focus; from the keyboard (T on the PC's Rewired map: action 34 -> key 116) refused
            var y = EquipInput.Decide(false, "", false, false, true, false, false, false);
            var tk = EquipInput.Decide(false, "", false, false, true, false, true, false);
            var tf = EquipInput.Decide(false, "", false, false, true, false, true, true);
            Check("EO11", "TabToggle from the pad: 'pad' (the pad's Y); from the keyboard (T): refused, said as 'key' - with or without the focus (C5: the keyboard's key is LoadoutEquipKey alone)",
                y.Source == "pad" && y.How == "the pad's Y (TabToggle)" && !tk.Go && tk.RefusedBy == "key" && tk.Refused.Contains("LoadoutEquipKey") && !tf.Go, y + " | " + tk + " | " + tf);
            // the cfg key and a click keep 0.16.0's sources; the click wins over everything on its frame
            var k = EquipInput.Decide(false, "", false, true, true, false, true, false);
            var m = EquipInput.Decide(true, "", true, true, true, true, false, true);
            Check("EO11", "the cfg key -> 'key' (before TabToggle); a click on the plate -> its pointer ('mouse' when none is named), before every other input",
                k.Source == "key" && k.How == "" && m.Source == "mouse" && m.How == "", k + " | " + m);
            // the fallback tick (every 0.25 s, no click seen): a new focus it finds is not counted
            var fb = new PlateSim(); fb.Tick(100); fb.Selected = true; fb.Tick(115); var fa = fb.Tick(130, submit: true);
            Check("EO11", "a focus the fallback tick finds (no tick on the frame before: a click could have made it) does not count", !fb.PadFocus && !fa.Go && fb.Selected, fa.ToString());
            // a click's selection that could not be cleared: still not the pad's after the grace frames
            bool p1, r1, p2, r2;
            EquipInput.Focus(true, true, false, 50, true, out p1, out r1);
            EquipInput.Focus(false, true, true, 50, true, out p2, out r2);
            Check("EO11", "a click's selection kept (it could not be cleared) stays the click's; a focus that leaves counts for nothing", !p1 && !r1 && !p2 && !r2);
        }
    }
}
