// The ranking brain. What a card is worth depends on four things, all read live:
//
//  the build      what the player chose to follow for that survivor in the mod menu (weapon branch, ability order,
//                 evolution picks, level-up style); on Auto the squad, the Training Yard and the guides decide,
//  the squad      synergy as the run stands: shared damage types (every level adds a tag point to each type the
//                 powerup deals, +2 % for everything dealing it, a special effect at 10), team passives that boost
//                 a powerup tag, bought synergy nodes whose partner is on the team, what is held,
//  the clock      what pays back over the rest of the run is worth the most early (economy, a fresh ability, a
//                 recruit) and what works at once keeps its value (a weapon tier, the level that completes an
//                 ability, a tag special within reach); see Context.cs,
//  the mode       the horizon (20:00, 10:00, 5:00, waves, open-ended), boss focus, how much survival weighs.
//
//  Level-ups:  evolutions first (the build's pick, else the one that fits the squad), a recruit's first weapon, the
//              next weapon tier; then by STYLE - Weapon: every weapon level before any ability level; Balanced
//              (default): each ability once early, then the weapon and the focus ability side by side, the rest
//              after; Ability: the abilities first, the weapon fills in.
//  Chests:     guide tier, then the fit with this squad, this clock, what is held (ItemRules).
//  SOS:        synergy nodes that are BOUGHT, shared damage types, team passives, the guides' rescue tier - scaled by
//              the time a recruit still has to grow; Liberate once that time has run out, or when the squad is full.
//  Military:   rarity x stat weight, the weight moved by the squad's weapon / ability split and by the clock.
using System;
using System.Collections.Generic;
using System.Linq;
using CT = GamePlayer.CharacterType;

namespace YazsCompanion
{
    internal enum Screen { LevelUp, Chest, Military, Hashtag, SOS }

    internal sealed class Card
    {
        public int Index;
        public UIPowerupButtonBase Button;
        public PowerupBase Powerup;
        public ItemBase Item;
        public HashtagEventActivationObject.HashtagEventUpgrade Hashtag;
        public string Name = "?";
        public string Kind = "?";
        public Survivor Owner;
        public double Score;
        public int Rank;
        public readonly List<string> Why = new List<string>();   // Why[0] is the short headline shown on the card
        public string Reason { get { return Why.Count > 0 ? Why[0] : ""; } }
    }

    internal static class Ranker
    {
        static readonly int[] RankLevels = { 0, 20, 40, 60, 80 };
        static Knowledge K { get { return Knowledge.Current; } }

        public static void Rank(Screen screen, List<Card> cards, Snapshot s)
        {
            foreach (var c in cards)
            {
                try { Score(screen, c, s); }
                catch (Exception e) { c.Score = 0; c.Why.Insert(0, "not ranked (" + e.GetType().Name + ")"); Plugin.Logger.LogWarning("rank " + c.Name + ": " + e); }
                c.Score = Math.Round(c.Score, 2);
            }
            var order = cards.OrderByDescending(c => c.Score).ThenBy(c => c.Index).ToList();
            for (int i = 0; i < order.Count; i++) order[i].Rank = i + 1;
        }

        static void Score(Screen screen, Card c, Snapshot s)
        {
            if (c.Item != null) { ScoreItem(c, s); return; }
            if (c.Hashtag != null) { ScoreHashtag(c, s); return; }
            var p = c.Powerup;
            if (p == null) { c.Kind = "empty"; c.Score = 0.5; c.Why.Add("nothing attached"); return; }
            c.Name = G.Name(p);
            if (screen == Screen.SOS) { ScoreSos(c, s); return; }
            if (screen == Screen.Military || p.TryCast<BasicLevelPowerup>() != null) { ScoreMilitary(c, s); return; }

            var owner = OwnerOf(p, s);
            var w = p.TryCast<WeaponUpgradePowerup>();
            PowerupBase evoBase = null; try { evoBase = p.evolutionBaseAbility; } catch { }
            bool isAbility = false; try { isAbility = p.isAbility; } catch { }
            if (owner == null)
            {
                c.Kind = "other"; c.Score = 0.5;
                string cls = ClassOf(p);
                c.Why.Add(cls == null ? "not tied to a survivor" : cls + " is not on the squad");
                return;
            }
            c.Owner = owner;

            if (evoBase != null) { c.Kind = "evolution"; ScoreEvolution(c, p, evoBase, owner, s); }
            else if (w != null) { c.Kind = "weapon"; ScoreWeapon(c, w, owner, s); }
            else if (isAbility) { c.Kind = "ability"; ScoreAbility(c, p, owner, s); }
            else { c.Kind = "other"; c.Score = 1; c.Why.Add("powerup outside the tree"); }
        }

        // ---------------------------------------------------------------- names and styles
        static string Norm(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var ch in s ?? "") if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }
        /// <summary>Same name, forgiving punctuation and the "Ability: " prefix of an evolution.</summary>
        internal static bool SameName(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            string na = Norm(a), nb = Norm(b);
            if (na == nb) return true;
            int ia = a.IndexOf(':'), ib = b.IndexOf(':');
            string ta = Norm(ia >= 0 ? a.Substring(ia + 1) : a), tb = Norm(ib >= 0 ? b.Substring(ib + 1) : b);
            return ta.Length > 2 && ta == tb;
        }

        internal static BuildStyle StyleOf(Survivor owner)
        {
            var b = owner == null ? null : owner.Build;
            return b != null ? b.Style : Doctrine.Current.Style;
        }
        static string StyleName(BuildStyle st) { return st == BuildStyle.Weapon ? "weapon first" : st == BuildStyle.Ability ? "abilities first" : "balanced"; }

