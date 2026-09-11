using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MySqlConnector;
using SmokeLounge.AOtomation.Messaging.GameData;

static class LifecycleSpawnFixture
{
    // Complete synthetic spawn state shared by connected and repository reload fixtures.
    // Full vitals keep idle regeneration from becoming an unrequested lifecycle mutation.
    public static Dictionary<CharacterStat, int> Stats() => new()
    {
        [CharacterStat.Flags] = 0x00081241,
        [CharacterStat.Level] = 1, [CharacterStat.TitleLevel] = 1,
        [CharacterStat.Breed] = 1, [CharacterStat.Race] = 1, [CharacterStat.Sex] = 2,
        [CharacterStat.Profession] = 1, [CharacterStat.Fatness] = 0,
        [CharacterStat.HeadMesh] = 40683, [CharacterStat.VisualFlags] = 31, [CharacterStat.Scale] = 100,
        [CharacterStat.Side] = 0, [CharacterStat.Expansion] = 171,
        [CharacterStat.Strength] = 6, [CharacterStat.Agility] = 6, [CharacterStat.Stamina] = 6,
        [CharacterStat.Intelligence] = 6, [CharacterStat.Sense] = 6, [CharacterStat.Psychic] = 6,
        [CharacterStat.BodyDevelopment] = 5, [CharacterStat.NanoPool] = 5,
        [CharacterStat.Cash] = 1234,
        [CharacterStat.Health] = 31, [CharacterStat.MaxHealth] = 31,
        [CharacterStat.CurrentNano] = 29, [CharacterStat.MaxNanoEnergy] = 29,
        [CharacterStat.MonsterData] = 0, [CharacterStat.CATMesh] = 111, [CharacterStat.DisplayCATMesh] = 222,
        [(CharacterStat)12] = 5907,
        [CharacterStat.MaxNCU] = 100, [CharacterStat.RunSpeed] = 100,
        // Existing 827c7fb5 first-save derived rows, confirmed with the same fixture.
        // Seed them explicitly so lifecycle equality has no permitted stat additions.
        [CharacterStat.CurrentNCU] = 0, [CharacterStat.TeamSide] = 0,
        [CharacterStat.NextXP] = 1450, [CharacterStat.LastXP] = 0,
        [CharacterStat.SocialStatus] = 4, [CharacterStat.MapsC] = 0,
        [CharacterStat.NumberOfTeamMembers] = 0, [CharacterStat.Team] = 0
    };
}

sealed class LifecyclePersistenceEvidence
{
    readonly int owner;
    readonly SortedDictionary<string, string?> character;
    readonly SortedDictionary<string, string?> stats;
    readonly SortedDictionary<string, string?> items;

    LifecyclePersistenceEvidence(int owner, SortedDictionary<string, string?> character,
        SortedDictionary<string, string?> stats, SortedDictionary<string, string?> items)
    {
        this.owner = owner; this.character = character; this.stats = stats; this.items = items;
    }

    public static LifecyclePersistenceEvidence Capture(MySqlConnection connection, int owner)
    {
        var character = Read(connection, "SELECT * FROM characters WHERE Id=@owner", owner, "Id");
        var stats = Read(connection, "SELECT Type,Instance,StatId,StatValue FROM stats WHERE Instance=@owner ORDER BY Type,StatId", owner, "Type", "StatId");
        var items = Read(connection, "SELECT * FROM item_instances WHERE ContainerInstance=@owner ORDER BY InstanceId", owner, "InstanceId");
        return new LifecyclePersistenceEvidence(owner, character, stats, items);
    }

    public void ExpectInventoryMove(int instance, int source, int destination)
    {
        string key = instance + "/ContainerPlacement";
        if (!items.TryGetValue(key, out string? actual) || actual != source.ToString(CultureInfo.InvariantCulture))
            throw new FixtureFailure("lifecycle-intentional-move-source-mismatch");
        items[key] = destination.ToString(CultureInfo.InvariantCulture);
    }

    public void Verify(MySqlConnection connection, string phase, int expectedOnline)
    {
        var actual = Capture(connection, owner);
        var expectedCharacter = new SortedDictionary<string, string?>(character, StringComparer.Ordinal)
        {
            [owner + "/Online"] = expectedOnline.ToString(CultureInfo.InvariantCulture)
        };
        Equal(expectedCharacter, actual.character, phase, "character");
        Equal(stats, actual.stats, phase, "stats");
        Equal(items, actual.items, phase, "items");
        string serialized = JsonSerializer.Serialize(new { Character = actual.character, Stats = actual.stats, Items = actual.items });
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized))).ToLowerInvariant();
        Console.WriteLine($"CONNECTED_FULL_PERSISTENCE_{phase}=PASS SHA256={hash} STAT_ROWS={actual.stats.Count / 4} ITEM_ROWS={actual.items.Keys.Count(key => key.EndsWith("/InstanceId", StringComparison.Ordinal))} ONLINE={expectedOnline} EQUIPMENT=SEEDED_STORAGE_PRESERVED");
    }

    static void Equal(SortedDictionary<string, string?> expected, SortedDictionary<string, string?> actual, string phase, string kind)
    {
        var differences = expected.Keys.Union(actual.Keys).Order(StringComparer.Ordinal).Where(key =>
            !expected.TryGetValue(key, out var before) || !actual.TryGetValue(key, out var after) || before != after)
            .Select(key => new { Key = key, Before = expected.GetValueOrDefault(key), After = actual.GetValueOrDefault(key) }).ToArray();
        if (differences.Length != 0)
        {
            Console.WriteLine($"CONNECTED_PERSISTENCE_DIFFERENCE_{phase}_{kind}=" + JsonSerializer.Serialize(differences));
            throw new FixtureFailure($"connected-full-persistence-{phase}-{kind}");
        }
    }

    static SortedDictionary<string, string?> Read(MySqlConnection connection, string sql, int owner, params string[] keys)
    {
        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@owner", owner);
        using var reader = command.ExecuteReader();
        var result = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        while (reader.Read())
        {
            string identity = string.Join("/", keys.Select(key => Convert.ToString(reader[key], CultureInfo.InvariantCulture)));
            for (int index = 0; index < reader.FieldCount; index++)
                result.Add(identity + "/" + reader.GetName(index), reader.IsDBNull(index) ? null : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture));
        }
        return result;
    }
}
