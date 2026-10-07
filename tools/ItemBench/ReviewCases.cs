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
// Generic names only (the repository is public): the game's card names, the bench's own build names and invented lent names.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            return _bad;
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
    }
}
