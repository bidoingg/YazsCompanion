// The active quest's team rules (0.13.0, C1). A quest of the game's quest log can limit the team: "Team Size" of the
// Huntress's 'Readjust' is a statistic threshold on InternalNumSurvivors (== 1, the leader counts: the HUD's check is
// green while the leader is alone - finish solo), the Ghost's third quest wants exactly Ghost + Huntress (a
// FinishSpecificTeamSetup with == 2), two others want a full team of 3 (FullTeamTier3Weapons, TimedPowerupsFullTeam).
// Up to 0.13.0 the rescue cards knew none of it: in the user's 10-04 run the Companion ranked a survivor first on six of
// seven rescue screens and showed the REROLL HINT three times, while the quest wanted no recruit at all - the user took
// Liberate seven times out of seven.
//
// Quest.cs reads the objectives from the game (once per quest and run) into a QuestTeam; Judge() says what that means
// for the squad as it stands (QuestSos), and the SOS cards, the reroll hint and the PLAN readout's SOS row follow it:
//  - the team is at the quest's limit: Liberate first ("quest: stay solo - take the level-up and cash"), every recruit
//    far below ("quest: stay solo - Tank would fail it"), no reroll hint, the SOS row reads "quest: stay solo";
//  - the quest needs a class the squad lacks: that survivor first ("quest: needs Huntress"), a recruit that would take
//    the needed one's slot far below, Liberate keeps the slot; the reroll hint speaks for the needed class alone;
//  - the quest wants more survivors than the squad has: Liberate goes under every recruit (late in a run the clock
//    would say Liberate), and the late SOS row names the recruits with "quest: full team" instead of "Liberate".
// A quest whose objectives match "Any" (one way of several), a quest the game counts as failed, a run that does not fit
// the quest's conditions (arena, mode, difficulty or leader: the game completes a quest only in a run that fits them -
// QuestCompleted asks AreFulfillmentConditionsMet first) and a team that has broken the rule already leave the advice as
// it is. Pure: the offline bench replays it.
using System;
using System.Collections.Generic;
using System.Linq;

namespace YazsCompanion
{
    /// <summary>What the active quest wants of the team at the end of the run (the leader counts).</summary>
    internal sealed class QuestTeam
    {
        public const int Full = 3;                  // the game's team: the leader and two recruits
        public string Quest = "";                   // the quest's asset name, for the log
        public int Min = 1, Max = Full;
        public readonly List<string> Needs = new List<string>();     // classes the team must hold (the game's display names: Ghost for Ninja)
        public bool AnyOf;                          // the objectives match "Any": a team rule is one way of several
        public bool Failed;                         // the game counts the quest as failed already
        public string NotThisRun;                   // why this run cannot complete the quest (its arena / mode / difficulty / leader condition refuses the run); null = it can
        public string Muted;                        // 0.14.0 (C3): [Advice] QuestSteer is not On - why the rule is not followed (null: it is)

        /// <summary>The quest has a team rule at all (followed or not).</summary>
        public bool HasRule { get { return Min > 1 || Max < Full || Needs.Count > 0; } }
        /// <summary>The quest limits the team in a way the advice follows.</summary>
        public bool Rules { get { return !AnyOf && !Failed && NotThisRun == null && Muted == null && HasRule; } }

        static int Clamp(int n) { return n < 0 ? 0 : n > Full ? Full : n; }
        public void AtLeast(int n) { Min = Math.Max(Min, Clamp(n)); }
        public void AtMost(int n) { Max = Math.Min(Max, Math.Max(1, Clamp(n))); }
        public void Need(string cls) { if (!string.IsNullOrEmpty(cls) && !Needs.Contains(cls, StringComparer.OrdinalIgnoreCase)) Needs.Add(cls); }

        static readonly string[] Ops = { "<", "<=", "==", ">=", ">" };

        /// <summary>An InternalNumSurvivors threshold: <paramref name="comparison"/> is the game's GameHubQuestComparison
        /// (0 Less, 1 LessOrEqual, 2 Equal, 3 GreaterOrEqual, 4 Greater). Returns the rule in words, for the log.</summary>
        public string Survivors(int comparison, double threshold)
        {
            int lo = (int)Math.Floor(threshold + 1e-6), hi = (int)Math.Ceiling(threshold - 1e-6);
            switch (comparison)
            {
                case 0: AtMost(hi - 1); break;
                case 1: AtMost(lo); break;
                case 2: { int n = (int)Math.Round(threshold); AtLeast(n); AtMost(n); break; }
                case 3: AtLeast(hi); break;
                case 4: AtLeast(lo + 1); break;
                default: return "survivors, comparison " + comparison + " unknown - advice unchanged";
            }
            return "survivors " + Ops[comparison] + " " + threshold.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " (the leader counts)";
        }

        /// <summary>The rule in a few words: "stay solo", "team of 2", "full team", "Ghost + Huntress".</summary>
        public string Words
        {
            get
            {
                if (Needs.Count > 0) return string.Join(" + ", Needs);
                if (Max <= 1) return "stay solo";
                if (Min >= Full) return "full team";
                if (Min == Max) return "team of " + Max;
                if (Max < Full) return "at most " + Max;
                if (Min > 1) return "team of " + Min + " or more";
                return "any team";
            }
        }

