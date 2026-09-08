using AORebirth.Database.Domain.Missions;
using AORebirth.Interfaces.Persistence.Missions;
using MySqlConnector;
using ZoneEngine_New.Core.Data;

/// <summary>Existing authored DAO contracts, exercised only inside the harness-owned database.</summary>
static class AuthoredMissionSmoke
{
    const int Owner = 9701, CharacterType = 50000;
    const string Account = "disposable-authored";
    const long Now = 639200000000000000;

    public static void CreateBaseline(MySqlConnection connection)
    {
        // Exact consumed definitions from Database/SqlTables, not a runtime migration.
        FixtureSql.Execute(connection, "ALTER TABLE characters ADD Username VARCHAR(32) NOT NULL DEFAULT ''");
        FixtureSql.Execute(connection, """
            CREATE TABLE missionstates (
              Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, CharacterId INT NOT NULL, QuestId VARCHAR(128) NOT NULL,
              State INT NOT NULL, CurrentStepId VARCHAR(128) NULL, OfferedAtUtcTicks BIGINT NOT NULL DEFAULT 0,
              AcceptedAtUtcTicks BIGINT NOT NULL DEFAULT 0, CompletedAtUtcTicks BIGINT NOT NULL DEFAULT 0,
              FailedAtUtcTicks BIGINT NOT NULL DEFAULT 0, AbandonedAtUtcTicks BIGINT NOT NULL DEFAULT 0,
              CreatedAtUtcTicks BIGINT NOT NULL, UpdatedAtUtcTicks BIGINT NOT NULL, Version BIGINT NOT NULL DEFAULT 1,
              UNIQUE KEY character_quest(CharacterId,QuestId), KEY `character`(CharacterId)
            ) COLLATE=latin1_general_ci ENGINE=InnoDB;
            CREATE TABLE missionobjectiveprogress (
              Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, CharacterId INT NOT NULL, QuestId VARCHAR(128) NOT NULL,
              ObjectiveId VARCHAR(128) NOT NULL, Progress INT NOT NULL DEFAULT 0, RequiredCount INT NOT NULL DEFAULT 0,
              LastObservationKey VARCHAR(191) NULL, CreatedAtUtcTicks BIGINT NOT NULL, UpdatedAtUtcTicks BIGINT NOT NULL,
              Version BIGINT NOT NULL DEFAULT 1, UNIQUE KEY character_quest_objective(CharacterId,QuestId,ObjectiveId),
              KEY character_quest(CharacterId,QuestId)
            ) COLLATE=latin1_general_ci ENGINE=InnoDB;
            CREATE TABLE missionflags (
              Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, CharacterId INT NOT NULL, QuestId VARCHAR(128) NOT NULL,
              FlagKey VARCHAR(128) NOT NULL, `Value` VARCHAR(1024) NULL, CreatedAtUtcTicks BIGINT NOT NULL,
              UpdatedAtUtcTicks BIGINT NOT NULL, Version BIGINT NOT NULL DEFAULT 1,
              UNIQUE KEY character_quest_flag(CharacterId,QuestId,FlagKey), KEY character_quest(CharacterId,QuestId)
            ) COLLATE=latin1_general_ci ENGINE=InnoDB;
            CREATE TABLE missionobjectiveobservations (
              Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, CharacterId INT NOT NULL, QuestId VARCHAR(128) NOT NULL,
              ObjectiveId VARCHAR(128) NOT NULL, ObservationKey VARCHAR(191) NOT NULL, EventType VARCHAR(64) NOT NULL,
              SourceIdentity VARCHAR(64) NULL, TargetIdentity VARCHAR(64) NULL, ObservedAtUtcTicks BIGINT NOT NULL,
              UNIQUE KEY character_quest_objective_observation(CharacterId,QuestId,ObjectiveId,ObservationKey),
              KEY character_quest(CharacterId,QuestId)
            ) COLLATE=latin1_general_ci ENGINE=InnoDB;
            CREATE TABLE missionaccountflags (
              Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, AccountKey VARCHAR(32) NOT NULL, FlagKey VARCHAR(128) NOT NULL,
              `Value` VARCHAR(1024) NULL, SourceQuestId VARCHAR(128) NULL, CreatedAtUtcTicks BIGINT NOT NULL,
              UpdatedAtUtcTicks BIGINT NOT NULL, Version BIGINT NOT NULL DEFAULT 1,
              UNIQUE KEY account_flag(AccountKey,FlagKey), KEY account(AccountKey)
            ) COLLATE=latin1_general_ci ENGINE=InnoDB;
            """);
    }

