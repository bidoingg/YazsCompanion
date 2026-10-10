// The action hints on every selection screen (0.14.0, roadmap item 5 of the 10-05 review): the rescue screen's reroll hint
// (RerollCall in Synergy.cs, 0.12.2) generalised. The player follows the #1 card on 96 - 100 % of the offers, so the gains
// are the decisions the Companion was silent on: the user rerolled chests by hand whenever the best card was a B-tier item
// (the logged best cards 1.98 / 2.51 / 2.76 / 2.78, and 3.62 / 3.71), never banished in 66,000 log lines, and never
// skipped. One hint at most per screen, the first that applies of:
//   SKIP    a chest under a quest that wants no item at all (a reroll brings only more items);
//   REROLL  the best card is under the screen's floor (a chest 2.5, a level-up 3.5 in the first half of the run and 2.5
//           after, a military training 2.0, a Research Pod 2.0) and the game still has a reroll for the screen; "(2 rerolls
//           left)" when two or fewer are. The chest floor was 3.0 in the plan: over the user's 147 logged chests it would have
//           spoken on 70 (48 %), 65 of them taken as offered; at 2.5 it speaks on 34 (23 %), 1.98 among them;
//   SKIP    every card would hurt the squad (all AVOID), or every card is under SkipUnder and the screen's skip bonus is
//           worth more: the game's ApplySkipBonus grants cash ((TeamMoneyMultiplier + the enemy scaling - 1) x 300) and heals
//           20 % of the team's max health (machine code of 1.0.2: GetSkipBonusMoney / GetSkipBonusHp,
//           research\review_1005\impl\c4\dis_skip_banish.txt) - the heal counts while the squad is hurting, the cash as the
//           run goal values it (Context.Cash);
//   BANISH  (off by default: a banish is for the whole run) a banishable card the build skips, or one scored under 1, that
//           the active quest does not name - never a card the game marks as not banishable, never the recommended card.
// The rescue screen keeps its own verdict (RerollCall: who could still come) and its words; FromRescue carries it. LOCKDOWN is
// not advised: what a lockdown keeps has not been read in the game's code yet.
// 0.16.0: the items the squad holds change the skip (HeldRules, C16-01b / d / e / f): with Reserve Bench the skip of a chest or a
// training is a level-up - REROLL first, then (no reroll left, or reroll hints off) SKIP when the best card is under the floor, the
// level-up worth a card at the floor; Life Savings turns the skip's cash into a heal, Hijacked Signal pays a chest item the same cash
// (the skip's cash is no gain there), A Cookie heals 500 more, Skip Rope pays points toward a Military Training (they speak once no
// reroll is left, in the weak-cards SKIP). The revive guard (C16-13, DK-C01): while a card holds a second life and the quest asks to
// survive the run or the squad is under 60 % health, no REROLL and no SKIP sends it away ('held back: ...' / '| revive guard: ...' in
// the log; the no-item quest's SKIP keeps its place), and a BANISH never names a second life. With Wooden Stick / Empty Chest held (C16-01c)
// every card that fills an empty slot carries the slot's cost (HintCard.HeldCost): REROLL is judged on the best card's merit (its score
// before the cost - a reroll brings items with the same cost), the all-AVOID SKIP on the net ('an empty slot is worth more than these').
// 0.16.0 (C16-15, DK-C04 of the 10-07 Deck round): every hint's words have a short form (Short: 'a weak offer (2 left)', 'every card hurts',
// 'the heal and cash are worth more', 'Quest: no items'; the rescue screen's 'Tank fits better') - RerollHint draws it when the line goes
// UNDER its button (1280 x 800: the band over the button is 63 units), where it stands in the WHY band's row; over the button (the PC) the
// long words stay. BANISH (off by default) and the quest's rescue form keep their words.
// Pure (no game types): RerollHint.cs reads the screen and draws the line, the offline bench replays the logged chests.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace YazsCompanion
{
    internal enum HintAction { None, Reroll, Skip, Banish }

    /// <summary>One card of the offer as the action hints see it.</summary>
    internal sealed class HintCard
    {
        public string Name = "?", Kind = "", Class, Base;   // the card, its kind (Ranker's), the owner's class, an evolution's base
        public double Score; public int Rank;
        public bool Banishable = true;          // the game's isBanishable, and no "not banishable" plate on the card
        public bool Skipped; public string Build;  // the owner's build skips it (its words: CardWords.HeadRank 6), that build
        public bool Health;                     // an item the game counts as health related (a health-items quest keeps it)
        public string Head = "";                // the ranking's headline (Why[0]): a quest line protects the card
        public double HeldCost;                 // 0.16.0 (C16-01c): what filling an empty item slot costs this card (Wooden Stick / Empty Chest
                                                // held; ItemSay.HeldCost) - Score + HeldCost is the card's merit
        public bool Revive;                     // 0.16.0 (C16-13): a second life - a revive item (ItemBook.Revive) or the Medic's Resuscitation
                                                // not yet owned (ItemBook.ReviveAbilities)
    }

    /// <summary>What the screen holds for the hints.</summary>
    internal sealed class ScreenCallIn
    {
        public string Screen = "";              // LevelUp, Chest, Military, Hashtag (the rescue screen: RerollCall)
        public readonly List<HintCard> Cards = new List<HintCard>();
        public double Progress;                 // the run's progress, 0..1 (RunContext.Progress)
        public bool CanReroll; public int Rerolls = -1; public bool FreeReroll; public string RerollsText = "?";
        public bool CanBanish; public int Banishes = -1;
        public bool CanSkip; public int SkipCash, SkipHeal;   // the skip button is up; what it grants (cash, health points)
        public double Health = 1;               // the squad's health, 0..1
        public double Survival = 1, Cash = 0.3; // RunContext.Survival / Cash: what health and cash weigh now
        public bool Reroll = true, Skip = true, Banish;        // [Advice] ActionHint* (banish off by default)
        public string NoItems;                  // a chest under a quest that wants no item: its words (null: none)
        public QuestRules Quest;                // the active quest's rules: the cards they name are never advised for a banish
        // ---- 0.16.0 (C16-01): what the items the squad holds add to the skip and the cards (RerollHint.Inputs0 fills them from HeldRules;
        // all 0 / false / null without those items - the 0.15.0 decisions)
        public int SkipLevelUps; public string SkipLevelUpBy = "Reserve Bench";   // C16-01b: level-ups the skip gives here, and the item
        public string SlotBy;                   // C16-01c: the held items that price a filled slot ('Wooden Stick', 'Empty Chest' or both)
        public int ExtraHeal;                   // C16-01f: health a skip restores beyond its own heal (A Cookie)
        public bool CashHeals, CashAlsoOnPick;  // C16-01e: granted cash heals instead (Life Savings); C16-01d: a chest item pays the skip's cash too (Hijacked Signal)
        public double SkipBonus, Economy;       // C16-01f: what Skip Rope's points of the skip are worth; RunContext.Economy
        public string SkipBy;                   // C16-01f: the skip extras held, joined ' and ' (A Cookie, Skip Rope)
        public int RopePoints, RopeReq = 4;     // C16-01f: Skip Rope's points of this skip, Military Training Points per training (the log)
        public bool SurviveQuest;               // C16-13: the active quest asks to survive the run and is followed (RunContext.SurviveQuest)
    }

    internal sealed class ScreenCall
    {
        // 0.16.0: the chest floor stays 2.5 until the user answers Q03 (2.45 proposed: about as many REROLL hints as in 0.15.0 once the item
        // book dropped the false 'damage' bonus - 39 of 150 logged chests instead of 49)
        public const double ChestFloor = 2.5, LevelUpEarly = 3.5, LevelUpLate = 2.5, MilitaryFloor = 2.0, PodFloor = 2.0, SkipUnder = 1.5;
        public const int Budget = 64;           // the words after "REROLL  -  " (the reroll line has a band of its own, 1800 units wide)
        /// <summary>0.16.0 (C16-13): under this squad health (and while the quest asks to survive the run) a second life on the cards holds the
        /// REROLL and SKIP hints back - one constant with the revive need's (C16-12).</summary>
        public const double ReviveHealth = ItemRules.ReviveHurt;

        public bool Show;
        public HintAction Action;
        public string Why = "";                 // shown or not, and why - for the log
        public string Words;                    // what the line says after the action's name (null: nothing shown)
        public string Short;                    // 0.16.0 (C16-15): the same, short - drawn when the line goes under its button (1280 x 800) and stands
                                                // in the WHY band's row; set wherever Words is (BANISH: its own words), null with it
        public HintCard Best, Target;           // the best card; a banish's card
        public double Floor;                    // the screen's reroll floor
        public double SkipValue;                // what the skip bonus is worth on the cards' scale
        public RerollCall Rescue;               // the rescue screen's own verdict (FromRescue)

        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static string F2(double v) { return v.ToString("0.00", IC); }

        /// <summary>The best card's score under which a reroll is advised on <paramref name="screen"/> (0: never).</summary>
        public static double FloorOf(string screen, double progress)
        {
            switch (screen ?? "")
            {
                case "Chest": return ChestFloor;
                case "LevelUp": return progress < 0.5 ? LevelUpEarly : LevelUpLate;
                case "Military": return MilitaryFloor;
                case "Hashtag": return PodFloor;
                default: return 0;
            }
        }

        /// <summary>What the skip bonus is worth on the cards' scale: the heal while the squad is hurting (only the health it is
        /// missing counts) and the cash as the run goal values it. The skip heals 20 % of the team's max health (GetSkipBonusHp), so its
        /// <paramref name="heal"/> gives the max health: heal / 0.2. 0.16.0 (C16-01e / d / f), every older call unchanged: health a held
        /// item adds to the skip (<paramref name="extraHeal"/>, A Cookie), cash that heals instead (<paramref name="cashHeals"/>, Life
        /// Savings: the cash joins the heal and no longer counts as cash) and cash a pick pays as well (<paramref name="cashAlsoOnPick"/>,
        /// Hijacked Signal on a chest: the skip's cash is no gain over a pick). The heal part is capped at max(3, 1.5 x survival) - with
        /// the defaults it never binds (at most the 1.5 x survival of a fifth of the health missing).</summary>
        public static double SkipWorth(int cash, int heal, double health, double survival, double cashWeight, int extraHeal = 0, bool cashHeals = false, bool cashAlsoOnPick = false)
        {
            double maxHp = heal > 0 ? heal / 0.2 : 0;
            int all = heal + Math.Max(0, extraHeal) + (cashHeals && !cashAlsoOnPick ? Math.Max(0, cash) : 0);
            double frac = maxHp > 0 ? 0.2 * ((double)all / heal) : 0;                        // = all / maxHp (exactly 0.2 for the skip's own heal)
            double missing = Math.Max(0, Math.Min(frac, 1 - Clamp01(health))) / 0.2;        // 0 at full health, 1 at 80 % or less for the plain heal
            double cap = Math.Max(3.0, 1.5 * Math.Max(0, survival));
            double healPart = all > 0 ? Math.Min(cap, 1.5 * missing * Math.Max(0, survival)) : 0;
            double cashPart = cash > 0 && !cashHeals && !cashAlsoOnPick ? 0.5 * Math.Max(0, cashWeight) : 0;
            return Math.Round(healPart + cashPart, 2);
        }

        /// <summary>0.16.0 (C16-01): what the held items add to the skip, for the action hint's log after '(worth X)': ' (Life Savings: the
        /// cash heals)', ', a level-up (Reserve Bench)', ', the cash also on a pick (Hijacked Signal)', ', with A Cookie and Skip Rope'; empty
        /// without them.</summary>
        public static string SkipNote(ScreenCallIn x)
        {
            if (x == null) return "";
            string s = "";
            if (x.CashHeals) s += " (Life Savings: the cash heals)";
            if (x.SkipLevelUps > 0) s += ", " + (x.SkipLevelUps == 1 ? "a level-up" : x.SkipLevelUps.ToString(IC) + " level-ups") + " (" + x.SkipLevelUpBy + ")";
            if (x.CashAlsoOnPick) s += ", the cash also on a pick (Hijacked Signal)";
            if (!string.IsNullOrEmpty(x.SkipBy)) s += ", with " + x.SkipBy;
            return s;
        }

        /// <summary>0.16.0 (C16-13): the revive guard's words - '&lt;card&gt; is a second life (a survive quest, squad health 55%)' while a
        /// card holds a second life and the quest asks to survive the run or the squad is under 60 % health; null otherwise.</summary>
        public static string Guard(ScreenCallIn x)
        {
            if (x == null) return null;
            var revive = x.Cards.FirstOrDefault(k => k.Revive);
            if (revive == null) return null;
            bool hurt = x.Health < ReviveHealth;
            if (!x.SurviveQuest && !hurt) return null;
            var why = new List<string>();
            if (x.SurviveQuest) why.Add("a survive quest");
            if (hurt) why.Add("squad health " + ((int)Math.Floor(Clamp01(x.Health) * 100 + 1e-6)).ToString(IC) + "%");
            return revive.Name + " is a second life (" + string.Join(", ", why) + ")";
        }
        static double Clamp01(double v) { return v < 0 ? 0 : v > 1 ? 1 : v; }

        /// <summary>The hint for one screen (at most one action). <paramref name="x"/>.Cards hold the ranked offer.</summary>
        public static ScreenCall Decide(ScreenCallIn x)
        {
            var c = new ScreenCall();
            if (x == null || x.Cards.Count == 0) { c.Why = "no cards"; return c; }
            var best = x.Cards.OrderByDescending(k => Math.Round(k.Score, 2)).ThenBy(k => k.Rank).First();
            c.Best = best;
            c.Floor = FloorOf(x.Screen, x.Progress);
            // the skip's worth (0.16.0, C16-01): its heal and its cash (Life Savings: the cash heals; Hijacked Signal: a chest item pays the
            // cash too), A Cookie's heal, a level-up worth a card at the screen's floor (Reserve Bench), Skip Rope's points
            double plain = SkipWorth(x.SkipCash, x.SkipHeal, x.Health, x.Survival, x.Cash, 0, x.CashHeals, x.CashAlsoOnPick);
            double healed = x.ExtraHeal > 0 ? SkipWorth(x.SkipCash, x.SkipHeal, x.Health, x.Survival, x.Cash, x.ExtraHeal, x.CashHeals, x.CashAlsoOnPick) : plain;
            double levelUps = x.SkipLevelUps > 0 && c.Floor > 0 ? x.SkipLevelUps * c.Floor : 0;
            double bonus = x.SkipBonus > 0 ? x.SkipBonus : 0;
            c.SkipValue = Math.Round(healed + levelUps + bonus, 2);
            double top = Math.Round(best.Score, 2);
            double merit = Math.Round(x.Cards.Max(k => k.Score + k.HeldCost), 2);     // C16-01c: the best card before a held slot cost (= top without one)
            var meritCard = x.Cards.OrderByDescending(k => Math.Round(k.Score + k.HeldCost, 2)).ThenBy(k => k.Rank).First();
            bool allAvoid = x.Cards.All(k => Math.Round(k.Score, 2) < 1.0);
            // C16-01c: with Wooden Stick / Empty Chest held every chest card carries the same slot cost - REROLL is judged on the merit (an early
            // chest's nets all fall under the floor, and a reroll brings items with the same cost), AVOID and SKIP on the net
            string bestText = best.Name + " " + F2(top) + MeritNote(best, meritCard, merit, top);
            // 0.16.0 (C16-13, DK-C01): a second life on the cards while the quest asks to survive the run or the squad is under 60 % health -
            // no REROLL and no SKIP sends it away (the no-item quest's SKIP below keeps its place: any item fails that quest for good)
            string held = Guard(x);

            // SKIP under a quest that wants no item: a reroll brings only more items
            if (x.Skip && x.CanSkip && x.NoItems != null)
            {
                c.Show = true; c.Action = HintAction.Skip;
                c.Words = Pick("Quest: taking any item fails it", "Quest: " + x.NoItems);
                c.Short = Pick("Quest: no items");                                   // C16-15: the line under its button
                c.Why = "quest: " + x.NoItems + "; skip bonus +" + x.SkipCash + " cash, +" + x.SkipHeal + " health";
                return c;
            }
            // REROLL: the best card is under the screen's floor (its merit: before a held slot cost, C16-01c), and the game still has a reroll for it
            if (c.Floor > 0 && merit < c.Floor)
            {
                string under = "best " + bestText + " under the " + ScreenWord(x.Screen) + " floor " + F2(c.Floor);
                if (x.Reroll && x.CanReroll && held != null) c.Why = under + " - held back: " + held;
                else if (x.Reroll && x.CanReroll)
                {
                    c.Show = true; c.Action = HintAction.Reroll;
                    int left = x.FreeReroll ? -1 : x.Rerolls;
                    string tail = left > 0 && left <= 2 ? " (" + left + (left == 1 ? " reroll left)" : " rerolls left)") : "";
                    // the best card's own line may well be "A-tier, fits the X build": weak against the screen's floor, not wrong
                    c.Words = Pick("the best card here is weak for this squad" + tail, "the best here is weak for this squad" + tail, "the best here is weak for this squad");
                    c.Short = Pick("a weak offer" + (left > 0 && left <= 2 ? " (" + left + " left)" : ""));      // C16-15: 'a weak offer (2 left)'
                    c.Why = under + " | rerolls " + x.RerollsText;
                    // C16-01b: Reserve Bench - the Skip button stays after a reroll, and its level-up with it (Q35's default: REROLL first)
                    if (x.SkipLevelUps > 0) c.Why += " | " + x.SkipLevelUpBy + ": if it stays weak, the skip is a level-up";
                    return c;
                }
                else c.Why = under + " - " + (!x.Reroll ? "reroll hints off" : "no reroll left (" + x.RerollsText + ")");
            }
            else c.Why = c.Floor > 0 ? "best " + bestText + " at or over the " + ScreenWord(x.Screen) + " floor " + F2(c.Floor) : "no reroll floor on this screen (best " + bestText + ")";
            if (held != null && c.Why.IndexOf("held back", StringComparison.Ordinal) < 0) c.Why += " | revive guard: " + held;
            // SKIP (0.16.0, C16-01b): Reserve Bench held - the skip of a chest or training under its floor is a level-up, worth at least a card
            // at the floor; once no reroll is left (or reroll hints are off)
            if (held == null && x.Skip && x.CanSkip && x.SkipLevelUps > 0 && c.Floor > 0 && merit < c.Floor)
            {
                c.Show = true; c.Action = HintAction.Skip;
                string by = x.SkipLevelUpBy ?? "Reserve Bench";
                c.Words = Pick("the skip is a level-up (" + by + ") - worth more than these", "the skip is a level-up with " + by, "the skip is a level-up");
                c.Short = Pick("the skip is a level-up");
                c.Why +=" | " + by + ": the skip is a level-up, worth " + F2(c.SkipValue) + " (+" + x.SkipCash + " cash, +" + x.SkipHeal + " health)";
                return c;
            }
            // SKIP: every card would hurt the squad (no reroll to try first)
            if (held == null && x.Skip && x.CanSkip && allAvoid)
            {
                c.Show = true; c.Action = HintAction.Skip;
                // C16-01c: every card fills an empty slot that Wooden Stick / Empty Chest pays for - the empty slot is worth more than these
                bool slot = x.Cards.All(k => k.HeldCost > 0);
                c.Words = slot ? Pick(x.SlotBy != null ? "an empty slot is worth more than these (" + x.SlotBy + ")" : null, "an empty slot is worth more than these cards", "an empty slot is worth more here")
                               : Pick("every card here would hurt this squad - take the skip bonus", "every card here would hurt this squad");
                c.Short = Pick(slot ? "an empty slot is worth more" : "every card hurts");
                c.Why += " | every card is under 1 (AVOID)" + (slot ? " (merit " + F2(merit) + " before the held slot cost " + F2(meritCard.HeldCost) + ")" : "")
                    + "; skip bonus +" + x.SkipCash + " cash, +" + x.SkipHeal + " health";
                return c;
            }
            // SKIP: every card is weak, and the cash and the heal are worth more than the best of them
            if (held == null && x.Skip && x.CanSkip && top < SkipUnder && c.SkipValue > top)
            {
                c.Show = true; c.Action = HintAction.Skip;
                double without = Math.Round(plain + levelUps, 2);             // the skip without A Cookie and Skip Rope (C16-01f)
                bool rope = bonus > 0 && Math.Round(without + bonus, 2) > top, cookie = x.ExtraHeal > 0 && Math.Round(healed + levelUps, 2) > top;
                bool heal = x.SkipHeal > 0 && x.Health < 0.95;
                if (without <= top && bonus > 0 && (rope || !cookie))
                {
                    c.Words = x.Screen == "LevelUp"
                        ? Pick("the skip pays a Skip Rope point - worth more than these cards", "the skip pays a point (Skip Rope)", "the skip pays a point")
                        : Pick("the skip pays a training point (Skip Rope) - worth more than these", "the skip pays a training point (Skip Rope)", "the skip pays a training point");
                    c.Short = Pick(x.Screen == "LevelUp" ? "a Skip Rope point is worth more" : "a training point is worth more");
                }
                else if (without <= top && x.ExtraHeal > 0)
                {
                    c.Words = Pick("the skip heals " + x.ExtraHeal.ToString(IC) + " more (A Cookie) - worth more than these cards", "the heal is worth more than these cards");
                    c.Short = Pick("the heal is worth more");
                }
                else if (x.CashHeals || x.CashAlsoOnPick)            // C16-01e / d: no cash to gain over a pick - the heal decides
                {
                    c.Words = Pick("the heal is worth more than these cards", "the heal is worth more");
                    c.Short = Pick("the heal is worth more");
                }
                else
                {
                    c.Words = heal ? Pick("the heal and the cash are worth more than these cards", "the heal and the cash are worth more")
                                   : Pick("the cash is worth more than these cards", "the cash is worth more");
                    c.Short = Pick(heal ? "the heal and cash are worth more" : "the cash is worth more");      // C16-15
                }
                c.Why += " | every card under " + F2(SkipUnder) + ", the skip bonus worth " + F2(c.SkipValue) + " (+" + x.SkipCash + " cash, +" + x.SkipHeal + " health, squad health " + ((int)Math.Round(x.Health * 100)).ToString(IC) + "%)";
                if (x.ExtraHeal > 0) c.Why += " | A Cookie: +" + x.ExtraHeal.ToString(IC) + " health";
                if (bonus > 0) c.Why += " | Skip Rope: " + (HeldRules.RopeNote(x.RopePoints > 0 ? x.RopePoints : x.Screen == "LevelUp" ? HeldRules.RopeSmall : HeldRules.RopeBig, x.RopeReq) ?? "points") + " worth " + F2(bonus);
                return c;
            }
            // BANISH (off by default): the worst banishable card the build skips or that would hurt the squad - never a second life (C16-13)
            if (x.Banish && x.CanBanish)
            {
                HintCard target = null;
                foreach (var k in x.Cards.OrderBy(k => k.Score))
                {
                    if (k.Rank == 1 || ReferenceEquals(k, best) || !k.Banishable || k.Revive || Protected(x.Quest, k)) continue;      // never the card framed RECOMMENDED
                    if (k.Skipped || Math.Round(k.Score, 2) < 1.0) { target = k; break; }
                }
                if (target != null)
                {
                    c.Show = true; c.Action = HintAction.Banish; c.Target = target;
                    c.Words = target.Skipped && target.Build != null
                        ? Pick(target.Name + " - the " + target.Build + " build skips it", target.Name + " - the build skips it", "the build skips " + target.Name)
                        : Pick(target.Name + " would hurt this squad", "this card would hurt this squad");
                    c.Short = c.Words;                                                  // C16-15: BANISH keeps its words (off by default)
                    c.Why += " | banish " + target.Name + " " + F2(Math.Round(target.Score, 2)) + (target.Skipped ? " (the build skips it)" : " (AVOID)") + ", banishes " + (x.Banishes >= 0 ? x.Banishes.ToString(IC) : "?");
                    return c;
                }
            }
            return c;
        }

        static string ScreenWord(string screen) { return screen == "LevelUp" ? "level-up" : screen == "Chest" ? "chest" : screen == "Military" ? "military" : screen == "Hashtag" ? "Research Pod" : screen; }

        /// <summary>0.16.0 (C16-01c): the log's merit after the best card - ' (3.12 before the held slot cost 2.18)', or with the name of the
        /// card whose merit it is when that is another (' (Black Box 4.76 before the held slot cost 2.18)'); empty without a held slot cost.</summary>
        static string MeritNote(HintCard best, HintCard meritCard, double merit, double top)
        {
            if (meritCard == null || meritCard.HeldCost <= 0 || merit <= top) return "";
            return " (" + (ReferenceEquals(meritCard, best) ? "" : meritCard.Name + " ") + F2(merit) + " before the held slot cost " + F2(meritCard.HeldCost) + ")";
        }

        /// <summary>Does the active quest name this card (its class, its name or base, a health item it counts, or a quest line
        /// that decided the card)? Such a card is never advised for a banish: FullyUpgradeClass fails for good on a banished powerup
        /// of its class.</summary>
        public static bool Protected(QuestRules q, HintCard k)
        {
            if (k == null) return false;
            if ((k.Head ?? "").StartsWith("quest", StringComparison.OrdinalIgnoreCase)) return true;
            if (q == null || !q.Follows) return false;
            foreach (var r in q.List)
            {
                if (!r.Open) continue;
                if (r.Class != null && k.Class != null && string.Equals(r.Class, k.Class, StringComparison.OrdinalIgnoreCase)
                    && (r.Ask == QuestAsk.FullClass || r.Ask == QuestAsk.Synergies || r.Ask == QuestAsk.WeaponLine || r.Ask == QuestAsk.Kills)) return true;
                foreach (var t in r.Targets)
                    if (string.Equals(t, k.Name, StringComparison.OrdinalIgnoreCase) || (k.Base != null && string.Equals(t, k.Base, StringComparison.OrdinalIgnoreCase))) return true;
                if (r.Ask == QuestAsk.HealthItems && k.Health) return true;
            }
            return false;
        }

        /// <summary>The rescue screen's verdict (RerollCall, 0.12.2) as an action hint: its words are the line it always had
        /// ("Tank would fit this squad better (+1 more)", "the quest needs Huntress"). 0.16.0 (C16-15): <paramref name="shortWords"/> the
        /// line under its button ("Tank fits better", Wording.RerollShort; the quest's form keeps its words) - null: the words themselves.</summary>
        public static ScreenCall FromRescue(RerollCall r, string words, string shortWords = null)
        {
            var c = new ScreenCall { Rescue = r };
            if (r == null) { c.Why = "no verdict"; return c; }
            c.Show = r.Show; c.Action = r.Show ? HintAction.Reroll : HintAction.None; c.Why = r.Why; c.Words = r.Show ? words : null;
            c.Short = c.Words == null ? null : shortWords ?? c.Words;
            return c;
        }

        /// <summary>The action's name as the line shows it in front of its words.</summary>
        public static string Name(HintAction a) { return a == HintAction.Skip ? "SKIP" : a == HintAction.Banish ? "BANISH" : a == HintAction.Reroll ? "REROLL" : ""; }

        // the first form within the budget, in the card font's glyphs (the last one when none fits)
        static string Pick(params string[] forms)
        {
            string last = null;
            foreach (var f in forms) { if (string.IsNullOrEmpty(f)) continue; last = Wording.Safe(f); if (last.Length <= Budget) return last; }
            return last ?? "";
        }
    }
}
