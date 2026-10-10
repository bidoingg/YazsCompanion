// 0.16.0 (C16-07): the Training Yard's badge steering (TreePlan.ClassSteps with YardBadges, held and whole badge steps): YB0 the
// 0.15 identity (YB0a inline, YB0b against yard_015_expected.txt), the model's cases, a logged Medic replay, the rails, the sources.
// RunDataFree runs in the full bench (from Run) and in --no-data (Verdict.DataFree); Run adds the cases over data\gamedata.json
// (printing '(no gamedata.json: skipped)' without it).
//   YB0a  a made-up tree (generic names): ClassSteps without badges = the 0.15 steps and advice, captured before the edit (STEP 0)
//   YB0b  the 9 survivor trees of gamedata.json at the badge levels of Profile = yard_015_expected.txt (STEP 0)
//   YB1   a tab of three badges equipped / never equipped (data-free advices, Normal I, 9 survivors): whole chunks, the held badge
//   YB2   a tab with a badge that pays only at level 3 (+ YB2b one survivor, YB2c Extermination III, YB2d a pinned badge)
//   YB3   Simulate's two passes on hand-made steps: a held badge waits, a whole chunk is never partly bought, the leftover still flows
//   YB4   a logged Medic replay (probe facts, guides, Normal I and II): Healing Badge no longer bought with the leftover point
//   YB4b  DK-X01 (the Deck bought Glacier Badge 0>1 while SWAT's loadout listed it under 'never'): the plan buys it whole to 2
//   YB5   the approximation against a full re-advice (the oracle), YB6 the rails of every badge reason, YB7 the sources,
//   YB8   the cost (printed), YB9 the advice cache's key
// Every golden was measured by the reviser's scratch model of the spec (research\roadmap_1007\scratch\c16_yard\revise_out.txt), numbers
// within 0.005. Game names only (the repository is public).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class YardBadgeCases
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static int _bad;
        static void Check(string label, bool ok, string detail = "")
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok   " : "BAD  ") + label + (detail.Length > 0 ? ": " + detail : ""));
        }

        /// <summary>The TreeUi advice line after "[yard] &lt;tree&gt;: N points; " (TreeUi.cs).</summary>
        internal static string Say(TAdvice a)
        {
            return "buy " + (a.Now.Count == 0 ? "nothing" : string.Join(", ", a.Now.Select(b => b.Order + " " + b.Label + " (" + b.Cost + ")")))
                + (a.SaveFor != null ? "; save for " + a.SaveFor.Label + " (" + a.SaveFor.Cost + ")" : "") + (a.Later.Count > 0 ? "; later " + string.Join(", ", a.Later.Select(b => b.Label)) : "");
        }

        static readonly int[] Budgets = { 0, 1, 2, 3, 5, 8, 13, 40 };
        // the reviser printed two decimals: within half a unit of the last digit (a value on the .xx5 edge rounds either way)
        static bool Near(double a, double b) { return Math.Abs(a - b) <= 0.005 + 1e-6; }
        static string F2(double x) { return x.ToString("0.00", IC); }

        static string Src(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod", file));
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }

        // ------------------------------------------------------------------------------------------------ YB0a: a made-up tree
        static TNode N(string key, string name, TKind kind, int rank, int slot, int min, int max, int[] costs)
        {
            return new TNode { Key = "Test_" + key, Name = name, Kind = kind, Rank = rank, Slot = slot, Min = min, Max = max, Level = min, Costs = costs };
        }

        /// <summary>A survivor tree with generic names: every kind ClassSteps orders, every node at its minimum, every rank open.</summary>
        internal static List<TNode> MadeUpTree()
        {
            int[] five = { 1, 2, 3, 4, 5 };
            var a1 = N("Ability_1", "Ability One", TKind.Ability, 1, 0, 1, 5, five); a1.Tier = "S";
            var a2 = N("Ability_2", "Ability Two", TKind.Ability, 1, 1, 1, 5, five);
            var w0 = N("Weapon_1", "Weapon One", TKind.Weapon, 1, 2, 1, 3, new[] { 1, 2, 3 }); w0.WeaponDepth = 0;
            var w1 = N("Weapon_2", "Weapon Two", TKind.Weapon, 1, 3, 1, 4, new[] { 1, 2, 3, 4 }); w1.WeaponDepth = 1;
            var e1 = N("Evolution_1", "Evolution One", TKind.Evolution, 2, 0, 0, 1, new[] { 5 }); e1.Prereqs.Add(a1.Key);
            var e2 = N("Evolution_2", "Evolution Two", TKind.Evolution, 2, 1, 0, 1, new[] { 5 }); e2.Prereqs.Add(a2.Key);
            var f1 = N("Weapon_3", "Weapon Three", TKind.Weapon, 2, 2, 0, 5, five); f1.WeaponDepth = 2;
            var f2 = N("Weapon_4", "Weapon Four", TKind.Weapon, 2, 3, 0, 5, five); f2.WeaponDepth = 2; f2.GuideBranch = true;
            var b2 = N("BadgeBoost_2", "Badge Two", TKind.Badge, 2, 4, 0, 5, five);
            var s2 = N("Synergy_2", "Synergy Two", TKind.Synergy, 2, 5, 0, 1, new[] { 5 });
            var a3 = N("Ability_3", "Ability Three", TKind.Ability, 3, 0, 0, 5, five); a3.Tier = "A";
            var f3 = N("Weapon_5", "Weapon Five", TKind.Weapon, 3, 1, 0, 5, five); f3.WeaponDepth = 2;
            var b3 = N("BadgeBoost_3", "Badge Three", TKind.Badge, 3, 2, 0, 5, five);
            var e3 = N("Evolution_3", "Evolution Three", TKind.Evolution, 4, 0, 0, 1, new[] { 5 }); e3.Prereqs.Add(a3.Key);
            var b4 = N("BadgeBoost_4", "Badge Four", TKind.Badge, 4, 1, 0, 5, five);
            var s4 = N("Synergy_4", "Synergy Four", TKind.Synergy, 4, 2, 0, 1, new[] { 5 });
            var p1 = N("Passive_1", "Passive One", TKind.Passive, 5, 0, 0, 3, new[] { 1, 3, 5 }); p1.Desc = "+10% weapon damage";
            var p2 = N("Passive_2", "Passive Two", TKind.Passive, 5, 1, 0, 3, new[] { 1, 3, 5 }); p2.Desc = "-5% cooldown";
            var s5 = N("Synergy_5", "Synergy Five", TKind.Synergy, 5, 2, 0, 1, new[] { 5 });
            return new List<TNode> { a1, a2, w0, w1, e1, e2, f1, f2, b2, s2, a3, f3, b3, e3, b4, s4, p1, p2, s5 };
        }

        static List<string> PlanLines(List<TNode> nodes, List<TStep> steps, string prefix)
        {
            var o = new List<string>();
            for (int i = 0; i < steps.Count; i++) o.Add(prefix + "step " + (i + 1) + "/" + steps.Count + " " + steps[i].Node.Name + " to " + steps[i].To + ": " + steps[i].Why);
            foreach (int p in Budgets) { var a = TreePlan.Advise(nodes, steps, p); o.Add(prefix + p + " pts: " + Say(a) + " | left " + a.Left); }
            return o;
        }

        // captured from the 0.15.0 TreePlan before the edit (STEP 0, the bench's print)
        static readonly string[] Yb0aWant =
        {
            "step 1/31 Ability One to 2: Cheap damage and cooldown on a starting ability (S tier).",
            "step 2/31 Ability Two to 2: Cheap damage and cooldown on a starting ability.",
            "step 3/31 Weapon Two to 2: The second starting weapon carries the early run.",
            "step 4/31 Ability One to 3: ",
            "step 5/31 Ability Two to 3: ",
            "step 6/31 Weapon One to 3: Levels of the first two weapons are cheap and speed up the start.",
            "step 7/31 Weapon Two to 4: Levels of the first two weapons are cheap and speed up the start.",
            "step 8/31 Evolution One to 1: Unlocks both evolutions of Ability One: the biggest spike in the tree.",
            "step 9/31 Evolution Two to 1: Unlocks both evolutions of Ability Two: the biggest spike in the tree.",
            "step 10/31 Weapon Four to 3: The guides' weapon branch for this survivor.",
            "step 11/31 Ability One to 5: Max the starting abilities (S tier).",
            "step 12/31 Ability Two to 5: Max the starting abilities.",
            "step 13/31 Weapon Four to 5: ",
            "step 14/31 Ability Three to 3: Rank III ability (A tier).",
            "step 15/31 Badge Two to 1: Badge Two: only matters if you equip it.",
            "step 16/31 Synergy Two to 1: Pays off when both survivors are on the squad.",
            "step 17/31 Ability Three to 5: ",
            "step 18/31 Badge Three to 1: Badge Three: only matters if you equip it.",
            "step 19/31 Evolution Three to 1: Unlocks both evolutions of Ability Three.",
            "step 20/31 Badge Four to 1: Badge Four: only matters if you equip it.",
            "step 21/31 Synergy Four to 1: Pays off when both survivors are on the squad.",
            "step 22/31 Passive One to 1: Rank V passive: always on.",
            "step 23/31 Passive Two to 1: Rank V passive: always on.",
            "step 24/31 Passive One to 3: Passives scale to level 3 (1 + 3 + 5 points).",
            "step 25/31 Passive Two to 3: Passives scale to level 3 (1 + 3 + 5 points).",
            "step 26/31 Synergy Five to 1: Pays off when both survivors are on the squad.",
            "step 27/31 Weapon Three to 5: The other branches of the fork, for the runs that offer them.",
            "step 28/31 Weapon Five to 5: The other branches of the fork, for the runs that offer them.",
            "step 29/31 Badge Two to 5: ",
            "step 30/31 Badge Three to 5: ",
            "step 31/31 Badge Four to 5: ",
            "0 pts: buy nothing; save for Ability One 1>2 (2); later Ability Two 1>2, Weapon Two 1>2, Weapon One 1>3 | left 0",
            "1 pts: buy 1 Weapon Four 0>1 (1); save for Ability One 1>2 (2); later Ability Two 1>2, Weapon Two 1>2, Weapon One 1>3 | left 0",
            "2 pts: buy 1 Ability One 1>2 (2); later Ability Two 1>2, Weapon Two 1>2, Ability One 2>3 | left 0",
            "3 pts: buy 1 Ability One 1>2 (2), 2 Weapon Four 0>1 (1); save for Ability Two 1>2 (2); later Weapon Two 1>2, Ability One 2>3, Weapon One 1>3 | left 0",
            "5 pts: buy 1 Ability One 1>2 (2), 2 Ability Two 1>2 (2), 3 Weapon Four 0>1 (1); save for Weapon Two 1>2 (2); later Ability One 2>3, Ability Two 2>3, Weapon One 1>3 | left 0",
            "8 pts: buy 1 Ability One 1>2 (2), 2 Ability Two 1>2 (2), 3 Weapon Two 1>2 (2), 4 Weapon One 1>2 (2); save for Ability One 2>3 (3); later Ability Two 2>3, Weapon One 2>3, Weapon Two 2>4 | left 0",
            "13 pts: buy 1 Ability One 1>3 (5), 2 Ability Two 1>3 (5), 3 Weapon Two 1>2 (2), 4 Weapon Four 0>1 (1); save for Weapon One 1>2 (2); later Weapon Two 2>4, Evolution One 0, Evolution Two 0 | left 0",
            "40 pts: buy 1 Ability One 1>3 (5), 2 Ability Two 1>3 (5), 3 Weapon Two 1>4 (9), 4 Weapon One 1>3 (5), 5 Evolution One 0 (5), 6 Evolution Two 0 (5), 7 Weapon Four 0>3 (6); later Ability One 3>5, Ability Two 3>5, Weapon Four 3>5 | left 0",
        };

        static string FirstDiff(IList<string> got, IList<string> want)
        {
            for (int i = 0; i < Math.Max(got.Count, want.Count); i++)
            {
                string g = i < got.Count ? got[i] : "(none)", w = i < want.Count ? want[i] : "(none)";
                if (g != w) return "line " + (i + 1) + ": got '" + g + "', want '" + w + "'";
            }
            return "";
        }

        static void Yb0a()
        {
            var nodes = MadeUpTree();
            var steps = TreePlan.ClassSteps(nodes);
            var got = PlanLines(nodes, steps, "");
            string diff = FirstDiff(got, Yb0aWant);
            Check("YB0a a made-up tree, no badge advice: the 0.15 steps and texts, and the 0.15 advice for 0 / 1 / 2 / 3 / 5 / 8 / 13 / 40 points (" + got.Count + " lines)",
                diff.Length == 0, diff);
            var nullSteps = TreePlan.ClassSteps(nodes, null);
            bool same = nullSteps.Count == steps.Count && nullSteps.Zip(steps, (x, y) => x.Node == y.Node && x.To == y.To && x.Why == y.Why && x.Hold == y.Hold && x.Whole == y.Whole).All(z => z);
            Check("YB0a the three '<badge>: only matters if you equip it.' texts are there, no step is held or whole, ClassSteps(nodes, null) is the same list",
                steps.Count(s => s.Why.EndsWith(": only matters if you equip it.", StringComparison.Ordinal)) == 3 && !steps.Any(s => s.Hold || s.Whole) && same);
        }

        // ------------------------------------------------------------------------------------------------ the helpers of the model cases
        // a mid-game badge profile (levels 0 - 2; the levels yard_015_expected.txt was captured at); 29 / 31 / 32 in ranks not reached
        static readonly Dictionary<int, int> Profile = new Dictionary<int, int>
        {
            { 0, 2 }, { 1, 2 }, { 2, 1 }, { 3, 1 }, { 4, 1 }, { 5, 0 }, { 6, 2 }, { 7, 1 }, { 8, 1 }, { 9, 1 }, { 10, 1 }, { 11, 1 }, { 12, 1 }, { 13, 1 }, { 14, 1 },
            { 15, 2 }, { 16, 2 }, { 17, 2 }, { 18, 2 }, { 19, 2 }, { 20, 2 }, { 27, 1 }, { 28, 0 }, { 29, 0 }, { 30, 1 }, { 31, 0 }, { 32, 0 }
        };
        static readonly int[] Closed = { 29, 31, 32 };

        static PowerFacts NoFacts(string name) { return null; }

        static LoadoutInput Input(string leader, Build build, string mode, int diff, Dictionary<int, int> levels, IEnumerable<int> closed, Func<string, PowerFacts> facts, Knowledge k)
        {
            var inp = Loadout.Prepare(leader, build, mode, diff, new Doctrine(), k, facts, null);
            inp.Slots = 4;
            foreach (var kv in levels) inp.Levels[kv.Key] = kv.Value;
            foreach (var id in closed) inp.RankOpen[id] = false;
            return inp;
        }

        /// <summary>The badge advice of every survivor (Builds.Survivors) for a mode and difficulty at the given levels.</summary>
        static List<KeyValuePair<string, LoadoutAdvice>> Advices(string mode, int diff, Dictionary<int, int> levels, Func<string, PowerFacts> facts, Knowledge k, List<BadgeFacts> fx, Func<string, Build> buildOf = null)
        {
            var list = new List<KeyValuePair<string, LoadoutAdvice>>();
            foreach (var s in Builds.Survivors)
                list.Add(new KeyValuePair<string, LoadoutAdvice>(s, Loadout.Recommend(Input(s, buildOf != null ? buildOf(s) : null, mode, diff, levels, Closed, facts, k), fx, k, 0)));
            return list;
        }

        static TNode BadgeNode(string name, int id, int rank, int level)
        {
            return new TNode { Key = "Test_BadgeBoost_" + rank + "_" + name.Replace(" Badge", ""), Name = name, Kind = TKind.Badge, Rank = rank, Slot = 10, Level = level, Min = 0, Max = 5, Costs = new[] { 1, 2, 3, 4, 5 }, BadgeId = id };
        }

        /// <summary>The badge steps of a plan: "&lt;name&gt; to N W|H|-: &lt;why&gt;" (W = whole, H = held).</summary>
        static List<string> BadgeSteps(List<TStep> steps, bool numbered = false)
        {
            var o = new List<string>();
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i]; if (s.Node.Kind != TKind.Badge) continue;
                o.Add((numbered ? (i + 1) + " " : "") + s.Node.Name + " to " + s.To + " " + (s.Hold ? "H" : s.Whole ? "W" : "-") + ": " + s.Why);
            }
            return o;
        }

        /// <summary>The gains and survivors of the levels above the node's level: "L3 24.57 (9) L4 ...".</summary>
        static string Gains(YardBadge b)
        {
            if (b == null) return "(no demand)";
            return string.Join(" ", Enumerable.Range(b.Level + 1, Math.Max(0, b.Max - b.Level)).Select(l => "L" + l + " " + b.Gain[l].ToString("0.000", IC) + " (" + b.EquipsAt[l] + ")"));
        }

        /// <summary>The gains match want (from the level above the node's) within 0.005, and the survivors counted.</summary>
        static bool GainsAre(YardBadge b, double[] want, int[] count)
        {
            if (b == null || b.Max - b.Level != want.Length) return false;
            for (int i = 0; i < want.Length; i++) if (!Near(b.Gain[b.Level + 1 + i], want[i]) || b.EquipsAt[b.Level + 1 + i] != count[i]) return false;
            return true;
        }

        static readonly List<string> _rails = new List<string>();
        static void Keep(List<TStep> steps) { foreach (var s in steps) if (s.Node.Kind == TKind.Badge && s.Why.Length > 0) _rails.Add(s.Why); }

        // ------------------------------------------------------------------------------------------------ YB1 / YB2: data-free tabs
        static void Yb1(Knowledge k, List<BadgeFacts> fx, List<KeyValuePair<string, LoadoutAdvice>> adv)
        {
            var tab = new List<TNode> { BadgeNode("Tough Badge", 18, 2, 2), BadgeNode("Bomber Badge", 19, 3, 2), BadgeNode("Power Badge", 20, 4, 2) };
            var d = YardBadges.Build(tab, adv, fx, k, 0.15, 2, Loadout.RunName("Normal", 1));
            var steps = TreePlan.ClassSteps(tab, d); Keep(steps);
            YardBadge tough = d.Of(tab[0]), bomber = d.Of(tab[1]), power = d.Of(tab[2]);
            Check("YB1 the demand (no probe facts, Normal I, 9 survivors): Tough equipped by 9, gains 24.57 / 49.14 / 73.71; Power 25.20 / 50.40 / 75.60; Bomber none",
                tough != null && tough.EquipsNow == 9 && GainsAre(tough, new[] { 24.57, 49.14, 73.71 }, new[] { 9, 9, 9 }) && GainsAre(power, new[] { 25.20, 50.40, 75.60 }, new[] { 9, 9, 9 })
                && GainsAre(bomber, new[] { 0.0, 0.0, 0.0 }, new[] { 0, 0, 0 }) && d.Survivors == 9,
                "Tough " + Gains(tough) + " | Power " + Gains(power) + " | Bomber " + Gains(bomber));
            string w1 = "The badge advice equips it for 9 of 9 survivors (Normal I).", w3 = "More levels: the advice equips it for 9 of 9 survivors (Normal I).";
            var want = new List<string>
            {
                "Tough Badge to 3 W: " + w1, "Power Badge to 3 W: " + w1, "Power Badge to 4 W: " + w3, "Tough Badge to 4 W: " + w3, "Power Badge to 5 W: " + w3, "Tough Badge to 5 W: " + w3,
                "Bomber Badge to 5 H: No survivor's badge advice equips it, even at level 5 (Normal I).",
            };
            var got = BadgeSteps(steps);
            Check("YB1 the steps: Tough and Power to 3 whole (W1), their further levels whole (W3), Bomber held to 5 (W4); nothing else", got.Count == steps.Count && FirstDiff(got, want).Length == 0, FirstDiff(got, want));
            string notes = "Tough Badge 2>3 stage 9/9 0.91/pt; Power Badge 2>3 stage 9/9 0.93/pt; Power Badge 3>4 levels 9/9 0.70/pt; Tough Badge 3>4 levels 9/9 0.68/pt; Power Badge 4>5 levels 9/9 0.56/pt; Tough Badge 4>5 levels 9/9 0.55/pt; Bomber Badge held (never)";
            Check("YB1 the log's notes", string.Join("; ", d.Notes) == notes, string.Join("; ", d.Notes));
            var a6 = TreePlan.Advise(tab, steps, 6); var a1 = TreePlan.Advise(tab, steps, 1); var a60 = TreePlan.Advise(tab, steps, 60);
            Check("YB1 6 points: both chunks 2>3, nothing partly bought; 1 point: saved for Tough 2>3, the held Bomber after the rest; 60 points: all three to 5",
                Say(a6) == "buy 1 Tough Badge 2>3 (3), 2 Power Badge 2>3 (3); later Power Badge 3>4, Tough Badge 3>4, Bomber Badge 2>5" && a6.Left == 0
                && Say(a1) == "buy nothing; save for Tough Badge 2>3 (3); later Power Badge 2>4, Bomber Badge 2>5" && a1.Left == 1
                && Say(a60) == "buy 1 Tough Badge 2>5 (12), 2 Power Badge 2>5 (12), 3 Bomber Badge 2>5 (12)" && a60.Left == 24,
                Say(a6) + " | left " + a6.Left + " / " + Say(a1) + " | left " + a1.Left + " / " + Say(a60) + " | left " + a60.Left);
        }

        static List<TNode> GhostTab() { return new List<TNode> { BadgeNode("Ninja Badge", 3, 2, 1), BadgeNode("Carver Badge", 4, 3, 1), BadgeNode("Chance Badge", 5, 4, 0) }; }

        static void Yb2(Knowledge k, List<BadgeFacts> fx, List<KeyValuePair<string, LoadoutAdvice>> adv)
        {
            var tab = GhostTab();
            var d = YardBadges.Build(tab, adv, fx, k, 0.15, 2, Loadout.RunName("Normal", 1));
            var steps = TreePlan.ClassSteps(tab, d); Keep(steps);
            YardBadge ninja = d.Of(tab[0]), carver = d.Of(tab[1]), chance = d.Of(tab[2]);
            Check("YB2 the demand: Ninja L2 4.45 (5 survivors) then 25.83 / 47.56 / 69.30 (9); Carver none; Chance L1 0, then 17.07 / 45.29 / 73.52 / 101.74 (9)",
                GainsAre(ninja, new[] { 4.45, 25.83, 47.56, 69.30 }, new[] { 5, 9, 9, 9 }) && GainsAre(carver, new[] { 0.0, 0.0, 0.0, 0.0 }, new[] { 0, 0, 0, 0 })
                && GainsAre(chance, new[] { 0.0, 17.07, 45.29, 73.52, 101.74 }, new[] { 0, 9, 9, 9, 9 }),
                "Ninja " + Gains(ninja) + " | Carver " + Gains(carver) + " | Chance " + Gains(chance));
            string w3 = "More levels: the advice equips it for 9 of 9 survivors (Normal I).";
            var want = new List<string>
            {
                "Ninja Badge to 3 W: At level 3 the advice equips it for 9 of 9 survivors (Normal I).", "Chance Badge to 2 W: At level 2 the advice equips it for 9 of 9 survivors (Normal I).",
                "Chance Badge to 3 W: " + w3, "Chance Badge to 4 W: " + w3, "Chance Badge to 5 W: " + w3, "Ninja Badge to 4 W: " + w3, "Ninja Badge to 5 W: " + w3,
                "Carver Badge to 5 H: No survivor's badge advice equips it, even at level 5 (Normal I).",
            };
            var got = BadgeSteps(steps);
            Check("YB2 the steps: Ninja whole 1>3 and Chance whole 0>2 (W2: equipped only from that level), the further levels (W3), Carver held (W4)", FirstDiff(got, want).Length == 0, FirstDiff(got, want));
            string notes = "Ninja Badge 1>3 stage 9/9 0.57/pt; Chance Badge 0>2 stage 9/9 0.63/pt; Chance Badge 2>3 levels 9/9 1.05/pt; Chance Badge 3>4 levels 9/9 0.78/pt; Chance Badge 4>5 levels 9/9 0.63/pt; Ninja Badge 3>4 levels 9/9 0.60/pt; Ninja Badge 4>5 levels 9/9 0.48/pt; Carver Badge held (never)";
            Check("YB2 the log's notes", string.Join("; ", d.Notes) == notes, string.Join("; ", d.Notes));
            var a4 = TreePlan.Advise(tab, steps, 4); var a5 = TreePlan.Advise(tab, steps, 5);
            Check("YB2 4 points: Chance 0>2 bought and Ninja 1>3 saved for whole (the leftover flows past the chunk it cannot afford, 1 left); 5 points: Ninja 1>3 whole",
                Say(a4) == "buy 1 Chance Badge 0>2 (3); save for Ninja Badge 1>3 (5); later Chance Badge 2>5, Carver Badge 1>5" && a4.Left == 1
                && Say(a5) == "buy 1 Ninja Badge 1>3 (5); later Chance Badge 0>5, Ninja Badge 3>5, Carver Badge 1>5" && a5.Left == 0,
                Say(a4) + " | left " + a4.Left + " / " + Say(a5) + " | left " + a5.Left);

            // YB2b: the Ghost's advice alone (one survivor unlocked)
            var one = adv.Where(x => x.Key == "Ghost").ToList();
            var tb = GhostTab();
            var db = YardBadges.Build(tb, one, fx, k, 0.15, 2, Loadout.RunName("Normal", 1));
            var sb = TreePlan.ClassSteps(tb, db); Keep(sb);
            string notesB = "Ninja Badge 1>3 stage 1/1 0.47/pt; Chance Badge 0>2 stage 1/1 0.56/pt; Chance Badge 2>3 levels 1/1 1.10/pt; Chance Badge 3>4 levels 1/1 0.82/pt; Chance Badge 4>5 levels 1/1 0.66/pt; Ninja Badge 3>4 levels 1/1 0.60/pt; Ninja Badge 4>5 levels 1/1 0.48/pt; Carver Badge held (never)";
            Check("YB2b one survivor (the Ghost alone): 'At level 3 the advice equips it for Ghost (Normal I).', the line says '1 survivor'",
                sb.Count > 0 && sb[0].Why == "At level 3 the advice equips it for Ghost (Normal I)." && string.Join("; ", db.Notes) == notesB && db.Line.Contains(", 1 survivor,"),
                sb.Count > 0 ? sb[0].Why + " | " + db.Line : "no steps");

            // YB2c: Extermination III, the longest run name
            var advX = Advices("Extermination", 3, Profile, NoFacts, k, fx);
            var tc = GhostTab();
            var dc = YardBadges.Build(tc, advX, fx, k, 0.15, 2, Loadout.RunName("Extermination", 3));
            var sc = TreePlan.ClassSteps(tc, dc); Keep(sc);
            var w2c = sc.FirstOrDefault(s => s.Why.StartsWith("At level", StringComparison.Ordinal)); var w3c = sc.FirstOrDefault(s => s.Why.StartsWith("More levels", StringComparison.Ordinal));
            var w4c = sc.FirstOrDefault(s => s.Why.StartsWith("No survivor's", StringComparison.Ordinal));
            Check("YB2c Extermination III: W2 73, W3 75, W4 74 characters (the cap is 76)", w2c != null && w3c != null && w4c != null && w2c.Why.Length == 73 && w3c.Why.Length == 75 && w4c.Why.Length == 74,
                (w2c != null ? w2c.Why.Length + " " : "-") + (w3c != null ? w3c.Why.Length + " " : "-") + (w4c != null ? w4c.Why.Length + "" : "-"));

            // YB2d: the Ghost's build pins Chance (locked): unlocked first, then its levels
            var pin = new Build { Id = "yb-test", Survivor = "Ghost", Name = "Test" }; pin.Badges.Add("Chance");
            var advD = Advices("Normal", 1, Profile, NoFacts, k, fx, s => s == "Ghost" ? pin : null);
            var td = GhostTab();
            var dd = YardBadges.Build(td, advD, fx, k, 0.15, 2, Loadout.RunName("Normal", 1));
            var sd = TreePlan.ClassSteps(td, dd); Keep(sd);
            var gotD = BadgeSteps(sd);
            Check("YB2d a build pins Chance: Chance to 1 whole 'Pinned on your Test build: unlock it.', then 'Chance Badge 1>3 levels 9/9 1.00/pt' (W2)",
                dd.Of(td[2]) != null && dd.Of(td[2]).PinnedBy == "your Test build" && gotD.Count > 2 && gotD[1] == "Chance Badge to 1 W: Pinned on your Test build: unlock it."
                && gotD[2] == "Chance Badge to 3 W: At level 3 the advice equips it for 9 of 9 survivors (Normal I)." && dd.Notes.Contains("Chance Badge 0>1 pinned") && dd.Notes.Contains("Chance Badge 1>3 levels 9/9 1.00/pt"),
                string.Join(" / ", gotD.Take(3)) + " | " + string.Join("; ", dd.Notes.Take(3)));
        }

        // ------------------------------------------------------------------------------------------------ YB3: Simulate's two passes
        static TNode Hand(string name, TKind kind, int level, int max, int[] costs) { return new TNode { Key = "T_" + name, Name = name, Kind = kind, Rank = 1, Slot = 0, Level = level, Min = 0, Max = max, Costs = costs }; }

        static void Yb3()
        {
            var armor = Hand("Armor", TKind.Stat, 0, 1, new[] { 5 }); var test = Hand("Test Badge", TKind.Badge, 0, 5, new[] { 1, 2, 3, 4, 5 });
            var sa = new List<TStep> { new TStep { Node = armor, To = 1, Why = "x" }, new TStep { Node = test, To = 5, Why = "h", Hold = true } };
            var both = new List<TNode> { armor, test };
            var a = TreePlan.Advise(both, sa, 1);
            Check("YB3a a held badge waits while an open step is unfinished: 1 point buys nothing (saved for Armor), the badge comes later (two passes at points + 40)",
                Say(a) == "buy nothing; save for Armor 0 (5); later Test Badge 0>5" && a.Left == 1, Say(a) + " | left " + a.Left);
            armor.Level = 1;
            var b = TreePlan.Advise(both, sa, 1);
            var before = TreeDiff.Levels(both); test.Level = 1; var ch = TreeDiff.Diff(before, both, b); test.Level = 0;
            Check("YB3b the rest done: the held badge is bought (1 point: 0>1, then saved for 1>2); the purchase log matches it 'advice #1'",
                Say(b) == "buy 1 Test Badge 0>1 (1); save for Test Badge 1>2 (2)" && ch.Count == 1 && ch[0].Line == "bought Test Badge 0>1 (advice #1)", Say(b) + " | " + string.Join(" ; ", ch.Select(c => c.Line)));
            var d = TreePlan.Advise(both, sa, 0);
            Check("YB3d the rest done, 0 points: saved for the held badge, the tab is not 'complete'", Say(d) == "buy nothing; save for Test Badge 0>1 (1)" && !d.Complete, Say(d) + (d.Complete ? " | complete" : ""));
            armor.Level = 0;
            var whole = Hand("Whole Badge", TKind.Badge, 1, 5, new[] { 1, 2, 3, 4, 5 }); var magnet = Hand("Magnet", TKind.Stat, 0, 1, new[] { 2 });
            var sc = new List<TStep> { new TStep { Node = whole, To = 3, Why = "w", Whole = true } };
            var c = TreePlan.Advise(new List<TNode> { whole }, sc, 4);
            Check("YB3c a whole chunk 1>3 (5 points) with 4: nothing partly bought, saved for whole (the strip: 'save 1 more for Whole Badge 1>3')",
                Say(c) == "buy nothing; save for Whole Badge 1>3 (5)" && c.Left == 4 && c.SaveFor != null && Math.Max(1, c.SaveFor.Cost - c.Left) == 1, Say(c) + " | left " + c.Left);
            var sc2 = new List<TStep> { sc[0], new TStep { Node = magnet, To = 1, Why = "y" } };
            var c2 = TreePlan.Advise(new List<TNode> { whole, magnet }, sc2, 4);
            Check("YB3c2 a cheaper step after the chunk: the leftover still flows to it (Magnet bought, the chunk saved for)", Say(c2) == "buy 1 Magnet 0 (2); save for Whole Badge 1>3 (5)" && c2.Left == 2, Say(c2) + " | left " + c2.Left);
            var c3 = TreePlan.Advise(new List<TNode> { whole }, sc, 5);
            Check("YB3c3 5 points: the chunk bought whole", Say(c3) == "buy 1 Whole Badge 1>3 (5)" && c3.Left == 0, Say(c3) + " | left " + c3.Left);
            Check("YB3e the Later list of YB3a comes from the same two-pass walk (points + 40): Armor first, then the held badge", a.Later.Count == 1 && a.Later[0].Label == "Test Badge 0>5", string.Join(", ", a.Later.Select(x => x.Label)));
        }

        // ------------------------------------------------------------------------------------------------ YB6: the rails of the reasons
        static readonly string[] Banned = { "pts", "/pt", "score", "floor", "per point" };
        static int Rails(IEnumerable<string> reasons, string label, bool wantAll)
        {
            var list = reasons.Distinct().ToList();
            var badOnes = list.Where(r => r.Any(ch => ch < 32 || ch > 126) || (!r.StartsWith("Pinned on ", StringComparison.Ordinal) && r.Length > 76) || Banned.Any(w => r.Contains(w))).ToList();
            string[] heads = { "The badge advice equips it for ", "At level ", "More levels: ", "No survivor's badge advice", "The badge advice equips it only from level", "Its next levels add little", "Pinned on " };
            var counts = heads.Select(h => list.Count(r => r.StartsWith(h, StringComparison.Ordinal))).ToList();
            int max = list.Count == 0 ? 0 : list.Max(r => r.StartsWith("Pinned on ", StringComparison.Ordinal) ? 0 : r.Length);
            Check(label + ": " + list.Count + " distinct reasons, printable ASCII, at most 76 characters (W7 exempt), no score / points-per-point words" + (wantAll ? "; W1-W7 each at least once" : ""),
                badOnes.Count == 0 && (!wantAll || counts.All(n => n > 0)), "max " + max + "; W1-W7 " + string.Join("/", counts) + (badOnes.Count > 0 ? "; off the rails: " + string.Join(" | ", badOnes.Take(3)) : ""));
            return max;
        }

        static void Yb6Synthetic()
        {
            var worst = new YardBadges { Context = "Extermination III", Survivors = 9 };
            var two = new List<string> { "Engineer", "Huntress" };
            var synth = new[] { YardWords.W1(two, worst), YardWords.W2(5, two, worst), YardWords.W3(two, worst), YardWords.W4(5, worst), YardWords.W5(5, worst), YardWords.W6(worst) };
            Check("YB6 the worst case (9 survivors, two of them, Extermination III): W1-W6 at 68 / 73 / 75 / 74 / 65 / 62 characters",
                string.Join("/", synth.Select(s => s.Length)) == "68/73/75/74/65/62", string.Join(" | ", synth));
            _rails.AddRange(synth);
        }

        // ------------------------------------------------------------------------------------------------ YB7: the sources
        static void Yb7()
        {
            string ui = Src("TreeUi.cs"), state = Src("TreeState.cs"), ls = Src("LoadoutState.cs"), plugin = Src("Plugin.cs"), menu = Src("Menu.cs"), lui = Src("LoadoutUi.cs"), builds = Src("Builds.cs"), preview = Src("Preview.cs");
            Check("YB7 TreeUi works out the badge demand on survivor tabs only, before the 'yard.advise' window, and plans with it",
                ui != null && Regex.IsMatch(ui, @"var badges = isTeam \? null : TreeState\.BadgeDemand\(tree, nodes\);\s*perf = Perf\.Begin\(\);\s*_steps = isTeam \? TreePlan\.TeamSteps\(nodes\) : TreePlan\.ClassSteps\(nodes, badges\);[\s\S]{0,200}Perf\.End\(""yard\.advise"", perf\);"));
            Check("YB7 the tab's signature appends TreeState.DemandSig(isTeam) (a menu toggle, a build, the run setup's mode, the doctrine, the knowledge re-plan)",
                ui != null && Regex.IsMatch(ui, @"sig\.Append\(TreeState\.DemandSig\(isTeam\)\);[^\n]*\n\s*string s = sig\.ToString\(\);"));
            Check("YB7 '[yard] badges of <tree>' logged once per change per tree",
                ui != null && ui.Contains("string line = \"[yard] badges of \" + tree + \" \" + badges.Line; string was;") && ui.Contains("if (!_badgeLines.TryGetValue(tree, out was) || was != line) { _badgeLines[tree] = line; Plugin.Logger.LogInfo(line); }"));
            Check("YB7 BadgeDemand reads [Advice] YardBadges, has the Perf sections 'yard.badges' / 'yard.badges.compute' and the '[yard] badge advice for' line, catches into null with a once-a-session warning",
                state != null && state.Contains("if (!Plugin.AdviceYardBadges.Value) return null;") && state.Contains("finally { Perf.End(\"yard.badges\", perf); }") && state.Contains("Perf.End(\"yard.badges.compute\", pc);")
                && state.Contains("\"[yard] badge advice for \"") && Regex.IsMatch(state, @"catch \(Exception e\)\s*\{\s*if \(!_steerWarnSaid\) \{ _steerWarnSaid = true; Plugin\.Logger\.LogWarning\(""\[yard\] badge steering off for this read: "" [^\n]*\n\s*return null;")
                && state.Contains("[yard] badge steering off: no badge readable here - the plan keeps the 0.15 badge order") && state.Contains("_yardCache.Get(key, () =>"));
            Check("YB7 Classify reads a badge node's badge through LoadoutState.BadgeIdOf in its own try; the save-key fallback after EnsureFacts",
                state != null && state.Contains("try { n.BadgeId = LoadoutState.BadgeIdOf(bb); } catch { n.BadgeId = -1; }") && state.Contains("n.BadgeId = LoadoutState.BadgeIdByNodeKey(n.Key);")
                && ls != null && ls.Contains("internal static int BadgeIdOf(SkillTreeUpgradeBadgeBoost n) { var b = n.targetBadge; return b == null ? -1 : IdOf(b); }") && ls.Contains("!ReferenceEquals(_idByNodeKeyOf, _all)"));
            Check("YB7 the game members hold SkillTreeUpgradeBadgeBoost.targetBadge (74 of 74) with its fallback",
                ls != null && ls.Contains("new[] { \"SkillTreeUpgradeBadgeBoost\", \"targetBadge\" }") && ls.Contains("{ \"SkillTreeUpgradeBadgeBoost.targetBadge\", \"badge nodes matched through the badges' own tree nodes\" }"));
            Check("YB7 Render: a badge later in the plan shows its reason and '(step n of m)', after the 'maxed' and locked notes",
                ui != null && Regex.IsMatch(ui, @"bool maxed = [^\n]*\n[\s\S]{0,400}if \(!maxed && _highlighted\.RankOpen && st != null && st\.Node\.Kind == TKind\.Badge && st\.Why\.Length > 0\)\s*rows\.Add\(Row\(""WHY"", Gold\(_highlighted\.Name\) \+ Dim\(_dash\) \+ st\.Why \+ Dim\("" \(step "" \+ \(pos \+ 1\) \+ "" of "" \+ _steps\.Count \+ ""\)""\)\)\);"));
            var kd = Knowledge.FromJson(Knowledge.DefaultJson); var kn = new Knowledge();
            Check("YB7 knowledge.json's yardRules (badgeFloor 0.15, badgeReach 2) read back as the code's defaults", kd.YardBadgeFloor == kn.YardBadgeFloor && kd.YardBadgeReach == kn.YardBadgeReach && kn.YardBadgeFloor == 0.15 && kn.YardBadgeReach == 2,
                kd.YardBadgeFloor.ToString(IC) + " / " + kd.YardBadgeReach);
            Check("YB7 the switch, its menu row, the build key shared with the run setup advice, the build change count, the preview's badge stage",
                plugin != null && plugin.Contains("AdviceYardBadges = Config.Bind(\"Advice\", \"YardBadges\", true,") && menu != null && menu.Contains("Cycler(rows, \"ad:yardbadges\"")
                && lui != null && lui.Contains("sb.Append('|').Append(BuildSig(b));") && lui.Contains("internal static string BuildSig(Build b)")
                && builds != null && Regex.IsMatch(builds, @"public static void Save\(\)\s*\{\s*Changes\+\+;") && preview != null && preview.Contains("\"yard7b_medic_badge\"") && preview.Contains("TreeUi.PreviewBadge(out held)"));
        }

        // ------------------------------------------------------------------------------------------------ YB9: the advice cache's key
        static void Yb9()
        {
            var levels = new Dictionary<int, int>(Profile);
            var sigs = Builds.Survivors.Select(s => s + "=auto").ToList();
            string k1 = YardBadges.Key("Normal", 1, 4, levels, sigs, "011TrueAutoBalancedFalse", 7);
            string ghostTab = YardBadges.Key("Normal", 1, 4, new Dictionary<int, int>(Profile), sigs, "011TrueAutoBalancedFalse", 7);
            var moved = new Dictionary<int, int>(Profile); moved[11] = 2;
            string k2 = YardBadges.Key("Normal", 1, 4, moved, sigs, "011TrueAutoBalancedFalse", 7);
            var ps = typeof(YardBadges).GetMethod("Key").GetParameters().Select(p => p.Name.ToLowerInvariant()).ToList();
            Check("YB9 the key: another tab with the same levels and survivors has the same key (the tab is no input), one badge level changes it, no RankOpen in it",
                k1 == ghostTab && k1 != k2 && !ps.Any(p => p.Contains("open") || p.Contains("rank") || p.Contains("tab")), string.Join(", ", ps));
            var cache = new YardAdviceCache();
            Func<List<KeyValuePair<string, LoadoutAdvice>>> compute = () => new List<KeyValuePair<string, LoadoutAdvice>>();
            cache.Get(k1, compute); cache.Get(ghostTab, compute); cache.Get(k1, compute);       // Ghost -> Medic -> Ghost
            int afterTabs = cache.Computes;
            cache.Get(k2, compute);                                                               // a badge level bought
            int afterBadge = cache.Computes;
            cache.Get(k2, compute);                                                               // a weapon level bought: the same key
            Check("YB9 the cache: Ghost -> Medic -> Ghost with one key computes once; a badge level change twice; a non-badge purchase keeps it", afterTabs == 1 && afterBadge == 2 && cache.Computes == 2,
                afterTabs + " / " + afterBadge + " / " + cache.Computes);
        }

        // ================================================================================================ the runs
        /// <summary>The cases without game data (also --no-data).</summary>
        public static int RunDataFree()
        {
            _bad = 0; _rails.Clear();
            Console.WriteLine("\n=== 0.16.0: the Training Yard's badges follow the badge advice (C16-07)");
            Yb0a();
            var k = Knowledge.FromJson(Knowledge.DefaultJson); var fx = Loadouts.Fixture();
            var adv = Advices("Normal", 1, Profile, NoFacts, k, fx);
            Yb1(k, fx, adv);
            Yb2(k, fx, adv);
            Yb3();
            Yb6Synthetic();
            Rails(_rails, "YB6 the badge reasons of YB1-YB2 and the worst case", true);
            Yb7();
            Yb9();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        static readonly MethodInfo TreeOfMethod = typeof(Checks).GetMethod("TreeOf", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        /// <summary>Checks.TreeOf (the tree of gamedata.json as TreeState reads it live) with each badge node's BadgeId: the node's
        /// badge.badgeId (= badgeSortOrder) -> the fixture's badgeBaseId.</summary>
        static List<TNode> TreeOf(JsonElement nodes, string tree, string branch, Dictionary<string, int> levels)
        {
            var list = (List<TNode>)TreeOfMethod.Invoke(null, new object[] { nodes, tree, branch, levels });
            var fx = Loadouts.Fixture();
            foreach (var n in list)
            {
                if (n.Kind != TKind.Badge) continue;
                JsonElement o, be, bi;
                if (!nodes.TryGetProperty(n.Key, out o) || !o.TryGetProperty("badge", out be) || be.ValueKind != JsonValueKind.Object || !be.TryGetProperty("badgeId", out bi)) continue;
                int sort = bi.GetInt32();
                var f = fx.FirstOrDefault(x => x.Sort == sort);
                if (f != null) n.BadgeId = f.Id;
            }
            return list;
        }

        /// <summary>A survivor's tree with the guides' branch, the badge nodes at the profile's levels (ranks 29 / 31 / 32 closed), the rest at minimum.</summary>
        static List<TNode> LiveTree(JsonElement gnodes, string survivor, Knowledge k)
        {
            string branch; k.WeaponBranch.TryGetValue(survivor, out branch);
            var tr = TreeOf(gnodes, survivor, branch, null);
            foreach (var n in tr)
            {
                if (n.Kind != TKind.Badge || n.BadgeId < 0) continue;
                int l; n.Level = Profile.TryGetValue(n.BadgeId, out l) ? l : 0;
                if (Closed.Contains(n.BadgeId)) n.RankOpen = false;
            }
            return tr;
        }

        static string ExpectedPath()
        {
            foreach (var p in new[] { Path.Combine(AppContext.BaseDirectory, "yard_015_expected.txt"), Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "yard_015_expected.txt")) })
                if (File.Exists(p)) return p;
            return null;
        }

        // the '[yard] badges of' lines the reviser's model printed for the 9 trees (revise_out.txt:125-292)
        static readonly Dictionary<string, string> TreeLines = new Dictionary<string, string>
        {
            { "SWAT|1", "(Normal I, 9 survivors, floor 0.15, reach 2): Gunner Badge 2>3 stage 6/9 0.87/pt; Critical Badge 2>4 stage 9/9 0.56/pt; Gunner Badge 3>4 levels 6/9 0.72/pt; Gunner Badge 4>5 levels 6/9 0.58/pt; Critical Badge 4>5 levels 9/9 0.45/pt; Leveling Badge held (from level 5)" },
            { "Tank|1", "(Normal I, 9 survivors, floor 0.15, reach 2): Tough Badge 2>3 stage 9/9 0.91/pt; Bomber Badge 2>3 stage 4/9 0.41/pt; Power Badge 2>3 stage 9/9 0.93/pt; Power Badge 3>4 levels 9/9 0.70/pt; Tough Badge 3>4 levels 9/9 0.68/pt; Power Badge 4>5 levels 9/9 0.56/pt; Tough Badge 4>5 levels 9/9 0.55/pt; Bomber Badge 3>4 levels 5/9 0.38/pt; Bomber Badge 4>5 levels 6/9 0.32/pt" },
            { "Engineer|1", "(Normal I, 9 survivors, floor 0.15, reach 2): Coverage Badge 2>4 stage 9/9 0.26/pt; Thunder Badge 2>3 stage 2/9 0.44/pt; Reload Badge 1>3 stage 9/9 0.77/pt; Reload Badge 3>4 levels 9/9 0.75/pt; Reload Badge 4>5 levels 9/9 0.60/pt; Coverage Badge 4>5 levels 9/9 0.35/pt; Thunder Badge 3>4 levels 2/9 0.33/pt; Thunder Badge 4>5 levels 2/9 0.27/pt" },
            { "Huntress|1", "(Normal I, 9 survivors, floor 0.15, reach 2): Speed Badge 2>3 stage 9/9 0.74/pt; Physical Badge 1>3 stage 8/9 0.91/pt; Speed Badge 3>4 levels 9/9 0.62/pt; Physical Badge 3>5 levels 8/9 0.61/pt; Speed Badge 4>5 levels 9/9 0.49/pt; Boss Badge 1>5 levels 9/9 0.18/pt" },
            { "Ghost|1", "(Normal I, 9 survivors, floor 0.15, reach 2): Ninja Badge 1>3 stage 9/9 0.42/pt; Carver Badge 1>2 stage 3/9 0.53/pt; Chance Badge 0>2 stage 9/9 0.38/pt; Chance Badge 2>3 levels 9/9 1.05/pt; Chance Badge 3>4 levels 9/9 0.78/pt; Chance Badge 4>5 levels 9/9 0.63/pt; Ninja Badge 3>4 levels 9/9 0.60/pt; Ninja Badge 4>5 levels 9/9 0.48/pt; Carver Badge 2>3 levels 3/9 0.45/pt; Carver Badge 3>4 levels 5/9 0.40/pt; Carver Badge 4>5 levels 6/9 0.37/pt" },
            { "Medic|1", "(Normal I, 9 survivors, floor 0.15, reach 2): Growth Badge 1>3 stage 9/9 0.41/pt; Soul Badge 1>2 stage 1/9 0.31/pt; Growth Badge 3>4 levels 9/9 0.60/pt; Growth Badge 4>5 levels 9/9 0.48/pt; Soul Badge 2>3 levels 1/9 0.21/pt; Soul Badge 3>4 levels 1/9 0.16/pt; Soul Badge held (next levels 0.12/pt); Healing Badge held (from level 4)" },
            { "Pyro|1", "(Normal I, 9 survivors, floor 0.15, reach 2): Gamble Badge 1>3 stage 9/9 0.29/pt; Heat Badge 1>2 stage 1/9 0.42/pt; Elemental Badge 1>3 stage 4/9 0.53/pt; Gamble Badge 3>4 levels 9/9 0.38/pt; Elemental Badge 3>5 levels 5/9 0.34/pt; Heat Badge 2>3 levels 1/9 0.28/pt; Gamble Badge 4>5 levels 9/9 0.26/pt; Heat Badge 3>4 levels 1/9 0.21/pt; Heat Badge 4>5 levels 1/9 0.17/pt" },
            { "Mechanic|1", "(Normal I, 9 survivors, floor 0.15, reach 2): Glacier Badge 0>2 stage 2/9 0.28/pt; Glacier Badge 2>3 levels 2/9 0.30/pt; Glacier Badge 3>4 levels 2/9 0.23/pt; Thief Badge 0>5 levels 9/9 0.19/pt; Glacier Badge 4>5 levels 2/9 0.18/pt; Gatherer Badge held (never)" },
            { "Ranger|1", "(Normal I, 9 survivors, floor 0.15, reach 2): Refresh Badge 0>5 levels 9/9 0.16/pt; Dexterity Badge held (never); Training Badge held (never)" },
            { "SWAT|2", "(Normal II, 9 survivors, floor 0.15, reach 2): Gunner Badge 2>3 stage 6/9 0.86/pt; Critical Badge 2>4 stage 9/9 0.54/pt; Gunner Badge 3>4 levels 6/9 0.72/pt; Gunner Badge 4>5 levels 6/9 0.58/pt; Critical Badge 4>5 levels 9/9 0.45/pt; Leveling Badge held (from level 5)" },
            { "Tank|2", "(Normal II, 9 survivors, floor 0.15, reach 2): Tough Badge 2>3 stage 9/9 0.98/pt; Bomber Badge 2>3 stage 4/9 0.41/pt; Power Badge 2>3 stage 9/9 0.93/pt; Tough Badge 3>4 levels 9/9 0.74/pt; Power Badge 3>4 levels 9/9 0.70/pt; Tough Badge 4>5 levels 9/9 0.59/pt; Power Badge 4>5 levels 9/9 0.56/pt; Bomber Badge 3>4 levels 5/9 0.38/pt; Bomber Badge 4>5 levels 5/9 0.32/pt" },
            { "Engineer|2", "(Normal II, 9 survivors, floor 0.15, reach 2): Coverage Badge 2>4 stage 9/9 0.24/pt; Thunder Badge 2>3 stage 2/9 0.44/pt; Reload Badge 1>3 stage 9/9 0.76/pt; Reload Badge 3>4 levels 9/9 0.75/pt; Reload Badge 4>5 levels 9/9 0.60/pt; Coverage Badge 4>5 levels 9/9 0.35/pt; Thunder Badge 3>4 levels 2/9 0.33/pt; Thunder Badge 4>5 levels 2/9 0.27/pt" },
            { "Huntress|2", "(Normal II, 9 survivors, floor 0.15, reach 2): Speed Badge 2>3 stage 9/9 0.81/pt; Physical Badge 1>3 stage 8/9 0.89/pt; Speed Badge 3>4 levels 9/9 0.64/pt; Physical Badge 3>5 levels 8/9 0.61/pt; Speed Badge 4>5 levels 9/9 0.51/pt; Boss Badge 1>5 levels 9/9 0.17/pt" },
            { "Ghost|2", "(Normal II, 9 survivors, floor 0.15, reach 2): Ninja Badge 1>3 stage 9/9 0.52/pt; Carver Badge 1>2 stage 3/9 0.52/pt; Chance Badge 0>2 stage 9/9 0.35/pt; Chance Badge 2>3 levels 9/9 1.05/pt; Chance Badge 3>4 levels 9/9 0.78/pt; Ninja Badge 3>4 levels 9/9 0.65/pt; Chance Badge 4>5 levels 9/9 0.63/pt; Ninja Badge 4>5 levels 9/9 0.52/pt; Carver Badge 2>3 levels 3/9 0.45/pt; Carver Badge 3>4 levels 5/9 0.39/pt; Carver Badge 4>5 levels 6/9 0.37/pt" },
            { "Medic|2", "(Normal II, 9 survivors, floor 0.15, reach 2): Growth Badge 1>3 stage 9/9 0.39/pt; Soul Badge 1>2 stage 1/9 0.31/pt; Growth Badge 3>4 levels 9/9 0.60/pt; Growth Badge 4>5 levels 9/9 0.48/pt; Soul Badge 2>3 levels 1/9 0.21/pt; Soul Badge 3>4 levels 1/9 0.16/pt; Soul Badge held (next levels 0.12/pt); Healing Badge held (from level 4)" },
            { "Pyro|2", "(Normal II, 9 survivors, floor 0.15, reach 2): Gamble Badge 1>3 stage 9/9 0.27/pt; Heat Badge 1>2 stage 1/9 0.42/pt; Elemental Badge 1>3 stage 4/9 0.54/pt; Gamble Badge 3>4 levels 9/9 0.38/pt; Elemental Badge 3>5 levels 5/9 0.34/pt; Heat Badge 2>3 levels 1/9 0.28/pt; Gamble Badge 4>5 levels 9/9 0.26/pt; Heat Badge 3>4 levels 1/9 0.21/pt; Heat Badge 4>5 levels 1/9 0.17/pt" },
            { "Mechanic|2", "(Normal II, 9 survivors, floor 0.15, reach 2): Glacier Badge 0>2 stage 2/9 0.28/pt; Glacier Badge 2>3 levels 2/9 0.30/pt; Glacier Badge 3>4 levels 2/9 0.23/pt; Thief Badge 0>5 levels 9/9 0.18/pt; Glacier Badge 4>5 levels 2/9 0.18/pt; Gatherer Badge held (never)" },
            { "Ranger|2", "(Normal II, 9 survivors, floor 0.15, reach 2): Refresh Badge 0>5 levels 9/9 0.15/pt; Dexterity Badge held (never); Training Badge held (never)" },
        };

        /// <summary>The full bench (Checks.Run, after TreeCases): the data-free part, then the cases over the game's data.</summary>
        public static int Run(string probePath, string gamedataPath)
        {
            int bad = RunDataFree();
            _bad = 0; _rails.Clear();
            Console.WriteLine("\n=== 0.16.0: the Training Yard's badge steering over the game's data (C16-07: YB0b, the 9 trees, YB4 a logged Medic replay, YB5 the oracle)");
            if (string.IsNullOrEmpty(gamedataPath) || !File.Exists(gamedataPath)) { Console.WriteLine("  (no gamedata.json: skipped)"); return bad; }
            if (string.IsNullOrEmpty(probePath) || !File.Exists(probePath)) { Console.WriteLine("  (no probe.json: skipped)"); return bad; }
            if (TreeOfMethod == null) { Check("YB0b Checks.TreeOf found", false, "the bench's tree reader is gone"); return bad + _bad; }
            Loadouts.LoadProbe(probePath);
            Func<string, PowerFacts> facts = Loadouts.Fact;
            var k = Knowledge.FromJson(Knowledge.DefaultJson); var fx = Loadouts.Fixture();
            using (var doc = JsonDocument.Parse(File.ReadAllText(gamedataPath)))
            {
                var gnodes = doc.RootElement.GetProperty("nodes");
                Yb0b(gnodes, k);
                Trees(gnodes, k, fx, facts);
                Yb4(gnodes, k, fx, facts);
                Yb5(k, fx, facts);
                Rails(_rails, "YB6 the badge reasons of the 9 trees (Normal I and II) and of the Medic replay", false);
                Yb8(gnodes, k, fx, facts);
            }
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return bad + _bad;
        }

        static void Yb0b(JsonElement gnodes, Knowledge k)
        {
            string path = ExpectedPath();
            if (path == null) { Check("YB0b yard_015_expected.txt is there (the 0.15 goldens)", false, "not found next to the bench"); return; }
            var want = File.ReadAllLines(path).Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal)).ToList();
            var got = new List<string>();
            foreach (var t in Builds.Survivors)
            {
                var tr = LiveTree(gnodes, t, k);
                got.AddRange(PlanLines(tr, TreePlan.ClassSteps(tr, null), t + " "));
            }
            string diff = FirstDiff(got, want);
            Check("YB0b the 9 survivor trees at the profile's badge levels, no badge advice ([Advice] YardBadges off): the 0.15 steps and advice of yard_015_expected.txt (" + want.Count + " lines)", diff.Length == 0, diff);
        }

        // the 9 trees with the badge advice on (the profile's levels, guides, probe facts): the '[yard] badges of' lines; DK-X01 (Glacier)
        static void Trees(JsonElement gnodes, Knowledge k, List<BadgeFacts> fx, Func<string, PowerFacts> facts)
        {
            var diffs = new List<string>(); int lines = 0;
            foreach (int diff in new[] { 1, 2 })
            {
                var adv = Advices("Normal", diff, Profile, facts, k, fx);
                foreach (var t in Builds.Survivors)
                {
                    var tr = LiveTree(gnodes, t, k);
                    var d = YardBadges.Build(tr, adv, fx, k, k.YardBadgeFloor, k.YardBadgeReach, Loadout.RunName("Normal", diff));
                    var st = TreePlan.ClassSteps(tr, d); Keep(st);
                    lines++;
                    string want; TreeLines.TryGetValue(t + "|" + diff, out want);
                    if (d.Line != want) diffs.Add(t + " Normal " + diff + ": " + d.Line);
                    if (t == "Mechanic" && diff == 1) Glacier(tr, st, d);
                }
            }
            Check("YB6 the 9 trees at the profile's levels, Normal I and II: every '[yard] badges of' line as the reviser's model printed it (" + lines + " lines)", diffs.Count == 0 && lines == 18, diffs.Count > 0 ? diffs[0] : "");
        }

        static void Glacier(List<TNode> tr, List<TStep> st, YardBadges d)
        {
            var g = tr.FirstOrDefault(n => n.Kind == TKind.Badge && n.Name == "Glacier Badge");
            var b = d.Of(g);
            var first = st.FirstOrDefault(s => s.Node == g);
            bool lone = false;
            for (int p = 0; p <= 40 && g != null; p++) { var a = TreePlan.Advise(tr, st, p); if (a.Now.Any(x => x.Node == g && x.From == 0 && x.To == 1)) { lone = true; break; } }
            Check("YB4b DK-X01 (the Deck bought Glacier Badge 0>1, a level no advice equips): the plan takes it whole 0>2, never 0>1 alone (0-40 points)",
                g != null && b != null && first != null && first.Whole && first.To == 2 && b.EquipsAt[1] == 0 && !lone,
                g == null ? "no Glacier node" : "equipped at L1 by " + (b != null ? b.EquipsAt[1] : -1) + ", at L2 by " + (b != null ? string.Join(", ", b.WhoAt[2]) : "-") + "; first step to " + (first != null ? first.To + (first.Whole ? " whole" : "") : "-"));
        }

        // ------------------------------------------------------------------------------------------------ YB4: a logged Medic replay
        static void Yb4(JsonElement gnodes, Knowledge k, List<BadgeFacts> fx, Func<string, PowerFacts> facts)
        {
            var medLv = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "Experiment 21", 3 }, { "Medical Drone", 3 }, { "Freezing Flasks", 3 }, { "Handgun", 3 }, { "Syringe Gun", 4 }, { "Experiment 21 Evolutions", 1 },
                { "Medical Drone Evolutions", 1 }, { "Stimpack", 2 }, { "Resuscitation", 1 }, { "Growth Badge", 1 }, { "Soul Badge", 1 }, { "Healing Badge", 0 },
            };
            var before = new Dictionary<int, int>(Profile); before[11] = 0;
            string say23 = null, say1 = null, say200 = null;
            foreach (int diff in new[] { 1, 2 })
            {
                string ctx = Loadout.RunName("Normal", diff);
                var med = TreeOf(gnodes, "Medic", "Freezing Flasks", medLv);
                foreach (var n in med) if (n.Rank == 5) n.RankOpen = false;
                var adv = Advices("Normal", diff, before, facts, k, fx);
                var d = YardBadges.Build(med, adv, fx, k, 0.15, 2, ctx);
                var st = TreePlan.ClassSteps(med, d); Keep(st);
                var on23 = TreePlan.Advise(med, st, 23); var on1 = TreePlan.Advise(med, st, 1); var on83 = TreePlan.Advise(med, st, 83); var on200 = TreePlan.Advise(med, st, 200);
                Func<TAdvice, bool> healing = a => a.Now.Concat(a.Later).Concat(a.SaveFor != null ? new[] { a.SaveFor } : new TBuy[0]).Any(x => x.Node.Name == "Healing Badge");
                if (diff == 1)
                {
                    var off = TreePlan.Advise(med, TreePlan.ClassSteps(med), 23);
                    Check("YB4 off (0.15): 23 points buy Healing Badge 0>1 with the leftover point - as logged",
                        Say(off) == "buy 1 Experiment 21 3>5 (9), 2 Medical Drone 3>5 (9), 3 Freezing Flasks 3>4 (4), 4 Healing Badge 0>1 (1); save for Freezing Flasks 4>5 (5); later Resuscitation 1>3, Stimpack 2>3, Synergy: SWAT 0", Say(off));
                    Check("YB4 on: 23 points no longer buy Healing (1 left), 1 point saves for Experiment 21 (no Healing), 83 points name no Healing anywhere",
                        Say(on23) == "buy 1 Experiment 21 3>5 (9), 2 Medical Drone 3>5 (9), 3 Freezing Flasks 3>4 (4); save for Freezing Flasks 4>5 (5); later Resuscitation 1>3, Stimpack 2>3, Growth Badge 1>3" && on23.Left == 1
                        && Say(on1) == "buy nothing; save for Experiment 21 3>4 (4); later Medical Drone 3>5, Freezing Flasks 3>5, Resuscitation 1>3" && on1.Left == 1 && !healing(on83),
                        Say(on23) + " | left " + on23.Left + " / " + Say(on1) + " | left " + on1.Left);
                    Check("YB4 on: with 200 points Healing Badge 0>5 (15) is the 15th and last purchase, after every other open step",
                        on200.Now.Count == 15 && on200.Now[14].Label == "Healing Badge 0>5" && on200.Now[14].Cost == 15, on200.Now.Count > 0 ? on200.Now.Count + " purchases, last " + on200.Now.Last().Label + " (" + on200.Now.Last().Cost + ")" : "none");
                    YardBadge growth = d.Of(med.FirstOrDefault(n => n.Name == "Growth Badge")), soul = d.Of(med.FirstOrDefault(n => n.Name == "Soul Badge")), heal = d.Of(med.FirstOrDefault(n => n.Name == "Healing Badge"));
                    Check("YB4 the demand: Growth L2 0.05 (1) then 18.37 / 39.90 / 61.44 (9); Soul equipped by the Medic, 5.59 / 11.19 / 16.78 / 22.38 (1); Healing 0 to level 3, L4 1.58 (5), L5 12.82 (9)",
                        GainsAre(growth, new[] { 0.05, 18.37, 39.90, 61.44 }, new[] { 1, 9, 9, 9 }) && soul != null && soul.EquipsNow == 1 && soul.WhoNow.SequenceEqual(new[] { "Medic" })
                        && GainsAre(soul, new[] { 5.59, 11.19, 16.78, 22.38 }, new[] { 1, 1, 1, 1 }) && GainsAre(heal, new[] { 0.0, 0.0, 0.0, 1.58, 12.82 }, new[] { 0, 0, 0, 5, 9 }),
                        "Growth " + Gains(growth) + " | Soul " + Gains(soul) + " | Healing " + Gains(heal));
                    string w3 = "More levels: the advice equips it for ";
                    var want = new List<string>
                    {
                        "16 Growth Badge to 3 W: At level 3 the advice equips it for 9 of 9 survivors (Normal I).", "20 Soul Badge to 2 W: The badge advice equips it for Medic (Normal I).",
                        "32 Growth Badge to 4 W: " + w3 + "9 of 9 survivors (Normal I).", "33 Growth Badge to 5 W: " + w3 + "9 of 9 survivors (Normal I).",
                        "34 Soul Badge to 3 W: " + w3 + "Medic (Normal I).", "35 Soul Badge to 4 W: " + w3 + "Medic (Normal I).",
                        "38 Soul Badge to 5 H: Its next levels add little for the points (Normal I).", "39 Healing Badge to 5 H: The badge advice equips it only from level 4 (Normal I).",
                    };
                    var got = BadgeSteps(st, true);
                    Check("YB4 the badge steps of the 39: Growth whole 1>3 (W2), Soul 1>2 for the Medic (W1), their levels after the rank V passives (W3), Soul's last level held (W6), Healing held (W5)",
                        st.Count == 39 && FirstDiff(got, want).Length == 0, st.Count + " steps; " + FirstDiff(got, want));
                    say23 = Say(on23); say1 = Say(on1); say200 = Say(on200);
                }
                else Check("YB4 Normal II: the same purchases (23, 1 and 200 points)", Say(on23) == say23 && Say(on1) == say1 && Say(on200) == say200, Say(on23));
                string notes = diff == 1
                    ? "Growth Badge 1>3 stage 9/9 0.41/pt; Soul Badge 1>2 stage 1/9 0.31/pt; Growth Badge 3>4 levels 9/9 0.60/pt; Growth Badge 4>5 levels 9/9 0.48/pt; Soul Badge 2>3 levels 1/9 0.21/pt; Soul Badge 3>4 levels 1/9 0.16/pt; Soul Badge held (next levels 0.12/pt); Healing Badge held (from level 4)"
                    : "Growth Badge 1>3 stage 9/9 0.39/pt; Soul Badge 1>2 stage 1/9 0.31/pt; Growth Badge 3>4 levels 9/9 0.60/pt; Growth Badge 4>5 levels 9/9 0.48/pt; Soul Badge 2>3 levels 1/9 0.21/pt; Soul Badge 3>4 levels 1/9 0.16/pt; Soul Badge held (next levels 0.12/pt); Healing Badge held (from level 4)";
                Check("YB4 the log's notes (" + ctx + ")", string.Join("; ", d.Notes) == notes, string.Join("; ", d.Notes));
            }
        }

        // ------------------------------------------------------------------------------------------------ YB5: the oracle
        static void Yb5(Knowledge k, List<BadgeFacts> fx, Func<string, PowerFacts> facts)
        {
            double worst = 0; string worstRow = ""; int rows = 0, same = 0; var differ = new List<string>();
            foreach (var md in new[] { Tuple.Create("Normal", 1), Tuple.Create("Normal", 2), Tuple.Create("Hardcore", 1), Tuple.Create("OneHit", 1) })
            {
                var adv = Advices(md.Item1, md.Item2, Profile, facts, k, fx);
                foreach (var bf in fx.OrderBy(x => x.Sort))
                {
                    int L; if (!Profile.TryGetValue(bf.Id, out L)) L = 0;
                    if (L >= bf.Max) continue;
                    var node = BadgeNode(bf.Name, bf.Id, bf.Rank, L);
                    var b = YardBadges.Build(new List<TNode> { node }, adv, fx, k, 0.15, 2, "x").Of(node);
                    if (b == null) continue;
                    double bestL = double.NegativeInfinity, bestX = double.NegativeInfinity; int tL = -1, tX = -1, cost = 0;
                    var closed = Closed.Where(x => x != bf.Id).ToList();
                    for (int l = L + 1; l <= bf.Max; l++)
                    {
                        cost += node.CostFrom(l - 1);
                        double gx = 0;
                        var lv2 = new Dictionary<int, int>(Profile); lv2[bf.Id] = l;
                        foreach (var kv in adv)
                        {
                            var inp = Loadout.Prepare(kv.Key, null, md.Item1, md.Item2, new Doctrine(), k, facts, null);
                            inp.Slots = 4; foreach (var x in lv2) inp.Levels[x.Key] = x.Value; foreach (var id in closed) inp.RankOpen[id] = false;
                            var a2 = Loadout.Recommend(inp, fx, k, 0);
                            gx += Math.Max(0, a2.Picks.Sum(p => p.Score) - kv.Value.Picks.Sum(p => p.Score));
                        }
                        double vl = b.Gain[l] / cost, vx = gx / cost, dlt = Math.Abs(vl - vx);
                        if (dlt > worst) { worst = dlt; worstRow = md.Item1 + " " + md.Item2 + " " + bf.Short + " " + L + ">" + l + ": the model " + F2(vl) + ", a re-advice " + F2(vx) + " per point"; }
                        if (vl > bestL + 1e-9) { bestL = vl; tL = l; }
                        if (vx > bestX + 1e-9) { bestX = vx; tX = l; }
                    }
                    rows++;
                    if (tL == tX) same++; else differ.Add(md.Item1 + " " + md.Item2 + " " + bf.Short + " " + L + ": to " + tL + " / " + tX);
                }
            }
            double share = rows == 0 ? 0 : (double)same / rows;
            Check("YB5 the oracle (guides, profile levels, Normal I / Normal II / Hardcore I / One Hit I): the model within 1.25 per point of a full re-advice summed over 9 survivors, the same best level in 95 % of the rows or more",
                rows > 0 && worst <= 1.25 && share >= 0.95, "worst " + worst.ToString("0.000", IC) + " (" + worstRow + "); same best level " + same + " of " + rows + (differ.Count > 0 ? "; " + string.Join("; ", differ.Take(3)) : ""));
        }

        // ------------------------------------------------------------------------------------------------ YB8: the cost (printed, no gate)
        static void Yb8(JsonElement gnodes, Knowledge k, List<BadgeFacts> fx, Func<string, PowerFacts> facts)
        {
            // the machine's timings only on request: without them a bench log is the same bytes on every run of one tree (the release's
            // --strict log is compared byte for byte)
            if (!Environment.GetCommandLineArgs().Contains("--timings"))
            {
                Console.WriteLine("  YB8 the cost on this machine (no gate): not timed in this run - '--timings' prints the ms of 9 advices + Build per key change and of Build + ClassSteps on a cache hit (the reviser's scratch: 1.6-1.8 / 0.16-0.20 ms)");
                return;
            }
            var med = LiveTree(gnodes, "Medic", k);
            for (int i = 0; i < 3; i++) Advices("Normal", 1, Profile, facts, k, fx);
            var sw = System.Diagnostics.Stopwatch.StartNew(); const int n = 20;
            for (int i = 0; i < n; i++) YardBadges.Build(med, Advices("Normal", 1, Profile, facts, k, fx), fx, k, 0.15, 2, "x");
            double perKey = sw.Elapsed.TotalMilliseconds / n;
            var adv = Advices("Normal", 1, Profile, facts, k, fx);
            sw.Restart(); const int m = 200;
            for (int i = 0; i < m; i++) TreePlan.ClassSteps(med, YardBadges.Build(med, adv, fx, k, 0.15, 2, "x"));
            Console.WriteLine("  YB8 the cost on this machine (no gate): 9 advices + Build " + perKey.ToString("0.00", IC) + " ms per key change; Build + ClassSteps on a cache hit "
                + (sw.Elapsed.TotalMilliseconds / m).ToString("0.000", IC) + " ms (the reviser's scratch: 1.6-1.8 / 0.16-0.20 ms)");
        }
    }
}
