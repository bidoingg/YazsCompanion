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
                    // a type nobody else deals: its tag point boosts this one ability alone and the evolution's damage is
                    // split away from the stack the squad is feeding. A small malus, so that two evolutions that are
                    // otherwise level are settled by the tag doctrine (stay inside the stacked type), not by card position
                    else { v -= 0.15 * weight; if (why != null) why.Add("adds " + t + " (nothing else on the squad deals it)"); }
                }
            }
            if (ctx != null && evo.Healing && !(baseAbility != null && baseAbility.Healing)) v += 0.3 * (ctx.Survival - 1.0);
            return v;
        }

        /// <summary>How well a weapon branch (a tier-3 weapon) fits what the REST of the squad deals (the survivor's own current weapon
        /// is about to be replaced, so the caller leaves it out of <paramref name="others"/>). The score is the branch's own;
        /// the REASON names only what sets it apart from the <paramref name="rivals"/> (the other branches of the fork):
        /// "shares Kinetic with Bow" explains nothing when every branch of the fork deals Kinetic.</summary>
        public static double BranchFit(PowerFacts branch, TagProfile others, IList<TeamBoost> boosts, RunContext ctx, List<string> why, IList<PowerFacts> rivals = null)
        {
            if (branch == null) return 0;
            double weight = ctx == null || ctx.D == null ? 1.0 : ctx.D.SynergyWeight;
            double v = 0;
            if (others != null)
                foreach (var t in branch.Damage.Distinct(StringComparer.OrdinalIgnoreCase))
                    v += 1.2 * others.Share(t) + 0.04 * Math.Min(10, others.PointsOf(t));
            string best = BranchType(branch, others, rivals);
            if (best != null && why != null) why.Add("shares " + best + " with " + (others.SourceText(best).Length > 0 ? others.SourceText(best) : "the squad"));
            return (v + BoostValue(branch, boosts, ctx, null)) * weight;
        }

        /// <summary>The damage type that speaks for this branch against its rivals: the one the rest of the squad deals most
        /// (a fifth of its damage at least) among the types that not every rival deals too; null when nothing sets it apart.</summary>
        public static string BranchType(PowerFacts branch, TagProfile others, IList<PowerFacts> rivals)
        {
            if (branch == null || others == null) return null;
            string best = null; double bestShare = 0;
            foreach (var t in branch.Damage.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (rivals != null && rivals.Count > 0 && rivals.All(r => r != null && r.Damage.Contains(t, StringComparer.OrdinalIgnoreCase))) continue;
                double share = others.Share(t);
                if (share > bestShare) { bestShare = share; best = t; }
            }
            return bestShare >= 0.2 ? best : null;
        }

        /// <summary>The headline under an evolution card when the survivor's build names an evolution for its ability.
        /// 0.12.2 (F12): when the build's pick is not among the cards the offered one still comes first (an evolution
        /// outranks any tier-up), and "your build takes the other one" under a card marked PICK read as a contradiction.</summary>
        public static string EvolutionHead(string baseName, string evoName, string pick, string buildName, bool mine, bool pickOffered)
        {
            if (mine) return "evolution of " + baseName + ": your " + buildName + " build's pick";
            if (!pickOffered) return "only " + evoName + " is offered - your build prefers " + pick + "; still the biggest spike";
            return "evolution of " + baseName + "; your build takes " + pick;
        }
    }

    /// <summary>A survivor who could be rescued, as the SOS cards and the PLAN readout's SOS row rank them.
    /// 0.12.2 (F12): both used to settle an exact tie their own way - the cards by button position, the row by the game's
    /// class order - and an A-tier recruit with a team passive ties an S-tier one exactly once both are trained to the cap
    /// (5.00 = 5.00 and 4.60 = 4.60 on the Deck): twice the row named the SWAT first while the card framed the Tank. One
    /// order for both now.</summary>
    internal sealed class Recruit
    {
        public string Name = "";
        public int Class;                 // the game's class order (CharacterType): the last word on a tie
        public double Score;
        public string Tier = "";          // the guides' rescue tier, S / A / B / C / ""
        public int Bought;                // synergy nodes with the squad that are bought, either way

        static int TierRank(string tier)
        {
            switch ((tier ?? "").Trim().ToUpperInvariant()) { case "S": return 4; case "A": return 3; case "B": return 2; case "C": return 1; default: return 0; }
        }

        /// <summary>Best first: the score as the cards show it (to the hundredth), then <see cref="Ties"/>.</summary>
        public static int Compare(Recruit a, Recruit b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int r = Math.Round(b.Score, 2).CompareTo(Math.Round(a.Score, 2));
            return r != 0 ? r : Ties(a, b);
        }

        /// <summary>Two recruits that score the same: the guides' rescue tier (S over A over B), then the bought synergy nodes
        /// with the squad, then the game's class order. A card that is no recruit (null: Liberate) comes after them.</summary>
        public static int Ties(Recruit a, Recruit b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int r = TierRank(b.Tier).CompareTo(TierRank(a.Tier)); if (r != 0) return r;
            r = b.Bought.CompareTo(a.Bought); if (r != 0) return r;
            return a.Class.CompareTo(b.Class);
        }
    }

    /// <summary>The reroll hint on the rescue screen (0.12.2): is a reroll worth it here? It compares the best card on the
    /// table (a recruit, or Liberate when the cards rank it first) with the best survivor who could still come - unlocked,
    /// not on the squad, not on the cards, not held back by the game after a reroll - all scored by the very rules of the
    /// SOS cards (Ranker.RecruitScore). The hint speaks only when that survivor rates clearly higher AND the game still
    /// has a reroll for the screen. Pure: the offline bench replays it on the rescue screens of a round of Steam Deck logs.
    /// Why this margin: early in a run a recruit scores 2.0 plus what fits (the guides' tier S 1.6 / A 1.1 / B 0.6, 1.1 per
    /// bought synergy node, up to 1.2 for shared damage types, team passives, training), so one guide tier apart is 0.5
    /// and one bought synergy 1.1. 0.75 lets a tier alone, a trained level or an exact tie pass in silence - a reroll is
    /// the run's, and the next draw may be no better - and speaks for a synergy, or a tier together with shared damage.
    /// On the Deck's screens the player rerolled four times, the gap was 0.96 - 1.29 each time.</summary>
    internal sealed class RerollCall
    {
        public const double Margin = 0.75;
        /// <summary>Under this recruit value (Context.RecruitValue) the cards say Liberate: a recruit no longer has the time to grow.</summary>
        public const double Late = 0.45;

        public bool Show;
        public string Why = "";                     // shown or not, and why - for the log
        public Recruit Best;                        // the best survivor who could still come (null: nobody could)
        public string OfferedName = "";             // the best card on the table: a recruit's name or "Liberate"
        public double Offered;                      // its score, to the hundredth
        public double Gap;                          // Best - Offered, to the hundredth
        public readonly List<Recruit> Better = new List<Recruit>();     // every one who clears the margin, best first

        /// <summary><paramref name="offered"/> = the recruits on the cards, <paramref name="liberate"/> = the Liberate card's
        /// score (below 0: none), <paramref name="possible"/> = who could still come, <paramref name="canReroll"/> = the game
        /// has a reroll for this screen (<paramref name="rerolls"/> = how the log words it), <paramref name="recruitValue"/> =
        /// the value left in a recruit now (0..1).</summary>
        public static RerollCall Decide(IList<Recruit> offered, double liberate, IList<Recruit> possible, bool canReroll, string rerolls, double recruitValue)
        {
            var c = new RerollCall();
            Recruit top = null;
            foreach (var r in offered ?? new Recruit[0]) if (r != null && (top == null || Recruit.Compare(r, top) < 0)) top = r;
            double topScore = top != null ? Math.Round(top.Score, 2) : double.MinValue;
            if (liberate >= 0 && Math.Round(liberate, 2) > topScore) { c.OfferedName = "Liberate"; c.Offered = Math.Round(liberate, 2); }
            else if (top != null) { c.OfferedName = top.Name; c.Offered = topScore; }
            var ranked = new List<Recruit>();
            foreach (var r in possible ?? new Recruit[0]) if (r != null) ranked.Add(r);
            ranked.Sort(Recruit.Compare);
            c.Best = ranked.Count > 0 ? ranked[0] : null;
            if (c.Best != null && c.OfferedName.Length > 0) c.Gap = Math.Round(Math.Round(c.Best.Score, 2) - c.Offered, 2);
            foreach (var r in ranked) if (c.OfferedName.Length > 0 && Math.Round(Math.Round(r.Score, 2) - c.Offered, 2) >= Margin) c.Better.Add(r);

            if (top == null) { c.Why = "no survivor on the cards"; return c; }
            if (recruitValue < Late) { c.Why = "late in the run: a recruit no longer has the time to grow, the cards say Liberate"; return c; }
            if (c.Best == null) { c.Why = "every survivor who could come is on the cards or on the squad"; return c; }
            if (c.Better.Count == 0)
            {
                c.Why = c.Gap <= 0 ? "the best survivor is on the cards (nobody who could still come rates higher)" : "the best that could still come (" + c.Best.Name + " " + Math.Round(c.Best.Score, 2).ToString("0.00") + ") is only " + c.Gap.ToString("0.00") + " above the cards - under the " + Margin.ToString("0.00") + " margin";
                return c;
            }
            if (!canReroll) { c.Why = "no reroll left (" + rerolls + ")"; return c; }
            c.Show = true;
            c.Why = string.Join(" or ", c.Better.ConvertAll(r => r.Name)) + " would rate higher (" + Math.Round(c.Best.Score, 2).ToString("0.00") + " vs " + c.OfferedName + " " + c.Offered.ToString("0.00") + ", +" + c.Gap.ToString("0.00") + ")";
            return c;
        }
    }
}
