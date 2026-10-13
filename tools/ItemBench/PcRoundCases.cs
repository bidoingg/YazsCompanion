// 0.16.1, the 10-11 PC round (one PC session with the 0.16.1 draft, 3440 x 1440; the first Extermination run in any log): two faults
// older than 0.16.1, fixed before its commit. Synthesized runs only - no logged run is replayed here.
//   XH1 - XH8  COMPANION-LOG-03: Extermination's horizon. RunContext.Remaining was (1 - Wave / Waves) x the game's TimeRequiredForSuccess,
//        which is not that run's length: '~3 level-ups to come' at wave 2/20 while about fifty came, so Liberate led the rescues, 'Too late
//        for a recruit to grow (wave 3/20)', evolutions out of reach. Now the waves still to play x the wave's length (a 42 s prior
//        blended with the run's own Seconds / (Wave - 1)), on every path (ModeAware off too), never the game's goal; the [ctx] line
//        shows the wave length used and the game's goal; Extermination's level-up pace prior 3.9 a minute. Every other mode as it was.
//   BO1  COMPANION-LOG-06: a WHY band said 'Boosted by X (Owner)' and 'Boosted by X' for one team passive - the head named the owner,
//        the tag facts did not, and the de-duplication compares the words. The tag facts carry the owner now: one reason. Not an
//        evolution's (no head; its own line 'Evolution - boosted by X' must still swallow the reason, as in 0.16.0 - the review's catch).
// Pure (no game data): runs in the full bench and in --no-data (Verdict.DataFree). Game names only (the repository is public).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class PcRoundCases
    {
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static string F(double v) { return v.ToString("0.00", IC); }
        static string F0(double v) { return v.ToString("0", IC); }
        static double Clamp01(double v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
        static string Src(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod", file));
            return File.Exists(p) ? File.ReadAllText(p).Replace("\r\n", "\n") : null;
        }

        public static int RunPure()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.16.1, the 10-11 PC round: Extermination's horizon from its waves (COMPANION-LOG-03), one 'Boosted by' reason (COMPANION-LOG-06) - PcRoundCases.cs");
            Early();
            Late();
            Guards();
            AwareOff();
            OtherModes();
            CtxLine();
            Pace();
            Sources();
            OneBoost();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        // ================================================================ a made-up Extermination run
        /// <summary>A 20-wave Extermination run at <paramref name="wave"/>, <paramref name="seconds"/> of play, with a level-up pace handed in
        /// (<paramref name="rate"/>) or replayed from <paramref name="pace"/>; <paramref name="goal"/> = what TimeRequiredForSuccess holds
        /// (made up: the live value is what the [ctx] line will show).</summary>
        static RunContext Ext(int wave, double seconds, double rate, double goal = 55, LevelPace pace = null, Doctrine d = null, int waves = 20)
        {
            var c = new RunContext { Mode = "Extermination", Difficulty = 1, Seconds = seconds, Goal = goal, Wave = wave, Waves = waves, D = d ?? new Doctrine() };
            if (pace != null) { c.LevelUps = pace.Count; c.LevelRate = pace.Rate(c.Mode); }
            else { c.LevelRate = rate; c.LevelUps = (int)(seconds / 15); }
            return c;
        }

        // ---- 0.16.0's horizon (Context.cs up to 0.16.0), kept to show what changed and to prove the other modes untouched
        static bool Aware(RunContext c) { return c.D == null || c.D.ModeAware; }
        static double OldProgress(RunContext c)
        {
            if (!Aware(c)) return c.Goal > 0 ? Clamp01(c.Seconds / c.Goal) : Clamp01(c.Seconds / 1200.0);
            switch (c.Kind)
            {
                case ModeKind.Open: return 0.15;
                case ModeKind.Waves: return c.Waves > 0 ? Clamp01((double)c.Wave / c.Waves) : Clamp01(c.Seconds / 1200.0);
                default: return c.Goal > 0 ? Clamp01(c.Seconds / c.Goal) : Clamp01(c.Seconds / 1200.0);
            }
        }
        static double OldRemaining(RunContext c)
        {
            if (Aware(c) && c.Kind == ModeKind.Open) return 600;
            double goal = c.Goal > 0 ? c.Goal : 1200.0;
            if (Aware(c) && c.Kind == ModeKind.Waves && c.Waves > 0) return Math.Max(0, (1 - OldProgress(c)) * goal);
            return Math.Max(0, goal - c.Seconds);
        }
        static string OldClock(RunContext c)
        {
            if (c.Kind == ModeKind.Waves && c.Waves > 0) return "wave " + c.Wave + "/" + c.Waves;
            if (c.Kind == ModeKind.Open || c.Goal <= 0) return "open-ended";
            int left = (int)Math.Round(OldRemaining(c));
            return (left / 60) + ":" + (left % 60).ToString("00") + " left";
        }
        /// <summary>The pace ExpectedLevelUps reads (unchanged code; the prior is LevelPace.Prior of the mode).</summary>
        static double Rate(RunContext c)
        {
            double rate = c.LevelRate > 0 ? c.LevelRate
                : LevelPace.Blend(LevelPace.Prior(c.Mode), c.LevelUps > 0 && c.Seconds > 0 ? c.LevelUps / Math.Max(LevelPace.MinWindow, c.Seconds) * 60.0 : 0, c.LevelUps);
            return Math.Max(0.6, Math.Min(5.0, rate));
        }
        static double OldLevelUps(RunContext c) { return Rate(c) * OldRemaining(c) / 60.0; }
        static double OldRecruitValue(RunContext c)
        {
            int r = c.D == null ? 0 : c.D.Recruit, t = c.D == null ? 1 : c.D.Timing;
            if (r == 1 || t == 0) return 1;
            if (r == 2) return OldProgress(c) < 0.5 ? 1 : 0.2;
            return Clamp01(OldLevelUps(c) / 12.0);
        }

        // ---- the SOS cards' scores (Ranker.RecruitScore / ScoreSos read game types: the bench keeps their last step, and Sources (XH8)
        // holds Ranker.cs to it): a recruit 2.0 x the recruit value + fit x (0.35 + 0.65 x value), fit = the guides' rescue tier (S 1.6, A 1.1,
        // B 0.6, C 0.1) + 1.1 per bought synergy node; Liberate HeldRules.LiberateScore (1 + 3.2 x (1 - value), nothing held)
        static double Recruit(double value, string tier, int bought) { double fit = Knowledge.Tier(tier, 1.6, 1.1, 0.6, 0.1) + 1.1 * bought; return 2.0 * value + fit * (0.35 + 0.65 * value); }
        static double Liberate(double value) { return HeldRules.LiberateScore(value, 0, false, 1); }
        // the rescue card's line: Ranker.ScoreSos sets Late (the clock) under RerollCall.Late
        static string LiberateLine(RunContext c) { return Wording.Card(new CardWords { Kind = SayKind.Recruit, Recruit = new RecruitSay { Liberate = true, Late = c.RecruitValue < RerollCall.Late ? c.ClockText : null } }, 1, null, null); }

        // ================================================================ XH1 wave 3 of 20
        static void Early()
        {
            // the first minute: four level-ups, two at each wave's end (made-up clocks)
            var pace = new LevelPace(); foreach (var t in new[] { 30.0, 30.0, 60.0, 60.0 }) pace.Add(t);
            var c = Ext(3, 62, 0, 55, pace);
            var old = new LevelPace(); foreach (var t in new[] { 30.0, 30.0, 60.0, 60.0 }) old.Add(t);
            var was = Ext(3, 62, 0, 55, old); was.LevelRate = old.Rate("Normal");          // up to 0.16.0 Extermination borrowed Normal's 2.9
            double lu = c.ExpectedLevelUps, rv = c.RecruitValue, s = Recruit(rv, "S", 0), lib = Liberate(rv);
            double oldLu = OldLevelUps(was), oldRv = OldRecruitValue(was);
            Check("XH1", "wave 3/20 at 01:02 (game goal 55 s): ~" + F0(lu) + " level-ups to come over " + F0(c.Remaining) + " s (18 waves of " + F0(c.WaveSeconds) + " s), recruit value " + F(rv)
                + " - 0.16.0's horizon gave ~" + F0(oldLu) + " over " + F0(OldRemaining(was)) + " s, recruit value " + F(oldRv),
                lu >= 30 && rv == 1 && oldLu < 5 && oldRv < RerollCall.Late);
            Check("XH1", "an S-tier recruit with nothing bought rates " + F(s) + " over Liberate " + F(lib) + " (0.16.0: " + F(Recruit(oldRv, "S", 0)) + " under " + F(Liberate(oldRv)) + ")",
                s > lib && Recruit(oldRv, "S", 0) < Liberate(oldRv));
            string line = LiberateLine(c), oldLine = Wording.Card(new CardWords { Kind = SayKind.Recruit, Recruit = new RecruitSay { Liberate = true, Late = "wave 3/20" } }, 1, null, null);
            Check("XH1", "the Liberate card no longer says it is too late: '" + line + "' (0.16.0: '" + oldLine + "')",
                line == "A level-up and cash instead of a recruit" && oldLine.StartsWith("Too late for a recruit", StringComparison.Ordinal));
            Func<string, string, double, Recruit> r = (n, tier, sc) => new Recruit { Name = n, Score = sc, Class = n.Length, Tier = tier };
            var hint = RerollCall.Decide(new[] { r("Ranger", "B", Recruit(rv, "B", 0)) }, lib, new[] { r("SWAT", "S", Recruit(rv, "S", 2)) }, true, "2", rv);
            var oldHint = RerollCall.Decide(new[] { r("Ranger", "B", Recruit(oldRv, "B", 0)) }, Liberate(oldRv), new[] { r("SWAT", "S", Recruit(oldRv, "S", 2)) }, true, "2", oldRv);
            Check("XH1", "the rescue reroll hint judges the recruits again ('" + hint.Why + "'), not 'late in the run' as 0.16.0 did",
                !hint.Why.StartsWith("late in the run", StringComparison.Ordinal) && hint.Show && oldHint.Why.StartsWith("late in the run", StringComparison.Ordinal));
            Check("XH1", "a new ability's three picks and an evolution's four are in reach (Reach(3) " + F(c.Reach(3)) + ", Reach(4) " + F(c.Reach(4)) + "; 0.16.0 about "
                + F(Clamp01(0.35 * oldLu / 3)) + "): no 'little time left' or 'too late to evolve it'", c.Reach(3) >= 0.6 && c.Reach(4) >= 0.5);
            var goals = new[] { 0.0, 55, 600, 1200, 30000 }.Select(g => { var x = Ext(3, 62, 0, g, pace); return x.Remaining; }).ToList();
            Check("XH1", "the game's goal moves nothing: TimeRequiredForSuccess 0 / 55 / 600 / 1200 / 30000 -> " + string.Join(" / ", goals.Select(F0)) + " s left",
                goals.All(g => g == goals[0]));
        }

        // ================================================================ XH2 the last waves
        static void Late()
        {
            var rows = new List<string>(); bool ok = true;
            foreach (int wave in new[] { 10, 16, 18, 19, 20 })
            {
                var c = Ext(wave, (wave - 1) * 42.0, 3.9);
                double rv = c.RecruitValue, s = Recruit(rv, "S", 0), weak = Recruit(rv, "", 0), lib = Liberate(rv);
                rows.Add("wave " + wave + ": ~" + F0(c.ExpectedLevelUps) + ", value " + F(rv) + ", S " + F(s) + " / untiered " + F(weak) + " / Liberate " + F(lib) + " '" + LiberateLine(c) + "'");
                if (wave <= 16) ok &= rv == 1 && s > lib && weak > lib;
                if (wave == 18) ok &= s > lib && lib > weak && rv >= RerollCall.Late;
                if (wave == 19) ok &= lib > s;
                if (wave == 20) ok &= rv < RerollCall.Late && LiberateLine(c) == "Too late for a recruit to grow (wave 20/20)";
            }
            Check("XH2", "42 s waves at 3.9 level-ups a minute: recruits lead to wave 16; at wave 18 Liberate passes a recruit with nothing for the squad, at 19 an S-tier one; 'too late' only at wave 20",
                ok, string.Join(" | ", rows));
        }

        // ================================================================ XH3 the guards
        static void Guards()
        {
            var w1 = Ext(1, 10, 3.9); var w0 = Ext(1, 0, 3.9);
            Check("XH3", "wave 1: nothing finished to divide by - the prior alone, " + F0(w1.WaveSeconds) + " s a wave, " + F0(w1.Remaining) + " s for 20 waves (at 00:00 too: " + F0(w0.Remaining) + ")",
                w1.WaveSeconds == RunContext.WavePrior && w1.Remaining == 20 * RunContext.WavePrior && w0.Remaining == w1.Remaining && !double.IsNaN(w1.ExpectedLevelUps));
            var w5 = Ext(5, 4 * 30.0, 3.9);
            Check("XH3", "the run's own waves count by the waves finished: 4 of 30 s with the 42 s prior as 4 -> " + F(w5.WaveSeconds) + " s",
                Math.Abs(w5.WaveSeconds - (42 * 4 + 30 * 4) / 8.0) < 1e-9);
            var won = Ext(21, 840, 3.9);
            Check("XH3", "past the last wave (the game counts 20 finished): 0 s left, '" + won.ClockText + "'", won.Remaining == 0 && won.ExpectedLevelUps == 0);
            double kg = 1200; BadgeMode m; if (Knowledge.Current != null && Knowledge.Current.BadgeModes.TryGetValue("Extermination", out m) && m.Goal > 0) kg = m.Goal;
            var bad = new List<string>();
            foreach (var g in new[] { 0.0, 55 })
                foreach (var x in new[] { Ext(0, 300, 3.9, g, null, null, 0), Ext(0, 300, 3.9, g, null, null, 20), Ext(4, 300, 3.9, g, null, null, 0) })
                {
                    double want = Math.Max(0, (1 - 300 / kg) * kg);
                    if (Math.Abs(x.Remaining - want) > 1e-9 || Math.Abs(x.Progress - 300 / kg) > 1e-12 || x.ClockText != "15:00 left") bad.Add("wave " + x.Wave + "/" + x.Waves + " goal " + g + ": " + F0(x.Remaining) + " s, '" + x.ClockText + "'");
                }
            Check("XH3", "waves not read (no count, or no wave): (1 - Progress) x the knowledge goal (" + F0(kg) + " s) - 900 s left at 5:00, '15:00 left', whatever the game's goal (0.16.0: 'open-ended' with goal 0, 'wave 0/20')",
                bad.Count == 0, string.Join("; ", bad));
            // the badge advice's run shape (Loadout.Averaged: the knowledge goal, no waves) reads Extermination as 0.16.0 did
            var lc = Loadout.ContextFor("Extermination", 1, new Doctrine(), Knowledge.FromJson(Knowledge.DefaultJson));
            var off = new List<string>();
            foreach (var t in new[] { 0.0, 300, 600, 1199, 1300 })
            {
                var x = new RunContext { Mode = lc.Mode, Difficulty = lc.Difficulty, Goal = lc.Goal, Seconds = t, D = lc.D };
                if (x.Progress != OldProgress(x) || Math.Abs(x.Remaining - OldRemaining(x)) > 1e-9) off.Add(F0(t));
            }
            Check("XH3", "the SELECT LOADOUT shape (knowledge goal " + F0(lc.Goal) + " s, no waves) as in 0.16.0: the same progress and time left at every sample (its curves read the progress)", off.Count == 0, string.Join(", ", off));
        }

        // ================================================================ XH4 ModeAware off
        static void AwareOff()
        {
            var d = new Doctrine { ModeAware = false };
            var c = Ext(3, 62, 3.9, 55, null, d); var on = Ext(3, 62, 3.9);
            Check("XH4", "[Advice] ModeAware off: wave 3/20 at 01:02 reads progress " + F(c.Progress) + ", " + F0(c.Remaining) + " s left, recruit value " + F(c.RecruitValue)
                + " - as with it on; 0.16.0 read progress " + F(OldProgress(c)) + " and " + F0(OldRemaining(c)) + " s (the game's goal minus the clock)",
                c.Progress == on.Progress && c.Remaining == on.Remaining && c.RecruitValue == 1 && OldProgress(c) == 1 && OldRemaining(c) == 0);
            Check("XH4", "... the mode's own steering stays off (boss x" + F(c.Boss) + ", crowd control x" + F(c.Control) + ")", c.Boss == 1 && c.Control == 1);
        }

        // ================================================================ XH5 every other mode as it was
        static void OtherModes()
        {
            var modes = new[] { Tuple.Create("Normal", 1200.0), Tuple.Create("Normal", 0.0), Tuple.Create("Hardcore", 600.0), Tuple.Create("OneHit", 300.0), Tuple.Create("BossRush", 600.0),
                Tuple.Create("Endless", 0.0), Tuple.Create("Infinite", 0.0), Tuple.Create("SomeNewMode", 900.0) };
            int n = 0; var diff = new List<string>();
            foreach (var m in modes)
                foreach (double t in new[] { 0, 45, 300, 599.5, 600, 900, 1199, 1250 })
                    foreach (bool aware in new[] { true, false })
                        foreach (int timing in new[] { 0, 1, 2 })
                            foreach (int recruit in new[] { 0, 1, 2 })
                                foreach (var pace in new[] { Tuple.Create(0.0, 0), Tuple.Create(2.6, 9) })
                                {
                                    var d = new Doctrine { ModeAware = aware, Timing = timing, Recruit = recruit };
                                    var c = new RunContext { Mode = m.Item1, Goal = m.Item2, Seconds = t, LevelRate = pace.Item1, LevelUps = pace.Item2, D = d };
                                    n++;
                                    double reach = timing == 0 ? 1 : Clamp01(0.35 * OldLevelUps(c) / 3); if (timing == 2) reach *= reach;
                                    string s = c.ToString();
                                    if (c.Progress != OldProgress(c) || c.Remaining != OldRemaining(c) || c.ClockText != OldClock(c) || c.ExpectedLevelUps != OldLevelUps(c)
                                        || c.RecruitValue != OldRecruitValue(c) || c.Reach(3) != reach || c.WaveSeconds != 0 || s.Contains(" a wave") || s.Contains("game goal"))
                                        if (diff.Count < 6) diff.Add(m.Item1 + " goal " + m.Item2 + " at " + t + (aware ? "" : " unaware") + " timing " + timing + " recruit " + recruit + ": " + s);
                                }
            Check("XH5", "every other mode as 0.16.0 read it - " + n + " contexts (Normal 20:00 and with no goal read, Hardcore, One Hit, Boss Rush, Endless, Infinite, an unknown timed mode; ModeAware, Timing, "
                + "Recruit, pace): the same progress, time left, clock words, '~N level-ups', recruit value and reach, and a [ctx] line without the waves part", diff.Count == 0, string.Join(" | ", diff));
        }

        // ================================================================ XH6 the [ctx] line
        static void CtxLine()
        {
            var c = Ext(3, 62, 3.9); c.SurviveQuest = true;
            string line = c.ToString();
            string notRead = Ext(0, 300, 3.9, 55, null, null, 0).ToString();
            string normal = new RunContext { Mode = "Normal", Goal = 1200, Seconds = 392, LevelRate = 2.6, LevelUps = 15, D = new Doctrine() }.ToString();
            Check("XH6", "Extermination's [ctx] shows the wave length used and the game's goal, ahead of the health tail: '" + line + "'",
                line.StartsWith("Extermination d1, wave 3/20, early (", StringComparison.Ordinal) && line.EndsWith(" level-ups to come, ~38 s a wave, game goal 55 s, survive quest)", StringComparison.Ordinal));
            Check("XH6", "... with the waves not read it says so: '" + notRead + "'",
                notRead.StartsWith("Extermination d1, 15:00 left, early (", StringComparison.Ordinal) && notRead.EndsWith(" level-ups to come, waves not read: knowledge goal 1200 s, game goal 55 s)", StringComparison.Ordinal));
            Check("XH6", "... and a Normal line ends as 0.16.0 wrote it: '" + normal + "'", normal.EndsWith(" level-ups to come)", StringComparison.Ordinal) && !normal.Contains("game goal"));
            var g = Ext(3, 62, 3.9, 47.26);
            Check("XH6", "the game's goal to a tenth, in the invariant culture: '" + g.ToString().Split(new[] { ", game goal " }, StringSplitOptions.None)[1] + "'", g.ToString().Contains(", game goal 47.3 s"));
        }

        // ================================================================ XH7 the pace prior
        static void Pace()
        {
            var start = Ext(1, 0, 0); start.LevelUps = 0;
            Check("XH7", "Extermination's level-up pace prior 3.9 a minute (one measured run; it borrowed Normal's 2.9); the other modes' unchanged; at 00:00 of 20 waves ~" + F0(start.ExpectedLevelUps) + " to come",
                LevelPace.Prior("Extermination") == 3.9 && LevelPace.Prior("Normal") == 2.9 && LevelPace.Prior("Hardcore") == 4.7 && LevelPace.Prior("Endless") == 3.2 && LevelPace.Prior("BossRush") == 4.6
                && LevelPace.Prior("OneHit") == 2.9 && LevelPace.Prior("Infinite") == 2.9 && Math.Abs(start.ExpectedLevelUps - 3.9 * 20 * 42 / 60.0) < 1e-9);
        }

        // ================================================================ XH8 the sources
        static void Sources()
        {
            string ctx = Src("Context.cs"), state = Src("GameState.cs"), ranker = Src("Ranker.cs");
            Check("XH8", "sources: Remaining asks the waves before any goal, no '(1 - Progress) * goal' left; GameState still reads TimeRequiredForSuccess and the waves",
                ctx != null && ctx.Contains("if (Kind == ModeKind.Waves) return WavesLeft;") && ctx.IndexOf("if (Kind == ModeKind.Waves) return WavesLeft;", StringComparison.Ordinal) < ctx.IndexOf("double goal = Goal > 0 ? Goal : 1200.0;", StringComparison.Ordinal)
                && !ctx.Contains("(1 - Progress) * goal)") && state != null && state.Contains("c.Goal = gm.TimeRequiredForSuccess;") && state.Contains("c.Wave = ex.CompletedWaveCount() + 1;")
                && state.Contains("c.Waves = def.waveDefinitions.Count;"));
            Check("XH8", "sources: the SOS scores this section rebuilds are Ranker's - 2.0 fixed, the rescue tier 1.6 / 1.1 / 0.6 / 0.1, 1.1 a bought node, fixedPart x value + fit x (0.35 + 0.65 x value), Liberate by HeldRules.LiberateScore of the recruit value",
                ranker != null && ranker.Contains("double fixedPart = 2.0, fit = 0;") && ranker.Contains("fit += Knowledge.Tier(tier, 1.6, 1.1, 0.6, 0.1);") && ranker.Contains("fit += 1.1 * owned;")
                && ranker.Contains("fixedPart * left + fit * (0.35 + 0.65 * left)") && ranker.Contains("double left = s.Ctx.RecruitValue;") && ranker.Contains("c.Score = HeldRules.LiberateScore(left, farming, s.SquadFull, libUps);")
                && ranker.Contains("bool late = left < 0.45 && joinUps < 1;"));
        }

        // ================================================================ BO1 one 'Boosted by'
        static void OneBoost()
        {
            // a recruit's team passive (SWAT's, on the squad) boosts the leader's grenade ability: the head and the tag facts both say it
            var facts = new PowerFacts { Name = "Grenade Trail", IsAbility = true }.Deals("Explosive").Tagged("Grenade");
            var boosts = new List<TeamBoost> { new TeamBoost { Name = "Grenade Expertise", Owner = "SWAT", Tag = "Grenade" } };
            var tags = new TagProfile { SpecialAt = 10 }; tags.Source("Handgun", 1.0, new[] { "Kinetic" }); tags.Points["Kinetic"] = 4;
            var ctx = new RunContext { Mode = "Normal", Goal = 1200, Seconds = 300, LevelRate = 3, D = new Doctrine() };
            var why = new List<string>();
            double tv = Synergy.TagValue(facts, tags, ctx, why), bv = Synergy.BoostValue(facts, boosts, ctx, why);
            var head = Synergy.AbilityHeads(facts, tags, boosts, tv, bv, why).OrderByDescending(h => h.Rank).FirstOrDefault();      // AbilityVerdict.Mark: the higher rank wins
            Func<bool, CardWords> card = withHead =>
            {
                var w = new CardWords { Kind = SayKind.Ability, Level = 1, Max = 4, Build = "Bombardier", Priority = 3, HeadRank = withHead && head != null ? head.Rank : 0, Head = withHead && head != null ? head.Say : null,
                    EvoExists = true, EvoOwned = true, Reach = 1, Clock = "15:00 left", Owned = 3 };
                Wording.Tags(w, facts, tags, boosts, ctx);
                return w;
            };
            Func<CardWords, List<string>> boosted = w => WhyText.Reasons(w, why, Wording.Card(w, 1, null, null)).Where(x => x.StartsWith("Boosted by", StringComparison.Ordinal)).ToList();
            var a = card(true); var withHead = boosted(a);
            var b = card(false); var noHead = boosted(b);
            var was = card(true); was.BoostOwner = null; var before = boosted(was);         // 0.16.0's tag facts: no owner
            Check("BO1", "a recruit-owned team passive on an ability card: the WHY says it once - '" + string.Join("' | '", withHead) + "' (line '" + Wording.Card(a, 1, null, null) + "'); 0.16.0 said '"
                + string.Join("' | '", before) + "'",
                head != null && head.Rank == 3 && a.BoostOwner == "SWAT" && withHead.Count == 1 && withHead[0] == "Boosted by Grenade Expertise (SWAT)" && before.Count == 2);
            Check("BO1", "... without that head the tag facts alone say it, with the owner: '" + string.Join("' | '", noHead) + "'", noHead.Count == 1 && noHead[0] == "Boosted by Grenade Expertise (SWAT)");
            // the review of the PC round 10-11 fix: an evolution of that ability carries no head, and its own line can be the boost
            // ('Evolution - boosted by X', no owner for room) - with the owner in the tag facts the WHY repeated it; 0.16.0 said nothing
            var evoFacts = new PowerFacts { Name = "Grenade Trail: Shrapnel", IsAbility = true }.Deals("Explosive").Tagged("Grenade");
            Func<string, int, CardWords> evo = (build, priority) =>
            {
                var w = new CardWords { Kind = SayKind.Evolution, Base = "Grenade Trail", Build = build, BasePriority = priority };
                Wording.Tags(w, evoFacts, tags, boosts, ctx, facts);
                return w;
            };
            var e = evo(null, -1); string eLine = Wording.Card(e, 1, null, null);
            var eWhy = WhyText.Reasons(e, new List<string>(), eLine); var eBoost = eWhy.Where(x => x.StartsWith("Boosted by", StringComparison.Ordinal)).ToList();
            string eAlt = WhyText.Alternative(e, new List<string>(), eLine, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { eLine }, Wording.Budget, null);
            Check("BO1", "an evolution whose own line is the boost - '" + eLine + "': its WHY says no 'Boosted by' (" + eWhy.Count + " reasons), and a second card drawing that line has no near-copy to say instead (" + (eAlt ?? "none") + "), as in 0.16.0",
                e.BoostOwner == "SWAT" && e.Head == null && eLine == "Evolution - boosted by Grenade Expertise" && eBoost.Count == 0 && eAlt == null);
            var r = evo("Bombardier", 0); string rLine = Wording.Card(r, 1, null, null);
            var rBoost = WhyText.Reasons(r, new List<string>(), rLine).Where(x => x.StartsWith("Boosted by", StringComparison.Ordinal)).ToList();
            Check("BO1", "... one whose line says something else ('" + rLine + "'): the WHY says it once, in 0.16.0's words - '" + string.Join("' | '", rBoost) + "'",
                rLine == "Evolves the Bombardier build's main ability" && rBoost.Count == 1 && rBoost[0] == "Boosted by Grenade Expertise");
            string whySrc = Src("WhyText.cs"), words = Src("Wording.cs");
            Check("BO1", "sources: TagFacts words it with the owner, an evolution's without; Wording.Tags carries it; the evolution card's own line keeps the short form (its room)",
                whySrc != null && whySrc.Contains("raw.Add(First(Wording.Boosted(w.Boost, w.Kind == SayKind.Evolution ? null : w.BoostOwner)));") && !whySrc.Contains("Wording.Boosted(w.Boost, null)")
                && words != null && words.Contains("w.Boost = b.Name; w.BoostOwner = b.Owner; break;") && words.Contains("if (w.Boost != null) return Say(lead, Boosted(w.Boost, null));"));
        }

        // ================================================================ 0.16.3
        //   LA1 - LA2  C162-01 (COMPANION-LOG-05 of the 10-11 round): the action hint's line placed once. Placed the frame after the cards, it
        //        moved 0.58 - 0.60 s later on 5 of 5 first draws (band 85 -> 111 units); now an offer's first line waits for SettleAfter
        //        (ScreenBand.LineAt) and is placed where the screen settled; the gold frame still comes with the cards.
        //   CI1 - CI2  CO-INFO (the 10-12 PC session's three marks): another mod's one-choice option is a row of information - no arrows, no
        //        set() from left / right / select / a click (43 '[ext] ... = ...' lines in under three minutes on two status rows), its footer
        //        not typed on again when the body is rebuilt under the focused row.
        //   CG1 - CG4  CO-GLYPH: another mod's words as the menu font can draw them - a backslash drawn as a box in a footer on 10-12.
        public static int RunPure0163()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.16.3: the hint's line placed once (C162-01), one-choice rows as information (CO-INFO), the menu font's characters (CO-GLYPH) - PcRoundCases.cs");
            LineOnce();
            InfoRows();
            Glyphs();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        static string S2(float v) { return v.ToString("0.00", IC); }

        // ---------------------------------------------------------------- LA: the line placed once
        static void LineOnce()
        {
            const float Settle = 0.6f, Offer = 100f;
            float first = ScreenBand.LineAt(Offer + 0.016f, Offer, false, Settle), early = ScreenBand.LineAt(Offer + 0.3f, Offer, false, Settle);
            float late = ScreenBand.LineAt(Offer + 3f, Offer, false, Settle), words = ScreenBand.LineAt(Offer + 0.3f, Offer, true, Settle), none = ScreenBand.LineAt(5f, -1f, false, Settle);
            Check("LA1", "ScreenBand.LineAt: an offer's first line at the settle (" + S2(first - Offer) + " s after the cards; a re-judge 0.30 s in keeps it: " + S2(early - Offer)
                + "), a verdict after the settle at once (" + S2(late - Offer) + "), new words on a shown line 0.05 s on (" + S2(words - Offer) + "), no offer clock: now (" + S2(none) + ")",
                Math.Abs(first - (Offer + Settle)) < 1e-4 && Math.Abs(early - (Offer + Settle)) < 1e-4 && Math.Abs(late - (Offer + 3f)) < 1e-4
                && Math.Abs(words - (Offer + 0.35f)) < 1e-4 && Math.Abs(none - 5f) < 1e-4);
            // COMPANION-LOG-05 replayed on a made-up screen: the band over the button as the screen flies in (85 units on the cards' frame,
            // 111 once it has flown in at 0.58 s). 0.16.2 placed the line on the next frame and again at the settle; 0.16.3 once, at the settle
            Func<float, float> band = t => t < 0.58f ? 85f : 111f;
            var then = new List<float> { band(0.016f) }; if (band(Settle) != then[0]) then.Add(band(Settle));
            var now = new List<float> { band(ScreenBand.LineAt(Offer + 0.016f, Offer, false, Settle) - Offer) };
            Check("LA1", "COMPANION-LOG-05 replayed: 0.16.2 placed the line " + then.Count + " times (band " + string.Join(" -> ", then.Select(b => F0(b))) + " units), 0.16.3 "
                + now.Count + " time (band " + F0(now[0]) + " units, the settled one)", then.Count == 2 && now.Count == 1 && now[0] == 111f);
            string hint = Src("RerollHint.cs"); string apply = Body(hint, "static void Apply(ScreenCall call)"), tick = Body(hint, "public static void Tick()"), place = Body(hint, "static void Place()");
            Check("LA2", "sources: Apply draws the frame first (FrameNow) and times the line by ScreenBand.LineAt with SettleAfter; Tick places it once (no second measure, no '_settleAt'); "
                + "no 'as the screen settled' line is left; the drawn line says how long after the cards ('..., 0.60 s after the cards')",
                hint != null && apply.Contains("FrameNow();") && apply.Contains("_placeAt = ScreenBand.LineAt(Time.realtimeSinceStartup, _offerAt, _shown, SettleAfter); _placeFrame = Time.frameCount;")
                && apply.IndexOf("FrameNow();", StringComparison.Ordinal) < apply.IndexOf("ScreenBand.LineAt(", StringComparison.Ordinal)
                && tick.Contains("{ _placeAt = -1f; Place(); }") && !hint.Contains("_settleAt") && !hint.Contains("Place(true)") && !hint.Contains("\"the line \" + (appear")
                && !hint.Contains("as the screen settled - ") && place.Contains("Plugin.Logger.LogInfo(Tag + \"drawn \" + what + After() + Pv());")
                && hint.Contains("Time.realtimeSinceStartup - _offerAt).ToString(\"0.00\", CultureInfo.InvariantCulture) + \" s after the cards\""));
        }

        static string Body(string src, string head)
        {
            if (src == null) return "";
            int at = src.IndexOf(head, StringComparison.Ordinal); if (at < 0) return "";
            int end = src.IndexOf("\n        }", at, StringComparison.Ordinal);
            return end < 0 ? src.Substring(at) : src.Substring(at, end - at);
        }

        // ---------------------------------------------------------------- CI: one-choice rows are information
        static void InfoRows()
        {
            string menu = Src("Menu.cs"), api = Src("Api/Extensions.cs");
            string mods = Body(menu, "static void BuildMods()"), info = Body(menu, "static void Info(Ctl c)"), isInfo = Body(api, "internal static bool IsInfo(Option o)");
            string mouse = Body(menu, "static bool Mouse()"), activate = Body(menu, "static void Activate(Ctl c)"), input = Body(menu, "static void ReadInput()");
            Check("CI1", "sources: a MODS row whose option has exactly one choice (Api.Extensions.IsInfo, said once per option) loses its arrows and its Change - and left / right, select, "
                + "a click and a touch reach set() only through Change (ReadInput, Activate, Mouse), so nothing on it calls Step; a row that gains or loses its second choice is built again; 2+ choices unchanged",
                isInfo.Contains("if (ChoicesOf(o).Length != 1) return false;") && isInfo.Contains(": one choice - shown as information (no arrows; left / right and select change nothing)\", false);")
                && mods.Contains("bool info = Api.Extensions.IsInfo(o);") && mods.Contains("if (info) Info(c);") && mods.Contains("SettingSaved(Api.Extensions.Step(o, d), o.Owner + \" did not take the change\");")
                && mods.Contains("if ((Api.Extensions.ChoicesOf(o).Length == 1) != info) _dirty = true; else show();")
                && info.Contains("a.gameObject.SetActive(false);") && info.Contains("c.Left = null; c.Right = null; c.Change = null;")
                && input.Contains("else if (_focus != null && _focus.Change != null) { _focus.Change(x); Click(); }") && activate.Contains("else if (c.Change != null) { c.Change(1); Click(); }")
                && mouse.Contains("if (over.Change != null && over.Left != null && Hit(over.Left, p))") && mouse.Contains("else if (over.Change != null && over.Right != null && Hit(over.Right, p))")
                && menu.Split(new[] { "Api.Extensions.Step(" }, StringSplitOptions.None).Length == 2);
            string focus = Body(menu, "static void Focus(Ctl c, bool byMouse)"), setDesc = Body(menu, "static void SetDesc(string text, string key)");
            // the guard, replayed: the footer types on again only for other words or another control
            string shownText = "", shownKey = null; int typed = 0;
            Action<string, string> set = (text, key) => { if (key != null && key == shownKey && shownText == text) return; shownKey = key; shownText = text; typed++; };
            set("Build 0.1.0 ...", "mod:A/Updates/Build"); set("Build 0.1.0 ...", "mod:A/Updates/Build"); set("Build 0.1.0 ...", "mod:A/Updates/Build");     // focused, then two rebuilds
            int afterRebuilds = typed; set("Build 0.1.1 ...", "mod:A/Updates/Build"); set("This session ...", "mod:A/Updates/This session"); set("This session ...", null);
            Check("CI2", "sources: Focus hands SetDesc its control's key, and SetDesc leaves a footer as it is when the same control is focused again with the same words (a rebuild under it); "
                + "replayed: 3 focuses of one row typed it " + afterRebuilds + " time, new words, another row and a press typed it on again (" + typed + " in all)",
                focus.Contains("SetDesc(c.Desc != null ? c.Desc() : \"\", c.Key);") && setDesc.Contains("if (key != null && key == _descKey && _desc.text == text) return;")
                && setDesc.Contains("_descKey = key;") && menu.Contains("static void SetDesc(string text) { SetDesc(text, null); }") && afterRebuilds == 1 && typed == 4);
        }

        // ---------------------------------------------------------------- CG: the menu font's characters
        static void Glyphs()
        {
            // a font with no backslash, as the frames of 10-12 showed it (a box between a folder and a file name in another mod's footer)
            Func<char, bool> noBackslash = c => c != '\\';
            var miss = new Dictionary<string, string>();
            string footer = "Off puts the release back (tools\\switch.ps1 -Stable).";
            string drawn = Wording.Drawable(footer, noBackslash, miss);
            Check("CG1", "a footer with a path, as on 10-12, with a font that lacks the backslash: '" + drawn + "' - " + string.Join(", ", miss.Select(m => "'" + m.Key + "' as '" + m.Value + "'")),
                drawn == "Off puts the release back (tools/switch.ps1 -Stable)." && miss.Count == 1 && miss["\\"] == "/");
            // a font with ASCII only: the stand-ins; a character with none, and a stand-in the font lacks too, are left out
            Func<char, bool> ascii = c => c < 128;
            miss.Clear();
            string quotes = Wording.Drawable("“Fast” – it’s 3× … ★", ascii, miss);
            var noDash = new Dictionary<string, string>();
            string dash = Wording.Drawable("a — b", c => c < 128 && c != '-', noDash);
            Check("CG2", "ASCII only: '" + quotes + "' (" + miss.Count + " characters: " + string.Join(", ", miss.Select(m => m.Value.Length > 0 ? "as '" + m.Value + "'" : "left out")) + "); a long dash where '-' is missing too: '" + dash + "'",
                quotes == "\"Fast\" - it's 3x ... " && miss.Count == 7 && miss["★"] == "" && miss["…"] == "..." && dash == "a  b" && noDash["—"] == "");
            // markup, spaces, line breaks never asked; an emoji (a surrogate pair) is one character; nothing to change: the very same string
            var asked = new HashSet<char>(); miss.Clear();
            Func<char, bool> spy = c => { asked.Add(c); return c != 'Z'; };
            string marked = Wording.Drawable("<color=#FFD700>A</color> B\nZ 😀", spy, miss);
            string same = "Build 0.1.0 (built 10-12 03:10)"; var none = new Dictionary<string, string>();
            Check("CG3", "markup kept whole and never asked ('" + marked.Replace("\n", "|") + "', asked: " + new string(asked.OrderBy(c => c).ToArray()) + "), a surrogate pair one character left out; a text the font draws whole comes back as the same string",
                marked == "<color=#FFD700>A</color> B\n " && !asked.Contains('<') && !asked.Contains('#') && !asked.Contains(' ') && !asked.Contains('\n') && asked.Contains('Z')
                && miss.Count == 2 && miss["Z"] == "" && miss["😀"] == "" && ReferenceEquals(Wording.Drawable(same, noBackslash, none), same) && none.Count == 0);
            string menu = Src("Menu.cs"); string mods = Body(menu, "static void BuildMods()"), has = Body(menu, "static bool HasGlyph(TMP_FontAsset font, char ch)"), say = Body(menu, "static void SayGlyphs(Api.Extensions.Option o)");
            Check("CG4", "sources: the MODS tab draws every word of another mod through Drawn (owner, group, label, value, footer); the font is asked once per character (HasGlyph: TMP's HasCharacter with "
                + "its fallbacks, kept until the font changes); '[ext] '<owner>': N character(s) the menu font cannot draw (...)' once per owner and character",
                mods.Contains("Drawn(o, owner.ToUpperInvariant())") && mods.Contains("Drawn(o, group.ToUpperInvariant())") && mods.Contains("Drawn(o, o.Label)") && mods.Contains("() => Drawn(o, Api.Extensions.ValueOf(o))")
                && mods.Contains("() => Drawn(o, o.Description.Length > 0 ? o.Description : \"An option of \" + o.Owner + \".\")")
                && has.Contains("if (_glyphs.TryGetValue(ch, out ok)) return ok;") && has.Contains("ok = font.HasCharacter(ch, true, true);") && menu.Contains("if (p != _glyphFont) { _glyphFont = p; _glyphs.Clear(); }")
                && say.Contains("if (said.Add(m.Key))") && say.Contains("\" character(s) the menu font cannot draw (\""));
        }
    }
}
