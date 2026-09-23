// Display names: what the Companion SHOWS for a class, a powerup or an item. Another mod may lend names of its own
// (Api\Extensions.cs, RegisterDisplayNames). The rules never see them: the ranking, the builds, the plan's keys and the
// [card] / [pick] / [yard] log lines keep the game's English names and asset names, so the advice is the same whatever
// is shown (the [plan] line logs the readout as it is drawn). Only text drawn for the player goes through here -
//   - the PLAN readout asks by object while it builds its rows (Class, Of): exact, one lookup per name and run;
//   - the reasons under the cards, the BUILDS tab and the Training Yard strip are the rules' own English sentences and
//     guide texts: Text() swaps the game names in them, whole words only, and Name() swaps one name.
//
// While no mod lends names every call hands back the game's name at the cost of one int compare, so nothing drawn
// changes. With a provider: each class / powerup / item is asked once and its answer kept (per object for the run);
// Text() and Name() work from a table of every class, powerup and item name the game has, built once per run (in play
// at the first offer, a paused moment; in the menus when the BUILDS tab or the Training Yard first needs it; timed as
// `names.table` by [Debug] Perf), and a translated text is kept per input string. It is all forgotten when a provider
// comes or goes or says its names changed (InvalidateDisplayNames), when a run ends or a new one shows its HUD, and
// when the mod menu closes.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CT = GamePlayer.CharacterType;

namespace YazsCompanion
{
    internal static class Names
    {
        static int _version = int.MinValue;       // Extensions.NamesVersion the caches below belong to
        static bool _on;                          // some mod lends names
        static readonly string[] _classes = new string[(int)CT.NumCharacters];                        // the name to show per class, null = not asked yet
        static readonly Dictionary<IntPtr, string> _objects = new Dictionary<IntPtr, string>();       // powerup / item -> the name to show
        static readonly Dictionary<string, string> _asked = new Dictionary<string, string>(StringComparer.Ordinal);   // kind + key -> answer, null = none
        static Dictionary<string, string> _table;       // game name -> name to show, for Text(): what a provider renamed, and the longer names that contain one (unchanged)
        static Dictionary<string, string> _loose;       // normalized game name, an evolution's short form -> name to show, for Name()
        static Regex _rx;                               // every key of _table as a whole word, the longest first; null = nothing renamed
        static readonly Dictionary<string, string> _texts = new Dictionary<string, string>(StringComparer.Ordinal);
        static string[] _gameClass;                     // G.ClassName per class, once (ToString allocates)

        /// <summary>Moves when the names another mod lends may have changed: the readout on screen redraws when it did.</summary>
        public static int Version { get { return Api.Extensions.NamesVersion; } }

        /// <summary>Some mod lends names right now.</summary>
        public static bool Active { get { Sync(); return _on; } }

        static void Sync()
        {
            int v = Api.Extensions.NamesVersion;
            if (v == _version) return;
            _version = v; Clear(); _on = Api.Extensions.HasDisplayNames;
        }

        /// <summary>A run ended or a new one began, or the mod menu closed: ask the providers again from here on.</summary>
        public static void Forget() { if (_table != null || _objects.Count > 0 || _asked.Count > 0) Clear(); }

        static void Clear()
        {
            Array.Clear(_classes, 0, _classes.Length); _objects.Clear(); _asked.Clear(); _texts.Clear();
            _table = null; _loose = null; _rx = null;
        }

        static string Ask(string kind, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string k = kind + "\n" + key, d;
            if (_asked.TryGetValue(k, out d)) return d;
            d = Api.Extensions.DisplayName(kind, key);
            _asked[k] = d;
            return d;
        }

        static string GameClass(int i)
        {
            if (_gameClass == null) { var a = new string[(int)CT.NumCharacters]; for (int j = 0; j < a.Length; j++) a[j] = G.ClassName((CT)j); _gameClass = a; }
            return _gameClass[i];
        }

        // ---------------------------------------------------------------- by object: the PLAN readout
        /// <summary>The name to show for a class (Ghost for the class the game calls Ninja, unless a mod lends another).</summary>
        public static string Class(CT t)
        {
            Sync();
            int i = (int)t;
            if (!_on || i < 0 || i >= _classes.Length) return G.ClassName(t);
            return _classes[i] ?? (_classes[i] = Ask("class", t.ToString()) ?? GameClass(i));
        }

        /// <summary>The class as a label (the readout's label column, the BUILDS tab): upper case.</summary>
        public static string ClassLabel(CT t) { return Class(t).ToUpperInvariant(); }

        /// <summary>A survivor by the name builds.json and the rules use (SWAT, Tank, ... Ghost, Ranger).</summary>
        public static string Class(string survivor)
        {
            Sync();
            if (!_on || string.IsNullOrEmpty(survivor)) return survivor;
            for (int i = 0; i < _classes.Length; i++) if (string.Equals(GameClass(i), survivor, StringComparison.OrdinalIgnoreCase)) return Class((CT)i);
            return survivor;
        }

        /// <summary>The name to show for a powerup (its English name unless a mod lends another).</summary>
        public static string Of(PowerupBase p)
        {
            Sync();
            if (!_on || p == null) return G.Name(p);
            IntPtr key = Ptr(p); string d;
            if (key != IntPtr.Zero && _objects.TryGetValue(key, out d)) return d;
            d = Ask("powerup", G.Asset(p)) ?? G.Name(p);
            if (key != IntPtr.Zero) _objects[key] = d;
            return d;
        }