        // ---------------------------------------------------------------- weapons
        internal sealed class WStep
        {
            public WeaponUpgradePowerup W; public int Depth; public int Index; public int Level; public bool Available;
            public double Pref; public bool Recommended; public bool Alt; public bool GuidePick; public bool BuildPick; public string Why;
            public double Tree;        // the Training Yard part of Pref alone
            public string WhyType;     // the damage type Why is about ("shares Kinetic with Bow"), when the squad's damage decided
        }
        // what decided a fork when no damage type sets the branches apart
        static string ForkFallback(WStep x) { return x.GuidePick ? "the guides' branch" : x.Tree > 0 ? "your Training Yard investment" : null; }

        static int Depth(WeaponUpgradePowerup w)
        {
            int d = 0; PowerupBase cur = w;
            for (int guard = 0; guard < 8 && cur != null; guard++)
            {
                PowerupBase prev = null; try { prev = cur.previousLevelWeapon; } catch { }
                if (prev == null) break;
                d++; cur = prev;
            }
            Weapon.WeaponTier tier = Weapon.WeaponTier.Tier1; try { tier = w.weaponTier; } catch { }
            if (d == 0 && tier != Weapon.WeaponTier.Tier1) d = 3;   // the final weapon has no predecessor link
            return d;
        }

        internal static List<WStep> WeaponPath(Survivor owner)
        {
            var path = new List<WStep>();
            if (owner.Props == null) return path;
            var s = owner.Snap;
            var build = owner.Build;
            string guideBranch; K.WeaponBranch.TryGetValue(owner.Name, out guideBranch);
            foreach (var p in G.Each(owner.Props.weaponPowerups))
            {
                var w = p == null ? null : p.TryCast<WeaponUpgradePowerup>();
                if (w == null) continue;
                SkillTreeUpgradeBase node = null; try { node = w.skillTreeRequirement; } catch { }
                int idx = 0; try { idx = w.weaponIndex; } catch { }
                var st = new WStep { W = w, Depth = Depth(w), Index = idx, Level = owner.LevelOf(w) };
                st.Available = st.Level >= 1 || node == null || G.NodeOwned(node);
                st.GuidePick = guideBranch != null && SameName(G.Name(w), guideBranch);
                st.BuildPick = build != null && !string.IsNullOrEmpty(build.Branch) && SameName(G.Name(w), build.Branch);
                st.Pref = st.Tree = 0.5 * Math.Max(0, G.NodeLevel(node) - 1);
                path.Add(st);
            }
            foreach (var g in path.GroupBy(x => x.Depth))
            {
                if (g.Key == 2)
                {
                    // the fork: a branch already taken wins; else the build's branch; else what the rest of the squad deals,
                    // the Training Yard investment and the guides, among the branches the tree unlocked (the game only
                    // offers those: a locked pick must not demote the branch that can be offered)
                    var pool = g.Where(x => x.Level >= 1).ToList();
                    if (pool.Count == 0) pool = g.Where(x => x.Available).ToList();
                    if (pool.Count == 0) pool = g.ToList();
                    TagProfile others = null;
                    if (s != null && pool.Count > 1 && !pool.Any(x => x.BuildPick)) { try { others = G.SquadProfile(s, owner); } catch { } }
                    foreach (var x in pool)
                    {
                        if (x.BuildPick) { x.Pref += 10; x.Why = "your " + build.Name + " build"; continue; }
                        if (x.GuidePick) x.Pref += 0.9;
                        if (others != null)
                        {
                            // the reason names only a type that sets this branch apart from the others of the fork
                            var why = new List<string>(); var rivals = pool.Where(y => y != x).Select(y => G.Facts(y.W)).ToList();
                            double fit = Synergy.BranchFit(G.Facts(x.W), others, s.Boosts, s.Ctx, why, rivals);
                            x.Pref += fit; if (fit >= 0.3 && why.Count > 0) { x.Why = why[0]; x.WhyType = Synergy.BranchType(G.Facts(x.W), others, rivals); }
                        }
                    }
                    var best = pool.OrderByDescending(x => x.Pref).ThenBy(x => x.Index).First();
                    if (best.Why == null) best.Why = ForkFallback(best);
                    foreach (var x in g) { x.Recommended = x == best; x.Alt = x != best; }
                }
                else foreach (var x in g) x.Recommended = true;
            }
            return path.OrderBy(x => x.Depth).ThenBy(x => x.Index).ToList();
        }

        /// <summary>The weapon the survivor is on (the deepest owned step; the game's own current weapon when it is owned).</summary>
        internal static WStep CurrentStep(Survivor owner, List<WStep> path)
        {
            var current = path.Where(x => x.Level >= 1).OrderByDescending(x => x.Depth).FirstOrDefault();
            if (owner.Weapon != null && owner.LevelOf(owner.Weapon) >= 1) current = path.FirstOrDefault(x => G.Same(x.W, owner.Weapon)) ?? current;
            return current;
        }

        static double WeaponFloor(BuildStyle style) { return style == BuildStyle.Weapon ? 6.0 : style == BuildStyle.Balanced ? 4.3 : 3.3; }

