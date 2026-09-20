// Build guides: for every survivor the builds the advice can follow. A build names the tier-2 weapon branch, the
// order in which the four abilities matter, which evolution to take when both are offered, how level-ups are split
// between the weapon and the abilities, and what the build wants from items. Most survivors have more than one
// build worth playing - the two weapon branches deal different damage types, and abilities and evolutions line up
// behind one or the other - so the player picks (mod menu, BUILDS tab) and the ranking follows; "Auto" reads the
// squad instead: the branch and the evolutions that share damage types with what the squad already deals.
//
// The presets below come from the game's own data (what each weapon, ability and evolution deals and which tags it
// carries) and the published guides listed in Knowledge.cs. The player's choices and edited copies live in
// builds.json next to the DLL: { "selected": { "SWAT": "swat-rifleman" }, "custom": { "SWAT": { ... } } }.
// Pure C# (no game types); names are the game's English names.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace YazsCompanion
{
    /// <summary>How level-ups are split between the weapon and the abilities.</summary>
    internal enum BuildStyle { Weapon, Balanced, Ability }

    internal sealed class Build
    {
        public string Id = "", Survivor = "", Name = "", Summary = "";
        public string Glyph = "";                                   // menu art: one of Art's glyph names
        public string Source = "";                                  // "guides" = a build the human guides describe, "data" = an alternative read from the game's data, "" = the player's own
        public string Branch = "";                                  // tier-2 weapon; "" = decided live (squad damage types, tree investment, guides)
        public BuildStyle Style = BuildStyle.Weapon;
        public List<string> Abilities = new List<string>();         // priority order, the first is the one to focus; unlisted = after these
        public List<string> Skip = new List<string>();              // abilities this build does not want
        public Dictionary<string, string> Evolution = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // ability -> evolution name; missing = decided live
        public List<string> Wants = new List<string>();             // item leanings: ItemRules tags ("critical", "turret", "abilities", ...) and damage types
        public bool Custom;

        public Build Clone()
        {
            var b = new Build { Id = Id, Survivor = Survivor, Name = Name, Summary = Summary, Glyph = Glyph, Source = Source, Branch = Branch, Style = Style, Custom = Custom };
            b.Abilities.AddRange(Abilities); b.Skip.AddRange(Skip); b.Wants.AddRange(Wants);
            foreach (var kv in Evolution) b.Evolution[kv.Key] = kv.Value;
            return b;
        }

        /// <summary>0 for the focus ability, 1, 2, ...; -1 when the build does not rank it.</summary>
        public int PriorityOf(string ability)
        {
            for (int i = 0; i < Abilities.Count; i++) if (string.Equals(Abilities[i], ability, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
        public bool Skips(string ability) { return Skip.Contains(ability, StringComparer.OrdinalIgnoreCase); }
        public string EvolutionOf(string ability) { string e; return Evolution.TryGetValue(ability ?? "", out e) && !string.IsNullOrEmpty(e) ? e : null; }
    }

    /// <summary>The abilities and weapon line of a survivor, by English name: what a build can be made of (the editor's choices).</summary>
    internal sealed class Kit
    {
        public string Survivor;
        public string[] Line;                 // start, upgrade, branch A, branch B, final
        public string[][] Abilities;          // { ability, evolution A, evolution B }
        public string BranchA { get { return Line[2]; } }
        public string BranchB { get { return Line[3]; } }
        public string[] EvolutionsOf(string ability)
        {
            foreach (var a in Abilities) if (string.Equals(a[0], ability, StringComparison.OrdinalIgnoreCase)) return new[] { a[1], a[2] };
            return new string[0];
        }
    }

    internal static class Builds
    {
        public static readonly string[] Survivors = { "SWAT", "Tank", "Engineer", "Huntress", "Ghost", "Medic", "Pyro", "Mechanic", "Ranger" };

        static readonly Dictionary<string, string> _selected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, Build> _custom = new Dictionary<string, Build>(StringComparer.OrdinalIgnoreCase);
        static string _path;
        public const string AutoId = "auto";

        // ------------------------------------------------------------------ the kits (from the game's data, 1.0.1)
        public static readonly Kit[] Kits =
        {
            K("SWAT", new[] { "Pistol", "SMG", "Assault Rifle", "Sniper Rifle", "Grenade Launcher" },
                A("Grenade Trail", "Grenade Trail: Domino", "Grenade Trail: Shrapnel"), A("Helicopter Strike", "Helicopter Strike: Chemtrails", "Helicopter Strike: Gunner"),
                A("Automatic Turret", "Automatic Turret: Provocation", "Automatic Turret: Demolition"), A("Ricochet", "Ricochet: Infinite", "Ricochet: Shattering")),
            K("Tank", new[] { "Shotgun", "Pump-Action Shotgun", "Minigun", "Rocket Launcher", "Super Shotgun" },
                A("Minefield", "Minefield: Shrapnel", "Minefield: Taunt"), A("Sawblade Drone", "Sawblade Drone: Enchantment", "Sawblade Drone: Cogwheels"),
                A("Bombing Strike", "Bombing Strike: Supercharge", "Bombing Strike: Bioweapon"), A("Fury Unleashed", "Fury Unleashed: Heroism", "Fury Unleashed: Cooperation")),
            K("Engineer", new[] { "Taser", "Tesla", "Blaster", "Laser", "Plasma" },
                A("Electric Turret", "Electric Turret: Cooling", "Electric Turret: Fly Catcher"), A("Electrocution", "Electrocution: Passion", "Electrocution: Venomous"),
                A("EMP Grenade", "EMP Grenade: Support", "EMP Grenade: Experimental"), A("Energy Shield", "Energy Shield: Protection", "Energy Shield: Sub-Zero")),
            K("Huntress", new[] { "Bow", "Multishot", "Explosive Arrows", "Freezing Arrows", "Toxic Arrows" },
                A("Arrow Rain", "Arrow Rain: Downpour", "Arrow Rain: Thunderstruck"), A("Bear Trap", "Bear Trap: Arrows", "Bear Trap: Fire"),
                A("Arrow Penetration", "Arrow Penetration: Explosive", "Arrow Penetration: Barbed"), A("Zombie Decoy", "Zombie Decoy: Moving", "Zombie Decoy: Toxic")),
            K("Ghost", new[] { "Katana", "Katana Splash", "Thousand Cuts", "Windcutter", "Soul Reaper" },
                A("Pulsar", "Pulsar: Overheat", "Pulsar: Unstoppable"), A("Shuriken", "Shuriken: Boomerang", "Shuriken: Single"),
                A("Holo-bait", "Holo-bait: Sub Zero", "Holo-bait: Laser"), A("Kunai Dance", "Kunai Dance: Microbombs", "Kunai Dance: Cyanide")),
            K("Medic", new[] { "Handgun", "Syringe Gun", "Antidote Flasks", "Freezing Flasks", "Syringer" },
                A("Experiment 21", "Experiment 21: 13", "Experiment 21: 73"), A("Medical Drone", "Medical Drone: Offense", "Medical Drone: Defense"),
                A("Resuscitation", "Resuscitation: Hoarder", "Resuscitation: Valhalla"), A("Stimpack", "Stimpack: Tough Skin", "Stimpack: Medicament")),
            K("Pyro", new[] { "Fireaxe", "Blowtorch", "Flamethrower", "Infernax", "Axerangs" },
                A("Molotov Cocktail", "Molotov Cocktail: Ice", "Molotov Cocktail: Spray"), A("No Pain, No Gain", "No Pain, No Gain: Commando", "No Pain, No Gain: Reverse"),
                A("Fire Walk", "Fire Walk: Always Active", "Fire Walk: Chemical"), A("Makeshift Bomb", "Makeshift Bomb: Directed", "Makeshift Bomb: Electric")),
            K("Mechanic", new[] { "Trusty Spanner", "Lancer", "Chainsaw", "Nitro-Gun", "Spanner Spammer" },
                A("Remote Control Car", "Remote Control Car: Oil Spill", "Remote Control Car: Nemesis"), A("Transmitter", "Transmitter: Network", "Transmitter: Signal Boost"),
                A("Cooling Mods", "Cooling Mods: Infinite", "Cooling Mods: Support"), A("Ice Turret", "Ice Turret: Pressure Washer", "Ice Turret: Bermuda Triangle")),
            K("Ranger", new[] { "Crossbow", "Repeater Crossbow", "Barber", "Shockspike", "Flarebolt" },
                A("Falcon", "Falcon: Guardian Falcon", "Falcon: Hunting Sweep"), A("Good Boy", "Good Boy: Doberman", "Good Boy: Retriever"),
                A("Animal Whistle", "Hunter's Whistle: Panic Whistle", "Hunter's Whistle: Rally Whistle"), A("Incense", "Incense: Bad Juju", "Incense: Good Vibes")),
        };
        static Kit K(string survivor, string[] line, params string[][] abilities) { return new Kit { Survivor = survivor, Line = line, Abilities = abilities }; }
        static string[] A(string ability, string evoA, string evoB) { return new[] { ability, evoA, evoB }; }
        public static Kit KitOf(string survivor) { foreach (var k in Kits) if (string.Equals(k.Survivor, survivor, StringComparison.OrdinalIgnoreCase)) return k; return null; }

        // ------------------------------------------------------------------ the presets
        static List<Build> _presets;
        public static List<Build> Presets { get { return _presets ?? (_presets = BuildPresets.All()); } }

        public static List<Build> PresetsOf(string survivor) { return Presets.Where(b => string.Equals(b.Survivor, survivor, StringComparison.OrdinalIgnoreCase)).ToList(); }

        /// <summary>Everything the menu lists for a survivor: the presets, then the player's edited copy when there is one.</summary>
        public static List<Build> ChoicesOf(string survivor)
        {
            var list = PresetsOf(survivor);
            Build c; if (_custom.TryGetValue(survivor, out c)) list.Add(c);
            return list;
        }

        public static string SelectedId(string survivor) { string id; return _selected.TryGetValue(survivor ?? "", out id) && !string.IsNullOrEmpty(id) ? id : AutoId; }

        /// <summary>The build the advice follows for this survivor; null = Auto (read the squad, the tree and the guides).</summary>
        public static Build For(string survivor)
        {
            string id = SelectedId(survivor);
            if (id == AutoId) return null;
            foreach (var b in ChoicesOf(survivor)) if (string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase)) return b;
            return null;
        }

        public static void Select(string survivor, string id) { _selected[survivor] = id ?? AutoId; Save(); }

        public static Build CustomOf(string survivor) { Build c; return _custom.TryGetValue(survivor ?? "", out c) ? c : null; }

        /// <summary>The player's own copy of a build to edit: made from the selected build (or the first preset) on first use.</summary>
        public static Build EnsureCustom(string survivor)
        {
            Build c;
            if (_custom.TryGetValue(survivor, out c)) return c;
            var from = For(survivor) ?? PresetsOf(survivor).FirstOrDefault();
            c = from != null ? from.Clone() : new Build { Survivor = survivor };
            c.Id = "custom-" + survivor.ToLowerInvariant(); c.Survivor = survivor; c.Custom = true; c.Source = "";
            c.Name = "My " + survivor; c.Summary = from != null ? "Your own build, started from " + from.Name + "." : "Your own build.";
            var kit = KitOf(survivor);
            if (kit != null) foreach (var a in kit.Abilities) if (c.PriorityOf(a[0]) < 0 && !c.Skips(a[0])) c.Abilities.Add(a[0]);   // every ability has a place in the editor
            _custom[survivor] = c;
            return c;
        }

        public static void ReplaceCustom(string survivor, Build build)
        {
            if (build == null) return;
            build.Survivor = survivor; build.Custom = true;
            _custom[survivor] = build;
            Save();
        }

        public static void DropCustom(string survivor)
        {
            Build c; if (!_custom.TryGetValue(survivor, out c)) return;
            _custom.Remove(survivor);
            if (string.Equals(SelectedId(survivor), c.Id, StringComparison.OrdinalIgnoreCase)) _selected[survivor] = AutoId;
            Save();
        }

        // ------------------------------------------------------------------ builds.json
        public static void Load(string path)
        {
            _path = path; _selected.Clear(); _custom.Clear();
            try
            {
                if (!File.Exists(path)) return;
                Parse(File.ReadAllText(path));
            }
            catch (Exception e) { Log("builds.json unreadable (" + e.Message + "); every survivor on Auto"); _selected.Clear(); _custom.Clear(); }
        }

        public static void Parse(string json)
        {
            using (var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }))
            {
                JsonElement e;
                if (doc.RootElement.TryGetProperty("selected", out e)) foreach (var p in e.EnumerateObject()) _selected[p.Name] = p.Value.GetString();
                if (doc.RootElement.TryGetProperty("custom", out e))
                    foreach (var p in e.EnumerateObject())
                    {
                        var b = Read(p.Value); b.Survivor = p.Name; b.Custom = true;
                        if (string.IsNullOrEmpty(b.Id)) b.Id = "custom-" + p.Name.ToLowerInvariant();
                        _custom[p.Name] = b;
                    }
            }
        }

        static Build Read(JsonElement o)
        {
            var b = new Build(); JsonElement v;
            if (o.TryGetProperty("id", out v)) b.Id = v.GetString() ?? "";
            if (o.TryGetProperty("name", out v)) b.Name = v.GetString() ?? "";
            if (o.TryGetProperty("summary", out v)) b.Summary = v.GetString() ?? "";
            if (o.TryGetProperty("glyph", out v)) b.Glyph = v.GetString() ?? "";
            if (o.TryGetProperty("branch", out v)) b.Branch = v.GetString() ?? "";
            if (o.TryGetProperty("style", out v)) { BuildStyle s; if (Enum.TryParse(v.GetString(), true, out s)) b.Style = s; }
            if (o.TryGetProperty("abilities", out v)) foreach (var a in v.EnumerateArray()) b.Abilities.Add(a.GetString());
            if (o.TryGetProperty("skip", out v)) foreach (var a in v.EnumerateArray()) b.Skip.Add(a.GetString());
            if (o.TryGetProperty("wants", out v)) foreach (var a in v.EnumerateArray()) b.Wants.Add(a.GetString());
            if (o.TryGetProperty("evolution", out v)) foreach (var p in v.EnumerateObject()) b.Evolution[p.Name] = p.Value.GetString();
            return b;
        }

        public static string ToJson()
        {
            using (var ms = new MemoryStream())
            {
                using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
                {
                    w.WriteStartObject();
                    w.WriteStartObject("selected");
                    foreach (var kv in _selected.OrderBy(x => x.Key)) w.WriteString(kv.Key, kv.Value);
                    w.WriteEndObject();
                    w.WriteStartObject("custom");
                    foreach (var kv in _custom.OrderBy(x => x.Key))
                    {
                        var b = kv.Value;
                        w.WriteStartObject(kv.Key);
                        w.WriteString("id", b.Id); w.WriteString("name", b.Name); w.WriteString("summary", b.Summary); w.WriteString("glyph", b.Glyph);
                        w.WriteString("branch", b.Branch); w.WriteString("style", b.Style.ToString());
                        w.WriteStartArray("abilities"); foreach (var a in b.Abilities) w.WriteStringValue(a); w.WriteEndArray();
                        w.WriteStartArray("skip"); foreach (var a in b.Skip) w.WriteStringValue(a); w.WriteEndArray();
                        w.WriteStartArray("wants"); foreach (var a in b.Wants) w.WriteStringValue(a); w.WriteEndArray();
                        w.WriteStartObject("evolution"); foreach (var ev in b.Evolution) w.WriteString(ev.Key, ev.Value); w.WriteEndObject();
                        w.WriteEndObject();
                    }
                    w.WriteEndObject();
                    w.WriteEndObject();
                }
                return System.Text.Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        /// <summary>Whether the last Save reached the file (the mod menu says so after every change).</summary>
        public static bool LastSaveOk = true;

        public static void Save()
        {
            if (_path == null) return;
            try { File.WriteAllText(_path, ToJson()); LastSaveOk = true; }
            catch (Exception e) { LastSaveOk = false; Log("builds.json not saved: " + e.Message); }
        }

        /// <summary>Set by the plugin; the bench leaves it null.</summary>
        public static Action<string> Logger;
        static void Log(string s) { try { if (Logger != null) Logger(s); } catch { } }
    }
}
