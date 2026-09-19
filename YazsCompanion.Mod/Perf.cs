// What the mod costs ([Debug] Perf, off by default): wall-clock time of its own sections - the per-frame ticks, the
// cheap change check, the full snapshot, a plan build, an offer - summed per section and logged once a minute as
// "[perf] 60 s, 8640 frames: tick.hud 8640x 9.1 ms (max 0.38) | ..." (calls, total, the worst single call), so a
// change can be judged from the companion.log of a real run. Switched off, a section costs one bool test.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace YazsCompanion
{
    internal static class Perf
    {
        sealed class Slot { public long Ticks, Max; public int Calls; }

        /// <summary>Set from the config at load and whenever the setting changes.</summary>
        public static bool On;
        const float Every = 60f;
        static readonly Dictionary<string, Slot> _slots = new Dictionary<string, Slot>();
        static readonly List<string> _order = new List<string>();
        static float _nextReport = -1f, _since;
        static int _frames;

        /// <summary>Start a section: 0 when the probe is off (End then does nothing).</summary>
        public static long Begin() { return On ? Stopwatch.GetTimestamp() : 0L; }

        public static void End(string section, long start)
        {
            if (start == 0L) return;
            long t = Stopwatch.GetTimestamp() - start;
            Slot s;
            if (!_slots.TryGetValue(section, out s)) { s = new Slot(); _slots[section] = s; _order.Add(section); }
            s.Ticks += t; s.Calls++; if (t > s.Max) s.Max = t;
        }

        /// <summary>Once a frame (GameMaster.Update): count the frame and, once a minute, log and clear the sums.</summary>
        public static void Frame()
        {
            if (!On) { if (_slots.Count > 0) { _slots.Clear(); _order.Clear(); _nextReport = -1f; } return; }
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (_nextReport < 0f) { _nextReport = now + Every; _since = now; _frames = 0; }
            _frames++;
            if (now < _nextReport) return;
            try
            {
                // sections nest (a tick contains the reads and builds it triggered), so the columns do not add up
                double ms = 1000.0 / Stopwatch.Frequency;
                var sb = new StringBuilder("[perf] ").Append((now - _since).ToString("0")).Append(" s, ").Append(_frames).Append(" frames:");
                bool first = true;
                foreach (var name in _order)
                {
                    var s = _slots[name]; if (s.Calls == 0) continue;
                    sb.Append(first ? " " : " | ").Append(name).Append(' ').Append(s.Calls).Append("x ").Append((s.Ticks * ms).ToString("0.0")).Append(" ms (max ").Append((s.Max * ms).ToString("0.00")).Append(')');
                    first = false;
                    s.Ticks = 0; s.Max = 0; s.Calls = 0;
                }
                if (first) sb.Append(" nothing ran");
                Plugin.Logger.LogInfo(sb.ToString());
            }
            catch { }
            _nextReport = now + Every; _since = now; _frames = 0;
        }
    }
}
