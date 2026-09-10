using System.Security.Cryptography;
using System.Text;
using AORebirth.Core.Encryption;
using AORebirth.Database.Dao;
using MySqlConnector;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;

static partial class ConnectedAcceptanceSmoke
{
    static ZoneLoginMessage expiredHandoff = null!;
    static ZoneLoginMessage consumedHandoff = null!;

    // Only administrative setup, before either engine is started. No running-state edits.
    static void SeedHandoffCases(MySqlConnection connection, DisposableSchemaDatabase fixture, string password)
    {
        foreach (var account in new[] { (9903, "cutoverother"), (9904, "cutoverexpired") })
        {
            using var command = new MySqlCommand("INSERT INTO login (Id,CreationDate,Email,FirstName,LastName,Username,Password,AllowedCharacters,Flags,AccountFlags,Expansions,GM) VALUES (@id,'2026-09-09','fixture@invalid','','',@account,@hash,6,0,0,2047,0)", connection);
            command.Parameters.AddWithValue("@id", account.Item1);
            command.Parameters.AddWithValue("@account", account.Item2);
            command.Parameters.AddWithValue("@hash", PasswordHash.CreateHash(password));
            command.ExecuteNonQuery();
        }
        foreach (var character in new[] { (9902, Account), (9903, "cutoverother"), (9904, "cutoverexpired") })
        {
            FixtureSql.Execute(connection, $"INSERT INTO characters (Id,Username,Name,FirstName,LastName,Playfield,X,Y,Z,HeadingW,HeadingX,HeadingY,HeadingZ,Online) VALUES ({character.Item1},'{character.Item2}','HandoffFixture{character.Item1}','','',4582,100,0,100,1,0,0,0,0)");
            FixtureSql.Execute(connection, $"INSERT INTO stats (Type,Instance,StatId,StatValue) SELECT Type,{character.Item1},StatId,StatValue FROM stats WHERE Instance={Owner} AND Type=50000");
        }
        var store = new ZoneHandoffStore(Path.Combine(fixture.DirectoryPath, "sessions", "zone-handoffs-v1"), () => DateTime.UtcNow.AddMinutes(-5));
        var ticket = store.Issue("cutoverexpired", store.BeginLogin("cutoverexpired"), 9904);
        expiredHandoff = new ZoneLoginMessage { CharacterId = 9904, Cookie1 = ticket.Cookie1, Cookie2 = ticket.Cookie2 };
    }

    static void ValidateHandoffCases(DisposableSchemaDatabase fixture, string password, MySqlConnection connection, int identity)
    {
        RejectUnchanged(fixture, connection, new ZoneLoginMessage { CharacterId = Owner }, "no-handoff");
        RejectUnchanged(fixture, connection, new ZoneLoginMessage { CharacterId = Owner, Cookie1 = 123, Cookie2 = 456 }, "random-handoff");
        RejectUnchanged(fixture, connection, new ZoneLoginMessage { CharacterId = 999999, Cookie1 = 123, Cookie2 = 456 }, "unknown-character-handoff");
        RejectUnchanged(fixture, connection, expiredHandoff, "expired-handoff");

        var selected = Authorize(fixture, password);
        RejectUnchanged(fixture, connection, selected, "mismatched-header-character", headerSender: 9902);
        RejectUnchanged(fixture, connection, selected, "wrong-header-destination", headerReceiver: 0);
        RejectUnchanged(fixture, connection, Copy(selected, 9902), "account-A1-ticket-used-for-A2");
        RejectUnchanged(fixture, connection, Copy(selected, 9903), "account-A-ticket-used-for-B");
        RejectUnchanged(fixture, connection, new ZoneLoginMessage { CharacterId = Owner, Cookie1 = selected.Cookie1 ^ 1, Cookie2 = selected.Cookie2 }, "altered-handoff");
        var other = Authorize(fixture, password, "cutoverother", 9903);
        RejectUnchanged(fixture, connection, Copy(other, Owner), "account-B-ticket-used-for-A");

        var fresh = Authorize(fixture, password);
        RejectUnchanged(fixture, connection, selected, "stale-login-generation");
        const int attempts = 8;
        var clients = Enumerable.Range(0, attempts).Select(_ => new ConnectedWireClient(fixture.ZonePort)).ToArray();
        try
        {
            using var start = new ManualResetEventSlim();
            var tasks = clients.Select(client => Task.Run(() => { start.Wait(); client.Send(fresh); return client.AdmissionRejected(Owner); })).ToArray();
            start.Set(); Task.WaitAll(tasks);
            Require(tasks.Count(t => !t.Result) == 1, "concurrent-handoff-exactly-one-admission");
            var winner = clients[Array.FindIndex(tasks, t => !t.Result)];
            EnterWorld(winner);
            VerifyConnected(winner, identity, 66, "CONCURRENT_WINNER");
            VerifyDatabase(connection, identity, 66);
            RejectUnchanged(fixture, connection, fresh, "replay-while-owner-in-play");
            Logout(winner, connection);
            consumedHandoff = fresh;
            RejectUnchanged(fixture, connection, fresh, "replay-after-disconnect");
            Console.WriteLine($"ZONE_HANDOFF_CONCURRENCY=PASS ATTEMPTS={attempts} ADMITTED=1 REJECTED={attempts - 1}");
        }
        finally { foreach (var client in clients) client.Dispose(); }
        HandoffRejected = true;
        Console.WriteLine("ZONE_HANDOFF_FAIL_CLOSED=PASS NEGATIVE_DATABASE_MUTATIONS=NONE");
    }
    static ZoneLoginMessage Copy(ZoneLoginMessage message, int character) => new() { CharacterId = character, Cookie1 = message.Cookie1, Cookie2 = message.Cookie2 };

    static void RejectUnchanged(DisposableSchemaDatabase fixture, MySqlConnection connection, ZoneLoginMessage message, string scenario, int? headerSender = null, int? headerReceiver = null)
    {
        string before = DatabaseFingerprint(connection);
        using var client = new ConnectedWireClient(fixture.ZonePort);
        client.Send(message, headerSender: headerSender, headerReceiver: headerReceiver);
        Require(client.AdmissionRejected(message.CharacterId), "handoff-accepted-" + scenario);
        Require(DatabaseFingerprint(connection) == before, "rejected-handoff-mutated-database-" + scenario);
        Console.WriteLine("ZONE_HANDOFF_NEGATIVE=PASS CASE=" + scenario + " DATABASE_UNCHANGED=YES");
    }

    // Complete disposable schema contents, sorted independently of physical row order.
    // Hashes and values (including credentials) are never printed.
    static string DatabaseFingerprint(MySqlConnection connection)
    {
        var tables = new List<string>();
        using (var command = new MySqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME", connection))
        using (var reader = command.ExecuteReader()) while (reader.Read()) tables.Add(reader.GetString(0));
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string table in tables)
        {
            var rows = new List<string>();
            using var command = new MySqlCommand("SELECT * FROM `" + table.Replace("`", "``") + "`", connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                object[] values = new object[reader.FieldCount]; reader.GetValues(values);
                rows.Add(System.Text.Json.JsonSerializer.Serialize(values));
            }
            rows.Sort(StringComparer.Ordinal);
            digest.AppendData(Encoding.UTF8.GetBytes(table + "\n" + string.Join("\n", rows) + "\n"));
        }
        return Convert.ToHexString(digest.GetHashAndReset());
    }
}
