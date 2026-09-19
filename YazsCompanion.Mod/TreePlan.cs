// Training Yard advice: which nodes to buy with the points on hand, what to save for next, and why. A port of the PC
// app's "Spend now" (lib/engine.js: TEAM_PLAN_BASE, classPlan, simulate) without its run-history tailoring: the order
// follows the published guides (the weapon branch and ability tiers of knowledge.json) and, inside a group, the tree's
// own layout. Pure: it sees only TNode values, so it can be exercised without the game.
//
// A plan is an ordered list of steps (node, target level, reason). Simulate walks it with a budget: a step whose rank
// is still locked or whose prerequisite is not bought is deferred, a step that is too expensive becomes the thing to
// save for, everything else is bought level by level. Costs: going from level L to L + 1 costs Costs[L]; levels below
// Min are free (the first level of weapons and abilities).
using System;
using System.Collections.Generic;
using System.Linq;

namespace YazsCompanion
{
    internal enum TKind { Weapon, Ability, Evolution, Synergy, Badge, Passive, Stat, Mechanic, Other }

    internal sealed class TNode
    {
        public string Key = "", Name = "", Desc = "";
        public TKind Kind = TKind.Other;
        public int Rank = 1, Slot, Level, Min, Max = 1;
        public int[] Costs = new int[0];
        public readonly List<string> Prereqs = new List<string>();
        public bool RankOpen = true, Perm;
        public string Tier = "";          // the guides' tier of the ability this node boosts or evolves (S / A / B / C / "")
        public string BaseKey = "";       // evolution: the key of the ability node it evolves
        public bool GuideBranch;          // weapon: the pick at the tier-2 fork (the selected build's branch, else the guides')
        public string BranchWhy = "";     // set when the pick comes from the player's build
        public object Ui;                 // the UISkillTreeNode it was read from

        public int CostFrom(int level)
        {
            if (level >= Max) return int.MaxValue;
            if (level < Min) return 0;
            return level < Costs.Length ? Costs[level] : int.MaxValue;
        }
    }

    internal sealed class TStep { public TNode Node; public int To; public string Why = ""; }

    internal sealed class TBuy
    {
        public TNode Node; public int From, To, Cost; public string Why = ""; public int Order;
        public string Label { get { return Node.Name + " " + From + (To - From > 1 || Node.Max > 1 ? ">" + To : ""); } }
    }

    internal sealed class TAdvice
    {
        public int Points, Left;
        public readonly List<TBuy> Now = new List<TBuy>();
        public TBuy SaveFor;
        public readonly List<TBuy> Later = new List<TBuy>();
        public bool Complete;             // nothing left to buy in the open ranks
        public TBuy Find(TNode n) { return Now.FirstOrDefault(b => b.Node == n) ?? (SaveFor != null && SaveFor.Node == n ? SaveFor : null) ?? Later.FirstOrDefault(b => b.Node == n); }
    }

