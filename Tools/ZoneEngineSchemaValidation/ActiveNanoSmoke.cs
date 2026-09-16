using MySqlConnector;
using ZoneEngine_New.Core.Data;
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
            ActiveNanoRecord legacy = new()
            {
                NanoId = 1001,
                Strain = 11,
                NanoInstance = 2001,
                DurationCentiseconds = 6000,
                ExpiresAtUtcTicks = 0
            };
            ActiveNanoRecord current = new()
            {
                NanoId = 1002,
                Strain = 12,
                NanoInstance = 2002,
                DurationCentiseconds = 6000,
                ExpiresAtUtcTicks = 639200000000000000
            };
            dao.WriteReplaceAll(9601, [legacy]);
            dao.WriteReplaceAll(9602, [current]);
            if (!Same(dao.GetForCharacter(9601), [legacy]) || !Same(dao.GetForCharacter(9602), [current]))
                throw new FixtureFailure("active-nano-existing-schema-roundtrip");
            dao.WriteReplaceAll(9601, [current]);
            dao.WriteReplaceAll(9602, []);
            if (!Same(dao.GetForCharacter(9601), [current]) || dao.GetForCharacter(9602).Count != 0)
                throw new FixtureFailure("active-nano-replacement-clear");
            Console.WriteLine("ACTIVE_NANO_LEGACY_AND_CURRENT_ROUNDTRIP=PASS ACTIVE_NANO_REPLACE_ALL=PASS");
        }
        finally { Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", previous); }
    }

    static bool Same(IReadOnlyList<ActiveNanoRecord> left, ActiveNanoRecord[] right)
        => left.Count == right.Length
            && left.Zip(right).All(pair => pair.First.NanoId == pair.Second.NanoId
                && pair.First.Strain == pair.Second.Strain
                && pair.First.NanoInstance == pair.Second.NanoInstance
                && pair.First.DurationCentiseconds == pair.Second.DurationCentiseconds
                && pair.First.ExpiresAtUtcTicks == pair.Second.ExpiresAtUtcTicks);
}
