namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.NpcEditor;

internal sealed record NpcEditorStatsRevision(
    int Revision, DateTimeOffset AtUtc, string Actor, string Action, Dictionary<int, int> Values);

internal sealed record NpcEditorStatsHistory(
    string TemplateHash, int BandIndex, string Signature, NpcEditorStatsRevision[] Revisions);

internal sealed record NpcEditorScopedStatsHistory(
    string Scope, string? FamilyHash, int? ResourcePlayfieldId, string? TemplateHash, int? BandIndex,
    string Signature, NpcEditorStatsRevision[] Revisions);

internal sealed record NpcEditorStatsJournal(
    int Version, Dictionary<string, NpcEditorStatsHistory> Bands,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Dictionary<string, NpcEditorScopedStatsHistory>? Scopes = null)
{
    internal static readonly NpcEditorStatsJournal Empty = new(1, new(StringComparer.Ordinal));
    [JsonIgnore]
    internal string? FileFingerprint { get; init; }
    [JsonIgnore]
    internal string? LoadError { get; init; }

    internal NpcEditorStatsHistory? Find(string hash, int bandIndex) => Bands.GetValueOrDefault(Key(hash, bandIndex));
    internal static string Key(string hash, int bandIndex) => hash + ":" + bandIndex.ToString(CultureInfo.InvariantCulture);
    internal static string FamilyKey(string hash) => "family:" + hash;
    internal static string PlayfieldKey(int resourceId, string hash, int bandIndex)
        => "playfield:" + resourceId.ToString(CultureInfo.InvariantCulture) + ":" + Key(hash, bandIndex);
}

internal sealed class NpcEditorStatsJournalFile(string dataRoot, string? overlayRoot)
{
    internal const string FileName = "NpcEditorStatOverrides.json";
    internal const int MaxRevisionsPerBand = 256;
    const int MaxVitalValidationLevels = 1024;

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    readonly NpcEditorOverrideFile _file = new(dataRoot, overlayRoot, FileName, "NPC stat");
    internal string? ConfigurationProblem => _file.ConfigurationProblem;

    internal NpcEditorStatsJournal Read(NpcTemplateCatalog catalog)
    {
        byte[]? bytes = _file.Read();
        if (bytes == null)
            return NpcEditorStatsJournal.Empty;
        var journal = JsonSerializer.Deserialize<NpcEditorStatsJournal>(bytes, JsonOptions)
            ?? throw new InvalidDataException("The NPC stat override document is empty.");
        if (journal.Version is not (1 or 2) || journal.Bands == null || (journal.Version == 1 && journal.Scopes != null))
            throw new InvalidDataException("Unsupported NPC stat override document.");

        foreach ((string key, NpcEditorStatsHistory history) in journal.Bands)
        {
            if (history == null || string.IsNullOrWhiteSpace(history.TemplateHash)
                || key != NpcEditorStatsJournal.Key(history.TemplateHash, history.BandIndex)
                || !catalog.TryGetStatsBand(history.TemplateHash, history.BandIndex, out NpcLeaf leaf)
                || history.Signature != leaf.EditorLootTemplateSignature
                || history.Revisions == null || history.Revisions.Length is < 1 or > MaxRevisionsPerBand)
                throw new InvalidDataException("The NPC stat override no longer matches its authored template band. " + key);

            for (int i = 0; i < history.Revisions.Length; i++)
            {
                if (!ValidRevision(history.Revisions[i], i))
                    throw new InvalidDataException("Invalid NPC stat override revision. " + key);
            }
            if (!ValidateBand(leaf, history.BandIndex, history.Revisions[^1].Values, out string? error))
                throw new InvalidDataException(error);
        }

        if (journal.Scopes != null)
        {
            var familyAssignments = new HashSet<(string Leaf, int Stat)>();
            foreach ((string key, NpcEditorScopedStatsHistory history) in journal.Scopes)
            {
                if (history == null || history.Revisions == null
                    || history.Revisions.Length is < 1 or > MaxRevisionsPerBand
                    || history.Revisions.Where((revision, index) =>
                        !ValidRevision(revision, index) || !ValidScopedValues(revision.Values)).Any())
                    throw new InvalidDataException("Invalid NPC scoped stat override revision. " + key);

                if (history is { Scope: "family", FamilyHash: { Length: > 0 } family, ResourcePlayfieldId: null, TemplateHash: null, BandIndex: null }
                    && key == NpcEditorStatsJournal.FamilyKey(family) && catalog.EditorStatsFamily(family) is { } binding
                    && history.Signature == binding.Signature)
                {
                    foreach (string leaf in binding.Leaves)
                    foreach (int stat in history.Revisions[^1].Values.Keys)
                    {
                        if (!familyAssignments.Add((leaf, stat)))
                            throw new InvalidDataException("Overlapping NPC families assign the same stat. " + family);
                    }
                    continue;
                }
                if (history is { Scope: "playfield", FamilyHash: null, ResourcePlayfieldId: int resource,
                        TemplateHash: { Length: > 0 } hash, BandIndex: int band }
                    && resource > 0 && key == NpcEditorStatsJournal.PlayfieldKey(resource, hash, band)
                    && catalog.TryGetStatsBand(hash, band, out NpcLeaf selected)
                    && history.Signature == selected.EditorLootTemplateSignature)
                    continue;

                throw new InvalidDataException("The NPC stat override no longer matches its authored scope. " + key);
            }
        }

        return journal with { FileFingerprint = NpcEditorOverrideFile.Fingerprint(bytes) };
    }

