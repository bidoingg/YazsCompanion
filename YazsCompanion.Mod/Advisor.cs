// Entry points from the game: every selection screen after it filled its cards, and its close.
using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;

namespace YazsCompanion
{
    internal static class Advisor
    {
        static List<Card> _lastCards;
        static Screen _lastScreen;
        static string _lastClock = "";
        static float _lastT = -1f;           // the run clock (CurrentModePlayTime) of the last offer: a selection screen pauses it
        static bool _heldLineSaid, _itemsLineSaid;      // 0.16.0: a failed [held] / [items] held line said once a session

        /// <summary>The HUD went away (the run ended or the scene changed): no offer is open any more.</summary>
        public static void Forget() { _lastCards = null; _debugScreen = null; _lastT = -1f; RerollHint.Forget(); WhyUi.Forget(); Quest.Forget(); Badge.Forget(); }

        // 0.12.2 (F13): a reroll or a banish fills the SAME screen again - AssignGeneratedElements runs a second time with no
        // Hide in between (every close clears _lastCards), on the clock the screen holds still. The game's isReroll flag was
        // never seen true (0 of hundreds of offers on the PC and the Deck), so a replaced level-up was counted twice by the
        // pace estimate ('~87' then '~91 level-ups to come' in the same second). Not "same clock = same level-up": chained
        // level-ups share the paused clock too, but each one closes the screen before the next opens.
        static bool Replaced(Screen screen, float t) { return _lastCards != null && _lastScreen == screen && _lastT >= 0f && Math.Abs(t - _lastT) < 1f; }

        // what replaced the cards, from the game's own flags when it sets them: a banish swaps one card, a reroll the lot
        static string ReplacedBy(UIGameplayUpgradeSelection sel, int gone)
        {
            bool reroll = false, banish = false;
            try { reroll = sel._didUseRerollThisEvent || sel.isReroll; } catch { }
            try { banish = sel._didUseBanishThisChoice; } catch { }
            if (banish && (gone <= 1 || !reroll)) return "banish";
            if (reroll) return "reroll";
            return null;
        }

        static string ReplacedText(UIGameplayUpgradeSelection sel, List<Card> before, List<Card> now)
        {
            var gone = new List<string>(); var added = new List<string>();
            foreach (var c in before) if (!now.Exists(x => x.Name == c.Name)) gone.Add(c.Name);
            foreach (var c in now) if (!before.Exists(x => x.Name == c.Name)) added.Add(c.Name);
            string by = ReplacedBy(sel, gone.Count);
            return " replaced" + (by != null ? " (" + by + ")" : "") + ": gone " + (gone.Count > 0 ? string.Join(", ", gone) : "none") + "; new " + (added.Count > 0 ? string.Join(", ", added) : "none");
        }

        public static void OnOffer(UIGameplayUpgradeSelection sel, Screen screen)
        {
            long perf = Perf.Begin();
            try { using (G.Cache()) Offer(sel, screen); }
            finally { Perf.End("offer", perf); }
        }