    internal static class TreePlan
    {
        // ---- the General tree: a fixed order (economy first, then the strongest multipliers, survivability in between)
        static readonly string[][] Team =
        {
            new[] { "XPModifier", "3", "More XP per run: more level-ups, everything snowballs." },
            new[] { "MoneyModifier", "3", "Cash levels this tree: the node pays for itself." },
            new[] { "WeaponDamage", "3", "Flat damage for every survivor's weapon." },
            new[] { "WeaponAttackSpeed", "3", "+5 % attack speed per level: the best rank I multiplier." },
            new[] { "AbilityDamage", "3", "Abilities carry most of the late-run damage." },
            new[] { "MaxHP", "2", "Cheap survivability while the trees are low." },
            new[] { "MovementSpeed", "2", "Room to kite; movement speed is hard to find in a run." },
            new[] { "XPModifier", "5", "Max the economy nodes while you are still leveling." },
            new[] { "MoneyModifier", "5", "Max the economy nodes while you are still leveling." },
            new[] { "SpecializationTempo", "3", "Faster survivor ranks: evolutions and rank V passives sooner." },
            new[] { "NumRerolls", "2", "Rerolls make builds far more consistent." },
            new[] { "WeaponCDRed", "3", "Cooldown reduction multiplies with attack speed." },
            new[] { "AbilityCDRed", "2", "More ability casts per minute." },
            new[] { "Armor", "2", "The best flat mitigation there is." },
            new[] { "WeaponAttackSpeed", "5", "The best weapon scaling per point." },
            new[] { "WeaponDamage", "5", "" }, new[] { "AbilityDamage", "5", "" }, new[] { "SpecializationTempo", "5", "" },
            new[] { "MagnetRange", "2", "Pickup range makes collecting XP and cash safer." },
            new[] { "MaxHP", "3", "" }, new[] { "MovementSpeed", "3", "" }, new[] { "WeaponCDRed", "5", "" }, new[] { "AbilityCDRed", "5", "" }, new[] { "Armor", "5", "" },
            new[] { "HPRegen2", "3", "Regeneration between fights." },
            new[] { "NumRerolls", "5", "" }, new[] { "MagnetRange", "5", "" }, new[] { "HPRegen2", "5", "" },
            new[] { "UnlockMechanic_HashtagEvent", "1", "Research Pod events add damage-type tag points in every run." },
            new[] { "WeaponCritChance", "3", "Crit chance multiplies with the rank IV crit damage nodes." },
            new[] { "AbilityCritChance", "3", "" },
            new[] { "DamagevsElites", "3", "Elites are the main threat between minute 15 and 20." },
            new[] { "NumBanishes", "2", "A banish removes a bad option for the rest of the run." },
            new[] { "MaxHP2", "3", "" }, new[] { "WeaponCritChance", "5", "" }, new[] { "AbilityCritChance", "5", "" }, new[] { "DamagevsElites", "5", "" },
            new[] { "WeaponDamage2", "5", "" }, new[] { "AbilityDamage2", "5", "" }, new[] { "WeaponCritDamage", "5", "" }, new[] { "AbilityCritDamage", "5", "" },
            new[] { "PowerupDuration", "3", "The lowest impact of rank II: late." },
            new[] { "DodgeChance", "5", "" }, new[] { "AbilityDuration", "5", "" }, new[] { "NumBanishes", "5", "" }, new[] { "PowerupDuration", "5", "" },
            new[] { "AbilityArea", "5", "" }, new[] { "DamagevsBosses", "5", "" }, new[] { "HPRegen", "5", "" }, new[] { "MovementSpeed2", "3", "" }, new[] { "ImmunityAfterHit", "5", "" },
        };

        public static List<TStep> TeamSteps(List<TNode> nodes)
        {
            var steps = new List<TStep>();
            foreach (var row in Team)
            {
                var n = nodes.FirstOrDefault(x => x.Key.EndsWith("_" + row[0], StringComparison.OrdinalIgnoreCase));
                if (n != null) steps.Add(new TStep { Node = n, To = Math.Min(int.Parse(row[1]), n.Max), Why = row[2] });
            }
            Rest(nodes, steps);
            return steps;
        }

