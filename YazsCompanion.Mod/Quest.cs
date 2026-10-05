// The active quest's objectives, read from the game for the advice (0.13.0, C1; the rules themselves: QuestTeam.cs, pure).
// GameQuestManager.Get.ActiveQuest is the quest the player accepted (a runtime copy of its asset); its objectives list holds
// GameHubQuestObjectiveBase entries of several classes. Read once per quest and run (the next run, or another quest, reads
// again) and logged once:
//     [quest] GameHubQuest_Huntress_4 "Readjust" - 2 objectives (objectives), all must hold: 1. StatisticThreshold
//     (LiveAndOnRunFinished) InternalNumSurvivors -> survivors == 1 (the leader counts); 2. Survive (LiveAndOnRunFinished)
//     advice unchanged; the run fits the quest's conditions (arena, mode, difficulty, leader Huntress); | team rule: stay solo
// A run that does not fit the quest's conditions cannot complete it: the game shows the quest box in every run, but when the
// quest would complete, QuestCompleted asks AreFulfillmentConditionsMet first (read in GameAssembly.dll of 1.0.2: the run's
// level, mode, difficulty and leader - GameplayMaster.level / currentGameMode / currentMainCharacterType - against the quest's
// arenaFulfillment / modeFulfillment / difficultyFulfillment / leaderFulfillment) and returns without completing it. Its team
// rule is then logged, not followed (a solo run for nothing). That check unreadable: the leader condition alone
// (IsLeaderFulfilled); that unreadable too: the rule is followed as read.
// What the advice follows (members checked against the 1.0.2 interop: GameHubQuestBase.objectives / _objectiveDefinitions /
// objectiveMatchMode / IsFailed, GameHubQuestObjectiveStatisticThreshold.statisticType / comparison / threshold,
// GameHubQuestObjectiveFinishSpecificTeamSetup.requiredCharactersSetup, GameHubQuestObjectiveFullTeamTier3Weapons and
// GameHubQuestObjectiveTimedPowerupsFullTeam.requiredTeamSize; the 1.0.2 quests that use them: Huntress_4 == 1 survivor,
// Ghost_3 Ghost + Huntress and == 2, Mechanic_R2 and Engineer_1 a full team):
//  - InternalNumSurvivors thresholds -> the team size (QuestTeam.Survivors),
//  - FinishSpecificTeamSetup -> the classes the team must hold,
//  - FullTeamTier3Weapons / TimedPowerupsFullTeam -> at least requiredTeamSize survivors (trivially right: the objective
//    counts a full team, so Liberate works against it).
// Every other objective (Survive, statistic thresholds on other statistics, FinishWithWeapons, game statistics, ...) is
// logged with its class and leaves the advice unchanged. Every game member is read through a small NoInlining accessor
// inside a try, as in LoadoutState: a member a game patch took away costs that one reading, not the run.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CT = GamePlayer.CharacterType;

namespace YazsCompanion
{
    internal static class Quest
    {
        static IntPtr _quest = IntPtr.Zero;           // the quest read (zero: none read this run)
        static bool _none;                            // no active quest this run (said once)
        static QuestTeam _team;
        static string _said;                          // the last verdict logged, so that a change is said once
        static bool _warned;                          // a failed read was said (once a run)
        static float _clock = -1f;                    // the play clock of the last snapshot: going back = a new run in the same scene (Try Again)
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        /// <summary>The HUD went away (the run ended), or the play clock went back (Try Again keeps the scene and the HUD): the
        /// next run reads the quest again and logs it once more.</summary>
        public static void Forget() { _quest = IntPtr.Zero; _none = false; _team = null; _said = null; _warned = false; _clock = -1f; }