        static void Offer(UIGameplayUpgradeSelection sel, Screen screen)
        {
            try
            {
                Panel.ScreenOpened(sel);
                float t = 0f; try { var gm = GameplayMaster.s_instance.currentGameMode; if (gm != null) t = gm.CurrentModePlayTime; } catch { }
                bool replaced = Replaced(screen, t);
                var before = replaced ? _lastCards : null;
                if (screen == Screen.LevelUp && !replaced)
                {
                    // the pace of the run: how many level-ups are still to come decides what is worth starting now; a
                    // replaced offer is the same level-up again
                    bool reroll0 = false; try { reroll0 = sel.isReroll; } catch { }
                    if (!reroll0) G.Pace.LevelUp(t);
                }
                var snap = G.Read();
                var cards = new List<Card>();
                var buttons = sel.powerupButtons;
                if (buttons == null) { Plugin.Logger.LogWarning("[offer] " + screen + ": no buttons"); return; }
                for (int i = 0; i < buttons.Length; i++)
                {
                    var b = buttons[i]; if (b == null) continue;
                    bool active; try { active = b.gameObject.activeInHierarchy; } catch { continue; }
                    if (!active) continue;
                    var c = new Card { Index = i, Button = b };
                    try { c.Powerup = b.attachedPowerup; } catch { }
                    try { c.Item = b.attachedItem; } catch { }
                    try { c.Hashtag = b.attachedHashtagEvent; } catch { }
                    if (c.Powerup == null && c.Item == null && c.Hashtag == null) { Badge.Hide(b); continue; }
                    cards.Add(c);
                }
                if (cards.Count == 0) { Plugin.Logger.LogInfo("[offer] " + screen + " " + snap.Clock + ": no active cards"); return; }

                Ranker.Rank(screen, cards, snap);
                if (Plugin.ShowBadges.Value) foreach (var c in cards) Badge.Show(c);
                // 10-07 review (C-m7): 1.0 s - after the WHY's own 0.6 s entrance delay and 0.12 s fade (at 0.6 s the shot came 3-38 ms
                // before the WHY was drawn); WhyUi adds a 'why' shot 0.4 s after the offer's first '[why]' line
                Shots.Later(1.0f, "offer");

                var sb = new StringBuilder();
                sb.Append("[offer] ").Append(screen).Append(' ').Append(snap.Clock).Append(" (").Append(snap.Mode).Append(" horde ").Append(snap.Horde).Append(")");
                if (before != null) sb.Append(ReplacedText(sel, before, cards));
                else { bool reroll = false; try { reroll = sel.isReroll; } catch { } if (reroll) sb.Append(" reroll"); }
                Plugin.Logger.LogInfo(sb.ToString());
                Plugin.Logger.LogInfo("[ctx] " + snap.Ctx + BuildsText(snap) + (snap.Boosts.Count > 0 ? " | boosts: " + string.Join(", ", snap.Boosts.ConvertAll(b => b.Name + " (" + b.Tag + ")")) : "") + LoadoutUi.CtxSuffix());
                // 0.16.0 (C16-01): what the held items change on this offer, and once a run what was read for them (HeldRules.cs)
                try
                {
                    string hl = HeldRules.Line(snap.Held, snap.Ctx); if (hl != null) Plugin.Logger.LogInfo(hl);
                    long masterKey = 0; try { var gpm = GameplayMaster.s_instance; if (gpm != null) masterKey = gpm.Pointer.ToInt64(); } catch { }
                    if (HeldRules.NewRun(snap.Seconds, masterKey)) { string rl = HeldRules.ReadsLine(snap.Held); if (rl != null) Plugin.Logger.LogInfo(rl); }
                }
                catch (Exception e) { if (!_heldLineSaid) { _heldLineSaid = true; Plugin.Logger.LogWarning("[held] line not written: " + e.Message + " (said once a session)"); } }
                // 0.16.0 (C16-02b): a chest's item reads - the items held, free slots, luck, pickup, speed - on every chest offer, not
                // behind LogSquad (ItemStats.cs; --check-log's 'items' check looks for it)
                if (screen == Screen.Chest)
                {
                    try { string il = ItemStats.HeldLine(snap); if (il != null) Plugin.Logger.LogInfo(il); }
                    catch (Exception e) { if (!_itemsLineSaid) { _itemsLineSaid = true; Plugin.Logger.LogWarning("[items] held line not written: " + e.Message + " (said once a session)"); } }
                }
                if (Plugin.LogSquad.Value) Plugin.Logger.LogInfo("[squad] " + snap.SquadText());
                if (Plugin.LogSquad.Value && (snap.Tags.Known || snap.Tags.Points.Count > 0))
                    Plugin.Logger.LogInfo("[tags] points: " + (snap.Tags.PointsText().Length > 0 ? snap.Tags.PointsText() : "none") + (snap.Tags.SpecialAt > 0 ? " (special at " + snap.Tags.SpecialAt + ")" : "")
                        + " | deals: " + (snap.Tags.Known ? snap.Tags.DealsText() : "unknown") + (snap.Tags.Focus() != null ? " | stack " + snap.Tags.Focus() : ""));
                foreach (var c in cards)
                {
                    var line = new StringBuilder();
                    line.Append("[card] #").Append(c.Rank).Append(c.Rank == 1 ? " PICK " : "      ").Append(c.Name).Append(" (").Append(c.Kind);
                    if (c.Owner != null) line.Append(", ").Append(c.Owner.Name);
                    line.Append(") ").Append(c.Score.ToString("0.00")).Append(" - ").Append(string.Join("; ", c.Why));
                    Plugin.Logger.LogInfo(line.ToString());
                }
                // 0.14.0 (A1): what the cards SAID, as drawn (their plain words, the names another mod lends, without "2ND" / "AVOID"),
                // best first - the [card] lines above keep the ranking's own headline
                if (Plugin.ShowBadges.Value) Plugin.Logger.LogInfo(ShownLine(screen, snap.Clock, cards));
                // 0.12.2: the rescue screen - is a reroll worth it? (judged again after a reroll: the replaced offer)
                // 0.14.0: and every other screen (REROLL / SKIP / BANISH: ScreenCall.cs); the WHY band of the selected card (WhyUi.cs)
                RerollHint.OnOffer(sel, screen, cards, snap, before != null);
                WhyUi.OnOffer(sel, screen, cards, snap.Clock, before != null);
                if (Plugin.Verbose.Value) Describe.Offer(sel, screen.ToString());
                _lastCards = cards; _lastScreen = screen; _lastClock = snap.Clock; _lastT = t;
                _debugScreen = sel; _debugOfferAt = UnityEngine.Time.realtimeSinceStartup;
            }
            catch (Exception e) { Plugin.Logger.LogError("[offer] " + screen + " failed: " + e); }
        }

