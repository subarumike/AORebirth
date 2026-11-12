using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AORebirth.NpcEditor.Official;

public sealed record OfficialLootObservation(
    OfficialSelectionLock Target, long CapturedUtcTicks, string Status, string? CorpseIdentity = null,
    string? ItemName = null, int? LowId = null, int? HighId = null, int? Quality = null, int? Quantity = null,
    int? Credits = null, string? Notes = null)
{
    public string Source => "Manual official AO loot observation";
    public string Classification => Status == "UNKNOWN_LOOT" ? "NOT_AVAILABLE" : "GAMEPLAY_OBSERVED";
    public string? Confidence => null;
}

public sealed record OfficialExportPaths(string JsonPath, string CsvPath);

/// <summary>Exports captured evidence locally without touching AORebirth GameData or contacting a server.</summary>
public static class OfficialObservationExport
{
    const int MaxLootRows = 128;
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    static readonly UTF8Encoding CsvEncoding = new(encoderShouldEmitUTF8Identifier: true);

    public static void ValidateLoot(OfficialLootObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.Target is not { Session.ProcessId: > 0, PlayfieldId: > 0, IdentityType: > 0, InstanceId: not 0, MinimumSequence: > 0 } target
            || !OfficialSnapshotReader.ValidGuid(target.Session.SessionId) || !OfficialSnapshotReader.ValidGuid(target.SelectionId)
            || !OfficialSnapshotReader.ValidTicks(observation.CapturedUtcTicks))
            throw new ArgumentException("A captured NPC generation and observation timestamp are required.");

        if (observation.Status is not ("OBSERVED_DROP" or "UNKNOWN_LOOT" or "UNRESOLVED_ITEM")
            || observation.LowId is <= 0 || observation.HighId is <= 0 || observation.Quality is <= 0 or > ushort.MaxValue
            || observation.Quantity is <= 0 || observation.Credits is < 0
            || !OfficialSnapshotReader.ValidText(observation.CorpseIdentity, 160)
            || !OfficialSnapshotReader.ValidText(observation.ItemName, 256)
            || !OfficialSnapshotReader.ValidText(observation.Notes, 2048))
            throw new ArgumentException("Enter a supported observation status and valid optional loot values.");

