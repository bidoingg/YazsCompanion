// Debug screenshots (config [Debug] Screenshots, off by default): PNGs of the game's own rendered frame, taken by
// Unity at the moments that matter for judging the UI - an offer with its badges, the pick, the sidebar right after
// a change (0.3 / 1.5 / 3.5 s into the gold highlight), its show / hide / creation, and one a minute during play.
// Files go to BepInEx\plugins\YazsCompanion\shots\HHmmss_fff_<label>.png; every capture is logged as "[shot] ...".
// Requests are queued with a delay and taken from the per-frame ticks (one capture per frame, capped per session).
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace YazsCompanion
{
    internal static class Shots
    {
        const int MaxPerSession = 90;
        const float MinGap = 0.25f;
        static readonly List<KeyValuePair<float, string>> _pending = new List<KeyValuePair<float, string>>();
        static readonly HashSet<string> _forced = new HashSet<string>();
        static int _count;
        static float _lastShot = -10f, _nextBase;
        static string _dir;
        /// <summary>Render scale of forced captures (the design preview emulating a bigger screen); 1 otherwise.</summary>
        public static int SuperSize = 1;

        public static bool Enabled { get { try { return Plugin.Screenshots != null && Plugin.Screenshots.Value; } catch { return false; } } }

        /// <summary>Queue a capture <paramref name="seconds"/> from now, labelled.</summary>
        public static void Later(float seconds, string label) { Later(seconds, label, false); }
        /// <summary>The same; <paramref name="force"/> captures even with the Screenshots setting off (the design preview).</summary>
        public static void Later(float seconds, string label, bool force)
        {
            if ((!Enabled && !force) || _count >= MaxPerSession) return;
            if (force) _forced.Add(label);
            _pending.Add(new KeyValuePair<float, string>(Time.realtimeSinceStartup + seconds, label));
        }

        /// <summary>One capture a minute while the caller (the sidebar) is on screen.</summary>
        public static void Baseline()
        {
            if (!Enabled) return;
            float now = Time.realtimeSinceStartup;
            if (now < _nextBase) return;
            _nextBase = now + 60f;
            Later(0f, "base");
        }

        /// <summary>Every frame from the gameplay and menu ticks: take the first due capture (Unity keeps one per frame).</summary>
        public static void Tick()
        {
            if (_pending.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            for (int i = 0; i < _pending.Count; i++)
            {
                var p = _pending[i];
                if (now < p.Key) continue;
                _pending.RemoveAt(i);
                float gap = p.Value.StartsWith("fx") ? 0.05f : MinGap;      // animation frames of the previews
                if (now - _lastShot < gap) { _pending.Add(new KeyValuePair<float, string>(_lastShot + gap, p.Value)); return; }
                try
                {
                    if (_dir == null) { _dir = Path.Combine(Plugin.PluginDir, "shots"); Directory.CreateDirectory(_dir); }
                    string file = Path.Combine(_dir, DateTime.Now.ToString("HHmmss_fff") + "_" + p.Value + ".png");
                    int size = _forced.Contains(p.Value) ? Math.Max(1, SuperSize) : 1;
                    ScreenCapture.CaptureScreenshot(file, size);
                    _lastShot = now; _count++;
                    Plugin.Logger.LogInfo("[shot] " + Path.GetFileName(file) + (_count >= MaxPerSession ? " (session cap reached)" : ""));
                }
                catch (Exception e) { Plugin.Logger.LogWarning("[shot] " + p.Value + ": " + e.Message); }
                return;
            }
        }
    }
}