        public static void OnClose(UIGameplayUpgradeSelection sel, UIPowerupButtonBase clicked)
        {
            try
            {
                Panel.ScreenClosed();
                RerollHint.Close();
                WhyUi.Close();
                string what = "skip / nothing";
                Card picked = null;
                if (clicked != null && _lastCards != null)
                    foreach (var c in _lastCards) if (c.Button != null && c.Button.Pointer == clicked.Pointer) { picked = c; break; }
                if (picked != null) what = picked.Name + " (#" + picked.Rank + (picked.Rank == 1 ? ", the pick" : ", pick was " + Best(_lastCards)) + ")";
                else if (clicked != null)
                {
                    PowerupBase p = null; ItemBase it = null;
                    try { p = clicked.attachedPowerup; } catch { }
                    try { it = clicked.attachedItem; } catch { }
                    what = p != null ? G.Name(p) : it != null ? G.Name(it) : "unknown card";
                }
                string screen = "?"; try { screen = sel.GetIl2CppType().Name.Replace("UIGameplay", ""); } catch { }
                Plugin.Logger.LogInfo("[pick] " + screen + " " + _lastClock + ": " + what);
                Shots.Later(1.2f, "pick");
            }
            catch (Exception e) { Plugin.Logger.LogError("[pick] failed: " + e); }
            // F13 counts a level-up as replaced while _lastCards is set: EVERY close must clear it, even one whose panel or pick lookup threw -
            // else the next chained level-up (same paused clock) would pass for a reroll and skip the pace count
            finally { _lastCards = null; }
        }

        static string Best(List<Card> cards) { foreach (var c in cards) if (c.Rank == 1) return c.Name; return "?"; }

