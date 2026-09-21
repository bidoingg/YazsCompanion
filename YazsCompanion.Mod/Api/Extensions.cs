// The extension point: the ONE public surface of the mod. Another BepInEx plugin can
//   - put options of its own into the mod menu (a MODS tab that exists only while an option is registered), and
//   - lend build guides per survivor (a "build pack" file, see Builds.cs), which then stand next to the presets.
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
        /// <summary>Raised when a signature here changes or one is added; check it before relying on a newer call.</summary>
        public static int ApiVersion => 1;

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

        /// <summary>Take back everything <paramref name="owner"/> registered: its options and its build provider. A survivor
        /// that followed one of its builds reads as Auto again.</summary>
        public static void Unregister(string owner)
        {
            try
            {
                owner = Clean(owner, "Another mod"); int options, providers;
                lock (_lock)
                {
                    options = _options.RemoveAll(o => o.Owner == owner);
                    providers = _providers.RemoveAll(p => p.Owner == owner);
                    if (_providers.Count == 0) Builds.PackSource = null;        // nobody left: the builds cost what they did before
                }
                if (options + providers == 0) return;
                Builds.ForgetPacks(); Interlocked.Increment(ref _version);
                Log("'" + owner + "' unregistered (" + options + " option(s), " + providers + " build provider(s))", false);
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

        static readonly object _lock = new object();
        static readonly List<Option> _options = new List<Option>();
        static readonly List<Provider> _providers = new List<Provider>();
        static int _version;

        /// <summary>Moves with every registration: the open menu compares it once a frame.</summary>
        internal static int Version { get { return Volatile.Read(ref _version); } }
        internal static bool HasOptions { get { lock (_lock) return _options.Count > 0; } }

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
