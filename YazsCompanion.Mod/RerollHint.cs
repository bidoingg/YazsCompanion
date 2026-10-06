// The action hints of the selection screens. 0.12.2: the reroll hint on the rescue (SOS) screen. In a round of Steam Deck logs
// the player rerolled rescue cards five times on four screens until the survivor the readout's SOS row named came up, and
// chose a hint for it: when the survivor the Companion rates best among those who could still come beats every card on offer
// by a clear margin (RerollCall in Synergy.cs, pure - the offline bench replays it) and the game still has a reroll for the
// screen, the game's Reroll button gets the recommended card's gold frame and a line stands over it, in the band between the
// cards' reason lines and the button:
//     REROLL  -  Tank would fit this squad better
// Who "could still come" follows how the game draws the cards (PowerupReferences.GetRandomCharacterUnlocks: the classes'
// UnlockCharacterPowerup cards, minus the cards shown before a reroll - the screen's _preRerollCharacters): every class
// unlocked in the profile that is not on the squad, not on the cards and not held back after a reroll, scored like the
// SOS cards (Ranker.Recruitable, shared with the readout's SOS row); where the class's own unlock card can be found its
// IsAvailable() must agree (a check that refuses every class is not trusted, and the log says so). The reroll count is
// the game's own for that screen: what it hands SetActionButtonsInteractivity (hooked below), else the team's "Rerolls
// available" statistic (the very number the game hands over - the cards are filled before the buttons are refreshed),
// else the number on the Reroll button (stale until that refresh: "0" on a session's first rescue screen); a FREE reroll
// counts too. When the game's count comes and differs from the one judged with (or its refresh changed the FREE label or
// the Reroll button with it), the hint is judged again once, on the next frame ('re-judged with rerolls N'; RerollCount
// in Synergy.cs). A reroll fills the screen again (Advisor's
// replaced offer): the hint is judged again; the pick takes it away. One '[squad] reroll hint: ...' line per
// evaluation, with the scores. The drawn names go through Names (another mod's names); the log keeps the game's.
// 0.13.0 (C1): the active quest's team rule goes first (RerollCall's quest argument, QuestTeam.cs): at the quest's limit no
// hint; while it needs a class the squad lacks, the hint speaks for that class alone ("REROLL - the quest needs Huntress").
// 0.14.0 (roadmap item 5 of the 10-05 review): the same hint on every other selection screen - the level-up, the chest, the
// military training, the Research Pod - with its own verdict (ScreenCall.cs, pure): REROLL when the best card is under the
// screen's floor ("REROLL  -  the best card here is weak for this squad (2 rerolls left)"), SKIP when every card would hurt the squad
// or the skip bonus beats them all, BANISH (off by default) for a card the build skips - over the Reroll, the Skip or the
// Banish button, one hint at most per screen; '[squad] action hint: ...' per evaluation. [Advice] ActionHints picks the
// actions; the rescue screen keeps its own switch ([Advice] RerollHint) and its words. The game's banishes are logged as
// '[pick] ... banish on X (#3, 0.85)' (66,000 log lines had none, unused or undetected). The line's place is the band
// manager's (ScreenBand.Hint, pure; the rule of 0.12.2 unchanged); the WHY band under the cards (WhyUi.cs) keeps clear of it.
// Placed 0.6 s after the cards came (the screen has flown in by then), measured from what is on screen: over the button
// when the band up to the lowest card line is tall enough, else under it, else the frame alone. Nothing here
// takes clicks. Motion only on appear: the frame settles, the line unfolds from its left tip and types on, the tip pings.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CT = GamePlayer.CharacterType;

namespace YazsCompanion
{
    /// <summary>[Advice] ActionHints: which actions the hints of the level-up, chest, military and Research Pod screens advise.</summary>
    [Flags] public enum HintActions { None = 0, Reroll = 1, Skip = 2, Banish = 4 }

    internal static class RerollHint
    {
        const string FrameName = "YazsRerollFrame", LineName = "YazsRerollHint";
        // canvas units at scale 1 (the card badges' sizes: the reason line's font, the ribbon's tips)
        const float Font = 40f, LineH = 62f, Tip = 36f, Pad = 26f, MaxW = 1800f, Gap = ScreenBand.Gap, PlaceDelay = 0.6f;
        const float MinTextPx = 16f, MaxScale = 1.3f;

        sealed class Avail
        {
            public bool Button, Interactable, Free; public int Label = -1, Stat = -1;          // the Reroll button
            public bool Skip, SkipUp; public int Cash, Heal;                                   // the Skip button, what it grants
            public bool Banish, BanishUp, BanishFree; public int BanishStat = -1;               // the Banish button
        }
        sealed class Inputs { public List<Recruit> Offered, Possible; public double Liberate = -1, RecruitValue; public Avail Rr; public string Pool = ""; public QuestSos Quest; }

        static UIGameplayUpgradeSelection _sel;          // the screen being advised (null: none)
        static IntPtr _selPtr;
        static List<Card> _cards;
        static Screen _screen;
        static string _clock = "";
        static Inputs _in;                               // the rescue screen's
        static ScreenCallIn _gin;                        // 0.14.0: the other screens'
        static Avail _av;                                // ... what their buttons said
        static ScreenCall _call;
        static RerollCall _rescue;
        static string _text;                             // the line while the hint is wanted, null = hidden
        static Button _button;                           // the button the hint is about (its frame)
        static float _placeAt = -1f;
        static bool _shown;
        static readonly RerollCount _count = new RerollCount();     // the count the verdict uses, the game's for this offer, a re-judge due
        static bool _rejudge;                            // the game's count differs from the one judged with: judge again on the next Tick
        static IntPtr _hookPtr; static int _hookFrame = -9, _hookCount = -1, _hookBanish = -1; static bool _hookSaid;
        static readonly Il2CppStructArray<Vector3> _corners = new Il2CppStructArray<Vector3>(4);

