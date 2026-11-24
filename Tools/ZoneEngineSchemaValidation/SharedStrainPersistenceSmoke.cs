using AORebirth.Database.Domain.Characters;
using AORebirth.Interfaces.Persistence.Characters;
using MySqlConnector;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Data;

static class SharedStrainPersistenceSmoke
{
    const int Owner = 9631;
    const int OtherOwner = 9632;
    const int Ward = 117680;
    const int Shrug = 266820;
    const int AbsorbStrain = 219;
    const int UploadedNano = 37;
    static readonly long FutureDeadline = new DateTime(2040, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

    public static void Validate(DisposableSchemaDatabase fixture, MySqlConnection connection)
    {
        CreateSchema(connection);
        FixtureSql.Execute(connection, "INSERT INTO characters (Id,Username,Name,FirstName,LastName,Playfield,X,Y,Z,"
            + "HeadingX,HeadingY,HeadingZ,HeadingW) VALUES "
            + $"({Owner},'shared_nano_a','DisposableSharedA','','',4582,1,2,3,0,0,0,1),"
            + $"({OtherOwner},'shared_nano_b','DisposableSharedB','','',4582,1,2,3,0,0,0,1)");

        var dao = new MySqlCharacterPersistenceDao(() => fixture.Open());
        var nanos = new MySqlActiveNanoRepository(dao);
        var ward = Nano(Ward, AbsorbStrain, 1, ActiveNanoRecord.Paused(TimeSpan.FromMinutes(2)));
        var shrug = Nano(Shrug, AbsorbStrain, 2, FutureDeadline);
        var unrelated = Nano(1002, 12, 1, FutureDeadline);
        var treatment = new SkillLockRecord { StatId = (int)CharacterStat.Treatment, ExpiresAtUtcTicks = FutureDeadline };
        nanos.WriteReplaceAll(OtherOwner, [unrelated]);
        dao.SaveTrainedPerks(OtherOwner, [73]);

        // The standalone path accepts both base-strain-219 rows in the checked-in MySQL schema.
        nanos.WriteReplaceAll(Owner, [ward, shrug]);
        RequireNanos(nanos, Owner, [ward, shrug], "active-nano-standalone-shared-strain-roundtrip");

        // A subsequent coalesced flush replaces Ward while retaining Shrug and saving other dirty state.
        ward = Nano(Ward, AbsorbStrain, 3, FutureDeadline);
        Coalesced(dao).Persist([], [], Owner, [UploadedNano], [ward, shrug], [treatment]);
        dao.SaveTrainedPerks(Owner, [71, 72]);
        dao.SaveResearch(Owner, [new PersistedResearchData { ResearchId = 74, RemainingXp = 875 }]);
        RequireDurableState();

        // The decorator fails after MySQL runs every statement in the coalesced transaction,
        // including the skill-lock insert, and before COMMIT.
        var coalescedFault = new PersistenceFault { FailAfterWrite = 6 };
        var faultDao = new MySqlCharacterPersistenceDao(() => new FaultConnection(fixture.Open(), coalescedFault));
        var refreshedWard = Nano(Ward, AbsorbStrain, 4, FutureDeadline);
        var refreshedShrug = Nano(Shrug, AbsorbStrain, 5, FutureDeadline);
        var replacementLock = new SkillLockRecord { StatId = (int)CharacterStat.FirstAid, ExpiresAtUtcTicks = FutureDeadline };
        RequireRollback(() => Coalesced(faultDao).Persist([], [], Owner, [38], [refreshedWard, refreshedShrug], [replacementLock]),
            coalescedFault, "active-nano-coalesced-rollback");
        RequireDurableState();

        var standaloneFault = new PersistenceFault { FailAfterWrite = 2 };
        faultDao = new MySqlCharacterPersistenceDao(() => new FaultConnection(fixture.Open(), standaloneFault));
        RequireRollback(() => new MySqlActiveNanoRepository(faultDao).WriteReplaceAll(Owner, [shrug]), standaloneFault,
            "active-nano-standalone-rollback");
        RequireDurableState();

        var duplicateSpell = Nano(Ward, AbsorbStrain, 6, FutureDeadline);
        var duplicateInstance = Nano(Shrug, AbsorbStrain, ward.NanoInstance, FutureDeadline);
        RequireIdentityRejected(() => nanos.WriteReplaceAll(Owner, [ward, duplicateSpell]), "active-nano-duplicate-spell");
        RequireIdentityRejected(() => Coalesced(dao).Persist([], [], Owner, [], [ward, duplicateInstance], null),
            "active-nano-duplicate-instance");
        RequireDurableState();

        // Removal replaces only this owner's snapshot and leaves the other owner's same instance number intact.
        Coalesced(dao).Persist([], [], Owner, [], [ward], null);
        RequireNanos(nanos, Owner, [ward], "active-nano-remove-only-selected-buff");
        RequireNanos(nanos, OtherOwner, [unrelated], "active-nano-owner-isolation");
        Console.WriteLine("ACTIVE_NANO_SHARED_STRAIN_MYSQL=PASS COALESCED_ROUNDTRIP=PASS STANDALONE_ROUNDTRIP=PASS "
            + "ROLLBACK_CASES=2 NANO_IDENTITY_VALIDATION=PASS OWNER_ISOLATION=PASS PRODUCTION_CONTACT=NO ENGINES_STARTED=NO");

        void RequireDurableState()
        {
            RequireNanos(nanos, Owner, [ward, shrug], "active-nano-coalesced-shared-strain-roundtrip");
            RequireNanos(nanos, OtherOwner, [unrelated], "active-nano-owner-isolation");
            var locks = dao.LoadSkillLocks(Owner);
            if (!dao.LoadUploadedNanos(Owner).SequenceEqual(new[] { UploadedNano })
                || locks.Count != 1 || locks[0].StatId != treatment.StatId || locks[0].ExpiresAtUtcTicks != treatment.ExpiresAtUtcTicks)
                throw new FixtureFailure("active-nano-coalesced-upload-and-lock-state");

            var progress = dao.LoadResearch(Owner);
            if (!dao.LoadTrainedPerks(Owner).SequenceEqual(new[] { 71, 72 }) || !dao.LoadTrainedPerks(OtherOwner).SequenceEqual(new[] { 73 })
                || progress.Count != 1 || progress[0].ResearchId != 74 || progress[0].RemainingXp != 875
                || dao.LoadResearch(OtherOwner).Count != 0)
                throw new FixtureFailure("active-nano-progression-owner-isolation");
        }
    }

    static void CreateSchema(MySqlConnection connection)
    {
        string folder = Path.Combine(ConnectedAcceptanceSmoke.RepositoryRoot(), "AORebirth", "Libraries", "Source",
            "AORebirth.Database", "SqlTables");
        foreach (string file in new[] { "characters.sql", "charactersactivenanos.sql", "charactersactivenanos_alter.sql",
            "charactersuploadednanos.sql", "characterskilllocks.sql", "charactersperks.sql", "characterresearch.sql" })
            FixtureSql.Execute(connection, File.ReadAllText(Path.Combine(folder, file)));
    }

    static MySqlCharacterCoalesceCommit Coalesced(MySqlCharacterPersistenceDao dao)
    {
        var logger = new SilentLogger();
        return new MySqlCharacterCoalesceCommit(new MySqlInventoryRepository(logger, dao),
            new MySqlUploadedNanoRepository(logger, dao), new MySqlActiveNanoRepository(dao), logger);
    }

    static ActiveNanoRecord Nano(int id, int strain, int instance, long expiresAtUtcTicks) => new()
    {
        NanoId = id,
        Strain = strain,
        NanoInstance = instance,
        DurationCentiseconds = 12000,
        ExpiresAtUtcTicks = expiresAtUtcTicks
    };

    static void RequireNanos(MySqlActiveNanoRepository repository, int owner, ActiveNanoRecord[] expected, string failure)
    {
        var actual = repository.GetForCharacter(owner);
        if (actual.Count != expected.Length || !actual.OrderBy(row => row.NanoId).Zip(expected.OrderBy(row => row.NanoId)).All(pair =>
            pair.First.NanoId == pair.Second.NanoId && pair.First.Strain == pair.Second.Strain
            && pair.First.NanoInstance == pair.Second.NanoInstance && pair.First.DurationCentiseconds == pair.Second.DurationCentiseconds
            && pair.First.ExpiresAtUtcTicks == pair.Second.ExpiresAtUtcTicks))
            throw new FixtureFailure(failure);
    }

    static void RequireRollback(Action action, PersistenceFault fault, string failure)
    {
        try
        {
            action();
        }
        catch (IOException) when (fault.Writes == fault.FailAfterWrite && fault.CommitCalls == 0 && fault.RollbackCalls == 1)
        {
            return;
        }

        throw new FixtureFailure(failure);
    }

    static void RequireIdentityRejected(Action action, string failure)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new FixtureFailure(failure);
    }
}
