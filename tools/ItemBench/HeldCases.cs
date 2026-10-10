// 0.16.0 (C16-01): the held-item rules (HeldRules.cs, pure) - what the items the squad holds change on the cards, the hints and the
// log: Has / Active / Line / ReadsLine / NewRun, the slot arithmetic, the rails of every 'held: ' / 'item: ' reason, the animal list
// against data\probe.json, the sources that call HeldRules (C16-01a..k add their sections here).
// RunPure (no game data) runs in the full bench (from Run) and in --no-data (Verdict.DataFree); Run adds the cases over the probe.
//   C16-01   H0 Has, H1 Active, H2 Line, H3 ReadsLine, H4 NewRun, H5 the slots, H6 the debug key's Warning, H7 the rails, H8 the WHY
//            band's order, H9 nothing held changes nothing, H10 the sources; H11 (Run) the animal list against the probe
//   C16-01a  Mana Potion held: MA1 the dead stat card, MA2 the cooldown items, MA3 a military offer, MA4 the words, MA5 Shrapnel
//   C16-01k  the Mana Potion card: MK1 the gain, MK2 the card's term, MK3 Evaluate, MK4 the words
//   C16-01e  Life Savings held: LS1 the generalised SkipWorth, LS2 every old call equal, LS3 the skip hint, LS4 the cash items, LS5 Liberate's
//            words, LS6 the keyword reading, LS7 the [held] part
//   C16-01b  Reserve Bench held: RB1 the skip on a chest / training (i - v), RB2 the recruits' level-ups (vi - vii), RB3 the words (viii)
//   C16-01d  Hijacked Signal held: HS1 Liberate's score and the crossover, HS2 the PLAN SOS row, HS3 the chest's cash, HS4 the words
//   C16-01f  A Cookie / Skip Rope: SR1 Skip Rope's worth, SR2 A Cookie's heal, SR3 the screens, SR4 the words and the log
//   H12      the rails of CH2's reasons and card words, the sources (Ranker, Plan, ItemRules, Wording, ScreenCall, RerollHint)
//   C16-01c  Wooden Stick / Empty Chest held: WS1 the slot cost, WS2 the cards (AVOID / WHY), WS3 the chest hints (REROLL on the merit, SKIP
//            on the net), WS4 the words and the [held] parts, WS5 Empty Chest's price against its own card, WS6 the sources
//   C16-01i  Last Unicorn: LU1 the animal count, LU2 an animal item with Last Unicorn held, LU3 the named pairs, LU4 the words; LU5 (Run)
//            every animal item of the probe pairs with a held Last Unicorn
//   C16-01j  the status conversions: CV1 the tables, CV2 rule A in Tank + SWAT + Engineer, CV3 rule A in Pyro + Medic + Mechanic, CV4 rule B,
//            CV5 only the status items move, CV6 the words, the [held] parts and the sources; CV7 (Run) the tables against the probe
// Game names only (the repository is public); 'Bench Widget' is the bench's own made-up item.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class HeldCases
    {
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static string F(double v) { return v.ToString("0.00", IC); }
        static bool Near(double a, double b, double tol = 0.01) { return Math.Abs(a - b) <= tol + 1e-9; }
        static string Src(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod", file));
            return File.Exists(p) ? File.ReadAllText(p).Replace("\r\n", "\n") : null;
        }

        /// <summary>The full bench (Checks.Run, after TreeCases): the pure part, then the cases over the probe's items.</summary>
        public static int Run(List<ProbeItem> items)
        {
            int bad = RunPure();
            _bad = 0;
            Console.WriteLine("  -- over the 1.0.2 probe's items");
            AnimalProbe(items);
            UnicornProbe(items); ConvertProbe(items);
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return bad + _bad;
        }

        /// <summary>The cases without game data (also --no-data).</summary>
        public static int RunPure()
        {
            Console.WriteLine("\n=== 0.16.0: the held-item rules (C16-01)");
            _bad = 0;
            HeldRules.ResetRun();
            Console.WriteLine("  -- C16-01 the table, the [held] lines, the run, the debug key");
            Has(); Active(); Line(); Reads(); NewRun(); Slots(); Pretend(); Rails(); WhyFirst(); NothingHeld(); Sources();
            Console.WriteLine("  -- C16-01a Mana Potion held: ability cooldown reduction is switched off");
            DeadStat(); CdrItems(); MilitaryOffer(); DeadWords(); Shrapnel();
            Console.WriteLine("  -- C16-01k the Mana Potion card: an ability reset every 4s against the squad's cooldown reduction");
            Gain(); CardTerm(); ManaEvaluate(); ManaWords();
            Console.WriteLine("  -- C16-01e Life Savings held: cash heals instead");
            LsWorth(); LsOldCalls(); LsHint(); LsItems(); LsLiberate(); LsKeyword(); LsPart();
            Console.WriteLine("  -- C16-01b Reserve Bench held: a chest or training skip is a level-up, a recruit brings level-ups");
            RbSkip(); RbRecruit(); RbWords();
            Console.WriteLine("  -- C16-01d Hijacked Signal held: Liberate gives two level-ups, chest items pay cash");
            HsScore(); HsPlan(); HsChest(); HsWords();
            Console.WriteLine("  -- C16-01f A Cookie and Skip Rope: what a skip pays beyond its heal and cash");
            SrWorth(); SrCookie(); SrScreens(); SrWords();
            Console.WriteLine("  -- CH2's rails and sources");
            Ch2Rails(); Ch2Sources();
            Console.WriteLine("  -- C16-01c Wooden Stick / Empty Chest held: an item that fills an empty slot costs 10% XP / 10% damage");
            WsCost(); WsCards(); WsScreen(); WsWords(); WsPrice(); WsSources();
            Console.WriteLine("  -- C16-01i Last Unicorn counts the animal items held; the pairs the item texts name");
            LuCount(); LuPairs(); LuNamed(); LuWords();
            Console.WriteLine("  -- C16-01j the status conversions: a status item by what still causes its status");
            CvTables(); CvRuleA(); CvFire(); CvRuleB(); CvOnly(); CvWords();
            HeldRules.ResetRun();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        // ================================================================ helpers
        static HeldFacts Held(params string[] names) { var h = new HeldFacts(); foreach (var n in names) h.Names.Add(n); return h; }
        static List<KeyValuePair<double, double>> Cds(double reduction, params double[] bases)
        {
            return bases.Select(b => new KeyValuePair<double, double>(b, b * (1 - reduction))).ToList();
        }
        static readonly double[] Four = { 10, 20, 30, 12 };

        static Knowledge _k;
        static Knowledge K { get { return _k ?? (_k = Knowledge.FromJson(Knowledge.DefaultJson)); } }

        /// <summary>Tank + SWAT + Engineer at 02:00 of a 20:00 Normal run (research c16_held_items, scores_today.txt): Explosive, Kinetic and
        /// Electric dealt evenly, nothing held.</summary>
        static ItemContext Tse(HeldFacts held, double seconds = 120)
        {
            var tags = new TagProfile();
            tags.Deals("Explosive", "Rocket Launcher", 1); tags.Deals("Kinetic", "Assault Rifle", 1); tags.Deals("Electric", "Tesla", 1);
            var ctx = new RunContext { Mode = "Normal", Goal = 1200, Seconds = seconds, LevelRate = 2.9, Health = 1, D = new Doctrine() };
            var c = new ItemContext { Squad = new List<string> { "Tank", "SWAT", "Engineer" }, Tags = tags, K = K, Ctx = ctx,
                Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase), OwnedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) };
            c.HeldFacts = held;
            if (held != null) { foreach (var n in held.Names) c.Held.Add(n); foreach (var n in held.Pretend) c.Held.Add(n); }
            return c;
        }

        sealed class Cell { public double Score; public string Line; public List<string> Why; public ItemSay Say; }
        static Cell Eval(string name, ItemContext c, string asset = null)
        {
            c.Say = new ItemSay(); c.Asset = asset; c.Stats = null; c.Healing = false;
            var why = new List<string>();
            double s = ItemRules.Evaluate(name, "", c, why);
            var say = c.Say; c.Say = null;
            return new Cell { Score = s, Line = Wording.Item(say), Why = why, Say = say };
        }

        /// <summary>A form as the card draws it in <paramref name="room"/> characters: the rails' complaints (empty = fine).</summary>
        static List<string> RailsAt(string form, int room) { return Wording.Rails(Wording.Cap(form), room); }

        /// <summary>The line a held-item rule's words draw in <paramref name="room"/> visible characters (Wording.Item with LeadForms).</summary>
        static string Drawn(string[] forms, int room)
        {
            Wording.Ambient(room, null);
            try { return Wording.Item(new ItemSay { LeadForms = forms }); }
            finally { Wording.Ambient(0, null); }
        }

        /// <summary>Runs <paramref name="f"/> with the stub logger's stderr lines kept out of the bench's output (a renamed item's
        /// '[items] no rule for ...', said once a session by the item rules).</summary>
        static T Quiet<T>(Func<T> f)
        {
            var was = Console.Error;
            Console.SetError(new StringWriter());
            try { return f(); } finally { Console.SetError(was); }
        }

        // ================================================================ C16-01
        static void Has()
        {
            var byName = Held("Mana Potion");
            var byAsset = new HeldFacts(); byAsset.Names.Add("Renamed Potion"); byAsset.Assets.Add("Item_ManaPotion");
            var byPretend = new HeldFacts(); byPretend.Pretend.Add("Mana Potion");
            var lower = Held("mana potion");
            var none = new HeldFacts();
            Check("H0", "Has: by name, by asset only (a renamed item), by pretend, case-insensitive; not with nothing held",
                byName.Has(HeldRules.ManaPotion) && byAsset.Has(HeldRules.ManaPotion) && byPretend.Has(HeldRules.ManaPotion) && lower.Has(HeldRules.ManaPotion) && !none.Has(HeldRules.ManaPotion) && !byName.Has(HeldRules.ReserveBench));
            Check("H0", "HasName: the names held and the pretend names; not an asset, not another name",
                byName.HasName("Mana Potion") && byPretend.HasName("mana potion") && !byAsset.HasName("Mana Potion") && !byName.HasName("Reserve Bench") && !none.HasName(null));
            Check("H0", "IsItem: the card's name, or its asset ('(Clone)' cut) when the name is another; CutClone",
                HeldRules.IsItem(HeldRules.ManaPotion, "Mana Potion", null) && HeldRules.IsItem(HeldRules.ManaPotion, "Potion Of Mana", "Item_ManaPotion(Clone)")
                && !HeldRules.IsItem(HeldRules.ManaPotion, "Chick Magnet", "Item_ChickMagnet") && HeldRules.CutClone("Item_ManaPotion (Clone)") == "Item_ManaPotion" && HeldRules.CutClone(null) == "");
        }

        static void Active()
        {
            var h = Held("Expired Sushi", "Mana Potion"); h.Pretend.Add("Reserve Bench"); h.Names.Add("Homing Pigeon");
            var a = HeldRules.Active(h);
            Check("H1", "Active: the table's items held or pretended, in the table's order (an item no rule reads is left out)",
                string.Join(", ", a.Select(i => i.Name)) == "Mana Potion, Reserve Bench, Expired Sushi" && HeldRules.Active(null).Count == 0 && HeldRules.Active(new HeldFacts()).Count == 0,
                string.Join(", ", a.Select(i => i.Name)));
            Check("H1", "the table: 16 items with the 1.0.2 assets (Hijacked Signal's is a quest asset), no name twice; the 8 animal items",
                HeldRules.All.Length == 16 && HeldRules.All.Select(i => i.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 16
                && HeldRules.HijackedSignal.Asset == "Item_Quest_004_HijackedSignal" && HeldRules.AnimalNames.Length == 8 && HeldRules.All.All(i => i.Asset.StartsWith("Item_")));
        }

        static void Line()
        {
            var mana = Held("Mana Potion");
            var pretend = new HeldFacts(); pretend.Pretend.Add("Mana Potion");
            string l = HeldRules.Line(mana, new RunContext()), p = HeldRules.Line(pretend, null);
            Check("H2", "Line: null with nothing held; Mana Potion's part", HeldRules.Line(new HeldFacts(), null) == null && HeldRules.Line(null, null) == null
                && l == "[held] Mana Potion: ability cooldown reduction off", l);
            Check("H2", "Line under [Debug] HeldPretend: 'PRETEND (debug): <names> - ' first", p == "[held] PRETEND (debug): Mana Potion - Mana Potion: ability cooldown reduction off", p);
        }

        static void Reads()
        {
            var h = new HeldFacts { Max = 8, Equipped = 3, FreeSlots = 5, FreeStat = 5, LeaderCdr = 0.15, SinceJoin = 372, AnimalHeld = 1, AbilitiesOwned = 4, Cooldowns = Cds(0.15, Four) };
            string r = HeldRules.ReadsLine(h);
            Check("H3", "ReadsLine: the spec's line", r == "[held] reads: item slots 3 of 8 filled, 5 empty (team statistic: 5); leader's ability cooldown reduction 15%; 372 s since the last survivor joined; animal items held 1; abilities with a cooldown 4 of 4 owned", r);
            string u = HeldRules.ReadsLine(new HeldFacts());
            Check("H3", "ReadsLine with nothing read: '?' for each value", u == "[held] reads: item slots ? of ? filled, ? empty (team statistic: ?); leader's ability cooldown reduction ?; ? s since the last survivor joined; animal items held 0; abilities with a cooldown ? of 0 owned", u);
            var p = new HeldFacts { Max = 8, Equipped = 0, FreeSlots = 8, FreeStat = 8, LeaderCdr = 0, SinceJoin = 0.4, Cooldowns = new List<KeyValuePair<double, double>>() }; p.Pretend.Add("Mana Potion");
            string pr = HeldRules.ReadsLine(p);
            Check("H3", "ReadsLine under [Debug] HeldPretend: led by 'PRETEND (debug): Mana Potion - '",
                pr == "[held] PRETEND (debug): Mana Potion - reads: item slots 0 of 8 filled, 8 empty (team statistic: 8); leader's ability cooldown reduction 0%; 0 s since the last survivor joined; animal items held 0; abilities with a cooldown 0 of 0 owned", pr);
        }

        static void NewRun()
        {
            HeldRules.ResetRun();
            bool first = HeldRules.NewRun(30, 111), same = HeldRules.NewRun(95, 111), back = HeldRules.NewRun(85, 111), small = HeldRules.NewRun(81, 111);
            bool key = HeldRules.NewRun(90, 222), unread = HeldRules.NewRun(120, 0), again = HeldRules.NewRun(130, 222);
            HeldRules.ResetRun();
            bool reset = HeldRules.NewRun(500, 222);
            HeldRules.ResetRun();
            Check("H4", "NewRun: the first call; not the same run; the clock back 10 s (Try Again); not back 4 s; another GameplayMaster; not an unread one (0); ResetRun",
                first && !same && back && !small && key && !unread && !again && reset,
                "first " + first + ", same " + same + ", back 10 s " + back + ", back 4 s " + small + ", key " + key + ", key 0 " + unread + ", key again " + again + ", reset " + reset);
        }

        static void Slots()
        {
            Check("H5", "the free slots by the game's formula: max 8, equipped 3 -> 5; never under 0; max unread -> -1 (shown '?')",
                HeldRules.FreeOf(8, 3) == 5 && HeldRules.FreeOf(8, 9) == 0 && HeldRules.FreeOf(HeldRules.CountOf(double.NaN), 3) == -1 && HeldRules.FreeOf(4, -1) == 4);
            Check("H5", "the filled slots: statistic 49 as read, else (NaN, negative) the DISTINCT items held",
                HeldRules.EquippedOf(3, 5) == 3 && HeldRules.EquippedOf(double.NaN, 5) == 5 && HeldRules.EquippedOf(-1, 2) == 2 && HeldRules.EquippedOf(0, 4) == 0
                && HeldRules.CountOf(2.6) == 3 && HeldRules.CountOf(double.PositiveInfinity) == -1);
        }

        static void Pretend()
        {
            string one = HeldRules.PretendWarning(HeldRules.SplitNames(" Mana Potion , ,"));
            string two = HeldRules.PretendWarning(HeldRules.SplitNames("Mana Potion,Bench Widget,Homing Pigeon,Schrodinger's Cat,mana potion"));
            Check("H6", "the Warning: none for an empty key; the names as given (trimmed, once each)",
                HeldRules.PretendWarning(HeldRules.SplitNames("")) == null && HeldRules.PretendWarning(null) == null
                && one == "[held] HeldPretend is on (debug): Mana Potion - the advice treats them as held; the game is unchanged", one);
            Check("H6", "a name no held-item rule reads and no animal item is named; an animal name (the cat without its umlaut too) is not",
                two == "[held] HeldPretend is on (debug): Mana Potion, Bench Widget, Homing Pigeon, Schrodinger's Cat - the advice treats them as held; the game is unchanged | not a held-item rule's item: Bench Widget", two);
            Check("H6", "an animal name counts for Last Unicorn (AnimalOf folds the umlaut); a table name is in the table",
                HeldRules.IsAnimalName("Schrodinger's Cat") && HeldRules.AnimalOf("schrodinger's cat").Asset == "Item_SchrodingersCat" && HeldRules.IsAnimalName("Last Unicorn")
                && !HeldRules.IsAnimalName("Mana Potion") && HeldRules.InTable("reserve bench") && !HeldRules.InTable("Bench Widget"));
        }

        /// <summary>Every 'held: ' / 'item: ' reason of C16-01a / C16-01k (without its prefix) passes WhyText.Fits; every card form passes the
        /// rails at 50 and its last form at 30.</summary>
        static void Rails()
        {
            var reasons = new List<string> { HeldRules.DeadWhy, HeldRules.ShrapnelWhy };
            var forms = new List<string[]> { HeldRules.DeadSay };
            foreach (var row in ItemBook.All.Where(r => r.ManaWhy != null)) { reasons.Add(HeldRules.Prefix + row.ManaWhy); if (row.ManaLead != null) forms.Add(row.ManaLead); }
            foreach (var r in new[] { 0.0, 0.2, 0.4, 0.6, 0.95 })
                foreach (var cds in new[] { Cds(r, Four), Cds(r, 12), Cds(r, Four.Concat(Four).Concat(Four).ToArray()), new List<KeyValuePair<double, double>>() })
                {
                    double t = 2.0; string[] lead; string log;
                    HeldRules.ManaPotionCard(cds, ref t, out lead, out log);
                    if (lead != null) { reasons.Add(HeldRules.ItemPrefix + lead[0]); forms.Add(lead); }
                }
            var badReasons = reasons.Where(x => !(x.StartsWith(HeldRules.Prefix) || x.StartsWith(HeldRules.ItemPrefix)) || !WhyText.Fits(Wording.Cap(x.Substring(6)))).ToList();
            Check("H7", reasons.Count + " 'held: ' / 'item: ' reasons: each fits the WHY band (64, the rails: no multiplier, no bare score)", badReasons.Count == 0, badReasons.Count == 0 ? null : string.Join(" / ", badReasons));
            var badForms = new List<string>();
            foreach (var f in forms)
            {
                if (RailsAt(f[0], Wording.Budget).Count > 0) badForms.Add(f[0] + " (" + string.Join(", ", RailsAt(f[0], Wording.Budget)) + ")");
                string at30 = Drawn(f, 30);         // as the card draws it in 30: the forms in turn, each without its parenthesis, the last one cut
                if (Wording.Rails(at30, 30).Count > 0) badForms.Add(at30 + " at 30 (" + string.Join(", ", Wording.Rails(at30, 30)) + ")");
            }
            Check("H7", forms.Count + " card word lists: the first form on the rails at 50, the line drawn in 30 on the rails at 30", badForms.Count == 0, badForms.Count == 0 ? null : string.Join(" / ", badForms));
        }

        /// <summary>WhyText.Reasons lists a 'held: ' reason first, even beside four verdict facts (log reasons came after them and were cut at four).</summary>
        static void WhyFirst()
        {
            var say = new ItemSay { Tier = "A", Note = "pickup range, and cooldowns on every magnet", Want = "abilities", WantBuild = "Bench" };
            say.Boosts.Add(new ItemFit { What = "Electric", Who = "Tesla", Typed = true, Pct = 33, Weight = 1 });
            say.Boosts.Add(new ItemFit { What = "abilities", Weight = 0.5 });
            say.Scaling = "early";
            var w = new CardWords { Kind = SayKind.Item, Item = say };
            var why = new List<string> { "A-tier item, pickup range, and cooldowns on every magnet", "Tesla: electric", "Bench wants abilities", "grows all run: take it early", "held: its cooldown bonus does nothing with Mana Potion" };
            var r = WhyText.Reasons(w, why, "Boosts Electric - your Tesla deals it");
            Check("H8", "a 'held: ' reason first beside four verdict facts", r.Count > 0 && r[0] == "Its cooldown bonus does nothing with Mana Potion", string.Join(" / ", r));
            var why2 = new List<string> { "A-tier item, strong on Mechanic with Engineer", "item: switches off your 60% ability cooldown reduction", "Mana Potion: casts x0.62 over 4 abilities with a cooldown" };
            var r2 = WhyText.Reasons(new CardWords { Kind = SayKind.Item, Item = new ItemSay { Tier = "A", Note = "strong on Mechanic with Engineer" } }, why2, "A-tier - strong on Mechanic with Engineer");
            Check("H8", "an 'item: ' reason first; the log's 'Mana Potion: casts x...' never reaches the band", r2.Count > 0 && r2[0] == "Switches off your 60% ability cooldown reduction" && !r2.Any(x => x.Contains("casts x")), string.Join(" / ", r2));
            var quest = new CardWords { Kind = SayKind.Item, Quest = "quest: no items - taking any item fails it", Item = new ItemSay() };
            var r3 = WhyText.Reasons(quest, why, "Quest: no items", true, true, null);
            Check("H8", "under a quest that makes the card AVOID the held reasons stay out too", !r3.Any(x => x.Contains("Mana Potion")), string.Join(" / ", r3));
        }

        /// <summary>An empty HeldFacts (nothing held, no pretend) changes no score of the book's rows, and the plumbing's functions return the
        /// 0.15.0 values - with no item held every older section prints what it printed before.</summary>
        static void NothingHeld()
        {
            var diffs = new List<string>();
            foreach (var row in ItemBook.All)
            {
                foreach (double sec in new[] { 120.0, 960.0 })
                {
                    var a = Eval(row.Name, Tse(null, sec)); var b = Eval(row.Name, Tse(new HeldFacts { Cooldowns = null }, sec));
                    if (a.Score != b.Score || a.Line != b.Line || string.Join("|", a.Why) != string.Join("|", b.Why)) diffs.Add(row.Name + " " + F(a.Score) + " / " + F(b.Score));
                }
            }
            Check("H9", ItemBook.All.Length + " book items at 02:00 and 16:00: an empty HeldFacts changes no score, line or reason", diffs.Count == 0, diffs.Count == 0 ? null : string.Join(", ", diffs.Take(6)));
            var empty = new HeldFacts();
            int n; double rh = HeldRules.RecruitHeld(2.37, empty, out n);
            bool lib = HeldRules.LiberateScore(0.3, 1, false, 1) == 1.0 + 3.2 * (1 - 0.3) + 0.4 * 1 && HeldRules.LiberateScore(0.9, 0, true, 1) == 5 && HeldRules.LiberateLevelUps(empty) == 1;
            Check("H9", "the plumbing with nothing held: a recruit unchanged, Liberate's 0.15.0 formula (5 with a full squad), no skip or slot term",
                rh == 2.37 && n == 0 && lib && HeldRules.SkipLevelUps(empty, "Chest") == 0 && !HeldRules.CashHeals(empty) && !HeldRules.CashAlsoOnPick(empty, "Chest")
                && HeldRules.SkipExtraHeal(empty, "LevelUp") == 0 && HeldRules.SkipRopeWorth(empty, "Chest", 1.36) == 0 && HeldRules.SkipBy(empty, "Chest") == null && HeldRules.SlotBy(empty) == null);
            var x = new ScreenCallIn();
            Check("H9", "ScreenCallIn's held fields default to nothing (the 0.15.0 decisions); HintCard.HeldCost 0",
                x.SkipLevelUps == 0 && x.SkipLevelUpBy == "Reserve Bench" && !x.CashHeals && !x.CashAlsoOnPick && x.ExtraHeal == 0 && x.SkipBonus == 0 && x.SkipBy == null && x.SlotBy == null && new HintCard().HeldCost == 0);
        }

        static void Sources()
        {
            string ranker = Src("Ranker.cs"), hint = Src("RerollHint.cs"), plan = Src("Plan.cs"), advisor = Src("Advisor.cs"), read = Src("HeldRead.cs"), state = Src("GameState.cs"),
                plugin = Src("Plugin.cs"), why = Src("WhyText.cs"), words = Src("Wording.cs"), rules = Src("ItemRules.cs");
            if (new[] { ranker, hint, plan, advisor, read, state, plugin, why, words, rules }.Any(s => s == null)) { Check("H10", "the sources are readable from the bench", false, "a file is missing next to tools\\ItemBench"); return; }
            Check("H10", "Ranker, RerollHint, Plan and Advisor call HeldRules",
                ranker.Contains("HeldRules.") && hint.Contains("HeldRules.") && plan.Contains("HeldRules.") && advisor.Contains("HeldRules."));
            Check("H10", "Ranker: the dead stat card, Shrapnel's cut, Liberate's and a recruit's held terms; ItemContextOf copies HeldFacts, the pretend names and C16-02b's five values; ItemScore sets Animal and the asset",
                ranker.Contains("HeldRules.StatDead(s.Held, stat, out dead)") && ranker.Contains("HeldRules.ShrapnelOff(s.Held, evoName, G.Asset(evo))") && ranker.Contains("HeldRules.LiberateScore(")
                && ranker.Contains("HeldRules.RecruitHeld(") && ranker.Contains("c.HeldFacts = s.Held;") && ranker.Contains("foreach (var n in s.Held.Pretend) c.Held.Add(n);")
                && ranker.Contains("c.FreeSlots = s.FreeSlots; c.Luck = s.Luck; c.Pickup = s.Pickup; c.MoveSpeed = s.MoveSpeed; c.LockdownAdvised = s.LockdownAdvised;")
                && ranker.Contains("ic.Animal = facts.Animal;"));
            Check("H10", "RerollHint fills ScreenCallIn's held fields and HintCard.HeldCost, lifts the rescue screen's 'late'; Plan's SOS row asks RecruitLevelUps",
                hint.Contains("x.SkipLevelUps = HeldRules.SkipLevelUps(held, x.Screen);") && hint.Contains("x.CashHeals = HeldRules.CashHeals(held);") && hint.Contains("x.SkipBonus = HeldRules.SkipRopeWorth(")
                && hint.Contains("k.HeldCost = w.Item.HeldCost;") && hint.Contains("HeldRules.RecruitLevelUps(s.Held) >= 1 ? Math.Max(s.Ctx.RecruitValue, RerollCall.Late)")
                && plan.Contains("s.Ctx.RecruitValue < RerollCall.Late && HeldRules.RecruitLevelUps(s.Held) < 1"));
            Check("H10", "Advisor writes the [held] line after [ctx] and the reads line once a run; Plugin's Warning is HeldRules.PretendWarning's",
                advisor.Contains("HeldRules.Line(snap.Held, snap.Ctx)") && advisor.Contains("HeldRules.NewRun(snap.Seconds, masterKey)") && advisor.Contains("HeldRules.ReadsLine(snap.Held)")
                && advisor.IndexOf("HeldRules.Line(", StringComparison.Ordinal) > advisor.IndexOf("\"[ctx] \"", StringComparison.Ordinal) && plugin.Contains("HeldRules.PretendWarning(names)"));
            Check("H10", "HeldRead: statistics 43 / 49 / 50 from the team, PlayerAbilityCDRed of the leader, lastCharacterSpawnTime, the cooldowns by the interop getters - never the game's candidate list; GameState calls it only in a game mode",
                read.Contains("EType.InternalMaxItems") && read.Contains("EType.InternalNumEquippedItems") && read.Contains("EType.InternalNumFreeItemSlots") && read.Contains("EType.PlayerAbilityCDRed")
                && read.Contains("lastCharacterSpawnTime") && read.Contains("GetAbilityCooldown(") && read.Contains("canReceiveCooldownReduction") && read.Contains("isDisabled")
                && !read.Contains("GetCooldownReductionCandidates(") && read.Contains("s.FreeSlots") && state.Contains("HeldRead.Fill(master, s)") && state.Contains("inMode = master.currentGameMode != null"));
            string reasons = Between(why, "public static List<string> Reasons(CardWords w, IList<string> why, string shown, bool avoid", "public static void Distinct(");
            Check("H10", "WhyText.Reasons takes the 'held: ' / 'item: ' reasons right after the quest's; Wording says CardWords.Held after the quest and ItemSay.LeadForms before the Lead",
                reasons != null && reasons.Contains("HeldRules.Prefix") && reasons.Contains("HeldRules.ItemPrefix") && reasons.IndexOf("HeldRules.Prefix", StringComparison.Ordinal) < reasons.IndexOf("switch (w.Kind)", StringComparison.Ordinal)
                && words.Contains("if (w.Held != null) return Say(null, w.Held);") && words.IndexOf("s.LeadForms != null", StringComparison.Ordinal) < words.IndexOf("if (s.Lead != null)", StringComparison.Ordinal)
                && rules.Contains("HeldRules.ManaPotionCard(") && rules.Contains("HeldRules.CdrItem(") && rules.Contains("HeldRules.SlotCost("));
        }
        static string Between(string src, string from, string to)
        {
            if (src == null) return null;
            int a = src.IndexOf(from, StringComparison.Ordinal); if (a < 0) return null;
            int b = src.IndexOf(to, a + from.Length, StringComparison.Ordinal);
            return b < 0 ? src.Substring(a) : src.Substring(a, b - a);
        }

        // ================================================================ C16-01a
        static void DeadStat()
        {
            string[] say;
            var byAsset = new HeldFacts(); byAsset.Assets.Add("Item_ManaPotion");
            var pretend = new HeldFacts(); pretend.Pretend.Add("Mana Potion");
            bool name = HeldRules.StatDead(Held("Mana Potion"), "AbilityCooldown", out say) && say != null && say[0] == "does nothing while you hold Mana Potion";
            bool asset = HeldRules.StatDead(byAsset, "AbilityCooldown", out say), pre = HeldRules.StatDead(pretend, "abilitycooldown", out say), stat = HeldRules.StatDead(Held("Mana Potion"), "PlayerAbilityCDRed", out say);
            bool without = HeldRules.StatDead(new HeldFacts(), "AbilityCooldown", out say) || HeldRules.StatDead(null, "AbilityCooldown", out say);
            bool weapon = HeldRules.StatDead(Held("Mana Potion"), "WeaponCooldownMod", out say);
            Check("MA1", "StatDead: Chem-Light Battery's stat (AbilityCooldown, or PlayerAbilityCDRed) with Mana Potion held by name, by asset, by pretend; not without it; not Sleight Of Hand's WeaponCooldownMod",
                name && asset && pre && stat && !without && !weapon);
            double floor = Synergy.StatCard(Synergy.RarityWeight("Endless", 0.1), 0, false);
            double least = new[] { "Common", "Rare", "Legendary", "Endless" }.Min(r => Synergy.StatCard(Synergy.RarityWeight(r, double.NaN), 0.05, false));
            Check("MA1", "DeadCard " + F(HeldRules.DeadCard) + " is under 1 (AVOID) and under every live stat card (StatCard's floor " + F(floor) + "; the weakest card of any rarity " + F(least) + ")",
                HeldRules.DeadCard < 1.0 && HeldRules.DeadCard < floor && HeldRules.DeadCard < least);
        }

        static void CdrItems()
        {
            var mana = Held("Mana Potion");
            var rows = new[] { Tuple.Create("Chick Magnet", 0.5), Tuple.Create("Hyperactivity", 0.75), Tuple.Create("Devil's Deal", 0.9) };
            foreach (var r in rows)
            {
                var before = Eval(r.Item1, Tse(new HeldFacts())); var after = Eval(r.Item1, Tse(mana));
                double want = 1 + (before.Score - 1) * r.Item2;
                bool spec = r.Item1 != "Chick Magnet" || (Near(before.Score, 3.45) && Near(after.Score, 2.225));     // the spec's 3.45 -> 2.225 (the other two moved with the item book)
                bool lead = r.Item1 != "Devil's Deal";
                bool words = lead ? after.Why[0].StartsWith("held: ") && after.Say.LeadForms != null && after.Line == Wording.Cap(after.Say.LeadForms[0])
                                  : after.Say.LeadForms == null && after.Line == before.Line && after.Why.Last() == "held: its ability cooldown part is off (Mana Potion)";
                Check("MA2", r.Item1 + " at 02:00 (Tank + SWAT + Engineer): " + F(before.Score) + " -> " + F(after.Score) + " = 1 + (score - 1) x " + r.Item2.ToString("0.##", IC)
                    + (lead ? ", the card says it" : ", a WHY reason only (the line stays)"), Near(after.Score, want) && words && spec, "'" + after.Line + "' | " + after.Why[0]);
            }
            var nb = Eval("Nuts & Bolts", Tse(new HeldFacts())); var nb2 = Eval("Nuts & Bolts", Tse(mana));
            Check("MA2", "Nuts & Bolts unchanged " + F(nb.Score) + " (its halved cooldown is its own roll, not the stat)", nb.Score == nb2.Score && nb.Line == nb2.Line);
            // a game build that renames Chick Magnet: no book row by that name (the keyword reading, its guide tier kept by the player in
            // knowledge.json), the held-item rule still finds it by its asset
            var kt = Knowledge.FromJson(Knowledge.DefaultJson); kt.ItemTier["Chick Magnet Deluxe"] = "A";
            Func<HeldFacts, ItemContext> tiered = h => { var c = Tse(h); c.K = kt; return c; };
            var renamed = Quiet(() => Eval("Chick Magnet Deluxe", tiered(mana), "Item_ChickMagnet")); var renamed0 = Quiet(() => Eval("Chick Magnet Deluxe", tiered(new HeldFacts()), "Item_ChickMagnet"));
            Check("MA2", "a renamed Chick Magnet is found by its asset (ItemBook.OfAsset)", renamed0.Score > 1.5 && Near(renamed.Score, 1 + (renamed0.Score - 1) * 0.5) && renamed.Why[0].StartsWith("held: "), F(renamed0.Score) + " -> " + F(renamed.Score));
            var pre = new HeldFacts(); pre.Pretend.Add("Mana Potion");
            var cm = Eval("Chick Magnet", Tse(pre));
            Check("MA2", "the same with Mana Potion pretended ([Debug] HeldPretend)", Near(cm.Score, Eval("Chick Magnet", Tse(mana)).Score) && cm.Line == "Its cooldown bonus does nothing with Mana Potion", cm.Line);
            var other = new List<string>();
            foreach (var row in ItemBook.All)
            {
                if (row.ManaWhy != null) continue;
                var a = Eval(row.Name, Tse(new HeldFacts())); var b = Eval(row.Name, Tse(Held("Reserve Bench")));
                if (a.Score != b.Score || a.Line != b.Line) other.Add(row.Name);
            }
            Check("MA2", "Mana Potion not held: no other book item changes (another item held, no rule of this lane reads it)", other.Count == 0, string.Join(", ", other));
        }

        static void MilitaryOffer()
        {
            var ctx = new RunContext { Mode = "Normal", Goal = 1200, Seconds = 120, LevelRate = 2.9, Health = 1, D = new Doctrine() };
            double luck = K.MilitaryStat["Luck"], dmg = K.MilitaryStat["WeaponDamageMod"];
            string[] say;
            var offer = new List<KeyValuePair<string, double>>
            {
                new KeyValuePair<string, double>("Chem-Light Battery (Rare)", HeldRules.StatDead(Held("Mana Potion"), "AbilityCooldown", out say) ? HeldRules.DeadCard : Synergy.StatCard(Synergy.RarityWeight("Rare", 2.5), K.MilitaryStat["AbilityCooldown"], false)),
                new KeyValuePair<string, double>("Dear John (Common)", Synergy.StatCard(Synergy.RarityWeight("Common", double.NaN), luck * ctx.Economy, false)),
                new KeyValuePair<string, double>("Max Pain (Common)", Synergy.StatCard(Synergy.RarityWeight("Common", double.NaN), dmg, false)),
            };
            var order = offer.OrderByDescending(kv => kv.Value).ToList();
            double live = Synergy.StatCard(Synergy.RarityWeight("Rare", 2.5), K.MilitaryStat["AbilityCooldown"], false);
            Check("MA3", "a military offer at 02:00 with Mana Potion held: Chem-Light Battery 0.80 and ranked last (" + string.Join(", ", order.Select(kv => kv.Key + " " + F(kv.Value))) + "; " + F(live) + " without it)",
                Near(offer[0].Value, 0.80) && order.Last().Key.StartsWith("Chem-Light Battery") && live > 1);
        }

        static void DeadWords()
        {
            string[] say; HeldRules.StatDead(Held("Mana Potion"), "AbilityCooldown", out say);
            var w = new CardWords { Kind = SayKind.Stat, Stat = Knowledge.StatLabel("AbilityCooldown") ?? "ability cooldown", Held = say };
            string at50 = Wording.Card(w, 3, "Max Pain", "Dear John", 50, null), at30 = Wording.Card(w, 3, "Max Pain", "Dear John", 30, null);
            Check("MA4", "the dead stat card's line: 'Does nothing while you hold Mana Potion' at 50, 'Mana Potion switches it off' at 30 (every rarity: the card returns before its rarity)",
                at50 == "Does nothing while you hold Mana Potion" && at30 == "Mana Potion switches it off", at50 + " / " + at30);
            var lines = new List<string>();
            foreach (var row in ItemBook.All.Where(r => r.ManaLead != null))
            {
                string a = Drawn(row.ManaLead, 50), b = Drawn(row.ManaLead, 30);
                lines.Add(row.Name + ": '" + a + "' / '" + b + "'");
                if (a.Length > 50 || b.Length > 30 || Wording.Rails(a).Count > 0 || Wording.Rails(b, 30).Count > 0) lines.Add("over the rails");
            }
            Check("MA4", "the cooldown items' lines at 50 and 30", lines.Count == 2 && lines.All(l => !l.Contains("over the rails"))
                && lines[0] == "Chick Magnet: 'Its cooldown bonus does nothing with Mana Potion' / 'Cooldown part off'"
                && lines[1] == "Hyperactivity: 'Its ability cooldown part is off (Mana Potion)' / 'Mana Potion cancels part of it'", string.Join(" | ", lines));
        }

        static void Shrapnel()
        {
            var mana = Held("Mana Potion");
            Check("MA5", "Grenade Trail: Shrapnel loses " + F(HeldRules.ShrapnelCut) + " with Mana Potion held (by name or by its asset 'Grenade Trail-Infite'); not Domino, not without it; the cut never flips a build's pick (+1.0 / -0.5)",
                HeldRules.ShrapnelOff(mana, "Grenade Trail: Shrapnel", null) && HeldRules.ShrapnelOff(mana, "Grenade Trail: Renamed", "Grenade Trail-Infite")
                && !HeldRules.ShrapnelOff(mana, "Grenade Trail: Domino", "Grenade Trail-Domino") && !HeldRules.ShrapnelOff(new HeldFacts(), "Grenade Trail: Shrapnel", null)
                && HeldRules.ShrapnelCut < 1.5 && WhyText.Fits(Wording.Cap(HeldRules.ShrapnelWhy.Substring(6))));
        }

        // ================================================================ C16-01k
        static void Gain()
        {
            var rows = new[]
            {
                Tuple.Create("bases [10, 20, 30, 12] s, reduction 0", Cds(0, Four), 1.55),
                Tuple.Create("reduction 0.2", Cds(0.2, Four), 1.24),
                Tuple.Create("reduction 0.4", Cds(0.4, Four), 0.93),
                Tuple.Create("reduction 0.6", Cds(0.6, Four), 0.62),
                Tuple.Create("the same four x3 (N 12) at 0.6", Cds(0.6, Four.Concat(Four).Concat(Four).ToArray()), 0.47),
                Tuple.Create("one 12 s ability at 0 (it fires every 4 s)", Cds(0, 12), 3.16),
            };
            var got = rows.Select(r => HeldRules.ManaPotionGain(r.Item2)).ToList();
            bool all = true; for (int i = 0; i < rows.Length; i++) all &= Near(got[i], rows[i].Item3, 0.005 + 1e-9);
            Check("MK1", "the gain (casts with Mana Potion over casts now), to two decimals as research's mana_gain_out.txt: " + string.Join("; ", rows.Select((r, i) => r.Item1 + " " + got[i].ToString("0.00", IC))), all);
            Check("MK1", "no ability with a cooldown -> 0; the cooldown reduction in per cent: 0 / 40 / 60",
                HeldRules.ManaPotionGain(new List<KeyValuePair<double, double>>()) == 0 && HeldRules.ManaPotionGain(null) == 0
                && HeldRules.CdrPct(Cds(0, Four)) == 0 && HeldRules.CdrPct(Cds(0.4, Four)) == 40 && HeldRules.CdrPct(Cds(0.6, Four)) == 60);
        }

        static void CardTerm()
        {
            Func<List<KeyValuePair<double, double>>, Tuple<double, string[], string, bool>> term = cds => { double t = 2.0; string[] l; string g; bool on = HeldRules.ManaPotionCard(cds, ref t, out l, out g); return Tuple.Create(t, l, g, on); };
            var r0 = term(Cds(0, Four)); var r2 = term(Cds(0.2, Four)); var r4 = term(Cds(0.4, Four)); var r6 = term(Cds(0.6, Four)); var n0 = term(new List<KeyValuePair<double, double>>()); var nul = term(null);
            Check("MK2", "reduction 0: A tier x1.25 (2.50), 'resets an ability every 4s - 55% more casts here'", Near(r0.Item1, 2.5) && r0.Item2[0] == "resets an ability every 4s - 55% more casts here" && r0.Item2[1] == "resets an ability every 4s", F(r0.Item1) + " '" + r0.Item2[0] + "'");
            Check("MK2", "0.2: the tier unchanged (2.00), no lead", r2.Item1 == 2.0 && r2.Item2 == null && r2.Item4, F(r2.Item1));
            Check("MK2", "0.4: tier x0.77 (" + F(r4.Item1) + "), 'would switch off 40% cooldown reduction'", Near(r4.Item1, 2.0 * (HeldRules.ManaPotionGain(Cds(0.4, Four)) - 0.7) / 0.3) && Near(r4.Item1 / 2.0, 0.77) && r4.Item2[0] == "would switch off 40% cooldown reduction", "'" + string.Join("' / '", r4.Item2) + "'");
            Check("MK2", "0.6: tier -1.00, 'switches off your 60% ability cooldown reduction'", r6.Item1 == -1.0 && r6.Item2[0] == "switches off your 60% ability cooldown reduction" && r6.Item2.Length == 3, "'" + string.Join("' / '", r6.Item2) + "'");
            Check("MK2", "no ability with a cooldown: tier x0.2, 'no ability with a cooldown on the squad'; cooldowns not read: no term at all",
                Near(n0.Item1, 0.4) && n0.Item2[0] == "no ability with a cooldown on the squad" && !nul.Item4 && nul.Item1 == 2.0 && nul.Item2 == null && nul.Item3 == null);
            Check("MK2", "the log's line carries the numbers: '" + r0.Item3 + "'", r0.Item3 == "Mana Potion: casts x1.55 over 4 abilities with a cooldown" && n0.Item3 == "Mana Potion: casts x0.00 over 0 abilities with a cooldown");
            var b = term(Cds(0.4, Four.Concat(Four).Concat(Four).ToArray()));
            Check("MK2", "40% over twelve abilities and 60% over four: the tier term falls under a B-tier item's (1.0): " + F(b.Item1) + " / " + F(r6.Item1) + "; 40% over four keeps " + F(r4.Item1),
                b.Item1 < 1.0 && r6.Item1 < 1.0);
        }

        static void ManaEvaluate()
        {
            var scores = new List<double>(); var lines = new List<string>(); bool reasonsClean = true, lastLog = true;
            foreach (var r in new[] { 0.0, 0.2, 0.4, 0.6 })
            {
                var h = new HeldFacts { Cooldowns = Cds(r, Four) };
                var c = Eval("Mana Potion", Tse(h));
                scores.Add(c.Score); lines.Add(c.Line);
                var w = new CardWords { Kind = SayKind.Item, Item = c.Say };
                if (WhyText.Reasons(w, c.Why, c.Line).Any(x => x.Contains("casts x") || x.Contains("Mana Potion: "))) reasonsClean = false;
                if (!c.Why.Last().StartsWith("Mana Potion: casts x")) lastLog = false;
            }
            bool monotone = scores[0] > scores[1] && scores[1] > scores[2] && scores[2] > scores[3];
            var plain = Eval("Mana Potion", Tse(null));
            Check("MK3", "Evaluate('Mana Potion') at reduction 0 / 0.2 / 0.4 / 0.6: " + string.Join(" > ", scores.Select(F)) + " (without the read " + F(plain.Score) + "): monotone in the gain, 0 keeps or raises it",
                monotone && scores[0] > plain.Score && Near(scores[1], plain.Score), string.Join(" | ", lines));
            Check("MK3", "the leads as the card's line: 'Resets an ability every 4s - 55% more casts here', the guide's line at 0.2, then the costs",
                lines[0] == "Resets an ability every 4s - 55% more casts here" && lines[1] == plain.Line && lines[2] == "Would switch off 40% cooldown reduction" && lines[3] == "Switches off your 60% ability cooldown reduction");
            Check("MK3", "'Mana Potion: casts x...' is the last log reason and never reaches the WHY band; an 'item: ' lead is the first", lastLog && reasonsClean
                && Eval("Mana Potion", Tse(new HeldFacts { Cooldowns = Cds(0.6, Four) })).Why[0] == "item: switches off your 60% ability cooldown reduction");
            var heldName = Held("Mana Potion"); heldName.Cooldowns = Cds(0.6, Four);
            var heldPre = new HeldFacts { Cooldowns = Cds(0.6, Four) }; heldPre.Pretend.Add("Mana Potion");
            var noRead = new HeldFacts { Cooldowns = null };
            double a = Eval("Mana Potion", Tse(heldName)).Score, p = Eval("Mana Potion", Tse(heldPre)).Score, n = Eval("Mana Potion", Tse(noRead)).Score;
            Check("MK3", "Mana Potion held by name or pretended, or the cooldowns not read: the card as before (" + F(plain.Score) + ")", a == plain.Score && p == plain.Score && n == plain.Score, F(a) + " / " + F(p) + " / " + F(n));
            var byAsset = Quiet(() => Eval("Potion Of Mana", Tse(new HeldFacts { Cooldowns = Cds(0.6, Four) }), "Item_ManaPotion"));
            Check("MK3", "a renamed Mana Potion is found by its asset", byAsset.Why[0] == "item: switches off your 60% ability cooldown reduction", byAsset.Why[0]);
        }

        static void ManaWords()
        {
            var bad = new List<string>(); var shown = new List<string>();
            foreach (var cds in new[] { Cds(0, Four), Cds(0.4, Four), Cds(0.6, Four), Cds(0.95, Four), new List<KeyValuePair<double, double>>() })
            {
                double t = 2; string[] lead; string log; HeldRules.ManaPotionCard(cds, ref t, out lead, out log);
                string a = Drawn(lead, 50), b = Drawn(lead, 30);
                shown.Add(a + " / " + b);
                if (a.Length > 50 || b.Length > 30 || Wording.Rails(a).Count > 0 || Wording.Rails(b, 30).Count > 0) bad.Add(a + " / " + b);
            }
            Check("MK4", "the Mana Potion card's lines at 50 and 30, on the rails: " + string.Join(" | ", shown), bad.Count == 0
                && shown[0] == "Resets an ability every 4s - 55% more casts here / Resets an ability every 4s" && shown[2] == "Switches off your 60% ability cooldown reduction / Costs your cooldown reduction", string.Join(" | ", bad));
        }

        // ================================================================ C16-01e / b / d / f (lane CH2): the skip, the rescue cards, the cash items
        /// <summary>A screen as the spec's cases build it (like HintCases.In): the skip +675 cash +292 health, full health, Survival 1.0, Cash
        /// 0.32 (a logged 'skip +675 cash, +292 health').</summary>
        static ScreenCallIn Sk(string screen, double progress, int rerolls, params double[] scores)
        {
            var x = new ScreenCallIn { Screen = screen, Progress = progress, CanReroll = rerolls > 0, Rerolls = rerolls, RerollsText = rerolls + " (bench)", CanSkip = true, SkipCash = 675, SkipHeal = 292, Health = 1.0, Survival = 1.0, Cash = 0.32, Economy = 1.36 };
            string[] names = { "A", "B", "C", "D" };
            for (int i = 0; i < scores.Length; i++) x.Cards.Add(new HintCard { Name = names[i], Score = scores[i], Rank = i + 1, Kind = "item" });
            return x;
        }
        /// <summary>The held items' fields as RerollHint.Inputs0 fills them.</summary>
        static ScreenCallIn With(ScreenCallIn x, HeldFacts h)
        {
            x.SkipLevelUps = HeldRules.SkipLevelUps(h, x.Screen); x.CashHeals = HeldRules.CashHeals(h); x.CashAlsoOnPick = HeldRules.CashAlsoOnPick(h, x.Screen);
            x.ExtraHeal = HeldRules.SkipExtraHeal(h, x.Screen); x.SkipBonus = HeldRules.SkipRopeWorth(h, x.Screen, x.Economy); x.SkipBy = HeldRules.SkipBy(h, x.Screen);
            x.RopePoints = HeldRules.SkipRopePoints(h, x.Screen); x.RopeReq = HeldRules.TrainingReq(h);
            return x;
        }
        static string Said(ScreenCall c) { return (c.Show ? ScreenCall.Name(c.Action) + " '" + c.Words + "'" : "silent") + " (worth " + F(c.SkipValue) + ") | " + c.Why; }
        static bool Same(ScreenCall a, ScreenCall b) { return a.Show == b.Show && a.Action == b.Action && a.Words == b.Words && a.Why == b.Why && a.SkipValue == b.SkipValue; }
        static string Rescue(RecruitSay r, int room) { return Wording.Card(new CardWords { Kind = SayKind.Recruit, Recruit = r }, 2, "Tank", "SWAT", room, null); }
        static readonly List<string> _hintWords = new List<string>();        // every action line of these cases (the rails, Ch2Rails)
        static ScreenCall D(ScreenCallIn x) { var c = ScreenCall.Decide(x); if (c.Words != null) _hintWords.Add(c.Words); return c; }

        /// <summary>0.15.0's SkipWorth, kept here to pin every old call: the heal while the squad is hurting, the cash as the goal values it.</summary>
        static double OldSkipWorth(int cash, int heal, double health, double survival, double cashWeight)
        {
            double h = health < 0 ? 0 : health > 1 ? 1 : health;
            double missing = Math.Max(0, Math.Min(0.2, 1 - h)) / 0.2;
            double healPart = heal > 0 ? 1.5 * missing * Math.Max(0, survival) : 0;
            double cashPart = cash > 0 ? 0.5 * Math.Max(0, cashWeight) : 0;
            return Math.Round(healPart + cashPart, 2);
        }

        // ---------------------------------------------------------------- C16-01e Life Savings
        static void LsWorth()
        {
            var rows = new[]
            {
                Tuple.Create("(675, 292, health 0.6, survival 1.3, cash 0.32): 3.00 (2.11 today)", ScreenCall.SkipWorth(675, 292, 0.6, 1.3, 0.32, cashHeals: true), 3.00, ScreenCall.SkipWorth(675, 292, 0.6, 1.3, 0.32), 2.11),
                Tuple.Create("full health: 0.00 (0.16 today)", ScreenCall.SkipWorth(675, 292, 1.0, 1.3, 0.32, cashHeals: true), 0.00, ScreenCall.SkipWorth(675, 292, 1.0, 1.3, 0.32), 0.16),
                Tuple.Create("health 0.85, survival 0.82: 0.92 (1.08 today)", ScreenCall.SkipWorth(675, 292, 0.85, 0.82, 0.32, cashHeals: true), 0.92, ScreenCall.SkipWorth(675, 292, 0.85, 0.82, 0.32), 1.08),
                Tuple.Create("with Hijacked Signal on a chest (a pick heals the same): 1.95", ScreenCall.SkipWorth(675, 292, 0.6, 1.3, 0.32, cashHeals: true, cashAlsoOnPick: true), 1.95, 1.95, 1.95),
                Tuple.Create("no held item at survival 2.4: 3.76 old and new (the cap max(3, 1.5 x survival) never binds the default path)", ScreenCall.SkipWorth(675, 292, 0.6, 2.4, 0.32), 3.76, OldSkipWorth(675, 292, 0.6, 2.4, 0.32), 3.76),
            };
            foreach (var r in rows) Check("LS1", "SkipWorth " + r.Item1, Near(r.Item2, r.Item3, 1e-9) && Near(r.Item4, r.Item5, 1e-9), F(r.Item2) + " / " + F(r.Item4));
            Check("LS1", "a hurt squad's skip with Life Savings is worth up to 3.0 (health 0.2, survival 1.3), a full-health skip 0",
                Near(ScreenCall.SkipWorth(675, 292, 0.2, 1.3, 0.32, cashHeals: true), 3.0, 1e-9) && ScreenCall.SkipWorth(675, 292, 1.0, 2.0, 0.9, cashHeals: true) == 0);
        }

        static void LsOldCalls()
        {
            int n = 0; var diffs = new List<string>();
            foreach (int cash in new[] { 0, 120, 300, 675, 705 })
                foreach (int heal in new[] { 0, 40, 200, 292, 320 })
                    foreach (double hp in new[] { -0.1, 0, 0.2, 0.5, 0.55, 0.6, 0.62, 0.79, 0.8, 0.85, 0.9, 0.95, 1.0, 1.2 })
                        foreach (double sv in new[] { -0.5, 0, 0.5, 0.82, 0.95, 0.96, 1.0, 1.3, 1.4, 2.4, 3.0 })
                            foreach (double cw in new[] { 0, 0.15, 0.3, 0.32, 0.4 })
                            {
                                n++;
                                double a = ScreenCall.SkipWorth(cash, heal, hp, sv, cw), b = OldSkipWorth(cash, heal, hp, sv, cw), c = ScreenCall.SkipWorth(cash, heal, hp, sv, cw, 0, false, false);
                                if (a != b || c != b) diffs.Add(cash + "/" + heal + "/" + hp + "/" + sv + "/" + cw + ": " + F(a) + " vs " + F(b));
                            }
            Check("LS2", n + " calls without the held arguments: SkipWorth equals 0.15.0's to the hundredth (HintCases' Skips() rerun in the full bench)", diffs.Count == 0, string.Join("; ", diffs.Take(5)));
        }

        static void LsHint()
        {
            var ls = Held("Life Savings");
            var hurt = With(Sk("Chest", 0.6, 0, 1.3, 1.1), ls); hurt.Health = 0.6; hurt.Survival = 1.3;
            var h = D(hurt);
            var plain = Sk("Chest", 0.6, 0, 1.3, 1.1); plain.Health = 0.6; plain.Survival = 1.3; var p = D(plain);
            Check("LS3", "weak cards at 60 % with Life Savings: SKIP 'the heal is worth more than these cards' worth 3.00 (no cash: it heals); without it the heal and the cash, 2.11",
                h.Show && h.Action == HintAction.Skip && h.Words == "the heal is worth more than these cards" && Near(h.SkipValue, 3.0, 1e-9) && p.Words == "the heal and the cash are worth more than these cards" && Near(p.SkipValue, 2.11, 1e-9), Said(h));
            var full = D(With(Sk("Chest", 0.6, 0, 1.3, 1.1), ls));
            Check("LS3", "at full health the skip is worth 0 with Life Savings: silent", !full.Show && full.SkipValue == 0, Said(full));
            Check("LS3", "the action hint's log after '(worth X)': ' (Life Savings: the cash heals)'; nothing without held items", ScreenCall.SkipNote(hurt) == " (Life Savings: the cash heals)" && ScreenCall.SkipNote(plain) == "" && ScreenCall.SkipNote(null) == "", ScreenCall.SkipNote(hurt));
        }

        static void LsItems()
        {
            var ls = Held("Life Savings");
            var ctx = Tse(null).Ctx;
            Check("LS4", "the 02:00 context (Tank + SWAT + Engineer, Normal 20:00): Survival " + F(ctx.Survival) + ", Cash " + F(ctx.Cash) + " (the spec's 0.85 / 0.47)", Near(ctx.Survival, 0.85, 0.005) && Near(ctx.Cash, 0.47, 0.005));
            foreach (var name in new[] { "Quick Buck", "Farming Tools" })
            {
                var a = Eval(name, Tse(new HeldFacts())); var b = Eval(name, Tse(ls));
                double want = 0.6 * 0.4 * (ctx.Survival - ctx.Cash);
                Check("LS4", name + " with Life Savings held: its cash keyword weighed by Survival instead of Cash (" + F(a.Score) + " -> " + F(b.Score) + "), the card says 'Its cash heals you instead (Life Savings)'",
                    Near(b.Score - a.Score, want, 0.001) && b.Line == "Its cash heals you instead (Life Savings)" && b.Why.Contains(HeldRules.CashHealWhy) && !a.Why.Contains(HeldRules.CashHealWhy), "'" + a.Line + "' -> '" + b.Line + "'");
            }
            var pre = new HeldFacts(); pre.Pretend.Add("Life Savings");
            Check("LS4", "the same with Life Savings pretended ([Debug] HeldPretend)", Eval("Quick Buck", Tse(pre)).Score == Eval("Quick Buck", Tse(ls)).Score);
            var qb = Eval("Quick Buck", Tse(ls));
            var band = WhyText.Reasons(new CardWords { Kind = SayKind.Item, Item = qb.Say }, qb.Why, qb.Line);
            Check("LS4", "Quick Buck's WHY band with Life Savings never repeats the card's line ('" + string.Join(" / ", band) + "')", !band.Contains(qb.Line) && band.Count <= WhyText.MaxReasons);
            // the partial cash gains: a WHY reason, the line unchanged; every book row that moves is one with a cash gain
            var moved = new List<string>(); var partial = new List<string>(); bool partialOk = true;
            int cashIdx = Array.FindIndex(ItemRules.All, r => r.Tag == "cash");
            var withCash = ItemBook.All.Where(r => r.GainW != null && cashIdx >= 0 && r.GainW[cashIdx] > 0).Select(r => r.Name).ToList();
            foreach (var row in ItemBook.All)
            {
                var a = Eval(row.Name, Tse(new HeldFacts())); var b = Eval(row.Name, Tse(ls));
                if (a.Score != b.Score || a.Line != b.Line) moved.Add(row.Name);
                if (withCash.Contains(row.Name) && row.Name != "Quick Buck" && row.Name != "Farming Tools")
                {
                    partial.Add(row.Name);
                    if (a.Line != b.Line || !b.Why.Contains(HeldRules.CashHealWhy)) partialOk = false;
                }
            }
            Check("LS4", "the items whose rule gains cash move, and only they (" + string.Join(", ", moved) + "); " + string.Join(", ", partial) + ": a WHY reason only, the line stays",
                moved.OrderBy(x => x).SequenceEqual(withCash.OrderBy(x => x)) && partialOk && new[] { "Quick Buck", "Farming Tools", "Emerald Gem", "Access Keycard", "Black Box", "Hijacked Signal" }.All(withCash.Contains),
                "rows with a cash gain: " + string.Join(", ", withCash));
        }

        static void LsLiberate()
        {
            var lines = new List<string>();
            Func<RecruitSay> lib = () => new RecruitSay { Liberate = true, CashHeals = true };
            var r0 = lib(); var rf = lib(); rf.Full = true; var rl = lib(); rl.Late = "1:10 left";
            string a50 = Rescue(r0, 50), a30 = Rescue(r0, 30), f50 = Rescue(rf, 50), f30 = Rescue(rf, 30), l50 = Rescue(rl, 50), l30 = Rescue(rl, 30);
            Check("LS5", "Liberate with Life Savings: 'A level-up and a heal instead of a recruit' / 'A level-up and a heal'; full 'Squad is full - take the level-up and a heal'; late 'Too late for a recruit - a level-up and a heal'",
                a50 == "A level-up and a heal instead of a recruit" && a30 == "A level-up and a heal" && f50 == "Squad is full - take the level-up and a heal" && f30 == "The level-up and a heal"
                && l50 == "Too late for a recruit - a level-up and a heal" && l30 == "A level-up and a heal", a50 + " / " + a30 + " | " + f50 + " / " + f30 + " | " + l50 + " / " + l30);
            var plain = new RecruitSay { Liberate = true };
            Check("LS5", "without the item the 0.15.0 words: 'A level-up and cash instead of a recruit'", Rescue(plain, 50) == "A level-up and cash instead of a recruit" && Rescue(new RecruitSay { Liberate = true, Full = true }, 50) == "Squad is full - take the level-up and cash");
            foreach (var s in new[] { a50, f50, l50 }) if (Wording.Rails(s).Count > 0) lines.Add(s + " (" + string.Join(", ", Wording.Rails(s)) + ")");
            foreach (var s in new[] { a30, f30, l30 }) if (Wording.Rails(s, 30).Count > 0) lines.Add(s + " at 30 (" + string.Join(", ", Wording.Rails(s, 30)) + ")");
            Check("LS5", "on the rails at 50 and 30", lines.Count == 0, string.Join(" | ", lines));
        }

        static void LsKeyword()
        {
            string at50, at30;
            Wording.Ambient(50, null); try { at50 = Wording.Item(new ItemSay { Keyword = "cash", KeywordAxis = "heal", KeywordValue = 0.85, Clock = "18:00 left" }); } finally { Wording.Ambient(0, null); }
            Wording.Ambient(30, null); try { at30 = Wording.Item(new ItemSay { Keyword = "cash", KeywordAxis = "heal", KeywordValue = 0.85, Clock = "18:00 left" }); } finally { Wording.Ambient(0, null); }
            Check("LS6", "the keyword reading's words for the cash rule with Life Savings: 'Its cash heals you instead (Life Savings)' at 50, '" + at30 + "' at 30",
                at50 == "Its cash heals you instead (Life Savings)" && at30.Length <= 30 && Wording.Rails(at30, 30).Count == 0 && at30.StartsWith("Its cash heals you"), at50 + " / " + at30);
            bool was = ItemRules.UseBook; ItemSay say; var why = new List<string>(); double s0, s1;
            try
            {
                ItemRules.UseBook = false;          // [Debug] ItemKeywords = true: no book row, the text's keywords
                var c0 = Tse(new HeldFacts()); c0.Say = new ItemSay(); s0 = ItemRules.Evaluate("Quick Buck", "Cash pickups grant a burst of movement speed.", c0, new List<string>());
                var c = Tse(Held("Life Savings")); c.Say = new ItemSay(); s1 = ItemRules.Evaluate("Quick Buck", "Cash pickups grant a burst of movement speed.", c, why); say = c.Say;
            }
            finally { ItemRules.UseBook = was; }
            Check("LS6", "the keyword reading ([Debug] ItemKeywords): the cash rule weighed as healing, its axis 'heal', the held reason; no card lead without a book row",
                s1 > s0 && say.Keyword == "cash" && say.KeywordAxis == "heal" && why.Contains(HeldRules.CashHealWhy) && say.LeadForms == null, F(s0) + " -> " + F(s1));
        }

        static void LsPart()
        {
            string l = HeldRules.Line(Held("Life Savings"), null);
            Check("LS7", "the [held] part: 'Life Savings: cash heals instead'", l == "[held] Life Savings: cash heals instead", l);
            Check("LS7", "CashHeals by name, asset, pretend; not without", HeldRules.CashHeals(Held("Life Savings")) && !HeldRules.CashHeals(new HeldFacts()) && !HeldRules.CashHeals(null)
                && HeldRules.CashHeals(new Func<HeldFacts>(() => { var h = new HeldFacts(); h.Assets.Add("Item_LifeSavings"); return h; })()));
        }

        // ---------------------------------------------------------------- C16-01b Reserve Bench
        static void RbSkip()
        {
            var rb = Held("Reserve Bench");
            var i = D(With(Sk("Chest", 0.3, 6, 2.37, 2.10, 1.90), rb));
            Check("RB1", "(i) a chest at 2.37 with 6 rerolls: REROLL first (Q35's default, no answer yet); its Why ends ' | Reserve Bench: if it stays weak, the skip is a level-up'",
                i.Show && i.Action == HintAction.Reroll && i.Why.EndsWith(" | Reserve Bench: if it stays weak, the skip is a level-up"), Said(i));
            var i1 = D(With(Sk("Chest", 0.3, 0, 2.37, 2.10, 1.90), rb));
            Check("RB1", "(i') the same with no reroll left: SKIP 'the skip is a level-up (Reserve Bench) - worth more than these' (62), worth 2.66 = 0.16 + 2.50",
                i1.Show && i1.Action == HintAction.Skip && i1.Words == "the skip is a level-up (Reserve Bench) - worth more than these" && i1.Words.Length == 62 && i1.Words.Length <= ScreenCall.Budget && Near(i1.SkipValue, 2.66, 1e-9)
                && i1.Why == "best A 2.37 under the chest floor 2.50 - no reroll left (0 (bench)) | Reserve Bench: the skip is a level-up, worth 2.66 (+675 cash, +292 health)", Said(i1));
            var off = With(Sk("Chest", 0.3, 6, 2.37, 2.10, 1.90), rb); off.Reroll = false; var i2 = D(off);
            Check("RB1", "(i'') 6 rerolls but reroll hints off: SKIP", i2.Show && i2.Action == HintAction.Skip && i2.Why.Contains("reroll hints off"), Said(i2));
            var ii = D(With(Sk("Chest", 0.3, 0, 2.60, 2.10), rb));
            Check("RB1", "(ii) a chest at 2.60 (over the floor), no reroll: no hint", !ii.Show, Said(ii));
            var m = D(With(Sk("Military", 0.5, 0, 1.90, 1.60), rb)); var m2 = D(With(Sk("Military", 0.5, 0, 2.40, 1.60), rb));
            Check("RB1", "(iii) a training at 1.90, no reroll: SKIP worth 2.16 (0.16 + the floor 2.00); at 2.40 no hint", m.Show && m.Action == HintAction.Skip && Near(m.SkipValue, 2.16, 1e-9) && !m2.Show, Said(m) + " || " + Said(m2));
            var same = new List<string>();
            foreach (var screen in new[] { "LevelUp", "Hashtag" })
                foreach (var sc in new[] { new[] { 3.0, 2.0 }, new[] { 1.3, 1.1 }, new[] { 0.8, 0.5 }, new[] { 2.4 } })
                    foreach (int rr in new[] { 0, 4 })
                        foreach (double hp in new[] { 1.0, 0.6 })
                        {
                            var a = Sk(screen, 0.3, rr, sc); a.Health = hp; var b = With(Sk(screen, 0.3, rr, sc), rb); b.Health = hp;
                            if (b.SkipLevelUps != 0 || !Same(ScreenCall.Decide(a), ScreenCall.Decide(b))) same.Add(screen + " " + string.Join("/", sc) + " rr " + rr + " hp " + hp);
                        }
            Check("RB1", "(iv) a level-up or a Research Pod: no level-up on the skip (events 21 / 40), every decision as without the item", same.Count == 0 && HeldRules.SkipLevelUps(rb, "LevelUp") == 0 && HeldRules.SkipLevelUps(rb, "Hashtag") == 0 && HeldRules.SkipLevelUps(rb, "SOS") == 0
                && HeldRules.SkipLevelUps(rb, "Chest") == 1 && HeldRules.SkipLevelUps(rb, "Military") == 1, string.Join(", ", same));
            var noSkip = With(Sk("Chest", 0.3, 0, 2.37), rb); noSkip.Skip = false; var noBtn = With(Sk("Chest", 0.3, 0, 2.37), rb); noBtn.CanSkip = false;
            Check("RB1", "(v) the Skip action off or no Skip button: no SKIP", !D(noSkip).Show && !D(noBtn).Show);
            Check("RB1", "the action hint's log after '(worth X)': ', a level-up (Reserve Bench)'", ScreenCall.SkipNote(With(Sk("Chest", 0.3, 0, 2.37), rb)) == ", a level-up (Reserve Bench)");
        }

        static void RbRecruit()
        {
            Func<double, HeldFacts> at = t => { var h = Held("Reserve Bench"); h.SinceJoin = t; return h; };
            var pre = new HeldFacts { SinceJoin = 371 }; pre.Pretend.Add("Reserve Bench");
            Check("RB2", "(vi) RecruitLevelUps: 371 s -> 3, 119 s -> 0, not read -> 0, not held -> 0, pretended -> 3",
                HeldRules.RecruitLevelUps(at(371)) == 3 && HeldRules.RecruitLevelUps(at(119)) == 0 && HeldRules.RecruitLevelUps(at(120)) == 1 && HeldRules.RecruitLevelUps(at(-1)) == 0 && HeldRules.RecruitLevelUps(at(double.NaN)) == 0
                && HeldRules.RecruitLevelUps(new HeldFacts { SinceJoin = 371 }) == 0 && HeldRules.RecruitLevelUps(pre) == 3 && HeldRules.RecruitLevelUps(null) == 0);
            double v = 0.3, aTier = 2 * v + 1.1 * (0.35 + 0.65 * v), lib = HeldRules.LiberateScore(v, 0, false, 1);
            int n; double held = HeldRules.RecruitHeld(aTier, at(371), out n);
            Check("RB2", "(vii) recruit value 0.3: an A-tier recruit " + F(aTier) + " against Liberate " + F(lib) + "; with 3 level-ups " + F(held) + " beats it",
                Near(aTier, 1.20, 0.005) && Near(lib, 3.24, 1e-9) && n == 3 && Near(held, 4.20, 0.005) && held > lib);
            int n2; double other = 0.95, other2 = HeldRules.RecruitHeld(other, at(371), out n2);
            Check("RB2", "(vii) two recruits keep their order and their gap (the same level-ups for every recruit)", n2 == n && held > other2 && Near(held - other2, aTier - other, 1e-9));
            Func<string, double, Recruit> r = (nm, s) => new Recruit { Name = nm, Score = s, Class = nm.Length };
            var late = RerollCall.Decide(new[] { r("Ranger", held) }, lib, new[] { r("Huntress", held + 1.3) }, true, "4", v);
            var lifted = RerollCall.Decide(new[] { r("Ranger", held) }, lib, new[] { r("Huntress", held + 1.3) }, true, "4", Math.Max(v, RerollCall.Late));
            Check("RB2", "(vii) the rescue reroll hint with the lifted recruit value " + F(Math.Max(v, RerollCall.Late)) + " no longer stops at 'late in the run'",
                late.Why.StartsWith("late in the run") && !lifted.Why.StartsWith("late in the run") && lifted.Show, lifted.Why);
            Check("RB2", "the reasons: 'held: recruiting gives 3 level-ups (Reserve Bench)', 'held: recruiting gives 1 level-up (Reserve Bench)', Liberate's 'held: a recruit brings 2 level-ups now (Reserve Bench)'",
                HeldRules.RecruitWhy(3) == "held: recruiting gives 3 level-ups (Reserve Bench)" && HeldRules.RecruitWhy(1) == "held: recruiting gives 1 level-up (Reserve Bench)" && HeldRules.RecruitNowWhy(2) == "held: a recruit brings 2 level-ups now (Reserve Bench)");
        }

        static void RbWords()
        {
            var shown = new List<string>(); var bad = new List<string>();
            foreach (int n in new[] { 1, 3, 12 })
                foreach (int room in new[] { 50, 30 })
                {
                    string s = Rescue(new RecruitSay { Late = "1:10 left", Tier = "A", JoinLevelUps = n }, room);
                    shown.Add(room + ": " + s);
                    if (s.Length > room || Wording.Rails(s, room).Count > 0 || !s.StartsWith(n + " level-") && !s.StartsWith("Recruiting gives " + n)) bad.Add(room + ": " + s);
                }
            Check("RB3", "(viii) the late recruit's line: " + string.Join(" | ", shown), bad.Count == 0 && shown[0] == "50: Recruiting gives 1 level-up (Reserve Bench)" && shown[4] == "50: Recruiting gives 12 level-ups (Reserve Bench)" && shown[5] == "30: Recruiting gives 12 level-ups", string.Join(" | ", bad));
            string early = Rescue(new RecruitSay { Tier = "A", JoinLevelUps = 3 }, 50), lateLib = Rescue(new RecruitSay { Liberate = true }, 50);
            Check("RB3", "earlier in the run it stays a WHY reason (every recruit would draw it): the card keeps its own line; Liberate's line without 'too late'",
                early == "A-tier recruit in the guides" && lateLib == "A level-up and cash instead of a recruit", early);
            var h = Held("Reserve Bench"); h.SinceJoin = 250;
            var u = Held("Reserve Bench");
            string l = HeldRules.Line(h, null), lu = HeldRules.Line(u, null);
            Check("RB3", "the [held] part: '" + l + "'", l == "[held] Reserve Bench: a chest or training skip is a level-up; recruiting gives 2 (4:10 since the last join)"
                && lu == "[held] Reserve Bench: a chest or training skip is a level-up; recruiting gives ? (the time since the last join not read)", lu);
        }

        // ---------------------------------------------------------------- C16-01d Hijacked Signal
        static double ATier(double v) { return 2 * v + 1.1 * (0.35 + 0.65 * v); }
        static double Crossover(int k)
        {
            double lo = 0, hi = 1;
            for (int i = 0; i < 60; i++) { double m = (lo + hi) / 2; if (ATier(m) - HeldRules.LiberateScore(m, 0, false, k) < 0) lo = m; else hi = m; }
            return (lo + hi) / 2;
        }

        static void HsScore()
        {
            var hs = Held("Hijacked Signal");
            Check("HS1", "LiberateLevelUps: 2 with Hijacked Signal (name, pretend), 1 without",
                HeldRules.LiberateLevelUps(hs) == 2 && HeldRules.LiberateLevelUps(new HeldFacts()) == 1 && HeldRules.LiberateLevelUps(null) == 1
                && HeldRules.LiberateLevelUps(new Func<HeldFacts>(() => { var p = new HeldFacts(); p.Pretend.Add("Hijacked Signal"); return p; })()) == 2);
            Check("HS1", "LiberateScore: (1.0) 2.00 with two level-ups (1.00 with one); (0.5) 3.60 (2.60); a full squad 5 either way",
                Near(HeldRules.LiberateScore(1.0, 0, false, 2), 2.0, 1e-9) && Near(HeldRules.LiberateScore(1.0, 0, false, 1), 1.0, 1e-9) && Near(HeldRules.LiberateScore(0.5, 0, false, 2), 3.6, 1e-9)
                && Near(HeldRules.LiberateScore(0.5, 0, false, 1), 2.6, 1e-9) && HeldRules.LiberateScore(0.5, 0, true, 2) == 5 && HeldRules.LiberateScore(0.5, 0, true, 1) == 5);
            double c1 = Crossover(1), c2 = Crossover(2);
            Check("HS1", "the crossover against an A-tier recruit (2v + 1.1 x (0.35 + 0.65v)): recruit value " + c1.ToString("0.000", IC) + " without the item, " + c2.ToString("0.000", IC) + " with it (about "
                + (12 * c1).ToString("0.0", IC) + " and " + (12 * c2).ToString("0.0", IC) + " level-ups to come; the spec's 0.65 / 0.81, 7.7 / 9.8)",
                Math.Abs(c1 - 0.65) < 0.006 && Math.Abs(c2 - 0.81) < 0.005 && Math.Round(12 * c1, 1) == 7.7 && Math.Round(12 * c2, 1) == 9.8);
            Check("HS1", "the card's reason: 'held: two level-ups and cash (Hijacked Signal)' ('a heal' with Life Savings); none with one level-up",
                HeldRules.LiberateWhy(2, false) == "held: two level-ups and cash (Hijacked Signal)" && HeldRules.LiberateWhy(2, true) == "held: two level-ups and a heal (Hijacked Signal)" && HeldRules.LiberateWhy(1, false) == null && HeldRules.LiberateWhy(1, true) == null);
        }

        static void HsPlan()
        {
            Check("HS2", "PlanSaysLiberate: late -> yes (the clock rule); two level-ups with Liberate 3.60 over the best recruit 3.20 -> yes; one level-up with the same numbers -> no (0.15.0's row); two under the recruit -> no",
                HeldRules.PlanSaysLiberate(true, 1, 5.0, 1.0) && HeldRules.PlanSaysLiberate(false, 2, 3.20, 3.60) && !HeldRules.PlanSaysLiberate(false, 1, 3.20, 3.60) && !HeldRules.PlanSaysLiberate(false, 2, 3.70, 3.60)
                && !HeldRules.PlanSaysLiberate(false, 2, 3.60, 3.60));
        }

        static void HsChest()
        {
            var hs = Held("Hijacked Signal");
            Check("HS3", "SkipWorth(675, 292, full health, 0.82, 0.32) with the chest item's cash: 0.00 (0.16 without)",
                ScreenCall.SkipWorth(675, 292, 1.0, 0.82, 0.32, cashAlsoOnPick: true) == 0 && Near(ScreenCall.SkipWorth(675, 292, 1.0, 0.82, 0.32), 0.16, 1e-9));
            Check("HS3", "CashAlsoOnPick: Hijacked Signal on a chest only", HeldRules.CashAlsoOnPick(hs, "Chest") && !HeldRules.CashAlsoOnPick(hs, "LevelUp") && !HeldRules.CashAlsoOnPick(hs, "Military") && !HeldRules.CashAlsoOnPick(new HeldFacts(), "Chest"));
            var hurt = With(Sk("Chest", 0.6, 0, 1.3, 1.1), hs); hurt.Health = 0.6; hurt.Survival = 1.3; var h = D(hurt);
            var lv = With(Sk("LevelUp", 0.6, 0, 1.3, 1.1), hs); lv.Health = 0.6; lv.Survival = 1.3; var l = D(lv);
            Check("HS3", "a hurt chest's skip with Hijacked Signal: the heal alone (1.95), 'the heal is worth more than these cards'; a level-up keeps the cash (2.11)",
                h.Show && Near(h.SkipValue, 1.95, 1e-9) && h.Words == "the heal is worth more than these cards" && Near(l.SkipValue, 2.11, 1e-9) && l.Words == "the heal and the cash are worth more than these cards"
                && ScreenCall.SkipNote(hurt) == ", the cash also on a pick (Hijacked Signal)", Said(h) + " || " + Said(l));
        }

        static void HsWords()
        {
            Func<bool, bool, string, RecruitSay> lib = (full, heal, late) => new RecruitSay { Liberate = true, Full = full, Late = late, LiberateLevelUps = 2, CashHeals = heal };
            var got = new List<string>(); var bad = new List<string>();
            var want = new[]
            {
                Tuple.Create(lib(false, false, null), "Two level-ups and cash (Hijacked Signal)", "Two level-ups and cash"),
                Tuple.Create(lib(true, false, null), "Squad is full - take two level-ups and cash", "Two level-ups and cash"),
                Tuple.Create(lib(false, false, "1:10 left"), "Too late for a recruit - two level-ups and cash", "Two level-ups and cash"),
                Tuple.Create(lib(false, true, null), "Two level-ups and a heal (Hijacked Signal)", "Two level-ups and a heal"),
                Tuple.Create(lib(true, true, null), "Squad is full - take two level-ups and a heal", "Two level-ups and a heal"),
                Tuple.Create(lib(false, true, "1:10 left"), "Too late for a recruit - two level-ups and a heal", "Two level-ups and a heal"),
            };
            foreach (var w in want)
            {
                string a = Rescue(w.Item1, 50), b = Rescue(w.Item1, 30);
                got.Add(a + " / " + b);
                if (a != w.Item2 || b != w.Item3 || Wording.Rails(a).Count > 0 || Wording.Rails(b, 30).Count > 0) bad.Add(a + " / " + b);
            }
            Check("HS4", "Liberate's line with Hijacked Signal (and Life Savings) at 50 / 30: " + string.Join(" | ", got), bad.Count == 0, string.Join(" | ", bad));
            string l = HeldRules.Line(Held("Hijacked Signal"), null);
            Check("HS4", "the [held] part: 'Hijacked Signal: Liberate gives two level-ups; chest items pay cash'", l == "[held] Hijacked Signal: Liberate gives two level-ups; chest items pay cash", l);
        }

        // ---------------------------------------------------------------- C16-01f A Cookie, Skip Rope
        static void SrWorth()
        {
            var rope = Held("Skip Rope"); var gold = Held("Skip Rope", "Gold Medal");
            Check("SR1", "SkipRopeWorth at economy 1.36: chest 1.39, level-up 0.28, training 1.39, Research Pod 1.39; with Gold Medal a chest 1.68; at economy 0.37 a chest 1.00; not held 0",
                Near(HeldRules.SkipRopeWorth(rope, "Chest", 1.36), 1.39, 1e-9) && Near(HeldRules.SkipRopeWorth(rope, "LevelUp", 1.36), 0.28, 1e-9) && Near(HeldRules.SkipRopeWorth(rope, "Military", 1.36), 1.39, 1e-9)
                && Near(HeldRules.SkipRopeWorth(rope, "Hashtag", 1.36), 1.39, 1e-9) && Near(HeldRules.SkipRopeWorth(gold, "Chest", 1.36), 1.68, 1e-9) && Near(HeldRules.SkipRopeWorth(rope, "Chest", 0.37), 1.00, 1e-9)
                && HeldRules.SkipRopeWorth(new HeldFacts(), "Chest", 1.36) == 0 && HeldRules.SkipRopeWorth(null, "Chest", 1.36) == 0);
            Check("SR1", "Skip Rope's points: 1 on a level-up, 5 on a training, a chest, a Research Pod; Military Training Points per training 4, 3 with Gold Medal",
                HeldRules.SkipRopePoints(rope, "LevelUp") == 1 && HeldRules.SkipRopePoints(rope, "Military") == 5 && HeldRules.SkipRopePoints(rope, "Chest") == 5 && HeldRules.SkipRopePoints(rope, "Hashtag") == 5
                && HeldRules.SkipRopePoints(new HeldFacts(), "Chest") == 0 && HeldRules.TrainingReq(rope) == 4 && HeldRules.TrainingReq(gold) == 3);
        }

        static void SrCookie()
        {
            var cookie = Held("A Cookie");
            Check("SR2", "SkipWorth with A Cookie's 500: (675, 292, 0.6, 1.3, 0.32) 3.16 (2.11 plain); at health 0.85 / survival 0.82 1.08 (no gain: the plain heal covers the 15% missing)",
                Near(ScreenCall.SkipWorth(675, 292, 0.6, 1.3, 0.32, 500), 3.16, 1e-9) && Near(ScreenCall.SkipWorth(675, 292, 0.85, 0.82, 0.32, 500), 1.08, 1e-9) && Near(ScreenCall.SkipWorth(675, 292, 0.85, 0.82, 0.32), 1.08, 1e-9));
            Check("SR2", "SkipExtraHeal: 500 on a level-up, a training, a chest; none on a Research Pod (event 40 is not A Cookie's); 0 without the item",
                HeldRules.SkipExtraHeal(cookie, "LevelUp") == 500 && HeldRules.SkipExtraHeal(cookie, "Military") == 500 && HeldRules.SkipExtraHeal(cookie, "Chest") == 500
                && HeldRules.SkipExtraHeal(cookie, "Hashtag") == 0 && HeldRules.SkipExtraHeal(new HeldFacts(), "Chest") == 0);
        }

        static void SrScreens()
        {
            var rope = Held("Skip Rope"); var cookie = Held("A Cookie");
            var plain = D(Sk("Chest", 0.5, 0, 1.40, 1.10));
            var r = D(With(Sk("Chest", 0.5, 0, 1.40, 1.10), rope));
            Check("SR3", "a chest at full health whose best card is 1.40: plain skip 0.16 -> no hint; with Skip Rope (economy 1.36) worth 1.55 -> SKIP 'the skip pays a training point (Skip Rope)'",
                !plain.Show && Near(plain.SkipValue, 0.16, 1e-9) && r.Show && r.Action == HintAction.Skip && Near(r.SkipValue, 1.55, 1e-9) && r.Words == "the skip pays a training point (Skip Rope)", Said(r));
            var rr = D(With(Sk("Chest", 0.5, 4, 1.40, 1.10), rope));
            Check("SR3", "the same chest with 4 rerolls -> REROLL (the extras speak once no reroll is left)", rr.Show && rr.Action == HintAction.Reroll, Said(rr));
            var lv = D(With(Sk("LevelUp", 0.3, 0, 1.40, 1.10), rope));
            Check("SR3", "a level-up at 1.40 with Skip Rope: worth 0.44 -> no hint", !lv.Show && Near(lv.SkipValue, 0.44, 1e-9), Said(lv));
            Func<HeldFacts, ScreenCallIn> low = h => { var x = With(Sk("Chest", 0.5, 0, 1.20, 1.00), h); x.Health = 0.6; x.Survival = 0.5; return x; };
            var cp = D(low(new HeldFacts())); var cc = D(low(cookie));
            Check("SR3", "a chest at 60 % (survival 0.5) whose best is 1.20: the plain skip 0.91 -> no hint; with A Cookie 1.66 -> SKIP 'the skip heals 500 more (A Cookie) - worth more than these cards'",
                !cp.Show && Near(cp.SkipValue, 0.91, 1e-9) && cc.Show && Near(cc.SkipValue, 1.66, 1e-9) && cc.Words == "the skip heals 500 more (A Cookie) - worth more than these cards", Said(cc));
            var hurt = With(Sk("Chest", 0.6, 0, 1.30, 1.10), cookie); hurt.Health = 0.6; hurt.Survival = 1.3; var hh = D(hurt);
            Check("SR3", "where the plain skip already wins, its own words ('the heal and the cash ...'), worth 3.16 with A Cookie", hh.Show && hh.Words == "the heal and the cash are worth more than these cards" && Near(hh.SkipValue, 3.16, 1e-9), Said(hh));
            var both = D(With(Sk("Chest", 0.5, 0, 1.40, 1.10), Held("Skip Rope", "A Cookie")));
            Check("SR3", "Skip Rope and A Cookie at full health: Skip Rope decides (A Cookie heals nothing at full health), its words", both.Show && both.Words == "the skip pays a training point (Skip Rope)", Said(both));
        }

        static void SrWords()
        {
            var r = ScreenCall.Decide(With(Sk("Chest", 0.5, 0, 1.40, 1.10), Held("Skip Rope")));
            Check("SR4", "the log's Why: ' | Skip Rope: +5 points (a quarter of a Military Training) worth 1.39'", r.Why.EndsWith(" | Skip Rope: +5 points (a quarter of a Military Training) worth 1.39"), r.Why);
            var low = With(Sk("Chest", 0.5, 0, 1.20, 1.00), Held("A Cookie")); low.Health = 0.6; low.Survival = 0.5; var c = ScreenCall.Decide(low);
            Check("SR4", "... and ' | A Cookie: +500 health'", c.Why.EndsWith(" | A Cookie: +500 health"), c.Why);
            Check("SR4", "RopeNote: a quarter (4 per training), a third (Gold Medal), a level-up's twentieth / fifteenth",
                HeldRules.RopeNote(5, 4) == "+5 points (a quarter of a Military Training)" && HeldRules.RopeNote(5, 3) == "+5 points (a third of a Military Training)"
                && HeldRules.RopeNote(1, 4) == "+1 point (a twentieth of a Military Training)" && HeldRules.RopeNote(1, 3) == "+1 point (a fifteenth of a Military Training)" && HeldRules.RopeNote(0, 4) == null);
            var both = Held("Skip Rope", "A Cookie");
            Check("SR4", "SkipBy: 'A Cookie and Skip Rope' on a chest, 'Skip Rope' on a Research Pod, null without; never Reserve Bench; the log's ', with A Cookie and Skip Rope'",
                HeldRules.SkipBy(both, "Chest") == "A Cookie and Skip Rope" && HeldRules.SkipBy(both, "Hashtag") == "Skip Rope" && HeldRules.SkipBy(Held("Reserve Bench"), "Chest") == null
                && ScreenCall.SkipNote(With(Sk("Chest", 0.5, 0, 1.4), both)) == ", with A Cookie and Skip Rope");
            var lr = With(Sk("LevelUp", 0.7, 0, 1.30, 1.10), Held("Skip Rope")); lr.Health = 0.65; lr.Survival = 0.7;
            var l = D(lr);
            Check("SR4", "a level-up where Skip Rope's one point tips it: 'the skip pays a Skip Rope point - worth more than these cards' (not 'a training point')",
                l.Show && l.Words == "the skip pays a Skip Rope point - worth more than these cards" && l.Why.EndsWith("| Skip Rope: +1 point (a twentieth of a Military Training) worth 0.28"), Said(l));
            string all = HeldRules.Line(Held("Reserve Bench", "Hijacked Signal", "Life Savings", "A Cookie", "Skip Rope"), null);
            Check("SR4", "the [held] parts in the table's order: '" + all + "'",
                all == "[held] Reserve Bench: a chest or training skip is a level-up; recruiting gives ? (the time since the last join not read) | Hijacked Signal: Liberate gives two level-ups; chest items pay cash | Life Savings: cash heals instead | A Cookie: a skip heals 500 more | Skip Rope: a skip pays 5 points (1 on a level-up)");
        }

        // ---------------------------------------------------------------- CH2's rails and sources
        static void Ch2Rails()
        {
            var reasons = new List<string> { HeldRules.RecruitWhy(12), HeldRules.RecruitWhy(1), HeldRules.RecruitNowWhy(12), HeldRules.LiberateWhy(2, true), HeldRules.LiberateWhy(2, false), HeldRules.CashHealWhy };
            var badR = reasons.Where(x => !x.StartsWith(HeldRules.Prefix) || !WhyText.Fits(Wording.Cap(x.Substring(HeldRules.Prefix.Length)))).ToList();
            Check("H12", reasons.Count + " 'held: ' reasons of C16-01b / d / e fit the WHY band", badR.Count == 0, string.Join(" / ", badR));
            var badF = new List<string>();
            if (RailsAt(HeldRules.CashHealSay[0], Wording.Budget).Count > 0) badF.Add(HeldRules.CashHealSay[0]);
            string at30 = Drawn(HeldRules.CashHealSay, 30); if (at30.Length > 30 || Wording.Rails(at30, 30).Count > 0) badF.Add(at30 + " at 30");
            Check("H12", "the cash items' words on the rails at 50, as drawn at 30 ('" + at30 + "')", badF.Count == 0, string.Join(" / ", badF));
            var badH = _hintWords.Distinct().Where(l => l.Length > ScreenCall.Budget || Wording.Rails(l).Any(b => !b.StartsWith("length"))).ToList();
            Check("H12", _hintWords.Distinct().Count() + " action lines of these cases within " + ScreenCall.Budget + " characters and the card font's glyphs", badH.Count == 0 && _hintWords.Count > 10, string.Join(" | ", badH));
        }

        static void Ch2Sources()
        {
            string ranker = Src("Ranker.cs"), plan = Src("Plan.cs"), rules = Src("ItemRules.cs"), words = Src("Wording.cs"), call = Src("ScreenCall.cs"), hint = Src("RerollHint.cs");
            if (new[] { ranker, plan, rules, words, call, hint }.Any(s => s == null)) { Check("H12", "the sources are readable from the bench", false); return; }
            Check("H12", "Ranker: a recruit's 'held: recruiting gives N level-ups' (the headline late in the run), Liberate's Reserve Bench and Hijacked Signal reasons",
                ranker.Contains("string r = HeldRules.RecruitWhy(joinUps);") && ranker.Contains("if (left < 0.45) why.Insert(0, r); else why.Add(r);") && ranker.Contains("c.Why.Add(HeldRules.RecruitNowWhy(joinUps));")
                && ranker.Contains("string libWhy = HeldRules.LiberateWhy(libUps, heals);"));
            Check("H12", "Plan's SOS row asks PlanSaysLiberate with Liberate's score and the best recruit; ItemRules weighs the cash rule on the survival axis with Life Savings",
                plan.Contains("HeldRules.PlanSaysLiberate(late, libUps, ranked.Max(r => r.Score), liberate)") && rules.Contains("bool cashHeals = kw.Tag == \"cash\" && HeldRules.CashHeals(c.HeldFacts);")
                && rules.Contains("double axis = AxisOf(axisName, ctx);") && rules.Contains("why.Add(HeldRules.CashHealWhy);"));
            Check("H12", "Wording: the heal keyword, the Reserve Bench recruit line before the late line; ScreenCall: SkipWorth with the held arguments, the level-up SKIP before the all-AVOID SKIP; RerollHint logs SkipNote",
                words.Contains("if (tag == \"cash\" && axis == \"heal\")") && words.IndexOf("r.Late != null && r.JoinLevelUps >= 1", StringComparison.Ordinal) < words.IndexOf("little time left for a recruit to grow", StringComparison.Ordinal)
                && call.Contains("SkipWorth(x.SkipCash, x.SkipHeal, x.Health, x.Survival, x.Cash, x.ExtraHeal, x.CashHeals, x.CashAlsoOnPick)")
                && call.IndexOf("x.SkipLevelUps > 0 && c.Floor > 0 && merit < c.Floor", StringComparison.Ordinal) < call.IndexOf("x.CanSkip && allAvoid", StringComparison.Ordinal)
                && hint.Contains("ScreenCall.SkipNote(_gin)") && hint.Contains("x.RopePoints = HeldRules.SkipRopePoints(held, x.Screen);"));
        }

        // ================================================================ C16-01c / C16-01i / C16-01j (lane CH3)
        /// <summary>HeldFacts with <paramref name="names"/> held and <paramref name="free"/> empty item slots of 8 (-1: not read).</summary>
        static HeldFacts Free(int free, params string[] names)
        {
            var h = Held(names); h.FreeSlots = free;
            if (free >= 0) { h.Max = 8; h.Equipped = Math.Max(0, 8 - free); }
            return h;
        }

        // ---------------------------------------------------------------- C16-01c Wooden Stick / Empty Chest
        static void WsCost()
        {
            Func<HeldFacts, double, string, Tuple<double, string[], string>> cost = (h, sec, item) =>
            { var c = Tse(h, sec); string[] say; string by; double v = HeldRules.SlotCost(c, item, out say, out by); return Tuple.Create(v, say, by); };
            double e120 = Tse(null, 120).Ctx.Economy, e960 = Tse(null, 960).Ctx.Economy;
            double W = Math.Round(1.6 * e120, 2);      // 2.17: the spec's 2.18 is 1.6 x its rounded 1.36 (the clock's economy is 1.356)
            Check("WS1", "the spec's clock (Tank + SWAT + Engineer, Normal 20:00): economy " + e120.ToString("0.000", IC) + " at 02:00, " + e960.ToString("0.000", IC) + " at 16:00 (the spec's 1.36 / 0.37)", Near(e120, 1.36, 0.005) && Near(e960, 0.37, 0.005));
            var ws = cost(Free(5, "Wooden Stick"), 120, "Emerald Gem"); var ws16 = cost(Free(5, "Wooden Stick"), 960, "Emerald Gem");
            Check("WS1", "Wooden Stick held, 5 empty: 1.6 x economy = " + F(ws.Item1) + " at 02:00 (the spec's 2.18 from the rounded 1.36), " + F(ws16.Item1) + " at 16:00; its words; priced by 'Wooden Stick'",
                Near(ws.Item1, W, 1e-9) && Near(ws.Item1, 2.17, 1e-9) && Near(ws16.Item1, Math.Round(1.6 * e960, 2), 1e-9) && ws.Item2 != null && ws.Item2[0] == "fills an empty slot: -10% XP (Wooden Stick)" && ws.Item3 == "Wooden Stick",
                F(ws.Item1) + " / " + F(ws16.Item1));
            var ec = cost(Free(5, "Empty Chest"), 120, "Emerald Gem"); var ec16 = cost(Free(5, "Empty Chest"), 960, "Emerald Gem");
            Check("WS1", "Empty Chest held: 2.00 flat at 02:00 and 16:00 (Q36's default, no answer yet)",
                ec.Item1 == 2.0 && ec16.Item1 == 2.0 && ec.Item2[0] == "fills an empty slot: -10% damage (Empty Chest)" && ec.Item3 == "Empty Chest");
            var both = cost(Free(5, "Wooden Stick", "Empty Chest"), 120, "Emerald Gem");
            Check("WS1", "both held at 02:00: " + F(W) + " + 2.00 = " + F(both.Item1) + ", 'fills an empty slot: -10% XP and -10% damage', priced by 'Wooden Stick and Empty Chest'",
                Near(both.Item1, W + 2.0, 1e-9) && both.Item2[0] == "fills an empty slot: -10% XP and -10% damage" && both.Item3 == "Wooden Stick and Empty Chest");
            var noClock = new ItemContext { HeldFacts = Free(5, "Wooden Stick"), Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase) }; string[] s0; string b0;
            Check("WS1", "no clock (the bench's old entry, ctx null): economy 1 -> 1.60", HeldRules.SlotCost(noClock, "Emerald Gem", out s0, out b0) == 1.6);
            var zero = cost(Free(0, "Wooden Stick"), 120, "Emerald Gem"); var unread = cost(Free(-1, "Wooden Stick"), 120, "Emerald Gem");
            var nine = cost(Free(9, "Wooden Stick"), 120, "Emerald Gem"); var eight = cost(Free(8, "Wooden Stick"), 120, "Emerald Gem");
            var stack = cost(Free(5, "Wooden Stick", "Emerald Gem"), 120, "Emerald Gem"); var none = cost(Free(5), 120, "Emerald Gem");
            var self = cost(Free(5, "Wooden Stick"), 120, "Wooden Stick");
            Check("WS1", "no cost: no empty slot, the slots not read, 9 empty (the 0.8 cap stays), an item held already (a stack - Wooden Stick's own second copy too), neither held; 8 empty still costs",
                zero.Item1 == 0 && zero.Item2 == null && unread.Item1 == 0 && nine.Item1 == 0 && stack.Item1 == 0 && self.Item1 == 0 && none.Item1 == 0 && none.Item3 == null && eight.Item1 == W);
            var wp = cost(Free(5, "Wooden Stick"), 120, "Well Prepped"); var wp0 = cost(Free(0, "Wooden Stick"), 120, "Well Prepped");
            Check("WS1", "Well Prepped: no cost, its words 'adds the slot it takes - keeps your empty slots' (none with no empty slot)",
                wp.Item1 == 0 && wp.Item2 != null && wp.Item2[0] == "adds the slot it takes - keeps your empty slots" && wp.Item2[1] == "adds the slot it takes" && wp0.Item2 == null);
            var byAsset = new HeldFacts { FreeSlots = 5 }; byAsset.Assets.Add("Item_WoodenStick");
            var pre = new HeldFacts { FreeSlots = 5 }; pre.Pretend.Add("Wooden Stick");
            Check("WS1", "held by its asset only (a renamed item) or pretended: the same cost; SlotBy",
                cost(byAsset, 120, "Emerald Gem").Item1 == W && cost(pre, 120, "Emerald Gem").Item1 == W
                && HeldRules.SlotBy(Free(5, "Empty Chest")) == "Empty Chest" && HeldRules.SlotBy(null) == null && HeldRules.SlotBy(new HeldFacts()) == null);
        }

        static void WsCards()
        {
            var ws = Free(5, "Wooden Stick");
            Func<string, HeldFacts, double, Cell> ev = (name, h, sec) => Eval(name, Tse(h, sec));
            double W = Math.Round(1.6 * Tse(null, 120).Ctx.Economy, 2);
            // the spec's scores (2.70 / 2.34 / 4.76, scores_today.txt) were measured before the item book (C16-02) re-scored these rows: the bench
            // pins the arithmetic (net = score - cost) and the outcome (AVOID at 02:00, a WHY reason at 16:00 and on Black Box)
            var eg0 = ev("Emerald Gem", Free(5), 120); var eg = ev("Emerald Gem", ws, 120);
            Check("WS2", "Emerald Gem at 02:00, 5 empty: " + F(eg0.Score) + " -> " + F(eg.Score) + " (the spec's 2.70 -> 0.52 before the item book): AVOID, the card says the cost, the reason first",
                eg0.Score >= 1 && Near(eg.Score, eg0.Score - W, 1e-9) && eg.Score < 1 && eg.Say.LeadForms != null && eg.Line == "Fills an empty slot: -10% XP (Wooden Stick)"
                && eg.Why[0] == "held: fills an empty slot: -10% XP (Wooden Stick)" && Near(eg.Say.HeldCost, W, 1e-9), "'" + eg.Line + "' | " + eg.Why[0]);
            var eg16a = ev("Emerald Gem", Free(5), 960); var eg16 = ev("Emerald Gem", ws, 960);
            double c16 = Math.Round(1.6 * Tse(null, 960).Ctx.Economy, 2);
            Check("WS2", "Emerald Gem at 16:00: " + F(eg16a.Score) + " -> " + F(eg16.Score) + " (the spec's 2.34 -> 1.75): still over 1 - a WHY reason only, the line stays",
                Near(eg16.Score, eg16a.Score - c16, 1e-9) && eg16.Score >= 1 && eg16.Line == eg16a.Line && eg16.Say.LeadForms == null
                && eg16.Why[0] == "held: fills an empty slot: -10% XP (Wooden Stick)", "'" + eg16.Line + "'");
            var bb0 = ev("Black Box", Free(5), 120); var bb = ev("Black Box", ws, 120);
            Check("WS2", "Black Box at 02:00: " + F(bb0.Score) + " -> " + F(bb.Score) + " (the spec's 4.76 -> 2.58): a WHY reason only",
                Near(bb.Score, bb0.Score - W, 1e-9) && bb.Score >= 1 && bb.Line == bb0.Line && bb.Why[0] == "held: fills an empty slot: -10% XP (Wooden Stick)");
            var full = ev("Emerald Gem", Free(0, "Wooden Stick"), 120); var nine = ev("Emerald Gem", Free(9, "Wooden Stick"), 120);
            var stack = ev("Emerald Gem", Free(5, "Wooden Stick", "Emerald Gem"), 120); var stack0 = ev("Emerald Gem", Free(5, "Emerald Gem"), 120);
            Check("WS2", "unchanged: no empty slot, 9 empty, Emerald Gem held already (a stack takes no slot)",
                full.Score == eg0.Score && full.Line == eg0.Line && nine.Score == eg0.Score && nine.Line == eg0.Line && stack.Score == stack0.Score && stack.Line == stack0.Line && stack.Say.HeldCost == 0);
            var wp0 = ev("Well Prepped", Free(5), 120); var wp = ev("Well Prepped", ws, 120);
            Check("WS2", "Well Prepped " + F(wp0.Score) + " -> " + F(wp.Score) + " with its lead '" + wp.Line + "'",
                wp.Score == wp0.Score && wp.Line == "Adds the slot it takes - keeps your empty slots" && wp.Why[0] == "held: adds the slot it takes - keeps your empty slots" && wp.Say.HeldCost == 0);
            var ec = ev("Emerald Gem", Free(5, "Empty Chest"), 120); var ec16 = ev("Emerald Gem", Free(5, "Empty Chest"), 960);
            Check("WS2", "Empty Chest held: minus 2.00 flat (" + F(ec.Score) + " at 02:00, " + F(ec16.Score) + " at 16:00), 'Fills an empty slot: -10% damage (Empty Chest)' where it turns the card AVOID",
                Near(ec.Score, eg0.Score - 2.0, 1e-9) && Near(ec16.Score, eg16a.Score - 2.0, 1e-9) && ec.Line == "Fills an empty slot: -10% damage (Empty Chest)" && ec16.Line == "Fills an empty slot: -10% damage (Empty Chest)");
            var both = ev("Emerald Gem", Free(5, "Wooden Stick", "Empty Chest"), 120);
            Check("WS2", "both at 02:00: minus (" + F(W) + " + 2.00) = " + F(both.Score) + ", 'Fills an empty slot: -10% XP and -10% damage'",
                Near(both.Score, eg0.Score - W - 2.0, 1e-9) && both.Line == "Fills an empty slot: -10% XP and -10% damage");
            // every book row: neither held -> nothing moves; Wooden Stick held -> every row but Well Prepped and Wooden Stick's own (a stack) moves by the cost
            var moved = new List<string>(); var wrong = new List<string>();
            foreach (var row in ItemBook.All)
            {
                var a = ev(row.Name, null, 120); var n = ev(row.Name, Free(5), 120); var b = ev(row.Name, ws, 120);
                if (a.Score != n.Score || a.Line != n.Line || string.Join("|", a.Why) != string.Join("|", n.Why)) moved.Add(row.Name);
                bool exempt = row.Name == "Well Prepped" || row.Name == "Wooden Stick";
                if (exempt ? b.Score != a.Score : !Near(b.Score, a.Score - W, 1e-9)) wrong.Add(row.Name + " " + F(a.Score) + " -> " + F(b.Score));
            }
            Check("WS2", ItemBook.All.Length + " book items at 02:00: neither held (5 empty) -> no score, line or reason moves; Wooden Stick held -> each moves by exactly " + F(W) + " but Well Prepped and Wooden Stick itself",
                moved.Count == 0 && wrong.Count == 0, string.Join(", ", moved.Concat(wrong).Take(6)));
        }

        static void WsScreen()
        {
            Func<int, double[], double, ScreenCallIn> chest = (rerolls, merits, cost) =>
            {
                var x = Sk("Chest", 0.1, rerolls, merits.Select(m => Math.Round(m - cost, 2)).ToArray());
                foreach (var k in x.Cards) k.HeldCost = cost;
                x.SlotBy = cost > 0 ? "Wooden Stick" : null;
                return x;
            };
            var a = D(chest(6, new[] { 3.12, 2.60, 2.40 }, 2.18));
            Check("WS3", "a chest at 02:00, merits 3.12 / 2.60 / 2.40 and the cost 2.18 each (nets 0.94 / 0.42 / 0.22), 6 rerolls: no REROLL (merit 3.12 >= 2.50), SKIP 'an empty slot is worth more than these (Wooden Stick)'",
                a.Show && a.Action == HintAction.Skip && a.Words == "an empty slot is worth more than these (Wooden Stick)"
                && a.Why.StartsWith("best A 0.94 (3.12 before the held slot cost 2.18) at or over the chest floor 2.50") && a.Why.Contains("| every card is under 1 (AVOID) (merit 3.12 before the held slot cost 2.18); skip bonus"), Said(a));
            var b = D(chest(6, new[] { 2.30, 2.10, 1.90 }, 2.18));
            Check("WS3", "merits 2.30 / 2.10 / 1.90 with the cost, 6 rerolls: REROLL (merit 2.30 < 2.50)",
                b.Show && b.Action == HintAction.Reroll && b.Why.StartsWith("best A 0.12 (2.30 before the held slot cost 2.18) under the chest floor 2.50 | rerolls"), Said(b));
            var c0 = D(chest(0, new[] { 2.30, 2.10, 1.90 }, 2.18));
            Check("WS3", "the same with 0 rerolls: SKIP (every net under 1)", c0.Show && c0.Action == HintAction.Skip && c0.Words == "an empty slot is worth more than these (Wooden Stick)", Said(c0));
            var bothIn = chest(6, new[] { 3.12, 2.60, 2.40 }, 4.18); bothIn.SlotBy = "Wooden Stick and Empty Chest"; var bw = D(bothIn);
            Check("WS3", "both held: the form naming both is over 64 - 'an empty slot is worth more than these cards'", bw.Show && bw.Words == "an empty slot is worth more than these cards", Said(bw));
            var mixed = chest(6, new[] { 3.12, 2.60, 2.40 }, 2.18); mixed.Cards[2].HeldCost = 0; mixed.Cards[2].Score = 0.5; var mw = D(mixed);
            Check("WS3", "one card a stack (no slot cost) and every card AVOID: the 0.15.0 words", mw.Show && mw.Words == "every card here would hurt this squad - take the skip bonus" && !mw.Why.Contains("merit"), Said(mw));
            var other = chest(6, new[] { 3.12, 2.60, 2.40 }, 2.18); other.Cards[2].HeldCost = 0; other.Cards[2].Score = 1.10; var ow = D(other);
            Check("WS3", "the best net a stack (1.10), the best merit another card: 'best C 1.10 (A 3.12 before the held slot cost 2.18)', no REROLL, silent",
                !ow.Show && ow.Why.StartsWith("best C 1.10 (A 3.12 before the held slot cost 2.18) at or over the chest floor 2.50"), Said(ow));
            var plain = D(chest(6, new[] { 2.30, 2.10, 1.90 }, 0));
            Check("WS3", "no held cost: merit = top - REROLL 'best A 2.30 under the chest floor 2.50' as in 0.15.0 (HintCases H1 / H2 rerun unchanged)",
                plain.Show && plain.Action == HintAction.Reroll && plain.Why.StartsWith("best A 2.30 under the chest floor 2.50 | rerolls"), Said(plain));
            var off = chest(6, new[] { 3.12, 2.60, 2.40 }, 2.18); off.Skip = false; var ofw = D(off);
            Check("WS3", "skip hints off: silent (no REROLL either: the merit is over the floor)", !ofw.Show, Said(ofw));
        }

        static void WsWords()
        {
            var lists = new[] { HeldRules.WoodenStickSay, HeldRules.EmptyChestSay, HeldRules.BothSlotSay, HeldRules.WellPreppedSay };
            var shown = new List<string>(); var bad = new List<string>();
            foreach (var f in lists)
            {
                string a = Drawn(f, 50), b = Drawn(f, 30);
                shown.Add("'" + a + "' / '" + b + "'");
                if (a.Length > 50 || b.Length > 30 || Wording.Rails(a).Count > 0 || Wording.Rails(b, 30).Count > 0 || !WhyText.Fits(Wording.Cap(f[0]))) bad.Add(a + " / " + b);
            }
            Check("WS4", "the slot cost's words drawn at 50 and 30 on the rails, each reason fits the WHY band: " + string.Join(" | ", shown),
                bad.Count == 0 && shown[0].StartsWith("'Fills an empty slot: -10% XP (Wooden Stick)' / ") && shown[3].StartsWith("'Adds the slot it takes - keeps your empty slots' / "), string.Join(" | ", bad));
            var skip = new[] { "an empty slot is worth more than these (Wooden Stick)", "an empty slot is worth more than these (Empty Chest)", "an empty slot is worth more than these cards", "an empty slot is worth more here" };
            Check("WS4", "the SKIP's forms within " + ScreenCall.Budget + " and the card font (" + string.Join(" / ", skip.Select(s => s.Length)) + "); the one naming both items is over it",
                skip.All(s => s.Length <= ScreenCall.Budget && Wording.Rails(s).All(r => r.StartsWith("length"))) && skip[0].Length == 53 && ("an empty slot is worth more than these (Wooden Stick and Empty Chest)").Length > ScreenCall.Budget);
            var ctx = Tse(null, 120).Ctx; string W = F(Math.Round(1.6 * ctx.Economy, 2));
            string p1 = HeldRules.Line(Free(5, "Wooden Stick"), ctx), p2 = HeldRules.Line(Free(5, "Empty Chest"), ctx), p3 = HeldRules.Line(Free(9, "Wooden Stick", "Empty Chest"), ctx);
            string p4 = HeldRules.Line(Free(0, "Wooden Stick"), null), p5 = HeldRules.Line(Free(-1, "Empty Chest"), null), p6 = HeldRules.Line(Free(1, "Wooden Stick"), null);
            Check("WS4", "the [held] parts: '" + p1 + "'",
                p1 == "[held] Wooden Stick: 5 empty slots (+50% XP), filling one costs " + W + " now" && p2 == "[held] Empty Chest: 5 empty slots (+50% damage), filling one costs 2.00"
                && p3 == "[held] Wooden Stick: 9 empty slots (+80% XP, the cap), filling one costs nothing | Empty Chest: 9 empty slots (+80% damage, the cap), filling one costs nothing"
                && p4 == "[held] Wooden Stick: no empty slot left" && p5 == "[held] Empty Chest: the empty slots not read - no slot cost" && p6 == "[held] Wooden Stick: 1 empty slot (+10% XP), filling one costs 1.60 now",
                p2 + " || " + p3 + " || " + p4 + " || " + p5 + " || " + p6);
        }

        /// <summary>The spec's risk (Q36): Empty Chest's own card (the item book's 'slots:damage' need, 0.3 per empty slot left after it - the
        /// value scale's 0.03 per 1% damage) against what it charges per slot once held; Wooden Stick's own card against its charge at 02:00.</summary>
        static void WsPrice()
        {
            var rows = new List<string>(); double best = 0; int from = -1;
            for (int f = 1; f <= 9; f++)
            {
                var c = Tse(new HeldFacts { FreeSlots = f }, 120); c.FreeSlots = f;
                double s = Eval("Empty Chest", c).Score;
                rows.Add(f + " " + F(s)); best = Math.Max(best, s);
                if (from < 0 && s >= HeldRules.EmptyChestCost) from = f;
            }
            Check("WS5", "Empty Chest's own card by the empty slots before it (" + string.Join(", ", rows) + ") against its per-slot charge " + F(HeldRules.EmptyChestCost)
                + ": the card is worth the charge from " + from + " empty slots on; Q36's 4.0 would stay over the card's best (" + F(best) + ")",
                HeldRules.EmptyChestCost == 2.0 && from > 0 && from <= 6 && best < 4.0);
            var wrows = new List<string>(); double charge = Math.Round(HeldRules.WoodenStickCost * Tse(null, 120).Ctx.Economy, 2); int wfrom = -1;
            for (int f = 1; f <= 9; f++)
            {
                var c = Tse(new HeldFacts { FreeSlots = f }, 120); c.FreeSlots = f;
                double s = Eval("Wooden Stick", c).Score;
                wrows.Add(f + " " + F(s));
                if (wfrom < 0 && s >= charge) wfrom = f;
            }
            Check("WS5", "Wooden Stick's own card at 02:00 (" + string.Join(", ", wrows) + ") against its per-slot charge " + F(charge) + ": worth it from " + wfrom + " empty slots on",
                wfrom > 0 && wfrom <= 6);
        }

        static void WsSources()
        {
            string rules = Src("ItemRules.cs"), call = Src("ScreenCall.cs"), hint = Src("RerollHint.cs");
            if (rules == null || call == null || hint == null) { Check("WS6", "the sources are readable from the bench", false); return; }
            Check("WS6", "ItemRules: the slot cost after Scaling (the merit kept), Well Prepped's words; ScreenCall: REROLL on the merit, the all-AVOID SKIP's slot words under the revive guard; RerollHint fills HintCard.HeldCost and SlotBy",
                rules.Contains("slotCost = HeldRules.SlotCost(c, name, out slotSay, out _);") && rules.IndexOf("score += Scaling(name, c, fitWhy);", StringComparison.Ordinal) < rules.IndexOf("HeldRules.SlotCost(c, name", StringComparison.Ordinal)
                && rules.Contains("C16-01c: Well Prepped while Wooden Stick / Empty Chest are held")
                && call.Contains("if (c.Floor > 0 && merit < c.Floor)") && !call.Contains("top < c.Floor") && call.Contains("\"an empty slot is worth more than these (\" + x.SlotBy + \")\"")
                && call.IndexOf("if (held == null && x.Skip && x.CanSkip && allAvoid)", StringComparison.Ordinal) < call.IndexOf("an empty slot is worth more than these (", StringComparison.Ordinal)
                && hint.Contains("k.HeldCost = w.Item.HeldCost;") && hint.Contains("x.SlotBy = HeldRules.SlotBy(held);"));
        }

        // ---------------------------------------------------------------- C16-01i Last Unicorn
        static Cell EvalA(string name, ItemContext c, bool animal) { c.Animal = animal; try { return Eval(name, c); } finally { c.Animal = false; } }
        static HeldFacts Animals(int n, params string[] names) { var h = Held(names); h.AnimalHeld = n; return h; }

        static void LuCount()
        {
            var lu0 = Eval("Last Unicorn", Tse(new HeldFacts()));
            var two = Eval("Last Unicorn", Tse(Animals(2, "Homing Pigeon", "Giant Enemy Crab")));
            Check("LU1", "Last Unicorn with Homing Pigeon + Giant Enemy Crab held (2 animal items): " + F(lu0.Score) + " -> " + F(two.Score) + " (+1.80), 'Counts 3 animal items: +30 Luck, +15% damage', the reason first",
                Near(two.Score - lu0.Score, 1.80, 1e-9) && two.Line == "Counts 3 animal items: +30 Luck, +15% damage" && two.Why[0] == "held: counts 3 animal items: +30 Luck, +15% damage", "'" + two.Line + "' | " + two.Why[0]);
            var one = Eval("Last Unicorn", Tse(Animals(1, "Homing Pigeon"))); var three = Eval("Last Unicorn", Tse(Animals(3, "Homing Pigeon", "Giant Enemy Crab", "Chick Magnet")));
            Check("LU1", "one animal item held: +0.90, 'counts 2 animal items: +20 Luck, +10% damage'; three: +1.80 (two at most, the pairs' cap), 'counts 4 ...'",
                Near(one.Score - lu0.Score, 0.90, 1e-9) && one.Why[0] == "held: counts 2 animal items: +20 Luck, +10% damage" && Near(three.Score - lu0.Score, 1.80, 1e-9) && three.Why[0] == "held: counts 4 animal items: +40 Luck, +20% damage");
            var noAnimal = Eval("Last Unicorn", Tse(Animals(0, "Torchlight")));
            var again = Eval("Last Unicorn", Tse(Animals(3, "Last Unicorn", "Homing Pigeon", "Giant Enemy Crab"))); var again0 = Eval("Last Unicorn", Tse(Animals(1, "Last Unicorn")));
            var pre = new HeldFacts { AnimalHeld = 1 }; pre.Pretend.Add("Homing Pigeon");
            Check("LU1", "no animal item held: no term; Last Unicorn held already (it counts itself): no count term on a second copy; a pretend animal name counts (HeldRead adds it to AnimalHeld)",
                noAnimal.Score == lu0.Score && noAnimal.Line == lu0.Line && again.Score == again0.Score && !again.Why.Any(w => w.StartsWith("held: counts")) && Near(Eval("Last Unicorn", Tse(pre)).Score - lu0.Score, 0.9, 1e-9));
            var kt = Knowledge.FromJson(Knowledge.DefaultJson);
            Func<HeldFacts, ItemContext> tiered = h => { var c = Tse(h); c.K = kt; return c; };
            var renamed = Quiet(() => Eval("Unicorn Of The Last", tiered(Animals(2, "Homing Pigeon", "Giant Enemy Crab")), "Item_LastUnicorn"));
            var renamed0 = Quiet(() => Eval("Unicorn Of The Last", tiered(new HeldFacts()), "Item_LastUnicorn"));
            Check("LU1", "a renamed Last Unicorn is found by its asset", Near(renamed.Score - renamed0.Score, 1.8, 1e-9) && renamed.Why[0].StartsWith("held: counts 3 animal items"), F(renamed0.Score) + " -> " + F(renamed.Score));
        }

        static void LuPairs()
        {
            var lu = Held("Last Unicorn"); var none = new HeldFacts();
            var hp0 = EvalA("Homing Pigeon", Tse(none), true); var hp = EvalA("Homing Pigeon", Tse(lu), true);
            Check("LU2", "Homing Pigeon (the game's animal flag) with Last Unicorn held: " + F(hp0.Score) + " -> " + F(hp.Score) + " (+0.90), 'Pairs with your Last Unicorn'",
                Near(hp.Score - hp0.Score, 0.9, 1e-9) && hp.Line == "Pairs with your Last Unicorn" && hp.Why.Contains("pairs with your Last Unicorn"), "'" + hp.Line + "'");
            var tl0 = EvalA("Torchlight", Tse(none), false); var tl = EvalA("Torchlight", Tse(lu), false);
            var flagOff = EvalA("Homing Pigeon", Tse(lu), false); var flagOff0 = EvalA("Homing Pigeon", Tse(none), false);
            Check("LU2", "a non-animal item with Last Unicorn held: no change; the live flag decides, not the name (Homing Pigeon without its flag: no change)",
                tl.Score == tl0.Score && tl.Line == tl0.Line && flagOff.Score == flagOff0.Score && flagOff.Line == flagOff0.Line);
            var pre = new HeldFacts(); pre.Pretend.Add("Last Unicorn");
            var byAsset = new HeldFacts(); byAsset.Assets.Add("Item_LastUnicorn");
            Check("LU2", "Last Unicorn pretended or held by its asset only: the same +0.90", Near(EvalA("Homing Pigeon", Tse(pre), true).Score - hp0.Score, 0.9, 1e-9) && Near(EvalA("Homing Pigeon", Tse(byAsset), true).Score - hp0.Score, 0.9, 1e-9));
        }

        static void LuNamed()
        {
            Func<string, string, Tuple<double, string>> pair = (offered, held) => { var a = Eval(offered, Tse(new HeldFacts())); var b = Eval(offered, Tse(Held(held))); return Tuple.Create(b.Score - a.Score, b.Line); };
            var tw = pair("The Word", "Pocket Watch"); var pw = pair("Pocket Watch", "The Word"); var rg = pair("Ruby Gem", "Omnigeode"); var sg = pair("Sapphire Gem", "Omnigeode"); var og = pair("Omnigeode", "Sapphire Gem");
            Check("LU3", "the pairs the item texts name: The Word with Pocket Watch held +" + F(tw.Item1) + " ('" + tw.Item2 + "'), Pocket Watch with The Word +" + F(pw.Item1) + " ('" + pw.Item2 + "'), Ruby Gem / Sapphire Gem with Omnigeode +" + F(rg.Item1) + " / +" + F(sg.Item1) + ", Omnigeode with Sapphire Gem +" + F(og.Item1),
                Near(tw.Item1, 0.9, 1e-9) && tw.Item2 == "Pairs with your Pocket Watch" && Near(pw.Item1, 0.9, 1e-9) && pw.Item2 == "Pairs with The Word" && Near(rg.Item1, 0.9, 1e-9) && rg.Item2 == "Pairs with your Omnigeode"
                && Near(sg.Item1, 0.9, 1e-9) && Near(og.Item1, 0.9, 1e-9));
            var named = new[] { new[] { "The Word", "Pocket Watch" }, new[] { "Omnigeode", "Ruby Gem" }, new[] { "Omnigeode", "Sapphire Gem" } };
            int found = named.Count(p => ItemBook.Pairs.Any(b => (b[0] == p[0] && b[1] == p[1]) || (b[0] == p[1] && b[1] == p[0])));
            var kp = Knowledge.FromJson(Knowledge.DefaultJson); kp.ItemPairs.Add(new KeyValuePair<string, string>("Pocket Watch", "The Word"));
            var twc = Tse(Held("Pocket Watch")); twc.K = kp; var twk = Eval("The Word", twc);
            Check("LU3", "all three are ItemBook.Pairs (C16-02; none added, knowledge.json's DefaultJson untouched - part 1 is redundant); a knowledge.json that names one too counts it once",
                found == 3 && Near(twk.Score, Eval("The Word", Tse(Held("Pocket Watch"))).Score, 1e-9));
        }

        static void LuWords()
        {
            var bad = new List<string>(); var shown = new List<string>();
            foreach (int n in new[] { 1, 2, 9, 10 })
            {
                string[] lead; HeldRules.UnicornCount(new HeldFacts { AnimalHeld = n }, null, out lead);
                string a = Drawn(lead, 50), b = Drawn(lead, 30);
                shown.Add(lead[0].Length + " '" + a + "' / '" + b + "'");
                if (a.Length > 50 || b.Length > 30 || Wording.Rails(a).Count > 0 || Wording.Rails(b, 30).Count > 0 || !WhyText.Fits(Wording.Cap(lead[0]))) bad.Add(a + " / " + b);
            }
            Check("LU4", "Last Unicorn's words at 50 and 30 on the rails, each reason fits the WHY band: " + string.Join(" | ", shown), bad.Count == 0 && shown[1].StartsWith("44 'Counts 3 animal items: +30 Luck, +15% damage' / '3 animal items: +15% damage'"), string.Join(" | ", bad));
            Wording.Ambient(30, null);
            string at30;
            try { at30 = Wording.Item(new ItemSay { Lead = "pairs with your Last Unicorn" }); } finally { Wording.Ambient(0, null); }
            Check("LU4", "'Pairs with your Last Unicorn' (28) fits 30 as it is: '" + at30 + "'", at30 == "Pairs with your Last Unicorn" && Wording.Rails(at30, 30).Count == 0);
        }

        // ---------------------------------------------------------------- C16-01j the status conversions
        static readonly string[] TseSquad = { "Tank", "SWAT", "Engineer" }, PmmSquad = { "Pyro", "Medic", "Mechanic" }, HgSquad = { "Huntress", "Ghost" };
        /// <summary>Program.cs's scenario 'Tank + SWAT + Engineer' (Explosive 2, Slashing 0.5, Kinetic 1, Electric 1.5 of 5).</summary>
        static TagProfile TseTags()
        {
            var t = new TagProfile();
            t.Deals("Explosive", "Rocket Launcher", 1); t.Deals("Explosive", "Minefield", .5); t.Deals("Slashing", "Sawblade Drone", .5); t.Deals("Kinetic", "Assault Rifle", 1);
            t.Deals("Explosive", "Grenade Trail", .5); t.Deals("Electric", "Tesla", 1); t.Deals("Electric", "Electric Turret", .5);
            t.Points["Explosive"] = 7; t.Points["Kinetic"] = 3; t.Points["Electric"] = 4; t.SpecialAt = 10;
            return t;
        }
        /// <summary>'Pyro + Medic + Mechanic' (Fire 1.5, Chemical 1, Ice 1 of 3.5).</summary>
        static TagProfile PmmTags()
        {
            var t = new TagProfile();
            t.Deals("Fire", "Flamethrower", 1); t.Deals("Fire", "Molotov", .5); t.Deals("Chemical", "Syringe Rifle", 1); t.Deals("Ice", "Nitro-Gun", 1);
            t.Points["Fire"] = 12; t.Points["Chemical"] = 2; t.Points["Ice"] = 6; t.SpecialAt = 10;
            return t;
        }
        /// <summary>'Huntress + Ghost (crit)' (Ice 1, Kinetic 0.5, Slashing 1.5 of 3.0).</summary>
        static TagProfile HgTags()
        {
            var t = new TagProfile();
            t.Deals("Ice", "Freezing Arrows", 1); t.Deals("Kinetic", "Bear Trap", .5); t.Deals("Slashing", "Thousand Cuts", 1); t.Deals("Slashing", "Shuriken", .5);
            t.Points["Slashing"] = 9; t.Points["Ice"] = 5; t.SpecialAt = 10;
            return t;
        }
        /// <summary>A scenario squad at 05:00 of a 20:00 Normal run with <paramref name="held"/> (its names and pretend names in ItemContext.Held).</summary>
        static ItemContext On(TagProfile tags, string[] squad, HeldFacts held)
        {
            var ctx = new RunContext { Mode = "Normal", Goal = 1200, Seconds = 300, LevelRate = 2.9, Health = 1, D = new Doctrine() };
            var c = new ItemContext { Squad = squad.ToList(), Tags = tags, K = K, Ctx = ctx,
                Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase), OwnedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) };
            c.CritSquad = squad.Any(x => K.CritSquad.Contains(x, StringComparer.OrdinalIgnoreCase));
            c.ShieldOwned = squad.Contains("Engineer");
            c.HeldFacts = held;
            if (held != null) { foreach (var n in held.Names) c.Held.Add(n); foreach (var n in held.Pretend) c.Held.Add(n); }
            return c;
        }
        static List<string[]> Convs(params string[] names) { return HeldRules.ConversionsHeld(Held(names), null); }

        static void CvTables()
        {
            Check("CV1", "12 status items (1.0.2 names and assets, the game's Item_IconOfStilness), two per status; the six conversions are ItemBook.Converts, each in the held-item table",
                HeldRules.StatusItems.Length == 12 && HeldRules.StatusItems.GroupBy(s => s.Status).All(g => g.Count() == 2) && HeldRules.StatusItems.Select(s => s.Status).Distinct().Count() == 6
                && HeldRules.StatusItems.All(s => ItemBook.StatusOf.Any(st => st[1] == s.Status)) && HeldRules.StatusItems.Any(s => s.Item.Asset == "Item_IconOfStilness")
                && ItemBook.Converts.Length == 6 && ItemBook.Converts.All(cv => HeldRules.All.Any(i => i.Name == cv[0])));
            var bl = Convs("Battery Leakage");
            string e0 = string.Join(",", HeldRules.StatusSources("Electrify", null)), t1 = string.Join(",", HeldRules.StatusSources("Toxify", bl)), e2 = string.Join(",", HeldRules.StatusSources("Electrify", Convs("Battery Leakage", "Biofuel Energy")));
            Check("CV1", "the causes: Electrify <- " + e0 + " without a conversion; Battery Leakage: Toxify <- " + t1 + ", Electrify <- none; + Biofuel Energy: Electrify <- " + e2,
                e0 == "Electric" && t1 == "Electric,Chemical" && HeldRules.StatusSources("Electrify", bl).Count == 0 && e2 == "Chemical");
            var t = TseTags(); var p = PmmTags();
            Check("CV1", "StatusFit = min(1, 1.6 x the combined share): Electrify 0.48 (Electric 1.5 of 5), with Battery Leakage 0 and Toxify 0.48; Pyro + Medic + Mechanic with Fire Extinguisher: Freeze 1.00 (Fire + Ice); an unknown profile 0",
                Near(HeldRules.StatusFit("Electrify", null, t), 0.48, 1e-9) && HeldRules.StatusFit("Electrify", bl, t) == 0 && Near(HeldRules.StatusFit("Toxify", bl, t), 0.48, 1e-9)
                && HeldRules.StatusFit("Freeze", Convs("Fire Extinguisher"), p) == 1.0 && HeldRules.StatusFit("Burn", null, new TagProfile()) == 0);
            var byAsset = new HeldFacts(); byAsset.Assets.Add("Item_BatteryLeakage"); var pre = new HeldFacts(); pre.Pretend.Add("Battery Leakage");
            Check("CV1", "a conversion held by name, by its asset only, pretended, or named in ItemContext.Held; AnyConversion; the possessive",
                HeldRules.AnyConversion(Held("Battery Leakage")) && HeldRules.AnyConversion(byAsset) && HeldRules.AnyConversion(pre) && !HeldRules.AnyConversion(new HeldFacts()) && !HeldRules.AnyConversion(null)
                && HeldRules.ConversionsHeld(null, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "nail bat" }).Count == 1 && HeldRules.Poss("Jacob's Ladder") == "Jacob's Ladder's" && HeldRules.Poss("Nine Inch Nails") == "Nine Inch Nails'");
        }

        static void CvRuleA()
        {
            var bl = Held("Battery Leakage"); var none = new HeldFacts();
            foreach (var name in new[] { "Jacob's Ladder", "Icon of Tempest" })
            {
                var a = Eval(name, On(TseTags(), TseSquad, none)); var b = Eval(name, On(TseTags(), TseSquad, bl));
                Check("CV2", name + " with Battery Leakage held (Tank + SWAT + Engineer): " + F(a.Score) + " -> " + F(b.Score) + " = its Electric fit 0.48 (-0.29) and the tier cut (-0.70); 'No Electrify while you hold Battery Leakage', the reason first",
                    Near(b.Score - a.Score, -0.48 * 0.6 - 0.7, 0.006) && b.Line == "No Electrify while you hold Battery Leakage" && b.Why[0] == "held: no Electrify while you hold Battery Leakage"
                    && a.Why.Any(w => w.EndsWith(": electric")) && !b.Why.Any(w => w.EndsWith(": electric")), "'" + b.Line + "' | " + string.Join("; ", b.Why));
            }
            var pa = Eval("Plague's Visage", On(TseTags(), TseSquad, none)); var pb = Eval("Plague's Visage", On(TseTags(), TseSquad, bl));
            Check("CV2", "Plague's Visage gains the fit 0.48 (+0.29) and loses the tier cut (+0.70): " + F(pa.Score) + " -> " + F(pb.Score) + ", 'Your Electric now causes Toxify (Battery Leakage)'; 'no Chemical damage on the squad' goes",
                Near(pb.Score - pa.Score, 0.48 * 0.6 + 0.7, 0.006) && pb.Line == "Your Electric now causes Toxify (Battery Leakage)" && pb.Why[0] == "held: your Electric now causes Toxify (Battery Leakage)"
                && pa.Why.Contains("no Chemical damage on the squad") && !pb.Why.Contains("no Chemical damage on the squad") && !pb.Say.Nobody.Contains("Chemical"), "'" + pb.Line + "' | " + string.Join("; ", pb.Why));
            var both = Held("Battery Leakage", "Biofuel Energy");
            var jb = Eval("Jacob's Ladder", On(TseTags(), TseSquad, both)); var j1 = Eval("Jacob's Ladder", On(TseTags(), TseSquad, bl)); var pvb = Eval("Plague's Visage", On(TseTags(), TseSquad, both));
            Check("CV2", "Battery Leakage + Biofuel Energy held: Electrify <- Chemical (none here) - Jacob's Ladder still dead, Plague's Visage still fed",
                Near(jb.Score, j1.Score, 1e-9) && jb.Line == "No Electrify while you hold Battery Leakage" && Near(pvb.Score, pb.Score, 1e-9) && pvb.Line == pb.Line, "'" + jb.Line + "' / '" + pvb.Line + "'");
            var pre = new HeldFacts(); pre.Pretend.Add("Battery Leakage");
            var unk = Eval("Jacob's Ladder", On(new TagProfile(), TseSquad, bl)); var unk0 = Eval("Jacob's Ladder", On(new TagProfile(), TseSquad, none));
            var fe = Eval("Jacob's Ladder", On(TseTags(), TseSquad, Held("Fire Extinguisher"))); var fe0 = Eval("Jacob's Ladder", On(TseTags(), TseSquad, none));
            Check("CV2", "the same with Battery Leakage pretended; no change while the squad's damage types are unknown, or with a conversion that does not touch Electrify (Fire Extinguisher)",
                Eval("Jacob's Ladder", On(TseTags(), TseSquad, pre)).Score == j1.Score && unk.Score == unk0.Score && unk.Line == unk0.Line && fe.Score == fe0.Score && fe.Line == fe0.Line);
        }

        static void CvFire()
        {
            var fe = Held("Fire Extinguisher"); var none = new HeldFacts(); var t = PmmTags();
            foreach (var name in new[] { "Spoil Canister", "Icon of Cinder" })
            {
                var a = Eval(name, On(PmmTags(), PmmSquad, none)); var b = Eval(name, On(PmmTags(), PmmSquad, fe));
                Check("CV3", name + " with Fire Extinguisher held (Pyro + Medic + Mechanic): Burn has no cause - " + F(a.Score) + " -> " + F(b.Score) + " (fit " + F(t.Fit("Fire")) + " and the tier cut), 'No Burn while you hold Fire Extinguisher'",
                    Near(b.Score - a.Score, -t.Fit("Fire") * 0.6 - 0.7, 0.006) && b.Line == "No Burn while you hold Fire Extinguisher", "'" + b.Line + "'");
            }
            var sa = Eval("Special Snowflake", On(PmmTags(), PmmSquad, none)); var sb = Eval("Special Snowflake", On(PmmTags(), PmmSquad, fe));
            Check("CV3", "Special Snowflake fed by Fire + Ice: fit " + F(t.Fit("Ice")) + " -> 1.00 (" + F(sa.Score) + " -> " + F(sb.Score) + "), 'Your Fire now causes Freeze (Fire Extinguisher)'",
                Near(t.Fit("Ice"), 0.46, 0.005) && Near(sb.Score - sa.Score, (1 - t.Fit("Ice")) * 0.6, 0.006) && sb.Line == "Your Fire now causes Freeze (Fire Extinguisher)", "'" + sb.Line + "'");
            var two = Held("Fire Extinguisher", "Warm Ice Cream");
            var dead = HeldRules.StatusItems.Select(s => Tuple.Create(s.Item.Name, Eval(s.Item.Name, On(PmmTags(), PmmSquad, two)))).Where(x => x.Item2.Say.LeadForms != null && x.Item2.Say.LeadForms[0].StartsWith("no ")).Select(x => x.Item1).ToList();
            var sc0 = Eval("Spoil Canister", On(PmmTags(), PmmSquad, none)); var sc = Eval("Spoil Canister", On(PmmTags(), PmmSquad, two)); var ss = Eval("Special Snowflake", On(PmmTags(), PmmSquad, two));
            Check("CV3", "Fire Extinguisher + Warm Ice Cream: Burn <- Ice, Freeze <- Fire - no status item dead; Spoil Canister " + F(sc0.Score) + " -> " + F(sc.Score) + " ('Burn comes from your Ice now', a log reason), Special Snowflake fed by Fire",
                dead.Count == 0 && Near(sc.Score - sc0.Score, (t.Fit("Ice") - t.Fit("Fire")) * 0.6, 0.006) && sc.Why.Contains("Burn comes from your Ice now") && sc.Say.LeadForms == null && ss.Line == "Your Fire now causes Freeze (Fire Extinguisher)",
                "dead: " + string.Join(", ", dead) + " | " + string.Join("; ", sc.Why));
        }

        static void CvRuleB()
        {
            var none = new HeldFacts();
            var a0 = Eval("Battery Leakage", On(TseTags(), TseSquad, none)); var a = Eval("Battery Leakage", On(TseTags(), TseSquad, Held("Jacob's Ladder")));
            Check("CV4", "a Battery Leakage card with Jacob's Ladder held (Tank + SWAT + Engineer): " + F(a0.Score) + " -> " + F(a.Score) + " (-0.90), 'Would stop your Jacob's Ladder's Electrify'",
                Near(a.Score - a0.Score, -0.9, 1e-9) && a.Line == "Would stop your Jacob's Ladder's Electrify" && a.Why[0] == "held: would stop your Jacob's Ladder's Electrify", "'" + a.Line + "' | " + a.Why[0]);
            var e0 = Eval("Expired Sushi", On(HgTags(), HgSquad, none)); var e = Eval("Expired Sushi", On(HgTags(), HgSquad, Held("Bleeding Edge")));
            Check("CV4", "an Expired Sushi card with Bleeding Edge held (Huntress + Ghost, Slashing 1.5 of 3.0): " + F(e0.Score) + " -> " + F(e.Score) + " (-0.90), 'Would stop your Bleeding Edge's Bleed'",
                Near(e.Score - e0.Score, -0.9, 1e-9) && e.Line == "Would stop your Bleeding Edge's Bleed", "'" + e.Line + "'");
            var f = Eval("Battery Leakage", On(TseTags(), TseSquad, Held("Plague's Visage")));
            Check("CV4", "a Battery Leakage card with Plague's Visage held: +0.60, 'Would feed your Plague's Visage's Toxify'", Near(f.Score - a0.Score, 0.6, 1e-9) && f.Line == "Would feed your Plague's Visage's Toxify", "'" + f.Line + "'");
            var g = Eval("Battery Leakage", On(TseTags(), TseSquad, Held("Jacob's Ladder", "Icon of Tempest", "Plague's Visage", "Icon of Pestilence")));
            var h2 = Eval("Battery Leakage", On(TseTags(), TseSquad, Held("Jacob's Ladder", "Icon of Tempest")));
            Check("CV4", "two stopped and two fed: -1.80 + 1.20 = " + F(g.Score - a0.Score) + ", the stop's words, a reason per item; two stopped alone: -1.80 (the floor)",
                Near(g.Score - a0.Score, -0.6, 1e-9) && g.Line == "Would stop your Jacob's Ladder's Electrify" && g.Why.Count(w => w.StartsWith("held: would ")) == 4 && Near(h2.Score - a0.Score, -1.8, 1e-9));
            var w0 = Eval("Warm Ice Cream", On(PmmTags(), PmmSquad, none)); var w = Eval("Warm Ice Cream", On(PmmTags(), PmmSquad, Held("Special Snowflake")));
            Check("CV4", "a Warm Ice Cream card with Special Snowflake held (Pyro + Medic + Mechanic): -0.90, the status named without the item ('special' is the 10-tag word on a card): '" + w.Line + "'",
                Near(w.Score - w0.Score, -0.9, 1e-9) && w.Line == "Would stop the Freeze your item needs" && w.Why[0] == "held: would stop the Freeze your item needs" && Wording.Rails(w.Line).Count == 0);
            var held = Eval("Battery Leakage", On(TseTags(), TseSquad, Held("Battery Leakage", "Jacob's Ladder"))); var held0 = Eval("Battery Leakage", On(TseTags(), TseSquad, Held("Battery Leakage")));
            var hg = Eval("Battery Leakage", On(HgTags(), HgSquad, Held("Jacob's Ladder"))); var hg0 = Eval("Battery Leakage", On(HgTags(), HgSquad, none));
            var fe = Eval("Fire Extinguisher", On(TseTags(), TseSquad, Held("Jacob's Ladder"))); var fe0 = Eval("Fire Extinguisher", On(TseTags(), TseSquad, none));
            var pre = new HeldFacts(); pre.Pretend.Add("Jacob's Ladder");
            Check("CV4", "no change: Battery Leakage held already; Jacob's Ladder held where nobody deals Electric (Huntress + Ghost); a conversion that does not touch Electrify; Jacob's Ladder pretended: the same -0.90",
                held.Score == held0.Score && hg.Score == hg0.Score && hg.Line == hg0.Line && fe.Score == fe0.Score && fe.Line == fe0.Line && Near(Eval("Battery Leakage", On(TseTags(), TseSquad, pre)).Score - a0.Score, -0.9, 1e-9));
        }

        /// <summary>The book rows whose score, line or reasons move with <paramref name="held"/> against nothing held.</summary>
        static List<string> Movers(string[] squad, Func<TagProfile> tags, HeldFacts held)
        {
            var list = new List<string>();
            foreach (var row in ItemBook.All)
            {
                var a = Eval(row.Name, On(tags(), squad, new HeldFacts())); var b = Eval(row.Name, On(tags(), squad, held));
                if (a.Score != b.Score || a.Line != b.Line || string.Join("|", a.Why) != string.Join("|", b.Why)) list.Add(row.Name);
            }
            return list;
        }

        static void CvOnly()
        {
            var m1 = Movers(TseSquad, TseTags, Held("Battery Leakage"));
            Check("CV5", "Battery Leakage held (Tank + SWAT + Engineer): only the Electrify and Toxify items move (" + string.Join(", ", m1) + ")",
                m1.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { "Icon of Pestilence", "Icon of Tempest", "Jacob's Ladder", "Plague's Visage" }.OrderBy(x => x, StringComparer.Ordinal)));
            var m2 = Movers(TseSquad, TseTags, Held("Jacob's Ladder"));
            Check("CV5", "Jacob's Ladder held: only the Battery Leakage card moves (" + string.Join(", ", m2) + ")", m2.SequenceEqual(new[] { "Battery Leakage" }));
            var m3 = Movers(PmmSquad, PmmTags, Held("Fire Extinguisher"));
            Check("CV5", "Fire Extinguisher held (Pyro + Medic + Mechanic): the Burn and Freeze items move, and Boiling Pot by the item book's own status count (C16-02: " + string.Join(", ", m3) + ")",
                m3.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { "Boiling Pot", "Icon of Cinder", "Icon of Stillness", "Spoil Canister", "Special Snowflake" }.OrderBy(x => x, StringComparer.Ordinal)));
            var m4 = Movers(HgSquad, HgTags, Held("Homing Pigeon", "Torchlight"));
            Check("CV5", "no conversion held and none offered against a status item: nothing moves (" + string.Join(", ", m4) + ")", m4.Count == 0);
        }

        static void CvWords()
        {
            // every rule A / rule B form of the 12 status items x the 6 conversions, the squad dealing each type evenly (each status fit 0.23)
            Func<TagProfile> even = () => { var t = new TagProfile(); foreach (var n in TagProfile.Names) t.Deals(n, n + " Gun", 1); return t; };
            var forms = new List<string[]>(); var reasons = new List<string>();
            foreach (var cv in ItemBook.Converts)
                foreach (var si in HeldRules.StatusItems)
                {
                    string type, note; double o, n; string[] lead;
                    if (HeldRules.StatusRule(On(even(), TseSquad, Held(cv[0])), si.Item.Name, out type, out o, out n, out lead, out note) && lead != null) { forms.Add(lead); reasons.Add(lead[0]); }
                    List<string> whys; string[] l2;
                    HeldRules.ConvertCard(On(even(), TseSquad, Held(si.Item.Name)), cv[0], out whys, out l2);
                    if (l2 != null) { forms.Add(l2); reasons.AddRange(whys); }
                }
            var bad = new List<string>(); int longest = 0;
            foreach (var f in forms)
            {
                string a = Drawn(f, 50), b = Drawn(f, 30); longest = Math.Max(longest, f[0].Length);
                if (a.Length > 50 || b.Length > 30 || Wording.Rails(a).Count > 0 || Wording.Rails(b, 30).Count > 0) bad.Add(a + " / " + b);
            }
            var badR = reasons.Where(r => !WhyText.Fits(Wording.Cap(r))).ToList();
            Check("CV6", forms.Count + " card word lists of rules A and B (6 conversions x the status items they stop or feed; the longest first form " + longest + "): drawn at 50 and 30 on the rails; "
                + reasons.Count + " reasons fit the WHY band", forms.Count == 48 && bad.Count == 0 && badR.Count == 0, string.Join(" | ", bad.Concat(badR).Take(6)));
            Check("CV6", "the 51-character form drops its parenthesis at 50: '" + Drawn(new[] { "your Chemical now causes Electrify (Biofuel Energy)", "your Chemical causes Electrify now" }, 50) + "'",
                Drawn(new[] { "your Chemical now causes Electrify (Biofuel Energy)", "your Chemical causes Electrify now" }, 50) == "Your Chemical now causes Electrify");
            var parts = ItemBook.Converts.Select(cv => HeldRules.Line(Held(cv[0]), null)).ToList();
            Check("CV6", "the [held] parts: '" + string.Join("' | '", parts.Select(p => p == null ? "null" : p.Substring(7))) + "'",
                parts.All(p => p != null) && parts[2] == "[held] Battery Leakage: Electric causes Toxify, not Electrify" && parts[0] == "[held] Fire Extinguisher: Fire causes Freeze, not Burn" && parts[5] == "[held] Expired Sushi: Slashing causes Injured, not Bleed");
            string rules = Src("ItemRules.cs"), held = Src("HeldRules.cs");
            if (rules == null || held == null) { Check("CV6", "the sources are readable from the bench", false); return; }
            int score = rules.IndexOf("fit = Score(text, c, fitWhy", StringComparison.Ordinal), conv = rules.IndexOf("StatusConverted(name, text, row, c, fitWhy, heldWhy, ref fit, ref fits);", StringComparison.Ordinal), cut = rules.IndexOf("if (typed && !fits && tierScore > 0)", StringComparison.Ordinal);
            Check("CV6", "ItemRules: rule A right after Score() and before the tier cut, rule B beside the pairs and clashes; HeldRules reads ItemBook.Converts / StatusOf - no second conversion table",
                score > 0 && conv > score && cut > conv && rules.Contains("score += StatusCard(name, c, heldWhy);") && held.Contains("foreach (var cv in ItemBook.Converts)") && held.Contains("ItemBook.StatusOf")
                && !held.Contains("\"Electric\", \"Toxify\"") && !held.Contains("\"Fire\", \"Freeze\""));
        }

        // ---------------------------------------------------------------- over the probe (Run): LU5, CV7
        static void UnicornProbe(List<ProbeItem> items)
        {
            if (items == null || items.Count == 0) { Check("LU5", "the probe's items are loaded", false, "none"); return; }
            var lu = Held("Last Unicorn"); var bad = new List<string>(); int animals = 0, others = 0;
            foreach (var it in items.Where(i => i.InPool && ItemBook.Of(i.Name) != null && !string.Equals(i.Name, "Last Unicorn", StringComparison.Ordinal)))
            {
                var a = EvalA(it.Name, Tse(new HeldFacts()), it.Animal); var b = EvalA(it.Name, Tse(lu), it.Animal);
                double d = Math.Round(b.Score - a.Score, 2);
                if (it.Animal) { animals++; if (d != 0.9) bad.Add(Ascii(it.Name) + " " + F(d)); }
                else { others++; if (d != 0) bad.Add(Ascii(it.Name) + " " + F(d)); }
            }
            Check("LU5", "with Last Unicorn held, each of the probe's " + animals + " other animal items (the game's flag) pairs +0.90 and none of its " + others + " other pool items moves",
                bad.Count == 0 && animals == 7 && others > 100, string.Join(", ", bad));
            var names = new HashSet<string>(items.Select(i => i.Name), StringComparer.Ordinal);
            var missing = ItemBook.Pairs.SelectMany(p => p).Where(n => !names.Contains(n)).Distinct().ToList();
            Check("LU5", "every name of ItemBook.Pairs is a 1.0.2 item name (The Word, Pocket Watch, Omnigeode, Ruby Gem, Sapphire Gem among them)", missing.Count == 0, string.Join(", ", missing));
        }

        static void ConvertProbe(List<ProbeItem> items)
        {
            if (items == null || items.Count == 0) { Check("CV7", "the probe's items are loaded", false, "none"); return; }
            var assets = ProbeAssets();
            var missing = HeldRules.StatusItems.Where(s => !assets.ContainsKey(s.Item.Name) || assets[s.Item.Name] != s.Item.Asset).Select(s => s.Item.Name).ToList();
            Check("CV7", "the 12 status items: each name and asset exactly the probe's", assets.Count > 0 && missing.Count == 0, string.Join(", ", missing));
            var off = new List<string>();
            foreach (var cv in ItemBook.Converts)
            {
                var it = items.FirstOrDefault(i => i.Name == cv[0]);
                string d = it == null ? "" : System.Text.RegularExpressions.Regex.Replace(it.Desc ?? "", @"\s+", " ");
                string want = cv[1] + " damage now causes " + cv[2] + " instead of " + HeldRules.StatusOfType(cv[1], null);
                if (d.IndexOf(want, StringComparison.OrdinalIgnoreCase) < 0) off.Add(cv[0] + ": '" + d + "'");
            }
            Check("CV7", "the six conversion items' 1.0.2 texts say '<Type> damage now causes <New> instead of <Old>' as ItemBook.Converts and StatusOf", off.Count == 0, string.Join(" | ", off));
        }

        // ================================================================ H11: the animal list against the probe (the game's own flags)
        static void AnimalProbe(List<ProbeItem> items)
        {
            if (items == null || items.Count == 0) { Check("H11", "the probe's items are loaded", false, "none"); return; }
            var assets = ProbeAssets();
            var flagged = items.Where(i => i.Animal).ToList();
            var missing = new List<string>();
            foreach (var it in flagged)
            {
                string asset; assets.TryGetValue(it.Name, out asset);
                bool found = HeldRules.AnimalNames.Any(a => string.Equals(a.Name, it.Name, StringComparison.Ordinal) || (asset != null && string.Equals(a.Asset, asset, StringComparison.Ordinal)));
                if (!found) missing.Add(it.Name);
            }
            var extra = HeldRules.AnimalNames.Where(a => !flagged.Any(it => string.Equals(it.Name, a.Name, StringComparison.Ordinal) || (assets.ContainsKey(it.Name) && assets[it.Name] == a.Asset))).Select(a => a.Id).ToList();
            bool cat = flagged.Any(it => assets.ContainsKey(it.Name) && assets[it.Name] == "Item_SchrodingersCat");
            Check("H11", "AnimalNames is the probe's " + flagged.Count + " animal items (the umlaut name matched by its asset Item_SchrodingersCat)",
                flagged.Count == HeldRules.AnimalNames.Length && missing.Count == 0 && extra.Count == 0 && cat, "not listed: " + (missing.Count == 0 ? "none" : string.Join(", ", missing.Select(Ascii))) + "; listed, not flagged: " + (extra.Count == 0 ? "none" : string.Join(", ", extra)));
            var tableMissing = HeldRules.All.Where(i => !items.Any(it => string.Equals(it.Name, i.Name, StringComparison.Ordinal)) || (assets.ContainsKey(i.Name) && assets[i.Name] != i.Asset)).Select(i => i.Name).ToList();
            Check("H11", "the 16 items of the held-item table: each name and asset exactly the probe's", tableMissing.Count == 0, tableMissing.Count == 0 ? null : string.Join(", ", tableMissing));
            var rows = ItemBook.All.Where(r => r.Asset != null).Where(r => !assets.ContainsKey(r.Name) || assets[r.Name] != r.Asset).Select(r => r.Name).ToList();
            Check("H11", "the assets on the item book's rows (ManaPotionHeld) are the probe's", rows.Count == 0, rows.Count == 0 ? null : string.Join(", ", rows));
        }
        static string Ascii(string s) { return new string((s ?? "").Select(ch => ch > '~' ? '?' : ch).ToArray()); }

        /// <summary>name -> asset of the probe's items (the probe the bench reads: '--probe &lt;file&gt;', else probe.json beside gamedata.json, as
        /// Program.cs finds it); empty when it cannot be read.</summary>
        static Dictionary<string, string> ProbeAssets()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
                int pi = Array.IndexOf(args, "--probe");
                string data = args.Where((a, i) => !a.StartsWith("--") && (pi < 0 || i != pi + 1)).FirstOrDefault()
                    ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "data", "gamedata.json"));
                string probe = pi >= 0 && pi + 1 < args.Length ? args[pi + 1] : Path.Combine(Path.GetDirectoryName(data) ?? ".", "probe.json");
                if (!File.Exists(probe)) return d;
                using (var doc = JsonDocument.Parse(File.ReadAllText(probe)))
                    foreach (var it in doc.RootElement.GetProperty("items").EnumerateArray())
                    {
                        JsonElement n, a;
                        if (it.TryGetProperty("name", out n) && it.TryGetProperty("asset", out a) && n.ValueKind == JsonValueKind.String && a.ValueKind == JsonValueKind.String) d[n.GetString()] = a.GetString();
                    }
            }
            catch { }
            return d;
        }
    }
}
