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
            }
        }

        // Keep this valid JSON with comments (the reader skips them). Class names are the game's display names.
        public const string DefaultJson = @"{
  // YAZS Companion - ranking knowledge. Edit freely; delete the file to restore these defaults.
  // Tiers: S / A / B / C. Names must match the game's English names exactly.
  // Sources (2026-09):
  //  - GoldMath, 'Synergy Guide' (Steam Community, id 3352985777): pair rankings by synergy count; the mod
  //    recomputes those counts from the game's own synergy nodes at run time.
  //  - yetanotherzombiesurvivors.wiki tier lists (survivors, weapons, squads, items) and its rule
  //    'take each ability once, finish the weapon, then dump into the one ability that is already working'.
  //  - yetanotherzombiesurvivorswiki.wiki 'best upgrades': max the starting weapon first, one weapon line,
  //    then crit / turret-shield / poison / dodge-regen depending on the squad identity.
  ""survivors"": {
    ""SWAT"":     { ""leader"": ""S"", ""rescue"": ""S"" },
    ""Engineer"": { ""leader"": ""A"", ""rescue"": ""S"" },
    ""Huntress"": { ""leader"": ""A"", ""rescue"": ""S"" },
    ""Tank"":     { ""leader"": ""A"", ""rescue"": ""A"" },
    ""Ranger"":   { ""leader"": ""A"", ""rescue"": ""A"" },
    ""Ghost"":    { ""leader"": ""B"", ""rescue"": ""B"" },
    ""Mechanic"": { ""leader"": ""B"", ""rescue"": ""B"" },
    ""Pyro"":     { ""leader"": ""B"", ""rescue"": ""B"" },
    ""Medic"":    { ""leader"": ""C"", ""rescue"": ""C"" }
  },
  // preferred tier-2 branch per survivor (the two rank-2 weapons are exclusive); unlisted = decided by tree investment
  ""weaponBranch"": {
    ""SWAT"": ""Assault Rifle"",
    ""Tank"": ""Rocket Launcher"",
    ""Ghost"": ""Thousand Cuts"",
    ""Mechanic"": ""Nitro-Gun"",
    ""Pyro"": ""Flamethrower""
  },
  // abilities the guides single out; unlisted abilities are neutral
  ""abilities"": {
    ""Helicopter Strike"": ""S"",
    ""Automatic Turret"": ""A"",
    ""Bombing Strike"": ""A"",
    ""Energy Shield"": ""S"",
    ""Electric Turret"": ""A"",
    ""Electrocution"": ""A"",
    ""Cooling Mods"": ""A"",
    ""Ice Turret"": ""A""
  },
  ""items"": {
    ""Silencer"": ""S"",
    ""Black Box"": ""A"", ""Mana Potion"": ""A"", ""Dartboard"": ""A"", ""Fishing Pole"": ""A"", ""Last Round"": ""A"",
    ""Ragged Patch"": ""A"", ""Power Generator"": ""A"",
    ""Hijacked Signal"": ""B"", ""Jailbroken Phone"": ""B"", ""Access Keycard"": ""B"", ""Great Nade"": ""B"",
    ""Hyperactivity"": ""B"", ""The Word"": ""B"", ""Boiling Pot"": ""B"", ""Pawn Shop Receipt"": ""B"",
    ""Glass Cannon"": ""C"", ""Brave Toaster"": ""C"", ""Acoustic Guitar"": ""C"", ""Easter Egg"": ""C"", ""Mushroom Mushroom"": ""C""
  },
  ""itemNotes"": {
    ""Silencer"": ""shines on crit squads"",
    ""Glass Cannon"": ""only behind Engineer's shield"",
    ""Mana Potion"": ""strong on Mechanic with Engineer"",
    ""Power Generator"": ""Endurance staple after elite bosses"",
    ""Boiling Pot"": ""Pyro on Green Hell""
  },
  ""critSquad"": [ ""Huntress"", ""Ghost"", ""SWAT"", ""Ranger"" ],
  // military-training stat weights (added to rarity: Common 1, Rare 2, Legendary 3, Endless 2.5)
  ""militaryStats"": {
    ""WeaponDamage"": 1.0, ""AbilityDamage"": 1.0, ""Damage"": 0.9,
    ""CritChance"": 0.9, ""CriticalChance"": 0.9, ""CritDamage"": 0.9, ""CriticalDamage"": 0.9,
    ""AbilityCooldown"": 0.9, ""WeaponCooldownMod"": 0.8, ""ReloadSpeed"": 0.7,
    ""MaxHealth"": 0.6, ""Armor"": 0.5, ""DodgeChance"": 0.5, ""HealthRegen"": 0.4,
    ""MovementSpeed"": 0.4, ""XP"": 0.5, ""Cash"": 0.4, ""Luck"": 0.4
  }
}";
    }
}
