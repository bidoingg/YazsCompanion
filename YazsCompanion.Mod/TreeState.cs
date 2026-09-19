// Reads the Training Yard tab that is on screen into TNode values for TreePlan: every node of the tab's columns with
// its kind (from the upgrade's class), rank (the column), level, costs, prerequisites and whether its rank is open
// (the node shows its lock or not - exactly what the player sees), plus the points on hand (the number the view
// prints next to the diamond, so it is right for the General tab and for every survivor).
using System;
using System.Collections.Generic;
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
            string branch = null; if (!isTeam) Knowledge.Current.WeaponBranch.TryGetValue(treeName, out branch);

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

        static void Classify(TNode n, SkillTreeUpgradeBase up, string guideBranch)
        {
            try
            {
                var w = up.TryCast<SkillTreeUpgradeWeaponBoost>();
                if (w != null)
                {
                    n.Kind = TKind.Weapon;
                    string name = G.Name(w.targetWeapon);
                    n.GuideBranch = guideBranch != null && string.Equals(name, guideBranch, StringComparison.OrdinalIgnoreCase);
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
                if (up.TryCast<SkillTreeUpgradeBadgeBoost>() != null) { n.Kind = TKind.Badge; return; }
                if (up.TryCast<SkillTreeUpgradeTeamStatisticBoost>() != null) { n.Kind = TKind.Stat; return; }
                if (up.TryCast<SkillTreeUpgradeUnlockMechanic>() != null || up.TryCast<SkillTreeUpgradeItemSlot>() != null || up.TryCast<SkillTreeUpgradeBadgeSlot>() != null) { n.Kind = TKind.Mechanic; return; }
                if (up.TryCast<SkillTreeUpgradeValueBase>() != null) { n.Kind = TKind.Passive; return; }
            }
            catch { }
        }

        static string TierOf(PowerupBase ability)
        {
            if (ability == null) return "";
            string tier; return Knowledge.Current.AbilityTier.TryGetValue(G.Name(ability), out tier) ? tier : "";
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
                  .Append(n.RankOpen ? "" : " LOCKED").Append(n.Perm ? " perm" : "").Append(n.GuideBranch ? " guide-branch" : "").Append(n.Tier.Length > 0 ? " tier " + n.Tier : "")
                  .Append(n.Prereqs.Count > 0 ? " pre " + string.Join(",", n.Prereqs) : "");
            return sb.ToString();
        }
    }
}
