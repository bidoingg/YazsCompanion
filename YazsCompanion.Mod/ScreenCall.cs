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
    }

    internal sealed class ScreenCall
    {
        public const double ChestFloor = 2.5, LevelUpEarly = 3.5, LevelUpLate = 2.5, MilitaryFloor = 2.0, PodFloor = 2.0, SkipUnder = 1.5;
        public const int Budget = 64;           // the words after "REROLL  -  " (the reroll line has a band of its own, 1800 units wide)

        public bool Show;
        public HintAction Action;
        public string Why = "";                 // shown or not, and why - for the log
        public string Words;                    // what the line says after the action's name (null: nothing shown)
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
        /// missing counts, at most the 20 % the skip heals) and the cash as the run goal values it.</summary>
        public static double SkipWorth(int cash, int heal, double health, double survival, double cashWeight)
        {
            double missing = Math.Max(0, Math.Min(0.2, 1 - Clamp01(health))) / 0.2;      // 0 at full health, 1 at 80 % or less
            double healPart = heal > 0 ? 1.5 * missing * Math.Max(0, survival) : 0;
            double cashPart = cash > 0 ? 0.5 * Math.Max(0, cashWeight) : 0;
            return Math.Round(healPart + cashPart, 2);
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
            c.SkipValue = SkipWorth(x.SkipCash, x.SkipHeal, x.Health, x.Survival, x.Cash);
            double top = Math.Round(best.Score, 2);
            bool allAvoid = x.Cards.All(k => Math.Round(k.Score, 2) < 1.0);
            string bestText = best.Name + " " + F2(top);

            // SKIP under a quest that wants no item: a reroll brings only more items
            if (x.Skip && x.CanSkip && x.NoItems != null)
            {
                c.Show = true; c.Action = HintAction.Skip;
                c.Words = Pick("Quest: taking any item fails it", "Quest: " + x.NoItems);
                c.Why = "quest: " + x.NoItems + "; skip bonus +" + x.SkipCash + " cash, +" + x.SkipHeal + " health";
                return c;
            }
            // REROLL: the best card is under the screen's floor, and the game still has a reroll for it
            if (c.Floor > 0 && top < c.Floor)
            {
                if (x.Reroll && x.CanReroll)
                {
                    c.Show = true; c.Action = HintAction.Reroll;
                    int left = x.FreeReroll ? -1 : x.Rerolls;
                    string tail = left > 0 && left <= 2 ? " (" + left + (left == 1 ? " reroll left)" : " rerolls left)") : "";
                    // the best card's own line may well be "A-tier, fits the X build": weak against the screen's floor, not wrong
                    c.Words = Pick("the best card here is weak for this squad" + tail, "the best here is weak for this squad" + tail, "the best here is weak for this squad");
                    c.Why = "best " + bestText + " under the " + ScreenWord(x.Screen) + " floor " + F2(c.Floor) + " | rerolls " + x.RerollsText;
                    return c;
                }
                c.Why = "best " + bestText + " under the " + ScreenWord(x.Screen) + " floor " + F2(c.Floor) + " - " + (!x.Reroll ? "reroll hints off" : "no reroll left (" + x.RerollsText + ")");
            }
            else c.Why = c.Floor > 0 ? "best " + bestText + " at or over the " + ScreenWord(x.Screen) + " floor " + F2(c.Floor) : "no reroll floor on this screen (best " + bestText + ")";
            // SKIP: every card would hurt the squad (no reroll to try first)
            if (x.Skip && x.CanSkip && allAvoid)
            {
                c.Show = true; c.Action = HintAction.Skip;
                c.Words = Pick("every card here would hurt this squad - take the skip bonus", "every card here would hurt this squad");
                c.Why += " | every card is under 1 (AVOID); skip bonus +" + x.SkipCash + " cash, +" + x.SkipHeal + " health";
                return c;
            }
            // SKIP: every card is weak, and the cash and the heal are worth more than the best of them
            if (x.Skip && x.CanSkip && top < SkipUnder && c.SkipValue > top)
            {
                c.Show = true; c.Action = HintAction.Skip;
                bool heal = x.SkipHeal > 0 && x.Health < 0.95;
                c.Words = heal ? Pick("the heal and the cash are worth more than these cards", "the heal and the cash are worth more")
                               : Pick("the cash is worth more than these cards", "the cash is worth more");
                c.Why += " | every card under " + F2(SkipUnder) + ", the skip bonus worth " + F2(c.SkipValue) + " (+" + x.SkipCash + " cash, +" + x.SkipHeal + " health, squad health " + ((int)Math.Round(x.Health * 100)).ToString(IC) + "%)";
                return c;
            }
            // BANISH (off by default): the worst banishable card the build skips or that would hurt the squad
            if (x.Banish && x.CanBanish)
            {
                HintCard target = null;
                foreach (var k in x.Cards.OrderBy(k => k.Score))
                {
                    if (k.Rank == 1 || ReferenceEquals(k, best) || !k.Banishable || Protected(x.Quest, k)) continue;      // never the card framed RECOMMENDED
                    if (k.Skipped || Math.Round(k.Score, 2) < 1.0) { target = k; break; }
                }
                if (target != null)
                {
                    c.Show = true; c.Action = HintAction.Banish; c.Target = target;
                    c.Words = target.Skipped && target.Build != null
                        ? Pick(target.Name + " - the " + target.Build + " build skips it", target.Name + " - the build skips it", "the build skips " + target.Name)
                        : Pick(target.Name + " would hurt this squad", "this card would hurt this squad");
                    c.Why += " | banish " + target.Name + " " + F2(Math.Round(target.Score, 2)) + (target.Skipped ? " (the build skips it)" : " (AVOID)") + ", banishes " + (x.Banishes >= 0 ? x.Banishes.ToString(IC) : "?");
                    return c;
                }
            }
            return c;
        }

        static string ScreenWord(string screen) { return screen == "LevelUp" ? "level-up" : screen == "Chest" ? "chest" : screen == "Military" ? "military" : screen == "Hashtag" ? "Research Pod" : screen; }

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
        /// ("Tank would fit this squad better (+1 more)", "the quest needs Huntress").</summary>
        public static ScreenCall FromRescue(RerollCall r, string words)
        {
            var c = new ScreenCall { Rescue = r };
            if (r == null) { c.Why = "no verdict"; return c; }
            c.Show = r.Show; c.Action = r.Show ? HintAction.Reroll : HintAction.None; c.Why = r.Why; c.Words = r.Show ? words : null;
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