        static void ScoreWeapon(Card c, WeaponUpgradePowerup w, Survivor owner, Snapshot s)
        {
            var path = WeaponPath(owner);
            var me = path.FirstOrDefault(x => G.Same(x.W, w));
            var current = CurrentStep(owner, path);
            var next = path.FirstOrDefault(x => x.Recommended && x.Available && x.Level < 1 && x.Depth > 0 && (current == null || x.Depth > current.Depth));
            int lvl = owner.LevelOf(w);
            int max = G.MaxLevel(w);
            SkillTreeUpgradeBase node = null; try { node = w.skillTreeRequirement; } catch { }
            int paid = Math.Max(0, G.NodeLevel(node) - 1);
            var style = StyleOf(owner);
            var facts = G.Facts(w);
            var synWhy = new List<string>();
            double syn = 0.4 * Synergy.TagValue(facts, s.Tags, s.Ctx, synWhy) + 0.4 * Synergy.BoostValue(facts, s.Boosts, s.Ctx, synWhy);

            if (lvl >= 1)
            {
                // a level of the weapon in hand. Its floor depends on the style; late in a timed run a weapon that can no
                // longer be finished falls back to the balanced floor (a level-1 bow with ninety seconds left is not a carry)
                double floor = WeaponFloor(style), soft = WeaponFloor(BuildStyle.Balanced);
                double reach = s.Ctx.Reach(Math.Max(1, max - lvl));
                if (floor > soft) floor = soft + (floor - soft) * reach;
                c.Score = floor + 0.1 * lvl + syn + (lvl == max - 1 ? 0.25 : 0);
                c.Why.Add((lvl == max - 1 ? "completes the weapon: " : "weapon level: ") + lvl + " to " + (lvl + 1) + " of " + max + (lvl == max - 1 && next != null ? ", then " + G.Name(next.W) : ""));
                c.Why.Add("style: " + StyleName(style) + (reach < 0.6 ? "; little time left to finish it (" + s.Ctx.ClockText + ")" : ""));
            }
            else if (me != null && me.Depth == 0) { c.Score = 7.2; c.Why.Add("first weapon for " + owner.Name + ": without it the recruit does nothing"); }
            else if (me != null && me.Depth >= 3 && (next == null || me == next)) { c.Score = 6.9 + syn; c.Why.Add("final weapon of the line"); }
            else if (next != null && me == next)
            {
                c.Score = 6.6 + syn;
                c.Why.Add("next weapon tier" + (me.Why != null ? ": " + me.Why : ""));
            }
            else if (me != null && me.Alt)
            {
                var pref = path.FirstOrDefault(x => x.Depth == me.Depth && x.Recommended);
                bool chosen = pref != null && pref.BuildPick;
                bool prefOffered = pref != null && pref.Available;
                // the branches exclude each other. The player's own build: hold out for it. Auto: a tier-2 weapon of the
                // other branch is still a big step up, so it ranks above the weak abilities, below the good ones
                c.Score = chosen ? 2.0 : prefOffered ? 3.6 + syn : 6.2 + syn;
                // "shares Kinetic with Bow" speaks for the favoured branch against a rocket launcher, not against this card
                // when it deals Kinetic too: there the tree investment or the guides decided (or nothing: card order)
                string prefWhy = pref == null || chosen ? null : pref.WhyType != null && facts.Damage.Contains(pref.WhyType, StringComparer.OrdinalIgnoreCase) ? ForkFallback(pref) : pref.Why;
                c.Why.Add(pref != null ? "other branch; " + (chosen ? "your build takes " : "the squad favours ") + G.Name(pref.W) + (prefWhy != null ? " (" + prefWhy + ")" : "") : "the other branch");
                if (!chosen && prefOffered) c.Why.Add("taking it locks " + G.Name(pref.W) + " out");
            }
            else if (me == null) { c.Score = 2; c.Why.Add("weapon outside " + owner.Name + "'s line"); }
            else { c.Score = 2; c.Why.Add("weapon tier-up"); }
            c.Why.AddRange(synWhy);
            if (paid > 0) c.Why.Add("tree boost " + (paid + 1) + "/" + G.NodeMax(node));
        }

        // ---------------------------------------------------------------- abilities
        internal sealed class AbilityVerdict
        {
            public double Score; public readonly List<string> Why = new List<string>(); public bool EvoOwned; public string Tier; public PowerupBase EvoA, EvoB; public int Priority = -1; public bool Skipped;
            /// <summary>The one short thing that sets this ability apart on this squad ("unlocks the Kinetic special", "#1 in
            /// Rifleman", "synergy: Tank", "S-tier", "Kinetic: 100% of the squad"), for the headline under the card.</summary>
            public string Head; int _headRank;
            /// <summary>The Why line the head was cut from: the card shows the head, so it leaves that line out ("A-tier in the
            /// guides; A-tier ability in the guides" said the same thing twice).</summary>
            public string HeadFrom;
            public void Mark(int rank, string text, string from = null) { if (rank > _headRank) { _headRank = rank; Head = text; HeadFrom = from; } }
        }

