// 0.16.0 (C16-05, C16-08, C16-17): a stat card weighed by its own value against the Common card of its stat (Synergy.RarityWeight /
// Ranker.OwnRatio), the Research Pod by fit, the ties and the WHY's tie words, the evolution card's 'closes' clause. Every case is
// data-free: registered in Checks.Run (after TreeCases) AND in Verdict.DataFree, so the public CI runs it.
// Game names only (the repository is public).
//   C16-05a  RarityWeight: Rare / Legendary by the card's own ratio, held to 1 - 4 / 1 - 6, 2 / 3 when unreadable
//   C16-05b  the 19 stat cards of 1.0.2 (sharedassets1.assets): every ratio inside its rarity's band, weighed as itself
//   C16-05c  six logged military offers with a Rare or Legendary card replayed: one first card changes (logged offer 1)
//   C16-05d  Ranker reads GetMyRarityBonusList; the stale "a Legendary three times" comment is gone
//   C16-08a  P1 - P6: a Research Pod card's 10-tag effect scaled by the squad's fit (Tags.Score)
//   C16-08b  T1 - T10: the ties (CardTies) - the build's order, the stat card's own value, the tag points, the position
//   C16-08c  W1 - W7: the WHY band on an exact tie ("Either works - even with X; this one is ...")
//   C16-17   the PLAN's preferred evolution and the card's "closes X" from one rule (Synergy.Preferred)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class RankSmall
    {
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string F(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        static string F3(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }
        static string Src(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod", file));
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }

        public static int Run()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.16.0: C16-05 stat cards by their own value, C16-08 the Research Pod by fit and the ties");
            OwnWeights();
            AssetRatios();
            StatReplays();
            StatSources();
            Pods();
            Ties();
            TieSources();
            TieWords();
            Closes();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        // ================================================================ C16-05: stat cards by their own value
        static void OwnWeights()
        {
            Func<string, double, double> rw = Synergy.RarityWeight;
            var unreadable = new[] { double.NaN, 0, -1, double.PositiveInfinity };
            Check("C16-05a", "Rare: its own ratio (2.5 -> 2.5, 1.875 -> 1.875), held to 1 - 4 (0.5 -> 1, 9 -> 4)",
                rw("Rare", 2.5) == 2.5 && rw("Rare", 1.875) == 1.875 && rw("Rare", 0.5) == 1.0 && rw("Rare", 9) == 4.0,
                "2.5 -> " + F(rw("Rare", 2.5)) + ", 1.875 -> " + F3(rw("Rare", 1.875)) + ", 0.5 -> " + F(rw("Rare", 0.5)) + ", 9 -> " + F(rw("Rare", 9)));
            Check("C16-05a", "Rare unreadable (NaN, 0, -1, infinite) -> 2 (the 0.15.0 factor)", unreadable.All(x => rw("Rare", x) == 2.0),
                string.Join(", ", unreadable.Select(x => F(rw("Rare", x)))));
            Check("C16-05a", "Legendary: its own ratio (5 -> 5, 3.333 -> 3.333), held to 1 - 6 (0.5 -> 1, 20 -> 6)",
                rw("Legendary", 5) == 5.0 && rw("Legendary", 3.333) == 3.333 && rw("Legendary", 0.5) == 1.0 && rw("Legendary", 20) == 6.0,
                "5 -> " + F(rw("Legendary", 5)) + ", 3.333 -> " + F3(rw("Legendary", 3.333)) + ", 0.5 -> " + F(rw("Legendary", 0.5)) + ", 20 -> " + F(rw("Legendary", 20)));
            Check("C16-05a", "Legendary unreadable (NaN, 0, -1, infinite) -> 3 (the 0.15.0 factor)", unreadable.All(x => rw("Legendary", x) == 3.0),
                string.Join(", ", unreadable.Select(x => F(rw("Legendary", x)))));
            Check("C16-05a", "Common 1 and any other rarity 1, whatever the ratio (5 -> 1)", rw("Common", 5) == 1.0 && rw("Unknown", 5) == 1.0);
        }

        // the stat cards of 1.0.2 (sharedassets1.assets): one bonus per list, Common / Rare / Legendary / Endless
        sealed class StatRow { public string Stat, Card; public float C, R, L, E; public bool Team; }
        static readonly StatRow[] Table =
        {
            new StatRow { Stat = "AbilityCooldown",   Card = "Chem-Light Battery", C = 0.1f,  R = 0.25f, L = 0.5f,  E = 0.05f },
            new StatRow { Stat = "AbilityCritChance", Card = "UAV System",         C = 0.1f,  R = 0.25f, L = 0.5f,  E = 0.05f },
            new StatRow { Stat = "AbilityCritDamage", Card = "Siege Engines",      C = 0.3f,  R = 0.6f,  L = 1.0f,  E = 0.15f },
            new StatRow { Stat = "AbilityDamage",     Card = "Master Blaster",     C = 0.1f,  R = 0.2f,  L = 0.4f,  E = 0.05f },
            new StatRow { Stat = "AbilityDuration",   Card = "Energy Awareness",   C = 0.1f,  R = 0.25f, L = 0.5f,  E = 0.05f },
            new StatRow { Stat = "AbilitySize",       Card = "Zone of Action",     C = 0.1f,  R = 0.2f,  L = 0.4f,  E = 0.025f },
            new StatRow { Stat = "Armor",             Card = "Kevlar Plating",     C = 10f,   R = 20f,   L = 40f,   E = 5f,     Team = true },
            new StatRow { Stat = "DodgeChance",       Card = "Evasive Maneuvers",  C = 0.04f, R = 0.08f, L = 0.15f, E = 0.01f,  Team = true },
            new StatRow { Stat = "HPRegen",           Card = "First Aid",          C = 3f,    R = 6f,    L = 10f,   E = 1f,     Team = true },
            new StatRow { Stat = "Luck",              Card = "Dear John",          C = 5f,    R = 10f,   L = 20f,   E = 2f,     Team = true },
            new StatRow { Stat = "MagnetRange",       Card = "Magnet Mount",       C = 0.24f, R = 0.5f,  L = 1.0f,  E = 0.12f,  Team = true },
            new StatRow { Stat = "MaxHealth",         Card = "Snake Eater",        C = 80f,   R = 150f,  L = 300f,  E = 40f,    Team = true },
            new StatRow { Stat = "MovementSpeedMod",  Card = "Fast Mover",         C = 5f,    R = 10f,   L = 25f,   E = 2f,     Team = true },
            new StatRow { Stat = "WeaponCooldownMod", Card = "Sleight Of Hand",    C = 0.1f,  R = 0.2f,  L = 0.3f,  E = 0.05f },
            new StatRow { Stat = "WeaponCritChance",  Card = "Armor Piercing",     C = 0.05f, R = 0.1f,  L = 0.25f, E = 0.025f },
            new StatRow { Stat = "WeaponCritDamage",  Card = "Stopping Power",     C = 0.2f,  R = 0.4f,  L = 0.8f,  E = 0.1f },
            new StatRow { Stat = "WeaponDamageMod",   Card = "Max Pain",           C = 0.1f,  R = 0.2f,  L = 0.4f,  E = 0.05f },
            new StatRow { Stat = "WeaponFireRateMod", Card = "Rapid Fire",         C = 0.1f,  R = 0.2f,  L = 0.3f,  E = 0.05f },
            new StatRow { Stat = "XPModifierMod",     Card = "Operation Planning", C = 0.1f,  R = 0.2f,  L = 0.4f,  E = 0.05f,  Team = true },
        };

        // the ratio exactly as Ranker.OwnRatio forms it: float division, then widened (0.5f / 0.24f = 2.0833332538604736, not 0.5 / 0.24)
        static double Ratio(float own, float common) { return Math.Abs(own) / Math.Abs(common); }
        static double RatioOf(StatRow s, string rarity)
        {
            switch (rarity)
            {
                case "Rare": return Ratio(s.R, s.C);
                case "Legendary": return Ratio(s.L, s.C);
                case "Endless": return Ratio(s.E, s.C);
                default: return double.NaN;          // a Common card: the mod reads no ratio
            }
        }

        static void AssetRatios()
        {
            const double eps = 1e-6;
            var rare = Table.Select(s => RatioOf(s, "Rare")).ToList();
            var leg = Table.Select(s => RatioOf(s, "Legendary")).ToList();
            var end = Table.Select(s => RatioOf(s, "Endless")).ToList();
            var rareOff = Table.Where(s => { double r = RatioOf(s, "Rare"); return !(Math.Abs(Synergy.RarityWeight("Rare", r) - r) < 1e-9 && r >= 1.875 - eps && r <= 2.5 + eps); }).Select(s => s.Card).ToList();
            var legOff = Table.Where(s => { double r = RatioOf(s, "Legendary"); return !(Math.Abs(Synergy.RarityWeight("Legendary", r) - r) < 1e-9 && r >= 3.0 - eps && r <= 5.0 + eps); }).Select(s => s.Card).ToList();
            var endOff = Table.Where(s => { double r = RatioOf(s, "Endless"); return !(Synergy.RarityWeight("Endless", r) == Synergy.EndlessWeight(r) && r >= 0.25 - eps && r <= 0.5 + eps); }).Select(s => s.Card).ToList();
            Check("C16-05b", "the 19 Rare cards weigh their own ratio, 1.875 - 2.5 (Snake Eater 150 / 80 the least; Chem-Light Battery, UAV System, Energy Awareness 2.5)",
                Table.Length == 19 && rareOff.Count == 0, F3(rare.Min()) + " - " + F3(rare.Max()) + (rareOff.Count > 0 ? "; off: " + string.Join(", ", rareOff) : "") + "; Magnet Mount " + RatioOf(Table[10], "Rare").ToString("R", CultureInfo.InvariantCulture));
            Check("C16-05b", "the 19 Legendary cards weigh their own ratio, 3 - 5 (Sleight Of Hand, Rapid Fire 3; five cards 5)",
                legOff.Count == 0, F3(leg.Min()) + " - " + F3(leg.Max()) + "; at 5: " + string.Join(", ", Table.Where(s => Math.Abs(RatioOf(s, "Legendary") - 5) < eps).Select(s => s.Card)) + (legOff.Count > 0 ? "; off: " + string.Join(", ", legOff) : ""));
            Check("C16-05b", "the 19 Endless cards keep the 0.14.0 rule (EndlessWeight), 0.25 - 0.5",
                endOff.Count == 0, F3(end.Min()) + " - " + F3(end.Max()) + (endOff.Count > 0 ? "; off: " + string.Join(", ", endOff) : ""));
        }

        // a logged card: its rarity, the score 0.15.0 logged and the one wanted now (Common cards keep theirs)
        sealed class Logged { public string Card, Rarity; public double Score, Want; }
        sealed class StatOffer { public string At; public Logged[] Cards; public string[] Order; }
        static Logged L(string card, string rarity, double score, double want = double.NaN) { return new Logged { Card = card, Rarity = rarity, Score = score, Want = double.IsNaN(want) ? score : want }; }

        static void StatReplays()
        {
            var offers = new[]
            {
                new StatOffer { At = "logged offer 1 (0.12.0)", Cards = new[] { L("Chem-Light Battery", "Rare", 4.75, 5.69), L("Master Blaster", "Common", 3.09), L("Fast Mover", "Common", 2.10), L("Dear John", "Rare", 4.80, 4.80) },
                    Order = new[] { "Chem-Light Battery", "Dear John", "Master Blaster", "Fast Mover" } },
                new StatOffer { At = "logged offer 2 (0.12.0)", Cards = new[] { L("UAV System", "Common", 3.27), L("Fast Mover", "Common", 2.14), L("Chem-Light Battery", "Legendary", 7.26, 11.43), L("Siege Engines", "Common", 3.13) },
                    Order = new[] { "Chem-Light Battery", "UAV System", "Siege Engines", "Fast Mover" } },
                new StatOffer { At = "logged offer 3 (0.11.0)", Cards = new[] { L("Energy Awareness", "Common", 2.39), L("Master Blaster", "Rare", 5.65, 5.65), L("Snake Eater", "Legendary", 4.70, 5.575), L("Max Pain", "Common", 2.68) },
                    Order = new[] { "Master Blaster", "Snake Eater", "Max Pain", "Energy Awareness" } },
                new StatOffer { At = "logged offer 4 (0.13.0)", Cards = new[] { L("Kevlar Plating", "Common", 2.26), L("Snake Eater", "Legendary", 4.66, 5.525), L("UAV System", "Common", 3.52), L("Rapid Fire", "Legendary", 6.33, 6.33) },
                    Order = new[] { "Rapid Fire", "Snake Eater", "UAV System", "Kevlar Plating" } },
                new StatOffer { At = "logged offer 5 (0.13.0)", Cards = new[] { L("Siege Engines", "Common", 3.03), L("Evasive Maneuvers", "Common", 2.20), L("Max Pain", "Legendary", 6.66, 8.55), L("Sleight Of Hand", "Common", 2.60) },
                    Order = new[] { "Max Pain", "Siege Engines", "Sleight Of Hand", "Evasive Maneuvers" } },
                new StatOffer { At = "logged offer 6 (0.13.0)", Cards = new[] { L("Fast Mover", "Legendary", 3.90, 5.70), L("Chem-Light Battery", "Common", 3.51), L("Max Pain", "Common", 3.01), L("Energy Awareness", "Common", 2.67) },
                    Order = new[] { "Fast Mover", "Chem-Light Battery", "Max Pain", "Energy Awareness" } },
            };
            int changed = 0;
            foreach (var o in offers)
            {
                var now = new List<KeyValuePair<string, double>>(); bool near = true; var said = new List<string>();
                foreach (var c in o.Cards)
                {
                    var row = Table.First(s => s.Card == c.Card);
                    double old = c.Rarity == "Legendary" ? 3.0 : c.Rarity == "Rare" ? 2.0 : 1.0;
                    double w = (c.Score - 1.0 - (row.Team ? 0.2 : 0)) / (2.0 * old);            // the stat weight the logged score was made of
                    double ratio = RatioOf(row, c.Rarity);
                    double v = Synergy.StatCard(Synergy.RarityWeight(c.Rarity, ratio), w, row.Team);
                    if (Math.Abs(v - c.Want) > 0.011) near = false;
                    now.Add(new KeyValuePair<string, double>(c.Card, v));
                    said.Add(c.Card + " " + c.Rarity + (row.Team ? " team" : "") + " " + F(c.Score) + (c.Rarity != "Common" ? " -> " + F(v) : ""));
                }
                string wasFirst = o.Cards.OrderByDescending(c => c.Score).First().Card;
                var order = now.Select((kv, i) => new { kv, i }).OrderByDescending(x => Math.Round(x.kv.Value, 2)).ThenBy(x => x.i).Select(x => x.kv.Key).ToList();
                if (order[0] != wasFirst) changed++;
                Check("C16-05c", o.At + ": " + (order[0] != wasFirst ? order[0] + " first now (was " + wasFirst + ")" : order[0] + " stays first"),
                    near && order.SequenceEqual(o.Order), string.Join(", ", said));
            }
            Check("C16-05c", "one first card changes in the six offers (a Rare +25 % ability cooldown over a Rare +10 luck)", changed == 1, changed + " changed");
            // the Common card and the 0.14.0 rule beside them: the formula of the other rarities is the same
            Check("C16-05c", "a Common card keeps its score (Master Blaster 3.09), an Endless one its 0.14.0 weight (Zone of Action x0.25)",
                Math.Round(Synergy.StatCard(Synergy.RarityWeight("Common", double.NaN), (3.09 - 1.0) / 2.0, false), 2) == 3.09 && Synergy.RarityWeight("Endless", RatioOf(Table[5], "Endless")) == Synergy.EndlessWeight(RatioOf(Table[5], "Endless")));
        }

        static void StatSources()
        {
            string ranker = Src("Ranker.cs"), synergy = Src("Synergy.cs");
            Check("C16-05d", "Ranker reads the list the game applies (GetMyRarityBonusList()) for Rare, Legendary and Endless cards",
                ranker != null && ranker.Contains("GetMyRarityBonusList()") && ranker.Contains("OwnRatio(b, rarity, stat) : double.NaN") && ranker.Contains("(rarity == \"Rare\" || rarity == \"Legendary\" || rarity == \"Endless\") && b != null"));
            Check("C16-05d", "the stale comment and the 0.14.0 reader are gone ('a Legendary three times', 'Ability Area 10 / 20 / 30', 'EndlessRatio(')",
                ranker != null && !ranker.Contains("a Legendary three times") && !ranker.Contains("Ability Area 10 / 20 / 30") && !ranker.Contains("EndlessRatio("));
            Check("C16-05d", "the file header names the 0.16.0 rule; the [rank] line per rarity and stat, with the field's fallback said",
                ranker != null && ranker.Contains("at every rarity (0.16.0, C16-05: GetMyRarityBonusList") && ranker.Contains("\" stat cards: weight x\"") && ranker.Contains("_ratioSaid.Add(rarity + \"/\"")
                && ranker.Contains("- read from the rarity's field") && ranker.Contains("the card's values not read"));
            Check("C16-05d", "Synergy.RarityWeight takes the card's ratio for Rare / Legendary (OwnWeight)", synergy != null && synergy.Contains("case \"Legendary\": return OwnWeight(ratio, 3.0, 1.0, 6.0);") && synergy.Contains("case \"Rare\": return OwnWeight(ratio, 2.0, 1.0, 4.0);"));
        }

        // ================================================================ C16-08a: the Research Pod by fit
        static TagProfile Profile(int specialAt) { return new TagProfile { SpecialAt = specialAt }; }
        static double Pod(string type, int n, TagProfile p, List<string> why = null) { return Math.Round(Tags.Score(type, n, p, why ?? new List<string>()), 2); }

        static void Pods()
        {
            // P1: a squad dealing Kinetic alone
            var p1 = Profile(10); p1.Source("Shotgun", 1.0, new[] { "Kinetic" }); p1.Points["Kinetic"] = 12;
            var why1 = new List<string>(); double fire1 = Pod("Fire", 10, p1, why1);
            Check("C16-08a", "P1 '#Fire +10' for a type nobody deals: 2.50 (was 4.00); its reasons unchanged",
                fire1 == 2.50 && why1.Count > 0 && why1[0] == "reaches the Fire special effect (10)" && why1.Contains("nothing on the squad deals Fire"), F(fire1) + " - " + string.Join("; ", why1));
            Check("C16-08a", "P1 '#Kinetic +10' (the stacked type, past its effect) 6.00 unchanged", Pod("Kinetic", 10, p1) == 6.00, F(Pod("Kinetic", 10, p1)));
            // P2: a full fit keeps the full +1.5
            var p2 = Profile(10); p2.Source("Shotgun", 1.0, new[] { "Kinetic" });
            Check("C16-08a", "P2 '#Kinetic +10' at a full fit reaching its effect: 6.50 unchanged", Pod("Kinetic", 10, p2) == 6.50, F(Pod("Kinetic", 10, p2)));
            // P3: a logged reroll (shares rounded, the sources folded by type)
            var p3 = Profile(10);
            p3.Source("Kinetic source", 0.45, new[] { "Kinetic" }); p3.Source("Electric source", 0.43, new[] { "Electric" }); p3.Source("Chemical source", 0.12, new[] { "Chemical" });
            p3.Points["Kinetic"] = 18; p3.Points["Electric"] = 17; p3.Points["Chemical"] = 1;
            double chem3 = Pod("Chemical", 10, p3), elec3 = Pod("Electric", 10, p3), fire3 = Pod("Fire", 10, p3), ice3 = Pod("Ice", 10, p3);
            Check("C16-08a", "P3 a logged reroll: Chemical +10 (12 %) 3.27 (was 4.48), Electric +10 4.22, Fire / Ice +10 2.50 (were 4.00): Electric now over Chemical",
                chem3 == 3.27 && elec3 == 4.22 && fire3 == 2.50 && ice3 == 2.50 && elec3 > chem3, "Chemical " + F(chem3) + ", Electric " + F(elec3) + ", Fire " + F(fire3) + ", Ice " + F(ice3) + " (logged: Chemical 4.46 over Electric 4.25)");
            // P4: a logged offer (the damage types of data\probe.json)
            var p4 = Profile(10);
            p4.Source("Explosive Arrows", 0.45, new[] { "Kinetic", "Explosive" }); p4.Source("Arrow Rain: Thunderstruck", 0.22, new[] { "Kinetic", "Electric" });
            p4.Source("Bear Trap: Fire", 0.22, new[] { "Slashing", "Fire" }); p4.Source("Zombie Decoy", 0.11, new[] { "Explosive" });
            p4.Points["Kinetic"] = 19; p4.Points["Explosive"] = 10; p4.Points["Slashing"] = 5; p4.Points["Fire"] = 1; p4.Points["Electric"] = 1;
            double sl4 = Pod("Slashing", 10, p4), fi4 = Pod("Fire", 10, p4), el4 = Pod("Electric", 10, p4), ex4 = Pod("Explosive", 10, p4), ki4 = Pod("Kinetic", 10, p4), ch4 = Pod("Chemical", 10, p4);
            Check("C16-08a", "P4 a logged offer: Slashing / Fire / Electric +10 (22 %) 3.91 (were 4.88), Explosive 4.74, Kinetic 6.00, Chemical 2.50: Explosive now over every special",
                sl4 == 3.91 && fi4 == 3.91 && el4 == 3.91 && ex4 == 4.74 && ki4 == 6.00 && ch4 == 2.50 && ex4 > sl4,
                "Slashing " + F(sl4) + ", Fire " + F(fi4) + ", Electric " + F(el4) + ", Explosive " + F(ex4) + ", Kinetic " + F(ki4) + ", Chemical " + F(ch4) + " (logged: Slashing 4.87 = Fire 4.87 over Explosive 4.76)");
            // P5: from 62.5 % of the squad's damage the full +1.5
            var p5 = Profile(10); p5.Source("Source A", 0.625, new[] { "Fire" }); p5.Source("Source B", 0.375, new[] { "Kinetic" }); p5.Points["Fire"] = 5;
            Check("C16-08a", "P5 '#Fire +5' at 62.5 % of the squad (fit 1): 1 + 0.75 + 2.5 + 1.5 + 1.0 (stacked) = 6.75 as in 0.15.0", Pod("Fire", 5, p5) == 6.75, F(Pod("Fire", 5, p5)));
            // P6: the Pod's reroll floor
            var p6 = Profile(10); p6.Source("Shotgun", 1.0, new[] { "Kinetic" }); p6.Points["Kinetic"] = 12; p6.Points["Fire"] = 5;
            double f5 = Pod("Fire", 5, p6), f7 = Pod("Fire", 7, p6);
            Check("C16-08a", "P6 '#Fire +5' reaching the effect of a type nobody deals: 1.75, under the Pod's REROLL floor; '#Fire +7' 2.05 stays over it",
                f5 == 1.75 && f5 < ScreenCall.PodFloor && f7 == 2.05 && f7 >= ScreenCall.PodFloor, F(f5) + " / " + F(f7) + " against " + F(ScreenCall.PodFloor));
            Check("C16-08a", "the bonus is a constant at 1.5 (Tags.SpecialBonus)", Tags.SpecialBonus == 1.5);
        }

        // ================================================================ C16-08b: the ties
        static TieKey K(int buildRank = int.MaxValue, int tags = 0) { return new TieKey { BuildRank = buildRank, TagPoints = tags }; }
        static TieKey Stat(double value) { return new TieKey { Stat = true, Value = value }; }
        static TieKey PodKey(int points, bool spread = false) { return new TieKey { Pod = true, Spread = spread, TagPoints = points }; }
        static string Ord(List<int> o) { return "[" + string.Join(", ", o) + "]"; }

        static void Ties()
        {
            // T1: a logged Tank run (index order Minefield, Bombing Strike, Fury Unleashed)
            TieKey mf = K(1, 2), bs = K(0, 2), fu = K(3);
            var o1 = CardTies.Order(new[] { 4.81, 4.81, 4.68 }, new[] { mf, bs, fu });
            Check("C16-08b", "T1 a logged Tank run: Bombing Strike (#1 in the build) before Minefield (#2) at 4.81 (up to 0.15.0 Minefield, further left)",
                o1.SequenceEqual(new[] { 1, 0, 2 }) && CardTies.Rule(bs, mf) == "the build's order (#1 before #2)" && CardTies.Say(bs, mf) == "higher in the build's order",
                Ord(o1) + " - " + CardTies.Rule(bs, mf) + " / '" + CardTies.Say(bs, mf) + "'");
            // T2: two owners (a logged offer)
            TieKey el = K(2), mi = K(1);
            var o2 = CardTies.Order(new[] { 5.00, 5.00 }, new[] { el, mi });
            Check("C16-08b", "T2 a logged offer, two owners: Minefield (#2 of its build) before Electrocution (#3 of its own)",
                o2.SequenceEqual(new[] { 1, 0 }) && CardTies.Rule(mi, el) == "the build's order (#2 before #3)", Ord(o2) + " - " + CardTies.Rule(mi, el));
            // T3: a logged Research Pod (index order Chemical, Electric, Fire, Slashing)
            var pods = new[] { PodKey(0), PodKey(1), PodKey(1), PodKey(5) };
            var o3 = CardTies.Order(new[] { 4.00, 4.87, 4.87, 4.87 }, pods);
            var o3a = CardTies.Order(new[] { 2.50, 3.91, 3.91, 3.91 }, pods);
            Check("C16-08b", "T3 a logged Research Pod: Slashing (5 tags) before Electric (1) and Fire (1, by position); the same with C16-08a's scores",
                o3.SequenceEqual(new[] { 3, 1, 2, 0 }) && o3a.SequenceEqual(o3) && CardTies.Rule(pods[3], pods[1]) == "tag points (5 before 1)" && CardTies.Say(pods[3], pods[1]) == "the type with more tags",
                Ord(o3) + " / " + Ord(o3a) + " - " + CardTies.Rule(pods[3], pods[1]) + " / '" + CardTies.Say(pods[3], pods[1]) + "'");
            // T4: the same cards under the "Spread" tag plan (the points negated)
            var spread = new[] { PodKey(0, true), PodKey(-1, true), PodKey(-1, true), PodKey(-5, true) };
            var o4 = CardTies.Order(new[] { 4.00, 4.87, 4.87, 4.87 }, spread);
            Check("C16-08b", "T4 Spread: Electric, Fire (by position), Slashing, Chemical - fewer tags first",
                o4.SequenceEqual(new[] { 1, 2, 3, 0 }) && CardTies.Rule(spread[1], spread[2]) == null && CardTies.Say(spread[1], spread[2]) == null
                && CardTies.Rule(spread[1], spread[3]) == "tag points (1 before 5, Spread)" && CardTies.Say(spread[1], spread[3]) == "the type with fewer tags",
                Ord(o4) + " - " + (CardTies.Rule(spread[1], spread[2]) ?? "the card further left") + " / " + CardTies.Rule(spread[1], spread[3]) + " / '" + CardTies.Say(spread[1], spread[3]) + "'");
            // T5: two stat cards (a logged offer)
            TieKey dj = Stat(1.00), fm = Stat(2.00);
            var o5 = CardTies.Order(new[] { 3.00, 3.00 }, new[] { dj, fm });
            Check("C16-08b", "T5 a logged offer: the Rare Fast Mover (x2.00) before the Common Dear John (x1.00) at 3.00",
                o5.SequenceEqual(new[] { 1, 0 }) && CardTies.Rule(fm, dj) == "its own value (x2.00 before x1.00)" && CardTies.Say(fm, dj) == "the bigger bonus",
                Ord(o5) + " - " + CardTies.Rule(fm, dj) + " / '" + CardTies.Say(fm, dj) + "'");
            // T6: two weapons with the same tags (a logged offer; Kinetic 11 points): the position
            var o6 = CardTies.Order(new[] { 6.30, 4.45, 4.63, 6.30 }, new[] { K(int.MaxValue, 11), K(2), K(1), K(int.MaxValue, 11) });
            Check("C16-08b", "T6 a logged offer: Shotgun and Multishot (11 Kinetic tags each) at 6.30 - the card further left",
                o6.SequenceEqual(new[] { 0, 3, 2, 1 }) && CardTies.Rule(K(int.MaxValue, 11), K(int.MaxValue, 11)) == null, Ord(o6));
            // T7: one base's two evolutions (a logged offer): the squad's points in each one's types, as Ranker.TagPointsOf sums them
            var p7 = Profile(10); p7.Points["Kinetic"] = 19; p7.Points["Explosive"] = 10; p7.Points["Slashing"] = 4; p7.Points["Electric"] = 1;
            int fire = CardTies.TagPoints(new[] { "Fire", "Slashing" }, p7), arrows = CardTies.TagPoints(new[] { "Ice", "Slashing" }, p7);
            TieKey bf = K(2, fire), ba = K(2, arrows);
            var o7 = CardTies.Order(new[] { 7.78, 7.78, 5.92 }, new[] { bf, ba, Stat(0.5) });
            Check("C16-08b", "T7 a logged offer: Bear Trap: Fire and Bear Trap: Arrows (4 tag points each, no Fire or Ice points) - by position, as logged",
                fire == 4 && arrows == 4 && o7.SequenceEqual(new[] { 0, 1, 2 }) && CardTies.Rule(bf, ba) == null, Ord(o7) + " - points " + fire + " / " + arrows);
            Check("C16-08b", "T7 the tag points: each damage type once, case aside, none for no profile",
                CardTies.TagPoints(new[] { "Slashing", "slashing", "Kinetic" }, p7) == 23 && CardTies.TagPoints(new[] { "Kinetic" }, null) == 0 && CardTies.TagPoints(null, p7) == 0 && CardTies.TagPoints(new[] { "", null, "Kinetic" }, p7) == 19);
            // T8: the key's basics, and a total preorder over a sweep
            bool basics = CardTies.Compare(null, TieKey.None) == 0 && CardTies.Compare(null, new TieKey()) == 0 && CardTies.Compare(K(3), new TieKey()) < 0 && CardTies.Compare(new TieKey(), K(3)) > 0;
            var a = K(1, 4); basics &= CardTies.Compare(a, a) == 0;
            Check("C16-08b", "T8 a null key is TieKey.None; an in-build #4 before none; a key ties itself", basics);
            var rnd = new Random(1607);
            int[] ranks = { 0, 1, 2, 3, int.MaxValue };
            double[] values = { 0.25, 0.4, 1.0, 2.0, 2.5, 5.0 };
            var keys = new List<TieKey>();
            for (int i = 0; i < 50; i++)
            {
                bool stat = rnd.Next(2) == 0;
                keys.Add(new TieKey { BuildRank = ranks[rnd.Next(ranks.Length)], Stat = stat, Value = stat ? values[rnd.Next(values.Length)] : 1.0, TagPoints = rnd.Next(-6, 13) });
            }
            int anti = 0, trans = 0, triples = 0;
            foreach (var x in keys) foreach (var y in keys) if (Math.Sign(CardTies.Compare(x, y)) != -Math.Sign(CardTies.Compare(y, x))) anti++;
            foreach (var x in keys) foreach (var y in keys) foreach (var z in keys)
            {
                if (CardTies.Compare(x, y) <= 0 && CardTies.Compare(y, z) <= 0) { triples++; if (CardTies.Compare(x, z) > 0) trans++; }
            }
            Check("C16-08b", "T8 50 keys (Random 1607): the order is antisymmetric for every pair and transitive for every triple", anti == 0 && trans == 0,
                anti + " antisymmetry breaks, " + trans + " transitivity breaks in " + triples + " ordered triples");
            // T10: the level-up's Endless filler
            TieKey filler = Stat(0.25), weapon = K(int.MaxValue, 0), common = Stat(1.00), tagged = K(int.MaxValue, 3);
            var o10 = CardTies.Order(new[] { 3.40, 3.40 }, new[] { filler, weapon });
            Check("C16-08b", "T10 the Endless filler (x0.25) loses an exact tie to a weapon; the WHY band says nothing of it",
                o10.SequenceEqual(new[] { 1, 0 }) && CardTies.Rule(weapon, filler) == "the stat card's own value (x0.25 against a Common card's x1.00)" && CardTies.Say(weapon, filler) == null,
                Ord(o10) + " - " + CardTies.Rule(weapon, filler));
            Check("C16-08b", "T10 a Common stat card ties a weapon without tags (the position), loses to one with tags ('backed by more of your tags')",
                CardTies.Compare(common, weapon) == 0 && CardTies.Compare(tagged, common) < 0 && CardTies.Order(new[] { 2.5, 2.5 }, new[] { common, tagged }).SequenceEqual(new[] { 1, 0 }) && CardTies.Say(tagged, common) == "backed by more of your tags",
                "'" + CardTies.Say(tagged, common) + "'");
            // the rescue screen keeps its own rules
            var tier = new Recruit { Name = "Tank", Tier = "S", Class = 5 }; var other = new Recruit { Name = "SWAT", Tier = "A", Bought = 2, Class = 1 };
            Check("C16-08b", "the rescue screen's order is Recruit.Ties' (the guides' tier first), untouched", Recruit.Ties(tier, other) < 0 && Recruit.Ties(other, tier) > 0);
        }

        static void TieSources()
        {
            string ranker = Src("Ranker.cs"), plan = Src("Plan.cs");
            Check("C16-08b", "T9 Ranker: the cards sort by CardTies, the PLAN's TopAbility compares CardTies too, the tie is logged, a stat card's key is its own value",
                ranker != null && ranker.Contains("CardTies.Comparer") && ranker.Contains("CardTies.Compare(c.Tie") && ranker.Contains("tie for #1 at") && ranker.Contains("Stat = true, Value = Math.Round(r, 2)")
                && ranker.Contains("ThenBy(c => c, RecruitTies)"));
            Check("C16-08b", "T9 Plan.NextAbility: to the hundredth, CardTies on a tie", plan != null && plan.Contains("CardTies.Compare(") && plan.Contains("Math.Round(v.Score, 2)"));
            Check("C16-08b", "T9 every scored kind sets its key (weapon, ability, evolution, stat card, Research Pod)",
                ranker != null && Regex.Matches(ranker, @"c\.Tie = new TieKey \{").Count == 5 && ranker.Contains("TagPoints = (spread ? -1 : 1) * (s.Tags != null ? s.Tags.PointsOf(type) : 0)"));
        }

        // ================================================================ C16-08c: the WHY band on an exact tie
        const string Higher = "higher in the build's order";
        static WhyBlock Band(string name, int rank, double score, string other, double otherScore, string tieSay, Func<string, string> lend = null)
        {
            var x = new WhyIn { Name = name, Rank = rank, Score = score, Shown = "x", TieSay = tieSay, Lend = lend };
            if (rank <= 1) { x.SecondName = other; x.SecondScore = otherScore; }
            else { x.FirstName = other; x.FirstScore = otherScore; }
            return WhyText.Block(x);
        }
        static string Side(string name, int rank, double score, string other, double otherScore, string tieSay, Func<string, string> lend = null)
        {
            var x = new WhyIn { Name = name, Rank = rank, Score = score, TieSay = tieSay, Lend = lend };
            if (rank <= 1) { x.SecondName = other; x.SecondScore = otherScore; }
            else { x.FirstName = other; x.FirstScore = otherScore; }
            bool close; string side; WhyText.Versus(x, out close, out side);
            return side;
        }
        static string Pad(int n) { var sb = new System.Text.StringBuilder(); for (int i = 0; i < n; i++) sb.Append((char)('a' + i % 26)); return sb.ToString(); }

        static void TieWords()
        {
            var w1 = Band("Bombing Strike", 1, 4.81, "Minefield", 4.81, Higher); string s1 = Side("Bombing Strike", 1, 4.81, "Minefield", 4.81, Higher);
            Check("C16-08c", "W1 #1 on the very same score: 'Either works - even with Minefield; this one is higher in the build's order' (75), CLOSE CALL",
                w1.Versus == "Either works - even with Minefield; this one is higher in the build's order" && w1.Close && w1.Lead == "CLOSE CALL" && s1 == "Either works - this one is higher in the build's order",
                "'" + w1.Versus + "' (" + (w1.Versus ?? "").Length + ") | side '" + s1 + "' (" + (s1 ?? "").Length + ")");
            var w2 = Band("Minefield", 2, 4.81, "Bombing Strike", 4.81, Higher); string s2 = Side("Minefield", 2, 4.81, "Bombing Strike", 4.81, Higher);
            Check("C16-08c", "W2 #2: 'Either works - even with Bombing Strike, which is higher in the build's order' (77)",
                w2.Versus == "Either works - even with Bombing Strike, which is higher in the build's order" && w2.Close && w2.Lead == "CLOSE CALL" && s2 == "Either works - Bombing Strike is higher in the build's order",
                "'" + w2.Versus + "' (" + (w2.Versus ?? "").Length + ") | side '" + s2 + "' (" + (s2 ?? "").Length + ")");
            var w3a = Band("Bombing Strike", 1, 4.81, "Minefield", 4.81, null); var w3b = Band("Minefield", 2, 4.81, "Bombing Strike", 4.81, null);
            string s3a = Side("Bombing Strike", 1, 4.81, "Minefield", 4.81, null);
            Check("C16-08c", "W3 no rule (the position decided): 'Either works - even with Minefield' / 'Either works - even with Bombing Strike'",
                w3a.Versus == "Either works - even with Minefield" && s3a == "Either works - even with Minefield" && w3b.Versus == "Either works - even with Bombing Strike" && w3a.Close && w3b.Close,
                "'" + w3a.Versus + "' | '" + w3b.Versus + "'");
            // W4: names another mod lends (here: made-up suffixes - only their length matters)
            Func<string, string> deluxe = n => n == "Minefield" ? "Minefield Deluxe" : n;
            var w4a = Band("Bombing Strike", 1, 4.81, "Minefield", 4.81, Higher, deluxe);
            Check("C16-08c", "W4a a 16-character name: the middle form keeps the rule ('...; higher in the build's order', 70)",
                w4a.Versus == "Either works - even with Minefield Deluxe; higher in the build's order", "'" + w4a.Versus + "' (" + (w4a.Versus ?? "").Length + ")");
            Func<string, string> long19 = n => n + Pad(19);
            var w4b1 = Band("Bombing Strike", 1, 4.81, "Minefield", 4.81, Higher, long19); var w4b2 = Band("Minefield", 2, 4.81, "Bombing Strike", 4.81, Higher, long19);
            Check("C16-08c", "W4b 19 letters longer: #1 'Either works - even with <28 characters>' (53), #2 'Either works - <33 characters> is higher in the build's order' (79)",
                w4b1.Versus == "Either works - even with Minefield" + Pad(19) && w4b2.Versus == "Either works - Bombing Strike" + Pad(19) + " is higher in the build's order",
                "'" + w4b1.Versus + "' (" + (w4b1.Versus ?? "").Length + ") | '" + w4b2.Versus + "' (" + (w4b2.Versus ?? "").Length + ")");
            int over = 0, rails = 0, lost = 0, sides = 0;
            for (int n = 0; n <= 40; n++)
            {
                Func<string, string> lend = s => s + Pad(n);
                string one = "Bombing Strike" + Pad(n), two = "Minefield" + Pad(n);
                var r1 = Band("Bombing Strike", 1, 4.81, "Minefield", 4.81, Higher, lend); var r2 = Band("Minefield", 2, 4.81, "Bombing Strike", 4.81, Higher, lend);
                string d1 = Side("Bombing Strike", 1, 4.81, "Minefield", 4.81, Higher, lend), d2 = Side("Minefield", 2, 4.81, "Bombing Strike", 4.81, Higher, lend);
                foreach (var v in new[] { r1.Versus, r2.Versus }) { if (v == null || v.Length > WhyText.VersusBudget) over++; else if (!WhyText.Fits(v, WhyText.VersusBudget)) rails++; }
                foreach (var v in new[] { d1, d2 }) if (v == null || v.Length > WhyText.Budget || !WhyText.Fits(v, WhyText.Budget)) sides++;
                bool fits1 = new[] { "Either works - even with " + two + "; this one is " + Higher, "Either works - even with " + two + "; " + Higher }.Any(f => f.Length <= WhyText.VersusBudget);
                bool fits2 = new[] { "Either works - even with " + one + ", which is " + Higher, "Either works - " + one + " is " + Higher }.Any(f => f.Length <= WhyText.VersusBudget);
                if (r1.Versus != null && r1.Versus.Contains(Higher) != fits1) lost++;
                if (r2.Versus != null && r2.Versus.Contains(Higher) != fits2) lost++;
            }
            Check("C16-08c", "W4c names 0 - 40 letters longer: every Versus within 80 and the rails, every side within 64, the rule kept whenever a form with it fits",
                over == 0 && rails == 0 && sides == 0 && lost == 0, over + " over 80, " + rails + " off the rails, " + sides + " sides off, " + lost + " rules lost or misplaced");
            var w5 = Band("#Fire +10", 3, 4.87, "#Slashing +10", 4.87, "the type with more tags");
            Check("C16-08c", "W5 a three-way tie's #3: 'Either works - even with #Slashing +10, which is the type with more tags' (72), CLOSE CALL",
                w5.Versus == "Either works - even with #Slashing +10, which is the type with more tags" && w5.Close && w5.Lead == "CLOSE CALL", "'" + w5.Versus + "' (" + (w5.Versus ?? "").Length + ")");
            var w6 = Band("Medic", 1, 5.07, "Pyro", 5.07, null);
            Check("C16-08c", "W6 a rescue-screen tie (Recruit.Ties decides, no words): 'Either works - even with Pyro'", w6.Versus == "Either works - even with Pyro" && w6.Close, "'" + w6.Versus + "'");
            var w7a = Band("Medical Drone", 1, 4.75, "Experiment 21", 4.72, null); var w7b = Band("Experiment 21", 2, 4.72, "Medical Drone", 4.75, null);
            var w7c = Band("Magazine Clip", 1, 0.40, "Potato", 0.40, null); var w7d = Band("Molotov Cocktail", 3, 4.66, "Medical Drone", 4.75, null);
            Check("C16-08c", "W7 a close call keeps 'a hair ahead' (4.75 / 4.72); 0.40 / 0.40 reads 'Either works - even with Potato'; a #3 not tied is no close call",
                w7a.Versus == "Either works - a hair ahead of Experiment 21" && w7b.Versus == "Either works - Medical Drone is a hair ahead" && w7c.Versus == "Either works - even with Potato" && !w7d.Close && w7d.Lead == "WHY",
                "'" + w7a.Versus + "' | '" + w7b.Versus + "' | '" + w7c.Versus + "' | '" + w7d.Versus + "'");
        }

        // ================================================================ C16-17: the evolution card's "closes X"
        static void Closes()
        {
            Check("C16-17", "Synergy.Preferred: (0.62, 0.31) +1, (0.31, 0.62) -1, (0.40, 0.20) 0 - a toss-up under 0.25, (0.50, 0.20) +1",
                Synergy.Preferred(0.62, 0.31) == 1 && Synergy.Preferred(0.31, 0.62) == -1 && Synergy.Preferred(0.40, 0.20) == 0 && Synergy.Preferred(0.50, 0.20) == 1 && Synergy.EvolutionTossUp == 0.25);
            // the fits the PLAN and the card compare: Helicopter Strike's two evolutions (data\probe.json's damage types)
            var heli = new PowerFacts { Name = "Helicopter Strike", IsAbility = true }.Deals("Kinetic").Tagged("Deployable");
            var chem = new PowerFacts { Name = "Helicopter Strike: Chemtrails", IsAbility = true }.Deals("Kinetic", "Chemical").Tagged("Grenade", "Deployable", "Projectile");
            var gun = new PowerFacts { Name = "Helicopter Strike: Gunner", IsAbility = true }.Deals("Kinetic").Tagged("Deployable");
            var mixed = new TagProfile(); mixed.Source("Kinetic source", 0.6, new[] { "Kinetic" }); mixed.Source("Chemical source", 0.4, new[] { "Chemical" });
            double fc = Synergy.EvolutionFit(chem, heli, mixed, null, null, null), fg = Synergy.EvolutionFit(gun, heli, mixed, null, null, null);
            var kin = new TagProfile(); kin.Source("Kinetic source", 1.0, new[] { "Kinetic" });
            double kc = Synergy.EvolutionFit(chem, heli, kin, null, null, null), kg = Synergy.EvolutionFit(gun, heli, kin, null, null, null);
            Check("C16-17", "one rule both ways: with Chemical on the squad the PLAN prefers Chemtrails (A) and Gunner's card closes it; Kinetic alone a toss-up - neither",
                Synergy.Preferred(fc, fg) == 1 && Synergy.Preferred(fc, fg) == -Synergy.Preferred(fg, fc) && Synergy.Preferred(kc, kg) == 0 && Synergy.Preferred(kg, kc) == 0,
                "fits " + F(fc) + " vs " + F(fg) + "; Kinetic alone " + F(kc) + " vs " + F(kg));
            Func<CardWords, WhyBlock> band = w => WhyText.Block(new WhyIn { Name = "Helicopter Strike: Gunner", Rank = 1, Score = 8.34, Shown = "Evolves the build's main ability", Say = w });
            Func<string, string, CardWords> evo = (closes, pick) => new CardWords { Kind = SayKind.Evolution, Base = "Helicopter Strike", Build = "Rifleman", BasePriority = 0, Closes = closes, Pick = pick };
            var b1 = band(evo("Helicopter Strike: Chemtrails", null));
            Check("C16-17", "the WHY band: 'Closes Chemtrails - the PLAN's preferred one' first (the card's own title names the base)",
                b1.Reasons.Count > 0 && b1.Reasons[0] == "Closes Chemtrails - the PLAN's preferred one", string.Join(" | ", b1.Reasons));
            var b2 = band(evo(null, null));
            var b3 = band(evo("Helicopter Strike: Chemtrails", "Helicopter Strike: Chemtrails"));
            Check("C16-17", "no 'closes' with Closes unset, nor beside a build's pick (the line says 'the build takes ... instead')",
                !b2.Items.Any(i => i.IndexOf("closes", StringComparison.OrdinalIgnoreCase) >= 0) && !b3.Items.Any(i => i.IndexOf("closes", StringComparison.OrdinalIgnoreCase) >= 0),
                string.Join(" | ", b2.Reasons) + " || " + string.Join(" | ", b3.Reasons));
            Func<string, string> lend = n => n.Replace("Chemtrails", "Chemtrails" + Pad(30));
            var b4 = WhyText.Block(new WhyIn { Name = "Helicopter Strike: Gunner", Rank = 1, Score = 8.34, Shown = "Evolves the build's main ability", Say = evo("Helicopter Strike: Chemtrails", null), Lend = lend });
            Check("C16-17", "a lent name too long for the band: 'Closes the PLAN's preferred evolution'; both forms within the rails",
                b4.Reasons.Count > 0 && b4.Reasons[0] == "Closes the PLAN's preferred evolution" && WhyText.Fits("Closes Chemtrails - the PLAN's preferred one") && WhyText.Fits("Closes the PLAN's preferred evolution"),
                b4.Reasons.Count > 0 ? b4.Reasons[0] : "none");
            Check("C16-17", "the log's reason is not translated into a second band item", WhyText.FromLog("closes Helicopter Strike: Chemtrails (the PLAN's preferred evolution, fit 1.15 vs 0.58)") == null);
            string ranker = Src("Ranker.cs"), plan = Src("Plan.cs");
            Check("C16-17", "Plan.PickEvolution asks Synergy.Preferred (its own 0.25 test gone)", plan != null && plan.Contains("Synergy.Preferred(a, b)") && !plan.Contains("Math.Abs(a - b) < 0.25"));
            string evoSrc = null;
            if (ranker != null)
            {
                int from = ranker.IndexOf("static void ScoreEvolution(", StringComparison.Ordinal), to = from < 0 ? -1 : ranker.IndexOf("static bool EvolutionOffered(", from, StringComparison.Ordinal);
                if (from >= 0 && to > from) evoSrc = ranker.Substring(from, to - from);
            }
            var elseAt = evoSrc == null ? null : Regex.Match(evoSrc, @"\r?\n\s*else\r?\n\s*\{");
            int pickAt = evoSrc == null ? -1 : evoSrc.IndexOf("if (pick != null)", StringComparison.Ordinal), closesAt = evoSrc == null ? -1 : evoSrc.IndexOf("say.Closes = G.Name(other)", StringComparison.Ordinal);
            Check("C16-17", "Ranker.ScoreEvolution sets say.Closes only in the no-pick branch, from Synergy.Preferred(fo, fit); the score untouched there",
                evoSrc != null && elseAt != null && elseAt.Success && pickAt >= 0 && elseAt.Index > pickAt && closesAt > elseAt.Index && Regex.Matches(evoSrc, @"say\.Closes =").Count == 1
                && evoSrc.Contains("Synergy.Preferred(fo, fit) > 0") && !Regex.IsMatch(evoSrc.Substring(elseAt.Index), @"c\.Score\s*[+\-*]?="));
        }
    }
}
