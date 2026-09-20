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
        public readonly Dictionary<string, string> WeaponBranch = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // class -> preferred tier-2 weapon
        public readonly Dictionary<string, string> AbilityTier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> ItemTier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> ItemNote = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, double> MilitaryStat = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> CritSquad = new List<string>();      // survivors that make crit items shine
        public readonly List<KeyValuePair<string, string>> ItemPairs = new List<KeyValuePair<string, string>>();   // items that name each other: worth more once the other is held
        public readonly HashSet<string> ScalingItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);        // items that grow over the run: early or not at all
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
            }
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
    ""Silencer"": ""short-range weapons"",
    ""Dartboard"": ""long-range weapons"",
    ""Accumulator"": ""magnet pickups nuke the screen"",
    ""Chick Magnet"": ""pickup range, and cooldowns on every magnet"",
    ""Electric Personality"": ""magnets collect everything"",
    ""Mana Potion"": ""strong on Mechanic with Engineer"",
    ""Power Generator"": ""ability crits reload the weapons"",
    ""Giant Enemy Crab"": ""a forum and guide staple"",
    ""Homing Pigeon"": ""ability hits on full health enemies always crit"",
    ""Gaslighter"": ""+20% damage per status effect on the target"",
    ""Wooden Stick"": ""XP: an early pick"",
    ""Crowbar"": ""chests and signals open at once: saves time, nothing else"",
    ""Boxing Gloves"": ""+30% against elites and bosses"",
    ""Acoustic Guitar"": ""guides disagree (a must-have in one, avoid in another)"",
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
  }
}";
    }
}