    public static void Validate(DisposableSchemaDatabase fixture, MySqlConnection connection)
    {
        IMissionDao dao = new MySqlMissionDao(() => fixture.Open());
        FixtureSql.Execute(connection, $"INSERT INTO characters (Id,Username,Name,Online) VALUES ({Owner},'{Account}','DisposableAuthored',0); INSERT INTO stats (Type,Instance,StatId,StatValue) VALUES ({CharacterType},{Owner},61,10),({CharacterType},{Owner},52,10)");
        int first;
        string? previous = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", fixture.ConnectionString);
            var inventory = new MySqlInventoryRepository(new SilentLogger());
            first = inventory.LeaseInstanceIdBlock(4);
            inventory.Insert(new ItemInstanceRecord
            {
                InstanceId = first, ContainerType = 104, ContainerInstance = Owner, ContainerPlacement = 64,
                ItemType = 0, LowId = 248258, HighId = 248258, Quality = 1, StackCount = 1, Source = (AORebirth.Enums.ItemSource)1
            });
        }
        finally { Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", previous); }

        var consumed = Row(first, 64, 248258);
        var grant = Row(first + 1, 65, 43384);
        const string quest = "disposable-authored-package";
        FixtureSql.Execute(connection, $"ALTER TABLE stats ADD CONSTRAINT fixture_authored_xp CHECK (Instance <> {Owner} OR StatId <> 52 OR StatValue <= 10)");
        ExpectUnchanged(connection, () => dao.Execute(Owner, Account, tx => Apply(tx, quest, consumed, grant)),
            e => e is MySqlException mysql && mysql.Number == 3819, "authored-late-stat-rollback");
        FixtureSql.Execute(connection, "ALTER TABLE stats DROP CHECK fixture_authored_xp");
        Require(dao.Execute(Owner, Account, tx => Apply(tx, quest, consumed, grant)) == MissionAtomicRewardStatus.Applied,
            "authored-atomic-package-commit");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM item_instances WHERE InstanceId={first} AND ContainerType=0 AND ContainerPlacement={first} AND LowId=248258 AND StackCount=1 AND Source=1") == 1,
            "authored-source-history-lost");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM item_instances WHERE InstanceId={first + 1} AND ContainerType=104 AND ContainerInstance={Owner} AND ContainerPlacement=65 AND LowId=43384 AND StackCount=1 AND Source=1") == 1,
            "authored-grant-identity-location-lost");
        Require(FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM stats WHERE Instance={Owner} AND ((StatId=61 AND StatValue=201) OR (StatId=52 AND StatValue=202))") == 2,
            "authored-final-stats-not-atomic");
        var restarted = new MySqlMissionDao(() => fixture.Open());
        Require(((IMissionDao)restarted).GetMission(new MissionKeyData(Owner, quest)).State == MissionLifecycleState.Completed,
            "authored-mission-state-not-restart-durable");
        string before = Fingerprint(connection);
        Require(dao.Execute(Owner, Account, tx => Reward(tx, quest)) == MissionAtomicRewardStatus.AlreadyApplied,
            "authored-reward-replay-not-idempotent");
        Require(before == Fingerprint(connection), "authored-reward-replay-mutated-database");

        bool entered = false;
        ExpectUnchanged(connection, () => dao.Execute(Owner, "wrong-account", tx => entered = true),
            e => e is InvalidOperationException, "authored-account-owner-fence");
        Require(!entered, "authored-foreign-account-entered-callback");
        ExpectUnchanged(connection, () => dao.Execute(Owner + 1, tx => entered = true),
            e => e is InvalidOperationException, "authored-missing-character-fence");
        Require(!entered, "authored-missing-character-entered-callback");
        ExpectUnchanged(connection, () => dao.Execute(Owner, Account,
                tx => tx.GetMission(new MissionKeyData(Owner + 1, quest))),
            e => e is InvalidOperationException, "authored-foreign-mission-fence");

        // Stale inventory hydration must roll back all earlier state/objective/account writes.
        var stale = Row(first + 1, 65, 43384); stale.StackCount = 2;
        ExpectUnchanged(connection, () => dao.Execute(Owner, Account,
                tx => Apply(tx, "disposable-authored-stale", stale, Row(first + 2, 66, 42423))),
            e => e is InvalidOperationException, "authored-stale-item-cas-rollback");
        FixtureSql.Execute(connection, $"INSERT INTO item_instances (InstanceId,ContainerType,ContainerInstance,ContainerPlacement,ItemType,LowId,HighId,Quality,StackCount,Source) VALUES ({first + 3},0xC749,{first + 1},0,0,42423,42423,1,1,1)");
        ExpectUnchanged(connection, () => dao.Execute(Owner, Account,
                tx => Apply(tx, "disposable-authored-child", grant, Row(first + 2, 66, 42423))),
            e => e is InvalidOperationException, "authored-child-row-retirement-rollback");
        Console.WriteLine("AUTHORED_MISSION_ATOMIC_ITEMS_STATE_STATS=PASS AUTHORED_MISSION_LATE_FAILURE_ROLLBACK=PASS AUTHORED_MISSION_REWARD_REPLAY=PASS AUTHORED_MISSION_OWNER_ACCOUNT_FENCES=PASS AUTHORED_MISSION_STALE_ITEM_ROLLBACK=PASS AUTHORED_MISSION_CHILD_ROW_ROLLBACK=PASS");
    }

    static MissionAtomicRewardStatus Apply(IMissionDaoTransaction tx, string quest,
        MissionItemInstanceData consumed, MissionItemInstanceData grant)
    {
        var key = new MissionKeyData(Owner, quest);
        tx.SaveMission(key, new MissionStateData { CharacterId = Owner, QuestId = quest,
            State = MissionLifecycleState.Completed, CurrentStepId = "completed", OfferedAtUtcTicks = Now,
            AcceptedAtUtcTicks = Now, CompletedAtUtcTicks = Now, CreatedAtUtcTicks = Now, UpdatedAtUtcTicks = Now });
        tx.SaveObjective(new MissionObjectiveKeyData(key, "open-package"), new MissionObjectiveProgressData {
            CharacterId = Owner, QuestId = quest, ObjectiveId = "open-package", Progress = 1, RequiredCount = 1,
            LastObservationKey = "disposable-open", CreatedAtUtcTicks = Now, UpdatedAtUtcTicks = Now });
        Require(tx.TryAddObservation(new MissionObjectiveObservationData { CharacterId = Owner, QuestId = quest,
            ObjectiveId = "open-package", ObservationKey = "disposable-open", EventType = "item-use",
            SourceIdentity = "50000:" + Owner, TargetIdentity = consumed.InstanceId.ToString(), ObservedAtUtcTicks = Now }),
            "authored-observation-write");
        tx.SaveFlag(key, new MissionFlagData { CharacterId = Owner, QuestId = quest, FlagKey = "complete",
            Value = "1", CreatedAtUtcTicks = Now, UpdatedAtUtcTicks = Now });
        tx.SaveAccountFlag(Account, new MissionAccountFlagData { AccountKey = Account, FlagKey = quest,
            Value = "1", SourceQuestId = quest, CreatedAtUtcTicks = Now, UpdatedAtUtcTicks = Now });
        ((IMissionInventoryMutationTransaction)tx).ApplyInventoryMutation([grant], [consumed]);
        return Reward(tx, quest);
    }

    static MissionAtomicRewardStatus Reward(IMissionDaoTransaction tx, string quest) =>
        tx.TryApplyCharacterStatReward(new MissionRewardKeyData(new MissionKeyData(Owner, quest), "package-reward"),
            "captured-package", [Stat(61, 201), Stat(52, 202)], "disposable-authored-proof", Now).Status;

    static MissionStatMutationData Stat(int stat, int value) => new() { StatIdentityType = CharacterType,
        StatId = stat, Kind = MissionStatMutationKind.Set, Value = value, MinimumValue = 0, MaximumValue = int.MaxValue };
    static MissionItemInstanceData Row(int id, int slot, int template) => new() { InstanceId = id,
        ContainerType = 104, ContainerInstance = Owner, ContainerPlacement = slot, ItemType = 0,
        LowId = template, HighId = template, Quality = 1, StackCount = 1, Source = 1 };
    static string Fingerprint(MySqlConnection connection) => FixtureSql.Fingerprint(connection, includeAutoIncrementCounters: false);
    static void ExpectUnchanged(MySqlConnection connection, Action action, Func<Exception, bool> expected, string failure)
    {
        string before = Fingerprint(connection);
        bool rejected = false;
        try { action(); }
        catch (Exception exception) when (expected(exception)) { rejected = true; }
        Require(rejected && before == Fingerprint(connection), failure);
    }
    static void Require(bool condition, string failure) { if (!condition) throw new FixtureFailure(failure); }
}
