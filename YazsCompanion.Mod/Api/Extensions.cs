// The extension point: the ONE public surface of the mod. Another BepInEx plugin can
//   - put options of its own into the mod menu (a MODS tab that exists only while an option is registered),
//   - lend build guides per survivor (a "build pack" file, see Builds.cs), which then stand next to the presets, and
//   - (ApiVersion 2) lend display names for classes, powerups and items: what the Companion SHOWS (the PLAN readout,
//     the card reasons, the BUILDS tab, the Training Yard strip) - the rules keep the game's own names (Names.cs).
// Nothing here knows who calls it, and the mod behaves exactly as before while nobody does.
//
// It is meant to be called through REFLECTION, without a reference to this DLL (a plugin that references it would
// fail to load where the Companion is not installed), so the signatures use BCL types only: strings, Func and Action.
// The caller should declare [BepInDependency("bidoi.yazs.companion", BepInDependency.DependencyFlags.SoftDependency)]
// so that this assembly is loaded before its own Load() looks for the type; the README has the snippet.
//
// Order and threads: a registration may come before this plugin's own Load() (no logger yet: the lines are kept and
// written then), before or after the menu exists, while it is open (the menu watches Version and rebuilds), and from
// any thread (one lock; callers get a copy). A call never throws back into the caller, and the callbacks of another
// mod are never trusted not to throw: each is caught where it is used, and each kind of failure is logged once.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace YazsCompanion.Api
{
    public static class Extensions
    {
        /// <summary>Raised when a signature here changes or one is added; check it before relying on a newer call.
        /// 1 = RegisterOption, RegisterBuildProvider, Unregister (0.12.0); 2 = RegisterDisplayNames, InvalidateDisplayNames
        /// (0.12.1). The calls of an older version stay as they were.</summary>
        public static int ApiVersion => 2;

        /// <summary>Add a choice option to the mod menu's MODS tab: a row with <paramref name="label"/> and a left / right value
        /// selector. <paramref name="owner"/> is the registering mod's display name (the section header),
        /// <paramref name="group"/> a sub-header under it (may be empty), <paramref name="description"/> the footer text while
        /// the row is in focus. <paramref name="choices"/> is asked every time the row is drawn or changed (the list may
        /// change at run time), <paramref name="get"/> gives the index shown, <paramref name="set"/> is called with the new
        /// index when the player changes the value - save it there; the menu reads <paramref name="get"/> again afterwards.
        /// Registering the same owner, group and label again replaces the row in place.</summary>
        public static void RegisterOption(string owner, string group, string label, string description, Func<string[]> choices, Func<int> get, Action<int> set)
        {
            try
            {
                owner = Clean(owner, "Another mod"); group = Clean(group, ""); label = Clean(label, "");
                if (label.Length == 0 || choices == null || get == null || set == null) { Once("option:" + owner + "/" + label, "'" + owner + "' registered an option without a label, choices, get or set: ignored", true); return; }
                var o = new Option { Owner = owner, Group = group, Label = label, Description = (description ?? "").Trim(), Choices = choices, Get = get, Set = set };
                bool replaced = false;
                lock (_lock)
                {
                    int i = _options.FindIndex(x => x.Key == o.Key);
                    if (i >= 0) { _options[i] = o; replaced = true; } else _options.Add(o);
                }
                Interlocked.Increment(ref _version);
                Log("'" + owner + "' " + (replaced ? "replaced" : "registered") + " the option '" + (group.Length > 0 ? group + " / " : "") + label + "'", false);
            }
            catch (Exception e) { Once("RegisterOption", "RegisterOption: " + e, true); }
        }

        /// <summary>Lend build guides. <paramref name="buildPackPathForClass"/> is asked with a survivor's name as builds.json
        /// has it (SWAT, Tank, Engineer, Huntress, Ghost, Medic, Pyro, Mechanic, Ranger) and answers the full path of a build
        /// pack JSON file for that survivor, or null for "none right now". It is asked whenever a survivor's build is
        /// resolved (an answer stands for a second) and every time the BUILDS tab is drawn, so the answer may change at
        /// run time; files are parsed once per path and write time. One provider per owner: a second call replaces it.</summary>
        public static void RegisterBuildProvider(string owner, Func<string, string> buildPackPathForClass)
        {
            try
            {
                owner = Clean(owner, "Another mod");
                if (buildPackPathForClass == null) { Once("provider:" + owner, "'" + owner + "' registered a build provider without a function: ignored", true); return; }
                bool replaced;
                lock (_lock)
                {
                    replaced = _providers.RemoveAll(p => p.Owner == owner) > 0;
                    _providers.Add(new Provider { Owner = owner, PathFor = buildPackPathForClass });
                    Builds.PackSource = PacksFor;
                }
                Builds.ForgetPacks(); Interlocked.Increment(ref _version);
                Log("'" + owner + "' " + (replaced ? "replaced its" : "registered a") + " build provider", false);
            }
            catch (Exception e) { Once("RegisterBuildProvider", "RegisterBuildProvider: " + e, true); }
        }

        /// <summary>Lend display names: what the Companion shows for a class, a powerup or an item, wherever it draws one (the
        /// PLAN readout, the reasons under the cards, the BUILDS tab, the Training Yard strip). The advice does not change:
        /// the rules, the builds and the log lines that review them keep the game's own names.
        /// <paramref name="nameFor"/>(kind, key) answers the name to show, or null for "the game's own":
        /// kind "class" with key = the game's class enum name (SWAT, Tank, Engineer, Huntress, Ninja - the class shown as
        /// Ghost -, Medic, Pyro, Mechanic, Ranger); kind "powerup" with key = the powerup's asset name (e.g. KatanaUpgrade);
        /// kind "item" with key = the item's asset name (e.g. Item_BloodyAxe). Plain text: angle brackets and line breaks
        /// are dropped, an empty answer counts as null.
        /// Every answer is kept - per class, powerup and item, for the run - so the function is asked about once per name
        /// and run, always on the game's main thread; call <see cref="InvalidateDisplayNames"/> when your answers change.
        /// Several mods may lend names: they are asked in the order they registered, the first answer that is not null
        /// wins. One function per owner: a second call replaces it and keeps its place in that order.</summary>
        public static void RegisterDisplayNames(string owner, Func<string, string, string> nameFor)
        {
            try
            {
                owner = Clean(owner, "Another mod");
                if (nameFor == null) { Once("names:" + owner + ":null", "'" + owner + "' registered display names without a function: ignored", true); return; }
                bool replaced;
                lock (_lock)
                {
                    var list = new List<Namer>(_namers);
                    int i = list.FindIndex(n => n.Owner == owner);
                    replaced = i >= 0;
                    var namer = new Namer { Owner = owner, NameFor = nameFor };
                    if (replaced) list[i] = namer; else list.Add(namer);
                    Volatile.Write(ref _namers, list.ToArray());
                }
                Interlocked.Increment(ref _namesVersion);
                Log("'" + owner + "' " + (replaced ? "replaced its" : "registered") + " display names", false);
            }
            catch (Exception e) { Once("RegisterDisplayNames", "RegisterDisplayNames: " + e, true); }
        }

        /// <summary>The names you lend have changed (another look chosen, say): everything the Companion kept is asked again,
        /// and the PLAN readout on screen is redrawn within two seconds. Cheap; callable from any thread.</summary>
        public static void InvalidateDisplayNames()
        {
            try { Interlocked.Increment(ref _namesVersion); }
            catch { }
        }

        /// <summary>Take back everything <paramref name="owner"/> registered: its options, its build provider and its display
        /// names. A survivor that followed one of its builds reads as Auto again; the game's names are shown again.</summary>
        public static void Unregister(string owner)
        {
            try
            {
                owner = Clean(owner, "Another mod"); int options, providers, namers;
                lock (_lock)
                {
                    options = _options.RemoveAll(o => o.Owner == owner);
                    providers = _providers.RemoveAll(p => p.Owner == owner);
                    if (_providers.Count == 0) Builds.PackSource = null;        // nobody left: the builds cost what they did before
                    var list = new List<Namer>(_namers);
                    namers = list.RemoveAll(n => n.Owner == owner);
                    if (namers > 0) Volatile.Write(ref _namers, list.ToArray());
                }
                if (options + providers + namers == 0) return;
                if (options + providers > 0) { Builds.ForgetPacks(); Interlocked.Increment(ref _version); }
                if (namers > 0) Interlocked.Increment(ref _namesVersion);
                Log("'" + owner + "' unregistered (" + options + " option(s), " + providers + " build provider(s)" + (namers > 0 ? ", its display names" : "") + ")", false);
            }
            catch (Exception e) { Once("Unregister", "Unregister: " + e, true); }
        }

        // ================================================================ the mod's side (internal)
        internal sealed class Option
        {
            public string Owner, Group, Label, Description;
            public Func<string[]> Choices; public Func<int> Get; public Action<int> Set;
            public string Key { get { return Owner + "/" + Group + "/" + Label; } }
        }
        sealed class Provider { public string Owner; public Func<string, string> PathFor; }
        sealed class Namer { public string Owner; public Func<string, string, string> NameFor; }

        static readonly object _lock = new object();
        static readonly List<Option> _options = new List<Option>();
        static readonly List<Provider> _providers = new List<Provider>();
        static Namer[] _namers = new Namer[0];          // replaced whole under the lock, read without it (Names asks from the main thread)
        static int _version, _namesVersion;

        /// <summary>Moves with every registration: the open menu compares it once a frame.</summary>
        internal static int Version { get { return Volatile.Read(ref _version); } }
        internal static bool HasOptions { get { lock (_lock) return _options.Count > 0; } }

        /// <summary>Moves whenever the display names may have changed (a provider came, went, or said so): Names.cs compares
        /// it on every lookup and forgets what it kept when it moved.</summary>
        internal static int NamesVersion { get { return Volatile.Read(ref _namesVersion); } }
        internal static bool HasDisplayNames { get { return Volatile.Read(ref _namers).Length > 0; } }

        /// <summary>The name another mod lends for (kind, key), the first answer in registration order; null = none. The
        /// provider's code is not trusted not to throw: a failure is logged once per owner and counts as no answer.</summary>
        internal static string DisplayName(string kind, string key)
        {
            var namers = Volatile.Read(ref _namers);
            for (int i = 0; i < namers.Length; i++)
            {
                string s;
                try { s = namers[i].NameFor(kind, key); }
                catch (Exception e) { Once("names:" + namers[i].Owner, "'" + namers[i].Owner + "': the display-name provider threw " + e.GetType().Name + " for " + kind + " '" + key + "': " + e.Message + " (logged once; no name from it)", true); continue; }
                s = Plain(s);
                if (s != null) return s;
            }
            return null;
        }

        // a name is drawn inside TMP rich text on one row: markup ("<b>...</b>") goes whole, a stray angle bracket and a line
        // break go too
        static string Plain(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            if (s.IndexOfAny(Unsafe) >= 0)
            {
                var sb = new System.Text.StringBuilder(s.Length);
                for (int i = 0; i < s.Length; i++)
                {
                    char ch = s[i];
                    if (ch == '<') { int end = s.IndexOf('>', i + 1); if (end > i) { i = end; continue; } }
                    if (ch == '\n' || ch == '\r' || ch == '\t') sb.Append(' ');
                    else if (ch != '<' && ch != '>') sb.Append(ch);
                }
                s = sb.ToString();
            }
            s = s.Trim();
            return s.Length > 0 ? s : null;
        }
        static readonly char[] Unsafe = { '<', '>', '\n', '\r', '\t' };

        /// <summary>A copy of the options, sections together: by owner, then by group, each in the order it first came - but
        /// an owner's rows without a group first, straight under its header (after a group they would read as part of it).</summary>
        internal static List<Option> Options()
        {
            List<Option> all; lock (_lock) all = _options.ToList();
            var owners = all.Select(o => o.Owner).Distinct().ToList();
            return all.OrderBy(o => owners.IndexOf(o.Owner))
                .ThenBy(o => o.Group.Length == 0 ? -1 : all.FindIndex(x => x.Owner == o.Owner && x.Group == o.Group)).ToList();      // OrderBy is stable: the rows keep their order
        }

        /// <summary>The choices of an option as they stand; empty when it has none or its mod threw.</summary>
        internal static string[] ChoicesOf(Option o)
        {
            try { return o.Choices() ?? new string[0]; }
            catch (Exception e) { Once("choices:" + o.Key, "'" + o.Owner + "', option '" + o.Label + "': choices() threw " + e.GetType().Name + ": " + e.Message, true); return new string[0]; }
        }

        /// <summary>The index an option shows, -1 when it is outside the choices or its mod threw.</summary>
        internal static int IndexOf(Option o, int count)
        {
            try { int i = o.Get(); return i >= 0 && i < count ? i : -1; }
            catch (Exception e) { Once("get:" + o.Key, "'" + o.Owner + "', option '" + o.Label + "': get() threw " + e.GetType().Name + ": " + e.Message, true); return -1; }
        }

        internal static string ValueOf(Option o)
        {
            var names = ChoicesOf(o); int i = IndexOf(o, names.Length);
            return i < 0 ? "-" : names[i] ?? "";
        }

        /// <summary>Step an option by <paramref name="d"/> (wrapping). False when its mod did not take the change.</summary>
        internal static bool Step(Option o, int d)
        {
            var names = ChoicesOf(o); int n = names.Length;
            if (n == 0) return false;
            int i = IndexOf(o, n), to = i < 0 ? 0 : ((i + d) % n + n) % n;
            try { o.Set(to); }
            catch (Exception e) { Once("set:" + o.Key, "'" + o.Owner + "', option '" + o.Label + "': set(" + to + ") threw " + e.GetType().Name + ": " + e.Message, true); return false; }
            Builds.ForgetPacks();       // an option of another mod may well be what decides which build pack it lends
            Log("'" + o.Owner + "', " + (o.Group.Length > 0 ? o.Group + " / " : "") + o.Label + " = " + ValueOf(o), false);
            return true;
        }

        // Builds.PackSource: every provider's answer for a survivor, as (owner, path)
        static List<KeyValuePair<string, string>> PacksFor(string survivor)
        {
            Provider[] providers; lock (_lock) providers = _providers.ToArray();
            var list = new List<KeyValuePair<string, string>>();
            foreach (var p in providers)
            {
                string path = null;
                try { path = p.PathFor(survivor); }
                catch (Exception e) { Once("path:" + p.Owner, "'" + p.Owner + "': the build provider threw " + e.GetType().Name + ": " + e.Message, true); }
                if (!string.IsNullOrEmpty(path)) list.Add(new KeyValuePair<string, string>(p.Owner, path));
            }
            return list;
        }

        static string Clean(string s, string fallback) { s = (s ?? "").Trim(); return s.Length > 0 ? s : fallback; }

        // ---- logging: possibly before Plugin.Load() has a logger, possibly from another thread
        static readonly List<KeyValuePair<string, bool>> _early = new List<KeyValuePair<string, bool>>();
        static readonly HashSet<string> _said = new HashSet<string>();

        static void Once(string key, string text, bool warning) { lock (_said) if (!_said.Add(key)) return; Log(text, warning); }

        static void Log(string text, bool warning)
        {
            try
            {
                var log = Plugin.Logger;
                if (log == null) { lock (_early) if (_early.Count < 200) _early.Add(new KeyValuePair<string, bool>(text, warning)); return; }
                if (warning) log.LogWarning("[ext] " + text); else log.LogInfo("[ext] " + text);
            }
            catch { }
        }

        /// <summary>From Plugin.Load(): the logger is there now - write what was registered before it.</summary>
        internal static void Attach()
        {
            KeyValuePair<string, bool>[] early; lock (_early) { early = _early.ToArray(); _early.Clear(); }
            foreach (var line in early) Log(line.Key + "  (before the Companion had loaded)", line.Value);
        }
    }
}
