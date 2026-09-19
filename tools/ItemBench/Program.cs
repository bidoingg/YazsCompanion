// Runs the mod's item and tag rules over every item of the game (from the PC app's extracted
// data/gamedata.json) for a few squads, so a rule change can be judged without a run:
//   - the top items per squad (what GRAB would list, and what a chest verdict would prefer),
//   - items that reach the GRAB threshold (3.0) on keywords alone, without a guide tier,
//   - the Research Pod cards a squad would see and how they rank.
// Usage: ItemBench [gamedata.json] [--all]   (default path: <project root>\data\gamedata.json, i.e. six levels
//        up from the exe in tools\ItemBench\bin\Release\net8.0; --all prints every item's score for every squad)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    sealed class Scenario
    {
        public string Title; public string[] Squad; public TagProfile Tags = new TagProfile();
        public Scenario(string title, params string[] squad) { Title = title; Squad = squad; }
        public Scenario Deals(string type, string source, double w) { Tags.Deals(type, source, w); return this; }
        public Scenario Points(string type, int n) { Tags.Points[type] = n; return this; }
        public Scenario Special(int at) { Tags.SpecialAt = at; return this; }
    }

    static class Program
    {
        static int Main(string[] args)
        {
            string path = args.FirstOrDefault(a => !a.StartsWith("--")) ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "data", "gamedata.json"));
            bool all = args.Contains("--all");
            int pi = Array.IndexOf(args, "--probe");
            string probe = pi >= 0 && pi + 1 < args.Length ? args[pi + 1] : Path.Combine(Path.GetDirectoryName(path) ?? ".", "probe.json");
            path = args.Where((a, i) => !a.StartsWith("--") && (pi < 0 || i != pi + 1)).FirstOrDefault() ?? path;
            if (!File.Exists(path)) { Console.Error.WriteLine("gamedata.json not found: " + path + " (run tools/extract_gamedata.py of the PC app, or pass the path)"); return 2; }
            var items = new List<KeyValuePair<string, string>>();
            using (var doc = JsonDocument.Parse(File.ReadAllText(path)))
                foreach (var it in doc.RootElement.GetProperty("items").EnumerateArray())
                    items.Add(new KeyValuePair<string, string>(it.GetProperty("name").GetString(), it.GetProperty("desc").GetString()));
            var k = Knowledge.FromJson(Knowledge.DefaultJson);
            if (args.Contains("--time"))
            {   // what the item pass of a plan build costs: the first pass of a process (patterns set up, every description
                // parsed) against a later one (everything kept) - the first one is what a session's first plan pays
                var squad = new[] { "Tank", "SWAT", "Engineer" };
                for (int pass = 1; pass <= 3; pass++)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    foreach (var it in items) ItemRules.Evaluate(it.Key, it.Value, squad, new TagProfile(), k, new List<string>());
                    Console.WriteLine("pass " + pass + ": " + sw.Elapsed.TotalMilliseconds.ToString("0.0") + " ms for " + items.Count + " items");
                }
                return 0;
            }
            Console.WriteLine(items.Count + " items, " + k.ItemTier.Count + " with a guide tier");

            var scenarios = new[]
            {
                new Scenario("Tank alone, early (no profile known)", "Tank"),
                new Scenario("Tank + SWAT + Engineer", "Tank", "SWAT", "Engineer")
                    .Deals("Explosive", "Rocket Launcher", 1).Deals("Explosive", "Minefield", .5).Deals("Slashing", "Sawblade Drone", .5)
                    .Deals("Kinetic", "Assault Rifle", 1).Deals("Explosive", "Grenade Trail", .5).Deals("Electric", "Tesla", 1).Deals("Electric", "Electric Turret", .5)
                    .Points("Explosive", 7).Points("Kinetic", 3).Points("Electric", 4).Special(10),
                new Scenario("Huntress + Ghost (crit)", "Huntress", "Ghost")
                    .Deals("Ice", "Freezing Arrows", 1).Deals("Kinetic", "Bear Trap", .5).Deals("Slashing", "Thousand Cuts", 1).Deals("Slashing", "Shuriken", .5)
                    .Points("Slashing", 9).Points("Ice", 5).Special(10),
                new Scenario("Pyro + Medic + Mechanic", "Pyro", "Medic", "Mechanic")
                    .Deals("Fire", "Flamethrower", 1).Deals("Fire", "Molotov", .5).Deals("Chemical", "Syringe Rifle", 1).Deals("Ice", "Nitro-Gun", 1)
                    .Points("Fire", 12).Points("Chemical", 2).Points("Ice", 6).Special(10),
            };

            foreach (var sc in scenarios)
            {
                Console.WriteLine();
                Console.WriteLine("=== " + sc.Title + " | deals: " + (sc.Tags.Known ? sc.Tags.DealsText() : "unknown") + " | points: " + sc.Tags.PointsText() + " | stack " + (sc.Tags.Focus() ?? "-"));
                var scored = new List<Tuple<double, string, string, bool>>();
                foreach (var it in items)
                {
                    var why = new List<string>();
                    double s = ItemRules.Evaluate(it.Key, it.Value, sc.Squad, sc.Tags, k, why);
                    scored.Add(Tuple.Create(s, it.Key, string.Join("; ", why), k.ItemTier.ContainsKey(it.Key)));
                }
                scored.Sort((a, b) => b.Item1.CompareTo(a.Item1));
                Console.WriteLine("-- top 12");
                foreach (var t in scored.Take(12)) Console.WriteLine("  " + t.Item1.ToString("0.00").PadLeft(5) + "  " + t.Item2.PadRight(22) + " " + t.Item3);
                var keywordOnly = scored.Where(t => !t.Item4 && t.Item1 >= 3.0).ToList();
                Console.WriteLine("-- at the GRAB threshold (3.0) without a guide tier: " + (keywordOnly.Count == 0 ? "none" : ""));
                foreach (var t in keywordOnly) Console.WriteLine("  " + t.Item1.ToString("0.00").PadLeft(5) + "  " + t.Item2.PadRight(22) + " " + t.Item3);
                Console.WriteLine("-- bottom 5");
                foreach (var t in scored.Skip(Math.Max(0, scored.Count - 5))) Console.WriteLine("  " + t.Item1.ToString("0.00").PadLeft(5) + "  " + t.Item2.PadRight(22) + " " + t.Item3);
                if (all) { Console.WriteLine("-- all"); foreach (var t in scored) Console.WriteLine("  " + t.Item1.ToString("0.00").PadLeft(5) + "  " + t.Item2.PadRight(22) + " " + t.Item3); }

                // a Research Pod reward screen: one card per type, +2 each, plus a +4 of the focus type
                Console.WriteLine("-- Research Pod cards (+2 of each type, +4 " + (sc.Tags.Focus() ?? "Fire") + ")");
                var cards = new List<Tuple<double, string, string>>();
                foreach (var type in TagProfile.Names) { var why = new List<string>(); cards.Add(Tuple.Create(Tags.Score(type, 2, sc.Tags, why), "#" + type + " +2", string.Join("; ", why))); }
                { var why = new List<string>(); string f = sc.Tags.Focus() ?? "Fire"; cards.Add(Tuple.Create(Tags.Score(f, 4, sc.Tags, why), "#" + f + " +4", string.Join("; ", why))); }
                cards.Sort((a, b) => b.Item1.CompareTo(a.Item1));
                foreach (var c in cards) Console.WriteLine("  " + c.Item1.ToString("0.00").PadLeft(5) + "  " + c.Item2.PadRight(16) + " " + c.Item3);
            }
            return Checks.Run(probe);
        }
    }
}
