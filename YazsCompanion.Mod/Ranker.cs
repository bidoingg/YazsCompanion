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
//              after; Ability: the abilities first, the weapon fills in - from 0.13.0 (C2) it fills in ahead of the other
//              survivors' ability levels once the survivor's own abilities are as good as done (WeaponLift in Synergy.cs).
//  Chests:     guide tier, then the fit with this squad, this clock, what is held (ItemRules).
//  SOS:        synergy nodes that are BOUGHT, shared damage types, team passives, the guides' rescue tier - scaled by
//              the time a recruit still has to grow; Liberate once that time has run out, or when the squad is full; the
//              active quest's team rules over all of it (0.13.0, C1: QuestTeam.cs - "quest: stay solo").
//  The quest:  every other objective of the active quest after all of the above (0.14.0, C3: QuestRules.cs) - the weapon line it
//              wants maxed over the abilities, a health item +2.0, AVOID on a pick that would fail it, a modest lift for kills
//              with a class that never passes the build's core; the build plan stays ([Advice] QuestSteer).
//  Military:   rarity x stat weight, the weight moved by the squad's weapon / ability split and by the clock; an Endless
//              card by its own value against the Common card of its stat (0.14.0, B1: it was a flat near-Legendary 2.6).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using BonusList = Il2CppSystem.Collections.Generic.List<PowerupBase.StatisticBonus>;
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
        public Recruit Recruit;          // SOS: what orders two survivors that score the same (null for Liberate)
        public double Score;
        public int Rank;
        public readonly List<string> Why = new List<string>();   // Why[0] is the ranking's headline: the log's (and the card's, without Display)
        public string Reason { get { return Why.Count > 0 ? Why[0] : ""; } }
        // 0.14.0 (A1): what the card SAYS under it - built from Say by Wording once the ranks are final (null: the headline above).
        // The log keeps Why[0] as it was, so weeks of [card] lines stay comparable; the [shown] line logs what was drawn.
        public CardWords Say;
        public string Display;
        internal bool LiftedWeapon;      // 0.13.0 (C2): a held weapon's level on the lifted floor - kept under the owner's own open build ability
        internal bool OwnBuildOpen;      // an ability card the owner's build ranks (no build: any), short of its last level
        internal string Rarity, StatKey; // a stat card's rarity and its stat (the asset's: AbilitySize, TeamArmor), for the quest's rules
    }

    internal static class Ranker
    {
        static readonly int[] RankLevels = { 0, 20, 40, 60, 80 };
        static Knowledge K { get { return Knowledge.Current; } }

        public static void Rank(Screen screen, List<Card> cards, Snapshot s)
        {
            foreach (var c in cards)
            {
                try { Score(screen, c, s, cards); }
                catch (Exception e) { c.Score = 0; c.Why.Insert(0, "not ranked (" + e.GetType().Name + ")"); c.Say = new CardWords { Text = "not ranked - an error, see the log" }; Plugin.Logger.LogWarning("rank " + c.Name + ": " + e); }
                c.Score = Math.Round(c.Score, 2);
            }
            try { UnderOwnAbility(cards); } catch (Exception e) { Plugin.Logger.LogWarning("rank: weapon cap - " + e.Message); }
            try { QuestPass(cards, s); } catch (Exception e) { if (!_questWarned) { _questWarned = true; Plugin.Logger.LogWarning("[rank] the quest's rules not applied (" + e.GetType().Name + " " + e.Message + ") - said once a session"); } }
            // an exact tie goes to the card further left - except between two recruits, which the PLAN readout's SOS row
            // ranks too: there both follow Recruit.Ties (0.12.2, F12), so the card framed is the survivor the row names first
            var order = screen == Screen.SOS
                ? cards.OrderByDescending(c => c.Score).ThenBy(c => c, RecruitTies).ThenBy(c => c.Index).ToList()
                : cards.OrderByDescending(c => c.Score).ThenBy(c => c.Index).ToList();
            for (int i = 0; i < order.Count; i++) order[i].Rank = i + 1;
            SayAll(order);
        }

        /// <summary>The names another mod lends, as the cards and the WHY band draw them (one delegate for all of them).</summary>
        internal static readonly Func<string, string> LendNames = Names.Text;

        // 0.14.0 (A1): the lines the cards show, once the ranks are final - a lifted weapon's line names the cards above it, and two
        // evolutions of one base say what sets them apart. A line that fails leaves its card on its headline (said once a session).
        // Each line is measured as it will be drawn: beside its "2ND" or "AVOID", with the names another mod lends (the character
        // packs' names run up to 19 characters longer than the game's). Two cards drawing the same line: the lower one says its next
        // reason (WhyText.Distinct).
        static bool _sayWarned, _distinctWarned;
        static void SayAll(List<Card> order)
        {
            string first = order.Count > 0 ? order[0].Name : null, second = order.Count > 1 ? order[1].Name : null;
            try { Wording.Pair(order.ConvertAll(c => c.Say)); } catch { }
            try { Wording.Ranks(order.ConvertAll(c => c.Say), order.ConvertAll(c => c.Name)); } catch { }      // 0.15.0 (C15-03 a): the WHY band's rivals
            var rooms = new List<int>(order.Count);
            // 0.15.0 (the review of 10-06): the line's room follows the card text's size on this screen - 56 characters at the PC's
            // size, 42 at the Deck's x1.56 (the rect is 900 units at most) - so a larger size picks a shorter form, not an ellipsis
            int width = Wording.DeckWidth;
            try { width = Badge.LineChars(); } catch { }
            foreach (var c in order)
            {
                int room = Wording.Budget;
                try { room = Wording.RoomBeside(Synergy.ReasonPrefixWidth(c.Rank, c.Score), width); } catch { }
                rooms.Add(room);
                try { c.Display = Wording.Card(c.Say, c.Rank, first, second, room, LendNames); }
                catch (Exception e) { c.Display = null; if (!_sayWarned) { _sayWarned = true; Plugin.Logger.LogWarning("[rank] the words of " + c.Name + " failed (" + e.GetType().Name + " " + e.Message + ") - its headline instead"); } }
            }
            try
            {
                var lines = order.ConvertAll(c => c.Display);
                WhyText.Distinct(lines, order.ConvertAll(c => c.Say), order.ConvertAll(c => (IList<string>)c.Why), rooms, LendNames);
                for (int i = 0; i < order.Count; i++) order[i].Display = lines[i];
            }
            catch (Exception e) { if (!_distinctWarned) { _distinctWarned = true; Plugin.Logger.LogWarning("[rank] two cards' equal lines not told apart (" + e.GetType().Name + " " + e.Message + ") - said once a session"); } }
        }

        static readonly IComparer<Card> RecruitTies = Comparer<Card>.Create((a, b) => Recruit.Ties(a.Recruit, b.Recruit));

        // 0.13.0 (C2): a lifted weapon level stays just under a card of its owner's own open build ability on the same offer -
        // "abilities first" still holds for the survivor's own abilities, only the other survivors' levels go after it
        static void UnderOwnAbility(List<Card> cards)
        {
            foreach (var c in cards)
            {
                if (!c.LiftedWeapon || c.Owner == null) continue;
                Card own = null;
                foreach (var o in cards) if (o != c && o.OwnBuildOpen && o.Owner == c.Owner && (own == null || o.Score > own.Score)) own = o;
                if (own == null) continue;
                double capped = WeaponLift.UnderOwn(c.Score, own.Score);
                if (capped >= c.Score) continue;
                c.Score = capped;
                int at = c.Why.FindIndex(w => w.StartsWith("style: ", StringComparison.Ordinal));
                c.Why.Insert(at >= 0 ? at + 1 : Math.Min(1, c.Why.Count), WeaponLift.UnderOwnLine(own.Name));
            }
        }

        static void Score(Screen screen, Card c, Snapshot s, List<Card> offer)
        {
            if (c.Item != null) { ScoreItem(c, s); return; }
            if (c.Hashtag != null) { ScoreHashtag(c, s); return; }
            var p = c.Powerup;
            if (p == null) { c.Kind = "empty"; c.Score = 0.5; c.Why.Add("nothing attached"); c.Say = new CardWords { Text = "nothing on this card" }; return; }
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
                c.Say = new CardWords { Text = cls == null ? "not for anyone on the squad" : cls + " is not on the squad" };
                return;
            }
            c.Owner = owner;

            if (evoBase != null) { c.Kind = "evolution"; ScoreEvolution(c, p, evoBase, owner, s, offer); }
            else if (w != null) { c.Kind = "weapon"; ScoreWeapon(c, w, owner, s); }
            else if (isAbility) { c.Kind = "ability"; ScoreAbility(c, p, owner, s); }
            else { c.Kind = "other"; c.Score = 1; c.Why.Add("powerup outside the tree"); c.Say = new CardWords { Text = "outside the squad's skill trees" }; }
        }

        // ---------------------------------------------------------------- names and styles
        /// <summary>Same name, forgiving punctuation and the "Ability: " prefix of an evolution; 0.15.0: else the same powerup by its
        /// asset key, so a display name the game renames in a patch never breaks a match (Builds.SameName: pure, the bench runs it).</summary>
        internal static bool SameName(string a, string b) { return Builds.SameName(a, b); }

        internal static BuildStyle StyleOf(Survivor owner) { StyleSource from; return StyleOf(owner, out from); }

        /// <summary>The level-up style the advice follows for <paramref name="owner"/>, and where it comes from (0.15.0, C15-07: Builds.StyleFor)
        /// - a build the player selected brings its own, plain Auto follows [Advice] LevelUpStyle, a build another mod lends that Auto
        /// follows brings its own unless [Advice] LentBuildStyle = Mine. Up to 0.14.0 the lent build's style won silently while the
        /// cfg said LevelUpStyle was for survivors on Auto; now the cards, the PLAN and the [ctx] line say whose it is.</summary>
        internal static BuildStyle StyleOf(Survivor owner, out StyleSource from)
        {
            var b = owner == null ? null : owner.Build;
            var d = Doctrine.Current;
            // on Auto a survivor's build is always one a pack lends (Builds.AutoFor): the selection is read only when there is a build
            return Builds.StyleFor(b, b != null && Builds.OnAuto(owner.Name), d.Style, d.LentStyleMine, out from);
        }

        /// <summary>0.15.0 (C15-07): a lent build's own style decides here and the player's LevelUpStyle says otherwise - the words say so.</summary>
        internal static bool LentStyleSays(BuildStyle style, StyleSource from) { return Builds.LentStyleSaid(style, from, Doctrine.Current.Style); }
        static string StyleName(BuildStyle st) { return st == BuildStyle.Weapon ? "weapon first" : st == BuildStyle.Ability ? "abilities first" : "balanced"; }

        // ---------------------------------------------------------------- weapons
        internal sealed class WStep
        {
            public WeaponUpgradePowerup W; public int Depth; public int Index; public int Level; public bool Available;
            public double Pref; public bool Recommended; public bool Alt; public bool GuidePick; public bool BuildPick; public string Why;
            public double Tree;        // the Training Yard part of Pref alone
            public string WhyType;     // the damage type Why is about ("shares Kinetic with Bow"), when the squad's damage decided
            public string WhyWith;     // ... and the rest of the squad's powerups dealing it ("Bow")
        }
        // what decided a fork when no damage type sets the branches apart
        static string ForkFallback(WStep x) { return x.GuidePick ? "the guides' branch" : x.Tree > 0 ? "your Training Yard investment" : null; }

        /// <summary>Where a weapon sits in its line: 0 = the starting weapon, 1 = its upgrade, 2 = one of the three tier-3
        /// weapons of the fork; -1 = no weapon. The Training Yard plan sorts its weapon nodes by this (TreeState).</summary>
        internal static int WeaponDepth(WeaponUpgradePowerup w) { if (w == null) return -1; try { return Depth(w); } catch { return -1; } }

        static int Depth(WeaponUpgradePowerup w)
        {
            int d = 0; PowerupBase cur = w;
            for (int guard = 0; guard < 8 && cur != null; guard++)
            {
                PowerupBase prev = null; try { prev = cur.previousLevelWeapon; } catch { }
                if (prev == null) break;
                d++; cur = prev;
            }
            // every weapon names the one before it: the fifth weapon of a line follows the tier-2 weapon just like the two
            // other tier-3 weapons (probe.json: previousWeapon, weaponTier Tier3) - a third branch of the fork, not a "final
            // weapon" (0.12.2, F05). Should a link ever be missing, the weapon's own tier says where it sits.
            Weapon.WeaponTier tier = Weapon.WeaponTier.Tier1; try { tier = w.weaponTier; } catch { }
            if (d == 0 && tier != Weapon.WeaponTier.Tier1) d = tier == Weapon.WeaponTier.Tier2 ? 1 : 2;
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
                    // the fork, the three tier-3 weapons: a branch already taken wins; else the build's branch; else what
                    // the rest of the squad deals, the Training Yard investment and the guides, among the branches the tree
                    // unlocked (the game only offers those: a locked pick must not demote the branch that can be offered)
                    var pool = g.Where(x => x.Level >= 1).ToList();
                    if (pool.Count == 0) pool = g.Where(x => x.Available).ToList();
                    if (pool.Count == 0) pool = g.ToList();
                    TagProfile others = null;
                    if (s != null && pool.Count > 1 && !pool.Any(x => x.BuildPick)) { try { others = G.SquadProfile(s, owner); } catch { } }
                    foreach (var x in pool)
                    {
                        if (x.BuildPick) { x.Pref += 10; x.Why = Builds.Your(owner.Name, build); continue; }
                        if (x.GuidePick) x.Pref += 0.9;
                        if (others != null)
                        {
                            // the reason names only a type that sets this branch apart from the others of the fork
                            var why = new List<string>(); var rivals = pool.Where(y => y != x).Select(y => G.Facts(y.W)).ToList();
                            double fit = Synergy.BranchFit(G.Facts(x.W), others, s.Boosts, s.Ctx, why, rivals);
                            x.Pref += fit; if (fit >= 0.3 && why.Count > 0) { x.Why = why[0]; x.WhyType = Synergy.BranchType(G.Facts(x.W), others, rivals); if (x.WhyType != null) x.WhyWith = others.SourceText(x.WhyType); }
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

        static double WeaponFloor(BuildStyle style) { return Synergy.WeaponFloor(style); }
        static bool _liftWarned;          // a failed LiftOf said once a session

        /// <summary>How far the survivor's own abilities are (0.13.0, C2: WeaponLift): owned abilities short of their last level
        /// (the build's skipped ones left out) and one of their names, and abilities the build ranks (no build: any) that the
        /// game can still offer - tree node bought, class rank open (Plan.RankClosed) - while a slot is free.</summary>
        internal static WeaponLift LiftOf(Survivor owner, Snapshot s, BuildStyle style)
        {
            if (style != BuildStyle.Ability) return new WeaponLift();
            var build = owner.Build;
            var owned = owner.Abilities();
            int unfinished = 0, missing = 0; string left = null;
            foreach (var kv in owned)
            {
                string n = G.Name(kv.Key);
                if (build != null && build.Skips(n)) continue;
                if (kv.Value < G.MaxLevel(kv.Key)) { unfinished++; left = n; }
            }
            int slots = 4 - owned.Count;
            if (slots > 0 && unfinished <= 1 && owner.Props != null)
                foreach (var a in G.Each(owner.Props.abilityBasePowerups))
                {
                    if (a == null || owner.LevelOf(a) >= 1) continue;
                    string n = G.Name(a);
                    if (build != null && (build.PriorityOf(n) < 0 || build.Skips(n))) continue;
                    SkillTreeUpgradeBase node = null; try { node = a.skillTreeAbilityBoost; } catch { }
                    if (node == null) { try { node = a.skillTreeRequirement; } catch { } }
                    if (node != null && !G.NodeOwned(node)) continue;
                    if (Plan.RankClosed(a, node, owner, s)) continue;
                    missing++;
                }
            return WeaponLift.Judge(style, unfinished, Math.Min(missing, Math.Max(0, slots)), left);
        }

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
            StyleSource styleFrom;
            var style = StyleOf(owner, out styleFrom);
            var facts = G.Facts(w);
            var synWhy = new List<string>();
            double syn = 0.4 * Synergy.TagValue(facts, s.Tags, s.Ctx, synWhy) + 0.4 * Synergy.BoostValue(facts, s.Boosts, s.Ctx, synWhy);

            if (lvl >= 1)
            {
                // a level of the weapon in hand. Its floor depends on the style; late in a timed run a weapon that can no
                // longer be finished falls back to the balanced floor (a level-1 bow with ninety seconds left is not a carry).
                // 0.13.0 (C2): "abilities first" lifts it to the balanced floor once the survivor's own abilities are as good as
                // done (Rank keeps it under a card of the survivor's own open build ability: UnderOwnAbility)
                // in its own try: a game member it reads failing costs the lift (the style's floor as before), not the weapon's card
                WeaponLift lift;
                try { lift = LiftOf(owner, s, style); }
                catch (Exception e) { lift = new WeaponLift(); if (!_liftWarned) { _liftWarned = true; Plugin.Logger.LogWarning("rank: weapon lift not judged (" + e.GetType().Name + " " + e.Message + ") - the style's floor"); } }
                // a lent build Auto follows (Lent, or Mine: the player's style standing in) - the same test as before StyleOf told the source
                double floor = lift.Floor(style, styleFrom == StyleSource.Lent || styleFrom == StyleSource.Mine, Doctrine.Current.Style);
                double reach = s.Ctx.Reach(Math.Max(1, max - lvl));
                c.Score = Synergy.HeldWeapon(floor, reach, lvl, max, syn);
                c.LiftedWeapon = lift.On;
                c.Say = new CardWords { Kind = SayKind.Weapon, Level = lvl, Max = max, Next = lvl == max - 1 && next != null ? G.Name(next.W) : null, Style = style,
                    Build = owner.Build != null ? owner.Build.Name : null, Lifted = lift.On, LiftLeft = lift.Left, Reach = reach, Clock = s.Ctx.ClockText,
                    LentStyle = LentStyleSays(style, styleFrom), MineStyle = styleFrom == StyleSource.Mine };
                Wording.Tags(c.Say, facts, s.Tags, s.Boosts, s.Ctx, null, 0.4);
                c.Why.Add((lvl == max - 1 ? "completes the weapon: " : "weapon level: ") + lvl + " to " + (lvl + 1) + " of " + max + (lvl == max - 1 && next != null ? ", then " + G.Name(next.W) : ""));
                c.Why.Add("style: " + StyleName(style) + (lift.On ? " - " + lift.Note : "") + (reach < 0.6 ? "; little time left to finish it (" + s.Ctx.ClockText + ")" : ""));
            }
            else if (me != null && me.Depth == 0) { c.Score = 7.2; c.Why.Add("first weapon for " + owner.Name + ": without it the recruit does nothing"); c.Say = new CardWords { Kind = SayKind.FirstWeapon }; }
            else if (next != null && me == next)
            {
                c.Score = 6.6 + syn;
                c.Why.Add("next weapon tier" + (me.Why != null ? ": " + me.Why : ""));
                // what decided the fork (WeaponPath): the build's branch, a damage type the rest of the squad deals, the guides, the yard
                c.Say = new CardWords { Kind = SayKind.NextTier, Build = owner.Build != null ? owner.Build.Name : null, BranchType = me.WhyType, BranchWith = Wording.FirstSource(me.WhyWith),
                    Branch = me.Why == null ? null : me.BuildPick ? "build" : me.WhyType != null ? "type" : me.GuidePick ? "guides" : me.Tree > 0 ? "yard" : null };
            }
            else if (me != null && me.Alt)
            {
                var pref = path.FirstOrDefault(x => x.Depth == me.Depth && x.Recommended);
                bool byBuild = pref != null && pref.BuildPick;
                // 0.13.0 (F02): only a build the PLAYER chose holds out for its branch; a lent build Auto follows is Auto
                bool chosen = byBuild && !Builds.OnAuto(owner.Name);
                bool prefOffered = pref != null && pref.Available;
                // "shares Kinetic with Bow" speaks for the favoured branch against a rocket launcher, not against this card
                // when it deals Kinetic too: there the tree investment or the guides decided (or nothing: card order)
                string prefWhy = pref == null || byBuild ? null : pref.WhyType != null && facts.Damage.Contains(pref.WhyType, StringComparer.OrdinalIgnoreCase) ? ForkFallback(pref) : pref.Why;
                // on Auto with a lent build: what Auto would follow once this branch is taken (Builds.AutoFor - asked directly, so
                // that a branch merely offered is not logged as followed)
                string then = null;
                if (byBuild && !chosen)
                {
                    var kit = Builds.KitOf(owner.Name); string mine = G.Name(w);
                    if (kit != null) foreach (var br in kit.Branches) if (SameName(br, mine)) { mine = br; break; }
                    var t = Builds.AutoFor(Builds.PacksOf(owner.Name), mine);
                    if (t != null && t != owner.Build) then = t.Name;
                }
                c.Score = Synergy.OtherBranch(pref == null ? null : G.Name(pref.W), prefWhy, prefOffered, syn, chosen, byBuild && !chosen ? owner.Build.Name : null, then, c.Why);
                c.Say = new CardWords { Kind = SayKind.OtherBranch, Next = pref == null ? null : G.Name(pref.W), NextOffered = prefOffered, Branch = byBuild ? "build" : "squad" };
            }
            else if (me == null) { c.Score = 2; c.Why.Add("weapon outside " + owner.Name + "'s line"); c.Say = new CardWords { Text = "not in " + owner.Name + "'s weapon line" }; }
            else { c.Score = 2; c.Why.Add("weapon tier-up"); c.Say = new CardWords { Text = "a weapon tier-up" }; }
            c.Why.AddRange(synWhy);
            if (paid > 0) c.Why.Add("Skill Tree " + (paid + 1) + "/" + G.NodeMax(node));
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
            /// <summary>0.14.0 (A1): the head in the words the card draws (Wording; longest form first), and its weight.</summary>
            public string[] HeadSay;
            public int HeadRank { get { return _headRank; } }
            public void Mark(int rank, string text, string from = null, string[] say = null) { if (rank > _headRank) { _headRank = rank; Head = text; HeadFrom = from; HeadSay = say; } }
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
                if (v.Skipped) { v.Score -= 2.0; v.Why.Add(Builds.Your(owner.Name, build) + " skips it"); v.Mark(6, build.Name + " skips it", v.Why[v.Why.Count - 1], Wording.Skips(build.Name)); }
                else if (v.Priority >= 0)
                {
                    double[] bonus = { 1.6, 1.0, 0.5, 0.1 };
                    v.Score += bonus[Math.Min(v.Priority, bonus.Length - 1)];
                    v.Why.Add("#" + (v.Priority + 1) + " in " + Builds.Your(owner.Name, build));
                    v.Mark(v.Priority <= 1 ? 4 : 1, "#" + (v.Priority + 1) + " in " + build.Name, v.Why[v.Why.Count - 1], Wording.Role(build.Name, v.Priority));
                }
            }
            else
            {
                string tier; if (K.AbilityTier.TryGetValue(name, out tier)) { v.Tier = tier; double b = Knowledge.Tier(tier, 1.2, 0.6, 0, -0.8); if (b != 0) { v.Score += b; v.Why.Add(tier + "-tier ability in the guides"); v.Mark(b > 0 ? 2 : 1, tier.ToUpperInvariant() + "-tier in the guides", v.Why[v.Why.Count - 1], Wording.Tier(tier)); } }
            }
            if (owner.Props != null)
            {
                foreach (var syn in G.Each(owner.Props.skillTreeSynergies))
                {
                    if (syn == null || !G.NodeOwned(syn)) continue;
                    PowerupBase req = null; try { req = syn.requiredPowerup; } catch { }
                    if (req == null || !G.Same(req, ability)) continue;
                    CT partner; try { partner = syn.synergiesWithClass; } catch { continue; }
                    if (s.OnSquad(partner)) { v.Score += 2.0; v.Why.Add("synergy with " + G.ClassName(partner) + " on the team"); v.Mark(3, "synergy: " + G.ClassName(partner), v.Why[v.Why.Count - 1], Wording.Synergy(G.ClassName(partner))); }
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
            if (paid > 0) { v.Score += 0.3 * paid; v.Why.Add("Skill Tree " + (paid + 1) + "/" + G.NodeMax(boost)); }

            var facts = G.Facts(ability);

            double tagValue = Synergy.TagValue(facts, s.Tags, s.Ctx, v.Why), boostValue = Synergy.BoostValue(facts, s.Boosts, s.Ctx, v.Why);
            v.Score += tagValue + boostValue;
            // "unlocks the Kinetic special", "Trap Expertise boosts it", "Kinetic: 79% of the squad" - each with the line it sums up
            // (0.13.0, C3: the last two were marked without it and the card said them twice)
            foreach (var h in Synergy.AbilityHeads(facts, s.Tags, s.Boosts, tagValue, boostValue, v.Why)) v.Mark(h.Rank, h.Text, h.From, h.Say);
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
        internal static PowerupBase TopAbility(Survivor owner, Snapshot s, PowerupBase missing) { double score; return TopAbility(owner, s, missing, out score); }

        /// <summary>... and the score its card would get (double.MinValue: none).</summary>
        internal static PowerupBase TopAbility(Survivor owner, Snapshot s, PowerupBase missing, out double bestScore)
        {
            var focus = FocusAbility(owner, s);
            var pool = owner.Abilities().Where(kv => kv.Value < G.MaxLevel(kv.Key)).Select(kv => kv.Key).ToList();
            if (missing != null) pool.Add(missing);
            PowerupBase best = null; bestScore = double.MinValue;
            foreach (var p in pool)
            {
                var c = new Card(); ScoreAbility(c, p, owner, s, focus, true);
                c.Kind = "ability"; c.Owner = owner; c.Powerup = p; c.Name = G.Name(p);
                var qv = QuestOf(c, s); if (qv != null && qv.Head) c.Score = qv.Score;        // 0.14.0 (C3): as the card will be scored under the quest
                double sc = Math.Round(c.Score, 2);
                if (sc > bestScore) { bestScore = sc; best = p; }      // a tie: the owned ability, listed first
            }
            return best;
        }

        /// <summary>The score the card of the next level of the held weapon <paramref name="w"/> would get now, and whether "abilities
        /// first" lifts it (WeaponLift): the PLAN readout orders a survivor's row by it (0.14.0, G7).</summary>
        internal static double WeaponLevelScore(Survivor owner, Snapshot s, WeaponUpgradePowerup w, out bool lifted)
        {
            var c = new Card(); ScoreWeapon(c, w, owner, s);
            c.Kind = "weapon"; c.Owner = owner; c.Powerup = w; c.Name = G.Name(w);
            lifted = c.LiftedWeapon;
            // 0.14.0 (C3): the quest's lift too - the readout's row must not say "later" for the weapon the quest wants maxed
            var v = QuestOf(c, s);
            if (v != null && v.Head && !v.Avoid && v.Score > c.Score) { c.Score = v.Score; lifted = true; }
            return Math.Round(c.Score, 2);
        }

        static double SoftCap(double x) { return x <= 4.6 ? x : 4.6 + (x - 4.6) * 0.3; }     // keeps the order among strong abilities, stays under 6

        static void ScoreAbility(Card c, PowerupBase p, Survivor owner, Snapshot s, PowerupBase focusKnown = null, bool haveFocus = false)
        {
            var a = AbilityScore(p, owner, s);
            int lvl = owner.LevelOf(p);
            int max = G.MaxLevel(p);
            var owned = owner.Abilities();
            c.OwnBuildOpen = lvl < max && !a.Skipped && (owner.Build == null || a.Priority >= 0);
            var ctx = s.Ctx;
            StyleSource styleFrom;
            var style = StyleOf(owner, out styleFrom);
            var say = c.Say = new CardWords { Kind = SayKind.Ability, Level = lvl, Max = max, Build = owner.Build != null ? owner.Build.Name : null, Priority = a.Priority,
                HeadRank = a.HeadSay != null ? a.HeadRank : 0, Head = a.HeadSay, EvoExists = a.EvoA != null || a.EvoB != null, EvoOwned = a.EvoOwned, Clock = ctx.ClockText, Owned = owned.Count,
                Style = style, LentStyle = LentStyleSays(style, styleFrom), MineStyle = styleFrom == StyleSource.Mine };      // 0.15.0 (C15-07): the WHY band says a lent build's style
            Wording.Tags(say, G.Facts(p), s.Tags, s.Boosts, ctx);      // 0.14.0: the tag facts for the WHY band (the card's own line reads the head)
            double score, once = 0;
            if (lvl == 0)
            {
                // "take each ability once": a new damage source early is worth more than another level of an old one -
                // while there is still time for it to grow
                double baseNew = owned.Count < 2 ? 3.6 : owned.Count < 4 ? 3.1 : 2.4;
                double reach = ctx.Reach(3);
                say.Reach = reach;
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
                once = Synergy.OnceEarly(style, owned.Count, a.Skipped, ctx.Reach(10));         // 0.15.0: pure, so the bench orders a hand by style (C15-07)
                if (once >= 0.3 && !c.Why.Any(w => w.Contains("once early"))) c.Why.Add("style: balanced - before another weapon level");     // "take each once early" already says it
            }
            else
            {
                var focusAbility = haveFocus ? focusKnown : FocusAbility(owner, s);
                bool focus = focusAbility != null && G.Same(focusAbility, p);
                bool completes = lvl == max - 1;
                score = 2.6 + (focus ? 1.0 : 0.2) + (completes ? 0.5 : 0) + a.Score * 0.5;
                say.Focus = focus;
                if (a.EvoOwned)
                {
                    double reach = ctx.Reach(max - lvl + 1);           // the levels left, plus the evolution card itself
                    say.Reach = reach;
                    score += 0.9 * reach;
                    c.Why.Add((focus ? "focus " : "") + lvl + " to " + (lvl + 1) + " of " + max + (reach >= 0.5 ? " toward its evolution" : ", evolution out of reach") + (a.Head != null ? " - " + a.Head : ""));
                    if (reach < 0.5) c.Why.Add("too few level-ups left to evolve it (" + ctx.ClockText + ")");
                }
                else c.Why.Add((focus ? "focus " : completes ? "completes it, " : "level ") + lvl + " to " + (lvl + 1) + " of " + max + (a.Head != null ? " - " + a.Head : ""));
            }
            c.Score = SoftCap(score);
            // the lift, squeezed above 5.6 so that two new abilities keep their order and none reaches a tier-up's 6.2
            c.Score = Synergy.WithOnce(c.Score, once);
            c.Why.AddRange(Synergy.ReasonsUnder(a.Head, a.HeadFrom, a.Why));      // the head already says the line it was cut from
        }

        // ---------------------------------------------------------------- evolutions
        static void ScoreEvolution(Card c, PowerupBase evo, PowerupBase baseAbility, Survivor owner, Snapshot s, List<Card> offer)
        {
            var parent = AbilityScore(baseAbility, owner, s);
            string baseName = G.Name(baseAbility), evoName = G.Name(evo);
            var build = owner.Build;
            string pick = build == null ? null : build.EvolutionOf(baseName);
            var fitWhy = new List<string>();
            double fit = Synergy.EvolutionFit(G.Facts(evo), G.Facts(baseAbility), s.Tags, s.Boosts, s.Ctx, fitWhy);
            c.Score = 7.6 + parent.Score * 0.1 + fit * 0.4;
            var say = c.Say = new CardWords { Kind = SayKind.Evolution, Base = baseName, Build = build != null ? build.Name : null, Pick = pick,
                BasePriority = build != null && !parent.Skipped ? parent.Priority : -1 };        // 0.15.0 (C15-03 c): "evolves the build's main ability"
            Wording.Tags(say, G.Facts(evo), s.Tags, s.Boosts, s.Ctx, G.Facts(baseAbility));
            if (pick != null)
            {
                bool mine = SameName(pick, evoName);
                say.Mine = mine; say.PickOffered = mine || offer == null || EvolutionOffered(offer, c, baseAbility, pick);
                c.Score += mine ? 1.0 : -0.5;
                // 0.12.2 (F12): with the build's pick not on the table this card still comes first (an evolution outranks
                // a tier-up) and says so, instead of "your build takes <the other one>" under a card marked PICK
                c.Why.Add(Synergy.EvolutionHead(baseName, evoName, pick, build.Name, mine, mine || offer == null || EvolutionOffered(offer, c, baseAbility, pick), Builds.OnAuto(owner.Name)));
            }
            else c.Why.Add("evolution of " + baseName + (fitWhy.Count > 0 ? ": " + fitWhy[0] : ""));
            c.Why.AddRange(pick != null ? fitWhy : fitWhy.Skip(1));
        }

        // the named evolution of this base ability is on another card of the same offer
        static bool EvolutionOffered(List<Card> offer, Card self, PowerupBase baseAbility, string evoName)
        {
            foreach (var o in offer)
            {
                if (o == self || o.Powerup == null) continue;
                PowerupBase ob = null; try { ob = o.Powerup.evolutionBaseAbility; } catch { }
                if (ob != null && G.Same(ob, baseAbility) && SameName(G.Name(o.Powerup), evoName)) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- items (chests)
        static void ScoreItem(Card c, Snapshot s)
        {
            c.Kind = "item"; c.Name = G.Name(c.Item);
            var say = new ItemSay();
            c.Score = ItemScore(c.Item, s, c.Why, null, say);
            c.Say = new CardWords { Kind = SayKind.Item, Item = say };
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
                if (b != null) { string whose = Builds.Your(sv.Name, b); foreach (var want in b.Wants) c.Wants.Add(new KeyValuePair<string, string>(whose, want)); }
            }
            if (weapons > 0) { c.CloseShare = close / (double)weapons; c.LongShare = far / (double)weapons; c.ClipShare = clips / (double)weapons; }
            if (weaponLevels + abilityLevels > 0) c.AbilityLean = abilityLevels / (weaponLevels + abilityLevels);
            return c;
        }

        /// <summary>The pure rules (shared with the offline bench), then the game-only parts: quest target, already held.
        /// Shared by the chest cards and the plan panel; pass the context when scoring many items against one snapshot.
        /// <paramref name="say"/> (a chest card's): what the rules reasoned, for the line under the card (0.14.0, A1).</summary>
        internal static double ItemScore(ItemBase it, Snapshot s, List<string> why, ItemContext shared = null, ItemSay say = null)
        {
            var facts = G.FactsOf(it);      // name, text, statistics: asset data, read once per item
            var ic = shared ?? ItemContextOf(s);
            ic.Stats = facts.Stats; ic.Healing = facts.Healing; ic.Say = say;
            double score = ItemRules.Evaluate(facts.Name, facts.Desc, ic, why);
            ic.Say = null;
            var steer = Quest.Steer();          // 0.14.0 (C3): [Advice] QuestSteer - Off ignores the quest's item, InfoOnly only says it
            try
            {
                var qm = steer == QuestSteer.Off ? null : s.ActiveQuests;
                if (qm != null && qm.IsActiveQuestTargetItem(it))
                {
                    if (steer == QuestSteer.On) { score += 3; why.Insert(0, "quest target"); if (say != null) say.Quest = true; }
                    else why.Add("quest target (info only)");
                }
            }
            catch { }
            if (s.AnyoneHas(it))
            {
                int max = facts.MaxCarry;
                if (max <= 1) { score -= 1.0; why.Insert(0, "already held"); if (say != null) say.Held = true; }
                else why.Add("stacks, already held");
            }
            // 0.14.0 (C3): the quest's other rules on items (health items +2.0 while too few are held, every item AVOID under "no
            // items", armor ...): the chest cards and the readout's GRAB row alike
            try { score = QuestItem(it, facts, s, score, why); }
            catch (Exception e) { if (!_questWarned) { _questWarned = true; Plugin.Logger.LogWarning("[rank] the quest's item rules not applied (" + e.GetType().Name + " " + e.Message + ") - said once a session"); } }
            if (why.Count == 0) why.Add("no squad-specific value");
            if (say != null) { say.Clock = s.Ctx.ClockText; say.QuestLine = QuestHead(why); }
            return score;
        }

        // a quest rule's line that went first and decides the card ("quest: stay solo - ..."), for the card's words; null: none
        static string QuestHead(List<string> why) { return why.Count > 0 && why[0].StartsWith("quest: ", StringComparison.Ordinal) ? why[0] : null; }

        // ---------------------------------------------------------------- SOS: survivors and Liberate
        static void ScoreSos(Card c, Snapshot s)
        {
            var p = c.Powerup; string asset = G.Asset(p);
            if (asset.StartsWith("Loot Character", StringComparison.OrdinalIgnoreCase) || asset.IndexOf("SendToBase", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                c.Kind = "liberate"; c.Name = "Liberate";
                double left = s.Ctx.RecruitValue;
                int farming = Doctrine.Current.Farming;
                var lib = new RecruitSay { Liberate = true, Full = s.SquadFull };
                if (s.SquadFull) { c.Score = 5; c.Why.Add("squad is full: take the level-up and cash"); }
                else
                {
                    c.Score = 1.0 + 3.2 * (1 - left) + 0.4 * farming;
                    c.Why.Add(left < 0.45 ? "a recruit no longer has time to grow (" + s.Ctx.ClockText + "): take the level-up and cash" : "level-up and cash instead of a recruit");
                    if (left < 0.45) lib.Late = s.Ctx.ClockText;
                }
                var lq = s.Quest; if (lq != null) c.Score = lq.Card(null, c.Score, c.Why);       // 0.13.0 (C1): the active quest's team rule
                lib.Quest = QuestHead(c.Why);
                c.Say = new CardWords { Kind = SayKind.Recruit, Recruit = lib };
                return;
            }
            c.Kind = "survivor";
            ClassProperties props = null; try { props = p.targetClassProperties; } catch { }
            if (props == null) { c.Score = 0.5; c.Why.Add("unknown survivor"); c.Say = new CardWords { Text = "a survivor the Companion cannot read" }; return; }
            CT cls = props.characterType; c.Name = G.ClassName(cls);
            if (s.OnSquad(cls)) { c.Score = 0; c.Why.Add("already on the squad"); c.Say = new CardWords { Text = "already on the squad" }; return; }
            c.Recruit = new Recruit();
            var say = new RecruitSay();
            c.Score = RecruitScore(cls, props, s, c.Why, c.Recruit, say);
            var q = s.Quest; if (q != null) c.Score = q.Card(c.Name, c.Score, c.Why);      // 0.13.0 (C1): the active quest's team rule
            say.Quest = QuestHead(c.Why);
            c.Say = new CardWords { Kind = SayKind.Recruit, Recruit = say };
            c.Recruit.Score = c.Score;
        }

        /// <summary>What a recruit brings to THIS squad: bought synergy nodes (an unbought node does nothing in a run), damage
        /// types shared with the squad, team passives either way, the guides' rescue tier, how trained the recruit is - all
        /// scaled by the time left for them to grow. Shared by the SOS cards and the plan panel; <paramref name="tie"/>, when
        /// given, gets what settles a tie between two recruits (Recruit.Ties); <paramref name="say"/> (a rescue card's), what speaks
        /// for the recruit, for the line under the card (0.14.0, A1).</summary>
        internal static double RecruitScore(CT cls, ClassProperties props, Snapshot s, List<string> why, Recruit tie = null, RecruitSay say = null)
        {
            string name = G.ClassName(cls);
            double fixedPart = 2.0, fit = 0;         // a third gun and +20 % XP for the rest of the run: never worth less than this early
            string tier; K.RescueTier.TryGetValue(name, out tier);
            if (tier != null) { fit += Knowledge.Tier(tier, 1.6, 1.1, 0.6, 0.1); why.Add(Recruit.TierLine(tier)); }
            if (say != null) say.Tier = tier;

            int owned = 0, unowned = 0; var partners = new List<KeyValuePair<string, int>>();
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
                if (o > 0) partners.Add(new KeyValuePair<string, int>(sv.Name, o));
            }
            fit += 1.1 * owned;
            if (tie != null) { tie.Name = name; tie.Class = (int)cls; tie.Tier = tier ?? ""; tie.Bought = owned; }
            if (owned > 0) why.Add(Recruit.BoughtLine(owned, partners));
            else if (unowned > 0) why.Add(unowned + " synergy node" + (unowned > 1 ? "s" : "") + " with this squad, none bought yet");
            if (say != null) { say.Bought = owned; say.Unbought = unowned; foreach (var pt in partners) say.Partners.Add(pt.Key); }

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
                if (share >= 0.2)
                {
                    fit += 1.2 * share * Doctrine.Current.SynergyWeight; why.Add("deals " + shared + " like " + (s.Tags.SourceText(shared).Length > 0 ? s.Tags.SourceText(shared) : "the squad"));
                    if (say != null) { say.SharedType = shared; say.SharedWith = Wording.FirstSource(s.Tags.SourceText(shared)); }
                }
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
                    if (covered > 0)
                    {
                        fit += Math.Min(0.9, 0.3 * covered); string nn = ""; try { nn = n.GetName(); } catch { } why.Add((nn.Length > 0 ? nn : tag + " passive") + " would boost " + covered + " of your powerups");
                        if (say != null && say.Boosts == null) { say.Boosts = nn.Length > 0 ? nn : tag + " passive"; say.Covered = covered; }
                    }
                    continue;
                }
                string d = G.NodeDesc(n);
                if (d.IndexOf("on the team", StringComparison.OrdinalIgnoreCase) >= 0) { fit += 0.5; string bonus; why.Add(TeamPassive(n, d, out bonus)); if (say != null && say.TeamBonus == null) say.TeamBonus = bonus; }
            }

            int lvl = props != null ? G.TreeLevel(props) : 0;
            fit += Math.Min(1.0, lvl / 120.0) * 0.6;                 // a trained recruit arrives with more unlocked
            if (Doctrine.Current.Farming >= 1)
            {
                int nextThr = -1; foreach (var thr in RankLevels) if (thr > lvl) { nextThr = thr; break; }
                if (nextThr > 0 && nextThr - lvl <= 5)
                {
                    fit += 0.3; why.Add((nextThr - lvl) + " level" + (nextThr - lvl > 1 ? "s" : "") + " from rank " + (Array.IndexOf(RankLevels, nextThr) + 1));
                    if (say != null) { say.RankLevels = nextThr - lvl; say.Rank = Array.IndexOf(RankLevels, nextThr) + 1; }
                }
            }
            double left = s.Ctx.RecruitValue;
            if (say != null && left < 0.45) say.Late = s.Ctx.ClockText;
            // the headline the card shows; it takes the tier and bought lines it summarises along (0.13.0, F12: each said once)
            Recruit.Headline(why, tier, owned, partners, left < 0.45 ? "little time left for a recruit to grow (" + s.Ctx.ClockText + ")" : null);
            if (why.Count == 0) why.Add("L" + lvl + ", no synergy with this squad");
            return fixedPart * left + fit * (0.35 + 0.65 * left);
        }

        /// <summary>Every survivor the squad could still be joined by - unlocked in the profile, not on the squad, not
        /// <paramref name="skip"/>ped - scored by the SOS cards' rules, in the game's class order; each Recruit carries the
        /// game's class name. The readout's SOS row and the reroll hint on the rescue screen (0.12.2) both ask here.</summary>
        internal static List<Recruit> Recruitable(Snapshot s, Func<CT, bool> skip = null)
        {
            var list = new List<Recruit>();
            foreach (CT cls in Enum.GetValues(typeof(CT)))
            {
                if (cls == CT.None || cls == CT.NumCharacters || s.OnSquad(cls)) continue;
                if (skip != null && skip(cls)) continue;
                var props = G.PropsOf(cls);
                if (props == null || !G.Unlocked(props)) continue;
                var r = new Recruit();
                r.Score = RecruitScore(cls, props, s, new List<string>(), r);
                list.Add(r);
            }
            return list;
        }

        // ---------------------------------------------------------------- military training and Endless stat cards
        static void ScoreMilitary(Card c, Snapshot s)
        {
            var p = c.Powerup; c.Kind = "stat";
            var b = p.TryCast<BasicLevelPowerup>();
            string rarity = "Common"; try { if (b != null) rarity = b.GetRarity().ToString(); } catch { }
            // the cards' own numbers go about 1 : 2 : 3-4 by rarity (Luck 5 / 10 / 20, Ability Area 10 / 20 / 30): a Rare is
            // twice the Common of its stat, a Legendary three times - at 1.6 / 2.3 a Legendary of a modest stat lost to a
            // Common of a good one, and both times the player overrode the mod that was why. An Endless card is the game's
            // filler, a quarter to a half of the Common of its stat (Ability Area +2.5 % against +10 %): 0.14.0 (B1) weighs it
            // by its own value against that Common's (Synergy.EndlessWeight) - up to 0.13.0 a flat 2.6, "just under
            // Legendary", put it over the build's own abilities and the player overrode it on 8 of 13 mixed offers.
            string asset = G.Asset(p); string stat = asset.StartsWith("MilitaryTraining_") ? asset.Substring("MilitaryTraining_".Length) : asset;
            double r = Synergy.RarityWeight(rarity, rarity == "Endless" ? EndlessRatio(b, stat) : double.NaN);
            double w = 0.5; bool known = false;
            foreach (var kv in K.MilitaryStat) if (string.Equals(stat, kv.Key, StringComparison.OrdinalIgnoreCase)) { w = kv.Value; known = true; }
            if (!known) foreach (var kv in K.MilitaryStat) if (stat.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0) w = Math.Max(w, kv.Value);
            string target = ""; try { if (b != null) target = b.targetType.ToString(); } catch { }
            bool team = target.IndexOf("Team", StringComparison.OrdinalIgnoreCase) >= 0;

            var ctx = s.Ctx; string note = null;
            string[] sayNote = null; string wantBuild = null;       // 0.14.0 (A1): the same note in the card's words, the build that wants it
            var ic = ItemContextOf(s);
            if (stat.StartsWith("Weapon", StringComparison.OrdinalIgnoreCase)) { double k = 0.7 + 0.6 * (1 - ic.AbilityLean); w *= k; if (k >= 1.1) { note = "the squad's damage is mostly weapons"; sayNote = Wording.StatNote("weapons", null); } }
            else if (stat.StartsWith("Ability", StringComparison.OrdinalIgnoreCase)) { double k = 0.7 + 0.6 * ic.AbilityLean; w *= k; if (k >= 1.1) { note = "the squad's damage is mostly abilities"; sayNote = Wording.StatNote("abilities", null); } }
            if (stat.IndexOf("Crit", StringComparison.OrdinalIgnoreCase) >= 0 && ic.CritSquad) { w *= 1.15; }
            if (stat.StartsWith("XP", StringComparison.OrdinalIgnoreCase) || stat == "Luck" || stat == "MagnetRange")
            {
                w *= ctx.Economy;
                note = ctx.Economy >= 1.25 ? "pays back all run (" + ctx.ClockText + ")" : ctx.Economy <= 0.5 ? "too late to pay back (" + ctx.ClockText + ")" : note;
                sayNote = ctx.Economy >= 1.25 ? Wording.StatNote("early", ctx.ClockText) : ctx.Economy <= 0.5 ? Wording.StatNote("late", ctx.ClockText) : sayNote;
            }
            else if (stat == "MaxHealth" || stat == "Armor" || stat == "HPRegen")
            {
                w *= Math.Max(0, ctx.Survival);
                note = ctx.Survival >= 1.3 ? "survival matters now" + (ctx.Health < 0.5 ? " (the squad is hurting)" : "") : note;
                sayNote = ctx.Survival >= 1.3 ? Wording.StatNote(ctx.Health < 0.5 ? "hurting" : "now", null) : sayNote;
            }
            else if (stat == "DodgeChance" || stat.StartsWith("MovementSpeed", StringComparison.OrdinalIgnoreCase)) w *= Math.Max(ctx.Control, Math.Min(1.3, ctx.Survival));
            foreach (var sv in s.Squad)
            {
                var build = sv.Build; if (build == null) continue;
                bool wants = (stat.StartsWith("Weapon", StringComparison.OrdinalIgnoreCase) && build.Wants.Contains("weapons", StringComparer.OrdinalIgnoreCase))
                    || (stat.StartsWith("Ability", StringComparison.OrdinalIgnoreCase) && build.Wants.Contains("abilities", StringComparer.OrdinalIgnoreCase))
                    || (stat.IndexOf("Crit", StringComparison.OrdinalIgnoreCase) >= 0 && build.Wants.Contains("critical", StringComparer.OrdinalIgnoreCase))
                    || ((stat == "Armor" || stat == "MaxHealth" || stat == "HPRegen") && (build.Wants.Contains("armor", StringComparer.OrdinalIgnoreCase) || build.Wants.Contains("healing", StringComparer.OrdinalIgnoreCase)));
                if (wants) { w *= 1.2; note = Builds.Your(sv.Name, build) + " wants it"; wantBuild = build.Name; break; }
            }
            c.Score = Synergy.StatCard(r, w, team);
            c.Why.Add(rarity + (team ? ", team-wide" : "") + ": " + Humanize(stat) + (note != null ? " - " + note : ""));
            // the card already shows its rarity and its stat: the line says why (the game's own label for the stat, not the asset's)
            c.Say = new CardWords { Kind = SayKind.Stat, Stat = Knowledge.StatLabel(stat) ?? Humanize(stat).ToLowerInvariant(), Team = team, StatWhy = sayNote, WantBuild = wantBuild };
            c.Rarity = rarity; c.StatKey = stat;
        }

        // 0.14.0 (B1): an Endless card's own value against the Common card of its stat, |bonusesEndless[0]| / |bonuses[0]| (NaN:
        // not readable - Synergy.EndlessWeight falls back). The lists are read in accessors of their own: a field a game patch
        // took away fails its accessor alone (caught here), not the whole stat card. Said once a session per stat.
        static readonly HashSet<string> _endlessSaid = new HashSet<string>();
        [MethodImpl(MethodImplOptions.NoInlining)] static BonusList CommonBonuses(BasicLevelPowerup b) { return b.bonuses; }
        [MethodImpl(MethodImplOptions.NoInlining)] static BonusList EndlessBonuses(BasicLevelPowerup b) { return b.bonusesEndless; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float BonusValue(PowerupBase.StatisticBonus bo) { return bo.value; }
        static float FirstValue(BonusList list) { foreach (var bo in G.Each(list)) return bo != null ? BonusValue(bo) : float.NaN; return float.NaN; }

        static double EndlessRatio(BasicLevelPowerup b, string stat)
        {
            float own = float.NaN, common = float.NaN; string fail = null;
            try { if (b != null) { own = FirstValue(EndlessBonuses(b)); common = FirstValue(CommonBonuses(b)); } }
            catch (Exception e) { fail = e.GetType().Name; }
            double ratio = !float.IsNaN(own) && !float.IsNaN(common) && common != 0 ? Math.Abs(own) / Math.Abs(common) : double.NaN;
            if (_endlessSaid.Add(stat ?? ""))
                Plugin.Logger.LogInfo("[rank] Endless stat cards: weight x" + Synergy.EndlessWeight(ratio).ToString("0.00", CultureInfo.InvariantCulture) + " for " + Humanize(stat ?? "?")
                    + (double.IsNaN(ratio) ? " (the card's values not read" + (fail != null ? ": " + fail : "") + " - the fallback)"
                        : " (the card's own +" + Math.Abs(own).ToString("0.###", CultureInfo.InvariantCulture) + " vs Common +" + Math.Abs(common).ToString("0.###", CultureInfo.InvariantCulture) + ")"));
            return ratio;
        }

        // ---------------------------------------------------------------- Research Pod rewards: damage type tag points
        static void ScoreHashtag(Card c, Snapshot s)
        {
            c.Kind = "tags";
            string type = null; int n = 1;
            try { type = G.TagName(c.Hashtag.hashtagType); } catch { }
            try { n = c.Hashtag.numUpgrades; } catch { }
            if (type == null) { c.Name = "#?"; c.Score = 1; c.Why.Add("unknown tag type"); c.Say = new CardWords { Text = "an unknown damage type" }; return; }
            c.Name = "#" + type + " +" + n;
            c.Score = Tags.Score(type, n, s.Tags, c.Why);
            c.Say = new CardWords { Kind = SayKind.Tags, Type = type, Points = n, Tags = s.Tags };
        }

        // ---------------------------------------------------------------- the active quest's other objectives (0.14.0, C3)
        static bool _questWarned;        // the quest's rules failed on a card: said once a session

        /// <summary>What the quest's rules do to one scored card (null: nothing, or no quest followed).</summary>
        static QuestVerdict QuestOf(Card c, Snapshot s)
        {
            var rules = s == null ? null : s.Rules;
            if (rules == null || !rules.Follows) return null;
            var qc = QuestCardOf(c, s, rules);
            return qc == null ? null : rules.Judge(qc, c.Score);
        }

        /// <summary>The quest's rules over an offer, once every card is scored (items have them already: ItemScore). A line that
        /// decides the card goes first and the card says it; a modest lift stays under the build's core cards that stood above it
        /// (QuestRules.UnderCore).</summary>
        static void QuestPass(List<Card> cards, Snapshot s)
        {
            var rules = s == null ? null : s.Rules;
            if (rules == null || !rules.Follows) return;
            var before = new Dictionary<Card, double>();
            List<Card> modest = null;
            foreach (var c in cards)
            {
                before[c] = c.Score;
                if (c.Item != null) continue;
                var qc = QuestCardOf(c, s, rules);
                if (qc == null) continue;
                var v = rules.Judge(qc, c.Score);
                if (v == null) continue;
                c.Score = Math.Round(v.Score, 2);
                if (v.Head) { c.Why.Insert(0, v.Line); if (c.Say == null) c.Say = new CardWords(); c.Say.Quest = v.Line; }
                else c.Why.Add(v.Line);
                if (v.Modest && c.Score > before[c]) (modest ?? (modest = new List<Card>())).Add(c);
            }
            if (modest == null) return;
            var cores = cards.Where(o => o.OwnBuildOpen && o.Say != null && o.Say.Priority >= 0 && o.Say.Priority <= 2).ToList();
            foreach (var c in modest)
                c.Score = Math.Round(QuestRules.UnderCore(before[c], c.Score, cores.Where(o => o != c).Select(o => new KeyValuePair<double, double>(before[o], o.Score))), 2);
        }

        // an item under the quest's rules (ItemScore: the chest cards and the GRAB row)
        static double QuestItem(ItemBase it, G.ItemFacts facts, Snapshot s, double score, List<string> why)
        {
            var rules = s.Rules;
            if (rules == null || !rules.Follows) return score;
            var qc = new QuestCard { Kind = QuestCardKind.Item, Name = facts.Name };
            if (rules.Any(QuestAsk.HealthItems) || rules.Any(QuestAsk.FullHealth)) { var h = Quest.IsHealthItem(it); qc.Health = h ?? facts.Healing; }
            if (rules.Any(QuestAsk.Armor) || rules.Any(QuestAsk.FullHealth)) qc.Armor = facts.Stats.Any(x => x.IndexOf("Armor", StringComparison.OrdinalIgnoreCase) >= 0);
            var v = rules.Judge(qc, score);
            if (v == null) return score;
            if (v.Head) why.Insert(0, v.Line); else why.Add(v.Line);
            return v.Score;
        }

        /// <summary>What the quest's rules need to know of a scored card - its kind, owner, level, the weapon's place in its line,
        /// what it deals, the build's word on it; null: a card the quest has nothing to say on.</summary>
        static QuestCard QuestCardOf(Card c, Snapshot s, QuestRules rules)
        {
            var w = c.Say;
            var qc = new QuestCard { Name = c.Name, Class = c.Owner != null ? c.Owner.Name : null };
            switch (c.Kind)
            {
                case "weapon":
                    {
                        var kind = w == null ? SayKind.Other : w.Kind;
                        qc.Kind = kind == SayKind.Weapon ? QuestCardKind.Weapon : kind == SayKind.NextTier ? QuestCardKind.NextTier : kind == SayKind.OtherBranch ? QuestCardKind.Branch
                            : kind == SayKind.FirstWeapon ? QuestCardKind.FirstWeapon : QuestCardKind.Other;
                        WeaponUpgradePowerup wu = null; try { if (c.Powerup != null) wu = c.Powerup.TryCast<WeaponUpgradePowerup>(); } catch { }
                        qc.Depth = WeaponDepth(wu);
                        if (w != null && kind == SayKind.Weapon) { qc.Level = w.Level; qc.Max = w.Max; }
                        break;
                    }
                case "ability":
                    {
                        qc.Kind = QuestCardKind.Ability;
                        if (w != null) { qc.Level = w.Level; qc.Max = w.Max; qc.Evolvable = w.EvoOwned; qc.Core = c.OwnBuildOpen && w.Priority >= 0 && w.Priority <= 2; }
                        var b = c.Owner == null ? null : c.Owner.Build;
                        qc.Skipped = b != null && b.Skips(c.Name);
                        if (rules.Any(QuestAsk.Synergies) && c.Owner != null && c.Powerup != null) SynergyPartners(c.Powerup, c.Owner, s, qc.Synergy);
                        break;
                    }
                case "evolution": qc.Kind = QuestCardKind.Evolution; qc.Base = w != null ? w.Base : null; break;
                case "tags":
                    qc.Kind = QuestCardKind.Tags;
                    if (w != null && w.Type != null) { qc.Types.Add(w.Type); qc.Points = w.Points; }
                    return qc;
                case "stat":
                    {
                        qc.Kind = QuestCardKind.Stat; qc.Rarity = c.Rarity;
                        string k = c.StatKey ?? "";
                        qc.Armor = k.IndexOf("Armor", StringComparison.OrdinalIgnoreCase) >= 0;
                        qc.Health = k.IndexOf("Health", StringComparison.OrdinalIgnoreCase) >= 0 || k.IndexOf("HPRegen", StringComparison.OrdinalIgnoreCase) >= 0;
                        return qc;
                    }
                case "survivor":
                    {
                        qc.Kind = QuestCardKind.Recruit; qc.Class = c.Name;
                        var rs = w == null ? null : w.Recruit;
                        if (rs != null && rs.Bought > 0) qc.Synergy.AddRange(rs.Partners);
                        return qc;
                    }
                case "liberate": qc.Kind = QuestCardKind.Liberate; return qc;
                default: return null;
            }
            if (c.Powerup != null) { var f = G.Facts(c.Powerup); qc.Types.AddRange(f.Damage); if (qc.Kind == QuestCardKind.Ability) qc.Health = f.Healing; }
            return qc;
        }

        // the classes an ability has an unlocked synergy with on this squad (its node bought, the partner on the team)
        static void SynergyPartners(PowerupBase ability, Survivor owner, Snapshot s, List<string> into)
        {
            if (owner.Props == null) return;
            foreach (var syn in G.Each(owner.Props.skillTreeSynergies))
            {
                if (syn == null || !G.NodeOwned(syn)) continue;
                PowerupBase req = null; try { req = syn.requiredPowerup; } catch { }
                if (req == null || !G.Same(req, ability)) continue;
                CT partner; try { partner = syn.synergiesWithClass; } catch { continue; }
                if (s.OnSquad(partner)) into.Add(G.ClassName(partner));
            }
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
        // 0.12.2 (F12): a recruit's own team passive in a few words - "team passive: +Armor", from the statistic the node
        // raises - where the game's sentence cut at 41 characters ("While Tank is on the team, Armor is incre...") said
        // nothing. The node is asset data: its phrase is kept per node for the session.
        // 0.14.0 (A1): <paramref name="bonus"/> = the stat in the game's own words for the card ("armor"; Knowledge.StatLabel), kept beside it
        static readonly Dictionary<IntPtr, string> _teamPassive = new Dictionary<IntPtr, string>(), _teamBonus = new Dictionary<IntPtr, string>();
        static readonly System.Text.RegularExpressions.Regex TeamStat = new System.Text.RegularExpressions.Regex(@"on the team, (?:all )?(.+?) (?:is|are) increased", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static string TeamPassive(SkillTreeUpgradeBase n, string desc, out string bonus)
        {
            IntPtr key = IntPtr.Zero; try { key = n.Pointer; } catch { }
            string text;
            if (key != IntPtr.Zero && _teamPassive.TryGetValue(key, out text)) { _teamBonus.TryGetValue(key, out bonus); return text; }
            string stat = null; bonus = null;
            try
            {
                var vb = n.TryCast<SkillTreeUpgradeValueBase>();
                if (vb != null) foreach (var b in G.Each(vb.statisticBonuses)) { var st = b == null ? null : b.targetStatistic; if (st != null) { string raw = st.statisticType.ToString(); stat = Humanize(raw); bonus = Knowledge.StatLabel(raw); break; } }
            }
            catch { }
            if (string.IsNullOrEmpty(stat)) { var m = TeamStat.Match(ItemRules.RichTag.Replace(desc ?? "", "")); if (m.Success) stat = m.Groups[1].Value.Trim(); }
            if (bonus == null && !string.IsNullOrEmpty(stat)) bonus = (stat.StartsWith("Team ", StringComparison.Ordinal) ? stat.Substring(5) : stat).ToLowerInvariant();
            text = string.IsNullOrEmpty(stat) ? "team passive" : "team passive: +" + stat;
            if (key != IntPtr.Zero) { _teamPassive[key] = text; _teamBonus[key] = bonus; }
            return text;
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
