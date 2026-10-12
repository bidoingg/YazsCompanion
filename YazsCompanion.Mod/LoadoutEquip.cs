// The one-click EQUIP ADVICE button on the run setup screen (0.13.0, the user's decision OD1 = (b): built, OFF by default,
// switched on from the mod menu's BADGES page: [Advice] LoadoutEquip). A plate next to CHOSEN BADGES says what it would do
// ("EQUIP ADVICE  6 clicks", "· Y" while the game is in pad mode, "· <key>" when [Advice] LoadoutEquipKey names one - none by
// default); a click on it (mouse, Deck touch), the key, the pad's Y (0.16.1: the game's TabToggle, the button its own start
// screens use for their extra action - from the pad only: the keyboard key the game maps onto it, T on the PC, is refused) or
// A on the plate once the d-pad has brought the focus there (0.16.1: the plate is a Button in the screen's own automatic
// navigation, with a bright frame while it has the pad's focus; Enter / Space likewise after the arrow keys; a click's or a
// tap's selection of it is given up again at once, as 0.16.0's plate never held one - EquipInput) presses the game's OWN badge
// button along the pure plan of LoadoutView (removes first, then adds - since 0.16.1 in the advice's order, so slot i ends
// holding badge #i; never a forced badge, never a locked one, never past the slots) - one press every 0.12 s, each exactly
// what a click by hand does: the game plays its sound, writes RunSetup_Badge1..4 into the profile and refreshes the slots.
// Every press is logged: the start line says how it was pressed (0.16.0, C16-11e: 'pressed by touch', 'mouse' or 'key';
// 0.16.1 'pad') and the slots before -> after, the done line the slots' numbers and whether they read in the advice's order.
// Afterwards the plate offers UNDO (0.16.1: the old row back - the same badges in the same slots - under the same guards)
// until the screen closes or the player changes the selection by hand.
//
// The guards are checked again, against live state, before EVERY press (GuardedClick): the screen is up and takes input, the
// mod menu is closed, the setting is on, the button still holds the badge the step names; for an add the badge is unlocked
// (no padlock, the game's own level >= 1, the button interactable) and the game has a free slot; for a remove the badge is
// selected and not forced; and the selection is what the presses so far lead to (EquipRun.Next) - a click by hand in between
// stops the run. GuardedClick is the only place in Loadout*.cs that may call the game's OnClickBadge (the bench's safety scan
// fails otherwise). No press starts while the screen takes no input (it flies in or out) or the mod menu holds the game's
// menus; Y is not taken while one of the game's other views that read TabToggle themselves is live (machine code of 1.0.2:
// the main menu, hero, arena, item pool, run history, save profile and camp hub views) - it would act there too.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YazsCompanion
{
    internal static class LoadoutEquip
    {
        const string PlateName = "YazsLoadoutEquip";
        const float Gap = 0.12f;
        const string PadAction = "TabToggle";            // the pad's Y on the game's start screens (Run History, Leaderboards, Change preset)
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        static RectTransform _plate, _ring; static TextMeshProUGUI _caption; static IntPtr _viewPtr;
        static EquipRun _run, _undo; static bool _running, _undoing; static float _nextPress;
        static List<int> _after;                 // the selection the last run left: UNDO is offered while it stays so
        static LoadoutVisit _visit; static int _advice;
        static LoadoutView _from;                // the view the last EQUIP started from: the numbers its done line reads the slots by
        static Camera _cam; static bool _camRead;
        static string _keyName; static KeyCode _key; static bool _keyValid;
        static bool _pad, _focused, _focusSaid; static float _nextPadRead;
        static bool _selected, _padFocus; static int _mouseFrame = -1000, _tickFrame = -1000;     // review 2: whose focus the plate holds (EquipInput.Focus)
        static readonly HashSet<string> _said = new HashSet<string>();      // once a visit: why a press was not taken
        public static string PlanText = "";

        public static bool On { get { try { return Plugin.AdviceLoadoutEquip.Value; } catch { return false; } } }
        public static bool Shown { get { try { return _plate != null && _plate.gameObject.activeSelf; } catch { return false; } } }

        static string KeyLabel()
        {
            string name = ""; try { name = (Plugin.AdviceLoadoutEquipKey.Value ?? "").Trim(); } catch { }
            if (!ReferenceEquals(name, _keyName))
            {
                _keyName = name; _keyValid = name.Length > 0 && Enum.TryParse(name, true, out _key);
                if (_keyValid) Plugin.NoteKeyClash("Advice", "LoadoutEquipKey", name);      // 0.13.0 (C5): once per key value
            }
            return _keyValid ? _key.ToString() : "";
        }

        public static void Hide()
        {
            try { if (_plate != null) _plate.gameObject.SetActive(false); } catch { _plate = null; }
        }

        /// <summary>The screen closed: the plate goes with it, a run stops, the UNDO is no longer offered.</summary>
        public static void Gone()
        {
            if (_running) Plugin.Logger.LogInfo("[loadout] equip: stopped - the screen closed after " + (_run != null ? _run.Done : 0) + " presses");
            if (_undo != null) Plugin.Logger.LogInfo("[loadout] equip: UNDO withdrawn - the screen closed");
            try { if (_plate != null) UnityEngine.Object.Destroy(_plate.gameObject); } catch { }
            _running = false; _run = null; _undo = null; _after = null; _plate = null; _ring = null; _caption = null; _viewPtr = IntPtr.Zero; _visit = null; _camRead = false; PlanText = "";
            _from = null; _focused = false; _focusSaid = false; _selected = false; _padFocus = false; _said.Clear();
        }

        /// <summary>The selection changed while no run was pressing: a click by hand takes the UNDO away (0.16.1: any change of the row -
        /// the UNDO puts back an order, and a reorder by hand would make it put back another).</summary>
        public static void SelectionChanged(List<int> live)
        {
            if (_running || _undo == null || _after == null) return;
            if ((live ?? new List<int>()).SequenceEqual(_after)) return;
            _undo = null; _after = null;
            Plugin.Logger.LogInfo("[loadout] equip: UNDO withdrawn - the selection was changed by hand");
        }

        static string Name(int id)
        {
            var f = LoadoutState.All == null ? null : LoadoutState.All.FirstOrDefault(x => x.Id == id);
            return f != null ? f.Short : id.ToString(IC);
        }
        static string Plan(IEnumerable<EquipStep> steps) { return string.Join(", ", steps.Select(s => (s.Add ? "add " : "remove ") + Name(s.Id))); }
        static string Ids(IList<int> l) { return l == null || l.Count == 0 ? "empty" : string.Join(",", l); }

        // ---------------------------------------------------------------- where and what
        /// <summary>From LoadoutUi.Draw: the plate to the right of the slot row when there is room, else under the summary rows,
        /// else none (said once). Shown only with the setting on and something to do (a plan, an UNDO on offer, a run going).</summary>
        public static void Place(UIViewRunSetup view, RectTransform vrt, LoadoutVisit visit, LoadoutView v, LoadoutAdvice advice, LayoutIn li, LayoutOut lo, TextMeshProUGUI template)
        {
            _visit = visit;
            if (advice != null) _advice = advice.Number;
            PlanText = v != null && v.Plan.Count > 0 ? v.Plan.Count + " click" + (v.Plan.Count == 1 ? "" : "s") + ": " + Plan(v.Plan) : "";
            if (!On || v == null || vrt == null || li == null || lo == null || template == null) { Hide(); return; }
            bool something = _running || _undo != null || v.Plan.Count > 0;
            if (!something) { Hide(); return; }
            try
            {
                IntPtr vp = view.Pointer;
                bool alive = false; try { alive = _plate != null && _plate.gameObject != null && _viewPtr == vp; } catch { }
                if (!alive) { _plate = Build(vrt, template); _viewPtr = vp; _camRead = false; if (_plate == null) return; }
                // a touch under the summary's size, never under the 15 px floor (SPEC 2.1: no text under 15 px)
                float f = Mathf.Max(Mathf.Max(lo.SumFontUnits, lo.FontUnits) * 0.95f, LoadoutLayout.MinPx / Mathf.Max(0.0001f, li.UnitPx)), h = LoadoutLayout.LineFactor * f + 24f;
                _pad = ReadPad();
                Caption(v);
                _caption.fontSize = f;
                float tw = 0f; try { _caption.ForceMeshUpdate(); tw = _caption.preferredWidth; } catch { }
                if (!(tw > 0f)) tw = f * 14f;
                float w = tw + 2f * 34f;
                var row = R4.Union(li.Slots);
                var blocked = LoadoutLayout.Blocked(li);
                if (lo.SumAt != "none") blocked.Add(new R4(lo.Sum.X0, lo.Sum.Y0, lo.Sum.X1, lo.Sum.Y0 + lo.SumRows * lo.LineUnits + 8f));
                R4 at = new R4();
                if (!row.Empty)
                {
                    var right = R4.Box(row.X1 + 40f, row.Y0 + (row.H - h) / 2f, w, h);
                    float limit = Math.Max(li.View.X1, li.Canvas.X1) - 20f;
                    if (right.X1 <= limit && !blocked.Any(b => b.Overlaps(right))) at = right;
                }
                if (at.Empty && lo.SumAt != "none")
                {
                    float y = lo.Sum.Y0 + lo.SumRows * lo.LineUnits + 16f;
                    var under = R4.Box(lo.Sum.X0, y, w, h);
                    if (under.Y1 <= lo.Sum.Y1 + 4f && !blocked.Any(b => b.Overlaps(under))) at = under;
                }
                if (at.Empty) { Hide(); LoadoutState.Once("equip:noroom", "the EQUIP ADVICE button has no room on this screen (" + li.View.Size + ") - not shown"); return; }
                var vr = vrt.rect;
                _plate.anchoredPosition = new Vector2(at.X0 - vr.width / 2f, vr.height / 2f - at.Y0);
                _plate.sizeDelta = new Vector2(at.W, at.H);
                bool was = _plate.gameObject.activeSelf;
                _plate.gameObject.SetActive(true); _plate.SetAsLastSibling();
                if (!was)
                {
                    var p = _plate;
                    Fx.Run("loadout:equip", 0.6f, 0.35f, k => { float s = 0.9f + 0.1f * Fx.OutBack(k); p.localScale = new Vector3(s, s, 1f); });
                    LoadoutState.Once("equip:shown", "the EQUIP ADVICE button is shown (" + at.Size + " units at " + at + ")");
                }
            }
            catch (Exception e) { Hide(); LoadoutState.Once("equip:fail:" + e.GetType().Name, "the EQUIP ADVICE button could not be drawn: " + e.Message, true); }
        }

        /// <summary>What presses the plate besides a click, for its caption: the pad's Y in pad mode, else the key when one is set.</summary>
        static string Binding() { if (_pad) return "Y"; return KeyLabel(); }

        static void Caption(LoadoutView v)
        {
            if (_caption == null) return;
            string key = Binding(); string dim = "<color=" + Theme.DimHex + ">";
            if (_running) _caption.text = "<b>" + (_undoing ? "UNDOING" : "EQUIPPING") + "</b>  " + dim + (_run != null ? _run.Done + " / " + _run.Steps.Count : "") + "</color>";
            else if (_undo != null && (v == null || v.Plan.Count == 0 || _after != null)) _caption.text = "<b><color=" + Theme.GoldHex + ">UNDO</color></b>  " + dim + _undo.Steps.Count + " click" + (_undo.Steps.Count == 1 ? "" : "s") + (key.Length > 0 ? " · " + key : "") + "</color>";
            else if (v != null) _caption.text = "<b><color=" + Theme.GoldHex + ">EQUIP ADVICE</color></b>  " + dim + v.Plan.Count + " click" + (v.Plan.Count == 1 ? "" : "s") + (key.Length > 0 ? " · " + key : "") + "</color>";
        }

        static RectTransform Build(RectTransform view, TextMeshProUGUI template)
        {
            var r = Ui.NewRect(PlateName, view);
            try { var le = r.gameObject.AddComponent(Il2CppType.Of<LayoutElement>()).TryCast<LayoutElement>(); if (le != null) le.ignoreLayout = true; } catch { }
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0f, 1f);
            var bg = Ui.Image(r, "Bg", Theme.Plate); Ui.Stretch(bg, 0, 0, 0, 0);
            try { bg.GetComponent<Image>().raycastTarget = true; } catch { }        // the click lands on the plate, not on a game control under it
            Ui.Frame(r, "Frame", 0f, 3f, Theme.GoldLine, 20f);
            // 0.16.1 (the 10-11 Deck report: "no easy way to nav to the auto-equip button"): a Button with the Automatic navigation every
            // button of this screen has (the level1 scene: no explicit links), so the d-pad reaches it like a game button - to the right
            // of the last filled slot; no transition (the ring below shows the pad's focus), no onClick (Frame presses it, as for a click).
            // Review 2: a click or a tap selects it too (Selectable.OnPointerDown) - Frame gives that selection up again (EquipInput.Focus)
            try
            {
                var b = r.gameObject.AddComponent(Il2CppType.Of<Button>()).TryCast<Button>();
                if (b != null)
                {
                    b.transition = Selectable.Transition.None;
                    var nav = b.navigation; nav.mode = Navigation.Mode.Automatic; b.navigation = nav;
                }
            }
            catch (Exception e) { LoadoutState.Once("equip:nonav", "the EQUIP ADVICE button is not in the pad's navigation (" + e.GetType().Name + ": " + e.Message + ") - Y still presses it"); }
            _ring = Ui.Frame(r, "Focus", -9f, 5f, Theme.HlGold, 0f);
            _ring.gameObject.SetActive(false);
            _caption = Ui.CloneText(template, r, "Caption");
            if (_caption == null) { UnityEngine.Object.Destroy(r.gameObject); _ring = null; return null; }
            Ui.Stretch(_caption.rectTransform, 30f, 4f, 30f, 4f);
            _caption.alignment = TextAlignmentOptions.Center; _caption.fontStyle = FontStyles.Normal; _caption.color = Theme.Cream;
            try { _caption.overflowMode = TextOverflowModes.Overflow; _caption.characterSpacing = 0f; } catch { }
            _focused = false; _selected = false; _padFocus = false;
            r.gameObject.SetActive(false);
            return r;
        }

        // ---------------------------------------------------------------- input and the presses (every frame while the screen is up)
        static bool Hit(Vector3 mouse)
        {
            try
            {
                if (!_camRead)
                {
                    _camRead = true; _cam = null;
                    var canvas = _plate.GetComponentInParent<Canvas>(); var root = canvas == null ? null : canvas.rootCanvas;
                    if (root != null && root.renderMode != RenderMode.ScreenSpaceOverlay) _cam = root.worldCamera;
                }
                return RectTransformUtility.RectangleContainsScreenPoint(_plate, new Vector2(mouse.x, mouse.y), _cam);
            }
            catch { return false; }
        }

        static bool ReadPad() { try { return GameMaster.Get != null && GameMaster.Get.UsesPad; } catch { return false; } }

        /// <summary>The game switched between pad and mouse mode: the caption gains or loses '· Y' and is placed again at its width.</summary>
        static void PadMode()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _nextPadRead) return;
            _nextPadRead = now + 0.25f;
            bool pad = ReadPad();
            if (pad == _pad) return;
            _pad = pad;
            LoadoutUi.Dirty();
        }

        /// <summary>A Rewired action this frame, as the screen's own Update reads its Cancel (no pause override: when the game holds its
        /// input - a fade, an unfocused window - it is not taken). A build without the call says so once.</summary>
        static bool Action(string action)
        {
            try { return GameMaster.GetButtonDown(action, false); }
            catch (Exception e) { LoadoutState.Once("equip:input:" + action, "the EQUIP ADVICE button cannot read the game's '" + action + "' action (" + e.GetType().Name + ": " + e.Message + ")", true); return false; }
        }

        /// <summary>The plate holds the event system's selection - the pad's focus, or a click's until Track gives that up.</summary>
        static bool Focused()
        {
            try
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                var go = es == null ? null : es.currentSelectedGameObject;
                return go != null && _plate != null && go.Pointer == _plate.gameObject.Pointer;
            }
            catch { return false; }
        }

        static void Focus(bool on)
        {
            if (on == _focused) return;
            _focused = on;
            try { if (_ring != null) _ring.gameObject.SetActive(on); } catch { }
            if (on && !_focusSaid) { _focusSaid = true; Plugin.Logger.LogInfo("[loadout] equip: the button has the screen's focus (" + (_pad ? "pad mode: the d-pad reached it" : "keyboard: the arrow keys reached it") + ") - A or Enter presses it"); }
        }

        /// <summary>Review 2, every frame while the plate is shown (a run included): whose focus the plate holds. Only the d-pad's (or the
        /// arrow keys') counts - for the ring, for A / Submit and for the focus line. A click or a tap selects the plate as well
        /// (Selectable.OnPointerDown); that selection is given up at once, as 0.16.0 left it (its plate had no select handler, so the
        /// input module cleared the selection on the same tap): else the next A, Enter or Space would press the plate the click
        /// pressed - EQUIP, then UNDO - without the d-pad ever having gone there.</summary>
        static void Track()
        {
            int frame = Time.frameCount;
            bool seenLast = _tickFrame == frame - 1; _tickFrame = frame;
            try { if (UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.GetMouseButton(0) || UnityEngine.Input.GetMouseButtonUp(0)) _mouseFrame = frame; } catch { }
            bool selected = Focused(), pad, release;
            EquipInput.Focus(selected, _selected, _padFocus, frame - _mouseFrame, seenLast, out pad, out release);
            if (release)
            {
                try { var es = UnityEngine.EventSystems.EventSystem.current; if (es != null) es.SetSelectedGameObject(null); selected = Focused(); } catch { }
                if (_said.Add("click focus")) Plugin.Logger.LogInfo("[loadout] equip: a click selected the button - given up again, as in 0.16.0 (" + (selected ? "NOT cleared" : "cleared") + "): A, Enter or Space press it only after the d-pad or the arrow keys bring the focus there");
            }
            _selected = selected; _padFocus = pad;
            Focus(pad);
        }

        // the game's own views whose Update reads TabToggle (machine code of 1.0.2): while one of them is live, Y acts there as well
        [MethodImpl(MethodImplOptions.NoInlining)] static bool MainInteractive(UIViewMainMenu v) { return v._isInteractive; }
        static readonly KeyValuePair<string, Func<UIMainMenu, Behaviour>>[] Readers =
        {
            new KeyValuePair<string, Func<UIMainMenu, Behaviour>>("main menu", m => { var v = m._uiViewMainMenu; if (v == null) return null; bool live = true; try { live = MainInteractive(v); } catch { } return live ? v : null; }),
            new KeyValuePair<string, Func<UIMainMenu, Behaviour>>("hero select", m => m._uiViewChooseHero),
            new KeyValuePair<string, Func<UIMainMenu, Behaviour>>("arena select", m => m._uiViewChooseArena),
            new KeyValuePair<string, Func<UIMainMenu, Behaviour>>("item pool", m => m._uiViewItemsPoolMainMenu),
            new KeyValuePair<string, Func<UIMainMenu, Behaviour>>("run history", m => m._uiViewRunHistory),
            new KeyValuePair<string, Func<UIMainMenu, Behaviour>>("save profile", m => m._uiViewSaveProfile),
            new KeyValuePair<string, Func<UIMainMenu, Behaviour>>("camp hub", m => m._uiViewGameHub),
        };

        /// <summary>The first of the game's other TabToggle views that is live now ("" = none). A member a game patch took away is skipped.</summary>
        static string OtherReader()
        {
            UIMainMenu m = null; try { m = UIMainMenu.s_instance; } catch { }
            if (m == null) return "";
            foreach (var r in Readers) { try { var b = r.Value(m); if (b != null && b.isActiveAndEnabled) return r.Key; } catch { } }
            return "";
        }

        static void Refused(string source, string why)
        {
            if (_said.Add(source + "|" + why)) Plugin.Logger.LogInfo("[loadout] equip: a press by " + source + " not taken - " + why);
        }

        /// <summary>From LoadoutUi.Tick, every frame: a click on the plate, the key, the pad's Y or A on the plate while the pad's focus is
        /// there starts a run; a run presses every 0.12 s.</summary>
        public static void Frame(UIViewRunSetup view, LoadoutVisit visit, LoadoutView v)
        {
            if (Shown) Track(); else { _selected = false; _padFocus = false; Focus(false); }
            if (_running) { if (Time.realtimeSinceStartup >= _nextPress) PressNext(view); return; }
            if (!Shown || !On) { Focus(false); return; }
            PadMode();
            // 0.16.0 (C16-11e, DK-C11): how it was pressed goes into the pressed line - a click on the plate is 'touch' or 'mouse' as the
            // menu tells them apart (Menu.Pointer: Input.touchCount), the key is 'key'; --check-log's 'equip' kind reads it. 0.16.1: the
            // pad's Y (TabToggle) and A on the focused plate are 'pad'. Review 2 (EquipInput.Decide): TabToggle from the keyboard (T on the
            // PC's map) is refused - the keyboard's key is [Advice] LoadoutEquipKey alone (C5); A / Submit only on the pad's focus (Track),
            // 'key' for Enter / Space after the arrow keys; never on a mouse-click frame (the game's UISubmit fires on the left button too)
            bool click = false, onPlate = false, cfgKey = false, tab = false, submit = false;
            try { click = UnityEngine.Input.GetMouseButtonDown(0); onPlate = click && Hit(UnityEngine.Input.mousePosition); } catch { }
            if (!onPlate && KeyLabel().Length > 0) { try { cfgKey = UnityEngine.Input.GetKeyDown(_key); } catch { } }
            if (!onPlate && !cfgKey) tab = Action(PadAction);
            if (!onPlate && !cfgKey && !tab && _padFocus && !click) submit = Action("UISubmit");
            if (!onPlate && !cfgKey && !tab && !submit) return;
            var press = EquipInput.Decide(onPlate, onPlate ? Menu.Pointer() : "", click, cfgKey, tab, submit, (tab || submit) && Menu.KeyboardHeld(), _padFocus);
            if (visit == null || v == null || Menu.Blocking) return;
            if (!press.Go) { if (press.Refused.Length > 0) Refused(press.RefusedBy, press.Refused); return; }
            string source = press.Source, how = press.How;
            bool canInteract = true; try { canInteract = LoadoutState.CanInteract(view); } catch { }
            if (!canInteract) { Refused(source, "the screen takes no input right now (it flies in or out)"); return; }
            if (how.Contains("TabToggle")) { string other = OtherReader(); if (other.Length > 0) { Refused(source, "the game's " + other + " view is live and reads TabToggle (Y) too"); return; } }
            if (_undo != null && (v.Plan.Count == 0 || _after != null)) Start(view, _undo, true, source, how, v);
            else if (v.Plan.Count > 0)
            {
                Func<int, bool> unlocked = id => { int l; return visit.Levels.TryGetValue(id, out l) && l >= 1; };
                Start(view, LoadoutView.RunOf(v, unlocked), false, source, how, v);
            }
        }

        static void Start(UIViewRunSetup view, EquipRun run, bool undo, string source, string how, LoadoutView v)
        {
            _run = run; _undoing = undo; _running = run.Steps.Count > 0; _nextPress = Time.realtimeSinceStartup;
            if (!undo) _from = v;
            var planned = LoadoutView.Apply(run.Start, run.Steps, run.Forced, run.Unlocked, run.Slots);
            string reads = undo ? "the row before the EQUIP" : "slots " + (_from != null ? _from.SlotsText(planned) : "?");
            Plugin.Logger.LogInfo("[loadout] equip: " + (undo ? "UNDO" : "EQUIP ADVICE (advice #" + _advice + ")") + " pressed by " + (string.IsNullOrEmpty(source) ? "mouse" : source) + " - " + Plan(run.Steps) + " = " + run.Steps.Count
                + " click" + (run.Steps.Count == 1 ? "" : "s") + " through the game's own badge button, " + Gap.ToString("0.00", IC) + " s apart; selection before " + Ids(run.Start) + " -> " + Ids(planned) + " (" + reads + ")"
                + (string.IsNullOrEmpty(how) ? "" : "; " + how));
            if (undo) _undo = null;
            Caption(null);
            if (!_running) Finish(view);
        }

        static void PressNext(UIViewRunSetup view)
        {
            try
            {
                var live = LoadoutState.LiveSelection(view);
                if (live == null) { _run.Stopped = "the selection is unreadable"; Finish(view); return; }
                var step = _run.Next(live);
                if (step == null) { Finish(view); return; }
                var cell = _visit == null ? null : _visit.CellOf(step.Id);
                string why;
                if (!GuardedClick(view, cell, step, live, out why)) { _run.Stopped = "press " + (_run.Done + 1) + " refused: " + why; Finish(view); return; }
                var after = LoadoutState.LiveSelection(view) ?? new List<int>();
                _run.Pressed(after);
                Plugin.Logger.LogInfo("[loadout] equip: press " + _run.Done + " of " + _run.Steps.Count + ": " + (step.Add ? "add " : "remove ") + Name(step.Id) + " -> selection " + (after.Count > 0 ? string.Join(",", after) : "empty"));
                LoadoutUi.Dirty();
                _nextPress = Time.realtimeSinceStartup + Gap;
                Caption(null);
                if (!_run.Running) Finish(view);
            }
            catch (Exception e) { if (_run != null) _run.Stopped = "the press failed: " + e.GetBaseException().Message; Finish(view); }
        }

        /// <summary>What the selection reads after a run: for an EQUIP the numbers slot by slot and whether they rise in the advice's order;
        /// for an UNDO whether the old row is back.</summary>
        static string Reads(EquipRun run, List<int> live)
        {
            if (_undoing)
            {
                var row = LoadoutView.Apply(run.Start, run.Steps, run.Forced, run.Unlocked, run.Slots);
                return live.SequenceEqual(row) ? "the row before the EQUIP" : "NOT the row before the EQUIP (" + Ids(row) + ")";
            }
            if (_from == null) return "slots ?";
            return "slots " + _from.SlotsText(live) + ", " + (_from.InOrder(live) ? "the advice's order" : "NOT the advice's order");
        }

        static void Finish(UIViewRunSetup view)
        {
            _running = false;
            var run = _run; if (run == null) return;
            var read = LoadoutState.LiveSelection(view);
            var live = read ?? new List<int>();
            if (run.Finished) Plugin.Logger.LogInfo("[loadout] equip: " + (_undoing ? "undone" : "done") + " - " + run.Done + " press" + (run.Done == 1 ? "" : "es") + ", the selection is now " + Ids(live) + " = " + Reads(run, live));
            else Plugin.Logger.LogInfo("[loadout] equip: stopped after " + run.Done + " of " + run.Steps.Count + " presses - " + (run.Stopped.Length > 0 ? run.Stopped : "nothing left to press") + "; the selection is " + Ids(live));
            // the UNDO starts from what the game shows (a click by hand that stopped the run included), else from what the presses made
            if (!_undoing && run.Done > 0) { _undo = run.Undo(read); _after = live; }
            else if (_undoing) { _undo = null; _after = null; }
            _run = null;
            LoadoutUi.Dirty();
            Caption(null);
        }

        // The only call in Loadout*.cs that writes the profile: the game's own badge button, exactly as a click by hand - after
        // every guard has passed against live state.
        static bool GuardedClick(UIViewRunSetup view, LoadoutCell cell, EquipStep step, List<int> live, out string why)
        {
            why = "";
            if (!On) { why = "[Advice] LoadoutEquip is off"; return false; }
            if (Menu.IsOpen) { why = "the mod menu is open"; return false; }
            if (view == null || !view.gameObject.activeInHierarchy) { why = "the screen closed"; return false; }
            if (!LoadoutState.CanInteract(view)) { why = "the screen takes no input right now"; return false; }
            if (cell == null || cell.Button == null) { why = "badge " + step.Id + " has no button on the grid"; return false; }
            var badge = LoadoutState.BadgeOf(cell.Button);
            var facts = LoadoutState.FactsOf(badge);
            if (facts == null || facts.Id != step.Id) { why = "the button no longer holds badge " + step.Id; return false; }
            if (step.Add)
            {
                if (live.Contains(step.Id)) { why = Name(step.Id) + " is equipped already"; return false; }
                if (!cell.Button.gameObject.activeInHierarchy) { why = "its button is hidden"; return false; }
                if (LoadoutState.IsLocked(cell.Button)) { why = Name(step.Id) + " is locked"; return false; }
                if (!LoadoutState.Interactable(cell.Button)) { why = "its button takes no input"; return false; }
                if (LoadoutState.GameLevel(badge) < 1) { why = Name(step.Id) + " is not unlocked"; return false; }
                if (!LoadoutState.CanSelectMore(view)) { why = "no free badge slot"; return false; }
            }
            else
            {
                if (!live.Contains(step.Id)) { why = Name(step.Id) + " is not equipped"; return false; }
                if (LoadoutState.IsForcedBadge(view, badge)) { why = Name(step.Id) + " is forced by the quest"; return false; }
            }
            view.OnClickBadge(cell.Button);
            return true;
        }
    }
}
