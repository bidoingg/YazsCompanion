// 0.16.1, the 10-11 Steam Deck round (research\roadmap_1010\deck_1011\DECK_ROUND_1011.md): card words that 1280 x 800 cut short or
// got wrong, fixed before 0.16.1's commit.
//   DR2  R-02: the Liberate card's line 'A level-up and cash instead of a recruit' was 969 units in the 900 of its rect beside the
//        Deck's "3RD" - an ellipsis on every rescue screen: a shorter form where the long one does not fit; every Liberate line fits
//        the Deck's room;
//   DR3  R-03: three item lines stopped mid-phrase ('Better military cards under a', 'Throws an Explosive grenade every', 'Rerolls and
//        banishes may cost'): the item book's short forms (Brief), a cut to the room that never ends on a word leading into the part
//        cut off, and a sweep of every item note at the Deck's rooms - no line ends mid-phrase;
//   DR5  R-05: a name lent for a weapon ('Blaster') leaked into the military card 'Master Blaster' in a WHY: the military training and
//        Research Pod cards are in the names table, whole (a made-up rename here - the repository is public);
//   DR7  R-07: Magazine Clip's only line was the clip rule's condition 'The squad reloads magazines': the item's own words first, the
//        condition worded as one.
// RunPure (no game data: the book's rows, the bench's own squads) runs in the full bench and in --no-data (Verdict.DataFree); Run adds
// every pool item of the probe and its military training cards. Game names only; the renames are the bench's own.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class DeckRoundCases
    {
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string Show(IEnumerable<string> l, int n = 6) { var x = l.ToList(); return x.Count == 0 ? "none" : string.Join("; ", x.Take(n)) + (x.Count > n ? " (+" + (x.Count - n) + ")" : ""); }
        static Knowledge _k;
        static Knowledge K { get { return _k ?? (_k = Knowledge.FromJson(Knowledge.DefaultJson)); } }
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

        // ================================================================ the Steam Deck's room
        /// <summary>The visible characters of a card's reason line at 1280 x 800 on Auto (CardTextSize.LineChars at x1.56, 16 px): 42 in
        /// all - 42 on the #1 card, 36 beside "2ND" / "3RD" / "4TH", 34 beside "AVOID" (Wording.RoomBeside).</summary>
        static int DeckChars()
        {
            float px1 = CardTextSize.ReasonPx1(1280, 800);
            return CardTextSize.LineChars(CardTextSize.Scale(0f, px1, CardTextSize.CanvasUnits(1280, 800)), px1);
        }
        static int DeckRoom(int rank, double score) { return Wording.RoomBeside(Synergy.ReasonPrefixWidth(rank, score), DeckChars()); }
        /// <summary>The live width of the 10-11 Liberate line: 969 units for "3RD   " and its 40 characters at 16 px (c:273 of the 005327
        /// upload) - 21.1 units a character on the Deck's card.</summary>
        const double DeckUnitsPerChar = 969.0 / 46;

        static string Drawn(int room, Func<string> line)
        {
            Wording.Ambient(room, null);
            try { return line(); } finally { Wording.Ambient(0, null); }
        }

        // ================================================================ the 10-11 Deck squad
        /// <summary>The 10-11 run's squad rebuilt (Medic, Tank and SWAT, by their game classes): Normal, 07:03 played of 20:00,
        /// Kinetic 16 / Ice 7 / Explosive 6 / Chemical 4, every weapon on a magazine, an item held, 7 free slots, luck 0.</summary>
        static ItemContext Deck1011(double clip = 1)
        {
            var tags = new TagProfile { SpecialAt = 10 };
            tags.Source("Handgun", 1.0, new[] { "Kinetic" }); tags.Source("Medical Drone", 0.5, new[] { "Kinetic" }); tags.Source("Experiment 21", 0.5, new[] { "Chemical", "Ice", "Explosive" });
            tags.Source("Pump-Action Shotgun", 0.8, new[] { "Kinetic" });
            tags.Points["Kinetic"] = 16; tags.Points["Ice"] = 7; tags.Points["Explosive"] = 6; tags.Points["Chemical"] = 4;
            var run = new RunContext { Mode = "Normal", Goal = 1200, Seconds = 423, LevelRate = 2.9, LevelUps = 20, D = new Doctrine(), Health = 0.9 };
            var c = new ItemContext { Squad = new[] { "Medic", "Tank", "SWAT" }, Tags = tags, K = K, Ctx = run, OwnedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Icon of Stillness" } };
            c.CloseShare = 0.4; c.LongShare = 0.2; c.ClipShare = clip; c.AbilityLean = 0.5; c.FreeSlots = 7; c.Luck = 0; c.Pickup = 1.75; c.MoveSpeed = 140;
            return c;
        }

        sealed class Cell { public double Score; public ItemSay Say; public List<string> Why; public string Line; }
        /// <summary>One item as a chest card in <paramref name="room"/> characters: the score, what the rules reasoned, the line.</summary>
        static Cell Eval(string name, string desc, ItemContext c, int room)
        {
            c.Say = new ItemSay();
            var why = new List<string>();
            double s = ItemRules.Evaluate(name, desc ?? "", c, why);
            var say = c.Say; c.Say = null;
            return new Cell { Score = s, Say = say, Why = why, Line = Drawn(room, () => Wording.Item(say)) };
        }

        // ================================================================ the pure part
        public static int RunPure()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.16.1, the 10-11 Steam Deck round: the Liberate line (R-02), item lines cut mid-phrase (R-03), a lent name in a military card (R-05), the magazine words (R-07) - DeckRoundCases.cs");
            LiberateLine();
            ItemNotes();
            LentNames();
            ClipWords();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        /// <summary>The full bench: the pure part, then every pool item of the probe and its military training cards.</summary>
        public static int Run(List<ProbeItem> items, string probePath)
        {
            int bad = RunPure();
            _bad = 0;
            Console.WriteLine("  -- with the probe's " + items.Count + " items");
            PoolSweep(items);
            MilitaryNames(items, probePath);
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return bad + _bad;
        }

        // ---------------------------------------------------------------- DR2: R-02, the Liberate line
        static string Liberate(RecruitSay r, int rank, int room) { return Wording.Card(new CardWords { Kind = SayKind.Recruit, Recruit = r }, rank, null, null, room, null); }

        static void LiberateLine()
        {
            int deck = DeckChars(), third = DeckRoom(3, 1.00), pcRoom = Wording.RoomBeside(Synergy.ReasonPrefixWidth(3, 1.00));
            string d3 = Liberate(new RecruitSay { Liberate = true }, 3, third), pc = Liberate(new RecruitSay { Liberate = true }, 3, pcRoom);
            double units = (Synergy.ReasonPrefixWidth(3, 1.00) + d3.Length) * DeckUnitsPerChar;
            Check("DR2", "the 10-11 rescue screens replayed: Liberate 3rd at 1.00 on the Steam Deck (" + third + " characters beside '3RD', " + deck + " in all) reads 'Level-up and cash, no recruit' - about "
                + units.ToString("0") + " units in the 900 (live: 969, an ellipsis); a PC card (" + pcRoom + ") keeps 'A level-up and cash instead of a recruit'",
                deck == 42 && third == 36 && d3 == "Level-up and cash, no recruit" && units <= 900 && pc == "A level-up and cash instead of a recruit", "'" + d3 + "' | PC '" + pc + "'");
            string full = Liberate(new RecruitSay { Liberate = true, Full = true }, 3, third), fullPc = Liberate(new RecruitSay { Liberate = true, Full = true }, 3, pcRoom);
            Check("DR2", "... and with the squad full: 'Squad is full - level-up and cash' on the Deck (42 characters did not fit its 36 either), the PC's words unchanged",
                full == "Squad is full - level-up and cash" && fullPc == "Squad is full - take the level-up and cash", "'" + full + "' | PC '" + fullPc + "'");

            // every Liberate line: 1 - 3 level-ups (Hijacked Signal), cash or a heal (Life Savings), the squad full / late / neither, #1 - #4
            var bad = new List<string>(); int n = 0;
            foreach (int k in new[] { 1, 2, 3 })
                foreach (bool heal in new[] { false, true })
                    foreach (int state in new[] { 0, 1, 2 })
                        foreach (int rank in new[] { 1, 2, 3, 4 })
                        {
                            var r = new RecruitSay { Liberate = true, LiberateLevelUps = k, CashHeals = heal, Full = state == 1, Late = state == 2 ? "3:02 left" : null };
                            int room = DeckRoom(rank, 1.00);
                            string line = Liberate(r, rank, room); n++;
                            if (line == null || line.Length > room || Wording.Rails(line, room).Count > 0 || Wording.EndsDangling(line))
                                bad.Add("#" + rank + " k" + k + (heal ? " heal" : "") + (state == 1 ? " full" : state == 2 ? " late" : "") + ": '" + line + "' (" + (line ?? "").Length + " in " + room + ")");
                        }
            Check("DR2", "every Liberate line fits its room on the Steam Deck (1 - 3 level-ups, cash or a heal, the squad full / late / neither, #1 - #4: " + n + " lines)", bad.Count == 0 && n == 72, Show(bad));
        }

        // ---------------------------------------------------------------- DR3: R-03, item lines cut mid-phrase
        /// <summary>The line an item's own words draw where nothing squad-specific leads: its guide tier (or the estimate's cue), the book's
        /// words or a guide note, the book's short form - as ItemRules leaves them for Wording.Item.</summary>
        static string NoteLine(string name, string note, string brief, int room)
        {
            string tier; K.ItemTier.TryGetValue(name, out tier);
            var say = new ItemSay { Tier = tier, Note = note, Estimate = tier == null, Shorter = brief != null ? new[] { brief } : null };
            return Drawn(room, () => Wording.Item(say));
        }
        static string NoParen(string s)
        {
            int o = s.LastIndexOf(" (", StringComparison.Ordinal); if (o < 0) return null;
            int c = s.IndexOf(')', o);
            return (s.Substring(0, o) + (c >= 0 && c + 1 < s.Length ? s.Substring(c + 1) : "")).TrimEnd();
        }
        /// <summary>The forms a note is drawn in on purpose: with its tier, alone, with the estimate's cue (whole or its first clause), the
        /// book's short form, its first clause - each also without its parenthesis (Wording.Say). Anything else is the note cut to the room.</summary>
        static HashSet<string> Deliberate(string name, string note, string brief)
        {
            string tier; K.ItemTier.TryGetValue(name, out tier);
            var forms = new List<string> { note, note + " (" + Wording.EstimateCue + ")" };
            if (tier != null) forms.Add(tier.ToUpperInvariant() + "-tier - " + note);
            if (brief != null) forms.Add(brief);
            foreach (var cut in new[] { ':', ',', ';' }) { int i = note.IndexOf(cut); if (i > 8) { forms.Add(note.Substring(0, i)); forms.Add(note.Substring(0, i).TrimEnd() + " (" + Wording.EstimateCue + ")"); } }
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in forms) { set.Add(Wording.Cap(f)); string b = NoParen(f); if (b != null) set.Add(Wording.Cap(b)); }
            return set;
        }

        static void ItemNotes()
        {
            int deck = DeckChars(), first = DeckRoom(1, 2.0), second = DeckRoom(2, 2.0), fourth = DeckRoom(4, 1.5);
            // the three lines of the 10-11 run, replayed in its squad: Bells & Whistles (c:824, c:1339) and Teddy Bear (c:1157) 4th, Great Nade
            // (c:997) 2nd; each as the run drew it, then on the Deck's #1 card and a PC card
            var live = new[]
            {
                Tuple.Create("Bells & Whistles", fourth, "Better military cards under a", "Better military cards in a lockdown", "Better military cards under a lockdown", "Better military cards under a lockdown (unrated)", "4TH"),
                Tuple.Create("Great Nade", second, "Throws an Explosive grenade every", "An Explosive grenade every 10s", "Throws an Explosive grenade every 10s", "B-tier - throws an Explosive grenade every 10s", "2ND"),
                Tuple.Create("Teddy Bear", fourth, "Rerolls and banishes may cost", "Rerolls and banishes may be free", "Rerolls and banishes may cost nothing", "Rerolls and banishes may cost nothing (unrated)", "4TH"),
            };
            foreach (var t in live)
            {
                var deckCell = Eval(t.Item1, "", Deck1011(), t.Item2);
                string atFirst = Eval(t.Item1, "", Deck1011(), first).Line, atPc = Eval(t.Item1, "", Deck1011(), Wording.Budget).Line;
                Check("DR3", t.Item1 + " in the 10-11 squad, beside the Deck's '" + t.Item7 + "' (" + t.Item2 + " characters): '" + t.Item4 + "', not '" + t.Item3 + "'; on the Deck's #1 card (" + first + ") '" + t.Item5 + "', on a PC card '" + t.Item6 + "'",
                    deckCell.Line == t.Item4 && atFirst == t.Item5 && atPc == t.Item6 && !Wording.EndsDangling(deckCell.Line), "'" + deckCell.Line + "' | '" + atFirst + "' | '" + atPc + "'");
            }

            // the cut to the room without a short form (a guide note in knowledge.json, an item the book does not name): never on a word
            // that leads into the part cut off
            string nade = Drawn(second, () => Wording.Item(new ItemSay { Tier = "B", Note = "throws an Explosive grenade every 10s" }));
            string bells = Drawn(fourth, () => Wording.Item(new ItemSay { Tier = "B", Note = "better military cards under a lockdown" }));
            string gas = Drawn(first, () => Wording.Item(new ItemSay { Tier = "A", Note = "+20% damage per status effect on the target" }));
            Check("DR3", "a note cut to its room drops the words that lead into the cut part: 'Throws an Explosive grenade' (not '... every'), 'Better military cards' (not '... under a'), '+20% damage per status effect' (not '... on the')",
                nade == "Throws an Explosive grenade" && bells == "Better military cards" && gas == "+20% damage per status effect", "'" + nade + "' | '" + bells + "' | '" + gas + "'");
            Check("DR3", "Wording.EndsDangling: 'every', 'under a', 'may', 'than', 'per' lead on; 'cost nothing', 'every 10s', 'take this' do not",
                Wording.EndsDangling("throws an Explosive grenade every") && Wording.EndsDangling("better military cards under a") && Wording.EndsDangling("rerolls and banishes may")
                && Wording.EndsDangling("twice as strong than") && Wording.EndsDangling("+10% damage per") && !Wording.EndsDangling("may cost nothing") && !Wording.EndsDangling("a grenade every 10s")
                && !Wording.EndsDangling("the build's pick isn't offered - take this"));

            // the item's own words on a card: knowledge.json's guide note where it has one, else the book's (ItemRules.Evaluate)
            Func<BookEntry, string> words = e => { string g; return K.ItemNote.TryGetValue(e.Name, out g) ? g : e.Say; };

            // the book's short forms: within the narrow room, on the rails, shorter than the words, only where the words do not fit it
            int narrow = second;
            var briefs = ItemBook.All.Where(e => e.Brief != null).ToList();
            var badBrief = briefs.Where(e => words(e) == null || e.Brief.Length > narrow || e.Brief.Length >= words(e).Length || words(e).Length <= narrow || Wording.Rails(e.Brief, narrow).Count > 0
                || e.Brief.Any(ch => ch < 32 || ch > 126) || Wording.EndsDangling(e.Brief)).Select(e => e.Name + " '" + e.Brief + "'").ToList();
            Check("DR3", "the item book's " + briefs.Count + " short forms (" + briefs.Count(e => K.ItemNote.ContainsKey(e.Name)) + " of a guide note): each " + narrow + " characters at most, on the rails, shorter than its words, and only on a row whose words are longer than " + narrow,
                badBrief.Count == 0 && briefs.Count >= 15, Show(badBrief));

            // THE SWEEP: every item note - the book's words and knowledge.json's guide notes, with the book's short form as the rules hand it
            // on - in every room from 30 to the PC's 50: no line ends on a word that leads into a cut part; at the Steam Deck's rooms (#1 42,
            // 2ND - 4TH 36) every one is drawn whole, as a clause or as its short form - never cut mid-phrase
            var notes = ItemBook.All.Where(e => words(e) != null).Select(e => Tuple.Create(e.Name, words(e), e.Brief, !K.ItemNote.ContainsKey(e.Name)))
                .Concat(K.ItemNote.Where(kv => ItemBook.Of(kv.Key) == null).Select(kv => Tuple.Create(kv.Key, kv.Value, (string)null, false))).ToList();
            var dangling = new List<string>(); var midPhrase = new List<string>(); int lines = 0;
            foreach (var t in notes)
            {
                for (int room = 30; room <= Wording.Budget; room++)
                {
                    string line = NoteLine(t.Item1, t.Item2, t.Item3, room); lines++;
                    if (Wording.EndsDangling(line)) dangling.Add(t.Item1 + " at " + room + ": '" + line + "'");
                }
                var ok = Deliberate(t.Item1, t.Item2, t.Item3);
                foreach (int room in new[] { first, second })
                {
                    string line = NoteLine(t.Item1, t.Item2, t.Item3, room);
                    if (!ok.Contains(line)) midPhrase.Add(t.Item1 + " at " + room + ": '" + line + "'");
                }
            }
            Check("DR3", "every item note (" + notes.Count(t => t.Item4) + " of the book, " + notes.Count(t => !t.Item4) + " guide notes) in every room from 30 to " + Wording.Budget + " (" + lines + " lines): none ends on a word that leads into a cut part",
                dangling.Count == 0 && lines > 0, Show(dangling));
            Check("DR3", "... and at the Steam Deck's rooms (#1 " + first + ", 2ND - 4TH " + second + ", " + deck + " in all) every one is drawn whole, as a clause or as its short form - none cut mid-phrase",
                midPhrase.Count == 0 && first == 42 && second == 36, Show(midPhrase));
            // the guide notes' short forms reach the card: Homing Pigeon (A, 'ability hits on full health enemies always crit') on the Deck
            var pigeon = Eval("Homing Pigeon", "", Deck1011(), second);
            string pigeonNote = Drawn(second, () => Wording.Item(new ItemSay { Tier = pigeon.Say.Tier, Note = pigeon.Say.Note, Shorter = pigeon.Say.Shorter }));
            Check("DR3", "a guide note too long for the Deck's room takes the book's short form: Homing Pigeon 'Abilities crit full-health enemies' beside '2ND', not 'Ability hits on full health enemies'",
                pigeonNote == "Abilities crit full-health enemies", "'" + pigeonNote + "' (its card: '" + pigeon.Line + "')");
        }

        /// <summary>Every pool item of the probe as the rules read it, in the 10-11 squad and two others, at the Deck's rooms and a PC card's:
        /// no line ends on a word that leads into a cut part, and none is longer than its room.</summary>
        static void PoolSweep(List<ProbeItem> items)
        {
            var names = items.Where(i => i.InPool || ItemBook.Of(i.Name) != null).GroupBy(i => i.Name).Select(g => g.First()).ToList();
            var squads = new List<Func<ItemContext>> { () => Deck1011(), () => Deck1011(0.5), () => Deck1011(0) };
            var bad = new List<string>(); int n = 0;
            foreach (var it in names)
                foreach (var sq in squads)
                    foreach (int room in new[] { DeckRoom(1, 2.0), DeckRoom(2, 2.0), DeckRoom(2, 0.5), Wording.Budget })
                    {
                        var c = sq(); c.Stats = it.Stats; c.Healing = it.Healing;
                        var cell = Eval(it.Name, it.Desc, c, room); n++;
                        if (cell.Line == null || cell.Line.Length > room || Wording.EndsDangling(cell.Line)) bad.Add(it.Name + " at " + room + ": '" + cell.Line + "'");
                    }
            Check("DR3", "every pool item of the probe (" + names.Count + ") in three squads at the Deck's rooms (42, 36, 34 beside AVOID) and a PC card's 50 (" + n + " lines): within its room, never ending on a word that leads into a cut part",
                bad.Count == 0 && names.Count > 100, Show(bad));
        }

        // ---------------------------------------------------------------- DR5: R-05, a lent name inside a military card's name
        static Dictionary<string, string> Renamed(params string[] pairs)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i + 1 < pairs.Length; i += 2) d[pairs[i]] = pairs[i + 1];
            return d;
        }
        static string Through(Dictionary<string, string> renamed, HashSet<string> all, string text)
        {
            Dictionary<string, string> table; Regex rx;
            Names.Compose(renamed, all, out table, out rx);
            return Names.Swap(text, table, rx);
        }

        static void LentNames()
        {
            // the game's names the table held up to 0.16.1 (classes, weapons, abilities, evolutions, skill powerups, items: a few of them), a
            // made-up rename of the weapon 'Blaster'; then the military training card 'Master Blaster' and the Research Pod heads, kept whole
            var lent = Renamed("Blaster", "Shiny Pellets");
            var before = new HashSet<string>(StringComparer.Ordinal) { "Engineer", "Blaster", "Grenade Trail", "Grenade", "Magazine Clip" };
            var after = new HashSet<string>(before, StringComparer.Ordinal);
            Names.Keep("Master Blaster", after); Names.Keep("Operation Planning", after); Names.Keep(Names.PodHeads(), after);
            const string why = "Just ahead of Master Blaster";
            string old = Through(lent, before, why), now = Through(lent, after, why), own = Through(lent, after, "Blaster goes first: Level 3 of 4");
            Check("DR5", "the 10-11 military screen (09:34) replayed with a made-up rename of 'Blaster': up to here the WHY read '" + old + "'; with the military cards in the table '" + now + "', and the weapon's own name is still lent ('" + own + "')",
                old == "Just ahead of Master Shiny Pellets" && now == why && own == "Shiny Pellets goes first: Level 3 of 4", "'" + old + "' -> '" + now + "' | '" + own + "'");
            // a Research Pod card ("#Kinetic +10", Ranker.ScoreHashtag) stays whole even if a name the table holds were the type's word
            var pod = Through(Renamed("Kinetic", "Motion"), after, "Well ahead of #Kinetic +10");
            var heads = Names.PodHeads().ToList();
            Check("DR5", "the Research Pod cards' heads (" + string.Join(", ", heads) + ") keep a card whole whatever its amount: 'Well ahead of #Kinetic +10' under a made-up rename of 'Kinetic'",
                pod == "Well ahead of #Kinetic +10" && heads.Count == TagProfile.Names.Length && heads.All(h => h.StartsWith("#", StringComparison.Ordinal)), "'" + pod + "'");
            int count = after.Count; Names.Keep("", after); Names.Keep("?", after); Names.Keep((string)null, after);
            Check("DR5", "Names.Keep adds a game name whole and skips an empty or unknown one ('?'); nothing renamed: the text comes back as it is",
                after.Count == count && after.Contains("Master Blaster") && Through(new Dictionary<string, string>(), after, why) == why);

            // the sources: the table reads the military cards (the game's stat powerups and Endless's) and the pod heads, the pure half is
            // linked here, and Ranker names a Research Pod card with that head
            string names = Src("Names.cs"), table = Src("Names.Table.cs"), ranker = Src("Ranker.cs"), proj = BenchFile("ItemBench.csproj");
            Check("DR5", "Names.EnsureTable keeps refs.statPowerups and refs.statPowerupsInfinite by name and the Research Pod heads; Names.Table.cs holds Compose / Swap; the bench links it; Ranker writes '#<type> +<n>'",
                names != null && names.Contains("foreach (var p in G.Each(refs.statPowerups)) Keep(p == null ? null : G.Name(p), all);") && names.Contains("foreach (var p in G.Each(refs.statPowerupsInfinite)) Keep(p == null ? null : G.Name(p), all);")
                && names.Contains("Keep(PodHeads(), all);") && names.Contains("internal static partial class Names") && !names.Contains("internal static void Compose(")
                && table != null && table.Contains("internal static void Compose(") && table.Contains("internal static string Swap(") && !table.Contains("Il2Cpp") && !table.Contains("PowerupBase")
                && ranker != null && ranker.Contains("c.Name = \"#\" + type + \" +\" + n;")
                && proj != null && proj.Contains("<Compile Include=\"..\\..\\YazsCompanion.Mod\\Names.Table.cs\" Link=\"mod\\Names.Table.cs\" />"));
        }

        /// <summary>The probe's military training cards (MilitaryTraining_* assets): each one that holds another game name as a word keeps its
        /// name whole when that name is lent (a made-up rename).</summary>
        static void MilitaryNames(List<ProbeItem> items, string probePath)
        {
            var military = new List<string>(); var game = new HashSet<string>(StringComparer.Ordinal);
            using (var doc = JsonDocument.Parse(File.ReadAllText(probePath)))
                foreach (var p in doc.RootElement.GetProperty("powerups").EnumerateArray())
                {
                    string name = p.GetProperty("name").GetString(); JsonElement a;
                    string asset = p.TryGetProperty("asset", out a) ? a.GetString() : "";
                    if ((asset ?? "").StartsWith("MilitaryTraining_", StringComparison.Ordinal)) military.Add(name); else game.Add(name);
                }
            foreach (var it in items) game.Add(it.Name);
            foreach (var cls in new[] { "SWAT", "Tank", "Engineer", "Huntress", "Ghost", "Medic", "Pyro", "Mechanic", "Ranger" }) game.Add(cls);
            var pairs = new List<string>(); var bad = new List<string>();
            foreach (var m in military)
                foreach (var g in game)
                {
                    if (g == m || g.Length >= m.Length || !Regex.IsMatch(m, "(?<![\\p{L}\\p{N}_])" + Regex.Escape(g) + "(?![\\p{L}\\p{N}_])")) continue;
                    pairs.Add(m + " / " + g);
                    var all = new HashSet<string>(game, StringComparer.Ordinal); Names.Keep(military, all);
                    string text = "Just ahead of " + m, swapped = Through(Renamed(g, "Shiny Pellets"), all, text);
                    if (swapped != text) bad.Add("'" + swapped + "'");
                }
            Check("DR5", "the probe's " + military.Count + " military training cards: every one that holds another game name keeps it whole when that name is lent (" + Show(pairs) + ")",
                military.Count == 19 && pairs.Count > 0 && bad.Count == 0, Show(bad));
        }

        // ---------------------------------------------------------------- DR7: R-07, the magazine words
        static void ClipWords()
        {
            int first = DeckRoom(1, 3.0), third = DeckRoom(3, 2.5);
            var pc = Eval("Magazine Clip", "", Deck1011(), Wording.Budget);
            var deck = Eval("Magazine Clip", "", Deck1011(), first);
            var reasons = WhyText.Reasons(new CardWords { Kind = SayKind.Item, Item = deck.Say }, deck.Why, deck.Line);
            Check("DR7", "the 10-11 chest of 04:47 replayed (Magazine Clip #1, the squad's guns on magazines): the item's words first, the condition as one - 'Doubles every magazine - your guns use magazines' on a PC card, "
                + "'Doubles every magazine' on the Deck's #1 (" + first + "), whose WHY says the condition",
                pc.Line == "Doubles every magazine - your guns use magazines" && deck.Line == "Doubles every magazine" && reasons.Contains("Doubles every magazine - your guns use magazines"),
                "'" + pc.Line + "' | '" + deck.Line + "' | WHY " + string.Join(" | ", reasons));

            // Last Round: its card leads with what the squad gets from it (a crit build's line), its WHY says what it does and when it works
            // (a WHY reason is built at a card's 50 characters: the book's words for it are short enough to keep the condition)
            var last = Eval("Last Round", "", Deck1011(), Wording.Budget);
            var lastWhy = WhyText.Reasons(new CardWords { Kind = SayKind.Item, Item = last.Say }, last.Why, last.Line);
            var lastDeck = Eval("Last Round", "", Deck1011(), first);
            const string lastNote = "The last bullet crits - your guns use magazines";
            Check("DR7", "Last Round (c:1614: 'The squad reloads magazines' in its WHY): '" + lastNote + "' in its WHY, the log's reason the same; where its own words are the card's line, 'The last bullet crits' on the Deck's #1",
                lastWhy.Contains(lastNote) && last.Why.Any(w => w.EndsWith(" the last bullet crits - your guns use magazines", StringComparison.Ordinal))
                && Drawn(first, () => Wording.Item(new ItemSay { Note = lastDeck.Say.Note, RuleNote = true, Shorter = lastDeck.Say.Shorter })) == "The last bullet crits",
                "'" + last.Line + "' | WHY " + string.Join(" | ", lastWhy));

            // Buckshot Roulette (c:1337): its own words; the condition where the line has room for it (64 characters in all: the log's reason);
            // its short form on the Deck's narrow card
            var shot = Eval("Buckshot Roulette", "", Deck1011(), Wording.Budget);
            var shotDeck = Eval("Buckshot Roulette", "", Deck1011(), third);
            Check("DR7", "Buckshot Roulette: 'Quicker cooldowns, 20% instant reloads' on a PC card, 'Quicker cooldowns and reloads' beside the Deck's '3RD' (" + third + "); the log's reason 'quicker cooldowns, 20% instant reloads - your guns use magazines'",
                shot.Line == "Quicker cooldowns, 20% instant reloads" && shotDeck.Line == "Quicker cooldowns and reloads" && shot.Why.Contains("quicker cooldowns, 20% instant reloads - your guns use magazines"),
                "'" + shot.Line + "' | '" + shotDeck.Line + "' | " + string.Join("; ", shot.Why));

            // no clip item says the condition alone or the old words, in any room or squad with a magazine; without one, the old reason stays
            var bad = new List<string>(); int n = 0;
            foreach (var name in new[] { "Magazine Clip", "Last Round", "Buckshot Roulette" })
                foreach (double clip in new[] { 1.0, 0.5, 0.2 })
                    foreach (int room in new[] { Wording.Budget, first, third, DeckRoom(2, 0.5) })
                    {
                        var cell = Eval(name, "", Deck1011(clip), room); n++;
                        var why = WhyText.Reasons(new CardWords { Kind = SayKind.Item, Item = cell.Say }, cell.Why, cell.Line);
                        foreach (var t in why.Concat(new[] { cell.Line }))
                            if (t == null || t.IndexOf("reloads magazines", StringComparison.OrdinalIgnoreCase) >= 0 || t.StartsWith(Wording.Cap(ItemRules.ClipWorks), StringComparison.Ordinal)
                                || Wording.EndsDangling(t))
                                bad.Add(name + " clip " + clip + " at " + room + ": '" + t + "'");
                    }
            var none = Eval("Magazine Clip", "", Deck1011(0), Wording.Budget);
            Check("DR7", "Magazine Clip, Last Round and Buckshot Roulette with 20 - 100 % of the squad on magazines, at a PC card and the Deck's rooms (" + n + " cards): no line or WHY reason is the condition alone or 'reloads magazines'; with no magazine on the squad: 'No weapon on the squad uses a magazine'",
                bad.Count == 0 && none.Line == "No weapon on the squad uses a magazine", Show(bad) + " | '" + none.Line + "'");
        }
    }
}
