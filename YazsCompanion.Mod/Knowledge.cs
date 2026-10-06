// Guide-derived knowledge: tiers and rules the ranking leans on instead of the player's own history.
// Lives in BepInEx/plugins/YazsCompanion/knowledge.json (written with these defaults on first run, then editable).
// Sources are listed in the file itself and in mod/README.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace YazsCompanion
{
    internal sealed class Knowledge
    {
        public readonly Dictionary<string, string> LeaderTier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> RescueTier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> WeaponBranch = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // class -> preferred weapon branch (a tier-3 weapon)
        public readonly Dictionary<string, string> AbilityTier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> ItemTier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> ItemNote = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, double> MilitaryStat = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> CritSquad = new List<string>();      // survivors that make crit items shine
        public readonly List<KeyValuePair<string, string>> ItemPairs = new List<KeyValuePair<string, string>>();   // items that name each other: worth more once the other is held
        public readonly HashSet<string> ScalingItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);        // items that grow over the run: early or not at all
        // 0.13.0 badge advice (Loadout.cs). Every key starts at its code default, so a knowledge.json from an older build (or one
        // that leaves a key out) falls back to the default, never to 0.
        public readonly Dictionary<string, BadgeStat> BadgeStats = BadgeStat.Defaults();                       // stat asset name minus Team_ / GamePlayer_ / Internal_
        public readonly Dictionary<string, BadgeMode> BadgeModes = BadgeMode.Defaults();                       // game mode enum name -> elite / boss shares, goal, zero list
        public readonly BadgeRules BadgeRules = new BadgeRules();
        public readonly Dictionary<string, BadgeNote> BadgeNotes = new Dictionary<string, BadgeNote>(StringComparer.OrdinalIgnoreCase);   // by short name, asset or id
        public string Source = "defaults";

        public static Knowledge Current = new Knowledge();

        public static double Tier(string tier, double s, double a, double b, double c)
        {
            switch ((tier ?? "").Trim().ToUpperInvariant())
            {
                case "S": return s;
                case "A": return a;
                case "B": return b;
                case "C": return c;
                default: return 0;
            }
        }

        // 0.14.0 (A1): the game's own names of the statistics, for the reason lines under the stat cards and the rescue cards - the
        // ENGLISH UI/StatType labels of data\gamedata.json, baked in. Not the game's localization (MyLocalization): the Companion
        // speaks English, and a translated label would mix two languages inside one sentence.
        static readonly Dictionary<string, string> StatLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "AbilityCooldownReduction", "Ability cooldown reduction" }, { "AbilityCritChance", "Ability critical chance" }, { "AbilityCritDamage", "Ability critical damage" },
            { "AbilityDamage", "Ability damage" }, { "AbilityDurationModifier", "Ability duration" }, { "AbilitySizeModifier", "Ability area" }, { "Armor", "Armor" },
            { "DodgeChance", "Dodge chance" }, { "HealthBonusesMod", "Healing bonuses" }, { "HealthRegen", "Health regeneration" }, { "InviFrames", "Invincibility after hit" },
            { "LifeStealChance", "Life steal chance" }, { "Luck", "Luck" }, { "MagnetRange", "Pickup range" }, { "MaxHealth", "Max health" }, { "MoneyCollectedMod", "Cash modifier" },
            { "MovementSpeedMod", "Movement speed" }, { "MultiCastChance", "Multicast chance" }, { "NumBanishes", "Banishes available" }, { "NumLockdowns", "Lockdowns available" },
            { "NumRerolls", "Rerolls available" }, { "WeaponCooldownReduction", "Weapon cooldown reduction" }, { "WeaponCritChance", "Weapon critical chance" },
            { "WeaponCritDamage", "Weapon critical damage" }, { "WeaponDamageMod", "Weapon damage" }, { "WeaponFireRateMod", "Weapon attack speed" }, { "XPCollectedMod", "XP modifier" },
            { "XPGemRarityModifier", "Experience gem rarity" },
        };
        // the other names the same statistics go by: the military cards' assets (MilitaryTraining_<x>) and the statistics themselves
        // (PlayerAbilitySize, TeamArmor, TeamMovementSpeedMultiplier - their Player / Team prefix is dropped first)
        static readonly Dictionary<string, string> StatAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "AbilityCooldown", "AbilityCooldownReduction" }, { "AbilityCDRed", "AbilityCooldownReduction" }, { "AbilityDuration", "AbilityDurationModifier" },
            { "AbilitySize", "AbilitySizeModifier" }, { "HPRegen", "HealthRegen" }, { "MovementSpeed", "MovementSpeedMod" }, { "MovementSpeedMultiplier", "MovementSpeedMod" },
            { "WeaponCooldown", "WeaponCooldownReduction" }, { "WeaponCooldownMod", "WeaponCooldownReduction" }, { "WeaponCDRed", "WeaponCooldownReduction" },
            { "WeaponDamage", "WeaponDamageMod" }, { "WeaponFireRate", "WeaponFireRateMod" }, { "XPModifierMod", "XPCollectedMod" }, { "XPModifier", "XPCollectedMod" },
            { "XPMultiplier", "XPCollectedMod" }, { "MoneyMultiplier", "MoneyCollectedMod" }, { "HealthBonuses", "HealthBonusesMod" }, { "XPGemRarity", "XPGemRarityModifier" },
        };

        /// <summary>The game's label of a statistic ("Ability area" for AbilitySize, AbilitySizeModifier, MilitaryTraining_AbilitySize or
        /// PlayerAbilitySize), lower-cased for use inside a sentence ("ability area"; "XP modifier" keeps its capitals); null when the
        /// game has no label for it.</summary>
        public static string StatLabel(string key, bool inSentence = true)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string k = key;
            if (k.StartsWith("MilitaryTraining_", StringComparison.Ordinal)) k = k.Substring("MilitaryTraining_".Length);
            string label;
            if (!StatLabels.TryGetValue(k, out label))
            {
                foreach (var prefix in new[] { "GamePlayer_", "Team_", "Internal_", "Player", "Team" })
                    if (k.Length > prefix.Length && k.StartsWith(prefix, StringComparison.Ordinal) && char.IsUpper(k[prefix.Length])) { k = k.Substring(prefix.Length); break; }
                string alias;
                if (!StatLabels.TryGetValue(k, out label) && (!StatAliases.TryGetValue(k, out alias) || !StatLabels.TryGetValue(alias, out label))) return null;
            }
            if (!inSentence || label.Length < 2 || char.IsUpper(label[1])) return label;
            return char.ToLowerInvariant(label[0]) + label.Substring(1);
        }

        public static Knowledge Load(string path)
        {
            var k = new Knowledge();
            try
            {
                RefreshDefaults(path);
                k.Parse(File.ReadAllText(path));
                k.Source = path;
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning("knowledge.json unreadable (" + e.Message + "), using built-in defaults");
                k = new Knowledge(); k.Parse(DefaultJson); k.Source = "defaults";
            }
            Current = k;
            return k;
        }

        /// <summary>Parse a knowledge file from memory (the offline bench; no file, no stamp).</summary>
        public static Knowledge FromJson(string json) { var k = new Knowledge(); k.Parse(json); return k; }

        // Write the defaults when the file is missing. When a newer build ships new defaults, replace the file only if
        // the user never edited it (its hash equals the hash of the defaults we last wrote, kept in knowledge.json.stamp);
        // an edited file is left alone with a log line, the old one is kept as knowledge.json.bak.
        static void RefreshDefaults(string path)
        {
            string stamp = path + ".stamp";
            string defaultsSha = Updater.Sha256(DefaultJson);
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, DefaultJson);
                File.WriteAllText(stamp, defaultsSha);
                return;
            }
            string currentSha = Updater.Sha256(File.ReadAllText(path));
            if (currentSha == defaultsSha) { if (!File.Exists(stamp)) File.WriteAllText(stamp, defaultsSha); return; }
            string lastWritten = File.Exists(stamp) ? File.ReadAllText(stamp).Trim() : null;
            if (lastWritten == null)
            {
                // an install from before the stamp existed: treat what is there as the baseline, refresh next time if untouched
                File.WriteAllText(stamp, currentSha);
                Plugin.Logger.LogInfo("knowledge.json predates the update stamp; keeping it (delete it to get the current defaults)");
                return;
            }
            if (currentSha == lastWritten)
            {
                File.Copy(path, path + ".bak", true);
                File.WriteAllText(path, DefaultJson);
                File.WriteAllText(stamp, defaultsSha);
                Plugin.Logger.LogInfo("knowledge.json refreshed to this build's defaults (previous copy in knowledge.json.bak)");
            }
            else Plugin.Logger.LogInfo("knowledge.json is user-edited; this build's new defaults were not applied (delete the file to reset)");
        }

        void Parse(string json)
        {
            using (var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }))
            {
                var root = doc.RootElement;
                JsonElement e;
                if (root.TryGetProperty("survivors", out e))
                    foreach (var p in e.EnumerateObject())
                    {
                        JsonElement t;
                        if (p.Value.TryGetProperty("leader", out t)) LeaderTier[p.Name] = t.GetString();
                        if (p.Value.TryGetProperty("rescue", out t)) RescueTier[p.Name] = t.GetString();
                    }
                if (root.TryGetProperty("weaponBranch", out e)) foreach (var p in e.EnumerateObject()) WeaponBranch[p.Name] = p.Value.GetString();
                if (root.TryGetProperty("abilities", out e)) foreach (var p in e.EnumerateObject()) AbilityTier[p.Name] = p.Value.GetString();
                if (root.TryGetProperty("items", out e)) foreach (var p in e.EnumerateObject()) ItemTier[p.Name] = p.Value.GetString();
                if (root.TryGetProperty("itemNotes", out e)) foreach (var p in e.EnumerateObject()) ItemNote[p.Name] = p.Value.GetString();
                if (root.TryGetProperty("militaryStats", out e)) foreach (var p in e.EnumerateObject()) MilitaryStat[p.Name] = p.Value.GetDouble();
                if (root.TryGetProperty("critSquad", out e)) foreach (var v in e.EnumerateArray()) CritSquad.Add(v.GetString());
                if (root.TryGetProperty("scalingItems", out e)) foreach (var v in e.EnumerateArray()) ScalingItems.Add(v.GetString());
                if (root.TryGetProperty("itemPairs", out e))
                    foreach (var pair in e.EnumerateArray())
                    {
                        var two = new List<string>(); foreach (var v in pair.EnumerateArray()) two.Add(v.GetString());
                        if (two.Count == 2) ItemPairs.Add(new KeyValuePair<string, string>(two[0], two[1]));
                    }
                if (root.TryGetProperty("badgeStats", out e) && e.ValueKind == JsonValueKind.Object)
                    foreach (var p in e.EnumerateObject())
                    {
                        if (p.Value.ValueKind != JsonValueKind.Object) continue;
                        BadgeStat s; if (!BadgeStats.TryGetValue(p.Name, out s)) BadgeStats[p.Name] = s = new BadgeStat();
                        s.Read(p.Value);
                    }
                if (root.TryGetProperty("badgeModes", out e) && e.ValueKind == JsonValueKind.Object)
                    foreach (var p in e.EnumerateObject())
                    {
                        if (p.Value.ValueKind != JsonValueKind.Object) continue;
                        BadgeMode m; if (!BadgeModes.TryGetValue(p.Name, out m)) BadgeModes[p.Name] = m = new BadgeMode();
                        m.Read(p.Value);
                    }
                if (root.TryGetProperty("badgeRules", out e) && e.ValueKind == JsonValueKind.Object) BadgeRules.Read(e);
                if (root.TryGetProperty("badges", out e) && e.ValueKind == JsonValueKind.Object)
                    foreach (var p in e.EnumerateObject())
                    {
                        if (p.Value.ValueKind != JsonValueKind.Object) continue;
                        var n = new BadgeNote(); n.Read(p.Value); BadgeNotes[p.Name] = n;
                    }
            }
        }

        internal static double Num(JsonElement o, string name, double fallback)
        {
            JsonElement v;
            if (!o.TryGetProperty(name, out v)) return fallback;
            if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
            double d;
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) return d;
            return fallback;
        }
        internal static string Str(JsonElement o, string name, string fallback)
        {
            JsonElement v;
            if (!o.TryGetProperty(name, out v)) return fallback;
            if (v.ValueKind == JsonValueKind.Null) return null;
            return v.ValueKind == JsonValueKind.String ? v.GetString() : fallback;
        }
        internal static bool Flag(JsonElement o, string name, bool fallback)
        {
            JsonElement v;
            if (!o.TryGetProperty(name, out v)) return fallback;
            return v.ValueKind == JsonValueKind.True ? true : v.ValueKind == JsonValueKind.False ? false : fallback;
        }

        // Keep this valid JSON with comments (the reader skips them). Class names are the game's display names.
        public const string DefaultJson = @"{
  // YAZS Companion - ranking knowledge. Edit freely; delete the file to restore these defaults.
  // Tiers: S / A / B / C. Names must match the game's English names exactly.
  // What the advice follows per survivor (weapon branch, ability order, evolutions) is chosen in the mod menu
  // (BUILDS tab, stored in builds.json); this file holds what is left: tiers, item notes, stat weights.
  //
  // Sources, graded (2026-09 review). Most '1.0 wikis' for this game are auto-generated and contradict the game's
  // own data (wrong weapon forks, abilities that do not exist), so they only count where a human source agrees:
  //  [official] Steam patch notes 0.3 - 1.0.1a and developer forum replies: modes, tag rules, item changes.
  //  [human]    Kudesnik, '0.7 General guide' (Steam id 3345006120); GoldMath, 'Synergy Guide' (id 3352985777; the mod
  //             recomputes its synergy counts from the game's own nodes); JHG's achievements guide (id 3006467623);
  //             Steam forum threads on builds, weapons and modes (2023 - 2026); Destructoid's 1.0 class tier list.
  //  [wiki]     yetanotherzombiesurvivors.wiki item and survivor tier lists: kept as a weak prior only.
  // The game's own data (damage types, powerup tags, tag points per level, mode masks) always wins over any of them.
  ""survivors"": {
    ""SWAT"":     { ""leader"": ""S"", ""rescue"": ""S"" },
    ""Engineer"": { ""leader"": ""A"", ""rescue"": ""S"" },
    ""Huntress"": { ""leader"": ""A"", ""rescue"": ""S"" },
    ""Tank"":     { ""leader"": ""A"", ""rescue"": ""A"" },
    ""Ranger"":   { ""leader"": ""A"", ""rescue"": ""A"" },
    ""Medic"":    { ""leader"": ""B"", ""rescue"": ""A"" },
    ""Ghost"":    { ""leader"": ""B"", ""rescue"": ""B"" },
    ""Mechanic"": { ""leader"": ""B"", ""rescue"": ""B"" },
    ""Pyro"":     { ""leader"": ""B"", ""rescue"": ""B"" }
  },
  // the tier-2 branch the guides prefer when a survivor is on Auto (no build selected) AND the squad's damage types
  // do not decide it; unlisted = decided by the squad and the Training Yard investment alone
  ""weaponBranch"": {
    ""SWAT"": ""Assault Rifle"",
    ""Tank"": ""Rocket Launcher"",
    ""Ghost"": ""Thousand Cuts"",
    ""Mechanic"": ""Nitro-Gun"",
    ""Pyro"": ""Infernax""
  },
  // abilities the guides single out, used while a survivor is on Auto; a selected build's own order replaces this
  ""abilities"": {
    ""Helicopter Strike"": ""S"",
    ""Energy Shield"": ""S"",
    ""Arrow Rain"": ""S"",
    ""Automatic Turret"": ""A"",
    ""Bombing Strike"": ""A"",
    ""Sawblade Drone"": ""A"",
    ""Electric Turret"": ""A"",
    ""Electrocution"": ""A"",
    ""Fire Walk"": ""A"",
    ""Experiment 21"": ""A"",
    ""Cooling Mods"": ""A"",
    ""Ice Turret"": ""A"",
    ""Transmitter"": ""C"",
    ""Remote Control Car"": ""C""
  },
  // item tiers. S / A from the wiki list where a human source agrees or is silent; [human] marks items the forum
  // and guide writers name as go-to picks. The mod adds the fit with the squad, the run clock and what is held on top.
  ""items"": {
    ""Silencer"": ""S"", ""Accumulator"": ""S"",
    ""Chick Magnet"": ""A"", ""Electric Personality"": ""A"",
    ""Black Box"": ""A"", ""Mana Potion"": ""A"", ""Dartboard"": ""A"", ""Fishing Pole"": ""A"", ""Last Round"": ""A"",
    ""Ragged Patch"": ""A"", ""Power Generator"": ""A"",
    ""Giant Enemy Crab"": ""A"", ""Homing Pigeon"": ""A"", ""Scouter"": ""A"", ""Magazine Clip"": ""A"", ""Gaslighter"": ""A"", ""Wooden Stick"": ""A"",
    ""Heavy Metal"": ""B"", ""Pickup Pick"": ""B"",
    ""Hijacked Signal"": ""B"", ""Jailbroken Phone"": ""B"", ""Access Keycard"": ""B"", ""Great Nade"": ""B"",
    ""Hyperactivity"": ""B"", ""The Word"": ""B"", ""Boiling Pot"": ""B"", ""Pawn Shop Receipt"": ""B"",
    ""Spoil Canister"": ""B"", ""Jacob's Ladder"": ""B"", ""Plague's Visage"": ""B"", ""Special Snowflake"": ""B"",
    ""Nine Inch Nails"": ""B"", ""Bloody Axe"": ""B"", ""Bleeding Edge"": ""B"", ""The Bomb"": ""B"", ""Explosive Surprise"": ""B"",
    ""Icon of Cinder"": ""B"", ""Icon of Pestilence"": ""B"", ""Icon of Stillness"": ""B"", ""Icon of Tempest"": ""B"",
    ""Slingshot"": ""B"", ""Magical Hat"": ""B"", ""Omnigeode"": ""B"", ""Ultra Instinct"": ""B"", ""One For All"": ""B"",
    ""Solar Panel"": ""B"", ""Power Glove"": ""B"", ""Detective's Pipe"": ""B"", ""Stretcher"": ""B"", ""Frozen Heart"": ""B"",
    ""Boxing Gloves"": ""B"", ""Ruby Gem"": ""B"", ""Sapphire Gem"": ""B"", ""Emerald Gem"": ""B"", ""Acoustic Guitar"": ""B"",
    ""Glass Cannon"": ""C"", ""Brave Toaster"": ""C"", ""Easter Egg"": ""C"", ""Mushroom Mushroom"": ""C""
  },
  ""itemNotes"": {
    ""Silencer"": ""boosts short-range weapons"",
    ""Dartboard"": ""boosts long-range weapons"",
    ""Accumulator"": ""magnet pickups nuke the screen"",
    ""Chick Magnet"": ""pickup range, and cooldowns on every magnet"",
    ""Electric Personality"": ""magnets collect everything"",
    ""Mana Potion"": ""strong on Mechanic with Engineer"",
    ""Power Generator"": ""ability crits reload the weapons"",
    ""Giant Enemy Crab"": ""a forum and guide staple"",
    ""Homing Pigeon"": ""ability hits on full health enemies always crit"",
    ""Gaslighter"": ""+20% damage per status effect on the target"",
    ""Wooden Stick"": ""XP item, take it early"",
    ""Crowbar"": ""chests and signals open at once: saves time, nothing else"",
    ""Boxing Gloves"": ""+30% against elites and bosses"",
    ""Acoustic Guitar"": ""the guides disagree on it (a must-have in one, avoid in another)"",
    ""Magical Hat"": ""+2 to every elemental tag (+4 at 4 or more)""
  },
  // pairs the items themselves name: the second half is worth more once the first is held
  ""itemPairs"": [
    [""Accumulator"", ""Electric Personality""], [""Accumulator"", ""Heavy Metal""], [""Accumulator"", ""Chick Magnet""],
    [""Golden Key"", ""Silver Padlock""],
    [""Access Keycard"", ""Black Box""], [""Access Keycard"", ""Jailbroken Phone""], [""Access Keycard"", ""Ragged Patch""], [""Access Keycard"", ""Hijacked Signal""],
    [""Omnigeode"", ""Emerald Gem""], [""Ultra Instinct"", ""One For All""]
  ],
  // items that grow over the run (a stack per kill, per chest, per second): worth the slot early, not late
  ""scalingItems"": [ ""Ring Of Power"", ""Glass of Milk"", ""Black Box"", ""Wooden Stick"", ""Frozen Heart"", ""99'th Balloon"", ""Hijacked Signal"", ""Reserve Bench"", ""Life Savings"" ],
  ""critSquad"": [ ""Huntress"", ""Ghost"", ""SWAT"", ""Ranger"" ],
  // military-training stat weights, by the card's asset name (MilitaryTraining_<Stat>). The mod scales them live: weapon
  // stats by how much of the squad is weapons, ability stats likewise, XP / luck / magnet range by the run clock,
  // health / armor / regeneration by how much survival matters right now; rarity multiplies the result.
  ""militaryStats"": {
    ""WeaponDamageMod"": 1.0, ""AbilityDamage"": 1.0,
    ""WeaponCritChance"": 0.85, ""AbilityCritChance"": 0.85, ""WeaponCritDamage"": 0.8, ""AbilityCritDamage"": 0.8,
    ""AbilityCooldown"": 0.9, ""WeaponCooldownMod"": 0.85, ""WeaponFireRateMod"": 0.8,
    ""AbilitySize"": 0.7, ""AbilityDuration"": 0.6,
    ""XPModifierMod"": 0.8, ""Luck"": 0.7, ""MagnetRange"": 0.6,
    ""MaxHealth"": 0.6, ""Armor"": 0.55, ""DodgeChance"": 0.5, ""HPRegen"": 0.45,
    ""MovementSpeedMod"": 0.45
  },
  // ---- badge advice on the run setup screen (0.13.0). POINTS = % more squad damage over the whole run, or its equivalent.
  // A badge bonus is worth: value per level x level x w x relevance x axis. Keys = the game's PlayerStatistic asset name
  // minus Team_ / GamePlayer_ / Internal_. w = points per unit (1 % for a fraction, else the raw unit: HP, points, seconds,
  // counts). on = what it needs (weapon / ability share of the build, elite / boss share of the mode, clip weapons, healing),
  // axis = the run curve that multiplies it (economy, cash = [Advice] RunGoal, survival, dodge / move, consistency).
  // raw = counts in its own unit; decay = each further count is worth x decay; dur = scaled by the build's deployables;
  // crit = chance / damage (scaled by how much the build leans on crits). 'estimate (unmeasured)' = not measured in the game yet.
  ""badgeStats"": {
    ""WeaponDamage"": { ""w"": 0.70, ""on"": ""weapon"", ""why"": ""weapon damage pool ~1.4 (tree +20 %, cards): 1 % = 0.7 % of weapon damage - estimate (unmeasured, U3)"" },
    ""AbilityDamage"": { ""w"": 0.70, ""on"": ""ability"", ""why"": ""ability damage pool ~1.4 - estimate (unmeasured, U3)"" },
    ""Hashtag"": { ""w"": 0.75, ""on"": ""type"", ""why"": ""Team_Hashtag<Type> multiplier, base 1.0 + ~0.3 from tag points mid-run: 1 % = 0.75 % of that type's damage - estimate (unmeasured, U1)"" },
    ""TagPoint"": { ""w"": 1.50, ""on"": ""type"", ""why"": ""+2 % damage per tag point (guides) in the same ~1.3 pool - estimate (unmeasured, U1)"" },
    ""WeaponFireRate"": { ""w"": 0.60, ""on"": ""weapon"", ""why"": ""fire-rate pool ~1.35, and cooldown-driven weapons gain less"" },
    ""WeaponCooldownReduction"": { ""w"": 0.75, ""on"": ""weapon"", ""why"": ""at ~20 % CDR, +1 % = +1.25 % cycles on cooldown weapons, ~60 % of weapons"" },
    ""AbilityCooldownReduction"": { ""w"": 1.00, ""on"": ""ability"", ""why"": ""every ability is cooldown-bound: +1 % CDR ~ +1.0-1.25 % casts"" },
    ""AbilitySize"": { ""w"": 0.40, ""on"": ""ability"", ""why"": ""more area = more targets, about half of a damage %"" },
    ""AbilityDuration"": { ""w"": 0.40, ""on"": ""ability"", ""dur"": true, ""why"": ""turrets, drones, fields, shields: x0.5..1 by the build's deployable share"" },
    ""WeaponCriticalChance"": { ""w"": 0.80, ""on"": ""weapon"", ""crit"": ""chance"", ""why"": ""crit x2 (statBase 2.0), chance ~25 %: +1 % = 1/1.25 = 0.8 % - estimate (unmeasured, U3)"" },
    ""AbilityCriticalChance"": { ""w"": 0.80, ""on"": ""ability"", ""crit"": ""chance"", ""why"": ""as weapon crit chance - estimate (unmeasured, U3)"" },
    ""WeaponCriticalDamage"": { ""w"": 0.20, ""on"": ""weapon"", ""crit"": ""damage"", ""why"": ""chance ~25 %: +1 % crit damage = 0.25/1.25 = 0.2 % - estimate (unmeasured, U3)"" },
    ""AbilityCriticalDamage"": { ""w"": 0.20, ""on"": ""ability"", ""crit"": ""damage"", ""why"": ""as weapon crit damage - estimate (unmeasured, U3)"" },
    ""DamageToElites"": { ""w"": 0.85, ""on"": ""elite"", ""why"": ""own multiplier (tree +20 %), times the share of damage that hits elites (mode table) - estimate (unmeasured, U6)"" },
    ""DamageToBosses"": { ""w"": 0.85, ""on"": ""boss"", ""why"": ""own multiplier, times the share of damage that hits bosses (mode table) - estimate (unmeasured, U6)"" },
    ""InstantWeaponReloadChance"": { ""w"": 0.30, ""on"": ""clip"", ""why"": ""a skipped reload on clip weapons: reloading is ~30 % of the cycle"" },
    ""InstantAbilityReloadChance"": { ""w"": 1.00, ""on"": ""ability"", ""why"": ""an instant refresh = a free cast: +1 % chance = +1 % casts"" },
    ""InternalMilitaryTrainingRarityBonus"": { ""w"": 0.12, ""on"": ""all"", ""axis"": ""economy"", ""why"": ""a Rare military card is 2x a Common; military cards ~15 % of run power"" },
    ""XPMultiplier"": { ""w"": 0.50, ""on"": ""all"", ""axis"": ""economy"", ""why"": ""+4 % XP = ~2 more level-ups in a 20:00 run"" },
    ""Luck"": { ""w"": 0.25, ""on"": ""all"", ""axis"": ""economy"", ""raw"": true, ""why"": ""rarer cards and drops; 4 luck < a Common luck card (5) - estimate (unmeasured, U4)"" },
    ""MagnetRange"": { ""w"": 0.06, ""on"": ""all"", ""axis"": ""economy"", ""why"": ""pickup comfort; XP is collected anyway"" },
    ""XPGemRarity"": { ""w"": 0.20, ""on"": ""all"", ""axis"": ""economy"", ""why"": ""rarer gems = more XP per kill - estimate (unmeasured, U4)"" },
    ""PowerupDurationExtension"": { ""w"": 0.03, ""on"": ""all"", ""axis"": ""economy"", ""why"": ""timed field power-ups last longer: minor"" },
    ""NumRerolls"": { ""w"": 1.00, ""on"": ""all"", ""axis"": ""consistency"", ""raw"": true, ""decay"": 0.90, ""why"": ""one bad offer dodged ~ +1 % of run power (base 3); each further reroll x0.9"" },
    ""NumBanishes"": { ""w"": 0.70, ""on"": ""all"", ""axis"": ""consistency"", ""raw"": true, ""decay"": 0.90, ""why"": ""an unwanted powerup out of the pool (base 2); each further banish x0.9"" },
    ""MoneyMultiplier"": { ""w"": 0.50, ""on"": ""all"", ""axis"": ""cash"", ""why"": ""cash only buys Training Yard levels: RunGoal decides (x0.3 Win / x0.6 Balanced / x1.2 Farm)"" },
    ""SpecializationPointsMod"": { ""w"": 0.50, ""on"": ""all"", ""axis"": ""cash"", ""why"": ""survivor XP = class tree points: RunGoal decides"" },
    ""MaxHealth"": { ""w"": 0.02, ""on"": ""all"", ""axis"": ""survival"", ""raw"": true, ""why"": ""80 HP on ~1000 = +8 % effective health"" },
    ""Armor"": { ""w"": 0.25, ""on"": ""all"", ""axis"": ""survival"", ""raw"": true, ""why"": ""~1 % less damage taken per point - estimate (unmeasured, U4)"" },
    ""DodgeChance"": { ""w"": 0.35, ""on"": ""all"", ""axis"": ""dodge"", ""why"": ""+3 % dodge = +3.1 % effective health; the game removes dodge cards in One Hit - estimate (unmeasured, U7)"" },
    ""InviFrames"": { ""w"": 25.0, ""on"": ""all"", ""axis"": ""survival"", ""raw"": true, ""why"": ""+0.05 s on 0.3 s = +17 % grace in a crowd"" },
    ""HealthRegen"": { ""w"": 1.00, ""on"": ""all"", ""axis"": ""survival"", ""raw"": true, ""why"": ""1 HP/s = 60 HP a minute"" },
    ""HealthBonusesMod"": { ""w"": 0.15, ""on"": ""heal"", ""axis"": ""survival"", ""why"": ""+% to all healing received: needs healing on the squad"" },
    ""MovementSpeed"": { ""w"": 0.20, ""on"": ""all"", ""axis"": ""move"", ""raw"": true, ""why"": ""+6 on base 100 = +6 % speed: kiting and pickups"" }
  },
  // per game mode: the share of the squad's damage that hits elites and bosses (estimate, unmeasured: U6), the seconds a timed
  // run lasts (0 = open-ended) and the stats worth nothing there (only with [Advice] ModeAware)
  ""badgeModes"": {
    ""Normal"": { ""elites"": 0.15, ""bosses"": 0.06, ""goal"": 1200 },
    ""Hardcore"": { ""elites"": 0.15, ""bosses"": 0.06, ""goal"": 600 },
    ""OneHit"": { ""elites"": 0.15, ""bosses"": 0.03, ""goal"": 300, ""zero"": [ ""MaxHealth"", ""Armor"", ""HealthRegen"", ""HealthBonusesMod"", ""DodgeChance"", ""InviFrames"" ] },
    ""BossRush"": { ""elites"": 0.10, ""bosses"": 0.50, ""goal"": 600 },
    ""Extermination"": { ""elites"": 0.20, ""bosses"": 0.06, ""goal"": 1200, ""zero"": [ ""XPMultiplier"", ""Luck"", ""MagnetRange"", ""XPGemRarity"" ] },
    ""Endless"": { ""elites"": 0.20, ""bosses"": 0.10, ""goal"": 0 },
    ""Infinite"": { ""elites"": 0.20, ""bosses"": 0.10, ""goal"": 0 }
  },
  // specialAt: tag points for a type's special (the game's own number is used when it can be read); specialPull: points for a
  // badge that completes a special the build would otherwise miss; earlySpecialPerPoint: per point on the stacked type once the
  // special is reached anyway; recruitWeight: 0 = the team leader alone (default), 0.3 = plus the two likely recruits;
  // swapMargin / swapMarginShare: a swap is advised only when it gains at least max(margin, share x the equipped badge's
  // points); buildWantsBoost: a stat the build's item leanings name; selectedBuildRerollBoost: rerolls / banishes with a build
  // selected; unknownStatPerLevel: a stat this file does not know (the badge is then marked 'estimated'); cashByRunGoal: cash
  // and survivor-XP badges by [Advice] RunGoal (win the run / balanced / farm progress)
  ""badgeRules"": {
    ""specialAt"": 10, ""specialPull"": 3.0, ""earlySpecialPerPoint"": 0.25, ""recruitWeight"": 0.0,
    ""swapMargin"": 1.0, ""swapMarginShare"": 0.15, ""buildWantsBoost"": 1.2, ""selectedBuildRerollBoost"": 1.15,
    ""unknownStatPerLevel"": 1.0, ""cashByRunGoal"": [ 0.3, 0.6, 1.2 ]
  },
  // your own word on single badges, by short name (""Gunner""), asset name or id: bias = points added, note = replaces the
  // reason shown. Example: ""Leveling"": { ""bias"": 2.0, ""note"": ""I farm the Training Yard"" }
  ""badges"": {
  }
}";
    }

    /// <summary>The worth of one badge stat (knowledge.json "badgeStats"; Loadout.cs reads it).</summary>
    internal sealed class BadgeStat
    {
        public double W = 1.0;
        public string On = "all";          // weapon | ability | type | all | heal | elite | boss | clip
        public string Axis;                // null | economy | cash | survival | dodge | move | consistency
        public string Crit;                // null | chance | damage
        public bool Dur, Raw;
        public double Decay;               // 0 = none
        public string Why = "";

        public BadgeStat Clone() { return (BadgeStat)MemberwiseClone(); }
        public bool SameAs(BadgeStat o)
        {
            return o != null && W == o.W && On == o.On && Axis == o.Axis && Crit == o.Crit && Dur == o.Dur && Raw == o.Raw && Decay == o.Decay;
        }

        public void Read(JsonElement o)
        {
            W = Knowledge.Num(o, "w", W); On = Knowledge.Str(o, "on", On) ?? "all"; Axis = Knowledge.Str(o, "axis", Axis); Crit = Knowledge.Str(o, "crit", Crit);
            Dur = Knowledge.Flag(o, "dur", Dur); Raw = Knowledge.Flag(o, "raw", Raw); Decay = Knowledge.Num(o, "decay", Decay); Why = Knowledge.Str(o, "why", Why) ?? "";
        }

        static BadgeStat S(double w, string on, string axis, string why, bool raw = false, double decay = 0, bool dur = false, string crit = null)
        {
            return new BadgeStat { W = w, On = on, Axis = axis, Why = why, Raw = raw, Decay = decay, Dur = dur, Crit = crit };
        }

        /// <summary>The code defaults: the same numbers as DefaultJson (the bench checks that they agree).</summary>
        public static Dictionary<string, BadgeStat> Defaults()
        {
            return new Dictionary<string, BadgeStat>(StringComparer.OrdinalIgnoreCase)
            {
                { "WeaponDamage", S(0.70, "weapon", null, "weapon damage pool ~1.4 (tree +20 %, cards): 1 % = 0.7 % of weapon damage - estimate (unmeasured, U3)") },
                { "AbilityDamage", S(0.70, "ability", null, "ability damage pool ~1.4 - estimate (unmeasured, U3)") },
                { "Hashtag", S(0.75, "type", null, "Team_Hashtag<Type> multiplier, base 1.0 + ~0.3 from tag points mid-run: 1 % = 0.75 % of that type's damage - estimate (unmeasured, U1)") },
                { "TagPoint", S(1.50, "type", null, "+2 % damage per tag point (guides) in the same ~1.3 pool - estimate (unmeasured, U1)") },
                { "WeaponFireRate", S(0.60, "weapon", null, "fire-rate pool ~1.35, and cooldown-driven weapons gain less") },
                { "WeaponCooldownReduction", S(0.75, "weapon", null, "at ~20 % CDR, +1 % = +1.25 % cycles on cooldown weapons, ~60 % of weapons") },
                { "AbilityCooldownReduction", S(1.00, "ability", null, "every ability is cooldown-bound: +1 % CDR ~ +1.0-1.25 % casts") },
                { "AbilitySize", S(0.40, "ability", null, "more area = more targets, about half of a damage %") },
                { "AbilityDuration", S(0.40, "ability", null, "turrets, drones, fields, shields: x0.5..1 by the build's deployable share", dur: true) },
                { "WeaponCriticalChance", S(0.80, "weapon", null, "crit x2 (statBase 2.0), chance ~25 %: +1 % = 1/1.25 = 0.8 % - estimate (unmeasured, U3)", crit: "chance") },
                { "AbilityCriticalChance", S(0.80, "ability", null, "as weapon crit chance - estimate (unmeasured, U3)", crit: "chance") },
                { "WeaponCriticalDamage", S(0.20, "weapon", null, "chance ~25 %: +1 % crit damage = 0.25/1.25 = 0.2 % - estimate (unmeasured, U3)", crit: "damage") },
                { "AbilityCriticalDamage", S(0.20, "ability", null, "as weapon crit damage - estimate (unmeasured, U3)", crit: "damage") },
                { "DamageToElites", S(0.85, "elite", null, "own multiplier (tree +20 %), times the share of damage that hits elites (mode table) - estimate (unmeasured, U6)") },
                { "DamageToBosses", S(0.85, "boss", null, "own multiplier, times the share of damage that hits bosses (mode table) - estimate (unmeasured, U6)") },
                { "InstantWeaponReloadChance", S(0.30, "clip", null, "a skipped reload on clip weapons: reloading is ~30 % of the cycle") },
                { "InstantAbilityReloadChance", S(1.00, "ability", null, "an instant refresh = a free cast: +1 % chance = +1 % casts") },
                { "InternalMilitaryTrainingRarityBonus", S(0.12, "all", "economy", "a Rare military card is 2x a Common; military cards ~15 % of run power") },
                { "XPMultiplier", S(0.50, "all", "economy", "+4 % XP = ~2 more level-ups in a 20:00 run") },
                { "Luck", S(0.25, "all", "economy", "rarer cards and drops; 4 luck < a Common luck card (5) - estimate (unmeasured, U4)", raw: true) },
                { "MagnetRange", S(0.06, "all", "economy", "pickup comfort; XP is collected anyway") },
                { "XPGemRarity", S(0.20, "all", "economy", "rarer gems = more XP per kill - estimate (unmeasured, U4)") },
                { "PowerupDurationExtension", S(0.03, "all", "economy", "timed field power-ups last longer: minor") },
                { "NumRerolls", S(1.00, "all", "consistency", "one bad offer dodged ~ +1 % of run power (base 3); each further reroll x0.9", raw: true, decay: 0.90) },
                { "NumBanishes", S(0.70, "all", "consistency", "an unwanted powerup out of the pool (base 2); each further banish x0.9", raw: true, decay: 0.90) },
                { "MoneyMultiplier", S(0.50, "all", "cash", "cash only buys Training Yard levels: RunGoal decides (x0.3 Win / x0.6 Balanced / x1.2 Farm)") },
                { "SpecializationPointsMod", S(0.50, "all", "cash", "survivor XP = class tree points: RunGoal decides") },
                { "MaxHealth", S(0.02, "all", "survival", "80 HP on ~1000 = +8 % effective health", raw: true) },
                { "Armor", S(0.25, "all", "survival", "~1 % less damage taken per point - estimate (unmeasured, U4)", raw: true) },
                { "DodgeChance", S(0.35, "all", "dodge", "+3 % dodge = +3.1 % effective health; the game removes dodge cards in One Hit - estimate (unmeasured, U7)") },
                { "InviFrames", S(25.0, "all", "survival", "+0.05 s on 0.3 s = +17 % grace in a crowd", raw: true) },
                { "HealthRegen", S(1.00, "all", "survival", "1 HP/s = 60 HP a minute", raw: true) },
                { "HealthBonusesMod", S(0.15, "heal", "survival", "+% to all healing received: needs healing on the squad") },
                { "MovementSpeed", S(0.20, "all", "move", "+6 on base 100 = +6 % speed: kiting and pickups", raw: true) },
            };
        }
    }

    /// <summary>What a game mode does to badge values (knowledge.json "badgeModes").</summary>
    internal sealed class BadgeMode
    {
        public double Elites = 0.15, Bosses = 0.06;
        public double Goal = 1200;          // seconds of a timed run; 0 = open-ended
        public readonly HashSet<string> Zero = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool SameAs(BadgeMode o) { return o != null && Elites == o.Elites && Bosses == o.Bosses && Goal == o.Goal && Zero.SetEquals(o.Zero); }

        public void Read(JsonElement o)
        {
            Elites = Knowledge.Num(o, "elites", Elites); Bosses = Knowledge.Num(o, "bosses", Bosses); Goal = Knowledge.Num(o, "goal", Goal);
            JsonElement z;
            if (o.TryGetProperty("zero", out z) && z.ValueKind == JsonValueKind.Array)
            {
                Zero.Clear();
                foreach (var v in z.EnumerateArray()) if (v.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(v.GetString())) Zero.Add(v.GetString());
            }
        }

        static BadgeMode M(double elites, double bosses, double goal, params string[] zero)
        {
            var m = new BadgeMode { Elites = elites, Bosses = bosses, Goal = goal };
            foreach (var z in zero) m.Zero.Add(z);
            return m;
        }

        public static Dictionary<string, BadgeMode> Defaults()
        {
            return new Dictionary<string, BadgeMode>(StringComparer.OrdinalIgnoreCase)
            {
                { "Normal", M(0.15, 0.06, 1200) },
                { "Hardcore", M(0.15, 0.06, 600) },
                { "OneHit", M(0.15, 0.03, 300, "MaxHealth", "Armor", "HealthRegen", "HealthBonusesMod", "DodgeChance", "InviFrames") },
                { "BossRush", M(0.10, 0.50, 600) },
                { "Extermination", M(0.20, 0.06, 1200, "XPMultiplier", "Luck", "MagnetRange", "XPGemRarity") },
                { "Endless", M(0.20, 0.10, 0) },
                { "Infinite", M(0.20, 0.10, 0) },
            };
        }
    }

    /// <summary>The numbers that are not per stat (knowledge.json "badgeRules").</summary>
    internal sealed class BadgeRules
    {
        public int SpecialAt = 10;
        public double SpecialPull = 3.0, EarlySpecialPerPoint = 0.25, RecruitWeight = 0.0;
        public double SwapMargin = 1.0, SwapMarginShare = 0.15, BuildWantsBoost = 1.2, SelectedBuildRerollBoost = 1.15, UnknownStatPerLevel = 1.0;
        public double[] CashByRunGoal = { 0.3, 0.6, 1.2 };

        public bool SameAs(BadgeRules o)
        {
            if (o == null || CashByRunGoal.Length != o.CashByRunGoal.Length) return false;
            for (int i = 0; i < CashByRunGoal.Length; i++) if (CashByRunGoal[i] != o.CashByRunGoal[i]) return false;
            return SpecialAt == o.SpecialAt && SpecialPull == o.SpecialPull && EarlySpecialPerPoint == o.EarlySpecialPerPoint && RecruitWeight == o.RecruitWeight
                && SwapMargin == o.SwapMargin && SwapMarginShare == o.SwapMarginShare && BuildWantsBoost == o.BuildWantsBoost
                && SelectedBuildRerollBoost == o.SelectedBuildRerollBoost && UnknownStatPerLevel == o.UnknownStatPerLevel;
        }

        public double CashFor(int runGoal)
        {
            if (CashByRunGoal == null || CashByRunGoal.Length == 0) return 0.3;
            return CashByRunGoal[Math.Max(0, Math.Min(CashByRunGoal.Length - 1, runGoal))];
        }

        public void Read(JsonElement o)
        {
            SpecialAt = (int)Math.Round(Knowledge.Num(o, "specialAt", SpecialAt));
            SpecialPull = Knowledge.Num(o, "specialPull", SpecialPull); EarlySpecialPerPoint = Knowledge.Num(o, "earlySpecialPerPoint", EarlySpecialPerPoint);
            RecruitWeight = Knowledge.Num(o, "recruitWeight", RecruitWeight); SwapMargin = Knowledge.Num(o, "swapMargin", SwapMargin);
            SwapMarginShare = Knowledge.Num(o, "swapMarginShare", SwapMarginShare); BuildWantsBoost = Knowledge.Num(o, "buildWantsBoost", BuildWantsBoost);
            SelectedBuildRerollBoost = Knowledge.Num(o, "selectedBuildRerollBoost", SelectedBuildRerollBoost);
            UnknownStatPerLevel = Knowledge.Num(o, "unknownStatPerLevel", UnknownStatPerLevel);
            JsonElement c;
            if (o.TryGetProperty("cashByRunGoal", out c) && c.ValueKind == JsonValueKind.Array)
            {
                var l = new List<double>();
                foreach (var v in c.EnumerateArray()) if (v.ValueKind == JsonValueKind.Number) l.Add(v.GetDouble());
                if (l.Count > 0) CashByRunGoal = l.ToArray();
            }
        }
    }

    /// <summary>The player's own word on one badge (knowledge.json "badges").</summary>
    internal sealed class BadgeNote
    {
        public string Tier = "", Note = "";
        public double Bias;
        public void Read(JsonElement o) { Tier = Knowledge.Str(o, "tier", "") ?? ""; Note = Knowledge.Str(o, "note", "") ?? ""; Bias = Knowledge.Num(o, "bias", 0); }
    }
}