        static bool On { get { try { return Plugin.AdviceRerollHint.Value; } catch { return true; } } }
        static HintActions Actions { get { try { return Plugin.AdviceActionHints.Value; } catch { return HintActions.Reroll | HintActions.Skip; } } }
        static bool OnFor(Screen screen) { return screen == Screen.SOS ? On : Actions != HintActions.None; }
        static IntPtr Ptr(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase o) { try { return o == null ? IntPtr.Zero : o.Pointer; } catch { return IntPtr.Zero; } }
        static string F2(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        static string Tag { get { return _screen == Screen.SOS ? "[squad] reroll hint: " : "[squad] action hint: "; } }

        /// <summary>The hint's line on the screen now, in view units (top-left origin, y down) - the WHY band keeps clear of it.
        /// Empty: no line.</summary>
        internal static R4 LineRect { get; private set; }

        // ---------------------------------------------------------------- the game's side
        /// <summary>From Advisor, once the cards are ranked (and again when a reroll or a banish filled the screen anew).</summary>
        public static void OnOffer(UIGameplayUpgradeSelection sel, Screen screen, List<Card> cards, Snapshot s, bool replaced)
        {
            try
            {
                var p = Ptr(sel);
                // the game may hand the screen its count just before it fills the cards (a reroll refreshes the buttons
                // first): keep that one, else the count comes right after the cards (the screen opening) - see RerollCount
                int hook = _hookPtr == p && Time.frameCount - _hookFrame <= 1 ? _hookCount : -1;
                if (_sel != null && _selPtr != p) Hide();
                _sel = sel; _selPtr = p; _cards = cards; _screen = screen; _clock = s.Clock; _call = null; _rescue = null; _rejudge = false;
                if (!OnFor(screen))
                {
                    _in = null; _gin = null; _count.Reset();
                    Hide();
                    Plugin.Logger.LogInfo(screen == Screen.SOS ? "[squad] reroll hint: not shown - off ([Advice] RerollHint = false)" : "[squad] action hint: " + screen + " " + s.Clock + ": not shown - off ([Advice] ActionHints = None)");
                    return;
                }
                var av = Read(sel);
                _count.Offer(hook, av.Stat, av.Label);
                if (screen == Screen.SOS)
                {
                    _gin = null;
                    var inp = new Inputs { Offered = new List<Recruit>(), RecruitValue = s.Ctx.RecruitValue, Rr = av, Quest = s.Quest };
                    foreach (var c in cards)
                    {
                        if (c.Recruit != null) inp.Offered.Add(c.Recruit);
                        else if (c.Kind == "liberate") inp.Liberate = Math.Max(inp.Liberate, c.Score);
                    }
                    inp.Possible = Possible(sel, s, inp.Offered, out inp.Pool);
                    _in = inp;
                }
                else
                {
                    _in = null; _av = av;
                    _gin = Inputs0(screen, cards, s, av, hook >= 0 && _hookBanish >= 0 && _hookPtr == p ? _hookBanish : -1);
                }
                Decide(replaced ? "the cards were replaced" : null, true);
            }
            catch (Exception e) { Plugin.Logger.LogWarning(Tag + "not judged - " + e.Message); _in = null; _gin = null; _count.Reset(); Hide(); }
        }

        // what the other screens' verdict reads: the ranked cards, the clock, the squad's health, the skip bonus, the quest
        static ScreenCallIn Inputs0(Screen screen, List<Card> cards, Snapshot s, Avail av, int banishes)
        {
            var acts = Actions;
            var x = new ScreenCallIn
            {
                Screen = screen.ToString(), Progress = s.Ctx.Progress, Health = s.Ctx.Health, Survival = s.Ctx.Survival, Cash = s.Ctx.Cash,
                Reroll = (acts & HintActions.Reroll) != 0, Skip = (acts & HintActions.Skip) != 0, Banish = (acts & HintActions.Banish) != 0,
                CanSkip = av.Skip && av.SkipUp, SkipCash = av.Cash, SkipHeal = av.Heal,
                Banishes = banishes >= 0 ? banishes : av.BanishStat, Quest = s.Rules,
            };
            x.CanBanish = av.Banish && av.BanishUp && (x.Banishes > 0 || av.BanishFree);
            try { var q = s.Rules; if (screen == Screen.Chest && q != null && q.Moves && q.Any(QuestAsk.NoItems)) x.NoItems = "take no items"; } catch { }
            foreach (var c in cards)
            {
                var k = new HintCard { Name = c.Name, Kind = c.Kind, Score = c.Score, Rank = c.Rank, Head = c.Reason, Class = c.Owner != null ? c.Owner.Name : null };
                var w = c.Say;
                if (w != null) { k.Base = w.Base; k.Build = w.Build; k.Skipped = w.Kind == SayKind.Ability && w.HeadRank >= 6; }
                k.Banishable = Banishable(c);
                if (c.Item != null) { var h = Quest.IsHealthItem(c.Item); k.Health = h == true; }
                x.Cards.Add(k);
            }
            return x;
        }

        // the game's own flag on the card's powerup or item (unreadable: not banishable - never advise what may be refused)
        [MethodImpl(MethodImplOptions.NoInlining)] static bool PowerupBanishable(PowerupBase p) { return p.isBanishable; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool ItemBanishable(ItemBase it) { return it.isBanishable; }
        static bool Banishable(Card c)
        {
            try
            {
                if (c.Hashtag != null) return false;
                if (c.Item != null) return ItemBanishable(c.Item);
                if (c.Powerup != null) return PowerupBanishable(c.Powerup);
            }
            catch { }
            return false;
        }

        /// <summary>From the SetActionButtonsInteractivity post-fixes: the game's reroll (and banish) count for the screen. When the
        /// reroll count differs from the one the hint was judged with, the hint is judged again once on the next Tick (logged
        /// 're-judged with rerolls N'); the same count only becomes the game's. The pointer test is all a call costs while no
        /// screen is advised.</summary>
        public static void OnButtons(UIGameplayUpgradeSelection sel, int numRerolls, int numBanishes)
        {
            var p = Ptr(sel);
            _hookPtr = p; _hookFrame = Time.frameCount; _hookCount = numRerolls; _hookBanish = numBanishes;
            if (p != _selPtr || (_in == null && _gin == null)) return;
            if (_gin != null && numBanishes >= 0 && _gin.Banishes != numBanishes) _gin.Banishes = numBanishes;
            if (_count.Seen(numRerolls)) return;
            try
            {
                int was = _count.Judged; string wasFrom = _count.From;
                // what the game just left on the buttons (its count on the label, the FREE label): the verdict read them before
                var now = Read(sel); var before = _in != null ? _in.Rr : _av;
                var changed = new List<string>();
                if (before != null && now.Free != before.Free) changed.Add(now.Free ? "a FREE reroll now" : "no FREE reroll now");
                if (before != null && now.Button != before.Button) changed.Add(now.Button ? "the Reroll button shown now" : "no Reroll button now");
                if (_in != null) _in.Rr = now; else _av = now;
                bool again = _count.Buttons(numRerolls, changed.Count > 0);
                _againWhy = Count(was) + " (" + wasFrom + ")" + (changed.Count > 0 ? "; " + string.Join(", ", changed) : "");
                if (!_hookSaid)
                {
                    _hookSaid = true;
                    Plugin.Logger.LogInfo(Tag + "the screen hands its reroll count to SetActionButtonsInteractivity (" + numRerolls + ") - that count comes first from here on; "
                        + (again ? "the hint was judged with " + _againWhy + " - judged again" : "the same count the hint was judged with (" + wasFrom + ")"));
                }
                _rejudge = again && OnFor(_screen);
            }
            catch (Exception e) { Plugin.Logger.LogWarning(Tag + e.Message); }
        }

        static string _againWhy = "";                    // what the re-judge's log line says the cards were judged with before
        static string Count(int n) { return n >= 0 ? n.ToString(CultureInfo.InvariantCulture) : "?"; }

        /// <summary>From Advisor: a selection screen closed (the pick, a skip) - the hint goes with it.</summary>
        public static void Close() { Hide(); _sel = null; _selPtr = IntPtr.Zero; _cards = null; _in = null; _gin = null; _av = null; _call = null; _rescue = null; _count.Reset(); _rejudge = false; }

        /// <summary>The HUD went away with the scene: nothing of ours is left to hide (the unlock cards are looked up again next run).</summary>
        public static void Forget() { _placeAt = -1f; _text = null; _shown = false; _button = null; LineRect = new R4(); _sel = null; _selPtr = IntPtr.Zero; _cards = null; _in = null; _gin = null; _av = null; _call = null; _rescue = null; _count.Reset(); _rejudge = false; _hookPtr = IntPtr.Zero; _unlockCards = null; _banishing = null; }

        /// <summary>From the GameMaster.Update post-fix (it ticks while the screen holds the game): a bool and a float compare a frame.</summary>
        public static void Tick()
        {
            if (_rejudge) Rejudge();
            if (_placeAt < 0f || Time.realtimeSinceStartup < _placeAt) return;
            _placeAt = -1f;
            Place();
        }

        // the game's count differed from the one the cards were judged with: judge them again, once, with what the game left on
        // the buttons (its count, the FREE label; read in OnButtons) - unless new cards came first (they took that count) or the
        // screen closed
        static void Rejudge()
        {
            _rejudge = false;
            try
            {
                int n = _count.Due();
                if (n < 0 || (_in == null && _gin == null) || _sel == null || !OnFor(_screen)) return;
                Decide("re-judged with rerolls " + n + " - " + RerollCount.FromGame + "; judged before with " + _againWhy, true);
            }
            catch (Exception e) { Plugin.Logger.LogWarning(Tag + "not re-judged - " + e.Message); }
        }

        // what the game says about the action buttons on this screen right now
        static Avail Read(UIGameplayUpgradeSelection sel)
        {
            var a = new Avail();
            Button b = null;
            try { b = sel.rerollButton; } catch { }
            try { a.Button = b != null && b.gameObject.activeInHierarchy; } catch { }
            try { a.Interactable = b != null && b.interactable; } catch { }
            try
            {
                var t = sel.rerollButtonCountText; int n;
                if (t != null && t.gameObject.activeInHierarchy && int.TryParse((t.text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) a.Label = n;
            }
            catch { }
            try { var f = sel.rerollFreeLabel; a.Free = f != null && f.activeInHierarchy; } catch { }
            try
            {
                // truncated like the game's RefreshActionButtons does before it hands the count to the buttons
                var gm = GameplayMaster.s_instance; var ts = gm == null ? null : gm.teamStatistics;
                if (ts != null)
                {
                    float v = ts.GetStatisticValue(PlayerStatistic.EType.TeamNumRerolls); if (!float.IsNaN(v)) a.Stat = Math.Max(0, (int)v);
                    float bv = ts.GetStatisticValue(PlayerStatistic.EType.TeamNumBanishes); if (!float.IsNaN(bv)) a.BanishStat = Math.Max(0, (int)bv);
                }
            }
            catch { }
            // 0.14.0: the Skip and Banish buttons and what a skip grants (the game's own GetSkipBonusMoney / GetSkipBonusHp: reads only)
            Button sk = null; try { sk = sel.skipButton; } catch { }
            try { a.Skip = sk != null && sk.gameObject.activeInHierarchy; a.SkipUp = sk != null && sk.interactable; } catch { }
            try { a.Cash = SkipCash(sel); } catch { }
            try { a.Heal = SkipHeal(sel); } catch { }
            Button bn = null; try { bn = sel.banishButton; } catch { }
            try { a.Banish = bn != null && bn.gameObject.activeInHierarchy; a.BanishUp = bn != null && bn.interactable; } catch { }
            try { var f = sel.banishFreeLabel; a.BanishFree = f != null && f.activeInHierarchy; } catch { }
            return a;
        }
        [MethodImpl(MethodImplOptions.NoInlining)] static int SkipCash(UIGameplayUpgradeSelection sel) { return sel.GetSkipBonusMoney(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static int SkipHeal(UIGameplayUpgradeSelection sel) { return sel.GetSkipBonusHp(); }

        // the count of the verdict (RerollCount: the game's, else the statistic, else the button's number) and how the log words it
        static bool CanReroll(Avail a, out string text)
        {
            int n = _count.Judged; string from = _count.From;
            text = Count(n) + " (" + from + (a.Free ? ", a FREE reroll" : "") + (a.Button ? "" : ", no Reroll button shown")
                + (from != RerollCount.FromLabel && a.Label >= 0 && a.Label != n ? ", the Reroll button still shows " + a.Label : "")
                + (from == RerollCount.FromLabel && a.Stat >= 0 && a.Stat != n ? ", team statistic " + a.Stat : "") + ")";
            return RerollCount.Can(a.Button, a.Interactable, a.Free, n);
        }

        // who could still come: unlocked, not on the squad, not on the cards, not held back after a reroll, and - where the
        // class's unlock card is found - available by the game's own check
        static List<Recruit> Possible(UIGameplayUpgradeSelection sel, Snapshot s, List<Recruit> offered, out string note)
        {
            var onCards = new HashSet<int>(offered.Select(r => r.Class));
            var held = new HashSet<int>();
            try
            {
                var rescue = sel.TryCast<UIGameplayCharacterRescue>();
                var pre = rescue == null ? null : rescue._preRerollCharacters;
                foreach (var p in G.Each(pre))
                {
                    ClassProperties cp = null; try { if (p != null) cp = p.targetClassProperties; } catch { }
                    if (cp != null) { int c = (int)cp.characterType; if (!onCards.Contains(c)) held.Add(c); }
                }
            }
            catch { }
            var list = Ranker.Recruitable(s, cls => onCards.Contains((int)cls) || held.Contains((int)cls));
            var parts = new List<string>();
            if (held.Count > 0) parts.Add("held back after a reroll: " + string.Join(", ", held.Select(c => G.ClassName((CT)c))));
            // the game's own check on each class's unlock card
            var refused = new List<Recruit>(); int asked = 0;
            foreach (var r in list)
            {
                var card = UnlockCard(r.Class); if (card == null) continue;
                bool ok = true; try { ok = card.IsAvailable(); asked++; } catch { continue; }
                if (!ok) refused.Add(r);
            }
            if (asked == 0 && list.Count > 0) parts.Add("no unlock card to ask the game about");
            else if (refused.Count > 0 && refused.Count == list.Count) parts.Add("the game's IsAvailable() refused all " + list.Count + " - not trusted, kept");
            else if (refused.Count > 0) { parts.Add("the game's IsAvailable() drops " + string.Join(", ", refused.Select(r => r.Name))); list = list.Where(r => !refused.Contains(r)).ToList(); }
            note = parts.Count > 0 ? " (" + string.Join("; ", parts) + ")" : "";
            return list;
        }

        // each class's UnlockCharacterPowerup, found once a session in the game's powerup lists (asset data)
        static Dictionary<int, PowerupBase> _unlockCards;
        static PowerupBase UnlockCard(int cls)
        {
            if (_unlockCards == null)
            {
                _unlockCards = new Dictionary<int, PowerupBase>();
                try
                {
                    var refs = PowerupReferences.Get;
                    if (refs != null)
                        foreach (var list in new[] { refs.skillPowerups, refs.stillAvailableSkillPowerups })
                            foreach (var p in G.Each(list))
                            {
                                if (p == null || p.TryCast<UnlockCharacterPowerup>() == null) continue;
                                ClassProperties cp = null; try { cp = p.targetClassProperties; } catch { }
                                if (cp != null && !_unlockCards.ContainsKey((int)cp.characterType)) _unlockCards[(int)cp.characterType] = p;
                            }
                }
                catch { }
            }
            PowerupBase found; return _unlockCards.TryGetValue(cls, out found) ? found : null;
        }

        // ---------------------------------------------------------------- the verdict
        static void Decide(string when, bool always)
        {
            if (_in == null && _gin == null) return;
            ScreenCall call;
            string rr;
            if (_in != null)
            {
                bool can = CanReroll(_in.Rr, out rr);
                var r = RerollCall.Decide(_in.Offered, _in.Liberate, _in.Possible, can, rr, _in.RecruitValue, _in.Quest);     // 0.13.0 (C1): the quest's team rule first
                _rescue = r;
                call = ScreenCall.FromRescue(r, r.Show ? LineText(r) : null);
            }
            else
            {
                _gin.CanReroll = CanReroll(_av, out rr); _gin.Rerolls = _count.Judged; _gin.FreeReroll = _av.Free; _gin.RerollsText = rr;
                _gin.CanBanish = _av.Banish && _av.BanishUp && (_gin.Banishes > 0 || _av.BanishFree);
                call = ScreenCall.Decide(_gin);
            }
            bool flipped = _call == null || _call.Show != call.Show || _call.Action != call.Action;
            _call = call;
            if (always || flipped) { if (_in != null) Log(_rescue, rr, when); else LogScreen(call, when); }
            if (!call.Show) { Hide(); return; }
            string text = _in != null ? call.Words : Head(call.Action) + Wording.Safe(Names.Text(call.Words));
            Button b = ButtonOf(call.Action);
            if (_shown && text == _text && b != null && _button != null && b.Pointer == _button.Pointer) return;     // the same words on screen already (a count that moved, still > 0)
            if (_shown && (_button == null || b == null || b.Pointer != _button.Pointer)) Hide();                      // another action: its own button
            _text = text; _button = b; _placeAt = Time.realtimeSinceStartup + (_shown ? 0.05f : PlaceDelay);
        }

        static Button ButtonOf(HintAction a)
        {
            try
            {
                if (_sel == null) return null;
                return a == HintAction.Skip ? _sel.skipButton : a == HintAction.Banish ? _sel.banishButton : _sel.rerollButton;
            }
            catch { return null; }
        }

        static void Log(RerollCall c, string rr, string when)
        {
            var sb = new System.Text.StringBuilder("[squad] reroll hint: ");
            if (when != null) sb.Append("(").Append(when).Append(") ");
            sb.Append(c.Show ? "SHOWN - " : "not shown - ").Append(c.Why);
            sb.Append(" | rerolls ").Append(rr);
            var on = new List<string>();
            foreach (var r in _in.Offered.OrderBy(r => r, Comparer<Recruit>.Create(Recruit.Compare))) on.Add(r.Name + " " + F2(Math.Round(r.Score, 2)));
            if (_in.Liberate >= 0) on.Add("Liberate " + F2(Math.Round(_in.Liberate, 2)));
            sb.Append(" | on the cards: ").Append(on.Count > 0 ? string.Join(", ", on) : "none");
            var could = _in.Possible.OrderBy(r => r, Comparer<Recruit>.Create(Recruit.Compare)).Select(r => r.Name + " " + F2(Math.Round(r.Score, 2))).ToList();
            sb.Append(" | could still come: ").Append(could.Count > 0 ? string.Join(", ", could) : "nobody").Append(_in.Pool);
            sb.Append(" | margin ").Append(F2(RerollCall.Margin)).Append(", recruit value ").Append(F2(_in.RecruitValue));
            Plugin.Logger.LogInfo(sb.ToString());
        }

        // [squad] action hint: Chest 03:09: SHOWN REROLL - best Detective's Pipe 1.98 under the chest floor 2.50 | rerolls 8 (...) | 'the best card here is weak ...'
        static void LogScreen(ScreenCall c, string when)
        {
            var sb = new System.Text.StringBuilder("[squad] action hint: ");
            sb.Append(_screen).Append(' ').Append(_clock).Append(": ");
            if (when != null) sb.Append("(").Append(when).Append(") ");
            sb.Append(c.Show ? "SHOWN " + ScreenCall.Name(c.Action) + " - " : "not shown - ").Append(c.Why);
            if (c.Show && c.Action != HintAction.Reroll) sb.Append(" | rerolls ").Append(_gin.RerollsText);
            sb.Append(" | skip ").Append(_gin.CanSkip ? "+" + _gin.SkipCash + " cash, +" + _gin.SkipHeal + " health (worth " + F2(c.SkipValue) + ")" : "not offered");
            sb.Append(" | actions ").Append(Actions);
            if (c.Show) sb.Append(" | '").Append(c.Words).Append('\'');
            Plugin.Logger.LogInfo(sb.ToString());
        }

        static string Head(HintAction a) { return "<b><color=" + Theme.GoldHex + ">" + ScreenCall.Name(a) + "</color></b><color=" + Theme.DimHex + ">  -  </color>"; }

        // REROLL - Tank would fit this squad better; a second survivor that clears the margin too: "Tank or SWAT", and "(+1 more)"
        // when still more do; for a class the active quest needs (0.13.0, C1): REROLL - the quest needs Huntress.
        // 0.14.0 (A1): up to 0.13.0 the line ended "would rate higher (5.9 vs 4.6)" - the Companion's own scores, which counted
        // nothing a player can see; they stay in the '[squad] reroll hint:' log line
        static string LineText(RerollCall c)
        {
            var who = c.Better.Take(2).Select(r => Wording.Safe(Names.Class((CT)r.Class))).ToList();
            if (who.Count == 0) who.Add(Wording.Safe(Names.Class((CT)c.Best.Class)));
            string head = Head(HintAction.Reroll);
            if (c.ForQuest) return head + "the quest needs " + (who.Count > 1 ? who[0] + " or " + who[1] : who[0]);
            return head + Wording.Reroll(who, Math.Max(0, c.Better.Count - who.Count));
        }

        // ---------------------------------------------------------------- drawing
        static void Hide()
        {
            _placeAt = -1f; _text = null;
            bool was = _shown; _shown = false;
            bool had = !LineRect.Empty; LineRect = new R4();
            if (had) WhyUi.Relayout();
            if (!was || _sel == null) { _button = null; return; }
            Fx.Cancel("reroll:");
            try
            {
                var b = _button; var f = b == null ? null : b.transform.Find(FrameName); if (f != null) f.gameObject.SetActive(false);
                var l = _sel.transform.Find(LineName); if (l != null) l.gameObject.SetActive(false);
            }
            catch { }
            _button = null;
        }

        static void Place()
        {
            if (_text == null || _sel == null) return;
            try
            {
                var sel = _sel;
                if (!sel.gameObject.activeInHierarchy) return;
                var button = _button;
                var brt = button == null ? null : button.transform.TryCast<RectTransform>();
                var view = sel.transform.TryCast<RectTransform>();
                if (brt == null || view == null) { Plugin.Logger.LogInfo(Tag + "not drawn - the screen has no " + ScreenCall.Name(_call != null ? _call.Action : HintAction.Reroll).ToLowerInvariant() + " button"); return; }
                bool first = !_shown;
                float s = Scale(view);

                // the gold frame on the button (the recommended card's, a size smaller)
                var frame = Frame(brt);

                // where the line fits, measured in the view's space: between the lowest card line and the button's top (the band
                // manager's rule, ScreenBand.Hint)
                var vr = view.rect;
                R4 b = SelectBands.Rect(brt, view, _corners);
                float lowest = SelectBands.CardsBottom(_cards, view, _corners, IntPtr.Zero);
                float floor = b.Y0;
                try
                {   // a FREE label standing over the button is kept clear too
                    var act = _call != null ? _call.Action : HintAction.Reroll;
                    var fl = act == HintAction.Banish ? sel.banishFreeLabel : act == HintAction.Reroll ? sel.rerollFreeLabel : null;      // the Skip button has none
                    var frt = fl == null || !fl.activeInHierarchy ? null : fl.transform.TryCast<RectTransform>();
                    if (frt != null) { var fr = SelectBands.Rect(frt, view, _corners); if (fr.Y1 <= b.Y0 + 4f) floor = Mathf.Min(floor, fr.Y0); }
                }
                catch { }
                float canvasBottom = SelectBands.CanvasBottom(view, _corners);
                float h = LineH * s;
                var spot = ScreenBand.Hint(lowest, b, floor, canvasBottom, h);

                var line = view.Find(LineName); var lrt = line == null ? null : line.TryCast<RectTransform>();
                if (spot.Where == null)
                {
                    if (lrt != null) lrt.gameObject.SetActive(false);
                    if (!LineRect.Empty) { LineRect = new R4(); WhyUi.Relayout(); }
                    if (first) Plugin.Logger.LogInfo(Tag + "drawn as the frame alone - no room for the line (over the button " + spot.Band.ToString("0") + " units, under it " + spot.Below.ToString("0") + ", the line needs " + spot.Need.ToString("0") + ")");
                    _shown = true;
                    if (first) Entrance(frame, null, null, true);
                    return;
                }
                string where = spot.Where == "over" ? "over the " + ButtonWord() + " button" : "under the " + ButtonWord() + " button (the band over it is " + spot.Band.ToString("0") + " units)";
                if (lrt == null) lrt = BuildLine(view, brt);
                if (lrt == null) { Plugin.Logger.LogInfo(Tag + "not drawn - no label to clone for the line"); return; }
                var textT = lrt.Find("Text"); var text = textT == null ? null : textT.GetComponent<TextMeshProUGUI>();
                float k = spot.K, sk = s * k, tip = Tip * sk, pad = Pad * sk;
                text.fontSize = Font * sk;
                text.text = _text;
                float tw = 0f; try { tw = text.preferredWidth; } catch { }
                if (!(tw > 0f)) tw = 22f * sk * _text.Length * 0.5f;
                float w = Mathf.Min(MaxW * s, tw + tip + 2 * pad + 4f), want = w;
                // 0.14.0 (R1): the side the line grows to, and never into the team panel or the skip reward
                float x0 = b.X0;
                try { x0 = ScreenBand.HintX(b, want, vr.width / 2f, spot.Top, spot.Top + h * k, SelectBands.Panels(sel, view, _corners), out w); } catch { w = want; }
                if (w < tip + 2 * pad + 200f * sk)
                {
                    lrt.gameObject.SetActive(false);
                    if (!LineRect.Empty) { LineRect = new R4(); WhyUi.Relayout(); }
                    if (first) Plugin.Logger.LogInfo(Tag + "drawn as the frame alone - the line would run into the team panel (" + want.ToString("0") + " units wanted, " + w.ToString("0") + " clear)");
                    _shown = true;
                    if (first) Entrance(frame, null, null, true);
                    return;
                }
                var bar = lrt.Find("Bar").TryCast<RectTransform>(); Ui.Stretch(bar, tip / 2, 0, tip / 2, 0);
                var rule = lrt.Find("Rule").TryCast<RectTransform>(); rule.sizeDelta = new Vector2(-tip, Mathf.Max(3f, 4f * sk));
                foreach (var n in new[] { "TipL", "TipR" }) { var d = lrt.Find(n).TryCast<RectTransform>(); d.sizeDelta = new Vector2(tip * 0.7071f, tip * 0.7071f); }
                Ui.Stretch(text.rectTransform, tip / 2 + pad, 0, tip / 2 + pad, 0);
                // anchored at the view's centre (pivot bottom-left): the offset from the rect's centre is the point in view space
                float bottomY = spot.Top + h * k;                                       // y down from the view's top
                lrt.anchoredPosition = new Vector2(x0 + vr.xMin - vr.center.x, (vr.yMax - bottomY) - vr.center.y);
                lrt.sizeDelta = new Vector2(w, h * k);
                lrt.gameObject.SetActive(true); lrt.SetAsLastSibling();
                LineRect = new R4(x0, spot.Top, x0 + w, bottomY);
                if (first)
                    Plugin.Logger.LogInfo(Tag + "drawn " + where + (x0 > b.X0 ? ", growing left" : "") + (w < want - 0.5f ? ", cut short by the team panel" : "") + " - " + w.ToString("0") + " x " + (h * k).ToString("0") + " units, font " + (Font * sk).ToString("0.#") + " (x" + sk.ToString("0.00") + "), " + (spot.Band >= 0 ? "band " + spot.Band.ToString("0") + " units between the cards' lowest line and the button" : "the cards reach below the button's top"));
                _shown = true;
                Entrance(frame, lrt, text, first);
                WhyUi.Relayout();
            }
            catch (Exception e) { Plugin.Logger.LogWarning(Tag + "not drawn - " + e.Message); }
        }

        static string ButtonWord() { var a = _call != null ? _call.Action : HintAction.Reroll; return a == HintAction.Skip ? "Skip" : a == HintAction.Banish ? "Banish" : "Reroll"; }

        static RectTransform Frame(RectTransform button)
        {
            var t = button.Find(FrameName); var f = t == null ? null : t.TryCast<RectTransform>();
            if (f == null) { f = Ui.Frame(button, FrameName, 2f, 4f, Theme.Gold, 24f); OutOfLayout(f); }
            f.gameObject.SetActive(true); f.SetAsLastSibling();
            return f;
        }

        static RectTransform BuildLine(RectTransform view, RectTransform button)
        {
            var template = Template(button);
            if (template == null) return null;
            var r = Ui.NewRect(LineName, view);
            OutOfLayout(r);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0f, 0f);
            Ui.Image(r, "Bar", Theme.Plate);
            var rule = Ui.Image(r, "Rule", Theme.Gold); rule.anchorMin = new Vector2(0, 0); rule.anchorMax = new Vector2(1, 0); rule.pivot = new Vector2(0.5f, 0); rule.anchoredPosition = Vector2.zero;
            Ui.Diamond(r, "TipL", 0, 0.5f, Tip, Theme.Gold);
            Ui.Diamond(r, "TipR", 1, 0.5f, Tip, Theme.Gold);
            var text = Ui.CloneText(template, r, "Text");
            if (text == null) { UnityEngine.Object.Destroy(r.gameObject); return null; }
            try { text.overflowMode = TextOverflowModes.Ellipsis; } catch { }
            text.fontStyle = FontStyles.Normal; text.alignment = TextAlignmentOptions.Center; text.characterSpacing = 0f; text.color = Theme.Cream;
            return r;
        }

        // the label to clone: the button's own caption (the action row's font), else a card's reason line
        static TextMeshProUGUI Template(RectTransform button)
        {
            try
            {
                var all = button.GetComponentsInChildren(Il2CppType.Of<TextMeshProUGUI>(), false);
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i].TryCast<TextMeshProUGUI>(); if (t == null) continue;
                    try
                    {
                        if (t.font == null || !t.gameObject.activeInHierarchy || t.name.StartsWith("Yazs", StringComparison.Ordinal)) continue;
                        if (!(t.text ?? "").Any(char.IsLetter)) continue;      // the count in the diamond
                    }
                    catch { continue; }
                    return t;
                }
            }
            catch { }
            if (_cards != null)
                foreach (var c in _cards)
                {
                    try { var t = c.Button == null ? null : c.Button.transform.Find("YazsReason"); var tmp = t == null ? null : t.GetComponent<TextMeshProUGUI>(); if (tmp != null) return tmp; } catch { }
                }
            return Ui.FindLabel(button.parent, null);
        }

        // a layout group on the button or its row must leave our pieces where we put them
        static void OutOfLayout(RectTransform rt)
        {
            try { var le = rt.gameObject.AddComponent(Il2CppType.Of<LayoutElement>()).TryCast<LayoutElement>(); if (le != null) le.ignoreLayout = true; } catch { }
        }

        // the card badges' rule: the text at least MinTextPx tall, at most x1.3 (BadgeScale in the config overrides it)
        static float Scale(RectTransform view)
        {
            float fixedScale = 0; try { fixedScale = Plugin.BadgeScale.Value; } catch { }
            if (fixedScale > 0) return Mathf.Clamp(fixedScale, 0.5f, MaxScale);
            try
            {
                var canvas = view.GetComponentInParent<Canvas>();
                var crt = canvas == null ? null : canvas.rootCanvas.transform.TryCast<RectTransform>();
                float h = crt == null ? 0 : crt.rect.height; if (h <= 0) return 1f;
                return Mathf.Clamp(MinTextPx / (Font * UnityEngine.Screen.height / h), 1f, MaxScale);
            }
            catch { return 1f; }
        }

        // on appear: the frame settles onto the button, the line unfolds from its left tip, types on, and the tip pings once;
        // a new verdict on a shown hint (after a reroll) only types its words on again
        static void Entrance(RectTransform frame, RectTransform line, TextMeshProUGUI text, bool full)
        {
            try
            {
                if (full)
                {
                    Fx.Cancel("reroll:");
                    if (frame != null)
                    {
                        CanvasGroup g = null;
                        try { g = frame.GetComponent<CanvasGroup>(); if (g == null) { g = frame.gameObject.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>(); g.blocksRaycasts = false; g.interactable = false; } } catch { }
                        Fx.Run("reroll:frame", 0.05f, 0.45f, k => { float sc = 1f + 0.08f * (1f - Fx.OutCubic(k)); frame.localScale = new Vector3(sc, sc, 1f); if (g != null) g.alpha = Fx.Smooth(k * 1.6f); });
                    }
                    if (line != null)
                        Fx.Run("reroll:line", 0.15f, 0.4f, k => { line.localScale = new Vector3(Mathf.Max(0.0001f, Fx.OutBack(k)), 1f, 1f); }, () =>
                        {
                            var tip = line.Find("TipL"); var rt = tip == null ? null : tip.TryCast<RectTransform>();
                            if (rt != null) Fx.Ping("reroll:ping", rt, Tip * 1.45f, 4f, Theme.Gold, false, 0f, 0.6f, 2.6f, false);
                        });
                }
                else if (line != null) line.localScale = Vector3.one;
                if (text != null) Fx.Type("reroll:type", text, full ? 0.3f : 0.05f, 70f);
            }
            catch { }
        }

        // ---------------------------------------------------------------- the game's banishes, logged (0.14.0)
        // The Banish button (ClickBanish) arms the banish; the next card clicked is banished (ProcessBanish, per screen) and the
        // screen fills that slot anew. 66,000 log lines held no 'replaced (banish)': unused or not seen - these lines settle it.
        static string _banishing; static int _banishFrame = -9; static IntPtr _banishPtr;

        /// <summary>The Banish button was pressed (banish armed) - logged once per press.</summary>
        public static void BanishArmed(UIGameplayUpgradeSelection sel)
        {
            try
            {
                if (Time.frameCount == _banishFrame && Ptr(sel) == _banishPtr) return;      // the base and an override in one press
                _banishFrame = Time.frameCount; _banishPtr = Ptr(sel);
                int left = -1; try { var gm = GameplayMaster.s_instance; var ts = gm == null ? null : gm.teamStatistics; if (ts != null) left = Math.Max(0, (int)ts.GetStatisticValue(PlayerStatistic.EType.TeamNumBanishes)); } catch { }
                string screen = "?"; try { screen = sel.GetIl2CppType().Name.Replace("UIGameplay", ""); } catch { }
                Plugin.Logger.LogInfo("[pick] " + screen + " " + _clock + ": the Banish button pressed" + (left >= 0 ? " (" + left + " banishes left)" : ""));
            }
            catch { }
        }

        /// <summary>A card is about to be banished (the prefix: the card is still on its button).</summary>
        public static void Banishing(UIPowerupButtonBase b)
        {
            try
            {
                var p = Ptr(b); if (p == IntPtr.Zero) return;
                Card c = null; if (_cards != null) foreach (var x in _cards) if (x.Button != null && Ptr(x.Button) == p) { c = x; break; }
                string name = c != null ? c.Name : CardName(b);
                string key = p.ToInt64().ToString(CultureInfo.InvariantCulture) + ":" + Time.frameCount;
                if (_banishing == key) return;              // the base and an override in one banish
                _banishing = key;
                string screen = _sel != null ? _screen.ToString() : "?";
                // the game's own banish of the card (ProcessBanish); the [offer] line that follows says "replaced (banish)" when it took
                Plugin.Logger.LogInfo("[pick] " + screen + " " + _clock + ": banish on " + name + (c != null ? " (#" + c.Rank + ", " + F2(c.Score) + (c.Rank == 1 ? ", the pick" : "") + ")" : ""));
            }
            catch { }
        }

        static string CardName(UIPowerupButtonBase b)
        {
            try { var p = b.attachedPowerup; if (p != null) return G.Name(p); } catch { }
            try { var it = b.attachedItem; if (it != null) return G.Name(it); } catch { }
            return "a card";
        }
    }

    // the game hands each screen its reroll / banish counts (its action buttons are refreshed through these overrides): the
    // count the hint trusts first. The pointer test in OnButtons is all a call costs while no screen is advised. Not the base
    // UIGameplayUpgradeSelection.SetActionButtonsInteractivity: its body is an empty function that 4,075 methods share
    // (identical-code folding, research\review_1005\impl\c4\rva_share_out.txt) - a detour there would fire for all of them.
    [HarmonyPatch(typeof(UIGameplayCharacterRescue), nameof(UIGameplayCharacterRescue.SetActionButtonsInteractivity))]
    static class P_RescueActions { static void Postfix(UIGameplayCharacterRescue __instance, int numRerolls, int numBanishes) { RerollHint.OnButtons(__instance, numRerolls, numBanishes); } }
    [HarmonyPatch(typeof(UIGameplayLevelUp), nameof(UIGameplayLevelUp.SetActionButtonsInteractivity))]
    static class P_LevelUpActions { static void Postfix(UIGameplayLevelUp __instance, int numRerolls, int numBanishes) { RerollHint.OnButtons(__instance, numRerolls, numBanishes); } }
    [HarmonyPatch(typeof(UIGameplayChestOpened), nameof(UIGameplayChestOpened.SetActionButtonsInteractivity))]
    static class P_ChestActions { static void Postfix(UIGameplayChestOpened __instance, int numRerolls, int numBanishes) { RerollHint.OnButtons(__instance, numRerolls, numBanishes); } }
    [HarmonyPatch(typeof(UIGameplayMilitaryTraining), nameof(UIGameplayMilitaryTraining.SetActionButtonsInteractivity))]
    static class P_MilitaryActions { static void Postfix(UIGameplayMilitaryTraining __instance, int numRerolls, int numBanishes) { RerollHint.OnButtons(__instance, numRerolls, numBanishes); } }
    [HarmonyPatch(typeof(UIGameplayHashtagEvent), nameof(UIGameplayHashtagEvent.SetActionButtonsInteractivity))]
    static class P_PodActions { static void Postfix(UIGameplayHashtagEvent __instance, int numRerolls, int numBanishes) { RerollHint.OnButtons(__instance, numRerolls, numBanishes); } }

    // the banishes: the Banish button (the base's ClickBanish and the level-up screen's own), and the card banished (the base's
    // ProcessBanish and each screen's own - every one a function of its own; NOT the Research Pod screen's, an empty body
    // shared by 4,075 methods: Research Pods cannot be banished). Reads only; the base and an override in one press log once.
    [HarmonyPatch]
    static class P_BanishArmed
    {
        static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach (var t in new[] { typeof(UIGameplayUpgradeSelection), typeof(UIGameplayLevelUp) })
            {
                var m = AccessTools.DeclaredMethod(t, nameof(UIGameplayUpgradeSelection.ClickBanish), Type.EmptyTypes);
                if (m != null) yield return m;
            }
        }
        static void Postfix(UIGameplayUpgradeSelection __instance) { RerollHint.BanishArmed(__instance); }
    }

    [HarmonyPatch]
    static class P_Banished
    {
        static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach (var t in new[] { typeof(UIGameplayUpgradeSelection), typeof(UIGameplayLevelUp), typeof(UIGameplayChestOpened), typeof(UIGameplayMilitaryTraining), typeof(UIGameplayCharacterRescue) })
            {
                var m = AccessTools.DeclaredMethod(t, nameof(UIGameplayUpgradeSelection.ProcessBanish), new[] { typeof(UIPowerupButtonBase) });
                if (m != null) yield return m;
            }
        }
        static void Prefix(UIPowerupButtonBase __0) { RerollHint.Banishing(__0); }
    }
}
