// The design preview (config [Debug] Preview, off by default): about five seconds after launch, on the main menu,
// the PLAN sidebar is built on an overlay canvas with sample plans and photographed at each stage - two survivors,
// the change highlight after a pick (0.35 / 1.5 / 3.6 s in), a full squad (the tall case), and the fade-out - so the
// look can be judged from screenshots without playing a run. Ticked from GameMaster.Update (alive in every scene).
// The overlay canvas scales like the HUD canvas (3840x2160 reference, expanded to the screen), so units match.
//
// [Debug] PreviewResolution (e.g. 3440x1440 for the PC, 1280x800 for the Steam Deck) makes the captures show the
// sidebar with the pixels it would have on that screen even when the game runs on a different desktop: the frame is
// rendered at a whole multiple of the window (ScreenCapture's supersize) and the block's scale is set so that one
// canvas unit ends up as many pixels as on the target screen, at the target's automatic scale. Only the frame around
// the block (the menu art) differs from the real thing.
using System;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace YazsCompanion
{
    internal static class Preview
    {
        const float Ref = 2160f, RefW = 3840f;        // the game's canvas reference (Expand: the smaller ratio wins)
        const float Font = 31f, MinTextPx = 15f;      // the sidebar's font and its readability floor (Panel.Scale)

        static bool _done;
        static int _stage;
        static float _at = -1f;
        static GameObject _canvasGo;

        public static bool Enabled { get { try { return Plugin.PreviewFlag != null && Plugin.PreviewFlag.Value; } catch { return false; } } }

        public static void Tick()
        {
            if (_done || !Enabled) return;
            try
            {
                float now = Time.realtimeSinceStartup;
                Panel.Animate();
                if (_at < 0) { _at = now + 5f; return; }       // let the menu settle
                if (now < _at) return;
                switch (_stage)
                {
                    case 0:     // the overlay canvas; its scaler applies on the next frame, so build one stage later
                        if (OverlayCanvas() == null) { _at = now + 2f; return; }
                        _at = now + 0.3f; _stage = 1; break;
                    case 1:
                        {
                            var canvas = OverlayCanvas();
                            var template = Ui.FindLabel(null, null);
                            if (canvas == null || template == null) { Plugin.Logger.LogWarning("[preview] no canvas or label yet, retrying"); _at = now + 2f; return; }
                            float scaleOverride = Emulation(canvas);
                            Panel.CheckGlyphs(template);   // before the sample plans, so they use the glyphs the font has
                            if (!Panel.PreviewBuild(canvas, template, Plan.Sample(0), scaleOverride)) { Plugin.Logger.LogWarning("[preview] sidebar could not be built"); Finish(); return; }
                            Plugin.Logger.LogInfo("[preview] sidebar with two survivors, cloned label '" + template.name + "'");
                            Shots.Later(1.0f, "preview_a", true);
                            _at = now + 4f; _stage = 2; break;
                        }
                    case 2:
                        Panel.PreviewApply(Plan.Sample(1));    // the weapon maxed and tags up: two changed lines highlighted
                        Plugin.Logger.LogInfo("[preview] change highlight");
                        Shots.Later(0.35f, "preview_hl1", true); Shots.Later(1.5f, "preview_hl2", true); Shots.Later(3.6f, "preview_hl3", true);
                        _at = now + 5f; _stage = 3; break;
                    case 3:
                        Panel.PreviewApply(Plan.Sample(2));    // a full squad: nine lines, the case that must stay above the minimap
                        Plugin.Logger.LogInfo("[preview] full squad");
                        Shots.Later(3.6f, "preview_full", true);
                        _at = now + 5f; _stage = 4; break;
                    case 4:
                        Panel.PreviewHide();
                        Shots.Later(0.08f, "preview_fade", true);
                        _at = now + 1.5f; _stage = 5; break;
                    default:
                        Finish();
                        break;
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[preview] " + e); Finish(); }
        }

        static void Finish()
        {
            try { Panel.PreviewEnd(); } catch { }
            try { if (_canvasGo != null) UnityEngine.Object.Destroy(_canvasGo); } catch { }
            _canvasGo = null; _done = true; Shots.SuperSize = 1;
            Plugin.Logger.LogInfo("[preview] done");
        }

        // the supersize and block scale that reproduce the target screen's pixels; 0 = no target, the real automatic scale
        static float Emulation(RectTransform canvas)
        {
            string want = ""; try { want = (Plugin.PreviewResolution.Value ?? "").Trim().ToLowerInvariant(); } catch { }
            Shots.SuperSize = 1;
            if (want.Length == 0) return 0f;
            try
            {
                var parts = want.Split('x');
                float tw = float.Parse(parts[0]), th = float.Parse(parts[1]);
                float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;
                float unitHere = sh / canvas.rect.height;                              // pixels per canvas unit on this desktop
                float unitThere = Mathf.Min(tw / RefW, th / Ref);                      // ... and on the target screen (Expand)
                float autoThere = Mathf.Clamp(MinTextPx / (Font * unitThere), 1f, 2f);  // the sidebar's automatic scale there
                float want_px = unitThere * autoThere;                                 // pixels per sidebar unit wanted
                int size = Mathf.Max(1, Mathf.FloorToInt(want_px / unitHere));
                float scale = want_px / (unitHere * size);
                Shots.SuperSize = size;
                Plugin.Logger.LogInfo("[preview] emulating " + tw + "x" + th + " (auto x" + autoThere.ToString("0.00") + ", " + (Font * want_px).ToString("0.0") + " px text) on "
                    + sw + "x" + sh + ": supersize " + size + ", block x" + scale.ToString("0.00"));
                return scale;
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[preview] PreviewResolution '" + want + "': " + e.Message); return 0f; }
        }

        static RectTransform OverlayCanvas()
        {
            try
            {
                if (_canvasGo != null) return _canvasGo.transform.TryCast<RectTransform>();
                var go = new GameObject("YazsPreview");
                var canvas = go.AddComponent(Il2CppType.Of<Canvas>()).TryCast<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 31000;
                var scaler = go.AddComponent(Il2CppType.Of<CanvasScaler>()).TryCast<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(RefW, Ref);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // like the HUD canvas: 3840x2400 on the Deck, 5161x2160 on 21:9
                _canvasGo = go;
                return go.transform.TryCast<RectTransform>();
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[preview] canvas: " + e.Message); return null; }
        }
    }
}