        bool hasItem = !string.IsNullOrWhiteSpace(observation.ItemName) || observation.LowId.HasValue || observation.HighId.HasValue;
        if (observation.Status == "UNKNOWN_LOOT" ? hasItem || observation.Quality.HasValue || observation.Quantity.HasValue : !hasItem)
            throw new ArgumentException("Record an observed item name or ID, or leave the item fields blank for unknown loot.");
    }

    public static OfficialExportPaths Write(OfficialSnapshot snapshot, IReadOnlyList<OfficialLootObservation> loot)
        => WriteAt(Path.Combine(OfficialLocalPaths.ResearchDirectory, "Exports"), snapshot, loot);

    internal static OfficialExportPaths WriteAt(string directory, OfficialSnapshot snapshot, IReadOnlyList<OfficialLootObservation> loot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(loot);
        if (snapshot.State != "selected" || !OfficialSnapshotReader.IsValid(snapshot) || loot.Count > MaxLootRows)
            throw new ArgumentException("A valid selected NPC snapshot and at most 128 manual loot observations are required.");

        foreach (OfficialLootObservation observation in loot)
        {
            ValidateLoot(observation);
            if (!observation.Target.Matches(snapshot))
                throw new ArgumentException("Every manual loot observation must match the exported NPC generation.");
        }

        string safeDirectory = OfficialLocalPaths.RequireLocalDirectory(directory);
        Directory.CreateDirectory(safeDirectory);
        OfficialLocalPaths.RequireLocalDirectory(safeDirectory);

        DateTimeOffset captured = new(snapshot.CapturedUtcTicks, TimeSpan.Zero);
        string stem = FormattableString.Invariant($"official-npc-{captured:yyyyMMdd-HHmmss}-{snapshot.ProcessId}-{Guid.NewGuid():N}");
        string jsonPath = Path.Combine(safeDirectory, stem + ".json");
        string csvPath = Path.Combine(safeDirectory, stem + ".csv");
        bool createdJson = false;
        bool createdCsv = false;
        try
        {
            using (var file = OpenNew(jsonPath))
            {
                createdJson = true;
                JsonSerializer.Serialize(file, new
                {
                    version = 1,
                    mode = "officialAOReadOnly",
                    exportedAtUtc = DateTimeOffset.UtcNow,
                    snapshot,
                    manualLoot = loot
                }, JsonOptions);
                file.Flush(flushToDisk: true);
            }
            using (var file = OpenNew(csvPath))
            {
                createdCsv = true;
                using var writer = new StreamWriter(file, CsvEncoding, leaveOpen: true);
                WriteCsv(writer, snapshot, loot);
                writer.Flush();
                file.Flush(flushToDisk: true);
            }
        }
        catch
        {
            if (createdCsv)
                TryDelete(csvPath);
            if (createdJson)
                TryDelete(jsonPath);
            throw;
        }
        return new(jsonPath, csvPath);
    }

    static FileStream OpenNew(string path) => new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);

    static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    static void WriteCsv(TextWriter writer, OfficialSnapshot snapshot, IReadOnlyList<OfficialLootObservation> loot)
    {
        WriteRow(writer, "record_kind", "captured_utc", "snapshot_utc", "process_id", "session_id", "sequence", "selection_id",
            "playfield_id", "identity_type", "instance_id", "npc_name", "monster_data", "npc_level", "key", "value",
            "unit", "source", "classification", "confidence", "loot_status", "corpse_identity", "item_name", "low_id", "high_id", "quality",
            "quantity", "credits", "notes");

        Row("snapshot", snapshot.CapturedUtcTicks, "identity", null, null, "AOSharp selected-target snapshot", "CLIENT_EXPOSED", null);
        foreach (OfficialField field in snapshot.Fields)
            Row("field", snapshot.CapturedUtcTicks, field.Key, field.Value, field.Unit, field.Source, field.Classification, field.Confidence);
        foreach (OfficialEvent observation in snapshot.Events)
            Row("event", observation.CapturedUtcTicks, observation.Kind, observation.Detail, null, observation.Source,
                observation.Classification, observation.Confidence);
        foreach (OfficialLootObservation observation in loot)
            Row("manualLoot", observation.CapturedUtcTicks, null, null, null, observation.Source,
                observation.Classification, observation.Confidence, observation);

        void Row(string kind, long ticks, string? key, string? value, string? unit,
            string source, string classification, string? confidence, OfficialLootObservation? observation = null)
            => WriteRow(writer, kind, Timestamp(ticks), Timestamp(snapshot.CapturedUtcTicks), snapshot.ProcessId, snapshot.SessionId,
                snapshot.Sequence, snapshot.SelectionId, snapshot.PlayfieldId, snapshot.IdentityType, snapshot.InstanceId,
                snapshot.Name, snapshot.MonsterData, snapshot.Level, key, value, unit, source, classification, confidence ?? "UNKNOWN",
                observation?.Status,
                observation?.CorpseIdentity, observation?.ItemName, observation?.LowId, observation?.HighId,
                observation?.Quality, observation?.Quantity, observation?.Credits, observation?.Notes);
    }

    static string Timestamp(long ticks) => new DateTime(ticks, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);

    static void WriteRow(TextWriter writer, params object?[] cells)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            if (i > 0)
                writer.Write(',');
            string value = Convert.ToString(cells[i], CultureInfo.InvariantCulture) ?? string.Empty;
            string trimmed = value.TrimStart();
            if (trimmed.Length > 0 && trimmed[0] is '=' or '+' or '-' or '@'
                && !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                value = "'" + value;
            writer.Write('"');
            writer.Write(value.Replace("\"", "\"\"", StringComparison.Ordinal));
            writer.Write('"');
        }
        writer.WriteLine();
    }
}
