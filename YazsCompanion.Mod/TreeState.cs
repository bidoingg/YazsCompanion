// Reads the Training Yard tab that is on screen into TNode values for TreePlan: every node of the tab's columns with
// its kind (from the upgrade's class), rank (the column), level, costs, prerequisites and whether its rank is open
// (the node shows its lock or not - exactly what the player sees), plus the points on hand (the number the view
// prints next to the diamond, so it is right for the General tab and for every survivor).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using CT = GamePlayer.CharacterType;

namespace YazsCompanion
{
    internal static class TreeState
    {
        public static List<TNode> Read(UISkillTreeSkillsContainer container, bool isTeam, out string treeName)
        {
            var nodes = new List<TNode>();
            ClassProperties cp = null; try { cp = container.targetClassProperties; } catch { }
            CT cls = CT.None; try { if (cp != null) cls = cp.characterType; } catch { }
            treeName = isTeam || cls == CT.None ? "General" : G.ClassName(cls);
            // the build the player follows for this survivor decides the branch and the ability order; Auto = the guides
            _build = isTeam ? null : Builds.For(treeName);
            string branch = null;
            if (_build != null && !string.IsNullOrEmpty(_build.Branch)) branch = _build.Branch;
            else if (!isTeam) Knowledge.Current.WeaponBranch.TryGetValue(treeName, out branch);

            var cols = container._columns;
            if (cols == null) return nodes;
            for (int c = 0; c < cols.Length; c++)
            {
                var col = cols[c]; if (col == null || col.nodes == null) continue;
                for (int i = 0; i < col.nodes.Length; i++)
                {
                    var ui = col.nodes[i]; if (ui == null) continue;
                    SkillTreeUpgradeBase up = null; try { up = ui.attachedSkillTreeUpgrade; } catch { }
                    if (up == null) continue;
                    var n = new TNode { Ui = ui, Rank = c + 1, Slot = i };
                    try { n.Key = up.GetSaveFileKey() ?? ""; } catch { }
                    if (n.Key.Length == 0) n.Key = G.Asset(up);
                    try { n.Name = up.GetName() ?? ""; } catch { }
                    if (n.Name.Length == 0) n.Name = n.Key;
                    n.Desc = G.NodeDesc(up);
                    n.Level = G.NodeLevel(up); n.Max = G.NodeMax(up);
                    try { n.Min = up.GetMinLevel(); } catch { try { n.Min = up.levelMin; } catch { } }
                    try { var costs = up.levelUpCosts; if (costs != null) { n.Costs = new int[costs.Length]; for (int k = 0; k < costs.Length; k++) n.Costs[k] = costs[k]; } } catch { }
                    try { n.Perm = up.isPermUpgrade; } catch { }
                    foreach (var pre in G.Each(up.prerequisites)) { if (pre == null) continue; try { n.Prereqs.Add(pre.GetSaveFileKey() ?? G.Asset(pre)); } catch { } }
                    bool locked = false; try { var lk = ui.containerLocked; locked = lk != null && lk.activeSelf; } catch { try { locked = !ui.IsAvailable(); } catch { } }
                    n.RankOpen = !locked;
                    Classify(n, up, branch);
                    nodes.Add(n);
                }
            }
            return nodes;
        }

        static Build _build;

        static void Classify(TNode n, SkillTreeUpgradeBase up, string guideBranch)
        {
            try
            {
                var w = up.TryCast<SkillTreeUpgradeWeaponBoost>();
                if (w != null)
                {
                    n.Kind = TKind.Weapon;
                    n.WeaponDepth = Ranker.WeaponDepth(w.targetWeapon);
                    string name = G.Name(w.targetWeapon);
                    n.GuideBranch = guideBranch != null && Ranker.SameName(name, guideBranch);
                    if (n.GuideBranch && _build != null && !string.IsNullOrEmpty(_build.Branch)) n.BranchWhy = "The weapon branch of your " + _build.Name + " build.";
                    return;
                }
                var a = up.TryCast<SkillTreeUpgradeAbilityBoost>();
                if (a != null) { n.Kind = TKind.Ability; n.Tier = TierOf(a.targetAbility); return; }
                var e = up.TryCast<SkillTreeUpgradeUnlockPowerup>();
                if (e != null)
                {
                    n.Kind = TKind.Evolution;
                    PowerupBase evo = null; try { evo = e.evolutionUnlockA ?? e.evolutionUnlockB; } catch { }
                    PowerupBase baseAbility = null; try { if (evo != null) baseAbility = evo.evolutionBaseAbility; } catch { }
                    if (baseAbility != null)
                    {
                        n.Tier = TierOf(baseAbility);
                        try { var node = baseAbility.skillTreeAbilityBoost; if (node != null) n.BaseKey = node.GetSaveFileKey() ?? ""; } catch { }
                    }
                    return;
                }
                if (up.TryCast<SkillTreeUpgradeUnlockSynergy>() != null) { n.Kind = TKind.Synergy; return; }
                // 0.16.0 (C16-07): the badge node's badge (badgeBaseId), in its own try: a field a game patch took away leaves the id at -1
                // (BadgeDemand then matches the node through the badges' own tree nodes)
                var bb = up.TryCast<SkillTreeUpgradeBadgeBoost>();
                if (bb != null) { n.Kind = TKind.Badge; try { n.BadgeId = LoadoutState.BadgeIdOf(bb); } catch { n.BadgeId = -1; } return; }
                if (up.TryCast<SkillTreeUpgradeTeamStatisticBoost>() != null) { n.Kind = TKind.Stat; return; }
                if (up.TryCast<SkillTreeUpgradeUnlockMechanic>() != null || up.TryCast<SkillTreeUpgradeItemSlot>() != null || up.TryCast<SkillTreeUpgradeBadgeSlot>() != null) { n.Kind = TKind.Mechanic; return; }
                if (up.TryCast<SkillTreeUpgradeValueBase>() != null) { n.Kind = TKind.Passive; return; }
            }
            catch { }
        }

