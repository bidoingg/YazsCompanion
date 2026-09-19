// The live PLAN readout during play, drawn like the HUD's own quest tracker rather than like a menu panel: a small
// gold title over a hairline that fades out, then the rows - a label column (the survivor's name, TAGS / SOS / GRAB)
// and a value column that wraps under itself - on a soft dark backing that dissolves towards the middle of the
// screen and has no frame, so the surroundings stay readable (0.6.0's framed, nearly opaque 800-unit panel hid too
// much of the field). It sits in the empty bottom-left corner under the weapon and ability icons by default (the
// right edge between the items and the minimap is still available), is only as wide as its text, fades in and out,
// and dims to PanelIdle when nothing has changed for a few seconds. Compact detail (the default) is one row per
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
            public readonly List<PlanLine> Rows = new List<PlanLine>();
        }

        static RectTransform _root, _content, _head;
        static CanvasGroup _fade;
        static bool _flip, _compact = true;           // at the right edge (backing dissolves to the left); one row per survivor
        static float _margin = 1f;                    // 1 live; the preview's screen emulation scales the margins
        static float _level = 1f, _awakeUntil;        // idle dimming: current strength, and until when the readout is at full strength
        static TextMeshProUGUI _template;
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
        static bool _glyphsChecked;
        static string _sig = "", _stateKey = "", _gates = "";
        static UIGameplayUpgradeSelection _screen;
        static bool _visible, _warnedNoCanvas, _warnedNoLabel, _sourceLogged, _preview;

        public static void ScreenOpened(UIGameplayUpgradeSelection sel) { _screen = sel; SetVisible(false); }
        /// <summary>The base Hide(clicked) ran (validated: once per screen, every type); the game may keep the screen
        /// object active while it animates out, so do not wait for activeInHierarchy to drop.</summary>
        public static void ScreenClosed() { _screen = null; _nextRebuild = 0f; }   // rebuild (and highlight) as soon as the sidebar is back

        /// <summary>The HUD is being destroyed (scene change): our objects die with it, forget them.</summary>
        public static void Reset() { Forget(); _screen = null; _hud = null; _gates = ""; }
        static void Forget()
        {
            _root = null; _content = null; _head = null; _fade = null; _template = null; _groups.Clear(); _rules.Clear();
            _sig = ""; _stateKey = ""; _visible = false; _alpha = 0f; _target = 0f; _level = 1f; _awakeUntil = 0f;
            _plan = null; _prev = null; _changed.Clear(); _hlStart = -100f;
        }

        /// <summary>Once per frame from UIGameplay.Update (hud set) and GameplayMaster.Update (hud null); throttled to 0.4 s here.</summary>
        public static void Tick(UIGameplay hud)
        {
            try
            {
                if (hud != null) _hud = hud; else hud = _hud;
                if (!_sourceLogged) { _sourceLogged = true; Plugin.Logger.LogInfo("[panel] first tick from " + (hud != null ? "UIGameplay.Update" : "GameplayMaster.Update")); }
                float now = Time.realtimeSinceStartup;
                Animate();
                Shots.Tick();
                if (_visible) Shots.Baseline();
                if (_preview) return;                     // the menu preview owns the widget
                if (now < _nextTick) return;
                _nextTick = now + 0.4f;
                if (!Plugin.ShowPanel.Value) { SetVisible(false); return; }

                GameplayMaster master = null; try { master = GameplayMaster.s_instance; } catch { }
                int players = 0; try { if (master != null && master.gamePlayers != null) players = master.gamePlayers.Count; } catch { }
                string active = "?"; try { active = master == null ? "nomaster" : master.currentGameMode == null ? "nomode" : master.currentGameMode.IsGameplayActive.ToString(); } catch { active = "err"; }
                string paused = "?"; try { paused = GameplayMaster.IsPaused.ToString(); } catch { paused = "err"; }
                string hudVisible = "?"; try { if (master != null) hudVisible = master.IsGameplayUIVisible().ToString(); } catch { hudVisible = "err"; }
                string selecting = "?"; try { if (hud != null) selecting = hud.IsDisplayingUpgradeSelection().ToString(); } catch { selecting = "err"; }
                bool screenUp = false; try { screenUp = _screen != null && _screen.gameObject.activeInHierarchy; } catch { _screen = null; }
                string pauseMenu = "?"; try { pauseMenu = GameplayMaster.IsPauseMenuFlowActive.ToString(); } catch { pauseMenu = "err"; }
                string defeat = "?"; try { defeat = GameplayMaster.IsDefeatResultsFlowActive.ToString(); } catch { defeat = "err"; }

                string gates = "players=" + players + " active=" + active + " paused=" + paused + " pauseMenu=" + pauseMenu + " defeat=" + defeat
                    + " hudVisible=" + hudVisible + " selecting=" + selecting + " screen=" + screenUp + " hud=" + (hud != null);
                if (gates != _gates) { _gates = gates; Plugin.Logger.LogInfo("[panel] " + gates); }

                // see the header comment: every one of these reads false during play and true on some screen
                bool viewUp = players == 0 || screenUp || selecting == "True" || paused == "True" || pauseMenu == "True" || defeat == "True" || hudVisible == "True";
                if (viewUp) { SetVisible(false); return; }
                if (!Ensure(hud)) return;
                SetVisible(true);   // before the rebuild: an inactive TMP object skips its mesh update, and Layout measures the text

                if (now >= _nextRebuild)
                {
                    _nextRebuild = now + 2f;
                    var snap = G.Read();
                    string key = StateKey(snap);
                    if (key == _stateKey) return;
                    _stateKey = key;
                    bool compact = true; try { compact = Plugin.PanelDetail.Value == PanelDetailLevel.Compact; } catch { }
                    var plan = Plan.Build(snap, compact);
                    if (plan.Signature != _sig)
                    {
                        _sig = plan.Signature;
                        Apply(plan, now);
                        Plugin.Logger.LogInfo("[plan] " + snap.Clock + ": " + plan.PlainText() + (_changed.Count > 0 ? "  [changed: " + string.Join(", ", _changed) + "]" : ""));
                        if (_hlStart > 0) { Shots.Later(0.3f, "hl1"); Shots.Later(1.5f, "hl2"); Shots.Later(3.5f, "hl3"); }
                        else Shots.Later(0.5f, "plan");
                    }
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[panel] " + e); _nextTick = Time.realtimeSinceStartup + 5f; }
        }

        /// <summary>The per-frame part: the fade and the highlight's colour ramp. Called from Tick and from the preview.</summary>
        public static void Animate()
        {
            float now = Time.realtimeSinceStartup;
            float dt = _lastFrame > 0 ? Mathf.Clamp(now - _lastFrame, 0f, 0.1f) : 0f;
            _lastFrame = now;
            Fade(dt);
            if (_hlStart > 0 && _target > 0 && now >= _nextFade) { _nextFade = now + 0.033f; Render(now); }   // the gold ramp, ~30 fps
        }

        // everything the plan depends on: squad members with weapons, abilities, levels, items, tree levels, and the active quest
        static string StateKey(Snapshot s)
        {
            string quest = "";
            try { var qm = GameQuestManager.Get; var q = qm == null ? null : qm.ActiveQuest; if (q != null) quest = q.Pointer.ToString(); } catch { }
            return s.Squad.Count + "|" + s.SquadText() + "|" + quest + "|" + s.Tags.Key();
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
                    _root.gameObject.SetActive(true);
                    Wake(4f);
                }
                Plugin.Logger.LogInfo("[panel] " + (v ? "shown" : "hidden"));
                Shots.Later(v ? 0.5f : 0.3f, v ? "shown" : "hidden");
            }
            catch { Forget(); }
        }

        /// <summary>Full strength for the next <paramref name="seconds"/>; afterwards the readout settles at PanelIdle.</summary>
        static void Wake(float seconds) { _awakeUntil = Mathf.Max(_awakeUntil, Time.realtimeSinceStartup + seconds); }

        static void Fade(float dt)
        {
            if (_root == null || _fade == null) return;
            float idle = 0.7f; try { idle = Mathf.Clamp(Plugin.PanelIdle.Value, 0.25f, 1f); } catch { }
            float level = Time.realtimeSinceStartup < _awakeUntil ? 1f : idle;
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
            Forget();
            var canvas = FindCanvas(hud);
            if (canvas == null)
            {
                if (!_warnedNoCanvas) { _warnedNoCanvas = true; Plugin.Logger.LogWarning("[panel] no HUD canvas (UIGameplay) to attach to"); }
                return false;
            }
            var template = Ui.FindLabel(canvas, PreferredLabels);
            if (template == null)
            {
                if (!_warnedNoLabel) { _warnedNoLabel = true; Plugin.Logger.LogWarning("[panel] no HUD text to clone yet"); }
                return false;
            }
            _warnedNoLabel = false;

            float scale = Scale(canvas);
            _autoScale = scale; try { _canvasH = canvas.rect.height; } catch { _canvasH = 0; }
            if (!Build(canvas, template, scale)) return false;
            string geo = "";
            try { geo = " canvas " + canvas.rect.width.ToString("0") + "x" + canvas.rect.height.ToString("0") + " screen " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height + " scale " + canvas.lossyScale.x.ToString("0.000"); } catch { }
            Plugin.Logger.LogInfo("[panel] created under " + canvas.name + " using label '" + template.name + "' at (" + _root.anchoredPosition.x + ", " + _root.anchoredPosition.y + ") x" + scale.ToString("0.00") + geo);
            Shots.Later(0.5f, "created");
            return true;
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
            var rule = Ui.FadeRule(head, "Rule", Theme.GoldLine); rule.anchorMin = new Vector2(0, 0); rule.anchorMax = new Vector2(1, 0); rule.pivot = new Vector2(0.5f, 0);
            rule.anchoredPosition = Vector2.zero; rule.sizeDelta = new Vector2(0, HeadRule);
            Ui.Diamond(head, "Tip", 0, 0.5f, HeadDiamond, Theme.Gold).anchoredPosition = new Vector2(HeadDiamond / 2, 2f);
            var title = Ui.CloneText(template, head, "Title");
            if (title == null) { UnityEngine.Object.Destroy(root.gameObject); return false; }
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

            float hlSeconds = 3f; try { hlSeconds = Plugin.PanelHighlight.Value; } catch { }
            _hlStart = _changed.Count > 0 && hlSeconds > 0 ? now : -100f;
            Wake(_changed.Count > 0 ? Mathf.Max(6f, hlSeconds + 3f) : 5f);   // new advice is shown at full strength, then settles
            Render(now);
            Layout();
        }

        // the text of every group: "LABEL<indent>value</indent>" rows, the changed values in the highlight colour
        // (gold held for 0.8 s, then easing to white)
        static void Render(float now)
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
            var sb = new StringBuilder();
            foreach (var g in _groups)
            {
                if (g.Rows.Count == 0 || g.Text == null) continue;
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
                    sb.Append("<indent=").Append(LabelW).Append('>');
                    if (hl && _changed.Contains(l.Key)) sb.Append("<color=").Append(hex).Append('>').Append(l.Text).Append("</color>");
                    else sb.Append(l.Text);
                    sb.Append("</indent>");
                }
                g.Text.text = sb.ToString();
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
                if (band > 0 && total * scale > band) scale = Mathf.Max(MinScale, band / total);
            }
            catch { }
            if (Mathf.Abs(_root.localScale.x - scale) > 0.005f)
            {
                _root.localScale = new Vector3(scale, scale, 1f);
                Plugin.Logger.LogInfo("[panel] scale x" + scale.ToString("0.00") + " for " + total.ToString("0") + " units (auto x" + _autoScale.ToString("0.00") + ")");
            }
        }

        // one canvas unit is Screen.height / canvas height pixels (1/3 on the Deck, 2/3 on a 1440p monitor): enlarge the
        // block until its 31-unit font is at least MinTextPx tall; PanelScale in the config overrides the automatic value
        const float MinTextPx = 15f;
        static float Scale(RectTransform canvas)
        {
            float fixedScale = 0; try { fixedScale = Plugin.PanelScale.Value; } catch { }
            if (fixedScale > 0) return Mathf.Clamp(fixedScale, 0.5f, 3f);
            try
            {
                float h = canvas.rect.height; if (h <= 0) return 1f;
                float px = Font * UnityEngine.Screen.height / h;
                return Mathf.Clamp(MinTextPx / px, 1f, 2f);
            }
            catch { return 1f; }
        }

        // ---- the design preview on the main menu (Preview.cs): the same widget on an overlay canvas with sample plans ----
        public static bool PreviewBuild(RectTransform canvas, TextMeshProUGUI template, Plan plan, float scaleOverride, float posScale, float canvasH)
        {
            Forget(); _preview = true;
            float scale = scaleOverride > 0 ? scaleOverride : Scale(canvas);
            _autoScale = scale; try { _canvasH = canvasH > 0 ? canvasH : canvas.rect.height; } catch { _canvasH = 0; }
            _margin = posScale;
            if (!Build(canvas, template, scale)) { _preview = false; return false; }
            _root.anchoredPosition = _root.anchoredPosition * posScale;     // an emulated screen: its margins in this canvas's units
            SetVisible(true);
            Apply(plan, Time.realtimeSinceStartup);
            Plugin.Logger.LogInfo("[panel] preview built x" + scale.ToString("0.00") + ", " + _root.sizeDelta.y.ToString("0") + " units tall");
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
            Forget(); _preview = false; _margin = 1f;
        }
    }
}
