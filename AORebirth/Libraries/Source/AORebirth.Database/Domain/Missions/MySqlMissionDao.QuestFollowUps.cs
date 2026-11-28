namespace AORebirth.Database.Domain.Missions
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using AORebirth.Database.Domain.Characters;
    using AORebirth.Database.Domain.Quests;
    using AORebirth.Interfaces.Persistence.Characters;
    using AORebirth.Interfaces.Persistence.Missions;
    using Dapper;
    using SmokeLounge.AOtomation.Messaging.GameData;

    public sealed partial class MySqlMissionDao : ICharacterQuestFollowUpReader
    {
        private const string QuestFollowUpRewardType = "CharacterQuestFollowUp";

        public IReadOnlyList<MissionRewardStageData> GetPendingCharacterQuestFollowUps(int characterId)
        {
            ValidateCharacterId(characterId);
            using (var connection = this.OpenConnection())
            {
                return connection.Query<MissionRewardStageData>(
                    "SELECT CharacterId,QuestId,RewardKey,EffectReference,CreatedAtUtcTicks FROM missionrewardledger "
                    + "WHERE CharacterId=@CharacterId AND RewardType=@RewardType AND Status=@Pending "
                    + "ORDER BY CreatedAtUtcTicks,Id",
                    new { CharacterId = characterId, RewardType = QuestFollowUpRewardType, Pending = MissionRewardStatus.Pending })
                    .ToArray();
            }
        }

        private sealed partial class MySqlMissionDaoTransaction
        {
            private MissionRewardStageData RecordCharacterQuestFollowUp(CharacterQuestCompletionSnapshotData quest, string nextQuestId, long now)
            {
                if (string.IsNullOrEmpty(nextQuestId))
                    return null;
                ValidateText(nextQuestId, "nextQuestId", 32, false);
                var stage = new MissionRewardStageData
                {
                    CharacterId = this.CharacterId,
                    QuestId = quest.QuestId,
                    RewardKey = "next-step:" + quest.AssignedAtUtcTicks.ToString(CultureInfo.InvariantCulture),
                    RewardType = QuestFollowUpRewardType,
                    Status = MissionRewardStatus.Pending,
                    EffectReference = nextQuestId,
                    CreatedAtUtcTicks = now,
                    UpdatedAtUtcTicks = now,
                    Version = 1
                };
                int inserted = this.ExecuteWrite(
                    "INSERT INTO missionrewardledger (CharacterId,QuestId,RewardKey,RewardType,Status,EffectReference,CreatedAtUtcTicks,UpdatedAtUtcTicks,Version) "
                    + "VALUES (@CharacterId,@QuestId,@RewardKey,@RewardType,@Status,@EffectReference,@CreatedAtUtcTicks,@UpdatedAtUtcTicks,@Version)",
                    stage, this.transaction);
                this.RequireSingleWrite(inserted, "quest follow-up intent", quest.QuestId, 0);
                return stage;
            }

            public CharacterQuestCompletionResult TryApplyCharacterQuestFollowUp(CharacterQuestFollowUpRequest request)
            {
                this.EnsureActive();
                try
                {
                    return this.ApplyCharacterQuestFollowUp(request);
                }
                catch
                {
                    this.failed = true;
                    throw;
                }
            }

            private CharacterQuestCompletionResult ApplyCharacterQuestFollowUp(CharacterQuestFollowUpRequest request)
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                if (request.RequiredCount <= 0 || request.NowUtcTicks <= 0 || request.NowUtcTicks > DateTime.MaxValue.Ticks)
                    throw new ArgumentException("A validated quest objective count and assignment time are required.");
                ValidateText(request.QuestId, "nextQuestId", 32, false);
                var stage = this.GetReward(request.Key);
                if (stage == null || stage.RewardType != QuestFollowUpRewardType
                    || !string.Equals(stage.EffectReference, request.QuestId, StringComparison.Ordinal))
                    return QuestCompletionResult(CharacterQuestCompletionStatus.Rejected, null, "The exact quest follow-up intent is unavailable.");
                if (stage.Status == MissionRewardStatus.Applied)
                    return QuestCompletionResult(CharacterQuestCompletionStatus.AlreadyApplied, null, "This quest follow-up was already applied.");
                if (stage.Status != MissionRewardStatus.Pending)
                    return QuestCompletionResult(CharacterQuestCompletionStatus.Rejected, null, "The quest follow-up ledger has a conflicting state.");

                var existing = this.ReadCharacterQuest(request.QuestId);
                if (existing != null && (existing.Source != 0 || !string.Equals(existing.QuestId, request.QuestId, StringComparison.Ordinal)))
                    return QuestCompletionResult(CharacterQuestCompletionStatus.Rejected, null, "The follow-up quest identity is already in use.");
                if (request.ExpectedActive && (existing == null || existing.State != 1))
                    return QuestCompletionResult(CharacterQuestCompletionStatus.Rejected, null, "The held follow-up changed before it could be settled.");

                CharacterQuestCompletionSnapshotData assigned = null;
                if (existing == null || existing.State != 1)
                {
                    // The completed journal row can be assigned again even with a fixed clock. A reused timestamp would
                    // share the earlier run's completion ledger key.
                    long start = existing == null ? request.NowUtcTicks : Math.Max(request.NowUtcTicks, existing.AssignedAtUtcTicks + 1);
                    if (start > DateTime.MaxValue.Ticks)
                        throw new InvalidOperationException("The follow-up assignment timestamp is outside the supported range.");
                    this.ApplyQuestStarterInventory(request);
                    MySqlCharacterQuestDao.UpsertCharacterQuest(this.connection, this.transaction, new CharacterQuestRow
                    {
                        CharacterId = this.CharacterId, QuestId = request.QuestId, Source = 0, State = 1,
                        RequiredCount = request.RequiredCount, AssignedAtUtcTicks = start, UpdatedAtUtcTicks = start
                    });
                    assigned = new CharacterQuestCompletionSnapshotData
                    {
                        CharacterId = this.CharacterId, QuestId = request.QuestId, Source = 0, State = 1,
                        RequiredCount = request.RequiredCount, AssignedAtUtcTicks = start, UpdatedAtUtcTicks = start
                    };
                }

                // If already held, the authored follow-up would be refused. Consume its intent in the same way
                // so reopening or completing the held quest later cannot replay these starter items.
                int applied = this.ExecuteWrite(
                    "UPDATE missionrewardledger SET Status=@Applied,Attempts=Attempts+1,AppliedAtUtcTicks=@Now,UpdatedAtUtcTicks=@Now,Version=Version+1 "
                    + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND RewardKey=@RewardKey AND Status=@Pending AND Version=@Version",
                    new { stage.CharacterId, stage.QuestId, stage.RewardKey, stage.Version, Now = request.NowUtcTicks,
                        Applied = MissionRewardStatus.Applied, Pending = MissionRewardStatus.Pending }, this.transaction);
                this.RequireSingleWrite(applied, "quest follow-up application", request.Key.ToString(), stage.Version);
                return QuestCompletionResult(CharacterQuestCompletionStatus.Applied, assigned,
                    "The follow-up quest assignment and starter grants are settled.");
            }

            private void ApplyQuestStarterInventory(CharacterQuestFollowUpRequest request)
            {
                if (request.StarterGrants == null || request.RetiredOverflow == null || request.StackMerges == null)
                    throw new ArgumentNullException("starterItemPlan");
                if (request.StarterGrants.Count == 0 && request.RetiredOverflow.Count == 0 && request.StackMerges.Count == 0)
                    return;
                var mutation = new CharacterInventoryMutationData { CharacterId = this.CharacterId };
                foreach (var insert in request.StarterGrants)
                {
                    if (!this.ValidStarterItem(insert)
                        || (insert.ContainerType != (int)IdentityType.Inventory && insert.ContainerType != (int)IdentityType.OverflowWindow))
                        throw new ArgumentException("Starter items must remain in the authorized owner pages.");
                    mutation.Inserts.Add(ToPersistedStarterItem(insert));
                }
                foreach (var retired in request.RetiredOverflow)
                {
                    if (!this.ValidStarterItem(retired) || retired.ContainerType != (int)IdentityType.OverflowWindow)
                        throw new ArgumentException("Starter item trimming requires an exact owned overflow item.");
                    mutation.ExpectedSources.Add(ToPersistedStarterItem(retired));
                    mutation.Locations.Add(new ItemLocationData
                    {
                        InstanceId = retired.InstanceId, ContainerType = (int)IdentityType.None,
                        ContainerInstance = this.CharacterId, ContainerPlacement = retired.InstanceId
                    });
                    mutation.EmptyContainersBeforeRetire.Add(retired.InstanceId);
                }
                foreach (var stack in request.StackMerges)
                {
                    var item = stack == null ? null : stack.Existing;
                    if (!this.ValidStarterItem(item) || item.ContainerType != (int)IdentityType.Inventory || stack.FinalCount <= item.StackCount)
                        throw new ArgumentException("A starter item stack merge requires an exact owned inventory item and increased count.");
                    mutation.ExpectedSources.Add(ToPersistedStarterItem(item));
                    mutation.Locations.Add(new ItemLocationData
                    {
                        InstanceId = item.InstanceId, ContainerType = item.ContainerType, ContainerInstance = this.CharacterId,
                        ContainerPlacement = item.ContainerPlacement, StackCount = stack.FinalCount
                    });
                }
                MySqlCharacterPersistenceDao.WriteInventoryMutation(this.connection, this.transaction, mutation);
            }

            private bool ValidStarterItem(MissionItemInstanceData item)
                => item != null && item.ContainerInstance == this.CharacterId && item.InstanceId > 0 && item.ContainerPlacement >= 0
                    && item.LowId > 0 && item.HighId > 0 && item.Quality > 0 && item.StackCount > 0;

            private static PersistedItemData ToPersistedStarterItem(MissionItemInstanceData item) => new PersistedItemData
            {
                InstanceId = item.InstanceId, ContainerType = item.ContainerType, ContainerInstance = item.ContainerInstance,
                ContainerPlacement = item.ContainerPlacement, ItemType = item.ItemType, LowId = item.LowId,
                HighId = item.HighId, Quality = item.Quality, StackCount = item.StackCount, Source = item.Source
            };
        }
    }
}