        /// <summary>What speaks for an ability on this squad right now, whatever its level: the build's order (or the guides'
        /// tier on Auto), bought synergy nodes, an unlocked evolution, tree boosts, its damage types, team passives.</summary>
        internal static AbilityVerdict AbilityScore(PowerupBase ability, Survivor owner, Snapshot s)
        {
            var v = new AbilityVerdict();
            string name = G.Name(ability);
            var build = owner.Build;
            if (build != null)
            {
                v.Priority = build.PriorityOf(name); v.Skipped = build.Skips(name);
                if (v.Skipped) { v.Score -= 2.0; v.Why.Add("your " + build.Name + " build skips it"); v.Mark(6, build.Name + " skips it", v.Why[v.Why.Count - 1]); }
                else if (v.Priority >= 0)
                {
                    double[] bonus = { 1.6, 1.0, 0.5, 0.1 };
                    v.Score += bonus[Math.Min(v.Priority, bonus.Length - 1)];
                    v.Why.Add("#" + (v.Priority + 1) + " in your " + build.Name + " build");
                    v.Mark(v.Priority <= 1 ? 4 : 1, "#" + (v.Priority + 1) + " in " + build.Name, v.Why[v.Why.Count - 1]);
                }
            }
            else
            {
                string tier; if (K.AbilityTier.TryGetValue(name, out tier)) { v.Tier = tier; double b = Knowledge.Tier(tier, 1.2, 0.6, 0, -0.8); if (b != 0) { v.Score += b; v.Why.Add(tier + "-tier ability in the guides"); v.Mark(b > 0 ? 2 : 1, tier.ToUpperInvariant() + "-tier in the guides", v.Why[v.Why.Count - 1]); } }
            }
            if (owner.Props != null)
            {
                foreach (var syn in G.Each(owner.Props.skillTreeSynergies))
                {
                    if (syn == null || !G.NodeOwned(syn)) continue;
                    PowerupBase req = null; try { req = syn.requiredPowerup; } catch { }
                    if (req == null || !G.Same(req, ability)) continue;
                    CT partner; try { partner = syn.synergiesWithClass; } catch { continue; }
                    if (s.OnSquad(partner)) { v.Score += 2.0; v.Why.Add("synergy with " + G.ClassName(partner) + " on the team"); v.Mark(3, "synergy: " + G.ClassName(partner), v.Why[v.Why.Count - 1]); }
                    else if (!s.SquadFull && s.Ctx.RecruitValue > 0.5) { v.Score += 0.6; v.Why.Add("synergy if you recruit " + G.ClassName(partner)); }
                }
            }
            PowerupBase evoA = null, evoB = null;
            try { evoA = ability.abilityEvolutionA; evoB = ability.abilityEvolutionB; } catch { }
            v.EvoA = evoA; v.EvoB = evoB;
            SkillTreeUpgradeBase evoNode = null;
            try { if (evoA != null) evoNode = evoA.skillTreeRequirement; } catch { }
            try { if (evoNode == null && evoB != null) evoNode = evoB.skillTreeRequirement; } catch { }
            if (evoNode != null && G.NodeOwned(evoNode)) v.EvoOwned = true;
            SkillTreeUpgradeBase boost = null; try { boost = ability.skillTreeAbilityBoost; } catch { }
            if (boost == null) { try { boost = ability.skillTreeRequirement; } catch { } }
            int paid = Math.Max(0, G.NodeLevel(boost) - 1);
            if (paid > 0) { v.Score += 0.3 * paid; v.Why.Add("tree boost " + (paid + 1) + "/" + G.NodeMax(boost)); }

            var facts = G.Facts(ability);

            double tagValue = Synergy.TagValue(facts, s.Tags, s.Ctx, v.Why), boostValue = Synergy.BoostValue(facts, s.Boosts, s.Ctx, v.Why);
            v.Score += tagValue + boostValue;
            if (v.Why.Count > 0 && v.Why[0].StartsWith("this level unlocks the ", StringComparison.Ordinal))
            {   // Synergy puts a special within one level first: "this level unlocks the Kinetic special (10 tags)"
                string t = v.Why[0].Substring("this level unlocks the ".Length); int sp = t.IndexOf(' ');
                v.Mark(5, "unlocks the " + (sp > 0 ? t.Substring(0, sp) : t) + " special");
            }
            if (boostValue > 0) foreach (var b in s.Boosts) if (b.Covers(facts)) { v.Mark(3, b.Name + " boosts it"); break; }
            if (tagValue >= 0.45 && facts.Damage.Count > 0)
            {
                string best = null; double share = 0;
                foreach (var t in facts.Damage) { double sh = s.Tags.Share(t); if (sh > share) { share = sh; best = t; } }
                if (best != null && share >= 0.4) v.Mark(1, best + ": " + (int)Math.Round(share * 100) + "% of the squad");
            }
            if (facts.Healing && s.Ctx.Survival <= 0) { v.Score -= 1.5; v.Why.Add("healing means nothing in One Hit"); }
            else if (facts.Healing && s.Ctx.Survival >= 1.3) { v.Score += 0.4; v.Why.Add("healing: survival matters now"); }
            return v;
        }

        /// <summary>The ability this survivor should be feeding: the build's highest-ranked one that can still level, else
        /// the one furthest along (ties: the better verdict). The card ranking and the PLAN readout share it.</summary>
        internal static PowerupBase FocusAbility(Survivor owner, Snapshot s)
        {
            var open = owner.Abilities().Where(kv => kv.Value < G.MaxLevel(kv.Key)).ToList();
            if (open.Count == 0) return null;
            var build = owner.Build;
            if (build != null)
            {
                var ranked = open.Where(kv => build.PriorityOf(G.Name(kv.Key)) >= 0 && !build.Skips(G.Name(kv.Key))).OrderBy(kv => build.PriorityOf(G.Name(kv.Key))).ToList();
                if (ranked.Count > 0) return ranked[0].Key;
            }
            return open.OrderByDescending(kv => kv.Value).ThenByDescending(kv => AbilityScore(kv.Key, owner, s).Score).First().Key;
        }

        /// <summary>The ability card the ranking would put first for this survivor right now: the next level of an owned
        /// ability, or <paramref name="missing"/> (the best ability not owned yet; may be null) - by the very scores the cards
        /// get. The PLAN readout asks here instead of keeping rules of its own, so that its row names what the cards will.</summary>
        internal static PowerupBase TopAbility(Survivor owner, Snapshot s, PowerupBase missing)
        {
            var focus = FocusAbility(owner, s);
            var pool = owner.Abilities().Where(kv => kv.Value < G.MaxLevel(kv.Key)).Select(kv => kv.Key).ToList();
            if (missing != null) pool.Add(missing);
            PowerupBase best = null; double bestScore = double.MinValue;
            foreach (var p in pool)
            {
                var c = new Card(); ScoreAbility(c, p, owner, s, focus, true);
                double sc = Math.Round(c.Score, 2);
                if (sc > bestScore) { bestScore = sc; best = p; }      // a tie: the owned ability, listed first
            }
            return best;
        }

