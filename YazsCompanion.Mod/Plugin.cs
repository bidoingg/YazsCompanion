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
    public enum PanelPlace { BottomLeft, Right }
    public enum PanelDetailLevel { Compact, Full }

    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BasePlugin
    {
        public const string GUID = "bidoi.yazs.companion";
        public const string NAME = "YAZS Companion";
        public const string VERSION = "0.9.0";
        public const string DefaultUpdateUrl = "https://github.com/bidoingg/YazsCompanion/releases/latest/download/latest.json";

        internal static ManualLogSource Logger;
        internal static ConfigEntry<bool> ShowBadges;
        internal static ConfigEntry<bool> ShowPanel;
        internal static ConfigEntry<bool> ShowYard;
        internal static ConfigEntry<bool> Motion;
        internal static ConfigEntry<PanelPlace> PanelPosition;
        internal static ConfigEntry<PanelDetailLevel> PanelDetail;
        internal static ConfigEntry<float> PanelOpacity;
        internal static ConfigEntry<float> PanelIdle;
        internal static ConfigEntry<float> PanelLeft;
        internal static ConfigEntry<float> PanelBottom;
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
        internal static ConfigEntry<bool> PreviewFlag;
        internal static ConfigEntry<bool> PreviewYard;
        internal static ConfigEntry<string> PreviewResolution;

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
            ShowYard = Config.Bind("General", "ShowYard", true, "Training Yard advice: number the nodes worth buying with the points on hand (gold diamonds, in purchase order), ring the node to save for next, and print a PLAN strip under the tree (SPEND / THEN / WHY). Read-only: it never buys anything.");
            Motion = Config.Bind("General", "Motion", true, "Animate what the mod draws: Training Yard diamonds stamp in and the next purchase pings, rules draw themselves, the strip types on, the RECOMMENDED ribbon unfolds, the PLAN readout slides in and its title diamond spins when the advice changes. During play nothing loops. false = everything appears in place.");
            PanelPosition = Config.Bind("General", "PanelPosition", PanelPlace.BottomLeft, "Where the PLAN readout sits during play. BottomLeft = the empty corner under the weapon and ability icons (PanelLeft / PanelBottom); Right = the right edge between the item icons and the minimap (PanelRight / PanelTop), where it was up to 0.6.0.");
            PanelDetail = Config.Bind("General", "PanelDetail", PanelDetailLevel.Compact, "Compact = one row per survivor with only what to pick next (the weapon level to finish or the next tier, the ability to evolve, feed or take), then TAGS / SOS / GRAB cut short. Full = two rows per survivor with the next steps and the evolution names.");
            PanelOpacity = Config.Bind("General", "PanelOpacity", 0.42f, "Darkness of the soft backing under the PLAN text, 0 (none) to 1 (black). The backing dissolves towards the middle of the screen and has no frame, so the surroundings stay visible.");
            PanelIdle = Config.Bind("General", "PanelIdle", 0.7f, "Opacity of the whole PLAN readout while nothing has changed for a few seconds (it is at full strength right after a pick, a recruit or a pause). 1 = never dims.");
            PanelLeft = Config.Bind("General", "PanelLeft", 44f, "BottomLeft position: distance from the left edge of the screen, in canvas units (the canvas is 3840 x 2160).");
            PanelBottom = Config.Bind("General", "PanelBottom", 70f, "BottomLeft position: distance from the bottom of the screen, in canvas units.");
            PanelTop = Config.Bind("General", "PanelTop", 780f, "Right position: distance from the top of the screen, in canvas units.");
            PanelRight = Config.Bind("General", "PanelRight", 44f, "Right position: distance from the right edge of the screen, in canvas units.");
            PanelScale = Config.Bind("General", "PanelScale", 0f, "Size multiplier of the sidebar. 0 = automatic: enlarged on small screens (Steam Deck) so its text stays about 15 px tall, 1 on a desktop monitor.");
            BadgeScale = Config.Bind("General", "BadgeScale", 0f, "Size multiplier of the RECOMMENDED ribbon and the reason lines under the cards. 0 = automatic (enlarged on small screens, up to 1.3).");
            PanelHighlight = Config.Bind("General", "PanelHighlight", 3f, "Seconds the sidebar lines whose advice changed (after a pick, a recruit or a Research Pod) glow gold before fading back to white. 0 = off.");
            LogSquad = Config.Bind("Logging", "LogSquad", true, "Log the squad state (weapons, abilities with levels, items) with every offer.");
            Verbose = Config.Bind("Logging", "Verbose", false, "Also log every raw field of every card and survivor (for validating the ranking).");
            AutoUpdate = Config.Bind("Update", "AutoUpdate", true, "At every launch, fetch the release feed and download a newer mod build next to this one; it runs from the next launch on (a notice at the top of the screen says so). The older file is removed by the new build.");
            UpdateUrl = Config.Bind("Update", "UpdateUrl", DefaultUpdateUrl, "The latest.json of the release feed to check.");
            Screenshots = Config.Bind("Debug", "Screenshots", false, "Save PNGs of the game frame into BepInEx\\plugins\\YazsCompanion\\shots at the moments that matter for checking the UI: offers, picks, sidebar changes (0.3 / 1.5 / 3.5 s into the highlight), its show/hide, and one a minute during play. For validating a build; off by default, at most 90 per session.");
            PreviewFlag = Config.Bind("Debug", "Preview", false, "Show the PLAN sidebar with sample data on the main menu about 5 s after launch (two survivors, then the change highlight after a pick, then a full squad, then the fade-out) and save a screenshot of each stage into the shots folder whatever the Screenshots setting. For checking the design without playing a run; off by default.");
            PreviewYard = Config.Bind("Debug", "PreviewYard", false, "About 6 s after launch, open the Training Yard from the main menu, walk its tabs and save a screenshot of each into the shots folder, then go back. For checking the Training Yard advice without touching the controls; off by default.");
            PreviewResolution = Config.Bind("Debug", "PreviewResolution", "", "Screen to emulate in the preview captures, WIDTHxHEIGHT (3440x1440 for the PC look, 1280x800 for the Steam Deck look): the frames are rendered so the sidebar has the pixels it would have on that screen, whatever desktop the game runs on. Empty = as the game is running.");

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
