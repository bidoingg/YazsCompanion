// 0.16.0 (C16-11a): the pause walk's recruit - with [Debug] PreviewPauseRecruit set (and PreviewPause on), one hero joins the
// walk's Quick Run 3 s into play as a rescue adds one (the game's Unlock Character <Class> Powerup, OnApply(false): no rescue
// bonuses), so the level-ups offer four cards and the hover tour (Advisor.DebugTourDue) reaches cards 3 and 4.
//
// The call sites are in Menu.cs (PausePreviewTick): DebugRecruitStart() once as the walk starts its Quick Run (stage 0);
// DebugRecruit(t) once at 3 s of play (stage 1, gated by PpRecruitKeySet() and _ppRecruitDone, which the call site sets);
// DebugRecruitCheck() once when _ppRecruitCheckAt (set by DebugRecruit: now + 1 s) has passed (stages 1 and 61; the call site
// resets _ppRecruitCheckAt to -1 first).
//
// ONE recruit, never two: the walk acts only while the squad has its leader alone (gamePlayers.Count == 1), once a session
// (_ppRecruitDone is never cleared). The first recruit is playerIndex 1; the game's SpawnNewGamePlayer unlocks an achievement for
// playerIndex 2, a SECOND recruit (offline disassembly, the overhaul's DebugTools.GrantRecruit notes) - the walk never makes one.
// OnApply(false) is the game's own spawn and the leader's list entry without the rescue's stat bonuses, its health refill and its
// rescue achievement check (rva 0x7eecf0); OnApply(true) is never called here.
//
// THE FIRST LEVEL-UP (fix round of series r1, 10-08; FAILURE 2: R_A1 / R_A2 got no offer): the walk's squad stands still (no
// movement input), and an XP gem is picked up only when it lies inside the pickup ring - the MovementPivot's
// CollectiblesMagnetActivator trigger (radius from TeamMagnetRange) marks it, CollectibleItem.SetMagnet / MagnetMovement pull it
// to the pivot, CollectiblesCollector hands it to CollectibleXP.OnCollect (offline disassembly, research\roadmap_1007\scratch\
// fix_a\firstoffer). With one hero the zombies reach the ring before they fall (10-07: 52 of 100 XP at 0:42, the first offer at
// 0:45); with the recruit's second gun they fall short of it, and the gems stay in a band just outside the ring: 0 of 100 XP and
// 0 $ at 0:42 with 37 / 38 kills in both r1 walks (the pause4_resumed shots). Not a threshold of the squad's size: level 1 needs
// 100 XP (ExperienceProgress.Reset) whatever the squad, CollectXPRepeated scales by TeamXPMultiplier and the enemy XP scaling
// only, and OnApply(false) never touches the ExperienceProgress. So the walk grants its first level-up with the game's own
// ExperienceProgress.Debug_LevelUp() (the developer tool the game's Hijacked Signal item uses too: the XP to the next level
// through CollectXPRepeated, unscaled - then CheckForLevelUp, LevelUp, the level-up screen) when no offer has come by
// PauseRecruit.LevelUpAt: 20 s with the recruit, 36 s without (in stage 1, before the 40 s pause; under PlayBound, 41 s = the 50 s
// save rule less the ~3 s of play to the done line and the series' ~6 s to its kill - fix_a review); once a session,
// never while a screen is up; ExperienceProgress.LevelUp() direct once when the level did not rise 1.5 s later (the screen
// without the level counter). The grant line says how far the gems lay from the squad (the live proof of the cause above).
// Call sites: DebugFirstLevelUp(t, now) in stages 1 and 61 of PausePreviewTick while the run plays.
//
// PauseRecruit (the key's parse, the grant's timing) is pure - no game type - so the offline bench can call it; the rest of the
// file is the game's side. Linked into the bench (tools\ItemBench) the file is compiled with YAZS_BENCH defined, which leaves the
// game's side out; while it is not linked, the bench reads the helper from this source (ReviewCases, V5).
using System;
#if !YAZS_BENCH
using Il2CppInterop.Runtime;
using UnityEngine;
using CT = GamePlayer.CharacterType;
#endif

namespace YazsCompanion
{
    /// <summary>0.16.0 (C16-11a): [Debug] PreviewPauseRecruit read without the game. The nine classes a recruit may be, in Auto's order,
    /// by the game's CharacterType names (the Ghost's is Ninja).</summary>
    internal static class PauseRecruit
    {
        internal const string Off = "", Auto = "Auto";
        internal static readonly string[] Classes = { "Medic", "Tank", "Pyro", "Engineer", "Huntress", "SWAT", "Mechanic", "Ranger", "Ninja" };