        // ---- a survivor's tree: starting abilities, the cheap tier-1 weapons, evolutions, the main weapon line, rank III
        // abilities, badges and synergies as their ranks open, rank V passives, then everything that is left
        public static List<TStep> ClassSteps(List<TNode> nodes)
        {
            var steps = new List<TStep>();
            Action<TNode, int, string> push = (n, to, why) => { if (n != null) steps.Add(new TStep { Node = n, To = Math.Min(to, n.Max), Why = why }); };
            Func<int, TKind, List<TNode>> kind = (r, k) => nodes.Where(n => n.Rank == r && n.Kind == k).OrderBy(n => n.Slot).ToList();
            Func<List<TNode>, List<TNode>> byTier = list => list.OrderBy(n => TierRank(n.Tier)).ThenBy(n => n.Slot).ToList();
            Func<TNode, TNode> evoOf = a => nodes.FirstOrDefault(n => n.Kind == TKind.Evolution && (n.BaseKey == a.Key || n.Prereqs.Contains(a.Key)));
            Func<TNode, string> tierTxt = a => a.Tier.Length > 0 ? " (" + a.Tier + " tier)" : "";

            var w1 = kind(1, TKind.Weapon); var a1 = byTier(kind(1, TKind.Ability)); var a3 = byTier(kind(3, TKind.Ability));
            var fork = kind(2, TKind.Weapon);
            var mainFork = fork.OrderByDescending(n => n.GuideBranch).ThenByDescending(n => n.Level).ThenBy(n => n.Slot).FirstOrDefault();
            var final = kind(3, TKind.Weapon).Concat(kind(4, TKind.Weapon)).Concat(kind(5, TKind.Weapon)).ToList();

            foreach (var a in a1) push(a, 2, "Cheap damage and cooldown on a starting ability" + tierTxt(a) + ".");
            if (w1.Count > 1) push(w1[1], 2, "The second starting weapon carries the early run.");
            foreach (var a in a1) push(a, 3, "");
            foreach (var w in w1) push(w, w.Max, "Tier-1 weapon levels are cheap and speed up the start.");
            foreach (var a in a1) push(evoOf(a), 1, "Unlocks both evolutions of " + a.Name + ": the biggest spike in the tree.");
            if (mainFork != null) push(mainFork, 3, mainFork.BranchWhy.Length > 0 ? mainFork.BranchWhy : mainFork.GuideBranch ? "The guides' weapon branch for this survivor." : "One tier-2 weapon first: the one you levelled most.");
            foreach (var a in a1) push(a, a.Max, "Max the starting abilities" + tierTxt(a) + ".");
            if (mainFork != null) push(mainFork, mainFork.Max, "");
            foreach (var w in final) push(w, 3, "The final weapon of the line.");
            foreach (var a in a3) push(a, 3, "Rank III ability" + tierTxt(a) + ".");
            foreach (var b in kind(2, TKind.Badge)) push(b, 1, b.Name + ": only matters if you equip it.");
            foreach (var s in kind(2, TKind.Synergy)) push(s, 1, "Pays off when both survivors are on the squad.");
            foreach (var w in final) push(w, w.Max, "");
            foreach (var a in a3) push(a, a.Max, "");
            foreach (var b in kind(3, TKind.Badge)) push(b, 1, b.Name + ": only matters if you equip it.");
            foreach (var s in kind(3, TKind.Synergy)) push(s, 1, "Pays off when both survivors are on the squad.");
            foreach (var a in a3) push(evoOf(a), 1, "Unlocks both evolutions of " + a.Name + ".");
            foreach (var b in kind(4, TKind.Badge)) push(b, 1, b.Name + ": only matters if you equip it.");
            foreach (var s in kind(4, TKind.Synergy)) push(s, 1, "Pays off when both survivors are on the squad.");
            var p5 = nodes.Where(n => n.Kind == TKind.Passive).OrderByDescending(PassiveScore).ThenBy(n => n.Slot).ToList();
            foreach (var p in p5) push(p, 1, "Rank V passive: always on.");
            foreach (var p in p5) push(p, p.Max, "Passives scale to level " + p.Max + " (1 + 3 + 5 points).");
            foreach (var s in kind(5, TKind.Synergy)) push(s, 1, "Pays off when both survivors are on the squad.");
            foreach (var w in fork) if (w != mainFork) push(w, w.Max, "The other tier-2 weapon, for the runs that offer it.");
            Rest(nodes, steps);
            return steps;
        }

        // whatever no rule named, to its max, in tree order - the plan always ends with every node maxed
        static void Rest(List<TNode> nodes, List<TStep> steps)
        {
            foreach (var n in nodes.OrderBy(x => x.Rank).ThenBy(x => x.Slot))
            {
                int top = steps.Where(s => s.Node == n).Select(s => s.To).DefaultIfEmpty(n.Min).Max();
                if (top < n.Max) steps.Add(new TStep { Node = n, To = n.Max, Why = "" });
            }
        }

        static int TierRank(string tier)
        {
            switch ((tier ?? "").ToUpperInvariant()) { case "S": return 0; case "A": return 1; case "": return 2; case "B": return 3; default: return 4; }
        }

