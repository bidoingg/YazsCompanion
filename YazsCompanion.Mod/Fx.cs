// Motion for everything the mod draws: a tiny tween runner (one tick per frame from whichever game Update is alive,
// unscaled time so it also runs while the game is paused on a selection screen) and the effects built from the game's
// own vocabulary - diamonds that stamp in, a gold diamond "ping" that expands and fades like a sonar ring, rules that
// draw themselves from the left, text that types on like a field terminal, a glint running along a gold line.
// During play the PLAN readout only moves when it appears or when its advice changes; nothing loops over the field.
// [General] Motion = false puts every element straight into its end pose.
using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using TMPro;

namespace YazsCompanion
{
    internal static class Fx
    {
        // Each tween has its own clock, advanced by the frame time capped at 50 ms: a hitch (the frame that builds a
        // widget, a scene load) delays an entrance instead of swallowing it.
        sealed class Tween { public string Key; public float Age, Delay, Dur; public bool Loop; public Action<float> Step; public Action Done; }
        static readonly List<Tween> _tweens = new List<Tween>();
        static int _frame = -1;
        static float _last = -1f;

        /// <summary>The tween clock was ticked within the last half second (some game Update we ride on is alive).</summary>
        static bool Ticking { get { return _last > 0f && Time.realtimeSinceStartup - _last < 0.5f; } }

        public static bool On { get { try { return Plugin.Motion.Value; } catch { return true; } } }

        public static float OutCubic(float k) { k = 1f - Mathf.Clamp01(k); return 1f - k * k * k; }
        public static float OutBack(float k) { k = Mathf.Clamp01(k) - 1f; return 1f + 2.70158f * k * k * k + 1.70158f * k * k; }
        public static float Smooth(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }

        /// <summary>Run <paramref name="step"/> with k from 0 to 1 over <paramref name="dur"/> seconds after <paramref name="delay"/>.
        /// The start pose is applied at once, so nothing shows in its end pose while it waits. A step that throws (its
        /// object died with a screen) ends the tween.</summary>
        public static void Run(string key, float delay, float dur, Action<float> step, Action done = null)
        {
            // no clock, no start pose: an element must never wait, hidden, for a tween that is not going to run
            if (!On || !Ticking) { try { step(1f); if (done != null) done(); } catch { } return; }
            try { step(0f); } catch { return; }
            _tweens.Add(new Tween { Key = key, Delay = delay, Dur = Mathf.Max(0.01f, dur), Step = step, Done = done });
        }

        /// <summary>Call <paramref name="step"/> with k cycling 0..1 every <paramref name="period"/> seconds until cancelled or until it throws.</summary>
        public static void Loop(string key, float period, Action<float> step)
        {
            if (!On || !Ticking) return;
            _tweens.Add(new Tween { Key = key, Dur = Mathf.Max(0.05f, period), Loop = true, Step = step });
        }

        /// <summary>Drop every tween whose key starts with <paramref name="prefix"/> (their objects keep the pose they have).</summary>
        public static void Cancel(string prefix)
        {
            for (int i = _tweens.Count - 1; i >= 0; i--) if (_tweens[i].Key != null && _tweens[i].Key.StartsWith(prefix, StringComparison.Ordinal)) _tweens.RemoveAt(i);
        }

        /// <summary>Once per frame, from any of the game's Updates (the frame counter makes extra calls free).</summary>
        public static void Tick()
        {
            int frame = Time.frameCount;
            if (frame == _frame) return;
            _frame = frame;
            float now = Time.realtimeSinceStartup;
            float dt = _last < 0f ? 0f : Mathf.Clamp(now - _last, 0f, 0.05f);
            _last = now;
            for (int i = _tweens.Count - 1; i >= 0; i--)
            {
                var t = _tweens[i];
                t.Age += dt;
                float k = (t.Age - t.Delay) / t.Dur;
                if (k < 0f) continue;
                try
                {
                    if (t.Loop) { t.Step(k - Mathf.Floor(k)); continue; }
                    t.Step(Mathf.Clamp01(k));
                    if (k >= 1f) { _tweens.RemoveAt(i); if (t.Done != null) t.Done(); }
                }
                catch
                {   // a step that throws ends its tween - in its end pose when the object still takes it, never half way in
                    if (i < _tweens.Count && _tweens[i] == t) _tweens.RemoveAt(i);
                    if (!t.Loop) { try { t.Step(1f); } catch { } }
                }
            }
        }