        static string TierOf(PowerupBase ability)
        {
            if (ability == null) return "";
            if (_build != null)
            {
                // the build's own order stands in for the guides' tiers: first = S ... fourth = C, skipped = C
                string name = G.Name(ability);
                if (_build.Skips(name)) return "C";
                int i = _build.PriorityOf(name);
                if (i >= 0) return i == 0 ? "S" : i == 1 ? "A" : i == 2 ? "B" : "C";
            }
            string tier; return Knowledge.Current.AbilityTier.TryGetValue(G.Name(ability), out tier) ? tier : "";
        }

        // ---- 0.15.0 (C15-08): the purchase log. The levels of the last read of a tab are kept until the next read of the same tab
        // and survivor; what changed in between was bought or refunded (TreeDiff, TreePlan.cs - pure, in the bench).
        static Dictionary<string, int> _levels; static IntPtr _levelsTab; static string _levelsTree;

        /// <summary>Logs '[yard] bought &lt;node&gt; a&gt;b (advice #n | not advised)' and '[yard] refunded &lt;node&gt; a&gt;b' for every level
        /// that changed since the last read of the same tab and survivor, a purchase matched against <paramref name="shown"/> (the advice
        /// worked out from that read: what was on screen when the player bought); then this read becomes the record. A first read,
        /// another tab or another survivor logs nothing and starts the record again. Every purchase is logged, not once a session.</summary>
        public static void LogChanges(IntPtr tab, string tree, List<TNode> nodes, TAdvice shown)
        {
            if (_levels != null && tab == _levelsTab && tree == _levelsTree)
                foreach (var ch in TreeDiff.Diff(_levels, nodes, shown)) Plugin.Logger.LogInfo("[yard] " + ch.Line);
            _levels = TreeDiff.Levels(nodes); _levelsTab = tab; _levelsTree = tree;
        }

        /// <summary>The record is void (the view went, the advice was switched off): the next read starts a new one.</summary>
        public static void ForgetLevels() { _levels = null; _levelsTab = IntPtr.Zero; _levelsTree = null; }

        // ---- 0.16.0 (C16-07): the Training Yard's badge steering. The badge advice of the run setup screen, worked out for every survivor
        // the player can lead (the mode and difficulty of the last run setup visit, else Normal I), values each badge node of a survivor's tab
        // (YardBadges, TreePlan.cs - pure, in the bench). The nine advices are worked out once per key (levels, run, builds, doctrine,
        // knowledge), never per tab or per tick: about 5 ms on the PC (0.5 ms each), unmeasured on the Deck (~1.8x slower, DK-C10).
        static readonly YardAdviceCache _yardCache = new YardAdviceCache();
        static bool _steerOffSaid, _steerWarnSaid;
        static readonly HashSet<string> _levelSaid = new HashSet<string>();

        static string DoctrineSig(Doctrine d) { return "" + d.Farming + d.Caution + d.Timing + d.ModeAware + d.TagPlan + d.Style + d.LentStyleMine; }

