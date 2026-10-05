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

    /// <summary>A headline an ability card can show, with the reason line it sums up (Synergy.AbilityHeads).</summary>
    internal sealed class HeadCut { public int Rank; public string Text, From; }

    /// <summary>0.13.0 (C2): a level of the weapon in hand under the "abilities first" style. The style puts the weapon on a low
    /// floor (3.3: "the abilities first, the weapon fills in") - but that floor is absolute, so every OTHER survivor's ability
    /// levels (4.3 - 5.0) stayed above it as well and the weapon never filled in once recruits had joined: in the user's 10-04
    /// run the leader's weapon was ranked last all run, and the user took it against the advice nine times (six of them after
    /// a reroll). Now, once the survivor's own abilities are as good as done - every ability the build ranks (or, without a
    /// build, every free slot's worth) is owned and at most one is still short of its last level - the level is scored on the
    /// balanced floor (4.3; on Auto with a build another mod lends, the player's own level-up style's floor if that is higher),
    /// so it goes ahead of a recruit's ability level; and it stays just under a card of the survivor's OWN open build ability
    /// on the same offer (that card's score - 0.01): "abilities first" still holds for its own abilities. Pure: the bench
    /// replays the logged hands.</summary>
    internal sealed class WeaponLift
    {
        public bool On;                               // the survivor's own abilities are as good as done
        public string Note = "";                      // the style reason's tail, when On
        public string Left;                           // the one ability still short of its last level (null: none)

        /// <summary><paramref name="unfinished"/> = owned abilities short of their last level (the build's skipped ones left
        /// out), <paramref name="left"/> = the name of one of them, <paramref name="missing"/> = abilities the build ranks that
        /// the game can still offer and a free slot could take.</summary>
        public static WeaponLift Judge(BuildStyle style, int unfinished, int missing, string left)
        {
            var w = new WeaponLift();
            if (style != BuildStyle.Ability || missing > 0 || unfinished > 1) return w;
            w.On = true; w.Left = unfinished == 1 ? left : null;
            w.Note = unfinished == 1 && !string.IsNullOrEmpty(left) ? "only " + left + " left: the weapon goes ahead of the others' levels" : "the abilities are done: the weapon now";
            return w;
        }

        /// <summary>The floor a held weapon's level is scored on (Ranker.ScoreWeapon): the style's, or - lifted - the balanced
        /// one, or the player's own style's when Auto follows a lent build and that floor is higher.</summary>
        public double Floor(BuildStyle style, bool lentAuto, BuildStyle doctrine)
        {
            double f = Synergy.WeaponFloor(style);
            if (!On) return f;
            f = Math.Max(f, Synergy.WeaponFloor(BuildStyle.Balanced));
            if (lentAuto) f = Math.Max(f, Synergy.WeaponFloor(doctrine));
            return f;
        }

        /// <summary>The lifted level next to a card of the survivor's own open build ability (<paramref name="own"/> = its score,
        /// to the hundredth): just under it. Returns the weapon's score unchanged when it is under already.</summary>
        public static double UnderOwn(double weapon, double own)
        {
            double o = Math.Round(own, 2);
            return Math.Round(weapon, 2) >= o ? Math.Round(o - 0.01, 2) : weapon;
        }
        public static string UnderOwnLine(string own) { return "abilities first: after " + own + ", ahead of the rest"; }
    }

    internal static class Synergy
    {
        /// <summary>The floor of a level of the weapon in hand by style: Weapon 6.0, Balanced 4.3, Ability 3.3 (Ranker.ScoreWeapon).</summary>
        public static double WeaponFloor(BuildStyle style) { return style == BuildStyle.Weapon ? 6.0 : style == BuildStyle.Balanced ? 4.3 : 3.3; }

        /// <summary>A level of the weapon in hand: the floor (a floor above the balanced one fades toward it when the weapon can
        /// no longer be finished: <paramref name="reach"/>), a tenth per level, the synergy, +0.25 for the level that completes it.</summary>
        public static double HeldWeapon(double floor, double reach, int lvl, int max, double syn)
        {
            double soft = WeaponFloor(BuildStyle.Balanced);
            if (floor > soft) floor = soft + (floor - soft) * reach;
            return floor + 0.1 * lvl + syn + (lvl == max - 1 ? 0.25 : 0);
        }

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
                    if (left <= 1) { v += 0.9; if (why != null) why.Insert(0, SpecialLine(t, tags.SpecialAt)); }
                    else if (left <= 3 && (share > 0 || spread)) { v += 0.3; if (why != null) why.Add(left + " tags from the " + t + " special"); }
                }
            }
            if (best != null && bestShare >= 0.25 && why != null) why.Add(ShareLine(best, bestShare, tags.SourceText(best)));
            return Math.Min(1.4, v) * weight;
        }

        // ---- the reason lines an ability card's headline can be cut from (0.13.0, C3): one builder each, shared by the line
        // and by the head's lookup, so a change of wording cannot bring the duplicate back
        public const string SpecialPrefix = "this level unlocks the ";
        public static string SpecialLine(string type, int at) { return SpecialPrefix + type + " special (" + at + " tags)"; }
        public static string SharePrefix(string type) { return "+1 " + type + " tag: "; }
        public static string ShareLine(string type, double share, string src) { return SharePrefix(type) + (int)Math.Round(share * 100) + "% of the squad's damage" + (!string.IsNullOrEmpty(src) ? " (" + src + ")" : ""); }
        public static string BoostLine(TeamBoost b) { return b.Name + " (" + b.Owner + ") boosts it: " + b.Tag.ToLowerInvariant(); }

        /// <summary>The headlines the damage types and the team passives offer an ability card, in the order Ranker's
        /// AbilityVerdict.Mark takes them (the higher rank wins, the first of a rank stays): "unlocks the Kinetic special" (5),
        /// "Trap Expertise boosts it" (3), "Kinetic: 79% of the squad" (1) - each with the reason line it sums up, which the
        /// card then leaves out. <paramref name="why"/> = the verdict's reasons after TagValue and BoostValue wrote theirs.
        /// 0.13.0 (C3): the second and third were marked without their line, and 30 cards of the user's 10-04 log said them twice
        /// ("new ability - Trap Expertise boosts it; ...; Trap Expertise (Huntress) boosts it: taunt").</summary>
        public static List<HeadCut> AbilityHeads(PowerFacts facts, TagProfile tags, IList<TeamBoost> boosts, double tagValue, double boostValue, List<string> why)
        {
            var heads = new List<HeadCut>();
            if (facts == null || why == null) return heads;
            if (why.Count > 0 && why[0].StartsWith(SpecialPrefix, StringComparison.Ordinal))
            {   // TagValue puts a special within one level first (the last type it met, when two are that close)
                string t = why[0].Substring(SpecialPrefix.Length); int sp = t.IndexOf(' ');
                heads.Add(new HeadCut { Rank = 5, Text = "unlocks the " + (sp > 0 ? t.Substring(0, sp) : t) + " special", From = why[0] });
            }
            if (boostValue > 0 && boosts != null)
                foreach (var b in boosts)
                    if (b.Covers(facts)) { string line = BoostLine(b); heads.Add(new HeadCut { Rank = 3, Text = b.Name + " boosts it", From = why.Contains(line) ? line : null }); break; }
            if (tagValue >= 0.45 && facts.Damage.Count > 0 && tags != null)
            {   // the type TagValue's share line names: the first of the highest share (it adds that line from 25 %, the head needs 40 %)
                string best = null; double share = 0;
                foreach (var t in facts.Damage) { double sh = tags.Share(t); if (sh > share) { share = sh; best = t; } }
                if (best != null && share >= 0.4)
                {
                    string prefix = SharePrefix(best);
                    heads.Add(new HeadCut { Rank = 1, Text = best + ": " + (int)Math.Round(share * 100) + "% of the squad", From = why.FirstOrDefault(w => w.StartsWith(prefix, StringComparison.Ordinal)) });
                }
            }
            return heads;
        }

        /// <summary>The reasons listed under a headline: all but the line it was cut from (the card already says that one).</summary>
        public static List<string> ReasonsUnder(string head, string headFrom, IEnumerable<string> why)
        {
            var list = new List<string>();
            foreach (var line in why ?? new string[0]) if (head == null || line != headFrom) list.Add(line);
            return list;
        }

        /// <summary>Does <paramref name="line"/> say the headline again? Every word of the head is in it ("Trap Expertise boosts it"
        /// in "Trap Expertise (Huntress) boosts it: taunt"). Words: letters and digits, case-insensitive.</summary>
        public static bool Restates(string head, string line)
        {
            var hw = Words(head); if (hw.Count == 0 || string.IsNullOrEmpty(line)) return false;
            var lw = new HashSet<string>(Words(line));
            return hw.All(lw.Contains);
        }
        static List<string> Words(string s)
        {
            var list = new List<string>(); var sb = new System.Text.StringBuilder();
            foreach (var ch in (s ?? "") + " ")
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
                else if (sb.Length > 0) { list.Add(sb.ToString()); sb.Clear(); }
            }
            return list;
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
                if (why != null) why.Add(BoostLine(b));
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
        /// outranks any tier-up), and "your build takes the other one" under a card marked PICK read as a contradiction.
        /// 0.13.0 (F02): <paramref name="auto"/> = the build is one another mod lends and Auto follows, not one the player chose:
        /// it is named as such ("Rifleman (Auto) prefers ...") instead of "your build".</summary>
        public static string EvolutionHead(string baseName, string evoName, string pick, string buildName, bool mine, bool pickOffered, bool auto = false)
        {
            string whose = auto ? buildName + " (Auto)" : "your build";
            if (mine) return "evolution of " + baseName + ": " + (auto ? buildName + " (Auto) takes it" : "your " + buildName + " build's pick");
            if (!pickOffered) return "only " + evoName + " is offered - " + whose + " prefers " + pick + "; still the biggest spike";
            return "evolution of " + baseName + "; " + whose + " takes " + pick;
        }

        /// <summary>The card of a tier-3 weapon whose branch is not the favoured one <paramref name="pref"/> (null: none known) - the
        /// branches exclude each other: its score and its reasons. <paramref name="own"/> = a build the PLAYER chose takes
        /// <paramref name="pref"/>: hold out for it (2.0). Otherwise this is Auto - plain (the squad, the tree, the guides favour
        /// <paramref name="pref"/>, for <paramref name="prefWhy"/>) or a build another mod lends that Auto follows
        /// (<paramref name="autoBuild"/>): a tier-3 weapon of another branch is still a big step up, so it ranks above the weak
        /// abilities and under the good ones while <paramref name="pref"/> can be offered (<paramref name="prefOffered"/>), and
        /// high when it cannot. <paramref name="then"/> = the lent build Auto would follow once this branch is taken.
        /// 0.13.0 (F02): a lent build on Auto held out like the player's own (2.00, "your build takes Rocket Launcher" four
        /// times for a build the player never chose).</summary>
        public static double OtherBranch(string pref, string prefWhy, bool prefOffered, double syn, bool own, string autoBuild, string then, List<string> why)
        {
            if (pref == null) { why.Add("the other branch"); return 6.2 + syn; }
            if (own) { why.Add("other branch; your build takes " + pref); return 2.0; }
            why.Add("other branch; " + (autoBuild != null ? "Auto follows " + autoBuild + ", which takes " + pref : "the squad favours " + pref + (prefWhy != null ? " (" + prefWhy + ")" : "")));
            if (prefOffered) why.Add("taking it locks " + pref + " out" + (then != null ? "; Auto then follows " + then : ""));
            else if (then != null) why.Add("taking it: Auto then follows " + then);
            return prefOffered ? 3.6 + syn : 6.2 + syn;
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

        // ---- the rescue card's reasons (Ranker.RecruitScore builds them; pure, so the bench says them too)
        /// <summary>The guides' rescue tier as a reason line.</summary>
        public static string TierLine(string tier) { return tier + "-tier rescue in the guides"; }
        /// <summary>The bought synergy nodes with the squad as a reason line: "2 bought synergies: 1 with Huntress, 1 with SWAT".
        /// <paramref name="partners"/> = (survivor on the squad, bought nodes with them).</summary>
        public static string BoughtLine(int bought, IList<KeyValuePair<string, int>> partners)
        {
            var with = new List<string>();
            if (partners != null) foreach (var p in partners) if (p.Value > 0) with.Add(p.Value + " with " + p.Key);
            return (bought == 1 ? "1 bought synergy: " : bought + " bought synergies: ") + string.Join(", ", with);
        }
        /// <summary>Puts the card's headline first (the line the card shows): <paramref name="late"/> when a recruit no longer has
        /// the time to grow, else - for a rescue the guides rate that has bought synergies with the squad - "A-tier rescue, 1
        /// bought synergy with Huntress". 0.13.0 (F12): that headline summarises the tier line and the bought line, which then
        /// stood under it as well ("A-tier rescue, 1 bought synergy with the squad; A-tier rescue in the guides; 1 bought
        /// synergy: 1 with Huntress", 20 rescue cards in the user's 10-03 / 10-04 logs): it takes the partners' names from the
        /// bought line and both lines go. Under the "late" headline both stay (it names neither).</summary>
        public static void Headline(List<string> why, string tier, int bought, IList<KeyValuePair<string, int>> partners, string late)
        {
            if (late != null) { why.Insert(0, late); return; }
            if (string.IsNullOrEmpty(tier) || bought <= 0) return;
            why.Remove(TierLine(tier)); why.Remove(BoughtLine(bought, partners));
            var names = new List<string>();
            if (partners != null) foreach (var p in partners) if (p.Value > 0 && !names.Contains(p.Key)) names.Add(p.Key);
            why.Insert(0, tier + "-tier rescue, " + (bought == 1 ? "1 bought synergy" : bought + " bought synergies") + " with " + (names.Count > 0 ? string.Join(", ", names) : "the squad"));
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
        public bool ForQuest;                       // 0.13.0 (C1): shown for a class the active quest needs (Better = those classes)

        /// <summary><paramref name="offered"/> = the recruits on the cards, <paramref name="liberate"/> = the Liberate card's
        /// score (below 0: none), <paramref name="possible"/> = who could still come, <paramref name="canReroll"/> = the game
        /// has a reroll for this screen (<paramref name="rerolls"/> = how the log words it), <paramref name="recruitValue"/> =
        /// the value left in a recruit now (0..1). <paramref name="quest"/> = the active quest's word on the squad (0.13.0, C1; null:
        /// none): at the quest's team limit no recruit is wanted - no hint; while it needs a class the squad lacks, the hint speaks
        /// for that class alone (shown when it could still come and a reroll is left, whatever the clock or the margin).</summary>
        public static RerollCall Decide(IList<Recruit> offered, double liberate, IList<Recruit> possible, bool canReroll, string rerolls, double recruitValue, QuestSos quest = null)
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
            if (quest != null && quest.Active)
            {
                if (quest.Capped) { c.Better.Clear(); c.Why = "quest: " + quest.Words + " - no recruit wanted (Liberate)"; return c; }
                if (quest.Missing.Count > 0)
                {
                    c.Better.Clear();
                    string needs = string.Join(" + ", quest.Missing);
                    var here = (offered ?? new Recruit[0]).Where(r => r != null && quest.Wanted(r.Name)).Select(r => r.Name).ToList();
                    if (here.Count > 0) { c.Why = "quest: " + string.Join(" + ", here) + " is on the cards"; return c; }
                    foreach (var r in ranked) if (quest.Wanted(r.Name)) c.Better.Add(r);
                    if (c.Better.Count == 0) { c.Why = "quest: needs " + needs + ", who cannot come now (locked, held back or not unlocked)"; return c; }
                    if (!canReroll) { c.Why = "quest: needs " + needs + " - no reroll left (" + rerolls + ")"; return c; }
                    c.Show = true; c.ForQuest = true;
                    c.Why = "quest: needs " + needs + " - a reroll may bring " + string.Join(" or ", c.Better.ConvertAll(r => r.Name));
                    return c;
                }
            }
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

    /// <summary>Which reroll count the reroll hint is judged with, and when the game's own count makes it judge again. What
    /// the game does (UIGameplayUpgradeSelection.RefreshActionButtons, read in GameAssembly.dll of 1.0.2): it truncates the team
    /// statistic TeamNumRerolls to an int, writes it on the screen's Reroll button and hands the same int to
    /// SetActionButtonsInteractivity. When a rescue screen opens, its cards are filled (AssignGeneratedElements: the hint's first
    /// verdict) BEFORE that refresh, so the button still shows what its own last refresh wrote - "0" on the first rescue screen
    /// of a session (log of 10-04: 'rerolls 0 (the Reroll button's count, team statistic 8)', the game's 8 three ms later), and a
    /// count from before the rerolls spent on other screens later on (each kind of screen has its own button) - while the
    /// statistic is already the number the game is about to hand over. So: the game's count for these cards, else the
    /// statistic, else the button's number. A game count that differs from the one judged with makes the hint judge again,
    /// once, on the next frame: a reroll refreshes the buttons under the old cards just before it fills new ones, and the new
    /// cards take that count instead (no verdict on cards that are already gone). Pure: the bench replays the logged order.</summary>
    internal sealed class RerollCount
    {
        public const string FromGame = "the screen's count", FromStat = "the team's Rerolls available", FromLabel = "the Reroll button's count", FromNone = "no count readable";
        public int Judged = -1;                 // the count of the current verdict (-1: none readable)
        public string From = FromNone;
        public int Handed = -1;                 // the game's count for these cards (-1: not handed over yet)
        public int Pending = -1;                // a game count waiting for the re-judge on the next frame (-1: none)

        /// <summary><paramref name="game"/> = the count the game handed over for these cards, <paramref name="stat"/> = the team
        /// statistic truncated like the game does, <paramref name="label"/> = the number on the Reroll button (each -1: unknown).</summary>
        public static int Pick(int game, int stat, int label, out string from)
        {
            if (game >= 0) { from = FromGame; return game; }
            if (stat >= 0) { from = FromStat; return stat; }
            if (label >= 0) { from = FromLabel; return label; }
            from = FromNone; return -1;
        }

        /// <summary>The game has a reroll for the screen: a Reroll button shown, and a count left or a FREE reroll (no count
        /// readable at all: the button's own interactable state).</summary>
        public static bool Can(bool button, bool interactable, bool free, int n) { return button && (n > 0 || free || (n < 0 && interactable)); }

        public void Reset() { Judged = -1; From = FromNone; Handed = -1; Pending = -1; }

        /// <summary>New cards on the screen (the first verdict); <paramref name="hook"/> = a count the game handed over just
        /// before them (a reroll refreshes the buttons first), -1: none.</summary>
        public int Offer(int hook, int stat, int label) { Handed = hook; Pending = -1; Judged = Pick(hook, stat, label, out From); return Judged; }

        /// <summary>The game hands over the count it already handed for these cards (a second refresh, a pick, a skip, a FREE
        /// reroll's refresh just before the new cards): nothing to read or judge again - RerollHint.OnButtons returns at once.</summary>
        public bool Seen(int n) { return n == Handed; }

        /// <summary>The game hands its count over (SetActionButtonsInteractivity): true when it differs from the one judged with,
        /// or when the rest of what the verdict read on the buttons changed with it (<paramref name="buttonsChanged"/>: the FREE
        /// reroll label, the Reroll button shown) - a re-judge is then due on the next frame. The same count only becomes the game's.</summary>
        public bool Buttons(int n, bool buttonsChanged = false)
        {
            Handed = n;
            if (n == Judged && !buttonsChanged) { From = FromGame; Pending = -1; return false; }
            Pending = n; return true;
        }

        /// <summary>The next frame: the count to judge again with, once (-1: nothing due - new cards came first, or the count went back).</summary>
        public int Due() { int n = Pending; Pending = -1; if (n >= 0) { Judged = n; From = FromGame; } return n; }
    }
}