        static double SoftCap(double x) { return x <= 4.6 ? x : 4.6 + (x - 4.6) * 0.3; }     // keeps the order among strong abilities, stays under 6

        static void ScoreAbility(Card c, PowerupBase p, Survivor owner, Snapshot s, PowerupBase focusKnown = null, bool haveFocus = false)
        {
            var a = AbilityScore(p, owner, s);
            int lvl = owner.LevelOf(p);
            int max = G.MaxLevel(p);
            var owned = owner.Abilities();
            var ctx = s.Ctx;
            double score, once = 0;
            if (lvl == 0)
            {
                // "take each ability once": a new damage source early is worth more than another level of an old one -
                // while there is still time for it to grow
                double baseNew = owned.Count < 2 ? 3.6 : owned.Count < 4 ? 3.1 : 2.4;
                double reach = ctx.Reach(3);
                score = baseNew * (0.55 + 0.45 * reach) + (ctx.Progress < 0.3 ? 0.3 : 0) + a.Score * 0.5;
                string what = reach < 0.6 ? "new ability, little time left" : "new ability";
                c.Why.Add(a.Head != null ? what + " - " + a.Head : owned.Count < 4 && reach >= 0.6 ? "new ability: take each once early" : what);
                if (a.Head != null && owned.Count < 4 && reach >= 0.6) c.Why.Add("take each ability once early");
                if (reach < 0.6) c.Why.Add("little time to level it (" + ctx.ClockText + ")");
                // Balanced says "each ability once early, THEN the weapon and the focus ability side by side" - but a third or
                // fourth new ability (base 3.1) sat under the weapon floor (4.3): a run's first level-ups all went to the weapon
                // and half the ability slots were still empty at the end. So under Balanced, while the clock still lets a fresh
                // ability grow up, the first level of a missing ability goes before a plain weapon level. "Grow up" is its four
                // levels and the evolution, counted twice because the weapon and the focus ability want their picks as well:
                // Reach(10) - the full lift while some thirty level-ups are still to come (the first half of a 20:00 run), fading
                // to nothing at about eleven, well before the "little time left" penalty above sets in. A plain weapon level
                // is 4.4 to 5.0 as a rule; squeezed in under 6.1 below, the lift never passes a weapon tier-up (6.2 and more),
                // a recruit's first weapon or an evolution. The other styles, and an ability the build skips, are left alone.
                if (StyleOf(owner) == BuildStyle.Balanced && owned.Count < 4 && !a.Skipped) once = 1.5 * Math.Max(0, Math.Min(1, (ctx.Reach(10) - 0.4) / 0.6));
                if (once >= 0.3) c.Why.Add("style: balanced - before another weapon level");
            }
            else
            {
                var focusAbility = haveFocus ? focusKnown : FocusAbility(owner, s);
                bool focus = focusAbility != null && G.Same(focusAbility, p);
                bool completes = lvl == max - 1;
                score = 2.6 + (focus ? 1.0 : 0.2) + (completes ? 0.5 : 0) + a.Score * 0.5;
                if (a.EvoOwned)
                {
                    double reach = ctx.Reach(max - lvl + 1);           // the levels left, plus the evolution card itself
                    score += 0.9 * reach;
                    c.Why.Add((focus ? "focus " : "") + lvl + ">" + (lvl + 1) + " of " + max + (reach >= 0.5 ? " toward its evolution" : ", evolution out of reach") + (a.Head != null ? " - " + a.Head : ""));
                    if (reach < 0.5) c.Why.Add("too few level-ups left to evolve it (" + ctx.ClockText + ")");
                }
                else c.Why.Add((focus ? "focus " : completes ? "completes it, " : "level ") + lvl + ">" + (lvl + 1) + " of " + max + (a.Head != null ? " - " + a.Head : ""));
            }
            c.Score = SoftCap(score);
            // the lift, squeezed above 5.6 so that two new abilities keep their order and none reaches a tier-up's 6.2
            if (once > 0) { double lifted = c.Score + once; c.Score = Math.Max(c.Score, lifted <= 5.6 ? lifted : 5.6 + (lifted - 5.6) * 0.25); }
            foreach (var line in a.Why) if (a.Head == null || line != a.HeadFrom) c.Why.Add(line);      // the head already says that one
        }

        // ---------------------------------------------------------------- evolutions
        static void ScoreEvolution(Card c, PowerupBase evo, PowerupBase baseAbility, Survivor owner, Snapshot s)
        {
            var parent = AbilityScore(baseAbility, owner, s);
            string baseName = G.Name(baseAbility), evoName = G.Name(evo);
            var build = owner.Build;
            string pick = build == null ? null : build.EvolutionOf(baseName);
            var fitWhy = new List<string>();
            double fit = Synergy.EvolutionFit(G.Facts(evo), G.Facts(baseAbility), s.Tags, s.Boosts, s.Ctx, fitWhy);
            c.Score = 7.6 + parent.Score * 0.1 + fit * 0.4;
            if (pick != null)
            {
                bool mine = SameName(pick, evoName);
                c.Score += mine ? 1.0 : -0.5;
                c.Why.Add(mine ? "evolution of " + baseName + ": your " + build.Name + " build's pick" : "evolution of " + baseName + "; your build takes " + pick);
            }
            else c.Why.Add("evolution of " + baseName + (fitWhy.Count > 0 ? ": " + fitWhy[0] : ""));
            c.Why.AddRange(pick != null ? fitWhy : fitWhy.Skip(1));
        }

