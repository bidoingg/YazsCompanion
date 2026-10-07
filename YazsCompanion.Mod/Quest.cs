// The active quest's objectives, read from the game for the advice (0.13.0, C1: the team rules, QuestTeam.cs; 0.14.0, C3: every
// other objective, QuestRules.cs - both pure). GameQuestManager.Get.ActiveQuest is the quest the player accepted (a runtime
// copy of its asset); its objectives list holds GameHubQuestObjectiveBase entries of several classes. Read once per quest and
// run (the next run, or another quest, reads again) and logged once:
//     [quest] GameHubQuest_Engineer_3 "Heroic Theory" - 3 objectives (objectives), all must hold: 1. Survive
//     (LiveAndOnRunFinished) the run's own goal - advice unchanged; 2. FinishWithWeapons (Live) requiredWeapons Plasma /
//     Laser / Blaster at level 4, in hand (any) -> lifts the Engineer's weapon line to max the tier-3 weapon; 3. ...
// then, whenever what the rules do changes (a rule met, the weapon line a step further, a health item taken), once:
//     [quest] GameHubQuest_Engineer_3 -> Taser to tier 3, max level: lifts the Engineer's weapon line ...; Engineer kills to 2000: ...
// A run that does not fit the quest's conditions cannot complete it: the game shows the quest box in every run, but when the
// quest would complete, QuestCompleted asks AreFulfillmentConditionsMet first (read in GameAssembly.dll of 1.0.2: the run's
// level, mode, difficulty and leader - GameplayMaster.level / currentGameMode / currentMainCharacterType - against the quest's
// arenaFulfillment / modeFulfillment / difficultyFulfillment / leaderFulfillment) and returns without completing it. Its rules
// are then logged, not followed. That check unreadable: the leader condition alone (IsLeaderFulfilled); that unreadable too:
// the rules are followed as read. [Advice] QuestSteer (0.14.0): InfoOnly keeps the QUEST row and the reasons in the log but
// moves no card (the team rule neither), Off follows nothing.
// What the advice follows (members checked against the 1.0.2 interop; what each objective's runtime counts was read in its
// machine code, research\review_1005\impl\c3; the 1.0.2 quests that use each in brackets):
//  - InternalNumSurvivors thresholds -> the team size (QuestTeam.Survivors) [Huntress_4 == 1, Ghost_3 == 2],
//  - FinishSpecificTeamSetup -> the classes the team must hold [Ghost_3],
//  - FullTeamTier3Weapons / TimedPowerupsFullTeam -> at least requiredTeamSize survivors [Mechanic_R2, Engineer_1]; the first
//    also lifts every survivor's weapon line to a tier-3 weapon at max level,
//  - FinishWithWeapons -> the weapon line of the weapons' class [Engineer_3, Mechanic_2: any tier-3 weapon at level 4 in
//    hand; Pyro_1: the tier-2 weapon at level 4], FinishWithAbilities [Pyro_1], FinishWithEvolutions [Ranger_3, Ranger_4],
//  - FinishWithoutClassAbilities [Pyro_4], FinishWithoutTier3Weapons [Pyro_RA] -> AVOID,
//  - FullyUpgradeClass [Engineer_4, Mechanic_R1, Tank_5], ClassActiveSynergies [Engineer_5],
//  - HealingItemCount [Medic_1] (B2), CollectItem (the game's own target test: +3, as since 0.10) [Engineer_2, Ghost_4, ...],
//  - GameStatisticThreshold CharacterKillsCurrentRun<class> [Engineer_3],
//  - StatisticThreshold on tag points, item slots, items held, evolutions, Rare+ trainings, armor [Ghost_5, Huntress_2/3,
//    Mechanic_1/4, Medic_5, Pyro_3/5/RB, Ranger_RB, Tank_RA/RB, Engineer_R1/R2], FullHealthTime [Medic_R2].
// Survive, SurviveTime, CompleteRun, kill counts of enemies, event counts and badge objectives are logged with their class and
// leave the advice unchanged. 0.15.0 (C15-09): the counted ones - CustomGameplayEvent (eventId, targetCount: the story quests),
// KillBossRushBoss, EventCount (targetEvent, targetCount), KillEnemyFilter (requireBoss, rankMask, targetCount) - are logged with
// their id and the runtime's own count (_count; _isBossKilled), a change of a count once, and the story kinds feed the readout's
// info-only QUEST row (QuestStory in QuestRules.cs; [General] QuestProgress). Every game member is read through a small NoInlining accessor inside a try, as in
// LoadoutState: a member a game patch took away costs that one reading, not the run.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using CT = GamePlayer.CharacterType;
using RuntimeList = Il2CppSystem.Collections.Generic.List<GameHubQuestObjectiveRuntimeBase>;

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

        // 0.14.0 (C3): the other objectives as rules for the cards and the QUEST row, each with the objective it was read from
        sealed class Def
        {
            public QuestRule Rule; public GameHubQuestObjectiveBase Obj; public IntPtr Ptr; public string Name = "";
            public PowerupBase Power;                 // FinishWithAbilities / FinishWithEvolutions: the ability asked for
            public GameStatisticValue Stat;           // GameStatisticThreshold: the counter it reads
            public CT Class = CT.None;                // FullyUpgradeClass: the class to upgrade
        }
        static QuestRules _rules;
        static readonly List<Def> _defs = new List<Def>();
        static readonly Dictionary<IntPtr, GameHubQuestObjectiveRuntimeBase> _runtimes = new Dictionary<IntPtr, GameHubQuestObjectiveRuntimeBase>();
        static readonly Dictionary<string, GameHubQuestObjectiveRuntimeBase> _runtimesByName = new Dictionary<string, GameHubQuestObjectiveRuntimeBase>();   // a copy the game made of an objective: by its asset name
        static int _runtimeCount = -1;
        static string _rulesSaid;                     // the key of the last "[quest] ... ->" line
        static bool _progressWarned, _healthPathSaid;
        static readonly Dictionary<IntPtr, bool> _healthItem = new Dictionary<IntPtr, bool>();     // items are assets: kept for the session

        // 0.15.0 (C15-09): the objectives the game counts and the advice does not follow (story events, the Boss Rush boss, event counts,
        // kills of a rank), each with the objective it was read from and its runtime once found
        sealed class CountDef { public QuestCount Count; public IntPtr Ptr; public string Name = ""; public GameHubQuestObjectiveRuntimeBase Runtime; }
        static QuestStory _story;
        static readonly List<CountDef> _counts = new List<CountDef>();
        static string _storySaid;                     // the key of the last "[quest] ... -> story ..." line
        static bool _countWarned;

        /// <summary>The HUD went away (the run ended), or the play clock went back (Try Again keeps the scene and the HUD): the
        /// next run reads the quest again and logs it once more.</summary>
        public static void Forget()
        {
            _quest = IntPtr.Zero; _none = false; _team = null; _said = null; _warned = false; _clock = -1f;
            _rules = null; _defs.Clear(); _runtimes.Clear(); _runtimesByName.Clear(); _runtimeCount = -1; _rulesSaid = null; _progressWarned = false; _healthPathSaid = false;
            _story = null; _counts.Clear(); _storySaid = null; _countWarned = false;
        }

        /// <summary>[Advice] QuestSteer as set now (On when the setting cannot be read).</summary>
        internal static QuestSteer Steer() { try { return Plugin.AdviceQuest.Value; } catch { return QuestSteer.On; } }
        static string MutedWhy(QuestSteer steer) { return steer == QuestSteer.On ? null : steer == QuestSteer.InfoOnly ? "the quest advice is set to Info only ([Advice] QuestSteer)" : "the quest advice is off ([Advice] QuestSteer)"; }

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
        // 0.14.0 (C3): the objectives' runtimes (built at the run's start) and what they count
        [MethodImpl(MethodImplOptions.NoInlining)] static RuntimeList Runtimes(GameHubQuestBase q) { return q._objectiveRuntimes; }
        [MethodImpl(MethodImplOptions.NoInlining)] static GameHubQuestObjectiveBase DefinitionOf(GameHubQuestObjectiveRuntimeBase rt) { return rt.definition; }
        [MethodImpl(MethodImplOptions.NoInlining)] static int CurrentOf(GameHubQuestObjectiveRuntimeBase rt) { return rt.CurrentValue; }
        [MethodImpl(MethodImplOptions.NoInlining)] static int TargetOf(GameHubQuestObjectiveRuntimeBase rt) { return rt.TargetValue; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool FulfilledOf(GameHubQuestObjectiveRuntimeBase rt) { return rt.IsFulfilled; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float FloatOf(GameHubQuestObjectiveRuntimeBase rt) { var a = rt.TryCast<GameHubQuestObjectiveStatisticThreshold.Runtime>(); return a != null ? a.CurrentFloatValue : float.NaN; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float StatValue(GameStatisticValue v) { return v.value; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool HealthRelated(ItemBase it) { return GameHubQuestObjectiveHealingItemCount.IsHealthRelatedItem(it); }
        [MethodImpl(MethodImplOptions.NoInlining)] static CT TargetClassOf(GameHubQuestObjectiveFullyUpgradeClass f) { return f.GetTargetClass(); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string Threshold(GameHubQuestObjectiveBase o, QuestTeam team, QuestRules rules)
        {
            var st = o.TryCast<GameHubQuestObjectiveStatisticThreshold>();
            if (st == null) return null;
            string stat = st.statisticType.ToString(), rule = stat + " " + st.comparison + " " + st.threshold.ToString("0.##", IC);
            if (st.statisticType != PlayerStatistic.EType.InternalNumSurvivors)
            {
                // 0.14.0 (C3): tag points, item slots, items held, evolutions, trainings, armor
                var r = QuestRules.FromStatistic(stat, (int)st.comparison, st.threshold, st.failPermanentlyWhenConditionNotMet);
                if (r == null) return rule + " -> advice unchanged";
                Add(rules, r, o);
                return rule + (st.failPermanentlyWhenConditionNotMet ? " (fails for good)" : "") + " -> " + QuestRules.Does(r);
            }
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
        static string FullTeam(GameHubQuestObjectiveBase o, QuestTeam team, QuestRules rules)
        {
            int n = -1; bool tier3 = false;
            var a = o.TryCast<GameHubQuestObjectiveFullTeamTier3Weapons>(); if (a != null) { n = a.requiredTeamSize; tier3 = true; }
            else { var b = o.TryCast<GameHubQuestObjectiveTimedPowerupsFullTeam>(); if (b != null) n = b.requiredTeamSize; }
            if (n < 0) return null;
            string weapons = "";
            if (tier3)
            {   // 0.14.0 (C3): every survivor's weapon line to a tier-3 weapon at max level ("_numWeaponsMaxed" counts them)
                var r = new QuestRule { Ask = QuestAsk.WeaponLine, Depth = 2, AllBranches = true, Level = 4, Need = n };
                Add(rules, r, o);
                weapons = "; " + QuestRules.Does(r);
            }
            if (n <= 1) return "requiredTeamSize " + n + " -> advice unchanged" + weapons;
            team.AtLeast(n);
            return "requiredTeamSize " + n + " -> a team of " + n + " (Liberate works against it)" + weapons;
        }

        // ---- 0.14.0 (C3): the other objectives
        static Def Add(QuestRules rules, QuestRule r, GameHubQuestObjectiveBase o)
        {
            rules.List.Add(r);
            var d = new Def { Rule = r, Obj = o }; try { d.Ptr = o.Pointer; } catch { } try { d.Name = Bare(G.Asset(o)); } catch { }
            _defs.Add(d);
            return d;
        }
        static string ClassName(PowerupBase p) { try { var cp = p == null ? null : p.targetClassProperties; return cp == null ? null : G.ClassName(cp.characterType); } catch { return null; } }
        static string Listed(QuestRule r) { return string.Join(" / ", r.Targets); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string Weapons(GameHubQuestObjectiveBase o, QuestTeam team, QuestRules rules)
        {
            var fw = o.TryCast<GameHubQuestObjectiveFinishWithWeapons>();
            if (fw == null) return null;
            var r = new QuestRule { Ask = QuestAsk.WeaponLine, All = fw.matchMode == GameHubQuestObjectiveMatchMode.All, Current = fw.requireCurrentWeapon };
            int level = int.MaxValue; CT cls = CT.None;
            foreach (var req in G.Each(fw.requiredWeapons))
            {
                var w = req == null ? null : req.weapon;
                if (w == null) continue;
                r.Targets.Add(G.Name(w));
                level = Math.Min(level, req.requiredLevel);
                r.Depth = Math.Max(r.Depth, Ranker.WeaponDepth(w.TryCast<WeaponUpgradePowerup>()));
                if (cls == CT.None) { try { var cp = w.targetClassProperties; if (cp != null) cls = cp.characterType; } catch { } }
            }
            if (r.Targets.Count == 0) return "requiredWeapons none readable - advice unchanged";
            r.Level = level == int.MaxValue ? 4 : level;
            if (cls != CT.None)
            {
                r.Class = G.ClassName(cls);
                // every weapon of that depth in the class's line named: any branch counts ("the tier-3 weapon")
                int at = 0, named = 0;
                var props = G.PropsOf(cls);
                if (props != null) foreach (var p in G.Each(props.weaponPowerups))
                {
                    var wu = p == null ? null : p.TryCast<WeaponUpgradePowerup>();
                    if (wu == null || Ranker.WeaponDepth(wu) != r.Depth) continue;
                    at++; string wn = G.Name(wu);
                    if (r.Targets.Any(t => Ranker.SameName(t, wn))) named++;
                }
                r.AllBranches = !r.All && at > 1 && named >= at;
                // a weapon not asked "in hand" counts for a recruit too (the leader holds every powerup); in hand, only the leader's
                if (!r.Current) team.Need(r.Class);
            }
            Add(rules, r, o);
            return "requiredWeapons " + Listed(r) + " at level " + r.Level + (r.Current ? ", in hand" : "") + " (" + (r.All ? "all" : "any") + ") -> " + QuestRules.Does(r);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string Abilities(GameHubQuestObjectiveBase o, QuestTeam team, QuestRules rules)
        {
            var fa = o.TryCast<GameHubQuestObjectiveFinishWithAbilities>();
            if (fa == null) return null;
            var r = new QuestRule { Ask = QuestAsk.Ability, All = fa.matchMode == GameHubQuestObjectiveMatchMode.All };
            int level = int.MaxValue; PowerupBase first = null;
            foreach (var req in G.Each(fa.requiredAbilities))
            {
                var a = req == null ? null : req.ability;
                if (a == null) continue;
                r.Targets.Add(G.Name(a)); level = Math.Min(level, req.requiredLevel);
                if (first == null) { first = a; r.Class = ClassName(a); }
            }
            if (r.Targets.Count == 0) return "requiredAbilities none readable - advice unchanged";
            r.Level = level == int.MaxValue ? 4 : level;
            if (r.Class != null) team.Need(r.Class);
            Add(rules, r, o).Power = first;
            return "requiredAbilities " + Listed(r) + " at level " + r.Level + " -> " + QuestRules.Does(r);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string Evolutions(GameHubQuestObjectiveBase o, QuestTeam team, QuestRules rules)
        {
            var fe = o.TryCast<GameHubQuestObjectiveFinishWithEvolutions>();
            if (fe == null) return null;
            var a = fe.requiredAbility;
            if (a == null) return "requiredAbility unreadable - advice unchanged";
            var r = new QuestRule { Ask = QuestAsk.Evolution, Class = ClassName(a) };
            r.Targets.Add(G.Name(a));
            if (r.Class != null) team.Need(r.Class);
            Add(rules, r, o).Power = a;
            return "requiredAbility " + r.Targets[0] + " (an evolution of it) -> " + QuestRules.Does(r);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string Without(GameHubQuestObjectiveBase o, QuestRules rules)
        {
            var fc = o.TryCast<GameHubQuestObjectiveFinishWithoutClassAbilities>();
            if (fc != null)
            {
                var r = new QuestRule { Ask = QuestAsk.NoClassAbility, Class = G.ClassName(fc.targetClass) };
                Add(rules, r, o);
                return "targetClass " + r.Class + " (fails for good at the pick) -> " + QuestRules.Does(r);
            }
            if (o.TryCast<GameHubQuestObjectiveFinishWithoutTier3Weapons>() != null)
            {
                var r = new QuestRule { Ask = QuestAsk.NoTier3 };
                Add(rules, r, o);
                return "no tier-3 weapon in hand at the end -> " + QuestRules.Does(r);
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string Upgrade(GameHubQuestObjectiveBase o, QuestTeam team, QuestRules rules, Snapshot s)
        {
            var fu = o.TryCast<GameHubQuestObjectiveFullyUpgradeClass>();
            if (fu != null)
            {
                CT cls = CT.None;
                try { cls = TargetClassOf(fu); } catch { }
                if (cls == CT.None || cls == CT.NumCharacters)
                {   // the game's own answer unreadable: the leader's class, or the class named
                    bool lead = false; try { lead = fu.useTeamLeaderClass; } catch { }
                    if (lead && s != null) { foreach (var sv in s.Squad) if (sv.Leader) cls = sv.Type; }
                    else { try { cls = fu.characterType; } catch { } }
                }
                if (cls == CT.None || cls == CT.NumCharacters) return "the class to upgrade unreadable - advice unchanged";
                var r = new QuestRule { Ask = QuestAsk.FullClass, Class = G.ClassName(cls) };
                team.Need(r.Class);
                Add(rules, r, o).Class = cls;
                bool banish = false; try { banish = fu.failWhenClassPowerupBanished; } catch { }
                return "class " + r.Class + (banish ? " (a banished " + r.Class + " powerup fails it: never banish one)" : "") + " -> " + QuestRules.Does(r);
            }
            var fs = o.TryCast<GameHubQuestObjectiveClassActiveSynergies>();
            if (fs != null)
            {
                var r = new QuestRule { Ask = QuestAsk.Synergies, Class = G.ClassName(fs.targetClass), Need = fs.requiredActiveSynergies };
                team.Need(r.Class);
                Add(rules, r, o);
                return "class " + r.Class + ", " + r.Need.ToString("0", IC) + " active synergies -> " + QuestRules.Does(r);
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string Counters(GameHubQuestObjectiveBase o, QuestRules rules)
        {
            var hc = o.TryCast<GameHubQuestObjectiveHealingItemCount>();
            if (hc != null)
            {   // B2: the game counts the health-related items the LEADER holds (isHealingItem, or the HealthRelated tag)
                var r = new QuestRule { Ask = QuestAsk.HealthItems, Need = Math.Max(1, hc.targetCount) };
                Add(rules, r, o);
                return "targetCount " + hc.targetCount + " (held by the leader) -> " + QuestRules.Does(r);
            }
            var ci = o.TryCast<GameHubQuestObjectiveCollectItem>();
            if (ci != null)
            {
                var r = new QuestRule { Ask = QuestAsk.Items, All = ci.matchMode == GameHubQuestObjectiveMatchMode.All };
                foreach (var it in G.Each(ci.targetItems)) if (it != null) r.Targets.Add(G.Name(it));
                Add(rules, r, o);
                return "targetItems " + (r.Targets.Count > 0 ? Listed(r) : "(none read)") + " -> " + QuestRules.Does(r);
            }
            var gs = o.TryCast<GameHubQuestObjectiveGameStatisticThreshold>();
            if (gs != null)
            {
                var v = gs.statisticValue; string asset = v == null ? "" : G.Asset(v);
                string rule = (asset.Length > 0 ? asset : "?") + " " + gs.comparison + " " + gs.threshold.ToString("0.##", IC);
                var r = QuestRules.FromGameStatistic(asset, (int)gs.comparison, gs.threshold);
                if (r == null) return rule + " -> advice unchanged";
                Add(rules, r, o).Stat = v;
                return rule + " -> " + QuestRules.Does(r);
            }
            var fh = o.TryCast<GameHubQuestObjectiveFullHealthTime>();
            if (fh != null)
            {
                var r = new QuestRule { Ask = QuestAsk.FullHealth, Need = Math.Round(fh.targetMinutes * 60.0) };
                Add(rules, r, o);
                return fh.targetMinutes.ToString("0.#", IC) + " min at " + (fh.fullHealthThreshold * 100f).ToString("0", IC) + "% health -> " + QuestRules.Does(r);
            }
            if (o.TryCast<GameHubQuestObjectiveSurvive>() != null || o.TryCast<GameHubQuestObjectiveSurviveTime>() != null || o.TryCast<GameHubQuestObjectiveCompleteRun>() != null)
                return "the run's own goal - advice unchanged";
            return null;
        }

        // ---- 0.15.0 (C15-09): the counted objectives (fields and runtimes as the 1.0.2 interop spells them; each read in its own try)
        [MethodImpl(MethodImplOptions.NoInlining)] static string EventIdOf(GameHubQuestObjectiveCustomGameplayEvent o) { return o.eventId; }
        [MethodImpl(MethodImplOptions.NoInlining)] static int TargetCountOf(GameHubQuestObjectiveCustomGameplayEvent o) { return o.targetCount; }
        [MethodImpl(MethodImplOptions.NoInlining)] static string TargetEventOf(GameHubQuestObjectiveEventCount o) { return o.targetEvent.ToString(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static int TargetCountOf(GameHubQuestObjectiveEventCount o) { return o.targetCount; }
        [MethodImpl(MethodImplOptions.NoInlining)] static int TargetCountOf(GameHubQuestObjectiveKillEnemyFilter o) { return o.targetCount; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool RequireBossOf(GameHubQuestObjectiveKillEnemyFilter o) { return o.requireBoss; }
        [MethodImpl(MethodImplOptions.NoInlining)] static int RankMaskOf(GameHubQuestObjectiveKillEnemyFilter o) { return (int)o.rankMask; }
        // the runtime's own count: _count (the boss: _isBossKilled), else the base class's CurrentValue
        [MethodImpl(MethodImplOptions.NoInlining)]
        static int CountOf(CountKind kind, GameHubQuestObjectiveRuntimeBase rt)
        {
            switch (kind)
            {
                case CountKind.Story: { var a = rt.TryCast<GameHubQuestObjectiveCustomGameplayEvent.Runtime>(); if (a != null) return a._count; break; }
                case CountKind.BossRushBoss: { var a = rt.TryCast<GameHubQuestObjectiveKillBossRushBoss.Runtime>(); if (a != null) return a._isBossKilled ? 1 : 0; break; }
                case CountKind.Event: { var a = rt.TryCast<GameHubQuestObjectiveEventCount.Runtime>(); if (a != null) return a._count; break; }
                case CountKind.Kills: { var a = rt.TryCast<GameHubQuestObjectiveKillEnemyFilter.Runtime>(); if (a != null) return a._count; break; }
            }
            return rt.CurrentValue;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string Counted(GameHubQuestObjectiveBase o, QuestStory story)
        {
            QuestCount c = null;
            var ce = o.TryCast<GameHubQuestObjectiveCustomGameplayEvent>();
            if (ce != null)
            {
                string id = null; int n = 1;
                try { id = EventIdOf(ce); } catch { }
                try { n = TargetCountOf(ce); } catch { }
                c = QuestStory.Decode("CustomGameplayEvent", n, eventId: id);
            }
            else if (o.TryCast<GameHubQuestObjectiveKillBossRushBoss>() != null) c = QuestStory.Decode("KillBossRushBoss", 1);
            else
            {
                var ec = o.TryCast<GameHubQuestObjectiveEventCount>();
                if (ec != null)
                {
                    string ev = null; int n = 1;
                    try { ev = TargetEventOf(ec); } catch { }
                    try { n = TargetCountOf(ec); } catch { }
                    c = QuestStory.Decode("EventCount", n, targetEvent: ev);
                }
                else
                {
                    var kf = o.TryCast<GameHubQuestObjectiveKillEnemyFilter>();
                    if (kf != null)
                    {
                        int n = 1, mask = 15; bool boss = false;
                        try { n = TargetCountOf(kf); } catch { }
                        try { boss = RequireBossOf(kf); } catch { }
                        try { mask = RankMaskOf(kf); } catch { }
                        c = QuestStory.Decode("KillEnemyFilter", n, requireBoss: boss, rankMask: mask);
                    }
                }
            }
            if (c == null) return null;
            var d = new CountDef { Count = c };
            try { d.Ptr = o.Pointer; } catch { }
            try { d.Name = Bare(G.Asset(o)); } catch { }
            _counts.Add(d); story.Counts.Add(c);
            ReadCount(d);                 // the count at the run's start, when the runtimes are built already
            return QuestStory.LogText(c);
        }

        // one counted objective's progress from its runtime (found once, by the objective it runs; looked for again while not found)
        static void ReadCount(CountDef d)
        {
            if (d.Runtime == null)
            {
                GameHubQuestObjectiveRuntimeBase rt;
                if (d.Ptr != IntPtr.Zero && _runtimes.TryGetValue(d.Ptr, out rt)) d.Runtime = rt;
                else if (d.Name.Length > 0 && _runtimesByName.TryGetValue(d.Name, out rt)) d.Runtime = rt;
            }
            var r = d.Runtime;
            if (r == null) { d.Count.Have = double.NaN; d.Count.Done = null; return; }
            try { d.Count.Have = CountOf(d.Count.Kind, r); } catch { d.Count.Have = double.NaN; }
            d.Count.Done = Fulfilled(r);
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
                if (_quest != IntPtr.Zero || (!_none && s != null && s.Squad.Count > 0)) { _none = true; _quest = IntPtr.Zero; _team = null; _rules = null; _defs.Clear(); _story = null; _counts.Clear(); _said = null; Plugin.Logger.LogInfo("[quest] no active quest this run - the rescue advice follows the squad and the clock alone"); }
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
                    _team.Muted = MutedWhy(Steer());
                }
                return _team;
            }
            _quest = p; _none = false; _said = null;
            _team = Read(q, s);
            _team.Muted = MutedWhy(Steer());
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
                said = "the run does NOT fit the quest's conditions (arena, mode, difficulty or leader" + lead + ") - the quest cannot complete this run, its rules are not followed";
                return "this run does not fit the quest's conditions (arena, mode, difficulty or leader" + lead + ") - the quest cannot complete this run";
            }
            if (leaderOk == false)
            {
                said = "the run's conditions unreadable; the leader, " + who + ", does NOT fit the quest's leader condition - the quest cannot complete this run, its rules are not followed";
                return "the leader (" + who + ") does not fit the quest's leader condition - the quest cannot complete this run";
            }
            said = "the run's conditions unreadable" + (leaderOk == true ? "; leader " + who + " fits" : "") + " - the rules are followed as read";
            return null;
        }

        static QuestTeam Read(GameHubQuestBase q, Snapshot s)
        {
            var team = new QuestTeam();
            var rules = new QuestRules();
            _rules = rules; _defs.Clear(); _runtimes.Clear(); _runtimesByName.Clear(); _runtimeCount = -1; _rulesSaid = null;
            var story = new QuestStory(); _story = story; _counts.Clear(); _storySaid = null;       // 0.15.0 (C15-09)
            var sb = new StringBuilder("[quest] ");
            try
            {
                team.Quest = G.Asset(q); rules.Quest = team.Quest; story.Quest = team.Quest;
                try { MapRuntimes(q); } catch { }       // 0.15.0 (C15-09): the counted objectives' counts at the run's start
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
                    try { what = Threshold(o, team, rules); } catch (Exception e) { what = "unreadable (" + e.GetType().Name + ") - advice unchanged"; }
                    if (what == null) { try { what = TeamSetup(o, team); } catch (Exception e) { what = "unreadable (" + e.GetType().Name + ") - advice unchanged"; } }
                    if (what == null) { try { what = FullTeam(o, team, rules); } catch (Exception e) { what = "unreadable (" + e.GetType().Name + ") - advice unchanged"; } }
                    if (what == null) { try { what = Weapons(o, team, rules); } catch (Exception e) { what = "unreadable (" + e.GetType().Name + ") - advice unchanged"; } }
                    if (what == null) { try { what = Abilities(o, team, rules); } catch (Exception e) { what = "unreadable (" + e.GetType().Name + ") - advice unchanged"; } }
                    if (what == null) { try { what = Evolutions(o, team, rules); } catch (Exception e) { what = "unreadable (" + e.GetType().Name + ") - advice unchanged"; } }
                    if (what == null) { try { what = Without(o, rules); } catch (Exception e) { what = "unreadable (" + e.GetType().Name + ") - advice unchanged"; } }
                    if (what == null) { try { what = Upgrade(o, team, rules, s); } catch (Exception e) { what = "unreadable (" + e.GetType().Name + ") - advice unchanged"; } }
                    if (what == null) { try { what = Counters(o, rules); } catch (Exception e) { what = "unreadable (" + e.GetType().Name + ") - advice unchanged"; } }
                    if (what == null) { try { what = Counted(o, story); } catch (Exception e) { what = "unreadable (" + e.GetType().Name + ") - advice unchanged"; } }
                    if (what == null) what = "advice unchanged";
                    sb.Append(' ').Append(i).Append(". ").Append(cls.Replace("GameHubQuestObjective", "")).Append(timing.Length > 0 ? " (" + timing + ")" : "").Append(" ").Append(what).Append(';');
                }
                if (count == 0) sb.Append(" none readable - advice unchanged;");
                string fit; team.NotThisRun = NotThisRun(q, s, out fit);
                team.Muted = MutedWhy(Steer());
                sb.Append(' ').Append(fit).Append(';');
                sb.Append(" | team rule: ").Append(team.Rules ? team.Words : team.HasRule ? team.Words + ", not followed (" + (team.AnyOf ? "objectives match Any" : team.Failed ? "failed already" : team.NotThisRun != null ? "this run does not fit the quest" : team.Muted) + ")" : "none");
                if (rules.List.Count > 0) sb.Append(" | rules for the cards: ").Append(rules.List.Count);
                if (story.Counts.Count > 0) sb.Append(" | counted: ").Append(story.Counts.Count).Append(story.Counts.Any(c => c.Story) ? " (story progress on the readout)" : "");
            }
            catch (Exception e) { sb.Append(" - not read: ").Append(e.GetType().Name).Append(' ').Append(e.Message); }
            if (story.Counts.Count == 0) _story = null;
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

        // ================================================================ 0.14.0 (C3): the rules for the cards, with their progress
        /// <summary>The active quest's rules for the cards and the QUEST row, their progress read for this snapshot (null: no
        /// quest, or no objective the cards follow). A change in what they do is logged once ('[quest] ... -> ...').</summary>
        public static QuestRules Rules(Snapshot s)
        {
            QuestTeam team = null;
            try { team = Team(s); }
            catch (Exception e) { if (!_warned) { _warned = true; Plugin.Logger.LogWarning("[quest] not read: " + e.GetType().Name + " " + e.Message + " (said once a run)"); } }
            var rules = _rules;
            if (team == null || rules == null) return null;
            var steer = Steer();
            rules.Steer = steer; rules.AnyOf = team.AnyOf; rules.Failed = team.Failed; rules.NotThisRun = team.NotThisRun;
            rules.TeamWords = steer == QuestSteer.InfoOnly && team.HasRule && !team.AnyOf && !team.Failed && team.NotThisRun == null ? team.Words : null;
            if (rules.List.Count == 0 && rules.TeamWords == null) return null;
            try { Progress(rules, s); }
            catch (Exception e) { if (!_progressWarned) { _progressWarned = true; Plugin.Logger.LogWarning("[quest] progress not read: " + e.GetType().Name + " " + e.Message + " - the rules as first read (said once a run)"); } }
            string key = rules.Key();
            if (key != _rulesSaid)
            {
                _rulesSaid = key;
                Plugin.Logger.LogInfo("[quest] " + (rules.Quest.Length > 0 ? rules.Quest : "the quest") + " -> " + rules.Said());
            }
            return rules;
        }

        // ================================================================ 0.15.0 (C15-09): what the game counts, progress only
        /// <summary>The active quest's counted objectives (story events, the Boss Rush boss, event counts, kills of a rank) with the
        /// counts the game keeps, read for this snapshot (null: no quest, or nothing counted in it). A change of a count is logged once
        /// ('[quest] GameHubQuest_Main_06 -> story objective main_story_objective_q6: 3 of 5 - progress only'). No card moves for it.</summary>
        public static QuestStory Story(Snapshot s)
        {
            QuestTeam team = null;
            try { team = Team(s); }
            catch (Exception e) { if (!_warned) { _warned = true; Plugin.Logger.LogWarning("[quest] not read: " + e.GetType().Name + " " + e.Message + " (said once a run)"); } }
            var story = _story;
            if (team == null || story == null || story.Counts.Count == 0) return null;
            story.Failed = team.Failed; story.NotThisRun = team.NotThisRun;
            try
            {
                GameHubQuestBase q = null; try { q = Active(); } catch { }
                if (q != null) MapRuntimes(q);
                foreach (var d in _counts) ReadCount(d);
            }
            catch (Exception e) { if (!_countWarned) { _countWarned = true; Plugin.Logger.LogWarning("[quest] counts not read: " + e.GetType().Name + " " + e.Message + " (said once a run)"); } }
            string key = story.Key();
            if (key != _storySaid)
            {
                _storySaid = key;
                Plugin.Logger.LogInfo("[quest] " + (story.Quest.Length > 0 ? story.Quest : "the quest") + " -> " + story.Said());
            }
            return story;
        }

        /// <summary>For the readout's cheap fingerprint (G.QuickKey, every two seconds): the story objectives' counts as they stand, so a
        /// story step rebuilds the QUEST row. The runtimes found by the last Story(); 0 = nothing to count. Never throws.</summary>
        public static long StoryHash()
        {
            try
            {
                if (_story == null || _counts.Count == 0) return 0;
                long h = 17;
                foreach (var d in _counts)
                {
                    if (!d.Count.Story || d.Runtime == null) continue;
                    int n = -1; try { n = CountOf(d.Count.Kind, d.Runtime); } catch { }
                    unchecked { h = h * 31 + n + 2; }
                }
                return h;
            }
            catch { return 0; }
        }

        /// <summary>An item the health-item objective counts (the game's own static test; null: unreadable).</summary>
        internal static bool? IsHealthItem(ItemBase it)
        {
            if (it == null) return null;
            IntPtr key = IntPtr.Zero; try { key = it.Pointer; } catch { }
            bool v;
            if (key != IntPtr.Zero && _healthItem.TryGetValue(key, out v)) return v;
            try { v = HealthRelated(it); }
            catch { return null; }
            if (key != IntPtr.Zero) _healthItem[key] = v;
            return v;
        }

        // the runtimes by the objective they run (built at the run's start; asked again while their number changes)
        static void MapRuntimes(GameHubQuestBase q)
        {
            RuntimeList list = null; try { list = Runtimes(q); } catch { }
            int n = 0; try { n = list == null ? 0 : list.Count; } catch { }
            if (n == _runtimeCount && _runtimes.Count > 0) return;
            _runtimeCount = n; _runtimes.Clear(); _runtimesByName.Clear();
            foreach (var cd in _counts) cd.Runtime = null;          // 0.15.0 (C15-09): found again in the new list
            foreach (var rt in G.Each(list))
            {
                if (rt == null) continue;
                try { var d = DefinitionOf(rt); if (d != null) { _runtimes[d.Pointer] = rt; string an = Bare(G.Asset(d)); if (an.Length > 0) _runtimesByName[an] = rt; } } catch { }
            }
        }

        static string Bare(string asset) { return (asset ?? "").Replace("(Clone)", "").Trim(); }
        static GameHubQuestObjectiveRuntimeBase RuntimeOf(Def d)
        {
            GameHubQuestObjectiveRuntimeBase rt;
            if (d.Ptr != IntPtr.Zero && _runtimes.TryGetValue(d.Ptr, out rt)) return rt;
            return d.Name.Length > 0 && _runtimesByName.TryGetValue(d.Name, out rt) ? rt : null;
        }
        static bool? Fulfilled(GameHubQuestObjectiveRuntimeBase rt) { if (rt == null) return null; try { return FulfilledOf(rt); } catch { return null; } }
        static double Current(GameHubQuestObjectiveRuntimeBase rt) { if (rt == null) return double.NaN; try { return CurrentOf(rt); } catch { return double.NaN; } }
        static double Target(GameHubQuestObjectiveRuntimeBase rt) { if (rt == null) return double.NaN; try { return TargetOf(rt); } catch { return double.NaN; } }
        static double Float(GameHubQuestObjectiveRuntimeBase rt) { if (rt == null) return double.NaN; try { return FloatOf(rt); } catch { return double.NaN; } }

        static void Progress(QuestRules rules, Snapshot s)
        {
            GameHubQuestBase q = null; try { q = Active(); } catch { }
            if (q != null) MapRuntimes(q);
            rules.Points.Clear();
            if (s != null) foreach (var kv in s.Tags.Points) rules.Points[kv.Key] = kv.Value;
            foreach (var d in _defs)
            {
                var r = d.Rule; var rt = RuntimeOf(d);
                r.Met = false; r.Unreachable = null;
                try { One(d, r, rt, s); }
                catch (Exception e) { if (!_progressWarned) { _progressWarned = true; Plugin.Logger.LogWarning("[quest] progress of " + r.Ask + " not read: " + e.GetType().Name + " " + e.Message + " (said once a run)"); } }
            }
        }

        static void One(Def d, QuestRule r, GameHubQuestObjectiveRuntimeBase rt, Snapshot s)
        {
            switch (r.Ask)
            {
                case QuestAsk.WeaponLine: WeaponLine(r, rt, s); break;
                case QuestAsk.Ability:
                    {
                        var sv = s == null || r.Class == null ? null : s.Squad.FirstOrDefault(x => x.Name == r.Class);
                        r.FromLevel = sv == null || d.Power == null ? 0 : sv.LevelOf(d.Power);
                        r.FromMax = d.Power == null ? 4 : G.MaxLevel(d.Power);
                        r.Met = Fulfilled(rt) ?? (r.FromLevel >= r.Level);
                        break;
                    }
                case QuestAsk.Evolution:
                    {
                        var a = d.Power;
                        PowerupBase ea = null, eb = null; try { ea = a.abilityEvolutionA; eb = a.abilityEvolutionB; } catch { }
                        bool open = false;
                        foreach (var e in new[] { ea, eb })
                        {
                            if (e == null) continue;
                            SkillTreeUpgradeBase node = null; try { node = e.skillTreeRequirement; } catch { }
                            if (node == null || G.NodeOwned(node)) open = true;
                        }
                        var sv = s == null || r.Class == null ? null : s.Squad.FirstOrDefault(x => x.Name == r.Class);
                        r.Met = Fulfilled(rt) ?? (sv != null && a != null && sv.EvolutionOf(a) != null);
                        if (!r.Met && !open) r.Unreachable = "its evolutions are locked in the Skill Tree";
                        break;
                    }
                case QuestAsk.FullClass: FullClass(d, r, rt, s); break;
                case QuestAsk.Synergies:
                    {
                        double have = Current(rt), need = Target(rt);
                        if (!double.IsNaN(have)) r.Have = have;
                        if (!double.IsNaN(need) && need > 0) r.Need = need;
                        r.Met = Fulfilled(rt) ?? (!double.IsNaN(r.Have) && r.Have >= r.Need);
                        break;
                    }
                case QuestAsk.HealthItems:
                    {
                        double have = Current(rt), need = Target(rt); string path = "the objective's own count";
                        if (double.IsNaN(have) && s != null)
                        {   // fallback: count what the leader holds with the game's own test
                            int n = 0; bool read = true;
                            foreach (var sv in s.Squad) if (sv.Leader) foreach (var kv in sv.Items) { var h = IsHealthItem(kv.Key); if (h == null) read = false; else if (h.Value) n++; }
                            if (read) { have = n; path = "the leader's items counted here"; }
                        }
                        if (!_healthPathSaid) { _healthPathSaid = true; Plugin.Logger.LogInfo("[quest] health items read from " + (double.IsNaN(have) ? "nowhere (unreadable)" : path)); }
                        r.Have = have;
                        if (!double.IsNaN(need) && need > 0) r.Need = need;
                        r.Met = !double.IsNaN(r.Have) && r.Have >= r.Need;
                        break;
                    }
                case QuestAsk.Items:
                    {
                        r.Have = Current(rt); double need = Target(rt); if (!double.IsNaN(need) && need > 0) r.Need = need;
                        r.Met = Fulfilled(rt) ?? (!double.IsNaN(r.Have) && r.Need > 0 && r.Have >= r.Need);
                        break;
                    }
                case QuestAsk.Kills:
                    {
                        double have = double.NaN;
                        if (d.Stat != null) { try { have = StatValue(d.Stat); } catch { } }
                        r.Have = have;
                        r.Met = Fulfilled(rt) ?? (!double.IsNaN(have) && have >= r.Need);
                        break;
                    }
                case QuestAsk.TagPoints: case QuestAsk.TypesAt10: case QuestAsk.FillSlots: case QuestAsk.Evolutions: case QuestAsk.RareTraining: case QuestAsk.Armor:
                    {
                        double have = Float(rt);
                        if (double.IsNaN(have) && s != null)
                        {   // the tag counts from the snapshot when the objective's own value is not there
                            if (r.Ask == QuestAsk.TagPoints) have = r.Targets.Sum(t => s.Tags.PointsOf(t));
                            else if (r.Ask == QuestAsk.TypesAt10) have = s.Tags.Points.Count(kv => kv.Value >= 10);
                        }
                        r.Have = have;
                        if (double.IsNaN(have)) break;
                        if (r.Ask == QuestAsk.FillSlots) r.Met = have <= 0;
                        else if (r.Ask == QuestAsk.TagPoints && r.Below) { if (have >= r.Need) r.Unreachable = "past the cap"; }
                        else r.Met = have >= r.Need;
                        break;
                    }
                case QuestAsk.FullHealth:
                    {
                        r.Have = Current(rt); double need = Target(rt); if (!double.IsNaN(need) && need > 0) r.Need = need;
                        r.Met = Fulfilled(rt) ?? (!double.IsNaN(r.Have) && r.Have >= r.Need);
                        break;
                    }
            }
        }

        // the weapon line: where the class stands (the weapon in hand), whether it holds, whether it still can
        static void WeaponLine(QuestRule r, GameHubQuestObjectiveRuntimeBase rt, Snapshot s)
        {
            if (s == null) return;
            if (r.Class == null)
            {   // a tier-3 weapon at max level for each survivor of a full team
                r.Done.Clear();
                foreach (var sv in s.Squad)
                {
                    var cur = Ranker.CurrentStep(sv, Ranker.WeaponPath(sv));
                    if (cur != null && cur.Depth >= 2 && cur.Level >= Math.Max(1, r.Level)) r.Done.Add(sv.Name);
                }
                r.Have = r.Done.Count;
                r.Met = Fulfilled(rt) ?? (r.Have >= r.Need);
                return;
            }
            var who = s.Squad.FirstOrDefault(x => x.Name == r.Class);
            r.From = null; r.FromDepth = -1; r.FromLevel = 0; r.FromMax = 0;
            Ranker.WStep at = null; List<Ranker.WStep> path = null;
            if (who != null)
            {
                path = Ranker.WeaponPath(who);
                at = Ranker.CurrentStep(who, path);
                if (at != null) { r.From = G.Name(at.W); r.FromLevel = at.Level; r.FromMax = G.MaxLevel(at.W); r.FromDepth = at.Depth; }
            }
            bool counts = at != null && at.Depth == r.Depth && (r.AllBranches || r.Targets.Any(t => Ranker.SameName(t, G.Name(at.W))));
            bool own = r.Current ? counts && at.Level >= r.Level
                : path != null && path.Any(x => x.Depth == r.Depth && x.Level >= r.Level && (r.AllBranches || r.Targets.Any(t => Ranker.SameName(t, G.Name(x.W)))));
            r.Met = Fulfilled(rt) ?? own;
            if (r.Met) return;
            if (r.Current && who == null) r.Unreachable = "no " + r.Class + " on the squad - the quest counts the leader's weapon in hand";
            else if (r.Current && !who.Leader) r.Unreachable = "the quest counts the leader's weapon in hand, the " + r.Class + " is a recruit";
            else if (at != null && at.Depth == r.Depth && !counts) r.Unreachable = "the " + r.Class + " took " + G.Name(at.W) + ", another branch";
            else if (at != null && at.Depth > r.Depth && r.Current) r.Unreachable = "the " + r.Class + " is past it";
        }

        // every ability of the class unlocked in the Skill Tree at its last level (evolved where its evolution is unlocked) and a
        // weapon of the class's top tier maxed - as the game's BuildUpgradeTargets / UpdateState count them
        static void FullClass(Def d, QuestRule r, GameHubQuestObjectiveRuntimeBase rt, Snapshot s)
        {
            var sv = s == null ? null : s.Find(d.Class);
            var props = sv != null ? sv.Props : G.PropsOf(d.Class);
            int need = 0, done = 0;
            if (props != null)
            {
                foreach (var a in G.Each(props.abilityBasePowerups))
                {
                    if (a == null || !Available(a)) continue;
                    need++;
                    PowerupBase ea = null, eb = null; try { ea = a.abilityEvolutionA; eb = a.abilityEvolutionB; } catch { }
                    bool evolvable = (ea != null && Available(ea)) || (eb != null && Available(eb));
                    if (sv != null && (evolvable ? sv.EvolutionOf(a) != null : sv.LevelOf(a) >= G.MaxLevel(a))) done++;
                }
                int top = -1;
                foreach (var p in G.Each(props.weaponPowerups)) { var w = p == null ? null : p.TryCast<WeaponUpgradePowerup>(); if (w != null && Available(w)) top = Math.Max(top, Ranker.WeaponDepth(w)); }
                if (top >= 0)
                {
                    need++;
                    if (sv != null) foreach (var p in G.Each(props.weaponPowerups)) { var w = p == null ? null : p.TryCast<WeaponUpgradePowerup>(); if (w != null && Ranker.WeaponDepth(w) == top && sv.LevelOf(w) >= G.MaxLevel(w)) { done++; break; } }
                }
            }
            if (need > 0) { r.Have = done; r.Need = need; }
            r.Met = Fulfilled(rt) ?? (need > 0 && done >= need);
        }
        static bool Available(PowerupBase p) { SkillTreeUpgradeBase node = null; try { node = p.skillTreeRequirement; } catch { } return node == null || G.NodeOwned(node); }
    }
}
