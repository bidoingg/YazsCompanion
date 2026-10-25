// 0.17.0: the active quest's counted rules and the survive quest on hard modes - part of QuestCases (section 12 of the bench), pure: it runs in
// the full bench (from QuestCases.Run) and in --no-data (Verdict.DataFree), so the public CI runs it.
//   Q6b (C16-04b) 'Rerolled Item Chests' (Ghost_1 "Encapsulation"): the chest's REROLL while the best card is under 3.5 and the game has not
//       counted the view yet, the rerolls kept on every other screen (the rescue screen's RerollCall too), the gates, the log's note, the short
//       words, and a MADE-UP chest corpus replayed through ScreenCall.Decide (the user's real corpus stays outside the repository: 'ItemBench
//       --replay-quest-chests <corpus>' replays it - 15 of 33 runs reach 3 rerolled chests, 64 hints, on the corpus of 10-07);
//   Q6c (C16-04c) 'Liberate Survivors' (SOSLiberate x3 / x5): recruits as usual while the SOS signals left cover the count with one spare,
//       Liberate first when they run short, nothing when even every signal cannot reach it, Extermination's own rule, the card numbers
//       (LiberateCard), the key, the rails, the rescue screen's reroll hint;
//   Q7  (C16-09) survive quests on Hardcore / difficulty 3+: the one gate (RunContext.QuestCautionOn) and the factor's place in Survival, the
//       QUEST row and the log's words, the replay's DECISION over made-up outcomes (CautionReplay.Decide) and the shipped constant (the replay of
//       10-25 said 'drop': SurviveCaution 1.0).
// Every fixture is made up for the bench (the scores and the counts are invented, the names are the game's); generic names only (the repository
// is public).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static partial class QuestCases
    {
        /// <summary>0.17.0: the Q6b / Q6c / Q7 cases (pure) - from QuestCases.Run and from Verdict.DataFree.</summary>
        public static int RunPure017()
        {
            int was = _bad; _bad = 0;
            Console.WriteLine("\n=== 0.17.0: the quest's counted rules - Q6b 'Rerolled Item Chests' (C16-04b), Q6c 'Liberate Survivors' (C16-04c), Q7 survive quests on hard modes (C16-09)");
            RerollQuest();
            RerollCorpus();
            RerollSources();
            LiberateQuest();
            LiberateSources();
            SurviveQuest();
            SurviveSources();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            int bad = _bad; _bad = was;
            return bad;
        }

        // ---------------------------------------------------------------- Q6b: 'Rerolled Item Chests'
        static HintCard Hc(string name, double score, int rank, string head = "") { return new HintCard { Name = name, Score = score, Rank = rank, Kind = "item", Head = head }; }
        static ScreenCallIn QChest(string ask, int rerolls, bool counted, params HintCard[] cards)
        {
            var x = new ScreenCallIn { Screen = "Chest", Progress = 0.4, CanReroll = rerolls > 0, Rerolls = rerolls, RerollsText = rerolls + " (bench)", CanSkip = true, SkipCash = 400, SkipHeal = 150,
                Health = 1, Survival = 1, Cash = 0.3, QuestReroll = ask, RerolledHere = counted };
            x.Cards.AddRange(cards);
            return x;
        }
        static string Said(ScreenCall c) { return (c.Show ? ScreenCall.Name(c.Action) + " '" + c.Words + "'" : "silent") + " | " + c.Why; }
        // a QuestStory under Ghost_1's objective with the count read
        static QuestStory Ghost1(double have, QuestSteer steer = QuestSteer.On)
        {
            var st = new QuestStory { Quest = "GameHubQuest_Ghost_1", Steer = steer };
            var c = QuestStory.Decode("GameHubQuestObjectiveEventCount", 3, targetEvent: "RerollItemChecOncePerView", quest: st.Quest); c.Have = have;
            st.Counts.Add(c);
            return st;
        }

        static void RerollQuest()
        {
            const string ask = "Rerolled Item Chests 1 of 3";
            var a = ScreenCall.Decide(QChest(ask, 4, false, Hc("Teddy Bear", 3.20, 1), Hc("Pills", 2.10, 2), Hc("Hit Tracks", 1.80, 3)));
            Check("Q6b", "a chest under the quest, best 3.20 (under the cap 3.50), 4 rerolls: REROLL 'the quest counts this reroll (Rerolled Item Chests 1 of 3)' (58 of 64 characters)",
                a.Show && a.Action == HintAction.Reroll && a.Words == "the quest counts this reroll (Rerolled Item Chests 1 of 3)" && a.Words.Length <= ScreenCall.Budget
                && a.Why == "quest: Rerolled Item Chests 1 of 3 - best Teddy Bear 3.20 under the quest cap 3.50 | rerolls 4 (bench)", Said(a));
            var b = ScreenCall.Decide(QChest(ask, 4, true, Hc("Teddy Bear", 3.20, 1), Hc("Pills", 2.10, 2)));
            Check("Q6b", "the same chest once the game counted the view (_rerollOncePerView): no quest hint, the floor 2.5 not reached - silent, 'quest: Rerolled Item Chests 1 of 3 - this chest counted already | best ...'",
                !b.Show && b.Why.StartsWith("quest: Rerolled Item Chests 1 of 3 - this chest counted already | best ", StringComparison.Ordinal), Said(b));
            var c = ScreenCall.Decide(QChest(ask, 4, false, Hc("Golden Key", 3.82, 1), Hc("Pills", 2.10, 2)));
            Check("Q6b", "best 3.82: silent, '... at or over the quest cap 3.50 | best ...'", !c.Show && c.Why.StartsWith("quest: Rerolled Item Chests 1 of 3 - best Golden Key 3.82 at or over the quest cap 3.50 | best Golden Key 3.82 at or over the chest floor 2.50", StringComparison.Ordinal), Said(c));
            var d = ScreenCall.Decide(QChest(ask, 4, false, Hc("MedKit", 3.00, 1, "quest: hold a health item (0 of 1)"), Hc("Pills", 2.10, 2)));
            Check("Q6b", "the best card is the quest's own (a 'quest: ...' head, 3.00): silent - 'the best card is the quest's own'", !d.Show && d.Why.Contains("the best card is the quest's own (MedKit 3.00)"), Said(d));
            var e = ScreenCall.Decide(QChest(ask, 0, false, Hc("Teddy Bear", 3.20, 1)));
            var off = QChest(ask, 4, false, Hc("Teddy Bear", 3.20, 1)); off.Reroll = false; var f = ScreenCall.Decide(off);
            Check("Q6b", "no reroll left, or reroll hints off: silent ('no reroll left (0 (bench))' / 'reroll hints off')",
                !e.Show && e.Why.StartsWith("quest: Rerolled Item Chests 1 of 3 - no reroll left (0 (bench)) | ", StringComparison.Ordinal) && !f.Show && f.Why.StartsWith("quest: Rerolled Item Chests 1 of 3 - reroll hints off | ", StringComparison.Ordinal),
                Said(e) + " || " + Said(f));
            var g0 = QChest(ask, 2, true, Hc("Hit Tracks", 2.10, 1)); g0.KeepRerolls = 2; var g = ScreenCall.Decide(g0);
            Check("Q6b", "a counted chest with its best 2.10 under the floor, 2 chests to reroll and 2 rerolls left: no REROLL - 'rerolls kept for the quest (2 chests to reroll, 2 left)'",
                !g.Show && g.Why.EndsWith("best Hit Tracks 2.10 under the chest floor 2.50 - rerolls kept for the quest (2 chests to reroll, 2 left)"), Said(g));
            var g1 = QChest(ask, 4, false, Hc("Hit Tracks", 2.10, 1, "quest: hold a health item (0 of 1)")); g1.KeepRerolls = 2; var gq = ScreenCall.Decide(g1);
            Check("Q6b", "a chest not yet counted whose best card is the quest's own and under 2.5: the floor's REROLL (that reroll counts for the quest too)", gq.Show && gq.Action == HintAction.Reroll && gq.Words.StartsWith("the best", StringComparison.Ordinal), Said(gq));
            Func<int, bool, ScreenCall> lv = (rerolls, free) =>
            {
                var x = new ScreenCallIn { Screen = "LevelUp", Progress = 0.3, CanReroll = true, Rerolls = rerolls, FreeReroll = free, RerollsText = rerolls + " (bench)", CanSkip = true, SkipCash = 300, SkipHeal = 200, Health = 1, Survival = 0.95, Cash = 0.3, KeepRerolls = 2 };
                x.Cards.Add(new HintCard { Name = "Handgun", Score = 3.00, Rank = 1, Kind = "weapon" }); x.Cards.Add(new HintCard { Name = "Stimpack", Score = 2.80, Rank = 2, Kind = "ability" });
                return ScreenCall.Decide(x);
            };
            var l2 = lv(2, false); var l5 = lv(5, false); var lf = lv(2, true);
            Check("Q6b", "a level-up at 30 % (floor 3.5), best 3.00, 2 chests to reroll: 2 rerolls left - silent 'rerolls kept for the quest'; 5 left - REROLL as before; a FREE reroll - REROLL as before",
                !l2.Show && l2.Why.Contains("rerolls kept for the quest (2 chests to reroll, 2 left)") && l5.Show && l5.Action == HintAction.Reroll && l5.Words.StartsWith("the best", StringComparison.Ordinal)
                && lf.Show && lf.Action == HintAction.Reroll, Said(l2) + " || " + Said(l5));
            var none = QChest(null, 4, false, Hc("Teddy Bear", 3.20, 1)); var n0 = ScreenCall.Decide(none);
            Check("Q6b", "no reroll quest (QuestReroll null, nothing kept): a chest at 3.20 is silent as in 0.16.5 ('best ... at or over the chest floor 2.50')", !n0.Show && n0.Why == "best Teddy Bear 3.20 at or over the chest floor 2.50", Said(n0));
            // the revive guard (C16-13) holds the quest's REROLL as it holds the floor's
            var rv = QChest(ask, 4, false, Hc("Teddy Bear", 3.20, 1), new HintCard { Name = "Jewel of Life", Score = 1.4, Rank = 2, Kind = "item", Revive = true }); rv.SurviveQuest = true;
            var r = ScreenCall.Decide(rv);
            Check("Q6b", "a second life on the cards under a survive quest: no quest REROLL ('the revive guard holds it')", !r.Show && r.Why.StartsWith("quest: Rerolled Item Chests 1 of 3 - the revive guard holds it | ", StringComparison.Ordinal), Said(r));
            // the short words under the button (C16-15) and the one gold frame (C-3)
            Check("Q6b", "the short words: 'the quest counts this reroll' - shorter than the words, within the rails; a REROLL takes the screen's gold frame",
                a.Short == "the quest counts this reroll" && a.Short.Length < a.Words.Length && Wording.Rails(a.Short).Count == 0 && Wording.Rails(a.Words, ScreenCall.Budget).All(x => !x.StartsWith("glyph"))
                && ScreenCall.GoldFrame(true, a.Show, a.Action, true) == FrameOn.Button, "'" + a.Short + "'");
            // the rescue screen: RerollCall's keptFor (after the team rule)
            var offered = new List<Recruit> { new Recruit { Class = 1, Name = "Pyro", Score = 3.0 } };
            var possible = new List<Recruit> { new Recruit { Class = 2, Name = "Tank", Score = 4.2 } };
            var kept = RerollCall.Decide(offered, 1.5, possible, true, "2 (bench)", 1.0, null, keptFor: "rerolls kept for Rerolled Item Chests (2 chests to reroll, 2 left)");
            var free = RerollCall.Decide(offered, 1.5, possible, true, "2 (bench)", 1.0, null);
            Check("Q6b", "the rescue screen with a better survivor that could come: keptFor set - no hint, 'quest: rerolls kept for Rerolled Item Chests (2 chests to reroll, 2 left)'; keptFor null - the hint as before",
                !kept.Show && kept.Why == "quest: rerolls kept for Rerolled Item Chests (2 chests to reroll, 2 left)" && kept.Better.Count == 0 && free.Show && free.Better.Count == 1, kept.Why + " || " + free.Why);
            // the gates and the log's note
            var on = Ghost1(1); string onSaid = QuestStory.SaidOne(on.Counts[0], on);
            Check("Q6b", "On: RerollAsk 'Rerolled Item Chests 1 of 3', RerollsKept 2; '[quest] GameHubQuest_Ghost_1 -> event RerollItemChecOncePerView: 1 of 3 - REROLL hint on item chests whose best card is under 3.50'",
                on.RerollAsk() == ask && on.RerollsKept() == 2 && onSaid == "event RerollItemChecOncePerView: 1 of 3 - REROLL hint on item chests whose best card is under 3.50", on.RerollAsk() + " / " + on.RerollsKept() + " / " + onSaid);
            var info = Ghost1(1, QuestSteer.InfoOnly); var offq = Ghost1(1, QuestSteer.Off);
            var any = Ghost1(1); any.AnyOf = true; var failed = Ghost1(1); failed.Failed = true; var away = Ghost1(1); away.NotThisRun = "the leader (Tank) does not fit the quest's leader condition - the quest cannot complete this run";
            var met = Ghost1(3); var unread = Ghost1(double.NaN);
            bool gates = new[] { info, offq, any, failed, away, met, unread }.All(s => s.RerollAsk() == null && s.RerollsKept() == 0);
            Check("Q6b", "the gates: InfoOnly, Off, Any, failed, not this run, met, unread - RerollAsk null, RerollsKept 0; InfoOnly's note ends ' (info only)'; Off / Any / failed / not this run say 'progress only'; met '... met'",
                gates && QuestStory.SaidOne(info.Counts[0], info).EndsWith("under 3.50 (info only)") && QuestStory.SaidOne(offq.Counts[0], offq).EndsWith(" - progress only")
                && QuestStory.SaidOne(any.Counts[0], any).EndsWith(" - progress only") && QuestStory.SaidOne(failed.Counts[0], failed).EndsWith(" - progress only") && QuestStory.SaidOne(away.Counts[0], away).EndsWith(" - progress only")
                && QuestStory.SaidOne(met.Counts[0], met) == "event RerollItemChecOncePerView met", QuestStory.SaidOne(info.Counts[0], info));
            Check("Q6b", "the cap is the user's Q39 (3.5); the existing chest floor and its hints are unchanged", ScreenCall.QuestRerollCap == 3.5 && ScreenCall.ChestFloor == 2.5);
        }

        // a made-up corpus (run, best card of each chest view in order) replayed as the spec replays the real one: each view a ScreenCallIn with
        // one card, QuestReroll 'Rerolled Item Chests n of 3', CanReroll, 9 rerolls; n += 1 per quest REROLL shown while n < 3
        static readonly double[][] MadeUpRuns =
        {
            new[] { 3.20, 3.60, 2.90, 3.40 },        // three hints: 3.20, 2.90, 3.40 - reaches 3
            new[] { 3.80, 4.10, 3.30 },              // one hint - no
            new[] { 2.40, 3.49, 3.50, 3.10 },        // 2.40, 3.49 and 3.10 (3.50 is not under the cap) - reaches 3
            new[] { 3.00, 3.00 },                    // two chests only
            new[] { 1.90, 2.20, 2.00, 2.80, 3.30 },  // the first three - reaches 3, then the quest is met (no more quest hints)
            new[] { 4.00, 4.50, 3.90 },              // none
        };
        internal static int[] ReplayRuns(IEnumerable<double[]> runs, out int withThree, out int hints)
        {
            int reach = 0; withThree = 0; hints = 0;
            foreach (var run in runs)
            {
                int n = 0;
                if (run.Length >= 3) withThree++;
                foreach (double best in run)
                {
                    var x = new ScreenCallIn { Screen = "Chest", Progress = 0.4, CanReroll = true, Rerolls = 9, RerollsText = "9 (corpus)", QuestReroll = n < 3 ? "Rerolled Item Chests " + n + " of 3" : null };
                    x.Cards.Add(new HintCard { Name = "best", Score = best, Rank = 1, Kind = "item" });
                    var c = ScreenCall.Decide(x);
                    if (c.Show && c.Action == HintAction.Reroll && c.Words != null && c.Words.StartsWith("the quest counts", StringComparison.Ordinal)) { n++; hints++; }
                }
                if (n >= 3) reach++;
            }
            return new[] { reach };
        }
        static void RerollCorpus()
        {
            int with3, hints; int reach = ReplayRuns(MadeUpRuns, out with3, out hints)[0];
            Check("Q6b", "a made-up corpus of 6 runs replayed through ScreenCall.Decide at the cap 3.5: 3 runs reach 3 rerolled chests (5 with 3+ chests), 12 hints; a best of exactly 3.50 is not under the cap",
                reach == 3 && with3 == 5 && hints == 12, reach + " runs, " + with3 + " with 3+ chests, " + hints + " hints");
        }

        /// <summary>'ItemBench --replay-quest-chests &lt;corpus&gt;': a chest corpus (lines 'run TAB seconds TAB mode TAB best', '#' comments) replayed as
        /// Q6b replays the made-up one - for the corpus of the user's logs, which stays outside the repository.</summary>
        public static int ReplayCorpusFile(string path)
        {
            if (path == null || !File.Exists(path)) { Console.Error.WriteLine("usage: ItemBench --replay-quest-chests <chest_corpus.txt>"); return 2; }
            var runs = new List<List<double>>(); string last = null;
            foreach (var l in File.ReadLines(path))
            {
                if (l.StartsWith("#", StringComparison.Ordinal) || l.Trim().Length == 0) continue;
                var p = l.Split('\t'); double best;
                if (p.Length < 4 || !double.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out best)) continue;
                if (p[0] != last) { runs.Add(new List<double>()); last = p[0]; }
                runs[runs.Count - 1].Add(best);
            }
            int with3, hints; int reach = ReplayRuns(runs.Select(r => r.ToArray()), out with3, out hints)[0];
            Console.WriteLine("quest chest replay at the cap " + ScreenCall.QuestRerollCap.ToString("0.00", CultureInfo.InvariantCulture) + ": " + runs.Count + " runs, " + runs.Sum(r => r.Count) + " chest views; "
                + reach + " runs reach 3 rerolled chests (" + with3 + " runs with 3+ chests), " + hints + " hints");
            return 0;
        }

        static void RerollSources()
        {
            string hint = Src("RerollHint.cs"), call = Src("ScreenCall.cs"), plugin = Src("Plugin.cs"), syn = Src("Synergy.cs");
            Check("Q6b", "RerollHint reads the chest view's _rerollOncePerView (at the offer and again before each verdict: RerolledOnce(_sel)), st.RerollAsk() on chests, st.RerollsKept() on every screen and the rescue screen (keptFor:)",
                hint != null && hint.Contains("_rerollOncePerView") && hint.Contains("st.RerollAsk()") && hint.Contains("st.RerollsKept()") && hint.Contains("RerolledOnce(_sel)") && hint.Contains("keptFor:"));
            Check("Q6b", "ScreenCall: the quest's REROLL before the floor's, its reason kept in front of the floor's words (qWhy + \"best \"), a Short beside its Words",
                call != null && call.Contains("qWhy + \"best \"") && call.IndexOf("x.QuestReroll != null", StringComparison.Ordinal) < call.IndexOf("if (c.Floor > 0 && merit < c.Floor)", StringComparison.Ordinal)
                && call.Contains("c.Short = Pick(\"the quest counts this reroll\")"));
            Check("Q6b", "RerollCall.Decide takes keptFor / liberateFirst after the quest's team rule; no new cfg key - [Advice] ActionHints' description names the quest's REROLL",
                syn != null && syn.Contains("string keptFor = null, bool liberateFirst = false") && syn.IndexOf("if (quest != null && quest.Active)", StringComparison.Ordinal) < syn.IndexOf("if (keptFor != null)", StringComparison.Ordinal)
                && plugin != null && plugin.Contains("'REROLL - the quest counts this reroll (Rerolled Item Chests 1 of 3)'") && !plugin.Contains("\"QuestReroll\""));
        }

        // ---------------------------------------------------------------- Q6c: 'Liberate Survivors'
        static QuestStory Liberates(int need, double have, QuestSteer steer = QuestSteer.On, string quest = "GameHubQuest_Medic_3")
        {
            var st = new QuestStory { Quest = quest, Steer = steer };
            var c = QuestStory.Decode("GameHubQuestObjectiveEventCount", need, targetEvent: "SOSLiberate", quest: quest); c.Have = have;
            st.Counts.Add(c);
            return st;
        }
        static string V(QuestStory.LiberateCall lc) { return lc == null ? "null" : (lc.Force ? "Force" : lc.Unreachable ? "Unreachable" : "no force") + " (L " + lc.Left + ", E " + lc.Expected + ") '" + lc.Said + "'"; }

        static void LiberateQuest()
        {
            const double goal = 1200;
            // Need 3 along a Normal run's measured rescue screens (the first at 1:39, the second at 4:44)
            var n3 = Liberates(3, 0);
            var a1 = n3.Liberate(1, ModeKind.Timed, goal - 99); var a2 = n3.Liberate(2, ModeKind.Timed, goal - 284); var a3 = n3.Liberate(3, ModeKind.Timed, goal - 284);
            Check("Q6c", "Need 3, Normal: squad 1 at 1:39 (about 6 signals left) and squad 2 at 4:44 (5): recruits as usual; a full team: 'recruits as usual: a full team's signals liberate on their own'",
                a1 != null && !a1.Force && !a1.Unreachable && a1.Expected == 6 && a2 != null && !a2.Force && a2.Expected == 5 && a3 != null && !a3.Force && !a3.Unreachable && a3.Said == "recruits as usual: a full team's signals liberate on their own",
                V(a1) + " | " + V(a2) + " | " + V(a3));
            // Need 5: forced from 4:44, then one Liberate per screen until met
            var n5 = Liberates(5, 0);
            var b0 = n5.Liberate(1, ModeKind.Timed, goal - 99); var b1 = n5.Liberate(1, ModeKind.Timed, goal - 284);
            var steps = new List<string>(); bool seq = b0 != null && !b0.Force && b1 != null && b1.Force && b1.Left == 5 && b1.Expected == 5;
            int have = 1;
            foreach (var t in new[] { 450, 620, 801, 961 })
            {
                n5.Counts[0].Have = have; var lc = n5.Liberate(2, ModeKind.Timed, goal - t); steps.Add(t + " s " + V(lc));
                seq &= lc != null && lc.Force && lc.Left == 5 - have && lc.Expected == 5 - have;
                have++;
            }
            n5.Counts[0].Have = 5; var metCall = n5.Liberate(2, ModeKind.Timed, goal - 1000);
            Check("Q6c", "Need 5, Normal: no force at 1:39; Liberate first from 4:44 (5 signals for 5); after each Liberate 7:30 (4, 4), 10:20 (3, 3), 13:21 (2, 2), 16:01 (1, 1); met: null",
                seq && metCall == null, V(b0) + " | " + V(b1) + " | " + string.Join(" | ", steps));
            n5.Counts[0].Have = 0;
            var m0 = n5.Liberate(1, ModeKind.Timed, goal - 284, 0);
            Check("Q6c", "with no spare signal (margin 0, the answer Q40 did not take) 4:44 is not forced yet; the shipped margin is 1 (Q40: one spare)", m0 != null && !m0.Force && QuestStory.SosMargin == 1, V(m0));
            var open = Liberates(3, 0).Liberate(1, ModeKind.Open, 600);
            Check("Q6c", "Endless (open-ended): never forced - 'recruits as usual: open-ended'", open != null && !open.Force && !open.Unreachable && open.Said == "recruits as usual: open-ended", V(open));
            var hc = Liberates(5, 0).Liberate(1, ModeKind.Timed, 600 - 112);
            var whyL = new List<string> { "a level-up and cash instead of a recruit" }; var whyR = new List<string> { "S-tier rescue" };
            double sl = QuestStory.LiberateCard(hc, QuestSteer.On, true, 2.10, whyL), sr = QuestStory.LiberateCard(hc, QuestSteer.On, false, 6.80, whyR);
            Check("Q6c", "Hardcore (600 s), the first screen at 1:52 (about 2 signals left), Need 5: out of reach - both cards keep their scores, the Liberate card adds 'quest: Liberate Survivors out of reach (about 2 SOS signals for 5)'",
                hc != null && hc.Unreachable && !hc.Force && sl == 2.10 && sr == 6.80 && whyL.Last() == "quest: Liberate Survivors out of reach (about 2 SOS signals for 5)" && whyR.Count == 1
                && hc.Said == "out of reach this run (about 2 SOS signals left for 5)", V(hc) + " | " + whyL.Last());
            var ex = Liberates(5, 1).Liberate(2, ModeKind.Waves, 0); var exFull = Liberates(5, 1).Liberate(3, ModeKind.Waves, 0);
            Check("Q6c", "Extermination: squad 2, Need 5, 1 liberated - Liberate first 'quest: Liberate 4 more - only a chosen one counts'; a full team - out of reach (its SOS rewards give XP)",
                ex != null && ex.Force && ex.Head == "quest: Liberate 4 more - only a chosen one counts" && ex.Said == "Extermination: Liberate first while the team has a free slot" && exFull != null && exFull.Unreachable, V(ex) + " | " + V(exFull));
            var full = Liberates(5, 0).Liberate(3, ModeKind.Timed, 700);
            Check("Q6c", "a full team in a timed mode: no force (the signals liberate on their own)", full != null && !full.Force && !full.Unreachable, V(full));
            var f1 = Liberates(5, 0); f1.Failed = true; var f2 = Liberates(5, 0); f2.NotThisRun = "this run does not fit the quest's conditions (arena, mode, difficulty or leader) - the quest cannot complete this run";
            var f3 = Liberates(5, 0, QuestSteer.Off); var f4 = Liberates(5, 0); f4.AnyOf = true; var f5 = Liberates(5, double.NaN);
            Check("Q6c", "failed, not this run, Off, Any, the count unread: no rule (null)", new[] { f1, f2, f3, f4, f5 }.All(s => s.Liberate(1, ModeKind.Timed, 916) == null));
            // the card numbers
            var force = Liberates(5, 1).Liberate(2, ModeKind.Timed, goal - 450);
            var lw = new List<string> { "held: two level-ups and cash (Hijacked Signal)" }; var rw = new List<string> { "S-tier rescue" }; var rw2 = new List<string> { "B-tier rescue" };
            double lib = QuestStory.LiberateCard(force, QuestSteer.On, true, 2.10, lw), rec = QuestStory.LiberateCard(force, QuestSteer.On, false, 6.80, rw), rec2 = QuestStory.LiberateCard(force, QuestSteer.On, false, 3.00, rw2);
            Check("Q6c", "forced: Liberate 2.10 -> 5.00 with 'quest: Liberate 4 more - about 4 SOS signals left' first; a recruit 6.80 -> 4.90 with 'quest: a recruit takes an SOS signal the quest needs' last; a recruit 3.00 stays 3.00",
                lib == 5.00 && lw[0] == "quest: Liberate 4 more - about 4 SOS signals left" && rec == 4.90 && rw.Last() == "quest: a recruit takes an SOS signal the quest needs" && rec2 == 3.00,
                F(lib) + " '" + lw[0] + "' | " + F(rec) + " '" + rw.Last() + "' | " + F(rec2));
            var il = new List<string>(); var ir = new List<string>();
            double li = QuestStory.LiberateCard(force, QuestSteer.InfoOnly, true, 2.10, il), ri = QuestStory.LiberateCard(force, QuestSteer.InfoOnly, false, 6.80, ir);
            var free = Liberates(5, 0).Liberate(1, ModeKind.Timed, goal - 99); var fw = new List<string>();
            Check("Q6c", "InfoOnly: the scores unchanged, the lines after the card's own with ' (info only)'; no force: nothing at all",
                li == 2.10 && ri == 6.80 && il.Last() == "quest: Liberate 4 more - about 4 SOS signals left (info only)" && ir.Last() == "quest: a recruit takes an SOS signal the quest needs (info only)"
                && QuestStory.LiberateCard(free, QuestSteer.On, true, 2.10, fw) == 2.10 && fw.Count == 0 && QuestStory.LiberateCard(null, QuestSteer.On, false, 6.80, fw) == 6.80);
            // the key: the verdict and the Liberates left, not the signals left
            var kq = Liberates(5, 0);
            kq.Sos = kq.Liberate(1, ModeKind.Timed, goal - 99); string k6 = kq.Key();                  // E 6, no force
            kq.Sos = kq.Liberate(1, ModeKind.Timed, goal - 200); string k5 = kq.Key();                 // E 5 ... forced (5 - 1 < 5)
            var kn = Liberates(3, 0); kn.Sos = kn.Liberate(1, ModeKind.Timed, goal - 99); string kn6 = kn.Key(); kn.Sos = kn.Liberate(1, ModeKind.Timed, goal - 284); string kn5 = kn.Key();
            Check("Q6c", "the key: the same verdict at about 6 and 5 signals left - the same key (no log line, no rebuild as the clock runs); a verdict that flips - a new key",
                kn6 == kn5 && k6 != k5, kn6 + " / " + kn5 + " | " + k6 + " / " + k5);
            // the log's words
            var said = Liberates(5, 1); said.Sos = said.Liberate(2, ModeKind.Timed, goal - 450);
            string one = QuestStory.SaidOne(said.Counts[0], said);
            var saidInfo = Liberates(5, 1, QuestSteer.InfoOnly); saidInfo.Sos = saidInfo.Liberate(2, ModeKind.Timed, goal - 450);
            Check("Q6c", "'[quest] GameHubQuest_Medic_3 -> event SOSLiberate: 1 of 5 - Liberate first (about 4 SOS signals left, margin 1)'; InfoOnly ' (info only)'",
                one == "event SOSLiberate: 1 of 5 - Liberate first (about 4 SOS signals left, margin 1)" && QuestStory.SaidOne(saidInfo.Counts[0], saidInfo).EndsWith("margin 1) (info only)"), one);
            // the Head lines on the card, through Wording.Quest within the card rails (single digits: L <= 5, E <= 7 in a timed mode)
            var heads = new List<string>();
            for (int l = 1; l <= 5; l++) for (int e = Math.Max(0, l - 1); e <= 7; e++) heads.Add("quest: Liberate " + l + " more - about " + e + " SOS signals left");
            heads.Add(ex.Head); heads.Add("quest: Liberate Survivors - no recruit wanted (Liberate)");
            var bad = heads.Select(h => Wording.Quest(h)).Where(w => Wording.Rails(w).Count > 0 || !w.StartsWith("Quest: ", StringComparison.Ordinal)).ToList();
            Check("Q6c", "every Liberate line a card can say stays within the card rails as 'Quest: ...' (" + heads.Count + " lines; 'Quest: Liberate 4 more - about 4 SOS signals left' is 49 of 50)",
                bad.Count == 0 && Wording.Quest("quest: Liberate 4 more - about 4 SOS signals left") == "Quest: Liberate 4 more - about 4 SOS signals left", string.Join(" | ", bad));
            foreach (var h in heads) _lines.Add(h);
            // the rescue screen's reroll hint
            var offered = new List<Recruit> { new Recruit { Class = 1, Name = "Pyro", Score = 3.0 } }; var possible = new List<Recruit> { new Recruit { Class = 2, Name = "Tank", Score = 4.2 } };
            var silent = RerollCall.Decide(offered, 5.0, possible, true, "3 (bench)", 1.0, null, liberateFirst: true);
            var team = new QuestSos { Active = true, Max = 1, Size = 1, Words = "stay solo" };
            var teamFirst = RerollCall.Decide(offered, 5.0, possible, true, "3 (bench)", 1.0, team, liberateFirst: true);
            Check("Q6c", "the rescue screen: liberateFirst - no hint, 'quest: Liberate Survivors - no recruit wanted (Liberate)'; the team rule still decides first ('quest: stay solo - no recruit wanted (Liberate)')",
                !silent.Show && silent.Why == "quest: Liberate Survivors - no recruit wanted (Liberate)" && !teamFirst.Show && teamFirst.Why == "quest: stay solo - no recruit wanted (Liberate)", silent.Why + " || " + teamFirst.Why);
            Check("Q6c", "the constants: a signal about every 170 s, one spare (Q40), Liberate to 5.00 and a recruit under 4.90, a full team's signals liberate (FullTeamLiberates)",
                QuestStory.SosCadence == 170 && QuestStory.LiberateFirst == 5.0 && QuestStory.RecruitUnder == 4.9 && QuestStory.FullTeamLiberates);
        }

        static void LiberateSources()
        {
            string ranker = Src("Ranker.cs"), hint = Src("RerollHint.cs"), plan = Src("Plan.cs"), quest = Src("Quest.cs"), plugin = Src("Plugin.cs");
            Check("Q6c", "Ranker.ScoreSos: QuestStory.LiberateCard on the Liberate card (true) and on a recruit (false), only while the team rule is not active (!lq.Active)",
                ranker != null && ranker.Contains("QuestStory.LiberateCard(st.Sos, st.Steer, true") && ranker.Contains("QuestStory.LiberateCard(st.Sos, st.Steer, false") && ranker.Contains("!lq.Active")
                && ranker.IndexOf("lq.Card(null, c.Score, c.Why)", StringComparison.Ordinal) < ranker.IndexOf("QuestStory.LiberateCard(st.Sos, st.Steer, true", StringComparison.Ordinal));
            Check("Q6c", "Quest.Story sets the rule before the key (story.Liberate(squad, kind, Remaining)); RerollHint's inp.LiberateFirst; the PLAN's SOS row 'quest: Liberate (n of N)'; [Advice] QuestSteer's description",
                quest != null && quest.Contains("story.Liberate(s.Squad.Count, s.Ctx.Kind, s.Ctx.Remaining)") && quest.IndexOf("story.Liberate(s.Squad.Count", StringComparison.Ordinal) < quest.IndexOf("string key = story.Key();", StringComparison.Ordinal)
                && hint != null && hint.Contains("inp.LiberateFirst") && plan != null && plan.Contains("quest: Liberate (") && plugin != null && plugin.Contains("Under a quest that counts Liberates"));
        }

        // ---------------------------------------------------------------- Q7: survive quests on hard modes
        static RunContext Rc(string mode, int d, double goal, double seconds, double k, double health = 1, bool aware = true)
        {
            return new RunContext { Mode = mode, Difficulty = d, Goal = goal, Seconds = seconds, QuestCaution = k, Health = health, D = new Doctrine { ModeAware = aware } };
        }
        static void SurviveQuest()
        {
            Func<RunContext, double> S = c => c.Survival;
            var h20 = Rc("Hardcore", 1, 600, 120, 1.15); var h20n = Rc("Hardcore", 1, 600, 120, 1.0);
            var h50 = Rc("Hardcore", 1, 600, 300, 1.15); var h50n = Rc("Hardcore", 1, 600, 300, 1.0);
            var hurt = Rc("Hardcore", 1, 600, 300, 1.15, 0.3);
            double base50 = (0.8 + 0.5 * 0.5) * 1.25;
            Check("Q7", "QuestCaution 1.15 at Hardcore d1: 20 % of the run unchanged; 50 % x1.15 of the unhurt value; hurting (health 30 %) the hurting term after it: base x 1.15 + 0.16",
                Math.Abs(S(h20) - S(h20n)) < 1e-9 && Math.Abs(S(h50) - S(h50n) * 1.15) < 1e-9 && Math.Abs(S(hurt) - (base50 * 1.15 + 0.16)) < 1e-9 && !h20.QuestCautionOn && h50.QuestCautionOn,
                F(S(h20)) + " / " + F(S(h50n)) + " -> " + F(S(h50)) + " / hurt " + F(S(hurt)));
            var n1 = Rc("Normal", 1, 1200, 600, 1.15); var n3 = Rc("Normal", 3, 1200, 600, 1.15); var n3n = Rc("Normal", 3, 1200, 600, 1.0);
            var oh = Rc("OneHit", 3, 300, 200, 1.15);
            var e5 = Rc("Endless", 3, 0, 300, 1.15); var e5n = Rc("Endless", 3, 0, 300, 1.0); var e8 = Rc("Endless", 3, 0, 480, 1.15); var e8n = Rc("Endless", 3, 0, 480, 1.0);
            Check("Q7", "Normal d1 at 50 %: unchanged; Normal d3: x1.15; One Hit: 0 (never a hard mode for this rule); Endless d3 at 5:00 (25 % by its clock) unchanged, at 8:00 x1.15",
                !n1.QuestCautionOn && Math.Abs(S(n1) - S(Rc("Normal", 1, 1200, 600, 1.0))) < 1e-9 && n3.QuestCautionOn && Math.Abs(S(n3) - S(n3n) * 1.15) < 1e-9 && S(oh) == 0 && !oh.QuestCautionOn
                && !e5.QuestCautionOn && Math.Abs(S(e5) - S(e5n)) < 1e-9 && e8.QuestCautionOn && Math.Abs(S(e8) - S(e8n) * 1.15) < 1e-9,
                F(S(n3n)) + " -> " + F(S(n3)) + " | Endless 8:00 " + F(S(e8n)) + " -> " + F(S(e8)));
            var ma = Rc("Hardcore", 1, 600, 300, 1.15, 1, false); var ma1 = Rc("Hardcore", 1, 600, 300, 1.0, 1, false);
            Check("Q7", "[Advice] ModeAware off: unchanged and the gate shut; QuestCaution 1.0 (no survive quest followed): unchanged",
                !ma.QuestCautionOn && Math.Abs(S(ma) - S(ma1)) < 1e-9 && !h50n.QuestCautionOn);
            Check("Q7", "the [ctx] line: 'survival x1.51 (quest)' while the gate is open, the 0.16.5 words otherwise",
                h50.ToString().Contains("survival x" + S(h50).ToString("0.00") + " (quest)") && !h50n.ToString().Contains("(quest)") && !h20.ToString().Contains("(quest)"), h50.ToString());
            // the QUEST row and the log's words
            Check("Q7", "SurviveText: not a survive quest, not followed, not a hard mode, before 30 % - null; the factor on 'survive the run - caution up' (28 characters); InfoOnly 'survive the run (info only)'; else 'survive the run'",
                QuestRules.SurviveText(false, true, QuestSteer.On, true, 0.5, true) == null && QuestRules.SurviveText(true, false, QuestSteer.On, true, 0.5, true) == null
                && QuestRules.SurviveText(true, true, QuestSteer.On, false, 0.5, false) == null && QuestRules.SurviveText(true, true, QuestSteer.On, true, 0.29, true) == null
                && QuestRules.SurviveText(true, true, QuestSteer.On, true, 0.5, true) == "survive the run - caution up" && QuestRules.SurviveText(true, true, QuestSteer.InfoOnly, true, 0.5, false) == "survive the run (info only)"
                && QuestRules.SurviveText(true, true, QuestSteer.On, true, 0.5, false) == "survive the run" && "survive the run - caution up".Length == 28);
            Check("Q7", "the log's words: 'survive the run: advice unchanged (not a hard mode)', 'survive the run: caution from 30% of the run', 'survive the run: advice unchanged (no caution factor)' while the factor is dropped",
                QuestRules.SurviveLog(true, false, false, null, "Normal", 1) == "survive the run: advice unchanged (not a hard mode)" && QuestRules.SurviveLog(true, true, false, null, "Hardcore", 1) == "survive the run: caution from 30% of the run"
                && QuestRules.SurviveLog(true, true, false, "survive the run", "Hardcore", 1) == "survive the run: advice unchanged (no caution factor)" && QuestRules.SurviveLog(false, true, true, null, "Hardcore", 1) == null
                && QuestRules.SurviveLog(true, true, false, "survive the run (info only)", "Hardcore", 1) == "survive the run (info only): advice unchanged",
                QuestRules.SurviveLog(true, true, true, "survive the run - caution up", "Hardcore", 1));
            var only = new QuestRules { Quest = "GameHubQuest_Tank_4", Survive = true, SurviveRow = "survive the run", SurviveSaid = QuestRules.SurviveLog(true, true, false, "survive the run", "Hardcore", 1) };
            var none = new QuestRules { Quest = "GameHubQuest_Tank_4", Survive = true };
            var withRule = new QuestRules { Quest = "GameHubQuest_Medic_5", Survive = true, SurviveRow = "survive the run" }; withRule.List.Add(new QuestRule { Ask = QuestAsk.Armor, Need = 150, Have = 40 });
            Check("Q7", "a survive-only quest: the QUEST row 'survive the run', the log 'survive the run: advice unchanged (no caution factor)', the row in the key; after the rules' items while room is left",
                only.Row().SequenceEqual(new[] { "survive the run" }) && only.Said() == "survive the run: advice unchanged (no caution factor)" && only.Key() != none.Key() && none.Row().Count == 0
                && withRule.Row().SequenceEqual(new[] { "armor to 150", "survive the run" }) && withRule.Row(1).SequenceEqual(new[] { "armor to 150" }),
                string.Join(" / ", withRule.Row()) + " | " + only.Said());
            // the replay's decision over made-up outcomes (CautionReplay.Decide; the logs themselves are never a fixture)
            Func<double, int, int, bool, bool, CautionReplay.KRow> R = (k, n, ch, order, other) => new CautionReplay.KRow { K = k, Offers = n, Changes = ch, BuildOrder = order, NonSurvival = other };
            string adopt = CautionReplay.Decide(1.0, true, new[] { R(1.10, 200, 1, false, false), R(1.15, 200, 2, false, false) });
            string upper = CautionReplay.Decide(0.97, true, new[] { R(1.10, 200, 4, false, false), R(1.15, 200, 20, false, false) });
            string order = CautionReplay.Decide(1.0, true, new[] { R(1.10, 200, 1, false, false), R(1.15, 200, 2, true, false) });
            string both = CautionReplay.Decide(1.0, true, new[] { R(1.10, 200, 18, false, false), R(1.15, 200, 20, false, false) });
            string few = CautionReplay.Decide(1.0, true, new[] { R(1.10, 200, 0, false, false), R(1.15, 200, 0, false, false) });
            string fid = CautionReplay.Decide(0.90, true, new[] { R(1.10, 200, 1, false, false), R(1.15, 200, 2, false, false) });
            string k1 = CautionReplay.Decide(1.0, false, new[] { R(1.10, 200, 1, false, false), R(1.15, 200, 2, false, false) });
            string bug = CautionReplay.Decide(1.0, true, new[] { R(1.10, 200, 1, false, true), R(1.15, 200, 2, false, false) });
            Check("Q7", "the replay's decision: 1 % adopts 1.15; over 8 % at 1.15 but 2 % at 1.10 adopts 1.10; a build-order card displaced at 1.15 falls back to 1.10; both over 8 % or none at all (under 0.5 %) drop; fidelity under 95 %, a moved #1 at 1.00 or a change to a non-survival card give no verdict",
                adopt == "caution replay: adopt k=1.15 from 0.3" && upper == "caution replay: adopt k=1.10 from 0.3" && order == "caution replay: adopt k=1.10 from 0.3" && both == "caution replay: drop" && few == "caution replay: drop"
                && fid.StartsWith("caution replay: no verdict (fidelity 90 %", StringComparison.Ordinal) && k1.Contains("k = 1.00 moved a #1") && bug.Contains("replay bug"),
                adopt + " | " + upper + " | " + order + " | " + both + " | " + few + " | " + fid);
            // the shipped constant: the replay of 10-25 said 'drop' (0 of 119 Hardcore offers moved at x1.10 / x1.15) - the user's Q41: the QUEST row alone
            var shipped = Rc("Hardcore", 1, 600, 300, QuestRules.SurviveCaution);
            Check("Q7", "the shipped SurviveCaution is 1.0 (Q41: the replay kept less than x1.10 - the QUEST row alone): no factor, the gate shut, the row 'survive the run', from 30 % (SurviveFrom 0.3)",
                QuestRules.SurviveCaution == 1.0 && !QuestRules.CautionKept && !shipped.QuestCautionOn && Math.Abs(S(shipped) - S(h50n)) < 1e-9 && QuestRules.SurviveFrom == 0.3
                && QuestRules.SurviveText(true, true, QuestSteer.On, QuestRules.Hard("Hardcore", 1), shipped.QuestProgress, shipped.QuestCautionOn) == "survive the run");
            Check("Q7", "QuestRules.Hard: Hardcore at any difficulty, any mode at difficulty 3+, never One Hit", QuestRules.Hard("Hardcore", 1) && QuestRules.Hard("Normal", 3) && QuestRules.Hard("Endless", 5) && !QuestRules.Hard("Normal", 2) && !QuestRules.Hard("OneHit", 5));
        }

        static void SurviveSources()
        {
            string state = Src("GameState.cs"), ctx = Src("Context.cs"), quest = Src("Quest.cs"), plugin = Src("Plugin.cs");
            string program = null; try { string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Program.cs")); if (File.Exists(p)) program = File.ReadAllText(p); } catch { }
            Check("Q7", "GameState.ReadContext sets QuestCaution from the followed survive quest; Survival's factor sits before the hurting term; ToString prints ' (quest)'",
                state != null && state.Contains("c.QuestCaution = c.SurviveQuest ? QuestRules.SurviveCaution : 1.0;") && ctx != null
                && ctx.IndexOf("if (QuestCautionOn) v *= QuestCaution;", StringComparison.Ordinal) > 0 && ctx.IndexOf("if (QuestCautionOn) v *= QuestCaution;", StringComparison.Ordinal) < ctx.IndexOf("if (Health < 0.5) v += 0.4", StringComparison.Ordinal)
                && ctx.Contains("(QuestCautionOn ? \" (quest)\" : \"\")"));
            Check("Q7", "Quest.Rules: the row and the log's words from the one gate (ctx.QuestCautionOn), a survive-only quest no longer returns null; no new cfg key (QuestSteer's description says it)",
                quest != null && quest.Contains("QuestRules.SurviveText(true, rules.Follows, steer, hard, ctx.QuestProgress, on)") && quest.Contains("rules.SurviveRow == null) return null;")
                && plugin != null && plugin.Contains("shows 'survive the run' on the QUEST row from 30% of the run") && !plugin.Contains("\"SurviveCaution\""));
            Check("Q7", "Program.cs dispatches --replay-caution before the path handling, and --replay-quest-chests",
                program != null && program.Contains("CautionReplay.Command(args, cr)") && program.IndexOf("--replay-caution", StringComparison.Ordinal) < program.IndexOf("Verdict.Install()", StringComparison.Ordinal)
                && program.Contains("QuestCases.ReplayCorpusFile("));
        }
    }
}