    internal NpcEditorStatsJournal Write(NpcEditorStatsJournal current, NpcEditorStatsJournal updated)
        => updated with { FileFingerprint = _file.Write(current.FileFingerprint, updated, JsonOptions).Fingerprint };

    static bool ValidValues(IReadOnlyDictionary<int, int>? values)
        => values != null && values.Count <= NpcEditorStatPolicy.EditableStats.Length
            && values.All(entry => NpcEditorStatPolicy.IsAllowedValue((CharacterStat)entry.Key, entry.Value));

    static bool ValidRevision(NpcEditorStatsRevision? revision, int index)
        => revision != null && revision.Revision == index + 1 && revision.AtUtc != default
            && !string.IsNullOrWhiteSpace(revision.Actor) && revision.Actor.Length <= 128
            && revision.Action is "save" or "restore" && ValidValues(revision.Values);

    internal static bool ValidScopedValues(IReadOnlyDictionary<int, int> values)
        => ValidValues(values) && !values.Keys.Any(id => NpcEditorStatPolicy.IsVital((CharacterStat)id));

    internal static bool ValidateBand(NpcLeaf leaf, int bandIndex, IReadOnlyDictionary<int, int> values, out string? error)
    {
        error = null;
        if (!ValidValues(values))
        {
            error = "A stat is outside the supported NPC edit policy or range.";
            return false;
        }
        if (!values.Keys.Any(statId => NpcEditorStatPolicy.IsVital((CharacterStat)statId)))
            return true;

        // This scope includes only levels whose nearest authored band is the selected band.
        int first = leaf.Bands[Math.Max(0, bandIndex - 1)].Level;
        int last = leaf.Bands[Math.Min(leaf.Bands.Length - 1, bandIndex + 1)].Level;
        if (first <= 0 || (long)last - first + 1 > MaxVitalValidationLevels)
        {
            error = "The authored level span is unavailable for bounded vital validation.";
            return false;
        }
        for (long level = first; level <= last; level++)
        {
            Dictionary<int, int>? authored = NpcTemplateCatalog.AuthoredStatsForBand(leaf, bandIndex, (int)level);
            if (authored == null || NpcEditorStatPolicy.TryApplyConfigured(authored, values, null, out error))
                continue;
            error = $"Level {level}. {error}";
            return false;
        }
        return true;
    }
}
