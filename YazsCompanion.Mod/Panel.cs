// The live sidebar during play: a right-aligned block under the item icons and above the minimap,
// styled like the game's own quest-objective text (right edge, no heavy chrome), with a small
// diamond-tipped PLAN header matching the card ribbon.
//
// Ticked from the HUD's own Update (UIGameplay, the object that owns the gameplay canvas) and, as a
// fallback, from GameplayMaster.Update; parented to that canvas so it dies with the HUD. The plan is
// rebuilt only when the squad state changes (a cheap key: squad text + active quest), because a
// rebuild walks every skill tree node and every item.
//
// 0.4.2 is a diagnostic build: the sidebar has never been seen in a run, so every signal that could
// hide it is logged whenever it changes ("[panel] players=.. active=.. ..."), and only the signals
// already proven by the card badges actually hide it (no players in the run, our own tracking of an
// open selection screen). The others (IsGameplayActive, IsPaused, IsGameplayUIVisible,
// IsDisplayingUpgradeSelection) are observed first and will gate again once the log shows what they
// really say during play, on pause and on the results screen.
using System;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YazsCompanion
{
    internal static class Panel
    {
        const string RootName = "YazsPlan";
        const float Width = 640f, PadTop = 92f, PadSide = 26f, PadBottom = 22f, Font = 31f, LineH = 42f;
        const float HeaderW = 240f, HeaderH = 52f, HeaderTip = 32f;
        // HUD labels worth cloning first (font, material, outline of the quest box / timer), by object name
        static readonly string[] PreferredLabels = { "Quest_Obj1", "QuestName", "QuestStatus_Text", "GameTimer_Txt" };
        static readonly Color Gold = new Color(0.96f, 0.78f, 0.32f, 1f);
        static readonly Color GoldText = new Color(0.98f, 0.84f, 0.42f, 1f);
        static readonly Color Backdrop = new Color(0.02f, 0.02f, 0.03f, 0.55f);
        static readonly Color RibbonBg = new Color(0.05f, 0.045f, 0.04f, 0.94f);

        static RectTransform _root;
        static TextMeshProUGUI _text;
        static UIGameplay _hud;                       // remembered from UIGameplay.Update ticks for the fallback ticks
        static float _nextTick, _nextRebuild;
        static string _sig = "", _stateKey = "", _gates = "";
        static UIGameplayUpgradeSelection _screen;
        static bool _visible, _warnedNoCanvas, _warnedNoLabel, _sourceLogged;

        public static void ScreenOpened(UIGameplayUpgradeSelection sel) { _screen = sel; SetVisible(false); }
        /// <summary>The base Hide(clicked) ran (validated: once per screen, every type); the game may keep the screen
        /// object active while it animates out, so do not wait for activeInHierarchy to drop.</summary>
        public static void ScreenClosed() { _screen = null; }

        /// <summary>The HUD is being destroyed (scene change): our objects die with it, forget them.</summary>
        public static void Reset() { Forget(); _screen = null; _hud = null; _gates = ""; }
        static void Forget() { _root = null; _text = null; _sig = ""; _stateKey = ""; _visible = false; }

        /// <summary>Once per frame from UIGameplay.Update (hud set) and GameplayMaster.Update (hud null); throttled to 0.4 s here.</summary>
        public static void Tick(UIGameplay hud)
        {
            try
            {
                if (hud != null) _hud = hud; else hud = _hud;
                if (!_sourceLogged) { _sourceLogged = true; Plugin.Logger.LogInfo("[panel] first tick from " + (hud != null ? "UIGameplay.Update" : "GameplayMaster.Update")); }
                float now = Time.realtimeSinceStartup;
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

                // diagnostic build: hide only on what the badges already proved (see the header comment)
                if (players == 0 || screenUp) { SetVisible(false); return; }
                if (!Ensure(hud)) return;
                SetVisible(true);   // before the rebuild: an inactive TMP object skips its mesh update, and Resize measures the text

                if (now >= _nextRebuild)
                {
                    _nextRebuild = now + 2f;
                    var snap = G.Read();
                    string key = StateKey(snap);
                    if (key == _stateKey) return;
                    _stateKey = key;
                    var plan = Plan.Build(snap);
                    if (plan.Signature != _sig)
                    {
                        _sig = plan.Signature;
                        _text.text = string.Join("\n", plan.Lines);
                        Resize(plan.Lines.Count);
                        Plugin.Logger.LogInfo("[plan] " + snap.Clock + ": " + plan.PlainText());
                    }
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[panel] " + e); _nextTick = Time.realtimeSinceStartup + 5f; }
        }

        // everything the plan depends on: squad members with weapons, abilities, levels, items, tree levels, and the active quest
        static string StateKey(Snapshot s)
        {
            string quest = "";
            try { var qm = GameQuestManager.Get; var q = qm == null ? null : qm.ActiveQuest; if (q != null) quest = q.Pointer.ToString(); } catch { }
            return s.Squad.Count + "|" + s.SquadText() + "|" + quest;
        }

        static void SetVisible(bool v)
        {
            if (_root == null || _visible == v) return;
            try { _root.gameObject.SetActive(v); _visible = v; Plugin.Logger.LogInfo("[panel] " + (v ? "shown" : "hidden")); } catch { Forget(); }
        }

        static bool Alive()
        {
            try { return _root != null && _text != null && _root.gameObject != null; } catch { return false; }
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
            var template = FindHudLabel(canvas);
            if (template == null)
            {
                if (!_warnedNoLabel) { _warnedNoLabel = true; Plugin.Logger.LogWarning("[panel] no HUD text to clone yet"); }
                return false;
            }
            _warnedNoLabel = false;

            var root = NewRect(RootName, canvas);
            root.anchorMin = root.anchorMax = new Vector2(1f, 1f); root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-Plugin.PanelRight.Value, -Plugin.PanelTop.Value);
            root.sizeDelta = new Vector2(Width, 300f);

            var bg = Image(root, "Backdrop", Backdrop); Stretch(bg, 0, 0, 0, 0);

            // header ribbon, same look as the card ribbon
            var head = NewRect("Header", root);
            head.anchorMin = head.anchorMax = new Vector2(1f, 1f); head.pivot = new Vector2(1f, 1f);
            head.anchoredPosition = new Vector2(-PadSide + HeaderTip / 2, -18f); head.sizeDelta = new Vector2(HeaderW, HeaderH);
            var bar = Image(head, "Bar", RibbonBg); Stretch(bar, HeaderTip / 2, 0, HeaderTip / 2, 0);
            var rule = Image(head, "Rule", Gold); rule.anchorMin = new Vector2(0, 0); rule.anchorMax = new Vector2(1, 0); rule.pivot = new Vector2(0.5f, 0);
            rule.anchoredPosition = Vector2.zero; rule.sizeDelta = new Vector2(-HeaderTip, 4f);
            Diamond(head, "TipL", 0, 0.5f, HeaderTip);
            Diamond(head, "TipR", 1, 0.5f, HeaderTip);
            var title = CloneText(template, head, "Title");
            if (title != null)
            {
                Stretch(title.rectTransform, HeaderTip, 0, HeaderTip, 0);
                title.text = "PLAN"; title.color = GoldText; title.fontSize = 34f; title.fontStyle = FontStyles.Bold;
                title.alignment = TextAlignmentOptions.Center; title.characterSpacing = 6f;
            }

            var text = CloneText(template, root, "Text");
            if (text == null) { UnityEngine.Object.Destroy(root.gameObject); return false; }
            Stretch(text.rectTransform, PadSide, PadBottom, PadSide, PadTop);
            text.fontSize = Font; text.fontStyle = FontStyles.Normal; text.alignment = TextAlignmentOptions.TopRight;
            text.color = Color.white;
            try { text.enableWordWrapping = true; } catch { }
            try { text.overflowMode = TextOverflowModes.Overflow; } catch { }
            text.text = "";

            _root = root; _text = text; _visible = true;
            string geo = "";
            try { geo = " canvas " + canvas.rect.width.ToString("0") + "x" + canvas.rect.height.ToString("0") + " screen " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height +" scale " + canvas.lossyScale.x.ToString("0.000"); } catch { }
            Plugin.Logger.LogInfo("[panel] created under " + canvas.name + " using label '" + template.name + "' at (" + root.anchoredPosition.x + ", " + root.anchoredPosition.y + ")" + geo);
            return true;
        }

        static void Resize(int lines)
        {
            float h = PadTop + Mathf.Max(1, lines) * LineH + PadBottom;
            try { _text.ForceMeshUpdate(); float ph = _text.preferredHeight; if (ph > 0) h = PadTop + ph + PadBottom; } catch { }
            _root.sizeDelta = new Vector2(Width, h);
        }

        // a live HUD label to clone (font, material, outline): the quest box or timer text by preference,
        // else any active TextMeshProUGUI under the gameplay canvas, else any active one at all
        static TextMeshProUGUI FindHudLabel(Transform canvas)
        {
            TextMeshProUGUI preferred = null, under = null, any = null; int bestPref = int.MaxValue;
            try
            {
                var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<TextMeshProUGUI>());
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i].TryCast<TextMeshProUGUI>();
                    if (t == null) continue;
                    bool live; try { live = t.gameObject.activeInHierarchy && t.font != null; } catch { continue; }
                    if (!live) continue;
                    bool isUnder = false; try { isUnder = t.transform.IsChildOf(canvas); } catch { }
                    if (isUnder)
                    {
                        int pref = int.MaxValue; try { pref = Array.IndexOf(PreferredLabels, t.name); } catch { }
                        if (pref >= 0 && pref < bestPref) { bestPref = pref; preferred = t; }
                        if (under == null) under = t;
                    }
                    else if (any == null) any = t;
                }
            }
            catch { }
            return preferred ?? under ?? any;
        }

        static TextMeshProUGUI CloneText(TextMeshProUGUI template, RectTransform parent, string name)
        {
            var go = UnityEngine.Object.Instantiate(template.gameObject, parent);
            go.name = name;
            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                var comp = comps[i];
                if (comp.TryCast<TextMeshProUGUI>() == null && comp.TryCast<RectTransform>() == null && comp.TryCast<CanvasRenderer>() == null)
                    UnityEngine.Object.Destroy(comp);
            }
            for (int i = go.transform.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(go.transform.GetChild(i).gameObject);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (text == null) { UnityEngine.Object.Destroy(go); return null; }
            var rt = text.rectTransform; rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity;
            text.enableAutoSizing = false; text.richText = true; text.raycastTarget = false;
            text.text = "";
            go.SetActive(true);
            return text;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent(Il2CppType.Of<RectTransform>()).TryCast<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity; rt.localPosition = Vector3.zero;
            return rt;
        }
        static RectTransform Image(RectTransform parent, string name, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent(Il2CppType.Of<Image>()).TryCast<Image>();
            img.color = color; img.raycastTarget = false;
            return rt;
        }
        static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
        }
        static void Diamond(RectTransform parent, string name, float ax, float ay, float size)
        {
            var rt = Image(parent, name, Gold);
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(size * 0.7071f, size * 0.7071f);
            rt.localRotation = Quaternion.Euler(0, 0, 45f);
        }
    }
}
