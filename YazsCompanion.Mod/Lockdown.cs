// 0.17.0 (C16-06a): LOCKDOWN read and logged - each screen's lock, the Training Yard's Lockdown unlock and every lock the player makes. No
// hint yet (the LOCKDOWN hint is C16-06b); nothing here changes a card. What the game does, read in its machine code of 1.0.2 (research\
// roadmap_1007\scratch\c16_lockdown, the RVAs there):
//  - Lockdown is a FREE, UNLIMITED action on the level-up, chest and Military Training screens once the Team tree's node
//    SkillTree_Team_UnlockMechanic_Lockdowns is bought (rank 3, cost 0). Each screen's SetActionButtonsInteractivity shows its Lockdown button
//    only while GamePermanentData.Get.skillTreeUnlockLockdown's runtime instance has levelCurrent > 0 and IsUnlocked() (the field, not
//    GetCurrentLevel(), which can return the last level); the button is interactable unless a banish is armed. No charges are counted.
//  - UIGameplayUpgradeSelection.ClickLockdown (no screen overrides it) does nothing while the screen holds input (_isHiding, onEnableLockTime);
//    else it toggles _isLockdownActive and shows the lock plate on every lockdownable card. While armed, a click goes to the screen's own
//    ProcessLockdown only when the BUTTON's virtual get_IsLockdownable says so (a skill's PowerupBase.isLockdownable, an item's
//    ItemBase.isLockdownable, a training's attached powerup's; the rescue and Research Pod buttons answer false - a constant).
//  - ProcessLockdown of the level-up (_lockedPowerup), the chest (_lockedItem) and the training (_lockedPowerup, its rarity in
//    _lockedDownRarity) sets the lock, replaces it, or clears it when the locked card is clicked again, then jumps into the base.
//  - The next screen of the same kind - and a reroll of the same screen - draws the locked card FIRST while it is still in the pool; a
//    locked training keeps its rarity (a forced Rare / Legendary overrides it). Taking the locked card spends the lock; a skip keeps it; the
//    locks live on the screen objects for the run (ResetGameplay clears them: Try Again, the pause menu's confirm, the main menu, the hub).
// The game's own guide (Guide_13b_Desc): the locked option keeps appearing until you choose it, lock another or unlock it.
// What is logged: one '[lock] <screen> <clock>: <name> locked - on the cards (#1, 3.80)' / '- not on the cards (no longer in the pool)' line
// per offer while a lock is held ('(Rare kept)' on a training), the presses '[pick] LevelUp 05:12: the Lockdown button pressed (armed)' /
// '(put away)' / '- ignored (the screen holds input)', the locks '[pick] LevelUp 05:12: lockdown on Fire Walk (#2, 3.40)' / '... (replaces
// Golden Key)' / 'lockdown off Fire Walk', and Advisor's pick line ends '- the lock is spent' when the locked card is taken. Once a run, in a
// profile without the unlock, '[lock] the Lockdown feature is not unlocked in this profile ...'. Every game member is read through an
// accessor of its own (NoInlining, no catch inside: a member a game patch takes away fails that read alone, and the caller says so).
// [Debug] PauseWalkLock (with PreviewPause): on the walk's first level-up the Lockdown button pressed and the #2 card locked the game's way,
// then the screen rerolled once - the new cards must hold the lock (the replaced offer's '[lock] ... on the cards' line).
// Game side only: the bench reads this file's text (HintCases LK1), never compiles it.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine.UI;

namespace YazsCompanion
{
    internal static class Lockdown
    {
        /// <summary>What the offer on screen holds of LOCKDOWN (read by the LOCKDOWN hint, C16-06b).</summary>
        internal sealed class LockState
        {
            public bool Available;                  // a LevelUp / Chest / Military screen, the unlock not known to be missing, the screen has its Lockdown button
            public bool? Unlocked;                  // the unlock as read for the offer (null: not read - treated as unlocked)
            public string LockedName;               // the screen's lock (the game's name), null: none
            public Card OnCards;                    // the card holding the lock, null: none (or no lock)
            public string Rarity;                   // a training's kept rarity (_lockedDownRarity), null: not a training or no lock
            public string Screen = "", Clock = "";  // the offer's screen (LevelUp / Chest / Military) and clock
        }
        internal static LockState State = new LockState();

