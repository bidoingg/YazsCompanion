// 0.16.1: the pure half of Names (Names.cs) - the swap table that Text() works from (Compose, Swap) and the names it keeps whole
// besides the ones the game's classes, powerups and items give. No game type: the offline bench links this file
// (tools\ItemBench\DeckRoundCases.cs, DR5).
//
// R-05 of the 10-11 Steam Deck round: live 10-11 01:21:20 (Military 09:34) the WHY of the #1 card read 'Just ahead of Master <a lent
// name>'. A character pack lends a name for the Engineer's weapon 'Blaster', and the #2 card was the military training card 'Master
// Blaster'. The table held every class, weapon, ability, evolution, skill powerup and item, but not the military cards, so Compose
// could not keep that name whole and its last word was swapped. The table now also holds the military training cards
// (Names.EnsureTable: the game's stat powerups, Endless's too) and the Research Pod cards (PodHeads), whole: no mod is asked for them.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace YazsCompanion
{
    internal static partial class Names
    {
        /// <summary>0.16.1 (R-05): the heads of the Research Pod cards' names as the Companion writes them ("#Kinetic" of "#Kinetic +10",
        /// Ranker.ScoreHashtag) - in the table each keeps every card of its type whole, whatever the amount.</summary>
        internal static IEnumerable<string> PodHeads() { return TagProfile.Names.Select(t => "#" + t); }

        /// <summary>0.16.1 (R-05): a card's game name into the table's set of every name, whole - nothing renamed, no mod asked (the military
        /// training cards, the Research Pod cards).</summary>
        internal static void Keep(string game, HashSet<string> all) { if (all != null && !string.IsNullOrEmpty(game) && game != "?") all.Add(game); }
        internal static void Keep(IEnumerable<string> games, HashSet<string> all) { if (games != null) foreach (var g in games) Keep(g, all); }

        /// <summary>The swap table and its pattern from what was renamed and every name the game has (pure: the offline
        /// check drives it). A longer game name that holds a renamed one as a word stays whole - "Grenade Trail" is not
        /// touched when only a "Grenade" was renamed - because the pattern tries the longest name first.</summary>
        internal static void Compose(Dictionary<string, string> renamed, IEnumerable<string> all, out Dictionary<string, string> table, out Regex rx)
        {
            table = new Dictionary<string, string>(renamed, StringComparer.Ordinal); rx = null;
            if (renamed.Count == 0) return;
            foreach (var g in all)
            {
                if (table.ContainsKey(g)) continue;
                foreach (var r in renamed.Keys) if (g.Length > r.Length && HasWord(g, r)) { table[g] = g; break; }
            }
            var keys = table.Keys.OrderByDescending(k => k.Length).ThenBy(k => k, StringComparer.Ordinal).Select(Regex.Escape);
            rx = new Regex("(?<![\\p{L}\\p{N}_])(?:" + string.Join("|", keys) + ")(?![\\p{L}\\p{N}_])", RegexOptions.CultureInvariant);
        }

        /// <summary>One text through a composed table (pure, as Compose).</summary>
        internal static string Swap(string s, Dictionary<string, string> table, Regex rx)
        {
            if (rx == null || string.IsNullOrEmpty(s)) return s;
            return rx.Replace(s, m => { string d; return table.TryGetValue(m.Value, out d) ? d : m.Value; });
        }

        static bool HasWord(string text, string word)
        {
            for (int i = text.IndexOf(word, StringComparison.Ordinal); i >= 0; i = text.IndexOf(word, i + 1, StringComparison.Ordinal))
            {
                int end = i + word.Length;
                if ((i == 0 || !IsWordChar(text[i - 1])) && (end >= text.Length || !IsWordChar(text[end]))) return true;
            }
            return false;
        }
        static bool IsWordChar(char c) { return char.IsLetterOrDigit(c) || c == '_'; }
    }
}
