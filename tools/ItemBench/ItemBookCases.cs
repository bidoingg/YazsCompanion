// 0.16.0 (C16-02, C16-12): the item book (ItemBook.cs) - a rule per chest item of game 1.0.2: IB1 coverage against the probe's pool,
// IB2 the rows themselves, IB3 the table in four reference squads against itembook_expected.txt, IB4 the 0.15.0 mis-hits guarded,
// IB5 the needs, IB6 pairs / clashes / the mixed effect, IB7 the logged chest offers, IB8 the keyword fallback, IB9 the revive need.
// RunPure (IB2, IB8a, IB9, IB9b, the [ctx] tail, the sources - no game data) runs in the full bench (from Run) and in --no-data
// (Verdict.DataFree); Run adds the data cases over the probe's items; WriteExpected is 'ItemBench --write-itembook' (Program.cs).
// The four reference squads (A SWAT + Engineer 10:00, B Pyro + Medic + Mechanic 05:00, C Huntress + Ghost 05:00, D Tank solo 18:30)
// are the ones the item audit measured (research\roadmap_1007\scratch\c16_item_audit\proto_r\Program.cs).
// Game names only (the repository is public); the build names are the bench's own.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class ItemBookCases
    {
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string F(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        static bool Near(double a, double b) { return Math.Abs(a - b) <= 0.01 + 1e-9; }
        static string Show(IEnumerable<string> l) { var x = l.ToList(); return x.Count == 0 ? "none" : string.Join(", ", x); }
        static string Src(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod", file));
            return File.Exists(p) ? File.ReadAllText(p).Replace("\r\n", "\n") : null;
        }
        static string BenchFile(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", file));
            return File.Exists(p) ? File.ReadAllText(p).Replace("\r\n", "\n") : null;
        }
        /// <summary>The source copy first (what --write-itembook rewrites), else the one the build copied next to the bench.</summary>
        static string ExpectedPath()
        {
            foreach (var p in new[] { SourceExpected(), Path.Combine(AppContext.BaseDirectory, "itembook_expected.txt") })
                if (File.Exists(p)) return p;
            return null;
        }
        static string SourceExpected() { return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "itembook_expected.txt")); }

        // ================================================================ the reference squads
        static Knowledge _k;
        static Knowledge K { get { return _k ?? (_k = Knowledge.FromJson(Knowledge.DefaultJson)); } }
        static readonly string[] SquadNames = { "A SWAT+Engineer 10:00", "B Pyro+Medic+Mechanic 05:00", "C Huntress+Ghost 05:00", "D Tank solo 18:30" };

        static ItemContext Ctx(Knowledge k, double seconds, string[] squad, Action<TagProfile> fill, string[] owned, double close, double far, double clip, double lean)
        {
            var tags = new TagProfile { SpecialAt = 10 }; fill(tags);
            var run = new RunContext { Mode = "Normal", Goal = 1200, Seconds = seconds, LevelRate = 2.2, LevelUps = (int)(seconds / 25), D = new Doctrine() };
            var c = new ItemContext { Squad = squad, Tags = tags, K = k, Ctx = run, OwnedTags = new HashSet<string>(owned, StringComparer.OrdinalIgnoreCase), Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase) };
            c.CloseShare = close; c.LongShare = far; c.ClipShare = clip; c.AbilityLean = lean; c.CritSquad = squad.Any(x => k.CritSquad.Contains(x, StringComparer.OrdinalIgnoreCase));
            c.ShieldOwned = squad.Contains("Engineer");
            return c;
        }
        static ItemContext Live(ItemContext c, double free, double luck, double pickup, double move) { c.FreeSlots = free; c.Luck = luck; c.Pickup = pickup; c.MoveSpeed = move; return c; }

        /// <summary>A fresh context of reference squad <paramref name="i"/> (0 A .. 3 D).</summary>
        static ItemContext Squad(int i, Knowledge k = null)
        {
            k = k ?? K;
            switch (i)
            {
                case 0:
                    return Live(Ctx(k, 600, new[] { "SWAT", "Engineer" }, t => { t.Source("Assault Rifle", 1.0, new[] { "Kinetic" }); t.Source("Helicopter Strike", 0.4, new[] { "Kinetic" }); t.Source("Tesla", 0.7, new[] { "Electric" }); t.Points["Kinetic"] = 8; t.Points["Electric"] = 4; },
                        new[] { "Deployable", "Turret" }, 0.5, 0, 1, 0.4), 3, 10, 1.3, 100);
                case 1:
                    return Live(Ctx(k, 300, new[] { "Pyro", "Medic", "Mechanic" }, t => { t.Source("Flamethrower", 1.0, new[] { "Fire" }); t.Source("Molotov", 0.5, new[] { "Fire" }); t.Source("Syringe Rifle", 1.0, new[] { "Chemical" }); t.Source("Nitro-Gun", 1.0, new[] { "Ice" }); t.Points["Fire"] = 12; t.Points["Chemical"] = 2; t.Points["Ice"] = 6; },
                        new[] { "Grenade" }, 0.67, 0.0, 0.33, 0.5), 5, 0, 1.0, 100);
                case 2:
                    return Live(Ctx(k, 300, new[] { "Huntress", "Ghost" }, t => { t.Source("Freezing Arrows", 1.0, new[] { "Ice" }); t.Source("Bear Trap", 0.5, new[] { "Kinetic" }); t.Source("Thousand Cuts", 1.0, new[] { "Slashing" }); t.Source("Shuriken", 0.5, new[] { "Slashing" }); t.Points["Slashing"] = 9; t.Points["Ice"] = 5; },
                        new[] { "Deployable", "Taunt", "Melee" }, 0.5, 0.5, 0.5, 0.5), 4, 20, 1.2, 110);
                default:
                    return Live(Ctx(k, 1110, new[] { "Tank" }, t => { t.Source("Rocket Launcher", 1.0, new[] { "Explosive" }); t.Source("Minefield", 0.5, new[] { "Explosive" }); t.Points["Explosive"] = 14; },
                        new[] { "Deployable" }, 0, 1, 1, 0.3), 1, 30, 1.5, 100);
            }
        }

        sealed class Cell { public double Score; public string Line; public List<string> Why; }

        /// <summary>One item in one context, as a chest card: the score, the card's line (Wording.Item) and the reasons.</summary>
        static Cell Eval(string name, string desc, IList<string> stats, bool healing, ItemContext c)
        {
            c.Stats = stats; c.Healing = healing; c.Say = new ItemSay();
            var why = new List<string>();
            double s = ItemRules.Evaluate(name, desc, c, why);
            return new Cell { Score = s, Line = Wording.Item(c.Say), Why = why };
        }
        static Cell Eval(ProbeItem it, ItemContext c) { return Eval(it.Name, it.Desc, it.Stats, it.Healing, c); }

        // ================================================================ the table (IB3 and --write-itembook)
        /// <summary>name|A|B|C|D|lineA|lineC for every probe item the book names, in name order.</summary>
        static List<string> Table(IEnumerable<ProbeItem> items)
        {
            var rows = new List<string>();
            foreach (var it in items.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (ItemBook.Of(it.Name) == null) continue;
                var cells = new List<string>(); string lineA = null, lineC = null;
                for (int i = 0; i < 4; i++)
                {
                    var cell = Eval(it, Squad(i));
                    cells.Add(F(cell.Score));
                    if (i == 0) lineA = cell.Line; if (i == 2) lineC = cell.Line;
                }
                rows.Add(it.Name + "|" + string.Join("|", cells) + "|" + lineA + "|" + lineC);
            }
            return rows;
        }
        const string TableHead = "# ItemBench IB3: the item book's score per reference squad (A B C D) and the card line in A and C. Regenerate: ItemBench --write-itembook\n# name|A|B|C|D|lineA|lineC\n";

        static List<ProbeItem> LoadProbe(string path)
        {
            var items = new List<ProbeItem>();
            using (var doc = JsonDocument.Parse(File.ReadAllText(path)))
                foreach (var it in doc.RootElement.GetProperty("items").EnumerateArray())
                {
                    var pi = new ProbeItem { Name = it.GetProperty("name").GetString() };
                    JsonElement e;
                    if (it.TryGetProperty("desc", out e)) pi.Desc = e.GetString();
                    if (it.TryGetProperty("healing", out e)) pi.Healing = e.GetBoolean();
                    if (it.TryGetProperty("inPool", out e) && (e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.False)) pi.InPool = e.GetBoolean();
                    if (it.TryGetProperty("animal", out e) && (e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.False)) pi.Animal = e.GetBoolean();
                    if (it.TryGetProperty("stats", out e)) foreach (var t in e.EnumerateArray()) pi.Stats.Add(t.GetString());
                    items.Add(pi);
                }
            return items;
        }

        /// <summary>'ItemBench --write-itembook [--probe &lt;probe.json&gt;]': rewrite tools\ItemBench\itembook_expected.txt from the IB3 run
        /// (AppContext.BaseDirectory\..\..\..\itembook_expected.txt) and exit 0 (2: no probe).</summary>
        public static int WriteExpected(string[] args)
        {
            int pi = Array.IndexOf(args, "--probe");
            string probe = pi >= 0 && pi + 1 < args.Length ? args[pi + 1] : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "data", "probe.json"));
            if (!File.Exists(probe)) { Console.Error.WriteLine("--write-itembook: probe.json not found: " + probe + " (pass --probe <file>) - nothing written"); return 2; }
            var rows = Table(LoadProbe(probe));
            string path = SourceExpected();
            File.WriteAllText(path, TableHead + string.Join("\n", rows) + "\n");      // LF: the file reads the same on every checkout
            Console.WriteLine("--write-itembook: " + rows.Count + " rows from " + probe + " -> " + path);
            return 0;
        }

        // ================================================================ the full bench
        /// <summary>The full bench (Checks.Run, after WordingCases): the pure part first, then the cases over the probe's items.</summary>
        public static int Run(List<ProbeItem> items)
        {
            int bad = RunPure();
            _bad = 0;
            Console.WriteLine("\n=== 0.16.0: the item book over the 1.0.2 probe's items - IB1, IB3 - IB7, IB8b (C16-02, C16-12)");
            var byName = new Dictionary<string, ProbeItem>(StringComparer.Ordinal);
            foreach (var it in items) byName[it.Name] = it;
            Coverage(items, byName);
            Parity(items);
            MisHits(byName);
            Needs(byName);
            PairsClashes(byName);
            Offers(byName);
            Keywords(byName);
            Console.WriteLine("  item book (probe): " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return bad + _bad;
        }

        static ProbeItem Item(Dictionary<string, ProbeItem> byName, string name)
        {
            ProbeItem it;
            if (byName.TryGetValue(name, out it)) return it;
            Check("--", "probe item '" + name + "'", false, "not in the probe");
            return new ProbeItem { Name = name, Desc = "" };
        }

        // ---------------------------------------------------------------- IB1
        static void Coverage(List<ProbeItem> items, Dictionary<string, ProbeItem> byName)
        {
            var pool = items.Where(i => i.InPool && !i.Name.StartsWith("Powerups/", StringComparison.Ordinal)).ToList();
            var missing = pool.Where(i => ItemBook.Of(i.Name) == null).Select(i => i.Name).ToList();
            var stray = ItemBook.All.Where(e => !byName.ContainsKey(e.Name)).Select(e => e.Name).ToList();
            var outside = ItemBook.All.Where(e => byName.ContainsKey(e.Name) && !byName[e.Name].InPool).Select(e => e.Name).ToList();
            Check("IB1", "every named pool item of the probe has a rule, every rule names a probe item letter for letter", missing.Count == 0 && stray.Count == 0,
                ItemBook.All.Length + " rules: " + pool.Count + " pool items + " + Show(outside) + "; pool items without a rule: " + Show(missing) + "; rules naming no probe item: " + Show(stray));
        }

        // ---------------------------------------------------------------- IB3
        static void Parity(List<ProbeItem> items)
        {
            var mine = Table(items);
            string path = ExpectedPath();
            if (path == null) { Check("IB3", "parity file itembook_expected.txt", false, "not found"); return; }
            var want = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var ln in File.ReadAllText(path).Replace("\r\n", "\n").Split('\n'))
            {
                if (ln.Length == 0 || ln.StartsWith("#", StringComparison.Ordinal)) continue;
                var f = ln.Split('|'); if (f.Length == 7) want[f[0]] = f;
            }
            var diff = new List<string>(); int same = 0, near = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in mine)
            {
                var f = row.Split('|'); seen.Add(f[0]);
                string[] w;
                if (!want.TryGetValue(f[0], out w)) { diff.Add(f[0] + ": not in the file"); continue; }
                bool numbers = true, exact = true;
                for (int i = 1; i <= 4; i++)
                {
                    double a = double.Parse(f[i], CultureInfo.InvariantCulture), b = double.Parse(w[i], CultureInfo.InvariantCulture);
                    if (!Near(a, b)) numbers = false; else if (f[i] != w[i]) exact = false;
                }
                bool lines = f[5] == w[5] && f[6] == w[6];
                if (numbers && lines) { if (exact) same++; else near++; }
                else diff.Add(row + "  (file: " + string.Join("|", w.Skip(1)) + ")");
            }
            foreach (var name in want.Keys) if (!seen.Contains(name)) diff.Add(name + ": in the file, no rule scored");
            foreach (var d in diff.Take(20)) Console.WriteLine("    DIFF " + d);
            Check("IB3", "the table in the four reference squads equals itembook_expected.txt (lines equal, numbers within 0.01)", diff.Count == 0 && mine.Count == 134,
                mine.Count + " rows: " + same + " identical, " + near + " within 0.01, " + diff.Count + " different");
        }

        // ---------------------------------------------------------------- IB4
        static void MisHits(Dictionary<string, ProbeItem> byName)
        {
            var own = new[] { "Accumulator", "Annoying Trumpet", "Barrel Roll", "Suspicious Pendrive", "Great Nade", "Boiling Pot" };
            var cells = new Dictionary<string, List<Cell>>(StringComparer.Ordinal);
            Func<string, List<Cell>> all = n =>
            {
                List<Cell> l; if (cells.TryGetValue(n, out l)) return l;
                var it = Item(byName, n); l = new List<Cell>(); for (int i = 0; i < 4; i++) l.Add(Eval(it, Squad(i)));
                return cells[n] = l;
            };
            Action<string, string[], Func<string, bool>> why = (label, names, hit) =>
            {
                var hits = names.SelectMany(n => all(n).SelectMany(c => c.Why).Where(hit).Select(w => n + ": '" + w + "'")).Distinct().ToList();
                Check("IB4", label, hits.Count == 0, hits.Count == 0 ? names.Length + " item(s) x 4 squads" : Show(hits));
            };
            Action<string, string[]> line = (words, names) =>
            {
                var hits = names.SelectMany(n => all(n).Select((c, i) => new { n, i, c.Line })).Where(x => (x.Line ?? "").Contains(words)).Select(x => x.n + " (" + SquadNames[x.i].Substring(0, 1) + "): '" + x.Line + "'").ToList();
                Check("IB4", "no card line '" + words + "' under " + string.Join(", ", names), hits.Count == 0, hits.Count == 0 ? "4 squads" : Show(hits));
            };
            why("no 'damage' reason for an item that adds no damage to the squad's", new[] { "Plot Armor", "Power of Friendship", "Omnigeode", "99'th Balloon" }.Concat(own).ToArray(), w => w == "damage");
            why("no 'Medic: healing' for a max-HP / revive / cost item", new[] { "Chocolate Box", "Devil's Deal", "Electric Personality", "Frozen Heart", "Glass of Milk", "Jewel of Life", "Plot Armor", "Pills", "Last Unicorn" }, w => w == "Medic: healing");
            why("no 'elites/bosses' reason for The Word, Frying Pan, Sugar Rush", new[] { "The Word", "Frying Pan", "Sugar Rush" }, w => w.Contains("elites/bosses"));
            why("no 'slow' reason for Hit Tracks (slows ON YOU halved)", new[] { "Hit Tracks" }, w => w.EndsWith(": slow", StringComparison.Ordinal));
            why("no 'dodge' reason for Fishing Pole (a parry is not a dodge)", new[] { "Fishing Pole" }, w => w.EndsWith(": dodge", StringComparison.Ordinal));
            why("no 'upgrade quality' reason for A Cookie, Black Box, Hijacked Signal, Jailbroken Phone", new[] { "A Cookie", "Black Box", "Hijacked Signal", "Jailbroken Phone" }, w => w.StartsWith("upgrade quality", StringComparison.Ordinal));
            why("no 'no grenade on the squad' for Great Nade (its grenade is its own)", new[] { "Great Nade" }, w => w == "no grenade on the squad");
            why("no 'no Electric / Explosive damage on the squad' for the six own-damage items", own, w => w == "no Electric damage on the squad" || w == "no Explosive damage on the squad");
            why("no 'pickups' reason for Pills, Metal Gear, Wrench, Nuclear Fusion", new[] { "Pills", "Metal Gear", "Wrench", "Nuclear Fusion" }, w => w.StartsWith("pickups", StringComparison.Ordinal));
            why("no 'economy' gain for Gold Medal (a cost), Farming Tools, Potato", new[] { "Gold Medal", "Farming Tools", "Potato" }, w => w == "economy" || w.StartsWith("economy:", StringComparison.Ordinal));
            why("no 'cash' gain for Life Savings (cash heals instead)", new[] { "Life Savings" }, w => w == "cash" || w.StartsWith("cash:", StringComparison.Ordinal));
            why("no malus read into Bulletproof Vest ('Enemy projectiles deal -50%')", new[] { "Bulletproof Vest" }, w => w.StartsWith("costs ", StringComparison.Ordinal) || w.StartsWith("hurts ", StringComparison.Ordinal));
            line("More crits", new[] { "Modchip", "Solar Panel", "Power Generator" });
            line("Slows enemies", new[] { "Broken Glass", "ACME Anvil", "Hit Tracks" });
            line("More dodge", new[] { "Barrel Roll", "Emerald Gem", "Fishing Pole" });
            line("Stronger status effects", new[] { "Gaslighter" });
            line("Boosts your taunts", new[] { "Jailbroken Phone" });
            line("More damage", new[] { "Plot Armor", "Power of Friendship", "Omnigeode" });
            // DK-C02 (a Deck session's wrong live reasons): in the four squads and on the rebuilt Deck chest (a weapons /
            // Kinetic / Electric build followed) Farming Tools never reads XP and luck, Hit Tracks never slows enemies, Last Unicorn
            // never fits a weapons build, Pocket Watch is never 'nothing in it'
            foreach (var x in new[] { Tuple.Create("Farming Tools", "XP and luck"), Tuple.Create("Hit Tracks", "slows enemies"), Tuple.Create("Last Unicorn", "fits the "), Tuple.Create("Pocket Watch", "nothing in it") })
            {
                var lines = all(x.Item1).Select(c => c.Line).ToList(); lines.Add(Eval(Item(byName, x.Item1), Deck(1.0, false)).Line);
                var hits = lines.Where(l => (l ?? "").IndexOf(x.Item2, StringComparison.OrdinalIgnoreCase) >= 0).Distinct().ToList();
                Check("IB4", "DK-C02: no card line '" + x.Item2.Trim() + "' under " + x.Item1 + " (4 squads + the Deck chest)", hits.Count == 0, hits.Count == 0 ? "'" + lines.Last() + "' on the Deck chest" : Show(hits));
            }
        }

        // ---------------------------------------------------------------- IB5
        static TagProfile Prof(params string[][] list) { var t = new TagProfile { SpecialAt = 10 }; foreach (var x in list) t.Source(x[0], 1, new[] { x[1] }); return t; }

        /// <summary>Squad C with one change: the score within 0.01, the card line exact (null: not pinned), a reason line it must carry.</summary>
        static void Need(Dictionary<string, ProbeItem> byName, string name, string label, Action<ItemContext> tweak, double score, string line = null, string why = null)
        {
            var c = Squad(2); tweak(c);
            var cell = Eval(Item(byName, name), c);
            bool ok = Near(cell.Score, score) && (line == null || cell.Line == line) && (why == null || cell.Why.Contains(why));
            Check("IB5", name + ", " + label + " = " + F(score) + (line != null ? " '" + line + "'" : "") + (why != null ? " (why '" + why + "')" : ""), ok,
                F(cell.Score) + " '" + cell.Line + "'" + (ok ? "" : " | " + string.Join("; ", cell.Why)));
        }

        static void Needs(Dictionary<string, ProbeItem> b)
        {
            Need(b, "Buckshot Roulette", "no magazine on the squad", c => c.ClipShare = 0, 1.60, "No weapon on the squad uses a magazine");
            Need(b, "Buckshot Roulette", "every weapon reloads a magazine", c => c.ClipShare = 1, 2.32, "The squad reloads magazines");
            Need(b, "Magazine Clip", "no magazine on the squad", c => c.ClipShare = 0, 1.40, "No weapon on the squad uses a magazine");
            Need(b, "Magazine Clip", "every weapon reloads a magazine", c => c.ClipShare = 1, 3.92, "The squad reloads magazines");
            Need(b, "Empty Chest", "1 free slot", c => c.FreeSlots = 1, 1.00, "No item slot left empty after it");
            Need(b, "Empty Chest", "3 free slots", c => c.FreeSlots = 3, 1.60, "+20% damage now (2 empty slots)");
            Need(b, "Empty Chest", "6 free slots", c => c.FreeSlots = 6, 2.50, "+50% damage now (5 empty slots)");
            Need(b, "Empty Chest", "free slots unknown", c => c.FreeSlots = double.NaN, 1.60, "+10% damage per empty item slot");
            Need(b, "Wooden Stick", "1 free slot", c => c.FreeSlots = 1, 1.59, "+10% XP now, no slot left empty");
            Need(b, "Wooden Stick", "3 free slots", c => c.FreeSlots = 3, 2.84, "+30% XP now (2 empty slots)");
            Need(b, "Wooden Stick", "6 free slots", c => c.FreeSlots = 6, 4.45, "+60% XP now (5 empty slots)");
            Need(b, "Jade Amulet", "luck 0", c => c.Luck = 0, 1.12, "No luck yet to turn into damage");
            Need(b, "Jade Amulet", "luck 20", c => c.Luck = 20, 1.72, "+20% damage at your luck");
            Need(b, "Jade Amulet", "luck 60", c => c.Luck = 60, 2.92, "+60% damage at your luck");
            Need(b, "Ruby Gem", "15:00, only Slashing 9", c => { c.Ctx.Seconds = 900; c.Tags.Points.Clear(); c.Tags.Points["Slashing"] = 9; }, 1.72, "1 damage type at 3 tags or more");
            Need(b, "Ruby Gem", "15:00, five types at 4", c => { c.Ctx.Seconds = 900; foreach (var t in new[] { "Slashing", "Ice", "Kinetic", "Fire", "Electric" }) c.Tags.Points[t] = 4; }, 2.52, "5 damage types at 3 tags or more");
            Need(b, "Ruby Gem", "as is (deals 3 types at 05:00)", c => { }, 2.12, "3 damage types at 3 tags or on the way");
            Need(b, "Boiling Pot", "as is (Freeze, Bleed, Injured)", c => { }, 2.00, "B-tier - a third status effect blows enemies up");
            Need(b, "Boiling Pot", "Rocket Launcher + Shotgun (Injured only)", c => c.Tags = Prof(new[] { "Rocket Launcher", "Explosive" }, new[] { "Shotgun", "Kinetic" }), 1.25, "Needs 3 status effects, the squad has 1");
            Need(b, "Boiling Pot", "the same, Brave Toaster held", c => { c.Tags = Prof(new[] { "Rocket Launcher", "Explosive" }, new[] { "Shotgun", "Kinetic" }); c.Held.Add("Brave Toaster"); }, 1.60, "Needs 3 status effects, the squad has 2");
            Need(b, "Boiling Pot", "Flamethrower + Nitro-Gun", c => c.Tags = Prof(new[] { "Flamethrower", "Fire" }, new[] { "Nitro-Gun", "Ice" }), 1.60, "Needs 3 status effects, the squad has 2");
            Need(b, "Boiling Pot", "the same, Fire Extinguisher held", c => { c.Tags = Prof(new[] { "Flamethrower", "Fire" }, new[] { "Nitro-Gun", "Ice" }); c.Held.Add("Fire Extinguisher"); }, 1.25, "Needs 3 status effects, the squad has 1");
            Need(b, "Mouse Trap", "as is (the squad owns a taunt)", c => { }, 2.40, "Boosts your taunts");
            Need(b, "Mouse Trap", "no taunt on the squad", c => c.OwnedTags.Remove("Taunt"), 1.00, "Nothing on the squad taunts");
            Need(b, "Jailbroken Phone", "no taunt on the squad", c => c.OwnedTags.Remove("Taunt"), 1.23, "Nothing on the squad taunts");
            Need(b, "MedKit", "health 100%", c => { }, 1.98);
            Need(b, "MedKit", "health 30%", c => c.Ctx.Health = 0.3, 2.84, null, "you are low on health");
            Need(b, "Nuclear Fusion", "health 30%", c => c.Ctx.Health = 0.3, 2.75, null, "you are low on health");
            Need(b, "Vampire Survivor", "solo Huntress", c => c.Squad = new[] { "Huntress" }, 2.38, "Solo: its healing doubles");
            Need(b, "Vampire Survivor", "Huntress, Ghost, Tank", c => c.Squad = new[] { "Huntress", "Ghost", "Tank" }, 1.88);
            Need(b, "Walkie Talkie", "Huntress, Ghost, Tank (a full squad)", c => c.Squad = new[] { "Huntress", "Ghost", "Tank" }, 1.32, "Halved in a full squad");
            Need(b, "Power of Friendship", "Huntress, Ghost, Tank", c => c.Squad = new[] { "Huntress", "Ghost", "Tank" }, 1.93);
            Need(b, "Combat Knife", "as is (Normal)", c => { }, 1.70, "+30% damage to basic enemies");
            Need(b, "Combat Knife", "Boss Rush", c => { c.Ctx.Mode = "BossRush"; c.Ctx.Goal = 600; }, 1.35, "Boss Rush: few basic enemies");
            Need(b, "Sugar Rush", "Boss Rush", c => { c.Ctx.Mode = "BossRush"; c.Ctx.Goal = 600; }, 1.40);
            Need(b, "Silver Padlock", "a lockdown advised", c => c.LockdownAdvised = true, 1.32);
            Need(b, "Zugzwang Hypergaster XD", "Slashing 10, Ice 7", c => { c.Tags.Points["Slashing"] = 10; c.Tags.Points["Ice"] = 7; }, -0.53, "Switches off your Slashing 10-tag effect");
            Need(b, "Omnigeode", "no tag points at all", c => c.Tags.Points.Clear(), 3.20);
            Need(b, "Magical Hat", "as is (Ice 5: +4)", c => { }, 2.85, "Boosts Ice - your Freezing Arrows deals it");
            Need(b, "Metal Gear", "pickup range 1.5", c => c.Pickup = 1.5, 2.17, "+25% duration from your pickup range");
            Need(b, "Wrench", "pickup range 1.0", c => c.Pickup = 1.0, 1.42, "Ability area from pickup range");
            Need(b, "Compass", "speed 150", c => c.MoveSpeed = 150, 1.87, "+12% ability power from your speed");
            Need(b, "Toilet Paper", "speed 150, no Chemical on the squad", c => c.MoveSpeed = 150, 1.15, "+20 speed; speed adds Chemical damage");
            Need(b, "Devil's Deal", "health 30%, 18:20", c => { c.Ctx.Health = 0.3; c.Ctx.Seconds = 1100; }, 1.85);
            Need(b, "Accumulator", "as is (no Electric points)", c => { }, 4.29);
            Need(b, "Accumulator", "Electric 10 points", c => c.Tags.Points["Electric"] = 10, 4.79, null, "your 10 Electric tags raise its damage");
            Need(b, "Great Nade", "no powerup tags owned", c => c.OwnedTags.Clear(), 2.00);
            Need(b, "Great Nade", "Explosive 5 points", c => c.Tags.Points["Explosive"] = 5, 2.25);
            Need(b, "Well Prepped", "as is", c => { }, 2.10);
        }

        // ---------------------------------------------------------------- IB6
        static void Held(Dictionary<string, ProbeItem> b, string name, string other, double alone, double with, string line, string aloneLine = null, Knowledge k = null)
        {
            var it = Item(b, name);
            var a = Eval(it, Squad(2, k));
            var c = Squad(2, k); c.Held.Add(other); var h = Eval(it, c);
            bool ok = Near(a.Score, alone) && Near(h.Score, with) && (line == null || h.Line == line) && (aloneLine == null || a.Line == aloneLine);
            Check("IB6", name + " " + F(alone) + (aloneLine != null ? " '" + aloneLine + "'" : "") + " -> " + F(with) + " with " + other + " held" + (line != null ? " '" + line + "'" : "") + (k != null ? " (knowledge.json edited)" : ""), ok,
                F(a.Score) + " '" + a.Line + "' -> " + F(h.Score) + " '" + h.Line + "'");
        }

        static void PairsClashes(Dictionary<string, ProbeItem> b)
        {
            Held(b, "Ruby Gem", "Omnigeode", 2.12, 3.02, "Pairs with your Omnigeode");
            Held(b, "Sapphire Gem", "Omnigeode", 2.12, 3.02, "Pairs with your Omnigeode");
            Held(b, "Pocket Watch", "The Word", 1.50, 2.40, "Pairs with The Word");
            Held(b, "Frozen Heart", "Devil's Deal", 2.44, 3.34, "Pairs with your Devil's Deal");
            Held(b, "Retaliation", "Devil's Deal", 1.82, 2.72, "Pairs with your Devil's Deal");
            Held(b, "T-Pose Doll", "Devil's Deal", 1.72, 0.82, "Clashes with your Devil's Deal");
            Held(b, "99'th Balloon", "Devil's Deal", 2.25, 1.35, "Clashes with your Devil's Deal");
            Held(b, "Devil's Deal", "T-Pose Doll", 1.94, 1.04, "Clashes with your T-Pose Doll");
            Held(b, "Access Keycard", "Black Box", 2.61, 3.51, "Pairs with your Black Box");
            Held(b, "Mouse Trap", "Jailbroken Phone", 2.40, 2.40, "Boosts your taunts");      // no clash: Mouse Trap's bonus reads taunted OR feared
            Held(b, "Hyperactivity", "Smooth Moves", 2.33, 1.93, "Halved by your Smooth Moves; shots miss");
            Held(b, "Smooth Moves", "Hyperactivity", 2.21, 2.21, "Halves your Hyperactivity; shots miss", "More dodge - suits Ghost");
            // the union: a pair (or a clash) that knowledge.json names as well counts once; the player's own clash works like the book's
            var kp = Knowledge.FromJson(Knowledge.DefaultJson.Replace("[\"Omnigeode\", \"Emerald Gem\"]", "[\"Omnigeode\", \"Emerald Gem\"], [\"Ruby Gem\", \"Omnigeode\"]"));
            Check("IB6", "the edited knowledge.json names [Ruby Gem, Omnigeode] too", kp.ItemPairs.Any(p => p.Key == "Ruby Gem" && p.Value == "Omnigeode"), kp.ItemPairs.Count + " knowledge pairs");
            Held(b, "Ruby Gem", "Omnigeode", 2.12, 3.02, "Pairs with your Omnigeode", null, kp);
            var kc = Knowledge.FromJson(Knowledge.DefaultJson.Replace("\"itemClashes\": []", "\"itemClashes\": [ [\"T-Pose Doll\", \"Devil's Deal\"], [\"Frying Pan\", \"Pocket Watch\"] ]"));
            Check("IB6", "the edited knowledge.json's itemClashes are read", kc.ItemClashes.Count == 2, kc.ItemClashes.Count + " clashes");
            Held(b, "T-Pose Doll", "Devil's Deal", 1.72, 0.82, "Clashes with your Devil's Deal", null, kc);
            var fp = Eval(Item(b, "Frying Pan"), Squad(2, kc)).Score;
            Held(b, "Frying Pan", "Pocket Watch", fp, fp - 0.9, "Clashes with your Pocket Watch", null, kc);
        }

        // ---------------------------------------------------------------- IB7
        static void Offers(Dictionary<string, ProbeItem> b)
        {
            var offers = new[]
            {
                Tuple.Create(new[] { "Jacob's Ladder", "Power Glove", "T-Pose Doll", "Annoying Trumpet" }, new[] { "Jacob's Ladder", "T-Pose Doll", "Power Glove", "Power Glove" }),
                Tuple.Create(new[] { "Sapphire Gem", "Combat Knife", "Icon of Cinder", "Sugar Rush" }, new[] { "Sapphire Gem", "Icon of Cinder", "Sapphire Gem", "Combat Knife" }),
                Tuple.Create(new[] { "Last Unicorn", "Vampire Survivor", "Potato", "Jewel of Life" }, new[] { "Jewel of Life", "Jewel of Life", "Jewel of Life", "Jewel of Life" }),     // C16-12: a second life, x4
                Tuple.Create(new[] { "Emerald Gem", "Frozen Heart", "Ruby Gem", "Icon of Cinder" }, new[] { "Frozen Heart", "Icon of Cinder", "Emerald Gem", "Frozen Heart" }),
                Tuple.Create(new[] { "Detective's Pipe", "Frying Pan", "Golden Key", "Crowbar" }, new[] { "Detective's Pipe", "Detective's Pipe", "Detective's Pipe", "Frying Pan" }),
                Tuple.Create(new[] { "Great Nade", "Solar Panel", "Boxing Gloves", "Nuts & Bolts" }, new[] { "Boxing Gloves", "Boxing Gloves", "Solar Panel", "Great Nade" }),
                Tuple.Create(new[] { "Magazine Clip", "Icon of Tempest", "Teddy Bear", "Pocket Watch" }, new[] { "Magazine Clip", "Magazine Clip", "Magazine Clip", "Magazine Clip" }),
                Tuple.Create(new[] { "Battery Leakage", "Glass of Milk", "Heavy Metal", "Mouse Trap" }, new[] { "Heavy Metal", "Heavy Metal", "Mouse Trap", "Heavy Metal" }),
                Tuple.Create(new[] { "Skip Rope", "Golden Key", "Buckshot Roulette", "Hit Tracks" }, new[] { "Buckshot Roulette", "Buckshot Roulette", "Buckshot Roulette", "Buckshot Roulette" }),
                Tuple.Create(new[] { "Last Round", "99'th Balloon", "Icon of Cinder", "An Apple" }, new[] { "Last Round", "Last Round", "Last Round", "Last Round" }),
            };
            foreach (var of in offers)
            {
                var got = new List<string>(); var cells = new List<string>();
                for (int i = 0; i < 4; i++)
                {
                    var best = of.Item1.Select(n => { var it = Item(b, n); var c = Squad(i); c.Stats = it.Stats; c.Healing = it.Healing; return Tuple.Create(n, ItemRules.Evaluate(n, it.Desc, c, new List<string>())); })
                                       .OrderByDescending(t => t.Item2).First();
                    got.Add(best.Item1); cells.Add(SquadNames[i].Substring(0, 1) + " " + best.Item1 + " " + F(best.Item2));
                }
                Check("IB7", "the logged offer [" + string.Join(", ", of.Item1) + "]: #1 " + string.Join(" | ", of.Item2), got.SequenceEqual(of.Item2), string.Join(" | ", cells));
            }
        }

        // ---------------------------------------------------------------- IB8b
        static void Keywords(Dictionary<string, ProbeItem> b)
        {
            bool was = ItemRules.UseBook;
            try
            {
                ItemBook.UseKeywords(true);      // [Debug] ItemKeywords = true, through the bridge Plugin.cs calls
                bool off = !ItemRules.UseBook;
                foreach (var x in new[] { Tuple.Create("Plot Armor", 0, 1.49, "More damage"), Tuple.Create("Electric Personality", 1, 4.38, "More healing - suits Medic"),
                    Tuple.Create("Accumulator", 1, 2.59, "Nobody on the squad deals Electric"), Tuple.Create("Frozen Heart", 1, 2.88, "More healing - suits Medic"), Tuple.Create("Combat Knife", 0, 1.18, "More damage") })
                {
                    var cell = Eval(Item(b, x.Item1), Squad(x.Item2));
                    Check("IB8b", "[Debug] ItemKeywords = true: " + x.Item1 + " in " + SquadNames[x.Item2].Substring(0, 1) + " scores as 0.15.0 did, " + F(x.Item3) + " '" + x.Item4 + "'",
                        off && Near(cell.Score, x.Item3) && cell.Line == x.Item4, F(cell.Score) + " '" + cell.Line + "'");
                }
                // and the revive need is the book's: no row, no floor (the 0.15.0 keyword reading of Jewel of Life)
                var jol = Eval(Item(b, "Jewel of Life"), Squad(2));
                Check("IB8b", "[Debug] ItemKeywords = true: no revive floor (no book row, no need)", jol.Score < 1 + ItemRules.ReviveFloor, F(jol.Score) + " '" + jol.Line + "'");
            }
            finally { ItemRules.UseBook = was; }
        }

        // ================================================================ the pure part (also --no-data)
        /// <summary>The cases without game data (also --no-data): IB2 the rows, IB8a the fallback, IB9 / IB9b the revive need, the [ctx]
        /// tail, the sources.</summary>
        public static int RunPure()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.16.0: the item book - a rule per chest item (C16-02)");
            Rows();
            Fallback();
            Revive();
            DeckChest();
            CtxLine();
            Sources();
            Console.WriteLine("  item book (pure): " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        // ---------------------------------------------------------------- IB2
        static readonly string[] NeedKinds = { "clip", "short", "long", "shield", "own", "status3", "slots:damage", "slots:xp", "luck", "pickup", "move:abilities", "move:Chemical",
            "gems", "tags:omni", "tags:zug", "tags:hat", "squad", "solo", "power", "lockdown", "skip", "hurt", "basic", "boss", "healthy", "revive" };

        static void Rows()
        {
            var k = K;
            var badTags = new List<string>();
            foreach (var e in ItemBook.All)
            {
                try { ItemBook.Weights(e.Gains, ItemRules.All); ItemBook.Weights(e.Costs, ItemRules.All); }
                catch (Exception x) { badTags.Add(e.Name + ": " + x.Message); }
            }
            ItemBook.Of("Magazine Clip");      // Ready(): the live path builds every row's weights once
            Check("IB2", "every row's gains and costs parse (Weights), nothing on the live path's bad-tag list", badTags.Count == 0 && ItemBook.BadTags.Count == 0,
                ItemBook.All.Length + " rows; Weights: " + Show(badTags) + "; BadTags: " + Show(ItemBook.BadTags));
            bool threw = false; try { ItemBook.Weights("weapons, bogus:1", ItemRules.All); } catch (ArgumentException) { threw = true; }
            Check("IB2", "a planted 'bogus:1' makes Weights throw (ArgumentException)", threw);
            var built = ItemBook.All.Where(e => e.GainW == null || e.CostW == null || e.Uses == null || e.GainW.Length != ItemRules.All.Length).Select(e => e.Name).ToList();
            Check("IB2", "Ready built every row's weights (Score never builds them)", built.Count == 0, Show(built));
            var needs = ItemBook.All.Where(e => e.Need != null && !NeedKinds.Contains(e.Need) && !NeedKinds.Contains(e.Need.Split(':')[0])).Select(e => e.Name + " '" + e.Need + "'").ToList();
            var clipF = ItemBook.All.Where(e => e.Need != null && e.Need.StartsWith("clip:", StringComparison.Ordinal) && !double.TryParse(e.Need.Substring(5), NumberStyles.Float, CultureInfo.InvariantCulture, out _)).Select(e => e.Name).ToList();
            Check("IB2", "every need is one of the list (C16-12: 'revive' joins it)", needs.Count == 0 && clipF.Count == 0, Show(needs.Concat(clipF)));
            var clocks = new[] { "now", "economy", "grows", "cash", "economy,grows" };
            Check("IB2", "every clock is now, economy, grows, cash or economy,grows", ItemBook.All.All(e => clocks.Contains(e.Clock)), Show(ItemBook.All.Where(e => !clocks.Contains(e.Clock)).Select(e => e.Name)));
            var says = ItemBook.All.Where(e => e.Say != null && (e.Say.Length > 40 || e.Say.Any(ch => ch < 32 || ch > 126) || Wording.Rails(e.Say).Count > 0)).Select(e => e.Name + " (" + e.Say.Length + ")").ToList();
            Check("IB2", "every Say at most 40 characters, ASCII, on the rails", says.Count == 0, Show(says));
            var overB = ItemBook.All.Where(e => !k.ItemTier.ContainsKey(e.Name) && e.Value > 1.0 + 1e-9).Select(e => e.Name + " " + F(e.Value)).ToList();
            var tierOff = ItemBook.All.Where(e => k.ItemTier.ContainsKey(e.Name) && Math.Abs(Knowledge.Tier(k.ItemTier[e.Name], 3, 2, 1, -1.5) - e.Value) > 1e-9).Select(e => e.Name).ToList();
            int tiered = ItemBook.All.Count(e => k.ItemTier.ContainsKey(e.Name));
            Check("IB2", "no untiered Value over 1.0 (B: Q02's default), every tiered Value its knowledge tier", overB.Count == 0 && tierOff.Count == 0,
                tiered + " tiered, " + (ItemBook.All.Length - tiered) + " untiered; over B: " + Show(overB) + "; tier differs: " + Show(tierOff));
            var names = new HashSet<string>(ItemBook.All.Select(e => e.Name), StringComparer.Ordinal);
            var refs = ItemBook.Pairs.SelectMany(p => p).Concat(ItemBook.Clashes.SelectMany(p => p)).Concat(ItemBook.Mixed.SelectMany(m => new[] { m.Offered, m.Held })).Concat(ItemBook.Converts.Select(cv => cv[0]));
            var stray = refs.Where(n => !names.Contains(n)).Distinct().ToList();
            Check("IB2", "every name in Pairs / Clashes / Mixed / Converts is a book row", stray.Count == 0,
                ItemBook.Pairs.Length + " pairs, " + ItemBook.Clashes.Length + " clashes, " + ItemBook.Mixed.Length + " mixed, " + ItemBook.Converts.Length + " conversions; stray: " + Show(stray));
            var dup = ItemBook.All.GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Check("IB2", "one row per name", dup.Count == 0, Show(dup));
            var revive = ItemBook.All.Where(e => ItemBook.Revive(e.Name)).Select(e => e.Name).ToList();
            Check("IB2", "the second lives (C16-12): Revive() is true for Jewel of Life and Plot Armor only, false for a name the book lacks; ReviveAbilities = Resuscitation",
                revive.SequenceEqual(new[] { "Jewel of Life", "Plot Armor" }) && !ItemBook.Revive("Bench Widget") && !ItemBook.Revive(null) && ItemBook.ReviveAbilities.SequenceEqual(new[] { "Resuscitation" }),
                Show(revive) + "; abilities " + Show(ItemBook.ReviveAbilities));
            Check("IB2", "Of() trims and ignores case", ItemBook.Of("  jewel of life ") != null && ItemBook.Of("Schrödinger's Cat") != null && ItemBook.Of("Bench Widget") == null);
        }

        // ---------------------------------------------------------------- IB8a
        static void Fallback()
        {
            bool was = ItemRules.UseBook;
            var err = Console.Error; var sw = new StringWriter();
            double book, keywords; var whyBook = new List<string>(); var whyKw = new List<string>();
            try
            {
                Console.SetError(sw);
                ItemRules.UseBook = true;
                book = ItemRules.Evaluate("Bench Widget", "+20% Weapon damage. +10 Armor", new[] { "SWAT", "Engineer" }, new TagProfile(), K, whyBook);
                ItemRules.Evaluate("Bench Widget", "+20% Weapon damage. +10 Armor", new[] { "SWAT", "Engineer" }, new TagProfile(), K, new List<string>());     // said once per name
                ItemRules.Evaluate("Powerups/Item/Bench", "+20% Weapon damage", new[] { "SWAT", "Engineer" }, new TagProfile(), K, new List<string>());          // an unlocalized duplicate: quiet
                ItemRules.UseBook = false;
                keywords = ItemRules.Evaluate("Bench Widget", "+20% Weapon damage. +10 Armor", new[] { "SWAT", "Engineer" }, new TagProfile(), K, whyKw);
            }
            finally { ItemRules.UseBook = was; Console.SetError(err); }
            string log = sw.ToString();
            int said = log.Split('\n').Count(l => l.Contains("[items] no rule for 'Bench Widget' - keyword reading"));
            Check("IB8a", "an item the book lacks keeps the keyword reading: the same score and reasons with the book on and off", Near(book, keywords) && string.Join(";", whyBook) == string.Join(";", whyKw),
                F(book) + " / " + F(keywords) + " (" + string.Join("; ", whyBook) + ")");
            Check("IB8a", "... and says so once per name ('[items] no rule for 'Bench Widget' - keyword reading'), never for a 'Powerups/' name", said == 1 && !log.Contains("Powerups/Item/Bench"),
                said + " time(s); log: " + log.Replace("\r", "").Replace("\n", " | ").Trim());
        }

        // ---------------------------------------------------------------- IB9 / IB9b: the revive need (C16-12)
        static Cell Pure(string name, ItemContext c) { return Eval(name, "", null, false, c); }

        static void Revive()
        {
            foreach (var name in new[] { "Jewel of Life", "Plot Armor" })
            {
                string say = name == "Jewel of Life" ? "One revive, then halves max HP" : "Survives a killing blow once a minute";
                var states = new[]
                {
                    Tuple.Create("health 100%, no quest", 1.0, false, 3.38, say, (string)null),
                    Tuple.Create("health 100%, survive quest", 1.0, true, 3.88, "A second life - survive the run (quest)", "a second life: the quest asks to survive the run"),
                    Tuple.Create("health 60% (no lift: under 60 % only)", 0.60, false, 3.38, say, (string)null),
                    Tuple.Create("health 59%", 0.59, false, 3.89, "A second life - the squad is at 59%", "a second life: the squad is at 59% health"),
                    Tuple.Create("health 55%", 0.55, false, 3.92, "A second life - the squad is at 55%", "a second life: the squad is at 55% health"),
                    Tuple.Create("health 55%, survive quest (the health words lead)", 0.55, true, 3.92, "A second life - the squad is at 55%", "a second life: the squad is at 55% health"),
                    Tuple.Create("health 30%", 0.30, false, 4.24, "A second life - the squad is at 30%", "a second life: the squad is at 30% health"),
                };
                foreach (var st in states)
                {
                    var c = Squad(2); c.Ctx.Health = st.Item2; c.Ctx.SurviveQuest = st.Item3;
                    var cell = Pure(name, c);
                    bool ok = Near(cell.Score, st.Item4) && cell.Line == st.Item5 && (st.Item6 == null ? !cell.Why.Any(w => w.StartsWith("a second life", StringComparison.Ordinal)) : cell.Why.Contains(st.Item6));
                    Check("IB9", name + " (squad C, the book only), " + st.Item1 + " = " + F(st.Item4) + " '" + st.Item5 + "'", ok, F(cell.Score) + " '" + cell.Line + "' | " + string.Join("; ", cell.Why));
                }
            }
            {   // the floor lifts a tier the player edited lower; One Hit (survival 0) has no floor
                var kc = Knowledge.FromJson(Knowledge.DefaultJson); kc.ItemTier["Jewel of Life"] = "C";
                var c = Squad(2, kc); var cell = Pure("Jewel of Life", c);
                Check("IB9", "Jewel of Life tiered C in an edited knowledge.json still scores at the floor (A) = 3.38", Near(cell.Score, 3.38), F(cell.Score) + " '" + cell.Line + "'");
                var o = Squad(2); o.Ctx.Mode = "OneHit"; var one = Pure("Jewel of Life", o);
                Check("IB9", "One Hit (health means nothing): no revive floor", o.Ctx.Survival <= 0 && one.Score < 1 + ItemRules.ReviveFloor, F(one.Score) + " (survival x" + F(o.Ctx.Survival) + ")");
            }
        }

        /// <summary>A logged Deck chest (Normal d1, 06:32 played, a Survive quest, the leader at 55 %), rebuilt in game names and the
        /// bench's own build names: Frozen Heart, Pickup Pick, Jewel of Life, Last Unicorn offered.</summary>
        static ItemContext Deck(double health, bool quest)
        {
            var k = K;
            var tags = new TagProfile { SpecialAt = 10 };
            tags.Source("Assault Rifle", 1.0, new[] { "Kinetic" }); tags.Source("Helicopter Strike: Gunner", 1.0, new[] { "Kinetic" }); tags.Source("Kinetic source 0", 0.5, new[] { "Kinetic" });
            tags.Source("Grenade Trail", 1.0, new[] { "Explosive" }); tags.Source("Automatic Turret: Demolition", 1.0, new[] { "Explosive" });
            tags.Source("Taser", 1.0, new[] { "Electric" }); tags.Source("Energy Shield", 1.0, new[] { "Electric" });
            tags.Points["Kinetic"] = 28; tags.Points["Explosive"] = 4; tags.Points["Electric"] = 2;
            var run = new RunContext { Mode = "Normal", Difficulty = 1, Goal = 1200, Seconds = 392, LevelRate = 2.6, LevelUps = 392 / 25, D = new Doctrine(), Health = health, SurviveQuest = quest };
            var c = new ItemContext { Squad = new[] { "SWAT", "Ranger", "Engineer" }, Tags = tags, K = k, Ctx = run,
                OwnedTags = new HashSet<string>(new[] { "Deployable", "Turret", "Grenade" }, StringComparer.OrdinalIgnoreCase),
                Held = new HashSet<string>(new[] { "Farming Tools", "Nine Inch Nails" }, StringComparer.OrdinalIgnoreCase) };
            c.AbilityLean = 14.0 / 23.0; c.CritSquad = c.Squad.Any(x => k.CritSquad.Contains(x, StringComparer.OrdinalIgnoreCase)); c.ShieldOwned = true;
            c.Wants.Add(new KeyValuePair<string, string>("Rifleman (Auto)", "weapons")); c.Wants.Add(new KeyValuePair<string, string>("Rifleman (Auto)", "Kinetic"));
            c.Wants.Add(new KeyValuePair<string, string>("Bench Anchor (Auto)", "Electric"));
            return c;
        }

        static void DeckChest()
        {
            var offer = new[] { "Frozen Heart", "Pickup Pick", "Jewel of Life", "Last Unicorn" };
            foreach (var st in new[] { Tuple.Create(0.55, true, new[] { 2.45, 2.27, 3.95, 1.57 }, "A second life - the squad is at 55%"), Tuple.Create(1.0, false, new[] { 2.45, 2.27, 3.41, 1.57 }, "One revive, then halves max HP") })
            {
                var cells = offer.Select(n => Pure(n, Deck(st.Item1, st.Item2))).ToList();
                bool ok = cells.Select((x, i) => Near(x.Score, st.Item3[i])).All(b => b) && cells[2].Line == st.Item4;
                string label = st.Item2 ? "health 55%, survive quest" : "health 100%, no quest";
                Check("IB9b", "the logged Deck chest rebuilt, " + label + ": " + string.Join(", ", offer.Select((n, i) => n + " " + F(st.Item3[i]))) + ", Jewel of Life '" + st.Item4 + "'", ok,
                    string.Join(", ", cells.Select((x, i) => offer[i] + " " + F(x.Score))) + "; Jewel of Life '" + cells[2].Line + "'");
                var ranked = cells.Select((x, i) => Tuple.Create(offer[i], x.Score)).OrderByDescending(t => t.Item2).ToList();
                var x2 = new ScreenCallIn { Screen = "Chest", Progress = 392.0 / 1200, CanReroll = true, Rerolls = 7, RerollsText = "7 (bench)", CanSkip = true, SkipCash = 705, SkipHeal = 320,
                    Health = st.Item1, Survival = 0.96, Cash = 0.4 };
                for (int i = 0; i < ranked.Count; i++) x2.Cards.Add(new HintCard { Name = ranked[i].Item1, Score = ranked[i].Item2, Rank = i + 1, Kind = "item" });
                var call = ScreenCall.Decide(x2);
                Check("IB9b", "... Jewel of Life is #1 and the hints stay silent (no REROLL sends the second life away)", ranked[0].Item1 == "Jewel of Life" && !call.Show,
                    "#1 " + ranked[0].Item1 + " " + F(ranked[0].Item2) + "; " + (call.Show ? ScreenCall.Name(call.Action) + " '" + call.Words + "'" : "silent") + " | " + call.Why);
            }
        }

        // ---------------------------------------------------------------- the [ctx] tail (C16-12 (c))
        static void CtxLine()
        {
            Func<double, bool, string> line = (h, q) => new RunContext { Mode = "Normal", Goal = 1200, Seconds = 392, LevelRate = 2.6, LevelUps = 15, D = new Doctrine(), Health = h, SurviveQuest = q }.ToString();
            string hurt = line(0.55, true), full = line(1, false), edge = line(0.599, false), quest = line(1, true);
            Check("IB9", "[ctx] ends '..., health 55%, survive quest)' at health 0.55 with a followed survive quest", hurt.EndsWith(" level-ups to come, health 55%, survive quest)", StringComparison.Ordinal), hurt);
            Check("IB9", "[ctx] at full health and no such quest ends as 0.15.0 wrote it ('... level-ups to come)')", full.EndsWith(" level-ups to come)", StringComparison.Ordinal) && !full.Contains("health ") && !full.Contains("survive quest"), full);
            Check("IB9", "[ctx] floors the health (0.599 reads 59%), the quest alone adds ', survive quest'", edge.EndsWith(", health 59%)", StringComparison.Ordinal) && quest.EndsWith(" level-ups to come, survive quest)", StringComparison.Ordinal), edge + " | " + quest);
        }

        // ---------------------------------------------------------------- the sources
        static string Between(string src, string from, string to)
        {
            if (src == null) return null;
            int a = src.IndexOf(from, StringComparison.Ordinal); if (a < 0) return null;
            int b = src.IndexOf(to, a + from.Length, StringComparison.Ordinal);
            return b < 0 ? src.Substring(a) : src.Substring(a, b - a);
        }

        static void Sources()
        {
            string rules = Src("ItemRules.cs"), book = Src("ItemBook.cs"), know = Src("Knowledge.cs"), plugin = Src("Plugin.cs"), quest = Src("Quest.cs"), state = Src("GameState.cs");
            string proj = BenchFile("ItemBench.csproj");
            if (rules == null || book == null || know == null || plugin == null || quest == null || state == null || proj == null)
            { Check("IB-S", "the sources are readable from the bench", false, "a file is missing next to tools\\ItemBench"); return; }
            string evaluate = Between(rules, "public static double Evaluate(string name, string description, ItemContext c, List<string> why)", "static bool Is(");
            string score = Between(rules, "public static double Score(string description, ItemContext c, List<string> why", "static bool Draw(");
            string ready = Between(book, "static void Ready()", "public static BookEntry Of(");
            string need = Between(rules, "static string Need(string need", "internal static int StatusKinds(");
            string follow = Between(quest, "internal static bool SurviveFollowed()", "\n");
            Check("IB-S", "ItemRules.Evaluate reads the book (RowOf -> ItemBook.Of) and says '[items] no rule for' once per name",
                evaluate != null && evaluate.Contains("var row = RowOf(name);") && rules.Contains("UseBook ? ItemBook.Of(name) : null") && rules.Contains("\"[items] no rule for '\""));
            Check("IB-S", "ItemBook.Ready builds every row's weights, logs '[items] bad rule tag'; Score never builds them (no 'GainW == null' there)",
                ready != null && ready.Contains("e.GainW = Weights(") && ready.Contains("e.CostW = Weights(") && ready.Contains("[items] bad rule tag: ") && score != null && !score.Contains("GainW == null"));
            Check("IB-S", "ItemRules.Need has case \"revive\" with the floor, the lift and the 60 % line (C16-12)",
                need != null && need.Contains("case \"revive\":") && need.Contains("ReviveFloor") && need.Contains("ReviveLift") && need.Contains("ReviveHurt") && rules.Contains("ReviveFloor = 2.0, ReviveLift = 0.5, ReviveHurt = 0.6"));
            Check("IB-S", "Knowledge parses itemClashes; DefaultJson writes it empty", know.Contains("TryGetProperty(\"itemClashes\"") && know.Contains("\"\"itemClashes\"\": []"));
            Check("IB-S", "ItemBench.csproj links ItemBook.cs and copies itembook_expected.txt",
                proj.Contains("<Compile Include=\"..\\..\\YazsCompanion.Mod\\ItemBook.cs\" Link=\"mod\\ItemBook.cs\" />") && proj.Contains("<None Include=\"itembook_expected.txt\" CopyToOutputDirectory=\"PreserveNewest\""));
            Check("IB-S", "Plugin.cs binds [Debug] ItemKeywords and hands it to ItemBook.UseKeywords; ItemBook sets UseBook = !on",
                plugin.Contains("Config.Bind(\"Debug\", \"ItemKeywords\", false,") && plugin.Contains("ItemBook.UseKeywords(ItemKeywords.Value)") && book.Contains("ItemRules.UseBook = !on"));
            Check("IB-S", "Quest.cs: Counters sets rules.Survive; SurviveFollowed reads the cached state, never Rules( (C16-12)",
                quest.Contains("rules.Survive = true;") && follow != null && follow.Contains("r.Survive") && !follow.Contains("Rules("));
            Check("IB-S", "GameState.ReadContext reads Quest.SurviveFollowed into RunContext.SurviveQuest (C16-12)", state.Contains("c.SurviveQuest = YazsCompanion.Quest.SurviveFollowed();"));
        }
    }
}
