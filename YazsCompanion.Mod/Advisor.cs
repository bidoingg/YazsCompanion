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
                if (screen == Screen.LevelUp)
                {
                    // the pace of the run: how many level-ups are still to come decides what is worth starting now
                    float t = 0f; try { var gm = GameplayMaster.s_instance.currentGameMode; if (gm != null) t = gm.CurrentModePlayTime; } catch { }
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
                Shots.Later(0.6f, "offer");

                var sb = new StringBuilder();
                sb.Append("[offer] ").Append(screen).Append(' ').Append(snap.Clock).Append(" (").Append(snap.Mode).Append(" horde ").Append(snap.Horde).Append(")");
                bool reroll = false; try { reroll = sel.isReroll; } catch { }
                if (reroll) sb.Append(" reroll");
                Plugin.Logger.LogInfo(sb.ToString());
                Plugin.Logger.LogInfo("[ctx] " + snap.Ctx + BuildsText(snap) + (snap.Boosts.Count > 0 ? " | boosts: " + string.Join(", ", snap.Boosts.ConvertAll(b => b.Name + " (" + b.Tag + ")")) : ""));
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
                if (Plugin.Verbose.Value) Describe.Offer(sel, screen.ToString());
                _lastCards = cards; _lastScreen = screen; _lastClock = snap.Clock;
                _debugScreen = sel; _debugOfferAt = UnityEngine.Time.realtimeSinceStartup;
            }
            catch (Exception e) { Plugin.Logger.LogError("[offer] " + screen + " failed: " + e); }
        }

        public static void OnClose(UIGameplayUpgradeSelection sel, UIPowerupButtonBase clicked)
        {
            try
            {
                Panel.ScreenClosed();
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
                _lastCards = null;
                Shots.Later(1.2f, "pick");
            }
            catch (Exception e) { Plugin.Logger.LogError("[pick] failed: " + e); }
        }

        static string Best(List<Card> cards) { foreach (var c in cards) if (c.Rank == 1) return c.Name; return "?"; }

        // ---- for the scripted pause walk ([Debug] PreviewPause) only: click the recommended card of the offer on screen ----
        static UIGameplayUpgradeSelection _debugScreen; static float _debugOfferAt;
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
                    screen.OnPowerupButtonClicked(c.Button);
                    return true;
                }
            }
            catch (Exception e) { _debugScreen = null; Plugin.Logger.LogWarning("[menu] pause walk: pick failed: " + e.Message); }
            return false;
        }

        static string BuildsText(Snapshot s)
        {
            var parts = new List<string>();
            foreach (var sv in s.Squad) { var b = sv.Build; parts.Add(sv.Name + " " + (b != null ? b.Name + " (" + b.Style + ")" : "Auto (" + Doctrine.Current.Style + ")")); }
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
        static void Postfix(UIGameplay __instance) { long t = Perf.Begin(); Fx.Tick(); Panel.Tick(__instance); Perf.End("tick.hud", t); }
    }
    [HarmonyPatch(typeof(UIGameplay), nameof(UIGameplay.OnDestroy))]
    static class P_HudGone { static void Postfix() { Panel.Reset(); } }

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
            Fx.Tick(); Panel.FallbackTick(); Notice.Tick(); Preview.Tick(); Probe.Tick(); Menu.Tick(); Shots.Tick(); Warmup.Tick();
            Perf.End("tick.master", t);
            Perf.Frame();
        }
    }
}
