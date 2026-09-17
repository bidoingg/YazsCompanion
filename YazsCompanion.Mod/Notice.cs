// The restart notice: a thin strip at the top centre of the screen on its own overlay canvas that
// survives scene changes, shown from the moment an update has been downloaded until the game is
// restarted. Ticked from GameMaster.Update (alive in every scene) and, as a backup, from the sidebar tick.
// Styled like the RECOMMENDED ribbon: a dark plate, gold text, a gold rule underneath and a diamond on each end.
using System;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YazsCompanion
{
    internal static class Notice
    {
        const float StripW = 1700f, StripH = 76f, Tip = 30f;

        static GameObject _go;
        static TextMeshProUGUI _text;
        static float _next;
        static string _shown = "";

        public static void Tick()
        {
            try
            {
                float now = Time.realtimeSinceStartup;
                if (now < _next) return;
                _next = now + 1f;
                string pending = Updater.Pending;
                if (pending == null) return;
                bool alive = false; try { alive = _go != null && _text != null && _go.activeInHierarchy; } catch { }
                if (!alive && !Create()) return;
                string msg = "YAZS COMPANION " + pending + " DOWNLOADED   •   RESTART THE GAME TO APPLY";
                if (msg != _shown) { _text.text = msg; _shown = msg; Plugin.Logger.LogInfo("[notice] " + msg); }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[notice] " + e.Message); _next = Time.realtimeSinceStartup + 10f; }
        }

        static bool Create()
        {
            var template = Ui.FindLabel(null, null);
            if (template == null) return false;

            var go = new GameObject("YazsNotice");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var canvas = go.AddComponent(Il2CppType.Of<Canvas>()).TryCast<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = go.AddComponent(Il2CppType.Of<CanvasScaler>()).TryCast<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3840f, 2160f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = go.transform.TryCast<RectTransform>();
            if (root == null) { UnityEngine.Object.Destroy(go); return false; }

            var strip = Ui.NewRect("Strip", root);
            strip.anchorMin = strip.anchorMax = new Vector2(0.5f, 1f); strip.pivot = new Vector2(0.5f, 1f);
            strip.anchoredPosition = new Vector2(0f, -28f); strip.sizeDelta = new Vector2(StripW, StripH);
            var bg = Ui.Image(strip, "Bg", Theme.Plate); Ui.Stretch(bg, Tip / 2, 0, Tip / 2, 0);
            var rule = Ui.Image(strip, "Rule", Theme.Gold); rule.anchorMin = new Vector2(0, 0); rule.anchorMax = new Vector2(1, 0); rule.pivot = new Vector2(0.5f, 0);
            rule.anchoredPosition = Vector2.zero; rule.sizeDelta = new Vector2(-Tip, 4f);
            Ui.Diamond(strip, "TipL", 0, 0.5f, Tip, Theme.Gold);
            Ui.Diamond(strip, "TipR", 1, 0.5f, Tip, Theme.Gold);
            var text = Ui.CloneText(template, strip, "Text");
            if (text == null) { UnityEngine.Object.Destroy(go); return false; }
            Ui.Stretch(text.rectTransform, Tip, 0, Tip, 0);
            text.fontSize = 36f; text.fontStyle = FontStyles.Bold; text.alignment = TextAlignmentOptions.Center;
            text.color = Theme.GoldText; text.characterSpacing = 4f;

            _go = go; _text = text; _shown = "";
            Plugin.Logger.LogInfo("[notice] overlay created using label '" + template.name + "'");
            return true;
        }
    }
}
