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
// 0.16.0: the items the squad holds feed the skip's worth (C16-01b / d / e / f: Reserve Bench's level-up, Hijacked Signal's cash on a pick,
// Life Savings' cash that heals, A Cookie's heal, Skip Rope's points - HeldRules, logged after '(worth X)'), and the revive guard (C16-13)
// reads the survive quest and the second life on each card: no REROLL or SKIP sends a revive away while the run must be survived or the
// squad is under 60 % health, no BANISH ever names one.
// 0.16.0 (C16-15, DK-C04 of the 10-07 Deck round): a line drawn UNDER its button (1280 x 800: the band over it is 63 units) stands in the
// WHY band's row and cut it (one rescue card had no WHY at all) - there it says its short words (ScreenCall.Short: 'REROLL  -  a weak
// offer', 'REROLL  -  Tank fits better'), about half as wide; over its button (the PC) the long words stay. The frame is unchanged.
// 0.16.1 (C-3 + DK-C07, the user's B2-Q3 of 10-10 "move it to the button"): ONE gold frame per screen (ScreenCall.GoldFrame). Up to 0.16.0
// a REROLL / SKIP hint framed its button in gold 0.6 s after the cards came, while the best card kept its gold frame and RECOMMENDED
// ribbon - two gold frames saying opposite things, the card's first (the Deck followed 1 hint of 4; live 10-10 11:57:59 the framed card
// was taken 8 s after a REROLL hint). Now the frame goes on the button IN THE FRAME THE CARDS ARE BADGED (Apply -> FrameNow), and the
// best card gives up its frame (Badge.Yield: its ribbon grey, reading 'IF YOU KEEP' over its own line, grey too); the line over the button
// carries the ribbon's word - 'RECOMMENDED: REROLL  -  the best card here is weak for this squad' (ScreenCall.HeadWord) - and under it
// (1280 x 800) the short words keep their 0.16.0 head ('REROLL  -  a weak offer'), so the WHY band's row has the room C16-15 gave it.
// The line is placed on the next frame (the buttons laid out) and unfolds there - only the line moves in -, and is measured again once
// the screen has flown in (SettleAfter, the old 0.6 s: '... the line moved as the screen settled' when that changed it). BANISH keeps
// the card's frame (the best card stays the pick); its button's frame is grey. When the verdict goes while the screen stays (a
// re-judge), the card's frame comes back (Badge.Restore). '[badge] <screen> <clock>: REROLL advised - the gold frame on the Reroll
// button; #1 <card> gave it up: 'IF YOU KEEP' over its line '...'' says what moved, once per offer and verdict. For the pause walk ([Debug] PreviewPause) only,
// DebugPreviewDue draws a REROLL and a SKIP hint in turn on its first offer (the walk's level-ups are strong), with a shot each.
// 0.16.3 (C162-01, COMPANION-LOG-05 of the 10-11 PC round): the line is placed ONCE. Placed the frame after the cards, it moved when the
// screen had flown in on 5 of 5 first draws (4 chest hints, 1 rescue hint: band 85 -> 111 units, 0.58 - 0.60 s later); now an offer's
// first line waits for that moment (ScreenBand.LineAt) and is placed where the screen settled - the frame still comes with the cards.
// 0.17.0: the active quest's counted rules feed the verdicts - a 'Rerolled Item Chests' quest (C16-04b) gives an item chest the game has not
// counted yet its own REROLL (the chest view's _rerollOncePerView read at the offer and again before every verdict) and keeps the rerolls it
// still needs on every screen, the rescue screen too (RerollCall's keptFor); a 'Liberate Survivors' quest (C16-04c) silences the rescue
// screen's reroll hint while it wants the signal liberated (RerollCall's liberateFirst).
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
        // 0.16.1 (C-3): SettleAfter - the line is measured again this long after the cards came (the screen has flown in by then; up to
        // 0.16.0 the whole hint waited for it, PlaceDelay) - 0.16.3 (C162-01): an offer's first line is placed then, once
        const float Font = 40f, LineH = 62f, Tip = 36f, Pad = 26f, MaxW = 1800f, Gap = ScreenBand.Gap, SettleAfter = 0.6f;

        sealed class Avail
        {
            public bool Button, Interactable, Free; public int Label = -1, Stat = -1;          // the Reroll button
            public bool Skip, SkipUp; public int Cash, Heal;                                   // the Skip button, what it grants
            public bool Banish, BanishUp, BanishFree; public int BanishStat = -1;               // the Banish button
            public bool RerolledHere;                                                          // 0.17.0 (C16-04b): a chest view the game counted a reroll on
        }
        // 0.17.0: Keep (C16-04b) = the chests a 'Rerolled Item Chests' quest still wants rerolled, LiberateFirst (C16-04c) = a 'Liberate Survivors'
        // quest wants this signal liberated
        sealed class Inputs { public List<Recruit> Offered, Possible; public double Liberate = -1, RecruitValue; public Avail Rr; public string Pool = ""; public QuestSos Quest; public int Keep; public bool LiberateFirst; }

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
        static string _short;                            // 0.16.0 (C16-15): its short form - drawn when the line goes under its button
        static Button _button;                           // the button the hint is about (its frame)
        static float _placeAt = -1f;
        static bool _shown;                              // the line placed (or the frame alone decided) on this offer
        // 0.16.1 (C-3): the action on screen (the walk's preview's while it runs), its button's frame drawn, the best card that gave up its
        // frame (Badge.Yield; null: none), when the cards came, the frame the line may be placed from, the line as drawn
        static HintAction _action;
        static bool _framed, _lineOn;
        static Card _keptCard;
        static float _offerAt = -1f;
        static int _placeFrame = -1;
        static string _drawn;
        static bool Badges { get { try { return Plugin.ShowBadges.Value; } catch { return true; } } }
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
                // 0.16.1 (C-3): Advisor has just badged these cards afresh (Badge.Show: the frame on the new best card) - nothing is given up yet
                _keptCard = null; _offerAt = Time.realtimeSinceStartup;
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
                    // 0.16.0 (C16-01b): with a recruit's own level-ups (Reserve Bench) a late recruit still beats Liberate - the verdict's
                    // early 'late in the run' gate is lifted
                    double recruitValue = HeldRules.RecruitLevelUps(s.Held) >= 1 ? Math.Max(s.Ctx.RecruitValue, RerollCall.Late) : s.Ctx.RecruitValue;
                    var inp = new Inputs { Offered = new List<Recruit>(), RecruitValue = recruitValue, Rr = av, Quest = s.Quest };
                    foreach (var c in cards)
                    {
                        if (c.Recruit != null) inp.Offered.Add(c.Recruit);
                        else if (c.Kind == "liberate") inp.Liberate = Math.Max(inp.Liberate, c.Score);
                    }
                    inp.Possible = Possible(sel, s, inp.Offered, out inp.Pool);
                    // 0.17.0: the rescue screen spends the team's one pool of rerolls (C16-04b), and a 'Liberate Survivors' quest may want this
                    // signal liberated (C16-04c; the team rule decides while it is active)
                    try
                    {
                        var st = s.Story;
                        inp.Keep = st == null ? 0 : st.RerollsKept();
                        inp.LiberateFirst = st != null && st.Sos != null && st.Sos.Force && st.Moves && (s.Quest == null || !s.Quest.Active);
                    }
                    catch { }
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
            // 0.16.0 (C16-01): what the items the squad holds add to the skip (HeldRules; nothing without them)
            var held = s.Held;
            x.SkipLevelUps = HeldRules.SkipLevelUps(held, x.Screen);                 // C16-01b Reserve Bench
            x.CashHeals = HeldRules.CashHeals(held);                                  // C16-01e Life Savings
            x.CashAlsoOnPick = HeldRules.CashAlsoOnPick(held, x.Screen);             // C16-01d Hijacked Signal
            x.ExtraHeal = HeldRules.SkipExtraHeal(held, x.Screen, x.SkipHeal);       // C16-01f A Cookie (0.16.4: half the max HP, from the skip's heal)
            x.Economy = s.Ctx.Economy;
            x.SkipBonus = HeldRules.SkipRopeWorth(held, x.Screen, s.Ctx.Economy);    // C16-01f Skip Rope
            x.SkipBy = HeldRules.SkipBy(held, x.Screen);
            x.RopePoints = HeldRules.SkipRopePoints(held, x.Screen); x.RopeReq = HeldRules.TrainingReq(held);       // C16-01f the log's words
            x.SlotBy = HeldRules.SlotBy(held);                                        // C16-01c Wooden Stick / Empty Chest
            // 0.16.0 (C16-13): the revive guard - the quest asks to survive the run (C16-12), a second life on a card
            x.SurviveQuest = s.Ctx.SurviveQuest;
            // 0.17.0 (C16-04b): a quest that counts rerolled item chests - the chest's own count and whether the game counted this view already, and
            // on every screen the rerolls the quest still needs (the chest included: once counted, its floor REROLL keeps them too)
            x.RerolledHere = av.RerolledHere;
            try { var st = s.Story; if (st != null) { x.KeepRerolls = st.RerollsKept(); if (screen == Screen.Chest) x.QuestReroll = st.RerollAsk(); } } catch { }
            foreach (var c in cards)
            {
                var k = new HintCard { Name = c.Name, Kind = c.Kind, Score = c.Score, Rank = c.Rank, Head = c.Reason, Class = c.Owner != null ? c.Owner.Name : null };
                var w = c.Say;
                if (w != null) { k.Base = w.Base; k.Build = w.Build; k.Skipped = w.Kind == SayKind.Ability && w.HeadRank >= 6; }
                if (w != null && w.Item != null) k.HeldCost = w.Item.HeldCost;      // 0.16.0 (C16-01c): the slot's cost - the merit is Score + HeldCost
                // a revive item (the book's row, with [Debug] ItemKeywords on too), or Resuscitation not yet owned (CardWords.Level = the level
                // before the pick)
                k.Revive = c.Item != null ? ItemBook.Revive(c.Name)
                    : c.Powerup != null && w != null && w.Kind == SayKind.Ability && w.Level == 0 && ItemBook.ReviveAbilities.Contains(c.Name, StringComparer.OrdinalIgnoreCase);
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

        /// <summary>0.17.0 (C16-06a): from Lockdown's P_Locked once the player locked, replaced or cleared a lock on the screen being advised (the cards
        /// are marked again by then). Nothing yet: the LOCKDOWN hint that answers here is C16-06b.</summary>
        internal static void OnLockChanged(UIGameplayUpgradeSelection sel) { }

        /// <summary>0.17.0 (C16-06a), for the pause walk's lock proof only: does the screen still have a reroll (RerollCount.Can over this file's reading:
        /// the verdict's count on the screen being advised, else the team's statistic, else the button's number; a FREE reroll counts)?
        /// <paramref name="why"/> = the count and where it came from.</summary>
        internal static bool DebugRerollLeft(UIGameplayUpgradeSelection sel, out string why)
        {
            var a = Read(sel);
            bool advised = Ptr(sel) == _selPtr && _count.Judged >= 0;
            int n = advised ? _count.Judged : a.Stat >= 0 ? a.Stat : a.Label;
            why = Count(n) + " (" + (advised ? _count.From : a.Stat >= 0 ? RerollCount.FromStat : a.Label >= 0 ? RerollCount.FromLabel : RerollCount.FromNone) + (a.Free ? ", a FREE reroll" : "") + ")";
            return RerollCount.Can(a.Button, a.Interactable, a.Free, n);
        }

        /// <summary>From Advisor: a selection screen closed (the pick, a skip) - the hint goes with it. 0.16.1 (C-3): the best card is left as it
        /// is (its screen animates out; the next offer badges every card afresh).</summary>
        public static void Close() { Hide(false); _sel = null; _selPtr = IntPtr.Zero; _cards = null; _in = null; _gin = null; _av = null; _call = null; _rescue = null; _count.Reset(); _rejudge = false; _preview = null; }

        /// <summary>The HUD went away with the scene: nothing of ours is left to hide (the unlock cards are looked up again next run).</summary>
        public static void Forget()
        {
            _placeAt = -1f; _text = null; _short = null; _drawn = null; _shown = false; _framed = false; _lineOn = false; _button = null; _keptCard = null; _preview = null;
            LineRect = new R4(); _sel = null; _selPtr = IntPtr.Zero; _cards = null; _in = null; _gin = null; _av = null; _call = null; _rescue = null; _count.Reset(); _rejudge = false; _hookPtr = IntPtr.Zero; _unlockCards = null; _banishing = null;
        }

        /// <summary>From the GameMaster.Update post-fix (it ticks while the screen holds the game): a bool and a float compare a frame.
        /// 0.16.1 (C-3): the line is placed on a frame after the hint was decided (never the same frame: the buttons are laid out by then);
        /// 0.16.3 (C162-01): an offer's first line once the screen has flown in (ScreenBand.LineAt), placed once. The clock is read only while
        /// the line is due (review 10-10: no engine call on the frames nothing waits).</summary>
        public static void Tick()
        {
            if (_rejudge) Rejudge();
            if (_placeAt < 0f) return;
            if (Time.realtimeSinceStartup >= _placeAt && Time.frameCount > _placeFrame) { _placeAt = -1f; Place(); }
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
            try { a.RerolledHere = RerolledOnce(sel); } catch { }           // 0.17.0 (C16-04b)
            return a;
        }
        // 0.17.0 (C16-04b): the chest view's own flag - set by its ClickReroll as the quest's event is raised (once per view), cleared by OnEnable
        [MethodImpl(MethodImplOptions.NoInlining)] static bool RerolledOnce(UIGameplayUpgradeSelection sel) { var ch = sel.TryCast<UIGameplayChestOpened>(); return ch != null && ch._rerollOncePerView; }
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
        internal static PowerupBase UnlockCard(int cls)        // 0.16.0 (C16-11a): internal - the pause walk's recruit (Menu.Walk016.cs) asks it too
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
                // 0.17.0: after the team rule, a 'Liberate Survivors' quest's Liberate first (C16-04c), then the rerolls a 'Rerolled Item Chests' quest
                // still needs (C16-04b: while no more are left than the chests to reroll and the reroll is not free)
                string kept = _in.Keep > 0 && !_in.Rr.Free && _count.Judged >= 0 && _count.Judged <= _in.Keep
                    ? "rerolls kept for Rerolled Item Chests (" + _in.Keep + " chests to reroll, " + _count.Judged + " left)" : null;
                var r = RerollCall.Decide(_in.Offered, _in.Liberate, _in.Possible, can, rr, _in.RecruitValue, _in.Quest,     // 0.13.0 (C1): the quest's team rule first
                    keptFor: kept, liberateFirst: _in.LiberateFirst);
                _rescue = r;
                call = ScreenCall.FromRescue(r, r.Show ? LineText(r, false) : null, r.Show ? LineText(r, true) : null);     // C16-15: both forms
            }
            else
            {
                _gin.CanReroll = CanReroll(_av, out rr); _gin.Rerolls = _count.Judged; _gin.FreeReroll = _av.Free; _gin.RerollsText = rr;
                _gin.CanBanish = _av.Banish && _av.BanishUp && (_gin.Banishes > 0 || _av.BanishFree);
                // 0.17.0 (C16-04b): a re-judge after a reroll runs on the old cards with the first verdict's inputs - the chest's flag read again, so
                // the quest's REROLL does not come back for the moment before the new cards land
                if (_gin.Screen == "Chest" && _sel != null) { try { _gin.RerolledHere = RerolledOnce(_sel); } catch { } }
                call = ScreenCall.Decide(_gin);
            }
            bool flipped = _call == null || _call.Show != call.Show || _call.Action != call.Action;
            _call = call;
            if (always || flipped) { if (_in != null) Log(_rescue, rr, when); else LogScreen(call, when); }
            if (_preview != null) return;                    // 0.16.1: the walk's preview holds the screen - the verdict is drawn when it ends (_call)
            Apply(call);
        }

        // 0.16.1 (C-3): a verdict onto the screen. Shown: the frame on its button at once (FrameNow: gold, and the best card gives up its own,
        // for REROLL / SKIP; grey for BANISH), the line from the next frame (Place) - 0.16.3 (C162-01): an offer's first line once the screen
        // has flown in, SettleAfter after the cards. Not shown: the hint goes and the best card's frame comes back.
        static void Apply(ScreenCall call)
        {
            if (call == null || !call.Show) { Hide(); return; }
            string text = LineOf(call, false);
            // 0.16.0 (C16-15): the short words too - Place draws them when the line goes under its button
            string brief = LineOf(call, true);
            Button b = ButtonOf(call.Action);
            bool same = b != null && _button != null && b.Pointer == _button.Pointer;
            if ((_shown || _framed) && !same) Hide();                                   // another action: its own button (the card's frame back first - one frame, nothing drawn between)
            _action = call.Action; _button = b;
            FrameNow();
            if (_shown && same && text == _text && brief == _short) return;            // the same words on screen already (a count that moved, still > 0)
            _text = text; _short = brief;
            // 0.16.3 (C162-01): placed once - a first line where the screen settles (a verdict that comes later - a re-judge, a reroll's new
            // cards - finds the screen in place: the next frame); up to 0.16.2 it was placed at once and measured again at SettleAfter
            _placeAt = ScreenBand.LineAt(Time.realtimeSinceStartup, _offerAt, _shown, SettleAfter); _placeFrame = Time.frameCount;
        }

        // the line's words: the rescue screen's verdict carries its own head (LineText); every other screen's words get theirs here -
        // 'RECOMMENDED: REROLL' over the button, the action alone in the short form under it (0.16.1, ScreenCall.HeadWord)
        static string LineOf(ScreenCall call, bool brief)
        {
            if (call.Rescue != null) return brief ? call.Short ?? call.Words : call.Words;
            return Head(call.Action, brief) + Wording.Safe(Names.Text(brief ? call.Short ?? call.Words : call.Words));
        }

        // 0.16.1 (C-3): the frame on the hint's button with the cards - gold when the hint takes the screen's one gold frame (ScreenCall.GoldFrame:
        // REROLL / SKIP, the button shown), and then the best card gives up its own (Keep); grey for BANISH (the best card stays the pick). A
        // button not shown (yet): nothing - the card keeps its frame; Place asks again once the line is placed.
        static void FrameNow()
        {
            RectTransform brt = null; bool up = false;
            try { brt = _button == null ? null : _button.transform.TryCast<RectTransform>(); up = brt != null && _button.gameObject.activeInHierarchy; } catch { up = false; }
            if (!up) return;
            bool gold = ScreenCall.GoldFrame(Badges, true, _action, true) == FrameOn.Button;
            var frame = Frame(brt);                                                    // the card frame's, a size smaller
            Tint(frame, gold ? Theme.Gold : Theme.Grey);
            if (!_framed) { _framed = true; EnterFrame(frame); }
            if (gold && Badges) Keep();
        }

        // the best card gives up its frame and ribbon (Badge.Yield) - once per offer and verdict, said in one line
        static void Keep()
        {
            if (_keptCard != null || _cards == null) return;
            Card best = null;
            foreach (var c in _cards) if (c != null && c.Rank == 1) { best = c; break; }
            if (best == null) return;
            string words = Badge.Yield(best);
            if (words == null) return;
            _keptCard = best;
            Plugin.Logger.LogInfo("[badge] " + _screen + " " + _clock + ": " + ScreenCall.Name(_action) + " advised - the gold frame on the " + ButtonWord() + " button; #1 " + best.Name
                + " gave it up: '" + ScreenCall.KeepLead + "' over its line '" + words + "'" + (_preview != null ? " (the walk's preview)" : ""));
        }

        // the verdict that took the frame went while the screen stays: the best card wears its frame again
        static void RestoreCard()
        {
            var c = _keptCard; _keptCard = null;
            if (c == null) return;
            if (Badge.Restore(c)) Plugin.Logger.LogInfo("[badge] " + _screen + " " + _clock + ": the gold frame back on #1 " + c.Name + (_preview != null || _pvStep > 0 ? " (the walk's preview)" : " (the hint went)"));
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
            sb.Append(" | skip ").Append(_gin.CanSkip ? "+" + _gin.SkipCash + " cash, +" + _gin.SkipHeal + " health (worth " + F2(c.SkipValue) + ")" + ScreenCall.SkipNote(_gin) : "not offered");     // 0.16.0 (C16-01): what held items add
            sb.Append(" | actions ").Append(Actions);
            if (c.Show) sb.Append(" | '").Append(c.Words).Append('\'');
            Plugin.Logger.LogInfo(sb.ToString());
        }

        // the line's head: 0.16.1 (C-3) 'RECOMMENDED: REROLL' / 'RECOMMENDED: SKIP' over the button - the ribbon the best card gave up -, the
        // action alone in the short form under it and for BANISH (ScreenCall.HeadWord)
        static string Head(HintAction a, bool brief) { return "<b><color=" + Theme.GoldHex + ">" + ScreenCall.HeadWord(a, brief) + "</color></b><color=" + Theme.DimHex + ">  -  </color>"; }

        // REROLL - Tank would fit this squad better; a second survivor that clears the margin too: "Tank or SWAT", and "(+1 more)"
        // when still more do; for a class the active quest needs (0.13.0, C1): REROLL - the quest needs Huntress.
        // 0.14.0 (A1): up to 0.13.0 the line ended "would rate higher (5.9 vs 4.6)" - the Companion's own scores, which counted
        // nothing a player can see; they stay in the '[squad] reroll hint:' log line.
        // 0.16.0 (C16-15): <paramref name="brief"/> the short form for a line under the button - "REROLL - Tank fits better"
        // (Wording.RerollShort: the first survivor alone); the quest's form keeps its words
        static string LineText(RerollCall c, bool brief)
        {
            var who = c.Better.Take(2).Select(r => Wording.Safe(Names.Class((CT)r.Class))).ToList();
            if (who.Count == 0) who.Add(Wording.Safe(Names.Class((CT)c.Best.Class)));
            string head = Head(HintAction.Reroll, brief);
            if (c.ForQuest) return head + "the quest needs " + (who.Count > 1 ? who[0] + " or " + who[1] : who[0]);
            return head + (brief ? Wording.RerollShort(who) : Wording.Reroll(who, Math.Max(0, c.Better.Count - who.Count)));
        }

        // ---------------------------------------------------------------- drawing
        static void Hide() { Hide(true); }

        // the hint off the screen: its line, its button's frame; 0.16.1 (C-3) with <restore> the best card wears its frame again (the verdict went,
        // the screen stays) - not when the screen closes (Close: it animates out as it is)
        static void Hide(bool restore)
        {
            _placeAt = -1f; _text = null; _short = null; _drawn = null;
            bool was = _shown || _framed; _shown = false; _framed = false; _lineOn = false;
            bool had = !LineRect.Empty; LineRect = new R4();
            if (had) WhyUi.Relayout();
            if (restore) RestoreCard(); else _keptCard = null;
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

        // the line, placed when Apply's time comes: 0.16.1 (C-3) the frame is FrameNow's, drawn with the cards (up to 0.16.0 all of it waited
        // 0.6 s); 0.16.3 (C162-01) an offer's first line is placed once, where the screen settled - the drawn line says how long after the
        // cards ('..., 0.60 s after the cards'); up to 0.16.2 it was measured again then ('... the line moved as the screen settled')
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
                if (brt == null || view == null) { Plugin.Logger.LogInfo(Tag + "not drawn - the screen has no " + ScreenCall.Name(_action).ToLowerInvariant() + " button" + Pv()); return; }
                bool first = !_shown;
                FrameNow();                 // a button that came up after the verdict: its frame now (and the best card gives up its own)
                float s = Scale(view);

                // where the line fits, measured in the view's space: between the lowest card line and the button's top (the band
                // manager's rule, ScreenBand.Hint)
                var vr = view.rect;
                R4 b = SelectBands.Rect(brt, view, _corners);
                float lowest = SelectBands.CardsBottom(_cards, view, _corners, IntPtr.Zero);
                float floor = b.Y0;
                try
                {   // a FREE label standing over the button is kept clear too
                    var fl = _action == HintAction.Banish ? sel.banishFreeLabel : _action == HintAction.Reroll ? sel.rerollFreeLabel : null;      // the Skip button has none
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
                    Alone(lrt, first, "no room for the line (over the button " + spot.Band.ToString("0") + " units, under it " + spot.Below.ToString("0") + ", the line needs " + spot.Need.ToString("0") + ")");
                    return;
                }
                string where = spot.Where == "over" ? "over the " + ButtonWord() + " button" : "under the " + ButtonWord() + " button (the band over it is " + spot.Band.ToString("0") + " units)";
                if (lrt == null) lrt = BuildLine(view, brt);
                if (lrt == null) { Plugin.Logger.LogInfo(Tag + "not drawn - no label to clone for the line"); return; }
                var textT = lrt.Find("Text"); var text = textT == null ? null : textT.GetComponent<TextMeshProUGUI>();
                float k = spot.K, sk = s * k, tip = Tip * sk, pad = Pad * sk;
                text.fontSize = Font * sk;
                // 0.16.0 (C16-15): under the button the line stands in the WHY band's row (1280 x 800) - its short words there, so the band
                // keeps its room; over the button (the PC since 0.12.2) the long words - 0.16.1 (C-3): with the ribbon's 'RECOMMENDED:' head
                string drawn = spot.Where == "under" && _short != null ? _short : _text;
                bool brief = drawn != _text;                                         // the log's ', short form' (BANISH has none)
                if (text.text != drawn) text.text = drawn;
                float tw = 0f; try { tw = text.preferredWidth; } catch { }
                if (!(tw > 0f)) tw = 22f * sk * drawn.Length * 0.5f;
                float w = Mathf.Min(MaxW * s, tw + tip + 2 * pad + 4f), want = w;
                // 0.14.0 (R1): the side the line grows to, and never into the team panel or the skip reward
                float x0 = b.X0;
                try { x0 = ScreenBand.HintX(b, want, vr.width / 2f, spot.Top, spot.Top + h * k, SelectBands.Panels(sel, view, _corners), out w); } catch { w = want; }
                if (w < tip + 2 * pad + 200f * sk)
                {
                    Alone(lrt, first, "the line would run into the team panel (" + want.ToString("0") + " units wanted, " + w.ToString("0") + " clear)");
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
                var rect = new R4(x0, spot.Top, x0 + w, bottomY);
                bool appear = !_lineOn, moved = !appear && !Same(rect, LineRect);
                LineRect = rect;
                string what = where + (x0 > b.X0 ? ", growing left" : "") + (w < want - 0.5f ? ", cut short by the team panel" : "") + (brief ? ", short form" : "") + " - " + w.ToString("0") + " x " + (h * k).ToString("0") + " units, font " + (Font * sk).ToString("0.#") + " (x" + sk.ToString("0.00") + "), " + (spot.Band >= 0 ? "band " + spot.Band.ToString("0") + " units between the cards' lowest line and the button" : "the cards reach below the button's top");
                if (first) Plugin.Logger.LogInfo(Tag + "drawn " + what + After() + Pv());
                _shown = true; _lineOn = true;
                if (appear) EnterLine(lrt, text, true);
                else if (drawn != _drawn) EnterLine(lrt, text, false);            // new words on a shown line (a re-judge): typed on again
                _drawn = drawn;
                if (appear || moved) WhyUi.Relayout();
            }
            catch (Exception e) { Plugin.Logger.LogWarning(Tag + "not drawn - " + e.Message); }
        }

        // the frame alone on the button: no room for the line (or it would run into the team panel)
        static void Alone(RectTransform lrt, bool first, string why)
        {
            if (lrt != null) lrt.gameObject.SetActive(false);
            _lineOn = false; _drawn = null;
            if (!LineRect.Empty) { LineRect = new R4(); WhyUi.Relayout(); }
            if (first) Plugin.Logger.LogInfo(Tag + "drawn as the frame alone - " + why + After() + Pv());
            _shown = true;
        }

        // 0.16.3 (C162-01): how long after the cards came the line was placed (an offer's first line: SettleAfter, 0.60 s)
        static string After() { return ", " + (Time.realtimeSinceStartup - _offerAt).ToString("0.00", CultureInfo.InvariantCulture) + " s after the cards"; }

        static bool Same(R4 a, R4 b) { return Mathf.Abs(a.X0 - b.X0) <= 1f && Mathf.Abs(a.Y0 - b.Y0) <= 1f && Mathf.Abs(a.X1 - b.X1) <= 1f && Mathf.Abs(a.Y1 - b.Y1) <= 1f; }
        static string Pv() { return _preview != null ? " (the walk's preview)" : ""; }

        static string ButtonWord() { var a = _action; return a == HintAction.Skip ? "Skip" : a == HintAction.Banish ? "Banish" : "Reroll"; }

        // the frame on the button (the card frame's, a size smaller), made gold; FrameNow tints it - 0.16.1 (C-3): gold when it is the screen's
        // one gold frame, grey for BANISH
        static RectTransform Frame(RectTransform button)
        {
            var t = button.Find(FrameName); var f = t == null ? null : t.TryCast<RectTransform>();
            if (f == null) { f = Ui.Frame(button, FrameName, 2f, 4f, Theme.Gold, 24f); OutOfLayout(f); }
            f.gameObject.SetActive(true); f.SetAsLastSibling();
            return f;
        }

        // the frame's edges and corner diamonds in one colour (one button's frame may serve REROLL, then BANISH - another verdict)
        static void Tint(RectTransform f, Color color)
        {
            try
            {
                for (int i = 0; i < f.childCount; i++)
                {
                    var img = f.GetChild(i).GetComponent<Image>();
                    if (img != null && img.color != color) img.color = color;
                }
            }
            catch { }
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

        // the card text's rule (CardTextSize, ScreenBand.cs): on Auto the line is enlarged until its text is 16 px, up to x1.3 (x1.6 on
        // a canvas taller than 16:9, like the Deck's); a "Card text size" picked in the menu ([General] BadgeScale, a share of Auto)
        // scales it as much as it scales the cards' reason lines against their Auto; never under 15 px. 0.15.0 (C15-06): up to
        // 0.14.0 a picked size was the line's own s (0.5 - 1.3) with no floor.
        static float Scale(RectTransform view)
        {
            float setting = 0; try { setting = Plugin.BadgeScale.Value; } catch { }
            float screenH = UnityEngine.Screen.height, h = 0f;
            try
            {
                var canvas = view.GetComponentInParent<Canvas>();
                var crt = canvas == null ? null : canvas.rootCanvas.transform.TryCast<RectTransform>();
                h = crt == null ? 0 : crt.rect.height;
            }
            catch { h = 0f; }
            if (!(h > 0)) h = CardTextSize.CanvasUnits(UnityEngine.Screen.width, screenH);
            float factor = CardTextSize.Factor(setting, CardTextSize.ReasonPx1OnCanvas(screenH, h), h);
            return CardTextSize.Follow(Font * screenH / h, h, factor);
        }

        // on appear (0.16.1, C-3: with the cards): the frame settles onto the button as the best card's frame would have (0.12 s in)
        static void EnterFrame(RectTransform frame)
        {
            try
            {
                Fx.Cancel("reroll:frame");
                CanvasGroup g = null;
                try { g = frame.GetComponent<CanvasGroup>(); if (g == null) { g = frame.gameObject.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>(); g.blocksRaycasts = false; g.interactable = false; } } catch { }
                Fx.Run("reroll:frame", 0.12f, 0.45f, k => { float sc = 1f + 0.08f * (1f - Fx.OutCubic(k)); frame.localScale = new Vector3(sc, sc, 1f); if (g != null) g.alpha = Fx.Smooth(k * 1.6f); });
            }
            catch { }
        }

        // the line unfolds from its left tip, types on, and the tip pings once - the only part of the hint that moves in; new words on a
        // shown line (a re-judge, a reroll's new cards) are only typed on again
        static void EnterLine(RectTransform line, TextMeshProUGUI text, bool full)
        {
            try
            {
                if (full)
                {
                    Fx.Cancel("reroll:line"); Fx.Cancel("reroll:ping");
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

        // ---------------------------------------------------------------- the pause walk's preview (0.16.1, C-3)
        // For the pause walk ([Debug] PreviewPause) only - the walk's level-ups are strong, so no real hint would show the new looks: once a
        // session, on its first offer of two cards or more, 2 s in (the 'offer' and 'why' shots taken) and before the hover tour, a REROLL and
        // then a SKIP hint are drawn as a real one draws them - the verdict comes from ScreenCall.Decide on these cards with their scores
        // lowered (under every floor for REROLL; all AVOID, rerolls off, for SKIP), so the words are the real ones - with a 'hint_reroll' /
        // 'hint_skip' shot 1.5 s into each (the frame, the 'RECOMMENDED:' line typed on, the best card's 'IF YOU KEEP', the WHY beside them);
        // then the screen's own verdict again (the frame back on the best card). Logged as '[menu] pause walk: hint preview ...' lines and the
        // drawing lines marked '(the walk's preview)' - never a '[squad] action hint: ... SHOWN' line (advice_audit counts those).
        static ScreenCall _preview;                      // the forced verdict on screen (null: none)
        static int _pvStep = -1;                         // -1 not started this session, 1 REROLL drawn, 2 SKIP drawn, 3 ending, 0 done
        static float _pvAt; static IntPtr _pvSel;
        const float PreviewShot = 1.5f;                  // the line unfolded (0.55 s) and typed on (0.3 s + up to 0.9 s) after it is placed

        /// <summary>From the pause walk, before the hover tour: true while the preview holds the offer (the walk asks again shortly and
        /// takes no pick meanwhile).</summary>
        internal static bool DebugPreviewDue(float now)
        {
            if (_pvStep == 0) return false;
            try
            {
                if (_pvStep > 0 && (_sel == null || _selPtr != _pvSel))
                {
                    Plugin.Logger.LogInfo("[menu] pause walk: hint preview cut short - the offer closed");
                    _pvStep = 0; _preview = null; return false;
                }
                if (_pvStep < 0)
                {
                    if (_sel == null || _cards == null || _cards.Count < 2 || now < _offerAt + 2f) return false;
                    _pvSel = _selPtr;
                    Plugin.Logger.LogInfo("[menu] pause walk: hint preview - a REROLL and a SKIP hint drawn in turn on this " + _screen + " offer as real hints draw them (the cards' own verdict: "
                        + (_call != null && _call.Show ? ScreenCall.Name(_call.Action) : "none") + "), a 'hint_reroll' / 'hint_skip' shot " + PreviewShot.ToString("0.0", CultureInfo.InvariantCulture) + " s into each");
                    if (!Preview(HintAction.Reroll)) { End("no REROLL to draw"); return false; }
                    _pvStep = 1; _pvAt = now + PreviewShot; return true;
                }
                if (now < _pvAt) return true;
                if (_pvStep == 1)
                {
                    Shots.Later(0.05f, "hint_reroll", true);
                    Said("REROLL");
                    if (Preview(HintAction.Skip)) { _pvStep = 2; _pvAt = now + PreviewShot; return true; }
                    Plugin.Logger.LogInfo("[menu] pause walk: hint preview - no SKIP on this screen (its Skip button not shown)");
                    _pvStep = 3; _pvAt = now + 0.3f; return true;
                }
                if (_pvStep == 2) { Shots.Later(0.05f, "hint_skip", true); Said("SKIP"); _pvStep = 3; _pvAt = now + 0.3f; return true; }
                End(null);
                return false;
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[menu] pause walk: hint preview failed: " + e.Message); _pvStep = 0; _preview = null; return false; }
        }

        // a forced verdict of <a> on the cards on screen: ScreenCall.Decide on them with their scores lowered, drawn as Apply draws any verdict
        static bool Preview(HintAction a)
        {
            if (a == HintAction.Skip) { Button sk = ButtonOf(HintAction.Skip); bool up = false; try { up = sk != null && sk.gameObject.activeInHierarchy; } catch { } if (!up) return false; }
            var x = new ScreenCallIn
            {
                Screen = _screen == Screen.SOS ? "LevelUp" : _screen.ToString(), Progress = 0.1, Health = 1, Survival = 1, Cash = 0.3,
                Reroll = a == HintAction.Reroll, Skip = a == HintAction.Skip, Banish = false,
                CanReroll = true, Rerolls = 5, RerollsText = "5 (the walk's preview)", CanSkip = true, SkipCash = 300, SkipHeal = 100,
            };
            foreach (var c in _cards)
                if (c != null) x.Cards.Add(new HintCard { Name = c.Name, Kind = c.Kind, Rank = c.Rank, Head = "", Score = a == HintAction.Reroll ? 1.2 + 0.1 * Math.Max(0, 5 - c.Rank) : 0.5 - 0.01 * c.Rank });     // REROLL: the best 1.6, under every floor (2.0 - 3.5)
            var call = ScreenCall.Decide(x);
            if (!call.Show || call.Action != a) return false;
            _preview = call;
            Apply(call);
            return true;
        }

        // what the preview drew, for the walk's log
        static void Said(string what)
        {
            Plugin.Logger.LogInfo("[menu] pause walk: hint preview " + what + " - " + (_framed ? "the frame on the " + ButtonWord() + " button" : "no frame (the button not shown)") + ", "
                + (_lineOn ? "the line " + (_drawn == _short && _short != _text ? "under it (short form)" : "over it") : "no line (the frame alone)")
                + (_keptCard != null ? ", #1 " + _keptCard.Name + " gave up its frame" : ", the best card kept its frame"));
        }

        // the preview over: the screen's own verdict again, without a log line of its own (it was logged when it was judged)
        static void End(string why)
        {
            _preview = null; _pvStep = 3;                    // still the preview's for RestoreCard's line
            if (_call != null && _call.Show) Apply(_call); else Hide();
            _pvStep = 0;
            Plugin.Logger.LogInfo("[menu] pause walk: hint preview done" + (why != null ? " (" + why + ")" : "") + " - the screen's own verdict again (" + (_call != null && _call.Show ? ScreenCall.Name(_call.Action) : "none")
                + "), the gold frame " + (_framed && ScreenCall.TakesFrame(_action) ? "on the " + ButtonWord() + " button" : "on the best card"));
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
