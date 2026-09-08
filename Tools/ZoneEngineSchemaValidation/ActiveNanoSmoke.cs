using MySqlConnector;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Nanos;
using SmokeLounge.AOtomation.Messaging.GameData;

static class ActiveNanoSmoke
{
    public static void Validate(DisposableSchemaDatabase fixture, MySqlConnection connection)
    {
        string? previous = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", fixture.ConnectionString);
            FixtureSql.Execute(connection, "INSERT INTO characters (Id,Name,Online) VALUES (9601,'DisposableNanoA',0),(9602,'DisposableNanoB',0)");
            var dao = new MySqlActiveNanoRepository();
            ActiveNanoRecord legacy = new(1001, 11, 2001, 6000, 0); // Existing Legacy remaining-duration representation.
            ActiveNanoRecord current = new(1002, 12, 2002, 6000, 639200000000000000);
            int manaStat = (int)CharacterStat.CurrentNano;
            StatRecord Mana(int value) => new() { StatId = manaStat, StatValue = value };
            dao.Commit([new(9601, [legacy], [Mana(200)]), new(9602, [current], [Mana(100)])]);
            if (!dao.Load(9601).SequenceEqual([legacy]) || !dao.Load(9602).SequenceEqual([current]))
                throw new FixtureFailure("active-nano-existing-schema-roundtrip");
            FixtureSql.Execute(connection, $"ALTER TABLE stats ADD CONSTRAINT fixture_nano_late_failure CHECK (Instance <> 9602 OR StatId <> {manaStat} OR StatValue <= 100)");
            string before = FixtureSql.Fingerprint(connection, includeAutoIncrementCounters: false);
            bool failed = false;
            try { dao.Commit([new(9601, [current], [Mana(150)]), new(9602, [], [Mana(101)])]); }
            catch (MySqlException exception) when (exception.Number == 3819) { failed = true; }
            if (!failed || before != FixtureSql.Fingerprint(connection, includeAutoIncrementCounters: false))
                throw new FixtureFailure("active-nano-second-participant-failure-did-not-restore-all-nanos-and-stats");
            FixtureSql.Execute(connection, "ALTER TABLE stats DROP CHECK fixture_nano_late_failure");
            dao.Commit([new(9601, [current], [Mana(150)]), new(9602, [], [Mana(101)])]);
            if (!dao.Load(9601).SequenceEqual([current]) || dao.Load(9602).Count != 0
                || FixtureSql.Scalar(connection, $"SELECT StatValue FROM stats WHERE Instance=9601 AND StatId={manaStat}") != 150)
                throw new FixtureFailure("active-nano-replacement-and-mana-not-atomic");
            Console.WriteLine("ACTIVE_NANO_LEGACY_AND_CURRENT_ROUNDTRIP=PASS ACTIVE_NANO_MULTI_OWNER_ATOMIC_COMMIT=PASS ACTIVE_NANO_LATE_STAT_ROLLBACK=PASS");
        }
        finally { Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", previous); }
    }
}
