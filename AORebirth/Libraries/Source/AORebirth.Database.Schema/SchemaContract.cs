namespace AORebirth.Database.Schema;

public enum SchemaState { SCHEMA_CURRENT, SCHEMA_MIGRATION_REQUIRED, SCHEMA_INCOMPATIBLE, DATABASE_UNREACHABLE }

public sealed record SchemaCheckResult(SchemaState State, string Message, IReadOnlyList<string> PendingMigrations)
{
    public bool IsCurrent => State == SchemaState.SCHEMA_CURRENT;
}

public sealed record ColumnRequirement(string Table, string Column, string DataType, bool? Unsigned = null);
public sealed record SchemaColumn(string Table, string Column, string DataType, string ColumnType);
public sealed record SchemaIndex(string Table, string Name, bool Unique, string Columns);
public sealed record SchemaSnapshot(
    IReadOnlyList<SchemaColumn> Columns,
    IReadOnlyList<SchemaIndex> Indexes,
    IReadOnlyList<string> AppliedMigrations,
    long? NextInstanceId,
    long MaximumInstanceId,
    IReadOnlyDictionary<string, string> TableEngines);

/// <summary>Versioned minimum contract actually consumed by the NewEngine repositories.</summary>
public static class SchemaContract
{
    public static readonly IReadOnlyList<string> MigrationNames = Array.AsReadOnly(new[]
    {
        "20260904_item_instances_from_legacy.sql",
        "20260905_item_instance_id_sequence.sql",
        "20260906_item_instances_source.sql",
        "20260908_generated_mission_state.sql"
    });

    // Other governed components may legitimately record these unrelated migrations.
    public static readonly IReadOnlyList<string> OtherKnownMigrations = Array.AsReadOnly(new[]
    {
        "20260816_account_email_verification_tokens.sql",
        "20260831_account_password_reset_tokens.sql"
    });

    public static readonly IReadOnlyList<ColumnRequirement> Columns = BuildColumns();

    // Mike explicitly approved the additive generated-mission schema on 2026-09-08.
    // Existing authored mission/reward tables remain baseline-owned, never bootstrapped here.
    public static readonly IReadOnlyList<string> MigrationOwnedTables = Array.AsReadOnly(new[]
    {
        "item_instances", "item_instance_id_sequence", "schema_migrations",
        "generatedmissionsequences", "generatedmissionbatches", "generatedmissionoffers",
        "generatedmissionbindings", "generatedmissionobservations", "generatedmissionartifacts", "generatedmissionobjects"
    });

    public static readonly IReadOnlyList<(string Table, string Columns)> UniqueKeys = Array.AsReadOnly(new[]
    {
        ("item_instances", "InstanceId"), ("item_instances", "ContainerType,ContainerInstance,ContainerPlacement"),
        ("item_instance_id_sequence", "Id"), ("schema_migrations", "MigrationName"), ("stats", "Type,Instance,StatId"),
        ("missionrewardledger", "CharacterId,QuestId,RewardKey"), ("charactersactivenanos", "Id"), ("generatedmissionsequences", "SequenceName"),
        ("missionstates", "CharacterId,QuestId"), ("missionobjectiveprogress", "CharacterId,QuestId,ObjectiveId"),
        ("missionflags", "CharacterId,QuestId,FlagKey"), ("missionaccountflags", "AccountKey,FlagKey"),
        ("missionobjectiveobservations", "CharacterId,QuestId,ObjectiveId,ObservationKey"),
        ("generatedmissionbatches", "OwnerId,BatchIdentity"), ("generatedmissionbatches", "BatchIdentity"),
        ("generatedmissionoffers", "OfferType,OfferInstance"), ("generatedmissionoffers", "OwnerId,BatchIdentity,OfferIndex"),
        ("generatedmissionbindings", "QuestType,QuestInstance"), ("generatedmissionbindings", "OwnerId,OfferType,OfferInstance"),
        ("generatedmissionbindings", "ActivePlayfield"), ("generatedmissionbindings", "KeyInstance"),
        ("generatedmissionobservations", "OwnerId,QuestType,QuestInstance,ObservationIdentity"),
        ("generatedmissionartifacts", "InstanceId"), ("generatedmissionobjects", "QuestType,QuestInstance,RuntimeType,RuntimeInstance"),
        ("generatedmissionobjects", "RuntimeType,RuntimeInstance")
    });

