namespace AORebirth.Database.Domain.Missions
{
    using System;
    using System.Globalization;
    using System.Linq;
    using AORebirth.Interfaces.Persistence.Missions;
    using Dapper;

    public sealed partial class MySqlMissionDao
    {
        private sealed partial class MySqlMissionDaoTransaction : ICharacterQuestCompletionTransaction, ICharacterQuestObjectiveCollectionTransaction
        {
            private const string CharacterQuestRewardType = "CharacterQuestCompletion";

            public CharacterQuestCompletionSnapshotData ReadCharacterQuest(string questId)
            {
                this.EnsureActive();
                ValidateText(questId, "questId", 32, false);
                return this.connection.Query<CharacterQuestCompletionSnapshotData>(
                    "SELECT c.CharacterId,c.QuestId,c.Source,c.State,c.Progress,c.RequiredCount,c.AssignedAtUtcTicks,c.UpdatedAtUtcTicks,"
                    + "COALESCE(g.OwnerType,0) OwnerType,COALESCE(g.OwnerId,0) OwnerId,COALESCE(g.ExpiresAtUtcTicks,0) ExpiresAtUtcTicks,"
                    + "g.DefinitionJson,g.AcgBuildingGeneratorJson FROM characterquests c "
                    + "LEFT JOIN generatedquests g ON c.Source=1 AND g.QuestId=c.QuestId "
                    + "WHERE c.CharacterId=@CharacterId AND c.QuestId=@QuestId FOR UPDATE",
                    new { this.CharacterId, QuestId = questId }, this.transaction).SingleOrDefault();
            }

            public CharacterQuestCompletionResult TryEarnCharacterQuest(CharacterQuestEarnRequest request)
            {
                this.EnsureActive();
                try { return this.EarnCharacterQuest(request); }
                catch { this.failed = true; throw; }
            }

            public CharacterQuestCompletionResult TryCollectCharacterQuestItem(CharacterQuestEarnRequest request)
            {
                this.EnsureActive();
                try { return this.CollectCharacterQuestItem(request); }
                catch { this.failed = true; throw; }
            }

            private CharacterQuestCompletionResult CollectCharacterQuestItem(CharacterQuestEarnRequest request)
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                if (request.Grants == null || request.Removals == null) throw new ArgumentNullException("collectionPlan");
                if (request.Grants.Count != 1 || request.Removals.Count != 0 || request.Grants[0] == null || request.Grants[0].StackCount != 1)
                    throw new ArgumentException("Return mission collection requires one exact objective item and no removals.");
                CharacterQuestCompletionSnapshotData quest = this.ReadCharacterQuest(request.QuestId);
                if (!this.CharacterQuestBindingMatches(quest, request.AssignedAtUtcTicks, request.ExpectedDefinitionJson,
                    request.ExpectedAcgBuildingGeneratorJson, request.NowUtcTicks)
                    || quest.Source != 1 || quest.State != 1 || quest.RequiredCount != 2 || quest.ExpiresAtUtcTicks <= request.NowUtcTicks)
                    return QuestCompletionResult(CharacterQuestCompletionStatus.Rejected, quest, "The exact return mission is unavailable, expired or changed.");
                if (quest.Progress == 1)
                    return QuestCompletionResult(CharacterQuestCompletionStatus.AlreadyCollected, quest, "This return mission item was already collected.");
                if (quest.Progress != 0 || this.GetReward(CharacterQuestRewardKey(quest)) != null)
                    return QuestCompletionResult(CharacterQuestCompletionStatus.Rejected, quest, "The return mission has a conflicting progress or reward state.");

                this.ApplyInventoryMutationCore(request.Grants, request.Removals);
                int updated = this.ExecuteWrite(
                    "UPDATE characterquests SET Progress=1,UpdatedAtUtcTicks=@Now "
                    + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND State=1 AND Progress=0 AND RequiredCount=2 AND AssignedAtUtcTicks=@Assigned",
                    new { this.CharacterId, quest.QuestId, Now = request.NowUtcTicks, Assigned = quest.AssignedAtUtcTicks }, this.transaction);
                this.RequireSingleWrite(updated, "return mission item collection", quest.QuestId, 0);
                quest.Progress = 1;
                quest.UpdatedAtUtcTicks = request.NowUtcTicks;
                return QuestCompletionResult(CharacterQuestCompletionStatus.Collected, quest, "The exact item is durable; completion requires return to its terminal.");
            }