        // ---- the game's members, one accessor each
        [MethodImpl(MethodImplOptions.NoInlining)] static GameHubQuestBase Active() { var qm = GameQuestManager.Get; return qm == null ? null : qm.ActiveQuest; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Il2CppSystem.Collections.Generic.List<GameHubQuestObjectiveBase> Objectives(GameHubQuestBase q) { return q.objectives; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Il2CppSystem.Collections.Generic.List<GameHubQuestObjectiveBase> Definitions(GameHubQuestBase q) { return q._objectiveDefinitions; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool MatchAny(GameHubQuestBase q) { return q.objectiveMatchMode == GameHubQuestObjectiveMatchMode.Any; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool IsFailed(GameHubQuestBase q) { return q.IsFailed; }
        [MethodImpl(MethodImplOptions.NoInlining)] static string QuestName(GameHubQuestBase q) { return q.GetQuestName(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static string Timing(GameHubQuestObjectiveBase o) { return o.evaluationTiming.ToString(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static string ClassOf(GameHubQuestObjectiveBase o) { return o.GetIl2CppType().Name; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool LeaderOk(GameHubQuestBase q, CT leader) { return q.IsLeaderFulfilled(leader); }
        // the game's own test before it completes the quest: this run's arena, mode, difficulty and leader (reads only)
        [MethodImpl(MethodImplOptions.NoInlining)] static bool RunFits(GameHubQuestBase q) { return q.AreFulfillmentConditionsMet(); }
        // a run is on: the game has players (the main menu's warm-up plans use a stand-in squad - no quest is read for them)
        [MethodImpl(MethodImplOptions.NoInlining)] static bool InRun() { var m = GameplayMaster.s_instance; var ps = m == null ? null : m.gamePlayers; return ps != null && ps.Count > 0; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string Threshold(GameHubQuestObjectiveBase o, QuestTeam team)
        {
            var st = o.TryCast<GameHubQuestObjectiveStatisticThreshold>();
            if (st == null) return null;
            string stat = st.statisticType.ToString(), rule = stat + " " + st.comparison + " " + st.threshold.ToString("0.##", IC);
            if (st.statisticType != PlayerStatistic.EType.InternalNumSurvivors) return rule + " -> advice unchanged";
            return stat + " -> " + team.Survivors((int)st.comparison, st.threshold);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string TeamSetup(GameHubQuestObjectiveBase o, QuestTeam team)
        {
            var ts = o.TryCast<GameHubQuestObjectiveFinishSpecificTeamSetup>();
            if (ts == null) return null;
            var names = new List<string>();
            foreach (var cls in G.Each(ts.requiredCharactersSetup)) { string n = G.ClassName(cls); names.Add(n); team.Need(n); }
            return "requiredCharactersSetup " + (names.Count > 0 ? string.Join(" + ", names) : "(empty)") + " -> the team must hold " + (names.Count > 0 ? string.Join(" + ", names) : "nobody in particular");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string FullTeam(GameHubQuestObjectiveBase o, QuestTeam team)
        {
            int n = -1;
            var a = o.TryCast<GameHubQuestObjectiveFullTeamTier3Weapons>(); if (a != null) n = a.requiredTeamSize;
            else { var b = o.TryCast<GameHubQuestObjectiveTimedPowerupsFullTeam>(); if (b != null) n = b.requiredTeamSize; }
            if (n < 0) return null;
            if (n <= 1) return "requiredTeamSize " + n + " -> advice unchanged";
            team.AtLeast(n);
            return "requiredTeamSize " + n + " -> a team of " + n + " (Liberate works against it)";
        }

        /// <summary>The active quest's team rules for this run (null: no quest, or it could not be read). Read when the quest
        /// changes; the objectives are logged then.</summary>
        public static QuestTeam Team(Snapshot s)
        {
            bool run = false; try { run = InRun(); } catch { }
            if (!run) return null;
            if (s != null) { if (_clock >= 0f && s.Seconds < _clock - 5f) Forget(); _clock = s.Seconds; }
            GameHubQuestBase q = null;
            try { q = Active(); } catch { }
            IntPtr p = IntPtr.Zero; try { if (q != null) p = q.Pointer; } catch { }
            if (p == IntPtr.Zero)
            {
                if (_quest != IntPtr.Zero || (!_none && s != null && s.Squad.Count > 0)) { _none = true; _quest = IntPtr.Zero; _team = null; _said = null; Plugin.Logger.LogInfo("[quest] no active quest this run - the rescue advice follows the squad and the clock alone"); }
                return null;
            }
            if (p == _quest)
            {
                if (_team != null)
                {
                    try { _team.Failed = IsFailed(q); } catch { }
                    // a run that did not fit when the quest was read is asked again (the run's level or mode may not have been set
                    // yet); one that fitted stays so - the end of a run clears the level and the mode, and the advice must not flap
                    if (_team.NotThisRun != null) { string fit; _team.NotThisRun = NotThisRun(q, s, out fit); }
                }
                return _team;
            }
            _quest = p; _none = false; _said = null;
            _team = Read(q, s);
            return _team;
        }

        /// <summary>Why this run cannot complete the quest (null: it can, or that is unknown). The game's own test first
        /// (AreFulfillmentConditionsMet: arena, mode, difficulty, leader), else the leader condition alone; <paramref name="said"/>
        /// = the words for the '[quest]' line.</summary>
        static string NotThisRun(GameHubQuestBase q, Snapshot s, out string said)
        {
            Survivor leader = null; if (s != null) foreach (var sv in s.Squad) if (sv.Leader) { leader = sv; break; }
            string who = leader != null ? leader.Name : null;
            bool? leaderOk = null, runOk = null;
            try { if (leader != null) leaderOk = LeaderOk(q, leader.Type); } catch { }
            try { runOk = RunFits(q); } catch { }
            string lead = who == null ? "" : leaderOk == false ? "; the leader, " + who + ", does not fit" : leaderOk == true ? "; leader " + who + " fits" : "";
            if (runOk == true) { said = "the run fits the quest's conditions (arena, mode, difficulty, leader" + (who != null ? " " + who : "") + ")"; return null; }
            if (runOk == false)
            {
                said = "the run does NOT fit the quest's conditions (arena, mode, difficulty or leader" + lead + ") - the quest cannot complete this run, its team rule is not followed";
                return "this run does not fit the quest's conditions (arena, mode, difficulty or leader" + lead + ") - the quest cannot complete this run";
            }
            if (leaderOk == false)
            {
                said = "the run's conditions unreadable; the leader, " + who + ", does NOT fit the quest's leader condition - the quest cannot complete this run, its team rule is not followed";
                return "the leader (" + who + ") does not fit the quest's leader condition - the quest cannot complete this run";
            }
            said = "the run's conditions unreadable" + (leaderOk == true ? "; leader " + who + " fits" : "") + " - the team rule is followed as read";
            return null;
        }

        static QuestTeam Read(GameHubQuestBase q, Snapshot s)
        {
            var team = new QuestTeam();
            var sb = new StringBuilder("[quest] ");
            try
            {
                team.Quest = G.Asset(q);
                sb.Append(team.Quest.Length > 0 ? team.Quest : "(unnamed quest)");
                try { string n = QuestName(q); if (!string.IsNullOrEmpty(n)) sb.Append(" \"").Append(ItemRules.RichTag.Replace(n, "")).Append('"'); } catch { }
                Il2CppSystem.Collections.Generic.List<GameHubQuestObjectiveBase> list = null; string from = "objectives";
                int nObj = -1, nDef = -1;
                try { list = Objectives(q); nObj = list == null ? -1 : list.Count; } catch (Exception e) { sb.Append(" (objectives unreadable: ").Append(e.GetType().Name).Append(')'); }
                if (nObj <= 0)
                {   // the runtime copy may keep its entries in the definitions list only
                    try { var defs = Definitions(q); nDef = defs == null ? -1 : defs.Count; if (nDef > 0) { list = defs; from = "_objectiveDefinitions"; } } catch { }
                }
                try { team.AnyOf = MatchAny(q); } catch { }
                try { team.Failed = IsFailed(q); } catch { }
                int count = list == null ? 0 : list.Count;
                sb.Append(" - ").Append(count).Append(count == 1 ? " objective" : " objectives").Append(" (").Append(from)
                  .Append(nObj <= 0 && from == "objectives" ? ", the definitions list " + (nDef < 0 ? "unreadable" : nDef.ToString(IC)) : "").Append("), ")
                  .Append(team.AnyOf ? "ANY one of them completes it" : "all must hold").Append(':');
                int i = 0;
                foreach (var o in G.Each(list))
                {
                    i++;
                    if (o == null) { sb.Append(' ').Append(i).Append(". (null);"); continue; }
                    string cls = "?"; try { cls = ClassOf(o); } catch { }
                    string timing = ""; try { timing = Timing(o); } catch { }
                    string what = null;
                    try { what = Threshold(o, team); } catch (Exception e) { what = "unreadable (" + e.GetType().Name + ") - advice unchanged"; }
                    if (what == null) { try { what = TeamSetup(o, team); } catch (Exception e) { what = "unreadable (" + e.GetType().Name + ") - advice unchanged"; } }
                    if (what == null) { try { what = FullTeam(o, team); } catch (Exception e) { what = "unreadable (" + e.GetType().Name + ") - advice unchanged"; } }
                    if (what == null) what = "advice unchanged";
                    sb.Append(' ').Append(i).Append(". ").Append(cls.Replace("GameHubQuestObjective", "")).Append(timing.Length > 0 ? " (" + timing + ")" : "").Append(" ").Append(what).Append(';');
                }
                if (count == 0) sb.Append(" none readable - advice unchanged;");
                string fit; team.NotThisRun = NotThisRun(q, s, out fit);
                sb.Append(' ').Append(fit).Append(';');
                sb.Append(" | team rule: ").Append(team.Rules ? team.Words : team.HasRule ? team.Words + ", not followed (" + (team.AnyOf ? "objectives match Any" : team.Failed ? "failed already" : "this run does not fit the quest") + ")" : "none");
            }
            catch (Exception e) { sb.Append(" - not read: ").Append(e.GetType().Name).Append(' ').Append(e.Message); }
            Plugin.Logger.LogInfo(sb.ToString());
            return team;
        }

        /// <summary>The quest's word on the squad as it stands (QuestSos; never null). A verdict that differs from the last one
        /// said is logged once ('[quest] team of 1: Liberate first ...').</summary>
        public static QuestSos Judge(Snapshot s)
        {
            QuestTeam team = null;
            try { team = Team(s); }
            catch (Exception e) { if (!_warned) { _warned = true; Plugin.Logger.LogWarning("[quest] not read: " + e.GetType().Name + " " + e.Message + " (said once a run)"); } }
            var squad = new List<string>();
            if (s != null) foreach (var sv in s.Squad) squad.Add(sv.Name);
            if (team == null) return new QuestSos { Size = squad.Count };
            var v = team.Judge(squad);
            if (team.HasRule)       // a quest without a team rule (kills, weapons, survive) has nothing to say on the squad
            {
                string said = (team.Quest.Length > 0 ? team.Quest : "the quest") + " (" + team.Words + ", team of " + squad.Count + "): " + v.Advice();
                if (said != _said) { _said = said; Plugin.Logger.LogInfo("[quest] " + said); }
            }
            return v;
        }
    }
}
