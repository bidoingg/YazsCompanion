// 0.15.0 (C15-02): the game-data drift guard. 1.0.2 renamed five Ranger evolutions (Falcon: Guardian / Assault, Good Boy:
// Dobermann, Animal Whistle: Panic / Rally); the kits, the presets and two bench cases kept the 1.0.1 names, so on a Ranger
// evolution the build's pick lost half a point and its card said the pick was "not offered".
//   E  exact names, run right after the preset check (section 1): every kit weapon, ability and evolution, every preset name
//      and every knowledge.json survivor / weapon / ability / item name equal to the fixture's English name - no normalizing,
//      no asset fallback (the game's name trimmed: one 1.0.2 name carries a stray space). Prints 'all names exact on <the
//      fixture's meta.gameVersion>'; one name off fails the bench.
//   A  the asset fallback: the kits' asset keys against the fixture, Builds.SameName / AssetOf / LearnAssets, a build's own
//      lists (PriorityOf, Skips, EvolutionOf) and a lent pack's names (ParsePack / Fit) after a rename.
//   R  the run-time guard (GameState.cs DataDrift): its comparison Builds.NotInGame over GameKits made from the fixture - clean,
//      a planted rename, a moved weapon, a class without data - the [builds] lines for lent packs and the player's own builds,
//      and the wiring (sources).
//   Tools (no check, run alone): --live-probe <newer probe.json> prints the fixture's name and description diffs against it;
//   --packs <folder> validates every lent build pack under it against the kits and the fixture, counts only.
// Generic names only (the repository is public): the game's own names, made-up ones for a rename.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class Drift
    {
        sealed class Power { public string Name, Asset, Class, Type, Previous, EvolutionOf, Desc; public bool Ability; }
        sealed class Fixture
        {
            public string Version, Path;
            public readonly List<Power> Powers = new List<Power>();
            public readonly Dictionary<string, string> Items = new Dictionary<string, string>(StringComparer.Ordinal);   // asset -> name (as dumped)
            public readonly Dictionary<string, string> ItemDesc = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly HashSet<string> Classes = new HashSet<string>(StringComparer.Ordinal);
            public bool HasPower(string name, string cls) { return Powers.Any(p => p.Name.Trim() == name && (cls == null || p.Class == cls)); }
            public Power ByAsset(string asset) { return asset == null ? null : Powers.FirstOrDefault(p => p.Asset == asset); }
        }

        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string Src(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod", file));
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }
        static string Norm(string s) { return new string((s ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray()); }

        static Fixture Load(string path)
        {
            var fx = new Fixture { Path = path };
            using (var doc = JsonDocument.Parse(File.ReadAllText(path)))
            {
                var root = doc.RootElement; JsonElement e, v;
                if (root.TryGetProperty("meta", out e) && e.ValueKind == JsonValueKind.Object && e.TryGetProperty("gameVersion", out v) && v.ValueKind == JsonValueKind.String) fx.Version = v.GetString();
                foreach (var p in root.GetProperty("powerups").EnumerateArray())
                {
                    Func<string, string> s = k => p.TryGetProperty(k, out v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                    var w = new Power { Name = s("name") ?? "", Asset = s("asset"), Class = s("class"), Type = s("type"), Previous = s("previousWeapon"), EvolutionOf = s("evolutionOf"), Desc = s("desc") };
                    w.Ability = p.TryGetProperty("ability", out v) && v.ValueKind == JsonValueKind.True;
                    fx.Powers.Add(w);
                    if (!string.IsNullOrEmpty(w.Class)) fx.Classes.Add(w.Class);
                }
                foreach (var it in root.GetProperty("items").EnumerateArray())
                {
                    string asset = it.TryGetProperty("asset", out v) ? v.GetString() : null, name = it.GetProperty("name").GetString();
                    fx.Items[asset ?? name] = name;
                    fx.ItemDesc[asset ?? name] = it.TryGetProperty("desc", out v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                }
            }
            return fx;
        }

        // the fixture as the run-time guard reads the live game: per class its weapons (with the line by previousWeapon), abilities, evolutions
        static List<GameKit> Kits(Fixture fx)
        {
            var list = new List<GameKit>();
            foreach (var cls in fx.Classes.OrderBy(c => c))
            {
                var g = new GameKit { Survivor = cls };
                var mine = fx.Powers.Where(p => p.Class == cls && (p.Type == "WeaponUpgradePowerup" || p.Ability)).ToList();
                foreach (var p in mine) g.Assets[p.Name.Trim()] = p.Asset;
                var weapons = mine.Where(p => p.Type == "WeaponUpgradePowerup").ToList();
                var starts = weapons.Where(p => string.IsNullOrEmpty(p.Previous)).ToList();
                if (starts.Count == 1)
                {
                    g.Start = starts[0].Name.Trim();
                    var ups = weapons.Where(p => (p.Previous ?? "").Trim() == g.Start).ToList();
                    if (ups.Count == 1) { g.Upgrade = ups[0].Name.Trim(); g.Branches.AddRange(weapons.Where(p => (p.Previous ?? "").Trim() == g.Upgrade).Select(p => p.Name.Trim())); }
                }
                list.Add(g);
            }
            return list;
        }

        // ================================================================ E: exact names (after Checks.Validate)
        public static int Exact(string probePath)
        {
            int bad = 0;
            Fixture fx;
            try { fx = Load(probePath); }
            catch (Exception ex) { Console.WriteLine("\n=== exact names: probe.json unreadable (" + ex.Message + ")\n  FAIL"); return 1; }
            string on = fx.Version ?? "(no meta.gameVersion in probe.json)";
            Console.WriteLine("\n=== exact names on " + on + " (0.15.0 drift guard): every name the Companion keeps, letter for letter as the game has it");
            var miss = new List<string>();
            Func<string, string> hint = name =>
            {
                var same = fx.ByAsset(Builds.AssetOf(name)) ?? fx.Powers.FirstOrDefault(p => Norm(p.Name) == Norm(name));
                return same != null ? " (the game: '" + same.Name.Trim() + "'" + (same.Class != null ? ", " + same.Class : "") + ")" : " (not in the game)";
            };
            Action<string, string, string> power = (what, name, cls) => { if (!fx.HasPower(name, cls)) miss.Add(what + " '" + name + "'" + hint(name)); };
            Action<string, string> survivor = (what, name) => { if (!fx.Classes.Contains(name)) miss.Add(what + " '" + name + "' (the game's classes: " + string.Join(", ", fx.Classes.OrderBy(c => c)) + ")"); };
            var itemNames = new HashSet<string>(fx.Items.Values.Select(n => n.Trim()), StringComparer.Ordinal);
            Action<string, string> item = (what, name) => { if (!itemNames.Contains(name)) { var near = fx.Items.Values.FirstOrDefault(n => Norm(n) == Norm(name)); miss.Add(what + " '" + name + "'" + (near != null ? " (the game: '" + near.Trim() + "')" : " (not in the game)")); } };

            // the kits: the line, the abilities and their evolutions, each the game's name of that survivor's powerup
            int weapons = 0, abilities = 0, evolutions = 0;
            foreach (var sv in Builds.Survivors) survivor("survivor", sv);
            foreach (var kit in Builds.Kits)
            {
                survivor("kit", kit.Survivor);
                foreach (var w in kit.Line) { power(kit.Survivor + " weapon", w, kit.Survivor); weapons++; }
                foreach (var a in kit.Abilities)
                {
                    power(kit.Survivor + " ability", a[0], kit.Survivor); abilities++;
                    for (int i = 1; i < a.Length; i++) { power(kit.Survivor + " evolution", a[i], kit.Survivor); evolutions++; }
                }
            }
            // the presets: branch, ability order, skips, evolution picks
            int presetNames = 0;
            foreach (var b in Builds.Presets)
            {
                survivor("preset " + b.Id, b.Survivor);
                if (b.Branch.Length > 0) { power("preset " + b.Id + " branch", b.Branch, b.Survivor); presetNames++; }
                foreach (var n in b.Abilities.Concat(b.Skip)) { power("preset " + b.Id + " ability", n, b.Survivor); presetNames++; }
                foreach (var ev in b.Evolution) { power("preset " + b.Id + " evolution of", ev.Key, b.Survivor); power("preset " + b.Id + " evolution", ev.Value, b.Survivor); presetNames += 2; }
            }
            // knowledge.json (the defaults the mod writes)
            var k = Knowledge.FromJson(Knowledge.DefaultJson);
            var survivors = k.LeaderTier.Keys.Concat(k.RescueTier.Keys).Concat(k.CritSquad).Concat(k.WeaponBranch.Keys).Distinct().ToList();
            foreach (var s in survivors) survivor("knowledge survivor", s);
            foreach (var kv in k.WeaponBranch) power("knowledge weaponBranch", kv.Value, kv.Key);
            foreach (var a in k.AbilityTier.Keys) power("knowledge ability", a, null);
            var items = k.ItemTier.Keys.Concat(k.ItemNote.Keys).Concat(k.ItemPairs.SelectMany(p => new[] { p.Key, p.Value })).Concat(k.ScalingItems).Distinct().ToList();
            foreach (var i in items) item("knowledge item", i);
            // 0.16.0 (C16-02, C16-12): the item book's names - knowledge.json's own clashes, every row, the stated pairs and clashes, the
            // mixed effect's two items, the conversion items - and the revive abilities (C16-13's guard reads them) as power names
            var bookItems = k.ItemClashes.SelectMany(p => new[] { p.Key, p.Value }).Concat(ItemBook.All.Select(e => e.Name))
                .Concat(ItemBook.Pairs.SelectMany(p => p)).Concat(ItemBook.Clashes.SelectMany(p => p))
                .Concat(ItemBook.Mixed.SelectMany(m => new[] { m.Offered, m.Held })).Concat(ItemBook.Converts.Select(cv => cv[0])).Distinct().ToList();
            foreach (var i in bookItems) item("item book", i);
            foreach (var a in ItemBook.ReviveAbilities) power("item book", a, null);
            var assets = new HashSet<string>(fx.Powers.Select(p => p.Asset ?? ""), StringComparer.Ordinal);
            foreach (var m in k.MilitaryStat.Keys) if (!assets.Contains("MilitaryTraining_" + m)) miss.Add("knowledge militaryStats '" + m + "' (no MilitaryTraining_" + m + " card)");

            Console.WriteLine("  kits: " + Builds.Kits.Length + " survivors, " + weapons + " weapons, " + abilities + " abilities, " + evolutions + " evolutions - each the name of that survivor's powerup");
            Console.WriteLine("  presets: " + Builds.Presets.Count + " builds, " + presetNames + " names (branches, abilities and skips, evolution picks)");
            Console.WriteLine("  knowledge.json: " + survivors.Count + " survivors, " + k.WeaponBranch.Count + " weapon branches, " + k.AbilityTier.Count + " abilities, " + items.Count + " items; " + k.MilitaryStat.Count + " military stats by their card's asset");
            Console.WriteLine("  item book (0.16.0): " + bookItems.Count + " item names (" + ItemBook.All.Length + " rows, the stated pairs / clashes / mixed effect, the conversion items, knowledge.json's itemClashes), " + ItemBook.ReviveAbilities.Length + " revive abilit" + (ItemBook.ReviveAbilities.Length == 1 ? "y" : "ies"));
            var padded = fx.Powers.Where(p => p.Name != p.Name.Trim()).Select(p => "'" + p.Name + "'").Concat(fx.Items.Values.Where(n => n != n.Trim()).Select(n => "'" + n + "'")).ToList();
            if (padded.Count > 0) Console.WriteLine("  (" + padded.Count + " game name" + (padded.Count == 1 ? " carries" : "s carry") + " stray whitespace, compared trimmed: " + string.Join(", ", padded) + ")");
            foreach (var m in miss) Console.WriteLine("  NOT EXACT  " + m);
            if (fx.Version == null) { Console.WriteLine("  BAD  probe.json has no meta.gameVersion: the fixture must say which game build it is"); bad++; }
            bad += miss.Count;
            Console.WriteLine(miss.Count == 0 && fx.Version != null ? "  all names exact on " + fx.Version : "  FAIL: " + miss.Count + " name(s) not exact on " + on);
            return bad;
        }

        // ================================================================ A / R: the asset fallback and the run-time guard (the last section)
        public static int Run(string probePath, int exactBad)
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.15.0: the game-data drift guard - A the asset fallback after a rename, R the run-time check, its [data] / [builds] lines, the wiring");
            Fixture fx = null;
            try { fx = Load(probePath); } catch (Exception ex) { Check("A0", "probe.json readable", false, ex.Message); }
            if (fx != null)
            {
                Assets(fx);
                Fallback();
                Packs(fx);
                Guard(fx);
            }
            Sources();
            if (exactBad > 0) Console.WriteLine("  (the exact names above: " + exactBad + " off)");
            int all = _bad + exactBad;
            Console.WriteLine("  " + (all == 0 ? "all as wanted" : all + " BAD"));
            return _bad;
        }

        // ---------------------------------------------------------------- A1: the kits' asset keys = the fixture's
        static void Assets(Fixture fx)
        {
            var off = new List<string>(); int n = 0;
            foreach (var kit in Builds.Kits)
                foreach (var kv in Builds.KitNamesWithAssets(kit))
                {
                    n++;
                    var p = fx.Powers.FirstOrDefault(x => x.Name.Trim() == kv.Key && x.Class == kit.Survivor);
                    if (p == null || p.Asset != kv.Value) off.Add(kit.Survivor + " '" + kv.Key + "' = " + (kv.Value ?? "(none)") + ", the game: " + (p == null ? "(no such powerup)" : p.Asset));
                }
            Check("A1", "every kit name has the game's asset key (KitAssets, in kit order): " + n + " names, " + off.Count + " off", off.Count == 0 && n == 153, string.Join("; ", off.Take(6)));
        }

        // ---------------------------------------------------------------- A2 - A5: SameName, AssetOf, a rename taught by the live game
        static void Fallback()
        {
            Builds.ForgetLearnedAssets();
            Check("A2", "SameName by the asset key itself: 'Falcon: Assault' = 'Falcon-HuntingSweep', 'Animal Whistle' = 'Hunters Whistle', 'Shockspike' = 'CrossbowShockspikeUpgrade'",
                Builds.SameName("Falcon: Assault", "Falcon-HuntingSweep") && Builds.SameName("Hunters Whistle", "Animal Whistle") && Builds.SameName("Shockspike", "CrossbowShockspikeUpgrade"));
            Check("A2", "...and no false match: the two Falcon evolutions, two abilities, an ability and its evolution, two branches stay apart",
                !Builds.SameName("Falcon: Assault", "Falcon: Guardian") && !Builds.SameName("Incense", "Animal Whistle") && !Builds.SameName("Good Boy", "Good Boy: Retriever")
                && !Builds.SameName("Barber", "Flarebolt") && !Builds.SameAsset("Falcon", "Falcon: Guardian"));
            Check("A2", "the words first, as before: case and punctuation ('animal whistle: rally'), an evolution's short name ('Microbombs')",
                Builds.SameName("animal whistle: rally", "Animal Whistle: Rally") && Builds.SameName("Microbombs", "Kunai Dance: Microbombs"));
            Check("A3", "no 1.0.1 spelling is kept: 'Hunter's Whistle: Panic Whistle' has no asset and matches nothing (the live game teaches the names it uses now)",
                Builds.AssetOf("Hunter's Whistle: Panic Whistle") == null && !Builds.SameName("Hunter's Whistle: Panic Whistle", "Animal Whistle: Panic") && Builds.AssetOf("Animal Whistle: Panic") == "Hunters Whistle-Panic");
            Check("A3", "...but 'Good Boy: Doberman' spells the asset key 'Good Boy-Doberman' (case and punctuation aside), so it still meets 'Good Boy: Dobermann'",
                Builds.AssetOf("Good Boy: Doberman") == "Good Boy-Doberman" && Builds.SameName("Good Boy: Doberman", "Good Boy: Dobermann"));

            // a later patch renames an evolution and the ability itself (made-up names): the warm-up teaches the live names
            var beast = Builds.Presets.First(b => b.Id == "ranger-beastmaster");
            bool before = Builds.SameName("Good Boy: Pinscher", beast.EvolutionOf("Good Boy")) || beast.PriorityOf("Loyal Hound") >= 0;
            int taught = Builds.LearnAssets(new[] { new KeyValuePair<string, string>("Good Boy: Pinscher", "Good Boy-Doberman"), new KeyValuePair<string, string>("Loyal Hound", "Good Boy"),
                new KeyValuePair<string, string>("Falcon: Assault", "Falcon-HuntingSweep") });
            string pick = beast.EvolutionOf("Loyal Hound");
            Check("A4", "a rename taught (LearnAssets: 2 new names, 1 unchanged): the build's pick of the renamed ability is found, and it IS the renamed evolution on the card",
                !before && taught == 2 && pick == "Good Boy: Dobermann" && Builds.SameName(pick, "Good Boy: Pinscher") && !Builds.SameName(pick, "Good Boy: Retriever"),
                "before: " + (before ? "matched" : "no match") + ", taught " + taught + ", EvolutionOf('Loyal Hound') = " + (pick ?? "null"));
            Check("A4", "...its place in the order and the skips follow the asset too: PriorityOf('Loyal Hound') = 0 (Good Boy is the focus), a skip list naming 'Good Boy' skips 'Loyal Hound'",
                beast.PriorityOf("Loyal Hound") == 0 && new Build { Skip = { "Good Boy" } }.Skips("Loyal Hound") && !new Build { Skip = { "Falcon" } }.Skips("Loyal Hound"));
            Builds.ForgetLearnedAssets();
            Check("A5", "ForgetLearnedAssets: back to the kits' own keys (the made-up names match nothing again)",
                !Builds.SameName("Good Boy: Pinscher", "Good Boy: Dobermann") && beast.PriorityOf("Loyal Hound") < 0 && Builds.SameName("Falcon: Assault", "Falcon-HuntingSweep"));
        }

        // ---------------------------------------------------------------- A6 / R4: a lent pack's names after a rename
        static void Packs(Fixture fx)
        {
            var warns = new List<string>();
            var p = Builds.ParsePack("{ \"title\": \"Bench pack\", \"builds\": [ { \"id\": \"keys\", \"name\": \"By keys\", \"branch\": \"CrossbowShockspikeUpgrade\", \"abilities\": [\"Hunters Whistle\", \"Falcon\"],"
                + " \"evolution\": { \"Falcon\": \"Falcon-HuntingSweep\", \"Hunters Whistle\": \"Hunters Whistle-Panic\" } },"
                + " { \"id\": \"old\", \"name\": \"Old names\", \"abilities\": [\"Good Boy\"], \"evolution\": { \"Good Boy\": \"Good Boy: Doberman\", \"Falcon\": \"Falcon: Guardian Falcon\", \"Animal Whistle\": \"Hunter's Whistle: Panic Whistle\" } } ] }",
                "bench-owner", "Ranger", warns.Add);
            var keys = p.Builds[0]; var old = p.Builds[1];
            Check("A6", "a pack naming asset keys fits in the kit's spelling: branch Shockspike, abilities Animal Whistle + Falcon, evolutions Falcon: Assault / Animal Whistle: Panic",
                keys.Branch == "Shockspike" && string.Join(",", keys.Abilities) == "Animal Whistle,Falcon" && keys.EvolutionOf("Falcon") == "Falcon: Assault" && keys.EvolutionOf("Animal Whistle") == "Animal Whistle: Panic",
                keys.Branch + " | " + string.Join(",", keys.Abilities) + " | " + string.Join(", ", keys.Evolution.Select(kv => kv.Key + " -> " + kv.Value)));
            Check("A6", "...1.0.1 names: 'Good Boy: Doberman' fits by its asset spelling, the other two fit nothing - decided live, said at parse time, kept as unmatched",
                old.Evolution.Count == 1 && old.EvolutionOf("Good Boy") == "Good Boy: Dobermann" && string.Join(" | ", p.Unmatched) == "Falcon: Guardian Falcon | Hunter's Whistle: Panic Whistle"
                && warns.Count(w => w.Contains("is not an evolution of")) == 2,
                "picks: " + string.Join(", ", old.Evolution.Select(kv => kv.Key + " -> " + kv.Value)) + " | unmatched: " + string.Join(", ", p.Unmatched) + " | " + warns.Count + " warning(s)");

            // the run-time guard says the pack's names the game does not have, once a session
            var oldLogger = Builds.Logger; var oldInGame = Builds.InGame; var said = new List<string>();
            try
            {
                Builds.Logger = s => said.Add("[builds] " + s);
                Builds.InGame = null;
                int early = Builds.SayNotInGame(p);
                var live = new HashSet<string>(fx.Powers.Select(x => x.Asset ?? ""), StringComparer.Ordinal);
                Builds.LearnAssets(fx.Powers.Where(x => x.Asset != null).Select(x => new KeyValuePair<string, string>(x.Name.Trim(), x.Asset)));
                Builds.InGame = n => { string k = Builds.AssetOf(n); return k != null && live.Contains(k); };
                int first = Builds.SayNotInGame(p), again = Builds.SayNotInGame(p);
                Check("R4", "a lent pack's unmatched names: nothing before the game's names are read, then one '[builds] pack <owner>: <name> is not in the game' per name, once a session",
                    early == 0 && first == 2 && again == 0 && said.Count == 2 && said[0] == "[builds] pack bench-owner: Falcon: Guardian Falcon is not in the game",
                    string.Join(" | ", said));
                Check("R4", "InGame: the game's names (Good Boy: Dobermann, 'Holo-bait: Laser' trimmed), asset keys and items not (Wooden Stick is an item, not a powerup)",
                    Builds.InGame("Good Boy: Dobermann") && Builds.InGame("Holo-bait: Laser") && Builds.InGame("Falcon-HuntingSweep") && !Builds.InGame("Hunter's Whistle: Panic Whistle") && !Builds.InGame("Wooden Stick"));

                // the player's own build, copied from a preset before the patch: said by name
                string tmp = Path.Combine(Path.GetTempPath(), "yazs_bench_drift_builds_" + Environment.ProcessId + ".json");
                Builds.Load(tmp);
                Builds.Parse("{ \"custom\": { \"Ranger\": { \"id\": \"custom-ranger\", \"name\": \"My Ranger\", \"abilities\": [\"Good Boy\", \"Falcon\"], \"evolution\": { \"Good Boy\": \"Good Boy: Doberman\", \"Falcon\": \"Falcon: Guardian Falcon\" } } } }");
                var mine = Builds.CustomNotInGame(Builds.InGame);
                Check("R5", "the player's own build (builds.json) with two 1.0.1 evolutions: 'Falcon: Guardian Falcon' named, 'Good Boy: Doberman' not (its asset spelling is in the game)",
                    mine.Count == 1 && mine[0] == "Ranger: 'Falcon: Guardian Falcon'" && Builds.CustomNotInGame(null).Count == 0, string.Join(", ", mine));
                Builds.Load(tmp);
            }
            finally { Builds.Logger = oldLogger; Builds.InGame = oldInGame; Builds.ForgetLearnedAssets(); }
        }

        // ---------------------------------------------------------------- R1 - R3: Builds.NotInGame over the fixture
        static void Guard(Fixture fx)
        {
            var game = Kits(fx);
            var line = new List<string>();
            var miss = Builds.NotInGame(game, line);
            Check("R1", "the fixture as the live walk reads it (" + game.Count + " classes, the weapon line by previousWeapon): nothing missing, every line as the kits have it",
                miss.Count == 0 && line.Count == 0 && game.Count == 9, string.Join("; ", miss.Concat(line)));

            // a later game build renames an evolution (made-up name): named with the game's new name for its asset
            var renamed = Kits(fx); var ranger = renamed.First(g => g.Survivor == "Ranger");
            ranger.Assets.Remove("Good Boy: Dobermann"); ranger.Assets["Good Boy: Pinscher"] = "Good Boy-Doberman";
            ranger.Assets.Remove("Incense: Bad Juju");                                 // ... and drops another outright
            var m2 = Builds.NotInGame(renamed);
            Check("R2", "a renamed and a dropped evolution: '[data] 2 names ... not in this game build: Good Boy: Dobermann (the game: 'Good Boy: Pinscher'), Incense: Bad Juju'",
                m2.Count == 2 && m2[0] == "Good Boy: Dobermann (the game: 'Good Boy: Pinscher')" && m2[1] == "Incense: Bad Juju", string.Join(", ", m2));

            // the weapon line moved: Flarebolt follows the Crossbow, not the Repeater Crossbow
            var moved = Kits(fx); var r = moved.First(g => g.Survivor == "Ranger"); r.Branches.Remove("Flarebolt");
            var l3 = new List<string>(); var m3 = Builds.NotInGame(moved, l3);
            Check("R3", "a weapon the game links elsewhere: no name missing, the line differs ('Ranger: Flarebolt does not follow Repeater Crossbow')",
                m3.Count == 0 && l3.Count == 1 && l3[0] == "Ranger: Flarebolt does not follow Repeater Crossbow", string.Join("; ", l3));
            var partial = Kits(fx).Where(g => g.Survivor != "Mechanic").ToList();
            Check("R3", "a class without data is not judged (no false alarm): 8 classes, nothing missing", Builds.NotInGame(partial).Count == 0 && partial.Count == 8);
        }

        // ---------------------------------------------------------------- R6: the wiring
        static void Sources()
        {
            string gs = Src("GameState.cs") ?? "", wu = Src("Warmup.cs") ?? "", rk = Src("Ranker.cs") ?? "", bp = Src("BuildPresets.cs") ?? "";
            Check("R6", "GameState.cs DataDrift: the live walk (weaponPowerups + previousLevelWeapon, abilityBasePowerups + both evolutions), LearnAssets, the one Warning, the packs and the player's builds",
                gs.Contains("internal static class DataDrift") && gs.Contains("cp.weaponPowerups") && gs.Contains("previousLevelWeapon") && gs.Contains("cp.abilityBasePowerups")
                && gs.Contains("abilityEvolutionA") && gs.Contains("Builds.LearnAssets(") && gs.Contains("LogWarning(\"[data] \" + miss.Count + \" names in the Companion's tables are not in this game build: \"")
                && gs.Contains("Builds.SayNotInGame(") && gs.Contains("Builds.CustomNotInGame("));
            Check("R6", "Warmup.cs runs it once a session on the main menu (the last warm-up stage, later visits when that could not), never in a run",
                wu.Contains("DataDrift.Run()") && wu.Contains("if (_done) { if (!DataDrift.Done) DataLater(); return; }") && wu.Contains("if (!Menu.OnMainMenu || Menu.IsOpen)"));
            Check("R6", "Ranker.SameName is Builds.SameName (the ranking's every name test gets the asset fallback); the presets carry no 1.0.1 Ranger name",
                rk.Contains("internal static bool SameName(string a, string b) { return Builds.SameName(a, b); }")
                && !bp.Contains("Good Boy: Doberman\"") && !bp.Contains("Guardian Falcon") && !bp.Contains("Hunter's Whistle"));
        }

        // ================================================================ the tools: --live-probe <path>, --packs <folder>
        public static int Tools(string[] args, int liveAt, int packsAt)
        {
            int pi = Array.IndexOf(args, "--probe");
            string fixture = pi >= 0 && pi + 1 < args.Length ? args[pi + 1] : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "data", "probe.json"));
            if (!File.Exists(fixture)) { Console.Error.WriteLine("probe.json (the fixture) not found: " + fixture + " (pass --probe <path>)"); return 2; }
            var fx = Load(fixture);
            int code = 0;
            if (liveAt >= 0) code = Math.Max(code, LiveDiff(fx, liveAt + 1 < args.Length ? args[liveAt + 1] : null));
            if (packsAt >= 0) code = Math.Max(code, PackCounts(fx, packsAt + 1 < args.Length ? args[packsAt + 1] : null));
            return code;
        }

        static int LiveDiff(Fixture fx, string path)
        {
            if (path == null || !File.Exists(path)) { Console.Error.WriteLine("--live-probe: no file " + (path ?? "(none given)")); return 2; }
            Fixture live; try { live = Load(path); } catch (Exception ex) { Console.Error.WriteLine("--live-probe: unreadable (" + ex.Message + ")"); return 2; }
            Console.WriteLine("=== the fixture (" + (fx.Version ?? "no gameVersion") + ") against the dump given (" + (live.Version ?? "no gameVersion") + "): names and descriptions by asset");
            Func<string, string> q = s => s == null ? "(none)" : "'" + s + "'";
            var a = fx.Powers.Where(p => p.Asset != null).GroupBy(p => p.Asset).ToDictionary(g => g.Key, g => g.First());
            var b = live.Powers.Where(p => p.Asset != null).GroupBy(p => p.Asset).ToDictionary(g => g.Key, g => g.First());
            var lines = new List<string>(); int renamed = 0, desc = 0, irenamed = 0, idesc = 0;
            foreach (var kv in a.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                Power o; if (!b.TryGetValue(kv.Key, out o)) { lines.Add("  ONLY IN THE FIXTURE  powerup " + kv.Key + " " + q(kv.Value.Name)); continue; }
                if (o.Name != kv.Value.Name) { renamed++; lines.Add("  RENAMED  powerup " + kv.Key + ": " + q(kv.Value.Name) + " -> " + q(o.Name)); }
                if (o.Desc != kv.Value.Desc) { desc++; lines.Add("  DESC     powerup " + kv.Key + " " + q(o.Name) + ": " + q(kv.Value.Desc) + " -> " + q(o.Desc)); }
            }
            foreach (var kv in b.Where(x => !a.ContainsKey(x.Key)).OrderBy(x => x.Key, StringComparer.Ordinal)) lines.Add("  ONLY IN THE DUMP  powerup " + kv.Key + " " + q(kv.Value.Name));
            foreach (var kv in fx.Items.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                string n; if (!live.Items.TryGetValue(kv.Key, out n)) { lines.Add("  ONLY IN THE FIXTURE  item " + kv.Key + " " + q(kv.Value)); continue; }
                if (n != kv.Value) { irenamed++; lines.Add("  RENAMED  item " + kv.Key + ": " + q(kv.Value) + " -> " + q(n)); }
                string d0 = fx.ItemDesc[kv.Key], d1 = live.ItemDesc[kv.Key];
                if (d0 != d1) { idesc++; lines.Add("  DESC     item " + kv.Key + " " + q(n) + ": " + q(d0) + " -> " + q(d1)); }
            }
            foreach (var kv in live.Items.Where(x => !fx.Items.ContainsKey(x.Key)).OrderBy(x => x.Key, StringComparer.Ordinal)) lines.Add("  ONLY IN THE DUMP  item " + kv.Key + " " + q(kv.Value));
            Console.WriteLine("  powerups: " + a.Count + " in the fixture, " + b.Count + " in the dump; " + renamed + " renamed, " + desc + " descriptions changed");
            Console.WriteLine("  items: " + fx.Items.Count + " in the fixture, " + live.Items.Count + " in the dump; " + irenamed + " renamed, " + idesc + " descriptions changed");
            foreach (var l in lines) Console.WriteLine(l);
            if (lines.Count == 0) Console.WriteLine("  the same names and descriptions");
            return 0;
        }

        // every lent build pack under a folder: what fits the kits and what the fixture's game has - counts only (no titles, names or paths)
        static int PackCounts(Fixture fx, string dir)
        {
            if (dir == null || !Directory.Exists(dir)) { Console.Error.WriteLine("--packs: no folder " + (dir ?? "(none given)")); return 2; }
            Console.WriteLine("=== lent build packs under the folder given, against the kits and the fixture (" + (fx.Version ?? "no gameVersion") + "): counts only");
            var live = new HashSet<string>(fx.Powers.Select(x => x.Asset ?? ""), StringComparer.Ordinal);
            Builds.LearnAssets(fx.Powers.Where(x => x.Asset != null).Select(x => new KeyValuePair<string, string>(x.Name.Trim(), x.Asset)));
            Func<string, bool> inGame = n => { string k = Builds.AssetOf(n); return k != null && live.Contains(k); };
            int json = 0, other = 0, unreadable = 0, packs = 0, found = 0, builds = 0, defaults = 0, defaultsFound = 0;
            int given = 0, exact = 0, other2 = 0, dropped = 0, notInGame = 0;
            var withDrops = new List<string>();
            foreach (var file in Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                json++;
                string text; JsonDocument doc;
                try { text = File.ReadAllText(file); doc = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
                catch { unreadable++; continue; }
                var names = new List<string>(); bool isPack = false; string wanted = null;
                using (doc)
                {
                    var root = doc.RootElement; JsonElement v;
                    if (root.ValueKind != JsonValueKind.Object) { other++; continue; }
                    var list = new List<JsonElement>();
                    if (root.TryGetProperty("builds", out v) && v.ValueKind == JsonValueKind.Array) { foreach (var o in v.EnumerateArray()) if (o.ValueKind == JsonValueKind.Object) list.Add(o); isPack = list.Count > 0; }
                    else if (root.TryGetProperty("abilities", out v) || root.TryGetProperty("branch", out v)) { list.Add(root); isPack = true; }
                    if (!isPack) { other++; continue; }
                    if (root.TryGetProperty("default", out v) && v.ValueKind == JsonValueKind.String && (v.GetString() ?? "").Length > 0) wanted = v.GetString();
                    foreach (var o in list)
                    {
                        if (o.TryGetProperty("branch", out v) && v.ValueKind == JsonValueKind.String && (v.GetString() ?? "").Length > 0) names.Add(v.GetString());
                        foreach (var key in new[] { "abilities", "skip" }) if (o.TryGetProperty(key, out v) && v.ValueKind == JsonValueKind.Array) foreach (var a in v.EnumerateArray()) if (a.ValueKind == JsonValueKind.String) names.Add(a.GetString());
                        if (o.TryGetProperty("evolution", out v) && v.ValueKind == JsonValueKind.Object) foreach (var p in v.EnumerateObject()) { names.Add(p.Name); if (p.Value.ValueKind == JsonValueKind.String) names.Add(p.Value.GetString()); }
                    }
                }
                packs++;
                // the survivor: the kit most of the pack's names belong to (a pack file does not say; its mod does)
                Kit best = null; int bestScore = 0;
                foreach (var kit in Builds.Kits)
                {
                    int s = names.Count(n => kit.Abilities.Any(a => a.Any(x => string.Equals(x, (n ?? "").Trim(), StringComparison.OrdinalIgnoreCase) || Builds.SameAsset(x, n))) || kit.BranchNamed(n) != null);
                    if (s > bestScore) { bestScore = s; best = kit; }
                }
                if (best == null) { withDrops.Add("#" + packs + " (no survivor)"); continue; }
                found++;
                var pack = Builds.ParsePack(text, "pack" + packs, best.Survivor);
                builds += pack.Builds.Count;
                if (wanted != null) { defaults++; if (pack.Default != null) defaultsFound++; }
                foreach (var n in names)
                {
                    given++;
                    if (fx.HasPower(n, best.Survivor)) exact++;
                    else if (pack.Unmatched.Contains((n ?? "").Trim(), StringComparer.OrdinalIgnoreCase)) { dropped++; if (!inGame(n)) notInGame++; }
                    else other2++;
                }
                if (pack.Unmatched.Count > 0) withDrops.Add("#" + packs + " (" + pack.Unmatched.Count + ")");
            }
            Builds.ForgetLearnedAssets();
            Console.WriteLine("  files: " + json + " JSON, " + packs + " build pack(s), " + other + " other, " + unreadable + " unreadable");
            Console.WriteLine("  survivors found by the packs' names: " + found + " of " + packs + "; builds: " + builds + "; defaults named " + defaults + ", found " + defaultsFound);
            Console.WriteLine("  names given: " + given + " - " + exact + " exact on " + (fx.Version ?? "the fixture") + ", " + other2 + " fit another way (case, a short evolution name, an asset key), "
                + dropped + " fit nothing (dropped; " + notInGame + " not in the game)");
            if (withDrops.Count > 0) Console.WriteLine("  packs (in file order) with names that fit nothing: " + string.Join(", ", withDrops));
            bool ok = withDrops.Count == 0 && found == packs && defaults == defaultsFound;
            Console.WriteLine(ok ? "  every lent name fits" : "  NOT ALL LENT NAMES FIT");
            return ok ? 0 : 3;
        }
    }
}
