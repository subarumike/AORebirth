namespace AORebirth.Tools.MissionDaoValidation
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AORebirth.Database.Domain.Missions;
    using AORebirth.Database.Domain.Quests;
    using AORebirth.Interfaces.Persistence.Missions;
    using MySqlConnector;
    using SmokeLounge.AOtomation.Messaging.GameData;

    internal static partial class Program
    {
        private const int CompletionOwner = 103;
        private const long CompletionAssigned = 1000;
        private const string CompletionDefinition = "{\"Action\":\"FindItem\",\"Reward\":{\"Credits\":17,\"Xp\":23}}";
        private const string CompletionBinding = "{\"EntranceInstance\":-1073741319,\"ObjectiveInstance\":910099}";

        private static void ValidateCharacterQuestSuccessors(MySqlMissionDao dao, string connectionString)
        {
            var quests = new MySqlCharacterQuestDao(() => new MySqlConnection(connectionString));
            foreach (bool restart in new[] { false, true })
            {
                string path = restart ? "restart" : "insert";
                string predecessor = "successor." + path + ".before";
                string successor = "successor." + path + ".after";
                int template = (int)CharacterQuestSourceCode.Template;
                int active = (int)CharacterQuestStateCode.Active;
                int abandoned = (int)CharacterQuestStateCode.Abandoned;
                quests.SaveCharacterQuest(CompletionRow(predecessor, state: active, source: template));
                if (restart)
                    quests.SaveCharacterQuest(CompletionRow(successor, state: abandoned, source: template, assigned: CompletionAssigned - 1));
                ResetCompletionStats(connectionString);
                Require(dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                    .TryEarnCharacterQuest(EarnRequest(predecessor))).Status == CharacterQuestCompletionStatus.Earned,
                    "successor-" + path + "-predecessor-earned");

                MissionItemInstanceData startItem = CompletionItem(restart ? 920301 : 920300, restart ? 112 : 111);
                startItem.ContainerType = (int)IdentityType.OverflowWindow;
                var request = CompleteRequest(predecessor, startItem);
                request.Successor = new CharacterQuestSuccessorRequest { QuestId = successor, RequiredCount = 2 };

                Expect<ExpectedRollbackException>(() => dao.Execute<int>(CompletionOwner, tx =>
                {
                    Require(((ICharacterQuestCompletionTransaction)tx).TryCompleteCharacterQuest(request).Status
                        == CharacterQuestCompletionStatus.Applied, "successor-" + path + "-applied-within-transaction");
                    throw new ExpectedRollbackException();
                }), "successor-" + path + "-known-rollback");

                var rolledBack = quests.LoadForCharacter(CompletionOwner);
                CharacterQuestRow oldSuccessor = rolledBack.SingleOrDefault(row => row.QuestId == successor);
                Require(rolledBack.Single(row => row.QuestId == predecessor).State == active
                    && (restart ? oldSuccessor != null && oldSuccessor.State == abandoned
                        && oldSuccessor.AssignedAtUtcTicks == CompletionAssigned - 1 : oldSuccessor == null),
                    "successor-" + path + "-rollback-restores-both-quest-states");
                Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=" + startItem.InstanceId) == 0
                    && ReadCompletionStat(connectionString, CharacterStat.Cash) == 100
                    && ReadCompletionStat(connectionString, CharacterStat.XP) == 200
                    && Scalar(connectionString, "SELECT Status FROM missionrewardledger WHERE QuestId='" + predecessor + "'")
                        == (int)MissionRewardStatus.Pending, "successor-" + path + "-rollback-restores-reward-and-grant");

                Require(dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                    .TryCompleteCharacterQuest(request)).Status == CharacterQuestCompletionStatus.Applied,
                    "successor-" + path + "-commits");
                var committed = quests.LoadForCharacter(CompletionOwner);
                CharacterQuestRow next = committed.Single(row => row.QuestId == successor);
                Require(committed.Single(row => row.QuestId == predecessor).State == (int)CharacterQuestStateCode.Completed
                    && next.Source == template && next.State == active && next.Progress == 0 && next.RequiredCount == 2
                    && next.AssignedAtUtcTicks == request.NowUtcTicks, "successor-" + path + "-commits-exact-row");
                Require(dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                    .TryCompleteCharacterQuest(request)).Status == CharacterQuestCompletionStatus.AlreadyApplied,
                    "successor-" + path + "-retry-idempotent");
                Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=" + startItem.InstanceId
                    + " AND ContainerType=" + startItem.ContainerType + " AND ContainerInstance=" + CompletionOwner) == 1
                    && ReadCompletionStat(connectionString, CharacterStat.Cash) == 117
                    && ReadCompletionStat(connectionString, CharacterStat.XP) == 223
                    && Scalar(connectionString, "SELECT Status FROM missionrewardledger WHERE QuestId='" + predecessor + "'")
                        == (int)MissionRewardStatus.Applied, "successor-" + path + "-reward-and-grant-exactly-once");
            }
        }

        private static void ValidateCharacterQuestCompletion(MySqlMissionDao dao, string connectionString)
        {
            var quests = new MySqlCharacterQuestDao(() => new MySqlConnection(connectionString));
            SeedCompletionQuest(quests, "completion.pending");
            ResetCompletionStats(connectionString);
            var objective = CompletionItem(920001, 64);
            var earned = dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                .TryEarnCharacterQuest(EarnRequest("completion.pending", objective)));
            Require(earned.Status == CharacterQuestCompletionStatus.Earned && earned.Quest.State == 1 && earned.Quest.Progress == 1,
                "quest-collection-durable-earned-active");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=920001 AND ContainerInstance=103") == 1,
                "quest-objective-item-granted-with-earned-marker");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM missionrewardledger WHERE QuestId='completion.pending' AND Status=1") == 1,
                "quest-full-inventory-pending-ledger");
            Require(ReadCompletionStat(connectionString, CharacterStat.Cash) == 100 && ReadCompletionStat(connectionString, CharacterStat.XP) == 200,
                "quest-pending-no-premature-cash-or-xp");
            Require(dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                .TryEarnCharacterQuest(EarnRequest("completion.pending", CompletionItem(920002, 65)))).Status == CharacterQuestCompletionStatus.AlreadyEarned,
                "quest-repeat-collection-idempotent");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=920002") == 0,
                "quest-repeat-collection-no-second-objective");

            // Pending is earned, so both normal expiry cleanup and a stale save must leave it recoverable.
            quests.CloseCharacterQuests("completion.pending", 4, 4000);
            Require(quests.LoadForCharacter(CompletionOwner).Single(row => row.QuestId == "completion.pending").State == 1,
                "quest-earned-pending-survives-expiry-close");
            Expect<InvalidOperationException>(() => quests.SaveCharacterQuest(CompletionRow("completion.pending", state: 5)),
                "quest-earned-pending-cannot-be-abandoned");
            Expect<InvalidOperationException>(() => quests.SaveCharacterQuest(CompletionRow("completion.pending")),
                "quest-earned-pending-cannot-reset-progress");
            Require(quests.PurgeEndedGeneratedQuests(4000, 50) == 0 && quests.LoadGenerated("completion.pending") != null,
                "quest-earned-pending-survives-purge-without-keys");
            using (var connection = Open(connectionString))
            using (var command = new MySqlCommand("INSERT INTO questdungeonkeys (KeyInstanceId,QuestId,CreatedAtUtcTicks) VALUES (929999,'completion.pending',1000)", connection))
                command.ExecuteNonQuery();
            Require(!quests.LoadExpiredQuestsWithKeys(4000, 50).Contains("completion.pending"),
                "quest-earned-pending-skipped-by-expiry-key-sweep");

            var afterRestart = new MySqlMissionDao(() => new MySqlConnection(connectionString));
            CharacterQuestCompletionRequest pendingReward = CompleteRequest("completion.pending", CompletionItem(920003, 65));
            pendingReward.NowUtcTicks = 4000;
            var paid = afterRestart.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryCompleteCharacterQuest(pendingReward));
            Require(paid.Status == CharacterQuestCompletionStatus.Applied && paid.Quest.State == 2,
                "quest-pending-restart-after-deadline-pays-and-completes");
            Require(ReadCompletionStat(connectionString, CharacterStat.Cash) == 117 && ReadCompletionStat(connectionString, CharacterStat.XP) == 223,
                "quest-cash-and-xp-durable-with-completion");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=920003") == 1
                && Scalar(connectionString, "SELECT COUNT(*) FROM missionrewardledger WHERE QuestId='completion.pending' AND Status=3") == 1,
                "quest-item-and-applied-ledger-durable-with-completion");
            pendingReward.Grants = new[] { CompletionItem(920004, 66) };
            Require(afterRestart.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryCompleteCharacterQuest(pendingReward)).Status
                == CharacterQuestCompletionStatus.AlreadyApplied, "quest-payment-retry-is-idempotent");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=920004") == 0
                && ReadCompletionStat(connectionString, CharacterStat.Cash) == 117, "quest-retry-no-item-or-cash-duplication");

            ValidateQuestCompletionRejections(dao, quests, connectionString);
            ValidateQuestCompletionRollbackAndConcurrency(dao, quests, connectionString);
            ValidateRepeatableQuestRewards(dao, quests, connectionString);
            ValidateQuestCompletionStatBits(dao, quests, connectionString);
            ValidateCharacterQuestSuccessors(dao, connectionString);
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM missionstates WHERE QuestId LIKE 'completion.%'") == 0,
                "quest-completion-does-not-mirror-authored-missionstate");
        }

        private static void ValidateQuestCompletionRejections(MySqlMissionDao dao, MySqlCharacterQuestDao quests, string connectionString)
        {
            SeedCompletionQuest(quests, "completion.expired");
            var expired = EarnRequest("completion.expired", CompletionItem(920010, 70));
            expired.NowUtcTicks = 3000;
            Require(dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryEarnCharacterQuest(expired)).Status
                == CharacterQuestCompletionStatus.Rejected, "quest-unearned-expired-rejected");
            SeedCompletionQuest(quests, "completion.abandoned", state: 5);
            Require(dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryEarnCharacterQuest(EarnRequest("completion.abandoned"))).Status
                == CharacterQuestCompletionStatus.Rejected, "quest-abandoned-rejected");
            SeedCompletionQuest(quests, "completion.binding");
            foreach (string mismatch in new[] { "definition", "layout", "assigned" })
            {
                var request = EarnRequest("completion.binding", CompletionItem(920011, 71));
                if (mismatch == "definition") request.ExpectedDefinitionJson = "{}";
                if (mismatch == "layout") request.ExpectedAcgBuildingGeneratorJson = "{}";
                if (mismatch == "assigned") request.AssignedAtUtcTicks++;
                Require(dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryEarnCharacterQuest(request)).Status
                    == CharacterQuestCompletionStatus.Rejected, "quest-exact-" + mismatch + "-required");
            }
            Require(dao.Execute(102, tx => ((ICharacterQuestCompletionTransaction)tx).TryEarnCharacterQuest(EarnRequest("completion.binding"))).Status
                == CharacterQuestCompletionStatus.Rejected, "quest-wrong-character-rejected");
            Require(dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryCompleteCharacterQuest(CompleteRequest("completion.binding"))).Status
                == CharacterQuestCompletionStatus.Rejected, "quest-unearned-payment-rejected");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId IN (920010,920011)") == 0,
                "quest-rejections-grant-no-objective");

            SeedCompletionQuest(quests, "completion.stale-stats");
            ResetCompletionStats(connectionString);
            dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryEarnCharacterQuest(EarnRequest("completion.stale-stats")));
            var stale = CompleteRequest("completion.stale-stats", CompletionItem(920012, 72));
            stale.ExpectedStatValues[0].Value = 99;
            Require(dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryCompleteCharacterQuest(stale)).Status
                == CharacterQuestCompletionStatus.Rejected, "quest-stale-stat-plan-rejected");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=920012") == 0
                && ReadCompletionStat(connectionString, CharacterStat.Cash) == 100, "quest-stale-stat-plan-no-effects");
        }

        private static void ValidateQuestCompletionRollbackAndConcurrency(MySqlMissionDao dao, MySqlCharacterQuestDao quests, string connectionString)
        {
            SeedCompletionQuest(quests, "completion.rollback");
            ResetCompletionStats(connectionString);
            Expect<ExpectedRollbackException>(() => dao.Execute<int>(CompletionOwner, tx =>
            {
                var owner = (ICharacterQuestCompletionTransaction)tx;
                owner.TryEarnCharacterQuest(EarnRequest("completion.rollback", CompletionItem(920020, 73)));
                owner.TryCompleteCharacterQuest(CompleteRequest("completion.rollback", CompletionItem(920021, 74)));
                throw new ExpectedRollbackException();
            }), "quest-collection-and-payment-rollback-injected");
            Require(quests.LoadForCharacter(CompletionOwner).Single(row => row.QuestId == "completion.rollback").Progress == 0
                && Scalar(connectionString, "SELECT COUNT(*) FROM missionrewardledger WHERE QuestId='completion.rollback'") == 0
                && Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId IN (920020,920021)") == 0
                && ReadCompletionStat(connectionString, CharacterStat.Cash) == 100,
                "quest-rollback-removes-progress-items-stats-and-ledger");

            dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryEarnCharacterQuest(EarnRequest("completion.rollback")));
            var faultConnection = new FaultConnection(connectionString) { FailurePoint = "quest-completion" };
            var faultDao = new MySqlMissionDao(() => faultConnection);
            Expect<InjectedPersistenceException>(() => faultDao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                .TryCompleteCharacterQuest(CompleteRequest("completion.rollback", CompletionItem(920022, 75)))), "quest-state-write-failure-after-reward-writes");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=920022") == 0
                && Scalar(connectionString, "SELECT Status FROM missionrewardledger WHERE QuestId='completion.rollback'") == 1
                && ReadCompletionStat(connectionString, CharacterStat.Cash) == 100, "quest-state-write-failure-rolls-back-all-rewards");

            var concurrent = new CharacterQuestCompletionResult[2];
            Parallel.For(0, 2, index => concurrent[index] = dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                .TryCompleteCharacterQuest(CompleteRequest("completion.rollback", CompletionItem(920023 + index, 76 + index)))));
            Require(concurrent.Count(result => result.Status == CharacterQuestCompletionStatus.Applied) == 1
                && concurrent.Count(result => result.Status == CharacterQuestCompletionStatus.AlreadyApplied) == 1,
                "quest-concurrent-completion-one-winner");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId IN (920023,920024)") == 1
                && ReadCompletionStat(connectionString, CharacterStat.Cash) == 117, "quest-concurrent-completion-no-double-effects");

            foreach (string point in new[] { "commit-before", "commit-after" })
            {
                string quest = "completion." + point;
                SeedCompletionQuest(quests, quest);
                ResetCompletionStats(connectionString);
                dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryEarnCharacterQuest(EarnRequest(quest)));
                int item = point == "commit-before" ? 920025 : 920026;
                var transport = new FaultConnection(connectionString) { FailurePoint = point };
                var uncertain = new MySqlMissionDao(() => transport);
                Expect<MissionCommitOutcomeUnknownException>(() => uncertain.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                    .TryCompleteCharacterQuest(CompleteRequest(quest, CompletionItem(item, 78)))), "quest-unknown-" + point + "-reported");
                var retried = dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                    .TryCompleteCharacterQuest(CompleteRequest(quest, CompletionItem(item + 100, 79))));
                Require(retried.Status == (point == "commit-before" ? CharacterQuestCompletionStatus.Applied : CharacterQuestCompletionStatus.AlreadyApplied),
                    "quest-unknown-" + point + "-durable-reconciliation");
                Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId IN (" + item + "," + (item + 100) + ")") == 1
                    && ReadCompletionStat(connectionString, CharacterStat.Cash) == 117, "quest-unknown-" + point + "-one-reward");
            }
        }

        private static void ValidateRepeatableQuestRewards(MySqlMissionDao dao, MySqlCharacterQuestDao quests, string connectionString)
        {
            for (int run = 0; run < 2; run++)
            {
                long assigned = CompletionAssigned + run;
                quests.SaveCharacterQuest(CompletionRow("completion.template", source: 0, assigned: assigned));
                ResetCompletionStats(connectionString);
                var earn = EarnRequest("completion.template");
                earn.AssignedAtUtcTicks = assigned;
                var reward = CompleteRequest("completion.template");
                reward.AssignedAtUtcTicks = assigned;
                var result = dao.Execute(CompletionOwner, tx =>
                {
                    var owner = (ICharacterQuestCompletionTransaction)tx;
                    owner.TryEarnCharacterQuest(earn);
                    return owner.TryCompleteCharacterQuest(reward);
                });
                Require(result.Status == CharacterQuestCompletionStatus.Applied, "quest-template-repeat-run-" + run + "-paid");
            }
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM missionrewardledger WHERE QuestId='completion.template' AND Status=3") == 2,
                "quest-template-runs-have-separate-ledger-identities");
            Require(dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx).TryCompleteCharacterQuest(CompleteRequest("completion.template"))).Status
                == CharacterQuestCompletionStatus.Rejected, "quest-old-assignment-cannot-complete-new-run");
        }

        private static void ValidateQuestCompletionStatBits(MySqlMissionDao dao, MySqlCharacterQuestDao quests, string connectionString)
        {
            SeedCompletionQuest(quests, "completion.stat-bits");
            const uint beforeBits = 0xC00001F9u;
            const uint afterBits = 0xE0000314u;
            using (var connection = Open(connectionString))
            using (var command = new MySqlCommand("UPDATE stats SET StatValue=@Value WHERE Instance=103 AND Type=50000 AND StatId=@Xp", connection))
            {
                command.Parameters.AddWithValue("@Value", unchecked((int)beforeBits));
                command.Parameters.AddWithValue("@Xp", (int)CharacterStat.XP);
                command.ExecuteNonQuery();
            }
            var reward = CompleteRequest("completion.stat-bits");
            reward.ExpectedStatValues = new[] { new MissionStatValueData { StatIdentityType = 50000,
                StatId = (int)CharacterStat.XP, Value = unchecked((int)beforeBits) } };
            reward.StatMutations = new[] { new MissionStatMutationData { StatIdentityType = 50000, StatId = (int)CharacterStat.XP,
                Kind = MissionStatMutationKind.Set, Value = unchecked((int)afterBits), MinimumValue = int.MinValue, MaximumValue = int.MaxValue } };
            var applied = dao.Execute(CompletionOwner, tx =>
            {
                var owner = (ICharacterQuestCompletionTransaction)tx;
                owner.TryEarnCharacterQuest(EarnRequest("completion.stat-bits"));
                return owner.TryCompleteCharacterQuest(reward);
            });
            Require(applied.Status == CharacterQuestCompletionStatus.Applied
                && unchecked((uint)ReadCompletionStat(connectionString, CharacterStat.XP)) == afterBits,
                "quest-stat-set-preserves-all-signed-int-bits");

            SeedCompletionQuest(quests, "completion.no-rewards");
            var empty = CompleteRequest("completion.no-rewards");
            empty.ExpectedStatValues = new MissionStatValueData[0];
            empty.StatMutations = new MissionStatMutationData[0];
            Require(dao.Execute(CompletionOwner, tx =>
            {
                var owner = (ICharacterQuestCompletionTransaction)tx;
                owner.TryEarnCharacterQuest(EarnRequest("completion.no-rewards"));
                return owner.TryCompleteCharacterQuest(empty);
            }).Status == CharacterQuestCompletionStatus.Applied, "quest-without-item-or-stat-rewards-still-completes-once");
        }

        private static void SeedCompletionQuest(MySqlCharacterQuestDao quests, string id, int state = 1)
        {
            quests.SaveGenerated(new GeneratedQuestRow { QuestId = id, OwnerType = 0, OwnerId = CompletionOwner,
                DefinitionJson = CompletionDefinition, AcgBuildingGeneratorJson = CompletionBinding,
                CreatedAtUtcTicks = CompletionAssigned, UpdatedAtUtcTicks = CompletionAssigned, ExpiresAtUtcTicks = 3000 });
            quests.SaveCharacterQuest(CompletionRow(id, state));
        }

        private static CharacterQuestRow CompletionRow(string id, int state = 1, int source = 1, long assigned = CompletionAssigned)
            => new CharacterQuestRow { CharacterId = CompletionOwner, QuestId = id, Source = source, State = state,
                Progress = 0, RequiredCount = 1, AssignedAtUtcTicks = assigned, UpdatedAtUtcTicks = assigned };

        private static CharacterQuestEarnRequest EarnRequest(string quest, params MissionItemInstanceData[] grants)
            => new CharacterQuestEarnRequest { QuestId = quest, AssignedAtUtcTicks = CompletionAssigned, ExpectedDefinitionJson = CompletionDefinition,
                ExpectedAcgBuildingGeneratorJson = CompletionBinding, NowUtcTicks = 2000, Grants = grants };

        private static CharacterQuestCompletionRequest CompleteRequest(string quest, params MissionItemInstanceData[] grants)
            => new CharacterQuestCompletionRequest { QuestId = quest, AssignedAtUtcTicks = CompletionAssigned, ExpectedDefinitionJson = CompletionDefinition,
                ExpectedAcgBuildingGeneratorJson = CompletionBinding, NowUtcTicks = 2000, Grants = grants,
                ExpectedStatValues = new[] { new MissionStatValueData { StatIdentityType = 50000, StatId = (int)CharacterStat.Cash, Value = 100 },
                    new MissionStatValueData { StatIdentityType = 50000, StatId = (int)CharacterStat.XP, Value = 200 } },
                StatMutations = new[] { CompletionStat(CharacterStat.Cash, 117), CompletionStat(CharacterStat.XP, 223) } };

        private static MissionStatMutationData CompletionStat(CharacterStat stat, long value)
            => new MissionStatMutationData { StatIdentityType = 50000, StatId = (int)stat, Kind = MissionStatMutationKind.Set,
                Value = value, MinimumValue = 0, MaximumValue = int.MaxValue };

        private static MissionItemInstanceData CompletionItem(int id, int slot)
            => new MissionItemInstanceData { InstanceId = id, ContainerType = 104, ContainerInstance = CompletionOwner, ContainerPlacement = slot,
                ItemType = 0xC76D, LowId = 10, HighId = 10, Quality = 1, StackCount = 1, Source = 0 };

        private static void ResetCompletionStats(string connectionString)
        {
            using (var connection = Open(connectionString))
            using (var command = new MySqlCommand("INSERT INTO stats (Instance,Type,StatId,StatValue) VALUES (103,50000,@Cash,100),(103,50000,@Xp,200) "
                + "ON DUPLICATE KEY UPDATE StatValue=VALUES(StatValue)", connection))
            {
                command.Parameters.AddWithValue("@Cash", (int)CharacterStat.Cash);
                command.Parameters.AddWithValue("@Xp", (int)CharacterStat.XP);
                command.ExecuteNonQuery();
            }
        }

        private static long ReadCompletionStat(string connectionString, CharacterStat stat)
            => Scalar(connectionString, "SELECT StatValue FROM stats WHERE Instance=103 AND Type=50000 AND StatId=" + (int)stat);
    }
}
