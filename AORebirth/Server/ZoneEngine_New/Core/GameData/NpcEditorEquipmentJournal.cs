namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Mobs;

internal sealed record NpcEditorEquipmentEntry(int? SourceIndex, int? ItemId);

internal sealed record NpcEditorEquipmentRevision(
    int Revision, DateTimeOffset AtUtc, string Actor, string Action, NpcEditorEquipmentEntry[] Entries);

internal sealed record NpcEditorEquipmentHistory(
    string TemplateHash, int BandIndex, string Signature, NpcEditorEquipmentRevision[] Revisions);

internal sealed record NpcEditorEquipmentJournal(int Version, Dictionary<string, NpcEditorEquipmentHistory> Bands)
{
    internal static readonly NpcEditorEquipmentJournal Empty = new(1, new(StringComparer.Ordinal));
    [JsonIgnore]
    internal string? FileFingerprint { get; init; }
    [JsonIgnore]
    internal string? LoadError { get; init; }

    internal NpcEditorEquipmentHistory? Find(string hash, int bandIndex) => Bands.GetValueOrDefault(Key(hash, bandIndex));

    internal static string Key(string hash, int bandIndex) => hash + ":" + bandIndex.ToString(CultureInfo.InvariantCulture);

    internal static NpcEditorEquipmentEntry[] Authored(NpcLevelBand band)
        => Enumerable.Range(0, band.Equipment?.Count ?? 0).Select(i => new NpcEditorEquipmentEntry(i, null)).ToArray();
}

internal sealed class NpcEditorEquipmentJournalFile(string dataRoot, string? overlayRoot)
{
    internal const string FileName = "NpcEditorEquipmentOverrides.json";
    internal const int MaxRevisionsPerBand = 256;

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    readonly NpcEditorOverrideFile _file = new(dataRoot, overlayRoot, FileName, "NPC equipment");

    internal string? ConfigurationProblem => _file.ConfigurationProblem;

    internal NpcEditorEquipmentJournal Read(NpcTemplateCatalog catalog)
    {
        byte[]? bytes = _file.Read();
        if (bytes == null)
            return NpcEditorEquipmentJournal.Empty;
        NpcEditorEquipmentJournal journal = JsonSerializer.Deserialize<NpcEditorEquipmentJournal>(bytes, JsonOptions)
            ?? throw new InvalidDataException("The NPC equipment override document is empty.");
        if (journal.Version != 1 || journal.Bands == null)
            throw new InvalidDataException("Unsupported NPC equipment override document.");

        foreach ((string key, NpcEditorEquipmentHistory history) in journal.Bands)
        {
            if (history == null || string.IsNullOrWhiteSpace(history.TemplateHash)
                || key != NpcEditorEquipmentJournal.Key(history.TemplateHash, history.BandIndex)
                || !catalog.TryGetEquipmentBand(history.TemplateHash, history.BandIndex, out NpcLevelBand band)
                || Signature(band) != history.Signature
                || history.Revisions == null || history.Revisions.Length is < 1 or > MaxRevisionsPerBand)
                throw new InvalidDataException("The NPC equipment override no longer matches its authored template: " + key);

            for (int i = 0; i < history.Revisions.Length; i++)
            {
                NpcEditorEquipmentRevision revision = history.Revisions[i];
                if (revision == null || revision.Revision != i + 1 || revision.AtUtc == default
                    || string.IsNullOrWhiteSpace(revision.Actor) || revision.Actor.Length > 128
                    || revision.Action is not ("save" or "restore")
                    || !ValidEntries(band, revision.Entries))
                    throw new InvalidDataException("Invalid NPC equipment override revision for " + key + ".");
            }
        }
        return journal with { FileFingerprint = NpcEditorOverrideFile.Fingerprint(bytes) };
    }

    internal NpcEditorEquipmentJournal Write(NpcEditorEquipmentJournal current, NpcEditorEquipmentJournal updated)
    {
        var (_, fingerprint) = _file.Write(current.FileFingerprint, updated, JsonOptions);
        return updated with { FileFingerprint = fingerprint };
    }

    internal static bool ValidEntries(NpcLevelBand band, NpcEditorEquipmentEntry[]? entries)
    {
        if (entries == null || entries.Length > NpcCharacter.EquipmentCapacity)
            return false;
        int last = -1;
        int authoredCount = band.Equipment?.Count ?? 0;
        bool sawAddition = false;
        foreach (NpcEditorEquipmentEntry entry in entries)
        {
            if (entry == null || entry.ItemId is <= 0)
                return false;
            if (entry.SourceIndex is int index)
            {
                if (sawAddition || index <= last || index >= authoredCount)
                    return false;
                last = index;
            }
            else
            {
                if (entry.ItemId == null)
                    return false;
                sawAddition = true;
            }
        }
        return true;
    }

    internal static string Signature(NpcLevelBand band)
        => NpcEditorOverrideFile.Fingerprint(JsonSerializer.SerializeToUtf8Bytes(
            new { band.Level, band.TemplateId, band.Equipment }, JsonOptions));
}
