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
using System.Globalization;
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
        public bool GuideBranch;          // weapon: the pick at the fork (the selected build's branch, else the guides')
        public int WeaponDepth = -1;      // weapon: 0 = the starting weapon, 1 = its upgrade, 2 = one of the three tier-3 weapons (the
                                          // fork); -1 = not known, then the column decides (column 1 = the first two, the rest = the fork)
        public string BranchWhy = "";     // set when the pick comes from the player's build
        public int BadgeId = -1;          // 0.16.0 (C16-07): a badge node's badgeBaseId (the id of LoadoutInput.Levels); -1 = not known
        public object Ui;                 // the UISkillTreeNode it was read from

        public int CostFrom(int level)
        {
            if (level >= Max) return int.MaxValue;
            if (level < Min) return 0;
            return level < Costs.Length ? Costs[level] : int.MaxValue;
        }
    }

    // 0.16.0 (C16-07): Hold = bought only once every other open, buyable step of the tab is complete (a badge no survivor's advice
    // equips yet); Whole = a multi-level badge investment that only pays at its target level, never partly bought
    internal sealed class TStep { public TNode Node; public int To; public string Why = ""; public bool Hold, Whole; }

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

        // ---- a survivor's tree: starting abilities, the cheap first two weapons, evolutions, the main weapon branch, rank III
        // abilities, badges and synergies as their ranks open, rank V passives, then everything that is left
        //
        // 0.12.2 (F05): the weapon nodes are sorted by the weapon they boost, not by their column. The fifth weapon of a
        // line sits in column 3, and the plan took it for "the final weapon of the line" and pushed it to level 3 before
        // rank III abilities, badges and synergies - for a survivor whose build takes another branch (a Training Yard
        // advice to buy that node 2>3 was followed on the Deck while the build, and every card in the run, said the
        // other branch). It is the third tier-3 weapon of the fork: the main branch is chosen among all three, and the
        // other two come last, for the runs that offer them.
        //
        // 0.16.0 (C16-07, OD5): with <paramref name="badges"/> (the badge advice of every survivor, YardBadges) a badge node is valued by
        // that advice: at its rank's stage it is planned to the level where its points pay best (a whole chunk, never partly bought),
        // pinned on a build it is unlocked first; after the rank V passives its further levels follow while they clear the floor; a badge
        // no survivor's advice equips (or whose next levels add little) is HELD - bought only when every other open step of the tab is
        // done. Without it (null: [Advice] YardBadges off, the General tab, no badge readable) the steps are exactly 0.15's.
        public static List<TStep> ClassSteps(List<TNode> nodes, YardBadges badges = null)
        {
            var steps = new List<TStep>();
            Action<TNode, int, string> push = (n, to, why) => { if (n != null) steps.Add(new TStep { Node = n, To = Math.Min(to, n.Max), Why = why }); };
            if (badges != null) badges.Notes.Clear();
            var planned = new Dictionary<TNode, int>();
            Func<TNode, int> curOf = n => { int p; return planned.TryGetValue(n, out p) ? p : Math.Max(n.Level, n.Min); };
            Func<IEnumerable<TNode>> badgeNodes = () => nodes.Where(n => n.Kind == TKind.Badge).OrderBy(n => n.Rank).ThenBy(n => n.Slot);
            // (a) a rank's badges as the rank opens
            Action<int> stage = r =>
            {
                var list = nodes.Where(n => n.Rank == r && n.Kind == TKind.Badge).OrderBy(n => n.Slot).ToList();
                if (badges == null) { foreach (var b in list) push(b, 1, b.Name + ": only matters if you equip it."); return; }
                var rated = list.Select(n =>
                {
                    var b = badges.Of(n); int cur = Math.Max(n.Level, n.Min), to = -1; double v = -1;
                    if (b != null && !badges.Best(n, cur, badges.Reach, out to, out v)) { to = -1; v = 0; }
                    return new { n, b, cur, to, v, pin = b != null && b.PinnedBy.Length > 0 && cur == 0 };
                }).OrderByDescending(x => x.pin).ThenByDescending(x => x.v).ThenBy(x => x.n.Slot).ToList();
                foreach (var x in rated)
                {
                    if (x.b == null) { push(x.n, 1, x.n.Name + ": only matters if you equip it."); continue; }
                    if (x.pin)
                    {
                        push(x.n, 1, YardWords.W7(x.b.PinnedBy)); steps[steps.Count - 1].Whole = true; planned[x.n] = 1;
                        badges.Notes.Add(x.n.Name + " 0>1 pinned");
                        continue;
                    }
                    if (x.to > 0 && x.v >= badges.Floor)
                    {
                        push(x.n, x.to, x.b.EquipsNow > 0 ? YardWords.W1(x.b.WhoNow, badges) : YardWords.W2(x.to, x.b.WhoAt[x.to], badges)); steps[steps.Count - 1].Whole = true; planned[x.n] = x.to;
                        badges.Notes.Add(x.n.Name + " " + x.cur + ">" + x.to + " stage " + x.b.EquipsAt[x.to] + "/" + badges.Survivors + " " + YardWords.P(x.v) + "/pt");
                    }
                }
            };
            // (b) after the rank V passives: the further levels that clear the floor, the best per point first
            Action levels = () =>
            {
                if (badges == null) return;
                for (int guard = 0; guard < 25; guard++)
                {
                    TNode bestN = null; int bestTo = -1, bestFrom = 0; double bestV = double.NegativeInfinity;
                    foreach (var n in badgeNodes())
                    {
                        var b = badges.Of(n); if (b == null) continue;
                        int cur = curOf(n);
                        if (cur >= b.Max) continue;
                        int to; double v;
                        if (badges.Best(n, cur, b.Max - cur, out to, out v) && v > bestV + 1e-9) { bestN = n; bestTo = to; bestV = v; bestFrom = cur; }
                    }
                    if (bestN == null || bestV < badges.Floor) break;
                    var bb = badges.Of(bestN);
                    push(bestN, bestTo, bb.EquipsAt[bestFrom] > 0 ? YardWords.W3(bb.WhoAt[bestTo], badges) : YardWords.W2(bestTo, bb.WhoAt[bestTo], badges)); steps[steps.Count - 1].Whole = true;
                    planned[bestN] = bestTo;
                    badges.Notes.Add(bestN.Name + " " + bestFrom + ">" + bestTo + " levels " + bb.EquipsAt[bestTo] + "/" + badges.Survivors + " " + YardWords.P(bestV) + "/pt");
                }
            };
            // (c) what is left of every badge: held, bought once the rest of the tab is done
            Action late = () =>
            {
                if (badges == null) return;
                foreach (var n in badgeNodes())
                {
                    var b = badges.Of(n); if (b == null) continue;
                    int top = curOf(n);
                    if (top >= n.Max) continue;
                    string why, note;
                    if (planned.ContainsKey(n) || b.EquipsNow > 0)
                    {
                        int to; double v; if (!badges.Best(n, top, b.Max - top, out to, out v)) v = 0;
                        why = YardWords.W6(badges); note = "next levels " + YardWords.P(Math.Max(0, v)) + "/pt";
                    }
                    else
                    {
                        int from = 0; for (int l = b.Level + 1; l <= b.Max; l++) if (b.EquipsAt[l] > 0) { from = l; break; }
                        if (from == 0) { why = YardWords.W4(b.Max, badges); note = "never"; }
                        else { why = YardWords.W5(from, badges); note = "from level " + from; }
                    }
                    push(n, n.Max, why); steps[steps.Count - 1].Hold = true;
                    badges.Notes.Add(n.Name + " held (" + note + ")");
                }
            };
            Func<int, TKind, List<TNode>> kind = (r, k) => nodes.Where(n => n.Rank == r && n.Kind == k).OrderBy(n => n.Slot).ToList();
            Func<List<TNode>, List<TNode>> byTier = list => list.OrderBy(n => TierRank(n.Tier)).ThenBy(n => n.Slot).ToList();
            Func<TNode, TNode> evoOf = a => nodes.FirstOrDefault(n => n.Kind == TKind.Evolution && (n.BaseKey == a.Key || n.Prereqs.Contains(a.Key)));
            Func<TNode, string> tierTxt = a => a.Tier.Length > 0 ? " (" + a.Tier + " tier)" : "";

            var a1 = byTier(kind(1, TKind.Ability)); var a3 = byTier(kind(3, TKind.Ability));
            var weapons = nodes.Where(n => n.Kind == TKind.Weapon).ToList();
            Func<TNode, bool> forked = n => n.WeaponDepth >= 0 ? n.WeaponDepth >= 2 : n.Rank >= 2;
            var w1 = weapons.Where(n => !forked(n)).OrderBy(n => n.WeaponDepth).ThenBy(n => n.Rank).ThenBy(n => n.Slot).ToList();
            var fork = weapons.Where(forked).OrderBy(n => n.Rank).ThenBy(n => n.Slot).ToList();
            var mainFork = fork.OrderByDescending(n => n.GuideBranch).ThenByDescending(n => n.Level).ThenBy(n => n.Rank).ThenBy(n => n.Slot).FirstOrDefault();

            foreach (var a in a1) push(a, 2, "Cheap damage and cooldown on a starting ability" + tierTxt(a) + ".");
            if (w1.Count > 1) push(w1[1], 2, "The second starting weapon carries the early run.");
            foreach (var a in a1) push(a, 3, "");
            foreach (var w in w1) push(w, w.Max, "Levels of the first two weapons are cheap and speed up the start.");
            foreach (var a in a1) push(evoOf(a), 1, "Unlocks both evolutions of " + a.Name + ": the biggest spike in the tree.");
            if (mainFork != null) push(mainFork, 3, mainFork.BranchWhy.Length > 0 ? mainFork.BranchWhy : mainFork.GuideBranch ? "The guides' weapon branch for this survivor." : "One tier-3 weapon first: the one you levelled most.");
            foreach (var a in a1) push(a, a.Max, "Max the starting abilities" + tierTxt(a) + ".");
            if (mainFork != null) push(mainFork, mainFork.Max, "");
            foreach (var a in a3) push(a, 3, "Rank III ability" + tierTxt(a) + ".");
            stage(2);
            foreach (var s in kind(2, TKind.Synergy)) push(s, 1, "Pays off when both survivors are on the squad.");
            foreach (var a in a3) push(a, a.Max, "");
            stage(3);
            foreach (var s in kind(3, TKind.Synergy)) push(s, 1, "Pays off when both survivors are on the squad.");
            foreach (var a in a3) push(evoOf(a), 1, "Unlocks both evolutions of " + a.Name + ".");
            stage(4);
            foreach (var s in kind(4, TKind.Synergy)) push(s, 1, "Pays off when both survivors are on the squad.");
            var p5 = nodes.Where(n => n.Kind == TKind.Passive).OrderByDescending(PassiveScore).ThenBy(n => n.Slot).ToList();
            foreach (var p in p5) push(p, 1, "Rank V passive: always on.");
            foreach (var p in p5) push(p, p.Max, "Passives scale to level " + p.Max + " (1 + 3 + 5 points).");
            foreach (var s in kind(5, TKind.Synergy)) push(s, 1, "Pays off when both survivors are on the squad.");
            levels();
            foreach (var w in fork) if (w != mainFork) push(w, w.Max, "The other branches of the fork, for the runs that offer them.");
            late();
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

        // the node's rank is open and its prerequisites are bought (in this walk)
        static bool Open(TNode n, Dictionary<TNode, int> alloc)
        {
            if (!n.RankOpen) return false;
            foreach (var key in n.Prereqs)
            {
                var pre = alloc.Keys.FirstOrDefault(x => x.Key == key);
                if (pre != null && alloc[pre] < 1) return false;
            }
            return true;
        }

        // 0.16.0 (C16-07): two passes. Pass 1 walks every step that is not held, as 0.15 did, except that a Whole step (a badge chunk that
        // only pays at its target level) is bought whole or not at all - it becomes the thing to save for and the leftover still flows to
        // cheaper steps further down. Pass 2, only when pass 1 left no open, buyable step unfinished, walks the held steps (a held step that
        // does not fit is saved for, so a tab with only held badges left is never called complete). Without Hold / Whole steps: 0.15.
        static Sim Simulate(List<TStep> steps, int budget, Dictionary<TNode, int> alloc)
        {
            var sim = new Sim { Left = budget };
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var step in steps)
                {
                    if (step.Hold != (pass == 1)) continue;
                    var n = step.Node;
                    int cur; if (!alloc.TryGetValue(n, out cur)) cur = Math.Max(n.Level, n.Min);
                    if (cur >= step.To) continue;
                    if (!Open(n, alloc)) continue;
                    if (step.Whole)
                    {
                        int total = 0;
                        for (int l = cur; l < step.To; l++) { int c1 = n.CostFrom(l); if (c1 == int.MaxValue) { total = int.MaxValue; break; } total += c1; }
                        if (total == int.MaxValue) continue;
                        if (total > sim.Left)
                        {
                            if (sim.SaveFor == null) sim.SaveFor = new TBuy { Node = n, From = cur, To = step.To, Cost = total, Why = WhyOf(steps, step) };
                            continue;                  // never a part of it, whatever SaveFor holds
                        }
                    }
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
                    if (sim.Left <= 0) return sim;     // like the PC app: leftover points go to cheaper steps further down,
                }                                      // the first step that did not fit stays the thing to save for
                if (pass == 0 && steps.Any(st => !st.Hold && Unfinished(st, alloc))) return sim;
            }
            return sim;
        }

        // a step not held that pass 1 left open and buyable: below its target, its next level for sale, its rank open, its prerequisites bought
        static bool Unfinished(TStep st, Dictionary<TNode, int> alloc)
        {
            int cur; if (!alloc.TryGetValue(st.Node, out cur)) cur = Math.Max(st.Node.Level, st.Node.Min);
            return cur < st.To && st.Node.CostFrom(cur) != int.MaxValue && Open(st.Node, alloc);
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

    /// <summary>0.15.0 (C15-08): one node whose level changed between two reads of the same Training Yard tab.</summary>
    internal sealed class TChange
    {
        public TNode Node;                // as the later read has it
        public int From, To;
        public int Advice;                // a purchase: its place in the SPEND list shown before it (1 ..); 0 = not advised (or a refund)
        public bool Bought { get { return To > From; } }

        /// <summary>The log line after "[yard] ": "bought Weapon Damage 1>2 (advice #1)", "bought Armor 0>1 (not advised)",
        /// "refunded Armor 1>0".</summary>
        public string Line
        {
            get
            {
                string head = (Bought ? "bought " : "refunded ") + Node.Name + " " + From + ">" + To;
                return Bought ? head + (Advice > 0 ? " (advice #" + Advice + ")" : " (not advised)") : head;
            }
        }
    }

    /// <summary>0.15.0 (C15-08): the Training Yard purchase log - what the player bought or refunded between two reads of a tab, and
    /// whether a purchase followed the advice on screen. The game says nothing when a node is bought; the levels of two consecutive
    /// reads do. It makes the advice measurable (does the player follow it, which place) before 0.16.0 steers the plan by the badge
    /// advice. Pure: TNode values only, so the bench drives it.</summary>
    internal static class TreeDiff
    {
        /// <summary>A node's identity in its tab: its save key and its place (two nodes never share both, even with keys missing).</summary>
        public static string Id(TNode n) { return (n.Key ?? "") + "@" + n.Rank + "." + n.Slot; }

        /// <summary>The levels of one read by <see cref="Id"/>: what the next read of the same tab is compared with.</summary>
        public static Dictionary<string, int> Levels(IEnumerable<TNode> nodes)
        {
            var d = new Dictionary<string, int>();
            if (nodes != null) foreach (var n in nodes) if (n != null) d[Id(n)] = n.Level;
            return d;
        }

        /// <summary>Every node of <paramref name="now"/> whose level differs from <paramref name="before"/>, in the read's (the tree's)
        /// order; a node the earlier read did not have is left out (another tab or survivor starts a record of its own). A purchase
        /// carries its place in <paramref name="advice"/> - the SPEND list worked out from the earlier read, the one on screen when the
        /// player bought - when the node is in it and the bought levels start below the level the advice took it to (a level past the
        /// advice is not advised).</summary>
        public static List<TChange> Diff(Dictionary<string, int> before, List<TNode> now, TAdvice advice)
        {
            var list = new List<TChange>();
            if (before == null || now == null) return list;
            foreach (var n in now)
            {
                if (n == null) continue;
                string id = Id(n);
                int was;
                if (!before.TryGetValue(id, out was) || was == n.Level) continue;
                var ch = new TChange { Node = n, From = was, To = n.Level };
                if (ch.Bought && advice != null)
                {
                    var b = advice.Now.FirstOrDefault(x => x.Node != null && Id(x.Node) == id);
                    if (b != null && ch.From < b.To) ch.Advice = b.Order;
                }
                list.Add(ch);
            }
            return list;
        }
    }

    /// <summary>0.16.0 (C16-07): one badge node of a Training Yard tab as the badge advice of every survivor values it.</summary>
    internal sealed class YardBadge
    {
        public int Id, Level, Max, EquipsNow;     // Level = the node's level now (at least its minimum); Max = min(node max, badge max)
        public string Name = "", PinnedBy = "";   // PinnedBy: "your Test build" / "the Test build" - the first survivor whose build pins it while locked
        public double[] Gain;                     // by level: badge-advice points over the level now, summed over the survivors counted
        public int[] EquipsAt;                    // by level: how many survivors' advice would equip it there
        public List<string>[] WhoAt;              // by level: which (class names)
        public readonly List<string> WhoNow = new List<string>();
    }

    /// <summary>0.16.0 (C16-07): the badge demand of one tab - what the run setup screen's badge advice, worked out for every survivor
    /// the player can lead, makes of each badge node and its levels. Pure (LoadoutAdvice values in, no game types), so the bench drives it.
    /// Unit of <see cref="Best"/>: badge-advice points (% squad damage over a run) per Training Yard point, averaged over the survivors.</summary>
    internal sealed class YardBadges
    {
        public string Context = "";               // "Normal I" (Loadout.RunName)
        public int Survivors;
        public double Floor = 0.15;               // knowledge.json yardRules.badgeFloor: the least a planned chunk must add per point
        public int Reach = 2;                     // yardRules.badgeReach: the levels a rank's stage plans at once
        public readonly Dictionary<string, YardBadge> ByKey = new Dictionary<string, YardBadge>();     // by TreeDiff.Id(node)
        public readonly List<string> Notes = new List<string>();                                     // filled by TreePlan.ClassSteps

        public YardBadge Of(TNode n) { YardBadge b; return n != null && ByKey.TryGetValue(TreeDiff.Id(n), out b) ? b : null; }

        /// <summary>The target level in (from, from + reach] whose points per Training Yard point are highest (the smallest level on a tie);
        /// false when the node has no demand or no level is for sale.</summary>
        public bool Best(TNode n, int from, int reach, out int to, out double perPoint)
        {
            to = -1; perPoint = 0;
            var b = Of(n); if (b == null) return false;
            int top = Math.Min(Math.Min(n.Max, b.Max), from + reach);
            int cost = 0; double best = double.NegativeInfinity;
            for (int l = from + 1; l <= top; l++)
            {
                int c = n.CostFrom(l - 1); if (c == int.MaxValue) break;
                cost += c;
                if (cost <= 0) continue;
                double v = (b.Gain[l] - b.Gain[from]) / cost / Math.Max(1, Survivors);
                if (v > best + 1e-9) { best = v; to = l; }
            }
            if (to < 0) return false;
            perPoint = best;
            return true;
        }

        /// <summary>The tail of '[yard] badges of &lt;tree&gt; ...': "(Normal I, 9 survivors, floor 0.15, reach 2): Growth Badge 1>3 stage 9/9 0.41/pt; ...".</summary>
        public string Line
        {
            get
            {
                return "(" + Context + ", " + Survivors + (Survivors == 1 ? " survivor" : " survivors") + ", floor " + Floor.ToString("0.00", CultureInfo.InvariantCulture) + ", reach " + Reach + "): "
                    + (Notes.Count > 0 ? string.Join("; ", Notes) : "nothing to plan");
            }
        }

        /// <summary>The demand of the badge nodes of <paramref name="tab"/> (each with its BadgeId) from one advice per survivor
        /// (<paramref name="advices"/>: class name -> the badge advice of the run setup screen for that leader). A survivor counts a level
        /// when its advice equips the badge now (the LEVEL hint's gain over the level now) or when the badge would beat its weakest score
        /// pick there (the difference; a free slot = 0); a skipped, unrated or off-grid badge is not counted for that survivor.</summary>
        public static YardBadges Build(IList<TNode> tab, IList<KeyValuePair<string, LoadoutAdvice>> advices, IList<BadgeFacts> all, Knowledge k, double floor, int reach, string context)
        {
            var d = new YardBadges { Context = context ?? "", Survivors = advices == null ? 0 : advices.Count, Floor = floor, Reach = reach };
            if (tab == null || advices == null || all == null) return d;
            foreach (var n in tab)
            {
                if (n == null || n.Kind != TKind.Badge || n.BadgeId < 0) continue;
                var f = all.FirstOrDefault(x => x.Id == n.BadgeId); if (f == null) continue;
                int L = Math.Max(n.Level, n.Min), M = Math.Min(n.Max, f.Max), size = Math.Max(L, M) + 1;
                var b = new YardBadge { Id = f.Id, Name = n.Name, Level = L, Max = M, Gain = new double[size], EquipsAt = new int[size], WhoAt = new List<string>[size] };
                for (int i = 0; i < size; i++) b.WhoAt[i] = new List<string>();
                foreach (var kv in advices)
                {
                    var a = kv.Value; if (a == null || a.Input == null) continue;
                    var row = a.RowOf(f.Id);
                    if (row == null || !row.Rated || row.Skipped || !row.OnGrid) continue;
                    var shape = a.Input.Shape; var ctx = a.Input.Ctx;
                    bool inEq = a.Picks.Any(p => p.Badge.Id == f.Id && !p.Forced);
                    if (inEq) { b.EquipsNow++; b.WhoNow.Add(kv.Key); }
                    int free = a.Input.Slots - a.Picks.Count;
                    var scored = a.Picks.Where(p => p.Source == "score").ToList();
                    double weakest = free > 0 ? 0 : scored.Count > 0 ? scored.Min(p => p.Score) : double.PositiveInfinity;
                    var proj0 = new Dictionary<string, int>(shape.Projected, StringComparer.OrdinalIgnoreCase);
                    double now = inEq ? Loadout.Score(f, Math.Max(L, 1), shape, ctx, new Dictionary<string, int>(), k, null) : 0;
                    for (int l = L + 1; l <= M; l++)
                    {
                        if (inEq)
                        {   // the LEVEL hint's convention (Loadout.Recommend): no projection, the gain over the level now
                            b.Gain[l] += Loadout.Score(f, l, shape, ctx, new Dictionary<string, int>(), k, null) - now;
                            b.EquipsAt[l]++; b.WhoAt[l].Add(kv.Key);
                        }
                        else
                        {
                            double v = Loadout.Score(f, l, shape, ctx, proj0, k, null);
                            if (v > weakest) { b.Gain[l] += v - weakest; b.EquipsAt[l]++; b.WhoAt[l].Add(kv.Key); }
                        }
                    }
                    if (b.PinnedBy.Length == 0 && a.Unlocks.Any(u => u.Pinned && u.Row != null && u.Row.Badge.Id == f.Id)) b.PinnedBy = Loadout.BuildRef(shape);
                }
                // a badge equipped now and planned from its level reads "equips it for ..." (W3), never "At level N ..."
                if (L < size) { b.EquipsAt[L] = b.EquipsNow; b.WhoAt[L] = new List<string>(b.WhoNow); }
                d.ByKey[TreeDiff.Id(n)] = b;
            }
            return d;
        }

        /// <summary>What the advices depend on: the run (mode, difficulty, slots), every badge level, each survivor with its build, the
        /// doctrine and the knowledge. Not the tab (every tab reads the same advices) and not RankOpen (Recommend reads it only for the
        /// non-pinned UNLOCK hint, which Build never reads).</summary>
        public static string Key(string mode, int diff, int slots, IDictionary<int, int> levels, IEnumerable<string> survivorSigs, string doctrine, int knowledgeId)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(mode).Append('|').Append(diff).Append('|').Append(slots).Append('|');
            if (levels != null) foreach (var kv in levels.OrderBy(x => x.Key)) sb.Append(kv.Key).Append(':').Append(kv.Value).Append(',');
            sb.Append('|').Append(survivorSigs == null ? "" : string.Join(";", survivorSigs)).Append('|').Append(doctrine).Append('|').Append(knowledgeId);
            return sb.ToString();
        }
    }

    /// <summary>0.16.0 (C16-07): the badge advices of every survivor, worked out once per key (not per tab, not per tick).</summary>
    internal sealed class YardAdviceCache
    {
        public string Key = "";
        public int Computes;
        public List<KeyValuePair<string, LoadoutAdvice>> Advices;
        public List<KeyValuePair<string, LoadoutAdvice>> Get(string key, Func<List<KeyValuePair<string, LoadoutAdvice>>> compute)
        {
            if (Advices == null || key != Key) { Advices = compute(); Key = key; Computes++; }
            return Advices;
        }
    }

    /// <summary>0.16.0 (C16-07): the WHY of a badge step (printable ASCII, 76 characters at most before Names.Text; plain words - no score,
    /// no points per point: the numbers go to the log).</summary>
    internal static class YardWords
    {
        /// <summary>One name, else "N of S survivors".</summary>
        public static string Who(IList<string> names, int survivors) { return names != null && names.Count == 1 ? names[0] : (names == null ? 0 : names.Count) + " of " + survivors + " survivors"; }
        public static string W1(IList<string> whoNow, YardBadges d) { return "The badge advice equips it for " + Who(whoNow, d.Survivors) + " (" + d.Context + ")."; }
        public static string W2(int to, IList<string> whoAt, YardBadges d) { return "At level " + to + " the advice equips it for " + Who(whoAt, d.Survivors) + " (" + d.Context + ")."; }
        public static string W3(IList<string> whoAt, YardBadges d) { return "More levels: the advice equips it for " + Who(whoAt, d.Survivors) + " (" + d.Context + ")."; }
        public static string W4(int max, YardBadges d) { return "No survivor's badge advice equips it, even at level " + max + " (" + d.Context + ")."; }
        public static string W5(int level, YardBadges d) { return "The badge advice equips it only from level " + level + " (" + d.Context + ")."; }
        public static string W6(YardBadges d) { return "Its next levels add little for the points (" + d.Context + ")."; }
        /// <summary>A pinned badge (exempt from the cap: it carries a build name, like "The weapon branch of your X build.").</summary>
        public static string W7(string pinnedBy) { return "Pinned on " + pinnedBy + ": unlock it."; }
        /// <summary>A number of the log lines: two decimals, invariant.</summary>
        public static string P(double x) { return x.ToString("0.00", CultureInfo.InvariantCulture); }
    }
}