    private static IReadOnlyList<ColumnRequirement> BuildColumns()
    {
        var result = new List<ColumnRequirement>();
        void Add(string table, string type, string names, bool? unsigned = null)
        {
            result.AddRange(names.Split(' ').Select(name => new ColumnRequirement(table, name, type, unsigned)));
        }
        Add("characters", "int", "Id Playfield");
        Add("characters", "varchar", "Name FirstName LastName Username");
        Add("characters", "float", "X Y Z HeadingW HeadingX HeadingY HeadingZ");
        Add("characters", "smallint", "Online");
        Add("stats", "int", "Type Instance StatId StatValue");
        Add("charactersuploadednanos", "int", "CharacterId NanoId");
        Add("charactersactivenanos", "int", "Id CharacterId NanoInstance DurationCentiseconds", false);
        Add("charactersactivenanos", "int", "NanoId Strain", true);
        Add("charactersactivenanos", "bigint", "ExpiresAtUtcTicks", false);
        Add("itemnames", "int", "Id");
        Add("itemnames", "varchar", "Name");
        Add("item_instances", "int", "InstanceId ContainerType ContainerInstance ContainerPlacement ItemType LowId HighId Quality StackCount", false);
        Add("item_instances", "tinyint", "Source", true);
        Add("item_instance_id_sequence", "tinyint", "Id", false);
        Add("item_instance_id_sequence", "int", "NextInstanceId", false);
        Add("schema_migrations", "varchar", "MigrationName");
        Add("schema_migrations", "datetime", "AppliedAtUtc");
        Add("missionrewardledger", "int", "CharacterId Status Attempts");
        Add("missionrewardledger", "varchar", "QuestId RewardKey RewardType EffectReference LastError ClaimToken");
        Add("missionrewardledger", "bigint", "AppliedAtUtcTicks CreatedAtUtcTicks UpdatedAtUtcTicks Version ClaimedAtUtcTicks ClaimExpiresAtUtcTicks");
        // Existing authored tables are required baseline dependencies, not migration-owned tables.
        Add("missionstates", "int", "CharacterId State");
        Add("missionstates", "varchar", "QuestId CurrentStepId");
        Add("missionstates", "bigint", "OfferedAtUtcTicks AcceptedAtUtcTicks CompletedAtUtcTicks FailedAtUtcTicks AbandonedAtUtcTicks CreatedAtUtcTicks UpdatedAtUtcTicks Version");
        Add("missionobjectiveprogress", "int", "CharacterId Progress RequiredCount");
        Add("missionobjectiveprogress", "varchar", "QuestId ObjectiveId LastObservationKey");
        Add("missionobjectiveprogress", "bigint", "CreatedAtUtcTicks UpdatedAtUtcTicks Version");
        Add("missionflags", "int", "CharacterId");
        Add("missionflags", "varchar", "QuestId FlagKey Value");
        Add("missionflags", "bigint", "CreatedAtUtcTicks UpdatedAtUtcTicks Version");
        Add("missionaccountflags", "varchar", "AccountKey FlagKey Value SourceQuestId");
        Add("missionaccountflags", "bigint", "CreatedAtUtcTicks UpdatedAtUtcTicks Version");
        Add("missionobjectiveobservations", "int", "CharacterId");
        Add("missionobjectiveobservations", "varchar", "QuestId ObjectiveId ObservationKey EventType SourceIdentity TargetIdentity");
        Add("missionobjectiveobservations", "bigint", "ObservedAtUtcTicks");
        Add("generatedmissionsequences", "varchar", "SequenceName");
        Add("generatedmissionsequences", "int", "NextIdentity MaximumIdentity", false);
        Add("generatedmissionbatches", "varchar", "BatchIdentity");
        Add("generatedmissionbatches", "int", "OwnerId OwnerType RollSeed ResponseNonce Fee TerminalType TerminalInstance TerminalPlayfield LevelSlider GoodBadSlider OrderChaosSlider OpenHiddenSlider PhysicalMysticalSlider HeadOnStealthSlider MoneyExperienceSlider CashBefore CashAfter", false);
        Add("generatedmissionbatches", "bigint", "OfferedAtUtcTicks ExpiresAtUtcTicks", false);
        Add("generatedmissionoffers", "varchar", "BatchIdentity Title");
        Add("generatedmissionoffers", "text", "Description");
        Add("generatedmissionoffers", "char", "FrozenWireSha256");
        Add("generatedmissionoffers", "mediumblob", "FrozenWireBody");
        Add("generatedmissionoffers", "int", "OwnerId OfferIndex OfferType OfferInstance MissionType Quality DestinationType DestinationInstance DestinationPlayfield EntranceType EntranceInstance EntranceLow EntranceHigh CashReward ExperienceReward RewardLowId RewardHighId RewardQuality RewardCount State", false);
        Add("generatedmissionoffers", "float", "DestinationX DestinationY DestinationZ");
        Add("generatedmissionoffers", "bigint", "OfferedAtUtcTicks ExpiresAtUtcTicks Version", false);
        Add("generatedmissionbindings", "varchar", "BundleId");
        Add("generatedmissionbindings", "char", "BundleSha256");
        Add("generatedmissionbindings", "int", "OwnerId OfferType OfferInstance QuestType QuestInstance TeamType TeamInstance KeyInstance BuildingType BuildingInstance LivePlayfield ActivePlayfield ObjectiveType ObjectiveInstance ObjectiveTemplateId ObjectiveInteraction RequiredCount Progress State", false);
        Add("generatedmissionbindings", "bigint", "CleanupCheckpoints AcceptedAtUtcTicks ExpiresAtUtcTicks CompletedAtUtcTicks UpdatedAtUtcTicks Version", false);
        Add("generatedmissionbindings", "int", "TokenProgressPercent TokenClaimLevel TokenClaimSide TokenDisposition TokenCount", false);
        Add("generatedmissionbindings", "bigint", "CompletionFrozenAtUtcTicks", false);
        Add("generatedmissionobservations", "varchar", "ObservationIdentity");
        Add("generatedmissionobservations", "int", "OwnerId QuestType QuestInstance LivePlayfield ObjectiveType ObjectiveInstance ObjectiveTemplateId Interaction", false);
        Add("generatedmissionobservations", "bigint", "ObservedAtUtcTicks", false);
        Add("generatedmissionartifacts", "int", "OwnerId QuestType QuestInstance InstanceId ArtifactRole", false);
        Add("generatedmissionartifacts", "bigint", "CreatedAtUtcTicks", false);
        Add("generatedmissionobjects", "int", "OwnerId QuestType QuestInstance RuntimeType RuntimeInstance CapturedType CapturedInstance Kind TemplateId CurrentHealth MaxHealth Level", false);
        Add("generatedmissionobjects", "float", "X Y Z HeadingX HeadingY HeadingZ HeadingW");
        Add("generatedmissionobjects", "int", "DeathActorId CorpseCredits", false);
        Add("generatedmissionobjects", "tinyint", "IsDead IsOpen IsLocked LootResolved ObjectiveConsumed CorpseClaimed", false);
        Add("generatedmissionobjects", "bigint", "Version UpdatedAtUtcTicks DiedAtUtcTicks CorpseExpiresAtUtcTicks", false);
        return result.AsReadOnly();
    }
}
