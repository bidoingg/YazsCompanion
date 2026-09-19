// The second half of the bench: what 0.10 added. Run with a probe.json (the mod's [Debug] Probe dump: every item and
// powerup with the exact fields the game uses) and it
//   1. VALIDATES the build presets and kits against the game's own names - a misspelt evolution would never match,
//   2. shows the run clock at work: the same items and picks early, mid-run and near the end, and per game mode,
//   3. shows which evolution and which weapon branch the live synergy rules pick for a few squads.
// Usage: ItemBench [gamedata.json] --probe path\to\probe.json
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    sealed class ProbeItem { public string Name, Desc; public List<string> Stats = new List<string>(); public bool Healing; }

    static class Checks
    {
        static readonly Dictionary<string, PowerFacts> Powers = new Dictionary<string, PowerFacts>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, string> ClassOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly List<ProbeItem> Items = new List<ProbeItem>();

        static string Norm(string s) { return new string((s ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray()); }
        static bool Same(string a, string b)
        {
            if (Norm(a) == Norm(b)) return true;
            int ia = a.IndexOf(':'), ib = b.IndexOf(':');
            string ta = Norm(ia >= 0 ? a.Substring(ia + 1) : a), tb = Norm(ib >= 0 ? b.Substring(ib + 1) : b);
            return ta.Length > 2 && ta == tb;
        }

        public static int Run(string probePath)
        {
            if (!File.Exists(probePath)) { Console.WriteLine("\n(no probe.json at " + probePath + ": preset validation and the 0.10 scenarios skipped)"); return 0; }
            using (var doc = JsonDocument.Parse(File.ReadAllText(probePath)))
            {
                foreach (var p in doc.RootElement.GetProperty("powerups").EnumerateArray())
                {
                    var f = new PowerFacts { Name = p.GetProperty("name").GetString() };
                    JsonElement e;
                    if (p.TryGetProperty("ability", out e)) f.IsAbility = e.GetBoolean();
                    if (p.TryGetProperty("healing", out e)) f.Healing = e.GetBoolean();
                    if (p.TryGetProperty("type", out e)) f.IsWeapon = e.GetString() == "WeaponUpgradePowerup";
                    if (p.TryGetProperty("damage", out e)) foreach (var t in e.EnumerateArray()) f.Damage.Add(t.GetString());
                    if (p.TryGetProperty("tags", out e)) foreach (var t in e.EnumerateArray()) f.Tags.Add(t.GetString());
                    Powers[f.Name] = f;
                    if (p.TryGetProperty("class", out e)) ClassOf[f.Name] = e.GetString();
                }
                foreach (var it in doc.RootElement.GetProperty("items").EnumerateArray())
                {
                    var pi = new ProbeItem { Name = it.GetProperty("name").GetString() };
                    JsonElement e;
                    if (it.TryGetProperty("desc", out e)) pi.Desc = e.GetString();
                    if (it.TryGetProperty("healing", out e)) pi.Healing = e.GetBoolean();
                    if (it.TryGetProperty("stats", out e)) foreach (var t in e.EnumerateArray()) pi.Stats.Add(t.GetString());
                    Items.Add(pi);
                }
            }
            Console.WriteLine("\n\n################ 0.10 checks (" + Powers.Count + " powerups, " + Items.Count + " items from the probe)");
            int bad = Validate();
            Clock();
            Modes();
            Evolutions();
            Branches();
            return bad == 0 ? 0 : 3;
        }

        static PowerFacts Find(string name)
        {
            foreach (var kv in Powers) if (Norm(kv.Key) == Norm(name)) return kv.Value;      // "Laser" is Engineer's weapon, not Holo-bait: Laser
            foreach (var kv in Powers) if (Same(kv.Key, name)) return kv.Value;
            return null;
        }

        // ------------------------------------------------------------ 1. names
        static int Validate()
        {
            Console.WriteLine("\n=== preset and kit names against the game");
            int bad = 0;
            Action<string, string> need = (what, name) =>
            {
                var f = Find(name);
                if (f == null) { Console.WriteLine("  MISSING  " + what + ": '" + name + "'"); bad++; }
            };
            foreach (var kit in Builds.Kits)
            {
                foreach (var w in kit.Line) { need(kit.Survivor + " weapon", w); if (Find(w) != null && ClassOf.ContainsKey(Find(w).Name) && ClassOf[Find(w).Name] != kit.Survivor) { Console.WriteLine("  WRONG CLASS " + w + " is " + ClassOf[Find(w).Name]); bad++; } }
                foreach (var a in kit.Abilities) foreach (var n in a) need(kit.Survivor + " ability", n);
            }
            foreach (var b in Builds.Presets)
            {
                var kit = Builds.KitOf(b.Survivor);
                if (kit == null) { Console.WriteLine("  NO KIT for " + b.Id); bad++; continue; }
                if (b.Branch.Length > 0 && !Same(b.Branch, kit.BranchA) && !Same(b.Branch, kit.BranchB)) { Console.WriteLine("  BAD BRANCH " + b.Id + ": " + b.Branch); bad++; }
                foreach (var a in b.Abilities.Concat(b.Skip)) if (!kit.Abilities.Any(x => Same(x[0], a))) { Console.WriteLine("  BAD ABILITY " + b.Id + ": " + a); bad++; }
                foreach (var ev in b.Evolution) { var evos = kit.EvolutionsOf(ev.Key); if (!evos.Any(x => Same(x, ev.Value))) { Console.WriteLine("  BAD EVOLUTION " + b.Id + ": " + ev.Key + " -> " + ev.Value); bad++; } }
                if (b.Abilities.Distinct(StringComparer.OrdinalIgnoreCase).Count() != b.Abilities.Count) { Console.WriteLine("  DUPLICATE ABILITY in " + b.Id); bad++; }
            }
            Console.WriteLine(bad == 0 ? "  all " + Builds.Presets.Count + " presets and 9 kits match the game's names" : "  " + bad + " problem(s)");
            foreach (var sv in Builds.Survivors) Console.WriteLine("  " + sv.PadRight(9) + string.Join("  |  ", Builds.PresetsOf(sv).Select(b => b.Name + " (" + b.Style + (b.Branch.Length > 0 ? ", " + b.Branch : "") + ")")));
            return bad;
        }

        // ------------------------------------------------------------ 2. the run clock
        static ItemContext Ctx(RunContext run, params string[] squad)
        {
            var k = Knowledge.FromJson(Knowledge.DefaultJson);
            var tags = new TagProfile { SpecialAt = 10 };
            tags.Source("Assault Rifle", 1.0, new[] { "Kinetic" }); tags.Source("Helicopter Strike", 0.4, new[] { "Kinetic" }); tags.Source("Tesla", 0.7, new[] { "Electric" });
            tags.Points["Kinetic"] = 8; tags.Points["Electric"] = 4;
            var c = new ItemContext { Squad = squad, Tags = tags, K = k, Ctx = run, OwnedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Deployable" }, Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase) };
            c.CloseShare = 0.5; c.LongShare = 0; c.ClipShare = 1; c.AbilityLean = 0.4; c.CritSquad = true;
            return c;
        }

        static double ScoreItem(ProbeItem it, ItemContext c, List<string> why)
        {
            c.Stats = it.Stats; c.Healing = it.Healing;
            return ItemRules.Evaluate(it.Name, it.Desc, c, why);
        }

        static void Clock()
        {
            Console.WriteLine("\n=== the run clock: SWAT + Engineer, Default mode (20:00), the same item at 02:00 / 10:00 / 18:30");
            string[] watch = { "Wooden Stick", "Chick Magnet", "Electric Personality", "Black Box", "Ring Of Power", "Quick Buck", "Boxing Gloves", "MedKit", "Frozen Heart", "Nine Inch Nails", "Magazine Clip", "Silencer", "Dartboard" };
            var times = new[] { 120.0, 600.0, 1110.0 };
            Console.WriteLine("  " + "item".PadRight(22) + string.Join("", times.Select(t => TimeSpan.FromSeconds(t).ToString(@"mm\:ss").PadLeft(8))) + "   reasons at 18:30");
            foreach (var name in watch)
            {
                var it = Items.FirstOrDefault(x => x.Name == name); if (it == null) { Console.WriteLine("  (no item " + name + ")"); continue; }
                var cells = new List<string>(); List<string> last = null;
                foreach (var t in times)
                {
                    var run = new RunContext { Mode = "Normal", Goal = 1200, Seconds = t, LevelRate = t < 300 ? 3.5 : 2.2, LevelUps = (int)(t / 25), D = new Doctrine() };
                    last = new List<string>(); cells.Add(ScoreItem(it, Ctx(run, "SWAT", "Engineer"), last).ToString("0.00").PadLeft(8));
                }
                Console.WriteLine("  " + name.PadRight(22) + string.Join("", cells) + "   " + string.Join("; ", last));
            }
            Console.WriteLine("\n  context lines:");
            foreach (var t in times) Console.WriteLine("    " + new RunContext { Mode = "Normal", Goal = 1200, Seconds = t, LevelRate = t < 300 ? 3.5 : 2.2, D = new Doctrine() });
            var late = new RunContext { Mode = "Normal", Goal = 1200, Seconds = 1110, LevelRate = 2.0, D = new Doctrine() };
            Console.WriteLine("    reach at 18:30: 1 pick " + late.Reach(1).ToString("0.00") + ", 3 picks " + late.Reach(3).ToString("0.00") + ", 5 picks " + late.Reach(5).ToString("0.00") + "; recruit value " + late.RecruitValue.ToString("0.00"));
            var early = new RunContext { Mode = "Normal", Goal = 1200, Seconds = 160, LevelRate = 3.5, D = new Doctrine() };
            Console.WriteLine("    reach at 02:40: 5 picks " + early.Reach(5).ToString("0.00") + "; recruit value " + early.RecruitValue.ToString("0.00"));
        }

        static void Modes()
        {
            Console.WriteLine("\n=== the game mode: the same items at the halfway mark of each mode");
            string[] watch = { "MedKit", "Frozen Heart", "Boxing Gloves", "Wooden Stick", "ACME Anvil", "Mouse Trap" };
            var modes = new[] { Tuple.Create("Normal", 1200.0), Tuple.Create("Hardcore", 600.0), Tuple.Create("OneHit", 300.0), Tuple.Create("BossRush", 600.0), Tuple.Create("Endless", 0.0) };
            Console.WriteLine("  " + "item".PadRight(16) + string.Join("", modes.Select(m => m.Item1.PadLeft(10))));
            foreach (var name in watch)
            {
                var it = Items.FirstOrDefault(x => x.Name == name); if (it == null) continue;
                var cells = new List<string>();
                foreach (var m in modes)
                {
                    var run = new RunContext { Mode = m.Item1, Goal = m.Item2, Seconds = m.Item2 > 0 ? m.Item2 / 2 : 900, LevelRate = 2.5, Difficulty = 3, D = new Doctrine() };
                    cells.Add(ScoreItem(it, Ctx(run, "SWAT", "Engineer"), new List<string>()).ToString("0.00").PadLeft(10));
                }
                Console.WriteLine("  " + name.PadRight(16) + string.Join("", cells));
            }
            foreach (var m in modes) Console.WriteLine("    " + new RunContext { Mode = m.Item1, Goal = m.Item2, Seconds = m.Item2 > 0 ? m.Item2 / 2 : 900, LevelRate = 2.5, Difficulty = 3, D = new Doctrine() });
        }

        // ------------------------------------------------------------ 3. evolutions and branches by squad
        static TagProfile Squad(params object[] sources)
        {
            var t = new TagProfile { SpecialAt = 10 };
            for (int i = 0; i + 1 < sources.Length; i += 2)
            {
                var f = Find((string)sources[i]); double w = Convert.ToDouble(sources[i + 1]);
                if (f != null) { t.Source(f.Name, w, f.Damage); foreach (var d in f.Damage) t.Points[d] = t.PointsOf(d) + (int)Math.Round(w * 4); }
            }
            return t;
        }

        static void Evolutions()
        {
            Console.WriteLine("\n=== which evolution fits which squad (no build selected: the live rules decide)");
            var run = new RunContext { Mode = "Normal", Goal = 1200, Seconds = 600, LevelRate = 2.5, D = new Doctrine() };
            var cases = new[]
            {
                Tuple.Create("Tank's Bombing Strike, with Engineer (Tesla, Electric Turret)", "Bombing Strike", Squad("Rocket Launcher", 1.0, "Bombing Strike", 0.5, "Tesla", 1.0, "Electric Turret", 0.5), new List<TeamBoost>()),
                Tuple.Create("Tank's Bombing Strike, with Medic (Antidote Flasks, Experiment 21)", "Bombing Strike", Squad("Rocket Launcher", 1.0, "Bombing Strike", 0.5, "Antidote Flasks", 1.0, "Experiment 21", 0.5), new List<TeamBoost>()),
                Tuple.Create("SWAT's Helicopter Strike, Grenade Expertise bought", "Helicopter Strike", Squad("Assault Rifle", 1.0, "Helicopter Strike", 0.5), new List<TeamBoost> { new TeamBoost { Name = "Grenade Expertise", Owner = "SWAT", Tag = "Grenade" } }),
                Tuple.Create("SWAT's Automatic Turret, with Huntress and Trap Expertise", "Automatic Turret", Squad("Assault Rifle", 1.0, "Automatic Turret", 0.5, "Bow", 0.7), new List<TeamBoost> { new TeamBoost { Name = "Trap Expertise", Owner = "Huntress", Tag = "Taunt", AbilitiesOnly = true } }),
                Tuple.Create("Tank's Sawblade Drone, with Pyro (Flamethrower) and Mechanic (Nitro-Gun)", "Sawblade Drone", Squad("Shotgun", 1.0, "Sawblade Drone", 0.5, "Flamethrower", 1.0, "Nitro-Gun", 1.0), new List<TeamBoost>()),
            };
            foreach (var c in cases)
            {
                var kit = Builds.Kits.First(k => k.Abilities.Any(a => Same(a[0], c.Item2)));
                var evos = kit.EvolutionsOf(c.Item2); var bf = Find(c.Item2);
                Console.WriteLine("  " + c.Item1);
                foreach (var e in evos)
                {
                    var why = new List<string>(); var ef = Find(e);
                    double fit = Synergy.EvolutionFit(ef, bf, c.Item3, c.Item4, run, why);
                    Console.WriteLine("     " + fit.ToString("0.00").PadLeft(5) + "  " + e.PadRight(34) + string.Join("; ", why));
                }
            }
        }

        static void Branches()
        {
            Console.WriteLine("\n=== which tier-2 branch fits the rest of the squad (Auto, no tree investment, before the guides' nudge of +0.9)");
            var run = new RunContext { Mode = "Normal", Goal = 1200, Seconds = 420, LevelRate = 3, D = new Doctrine() };
            var cases = new[]
            {
                Tuple.Create("Tank, with SWAT (Assault Rifle, Helicopter Strike)", "Tank", Squad("Minefield", 0.4, "Assault Rifle", 1.0, "Helicopter Strike", 0.5)),
                Tuple.Create("Tank alone, Minefield and Bombing Strike owned", "Tank", Squad("Minefield", 0.5, "Bombing Strike", 0.5)),
                Tuple.Create("Huntress, with Mechanic (Nitro-Gun, Ice Turret)", "Huntress", Squad("Arrow Rain", 0.4, "Nitro-Gun", 1.0, "Ice Turret", 0.5)),
                Tuple.Create("Medic, with Pyro (Flamethrower) and Tank (Bombing Strike: Bioweapon)", "Medic", Squad("Experiment 21", 0.4, "Flamethrower", 1.0, "Bombing Strike: Bioweapon", 0.6)),
                Tuple.Create("Ranger, with Engineer (Tesla, Electrocution)", "Ranger", Squad("Falcon", 0.4, "Tesla", 1.0, "Electrocution", 0.5)),
            };
            foreach (var c in cases)
            {
                var kit = Builds.KitOf(c.Item2);
                Console.WriteLine("  " + c.Item1);
                foreach (var b in new[] { kit.BranchA, kit.BranchB })
                {
                    var why = new List<string>();
                    double fit = Synergy.BranchFit(Find(b), c.Item3, new List<TeamBoost>(), run, why);
                    Console.WriteLine("     " + fit.ToString("0.00").PadLeft(5) + "  " + b.PadRight(20) + string.Join("; ", why));
                }
            }
        }
    }
}