        // [shown] LevelUp 03:12: 1 'Level 3 of 4 - evolution unlocks at level 4' | 2 'The Rifleman build's main ability' | ...
        static string ShownLine(Screen screen, string clock, List<Card> cards)
        {
            var sb = new StringBuilder("[shown] ");
            sb.Append(screen).Append(' ').Append(clock).Append(':');
            bool first = true;
            var ranked = new List<Card>(cards); ranked.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            foreach (var c in ranked)
            {
                string text; try { text = Badge.Text(c); } catch (Exception e) { text = "(not read: " + e.GetType().Name + ")"; }
                sb.Append(first ? " " : " | ").Append(c.Rank).Append(" '").Append(text).Append('\'');
                first = false;
            }
            return sb.ToString();
        }

        // ---- for the scripted pause walk ([Debug] PreviewPause) only: click the recommended card of the offer on screen ----
        static UIGameplayUpgradeSelection _debugScreen; static float _debugOfferAt;
        internal static int DebugPicks;       // cards the pause walk has taken this session
        internal static bool DebugPickDue(float now)
        {
            try
            {
                if (_lastCards == null || _debugScreen == null || now < _debugOfferAt + 2f) return false;      // let the cards land first
                foreach (var c in _lastCards)
                {
                    if (c.Rank != 1 || c.Button == null) continue;
                    Plugin.Logger.LogInfo("[menu] pause walk: taking " + c.Name);
                    var screen = _debugScreen; _debugScreen = null;
                    screen.OnPowerupButtonClicked(c.Button); DebugPicks++;
                    return true;
                }
            }
            catch (Exception e) { _debugScreen = null; Plugin.Logger.LogWarning("[menu] pause walk: pick failed: " + e.Message); }
            return false;
        }

        // ---- the pause walk's hover tour (C-m7 / C-m8 of the 10-07 release review): once a session, on the first offer of two cards or
        // more, 2 s in (the 'offer' and 'why' shots taken), the selection moves to the cards in screen positions 2, 3 and 4 in turn the
        // game's own way - EventSystem.SetSelectedGameObject, what the pad's focus and the game's first-card select do (never a click;
        // the card's OnSelected direct when that did not reach it in 0.8 s) - and each card's WHY is photographed 0.4 s after its
        // '[why]' line ('why_card2' ...: the middle cards, the right wing and the team panel's clearance at 21:9, the band following
        // the selection at 16:10); then the selection goes back to card 1 and the walk takes the pick as before
        static int _tourPos = -1;            // the screen position on show (2..4); -1 not started this session, 0 done
        static float _tourAt, _tourSelAt; static bool _tourShot, _tourDirect; static IntPtr _tourScreen;

