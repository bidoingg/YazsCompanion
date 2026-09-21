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
//
// Build packs: another mod can lend builds for a survivor (Api\Extensions.cs, RegisterBuildProvider). It names a JSON
// file - { "title", "default", "builds": [ the same objects as under "custom" ] } - and while it does, those builds
// stand next to the presets: listed in the menu under the pack's title, selected and saved like any preset, and
// followed through the very same Build object, so the ranking needs no code of its own for them. A pack may name a
// default, which is what Auto means for that survivor while the pack is there. When the pack goes away a selection
// that pointed into it reads as Auto again, without a word and without touching builds.json (it is back in force
// when the pack returns). With no provider registered - and in the bench - none of this runs.
// Pure C# (no game types); names are the game's English names.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace YazsCompanion
{
    /// <summary>How level-ups are split between the weapon and the abilities.</summary>
    internal enum BuildStyle { Weapon, Balanced, Ability }

    internal sealed class Build
    {
        public string Id = "", Survivor = "", Name = "", Summary = "";
        public string Glyph = "";                                   // menu art: one of Art's glyph names
        public string Source = "";                                  // "guides" = a build the human guides describe, "data" = an alternative read from the game's data, "" = the player's own
        public string Pack = "";                                    // the title of the build pack another mod lent it with; "" = the mod's own or the player's
        public string Branch = "";                                // tier-2 weapon; "" = decided live (squad damage types, tree investment, guides)
        public BuildStyle Style = BuildStyle.Weapon;
        public List<string> Abilities = new List<string>();         // priority order, the first is the one to focus; unlisted = after these
        public List<string> Skip = new List<string>();              // abilities this build does not want
        public Dictionary<string, string> Evolution = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // ability -> evolution name; missing = decided live
        public List<string> Wants = new List<string>();             // item leanings: ItemRules tags ("critical", "turret", "abilities", ...) and damage types
        public bool Custom;

        public Build Clone()
        {
            var b = new Build { Id = Id, Survivor = Survivor, Name = Name, Summary = Summary, Glyph = Glyph, Source = Source, Pack = Pack, Branch = Branch, Style = Style, Custom = Custom };
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

    /// <summary>Builds another mod lends for one survivor: a JSON file, read once per change of the file.</summary>
    internal sealed class BuildPack
    {
        public string Owner = "", Survivor = "", Title = "", Path = "";
        public List<Build> Builds = new List<Build>();
        public Build Default;                 // what Auto means for the survivor while the pack is there; null = Auto stays Auto
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

        /// <summary>Everything the menu lists for a survivor: the presets, the builds of the packs other mods lend right now,
        /// then the player's edited copy when there is one.</summary>
        public static List<Build> ChoicesOf(string survivor)
        {
            var list = PresetsOf(survivor);
            foreach (var p in PacksOf(survivor)) list.AddRange(p.Builds);
            Build c; if (_custom.TryGetValue(survivor, out c)) list.Add(c);
            return list;
        }

        /// <summary>The selection as builds.json has it - which may point into a build pack that is not there right now.</summary>
        public static string SelectedId(string survivor) { string id; return _selected.TryGetValue(survivor ?? "", out id) && !string.IsNullOrEmpty(id) ? id : AutoId; }

        static Build Chosen(string survivor)
        {
            string id = SelectedId(survivor);
            if (id == AutoId) return null;
            foreach (var b in ChoicesOf(survivor)) if (string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase)) return b;
            return null;
        }

        /// <summary>The survivor is on Auto: by choice, or because the build selected came with a pack that is away.</summary>
        public static bool OnAuto(string survivor) { return Chosen(survivor) == null; }

        /// <summary>What Auto means right now: null = read the squad, the tree and the guides; a build = the default of a
        /// build pack another mod lends for this survivor.</summary>
        public static Build AutoOf(string survivor)
        {
            foreach (var p in PacksOf(survivor)) if (p.Default != null) return p.Default;
            return null;
        }

        /// <summary>The build the advice follows for this survivor; null = Auto (read the squad, the tree and the guides).</summary>
        public static Build For(string survivor) { return Chosen(survivor) ?? AutoOf(survivor); }

        public static void Select(string survivor, string id) { _selected[survivor] = id ?? AutoId; Save(); }

        public static Build CustomOf(string survivor) { Build c; return _custom.TryGetValue(survivor ?? "", out c) ? c : null; }

        /// <summary>The player's own copy of a build to edit: made from the selected build (or the first preset) on first use.</summary>
        public static Build EnsureCustom(string survivor)
        {
            Build c;
            if (_custom.TryGetValue(survivor, out c)) return c;
            var from = For(survivor) ?? PresetsOf(survivor).FirstOrDefault();
            c = from != null ? from.Clone() : new Build { Survivor = survivor };
            c.Id = "custom-" + survivor.ToLowerInvariant(); c.Survivor = survivor; c.Custom = true; c.Source = ""; c.Pack = "";
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

        // ------------------------------------------------------------------ build packs (other mods, through Api\Extensions.cs)
        /// <summary>Asked with a survivor's name: the build-pack files other mods offer for it right now, as (owner, path).
        /// Set by Api.Extensions while a provider is registered; null = nobody (always, in the bench), and then nothing
        /// below runs.</summary>
        public static Func<string, List<KeyValuePair<string, string>>> PackSource;

        // The ranking asks for a survivor's build dozens of times within one offer, and every answer is a call into
        // another mod plus a look at a file's write time: an answer stands for a second. The menu forgets them whenever it
        // draws (ForgetPacks), so what it lists is what the providers say at that moment.
        const long PackAnswerMs = 1000;
        sealed class PackAnswer { public long At; public int Epoch; public List<BuildPack> Packs; }
        sealed class PackFile { public DateTime Stamp; public BuildPack Pack; }        // Pack == null: unreadable as it stands (said once)
        static readonly Dictionary<string, PackAnswer> _packAnswers = new Dictionary<string, PackAnswer>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, PackFile> _packFiles = new Dictionary<string, PackFile>(StringComparer.OrdinalIgnoreCase);
        static readonly List<BuildPack> NoPacks = new List<BuildPack>();
        static readonly HashSet<string> _packSaid = new HashSet<string>();
        static int _packEpoch;

        /// <summary>Ask the providers afresh at the next question (any thread: a registration may come from anywhere).</summary>
        public static void ForgetPacks() { Interlocked.Increment(ref _packEpoch); }

        /// <summary>The build packs other mods lend for this survivor right now, in the order the mods registered.</summary>
        public static List<BuildPack> PacksOf(string survivor)
        {
            var source = PackSource;
            if (source == null || string.IsNullOrEmpty(survivor)) return NoPacks;
            long now = Environment.TickCount64; int epoch = Volatile.Read(ref _packEpoch);
            PackAnswer a;
            if (_packAnswers.TryGetValue(survivor, out a) && a.Epoch == epoch && now - a.At < PackAnswerMs) return a.Packs;
            var packs = new List<BuildPack>();
            try
            {
                var offers = source(survivor);
                if (offers != null)
                    foreach (var o in offers)
                    {
                        var p = PackFrom(o.Key, o.Value, survivor);
                        if (p != null && p.Builds.Count > 0) packs.Add(p);
                    }
            }
            catch (Exception e) { if (_packSaid.Add("source:" + e.Message)) Log("build packs for " + survivor + " could not be asked for: " + e.Message); }
            _packAnswers[survivor] = new PackAnswer { At = now, Epoch = epoch, Packs = packs };
            return packs;
        }

        // parsed files are kept by survivor + path and read again only when the file's write time moves
        static BuildPack PackFrom(string owner, string path, string survivor)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string key = survivor + "|" + path; DateTime stamp;
            try
            {
                if (!File.Exists(path)) { _packFiles.Remove(key); if (_packSaid.Add("missing:" + key)) Log("build pack of " + owner + " for " + survivor + " not found: " + path); return null; }
                stamp = File.GetLastWriteTimeUtc(path);
            }
            catch { return null; }
            PackFile f;
            if (_packFiles.TryGetValue(key, out f) && f.Stamp == stamp && (f.Pack == null || f.Pack.Owner == (owner ?? ""))) return f.Pack;
            f = new PackFile { Stamp = stamp };
            try
            {
                f.Pack = ParsePack(File.ReadAllText(path), owner, survivor, s => Log("build pack " + path + ": " + s));
                f.Pack.Path = path;
                Log("build pack '" + f.Pack.Title + "' of " + f.Pack.Owner + " for " + survivor + ": " + f.Pack.Builds.Count + " build(s)"
                    + (f.Pack.Default != null ? ", Auto follows " + f.Pack.Default.Name : "") + " - " + path);
            }
            catch (Exception e) { f.Pack = null; Log("build pack " + path + " unreadable (" + e.Message + "); ignored until the file changes"); }
            _packFiles[key] = f;
            return f.Pack;
        }

        /// <summary>A build pack from its JSON: { "title": "...", "default": "id or name", "builds": [ { ...a build as under
        /// "custom" in builds.json... } ] } - or one such build alone. Names are checked against the survivor's kit (a misspelt
        /// one would silently never match): what does not fit is dropped and said through <paramref name="warn"/>. Throws on
        /// JSON that cannot be read.</summary>
        public static BuildPack ParsePack(string json, string owner, string survivor, Action<string> warn = null)
        {
            var pack = new BuildPack { Owner = owner ?? "", Survivor = survivor ?? "" };
            Action<string> say = s => { if (warn != null) warn(s); };
            string wanted = "";
            using (var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }))
            {
                var root = doc.RootElement; JsonElement v;
                if (root.ValueKind != JsonValueKind.Object) throw new FormatException("the root is not an object");
                if (root.TryGetProperty("title", out v) && v.ValueKind == JsonValueKind.String) pack.Title = v.GetString() ?? "";
                if (root.TryGetProperty("default", out v) && v.ValueKind == JsonValueKind.String) wanted = v.GetString() ?? "";
                var found = new List<JsonElement>();
                if (root.TryGetProperty("builds", out v) && v.ValueKind == JsonValueKind.Array) { foreach (var o in v.EnumerateArray()) if (o.ValueKind == JsonValueKind.Object) found.Add(o); }
                else if (root.TryGetProperty("abilities", out v) || root.TryGetProperty("branch", out v)) found.Add(root);      // one build alone
                else say("no \"builds\" array");
                var kit = KitOf(survivor); var raw = new Dictionary<Build, string>();
                foreach (var o in found)
                {
                    var b = Read(o); string given = b.Id;
                    if (string.IsNullOrEmpty(b.Name)) b.Name = given.Length > 0 ? given : "Build " + (pack.Builds.Count + 1);
                    // an id of its own kind: a pack can never shadow a preset or the player's build, and the selection saved in
                    // builds.json says where it points
                    b.Id = "ext:" + Slug(pack.Owner) + ":" + Slug(given.Length > 0 ? given : b.Name);
                    if (pack.Builds.Any(x => string.Equals(x.Id, b.Id, StringComparison.OrdinalIgnoreCase))) { say("'" + b.Name + "': a second build with the id '" + given + "', dropped"); continue; }
                    b.Survivor = pack.Survivor; b.Custom = false; b.Source = "pack";
                    Fit(b, kit, s => say("'" + b.Name + "': " + s));
                    pack.Builds.Add(b); raw[b] = given;
                }
                if (wanted.Length > 0)
                {
                    pack.Default = pack.Builds.FirstOrDefault(b => string.Equals(raw[b], wanted, StringComparison.OrdinalIgnoreCase))
                        ?? pack.Builds.FirstOrDefault(b => string.Equals(b.Name, wanted, StringComparison.OrdinalIgnoreCase) || string.Equals(b.Id, wanted, StringComparison.OrdinalIgnoreCase));
                    if (pack.Default == null) say("\"default\": no build with the id or name '" + wanted + "'; Auto stays Auto");
                }
            }
            if (pack.Title.Length == 0) pack.Title = pack.Owner.Length > 0 ? pack.Owner : "Build pack";
            foreach (var b in pack.Builds) b.Pack = pack.Title;
            return pack;
        }

        // the names of a lent build against the kit, in the kit's own spelling; an evolution may be given by its short
        // name ("Microbombs" for "Kunai Dance: Microbombs")
        static void Fit(Build b, Kit kit, Action<string> say)
        {
            if (kit == null) return;
            if (b.Branch.Length > 0)
            {
                string branch = Same(b.Branch, kit.BranchA) ? kit.BranchA : Same(b.Branch, kit.BranchB) ? kit.BranchB : null;
                if (branch == null) say("the weapon branch '" + b.Branch + "' is neither " + kit.BranchA + " nor " + kit.BranchB + ": decided live");
                b.Branch = branch ?? "";
            }
            Func<List<string>, string, List<string>> known = (names, what) =>
            {
                var keep = new List<string>();
                foreach (var n in names)
                {
                    var a = kit.Abilities.FirstOrDefault(x => Same(x[0], n));
                    if (a == null) say("unknown " + what + " '" + n + "' (" + kit.Survivor + " has " + string.Join(", ", kit.Abilities.Select(x => x[0])) + ")");
                    else if (!keep.Contains(a[0])) keep.Add(a[0]);
                }
                return keep;
            };
            var abilities = known(b.Abilities, "ability"); b.Abilities.Clear(); b.Abilities.AddRange(abilities);
            var skip = known(b.Skip, "ability to skip"); b.Skip.Clear(); b.Skip.AddRange(skip.Where(s => !b.Abilities.Contains(s)));
            foreach (var ev in b.Evolution.ToList())
            {
                b.Evolution.Remove(ev.Key);
                var a = kit.Abilities.FirstOrDefault(x => Same(x[0], ev.Key));
                if (a == null) { say("evolution of an unknown ability '" + ev.Key + "'"); continue; }
                string pick = null;
                for (int i = 1; i <= 2; i++) if (Same(a[i], ev.Value) || Same(a[i].Substring(a[i].IndexOf(':') + 1).Trim(), ev.Value)) pick = a[i];
                if (pick == null) say("'" + ev.Value + "' is not an evolution of " + a[0] + " (" + a[1] + " / " + a[2] + "): decided live");
                else b.Evolution[a[0]] = pick;
            }
        }
        static bool Same(string a, string b) { return string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase); }

        static string Slug(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var ch in (s ?? "").Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            return sb.ToString().Trim('-');
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
