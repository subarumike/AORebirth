namespace ZoneEngine_New.Core.GameData;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed record NamedLootRevision(
    int Revision, DateTimeOffset AtUtc, string Actor, string Action, string Target,
    NamedLootTable[] Tables, NamedLootAssignment[] Assignments);

internal sealed record NamedLootJournal(int Version, NamedLootRevision[] Revisions)
{
    internal static readonly NamedLootJournal Empty = new(1, []);
    [JsonIgnore]
    internal string? Fingerprint { get; init; }
    [JsonIgnore]
    internal string? LoadError { get; init; }
    internal NamedLootRevision? Latest => Revisions.Length > 0 ? Revisions[^1] : null;
}

internal sealed class NamedLootJournalFile(string dataRoot, string? overlayRoot)
{
    internal const string FileName = "NpcEditorNamedLoot.json";
    internal const int MaxRevisions = 128;
    static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    readonly NpcEditorOverrideFile _file = new(dataRoot, overlayRoot, FileName, "named NPC loot");
    internal string? ConfigurationProblem => _file.ConfigurationProblem;

    internal NamedLootJournal Read()
    {
        byte[]? bytes = _file.Read();
        if (bytes == null)
            return NamedLootJournal.Empty;
        NamedLootJournal journal = JsonSerializer.Deserialize<NamedLootJournal>(bytes, Options)
            ?? throw new InvalidDataException("The named loot journal is empty.");
        if (journal.Version != 1 || journal.Revisions == null || journal.Revisions.Length is < 1 or > MaxRevisions)
            throw new InvalidDataException("Unsupported named loot journal.");
        for (int index = 0; index < journal.Revisions.Length; index++)
        {
            NamedLootRevision revision = journal.Revisions[index];
            if (revision == null || revision.Revision != index + 1 || revision.AtUtc == default
                || string.IsNullOrWhiteSpace(revision.Actor) || revision.Actor.Length > 128
                || revision.Action is not ("saveTable" or "deleteTable" or "saveAssignment" or "deleteAssignment" or "restore")
                || string.IsNullOrWhiteSpace(revision.Target) || revision.Target.Length > 64
                || revision.Tables == null || revision.Tables.Length > NamedLootValidation.MaxTables
                || revision.Assignments == null || revision.Assignments.Length > NamedLootValidation.MaxAssignments)
                throw new InvalidDataException("The named loot journal contains an invalid revision.");
        }
        return journal with { Fingerprint = NpcEditorOverrideFile.Fingerprint(bytes) };
    }

    internal NamedLootJournal Write(NamedLootJournal previous, NamedLootJournal next)
        => next with { Fingerprint = _file.Write(previous.Fingerprint, next, Options).Fingerprint };
}