        // ---- effects ----

        /// <summary>A hollow gold diamond centred on <paramref name="parent"/> that can expand and fade: the ping. Returned
        /// with its CanvasGroup at 0; drive it with <see cref="PingPose"/>. <paramref name="diamond"/> = false when the parent
        /// is already a rotated diamond.</summary>
        public static RectTransform Ring(RectTransform parent, float size, float thick, Color color, bool diamond, out CanvasGroup group)
        {
            var ring = Ui.NewRect("YazsPing", parent);
            ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f); ring.pivot = new Vector2(0.5f, 0.5f);
            ring.anchoredPosition = Vector2.zero; ring.sizeDelta = new Vector2(size * 0.7071f, size * 0.7071f);
            if (diamond) ring.localRotation = Quaternion.Euler(0, 0, 45f);
            Ui.Frame(ring, "Edge", 0, thick, color, 0);
            group = ring.gameObject.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>();
            group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
            return ring;
        }

        /// <summary>The ping at progress k: grows to <paramref name="reach"/> times its size while fading out.</summary>
        public static void PingPose(RectTransform ring, CanvasGroup group, float k, float reach)
        {
            float s = 1f + (reach - 1f) * OutCubic(k);
            ring.localScale = new Vector3(s, s, 1f);
            group.alpha = k >= 1f ? 0f : (1f - k) * (1f - k) * 0.9f;
        }

        /// <summary>One ping on <paramref name="parent"/>, then the ring is destroyed.</summary>
        public static void Ping(string key, RectTransform parent, float size, float thick, Color color, bool diamond, float delay, float dur, float reach, bool behind)
        {
            if (!On) return;
            try
            {
                CanvasGroup g; var ring = Ring(parent, size, thick, color, diamond, out g);
                if (behind) ring.SetAsFirstSibling();
                Run(key, delay, dur, k => PingPose(ring, g, k, reach), () => { try { UnityEngine.Object.Destroy(ring.gameObject); } catch { } });
            }
            catch { }
        }

        /// <summary>The text types on, left to right, at <paramref name="cps"/> characters a second (0.25 - 0.9 s in all).</summary>
        public static void Type(string key, TextMeshProUGUI text, float delay, float cps)
        {
            Cancel(key);
            if (text == null) return;
            int total = 0;
            try { text.ForceMeshUpdate(); total = text.textInfo.characterCount; } catch { }
            if (!On || total <= 0) { try { text.maxVisibleCharacters = 99999; } catch { } return; }
            float dur = Mathf.Clamp(total / cps, 0.25f, 0.9f);
            Run(key, delay, dur, k => { text.maxVisibleCharacters = k >= 1f ? 99999 : (int)(total * k); });
        }

        /// <summary>A rule that draws itself from its left end (the rule must have its pivot on the left: Ui.LeftPivot).</summary>
        public static void Draw(string key, RectTransform rule, float delay, float dur)
        {
            if (rule == null) return;
            Cancel(key);
            Run(key, delay, dur, k => { rule.localScale = new Vector3(Mathf.Max(0.0001f, OutCubic(k)), 1f, 1f); });
        }

        /// <summary>A diamond (already rotated 45 degrees) spins half a turn into place.</summary>
        public static void Spin(string key, RectTransform diamond, float delay, float dur, float turns)
        {
            if (diamond == null) return;
            Cancel(key);
            Run(key, delay, dur, k => { diamond.localRotation = Quaternion.Euler(0, 0, 45f + 360f * turns * (1f - OutCubic(k))); });
        }
    }
}