        static bool? _unlocked; static bool _unlockRead, _unlockWarned, _offSaid;
        static IntPtr _selPtr = IntPtr.Zero;        // the screen the cards below were marked on
        static List<Card> _cards;                   // the offer's cards (Advisor's own list: a re-mark is seen by Advisor and RerollHint alike)
        static Screen _screen;
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static string F2(double v) { return v.ToString("0.00", IC); }
        static IntPtr Ptr(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase o) { try { return o == null ? IntPtr.Zero : o.Pointer; } catch { return IntPtr.Zero; } }

        // ---- the game's members, one accessor each (no catch of their own)
        [MethodImpl(MethodImplOptions.NoInlining)] static SkillTreeUpgradeBase Node() { return GamePermanentData.Get.skillTreeUnlockLockdown; }
        [MethodImpl(MethodImplOptions.NoInlining)] static SkillTreeUpgradeBase Rt(SkillTreeUpgradeBase n) { return n.GetRuntimeInstance(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static int Lvl(SkillTreeUpgradeBase r) { return r.levelCurrent; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool Unl(SkillTreeUpgradeBase r) { return r.IsUnlocked(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool Lockable(UIPowerupButtonBase b) { return b.IsLockdownable; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Button LockButton(UIGameplayUpgradeSelection sel) { return sel.lockdownUpgradeButton; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool Armed(UIGameplayUpgradeSelection sel) { return sel._isLockdownActive; }
        [MethodImpl(MethodImplOptions.NoInlining)] static PowerupBase LevelUpLock(UIGameplayLevelUp s) { return s._lockedPowerup; }
        [MethodImpl(MethodImplOptions.NoInlining)] static ItemBase ChestLock(UIGameplayChestOpened s) { return s._lockedItem; }
        [MethodImpl(MethodImplOptions.NoInlining)] static PowerupBase TrainingLock(UIGameplayMilitaryTraining s) { return s._lockedPowerup; }
        [MethodImpl(MethodImplOptions.NoInlining)] static string TrainingRarity(UIGameplayMilitaryTraining s) { return s._lockedDownRarity.ToString(); }

        // ================================================================ the unlock
        /// <summary>The Training Yard's Lockdown unlock, as each screen's button test reads it: the node's runtime instance at levelCurrent &gt; 0 and
        /// IsUnlocked(). Null: the node or its runtime instance is missing or a read failed (the game itself throws there; treated as unlocked,
        /// logged once a run). Cached until <see cref="Forget"/> (the Training Yard does not change during a run). Never throws.</summary>
        public static bool? Unlocked()
        {
            if (_unlockRead) return _unlocked;
            _unlockRead = true;
            try
            {
                var n = Node();
                var rt = n == null ? null : Rt(n);
                if (rt == null) { _unlocked = null; Warn(n == null ? "no node" : "no runtime instance"); return null; }
                _unlocked = Lvl(rt) > 0 && Unl(rt);
            }
            catch (Exception e) { _unlocked = null; Warn(e.GetType().Name); }
            return _unlocked;
        }
        static void Warn(string why)
        {
            if (_unlockWarned) return;
            _unlockWarned = true;
            Plugin.Logger.LogInfo("[lock] the Lockdown unlock not read (" + why + ") - treated as unlocked");
        }

        // ================================================================ an offer
        /// <summary>From Advisor.Offer right after the ranking: each card's Lockable (its button's own IsLockdownable) and Locked (the screen's lock),
        /// and <see cref="State"/>. Marks only; <see cref="LogOffer"/> writes the line. Never throws.</summary>
        public static void Mark(UIGameplayUpgradeSelection sel, Screen screen, List<Card> cards, Snapshot snap)
        {
            try
            {
                _selPtr = Ptr(sel); _cards = cards; _screen = screen;
                State = new LockState { Screen = screen.ToString(), Clock = snap != null ? snap.Clock : "?", Unlocked = snap != null ? snap.LockdownOn : null };
                if (cards == null) return;
                bool lockScreen = screen == Screen.LevelUp || screen == Screen.Chest || screen == Screen.Military;
                foreach (var c in cards)
                {
                    c.Lockable = false; c.Locked = false;
                    if (!lockScreen || c.Button == null) continue;      // the rescue and Research Pod cards: never lockdownable (the game's own getter)
                    try { c.Lockable = Lockable(c.Button); } catch { c.Lockable = false; }
                }
                if (!lockScreen) return;
                bool button = false; try { button = LockButton(sel) != null; } catch { }
                State.Available = State.Unlocked != false && button;
                Read(sel);
            }
            catch (Exception e) { Plugin.Logger.LogInfo("[lock] " + screen + ": the lock not read (" + e.GetType().Name + ")"); }
        }

        /// <summary>From P_Locked after the player locked, replaced or cleared a lock: the kept cards marked again when they are that screen's.</summary>
        static void Remark(UIGameplayUpgradeSelection sel)
        {
            if (_cards == null || Ptr(sel) != _selPtr || _selPtr == IntPtr.Zero) return;
            foreach (var c in _cards) c.Locked = false;
            State.LockedName = null; State.OnCards = null; State.Rarity = null;
            Read(sel);
        }

        // the screen's lock onto the kept cards and State (pointer equality, G.Same)
        static void Read(UIGameplayUpgradeSelection sel)
        {
            string name = null; Card on = null;
            switch (_screen)
            {
                case Screen.LevelUp:
                    {
                        var lu = sel.TryCast<UIGameplayLevelUp>(); var p = lu == null ? null : LevelUpLock(lu);
                        if (p == null) break;
                        name = G.Name(p);
                        foreach (var c in _cards) if (c.Powerup != null && G.Same(c.Powerup, p)) { c.Locked = true; on = c; break; }
                        break;
                    }
                case Screen.Chest:
                    {
                        var ch = sel.TryCast<UIGameplayChestOpened>(); var it = ch == null ? null : ChestLock(ch);
                        if (it == null) break;
                        name = G.Name(it);
                        foreach (var c in _cards) if (c.Item != null && G.Same(c.Item, it)) { c.Locked = true; on = c; break; }
                        break;
                    }
                case Screen.Military:
                    {
                        var m = sel.TryCast<UIGameplayMilitaryTraining>(); var p = m == null ? null : TrainingLock(m);
                        if (p == null) break;
                        name = G.Name(p);
                        foreach (var c in _cards) if (c.Powerup != null && G.Same(c.Powerup, p)) { c.Locked = true; on = c; break; }
                        try { State.Rarity = TrainingRarity(m); } catch { State.Rarity = null; }
                        break;
                    }
            }
            State.LockedName = name; State.OnCards = on;
        }

        /// <summary>From Advisor.Offer right before RerollHint.OnOffer (after the [card] lines): one '[lock]' line while the screen holds a lock;
        /// none while it holds none (it would add a line to every level-up). Once a run, a profile without the unlock is said.</summary>
        public static void LogOffer()
        {
            try
            {
                var st = State;
                bool lockScreen = st.Screen == "LevelUp" || st.Screen == "Chest" || st.Screen == "Military";
                if (lockScreen && st.Unlocked == false && !_offSaid)
                {
                    _offSaid = true;
                    Plugin.Logger.LogInfo("[lock] the Lockdown feature is not unlocked in this profile (Training Yard team tree, rank 3) - no LOCKDOWN hints; the lockdown items score without it");
                }
                if (st.LockedName == null) return;
                string where = st.OnCards != null ? "on the cards (#" + st.OnCards.Rank + ", " + F2(st.OnCards.Score) + ")" : "not on the cards (no longer in the pool)";
                Plugin.Logger.LogInfo("[lock] " + st.Screen + " " + st.Clock + ": " + st.LockedName + " locked - " + where + (st.Screen == "Military" && !string.IsNullOrEmpty(st.Rarity) ? " (" + st.Rarity + " kept)" : ""));
            }
            catch { }
        }

        /// <summary>The HUD went away (the run ended): the cached unlock, the once-a-run flags, the state, the kept cards, the walk's proof.</summary>
        public static void Forget()
        {
            _unlocked = null; _unlockRead = false; _unlockWarned = false; _offSaid = false;
            State = new LockState(); _cards = null; _selPtr = IntPtr.Zero;
            _proofStep = -1; _proofSel = IntPtr.Zero; _proofCard = null;
        }

        // ================================================================ the player's presses and locks
        // the Companion's name of a screen (as RerollHint.Banishing names them; advice_audit's SCREEN_OF maps both spellings)
        static string ScreenName(UIGameplayUpgradeSelection sel)
        {
            try
            {
                if (sel.TryCast<UIGameplayLevelUp>() != null) return "LevelUp";
                if (sel.TryCast<UIGameplayChestOpened>() != null) return "Chest";
                if (sel.TryCast<UIGameplayMilitaryTraining>() != null) return "Military";
                return sel.GetIl2CppType().Name.Replace("UIGameplay", "");
            }
            catch { return "?"; }
        }
        // the offer's clock on that screen, else the run's clock now
        static string ClockOf(UIGameplayUpgradeSelection sel)
        {
            if (Ptr(sel) == _selPtr && _selPtr != IntPtr.Zero && !string.IsNullOrEmpty(State.Clock)) return State.Clock;
            try { var gm = GameplayMaster.s_instance; var mode = gm == null ? null : gm.currentGameMode; if (mode != null) return mode.GetCurrentRunTimeMMSS(); } catch { }
            return "?";
        }

        /// <summary>The Lockdown button pressed (ClickLockdown; the Rewired hotkey ends there too): armed, put away, or ignored while the screen
        /// holds input.</summary>
        internal static void Pressed(UIGameplayUpgradeSelection sel, bool before)
        {
            try
            {
                bool now = Armed(sel);
                string head = "[pick] " + ScreenName(sel) + " " + ClockOf(sel) + ": the Lockdown button pressed";
                Plugin.Logger.LogInfo(now == before ? head + " - ignored (the screen holds input)" : head + (now ? " (armed)" : " (put away)"));
            }
            catch (Exception e) { Plugin.Logger.LogInfo("[pick] the Lockdown button pressed - not read (" + e.GetType().Name + ")"); }
        }

        /// <summary>What a screen's lock is, for P_Locked's prefix and postfix (a pointer and the game's name; zero: none).</summary>
        internal sealed class Held { public IntPtr Ptr; public string Name; }
        internal static Held LockOf(UIGameplayUpgradeSelection sel)
        {
            var h = new Held();
            try
            {
                var lu = sel.TryCast<UIGameplayLevelUp>();
                if (lu != null) { var p = LevelUpLock(lu); if (p != null) { h.Ptr = p.Pointer; h.Name = G.Name(p); } return h; }
                var ch = sel.TryCast<UIGameplayChestOpened>();
                if (ch != null) { var it = ChestLock(ch); if (it != null) { h.Ptr = it.Pointer; h.Name = G.Name(it); } return h; }
                var m = sel.TryCast<UIGameplayMilitaryTraining>();
                if (m != null) { var p = TrainingLock(m); if (p != null) { h.Ptr = p.Pointer; h.Name = G.Name(p); } return h; }
            }
            catch (Exception e) { h.Name = "?" + e.GetType().Name; }
            return h;
        }

        /// <summary>After a screen's own ProcessLockdown: the lock set, replaced or cleared - logged, the cards marked again, the hint told.</summary>
        internal static void Changed(UIGameplayUpgradeSelection sel, Held before)
        {
            try
            {
                var after = LockOf(sel);
                string head = "[pick] " + ScreenName(sel) + " " + ClockOf(sel) + ": ";
                if (before == null) before = new Held();
                if (after.Name != null && after.Name.StartsWith("?", StringComparison.Ordinal)) Plugin.Logger.LogInfo(head + "lockdown - the lock not read (" + after.Name.Substring(1) + ")");
                else if (after.Ptr != IntPtr.Zero && after.Ptr != before.Ptr)
                    Plugin.Logger.LogInfo(head + "lockdown on " + after.Name + Where(after) + (before.Ptr != IntPtr.Zero ? " (replaces " + before.Name + ")" : ""));
                else if (after.Ptr == IntPtr.Zero && before.Ptr != IntPtr.Zero) Plugin.Logger.LogInfo(head + "lockdown off " + before.Name);
                else Plugin.Logger.LogInfo(head + "lockdown - the lock unchanged (" + (after.Name ?? "none") + ")");
                Remark(sel);
                RerollHint.OnLockChanged(sel);
            }
            catch (Exception e) { Plugin.Logger.LogInfo("[pick] lockdown - not read (" + e.GetType().Name + ")"); }
        }

        // ' (#2, 3.40)': the locked card's place and score on the offer
        static string Where(Held h)
        {
            if (_cards == null) return "";
            foreach (var c in _cards)
            {
                IntPtr p = c.Item != null ? Ptr(c.Item) : Ptr(c.Powerup);
                if (p != IntPtr.Zero && p == h.Ptr) return " (#" + c.Rank + ", " + F2(c.Score) + ")";
            }
            return "";
        }

        // ================================================================ [Debug] PauseWalkLock: the walk's lock proof
        // Once a session, on the walk's first level-up with two cards or more (after its hint preview and hover tour): from 2.4 s after the
        // offer the Lockdown button is pressed and card #2 locked the game's way (OnPowerupButtonClicked routes the click to ProcessLockdown,
        // whose P_Locked logs it), a 'locked' shot 0.6 s later, then 1.0 s on the screen rerolled once when a reroll is left - the game draws the
        // locked card first, so the replaced offer's '[lock] ... on the cards' line proves the read and the mechanics together. It spends one
        // reroll of the walk's throwaway run. The walk then takes its pick as before.
        static int _proofStep = -1;                 // -1 not started this session, 1 locked (the reroll due), 0 done
        static float _proofAt; static IntPtr _proofSel; static string _proofCard;
        static bool WalkLock { get { try { return Plugin.PauseWalkLock != null && Plugin.PauseWalkLock.Value; } catch { return false; } } }

        /// <summary>From Advisor.DebugLockDue (the pause walk, stages 1 and 61, between the hover tour and the pick): true while the proof holds the
        /// offer. False (never holds) unless [Debug] PauseWalkLock is on and the offer is a level-up of two cards or more.</summary>
        internal static bool DebugDue(float now, UIGameplayUpgradeSelection sel, Screen screen, List<Card> cards, float offerAt)
        {
            if (_proofStep == 0 || !WalkLock) return false;
            try
            {
                if (_proofStep < 0)
                {
                    if (sel == null || cards == null || screen != Screen.LevelUp || cards.Count < 2 || now < offerAt + 2.4f) return false;
                    _proofStep = 0;                         // once a session, whatever comes of it
                    Card second = null; foreach (var c in cards) if (c.Rank == 2) { second = c; break; }
                    bool up = false; try { var b = LockButton(sel); up = b != null && b.gameObject.activeInHierarchy; } catch { }
                    string not = !State.Available ? "no Lockdown on this screen (" + (State.Unlocked == false ? "not unlocked in this profile" : "no Lockdown button") + ")"
                        : !up ? "the Lockdown button is not shown" : second == null || second.Button == null ? "no card #2" : !second.Lockable ? "card #2 (" + second.Name + ") is not lockdownable" : null;
                    if (not != null) { Plugin.Logger.LogInfo("[menu] pause walk: lock proof not reached - " + not); return false; }
                    sel.ClickLockdown();
                    bool armed = false; try { armed = Armed(sel); } catch { }
                    if (!armed) { Plugin.Logger.LogInfo("[menu] pause walk: lock proof not reached - the Lockdown press was ignored"); return false; }
                    sel.OnPowerupButtonClicked(second.Button);
                    Plugin.Logger.LogInfo("[menu] pause walk: lockdown on " + second.Name + " (debug)");
                    Shots.Later(0.6f, "locked", true);
                    _proofSel = Ptr(sel); _proofCard = second.Name; _proofAt = now + 1.0f; _proofStep = 1;
                    return true;
                }
                if (now < _proofAt) return true;
                _proofStep = 0;
                if (sel == null || Ptr(sel) != _proofSel) { Plugin.Logger.LogInfo("[menu] pause walk: lock proof cut short - the offer closed"); return false; }
                string why;
                if (!RerollHint.DebugRerollLeft(sel, out why)) { Plugin.Logger.LogInfo("[menu] pause walk: lock proof not reached - no reroll left (" + why + ")"); return false; }
                sel.ClickReroll();
                Plugin.Logger.LogInfo("[menu] pause walk: lock proof - rerolled (" + why + "); the new cards must hold " + _proofCard);
                return false;                               // the replaced offer comes; DebugPickDue takes it 2 s after it, as before
            }
            catch (Exception e) { _proofStep = 0; Plugin.Logger.LogWarning("[menu] pause walk: lock proof failed: " + e.Message); return false; }
        }
    }

    // ---- 0.17.0 (C16-06a): the presses and the locks, reads only (as the banish patches, RerollHint.cs). ClickLockdown: the base's only - no
    //      screen overrides it. ProcessLockdown: exactly the level-up, chest and training overrides (each a function of its own that ends in a
    //      jump into the base: the postfix runs after the base). NEVER the base UIGameplayUpgradeSelection.ProcessLockdown (every override jumps
    //      into it: one lock would be seen twice), never UIGameplayHashtagEvent's (an empty body that 4,075 methods share), never
    //      UIGameplayCharacterRescue's (unreachable: a rescue card is never lockdownable).
    [HarmonyPatch(typeof(UIGameplayUpgradeSelection), nameof(UIGameplayUpgradeSelection.ClickLockdown))]
    static class P_LockdownArmed
    {
        static void Prefix(UIGameplayUpgradeSelection __instance, out bool __state) { __state = false; try { __state = __instance._isLockdownActive; } catch { } }
        static void Postfix(UIGameplayUpgradeSelection __instance, bool __state) { Lockdown.Pressed(__instance, __state); }
    }

    [HarmonyPatch]
    static class P_Locked
    {
        static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach (var t in new[] { typeof(UIGameplayLevelUp), typeof(UIGameplayChestOpened), typeof(UIGameplayMilitaryTraining) })
            {
                var m = AccessTools.DeclaredMethod(t, nameof(UIGameplayUpgradeSelection.ProcessLockdown), new[] { typeof(UIPowerupButtonBase) });
                if (m != null) yield return m;
            }
        }
        static void Prefix(UIGameplayUpgradeSelection __instance, out Lockdown.Held __state) { __state = Lockdown.LockOf(__instance); }
        static void Postfix(UIGameplayUpgradeSelection __instance, Lockdown.Held __state) { Lockdown.Changed(__instance, __state); }
    }
}
