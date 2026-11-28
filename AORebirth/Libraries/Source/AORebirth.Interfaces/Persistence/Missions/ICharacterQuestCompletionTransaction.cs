namespace AORebirth.Interfaces.Persistence.Missions
{
    using System.Collections.Generic;

    /// <summary>Atomic completion for the existing characterquests owner, inside IMissionDao.Execute.</summary>
    public interface ICharacterQuestCompletionTransaction
    {
        CharacterQuestCompletionSnapshotData ReadCharacterQuest(string questId);
        CharacterQuestCompletionResult TryEarnCharacterQuest(CharacterQuestEarnRequest request);
        CharacterQuestCompletionResult TryCompleteCharacterQuest(CharacterQuestCompletionRequest request);
        CharacterQuestCompletionResult TryApplyCharacterQuestFollowUp(CharacterQuestFollowUpRequest request);
    }

    /// <summary>Collects an exact return-mission item without earning its independent completion reward.</summary>
    public interface ICharacterQuestObjectiveCollectionTransaction
    {
        CharacterQuestCompletionResult TryCollectCharacterQuestItem(CharacterQuestEarnRequest request);
    }

    /// <summary>Reads pending next-step stages from the same mission DAO ledger during character restoration.</summary>
    public interface ICharacterQuestFollowUpReader
    {
        IReadOnlyList<MissionRewardStageData> GetPendingCharacterQuestFollowUps(int characterId);
    }

    /// <summary>Extends an authored stat reward's existing ledger transaction with its research projection.</summary>
    public interface IMissionRewardResearchTransaction
    {
        MissionAtomicStatRewardResultData TryApplyCharacterStatRewardWithResearch(MissionRewardKeyData key,
            string rewardType, IList<MissionStatMutationData> mutations, string effectReference, long appliedAtUtcTicks,
            MissionResearchMutationData research);
    }

    /// <summary>An expected-value change to one research tier. Zero remaining completes the tier and trains its perk.</summary>
    public sealed class MissionResearchMutationData
    {
        public int ResearchId { get; set; }
        public int? ExpectedRemainingXp { get; set; }
        public int RemainingXp { get; set; }
    }

    public enum CharacterQuestCompletionStatus
    {
        Rejected = 0,
        Earned = 1,
        AlreadyEarned = 2,
        Applied = 3,
        AlreadyApplied = 4,
        Collected = 5,
        AlreadyCollected = 6
    }

    public sealed class CharacterQuestCompletionSnapshotData
    {
        public int CharacterId { get; set; }
        public string QuestId { get; set; }
        public int Source { get; set; }
        public int State { get; set; }
        public int Progress { get; set; }
        public int RequiredCount { get; set; }
        public long AssignedAtUtcTicks { get; set; }
        public long UpdatedAtUtcTicks { get; set; }
        public int OwnerType { get; set; }
        public int OwnerId { get; set; }
        public long ExpiresAtUtcTicks { get; set; }
        public string DefinitionJson { get; set; }
        public string AcgBuildingGeneratorJson { get; set; }
    }

    public sealed class CharacterQuestEarnRequest
    {
        public string QuestId { get; set; }
        public long AssignedAtUtcTicks { get; set; }
        public string ExpectedDefinitionJson { get; set; }
        public string ExpectedAcgBuildingGeneratorJson { get; set; }
        public long NowUtcTicks { get; set; }
        public IReadOnlyList<MissionItemInstanceData> Grants { get; set; } = new MissionItemInstanceData[0];
        public IReadOnlyList<MissionItemInstanceData> Removals { get; set; } = new MissionItemInstanceData[0];
    }

    public sealed class CharacterQuestCompletionRequest
    {
        public string QuestId { get; set; }
        public long AssignedAtUtcTicks { get; set; }
        public string ExpectedDefinitionJson { get; set; }
        public string ExpectedAcgBuildingGeneratorJson { get; set; }
        public long NowUtcTicks { get; set; }
        public string FollowUpQuestId { get; set; }
        public IReadOnlyList<MissionItemInstanceData> Grants { get; set; } = new MissionItemInstanceData[0];
        public IList<MissionStatMutationData> StatMutations { get; set; } = new MissionStatMutationData[0];
        public IList<MissionStatValueData> ExpectedStatValues { get; set; } = new MissionStatValueData[0];
        public MissionResearchMutationData ResearchMutation { get; set; }
    }

    /// <summary>The next template assignment and its starter items consume one existing reward ledger stage.</summary>
    public sealed class CharacterQuestFollowUpRequest
    {
        public MissionRewardKeyData Key { get; set; }
        public string QuestId { get; set; }
        public int RequiredCount { get; set; }
        public long NowUtcTicks { get; set; }
        public bool ExpectedActive { get; set; }
        public IReadOnlyList<MissionItemInstanceData> StarterGrants { get; set; } = new MissionItemInstanceData[0];
        public IReadOnlyList<MissionItemInstanceData> RetiredOverflow { get; set; } = new MissionItemInstanceData[0];
        public IReadOnlyList<CharacterQuestStarterStack> StackMerges { get; set; } = new CharacterQuestStarterStack[0];
    }

    public sealed class CharacterQuestStarterStack
    {
        public MissionItemInstanceData Existing { get; set; }
        public int FinalCount { get; set; }
    }

    public sealed class CharacterQuestCompletionResult
    {
        public CharacterQuestCompletionStatus Status { get; set; }
        public CharacterQuestCompletionSnapshotData Quest { get; set; }
        public IList<MissionStatValueData> StatValues { get; set; } = new MissionStatValueData[0];
        public MissionRewardStageData FollowUp { get; set; }
        public string Reason { get; set; }
    }
}
