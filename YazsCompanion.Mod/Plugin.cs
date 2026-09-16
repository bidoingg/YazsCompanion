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
        public const string VERSION = "0.5.5";
        public const string DefaultUpdateUrl = "https://github.com/bidoingg/YazsCompanion/releases/latest/download/latest.json";

        internal static ManualLogSource Logger;
        internal static ConfigEntry<bool> ShowBadges;
        internal static ConfigEntry<bool> ShowPanel;
        internal static ConfigEntry<float> PanelTop;
        internal static ConfigEntry<float> PanelRight;
        internal static ConfigEntry<float> PanelScale;
        internal static ConfigEntry<float> BadgeScale;
        internal static ConfigEntry<float> PanelHighlight;
        internal static ConfigEntry<bool> LogSquad;
        internal static ConfigEntry<bool> Verbose;
        internal static ConfigEntry<bool> AutoUpdate;
        internal static ConfigEntry<string> UpdateUrl;
        internal static ConfigEntry<bool> Screenshots;

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
            PanelScale = Config.Bind("General", "PanelScale", 0f, "Size multiplier of the sidebar. 0 = automatic: enlarged on small screens (Steam Deck) so its text stays about 15 px tall, 1 on a desktop monitor.");
            BadgeScale = Config.Bind("General", "BadgeScale", 0f, "Size multiplier of the RECOMMENDED ribbon and the reason lines under the cards. 0 = automatic (enlarged on small screens, up to 1.3).");
            PanelHighlight = Config.Bind("General", "PanelHighlight", 3f, "Seconds the sidebar lines whose advice changed (after a pick, a recruit or a Research Pod) glow gold before fading back to white. 0 = off.");
            LogSquad = Config.Bind("Logging", "LogSquad", true, "Log the squad state (weapons, abilities with levels, items) with every offer.");
            Verbose = Config.Bind("Logging", "Verbose", false, "Also log every raw field of every card and survivor (for validating the ranking).");
            AutoUpdate = Config.Bind("Update", "AutoUpdate", true, "At every launch, fetch the release feed and download a newer mod build next to this one; it runs from the next launch on (a notice at the top of the screen says so). The older file is removed by the new build.");
            UpdateUrl = Config.Bind("Update", "UpdateUrl", DefaultUpdateUrl, "The latest.json of the release feed to check.");
            Screenshots = Config.Bind("Debug", "Screenshots", false, "Save PNGs of the game frame into BepInEx\\plugins\\YazsCompanion\\shots at the moments that matter for checking the UI: offers, picks, sidebar changes (0.3 / 1.5 / 3.5 s into the highlight), its show/hide, and one a minute during play. For validating a build; off by default, at most 90 per session.");

            // BepInEx overwrites LogOutput.log on every launch; keep our own append-only copy next to the DLL.
            try { BepInEx.Logging.Logger.Listeners.Add(new FileListener(Path.Combine(PluginDir, "companion.log"))); }
            catch (Exception e) { Logger.LogWarning("file log unavailable: " + e.Message); }

            Updater.CleanupSiblings();
            var knowledge = Knowledge.Load(Path.Combine(PluginDir, "knowledge.json"));

            var harmony = new Harmony(GUID);
            harmony.PatchAll(typeof(Plugin).Assembly);
            int patched = 0;
            foreach (var _ in harmony.GetPatchedMethods()) patched++;
            Logger.LogInfo(NAME + " " + VERSION + " loaded from " + PluginDir + "; " + patched + " methods patched; badges " + (ShowBadges.Value ? "on" : "off")
                + "; panel " + (ShowPanel.Value ? "on" : "off") + "; auto-update " + (AutoUpdate.Value ? "on" : "off") + "; knowledge from " + knowledge.Source
                + " (" + knowledge.ItemTier.Count + " items, " + knowledge.AbilityTier.Count + " abilities, " + knowledge.RescueTier.Count + " survivors)");

            if (AutoUpdate.Value) Updater.CheckInBackground(UpdateUrl.Value, VERSION, PluginDir);
        }
    }

    /// <summary>Appends this plugin's own log lines (only) to a file, timestamped, flushed per line. Thread-safe: the updater logs from a worker.</summary>
    internal sealed class FileListener : ILogListener
    {
        readonly StreamWriter _w;
        readonly object _lock = new object();
        public FileListener(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
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
    }
}
