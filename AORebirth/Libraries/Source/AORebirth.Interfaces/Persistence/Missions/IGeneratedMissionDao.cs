namespace AORebirth.Interfaces.Persistence.Missions
{
    using System.Collections.Generic;

    public enum GeneratedMissionState { Offered = 1, Active = 2, Completed = 3, Abandoned = 4, Expired = 5, Replaced = 6 }
    public enum GeneratedMissionResultStatus { Applied = 1, AlreadyApplied = 2, Rejected = 3 }

    public sealed class MissionCommitOutcomeUnknownException : System.Exception
    {
        public MissionCommitOutcomeUnknownException(System.Exception inner)
            : base("Mission transaction commit outcome is unknown; quarantine the character and reconcile before another write.", inner) { }
    }

    /// <summary>
    /// Optional capability of the existing mission DAO. Generated terminal missions use typed
    /// SQL rows; authored quests continue using IMissionDao without a second persistence owner.
    /// FrozenWireBody is the server-generated protocol projection, never a capture path or JSON ledger.
    /// </summary>
    public interface IGeneratedMissionDao
    {
        int ReserveIdentities(string sequence, int count);
        GeneratedMissionResult PublishOffers(GeneratedMissionOfferBatch batch);
        GeneratedMissionResult Accept(GeneratedMissionAcceptance acceptance);
        IList<GeneratedMissionOffer> ReadOffers(int ownerId);
        IList<GeneratedMissionBinding> ReadAccepted(int ownerId);
        GeneratedMissionBinding ReadAccepted(int ownerId, int questType, int questInstance);
        GeneratedMissionResult Observe(GeneratedMissionObservation observation);
        GeneratedMissionResult Complete(GeneratedMissionCompletion completion);
        GeneratedMissionResult End(int ownerId, int questType, int questInstance, GeneratedMissionState state, long nowUtcTicks);
        GeneratedMissionResult AdvanceCleanup(int ownerId, int questType, int questInstance, long expectedVersion, long checkpointMask, long nowUtcTicks);
        IList<GeneratedMissionObject> ReadObjects(int ownerId, int questType, int questInstance);
        GeneratedMissionResult UpdateObjects(int ownerId, int questType, int questInstance, IList<GeneratedMissionObject> objects, long nowUtcTicks);
        GeneratedMissionResult SavePosition(int ownerId, int questType, int questInstance, int livePlayfield, float x, float y, float z, long nowUtcTicks);
        IList<MissionItemInstanceData> ReadArtifacts(int ownerId, int questType, int questInstance);
        GeneratedMissionResult CleanupArtifacts(int ownerId, int questType, int questInstance, long nowUtcTicks);
        GeneratedMissionResult ClaimCorpseCredits(int ownerId, int questType, int questInstance, int runtimeNpcInstance, int currentCash, long nowUtcTicks);
    }

    public sealed class GeneratedMissionOfferBatch
    {
        public int OwnerType { get; set; }
        public int OwnerId { get; set; }
        public string BatchIdentity { get; set; }
        public int RollSeed { get; set; }
        public int ResponseNonce { get; set; }
        public int Fee { get; set; }
        public int CurrentCash { get; set; }
        public int TerminalType { get; set; }
        public int TerminalInstance { get; set; }
        public int TerminalPlayfield { get; set; }
        public int LevelSlider { get; set; }
        public int GoodBadSlider { get; set; }
        public int OrderChaosSlider { get; set; }
        public int OpenHiddenSlider { get; set; }
        public int PhysicalMysticalSlider { get; set; }
        public int HeadOnStealthSlider { get; set; }
        public int MoneyExperienceSlider { get; set; }
        public long OfferedAtUtcTicks { get; set; }
        public long ExpiresAtUtcTicks { get; set; }
        public IList<GeneratedMissionOffer> Offers { get; set; }
    }

    public sealed class GeneratedMissionOffer
    {
        // Hydrated from the normalized parent batch, never from captured/client wire.
        public int IssuingTerminalType { get; set; }
        public int IssuingTerminalInstance { get; set; }
        public int IssuingTerminalPlayfield { get; set; }
        public int OwnerId { get; set; }
        public string BatchIdentity { get; set; }
        public int OfferIndex { get; set; }
        public int OfferType { get; set; }
        public int OfferInstance { get; set; }
        public int MissionType { get; set; }
        public int Quality { get; set; }
        public int DestinationType { get; set; }
        public int DestinationInstance { get; set; }
        public int DestinationPlayfield { get; set; }
        public float DestinationX { get; set; }
        public float DestinationY { get; set; }
        public float DestinationZ { get; set; }
        public int EntranceType { get; set; }
        public int EntranceInstance { get; set; }
        public int EntranceLow { get; set; }
        public int EntranceHigh { get; set; }
        public int CashReward { get; set; }
        public int ExperienceReward { get; set; }
        public int RewardLowId { get; set; }
        public int RewardHighId { get; set; }
        public int RewardQuality { get; set; }
        public int RewardCount { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public byte[] FrozenWireBody { get; set; }
        public string FrozenWireSha256 { get; set; }
        public GeneratedMissionState State { get; set; }
        public long OfferedAtUtcTicks { get; set; }
        public long ExpiresAtUtcTicks { get; set; }
        public long Version { get; set; }
    }

    public sealed class GeneratedMissionAcceptance
    {
        public int OwnerId { get; set; }
        public int OfferType { get; set; }
        public int OfferInstance { get; set; }
        public int QuestType { get; set; }
        public int QuestInstance { get; set; }
        public int TeamType { get; set; }
        public int TeamInstance { get; set; }
        public int KeyInstance { get; set; }
        public string BundleId { get; set; }
        public string BundleSha256 { get; set; }
        public int BuildingType { get; set; }
        public int BuildingInstance { get; set; }
        public int LivePlayfield { get; set; }
        public int ObjectiveType { get; set; }
        public int ObjectiveInstance { get; set; }
        public int ObjectiveTemplateId { get; set; }
        public int ObjectiveInteraction { get; set; }
        public int RequiredCount { get; set; }
        public long AcceptedAtUtcTicks { get; set; }
        public long ExpiresAtUtcTicks { get; set; }
        public IList<MissionItemInstanceData> Artifacts { get; set; }
        public IList<GeneratedMissionObject> Objects { get; set; }
    }

    public sealed class GeneratedMissionBinding
    {
        public int OwnerId { get; set; }
        public int OfferType { get; set; }
        public int OfferInstance { get; set; }
        public int QuestType { get; set; }
        public int QuestInstance { get; set; }
        public int TeamType { get; set; }
        public int TeamInstance { get; set; }
        public int KeyInstance { get; set; }
        public string BundleId { get; set; }
        public string BundleSha256 { get; set; }
        public int BuildingType { get; set; }
        public int BuildingInstance { get; set; }
        public int LivePlayfield { get; set; }
        public int ObjectiveType { get; set; }
        public int ObjectiveInstance { get; set; }
        public int ObjectiveTemplateId { get; set; }
        public int ObjectiveInteraction { get; set; }
        public int RequiredCount { get; set; }
        public int Progress { get; set; }
        public GeneratedMissionState State { get; set; }
        public long CleanupCheckpoints { get; set; }
        public long AcceptedAtUtcTicks { get; set; }
        public long ExpiresAtUtcTicks { get; set; }
        public long CompletedAtUtcTicks { get; set; }
        public long UpdatedAtUtcTicks { get; set; }
        public long Version { get; set; }
        public GeneratedMissionOffer Offer { get; set; }
        // Derived from normalized artifact/item rows, never an independent binding ledger.
        public MissionItemInstanceData MissionItem { get; set; }
        public int? TokenProgressPercent { get; set; }
        public int? TokenClaimLevel { get; set; }
        public int? TokenClaimSide { get; set; }
        public int? TokenDisposition { get; set; }
        public int? TokenCount { get; set; }
        public long CompletionFrozenAtUtcTicks { get; set; }
    }

    public sealed class GeneratedMissionObservation
    {
        public int OwnerId { get; set; }
        public int QuestType { get; set; }
        public int QuestInstance { get; set; }
        public int LivePlayfield { get; set; }
        public int ObjectiveType { get; set; }
        public int ObjectiveInstance { get; set; }
        public int ObjectiveTemplateId { get; set; }
        public int Interaction { get; set; }
        public string ObservationIdentity { get; set; }
        public long ObservedAtUtcTicks { get; set; }
        public bool AdvanceProgress { get; set; } = true;
        public IList<GeneratedMissionObject> Objects { get; set; } = new List<GeneratedMissionObject>();
        public IList<MissionItemInstanceData> Grants { get; set; } = new List<MissionItemInstanceData>();
        public MissionItemInstanceData ConsumeItem { get; set; }
        public int TerminalType { get; set; }
        public int TerminalInstance { get; set; }
        public int ActualPlayfield { get; set; }
        public GeneratedMissionTokenClaim CompletionToken { get; set; }
    }

    public sealed class GeneratedMissionTokenClaim
    {
        public int ProgressPercent { get; set; }
        public int CharacterLevel { get; set; }
        public int Side { get; set; }
        public int Disposition { get; set; }
        public int Count { get; set; }
    }

    public sealed class GeneratedMissionCompletion
    {
        public int OwnerId { get; set; }
        public int QuestType { get; set; }
        public int QuestInstance { get; set; }
        public int CharacterType { get; set; }
        public int CurrentCash { get; set; }
        public int CurrentExperience { get; set; }
        public int RequestedExperienceReward { get; set; }
        public int CurrentLevel { get; set; }
        public int FinalExperience { get; set; }
        public IList<MissionStatValueData> ProgressionStats { get; set; } = new List<MissionStatValueData>();
        public long CompletedAtUtcTicks { get; set; }
        public IList<MissionItemInstanceData> Items { get; set; }
        public MissionItemInstanceData TokenItem { get; set; }
    }

    public sealed class MissionItemInstanceData
    {
        public int InstanceId { get; set; }
        public int ContainerType { get; set; }
        public int ContainerInstance { get; set; }
        public int ContainerPlacement { get; set; }
        public int ItemType { get; set; }
        public int LowId { get; set; }
        public int HighId { get; set; }
        public int Quality { get; set; }
        public int StackCount { get; set; }
        public byte Source { get; set; }
    }

    public sealed class GeneratedMissionResult
    {
        public GeneratedMissionResultStatus Status { get; set; }
        public string Reason { get; set; }
        public GeneratedMissionBinding Binding { get; set; }
        public int Cash { get; set; }
        public int Experience { get; set; }
    }

    /// <summary>Typed world state, bound to immutable frozen-bundle identities and templates.</summary>
    public sealed class GeneratedMissionObject
    {
        public int OwnerId { get; set; }
        public int QuestType { get; set; }
        public int QuestInstance { get; set; }
        public int RuntimeType { get; set; }
        public int RuntimeInstance { get; set; }
        public int CapturedType { get; set; }
        public int CapturedInstance { get; set; }
        public int Kind { get; set; }
        public int TemplateId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float HeadingX { get; set; }
        public float HeadingY { get; set; }
        public float HeadingZ { get; set; }
        public float HeadingW { get; set; }
        public int? CurrentHealth { get; set; }
        public int? Level { get; set; }
        public int? MaxHealth { get; set; }
        public bool IsDead { get; set; }
        public bool IsOpen { get; set; }
        public bool IsLocked { get; set; }
        public bool LootResolved { get; set; }
        public bool ObjectiveConsumed { get; set; }
        public int DeathActorId { get; set; }
        public long DiedAtUtcTicks { get; set; }
        public int CorpseCredits { get; set; }
        public bool CorpseClaimed { get; set; }
        public long CorpseExpiresAtUtcTicks { get; set; }
        public long Version { get; set; }
        public long UpdatedAtUtcTicks { get; set; }
    }
}