        /// <summary>True while the tour holds the offer (the walk asks again shortly and takes no pick meanwhile).</summary>
        internal static bool DebugTourDue(float now)
        {
            if (_tourPos == 0) return false;
            try
            {
                if (_lastCards == null || _debugScreen == null || (_tourPos > 0 && _debugScreen.Pointer != _tourScreen))
                {
                    if (_tourPos > 0) { Plugin.Logger.LogInfo("[menu] pause walk: hover tour cut short at card " + _tourPos + " - the offer closed"); _tourPos = 0; }
                    return false;
                }
                if (_tourPos < 0)
                {
                    if (_lastCards.Count < 2 || now < _debugOfferAt + 2f) return false;     // a one-card offer: the next one
                    _tourScreen = _debugScreen.Pointer; _tourPos = 2; _tourAt = now; _tourSelAt = -1f;
                    Plugin.Logger.LogInfo("[menu] pause walk: hover tour - cards 2 to " + Math.Min(4, _lastCards.Count) + " of " + _lastCards.Count + " selected in turn (the game's selection, never a click), a 'why_card' shot 0.4 s after each '[why]' line");
                }
                if (now < _tourAt) return true;
                if (_tourPos > Math.Min(4, _lastCards.Count))
                {
                    var first = _lastCards[0];
                    TourSelect(first);
                    Plugin.Logger.LogInfo("[menu] pause walk: hover tour done - the selection back on card 1 (" + first.Name + ")");
                    _tourPos = 0; return false;
                }
                var c = _lastCards[_tourPos - 1];
                var p = c.Button == null ? IntPtr.Zero : c.Button.Pointer;
                if (_tourSelAt < 0f)
                {
                    _tourSelAt = now; _tourShot = false; _tourDirect = false;
                    Plugin.Logger.LogInfo("[menu] pause walk: hover card " + _tourPos + " - #" + c.Rank + " " + c.Name);
                    if (!TourSelect(c)) { _tourDirect = true; c.Button.OnSelected(); Plugin.Logger.LogInfo("[menu] pause walk: no EventSystem - card " + _tourPos + "'s OnSelected called direct"); }
                    _tourAt = now + 0.05f; return true;
                }
                if (!_tourShot)
                {
                    if (p != IntPtr.Zero && WhyUi.LastSaid == p && WhyUi.LastSaidAt >= _tourSelAt)
                    {
                        Shots.Later(Math.Max(0.05f, 0.4f - (now - WhyUi.LastSaidAt)), "why_card" + _tourPos, true);
                        _tourShot = true; _tourAt = now + 1.0f; return true;
                    }
                    if (!_tourDirect && now - _tourSelAt > 0.8f && WhyUi.SelectedNow != p)
                    {
                        _tourDirect = true; c.Button.OnSelected();
                        Plugin.Logger.LogInfo("[menu] pause walk: the selection did not reach card " + _tourPos + " in 0.8 s (no OnSelected) - its OnSelected called direct");
                        _tourAt = now + 0.05f; return true;
                    }
                    if (now - _tourSelAt > 2f)
                    {
                        Plugin.Logger.LogInfo("[menu] pause walk: no '[why]' line for card " + _tourPos + " within 2 s (" + (WhyUi.SelectedNow == p ? "selected" : "NOT selected") + ") - shot anyway");
                        Shots.Later(0.05f, "why_card" + _tourPos, true); _tourShot = true; _tourAt = now + 0.6f; return true;
                    }
                    _tourAt = now + 0.05f; return true;
                }
                _tourPos++; _tourSelAt = -1f; return true;
            }
            catch (Exception e) { _tourPos = 0; Plugin.Logger.LogWarning("[menu] pause walk: hover tour failed: " + e.Message); return false; }
        }