            private CharacterQuestCompletionResult EarnCharacterQuest(CharacterQuestEarnRequest request)
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                if (request.Grants == null) throw new ArgumentNullException(nameof(request.Grants));
                if (request.Removals == null) throw new ArgumentNullException(nameof(request.Removals));
                CharacterQuestCompletionSnapshotData quest = this.ReadCharacterQuest(request.QuestId);
                if (!this.CharacterQuestBindingMatches(quest, request.AssignedAtUtcTicks, request.ExpectedDefinitionJson,
                    request.ExpectedAcgBuildingGeneratorJson, request.NowUtcTicks))
                    return QuestCompletionResult(CharacterQuestCompletionStatus.Rejected, quest, "The exact assigned quest binding is unavailable or changed.");

                MissionRewardKeyData key = CharacterQuestRewardKey(quest);
                MissionRewardStageData ledger = this.GetReward(key);
                if (quest.State == 2 && ledger != null && ledger.Status == MissionRewardStatus.Applied
                    && ledger.RewardType == CharacterQuestRewardType)
                    return QuestCompletionResult(CharacterQuestCompletionStatus.AlreadyApplied, quest, "This assigned quest run was already paid.");
                if (quest.State != 1)
                    return QuestCompletionResult(CharacterQuestCompletionStatus.Rejected, quest, "The quest is no longer active.");
                bool alreadyEarned = quest.Progress >= quest.RequiredCount;
                if (!alreadyEarned && quest.Source == 1 && quest.ExpiresAtUtcTicks <= request.NowUtcTicks)
                    return QuestCompletionResult(CharacterQuestCompletionStatus.Rejected, quest, "The objective was not earned before expiry.");
                if (ledger != null && (ledger.RewardType != CharacterQuestRewardType || ledger.Status != MissionRewardStatus.Pending))
                    return QuestCompletionResult(CharacterQuestCompletionStatus.Rejected, quest, "The completion reward ledger has a conflicting state.");

                // The objective item and its durable earned marker share this transaction. A retry never grants it twice.
                if (!alreadyEarned)
                {
                    this.ApplyInventoryMutationCore(request.Grants, request.Removals);
                    int updated = this.ExecuteWrite(
                        "UPDATE characterquests SET Progress=RequiredCount,UpdatedAtUtcTicks=@Now "
                        + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND State=1 AND AssignedAtUtcTicks=@Assigned",
                        new { this.CharacterId, quest.QuestId, Now = request.NowUtcTicks, Assigned = quest.AssignedAtUtcTicks }, this.transaction);
                    this.RequireSingleWrite(updated, "quest objective collection", quest.QuestId, 0);
                    quest.Progress = quest.RequiredCount;
                    quest.UpdatedAtUtcTicks = request.NowUtcTicks;
                }
                if (ledger == null)
                {
                    int inserted = this.ExecuteWrite(
                        "INSERT INTO missionrewardledger (CharacterId,QuestId,RewardKey,RewardType,Status,Attempts,"
                        + "ClaimedAtUtcTicks,ClaimExpiresAtUtcTicks,AppliedAtUtcTicks,CreatedAtUtcTicks,UpdatedAtUtcTicks,Version) "
                        + "VALUES (@CharacterId,@QuestId,@RewardKey,@RewardType,@Status,0,0,0,0,@Now,@Now,1)",
                        new { this.CharacterId, quest.QuestId, key.RewardKey, RewardType = CharacterQuestRewardType,
                            Status = MissionRewardStatus.Pending, Now = request.NowUtcTicks }, this.transaction);
                    this.RequireSingleWrite(inserted, "pending quest completion reward", quest.QuestId, 0);
                }
                return QuestCompletionResult(alreadyEarned ? CharacterQuestCompletionStatus.AlreadyEarned : CharacterQuestCompletionStatus.Earned,
                    quest, "The objective is durable; rewards remain pending until they can be committed together.");
            }

            public CharacterQuestCompletionResult TryCompleteCharacterQuest(CharacterQuestCompletionRequest request)
            {
                this.EnsureActive();
                try { return this.CompleteCharacterQuest(request); }
                catch { this.failed = true; throw; }
            }

