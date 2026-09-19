// The shared look of everything the mod draws (the PLAN sidebar, the card badges, the restart notice) and the
// uGUI primitives they are built from. The palette follows the game's own panels: a warm near-black body, thin
// gold hairlines, small gold diamonds on corners and tips, condensed uppercase labels. Text is always a clone of
// one of the game's TextMeshPro labels so it inherits the font, material and outline the game uses on that screen.
using System;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YazsCompanion
{
    internal static class Theme
    {
        public static readonly Color Gold = new Color(0.96f, 0.78f, 0.32f, 1f);          // the game's gold (#F5C752)
        public static readonly Color GoldText = new Color(0.98f, 0.84f, 0.42f, 1f);
        public static readonly Color GoldLine = new Color(0.96f, 0.78f, 0.32f, 0.85f);    // panel frames and header rules
        public static readonly Color GoldRule = new Color(0.96f, 0.78f, 0.32f, 0.36f);    // hairlines between groups
        public static readonly Color Body = new Color(0.035f, 0.032f, 0.03f, 0.84f);      // panel body (flat fallback)
        public static readonly Color BodyTop = new Color(0.055f, 0.05f, 0.045f, 0.92f);   // panel body gradient, top
        public static readonly Color BodyBottom = new Color(0.025f, 0.023f, 0.022f, 0.85f);
        public static readonly Color Scrim = new Color(0.02f, 0.018f, 0.016f, 1f);        // the PLAN readout's soft backing (alpha from the config)
        public static readonly Color Plate =new Color(0.05f, 0.045f, 0.04f, 0.94f);      // ribbon and strip plates
        public static readonly Color Band = new Color(1f, 1f, 1f, 0.045f);               // header band tint over the body
        public static readonly Color White = new Color(0.93f, 0.93f, 0.93f, 1f);
        public static readonly Color Grey = new Color(0.66f, 0.66f, 0.66f, 1f);
        public static readonly Color Cream = new Color(0.93f, 0.88f, 0.72f, 1f);
        public static readonly Color Rust = new Color(0.80f, 0.45f, 0.40f, 1f);
        public static readonly Color HlGold = new Color(1f, 0.85f, 0.40f, 1f);           // a changed sidebar line, before it fades to White
        public const string GoldHex = "#F5C752", DimHex = "#A6A6A6", WhiteHex = "#EDEDED";

        public static string Hex(Color c)
        {
            return "#" + ((int)(c.r * 255)).ToString("X2") + ((int)(c.g * 255)).ToString("X2") + ((int)(c.b * 255)).ToString("X2");
        }
    }

    internal static class Ui
    {
        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent(Il2CppType.Of<RectTransform>()).TryCast<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity; rt.localPosition = Vector3.zero;
            return rt;
        }

        public static RectTransform Image(RectTransform parent, string name, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent(Il2CppType.Of<Image>()).TryCast<Image>();
            img.color = color; img.raycastTarget = false;
            return rt;
        }

        public static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>A bar along one edge of <paramref name="parent"/> (anchors given), e.g. one side of a frame.</summary>
        public static RectTransform Edge(RectTransform parent, string name, float ax0, float ay0, float ax1, float ay1, Vector2 pos, Vector2 size, Color color)
        {
            var rt = Image(parent, name, color);
            rt.anchorMin = new Vector2(ax0, ay0); rt.anchorMax = new Vector2(ax1, ay1); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        /// <summary>A small diamond (a rotated square) centred on the anchor point (ax, ay) of <paramref name="parent"/>.</summary>
        public static RectTransform Diamond(RectTransform parent, string name, float ax, float ay, float size, Color color)
        {
            var rt = Image(parent, name, color);
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(size * 0.7071f, size * 0.7071f);
            rt.localRotation = Quaternion.Euler(0, 0, 45f);
            return rt;
        }

        /// <summary>A hairline frame on the parent's rect inset by <paramref name="inset"/>, with a solid diamond on each
        /// corner when <paramref name="corner"/> is positive: the frame the game draws around its panels and cards.</summary>
        public static RectTransform Frame(RectTransform parent, string name, float inset, float thick, Color line, float corner)
        {
            var f = NewRect(name, parent);
            Stretch(f, inset, inset, inset, inset);
            Edge(f, "Top", 0, 1, 1, 1, new Vector2(0, -thick / 2), new Vector2(0, thick), line);
            Edge(f, "Bottom", 0, 0, 1, 0, new Vector2(0, thick / 2), new Vector2(0, thick), line);
            Edge(f, "Left", 0, 0, 0, 1, new Vector2(thick / 2, 0), new Vector2(thick, 0), line);
            Edge(f, "Right", 1, 0, 1, 1, new Vector2(-thick / 2, 0), new Vector2(thick, 0), line);
            if (corner > 0)
            {
                var solid = new Color(line.r, line.g, line.b, 1f);
                Diamond(f, "TL", 0, 1, corner, solid);
                Diamond(f, "TR", 1, 1, corner, solid);
                Diamond(f, "BL", 0, 0, corner, solid);
                Diamond(f, "BR", 1, 0, corner, solid);
            }
            return f;
        }

        static Sprite _gradient;
        /// <summary>A 1 x N vertical gradient sprite for panel bodies (null when the texture API is unavailable; use a
        /// flat colour then). Cached: every panel shares the same texture.</summary>
        public static Sprite VerticalGradient(Color top, Color bottom, int steps)
        {
            try { if (_gradient != null && _gradient.texture != null) return _gradient; } catch { _gradient = null; }
            try
            {
                var tex = new Texture2D(1, steps, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
                tex.hideFlags = HideFlags.HideAndDontSave;
                for (int y = 0; y < steps; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, (float)y / (steps - 1)));   // y = 0 is the bottom
                tex.Apply();
                var sprite = Sprite.Create(tex, new Rect(0, 0, 1, steps), new Vector2(0.5f, 0.5f));
                sprite.hideFlags = HideFlags.HideAndDontSave;
                _gradient = sprite;
                return sprite;
            }
            catch (Exception e) { Plugin.Logger.LogInfo("[ui] gradient unavailable (" + e.Message + "), flat body"); return null; }
        }

        static readonly Sprite[] _fades = new Sprite[3];
        /// <summary>A white sprite whose alpha is solid on the left and eases out to nothing on the right (the last 45 %),
        /// for backings that dissolve into the game instead of ending in an edge; tint it with Image.color. Kind 0 = the
        /// ramp alone, 1 = the ramp feathered out towards its top edge, 2 = towards its bottom edge. Null when the
        /// texture API is unavailable.</summary>
        public static Sprite FadeSprite(int kind)
        {
            try { if (_fades[kind] != null && _fades[kind].texture != null) return _fades[kind]; } catch { _fades[kind] = null; }
            try
            {
                const int W = 64, H = 16;
                int h = kind == 0 ? 1 : H;
                var tex = new Texture2D(W, h, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
                tex.hideFlags = HideFlags.HideAndDontSave;
                for (int x = 0; x < W; x++)
                {
                    float u = (float)x / (W - 1);
                    float k = Mathf.Clamp01((u - 0.55f) / 0.45f);
                    float ax = 1f - k * k * (3f - 2f * k);
                    for (int y = 0; y < h; y++)
                    {
                        float v = h == 1 ? 1f : (float)y / (h - 1);             // y = 0 is the bottom row
                        float ay = kind == 1 ? 1f - v : kind == 2 ? v : 1f;
                        ay = ay * ay * (3f - 2f * ay);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, ax * ay));
                    }
                }
                tex.Apply();
                var sprite = Sprite.Create(tex, new Rect(0, 0, W, h), new Vector2(0.5f, 0.5f));
                sprite.hideFlags = HideFlags.HideAndDontSave;
                _fades[kind] = sprite;
                return sprite;
            }
            catch (Exception e) { Plugin.Logger.LogInfo("[ui] fade sprite unavailable (" + e.Message + "), flat backing"); return null; }
        }

        static RectTransform FadeImage(RectTransform parent, string name, Color color, int kind)
        {
            var rt = Image(parent, name, color);
            var sprite = FadeSprite(kind);
            if (sprite != null) { try { var img = rt.GetComponent<Image>(); img.sprite = sprite; img.type = UnityEngine.UI.Image.Type.Simple; } catch { } }
            return rt;
        }

        /// <summary>A soft backing for text drawn over the live game: <paramref name="color"/> under the text, dissolving
        /// to the right and feathered over <paramref name="feather"/> units at the top and bottom, so there is no box to
        /// look at. Stretched over <paramref name="parent"/>; <paramref name="flip"/> dissolves to the left instead.</summary>
        public static RectTransform Scrim(RectTransform parent, string name, Color color, float feather, bool flip)
        {
            var s = NewRect(name, parent);
            Stretch(s, 0, 0, 0, 0);
            if (flip) s.localScale = new Vector3(-1f, 1f, 1f);
            var top = FadeImage(s, "Top", color, 1);
            top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); top.pivot = new Vector2(0.5f, 1f);
            top.anchoredPosition = Vector2.zero; top.sizeDelta = new Vector2(0, feather);
            var mid = FadeImage(s, "Mid", color, 0);
            Stretch(mid, 0, feather, 0, feather);
            var bottom = FadeImage(s, "Bottom", color, 2);
            bottom.anchorMin = new Vector2(0, 0); bottom.anchorMax = new Vector2(1, 0); bottom.pivot = new Vector2(0.5f, 0f);
            bottom.anchoredPosition = Vector2.zero; bottom.sizeDelta = new Vector2(0, feather);
            return s;
        }

        /// <summary>A hairline that is solid on the left and fades out to the right (the rule under a HUD title).</summary>
        public static RectTransform FadeRule(RectTransform parent, string name, Color color)
        {
            return FadeImage(parent, name, color, 0);
        }

        /// <summary>Clone one of the game's labels to inherit its font, material and canvas settings; strip everything but
        /// the text component. Wrapping is off and overflow visible; callers change that as they need.</summary>
        public static TextMeshProUGUI CloneText(TextMeshProUGUI template, RectTransform parent, string name)
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
            try { text.enableWordWrapping = false; } catch { }
            try { text.overflowMode = TextOverflowModes.Overflow; } catch { }
            text.text = "";
            go.SetActive(true);
            return text;
        }

        /// <summary>A live label to clone: by preference one of the named objects under <paramref name="under"/>, else any
        /// active label under it, else any active label at all, else an inactive one with a font (menus between scenes).</summary>
        public static TextMeshProUGUI FindLabel(Transform under, string[] preferred)
        {
            TextMeshProUGUI best = null, underAny = null, any = null, inactive = null; int bestPref = int.MaxValue;
            try
            {
                var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<TextMeshProUGUI>());
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i].TryCast<TextMeshProUGUI>();
                    if (t == null) continue;
                    bool live; try { if (t.font == null) continue; live = t.gameObject.activeInHierarchy; } catch { continue; }
                    if (!live) { if (inactive == null) inactive = t; continue; }
                    bool isUnder = false; if (under != null) { try { isUnder = t.transform.IsChildOf(under); } catch { } }
                    if (isUnder)
                    {
                        int pref = preferred == null ? -1 : Array.IndexOf(preferred, t.name);
                        if (pref >= 0 && pref < bestPref) { bestPref = pref; best = t; }
                        if (underAny == null) underAny = t;
                    }
                    else if (any == null) any = t;
                }
            }
            catch { }
            return best ?? underAny ?? any ?? inactive;
        }
    }
}
