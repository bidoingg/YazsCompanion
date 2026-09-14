// The restart notice: a thin strip at the top centre of the screen on its own overlay canvas that
// survives scene changes, shown from the moment an update has been downloaded until the game is
// restarted. Ticked from GameMaster.Update (alive in every scene) and, as a backup, from the sidebar tick.
using System;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YazsCompanion
{
    internal static class Notice
    {
        static readonly Color Gold = new Color(0.96f, 0.78f, 0.32f, 1f);
        static readonly Color GoldText = new Color(0.98f, 0.84f, 0.42f, 1f);
        static readonly Color Bg = new Color(0.03f, 0.03f, 0.035f, 0.88f);

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
            var template = AnyLabel();
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

            var strip = NewRect("Strip", root);
            strip.anchorMin = strip.anchorMax = new Vector2(0.5f, 1f); strip.pivot = new Vector2(0.5f, 1f);
            strip.anchoredPosition = new Vector2(0f, -28f); strip.sizeDelta = new Vector2(1700f, 76f);
            var bg = Image(strip, "Bg", Bg); Stretch(bg, 0, 0, 0, 0);
            var rule = Image(strip, "Rule", Gold); rule.anchorMin = new Vector2(0, 0); rule.anchorMax = new Vector2(1, 0); rule.pivot = new Vector2(0.5f, 0);
            rule.anchoredPosition = Vector2.zero; rule.sizeDelta = new Vector2(0f, 4f);
            var text = CloneText(template, strip, "Text");
            if (text == null) { UnityEngine.Object.Destroy(go); return false; }
            Stretch(text.rectTransform, 30, 0, 30, 0);
            text.fontSize = 36f; text.fontStyle = FontStyles.Bold; text.alignment = TextAlignmentOptions.Center;
            text.color = GoldText; text.characterSpacing = 4f;
            try { text.enableWordWrapping = false; } catch { }
            try { text.overflowMode = TextOverflowModes.Overflow; } catch { }

            _go = go; _text = text; _shown = "";
            Plugin.Logger.LogInfo("[notice] overlay created using label '" + template.name + "'");
            return true;
        }

        static TextMeshProUGUI AnyLabel()
        {
            try
            {
                var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<TextMeshProUGUI>());
                TextMeshProUGUI fallback = null;
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i].TryCast<TextMeshProUGUI>();
                    if (t == null) continue;
                    bool live; try { live = t.gameObject.activeInHierarchy && t.font != null; } catch { continue; }
                    if (live) return t;
                    if (fallback == null) { try { if (t.font != null) fallback = t; } catch { } }
                }
                return fallback;
            }
            catch { return null; }
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
    }
}
