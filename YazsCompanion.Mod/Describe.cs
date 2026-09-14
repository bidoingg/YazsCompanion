// Verbose dump of an offer (every raw field the ranker could use). Off by default; enable
// `Verbose` in BepInEx/config/bidoi.yazs.companion.cfg when validating the ranking.
using System;
using System.Text;

namespace YazsCompanion
{
    internal static class Describe
    {
        static string S(Func<object> f)
        {
            try { var v = f(); return v == null ? "null" : v.ToString(); }
            catch (Exception e) { return "ERR(" + e.GetType().Name + ")"; }
        }

        public static string Powerup(PowerupBase p)
        {
            if (p == null) return "powerup=null";
            var sb = new StringBuilder();
            sb.Append("asset=").Append(S(() => p.name));
            sb.Append(" en=").Append(S(() => p.EnglishName));
            sb.Append(" id=").Append(S(() => p.MyUpgradeId));
            sb.Append(" lvl=").Append(S(() => p.GetPowerupLevel())).Append("/").Append(S(() => p.MaxLevel));
            sb.Append(" ability=").Append(S(() => p.isAbility));
            sb.Append(" class=").Append(S(() => p.targetClassProperties == null ? null : (object)p.targetClassProperties.characterType));
            sb.Append(" prevWeapon=").Append(S(() => p.previousLevelWeapon == null ? null : p.previousLevelWeapon.name));
            sb.Append(" evoBase=").Append(S(() => p.evolutionBaseAbility == null ? null : p.evolutionBaseAbility.name));
            sb.Append(" evoA=").Append(S(() => p.abilityEvolutionA == null ? null : p.abilityEvolutionA.name));
            sb.Append(" evoB=").Append(S(() => p.abilityEvolutionB == null ? null : p.abilityEvolutionB.name));
            sb.Append(" treeReq=").Append(S(() => p.skillTreeRequirement == null ? null : p.skillTreeRequirement.name + "@" + G.NodeLevel(p.skillTreeRequirement)));
            sb.Append(" boost=").Append(S(() => p.skillTreeAbilityBoost == null ? null : p.skillTreeAbilityBoost.name + "@" + G.NodeLevel(p.skillTreeAbilityBoost)));
            sb.Append(" tags=").Append(S(() =>
            {
                var tags = p.powerupTags; if (tags == null) return "null";
                var t = new StringBuilder();
                for (int i = 0; i < tags.Count; i++) { if (i > 0) t.Append('|'); t.Append(tags[i].ToString()); }
                return t.ToString();
            }));
            var w = p.TryCast<WeaponUpgradePowerup>();
            if (w != null) sb.Append(" weaponTier=").Append(S(() => w.weaponTier)).Append(" weaponIdx=").Append(S(() => w.weaponIndex));
            var b = p.TryCast<BasicLevelPowerup>();
            if (b != null) sb.Append(" rarity=").Append(S(() => b.GetRarity())).Append(" target=").Append(S(() => b.targetType));
            return sb.ToString();
        }

        public static string Item(ItemBase it)
        {
            if (it == null) return "item=null";
            return "asset=" + S(() => it.name) + " en=" + S(() => it.EnglishName) + " id=" + S(() => it.itemBaseId)
                + " max=" + S(() => it.numMaxCanCarry) + " swappable=" + S(() => it.isSwappable) + " healing=" + S(() => it.isHealingItem)
                + " desc='" + S(() => ItemRules.RichTag.Replace(it.EnglishDescription ?? "", "")) + "'";
        }

        public static void Offer(UIGameplayUpgradeSelection sel, string kind)
        {
            try
            {
                var log = Plugin.Logger;
                var buttons = sel.powerupButtons;
                if (buttons == null) return;
                for (int i = 0; i < buttons.Length; i++)
                {
                    var b = buttons[i];
                    if (b == null) continue;
                    if (S(() => b.gameObject.activeInHierarchy) != "True") continue;
                    var p = b.attachedPowerup; var it = b.attachedItem;
                    log.LogInfo("[raw] card " + i + " " + S(() => b.GetIl2CppType().Name) + " " + (p != null ? Powerup(p) : it != null ? Item(it) : "hashtag=" + S(() => b.attachedHashtagEvent)));
                }
                var snap = G.Read();
                foreach (var sv in snap.Squad)
                {
                    var sb = new StringBuilder();
                    sb.Append("[raw] ").Append(sv.Name).Append(sv.Leader ? "*" : "").Append(" L").Append(sv.TreeLevel);
                    sb.Append(" hp=").Append(S(() => sv.Player.health == null ? null : sv.Player.health.CurrentHealth.ToString("0") + "/" + sv.Player.health.MaxHealth.ToString("0")));
                    sb.Append(" xp=").Append(S(() => sv.Player.xpGainedThisRun));
                    sb.Append(" synergies=");
                    if (sv.Props != null) foreach (var syn in G.Each(sv.Props.skillTreeSynergies)) if (syn != null) sb.Append(S(() => syn.name)).Append('@').Append(G.NodeLevel(syn)).Append(S(() => "(" + syn.synergiesWithClass + "/" + (syn.requiredPowerup == null ? "-" : syn.requiredPowerup.name) + ")")).Append(' ');
                    log.LogInfo(sb.ToString());
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[raw] " + kind + " failed: " + e.Message); }
        }
    }
}
