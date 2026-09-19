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
    public enum LevelUpStyle { WeaponFirst, Balanced, AbilitiesFirst }
    public enum Strength { Off, Normal, Strong }
    public enum RunGoal { WinTheRun, Balanced, FarmProgress }
    public enum CautionLevel { GlassCannon, Normal, Cautious }
    public enum RecruitPolicy { ByTheClock, AlwaysRecruit, LiberateFromHalfway }
    public enum TagStrategy { Auto, Spread, Fire, Electric, Chemical, Ice, Explosive, Kinetic, Slashing }

    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BasePlugin
    {
        public const string GUID = "bidoi.yazs.companion";
        public const string NAME = "YAZS Companion";
        public const string VERSION = "0.10.0";
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
        internal static ConfigEntry<bool> ProbeFlag;
        internal static ConfigEntry<bool> PreviewMenu;
        internal static ConfigEntry<bool> PreviewPause;
        internal static ConfigEntry<LevelUpStyle> AdviceStyle;
        internal static ConfigEntry<Strength> AdviceTiming;
        internal static ConfigEntry<bool> AdviceModeAware;
        internal static ConfigEntry<Strength> AdviceSynergy;
        internal static ConfigEntry<RunGoal> AdviceGoal;
        internal static ConfigEntry<CautionLevel> AdviceCaution;
        internal static ConfigEntry<TagStrategy> AdviceTags;
        internal static ConfigEntry<RecruitPolicy> AdviceRecruit;
        internal static ConfigEntry<bool> MenuButton;
        internal static ConfigEntry<string> MenuKey;

        /// <summary>Copy the [Advice] settings into the doctrine the ranking reads (at load, and whenever the menu changes one).</summary>
        internal static void ApplyDoctrine()
        {
            var d = Doctrine.Current;
            d.Style = AdviceStyle.Value == LevelUpStyle.WeaponFirst ? BuildStyle.Weapon : AdviceStyle.Value == LevelUpStyle.AbilitiesFirst ? BuildStyle.Ability : BuildStyle.Balanced;
            d.Timing = (int)AdviceTiming.Value;
            d.ModeAware = AdviceModeAware.Value;
            d.Synergy = (int)AdviceSynergy.Value;
            d.Farming = (int)AdviceGoal.Value;
            d.Caution = (int)AdviceCaution.Value;
            d.TagPlan = AdviceTags.Value.ToString();
            d.Recruit = (int)AdviceRecruit.Value;
        }

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

            AdviceStyle = Config.Bind("Advice", "LevelUpStyle", LevelUpStyle.Balanced, "How level-ups are split between the weapon and the abilities for survivors on Auto (a build selected in the mod menu brings its own style). WeaponFirst = every weapon level before any ability level (the rule up to 0.9); Balanced = each ability once early, then the weapon and the focus ability side by side; AbilitiesFirst = the abilities first, the weapon fills in. The human guides disagree on this, so it is yours to set.");
            AdviceTiming = Config.Bind("Advice", "Timing", Strength.Normal, "How strongly the run clock moves the advice. What pays back over the rest of the run (XP, luck, pickup range, a fresh ability, a recruit) is worth the most early and little near the end; what works at once keeps its value. Off = the clock is ignored.");
            AdviceModeAware = Config.Bind("Advice", "ModeAware", true, "Let the game mode and difficulty steer the advice: the horizon (20:00 Default, 10:00 Hardcore and Boss Rush, 5:00 One Hit, waves in Extermination, open-ended Endurance and Infinite), boss damage in Boss Rush, crowd control and no health picks in One Hit, more weight on survival in Hardcore and on higher difficulties.");
            AdviceSynergy = Config.Bind("Advice", "Synergy", Strength.Normal, "Weight of the live squad synergy: damage types shared across the squad (every level adds a tag point to each type it deals), tag specials within reach, team passives that boost grenades / turrets / taunts / deployables, and which evolution fits the squad.");
            AdviceGoal = Config.Bind("Advice", "RunGoal", RunGoal.WinTheRun, "What the run is for. WinTheRun = cash and XP bonuses fade with the clock; FarmProgress = cash, XP and luck keep their value to the end (the Training Yard is the goal), and a recruit close to a new rank counts for more.");
            AdviceCaution = Config.Bind("Advice", "Caution", CautionLevel.Normal, "How much survival picks (max health, armor, regeneration, healing) weigh.");
            AdviceTags = Config.Bind("Advice", "TagPlan", TagStrategy.Auto, "Damage type tags. Auto = stack the type the squad deals most; Spread = no stacking bonus; or name one type to always favour.");
            AdviceRecruit = Config.Bind("Advice", "Recruit", RecruitPolicy.ByTheClock, "SOS signals late in a timed run. ByTheClock = Liberate once a recruit no longer has the level-ups to grow; AlwaysRecruit; LiberateFromHalfway.");
            MenuButton = Config.Bind("Menu", "MenuButton", true, "Add a COMPANION button to the main menu and the pause menu that opens the mod menu (builds per survivor, advice settings, display settings). Mouse, keyboard and controller.");
            MenuKey = Config.Bind("Menu", "MenuKey", "F10", "Keyboard key that opens and closes the mod menu on the main menu and while paused (a UnityEngine.KeyCode name; empty = none).");
            PreviewMenu = Config.Bind("Debug", "PreviewMenu", false, "About 6 s after launch, open the mod menu on the main menu, walk its tabs and save a screenshot of each into the shots folder. For checking the menu without touching the controls; off by default.");
            PreviewPause = Config.Bind("Debug", "PreviewPause", false, "About 7 s after launch, start a Quick Run, pause it a few seconds in, open the mod menu over the pause menu, close it again and log what happened (with screenshots). For checking the pause-menu button without touching the controls. It plays under fifteen seconds, so the game writes no save; stop the game afterwards. Off by default.");
            ApplyDoctrine();
            Config.SettingChanged += (sender, args) => { try { if (args.ChangedSetting.Definition.Section == "Advice") ApplyDoctrine(); } catch { } };

            ProbeFlag = Config.Bind("Debug", "Probe", false, "About 6 s after launch, on the main menu, write probe.json next to the DLL: every item and powerup with the exact fields the game uses (tags, damage types per level, statistics, mode availability), the input actions and the main menu's button layout. For development; off by default.");

            // BepInEx overwrites LogOutput.log on every launch; keep our own append-only copy next to the DLL.
            try { BepInEx.Logging.Logger.Listeners.Add(new FileListener(Path.Combine(PluginDir, "companion.log"))); }
            catch (Exception e) { Logger.LogWarning("file log unavailable: " + e.Message); }

            Updater.CleanupSiblings();
            var knowledge = Knowledge.Load(Path.Combine(PluginDir, "knowledge.json"));
            Builds.Logger = s => Logger.LogInfo("[builds] " + s);
            Builds.Load(Path.Combine(PluginDir, "builds.json"));

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
