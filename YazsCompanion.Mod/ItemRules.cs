// Item scoring. Pure C# (no game types): the game-only parts (quest target, already held) live in
// Ranker.ItemScore, and tools/ItemBench runs this file offline over every item of the game.
//
// An item is worth: its guide tier, plus how well its effects fit THIS squad right now, on a few axes -
//   damage types     weighted by the share of the squad's damage that carries the type (TagProfile, exact),
//   powerup tags     turret / melee / taunt / deployable / grenade: does the squad OWN such a powerup (exact),
//   weapon traits    short / long range, magazines (from the weapons' own range modifiers and clip sizes),
//   the run clock    economy (XP, luck, pickup range, chest and upgrade quality) pays back over the REST of the
//                    run: x1.5 at the start, x0.3 near the end, never fading in the open-ended modes; cash only
//                    matters to the Training Yard; survival weighs more as the horde grows and nothing in One Hit;
//                    boss damage is doubled in Boss Rush,
//   tag points       items that add or reshuffle tag points are judged from the run's live points (Ultra Instinct
//                    needs a tag at 30, a +4 that reaches the special at 10 is worth more than one that does not),
//   what is held     pairs that the items themselves name (Accumulator and the magnet items, Golden Key and
//                    Silver Padlock, the Parca set),
//   the builds       what the player's selected builds say they want.
// Where the game's own data is missing (the bench, an unknown item) the old keyword and class tables still answer.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace YazsCompanion
{
    internal sealed class ItemRule
    {
        public string Tag;
        public Regex Re;
        public string Type;                      // damage type tag this keyword is about (TagProfile.Names), or null
        public string PowerTag;                  // powerup tag that answers it exactly (Turret, Melee, Taunt, Deployable, Grenade), or null
        public string[] Stats;                   // highlighted statistics that also trigger it (substring match), or null
        public string Axis;                      // economy | cash | survival | control | boss | null: which run-clock multiplier applies
        public Dictionary<string, double> Cls;   // class display name -> weight (fallback when the exact answer is unknown)
        public bool Exclusive;
        public double Generic, Ability, Weapon, Survival;

        public ItemRule(string tag, string pattern, string cls = null, bool exclusive = false, double generic = 0, double ability = 0, double weapon = 0, double survival = 0,
            string type = null, string powerTag = null, string stats = null, string axis = null)
        {
            Tag = tag; Re = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled); Type = type; PowerTag = powerTag; Axis = axis;
            Exclusive = exclusive; Generic = generic; Ability = ability; Weapon = weapon; Survival = survival;
            if (stats != null) Stats = stats.Split(',');
            if (cls != null)
            {
                Cls = new Dictionary<string, double>();
                foreach (var part in cls.Split(','))
                {
                    var kv = part.Trim().Split(':');
                    Cls[kv[0]] = double.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }
    }

    /// <summary>Everything the item rules may know about the squad and the run. Only Squad is required.</summary>
    internal sealed class ItemContext
    {
        public IList<string> Squad = new List<string>();
        public TagProfile Tags;
        public Knowledge K;
        public RunContext Ctx;
        public HashSet<string> OwnedTags;        // powerup tags of the owned weapons and abilities; null = unknown
        public HashSet<string> Held;             // names of the items held; null = unknown
        public IList<string> Stats;              // the item's highlighted statistics; null = unknown
        public bool Healing;                     // the game flags it as a healing item
        public double CloseShare = -1, LongShare = -1, ClipShare = -1;   // share of the squad's weapons that favour close / long range, reload a magazine; -1 = unknown
        public double AbilityLean = 0.5;         // 0 = a weapon squad .. 1 = an ability squad (owned levels)
        public readonly List<KeyValuePair<string, string>> Wants = new List<KeyValuePair<string, string>>();   // (build name, leaning) of the selected builds on the squad
        public bool ShieldOwned;                 // Energy Shield is up (Glass Cannon)
        public bool CritSquad;
    }

    internal static class ItemRules
    {
        // a clause that takes something away: "-10% Kinetic damage", "Decreases your Max HP", "Halves your Max HP"
        public static readonly Regex Negative = new Regex(@"(^|[^+\d])-\s?\d+(\.\d+)?\s?%|\bhalves\b|\bhalved\b|decreases your|reduces your|\bcannot\b|no longer|\blose\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // ... unless what it takes away is the enemy's: "Enemy projectiles deal -50% damage to Survivors"
        static readonly Regex NegativeButGood = new Regex(@"enem\w+ .*-\s?\d+\s?% damage|damage (received|taken) .*-\s?\d+|-\s?\d+\s?% damage (received|taken)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static readonly Regex RichTag = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        // "+4 to Explosive damage type tag", "+2 Electric & Chemical damage type tags", "+2 Fire, Ice, Electric, Chemical damage type tags"
        static readonly Regex TagGrant = new Regex(@"\+\s?(\d+)\s+(?:to\s+)?((?:(?:Fire|Ice|Electric|Chemical|Explosive|Kinetic|Slashing)(?:\s*(?:,|&|and)\s*)?)+)\s*damage type tags?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex TypeName = new Regex(@"Fire|Ice|Electric|Chemical|Explosive|Kinetic|Slashing", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly ItemRule[] All =
        {
            new ItemRule("fire", @"\bburn|\bfire\b|flame|hellfire|ignit", "Pyro:1,SWAT:.3,Tank:.3,Engineer:.3,Huntress:.3,Ghost:.3,Mechanic:.3", type: "Fire", stats: "HashtagFire"),
            new ItemRule("electric", @"electri|\bshock|lightning", "Engineer:1,Tank:.3,Ghost:.3,Ranger:.3,Pyro:.3,Mechanic:.3", type: "Electric", stats: "HashtagElectric"),
            new ItemRule("ice", @"freez|frost|frozen|\bice\b", "Mechanic:1,Huntress:.8,Medic:.5,Engineer:.3,Ghost:.3,Ranger:.3", type: "Ice", stats: "HashtagIce"),
            new ItemRule("chemical", @"toxi|poison|chemical|acid|plague", "Medic:1,Huntress:.6,Tank:.3,Ghost:.3,Pyro:.3", type: "Chemical", stats: "HashtagToxic"),
            new ItemRule("explosive", @"explos|\bbomb|\bmine\b|implosion", "SWAT:1,Tank:1,Pyro:.5,Medic:.4,Engineer:.4", type: "Explosive", stats: "HashtagExplosive"),
            new ItemRule("slashing", @"bleed|slashing", "Ghost:1,Mechanic:.5,Huntress:.4,Pyro:.3", type: "Slashing", stats: "HashtagSlashing"),
            new ItemRule("kinetic", @"kinetic|injured", "SWAT:.8,Tank:.8,Huntress:.6,Ranger:.6,Medic:.4,Engineer:.3", type: "Kinetic", stats: "HashtagKinetic"),
            new ItemRule("slow", @"\bslow(?!er)|movement impair", "Mechanic:1,Huntress:.8,Medic:.5,Engineer:.3", axis: "control"),
            new ItemRule("marked", @"\bmark", "Ranger:1,Huntress:.3", exclusive: true),
            new ItemRule("critical", @"critical", "Huntress:1,Ghost:.6,Ranger:.6,SWAT:.4", stats: "CritChance,CritDamage"),
            new ItemRule("grenade", @"grenade", "SWAT:1,Medic:.5,Pyro:.5,Engineer:.4,Huntress:.4", powerTag: "Grenade"),
            new ItemRule("turret", @"turret", "Engineer:1,SWAT:.6,Mechanic:.6", exclusive: true, powerTag: "Turret"),
            new ItemRule("melee", @"melee", "Ghost:1,Mechanic:.8,Pyro:.5,Tank:.3", exclusive: true, powerTag: "Melee"),
            new ItemRule("taunt", @"taunt|\bfear", "Huntress:.6,Tank:.5,Mechanic:.4,Ghost:.4", powerTag: "Taunt", axis: "control"),
            new ItemRule("deployable", @"deployable|\btrap", "Mechanic:.8,Engineer:.6,Huntress:.6,Tank:.4", powerTag: "Deployable"),
            new ItemRule("dodge", @"dodge|parry", "Ghost:.8,SWAT:.3", stats: "DodgeChance"),
            new ItemRule("armor", @"armor", "Tank:.8", survival: .5, stats: "TeamArmor", axis: "survival"),
            new ItemRule("healing", @"\bheal(?!th)|healthpa|max health|max hp|\bhp\b|regenerat", "Medic:.8", survival: .8, stats: "MaxHealth,HealthRegen,HealthBonuses", axis: "survival"),
            new ItemRule("status effects", @"status effect|\bstun", "Pyro:.4,Engineer:.4,Medic:.4,Huntress:.4,Mechanic:.4,Ghost:.3"),
            new ItemRule("weapons", @"weapon|reload|\bclip|bullet|(?<!enemy )projectile(?! attacks)", weapon: .6, generic: .2, stats: "WeaponDamage,WeaponCDRed,WeaponFireRate"),
            new ItemRule("abilities", @"abilit|(?<!weapon )(?<!\ds )cooldown", ability: .6, generic: .2, stats: "AbilityDamage,AbilityCDRed,AbilitySize,AbilityDuration,Multicast"),
            new ItemRule("economy", @"\bxp\b|experience|specializ|\bluck|level-up", generic: .5, stats: "XPMultiplier,TeamLuck", axis: "economy"),
            new ItemRule("cash", @"\bcash|money", generic: .4, stats: "MoneyMultiplier", axis: "cash"),
            new ItemRule("elites/bosses", @"\belite|\bboss", generic: .6, axis: "boss"),
            new ItemRule("pickups", @"magnet|pickup|pick-up|collect", generic: .4, stats: "MagnetRange", axis: "economy"),
            new ItemRule("upgrade quality", @"military training|lockdown|reroll|banish|\bskip|rarity|quality|item chest", generic: .3, stats: "NumRerolls,NumBanishes", axis: "economy"),
            new ItemRule("damage", @"(?<!receive )(?<!receives )(?<!receiving )(?<!take )\bdamage\b(?! type tag)(?! to survivors)", generic: .3),
        };

        static readonly string[] Elemental = { "Pyro", "Engineer", "Medic", "Huntress", "Mechanic" };

        /// <summary>The bench's and the old callers' entry: squad, tags and knowledge only.</summary>
        public static double Evaluate(string name, string description, IList<string> squad, TagProfile tags, Knowledge k, List<string> why)
        {
            var c = new ItemContext { Squad = squad, Tags = tags, K = k };
            c.CritSquad = squad.Any(x => k.CritSquad.Contains(x, StringComparer.OrdinalIgnoreCase));
            c.ShieldOwned = squad.Contains("Engineer", StringComparer.OrdinalIgnoreCase);
            return Evaluate(name, description, c, why);
        }

        /// <summary>1 + guide tier + 0.6 x fit, with the caveats an item's own text names. The headline reason comes first.</summary>
        public static double Evaluate(string name, string description, ItemContext c, List<string> why)
        {
            var k = c.K ?? Knowledge.Current;
            var fitWhy = new List<string>();
            bool typed, fits, economic;
            double fit = Score(description, c, fitWhy, out typed, out fits, out economic);
            string tier; k.ItemTier.TryGetValue(name ?? "", out tier);
            double tierScore = Knowledge.Tier(tier, 3.0, 2.0, 1.0, -1.5);
            string note; k.ItemNote.TryGetValue(name ?? "", out note);

            // an item that is ABOUT a damage type the squad does not deal keeps little of its tier
            if (typed && !fits && tierScore > 0) { tierScore *= 0.3; note = null; }
            // an item that is only about the economy (XP, luck, pickup range, chest quality) is tiered for a whole run:
            // its tier follows the clock too, or a pickup-range item would still be "A" with ninety seconds left
            if (economic && tierScore > 0 && c.Ctx != null) tierScore *= Math.Max(0.3, Math.Min(1.15, 0.3 + 0.7 * c.Ctx.Economy));

            if (Is(name, "Silencer")) Ranged(c.CloseShare, "short", c, ref tierScore, ref note);
            else if (Is(name, "Dartboard")) Ranged(c.LongShare, "long", c, ref tierScore, ref note);
            else if (Is(name, "Magazine Clip") || Is(name, "Last Round"))
            {
                if (c.ClipShare >= 0) { tierScore += 1.2 * c.ClipShare - 0.4; note = c.ClipShare > 0 ? "the squad reloads magazines" : "no weapon on the squad uses a magazine"; }
            }
            else if (Is(name, "Glass Cannon"))
            {
                tierScore = c.ShieldOwned ? 1.5 : -2.0;
                note = c.ShieldOwned ? "viable behind the Energy Shield" : "no Energy Shield: double damage taken, less max HP";
            }

            double score = 1.0 + tierScore + fit * 0.6;
            score += TagPoints(name, description, c, fitWhy);
            score += Pairs(name, c, fitWhy);
            score += Scaling(name, c, fitWhy);

            if (tier != null) why.Add(tier + "-tier item" + (note != null ? ", " + note : ""));
            else if (note != null) why.Add(note);
            why.AddRange(fitWhy);
            return score;
        }

        static bool Is(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

        static void Ranged(double share, string what, ItemContext c, ref double tierScore, ref string note)
        {
            if (share < 0) return;      // unknown: the tier stands
            tierScore = tierScore * (0.25 + 0.75 * share) - (share <= 0 ? 0.8 : 0);
            note = share <= 0 ? "no " + what + "-range weapon on the squad" : (int)Math.Round(share * 100) + "% of the squad's weapons are " + what + "-range";
        }

        /// <summary>Score an item description against the squad and the run. <paramref name="typed"/>: the item is about a
        /// damage type; <paramref name="fits"/>: the squad deals one of them.</summary>
        public static double Score(string description, ItemContext c, List<string> why, out bool typed, out bool fits, out bool economic)
        {
            typed = false; fits = false; economic = false;
            int economyRules = 0, otherRules = 0;
            string text = RichTag.Replace(description ?? "", "");
            double score = 0;
            var clauses = new List<string>();
            foreach (var cl in Regex.Split(text, @"(?<=[.!])\s+|\s(?=-\s?\d+\s?%)")) if (!string.IsNullOrWhiteSpace(cl)) clauses.Add(cl);
            var posParts = new List<string>(); var negParts = new List<string>();
            foreach (var cl in clauses) ((Negative.IsMatch(cl) && !NegativeButGood.IsMatch(cl)) ? negParts : posParts).Add(cl);
            string pos = string.Join(" ", posParts);       // every clause a malus: nothing positive to find (not "the whole text")
            string neg = string.Join(" ", negParts);
            var squad = c.Squad ?? new List<string>();
            bool hasElemental = false; foreach (var s in squad) if (Array.IndexOf(Elemental, s) >= 0) hasElemental = true;
            bool exact = c.Tags != null && c.Tags.Known;
            var ctx = c.Ctx;
            bool wanted = false;

            foreach (var kw in All)
            {
                bool inPos = kw.Re.IsMatch(pos) || StatHit(kw, c.Stats, neg.Length == 0), inNeg = !inPos && kw.Re.IsMatch(neg);
                if (!inPos && !inNeg) continue;
                double axis = AxisOf(kw.Axis, ctx);
                if (inPos) { if (kw.Axis == "economy" || kw.Axis == "cash") economyRules++; else if (kw.Tag != "damage") otherRules++; }
                if (kw.Cls != null)
                {
                    double best = 0; string who = null;
                    if (kw.Type != null) typed = true;
                    if (kw.Type != null && exact)
                    {
                        best = c.Tags.Fit(kw.Type);
                        if (best > 0) { who = c.Tags.SourceText(kw.Type); if (who.Length == 0) who = "the squad"; fits = true; }
                    }
                    else if (kw.PowerTag != null && c.OwnedTags != null)
                    {
                        if (c.OwnedTags.Contains(kw.PowerTag)) { best = 1; who = "the squad owns a " + kw.PowerTag.ToLowerInvariant(); }
                    }
                    else foreach (var s in squad) { double v; if (kw.Cls.TryGetValue(s, out v) && v > best) { best = v; who = s; } }
                    if (kw.Type != null && !exact && best > 0) fits = true;
                    if (inNeg) { if (best > 0) { score -= 0.7 * best; why.Add("hurts " + who + ": " + kw.Tag); } continue; }
                    if (best > 0) { score += best * axis; why.Add(who + ": " + kw.Tag); }
                    else if (kw.Exclusive || (kw.PowerTag != null && c.OwnedTags != null && kw.Generic <= 0 && kw.Survival <= 0))
                    {
                        var needs = new List<string>(); foreach (var kv in kw.Cls) if (kv.Value >= 1) needs.Add(kv.Key);
                        score -= kw.Exclusive ? 1 : 0.4; why.Add(kw.PowerTag != null && c.OwnedTags != null ? "no " + kw.PowerTag.ToLowerInvariant() + " on the squad" : "needs " + string.Join("/", needs));
                    }
                    else if (kw.Type != null && exact) why.Add("no " + kw.Type + " damage on the squad");
                    else if (kw.Tag == "status effects" && hasElemental) score += 0.3;
                }
                if (inNeg) { if (kw.Cls == null && kw.Generic > 0) { score -= 0.5 * kw.Generic; why.Add("costs " + kw.Tag); } continue; }
                if (kw.Generic > 0)
                {
                    score += kw.Generic * axis;
                    if (kw.Cls == null) why.Add(kw.Tag + AxisNote(kw.Axis, axis, ctx));
                }
                if (kw.Survival > 0 && ctx != null)
                {
                    score += kw.Survival * (ctx.Survival - 0.4);       // a squad that is fine gains little from more health; one that is hurting, late, a lot
                    if (ctx.Survival <= 0) why.Add("health means nothing in One Hit");
                    else if (ctx.Survival >= 1.3) why.Add("survival matters now" + (ctx.Health < 0.5 ? " (the squad is hurting)" : ""));
                }
                if (kw.Ability > 0) score += kw.Ability * (c.AbilityLean - 0.5) * 2 * 0.5;
                if (kw.Weapon > 0) score += kw.Weapon * (0.5 - c.AbilityLean) * 2 * 0.5;
                if (!wanted)
                    foreach (var w in c.Wants)
                        if (Is(w.Value, kw.Tag) || (kw.Type != null && Is(w.Value, kw.Type))) { score += 0.5; why.Add("your " + w.Key + " build wants " + kw.Tag); wanted = true; break; }
            }
            if (c.Healing && ctx != null && ctx.Survival <= 0) { score -= 1.0; }
            economic = economyRules > 0 && otherRules == 0;
            return score;
        }

        /// <summary>Old entry (bench).</summary>
        public static double Score(string description, IList<string> squad, TagProfile tags, List<string> why)
        {
            bool a, b, e; return Score(description, new ItemContext { Squad = squad, Tags = tags }, why, out a, out b, out e);
        }

        static bool StatHit(ItemRule kw, IList<string> stats, bool noMalus)
        {
            if (kw.Stats == null || stats == null || !noMalus) return false;      // with a malus clause around, the statistics cannot say which side they are on
            foreach (var st in stats) foreach (var key in kw.Stats) if (st.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static double AxisOf(string axis, RunContext ctx)
        {
            if (ctx == null || axis == null) return 1;
            switch (axis)
            {
                case "economy": return ctx.Economy;
                case "cash": return ctx.Cash;
                case "survival": return Math.Max(0, ctx.Survival);
                case "control": return ctx.Control;
                case "boss": return ctx.Boss;
                default: return 1;
            }
        }

        static string AxisNote(string axis, double value, RunContext ctx)
        {
            if (ctx == null || axis == null) return "";
            if (axis == "economy") return value >= 1.25 ? ": pays back all run (" + ctx.ClockText + ")" : value <= 0.5 ? ": too late to pay back (" + ctx.ClockText + ")" : "";
            if (axis == "cash") return value <= 0.3 ? ": Training Yard cash only" : "";
            if (axis == "boss") return value >= 1.5 ? ": this mode is about the boss" : "";
            return "";
        }

        /// <summary>Items that grant or reshuffle tag points, judged from the run's live points.</summary>
        static double TagPoints(string name, string description, ItemContext c, List<string> why)
        {
            var tags = c.Tags;
            if (tags == null) return 0;
            string text = RichTag.Replace(description ?? "", "");
            int top = 0; string topType = null; int withPoints = 0;
            foreach (var t in TagProfile.Names) { int n = tags.PointsOf(t); if (n > 0) withPoints++; if (n > top) { top = n; topType = t; } }

            if (Is(name, "Ultra Instinct"))
            {
                if (top >= 30) { why.Insert(0, topType + " is at " + top + ": every other tag point pours into it"); return 3.0; }
                if (top >= 22) { why.Add(topType + " " + top + "/30: close to switching it on"); return 0.8; }
                why.Add("does nothing until a tag reaches 30 (best: " + (topType ?? "none") + " " + top + ")"); return -0.8;
            }
            if (Is(name, "One For All"))
            {
                if (top >= 10 && withPoints <= 3) { why.Add("would flatten your " + topType + " stack of " + top); return -1.5; }
                if (withPoints >= 4) { why.Add("evens out " + withPoints + " tag types"); return 0.6; }
                return 0;
            }

            double v = 0;
            foreach (Match m in TagGrant.Matches(text))
            {
                int n; if (!int.TryParse(m.Groups[1].Value, out n)) continue;
                foreach (Match tm in TypeName.Matches(m.Groups[2].Value))
                {
                    string type = Canon(tm.Value);
                    double fit = tags.Known ? tags.Fit(type) : 0.4;
                    int cur = tags.PointsOf(type);
                    v += 0.25 * n * fit;
                    if (tags.SpecialAt > 0 && cur < tags.SpecialAt && cur + n >= tags.SpecialAt && fit > 0)
                    {
                        v += 1.2; why.Insert(0, "+" + n + " " + type + " reaches the special (" + tags.SpecialAt + ")");
                    }
                }
            }
            return Math.Min(2.5, v);
        }

        static string Canon(string type)
        {
            foreach (var n in TagProfile.Names) if (Is(n, type)) return n;
            return type;
        }

        /// <summary>Pairs the items themselves name: worth more when the other half is already held.</summary>
        static double Pairs(string name, ItemContext c, List<string> why)
        {
            if (c.Held == null || c.K == null || name == null) return 0;
            double v = 0;
            foreach (var pair in c.K.ItemPairs)
            {
                string other = Is(pair.Key, name) ? pair.Value : Is(pair.Value, name) ? pair.Key : null;
                if (other == null || !c.Held.Contains(other)) continue;
                v += 0.9; why.Insert(0, "pairs with your " + other);
            }
            return Math.Min(1.8, v);
        }

        /// <summary>Items that grow over the run (a stack per kill, per chest, per second): early or not at all.</summary>
        static double Scaling(string name, ItemContext c, List<string> why)
        {
            if (c.Ctx == null || c.K == null || name == null || !c.K.ScalingItems.Contains(name)) return 0;
            double e = c.Ctx.Economy;
            if (e >= 1.25) { why.Add("grows all run: take it early"); return 0.7; }
            if (e <= 0.5) { why.Add("grows over time: too late to matter (" + c.Ctx.ClockText + ")"); return -0.9; }
            return 0.2 * (e - 1);
        }
    }
}
