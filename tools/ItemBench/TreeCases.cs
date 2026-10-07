// 0.15.0 (roadmap 2026-10-06, Companion item 8: C15-08): the proof lines and the Training Yard purchase log, section 17 of the bench:
//   Y1  a purchase on the advice on screen: its place in the SPEND list ("advice #2"), a node bought several levels in one read
//       ("0>3"), the line's exact words.
//   Y2  a purchase the advice did not name ("not advised"): a node outside the SPEND list, the node to save for, a level past the
//       level the advice took the node to; one that starts below it is the advice's; no advice at all.
//   Y3  refunds: a points reset gives one 'refunded' line per node in the tree's order, no advice tag; a refund and a purchase in
//       the same read.
//   Y4  what is not a change: the same levels, the points alone, a node the earlier read did not have (another survivor's tab), no
//       earlier read; two nodes with the same key told apart by their place.
//   Y5  a player who follows the advice step by step: every purchase is 'advice #1' against the advice worked out before it; one
//       who buys the last place is told its number.
//   S   the sources: TreeUi logs the changes after a level change and before the new advice, against the advice shown then, and
//       forgets the record with the view; '[menu] first input this session' once, not verbose, from the pad, keys, mouse and touch;
//       '[badge] ribbon stepped aside' once; WideMenus counts every '[wide] ... restored' and the pause walk cycles "Menus on wide
//       screens" Off and back through its row, putting the player's value back.
// The trees are made up from the General tree's keys (TreePlan's own order picks the advice); generic names only (the repository
// is public).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class TreeCases
    {
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

        public static int Run()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.15.0: the Training Yard purchase log (TreeDiff: '[yard] bought / refunded', matched against the advice shown) and the proof lines");
            Advised();
            NotAdvised();
            Refunds();
            NoChange();
            Follow();
            Sources();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        // ---------------------------------------------------------------- a made-up General tree
        // the first four rows of TreePlan's General order and two more, every level costing 1 point (levels 0 .. 5)
        static readonly string[][] Rows =
        {
            new[] { "XPModifier", "Experience" }, new[] { "MoneyModifier", "Money" }, new[] { "WeaponDamage", "Weapon Damage" },
            new[] { "WeaponAttackSpeed", "Attack Speed" }, new[] { "Armor", "Armor" }, new[] { "MaxHP", "Max Health" },
        };

        static List<TNode> Tree(Dictionary<string, int> levels = null, int cost = 1)
        {
            var list = new List<TNode>();
            for (int i = 0; i < Rows.Length; i++)
            {
                var n = new TNode { Key = "General_" + Rows[i][0], Name = Rows[i][1], Kind = TKind.Stat, Rank = 1 + i / 3, Slot = i % 3, Min = 0, Max = 5, Costs = Enumerable.Repeat(cost, 5).ToArray() };
                int lv; if (levels != null && levels.TryGetValue(Rows[i][0], out lv)) n.Level = lv;
                list.Add(n);
            }
            return list;
        }

        static TAdvice AdviceOf(List<TNode> nodes, int points) { return TreePlan.Advise(nodes, TreePlan.TeamSteps(nodes), points); }
        static TNode Of(List<TNode> nodes, string key) { return nodes.First(n => n.Key == "General_" + key); }
        static List<TNode> Bought(List<TNode> before, string key, int to)
        {
            var levels = before.ToDictionary(n => n.Key.Substring("General_".Length), n => n.Level);
            levels[key] = to;
            return Tree(levels);
        }
        static string Lines(List<TChange> list) { return list.Count == 0 ? "(none)" : string.Join(" | ", list.Select(c => c.Line)); }
        static string Spend(TAdvice a) { return a.Now.Count == 0 ? "nothing" : string.Join(", ", a.Now.Select(b => b.Order + " " + b.Label)) + (a.SaveFor != null ? "; save for " + a.SaveFor.Label : ""); }

        // ---------------------------------------------------------------- Y1: on the advice
        static void Advised()
        {
            var a = Tree(); var shown = AdviceOf(a, 8);
            bool plan = shown.Now.Count == 3 && shown.Now[0].Node.Key == "General_XPModifier" && shown.Now[1].Node.Key == "General_MoneyModifier" && shown.Now[2].Node.Key == "General_WeaponDamage";
            Check("Y1", "the made-up tree's advice with 8 points: Experience 0>3, Money 0>3, Weapon Damage 0>2 (TreePlan's General order)", plan, Spend(shown));

            var b = Bought(a, "MoneyModifier", 1);
            var d = TreeDiff.Diff(TreeDiff.Levels(a), b, shown);
            Check("Y1", "Money bought 0>1 while the advice's second place was Money 0>3: 'bought Money 0>1 (advice #2)'",
                d.Count == 1 && d[0].Line == "bought Money 0>1 (advice #2)" && d[0].Bought && d[0].Advice == 2 && d[0].Node == Of(b, "MoneyModifier"), Lines(d));

            var c = Bought(a, "XPModifier", 3);
            d = TreeDiff.Diff(TreeDiff.Levels(a), c, shown);
            Check("Y1", "three levels between two reads (0.25 s apart) make one line: 'bought Experience 0>3 (advice #1)'",
                d.Count == 1 && d[0].Line == "bought Experience 0>3 (advice #1)", Lines(d));
        }

        // ---------------------------------------------------------------- Y2: not advised
        static void NotAdvised()
        {
            var a = Tree(); var shown = AdviceOf(a, 8);
            var d = TreeDiff.Diff(TreeDiff.Levels(a), Bought(a, "Armor", 1), shown);
            Check("Y2", "Armor bought 0>1, outside the SPEND list: 'bought Armor 0>1 (not advised)'", d.Count == 1 && d[0].Line == "bought Armor 0>1 (not advised)" && d[0].Advice == 0, Lines(d));

            var dear = Tree(cost: 3); var save = AdviceOf(dear, 2);
            bool saving = save.Now.Count == 0 && save.SaveFor != null && save.SaveFor.Node.Key == "General_XPModifier";
            d = TreeDiff.Diff(TreeDiff.Levels(dear), Bought(dear, "XPModifier", 1).Select(n => { n.Costs = Enumerable.Repeat(3, 5).ToArray(); return n; }).ToList(), save);
            Check("Y2", "the node to save for (2 points, 3 a level) bought after all - points from elsewhere: not the SPEND list's, 'not advised'",
                saving && d.Count == 1 && d[0].Line == "bought Experience 0>1 (not advised)", Spend(save) + " -> " + Lines(d));

            var wd = Of(a, "WeaponDamage");
            var hand = new TAdvice { Points = 3 };
            hand.Now.Add(new TBuy { Node = wd, From = 0, To = 2, Cost = 2, Order = 3 });
            var before = TreeDiff.Levels(Bought(a, "WeaponDamage", 2));
            d = TreeDiff.Diff(before, Bought(a, "WeaponDamage", 3), hand);
            Check("Y2", "a level past the advice (Weapon Damage advised 0>2, bought 2>3): 'not advised'", d.Count == 1 && d[0].Line == "bought Weapon Damage 2>3 (not advised)", Lines(d));
            before = TreeDiff.Levels(Bought(a, "WeaponDamage", 1));
            d = TreeDiff.Diff(before, Bought(a, "WeaponDamage", 3), hand);
            Check("Y2", "bought from below the advised level and past it (1>3 against 0>2): still the advice's place", d.Count == 1 && d[0].Line == "bought Weapon Damage 1>3 (advice #3)", Lines(d));

            d = TreeDiff.Diff(TreeDiff.Levels(a), Bought(a, "XPModifier", 1), null);
            Check("Y2", "no advice shown before the purchase (none worked out yet): 'not advised'", d.Count == 1 && d[0].Line == "bought Experience 0>1 (not advised)", Lines(d));
        }

        // ---------------------------------------------------------------- Y3: refunds
        static void Refunds()
        {
            var full = Tree(new Dictionary<string, int> { { "XPModifier", 3 }, { "MoneyModifier", 3 }, { "WeaponDamage", 2 }, { "Armor", 1 } });
            var reset = Tree();
            var shown = AdviceOf(full, 4);
            var d = TreeDiff.Diff(TreeDiff.Levels(full), reset, shown);
            Check("Y3", "a points reset: one 'refunded' line per node that had levels, in the tree's order, no advice tag",
                d.Count == 4 && Lines(d) == "refunded Experience 3>0 | refunded Money 3>0 | refunded Weapon Damage 2>0 | refunded Armor 1>0" && d.All(x => !x.Bought && x.Advice == 0), Lines(d));

            var mixed = Tree(new Dictionary<string, int> { { "XPModifier", 3 }, { "MoneyModifier", 3 }, { "WeaponDamage", 3 }, { "Armor", 0 } });
            d = TreeDiff.Diff(TreeDiff.Levels(full), mixed, shown);
            var wdAdvice = shown.Now.FirstOrDefault(x => x.Node.Key == "General_WeaponDamage");
            string want = "bought Weapon Damage 2>3 " + (wdAdvice != null && wdAdvice.To > 2 ? "(advice #" + wdAdvice.Order + ")" : "(not advised)") + " | refunded Armor 1>0";
            Check("Y3", "a refund and a purchase between the same two reads: both, in the tree's order", Lines(d) == want, Spend(shown) + " -> " + Lines(d));
        }

        // ---------------------------------------------------------------- Y4: not a change
        static void NoChange()
        {
            var a = Tree(new Dictionary<string, int> { { "XPModifier", 2 } });
            var shown = AdviceOf(a, 5);
            Check("Y4", "the same levels read again (points changed or not): nothing", TreeDiff.Diff(TreeDiff.Levels(a), Tree(new Dictionary<string, int> { { "XPModifier", 2 } }), shown).Count == 0);

            var other = Tree().Select(n => { n.Key = n.Key.Replace("General_", "Survivor_"); n.Level = 1; return n; }).ToList();
            Check("Y4", "another survivor's nodes (keys the earlier read did not have): nothing - that tab starts its own record",
                TreeDiff.Diff(TreeDiff.Levels(a), other, shown).Count == 0);
            Check("Y4", "no earlier read: nothing", TreeDiff.Diff(null, a, shown).Count == 0 && TreeDiff.Diff(new Dictionary<string, int>(), a, shown).Count == 0);

            var twins = new List<TNode> { new TNode { Key = "", Name = "Left", Rank = 2, Slot = 0, Max = 3 }, new TNode { Key = "", Name = "Right", Rank = 2, Slot = 1, Max = 3, Level = 1 } };
            var levels = TreeDiff.Levels(twins);
            var after = new List<TNode> { new TNode { Key = "", Name = "Left", Rank = 2, Slot = 0, Max = 3, Level = 1 }, new TNode { Key = "", Name = "Right", Rank = 2, Slot = 1, Max = 3, Level = 1 } };
            var d = TreeDiff.Diff(levels, after, null);
            Check("Y4", "two nodes without a key told apart by their place (Id = key @ rank.slot): only the one bought", levels.Count == 2 && d.Count == 1 && d[0].Line == "bought Left 0>1 (not advised)", Lines(d));
        }

        // ---------------------------------------------------------------- Y5: following the advice
        static void Follow()
        {
            var nodes = Tree(); int points = 6; var says = new List<string>(); bool allFirst = true;
            for (int step = 0; step < 6; step++)
            {
                var shown = AdviceOf(nodes, points);
                if (shown.Now.Count == 0) { allFirst = false; break; }
                var first = shown.Now[0];
                string key = first.Node.Key.Substring("General_".Length);
                var next = Bought(nodes, key, first.From + 1);
                var d = TreeDiff.Diff(TreeDiff.Levels(nodes), next, shown);
                if (d.Count != 1 || d[0].Advice != 1) allFirst = false;
                says.Add(Lines(d));
                nodes = next; points -= first.Node.CostFrom(first.From);
            }
            Check("Y5", "six purchases, each the first place of the advice worked out before it: 'advice #1' every time", allFirst && says.Count == 6, string.Join(" ; ", says));

            var a = Tree(); var shown2 = AdviceOf(a, 8); var last = shown2.Now.Last();
            var d2 = TreeDiff.Diff(TreeDiff.Levels(a), Bought(a, last.Node.Key.Substring("General_".Length), last.From + 1), shown2);
            Check("Y5", "the last place bought first: its number (#" + last.Order + ")", d2.Count == 1 && d2[0].Advice == last.Order && d2[0].Line.EndsWith("(advice #" + last.Order + ")"), Lines(d2));
        }

        // ---------------------------------------------------------------- S: the sources
        static void Sources()
        {
            string ui = Src("TreeUi.cs"), state = Src("TreeState.cs"), menu = Src("Menu.cs"), badge = Src("Badge.cs"), wide = Src("WideMenus.cs");
            Check("S", "TreeUi logs the changes once a level changed the signature and before the new advice, against the advice shown until then",
                ui != null && Regex.IsMatch(ui, @"_sig = s;\s*TreeState\.LogChanges\(container\.Pointer, tree, nodes, _advice\);[\s\S]{0,600}_advice = TreePlan\.Advise\("));
            Check("S", "the record is forgotten with the view and when the advice goes off; it compares the same tab and survivor only; every purchase logged",
                ui != null && state != null && Regex.Matches(ui, @"TreeState\.ForgetLevels\(\);").Count >= 2
                && state.Contains("if (_levels != null && tab == _levelsTab && tree == _levelsTree)") && state.Contains("foreach (var ch in TreeDiff.Diff(_levels, nodes, shown)) Plugin.Logger.LogInfo(\"[yard] \" + ch.Line);"));
            Check("S", "'[menu] first input this session' is said once, outside the verbose log",
                menu != null && Regex.IsMatch(menu, @"static void FirstInput\(string source\)\s*\{\s*if \(_firstSaid\) return;\s*_firstSaid = true;\s*Plugin\.Logger\.LogInfo\(""\[menu\] first input this session: "" \+ source\);"));
            Check("S", "its sources: the game's actions and the axes (pad, or key with a key down), the keys, the wheel and clicks (mouse, or touch by Input.touchCount), a second hover",
                menu != null && menu.Contains("PadOrKey(Pad(\"GoNextTab\") || Pad(\"GoNextTab2\")) || ByKey(Key(KeyCode.E) || Key(KeyCode.PageDown))") && menu.Contains("PadOrKey(Pad(\"Cancel\")) || ByKey(Key(KeyCode.Escape)")
                && menu.Contains("PadOrKey(submit) || ByKey(Key(KeyCode.Return)") && menu.Contains("PadOrKey(true);") && menu.Contains("FirstInput(KeyboardHeld() ? \"key\" : \"pad\")")
                && menu.Contains("if (UnityEngine.Input.touchCount > 0) return \"touch\";") && menu.Contains("if (!_firstSaid) FirstInput(Pointer());") && menu.Contains("++_hoverFocus >= 2) FirstInput(\"mouse\")")
                && menu.Contains("if (wheel != 0f) { if (!_firstSaid) FirstInput(\"mouse\");"));
            Check("S", "'[badge] ribbon stepped aside for the Skill Tree label' once, the first time the ribbon follows the label under half alpha",
                badge != null && Regex.IsMatch(badge, @"if \(want < 0\.5f && !_steppedSaid\)\s*\{\s*_steppedSaid = true;\s*Plugin\.Logger\.LogInfo\(""\[badge\] ribbon stepped aside for the Skill Tree label"));
            bool resetsOk = wide != null && wide.Split('\n').Where(l => l.Contains("s.Reset();") && !l.Contains("void Reset()")).All(l => l.Contains("if (fwhy != null)") || l.Trim() == "s.Reset();");
            Check("S", "WideMenus: every way back to the game's frame logs '[wide] ... restored' and counts it (a failed apply resets before changing anything)",
                wide != null && Regex.IsMatch(wide, @"restored \(""[^\n]*\n\s*Restores\+\+;\s*\n\s*s\.Reset\(\);") && resetsOk && wide.Contains("public static bool RunApplied"));
            Check("S", "the pause walk cycles 'Menus on wide screens' Off through its row, checks the restored line, cycles back to the player's value, checks the re-apply, and puts the value back on an error",
                menu != null && menu.Contains("_ppAt = now + 1.0f; _ppStage = 44; return;") && menu.Contains("WideCycle(WideMode.Off);") && menu.Contains("int restored = WideMenus.Restores - _ppRestores;")
                && menu.Contains("WideCycle(_ppWide); _ppWideChanged = false;") && menu.Contains("bool want = _ppWideWas && _ppWide != WideMode.Off, applied = WideMenus.RunApplied;")
                && menu.Contains("if (_ppWideChanged) { _ppWideChanged = false; try { WideMenus.Mode.Value = _ppWide; } catch { } }") && menu.Contains("if (!Work(\"di:wide\", 1)) break;"));
        }
    }
}
