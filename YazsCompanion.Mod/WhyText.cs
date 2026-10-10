// WHY on highlight (0.14.0, roadmap item 3 of the 10-05 review): the words of the band under the cards while one is selected
// (WhyUi.cs draws them). A card's reason line says ONE thing, cut to the card's width; the player follows the #1 card on 96 -
// 100 % of the offers, so what was missing is the rest of the verdict and why the other cards come after it:
//     WHY   Experiment 21 goes first: The Medic build's main ability  /  Evolution locked in the Skill Tree  /  ...
//     CLOSE CALL   Either works - Experiment 21 is a hair ahead  /  Kinetic is 44% of your damage
//  - up to four reasons beyond the card's own line, from the verdict data the ranking already holds (CardWords, ItemSay,
//    RecruitSay - the facts Wording.Card picks its one line from) plus the few log-only reasons that read plainly once
//    translated; every one through Wording's rules (the game's words, a noun for every number, the card font's glyphs) and
//    the bench's rails, 64 characters at most (the band is as wide as the cards' row, a card's line has 50; the "vs #1"
//    sentence 80, it carries the first card's own line);
//  - one "vs #1" sentence: what the card ranked first has that this one lacks (its own line, else the first of its reasons
//    this card does not share; never its place in a build this card stands higher in), or for the first card how far ahead of
//    the second it is, in words - never a score;
//  - CLOSE CALL on the first and the second card when their scores, as the cards show them, are less than 0.20 apart (the
//    user's 10-05 offer at 00:18: Experiment 21 4.75, Medical Drone 4.72): "Either works - Medical Drone is a hair ahead" -
//    the band does not argue an order the scores barely make; on the very same score (0.16.0, C16-08) "Either works - even with
//    Minefield; this one is higher in the build's order" / "Either works - even with Bombing Strike, which is higher in the build's
//    order" - what settled the tie, said of the first card - and a third card with the same score says it too;
//  - a card a quest rule decided: the quest line first; on a lifted weapon the quest stands in for the build's order ("levels
//    abilities first" would contradict the pick), and on a card the quest makes AVOID the card's merits are left out.
//  - 0.16.0 (C16-01): what an item the squad holds does to the card ("held: ...") and what the card would do to what is held
//    ("item: ...") come right after the quest line, before the verdict facts (HeldRules.Prefix / ItemPrefix).
//  - the side panel of a wide screen (0.15.x, C-M1 of the 10-07 review) gets the same items with a shorter "vs #1" sentence: the
//    first card's own line stands under that card, so the panel names the decider alone ("Experiment 21 goes first", or with the
//    style in two words: "... goes first: weapons first") - WhyBlock.SideItems.
// The [card] log lines are not touched: [why] logs what the band drew.
// Pure (no game types): the offline bench sweeps the builders and replays the 10-05 offers.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace YazsCompanion
{
    /// <summary>One selected card and the two cards ranked first, as the WHY band needs them.</summary>
    internal sealed class WhyIn
    {
        public string Name = ""; public int Rank; public double Score; public string Shown = "";   // the card and its own line (without "2ND")
        public CardWords Say; public IList<string> Why;                                              // its verdict data and its log reasons
        public string FirstName; public double FirstScore = double.NaN; public string FirstShown;  // the card ranked first
        public string SecondName; public double SecondScore = double.NaN;                           // the card ranked second
        public CardWords FirstSay; public IList<string> FirstWhy;                                    // the first card's verdict data, for what it has that this one lacks
        public Func<string, string> Lend;                                                            // the names another mod lends (Names.Text; null: the game's)
        public string TieSay;   // 0.16.0 (C16-08): on an exact tie with the first card, what settled it, said of the first card (CardTies.Say: "higher in the build's order", "in the build's order", "the bigger bonus", "the type with more tags", "the type with fewer tags", "backed by more of your tags"); null: no tie, the position decided, or the rescue screen
    }

    /// <summary>What the band says for one card, in the order it packs it.</summary>
    internal sealed class WhyBlock
    {
        public bool Close;
        public string Lead = "WHY";                                     // "CLOSE CALL" on a close call
        public string Versus;                                           // the "vs #1" sentence (null: a card alone)
        public readonly List<string> Reasons = new List<string>();
        public readonly List<string> Items = new List<string>();        // Versus and Reasons in packing order
        // 0.15.x (C-M1 of the 10-07 review): the side panel's words - the "vs #1" sentence without the first card's own line (it stands
        // under that card already: "X goes first", "X goes first: weapons first"), then the reasons, in the same order
        public string SideVersus;
        public readonly List<string> SideItems = new List<string>();
    }

    internal static class WhyText
    {
        public const int Budget = 64;           // one item of the band
        public const int VersusBudget = 80;     // the "vs #1" sentence (it carries the first card's own line)
        public const int MaxReasons = 4;
        public const double CloseGap = 0.20;    // CLOSE CALL under this gap, the scores to the hundredth as the cards show them

        /// <summary>Two scores less than <see cref="CloseGap"/> apart, as the cards show them (to the hundredth).</summary>
        public static bool Close(double a, double b)
        {
            if (double.IsNaN(a) || double.IsNaN(b)) return false;
            return Math.Abs(Math.Round(a, 2) - Math.Round(b, 2)) < CloseGap - 1e-9;
        }

        /// <summary>The band's words for one card: the lead, the "vs #1" sentence and up to four reasons beyond its own line.</summary>
        public static WhyBlock Block(WhyIn x)
        {
            var b = new WhyBlock();
            if (x == null) return b;
            bool close; string side;
            b.Versus = Versus(x, out close, out side);
            b.SideVersus = side;
            b.Close = close;
            if (close) b.Lead = "CLOSE CALL";
            b.Reasons.AddRange(Reasons(x.Say, x.Why, x.Shown, Math.Round(x.Score, 2) < 1.0, true, x.Lend));
            // the first card's sentence says little (how far ahead): after its reasons; any other card's says why it is not first
            if (b.Versus != null && (x.Rank > 1 || close)) b.Items.Add(b.Versus);
            b.Items.AddRange(b.Reasons);
            if (b.Versus != null && x.Rank <= 1 && !close) b.Items.Add(b.Versus);
            if (b.SideVersus != null && (x.Rank > 1 || close)) b.SideItems.Add(b.SideVersus);
            b.SideItems.AddRange(b.Reasons);
            if (b.SideVersus != null && x.Rank <= 1 && !close) b.SideItems.Add(b.SideVersus);
            return b;
        }

        // ================================================================ the "vs #1" sentence
        /// <summary>The first card: how far ahead of the second it is ("Just ahead of Medical Drone"). Any other: what the first card
        /// has that this one lacks ("Experiment 21 goes first: The Medic build's main ability"); a close call says "Either works - ...".</summary>
        public static string Versus(WhyIn x, out bool close) { string side; return Versus(x, out close, out side); }

        /// <summary>As above; <paramref name="side"/>: the side panel's form (0.15.x, C-M1 of the 10-07 review). Where the band's sentence
        /// carries the first card's own line - which stands under that card already - the panel says the decider alone: "Experiment 21
        /// goes first", with the style in two words where that line names it ("... goes first: weapons first"); live 10-06 the whole
        /// sentence (63 - 79 characters) took two of the panel's lines and pushed a reason out. Any other sentence as the band says it,
        /// within <see cref="Budget"/>.</summary>
        public static string Versus(WhyIn x, out bool close, out string side)
        {
            close = false; side = null;
            if (x == null) return null;
            if (x.Rank <= 1)
            {
                if (string.IsNullOrEmpty(x.SecondName) || double.IsNaN(x.SecondScore)) return null;
                close = Close(x.Score, x.SecondScore);
                double gap = Math.Round(Math.Round(x.Score, 2) - Math.Round(x.SecondScore, 2), 2);
                string two = Named(x.SecondName, x.Lend, x.Name);
                // 0.16.0 (C16-08): the very same score - not "a hair ahead": say the tie, and what settled it (close is true at gap 0). The
                // middle form keeps the rule when the second card's shown name is 15 - 26 characters (a lent name of 16: the full form 82)
                if (gap == 0)
                {
                    side = Pick(Budget, x.TieSay != null ? "either works - this one is " + x.TieSay : null, "either works - even with " + two, "either works");
                    return Pick(VersusBudget, x.TieSay != null ? "either works - even with " + two + "; this one is " + x.TieSay : null,
                        x.TieSay != null ? "either works - even with " + two + "; " + x.TieSay : null, "either works - even with " + two, "either works");
                }
                if (close)
                {
                    side = Pick(Budget, "either works - a hair ahead of " + two, "either works");
                    return Pick(VersusBudget, "either works - a hair ahead of " + two, "either works");
                }
                string head = gap < 0.6 ? "just ahead of " : gap < 1.5 ? "ahead of " : "well ahead of ";
                side = Pick(Budget, head + two);
                return Pick(VersusBudget, head + two);
            }
            if (string.IsNullOrEmpty(x.FirstName) || double.IsNaN(x.FirstScore)) return null;
            bool tie = Math.Round(x.FirstScore, 2) == Math.Round(x.Score, 2);
            close = tie || (x.Rank == 2 && Close(x.FirstScore, x.Score));
            string one = Named(x.FirstName, x.Lend, x.Name);
            // 0.16.0 (C16-08): the very same score as the first card (the second, or a third of a three-way tie): the tie, and what put
            // the first card first - the middle form names that card as the subject, so the rule is never read as this card's
            if (tie)
            {
                side = Pick(Budget, x.TieSay != null ? "either works - " + one + " is " + x.TieSay : null, "either works - even with " + one, "either works");
                return Pick(VersusBudget, x.TieSay != null ? "either works - even with " + one + ", which is " + x.TieSay : null,
                    x.TieSay != null ? "either works - " + one + " is " + x.TieSay : null, "either works - even with " + one, "either works");
            }
            if (close)
            {
                side = Pick(Budget, "either works - " + one + " is a hair ahead", "either works");
                return Pick(VersusBudget, "either works - " + one + " is a hair ahead", "either works");
            }
            string lead = one + " goes first";
            bool onCard;
            var edge = Edge(x, out onCard);
            string brief = edge == null ? null : onCard ? Brief(edge) : edge;
            side = Pick(Budget, brief != null ? lead + ": " + brief : null, lead);
            return Pick(VersusBudget, edge != null ? lead + ": " + edge : null, lead);
        }

        /// <summary>The style a card's line names, in two words ("Level 2 of 4 - weapons first, the build's style" -&gt; "weapons first");
        /// null: none (the side panel's "vs #1" sentence then names the first card alone).</summary>
        public static string Brief(string shown)
        {
            string d = Decisive(shown ?? "");
            int comma = d.IndexOf(", ", StringComparison.Ordinal);
            string head = (comma > 0 ? d.Substring(0, comma) : d).ToLowerInvariant();
            foreach (var style in new[] { "weapons first", "abilities first" })
                if (head.Contains(style)) return style;
            return null;
        }

        // what the first card has that this one lacks: its own line (whole, then its decisive part), else the first of its reasons
        // this card does not share - never its place in a build this card stands higher in ("a core ability of the build" over the
        // build's main ability read as if the order were wrong); null: nothing sets it apart in words. <onCard>: it is the first card's
        // own line (or its decisive part), on screen under that card
        static string Edge(WhyIn x, out bool onCard)
        {
            onCard = false;
            var mine = new HashSet<string>();
            string myShown = (x.Shown ?? "").Trim();
            mine.Add(Key(myShown)); mine.Add(Key(Decisive(myShown)));
            foreach (var r in Reasons(x.Say, x.Why, null)) mine.Add(Key(r));
            var roles = new HashSet<string>();
            if (x.FirstSay != null && x.Say != null && x.FirstSay.Build != null && x.FirstSay.Build == x.Say.Build && x.Say.Priority >= 0 && x.FirstSay.Priority > x.Say.Priority)
                foreach (var f in Wording.Role(x.FirstSay.Build, x.FirstSay.Priority) ?? new string[0]) roles.Add(Key(f));
            string shown = (x.FirstShown ?? "").Trim();
            var candidates = new List<string> { shown, Decisive(shown) };
            candidates.AddRange(Reasons(x.FirstSay, x.FirstWhy, shown));
            for (int i = 0; i < candidates.Count; i++)
            {
                string c = candidates[i], k = Key(c);
                if (k.Length == 0 || mine.Contains(k) || roles.Contains(k) || roles.Contains(Key(Decisive(c)))) continue;
                onCard = i < 2;
                return c;
            }
            return null;
        }

        // a card's name as the band says it - of the name another mod lends when it lends one. 0.15.0 (C15-03 b): an evolution's own
        // part ("Bioweapon") only where <named> (this card's game name or base) names its base already; anywhere else the name whole,
        // as its card shows it (live 10-06 19:33:05: "Frost goes first" on a Sawblade Drone card named nothing the player could find)
        static string Named(string name, Func<string, string> lend, string named)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string shown = name;
            if (lend != null) { try { shown = lend(name) ?? name; } catch { shown = name; } }
            return shown.IndexOf(':') > 0 && Wording.BaseNamed(name, named) ? Wording.Short(shown) : shown;
        }

        /// <summary>The decisive part of a card's line: after a level or a kind ("Level 3 of 4 - evolution unlocks at level 4" -&gt;
        /// "evolution unlocks at level 4"), else the line up to its dash.</summary>
        public static string Decisive(string shown)
        {
            if (string.IsNullOrEmpty(shown)) return "";
            int dash = shown.IndexOf(" - ", StringComparison.Ordinal);
            if (dash < 0) return shown;
            string head = shown.Substring(0, dash);
            bool lead = head.StartsWith("Level ", StringComparison.Ordinal) || head == "Max level" || head == "Next weapon tier" || head == "Evolution" || head == "First weapon";
            return lead ? shown.Substring(dash + 3) : head;
        }

        // ================================================================ the reasons
        /// <summary>Up to four reasons of a card beyond its own line <paramref name="shown"/>, strongest first: its verdict data in the
        /// game's words (<paramref name="w"/>), then the log reasons that read plainly translated (<paramref name="why"/>). A reason
        /// the card's line already says, or that breaks the rails, is left out.</summary>
        public static List<string> Reasons(CardWords w, IList<string> why, string shown) { return Reasons(w, why, shown, false, true, null); }

        /// <summary>As above; <paramref name="avoid"/>: the card is scored under 1 (a quest that makes it AVOID leaves its merits out),
        /// <paramref name="cap"/>: capitalised as the band draws them (false: as written, for a line after a lead), <paramref name="lend"/>:
        /// the names another mod lends (an evolution's short name is taken of the lent name).</summary>
        public static List<string> Reasons(CardWords w, IList<string> why, string shown, bool avoid, bool cap, Func<string, string> lend)
        {
            var raw = new List<string>();
            bool questAvoid = false;
            if (w != null)
            {
                // a quest rule that decided the card (an item's and a recruit's ride on their own words)
                string quest = w.Quest ?? (w.Item != null ? w.Item.QuestLine : null) ?? (w.Recruit != null ? w.Recruit.Quest : null);
                if (quest != null)
                {
                    raw.Add(Wording.Quest(quest));
                    // under its AVOID the card's merits would read as praise for a pick the quest rules out
                    if (avoid) { raw.Add("whatever else it has, the quest rules it out"); questAvoid = true; }
                }
            }
            // 0.16.0 (C16-01): what a held item does to this card ('held: ') and what this card would do to what is held ('item: ') come
            // first - among the log reasons further down they would follow the verdict facts and fall past the fourth reason
            if (why != null && !questAvoid)
                foreach (var line in why)
                    if (line != null && (line.StartsWith(HeldRules.Prefix, StringComparison.Ordinal) || line.StartsWith(HeldRules.ItemPrefix, StringComparison.Ordinal)))
                        raw.Add(line.Substring(6));
            if (w != null)
            {
                if (!questAvoid)
                {
                    switch (w.Kind)
                    {
                        case SayKind.Ability: AbilityFacts(w, raw, lend); break;
                        case SayKind.Weapon: WeaponFacts(w, raw, lend); break;
                        case SayKind.NextTier: case SayKind.OtherBranch: case SayKind.FirstWeapon: raw.Add(Wording.Card(w, 1, null, null)); TagFacts(w, raw, true); break;
                        case SayKind.Evolution: EvolutionFacts(w, raw, lend); break;
                        case SayKind.Item: ItemFacts(w.Item, raw); break;
                        case SayKind.Tags: PodFacts(w, raw); break;
                        case SayKind.Stat: StatFacts(w, raw); break;
                        case SayKind.Recruit: RecruitFacts(w.Recruit, raw, shown); break;
                    }
                }
            }
            if (why != null && !questAvoid) foreach (var line in why) { var t = FromLog(line); if (t != null) raw.Add(t); }

            var list = new List<string>(); var seen = new HashSet<string>();
            string shownKey = Key(shown);
            foreach (var r in raw)
            {
                if (string.IsNullOrWhiteSpace(r)) continue;
                string s = Wording.Safe(r.Trim());
                if (cap) s = Wording.Cap(s);
                string k = Key(s);
                if (k.Length == 0 || !seen.Add(k)) continue;
                if (shownKey.Length > 0 && (shownKey == k || shownKey.Contains(k))) continue;      // the card's own line says it
                if (!Fits(s)) continue;
                list.Add(s);
                if (list.Count >= MaxReasons) break;
            }
            return list;
        }

        // ================================================================ two cards, one line
        /// <summary>Two cards of one offer drawing the same line tell the player nothing (10-05 19:12: Electric Turret and Energy Shield
        /// both "Max level - turns on the Electric 10-tag effect"; a chest of three "Fits the X build (healing)"): in rank order, a card
        /// whose line a higher card already draws says its next reason instead (<see cref="Alternative"/>). A line a quest rule decided
        /// stays - each of those cards counts for the quest, and the line says so. <paramref name="lines"/> is changed in place.</summary>
        public static void Distinct(IList<string> lines, IList<CardWords> says, IList<IList<string>> whys, IList<int> rooms, Func<string, string> lend)
        {
            if (lines == null) return;
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line) || taken.Add(line)) continue;
                var w = says != null && i < says.Count ? says[i] : null;
                if (w == null || w.Quest != null || (w.Item != null && (w.Item.QuestLine != null || w.Item.Quest)) || (w.Recruit != null && w.Recruit.Quest != null)) continue;
                string alt = Alternative(w, whys != null && i < whys.Count ? whys[i] : null, line, taken, rooms != null && i < rooms.Count ? rooms[i] : Wording.Budget, lend);
                if (alt != null) { lines[i] = alt; taken.Add(alt); }
            }
        }

        /// <summary>A card's line when a higher card on the offer draws <paramref name="shown"/> already: its next reason no card on the
        /// offer draws (<paramref name="taken"/>), after its level for an ability or a weapon ("Max level - its evolution comes next"),
        /// within <paramref name="room"/> visible characters as drawn; null: none fits (the line stays).</summary>
        public static string Alternative(CardWords w, IList<string> why, string shown, ICollection<string> taken, int room, Func<string, string> lend)
        {
            if (w == null) return null;
            string lead = (w.Kind == SayKind.Ability || w.Kind == SayKind.Weapon) && w.Level > 0 ? Wording.LevelText(w.Level + 1, w.Max) : null;
            foreach (var r in Reasons(w, why, shown, false, false, lend))
            {
                string line = lead != null ? lead + " - " + r : Wording.Cap(r);
                if (taken != null && taken.Contains(line)) continue;
                if (Wording.Rails(line, int.MaxValue).Count > 0) continue;
                string drawn = line;
                if (lend != null) { try { drawn = Wording.Safe(lend(line) ?? line); } catch { drawn = line; } }
                if (drawn.Length > room) continue;
                return line;
            }
            return null;
        }

        // lower-case letters, digits and single spaces: two wordings of one reason compare equal
        static string Key(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length); bool space = false;
            foreach (char ch in s)
            {
                if (char.IsLetterOrDigit(ch)) { if (space && sb.Length > 0) sb.Append(' '); sb.Append(char.ToLowerInvariant(ch)); space = false; }
                else space = true;
            }
            return sb.ToString();
        }

        // the first form that fits, as written: Say capitalises a line that stands alone, a reason may follow a level ("Max level - ...")
        static string First(string[] forms)
        {
            if (forms == null || forms.Length == 0) return null;
            string s = Wording.Say(null, forms), f0 = forms.FirstOrDefault(f => !string.IsNullOrEmpty(f));
            return !string.IsNullOrEmpty(s) && f0 != null && char.IsLower(f0[0]) ? char.ToLowerInvariant(s[0]) + s.Substring(1) : s;
        }

        // ---- abilities: the evolution, the build's order, what sets it apart, the squad's tags and team passives
        static void AbilityFacts(CardWords w, List<string> raw, Func<string, string> lend)
        {
            int next = w.Level + 1; bool last = w.Max > 0 && next >= w.Max;
            if (w.Level <= 0)
            {
                if (w.Reach < 0.6) raw.Add("little time left to level it (" + w.Clock + ")");
                else if (w.Owned < 4) raw.Add("fills an empty ability slot early");
            }
            else
            {
                if (w.EvoOwned) raw.Add(w.Reach >= 0.5 ? (last ? "its evolution comes next" : "evolution unlocks at level " + w.Max) : "too late to evolve it (" + w.Clock + ")");
                else if (w.EvoExists) raw.Add("evolution locked in the Skill Tree");
            }
            if (w.Build != null && w.Priority >= 0) raw.Add(First(Wording.Role(w.Build, w.Priority)));
            if (w.Head != null && w.HeadRank > 0) raw.Add(First(w.Head));
            if (w.Level > 0 && w.Focus) raw.Add("your top ability - keep feeding it");
            // ahead of the tags: what decided between this card and the weapon level on the offer (the band keeps four reasons)
            var r = w.Rival;
            // 0.15.0 (C15-03 a, the mirror): the first card, an ability, over a level of a weapon its style puts first - what decided
            if (w.Rank == 1 && r != null && r.Rank > 1 && r.Style == BuildStyle.Weapon && !r.Lifted && r.Quest == null) raw.Add(WeaponLower(r, lend));
            // 0.15.0 (C15-07): over its own survivor's weapon level because a lent build's own style says abilities first - said as the build's
            if (w.LentStyle && !w.MineStyle && w.Style == BuildStyle.Ability && w.Build != null && w.Rank > 0 && r != null && r.Build == w.Build && r.Rank > w.Rank)
                raw.Add(Within(lend, "abilities first, the " + w.Build + " build's style", "abilities first, the build's style"));      // 0.15.x: the cards' phrase (one for the idea everywhere)
            TagFacts(w, raw, true);
        }

        // ---- the weapon in hand: what this level does, the style, the clock, the tags
        static void WeaponFacts(CardWords w, List<string> raw, Func<string, string> lend)
        {
            int next = w.Level + 1; bool last = w.Max > 0 && next >= w.Max;
            if (w.Special != null) raw.Add(First(Wording.Special(w.Special)));
            if (last && w.Next != null) raw.Add("then the next tier: " + w.Next);
            if (w.Lifted) raw.Add(w.LiftLeft == null ? "your abilities are done" : "your abilities are nearly done");
            var r = w.Rival;
            if (w.Quest != null) raw.Add("the quest comes before the build's order");        // "levels abilities first" would contradict the lift
            // 0.15.0 (C15-03 a): the first card under "abilities first", not lifted - the style would contradict the order (live 10-06
            // 19:18:27: "The ... build levels abilities first" on the #1 Handgun); what decided is that the abilities on offer rank lower
            else if (w.Style == BuildStyle.Ability && !w.Lifted && w.Rank == 1) raw.Add(AbilitiesLower(w, lend));
            // ... and the mirror: under "weapons first" with an ability ranked above it, the style would argue for the card that lost -
            // the "vs #1" sentence and the clock say why
            else if (w.Style == BuildStyle.Weapon && w.Rank > 1 && r != null && r.Rank > 0 && r.Rank < w.Rank) { }
            else raw.Add(StyleLine(w, lend));
            if (w.Reach < 0.6 && !last) raw.Add("too late to finish it (" + w.Clock + ")");
            TagFacts(w, raw, false);
        }

        // the style a weapon level follows, in words: the build's ("the Medic build levels abilities first"), a lent build's own followed
        // through Auto (0.15.0, C15-07: "abilities first, the Medic build's style" - 0.15.x: the cards' phrase, it said "build's own style"), the player's ("your style levels abilities first" -
        // up to 0.14.0 it read "your level-up style: abilities first", which the rails leave out: the reason never showed)
        static string StyleLine(CardWords w, Func<string, string> lend)
        {
            string order = w.Style == BuildStyle.Ability ? "abilities first" : w.Style == BuildStyle.Weapon ? "weapons first" : null;
            bool build = w.Build != null && !w.MineStyle;
            if (w.LentStyle && build) return Within(lend, (order ?? "balanced") + ", the " + w.Build + " build's style", (order ?? "balanced") + ", the build's style");
            if (order == null) return "weapon and abilities side by side";
            return build ? "the " + w.Build + " build levels " + order : "your style levels " + order;
        }

        // 0.15.0 (C15-03 a): why the first card, a weapon level under "abilities first", stands over every ability on the offer - the best
        // of them has a low place in the same build ("Resuscitation is 4th in the Medic build's order"), none, or the build skips it;
        // else plainly that they rank lower here
        static string AbilitiesLower(CardWords w, Func<string, string> lend)
        {
            const string plain = "the abilities on offer rank lower here";
            var r = w.Rival;
            if (r == null) return "no ability on this offer";
            if (string.IsNullOrEmpty(r.Name) || w.Build == null || r.Build != w.Build) return plain;
            string a = r.Name, b = w.Build;
            if (r.HeadRank >= 6) return Within(lend, "the " + b + " build skips " + a, "the build skips " + a, plain);
            if (r.Priority >= 3) return Within(lend, a + " is " + Ord(r.Priority + 1) + " in the " + b + " build's order", a + " is " + Ord(r.Priority + 1) + " in the build's order", plain);
            if (r.Priority < 0) return Within(lend, a + " is not in the " + b + " build", a + " is not in the build", plain);
            return plain;
        }

        // 0.15.0 (C15-03 a, the mirror): why an ability stands over a level of a weapon whose style puts weapons first - the clock as a rule
        // (a weapon that can no longer be finished falls to the balanced floor)
        static string WeaponLower(CardWords r, Func<string, string> lend)
        {
            bool last = r.Max > 0 && r.Level + 1 >= r.Max;
            string name = string.IsNullOrEmpty(r.Name) ? null : r.Name;
            if (r.Reach < 0.6 && !last)
                return name != null ? Within(lend, "too late to finish " + name + " (" + r.Clock + ")", "too late to finish " + name, "too late to finish the weapon") : "too late to finish the weapon";
            return name != null ? Within(lend, "the " + name + " level ranks lower here", "the weapon level ranks lower here") : "the weapon level ranks lower here";
        }

        // the first form within a card line's 50 visible characters as the band draws it (the names another mod lends swapped in); the
        // last form when none is
        static string Within(Func<string, string> lend, params string[] forms)
        {
            string last = null;
            foreach (var f in forms)
            {
                if (string.IsNullOrEmpty(f)) continue;
                last = f;
                string drawn = f;
                if (lend != null) { try { drawn = lend(f) ?? f; } catch { drawn = f; } }
                if (Wording.Safe(drawn).Length <= Wording.Budget) return f;
            }
            return last;
        }

        static string Ord(int n)
        {
            int t = n % 100;
            string suffix = t >= 11 && t <= 13 ? "th" : n % 10 == 1 ? "st" : n % 10 == 2 ? "nd" : n % 10 == 3 ? "rd" : "th";
            return n + suffix;
        }

        // ---- an evolution: the build's pick (or, picking none, the base's place in the build), what it adds, the tags
        static void EvolutionFacts(CardWords w, List<string> raw, Func<string, string> lend)
        {
            // 0.16.0 (C16-17): taking this one closes the base's other evolution, the one the PLAN names as preferred (Ranker.ScoreEvolution
            // sets Closes only when the build picks none) - first, so a cut band keeps it; the log's own "closes ..." reason is not translated
            if (w.Closes != null && w.Pick == null)
                raw.Add(Pick(Budget, "closes " + Named(w.Closes, lend, w.Base) + " - the PLAN's preferred one", "closes the PLAN's preferred evolution"));
            if (w.Pick != null)
            {
                string pick = Named(w.Pick, lend, w.Base);          // the card is the base's evolution: its title names the base
                raw.Add(w.Mine ? "the " + w.Build + " build's evolution" : !w.PickOffered ? pick + " isn't offered - this one still evolves it" : "the " + w.Build + " build takes " + pick + " instead");
            }
            var role = Wording.Evolves(w);                          // 0.15.0 (C15-03 c)
            if (role != null) raw.Add(First(role));
            if (w.Adds != null) raw.Add(w.AddsNew || string.IsNullOrWhiteSpace(w.AddsWith) ? "adds " + w.Adds + " - new to the squad" : "adds " + w.Adds + ", like your " + w.AddsWith);
            TagFacts(w, raw, true);
        }

        // what the squad's damage type tags and team passives say (Wording.Tags filled them)
        static void TagFacts(CardWords w, List<string> raw, bool special)
        {
            if (special && w.Special != null) raw.Add(First(Wording.Special(w.Special)));
            if (w.ShortType != null) raw.Add(w.ShortType + " is " + w.Short + " tags from its 10-tag effect");
            if (w.ShareType != null) raw.Add(First(Wording.Share(w.ShareType, w.SharePct)));
            if (w.Boost != null) raw.Add(First(Wording.Boosted(w.Boost, null)));
        }

        // ---- an item: every reason Wording.Item would say, strongest first - it is asked again with each reason it gave taken out
        static void ItemFacts(ItemSay s, List<string> raw)
        {
            if (s == null) return;
            var copy = Copy(s);
            for (int i = 0; i < 10; i++)
            {
                string line = Wording.Item(copy);
                if (string.IsNullOrEmpty(line) || line == "Nothing in it for this squad") break;
                raw.Add(line);
                if (!Peel(copy)) break;
            }
        }

        static ItemSay Copy(ItemSay s)
        {
            var c = new ItemSay { Quest = s.Quest, Held = s.Held, QuestLine = s.QuestLine, Lead = s.Lead, Tier = s.Tier, Note = s.Note, RuleNote = s.RuleNote, NoteSay = s.NoteSay,
                Lacks = s.Lacks, Want = s.Want, WantBuild = s.WantBuild, Scaling = s.Scaling, Clock = s.Clock, Keyword = s.Keyword, KeywordAxis = s.KeywordAxis, KeywordValue = s.KeywordValue, Survival = s.Survival,
                LeadForms = s.LeadForms, HeldCost = s.HeldCost };     // 0.16.0 (C16-01)
            c.Boosts.AddRange(s.Boosts); c.Hurts.AddRange(s.Hurts); c.Nobody.AddRange(s.Nobody);
            return c;
        }

        // takes out the reason Wording.Item says first (its own order); false: nothing left to take
        static bool Peel(ItemSay s)
        {
            if (s.QuestLine != null) { s.QuestLine = null; return true; }
            if (s.Quest) { s.Quest = false; return true; }
            if (s.Held) { s.Held = false; return true; }
            if (s.LeadForms != null) { s.LeadForms = null; return true; }      // 0.16.0 (C16-01): Wording.Item says it before the Lead
            if (s.Lead != null) { s.Lead = null; return true; }
            if (s.Hurts.Count > 0) { s.Hurts.Clear(); return true; }
            var typed = s.Boosts.Where(b => b.Typed).OrderByDescending(b => b.Weight).FirstOrDefault();
            if (typed != null) { s.Boosts.Remove(typed); return true; }
            if (s.Want != null) { s.Want = null; return true; }
            if (s.Nobody.Count > 0) { s.Nobody.Clear(); return true; }
            if (s.Lacks != null) { s.Lacks = null; return true; }
            var other = s.Boosts.OrderByDescending(b => b.Weight).FirstOrDefault();
            if (other != null) { s.Boosts.Remove(other); return true; }
            if (s.Scaling != null) { s.Scaling = null; return true; }
            if (!string.IsNullOrEmpty(s.Note)) { s.Note = null; s.NoteSay = null; return true; }
            if (s.Tier != null) { s.Tier = null; return true; }
            if (s.Survival != null) { s.Survival = null; return true; }
            if (s.Keyword != null) { s.Keyword = null; return true; }
            return false;
        }

        // ---- a Research Pod: where the type stands on the squad
        static void PodFacts(CardWords w, List<string> raw)
        {
            var p = w.Tags; string type = w.Type;
            if (p == null || !p.Known || string.IsNullOrEmpty(type)) return;
            int cur = p.PointsOf(type), pct = (int)Math.Round(p.Share(type) * 100);
            string tags = "your squad has " + cur + " " + type + (cur == 1 ? " tag" : " tags");
            if (cur > 0) raw.Add(p.SpecialAt <= 0 ? tags : cur < p.SpecialAt ? tags + " - " + (p.SpecialAt - cur) + " short of its 10-tag effect" : tags + " - its 10-tag effect is on");
            if (pct > 0) raw.Add(First(Wording.Share(type, pct)));
            else raw.Add("nobody on the squad deals " + type);
            string focus = p.Focus();
            if (!string.IsNullOrEmpty(focus) && !string.Equals(focus, type, StringComparison.OrdinalIgnoreCase)) raw.Add("your highest tag is " + focus);
        }

        // ---- a stat card: the build that wants it, the note, who gets it
        static void StatFacts(CardWords w, List<string> raw)
        {
            string label = string.IsNullOrEmpty(w.Stat) ? "this stat" : w.Stat;
            if (w.WantBuild != null) raw.Add("the " + w.WantBuild + " build wants " + label);
            if (w.StatWhy != null) raw.Add(First(w.StatWhy));
            raw.Add(w.Team ? "for the whole team" : "for one survivor");
        }

        // ---- a rescue card: everything that speaks for the recruit, in the card's order
        static void RecruitFacts(RecruitSay r, List<string> raw, string shown)
        {
            if (r == null) return;
            if (r.Liberate)
            {
                raw.Add(r.Full ? "the squad is full" : r.Late != null ? "too late for a recruit to grow (" + r.Late + ")" : HeldRules.LiberateWhat(r.LiberateLevelUps, r.CashHeals, "a level-up") + " instead of a recruit");
                return;
            }
            if (r.Late != null) raw.Add("little time left for a recruit to grow (" + r.Late + ")");
            var partners = r.Partners.Distinct().ToList();
            if (r.Bought > 0) raw.Add((r.Bought == 1 ? "unlocked synergy with " : r.Bought + " unlocked synergies with ") + (partners.Count > 0 ? And(partners) : "your squad"));
            if (r.SharedType != null) raw.Add("also deals " + r.SharedType + (r.SharedWith != null ? ", like your " + r.SharedWith : ""));
            if (r.TeamBonus != null) raw.Add("team bonus: " + r.TeamBonus);
            if (r.Boosts != null) raw.Add(r.Boosts + " would boost " + r.Covered + " of your powerups");
            // 0.15.x (the 10-07 review): not where the card's own line names the tier already ("A-tier, also deals Slashing ...")
            string tier = string.IsNullOrEmpty(r.Tier) ? null : r.Tier.Trim().ToUpperInvariant() + "-tier";
            if (tier != null && (shown ?? "").IndexOf(tier, StringComparison.OrdinalIgnoreCase) < 0) raw.Add(tier + " recruit in the guides");
            // live check 2026-10-05 (rescue shot): next to a card that says "Unlocked synergy with X" the plain count read as a contradiction
            string more = r.Bought > 0 ? " more" : "";
            if (r.Unbought > 0) raw.Add(r.Unbought == 1 ? "1" + more + " synergy with your squad, not unlocked yet" : r.Unbought + more + " synergies with your squad, not unlocked yet");
            if (r.RankLevels > 0) raw.Add(Wording.RankAway(r.RankLevels, r.Rank));
        }

        static string And(IList<string> names) { return names.Count <= 1 ? (names.Count == 1 ? names[0] : "") : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1]; }

        /// <summary>A log reason that reads plainly once translated (null: one the verdict data says better, or the log's alone - the
        /// "Skill Tree 3/5" of a boosted ability the game's own label shows on the selected card).</summary>
        public static string FromLog(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            string s = line.Trim();
            const string Recruit = "synergy if you recruit ";
            if (s.StartsWith(Recruit, StringComparison.Ordinal)) return "a synergy unlocks if you recruit " + s.Substring(Recruit.Length);
            if (s.StartsWith("style: balanced - before another weapon level", StringComparison.Ordinal)) return "balanced style: before another weapon level";
            if (s == "healing: survival matters now") return "healing - survival matters now";
            if (s == "healing means nothing in One Hit") return s;
            if (s.EndsWith("-tier ability in the guides", StringComparison.Ordinal)) return s.Substring(0, s.Length - "ability in the guides".Length) + "in the guides";
            const string After = "abilities first: after ", Rest = ", ahead of the rest";
            if (s.StartsWith(After, StringComparison.Ordinal) && s.EndsWith(Rest, StringComparison.Ordinal) && s.Length > After.Length + Rest.Length)
                return "after " + s.Substring(After.Length, s.Length - After.Length - Rest.Length) + ", before the other survivors' abilities";
            if (s == "stacks, already held") return "already held - it stacks";
            return null;
        }

        // ================================================================ the rails and the packing
        /// <summary>A WHY item keeps Wording's rails (its 50 characters aside) and <see cref="Budget"/> characters (the "vs #1" sentence
        /// <see cref="VersusBudget"/>).</summary>
        public static bool Fits(string item, int budget = Budget)
        {
            if (string.IsNullOrEmpty(item) || item.Length > budget) return false;
            foreach (var bad in Wording.Rails(item)) if (!bad.StartsWith("length ", StringComparison.Ordinal)) return false;
            return true;
        }

        // the first form within the budget, in the card font's glyphs, capitalised (null: none fits)
        static string Pick(int budget, params string[] forms)
        {
            foreach (var f in forms)
            {
                if (string.IsNullOrEmpty(f)) continue;
                string s = Wording.Cap(Wording.Safe(f));
                if (Fits(s, budget)) return s;
            }
            return null;
        }

        /// <summary>The items in lines of <paramref name="lineWidth"/>, in order: the first line carries the lead
        /// (<paramref name="leadWidth"/>, 0: none), a separator (<paramref name="sepWidth"/>) between two items; an item that does
        /// not fit starts the next line; what is left after <paramref name="maxLines"/> lines - or wider than a line alone - is
        /// left out. Each line: the indexes of its items.</summary>
        public static List<List<int>> Pack(IList<float> widths, float leadWidth, float sepWidth, float lineWidth, int maxLines)
        {
            var lines = new List<List<int>>();
            if (widths == null || maxLines <= 0) return lines;
            var cur = new List<int>(); float used = leadWidth;
            for (int i = 0; i < widths.Count; i++)
            {
                float w = widths[i];
                float add = (used > 0 ? sepWidth : 0) + w;
                if (used + add <= lineWidth) { cur.Add(i); used += add; continue; }
                if (cur.Count == 0) continue;                       // too wide even for the line it would open: left out
                lines.Add(cur);
                if (lines.Count >= maxLines) return lines;
                cur = new List<int>(); used = 0;
                if (w <= lineWidth) { cur.Add(i); used = w; }
            }
            if (cur.Count > 0) lines.Add(cur);
            return lines;
        }
    }
}
