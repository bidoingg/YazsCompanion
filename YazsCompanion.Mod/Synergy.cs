// Live squad synergy for weapons, abilities and evolutions - what the game's own data says fits together, read
// as the run stands right now:
//
//  Damage type tags. Every level of a weapon or ability adds one tag point to each damage type it deals (an
//    evolution too, including the types it adds: Electrocution: Passion is +1 Electric and +1 Fire). A point is
//    +2 % damage for everything dealing that type, and at HashtagSystem.NumRequiredForSpecial (10) the type's
//    special effect switches on. So a level is worth more when the squad already deals its type (the share of the
//    squad's damage that profits), and more again when it carries the type over the threshold.
//  Team passives. Grenade / Turret / Trap Expertise and Cold Chain boost every powerup on the squad that carries a
//    tag (Grenade, Turret, Taunt, Deployable) while their owner is on the team - including the evolutions that add
//    such a tag (Helicopter Strike: Chemtrails throws grenades, Automatic Turret: Provocation taunts).
//  Evolutions. The two evolutions of an ability differ in exactly these things - the damage types and the tags
//    they add - so which one is right depends on who is on the squad. A build can fix the choice; otherwise it
//    is made here.
//
// Pure C# (no game types): GameState.cs fills PowerFacts and TeamBoost from the live objects, the bench by hand.
using System;
using System.Collections.Generic;
using System.Linq;

namespace YazsCompanion
{
    /// <summary>What a weapon, ability or evolution is, in the game's own terms.</summary>
    internal sealed class PowerFacts
    {
        public string Name = "";
        public bool IsWeapon, IsAbility, Healing;
        public readonly List<string> Damage = new List<string>();                                        // damage type tags it deals
        public readonly HashSet<string> Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);    // Grenade, Turret, Taunt, Deployable, Melee, Projectile
        public PowerFacts Deals(params string[] types) { Damage.AddRange(types); return this; }
        public PowerFacts Tagged(params string[] tags) { foreach (var t in tags) Tags.Add(t); return this; }
    }

    /// <summary>An owned team passive whose owner is on the squad: everything carrying <see cref="Tag"/> is boosted.</summary>
    internal sealed class TeamBoost
    {
        public string Name = "", Owner = "", Tag = "", NotTag;
        public bool AbilitiesOnly;
        public bool Covers(PowerFacts p)
        {
            if (p == null || !p.Tags.Contains(Tag)) return false;
            if (!string.IsNullOrEmpty(NotTag) && p.Tags.Contains(NotTag)) return false;
            return !AbilitiesOnly || p.IsAbility;
        }
    }

    internal static class Synergy
    {
        /// <summary>The value of the tag points one more level of <paramref name="p"/> brings, and of sharing damage types
        /// with the rest of the squad. 0 for powerups that deal no damage type (Ricochet, Fury Unleashed).</summary>
        public static double TagValue(PowerFacts p, TagProfile tags, RunContext ctx, List<string> why)
        {
            if (p == null || tags == null || p.Damage.Count == 0) return 0;
            double weight = ctx == null || ctx.D == null ? 1.0 : ctx.D.SynergyWeight;
            if (weight <= 0) return 0;
            string focus = tags.Focus();
            bool spread = string.Equals(tags.Plan, "Spread", StringComparison.OrdinalIgnoreCase);
            double v = 0; string best = null; double bestShare = 0;
            foreach (var t in p.Damage.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                double share = tags.Share(t);
                int cur = tags.PointsOf(t);
                if (share > bestShare) { bestShare = share; best = t; }
                v += (spread ? 0.15 : 0.55) * share;
                if (!spread && focus != null && string.Equals(focus, t, StringComparison.OrdinalIgnoreCase)) v += 0.25;
                if (tags.SpecialAt > 0 && cur < tags.SpecialAt)
                {
                    int left = tags.SpecialAt - cur;
                    if (left <= 1) { v += 0.9; if (why != null) why.Insert(0, "this level unlocks the " + t + " special (" + tags.SpecialAt + " tags)"); }
                    else if (left <= 3 && (share > 0 || spread)) { v += 0.3; if (why != null) why.Add(left + " tags from the " + t + " special"); }
                }
            }
            if (best != null && bestShare >= 0.25 && why != null)
            {
                string src = tags.SourceText(best);
                why.Add("+1 " + best + " tag: " + (int)Math.Round(bestShare * 100) + "% of the squad's damage" + (src.Length > 0 ? " (" + src + ")" : ""));
            }
            return Math.Min(1.4, v) * weight;
        }

        /// <summary>The team passives on the squad that boost this powerup.</summary>
        public static double BoostValue(PowerFacts p, IList<TeamBoost> boosts, RunContext ctx, List<string> why)
        {
            if (p == null || boosts == null) return 0;
            double weight = ctx == null || ctx.D == null ? 1.0 : ctx.D.SynergyWeight;
            double v = 0;
            foreach (var b in boosts)
            {
                if (!b.Covers(p)) continue;
                v += 0.5;
                if (why != null) why.Add(b.Name + " (" + b.Owner + ") boosts it: " + b.Tag.ToLowerInvariant());
            }
            return Math.Min(1.0, v) * weight;
        }

        /// <summary>How well one evolution of <paramref name="baseAbility"/> fits the squad: its tag value, the team passives
        /// that cover it, and what it adds over the base ability that the squad can use.</summary>
        public static double EvolutionFit(PowerFacts evo, PowerFacts baseAbility, TagProfile tags, IList<TeamBoost> boosts, RunContext ctx, List<string> why)
        {
            if (evo == null) return 0;
            double weight = ctx == null || ctx.D == null ? 1.0 : ctx.D.SynergyWeight;
            double v = TagValue(evo, tags, ctx, why) + BoostValue(evo, boosts, ctx, why);
            if (baseAbility != null && tags != null)
            {
                foreach (var t in evo.Damage)
                {
                    if (baseAbility.Damage.Contains(t, StringComparer.OrdinalIgnoreCase)) continue;
                    double share = tags.Share(t);
                    if (share > 0) { v += 0.35 * weight; if (why != null) why.Add("adds " + t + ", which " + (tags.SourceText(t).Length > 0 ? tags.SourceText(t) : "the squad") + " deals"); }
                    else if (why != null) why.Add("adds " + t + " (nothing else on the squad deals it)");
                }
            }
            if (ctx != null && evo.Healing && !(baseAbility != null && baseAbility.Healing)) v += 0.3 * (ctx.Survival - 1.0);
            return v;
        }

        /// <summary>How well a tier-2 weapon branch fits what the REST of the squad deals (the survivor's own current weapon
        /// is about to be replaced, so the caller leaves it out of <paramref name="others"/>).</summary>
        public static double BranchFit(PowerFacts branch, TagProfile others, IList<TeamBoost> boosts, RunContext ctx, List<string> why)
        {
            if (branch == null) return 0;
            double weight = ctx == null || ctx.D == null ? 1.0 : ctx.D.SynergyWeight;
            double v = 0; string best = null; double bestShare = 0;
            if (others != null)
                foreach (var t in branch.Damage.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    double share = others.Share(t);
                    v += 1.2 * share + 0.04 * Math.Min(10, others.PointsOf(t));
                    if (share > bestShare) { bestShare = share; best = t; }
                }
            if (best != null && bestShare >= 0.2 && why != null) why.Add("shares " + best + " with " + (others.SourceText(best).Length > 0 ? others.SourceText(best) : "the squad"));
            return (v + BoostValue(branch, boosts, ctx, null)) * weight;
        }
    }
}
