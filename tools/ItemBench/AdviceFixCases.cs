// 0.13.0: two findings from the user's 1.0.2 sessions of 2026-10-03 / 04, replayed offline (section 8 of the bench):
//   F02  a build pack another mod lends: Auto follows the tier-3 branch actually taken (Builds.AutoFor, Builds.For(survivor,
//        branch)); the card of the other branch is scored and worded as Auto when the build is a lent one Auto follows
//        (Synergy.OtherBranch); reasons name such a build "(Auto)", not as the player's own (Builds.Your, Synergy.EvolutionHead).
//        In the logged run a Tank on Auto followed a pack's default (branch Rocket Launcher) all run: the Super Shotgun card
//        read "2.00 - other branch; your build takes Rocket Launcher" four times, and after the player took it anyway the
//        abilities, the focus and the item leanings still followed the default.
//   F12  each reason once: the survival clause of an item that both armors and heals (ItemRules; Frozen Heart read "survival
//        matters now (the squad is hurting)" twice), and the rescue card's tier and bought-synergy lines under the headline that
//        sums them up (Recruit.Headline; "A-tier rescue, 1 bought synergy with the squad; A-tier rescue in the guides; ..." on
//        20 rescue cards).
// The pack below is made up for the bench with generic names (the repository is public); its shape is the logged one: a Tank
// pack whose default takes the Rocket Launcher, a second build for the Super Shotgun, a third that decides its branch live.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class AdviceFixes
    {
        const string PackJson = @"{
  ""title"": ""Bench Pack"", ""default"": ""rockets"",
  ""builds"": [
    { ""id"": ""rockets"", ""name"": ""Rockets"", ""branch"": ""Rocket Launcher"", ""style"": ""Weapon"",
      ""abilities"": [ ""Bombing Strike"", ""Minefield"", ""Sawblade Drone"", ""Fury Unleashed"" ], ""evolution"": { ""Sawblade Drone"": ""Cogwheels"" }, ""wants"": [ ""explosive"" ] },
    { ""id"": ""pellets"", ""name"": ""Pellets"", ""branch"": ""Super Shotgun"", ""style"": ""Weapon"",
      ""abilities"": [ ""Fury Unleashed"", ""Sawblade Drone"", ""Bombing Strike"", ""Minefield"" ], ""evolution"": { ""Sawblade Drone"": ""Enchantment"" }, ""wants"": [ ""critical"" ] },
    { ""id"": ""anything"", ""name"": ""Any Branch"", ""branch"": """", ""style"": ""Balanced"", ""abilities"": [ ""Sawblade Drone"", ""Minefield"" ] }
  ]
}";

        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string N(Build b) { return b == null ? "plain Auto" : b.Name; }

        public static int Run(List<ProbeItem> items)
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.13.0: the 10-03 / 10-04 sessions - F02 a lent build pack follows the tier-3 branch taken, F12 each reason once");
            LentPacks();
            Wording();
            ItemClauses(items);
            RescueHeadlines();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        // ---- F02: what Auto follows (pure: Builds.AutoFor; then the whole way through a provider, as in the game)
        static void LentPacks()
        {
            var warns = new List<string>();
            var pack = Builds.ParsePack(PackJson, "bench", "Tank", s => warns.Add(s));
            Check("F02", "the bench pack reads cleanly", warns.Count == 0 && pack.Builds.Count == 3 && pack.Default != null && pack.Default.Name == "Rockets",
                warns.Count > 0 ? string.Join("; ", warns) : pack.Builds.Count + " builds, default " + N(pack.Default));
            if (pack.Builds.Count != 3) return;
            Build rockets = pack.Builds[0], pellets = pack.Builds[1], any = pack.Builds[2];
            var packs = new List<BuildPack> { pack };
            Action<string, List<BuildPack>, string, Build> auto = (what, ps, owned, want) =>
            {
                var got = Builds.AutoFor(ps, owned);
                Check("F02", "Auto, " + what, got == want, "owns " + (owned ?? "no tier-3 weapon") + " -> " + N(got));
            };
            auto("no tier-3 weapon owned yet: the pack default", packs, null, rockets);
            auto("the default's own branch owned", packs, "Rocket Launcher", rockets);
            auto("the other branch owned (the logged run): the lent build of that branch", packs, "Super Shotgun", pellets);
            auto("the branch spelt another way", packs, " super shotgun ", pellets);
            auto("a branch no lent build names: the lent build that decides its branch live", packs, "Minigun", any);
            var noLive = new BuildPack { Title = "Bench Pack", Default = rockets }; noLive.Builds.Add(rockets); noLive.Builds.Add(pellets);
            auto("a branch no lent build names, none decides live: plain Auto", new List<BuildPack> { noLive }, "Minigun", null);
            var liveDefault = new BuildPack { Title = "Bench Pack", Default = any }; liveDefault.Builds.AddRange(pack.Builds);
            auto("a default that decides its branch live stays whatever is owned", new List<BuildPack> { liveDefault }, "Super Shotgun", any);
            var noDefault = new BuildPack { Title = "Bench Pack" }; noDefault.Builds.AddRange(pack.Builds);
            auto("a pack without a default: plain Auto, even with a lent build for the branch", new List<BuildPack> { noDefault }, "Super Shotgun", null);
            var first = new BuildPack { Title = "First" }; var firstPellets = pellets.Clone(); firstPellets.Name = "First Pellets"; first.Builds.Add(firstPellets);
            auto("two packs: the default of the second, the branch build of the first registered", new List<BuildPack> { first, pack }, "Super Shotgun", firstPellets);
            auto("no pack at all", new List<BuildPack>(), "Super Shotgun", null);
            auto("no pack list", null, "Super Shotgun", null);

            // the whole way, through a provider as another mod registers one: the menu, a run, the [builds] line, a chosen build
            string dir = Path.Combine(Path.GetTempPath(), "yazs-bench-pack-" + Environment.ProcessId);
            var said = new List<string>();
            var oldSource = Builds.PackSource; var oldLogger = Builds.Logger;
            try
            {
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "tank.json"); File.WriteAllText(file, PackJson);
                Builds.PackSource = sv => string.Equals(sv, "Tank", StringComparison.OrdinalIgnoreCase) ? new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("bench", file) } : null;
                Builds.Logger = s => said.Add(s);
                Builds.ForgetPacks();
                Builds.Parse("{ \"selected\": { \"Tank\": \"auto\" } }");          // the selection without a save (no builds.json in the bench)
                Func<int> autoLines = () => said.Count(x => x.StartsWith("Tank on Auto", StringComparison.Ordinal));
                Func<string> lastLine = () => said.LastOrDefault(x => x.StartsWith("Tank on Auto", StringComparison.Ordinal)) ?? "(none)";

                var menu = Builds.For("Tank");
                Check("F02", "between runs (the menu, the Training Yard, the run setup screen): the pack default, as before", menu != null && menu.Name == "Rockets", N(menu));
                var b0 = Builds.For("Tank", null);
                Check("F02", "a run, no tier-3 weapon yet: the default, no [builds] line (the pack's own line says it)", b0 != null && b0.Name == "Rockets" && autoLines() == 0, N(b0));
                var b1 = Builds.For("Tank", "Super Shotgun");
                Check("F02", "a run, Super Shotgun taken: Auto follows the lent Super Shotgun build", b1 != null && b1.Name == "Pellets" && Builds.OnAuto("Tank") && autoLines() == 1, N(b1) + " | [builds] " + lastLine());
                int n = said.Count; for (int i = 0; i < 20; i++) Builds.For("Tank", "Super Shotgun");
                Check("F02", "asked twenty times more (a card asks dozens of times): no new line", said.Count == n);
                if (b0 != null && b1 != null)
                {
                    Check("F02", "the reasons name it as Auto's", Builds.Your("Tank", b1) == "Pellets (Auto)", "\"" + Builds.Your("Tank", b1) + "\" (was \"your Rockets build\")");
                    Check("F02", "the ability order follows the branch: Fury Unleashed #4 -> #1 (the focus), Minefield #2 -> #4",
                        b0.PriorityOf("Fury Unleashed") == 3 && b1.PriorityOf("Fury Unleashed") == 0 && b0.PriorityOf("Minefield") == 1 && b1.PriorityOf("Minefield") == 3);
                    Check("F02", "the evolution and the item leanings follow it", b1.EvolutionOf("Sawblade Drone") == "Sawblade Drone: Enchantment" && b1.Wants.Contains("critical"),
                        b0.EvolutionOf("Sawblade Drone") + " -> " + b1.EvolutionOf("Sawblade Drone") + ", wants " + string.Join("/", b0.Wants) + " -> " + string.Join("/", b1.Wants));
                }
                var b2 = Builds.For("Tank", "Minigun");
                Check("F02", "a run, Minigun taken: the lent build that decides its branch live", b2 != null && b2.Name == "Any Branch" && autoLines() == 2, N(b2) + " | [builds] " + lastLine());
                var b3 = Builds.For("Tank", null);
                Check("F02", "the next run, nothing owned again: back to the default, said once", b3 != null && b3.Name == "Rockets" && autoLines() == 3, N(b3) + " | [builds] " + lastLine());
                if (b0 != null)
                {
                    Builds.Parse("{ \"selected\": { \"Tank\": \"" + b0.Id + "\" } }");     // the player chooses the default build himself
                    var own = Builds.For("Tank", "Super Shotgun");
                    Check("F02", "a build the player chose stays whatever is owned, and is 'your' build", own != null && own.Name == "Rockets" && !Builds.OnAuto("Tank") && Builds.Your("Tank", own) == "your Rockets build" && autoLines() == 3,
                        N(own) + ", \"" + Builds.Your("Tank", own) + "\"");
                    Builds.Parse("{ \"selected\": { \"Tank\": \"auto\" } }");
                }
                Check("F02", "a survivor no pack lends for: plain Auto, nothing said", Builds.For("SWAT", "Sniper Rifle") == null && Builds.For("SWAT", null) == null && autoLines() == 3);
            }
            finally
            {
                Builds.PackSource = oldSource; Builds.Logger = oldLogger; Builds.ForgetPacks();
                Builds.Parse("{ \"selected\": { \"Tank\": \"auto\" } }");
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        // ---- F02: the words and scores of the cards that name the build (pure parts of Ranker)
        static void Wording()
        {
            Action<string, double, double, List<string>, string[]> card = (what, got, want, why, lines) =>
                Check("F02", what, Math.Abs(got - want) < 1e-9 && why.SequenceEqual(lines), got.ToString("0.00") + " - " + string.Join("; ", why));
            double syn = 0.3;       // the logged weapon cards' synergy part
            var w = new List<string>();
            card("Super Shotgun offered, Auto follows the lent Rockets: scored as Auto, says whose (0.12.2: 2.00 - your build takes Rocket Launcher)",
                Synergy.OtherBranch("Rocket Launcher", null, true, syn, false, "Rockets", "Pellets", w), 3.6 + syn, w,
                new[] { "other branch; Auto follows Rockets, which takes Rocket Launcher", "taking it locks Rocket Launcher out; Auto then follows Pellets" });
            w = new List<string>();
            card("the same, the Rocket Launcher still locked in the Training Yard", Synergy.OtherBranch("Rocket Launcher", null, false, syn, false, "Rockets", "Pellets", w), 6.2 + syn, w,
                new[] { "other branch; Auto follows Rockets, which takes Rocket Launcher", "taking it: Auto then follows Pellets" });
            w = new List<string>();
            card("the player chose Rockets: hold out for it, as in 0.12.2", Synergy.OtherBranch("Rocket Launcher", null, true, syn, true, null, null, w), 2.0, w,
                new[] { "other branch; your build takes Rocket Launcher" });
            w = new List<string>();
            card("plain Auto, as in 0.12.2", Synergy.OtherBranch("Rocket Launcher", "the guides' branch", true, syn, false, null, null, w), 3.6 + syn, w,
                new[] { "other branch; the squad favours Rocket Launcher (the guides' branch)", "taking it locks Rocket Launcher out" });
            w = new List<string>();
            card("no favoured branch known, as in 0.12.2", Synergy.OtherBranch(null, null, false, syn, false, null, null, w), 6.2 + syn, w, new[] { "the other branch" });

            string mine = "Sawblade Drone: Enchantment", other = "Sawblade Drone: Cogwheels";
            Func<bool, bool, bool, string> head = (isMine, offered, onAuto) => Synergy.EvolutionHead("Sawblade Drone", isMine ? mine : other, mine, "Pellets", isMine, offered, onAuto);
            Check("F02", "evolution headlines of a lent build on Auto name it", head(true, true, true) == "evolution of Sawblade Drone: Pellets (Auto) takes it"
                && head(false, true, true) == "evolution of Sawblade Drone; Pellets (Auto) takes " + mine && head(false, false, true) == "only " + other + " is offered - Pellets (Auto) prefers " + mine + "; still the biggest spike",
                head(true, true, true) + " | " + head(false, true, true) + " | " + head(false, false, true));
            Check("F02", "the player's own build: the 0.12.2 words", head(true, true, false) == "evolution of Sawblade Drone: your Pellets build's pick"
                && head(false, true, false) == "evolution of Sawblade Drone; your build takes " + mine && head(false, false, false) == "only " + other + " is offered - your build prefers " + mine + "; still the biggest spike");
        }

        // ---- F12: an item that both armors and heals says its survival clause once
        static void ItemClauses(List<ProbeItem> items)
        {
            var it = items == null ? null : items.FirstOrDefault(x => x.Name == "Frozen Heart");
            if (it == null) { Console.WriteLine("  (no Frozen Heart in the probe: the item clause cases skipped)"); return; }
            var k = Knowledge.FromJson(Knowledge.DefaultJson);
            Func<RunContext, List<string>> reasons = run =>
            {
                var c = new ItemContext
                {
                    Squad = new[] { "Tank", "Huntress", "SWAT" }, Tags = new TagProfile { SpecialAt = 10 }, K = k, Ctx = run,
                    OwnedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase), Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase), Stats = it.Stats, Healing = it.Healing
                };
                var why = new List<string>(); ItemRules.Evaluate(it.Name, it.Desc, c, why); return why;
            };
            // the logged offer: 12:27 into the run, 1:57 left, the squad hurting
            var hurt = reasons(new RunContext { Mode = "Normal", Goal = 1200, Seconds = 1083, LevelRate = 2.2, LevelUps = 43, Health = 0.4, D = new Doctrine() });
            string clause = "survival matters now (the squad is hurting)";
            Check("F12", "Frozen Heart late, the squad hurting: the survival clause once (it matches the armor and the health rule)",
                hurt.Count(x => x == clause) == 1 && hurt.Contains("Tank: armor"), string.Join("; ", hurt));
            var oneHit = reasons(new RunContext { Mode = "OneHit", Goal = 1200, Seconds = 600, LevelRate = 2.5, LevelUps = 24, D = new Doctrine() });
            Check("F12", "Frozen Heart in One Hit: 'health means nothing in One Hit' once", oneHit.Count(x => x == "health means nothing in One Hit") == 1, string.Join("; ", oneHit));
        }

        // ---- F12: the rescue card's headline takes the lines it sums up along
        static void RescueHeadlines()
        {
            Func<string, int, KeyValuePair<string, int>> p = (who, n) => new KeyValuePair<string, int>(who, n);
            Func<string, int, List<KeyValuePair<string, int>>, string, List<string>> card = (tier, bought, partners, late) =>
            {
                var why = new List<string>();
                if (tier != null) why.Add(Recruit.TierLine(tier));
                if (bought > 0) why.Add(Recruit.BoughtLine(bought, partners));
                why.Add("deals Kinetic like Bow");
                Recruit.Headline(why, tier, bought, partners, late);
                return why;
            };
            var one = card("A", 1, new List<KeyValuePair<string, int>> { p("Huntress", 1) }, null);
            Check("F12", "A-tier rescue with 1 bought synergy (the logged Tank card): each said once",
                one.SequenceEqual(new[] { "A-tier rescue, 1 bought synergy with Huntress", "deals Kinetic like Bow" }), string.Join("; ", one)
                + " (was: A-tier rescue, 1 bought synergy with the squad; A-tier rescue in the guides; 1 bought synergy: 1 with Huntress; deals Kinetic like Bow)");
            var two = card("S", 3, new List<KeyValuePair<string, int>> { p("Huntress", 2), p("SWAT", 1) }, null);
            Check("F12", "S-tier rescue, synergies with two of the squad", two.SequenceEqual(new[] { "S-tier rescue, 3 bought synergies with Huntress, SWAT", "deals Kinetic like Bow" }), string.Join("; ", two));
            var late = card("A", 1, new List<KeyValuePair<string, int>> { p("Huntress", 1) }, "little time left for a recruit to grow (1:30 left)");
            Check("F12", "late in the run: that headline, the tier and bought lines stay (it names neither)",
                late.SequenceEqual(new[] { "little time left for a recruit to grow (1:30 left)", "A-tier rescue in the guides", "1 bought synergy: 1 with Huntress", "deals Kinetic like Bow" }), string.Join("; ", late));
            var noTier = card(null, 1, new List<KeyValuePair<string, int>> { p("Huntress", 1) }, null);
            Check("F12", "no tier in the guides: no headline, the bought line stays", noTier.SequenceEqual(new[] { "1 bought synergy: 1 with Huntress", "deals Kinetic like Bow" }), string.Join("; ", noTier));
            var noneBought = card("B", 0, new List<KeyValuePair<string, int>>(), null);
            Check("F12", "nothing bought: no headline, the tier line stays", noneBought.SequenceEqual(new[] { "B-tier rescue in the guides", "deals Kinetic like Bow" }), string.Join("; ", noneBought));
        }
    }
}
