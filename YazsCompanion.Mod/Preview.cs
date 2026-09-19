// The design preview (config [Debug] Preview, off by default): about five seconds after launch, on the main menu,
// the PLAN readout is built on an overlay canvas with sample plans and photographed at each stage - two survivors,
// the change highlight after a pick (0.35 / 1.5 / 3.6 s in), a full squad, the same settled at PanelIdle, the Full
// detail level (the tall case), and the fade-out - so the look can be judged from screenshots without playing a run.
// Ticked from GameMaster.Update (alive in every scene). The overlay canvas scales like the HUD canvas (3840x2160
// reference, expanded to the screen), so units match.
//
// A gameplay frame next to the DLL is drawn behind the readout when there is one, because what matters is how much of
// the field the readout hides and the menu art cannot tell: preview_bg_<WIDTH>x<HEIGHT>.jpg / .png for the emulated
// screen (drawn at that screen's pixels from the bottom-left corner), else preview_bg.jpg / .png stretched.
//
// [Debug] PreviewResolution (e.g. 3440x1440 for the PC, 1280x800 for the Steam Deck) makes the captures show the
// sidebar with the pixels it would have on that screen even when the game runs on a different desktop: the frame is
// rendered at a whole multiple of the window (ScreenCapture's supersize) and the block's scale is set so that one
// canvas unit ends up as many pixels as on the target screen, at the target's automatic scale. Only the frame around
// the block (the menu art) differs from the real thing.
using System;
using System.IO;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
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
        static float _posScale = 1f;                  // emulation: canvas units here per canvas unit on the target screen
        static float _bgW, _bgH;                      // emulation: the target screen in canvas units here (0 = no target)

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
                            Backdrop(canvas);
                            Panel.CheckGlyphs(template);   // before the sample plans, so they use the glyphs the font has
                            if (!Panel.PreviewBuild(canvas, template, Plan.Sample(0, true), scaleOverride, _posScale, _bgH)) { Plugin.Logger.LogWarning("[preview] readout could not be built"); Finish(); return; }
                            Plugin.Logger.LogInfo("[preview] compact, two survivors, cloned label '" + template.name + "'");
                            Shots.Later(1.0f, "preview_a", true);
                            _at = now + 2.5f; _stage = 2; break;
                        }
                    case 2:
                        Panel.PreviewApply(Plan.Sample(1, true));    // the weapon maxed and tags up: two changed lines highlighted
                        Plugin.Logger.LogInfo("[preview] change highlight");
                        Shots.Later(0.35f, "preview_hl1", true); Shots.Later(1.5f, "preview_hl2", true); Shots.Later(3.6f, "preview_hl3", true);
                        _at = now + 4.2f; _stage = 3; break;
                    case 3:
                        Panel.PreviewFresh(); Panel.PreviewApply(Plan.Sample(2, true));    // a full squad, compact
                        Plugin.Logger.LogInfo("[preview] full squad");
                        Shots.Later(0.8f, "preview_squad", true);
                        _at = now + 1.2f; _stage = 4; break;
                    case 4:
                        Panel.PreviewDoze();                   // the resting state: nothing changed for a while
                        Shots.Later(1.8f, "preview_idle", true);
                        _at = now + 2.2f; _stage = 5; break;
                    case 5:
                        Panel.PreviewFresh(); Panel.PreviewApply(Plan.Sample(2, false));   // Full detail: nine lines, the tall case
                        Plugin.Logger.LogInfo("[preview] full detail");
                        Shots.Later(0.8f, "preview_detail", true);
                        _at = now + 1.2f; _stage = 6; break;
                    case 6:
                        Panel.PreviewHide();
                        Shots.Later(0.08f, "preview_fade", true);
                        _at = now + 1.5f; _stage = 7; break;
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
            Shots.SuperSize = 1; _posScale = 1f; _bgW = _bgH = 0f;
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
                _posScale = unitThere / (unitHere * size);
                _bgW = tw / (unitHere * size); _bgH = th / (unitHere * size);
                Plugin.Logger.LogInfo("[preview] emulating " + tw + "x" + th + " (auto x" + autoThere.ToString("0.00") + ", " + (Font * want_px).ToString("0.0") + " px text) on "
                    + sw + "x" + sh + ": supersize " + size + ", block x" + scale.ToString("0.00"));
                return scale;
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[preview] PreviewResolution '" + want + "': " + e.Message); return 0f; }
        }

        // a gameplay frame behind the readout (see the header); silently absent when there is no file or no decoder
        static void Backdrop(RectTransform canvas)
        {
            try
            {
                string dir = Plugin.PluginDir, path = null;
                var names = new System.Collections.Generic.List<string>();
                string want = ""; try { want = (Plugin.PreviewResolution.Value ?? "").Trim().ToLowerInvariant(); } catch { }
                if (_bgW > 0 && want.Length > 0) { names.Add("preview_bg_" + want + ".jpg"); names.Add("preview_bg_" + want + ".png"); }
                names.Add("preview_bg.jpg"); names.Add("preview_bg.png");
                foreach (var n in names) { var f = Path.Combine(dir, n); if (File.Exists(f)) { path = f; break; } }
                if (path == null) return;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.hideFlags = HideFlags.HideAndDontSave;
                if (!ImageConversion.LoadImage(tex, (Il2CppStructArray<byte>)File.ReadAllBytes(path))) { Plugin.Logger.LogInfo("[preview] backdrop not decoded: " + path); return; }
                var rt = Ui.Image(canvas, "Backdrop", Color.white);
                var img = rt.GetComponent<Image>();
                img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                bool sized = _bgW > 0 && want.Length > 0 && Path.GetFileName(path).Contains(want);
                if (sized) { rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero; rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(_bgW, _bgH); }
                else Ui.Stretch(rt, 0, 0, 0, 0);
                rt.SetAsFirstSibling();
                Plugin.Logger.LogInfo("[preview] backdrop " + Path.GetFileName(path) + " " + tex.width + "x" + tex.height + (sized ? " at the target's pixels" : " stretched"));
            }
            catch (Exception e) { Plugin.Logger.LogInfo("[preview] no backdrop (" + e.Message + ")"); }
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
