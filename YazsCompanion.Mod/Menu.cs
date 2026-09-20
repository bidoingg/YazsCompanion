// The mod menu: BUILDS (the build the advice follows per survivor, with an editor for your own), ADVICE (the standing
// orders of the ranking: level-up style, the run clock, the game mode, squad synergy, what the run is for, caution,
// tags, recruiting) and DISPLAY (what the mod draws). Opened from a COMPANION button the mod adds to the main menu
// and the pause menu, or with a key (F10). Mouse, keyboard and controller.
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
        static readonly string[] Tabs = { "BUILDS", "ADVICE", "DISPLAY" };
        static readonly string[] TabGlyphs = { "chevrons", "clock", "eye" };

        sealed class Ctl
        {
            public string Key; public RectTransform Rt; public Image Bg; public RectTransform Glow;
            public Func<string> Desc; public Action Press; public Action<int> Change; public Action Focused;
            public RectTransform Left, Right;
        }

        static GameObject _go; static RectTransform _stage, _body; static CanvasGroup _group, _bodyGroup;
        static TextMeshProUGUI _template, _desc, _hints; static readonly TextMeshProUGUI[] _tabLabels = new TextMeshProUGUI[3];
        static RectTransform _tabRule;
        static readonly List<Ctl> _ctls = new List<Ctl>(); static Ctl _focus; static string _focusKey = "";
        static bool _open, _dirty, _editing; static int _tab, _survivor, _blockFrame = -1;
        static GameObject _restoreSelected;
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
                _open = true; _editing = false; _dirty = true;
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
            _go = null; _stage = null; _body = null; _ctls.Clear(); _focus = null;
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
                if (_dirty) { _dirty = false; BuildBody(); }
                ReadInput();
                if (_dirty && _open) { _dirty = false; BuildBody(); }
            }
            catch (Exception e) { Plugin.Logger.LogError("[menu] " + e); Close(); }
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

        static void ReadInput()
        {
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

            if (Pad("GoNextTab") || Pad("GoNextTab2") || Key(KeyCode.E) || Key(KeyCode.PageDown)) { SetTab((_tab + 1) % Tabs.Length); return; }
            if (Pad("GoPrevTab") || Pad("GoPrevTab2") || Key(KeyCode.Q) || Key(KeyCode.PageUp)) { SetTab((_tab + Tabs.Length - 1) % Tabs.Length); return; }
            if (Pad("Cancel") || Key(KeyCode.Escape) || Key(KeyCode.Backspace)) { Back(); return; }

            Mouse();
            if (!_open) return;
            if (fire)
            {
                if (y != 0) Move(0, y);
                else if (_focus != null && _focus.Change != null) { _focus.Change(x); Click(); }
                else Move(x, 0);
            }
            if (Pad("UISubmit") || Key(KeyCode.Return) || Key(KeyCode.KeypadEnter) || Key(KeyCode.Space)) Activate(_focus);
        }

        static Vector3 _lastMouse;
        static void Mouse()
        {
            Vector3 mp; try { mp = UnityEngine.Input.mousePosition; } catch { return; }
            bool moved = (mp - _lastMouse).sqrMagnitude > 4f; _lastMouse = mp;
            bool down = false; try { down = UnityEngine.Input.GetMouseButtonDown(0); } catch { }
            if (!moved && !down) return;
            var p = new Vector2(mp.x, mp.y);
            for (int i = 0; i < 3; i++)
                if (down && _tabLabels[i] != null && Hit(_tabLabels[i].rectTransform, p)) { SetTab(i); return; }
            Ctl over = null;
            foreach (var c in _ctls) if (c.Rt != null && Hit(c.Rt, p)) { over = c; break; }
            if (over == null) return;
            if (over != _focus) Focus(over, true);
            if (!down) return;
            if (over.Change != null && over.Left != null && Hit(over.Left, p)) { over.Change(-1); Click(); }
            else if (over.Change != null && over.Right != null && Hit(over.Right, p)) { over.Change(1); Click(); }
            else Activate(over);
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
            float tx = 2050f;
            for (int i = 0; i < Tabs.Length; i++)
            {
                Pic(_stage, "TabIcon" + i, tx + i * 560f, 112f, 64f, 64f, Art.Glyph(TabGlyphs[i]), Theme.Gold);
                _tabLabels[i] = Text(_stage, "Tab" + i, tx + i * 560f + 84f, 96f, 440f, 96f, 60f, Theme.Grey, Tabs[i], TextAlignmentOptions.Left, true);
            }
            _tabRule = Box(_stage, "TabRule", tx, 204f, 420f, 8f, Theme.Gold);
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
            for (int i = _body.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_body.GetChild(i).gameObject);
            _ctls.Clear(); _focus = null;
            for (int i = 0; i < Tabs.Length; i++) if (_tabLabels[i] != null) _tabLabels[i].color = i == _tab ? Theme.GoldText : Theme.Grey;
            if (_tabRule != null) { float to = 2050f + _tab * 560f, from = _tabRule.anchoredPosition.x; Fx.Run("menu.tab", 0f, 0.22f, k => { _tabRule.anchoredPosition = new Vector2(Mathf.Lerp(from, to, Fx.OutCubic(k)), -204f); }); }

            if (_tab == 0) { if (_editing) BuildEditor(); else BuildBuilds(); }
            else if (_tab == 1) BuildAdvice();
            else BuildDisplay();
            Hints();

            Ctl want = _ctls.FirstOrDefault(c => c.Key == _focusKey) ?? _ctls.FirstOrDefault();
            if (want != null) Focus(want, false);
            if (_bodyGroup != null) Fx.Run("menu.body", 0f, 0.2f, k => { _bodyGroup.alpha = Fx.OutCubic(k); _body.anchoredPosition = new Vector2(0f, -18f * (1f - Fx.OutCubic(k))); });
        }

        // ---------------------------------------------------------------- BUILDS
        static string StyleText(BuildStyle s) { return s == BuildStyle.Weapon ? "Weapon first" : s == BuildStyle.Ability ? "Abilities first" : "Balanced"; }
        static string Short(string evolution) { int i = evolution == null ? -1 : evolution.IndexOf(':'); return i >= 0 ? evolution.Substring(i + 1).Trim() : evolution ?? ""; }

        static void BuildBuilds()
        {
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
                var b = Builds.For(sv);
                Text(c.Rt, "Build", 172, 84, 570, 54, 40f, b != null ? Theme.GoldText : Theme.Grey, b != null ? b.Name : "Auto", TextAlignmentOptions.Left);
                c.Desc = () => sv + (unlocked ? "" : " (not unlocked yet)") + ": " + (Builds.For(sv) != null ? "following " + Builds.For(sv).Name + ". " : "on Auto - the advice reads your squad. ") + "Move right to choose a build.";
                c.Focused = () => { if (_survivor != index) { _survivor = index; _dirty = true; } };
                c.Press = () => Move(1, 0);
            }

            // ---- the builds of the survivor in focus
            string who = Builds.Survivors[_survivor];
            var choices = Builds.ChoicesOf(who);
            string selected = Builds.SelectedId(who);
            int n = choices.Count + 1; float gap = 26f, area = 2760f, cw = Mathf.Min(700f, (area - (n - 1) * gap) / n), x0 = 940f, y0 = 352f, ch = 1230f;
            Text(_body, "Who", x0, y0 - 2f, 2000, 70, 50f, Theme.Grey, "<color=" + Theme.GoldHex + ">" + who.ToUpperInvariant() + "</color>   the build the advice follows", TextAlignmentOptions.Left, true);
            y0 += 84f;
            for (int i = 0; i < n; i++)
            {
                Build b = i == 0 ? null : choices[i - 1];
                string id = b == null ? Builds.AutoId : b.Id;
                bool active = string.Equals(id, selected, StringComparison.OrdinalIgnoreCase);
                var c = Plate(_body, "build:" + id, x0 + i * (cw + gap), y0, cw, ch, active ? new Color(1f, 0.93f, 0.74f, 1f) : new Color(1f, 1f, 1f, 0.82f));
                BuildCard(c, who, b, cw, active);
                c.Press = () => { Builds.Select(who, id); _focusKey = "build:" + id; _dirty = true; Plugin.Logger.LogInfo("[menu] " + who + " follows " + (b == null ? "Auto" : b.Name)); };
            }

            // ---- your own build
            var custom = Builds.CustomOf(who); float by = y0 + ch + 30f;
            Btn(_body, "customize", x0, by, 760, 116, custom == null ? "MAKE MY OWN BUILD" : "EDIT MY BUILD", "gear", () =>
            {
                var mine = Builds.EnsureCustom(who); Builds.Select(who, mine.Id); Builds.Save();
                _editing = true; _focusKey = "ed:style"; _dirty = true;
            }, () => custom == null
                ? "Start your own " + who + " build from the one selected: change the level-up style, the weapon branch, the order of the abilities, the evolution of each, and what the build wants from items."
                : "Edit your own " + who + " build: style, weapon branch, ability order, evolutions, item leanings.");
            if (custom != null)
                Btn(_body, "delete", x0 + 790, by, 620, 116, "DELETE MY BUILD", null, () => { Builds.DropCustom(who); _focusKey = "customize"; _dirty = true; },
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

        static void BuildCard(Ctl c, string who, Build b, float cw, bool active)
        {
            var rt = c.Rt; float pad = 30f, inner = cw - 2 * pad;
            Pic(rt, "Glyph", pad, 30, 116, 116, Art.Glyph(b == null ? "chevrons" : string.IsNullOrEmpty(b.Glyph) ? "diamond" : b.Glyph), Theme.Gold);
            Text(rt, "Name", pad + 136, 30, inner - 136, 70, 54f, Theme.White, b == null ? "Auto" : b.Name, TextAlignmentOptions.Left, true);
            string tag = b == null ? "READS YOUR SQUAD" : b.Custom ? "YOUR BUILD" : b.Source == "guides" ? "GUIDE PICK" : "ALTERNATIVE";
            Text(rt, "Tag", pad + 138, 100, inner - 136, 48, 34f, b != null && b.Source == "guides" ? Theme.GoldText : Theme.Grey, tag, TextAlignmentOptions.Left, true);
            if (active)
            {
                var rib = Box(rt, "Active", cw - 250, -18, 232, 50, Theme.Gold);
                Text(rib, "T", 0, 0, 232, 50, 32f, new Color(0.08f, 0.06f, 0.03f, 1f), "FOLLOWING", TextAlignmentOptions.Center, true);
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
                ? "AUTO. No fixed build: the weapon branch and the evolutions follow what your squad deals right now (shared damage types, tag specials within reach, team passives), then your Training Yard investment, then the guides. Level-ups use the style set on the ADVICE tab."
                : b.Summary + (active ? "" : "   [select to follow it]");
        }

        // ---------------------------------------------------------------- the editor of your own build
        static void BuildEditor()
        {
            string who = Builds.Survivors[_survivor];
            var b = Builds.EnsureCustom(who); var kit = Builds.KitOf(who);
            float x = 520f, w = 2800f, y = 352f, rh = 118f, step = 132f;
            Text(_body, "Who", x, y - 2f, w, 70, 50f, Theme.Grey, "<color=" + Theme.GoldHex + ">" + who.ToUpperInvariant() + "</color>   your own build", TextAlignmentOptions.Left, true);
            y += 84f;
            Action changed = () => { Builds.Save(); };

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
                    Builds.ReplaceCustom(who, from); _focusKey = "ed:copy"; _dirty = true;
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
            Text(_body, "Lead", x, y, w, 60, 44f, Theme.Grey, "The standing orders of the advice. They apply to every survivor; a build you select on the BUILDS tab brings its own level-up style.", TextAlignmentOptions.Left);
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

        static void BuildDisplay()
        {
            float x = 320f, w = 3200f, y = 360f, rh = 128f, step = 148f;
            Text(_body, "Lead", x, y, w, 60, 44f, Theme.Grey, "What the mod draws. Changes apply at once; the PLAN readout rebuilds when you close this menu.", TextAlignmentOptions.Left);
            y += 96f;
            Func<bool, string> onOff = v => v ? "On" : "Off";
            Cycler(_body, "di:badges", x, y, w, rh, "Card verdicts", "diamond", () => onOff(Plugin.ShowBadges.Value), d => Plugin.ShowBadges.Value = !Plugin.ShowBadges.Value,
                () => "Frame the recommended card, hang the RECOMMENDED ribbon under it and print a reason under every offered card."); y += step;
            Cycler(_body, "di:panel", x, y, w, rh, "PLAN readout during play", "eye", () => onOff(Plugin.ShowPanel.Value), d => Plugin.ShowPanel.Value = !Plugin.ShowPanel.Value,
                () => "The see-through readout of what to pick next: a row per survivor, then TAGS / SOS / GRAB."); y += step;
            Cycler(_body, "di:detail", x, y, w, rh, "Readout detail", null, () => Plugin.PanelDetail.Value.ToString(), d => Plugin.PanelDetail.Value = Next(Plugin.PanelDetail.Value, d),
                () => "COMPACT: one row per survivor with only what to pick next. FULL: two rows per survivor with the next steps and the evolution names."); y += step;
            Cycler(_body, "di:place", x, y, w, rh, "Readout position", null, () => Words(Plugin.PanelPosition.Value.ToString()), d => Plugin.PanelPosition.Value = Next(Plugin.PanelPosition.Value, d),
                () => "BOTTOM LEFT: the empty corner under the weapon and ability icons. RIGHT: the right edge between the item icons and the minimap."); y += step;
            Cycler(_body, "di:opacity", x, y, w, rh, "Readout backing", null, () => Mathf.RoundToInt(Plugin.PanelOpacity.Value * 100f) + "%", d => Plugin.PanelOpacity.Value = Mathf.Round(Mathf.Clamp01(Plugin.PanelOpacity.Value + 0.1f * d) * 10f) / 10f,
                () => "Darkness of the soft backing under the readout text. Lower shows more of the field; higher reads better over bright effects."); y += step;
            Cycler(_body, "di:idle", x, y, w, rh, "Readout when nothing changed", null, () => Mathf.RoundToInt(Plugin.PanelIdle.Value * 100f) + "%", d => Plugin.PanelIdle.Value = Mathf.Round(Mathf.Clamp(Plugin.PanelIdle.Value + 0.1f * d, 0.2f, 1f) * 10f) / 10f,
                () => "The readout is at full strength right after a pick, then settles to this opacity. 100% = it never dims."); y += step;
            Cycler(_body, "di:yard", x, y, w, rh, "Training Yard advice", "up", () => onOff(Plugin.ShowYard.Value), d => Plugin.ShowYard.Value = !Plugin.ShowYard.Value,
                () => "Number the nodes worth buying with the points on hand and print the SPEND / THEN / WHY strip. It follows the build you selected for that survivor."); y += step;
            Cycler(_body, "di:motion", x, y, w, rh, "Motion", null, () => onOff(Plugin.Motion.Value), d => Plugin.Motion.Value = !Plugin.Motion.Value,
                () => "Animate what the mod draws. During play nothing loops; the flair is kept for menus like this one.");
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
        // the paused run, close it, check the pause menu is still up, then resume for five seconds so the readout the mod
        // menu took down comes back. Well under fifty seconds of play: no save is written.
        static bool _ppDone, _ppHero, _ppResumed; static int _ppStage; static float _ppAt = -1f;
        internal static bool FakePauseOnce;

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
                            _ppAt = now + 4f; _ppStage = 1; return;
                        }
                    case 1:
                        {
                            bool playing = false; float t = 0f;
                            try { var gm = GameplayMaster.s_instance; if (gm != null && gm.currentGameMode != null) { playing = gm.currentGameMode.IsGameplayActive; t = gm.currentGameMode.CurrentModePlayTime; } } catch { }
                            // thirty seconds of play first, taking the recommended card of every offer on the way: that is what
                            // exercises the readout, the plan worked out while a screen closes, and the [Debug] Perf sections
                            if (playing && t < 30f && Advisor.DebugPickDue(now)) { _ppAt = now + 0.5f; return; }
                            // the game opens its pause menu by itself when its window is not the focused one as the run comes
                            // up (a launch from a script): the clock then stands at 0 until somebody resumes - do that, once
                            if (playing && t < 30f && !_ppResumed && PauseFlow())
                            {
                                var own = PauseMenu();
                                if (own != null) { _ppResumed = true; Plugin.Logger.LogInfo("[menu] pause walk: the game paused itself at " + t.ToString("0.0") + " s, resuming"); own.OnClickClose(); _ppAt = now + 1f; return; }
                            }
                            if (!playing || t < 30f)
                            {
                                _ppAt = now + (playing ? 0.5f : 1f);
                                // Quick Run can stop at SELECT TEAM LEADER first: press its Start once, with the leader it proposes
                                if (!playing && !_ppHero) { var hero = FindActive<UIViewChooseHero>(); if (hero != null) { _ppHero = true; Plugin.Logger.LogInfo("[menu] pause walk: team leader screen, Start"); hero.OnClickStart(); _ppAt = now + 3f; } }
                                if (now > 200f) { Plugin.Logger.LogWarning("[menu] pause walk: the run never started"); _ppDone = true; }
                                return;
                            }
                            Plugin.Logger.LogInfo("[menu] pause walk: pausing at " + t.ToString("0.0") + " s of play"); FakePauseOnce = true;
                            _ppAt = now + 2.5f; _ppStage = 2; return;
                        }
                    case 2:
                        EnsureButtons();
                        Plugin.Logger.LogInfo("[menu] pause walk: pause menu " + (PauseMenu() != null ? "open" : "NOT open") + ", COMPANION button " + (_pauseButton != null && Alive(_pauseButton) ? "added" : "MISSING"));
                        Shots.Later(0.2f, "pause0_menu", true); _ppAt = now + 1.0f; _ppStage = 3; return;
                    case 3:
                        Open(); Shots.Later(1.0f, "pause1_modmenu", true); _ppAt = now + 1.8f; _ppStage = 4; return;
                    case 4:
                        Plugin.Logger.LogInfo("[menu] pause walk: mod menu " + (_open ? "open over the paused run" : "did NOT open")); Back();
                        _ppAt = now + 1.0f; _ppStage = 5; return;
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
                            _ppStage = 7; _ppAt = now + 5f; return;
                        }
                    default:
                        _ppDone = true; Plugin.Logger.LogInfo("[menu] pause walk done"); return;
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[menu] pause walk: " + e); _ppDone = true; }
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
                        SetTab(2); Shots.Later(0.9f, "menu5_display", true); _pvAt = now + 1.5f; _pvStage = 7; return;
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