        // the game's own selection (its first-card select and the pad's focus go the same way); false without an EventSystem
        static bool TourSelect(Card c)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null || c.Button == null) return false;
            es.SetSelectedGameObject(c.Button.gameObject);
            return true;
        }

        static string BuildsText(Snapshot s)
        {
            var parts = new List<string>();
            // a lent build Auto follows says so (0.13.0, F02: it follows the tier-3 branch taken - "Tank Pellets (Weapon, Auto)"); 0.15.0
            // (C15-07): with the style's source - "(Ability, Auto, the build's own)", or "(Balanced, Auto, mine)" with LentBuildStyle = Mine
            foreach (var sv in s.Squad)
            {
                var b = sv.Build;
                if (b == null) { parts.Add(sv.Name + " Auto (" + Doctrine.Current.Style + ")"); continue; }
                StyleSource from; var style = Ranker.StyleOf(sv, out from);
                parts.Add(sv.Name + " " + b.Name + " (" + style + (from == StyleSource.Lent ? ", Auto, the build's own" : from == StyleSource.Mine ? ", Auto, mine" : "") + ")");
            }
            return parts.Count > 0 ? " | builds: " + string.Join(", ", parts) : "";
        }
    }

    // ---- offers: AssignGeneratedElements is virtual and overridden per screen, so patch each override ----
    [HarmonyPatch(typeof(UIGameplayLevelUp), nameof(UIGameplayLevelUp.AssignGeneratedElements))]
    static class P_LevelUp { static void Postfix(UIGameplayLevelUp __instance) { Advisor.OnOffer(__instance, Screen.LevelUp); } }
    [HarmonyPatch(typeof(UIGameplayChestOpened), nameof(UIGameplayChestOpened.AssignGeneratedElements))]
    static class P_Chest { static void Postfix(UIGameplayChestOpened __instance) { Advisor.OnOffer(__instance, Screen.Chest); } }
    [HarmonyPatch(typeof(UIGameplayMilitaryTraining), nameof(UIGameplayMilitaryTraining.AssignGeneratedElements))]
    static class P_Military { static void Postfix(UIGameplayMilitaryTraining __instance) { Advisor.OnOffer(__instance, Screen.Military); } }
    [HarmonyPatch(typeof(UIGameplayHashtagEvent), nameof(UIGameplayHashtagEvent.AssignGeneratedElements))]
    static class P_Hashtag { static void Postfix(UIGameplayHashtagEvent __instance) { Advisor.OnOffer(__instance, Screen.Hashtag); } }
    [HarmonyPatch(typeof(UIGameplayCharacterRescue), nameof(UIGameplayCharacterRescue.AssignGeneratedElements))]
    static class P_Rescue { static void Postfix(UIGameplayCharacterRescue __instance) { Advisor.OnOffer(__instance, Screen.SOS); } }

    // ---- close: the base Hide(clicked) runs once per screen for every type (the chest override calls it) ----
    [HarmonyPatch(typeof(UIGameplayUpgradeSelection), nameof(UIGameplayUpgradeSelection.Hide))]
    static class P_Hide { static void Postfix(UIGameplayUpgradeSelection __instance, UIPowerupButtonBase clicked) { Advisor.OnClose(__instance, clicked); } }

    // ---- heartbeat for the sidebar: the HUD's own Update (UIGameplay owns the gameplay canvas), throttled inside
    //      Panel.Tick; when the HUD is destroyed with the scene, so is the sidebar, so forget it ----
    [HarmonyPatch(typeof(UIGameplay), nameof(UIGameplay.Update))]
    static class P_HudTick
    {
        // 0.14.0: WideMenus after the game's own frame switch of this frame (its Update ends with it)
        static void Postfix(UIGameplay __instance) { long t = Perf.Begin(); Fx.Tick(); Badge.Tick(); Panel.Tick(__instance); WideMenus.Tick(__instance); Perf.End("tick.hud", t); }
    }
    [HarmonyPatch(typeof(UIGameplay), nameof(UIGameplay.OnDestroy))]
    static class P_HudGone { static void Postfix() { Panel.Reset(); Advisor.Forget(); LoadoutUi.RunGone(); WideMenus.Forget(); } }

    // the Training Yard: advice on the tab that is open, and the node under the cursor for its WHY row
    [HarmonyPatch(typeof(UIViewSkillTree), nameof(UIViewSkillTree.Update))]
    static class P_YardTick
    {
        static void Postfix(UIViewSkillTree __instance) { long t = Perf.Begin(); Fx.Tick(); TreeUi.Tick(__instance); Perf.End("tick.yard", t); }
    }
    [HarmonyPatch(typeof(UIViewSkillTree), nameof(UIViewSkillTree.OnHighlighted))]
    static class P_YardHighlight { static void Postfix(UISkillTreeNode __0) { TreeUi.Highlighted(__0); } }

    // ---- GameMaster lives in every scene, menu included: the restart notice after an auto-update, the mod menu, the
    //      debug walks - and the readout's fallback tick should the HUD's own Update ever stop reaching us during a run
    //      (up to 0.10.0 a second per-frame hook on GameplayMaster.Update did that; the HUD tick has never failed) ----
    [HarmonyPatch(typeof(GameMaster), nameof(GameMaster.Update))]
    static class P_Notice
    {
        static void Postfix()
        {
            long t = Perf.Begin();
            Fx.Tick(); Panel.FallbackTick(); Notice.Tick(); Preview.Tick(); Probe.Tick(); Menu.Tick(); Shots.Tick(); Warmup.Tick(); RerollHint.Tick(); WhyUi.Tick(); LoadoutUi.FallbackTick(); WideMenus.MenuTick(); Plugin.CheckKeysOnce();
            Perf.End("tick.master", t);
            Perf.Frame();
        }
    }
}