        /// <summary>The badge demand of a survivor's tab, or null = the 0.15 badge order ([Advice] YardBadges off, no badge node, no badge
        /// readable, or a failure - said once a session).</summary>
        internal static YardBadges BadgeDemand(string tree, List<TNode> nodes)
        {
            long perf = Perf.Begin();
            try
            {
                if (!Plugin.AdviceYardBadges.Value) return null;
                if (nodes == null || !nodes.Any(n => n.Kind == TKind.Badge)) return null;
                if (!LoadoutState.EnsureFacts())
                {
                    if (!_steerOffSaid) { _steerOffSaid = true; Plugin.Logger.LogInfo("[yard] badge steering off: no badge readable here - the plan keeps the 0.15 badge order"); }
                    return null;
                }
                foreach (var n in nodes) if (n.Kind == TKind.Badge && n.BadgeId < 0) n.BadgeId = LoadoutState.BadgeIdByNodeKey(n.Key);
                if (!nodes.Any(n => n.Kind == TKind.Badge && n.BadgeId >= 0)) return null;
                var last = LoadoutUi.Last;
                string mode = last != null ? last.Mode : "Normal"; int diff = last != null ? last.Difficulty : 1;
                string ctx = Loadout.RunName(mode, diff);
                var survivors = new List<string>();
                foreach (CT cls in Enum.GetValues(typeof(CT)))
                {
                    if (cls == CT.None || cls == CT.NumCharacters) continue;
                    var props = G.PropsOf(cls);
                    if (props != null && G.Unlocked(props)) survivors.Add(G.ClassName(cls));
                }
                if (survivors.Count == 0) survivors.AddRange(Builds.Survivors);
                // the levels the run setup screen and the BADGES page use; no override per tab, so every tab reads the same advices
                var levels = new Dictionary<int, int>(); var open = new Dictionary<int, bool>(); int raised;
                LoadoutState.ReadLevels(levels, open, out raised);
                foreach (var n in nodes)
                {
                    int lv;
                    if (n.Kind == TKind.Badge && n.BadgeId >= 0 && levels.TryGetValue(n.BadgeId, out lv) && lv != n.Level && _levelSaid.Add(n.BadgeId.ToString(CultureInfo.InvariantCulture)))
                        Plugin.Logger.LogInfo("[yard] " + n.Name + ": the tab reads level " + n.Level + ", the badge advice " + lv);
                }
                var k = Knowledge.Current;
                int slots = LoadoutState.SlotsNow();
                string key = YardBadges.Key(mode, diff, slots, levels, survivors.Select(s => s + "=" + LoadoutUi.BuildSig(Builds.For(s))), DoctrineSig(Doctrine.Current), RuntimeHelpers.GetHashCode(k));
                var advices = _yardCache.Get(key, () =>
                {
                    long pc = Perf.Begin();
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var list = new List<KeyValuePair<string, LoadoutAdvice>>();
                    foreach (var s in survivors)
                    {
                        var inp = LoadoutState.Input(s, Builds.For(s), mode, diff, slots, levels, open, null, null, null);
                        list.Add(new KeyValuePair<string, LoadoutAdvice>(s, Loadout.Recommend(inp, LoadoutState.All, k, 0)));
                    }
                    Perf.End("yard.badges.compute", pc);
                    Plugin.Logger.LogInfo("[yard] badge advice for " + list.Count + (list.Count == 1 ? " survivor" : " survivors") + " (" + ctx + "): " + sw.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture) + " ms");
                    return list;
                });
                return YardBadges.Build(nodes, advices, LoadoutState.All, k, k.YardBadgeFloor, k.YardBadgeReach, ctx);
            }
            catch (Exception e)
            {
                if (!_steerWarnSaid) { _steerWarnSaid = true; Plugin.Logger.LogWarning("[yard] badge steering off for this read: " + e.GetBaseException().Message + " - the plan keeps the 0.15 badge order"); }
                return null;
            }
            finally { Perf.End("yard.badges", perf); }
        }

        /// <summary>The part of TreeUi's signature the badge steering depends on beyond the tab's levels: the switch, a build change, the last
        /// run setup's mode and difficulty, the doctrine and the knowledge - a change re-plans on the next tick. "" on the General tab.</summary>
        internal static string DemandSig(bool isTeam)
        {
            if (isTeam) return "";
            bool on = true; try { on = Plugin.AdviceYardBadges.Value; } catch { }
            var last = LoadoutUi.Last;
            return "|" + (on ? "B" : "b") + Builds.Changes + "|" + (last == null ? "-" : last.Mode + last.Difficulty) + "|" + DoctrineSig(Doctrine.Current) + "|" + RuntimeHelpers.GetHashCode(Knowledge.Current);
        }

        /// <summary>The number next to the points diamond; falls back to the survivor's own counter.</summary>
        public static int Points(UIViewSkillTree view, UISkillTreeSkillsContainer container)
        {
            try { int p; var t = view.pointsOwnedText; if (t != null && int.TryParse((t.text ?? "").Trim(), out p)) return p; } catch { }
            try { var v = container.targetClassProperties.skillTreePointsAvailable; if (v != null) return (int)v.value; } catch { }
            return 0;
        }

        public static string Describe(List<TNode> nodes)
        {
            var sb = new StringBuilder();
            foreach (var n in nodes)
                sb.Append("\n    r").Append(n.Rank).Append(" #").Append(n.Slot).Append(' ').Append(n.Kind).Append(' ').Append(n.Key).Append(" '").Append(n.Name).Append("' ")
                  .Append(n.Level).Append('/').Append(n.Max).Append(" min ").Append(n.Min).Append(" costs [").Append(string.Join(",", n.Costs)).Append(']')
                  .Append(n.RankOpen ? "" : " LOCKED").Append(n.Perm ? " perm" : "").Append(n.Kind == TKind.Weapon && n.WeaponDepth >= 0 ? (n.WeaponDepth >= 2 ? " fork" : " line" + n.WeaponDepth) : "").Append(n.GuideBranch ? " guide-branch" : "").Append(n.Tier.Length > 0 ? " tier " + n.Tier : "")
                  .Append(n.Prereqs.Count > 0 ? " pre " + string.Join(",", n.Prereqs) : "");
            return sb.ToString();
        }
    }
}
