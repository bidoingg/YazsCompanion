// 0.14.0: the integration review of the round's five lanes (section 14 of the bench) - what the reviewers found, replayed:
//   P1  the WHY band on a card a quest rule decided: the lifted weapon of the 10-05 'Heroic Theory' run no longer says "the
//       build levels abilities first" beside "Quest: ...", and a card the quest makes AVOID keeps its merits to itself.
//   P7  the "vs #1" sentence names what the first card has that this one lacks - never its place in a build this card stands
//       higher in (a core ability of the build over the build's main ability read as if the order were wrong).
//   P8  two cards of one offer never draw the same line (10-05 19:12: Electric Turret and Energy Shield both "Max level - turns
//       on the Electric 10-tag effect"; a chest of three "Fits the ... build (healing)"); a line a quest decided stays on each.
//   P11 an evolution's short name is taken of the name another mod lends, not of the game's.
//   P12 the rewordings: a weapon level under "abilities first", a type short of its 10-tag effect, the quest's item, a hurting
//       squad, an unread squad on a Research Pod, a guide note after its tier; the QUEST row counts as the cards do.
//   R3  SELECT LOADOUT: a survival weight under 1 says survival picks count LESS (it said "always help").
//   S   the sources: the DISPLAY tab's WHY row, the cfg's migration of an old RerollHint = false, the 0.14.0 release notes (the
//       README's status block up to 0.15.0, the CHANGELOG's 0.14.0 entry since).
//   0.16.0 (a section of its own after these): the test walks - the pause walk's recruit (C16-11a: [Debug] PreviewPauseRecruit, the
//       key's parse, OnApply(false) only, one recruit a session to a leader alone; V7 / V8: the walk's first level-up through the
//       game's ExperienceProgress.Debug_LevelUp() at 20 s with the recruit, 36 s without, under the 41 s bound; V9: every debug walk
//       logs its [Debug] Perf sums just before its done line) and the results flow's stats-step captures
//       (C16-11b: UIDefeatState2.OnEnable + Setup, UIDefeat.OnState1Continue as the fallback, never UIDefeatState2's Awake).
// Generic names only (the repository is public): the game's card names, the bench's own build names and invented lent names.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class ReviewCases
    {
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string Src(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", file));
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }

        public static int Run()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.14.0: the integration review - the WHY band under a quest, the vs sentence, equal lines told apart, lent short names, rewordings, the loadout's survival words, sources");
            QuestBand();
            Versus();
            Distinct();
            LentShort();
            Rewordings();
            Survival();
            Sources();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            int bad = _bad;
            bad += Walks016();                  // 0.16.0 C16-11a / C16-11b: a section of its own (reached from Checks.Run and Verdict.DataFree)
            return bad;
        }

        const string Anchor = "Shield Anchor", A = "Bench Anchor";

        // ---------------------------------------------------------------- P1
        static void QuestBand()
        {
            // 00:29 of 'Heroic Theory': the Taser lifted by the quest over the Ability-style build's abilities (QuestCases Q1)
            var taser = new CardWords { Kind = SayKind.Weapon, Level = 1, Max = 4, Style = BuildStyle.Ability, Build = Anchor, Reach = 1, ShareType = "Electric", SharePct = 100, Quest = "quest: leads to the tier-3 weapon (Level 2 of 4)" };
            string shown = Wording.Card(taser, 1, "Taser", "Energy Shield");
            var b = WhyText.Block(new WhyIn { Name = "Taser", Rank = 1, Score = 6.95, Shown = shown, Say = taser, Why = new List<string> { "quest: leads to the tier-3 weapon (Level 2 of 4)", "style: abilities first" },
                SecondName = "Energy Shield", SecondScore = 5.23 });
            Console.WriteLine("    00:29 Taser under the card '" + shown + "' -> " + b.Lead + "  " + string.Join("  /  ", b.Items));
            Check("P1", "the quest-lifted Taser: 'Quest: ...' on the card, the band says the quest comes before the build's order - never 'levels abilities first'",
                shown == "Quest: leads to the tier-3 weapon (Level 2 of 4)" && !b.Items.Any(i => i.Contains("abilities first")) && b.Reasons.Contains("The quest comes before the build's order") && b.Reasons.Contains("Electric is 100% of your damage"),
                string.Join(" | ", b.Items));
            // Pyro_4: the build's main ability, AVOID under a quest that forbids the class's abilities
            var molotov = new CardWords { Kind = SayKind.Ability, Level = 0, Max = 4, Build = A, Priority = 0, HeadRank = 4, Head = Wording.Role(A, 0), EvoExists = true, Reach = 1, Owned = 1, ShareType = "Fire", SharePct = 80, Quest = "quest: any Pyro ability fails it" };
            string ms = Wording.Card(molotov, 3, "Minefield", "Fireaxe");
            var mb = WhyText.Block(new WhyIn { Name = "Molotov Cocktail", Rank = 3, Score = 0.40, Shown = ms, Say = molotov, Why = new List<string> { "quest: any Pyro ability fails it", "A-tier ability in the guides" },
                FirstName = "Minefield", FirstScore = 4.1, FirstShown = "Fills an empty ability slot early", SecondName = "Fireaxe", SecondScore = 3.5 });
            Console.WriteLine("    Pyro_4 Molotov Cocktail under the card '" + ms + "' -> " + mb.Lead + "  " + string.Join("  /  ", mb.Items));
            Check("P1", "a card the quest makes AVOID: its merits left out (no 'main ability', no 'Fire is 80%', no guide tier), the quest rules it out",
                ms == "Quest: any Pyro ability fails it" && mb.Reasons.Count == 1 && mb.Reasons[0] == "Whatever else it has, the quest rules it out" && !mb.Items.Any(i => i.Contains("main ability") || i.Contains("80%") || i.Contains("tier")),
                string.Join(" | ", mb.Items));
            // Ranger_RB: an A-tier item the build wants, AVOID under a quest that wants no item at all
            var clip = new ItemSay { QuestLine = "quest: taking any item fails it", Tier = "A", Want = "weapons", WantBuild = A };
            var cw = new CardWords { Kind = SayKind.Item, Item = clip };
            string cs = Wording.Card(cw, 1, null, null);
            var cb = WhyText.Block(new WhyIn { Name = "Magazine Clip", Rank = 1, Score = 0.40, Shown = cs, Say = cw, Why = new List<string> { "quest: taking any item fails it", "A-tier item" }, SecondName = "Potato", SecondScore = 0.40 });
            Check("P1", "an item the quest makes AVOID: no 'A-tier, fits the build' under the quest's line", cs == "Quest: taking any item fails it" && !cb.Items.Any(i => i.Contains("tier") || i.Contains("fits")) && cb.Reasons.Contains("Whatever else it has, the quest rules it out"),
                string.Join(" | ", cb.Items));
        }

        // ---------------------------------------------------------------- P7
        static void Versus()
        {
            var drone = new CardWords { Kind = SayKind.Ability, Level = 0, Max = 4, Build = A, Priority = 1, HeadRank = 4, Head = Wording.Role(A, 1), EvoExists = true, Reach = 1, Owned = 0, ShareType = "Kinetic", SharePct = 100 };
            var exp = new CardWords { Kind = SayKind.Ability, Level = 0, Max = 4, Build = A, Priority = 0, HeadRank = 4, Head = Wording.Role(A, 0), EvoExists = true, Reach = 1, Owned = 0 };
            string sDrone = Wording.Card(drone, 1, "Medical Drone", "Experiment 21"), sExp = Wording.Card(exp, 2, "Medical Drone", "Experiment 21");
            // not a close call: 4.95 against 4.50
            var b = WhyText.Block(new WhyIn { Name = "Experiment 21", Rank = 2, Score = 4.50, Shown = sExp, Say = exp, Why = new List<string>(),
                FirstName = "Medical Drone", FirstScore = 4.95, FirstShown = sDrone, FirstSay = drone, FirstWhy = new List<string>(), SecondName = "Experiment 21", SecondScore = 4.50 });
            Check("P7", "the build's main ability under a core ability of the build: the vs sentence names what the first card has that it lacks, not the weaker place in the build",
                b.Versus == "Medical Drone goes first: Kinetic is 100% of your damage" && !b.Versus.Contains("core ability"), "'" + b.Versus + "'");
            var gun = new CardWords { Kind = SayKind.Weapon, Level = 1, Max = 4, Style = BuildStyle.Ability, Build = A, Reach = 1, ShareType = "Kinetic", SharePct = 100 };
            var g = WhyText.Block(new WhyIn { Name = "Handgun", Rank = 3, Score = 3.72, Shown = Wording.Card(gun, 3, "Medical Drone", "Experiment 21"), Say = gun, Why = new List<string>(),
                FirstName = "Medical Drone", FirstScore = 4.95, FirstShown = sDrone, FirstSay = drone, FirstWhy = new List<string>(), SecondName = "Experiment 21", SecondScore = 4.50 });
            Check("P7", "a card with no place in the build: the first card's own line, as before", g.Versus == "Medical Drone goes first: " + sDrone, "'" + g.Versus + "'");
        }

        // ---------------------------------------------------------------- P8
        static void Distinct()
        {
            // 19:12:19: Electric Turret (#1) and Energy Shield (2ND), both at 3 of 4 with their evolutions unlocked, both carrying Electric to 10
            var turret = new CardWords { Kind = SayKind.Ability, Level = 3, Max = 4, Build = Anchor, Priority = 1, HeadRank = 5, Head = Wording.Special("Electric"), EvoExists = true, EvoOwned = true, Reach = 1, Special = "Electric" };
            var shield = new CardWords { Kind = SayKind.Ability, Level = 3, Max = 4, Build = Anchor, Priority = 0, HeadRank = 5, Head = Wording.Special("Electric"), EvoExists = true, EvoOwned = true, Reach = 1, Special = "Electric" };
            var says = new List<CardWords> { turret, shield };
            var lines = says.Select((w, i) => Wording.Card(w, i + 1, "Electric Turret", "Energy Shield")).ToList();
            string before = string.Join(" | ", lines);
            WhyText.Distinct(lines, says, new List<IList<string>> { new List<string>(), new List<string>() }, new List<int> { 50, 50 }, null);
            Check("P8", "19:12:19: the two 'Max level - turns on the Electric 10-tag effect' told apart - the first keeps it, the second says its next reason",
                before == "Max level - turns on the Electric 10-tag effect | Max level - turns on the Electric 10-tag effect" && lines[0] == "Max level - turns on the Electric 10-tag effect" && lines[1] != lines[0] && lines[1].StartsWith("Max level - ") && lines[1].Length <= 50,
                before + " -> " + string.Join(" | ", lines));
            // 18:15:41: a chest of three healing items the lent build wants
            Func<string, string, ItemSay> heal = (tier, survival) => { var s = new ItemSay { Want = "healing", WantBuild = A, Tier = tier, Survival = survival }; return s; };
            var items = new List<CardWords> { new CardWords { Kind = SayKind.Item, Item = heal(null, "now") }, new CardWords { Kind = SayKind.Item, Item = heal("B", "now") }, new CardWords { Kind = SayKind.Item, Item = heal(null, "now") } };
            items[2].Item.Boosts.Add(new ItemFit { What = "armor", Weight = 0.4 });
            var il = items.Select(w => Wording.Card(w, 1, null, null)).ToList();
            string ib = string.Join(" | ", il);
            WhyText.Distinct(il, items, new List<IList<string>> { null, null, null }, new List<int> { 50, 50, 50 }, null);
            Check("P8", "18:15:41: three 'Fits the Bench Anchor build (healing)' - three different lines, the first unchanged", il.Distinct().Count() == 3 && il[0] == "Fits the Bench Anchor build (healing)",
                ib + " -> " + string.Join(" | ", il));
            // B2 'Trauma': three health items under the quest - each one counts, each one says so
            var q = Enumerable.Range(0, 3).Select(_ => new CardWords { Kind = SayKind.Item, Item = new ItemSay { QuestLine = "quest: hold a health item (0 of 1)" } }).ToList();
            var ql = q.Select(w => Wording.Card(w, 1, null, null)).ToList();
            WhyText.Distinct(ql, q, new List<IList<string>> { null, null, null }, new List<int> { 50, 50, 50 }, null);
            Check("P8", "a line a quest decided stays on every card it decided (each health item counts for 'Trauma')", ql.All(l => l == "Quest: hold a health item (0 of 1)"), string.Join(" | ", ql));
        }

        // ---------------------------------------------------------------- P11
        static void LentShort()
        {
            Func<string, string> lend = s => s == null ? null : s.Replace("Bombing Strike: Bioweapon", "Sky Fall: Venom Rain").Replace("Bombing Strike", "Sky Fall");
            var w = new CardWords { Kind = SayKind.Evolution, Base = "Bombing Strike", Build = "Bench Rocket", Pick = "Bombing Strike: Bioweapon", Mine = false, PickOffered = true };
            string line = Wording.Card(w, 2, null, null, 50, lend), plain = Wording.Card(w, 2, null, null);
            Check("P11", "an evolution's short name of the lent name: 'takes Venom Rain instead', not the game's 'Bioweapon' nobody sees on screen",
                line.Contains("Venom Rain") && !line.Contains("Bioweapon") && plain.Contains("Bioweapon"), "'" + line + "' (the game's names: '" + plain + "')");
            var v = WhyText.Block(new WhyIn { Name = "Bombing Strike: Supercharge", Rank = 2, Score = 4.0, Shown = "x", FirstName = "Bombing Strike: Bioweapon", FirstScore = 5.0, FirstShown = "", Lend = lend });
            Check("P11", "the WHY band names the first card by its lent short name", v.Versus == "Venom Rain goes first", "'" + v.Versus + "'");
        }

        // ---------------------------------------------------------------- P12
        static void Rewordings()
        {
            var pairs = new List<Tuple<string, string, string>>
            {
                Tuple.Create("a weapon level under the build's 'abilities first'", Wording.Card(new CardWords { Kind = SayKind.Weapon, Level = 1, Max = 4, Style = BuildStyle.Ability, Build = A, Reach = 1 }, 3, "Medical Drone", "Experiment 21"), "Level 2 of 4 - this build levels abilities first"),
                Tuple.Create("the same in your own style", Wording.Card(new CardWords { Kind = SayKind.Weapon, Level = 1, Max = 4, Style = BuildStyle.Ability, Reach = 1 }, 3, "Medical Drone", "Experiment 21"), "Level 2 of 4 - your style levels abilities first"),
                Tuple.Create("a level that turns a long type's 10-tag effect on", Wording.Card(new CardWords { Kind = SayKind.Weapon, Level = 1, Max = 4, Special = "Explosive" }, 1, null, null), "Level 2 of 4 - Explosive 10-tag effect turns on"),
                Tuple.Create("an evolution a type short", Wording.Card(new CardWords { Kind = SayKind.Evolution, Base = "x", ShortType = "Fire", Short = 3 }, 1, null, null), "Evolution - Fire is 3 tags from its 10-tag effect"),
                Tuple.Create("the quest's own item", Wording.Item(new ItemSay { Quest = true }), "Quest: the quest asks for this item"),
                Tuple.Create("a hurting squad", Wording.Item(new ItemSay { Survival = "hurting" }), "Keeps the squad alive - you are low on health"),
                Tuple.Create("a Research Pod with the squad not read", Wording.Tag("Fire", 2, null), "+2 Fire tags - no damage data yet this run"),
                Tuple.Create("a guide note after its tier", Wording.Item(new ItemSay { Tier = "S", Note = "boosts short-range weapons" }), "S-tier - boosts short-range weapons"),
            };
            var off = pairs.Where(p => p.Item2 != p.Item3).Select(p => p.Item1 + ": '" + p.Item2 + "' (wanted '" + p.Item3 + "')").ToList();
            Check("P12", "the rewordings (" + pairs.Count + ")", off.Count == 0, off.Count > 0 ? string.Join(" | ", off) : string.Join(" | ", pairs.Select(p => p.Item2)));
            var h = new QuestRule { Ask = QuestAsk.HealthItems, Need = 1, Have = 0 };
            var t = new QuestRule { Ask = QuestAsk.TypesAt10, Need = 4, Have = 1 };
            var e = new QuestRule { Ask = QuestAsk.Evolutions, Need = 6, Have = 2 };
            var r = new QuestRule { Ask = QuestAsk.RareTraining, Need = 3, Have = 1 };
            string rows = string.Join(" | ", new[] { h, t, e, r }.Select(QuestRules.RowText));
            Check("P12", "the QUEST row counts as the cards do: '(1 of 4)', never '(0/1)' or a bare '(1)'", rows == "a health item (0 of 1) | types at 10 tags (1 of 4) | evolutions (2 of 6) | Rare+ trainings (1 of 3)", rows);
            string rules = Src("YazsCompanion.Mod/ItemRules.cs"), view = Src("YazsCompanion.Mod/LoadoutView.cs"), know = Src("YazsCompanion.Mod/Knowledge.cs");
            Check("P12", "'half your weapons', the loadout's 'the #3 badge scores 4.2' / '0.3 score behind', the guide notes reworded",
                rules != null && rules.Contains("case 50: return \"half\";") && view != null && view.Contains("\" badge scores \"") && view.Contains("+ \" score\"; }")
                && know != null && know.Contains("\"\"Silencer\"\": \"\"boosts short-range weapons\"\"") && know.Contains("\"\"Wooden Stick\"\": \"\"XP item, take it early\"\""));
        }

        // ---------------------------------------------------------------- R3
        static void Survival()
        {
            var k = Knowledge.FromJson(Knowledge.DefaultJson);
            var b = new BadgeFacts { Id = 990001, Asset = "BenchSurvival", Short = "BenchSurvival", Name = "Bench Survival" };
            var terms = new List<BadgeTerm> { new BadgeTerm { Kind = "survival", Points = 1.2, Text = "survival" } };
            Func<double, string, string> why = (sv, mode) => Loadout.Why(b, 1, terms, new RunShape(), new LoadoutCtx { Survival = sv, Mode = mode, Difficulty = 2 }, k, false);
            string less = why(0.6, "Normal"), help = why(1.0, "Normal"), more = why(1.31, "Hardcore");
            Check("R3", "a survival weight under 1 (Caution Low, a short horizon) says LESS; about 1 'always help'; over 1.10 'N% more'",
                less == "survival picks count 40% less" && help == "survival picks always help" && more == "Hardcore: survival picks count 31% more", less + " | " + help + " | " + more);
        }

        // ---------------------------------------------------------------- S
        static void Sources()
        {
            string menu = Src("YazsCompanion.Mod/Menu.cs"), plugin = Src("YazsCompanion.Mod/Plugin.cs"), readme = Src("README.md");
            Check("S", "the DISPLAY tab switches the WHY band (di:why, in the tab's scroller) and the Card verdicts help says it hides it too",
                menu != null && menu.Contains("\"di:why\"") && menu.Contains("Plugin.ShowWhy.Value = !Plugin.ShowWhy.Value") && menu.Contains("Scroll(\"display\", x, top, w, room, false, 40f") && menu.Contains("Off also hides the WHY band"));
            Check("S", "an older cfg with RerollHint = false and no ActionHints line: the new hints start off (read before the bind)",
                plugin != null && plugin.IndexOf("bool hadHints", StringComparison.Ordinal) > 0 && plugin.IndexOf("bool hadHints", StringComparison.Ordinal) < plugin.IndexOf("Config.Bind(\"Advice\", \"ActionHints\"", StringComparison.Ordinal)
                && plugin.Contains("if (!hadHints && !AdviceRerollHint.Value)") && plugin.Contains("AdviceActionHints.Value = HintActions.None;"));
            // 0.15.0 (C15-10): the release history moved from the README's status blocks to CHANGELOG.md (DocCases.cs checks the split)
            string log = Src("CHANGELOG.md");
            int status = log == null ? -1 : log.IndexOf("\n## 0.14.0 ", StringComparison.Ordinal), next = log == null || status < 0 ? -1 : log.IndexOf("\n## ", status + 1, StringComparison.Ordinal);
            string block = status >= 0 ? (next > status ? log.Substring(status, next - status) : log.Substring(status)) : "";
            Check("S", "the CHANGELOG's 0.14.0 entry names the wide menus (on by default), the chest floor 2.5, AVOID in place of the place",
                block.Contains("WideMenus") && block.Contains("2.5") && block.Contains("AVOID   ") && readme != null && !readme.Contains("**0.14.0**"));
        }

        // ================================================================ 0.16.0: the test walks (C16-11a / C16-11b)
        static int Walks016()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.16.0: the test walks - the pause walk recruits one hero (C16-11a), the results flow's stats step is captured (C16-11b)");
            Recruit();
            ResultsShots();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        /// <summary>The text of the first member whose head contains <paramref name="head"/>: from the head to the next line that opens a
        /// member at the same indent ('        static ' / '        public static ' / '        internal static '), or to the end.</summary>
        static string Body(string src, string head)
        {
            if (src == null) return "";
            int i = src.IndexOf(head, StringComparison.Ordinal);
            if (i < 0) return "";
            var next = new Regex(@"\n        (?:public |internal |private )?static ");
            var m = next.Match(src, i + head.Length);
            return m.Success ? src.Substring(i, m.Index - i) : src.Substring(i);
        }

        /// <summary>The text of a patch class: from 'static class NAME' to the next [HarmonyPatch], or to the end.</summary>
        static string PatchClass(string src, string name)
        {
            if (src == null) return "";
            int i = src.IndexOf("static class " + name + "\n", StringComparison.Ordinal);
            if (i < 0) return "";
            int j = src.IndexOf("[HarmonyPatch", i, StringComparison.Ordinal);
            return j > i ? src.Substring(i, j - i) : src.Substring(i);
        }

        /// <summary>Code without its line comments (no '//' inside a string literal is expected in the files read here).</summary>
        static string Code(string src)
        {
            if (src == null) return "";
            return string.Join("\n", src.Replace("\r\n", "\n").Split('\n').Select(l => { int c = l.IndexOf("//", StringComparison.Ordinal); return c >= 0 ? l.Substring(0, c) : l; }));
        }

        // ---------------------------------------------------------------- C16-11a: [Debug] PreviewPauseRecruit
        static void Recruit()
        {
            string plugin = Src("YazsCompanion.Mod/Plugin.cs"), menu = Src("YazsCompanion.Mod/Menu.cs"), walk = Src("YazsCompanion.Mod/Menu.Walk016.cs");
            if (plugin == null || menu == null || walk == null) { Check("V1", "the sources (Plugin.cs, Menu.cs, Menu.Walk016.cs)", false, "not found next to the bench"); return; }
            plugin = plugin.Replace("\r\n", "\n"); menu = menu.Replace("\r\n", "\n"); walk = walk.Replace("\r\n", "\n");

            // V1 the key: empty by default, its description for scripted test runs
            const string bind = "Config.Bind(\"Debug\", \"PreviewPauseRecruit\", \"\", \"";
            int b = plugin.IndexOf(bind, StringComparison.Ordinal);
            string line = b < 0 ? "" : plugin.Substring(b, Math.Max(0, plugin.IndexOf('\n', b) - b));
            Check("V1", "[Debug] PreviewPauseRecruit defaults to \"\" and its description says it is for scripted test runs",
                b >= 0 && line.Contains("For scripted test runs with PreviewPause only") && line.Contains("Ghost or Ninja for the Ghost"), b < 0 ? "no such bind in Plugin.cs" : null);

            // V2 OnApply(false), never OnApply(true) - nor any other argument
            string wcode = Code(walk);
            int applies = Regex.Matches(wcode, @"\.\s*OnApply\s*\(\s*false\s*\)").Count, others = Regex.Matches(wcode, @"\.\s*OnApply\s*\((?!\s*false\s*\))").Count;
            Check("V2", "the recruit is applied with OnApply(false) - the rescue's bonuses, health refill and achievement check left out; never OnApply(true)",
                applies == 1 && others == 0, applies + " OnApply(false), " + others + " other OnApply call(s)");

            // V3 only under PreviewPause, only to a leader alone: the walk's tick returns without PreviewPause before its call site; the
            // recruit itself tests PreviewPause and the squad of one before the apply
            string tick = Code(Body(menu, "static void PausePreviewTick()")), rec = Code(Body(walk, "static void DebugRecruit(float t)"));
            int gate = tick.IndexOf("if (_ppDone || !on) return;", StringComparison.Ordinal), call = tick.IndexOf("DebugRecruit(t);", StringComparison.Ordinal);
            int walkGate = rec.IndexOf("if (!walk) return;", StringComparison.Ordinal), one = rec.IndexOf("if (n != 1)", StringComparison.Ordinal), apply = rec.IndexOf(".OnApply(false)", StringComparison.Ordinal);
            Check("V3", "the recruit acts only inside the PreviewPause walk and only while the squad is its leader alone (gamePlayers.Count == 1: one recruit, never the second that unlocks an achievement)",
                gate >= 0 && call > gate && rec.Contains("Plugin.PreviewPause.Value") && walkGate >= 0 && rec.Contains("gamePlayers.Count") && one > walkGate && apply > one,
                "tick gate " + (gate >= 0) + ", call after it " + (call > gate) + ", PreviewPause tested " + (walkGate >= 0) + ", squad of one before the apply " + (one > walkGate && apply > one));

            // V4 once a session: the call site sets _ppRecruitDone, and nothing clears it
            string mcode = Code(menu);
            int calls = Regex.Matches(mcode, @"\bDebugRecruit\s*\(\s*t\s*\)").Count, clears = Regex.Matches(mcode + "\n" + wcode, @"_ppRecruitDone\s*=\s*false").Count;
            Check("V4", "once a session: '!_ppRecruitDone && PpRecruitKeySet()) { _ppRecruitDone = true; DebugRecruit(t); }' is the one call, and nothing sets _ppRecruitDone back",
                mcode.Contains("!_ppRecruitDone && PpRecruitKeySet()) { _ppRecruitDone = true; DebugRecruit(t); }") && calls == 1 && clears == 0, calls + " call(s), " + clears + " reset(s)");

            // V5 the parse: Ghost and Ninja both the Ghost (CharacterType Ninja), Auto, the nine classes, nothing else
            var inputs = new[] { "auto", "Ghost", "ninja", "SWAT", "Bench", "" };
            var t = typeof(ReviewCases).Assembly.GetType("YazsCompanion.PauseRecruit");
            if (t != null)
            {   // the helper compiled into the bench (Menu.Walk016.cs linked with YAZS_BENCH defined): called
                const BindingFlags F = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                MethodInfo parse = t.GetMethod("Parse", F), pick = t.GetMethod("AutoPick", F), shown = t.GetMethod("Shown", F);
                Func<MethodInfo, string, string> call1 = (m, v) => m == null ? "?" : (string)m.Invoke(null, new object[] { v });
                Func<string, string> P = v => call1(parse, v);
                string got = string.Join(" | ", inputs.Select(v => "'" + v + "' -> " + (P(v) == null ? "no class" : P(v).Length == 0 ? "off" : P(v))));
                bool ok = P("auto") == "Auto" && P("Ghost") == "Ninja" && P("ninja") == "Ninja" && P("SWAT") == "SWAT" && P("Bench") == null && P("") == ""
                    && P("  Medic ") == "Medic" && P("4") == null && P("None") == null && P("NumCharacters") == null
                    && call1(pick, "Medic") == "Tank" && call1(pick, "Tank") == "Medic" && call1(pick, "Ninja") == "Medic" && call1(shown, "Ninja") == "Ghost" && call1(shown, "SWAT") == "SWAT";
                Check("V5", "the key's parse (PauseRecruit.Parse, called): 'Ghost' and 'Ninja' both the Ghost (CharacterType Ninja), 'auto' Auto, '' off, 'Bench' no class; Auto skips the leader; a log line says Ghost",
                    ok, got + "; Auto under a Medic leader -> " + call1(pick, "Medic") + ", under a Tank -> " + call1(pick, "Tank"));
            }
            else
            {   // the bench does not compile Menu.Walk016.cs (yet): the same facts read from the helper's source
                string parseBody = Code(Body(walk, "internal static string Parse(string raw)"));
                bool table = walk.Contains("internal static readonly string[] Classes = { \"Medic\", \"Tank\", \"Pyro\", \"Engineer\", \"Huntress\", \"SWAT\", \"Mechanic\", \"Ranger\", \"Ninja\" };");
                bool ghost = parseBody.Contains("if (string.Equals(v, \"Ghost\", StringComparison.OrdinalIgnoreCase)) return \"Ninja\";");
                bool auto = parseBody.Contains("if (string.Equals(v, Auto, StringComparison.OrdinalIgnoreCase)) return Auto;") && walk.Contains("internal const string Off = \"\", Auto = \"Auto\";");
                bool nine = parseBody.Contains("foreach (var c in Classes) if (string.Equals(v, c, StringComparison.OrdinalIgnoreCase)) return c;") && parseBody.Contains("return null;");
                bool off = parseBody.Contains("if (v.Length == 0) return Off;") && parseBody.Contains("raw.Trim()");
                int split = walk.IndexOf("#if !YAZS_BENCH\n    internal static partial class Menu", StringComparison.Ordinal);
                string pureText = split < 0 ? "" : Code(walk.Substring(0, split)).Replace("#if !YAZS_BENCH\nusing Il2CppInterop.Runtime;\nusing UnityEngine;\nusing CT = GamePlayer.CharacterType;\n#endif", "");
                bool pure = split > 0 && !Regex.IsMatch(pureText, @"\bUnityEngine\b|\bIl2Cpp|\bGamePlayer\b|\bCT\.");
                Check("V5", "the key's parse (PauseRecruit.Parse, read from its source - Menu.Walk016.cs is not compiled into the bench): " + string.Join(", ", inputs.Select(v => "'" + v + "'")) + " -> Auto, Ninja, Ninja, SWAT, no class, off; the helper free of game types",
                    table && ghost && auto && nine && off && pure, "the nine in Auto's order " + table + ", Ghost -> Ninja " + ghost + ", Auto " + auto + ", only the nine " + nine + ", '' off " + off + ", no game type " + pure);
            }

            // V6 the log lines name the Ghost by G.ClassName (never 'Ninja'), and the three failures and the success line are the spec's
            Check("V6", "the recruit's lines: 'joined at ... - squad 2: the level-ups offer four cards', the three 'recruit not applied' reasons, a class named the game's way (G.ClassName)",
                rec.Contains("G.ClassName(cls)") && walk.Contains("\" joined at \"") && walk.Contains(") - squad 2: the level-ups offer four cards\"")
                && walk.Contains("\"[menu] pause walk: recruit not applied - no \" + asset + \" in the game's powerup lists\"") && walk.Contains("\"[menu] pause walk: recruit not applied - OnApply(false) threw \"")
                && walk.Contains("\"[menu] pause walk: recruit not applied - squad still 1 a second later\"") && walk.Contains("\" is no class - off\"")
                && Code(Body(walk, "static void DebugRecruitStart()")).Contains("PauseRecruit.Parse(raw)") && rec.Contains("_ppRecruitCheckAt = Time.realtimeSinceStartup + 1f;"));

            // V7 (series r1 fix round, FAILURE 2): the first level-up's timing - 20 s with the recruit (its standing squad gathered 0 of 100
            // XP by 0:42 in both r1 walks), 36 s without (fix_a review: in stage 1, before the 40 s pause - at 46 s the walk's end came past
            // 50 s of play), never at or past the 41 s bound (50 s less ~3 s of play to the done line, less the series' ~6 s to its kill),
            // once, never with a card taken or a screen up
            var pr = typeof(ReviewCases).Assembly.GetType("YazsCompanion.PauseRecruit");
            MethodInfo at = pr == null ? null : pr.GetMethod("LevelUpAt", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo due = pr == null ? null : pr.GetMethod("LevelUpDue", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo bound = pr == null ? null : pr.GetField("PlayBound", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (at != null && due != null && bound != null)
            {
                Func<bool, int, float, bool, bool, bool> D = (r, picks, tt, asked, paused) => (bool)due.Invoke(null, new object[] { r, picks, tt, asked, paused });
                float atR = (float)at.Invoke(null, new object[] { true }), atA = (float)at.Invoke(null, new object[] { false }), b48 = (float)bound.GetValue(null);
                var rows = new[]
                {
                    Tuple.Create("recruit 19.9 s", D(true, 0, 19.9f, false, false), false), Tuple.Create("recruit 20 s", D(true, 0, 20f, false, false), true),
                    Tuple.Create("recruit 25 s, a card taken", D(true, 1, 25f, false, false), false), Tuple.Create("recruit 25 s, asked before", D(true, 0, 25f, true, false), false),
                    Tuple.Create("recruit 25 s, a screen up", D(true, 0, 25f, false, true), false), Tuple.Create("recruit 40.9 s", D(true, 0, 40.9f, false, false), true),
                    Tuple.Create("recruit 41 s", D(true, 0, 41f, false, false), false), Tuple.Create("alone 35.9 s", D(false, 0, 35.9f, false, false), false),
                    Tuple.Create("alone 36 s", D(false, 0, 36f, false, false), true), Tuple.Create("alone 39.9 s", D(false, 0, 39.9f, false, false), true),
                    Tuple.Create("alone 41 s", D(false, 0, 41f, false, false), false), Tuple.Create("alone 46 s (the old grant time)", D(false, 0, 46f, false, false), false),
                };
                var wrong = rows.Where(r => r.Item2 != r.Item3).Select(r => r.Item1 + " -> " + r.Item2).ToList();
                // the walk's two waits read the bound: stage 1 pauses at 40 s without a card (the grant must come before it), stage 61 waits under PlayBound
                bool waits = tick.Contains("bool more = t < 30f || (Advisor.DebugPicks == 0 && t < 40f);") && tick.Contains("t < PauseRecruit.PlayBound && now - _ppResumeAt < 20f")
                    && !Regex.IsMatch(Code(tick), @"\bt\s*<\s*4[2-9]f");
                Check("V7", "the first level-up's time (PauseRecruit.LevelUpAt / LevelUpDue, called): 20 s with the recruit, 36 s without (before stage 1's 40 s pause), under the 41 s bound (50 s less 3 s to the done line less the series' 6 s to its kill), once a session, never with a card taken or a screen up; stage 61 waits under the same bound",
                    atR == 20f && atA == 36f && atA + 1.5f < 40f && b48 == 41f && wrong.Count == 0 && waits, "at " + atR + " / " + atA + " s, bound " + b48 + " s, the walk's waits " + waits + (wrong.Count == 0 ? "; " + rows.Length + " rows as wanted" : "; wrong: " + string.Join(", ", wrong)));
            }
            else Check("V7", "the first level-up's time (PauseRecruit.LevelUpAt / LevelUpDue / PlayBound)", false, "not in the bench's PauseRecruit (Menu.Walk016.cs not linked?)");

            // V8 the grant is the game's own developer level-up, from both waiting stages of the walk, once a session: ExperienceProgress.
            // Debug_LevelUp() (LevelUp() direct only as its fallback), never XP through CollectXP, never another OnApply (V2 counts them)
            string lu = Code(Body(walk, "static void DebugFirstLevelUp(float t, float now)")), luChk = Code(Body(walk, "static void DebugLevelUpCheck(float t, float now)"));
            int stage1 = tick.IndexOf("case 1:", StringComparison.Ordinal), stage2 = tick.IndexOf("case 2:", StringComparison.Ordinal);
            int stage61 = tick.IndexOf("case 61:", StringComparison.Ordinal), stage7 = tick.IndexOf("case 7:", StringComparison.Ordinal);
            int g1 = tick.IndexOf("if (playing) DebugFirstLevelUp(t, now);", StringComparison.Ordinal), g2 = g1 < 0 ? -1 : tick.IndexOf("if (playing) DebugFirstLevelUp(t, now);", g1 + 1, StringComparison.Ordinal);
            string wNoStr = Regex.Replace(wcode, "\"(?:[^\"\\\\\\n]|\\\\.)*\"", "\"\""), mNoStr = Regex.Replace(mcode, "\"(?:[^\"\\\\\\n]|\\\\.)*\"", "\"\"");     // calls, not log text
            int nGrant = Regex.Matches(wNoStr, @"\.\s*Debug_LevelUp\s*\(\s*\)").Count, nDirect = Regex.Matches(wNoStr, @"\.\s*LevelUp\s*\(\s*\)").Count;
            bool once = lu.IndexOf("_luAsked = true;", StringComparison.Ordinal) >= 0 && lu.IndexOf("_luAsked = true;", StringComparison.Ordinal) < lu.IndexOf(".Debug_LevelUp()", StringComparison.Ordinal)
                && Regex.Matches(mcode + "\n" + wcode, @"_luAsked\s*=\s*false").Count == 0 && lu.Contains("PauseRecruit.LevelUpDue(_rkJoined, Advisor.DebugPicks, t, _luAsked, paused)");
            Check("V8", "the first level-up: ExperienceProgress.Debug_LevelUp() (LevelUp() direct once as its fallback), called in stages 1 and 61 of the walk after its PreviewPause gate, once a session (_luAsked set before the call, never cleared), no XP through CollectXP",
                nGrant == 1 && nDirect == 1 && luChk.Contains("xp.LevelUp();") && !Regex.IsMatch(wNoStr + "\n" + mNoStr, @"\.\s*CollectXP(Repeated)?\s*\(") && once
                && g1 > stage1 && g1 < stage2 && g2 > stage61 && g2 < stage7 && g1 > gate && lu.Contains("the first level-up granted with the game's ExperienceProgress.Debug_LevelUp()"),
                nGrant + " Debug_LevelUp, " + nDirect + " LevelUp() call(s); stage 1 " + (g1 > stage1 && g1 < stage2) + ", stage 61 " + (g2 > stage61 && g2 < stage7) + ", once " + once);

            // V9 (fix_a review, H6 of series r1): every debug walk logs the [Debug] Perf sums so far just before its done line - the
            // Training Yard and run setup walks end ~46 s after load, before the first once-a-minute line (Y1 / Y2 / D_setup_1280 of r1
            // had no [perf] line at all); Perf.ReportNow logs nothing with the probe off or nothing measured
            string prev = Src("YazsCompanion.Mod/Preview.cs"), perf = Src("YazsCompanion.Mod/Perf.cs");
            if (prev == null || perf == null) Check("V9", "the sources Preview.cs / Perf.cs", false, "not found next to the bench");
            else
            {
                prev = prev.Replace("\r\n", "\n"); perf = perf.Replace("\r\n", "\n");
                string pcode = Code(prev), fcode = Code(perf), now = Code(Body(perf, "public static void ReportNow()"));
                var done = new[] { "Perf.ReportNow(); Plugin.Logger.LogInfo(\"[loadout] setup walk done\");", "Perf.ReportNow(); Plugin.Logger.LogInfo(\"[menu] pause walk done\");",
                    "Perf.ReportNow(); Plugin.Logger.LogInfo(\"[menu] preview done\");" };
                int inMenu = done.Count(d => mcode.Contains(d));
                bool yard = pcode.Contains("Perf.ReportNow(); Plugin.Logger.LogInfo(\"[preview] yard done\");");
                bool gated = now.Contains("if (!On || _nextReport < 0f) return;") && now.Contains("if (!any) return;") && now.Contains("Report(") && fcode.Contains("static void Report(float now)");
                Check("V9", "every debug walk (setup, pause, menu preview, Training Yard) logs its [Debug] Perf sums just before its done line (Perf.ReportNow: nothing with the probe off or nothing measured)",
                    inMenu == 3 && yard && gated, "Menu.cs " + inMenu + " of 3, Preview.cs yard " + yard + ", ReportNow gated " + gated);
            }
        }

        // ---------------------------------------------------------------- C16-11b: the results flow's stats step (step 2)
        static void ResultsShots()
        {
            string shots = Src("YazsCompanion.Mod/Shots.cs");
            if (shots == null) { Check("R1", "the source Shots.cs", false, "not found next to the bench"); return; }
            shots = shots.Replace("\r\n", "\n");
            string s2 = Code(PatchClass(shots, "P_ResultsShot2")), s2s = Code(PatchClass(shots, "P_ResultsShot2Setup")), cont = Code(PatchClass(shots, "P_ResultsContinue"));
            Check("R1", "P_ResultsShot2 hooks UIDefeatState2's OnEnable (1.0.2 never calls Setup); P_ResultsShot2Setup keeps Setup for a later game build - one class per target, each naming its step (no __originalMethod), each skipped alone when a game build lacks it",
                s2.Contains("typeof(UIDefeatState2), \"OnEnable\", Type.EmptyTypes") && s2.Contains("Shots.ResultsState2(\"OnEnable\", __instance)")
                && s2s.Contains("typeof(UIDefeatState2), \"Setup\", Type.EmptyTypes") && s2s.Contains("Shots.ResultsState2(\"Setup\", __instance)")
                && s2.Contains("static bool Prepare() { return Target() != null; }") && s2s.Contains("static bool Prepare() { return Target() != null; }")
                && !Code(shots).Contains("__originalMethod"));
            Check("R2", "P_ResultsContinue hooks UIDefeat.OnState1Continue (the fallback's clock), skipped when a game build lacks it",
                cont.Contains("typeof(UIDefeat)") && cont.Contains("\"OnState1Continue\"") && cont.Contains("Shots.ResultsContinue(") && cont.Contains("static bool Prepare() { return Target() != null; }"));

            // R3 never UIDefeatState2's Awake (one body shared by 4,075 methods): no code line of the mod names both
            var hits = new List<string>();
            string dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod"));
            int files = 0;
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    string rel = f.Substring(dir.Length).Replace('\\', '/');
                    if (rel.StartsWith("/obj/", StringComparison.Ordinal) || rel.StartsWith("/bin/", StringComparison.Ordinal)) continue;
                    files++;
                    foreach (var l in Code(File.ReadAllText(f)).Split('\n'))
                        if (l.Contains("UIDefeatState2") && l.Contains("Awake")) hits.Add(rel.TrimStart('/') + ": " + l.Trim());
                }
            Check("R3", "no patch names UIDefeatState2's Awake (its rva is one body shared by 4,075 methods)", files > 0 && hits.Count == 0, hits.Count > 0 ? string.Join(" | ", hits) : files + " source files read");

            // R4 the flow moves on before the Enabled test (a capture-off flow still counts)
            string res = Code(Body(shots, "public static void Results(int step)"));
            int flow = res.IndexOf("_resultsFlow++", StringComparison.Ordinal), en = res.IndexOf("if (!Enabled) return;", StringComparison.Ordinal);
            Check("R4", "Results(1) starts a new flow (_resultsFlow, step 1's time, no continue, no step 2 yet) before its Enabled test",
                flow >= 0 && en > flow && res.Contains("_step1At = Time.realtimeSinceStartup") && res.Contains("_contAt = -1f") && res.Contains("_s2Via = null"), "flow at " + flow + ", Enabled test at " + en);

            // R5 / R6 one step-2 capture a flow; with the flow (under 1 s) nothing; the fallback 1.5 s after the continue, looked at before
            // the capture queue's early return
            string st2 = Code(Body(shots, "public static void ResultsState2(string via, UIDefeatState2 state2)")), rt = Code(Body(shots, "static void ResultsTick(float now)")), tk = Code(Body(shots, "public static void Tick()"));
            int dedupe = st2.IndexOf("if (_s2Via == \"OnEnable\" || _s2Via == \"Setup\" || _s2Due >= 0f) return;", StringComparison.Ordinal), early = st2.IndexOf("since < 1.0f", StringComparison.Ordinal), cap = st2.IndexOf("Step2(via, now, null);", StringComparison.Ordinal);
            Check("R5", "ResultsState2 captures once a flow (OnEnable or Setup; a fallback capture lets a later OnEnable through), nothing under 1 s after step 1, held while state 1 is up",
                dedupe >= 0 && early > dedupe && cap > early && st2.Contains("State1Up()") && st2.Contains("waiting for state 1's continue"), "once a flow " + (dedupe >= 0) + ", the 1 s rule after it " + (early > dedupe) + ", the capture last " + (cap > early));
            int rtCall = tk.IndexOf("ResultsTick(", StringComparison.Ordinal), ret = tk.IndexOf("if (_pending.Count == 0) return;", StringComparison.Ordinal);
            Check("R6", "the fallback: 1.5 s after state 1's continue with no state-2 hook, 'via OnState1Continue', captures now and at 0.8 s - looked at every tick before the queue's early return",
                rtCall >= 0 && ret > rtCall && rt.Contains("now - _contAt < 1.5f") && rt.Contains("_s2Via = \"OnState1Continue\";") && rt.Contains("Later(0f, \"results2\"); Later(0.8f, \"results2b\");")
                && Code(Body(shots, "public static void ResultsContinue(UIDefeat defeat)")).Contains("_contAt = now;"));
        }
    }
}