        static double PassiveScore(TNode n)
        {
            string t = (n.Desc ?? "").ToLowerInvariant();
            if (t.Contains("weapon damage") || t.Contains("ability damage")) return 3;
            if (t.Contains("attack speed") || t.Contains("cooldown")) return 2.5;
            if (t.Contains("critical")) return 2;
            return 1;
        }

        sealed class Sim { public readonly List<TBuy> Bought = new List<TBuy>(); public int Left; public TBuy SaveFor; }

        static Sim Simulate(List<TStep> steps, int budget, Dictionary<TNode, int> alloc)
        {
            var sim = new Sim { Left = budget };
            foreach (var step in steps)
            {
                var n = step.Node;
                int cur; if (!alloc.TryGetValue(n, out cur)) cur = Math.Max(n.Level, n.Min);
                if (cur >= step.To) continue;
                if (!n.RankOpen) continue;
                bool blocked = false;
                foreach (var key in n.Prereqs)
                {
                    var pre = alloc.Keys.FirstOrDefault(x => x.Key == key);
                    if (pre != null && alloc[pre] < 1) { blocked = true; break; }
                }
                if (blocked) continue;
                int lvl = cur;
                while (lvl < step.To)
                {
                    int c = n.CostFrom(lvl);
                    if (c > sim.Left)
                    {
                        if (sim.SaveFor == null && c != int.MaxValue) sim.SaveFor = new TBuy { Node = n, From = lvl, To = lvl + 1, Cost = c, Why = WhyOf(steps, step) };
                        break;
                    }
                    sim.Left -= c; lvl++;
                    var last = sim.Bought.Count > 0 ? sim.Bought[sim.Bought.Count - 1] : null;
                    if (last != null && last.Node == n && last.To == lvl - 1) { last.To = lvl; last.Cost += c; }
                    else sim.Bought.Add(new TBuy { Node = n, From = lvl - 1, To = lvl, Cost = c, Why = WhyOf(steps, step) });
                }
                alloc[n] = lvl;
                if (sim.Left <= 0) break;              // like the PC app: leftover points go to cheaper steps further down,
            }                                          // the first step that did not fit stays the thing to save for
            return sim;
        }

        // a step without its own reason (a later "to 5" of the same node) borrows the node's first reason
        static string WhyOf(List<TStep> steps, TStep step)
        {
            if (step.Why.Length > 0) return step.Why;
            var first = steps.FirstOrDefault(s => s.Node == step.Node && s.Why.Length > 0);
            return first != null ? first.Why : "";
        }

        public static TAdvice Advise(List<TNode> nodes, List<TStep> steps, int points)
        {
            var advice = new TAdvice { Points = points };
            Func<Dictionary<TNode, int>> current = () => nodes.ToDictionary(n => n, n => Math.Max(n.Level, n.Min));
            var now = Simulate(steps, points, current());
            advice.Left = now.Left; advice.SaveFor = now.SaveFor;
            // one entry per node, in the order of its first purchase
            foreach (var b in now.Bought)
            {
                var same = advice.Now.FirstOrDefault(x => x.Node == b.Node);
                if (same != null) { same.To = b.To; same.Cost += b.Cost; }
                else advice.Now.Add(new TBuy { Node = b.Node, From = b.From, To = b.To, Cost = b.Cost, Why = b.Why, Order = advice.Now.Count + 1 });
            }
            var ahead = Simulate(steps, points + 40, current());
            foreach (var b in ahead.Bought)
            {
                if (advice.Later.Count >= 3) break;
                var mine = advice.Now.FirstOrDefault(x => x.Node == b.Node);
                if (mine != null && b.To <= mine.To) continue;
                if (advice.SaveFor != null && advice.SaveFor.Node == b.Node) continue;
                if (advice.Later.Any(x => x.Node == b.Node)) continue;
                advice.Later.Add(new TBuy { Node = b.Node, From = mine != null ? mine.To : b.From, To = b.To, Cost = b.Cost, Why = b.Why });
            }
            advice.Complete = advice.Now.Count == 0 && advice.SaveFor == null;
            return advice;
        }
    }
}
