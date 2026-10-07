// Menus on screens that are not 16:9 (0.14.0). The game lays its menus out in a centred 3840 x 2160 frame and paints the
// rest of a wider (21:9, 32:9) or taller (16:10 - the Steam Deck) screen black:
//   - in a run the frame is UIGameplay/Border: four black Images round a centred 3840 x 2160 hole, switched by
//     UIGameplay.Update EVERY frame to GameplayMaster.IsGameplayUIVisible() - on with every level-up, chest, rescue, military
//     training, Research Pod, pause, results, jukebox, guide, options ... (61 times in a 26-minute run), so the bars come
//     and go;
//   - on the main menu and in the camp it is UIMainMenu/Border_01: the same four pieces, always on (the camp's 3D rooms are
//     drawn full width behind it).
// [General] WideMenus (Off / DuringRuns / Everywhere; Everywhere by default) hides the four PIECES once - never the parent the
// game switches, and never a null field: the game dereferences borderGameObject every frame - and carries each menu's own
// dark backdrop to the screen edges: the grid layer and the black dims grow by sizeDelta (smooth luminance; mirrored
// copies leave a darker band where the old edge was - [Debug] WideMenusWings builds them for a capture comparison), the
// sliced vignettes by SpriteRenderer.size. Cards, buttons and labels stay where the game puts them. Every element is found
// by name and checked (size and centre in canvas units, the vignette's slicing) before anything is touched; a menu whose
// elements do not match keeps the game's frame (the pieces show again while it is up), and so do the end credits and the
// demo end (art backdrops). The main menu keeps its frame on canvases wider than its art (half-width 2863 units, ~2.65:1).
// Undone on Off, on 16:9, on a resize (then applied again); nothing changes on a 16:9 screen.
// Ticked from the HUD's own Update post-fix (after the game set its frame, same frame: no flicker), the canvas looked at
// every 15 frames; the main menu from GameMaster.Update every 0.25 s. The work is done once per HUD / main menu and canvas
// size ([perf] wide.apply / wide.menu); in between a frame costs one activeSelf read (the frame-keeping menus).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx.Configuration;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace YazsCompanion
{
    public enum WideMode { Off, DuringRuns, Everywhere }

    internal static class WideMenus
    {
        internal static ConfigEntry<WideMode> Mode;
        internal static ConfigEntry<bool> Wings;

        const float RefW = 3840f, RefH = 2160f;     // the CanvasScaler's reference (Expand): the canvas is never smaller
        const float Pad = 4f;                       // canvas units past the screen edge (rounding, the grid's animated scale)
        const float Drift = 60f;                    // how far an element's centre may sit from where the scene has it
        const float ArtHalf = 2863f;                // the main menu art reaches x -2863 .. 3137
        const int RunEvery = 15;                    // frames between two looks at the HUD canvas
        const float MenuEvery = 0.25f;              // seconds between two looks at the main menu

        public static void Bind(ConfigFile cfg)
        {
            Mode = cfg.Bind("General", "WideMenus", WideMode.Everywhere, "Menus on screens wider or taller than 16:9 (ultrawide monitors; 16:10 like the Steam Deck). The game frames its menus in black there: during a run the bars come and go with every level-up, chest, rescue, pause and the results; on the main menu and in the camp they stay. Everywhere = every menu fills the screen: the frame is hidden and each menu's own dark backdrop reaches the screen edges (cards, buttons and text stay where the game puts them; the end credits keep the frame, and so does the main menu on screens wider than its art, about 2.65:1). DuringRuns = the run's menus only. Off = the game's frame. Nothing changes on a 16:9 screen.");
            Wings = cfg.Bind("Debug", "WideMenusWings", false, "WideMenus: carry the grid backdrops to the screen edges with mirrored copies instead of stretching them (square grid cells, but a faint darker band where the old edge was). For comparing captures; off by default.");
            Mode.SettingChanged += (s, e) => _gen++;
            Wings.SettingChanged += (s, e) => _gen++;
        }

        static WideMode Current() { return Mode == null ? WideMode.Off : Mode.Value; }

        // ---- what is touched: a menu (found from the HUD or by its name under the main menu canvas) and its backdrop layers,
        //      with the size (canvas units, the element's own scale left out: the game animates the selection screens' grid -
        //      1.3 -> 1 as it shows, 1 -> 1.4 as it hides, and it stays at 1.4 while hidden) and centre the game's scene data
        //      has for them (research review_1005 ultrawide: extents_level2_full.txt, the level1 dumps of the C5 lane)
        enum Kind { Grow, Grid, Sprite }

        sealed class Op
        {
            public string Path, Label;
            public Kind Kind;
            public float W = RefW, H = RefH, Cy;
            public bool X = true, Y = true, TopOnly;
        }

        sealed class View
        {
            public string Label, Child;
            public Func<UIGameplay, Component> Get;
            public Op[] Ops;
            public bool KeepFrame, Required;
        }

        static Op GridOp(string path, string label) { return new Op { Path = path, Label = label, Kind = Kind.Grid }; }
        static Op VignetteOp(string path) { return new Op { Path = path, Label = "vignette", Kind = Kind.Sprite }; }
        static Op DimOp(string path, string label) { return new Op { Path = path, Label = label, Kind = Kind.Grow }; }

        static View[] _runViews, _menuViews;        // made on first use: delegates over game types, never built while the plugin loads

        static View[] RunViews()
        {
            if (_runViews != null) return _runViews;
            Func<Op[]> pick = () => new[] { GridOp("Grid", "grid"), VignetteOp("VINETTE"), DimOp("Background", "dim") };
            Func<Op[]> net = () => new[] { GridOp("Bgr_Net", "net"), DimOp("Bgr_Net/Bgr_Net", "dim") };
            return _runViews = new[]
            {
                new View { Label = "level-up", Get = LevelUpOf, Ops = pick() },
                new View { Label = "chest", Get = ChestOf, Ops = pick() },
                new View { Label = "rescue", Get = RescueOf, Ops = pick() },
                new View { Label = "military", Get = MilitaryOf, Ops = pick() },
                new View { Label = "research pod", Get = PodOf, Ops = pick() },
                new View { Label = "jukebox", Get = JukeboxOf, Ops = new[] { GridOp("Background/Grid_01", "grid"), VignetteOp("Background/VINETTE_01") } },
                new View { Label = "item pool", Get = PoolOf, Ops = net() },
                new View { Label = "guide", Get = GuideOf, Ops = net() },
                new View { Label = "achievements", Get = AchievementsOf, Ops = net() },
                new View { Label = "options", Get = OptionsOf, Ops = net() },
                new View { Label = "language", Get = LanguageOf, Ops = new[] { DimOp("Bgr", "dim") } },
                new View { Label = "newsletter", Get = NewsletterOf, Ops = new[] { DimOp("Bgr", "dim") } },
                // the results' first step: a 0.32 black box at +-1920 (inactive in the scene; whether that step shows it is unproven)
                new View { Label = "results", Get = ResultsOf, Ops = new[] { new Op { Path = "Step_00/Background", Label = "step-1 box", Kind = Kind.Grow, H = 2682f, Cy = -128f, Y = false } } },
                new View { Label = "credits", Get = CreditsOf, KeepFrame = true },
                new View { Label = "demo end", Get = DemoEndOf, KeepFrame = true },
            };
            // the pause menu and the results' own backdrop are already 7680 x 4320 (stretched, scale 2): nothing to do
        }

        static View[] MenuViews()
        {
            if (_menuViews != null) return _menuViews;
            Func<Op[]> net = () => new[] { GridOp("Bgr_Net", "net"), DimOp("Bgr_Net/Bgr_Net", "dim") };
            return _menuViews = new[]
            {
                // behind every main menu screen without its own backdrop (run setup, team leader, arena, options, achievements, guide ...)
                new View { Label = "backdrop", Child = "Bgr_Net", Required = true, Ops = new[] { GridOp("", "net"), DimOp("Bgr_Net", "dim") } },
                new View { Label = "item pool", Child = "UIViewItemsPoolMainMenu", Ops = net() },
                new View { Label = "training yard", Child = "UIViewSkillTree", Ops = new[] { GridOp("Bgr_Net", "net"), DimOp("Bgr_Net/Bgr_Net", "dim"), new Op { Path = "ResetPointsConfirm/Panel (3)", Label = "reset box", Kind = Kind.Grow, W = 3740f, H = 2105f } } },
                // the camp's top bar (4840 wide, 157 high at the top edge) and the rule under it: wider; on a taller screen the bar also reaches up
                new View { Label = "camp", Child = "UIViewGameHub", Ops = new[] { new Op { Path = "Buttons/Bgr", Label = "bar", Kind = Kind.Grow, W = 4840f, H = 157f, Cy = 1001f, TopOnly = true },
                                                                                    new Op { Path = "Buttons/Line", Label = "rule", Kind = Kind.Grow, W = 1916f, H = 6f, Cy = 924f, Y = false } } },
                new View { Label = "camp dialogue", Child = "UIViewGameHubDialogue", Ops = new[] { DimOp("Bgr", "dim") } },
                new View { Label = "camp dialogue 2", Child = "UIGameHubDialogue_v2", Ops = new[] { DimOp("Bgr", "dim") } },
                new View { Label = "quest", Child = "UIViewGameHubQuest", Ops = new[] { DimOp("Bgr", "dim") } },
                new View { Label = "radio", Child = "UIViewGameHubRadio", Ops = new[] { DimOp("Bgr", "dim") } },
                new View { Label = "newsletter", Child = "UINewsletterPanel", Ops = new[] { DimOp("Bgr", "dim") } },
                new View { Label = "language", Child = "UIViewOptions_LanguageSelector", Ops = new[] { DimOp("Bgr", "dim") } },
                new View { Label = "curator", Child = "UIViewSteamCuratorFollow", Ops = new[] { new Op { Path = "RaycastBlockImage", Label = "dim", Kind = Kind.Grow, W = 4444f, H = 3333f } } },
                new View { Label = "popups", Child = "PopupManager", Ops = new[] { new Op { Path = "UIPopupView/RaycastBlockImage", Label = "dim", Kind = Kind.Grow, W = 4444f, H = 3333f } } },
            };
        }

        // ---- the game members, one small accessor each: a member a game patch takes away fails alone ----
        [MethodImpl(MethodImplOptions.NoInlining)] static GameObject BorderOf(UIGameplay h) { return h.borderGameObject; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Component LevelUpOf(UIGameplay h) { return h.UILevelUp; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Component ChestOf(UIGameplay h) { return h.UIChestOpened; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Component RescueOf(UIGameplay h) { return h.UICharacterRescue; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Component MilitaryOf(UIGameplay h) { return h.UIMilitaryTraining; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Component PodOf(UIGameplay h) { return h.UIHashtagEvent; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Component JukeboxOf(UIGameplay h) { return h.uiViewJukebox; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Component PoolOf(UIGameplay h) { return h.uiViewItemsPoolPauseMenu; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Component GuideOf(UIGameplay h) { return h.uiViewGuide; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Component AchievementsOf(UIGameplay h) { return h.UIViewAchievements; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Component OptionsOf(UIGameplay h) { return h.UIViewOptions; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Component LanguageOf(UIGameplay h) { return h.UIViewLanguageSelector; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Component NewsletterOf(UIGameplay h) { return h.UINewsletterPanel; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Component ResultsOf(UIGameplay h) { return h.UIDefeat; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Component CreditsOf(UIGameplay h) { return h.UIViewCreditsEndGame; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Component DemoEndOf(UIGameplay h) { return h.UIDemoEnd; }
        [MethodImpl(MethodImplOptions.NoInlining)] static UIMainMenu MainMenuOf() { return UIMainMenu.s_instance; }

        // ---- the state of one canvas (the run's HUD, the main menu): what was changed, to put it back ----
        sealed class Change { public RectTransform Rt; public Vector2 Size, Pos; public SpriteRenderer Sr; public Vector2 SrSize; public GameObject Made; }

        sealed class Site
        {
            public readonly string Name;                // the log prefix: "" for the run, "main menu: "
            public IntPtr Ptr;                          // the UIGameplay / UIMainMenu it is set up for
            public RectTransform Canvas;
            public GameObject Frame;
            public float W = -1f, H = -1f;              // the canvas size, setting and variant last decided for
            public bool Want, WingsOn, Applied, PiecesShown;
            public readonly List<Change> Changes = new List<Change>();
            public readonly List<GameObject> Pieces = new List<GameObject>();
            public readonly List<GameObject> Keep = new List<GameObject>();
            public Site(string name) { Name = name; }
            public void Reset() { Applied = false; PiecesShown = false; Frame = null; Changes.Clear(); Pieces.Clear(); Keep.Clear(); }
            /// <summary>The objects went with their scene: forget them, nothing to put back.</summary>
            public void Drop() { Reset(); Ptr = IntPtr.Zero; Canvas = null; W = H = -1f; Want = WingsOn = false; }
        }

        sealed class Target { public Op Op; public RectTransform Rt; public SpriteRenderer Sr; public Image Img; public bool Covered; }

        static readonly Site _run = new Site(""), _menu = new Site("main menu: ");
        static int _gen, _runGen = -1, _menuGen = -1, _runNext;
        static float _menuNext;
        static readonly HashSet<string> _said = new HashSet<string>();
        static Il2CppStructArray<Vector3> _cornersArr;       // made on first use, never at load
        static Il2CppStructArray<Vector3> Corners { get { return _cornersArr ?? (_cornersArr = new Il2CppStructArray<Vector3>(4)); } }

        static void Once(string key, string warning) { if (_said.Add(key)) Plugin.Logger.LogWarning(warning); }

        // ================================================================ the ticks
        /// <summary>From the HUD's Update post-fix, every frame, after the game set its frame for the frame.</summary>
        public static void Tick(UIGameplay hud)
        {
            if (hud == null) return;
            var s = _run;
            try
            {
                IntPtr p = hud.Pointer;
                if (p != s.Ptr) { s.Drop(); s.Ptr = p; _runNext = 0; }
                int f = Time.frameCount;
                if (f >= _runNext || _runGen != _gen)
                {
                    _runNext = f + RunEvery; _runGen = _gen;
                    if (s.Canvas == null) s.Canvas = hud.transform.TryCast<RectTransform>();
                    if (s.Canvas != null && Decide(s, Current() != WideMode.Off, "off")) ApplyRun(hud, s);
                }
                if (s.Applied && s.Keep.Count > 0) KeepFrame(s, true);
            }
            catch (Exception e) { Once("tick", "[wide] the run's menus: " + e.GetType().Name + ": " + e.Message); }
        }

        /// <summary>The HUD went away with its scene: so did everything changed under it.</summary>
        public static void Forget() { _run.Drop(); }

        /// <summary>The run's menus reach past the game's 16:9 frame (0.15.0, C15-12: the WHY panel in a side wing): the frame's pieces
        /// are hidden on a canvas wider than 3840 units and none shows again; <paramref name="wing"/> = the canvas units on each side.
        /// False on 16:9 and on taller screens (16:10, the Steam Deck), with WideMenus off, while a frame-keeping menu has the pieces
        /// back, and when the frame did not match (left as the game has it).</summary>
        public static bool RunWings(out float wing)
        {
            var s = _run; wing = 0f;
            if (!s.Applied || s.PiecesShown || s.W - RefW < 1f) return false;
            foreach (var p in s.Pieces) { try { if (p == null || p.activeSelf) return false; } catch { return false; } }
            wing = (s.W - RefW) / 2f;
            return true;
        }

        /// <summary>From GameMaster.Update (every scene): the main menu and the camp, every 0.25 s, at once after a setting change.</summary>
        public static void MenuTick()
        {
            var s = _menu;
            try
            {
                if (s.Applied && s.Keep.Count > 0) KeepFrame(s, false);
                float now = Time.realtimeSinceStartup;
                if (now < _menuNext && _menuGen == _gen) return;
                _menuNext = now + MenuEvery; _menuGen = _gen;
                UIMainMenu m = null;
                try { m = MainMenuOf(); } catch (Exception e) { Once("menu:get", "[wide] main menu: not readable (" + e.GetType().Name + ") - left as it is"); }
                if (m == null) { if (s.Ptr != IntPtr.Zero) s.Drop(); return; }
                IntPtr p = m.Pointer;
                if (p != s.Ptr) { s.Drop(); s.Ptr = p; }
                if (s.Canvas == null) s.Canvas = m.transform.TryCast<RectTransform>();
                if (s.Canvas == null) return;
                var mode = Current();
                if (Decide(s, mode == WideMode.Everywhere, mode == WideMode.Off ? "off" : "during runs only")) ApplyMenu(s);
            }
            catch (Exception e) { Once("menu", "[wide] main menu: " + e.GetType().Name + ": " + e.Message); }
        }

        // the canvas size, the setting and the variant against what was last decided for them: a change puts back what was
        // done; true = apply now. A failed apply is not tried again until one of them changes.
        static bool Decide(Site s, bool want, string offWhy)
        {
            Rect r = s.Canvas.rect;
            float w = r.width, h = r.height;
            bool wings = Wings != null && Wings.Value;
            if (s.Want == want && s.WingsOn == wings && Mathf.Abs(w - s.W) <= 1f && Mathf.Abs(h - s.H) <= 1f) return false;
            bool wide = w - RefW >= 1f || h - RefH >= 1f;
            string why = !want ? offWhy : !wide ? "16:9" : s.WingsOn != wings && s.W > 0f ? "variant" : "canvas " + Dims(s.W, s.H) + " -> " + Dims(w, h);
            s.Want = want; s.WingsOn = wings; s.W = w; s.H = h;
            if (s.Applied) Restore(s, why);
            return want && wide;
        }

        // the frame-keeping menus (end credits, demo end, a menu whose backdrop did not match): the pieces show while one is up.
        // In a run the game switches the frame itself, so with the frame off nothing is read further.
        static void KeepFrame(Site s, bool gated)
        {
            bool up = false;
            if (!gated || s.Frame == null || s.Frame.activeSelf)
                foreach (var k in s.Keep) if (k != null && k.activeInHierarchy) { up = true; break; }
            if (up == s.PiecesShown) return;
            s.PiecesShown = up;
            foreach (var p in s.Pieces) p.SetActive(up);
        }

        // ================================================================ apply / restore
        static void ApplyRun(UIGameplay hud, Site s)
        {
            long t = Perf.Begin();
            try
            {
                string head = Head(s);
                // first find and check everything, then change: a frame that does not match leaves the run as the game has it
                var plans = new List<KeyValuePair<View, List<Target>>>();
                var keep = new List<KeyValuePair<string, GameObject>>();
                foreach (var v in RunViews())
                {
                    Component c = null; string err = null;
                    try { c = v.Get(hud); } catch (Exception e) { err = e.GetType().Name; }
                    Transform view = null; try { if (c != null) view = c.transform; } catch { }
                    if (view == null) { Once("run:" + v.Label, "[wide] " + v.Label + ": not found" + (err != null ? " (" + err + ")" : "") + " - left as it is"); continue; }
                    if (v.KeepFrame) { keep.Add(new KeyValuePair<string, GameObject>(v.Label, view.gameObject)); continue; }
                    var ts = new List<Target>();
                    string why = Resolve(s, view, v, ts);
                    if (why != null)
                    {
                        Once("run:" + v.Label + ":" + why, "[wide] " + v.Label + ": " + why + " - this menu keeps the game's frame");
                        keep.Add(new KeyValuePair<string, GameObject>(v.Label + " (did not match)", view.gameObject));
                        continue;
                    }
                    plans.Add(new KeyValuePair<View, List<Target>>(v, ts));
                }
                GameObject frame = null; string ferr = null;
                try { frame = BorderOf(hud); } catch (Exception e) { ferr = "not readable (" + e.GetType().Name + ")"; }
                string fwhy = ferr ?? TakeFrame(s, frame);
                if (fwhy != null) { Plugin.Logger.LogWarning("[wide] " + head + ": the game's frame left as it is - " + fwhy); s.Reset(); return; }
                Finish(s, head, plans, keep);
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[wide] the run's menus: " + e.GetType().Name + ": " + e.Message); try { Restore(s, "error"); } catch { } }
            finally { Perf.End("wide.apply", t); }
        }

        static void ApplyMenu(Site s)
        {
            long t = Perf.Begin();
            try
            {
                string head = Head(s);
                if (s.W / 2f > ArtHalf)
                {
                    Plugin.Logger.LogInfo("[wide] main menu: " + head + ": frame kept - the main menu art ends " + ArtHalf.ToString("0", CultureInfo.InvariantCulture) + " units from the centre, this canvas reaches " + (s.W / 2f).ToString("0", CultureInfo.InvariantCulture));
                    return;
                }
                var plans = new List<KeyValuePair<View, List<Target>>>();
                var keep = new List<KeyValuePair<string, GameObject>>();
                foreach (var v in MenuViews())
                {
                    Transform view = s.Canvas.Find(v.Child);
                    string why = view == null ? "no " + v.Child : null;
                    var ts = new List<Target>();
                    if (why == null) why = Resolve(s, view, v, ts);
                    if (why == null) { plans.Add(new KeyValuePair<View, List<Target>>(v, ts)); continue; }
                    if (v.Required) { Plugin.Logger.LogWarning("[wide] main menu: " + head + ": the game's frame left as it is - " + v.Label + ": " + why); return; }
                    Once("menu:" + v.Label + ":" + why, "[wide] main menu: " + v.Label + ": " + why + (view != null ? " - this screen keeps the game's frame" : " - left as it is"));
                    if (view != null) keep.Add(new KeyValuePair<string, GameObject>(v.Label + " (did not match)", view.gameObject));
                }
                Transform ft = s.Canvas.Find("Border_01");
                string fwhy = TakeFrame(s, ft != null ? ft.gameObject : null);
                if (fwhy != null) { Plugin.Logger.LogWarning("[wide] main menu: " + head + ": the game's frame left as it is - " + fwhy); s.Reset(); return; }
                Finish(s, head, plans, keep);
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[wide] main menu: " + e.GetType().Name + ": " + e.Message); try { Restore(s, "error"); } catch { } }
            finally { Perf.End("wide.menu", t); }
        }

        // the frame is off: grow what was checked, note the frame-keeping menus, one log line
        static void Finish(Site s, string head, List<KeyValuePair<View, List<Target>>> plans, List<KeyValuePair<string, GameObject>> keep)
        {
            float ex = Mathf.Max(0f, (s.W - RefW) / 2f), ey = Mathf.Max(0f, (s.H - RefH) / 2f);
            var groups = new List<KeyValuePair<string, List<string>>>();
            foreach (var pv in plans)
            {
                var what = new StringBuilder();
                foreach (var tg in pv.Value)
                {
                    bool done;
                    if (tg.Covered) done = false;
                    else if (tg.Op.Kind == Kind.Sprite) done = Stretch(s, tg.Sr, ex, ey);
                    else if (tg.Op.Kind == Kind.Grid && s.WingsOn && tg.Img != null) done = MakeWings(s, tg.Rt, tg.Img, ex, ey);
                    else done = Grow(s, tg.Rt, tg.Op, ex, ey);
                    if (!done) continue;        // already edge to edge, or an element that only widens on a taller screen
                    if (what.Length > 0) what.Append(", ");
                    what.Append(tg.Op.Label);
                }
                if (what.Length > 0) Group(groups, what.ToString(), pv.Key.Label);
            }
            var kept = new List<string>();
            foreach (var k in keep) { s.Keep.Add(k.Value); kept.Add(k.Key); }
            s.Applied = true;
            var sb = new StringBuilder("[wide] ").Append(s.Name).Append(head).Append(": frame off");
            sb.Append("; grown +").Append((2f * ex).ToString("0", CultureInfo.InvariantCulture)).Append(" x +").Append((2f * ey).ToString("0", CultureInfo.InvariantCulture))
              .Append(" units").Append(s.WingsOn ? " (grid wings)" : "").Append(": ");
            for (int i = 0; i < groups.Count; i++) sb.Append(i > 0 ? " | " : "").Append(string.Join(", ", groups[i].Value)).Append(": ").Append(groups[i].Key);
            if (groups.Count == 0) sb.Append("nothing");
            if (kept.Count > 0) sb.Append("; frame kept for: ").Append(string.Join(", ", kept));
            Plugin.Logger.LogInfo(sb.ToString());
        }

        static void Group(List<KeyValuePair<string, List<string>>> groups, string what, string label)
        {
            foreach (var g in groups) if (g.Key == what) { g.Value.Add(label); return; }
            groups.Add(new KeyValuePair<string, List<string>>(what, new List<string> { label }));
        }

        /// <summary>How many times a site was put back this session (each with its '[wide] ... restored (why)' line: every way back to the
        /// game's frame goes through <see cref="Restore"/>; a failed apply changes nothing before it gives up). The pause walk's check
        /// (0.15.0, C15-08).</summary>
        public static int Restores { get; private set; }

        /// <summary>The run's menus have the frame off now (the pause walk's check of the re-apply, 0.15.0 C15-08).</summary>
        public static bool RunApplied { get { return _run.Applied; } }

        /// <summary>Puts back everything in reverse, shows the frame again, forgets.</summary>
        static void Restore(Site s, string why)
        {
            int n = 0;
            for (int i = s.Changes.Count - 1; i >= 0; i--)
            {
                var c = s.Changes[i];
                try
                {
                    if (c.Made != null) { c.Made.SetActive(false); UnityEngine.Object.Destroy(c.Made); }
                    else if (c.Sr != null) c.Sr.size = c.SrSize;
                    else if (c.Rt != null) { c.Rt.sizeDelta = c.Size; c.Rt.anchoredPosition = c.Pos; }
                    n++;
                }
                catch { }
            }
            int shown = 0;
            foreach (var p in s.Pieces) { try { p.SetActive(true); shown++; } catch { } }
            Plugin.Logger.LogInfo("[wide] " + s.Name + "restored (" + why + "): " + n + " change" + (n == 1 ? "" : "s") + " undone, " + shown + " frame piece" + (shown == 1 ? "" : "s") + " back");
            Restores++;
            s.Reset();
        }

        // ================================================================ checks
        // finds every element of a menu and checks it against the scene data before anything is touched: null = all good
        static string Resolve(Site s, Transform view, View v, List<Target> into)
        {
            foreach (var op in v.Ops)
            {
                Transform t = op.Path.Length == 0 ? view : view.Find(op.Path);
                if (t == null) return "no " + op.Path;
                var tg = new Target { Op = op };
                if (op.Kind == Kind.Sprite)
                {
                    tg.Sr = t.GetComponent<SpriteRenderer>();
                    if (tg.Sr == null) return op.Path + " has no sprite renderer";
                    var size = tg.Sr.size; var dm = tg.Sr.drawMode;
                    if (dm != SpriteDrawMode.Sliced || Mathf.Abs(size.x - 40f) > 0.5f || Mathf.Abs(size.y - 22.5f) > 0.5f)
                        return op.Path + " is " + dm + " " + size.x.ToString("0.##", CultureInfo.InvariantCulture) + "x" + size.y.ToString("0.##", CultureInfo.InvariantCulture) + ", not sliced 40x22.5";
                    var c = PointIn(t.position, s.Canvas);
                    if (Mathf.Abs(c.x) > Drift || Mathf.Abs(c.y) > Drift) return op.Path + " sits at " + Dims2(c.x, c.y) + ", not the centre";
                }
                else
                {
                    tg.Rt = t.TryCast<RectTransform>();
                    if (tg.Rt == null) return op.Path + " is not a rect";
                    tg.Img = t.GetComponent<Image>();
                    if (tg.Img == null) return (op.Path.Length > 0 ? op.Path : v.Child) + " has no image";
                    Vector3 ps = ParentScale(tg.Rt, s.Canvas);
                    Rect r = tg.Rt.rect;
                    float w = r.width * Mathf.Abs(ps.x), h = r.height * Mathf.Abs(ps.y);
                    var c = RectCentre(tg.Rt, s.Canvas);
                    // already edge to edge (a menu stretched over the whole canvas, as the main menu's language selector is):
                    // nothing to do - and not a mismatch
                    bool fullX = !op.X || (c.x - w / 2f <= 1f - s.W / 2f && c.x + w / 2f >= s.W / 2f - 1f);
                    bool fullY = !op.Y || (c.y + h / 2f >= s.H / 2f - 1f && (op.TopOnly || c.y - h / 2f <= 1f - s.H / 2f));
                    if (fullX && fullY) { tg.Covered = true; into.Add(tg); continue; }
                    if (!Near(w, op.W) || !Near(h, op.H)) return (op.Path.Length > 0 ? op.Path : v.Child) + " is " + Dims(w, h) + ", not " + Dims(op.W, op.H);
                    if (Mathf.Abs(c.x) > Drift || Mathf.Abs(c.y - op.Cy) > Drift) return (op.Path.Length > 0 ? op.Path : v.Child) + " is centred at " + Dims2(c.x, c.y) + ", not " + Dims2(0f, op.Cy);
                }
                into.Add(tg);
            }
            return null;
        }

        // the frame: 11520 x 6480 with four opaque black Images; they are switched off (the parent stays the game's), null = done
        static string TakeFrame(Site s, GameObject frame)
        {
            if (frame == null) return "no frame object";
            var tr = frame.transform;
            var frt = tr.TryCast<RectTransform>();
            if (frt != null) { var sd = frt.sizeDelta; if (!Near(sd.x, 11520f) || !Near(sd.y, 6480f)) return "the frame is " + Dims(sd.x, sd.y) + ", not 11520x6480"; }
            int n = tr.childCount;
            if (n != 4) return "the frame has " + n + " pieces, not 4";
            var pieces = new List<GameObject>(4);
            for (int i = 0; i < n; i++)
            {
                var c = tr.GetChild(i);
                var img = c.GetComponent<Image>();
                if (img == null) return "frame piece " + (i + 1) + " is not an image";
                var col = img.color;
                if (col.a < 0.99f || col.r > 0.02f || col.g > 0.02f || col.b > 0.02f) return "frame piece '" + c.name + "' is not opaque black";
                pieces.Add(c.gameObject);
            }
            s.Frame = frame;
            foreach (var go in pieces) if (go.activeSelf) { go.SetActive(false); s.Pieces.Add(go); }
            s.PiecesShown = false;
            return null;
        }

        static bool Near(float v, float want) { return Mathf.Abs(v - want) <= Mathf.Max(48f, 0.025f * want); }

        // a transform's parent scale in canvas units per local unit (UI rects are never rotated)
        static Vector3 ParentScale(Transform t, RectTransform canvas)
        {
            var p = t.parent;
            Vector3 a = p != null ? p.lossyScale : Vector3.one, c = canvas.lossyScale;
            return new Vector3(c.x != 0f ? a.x / c.x : 1f, c.y != 0f ? a.y / c.y : 1f, 1f);
        }

        static Vector2 PointIn(Vector3 world, RectTransform canvas)
        {
            var p = canvas.InverseTransformPoint(world); var r = canvas.rect;
            return new Vector2(p.x - r.center.x, p.y - r.center.y);
        }

        static Vector2 RectCentre(RectTransform rt, RectTransform canvas)
        {
            rt.GetWorldCorners(Corners);
            var a = PointIn(Corners[0], canvas); var b = PointIn(Corners[2], canvas);
            return new Vector2((a.x + b.x) / 2f, (a.y + b.y) / 2f);
        }

        // ================================================================ the three changes
        // a rect reaches past the screen edges: ex / ey canvas units more on each side (top only for a bar at the top edge),
        // pivot kept; sizeDelta grows in its own units - the element's own scale is taken as at most 1 (the grid rests at 1
        // and is never animated below it), so a grid caught at its hidden 1.4 still reaches the edges at rest
        static bool Grow(Site s, RectTransform rt, Op op, float ex, float ey)
        {
            float l = op.X && ex >= 0.5f ? ex + Pad : 0f;
            float t = op.Y && ey >= 0.5f ? ey + Pad : 0f, b = op.TopOnly ? 0f : t;
            if (l <= 0f && t <= 0f) return false;
            Vector3 ps = ParentScale(rt, s.Canvas), own = rt.localScale;
            float px = Mathf.Max(1e-3f, Mathf.Abs(ps.x)), py = Mathf.Max(1e-3f, Mathf.Abs(ps.y));
            float ox = Mathf.Clamp(Mathf.Abs(own.x), 1e-3f, 1f), oy = Mathf.Clamp(Mathf.Abs(own.y), 1e-3f, 1f);
            float lp = l / px, tp = t / py, bp = b / py;      // in the parent's units, as anchoredPosition is
            Vector2 sd = rt.sizeDelta, ap = rt.anchoredPosition, pv = rt.pivot;
            s.Changes.Add(new Change { Rt = rt, Size = sd, Pos = ap });
            rt.sizeDelta = new Vector2(sd.x + 2f * lp / ox, sd.y + (bp + tp) / oy);
            rt.anchoredPosition = new Vector2(ap.x - lp + pv.x * 2f * lp, ap.y - bp + pv.y * (bp + tp));
            return true;
        }

        // a sliced vignette: its soft border moves out to the new edges (it already overhangs 16:9 by 80 x 45 units: kept)
        static bool Stretch(Site s, SpriteRenderer sr, float ex, float ey)
        {
            if (ex < 0.5f && ey < 0.5f) return false;
            Vector3 a = sr.transform.lossyScale, c = s.Canvas.lossyScale;
            float sx = Mathf.Max(1e-3f, Mathf.Abs(c.x != 0f ? a.x / c.x : 1f)), sy = Mathf.Max(1e-3f, Mathf.Abs(c.y != 0f ? a.y / c.y : 1f));
            var size = sr.size;
            s.Changes.Add(new Change { Sr = sr, SrSize = size });
            sr.size = new Vector2(size.x + (ex >= 0.5f ? 2f * ex / sx : 0f), size.y + (ey >= 0.5f ? 2f * ey / sy : 0f));
            return true;
        }

        // [Debug] WideMenusWings: mirrored copies of a grid layer beside it (first children: under the dim it carries, and they
        // follow its animated scale and fade); one per side covers up to 48:9
        static bool MakeWings(Site s, RectTransform rt, Image src, float ex, float ey)
        {
            if (ex < 0.5f && ey < 0.5f) return false;
            Rect r = rt.rect;
            if (ex >= 0.5f)
            {
                Wing(s, rt, src, "YC_WideL", new Vector2(-r.width, 0f), new Vector3(-1f, 1f, 1f));
                Wing(s, rt, src, "YC_WideR", new Vector2(r.width, 0f), new Vector3(-1f, 1f, 1f));
            }
            if (ey >= 0.5f)
            {
                Wing(s, rt, src, "YC_WideB", new Vector2(0f, -r.height), new Vector3(1f, -1f, 1f));
                Wing(s, rt, src, "YC_WideT", new Vector2(0f, r.height), new Vector3(1f, -1f, 1f));
            }
            return true;
        }

        static void Wing(Site s, RectTransform parent, Image src, string name, Vector2 at, Vector3 scale)
        {
            var w = Ui.NewRect(name, parent);
            s.Changes.Add(new Change { Made = w.gameObject });
            try { w.gameObject.layer = parent.gameObject.layer; } catch { }
            w.anchorMin = Vector2.zero; w.anchorMax = Vector2.one; w.pivot = new Vector2(0.5f, 0.5f);
            w.sizeDelta = Vector2.zero; w.anchoredPosition = at; w.localScale = scale;
            var img = w.gameObject.AddComponent(Il2CppType.Of<Image>()).TryCast<Image>();
            img.sprite = src.sprite; img.type = src.type; img.material = src.material; img.color = src.color; img.raycastTarget = src.raycastTarget;
            w.SetAsFirstSibling();
        }

        // ================================================================ text
        static string Dims(float w, float h) { return w.ToString("0", CultureInfo.InvariantCulture) + "x" + h.ToString("0", CultureInfo.InvariantCulture); }
        static string Dims2(float x, float y) { return "(" + x.ToString("0", CultureInfo.InvariantCulture) + ", " + y.ToString("0", CultureInfo.InvariantCulture) + ")"; }

        static string Ratio(int w, int h)
        {
            if (w <= 0 || h <= 0) return "?";
            float a = (float)w / h;
            if (Mathf.Abs(a - 16f / 9f) < 0.01f) return "16:9";
            if (Mathf.Abs(a - 1.6f) < 0.01f) return "16:10";
            if (Mathf.Abs(a - 4f / 3f) < 0.01f) return "4:3";
            if (Mathf.Abs(a - 1.25f) < 0.01f) return "5:4";
            if (Mathf.Abs(a - 1.5f) < 0.01f) return "3:2";
            if (a > 2.3f && a < 2.42f) return "21:9";
            if (a > 3.5f && a < 3.6f) return "32:9";
            return a.ToString("0.00", CultureInfo.InvariantCulture) + ":1";
        }

        // "screen 3440x1440, canvas 5160x2160 (21:9, 440 px each side)"
        static string Head(Site s)
        {
            int w = 0, h = 0;
            try { w = UnityEngine.Screen.width; h = UnityEngine.Screen.height; } catch { }
            float unit = s.H > 0f ? h / s.H : 0f;
            string bars = s.W - RefW >= 1f ? Mathf.RoundToInt((s.W - RefW) / 2f * unit) + " px each side" : Mathf.RoundToInt((s.H - RefH) / 2f * unit) + " px above and below";
            return "screen " + w + "x" + h + ", canvas " + Dims(s.W, s.H) + " (" + Ratio(w, h) + ", " + bars + ")";
        }

        /// <summary>The DISPLAY tab's value.</summary>
        public static string Label()
        {
            var m = Current();
            return m == WideMode.DuringRuns ? "During runs" : m.ToString();
        }

        /// <summary>The DISPLAY tab's left / right: the next value (saved by the config entry; the ticks follow at once).</summary>
        public static void Step(int d)
        {
            if (Mode == null) return;
            var all = (WideMode[])Enum.GetValues(typeof(WideMode));
            int i = Array.IndexOf(all, Mode.Value);
            Mode.Value = all[((i + d) % all.Length + all.Length) % all.Length];
        }

        /// <summary>The DISPLAY tab's help, written for the screen the game runs on.</summary>
        public static string Help()
        {
            const string modes = "EVERYWHERE (default): every menu fills the screen, its own dark backdrop carried to the edges; the cards, buttons and text stay where the game puts them. DURING RUNS: the run's menus only. OFF: the game's frame.";
            int w = 0, h = 0;
            try { w = UnityEngine.Screen.width; h = UnityEngine.Screen.height; } catch { }
            if (w <= 0 || h <= 0) return modes;
            float a = (float)w / h;
            string screen = "This screen is " + w + " x " + h + " (" + Ratio(w, h) + ")";
            if (Mathf.Abs(a - 16f / 9f) < 0.01f) return screen + ": the game draws no frame here, so nothing changes. On a wider or taller screen (an ultrawide monitor, the Steam Deck): " + modes;
            string bars = a > 16f / 9f ? Mathf.RoundToInt((w - h * 16f / 9f) / 2f) + " px of black on each side of" : Mathf.RoundToInt((h - w * 9f / 16f) / 2f) + " px of black above and below";
            string text = screen + ": the game paints " + bars + " its menus - in a run with every level-up, chest, rescue, pause and the results, on the main menu and in the camp all the time. " + modes;
            if (a > 16f / 9f && RefH * a / 2f > ArtHalf) text += " Wider than the main menu art (about 2.65:1): the main menu keeps its frame.";
            return text;
        }
    }
}
