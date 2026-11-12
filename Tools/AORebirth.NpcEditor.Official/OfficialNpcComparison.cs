namespace AORebirth.NpcEditor.Official;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using SmokeLounge.AOtomation.Messaging.GameData;

internal sealed record OfficialComparisonRow(string Property, string Observation, string ObservationSource, string Configured);

internal sealed record OfficialComparisonCandidate(string TemplateHash, string Name, int? Level, int? MonsterData,
    string Match, OfficialComparisonRow[] Rows);

internal sealed record OfficialComparisonResult(OfficialComparisonCandidate[] Candidates, int Total);

internal static class OfficialNpcComparison
{
    const long MaxCatalogBytes = 128 * 1024 * 1024;
    const int MaxCandidates = 120;

    internal static async Task<OfficialComparisonResult> ReadAsync(string file, OfficialSnapshot snapshot, string? templateHash,
        CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(file))
            throw new ArgumentException("Choose a local AORebirth NpcTemplates.json file.");
        string path = Path.GetFullPath(file);
        if (!Path.GetFileName(path).Equals("NpcTemplates.json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a local AORebirth NpcTemplates.json file.");
        OfficialLocalPaths.RequireLocalDirectory(Path.GetDirectoryName(path)!);
        OfficialLocalPaths.RejectLink(path);

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length is <= 0 or > MaxCatalogBytes)
            throw new InvalidDataException("The selected catalog must be between 1 byte and 128 MiB.");
        using JsonDocument catalog = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return Compare(catalog.RootElement, snapshot, templateHash, cancellationToken);
    }

    internal static OfficialComparisonResult Compare(JsonElement catalog, OfficialSnapshot snapshot, string? templateHash,
        CancellationToken cancellationToken)
    {
        if (catalog.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The NPC template catalog root must be an object keyed by template hash.");
        string? requestedHash = string.IsNullOrWhiteSpace(templateHash) ? null : templateHash.Trim();
        if (requestedHash?.Length > 64 || (requestedHash == null && string.IsNullOrWhiteSpace(snapshot.Name) && snapshot.MonsterData == null))
            throw new ArgumentException("Enter an exact AORebirth template hash when the observation lacks a usable name or MonsterData.");

        var candidates = new List<(int Rank, long LevelDifference, OfficialComparisonCandidate Candidate)>();
        int total = 0;
        foreach (JsonProperty definition in catalog.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (requestedHash != null && definition.Name != requestedHash)
                continue;
            if (Property(definition.Value, "Templates") is not { ValueKind: JsonValueKind.Array } bands)
                continue;

            foreach (JsonElement band in bands.EnumerateArray())
            {
                string? name = Text(Property(band, "Name"));
                int? level = Number(Property(band, "Level"));
                JsonElement stats = Property(band, "Stats");
                int? monsterData = Stat(stats, CharacterStat.MonsterData);
                bool nameMatches = !string.IsNullOrWhiteSpace(snapshot.Name)
                    && string.Equals(name, snapshot.Name, StringComparison.OrdinalIgnoreCase);
                bool bodyMatches = snapshot.MonsterData is > 0 && monsterData == snapshot.MonsterData;
                if (requestedHash == null && !nameMatches && !bodyMatches)
                    continue;

                total++;
                int rank = requestedHash != null || nameMatches && bodyMatches ? 0 : nameMatches ? 1 : 2;
                long distance = level.HasValue && snapshot.Level.HasValue ? Math.Abs((long)level.Value - snapshot.Level.Value) : long.MaxValue;
                if (candidates.Count == MaxCandidates && CompareRank(candidates[^1], rank, distance, definition.Name) <= 0)
                    continue;
                string match = requestedHash != null ? "Requested AORebirth hash"
                    : nameMatches && bodyMatches ? "Name and MonsterData candidate"
                    : nameMatches ? "Name candidate" : "MonsterData candidate";
                var candidate = new OfficialComparisonCandidate(definition.Name, name ?? "UNKNOWN", level, monsterData, match,
                    Rows(snapshot, band, stats, name, level, monsterData));
                int insert = candidates.FindIndex(existing => CompareRank(existing, rank, distance, definition.Name) > 0);
                candidates.Insert(insert < 0 ? candidates.Count : insert, (rank, distance, candidate));
                if (candidates.Count > MaxCandidates)
                    candidates.RemoveAt(MaxCandidates);
            }
        }
        return new(candidates.Select(entry => entry.Candidate).ToArray(), total);
    }

    static int CompareRank((int Rank, long LevelDifference, OfficialComparisonCandidate Candidate) existing,
        int rank, long distance, string hash)
    {
        int compare = existing.Rank.CompareTo(rank);
        if (compare != 0)
            return compare;
        compare = existing.LevelDifference.CompareTo(distance);
        return compare != 0 ? compare : string.CompareOrdinal(existing.Candidate.TemplateHash, hash);
    }

    static OfficialComparisonRow[] Rows(OfficialSnapshot snapshot, JsonElement band, JsonElement stats, string? name,
        int? level, int? monsterData)
    {
        var rows = new List<OfficialComparisonRow>
        {
            Core("Name", "identity.name", snapshot.Name, name),
            Core("Level", "identity.level", Format(snapshot.Level), Format(level)),
            Core("MonsterData", "identity.monster_data", Format(snapshot.MonsterData), Format(monsterData))
        };
        foreach ((string property, CharacterStat stat, string key) in StatRows)
        {
            OfficialField? field = snapshot.Fields.FirstOrDefault(field => field.Key == key);
            rows.Add(new(property, field?.Value ?? "NOT EXPOSED",
                field == null ? "NOT_AVAILABLE" : field.Classification + " / " + field.Source,
                Format(Stat(stats, stat)) ?? "UNCONFIGURED"));
        }
        JsonElement equipment = Property(band, "Equipment");
        rows.Add(new("Equipment template pairs", "NOT EXPOSED", "NOT_AVAILABLE",
            equipment.ValueKind == JsonValueKind.Array && equipment.GetArrayLength() > 0
                ? $"{equipment.GetArrayLength()} authored equipment entries" : "UNCONFIGURED"));
        return [.. rows];

        OfficialComparisonRow Core(string property, string key, string? observed, string? configured)
        {
            OfficialField? field = snapshot.Fields.FirstOrDefault(field => field.Key == key);
            return new(property, observed ?? "UNKNOWN", field != null ? field.Classification + " / " + field.Source
                : observed == null ? "NOT_AVAILABLE" : "CLIENT_EXPOSED / AOSharp target snapshot", configured ?? "UNCONFIGURED");
        }
    }

    static readonly (string Property, CharacterStat Stat, string Key)[] StatRows =
    [
        ("Current health", CharacterStat.Health, "health.current"),
        ("Maximum health", CharacterStat.MaxHealth, "health.maximum"),
        ("Health percentage", CharacterStat.PercentRemainingHealth, "health.percent"),
        ("Current nano", CharacterStat.CurrentNano, "nano.current"),
        ("Maximum nano", CharacterStat.MaxNanoEnergy, "nano.maximum"),
        ("Breed", CharacterStat.Breed, "identity.breed"),
        ("Profession", CharacterStat.Profession, "identity.profession"),
        ("NPC family stat", CharacterStat.NPCFamily, "identity.family"),
        ("Visual profession", CharacterStat.VisualProfession, "stats.visualprofession"),
        ("Armor class, melee", CharacterStat.MeleeAC, "stats.meleeac"),
        ("Evade close combat", CharacterStat.EvadeClsC, "stats.evadeclsc"),
        ("Add all offense", CharacterStat.AMSModifier, "stats.addalloff"),
        ("Add all defense", CharacterStat.DMSModifier, "stats.addalldef")
    ];

    static JsonElement Property(JsonElement record, string name)
    {
        if (record.ValueKind != JsonValueKind.Object)
            return default;
        if (record.TryGetProperty(name, out JsonElement value))
            return value;
        foreach (JsonProperty property in record.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }
        return default;
    }

    static int? Stat(JsonElement stats, CharacterStat stat)
        => Number(Property(stats, ((int)stat).ToString(CultureInfo.InvariantCulture))) ?? Number(Property(stats, stat.ToString()));

    static int? Number(JsonElement value)
        => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) ? number : null;

    static string? Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    static string? Format(int? value) => value?.ToString(CultureInfo.InvariantCulture);
}
