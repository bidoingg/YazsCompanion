// The mod menu: BUILDS (the build the advice follows per survivor, with an editor for your own), ADVICE (the standing
// orders of the ranking: level-up style, the run clock, the game mode, squad synergy, what the run is for, caution,
// tags, recruiting) and DISPLAY (what the mod draws) - and MODS, a fourth tab that exists only while another mod
// has registered an option (Api\Extensions.cs): its rows are the same rows, under a header per mod. Opened from a
// COMPANION button the mod adds to the main menu and the pause menu, or with a key (F10). Mouse, keyboard and controller.
//
// It lives on its own overlay canvas and runs its own focus: input is polled once a frame (the mouse by hit-testing
// its controls, the keyboard through Unity's legacy input, the controller through the game's own Rewired wrapper,
// GameMaster.GetButtonDown("UISubmit" / "Cancel" / "GoNextTab" / "GoPrevTab") and the "Move" axes), so nothing here
// depends on the game's event system. While it is open the game's menu underneath is held still: a full-screen
// blocker takes the mouse, the event system's selection is cleared every frame so Submit cannot press a game
// button, and the Update of the main menu / pause menu is skipped (Harmony prefixes at the end of this file).
//
// Layout is absolute on a 3840 x 2160 stage centred in the canvas (the game's own reference), so it fits 16:9,
// the Deck's 16:10 and 21:9 alike. Body text is 42 units or more: a unit is a third of a pixel on the Deck.
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YazsCompanion
{
    internal static class Menu
    {
        const float W = 3840f, H = 2160f;
        static readonly string[] Tabs = { "BUILDS", "ADVICE", "DISPLAY", "MODS" };          // MODS only while another mod has an option registered
        static readonly string[] TabGlyphs = { "chevrons", "clock", "eye", "link" };

        sealed class Ctl
        {
            public string Key; public RectTransform Rt; public Image Bg; public RectTransform Glow;
            public Func<string> Desc; public Action Press; public Action<int> Change; public Action Focused;
            public RectTransform Left, Right;
            public Scroller In; public float Lead;      // the clipped area it lives in, if any (it is only under the mouse where that shows it), and the height of the headers over it
            public Action Refresh;          // read the shown value again (a row whose value another row's change may have moved)
        }

        /// <summary>A clipped area whose content follows the focus: the rows of the MODS tab (down), the cards of the BUILDS
        /// tab once other mods lend builds and they no longer fit side by side (across).</summary>
        sealed class Scroller
        {
            public string Key; public RectTransform View, Content, Before, After; public bool Across;
            public float Size, Extent, Pos, Inset;       // the view's length, the content's, how far it is scrolled, the content's margin inside the view
            public float Max { get { return Mathf.Max(0f, Extent - Size); } }
        }

        static GameObject _go; static RectTransform _stage, _body; static CanvasGroup _group, _bodyGroup;
        static TextMeshProUGUI _template, _desc, _hints; static readonly TextMeshProUGUI[] _tabLabels = new TextMeshProUGUI[4];
        static RectTransform _tabRule, _tabBar; static int _tabCount = 3, _extSeen = -1;
        static readonly List<Scroller> _scrolls = new List<Scroller>();
        static readonly Dictionary<string, float> _scrollPos = new Dictionary<string, float>();      // where each area stood: the body is rebuilt on every change
        static bool _building;
        static readonly List<Ctl> _ctls = new List<Ctl>(); static Ctl _focus; static string _focusKey = "";
        static bool _open, _dirty, _editing; static int _tab, _survivor, _blockFrame = -1;
        static GameObject _restoreSelected;
        static float _openedAt; static int _openFrame; static bool _armed;        // the press that opened the menu is not input for the menu
        static bool _submitIsMouse;                                                // seen once: the game's UISubmit action also fires on the left mouse button
        static TextMeshProUGUI _saved;                                             // "SAVED" in the header, lit for a moment after every change
        static RectTransform _pvWindow; static TextMeshProUGUI _pvCaption;         // DISPLAY tab: the readout itself, as configured
        static bool _pvDirty; static int _pvSample; static float _pvNextSample;
        static readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, Sprite> _portraits = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, bool> _unlocked = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        static int _copyFrom;

        public static bool IsOpen { get { return _open; } }
        /// <summary>The game's menus underneath hold still: while open, and one frame longer so the key that closed the mod
        /// menu does not also close the pause menu.</summary>
        public static bool Blocking
        {
            get
            {
                if (_open) return true;
                if (_blockFrame < 0) return false;
                if (Time.frameCount <= _blockFrame) return true;
                _blockFrame = -1; return false;
            }
        }

        // ================================================================ open / close
        // Which of the game's menus is up. Their own Update tells us: the hold-still prefixes at the end of this file run
        // every frame a menu is live (also while they skip the original), so the view they saw within the last few
        // frames IS the live one - no search. Resources.FindObjectsOfTypeAll walks every loaded object (thousands in a
        // run) and used to run twice every half second during play for the button upkeep, and every frame while the mod
        // menu was open. The search remains as the fallback, fenced in: for the main menu until its Update has been
        // seen once (the start-up seconds), for the pause menu only while the game's pause-menu flow is on, and for an
        // explicit Open() the hooks cannot place - the search then stands in for the hooks until the mod menu closes.
        const int SeenFrames = 3;
        static UIViewMainMenu _mainView; static UIPauseMenu _pauseView;
        static int _mainFrame = -100, _pauseFrame = -100;
        static bool _mainHooked, _pauseHooked, _viaSearch;

        internal static void Saw(UIViewMainMenu v) { _mainView = v; _mainFrame = Time.frameCount; _mainHooked = true; }
        internal static void Saw(UIPauseMenu v) { _pauseView = v; _pauseFrame = Time.frameCount; _pauseHooked = true; }

        static bool Live(Component view, int frame)
        {
            if (view == null || Time.frameCount - frame > SeenFrames) return false;
            try { return view.gameObject.activeInHierarchy; } catch { return false; }
        }

        static UIViewMainMenu MainMenu()
        {
            if (_mainHooked && !_viaSearch) return Live(_mainView, _mainFrame) ? _mainView : null;
            return FindActive<UIViewMainMenu>();        // only before its first Update of the session: the start-up seconds
        }
        // One search costs about 41 ms on the PC (measured with [Debug] Perf on the team-leader screen, where it still ran
        // twice a second: 4.9 s of every minute). Until the pause menu's Update has been seen, it is only looked for while
        // the game says its pause-menu flow is on - which its first Update then ends.
        static UIPauseMenu PauseMenu()
        {
            if (_pauseHooked && !_viaSearch) return Live(_pauseView, _pauseFrame) ? _pauseView : null;
            return _viaSearch || PauseFlow() ? FindActive<UIPauseMenu>() : null;
        }
        static bool PauseFlow() { try { return GameplayMaster.IsPauseMenuFlowActive; } catch { return false; } }
        /// <summary>The main menu is up right now, by its own Update (no search).</summary>
        public static bool OnMainMenu { get { return _mainHooked && Live(_mainView, _mainFrame); } }
        /// <summary>A run is being played right now (not paused): neither menu can be up, so nothing is searched for. The
        /// main menu scene has a GameplayMaster of its own, without a game mode - hence the mode test.</summary>
        static bool InRun()
        {
            try
            {
                var gm = GameplayMaster.s_instance;
                if (gm == null || GameplayMaster.IsPaused) return false;
                var mode = gm.currentGameMode;
                return mode != null && mode.IsGameplayActive;
            }
            catch { return false; }
        }

        static T FindActive<T>() where T : Component
        {
            try
            {
                var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<T>());
                for (int i = 0; i < all.Length; i++) { var o = all[i].TryCast<T>(); if (o != null && o.gameObject.activeInHierarchy) return o; }
            }
            catch { }
            return null;
        }

        public static void Open()
        {
            if (_open) return;
            try
            {
                Transform host = null; var mm = MainMenu(); var pm = mm == null ? PauseMenu() : null;
                if (mm == null && pm == null && !_viaSearch && !_pauseHooked && !InRun())
                {   // asked for, and the pause menu has never reported itself: look the old way, once, and trust the search while the mod menu is open
                    pm = FindActive<UIPauseMenu>();
                    if (pm != null) { _viaSearch = true; Plugin.Logger.LogInfo("[menu] the pause menu was found by search, its Update hook had not reported it"); }
                }
                if (mm != null) host = mm.transform; else if (pm != null) host = pm.transform;
                if (host == null) { Plugin.Logger.LogInfo("[menu] not on the main menu or the pause menu"); return; }
                _template = Ui.FindLabel(host, new[] { "Name" });
                if (_template == null) { Plugin.Logger.LogWarning("[menu] no label to clone"); return; }
                ReadGameArt();
                BuildShell();
                Panel.Suspend();            // the DISPLAY tab borrows the readout's widget for its preview; Close brings the live one back
                _open = true; _editing = false; _dirty = true;
                _openedAt = Time.realtimeSinceStartup; _openFrame = Time.frameCount; _armed = false;
                try { _lastMouse = UnityEngine.Input.mousePosition; } catch { }
                Shots.Later(1.0f, "menu_open");
                try { var es = UnityEngine.EventSystems.EventSystem.current; if (es != null) { _restoreSelected = es.currentSelectedGameObject; es.SetSelectedGameObject(null); } } catch { }
                Intro();
                Plugin.Logger.LogInfo("[menu] opened over " + (mm != null ? "the main menu" : "the pause menu") + ", label '" + _template.name + "'");
            }
            catch (Exception e) { Plugin.Logger.LogError("[menu] open: " + e); Close(); }
        }

        public static void Close()
        {
            bool was = _open;
            _open = false; _viaSearch = false; _blockFrame = Time.frameCount + 1;
            Fx.Cancel("menu");
            try { if (_go != null) UnityEngine.Object.Destroy(_go); } catch { }
            _go = null; _stage = null; _body = null; _ctls.Clear(); _scrolls.Clear(); _focus = null; _saved = null; _pvWindow = null; _pvCaption = null;
            _tabBar = null; _tabRule = null;
            try
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (was && es != null && _restoreSelected != null && _restoreSelected.activeInHierarchy) es.SetSelectedGameObject(_restoreSelected);
            }
            catch { }
            _restoreSelected = null;
            if (was) { Panel.Rebuild(); Plugin.Logger.LogInfo("[menu] closed"); }    // the readout rebuilds with the new display settings
        }

        // ================================================================ the tick (GameMaster.Update postfix: alive in every scene)
        static float _nextEnsure; static int _axisX, _axisY; static float _repeatAt;

        public static void Tick()
        {
            try
            {
                PreviewTick(); PausePreviewTick();
                if (ToggleKeyDown()) { if (_open) Close(); else Open(); return; }
                if (!_open)
                {
                    float now = Time.realtimeSinceStartup;
                    if (now >= _nextEnsure) { _nextEnsure = now + 0.5f; EnsureButtons(); }
                    return;
                }
                if (_go == null || (MainMenu() == null && PauseMenu() == null)) { Close(); return; }      // the scene changed under us
                try { var es = UnityEngine.EventSystems.EventSystem.current; if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null); } catch { }
                if (_extSeen != Api.Extensions.Version)
                {   // another mod registered or took back something while the menu is open: the MODS tab comes, changes or goes
                    _extSeen = Api.Extensions.Version; BuildTabs();
                    if (_tab >= _tabCount) { _tab = 0; _focusKey = ""; }
                    _dirty = true;
                }
                if (_dirty) { _dirty = false; BuildBody(); }
                ReadInput();
                if (_dirty && _open) { _dirty = false; BuildBody(); }
                if (_open) PreviewUpkeep();
            }
            catch (Exception e) { Plugin.Logger.LogError("[menu] " + e); Close(); }
        }

        /// <summary>A setting was written (BepInEx saves the file inside the setter; builds.json is written by Builds): say so
        /// in the header for a moment. Called from the config's SettingChanged and after every change to the builds.</summary>
        internal static void SettingSaved(bool written, string why = null)
        {
            if (!_open || _saved == null) return;
            try
            {
                _saved.text = written ? "SAVED  <color=" + Theme.DimHex + ">-  applies at once</color>" : "NOT SAVED  <color=" + Theme.DimHex + ">-  " + (why ?? "the file could not be written") + "</color>";
                _saved.color = written ? Theme.GoldText : Theme.Rust;
                var label = _saved;
                Fx.Cancel("menu.saved");
                label.alpha = 1f;
                Fx.Run("menu.saved", written ? 1.6f : 6f, 0.7f, k => { label.alpha = 1f - k; });
            }
            catch { }
        }

        // DISPLAY tab: keep the readout in the preview window in step with the settings, and alive (its fade, its gold
        // highlight, its idle dimming all run from Panel.Animate); the sample plan moves on every few seconds so the
        // change highlight and the settling to the idle opacity can be seen too
        static void PreviewUpkeep()
        {
            if (_pvWindow == null) return;
            if (Time.frameCount <= _openFrame + 1) return;         // the menu canvas takes its scale a frame after it was made
            float now = Time.realtimeSinceStartup;
            try
            {
                if (_pvDirty || !Panel.Previewing)
                {
                    bool show = true; try { show = Plugin.ShowPanel.Value; } catch { }
                    if (!_pvDirty && !show) { Panel.Animate(); return; }        // switched off: nothing to keep up
                    _pvDirty = false; _pvSample = 0; _pvNextSample = now + 5f;
                    var canvas = _go.transform.TryCast<RectTransform>();
                    bool compact = true; try { compact = Plugin.PanelDetail.Value == PanelDetailLevel.Compact; } catch { }
                    Panel.CheckGlyphs(_template);
                    if (show && Panel.MenuPreview(_pvWindow, canvas, _template, Plan.Sample(0, compact))) PreviewCaption(canvas);
                    else
                    {
                        if (Panel.Previewing) Panel.PreviewEnd();
                        if (_pvCaption != null) _pvCaption.text = show ? "" : "The PLAN readout is off.";
                        if (show) { Plugin.Logger.LogInfo("[menu] the readout preview could not be built"); _pvWindow = null; }     // once, not every frame
                    }
                }
                else if (now >= _pvNextSample)
                {
                    _pvNextSample = now + 5f; _pvSample = (_pvSample + 1) % 3;
                    bool compact = true; try { compact = Plugin.PanelDetail.Value == PanelDetailLevel.Compact; } catch { }
                    if (_pvSample != 1) Panel.PreviewFresh();      // 0 -> 1 is "after a pick": two lines change and glow; the others are new squads
                    Panel.PreviewApply(Plan.Sample(_pvSample, compact));
                    PreviewCaption(_go.transform.TryCast<RectTransform>());
                }
                Panel.Animate();
            }
            catch (Exception e) { Plugin.Logger.LogInfo("[menu] preview: " + e.Message); _pvWindow = null; }
        }

        static void PreviewCaption(RectTransform canvas)
        {
            if (_pvCaption == null) return;
            float shown = Panel.ShownScale, wanted = Panel.ScaleFor(canvas);
            string px = Mathf.RoundToInt(Panel.TextPx(canvas, shown)) + " px text on this " + UnityEngine.Screen.width + " x " + UnityEngine.Screen.height + " screen";
            _pvCaption.text = "<color=" + Theme.GoldHex + ">ACTUAL SIZE</color>   " + px
                + (shown > 0 && shown < wanted - 0.01f ? "   <color=" + Theme.DimHex + ">(a long plan shrinks to stay clear of the HUD)</color>" : "");
        }

        // the key is asked for every frame of the session: parse its name only when the setting changes
        static string _toggleName; static KeyCode _toggleKey; static bool _toggleValid;
        static bool ToggleKeyDown()
        {
            try
            {
                string name = Plugin.MenuKey.Value;
                if (!ReferenceEquals(name, _toggleName))
                {
                    _toggleName = name;
                    string trimmed = (name ?? "").Trim();
                    _toggleValid = trimmed.Length > 0 && Enum.TryParse(trimmed, true, out _toggleKey);
                }
                return _toggleValid && Key(_toggleKey);
            }
            catch { return false; }
        }

        // every input source may be missing in this build (IL2CPP strips what the game never calls): say so once, carry on
        static readonly HashSet<string> _inputErrors = new HashSet<string>();
        static void InputError(string what, Exception e) { if (_inputErrors.Add(what)) Plugin.Logger.LogWarning("[menu] input source '" + what + "' unavailable: " + e.GetType().Name + ": " + e.Message); }
        static bool Key(KeyCode k) { try { return UnityEngine.Input.GetKeyDown(k); } catch (Exception e) { InputError("Input.GetKeyDown", e); return false; } }
        static bool Held(KeyCode k) { try { return UnityEngine.Input.GetKey(k); } catch (Exception e) { InputError("Input.GetKey", e); return false; } }
        static bool Pad(string action) { try { return GameMaster.GetButtonDown(action, true); } catch (Exception e) { InputError("GameMaster.GetButtonDown(" + action + ")", e); return false; } }
        static float Axis(string action) { try { return GameMaster.GetAxisOverridePause(action); } catch (Exception e) { InputError("GameMaster.GetAxisOverridePause(" + action + ")", e); return 0f; } }

        static bool MouseHeld() { try { return UnityEngine.Input.GetMouseButton(0); } catch { return false; } }
        static bool PadHeld(string action) { try { return GameMaster.GetButton(action); } catch (Exception e) { InputError("GameMaster.GetButton(" + action + ")", e); return false; } }

        static void ReadInput()
        {
            // the click or key press that opened the menu has to be over before anything counts as input for the menu: a
            // double click on the COMPANION button used to land on whatever control lay under the cursor (a logged run
            // has "SWAT follows Auto" 75 ms after the menu opened - a build chosen by accident)
            if (!_armed)
            {
                bool held = MouseHeld() || Held(KeyCode.Return) || Held(KeyCode.KeypadEnter) || Held(KeyCode.Space) || PadHeld("UISubmit");
                if (held || Time.realtimeSinceStartup - _openedAt < 0.3f) { try { _lastMouse = UnityEngine.Input.mousePosition; } catch { } return; }
                _armed = true;
            }

            // ---- direction: the stick / d-pad and the arrow keys as ONE source (the game maps the arrows onto the same axes)
            float ax = Axis("Move Horizontal"), ay = Axis("Move Vertical");
            int x = ax > 0.55f ? 1 : ax < -0.55f ? -1 : 0, y = ay > 0.55f ? 1 : ay < -0.55f ? -1 : 0;
            if (x == 0) x = Held(KeyCode.RightArrow) || Held(KeyCode.D) ? 1 : Held(KeyCode.LeftArrow) || Held(KeyCode.A) ? -1 : 0;
            if (y == 0) y = Held(KeyCode.UpArrow) || Held(KeyCode.W) ? 1 : Held(KeyCode.DownArrow) || Held(KeyCode.S) ? -1 : 0;
            float now = Time.realtimeSinceStartup;
            bool fresh = x != _axisX || y != _axisY;
            if (fresh) { _axisX = x; _axisY = y; _repeatAt = now + 0.42f; }
            bool fire = (x != 0 || y != 0) && (fresh || now >= _repeatAt);
            if (fire && !fresh) _repeatAt = now + 0.11f;

            if (Pad("GoNextTab") || Pad("GoNextTab2") || Key(KeyCode.E) || Key(KeyCode.PageDown)) { SetTab((_tab + 1) % _tabCount); return; }
            if (Pad("GoPrevTab") || Pad("GoPrevTab2") || Key(KeyCode.Q) || Key(KeyCode.PageUp)) { SetTab((_tab + _tabCount - 1) % _tabCount); return; }
            if (Pad("Cancel") || Key(KeyCode.Escape) || Key(KeyCode.Backspace)) { Back(); return; }

            bool mouseDown = Mouse();
            if (!_open) return;
            if (_scrolls.Count > 0) { float wheel = Wheel(); if (wheel != 0f) WheelScroll(wheel); }
            if (fire)
            {
                if (y != 0) Move(0, y);
                else if (_focus != null && _focus.Change != null) { _focus.Change(x); Click(); }
                else Move(x, 0);
            }
            // a frame with a mouse click is the mouse's: should the game's UISubmit action fire on the left button too, the
            // control would be worked twice (a toggle flipped and flipped back, a left arrow undone by the +1 of Submit)
            bool submit = Pad("UISubmit");
            if (mouseDown)
            {
                if (submit && !_submitIsMouse) { _submitIsMouse = true; Plugin.Logger.LogInfo("[menu] UISubmit fires on the left mouse button too: ignored on click frames"); }
                return;
            }
            if (submit || Key(KeyCode.Return) || Key(KeyCode.KeypadEnter) || Key(KeyCode.Space)) Activate(_focus);
        }

        static Vector3 _lastMouse;
        /// <summary>Hover and click; true when the left button went down this frame (whatever it hit).</summary>
        static bool Mouse()
        {
            Vector3 mp; try { mp = UnityEngine.Input.mousePosition; } catch { return false; }
            bool moved = (mp - _lastMouse).sqrMagnitude > 4f; _lastMouse = mp;
            bool down = false; try { down = UnityEngine.Input.GetMouseButtonDown(0); } catch { }
            if (!moved && !down) return false;
            var p = new Vector2(mp.x, mp.y);
            for (int i = 0; i < _tabCount; i++)
                if (down && _tabLabels[i] != null && Hit(_tabLabels[i].rectTransform, p)) { SetTab(i); return true; }
            Ctl over = null;
            foreach (var c in _ctls) if (c.Rt != null && Hit(c.Rt, p) && (c.In == null || Hit(c.In.View, p))) { over = c; break; }
            if (over == null) return down;
            if (over != _focus) Focus(over, true);
            if (!down) return false;
            if (over.Change != null && over.Left != null && Hit(over.Left, p)) { over.Change(-1); Click(); }
            else if (over.Change != null && over.Right != null && Hit(over.Right, p)) { over.Change(1); Click(); }
            else Activate(over);
            return true;
        }

        static bool Hit(RectTransform rt, Vector2 screen) { try { return RectTransformUtility.RectangleContainsScreenPoint(rt, screen, null); } catch { return false; } }

        static void Activate(Ctl c)
        {
            if (c == null) return;
            if (c.Press != null) { c.Press(); Click(); }
            else if (c.Change != null) { c.Change(1); Click(); }
        }

        static void Back()
        {
            if (_editing) { _editing = false; _focusKey = "customize"; _dirty = true; return; }
            Close();
        }

        static void SetTab(int tab)
        {
            if (tab == _tab && !_editing) return;
            _tab = tab; _editing = false; _focusKey = ""; _dirty = true;
            try { if (Plugin.Verbose.Value) Plugin.Logger.LogInfo("[menu] tab " + Tabs[tab]); } catch { }
        }

        // spatial navigation: the nearest control whose centre lies in the direction pressed
        static void Move(int dx, int dy)
        {
            if (_ctls.Count == 0) return;
            if (_focus == null) { Focus(_ctls[0], false); return; }
            Vector2 from = Centre(_focus.Rt); Ctl best = null; float bestCost = float.MaxValue;
            foreach (var c in _ctls)
            {
                if (c == _focus || c.Rt == null) continue;
                Vector2 d = Centre(c.Rt) - from;
                float along = dx != 0 ? d.x * dx : d.y * dy, across = dx != 0 ? Mathf.Abs(d.y) : Mathf.Abs(d.x);
                if (along < 8f) continue;
                float cost = along + across * 2.4f;
                if (cost < bestCost) { bestCost = cost; best = c; }
            }
            if (best != null) Focus(best, false);
        }

        static Vector2 Centre(RectTransform rt)
        {
            var p = _stage.InverseTransformPoint(rt.TransformPoint(rt.rect.center));
            return new Vector2(p.x, p.y);
        }

        static void Focus(Ctl c, bool byMouse)
        {
            if (c == _focus) return;
            if (_focus != null && _focus.Glow != null) { try { _focus.Glow.gameObject.SetActive(false); } catch { } }
            _focus = c; _focusKey = c == null ? "" : c.Key;
            if (c == null) return;
            try { if (Plugin.Verbose.Value) Plugin.Logger.LogInfo("[menu] focus " + c.Key + (byMouse ? " (mouse)" : "")); } catch { }
            if (c.Glow != null)
            {
                c.Glow.gameObject.SetActive(true);
                var img = c.Glow.GetComponent<Image>();
                Fx.Cancel("menu.glow");
                if (img != null) Fx.Loop("menu.glow", 1.8f, k => { img.color = new Color(1f, 0.93f, 0.7f, 0.62f + 0.3f * Mathf.Sin(k * Mathf.PI * 2f)); });
            }
            SetDesc(c.Desc != null ? c.Desc() : "");
            if (!byMouse) Reveal(c);        // the mouse focuses what it can see; the wheel scrolls for it
            if (c.Focused != null) c.Focused();
        }

        static void SetDesc(string text)
        {
            if (_desc == null) return;
            _desc.text = text ?? "";
            Fx.Type("menu.desc", _desc, 0f, 420f);
        }

        static void Click()
        {
            // the game's own UI click, when there is one to borrow
            try { var src = _go != null ? _go.GetComponent<AudioSource>() : null; if (src != null && src.clip != null) src.Play(); } catch { }
        }

        // ================================================================ building blocks
        static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static RectTransform Box(RectTransform parent, string name, float x, float y, float w, float h, Color color)
        {
            return Place(Ui.Image(parent, name, color), x, y, w, h);
        }

        static RectTransform Pic(RectTransform parent, string name, float x, float y, float w, float h, Sprite sprite, Color tint, bool sliced = false)
        {
            var rt = Box(parent, name, x, y, w, h, tint);
            var img = rt.GetComponent<Image>();
            if (sprite != null) { img.sprite = sprite; img.type = sliced ? Image.Type.Sliced : Image.Type.Simple; img.preserveAspect = !sliced; }
            else if (!sliced) img.enabled = false;
            return rt;
        }

        static TextMeshProUGUI Text(RectTransform parent, string name, float x, float y, float w, float h, float size, Color color, string text,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool bold = false, bool wrap = false)
        {
            var t = Ui.CloneText(_template, parent, name);
            if (t == null) return null;
            if (!wrap)
            {
                float need = size * 1.55f;
                if (h < need)
                {
                    bool top = align == TextAlignmentOptions.TopLeft || align == TextAlignmentOptions.TopRight || align == TextAlignmentOptions.Top;
                    if (!top) y -= (need - h) / 2f;
                    h = need;
                }
            }
            Place(t.rectTransform, x, y, w, h);
            t.fontSize = size; t.color = color; t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            try { t.margin = Vector4.zero; } catch { }
            try { t.enableWordWrapping = wrap; } catch { }
            try { t.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis; } catch { }
            try { t.characterSpacing = 0f; t.lineSpacing = 0f; } catch { }
            t.text = text ?? "";
            return t;
        }

        /// <summary>A focusable plate: the chamfered panel, a glow that shows while focused.</summary>
        static Ctl Plate(RectTransform parent, string key, float x, float y, float w, float h, Color tint)
        {
            var root = Place(Ui.NewRect(key, parent), x, y, w, h);
            var glow = Pic(root, "Glow", -34, -34, w + 68, h + 68, Art.Glow, new Color(1f, 0.93f, 0.7f, 0.8f), true);
            glow.gameObject.SetActive(false);
            var bg = Pic(root, "Bg", 0, 0, w, h, Art.Panel, tint, true);
            var c = new Ctl { Key = key, Rt = root, Bg = bg.GetComponent<Image>(), Glow = glow };
            _ctls.Add(c);
            return c;
        }

        static RectTransform Arrow(RectTransform parent, float x, float y, float size, bool right)
        {
            var rt = Pic(parent, right ? "Next" : "Prev", x, y, size, size, Art.Glyph("up"), Theme.Gold);
            rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = new Vector2(x + size / 2f, -(y + size / 2f));
            rt.localRotation = Quaternion.Euler(0, 0, right ? -90f : 90f);
            return rt;
        }

        /// <summary>A settings row: label on the left, a value between two arrows on the right; left / right (or a click on
        /// an arrow, or Submit) changes it.</summary>
        static Ctl Cycler(RectTransform parent, string key, float x, float y, float w, float h, string label, string glyph, Func<string> value, Action<int> change, Func<string> desc, float valueW = 1000f)
        {
            var c = Plate(parent, key, x, y, w, h, new Color(1f, 1f, 1f, 0.92f));
            float pad = 44f;
            if (glyph != null) { Pic(c.Rt, "Icon", pad, (h - 64f) / 2f, 64f, 64f, Art.Glyph(glyph), Theme.Gold); pad += 92f; }
            Text(c.Rt, "Label", pad, 0, w - valueW - pad - 20f, h, 46f, Theme.White, label, TextAlignmentOptions.Left, true);
            float vx = w - valueW - 30f;
            Box(c.Rt, "Well", vx, 16f, valueW, h - 32f, new Color(0f, 0f, 0f, 0.42f));
            c.Left = Arrow(c.Rt, vx + 14f, (h - 56f) / 2f, 56f, false);
            c.Right = Arrow(c.Rt, vx + valueW - 70f, (h - 56f) / 2f, 56f, true);
            var val = Text(c.Rt, "Value", vx + 80f, 0, valueW - 160f, h, 46f, Theme.GoldText, value(), TextAlignmentOptions.Center, true);
            c.Change = d => { change(d); if (val != null) val.text = value(); SetDesc(desc()); };
            c.Refresh = () => { if (val != null) val.text = value(); };
            c.Desc = desc;
            return c;
        }

        static Ctl Btn(RectTransform parent, string key, float x, float y, float w, float h, string label, string glyph, Action press, Func<string> desc)
        {
            var c = Plate(parent, key, x, y, w, h, new Color(1f, 1f, 1f, 0.95f));
            float tw = w - 60f, tx = 30f;
            if (glyph != null) { Pic(c.Rt, "Icon", 40f, (h - 60f) / 2f, 60f, 60f, Art.Glyph(glyph), Theme.Gold); tx = 120f; tw = w - 150f; }
            Text(c.Rt, "Label", tx, 0, tw, h, 46f, Theme.White, label, glyph != null ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, true);
            c.Press = press; c.Desc = desc;
            return c;
        }

        // ================================================================ clipped areas that follow the focus
        // Controls are placed on Content as anywhere else (Place), say which area they are in (Ctl.In), and the rest
        // follows: the focus moving onto a control that is not fully shown brings it into view, the mouse only works
        // a control where the area shows it, the wheel scrolls the area under the pointer, and a small gold arrow
        // outside the area says on which side there is more. Where an area stood outlives the rebuild of the body.
        static Scroller Scroll(string key, float x, float y, float w, float h, bool across, float inset)
        {
            var s = new Scroller { Key = key, Across = across, Inset = inset, Size = across ? w : h };
            s.View = Place(Ui.NewRect("View:" + key, _body), x - inset, y - inset, w + 2f * inset, h + 2f * inset);     // the inset keeps the focus glow of a control at the edge whole
            s.View.gameObject.AddComponent(Il2CppType.Of<RectMask2D>());
            s.Content = Place(Ui.NewRect("Content", s.View), inset, inset, w, h);
            s.Before = across ? Pointer(_body, x + w - 124f, y - 78f, 52f, 90f) : Pointer(_body, x + w + inset + 12f, y, 52f, 0f);
            s.After = across ? Pointer(_body, x + w - 56f, y - 78f, 52f, -90f) : Pointer(_body, x + w + inset + 12f, y + h - 52f, 52f, 180f);
            _scrolls.Add(s);
            return s;
        }

        static RectTransform Pointer(RectTransform parent, float x, float y, float size, float degrees)
        {
            var rt = Pic(parent, "More", x, y, size, size, Art.Glyph("up"), Theme.Gold);
            rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = new Vector2(x + size / 2f, -(y + size / 2f));
            rt.localRotation = Quaternion.Euler(0, 0, degrees);
            return rt;
        }

        /// <summary>The content is laid out: this is how long it is. Back to where the area stood before the rebuild.</summary>
        static void Settle(Scroller s, float extent)
        {
            s.Extent = extent; float pos; _scrollPos.TryGetValue(s.Key, out pos);
            ScrollTo(s, pos, false);
        }

        static void ScrollTo(Scroller s, float pos, bool glide)
        {
            pos = Mathf.Clamp(pos, 0f, s.Max); s.Pos = pos; _scrollPos[s.Key] = pos;
            try { s.Before.gameObject.SetActive(pos > 1f); s.After.gameObject.SetActive(pos < s.Max - 1f); } catch { }
            Fx.Cancel("menu.scroll");
            var content = s.Content; bool across = s.Across; float inset = s.Inset;
            float from = across ? inset - content.anchoredPosition.x : content.anchoredPosition.y + inset;      // where it is on screen: a glide cut short starts from there
            Action<float> put = p => { content.anchoredPosition = across ? new Vector2(inset - p, -inset) : new Vector2(inset, p - inset); };
            if (!glide || Mathf.Abs(from - pos) < 1f) { put(pos); return; }
            Fx.Run("menu.scroll", 0f, 0.16f, k => put(Mathf.Lerp(from, pos, Fx.OutCubic(k))));
        }

        /// <summary>Bring a control of a clipped area fully into view, with the headers that stand over it (Ctl.Lead).</summary>
        static void Reveal(Ctl c)
        {
            var s = c == null ? null : c.In;
            if (s == null || c.Rt == null) return;
            float start = s.Across ? c.Rt.anchoredPosition.x : -c.Rt.anchoredPosition.y, len = s.Across ? c.Rt.sizeDelta.x : c.Rt.sizeDelta.y;
            float pos = s.Pos;
            if (start + len > pos + s.Size) pos = start + len - s.Size;
            if (start - c.Lead < pos) pos = start - c.Lead;
            if (Mathf.Abs(pos - s.Pos) >= 1f) ScrollTo(s, pos, !_building);
        }

        static float Wheel() { try { return UnityEngine.Input.mouseScrollDelta.y; } catch (Exception e) { InputError("Input.mouseScrollDelta", e); return 0f; } }

        static void WheelScroll(float wheel)
        {
            var p = new Vector2(_lastMouse.x, _lastMouse.y);
            foreach (var s in _scrolls) if (s.Max > 0f && Hit(s.View, p)) { ScrollTo(s, s.Pos - wheel * 150f, true); return; }
        }

        // ================================================================ the shell: backdrop, header, tabs, footer
        static void BuildShell()
        {
            if (_go != null) UnityEngine.Object.Destroy(_go);
            _ctls.Clear(); _focus = null;
            _go = new GameObject("YazsMenu");
            UnityEngine.Object.DontDestroyOnLoad(_go);
            var canvas = _go.AddComponent(Il2CppType.Of<Canvas>()).TryCast<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scaler = _go.AddComponent(Il2CppType.Of<CanvasScaler>()).TryCast<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            _go.AddComponent(Il2CppType.Of<GraphicRaycaster>());
            _group = _go.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>();
            var root = _go.transform.TryCast<RectTransform>();

            // the blocker takes every click meant for the game's menu underneath
            var block = Ui.Image(root, "Blocker", new Color(0.02f, 0.018f, 0.016f, 0.9f)); Ui.Stretch(block, 0, 0, 0, 0);
            block.GetComponent<Image>().raycastTarget = true;
            var back = Ui.Image(root, "Backdrop", new Color(1f, 1f, 1f, 0.96f)); Ui.Stretch(back, 0, 0, 0, 0);
            var bimg = back.GetComponent<Image>(); var bs = Art.Backdrop;
            if (bs != null) { bimg.sprite = bs; bimg.type = Image.Type.Simple; bimg.preserveAspect = false; } else bimg.color = new Color(0.04f, 0.035f, 0.03f, 0.97f);

            _stage = Ui.NewRect("Stage", root);
            _stage.anchorMin = _stage.anchorMax = new Vector2(0.5f, 0.5f); _stage.pivot = new Vector2(0.5f, 0.5f);
            _stage.anchoredPosition = Vector2.zero; _stage.sizeDelta = new Vector2(W, H);

            // ---- header: the crest, the title, the tabs
            var crest = Pic(_stage, "Crest", 120, 36, 260, 260, Art.Emblem, Color.white);
            crest.pivot = new Vector2(0.5f, 0.5f); crest.anchoredPosition = new Vector2(250, -166);
            Text(_stage, "Title", 410, 66, 1500, 110, 96f, Theme.White, "<color=" + Theme.GoldHex + ">YAZS</color> COMPANION", TextAlignmentOptions.Left, true);
            Text(_stage, "Sub", 414, 182, 1500, 60, 42f, Theme.Grey, "v" + Plugin.VERSION + "   -   builds, advice and display", TextAlignmentOptions.Left);
            _tabBar = Place(Ui.NewRect("Tabs", _stage), 0, 0, W, H);
            BuildTabs();
            _saved = Text(_stage, "Saved", 2520f, 238f, 1200f, 60f, 38f, Theme.GoldText, "", TextAlignmentOptions.Right, true);
            if (_saved != null) _saved.alpha = 0f;
            var rule = Ui.FadeRule(_stage, "HeaderRule", Theme.GoldLine); Place(rule, 120, 312, W - 240, 4);
            rule.pivot = new Vector2(0f, 1f);
            Ui.Diamond(rule, "Tip", 0f, 0.5f, 26f, Theme.Gold);
            Fx.Draw("menu.rule", rule, 0.05f, 0.55f);

            // ---- footer: what the focused control does, and the keys
            var foot = Ui.FadeRule(_stage, "FootRule", Theme.GoldRule); Place(foot, 120, 1846, W - 240, 3);
            _desc = Text(_stage, "Desc", 140, 1872, 2780, 250, 44f, Theme.Cream, "", TextAlignmentOptions.TopLeft, false, true);
            _hints = Text(_stage, "Hints", 2960, 1872, 760, 250, 38f, Theme.Grey, "", TextAlignmentOptions.TopRight, false, true);

            _body = Ui.NewRect("Body", _stage); Place(_body, 0, 0, W, H);
            _bodyGroup = _body.gameObject.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>();
        }

        // Three tabs, where they always stood. While another mod has an option registered there is a fourth, MODS, and
        // the four are set closer so they still end at the right margin. Rebuilt when that changes under an open menu.
        static float TabX(int i) { return _tabCount > 3 ? 1930f + i * 450f : 2050f + i * 560f; }
        static float TabRuleWidth { get { return _tabCount > 3 ? 330f : 420f; } }

        static void BuildTabs()
        {
            if (_tabBar == null) return;
            Fx.Cancel("menu.tab");
            for (int i = _tabBar.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_tabBar.GetChild(i).gameObject);
            for (int i = 0; i < _tabLabels.Length; i++) _tabLabels[i] = null;
            bool mods = false; try { mods = Api.Extensions.HasOptions; _extSeen = Api.Extensions.Version; } catch { }
            _tabCount = mods ? 4 : 3;
            if (_tab >= _tabCount) { _tab = 0; _focusKey = ""; }
            for (int i = 0; i < _tabCount; i++)
            {
                Pic(_tabBar, "TabIcon" + i, TabX(i), 112f, 64f, 64f, Art.Glyph(TabGlyphs[i]), Theme.Gold);
                _tabLabels[i] = Text(_tabBar, "Tab" + i, TabX(i) + 84f, 96f, (mods ? 450f : 560f) - 120f, 96f, 60f, i == _tab ? Theme.GoldText : Theme.Grey, Tabs[i], TextAlignmentOptions.Left, true);
            }
            _tabRule = Box(_tabBar, "TabRule", TabX(_open ? _tab : 0), 204f, TabRuleWidth, 8f, Theme.Gold);      // a menu that opens slides it to its tab
        }

        static void Intro()
        {
            if (_group == null) return;
            Fx.Run("menu.fade", 0f, 0.22f, k => { _group.alpha = Fx.OutCubic(k); });
            var crest = _stage.Find("Crest"); var crt = crest == null ? null : crest.TryCast<RectTransform>();
            if (crt != null) Fx.Run("menu.crest", 0.05f, 0.5f, k => { float s = 0.6f + 0.4f * Fx.OutBack(k); crt.localScale = new Vector3(s, s, 1f); crt.localRotation = Quaternion.Euler(0, 0, 40f * (1f - Fx.OutCubic(k))); });
        }

        static void Hints()
        {
            bool pad = false; try { pad = GameMaster.Get != null && GameMaster.Get.UsesPad; } catch { }
            _hints.text = pad
                ? "<b>A</b> select    <b>B</b> " + (_editing ? "done" : "close") + "\n<b>LB / RB</b> tab\n<b>LEFT / RIGHT</b> change"
                : "<b>ENTER</b> select    <b>ESC</b> " + (_editing ? "done" : "close") + "\n<b>Q / E</b> tab\n<b>LEFT / RIGHT</b> change";
        }

        // ================================================================ the body, rebuilt whenever the state changes
        static void BuildBody()
        {
            Fx.Cancel("menu.glow"); Fx.Cancel("menu.body");
            if (Panel.Previewing) Panel.PreviewEnd();           // the readout in the DISPLAY tab's window goes with the body
            _pvWindow = null; _pvCaption = null;
            for (int i = _body.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_body.GetChild(i).gameObject);
            _ctls.Clear(); _scrolls.Clear(); _focus = null; Fx.Cancel("menu.scroll");
            if (_tab >= _tabCount) _tab = 0;
            for (int i = 0; i < _tabCount; i++) if (_tabLabels[i] != null) _tabLabels[i].color = i == _tab ? Theme.GoldText : Theme.Grey;
            if (_tabRule != null) { var tabRule = _tabRule; float to = TabX(_tab), from = tabRule.anchoredPosition.x; Fx.Cancel("menu.tab"); Fx.Run("menu.tab", 0f, 0.22f, k => { tabRule.anchoredPosition = new Vector2(Mathf.Lerp(from, to, Fx.OutCubic(k)), -204f); }); }

            if (_tab == 0) { if (_editing) BuildEditor(); else BuildBuilds(); }
            else if (_tab == 1) BuildAdvice();
            else if (_tab == 2) BuildDisplay();
            else BuildMods();
            Hints();

            Ctl want = _ctls.FirstOrDefault(c => c.Key == _focusKey) ?? _ctls.FirstOrDefault();
            _building = true;           // an area that has to scroll to the control in focus is simply there, it does not travel
            try { if (want != null) Focus(want, false); } finally { _building = false; }
            if (_bodyGroup != null) Fx.Run("menu.body", 0f, 0.2f, k => { _bodyGroup.alpha = Fx.OutCubic(k); _body.anchoredPosition = new Vector2(0f, -18f * (1f - Fx.OutCubic(k))); });
        }

        // ---------------------------------------------------------------- BUILDS
        static string StyleText(BuildStyle s) { return s == BuildStyle.Weapon ? "Weapon first" : s == BuildStyle.Ability ? "Abilities first" : "Balanced"; }
        static string Short(string evolution) { int i = evolution == null ? -1 : evolution.IndexOf(':'); return i >= 0 ? evolution.Substring(i + 1).Trim() : evolution ?? ""; }

        static void BuildBuilds()
        {
            Builds.ForgetPacks();       // builds other mods lend: what is listed is what their providers say as this is drawn
            // ---- the nine survivors
            for (int i = 0; i < Builds.Survivors.Length; i++)
            {
                string sv = Builds.Survivors[i]; int index = i;
                bool current = i == _survivor;
                var c = Plate(_body, "sv:" + i, 120, 352 + i * 162, 760, 150, current ? new Color(1f, 0.95f, 0.8f, 1f) : new Color(1f, 1f, 1f, 0.78f));
                Sprite p; _portraits.TryGetValue(sv, out p);
                if (p != null) Portrait(c.Rt, 22, 12, 126, p, unlocked0(sv));
                else Pic(c.Rt, "Portrait", 40, 30, 90, 90, Art.Glyph("diamond"), Theme.Gold);
                bool unlocked; if (!_unlocked.TryGetValue(sv, out unlocked)) unlocked = true;
                Text(c.Rt, "Name", 170, 16, 570, 70, 56f, unlocked ? Theme.White : Theme.Grey, sv.ToUpperInvariant(), TextAlignmentOptions.Left, true);
                var b = Builds.For(sv); bool lent = b != null && Builds.OnAuto(sv);        // on Auto, and a build pack of another mod says what Auto follows
                Text(c.Rt, "Build", 172, 84, 570, 54, 40f, b != null ? Theme.GoldText : Theme.Grey, b == null ? "Auto" : lent ? "Auto: " + b.Name : b.Name, TextAlignmentOptions.Left);
                c.Desc = () =>
                {
                    var f = Builds.For(sv);
                    return sv + (unlocked ? "" : " (not unlocked yet)") + ": " + (f == null ? "on Auto - the advice reads your squad. "
                        : Builds.OnAuto(sv) ? "on Auto, which follows " + f.Name + " while " + f.Pack + " lends its builds. " : "following " + f.Name + ". ") + "Move right to choose a build.";
                };
                c.Focused = () => { if (_survivor != index) { _survivor = index; _dirty = true; } };
                c.Press = () => Move(1, 0);
            }

            // ---- the builds of the survivor in focus
            string who = Builds.Survivors[_survivor];
            var choices = Builds.ChoicesOf(who);
            // a selection that points into a build pack which is not there right now reads as Auto; and Auto follows the
            // default of a pack that is there, if it names one
            bool onAuto = Builds.OnAuto(who); string selected = onAuto ? Builds.AutoId : Builds.SelectedId(who);
            var viaAuto = onAuto ? Builds.AutoOf(who) : null;
            int n = choices.Count + 1; float gap = 26f, area = 2760f, cw = Mathf.Min(700f, (area - (n - 1) * gap) / n), x0 = 940f, y0 = 352f, ch = 1230f;
            var lenders = choices.Where(b => b.Pack.Length > 0).Select(b => b.Pack).Distinct().ToList();
            Text(_body, "Who", x0, y0 - 2f, 2000, 70, 50f, Theme.Grey, "<color=" + Theme.GoldHex + ">" + who.ToUpperInvariant() + "</color>   the build the advice follows"
                + (lenders.Count > 0 ? "   <color=" + Theme.DimHex + ">+ builds from " + string.Join(", ", lenders) + "</color>" : ""), TextAlignmentOptions.Left, true);
            y0 += 84f;
            // Auto, the presets and your own build fit side by side (five cards at most). With builds lent by other mods
            // there can be more: the cards keep a readable width and the row scrolls with the focus, the next card peeking in
            Scroller row = null;
            if (cw < 531f) { cw = 560f; row = Scroll("builds:" + who, x0, y0, area, ch, true, 40f); }
            for (int i = 0; i < n; i++)
            {
                Build b = i == 0 ? null : choices[i - 1];
                string id = b == null ? Builds.AutoId : b.Id;
                bool active = string.Equals(id, selected, StringComparison.OrdinalIgnoreCase);
                bool followed = active || (viaAuto != null && b != null && string.Equals(b.Id, viaAuto.Id, StringComparison.OrdinalIgnoreCase));
                var c = Plate(row != null ? row.Content : _body, "build:" + id, (row != null ? 0f : x0) + i * (cw + gap), row != null ? 0f : y0, cw, ch, followed ? new Color(1f, 0.93f, 0.74f, 1f) : new Color(1f, 1f, 1f, 0.82f));
                c.In = row;
                BuildCard(c, who, b, cw, active, viaAuto);
                c.Press = () => { Builds.Select(who, id); SettingSaved(Builds.LastSaveOk); _focusKey = "build:" + id; _dirty = true; Plugin.Logger.LogInfo("[menu] " + who + " follows " + (b == null ? "Auto" : b.Name) + (Builds.LastSaveOk ? " (saved)" : " (builds.json was NOT written)")); };
            }
            if (row != null) Settle(row, n * (cw + gap) - gap);

            // ---- your own build
            var custom = Builds.CustomOf(who); float by = y0 + ch + 30f;
            Btn(_body, "customize", x0, by, 760, 116, custom == null ? "MAKE MY OWN BUILD" : "EDIT MY BUILD", "gear", () =>
            {
                var mine = Builds.EnsureCustom(who); Builds.Select(who, mine.Id); SettingSaved(Builds.LastSaveOk);
                _editing = true; _focusKey = "ed:style"; _dirty = true;
            }, () => custom == null
                ? "Start your own " + who + " build from the one selected: change the level-up style, the weapon branch, the order of the abilities, the evolution of each, and what the build wants from items."
                : "Edit your own " + who + " build: style, weapon branch, ability order, evolutions, item leanings.");
            if (custom != null)
                Btn(_body, "delete", x0 + 790, by, 620, 116, "DELETE MY BUILD", null, () => { Builds.DropCustom(who); SettingSaved(Builds.LastSaveOk); _focusKey = "customize"; _dirty = true; },
                    () => "Remove your own " + who + " build. The presets stay; the survivor goes back to Auto if it was following it.");
        }

        static bool unlocked0(string sv) { bool u; return !_unlocked.TryGetValue(sv, out u) || u; }

        // the portraits are full-body renders: show the head and shoulders through a masked window
        static void Portrait(RectTransform parent, float x, float y, float size, Sprite sprite, bool unlocked)
        {
            var frame = Box(parent, "PortraitFrame", x, y, size, size, new Color(0f, 0f, 0f, 0.55f));
            frame.gameObject.AddComponent(Il2CppType.Of<RectMask2D>());
            float aspect = sprite.rect.height > 0 ? sprite.rect.width / sprite.rect.height : 1f;
            bool tall = aspect < 0.9f;                                  // a full-body render: look at the head and shoulders
            float zoom = tall ? 2.6f : 1.12f, h = size * zoom, w = h * aspect;
            var img = Box(frame, "Portrait", (size - w) / 2f, tall ? -size * 0.04f : (size - h) / 2f, w, h, unlocked ? Color.white : new Color(0.45f, 0.45f, 0.45f, 1f));
            var im = img.GetComponent<Image>(); im.sprite = sprite; im.type = Image.Type.Simple; im.preserveAspect = true;
            Ui.Frame(frame, "Edge", 0, 3, Theme.GoldRule, 0);
        }

        // viaAuto: the survivor is on Auto and a build pack of another mod names this build as what Auto follows
        static void BuildCard(Ctl c, string who, Build b, float cw, bool active, Build viaAuto = null)
        {
            bool lentAuto = viaAuto != null && b != null && string.Equals(b.Id, viaAuto.Id, StringComparison.OrdinalIgnoreCase);
            var rt = c.Rt; float pad = 30f, inner = cw - 2 * pad;
            Pic(rt, "Glyph", pad, 30, 116, 116, Art.Glyph(b == null ? "chevrons" : string.IsNullOrEmpty(b.Glyph) ? "diamond" : b.Glyph), Theme.Gold);
            Text(rt, "Name", pad + 136, 30, inner - 136, 70, 54f, Theme.White, b == null ? "Auto" : b.Name, TextAlignmentOptions.Left, true);
            string tag = b == null ? (viaAuto != null ? "NOW: " + viaAuto.Name.ToUpperInvariant() : "READS YOUR SQUAD") : b.Custom ? "YOUR BUILD" : b.Pack.Length > 0 ? b.Pack.ToUpperInvariant() : b.Source == "guides" ? "GUIDE PICK" : "ALTERNATIVE";
            Text(rt, "Tag", pad + 138, 100, inner - 136, 48, 34f, b != null && b.Source == "guides" ? Theme.GoldText : Theme.Grey, tag, TextAlignmentOptions.Left, true);
            if (active || lentAuto)
            {
                var rib = Box(rt, "Active", cw - 250, -18, 232, 50, Theme.Gold);
                Text(rib, "T", 0, 0, 232, 50, 32f, new Color(0.08f, 0.06f, 0.03f, 1f), active ? "FOLLOWING" : "VIA AUTO", TextAlignmentOptions.Center, true);
            }
            Box(rt, "Rule", pad, 172, inner, 3, Theme.GoldRule);

            Text(rt, "L1", pad, 190, inner, 40, 30f, Theme.Grey, "LEVEL-UPS", TextAlignmentOptions.Left, true);
            Text(rt, "Style", pad, 226, inner, 56, 44f, Theme.White, StyleText(b == null ? Doctrine.Current.Style : b.Style) + (b == null ? " (ADVICE tab)" : ""), TextAlignmentOptions.Left);

            Text(rt, "L2", pad, 306, inner, 40, 30f, Theme.Grey, "WEAPON BRANCH", TextAlignmentOptions.Left, true);
            string branch = b == null ? "" : b.Branch;
            if (!string.IsNullOrEmpty(branch))
            {
                Sprite icon; _icons.TryGetValue(branch, out icon);
                if (icon != null) Pic(rt, "WIcon", pad, 348, 92, 92, icon, Color.white);
                Text(rt, "Branch", pad + (icon != null ? 108 : 0), 362, inner - 108, 60, 44f, Theme.White, branch, TextAlignmentOptions.Left);
            }
            else Text(rt, "Branch", pad, 350, inner, 110, 36f, Theme.Cream, "decided live: what the squad deals, then your Training Yard, then the guides", TextAlignmentOptions.TopLeft, false, true);

            Text(rt, "L3", pad, 466, inner, 40, 30f, Theme.Grey, "ABILITIES, IN ORDER", TextAlignmentOptions.Left, true);
            var kit = Builds.KitOf(who);
            var order = new List<string>();
            if (b != null) order.AddRange(b.Abilities);
            if (kit != null) foreach (var a in kit.Abilities) if (!order.Contains(a[0], StringComparer.OrdinalIgnoreCase) && (b == null || !b.Skips(a[0]))) order.Add(a[0]);
            if (b == null && kit != null)
            {
                // Auto: the guides' tiers, as the ranking uses them
                order = kit.Abilities.Select(a => a[0]).OrderBy(a => { string t; return Knowledge.Current.AbilityTier.TryGetValue(a, out t) ? "SABC".IndexOf(t.ToUpperInvariant()[0]) : 2; }).ToList();
            }
            float ry = 512f;
            for (int i = 0; i < order.Count && i < 4; i++)
            {
                string a = order[i]; Sprite icon; _icons.TryGetValue(a, out icon);
                if (icon != null) Pic(rt, "AIcon" + i, pad, ry + 6, 100, 100, icon, Color.white);
                string evo = b == null ? null : b.EvolutionOf(a);
                string tier = null; if (b == null) Knowledge.Current.AbilityTier.TryGetValue(a, out tier);
                Text(rt, "AName" + i, pad + 118, ry + (evo != null || tier != null ? 8 : 28), inner - 118, 56, 42f, Theme.White, (b != null ? "<color=" + Theme.GoldHex + ">" + (i + 1) + "</color>  " : "") + a, TextAlignmentOptions.Left);
                if (evo != null) Text(rt, "AEvo" + i, pad + 118, ry + 60, inner - 118, 46, 34f, Theme.GoldText, "evolve: " + Short(evo), TextAlignmentOptions.Left);
                else if (tier != null) Text(rt, "AEvo" + i, pad + 118, ry + 60, inner - 118, 46, 34f, Theme.Grey, tier.ToUpperInvariant() + " tier in the guides", TextAlignmentOptions.Left);
                ry += 122f;
            }
            if (b != null && b.Skip.Count > 0) Text(rt, "Skip", pad, ry + 6, inner, 46, 34f, Theme.Rust, "skips " + string.Join(", ", b.Skip), TextAlignmentOptions.Left);
            if (b != null && b.Wants.Count > 0) Text(rt, "Wants", pad, 1070, inner, 140, 34f, Theme.Grey, "<color=" + Theme.DimHex + ">ITEMS  </color>" + string.Join(" · ", b.Wants.Take(5)).ToLowerInvariant(), TextAlignmentOptions.TopLeft, false, true);

            c.Desc = () => b == null
                ? (viaAuto != null ? "AUTO - while " + viaAuto.Pack + " lends its builds, Auto follows its " + viaAuto.Name + "; select any card to follow that instead. Otherwise: no" : "AUTO. No")
                    + " fixed build: the weapon branch and the evolutions follow what your squad deals right now (shared damage types, tag specials within reach, team passives), then your Training Yard investment, then the guides. Level-ups use the style set on the ADVICE tab."
                : (b.Pack.Length > 0 ? "[" + b.Pack + "]  " : "") + b.Summary + (active ? "" : lentAuto ? "   [followed through Auto]" : "   [select to follow it]");
        }

        // ---------------------------------------------------------------- the editor of your own build
        static void BuildEditor()
        {
            string who = Builds.Survivors[_survivor];
            var b = Builds.EnsureCustom(who); var kit = Builds.KitOf(who);
            float x = 520f, w = 2800f, y = 352f, rh = 118f, step = 132f;
            Text(_body, "Who", x, y - 2f, w, 70, 50f, Theme.Grey, "<color=" + Theme.GoldHex + ">" + who.ToUpperInvariant() + "</color>   your own build", TextAlignmentOptions.Left, true);
            y += 84f;
            Action changed = () => { Builds.Save(); SettingSaved(Builds.LastSaveOk); };

            var styles = new[] { BuildStyle.Weapon, BuildStyle.Balanced, BuildStyle.Ability };
            Cycler(_body, "ed:style", x, y, w, rh, "Level-up style", "chevrons", () => StyleText(b.Style),
                d => { b.Style = styles[(Array.IndexOf(styles, b.Style) + d + styles.Length) % styles.Length]; changed(); },
                () => b.Style == BuildStyle.Weapon ? "WEAPON FIRST: every weapon level comes before any ability level. Evolutions, a recruit's first weapon and the next weapon tier are always on top."
                    : b.Style == BuildStyle.Ability ? "ABILITIES FIRST: the abilities in your order come before weapon levels; the weapon fills in. For support and ability-damage builds."
                    : "BALANCED: each ability once early, then the weapon and your #1 ability side by side, the other abilities after."); y += step;

            var branches = new List<string> { "" }; if (kit != null) { branches.Add(kit.BranchA); branches.Add(kit.BranchB); }
            Cycler(_body, "ed:branch", x, y, w, rh, "Weapon branch", "crosshair", () => string.IsNullOrEmpty(b.Branch) ? "Decided live" : b.Branch,
                d => { int i = Math.Max(0, branches.FindIndex(s => string.Equals(s, b.Branch, StringComparison.OrdinalIgnoreCase))); b.Branch = branches[(i + d + branches.Count) % branches.Count]; changed(); },
                () => string.IsNullOrEmpty(b.Branch) ? "The tier-2 weapon is decided live: the branch that shares damage types with the rest of your squad, then your Training Yard investment, then the guides."
                    : "Always go " + b.Branch + ". The two tier-2 weapons exclude each other; the other branch will be ranked low when it is offered." + (kit != null ? "  Final weapon: " + kit.Line[4] + "." : "")); y += step;

            // the four abilities: a rank (or SKIP) and an evolution each
            if (kit != null)
                foreach (var ab in kit.Abilities)
                {
                    string a = ab[0]; var evos = new List<string> { "", ab[1], ab[2] };
                    var row = Place(Ui.NewRect("Row:" + a, _body), x, y, w, rh);
                    Sprite icon; _icons.TryGetValue(a, out icon);
                    if (icon != null) Pic(row, "Icon", 20, 9, 100, 100, icon, Color.white);
                    Text(row, "Name", 140, 0, 800, rh, 48f, Theme.White, a, TextAlignmentOptions.Left, true);
                    Cycler(_body, "ed:rank:" + a, x + 960, y, 640, rh, "", null, () => b.Skips(a) ? "SKIP" : "#" + (b.PriorityOf(a) + 1),
                        d => { Rerank(b, a, d); changed(); _focusKey = "ed:rank:" + a; _dirty = true; },
                        () => b.Skips(a) ? a + " is skipped: the advice ranks it under everything else for this build." : a + " is #" + (b.PriorityOf(a) + 1) + " in your order. #1 is the ability to focus; new abilities are suggested in this order.", 560f);
                    Cycler(_body, "ed:evo:" + a, x + 1630, y, 1170, rh, "", null, () => string.IsNullOrEmpty(b.EvolutionOf(a)) ? "Evolution: live" : Short(b.EvolutionOf(a)),
                        d => { int i = Math.Max(0, evos.FindIndex(s => string.Equals(s, b.EvolutionOf(a) ?? "", StringComparison.OrdinalIgnoreCase))); string pick = evos[(i + d + evos.Count) % evos.Count]; if (pick.Length == 0) b.Evolution.Remove(a); else b.Evolution[a] = pick; changed(); },
                        () => string.IsNullOrEmpty(b.EvolutionOf(a)) ? "The evolution of " + a + " is decided live: the one that shares a damage type with what your squad deals, or carries a tag a team passive boosts (" + Short(ab[1]) + " or " + Short(ab[2]) + ")."
                            : "Always evolve " + a + " into " + Short(b.EvolutionOf(a)) + ".", 1090f);
                    y += step;
                }

            // what the build wants from items
            string[] leanings = { "weapons", "abilities", "critical", "armor", "healing", "slow", "turret", "melee" };
            Text(_body, "WantsLabel", x + 40, y, 600, rh, 48f, Theme.White, "Items it wants", TextAlignmentOptions.Left, true);
            float cx = x + 640f, chipW = (w - 640f - 7 * 14f) / 8f;
            foreach (var lean in leanings)
            {
                string l = lean; bool on = b.Wants.Contains(l, StringComparer.OrdinalIgnoreCase);
                var chip = Plate(_body, "ed:want:" + l, cx, y + 6, chipW, rh - 12, on ? new Color(1f, 0.9f, 0.62f, 1f) : new Color(1f, 1f, 1f, 0.6f));
                Text(chip.Rt, "T", 0, 0, chipW, rh - 12, 38f, on ? Theme.GoldText : Theme.Grey, l.ToUpperInvariant(), TextAlignmentOptions.Center, true);
                chip.Press = () => { if (on) b.Wants.RemoveAll(s => string.Equals(s, l, StringComparison.OrdinalIgnoreCase)); else b.Wants.Add(l); changed(); _focusKey = "ed:want:" + l; _dirty = true; };
                chip.Desc = () => (on ? "ON: " : "OFF: ") + "items and military stats about " + l + " get a bonus while this build is on the squad. Damage types follow the weapon branch on their own.";
                cx += chipW + 14f;
            }
            y += step + 10f;

            var presets = Builds.PresetsOf(who);
            if (presets.Count > 0)
            {
                _copyFrom = Mathf.Clamp(_copyFrom, 0, presets.Count - 1);
                var copy = Cycler(_body, "ed:copy", x, y, 1960, rh, "Start over from", "link", () => presets[_copyFrom].Name,
                    d => { _copyFrom = (_copyFrom + d + presets.Count) % presets.Count; },
                    () => "Press to replace your build with a copy of " + presets[_copyFrom].Name + " (left / right picks another preset).", 900f);
                copy.Press = () =>
                {
                    var from = presets[_copyFrom].Clone(); from.Id = b.Id; from.Custom = true; from.Source = ""; from.Name = b.Name; from.Survivor = who;
                    from.Summary = "Your own build, started from " + presets[_copyFrom].Name + ".";
                    foreach (var ab in kit.Abilities) if (from.PriorityOf(ab[0]) < 0 && !from.Skips(ab[0])) from.Abilities.Add(ab[0]);
                    Builds.ReplaceCustom(who, from); SettingSaved(Builds.LastSaveOk); _focusKey = "ed:copy"; _dirty = true;
                };
            }
            Btn(_body, "ed:done", x + 1990, y, 810, rh, "DONE", null, () => { _editing = false; _focusKey = "customize"; _dirty = true; }, () => "Back to the builds. Your build is saved as you change it.");
        }

        // move an ability up or down the order; past the last place it becomes SKIP, and back
        static void Rerank(Build b, string a, int d)
        {
            if (b.Skips(a)) { b.Skip.RemoveAll(s => string.Equals(s, a, StringComparison.OrdinalIgnoreCase)); if (d < 0) b.Abilities.Add(a); else b.Abilities.Insert(0, a); return; }
            int i = b.PriorityOf(a); if (i < 0) { b.Abilities.Add(a); return; }
            int to = i + d;
            b.Abilities.RemoveAt(i);
            if (to < 0 || to > b.Abilities.Count) { b.Skip.Add(a); return; }
            b.Abilities.Insert(to, a);
        }

        // ---------------------------------------------------------------- ADVICE and DISPLAY
        static T Next<T>(T value, int d) where T : struct
        {
            var all = (T[])Enum.GetValues(typeof(T)); int i = Array.IndexOf(all, value);
            return all[(i + d + all.Length) % all.Length];
        }
        static string Words(string pascal)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var ch in pascal) { if (char.IsUpper(ch) && sb.Length > 0) sb.Append(' '); sb.Append(sb.Length == 0 ? ch : char.ToLowerInvariant(ch)); }
            return sb.ToString();
        }

        static void BuildAdvice()
        {
            float x = 320f, w = 3200f, y = 360f, rh = 128f, step = 148f;
            Text(_body, "Lead", x, y, w, 60, 44f, Theme.Grey, "The standing orders of the advice, saved as you change them and followed from the next offer on. They apply to every survivor; a build you select on the BUILDS tab brings its own level-up style.", TextAlignmentOptions.Left);
            y += 96f;
            Cycler(_body, "ad:style", x, y, w, rh, "Level-up style (survivors on Auto)", "chevrons", () => Words(Plugin.AdviceStyle.Value.ToString()), d => Plugin.AdviceStyle.Value = Next(Plugin.AdviceStyle.Value, d),
                () => Plugin.AdviceStyle.Value == LevelUpStyle.WeaponFirst ? "WEAPON FIRST: every weapon level before any ability level - the rule of the mod up to 0.9. Some guides swear by it; others, and the ability-centred survivors, do not."
                    : Plugin.AdviceStyle.Value == LevelUpStyle.AbilitiesFirst ? "ABILITIES FIRST: abilities before weapon levels; the weapon fills in. Evolutions, a recruit's first weapon and new weapon tiers stay on top."
                    : "BALANCED (default): each ability once early, then the weapon and the focus ability side by side, the rest after. The human guides disagree on 'weapon first', so this sits between them."); y += step;
            Cycler(_body, "ad:timing", x, y, w, rh, "Run clock", "clock", () => Words(Plugin.AdviceTiming.Value.ToString()), d => Plugin.AdviceTiming.Value = Next(Plugin.AdviceTiming.Value, d),
                () => "What pays back over the rest of the run - XP, luck, pickup range, a fresh ability, a recruit - is worth the most early and little near the end; what works at once (a weapon tier, the level that completes an ability, a tag special within reach) keeps its value. " + (Plugin.AdviceTiming.Value == Strength.Off ? "OFF: the clock is ignored." : Plugin.AdviceTiming.Value == Strength.Strong ? "STRONG: the late-run cut-off is sharper." : "")); y += step;
            Cycler(_body, "ad:mode", x, y, w, rh, "Game mode and difficulty", "skull", () => Plugin.AdviceModeAware.Value ? "Steer the advice" : "Ignore", d => Plugin.AdviceModeAware.Value = !Plugin.AdviceModeAware.Value,
                () => "The mode sets the horizon (Default 20:00, Hardcore and Boss Rush 10:00, One Hit 5:00, Extermination by waves, Endurance and Infinite open-ended: economy never fades), doubles boss damage in Boss Rush, drops health picks and raises crowd control in One Hit, and weighs survival more in Hardcore and on higher difficulties."); y += step;
            Cycler(_body, "ad:synergy", x, y, w, rh, "Squad synergy", "link", () => Words(Plugin.AdviceSynergy.Value.ToString()), d => Plugin.AdviceSynergy.Value = Next(Plugin.AdviceSynergy.Value, d),
                () => "Read live: every level adds a tag point to each damage type it deals (+2% for everything dealing it, a special effect at 10), so picks that share a type with your squad are worth more; team passives boost grenades, turrets, taunts and deployables across the squad; evolutions are chosen by what they add for THIS squad."); y += step;
            Cycler(_body, "ad:tags", x, y, w, rh, "Damage type tags", "hash", () => Plugin.AdviceTags.Value == TagStrategy.Auto ? "Auto: stack the main type" : Plugin.AdviceTags.Value == TagStrategy.Spread ? "Spread" : "Always " + Plugin.AdviceTags.Value, d => Plugin.AdviceTags.Value = Next(Plugin.AdviceTags.Value, d),
                () => "AUTO stacks the type your squad deals most (the guides: one main type, maybe a second, avoid totals under 10). SPREAD gives no stacking bonus. Or name one type to always favour on Research Pods, items and level-ups."); y += step;
            Cycler(_body, "ad:goal", x, y, w, rh, "What the run is for", "coin", () => Words(Plugin.AdviceGoal.Value.ToString()), d => Plugin.AdviceGoal.Value = Next(Plugin.AdviceGoal.Value, d),
                () => "WIN THE RUN: cash never helps the run itself, so cash bonuses rank low and XP fades with the clock. FARM PROGRESS: cash, XP and luck keep their value to the end, and a recruit close to a new rank counts for more."); y += step;
            Cycler(_body, "ad:caution", x, y, w, rh, "Caution", "heart", () => Words(Plugin.AdviceCaution.Value.ToString()), d => Plugin.AdviceCaution.Value = Next(Plugin.AdviceCaution.Value, d),
                () => "How much max health, armor, regeneration and healing weigh. They already weigh more late, on higher difficulties and while the squad is hurting; this scales all of that."); y += step;
            Cycler(_body, "ad:recruit", x, y, w, rh, "SOS signals late in a run", "radio", () => Words(Plugin.AdviceRecruit.Value.ToString()), d => Plugin.AdviceRecruit.Value = Next(Plugin.AdviceRecruit.Value, d),
                () => "BY THE CLOCK: recruit while a newcomer still has the level-ups to grow (a recruit also adds +20% XP), Liberate for the level-up and cash once they do not. Or always recruit, or Liberate from the halfway mark.");
        }

        // the steps of "Readout size": 70 % .. 200 % of the automatic size
        static float SizeStep(float value, int d) { return Mathf.Clamp(Mathf.Round((value + 0.1f * d) * 10f) / 10f, 0.7f, 2f); }

        static void BuildDisplay()
        {
            // the settings on the left; on the right the PLAN readout itself, as it will look in play with the settings as
            // they stand - same canvas geometry as the HUD, so the same pixels
            float x = 120f, w = 2060f, y = 352f, rh = 124f, step = 142f, vw = 800f;
            Text(_body, "Lead", x, y, 3600f, 60, 44f, Theme.Grey, "What the mod draws. Every change is saved as you make it and applies at once; the preview is the PLAN readout at its real size.", TextAlignmentOptions.Left);
            y += 92f;
            float top = y;
            Func<bool, string> onOff = v => v ? "On" : "Off";
            Action redraw = () => { _pvDirty = true; };
            Cycler(_body, "di:badges", x, y, w, rh, "Card verdicts", "diamond", () => onOff(Plugin.ShowBadges.Value), d => Plugin.ShowBadges.Value = !Plugin.ShowBadges.Value,
                () => "Frame the recommended card, hang the RECOMMENDED ribbon under it and print a reason under every offered card.", vw); y += step;
            Cycler(_body, "di:panel", x, y, w, rh, "PLAN readout during play", "eye", () => onOff(Plugin.ShowPanel.Value), d => { Plugin.ShowPanel.Value = !Plugin.ShowPanel.Value; redraw(); },
                () => "The see-through readout of what to pick next: a row per survivor, then TAGS / SOS / GRAB.", vw); y += step;
            Cycler(_body, "di:size", x, y, w, rh, "Readout size", null, () => Mathf.RoundToInt(Plugin.PanelSize.Value * 100f) + "%" + (Mathf.Abs(Plugin.PanelSize.Value - 1f) < 0.01f ? "  (automatic)" : ""),
                d => { Plugin.PanelSize.Value = SizeStep(Plugin.PanelSize.Value, d); redraw(); },
                () => "How large the readout is drawn. 100% is the automatic size, which follows the screen: its text is 1.9% of the screen's height and never under 15 pixels, whatever the resolution or aspect. Raise it if the advice is hard to read from where you sit, lower it to see more of the field. The preview shows the real size.", vw); y += step;
            Cycler(_body, "di:detail", x, y, w, rh, "Readout detail", null, () => Plugin.PanelDetail.Value.ToString(), d => { Plugin.PanelDetail.Value = Next(Plugin.PanelDetail.Value, d); redraw(); },
                () => "COMPACT: one row per survivor with only what to pick next. FULL: two rows per survivor with the next steps and the evolution names.", vw); y += step;
            Cycler(_body, "di:place", x, y, w, rh, "Readout position", null, () => Words(Plugin.PanelPosition.Value.ToString()), d => { Plugin.PanelPosition.Value = Next(Plugin.PanelPosition.Value, d); redraw(); },
                () => "BOTTOM LEFT: the empty corner under the weapon and ability icons. RIGHT: the right edge between the item icons and the minimap.", vw); y += step;
            Cycler(_body, "di:opacity", x, y, w, rh, "Readout backing", null, () => Mathf.RoundToInt(Plugin.PanelOpacity.Value * 100f) + "%", d => { Plugin.PanelOpacity.Value = Mathf.Round(Mathf.Clamp01(Plugin.PanelOpacity.Value + 0.1f * d) * 10f) / 10f; redraw(); },
                () => "Darkness of the soft backing under the readout text. Lower shows more of the field; higher reads better over bright effects.", vw); y += step;
            Cycler(_body, "di:idle", x, y, w, rh, "Readout when nothing changed", null, () => Mathf.RoundToInt(Plugin.PanelIdle.Value * 100f) + "%", d => { Plugin.PanelIdle.Value = Mathf.Round(Mathf.Clamp(Plugin.PanelIdle.Value + 0.1f * d, 0.2f, 1f) * 10f) / 10f; Panel.PreviewDoze(); },
                () => "The readout is at full strength right after a pick, then settles to this opacity. 100% = it never dims. The preview settles to it now.", vw); y += step;
            Cycler(_body, "di:yard", x, y, w, rh, "Training Yard advice", "up", () => onOff(Plugin.ShowYard.Value), d => Plugin.ShowYard.Value = !Plugin.ShowYard.Value,
                () => "Number the nodes worth buying with the points on hand and print the SPEND / THEN / WHY strip. It follows the build you selected for that survivor.", vw); y += step;
            Cycler(_body, "di:motion", x, y, w, rh, "Motion", null, () => onOff(Plugin.Motion.Value), d => { Plugin.Motion.Value = !Plugin.Motion.Value; redraw(); },
                () => "Animate what the mod draws. During play nothing loops; the flair is kept for menus like this one.", vw); y += step;

            // ---- the preview window: a stand-in for the field (dark ground, a few bright effects to judge the backing
            //      against), the readout in its corner, a caption with the size in pixels
            float wx = x + w + 60f, ww = W - 120f - wx, wh = y - step + rh - top - 96f;
            var window = Box(_body, "PreviewWindow", wx, top, ww, wh, new Color(0.10f, 0.11f, 0.10f, 1f));
            window.gameObject.AddComponent(Il2CppType.Of<RectMask2D>());
            var field = Art.Field;
            if (field != null) { var fi = Box(window, "Field", 0, 0, ww, wh, Color.white).GetComponent<Image>(); fi.sprite = field; fi.type = Image.Type.Simple; fi.preserveAspect = false; }
            var host = Place(Ui.NewRect("Readout", window), 0, 0, ww, wh);       // the readout anchors to this rect's corners as it does to the screen's
            Ui.Frame(Place(Ui.NewRect("Edge", _body), wx, top, ww, wh), "Line", 0, 3, Theme.GoldRule, 18f);
            _pvCaption = Text(_body, "PreviewCaption", wx, top + wh + 18f, ww, 96f, 42f, Theme.Grey, "", TextAlignmentOptions.TopLeft, false, true);
            _pvWindow = host; _pvDirty = true;
        }

        // ---------------------------------------------------------------- MODS: the options other mods registered (Api\Extensions.cs)
        // A header per mod, a sub-header per group, and under them the same rows as on the other tabs: the value comes
        // from the mod's get(), left / right hands the next index to its set(), and the row reads get() again. The
        // callbacks are another mod's code: Api.Extensions catches what they throw. A list longer than the tab scrolls
        // with the focus (and the wheel).
        static void BuildMods()
        {
            var options = Api.Extensions.Options();         // a copy: registrations come and go on their own time
            float x = 320f, w = 3200f, y0 = 360f, rh = 124f, step = 140f, vw = 1300f;
            Text(_body, "Lead", x, y0, w, 60, 44f, Theme.Grey, "What other mods have added to this menu. Each of them keeps and saves its own settings; a change is handed over as you make it.", TextAlignmentOptions.Left);
            float top = y0 + 110f;
            var area = Scroll("mods", x, top, w, 1800f - top, false, 40f);
            float y = 0f; string owner = null, group = null;
            foreach (var option in options)
            {
                var o = option; float head = y;
                if (o.Owner != owner)
                {
                    owner = o.Owner; group = null;
                    if (y > 0f) { y += 36f; head = y; }
                    Text(area.Content, "Owner", 0, y, w, 78, 50f, Theme.GoldText, owner.ToUpperInvariant(), TextAlignmentOptions.Left, true);
                    Box(area.Content, "OwnerRule", 0, y + 82f, w, 3f, Theme.GoldRule);
                    y += 104f;
                }
                if (o.Group != group)
                {
                    group = o.Group;
                    if (group.Length > 0) { Text(area.Content, "Group", 8, y, w, 60, 38f, Theme.Grey, group.ToUpperInvariant(), TextAlignmentOptions.Left, true); y += 66f; }
                }
                var c = Cycler(area.Content, "mod:" + o.Key, 0, y, w, rh, o.Label, null, () => Api.Extensions.ValueOf(o),
                    d =>
                    {
                        if (Api.Extensions.ChoicesOf(o).Length == 0) return;        // nothing to choose from right now
                        SettingSaved(Api.Extensions.Step(o, d), o.Owner + " did not take the change");
                        foreach (var k in _ctls) if (k.Refresh != null && k.Key.StartsWith("mod:", StringComparison.Ordinal)) k.Refresh();      // one option may move another, and its choices
                    },
                    () => o.Description.Length > 0 ? o.Description : "An option of " + o.Owner + ".", vw);
                c.In = area; c.Lead = y - head;
                y += step;
            }
            Settle(area, Mathf.Max(0f, y - (step - rh)));
        }

        // ================================================================ the game's own art: portraits, weapon and ability icons
        static void ReadGameArt()
        {
            if (_icons.Count > 0 && _portraits.Count > 0) return;
            try
            {
                var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<PowerupBase>());
                for (int i = 0; i < all.Length; i++)
                {
                    var p = all[i].TryCast<PowerupBase>(); if (p == null) continue;
                    Sprite s = null; try { s = p.powerupSpriteMini ?? p.powerupSprite; } catch { }
                    if (s != null) _icons[G.Name(p)] = s;
                }
                var classes = Resources.FindObjectsOfTypeAll(Il2CppType.Of<ClassProperties>());
                for (int i = 0; i < classes.Length; i++)
                {
                    var cp = classes[i].TryCast<ClassProperties>(); if (cp == null) continue;
                    string name = G.ClassName(cp.characterType);
                    Sprite s = null; try { s = cp.heroSelectionPreviewSprite ?? cp.heroDialoguePortrait; } catch { }
                    if (s != null) _portraits[name] = s;
                    _unlocked[name] = G.Unlocked(cp);
                }
                Plugin.Logger.LogInfo("[menu] game art: " + _icons.Count + " icons, " + _portraits.Count + " portraits");
            }
            catch (Exception e) { Plugin.Logger.LogInfo("[menu] game art: " + e.Message); }
        }

        // ================================================================ the COMPANION button on the game's menus
        static Button _mainButton, _pauseButton;

        static void EnsureButtons()
        {
            bool want = true; try { want = Plugin.MenuButton.Value; } catch { }
            if (!want) return;
            try
            {
                var mm = MainMenu();
                if (mm != null && (_mainButton == null || !Alive(_mainButton)))
                {
                    Button template = null;
                    var col = mm.buttonsVerticalLayoutGroup != null ? mm.buttonsVerticalLayoutGroup.transform : null;
                    if (col != null) for (int i = 0; i < col.childCount; i++) { var ch = col.GetChild(i); if (ch.gameObject.activeSelf && ch.name.EndsWith("_Options")) template = ch.GetComponent<Button>(); }
                    if (template != null) _mainButton = CloneButton(template, "YazsCompanionButton");
                }
                if (_mainButton != null && Alive(_mainButton)) Rewire(_mainButton);
                var pm = mm == null ? PauseMenu() : null;
                if (pm != null && (_pauseButton == null || !Alive(_pauseButton)))
                {
                    Button template = null;
                    foreach (var b in new[] { pm.achievementsButton, pm.howToPlayButton, pm.itemPoolButton }) { try { if (b != null && b.gameObject.activeInHierarchy) { template = b; break; } } catch { } }
                    if (template != null) _pauseButton = CloneButton(template, "YazsCompanionPauseButton");
                }
                if (_pauseButton != null && Alive(_pauseButton)) Rewire(_pauseButton);
            }
            catch (Exception e) { Plugin.Logger.LogInfo("[menu] button: " + e.Message); }
        }

        static bool Alive(Button b) { try { return b != null && b.gameObject != null && b.transform.parent != null; } catch { return false; } }

        static readonly Dictionary<IntPtr, Button> _anchor = new Dictionary<IntPtr, Button>();

        static Button CloneButton(Button template, string name)
        {
            var go = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            go.name = name;
            go.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
            var label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                var comps = label.GetComponents<Component>();
                for (int i = 0; i < comps.Length; i++)
                {
                    var c = comps[i]; if (c == null) continue;
                    string n = ""; try { n = c.GetIl2CppType().Name; } catch { }
                    if (n == "Localize" || n == "RegisterMeForLanguageChanges") UnityEngine.Object.Destroy(c);      // they would put the original caption back
                }
                label.text = "Companion";
            }
            var btn = go.GetComponent<Button>();
            btn.onClick = new Button.ButtonClickedEvent();      // drops the serialized call to the game's own handler
            btn.onClick.AddListener(Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>(new Action(Open)));
            _anchor[btn.Pointer] = template;
            Plugin.Logger.LogInfo("[menu] COMPANION button added under '" + template.name + "'");
            return btn;
        }

        // explicit navigation: template -> clone -> what used to be under the template (the game re-fixes its column on enable)
        static void Rewire(Button clone)
        {
            Button above; if (!_anchor.TryGetValue(clone.Pointer, out above) || above == null) return;
            var na = above.navigation;
            if (na.mode != Navigation.Mode.Explicit) return;
            if (na.selectOnDown != null && na.selectOnDown.Pointer == clone.Pointer) return;
            var below = na.selectOnDown;
            var nc = clone.navigation; nc.mode = Navigation.Mode.Explicit; nc.selectOnUp = above; nc.selectOnDown = below; nc.selectOnLeft = null; nc.selectOnRight = null; clone.navigation = nc;
            na.selectOnDown = clone; above.navigation = na;
            if (below != null) { var nb = below.navigation; if (nb.mode == Navigation.Mode.Explicit) { nb.selectOnUp = clone; below.navigation = nb; } }
        }

        // ================================================================ the pause walk ([Debug] PreviewPause)
        // Start a Quick Run with the game's own button, let the game open its own pause menu a few seconds in (the
        // pause-key prefix below answers "pressed" once), check the COMPANION button arrived there, open the mod menu over
        // the paused run, change two display settings through the menu's own controls (and put them back), close it, check
        // the pause menu is still up, then resume for five seconds so the readout the mod menu took down comes back - with
        // the changed settings. Well under fifty seconds of play: no save is written.
        static bool _ppDone, _ppResumed; static int _ppStage; static float _ppAt = -1f, _ppStarted;
        static PanelDetailLevel _ppDetail; static float _ppSize; static bool _ppChanged;
        static float _ppLastPick = -10f; static int _ppPauseTries;
        internal static bool FakePauseOnce;

        // Quick Run does not always start the run: it can stop at SELECT TEAM LEADER, and it can open the whole run
        // wizard - arena, game mode, run setup (difficulty, badges) and the START bar. The walk clicks through whichever
        // of them is up with the game's own handlers, taking what each screen proposes (the profile's last choice), else
        // the game's default, else the first that is unlocked. Those screens store the choice in the profile; scripted
        // anyway at the user's request (2026-09-20), so that a test run needs no hands. Later screens are asked first: an
        // earlier one may stay loaded underneath.
        static int _wzHero, _wzArena, _wzMode, _wzSetup, _wzStart;

        static bool WizardStep()
        {
            var setup = FindActive<UIViewRunSetup>();
            if (setup != null)
            {
                if (_wzSetup < 1)       // one press selects the difficulty (seen in the game); START is the bar's
                {
                    _wzSetup++;
                    UIViewRunSetupDifficultyButton pick = null;
                    try { var sel = setup.selectedDifficultyButton; if (sel != null && !sel.IsLocked()) pick = sel; } catch { }
                    if (pick == null)
                        try { var list = setup.difficultyButtons; for (int i = 0; list != null && i < list.Count; i++) { var b = list[i]; if (b != null && b.gameObject.activeInHierarchy && !b.IsLocked()) { pick = b; break; } } } catch { }
                    if (pick != null)
                    {
                        string name = "?"; try { name = pick.difficultyText != null ? pick.difficultyText.text : pick.name; } catch { }
                        Plugin.Logger.LogInfo("[menu] pause walk: run setup, difficulty '" + name + "'"); setup.OnClickDifficulty(pick); return true;
                    }
                    Plugin.Logger.LogInfo("[menu] pause walk: run setup, no difficulty button to press");
                }
                var bar = FindActive<UIStartGameBar>();
                if (bar != null && _wzStart < 3)
                {
                    _wzStart++;
                    bool ok = true; try { ok = bar.IsSelectedRunAvailable(); } catch { }
                    Plugin.Logger.LogInfo("[menu] pause walk: START" + (ok ? "" : " (the game says the selected run is not available)")); bar.OnClickStartGame(); return true;
                }
                return false;
            }
            var arena = FindActive<UIViewChooseArena>();
            if (arena != null)
            {
                bool modes = false; try { modes = arena.modeButtonsContainer != null && arena.modeButtonsContainer.activeInHierarchy; } catch { }
                if (!modes)
                {
                    if (_wzArena >= 3) return false;
                    _wzArena++;
                    try { if (arena._currentArenaSelected == null && arena.defaultArena != null) arena.ArenaButtonClicked(arena.defaultArena); } catch { }
                    string name = "?"; try { if (arena._currentArenaSelected != null) name = arena._currentArenaSelected.name; } catch { }
                    Plugin.Logger.LogInfo("[menu] pause walk: arena screen, '" + name + "'"); arena.OnClickStartArena(); return true;
                }
                if (_wzMode >= 4) return false;
                _wzMode++;
                if (_wzMode % 2 == 1)
                {   // the mode the screen proposes; should that only select it, the next step presses Continue
                    UIGameplayModeButton b = null; try { b = arena._selectedGameplayModeButton ?? arena.defaultMode; } catch { }
                    if (b != null) { string mode = "?"; try { mode = b.gameplayMode.ToString(); } catch { } Plugin.Logger.LogInfo("[menu] pause walk: game mode '" + mode + "'"); arena.OnClickStartGameMode(b); return true; }
                }
                Plugin.Logger.LogInfo("[menu] pause walk: game mode, Continue"); arena.OnClickContinueFromGameMode(); return true;
            }
            var hero = FindActive<UIViewChooseHero>();
            if (hero != null && _wzHero < 3) { _wzHero++; Plugin.Logger.LogInfo("[menu] pause walk: team leader screen, Start"); hero.OnClickStart(); return true; }
            return false;
        }

        // work a control of the open mod menu the way a key press does (its own Change handler), for the walk
        static bool Work(string key, int d)
        {
            var c = _ctls.FirstOrDefault(k => k.Key == key);
            if (c == null || c.Change == null) { Plugin.Logger.LogWarning("[menu] pause walk: no control '" + key + "'"); return false; }
            c.Change(d); return true;
        }

        static void PausePreviewTick()
        {
            bool on = false; try { on = Plugin.PreviewPause.Value; } catch { }
            if (_ppDone || !on) return;
            float now = Time.realtimeSinceStartup;
            if (_ppAt < 0) { _ppAt = now + 7f; return; }
            if (now < _ppAt) return;
            try
            {
                switch (_ppStage)
                {
                    case 0:
                        {
                            var mm = MainMenu(); if (mm == null) { _ppAt = now + 2f; return; }
                            Plugin.Logger.LogInfo("[menu] pause walk: Quick Run"); mm.quickRunButton.onClick.Invoke();
                            _ppAt = now + 4f; _ppStage = 1; _ppStarted = now; return;
                        }
                    case 1:
                        {
                            bool playing = false; float t = 0f;
                            try { var gm = GameplayMaster.s_instance; if (gm != null && gm.currentGameMode != null) { playing = gm.currentGameMode.IsGameplayActive; t = gm.currentGameMode.CurrentModePlayTime; } } catch { }
                            // thirty seconds of play first, taking the recommended card of every offer on the way: that is what
                            // exercises the readout, the plan worked out while a screen closes, and the [Debug] Perf sections -
                            // or up to forty when no offer has come by then: a walk without one offer ranked and taken
                            // checks little (still under the fifty seconds after which a killed run leaves a save)
                            bool more = t < 30f || (Advisor.DebugPicks == 0 && t < 40f);
                            if (playing && Advisor.DebugPickDue(now)) { _ppLastPick = now; _ppAt = now + 0.5f; return; }      // an offer that is up is always answered
                            // the game opens its pause menu by itself when its window is not the focused one as the run comes
                            // up (a launch from a script): the clock then stands at 0 until somebody resumes - do that, once
                            if (playing && more && !_ppResumed && PauseFlow())
                            {
                                var own = PauseMenu();
                                if (own != null) { _ppResumed = true; Plugin.Logger.LogInfo("[menu] pause walk: the game paused itself at " + t.ToString("0.0") + " s, resuming"); own.OnClickClose(); _ppAt = now + 1f; return; }
                            }
                            if (!playing || more)
                            {
                                _ppAt = now + (playing ? 0.5f : 1.5f);
                                if (!playing && WizardStep()) _ppAt = now + 2.5f;       // one screen of the run wizard per step
                                if (now - _ppStarted > 200f) { Plugin.Logger.LogWarning("[menu] pause walk: the run never started"); _ppDone = true; }
                                return;
                            }
                            // the game takes no pause key while a selection screen is up or still animating out (seen: asked
                            // half a second after a pick, the pause menu never came): wait for plain play
                            bool busy = now - _ppLastPick < 2.5f; try { busy |= GameplayMaster.IsPaused; } catch { }
                            if (busy) { _ppAt = now + 0.5f; return; }
                            Plugin.Logger.LogInfo("[menu] pause walk: pausing at " + t.ToString("0.0") + " s of play"); FakePauseOnce = true;
                            _ppAt = now + 2.5f; _ppStage = 2; return;
                        }
                    case 2:
                        if (PauseMenu() == null && _ppPauseTries < 3)
                        {   // not up: an offer got in first - answer it, then ask again
                            _ppPauseTries++;
                            if (Advisor.DebugPickDue(now)) { _ppLastPick = now; _ppAt = now + 3f; return; }
                            bool paused = false; try { paused = GameplayMaster.IsPaused; } catch { }
                            if (!paused) { Plugin.Logger.LogInfo("[menu] pause walk: the pause menu did not come, asking again"); FakePauseOnce = true; }
                            _ppAt = now + 2.5f; return;
                        }
                        EnsureButtons();
                        Plugin.Logger.LogInfo("[menu] pause walk: pause menu " + (PauseMenu() != null ? "open" : "NOT open") + ", COMPANION button " + (_pauseButton != null && Alive(_pauseButton) ? "added" : "MISSING"));
                        Shots.Later(0.2f, "pause0_menu", true); _ppAt = now + 1.0f; _ppStage = 3; return;
                    case 3:
                        Open(); Shots.Later(1.0f, "pause1_modmenu", true); _ppAt = now + 1.8f; _ppStage = 4; return;
                    case 4:
                        Plugin.Logger.LogInfo("[menu] pause walk: mod menu " + (_open ? "open over the paused run" : "did NOT open"));
                        if (!_open) { _ppStage = 5; _ppAt = now + 0.5f; return; }
                        SetTab(2); _ppAt = now + 1.2f; _ppStage = 41; return;
                    case 41:
                        {   // DISPLAY: the settings a player would try first, through the controls themselves; the preview follows
                            _ppDetail = Plugin.PanelDetail.Value; _ppSize = Plugin.PanelSize.Value;
                            Shots.Later(0.05f, "pause2_display", true);
                            _ppAt = now + 0.6f; _ppStage = 42; return;
                        }
                    case 42:
                        _ppChanged = true; Work("di:detail", 1); Work("di:size", 1); Work("di:size", 1);
                        Plugin.Logger.LogInfo("[menu] pause walk: detail " + _ppDetail + " -> " + Plugin.PanelDetail.Value + ", size " + _ppSize.ToString("0.0") + " -> " + Plugin.PanelSize.Value.ToString("0.0"));
                        Shots.Later(0.9f, "pause3_display_changed", true);
                        _ppAt = now + 1.6f; _ppStage = 43; return;
                    case 43:
                        Back(); _ppAt = now + 1.0f; _ppStage = 5; return;
                    case 5:
                        {
                            bool paused = false; try { paused = GameplayMaster.IsPaused; } catch { }
                            Plugin.Logger.LogInfo("[menu] pause walk: after closing - pause menu " + (PauseMenu() != null ? "still up" : "GONE") + ", game " + (paused ? "paused" : "RUNNING"));
                            _ppStage = 6; _ppAt = now + 1.0f; return;
                        }
                    case 6:
                        {   // back into play: the readout the mod menu took down has to come back ([panel] created / shown, [plan])
                            var pm = PauseMenu();
                            Plugin.Logger.LogInfo("[menu] pause walk: " + (pm != null ? "resuming the run" : "no pause menu to resume from"));
                            if (pm != null) pm.OnClickClose();
                            Shots.Later(2.5f, "pause4_resumed", true);
                            _ppStage = 7; _ppAt = now + 5f; return;
                        }
                    case 7:
                        // the walk's two changes are the walk's: the player's settings go back as they were - when it made
                        // them (a walk whose mod menu never opened once wrote its unset 0 into PanelSize here)
                        if (_ppChanged)
                        {
                            Plugin.PanelDetail.Value = _ppDetail; Plugin.PanelSize.Value = _ppSize; _ppChanged = false;
                            Plugin.Logger.LogInfo("[menu] pause walk: settings put back (detail " + Plugin.PanelDetail.Value + ", size " + Plugin.PanelSize.Value.ToString("0.0") + ")");
                        }
                        _ppStage = 8; return;
                    default:
                        _ppDone = true; Plugin.Logger.LogInfo("[menu] pause walk done"); return;
                }
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning("[menu] pause walk: " + e); _ppDone = true;
                if (_ppChanged) { _ppChanged = false; try { Plugin.PanelDetail.Value = _ppDetail; Plugin.PanelSize.Value = _ppSize; } catch { } }
            }
        }

        // ================================================================ the preview walk ([Debug] PreviewMenu)
        static bool _pvDone; static int _pvStage; static float _pvAt = -1f;

        static void PreviewTick()
        {
            bool on = false; try { on = Plugin.PreviewMenu.Value; } catch { }
            if (_pvDone || !on) return;
            float now = Time.realtimeSinceStartup;
            if (_pvAt < 0) { _pvAt = now + 7f; return; }
            if (now < _pvAt) return;
            try
            {
                switch (_pvStage)
                {
                    case 0:
                        if (MainMenu() == null) { _pvAt = now + 2f; return; }
                        if (Preview.Window()) { _pvAt = now + 2.5f; _pvStage = 1; return; }
                        _pvStage = 1; return;
                    case 1:
                        EnsureButtons(); Shots.Later(0.3f, "menu0_mainmenu", true);
                        _pvAt = now + 1.0f; _pvStage = 2; return;
                    case 2:
                        Open(); for (int f = 0; f < 4; f++) Shots.Later(0.05f + 0.1f * f, "fxmenu" + f, true);
                        Shots.Later(1.2f, "menu1_builds", true); _pvAt = now + 1.8f; _pvStage = 3; return;
                    case 3:
                        _survivor = 1; _focusKey = "build:tank-reaper"; _dirty = true;
                        Shots.Later(0.9f, "menu2_tank", true); _pvAt = now + 1.5f; _pvStage = 4; return;
                    case 4:
                        { var mine = Builds.EnsureCustom("Tank"); _editing = true; _focusKey = "ed:evo:Bombing Strike"; _dirty = true; }
                        Shots.Later(0.9f, "menu3_editor", true); _pvAt = now + 1.5f; _pvStage = 5; return;
                    case 5:
                        Builds.DropCustom("Tank"); SetTab(1); _focusKey = "ad:timing"; _dirty = true;
                        Shots.Later(0.9f, "menu4_advice", true); _pvAt = now + 1.5f; _pvStage = 6; return;
                    case 6:
                        SetTab(2); Shots.Later(0.9f, "menu5_display", true); _pvAt = now + 1.5f; _pvStage = _tabCount > 3 ? 61 : 7; return;
                    case 61:    // only while another mod has options registered
                        SetTab(3); Shots.Later(0.9f, "menu6_mods", true); _pvAt = now + 1.5f; _pvStage = 7; return;
                    case 7:
                        Close(); Preview.Restore(); _pvAt = now + 1.5f; _pvStage = 8; return;
                    default:
                        _pvDone = true; Plugin.Logger.LogInfo("[menu] preview done"); return;
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[menu] preview: " + e); Preview.Restore(); _pvDone = true; }
        }
    }

    // ---- while the mod menu is open, the game's menus underneath hold still ----
    // (the two menu views also report themselves here: that is how the mod knows which menu is up without searching)
    [HarmonyPatch(typeof(UIViewMainMenu), nameof(UIViewMainMenu.Update))]
    static class P_MenuHoldMain { static bool Prefix(UIViewMainMenu __instance) { Menu.Saw(__instance); return !Menu.Blocking; } }
    [HarmonyPatch(typeof(UIPauseMenu), nameof(UIPauseMenu.Update))]
    static class P_MenuHoldPause { static bool Prefix(UIPauseMenu __instance) { Menu.Saw(__instance); return !Menu.Blocking; } }
    [HarmonyPatch(typeof(SelectFirstButtonIfNeeded), nameof(SelectFirstButtonIfNeeded.Update))]
    static class P_MenuHoldSelect { static bool Prefix() { return !Menu.Blocking; } }
    [HarmonyPatch(typeof(GameplayMaster), nameof(GameplayMaster.GetPauseClickInput))]
    static class P_MenuHoldPauseKey
    {
        static bool Prefix(ref bool __result)
        {
            if (Menu.FakePauseOnce) { Menu.FakePauseOnce = false; __result = true; return false; }      // the pause walk presses the key for the game
            if (!Menu.Blocking) return true;
            __result = false; return false;
        }
    }
}
