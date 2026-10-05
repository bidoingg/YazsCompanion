// The one-click EQUIP ADVICE button on the run setup screen (0.13.0, the user's decision OD1 = (b): built, OFF by default,
// switched on from the mod menu's BADGES page: [Advice] LoadoutEquip). A plate next to CHOSEN BADGES says what it would do
// ("EQUIP ADVICE  6 clicks", "· <key>" when [Advice] LoadoutEquipKey names one - none by default); a click on it (mouse, Deck
// touch) or the key presses the game's OWN badge button along the
// pure plan of LoadoutView (removes first, then adds; never a forced badge, never a locked one, never past the slots) - one
// press every 0.12 s, each exactly what a click by hand does: the game plays its sound, writes RunSetup_Badge1..4 into the
// profile and refreshes the slots. Every press is logged. Afterwards the plate offers UNDO (the inverse plan, under the same
// guards) until the screen closes or the player clicks a badge by hand.
//
// The guards are checked again, against live state, before EVERY press (GuardedClick): the screen is up and takes input, the
// mod menu is closed, the setting is on, the button still holds the badge the step names; for an add the badge is unlocked
// (no padlock, the game's own level >= 1, the button interactable) and the game has a free slot; for a remove the badge is
// selected and not forced; and the selection is what the presses so far lead to (EquipRun.Next) - a click by hand in between
// stops the run. GuardedClick is the only place in Loadout*.cs that may call the game's OnClickBadge (the bench's safety scan
// fails otherwise). The pad route (a cloned game button in the screen's navigation) comes later.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        static RectTransform _plate; static TextMeshProUGUI _caption; static IntPtr _viewPtr;
        static EquipRun _run, _undo; static bool _running, _undoing; static float _nextPress;
        static List<int> _after;                 // the selection the last run left: UNDO is offered while it stays so
        static LoadoutVisit _visit; static int _advice;
        static Camera _cam; static bool _camRead;
        static string _keyName; static KeyCode _key; static bool _keyValid;
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
            _running = false; _run = null; _undo = null; _after = null; _plate = null; _caption = null; _viewPtr = IntPtr.Zero; _visit = null; _camRead = false; PlanText = "";
        }

        /// <summary>The selection changed while no run was pressing: a click by hand takes the UNDO away.</summary>
        public static void SelectionChanged(List<int> live)
        {
            if (_running || _undo == null || _after == null) return;
            if (new HashSet<int>(live ?? new List<int>()).SetEquals(_after)) return;
            _undo = null; _after = null;
            Plugin.Logger.LogInfo("[loadout] equip: UNDO withdrawn - the selection was changed by hand");
        }

        static string Name(int id)
        {
            var f = LoadoutState.All == null ? null : LoadoutState.All.FirstOrDefault(x => x.Id == id);
            return f != null ? f.Short : id.ToString(IC);
        }
        static string Plan(IEnumerable<EquipStep> steps) { return string.Join(", ", steps.Select(s => (s.Add ? "add " : "remove ") + Name(s.Id))); }

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

        static void Caption(LoadoutView v)
        {
            if (_caption == null) return;
            string key = KeyLabel(); string dim = "<color=" + Theme.DimHex + ">";
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
            _caption = Ui.CloneText(template, r, "Caption");
            if (_caption == null) { UnityEngine.Object.Destroy(r.gameObject); return null; }
            Ui.Stretch(_caption.rectTransform, 30f, 4f, 30f, 4f);
            _caption.alignment = TextAlignmentOptions.Center; _caption.fontStyle = FontStyles.Normal; _caption.color = Theme.Cream;
            try { _caption.overflowMode = TextOverflowModes.Overflow; _caption.characterSpacing = 0f; } catch { }
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

        /// <summary>From LoadoutUi.Tick, every frame: a click on the plate or the key starts a run; a run presses every 0.12 s.</summary>
        public static void Frame(UIViewRunSetup view, LoadoutVisit visit, LoadoutView v)
        {
            if (_running) { if (Time.realtimeSinceStartup >= _nextPress) PressNext(view); return; }
            if (!Shown || !On) return;
            bool go = false;
            try { if (UnityEngine.Input.GetMouseButtonDown(0) && Hit(UnityEngine.Input.mousePosition)) go = true; } catch { }
            if (!go && KeyLabel().Length > 0) { try { go = UnityEngine.Input.GetKeyDown(_key); } catch { } }
            if (!go || visit == null || v == null || Menu.IsOpen) return;
            if (_undo != null && (v.Plan.Count == 0 || _after != null)) Start(view, _undo, true, "undo");
            else if (v.Plan.Count > 0)
            {
                Func<int, bool> unlocked = id => { int l; return visit.Levels.TryGetValue(id, out l) && l >= 1; };
                Start(view, LoadoutView.RunOf(v, unlocked), false, "equip");
            }
        }

        static void Start(UIViewRunSetup view, EquipRun run, bool undo, string why)
        {
            _run = run; _undoing = undo; _running = run.Steps.Count > 0; _nextPress = Time.realtimeSinceStartup;
            Plugin.Logger.LogInfo("[loadout] equip: " + (undo ? "UNDO" : "EQUIP ADVICE (advice #" + _advice + ")") + " pressed - " + Plan(run.Steps) + " = " + run.Steps.Count
                + " click" + (run.Steps.Count == 1 ? "" : "s") + " through the game's own badge button, " + Gap.ToString("0.00", IC) + " s apart; selection before " + string.Join(",", run.Start));
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

        static void Finish(UIViewRunSetup view)
        {
            _running = false;
            var run = _run; if (run == null) return;
            var live = LoadoutState.LiveSelection(view) ?? new List<int>();
            if (run.Finished) Plugin.Logger.LogInfo("[loadout] equip: " + (_undoing ? "undone" : "done") + " - " + run.Done + " press" + (run.Done == 1 ? "" : "es") + ", the selection is now " + (live.Count > 0 ? string.Join(",", live) : "empty"));
            else Plugin.Logger.LogInfo("[loadout] equip: stopped after " + run.Done + " of " + run.Steps.Count + " presses - " + (run.Stopped.Length > 0 ? run.Stopped : "nothing left to press") + "; the selection is " + (live.Count > 0 ? string.Join(",", live) : "empty"));
            if (!_undoing && run.Done > 0) { _undo = run.Undo(); _after = live; }
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
