// Debug screenshots (config [Debug] Screenshots, off by default): PNGs of the game's own rendered frame, taken by
// Unity at the moments that matter for judging the UI - an offer with its badges, the pick, the sidebar right after
// a change (0.3 / 1.5 / 3.5 s into the gold highlight), its show / hide / creation, and one a minute during play.
// Files go to BepInEx\plugins\YazsCompanion\shots\HHmmss_fff_<label>.png; every capture is logged as "[shot] ...".
// Requests are queued with a delay and taken from the per-frame ticks (one capture per frame, capped per session).
// 10-07 review (C-m11): also the run's results screens - the defeat (or victory) flow's first step and its stats step - so the
// 21:9 results backdrop and WideMenus' 'results: step-1 box' can be judged from a frame ('results1', 'results2').
// 0.16.0 (C16-11b, game 1.0.2 machine code): UIDefeatState2.Setup() has no caller (cm_xref callerCount=0 - UIDefeat.OnEnable fills
// state 2 itself), so step 2 is taken from UIDefeatState2.OnEnable (a Unity message), held while state 1 is still up; else 1.5 s
// after state 1's continue (UIDefeat.OnState1Continue). Setup stays hooked for a later game build that calls it.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
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
            if (_s2Due >= 0f || (_contAt >= 0f && _s2Via == null)) ResultsTick(Time.realtimeSinceStartup);     // C16-11b: a step-2 capture waits
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

        // ---------------------------------------------------------------- the results flow (C16-11b)
        // One flow runs from a step 1 (UIDefeatState1.Setup) to the next. Step 2 is captured once a flow: by state 2's OnEnable (or its
        // Setup in a game build that calls it), or by the fallback 1.5 s after state 1's continue. An OnEnable after the fallback still
        // captures (the fallback may have caught the fade).
        static int _resultsFlow;
        static float _step1At = -1f, _contAt = -1f;
        static string _s2Via;                       // how step 2 was captured in this flow: "OnEnable", "Setup", "OnState1Continue"; null = not yet
        static int _s2EarlyFlow = -1;               // the flow whose 'enabled with the flow' line is written (once a flow)
        static float _s2Due = -1f, _s2Until;        // O14-20c: a state-2 capture held while state 1 is still up - next look, last moment
        static string _s2DueVia;
        static UIDefeat _defeat;                    // the flow's results screen (state 2's parent, or the one OnState1Continue ran on)

        static string Size() { return UnityEngine.Screen.width + "x" + UnityEngine.Screen.height; }

        /// <summary>A step of the results flow set itself up: two captures of it, 0.8 and 1.6 s on (the entrance played; the harness
        /// advances a step about 2 s after it shows). Step 1 = UIDefeatState1.Setup(bool), which UIDefeat.OnEnable calls; it starts a
        /// new flow even with captures off. Game 1.0.2: UIDefeatState2.Setup() has no caller - step 2 = UIDefeatState2.OnEnable
        /// (<see cref="ResultsState2"/>), else 1.5 s after state 1's continue (<see cref="ResultsContinue"/>).</summary>
        public static void Results(int step)
        {
            if (step == 1)
            {   // a new flow - before the Enabled test, so the flow moves on with captures off too
                _resultsFlow++; _step1At = Time.realtimeSinceStartup; _contAt = -1f; _s2Via = null;
                _s2Due = -1f; _s2DueVia = null; _defeat = null;
            }
            if (!Enabled) return;
            Plugin.Logger.LogInfo("[shot] results step " + step + " set up (" + Size() + ") - captures at 0.8 and 1.6 s");
            Later(0.8f, "results" + step); Later(1.6f, "results" + step + "b");
        }

        /// <summary>State 2 of the results flow came up (<paramref name="via"/> = the hook: "OnEnable", or "Setup" in a game build that
        /// calls it): one capture pair a flow; nothing while it comes with the flow (under state 1, less than 1 s after step 1); held
        /// while state 1 is still up (its exit fade), 3 s at most.</summary>
        public static void ResultsState2(string via, UIDefeatState2 state2)
        {
            if (!Enabled) return;
            float now = Time.realtimeSinceStartup;
            if (_s2Via == "OnEnable" || _s2Via == "Setup" || _s2Due >= 0f) return;       // (a) once a flow (a fallback capture lets it through)
            if (_defeat == null) _defeat = DefeatOf(state2);
            float since = _step1At >= 0f ? now - _step1At : -1f;
            if (_step1At >= 0f && since < 1.0f)
            {   // (b) enabled together with the flow (state 2's object under state 1): nothing of it on screen yet
                if (_s2EarlyFlow != _resultsFlow)
                {
                    _s2EarlyFlow = _resultsFlow;
                    Plugin.Logger.LogInfo("[shot] results: state 2 enabled with the flow (+" + since.ToString("0.0") + " s after step 1) - waiting for state 1's continue");
                }
                return;
            }
            if (State1Up())
            {   // O14-20c: state 2 enabled while state 1 still plays its exit (OnState1Continue -> FadeInOut.Fade) - held until it is gone
                _s2Due = now + 0.2f; _s2Until = now + 3f; _s2DueVia = via;
                Plugin.Logger.LogInfo("[shot] results: state 2 enabled via " + via + (since >= 0f ? " (+" + since.ToString("0.0") + " s after step 1)" : "") + " while state 1 is still up - held until it is gone (3 s at most)");
                return;
            }
            Step2(via, now, null);                                                       // (c)
        }

        /// <summary>State 1 of the results flow continued (UIDefeat.OnState1Continue: the player's Continue, the auto-continue or the
        /// harness): the fallback's clock starts at the first continue of the flow.</summary>
        public static void ResultsContinue(UIDefeat defeat)
        {
            float now = Time.realtimeSinceStartup;
            try { if (defeat != null) _defeat = defeat; } catch { }
            if (_contAt >= 0f && _s2Via == null) return;        // a second press while the first one's fallback waits
            _contAt = now;
            if (!Enabled) return;
            Plugin.Logger.LogInfo("[shot] results: state 1 continued " + (_step1At >= 0f ? "+" + (now - _step1At).ToString("0.0") + " s after step 1" : "(no step 1 in this flow)"));
        }

        /// <summary>From <see cref="Tick"/> while a step-2 capture waits: the held OnEnable (until state 1 is gone; 3 s at most, and no
        /// later than 1.5 s after state 1's continue - the harness holds state 2 about 2.5 s), and the fallback 1.5 s after state 1's
        /// continue when no state-2 hook captured (it does not wait for state 1: a state 2 enabled with the flow may share the screen
        /// with a state 1 that never goes inactive).</summary>
        static void ResultsTick(float now)
        {
            if (!Enabled) { _s2Due = -1f; _contAt = -1f; return; }          // captures off: nothing to wait for (the next step 1 starts afresh)
            if (_s2Due >= 0f)
            {
                if (now < _s2Due) return;
                string via = _s2DueVia;
                if (!FlowUp()) { _s2Due = -1f; Plugin.Logger.LogInfo("[shot] results: the results screen closed while state 2's " + via + " was held - no step-2 capture"); return; }
                if (!State1Up()) { _s2Due = -1f; Step2(via, now, ", state 1 gone"); return; }
                bool late = _contAt >= 0f && now - _contAt >= 1.5f;          // the fallback's moment: no longer
                if (now < _s2Until && !late) { _s2Due = now + 0.2f; return; }
                _s2Due = -1f;
                if (_contAt >= 0f) { Step2(via, now, ", state 1 still up " + (now - _contAt).ToString("0.0") + " s after its continue"); return; }
                Plugin.Logger.LogInfo("[shot] results: state 1 still up 3 s after state 2's " + via + " - waiting for state 1's continue");
                return;
            }
            if (_contAt < 0f || _s2Via != null || now - _contAt < 1.5f) return;
            if (!FlowUp()) { _contAt = -1f; Plugin.Logger.LogInfo("[shot] results: the results screen closed before step 2 showed - no step-2 capture"); return; }
            bool s1 = State1Up();
            _s2Via = "OnState1Continue";
            Plugin.Logger.LogInfo("[shot] results step 2 set up (" + Size() + ", via OnState1Continue: no state-2 OnEnable " + (now - _contAt).ToString("0.0") + " s after it" + (s1 ? "; state 1 still up" : "") + ") - captures now and at 0.8 s");
            Later(0f, "results2"); Later(0.8f, "results2b");
        }

        static void Step2(string via, float now, string note)
        {
            _s2Via = via;
            string head = _step1At < 0f ? "[shot] results step 2 without step 1, set up (" : "[shot] results step 2 set up (";
            string after = _step1At >= 0f ? ", +" + (now - _step1At).ToString("0.0") + " s after step 1" : "";
            Plugin.Logger.LogInfo(head + Size() + ", via " + via + after + (note ?? "") + ") - captures at 0.8 and 1.6 s");
            Later(0.8f, "results2"); Later(1.6f, "results2b");
        }

        /// <summary>State 1 of the flow's results screen is active (false when it is gone or not known).</summary>
        static bool State1Up()
        {
            try { var d = _defeat; if (d == null) return false; var s1 = d.state1; return s1 != null && s1.gameObject.activeInHierarchy; }
            catch { return false; }
        }

        /// <summary>The flow's results screen is still up (true when it is not known).</summary>
        static bool FlowUp()
        {
            try { var d = _defeat; return d == null || d.gameObject.activeInHierarchy; }
            catch { return true; }
        }

        static UIDefeat DefeatOf(UIDefeatState2 state2)
        {
            try
            {
                if (state2 == null) return null;
                var c = state2.GetComponentInParent(Il2CppType.Of<UIDefeat>());
                return c == null ? null : c.TryCast<UIDefeat>();
            }
            catch { return null; }
        }
    }

    // ---- the results flow's steps (read only: post-fixes that queue a capture with [Debug] Screenshots on); a step a game build lacks is skipped
    [HarmonyPatch]
    static class P_ResultsShot1
    {
        static MethodBase Target() { try { return AccessTools.DeclaredMethod(typeof(UIDefeatState1), "Setup", new[] { typeof(bool) }); } catch { return null; } }
        static bool Prepare() { return Target() != null; }
        static MethodBase TargetMethod() { return Target(); }
        static void Postfix() { try { Shots.Results(1); } catch { } }
    }

    // C16-11b: state 2's OnEnable (a Unity message; its own rva 0x87b2c0) - 1.0.2 never calls UIDefeatState2.Setup() (rva 0x87b8a0,
    // callerCount 0), which stays hooked for a later game build (P_ResultsShot2Setup). Never UIDefeatState2's Awake: its rva 0x384170
    // is one body shared by 4,075 methods, so a patch there would patch all of them. One class per target, each naming its own
    // step: no __originalMethod (never used by either plugin under IL2CPP), and a target a game build lacks skips only its class.
    [HarmonyPatch]
    static class P_ResultsShot2
    {
        static MethodBase Target() { try { return AccessTools.DeclaredMethod(typeof(UIDefeatState2), "OnEnable", Type.EmptyTypes); } catch { return null; } }
        static bool Prepare() { return Target() != null; }
        static MethodBase TargetMethod() { return Target(); }
        static void Postfix(UIDefeatState2 __instance) { try { Shots.ResultsState2("OnEnable", __instance); } catch { } }
    }

    // C16-11b: state 2's Setup() - not called by 1.0.2 (callerCount 0); kept for a later game build that calls it
    [HarmonyPatch]
    static class P_ResultsShot2Setup
    {
        static MethodBase Target() { try { return AccessTools.DeclaredMethod(typeof(UIDefeatState2), "Setup", Type.EmptyTypes); } catch { return null; } }
        static bool Prepare() { return Target() != null; }
        static MethodBase TargetMethod() { return Target(); }
        static void Postfix(UIDefeatState2 __instance) { try { Shots.ResultsState2("Setup", __instance); } catch { } }
    }

    // C16-11b: state 1's continue (its own rva 0x874570; called by OnState1AutoContinue, the player's Continue and the harness) - the
    // fallback's clock when no state-2 hook fires
    [HarmonyPatch]
    static class P_ResultsContinue
    {
        static MethodBase Target() { try { return AccessTools.DeclaredMethod(typeof(UIDefeat), "OnState1Continue", Type.EmptyTypes); } catch { return null; } }
        static bool Prepare() { return Target() != null; }
        static MethodBase TargetMethod() { return Target(); }
        static void Postfix(UIDefeat __instance) { try { Shots.ResultsContinue(__instance); } catch { } }
    }
}
