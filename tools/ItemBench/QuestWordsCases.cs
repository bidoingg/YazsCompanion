// 0.16.1 (C16-04a; the user's Q38 of 10-10: yes): Q6a - every counted quest objective of game 1.0.2 in the words of the game's own quest
// box, the PLAN readout's QUEST rows (QuestStory in QuestRules.cs). Up to 0.16.0 only the story kinds had a row, in the Companion's
// words ("QUEST  story objective 0 of 5" live 10-10 under Genesis, whose quest box reads "Broken Vials 0/5"); the 14 EventCount and 2
// KillEnemyFilter quests were logged and nothing more. The table: research\roadmap_1007\impl_queue_r1.json C16-04a section A (objectives:
// research\review_1005\impl\c3\quest_objectives_all_out.txt; labels: the I2 terms Quests/ObjShort_* in roadmap_1007\scratch\c16_quests\
// loc_quests.json; GameEvents: enums_out.txt:2-54). Part of QuestCases (section 12 of the bench). Game names only (the repository is public).
using System;
using System.Collections.Generic;
using System.Linq;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static partial class QuestCases
    {
        // the 16 counted quests of 1.0.2: quest, class (EventCount / KillEnemyFilter), targetCount, event (or null), requireBoss, rank mask,
        // the quest box's words, the readout's step
        sealed class Counted
        {
            public string Quest, Cls, Event, Label; public int Need, Mask = 15, Step; public bool Boss;
            public Counted(string quest, string cls, int need, string ev, bool boss, int mask, string label, int step)
            { Quest = quest; Cls = cls; Need = need; Event = ev; Boss = boss; Mask = mask; Label = label; Step = step; }
        }
        static readonly Counted[] CountedQuests =
        {
            new Counted("GameHubQuest_Ghost_1", "EventCount", 3, "RerollItemChecOncePerView", false, 15, "Rerolled Item Chests", 1),
            new Counted("GameHubQuest_Ghost_2", "EventCount", 3, "SOSLiberate", false, 15, "Liberate Survivors", 1),
            new Counted("GameHubQuest_Ghost_R2", "KillEnemyFilter", 1, null, true, 4, "Defeat Elite Bosses", 1),
            new Counted("GameHubQuest_Huntress_1", "EventCount", 7, "ChestOpened", false, 15, "Open Item Chests", 1),
            new Counted("GameHubQuest_Mechanic_5", "KillEnemyFilter", 3, null, true, 4, "Defeat Elite Bosses", 1),
            new Counted("GameHubQuest_Medic_2", "EventCount", 30, "MoneyCollected", false, 15, "Collect Cash", 3),
            new Counted("GameHubQuest_Medic_3", "EventCount", 5, "SOSLiberate", false, 15, "Liberate Survivors", 1),
            new Counted("GameHubQuest_Medic_R1", "EventCount", 1, "HashtagEventFinished", false, 15, "Complete a Research Pod Event", 1),
            new Counted("GameHubQuest_SWAT_1", "EventCount", 5, "SOSReacted", false, 15, "SOS Signals", 1),
            new Counted("GameHubQuest_SWAT_2", "EventCount", 10, "PowerupMilitaryPointCollected", false, 15, "Military Training Points", 1),
            new Counted("GameHubQuest_SWAT_3", "EventCount", 20, "EliteKilled", false, 15, "Defeat Elites", 2),
            new Counted("GameHubQuest_SWAT_4", "EventCount", 3, "SOSLiberate", false, 15, "Liberate Survivors", 1),
            new Counted("GameHubQuest_SWAT_RA", "EventCount", 5, "SOSLiberate", false, 15, "Liberate Survivors", 1),
            new Counted("GameHubQuest_SWAT_RB", "EventCount", 20, "PowerupMilitaryPointCollected", false, 15, "Military Training Points", 2),
            new Counted("GameHubQuest_Tank_1", "EventCount", 3, "PowerupKillingFrenzyCollected", false, 15, "Pick up Killing Frenzy", 1),
            new Counted("GameHubQuest_Tank_2", "EventCount", 30, "BossKilled", false, 15, "Defeat Bosses", 3),
        };
        // GameEventsManager.GameEvents of 1.0.2, values 0 - 52 (roadmap_1007\scratch\c16_quests\enums_out.txt:2-54)
        static readonly string[] GameEvents =
        {
            "LevelUp", "LevelUpChosen", "GameDefeat", "GameVictory", "PlayerGotHit", "TrainingPointReceived", "CriticalHit", "PowerupCollected", "MagnetCollected", "LowHP",
            "MoneyCollected", "HealthCollected", "BossKilled", "ItemChosen", "CharacterChosen", "EliteKilled", "AbilityUsed", "BossOrEliteKilled", "ZombieKilled", "SuccessfulDodge",
            "SuccessfulParry", "SkipLevelUp", "SkipMilitaryTraining", "SkipItemChest", "PowerupKillingFrenzyCollected", "PowerupArmorCollected", "PowerupSpeedCollected",
            "PowerupMilitaryPointCollected", "HPValueChanged", "WeaponCriticalHit", "AbilityCriticalHit", "SkipSOSSignal", "PlayerReceiveDamage", "WeaponChanged", "AbilityUpgraded",
            "HashtagAdded", "HashtagEventFinished", "HashtagEventActivated", "HashtagEventCollected", "NewPlayerSpawned", "SkipHashtagEvent", "ItemSwap", "SOSReacted",
            "MilitaryTrainingReceived", "RareMaterialCollected", "SOSLiberate", "GameplayReset", "RerollItemChest", "ItemRemoved", "ChestOpened", "NewItemChosen",
            "PowerupKillingFrenzyPickedUp", "RerollItemChecOncePerView",
        };

        static QuestCount DecodeCounted(Counted q)
        {
            return q.Cls == "EventCount" ? QuestStory.Decode("GameHubQuestObjectiveEventCount", q.Need, targetEvent: q.Event, quest: q.Quest)
                : QuestStory.Decode("GameHubQuestObjectiveKillEnemyFilter", q.Need, requireBoss: q.Boss, rankMask: q.Mask, quest: q.Quest);
        }
        static bool RowOk(string t) { return t != null && t.Length > 0 && t.Length <= QuestStory.RowRoom && t.IndexOf('<') < 0 && t.All(ch => ch >= ' ' && ch <= '~'); }

        static void CountedWords()
        {
            Console.WriteLine("  -- Q6a 0.16.1 (C16-04a): every counted objective of 1.0.2 in the game's own words");
            // (a) the 16 quests: kind, the quest box's words, step, the row unread / at 0 / at 1 / met
            var bad = new List<string>();
            foreach (var q in CountedQuests)
            {
                var c = DecodeCounted(q);
                if (c == null) { bad.Add(q.Quest + ": not decoded"); continue; }
                var kind = q.Cls == "EventCount" ? CountKind.Event : CountKind.Kills;
                c.Have = double.NaN; string unread = QuestStory.RowText(c);
                c.Have = 0; string r0 = QuestStory.RowText(c);
                c.Have = 1; string r1 = QuestStory.RowText(c);
                c.Have = q.Need; string done = QuestStory.RowText(c);
                string want1 = q.Need == 1 ? q.Label + ", done" : q.Label + " 1 of " + q.Need;
                if (c.Kind != kind || c.Story || c.Label != q.Label || c.Step != q.Step || unread != q.Label + " (needs " + q.Need + ")" || r0 != q.Label + " 0 of " + q.Need
                    || r1 != want1 || done != q.Label + ", done" || !new[] { unread, r0, r1, done }.All(RowOk))
                    bad.Add(q.Quest + ": " + c.Kind + " '" + c.Label + "' step " + c.Step + " | '" + unread + "' '" + r0 + "' '" + r1 + "' '" + done + "'");
            }
            Check("Q6a", "the 14 EventCount and 2 KillEnemyFilter quests decode to the quest box's words, step and row: 'Liberate Survivors 1 of 3', 'Complete a Research Pod Event (needs 1)', 'Defeat Elite Bosses 0 of 3', 'Collect Cash, done' (40 characters at most)",
                bad.Count == 0, string.Join("; ", bad));

            // (b) the step: a count of more than ten rebuilds the readout once per tenth of the way (Key), a short one at every count
            Func<string, int> keys = name =>
            {
                var q = CountedQuests.First(x => x.Quest == name); var st = new QuestStory { Quest = name }; var c = DecodeCounted(q); st.Counts.Add(c);
                var seen = new HashSet<string>(); for (int h = 0; h <= q.Need; h++) { c.Have = h; seen.Add(st.Key()); }
                return seen.Count;
            };
            int kMedic = keys("GameHubQuest_Medic_2"), kSwat = keys("GameHubQuest_SWAT_3"), kGhost = keys("GameHubQuest_Ghost_1");
            Check("Q6a", "the readout's step: Collect Cash 30 / Defeat Bosses 30 every 3rd, Defeat Elites 20 / Military Training Points 20 every 2nd, the rest every count; the key over a run: Collect Cash 11 rebuilds (not 31), Defeat Elites 11, Rerolled Item Chests 4",
                QuestStory.StepOf(30) == 3 && QuestStory.StepOf(20) == 2 && QuestStory.StepOf(10) == 1 && QuestStory.StepOf(7) == 1 && QuestStory.StepOf(1) == 1 && kMedic == 11 && kSwat == 11 && kGhost == 4,
                kMedic + " / " + kSwat + " / " + kGhost);

            // (c) the rails over every GameEvents name and every rank mask, at every count a row can show
            bad.Clear(); int rows = 0;
            foreach (int need in new[] { 1, 10, 30 })
                foreach (double have in new[] { double.NaN, 0, need - 1, need })
                {
                    foreach (var ev in GameEvents) { var c = QuestStory.Decode("EventCount", need, targetEvent: ev); c.Have = have; string t = QuestStory.RowText(c); rows++; if (!RowOk(t)) bad.Add(ev + " " + need + "/" + have + ": '" + t + "'"); }
                    for (int mask = 1; mask <= 15; mask++)
                        foreach (bool boss in new[] { true, false })
                        { var c = QuestStory.Decode("KillEnemyFilter", need, requireBoss: boss, rankMask: mask); c.Have = have; string t = QuestStory.RowText(c); rows++; if (!RowOk(t)) bad.Add("mask " + mask + (boss ? " boss " : " ") + need + "/" + have + ": '" + t + "'"); }
                }
            Check("Q6a", "the rails: every row of the 53 GameEvents names and the rank masks 1 - 15 (bosses only or not), at counts 1 / 10 / 30 unread / 0 / one short / met, within 40 printable characters (" + rows + " rows)",
                bad.Count == 0, string.Join("; ", bad.Take(6)));
            Check("Q6a", "a name the table lacks, in words: 'HPValueChanged' -> 'HP Value Changed', 'SOSReacted' -> 'SOS Reacted', 'HashtagEventActivated' -> 'Hashtag Event Activated'",
                QuestStory.Words("HPValueChanged") == "HP Value Changed" && QuestStory.Words("SOSReacted") == "SOS Reacted" && QuestStory.Words("HashtagEventActivated") == "Hashtag Event Activated"
                && QuestStory.Decode("EventCount", 1, targetEvent: "HashtagEventActivated").Label == "Hashtag Event Activated");

            // (d) the rows of one quest, and the gates the counted rules of 0.17.0 share
            Func<QuestStory> three = () =>
            {
                var st = new QuestStory { Quest = "a made-up quest" };
                var story = QuestStory.Decode("CustomGameplayEvent", 5, eventId: "main_story_objective_q6"); story.Have = 2;
                var met = QuestStory.Decode("EventCount", 7, targetEvent: "ChestOpened"); met.Have = 7;
                var open = QuestStory.Decode("EventCount", 3, targetEvent: "SOSLiberate"); open.Have = 1;
                st.Counts.Add(met); st.Counts.Add(open); st.Counts.Add(story);
                return st;
            };
            var plain = three();
            var want = new[] { "Broken Vials 2 of 5", "Liberate Survivors 1 of 3" };
            bool order = plain.Rows(2).SequenceEqual(want) && plain.Row() == want[0] && plain.Rows(3).Last() == "Open Item Chests, done" && plain.Rows(0).Count == 0;
            var failed = three(); failed.Failed = true; var away = three(); away.NotThisRun = "this run does not fit the quest's conditions";
            var off = three(); off.Steer = QuestSteer.Off; var any = three(); any.AnyOf = true; var info = three(); info.Steer = QuestSteer.InfoOnly;
            bool gates = failed.Rows(2).Count == 0 && away.Rows(2).Count == 0 && failed.Row() == null
                && off.Rows(2).SequenceEqual(want) && any.Rows(2).SequenceEqual(want) && !off.Follows && !off.Moves && !any.Follows && !any.Moves
                && info.Follows && !info.Moves && plain.Follows && plain.Moves && !failed.Follows && !away.Follows;
            var notRead = three(); notRead.Counts[1].Have = double.NaN;
            bool openEvent = plain.OpenEvent("SOSLiberate") == plain.Counts[1] && off.OpenEvent("SOSLiberate") != null && failed.OpenEvent("SOSLiberate") != null
                && plain.OpenEvent("ChestOpened") == null && notRead.OpenEvent("SOSLiberate") == null && plain.OpenEvent("main_story_objective_q6") == null;
            bool keyGates = plain.Key() != off.Key() && plain.Key() != any.Key() && plain.Key() != info.Key();
            Check("Q6a", "the rows: the story count first, the open ones before the met, two at most ('Broken Vials 2 of 5', 'Liberate Survivors 1 of 3'); a failed quest or a run that cannot complete it: none; QuestSteer Off / Any keep the rows (info only)",
                order && gates, string.Join(" / ", plain.Rows(3)));
            Check("Q6a", "the gates for 0.17.0's counted rules: Follows / Moves as QuestRules' (InfoOnly follows, moves nothing); OpenEvent finds a read, open event count whatever the gates, none met or unread; the gates are in the key",
                openEvent && keyGates);
            Check("Q6a", "the log's words keep the ids (the advice audit's keys): 'event SOSLiberate: 1 of 3 - progress only', 'event ChestOpened met', 'story objective main_story_objective_q6: 2 of 5 - progress only'",
                plain.Said() == "event ChestOpened met; event SOSLiberate: 1 of 3 - progress only; story objective main_story_objective_q6: 2 of 5 - progress only", plain.Said());

            // (e) a count built without Decode (no words known): the fallback words
            var bare1 = new QuestCount { Kind = CountKind.Story, Id = "x", Need = 4, Have = 2 };
            var bare2 = new QuestCount { Kind = CountKind.Event, Id = "HashtagEventActivated", Need = 1, Have = 0 };
            var bare3 = new QuestCount { Kind = CountKind.BossRushBoss, Id = "the Boss Rush boss", Need = 1, Have = 0 };
            var unknown = QuestStory.Decode("KillBossRushBoss", 1, quest: "GameHubQuest_Main_99");
            Check("Q6a", "no words known: 'story objective 2 of 4', 'Hashtag Event Activated 0 of 1', 'the Boss Rush boss' (a quest the table lacks too)",
                QuestStory.RowText(bare1) == "story objective 2 of 4" && QuestStory.RowText(bare2) == "Hashtag Event Activated 0 of 1" && QuestStory.RowText(bare3) == "the Boss Rush boss"
                && QuestStory.RowText(unknown) == "the Boss Rush boss",
                QuestStory.RowText(bare1) + " | " + QuestStory.RowText(bare2) + " | " + QuestStory.RowText(bare3));

            // (f) the sources: where the rows are drawn, the step in the readout's fingerprint, the quest's name for the Boss Rush boss, the gates
            string quest = Src("Quest.cs"), plan = Src("Plan.cs");
            Check("Q6a", "sources: Plan.cs draws StoryRows (two at most, dim); Quest.cs's StoryHash hashes every counted kind by its step; Counted passes the quest's name at its four decodes; Story sets the gates",
                plan != null && plan.Contains("StoryRows(s, 2 - shown.Count)") && plan.Contains("st.Rows(max)")
                && quest != null && quest.Contains("n / Math.Max(1, d.Count.Step)") && !quest.Contains("if (!d.Count.Story || d.Runtime == null) continue;")
                && CountOf(quest, "quest: story.Quest") == 4 && quest.Contains("story.Steer = Steer(); story.AnyOf = team.AnyOf;"));
        }

        static int CountOf(string s, string what) { int n = 0, i = 0; while ((i = s.IndexOf(what, i, StringComparison.Ordinal)) >= 0) { n++; i += what.Length; } return n; }
    }
}