            private CharacterQuestCompletionResult CompleteCharacterQuest(CharacterQuestCompletionRequest request)
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                if (request.Grants == null || request.StatMutations == null || request.ExpectedStatValues == null)
                    throw new ArgumentNullException("completionPlan");
                CharacterQuestCompletionSnapshotData quest = this.ReadCharacterQuest(request.QuestId);
                if (!this.CharacterQuestBindingMatches(quest, request.AssignedAtUtcTicks, request.ExpectedDefinitionJson,
                    request.ExpectedAcgBuildingGeneratorJson, request.NowUtcTicks))
                    return QuestCompletionResult(CharacterQuestCompletionStatus.Rejected, quest, "The exact assigned quest binding is unavailable or changed.");
                MissionRewardKeyData key = CharacterQuestRewardKey(quest);
                MissionRewardStageData ledger = this.GetReward(key);
                if (quest.State == (int)CharacterQuestStateCode.Completed && ledger != null && ledger.Status == MissionRewardStatus.Applied
                    && ledger.RewardType == CharacterQuestRewardType)
                    return QuestCompletionResult(CharacterQuestCompletionStatus.AlreadyApplied, quest, "This assigned quest run was already paid.");
                if (quest.State != (int)CharacterQuestStateCode.Active || quest.Progress < quest.RequiredCount || ledger == null
                    || ledger.Status != MissionRewardStatus.Pending || ledger.RewardType != CharacterQuestRewardType)
                    return QuestCompletionResult(CharacterQuestCompletionStatus.Rejected, quest, "The objective has no pending earned reward.");

                CharacterQuestSuccessorRequest successor = request.Successor;
                CharacterQuestCompletionSnapshotData previousSuccessor = null;
                if (successor != null)
                {
                    ValidateText(successor.QuestId, "successorQuestId", 32, false);
                    if (string.Equals(successor.QuestId, quest.QuestId, StringComparison.OrdinalIgnoreCase)
                        || successor.RequiredCount <= 0 || request.NowUtcTicks < quest.AssignedAtUtcTicks)
                        throw new ArgumentException("The successor must be a distinct valid template assignment.");
                    previousSuccessor = this.ReadCharacterQuest(successor.QuestId);
                    if (!CharacterQuestSuccessorRequest.CanAssign(previousSuccessor, quest.AssignedAtUtcTicks))
                        return QuestCompletionResult(CharacterQuestCompletionStatus.Rejected, quest, "The successor already has a protected assignment.");
                }

                // Projections contain the existing runtime's complete cash/XP/level effects. Fail closed on a stale base.
                if (request.StatMutations.Count != request.ExpectedStatValues.Count
                    || request.StatMutations.Any(mutation => mutation == null || mutation.Kind != MissionStatMutationKind.Set)
                    || request.ExpectedStatValues.Any(value => value == null)
                    || request.StatMutations.Select(value => (value.StatIdentityType, value.StatId)).Distinct().Count() != request.StatMutations.Count
                    || request.ExpectedStatValues.Select(value => (value.StatIdentityType, value.StatId)).Distinct().Count() != request.ExpectedStatValues.Count)
                    throw new ArgumentException("Completion requires distinct exact stat Set projections and their expected durable values.");
                foreach (MissionStatMutationData mutation in request.StatMutations)
                {
                    ValidateStatMutation(mutation);
                    MissionStatValueData expected = request.ExpectedStatValues.SingleOrDefault(value => value.StatIdentityType == mutation.StatIdentityType && value.StatId == mutation.StatId);
                    if (expected == null || this.ReadStatValue(mutation.StatIdentityType, mutation.StatId) != expected.Value)
                        return QuestCompletionResult(CharacterQuestCompletionStatus.Rejected, quest, "The durable character stats changed before completion.");
                }

