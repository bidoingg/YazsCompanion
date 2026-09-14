using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace YazsCompanion
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BasePlugin
    {
        public const string GUID = "bidoi.yazs.companion";
        public const string NAME = "YAZS Companion";
        public const string VERSION = "0.5.1";
        public const string DefaultUpdateUrl = "https://github.com/bidoingg/YazsCompanion/releases/latest/download/latest.json";

        internal static ManualLogSource Logger;
        internal static ConfigEntry<bool> ShowBadges;
        internal static ConfigEntry<bool> ShowPanel;
        internal static ConfigEntry<float> PanelTop;
        internal static ConfigEntry<float> PanelRight;
        internal static ConfigEntry<bool> LogSquad;
        internal static ConfigEntry<bool> Verbose;
        internal static ConfigEntry<bool> AutoUpdate;
        internal static ConfigEntry<string> UpdateUrl;

        /// <summary>The folder the running DLL was loaded from (auto-updated builds live next to the original).</summary>
        internal static string PluginDir
        {
            get
            {
                try { var loc = Assembly.GetExecutingAssembly().Location; if (!string.IsNullOrEmpty(loc)) return Path.GetDirectoryName(loc); } catch { }
                return Path.Combine(Paths.PluginPath, "YazsCompanion");
            }
        }

        public override void Load()
        {
            Logger = Log;
            ShowBadges = Config.Bind("General", "ShowBadges", true, "Frame the recommended card, hang a RECOMMENDED ribbon under it and print a reason line under every offered card.");
            ShowPanel = Config.Bind("General", "ShowPanel", true, "Show the live PLAN sidebar during play (weapon line, ability to feed, next ability, SOS and item advice).");
            PanelTop = Config.Bind("General", "PanelTop", 780f, "Sidebar distance from the top of the screen, in canvas units (the canvas is 3840 x 2160).");
            PanelRight = Config.Bind("General", "PanelRight", 44f, "Sidebar distance from the right edge of the screen, in canvas units.");
            LogSquad = Config.Bind("Logging", "LogSquad", true, "Log the squad state (weapons, abilities with levels, items) with every offer.");
            Verbose = Config.Bind("Logging", "Verbose", false, "Also log every raw field of every card and survivor (for validating the ranking).");
            AutoUpdate = Config.Bind("Update", "AutoUpdate", true, "At every launch, fetch the release feed and download a newer mod build next to this one; it runs from the next launch on (a notice at the top of the screen says so). The older file is removed by the new build.");
            UpdateUrl = Config.Bind("Update", "UpdateUrl", DefaultUpdateUrl, "The latest.json of the release feed to check.");

            // BepInEx overwrites LogOutput.log on every launch; keep our own append-only copy next to the DLL.
            try { BepInEx.Logging.Logger.Listeners.Add(new FileListener(Path.Combine(PluginDir, "companion.log"))); }
            catch (Exception e) { Logger.LogWarning("file log unavailable: " + e.Message); }

            Updater.CleanupSiblings();
            // The update check touches no game object, so it runs before the hooks: a build the game has outgrown
            // (a renamed member fails its patch below) can still fetch the fixed build and ask for a restart.
            if (AutoUpdate.Value) Updater.CheckInBackground(UpdateUrl.Value, VERSION, PluginDir);
            else Logger.LogInfo("[update] auto-update is off (config)");

            var knowledge = Knowledge.Load(Path.Combine(PluginDir, "knowledge.json"));

            // One patch class at a time (what Harmony.PatchAll does, minus the shared fate): a hook the game
            // renamed logs its error and every other screen keeps working.
            var harmony = new Harmony(GUID);
            int failed = 0;
            foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                try { harmony.CreateClassProcessor(type).Patch(); }
                catch (Exception e) { failed++; Logger.LogError("patch " + type.Name + " failed (game update?): " + e.Message); }
            }
            int patched = 0;
            foreach (var _ in harmony.GetPatchedMethods()) patched++;
            Logger.LogInfo(NAME + " " + VERSION + " loaded from " + PluginDir + "; " + patched + " methods patched"
                + (failed > 0 ? " (" + failed + " patch classes FAILED, see above)" : "") + "; badges " + (ShowBadges.Value ? "on" : "off")
                + "; panel " + (ShowPanel.Value ? "on" : "off") + "; auto-update " + (AutoUpdate.Value ? "on" : "off") + "; knowledge from " + knowledge.Source
                + " (" + knowledge.ItemTier.Count + " items, " + knowledge.AbilityTier.Count + " abilities, " + knowledge.RescueTier.Count + " survivors)");
        }
    }

    /// <summary>Appends this plugin's own log lines (only) to a file, timestamped, flushed per line. Thread-safe: the updater logs from a worker.
    /// Bounded on long-lived installs: past MaxBytes the file is rotated to companion.log.1 (replacing the previous one) at launch.</summary>
    internal sealed class FileListener : ILogListener
    {
        const long MaxBytes = 2L * 1024 * 1024;
        readonly StreamWriter _w;
        readonly object _lock = new object();
        public FileListener(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Rotate(path);
            _w = new StreamWriter(path, true) { AutoFlush = true };
            _w.WriteLine("==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " session start ====");
        }
        public LogLevel LogLevelFilter { get { return LogLevel.All; } }
        public void LogEvent(object sender, LogEventArgs e)
        {
            if (e.Source == null || e.Source.SourceName != Plugin.NAME) return;
            lock (_lock) { try { _w.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " [" + e.Level + "] " + e.Data); } catch { } }
        }
        public void Dispose() { lock (_lock) { _w.Dispose(); } }

        static void Rotate(string path)
        {
            try
            {
                var f = new FileInfo(path);
                if (!f.Exists || f.Length < MaxBytes) return;
                string old = path + ".1";
                if (File.Exists(old)) File.Delete(old);
                File.Move(path, old);
            }
            catch { }
        }
    }
}
