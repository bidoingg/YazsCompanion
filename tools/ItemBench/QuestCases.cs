// 0.14.0 (C3): the active quest's other objectives as advice (QuestRules.cs), replayed offline (section 12 of the bench):
//   Q0  every objective of the 1.0.2 quests, decoded from the game's assets, maps to the rule the advice follows (or to none,
//       on purpose): the statistic thresholds by the statistic's enum NAME, the kill counter by its asset name;
//   Q1  the user's 2026-10-05 run with the Engineer's "Heroic Theory" (survive, finish with the tier-3 weapon at max level in
//       hand, 2000 kills with the Engineer): the logged offers of 19:10 - 19:16 replayed - the Taser went last of three under
//       the lent build's "abilities first", and the user took it against the advice three times; now every step of the weapon
//       line comes first, under evolutions and a recruit's first weapon, the abilities keep their order, the readout's QUEST
//       row follows the line (Taser, Tesla, the tier-3 weapon) and goes once the weapon is maxed;
//   Q2  one case per other decoded objective kind: a single weapon asked for (and a branch it does not count), an ability, an
//       evolution, no class abilities, no tier-3 weapon, a fully upgraded class, active synergies, health items (B2: the 10-05
//       "Trauma" chests replayed), kills that never pass the build's core, tag points to reach and to stay under, types at 10,
//       item slots, no items, evolutions, Rare+ trainings, armor, time at full health, a quest item, a tier-3 weapon each;
//   Q3  the gates: [Advice] QuestSteer InfoOnly / Off, objectives that match Any, a failed quest, a run that does not fit;
//   Q4  every line a card can say for a quest within the card rails (Wording.Rails), and the sources wired (a source check).
// Generic names only (the repository is public): the game's own quest, class and card names; the lent build is "Bench Anchor".
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class QuestCases
    {
        static int _bad;
        static readonly List<string> _lines = new List<string>();       // every quest line a card would say (Q4)
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string F(double v) { return v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }
        static string Src(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod", file));
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }
        static string Proj() { return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "ItemBench.csproj")); }

        public static int Run(string probePath)
        {
            _bad = 0; _lines.Clear();
            Console.WriteLine("\n=== 0.14.0 (C3): the active quest's objectives as advice - Q0 decoding, Q1 the 10-05 'Heroic Theory' run, Q2 every objective kind, Q3 the gates, Q4 rails and sources");
            Decoding();
            HeroicTheory();
            OneWeapon();
            Abilities();
            Without();
            FullClass();
            Synergies();
            HealthItems(probePath);
            Kills();
            Tags();
            Items();
            Counts();
            FullTeam();
            Gates();
            Rails();
            Sources();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        // ---------------------------------------------------------------- helpers
        // a card as the ranking hands it to the rules
        static QuestCard Card(QuestCardKind kind, string cls, string name, int level = 0, int max = 4, int depth = -1, params string[] types)
        {
            var c = new QuestCard { Kind = kind, Class = cls, Name = name, Level = level, Max = max, Depth = depth };
            c.Types.AddRange(types);
            return c;
        }
        sealed class Offered { public string Name; public QuestCard Card; public double Score; public double Now; public string Line; public bool Head, Avoid; }

        // an offer under the rules, as Ranker.QuestPass does it: each card judged, a modest lift kept under the core cards above it
        static List<Offered> Offer(QuestRules rules, params Tuple<QuestCard, double>[] cards)
        {
            var list = new List<Offered>();
            foreach (var t in cards)
            {
                var o = new Offered { Name = t.Item1.Name, Card = t.Item1, Score = t.Item2, Now = t.Item2 };
                var v = rules.Judge(t.Item1, t.Item2);
                if (v != null) { o.Now = Math.Round(v.Score, 2); o.Line = v.Line; o.Head = v.Head; o.Avoid = v.Avoid; if (v.Head) _lines.Add(v.Line); }
                list.Add(o);
            }
            foreach (var o in list)
            {
                if (o.Head || o.Now <= o.Score) continue;
                o.Now = Math.Round(QuestRules.UnderCore(o.Score, o.Now, list.Where(x => x != o && x.Card.Core).Select(x => new KeyValuePair<double, double>(x.Score, x.Now))), 2);
            }
            return list.OrderByDescending(o => o.Now).ToList();
        }
        static string Text(List<Offered> ranked) { return string.Join(" | ", ranked.Select(o => o.Name + " " + F(o.Score) + (o.Now != o.Score ? ">" + F(o.Now) : "") + (o.Head ? " '" + Wording.Quest(o.Line) + "'" : ""))); }
        static Tuple<QuestCard, double> T(QuestCard c, double score) { return Tuple.Create(c, score); }

        // ---------------------------------------------------------------- Q0: the 1.0.2 quests' objectives, decoded from sharedassets1.assets
        // (research: the objectives' serialized fields, read with UnityPy; the statistic by PlayerStatistic.EType's name)
        static void Decoding()
        {
            var stats = new[]
            {   // quest, statistic, comparison (0 < .. 4 >), threshold, fails for good, the rule wanted
                Tuple.Create("Engineer_R1", "InternalNumAbilityEvolutions", 3, 6.0, false, "Evolutions 6"),
                Tuple.Create("Engineer_R2", "InternalNumRarePlusMilitaryTrainings", 3, 3.0, false, "RareTraining 3"),
                Tuple.Create("Ghost_5", "TeamHashtagElectricNum", 3, 40.0, false, "TagPoints Electric to 40"),
                Tuple.Create("Huntress_2", "TeamHashtagExplosiveNum", 3, 10.0, false, "TagPoints Explosive to 10"),
                Tuple.Create("Huntress_3", "InternalNumFreeItemSlots", 1, 0.0, false, "FillSlots 0"),
                Tuple.Create("Mechanic_1", "InternalNumFreeItemSlots", 1, 0.0, false, "FillSlots 0"),
                Tuple.Create("Mechanic_4", "TeamHashtagIceNum", 3, 25.0, false, "TagPoints Ice to 25"),
                Tuple.Create("Medic_5", "TeamHashtagToxicNum", 3, 20.0, false, "TagPoints Chemical to 20"),
                Tuple.Create("Pyro_3", "TeamHashtagExplosiveSlashingFireNum", 3, 30.0, false, "TagPoints Explosive+Slashing+Fire to 30"),
                Tuple.Create("Pyro_5", "TeamHashtagExplosiveSlashingFireNum", 0, 20.0, false, "TagPoints Explosive+Slashing+Fire under 20"),
                Tuple.Create("Pyro_RB", "InternalNumHashtagTypes10Plus", 3, 4.0, false, "TypesAt10 4"),
                Tuple.Create("Ranger_RB", "InternalNumEquippedItems", 2, 0.0, true, "NoItems 0"),
                Tuple.Create("Tank_RA", "TeamHashtagExplosiveNum", 3, 20.0, false, "TagPoints Explosive to 20"),
                Tuple.Create("Tank_RB", "TeamArmor", 3, 150.0, false, "Armor 150"),
                Tuple.Create("Huntress_4", "InternalNumSurvivors", 2, 1.0, false, "none (the team size: QuestTeam)"),
                Tuple.Create("Ghost_3", "InternalNumSurvivors", 2, 2.0, false, "none (the team size: QuestTeam)"),
            };
            int ok = 0; var bad = new List<string>();
            foreach (var s in stats)
            {
                var r = QuestRules.FromStatistic(s.Item2, s.Item3, s.Item4, s.Item5);
                string got = r == null ? "none (the team size: QuestTeam)" : r.Ask == QuestAsk.TagPoints ? "TagPoints " + string.Join("+", r.Targets) + (r.Below ? " under " : " to ") + r.Need
                    : r.Ask + " " + r.Need;
                if (got == s.Item6) ok++; else bad.Add(s.Item1 + ": " + got + " (wanted " + s.Item6 + ")");
            }
            Check("Q0", "the 16 statistic thresholds of the 1.0.2 quests decode as wanted (" + ok + " of " + stats.Length + ")", bad.Count == 0, string.Join("; ", bad));
            var k = QuestRules.FromGameStatistic("CharacterKillsCurrentRunEngineer", 3, 2000);
            Check("Q0", "Engineer_3's GameStatisticThreshold CharacterKillsCurrentRunEngineer >= 2000 -> kills with the Engineer", k != null && k.Ask == QuestAsk.Kills && k.Class == "Engineer" && k.Need == 2000,
                k == null ? "none" : k.Ask + " " + k.Class + " " + k.Need);
            var g = QuestRules.FromGameStatistic("CharacterKillsCurrentRunNinja", 4, 99.5);
            Check("Q0", "the class part is the game's enum name (Ninja shows as Ghost); '>' 99.5 needs 100", g != null && g.Class == "Ghost" && g.Need == 100);
            Check("Q0", "a statistic no quest asks (TeamLuck >= 5, TeamHashtagFire without Num) and a comparison the advice cannot use leave the cards alone",
                QuestRules.FromStatistic("TeamLuck", 3, 5, false) == null && QuestRules.FromStatistic("TeamHashtagFire", 3, 5, false) == null && QuestRules.FromStatistic("TeamArmor", 2, 150, false) == null
                && QuestRules.FromGameStatistic("ZombiesKilledTotal", 3, 10) == null);
            Check("Q0", "comparisons: '<=' 19 caps at 20, '>' 9 needs 10", QuestRules.FromStatistic("TeamHashtagFireNum", 1, 19, false).Need == 20 && QuestRules.FromStatistic("TeamHashtagFireNum", 4, 9, false).Need == 10);
            Check("Q0", "the game's comparison (Holds)", QuestRules.Holds(0, 19, 20) && !QuestRules.Holds(0, 20, 20) && QuestRules.Holds(1, 20, 20) && QuestRules.Holds(2, 0, 0) && QuestRules.Holds(3, 40, 40) && !QuestRules.Holds(4, 40, 40));
        }

        // ---------------------------------------------------------------- Q1: Heroic Theory, the user's run of 2026-10-05 19:10
        // Engineer leader alone, Endless, the lent build "Bench Anchor" (Ability style: Energy Shield #1, Electric Turret #2,
        // Electrocution #3, EMP Grenade #4), quest GameHubQuest_Engineer_3: FinishWithWeapons Plasma / Laser / Blaster at level 4,
        // any, in hand (every tier-3 branch of the Engineer: "the tier-3 weapon"); kills with the Engineer >= 2000; survive.
        static QuestRules Heroic()
        {
            var rules = new QuestRules { Quest = "GameHubQuest_Engineer_3" };
            var w = new QuestRule { Ask = QuestAsk.WeaponLine, Class = "Engineer", Depth = 2, Level = 4, Current = true, AllBranches = true };
            w.Targets.AddRange(new[] { "Plasma", "Laser", "Blaster" });
            rules.List.Add(w);
            rules.List.Add(QuestRules.FromGameStatistic("CharacterKillsCurrentRunEngineer", 3, 2000));
            return rules;
        }
        static QuestCard Ab(string name, int level, int priority, string type = "Electric")
        {
            var c = Card(QuestCardKind.Ability, "Engineer", name, level, 4, -1, type);
            c.Core = priority >= 0 && priority <= 2; return c;
        }
        static QuestCard Taser(int level) { return Card(QuestCardKind.Weapon, "Engineer", "Taser", level, 4, 0, "Electric"); }

        static void HeroicTheory()
        {
            var rules = Heroic();
            var w = rules.List[0]; var kills = rules.List[1];
            w.From = "Taser"; w.FromLevel = 1; w.FromMax = 4; w.FromDepth = 0; kills.Have = 0;
            // the logged offers with a Taser card (19:10:58 - 19:15:25): score as logged, the card ranked first then
            var offers = new[]
            {
                Tuple.Create("00:29", "Energy Shield", new[] { T(Ab("Energy Shield", 0, 0), 4.93), T(Taser(1), 3.72), T(Ab("EMP Grenade", 0, 3), 4.66) }),
                Tuple.Create("00:44", "Energy Shield", new[] { T(Ab("EMP Grenade", 0, 3), 4.30), T(Ab("Energy Shield", 1, 0), 4.84), T(Taser(1), 3.72) }),
                Tuple.Create("00:54", "Electric Turret", new[] { T(Taser(1), 3.84), T(Ab("Electrocution", 0, 2), 4.75), T(Ab("Electric Turret", 2, 1), 4.90) }),
                Tuple.Create("01:04", "Energy Shield", new[] { T(Ab("EMP Grenade", 0, 3), 4.45), T(Taser(1), 3.84), T(Ab("Energy Shield", 2, 0), 4.88) }),
                Tuple.Create("01:14", "Electric Turret", new[] { T(Taser(1), 3.96), T(Ab("Electric Turret", 3, 1), 5.10), T(Ab("Energy Shield", 3, 0), 5.08) }),
                Tuple.Create("01:20", "Electrocution", new[] { T(Ab("Electrocution", 0, 2), 4.70), T(Ab("EMP Grenade", 0, 3), 4.30), T(Taser(2), 3.82) }),
                Tuple.Create("01:56", "Electric Turret", new[] { T(Taser(3), 4.17), T(Ab("Electrocution", 0, 2), 4.70), T(Ab("Electric Turret", 3, 1), 5.24) }),
            };
            int first = 0;
            foreach (var o in offers)
            {
                var ranked = Offer(rules, o.Item3);
                var taser = ranked.First(x => x.Name == "Taser");
                int lvl = taser.Card.Level + 1;
                string want = "Quest: leads to the tier-3 weapon (Level " + lvl + " of 4)";       // a step on the way (the game's TIER I plate), not the weapon asked for
                bool ok = ranked[0].Name == "Taser" && Wording.Quest(taser.Line) == want && taser.Now < 7.2;
                // the abilities keep the order they had among themselves
                var before = o.Item3.Where(x => x.Item1.Name != "Taser").OrderByDescending(x => x.Item2).Select(x => x.Item1.Name).ToList();
                var after = ranked.Where(x => x.Name != "Taser").Select(x => x.Name).ToList();
                ok &= before.SequenceEqual(after);
                if (ok) first++;
                Check("Q1", o.Item1 + ": Taser first (logged: " + o.Item2 + " first, Taser last)", ok, Text(ranked));
            }
            Check("Q1", "the Taser came first on every logged offer it was on (" + first + " of " + offers.Length + "); the user took it at 01:14, 01:20, 01:56", first == offers.Length);

            // 00:22: no weapon card - the kill lift moves every Engineer card alike, the order stays
            var r22 = Offer(rules, T(Ab("EMP Grenade", 0, 3), 4.66), T(Ab("Electric Turret", 0, 1), 4.92), T(Ab("Electrocution", 0, 2), 4.85));
            Check("Q1", "00:22 (no weapon card): the order as logged, Electric Turret first; the kills reason in the log only", r22.Select(x => x.Name).SequenceEqual(new[] { "Electric Turret", "Electrocution", "EMP Grenade" }) && r22.All(x => !x.Head),
                Text(r22));

            // 02:31: the next tier (Tesla) on the way to tier 3: over the abilities, still under a recruit's first weapon and evolutions
            w.From = "Taser"; w.FromLevel = 4;
            var tesla = Card(QuestCardKind.NextTier, "Engineer", "Tesla", 0, 4, 1, "Electric");
            var r231 = Offer(rules, T(Ab("Electrocution", 0, 2), 4.70), T(Ab("Electric Turret", 3, 1), 5.24), T(tesla, 6.92));
            Check("Q1", "02:31: Tesla (the next tier) first with 'Quest: leads to the tier-3 weapon (next tier)', at most 7.15", r231[0].Name == "Tesla" && Wording.Quest(r231[0].Line) == "Quest: leads to the tier-3 weapon (next tier)" && r231[0].Now <= QuestRules.LineCap && r231[0].Now >= 6.92, Text(r231));

            // the fork: the favoured branch and another one both count (any branch, in hand); the evolution and a recruit's first weapon stay on top
            w.From = "Tesla"; w.FromLevel = 4; w.FromDepth = 1;
            var plasma = Card(QuestCardKind.NextTier, "Engineer", "Plasma", 0, 4, 2, "Electric");
            var laser = Card(QuestCardKind.Branch, "Engineer", "Laser", 0, 4, 2, "Electric");
            var evo = Card(QuestCardKind.Evolution, "Engineer", "Electric Turret: Tesla Coil", 0, 1, -1, "Electric"); evo.Base = "Electric Turret";
            var first2 = Card(QuestCardKind.FirstWeapon, "Tank", "Shotgun", 0, 4, 0, "Kinetic");
            var fork = Offer(rules, T(plasma, 6.95), T(laser, 2.10), T(evo, 7.92), T(first2, 7.2));
            Check("Q1", "the fork: an evolution 7.92 and a recruit's first weapon 7.2 stay over the weapon line; Plasma (favoured) over Laser, both 'this one counts'",
                fork.Select(x => x.Name).SequenceEqual(new[] { "Electric Turret: Tesla Coil", "Shotgun", "Plasma", "Laser" }) && fork[2].Line == "quest: max the tier-3 weapon - this one counts" && fork[3].Head && !fork[3].Avoid,
                Text(fork));

            // on the tier-3 weapon: its levels first, the row says how far
            w.From = "Plasma"; w.FromLevel = 1; w.FromDepth = 2;
            var pl = Card(QuestCardKind.Weapon, "Engineer", "Plasma", 1, 4, 2, "Electric");
            var r3 = Offer(rules, T(pl, 4.10), T(Ab("Electric Turret", 3, 1), 5.24), T(Ab("EMP Grenade", 2, 3), 4.40));
            Check("Q1", "Plasma 1 to 2 first: 'Quest: max the tier-3 weapon (Level 2 of 4)'", r3[0].Name == "Plasma" && Wording.Quest(r3[0].Line) == "Quest: max the tier-3 weapon (Level 2 of 4)", Text(r3));

            // the QUEST row along the line, and gone once the weapon is maxed in hand
            var rows = new List<string>();
            w.From = "Taser"; w.FromLevel = 1; w.FromDepth = 0; rows.Add(string.Join(" · ", rules.Row()));
            w.From = "Tesla"; w.FromLevel = 2; w.FromDepth = 1; rows.Add(string.Join(" · ", rules.Row()));
            w.From = "Plasma"; w.FromLevel = 3; w.FromDepth = 2; rows.Add(string.Join(" · ", rules.Row()));
            w.Met = true; rows.Add(string.Join(" · ", rules.Row()));
            Check("Q1", "the QUEST row: 'Taser to tier 3, max level' -> 'Tesla to tier 3, max level' -> 'Plasma 3/4 to max level' -> the kills alone once maxed",
                rows[0] == "Taser to tier 3, max level · Engineer kills to 2000" && rows[1] == "Tesla to tier 3, max level · Engineer kills to 2000" && rows[2] == "Plasma 3/4 to max level · Engineer kills to 2000" && rows[3] == "Engineer kills to 2000",
                string.Join(" / ", rows));
            var met = Offer(rules, T(Card(QuestCardKind.Weapon, "Engineer", "Plasma", 4, 5, 2, "Electric"), 3.0), T(Ab("Electric Turret", 3, 1), 5.24));
            Check("Q1", "once met, the weapon line goes back to the build's order", met[0].Name == "Electric Turret" && !met.Any(x => x.Head), Text(met));
            w.Met = false;
            // the log line
            w.From = "Taser"; w.FromLevel = 1; w.FromDepth = 0; kills.Have = 812;
            string said = rules.Said();
            Check("Q1", "the [quest] line: what each rule does", said.StartsWith("Taser to tier 3, max level: lifts the Engineer's weapon line to max the tier-3 weapon") && said.Contains("Engineer kills to 2000: a modest lift"), said);
            Console.WriteLine("      [quest] GameHubQuest_Engineer_3 -> " + said);
            string key1 = rules.Key(); kills.Have = 1500; string key2 = rules.Key(); w.FromLevel = 2; string key3 = rules.Key();
            Check("Q1", "the [quest] line comes again with a pick that moves the weapon line, not with every kill", key1 == key2 && key2 != key3);
        }

        // ---------------------------------------------------------------- Q2: one case per other decoded objective kind
        static void OneWeapon()
        {
            // Pyro_1: the tier-2 weapon (Blowtorch) at level 4, not in hand, and Molotov Cocktail at level 4
            var rules = new QuestRules { Quest = "GameHubQuest_Pyro_1" };
            var w = new QuestRule { Ask = QuestAsk.WeaponLine, Class = "Pyro", Depth = 1, Level = 4 }; w.Targets.Add("Blowtorch");
            var a = new QuestRule { Ask = QuestAsk.Ability, Class = "Pyro", Level = 4, All = true }; a.Targets.Add("Molotov Cocktail");
            rules.List.Add(w); rules.List.Add(a);
            var fireaxe = Card(QuestCardKind.Weapon, "Pyro", "Fireaxe", 1, 4, 0, "Slashing");
            var r = Offer(rules, T(fireaxe, 3.6), T(Card(QuestCardKind.Ability, "Pyro", "Fire Walk", 1, 4, -1, "Fire"), 4.6));
            Check("Q2", "Pyro_1: the tier-1 weapon leads to the one asked: 'Quest: leads to Blowtorch (Level 2 of 4)'", r[0].Name == "Fireaxe" && Wording.Quest(r[0].Line) == "Quest: leads to Blowtorch (Level 2 of 4)", Text(r));
            var r2 = Offer(rules, T(Card(QuestCardKind.NextTier, "Pyro", "Blowtorch", 0, 4, 1, "Fire"), 6.7), T(Card(QuestCardKind.Ability, "Pyro", "Molotov Cocktail", 0, 4, -1, "Fire"), 4.4));
            Check("Q2", "Pyro_1: the tier-2 weapon itself 'counts'; a new Molotov Cocktail 'take it and max it', both over 6.7",
                r2[0].Line == "quest: max Blowtorch - this one counts" && r2[1].Line == "quest: take it and max it" && r2.All(x => x.Now >= 6.7), Text(r2));
            w.Met = true;
            var past = Offer(rules, T(Card(QuestCardKind.NextTier, "Pyro", "Flamethrower", 0, 4, 2, "Fire"), 6.9));
            Check("Q2", "Pyro_1: past the weapon asked for (held at level 4: the game counts it held, not in hand) the tier-3 weapon is left alone", !past[0].Head && past[0].Now == 6.9, Text(past));
            Check("Q2", "Pyro_1: the row 'take Molotov Cocktail, max it' (the weapon met)", string.Join(" · ", rules.Row()) == "take Molotov Cocktail, max it");
            // Mechanic_2: any of the three tier-3 branches in hand - the same rule as Q1 on the Mechanic's line
            var m = new QuestRules { Quest = "GameHubQuest_Mechanic_2" };
            var mw = new QuestRule { Ask = QuestAsk.WeaponLine, Class = "Mechanic", Depth = 2, Level = 4, Current = true, AllBranches = true }; mw.Targets.AddRange(new[] { "Chaos Engine", "Chainsaw", "Nitro-Gun" });
            m.List.Add(mw);
            var rm = Offer(m, T(Card(QuestCardKind.Weapon, "Mechanic", "Wrench", 2, 4, 0, "Kinetic"), 3.3), T(Card(QuestCardKind.Ability, "Mechanic", "Ice Turret", 1, 4, -1, "Ice"), 4.8));
            Check("Q2", "Mechanic_2: the Mechanic's tier-1 weapon first, 'Quest: leads to the tier-3 weapon (Level 3 of 4)'", rm[0].Name == "Wrench" && Wording.Quest(rm[0].Line) == "Quest: leads to the tier-3 weapon (Level 3 of 4)", Text(rm));
            // a quest that names ONE tier-3 branch: the others fail it (the branches exclude each other)
            var one = new QuestRules { Quest = "a one-branch quest (made up)" };
            var ow = new QuestRule { Ask = QuestAsk.WeaponLine, Class = "Engineer", Depth = 2, Level = 4, Current = true }; ow.Targets.Add("Plasma");
            one.List.Add(ow);
            var ro = Offer(one, T(Card(QuestCardKind.NextTier, "Engineer", "Laser", 0, 4, 2, "Electric"), 6.9), T(Card(QuestCardKind.Branch, "Engineer", "Plasma", 0, 4, 2, "Electric"), 2.0));
            Check("Q2", "one branch asked: Plasma (another branch on the cards) over Laser (the build's), Laser AVOID 'needs Plasma - this branch fails it'",
                ro[0].Name == "Plasma" && ro[1].Avoid && ro[1].Now < 1 && ro[1].Line == "quest: needs Plasma - this branch fails it" && Synergy.ReasonPrefix(2, ro[1].Now).Contains("AVOID"), Text(ro));
        }

        static void Abilities()
        {
            // Ranger_3: finish with an evolution of Good Boy
            var rules = new QuestRules { Quest = "GameHubQuest_Ranger_3" };
            var e = new QuestRule { Ask = QuestAsk.Evolution, Class = "Ranger" }; e.Targets.Add("Good Boy");
            rules.List.Add(e);
            var r = Offer(rules, T(Card(QuestCardKind.Ability, "Ranger", "Good Boy", 2, 4, -1, "Kinetic"), 4.2), T(Card(QuestCardKind.Ability, "Ranger", "Falcon", 3, 4, -1, "Kinetic"), 5.1));
            Check("Q2", "Ranger_3: Good Boy 2 to 3 first, 'Quest: evolve it by the end (Level 3 of 4)'", r[0].Name == "Good Boy" && Wording.Quest(r[0].Line) == "Quest: evolve it by the end (Level 3 of 4)", Text(r));
            var evA = Card(QuestCardKind.Evolution, "Ranger", "Good Boy: Alpha", 0, 1, -1, "Kinetic"); evA.Base = "Good Boy";
            var evB = Card(QuestCardKind.Evolution, "Ranger", "Good Boy: Pack", 0, 1, -1, "Kinetic"); evB.Base = "Good Boy";
            var r2 = Offer(rules, T(evA, 8.1), T(evB, 7.7), T(Card(QuestCardKind.Evolution, "Ranger", "Falcon: Hunting Sweep", 0, 1, -1), 8.3));
            Check("Q2", "Ranger_3: both evolutions of Good Boy count (+1.0, the build's pick still first), over another base's evolution", r2[0].Name == "Good Boy: Alpha" && r2[1].Name == "Good Boy: Pack" && r2[0].Line == "quest: evolves Good Boy - it counts", Text(r2));
            e.Unreachable = "its evolutions are locked in the Skill Tree";
            var r3 = Offer(rules, T(Card(QuestCardKind.Ability, "Ranger", "Good Boy", 2, 4, -1), 4.2), T(Card(QuestCardKind.Ability, "Ranger", "Falcon", 3, 4, -1), 5.1));
            Check("Q2", "Ranger_3 with the evolution locked in the Skill Tree: nothing lifted (it cannot be met), no QUEST row", r3[0].Name == "Falcon" && !r3.Any(x => x.Head) && rules.Row().Count == 0, Text(r3));
        }

        static void Without()
        {
            // Pyro_4: finish without Pyro abilities - every Pyro ability or evolution AVOID, the weapon alone
            var rules = new QuestRules { Quest = "GameHubQuest_Pyro_4" };
            rules.List.Add(new QuestRule { Ask = QuestAsk.NoClassAbility, Class = "Pyro" });
            var r = Offer(rules, T(Card(QuestCardKind.Ability, "Pyro", "Molotov Cocktail", 0, 4, -1, "Fire"), 4.9), T(Card(QuestCardKind.Weapon, "Pyro", "Fireaxe", 1, 4, 0, "Slashing"), 3.5),
                T(Card(QuestCardKind.Ability, "Tank", "Minefield", 0, 4, -1, "Explosive"), 4.1));
            var molotov = r.First(x => x.Name == "Molotov Cocktail");
            Check("Q2", "Pyro_4: a Pyro ability AVOID ('any Pyro ability fails it'), the weapon and another class's ability untouched",
                molotov.Avoid && molotov.Now < 1 && Wording.Quest(molotov.Line) == "Quest: any Pyro ability fails it" && r[0].Name == "Minefield" && r.First(x => x.Name == "Fireaxe").Now == 3.5, Text(r));
            // Pyro_RA: no tier-3 weapon in hand at the end
            var t3 = new QuestRules { Quest = "GameHubQuest_Pyro_RA" };
            t3.List.Add(new QuestRule { Ask = QuestAsk.NoTier3 });
            var r2 = Offer(t3, T(Card(QuestCardKind.NextTier, "Pyro", "Flamethrower", 0, 4, 2, "Fire"), 6.9), T(Card(QuestCardKind.NextTier, "Tank", "Pump-Action Shotgun", 0, 4, 1, "Kinetic"), 6.7));
            Check("Q2", "Pyro_RA: a tier-3 weapon AVOID, a tier-2 one untouched; the row 'no tier-3 weapon'",
                r2[0].Name == "Pump-Action Shotgun" && r2[1].Avoid && Wording.Quest(r2[1].Line) == "Quest: a tier-3 weapon fails it" && string.Join(" · ", t3.Row()) == "no tier-3 weapon", Text(r2));
        }

        static void FullClass()
        {
            // Tank_5: every Tank powerup to its last level (the leader's class) - the ability the build skips comes back in, the rest a little
            var rules = new QuestRules { Quest = "GameHubQuest_Tank_5" };
            rules.List.Add(new QuestRule { Ask = QuestAsk.FullClass, Class = "Tank", Have = 1, Need = 5 });
            var skipped = Card(QuestCardKind.Ability, "Tank", "Bombing Strike", 0, 4, -1, "Explosive"); skipped.Skipped = true;
            var r = Offer(rules, T(skipped, 1.9), T(Card(QuestCardKind.Ability, "Tank", "Sawblade Drone", 2, 4, -1, "Slashing"), 4.5), T(Card(QuestCardKind.Ability, "SWAT", "Ricochet", 1, 4, -1, "Kinetic"), 4.6));
            var bs = r.First(x => x.Name == "Bombing Strike"); var sd = r.First(x => x.Name == "Sawblade Drone");
            Check("Q2", "Tank_5: the ability the build skips +2.0 ('every Tank ability to max level'), another Tank level +0.4 (log only), over the SWAT's level",
                bs.Head && Math.Abs(bs.Now - 3.9) < 0.001 && !sd.Head && Math.Abs(sd.Now - 4.9) < 0.001 && r[0].Name == "Sawblade Drone", Text(r));
            Check("Q2", "Tank_5: the row 'max every Tank powerup (1 of 5)'", string.Join(" · ", rules.Row()) == "max every Tank powerup (1 of 5)");
        }

        static void Synergies()
        {
            // Engineer_5: 2 active synergies of the Engineer - a recruit with an unlocked synergy with the Engineer first
            var rules = new QuestRules { Quest = "GameHubQuest_Engineer_5" };
            rules.List.Add(new QuestRule { Ask = QuestAsk.Synergies, Class = "Engineer", Have = 0, Need = 2 });
            var tank = Card(QuestCardKind.Recruit, "Tank", "Tank"); tank.Synergy.Add("Engineer");
            var pyro = Card(QuestCardKind.Recruit, "Pyro", "Pyro");
            var r = Offer(rules, T(pyro, 4.4), T(tank, 3.6), T(Card(QuestCardKind.Liberate, null, "Liberate"), 1.6));
            Check("Q2", "Engineer_5: the recruit with a synergy with the Engineer first ('2 synergies with Engineer (0 so far)')", r[0].Name == "Tank" && Wording.Quest(r[0].Line) == "Quest: 2 synergies with Engineer (0 so far)", Text(r));
            var ab = Card(QuestCardKind.Ability, "Engineer", "Electric Turret", 1, 4, -1, "Electric"); ab.Synergy.Add("Tank");
            var r2 = Offer(rules, T(ab, 4.5), T(Card(QuestCardKind.Ability, "Engineer", "EMP Grenade", 1, 4, -1, "Electric"), 4.7));
            Check("Q2", "Engineer_5: an ability with its synergy partner on the team +0.6 (log only)", r2[0].Name == "Electric Turret" && !r2[0].Head && Math.Abs(r2[0].Now - 5.1) < 0.001, Text(r2));
        }

        // B2: the 10-05 "Trauma" run (Medic_1: survive, 1 health item held by the leader); a health item = isHealingItem or the
        // HealthRelated tag (the game's IsHealthRelatedItem, read in its machine code)
        static void HealthItems(string probePath)
        {
            var health = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var doc = JsonDocument.Parse(File.ReadAllText(probePath)))
                    foreach (var it in doc.RootElement.GetProperty("items").EnumerateArray())
                    {
                        JsonElement e; bool h = it.TryGetProperty("healing", out e) && e.ValueKind == JsonValueKind.True;
                        if (it.TryGetProperty("stringTags", out e) && e.ValueKind == JsonValueKind.Array) foreach (var t in e.EnumerateArray()) if (t.GetString() == "HealthRelated") h = true;
                        if (h) health.Add(it.GetProperty("name").GetString());
                    }
            }
            catch { }
            Check("Q2", "B2: the probe's health items (isHealingItem or HealthRelated): " + health.Count + ", Last Unicorn by its tag, Health Potion by its flag, Giant Enemy Crab not",
                health.Count >= 10 && health.Contains("Last Unicorn") && health.Contains("Health Potion") && !health.Contains("Giant Enemy Crab"), string.Join(", ", health.OrderBy(x => x)));
            Func<string, QuestCard> item = n => { var c = Card(QuestCardKind.Item, null, n); c.Health = health.Contains(n); return c; };
            var rules = new QuestRules { Quest = "GameHubQuest_Medic_1" };
            var h1 = new QuestRule { Ask = QuestAsk.HealthItems, Need = 1, Have = 0 };
            rules.List.Add(h1);
            // 02:15 (logged: Last Unicorn first on other merits) - still first, the other two health items above the rest
            var c215 = Offer(rules, T(item("Last Unicorn"), 2.98), T(item("Vampire Survivor"), 2.20), T(item("Potato"), 1.35), T(item("Jewel of Life"), 2.20));
            Check("Q2", "B2 02:15: Last Unicorn first, 'Quest: hold a health item (0 of 1)', +2.0", c215[0].Name == "Last Unicorn" && Math.Abs(c215[0].Now - 4.98) < 0.001 && Wording.Quest(c215[0].Line) == "Quest: hold a health item (0 of 1)", Text(c215));
            // 03:43 had 02:15 gone elsewhere: Health Potion (logged #3, 2.29) over Giant Enemy Crab 3.72
            var c343 = Offer(rules, T(item("Health Potion"), 2.29), T(item("Teddy Bear"), 1.47), T(item("Giant Enemy Crab"), 3.72), T(item("Boxing Gloves"), 2.54));
            Check("Q2", "B2 03:43 with no health item held: Health Potion (logged #3) first", c343[0].Name == "Health Potion" && Math.Abs(c343[0].Now - 4.29) < 0.001, Text(c343));
            Check("Q2", "B2: the row 'a health item (0 of 1)' (as the card counts) until one is held", string.Join(" · ", rules.Row()) == "a health item (0 of 1)");
            h1.Have = 1; h1.Met = true;
            var c343b = Offer(rules, T(item("Health Potion"), 2.29), T(item("Giant Enemy Crab"), 3.72));
            var c829 = Offer(rules, T(item("Glass of Milk"), 2.50), T(item("Battery Leakage"), 2.58));
            Check("Q2", "B2 03:43 / 08:29 as they were (Last Unicorn held: 1 of 1): no lift, the logged order, no row", c343b[0].Name == "Giant Enemy Crab" && c829[0].Name == "Battery Leakage" && !c343b.Concat(c829).Any(x => x.Line != null) && rules.Row().Count == 0,
                Text(c343b) + " / " + Text(c829));
            var two = new QuestRules(); two.List.Add(new QuestRule { Ask = QuestAsk.HealthItems, Need = 2, Have = 1 });
            var r2 = Offer(two, T(item("MedKit"), 1.0));
            Check("Q2", "B2 with a target of 2: 'Quest: hold 2 health items (1 of 2)'", Wording.Quest(r2[0].Line) == "Quest: hold 2 health items (1 of 2)", r2[0].Line);
        }

        static void Kills()
        {
            // kills with one class: a modest lift on its damage cards, never over a card of a build's core that stood above it
            var rules = new QuestRules { Quest = "kills with the Engineer (Engineer_3's second objective)" };
            rules.List.Add(QuestRules.FromGameStatistic("CharacterKillsCurrentRunEngineer", 3, 2000));
            rules.List[0].Have = 300;
            var fury = Card(QuestCardKind.Ability, "Tank", "Fury Unleashed", 2, 4, -1, "Kinetic"); fury.Core = true;
            var emp = Card(QuestCardKind.Ability, "Engineer", "EMP Grenade", 1, 4, -1, "Electric");
            var shield = Card(QuestCardKind.Ability, "Engineer", "Energy Shield", 1, 4, -1);        // no damage type: no lift
            var r = Offer(rules, T(fury, 5.00), T(emp, 4.90), T(shield, 4.80));
            var e = r.First(x => x.Name == "EMP Grenade");
            Check("Q2", "kills: the Engineer's EMP Grenade 4.90 + 0.3 stays under the Tank's core Fury Unleashed 5.00; a card without damage gets nothing",
                r[0].Name == "Fury Unleashed" && e.Now > 4.90 && e.Now < 5.00 && !e.Head && e.Line == "quest: Engineer kills (300 of 2000)" && r.First(x => x.Name == "Energy Shield").Now == 4.80, Text(r));
            var r2 = Offer(rules, T(fury, 4.00), T(emp, 4.90));
            Check("Q2", "kills: over a core card that was BELOW it already the lift is free", r2[0].Name == "EMP Grenade" && Math.Abs(r2[0].Now - 5.20) < 0.001, Text(r2));
        }

        static void Tags()
        {
            // Ghost_5: Electric tags to 40 - a Research Pod of the type strongly, a level dealing it a little
            var rules = new QuestRules { Quest = "GameHubQuest_Ghost_5" };
            rules.List.Add(QuestRules.FromStatistic("TeamHashtagElectricNum", 3, 40, false));
            rules.List[0].Have = 22;
            var pod = Card(QuestCardKind.Tags, null, "#Electric +2", 0, 0, -1, "Electric"); pod.Points = 2;
            var fire = Card(QuestCardKind.Tags, null, "#Fire +4", 0, 0, -1, "Fire"); fire.Points = 4;
            var r = Offer(rules, T(fire, 3.4), T(pod, 2.6));
            Check("Q2", "Ghost_5: '#Electric +2' over '#Fire +4': 'Quest: Electric tags to 40 (22 now)'", r[0].Name == "#Electric +2" && Wording.Quest(r[0].Line) == "Quest: Electric tags to 40 (22 now)", Text(r));
            var lvl = Offer(rules, T(Card(QuestCardKind.Ability, "Ghost", "Pulsar", 1, 4, -1, "Electric"), 4.0));
            Check("Q2", "Ghost_5: a level dealing Electric +0.4, the reason in the log", !lvl[0].Head && Math.Abs(lvl[0].Now - 4.4) < 0.001, Text(lvl));
            // Pyro_5: Explosive + Slashing + Fire must stay under 20 - a Research Pod that reaches it AVOID, a level near the cap lower
            var cap = new QuestRules { Quest = "GameHubQuest_Pyro_5" };
            cap.List.Add(QuestRules.FromStatistic("TeamHashtagExplosiveSlashingFireNum", 0, 20, false));
            cap.List[0].Have = 17;
            var fpod = Card(QuestCardKind.Tags, null, "#Fire +4", 0, 0, -1, "Fire"); fpod.Points = 4;
            var molotov = Card(QuestCardKind.Ability, "Pyro", "Molotov Cocktail", 1, 4, -1, "Fire");
            var rc = Offer(cap, T(fpod, 3.8), T(molotov, 4.6), T(Card(QuestCardKind.Ability, "Pyro", "No Pain, No Gain", 1, 4, -1), 4.3));
            var f = rc.First(x => x.Name == "#Fire +4"); var m = rc.First(x => x.Name == "Molotov Cocktail");
            Check("Q2", "Pyro_5 at 17 of 20: '#Fire +4' AVOID ('would pass its cap of 20 tags'), a Fire level 0.8 lower (log only)",
                f.Avoid && Wording.Quest(f.Line) == "Quest: would pass its cap of 20 tags" && !m.Head && Math.Abs(m.Now - 3.8) < 0.001 && rc[0].Name == "No Pain, No Gain", Text(rc));
            cap.List[0].Have = 19;
            var rc2 = Offer(cap, T(molotov, 4.6));
            Check("Q2", "Pyro_5 at 19: the Fire level itself would reach 20 - AVOID", rc2[0].Avoid, Text(rc2));
            Check("Q2", "Pyro_5: the row 'Explosive+Slashing+Fire under 20'", string.Join(" · ", cap.Row()) == "Explosive+Slashing+Fire under 20", string.Join(" · ", cap.Row()));
            // Pyro_RB: 4 types at 10 tags - a Research Pod that brings a type to 10 strongly
            var ten = new QuestRules { Quest = "GameHubQuest_Pyro_RB" };
            ten.List.Add(QuestRules.FromStatistic("InternalNumHashtagTypes10Plus", 3, 4, false));
            ten.List[0].Have = 1; ten.Points["Fire"] = 12; ten.Points["Kinetic"] = 8; ten.Points["Ice"] = 3;
            var kin = Card(QuestCardKind.Tags, null, "#Kinetic +2", 0, 0, -1, "Kinetic"); kin.Points = 2;
            var ice = Card(QuestCardKind.Tags, null, "#Ice +2", 0, 0, -1, "Ice"); ice.Points = 2;
            var fr = Card(QuestCardKind.Tags, null, "#Fire +4", 0, 0, -1, "Fire"); fr.Points = 4;
            var rt = Offer(ten, T(fr, 3.9), T(ice, 2.0), T(kin, 2.2));
            Check("Q2", "Pyro_RB: '#Kinetic +2' (8 -> 10) first, '4 types at 10 tags (1 now)'; '#Ice +2' +0.5; '#Fire +4' (12 already) untouched",
                rt[0].Name == "#Kinetic +2" && Wording.Quest(rt[0].Line) == "Quest: 4 types at 10 tags (1 now)" && Math.Abs(rt.First(x => x.Name == "#Ice +2").Now - 2.5) < 0.001 && rt.First(x => x.Name == "#Fire +4").Now == 3.9, Text(rt));
        }

        static void Items()
        {
            // Ranger_RB: no item at all (fails for good) - every item AVOID; Huntress_3 / Mechanic_1: every slot filled - items a little up
            var none = new QuestRules { Quest = "GameHubQuest_Ranger_RB" };
            none.List.Add(QuestRules.FromStatistic("InternalNumEquippedItems", 2, 0, true));
            var r = Offer(none, T(Card(QuestCardKind.Item, null, "Magazine Clip"), 3.46), T(Card(QuestCardKind.Item, null, "Potato"), 1.35));
            Check("Q2", "Ranger_RB: every item AVOID, 'Quest: taking any item fails it'; the row 'take no items'",
                r.All(x => x.Avoid && x.Now < 1) && Wording.Quest(r[0].Line) == "Quest: taking any item fails it" && string.Join(" · ", none.Row()) == "take no items", Text(r));
            none.Failed = true;
            var rf = Offer(none, T(Card(QuestCardKind.Item, null, "Magazine Clip"), 3.46));
            Check("Q2", "Ranger_RB once the game counts the quest as failed (an item taken): the advice as usual, no row", rf[0].Line == null && none.Row().Count == 0);
            var fill = new QuestRules { Quest = "GameHubQuest_Huntress_3" };
            fill.List.Add(QuestRules.FromStatistic("InternalNumFreeItemSlots", 1, 0, false)); fill.List[0].Have = 2;
            var rfi = Offer(fill, T(Card(QuestCardKind.Item, null, "Potato"), 1.35));
            Check("Q2", "Huntress_3: an item +0.3 ('fill every item slot (2 free)', log only); the row 'fill every item slot'",
                Math.Abs(rfi[0].Now - 1.65) < 0.001 && rfi[0].Line == "quest: fill every item slot (2 free)" && string.Join(" · ", fill.Row()) == "fill every item slot", Text(rfi));
            // Engineer_2: one of five items (the game's own target test scores them, +3) - the row names them
            var col = new QuestRules { Quest = "GameHubQuest_Engineer_2" };
            var cr = new QuestRule { Ask = QuestAsk.Items }; cr.Targets.AddRange(new[] { "Emerald Gem", "Sapphire Gem", "Ruby Gem", "Omnigeode", "Heavy Metal" }); col.List.Add(cr);
            var rc = Offer(col, T(Card(QuestCardKind.Item, null, "Ruby Gem"), 5.2));
            Check("Q2", "Engineer_2: the item is scored by the game's own target test (+3, Ranker.ItemScore), not twice; the row 'collect Emerald Gem or another'",
                rc[0].Now == 5.2 && rc[0].Line == null && string.Join(" · ", col.Row()) == "collect Emerald Gem or another");
        }

        static void Counts()
        {
            // Engineer_R1: 6 evolutions - abilities on the way to one a little up
            var evo = new QuestRules { Quest = "GameHubQuest_Engineer_R1" };
            evo.List.Add(QuestRules.FromStatistic("InternalNumAbilityEvolutions", 3, 6, false)); evo.List[0].Have = 2;
            var a = Card(QuestCardKind.Ability, "Engineer", "Electric Turret", 2, 4, -1, "Electric"); a.Evolvable = true;
            var b = Card(QuestCardKind.Ability, "Engineer", "EMP Grenade", 2, 4, -1, "Electric");
            var r = Offer(evo, T(b, 4.6), T(a, 4.4));
            Check("Q2", "Engineer_R1: an ability whose evolution is unlocked +0.5 (log only: 'evolutions (2 of 6)')", r[0].Name == "Electric Turret" && !r[0].Head && r[0].Line == "quest: evolutions (2 of 6)", Text(r));
            // Engineer_R2: 3 Rare or better trainings
            var rare = new QuestRules { Quest = "GameHubQuest_Engineer_R2" };
            rare.List.Add(QuestRules.FromStatistic("InternalNumRarePlusMilitaryTrainings", 3, 3, false)); rare.List[0].Have = 1;
            var rc = Card(QuestCardKind.Stat, null, "Luck"); rc.Rarity = "Rare";
            var cc = Card(QuestCardKind.Stat, null, "Weapon Damage"); cc.Rarity = "Common";
            var ec = Card(QuestCardKind.Stat, null, "Ability Size"); ec.Rarity = "Endless";
            var rr = Offer(rare, T(cc, 3.1), T(rc, 2.0), T(ec, 1.5));
            Check("Q2", "Engineer_R2: the Rare training first ('Rare or better training (1 of 3)'); Common and Endless untouched", rr[0].Name == "Luck" && Wording.Quest(rr[0].Line) == "Quest: Rare or better training (1 of 3)" && rr.Count(x => x.Line != null) == 1, Text(rr));
            // Tank_RB: armor to 150
            var arm = new QuestRules { Quest = "GameHubQuest_Tank_RB" };
            arm.List.Add(QuestRules.FromStatistic("TeamArmor", 3, 150, false)); arm.List[0].Have = 80;
            var ac = Card(QuestCardKind.Stat, null, "Armor"); ac.Armor = true;
            var ra = Offer(arm, T(cc, 3.1), T(ac, 2.2));
            Check("Q2", "Tank_RB: the armor training first ('armor to 150 (80 now)')", ra[0].Name == "Armor" && Wording.Quest(ra[0].Line) == "Quest: armor to 150 (80 now)", Text(ra));
            // Medic_R2: 6:00 at full health - health, armor and healing a little up
            var fh = new QuestRules { Quest = "GameHubQuest_Medic_R2" };
            fh.List.Add(new QuestRule { Ask = QuestAsk.FullHealth, Need = 360, Have = 130 });
            var heal = Card(QuestCardKind.Ability, "Medic", "Stimpack", 1, 4, -1); heal.Health = true;
            var rh = Offer(fh, T(heal, 4.0), T(Card(QuestCardKind.Ability, "Medic", "Experiment 21", 1, 4, -1, "Chemical"), 4.2));
            Check("Q2", "Medic_R2: a healing ability +0.4 (log only: 'time at full health (2:10 of 6:00)'); the row '6:00 at full health'",
                rh[0].Name == "Stimpack" && !rh[0].Head && rh[0].Line == "quest: time at full health (2:10 of 6:00)" && string.Join(" · ", fh.Row()) == "6:00 at full health", Text(rh));
        }

        static void FullTeam()
        {
            // Mechanic_R2: a full team, each with a tier-3 weapon at max level - every survivor's line lifted until it has one
            var rules = new QuestRules { Quest = "GameHubQuest_Mechanic_R2" };
            var w = new QuestRule { Ask = QuestAsk.WeaponLine, Depth = 2, AllBranches = true, Level = 4, Need = 3, Have = 1 }; w.Done.Add("Mechanic");
            rules.List.Add(w);
            var r = Offer(rules, T(Card(QuestCardKind.Weapon, "Tank", "Shotgun", 2, 4, 0, "Kinetic"), 3.5), T(Card(QuestCardKind.Weapon, "Mechanic", "Nitro-Gun", 2, 4, 2, "Ice"), 3.6),
                T(Card(QuestCardKind.Ability, "Tank", "Minefield", 1, 4, -1, "Explosive"), 4.5));
            Check("Q2", "Mechanic_R2: the Tank's weapon first ('leads to the tier-3 weapon (Level 3 of 4)'); the Mechanic's (done) untouched; the row 'a tier-3 weapon each (1 of 3)'",
                r[0].Name == "Shotgun" && Wording.Quest(r[0].Line) == "Quest: leads to the tier-3 weapon (Level 3 of 4)" && r.First(x => x.Name == "Nitro-Gun").Now == 3.6
                && string.Join(" · ", rules.Row()) == "a tier-3 weapon each (1 of 3)", Text(r));
        }

        // ---------------------------------------------------------------- Q3: the gates
        static void Gates()
        {
            var info = Heroic(); info.Steer = QuestSteer.InfoOnly;
            info.List[0].From = "Taser"; info.List[0].FromLevel = 1; info.List[0].FromDepth = 0;
            var r = Offer(info, T(Ab("Energy Shield", 0, 0), 4.93), T(Taser(1), 3.72));
            var t = r.First(x => x.Name == "Taser");
            Check("Q3", "InfoOnly: no card moves, the reason goes after the card's own ('... (info only)'), the QUEST row stays",
                r[0].Name == "Energy Shield" && t.Now == 3.72 && !t.Head && t.Line.EndsWith("(info only)") && info.Row().Count == 2, Text(r));
            info.TeamWords = "stay solo";
            Check("Q3", "InfoOnly: the team rule joins the QUEST row (the SOS row leaves it out then)", info.Row()[0] == "stay solo");
            var off = Heroic(); off.Steer = QuestSteer.Off;
            Check("Q3", "Off: nothing judged, no row", off.Judge(Taser(1), 3.72) == null && off.Row().Count == 0 && off.Said().Contains("QuestSteer Off"));
            var any = Heroic(); any.AnyOf = true;
            var failed = Heroic(); failed.Failed = true;
            var away = Heroic(); away.NotThisRun = "this run does not fit the quest's conditions (arena, mode, difficulty or leader) - the quest cannot complete this run";
            Check("Q3", "objectives that match Any, a quest the game counts as failed, a run that does not fit: the advice as usual",
                any.Judge(Taser(1), 3.72) == null && failed.Judge(Taser(1), 3.72) == null && away.Judge(Taser(1), 3.72) == null && any.Row().Count == 0 && away.Row().Count == 0);
            // the team rule follows the switch too (QuestTeam.Muted)
            var team = new QuestTeam { Quest = "GameHubQuest_Huntress_4" }; team.Survivors(2, 1.0); team.Muted = "the quest advice is off ([Advice] QuestSteer)";
            var v = team.Judge(new List<string> { "Huntress" });
            Check("Q3", "the team rule is not followed under InfoOnly / Off (QuestTeam.Muted): the rescue cards as usual", !v.Active && v.Broken == team.Muted && !team.Rules);
            team.Muted = null;
            Check("Q3", "... and followed again under On", team.Judge(new List<string> { "Huntress" }).Active);
            // AVOID over a lift on one card
            var both = new QuestRules();
            var bw = new QuestRule { Ask = QuestAsk.WeaponLine, Class = "Pyro", Depth = 2, Level = 4, AllBranches = true }; bw.Targets.Add("Flamethrower");
            both.List.Add(bw); both.List.Add(new QuestRule { Ask = QuestAsk.NoTier3 });
            var vb = both.Judge(Card(QuestCardKind.NextTier, "Pyro", "Flamethrower", 0, 4, 2, "Fire"), 6.9);
            Check("Q3", "two rules on one card: AVOID (a pick that fails the quest) wins over a lift", vb != null && vb.Avoid && vb.Score < 1);
        }

        // ---------------------------------------------------------------- Q4: rails and sources
        static void Rails()
        {
            // every line a card could say, in every made-up state, through Wording.Quest within the card rails
            var more = new List<string>();
            var w = new QuestRule { Ask = QuestAsk.WeaponLine, Class = "Engineer", Depth = 2, Level = 4, AllBranches = true };
            var one = new QuestRule { Ask = QuestAsk.WeaponLine, Class = "Mechanic", Depth = 2, Level = 3 }; one.Targets.AddRange(new[] { "Chaos Engine", "Nitro-Gun" });
            foreach (var r in new[] { w, one })
            {
                var q = new QuestRules(); q.List.Add(r);
                for (int lvl = 0; lvl < 4; lvl++) { var v = q.Judge(Card(QuestCardKind.Weapon, r.Class, "Wrench", lvl, 4, 0), 3); if (v != null) more.Add(v.Line); }
                foreach (var k in new[] { QuestCardKind.NextTier, QuestCardKind.Branch })
                    foreach (var n in new[] { "Chaos Engine", "Chainsaw", "Electric Turret: Tesla Coil" })
                    { var v = q.Judge(Card(k, r.Class, n, 0, 4, 2), 3); if (v != null) more.Add(v.Line); var v1 = q.Judge(Card(k, r.Class, n, 0, 4, 1), 3); if (v1 != null) more.Add(v1.Line); }
            }
            foreach (var cls in new[] { "Engineer", "Mechanic", "Huntress", "Ghost", "Ranger" })
            {
                var q = new QuestRules();
                q.List.Add(new QuestRule { Ask = QuestAsk.NoClassAbility, Class = cls });
                q.List.Add(new QuestRule { Ask = QuestAsk.FullClass, Class = cls, Have = 12, Need = 15 });
                var s = new QuestRule { Ask = QuestAsk.Synergies, Class = cls, Have = 1, Need = 2 }; q.List.Add(s);
                var kc = Card(QuestCardKind.Ability, cls, "X", 1, 4, -1, "Kinetic"); var v = q.Judge(kc, 3); if (v != null) more.Add(v.Line);
                var q2 = new QuestRules(); q2.List.Add(new QuestRule { Ask = QuestAsk.FullClass, Class = cls, Have = 12, Need = 15 });
                var sk = Card(QuestCardKind.Ability, cls, "X", 0, 4, -1); sk.Skipped = true; v = q2.Judge(sk, 3); if (v != null) more.Add(v.Line);
                var q3 = new QuestRules(); q3.List.Add(s); var rc = Card(QuestCardKind.Recruit, "Tank", "Tank"); rc.Synergy.Add(cls); v = q3.Judge(rc, 3); if (v != null) more.Add(v.Line);
                var e = new QuestRule { Ask = QuestAsk.Evolution, Class = cls }; e.Targets.Add("Remote Control Car");
                var q4 = new QuestRules(); q4.List.Add(e);
                var ev = Card(QuestCardKind.Evolution, cls, "Remote Control Car: Nitro", 0, 1); ev.Base = "Remote Control Car"; v = q4.Judge(ev, 8); if (v != null) more.Add(v.Line);
            }
            foreach (var tags in new[] { "TeamHashtagExplosiveSlashingFireNum", "TeamHashtagToxicNum", "TeamHashtagKineticNum" })
                foreach (int cmp in new[] { 0, 3 })
                {
                    var q = new QuestRules(); var r = QuestRules.FromStatistic(tags, cmp, 100, false); r.Have = 97; q.List.Add(r);
                    var pod = Card(QuestCardKind.Tags, null, "#", 0, 0, -1, r.Targets[0]); pod.Points = 4;
                    var v = q.Judge(pod, 2); if (v != null) more.Add(v.Line);
                }
            foreach (var need in new[] { 1, 2, 5 }) { var q = new QuestRules(); q.List.Add(new QuestRule { Ask = QuestAsk.HealthItems, Need = need, Have = 0 }); var c = Card(QuestCardKind.Item, null, "MedKit"); c.Health = true; more.Add(q.Judge(c, 1).Line); }
            var all = _lines.Concat(more).Distinct().ToList();
            var bad = new List<string>();
            foreach (var l in all)
            {
                string shown = Wording.Quest(l);
                var rails = Wording.Rails(shown);
                if (rails.Count > 0 || !shown.StartsWith("Quest: ", StringComparison.Ordinal)) bad.Add("'" + shown + "' (" + string.Join(", ", rails) + ")");
            }
            Check("Q4", "every quest line a card can say stays within the card rails as 'Quest: ...' (" + all.Count + " lines)", bad.Count == 0, string.Join("; ", bad));
            if (Environment.GetEnvironmentVariable("YAZS_BENCH_ALLWORDS") == "1") foreach (var l in all) Console.WriteLine("      " + Wording.Quest(l));
            // the card says the quest's line first, whatever its kind
            var cw = new CardWords { Kind = SayKind.Weapon, Level = 1, Max = 4, Style = BuildStyle.Ability, Quest = "quest: max the tier-3 weapon (Level 2 of 4)" };
            Check("Q4", "Wording.Card says the quest's line first (CardWords.Quest), before the card's own words", Wording.Card(cw, 1, "Taser", null) == "Quest: max the tier-3 weapon (Level 2 of 4)");
            var rows = new List<string>();
            foreach (QuestAsk ask in Enum.GetValues(typeof(QuestAsk)))
            {
                var r = new QuestRule { Ask = ask, Class = "Engineer", Need = 3, Have = 1, Level = 4, Depth = 2, AllBranches = true, From = "Tesla", FromLevel = 2, FromMax = 4, FromDepth = 1 };
                r.Targets.Add("Electric");
                string t = QuestRules.RowText(r);
                if (t == null || t.Length > 40 || t.IndexOf('<') >= 0) rows.Add(ask + ": '" + t + "'");
            }
            Check("Q4", "every rule has a QUEST row text of 40 characters at most", rows.Count == 0, string.Join("; ", rows));
        }

        static void Sources()
        {
            string quest = Src("Quest.cs"), ranker = Src("Ranker.cs"), plan = Src("Plan.cs"), menu = Src("Menu.cs"), plugin = Src("Plugin.cs"), state = Src("GameState.cs"), team = Src("QuestTeam.cs"), rules = Src("QuestRules.cs");
            string proj = File.Exists(Proj()) ? File.ReadAllText(Proj()) : "";
            Check("Q4", "Quest.cs reads the objectives' runtimes and what they count (_objectiveRuntimes, definition, CurrentValue / TargetValue / IsFulfilled, CurrentFloatValue)",
                quest != null && quest.Contains("q._objectiveRuntimes") && quest.Contains("rt.definition") && quest.Contains("rt.CurrentValue") && quest.Contains("rt.TargetValue") && quest.Contains("rt.IsFulfilled") && quest.Contains("CurrentFloatValue"));
            Check("Q4", "Quest.cs decodes every objective kind the cards follow (one accessor each, inside a try)",
                quest != null && new[] { "GameHubQuestObjectiveFinishWithWeapons", "GameHubQuestObjectiveFinishWithAbilities", "GameHubQuestObjectiveFinishWithEvolutions", "GameHubQuestObjectiveFinishWithoutClassAbilities",
                    "GameHubQuestObjectiveFinishWithoutTier3Weapons", "GameHubQuestObjectiveFullyUpgradeClass", "GameHubQuestObjectiveClassActiveSynergies", "GameHubQuestObjectiveHealingItemCount", "GameHubQuestObjectiveCollectItem",
                    "GameHubQuestObjectiveGameStatisticThreshold", "GameHubQuestObjectiveFullHealthTime", "GameHubQuestObjectiveFullTeamTier3Weapons", "IsHealthRelatedItem" }.All(quest.Contains));
            Check("Q4", "B2: no objective the cards follow is logged 'advice unchanged' any more (the Read line says what each does)", quest != null && quest.Contains("\" -> \" + QuestRules.Does(r)") && quest.Contains("\"[quest] \" + (rules.Quest.Length > 0"));
            Check("Q4", "Ranker: the quest pass after the weapon cap and before the order (QuestPass), the items in ItemScore, the readout's weapon and ability scores under the quest",
                ranker != null && ranker.IndexOf("UnderOwnAbility(cards)", StringComparison.Ordinal) < ranker.IndexOf("QuestPass(cards, s)", StringComparison.Ordinal)
                && ranker.IndexOf("QuestPass(cards, s)", StringComparison.Ordinal) < ranker.IndexOf("cards.OrderByDescending", StringComparison.Ordinal)
                && ranker.Contains("score = QuestItem(it, facts, s, score, why)") && ranker.Contains("var v = QuestOf(c, s);") && ranker.Contains("var qv = QuestOf(c, s);"));
            Check("Q4", "Ranker: the game's own quest item (+3) follows the switch", ranker != null && ranker.Contains("steer == QuestSteer.Off ? null : s.ActiveQuests") && ranker.Contains("\"quest target (info only)\""));
            Check("Q4", "the PLAN readout's QUEST row (QuestRules.Row), names lent by other mods", plan != null && plan.Contains("Add(\"run\", \"quest\", \"QUEST\"") && plan.Contains("r.Row(2)") && plan.Contains("Names.Text(x)"));
            Check("Q4", "the snapshot asks once (Snapshot.Rules), the team rule follows the switch (QuestTeam.Muted)", state != null && state.Contains("YazsCompanion.Quest.Rules(this)") && team != null && team.Contains("Muted == null && HasRule"));
            Check("Q4", "[Advice] QuestSteer, On by default, and its ADVICE-tab row (SAVED comes with every [Advice] change)",
                plugin != null && plugin.Contains("Config.Bind(\"Advice\", \"QuestSteer\", QuestSteer.On") && menu != null && menu.Contains("\"ad:quest\"") && menu.Contains("eleven rows"));
            Check("Q4", "QuestRules.cs is compiled into the bench (pure: no game types)", proj.Contains("QuestRules.cs") && rules != null && !rules.Contains("Il2Cpp") && !rules.Contains("UnityEngine"));
        }
    }
}