                this.ApplyInventoryMutationCore(request.Grants, new MissionItemInstanceData[0], allowOverflowGrants: successor != null);
                // Only this private path can authorize the shared ledger/stat primitive from characterquests.
                MissionAtomicStatRewardResultData reward = this.ApplyCharacterStatReward(key, CharacterQuestRewardType,
                    request.StatMutations, "characterquest:" + quest.QuestId + ":" + quest.AssignedAtUtcTicks.ToString(CultureInfo.InvariantCulture),
                    request.NowUtcTicks, validatedCharacterQuestCompletion: true);
                if (reward.Status != MissionAtomicRewardStatus.Applied)
                    throw new InvalidOperationException("The validated pending quest reward could not be applied atomically.");
                int completed = this.ExecuteWrite(
                    "UPDATE characterquests SET State=@Completed,Progress=RequiredCount,UpdatedAtUtcTicks=@Now "
                    + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND State=@Active AND Progress>=RequiredCount AND AssignedAtUtcTicks=@Assigned",
                    new { this.CharacterId, quest.QuestId, Now = request.NowUtcTicks, Assigned = quest.AssignedAtUtcTicks,
                        Completed = (int)CharacterQuestStateCode.Completed, Active = (int)CharacterQuestStateCode.Active }, this.transaction);
                this.RequireSingleWrite(completed, "quest completion", quest.QuestId, 0);
                if (successor != null)
                    this.AssignSuccessor(successor, previousSuccessor, request.NowUtcTicks);
                quest.State = (int)CharacterQuestStateCode.Completed;
                quest.UpdatedAtUtcTicks = request.NowUtcTicks;
                var result = QuestCompletionResult(CharacterQuestCompletionStatus.Applied, quest, "Quest state, inventory, stats and reward ledger committed together.");
                result.StatValues = reward.StatValues;
                return result;
            }

            private void AssignSuccessor(CharacterQuestSuccessorRequest successor,
                CharacterQuestCompletionSnapshotData previous, long now)
            {
                int template = (int)CharacterQuestSourceCode.Template;
                int active = (int)CharacterQuestStateCode.Active;
                int updated = previous == null
                    ? this.ExecuteWrite(
                        "INSERT INTO characterquests (CharacterId,QuestId,Source,State,Progress,RequiredCount,AssignedAtUtcTicks,UpdatedAtUtcTicks) "
                        + "VALUES (@CharacterId,@QuestId,@Source,@Active,0,@RequiredCount,@Now,@Now)",
                        new { this.CharacterId, successor.QuestId, successor.RequiredCount, Source = template, Active = active, Now = now },
                        this.transaction)
                    : this.ExecuteWrite(
                        "UPDATE characterquests SET State=@Active,Progress=0,RequiredCount=@RequiredCount,AssignedAtUtcTicks=@Now,UpdatedAtUtcTicks=@Now "
                        + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND Source=@Source AND State=@PreviousState AND AssignedAtUtcTicks=@Assigned",
                        new { this.CharacterId, successor.QuestId, successor.RequiredCount, Now = now,
                            Source = template, Active = active, PreviousState = previous.State, Assigned = previous.AssignedAtUtcTicks }, this.transaction);
                this.RequireSingleWrite(updated, "quest successor assignment", successor.QuestId, 0);
            }

            private bool CharacterQuestBindingMatches(CharacterQuestCompletionSnapshotData quest, long assigned,
                string definition, string acg, long now)
            {
                if (quest == null || quest.CharacterId != this.CharacterId || assigned <= 0 || now <= 0
                    || quest.AssignedAtUtcTicks != assigned || quest.RequiredCount <= 0 || (quest.Source != 0 && quest.Source != 1))
                    return false;
                return quest.Source == 0 || (quest.OwnerType == 0 && quest.OwnerId == this.CharacterId && quest.ExpiresAtUtcTicks > 0
                    && !string.IsNullOrEmpty(definition) && string.Equals(quest.DefinitionJson, definition, StringComparison.Ordinal)
                    && string.Equals(quest.AcgBuildingGeneratorJson, acg, StringComparison.Ordinal));
            }

            private static MissionRewardKeyData CharacterQuestRewardKey(CharacterQuestCompletionSnapshotData quest)
                => new MissionRewardKeyData(new MissionKeyData(quest.CharacterId, quest.QuestId),
                    "completion:" + quest.AssignedAtUtcTicks.ToString(CultureInfo.InvariantCulture));

            private static CharacterQuestCompletionResult QuestCompletionResult(CharacterQuestCompletionStatus status,
                CharacterQuestCompletionSnapshotData quest, string reason)
                => new CharacterQuestCompletionResult { Status = status, Quest = quest, Reason = reason };
        }
    }
}
