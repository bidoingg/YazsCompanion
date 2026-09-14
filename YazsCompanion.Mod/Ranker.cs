// The ranking brain. Rules come from published guides (see Knowledge.cs and mod/README.md), the
// state comes live from the game. Nothing here assumes the player's past picks were good.
//
//  Level-ups:  finish the weapon line first (every weapon level beats every ability level),
//              a fresh recruit's first weapon on top; evolutions always; take each ability once,
//              then focus the ability that is furthest along; guide tiers nudge between abilities;
//              Training Yard synergies and unlocked evolutions add on top.
//  Chests:     guide item tier first, then fit with the squad's damage types, quest targets, stacking.
//  SOS:        guide rescue tier + synergy potential with the squad (all synergy nodes between the
//              two survivors, owned ones counting more) + owned team passives; Liberate when full.
//  Military:   rarity + stat weights from the knowledge file.
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
        const int TreeMaxLevel = 224;
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

            if (evoBase != null)
            {
                // an evolution card only appears when the ability is maxed and the tree unlocked it: always take it
                c.Kind = "evolution";
                var parent = AbilityScore(evoBase, owner, s);
                c.Score = 7.5 + parent.Score * 0.2;
                c.Why.Add("evolution of " + G.Name(evoBase));
                c.Why.AddRange(parent.Why);
            }
            else if (w != null) { c.Kind = "weapon"; ScoreWeapon(c, w, owner, s); }
            else if (isAbility) { c.Kind = "ability"; ScoreAbility(c, p, owner, s); }
            else { c.Kind = "other"; c.Score = 1; c.Why.Add("powerup outside the tree"); }
        }

        // ---------------------------------------------------------------- weapons: finish the gun
        internal sealed class WStep
        {
            public WeaponUpgradePowerup W; public int Depth; public int Index; public int Level; public bool Available;
            public double Pref; public bool Recommended; public bool Alt; public bool GuidePick;
        }

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
            string guideBranch; K.WeaponBranch.TryGetValue(owner.Name, out guideBranch);
            foreach (var p in G.Each(owner.Props.weaponPowerups))
            {
                var w = p == null ? null : p.TryCast<WeaponUpgradePowerup>();
                if (w == null) continue;
                SkillTreeUpgradeBase node = null; try { node = w.skillTreeRequirement; } catch { }
                int idx = 0; try { idx = w.weaponIndex; } catch { }
                var st = new WStep { W = w, Depth = Depth(w), Index = idx, Level = owner.LevelOf(w) };
                st.Available = st.Level >= 1 || node == null || G.NodeOwned(node);
                st.GuidePick = guideBranch != null && string.Equals(G.Name(w), guideBranch, StringComparison.OrdinalIgnoreCase);
                st.Pref = (st.GuidePick ? 2.0 : 0) + 0.5 * Math.Max(0, G.NodeLevel(node) - 1);
                path.Add(st);
            }
            foreach (var g in path.GroupBy(x => x.Depth))
            {
                if (g.Key == 2)
                {
                    // the fork: a branch already taken wins, else the guides' branch among the ones the Training Yard
                    // unlocked (the game only offers unlocked ones; a locked guide pick must not demote the offered branch)
                    var pool = g.Where(x => x.Level >= 1).ToList();
                    if (pool.Count == 0) pool = g.Where(x => x.Available).ToList();
                    if (pool.Count == 0) pool = g.ToList();
                    var best = pool.OrderByDescending(x => x.Pref).ThenBy(x => x.Index).First();
                    foreach (var x in g) { x.Recommended = x == best; x.Alt = x != best; }
                }
                else foreach (var x in g) x.Recommended = true;
            }
            return path.OrderBy(x => x.Depth).ThenBy(x => x.Index).ToList();
        }

        static void ScoreWeapon(Card c, WeaponUpgradePowerup w, Survivor owner, Snapshot s)
        {
            var path = WeaponPath(owner);
            var me = path.FirstOrDefault(x => G.Same(x.W, w));
            var current = path.Where(x => x.Level >= 1).OrderByDescending(x => x.Depth).FirstOrDefault();
            if (owner.Weapon != null && owner.LevelOf(owner.Weapon) >= 1) current = path.FirstOrDefault(x => G.Same(x.W, owner.Weapon)) ?? current;
            var next = path.FirstOrDefault(x => x.Recommended && x.Available && x.Level < 1 && x.Depth > 0 && (current == null || x.Depth > current.Depth));
            int lvl = owner.LevelOf(w);
            int max = 0; try { max = w.MaxLevel; } catch { }
            SkillTreeUpgradeBase node = null; try { node = w.skillTreeRequirement; } catch { }
            int paid = Math.Max(0, G.NodeLevel(node) - 1);

            // score bands: weapons 6+, evolutions 7.5+, abilities capped at 5.5 -> a weapon level always wins
            if (lvl >= 1)
            {
                c.Score = 6.0 + 0.1 * lvl;
                c.Why.Add("finish the weapon: " + lvl + " to " + (lvl + 1) + (max > 0 ? " of " + max : ""));
                c.Why.Add("guides: max the weapon before abilities");
            }
            else if (me != null && me.Depth == 0) { c.Score = 6.6; c.Why.Add("first weapon for " + owner.Name); }
            else if (next != null && me == next)
            {
                c.Score = 6.4 + (s.Seconds < 300 ? 0.2 : 0);
                c.Why.Add(me.GuidePick ? "next weapon step, the guides' branch" : "next step of the weapon line");
            }
            else if (me != null && me.Depth >= 3) { c.Score = 6.8; c.Why.Add("final weapon of the line"); }
            else if (me != null && me.Alt)
            {
                var pref = path.FirstOrDefault(x => x.Depth == me.Depth && x.Recommended);
                c.Score = 1.5;
                c.Why.Add(pref != null ? "other branch, plan prefers " + G.Name(pref.W) : "the other branch");
            }
            else if (me == null) { c.Score = 2; c.Why.Add("weapon outside " + owner.Name + "'s line"); }
            else { c.Score = 2; c.Why.Add("weapon tier-up"); }
            if (paid > 0) c.Why.Add("tree boost " + (paid + 1) + "/" + G.NodeMax(node));
        }

        // ---------------------------------------------------------------- abilities: one each, then focus
        internal sealed class AbilityVerdict { public double Score; public readonly List<string> Why = new List<string>(); public bool EvoOwned; public string Tier; public PowerupBase EvoA, EvoB; }

        internal static AbilityVerdict AbilityScore(PowerupBase ability, Survivor owner, Snapshot s)
        {
            var v = new AbilityVerdict();
            string tier; if (K.AbilityTier.TryGetValue(G.Name(ability), out tier)) { v.Tier = tier; double b = Knowledge.Tier(tier, 1.2, 0.6, 0, -0.8); if (b != 0) { v.Score += b; v.Why.Add(tier + "-tier ability in the guides"); } }
            if (owner.Props != null)
            {
                foreach (var syn in G.Each(owner.Props.skillTreeSynergies))
                {
                    if (syn == null || !G.NodeOwned(syn)) continue;
                    PowerupBase req = null; try { req = syn.requiredPowerup; } catch { }
                    if (req == null || !G.Same(req, ability)) continue;
                    CT partner; try { partner = syn.synergiesWithClass; } catch { continue; }
                    if (s.OnSquad(partner)) { v.Score += 2.5; v.Why.Add("synergy with " + G.ClassName(partner) + " on the team"); }
                    else if (!s.SquadFull) { v.Score += 0.8; v.Why.Add("synergy if you recruit " + G.ClassName(partner)); }
                }
            }
            PowerupBase evoA = null, evoB = null;
            try { evoA = ability.abilityEvolutionA; evoB = ability.abilityEvolutionB; } catch { }
            v.EvoA = evoA; v.EvoB = evoB;
            SkillTreeUpgradeBase evoNode = null;
            try { if (evoA != null) evoNode = evoA.skillTreeRequirement; } catch { }
            try { if (evoNode == null && evoB != null) evoNode = evoB.skillTreeRequirement; } catch { }
            if (evoNode != null && G.NodeOwned(evoNode))
            {
                v.EvoOwned = true; v.Score += 1.5;
                v.Why.Add("evolution unlocked in the Training Yard");
            }
            SkillTreeUpgradeBase boost = null; try { boost = ability.skillTreeAbilityBoost; } catch { }
            if (boost == null) { try { boost = ability.skillTreeRequirement; } catch { } }
            int paid = Math.Max(0, G.NodeLevel(boost) - 1);
            if (paid > 0) { v.Score += 0.3 * paid; v.Why.Add("tree boost " + (paid + 1) + "/" + G.NodeMax(boost)); }
            return v;
        }

        static void ScoreAbility(Card c, PowerupBase p, Survivor owner, Snapshot s)
        {
            var a = AbilityScore(p, owner, s);
            int lvl = owner.LevelOf(p);
            int max = G.MaxLevel(p);
            var owned = owner.Powerups.Where(kv => kv.Value >= 1 && kv.Key != null && G.IsAbility(kv.Key)).ToList();
            // the ability to keep feeding is the one furthest along that can still level (maxed ones are done)
            var open = owned.Where(kv => kv.Value < G.MaxLevel(kv.Key)).ToList();
            int highest = open.Count == 0 ? 0 : open.Max(kv => kv.Value);
            if (lvl == 0)
            {
                // the guides: take each ability once (early), then stop spreading
                double baseNew = owned.Count < 2 ? 2.8 : owned.Count < 4 ? 2.2 : 1.6;
                c.Score = baseNew + a.Score * 0.5 + (s.Seconds < 300 ? 0.3 : 0);
                c.Why.Add(owned.Count < 2 ? "new ability, take each once early" : "new ability");
            }
            else
            {
                bool focus = lvl >= highest;
                c.Score = 2.4 + (focus ? 1.2 : 0.2) + a.Score * 0.5 + (a.EvoOwned && lvl == max - 1 ? 0.6 : 0);
                if (a.EvoOwned) c.Why.Add((focus ? "focus: " : "") + "toward its evolution, " + lvl + " to " + (lvl + 1) + " of " + max);
                else c.Why.Add((focus ? "focus ability, " : "leveling, ") + lvl + " to " + (lvl + 1) + " of " + max);
            }
            c.Score = Math.Min(5.5, c.Score);
            c.Why.AddRange(a.Why);
        }

        // ---------------------------------------------------------------- items (chests)
        static void ScoreItem(Card c, Snapshot s)
        {
            c.Kind = "item"; c.Name = G.Name(c.Item);
            c.Score = ItemScore(c.Item, s, c.Why);
        }

        /// <summary>Guide tier first, then squad fit (ItemRules.Evaluate, shared with the offline bench), then the
        /// game-only parts: quest target, stacking. Shared by the chest cards and the plan panel.</summary>
        internal static double ItemScore(ItemBase it, Snapshot s, List<string> why)
        {
            string name = G.Name(it);
            string desc = ""; try { desc = it.EnglishDescription; } catch { }
            if (string.IsNullOrEmpty(desc)) { try { desc = it.GetDescriptionText(); } catch { } }
            var squad = s.Squad.Select(x => x.Name).ToList();
            double score = ItemRules.Evaluate(name, desc, squad, s.Tags, K, why);
            try
            {
                var qm = GameQuestManager.Get;
                if (qm != null && qm.ActiveQuest != null && qm.IsActiveQuestTargetItem(it)) { score += 3; why.Insert(0, "quest target"); }
            }
            catch { }
            if (s.AnyoneHas(it))
            {
                int max = 1; try { max = it.numMaxCanCarry; } catch { }
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
                c.Score = s.SquadFull ? 5 : 1.2;
                c.Why.Add(s.SquadFull ? "squad is full: take the level-up and cash" : "level-up and cash instead of a recruit");
                return;
            }
            c.Kind = "survivor";
            ClassProperties props = null; try { props = p.targetClassProperties; } catch { }
            if (props == null) { c.Score = 0.5; c.Why.Add("unknown survivor"); return; }
            CT cls = props.characterType; c.Name = G.ClassName(cls);
            if (s.OnSquad(cls)) { c.Score = 0; c.Why.Add("already on the squad"); return; }
            c.Score = RecruitScore(cls, props, s, c.Why);
        }

        /// <summary>Guide rescue tier + synergy potential with the squad + owned team passives. Shared by SOS cards and the plan panel.</summary>
        internal static double RecruitScore(CT cls, ClassProperties props, Snapshot s, List<string> why)
        {
            string name = G.ClassName(cls);
            double score = 0;
            string tier; K.RescueTier.TryGetValue(name, out tier);
            double t = Knowledge.Tier(tier, 2.5, 1.5, 0.5, -0.5);
            if (tier != null) { score += t; why.Add(tier + "-tier rescue in the guides"); }

            // synergy potential with everyone on the squad: all synergy nodes between the pair (owned count more)
            int pairs = 0, ownedPairs = 0; var partners = new List<string>();
            foreach (var sv in s.Squad)
            {
                int n = 0, o = 0;
                if (sv.Props != null) foreach (var syn in G.Each(sv.Props.skillTreeSynergies))
                {
                    CT partner; try { partner = syn.synergiesWithClass; } catch { continue; }
                    if (partner != cls) continue;
                    n++; if (G.NodeOwned(syn)) o++;
                }
                if (props != null) foreach (var syn in G.Each(props.skillTreeSynergies))
                {
                    CT partner; try { partner = syn.synergiesWithClass; } catch { continue; }
                    if (partner != sv.Type) continue;
                    n++; if (G.NodeOwned(syn)) o++;
                }
                pairs += n; ownedPairs += o;
                if (n > 0) partners.Add(n + " with " + sv.Name + (o > 0 ? " (" + o + " owned)" : ""));
            }
            score += 0.7 * pairs + 0.6 * ownedPairs;
            if (pairs > 0) why.Add((pairs == 1 ? "1 synergy " : pairs + " synergies ") + string.Join(", ", partners));

            foreach (var n in G.NodesOf(cls))
            {
                if (!G.NodeOwned(n)) continue;
                string d = G.NodeDesc(n);
                if (d.IndexOf("on the team", StringComparison.OrdinalIgnoreCase) >= 0) { score += 0.5; why.Add(ShortDesc(d)); }
            }
            int lvl = props != null ? G.TreeLevel(props) : 0;
            score += Math.Max(0, 1 - lvl / (double)TreeMaxLevel) * 0.6;
            int nextThr = -1; foreach (var thr in RankLevels) if (thr > lvl) { nextThr = thr; break; }
            if (nextThr > 0 && nextThr - lvl <= 5) { score += 0.3; why.Add((nextThr - lvl) + " level" + (nextThr - lvl > 1 ? "s" : "") + " from rank " + (Array.IndexOf(RankLevels, nextThr) + 1)); }
            if (why.Count == 0) why.Add("L" + lvl + ", no synergy with this squad");
            // headline: tier + synergies in one line
            if (tier != null && pairs > 0) why.Insert(0, tier + "-tier rescue, " + (pairs == 1 ? "1 synergy" : pairs + " synergies") + " with the squad");
            return score;
        }

        // ---------------------------------------------------------------- military training and Endless stat cards
        static void ScoreMilitary(Card c, Snapshot s)
        {
            var p = c.Powerup; c.Kind = "stat";
            var b = p.TryCast<BasicLevelPowerup>();
            string rarity = "Common"; try { if (b != null) rarity = b.GetRarity().ToString(); } catch { }
            double r = rarity == "Legendary" ? 3 : rarity == "Endless" ? 2.5 : rarity == "Rare" ? 2 : 1;
            string asset = G.Asset(p); string stat = asset.StartsWith("MilitaryTraining_") ? asset.Substring("MilitaryTraining_".Length) : asset;
            double w = 0.5; foreach (var kv in K.MilitaryStat) if (stat.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0) w = Math.Max(w, kv.Value);
            string target = ""; try { if (b != null) target = b.targetType.ToString(); } catch { }
            bool team = target.IndexOf("Team", StringComparison.OrdinalIgnoreCase) >= 0;
            c.Score = r + w + (team ? 0.3 : 0);
            c.Why.Add(rarity + (team ? ", team-wide" : "") + ": " + Humanize(stat));
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
            var sb = new System.Text.StringBuilder();
            foreach (var ch in s) { if (char.IsUpper(ch) && sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' '); sb.Append(ch); }
            return sb.ToString().Replace("Mod", "").Trim();
        }
    }
}
