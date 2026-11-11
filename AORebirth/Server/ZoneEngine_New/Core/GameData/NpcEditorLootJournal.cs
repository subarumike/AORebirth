namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.NpcEditor;

internal sealed record NpcEditorLootHistory(string TemplateSignature, NpcEditorLootRevision[] Revisions);

internal sealed record NpcEditorLootJournal(
    int Version,
    Dictionary<string, NpcEditorLootHistory> Templates)
{
    internal static readonly NpcEditorLootJournal Empty = new(1, new(StringComparer.Ordinal));

    [JsonIgnore]
    internal string? FileFingerprint { get; init; }

    [JsonIgnore]
    internal string? LoadError { get; init; }

    internal NpcEditorLootRevision[] HistoryFor(string hash)
        => Templates.TryGetValue(hash, out NpcEditorLootHistory? history) ? history.Revisions : [];

    internal static NpcEditorLootBandRow[] BandRows(NpcLevelBand band, int bandIndex, NpcEditorLootRevision? revision)
    {
        NpcEditorLootBand? saved = revision?.Bands?.FirstOrDefault(candidate => candidate.BandIndex == bandIndex);
        if (saved != null)
            return saved.Rows;

        List<MobItemTableEntry> authored = band.LootTable ?? [];
        return [.. Enumerable.Range(0, authored.Count).Where(index => authored[index] != null)
            .Select(index => new NpcEditorLootBandRow(index, null)),
            .. (revision?.Entries ?? []).Select(entry => new NpcEditorLootBandRow(null, entry))];
    }

    internal static List<MobItemTableEntry> MaterializeBand(NpcLevelBand band, int bandIndex, NpcEditorLootRevision? revision)
        => BandRows(band, bandIndex, revision).Select(row => row.Materialize(band)).ToList();
}

/// <summary>Writes the editor's own versioned document to a separately configured GameData overlay.</summary>
internal sealed class NpcEditorLootJournalFile(string dataRoot, string? overlayRoot)
{
    internal const string FileName = "NpcEditorLootOverrides.json";
    internal const int MaxRowsPerTemplate = 64;
    internal const int MaxRevisionsPerTemplate = 256;
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    readonly NpcEditorOverrideFile _file = new(dataRoot, overlayRoot, FileName, "loot");

    internal string? ConfigurationProblem => _file.ConfigurationProblem;

    internal NpcEditorLootJournal Read(NpcTemplateCatalog catalog, Func<string, bool> itemExists)
    {
        byte[]? bytes = _file.Read();
        if (bytes == null)
            return NpcEditorLootJournal.Empty;

        NpcEditorLootJournal journal = JsonSerializer.Deserialize<NpcEditorLootJournal>(bytes, JsonOptions)
            ?? throw new InvalidDataException("The NPC loot override document is empty.");
        if (journal.Version is not (1 or 2) || journal.Templates == null)
            throw new InvalidDataException("Unsupported NPC loot override document.");

        foreach ((string hash, NpcEditorLootHistory history) in journal.Templates)
        {
            if (!catalog.TryGetEditableLootLeaf(hash, out NpcLeaf leaf)
                || history == null || history.TemplateSignature != leaf.EditorLootTemplateSignature
                || history.Revisions == null || history.Revisions.Length is < 1 or > MaxRevisionsPerTemplate)
            {
                throw new InvalidDataException("The NPC loot override no longer matches its authored template: " + hash);
            }

            for (int i = 0; i < history.Revisions.Length; i++)
            {
                NpcEditorLootRevision revision = history.Revisions[i];
                if (revision == null || revision.Revision != i + 1 || revision.AtUtc == default
                    || string.IsNullOrWhiteSpace(revision.Actor) || revision.Actor.Length > 128
                    || revision.Action is not ("save" or "revert")
                    || revision.Entries == null || revision.Entries.Length > MaxRowsPerTemplate
                    || revision.Entries.Any(entry => !ValidEntry(entry)) || !ValidBands(leaf, revision.Bands))
                {
                    throw new InvalidDataException("Invalid NPC loot override revision for " + hash + ".");
                }
            }

            if (history.Revisions[^1].Overrides.Any(entry => !itemExists(entry.ItemHash)))
                throw new InvalidDataException("The NPC loot override references an unavailable item family: " + hash);
        }

        return journal with { FileFingerprint = NpcEditorOverrideFile.Fingerprint(bytes) };
    }

    internal NpcEditorLootJournal Write(NpcEditorLootJournal current, NpcEditorLootJournal updated)
    {
        try
        {
            NpcEditorLootJournal next = updated with { Version = 2 };
            return next with { FileFingerprint = _file.Write(current.FileFingerprint, next, JsonOptions).Fingerprint };
        }
        catch (NpcEditorRuntimeException exception) when (exception.StatusCode == 409)
        {
            throw new NpcEditorLootConflictException(exception.Message);
        }
    }

    internal static bool ValidEntry(NpcEditorLootEntry? entry)
        => entry != null && !string.IsNullOrWhiteSpace(entry.ItemHash) && entry.ItemHash.Length <= 64
            && entry.Chance is >= 0 and <= 100 && entry.Repeats is >= 1 and <= Corpse.LootCapacity
            && entry.LevelMod is >= 0 and <= 100
            && ((entry.MinQuality == null && entry.MaxQuality == null)
                || (entry.LevelMod == 0 && entry.MinQuality is > 0 && entry.MaxQuality >= entry.MinQuality
                    && entry.MaxQuality < int.MaxValue));

    internal static bool ValidBands(NpcLeaf leaf, NpcEditorLootBand[]? bands)
    {
        var seen = new HashSet<int>();
        foreach (NpcEditorLootBand band in bands ?? [])
        {
            if (band == null || band.BandIndex < 0 || band.BandIndex >= leaf.Bands.Length
                || !seen.Add(band.BandIndex) || band.Rows == null)
                return false;

            List<MobItemTableEntry> authored = leaf.Bands[band.BandIndex].LootTable ?? [];
            if (band.Rows.Length > authored.Count + MaxRowsPerTemplate)
                return false;
            int lastIndex = -1;
            bool customRowsStarted = false;
            foreach (NpcEditorLootBandRow row in band.Rows)
            {
                if (row == null || (row.Entry != null && !ValidEntry(row.Entry)))
                    return false;
                if (row.AuthoredIndex is not int index)
                {
                    if (row.Entry == null)
                        return false;
                    customRowsStarted = true;
                    continue;
                }

                if (customRowsStarted || index <= lastIndex || index >= authored.Count || authored[index] == null)
                    return false;
                lastIndex = index;
            }
        }

        return true;
    }

    internal static string TemplateSignature(NpcLeaf leaf)
        => NpcEditorOverrideFile.Fingerprint(JsonSerializer.SerializeToUtf8Bytes(leaf.Bands, JsonOptions));
}
