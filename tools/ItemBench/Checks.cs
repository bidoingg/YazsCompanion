// The second half of the bench: what 0.10 added. Run with a probe.json (the mod's [Debug] Probe dump: every item and
// powerup with the exact fields the game uses) and it
//   1. VALIDATES the build presets and kits against the game's own names - a misspelt evolution would never match,
//   2. shows the run clock at work: the same items and picks early, mid-run and near the end, and per game mode,
//   3. shows which evolution and which weapon branch the live synergy rules pick for a few squads,
//   4. replays item, Research Pod and evolution offers of the logged run of 2026-09-19 (the 0.11 advice review),
//   5. (0.12.2) checks the weapon fork - three tier-3 branches per survivor, each accepted in a build - against the game's
//      data, walks the Training Yard plan of every survivor over the tree in gamedata.json, and replays the recruit ties
//      and evolution headlines of the Steam Deck logs of 2026-09-23 / 24.
//   6. (0.12.2) replays the reroll hint (RerollCall) on the rescue screens of those logs - the four the player rerolled
//      must speak, the offers after the reroll must not - and on made-up screens for its edges (the margin, ties, no reroll
//      left, late in the run, Liberate on top, a full squad).
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
    sealed class ProbeWeapon { public string Asset, Name, Class, Previous, Tier; }

    static class Checks
    {
        static readonly Dictionary<string, PowerFacts> Powers = new Dictionary<string, PowerFacts>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, string> ClassOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly List<ProbeItem> Items = new List<ProbeItem>();
        static readonly List<ProbeWeapon> Weapons = new List<ProbeWeapon>();

        static string Norm(string s) { return new string((s ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray()); }
        static bool Same(string a, string b)
        {
            if (Norm(a) == Norm(b)) return true;
            int ia = a.IndexOf(':'), ib = b.IndexOf(':');
            string ta = Norm(ia >= 0 ? a.Substring(ia + 1) : a), tb = Norm(ib >= 0 ? b.Substring(ib + 1) : b);
            return ta.Length > 2 && ta == tb;
        }

        public static int Run(string probePath, string gamedataPath = null)
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
                    if (f.IsWeapon)
                    {
                        var w = new ProbeWeapon { Name = f.Name };
                        if (p.TryGetProperty("asset", out e)) w.Asset = e.GetString();
                        if (p.TryGetProperty("class", out e)) w.Class = e.GetString();
                        if (p.TryGetProperty("previousWeapon", out e) && e.ValueKind == JsonValueKind.String) w.Previous = e.GetString();
                        if (p.TryGetProperty("weaponTier", out e) && e.ValueKind == JsonValueKind.String) w.Tier = e.GetString();
                        Weapons.Add(w);
                    }
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
            bad += Forks();
            Clock();
            Modes();
            Evolutions();
            Branches();
            bad += Yard(gamedataPath);
            Logged();
            Deck0924();
            bad += RerollHints();
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
                if (b.Branch.Length > 0 && !kit.Branches.Any(x => Same(b.Branch, x))) { Console.WriteLine("  BAD BRANCH " + b.Id + ": " + b.Branch); bad++; }
                foreach (var a in b.Abilities.Concat(b.Skip)) if (!kit.Abilities.Any(x => Same(x[0], a))) { Console.WriteLine("  BAD ABILITY " + b.Id + ": " + a); bad++; }
                foreach (var ev in b.Evolution) { var evos = kit.EvolutionsOf(ev.Key); if (!evos.Any(x => Same(x, ev.Value))) { Console.WriteLine("  BAD EVOLUTION " + b.Id + ": " + ev.Key + " -> " + ev.Value); bad++; } }
                if (b.Abilities.Distinct(StringComparer.OrdinalIgnoreCase).Count() != b.Abilities.Count) { Console.WriteLine("  DUPLICATE ABILITY in " + b.Id); bad++; }
                // 0.12.2: the text and the branch agree - a tier-3 weapon the summary names is the build's branch (an empty branch is decided
                // live, and the Training Yard then pushed the guides' weapon first), and no text promises another tier-3 weapon after its
                // branch ("on to X", "up to the X": the three exclude each other)
                var named = kit.Branches.Where(x => System.Text.RegularExpressions.Regex.IsMatch(b.Summary ?? "", @"(?<![A-Za-z-])" + System.Text.RegularExpressions.Regex.Escape(x) + @"(?![A-Za-z-])")).ToList();
                if (named.Count > 0 && b.Branch.Length == 0) { Console.WriteLine("  BAD TEXT " + b.Id + ": its summary names " + string.Join(", ", named) + " but its branch is decided live"); bad++; }
                else if (named.Count > 0 && !named.Any(x => Same(x, b.Branch))) { Console.WriteLine("  BAD TEXT " + b.Id + ": its summary names " + string.Join(", ", named) + ", not its branch " + b.Branch); bad++; }
                foreach (var x in kit.Branches)
                    if (!Same(x, b.Branch) && System.Text.RegularExpressions.Regex.IsMatch(b.Summary ?? "", @"\b(?:on |up )?to (?:the )?" + System.Text.RegularExpressions.Regex.Escape(x) + @"(?![A-Za-z-])"))
                    { Console.WriteLine("  BAD TEXT " + b.Id + ": its summary promises " + x + " after its branch " + (b.Branch.Length > 0 ? b.Branch : "(decided live)") + " - the tier-3 weapons exclude each other"); bad++; }
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
            Console.WriteLine("\n=== which tier-3 branch fits the rest of the squad (Auto, no tree investment, before the guides' nudge of +0.9)");
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
                foreach (var b in kit.Branches)
                {
                    var why = new List<string>();      // the reason names only what not every other branch of the fork deals too (as the live path)
                    double fit = Synergy.BranchFit(Find(b), c.Item3, new List<TeamBoost>(), run, why, kit.Branches.Where(x => x != b).Select(Find).ToList());
                    Console.WriteLine("     " + fit.ToString("0.00").PadLeft(5) + "  " + b.PadRight(20) + string.Join("; ", why));
                }
            }
        }

        // ------------------------------------------------------------ 5a. the weapon fork (0.12.2, F05)
        // Every survivor's weapon line is a starting weapon (Tier1), its upgrade (Tier2) and THREE Tier3 weapons that each
        // follow the upgrade - the fifth weapon was taken for a "final weapon" up to 0.12.1. Checked against the probe, and
        // every branch must survive a build pack (Builds.Fit), which is where a lent build naming the fifth weapon was dropped.
        static int Forks()
        {
            Console.WriteLine("\n=== the weapon fork: three tier-3 branches per survivor, each one a build may name");
            int bad = 0;
            Func<string, ProbeWeapon> weapon = name => Weapons.FirstOrDefault(w => Norm(w.Name) == Norm(name));
            foreach (var kit in Builds.Kits)
            {
                var problems = new List<string>();
                var start = weapon(kit.Line[0]); var up = weapon(kit.Line[1]);
                if (start == null || start.Tier != "Tier1" || !string.IsNullOrEmpty(start.Previous)) problems.Add(kit.Line[0] + " is not a starting weapon");
                if (up == null || up.Tier != "Tier2" || !Same(up.Previous ?? "", kit.Line[0])) problems.Add(kit.Line[1] + " does not follow " + kit.Line[0]);
                foreach (var br in kit.Branches)
                {
                    var w = weapon(br);
                    if (w == null || w.Tier != "Tier3" || !Same(w.Previous ?? "", kit.Line[1])) problems.Add(br + " is not a tier-3 weapon after " + kit.Line[1]);
                    var said = new List<string>();
                    var pack = Builds.ParsePack("{ \"builds\": [ { \"id\": \"bench\", \"branch\": \"" + br.ToUpperInvariant() + "\" } ] }", "bench", kit.Survivor, said.Add);
                    if (pack.Builds.Count != 1 || pack.Builds[0].Branch != br || said.Count > 0) problems.Add("a build naming " + br + " is not accepted (" + string.Join("; ", said) + ")");
                }
                foreach (var w in Weapons.Where(x => x.Class == kit.Survivor && x.Tier == "Tier3" && !kit.Branches.Any(b => Same(b, x.Name))))
                    problems.Add(w.Name + " is a tier-3 weapon of " + kit.Survivor + " missing from the kit");
                if (problems.Count > 0) { foreach (var pr in problems) Console.WriteLine("  BAD FORK " + kit.Survivor + ": " + pr); bad += problems.Count; }
                else Console.WriteLine("  " + kit.Survivor.PadRight(9) + (kit.Line[0] + " > " + kit.Line[1] + " > ").PadRight(36) + string.Join(" | ", kit.Branches).PadRight(52) + "game data ok, a build may name all three");
            }
            return bad;
        }

        // ------------------------------------------------------------ 5b. the Training Yard plan (0.12.2, F05)
        // TreePlan over each survivor's tree as gamedata.json has it (a fresh tree: every node at its minimum level, every
        // rank open), the weapon nodes tagged with the depth of the weapon they boost as TreeState does live. What is
        // printed: the weapon steps in plan order ("Infernax>3 @10" = step 10 takes Infernax to 3); the check: the main
        // branch comes before the two other tier-3 weapons, and those come after the rank V passives.
        static List<TNode> TreeOf(JsonElement nodes, string tree, string branch, Dictionary<string, int> levels = null)
        {
            var k = Knowledge.FromJson(Knowledge.DefaultJson); var list = new List<TNode>();
            var kinds = new Dictionary<string, TKind>(StringComparer.OrdinalIgnoreCase) { { "weapon", TKind.Weapon }, { "ability", TKind.Ability }, { "evolution", TKind.Evolution }, { "synergy", TKind.Synergy }, { "badge", TKind.Badge }, { "passive", TKind.Passive }, { "stat", TKind.Stat }, { "mechanic", TKind.Mechanic } };
            foreach (var p in nodes.EnumerateObject())
            {
                var o = p.Value; JsonElement e;
                if (o.GetProperty("tree").GetString() != tree) continue;
                var n = new TNode { Key = p.Name, Name = o.GetProperty("name").GetString() ?? "", Rank = o.GetProperty("rank").GetInt32(), Slot = o.GetProperty("slot").GetInt32() };
                if (o.TryGetProperty("desc", out e) && e.ValueKind == JsonValueKind.String) n.Desc = e.GetString();
                TKind kind; if (kinds.TryGetValue(o.GetProperty("kind").GetString() ?? "", out kind)) n.Kind = kind;
                n.Min = o.GetProperty("levelMin").GetInt32(); n.Max = o.GetProperty("levelMax").GetInt32();
                n.Costs = o.GetProperty("costs").EnumerateArray().Select(x => x.GetInt32()).ToArray();
                foreach (var pre in o.GetProperty("prerequisites").EnumerateArray()) n.Prereqs.Add(pre.GetString());
                int lvl; n.Level = levels != null && levels.TryGetValue(n.Name, out lvl) ? lvl : n.Min;
                string tier;
                if (n.Kind == TKind.Ability && k.AbilityTier.TryGetValue(n.Name, out tier)) n.Tier = tier;
                if (n.Kind == TKind.Weapon && o.TryGetProperty("target", out e) && e.ValueKind == JsonValueKind.String)
                {
                    var w = Weapons.FirstOrDefault(x => x.Asset == e.GetString());
                    if (w != null) n.WeaponDepth = w.Tier == "Tier1" ? 0 : w.Tier == "Tier2" ? 1 : 2;
                    n.GuideBranch = branch != null && w != null && Same(w.Name, branch);
                }
                list.Add(n);
            }
            foreach (var n in list.Where(x => x.Kind == TKind.Evolution))
            {
                var baseNode = list.FirstOrDefault(x => x.Kind == TKind.Ability && n.Prereqs.Contains(x.Key));
                if (baseNode != null) { n.BaseKey = baseNode.Key; n.Tier = baseNode.Tier; }
            }
            return list;
        }

        static int YardCase(string label, List<TNode> nodes, Kit kit)
        {
            var steps = TreePlan.ClassSteps(nodes);
            var cells = new List<string>(); var at = new Dictionary<TNode, int>(); var last = new Dictionary<TNode, int>();
            for (int i = 0; i < steps.Count; i++)
            {
                var st = steps[i]; if (st.Node.Kind != TKind.Weapon) continue;
                if (!at.ContainsKey(st.Node)) at[st.Node] = i + 1;
                last[st.Node] = i + 1;
                cells.Add(st.Node.Name + ">" + st.To + " @" + (i + 1));
            }
            var fork = nodes.Where(n => n.Kind == TKind.Weapon && n.WeaponDepth >= 2).ToList();
            var main = fork.Where(at.ContainsKey).OrderBy(n => at[n]).FirstOrDefault();
            int lastPassive = 0; for (int i = 0; i < steps.Count; i++) if (steps[i].Node.Kind == TKind.Passive) lastPassive = i + 1;
            var problems = new List<string>();
            if (fork.Count != 3) problems.Add(fork.Count + " fork nodes, not 3");
            if (main == null) problems.Add("no branch in the plan");
            else foreach (var o in fork) if (o != main && at.ContainsKey(o) && (at[o] < last[main] || at[o] < lastPassive)) problems.Add(o.Name + " before the end of " + main.Name + " or the rank V passives");
            Console.WriteLine("  " + kit.Survivor.PadRight(9) + label.PadRight(33) + string.Join(", ", cells) + " (of " + steps.Count + ")" + (problems.Count == 0 ? "" : "   BAD YARD: " + string.Join("; ", problems)));
            return problems.Count;
        }

        static int Yard(string gamedataPath)
        {
            Console.WriteLine("\n=== the Training Yard plan per survivor: weapon steps in plan order (fresh tree, every rank open)");
            if (string.IsNullOrEmpty(gamedataPath) || !File.Exists(gamedataPath)) { Console.WriteLine("  (no gamedata.json: skipped)"); return 0; }
            int bad = 0;
            var k = Knowledge.FromJson(Knowledge.DefaultJson);
            using (var doc = JsonDocument.Parse(File.ReadAllText(gamedataPath)))
            {
                var nodes = doc.RootElement.GetProperty("nodes");
                foreach (var kit in Builds.Kits)
                {
                    string tree = kit.Survivor; string guide; k.WeaponBranch.TryGetValue(kit.Survivor, out guide);
                    bad += YardCase(guide != null ? "Auto (guides: " + guide + ")" : "Auto (no guide branch)", TreeOf(nodes, tree, guide), kit);
                    bad += YardCase("a build on " + kit.Line[4], TreeOf(nodes, tree, kit.Line[4]), kit);
                }
                // the Deck's Training Yard of 2026-09-23: a Pyro build on Infernax, 3 points; 0.12.1 said "buy Axerangs 2>3 (3);
                // save for Molotov Cocktail 3>4 (4)" - the levels below give exactly that under the old plan
                var done = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { "Fireaxe", 3 }, { "Blowtorch", 4 }, { "Molotov Cocktail", 3 }, { "No Pain, No Gain", 5 }, { "Molotov Cocktail Evolutions", 1 }, { "No pain no gain Evolutions", 1 }, { "Infernax", 3 }, { "Axerangs", 2 } };
                var pyro = TreeOf(nodes, "Pyro", "Infernax", done);
                var advice = TreePlan.Advise(pyro, TreePlan.ClassSteps(pyro), 3);
                bool wrong = advice.Now.Any(b => b.Node.Name == "Axerangs");
                Console.WriteLine("  the Deck's Pyro tree (a build on Infernax, Axerangs at 2), 3 points: buy " + (advice.Now.Count > 0 ? string.Join(", ", advice.Now.Select(b => b.Label + " (" + b.Cost + ")")) : "nothing")
                    + (advice.SaveFor != null ? "; save for " + advice.SaveFor.Label : "") + (wrong ? "   BAD YARD: Axerangs bought against the build" : ""));
                if (wrong) bad++;
            }
            return bad;
        }

        // ------------------------------------------------------------ 5c. the Steam Deck logs of 2026-09-23 / 24 (0.12.2, F12)
        static void Deck0924()
        {
            Console.WriteLine("\n=== the Steam Deck logs of 2026-09-23 / 24: recruit ties and evolution headlines");
            var k = Knowledge.FromJson(Knowledge.DefaultJson);
            Func<string, int, double, Recruit> recruit = (name, cls, score) => { string t; k.RescueTier.TryGetValue(name, out t); return new Recruit { Name = name, Class = cls, Score = score, Tier = t ?? "" }; };
            foreach (var offer in new[] { Tuple.Create("SOS at 01:13", 5.00), Tuple.Create("SOS at 02:52", 4.60) })
            {
                // on the screen: Tank on the left (an A-tier rescue whose team passive is bought), SWAT next to it (S-tier)
                var onScreen = new List<Recruit> { recruit("Tank", 1, offer.Item2), recruit("SWAT", 0, offer.Item2) };
                string before = onScreen.OrderByDescending(r => Math.Round(r.Score, 2)).First().Name;                     // up to 0.12.1: the card further left
                string rowBefore = onScreen.OrderBy(r => r.Class).OrderByDescending(r => Math.Round(r.Score, 2)).First().Name;   // the SOS row: the class order
                var cards = onScreen.Select((r, i) => Tuple.Create(r, i)).OrderByDescending(t => Math.Round(t.Item1.Score, 2)).ThenBy(t => t.Item1, Comparer<Recruit>.Create(Recruit.Ties)).ThenBy(t => t.Item2).Select(t => t.Item1.Name).First();
                var row = onScreen.OrderBy(r => r.Class).ToList(); row.Sort(Recruit.Compare);
                Console.WriteLine("  " + offer.Item1 + ": Tank " + offer.Item2.ToString("0.00") + " (" + recruit("Tank", 1, 0).Tier + "-tier) = SWAT " + offer.Item2.ToString("0.00") + " (" + recruit("SWAT", 0, 0).Tier
                    + "-tier)   up to 0.12.1: card " + before + ", SOS row " + rowBefore + "   now: card " + cards + ", SOS row " + row[0].Name + (cards == row[0].Name ? "" : "   BAD: they differ"));
            }
            // a Huntress build that takes Arrow Rain: Downpour, and a level-up that offers only Arrow Rain: Thunderstruck
            var build = Builds.PresetsOf("Huntress").First(b => b.EvolutionOf("Arrow Rain") != null);
            string pick = build.EvolutionOf("Arrow Rain"), other = Builds.KitOf("Huntress").EvolutionsOf("Arrow Rain").First(e => e != pick);
            Console.WriteLine("  Arrow Rain, the " + build.Name + " build (takes " + pick + "):");
            Console.WriteLine("     " + ("only " + other + " offered:").PadRight(52) + Synergy.EvolutionHead("Arrow Rain", other, pick, build.Name, false, false));
            Console.WriteLine("     " + ("both offered, the card " + other + ":").PadRight(52) + Synergy.EvolutionHead("Arrow Rain", other, pick, build.Name, false, true));
            Console.WriteLine("     " + ("both offered, the card " + pick + ":").PadRight(52) + Synergy.EvolutionHead("Arrow Rain", pick, pick, build.Name, true, true));
        }

        // ------------------------------------------------------------ 6. the reroll hint on the rescue screen (0.12.2)
        // The Steam Deck logs of 2026-09-23 / 24: on four rescue screens the player rerolled (five rerolls) until the survivor the
        // readout's SOS row named came up. The scores are the logged [card] lines; who "could still come" is the survivors the
        // same screen showed after its reroll (same clock, same squad: the same scores). Every case says what it must give.
        static int RerollHints()
        {
            Console.WriteLine("\n=== the reroll hint on the rescue screen (margin " + RerollCall.Margin.ToString("0.00") + ")");
            var k = Knowledge.FromJson(Knowledge.DefaultJson);
            var classes = new[] { "SWAT", "Tank", "Engineer", "Huntress", "Ghost", "Medic", "Pyro", "Mechanic", "Ranger" };      // the game's class order
            Func<string, double, Recruit> r = (name, score) => { string t; k.RescueTier.TryGetValue(name, out t); return new Recruit { Name = name, Class = Array.IndexOf(classes, name), Score = score, Tier = t ?? "" }; };
            int bad = 0;
            Action<string, Recruit[], double, Recruit[], bool, double, bool> check = (label, offered, liberate, possible, canReroll, rv, want) =>
            {
                var c = RerollCall.Decide(offered, liberate, possible, canReroll, canReroll ? "3" : "0", rv);
                bool ok = c.Show == want;
                if (!ok) bad++;
                Console.WriteLine("  " + label.PadRight(64) + (c.Show ? "SHOWN      " : "not shown  ") + c.Why + (ok ? "" : "   BAD: wanted " + (want ? "shown" : "not shown")));
            };
            Recruit[] none = new Recruit[0];
            // 01:46, Huntress alone: the player rerolled Ranger / Pyro away and took the SWAT that came
            check("Deck 01:46 Ranger 4.09, Pyro 3.83 (rerolled)", new[] { r("Ranger", 4.09), r("Pyro", 3.83) }, 1.00, new[] { r("SWAT", 5.14), r("Engineer", 4.02) }, true, 1, true);
            check("Deck 01:46 after the reroll: SWAT 5.14, Engineer 4.02", new[] { r("SWAT", 5.14), r("Engineer", 4.02) }, 1.00, new[] { r("Ranger", 4.09), r("Pyro", 3.83) }, true, 1, false);
            // 01:57, Huntress alone: Medic / Ranger rerolled, Tank taken
            check("Deck 01:57 Medic 4.18, Ranger 4.09 (rerolled)", new[] { r("Medic", 4.18), r("Ranger", 4.09) }, 1.00, new[] { r("Tank", 5.14), r("Mechanic", 3.63) }, true, 1, true);
            check("Deck 01:57 after the reroll: Tank 5.14, Mechanic 3.63", new[] { r("Tank", 5.14), r("Mechanic", 3.63) }, 1.00, new[] { r("Medic", 4.18), r("Ranger", 4.09) }, true, 1, false);
            // 05:55, Pyro + SWAT: two rerolls until the Tank came
            check("Deck 05:55 Huntress 4.64, Ghost 2.98 (rerolled)", new[] { r("Huntress", 4.64), r("Ghost", 2.98) }, 1.00, new[] { r("Tank", 5.93), r("Engineer", 4.03), r("Mechanic", 3.32), r("Ranger", 3.78) }, true, 1, true);
            check("Deck 05:55 1st reroll: Engineer 4.03, Mechanic 3.32 (rerolled)", new[] { r("Engineer", 4.03), r("Mechanic", 3.32) }, 1.00, new[] { r("Tank", 5.93), r("Ranger", 3.78), r("Huntress", 4.64), r("Ghost", 2.98) }, true, 1, true);
            check("Deck 05:55 2nd reroll: Tank 5.93, Ranger 3.78", new[] { r("Tank", 5.93), r("Ranger", 3.78) }, 1.00, new[] { r("Engineer", 4.03), r("Mechanic", 3.32), r("Huntress", 4.64), r("Ghost", 2.98) }, true, 1, false);
            // 01:13, Mechanic alone: Ranger / Pyro rerolled, then Tank = SWAT on the cards
            check("Deck 01:13 Ranger 3.94, Pyro 3.70 (rerolled)", new[] { r("Ranger", 3.94), r("Pyro", 3.70) }, 1.00, new[] { r("Tank", 5.00), r("SWAT", 5.00) }, true, 1, true);
            check("Deck 01:13 after the reroll: Tank 5.00 = SWAT 5.00", new[] { r("Tank", 5.00), r("SWAT", 5.00) }, 1.00, new[] { r("Ranger", 3.94), r("Pyro", 3.70) }, true, 1, false);
            // the edges
            check("tie: Tank 5.00 on the cards, SWAT 5.00 could come", new[] { r("Tank", 5.00), r("Pyro", 3.70) }, 1.00, new[] { r("SWAT", 5.00) }, true, 1, false);
            check("small gap: Medic 4.60 on the cards, SWAT 5.20 could come", new[] { r("Medic", 4.60) }, 1.00, new[] { r("SWAT", 5.20) }, true, 1, false);
            check("gap 0.74: Medic 4.26, SWAT 5.00", new[] { r("Medic", 4.26) }, 1.00, new[] { r("SWAT", 5.00) }, true, 1, false);
            check("gap 0.75 exactly: Medic 4.25, SWAT 5.00", new[] { r("Medic", 4.25) }, 1.00, new[] { r("SWAT", 5.00) }, true, 1, true);
            check("no reroll left (Deck 05:55 again)", new[] { r("Huntress", 4.64), r("Ghost", 2.98) }, 1.00, new[] { r("Tank", 5.93) }, false, 1, false);
            check("late: recruit value 0.30, the cards say Liberate", new[] { r("Ranger", 1.80), r("Pyro", 1.60) }, 3.24, new[] { r("Tank", 2.90) }, true, 0.30, false);
            check("Liberate on top (3.40), Tank 4.50 could come", new[] { r("Ghost", 2.10), r("Mechanic", 2.30) }, 3.40, new[] { r("Tank", 4.50) }, true, 0.8, true);
            check("full squad: Liberate alone", none, 5.00, none, true, 1, false);
            check("nobody else could come (all on the cards)", new[] { r("Ranger", 3.94), r("Pyro", 3.70) }, 1.00, none, true, 1, false);
            Console.WriteLine("  " + (bad == 0 ? "all as wanted" : bad + " BAD"));
            return bad;
        }

        // ------------------------------------------------------------ 4. offers of the logged run of 2026-09-19 (the 0.11 advice review)
        // Ghost (Thousand Cuts, Shuriken, Pulsar) + Tank (Shotgun, Sawblade Drone: Enchantment, Bombing Strike) + Huntress (Bow,
        // Bear Trap): a Slashing stack, a Kinetic second, no magazine on the squad. What the item and tag rules say there.
        static TagProfile LoggedTags(int slashing, int kinetic, int rest)
        {
            var t = new TagProfile { SpecialAt = 10 };
            t.Source("Thousand Cuts", 1.0, new[] { "Slashing" }); t.Source("Shuriken", 0.4, new[] { "Slashing" }); t.Source("Sawblade Drone: Enchantment", 0.5, new[] { "Slashing", "Fire", "Ice" });
            t.Source("Pump-Action Shotgun", 0.8, new[] { "Kinetic" }); t.Source("Bow", 0.6, new[] { "Kinetic" }); t.Source("Pulsar", 0.15, new[] { "Electric" }); t.Source("Bombing Strike", 0.15, new[] { "Explosive" });
            t.Points["Slashing"] = slashing; t.Points["Kinetic"] = kinetic;
            foreach (var n in new[] { "Fire", "Ice", "Electric", "Explosive" }) t.Points[n] = rest;
            return t;
        }

        static ItemContext LoggedCtx(double seconds, TagProfile tags, double clipShare, params string[] squad)
        {
            var run = new RunContext { Mode = "Normal", Goal = 1200, Seconds = seconds, LevelRate = 2.6, LevelUps = (int)(seconds / 25), D = new Doctrine() };
            var c = new ItemContext { Squad = squad, Tags = tags, K = Knowledge.FromJson(Knowledge.DefaultJson), Ctx = run, OwnedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Deployable", "Melee", "Projectile" }, Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase) };
            c.CloseShare = 0.67; c.LongShare = 0; c.ClipShare = clipShare; c.AbilityLean = 0.45; c.CritSquad = true;
            return c;
        }

        static void Line(string label, string item, ItemContext c)
        {
            var it = Items.FirstOrDefault(x => x.Name == item); if (it == null) { Console.WriteLine("  (no item " + item + ")"); return; }
            var why = new List<string>(); double sc = ScoreItem(it, c, why);
            Console.WriteLine("  " + label.PadRight(34) + sc.ToString("0.00").PadLeft(6) + "  " + item.PadRight(21) + string.Join("; ", why));
        }

        static void Logged()
        {
            Console.WriteLine("\n=== the logged run (Ghost + Tank + Huntress, Slashing stacked): what the item and tag rules say");
            string[] three = { "Ghost", "Tank", "Huntress" };
            var at0646 = LoggedTags(19, 5, 1);
            foreach (var name in new[] { "Last Round", "Magazine Clip", "Power Glove" })
            {
                Line("06:46, no magazine on the squad", name, LoggedCtx(406, at0646, 0, three));
                Line("06:46, one weapon in three reloads", name, LoggedCtx(406, at0646, 1 / 3.0, three));
            }
            Line("09:01, squad of 1", "Duct Tape", LoggedCtx(541, at0646, 0, "Ghost"));
            Line("09:01, squad of 2", "Duct Tape", LoggedCtx(541, at0646, 0, "Ghost", "Tank"));
            Line("09:01, squad of 3", "Duct Tape", LoggedCtx(541, at0646, 0, three));
            foreach (var name in new[] { "Crowbar", "Suspicious Pendrive", "Glass of Milk", "Vampire Survivor", "MedKit", "Coffee Cup" }) Line("03:00", name, LoggedCtx(180, LoggedTags(12, 1, 0), 0, "Ghost", "Tank"));
            foreach (var t in new[] { 406.0, 1060.0, 1187.0 }) Line(TimeSpan.FromSeconds(t).ToString(@"mm\:ss"), "Electric Personality", LoggedCtx(t, at0646, 0, three));
            Line("12:27, Slashing 32, Kinetic 13", "Ultra Instinct", LoggedCtx(747, LoggedTags(32, 13, 1), 0, three));
            Line("19:47, Slashing 33, Kinetic 26", "Ultra Instinct", LoggedCtx(1187, LoggedTags(33, 26, 2), 0, three));
            Line("Slashing 31, the rest spread thin", "Ultra Instinct", LoggedCtx(747, LoggedTags(31, 4, 5), 0, three));
            Line("Slashing 24: not switched on", "Ultra Instinct", LoggedCtx(747, LoggedTags(24, 13, 1), 0, three));
            Console.WriteLine("  Research Pod at 11:53 (Slashing 25, Kinetic 13):");
            var pod = LoggedTags(25, 13, 1);
            foreach (var type in new[] { "Slashing", "Kinetic", "Fire", "Chemical" }) { var why = new List<string>(); double sc = Tags.Score(type, 7, pod, why); Console.WriteLine("     " + sc.ToString("0.00").PadLeft(5) + "  #" + (type + " +7").PadRight(14) + string.Join("; ", why)); }
            Console.WriteLine("  the two Sawblade Drone evolutions on that squad at 05:03 (Slashing 17):");
            var evoTags = new TagProfile { SpecialAt = 10 };
            evoTags.Source("Thousand Cuts", 0.8, new[] { "Slashing" }); evoTags.Source("Shuriken", 0.3, new[] { "Slashing" }); evoTags.Source("Sawblade Drone", 0.5, new[] { "Slashing" });
            evoTags.Source("Shotgun", 0.6, new[] { "Kinetic" }); evoTags.Source("Pulsar", 0.15, new[] { "Electric" }); evoTags.Source("Bombing Strike", 0.15, new[] { "Explosive" });
            evoTags.Points["Slashing"] = 17; evoTags.Points["Kinetic"] = 2; evoTags.Points["Electric"] = 1; evoTags.Points["Explosive"] = 1;
            var run = new RunContext { Mode = "Normal", Goal = 1200, Seconds = 303, LevelRate = 3.3, D = new Doctrine() };
            foreach (var e in new[] { "Sawblade Drone: Enchantment", "Sawblade Drone: Cogwheels" })
            {
                var why = new List<string>(); double fit = Synergy.EvolutionFit(Find(e), Find("Sawblade Drone"), evoTags, new List<TeamBoost>(), run, why);
                Console.WriteLine("     " + fit.ToString("0.00").PadLeft(5) + "  card " + (7.6 + 0.6 * 0.1 + fit * 0.4).ToString("0.00") + "  " + e.PadRight(30) + string.Join("; ", why));
            }
        }
    }
}
