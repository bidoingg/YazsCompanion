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
// 0.16.0 (C16-02): every chest item of game 1.0.2 is read by its rule in the item book (ItemBook.cs: what it gives and takes as
// the tags below, what it needs, its run curve, its worth when no guide tiers it, its words) instead of the keyword patterns; an
// item the book does not name keeps the keyword reading, and [Debug] ItemKeywords (UseBook = false) brings it back for every item.
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
            Tag = tag; Re = new Regex(pattern, RegexOptions.IgnoreCase); Type = type; PowerTag = powerTag; Axis = axis;
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
        public readonly List<KeyValuePair<string, string>> Wants = new List<KeyValuePair<string, string>>();   // (how a reason names the build: "your Rifleman build", or "Rifleman (Auto)" for a lent build Auto follows; a leaning) of the builds the squad follows
        public bool ShieldOwned;                 // Energy Shield is up (Glass Cannon)
        public bool CritSquad;
        public ItemSay Say;                      // 0.14.0 (A1): set for a chest card - what the rules reason, for the line under the card (Wording.Item)
        // 0.16.0 (C16-02): live team statistics the book's needs read (NaN = unknown: the need leaves the worth as it is; C16-02b fills them)
        public double FreeSlots = double.NaN;    // InternalNumFreeItemSlots before the pick
        public double Luck = double.NaN;         // TeamLuck
        public double Pickup = double.NaN;       // TeamMagnetRange as a multiplier (1.0 = 100 %)
        public double MoveSpeed = double.NaN;    // movement speed points (base 100)
        public bool LockdownAdvised;             // the action hints advise a LOCKDOWN (Snapshot.LockdownAdvised: false until the LOCKDOWN rule lands)
        internal BookEntry Row;                  // the book's rule of the item being scored (set by Evaluate)
        // 0.16.0 (C16-01): what the squad holds beyond the names in Held (Snapshot.Held: slots, the join clock, the cooldowns, the pretend
        // names; null = unknown: no held-item rule applies), and of the item being scored the game's animal flag (C16-01i) and its asset
        // (a held-item rule still finds a renamed item; Ranker.ItemScore sets both per item)
        public HeldFacts HeldFacts;
        public bool Animal;
        public string Asset;
    }

    internal static class ItemRules
    {
        // a clause that takes something away: "-10% Kinetic damage", "Decreases your Max HP", "Halves your Max HP"
        public static readonly Regex Negative = new Regex(@"(^|[^+\d])-\s?\d+(\.\d+)?\s?%|\bhalves\b|\bhalved\b|decreases your|reduces your|\bcannot\b|no longer|\blose\b", RegexOptions.IgnoreCase);
        // ... unless what it takes away is the enemy's: "Enemy projectiles deal -50% damage to Survivors"
        static readonly Regex NegativeButGood = new Regex(@"enem\w+ .*-\s?\d+\s?% damage|damage (received|taken) .*-\s?\d+|-\s?\d+\s?% damage (received|taken)", RegexOptions.IgnoreCase);
        public static readonly Regex RichTag = new Regex(@"<[^>]+>");
        // "+4 to Explosive damage type tag", "+2 Electric & Chemical damage type tags", "+2 Fire, Ice, Electric, Chemical damage type tags"
        static readonly Regex TagGrant = new Regex(@"\+\s?(\d+)\s+(?:to\s+)?((?:(?:Fire|Ice|Electric|Chemical|Explosive|Kinetic|Slashing)(?:\s*(?:,|&|and)\s*)?)+)\s*damage type tags?", RegexOptions.IgnoreCase);
        static readonly Regex TypeName = new Regex(@"Fire|Ice|Electric|Chemical|Explosive|Kinetic|Slashing", RegexOptions.IgnoreCase);

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
            // what a keyword is NOT about: "2 Elite enemies or 1 Boss enemy" is a kill count (Glass of Milk is a Max HP item),
            // "Item Chests, responding to Signals, and collecting Samples" is the list of things one opens (Crowbar only
            // makes them instant), "10% of the damage done" is a measure (Vampire Survivor restores health)
            new ItemRule("elites/bosses", @"(?<!\d )\belite|(?<!\d )\bboss", generic: .6, axis: "boss"),
            new ItemRule("pickups", @"magnet|pickup|pick-up|collect(?!(ing)? samples)", generic: .4, stats: "MagnetRange", axis: "economy"),
            new ItemRule("upgrade quality", @"military training|lockdown|reroll|banish|\bskip|rarity|quality|item chest(?!s?, re)", generic: .3, stats: "NumRerolls,NumBanishes", axis: "economy"),
            new ItemRule("damage", @"(?<!receive )(?<!receives )(?<!receiving )(?<!take )(?<!% of the )\bdamage\b(?! type tag)(?! to survivors)", generic: .3),
            // 0.16.0 (C16-02): the item book's own tags - never matched by a pattern (an unknown item keeps the 0.15.0 keyword reading)
            new ItemRule("survival", @"(?!)", survival: .8, axis: "survival"),
            new ItemRule("control", @"(?!)", generic: .5, axis: "control"),
            new ItemRule("move", @"(?!)", generic: .15),
        };

        static readonly string[] Elemental = { "Pyro", "Engineer", "Medic", "Huntress", "Mechanic" };

        /// <summary>0.16.0 (C16-02): false = the 0.15.0 keyword reading for every item ([Debug] ItemKeywords = true), for comparison.
        /// Every term of the book (rows, the new grants, the book's pairs and clashes, the mixed effect) is off then.</summary>
        public static bool UseBook = true;
        static BookEntry RowOf(string name) { return UseBook ? ItemBook.Of(name) : null; }

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
            var row = RowOf(name);                           // 0.16.0 (C16-02): the book's rule replaces the keyword reading
            if (row == null && UseBook) NoRule(name);
            var keepRow = c.Row; c.Row = row;
            // a bonus per squad size (Duct Tape): only the line of the current size is scored, not the sum of the three. The
            // highlighted statistics list all three lines too, so for such an item the line's own words decide
            string sizeLine; string text = SizedText(description, c.Squad == null ? 0 : c.Squad.Count, out sizeLine);
            string noteSay = null;          // 0.14.0 (A1): a rule's note in the card's words, where they differ from the log's
            var stats = c.Stats; if (sizeLine != null || row != null) c.Stats = null;     // 0.16.0 (C16-02): a book item is read from its rule alone
            if (row != null && row.Gains == "line") c.Row = null;                         // 0.16.0 (C16-02): Duct Tape - the line's own words decide
            double fit;
            try { fit = Score(text, c, fitWhy, out typed, out fits, out economic); } finally { c.Stats = stats; c.Row = keepRow; }
            // 0.16.0 (C16-01j rule A): a status item while a conversion item is held - its fit is what still causes its status on this squad, and
            // 'fits' follows it (the 'about a type nobody deals' cut below applies once the status lost every cause, and no longer once it gained one)
            var heldWhy = new List<string>();        // the held-item reasons of C16-01i / C16-01j, put first (after the slot cost's)
            StatusConverted(name, text, row, c, fitWhy, heldWhy, ref fit, ref fits);
            string tier; k.ItemTier.TryGetValue(name ?? "", out tier);
            double tierScore = Knowledge.Tier(tier, 3.0, 2.0, 1.0, -1.5);
            if (tier == null && row != null) tierScore = row.Value;        // 0.16.0 (C16-02): the rule's worth (never shown as a tier)
            string note; k.ItemNote.TryGetValue(name ?? "", out note);
            string guideNote = note;
            if (note == null && row != null && row.Say != null) { note = row.Say; guideNote = note; }       // 0.16.0 (C16-02): the book's words, said like a guide note
            if (sizeLine != null && (note == null || row != null)) { note = "with " + c.Squad.Count + (c.Squad.Count == 1 ? " survivor: " : " survivors: ") + sizeLine; noteSay = (c.Squad.Count == 1 ? "solo: " : "with " + c.Squad.Count + " survivors: ") + sizeLine; }
            if (row != null && row.Gains != "line") economic = row.Clock.Contains("economy");

            // an item that is ABOUT a damage type the squad does not deal keeps little of its tier
            if (typed && !fits && tierScore > 0) { tierScore *= 0.3; if (row == null || TypedFull(row)) note = null; }
            // an item that is only about the economy (XP, luck, pickup range, chest quality) is tiered for a whole run:
            // its tier follows the clock too, or a pickup-range item would still be "A" with ninety seconds left
            if (economic && tierScore > 0 && c.Ctx != null) tierScore *= Math.Max(0.3, Math.Min(1.15, 0.3 + 0.7 * c.Ctx.Economy));
            if (row != null && row.Clock.Contains("cash") && tierScore > 0 && c.Ctx != null) tierScore *= Math.Min(1.2, 2 * c.Ctx.Cash);

            string need = row != null ? row.Need : null;
            if (need == null && row == null)
            {   // the 0.15.0 special cases, for a knowledge file that names them but a book that does not
                if (Is(name, "Silencer")) need = "short"; else if (Is(name, "Dartboard")) need = "long";
                else if (Is(name, "Magazine Clip") || Is(name, "Last Round")) need = "clip"; else if (Is(name, "Glass Cannon")) need = "shield";
            }
            if (need != null) noteSay = Need(need, name, c, ref tierScore, ref fit, fitWhy, ref note) ?? noteSay;
            if (row != null && c.Held != null)
                foreach (var mx in ItemBook.Mixed)
                    if (Is(mx.Offered, name) && c.Held.Contains(mx.Held))
                    { if (tierScore > 0) tierScore *= mx.Factor; fitWhy.Insert(0, mx.Lead); if (c.Say != null) c.Say.Lead = mx.Lead; }
            // a growing or economy item late in the run: the clock's words, not the item's
            if (row != null && c.Ctx != null && (row.Clock.Contains("economy") || row.Clock.Contains("grows")) && c.Ctx.Economy <= 0.5 && note == guideNote)
            { note = "too late to pay off (" + c.Ctx.ClockText + ")"; noteSay = note; }

            // 0.16.0 (C16-01k): the Mana Potion card - what it costs (the squad's ability cooldown reduction, which it switches off)
            // against what it gives (an ability reset every 4 s), from the live cooldowns; the guide tier is the anchor. Not when it is
            // held already (by name or pretend: Ranker's 'already held' says that) or the cooldowns were not read
            string[] manaLead = null; string manaLog = null;
            var held = c.HeldFacts;
            if (held != null && held.Cooldowns != null && HeldRules.IsItem(HeldRules.ManaPotion, name, c.Asset)
                && !held.Has(HeldRules.ManaPotion) && !(c.Held != null && c.Held.Contains(HeldRules.ManaPotion.Name)))
                HeldRules.ManaPotionCard(held.Cooldowns, ref tierScore, out manaLead, out manaLog);

            double score = 1.0 + tierScore + fit * 0.6;
            score += TagPoints(name, description, c, fitWhy);
            score += Pairs(name, c, fitWhy, heldWhy);
            score += Clashes(name, c, fitWhy);
            score += StatusCard(name, c, heldWhy);   // 0.16.0 (C16-01j rule B): a conversion item against the status items held
            score += Scaling(name, c, fitWhy);

            // 0.16.0 (C16-01a): Mana Potion held - an item whose worth is partly an ability cooldown reduction bonus keeps the rest
            // (HeldRules.CdrItem with its item book row's share: Chick Magnet, Hyperactivity, Devil's Deal)
            string[] cdrLead = null; string cdrWhy = null;
            if (held != null) score = HeldRules.CdrItem(held, name, c.Asset, score, out cdrLead, out cdrWhy);
            // 0.16.0 (C16-01c): an item that fills an empty slot while Wooden Stick / Empty Chest are held (HeldRules.SlotCost); the card
            // line names the cost only when it turns the card AVOID, else it is a WHY reason
            string[] slotSay = null; double merit = score, slotCost = 0;
            if (held != null) slotCost = HeldRules.SlotCost(c, name, out slotSay, out _);
            if (slotCost > 0) { score -= slotCost; if (c.Say != null) c.Say.HeldCost = slotCost; }

            if (tier != null) why.Add(tier + "-tier item" + (note != null ? ", " + note : ""));
            else if (note != null) why.Add(note);
            why.AddRange(fitWhy);
            // the held-item reasons go first ('held: ' / 'item: ', WhyText lists them first too); a WHY-only reason and the Mana Potion
            // card's numbers (no prefix: they stay in the log) after the rest
            why.InsertRange(0, heldWhy);
            if (slotCost > 0 && slotSay != null && slotSay.Length > 0)
            {
                why.Insert(0, HeldRules.Prefix + slotSay[0]);
                if (c.Say != null && merit >= 1 && score < 1) c.Say.LeadForms = slotSay;
            }
            else if (slotSay != null && slotSay.Length > 0)
            {   // C16-01c: Well Prepped while Wooden Stick / Empty Chest are held - it adds the slot it takes (no cost, its words)
                why.Insert(0, HeldRules.Prefix + slotSay[0]);
                if (c.Say != null) c.Say.LeadForms = slotSay;
            }
            if (cdrWhy != null)
            {
                if (cdrLead != null) { why.Insert(0, cdrWhy); if (c.Say != null) c.Say.LeadForms = cdrLead; }
                else why.Add(cdrWhy);
            }
            if (manaLead != null) { why.Insert(0, HeldRules.ItemPrefix + manaLead[0]); if (c.Say != null) c.Say.LeadForms = manaLead; }
            if (manaLog != null) why.Add(manaLog);
            if (c.Say != null) { c.Say.Tier = tier; c.Say.Note = note; c.Say.RuleNote = note != null && note != guideNote; c.Say.NoteSay = c.Say.RuleNote ? noteSay : null; if (c.Ctx != null) c.Say.Clock = c.Ctx.ClockText; }
            return score;
        }

        static bool Is(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

        // 0.16.0 (C16-02): an item the book does not name (a newer game build's) keeps the keyword reading - said once per name and
        // session; the game's unlocalized 'Powerups/...' duplicates are never offered and stay quiet
        static readonly HashSet<string> _noRule = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static void NoRule(string name)
        {
            if (string.IsNullOrEmpty(name) || name.StartsWith("Powerups/", StringComparison.Ordinal) || !_noRule.Add(name)) return;
            try { Plugin.Logger.LogInfo("[items] no rule for '" + name + "' - keyword reading"); } catch { }
        }

        // 0.16.0 (C16-02): the row gains a damage type at full weight - only then is "nobody on the squad deals X" drawn instead of its Say
        static bool TypedFull(BookEntry row) { if (row.GainW == null) return false; for (int i = 0; i < All.Length; i++) if (All[i].Type != null && row.GainW[i] >= 1) return true; return false; }

        // 0.16.0 (C16-12, DK-C01): the revive need - a second life ranks at least like a guide A-tier pick (Knowledge.Tier("A", 3, 2, 1,
        // -1.5) = 2.0), and higher while the quest asks to survive the run or the squad (the leader's bar) is under 60 % health
        public const double ReviveFloor = 2.0, ReviveLift = 0.5, ReviveHurt = 0.6;

        /// <summary>0.16.0 (C16-02): what an item needs to work, against this squad (the item book's Need). Returns the note in the
        /// card's words (null: none).</summary>
        static string Need(string need, string name, ItemContext c, ref double tierScore, ref double fit, List<string> fitWhy, ref string note)
        {
            string kind = need, arg = null; int colon = need.IndexOf(':');
            if (colon > 0) { kind = need.Substring(0, colon); arg = need.Substring(colon + 1); }
            var ctx = c.Ctx; var tags = c.Tags;
            switch (kind)
            {
                case "short": return Ranged(c.CloseShare, "short", c, ref tierScore, ref note);
                case "long": return Ranged(c.LongShare, "long", c, ref tierScore, ref note);
                case "clip":
                    {
                        double f = 1; if (arg != null) double.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f);
                        if (c.ClipShare > 0) { tierScore += f * (1.2 * c.ClipShare - 0.4); note = "the squad reloads magazines"; }
                        else if (c.ClipShare == 0)
                        {
                            if (tierScore > 0) tierScore *= 1 - 0.8 * f;
                            if (f >= 1) { fit = 0; fitWhy.Clear(); }
                            note = "no weapon on the squad uses a magazine";
                        }
                        return null;
                    }
                case "shield":
                    tierScore = c.ShieldOwned ? 1.5 : -2.0;
                    note = c.ShieldOwned ? "viable behind the Energy Shield" : "no Energy Shield: double damage taken, less max HP";
                    return null;
                case "own":
                    {   // the item deals its own damage of that type: the squad's tags of the type raise it, nothing on the squad has to deal it
                        int pts = tags == null ? 0 : tags.PointsOf(arg);
                        double ft = (tags != null && (tags.Known || pts > 0)) ? Math.Min(1.0, pts / (double)Math.Max(1, tags.SpecialAt)) : 0.3;
                        tierScore += 0.5 * ft;
                        if (pts >= 3) fitWhy.Add("your " + pts + " " + arg + " tags raise its damage");
                        return null;
                    }
                case "status3":
                    {
                        if (tags == null || !tags.Known) return null;
                        int kinds = StatusKinds(c);
                        double m = kinds >= 3 ? 1.0 : kinds == 2 ? 0.6 : 0.25;
                        if (tierScore > 0) tierScore *= m;
                        if (kinds < 3) { note = "the squad applies " + kinds + " of the 3 status effects it needs"; if (c.Say != null) c.Say.Lead = "needs 3 status effects, the squad has " + kinds; return null; }
                        return null;
                    }
                case "slots":
                    {
                        if (double.IsNaN(c.FreeSlots)) return null;
                        int after = Math.Max(0, (int)c.FreeSlots - 1);       // taking it fills a slot
                        if (arg == "damage")
                        {
                            tierScore = Math.Max(-0.3, Math.Min(2.1, 0.3 * after));
                            note = after == 0 ? "no item slot left empty after it" : "+" + (after * 10) + "% damage now (" + after + " empty slots)";
                            return note;
                        }
                        if (tierScore > 0) tierScore *= Math.Max(0.1, Math.Min(1.4, after / 3.0));
                        note = after == 0 ? "+10% XP now, no slot left empty" : "+" + (10 + after * 10) + "% XP now (" + after + " empty slots)";
                        return note;
                    }
                case "luck":
                    if (double.IsNaN(c.Luck)) return null;
                    tierScore = Math.Max(0, Math.Min(2.2, 0.03 * c.Luck));
                    note = c.Luck < 1 ? "no luck yet to turn into damage" : "+" + (int)Math.Round(c.Luck) + "% damage at your luck"; return note;
                case "pickup":
                    {
                        if (double.IsNaN(c.Pickup)) return null;
                        int bonus = (int)Math.Round(Math.Max(0, c.Pickup - 1) * 100 / 10 * 5);
                        tierScore += 0.03 * bonus;
                        if (bonus > 0) { note = "+" + bonus + (Is(name, "Wrench") ? "% area" : "% duration") + " from your pickup range"; return note; }
                        return null;
                    }
                case "move":
                    {
                        if (double.IsNaN(c.MoveSpeed)) return null;
                        double over = Math.Max(0, c.MoveSpeed + (arg == "Chemical" ? 20 : 10) - 100);
                        int bonus = (int)Math.Round(arg == "Chemical" ? over / 2 : over / 5);
                        if (arg == "Chemical" && tags != null && tags.Known && tags.Fit("Chemical") <= 0) bonus = 0;
                        tierScore += 0.03 * bonus;
                        if (bonus > 0) { note = "+" + bonus + "% " + (arg == "Chemical" ? "Chemical damage" : "ability power") + " from your speed"; return note; }
                        return null;
                    }
                case "gems":
                    {
                        if (tags == null || tags.Points.Count == 0) return null;
                        // a type the squad deals reaches 3 within a few levels: early in the run those count already (the gem pays all run)
                        bool early = ctx == null || ctx.Progress < 0.5;
                        int n3 = TagProfile.Names.Count(t => tags.PointsOf(t) >= 3 || (early && tags.Known && tags.Share(t) >= 0.1));
                        if (tierScore > 0) tierScore *= Math.Max(0.4, Math.Min(1.6, 0.4 + 0.2 * n3));
                        note = n3 + (n3 == 1 ? " damage type" : " damage types") + (early ? " at 3 tags or on the way" : " at 3 tags or more"); return note;
                    }
                case "squad":
                    {
                        int size = c.Squad == null ? 1 : c.Squad.Count;
                        if (Is(name, "Walkie Talkie")) { if (size >= 3) { tierScore *= 0.5; note = "halved in a full squad"; return note; } return null; }
                        tierScore += (Is(name, "Farming Tools") ? 0.1 : 0.15) * (size - 1);
                        return null;
                    }
                case "solo":
                    if (c.Squad != null && c.Squad.Count == 1) { tierScore += 0.5; note = "solo: its healing doubles"; return note; }
                    return null;
                case "power":
                    // the item works through a powerup tag (taunts): with none on the squad it keeps little of its worth, like a type nobody deals
                    if (c.OwnedTags != null && !c.OwnedTags.Contains(arg) && tierScore > 0) tierScore *= 0.3;
                    return null;
                case "lockdown":
                    if (!c.LockdownAdvised && tierScore > 0) tierScore *= 0.5;
                    return null;
                case "skip":
                    if (tierScore > 0) tierScore *= 0.6;
                    return null;
                case "hurt":
                    if (ctx != null && ctx.Health < 0.6) { tierScore += 1.5 * (0.6 - ctx.Health) / 0.6; fitWhy.Add("you are low on health"); }
                    return null;
                case "basic":
                    if (ctx != null && ctx.Kind == ModeKind.Boss) { tierScore *= 0.5; note = "Boss Rush: few basic enemies"; return note; }
                    return null;
                case "boss":
                    if (ctx != null) tierScore *= ctx.Boss;
                    return null;
                case "healthy":
                    if (ctx != null && ctx.Health < 0.7 && tierScore > 0) tierScore *= 0.5;
                    return null;
                case "revive":   // C16-12 (DK-C01): a second life is worth an A-tier pick at least; more while the run must be survived for a quest or the squad is under 60 %
                    {
                        if (ctx != null && ctx.Survival <= 0) return null;          // One Hit: health means nothing (both revive items leave that mode out anyway)
                        if (tierScore < ReviveFloor) tierScore = ReviveFloor;      // also over a tier the player edited lower in knowledge.json
                        bool hurt = ctx != null && ctx.Health < ReviveHurt, quest = ctx != null && ctx.SurviveQuest;
                        if (!hurt && !quest) return null;
                        tierScore += ReviveLift + (hurt ? ReviveLift * (ReviveHurt - ctx.Health) / ReviveHurt : 0);
                        int pct = (int)Math.Floor(ctx.Health * 100 + 1e-6);      // floor: a squad under 60 % never reads '60%'
                        fitWhy.Insert(0, hurt ? "a second life: the squad is at " + pct + "% health" : "a second life: the quest asks to survive the run");
                        if (c.Say != null) c.Say.Lead = hurt ? "a second life - the squad is at " + pct + "%" : "a second life - survive the run (quest)";
                        return null;
                    }
                default: return null;       // tags:* are judged in TagPoints
            }
        }

        /// <summary>0.16.0 (C16-02): the distinct statuses the squad applies - each damage type with a share of 10 % or more applies its
        /// own status, or the one a held conversion item turns it into; Brave Toaster held adds one.</summary>
        internal static int StatusKinds(ItemContext c)
        {
            var tags = c.Tags; var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var st in ItemBook.StatusOf)
            {
                if (tags.Share(st[0]) < 0.1) continue;
                string status = st[1];
                if (c.Held != null) foreach (var cv in ItemBook.Converts) if (Is(cv[1], st[0]) && c.Held.Contains(cv[0])) status = cv[2];
                seen.Add(status);
            }
            return seen.Count + (c.Held != null && c.Held.Contains("Brave Toaster") ? 1 : 0);
        }

        /// <summary>0.16.0 (C16-02): items that work against an item already held - the clashes the item texts state (ItemBook.Clashes:
        /// Devil's Deal's hit pops 99'th Balloon and resets T-Pose Doll) and the player's own (knowledge.json itemClashes), each unordered
        /// pair once: worth less while the other one is held. The mirror of Pairs. The status conversions and Mana Potion against the
        /// cooldown items are the held-item rules' (C16-01), squad-aware.</summary>
        static double Clashes(string name, ItemContext c, List<string> why)
        {
            if (c.Held == null || name == null) return 0;
            double v = 0;
            foreach (var pair in Union(UseBook ? ItemBook.Clashes : new string[0][], c.K != null ? c.K.ItemClashes : null))
            {
                string other = Is(pair.Key, name) ? pair.Value : Is(pair.Value, name) ? pair.Key : null;
                if (other == null || !c.Held.Contains(other)) continue;
                v -= 0.9; why.Insert(0, "clashes with " + Your(other));
                if (c.Say != null) c.Say.Lead = "clashes with " + Your(other);
            }
            return Math.Max(-1.8, v);
        }

        // returns the note in the card's words (null: the tier stands, no note)
        static string Ranged(double share, string what, ItemContext c, ref double tierScore, ref string note)
        {
            if (share < 0) return null;      // unknown: the tier stands
            tierScore = tierScore * (0.25 + 0.75 * share) - (share <= 0 ? 0.8 : 0);
            note = share <= 0 ? "no " + what + "-range weapon on the squad" : (int)Math.Round(share * 100) + "% of the squad's weapons are " + what + "-range";
            return share <= 0 ? note : share >= 0.995 ? "all your weapons are " + what + "-range" : Portion(share) + " your weapons are " + what + "-range";
        }

        // a share of the squad's weapons in words where it is a plain fraction ("half", "a third of"), else in per cent
        static string Portion(double share)
        {
            int pct = (int)Math.Round(share * 100);
            switch (pct)
            {
                case 50: return "half";
                case 25: return "a quarter of";
                case 33: return "a third of";
                case 67: return "two thirds of";
                case 75: return "three quarters of";
                default: return pct + "% of";
            }
        }

        // What a description says never changes, only what it is worth to this squad at this minute: so the text work -
        // stripping the rich text, splitting it into bonus and malus clauses, some sixty pattern matches over them, the
        // tag grants - is done once per description and kept. The plan's GRAB row scores every item that can still drop
        // after every pick; without this that was several thousand pattern matches a time.
        sealed class TextFacts
        {
            public string Text, Neg;
            public bool[] InPos, InNeg;                              // per rule of All: its pattern is in the bonus / the malus clauses
            public List<KeyValuePair<int, string>> Grants;           // "+4 to Fire damage type tag": (4, Fire), in text order
            public List<KeyValuePair<int, string>> Sizes;            // "1: +20% XP ... 2: +20 Armor ... 3: +15% Critical Chance": a bonus per squad size, else null
            public string SizeHead;                                  // the text before that list
        }
        // Duct Tape: one bonus per squad size, on lines of their own in the game and run together in the extracted data
        static readonly Regex PerSize = new Regex(@"(?<=^|\s)([1-9]):\s*(.+?)(?=\s+[1-9]:\s|\s*$)", RegexOptions.Singleline);

        /// <summary>An item that lists a bonus per squad size gives only the line of the CURRENT size: the text with the other
        /// lines cut out (and that line, for the reason), or the text as it is for every other item.</summary>
        static string SizedText(string description, int size, out string line)
        {
            line = null;
            var f = Parse(description);
            if (f.Sizes == null || size <= 0) return description;
            var mine = f.Sizes[0];
            foreach (var kv in f.Sizes) if (kv.Key <= size && kv.Key >= mine.Key) mine = kv;
            line = mine.Value;
            return f.SizeHead + " " + mine.Value;
        }
        static readonly Dictionary<string, TextFacts> _parsed = new Dictionary<string, TextFacts>();

        static TextFacts Parse(string description)
        {
            string key = description ?? "";
            TextFacts f;
            if (_parsed.TryGetValue(key, out f)) return f;
            f = new TextFacts { Text = RichTag.Replace(key, "") };
            var clauses = new List<string>();
            foreach (var cl in Regex.Split(f.Text, @"(?<=[.!])\s+|\s(?=-\s?\d+\s?%)")) if (!string.IsNullOrWhiteSpace(cl)) clauses.Add(cl);
            var posParts = new List<string>(); var negParts = new List<string>();
            foreach (var cl in clauses) ((Negative.IsMatch(cl) && !NegativeButGood.IsMatch(cl)) ? negParts : posParts).Add(cl);
            string pos = string.Join(" ", posParts);       // every clause a malus: nothing positive to find (not "the whole text")
            f.Neg = string.Join(" ", negParts);
            f.InPos = new bool[All.Length]; f.InNeg = new bool[All.Length];
            for (int i = 0; i < All.Length; i++) { f.InPos[i] = All[i].Re.IsMatch(pos); f.InNeg[i] = All[i].Re.IsMatch(f.Neg); }
            f.Grants = new List<KeyValuePair<int, string>>();
            foreach (Match m in TagGrant.Matches(f.Text))
            {
                int n; if (!int.TryParse(m.Groups[1].Value, out n)) continue;
                foreach (Match tm in TypeName.Matches(m.Groups[2].Value)) f.Grants.Add(new KeyValuePair<int, string>(n, Canon(tm.Value)));
            }
            var sizes = PerSize.Matches(f.Text);
            if (sizes.Count >= 2 && sizes[0].Groups[1].Value == "1")
            {
                f.Sizes = new List<KeyValuePair<int, string>>(); f.SizeHead = f.Text.Substring(0, sizes[0].Index).Trim();
                foreach (Match m in sizes) f.Sizes.Add(new KeyValuePair<int, string>(m.Groups[1].Value[0] - '0', m.Groups[2].Value.Trim()));
            }
            if (_parsed.Count > 2048) _parsed.Clear();      // 136 items in the game; a bound all the same
            _parsed[key] = f;
            return f;
        }

        /// <summary>Score an item description against the squad and the run. <paramref name="typed"/>: the item is about a
        /// damage type; <paramref name="fits"/>: the squad deals one of them.</summary>
        public static double Score(string description, ItemContext c, List<string> why, out bool typed, out bool fits, out bool economic)
        {
            typed = false; fits = false; economic = false;
            int economyRules = 0, otherRules = 0;
            double score = 0;
            var parsed = Parse(description);
            string neg = parsed.Neg;
            var squad = c.Squad ?? new List<string>();
            bool hasElemental = false; foreach (var s in squad) if (Array.IndexOf(Elemental, s) >= 0) hasElemental = true;
            bool exact = c.Tags != null && c.Tags.Known;
            var ctx = c.Ctx;
            bool wanted = false;
            var row = c.Row;

            for (int ki = 0; ki < All.Length; ki++)
            {
                var kw = All[ki];
                bool inPos, inNeg; double wt = 1;
                if (row != null)
                {   // 0.16.0 (C16-02): the book says what the item gives and takes, and how much of each
                    inPos = row.GainW[ki] > 0; inNeg = !inPos && row.CostW[ki] > 0; wt = inPos ? row.GainW[ki] : inNeg ? row.CostW[ki] : 0;
                }
                else { inPos = parsed.InPos[ki] || StatHit(kw, c.Stats, neg.Length == 0); inNeg = !inPos && parsed.InNeg[ki]; }
                if (!inPos && !inNeg) continue;
                double before = score;
                // 0.16.0 (C16-01e): Life Savings held - every granted cash heals instead, so the cash rule is weighed as healing (the survival axis)
                bool cashHeals = kw.Tag == "cash" && HeldRules.CashHeals(c.HeldFacts);
                string axisName = cashHeals ? "survival" : kw.Axis;
                double axis = AxisOf(axisName, ctx); bool named = false;
                // a health word in the text of an item the game neither flags as healing nor lists a health statistic for is
                // incidental ("magnets now also collect ... Healthpaks"): it must not keep an economy item from being one,
                // or its tier never follows the clock (Electric Personality stood on GRAB to the last second)
                bool incidental = kw.Tag == "healing" && c.Stats != null && !c.Healing && !StatHit(kw, c.Stats, true);
                if (inPos) { if (kw.Axis == "economy" || kw.Axis == "cash") economyRules++; else if (kw.Tag != "damage" && !incidental) otherRules++; }
                if (kw.Cls != null)
                {
                    double best = 0; string who = null; bool power = false;
                    if (kw.Type != null) typed = true;
                    if (kw.Type != null && exact)
                    {
                        best = c.Tags.Fit(kw.Type);
                        if (best > 0) { who = c.Tags.SourceText(kw.Type); if (who.Length == 0) who = "the squad"; fits = true; }
                    }
                    else if (kw.PowerTag != null && c.OwnedTags != null)
                    {
                        if (c.OwnedTags.Contains(kw.PowerTag)) { best = 1; who = "the squad owns a " + kw.PowerTag.ToLowerInvariant(); power = true; }
                    }
                    else foreach (var s in squad) { double v; if (kw.Cls.TryGetValue(s, out v) && v > best) { best = v; who = s; } }
                    if (kw.Type != null && !exact && best > 0) fits = true;
                    if (inNeg) { if (best > 0) { score -= 0.7 * best; why.Add("hurts " + who + ": " + kw.Tag); if (c.Say != null && (row == null || row.Say == null || kw.Type != null)) c.Say.Hurts.Add(Fit(kw, who, power, exact, c.Tags, 0.7 * best * wt)); } goto done; }
                    if (best > 0) { score += best * axis; why.Add(who + ": " + kw.Tag); named = true; if (c.Say != null && Draw(row, wt, inPos && row != null && row.Uses[ki])) c.Say.Boosts.Add(Fit(kw, who, power, exact, c.Tags, best * axis * wt)); }
                    else if (kw.Exclusive || (kw.PowerTag != null && c.OwnedTags != null && kw.Generic <= 0 && kw.Survival <= 0))
                    {
                        var needs = new List<string>(); foreach (var kv in kw.Cls) if (kv.Value >= 1) needs.Add(kv.Key);
                        score -= kw.Exclusive ? 1 : 0.4; why.Add(kw.PowerTag != null && c.OwnedTags != null ? "no " + kw.PowerTag.ToLowerInvariant() + " on the squad" : "needs " + string.Join("/", needs));
                        if (c.Say != null && c.Say.Lacks == null && Draw(row, wt, false)) c.Say.Lacks = Wording.Lacks(kw.PowerTag != null && c.OwnedTags != null ? kw.PowerTag : null, needs);
                    }
                    else if (kw.Type != null && exact) { why.Add("no " + kw.Type + " damage on the squad"); if (c.Say != null && Draw(row, wt, false)) c.Say.Nobody.Add(kw.Type); }
                    else if (kw.Tag == "status effects" && hasElemental) score += 0.3;
                }
                if (inNeg)
                {
                    if (kw.Cls == null && kw.Generic > 0) { score -= 0.5 * kw.Generic; why.Add("costs " + kw.Tag); if (c.Say != null && (row == null || row.Say == null)) c.Say.Hurts.Add(new ItemFit { What = kw.Tag, Weight = 0.5 * kw.Generic * wt }); }
                    // 0.16.0 (C16-02): a cost in health (Devil's Deal, Burger, Jewel of Death): weighs what survival weighs now
                    else if (kw.Cls == null && kw.Survival > 0) { double sv = ctx == null ? 1 : Math.Max(0.3, ctx.Survival); score -= 0.5 * kw.Survival * sv; why.Add("costs health"); if (c.Say != null && (row == null || row.Say == null)) c.Say.Hurts.Add(new ItemFit { What = "health", Weight = 0.5 * kw.Survival * sv * wt }); }
                    goto done;
                }
                if (kw.Generic > 0)
                {
                    score += kw.Generic * axis;
                    if (kw.Cls == null)
                    {
                        why.Add(kw.Tag + AxisNote(axisName, axis, ctx));
                        if (c.Say != null && c.Say.Keyword == null) { c.Say.Keyword = kw.Tag; c.Say.KeywordAxis = cashHeals ? "heal" : kw.Axis; c.Say.KeywordValue = axis; }
                        if (cashHeals)
                        {   // C16-01e: the reason, and the card's words where cash is what the item gives (a book row's full-weight gain)
                            why.Add(HeldRules.CashHealWhy);
                            if (c.Say != null && row != null && wt >= 1 && c.Say.LeadForms == null) c.Say.LeadForms = (string[])HeldRules.CashHealSay.Clone();
                        }
                    }
                }
                if (kw.Survival > 0 && ctx != null)
                {
                    score += kw.Survival * (ctx.Survival - 0.4);       // a squad that is fine gains little from more health; one that is hurting, late, a lot
                    // the clause once per card (0.13.0, F12): an item that both armors and heals matches two survival rules - both
                    // still count in the score, but "survival matters now (the squad is hurting)" stood twice under Frozen Heart
                    string clause = ctx.Survival <= 0 ? "health means nothing in One Hit" : ctx.Survival >= 1.3 ? "survival matters now" + (ctx.Health < 0.5 ? " (the squad is hurting)" : "") : null;
                    if (clause != null) { if (!why.Contains(clause)) why.Add(clause); }
                    // say what it is scored for when no survivor is named for it: a MedKit read "no squad-specific value"
                    else if (!named && !incidental && !why.Contains("survival")) why.Add("survival");
                    if (c.Say != null && c.Say.Survival == null && (clause != null || (!named && !incidental)))
                        c.Say.Survival = ctx.Survival <= 0 ? "onehit" : ctx.Survival >= 1.3 ? (ctx.Health < 0.5 ? "hurting" : "now") : "plain";
                }
                if (kw.Ability > 0) score += kw.Ability * (c.AbilityLean - 0.5) * 2 * 0.5;
                if (kw.Weapon > 0) score += kw.Weapon * (0.5 - c.AbilityLean) * 2 * 0.5;
                if (!wanted)
                    foreach (var w in c.Wants)
                        if (Is(w.Value, kw.Tag) || (kw.Type != null && Is(w.Value, kw.Type)) || (kw.Tag == "survival" && Is(w.Value, "healing")))
                        {
                            score += 0.5; why.Add(w.Key + " wants " + kw.Tag); wanted = true;
                            if (c.Say != null) { c.Say.Want = w.Value; c.Say.WantBuild = Wording.BuildOf(w.Key); }
                            break;
                        }
                done:
                score = before + (score - before) * wt;
            }
            if (c.Healing && ctx != null && ctx.Survival <= 0) { score -= 1.0; }
            economic = economyRules > 0 && otherRules == 0;
            return score;
        }

        // 0.16.0 (C16-02): whether a fit of a book row reaches the card's words. Every fit is scored and logged; the card draws a boost,
        // a type nobody deals or what it lacks only from a rule of full weight (Emerald Gem's +1 % dodge never reads "more dodge"), and
        // never a boost from a "~" gain - what the item works with, not what it gives (Modchip gives no crits, Broken Glass no slows)
        static bool Draw(BookEntry row, double wt, bool uses) { return row == null || (wt >= 1 && !uses); }

        /// <summary>Old entry (bench).</summary>
        public static double Score(string description, IList<string> squad, TagProfile tags, List<string> why)
        {
            bool a, b, e; return Score(description, new ItemContext { Squad = squad, Tags = tags }, why, out a, out b, out e);
        }

        // one thing an item boosts or weakens, for the card's words: a damage type the squad deals (and the first powerup dealing it), a
        // powerup tag the squad owns, or a keyword a class favours
        static ItemFit Fit(ItemRule kw, string who, bool power, bool exact, TagProfile tags, double weight)
        {
            if (power) return new ItemFit { What = kw.PowerTag, Power = true, Weight = weight };
            if (kw.Type != null && exact) return new ItemFit { What = kw.Type, Who = Wording.FirstSource(who), Typed = true, Pct = (int)Math.Round(tags.Share(kw.Type) * 100), Weight = weight };
            return new ItemFit { What = kw.Type ?? kw.Tag, Who = who, Weight = weight };
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
            int top = 0; string topType = null; int withPoints = 0;
            foreach (var t in TagProfile.Names) { int n = tags.PointsOf(t); if (n > 0) withPoints++; if (n > top) { top = n; topType = t; } }

            var say = c.Say;
            if (Is(name, "Ultra Instinct"))
            {
                if (top >= 30 && !tags.Known) { why.Insert(0, topType + " is at " + top + ": every other tag point pours into it"); if (say != null) say.Lead = "pools every other tag point into " + topType; return 3.0; }
                if (top >= 22 && tags.Known)
                {
                    // taking it POOLS the tags: every other type's points move into the top one. Gained: those points for the
                    // share of the squad's damage that deals the top type; wiped: the same points for the shares that deal
                    // THEIR types, and every special (10 points) that was on. On this file's scale a point is 0.25 x fit =
                    // 0.4 x share and a special 1.2. Points spread thin over types the squad barely deals cost nothing to
                    // pool; a second stack the squad lives on (Kinetic 26 and its special beside Slashing 33) costs about
                    // what it brings, and then this is no top pick. What is wiped counts in full, what is gained at 85 %:
                    // the shares are estimates, and the other types can never reach a special again
                    int moved = 0, specials = 0; double lost = 0, worst = 0; string hurt = null;
                    foreach (var t in TagProfile.Names)
                    {
                        int n = tags.PointsOf(t); if (n <= 0 || t == topType) continue;
                        double l = tags.Share(t) * n; bool special = tags.SpecialAt > 0 && n >= tags.SpecialAt && tags.Share(t) > 0;
                        moved += n; lost += l; if (special) specials++;
                        if (l + (special ? 3 : 0) > worst) { worst = l + (special ? 3 : 0); hurt = t + " " + n + (special ? " and its special" : ""); }
                    }
                    double net = Math.Max(-1.5, Math.Min(3.0, 0.4 * (0.85 * tags.Share(topType) * moved - lost) - 1.2 * specials));
                    // not switched on yet (22 to 29): worth the old "close to it" bonus at most, and nothing when switching it on
                    // would wipe more than it pools
                    if (top < 30 && net >= 0.8) { why.Add(topType + " " + top + "/30: close to switching it on"); if (say != null) say.Lead = topType + " has " + top + " of the 30 tags it needs"; return 0.8; }
                    if (net >= 1.5) why.Insert(0, topType + " is at " + top + ": the " + moved + " points of the other tags pour into it");
                    else if (hurt != null && worst >= 1) why.Insert(0, (top < 30 ? "at 30 it pools " : "pools ") + moved + " points into " + topType + " but wipes " + hurt);
                    else why.Add(topType + (top < 30 ? " " + top + "/30" : " is at " + top) + ": only " + moved + " other points to pour into it");
                    if (say != null)
                        say.Lead = net >= 1.5 ? "pools " + moved + " tag points into " + topType + " (at " + top + ")"
                            : hurt != null && worst >= 1 ? "would wipe your " + hurt.Split(' ')[0] + " tags to feed " + topType
                            : "only " + moved + " other tag points to pour into " + topType;
                    return net;
                }
                if (top >= 22) { why.Add(topType + " " + top + "/30: close to switching it on"); if (say != null) say.Lead = topType + " has " + top + " of the 30 tags it needs"; return 0.8; }
                why.Add("does nothing until a tag reaches 30 (best: " + (topType ?? "none") + " " + top + ")");
                if (say != null) say.Lead = "does nothing until a tag hits 30 (" + (topType != null ? topType + " is " + top : "no tags yet") + ")";
                return -0.8;
            }
            if (Is(name, "One For All"))
            {
                if (top >= 10 && withPoints <= 3) { why.Add("would flatten your " + topType + " stack of " + top); if (say != null) say.Lead = "would flatten your " + top + " " + topType + " tags"; return -1.5; }
                if (withPoints >= 4) { why.Add("evens out " + withPoints + " tag types"); if (say != null) say.Lead = "evens out your " + withPoints + " damage type tags"; return 0.6; }
                return 0;
            }

            double v = 0;
            // 0.16.0 (C16-02): the grants the pattern cannot read - Omnigeode (+1 to every type, +3 where a type has none), Zugzwang (+5 below
            // 6, -2 at 6 or more), Magical Hat (+4 instead of +2 on an elemental type that has 4 or more)
            var grants = new List<KeyValuePair<int, string>>();
            if (!UseBook) foreach (var g in Parse(description).Grants) grants.Add(g);
            else if (Is(name, "Omnigeode")) foreach (var t in TagProfile.Names) grants.Add(new KeyValuePair<int, string>(tags.PointsOf(t) == 0 ? 3 : 1, t));
            else if (Is(name, "Zugzwang Hypergaster XD")) foreach (var t in TagProfile.Names) grants.Add(new KeyValuePair<int, string>(tags.PointsOf(t) < 6 ? 5 : -2, t));
            else foreach (var g in Parse(description).Grants) grants.Add(Is(name, "Magical Hat") && tags.PointsOf(g.Value) >= 4 ? new KeyValuePair<int, string>(4, g.Value) : g);
            bool every = grants.Count >= TagProfile.Names.Length;       // a grant to every type (Omnigeode, Zugzwang): unknown damage types count as two of seven
            foreach (var grant in grants)
            {
                int n = grant.Key; string type = grant.Value;
                double fit = tags.Known ? tags.Fit(type) : every ? 0.4 * 2 / 7.0 : 0.4;
                if (!tags.Known && n < 0) continue;
                int cur = tags.PointsOf(type);
                v += 0.25 * n * fit;
                if (tags.SpecialAt > 0 && cur < tags.SpecialAt && cur + n >= tags.SpecialAt && fit > 0)
                {
                    v += 1.2; why.Insert(0, "+" + n + " " + type + " reaches the special (" + tags.SpecialAt + ")");
                    if (say != null) say.Lead = "turns on the " + type + " 10-tag effect";
                }
                else if (n < 0 && tags.SpecialAt > 0 && cur >= tags.SpecialAt && cur + n < tags.SpecialAt && fit > 0)
                {
                    v -= 1.2; why.Insert(0, n + " " + type + " drops it under the special (" + tags.SpecialAt + ")");
                    if (say != null) say.Lead = "switches off your " + type + " 10-tag effect";
                }
            }
            return Math.Max(-2.0, Math.Min(2.5, v));
        }

        static string Canon(string type)
        {
            foreach (var n in TagProfile.Names) if (Is(n, type)) return n;
            return type;
        }

        /// <summary>Pairs the items themselves name: worth more when the other half is already held. 0.16.0 (C16-01i): Last Unicorn counts the
        /// animal items held (the game's own flag, HeldFacts.AnimalHeld: +0.9 each, two at most, its words first as a 'held: ' reason), and
        /// an animal item (ItemContext.Animal) pairs with a held Last Unicorn - a held-item rule, so also under [Debug] ItemKeywords.</summary>
        static double Pairs(string name, ItemContext c, List<string> why, List<string> heldWhy)
        {
            if (name == null || (c.Held == null && c.HeldFacts == null)) return 0;
            double v = 0;
            if (HeldRules.IsItem(HeldRules.LastUnicorn, name, c.Asset))
            {
                string[] count; double u = HeldRules.UnicornCount(c.HeldFacts, c.Held, out count);
                if (u > 0) { v += u; heldWhy.Add(HeldRules.Prefix + count[0]); if (c.Say != null) c.Say.LeadForms = count; }
            }
            else if (c.Animal && HeldRules.UnicornHeld(c.HeldFacts, c.Held))
            {
                v += HeldRules.AnimalPair; why.Insert(0, "pairs with " + Your(HeldRules.LastUnicorn.Name));
                if (c.Say != null) c.Say.Lead = "pairs with " + Your(HeldRules.LastUnicorn.Name);
            }
            if (c.Held == null || c.K == null) return Math.Min(1.8, v);
            foreach (var pair in Union(UseBook ? ItemBook.Pairs : new string[0][], c.K.ItemPairs))
            {
                string other = Is(pair.Key, name) ? pair.Value : Is(pair.Value, name) ? pair.Key : null;
                if (other == null || !c.Held.Contains(other)) continue;
                v += 0.9; why.Insert(0, "pairs with " + Your(other));
                if (c.Say != null) c.Say.Lead = "pairs with " + Your(other);
            }
            return Math.Min(1.8, v);
        }

        /// <summary>0.16.0 (C16-01j rule A): a status item while a conversion item is held - <paramref name="fit"/> loses the default type's fit
        /// and gains the fit of what causes the status now (HeldRules.StatusRule, times the row's weight of the type), <paramref name="fits"/>
        /// follows; the card says it ('no Electrify while you hold Battery Leakage' / 'your Electric now causes Toxify (Battery Leakage)'), and the
        /// reason lines of the old fit ('Tesla: electric' once the type causes another status, 'no Chemical damage on the squad' once the status
        /// has a cause the squad deals) go with it. Nothing changes otherwise.</summary>
        static void StatusConverted(string name, string text, BookEntry row, ItemContext c, List<string> fitWhy, List<string> heldWhy, ref double fit, ref bool fits)
        {
            string type, note; double oldFit, newFit; string[] lead;
            if (!HeldRules.StatusRule(c, name, out type, out oldFit, out newFit, out lead, out note)) return;
            int ki = Array.FindIndex(All, r => r.Type != null && Is(r.Type, type));
            if (ki < 0) return;
            var parsed = row == null ? Parse(text) : null;
            Func<int, double> weight = j => row != null ? (row.GainW != null ? row.GainW[j] : 0)
                : (parsed.InPos[j] || StatHit(All[j], c.Stats, parsed.Neg.Length == 0)) ? 1 : 0;
            double wt = weight(ki);
            if (wt <= 0) return;             // the item's own type was not scored (a keyword text that does not name it): nothing to move
            fit += (newFit - oldFit) * wt;
            bool others = false;             // another damage type the item is about, dealt by the squad
            for (int j = 0; j < All.Length; j++) if (j != ki && All[j].Type != null && weight(j) > 0 && c.Tags.Fit(All[j].Type) > 0) others = true;
            fits = newFit > 0 || others;
            // the item's own type causes another status now (a held conversion): its fit line and its boost go
            if (oldFit > 0 && HeldRules.ConversionsHeld(c.HeldFacts, c.Held).Any(cv => Is(cv[1], type)))
            {
                string who = c.Tags.SourceText(type); if (who.Length == 0) who = "the squad";
                fitWhy.Remove(who + ": " + All[ki].Tag);
                if (c.Say != null) c.Say.Boosts.RemoveAll(b => b.Typed && Is(b.What, type));
            }
            if (newFit > 0 && oldFit <= 0)
            {
                fitWhy.Remove("no " + type + " damage on the squad");
                if (c.Say != null) c.Say.Nobody.RemoveAll(t => Is(t, type));
            }
            if (lead != null) { heldWhy.Add(HeldRules.Prefix + lead[0]); if (c.Say != null) c.Say.LeadForms = lead; }
            else if (note != null) fitWhy.Insert(0, note);
        }

        /// <summary>0.16.0 (C16-01j rule B): a conversion item card while status items are held - what it would stop or feed of them
        /// (HeldRules.ConvertCard: -0.9 / +0.6 each, clamped -1.8 .. +1.2), a 'held: ' reason per item and the card's words.</summary>
        static double StatusCard(string name, ItemContext c, List<string> heldWhy)
        {
            List<string> whys; string[] lead;
            double v = HeldRules.ConvertCard(c, name, out whys, out lead);
            if (whys != null) foreach (var w in whys) heldWhy.Add(HeldRules.Prefix + w);
            if (lead != null && c.Say != null) c.Say.LeadForms = lead;
            return v;
        }

        /// <summary>0.16.0 (C16-02): "your Omnigeode", but "The Word" as it is.</summary>
        internal static string Your(string item) { return item.StartsWith("The ", StringComparison.Ordinal) ? item : "your " + item; }

        /// <summary>0.16.0 (C16-02): the book's pairs (or clashes) and knowledge.json's, each unordered pair once.</summary>
        static List<KeyValuePair<string, string>> Union(string[][] book, IEnumerable<KeyValuePair<string, string>> knowledge)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var all = new List<KeyValuePair<string, string>>();
            Action<string, string> add = (a, b) => { string key = string.CompareOrdinal(a.ToLowerInvariant(), b.ToLowerInvariant()) <= 0 ? a + "|" + b : b + "|" + a; if (seen.Add(key)) all.Add(new KeyValuePair<string, string>(a, b)); };
            foreach (var bp in book) add(bp[0], bp[1]);
            if (knowledge != null) foreach (var kp in knowledge) add(kp.Key, kp.Value);
            return all;
        }

        /// <summary>Items that grow over the run (a stack per kill, per chest, per second): early or not at all.</summary>
        static double Scaling(string name, ItemContext c, List<string> why)
        {
            var row = RowOf(name);
            if (c.Ctx == null || c.K == null || name == null || !(c.K.ScalingItems.Contains(name) || (row != null && row.Clock.Contains("grows")))) return 0;
            double e = c.Ctx.Economy;
            if (e >= 1.25) { why.Add("grows all run: take it early"); if (c.Say != null) c.Say.Scaling = "early"; return 0.7; }
            if (e <= 0.5) { why.Add("grows over time: too late to matter (" + c.Ctx.ClockText + ")"); if (c.Say != null) c.Say.Scaling = "late"; return -0.9; }
            return 0.2 * (e - 1);
        }
    }
}