        /// <summary>The name to show for an item.</summary>
        public static string Of(ItemBase it)
        {
            Sync();
            if (!_on || it == null) return G.Name(it);
            IntPtr key = Ptr(it); string d;
            if (key != IntPtr.Zero && _objects.TryGetValue(key, out d)) return d;
            d = Ask("item", G.Asset(it)) ?? G.Name(it);
            if (key != IntPtr.Zero) _objects[key] = d;
            return d;
        }

        static IntPtr Ptr(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase o) { try { return o.Pointer; } catch { return IntPtr.Zero; } }

        // ---------------------------------------------------------------- by text: the rules' own sentences
        /// <summary><paramref name="s"/> with every game name a mod renamed swapped for the name it lends (whole words, the
        /// game's spelling and case). The same string back while nothing is renamed.</summary>
        public static string Text(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            Sync();
            if (!_on) return s;
            EnsureTable();
            if (_rx == null) return s;
            string r;
            if (_texts.TryGetValue(s, out r)) return r;
            try { r = Swap(s, _table, _rx); } catch { r = s; }
            if (_texts.Count >= 2048) _texts.Clear();
            _texts[s] = r;
            return r;
        }

        /// <summary>One name as the rules and builds.json write it (a class, a weapon, an ability, an evolution in full or as
        /// its short form, an item), forgiving case and punctuation: the name to show, or the name itself.</summary>
        public static string Name(string game)
        {
            if (string.IsNullOrEmpty(game)) return game;
            Sync();
            if (!_on) return game;
            EnsureTable();
            string d;
            if (_table.TryGetValue(game, out d)) return d;
            if (_loose.TryGetValue(Norm(game), out d)) return d;
            return game;
        }

        // every class, then every class's weapon line, abilities and their evolutions, then the game's lists of skill
        // powerups and items: the name each has and the name to show
        static void EnsureTable()
        {
            if (_table != null) return;
            long perf = Perf.Begin();
            var renamed = new Dictionary<string, string>(StringComparer.Ordinal);
            var loose = new Dictionary<string, string>(StringComparer.Ordinal);
            var all = new HashSet<string>(StringComparer.Ordinal);
            int asked = 0;
            try
            {
                using (G.Cache())
                {
                    for (int i = 0; i < _classes.Length; i++) Note(GameClass(i), Class((CT)i), false, renamed, loose, all);
                    var seen = new HashSet<IntPtr>();
                    Action<PowerupBase> visit = p =>
                    {
                        if (p == null) return;
                        IntPtr k = Ptr(p); if (k == IntPtr.Zero || !seen.Add(k)) return;
                        string game = G.Name(p); if (string.IsNullOrEmpty(game) || game == "?") return;
                        asked++;
                        Note(game, Of(p), G.IsEvolution(p), renamed, loose, all);
                    };
                    for (int i = 0; i < _classes.Length; i++)
                    {
                        ClassProperties cp = null; try { cp = G.PropsOf((CT)i); } catch { }
                        if (cp == null) continue;
                        try { foreach (var w in G.Each(cp.weaponPowerups)) visit(w); } catch { }
                        try
                        {
                            foreach (var a in G.Each(cp.abilityBasePowerups))
                            {
                                visit(a);
                                PowerupBase ea = null, eb = null; try { if (a != null) { ea = a.abilityEvolutionA; eb = a.abilityEvolutionB; } } catch { }
                                visit(ea); visit(eb);
                            }
                        }
                        catch { }
                    }
                    PowerupReferences refs = null; try { refs = PowerupReferences.Get; } catch { }
                    if (refs != null)
                    {
                        try { foreach (var p in G.Each(refs.skillPowerups)) visit(p); } catch { }
                        try
                        {
                            foreach (var it in G.Each(refs.items))
                            {
                                if (it == null) continue;
                                IntPtr k = Ptr(it); if (k == IntPtr.Zero || !seen.Add(k)) continue;
                                string game = G.Name(it); if (string.IsNullOrEmpty(game) || game == "?") continue;
                                asked++;
                                Note(game, Of(it), false, renamed, loose, all);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[names] " + e.Message); }

            Regex rx; Dictionary<string, string> table;
            try { Compose(renamed, all, out table, out rx); }
            catch (Exception e) { table = new Dictionary<string, string>(StringComparer.Ordinal); rx = null; Plugin.Logger.LogWarning("[names] no text swap: " + e.Message); }
            _table = table; _loose = loose; _rx = rx;
            Perf.End("names.table", perf);
            Plugin.Logger.LogInfo("[names] display names lent: " + renamed.Count + " of " + all.Count + " names (" + asked + " powerups and items asked)");
        }

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

        static void Note(string game, string shown, bool evolution, Dictionary<string, string> renamed, Dictionary<string, string> loose, HashSet<string> all)
        {
            if (string.IsNullOrEmpty(game)) return;
            all.Add(game);
            bool changed = !string.IsNullOrEmpty(shown) && shown != game;
            if (changed) { renamed[game] = shown; loose[Norm(game)] = shown; }
            if (!evolution || !changed) return;
            // an evolution also goes by the part after "Ability: " in builds.json and build packs ("Microbombs"): Name() finds
            // it. Not Text(): a short form is a common word often enough ("Taunt") and the sentences use the full name
            string gs = Short(game);
            if (gs.Length < 3 || gs == game) return;
            string n = Norm(gs); if (!loose.ContainsKey(n)) loose[n] = Short(shown);
        }

        static string Short(string evolution) { int i = evolution.IndexOf(':'); return i >= 0 ? evolution.Substring(i + 1).Trim() : evolution; }

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

        static string Norm(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s) if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }
    }
}
