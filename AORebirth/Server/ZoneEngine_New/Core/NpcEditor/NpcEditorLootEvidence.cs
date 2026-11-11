namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

using Microsoft.VisualBasic.FileIO;

using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Logging;

internal sealed record LootEvidenceInput(string? EnemyName, string? EnemyIdentity, int? PlayfieldId, int? MonsterData,
    string? TemplateHash, int? Level, int LowId, int HighId, int Quality, int ObservationCount = 1,
    DateTimeOffset? CapturedUtc = null, string? Patch = null, string? Classification = null, string? SourceRow = null);

internal sealed record LootEvidenceObservation(string Id, string CaptureSource, string FileName, string FileFingerprint, string Format,
    string SourceRow, string? EnemyName, string? EnemyIdentity, int? PlayfieldId, int? MonsterData, string? TemplateHash,
    int? Level, int LowId, int HighId, int Quality, int ObservationCount, DateTimeOffset? CapturedUtc, string? Patch,
    string Classification, string Confidence, string? ClaimedClassification, string ImportedBy, DateTimeOffset ImportedUtc,
    string Review = "pending", string? ReviewedBy = null, DateTimeOffset? ReviewedUtc = null);

internal sealed record LootEvidenceImport([property: JsonRequired] int ExpectedRevision, string CaptureSource, string FileName,
    string Format, string Content);
internal sealed record LootEvidenceReview([property: JsonRequired] int ExpectedRevision, string Id, string Decision);
internal sealed record LootEvidenceImportResult(int Revision, int Imported, int Duplicates, int Skipped);
internal sealed record LootEvidencePage(int Revision, int Total, LootEvidenceObservation[] Rows, string? UnavailableReason);

/// <summary>Review-only observations. The corpse resolver never reads this document.</summary>
internal sealed class NpcEditorLootEvidence
{
    internal const int MaxRequestBytes = 2 * 1024 * 1024;
    internal const int MaxContentBytes = 256 * 1024;
    const int MaxImportRows = 2000;
    const int MaxStoredRows = 10000;
    const string Observed = "OBSERVED_AVAILABLE_LOOT";
    const string Unresolved = "UNRESOLVED";

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    readonly NpcEditorOverrideFile _file;
    readonly Lock _sync = new();
    readonly string? _loadProblem;
    volatile Document _document = new(1, 0, []);

