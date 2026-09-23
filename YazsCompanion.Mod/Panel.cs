// The live PLAN readout during play, drawn like the HUD's own quest tracker rather than like a menu panel: a small
// gold title over a hairline that fades out, then the rows - a label column (the survivor's name, TAGS / SOS / GRAB)
// and a value column that wraps under itself - on a soft dark backing that dissolves towards the middle of the
// screen and has no frame, so the surroundings stay readable (0.6.0's framed, nearly opaque 800-unit panel hid too
// much of the field). It sits in the empty bottom-left corner under the weapon and ability icons by default (the
// right edge between the items and the minimap is still available), is only as wide as its text, fades in and out,
// and dims to PanelIdle when nothing has changed for a few seconds. Motion (Fx.cs) only when it matters: on
// appearing the rule draws itself, the title diamond spins in and the groups slide in from the edge; when the advice
// changes the diamond spins and pings once. Nothing loops over the field. Compact detail (the default) is one row per
// survivor; Full keeps two rows per survivor with a hairline between the groups.
//
// Ticked from the HUD's own Update (UIGameplay, the object that owns the gameplay canvas) and, as a
// fallback, from GameplayMaster.Update; parented to that canvas so it dies with the HUD. The plan is
// rebuilt only when the squad state changes (a cheap key: squad text + active quest + tag points), because a
// rebuild walks every skill tree node and every item.
//
// Gating, validated on the Steam Deck log of 0.5.0 (2026-09-14): during play every flag reads false
// (players=1 active=True paused=False pauseMenu=False defeat=False hudVisible=False selecting=False).
// The pause menu sets IsPaused + IsPauseMenuFlowActive + IsGameplayUIVisible(); a selection screen sets
// IsPaused + IsDisplayingUpgradeSelection() (and our own tracker, which clears on the pick while the
// screen still animates out); the end of the run sets IsGameplayUIVisible(), then IsDefeatResultsFlowActive,
// then gamePlayers drops to 0. So IsGameplayUIVisible() means "a UI view is showing", NOT "the HUD is
// visible" (0.4.1 hid the sidebar on its inverse, hence the results-screen-only sightings). The panel hides
// on any of them and shows otherwise; the flags are still logged whenever they change.
//
// Size: the canvas is 3840 units wide on every screen (3840x2400 on the Deck's 1280x800, so one unit is a
// third of a pixel there), hence the automatic scale that keeps the text at a readable pixel size.
using System;
using System.Collections.Generic;
using System.Text;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YazsCompanion
{
    // Change highlight: every plan line has a stable key (Tank.weapon, Tank.ability, tags, sos, grab). When a rebuild
    // changes a line's text (a pick, a recruit, a Research Pod), that line's value is rendered gold and eases back to
    // white over PanelHighlight seconds (30 fps re-render of the text); the block itself never grows or jumps for it.
    internal static class Panel
    {
        const string RootName = "YazsPlan";
        const float WidthCompact = 640f, WidthFull = 800f;     // the widest the block gets per detail level; it shrinks to its text
        const float PadSide = 22f, PadTop = 14f, PadBottom = 18f, Font = 31f, LineGap = 6f;
        const float HeadH = 40f, HeadRule = 3f, HeadGap = 12f, HeadDiamond = 13f, TitleSize = 23f;  // the title line, its fading gold rule, the gap under it
        const float LabelW = 136f, LabelSize = 25f;             // the label column: survivor names (HUNTRESS, ENGINEER), TAGS / SOS / GRAB
        const float LabelMaxW = 300f;                           // ... wider only for a longer name another mod lends (Names.cs), up to this
        // the hairline between two groups: 3 units so it is at least 1.45 px on the Deck (2 units was 0.97 px there and
        // vanished when it fell between two pixel rows)
        const float GroupGap = 12f, RuleH = 3f;
        // the backing: feathered over Feather units at the top and bottom; the block is TailFactor times as wide as its
        // padded text so the text sits on the solid part of the fade sprite (solid up to 62 %) and the rest dissolves
        const float Feather = 22f, TailFactor = 1.5f;
        // the block must stay clear of the HUD around it: at the right edge it ends above the minimap (top at ~0.75 of
        // the screen on the Deck, ~0.78 on 21:9); in the bottom-left corner it stays under the weapon / ability icons
        // (the column ends at ~0.59 of the screen). A long plan shrinks from its automatic scale down to MinScale.
        const float BandBottom = 0.74f, LeftBandTop = 0.62f, MinScale = 0.75f;
        const float FadeIn = 0.25f, FadeOut = 0.15f;            // seconds
        const float WakeUp = 0.3f, Doze = 1.2f;                 // seconds to reach full strength / to settle at PanelIdle
        // HUD labels worth cloning first (font, material, outline of the quest box / timer), by object name
        static readonly string[] PreferredLabels = { "Quest_Obj1", "QuestName", "QuestStatus_Text", "GameTimer_Txt" };

        sealed class Group
        {
            public string Name;
            public RectTransform Block;
            public TextMeshProUGUI Text;
            public string Shown;          // the rich text the label holds: a render that would not change it is skipped
            public bool Hot;              // holds a row whose advice changed: the only groups the gold ramp re-renders
            public readonly List<PlanLine> Rows = new List<PlanLine>();
        }

        /// <summary>A plan worked out ahead of time (while the game was still paused on the screen that closed), good for as
        /// long as the run's fingerprint stays what it was then.</summary>
        sealed class Ahead { public long Quick; public string Key, Clock; public Plan Plan; public int Names; }

        static RectTransform _root, _content, _head, _rule, _tip;
        static bool _intro;                           // just became visible: play the entrance on the next frame (the groups exist by then)
        static CanvasGroup _fade;
        static bool _flip, _compact = true;           // at the right edge (backing dissolves to the left); one row per survivor
        static float _margin = 1f;                    // 1 live; the preview's screen emulation scales the margins
        static float _level = 1f, _awakeUntil;        // idle dimming: current strength, and until when the readout is at full strength
        static TextMeshProUGUI _template;
        static TextMeshProUGUI _kept;                 // the template of the readout the mod menu took down, for the one that replaces it
        static readonly List<Group> _groups = new List<Group>();
        static readonly List<RectTransform> _rules = new List<RectTransform>();
        static UIGameplay _hud;                       // remembered from UIGameplay.Update ticks for the fallback ticks
        static float _nextTick, _nextRebuild, _lastFrame;
        static float _canvasH, _autoScale = 1f;       // canvas height in units and the screen-size scale, from Ensure
        static float _alpha, _target;                 // the fade: current and wanted opacity
        static Plan _plan;                                        // the lines on screen, keyed, for the change highlight
        static Dictionary<string, string> _prev;                  // key -> text of the previous plan (null = first build of a run)
        static readonly HashSet<string> _changed = new HashSet<string>();
        static float _hlStart = -100f, _nextFade;
        static float _labelW = LabelW;                // the label column of the plan on screen
        static int _namesSeen;                        // Names.Version the plan on screen was built under
        static string _hlHex;                         // the highlight colour of the last render: the ramp re-renders only when it moves
        static bool _glyphsChecked;
        static string _sig = "", _stateKey = "";
        static int _gateCode = -1;                    // the gating flags packed into a number: the log line is only written when it changes
        static long _quick;                           // G.QuickKey() of the state the plan on screen was built from (0 = none)
        static Ahead _ahead;
        static int _lastHudFrame = -100;              // the frame of the HUD's last own tick; FallbackTick steps in when it goes quiet
        static float _nextFallback;
        static UIGameplayUpgradeSelection _screen;
        static bool _visible, _warnedNoCanvas, _warnedNoLabel, _sourceLogged, _preview;
        static bool _quiet;                           // the mod menu's preview rebuilds on every setting change: no log lines, no screenshots for those

        public static void ScreenOpened(UIGameplayUpgradeSelection sel) { _screen = sel; _ahead = null; SetVisible(false); }
        /// <summary>The base Hide(clicked) ran (validated: once per screen, every type); the game may keep the screen
        /// object active while it animates out, so do not wait for activeInHierarchy to drop.</summary>
        public static void ScreenClosed() { _screen = null; _nextRebuild = 0f; PlanAhead(); }   // shown (and highlighted) as soon as the sidebar is back

        /// <summary>The HUD is being destroyed (scene change): our objects die with it, forget them.</summary>
        public static void Reset()
        {
            if (_preview) _kept = null; else Discard();     // the widget in the mod menu's preview is not the HUD's: leave it to the menu
            _screen = null; _hud = null; _gateCode = -1; G.ForgetRun();
        }

        /// <summary>The mod menu closed over the paused run: the display settings or the builds may have changed, so the
        /// readout is taken down and the plan worked out again - now, while the game is still paused, not on the first
        /// frame back in play. The HUD is the same object, so the label the readout was cloned from is kept: looking for
        /// one again made 27 - 28 ms frames in a logged run (and could come back with another label than before).</summary>
        public static void Rebuild()
        {
            var hud = _hud; var template = _preview ? _kept : _template ?? _kept;   // Suspend put the HUD's label in _kept when the menu opened
            if (_preview) PreviewEnd();                                             // the menu's preview had the widget
            Discard(); _gateCode = -1; G.ForgetRun();
            _hud = hud; _kept = template;
            if (hud != null) PlanAhead();       // no HUD known: the menu was used on the main menu, there is no run to plan for
        }

        // up to 0.10.1 the old readout was only forgotten: every use of the mod menu left an inactive YazsPlan under the
        // HUD until the run ended. When the HUD itself is going the object is already dying - destroying it twice is harmless.
        static void Discard()
        {
            try { if (_root != null && _root.gameObject != null) UnityEngine.Object.Destroy(_root.gameObject); } catch { }
            Forget(); _kept = null;
        }

        static void Forget()
        {
            _root = null; _content = null; _head = null; _rule = null; _tip = null; _intro = false; Fx.Cancel("plan"); _fade = null; _template = null; _groups.Clear(); _rules.Clear();
            _sig = ""; _stateKey = ""; _visible = false; _alpha = 0f; _target = 0f; _level = 1f; _awakeUntil = 0f;
            _plan = null; _prev = null; _changed.Clear(); _hlStart = -100f; _hlHex = null; _quick = 0; _ahead = null; _labelW = LabelW;
        }

        // The pick has been applied and the game is still paused while the screen animates out: the moment to take the
        // snapshot and build the new plan (it re-scores every item that can still drop and every recruit - the mod's
        // heaviest piece of work). Up to 0.10.0 that happened on the first tick back in play, a hitch right after every
        // level-up. The tick only has to show it; should the run have moved on by then, it rebuilds as before.
        static void PlanAhead()
        {
            _ahead = null;
            if (_preview) return;
            long perf = Perf.Begin();
            try
            {
                if (!Plugin.ShowPanel.Value) return;
                using (G.Cache())
                {
                    long quick = G.QuickKey();
                    if (quick == 0) return;
                    var snap = G.Read();
                    if (snap.Squad.Count == 0) return;
                    string key = StateKey(snap);
                    if (key == _stateKey) { _quick = quick; return; }       // a skip, a reroll, a banish: the plan on screen still stands
                    _ahead = new Ahead { Quick = quick, Key = key, Clock = snap.Clock, Plan = Plan.Build(snap, CompactDetail()), Names = Names.Version };
                }
            }
            catch (Exception e) { _ahead = null; Plugin.Logger.LogWarning("[panel] plan ahead: " + e.Message); }
            finally { Perf.End("plan.ahead", perf); }
        }

        static bool CompactDetail() { try { return Plugin.PanelDetail.Value == PanelDetailLevel.Compact; } catch { return true; } }

        /// <summary>From GameMaster.Update (alive in every scene): ticks the readout whenever a run is on and the HUD's own
        /// Update did not reach us in the last two frames (the HUD switched off under a screen, or its hook failing) - what
        /// the second per-frame hook, on GameplayMaster.Update, was for. No gap: the fade must not stall when a screen opens.</summary>
        public static void FallbackTick()
        {
            if (_preview) return;
            if (Time.frameCount - _lastHudFrame <= 2) return;
            float now = Time.realtimeSinceStartup;
            if (now < _nextFallback) return;
            bool run = false; try { run = GameplayMaster.s_instance != null; } catch { }
            if (!run) { _nextFallback = now + 0.5f; return; }      // a menu scene: look again in half a second
            Tick(null);
        }

        /// <summary>Once per frame from UIGameplay.Update (hud set), else from FallbackTick (hud null); throttled to 0.4 s here.</summary>
        public static void Tick(UIGameplay hud)
        {
            try
            {
                float now = Time.realtimeSinceStartup;
                if (hud != null) { if ((object)_hud == null) Names.Forget(); _hud = hud; _lastHudFrame = Time.frameCount; } else hud = _hud;     // a new run's HUD: display names are asked afresh
                if (!_sourceLogged) { _sourceLogged = true; Plugin.Logger.LogInfo("[panel] first tick from " + (hud != null ? "UIGameplay.Update" : "GameMaster.Update (no HUD tick)")); }
                Animate(now);
                Shots.Tick();
                if (_visible) Shots.Baseline();
                if (_preview) return;                     // the menu preview owns the widget
                if (now < _nextTick) return;
                _nextTick = now + 0.4f;
                if (!Plugin.ShowPanel.Value) { SetVisible(false); return; }

                // the gating flags as numbers (Yes / No / Unknown / Error ...): their log line is only put together when one of them changed
                GameplayMaster master = null; try { master = GameplayMaster.s_instance; } catch { }
                int players = 0; try { if (master != null && master.gamePlayers != null) players = master.gamePlayers.Count; } catch { }
                int active = Unknown; try { var mode = master == null ? null : master.currentGameMode; active = master == null ? NoMaster : mode == null ? NoMode : mode.IsGameplayActive ? Yes : No; } catch { active = Error; }
                int paused = Unknown; try { paused = GameplayMaster.IsPaused ? Yes : No; } catch { paused = Error; }
                int hudVisible = Unknown; try { if (master != null) hudVisible = master.IsGameplayUIVisible() ? Yes : No; } catch { hudVisible = Error; }
                int selecting = Unknown; try { if (hud != null) selecting = hud.IsDisplayingUpgradeSelection() ? Yes : No; } catch { selecting = Error; }
                bool screenUp = false; try { screenUp = _screen != null && _screen.gameObject.activeInHierarchy; } catch { _screen = null; }
                int pauseMenu = Unknown; try { pauseMenu = GameplayMaster.IsPauseMenuFlowActive ? Yes : No; } catch { pauseMenu = Error; }
                int defeat = Unknown; try { defeat = GameplayMaster.IsDefeatResultsFlowActive ? Yes : No; } catch { defeat = Error; }

                int code = (Math.Min(players, 15) << 20) | (active << 17) | (paused << 14) | (pauseMenu << 11) | (defeat << 8) | (hudVisible << 5) | (selecting << 2) | (screenUp ? 2 : 0) | (hud != null ? 1 : 0);
                if (code != _gateCode)
                {
                    _gateCode = code;
                    Plugin.Logger.LogInfo("[panel] players=" + players + " active=" + Flag(active) + " paused=" + Flag(paused) + " pauseMenu=" + Flag(pauseMenu) + " defeat=" + Flag(defeat)
                        + " hudVisible=" + Flag(hudVisible) + " selecting=" + Flag(selecting) + " screen=" + screenUp + " hud=" + (hud != null));
                }

                // see the header comment: every one of these reads false during play and true on some screen
                bool viewUp = players == 0 || screenUp || selecting == Yes || paused == Yes || pauseMenu == Yes || defeat == Yes || hudVisible == Yes;
                if (viewUp) { SetVisible(false); return; }
                if (!Ensure(hud)) return;
                SetVisible(true);   // before the rebuild: an inactive TMP object skips its mesh update, and Layout measures the text

                if (now >= _nextRebuild)
                {
                    _nextRebuild = now + 2f;
                    Refresh(now);
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[panel] " + e); _nextTick = Time.realtimeSinceStartup + 5f; }
        }

        const int No = 0, Yes = 1, Unknown = 2, Error = 3, NoMaster = 4, NoMode = 5;
        static string Flag(int v) { return v == Yes ? "True" : v == No ? "False" : v == Error ? "err" : v == NoMaster ? "nomaster" : v == NoMode ? "nomode" : "?"; }

        // Every two seconds while the readout is up (at once after a screen closed): has anything the plan reads moved?
        // The fingerprint answers that with a few dozen calls into the game; only when it moved is the snapshot taken and
        // the plan rebuilt - or the plan that was worked out while the game was still paused is put up.
        static void Refresh(float now)
        {
            long perf = Perf.Begin();
            try
            {
                long quick = G.QuickKey();
                var ahead = _ahead; _ahead = null;
                // another mod lends other names now (Extensions.InvalidateDisplayNames, or it came or went): the rows are
                // drawn again with them even though nothing in the run moved
                int names = Names.Version;
                if (names != _namesSeen) { _namesSeen = names; _quick = 0; _stateKey = ""; if (ahead != null && ahead.Names != names) ahead = null; }
                Plan plan; string key, clock;
                if (ahead != null && quick != 0 && ahead.Quick == quick) { plan = ahead.Plan; key = ahead.Key; clock = ahead.Clock; }
                else
                {
                    if (quick != 0 && quick == _quick && _stateKey.Length > 0) return;
                    using (G.Cache())
                    {
                        long read = Perf.Begin();
                        var snap = G.Read();
                        key = StateKey(snap); clock = snap.Clock;
                        Perf.End("read", read);
                        if (key == _stateKey) { _quick = quick; return; }
                        long build = Perf.Begin();
                        plan = Plan.Build(snap, CompactDetail());
                        Perf.End("plan.build", build);
                    }
                }
                _quick = quick;
                if (key == _stateKey) return;
                _stateKey = key;
                if (plan.Signature != _sig)
                {
                    _sig = plan.Signature;
                    Apply(plan, now);
                    Plugin.Logger.LogInfo("[plan] " + clock + ": " + plan.PlainText() + (_changed.Count > 0 ? "  [changed: " + string.Join(", ", _changed) + "]" : ""));
                    if (_hlStart > 0) { Shots.Later(0.3f, "hl1"); Shots.Later(1.5f, "hl2"); Shots.Later(3.5f, "hl3"); }
                    else Shots.Later(0.5f, "plan");
                }
            }
            finally { Perf.End("refresh", perf); }
        }

        /// <summary>The per-frame part: the fade and the highlight's colour ramp. Called from Tick and from the preview.</summary>
        public static void Animate() { Animate(Time.realtimeSinceStartup); }
        static void Animate(float now)
        {
            float dt = _lastFrame > 0 ? Mathf.Clamp(now - _lastFrame, 0f, 0.1f) : 0f;
            _lastFrame = now;
            Fade(dt, now);
            if (_intro && _root != null) { _intro = false; Intro(); }
            if (_hlStart > 0 && _target > 0 && now >= _nextFade) { _nextFade = now + 0.033f; Render(now, false); }   // the gold ramp, ~30 fps
        }

        // everything the plan depends on: squad members with weapons, abilities, levels, items, tree levels, and the active quest
        static string StateKey(Snapshot s)
        {
            string quest = "";
            try { var qm = GameQuestManager.Get; var q = qm == null ? null : qm.ActiveQuest; if (q != null) quest = q.Pointer.ToString(); } catch { }
            return s.Squad.Count + "|" + s.SquadText() + "|" + quest + "|" + s.Tags.Key();
        }

        // ---- motion: the entrance, and the cue that the advice changed ----
        static void Intro()
        {
            try
            {
                Fx.Draw("planrule", _rule, 0.05f, 0.4f);
                Fx.Spin("plantip", _tip, 0f, 0.45f, 0.5f);
                float from = _flip ? 30f : -30f; int i = 0;
                foreach (var g in _groups)
                {
                    if (g.Rows.Count == 0 || g.Block == null) continue;
                    var block = g.Block;
                    Fx.Run("plangroup" + i, 0.06f + 0.07f * i, 0.32f, k => { block.anchoredPosition = new Vector2(from * (1f - Fx.OutCubic(k)), block.anchoredPosition.y); });
                    i++;
                }
            }
            catch { }
        }

        static void Pulse()
        {
            try
            {
                if (_tip == null) return;
                Fx.Spin("plantip", _tip, 0f, 0.5f, 0.5f);
                Fx.Ping("planping", _tip, HeadDiamond * 1.4f, 3f, Theme.Gold, false, 0.1f, 0.6f, 3.2f, false);
            }
            catch { }
        }

        // ---- visibility: a short fade each way; the object is deactivated only once fully transparent ----
        static void SetVisible(bool v)
        {
            if (_root == null || _visible == v) return;
            try
            {
                _visible = v; _target = v ? 1f : 0f;
                if (v)
                {
                    if (!_root.gameObject.activeSelf) { _alpha = 0f; if (_fade != null) _fade.alpha = 0f; }
                    if (_alpha <= 0.001f) _intro = true;       // from nothing (also the first show: a new widget is born active): play the entrance
                    _root.gameObject.SetActive(true);
                    Wake(4f);
                }
                if (_quiet) return;
                Plugin.Logger.LogInfo("[panel] " + (v ? "shown" : "hidden"));
                Shots.Later(v ? 0.5f : 0.3f, v ? "shown" : "hidden");
            }
            catch { Forget(); }
        }

        /// <summary>Full strength for the next <paramref name="seconds"/>; afterwards the readout settles at PanelIdle.</summary>
        static void Wake(float seconds) { _awakeUntil = Mathf.Max(_awakeUntil, Time.realtimeSinceStartup + seconds); }

        static void Fade(float dt, float now)
        {
            if (_root == null || _fade == null) return;
            float idle = 0.7f; try { idle = Mathf.Clamp(Plugin.PanelIdle.Value, 0.25f, 1f); } catch { }
            float level = now < _awakeUntil ? 1f : idle;
            if (Mathf.Abs(_alpha - _target) < 0.0001f && Mathf.Abs(_level - level) < 0.0001f) return;
            _alpha = Mathf.MoveTowards(_alpha, _target, dt / (_target > _alpha ? FadeIn : FadeOut));
            _level = Mathf.MoveTowards(_level, level, dt / (level > _level ? WakeUp : Doze));
            try
            {
                _fade.alpha = _alpha * _alpha * (3f - 2f * _alpha) * _level;    // smoothstep
                if (_alpha <= 0f && _target <= 0f) _root.gameObject.SetActive(false);
            }
            catch { Forget(); }
        }

        static bool Alive()
        {
            try { return _root != null && _content != null && _root.gameObject != null; } catch { return false; }
        }

        static RectTransform FindCanvas(UIGameplay hud)
        {
            try { if (hud != null) return hud.transform.TryCast<RectTransform>(); } catch { }
            try { var go = GameObject.Find("UIGameplay"); if (go != null) return go.transform.TryCast<RectTransform>(); } catch { }
            try
            {
                var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<UIGameplay>());
                for (int i = 0; i < all.Length; i++)
                {
                    var u = all[i].TryCast<UIGameplay>();
                    if (u == null) continue;
                    bool live = false; try { live = u.gameObject.activeInHierarchy; } catch { }
                    if (live) { _hud = u; return u.transform.TryCast<RectTransform>(); }
                }
            }
            catch { }
            return null;
        }

        static bool Ensure(UIGameplay hud)
        {
            if (Alive()) return true;
            long perf = Perf.Begin();
            try
            {
                var kept = _kept; var ahead = _ahead;       // what Rebuild left for this moment: the old template, the plan made while paused
                Discard(); _ahead = ahead;
                var canvas = FindCanvas(hud);
                if (canvas == null)
                {
                    if (!_warnedNoCanvas) { _warnedNoCanvas = true; Plugin.Logger.LogWarning("[panel] no HUD canvas (UIGameplay) to attach to"); }
                    return false;
                }
                var template = StillUnder(kept, canvas) ? kept : Ui.FindLabel(canvas, PreferredLabels);
                if (template == null)
                {
                    if (!_warnedNoLabel) { _warnedNoLabel = true; Plugin.Logger.LogWarning("[panel] no HUD text to clone yet"); }
                    return false;
                }
                _warnedNoLabel = false;

                float scale = ScaleFor(canvas);
                _autoScale = scale; try { _canvasH = canvas.rect.height; } catch { _canvasH = 0; }
                if (!Build(canvas, template, scale)) return false;
                string geo = "";
                try { geo = " canvas " + canvas.rect.width.ToString("0") + "x" + canvas.rect.height.ToString("0") + " screen " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height + " scale " + canvas.lossyScale.x.ToString("0.000"); } catch { }
                Plugin.Logger.LogInfo("[panel] created under " + canvas.name + " using label '" + template.name + "'" + (template == kept ? " (kept)" : "") + " at (" + _root.anchoredPosition.x + ", " + _root.anchoredPosition.y + ") x" + scale.ToString("0.00") + geo);
                Shots.Later(0.5f, "created");
                return true;
            }
            finally { Perf.End("panel.create", perf); }
        }

        // the label the readout was cloned from before the mod menu took it down: good while it lives under this canvas
        static bool StillUnder(TextMeshProUGUI label, RectTransform canvas)
        {
            try { return label != null && label.gameObject != null && label.font != null && label.transform.IsChildOf(canvas); } catch { return false; }
        }

        static float MaxWidth { get { return _compact ? WidthCompact : WidthFull; } }

        // ---- the widget: the soft backing, the title line, and an empty content rect the groups are laid out in ----
        static bool Build(RectTransform canvas, TextMeshProUGUI template, float scale)
        {
            try { _flip = Plugin.PanelPosition.Value == PanelPlace.Right; } catch { _flip = false; }
            var root = Ui.NewRect(RootName, canvas);
            if (_flip)
            {   // the right edge, under the item icons: grows down and to the left
                root.anchorMin = root.anchorMax = new Vector2(1f, 1f); root.pivot = new Vector2(1f, 1f);
                root.anchoredPosition = new Vector2(-Plugin.PanelRight.Value, -Plugin.PanelTop.Value);
            }
            else
            {   // the bottom-left corner: grows up and to the right
                root.anchorMin = root.anchorMax = Vector2.zero; root.pivot = Vector2.zero;
                root.anchoredPosition = new Vector2(Plugin.PanelLeft.Value, Plugin.PanelBottom.Value);
            }
            root.sizeDelta = new Vector2(MaxWidth * TailFactor, 300f);
            root.localScale = new Vector3(scale, scale, 1f);
            CanvasGroup fade = null;
            try { fade = root.gameObject.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>(); fade.alpha = 0f; fade.blocksRaycasts = false; fade.interactable = false; }
            catch (Exception e) { Plugin.Logger.LogInfo("[panel] no CanvasGroup (" + e.Message + "), no fade"); }

            // backing: no box - a dark wash under the text that dissolves towards the middle of the screen
            float opacity = 0.42f; try { opacity = Mathf.Clamp01(Plugin.PanelOpacity.Value); } catch { }
            if (opacity > 0.01f) Ui.Scrim(root, "Scrim", new Color(Theme.Scrim.r, Theme.Scrim.g, Theme.Scrim.b, opacity), Feather, _flip);

            // title line: a diamond, PLAN in small gold capitals, a gold rule underneath that fades out - the HUD's quest tracker
            var head = Ui.NewRect("Header", root);
            head.anchorMin = head.anchorMax = new Vector2(0, 1); head.pivot = new Vector2(0, 1f);
            head.anchoredPosition = new Vector2(PadSide, -PadTop); head.sizeDelta = new Vector2(MaxWidth - 2 * PadSide, HeadH);
            var rule = Ui.FadeRule(head, "Rule", Theme.GoldLine); Ui.LeftPivot(rule, 0f, HeadRule);
            var tip = Ui.Diamond(head, "Tip", 0, 0.5f, HeadDiamond, Theme.Gold); tip.anchoredPosition = new Vector2(HeadDiamond / 2, 2f);
            var title = Ui.CloneText(template, head, "Title");
            if (title == null) { UnityEngine.Object.Destroy(root.gameObject); return false; }
            _rule = rule; _tip = tip;
            Ui.Stretch(title.rectTransform, HeadDiamond + 12f, HeadRule + 1f, 0, 0);
            title.text = "PLAN"; title.color = Theme.GoldText; title.fontSize = TitleSize; title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Left; title.characterSpacing = 8f;

            // the groups go into a content rect under the title; Layout positions them by hand after every new plan
            var content = Ui.NewRect("Content", root);
            content.anchorMin = content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 1f);
            content.anchoredPosition = new Vector2(PadSide, -(PadTop + HeadH + HeadGap)); content.sizeDelta = new Vector2(MaxWidth - 2 * PadSide, 100f);

            _root = root; _content = content; _head = head; _fade = fade; _template = template;
            _groups.Clear(); _rules.Clear();
            _alpha = 0f; _target = 0f; _visible = false; _level = 1f;
            CheckGlyphs(template);
            return true;
        }

        /// <summary>Settle the plan's glyphs against the font of <paramref name="text"/> (once): the preview calls it before
        /// building its sample plans, the live path from Build.</summary>
        public static void CheckGlyphs(TextMeshProUGUI text)
        {
            if (_glyphsChecked) return;
            _glyphsChecked = true;
            // the plan's "›" and "·" only if the HUD font (or its fallbacks) has them; ASCII otherwise
            try
            {
                var font = text.font;
                if (font != null)
                {
                    if (!font.HasCharacter('›', true, true)) Plan.Arrow = " > ";
                    if (!font.HasCharacter('·', true, true)) Plan.Sep = "  |  ";
                }
                Plugin.Logger.LogInfo("[panel] glyphs: arrow '" + Plan.Arrow.Trim() + "' sep '" + Plan.Sep.Trim() + "'");
            }
            catch (Exception e) { Plan.Arrow = " > "; Plan.Sep = "  |  "; Plugin.Logger.LogInfo("[panel] glyph check failed (" + e.Message + "), using ascii"); }
        }

        static Group GroupAt(int i, string name)
        {
            while (_groups.Count <= i)
            {
                var g = new Group();
                g.Block = Ui.NewRect("Group" + _groups.Count, _content);
                g.Block.anchorMin = new Vector2(0, 1); g.Block.anchorMax = new Vector2(1, 1); g.Block.pivot = new Vector2(0.5f, 1f);
                g.Block.anchoredPosition = Vector2.zero; g.Block.sizeDelta = new Vector2(0, 40f);
                var t = Ui.CloneText(_template, g.Block, "Text");
                if (t != null)
                {
                    Ui.Stretch(t.rectTransform, 0, 0, 0, 0);
                    t.fontSize = Font; t.fontStyle = FontStyles.Normal; t.alignment = TextAlignmentOptions.TopLeft; t.color = Theme.White;
                    try { t.enableWordWrapping = true; } catch { }
                    try { t.overflowMode = TextOverflowModes.Overflow; } catch { }
                    try { t.lineSpacing = LineGap; } catch { }
                }
                g.Text = t;
                _groups.Add(g);
            }
            var gr = _groups[i];
            gr.Name = name; gr.Rows.Clear();
            try { gr.Block.gameObject.SetActive(true); } catch { }
            return gr;
        }

        static RectTransform RuleAt(int i)
        {
            while (_rules.Count <= i)
            {
                var r = Ui.FadeRule(_content, "Rule" + _rules.Count, Theme.GoldRule);
                r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 1f);
                r.sizeDelta = new Vector2(0, RuleH);
                _rules.Add(r);
            }
            var rule = _rules[i];
            try { rule.gameObject.SetActive(true); } catch { }
            return rule;
        }

        // ---- a new plan: diff against the previous one for the highlight, distribute the rows into groups, draw ----
        static void Apply(Plan plan, float now)
        {
            _changed.Clear();
            if (_prev != null) foreach (var l in plan.Lines) { string old; if (!_prev.TryGetValue(l.Key, out old) || old != l.Text) _changed.Add(l.Key); }
            _prev = new Dictionary<string, string>();
            foreach (var l in plan.Lines) _prev[l.Key] = l.Text;
            _plan = plan; _compact = plan.Compact;      // the block's widest width follows the detail level of the plan it shows

            int gi = 0; Group g = null;
            foreach (var l in plan.Lines)
            {
                if (g == null || g.Name != l.Group) g = GroupAt(gi++, l.Group);
                g.Rows.Add(l);
            }
            for (int i = gi; i < _groups.Count; i++) { _groups[i].Rows.Clear(); try { _groups[i].Block.gameObject.SetActive(false); } catch { } }
            foreach (var gr in _groups) { gr.Hot = false; foreach (var l in gr.Rows) if (_changed.Contains(l.Key)) { gr.Hot = true; break; } }
            _labelW = LabelWidth();

            float hlSeconds = 3f; try { hlSeconds = Plugin.PanelHighlight.Value; } catch { }
            _hlStart = _changed.Count > 0 && hlSeconds > 0 ? now : -100f;
            Wake(_changed.Count > 0 ? Mathf.Max(6f, hlSeconds + 3f) : 5f);   // new advice is shown at full strength, then settles
            if (_changed.Count > 0 && !_intro) Pulse();
            Render(now, true);
            Layout();
        }

        // The label column is 136 units: HUNTRESS and ENGINEER fit. A survivor name another mod lends may be longer - then
        // the column widens to the widest label (measured in the readout's own font), up to LabelMaxW. Without lent names
        // nothing is measured and the column is what it always was.
        static float LabelWidth()
        {
            if (!Names.Active || _plan == null) return LabelW;
            TextMeshProUGUI probe = null;
            foreach (var g in _groups) if (g.Rows.Count > 0 && g.Text != null) { probe = g.Text; break; }
            if (probe == null) return LabelW;
            float widest = 0f;
            try
            {
                foreach (var l in _plan.Lines)
                {
                    if (l.Label.Length == 0) continue;
                    float w = probe.GetPreferredValues("<size=" + LabelSize + "><b>" + l.Label + "</b></size>").x;
                    if (w > widest) widest = w;
                }
            }
            catch { return LabelW; }
            finally { foreach (var g in _groups) g.Shown = null; }      // measuring went through a label's text buffers: every label is written again below
            return Mathf.Clamp(Mathf.Ceil(widest) + 16f, LabelW, LabelMaxW);
        }

        // the text of every group: "LABEL<indent>value</indent>" rows, the changed values in the highlight colour
        // (gold held for 0.8 s, then easing to white). A new plan renders every group; the ramp (30 times a second for
        // the length of the highlight) only the groups holding a changed row, only when the colour has moved on since the
        // last render (it stands still for the first 0.8 s), and a label is only written to when its text differs:
        // every write is a string copied into the game and a full re-parse and re-mesh of that label.
        static void Render(float now, bool all)
        {
            if (_plan == null) return;
            float dur = 3f; try { dur = Plugin.PanelHighlight.Value; } catch { }
            float t = now - _hlStart;
            bool hl = _hlStart > 0 && t < dur && _changed.Count > 0;
            string hex = null;
            if (hl)
            {
                float k = dur <= 0.8f ? t / dur : Mathf.Clamp01((t - 0.8f) / (dur - 0.8f));
                k = k * k * (3f - 2f * k);
                hex = Theme.Hex(Color.Lerp(Theme.HlGold, Theme.White, k));
            }
            if (!all && hl && hex == _hlHex) return;
            _hlHex = hex;
            var sb = new StringBuilder();
            foreach (var g in _groups)
            {
                if (g.Rows.Count == 0 || g.Text == null) continue;
                if (!all && !g.Hot) continue;
                sb.Length = 0;
                foreach (var l in g.Rows)
                {
                    if (sb.Length > 0) sb.Append('\n');
                    if (l.Label.Length > 0)
                    {
                        sb.Append("<size=").Append(LabelSize).Append("><b>");
                        if (l.Head) sb.Append(l.Label);
                        else sb.Append("<color=").Append(Theme.GoldHex).Append('>').Append(l.Label).Append("</color>");
                        sb.Append("</b></size>");
                    }
                    sb.Append("<indent=").Append(_labelW).Append('>');
                    if (hl && _changed.Contains(l.Key)) sb.Append("<color=").Append(hex).Append('>').Append(l.Text).Append("</color>");
                    else sb.Append(l.Text);
                    sb.Append("</indent>");
                }
                string text = sb.ToString();
                if (text != g.Shown) { g.Text.text = text; g.Shown = text; }
            }
            if (!hl) _hlStart = -100f;
        }

        // stack the groups top-down with a hairline between them, shrink the block to its text, and keep it clear of the HUD
        static void Layout()
        {
            // measure at the widest the block may get (the text wraps there), then take the width the text really uses
            float maxText = MaxWidth - 2 * PadSide, w = 0f;
            _content.sizeDelta = new Vector2(maxText, _content.sizeDelta.y);
            foreach (var g in _groups)
            {
                if (g.Rows.Count == 0 || g.Text == null) continue;
                try { g.Text.ForceMeshUpdate(); w = Mathf.Max(w, g.Text.renderedWidth); } catch { w = maxText; }
            }
            w = w <= 0 ? maxText : Mathf.Min(maxText, Mathf.Ceil(w) + 6f);   // a little slack so the same lines still fit
            _content.sizeDelta = new Vector2(w, _content.sizeDelta.y);

            float y = 0f; int ri = 0; bool first = true;
            foreach (var g in _groups)
            {
                if (g.Rows.Count == 0 || g.Text == null) continue;
                if (!first)
                {
                    y += GroupGap;
                    RuleAt(ri++).anchoredPosition = new Vector2(0, -y);
                    y += RuleH + GroupGap;
                }
                first = false;
                float h = (Font + LineGap) * 1.25f * g.Rows.Count;
                try { g.Text.ForceMeshUpdate(); float ph = g.Text.preferredHeight; if (ph > 0) h = ph; } catch { }
                g.Block.anchoredPosition = new Vector2(0, -y); g.Block.sizeDelta = new Vector2(0, h);
                y += h;
            }
            for (int i = ri; i < _rules.Count; i++) { try { _rules[i].gameObject.SetActive(false); } catch { } }
            _content.sizeDelta = new Vector2(w, y);

            // the block is wider than its text: the backing is solid under the text and dissolves over the rest, towards
            // the middle of the screen (to the right in the bottom-left corner, to the left at the right edge)
            float blockW = (w + 2 * PadSide) * TailFactor;
            float x0 = _flip ? blockW - (w + 2 * PadSide) : 0f;
            _head.anchoredPosition = new Vector2(x0 + PadSide, -PadTop); _head.sizeDelta = new Vector2(w, HeadH);
            _content.anchoredPosition = new Vector2(x0 + PadSide, -(PadTop + HeadH + HeadGap));
            if (_preview)
            {
                var sb = new StringBuilder("[panel] layout:");
                foreach (var g in _groups) { if (g.Rows.Count == 0) continue; try { sb.Append(" ").Append(g.Name).Append("@").Append((-g.Block.anchoredPosition.y).ToString("0")).Append("+").Append(g.Block.sizeDelta.y.ToString("0")); } catch { } }
                for (int i = 0; i < _rules.Count; i++) { try { sb.Append(" rule").Append(i).Append("@").Append((-_rules[i].anchoredPosition.y).ToString("0")).Append(_rules[i].gameObject.activeSelf ? "" : "(off)").Append(" parent=").Append(_rules[i].parent == null ? "none" : _rules[i].parent.name).Append(" sib=").Append(_rules[i].GetSiblingIndex()); } catch (Exception e) { sb.Append(" rule").Append(i).Append("!").Append(e.Message); } }
                Plugin.Logger.LogInfo(sb.ToString());
            }
            float total = PadTop + HeadH + HeadGap + y + PadBottom;
            _root.sizeDelta = new Vector2(blockW, total);

            float scale = _autoScale;
            try
            {
                float band = _flip ? _canvasH * BandBottom - Plugin.PanelTop.Value * _margin : _canvasH * (1f - LeftBandTop) - Plugin.PanelBottom.Value * _margin;
                if (band > 0 && total * scale > band) scale = Mathf.Min(scale, Mathf.Max(MinScale, band / total));
            }
            catch { }
            if (Mathf.Abs(_root.localScale.x - scale) > 0.005f)
            {
                _root.localScale = new Vector3(scale, scale, 1f);
                Plugin.Logger.LogInfo("[panel] scale x" + scale.ToString("0.00") + " for " + total.ToString("0") + " units (auto x" + _autoScale.ToString("0.00") + ")");
            }
        }

        // The size follows the screen. One canvas unit is Screen.height / canvas height pixels (1/3 on the Deck, 2/3 on a
        // 1440p monitor), so at scale 1 the 31-unit font is 10 px on the Deck and 21 px at 1440p. Up to 0.10.2 the block
        // was only enlarged until its text reached MinTextPx, which left a desktop at scale 1: 21 px on a 3440x1440
        // screen, smaller than the HUD's own quest text and hard to read from a desk. Now the text is TextShare of the
        // screen's height on every screen and never under MinTextPx: 15 px on the Deck as before (x1.45), 20 px at
        // 1080p, 27 px at 1440p, 40 px at 4K (x1.31 each). PanelSize multiplies that (the mod menu's "Readout size");
        // PanelScale, when set, replaces the automatic part.
        const float MinTextPx = 15f, TextShare = 0.01875f;

        /// <summary>The automatic scale on a screen where one canvas unit is <paramref name="unitPx"/> pixels and the canvas
        /// is <paramref name="canvasH"/> units tall.</summary>
        public static float AutoScale(float unitPx, float canvasH)
        {
            if (unitPx <= 0 || canvasH <= 0) return 1f;
            float px = Mathf.Max(MinTextPx, TextShare * unitPx * canvasH);
            return Mathf.Clamp(px / (Font * unitPx), 1f, 2f);
        }

        /// <summary>The player's size setting, 1 = the automatic size.</summary>
        public static float UserSize { get { try { return Mathf.Clamp(Plugin.PanelSize.Value, 0.5f, 2.5f); } catch { return 1f; } } }

        /// <summary>The height in pixels of the readout's body text at <paramref name="scale"/> under <paramref name="canvas"/>.</summary>
        public static float TextPx(RectTransform canvas, float scale)
        {
            try { float h = canvas.rect.height; return h <= 0 ? 0f : Font * scale * UnityEngine.Screen.height / h; } catch { return 0f; }
        }

        /// <summary>The readout's scale under a canvas with the HUD's geometry (the HUD canvas itself, or an overlay canvas
        /// with the same reference and match mode): the automatic or the fixed part, times the player's size.</summary>
        public static float ScaleFor(RectTransform canvas)
        {
            float basis = 1f;
            float fixedScale = 0; try { fixedScale = Plugin.PanelScale.Value; } catch { }
            if (fixedScale > 0) basis = fixedScale;
            else
            {
                try { float h = canvas.rect.height; if (h > 0) basis = AutoScale(UnityEngine.Screen.height / h, h); } catch { }
            }
            return Mathf.Clamp(basis * UserSize, 0.5f, 4f);
        }

        // ---- the design preview on the main menu (Preview.cs): the same widget on an overlay canvas with sample plans ----
        public static bool PreviewBuild(RectTransform canvas, TextMeshProUGUI template, Plan plan, float scaleOverride, float posScale, float canvasH)
        {
            Forget(); _preview = true;
            float scale = scaleOverride > 0 ? scaleOverride : ScaleFor(canvas);
            _autoScale = scale; try { _canvasH = canvasH > 0 ? canvasH : canvas.rect.height; } catch { _canvasH = 0; }
            _margin = posScale;
            if (!Build(canvas, template, scale)) { _preview = false; return false; }
            _root.anchoredPosition = _root.anchoredPosition * posScale;     // an emulated screen: its margins in this canvas's units
            SetVisible(true);
            Apply(plan, Time.realtimeSinceStartup);
            if (!_quiet) Plugin.Logger.LogInfo("[panel] preview built x" + scale.ToString("0.00") + ", " + _root.sizeDelta.y.ToString("0") + " units tall");
            return true;
        }
        public static void PreviewApply(Plan plan) { if (_root != null) Apply(plan, Time.realtimeSinceStartup); }
        public static void PreviewHide() { SetVisible(false); }
        /// <summary>Let the readout settle at PanelIdle now (the preview photographs the resting state).</summary>
        public static void PreviewDoze() { _awakeUntil = 0f; }
        /// <summary>Forget the previous plan so the next one is drawn without a change highlight.</summary>
        public static void PreviewFresh() { _prev = null; }
        public static void PreviewEnd()
        {
            try { if (_root != null) UnityEngine.Object.Destroy(_root.gameObject); } catch { }
            Forget(); _preview = false; _quiet = false; _margin = 1f;
        }

        // ---- the mod menu's DISPLAY tab shows the readout itself, with the settings as they stand, in a window of the
        //      menu: the menu's canvas has the HUD canvas's geometry (same reference, same match mode), so at the same
        //      scale the text has exactly the pixels it will have in play. The widget is this class's one and only, so
        //      the live readout is taken down while the menu is open (Suspend) and comes back through Rebuild. ----

        /// <summary>The mod menu opened: take the live readout down (it is hidden under the pause menu anyway) and keep what
        /// Rebuild needs to bring it back cheaply - the HUD and the label it was cloned from.</summary>
        public static void Suspend()
        {
            if (_preview) return;
            var hud = _hud; var template = _template ?? _kept;
            Discard();
            _hud = hud; _kept = template;
        }

        /// <summary>Build (or build again, after a setting changed) the readout inside <paramref name="window"/> of the mod
        /// menu: scale, backing, detail and corner as configured right now. <paramref name="canvas"/> is the menu's root
        /// canvas rect, which the scale and the fit-to-band rule are worked out against as they are on the HUD.</summary>
        public static bool MenuPreview(RectTransform window, RectTransform canvas, TextMeshProUGUI template, Plan plan)
        {
            try
            {
                var hud = _hud; var kept = _kept;               // PreviewEnd forgets nothing of these, but keep them out of harm's way
                if (_preview) PreviewEnd();
                _hud = hud; _kept = kept; _quiet = true;
                float canvasH = 0f; try { canvasH = canvas.rect.height; } catch { }
                if (!PreviewBuild(window, template, plan, ScaleFor(canvas), 1f, canvasH)) return false;
                // the right-edge position hangs 780 units under the top of the screen: in the window, under its top edge
                if (_flip) _root.anchoredPosition = new Vector2(_root.anchoredPosition.x, -40f);
                return true;
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[panel] menu preview: " + e.Message); _preview = false; _quiet = false; return false; }
        }

        /// <summary>The scale the readout on screen ended up with (after the fit-to-band rule), 0 when there is none.</summary>
        public static float ShownScale { get { try { return _root != null ? _root.localScale.x : 0f; } catch { return 0f; } } }
        public static bool Previewing { get { return _preview; } }
    }
}
