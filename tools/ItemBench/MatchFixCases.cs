// 0.13.0: the review of the user's 2026-10-04 match (two runs on 1.0.2), replayed offline (section 9 of the bench):
//   C1  the active quest's team rules (QuestTeam / QuestSos, RerollCall's quest argument): run 1's quest wanted the leader
//       alone ("Team Size" = survivors == 1); the Companion ranked a survivor first on six of seven rescue screens and showed
//       the REROLL HINT three times - the user took Liberate seven times. The logged screens are replayed under the quest,
//       with the quest that needs a class (Ghost + Huntress, == 2) and the full-team objectives besides.
//   C2  the weapon in hand under "abilities first" (WeaponLift, Synergy.HeldWeapon): the leader's weapon sat on the 3.3 floor
//       under every recruit's ability level all run; the user took it against the advice nine times. The logged hands of run 2
//       are replayed with the lift and the cap under the survivor's own open build ability.
//   C3  each headline said once (Synergy.AbilityHeads / ReasonsUnder / Restates): 30 cards repeated a boost or a special
//       headline as a reason ("Trap Expertise boosts it; ...; Trap Expertise (Huntress) boosts it: taunt").
//   C5  the EQUIP ADVICE key has no default any more (F9 was another plugin's key on the screen before).
// Generic names only (the repository is public): the logged lent build is "Bench Anchor" here.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class MatchFixes
    {
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string F(double v) { return v.ToString("0.00"); }

        public static int Run(Func<string, PowerFacts> find, List<ProbeItem> items)
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.13.0: the 10-04 match - C1 the quest's team rules, C2 the weapon under \"abilities first\", C3 each headline once, C5 the equip key");
            QuestRules();
            QuestScreens();
            QuestNeeds();
            QuestFullTeam();
            WeaponHands();
            Headlines(find);
            OtherHeads(items);
            EquipKey();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        // ---------------------------------------------------------------- C1
        static readonly string[] Classes = { "SWAT", "Tank", "Engineer", "Huntress", "Ghost", "Medic", "Pyro", "Mechanic", "Ranger" };      // the game's class order
        static Recruit R(string name, double score) { return new Recruit { Name = name, Class = Array.IndexOf(Classes, name), Score = score }; }

        sealed class SosCard { public string Name; public double Score; public List<string> Why = new List<string>(); public bool Liberate; }

        // a rescue screen as the logged [card] lines had it, under the quest: the cards' scores and reasons, best first
        static List<SosCard> Screen(QuestSos q, params Tuple<string, double>[] cards)
        {
            var list = new List<SosCard>();
            foreach (var c in cards)
            {
                var sc = new SosCard { Name = c.Item1, Liberate = c.Item1 == "Liberate" };
                sc.Why.Add(sc.Liberate ? "level-up and cash instead of a recruit" : "(the logged reasons)");
                sc.Score = Math.Round(q.Card(sc.Liberate ? null : sc.Name, c.Item2, sc.Why), 2);
                list.Add(sc);
            }
            return list.Select((c, i) => Tuple.Create(c, i)).OrderByDescending(t => t.Item1.Score).ThenBy(t => t.Item2).Select(t => t.Item1).ToList();
        }
        static string Text(List<SosCard> ranked) { return string.Join(", ", ranked.Select(c => c.Name + " " + F(c.Score))); }
        static Tuple<string, double> C(string name, double score) { return Tuple.Create(name, score); }

        static void QuestRules()
        {
            Func<int, double, string> mm = (cmp, thr) => { var t = new QuestTeam(); t.Survivors(cmp, thr); return t.Min + ".." + t.Max + " " + t.Words; };
            Check("C1", "InternalNumSurvivors thresholds -> the team size (the leader counts)",
                mm(2, 1) == "1..1 stay solo" && mm(0, 2) == "1..1 stay solo" && mm(1, 1) == "1..1 stay solo" && mm(2, 2) == "2..2 team of 2" && mm(3, 3) == "3..3 full team" && mm(4, 1) == "2..3 team of 2 or more" && mm(1, 2) == "1..2 at most 2",
                "== 1: " + mm(2, 1) + " | < 2: " + mm(0, 2) + " | <= 1: " + mm(1, 1) + " | == 2: " + mm(2, 2) + " | >= 3: " + mm(3, 3) + " | > 1: " + mm(4, 1) + " | <= 2: " + mm(1, 2));
            var t1 = new QuestTeam(); string said = t1.Survivors(2, 1.0);
            Check("C1", "the log words of run 1's 'Team Size'", said == "survivors == 1 (the leader counts)", said);
            var none = new QuestTeam { Quest = "a quest without a team objective (run 2's: kills, weapons, survive)" };
            var v = none.Judge(new[] { "Engineer" });
            Check("C1", "no team objective: no rule, the advice unchanged", !none.Rules && !v.Active && v.Card("Tank", 4.2, new List<string>()) == 4.2 && v.Card(null, 1.0, new List<string>()) == 1.0);
            var any = new QuestTeam { AnyOf = true }; any.Survivors(2, 1.0);
            var failed = new QuestTeam { Failed = true }; failed.Survivors(2, 1.0);
            Check("C1", "objectives matched by Any, or a quest the game counts as failed: the advice unchanged",
                !any.Judge(new[] { "Huntress" }).Active && !failed.Judge(new[] { "Huntress" }).Active, any.Judge(new[] { "Huntress" }).Advice() + " | " + failed.Judge(new[] { "Huntress" }).Advice());
            var offRun = new QuestTeam { NotThisRun = "the leader (Engineer) does not fit the quest's leader condition - the quest cannot complete this run" }; offRun.Survivors(2, 1.0);
            var offV = offRun.Judge(new[] { "Engineer" });
            Check("C1", "a leader the quest's leader condition refuses: the rule is not followed (the quest cannot complete that run)", offRun.HasRule && !offRun.Rules && !offV.Active && offV.Broken == offRun.NotThisRun
                && offV.Card("Tank", 4.2, new List<string>()) == 4.2 && offV.Card(null, 1.0, new List<string>()) == 1.0, offV.Advice());
            // the game's whole test (AreFulfillmentConditionsMet: arena, mode, difficulty, leader) - here the class rule of the Ghost's
            // quest in a run of another mode with the right leader: no Huntress push, the usual reroll hint
            var offMode = new QuestTeam { NotThisRun = "this run does not fit the quest's conditions (arena, mode, difficulty or leader; leader Ghost fits) - the quest cannot complete this run" };
            offMode.Need("Ghost"); offMode.Need("Huntress"); offMode.Survivors(2, 2.0);
            var omV = offMode.Judge(new[] { "Ghost" });
            var omCall = RerollCall.Decide(new[] { R("Tank", 4.00) }, 1.0, new[] { R("SWAT", 5.20), R("Huntress", 3.10) }, true, "3", 1.0, omV);
            Check("C1", "a run that does not fit the quest's arena / mode / difficulty: the rule is not followed, the usual reroll hint", !omV.Active && omV.Broken == offMode.NotThisRun
                && omV.Card("Tank", 4.0, new List<string>()) == 4.0 && omV.Card("Huntress", 3.1, new List<string>()) == 3.1 && omV.Card(null, 1.0, new List<string>()) == 1.0
                && omCall.Show && !omCall.ForQuest && omCall.Better.Count == 1 && omCall.Better[0].Name == "SWAT", omV.Advice() + " | hint: " + omCall.Why);
            var solo = new QuestTeam(); solo.Survivors(2, 1.0);
            var broken = solo.Judge(new[] { "Huntress", "Tank" });
            Check("C1", "a team that broke the rule already (recruited anyway): the advice as usual, the log says why", !broken.Active && broken.Broken == "the team is 2 already, the quest wants 1"
                && broken.Card("Pyro", 3.8, new List<string>()) == 3.8, broken.Advice());
        }

        // run 1 (Huntress alone, quest 'Readjust': Survive 20:00 + survivors == 1): the seven logged rescue screens and the three hints
        static void QuestScreens()
        {
            var team = new QuestTeam { Quest = "GameHubQuest_Huntress_4" }; team.Survivors(2, 1.0);
            var q = team.Judge(new[] { "Huntress" });
            Check("C1", "run 1's quest on a solo Huntress: capped, Liberate first", q.Active && q.Capped && !q.WantsMore && q.Missing.Count == 0, q.Advice());
            var screens = new[]
            {
                Tuple.Create("01:39", new[] { C("Ranger", 4.34), C("Pyro", 4.11), C("Liberate", 1.0) }),
                Tuple.Create("04:44", new[] { C("SWAT", 5.40), C("Medic", 4.48), C("Liberate", 1.0) }),
                Tuple.Create("07:30", new[] { C("Pyro", 3.80), C("Ghost", 3.31), C("Liberate", 1.0) }),
                Tuple.Create("10:20", new[] { C("Tank", 6.01), C("SWAT", 5.21), C("Liberate", 1.0) }),
                Tuple.Create("13:21", new[] { C("Engineer", 5.90), C("SWAT", 5.21), C("Liberate", 1.0) }),
                Tuple.Create("16:01", new[] { C("Engineer", 4.37), C("Medic", 2.89), C("Liberate", 2.08) }),
                Tuple.Create("18:47", new[] { C("Liberate", 3.56), C("Tank", 2.33), C("Pyro", 1.18) }),
            };
            int agree = 0, before = 0;
            foreach (var sc in screens)
            {
                var ranked = Screen(q, sc.Item2);
                bool libFirst = ranked[0].Liberate;
                if (libFirst) agree++;
                if (sc.Item2.OrderByDescending(x => x.Item2).First().Item1 == "Liberate") before++;
                var rec = ranked.First(c => !c.Liberate);
                Check("C1", "SOS " + sc.Item1 + ": Liberate first (the user's pick)", libFirst && ranked[0].Why[0] == "quest: stay solo - take the level-up and cash" && rec.Why[0] == "quest: stay solo - " + rec.Name + " would fail it" && rec.Score <= QuestSos.RecruitFails,
                    Text(ranked) + " | Liberate: " + ranked[0].Why[0] + " | " + rec.Name + ": " + rec.Why[0]);
            }
            Check("C1", "the user's picks (Liberate 7 of 7) and the advice agree", agree == 7, "now " + agree + " of 7 (0.13.0 before the fix: " + before + " of 7)");
            // the three REROLL HINTs of run 1 (16:08:39 / 16:12:36 / 16:16:25), with the logged 'could still come' lists
            var hints = new[]
            {
                Tuple.Create("01:39", new[] { R("Ranger", 4.34), R("Pyro", 4.11) }, new[] { R("Tank", 6.50), R("SWAT", 5.40), R("Engineer", 5.22), R("Medic", 4.48), R("Mechanic", 3.97), R("Ghost", 2.98) }),
                Tuple.Create("04:44", new[] { R("SWAT", 5.40), R("Medic", 4.48) }, new[] { R("Tank", 6.50), R("Engineer", 5.83), R("Ranger", 4.34), R("Pyro", 4.11), R("Mechanic", 3.97), R("Ghost", 3.60) }),
                Tuple.Create("07:30", new[] { R("Pyro", 3.80), R("Ghost", 3.31) }, new[] { R("Tank", 6.19), R("Engineer", 5.94), R("SWAT", 5.39), R("Medic", 4.17), R("Ranger", 4.03), R("Mechanic", 3.66) }),
            };
            foreach (var h in hints)
            {
                var was = RerollCall.Decide(h.Item2, 1.0, h.Item3, true, "8", 1.0);
                var now = RerollCall.Decide(h.Item2, 1.0, h.Item3, true, "8", 1.0, q);
                Check("C1", "reroll hint at " + h.Item1 + ": not shown under the quest", was.Show && !now.Show && now.Why == "quest: stay solo - no recruit wanted (Liberate)", "was SHOWN (" + was.Why + "), now: " + now.Why);
            }
            bool replace; string row = q.PlanText(false, null, out replace);
            Check("C1", "the PLAN readout's SOS row (it named two recruits all run)", row == "quest: stay solo" && replace, "SOS  " + row);
            row = q.PlanText(true, null, out replace);
            Check("C1", "late in the run as well (it read 'SOS  Liberate · 0:39 left')", row == "quest: stay solo" && replace, "SOS  " + row);
        }

        // the quest that wants exactly Ghost + Huntress (a FinishSpecificTeamSetup, and survivors == 2)
        static void QuestNeeds()
        {
            var team = new QuestTeam { Quest = "GameHubQuest_Ghost_3" }; team.Need("Ghost"); team.Need("Huntress"); team.Survivors(2, 2.0);
            Check("C1", "Ghost + Huntress, == 2: the rule", team.Rules && team.Words == "Ghost + Huntress" && team.Min == 2 && team.Max == 2, team.Words + ", " + team.Min + ".." + team.Max);
            var q = team.Judge(new[] { "Ghost" });
            var ranked = Screen(q, C("Huntress", 4.00), C("Tank", 5.50), C("Liberate", 1.0));
            Check("C1", "a Ghost leader, Huntress on the cards: Huntress first, Liberate keeps the slot, Tank last",
                string.Join(",", ranked.Select(c => c.Name)) == "Huntress,Liberate,Tank" && ranked[0].Why[0] == "quest: needs Huntress" && ranked[2].Why[0] == "quest: Ghost + Huntress - Tank would fail it", Text(ranked) + " | " + ranked[0].Why[0] + " | " + ranked[1].Why[0]);
            var call = RerollCall.Decide(new[] { R("Huntress", 4.00), R("Tank", 5.50) }, 4.0, new[] { R("SWAT", 6.80) }, true, "3", 1.0, q);
            Check("C1", "... no reroll hint, even with a SWAT 6.80 that could come", !call.Show && call.Why == "quest: Huntress is on the cards", call.Why);
            ranked = Screen(q, C("Tank", 5.50), C("SWAT", 5.00), C("Liberate", 1.0));
            Check("C1", "Huntress not on the cards: Liberate first (the slot is hers)", ranked[0].Liberate && ranked[0].Why[0] == "quest: Ghost + Huntress - keep the slot for Huntress" && ranked[0].Score == QuestSos.LiberateKeeps, Text(ranked));
            call = RerollCall.Decide(new[] { R("Tank", 5.50), R("SWAT", 5.00) }, 4.0, new[] { R("Medic", 6.00), R("Huntress", 4.20) }, true, "3", 1.0, q);
            Check("C1", "... the reroll hint speaks for Huntress (scored under Medic and Tank)", call.Show && call.ForQuest && call.Better.Count == 1 && call.Better[0].Name == "Huntress", call.Why);
            call = RerollCall.Decide(new[] { R("Tank", 5.50), R("SWAT", 5.00) }, 4.0, new[] { R("Medic", 6.00), R("Huntress", 4.20) }, true, "3", 0.2, q);
            Check("C1", "... late in the run too (the quest decides, not the clock)", call.Show && call.ForQuest, call.Why);
            call = RerollCall.Decide(new[] { R("Tank", 5.50), R("SWAT", 5.00) }, 4.0, new[] { R("Medic", 6.00), R("Huntress", 4.20) }, false, "0", 1.0, q);
            Check("C1", "... no reroll left: not shown", !call.Show && call.Why.StartsWith("quest: needs Huntress - no reroll left"), call.Why);
            call = RerollCall.Decide(new[] { R("Tank", 5.50), R("SWAT", 5.00) }, 4.0, new[] { R("Medic", 6.00) }, true, "3", 1.0, q);
            Check("C1", "... Huntress cannot come (locked or held back): not shown", !call.Show && call.Why.StartsWith("quest: needs Huntress, who cannot come now"), call.Why);
            bool replace; string row = q.PlanText(false, n => n.ToUpperInvariant(), out replace);
            Check("C1", "... the SOS row names her (through the display names)", row == "quest: HUNTRESS" && replace, "SOS  " + row);
            var both = team.Judge(new[] { "Ghost", "Huntress" });
            ranked = Screen(both, C("Tank", 5.50), C("Liberate", 1.0));
            row = both.PlanText(false, null, out replace);
            Check("C1", "Ghost + Huntress on the squad: Liberate first, the row says the team is complete", ranked[0].Liberate && ranked[0].Why[0] == "quest: Ghost + Huntress - take the level-up and cash" && row == "quest: team complete", Text(ranked) + " | SOS  " + row);
            var hunt = team.Judge(new[] { "Huntress" });
            ranked = Screen(hunt, C("Ghost", 3.10), C("Tank", 6.00), C("Liberate", 1.0));
            Check("C1", "a Huntress leader: the Ghost is the one wanted", ranked[0].Name == "Ghost" && ranked[0].Why[0] == "quest: needs Ghost", Text(ranked));
            var loose = new QuestTeam(); loose.Need("Ghost"); loose.Need("Huntress");
            var lq = loose.Judge(new[] { "Ghost" });
            ranked = Screen(lq, C("Tank", 6.00), C("Huntress", 3.00), C("Liberate", 1.0));
            Check("C1", "a class rule without a size (made up): Huntress first, Tank still welcome (a third slot is left)", ranked[0].Name == "Huntress" && ranked[1].Name == "Tank" && ranked[1].Score == 6.00 && !lq.OnlyNeeded, Text(ranked));
            var unreachable = team.Judge(new[] { "Tank" });
            Check("C1", "a team that can no longer meet the class rule: the advice as usual", !unreachable.Active && unreachable.Broken != null, unreachable.Advice());
        }

        // the full-team objectives (FullTeamTier3Weapons, TimedPowerupsFullTeam: requiredTeamSize 3)
        static void QuestFullTeam()
        {
            var team = new QuestTeam { Quest = "a full-team quest" }; team.AtLeast(3);
            var q = team.Judge(new[] { "Engineer" });
            var ranked = Screen(q, C("Liberate", 3.56), C("Tank", 2.33), C("Pyro", 1.18));
            Check("C1", "a full team wanted, late (the logged 18:47 screen): the recruits over Liberate", string.Join(",", ranked.Select(c => c.Name)) == "Tank,Pyro,Liberate"
                && ranked[2].Why[0] == "quest: full team - Liberate leaves a slot empty" && ranked[0].Why.Last() == "quest: full team", Text(ranked));
            ranked = Screen(q, C("Liberate", 3.56), C("Tank", 0.05));
            Check("C1", "... even a recruit scored near nothing stays over Liberate", !ranked[0].Liberate && ranked[0].Score == QuestSos.RecruitFloor, Text(ranked));
            bool replace; string late = q.PlanText(true, null, out replace), early = q.PlanText(false, null, out bool r2);
            Check("C1", "... the SOS row: the recruits with 'quest: full team' late, unchanged early", late == "quest: full team" && !replace && early == null, "late: <names>  ·  " + late + " | early: " + (early ?? "(the usual row)"));
            var full = team.Judge(new[] { "Engineer", "Tank", "Pyro" });
            Check("C1", "... a full squad: nothing to say", !full.WantsMore && !full.Capped && full.Card(null, 5.0, new List<string>()) == 5.0);
        }

        // ---------------------------------------------------------------- C2
        // Run 2 (Endless d1): the leader on Auto follows a lent "abilities first" build that ranks its four abilities; the player's own
        // style is Balanced. The leader's weapon card: floor + 0.1 x level + synergy 0.19 (+0.25 for the level that completes it) -
        // the logged 3.59 / 3.69 / 4.04. EMP Grenade is the survivor's own build ability (open: 1/4, then 2/4).
        static void WeaponHands()
        {
            const double syn = 0.19;
            Func<int, int, string, WeaponLift> lift = (unfinished, missing, left) => WeaponLift.Judge(BuildStyle.Ability, unfinished, missing, left);
            Func<WeaponLift, int, double> taser = (l, lvl) => Math.Round(Synergy.HeldWeapon(l.Floor(BuildStyle.Ability, true, BuildStyle.Balanced), 1.0, lvl, 4, syn), 2);
            // one hand: the logged cards (name, score, own open build ability?) plus the weapon; returns the weapon's score and rank
            Func<WeaponLift, int, Tuple<string, double, bool>[], Tuple<double, int, string>> hand = (l, lvl, others) =>
            {
                double w = taser(l, lvl);
                if (l.On) { var own = others.Where(o => o.Item3).Select(o => o.Item2).DefaultIfEmpty(double.MinValue).Max(); if (own > double.MinValue) w = WeaponLift.UnderOwn(w, own); }
                var all = others.Select(o => Tuple.Create(o.Item1, o.Item2)).Concat(new[] { Tuple.Create("Taser", w) }).OrderByDescending(x => x.Item2).ToList();
                int rank = all.FindIndex(x => x.Item1 == "Taser") + 1;
                return Tuple.Create(w, rank, string.Join(", ", all.Select(x => x.Item1 + " " + F(x.Item2))));
            };
            Func<string, double, bool, Tuple<string, double, bool>> card = (n, s, own) => Tuple.Create(n, s, own);

            var done1 = lift(1, 0, "EMP Grenade");
            Check("C2", "the leader's own abilities: three maxed and evolved, EMP Grenade short (the 12:56 squad) -> lifted", done1.On && done1.Note == "only EMP Grenade left: the weapon goes ahead of the others' levels", done1.Note);
            Check("C2", "the floors: abilities first 3.3, lifted to the balanced 4.3", Synergy.WeaponFloor(BuildStyle.Ability) == 3.3 && done1.Floor(BuildStyle.Ability, true, BuildStyle.Balanced) == 4.3 && done1.Floor(BuildStyle.Ability, false, BuildStyle.Balanced) == 4.3);
            var h = hand(done1, 1, new[] { card("Blowtorch", 6.24, false), card("EMP Grenade", 4.78, true), card("Makeshift Bomb", 3.58, false) });
            Check("C2", "12:56 (after the reroll): Taser 3.59 -> 4.59, #3 behind the recruit's tier-up and its own EMP Grenade", h.Item1 == 4.59 && h.Item2 == 3, h.Item3 + " (the user took the Taser; Blowtorch is a recruit's weapon tier)");
            h = hand(done1, 2, new[] { card("Molotov Cocktail", 4.30, false), card("Makeshift Bomb", 3.57, false), card("Fury Unleashed", 3.45, false) });
            Check("C2", "13:27: Taser 3.69 -> 4.69, #1 (the user's pick)", h.Item1 == 4.69 && h.Item2 == 1, h.Item3);
            h = hand(done1, 3, new[] { card("EMP Grenade", 4.78, true), card("Fire Walk", 4.60, false), card("Molotov Cocktail", 4.30, false) });
            Check("C2", "13:34: Taser 4.04 -> 5.04, kept just under its own EMP Grenade: 4.77, #2 ahead of the recruits", h.Item1 == 4.77 && h.Item2 == 2, h.Item3 + " (the user took the Taser over EMP Grenade: abilities first still holds for its own)");
            h = hand(done1, 1, new[] { card("Rocket Launcher", 6.18, false), card("EMP Grenade", 4.78, true), card("Makeshift Bomb", 3.57, false) });
            Check("C2", "11:05: Taser 4.59, #3 - the user's pick (Rocket Launcher #1) unchanged", h.Item1 == 4.59 && h.Item2 == 3, h.Item3);
            var emp1 = lift(1, 0, "EMP Grenade");
            h = hand(emp1, 1, new[] { card("Sawblade Drone", 4.32, false), card("Fury Unleashed", 4.05, false), card("Makeshift Bomb", 3.70, false) });
            Check("C2", "08:03 (EMP Grenade 1/4): Taser 4.59 over a recruit's new ability - a flip on purpose (the user took Sawblade Drone)", h.Item2 == 1, h.Item3);
            var missing = lift(0, 1, null);
            h = hand(missing, 1, new[] { card("Molotov Cocktail", 4.45, false), card("Makeshift Bomb", 4.35, false), card("Fury Unleashed", 4.05, false) });
            Check("C2", "06:56 (EMP Grenade not owned yet, a slot free): no lift - Taser 3.59, #4 (the user took Molotov #1)", !missing.On && h.Item1 == 3.59 && h.Item2 == 4, h.Item3);
            var two = lift(2, 0, "Electrocution");
            Check("C2", "two abilities still short of their last level: no lift", !two.On && two.Floor(BuildStyle.Ability, true, BuildStyle.Balanced) == 3.3);
            var done0 = lift(0, 0, null);
            Check("C2", "every ability done: lifted, 'the abilities are done: the weapon now'", done0.On && done0.Note == "the abilities are done: the weapon now", done0.Note);
            Check("C2", "the other styles are left alone", !WeaponLift.Judge(BuildStyle.Balanced, 0, 0, null).On && !WeaponLift.Judge(BuildStyle.Weapon, 0, 0, null).On
                && WeaponLift.Judge(BuildStyle.Balanced, 0, 0, null).Floor(BuildStyle.Balanced, true, BuildStyle.Weapon) == 4.3 && WeaponLift.Judge(BuildStyle.Weapon, 1, 0, "x").Floor(BuildStyle.Weapon, false, BuildStyle.Balanced) == 6.0);
            Check("C2", "lent build on Auto and the player's own style WeaponFirst: the lift takes the higher floor (6.0, fading with the clock)",
                done0.Floor(BuildStyle.Ability, true, BuildStyle.Weapon) == 6.0 && Math.Abs(Synergy.HeldWeapon(6.0, 0.5, 1, 4, 0) - 5.25) < 1e-9 && done0.Floor(BuildStyle.Ability, false, BuildStyle.Weapon) == 4.3,
                "reach 0.5: " + F(Synergy.HeldWeapon(6.0, 0.5, 1, 4, 0)) + "; a build the player chose: " + F(done0.Floor(BuildStyle.Ability, false, BuildStyle.Weapon)));
            Check("C2", "the cap: just under the own ability, untouched when under it already", WeaponLift.UnderOwn(5.04, 4.78) == 4.77 && WeaponLift.UnderOwn(4.78, 4.78) == 4.77 && WeaponLift.UnderOwn(4.59, 4.78) == 4.59
                && WeaponLift.UnderOwnLine("EMP Grenade") == "abilities first: after EMP Grenade, ahead of the rest");
            Check("C2", "the weapon formula is the 0.12 one (3.59 / 3.69 / 4.04 logged)", taser(new WeaponLift(), 1) == 3.59 && taser(new WeaponLift(), 2) == 3.69 && taser(new WeaponLift(), 3) == 4.04);
        }

        // ---------------------------------------------------------------- C3
        // the head the card shows (the first of the highest rank, as AbilityVerdict.Mark takes them) and the reasons under it
        static Tuple<string, List<string>, List<string>> AbilityCard(PowerFacts f, TagProfile tags, List<TeamBoost> boosts, RunContext run, bool oldWay = false)
        {
            var why = new List<string>();
            double tv = Synergy.TagValue(f, tags, run, why), bv = Synergy.BoostValue(f, boosts, run, why);
            HeadCut head = null;
            foreach (var hc in Synergy.AbilityHeads(f, tags, boosts, tv, bv, why)) if (head == null || hc.Rank > head.Rank) head = hc;
            string from = head == null || oldWay ? null : head.From;         // up to 0.13.0 none of the three passed its line
            return Tuple.Create(head == null ? null : head.Text, Synergy.ReasonsUnder(head == null ? null : head.Text, from, why), why);
        }

        static void Headlines(Func<string, PowerFacts> find)
        {
            var run = new RunContext { Mode = "Normal", Goal = 1200, Seconds = 300, LevelRate = 2.5, D = new Doctrine() };
            var trap = new TeamBoost { Name = "Trap Expertise", Owner = "Huntress", Tag = "Taunt", AbilitiesOnly = true };
            var grenade = new TeamBoost { Name = "Grenade Expertise", Owner = "SWAT", Tag = "Grenade" };
            Func<string, PowerFacts> pf = n => find(n) ?? new PowerFacts { Name = n, IsAbility = true };
            Func<string, int, string, double, TagProfile> prof = (type, pts, src, w) => { var t = new TagProfile { SpecialAt = 10 }; t.Deals(type, src, w); t.Points[type] = pts; return t; };

            // the logged cards: each head, its line gone, nothing left that says it again
            var decoy = pf("Zombie Decoy");
            var tg = prof("Kinetic", 4, "Bow", 1.0); tg.Deals("Explosive", "Explosive Arrows", 0.3); tg.Points["Explosive"] = 2;
            var c1 = AbilityCard(decoy, tg, new List<TeamBoost> { trap }, run);
            var o1 = AbilityCard(decoy, tg, new List<TeamBoost> { trap }, run, true);
            Check("C3", "Zombie Decoy with Trap Expertise (16 logged cards): the boost line goes", c1.Item1 == "Trap Expertise boosts it" && !c1.Item2.Any(l => Synergy.Restates(c1.Item1, l)) && o1.Item2.Any(l => Synergy.Restates(o1.Item1, l)),
                "head '" + c1.Item1 + "' | reasons: " + string.Join("; ", c1.Item2) + " | up to 0.13.0: " + string.Join("; ", o1.Item2));
            var c1b = AbilityCard(decoy, tg, new List<TeamBoost> { trap, grenade }, run);
            Check("C3", "two boosts (the 13:29 card): the second stays, as its own reason", c1b.Item1 == "Trap Expertise boosts it" && c1b.Item2.Contains("Grenade Expertise (SWAT) boosts it: grenade") && !c1b.Item2.Any(l => Synergy.Restates(c1b.Item1, l)), string.Join("; ", c1b.Item2));
            var emp = pf("EMP Grenade");
            var c2 = AbilityCard(emp, prof("Electric", 9, "Taser", 1.0), new List<TeamBoost>(), run);
            Check("C3", "EMP Grenade at Electric 9/10 (14 'special' cards): 'unlocks the Electric special', its line gone", c2.Item1 == "unlocks the Electric special" && !c2.Item2.Any(l => Synergy.Restates(c2.Item1, l)), string.Join("; ", c2.Item2));
            var bomb = pf("Makeshift Bomb");
            var c3 = AbilityCard(bomb, prof("Kinetic", 9, "Rocket Launcher", 1.0), new List<TeamBoost>(), run);
            Check("C3", "Makeshift Bomb at Kinetic 9/10: 'unlocks the Kinetic special'", c3.Item1 == "unlocks the Kinetic special" && !c3.Item2.Any(l => Synergy.Restates(c3.Item1, l)), string.Join("; ", c3.Item2));
            var falcon = pf("Falcon");
            var c4 = AbilityCard(falcon, prof("Kinetic", 3, "Crossbow", 1.0), new List<TeamBoost>(), run);
            var o4 = AbilityCard(falcon, prof("Kinetic", 3, "Crossbow", 1.0), new List<TeamBoost>(), run, true);
            Check("C3", "Falcon, Kinetic 100% (122 older cards; missed by the finding): the share line goes", c4.Item1 == "Kinetic: 100% of the squad" && !c4.Item2.Any(l => Synergy.Restates(c4.Item1, l)) && o4.Item2.Any(l => Synergy.Restates(o4.Item1, l)),
                string.Join("; ", c4.Item2) + " | up to 0.13.0: " + string.Join("; ", o4.Item2));

            // every ability of the probe, on squads that stack each type at 3 / 9 / 12 points, with and without the team passives
            var abilities = Checks_Abilities(find);
            var boostSets = new[] { new List<TeamBoost>(), new List<TeamBoost> { trap }, new List<TeamBoost> { grenade, trap }, new List<TeamBoost> { new TeamBoost { Name = "Turret Expertise", Owner = "SWAT", Tag = "Turret" }, new TeamBoost { Name = "Cold Chain", Owner = "Mechanic", Tag = "Deployable" } } };
            int cards = 0, headed = 0; var restated = new List<string>();
            foreach (var a in abilities)
                foreach (var type in TagProfile.Names)
                    foreach (int pts in new[] { 3, 9, 12 })
                        foreach (var share in new[] { 1.0, 0.45 })
                            foreach (var bs in boostSets)
                            {
                                var t = prof(type, pts, "Main Gun", share); if (share < 1) t.Deals("Fire", "Other Gun", 1 - share);
                                var c = AbilityCard(a, t, bs, run);
                                cards++; if (c.Item1 == null) continue; headed++;
                                foreach (var l in c.Item2) if (Synergy.Restates(c.Item1, l)) { restated.Add(a.Name + ": '" + c.Item1 + "' / '" + l + "'"); break; }
                            }
            Check("C3", "no ability card's head is restated by a reason (" + abilities.Count + " abilities x 7 types x 3 point levels x 2 shares x 4 passive sets)", cards > 0 && headed > 0 && restated.Count == 0,
                cards + " cards, " + headed + " with a damage-type / passive head" + (restated.Count > 0 ? "; e.g. " + string.Join(" | ", restated.Take(3)) : ""));
            Check("C3", "Restates: same words in another order or with more around them, not a different boost", Synergy.Restates("Trap Expertise boosts it", "Trap Expertise (Huntress) boosts it: taunt")
                && Synergy.Restates("Kinetic: 79% of the squad", "+1 Kinetic tag: 79% of the squad's damage (Bow)") && !Synergy.Restates("Trap Expertise boosts it", "Grenade Expertise (SWAT) boosts it: grenade") && !Synergy.Restates(null, "x"));
        }

        // the other cards the bench can build: chest items (ItemRules) and Research Pod rewards (Tags.Score) - the card's head
        // (Why[0]) said again by a later reason?
        static void OtherHeads(List<ProbeItem> items)
        {
            var k = Knowledge.FromJson(Knowledge.DefaultJson);
            var squads = new[] { new[] { "Tank" }, new[] { "Huntress", "Ghost" }, new[] { "Tank", "SWAT", "Engineer" }, new[] { "Pyro", "Medic", "Mechanic" } };
            var runs = new[]
            {
                new RunContext { Mode = "Normal", Goal = 1200, Seconds = 120, LevelRate = 2.5, D = new Doctrine() },
                new RunContext { Mode = "Normal", Goal = 1200, Seconds = 1083, LevelRate = 2.2, LevelUps = 43, Health = 0.4, D = new Doctrine() },
                new RunContext { Mode = "OneHit", Goal = 300, Seconds = 120, LevelRate = 2.5, D = new Doctrine() },
            };
            int cards = 0; var restated = new List<string>();
            foreach (var it in items ?? new List<ProbeItem>())
                foreach (var sq in squads)
                    foreach (var run in runs)
                    {
                        var tags = new TagProfile { SpecialAt = 10 }; tags.Deals("Kinetic", "Main Gun", 1.0); tags.Points["Kinetic"] = 6;
                        var c = new ItemContext { Squad = sq, Tags = tags, K = k, Ctx = run, OwnedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase), Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase), Stats = it.Stats, Healing = it.Healing };
                        var why = new List<string>(); ItemRules.Evaluate(it.Name, it.Desc, c, why); cards++;
                        if (why.Count > 1) for (int i = 1; i < why.Count; i++) if (Synergy.Restates(why[0], why[i])) { restated.Add(it.Name + ": '" + why[0] + "' / '" + why[i] + "'"); break; }
                    }
            foreach (var type in TagProfile.Names)
                foreach (int pts in new[] { 0, 6, 9, 12 })
                {
                    var tags = new TagProfile { SpecialAt = 10 }; tags.Deals(type, "Main Gun", 1.0); tags.Points[type] = pts;
                    var why = new List<string>(); Tags.Score(type, 2, tags, why); cards++;
                    if (why.Count > 1) for (int i = 1; i < why.Count; i++) if (Synergy.Restates(why[0], why[i])) { restated.Add("#" + type + " +2: '" + why[0] + "' / '" + why[i] + "'"); break; }
                }
            Check("C3", "no chest item or Research Pod card's head is restated by a reason (every probe item x 4 squads x 3 clocks, 7 types x 4 point levels)", cards > 0 && restated.Count == 0,
                cards + " cards" + (restated.Count > 0 ? "; " + restated.Count + " restated, e.g. " + string.Join(" | ", restated.Distinct().Take(6)) : ""));
        }

        static List<PowerFacts> Checks_Abilities(Func<string, PowerFacts> find)
        {
            var names = new List<string>();
            foreach (var kit in Builds.Kits) foreach (var a in kit.Abilities) { names.Add(a[0]); for (int i = 1; i < a.Length; i++) names.Add(a[i]); }
            return names.Distinct().Select(n => find(n)).Where(f => f != null).ToList();
        }

        // ---------------------------------------------------------------- C5
        static void EquipKey()
        {
            string mod = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod", "Plugin.cs"));
            string src = File.Exists(mod) ? File.ReadAllText(mod) : "";
            var m = Regex.Match(src, "Bind\\(\"Advice\", \"LoadoutEquipKey\", \"([^\"]*)\"");
            string def = m.Success ? m.Groups[1].Value : null;
            var taken = new[] { "F8", "F9", "F10", "F11", "F12", "BackQuote" };
            Check("C5", "[Advice] LoadoutEquipKey has no default key (none of F8 / F9 / F10 / F11 / F12 / BackQuote)", def != null && def.Trim().Length == 0 && !taken.Contains(def.Trim(), StringComparer.OrdinalIgnoreCase),
                def == null ? "the Bind line was not found in " + mod : "default \"" + def + "\"");
            Check("C5", "a key set by hand is checked against the other plugins' keys (Plugin.NoteKeyClash, from LoadoutEquip and the first tick)", src.Contains("internal static void NoteKeyClash(") && src.Contains("IL2CPPChainloader.Instance.Plugins"));
        }
    }
}