        /// <summary>What the rule means for the squad right now (<paramref name="squad"/> = its classes, the leader first).</summary>
        public QuestSos Judge(IList<string> squad)
        {
            var v = new QuestSos { Words = Words, Min = Min, Max = Max, Size = squad == null ? 0 : squad.Count };
            if (!Rules) { v.Broken = Muted ?? (AnyOf ? "its objectives match Any (a team rule is one way of several)" : Failed ? "the game counts the quest as failed" : NotThisRun); return v; }
            foreach (var n in Needs) if (squad == null || !squad.Contains(n, StringComparer.OrdinalIgnoreCase)) v.Missing.Add(n);
            if (v.Size > Max) { v.Broken = "the team is " + v.Size + " already, the quest wants " + (Min == Max ? "" : "at most ") + Max; return v; }
            if (v.Missing.Count > Max - v.Size) { v.Broken = "no room left for " + string.Join(" + ", v.Missing) + " (team of " + v.Size + ", at most " + Max + ")"; return v; }
            v.Active = true;
            return v;
        }
    }

    /// <summary>The quest's word on a rescue screen (QuestTeam.Judge).</summary>
    internal sealed class QuestSos
    {
        public const double LiberateCapped = 5.0;     // as "squad is full: take the level-up and cash"
        public const double LiberateKeeps = 4.0;      // Liberate keeps the slot for the class the quest needs
        public const double LiberateEmpty = 0.2;      // under every recruit while the quest wants more survivors
        public const double RecruitFails = 0.3;       // a recruit that would fail the quest
        public const double RecruitFloor = 0.3;       // a recruit while the quest wants more survivors stays over Liberate
        public const double Needed = 8.0;             // the class the quest needs: over any other recruit (they score 7 at most)

        public bool Active;                           // the quest changes the advice now
        public string Words = "";
        public string Broken;                         // why the rule no longer steers anything (null: it never did, or it does)
        public int Size, Min, Max;
        public readonly List<string> Missing = new List<string>();      // classes the quest needs that the squad lacks

        /// <summary>The squad is at the quest's limit: no recruit may join (a squad of three is full anyway).</summary>
        public bool Capped { get { return Active && Max < QuestTeam.Full && Size >= Max; } }
        /// <summary>Every slot left belongs to a class the quest needs.</summary>
        public bool OnlyNeeded { get { return Active && !Capped && Missing.Count > 0 && Max - Size <= Missing.Count; } }
        /// <summary>The quest wants more survivors than the squad has: Liberate works against it.</summary>
        public bool WantsMore { get { return Active && Size < Min && Size < QuestTeam.Full; } }
        public bool Wanted(string cls) { return Active && !Capped && Missing.Contains(cls ?? "", StringComparer.OrdinalIgnoreCase); }
        public bool Fails(string cls) { return Active && (Capped || (Missing.Count > 0 && !Wanted(cls) && Max - (Size + 1) < Missing.Count)); }

        /// <summary>A rescue card under the quest: its score and its reasons (<paramref name="candidate"/> = the survivor's
        /// class, null for Liberate). The quest's line goes first where it decides the card.</summary>
        public double Card(string candidate, double score, List<string> why)
        {
            if (!Active) return score;
            if (candidate == null)
            {
                if (Capped) { why.Insert(0, "quest: " + Words + " - take the level-up and cash"); return Math.Max(score, LiberateCapped); }
                if (OnlyNeeded) { why.Insert(0, "quest: " + Words + " - keep the slot for " + string.Join(" + ", Missing)); return Math.Max(score, LiberateKeeps); }
                if (WantsMore) { why.Insert(0, "quest: " + Words + " - Liberate leaves a slot empty"); return Math.Min(score, LiberateEmpty); }
                return score;
            }
            if (Wanted(candidate)) { why.Insert(0, "quest: needs " + candidate); return Math.Max(score + 2.0, Needed); }
            if (Fails(candidate)) { why.Insert(0, "quest: " + Words + " - " + candidate + " would fail it"); return Math.Min(score, RecruitFails); }
            if (WantsMore) { why.Add("quest: " + Words); return Math.Max(score, RecruitFloor); }
            return score;
        }

        /// <summary>The PLAN readout's SOS row under the quest: null = the quest does not change it; <paramref name="replace"/>
        /// = the row is this text alone, else it follows the recruits' names (late in a run, where the row would say Liberate).
        /// <paramref name="show"/> turns a class name into the name shown (another mod's, else the game's).</summary>
        public string PlanText(bool late, Func<string, string> show, out bool replace)
        {
            replace = false;
            if (!Active) return null;
            Func<string, string> sh = show ?? (x => x);
            if (Capped) { replace = true; return "quest: " + (Missing.Count == 0 && Words.Contains("+") ? "team complete" : Words); }
            if (Missing.Count > 0) { replace = true; return "quest: " + string.Join(", ", Missing.Select(sh)); }
            if (WantsMore && late) return "quest: " + Words;
            return null;
        }

        /// <summary>One line for the log: what the advice does under the quest now.</summary>
        public string Advice()
        {
            if (!Active) return Broken != null ? "advice as usual - " + Broken : "advice as usual";
            if (Capped) return "Liberate first on rescue screens (team of " + Size + "), no reroll hint for a recruit, PLAN 'SOS  quest: ...'";
            if (Missing.Count > 0) return string.Join(" + ", Missing) + " first on rescue screens" + (OnlyNeeded ? ", any other recruit last, Liberate keeps the slot" : "") + "; the reroll hint speaks for " + string.Join(" + ", Missing) + " alone";
            if (WantsMore) return "recruits over Liberate (the quest wants a team of " + Min + ", the squad is " + Size + ")";
            return "advice as usual (the rule is met)";
        }
    }
}