    internal NpcEditorLootEvidence(string root, string? overlay, IZoneLogger logger)
    {
        _file = new(root, overlay, "NpcEditorLootEvidence.json", "loot evidence");
        try
        {
            byte[]? bytes = _file.Read();
            if (bytes == null)
                return;
            Document? document = JsonSerializer.Deserialize<Document>(bytes, Json);
            if (document is not { Version: 1, Revision: >= 0, Rows.Length: <= MaxStoredRows }
                || document.Rows.Any(row => row == null || !ValidStored(row))
                || document.Rows.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count() != document.Rows.Length)
                throw new InvalidDataException("Invalid loot evidence document.");
            _document = document with { Fingerprint = NpcEditorOverrideFile.Fingerprint(bytes) };
        }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            _loadProblem = "The loot evidence document failed validation. Repair it and restart ZoneEngine.";
            logger.Error(error, _loadProblem);
        }
    }

    internal (int Revision, LootEvidenceObservation[] Rows, string? Problem) Snapshot()
    {
        Document document = _document;
        return (document.Revision, document.Rows, _loadProblem ?? _file.ConfigurationProblem);
    }

    internal LootEvidencePage Read(string? query, string? classification, string? review, int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > 200 || query?.Length > 128)
            throw new ArgumentException("Use a short search and a page size from 1 to 200.");
        var (revision, rows, problem) = Snapshot();
        string term = query?.Trim() ?? "";
        LootEvidenceObservation[] matches = rows.Where(row =>
            (classification == null || row.Classification == classification)
            && (review == null || row.Review == review)
            && (term.Length == 0 || Contains(row.EnemyName, term) || Contains(row.TemplateHash, term)
                || Contains(row.CaptureSource, term) || row.LowId.ToString(CultureInfo.InvariantCulture) == term
                || row.HighId.ToString(CultureInfo.InvariantCulture) == term)).ToArray();
        return new(revision, matches.Length, matches.Skip(offset).Take(limit).ToArray(), problem);
    }

    internal LootEvidenceImportResult Import(LootEvidenceImport request, string actor)
    {
        if (!ValidText(request.CaptureSource, 128) || !ValidText(request.FileName, 128)
            || request.FileName.Contains('/') || request.FileName.Contains('\\') || request.Content == null
            || Encoding.UTF8.GetByteCount(request.Content) is < 1 or > MaxContentBytes)
            throw new ArgumentException("Supply a capture label, a file name, and up to 256 KiB of observations.");
        if (request.Format is not ("aosharp" or "decoded" or "json"))
            throw new ArgumentException("Select a supported capture projection format.");

        string fingerprint = NpcEditorOverrideFile.Fingerprint(Encoding.UTF8.GetBytes(request.Content));
        List<LootEvidenceInput> inputs;
        int skipped = 0;
        try
        {
            inputs = request.Format == "json"
                ? JsonSerializer.Deserialize<List<LootEvidenceInput>>(request.Content.TrimStart('\uFEFF'), Json)
                    ?? throw new ArgumentException("An observation array is required.")
                : ReadCsv(request.Content, request.Format == "aosharp", out skipped);
        }
        catch (Exception error) when (error is JsonException or MalformedLineException)
        {
            throw new ArgumentException("The evidence file does not match the selected format.");
        }
        if (inputs.Count > MaxImportRows || inputs.Any(input => input == null || !ValidInput(input)))
            throw new ArgumentException("The evidence file exceeds 2,000 item observations or contains an invalid observation.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var incoming = inputs.Select((row, index) => Create(row, request, fingerprint, index, actor, now)).ToArray();
        lock (_sync)
        {
            Document current = RequireRevision(request.ExpectedRevision, actor);
            var known = current.Rows.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
            LootEvidenceObservation[] added = incoming.Where(row => known.Add(row.Id)).ToArray();
            if (current.Rows.Length + added.Length > MaxStoredRows)
                throw new NpcEditorRuntimeException(409, "The evidence store has reached its observation limit.");
            if (added.Length > 0)
                Save(current, [.. added, .. current.Rows]);
            return new(_document.Revision, added.Length, incoming.Length - added.Length, skipped);
        }
    }

    internal LootEvidenceObservation Review(LootEvidenceReview edit, string actor)
    {
        if (edit.Decision is not ("approved" or "rejected") || !ValidText(edit.Id, 64))
            throw new ArgumentException("Choose an observation and a review decision.");
        lock (_sync)
        {
            Document current = RequireRevision(edit.ExpectedRevision, actor);
            int index = Array.FindIndex(current.Rows, row => row.Id == edit.Id);
            if (index < 0)
                throw new NpcEditorRuntimeException(404, "This observation is unavailable.");
            LootEvidenceObservation previous = current.Rows[index];
            if (previous.Review == edit.Decision)
                return previous;
            var changed = previous with { Review = edit.Decision, ReviewedBy = actor, ReviewedUtc = DateTimeOffset.UtcNow };
            var rows = (LootEvidenceObservation[])current.Rows.Clone();
            rows[index] = changed;
            Save(current, rows);
            return changed;
        }
    }

    Document RequireRevision(int expectedRevision, string actor)
    {
        if (!ValidText(actor, 128))
            throw new ArgumentException("A valid GM account is required.");
        if ((_loadProblem ?? _file.ConfigurationProblem) is string problem)
            throw new NpcEditorRuntimeException(409, problem);
        if (_document.Revision != expectedRevision)
            throw new NpcEditorRuntimeException(409, "The evidence revision changed. Refresh before saving.");
        return _document;
    }

    void Save(Document current, LootEvidenceObservation[] rows)
    {
        Document next = current with { Revision = checked(current.Revision + 1), Rows = rows };
        _document = next with { Fingerprint = _file.Write(current.Fingerprint, next, Json).Fingerprint };
    }

    static LootEvidenceObservation Create(LootEvidenceInput row, LootEvidenceImport request, string fingerprint,
        int index, string actor, DateTimeOffset at)
    {
        string sourceRow = row.SourceRow ?? $"{fingerprint}/{index + 1}";
        string id = NpcEditorOverrideFile.Fingerprint(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new object[] { request.Format, request.CaptureSource, sourceRow, row.LowId, row.HighId, row.Quality })));
        string classification = row.Classification is null or "GUARANTEED_PROVEN" || row.LowId == 0 || row.HighId == 0 || row.Quality == 0
            ? Unresolved : row.Classification;
        string confidence = request.Format == "aosharp" && classification == Observed ? "Linked initial corpse observation"
            : classification == Observed ? "Reported availability" : "Review required";
        return new(id, request.CaptureSource, request.FileName, fingerprint, request.Format, sourceRow, row.EnemyName,
            row.EnemyIdentity, row.PlayfieldId, row.MonsterData, row.TemplateHash, row.Level, row.LowId, row.HighId,
            row.Quality, row.ObservationCount, row.CapturedUtc, row.Patch, classification, confidence, row.Classification, actor, at);
    }

    static List<LootEvidenceInput> ReadCsv(string content, bool aosh, out int skipped)
    {
        using var csv = new TextFieldParser(new StringReader(content.TrimStart('\uFEFF'))) { HasFieldsEnclosedInQuotes = true };
        csv.SetDelimiters(",");
        string[] fields = csv.ReadFields() ?? [];
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < fields.Length; index++)
            if (!columns.TryAdd(fields[index], index))
                throw new ArgumentException("The evidence CSV contains duplicate column names.");
        string[] required = aosh ? ["InitialSnapshot", "Direction", "Items", "CorrelationStatus", "Sequence", "CorpseIdentity"]
            : ["loot_kind", "low_id", "high_id", "loot_quality", "loot_window_packet", "corpse"];
        if (required.Any(field => !columns.ContainsKey(field)))
            throw new ArgumentException("The evidence CSV is missing required capture columns.");

        var rows = new List<LootEvidenceInput>();
        skipped = 0;
        int line = 1;
        while (!csv.EndOfData)
        {
            string[] values = csv.ReadFields() ?? [];
            line++;
            if (values.Length != fields.Length)
                throw new ArgumentException($"Evidence CSV record {line} has the wrong number of fields.");
            string? Read(string key) => columns.TryGetValue(key, out int position) && values[position].Length > 0
                ? values[position] : null;
            if (aosh)
            {
                if (!string.Equals(Read("InitialSnapshot"), "true", StringComparison.OrdinalIgnoreCase) || Read("Direction") != "IN-N3")
                {
                    skipped++;
                    continue;
                }
                string? identity = Read("DeadNpcIdentity");
                bool linked = identity != null && Read("CorrelationStatus") is "linked-live-generation" or "linked-offline-generation";
                string[] itemFields = (Read("Items") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);
                for (int item = 0; item < itemFields.Length; item++)
                {
                    string[] parts = itemFields[item].Split(':');
                    if (parts.Length != 4)
                        throw new ArgumentException($"Evidence CSV record {line} contains an invalid item.");
                    rows.Add(new(Read("EnemyName"), identity, Positive(Read("PlayfieldId")), Positive(Read("MonsterData")), null,
                        Positive(Read("EnemyLevel")), ItemNumber(parts[0]), ItemNumber(parts[1]), ItemNumber(parts[2]), 1,
                        DateTimeOffset.TryParse(Read("CapturedUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                            out var at) ? at.ToUniversalTime() : null, null, linked ? Observed : Unresolved,
                        $"{Read("Sequence")}/{Read("CorpseIdentity")}/{item + 1}"));
                }
                if (itemFields.Length == 0)
                    skipped++;
            }
            else if (Read("loot_kind") == "Item")
            {
                string? identity = Read("enemy_identity");
                rows.Add(new(Read("enemy_name"), identity, Positive(Read("playfield_id")), null, null, Positive(Read("enemy_level")),
                    ItemNumber(Read("low_id")), ItemNumber(Read("high_id")), ItemNumber(Read("loot_quality")),
                    Classification: identity == null ? Unresolved : Observed,
                    SourceRow: $"{Read("loot_window_packet")}/{Read("corpse")}/{Read("item_identity") ?? line.ToString(CultureInfo.InvariantCulture)}"));
            }
            else
            {
                skipped++;
            }
            if (rows.Count > MaxImportRows)
                throw new ArgumentException("One import can contain up to 2,000 item observations.");
        }
        return rows;
    }

    static bool ValidStored(LootEvidenceObservation row) => ValidText(row.Id, 64) && ValidText(row.CaptureSource, 128)
        && ValidText(row.FileName, 128) && ValidText(row.FileFingerprint, 64) && ValidText(row.ImportedBy, 128)
        && row.Review is "pending" or "approved" or "rejected" && row.Classification != "GUARANTEED_PROVEN"
        && ValidInput(new(row.EnemyName, row.EnemyIdentity, row.PlayfieldId, row.MonsterData, row.TemplateHash, row.Level,
            row.LowId, row.HighId, row.Quality, row.ObservationCount, row.CapturedUtc, row.Patch, row.Classification, row.SourceRow));

    static bool ValidInput(LootEvidenceInput row) => row.LowId >= 0 && row.HighId >= 0 && (row.LowId > 0 || row.HighId > 0)
        && row.Quality is >= 0 and <= 500
        && row.ObservationCount is >= 1 and <= 1000000 && row.PlayfieldId is null or > 0
        && row.MonsterData is null or > 0 && row.Level is null or > 0
        && OptionalText(row.EnemyName, 128) && OptionalText(row.EnemyIdentity, 128) && OptionalText(row.TemplateHash, 64)
        && OptionalText(row.Patch, 64) && OptionalText(row.SourceRow, 256)
        && row.Classification is null or Observed or Unresolved or "GUARANTEED_PROVEN" or "COMMUNITY_DOCUMENTED" or "INFERRED";

    static bool OptionalText(string? value, int limit) => value == null || ValidText(value, limit);
    static bool ValidText(string? value, int limit) => !string.IsNullOrWhiteSpace(value) && value.Length <= limit && !value.Any(char.IsControl);
    static int? Integer(string? value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : null;
    static int? Positive(string? value) => Integer(value) is int number && number > 0 ? number : null;
    static int ItemNumber(string? value) => value == null ? 0 : Integer(value) ?? -1;
    static bool Contains(string? value, string term) => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    sealed record Document(int Version, int Revision, LootEvidenceObservation[] Rows)
    {
        [JsonIgnore]
        internal string? Fingerprint { get; init; }
    }
}