        // ---------------------------------------------------------------- items (chests)
        static void ScoreItem(Card c, Snapshot s)
        {
            c.Kind = "item"; c.Name = G.Name(c.Item);
            c.Score = ItemScore(c.Item, s, c.Why);
        }

        /// <summary>What the pure item rules may know, read once per snapshot.</summary>
        internal static ItemContext ItemContextOf(Snapshot s)
        {
            var c = new ItemContext { Tags = s.Tags, K = K, Ctx = s.Ctx };
            c.Squad = s.Squad.Select(x => x.Name).ToList();
            c.CritSquad = c.Squad.Any(x => K.CritSquad.Contains(x, StringComparer.OrdinalIgnoreCase));
            c.OwnedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            c.Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            double weaponLevels = 0, abilityLevels = 0; int weapons = 0, close = 0, far = 0, clips = 0;
            foreach (var sv in s.Squad)
            {
                foreach (var kv in sv.Items) c.Held.Add(G.Name(kv.Key));
                foreach (var kv in sv.Powerups)
                {
                    if (kv.Value < 1 || kv.Key == null) continue;
                    try { foreach (var t in G.Each(kv.Key.powerupTags)) c.OwnedTags.Add(t.ToString()); } catch { }
                    if (G.IsAbility(kv.Key)) { abilityLevels += kv.Value; if (SameName(G.Name(kv.Key), "Energy Shield") || (G.IsEvolution(kv.Key) && G.Name(kv.Key).StartsWith("Energy Shield"))) c.ShieldOwned = true; }
                }
                if (sv.Weapon != null && sv.LevelOf(sv.Weapon) >= 1)
                {
                    weaponLevels += sv.LevelOf(sv.Weapon) * 1.5; weapons++;
                    try
                    {
                        var w = sv.Weapon.TryCast<WeaponUpgradePowerup>(); var wp = w == null ? null : w.attachedWeaponProperties;
                        if (wp != null)
                        {
                            var rm = wp.rangeMod;
                            if (rm.Close > rm.Long + 0.01f) close++; else if (rm.Long > rm.Close + 0.01f) far++;
                            if (wp.HasClipBehavior && wp.ClipSize > 1) clips++;
                        }
                    }
                    catch { }
                }
                var b = sv.Build;
                if (b != null) foreach (var want in b.Wants) c.Wants.Add(new KeyValuePair<string, string>(b.Name, want));
            }
            if (weapons > 0) { c.CloseShare = close / (double)weapons; c.LongShare = far / (double)weapons; c.ClipShare = clips / (double)weapons; }
            if (weaponLevels + abilityLevels > 0) c.AbilityLean = abilityLevels / (weaponLevels + abilityLevels);
            return c;
        }

        /// <summary>The pure rules (shared with the offline bench), then the game-only parts: quest target, already held.
        /// Shared by the chest cards and the plan panel; pass the context when scoring many items against one snapshot.</summary>
        internal static double ItemScore(ItemBase it, Snapshot s, List<string> why, ItemContext shared = null)
        {
            var facts = G.FactsOf(it);      // name, text, statistics: asset data, read once per item
            var ic = shared ?? ItemContextOf(s);
            ic.Stats = facts.Stats; ic.Healing = facts.Healing;
            double score = ItemRules.Evaluate(facts.Name, facts.Desc, ic, why);
            try
            {
                var qm = s.ActiveQuests;
                if (qm != null && qm.IsActiveQuestTargetItem(it)) { score += 3; why.Insert(0, "quest target"); }
            }
            catch { }
            if (s.AnyoneHas(it))
            {
                int max = facts.MaxCarry;
                if (max <= 1) { score -= 1.0; why.Insert(0, "already held"); }
                else why.Add("stacks, already held");
            }
            if (why.Count == 0) why.Add("no squad-specific value");
            return score;
        }

