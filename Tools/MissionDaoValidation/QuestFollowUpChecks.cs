namespace AORebirth.Tools.MissionDaoValidation
{
    using System;
    using System.Linq;
    using AORebirth.Database.Domain.Missions;
    using AORebirth.Database.Domain.Quests;
    using AORebirth.Interfaces.Persistence.Missions;
    using MySqlConnector;
    using SmokeLounge.AOtomation.Messaging.GameData;

    internal static partial class Program
    {
        private static void ValidateQuestFollowUpRecovery(MySqlMissionDao dao, string connectionString)
        {
            const string parent = "follow-up.parent";
            const string child = "follow-up.child";
            var quests = new MySqlCharacterQuestDao(() => new MySqlConnection(connectionString));
            quests.SaveCharacterQuest(CompletionRow(parent, source: 0));
            dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryEarnCharacterQuest(EarnRequest(parent)));

            var overflow = CompletionItem(924001, 34);
            overflow.ContainerType = (int)IdentityType.OverflowWindow;
            var primary = new CharacterQuestCompletionRequest
            {
                QuestId = parent, AssignedAtUtcTicks = CompletionAssigned, NowUtcTicks = 2000,
                FollowUpQuestId = child, Grants = new[] { overflow, CompletionItem(924002, 113) }
            };
            Expect<InjectedPersistenceException>(() => dao.Execute<int>(CompletionOwner, tx =>
            {
                ((ICharacterQuestCompletionTransaction)tx).TryCompleteCharacterQuest(primary);
                throw new InjectedPersistenceException();
            }), "quest-follow-up-primary-intent-rolls-back-with-primary-reward");
            Require(dao.GetPendingCharacterQuestFollowUps(CompletionOwner).Count == 0
                && Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId IN (924001,924002)") == 0,
                "quest-follow-up-no-intent-or-overflow-grant-before-primary-commit");

            var paid = dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryCompleteCharacterQuest(primary));
            Require(paid.Status == CharacterQuestCompletionStatus.Applied && paid.Quest.State == 2
                && paid.FollowUp.Status == MissionRewardStatus.Pending
                && paid.FollowUp.RewardKey == "next-step:1000"
                && Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=924001 AND ContainerType=110 AND ContainerPlacement=34") == 1,
                "quest-follow-up-intent-and-primary-overflow-committed-together");
            var reopened = new MySqlMissionDao(() => new MySqlConnection(connectionString));
            var stage = reopened.GetPendingCharacterQuestFollowUps(CompletionOwner).Single();
            Require(stage.QuestId == parent && stage.EffectReference == child
                && !quests.LoadForCharacter(CompletionOwner).Any(quest => quest.QuestId == child),
                "quest-follow-up-reloaded-from-applied-parent-before-assignment");
            Require(reopened.GetPendingCharacterQuestFollowUps(102).Count == 0,
                "quest-follow-up-intent-is-character-scoped");

            var childGrant = new CharacterQuestFollowUpRequest
            {
                QuestId = child, RequiredCount = 2, NowUtcTicks = 3000,
                Key = new MissionRewardKeyData(new MissionKeyData(CompletionOwner, stage.QuestId), stage.RewardKey),
                StarterGrants = new[] { StarterItem(924003, IdentityType.OverflowWindow, 34) },
                RetiredOverflow = new[] { StarterItem(924001, IdentityType.OverflowWindow, 34) },
                StackMerges = new[]
                {
                    new CharacterQuestStarterStack { Existing = StarterItem(924002, IdentityType.Inventory, 113), FinalCount = 3 }
                }
            };
            Expect<InjectedPersistenceException>(() => dao.Execute<int>(CompletionOwner, tx =>
            {
                ((ICharacterQuestCompletionTransaction)tx).TryApplyCharacterQuestFollowUp(childGrant);
                throw new InjectedPersistenceException();
            }), "quest-follow-up-assignment-rolls-back-before-commit");
            Require(dao.GetPendingCharacterQuestFollowUps(CompletionOwner).Count == 1
                && !quests.LoadForCharacter(CompletionOwner).Any(quest => quest.QuestId == child)
                && Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=924003") == 0
                && Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=924001 AND ContainerType=110") == 1
                && Scalar(connectionString, "SELECT StackCount FROM item_instances WHERE InstanceId=924002") == 1,
                "quest-follow-up-rollback-preserves-primary-and-existing-inventory");
            var uncertain = new MySqlMissionDao(() => new FaultConnection(connectionString) { FailurePoint = "commit-after" });
            Expect<MissionCommitOutcomeUnknownException>(() => uncertain.Execute(CompletionOwner,
                tx => ((ICharacterQuestCompletionTransaction)tx).TryApplyCharacterQuestFollowUp(childGrant)),
                "quest-follow-up-commit-succeeded-with-lost-acknowledgement");
            Require(reopened.GetPendingCharacterQuestFollowUps(CompletionOwner).Count == 0,
                "quest-follow-up-readback-resolves-lost-ack");
            var assigned = quests.LoadForCharacter(CompletionOwner).Single(quest => quest.QuestId == child);
            Require(assigned.State == 1 && assigned.Source == 0 && assigned.Progress == 0
                && assigned.RequiredCount == 2 && assigned.AssignedAtUtcTicks == 3000
                && Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=924003 AND ContainerType=110 AND ContainerPlacement=34") == 1
                && Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=924001 AND ContainerType=0 AND ContainerPlacement=924001") == 1
                && Scalar(connectionString, "SELECT StackCount FROM item_instances WHERE InstanceId=924002") == 3,
                "quest-follow-up-exact-assignment-overflow-retirement-and-stack-committed");
            Require(reopened.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryApplyCharacterQuestFollowUp(childGrant))
                .Status == CharacterQuestCompletionStatus.AlreadyApplied
                && Scalar(connectionString, "SELECT StackCount FROM item_instances WHERE InstanceId=924002") == 3,
                "quest-follow-up-repeated-request-does-not-grant-twice");

            const string second = "follow-up.second";
            quests.SaveCharacterQuest(CompletionRow(second, source: 0));
            dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryEarnCharacterQuest(EarnRequest(second)));
            primary.QuestId = second;
            primary.Grants = new MissionItemInstanceData[0];
            var another = dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryCompleteCharacterQuest(primary));
            childGrant.Key = new MissionRewardKeyData(new MissionKeyData(CompletionOwner, second), another.FollowUp.RewardKey);
            childGrant.ExpectedActive = true;
            var alreadyHeld = reopened.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryApplyCharacterQuestFollowUp(childGrant));
            Require(alreadyHeld.Status == CharacterQuestCompletionStatus.Applied && alreadyHeld.Quest == null
                && reopened.GetPendingCharacterQuestFollowUps(CompletionOwner).Count == 0
                && quests.LoadForCharacter(CompletionOwner).Single(quest => quest.QuestId == child).AssignedAtUtcTicks == 3000,
                "quest-follow-up-already-held-keeps-original-assignment-and-starters");

            var authoredOverflow = StarterItem(924004, IdentityType.OverflowWindow, 35);
            dao.Execute(CompletionOwner, tx =>
            {
                ((IMissionInventoryMutationTransaction)tx).ApplyInventoryMutation(new[] { authoredOverflow }, new MissionItemInstanceData[0]);
                return 0;
            });
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=924004 AND ContainerType=110 AND ContainerPlacement=35") == 1,
                "quest-authored-reward-accepts-durable-overflow-grants");
            Expect<ArgumentException>(() => dao.Execute(CompletionOwner, tx =>
            {
                ((IMissionInventoryMutationTransaction)tx).ApplyInventoryMutation(new MissionItemInstanceData[0], new[] { authoredOverflow });
                return 0;
            }), "quest-authored-objective-consumption-still-requires-main-inventory");

            ValidateQuestFollowUpProcessRecovery(dao, quests, connectionString);
        }

        private static void ValidateQuestFollowUpProcessRecovery(MySqlMissionDao dao, MySqlCharacterQuestDao quests, string connectionString)
        {
            string password = new MySqlConnectionStringBuilder(connectionString).Password;
            int index = 0;
            foreach (string point in new[] { "follow-up-before-commit", "follow-up-after-commit" })
            {
                int item = 924011 + index;
                int slot = 36 + index++;
                string parent = "follow-up.process-" + index;
                string child = parent + ".child";
                quests.SaveCharacterQuest(CompletionRow(parent, source: 0));
                dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryEarnCharacterQuest(EarnRequest(parent)));
                var primary = new CharacterQuestCompletionRequest
                {
                    QuestId = parent, AssignedAtUtcTicks = CompletionAssigned, NowUtcTicks = 2000, FollowUpQuestId = child
                };
                Require(dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryCompleteCharacterQuest(primary))
                    .Status == CharacterQuestCompletionStatus.Applied, "quest-" + point + "-earned-intent");
                Require(RunRewardProcess(point, parent, item, slot, password, terminate: true) == RewardWorkerReady,
                    "quest-" + point + "-worker-terminated");
                bool wasCommitted = point == "follow-up-after-commit";
                Require(quests.LoadForCharacter(CompletionOwner).Any(quest => quest.QuestId == child) == wasCommitted
                    && dao.GetPendingCharacterQuestFollowUps(CompletionOwner).Any(intent => intent.QuestId == parent) != wasCommitted
                    && Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=" + item) == (wasCommitted ? 1 : 0),
                    "quest-" + point + "-authoritative-state-after-kill");
                Require(RunRewardProcess("follow-up-recover", parent, item, slot, password, terminate: false)
                    == (wasCommitted ? "AlreadyApplied" : "Applied"), "quest-" + point + "-fresh-process-recovery");
                Require(RunRewardProcess("follow-up-recover", parent, item, slot, password, terminate: false) == "AlreadyApplied"
                    && Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=" + item) == 1
                    && quests.LoadForCharacter(CompletionOwner).Count(quest => quest.QuestId == child && quest.State == 1) == 1,
                    "quest-" + point + "-fresh-process-no-duplicate-start-item-or-quest");
            }
        }

        private static int RunQuestFollowUpWorker(MySqlMissionDao dao, string point, string quest, int item, int slot)
        {
            if (point != "follow-up-before-commit" && point != "follow-up-after-commit" && point != "follow-up-recover")
                return 2;
            var request = new CharacterQuestFollowUpRequest
            {
                QuestId = quest + ".child", RequiredCount = 1, NowUtcTicks = 3000,
                Key = new MissionRewardKeyData(new MissionKeyData(CompletionOwner, quest), "next-step:1000"),
                StarterGrants = new[] { StarterItem(item, IdentityType.OverflowWindow, slot) }
            };
            var completed = dao.Execute(CompletionOwner, tx =>
            {
                var result = ((ICharacterQuestCompletionTransaction)tx).TryApplyCharacterQuestFollowUp(request);
                if (point == "follow-up-before-commit" && result.Status == CharacterQuestCompletionStatus.Applied)
                    ParkRewardWorker();
                return result;
            });
            if (point == "follow-up-after-commit" && completed.Status == CharacterQuestCompletionStatus.Applied)
                ParkRewardWorker();
            Console.WriteLine(completed.Status);
            return 0;
        }

        private static MissionItemInstanceData StarterItem(int instanceId, IdentityType page, int slot)
        {
            return new MissionItemInstanceData
            {
                InstanceId = instanceId, ContainerType = (int)page, ContainerInstance = CompletionOwner, ContainerPlacement = slot,
                ItemType = 0xC76D, LowId = 10, HighId = 10, Quality = 1, StackCount = 1, Source = 0
            };
        }
    }
}
