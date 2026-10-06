// The active quest's other objectives as advice (0.14.0, C3 of the 10-05 review; B2, the health items, first). A quest of
// the game's quest log asks more of a run than its team (QuestTeam.cs): a weapon finished at max level, an ability or an
// evolution held at the end, health items in the inventory, kills with one survivor, damage type tags, evolutions, a rare
// training, armor, time at full health - or that something is NOT taken (a class's abilities, a tier-3 weapon, any item).
// Up to 0.13.0 the Companion logged all of it "advice unchanged": in the user's 10-05 run under the Engineer's "Heroic
// Theory" (survive 20:00, finish with the tier-3 weapon at max level, 2000 kills with the Engineer) it ranked the Taser
// last of three on every offer under the lent build's "abilities first", and the player took it against the advice.
//
// Quest.cs decodes each objective into a QuestRule (what each one counts was read in the game's machine code - see
// research\review_1005\impl\c3: FinishWithWeapons = a player holds one of the weapons at the level, in hand when it says so,
// re-checked at every pick; FinishWithEvolutions = the leader holds an evolution of the ability; FinishWithoutClassAbilities
// = no ability of the class, failed for good at the pick; FinishWithoutTier3Weapons = no tier-3 weapon in hand at the end;
// FullyUpgradeClass = every ability of the class unlocked in the Skill Tree at its last level - evolved where its evolution is
// unlocked - and a weapon of the class's top tier maxed; HealingItemCount = health items the leader holds) and refreshes its
// progress once per snapshot. Judge() says what a rule means for one card (QuestCard: what the ranking knows of it), Row()
// what the PLAN readout's QUEST row says while a rule changes the advice:
//  - a strong lift with a "quest: ..." reason the card shows: the weapon line up to the weapon asked for (over every ability
//    and tier-up, under a recruit's first weapon and every evolution), an ability or an evolution the quest needs, a health
//    item (+2.0), a Research Pod of a type the quest counts, a Rare+ training, armor;
//  - AVOID with a "quest: ..." reason: a pick that fails the quest for good (an ability of a class it forbids, a tier-3
//    weapon under "no tier-3", any item under "no items", tag points past its cap, a tier-3 branch it does not count);
//  - a modest lift (kills with one survivor, tag points, evolutions, full health, a fully upgraded class) that never passes
//    the build's core abilities on the same offer (UnderCore): a reason in the log, the card keeps its own words.
// The build plan stays (decision D3 of 10-05): the build's branch, ability order and tag plan are not rewritten; the quest
// lifts what the build would leave for later and takes back what would fail it. [Advice] QuestSteer: On (default),
// InfoOnly (the QUEST row and the reasons in the log, no card moves), Off (the quest is not followed at all - its team rule
// neither). A quest whose objectives match Any, one the game counts as failed, and a run that does not fit the quest's
// conditions (QuestTeam's gate: AreFulfillmentConditionsMet) leave the advice as it is. Pure: the offline bench replays it.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace YazsCompanion
{
    /// <summary>[Advice] QuestSteer: how hard the active quest steers the advice (decision D3 of 10-05).</summary>
    public enum QuestSteer { On, InfoOnly, Off }

    /// <summary>What one objective asks of the run.</summary>
    internal enum QuestAsk
    {
        WeaponLine,      // FinishWithWeapons: one of Targets at Level (Depth: where they sit in the line); FullTeamTier3Weapons: Class null, a tier-3 weapon each
        Ability,         // FinishWithAbilities: Targets at Level
        Evolution,       // FinishWithEvolutions: an evolution of Targets[0]
        NoClassAbility,  // FinishWithoutClassAbilities: no ability of Class
        NoTier3,         // FinishWithoutTier3Weapons: no tier-3 weapon in hand at the end
        FullClass,       // FullyUpgradeClass: every ability of Class at its last level (evolved where unlocked), its top weapon maxed
        Synergies,       // ClassActiveSynergies: Need active synergies of Class
        HealthItems,     // HealingItemCount: Need health items held by the leader
        Items,           // CollectItem: one of Targets (scored by the game's own target test, +3; here for the QUEST row)
        Kills,           // GameStatisticThreshold on CharacterKillsCurrentRun<Class>
        TagPoints,       // StatisticThreshold on TeamHashtag<Type>Num (or the Explosive + Slashing + Fire sum): reach Need, or stay Below it
        TypesAt10,       // StatisticThreshold InternalNumHashtagTypes10Plus: Need damage types at 10 tag points
        FillSlots,       // StatisticThreshold InternalNumFreeItemSlots <= 0: every item slot filled
        NoItems,         // StatisticThreshold InternalNumEquippedItems == 0 (fails for good): no item at all
        Evolutions,      // StatisticThreshold InternalNumAbilityEvolutions: Need evolutions
        RareTraining,    // StatisticThreshold InternalNumRarePlusMilitaryTrainings: Need Rare or Legendary trainings
        Armor,           // StatisticThreshold TeamArmor: Need armor
        FullHealth,      // FullHealthTime: Need seconds at full health
    }

    /// <summary>One objective of the active quest, decoded, with its progress (refreshed per snapshot by Quest.cs).</summary>
    internal sealed class QuestRule
    {
        public QuestAsk Ask;
        public string Class;                    // the survivor class it is about (the game's display name: Ghost for Ninja); null = the squad
        public readonly List<string> Targets = new List<string>();     // the weapons / abilities / items / damage types it names
        public bool All;                        // all of Targets (else any one)
        public int Level;                       // the level asked for
        public int Depth = -1;                  // WeaponLine: where the weapons asked for sit in their line (0, 1, 2 = tier 3)
        public bool AllBranches;                // WeaponLine: every weapon of that depth counts ("the tier-3 weapon")
        public bool Current;                    // WeaponLine: it must be the weapon in hand
        public bool Below;                      // TagPoints: the points must stay under Need (else reach it)
        public double Have = double.NaN, Need;  // the progress the game counts (NaN: not read)
        public bool Met;                        // the objective holds now
        public string Unreachable;              // why it can no longer hold this run (null: it can)
        public readonly HashSet<string> Done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);    // FullTeamTier3: the classes with a tier-3 weapon
        // WeaponLine / Ability: where the class stands now (the weapon in hand, or the ability's level), for the QUEST row
        public string From; public int FromLevel, FromMax, FromDepth = -1;

        public bool Open { get { return !Met && Unreachable == null; } }
    }

    internal enum QuestCardKind { Other, Weapon, NextTier, Branch, FirstWeapon, Ability, Evolution, Item, Tags, Stat, Recruit, Liberate }

    /// <summary>What the ranking knows of a card, for the quest's rules (Ranker fills it once the card is scored).</summary>
    internal sealed class QuestCard
    {
        public QuestCardKind Kind;
        public string Class, Name, Base;        // the owner's class (a recruit: its own), the card's name, an evolution's base ability
        public int Level, Max;                  // the level now (before the pick) and the last level
        public int Depth = -1;                  // a weapon's place in its line
        public readonly List<string> Types = new List<string>();     // damage types it deals; a Research Pod's type
        public int Points;                      // a Research Pod's tag points
        public bool Health, Armor;              // an item the health objective counts / a healing ability / a health stat; armor (stat or item)
        public bool Skipped, Core;              // the owner's build skips it / ranks it among its first three (still open)
        public bool Evolvable;                  // an ability whose evolution is unlocked in the Skill Tree
        public string Rarity;                   // a stat card's rarity
        public readonly List<string> Synergy = new List<string>();   // classes it has an unlocked synergy with on this squad (a recruit; an ability's partner)
    }

    /// <summary>What the quest does to one card (QuestRules.Judge).</summary>
    internal sealed class QuestVerdict
    {
        public double Score;                    // the card's score under the quest
        public string Line;                     // "quest: ..." - the reason, logged and (Head) shown under the card
        public bool Head;                       // the line decides the card: it goes first and the card says it
        public bool Avoid, Strong, Modest;
        public QuestRule By;
    }

    internal sealed class QuestRules
    {
        public const double LineFloor = 6.8, LineCap = 7.15;    // the weapon line asked for: over every ability (under 6) and tier-up (6.6 + synergy),
                                                                // under a recruit's first weapon (7.2) and every evolution (7.6 and up)
        public const double PickFloor = 6.7;                    // an ability, or the base of an evolution, the quest asks for
        public const double AvoidScore = 0.4;                   // a pick that fails the quest: under 1, so the card reads AVOID
        public const double HealthLift = 2.0;                   // a health item while the quest counts too few (B2)
        public const double CountLift = 2.0;                    // a Research Pod, a training, an armor card or a recruit the quest counts
        public const double ModestMax = 0.8, ModestMin = -1.0;  // the modest lifts of one card together

        public string Quest = "";
        public bool AnyOf, Failed;
        public string NotThisRun;                               // QuestTeam's gate: why this run cannot complete the quest
        public QuestSteer Steer = QuestSteer.On;
        public string TeamWords;                                // InfoOnly: the team rule, which the SOS row then leaves out
        public readonly List<QuestRule> List = new List<QuestRule>();
        public readonly Dictionary<string, int> Points = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);     // the squad's tag points now

        /// <summary>The quest is followed at all (the QUEST row, the reasons).</summary>
        public bool Follows { get { return Steer != QuestSteer.Off && !AnyOf && !Failed && NotThisRun == null; } }
        /// <summary>... and moves the cards.</summary>
        public bool Moves { get { return Follows && Steer == QuestSteer.On; } }
        public bool Any(QuestAsk ask) { foreach (var r in List) if (r.Ask == ask && r.Open) return true; return false; }

        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static bool Same(string a, string b) { return string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase); }
        static string N(double v) { return double.IsNaN(v) ? "?" : Math.Round(v).ToString("0", IC); }
        static string Or(IList<string> names) { return names.Count <= 1 ? (names.Count == 1 ? names[0] : "") : string.Join(", ", names.Take(names.Count - 1)) + " or " + names[names.Count - 1]; }
        static string And(IList<string> names) { return names.Count <= 1 ? (names.Count == 1 ? names[0] : "") : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1]; }
        static string Clock(double seconds) { if (double.IsNaN(seconds)) return "?"; int s = (int)Math.Max(0, seconds); return (s / 60) + ":" + (s % 60).ToString("00", IC); }

        /// <summary>A lift to <paramref name="floor"/> that keeps the order among lifted cards: a little of the card's own score
        /// on top, never past <paramref name="cap"/>, never under the card's own score.</summary>
        public static double Up(double score, double floor, double cap) { return Math.Max(score, Math.Min(cap, floor + Math.Min(0.3, Math.Max(0, score) * 0.04))); }

        /// <summary>A modest lift keeps a card that was under one of the build's core cards under it (<paramref name="cores"/> = their
        /// scores before the quest's pass and after it): kills with a class or tag points never pass the build's own abilities.</summary>
        public static double UnderCore(double before, double after, IEnumerable<KeyValuePair<double, double>> cores)
        {
            if (after <= before || cores == null) return after;
            foreach (var c in cores) if (c.Key >= before && after >= c.Value) after = Math.Max(before, c.Value - 0.01);
            return after;
        }

        /// <summary>The game's comparison (GameHubQuestComparison 0 Less, 1 LessOrEqual, 2 Equal, 3 GreaterOrEqual, 4 Greater).</summary>
        public static bool Holds(int comparison, double have, double threshold)
        {
            switch (comparison)
            {
                case 0: return have < threshold;
                case 1: return have <= threshold;
                case 2: return Math.Abs(have - threshold) < 1e-4;
                case 3: return have >= threshold;
                case 4: return have > threshold;
                default: return false;
            }
        }

        // ================================================================ decoding (the game's raw values, pure)
        /// <summary>A StatisticThreshold objective as a rule: the statistic by its PlayerStatistic.EType NAME (robust to a patch that
        /// renumbers the enum), the comparison, the threshold, failPermanentlyWhenConditionNotMet. Null: nothing for the cards
        /// (the team size is QuestTeam's, the other statistics are not asked by any 1.0.2 quest).</summary>
        public static QuestRule FromStatistic(string stat, int comparison, double threshold, bool failPermanently)
        {
            string s = stat ?? "";
            bool atLeast = comparison == 3 || comparison == 4, atMost = comparison == 0 || comparison == 1;
            double reach = comparison == 4 ? Math.Floor(threshold + 1e-6) + 1 : Math.Ceiling(threshold - 1e-6);       // the count that holds it
            double cap = comparison == 1 ? Math.Floor(threshold + 1e-6) + 1 : threshold;                              // stay under this
            if (s == "InternalNumEquippedItems" && (comparison == 2 || atMost) && threshold < 1) return new QuestRule { Ask = QuestAsk.NoItems, Need = 0 };
            if (s == "InternalNumFreeItemSlots" && (comparison == 2 || atMost) && threshold < 1) return new QuestRule { Ask = QuestAsk.FillSlots, Need = 0 };
            if (!atLeast && !atMost) return null;
            if (s.StartsWith("TeamHashtag", StringComparison.Ordinal) && s.EndsWith("Num", StringComparison.Ordinal))
            {
                var r = new QuestRule { Ask = QuestAsk.TagPoints, Below = atMost, Need = atMost ? cap : reach };
                string mid = s.Substring("TeamHashtag".Length, s.Length - "TeamHashtag".Length - 3);
                foreach (var t in TagProfile.Names.Concat(new[] { "Toxic" }))
                    if (mid.IndexOf(t, StringComparison.Ordinal) >= 0) { string shown = t == "Toxic" ? "Chemical" : t; if (!r.Targets.Contains(shown)) r.Targets.Add(shown); }
                if (r.Targets.Count == 0) return null;
                // the game's order of the sum ("ExplosiveSlashingFire"), as the quest names it
                r.Targets.Sort((a, b) => mid.IndexOf(a == "Chemical" ? "Toxic" : a, StringComparison.Ordinal).CompareTo(mid.IndexOf(b == "Chemical" ? "Toxic" : b, StringComparison.Ordinal)));
                return r;
            }
            if (!atLeast) return null;
            switch (s)
            {
                case "InternalNumHashtagTypes10Plus": return new QuestRule { Ask = QuestAsk.TypesAt10, Need = reach };
                case "InternalNumAbilityEvolutions": return new QuestRule { Ask = QuestAsk.Evolutions, Need = reach };
                case "InternalNumRarePlusMilitaryTrainings": return new QuestRule { Ask = QuestAsk.RareTraining, Need = reach };
                case "TeamArmor": return new QuestRule { Ask = QuestAsk.Armor, Need = reach };
            }
            return null;
        }

        /// <summary>A GameStatisticThreshold objective: kills with one class this run ("CharacterKillsCurrentRunEngineer", the
        /// GameStatisticValue's asset name; the class part is the game's enum name - Ninja shows as Ghost). Null: another statistic.</summary>
        public static QuestRule FromGameStatistic(string statAsset, int comparison, double threshold)
        {
            const string Kills = "CharacterKillsCurrentRun";
            if (string.IsNullOrEmpty(statAsset) || !statAsset.StartsWith(Kills, StringComparison.Ordinal) || (comparison != 3 && comparison != 4)) return null;
            string cls = statAsset.Substring(Kills.Length);
            if (cls.Length == 0) return null;
            return new QuestRule { Ask = QuestAsk.Kills, Class = cls == "Ninja" ? "Ghost" : cls, Need = comparison == 4 ? Math.Floor(threshold) + 1 : Math.Ceiling(threshold) };
        }

        // ================================================================ a card
        sealed class Effect { public bool Avoid, Strong; public double Score, Delta; public string Line; public QuestRule By; }

        /// <summary>What the open rules do to one card of score <paramref name="score"/>; null: nothing. AVOID wins over a lift (a
        /// pick that fails the quest), the strongest lift over the others; modest lifts add up within their bounds. InfoOnly:
        /// the same reasons, logged after the card's own, and the score untouched.</summary>
        public QuestVerdict Judge(QuestCard c, double score)
        {
            if (c == null || !Follows) return null;
            Effect avoid = null, strong = null; double modest = 0; var lines = new List<string>(); QuestRule modestBy = null;
            foreach (var r in List)
            {
                if (!r.Open) continue;
                var e = Of(r, c, score);
                if (e == null) continue;
                e.By = r;
                if (e.Avoid) { if (avoid == null) avoid = e; }
                else if (e.Strong) { if (strong == null || e.Score > strong.Score) strong = e; }
                else if (e.Delta != 0) { modest += e.Delta; lines.Add(e.Line); if (modestBy == null) modestBy = r; }
            }
            QuestVerdict v = null;
            if (avoid != null) v = new QuestVerdict { Score = Math.Min(score, AvoidScore), Line = avoid.Line, Head = true, Avoid = true, By = avoid.By };
            else if (strong != null) v = new QuestVerdict { Score = strong.Score, Line = strong.Line, Head = true, Strong = true, By = strong.By };
            else if (lines.Count > 0) v = new QuestVerdict { Score = score + Math.Max(ModestMin, Math.Min(ModestMax, modest)), Line = string.Join("; ", lines), Modest = true, By = modestBy };
            if (v == null) return null;
            if (!Moves) { v.Score = score; v.Head = false; v.Line += " (info only)"; }
            return v;
        }

        // the weapon a weapon-line rule asks for, in words: "the tier-3 weapon", "Blowtorch", "Plasma or Laser"
        static string Goal(QuestRule r)
        {
            if (r.AllBranches && r.Depth >= 1) return "the tier-" + (r.Depth + 1) + " weapon";
            if (r.Class == null) return "a tier-3 weapon each";
            return r.Targets.Count > 0 ? Or(r.Targets) : "the weapon";
        }
        static bool Counts(QuestRule r, string name) { return r.AllBranches || r.Targets.Any(t => Same(t, name)); }
        static string Step(QuestCard c) { int next = c.Level + 1; return c.Max > 0 ? "Level " + next + " of " + c.Max : "Level " + next; }      // "Level 4 of 4": every number says what it counts
        static string ToLevel(QuestRule r, string what) { return r.Level >= 4 || r.Level <= 0 ? "max " + what : what + " to level " + r.Level; }
        static string Types(QuestRule r) { return r.Targets.Count <= 1 ? (r.Targets.Count == 1 ? r.Targets[0] + " tags" : "tags") : string.Join(", ", r.Targets); }
        static int Adds(QuestRule r, QuestCard c)
        {
            int n = 0;
            foreach (var t in c.Types) if (r.Targets.Any(x => Same(x, t))) n += c.Kind == QuestCardKind.Tags ? Math.Max(1, c.Points) : 1;
            return n;
        }
        static string Count(QuestRule r) { return double.IsNaN(r.Have) ? "" : " (" + N(r.Have) + " of " + N(r.Need) + ")"; }
        static string Now(QuestRule r, double have) { return double.IsNaN(have) ? "" : " (" + N(have) + " now)"; }
        static bool Powerup(QuestCard c) { return c.Kind == QuestCardKind.Weapon || c.Kind == QuestCardKind.NextTier || c.Kind == QuestCardKind.Branch || c.Kind == QuestCardKind.FirstWeapon || c.Kind == QuestCardKind.Ability || c.Kind == QuestCardKind.Evolution; }
        static bool Mine(QuestRule r, QuestCard c) { return r.Class == null || Same(r.Class, c.Class); }

        Effect Of(QuestRule r, QuestCard c, double score)
        {
            switch (r.Ask)
            {
                case QuestAsk.WeaponLine: return WeaponLine(r, c, score);
                case QuestAsk.Ability:
                    if (c.Kind != QuestCardKind.Ability || !Mine(r, c) || !r.Targets.Any(t => Same(t, c.Name)) || (r.Level > 0 && c.Level >= r.Level)) return null;
                    {
                        bool max = r.Level >= 4 || r.Level <= 0 || (c.Max > 0 && r.Level >= c.Max);
                        return Lift(Up(score, PickFloor, LineCap), c.Level <= 0 ? "quest: take it and " + (max ? "max it" : "level it to " + r.Level)
                            : "quest: " + (max ? "max this ability" : "this ability to level " + r.Level) + " (" + Step(c) + ")");
                    }
                case QuestAsk.Evolution:
                    {
                        string b = r.Targets.Count > 0 ? r.Targets[0] : null;
                        if (b == null || !Mine(r, c)) return null;
                        if (c.Kind == QuestCardKind.Evolution && Same(c.Base, b)) return Lift(score + 1.0, "quest: evolves " + b + " - it counts");      // over another base's evolution
                        if (c.Kind == QuestCardKind.Ability && Same(c.Name, b) && (c.Max <= 0 || c.Level < c.Max))
                            return Lift(Up(score, PickFloor, LineCap), "quest: evolve it by the end" + (c.Level <= 0 ? " - take it" : " (" + Step(c) + ")"));
                        return null;
                    }
                case QuestAsk.NoClassAbility:
                    if ((c.Kind == QuestCardKind.Ability || c.Kind == QuestCardKind.Evolution) && Mine(r, c)) return Fails("quest: any " + r.Class + " ability fails it");
                    return null;
                case QuestAsk.NoTier3:
                    if ((c.Kind == QuestCardKind.NextTier || c.Kind == QuestCardKind.Branch) && c.Depth >= 2) return Fails("quest: a tier-3 weapon fails it");
                    return null;
                case QuestAsk.FullClass:
                    {
                        if (!Mine(r, c) || !Powerup(c) || c.Kind == QuestCardKind.FirstWeapon) return null;
                        string have = Count(r);
                        if (c.Kind == QuestCardKind.Ability && c.Skipped) return Lift(score + 2.0, "quest: every " + r.Class + " ability to max level");
                        return Small(c.Kind == QuestCardKind.Evolution ? 0.3 : 0.4, "quest: max every " + r.Class + " powerup" + have);
                    }
                case QuestAsk.Synergies:
                    {
                        bool with = c.Synergy.Any(x => Same(x, r.Class)) || (Same(c.Class, r.Class) && c.Synergy.Count > 0);
                        if (!with) return null;
                        // "quest: 2 synergies with Engineer (0 so far)"
                        string syn = (double.IsNaN(r.Need) || r.Need <= 0 ? "synergies" : N(r.Need) + (r.Need == 1 ? " synergy" : " synergies")) + " with " + r.Class
                            + (double.IsNaN(r.Have) ? "" : " (" + N(r.Have) + " so far)");
                        if (c.Kind == QuestCardKind.Recruit) return Lift(score + CountLift, "quest: " + syn);
                        if (c.Kind == QuestCardKind.Ability) return Small(0.6, "quest: " + syn);
                        return null;
                    }
                case QuestAsk.HealthItems:
                    if (c.Kind != QuestCardKind.Item || !c.Health || (!double.IsNaN(r.Have) && r.Have >= r.Need)) return null;
                    {
                        int need = (int)Math.Max(1, r.Need);
                        return Lift(score + HealthLift, "quest: hold " + (need == 1 ? "a health item" : need + " health items") + " (" + (double.IsNaN(r.Have) ? "0" : N(r.Have)) + " of " + need + ")");
                    }
                case QuestAsk.Kills:
                    if (!Mine(r, c) || !Powerup(c) || c.Types.Count == 0 || c.Kind == QuestCardKind.FirstWeapon) return null;
                    return Small(0.3, double.IsNaN(r.Have) ? "quest: more " + r.Class + " kills" : "quest: " + r.Class + " kills (" + N(r.Have) + " of " + N(r.Need) + ")");
                case QuestAsk.TagPoints: return TagPoints(r, c, score);
                case QuestAsk.TypesAt10:
                    {
                        string have = Now(r, r.Have);
                        if (c.Kind == QuestCardKind.Tags)
                        {
                            string t = c.Types.FirstOrDefault(); int p = t == null ? 0 : PointsOf(t);
                            if (t != null && p < 10 && p + c.Points >= 10) return Lift(score + CountLift, "quest: " + N(r.Need) + " types at 10 tags" + have);
                            if (t != null && p < 10) return Small(0.5, "quest: " + N(r.Need) + " types at 10 tags" + have);
                            return null;
                        }
                        if (Powerup(c) && c.Types.Any(t => { int p = PointsOf(t); return p >= 7 && p < 10; })) return Small(0.4, "quest: " + N(r.Need) + " types at 10 tags" + have);
                        return null;
                    }
                case QuestAsk.FillSlots:
                    if (c.Kind != QuestCardKind.Item) return null;
                    return Small(0.3, "quest: fill every item slot" + (double.IsNaN(r.Have) ? "" : " (" + N(r.Have) + " free)"));
                case QuestAsk.NoItems:
                    return c.Kind == QuestCardKind.Item ? Fails("quest: taking any item fails it") : null;
                case QuestAsk.Evolutions:
                    {
                        string have = Count(r);
                        if (c.Kind == QuestCardKind.Evolution) return Small(0.3, "quest: evolutions" + have);
                        if (c.Kind == QuestCardKind.Ability && c.Evolvable && (c.Max <= 0 || c.Level < c.Max)) return Small(c.Level <= 0 ? 0.3 : 0.5, "quest: evolutions" + have);
                        return null;
                    }
                case QuestAsk.RareTraining:
                    if (c.Kind != QuestCardKind.Stat || !(Same(c.Rarity, "Rare") || Same(c.Rarity, "Legendary"))) return null;
                    return Lift(score + CountLift, "quest: Rare or better training" + Count(r));
                case QuestAsk.Armor:
                    if (!c.Armor) return null;
                    if (c.Kind == QuestCardKind.Stat) return Lift(score + CountLift, "quest: armor to " + N(r.Need) + Now(r, r.Have));
                    if (c.Kind == QuestCardKind.Item) return Small(0.6, "quest: armor to " + N(r.Need) + Now(r, r.Have));
                    return null;
                case QuestAsk.FullHealth:
                    if (!(c.Health || c.Armor) || (c.Kind != QuestCardKind.Stat && c.Kind != QuestCardKind.Item && c.Kind != QuestCardKind.Ability)) return null;
                    return Small(0.4, "quest: time at full health" + (double.IsNaN(r.Have) ? "" : " (" + Clock(r.Have) + " of " + Clock(r.Need) + ")"));
                default: return null;      // Items: the game's own target test scores them (Ranker.ItemScore)
            }
        }
        static Effect Lift(double score, string line) { return new Effect { Strong = true, Score = score, Line = line }; }
        static Effect Fails(string line) { return new Effect { Avoid = true, Line = line }; }
        static Effect Small(double delta, string line) { return new Effect { Delta = delta, Line = line }; }
        int PointsOf(string type) { int n; return type != null && Points.TryGetValue(type, out n) ? n : 0; }

        // the weapon line: every step up to the weapon asked for goes over the abilities; a branch of that tier the quest does not
        // count fails it (the branches exclude each other); past it (a tier-2 weapon asked for, the tier-3 taken) nothing
        Effect WeaponLine(QuestRule r, QuestCard c, double score)
        {
            if (!Mine(r, c) || c.Depth < 0) return null;
            if (r.Class == null && r.Done.Contains(c.Class ?? "")) return null;            // a tier-3 weapon each: this survivor has one
            string goal = ToLevel(r, Goal(r));
            switch (c.Kind)
            {
                case QuestCardKind.Weapon:
                    // a step on the way (the Taser of a tier-3 quest: the game's TIER I plate) leads there; "max the tier-3 weapon
                    // (Level 4 of 4)" on it read as if the quest were about to be met
                    if (c.Depth < r.Depth) return Lift(Up(score, LineFloor, LineCap), "quest: leads to " + Goal(r) + " (" + Step(c) + ")");
                    if (c.Depth == r.Depth && Counts(r, c.Name) && (r.Level <= 0 || c.Level < r.Level))
                        return Lift(Up(score, LineFloor, LineCap), "quest: " + goal + " (" + Step(c) + ")");
                    return null;
                case QuestCardKind.NextTier: case QuestCardKind.Branch:        // a recruit's first weapon (7.2) is over the line already
                    if (c.Depth < r.Depth) return Lift(Up(score, LineFloor, LineCap), "quest: leads to " + Goal(r) + " (next tier)");
                    if (c.Depth > r.Depth) return null;
                    if (Counts(r, c.Name)) return Lift(Up(score, LineFloor, LineCap), "quest: " + goal + " - this one counts");
                    return Fails("quest: needs " + Or(r.Targets) + " - this branch fails it");
            }
            return null;
        }

        // tag points: reach a count (a Research Pod of the type strongly, a level dealing it a little), or stay under a cap (a pick
        // that would reach it fails the quest for good: tag points never go down)
        Effect TagPoints(QuestRule r, QuestCard c, double score)
        {
            int add = Adds(r, c);
            if (add <= 0 || !(c.Kind == QuestCardKind.Tags || Powerup(c))) return null;
            double have = double.IsNaN(r.Have) ? r.Targets.Sum(t => PointsOf(t)) : r.Have;
            if (r.Below)
            {
                if (have + add >= r.Need) return Fails("quest: would pass its cap of " + N(r.Need) + " tags");
                if (r.Need - have - add <= 3) return Small(-0.8, "quest: tags near the cap (" + N(have) + " of " + N(r.Need) + ")");
                return Small(-0.2, "quest: tags under " + N(r.Need) + Now(r, have));
            }
            string line = "quest: " + Types(r) + " to " + N(r.Need) + Now(r, have);
            return c.Kind == QuestCardKind.Tags ? Lift(score + CountLift, line) : Small(0.4, line);
        }

        // ================================================================ the QUEST row
        static readonly QuestAsk[] RowOrder =
        {
            QuestAsk.NoItems, QuestAsk.NoClassAbility, QuestAsk.NoTier3, QuestAsk.WeaponLine, QuestAsk.Ability, QuestAsk.Evolution, QuestAsk.FullClass,
            QuestAsk.HealthItems, QuestAsk.Items, QuestAsk.Synergies, QuestAsk.TagPoints, QuestAsk.TypesAt10, QuestAsk.RareTraining, QuestAsk.Armor,
            QuestAsk.Evolutions, QuestAsk.FillSlots, QuestAsk.Kills, QuestAsk.FullHealth,
        };

        /// <summary>What the PLAN readout's QUEST row says (at most <paramref name="max"/> items, the strongest first): the open
        /// rules, in words that change with a pick, not with every kill. Empty while nothing open changes the advice.</summary>
        public List<string> Row(int max = 2)
        {
            var items = new List<string>();
            if (!Follows) return items;
            if (Steer == QuestSteer.InfoOnly && !string.IsNullOrEmpty(TeamWords)) items.Add(TeamWords);
            foreach (var ask in RowOrder)
                foreach (var r in List)
                {
                    if (items.Count >= max) return items;
                    if (r.Ask != ask || !r.Open) continue;
                    string t = RowText(r);
                    if (!string.IsNullOrEmpty(t) && !items.Contains(t)) items.Add(t);
                }
            return items;
        }

        /// <summary>One rule in a few words: "Taser to tier 3, max level", "a health item (0 of 1)", "no Pyro abilities".</summary>
        public static string RowText(QuestRule r)
        {
            string have = double.IsNaN(r.Have) ? "" : " (" + N(r.Have) + " of " + N(r.Need) + ")";
            string lvl = r.Level >= 4 || r.Level <= 0 ? "max level" : "level " + r.Level;
            switch (r.Ask)
            {
                case QuestAsk.WeaponLine:
                    if (r.Class == null) return "a tier-3 weapon each" + (double.IsNaN(r.Have) ? "" : " (" + N(r.Have) + " of " + N(r.Need) + ")");
                    if (r.From == null || r.FromDepth < 0) return Goal(r) + " at " + lvl;
                    if (r.FromDepth < r.Depth) return r.From + " to " + (r.AllBranches ? "tier " + (r.Depth + 1) : Or(r.Targets)) + ", " + lvl;
                    return r.From + " " + r.FromLevel + "/" + r.FromMax + " to " + lvl;
                case QuestAsk.Ability:
                    {
                        string a = r.Targets.Count > 0 ? r.Targets[0] : "the ability";
                        return r.FromLevel <= 0 ? "take " + a + ", " + (r.Level >= 4 || r.Level <= 0 ? "max it" : "to level " + r.Level) : a + " " + r.FromLevel + "/" + (r.FromMax > 0 ? r.FromMax : 4) + " to " + lvl;
                    }
                case QuestAsk.Evolution: return "evolve " + (r.Targets.Count > 0 ? r.Targets[0] : "the ability");
                case QuestAsk.NoClassAbility: return "no " + r.Class + " abilities";
                case QuestAsk.NoTier3: return "no tier-3 weapon";
                case QuestAsk.FullClass: return "max every " + r.Class + " powerup" + (double.IsNaN(r.Have) ? "" : " (" + N(r.Have) + " of " + N(r.Need) + ")");
                case QuestAsk.Synergies: return r.Class + " synergies" + have;
                case QuestAsk.HealthItems: return (r.Need <= 1 ? "a health item" : N(r.Need) + " health items") + (double.IsNaN(r.Have) ? "" : " (" + N(r.Have) + " of " + N(Math.Max(1, r.Need)) + ")");
                case QuestAsk.Items: return r.Targets.Count == 0 ? "the quest's item" : "collect " + r.Targets[0] + (r.Targets.Count > 1 ? " or another" : "");
                case QuestAsk.Kills: return r.Class + " kills to " + N(r.Need);
                case QuestAsk.TagPoints: return (r.Targets.Count > 1 ? string.Join("+", r.Targets) : r.Targets.Count == 1 ? r.Targets[0] : "tags") + (r.Below ? " under " : " to ") + N(r.Need);
                case QuestAsk.TypesAt10: return double.IsNaN(r.Have) ? N(r.Need) + " types at 10 tags" : "types at 10 tags (" + N(r.Have) + " of " + N(r.Need) + ")";
                case QuestAsk.FillSlots: return "fill every item slot";
                case QuestAsk.NoItems: return "take no items";
                case QuestAsk.Evolutions: return double.IsNaN(r.Have) ? N(r.Need) + " evolutions" : "evolutions (" + N(r.Have) + " of " + N(r.Need) + ")";
                case QuestAsk.RareTraining: return double.IsNaN(r.Have) ? N(r.Need) + " Rare+ trainings" : "Rare+ trainings (" + N(r.Have) + " of " + N(r.Need) + ")";
                case QuestAsk.Armor: return "armor to " + N(r.Need);
                case QuestAsk.FullHealth: return Clock(r.Need) + " at full health";
                default: return null;
            }
        }

        /// <summary>What each rule does to the advice, in words, for the log's "[quest] ... -> ..." line.</summary>
        public static string Does(QuestRule r)
        {
            switch (r.Ask)
            {
                case QuestAsk.WeaponLine: return r.Class == null ? "lifts each survivor's weapon line to its tier-3 weapon" : "lifts the " + r.Class + "'s weapon line to " + ToLevel(r, Goal(r)) + (r.AllBranches ? "" : ", AVOID on the other branches");
                case QuestAsk.Ability: return "lifts " + Or(r.Targets) + " to " + (r.Level >= 4 || r.Level <= 0 ? "max level" : "level " + r.Level);
                case QuestAsk.Evolution: return "lifts " + (r.Targets.Count > 0 ? r.Targets[0] : "the ability") + " and its evolutions";
                case QuestAsk.NoClassAbility: return "AVOID on every " + r.Class + " ability";
                case QuestAsk.NoTier3: return "AVOID on every tier-3 weapon";
                case QuestAsk.FullClass: return "lifts every " + r.Class + " powerup a little, the abilities the build skips back in";
                case QuestAsk.Synergies: return "lifts recruits and abilities with a synergy with " + r.Class;
                case QuestAsk.HealthItems: return "+" + HealthLift.ToString("0.0", IC) + " on health items";
                case QuestAsk.Items: return "+3 on the quest's items (the game's own target test)";
                case QuestAsk.Kills: return "a modest lift on the " + r.Class + "'s damage cards, never over the build's core";
                case QuestAsk.TagPoints: return r.Below ? "AVOID on picks that pass the cap, the types' levels a little lower" : "lifts Research Pods of " + And(r.Targets) + ", levels dealing it a little";
                case QuestAsk.TypesAt10: return "lifts Research Pods that bring a type to 10 tags";
                case QuestAsk.FillSlots: return "items a little up (no skipped chest)";
                case QuestAsk.NoItems: return "AVOID on every item";
                case QuestAsk.Evolutions: return "a modest lift on evolutions and abilities toward one";
                case QuestAsk.RareTraining: return "lifts Rare and Legendary trainings";
                case QuestAsk.Armor: return "lifts armor trainings, armor items a little";
                case QuestAsk.FullHealth: return "a modest lift on health, armor and healing";
                default: return "advice unchanged";
            }
        }

        /// <summary>The rules as they stand, for the log: the open ones with what they do, the ones met or out of reach.</summary>
        public string Said()
        {
            var parts = new List<string>();
            foreach (var r in List)
            {
                string t = RowText(r) ?? r.Ask.ToString();
                if (r.Met) parts.Add(t + " met");
                else if (r.Unreachable != null) parts.Add(t + " out of reach (" + r.Unreachable + ")");
                else parts.Add(t + ": " + Does(r));
            }
            string gate = Steer == QuestSteer.Off ? " - not followed ([Advice] QuestSteer Off)" : AnyOf ? " - not followed (objectives match Any)" : Failed ? " - not followed (the game counts the quest as failed)"
                : NotThisRun != null ? " - not followed (" + NotThisRun + ")" : Steer == QuestSteer.InfoOnly ? " (info only: no card moves)" : "";
            return (parts.Count == 0 ? "no rule for the cards" : string.Join("; ", parts)) + gate;
        }

        /// <summary>What a change worth a log line is: the rules' states and the counts that move with a pick, not every kill.</summary>
        public string Key()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append((int)Steer).Append(AnyOf ? 'a' : '-').Append(Failed ? 'f' : '-').Append(NotThisRun == null ? '-' : 'n');
            foreach (var r in List)
            {
                sb.Append('|').Append((int)r.Ask).Append(r.Met ? 'm' : '-').Append(r.Unreachable == null ? '-' : 'u');
                if (r.Ask == QuestAsk.WeaponLine || r.Ask == QuestAsk.Ability) sb.Append(r.From).Append(r.FromLevel);
                if (r.Ask == QuestAsk.HealthItems || r.Ask == QuestAsk.Synergies || r.Ask == QuestAsk.FullClass || r.Ask == QuestAsk.Evolutions
                    || r.Ask == QuestAsk.RareTraining || r.Ask == QuestAsk.TypesAt10 || r.Ask == QuestAsk.FillSlots || r.Ask == QuestAsk.Items || (r.Ask == QuestAsk.WeaponLine && r.Class == null))
                    sb.Append(N(r.Have));
            }
            return sb.ToString();
        }
    }
}
