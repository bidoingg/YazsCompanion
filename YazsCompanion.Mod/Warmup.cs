// The first plan of a game session used to cost 170 - 190 ms (measured on the PC in six sessions, against 7 - 9 ms for
// every later one) - in the first second of the first run: the item rules setting up their patterns and reading every
// item's text for the first time, the item facts crossing over from the game, the code being compiled on first use.
// None of that needs a run. So it is done once on the main menu, a few seconds after the menu is up, in two steps on
// separate frames: the plan of an empty squad (its GRAB row scores every item in the game), then the plan of a
// stand-in survivor made from the game's class data (weapon line, abilities, recruits - the survivor side of the
// code). The results are thrown away; what stays is the parsed text, the item facts and the compiled code.
// Never during a run; everything here may fail without consequence (the first run then pays, as it used to).
using System;
using UnityEngine;
using CT = GamePlayer.CharacterType;

namespace YazsCompanion
{
    internal static class Warmup
    {
        static bool _done; static float _at = -1f; static int _tries, _stage;

        /// <summary>Once a frame from GameMaster.Update; does its work once per session, on the main menu only.</summary>
        public static void Tick()
        {
            if (_done) return;
            try
            {
                if (!Menu.OnMainMenu) { _at = -1f; return; }                  // the clock starts when the main menu is up
                float now = Time.realtimeSinceStartup;
                if (_at < 0f) { _at = now + 4f; return; }                      // let the menu settle first
                if (now < _at) return;
                if (Menu.IsOpen) { _at = now + 2f; return; }

                long perf = Perf.Begin();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using (G.Cache())
                {
                    var snap = G.Read();
                    if (snap.Squad.Count > 0) { _done = true; return; }        // a run after all: leave it alone
                    if (_stage == 0)
                    {
                        int rows = Plan.Build(snap, true).Lines.Count;
                        Perf.End("warmup", perf);
                        // no GRAB row = the game's item list was not there yet: try again a few times, then let the first run pay
                        if (rows == 0 && ++_tries < 4) { _at = now + 3f; return; }
                        Plugin.Logger.LogInfo("[warmup] item pass on the main menu: " + sw.Elapsed.TotalMilliseconds.ToString("0") + " ms" + (rows == 0 ? " (no item list yet, the first run will pay for it)" : ""));
                        _stage = 1; _at = now + 0.5f;
                        return;
                    }
                    string who = StandIn(snap);
                    if (who != null) { Plan.Build(snap, true); Plan.Build(snap, false); }
                    Perf.End("warmup", perf);
                    _done = true;
                    Plugin.Logger.LogInfo("[warmup] squad pass on the main menu: " + (who == null ? "no unlocked class found, skipped" : sw.Elapsed.TotalMilliseconds.ToString("0") + " ms (stand-in " + who + ")"));
                }
            }
            catch (Exception e) { _done = true; Plugin.Logger.LogInfo("[warmup] skipped: " + e.Message); }
        }

        // a survivor with nothing picked yet, from the first unlocked class: enough to walk the weapon line, the abilities
        // and the recruits once
        static string StandIn(Snapshot snap)
        {
            foreach (CT cls in Enum.GetValues(typeof(CT)))
            {
                if (cls == CT.None || cls == CT.NumCharacters) continue;
                var props = G.PropsOf(cls);
                if (props == null || !G.Unlocked(props)) continue;
                snap.Squad.Add(new Survivor { Type = cls, Name = G.ClassName(cls), Props = props, Leader = true, Snap = snap, TreeLevel = G.TreeLevel(props) });
                return G.ClassName(cls);
            }
            return null;
        }
    }
}
