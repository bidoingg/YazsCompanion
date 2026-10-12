// 0.16.1 "Seen live" (research\roadmap_1010\NEXT.md section 2): what the user's own 0.16.0 play of 10-10 showed, the item words.
//   SL1  C161-01: the WHY's run-clock clause names what an item's "economy" gain really gives - live 11:57:59.936 Power of Friendship
//        (+10 Luck, no XP) read "More XP and luck - pays off all run (17:29 left)"; every item book row with an economy gain or cost
//        names its own noun, the game's text backs every word of it (the probe), and no item's words say "XP and luck" any more;
//   SL2  C-1 (the user's decision B2-Q2: mark them, the cap stays B): an item no guide tiers says "(unrated)" where a guide item says its
//        tier, the WHY's first reason says whose rating it is, a guide-tiered item never does, the scores do not move;
//   SL3  C-8: AVOID (under 1, rust) only with a real cost - no item under 1 without one (every probe item, five squads), a guide C tier
//        kept at 1.00 reads "C-tier in the guides", the 0.16.0 Brave Toaster "AVOID   Stronger status effects" is gone;
//   SL4  C-8: the all-AVOID SKIP says "would hurt" only when every card costs something, else "weak".
// RunPure (no game data: the book's rows, the bench's own squads) runs in the full bench and in --no-data (Verdict.DataFree); Run adds
// the probe's texts and every pool item. Game names only (the repository is public); the build names are the bench's own.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class SeenLiveCases
    {
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string F(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        static Knowledge _k;
        static Knowledge K { get { return _k ?? (_k = Knowledge.FromJson(Knowledge.DefaultJson)); } }

        // ================================================================ the squads
        /// <summary>The 10-10 11:57:59 chest's squad, rebuilt: Pyro (Blowtorch 4, Fire Walk 3, Molotov Cocktail 1) and Tank (Shotgun 2), Normal,
        /// 02:31 played of 20:00 (17:29 left, economy about x1.3), health 93 %, no item held, 8 free slots, luck 0.</summary>
        static ItemContext Live()
        {
            var tags = new TagProfile { SpecialAt = 10 };
            tags.Source("Blowtorch", 1.0, new[] { "Fire" }); tags.Source("Fire Walk", 0.6, new[] { "Fire" }); tags.Source("Shotgun", 0.5, new[] { "Kinetic" });
            tags.Points["Fire"] = 9; tags.Points["Slashing"] = 4; tags.Points["Kinetic"] = 2;
            var run = new RunContext { Mode = "Normal", Goal = 1200, Seconds = 151, LevelRate = 2.2, LevelUps = 6, D = new Doctrine(), Health = 0.93 };
            var c = new ItemContext { Squad = new[] { "Pyro", "Tank" }, Tags = tags, K = K, Ctx = run, OwnedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase), Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase) };
            c.CloseShare = 1; c.LongShare = 0; c.ClipShare = 0.5; c.AbilityLean = 0.4; c.FreeSlots = 8; c.Luck = 0; c.Pickup = 1.25; c.MoveSpeed = 152;
            return c;
        }
        /// <summary>Five squads of different makes and clocks (early, mid, late, One Hit, hurting): the rails sweep them all.</summary>
        static List<Tuple<string, ItemContext>> Squads()
        {
            var list = new List<Tuple<string, ItemContext>> { Tuple.Create("live 10-10 Pyro + Tank 02:31", Live()) };
            Func<string, string[], double, double, Action<TagProfile>, string[], ItemContext> make = (mode, squad, seconds, health, fill, owned) =>
            {
                var tags = new TagProfile { SpecialAt = 10 }; fill(tags);
                var run = new RunContext { Mode = mode, Goal = 1200, Seconds = seconds, LevelRate = 2.2, LevelUps = (int)(seconds / 25), D = new Doctrine(), Health = health };
                var c = new ItemContext { Squad = squad, Tags = tags, K = K, Ctx = run, OwnedTags = new HashSet<string>(owned, StringComparer.OrdinalIgnoreCase), Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase) };
                c.CloseShare = 0.5; c.LongShare = 0.5; c.ClipShare = 0.5; c.AbilityLean = 0.5; c.FreeSlots = 4; c.Luck = 10; c.Pickup = 1.1; c.MoveSpeed = 110;
                c.CritSquad = squad.Any(x => K.CritSquad.Contains(x, StringComparer.OrdinalIgnoreCase)); c.ShieldOwned = squad.Contains("Engineer");
                return c;
            };
            list.Add(Tuple.Create("SWAT + Engineer 10:00", make("Normal", new[] { "SWAT", "Engineer" }, 600, 1.0, t => { t.Source("Assault Rifle", 1.0, new[] { "Kinetic" }); t.Source("Tesla", 0.7, new[] { "Electric" }); t.Points["Kinetic"] = 8; t.Points["Electric"] = 4; }, new[] { "Deployable", "Turret" })));
            list.Add(Tuple.Create("Huntress + Ghost 05:00", make("Normal", new[] { "Huntress", "Ghost" }, 300, 1.0, t => { t.Source("Freezing Arrows", 1.0, new[] { "Ice" }); t.Source("Thousand Cuts", 1.0, new[] { "Slashing" }); t.Points["Slashing"] = 9; t.Points["Ice"] = 5; }, new[] { "Deployable", "Taunt", "Melee" })));
            list.Add(Tuple.Create("Tank solo 18:30", make("Normal", new[] { "Tank" }, 1110, 1.0, t => { t.Source("Rocket Launcher", 1.0, new[] { "Explosive" }); t.Points["Explosive"] = 14; }, new[] { "Deployable" })));
            list.Add(Tuple.Create("Medic + Mechanic One Hit 08:00", make("OneHit", new[] { "Medic", "Mechanic" }, 480, 1.0, t => { t.Source("Syringe Rifle", 1.0, new[] { "Chemical" }); t.Source("Nitro-Gun", 1.0, new[] { "Ice" }); t.Points["Chemical"] = 6; t.Points["Ice"] = 6; }, new string[0])));
            list.Add(Tuple.Create("Pyro hurting 12:00 (40 %)", make("Normal", new[] { "Pyro" }, 720, 0.4, t => { t.Source("Flamethrower", 1.0, new[] { "Fire" }); t.Points["Fire"] = 12; }, new[] { "Grenade" })));
            return list;
        }

        sealed class Cell { public double Score; public string Line; public ItemSay Say; public List<string> Why; public List<string> Reasons; }
        /// <summary>One item as a chest card: the score, the card's line, what the rules reasoned, the WHY's reasons beyond the line.</summary>
        static Cell Eval(string name, string desc, IList<string> stats, bool healing, ItemContext c)
        {
            c.Stats = stats; c.Healing = healing; c.Say = new ItemSay();
            var why = new List<string>();
            double s = ItemRules.Evaluate(name, desc ?? "", c, why);
            var say = c.Say; c.Say = null;
            string line = Wording.Item(say);
            var w = new CardWords { Kind = SayKind.Item, Item = say };
            return new Cell { Score = s, Line = line, Say = say, Why = why, Reasons = WhyText.Reasons(w, why, line) };
        }

        // ================================================================ the pure part
        public static int RunPure()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.16.1 'Seen live' (the user's 0.16.0 play of 10-10): the economy words (C161-01), the estimate cue (C-1), AVOID only with a cost (C-8) - SeenLiveCases.cs");
            Economy();
            Estimate(null);
            Avoid(null);
            SkipWords();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        /// <summary>The full bench: the pure part, then the probe's texts and every pool item.</summary>
        public static int Run(List<ProbeItem> items)
        {
            int bad = RunPure();
            _bad = 0;
            Console.WriteLine("  -- with the probe's " + items.Count + " items");
            EconomyText(items);
            Estimate(items);
            Avoid(items);
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return bad + _bad;
        }

        // ---------------------------------------------------------------- SL1: C161-01, the economy noun
        static readonly string[] EconWords = { "XP", "luck", "level-ups", "training points" };
        static string[] Parts(string noun) { return (noun ?? "").Split(new[] { " and ", ", " }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray(); }

        static void Economy()
        {
            // the live chest: Power of Friendship's WHY
            var pof = Eval("Power of Friendship", "", null, false, Live());
            string clause = pof.Reasons.FirstOrDefault(r => r.Contains("pays off all run"));
            Check("SL1", "the 10-10 11:57:59 chest replayed: Power of Friendship (+10 Luck, no XP) - its WHY says 'More luck - pays off all run (17:29 left)', never 'XP'",
                clause == "More luck - pays off all run (17:29 left)" && !pof.Reasons.Any(r => r.Contains("XP")) && pof.Say.EconNoun == "luck",
                string.Join(" | ", pof.Reasons) + " (economy x" + F(Live().Ctx.Economy) + ")");
            var rope = Eval("Skip Rope", "", null, false, Live());
            // (59 characters with the clock: a WHY reason is built at a card's 50, its clock parenthesis gives way)
            Check("SL1", "... and Skip Rope (+1% XP per 2 skip points, a Military Training Point per 5): 'More XP and training points - pays off all run'",
                rope.Reasons.Contains("More XP and training points - pays off all run") && rope.Say.EconNoun == "XP and training points", string.Join(" | ", rope.Reasons));

            // every row of the book with an economy gain or cost names its own noun, from the four words the game's texts use
            var econ = ItemBook.All.Where(e => Tags(e.Gains).Contains("economy") || Tags(e.Costs).Contains("economy")).ToList();
            var missing = econ.Where(e => string.IsNullOrEmpty(e.Econ) || Parts(e.Econ).Any(p => !EconWords.Contains(p))).Select(e => e.Name + " '" + e.Econ + "'").ToList();
            var stray = ItemBook.All.Where(e => !string.IsNullOrEmpty(e.Econ) && !econ.Contains(e)).Select(e => e.Name).ToList();
            Check("SL1", "every item book row that gains or costs 'economy' (" + econ.Count + ") names what it gives - XP, luck, level-ups or training points - and no other row names one",
                econ.Count >= 15 && missing.Count == 0 && stray.Count == 0, "missing: " + string.Join(", ", missing) + "; stray: " + string.Join(", ", stray));

            // ... and the words drawn say exactly that noun: the card line and the WHY of each such row, early (pays off) and late (too late), in
            // every squad; no item of the book says the tag's generic "XP and luck" any more
            var wrong = new List<string>(); int said = 0;
            foreach (var sq in Squads())
                foreach (var e in ItemBook.All)
                    foreach (double seconds in new[] { 60.0, 1150.0 })
                    {
                        var c = sq.Item2; c.Ctx.Seconds = seconds;
                        var cell = Eval(e.Name, "", null, false, c);
                        var texts = new List<string>(cell.Reasons) { cell.Line };
                        foreach (var t in texts)
                        {
                            if (t == null) continue;
                            if (t.IndexOf("XP and luck", StringComparison.OrdinalIgnoreCase) >= 0) wrong.Add(e.Name + " (" + sq.Item1 + "): '" + t + "'");
                            var m = Regex.Match(t, @"^More (.+?) - (pays off all run|too late to pay off)");
                            if (!m.Success) continue;
                            if (cell.Say.Keyword != "economy") continue;          // another keyword's clause ("More cash ...")
                            said++;
                            if (m.Groups[1].Value != e.Econ) wrong.Add(e.Name + " (" + sq.Item1 + "): '" + t + "' - its noun is '" + e.Econ + "'");
                        }
                    }
            Check("SL1", "the clause says the row's own noun wherever it is drawn (" + said + " clauses in six squads, early and late); no item's words say 'XP and luck'",
                wrong.Count == 0 && said > 0, string.Join("; ", wrong.Take(6)));
            Check("SL1", "an item the book does not name reads its noun from the game's text: 'XP' / 'luck' / 'level-ups', joined; none named: the tag's own words",
                ItemRules.EconomyNoun(null, "+10 Luck") == "luck" && ItemRules.EconomyNoun(null, "+20% XP modifier. +5 Luck") == "XP and luck" && ItemRules.EconomyNoun(null, "Gain a level-up for each chest") == "level-ups"
                && ItemRules.EconomyNoun(null, "+25% Experience Gem Rarity") == "XP" && ItemRules.EconomyNoun(null, "+10% Cash") == null && ItemRules.EconomyNoun(ItemBook.Of("Ban Hammer"), "anything") == "luck");
        }
        static List<string> Tags(string list)
        {
            var t = new List<string>();
            if (string.IsNullOrEmpty(list) || list == "line") return t;
            foreach (var part in list.Split(',')) { string p = part.Trim().TrimStart('~'); int colon = p.LastIndexOf(':'); if (colon > 0) p = p.Substring(0, colon); t.Add(p.Trim()); }
            return t;
        }

        // the game's text backs every word of each row's noun (data\probe.json, game 1.0.2)
        static readonly Dictionary<string, Regex> Backs = new Dictionary<string, Regex>
        {
            { "XP", new Regex(@"\bXP\b|experience", RegexOptions.IgnoreCase) }, { "luck", new Regex(@"\bluck\b", RegexOptions.IgnoreCase) },
            { "level-ups", new Regex(@"level-ups?\b", RegexOptions.IgnoreCase) }, { "training points", new Regex(@"military training points?", RegexOptions.IgnoreCase) },
        };
        static void EconomyText(List<ProbeItem> items)
        {
            var by = items.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var bad = new List<string>(); int n = 0;
            foreach (var e in ItemBook.All.Where(x => !string.IsNullOrEmpty(x.Econ)))
            {
                ProbeItem it; if (!by.TryGetValue(e.Name, out it)) { bad.Add(e.Name + ": not in the probe"); continue; }
                string text = ItemRules.RichTag.Replace(it.Desc ?? "", "");
                n++;
                foreach (var p in Parts(e.Econ)) { Regex re; if (!Backs.TryGetValue(p, out re) || !re.IsMatch(text)) bad.Add(e.Name + ": '" + p + "' not in its text"); }
            }
            Check("SL1", "the game's own text backs every word of each row's noun (" + n + " rows: Power of Friendship '+10 Luck' -> luck, Black Box 'a level-up' -> level-ups, Skip Rope 'XP' and 'Military Training Point')",
                bad.Count == 0 && n >= 15, string.Join("; ", bad));
            var pof = by.ContainsKey("Power of Friendship") ? ItemRules.RichTag.Replace(by["Power of Friendship"].Desc ?? "", "") : "";
            Check("SL1", "the live fault's ground truth: Power of Friendship's 1.0.2 text gives Luck and no XP", Backs["luck"].IsMatch(pof) && !Backs["XP"].IsMatch(pof), pof.Replace("\n", " "));
        }

        // ---------------------------------------------------------------- SL2: C-1, the estimate cue
        static void Estimate(List<ProbeItem> items)
        {
            bool data = items != null;
            var names = data ? items.Where(i => i.InPool || ItemBook.Of(i.Name) != null).Select(i => i.Name).Distinct().ToList() : ItemBook.All.Select(e => e.Name).ToList();
            Func<string, ProbeItem> probe = n => data ? items.First(i => i.Name == n) : null;
            var bad = new List<string>(); int est = 0, guide = 0, cued = 0, slot = 0;
            var deck = new List<string>(); int deckSlot = 0, deckCued = 0;
            foreach (var sq in Squads())
                foreach (var name in names)
                {
                    var pi = probe(name);
                    var cell = Eval(name, pi != null ? pi.Desc : "", pi != null ? pi.Stats : null, pi != null && pi.Healing, sq.Item2);
                    string tier; bool tiered = K.ItemTier.TryGetValue(name, out tier);
                    bool hasCue = cell.Line != null && cell.Line.EndsWith(" (" + Wording.EstimateCue + ")", StringComparison.Ordinal);
                    bool whyFirst = cell.Reasons.Count > 0 && cell.Reasons.FirstOrDefault(r => r == Wording.EstimateWhy) != null;
                    if (tiered)
                    {
                        guide++;
                        if (cell.Say.Estimate || hasCue || cell.Reasons.Contains(Wording.EstimateWhy)) bad.Add(name + " (" + tier + ", " + sq.Item1 + "): marked as an estimate: '" + cell.Line + "'");
                        continue;
                    }
                    est++;
                    if (!cell.Say.Estimate) bad.Add(name + " (" + sq.Item1 + "): no guide tier but not an estimate");
                    if (!whyFirst) bad.Add(name + " (" + sq.Item1 + "): the WHY lacks '" + Wording.EstimateWhy + "': " + string.Join(" | ", cell.Reasons));
                    else
                    {   // first of the item's own facts: after a quest line and the held items' lines only
                        int at = cell.Reasons.IndexOf(Wording.EstimateWhy);
                        if (at > 0 && cell.Reasons.Take(at).Any(r => !r.StartsWith("Quest", StringComparison.Ordinal))) bad.Add(name + " (" + sq.Item1 + "): the estimate reason is not first: " + string.Join(" | ", cell.Reasons));
                    }
                    // the tier's slot: the line is the item's own words (the note) - it carries the cue at a PC card's 50
                    var s = cell.Say;
                    bool atSlot = s.Note != null && !s.RuleNote && s.Tier == null && Wording.Item(Strip(s)) == null;
                    if (atSlot) { slot++; if (hasCue) cued++; else bad.Add(name + " (" + sq.Item1 + "): its own words without the cue: '" + cell.Line + "'"); }
                    if (cell.Line != null && Wording.Rails(cell.Line).Count > 0) bad.Add(name + ": '" + cell.Line + "' breaks the rails (" + string.Join(", ", Wording.Rails(cell.Line)) + ")");
                    // the Steam Deck's #1 card: 42 characters (CardTextSize.LineChars at x1.56)
                    if (atSlot)
                    {
                        Wording.Ambient(42, null);
                        try
                        {
                            string d = Wording.Item(s); deckSlot++;
                            if (d.EndsWith(" (" + Wording.EstimateCue + ")", StringComparison.Ordinal)) deckCued++;
                            if (d.Length > 42 || Wording.Rails(d, 42).Count > 0) deck.Add(name + ": '" + d + "'");
                        }
                        finally { Wording.Ambient(0, null); }
                    }
                }
            string where = data ? "every pool item of the probe" : "every item book row";
            Check("SL2", where + " in six squads: no guide tier -> an estimate, the WHY's first reason '" + Wording.EstimateWhy + "'; a guide-tiered item never marked (" + est + " estimate / " + guide + " guide cards)",
                bad.Count == 0, string.Join("; ", bad.Take(6)) + (bad.Count > 6 ? " (+" + (bad.Count - 6) + ")" : ""));
            Check("SL2", "... where the line is the item's own words (the tier's place) it ends '(" + Wording.EstimateCue + ")' on a PC card (50): " + cued + " of " + slot + "; on the Steam Deck's #1 card (42) " + deckCued + " of " + deckSlot + " (the rest: its words alone, the WHY says it)",
                slot > 0 && cued == slot && deck.Count == 0, string.Join("; ", deck.Take(4)));
            if (data) return;
            Check("SL2", "the WHY reason within the band's rails (64), the cue a plain word; the score never reads the mark (the C-1 cap stays the item book's: no untiered Value over B)",
                WhyText.Fits(Wording.EstimateWhy) && Wording.EstimateWhy.Length <= WhyText.Budget && Wording.Rails("x (" + Wording.EstimateCue + ")").Count == 0
                && ItemBook.All.Where(e => !K.ItemTier.ContainsKey(e.Name)).All(e => e.Value <= 1.0));
            // the live card: Power of Friendship's line with the cue (46 characters)
            var pof = Eval("Power of Friendship", "", null, false, Live());
            Check("SL2", "the 10-10 chest's #1 card: 'Halves some hits, more in a big team (unrated)'; its WHY starts with whose rating it is",
                pof.Line == "Halves some hits, more in a big team (unrated)" && pof.Reasons.FirstOrDefault() == Wording.EstimateWhy, "'" + pof.Line + "' | " + string.Join(" | ", pof.Reasons));
        }
        /// <summary>The ItemSay without its note, tier and estimate - what Wording.Item says before it reaches the tier's slot (null: nothing earlier).</summary>
        static ItemSay Strip(ItemSay s)
        {
            var c = new ItemSay { Quest = s.Quest, Held = s.Held, QuestLine = s.QuestLine, Lead = s.Lead, Lacks = s.Lacks, Want = s.Want, WantBuild = s.WantBuild, Scaling = s.Scaling, Clock = s.Clock,
                LeadForms = s.LeadForms, Weak = s.Weak, Tier = s.Weak ? s.Tier : null };
            c.Boosts.AddRange(s.Boosts); c.Hurts.AddRange(s.Hurts); c.Nobody.AddRange(s.Nobody);
            string line = Wording.Item(c);
            return line == "Nothing in it for this squad" ? null : c;
        }

        // ---------------------------------------------------------------- SL3: C-8, AVOID only with a real cost
        static void Avoid(List<ProbeItem> items)
        {
            bool data = items != null;
            var names = data ? items.Where(i => i.InPool || ItemBook.Of(i.Name) != null).Select(i => i.Name).Distinct().ToList() : ItemBook.All.Select(e => e.Name).ToList();
            var bad = new List<string>(); int avoid = 0, weak = 0, weakC = 0;
            foreach (var sq in Squads())
                foreach (var name in names)
                {
                    var pi = data ? items.First(i => i.Name == name) : null;
                    var cell = Eval(name, pi != null ? pi.Desc : "", pi != null ? pi.Stats : null, pi != null && pi.Healing, sq.Item2);
                    if (cell.Score < 1)
                    {
                        avoid++;
                        if (cell.Say.Cost == null) bad.Add(name + " (" + sq.Item1 + ") " + F(cell.Score) + " AVOID without a cost: '" + cell.Line + "'");
                    }
                    if (cell.Say.Weak)
                    {
                        weak++;
                        if (Math.Abs(cell.Score - 1.0) > 1e-9 || !cell.Why.Contains(ItemRules.WeakWhy)) bad.Add(name + " (" + sq.Item1 + ") weak at " + F(cell.Score));
                        string tier; K.ItemTier.TryGetValue(name, out tier);
                        if (tier == "C") { weakC++; if (cell.Line != "C-tier in the guides") bad.Add(name + " (" + sq.Item1 + ") a weak C tier reads '" + cell.Line + "'"); }
                    }
                }
            Check("SL3", (data ? "every pool item of the probe" : "every item book row") + " in six squads: under 1 (AVOID) only with a real cost (" + avoid + " AVOID cards, each with its cost); a weak card without one stays at 1.00 (" + weak + ", " + weakC + " guide C tiers reading 'C-tier in the guides')",
                bad.Count == 0 && weak > 0, string.Join("; ", bad.Take(6)) + (bad.Count > 6 ? " (+" + (bad.Count - 6) + ")" : ""));
            if (data) return;
            // the 0.16.0 cases by name: Brave Toaster (C) was "AVOID   Stronger status effects - suits Huntress"; Glass Cannon without the Energy
            // Shield, Gold Medal's slower levels, Detective's Pipe on a Kinetic squad and a clash keep their cost
            var huntress = Squads()[2].Item2;
            var toaster = Eval("Brave Toaster", "", null, false, huntress);
            Check("SL3", "Brave Toaster (C, no cost) on Huntress + Ghost: 1.00 'C-tier in the guides', grey - not 'AVOID   Stronger status effects - suits Huntress' (0.16.0: -0.26)",
                Math.Abs(toaster.Score - 1.0) < 1e-9 && toaster.Line == "C-tier in the guides" && toaster.Say.Weak && toaster.Say.Cost == null && Synergy.ReasonPrefix(2, toaster.Score).IndexOf("AVOID", StringComparison.Ordinal) < 0,
                F(toaster.Score) + " '" + toaster.Line + "' | " + string.Join("; ", toaster.Why));
            var noShield = Eval("Glass Cannon", "", null, false, Squads()[2].Item2);
            Check("SL3", "Glass Cannon without the Energy Shield keeps its AVOID, with its cost ('double damage taken, less max HP')",
                noShield.Score < 1 && noShield.Say.Cost == "double damage taken, less max HP", F(noShield.Score) + " cost '" + noShield.Say.Cost + "'");
            var held = Squads()[1].Item2; held.Held.Add("Devil's Deal");
            var balloon = Eval("99'th Balloon", "", null, false, held);
            Check("SL3", "a clash is a cost (99'th Balloon while Devil's Deal is held: its hits pop it - 'clashes with your Devil's Deal')", balloon.Say.Cost == "clashes with your Devil's Deal",
                F(balloon.Score) + " cost '" + balloon.Say.Cost + "'");
            var silencer = Eval("Silencer", "", null, false, Squads()[3].Item2);
            var tank = Squads()[3].Item2; tank.CloseShare = 0; var noShort = Eval("Silencer", "", null, false, tank);
            Check("SL3", "Silencer (S) with no short-range weapon: weak, not harmful - no AVOID, its words 'No short-range weapon on the squad'",
                noShort.Score >= 1 && noShort.Line == "No short-range weapon on the squad" && noShort.Say.Cost == null, F(noShort.Score) + " '" + noShort.Line + "' (with: " + F(silencer.Score) + ")");
        }

        // ---------------------------------------------------------------- SL4: C-8, the all-AVOID SKIP's words
        static HintCard Card(string name, double score, int rank, string kind, string head = "", bool skipped = false)
        { return new HintCard { Name = name, Score = score, Rank = rank, Kind = kind, Head = head, Skipped = skipped }; }
        static ScreenCall Decide(string screen, params HintCard[] cards)
        {
            var x = new ScreenCallIn { Screen = screen, Progress = 0.5, CanReroll = false, Rerolls = 0, RerollsText = "0 (bench)", CanSkip = true, SkipCash = 120, SkipHeal = 40, Health = 1, Survival = 1, Cash = 0.3 };
            x.Cards.AddRange(cards);
            return ScreenCall.Decide(x);
        }
        static void SkipWords()
        {
            var items = Decide("Chest", Card("Glass Cannon", 0.62, 1, "item", "C-tier item, no Energy Shield: double damage taken, less max HP"), Card("Jewel of Death", 0.4, 2, "item", "halves max HP, +44% XP"));
            var stats = Decide("Military", Card("Ability Cooldown", 0.8, 1, "stat", "held: does nothing while you hold Mana Potion"), Card("Ability Cooldown", 0.8, 2, "stat", "held: does nothing while you hold Mana Potion"));
            var quest = Decide("LevelUp", Card("Remote Control Car", 0.4, 1, "ability", "quest: Mechanic abilities fail it"), Card("Wrench", 0.4, 2, "ability", "quest: Mechanic abilities fail it"));
            var skipped = Decide("LevelUp", Card("Medical Drone", 0.85, 1, "ability", "", true), Card("Stimpack", 0.6, 2, "ability", "", true));
            var mixed = Decide("Chest", Card("Glass Cannon", 0.62, 1, "item", "C-tier item"), Card("Pocket Watch", 0.9, 2, "item", "already held"));
            Check("SL4", "every card costs something - items under 1 (the item rules keep a costless one at 1.00), a quest rule, abilities the build skips: 'every card here would hurt this squad' / 'every card hurts'",
                items.Action == HintAction.Skip && items.Words.StartsWith("every card here would hurt", StringComparison.Ordinal) && items.Short == "every card hurts"
                && quest.Words.StartsWith("every card here would hurt", StringComparison.Ordinal) && skipped.Words.StartsWith("every card here would hurt", StringComparison.Ordinal),
                items.Words + " | " + quest.Words + " | " + skipped.Words);
            Check("SL4", "cards under 1 that cost nothing (stat cards switched off, an item held already): 'every card here is weak for this squad - take the skip bonus' / 'every card is weak', the log says why",
                stats.Action == HintAction.Skip && stats.Words == "every card here is weak for this squad - take the skip bonus" && stats.Short == "every card is weak"
                && stats.Why.Contains("none costs the squad anything: weak") && mixed.Words.StartsWith("every card here is weak", StringComparison.Ordinal) && !items.Why.Contains("weak"),
                stats.Words + " / " + stats.Short + " | " + mixed.Words + " | " + stats.Why);
            Check("SL4", "the words within the hint's rails (64; the short form under the button)", (stats.Words ?? "").Length <= ScreenCall.Budget && (stats.Short ?? "").Length <= 20);
        }
    }
}