        // ---------------------------------------------------------------- SOS: survivors and Liberate
        static void ScoreSos(Card c, Snapshot s)
        {
            var p = c.Powerup; string asset = G.Asset(p);
            if (asset.StartsWith("Loot Character", StringComparison.OrdinalIgnoreCase) || asset.IndexOf("SendToBase", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                c.Kind = "liberate"; c.Name = "Liberate";
                double left = s.Ctx.RecruitValue;
                int farming = Doctrine.Current.Farming;
                if (s.SquadFull) { c.Score = 5; c.Why.Add("squad is full: take the level-up and cash"); }
                else
                {
                    c.Score = 1.0 + 3.2 * (1 - left) + 0.4 * farming;
                    c.Why.Add(left < 0.45 ? "a recruit no longer has time to grow (" + s.Ctx.ClockText + "): take the level-up and cash" : "level-up and cash instead of a recruit");
                }
                return;
            }
            c.Kind = "survivor";
            ClassProperties props = null; try { props = p.targetClassProperties; } catch { }
            if (props == null) { c.Score = 0.5; c.Why.Add("unknown survivor"); return; }
            CT cls = props.characterType; c.Name = G.ClassName(cls);
            if (s.OnSquad(cls)) { c.Score = 0; c.Why.Add("already on the squad"); return; }
            c.Score = RecruitScore(cls, props, s, c.Why);
        }

        /// <summary>What a recruit brings to THIS squad: bought synergy nodes (an unbought node does nothing in a run), damage
        /// types shared with the squad, team passives either way, the guides' rescue tier, how trained the recruit is - all
        /// scaled by the time left for them to grow. Shared by the SOS cards and the plan panel.</summary>
        internal static double RecruitScore(CT cls, ClassProperties props, Snapshot s, List<string> why)
        {
            string name = G.ClassName(cls);
            double fixedPart = 2.0, fit = 0;         // a third gun and +20 % XP for the rest of the run: never worth less than this early
            string tier; K.RescueTier.TryGetValue(name, out tier);
            if (tier != null) { fit += Knowledge.Tier(tier, 1.6, 1.1, 0.6, 0.1); why.Add(tier + "-tier rescue in the guides"); }

            int owned = 0, unowned = 0; var partners = new List<string>();
            foreach (var sv in s.Squad)
            {
                int o = 0;
                if (sv.Props != null) foreach (var syn in G.Each(sv.Props.skillTreeSynergies))
                {
                    CT partner; try { partner = syn.synergiesWithClass; } catch { continue; }
                    if (partner != cls) continue;
                    if (G.NodeOwned(syn)) o++; else unowned++;
                }
                if (props != null) foreach (var syn in G.Each(props.skillTreeSynergies))
                {
                    CT partner; try { partner = syn.synergiesWithClass; } catch { continue; }
                    if (partner != sv.Type) continue;
                    if (G.NodeOwned(syn)) o++; else unowned++;
                }
                owned += o;
                if (o > 0) partners.Add(o + " with " + sv.Name);
            }
            fit += 1.1 * owned;
            if (owned > 0) why.Add((owned == 1 ? "1 bought synergy: " : owned + " bought synergies: ") + string.Join(", ", partners));
            else if (unowned > 0) why.Add(unowned + " synergy node" + (unowned > 1 ? "s" : "") + " with this squad, none bought yet");

            // shared damage types: the recruit's weapon line and abilities feed the tags the squad already stacks
            double share = 0; string shared = null;
            if (props != null && s.Tags.Known)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var list in new[] { props.weaponPowerups, props.abilityBasePowerups })
                    foreach (var pw in G.Each(list))
                    {
                        if (pw == null) continue;
                        foreach (var t in G.Facts(pw).Damage) if (seen.Add(t)) { double sh = s.Tags.Share(t); if (sh > share) { share = sh; shared = t; } }
                    }
                if (share >= 0.2) { fit += 1.2 * share * Doctrine.Current.SynergyWeight; why.Add("deals " + shared + " like " + (s.Tags.SourceText(shared).Length > 0 ? s.Tags.SourceText(shared) : "the squad")); }
            }

            // team passives: the recruit's own (they switch on once on the team), and the squad's that cover the recruit's kit
            foreach (var n in G.NodesOf(cls))
            {
                if (!G.NodeOwned(n)) continue;
                var tagged = n.TryCast<SkillTreeUpgradeTaggedPowerupBoost>();
                if (tagged != null)
                {
                    string tag = ""; try { tag = tagged.requiredTag.ToString(); } catch { }
                    int covered = 0;
                    foreach (var sv in s.Squad) foreach (var kv in sv.Powerups) { if (kv.Value < 1 || kv.Key == null) continue; try { if (kv.Key.HasPowerupTag(tagged.requiredTag)) covered++; } catch { } }
                    if (covered > 0) { fit += Math.Min(0.9, 0.3 * covered); string nn = ""; try { nn = n.GetName(); } catch { } why.Add((nn.Length > 0 ? nn : tag + " passive") + " would boost " + covered + " of your powerups"); }
                    continue;
                }
                string d = G.NodeDesc(n);
                if (d.IndexOf("on the team", StringComparison.OrdinalIgnoreCase) >= 0) { fit += 0.5; why.Add(ShortDesc(d)); }
            }

