using System.Globalization;
using System.Text.Json;
using AORebirth.Database.Domain.Characters;
using MySqlConnector;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

// Positions come from the engine's existing snapshot log, independently of the database.
// Prepared reads expose stored FLOAT bits; ordinary reads expose the DAO's text protocol.
readonly record struct FixturePosition(float X, float Y, float Z)
{
    public void RequireEqual(FixturePosition actual, string stage)
    {
        if (Bits(X) != Bits(actual.X) || Bits(Y) != Bits(actual.Y) || Bits(Z) != Bits(actual.Z))
            throw new FixtureFailure($"position-{stage}-expected-{this}-actual-{actual}");
    }
    public void Trace(string stage) => Console.WriteLine("CONNECTED_POSITION=" + JsonSerializer.Serialize(new
    {
        Stage = stage, X = (double)X, Y = (double)Y, Z = (double)Z,
        XBits = Bits(X), YBits = Bits(Y), ZBits = Bits(Z)
    }));
    public static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
}

static class ConnectedPositionEvidence
{
    public static FixturePosition Expected = new(100, 0, 100);

    public static void ValidateContract()
    {
        var authoritative = new FixturePosition(100, -0.0625f, 100);
        authoritative.RequireEqual(authoritative, "unchanged-control");
        foreach (var damaged in new[] {
            authoritative with { X = MathF.BitIncrement(authoritative.X) },
            authoritative with { Y = MathF.BitIncrement(authoritative.Y) },
            authoritative with { Z = MathF.BitIncrement(authoritative.Z) },
            authoritative with { Y = 0 } })
        {
            bool rejected = false;
            try { authoritative.RequireEqual(damaged, "injected-position-loss"); }
            catch (FixtureFailure) { rejected = true; }
            if (!rejected) throw new FixtureFailure("position-regression-failed-to-detect-loss");
        }
        Console.WriteLine("CONNECTED_POSITION_NEGATIVE_FIXTURES=PASS CASES=4 TOLERANCE=NONE");
    }

    public static void VerifySaved(DisposableSchemaDatabase fixture, MySqlConnection connection,
        FixturePosition snapshot, LifecyclePersistenceEvidence evidence, string stage)
    {
        snapshot.Trace(stage + "_PRE_SNAPSHOT_RUNTIME");
        // The route intentionally changes height through simulation, not X/Z or destination ownership.
        if (snapshot.X != 100 || snapshot.Z != 100)
            throw new FixtureFailure("position-unrequested-horizontal-change");
        var stored = Read(connection, prepared: true);
        snapshot.RequireEqual(stored, stage + "-snapshot-to-stored-float");
        stored.Trace(stage + "_STORED_BINARY_FLOAT");
        var text = Read(connection, prepared: false);
        var row = new MySqlCharacterPersistenceDao(() => fixture.Open()).LoadCharacter(9901)
            ?? throw new FixtureFailure("position-dao-reload-missing");
        var loaded = new FixturePosition(row.X, row.Y, row.Z);
        text.RequireEqual(loaded, stage + "-text-protocol-to-dao");
        loaded.Trace(stage + "_DAO_RELOADED");
        Console.WriteLine("CONNECTED_POSITION_READ_CONVERSION=" + JsonSerializer.Serialize(new
        {
            Stage = stage, StoredY = (double)stored.Y, TextReadY = (double)text.Y,
            Error = (double)text.Y - stored.Y, StoredBits = FixturePosition.Bits(stored.Y),
            TextBits = FixturePosition.Bits(text.Y)
        }));
        Expected = loaded;
        evidence.ExpectPosition(text.X, text.Y, text.Z);
    }

    public static void VerifySpawn(SimpleCharFullUpdateMessage spawn, string stage)
    {
        var actual = new FixturePosition(spawn.Coordinates.X, spawn.Coordinates.Y, spawn.Coordinates.Z);
        actual.Trace(stage + "_POST_HYDRATION_WIRE");
        Expected.RequireEqual(actual, stage + "-dao-to-runtime-wire");
    }

    public static void TraceSpawn(ConnectedWireClient client, string stage)
    {
        var spawn = client.Received.OfType<SimpleCharFullUpdateMessage>().First(m => m.Identity.Instance == 9901);
        new FixturePosition(spawn.Coordinates.X, spawn.Coordinates.Y, spawn.Coordinates.Z).Trace(stage);
    }

    public static FixturePosition Read(MySqlConnection connection, bool prepared)
    {
        // Distinct SQL prevents MySqlConnector's connection-level prepared-statement cache
        // from silently turning the ordinary text read into another binary read.
        using var command = new MySqlCommand("SELECT Playfield,X,Y,Z FROM characters WHERE Id=@id"
            + (prepared ? " /* stored-binary */" : " /* ordinary-text */"), connection);
        command.Parameters.AddWithValue("@id", 9901);
        if (prepared) command.Prepare();
        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.GetInt32(0) != 4582) throw new FixtureFailure("position-row-or-playfield");
        return new(reader.GetFloat(1), reader.GetFloat(2), reader.GetFloat(3));
    }
}