        /// <summary>Trimmed, any case: '' = <see cref="Off"/>; 'Auto' = <see cref="Auto"/>; 'Ghost' or 'Ninja' = "Ninja"; one of the
        /// nine = its CharacterType name; anything else (a number, None, a typo) = null - no class.</summary>
        internal static string Parse(string raw)
        {
            string v = raw == null ? "" : raw.Trim();
            if (v.Length == 0) return Off;
            if (string.Equals(v, Auto, StringComparison.OrdinalIgnoreCase)) return Auto;
            if (string.Equals(v, "Ghost", StringComparison.OrdinalIgnoreCase)) return "Ninja";
            foreach (var c in Classes) if (string.Equals(v, c, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        /// <summary>Auto's class: the first of the nine that does not lead the run (the leader by its CharacterType name).</summary>
        internal static string AutoPick(string leader)
        {
            foreach (var c in Classes) if (!string.Equals(c, leader, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        /// <summary>The name a log line shows (G.ClassName's rule): the Ghost, never 'Ninja'.</summary>
        internal static string Shown(string cls) { return string.Equals(cls, "Ninja", StringComparison.OrdinalIgnoreCase) ? "Ghost" : cls; }

        /// <summary>Fix round of series r1 (the first level-up): the play time from which the walk grants its first level-up when no
        /// offer has come - 20 s with the recruit (its squad gathered no XP at all in both r1 walks), 36 s without (fix_a review: in
        /// stage 1, before its 40 s pause, with room for the 1.5 s LevelUp() fallback; at 46 s the walk's end came past 50 s of play).
        /// <see cref="PlayBound"/> is the walk's play-clock bound for waiting on an offer: <see cref="SaveAfter"/> (a killed run past
        /// ~50 s of play leaves a save) less <see cref="DoneLinePlay"/> (stages 61 / 7 / 8 run the game for ~3 s until the done line)
        /// less <see cref="KillLatency"/> (the series' walk sees the done line within one 3 s poll and sleeps 3 s before the kill) = 41 s.</summary>
        internal const float LevelUpAtRecruit = 20f, LevelUpAtAlone = 36f, SaveAfter = 50f, DoneLinePlay = 3f, KillLatency = 6f,
            PlayBound = SaveAfter - DoneLinePlay - KillLatency;

        /// <summary>The grant time: <see cref="LevelUpAtRecruit"/> once the recruit joined, else <see cref="LevelUpAtAlone"/>.</summary>
        internal static float LevelUpAt(bool recruitJoined) { return recruitJoined ? LevelUpAtRecruit : LevelUpAtAlone; }

        /// <summary>The walk grants its first level-up now: not asked yet this session, no card taken yet, no screen up (the game
        /// paused: an offer of its own, or the pause menu), and the play clock at the grant time or later but under the 41 s bound.</summary>
        internal static bool LevelUpDue(bool recruitJoined, int picks, float t, bool asked, bool paused)
        {
            return !asked && picks == 0 && !paused && t >= LevelUpAt(recruitJoined) && t < PlayBound;
        }
    }

#if !YAZS_BENCH
    internal static partial class Menu
    {
        static bool _rkRead;            // the key read (once, at the walk's start)
        static string _rkWant;          // PauseRecruit.Parse of it: "" off, "Auto", a CharacterType name; null = no class (off)
        static CT _rkCls = CT.None;     // the class applied, its play time, the powerup's asset name, and whether OnApply(false) threw
        static float _rkT;
        static string _rkCard;
        static bool _rkThrew;
        static bool _rkJoined;          // DebugRecruitCheck saw the recruit in the squad (the first level-up's time: PauseRecruit.LevelUpAt)
        // the first level-up (series r1 fix round): asked once a session; its check (realtime, -1 none); the level before it; whether
        // ExperienceProgress.LevelUp() direct was used after Debug_LevelUp left the level where it was
        static bool _luAsked; static float _luCheckAt = -1f; static int _luLevel = -1; static bool _luDirect;

        /// <summary>The walk's start (stage 0, before its Quick Run): parse [Debug] PreviewPauseRecruit once ('' off, Auto, Ghost / Ninja,
        /// one of the nine classes; anything else one Warning and off).</summary>
        static void DebugRecruitStart()
        {
            if (_rkRead) return;
            _rkRead = true;
            string raw = ""; try { raw = Plugin.PreviewPauseRecruit != null ? (Plugin.PreviewPauseRecruit.Value ?? "") : ""; } catch { }
            _rkWant = PauseRecruit.Parse(raw);
            CT cls;
            if (_rkWant != null && _rkWant.Length > 0 && _rkWant != PauseRecruit.Auto && !TryClass(_rkWant, out cls)) _rkWant = null;   // a name this game build lacks
            if (_rkWant == null) Plugin.Logger.LogWarning("[menu] pause walk: PreviewPauseRecruit \"" + raw.Trim() + "\" is no class - off");
            else if (_rkWant.Length > 0)
                Plugin.Logger.LogInfo("[menu] pause walk: PreviewPauseRecruit " + (_rkWant == PauseRecruit.Auto ? "Auto" : PauseRecruit.Shown(_rkWant)) + " - one hero joins 3 s into the run");
        }

        /// <summary>3 s into the walk's run: apply the Unlock Character powerup of the class asked for (OnApply(false)) while the squad
        /// has one hero, then set _ppRecruitCheckAt = now + 1 s for <see cref="DebugRecruitCheck"/>.</summary>
        static void DebugRecruit(float t)
        {
            bool walk = false; try { walk = Plugin.PreviewPause != null && Plugin.PreviewPause.Value; } catch { }
            if (!walk) return;                                       // never without the pause walk
            if (!_rkRead) DebugRecruitStart();
            if (string.IsNullOrEmpty(_rkWant)) return;               // off, or a key that names no class (said at the walk's start)
            var gm = GameplayMaster.s_instance;
            if (gm == null) { Plugin.Logger.LogWarning("[menu] pause walk: recruit not applied - no run to join"); return; }
            int n = -1; try { if (gm.gamePlayers != null) n = gm.gamePlayers.Count; } catch { }
            if (n != 1)
            {   // the one-recruit rule: a hero is added only to a leader alone (a second recruit would unlock an achievement)
                Plugin.Logger.LogInfo("[menu] pause walk: recruit not applied - " + (n < 0 ? "the squad could not be read" : "the squad has " + n + " heroes already"));
                return;
            }
            CT leader = CT.None; try { leader = gm.currentMainCharacterType; } catch { }
            string want = _rkWant == PauseRecruit.Auto ? PauseRecruit.AutoPick(leader.ToString()) : _rkWant;
            CT cls;
            if (!TryClass(want, out cls)) { Plugin.Logger.LogWarning("[menu] pause walk: recruit not applied - \"" + want + "\" is no class of this game build"); return; }
            string who = G.ClassName(cls), asset = "Unlock Character " + who + " Powerup";
            if (cls == leader) { Plugin.Logger.LogWarning("[menu] pause walk: recruit not applied - " + who + " leads the run already (name another class, or Auto)"); return; }
            PowerupBase card = null;
            try { card = RerollHint.UnlockCard((int)cls); } catch { }
            if (card == null) card = FindUnlock(asset);              // UnlockCard keeps an empty table when the lists were not there at its first call
            UnlockCharacterPowerup unlock = null; try { if (card != null) unlock = card.TryCast<UnlockCharacterPowerup>(); } catch { }
            if (unlock == null) { Plugin.Logger.LogWarning("[menu] pause walk: recruit not applied - no " + asset + " in the game's powerup lists"); return; }
            _rkCls = cls; _rkT = t; _rkThrew = false;
            _rkCard = asset; try { string nm = card.name; if (!string.IsNullOrEmpty(nm)) _rkCard = nm.Trim(); } catch { }
            try { unlock.OnApply(false); }                           // the game's spawn + the leader's list entry, no rescue bonuses
            catch (Exception e)
            {
                _rkThrew = true;
                Plugin.Logger.LogWarning("[menu] pause walk: recruit not applied - OnApply(false) threw " + e.GetType().Name + ": " + e.Message);
            }
            _ppRecruitCheckAt = Time.realtimeSinceStartup + 1f;      // a second later: did the hero join?
        }

        /// <summary>A second after the recruit: '[menu] pause walk: recruit &lt;Class&gt; joined at ...' or one Warning.</summary>
        static void DebugRecruitCheck()
        {
            int n = -1; bool there = false;
            try
            {
                var gm = GameplayMaster.s_instance;
                if (gm != null && gm.gamePlayers != null)
                {
                    n = gm.gamePlayers.Count;
                    foreach (var gp in G.Each(gm.gamePlayers)) { try { if (gp != null && !gp.isMainPlayer && gp.characterType == _rkCls) there = true; } catch { } }
                }
            }
            catch { }
            string who = G.ClassName(_rkCls), at = _rkT.ToString("0.0");
            if (n == 2 && there)
            {
                _rkJoined = true;                                    // the first level-up comes at 20 s, not 36 (PauseRecruit.LevelUpAt)
                Plugin.Logger.LogInfo("[menu] pause walk: recruit " + who + " joined at " + at + " s (" + _rkCard + ", OnApply(false)" + (_rkThrew ? " - it threw, yet the hero came" : "") + ") - squad 2: the level-ups offer four cards");
            }
            else if (_rkThrew) { }                                   // the throw was this recruit's one Warning
            else if (n == 1) Plugin.Logger.LogWarning("[menu] pause walk: recruit not applied - squad still 1 a second later");
            else Plugin.Logger.LogWarning("[menu] pause walk: recruit " + who + " at " + at + " s - " + (n < 0 ? "the squad could not be read" : "squad " + n + (there ? "" : ", no " + who + " in it")) + " a second later");
        }

        /// <summary>Stages 1 and 61 of the walk, while its run plays: the first level-up, granted once a session with the game's
        /// ExperienceProgress.Debug_LevelUp() when PauseRecruit.LevelUpDue says so (no offer yet at 20 s with the recruit, at 36 s
        /// without), and its check 1.5 s later (<see cref="DebugLevelUpCheck"/>).</summary>
        static void DebugFirstLevelUp(float t, float now)
        {
            if (_luCheckAt > 0f && now >= _luCheckAt) { _luCheckAt = -1f; DebugLevelUpCheck(t, now); return; }
            bool paused = false; try { paused = GameplayMaster.IsPaused; } catch { }
            if (!PauseRecruit.LevelUpDue(_rkJoined, Advisor.DebugPicks, t, _luAsked, paused)) return;
            _luAsked = true;                                         // once a session, whatever comes of it
            var gm = GameplayMaster.s_instance;
            ExperienceProgress xp = null;
            try { var rs = gm == null ? null : gm.runStats; xp = rs == null ? null : rs.ExperienceProgress; } catch { }
            if (xp == null) { Plugin.Logger.LogWarning("[menu] pause walk: first level-up not granted - no ExperienceProgress (GameplayMaster.runStats) at " + t.ToString("0.0") + " s"); return; }
            int cur = -1, next = -1; _luLevel = -1;
            try { cur = xp.GetCurrentLevelXp(); next = xp.GetNextLevelXp(); _luLevel = xp.GetCurrentLevel(); } catch { }
            string ground = GemsOnGround(gm);                        // read before the level-up's own explosion and screen
            try { xp.Debug_LevelUp(); }
            catch (Exception e) { Plugin.Logger.LogWarning("[menu] pause walk: first level-up not granted - ExperienceProgress.Debug_LevelUp() threw " + e.GetType().Name + ": " + e.Message); return; }
            Plugin.Logger.LogInfo("[menu] pause walk: no offer by " + t.ToString("0.0") + " s of play (XP " + cur + " of " + next + ", level " + _luLevel
                + (_rkJoined ? ", squad 2 standing still" : "") + "; " + ground + ") - the first level-up granted with the game's ExperienceProgress.Debug_LevelUp() (one level's XP, unscaled; the walk's " + PauseRecruit.PlayBound.ToString("0") + " s bound holds)");
            _luCheckAt = now + 1.5f;
        }

        /// <summary>1.5 s after the grant: the level rose (the game's level-up screen is queued or up) - one Info line; else once
        /// ExperienceProgress.LevelUp() direct (the screen without the level counter: CollectXPRepeated skips its XP while the leader's
        /// health reads under 1) and a second check; still nothing - one Warning (the walk's 'no offer came' line follows).</summary>
        static void DebugLevelUpCheck(float t, float now)
        {
            ExperienceProgress xp = null; int lvl = -1; bool paused = false;
            try { var gm = GameplayMaster.s_instance; var rs = gm == null ? null : gm.runStats; xp = rs == null ? null : rs.ExperienceProgress; if (xp != null) lvl = xp.GetCurrentLevel(); } catch { }
            try { paused = GameplayMaster.IsPaused; } catch { }
            if (_luDirect)
            {
                if (paused || Advisor.DebugPicks > 0) Plugin.Logger.LogInfo("[menu] pause walk: level-up screen up after ExperienceProgress.LevelUp() (level " + lvl + " - the counter stays)");
                else Plugin.Logger.LogWarning("[menu] pause walk: no level-up screen 1.5 s after ExperienceProgress.LevelUp() either (level " + lvl + ", play clock " + t.ToString("0.0") + " s)");
                return;
            }
            if (lvl > _luLevel && _luLevel >= 0)
            {
                Plugin.Logger.LogInfo("[menu] pause walk: level-up granted - level " + _luLevel + " -> " + lvl + ", the game's level-up screen " + (paused ? "up" : "queued"));
                return;
            }
            if (xp == null || Advisor.DebugPicks > 0) return;        // the run is gone, or an offer came and was taken meanwhile
            _luDirect = true;
            try { xp.LevelUp(); }
            catch (Exception e) { Plugin.Logger.LogWarning("[menu] pause walk: ExperienceProgress.LevelUp() threw " + e.GetType().Name + ": " + e.Message); return; }
            Plugin.Logger.LogInfo("[menu] pause walk: level still " + lvl + " 1.5 s after Debug_LevelUp (its XP skipped) - ExperienceProgress.LevelUp() direct: the level-up screen without the level counter");
            _luCheckAt = now + 1.5f;
        }

        /// <summary>For the grant line - the live proof of the cause (see the head of this file): the XP gems on the ground (Level.XPGems,
        /// active), the nearest one's distance from the squad's centre (the MovementPivot, on the ground plane), the pickup ring's
        /// radius (its CollectiblesMagnetActivator's sphere, world size) and how many gems lie inside it.</summary>
        static string GemsOnGround(GameplayMaster gm)
        {
            try
            {
                var pv = gm == null ? null : gm.movementPivot; var lv = gm == null ? null : gm.level;
                if (pv == null || lv == null || lv.XPGems == null) return "the gems not read";
                Vector3 c = pv.transform.position;
                float ring = -1f;
                try
                {
                    var act = pv.myCollectiblesMagnetActivator; var col = act == null ? null : act._magnetCollider;
                    if (col != null) { var s = col.transform.lossyScale; ring = col.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z)); }
                }
                catch { }
                int n = 0, inside = 0; float near = float.MaxValue;
                var seen = new System.Collections.Generic.HashSet<IntPtr>();
                var all = new System.Collections.Generic.List<CollectibleXP>();
                foreach (var list in new[] { lv.XPGems, lv.RegularXPGems, lv.BigXPGems })    // the level's gem lists, each gem once
                    if (list != null) foreach (var g in G.Each(list)) { try { if (g != null && seen.Add(g.Pointer)) all.Add(g); } catch { } }
                foreach (var g in all)
                {
                    try
                    {
                        if (!g.gameObject.activeInHierarchy) continue;
                        Vector3 p = g.transform.position; float dx = p.x - c.x, dz = p.z - c.z, d = Mathf.Sqrt(dx * dx + dz * dz);
                        n++; if (d < near) near = d; if (ring > 0f && d <= ring) inside++;
                    }
                    catch { }
                }
                return n + " XP gem(s) on the ground" + (n > 0 ? ", the nearest " + near.ToString("0.0") + " m from the squad's centre" : "")
                    + (ring > 0f ? ", the pickup ring " + ring.ToString("0.0") + " m" + (n > 0 ? " (" + inside + " inside it)" : "") : ", the pickup ring not read");
            }
            catch (Exception e) { return "the gems not read (" + e.GetType().Name + ")"; }
        }

        /// <summary>A CharacterType by its enum name, one of the nine (never None / NumCharacters).</summary>
        static bool TryClass(string name, out CT cls)
        {
            cls = CT.None;
            if (string.IsNullOrEmpty(name) || Array.IndexOf(PauseRecruit.Classes, name) < 0) return false;
            CT c;
            if (!Enum.TryParse(name, false, out c) || c == CT.None || c == CT.NumCharacters || !Enum.IsDefined(typeof(CT), c)) return false;
            cls = c; return true;
        }

        /// <summary>The overhaul's proven lookup (DebugTools.GrantRecruit): every loaded powerup asset by its name - one scan, about 41 ms
        /// on the PC, debug only.</summary>
        static PowerupBase FindUnlock(string asset)
        {
            try
            {
                var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<PowerupBase>());
                for (int i = 0; i < all.Length; i++)
                {
                    var p = all[i] == null ? null : all[i].TryCast<PowerupBase>();
                    if (p == null) continue;
                    string n = null; try { n = p.name; } catch { }
                    if (n != null && string.Equals(n.Trim(), asset, StringComparison.OrdinalIgnoreCase)) return p;
                }
            }
            catch { }
            return null;
        }
    }
#endif
}