            int lvl = props != null ? G.TreeLevel(props) : 0;
            fit += Math.Min(1.0, lvl / 120.0) * 0.6;                 // a trained recruit arrives with more unlocked
            if (Doctrine.Current.Farming >= 1)
            {
                int nextThr = -1; foreach (var thr in RankLevels) if (thr > lvl) { nextThr = thr; break; }
                if (nextThr > 0 && nextThr - lvl <= 5) { fit += 0.3; why.Add((nextThr - lvl) + " level" + (nextThr - lvl > 1 ? "s" : "") + " from rank " + (Array.IndexOf(RankLevels, nextThr) + 1)); }
            }
            double left = s.Ctx.RecruitValue;
            if (left < 0.45) why.Insert(0, "little time left for a recruit to grow (" + s.Ctx.ClockText + ")");
            else if (tier != null && owned > 0) why.Insert(0, tier + "-tier rescue, " + (owned == 1 ? "1 bought synergy" : owned + " bought synergies") + " with the squad");
            if (why.Count == 0) why.Add("L" + lvl + ", no synergy with this squad");
            return fixedPart * left + fit * (0.35 + 0.65 * left);
        }

        // ---------------------------------------------------------------- military training and Endless stat cards
        static void ScoreMilitary(Card c, Snapshot s)
        {
            var p = c.Powerup; c.Kind = "stat";
            var b = p.TryCast<BasicLevelPowerup>();
            string rarity = "Common"; try { if (b != null) rarity = b.GetRarity().ToString(); } catch { }
            // the cards' own numbers go about 1 : 2 : 3-4 by rarity (Luck 5 / 10 / 20, Ability Area 10 / 20 / 30): a Rare is
            // twice the Common of its stat, a Legendary three times - at 1.6 / 2.3 a Legendary of a modest stat lost to a
            // Common of a good one, and both times the player overrode the mod that was why. Endless keeps its place
            // just under Legendary.
            double r = rarity == "Legendary" ? 3.0 : rarity == "Endless" ? 2.6 : rarity == "Rare" ? 2.0 : 1.0;
            string asset = G.Asset(p); string stat = asset.StartsWith("MilitaryTraining_") ? asset.Substring("MilitaryTraining_".Length) : asset;
            double w = 0.5; bool known = false;
            foreach (var kv in K.MilitaryStat) if (string.Equals(stat, kv.Key, StringComparison.OrdinalIgnoreCase)) { w = kv.Value; known = true; }
            if (!known) foreach (var kv in K.MilitaryStat) if (stat.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0) w = Math.Max(w, kv.Value);
            string target = ""; try { if (b != null) target = b.targetType.ToString(); } catch { }
            bool team = target.IndexOf("Team", StringComparison.OrdinalIgnoreCase) >= 0;

            var ctx = s.Ctx; string note = null;
            var ic = ItemContextOf(s);
            if (stat.StartsWith("Weapon", StringComparison.OrdinalIgnoreCase)) { double k = 0.7 + 0.6 * (1 - ic.AbilityLean); w *= k; if (k >= 1.1) note = "the squad's damage is mostly weapons"; }
            else if (stat.StartsWith("Ability", StringComparison.OrdinalIgnoreCase)) { double k = 0.7 + 0.6 * ic.AbilityLean; w *= k; if (k >= 1.1) note = "the squad's damage is mostly abilities"; }
            if (stat.IndexOf("Crit", StringComparison.OrdinalIgnoreCase) >= 0 && ic.CritSquad) { w *= 1.15; }
            if (stat.StartsWith("XP", StringComparison.OrdinalIgnoreCase) || stat == "Luck" || stat == "MagnetRange")
            {
                w *= ctx.Economy;
                note = ctx.Economy >= 1.25 ? "pays back all run (" + ctx.ClockText + ")" : ctx.Economy <= 0.5 ? "too late to pay back (" + ctx.ClockText + ")" : note;
            }
            else if (stat == "MaxHealth" || stat == "Armor" || stat == "HPRegen")
            {
                w *= Math.Max(0, ctx.Survival);
                note = ctx.Survival >= 1.3 ? "survival matters now" + (ctx.Health < 0.5 ? " (the squad is hurting)" : "") : note;
            }
            else if (stat == "DodgeChance" || stat.StartsWith("MovementSpeed", StringComparison.OrdinalIgnoreCase)) w *= Math.Max(ctx.Control, Math.Min(1.3, ctx.Survival));
            foreach (var sv in s.Squad)
            {
                var build = sv.Build; if (build == null) continue;
                bool wants = (stat.StartsWith("Weapon", StringComparison.OrdinalIgnoreCase) && build.Wants.Contains("weapons", StringComparer.OrdinalIgnoreCase))
                    || (stat.StartsWith("Ability", StringComparison.OrdinalIgnoreCase) && build.Wants.Contains("abilities", StringComparer.OrdinalIgnoreCase))
                    || (stat.IndexOf("Crit", StringComparison.OrdinalIgnoreCase) >= 0 && build.Wants.Contains("critical", StringComparer.OrdinalIgnoreCase))
                    || ((stat == "Armor" || stat == "MaxHealth" || stat == "HPRegen") && (build.Wants.Contains("armor", StringComparer.OrdinalIgnoreCase) || build.Wants.Contains("healing", StringComparer.OrdinalIgnoreCase)));
                if (wants) { w *= 1.2; note = "your " + build.Name + " build wants it"; break; }
            }
            c.Score = 1.0 + r * w * 2.0 + (team ? 0.2 : 0);
            c.Why.Add(rarity + (team ? ", team-wide" : "") + ": " + Humanize(stat) + (note != null ? " - " + note : ""));
        }

        // ---------------------------------------------------------------- Research Pod rewards: damage type tag points
        static void ScoreHashtag(Card c, Snapshot s)
        {
            c.Kind = "tags";
            string type = null; int n = 1;
            try { type = G.TagName(c.Hashtag.hashtagType); } catch { }
            try { n = c.Hashtag.numUpgrades; } catch { }
            if (type == null) { c.Name = "#?"; c.Score = 1; c.Why.Add("unknown tag type"); return; }
            c.Name = "#" + type + " +" + n;
            c.Score = Tags.Score(type, n, s.Tags, c.Why);
        }

        // ---------------------------------------------------------------- helpers
        static Survivor OwnerOf(PowerupBase p, Snapshot s)
        {
            try { var gp = p.GetOwner(); if (gp != null) foreach (var sv in s.Squad) if (sv.Player != null && sv.Player.Pointer == gp.Pointer) return sv; } catch { }
            try { var cp = p.targetClassProperties; if (cp != null) return s.Find(cp.characterType); } catch { }
            return null;
        }
        static string ClassOf(PowerupBase p)
        {
            try { var cp = p.targetClassProperties; if (cp != null) return G.ClassName(cp.characterType); } catch { }
            return null;
        }
        static string ShortDesc(string d)
        {
            d = ItemRules.RichTag.Replace(d ?? "", "").Replace("\n", " ").Trim();
            return d.Length > 44 ? d.Substring(0, 41) + "..." : d;
        }
        static string Humanize(string s)
        {
            if (s.EndsWith("Mod", StringComparison.Ordinal) && s.Length > 3) s = s.Substring(0, s.Length - 3);     // WeaponDamageMod -> WeaponDamage; "Modifier" stays whole
            s = s.Replace("XPModifier", "XP").Replace("HPRegen", "HealthRegen");
            var sb = new System.Text.StringBuilder();
            foreach (var ch in s) { if (char.IsUpper(ch) && sb.Length > 0 && sb[sb.Length - 1] != ' ' && !char.IsUpper(sb[sb.Length - 1])) sb.Append(' '); sb.Append(ch); }
            return sb.ToString().Trim();
        }
    }
}
