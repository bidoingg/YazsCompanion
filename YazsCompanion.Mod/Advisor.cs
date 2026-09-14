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
            try
            {
                Panel.ScreenOpened(sel);
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

                var sb = new StringBuilder();
                sb.Append("[offer] ").Append(screen).Append(' ').Append(snap.Clock).Append(" (").Append(snap.Mode).Append(" horde ").Append(snap.Horde).Append(")");
                bool reroll = false; try { reroll = sel.isReroll; } catch { }
                if (reroll) sb.Append(" reroll");
                Plugin.Logger.LogInfo(sb.ToString());
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
            }
            catch (Exception e) { Plugin.Logger.LogError("[pick] failed: " + e); }
        }

        static string Best(List<Card> cards) { foreach (var c in cards) if (c.Rank == 1) return c.Name; return "?"; }
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
    static class P_HudTick { static void Postfix(UIGameplay __instance) { Panel.Tick(__instance); } }
    [HarmonyPatch(typeof(UIGameplay), nameof(UIGameplay.OnDestroy))]
    static class P_HudGone { static void Postfix() { Panel.Reset(); } }
    // fallback tick source in case the HUD's Update never reaches us (the sidebar has not been seen in a run yet)
    [HarmonyPatch(typeof(GameplayMaster), nameof(GameplayMaster.Update))]
    static class P_MasterTick { static void Postfix() { Panel.Tick(null); Notice.Tick(); } }

    // ---- the restart notice after an auto-update: GameMaster lives in every scene, menu included ----
    [HarmonyPatch(typeof(GameMaster), nameof(GameMaster.Update))]
    static class P_Notice { static void Postfix() { Notice.Tick(); } }
}
