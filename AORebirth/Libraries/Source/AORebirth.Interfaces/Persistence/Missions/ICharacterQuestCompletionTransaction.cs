namespace AORebirth.Interfaces.Persistence.Missions
{
    using System.Collections.Generic;

    /// <summary>Atomic completion for the existing characterquests owner, inside IMissionDao.Execute.</summary>
    public interface ICharacterQuestCompletionTransaction
    {
        CharacterQuestCompletionSnapshotData ReadCharacterQuest(string questId);
        CharacterQuestCompletionResult TryEarnCharacterQuest(CharacterQuestEarnRequest request);
        CharacterQuestCompletionResult TryCompleteCharacterQuest(CharacterQuestCompletionRequest request);
    }

    /// <summary>Collects an exact return-mission item without earning its independent completion reward.</summary>
    public interface ICharacterQuestObjectiveCollectionTransaction
    {
        CharacterQuestCompletionResult TryCollectCharacterQuestItem(CharacterQuestEarnRequest request);
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

    /// <summary>Stable source codes stored in characterquests.Source.</summary>
    public enum CharacterQuestSourceCode
    {
        Template = 0,
        Generated = 1
    }

    /// <summary>Stable state codes stored in characterquests.State.</summary>
    public enum CharacterQuestStateCode
    {
        Active = 1,
        Completed = 2,
        Failed = 3,
        Expired = 4,
        Abandoned = 5
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
        public CharacterQuestSuccessorRequest Successor { get; set; }
        public IReadOnlyList<MissionItemInstanceData> Grants { get; set; } = new MissionItemInstanceData[0];
        public IList<MissionStatMutationData> StatMutations { get; set; } = new MissionStatMutationData[0];
        public IList<MissionStatValueData> ExpectedStatValues { get; set; } = new MissionStatValueData[0];
    }

    /// <summary>A template step to assign with the completion of its predecessor.</summary>
    public sealed class CharacterQuestSuccessorRequest
    {
        public string QuestId { get; set; }
        public int RequiredCount { get; set; }

        /// <summary>A fresh chain can restart an older closed template row. A current-run row is left alone.</summary>
        public static bool CanAssign(CharacterQuestCompletionSnapshotData current, long predecessorAssignedAtUtcTicks)
        {
            if (current == null)
                return true;
            if (current.Source != (int)CharacterQuestSourceCode.Template
                || current.AssignedAtUtcTicks >= predecessorAssignedAtUtcTicks)
                return false;

            switch ((CharacterQuestStateCode)current.State)
            {
                case CharacterQuestStateCode.Completed:
                case CharacterQuestStateCode.Failed:
                case CharacterQuestStateCode.Expired:
                case CharacterQuestStateCode.Abandoned:
                    return true;
                default:
                    return false;
            }
        }
    }

    public sealed class CharacterQuestCompletionResult
    {
        public CharacterQuestCompletionStatus Status { get; set; }
        public CharacterQuestCompletionSnapshotData Quest { get; set; }
        public IList<MissionStatValueData> StatValues { get; set; } = new MissionStatValueData[0];
        public string Reason { get; set; }
    }
}
