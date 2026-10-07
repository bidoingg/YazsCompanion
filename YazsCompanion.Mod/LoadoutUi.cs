// Badge advice on the run setup screen ("SELECT LOADOUT", 0.13.0): which unlocked badges to equip for the team leader, the
// build, the mode, the difficulty and the doctrine the player picked - drawn in the game's own style on the game's own
// buttons (SPEC 2.1):
//   - a numbered gold diamond on the top-right corner of every advised badge (1 = most worth it; the game's green "selected"
//     diamond hangs under the bottom centre, so nothing goes there), plus a gold frame with corner diamonds on the advised
//     badges that are not equipped yet; a small hollow rust diamond on an equipped badge the advice would swap out; a hollow
//     numbered diamond on an equipped badge kept because the swap would gain too little (KEEP · close); Q on a quest's forced
//     badge; the same diamonds on the CHOSEN BADGES slots;
//   - the WHY line - the reason for the badge the game's info panel shows, following OnBadgeHighlight (mouse and pad) - in the
//     first free band of grid gap / under the info / over the name (LoadoutLayout.Choose), else one line appended to the
//     game's bonus label;
//   - the summary rows under CHOSEN BADGES: EQUIP 1 Gunner · 2 Critical ..., SWAP OUT ... (and in Full the YARD / UNLOCK hint);
//   - optionally (OD1, off by default) the EQUIP ADVICE button of LoadoutEquip.cs.
// Everything here only reads the game and draws (LoadoutState.cs is the reader, Loadout.cs / LoadoutView.cs / LoadoutLayout.cs
// the pure rules the offline bench checks); every [loadout] line can be replayed with ItemBench --replay-loadout.
//
// Ticked from UIViewRunSetup.Update (throttled to 0.25 s, sooner after a selection change or a highlight), with a fallback
// from GameMaster.Update that reads UIMainMenu's run setup field (no search) when the hook stays silent. A degradation ladder
// (A all / B no WHY / C summary only / D log only / E nothing) is named in every 'drawn' line; a circuit breaker turns the
// feature off for the session after 3 errors in one visit. The once-a-run '[loadout] run start:' line and the [ctx] suffix
// come from the same fallback tick. [Debug] PreviewSetup's stage (WalkTick) takes the screenshots and moves the game's own
// cursor with OnBadgeHighlight - it never clicks a badge.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YazsCompanion
{
    /// <summary>The last run setup advice of this session (the BADGES page, the run start line).</summary>
    internal sealed class LoadoutLast
    {
        public string Leader = "", Mode = "Normal";
        public int Difficulty = 1, Slots = 4, Number;
        public readonly List<int> Equipped = new List<int>(), Forced = new List<int>();
        public LoadoutAdvice Advice;
    }

    internal static class LoadoutUi
    {
        const string MarkName = "YazsLoadoutMark", FrameName = "YazsLoadoutFrame", WhyName = "YazsLoadoutWhy", SumName = "YazsLoadoutSummary";
        const float Throttle = 0.25f, EntranceDelay = 0.35f, Tip = 30f;
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        // ---- state of the visit ----
        static LoadoutVisit _visit; static int _visits, _number;
        static ulong _sig; static bool _dirty, _off, _gone = true;
        static float _next, _lastTick = -10f, _hookAt = -10f, _firstDrawAt, _nextKey, _fallbackSeen = -1f, _nextFallback;
        static string _adviceKey = "", _cfgKey = "", _equippedLine = "", _drawnLine = "", _visitLine = "";
        static LoadoutAdvice _advice; static LoadoutView _view;
        static bool _drawnThisVisit, _matched, _inlineSaid;
        static int _errors; static readonly HashSet<string> _errSaid = new HashSet<string>();
        static readonly HashSet<string> _termsSaid = new HashSet<string>(), _layoutSaid = new HashSet<string>(), _pinsSaid = new HashSet<string>();
        static bool _knowledgeSaid, _raisedSaid, _fallbackSaid;
        public static LoadoutLast Last;

        // ---- the drawn pieces ----
        sealed class Placed { public UiMarker Marker; public RectTransform Frame; public IntPtr Badge; public string State = ""; }
        static readonly Dictionary<IntPtr, Placed> _placed = new Dictionary<IntPtr, Placed>();      // by button pointer
        static RectTransform _why, _sum; static TextMeshProUGUI _why1, _why2; static readonly List<TextMeshProUGUI> _rows = new List<TextMeshProUGUI>();
        static TextMeshProUGUI _template, _numTemplate, _bonus;
        static string _sep = "·";
        // the WHY line's template and the info labels' measurement: the game fills its info labels only when a badge is first
        // highlighted, so the first measure (0.35 s after the entrance) finds them empty; they are measured once more on the
        // first highlight of the visit (one frame later, when the game's text has its mesh) - three field reads, no search
        static string _templateFrom = "", _whyFrom = "", _sumFrom = "", _labelNote = "";
        static bool _labelsWanted, _labelsPending, _whySaid, _templateSearched; static int _labelsFrame = -9;
        static LayoutIn _li; static LayoutOut _lo; static bool _measured; static int _measureTries; static float _unitPx = 1f, _markerLocal = 57f, _slotMarkerLocal = 57f;

        // ---- the cursor ----
        static int _hlId = -1, _hlFrame = -9; static IntPtr _hlPtr, _lastSel;
        static int _srcHook, _srcButton, _srcPoll;

        static LoadoutDetail Detail { get { try { return Plugin.AdviceLoadout.Value; } catch { return LoadoutDetail.NumbersAndReason; } } }
        static float Size { get { try { return Mathf.Clamp(Plugin.LoadoutSize.Value, 0.7f, 2f); } catch { return 1f; } } }
        static bool Verbose { get { try { return Plugin.Verbose.Value; } catch { return false; } } }

        // ================================================================================================ entry points
        /// <summary>From the UIViewRunSetup.Update post-fix (hook = true) or the fallback tick: once a frame while the screen is up.</summary>
        public static void Tick(UIViewRunSetup view, bool hook)
        {
            if (view == null) return;
            long perf = Perf.Begin();
            try
            {
                float now = Time.realtimeSinceStartup;
                if (hook) _hookAt = now;
                // no tick for a while = the view was disabled (its OnDisable hook may be missing): a new visit, read afresh
                if (!_gone && _visit != null && now - _lastTick > 1.5f) Gone();
                _lastTick = now;
                if (_off) return;
                if (_walk != null) WalkTick(view, now);
                LoadoutEquip.Frame(view, _visit, _view);
                FollowSlots();              // 0.15.x (C-m5): the slot markers' stand-ins over the slot row follow their slots
                if (_labelsPending && Time.frameCount > _labelsFrame) Relabel(view);
                if (now < _next && !_dirty) return;
                _next = now + Throttle;
                Step(view, now, hook);
            }
            catch (Exception e) { Fail("tick", e); }
            finally { Perf.End("tick.setup", perf); }
        }

        /// <summary>From the GameMaster.Update post-fix (every scene): the run start line, and the screen's tick when its own
        /// Update hook has been silent for half a second (UIMainMenu's run setup field: filled once the screen was opened, no search).</summary>
        public static void FallbackTick()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _nextFallback) return;
            _nextFallback = now + Throttle;
            try { RunStartTick(now); } catch { }
            if (_off || now - _hookAt < 0.5f) { _fallbackSeen = -1f; return; }
            UIViewRunSetup view = null; bool active = false;
            try { view = LoadoutState.SetupView(); active = view != null && view.gameObject.activeInHierarchy; } catch { view = null; }
            if (view == null || !active)
            {
                _fallbackSeen = -1f;
                if (!_gone && _visit != null) Gone();
                return;
            }
            if (_fallbackSeen < 0f) { _fallbackSeen = now; return; }       // its own hook may simply not have come yet
            if (now - _fallbackSeen < 0.5f) return;
            if (!_fallbackSaid) { _fallbackSaid = true; Plugin.Logger.LogInfo("[loadout] the run setup screen is ticked from GameMaster.Update (its own Update hook is silent)"); }
            _next = 0f;
            Tick(view, false);
        }

        /// <summary>From the RefreshSelectedBadgeButtons post-fix: the selection changed (a flag only; the tick reads it).</summary>
        public static void Dirty() { _dirty = true; }

        /// <summary>From the OnDisable post-fix: the screen closed - everything hides, the button-to-badge mapping is dropped.</summary>
        public static void Gone(UIViewRunSetup view)
        {
            try { if (_visit != null && view != null && view.Pointer != _visit.ViewPtr) return; } catch { }
            Gone();
        }

        static void Gone()
        {
            if (_gone) return;
            _gone = true;
            try
            {
                Fx.Cancel("loadout:");
                DestroyAll();
                LoadoutEquip.Gone();
                if (_visit != null) Plugin.Logger.LogInfo("[loadout] " + CursorLine());
            }
            catch { }
            _visit = null; _placed.Clear(); _why = null; _sum = null; _why1 = null; _why2 = null; _rows.Clear(); _measured = false; _hlId = -1; _hlPtr = IntPtr.Zero;
            _labelsWanted = false; _labelsPending = false;
        }

        static string CursorLine()
        {
            return "cursor sources this visit: OnBadgeHighlight " + _srcHook + ", button OnHighlight " + _srcButton + ", poll " + _srcPoll;
        }

        /// <summary>From the OnBadgeHighlight post-fix (after the game's own SetText) and the button's OnHighlight post-fix: the
        /// badge the game's info panel shows now.</summary>
        public static void Highlighted(UIViewRunSetupBadgeButton button, int source)
        {
            if (_off || _visit == null || button == null) return;
            try
            {
                IntPtr p = button.Pointer; int frame = Time.frameCount;
                if (p == _hlPtr && frame - _hlFrame <= 1) { if (source == 0 && _lo != null && _lo.WhyAt == "inline") Inline(); return; }     // the other hook in the same frame
                var cell = _visit.Grid.FirstOrDefault(c => c.ButtonPtr == p) ?? _visit.SlotCells.FirstOrDefault(c => c.ButtonPtr == p);
                if (cell == null) return;           // the same button class on another screen (the team panel)
                if (source == 0) _srcHook++; else if (source == 1) _srcButton++; else _srcPoll++;
                _hlPtr = p; _hlFrame = frame;
                // the first highlight of the visit fills the game's info labels: measure them again on the next tick (a frame
                // later - the text set just now has no mesh yet), once per visit
                if (_measured && _labelsWanted && !_labelsPending) { _labelsPending = true; _labelsFrame = frame; }
                int id = cell.Slot ? SlotBadge(cell) : cell.Id;
                if (id < 0) return;
                bool changed = id != _hlId;
                _hlId = id;
                ShowWhy(changed);
                if (Verbose && _view != null && changed)
                {
                    var m = _view.MarkOf(id); var f = LoadoutState.All == null ? null : LoadoutState.All.FirstOrDefault(x => x.Id == id);
                    if (m != null) Plugin.Logger.LogInfo("[loadout] highlight #" + _number + " " + id + " " + (f != null ? f.Short : "?") + ": " + LoadoutView.Visible(m.Why1) + (m.Why2.Length > 0 ? " - " + m.Why2 : ""));
                }
            }
            catch (Exception e) { Fail("highlight", e); }
        }

        /// <summary>From the HUD's OnDestroy: the run is over - the next one writes its own run start line.</summary>
        public static void RunGone() { _runArmed = true; _ctxSuffix = null; _runMaster = IntPtr.Zero; }

        // ================================================================================================ the step
        static void Step(UIViewRunSetup view, float now, bool hook)
        {
            bool fresh = _gone || _visit == null || _visit.ViewPtr != view.Pointer;
            if (fresh) StartVisit(view, now, hook);
            if (_visit == null) return;
            var detail = Detail; float size = Size;
            ulong sig = LoadoutState.Signature(view);
            bool changed = fresh || _dirty || sig != _sig;
            _dirty = false;
            string ck = detail + "|" + size.ToString("0.00", IC) + "|" + LoadoutEquip.On + "|" + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height;
            bool cfg = ck != _cfgKey; _cfgKey = ck;
            if (cfg && !fresh) _measured = false;                          // a new screen size or a new text size: measure again
            if (changed)
            {
                _sig = sig;
                LoadoutState.ReadSelection(_visit);
                LoadoutState.ReadRun(_visit);
                if (!fresh) LoadoutEquip.SelectionChanged(_visit.Equipped);
            }
            bool advised = false;
            if (changed || cfg || now >= _nextKey)
            {
                _nextKey = now + 1f;
                string key = AdviceKey();
                if (key != _adviceKey) { _adviceKey = key; Advise(view); advised = true; }
            }
            if (_advice == null) return;
            if (changed || cfg || advised || _view == null) MakeView(detail);
            if (now < _firstDrawAt) { _next = Mathf.Min(_next, _firstDrawAt); return; }      // the view is still flying in: the first draw waits for it
            if (changed || cfg || advised || !_drawnThisVisit || !_measured) Draw(view, detail);
            PollCursor();
        }

        static void StartVisit(UIViewRunSetup view, float now, bool hook)
        {
            if (!_gone && _visit != null) Gone();
            _gone = false; _visits++; _errors = 0;
            _visit = LoadoutState.BeginVisit(view, _visits);
            _sig = 0; _adviceKey = ""; _advice = null; _view = null; _equippedLine = ""; _drawnLine = ""; _visitLine = "";
            _drawnThisVisit = false; _matched = false; _inlineSaid = false;
            _measured = false; _measureTries = 0; _hlId = -1; _hlPtr = IntPtr.Zero; _lastSel = IntPtr.Zero;
            _lo = null; _li = null;          // the last visit's layout must not steer this one's WHY (an "inline" layout writes into the game's label)
            _srcHook = _srcButton = _srcPoll = 0;
            _firstDrawAt = now + EntranceDelay;
            _placed.Clear(); _why = null; _sum = null; _why1 = null; _why2 = null; _rows.Clear();
            _slotLayer = null; _slotHosts.Clear(); _followed.Clear(); _followWrites = _followFrames = 0; _sumNote = "";
            _visitSinceRun = true; _via = hook ? "hook" : "fallback";
            _labelsWanted = false; _labelsPending = false; _labelsFrame = -9; _whySaid = false; _templateSearched = false;
            _whyFrom = ""; _sumFrom = ""; _labelNote = "";
            // the WHY / summary template: the game's description label (it is a reference whether or not the panel shows text yet;
            // a clone is set active by Ui.CloneText), else the name label, else any label of the view (SPEC 6.2)
            LoadoutState.Labels(view, out _numTemplate, out _template, out _bonus);
            _templateFrom = "currentBadgeDescription";
            if (!Usable(_template)) { _template = Usable(_numTemplate) ? _numTemplate : null; _templateFrom = "currentBadgeName"; }
            if (_template == null) { _templateSearched = true; _template = Ui.FindLabel(view.transform, null); _templateFrom = Usable(_template) ? "a label of the view (" + Safe(() => _template.name) + ")" : ""; if (!Usable(_template)) _template = null; }
            if (!Usable(_numTemplate)) _numTemplate = _template;
            _sep = "·";
            try { var font = _template != null ? _template.font : null; if (font != null && !font.HasCharacter('·', true, true)) _sep = "-"; } catch { }
            if (_visit.Raised > 0 && !_raisedSaid)
            {
                _raisedSaid = true;
                Plugin.Logger.LogInfo("[loadout] levels: " + _visit.Raised + " badge" + (_visit.Raised == 1 ? "" : "s") + " read above their tree node (Mushroom Mushroom still active from the last run?) - the advice uses the tree's levels");
            }
        }
        static string _via = "hook";

        // what the advice depends on: a change of any of these is a new advice (#N); a badge click is not
        static string AdviceKey()
        {
            var v = _visit; var b = string.IsNullOrEmpty(v.Leader) ? null : Builds.For(v.Leader); var d = Doctrine.Current;
            var sb = new System.Text.StringBuilder();
            sb.Append(v.Leader).Append('|').Append(v.Mode).Append('|').Append(v.Difficulty).Append('|').Append(v.Slots).Append('|').Append(string.Join(",", v.Forced));
            sb.Append('|').Append(b == null ? "auto" : b.Id + "/" + b.Branch + "/" + b.Style + "/" + string.Join(",", b.Abilities) + "/" + string.Join(",", b.Skip) + "/" + string.Join(",", b.Wants) + "/" + string.Join(",", b.Badges) + "/" + string.Join(",", b.SkipBadges) + "/" + string.Join(",", b.Evolution.Select(kv => kv.Key + "=" + kv.Value)));
            sb.Append('|').Append(d.Farming).Append(d.Caution).Append(d.Timing).Append(d.ModeAware).Append(d.TagPlan).Append(d.Style);
            sb.Append('|').Append(v.Number).Append('|').Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Knowledge.Current));
            return sb.ToString();
        }

        // ================================================================================================ the advice and its lines
        static void Advise(UIViewRunSetup view)
        {
            var v = _visit;
            if (LoadoutState.All == null) { Once("noreg", "no badge readable on this screen - nothing advised (level E)"); return; }
            long perf = Perf.Begin();
            var build = string.IsNullOrEmpty(v.Leader) ? null : Builds.For(v.Leader);
            var k = Knowledge.Current;
            var inp = LoadoutState.Input(v, build);
            _number++;
            var a = Loadout.Recommend(inp, LoadoutState.All, k, _number);
            Perf.End("loadout.advise", perf);
            _advice = a;
            Last = new LoadoutLast { Leader = v.Leader, Mode = v.Mode, Difficulty = v.Difficulty, Slots = v.Slots, Number = _number, Advice = a };
            Last.Equipped.AddRange(v.Equipped); Last.Forced.AddRange(v.Forced);

            if (!_knowledgeSaid && !Loadout.KnowledgeIsDefault(k)) { _knowledgeSaid = true; Plugin.Logger.LogInfo("[loadout] knowledge " + Loadout.KnowledgeJson(k)); }
            if (build != null && (build.Badges.Count > 0 || build.SkipBadges.Count > 0)) PinsLine(build);
            string bname = build == null ? "Auto" : "\"" + build.Name + "\" (" + (build.Custom ? "your own" : build.Pack.Length > 0 ? "pack " + build.Pack : "preset") + ")";
            string Ids(List<int> ids) { return ids.Count == 0 ? "none" : string.Join(", ", ids.Select(id => { var r = a.RowOf(id); return id + " " + (r != null ? r.Badge.Short : "?"); })); }
            string line = "visit " + v.Number + " via " + _via + ": leader " + (v.Leader.Length > 0 ? v.Leader : "?") + " (" + v.LeaderFrom + ") | " + v.Mode + " d" + v.Difficulty
                + (v.ModeFrom != "bar" ? " (" + v.ModeFrom + ")" : "") + " | slots " + v.Slots + " (game " + (v.GameSlots >= 0 ? v.GameSlots.ToString(IC) : "?") + ", slot buttons " + v.SlotButtons
                + (v.BaseSlots >= 0 ? ", base " + v.BaseSlots : "") + ")" + (v.SlotsNote.Length > 0 ? " " + v.SlotsNote : "") + " | equipped " + Ids(v.Equipped) + " | forced " + Ids(v.Forced)
                + (v.ForcedFrom != "view" && v.ForcedFrom.Length > 0 ? " (" + v.ForcedFrom + ")" : "") + " | quest " + v.Quest + " | unlocked " + v.Unlocked + " of " + LoadoutState.All.Count
                + " (" + v.Grid.Count + " on the grid) | build " + bname + " | pins " + (build != null && build.Badges.Count > 0 ? string.Join(", ", build.Badges) : "-")
                + " | skips " + (build != null && build.SkipBadges.Count > 0 ? string.Join(", ", build.SkipBadges) : "-") + (v.Missing.Count > 0 ? " | fallbacks: " + string.Join("; ", v.Missing) : "");
            if (line != _visitLine) { _visitLine = line; Plugin.Logger.LogInfo("[loadout] " + line); }
            // a developer's note (0.14.0, B6: only with [Logging] Verbose - most quests have no badge objective)
            if (Verbose && v.Quest != "-" && v.Quest != "?" && v.Forced.Count == 0) Once("questnoforced:" + v.Quest, "an active quest (" + v.Quest + ") and no forced badge on the screen: the quest's badge objective, if any, is not read (worth a look)");
            Plugin.Logger.LogInfo("[loadout] " + Loadout.ShapeLine(a));
            Plugin.Logger.LogInfo("[loadout] " + Loadout.AdviseLine(a));
            foreach (var l in Loadout.WhyLines(a)) Plugin.Logger.LogInfo("[loadout] " + l);
            string tk = v.Leader + "|" + v.Mode + "|" + v.Difficulty;
            if (Verbose || _termsSaid.Add(tk)) foreach (var l in Loadout.TermLines(a)) Plugin.Logger.LogInfo("[loadout] " + l);
            Plugin.Logger.LogInfo("[loadout] input #" + a.Number + " " + Loadout.InputJson(a.Input, a));
            Plugin.Logger.LogInfo("[loadout] " + Loadout.HintLine(a));
            if (Detail == LoadoutDetail.Off) Plugin.Logger.LogInfo("[loadout] off ([Advice] LoadoutHint = Off): advice #" + a.Number + " logged, nothing drawn");
            _equippedLine = "";
        }

        static void PinsLine(Build b)
        {
            string key = b.Id + "|" + string.Join(",", b.Badges) + "|" + string.Join(",", b.SkipBadges);
            if (!_pinsSaid.Add(key)) return;
            var all = LoadoutState.All; var matched = new List<string>(); var how = new List<string>(); var unknown = new List<string>();
            foreach (var r in b.Badges.Concat(b.SkipBadges))
            {
                string h; var f = Loadout.Resolve(r, all, out h);
                if (f == null) { unknown.Add(r); continue; }
                matched.Add(f.Short); if (h != "id" && h != "short name") how.Add(f.Short + " by " + h); else if (h == "short name") how.Add(f.Short + " by short name");
            }
            Plugin.Logger.LogInfo("[loadout] build \"" + b.Name + "\"" + (b.Pack.Length > 0 ? " (pack \"" + b.Pack + "\")" : "") + " names badges " + string.Join(", ", b.Badges.Concat(b.SkipBadges))
                + ": " + matched.Count + " matched" + (how.Count > 0 ? " (" + string.Join(", ", how) + ")" : "") + (unknown.Count > 0 ? ", " + unknown.Count + " unknown - skipped" : ""));
        }

        static void Once(string key, string line) { LoadoutState.Once("ui:" + key, line); }

        static void MakeView(LoadoutDetail detail)
        {
            var v = _visit;
            _view = LoadoutView.Of(_advice, v.Equipped, v.Forced, v.Slots, detail, Knowledge.Current, LoadoutState.DisplayName, s => Names.Class(s), _sep);
            string eq = _view.EquippedLine();
            if (eq != _equippedLine) { _equippedLine = eq; Plugin.Logger.LogInfo("[loadout] " + eq); }
            if (Last != null && Last.Number == _advice.Number) { Last.Equipped.Clear(); Last.Equipped.AddRange(v.Equipped); }
        }

        // ================================================================================================ the cursor poll
        // the pad moves the event system's selection; the hooks normally report it - the poll only stands in when the selection
        // moved and no hook said so
        static void PollCursor()
        {
            try
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                var go = es == null ? null : es.currentSelectedGameObject;
                IntPtr p = go == null ? IntPtr.Zero : go.Pointer;
                if (p == _lastSel) return;
                _lastSel = p;
                if (p == IntPtr.Zero || _visit == null) return;
                var cell = _visit.Grid.FirstOrDefault(c => c.ObjectPtr == p) ?? _visit.SlotCells.FirstOrDefault(c => c.ObjectPtr == p);
                if (cell == null || cell.ButtonPtr == _hlPtr) return;
                Highlighted(cell.Button, 2);
            }
            catch { }
        }

        static int SlotBadge(LoadoutCell slot)
        {
            try { var f = LoadoutState.FactsOf(LoadoutState.BadgeOf(slot.Button)); return f != null ? f.Id : -1; } catch { return -1; }
        }

        // ================================================================================================ drawing
        static string LevelOf(LoadoutDetail detail)
        {
            if (detail == LoadoutDetail.Off) return "E";
            if (string.IsNullOrEmpty(_visit.Leader)) return "D";        // SPEC 6.2: no team leader read = no advice on the screen
            bool template = Usable(_template) || Alive(_why) || Alive(_sum);
            if (!_visit.GridOk) return template && _measured ? "C" : "D";
            if (!_measured || !template) return "B";
            if (detail >= LoadoutDetail.NumbersAndReason)
            {   // A = the WHY line can follow the cursor: a band with its widget built, or the inline line in the game's bonus label
                if (_lo == null || _lo.WhyAt == "none") return "B";
                if (_lo.WhyAt != "inline" && !Alive(_why)) return "B";
            }
            return "A";
        }

        // the WHY line's state for the 'drawn' line: where it is, what it was cloned from, and whether it follows the cursor yet
        static string WhyState(LoadoutDetail detail)
        {
            if (detail < LoadoutDetail.NumbersAndReason || _lo == null) return "";
            if (_lo.WhyAt == "inline") return " | why line inline in currentBadgeBonus" + (Usable(_bonus) ? "" : " (label unreadable)");
            if (_lo.WhyAt == "none") return " | why line not drawn: no band (labels: " + (_labelNote.Length > 0 ? _labelNote : "not measured") + ")";
            if (!Alive(_why)) return " | why line not built: no usable label template";
            string size = "?"; try { var sd = _why.sizeDelta; size = Mathf.RoundToInt(sd.x) + "x" + Mathf.RoundToInt(sd.y); } catch { }
            return " | why line built " + size + " u from " + _whyFrom + (_hlId >= 0 ? ", follows the cursor" : ", hidden until the first highlight");
        }

        static bool Usable(TextMeshProUGUI t) { try { return t != null && t.font != null; } catch { return false; } }
        static bool Alive(RectTransform r) { try { return r != null && r.gameObject != null; } catch { return false; } }
        static string Safe(Func<string> f) { try { return f() ?? ""; } catch { return "?"; } }

        static void Draw(UIViewRunSetup view, LoadoutDetail detail)
        {
            long perf = Perf.Begin();
            try
            {
                var vrt = view.transform.TryCast<RectTransform>();
                if (detail == LoadoutDetail.Off)
                {
                    HideAll(); LoadoutEquip.Hide();
                    DrawnLine(detail);
                    _drawnThisVisit = true;
                    return;
                }
                if (string.IsNullOrEmpty(_visit.Leader))
                {   // level D (SPEC 6.2): the advice without a leader is no advice - nothing drawn, and nothing for the EQUIP button to press;
                    // the leader's pointer is in the signature, so the screen is drawn as soon as one is read
                    HideAll(); LoadoutEquip.Hide();
                    string line = "drawn #" + _advice.Number + " level D (" + detail + "): nothing drawn - the team leader is unknown (" + _visit.LeaderFrom + ")";
                    if (line != _drawnLine) { _drawnLine = line; Plugin.Logger.LogInfo("[loadout] " + line); }
                    _drawnThisVisit = true;
                    return;
                }
                if (!_measured && vrt != null) Measure(view, vrt, detail);
                if (_measured) ChooseLayout(detail);
                bool first = !_drawnThisVisit;
                DrawMarks(first);
                if (detail >= LoadoutDetail.NumbersAndReason && vrt != null && _lo != null) { DrawSummary(vrt, first); DrawWhy(vrt, first); }
                else { Hide(_why); Hide(_sum); }
                LoadoutEquip.Place(view, vrt, _visit, _view, _advice, _li, _lo, _numTemplate ?? _template);
                bool matched = _view.Matches;
                if (matched && !_matched && !first) Glint();
                _matched = matched;
                DrawnLine(detail);
                if (first) Shots.Later(0.9f, "setup");
                _drawnThisVisit = true;
            }
            catch (Exception e) { Fail("draw", e); }
            finally { Perf.End("loadout.draw", perf); }
        }

        static void DrawnLine(LoadoutDetail detail)
        {
            string line = "drawn #" + _advice.Number + " level " + LevelOf(detail) + " (" + detail + ", size " + Size.ToString("0.00", IC) + "): " + _view.DrawnMarks()
                + (detail == LoadoutDetail.Off ? "" : _lo != null ? " | " + LoadoutLayout.Drawn(_lo, _unitPx) + (detail >= LoadoutDetail.NumbersAndReason ? _sumNote : "") : " | layout not measured")
                + WhyState(detail)
                + (LoadoutEquip.Shown ? " | equip button" + (LoadoutEquip.PlanText.Length > 0 ? " (" + LoadoutEquip.PlanText + ")" : "") : "");
            if (line == _drawnLine) return;
            _drawnLine = line;
            Plugin.Logger.LogInfo("[loadout] " + line);
        }

        // ---- the markers ----
        static void DrawMarks(bool first)
        {
            var want = new HashSet<IntPtr>();
            int stagger = 0;
            var order = _visit.Grid.Select(c => new { c, m = _view.MarkOf(c.Id) }).Where(x => x.m != null && x.m.Kind != MarkKind.None)
                .OrderBy(x => x.m.Kind == MarkKind.Forced ? 0 : x.m.Number > 0 ? x.m.Number : 99).ThenBy(x => x.c.Index).ToList();
            int topEquip = order.Where(x => x.m.Kind == MarkKind.Equip).Select(x => x.m.Number).DefaultIfEmpty(0).Min();
            foreach (var x in order)
            {
                want.Add(x.c.ButtonPtr);
                var pl = Show(x.c.Button, x.c.ButtonPtr, x.c.BadgePtr, _markerLocal, x.m.Kind, x.m.Number, x.m.Pin, x.m.Frame);
                if (pl == null) continue;
                string st = pl.Marker.State + (x.m.Frame ? "f" : "");
                bool changed = first || st != pl.State;
                pl.State = st;
                if (changed) Stamp(pl, "loadout:mark:" + x.c.ButtonPtr, first ? stagger++ : 0, x.m.Kind == MarkKind.Equip && x.m.Number == topEquip, x.m.Kind == MarkKind.SwapOut, first);
            }
            // the slots: the same diamonds, numbered for what stays, rust for what goes
            foreach (var s in _visit.SlotCells)
            {
                int id = SlotBadge(s);
                var sm = id < 0 ? null : _view.Slots.FirstOrDefault(m => m.Id == id);
                if (sm == null || sm.Kind == MarkKind.None) continue;
                want.Add(s.ButtonPtr);
                IntPtr bp = IntPtr.Zero; try { bp = LoadoutState.BadgeOf(s.Button).Pointer; } catch { }
                var pl = Show(s.Button, s.ButtonPtr, bp, _slotMarkerLocal, sm.Kind, sm.Number, sm.Pin, false, SlotHost(s.Button, s.ButtonPtr));
                if (pl == null) continue;
                string st = pl.Marker.State;
                bool changed = first || st != pl.State;
                pl.State = st;
                if (changed) Stamp(pl, "loadout:mark:" + s.ButtonPtr, first ? stagger++ : 0, false, sm.Kind == MarkKind.SwapOut, first);
            }
            foreach (var kv in _placed)
            {
                if (want.Contains(kv.Key)) continue;
                try { if (kv.Value.Marker != null && kv.Value.Marker.Alive) kv.Value.Marker.Root.gameObject.SetActive(false); if (kv.Value.Frame != null) kv.Value.Frame.gameObject.SetActive(false); } catch { }
                kv.Value.State = "";
                Fx.Cancel("loadout:mark:" + kv.Key);
            }
        }

        static Placed Show(UIViewRunSetupBadgeButton button, IntPtr key, IntPtr badge, float size, MarkKind kind, int number, bool pin, bool frame, RectTransform host = null)
        {
            try
            {
                var brt = button.transform.TryCast<RectTransform>(); if (brt == null) return null;
                var on = host ?? brt;           // a slot's marker hangs on its stand-in over the slot row (SlotHost), a grid badge's on its button
                Placed pl;
                if (!_placed.TryGetValue(key, out pl)) { pl = new Placed(); _placed[key] = pl; }
                if (pl.Marker == null || !pl.Marker.Alive || Mathf.Abs(pl.Marker.Size - size) > 0.5f || pl.Badge != badge || Ptr(pl.Marker.Root.parent) != Ptr(on))
                {
                    try { if (pl.Marker != null && pl.Marker.Alive) UnityEngine.Object.Destroy(pl.Marker.Root.gameObject); } catch { }
                    pl.Marker = Ui.Marker(on, MarkName, size, _numTemplate);
                    pl.Badge = badge; pl.State = "";
                }
                pl.Marker.Set(kind, number, pin);
                pl.Marker.Root.SetAsLastSibling();
                if (frame)
                {
                    bool alive = false; try { alive = pl.Frame != null && pl.Frame.gameObject != null; } catch { }
                    if (!alive)
                    {
                        pl.Frame = Ui.Frame(brt, FrameName, -4f, 4f, Theme.Gold, Mathf.Max(18f, size * 0.36f));
                        try { var le = pl.Frame.gameObject.AddComponent(Il2CppType.Of<LayoutElement>()).TryCast<LayoutElement>(); if (le != null) le.ignoreLayout = true; } catch { }
                    }
                    pl.Frame.gameObject.SetActive(true);
                    pl.Marker.Root.SetAsLastSibling();
                }
                else if (pl.Frame != null) { try { pl.Frame.gameObject.SetActive(false); } catch { pl.Frame = null; } }
                return pl;
            }
            catch (Exception e) { Fail("marker", e); return null; }
        }

        // 0.15.x (C-m5 of the 10-07 review): a marker sits on its button's top-right corner, a third of the diamond past the edge. On the
        // CHOSEN BADGES row the next slot is a later sibling and was drawn over that third - the '3' and the rust diamonds were cut, on
        // the Deck the digit unreadable (so since 0.13.0). The slot markers hang on one layer drawn right after the last slot (a child of
        // the slots' common parent, out of its layout, no clicks), each on a stand-in that takes its slot's rect every frame (the game
        // moves and scales the slots as the screen flies in). The grid's markers stay on their buttons: the grid's cells keep a gap.
        const string SlotLayerName = "YazsLoadoutSlotMarks";
        static RectTransform _slotLayer;
        static readonly Dictionary<IntPtr, RectTransform[]> _slotHosts = new Dictionary<IntPtr, RectTransform[]>();     // slot button -> { its rect, the stand-in }

        static RectTransform SlotHost(UIViewRunSetupBadgeButton button, IntPtr key)
        {
            try
            {
                var brt = button.transform.TryCast<RectTransform>(); if (brt == null) return null;
                RectTransform[] pair;
                if (_slotHosts.TryGetValue(key, out pair) && Alive(pair[1]) && Alive(_slotLayer)) { Follow(pair[0], pair[1]); return pair[1]; }
                if (!Alive(_slotLayer)) { _slotLayer = SlotLayer(); _slotHosts.Clear(); _followed.Clear(); }
                if (_slotLayer == null) return null;
                var host = Ui.NewRect("Slot", _slotLayer);
                host.anchorMin = host.anchorMax = new Vector2(0.5f, 0.5f);
                _slotHosts[key] = new[] { brt, host }; _followed.Remove(Ptr(host));      // a new stand-in is always written (a pointer can be reused)
                Follow(brt, host);
                return host;
            }
            catch (Exception e) { Once("slotlayer", "the slot markers stay on their buttons: " + e.GetBaseException().Message); return null; }
        }

        // the slots' lowest common parent gets the layer, right after the child that holds the last slot (never over what the game draws later)
        static RectTransform SlotLayer()
        {
            var slots = new List<Transform>();
            foreach (var c in _visit.SlotCells) { try { var t = c.Button.transform; if (t != null) slots.Add(t); } catch { } }
            if (slots.Count == 0) return null;
            Func<Transform, Transform, bool> under = (t, p) => { for (var u = t; u != null; u = u.parent) if (Ptr(u) == Ptr(p)) return true; return false; };
            Transform parent = slots[0].parent;
            while (parent != null && !slots.All(t => under(t, parent))) parent = parent.parent;
            var prt = parent == null ? null : parent.TryCast<RectTransform>();
            if (prt == null) return null;
            int after = -1;
            foreach (var t in slots) { var u = t; while (u != null && Ptr(u.parent) != Ptr(parent)) u = u.parent; if (u != null) after = Mathf.Max(after, u.GetSiblingIndex()); }
            var layer = Ui.NewRect(SlotLayerName, prt);
            Ui.Stretch(layer, 0, 0, 0, 0);
            try { var le = layer.gameObject.AddComponent(Il2CppType.Of<LayoutElement>()).TryCast<LayoutElement>(); if (le != null) le.ignoreLayout = true; } catch { }
            try { var g = layer.gameObject.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>(); if (g != null) { g.blocksRaycasts = false; g.interactable = false; } } catch { }
            if (after >= 0) layer.SetSiblingIndex(after + 1);
            return layer;
        }

        // the stand-in takes the slot's rect in world space (its pivot, size, position, rotation, scale) and its being shown - written
        // only when one of them changed since the stand-in's last write (the 10-07 fix review: five transform writes on up to four
        // stand-ins every frame of the screen mark its canvas dirty, a batch rebuild each frame - on the Deck, whose first draw of the
        // screen already costs 74-91 ms; the game's fly-in still moves them every frame while it runs, the still screen writes nothing)
        sealed class Followed { public bool Set; public Vector2 Pivot, Size; public Vector3 Pos, Scale, ParentScale, ParentPos; public Quaternion Rot; }
        static readonly Dictionary<IntPtr, Followed> _followed = new Dictionary<IntPtr, Followed>();       // stand-in -> what it was last given
        static int _followWrites, _followFrames;                                                           // this visit: frames that wrote / frames followed

        static void Follow(RectTransform slot, RectTransform host)
        {
            try
            {
                bool shown = slot.gameObject.activeInHierarchy;
                if (host.gameObject.activeSelf != shown) host.gameObject.SetActive(shown);
                var key = Ptr(host);
                Followed f;
                if (!_followed.TryGetValue(key, out f)) { f = new Followed(); _followed[key] = f; }
                if (!shown) { f.Set = false; return; }
                Vector2 pivot = slot.pivot, size = slot.rect.size;
                var hp = host.parent;
                Vector3 pos = slot.position, ls = slot.lossyScale, ps = hp != null ? hp.lossyScale : Vector3.one, pp = hp != null ? hp.position : Vector3.zero;
                Quaternion rot = slot.rotation;
                // a hundredth of a canvas unit (in world space: times the layer's scale), a ten-thousandth of a scale, a hundredth of a degree
                float eps = Mathf.Max(1e-7f, 0.01f * Mathf.Abs(ps.x));
                if (f.Set && (pivot - f.Pivot).sqrMagnitude < 1e-8f && (size - f.Size).sqrMagnitude < 1e-4f && (pos - f.Pos).sqrMagnitude < eps * eps && (pp - f.ParentPos).sqrMagnitude < eps * eps
                    && Same(ls, f.Scale) && Same(ps, f.ParentScale) && Quaternion.Angle(rot, f.Rot) < 0.01f) return;
                host.pivot = pivot;
                host.sizeDelta = size;
                host.position = pos;
                host.rotation = rot;
                host.localScale = new Vector3(Mathf.Abs(ps.x) > 1e-6f ? ls.x / ps.x : 1f, Mathf.Abs(ps.y) > 1e-6f ? ls.y / ps.y : 1f, 1f);
                f.Set = true; f.Pivot = pivot; f.Size = size; f.Pos = pos; f.Scale = ls; f.ParentScale = ps; f.ParentPos = pp; f.Rot = rot;
                _followWrote = true;
            }
            catch { }
        }

        static bool _followWrote;
        static bool Same(Vector3 a, Vector3 b)
        {
            float m = Mathf.Max(1e-6f, Mathf.Max(Mathf.Abs(b.x), Mathf.Abs(b.y)));
            return Mathf.Abs(a.x - b.x) < 1e-4f * m && Mathf.Abs(a.y - b.y) < 1e-4f * m && Mathf.Abs(a.z - b.z) < 1e-4f * Mathf.Max(m, Mathf.Abs(b.z));
        }

        /// <summary>Every frame of the screen: the slot markers' stand-ins follow their slots.</summary>
        static void FollowSlots()
        {
            if (_slotHosts.Count == 0) return;
            _followWrote = false;
            foreach (var kv in _slotHosts) { var pair = kv.Value; if (Alive(pair[0]) && Alive(pair[1])) Follow(pair[0], pair[1]); }
            _followFrames++; if (_followWrote) _followWrites++;
        }

        // big to small with a half turn, a ping as it lands (TreeUi's stamp); the top EQUIP badge keeps breathing; rust marks
        // only fade in, last; a frame settles onto its button
        static void Stamp(Placed pl, string key, int index, bool top, bool rust, bool first)
        {
            Fx.Cancel(key);
            var m = pl.Marker; var root = m.Root; var turn = m.Turn; var group = m.Group; var ring = m.Ring; var ringGroup = m.RingGroup;
            var frame = pl.Frame != null && pl.Frame.gameObject.activeSelf ? pl.Frame : null;
            if (!Fx.On)
            {
                root.localScale = Vector3.one; if (group != null) group.alpha = 1f; if (ringGroup != null) ringGroup.alpha = 0f;
                if (frame != null) frame.localScale = Vector3.one;
                return;
            }
            if (rust)
            {
                Fx.Run(key + ":in", first ? 0.55f + 0.04f * index : 0f, 0.4f, k => { root.localScale = Vector3.one; if (group != null) group.alpha = Fx.Smooth(k); });
                return;
            }
            float s0 = turn.localScale.x;
            Fx.Run(key + ":in", 0.05f + 0.085f * index, 0.36f, k =>
            {
                float s = Mathf.LerpUnclamped(2.3f, 1f, Fx.OutBack(k));
                root.localScale = new Vector3(s, s, 1f);
                if (group != null) group.alpha = Mathf.Clamp01(k * 3.5f);
                turn.localRotation = Quaternion.Euler(0, 0, 180f * (1f - Fx.OutCubic(k)));
            }, () =>
            {
                if (ring != null && ringGroup != null) Fx.Run(key + ":ping", 0f, 0.55f, k => Fx.PingPose(ring, ringGroup, k, 2.5f));
                if (top) Fx.Loop(key + ":idle", 2.8f, k =>
                {
                    float s = 1f + 0.055f * Mathf.Sin(k * Mathf.PI * 2f);
                    root.localScale = new Vector3(s, s, 1f);
                    if (ring != null && ringGroup != null && k > 0.45f) Fx.PingPose(ring, ringGroup, Mathf.Min(1f, (k - 0.45f) / 0.24f), 2.3f);
                });
            });
            if (frame != null)
            {
                CanvasGroup g = null;
                try { g = frame.GetComponent<CanvasGroup>(); if (g == null) { g = frame.gameObject.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>(); g.blocksRaycasts = false; g.interactable = false; } } catch { }
                Fx.Run(key + ":frame", 0.05f + 0.085f * index, 0.45f, k => { float sc = 1f + 0.08f * (1f - Fx.OutCubic(k)); frame.localScale = new Vector3(sc, sc, 1f); if (g != null) g.alpha = Fx.Smooth(k * 1.6f); });
            }
        }

        // equipped = advised after a change: one glint runs across the slot diamonds, then everything rests
        static void Glint()
        {
            int i = 0;
            foreach (var s in _visit.SlotCells)
            {
                Placed pl; if (!_placed.TryGetValue(s.ButtonPtr, out pl) || pl.Marker == null || !pl.Marker.Alive || !pl.Marker.Root.gameObject.activeSelf) continue;
                Fx.Ping("loadout:glint:" + i, pl.Marker.Root, pl.Marker.Size * 1.3f, 4f, Theme.Gold, true, 0.1f * i, 0.5f, 2.2f, false);
                i++;
            }
        }

        static void Hide(RectTransform rt) { try { if (rt != null) rt.gameObject.SetActive(false); } catch { } }

        // the screen closed: nothing of ours stays behind under its buttons (the next visit builds afresh; Destroy is deferred to
        // the end of the frame, so this is safe inside the view's own OnDisable)
        static void DestroyAll()
        {
            foreach (var pl in _placed.Values)
            {
                try { if (pl.Marker != null && pl.Marker.Alive) UnityEngine.Object.Destroy(pl.Marker.Root.gameObject); } catch { }
                try { if (pl.Frame != null) UnityEngine.Object.Destroy(pl.Frame.gameObject); } catch { }
            }
            try { if (_why != null) UnityEngine.Object.Destroy(_why.gameObject); } catch { }
            try { if (_sum != null) UnityEngine.Object.Destroy(_sum.gameObject); } catch { }
            try { if (Alive(_slotLayer)) UnityEngine.Object.Destroy(_slotLayer.gameObject); } catch { }
            _slotLayer = null; _slotHosts.Clear(); _followed.Clear();
        }

        static void HideAll()
        {
            foreach (var pl in _placed.Values)
            {
                try { if (pl.Marker != null && pl.Marker.Alive) pl.Marker.Root.gameObject.SetActive(false); } catch { }
                try { if (pl.Frame != null) pl.Frame.gameObject.SetActive(false); } catch { }
                pl.State = "";
            }
            Hide(_why); Hide(_sum);
            Fx.Cancel("loadout:");
        }

        // ---- the measurement (once per visit: the grid, the labels and the obstacles cannot move while the view is up) ----
        static Il2CppStructArray<Vector3> _cornersArr;       // made on first use, never at load (the class is touched while Harmony patches)
        static Il2CppStructArray<Vector3> _corners { get { return _cornersArr ?? (_cornersArr = new Il2CppStructArray<Vector3>(4)); } }

        static R4 RectIn(RectTransform rt, RectTransform view)
        {
            rt.GetWorldCorners(_corners);
            var a = view.InverseTransformPoint(_corners[0]); var b = view.InverseTransformPoint(_corners[2]);
            var r = view.rect;
            return new R4(Mathf.Min(a.x, b.x) - r.xMin, r.yMax - Mathf.Max(a.y, b.y), Mathf.Max(a.x, b.x) - r.xMin, r.yMax - Mathf.Min(a.y, b.y));
        }

        // a label's rendered text, not its rect; empty when it shows nothing
        static R4 TextIn(TextMeshProUGUI t, RectTransform view) { string state; return TextIn(t, view, false, out state); }

        static bool BoundsOk(Bounds tb) { return tb.size.x > 0.5f && tb.size.y > 0.5f && !float.IsInfinity(tb.size.x) && !float.IsNaN(tb.size.x) && tb.size.x < 100000f; }

        // the same, with what was found for the log ("null" / "off" / "no text" / "no bounds", each with the label's own rect, or
        // the rendered rect); force = an info label whose text the game may have set this frame: its mesh is built now
        // (ForceMeshUpdate) when its bounds are still empty
        static R4 TextIn(TextMeshProUGUI t, RectTransform view, bool force, out string state)
        {
            state = "null";
            try
            {
                if (t == null) return new R4();
                Func<string> own = () => { try { return " (rect " + RectIn(t.rectTransform, view) + ")"; } catch { return ""; } };
                if (!t.gameObject.activeInHierarchy) { state = "off" + own(); return new R4(); }
                if (string.IsNullOrEmpty(t.text)) { state = "no text" + own(); return new R4(); }
                var tb = t.textBounds;
                if (!BoundsOk(tb) && force) { try { t.ForceMeshUpdate(); } catch { } tb = t.textBounds; }
                if (!BoundsOk(tb)) { state = "no bounds" + own(); return new R4(); }
                var a = view.InverseTransformPoint(t.transform.TransformPoint(tb.min)); var b = view.InverseTransformPoint(t.transform.TransformPoint(tb.max));
                var r = view.rect;
                var o = new R4(Mathf.Min(a.x, b.x) - r.xMin, r.yMax - Mathf.Max(a.y, b.y), Mathf.Max(a.x, b.x) - r.xMin, r.yMax - Mathf.Min(a.y, b.y));
                state = o.Empty ? "zero size" + own() : o.ToString();
                return o;
            }
            catch (Exception e) { state = "error " + e.GetType().Name; return new R4(); }
        }

        // the three info labels (field reads, no search) into the layout input; their state for the 'layout' line
        static void MeasureLabels(UIViewRunSetup view, RectTransform vrt, LayoutIn li, out TextMeshProUGUI name, out TextMeshProUGUI desc, out TextMeshProUGUI bonus)
        {
            LoadoutState.Labels(view, out name, out desc, out bonus);
            string sn, sd, sb;
            li.Name = TextIn(name, vrt, true, out sn); li.Desc = TextIn(desc, vrt, true, out sd); li.Bonus = TextIn(bonus, vrt, true, out sb);
            _labelNote = "name " + sn + " desc " + sd + " bonus " + sb;
            if (Usable(bonus)) _bonus = bonus;
        }

        // the first highlight of the visit filled the game's info labels: measure them again (once per visit), choose the layout
        // again and redraw - the WHY line may now find under-info / over-name, and the labels join what a band must stay off
        static void Relabel(UIViewRunSetup view)
        {
            _labelsPending = false; _labelsWanted = false;
            if (_visit == null || _li == null || _advice == null || _view == null || !_measured) return;
            try { if (view.Pointer != _visit.ViewPtr) return; } catch { return; }
            var vrt = view.transform.TryCast<RectTransform>(); if (vrt == null) return;
            string whyBefore = _lo == null ? "?" : _lo.WhyAt;
            TextMeshProUGUI n, d, b;
            MeasureLabels(view, vrt, _li, out n, out d, out b);
            Plugin.Logger.LogInfo("[loadout] labels measured again on the first highlight (visit " + _visit.Number + "): " + _labelNote);
            Draw(view, Detail);
            if (_lo != null && _lo.WhyAt == "inline") Inline();          // the badge on show now gets its line too, not only the next one
            LayoutLine("on the first highlight, why " + whyBefore + " -> " + (_lo == null ? "?" : _lo.WhyAt));
        }

        static void Measure(UIViewRunSetup view, RectTransform vrt, LoadoutDetail detail)
        {
            var li = new LayoutIn();
            var vr = vrt.rect;
            li.View = new R4(0, 0, vr.width, vr.height);
            try { var canvas = view.GetComponentInParent<Canvas>(); var root = canvas == null ? null : canvas.rootCanvas; var crt = root == null ? null : root.transform.TryCast<RectTransform>(); if (crt != null) li.Canvas = RectIn(crt, vrt); } catch { }
            if (li.Canvas.Empty) li.Canvas = li.View;
            _unitPx = UnityEngine.Screen.height / Mathf.Max(1f, li.Canvas.H);
            // the grid, row by row (the game's grid is badgeSortOrder across the rows)
            var grid = new List<KeyValuePair<LoadoutCell, R4>>();
            foreach (var c in _visit.Grid) { try { var rt = c.Button.transform.TryCast<RectTransform>(); if (rt != null) grid.Add(new KeyValuePair<LoadoutCell, R4>(c, RectIn(rt, vrt))); } catch { } }
            if (_visit.GridOk && (grid.Count == 0 || grid.Any(g => g.Value.W <= 1f)) && ++_measureTries < 4) return;      // right after OnEnable a rect can still be zero
            li.Grid = grid.Select(g => g.Value).OrderBy(r => Mathf.Round(r.Y0 / 20f)).ThenBy(r => r.X0).ToArray();
            var slots = new List<R4>();
            foreach (var c in _visit.SlotCells) { try { var rt = c.Button.transform.TryCast<RectTransform>(); if (rt != null) slots.Add(RectIn(rt, vrt)); } catch { } }
            li.Slots = slots.OrderBy(r => r.X0).ToArray();
            var diff = new List<R4>();
            try { foreach (var rt in LoadoutState.DifficultyRects(view)) { try { diff.Add(RectIn(rt, vrt)); } catch { } } } catch { }      // the call itself in a try too: it names the difficulty button class
            li.Difficulty = diff.ToArray();
            TextMeshProUGUI name, desc, bonus;
            MeasureLabels(view, vrt, li, out name, out desc, out bonus);
            // before the first highlight the game's info panel shows nothing: the labels are measured again on that highlight
            _labelsWanted = li.Name.Empty || li.Desc.Empty || li.Bonus.Empty;
            // the START bar: its button and quest line (the bar's own rect may span the screen - never a parent of ours)
            try
            {
                var parts = new List<R4>();
                foreach (var rt in LoadoutState.StartBarRects()) { try { parts.Add(RectIn(rt, vrt)); } catch { } }
                var u = R4.Union(parts);
                if (!u.Empty && u.H < 0.45f * li.View.H) li.StartBar = u;
            }
            catch { }
            // the obstacles: every active graphic under the view but ours, the buttons' own pieces and the info labels; backgrounds
            // and frames become containers (LoadoutLayout.Split)
            var graphics = new List<R4>();
            IntPtr pn = Ptr(name), pd = Ptr(desc), pb = Ptr(bonus);
            var buttons = li.Grid.Concat(li.Slots).Select(r => new R4(r.X0 - 0.25f * r.W, r.Y0 - 0.25f * r.W, r.X1 + 0.25f * r.W, r.Y1 + 0.35f * r.W)).ToList();
            try
            {
                var all = vrt.GetComponentsInChildren(Il2CppType.Of<Graphic>(), false);
                int n = Math.Min(all.Length, 3000);
                for (int i = 0; i < n; i++)
                {
                    var g = all[i].TryCast<Graphic>(); if (g == null) continue;
                    IntPtr gp = g.Pointer;
                    if (gp == pn || gp == pd || gp == pb) continue;
                    try
                    {
                        if (g.color.a <= 0.05f) continue;
                        string gn = g.gameObject.name ?? "";
                        if (gn.StartsWith("Yazs", StringComparison.Ordinal)) continue;
                        if ((_why != null && g.transform.IsChildOf(_why)) || (_sum != null && g.transform.IsChildOf(_sum))) continue;
                        if (g.transform.parent != null && (g.transform.parent.name ?? "").StartsWith("Yazs", StringComparison.Ordinal)) continue;
                        R4 r;
                        var tmp = g.TryCast<TextMeshProUGUI>();
                        if (tmp != null) { r = TextIn(tmp, vrt); if (r.Empty) continue; }
                        else r = RectIn(g.rectTransform, vrt);
                        if (r.Empty) continue;
                        float cx = (r.X0 + r.X1) / 2, cy = (r.Y0 + r.Y1) / 2;
                        if (buttons.Any(b => b.Contains(cx, cy) && r.W <= b.W && r.H <= b.H)) continue;      // the buttons' own icons, frames, diamonds
                        graphics.Add(r);
                    }
                    catch { }
                }
            }
            catch (Exception e) { Once("obstacles", "the screen's graphics could not be listed (" + e.GetBaseException().Message + "): bands measured without obstacles"); }
            List<R4> obstacles, containers;
            LoadoutLayout.Split(graphics, li.View, li.Grid, li.Slots, out obstacles, out containers);
            li.Obstacles = obstacles.ToArray(); li.Containers = containers.ToArray();
            li.UnitPx = _unitPx; li.ScreenH = UnityEngine.Screen.height;
            _li = li; _measured = true;
            // marker sizes in the buttons' own units (the layout works in view units)
            try
            {
                if (grid.Count > 0) { var rt = grid[0].Key.Button.transform.TryCast<RectTransform>(); float local = rt.rect.width, inView = grid[0].Value.W; _markerScale = inView > 1f && local > 1f ? local / inView : 1f; }
                if (_visit.SlotCells.Count > 0 && slots.Count > 0) { var rt = _visit.SlotCells[0].Button.transform.TryCast<RectTransform>(); float local = rt.rect.width, inView = slots[0].W; _slotScale = inView > 1f && local > 1f ? local / inView : 1f; }
            }
            catch { }
        }
        static float _markerScale = 1f, _slotScale = 1f;
        static IntPtr Ptr(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase o) { try { return o == null ? IntPtr.Zero : o.Pointer; } catch { return IntPtr.Zero; } }

        static void ChooseLayout(LoadoutDetail detail)
        {
            _li.Size = Size; _li.Detail = detail; _li.SumRows = Math.Max(1, _view.Rows.Count);
            _lo = LoadoutLayout.Choose(_li);
            _markerLocal = _lo.MarkerUnits * _markerScale;
            _slotMarkerLocal = _lo.MarkerUnits * _slotScale;
            string key = UnityEngine.Screen.width + "x" + UnityEngine.Screen.height;
            if (_layoutSaid.Add(key)) LayoutLine();
            if (_lo.WhyAt == "inline" && !_inlineSaid) { _inlineSaid = true; Plugin.Logger.LogInfo("[loadout] why: inline in the game's bonus label (no band: " + _lo.Bands + ")"); }
        }

        static void LayoutLine(string when = "")
        {
            if (_li == null || _lo == null) return;
            Func<R4, string> s = r => r.Empty ? "-" : r.ToString();
            // the labels: the rendered rect, or why there is none ("no text (rect ..)" before the first highlight, "off", "null")
            Plugin.Logger.LogInfo("[loadout] layout " + (when.Length > 0 ? "(" + when + ") " : "") + LoadoutLayout.Describe(_li, _lo, UnityEngine.Screen.width, UnityEngine.Screen.height)
                + " | " + (_labelNote.Length > 0 ? _labelNote : "name " + s(_li.Name) + " desc " + s(_li.Desc) + " bonus " + s(_li.Bonus))
                + " | difficulty " + _li.Difficulty.Length + " " + s(R4.Union(_li.Difficulty))
                + " | start bar " + s(_li.StartBar) + " | containers " + _li.Containers.Length
                + " | why " + _lo.WhyAt + (_lo.WhyAt == "none" || _lo.WhyAt == "inline" ? "" : " " + _lo.Why + " (" + _lo.WhyLines + " line" + (_lo.WhyLines == 1 ? "" : "s") + ", " + _lo.WhyFontUnits.ToString("0.0", IC) + " u)")
                + " | template " + (_templateFrom.Length > 0 ? _templateFrom : "none") + (_lo.Notes.Count > 0 ? " | " + _lo.Note : ""));
        }

        // ---- the WHY line ----
        // the templates a band's text may be cloned from, best first: the visit's template (the game's description label), the
        // summary's own clones (made from a game label already), the name label; the last resort is one walk of the view's
        // labels, once per visit - the WHY line is never given up for want of a label
        static RectTransform BuildBand(RectTransform view, string name, int lines, out string from)
        {
            from = "";
            var tries = new List<KeyValuePair<string, TextMeshProUGUI>> { new KeyValuePair<string, TextMeshProUGUI>(_templateFrom.Length > 0 ? _templateFrom : "the visit's template", _template) };
            foreach (var row in _rows) tries.Add(new KeyValuePair<string, TextMeshProUGUI>("the summary's clone of " + (_sumFrom.Length > 0 ? _sumFrom : "a game label"), row));
            tries.Add(new KeyValuePair<string, TextMeshProUGUI>("currentBadgeName", _numTemplate));
            var seen = new HashSet<IntPtr>();
            foreach (var kv in tries)
            {
                if (!Usable(kv.Value) || !seen.Add(Ptr(kv.Value))) continue;
                var r = BuildBandFrom(view, name, lines, kv.Value);
                if (r != null) { from = kv.Key; return r; }
            }
            if (!_templateSearched)
            {
                _templateSearched = true;
                var t = Ui.FindLabel(view, null);
                if (Usable(t) && !seen.Contains(Ptr(t)))
                {
                    var r = BuildBandFrom(view, name, lines, t);
                    if (r != null) { _template = t; _templateFrom = "a label of the view (" + Safe(() => t.name) + ")"; from = _templateFrom; return r; }
                }
            }
            Once("notemplate:" + name, name + ": no label could be cloned (description, summary, name, the view's labels) - not drawn");
            return null;
        }

        static RectTransform BuildBandFrom(RectTransform view, string name, int lines, TextMeshProUGUI template)
        {
            var r = Ui.NewRect(name, view);
            try { var le = r.gameObject.AddComponent(Il2CppType.Of<LayoutElement>()).TryCast<LayoutElement>(); if (le != null) le.ignoreLayout = true; } catch { }
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0f, 1f);
            Ui.Image(r, "Bar", new Color(Theme.Plate.r, Theme.Plate.g, Theme.Plate.b, 0.82f));
            var rule = Ui.Image(r, "Rule", Theme.GoldLine); rule.anchorMin = new Vector2(0, 0); rule.anchorMax = new Vector2(1, 0); rule.pivot = new Vector2(0f, 0f); rule.anchoredPosition = Vector2.zero;
            Ui.Diamond(r, "TipL", 0, 0.5f, Tip, Theme.Gold);
            Ui.Diamond(r, "TipR", 1, 0.5f, Tip, Theme.Gold);
            for (int i = 0; i < lines; i++)
            {
                TextMeshProUGUI t = null;
                try { t = Ui.CloneText(template, r, "Line" + i); } catch { t = null; }
                if (t == null) { try { r.gameObject.SetActive(false); UnityEngine.Object.Destroy(r.gameObject); } catch { } return null; }
                try { t.overflowMode = TextOverflowModes.Ellipsis; t.enableWordWrapping = false; } catch { }
                t.fontStyle = FontStyles.Normal; t.alignment = TextAlignmentOptions.Left; t.characterSpacing = 0f; t.color = Theme.Cream;
                try { t.lineSpacing = 0f; t.paragraphSpacing = 0f; } catch { }
                try { t.maxVisibleCharacters = 99999; } catch { }        // a clone of a row caught mid type-on would keep its cut
            }
            return r;
        }

        // the band's rect in view units -> the widget (pivot top-left, anchored at the view's centre)
        static void PlaceBand(RectTransform r, RectTransform view, R4 band, float height)
        {
            var vr = view.rect;
            float y0 = band.Y0 + Mathf.Max(0f, (band.H - height) / 2f);
            r.anchoredPosition = new Vector2(band.X0 - vr.width / 2f, vr.height / 2f - y0);
            r.sizeDelta = new Vector2(band.W, height);
            var bar = r.Find("Bar").TryCast<RectTransform>(); Ui.Stretch(bar, Tip / 2f, 0, Tip / 2f, 0);
            var rule = r.Find("Rule").TryCast<RectTransform>(); rule.sizeDelta = new Vector2(0f, 3f); rule.offsetMin = new Vector2(Tip / 2f, 0); rule.offsetMax = new Vector2(-Tip / 2f, 3f);
            foreach (var n in new[] { "TipL", "TipR" }) { var d = r.Find(n).TryCast<RectTransform>(); d.sizeDelta = new Vector2(Tip * 0.7071f, Tip * 0.7071f); }
        }

        static void DrawWhy(RectTransform view, bool first)
        {
            if (_lo.WhyAt == "none" || _lo.WhyAt == "inline") { Hide(_why); return; }
            bool alive = false; try { alive = _why != null && _why.gameObject != null; } catch { }
            if (!alive) { _why = BuildBand(view, WhyName, 2, out _whyFrom); if (_why == null) return; _why1 = _why.Find("Line0").GetComponent<TextMeshProUGUI>(); _why2 = _why.Find("Line1").GetComponent<TextMeshProUGUI>(); }
            float f = _lo.WhyFontUnits, line = LoadoutLayout.LineFactor * f;
            int lines = Math.Max(1, _lo.WhyLines);
            PlaceBand(_why, view, _lo.Why, lines * line + 6f);
            float pad = Tip / 2f + 16f;
            // the dim second line is a touch smaller, but never under the 15 px floor (on the Deck the font sits on it already)
            float floorU = LoadoutLayout.MinPx / Mathf.Max(0.0001f, _unitPx);
            _why1.fontSize = f; _why2.fontSize = Mathf.Min(f, Mathf.Max(f * 0.94f, floorU));
            _why1.rectTransform.anchorMin = new Vector2(0, 1); _why1.rectTransform.anchorMax = new Vector2(1, 1); _why1.rectTransform.pivot = new Vector2(0.5f, 1f);
            _why1.rectTransform.offsetMin = new Vector2(pad, -line - 3f); _why1.rectTransform.offsetMax = new Vector2(-pad, -3f);
            _why2.rectTransform.anchorMin = new Vector2(0, 1); _why2.rectTransform.anchorMax = new Vector2(1, 1); _why2.rectTransform.pivot = new Vector2(0.5f, 1f);
            _why2.rectTransform.offsetMin = new Vector2(pad, -2f * line - 3f); _why2.rectTransform.offsetMax = new Vector2(-pad, -line - 3f);
            _why2.gameObject.SetActive(lines > 1);
            _whyLineU = line; _whyLinesN = lines;
            _why.SetAsLastSibling();
            ShowWhy(first);
        }

        static float _whyLineU = 60f; static int _whyLinesN = 1;

        // the WHY text against the band's width (a line holds ~22 em of the game's narrow font at 27 px on the PC, ~20 em at 15 px
        // on the Deck - a 60-character reason does not always fit): both lines shrink to fit, to max(0.87x, 15 px) at most; a
        // line 1 still too wide at that floor (the Deck, where the font already sits on it) wraps over both lines and the dim
        // line 2 gives way - the reason matters more than its details; "..." only past that
        static void FitWhy(string l1, string l2)
        {
            if (_lo == null || _why1 == null || _why2 == null) return;
            float f = _lo.WhyFontUnits, floorU = LoadoutLayout.MinPx / Mathf.Max(0.0001f, _unitPx);
            float minU = Mathf.Min(f, Mathf.Max(LoadoutLayout.MinShrink * f, floorU));
            float width = Mathf.Max(1f, _lo.Why.W - 2f * (Tip / 2f + 16f));
            float f1 = f, f2 = Mathf.Min(f, Mathf.Max(f * 0.94f, floorU));
            bool wrap = false, two = _whyLinesN > 1;
            try
            {
                _why2.gameObject.SetActive(two);                 // measured while active (the caller has the band up already)
                _why1.enableWordWrapping = false; _why1.fontSize = f1;
                float w1 = _why1.GetPreferredValues(l1).x;
                if (w1 > width) { f1 = Mathf.Max(minU, f * width / w1); wrap = two && w1 * f1 / f > width + 1f; }
                if (!wrap && two && l2.Length > 0)
                {
                    _why2.fontSize = f2;
                    float w2 = _why2.GetPreferredValues(l2).x;
                    if (w2 > width) f2 = Mathf.Max(Mathf.Min(minU, f2), f2 * width / w2);
                }
            }
            catch { }
            _why1.fontSize = f1; _why2.fontSize = f2;
            try { _why1.enableWordWrapping = wrap; } catch { }
            _why1.rectTransform.offsetMin = new Vector2(_why1.rectTransform.offsetMin.x, (wrap ? -2f : -1f) * _whyLineU - 3f);
            _why2.gameObject.SetActive(two && !wrap);
        }

        // the WHY of the highlighted badge (hidden until the first highlight of the visit), or the summary's first row when the
        // summary found no band and nothing is highlighted
        static void ShowWhy(bool retype)
        {
            if (_view == null || _lo == null) return;
            if (_lo.WhyAt == "inline") { Inline(); return; }
            bool alive = false; try { alive = _why != null && _why.gameObject != null && _why1 != null; } catch { }
            if (!alive) return;
            string l1 = "", l2 = "";
            var m = _hlId >= 0 ? _view.MarkOf(_hlId) : null;
            if (m != null && Detail >= LoadoutDetail.NumbersAndReason) { l1 = Names.Text(m.Why1); l2 = Names.Text(m.Why2); }
            else if (_lo.SumAt == "none" && _view.Rows.Count > 0) l1 = RowText(_view.Rows[0]);
            if (l1.Length == 0) { _why.gameObject.SetActive(false); return; }
            bool was = _why.gameObject.activeSelf;
            _why1.text = l1;
            _why2.text = l2.Length > 0 ? "<color=" + Theme.DimHex + ">" + l2 + "</color>" : "";
            _why.gameObject.SetActive(true);
            FitWhy(l1, _why2.text);
            if (!_whySaid && m != null)
            {   // once per visit: the proof that the WHY line is up, where, and what it says
                _whySaid = true;
                var f = LoadoutState.All == null ? null : LoadoutState.All.FirstOrDefault(x => x.Id == _hlId);
                string at = "?"; try { var p = _why.anchoredPosition; var sd = _why.sizeDelta; at = sd.x.ToString("0", IC) + "x" + sd.y.ToString("0", IC) + " at (" + p.x.ToString("0", IC) + "," + p.y.ToString("0", IC) + ") from the view's centre"; } catch { }
                Plugin.Logger.LogInfo("[loadout] why shown #" + _number + " for " + _hlId + " " + (f != null ? f.Short : "?") + " in " + _lo.WhyAt + " " + _lo.Why + " (" + at + ", font " + _why1.fontSize.ToString("0.0", IC)
                    + " u, " + (_why2.gameObject.activeSelf ? "2 lines" : "1 line") + ", from " + _whyFrom + "): " + LoadoutView.Visible(l1) + (l2.Length > 0 && _why2.gameObject.activeSelf ? " / " + LoadoutView.Visible(l2) : ""));
            }
            if (!was)
            {
                var r = _why;
                Fx.Run("loadout:why", 0f, 0.3f, k => { r.localScale = new Vector3(Mathf.Max(0.0001f, Fx.OutBack(k)), 1f, 1f); });
                Fx.Type("loadout:why1", _why1, 0.1f, 140f);
            }
            else if (retype) { _why.localScale = Vector3.one; Fx.Type("loadout:why1", _why1, 0f, 400f); }
            // a redraw with the line already up (the labels' re-measure one frame after the first highlight, a click) must not
            // flash the whole text into a type-on still waiting out its delay: a running type-on ends at full length by itself
            else if (!Fx.Busy("loadout:why1")) { try { _why1.maxVisibleCharacters = 99999; } catch { } }
        }

        // no band: one line appended to the game's own bonus label, right after its SetText (the next highlight overwrites it)
        static void Inline()
        {
            try
            {
                if (_bonus == null || _view == null || _hlId < 0 || Detail < LoadoutDetail.NumbersAndReason) return;
                var m = _view.MarkOf(_hlId); if (m == null || m.Why1.Length == 0) return;
                string cur = _bonus.text ?? "";
                if (cur.Contains("<color=#F5C752><b>")) return;
                _bonus.text = cur + "\n<color=" + Theme.GoldHex + ">" + Names.Text(m.Why1) + "</color>";
                if (!_whySaid) { _whySaid = true; Plugin.Logger.LogInfo("[loadout] why shown #" + _number + " for " + _hlId + " inline in currentBadgeBonus: " + LoadoutView.Visible(m.Why1)); }
            }
            catch { }
        }

        // ---- the summary rows ----
        internal static string RowText(SummaryRow r)
        {
            string label = "<b><color=" + Theme.GoldHex + ">" + r.Label + "</color></b>";
            if (r.Items.Count == 0) return label;
            return label + "  " + string.Join(" <color=" + Theme.DimHex + ">" + r.Sep + "</color> ", r.Items.Select(i => "<color=" + (i.Equipped ? Theme.WhiteHex : Theme.GoldHex) + ">" + Names.Text(i.Text) + "</color>"));
        }

        // 0.15.x (C-m3 of the 10-07 review): where the band holds one row (1280 x 800, 1280 x 720) the second row - SWAP OUT, CLOSE CALLS,
        // MATCH, FREE SLOTS - was dropped without a word; it now joins the EQUIP row when both fit the band's width at the 15 px floor
        // ("EQUIP 1 Gunner · 2 Critical  |  SWAP OUT Bomber · Tough"), else the drawn line says it was left out
        static string _sumNote = "";

        static void DrawSummary(RectTransform view, bool first)
        {
            _sumNote = "";
            if (_lo.SumAt == "none") { Hide(_sum); return; }
            var rows = _view.Rows.ToList();
            if (_lo.Dropped.Contains("yard")) rows.RemoveAll(r => r.Kind == "yard" || r.Kind == "unlock");
            SummaryRow second = null;
            if (_lo.Dropped.Contains("swap") && rows.Count > 1) { second = rows[1]; rows.RemoveAt(1); }
            if (rows.Count > _lo.SumRows) rows = rows.Take(_lo.SumRows).ToList();
            if (rows.Count == 0) { Hide(_sum); return; }
            bool alive = false; try { alive = _sum != null && _sum.gameObject != null; } catch { }
            if (!alive)
            {
                _sum = BuildBand(view, SumName, 3, out _sumFrom); if (_sum == null) return;
                _rows.Clear(); for (int i = 0; i < 3; i++) _rows.Add(_sum.Find("Line" + i).GetComponent<TextMeshProUGUI>());
                // the summary sits on the screen's own ground: a gold rule over the rows with a diamond on its left end (TreeUi's strip)
                var bar = _sum.Find("Bar"); if (bar != null) bar.gameObject.SetActive(false);
                var tr = _sum.Find("TipR"); if (tr != null) tr.gameObject.SetActive(false);
            }
            float f = _lo.SumFontUnits, line = LoadoutLayout.LineFactor * f, pad = Tip / 2f + 12f;
            float minU = Mathf.Max(LoadoutLayout.MinShrink * f, LoadoutLayout.MinPx / Mathf.Max(0.0001f, _unitPx)); if (minU > f) minU = f;
            float width = _lo.Sum.W - 2f * pad;
            var texts = rows.Select(RowText).ToList();
            if (second != null && texts.Count == 1)
            {
                // the second row on the first one's line, if both fit the width at the smallest the rows may shrink to (the floor)
                string bar = "|"; try { var font = _rows[0].font; if (font != null && !font.HasCharacter('|', true, true)) bar = "/"; } catch { }
                string joined = texts[0] + "   <color=" + Theme.DimHex + ">" + bar + "</color>   " + RowText(second);
                float jw = 0f; try { var t0 = _rows[0]; t0.fontSize = f; t0.text = joined; t0.ForceMeshUpdate(); jw = t0.preferredWidth; } catch { jw = 0f; }
                if (jw > 0f && jw * minU / f <= width) { texts[0] = joined; _sumNote = " | swap row joined to row 1 (" + jw.ToString("0", IC) + " of " + width.ToString("0", IC) + " u)"; }
                else _sumNote = " | " + second.Kind + " row dropped (one row fits; joined it needs " + (jw * minU / f).ToString("0", IC) + " of " + width.ToString("0", IC) + " u at the floor)";
            }
            else if (second != null) _sumNote = " | " + second.Kind + " row dropped";
            // shrink to fit the widest row, never under the 15 px floor; past that the row ends in "..."
            float widest = 0f;
            for (int i = 0; i < texts.Count; i++) { var t = _rows[i]; t.fontSize = f; t.text = texts[i]; try { t.ForceMeshUpdate(); widest = Mathf.Max(widest, t.preferredWidth); } catch { } }
            if (widest > width && widest > 0f) f = Mathf.Max(minU, f * width / widest);
            line = LoadoutLayout.LineFactor * f;
            float h = rows.Count * line + 8f;
            PlaceBand(_sum, view, new R4(_lo.Sum.X0, _lo.Sum.Y0, _lo.Sum.X1, _lo.Sum.Y0 + h), h);
            for (int i = 0; i < 3; i++)
            {
                var t = _rows[i];
                if (i >= texts.Count) { t.gameObject.SetActive(false); continue; }
                t.gameObject.SetActive(true);
                t.fontSize = f; t.text = texts[i];
                if (!first && !Fx.Busy("loadout:sum" + i)) { try { t.maxVisibleCharacters = 99999; } catch { } }      // a type-on cut short (a hide) never leaves a row half written; one still running finishes by itself
                var rt = t.rectTransform; rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(pad, -(i + 1) * line - 8f); rt.offsetMax = new Vector2(-pad, -i * line - 8f);
            }
            try
            {
                var rule = _sum.Find("Rule").TryCast<RectTransform>();
                rule.anchorMin = new Vector2(0, 1); rule.anchorMax = new Vector2(1, 1); rule.pivot = new Vector2(0f, 1f);
                rule.offsetMin = new Vector2(Tip / 2f, -3f); rule.offsetMax = new Vector2(-Tip / 2f, 0f);
                var tip = _sum.Find("TipL").TryCast<RectTransform>(); tip.anchorMin = tip.anchorMax = new Vector2(0f, 1f); tip.anchoredPosition = new Vector2(Tip / 2f, -1.5f);
                tip.sizeDelta = new Vector2(Tip * 0.5f, Tip * 0.5f);
                if (first) { Fx.Draw("loadout:sumrule", rule, 0.35f, 0.45f); Fx.Spin("loadout:sumtip", tip, 0.35f, 0.5f, 0.5f); }
            }
            catch { }
            _sum.gameObject.SetActive(true); _sum.SetAsLastSibling();
            if (first) for (int i = 0; i < texts.Count; i++) Fx.Type("loadout:sum" + i, _rows[i], 0.45f + 0.12f * i, 160f);
        }

        // ================================================================================================ the run start line
        static bool _runArmed = true, _visitSinceRun; static IntPtr _runMaster; static float _runClock, _nextRun; static string _ctxSuffix;
        /// <summary>When the current run's gameplay first read active (Time.realtimeSinceStartup); -1 = not yet.</summary>
        public static float RunStartedAt = -1f;

        static void RunStartTick(float now)
        {
            if (now < _nextRun) return;
            _nextRun = now + Throttle;
            GameplayMaster gm = null; try { gm = GameplayMaster.s_instance; } catch { }
            if (gm == null) { _runArmed = true; return; }
            GameMode mode = null; try { mode = gm.currentGameMode; } catch { }
            if (mode == null) return;                                  // the main menu's master has no mode
            bool active = false; float t = 0f;
            try { active = mode.IsGameplayActive; t = mode.CurrentModePlayTime; } catch { return; }
            IntPtr p = Ptr(gm);
            if (p != _runMaster) { _runMaster = p; _runArmed = true; _ctxSuffix = null; }
            if (t + 5f < _runClock) { _runArmed = true; _ctxSuffix = null; }     // the clock went back: Try Again keeps the scene
            _runClock = t;
            if (!active || !_runArmed) return;
            _runArmed = false;
            if (t > 5f) return;                                         // joined a run already going (a reload): no run start line
            RunStartedAt = now;
            string leader = "?", m = "?"; int diff = 1;
            try { leader = G.ClassName(gm.currentMainCharacterType); } catch { }
            try { m = mode.gameplayMode.ToString(); diff = (int)mode.difficulty + 1; } catch { }
            if (!_visitSinceRun) Once("novisit:" + p, "this run started without a run setup visit (Quick Run, Repeat Run or Try Again)");
            _visitSinceRun = false;
            string badges = RunBadgesText(leader, m, diff);
            Plugin.Logger.LogInfo("[loadout] run start: " + leader + " " + m + " d" + diff + " | badges " + badges);
        }

        static string RunBadgesText(string leader, string mode, int diff)
        {
            var list = LoadoutState.RunBadges();
            if (list == null) return "unreadable";
            string names = list.Count == 0 ? "none" : string.Join(", ", list.Select(x => x.Key.Short + " " + x.Value));
            string advice;
            if (Last == null || Last.Advice == null) advice = "no advice this session";
            else if (Last.Leader != leader || Last.Mode != mode || Last.Difficulty != diff) advice = "advice #" + Last.Number + " was for " + Last.Leader + " " + Last.Mode + " d" + Last.Difficulty;
            else
            {
                var advised = Last.Advice.Picks.Where(pk => !pk.Forced).Select(pk => pk.Badge.Id).ToList();
                int k = advised.Count(id => list.Any(x => x.Key.Id == id));
                advice = "advice #" + Last.Number + ": " + k + " of " + advised.Count;
            }
            _ctxSuffix = " | badges: " + names + " (" + advice + ")";
            return names + " (" + advice + ")";
        }

        /// <summary>The [ctx] line's suffix (Advisor.cs, every offer): this run's badges and how they compare with the advice.</summary>
        public static string CtxSuffix()
        {
            try
            {
                if (_ctxSuffix != null) return _ctxSuffix;
                var gm = GameplayMaster.s_instance; if (gm == null || gm.currentGameMode == null) return "";
                string leader = G.ClassName(gm.currentMainCharacterType), m = gm.currentGameMode.gameplayMode.ToString(); int diff = (int)gm.currentGameMode.difficulty + 1;
                RunBadgesText(leader, m, diff);
                return _ctxSuffix ?? "";
            }
            catch { return ""; }
        }

        // ================================================================================================ failures
        static void Fail(string where, Exception e)
        {
            _errors++;
            string msg = e.GetType().Name + ": " + e.Message;
            if (_errSaid.Add(msg)) Plugin.Logger.LogWarning("[loadout] " + where + ": " + e);
            if (_errors < 3 || _off) return;
            _off = true;
            try { HideAll(); LoadoutEquip.Hide(); } catch { }
            Plugin.Logger.LogWarning("[loadout] switched off for this session after 3 errors (first: " + e.GetType().Name + " in " + where + ") - the screen is left as the game draws it");
        }

        // ================================================================================================ load-time
        internal static MethodBase Hook(string role)
        {
            try
            {
                switch (role)
                {
                    case "tick": return AccessTools.DeclaredMethod(typeof(UIViewRunSetup), "Update");
                    case "cursor": return AccessTools.DeclaredMethod(typeof(UIViewRunSetup), "OnBadgeHighlight", new[] { typeof(UIViewRunSetupBadgeButton) });
                    case "cursor2": return AccessTools.DeclaredMethod(typeof(UIViewRunSetupBadgeButton), "OnHighlight");
                    case "dirty": return AccessTools.DeclaredMethod(typeof(UIViewRunSetup), "RefreshSelectedBadgeButtons");
                    case "close": return AccessTools.DeclaredMethod(typeof(UIViewRunSetup), "OnDisable");
                    case "fallback": return AccessTools.DeclaredMethod(typeof(GameMaster), "Update");
                }
            }
            catch { }
            return null;
        }

        /// <summary>At load: which of the hooks took ("[loadout] hooks: tick UIViewRunSetup.Update ok | ...").</summary>
        public static void LogHooks()
        {
            var parts = new List<string>(); bool all = true;
            foreach (var role in new[] { "tick", "cursor", "cursor2", "dirty", "close", "fallback" })
            {
                var m = Hook(role);
                string name = m == null ? "?" : m.DeclaringType.Name + "." + m.Name;
                string state;
                if (m == null) { state = "missing"; all = false; }
                else
                {
                    bool ours = false;
                    try { var info = Harmony.GetPatchInfo(m); ours = info != null && info.Postfixes.Any(p => p.owner == Plugin.GUID); } catch { }
                    state = ours ? "ok" : "not patched"; if (!ours) all = false;
                }
                parts.Add(role + " " + name + " " + state);
            }
            string line = "[loadout] hooks: " + string.Join(" | ", parts);
            if (all) Plugin.Logger.LogInfo(line); else Plugin.Logger.LogWarning(line);
        }

        // ================================================================================================ the debug walk's stage ([Debug] PreviewSetup)
        // On the run setup screen: wait for the first 'drawn' line (3 s at most), photograph the entrance and the screen, then move
        // the game's own cursor (view.OnBadgeHighlight - what the mouse does when it moves over a badge) onto the first advised
        // badge, an equipped badge the advice would swap out and a locked badge, photographing the WHY line each time; then log
        // the layout and the cursor sources. It never clicks a badge. Menu.cs drives the wizard around it.
        sealed class Walk { public float Armed, Next; public int Stage; }
        static Walk _walk;
        public static bool WalkArmed { get { return _walk != null; } }
        public static bool WalkDone;

        public static void ArmWalk()
        {
            if (_walk != null || WalkDone) return;
            _walk = new Walk { Armed = Time.realtimeSinceStartup, Next = 0f };
            Plugin.Logger.LogInfo("[loadout] setup walk: at the run setup screen, waiting for the advice to be drawn");
        }

        static void WalkHighlight(UIViewRunSetup view, LoadoutCell cell, string what, string shot)
        {
            if (cell == null) { Plugin.Logger.LogInfo("[loadout] setup walk: no " + what + " to highlight"); return; }
            Plugin.Logger.LogInfo("[loadout] setup walk: highlight " + what + " - " + cell.Id + " " + (cell.Facts != null ? cell.Facts.Short : "?"));
            LoadoutState.MoveCursor(view, cell.Button);
            Shots.Later(0.5f, shot, true);
        }

        static void WalkTick(UIViewRunSetup view, float now)
        {
            if (now < _walk.Next) return;
            try
            {
                switch (_walk.Stage)
                {
                    case 0:
                        if ((!_drawnThisVisit || _visit == null) && now - _walk.Armed < 3f) { _walk.Next = now + 0.1f; return; }
                        Plugin.Logger.LogInfo("[loadout] setup walk: " + (_drawnThisVisit ? "drawn #" + (_advice != null ? _advice.Number : 0) : "no drawn line within 3 s"));
                        // the entrance once more, for the motion frames
                        foreach (var pl in _placed.Values) pl.State = "";
                        if (_visit != null && _view != null) { DrawMarks(true); try { if (_sum != null && _sum.gameObject.activeSelf) for (int i = 0; i < _rows.Count; i++) Fx.Type("loadout:sum" + i, _rows[i], 0.45f + 0.12f * i, 160f); } catch { } }
                        for (int f = 0; f < 4; f++) Shots.Later(0.05f + 0.1f * f, "fxsetup" + f, true);
                        // 10-07 review (walk-shot nit): 2.0 s - at 1.0 s the replayed entrance was mid-way (no swap-out diamonds, no slot
                        // mirror, one summary row typed); the first highlight waits for it
                        Shots.Later(2.0f, "setup0_loadout", true);
                        _walk.Stage = 1; _walk.Next = now + 2.6f; return;
                    case 1:
                        {
                            LoadoutCell cell = null;
                            var first = _advice == null ? null : _advice.Picks.FirstOrDefault(p => !p.Forced);
                            if (first != null && _visit != null) cell = _visit.CellOf(first.Badge.Id);
                            WalkHighlight(view, cell, "EQUIP #1", "setup1_why");
                            _walk.Stage = 2; _walk.Next = now + 1.3f; return;
                        }
                    case 2:
                        {
                            LoadoutCell cell = null;
                            if (_visit != null && _view != null)
                            {
                                cell = _visit.Grid.FirstOrDefault(c => { var m = _view.MarkOf(c.Id); return m != null && (m.Kind == MarkKind.SwapOut || m.Kind == MarkKind.KeepClose); });
                                if (cell == null) cell = _visit.Grid.FirstOrDefault(c => _visit.Equipped.Contains(c.Id) && _advice.PickOf(c.Id) == null);
                            }
                            WalkHighlight(view, cell, "an equipped badge that is not advised", "setup2_why_swap");
                            _walk.Stage = 3; _walk.Next = now + 1.3f; return;
                        }
                    case 3:
                        {
                            LoadoutCell cell = _visit == null ? null : _visit.Grid.FirstOrDefault(c => c.Locked || (_visit.Levels.ContainsKey(c.Id) && _visit.Levels[c.Id] == 0));
                            WalkHighlight(view, cell, "a locked badge", "setup3_why_locked");
                            _walk.Stage = 4; _walk.Next = now + 1.3f; return;
                        }
                    default:
                        LayoutLine();
                        Plugin.Logger.LogInfo("[loadout] " + CursorLine());
                        Plugin.Logger.LogInfo("[loadout] setup walk: run setup stage done (no badge was clicked; the slot markers' stand-ins written in " + _followWrites + " of " + _followFrames + " frames)");
                        WalkDone = true; _walk = null; return;
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[loadout] setup walk: " + e.Message); WalkDone = true; _walk = null; }
        }

        /// <summary>The stage did not get its screen in time (Menu.cs gives up after 14 s): end it.</summary>
        public static void WalkGiveUp() { if (_walk != null) Plugin.Logger.LogWarning("[loadout] setup walk: the stage did not finish in time"); _walk = null; WalkDone = true; }
    }

    // ---- the hooks (SPEC 6.1): all post-fixes, all reads; a missing target is skipped (Prepare) and named in '[loadout] hooks' ----
    [HarmonyPatch]
    static class P_SetupTick
    {
        static bool Prepare() { return LoadoutUi.Hook("tick") != null; }
        static MethodBase TargetMethod() { return LoadoutUi.Hook("tick"); }
        static void Postfix(UIViewRunSetup __instance) { LoadoutUi.Tick(__instance, true); }
    }

    [HarmonyPatch]
    static class P_SetupCursor
    {
        static bool Prepare() { return LoadoutUi.Hook("cursor") != null; }
        static MethodBase TargetMethod() { return LoadoutUi.Hook("cursor"); }
        static void Postfix(UIViewRunSetupBadgeButton __0) { LoadoutUi.Highlighted(__0, 0); }
    }

    [HarmonyPatch]
    static class P_SetupButtonCursor
    {
        static bool Prepare() { return LoadoutUi.Hook("cursor2") != null; }
        static MethodBase TargetMethod() { return LoadoutUi.Hook("cursor2"); }
        static void Postfix(UIViewRunSetupBadgeButton __instance) { LoadoutUi.Highlighted(__instance, 1); }
    }

    [HarmonyPatch]
    static class P_SetupDirty
    {
        static bool Prepare() { return LoadoutUi.Hook("dirty") != null; }
        static MethodBase TargetMethod() { return LoadoutUi.Hook("dirty"); }
        static void Postfix() { LoadoutUi.Dirty(); }
    }

    [HarmonyPatch]
    static class P_SetupGone
    {
        static bool Prepare() { return LoadoutUi.Hook("close") != null; }
        static MethodBase TargetMethod() { return LoadoutUi.Hook("close"); }
        static void Postfix(UIViewRunSetup __instance) { LoadoutUi.Gone(__instance); }
    }
}
