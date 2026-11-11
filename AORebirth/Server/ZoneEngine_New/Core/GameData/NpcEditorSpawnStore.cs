namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

using AORebirth.Core.GameData;
using AORebirth.Core.Vector;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.NpcEditor;
using ZoneEngine_New.Core.Playfield.Locality;

internal sealed class NpcEditorSpawnStore
{
    internal const string FileName = "NpcEditorSpawnOverrides.json";
    const int MaxRevisions = 128;
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    readonly Lock _sync = new();
    readonly GameDataStore _gameData;
    readonly NpcEditorOverrideFile _file;
    readonly IZoneLogger _logger;
    volatile SpawnJournal _journal = new(1, new(StringComparer.Ordinal));

    internal NpcEditorSpawnStore(GameDataStore gameData, string? overlayRoot, IZoneLogger logger)
    {
        _gameData = gameData;
        _file = new(gameData.RootPath, overlayRoot, FileName, "spawn");
        _logger = logger;
        try
        {
            _journal = Read();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            const string reason = "The spawn overlay failed validation. Repair it and restart ZoneEngine.";
            _journal = _journal with { LoadError = reason };
            _logger.Error(error, reason);
        }
    }

    internal (NpcEditorSpawnRevision[] History, string? Unavailable) Describe(int resourceId, int index, string authoredSignature)
    {
        SpawnJournal journal = _journal;
        string? unavailable = _file.ConfigurationProblem ?? journal.LoadError;
        if (!journal.Points.TryGetValue(Key(resourceId, index), out SpawnHistory? saved))
            return ([], unavailable);
        if (saved.AuthoredSignature != authoredSignature)
            return ([], "The original spawn definition changed. Repair the spawn overlay before editing this placement.");
        return (saved.Revisions, unavailable);
    }

    internal NpcEditorSpawnSettings? Resolve(int resourceId, int index, PlayfieldSpawnEntry authored, CellGrid grid)
    {
        SpawnJournal journal = _journal;
        if (!journal.Points.TryGetValue(Key(resourceId, index), out SpawnHistory? saved))
            return null;
        if (saved.AuthoredSignature != Signature(authored))
        {
            _logger.Warn($"NPC editor spawn override skipped at {resourceId}/{index}: original placement changed.");
            return null;
        }

        NpcEditorSpawnSettings original = NpcEditorSpawnSettings.From(authored);
        NpcEditorSpawnSettings settings = saved.Revisions[^1].After;
        if (ValidationProblem(resourceId, grid, original, settings) is string problem)
        {
            _logger.Warn($"NPC editor spawn override skipped at {resourceId}/{index}: {problem}");
            return null;
        }

        return settings;
    }

    internal NpcEditorSpawnRevision[] Change(NpcEditorSpawnTarget target, int expectedRevision, string actor,
        NpcEditorSpawnSettings? edited, int? restoreRevision)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 128)
            throw new NpcEditorRuntimeException(400, "A valid GM actor is required.");
        lock (_sync)
        {
            SpawnJournal journal = _journal;
            if ((_file.ConfigurationProblem ?? journal.LoadError) is string problem)
                throw new NpcEditorRuntimeException(409, problem);

            PlayfieldSpawnEntry[] entries = _gameData.ReadAuthoredEditorSpawns(target.ResourcePlayfieldId).Spawns;
            if (target.AuthoredIndex < 0 || target.AuthoredIndex >= entries.Length
                || entries[target.AuthoredIndex] is not { } authored || Signature(authored) != target.AuthoredSignature)
            {
                throw new NpcEditorRuntimeException(409, "The authored spawn changed. Select a placement from a reconstructed playfield.");
            }

            string key = Key(target.ResourcePlayfieldId, target.AuthoredIndex);
            journal.Points.TryGetValue(key, out SpawnHistory? current);
            if (current != null && current.AuthoredSignature != target.AuthoredSignature)
                throw new NpcEditorRuntimeException(409, "The saved spawn journal refers to a different authored placement.");
            NpcEditorSpawnRevision[] history = current?.Revisions ?? [];
            if (history.Length != expectedRevision)
                throw new NpcEditorRuntimeException(409, "The spawn revision changed. Refresh the editor before saving.");
            if (history.Length >= MaxRevisions)
                throw new NpcEditorRuntimeException(409, "The spawn reached its revision limit.");

            NpcEditorSpawnSettings original = NpcEditorSpawnSettings.From(authored);
            NpcEditorSpawnSettings before = history.Length == 0 ? original : history[^1].After;
            NpcEditorSpawnSettings after;
            if (restoreRevision is int restore)
            {
                if (restore < 0 || restore >= history.Length)
                    throw new NpcEditorRuntimeException(400, "Choose an earlier spawn revision.");
                after = restore == 0 ? original : history[restore - 1].After;
            }
            else
            {
                after = edited ?? throw new NpcEditorRuntimeException(400, "Spawn settings are required.");
            }

            ValidateChange(target with { View = target.View with { Original = original } }, before, after);

            NpcEditorSpawnRevision[] updated = [.. history, new(history.Length + 1, actor, DateTimeOffset.UtcNow,
                restoreRevision == null ? "save" : "restore", before, after)];
            var points = new Dictionary<string, SpawnHistory>(journal.Points, StringComparer.Ordinal)
            {
                [key] = new(target.ResourcePlayfieldId, target.AuthoredIndex, target.AuthoredSignature,
                    target.View.SpawnPointKey, updated)
            };
            SpawnJournal next = journal with { Points = points };
            string fingerprint;
            try
            {
                fingerprint = _file.Write(journal.Fingerprint, next, JsonOptions).Fingerprint;
            }
            catch (NpcEditorRuntimeException error) when (error.StatusCode == 409)
            {
                throw new NpcEditorRuntimeException(409, "The spawn overlay changed on disk. Reopen it in ZoneEngine before saving.");
            }
            _journal = next with { Fingerprint = fingerprint };
            return updated;
        }
    }

    internal void ValidateChange(NpcEditorSpawnTarget target, NpcEditorSpawnSettings before, NpcEditorSpawnSettings after)
    {
        if (!target.View.CanSave || target.View.Original is not NpcEditorSpawnSettings original)
            throw new NpcEditorRuntimeException(409, target.View.UnavailableReason ?? "The authored spawn is unavailable.");
        if (before == after)
            throw new NpcEditorRuntimeException(400, "The configured spawn already has these settings.");
        if (ValidationProblem(target.ResourcePlayfieldId, target.Grid, original, after) is string invalid)
            throw new NpcEditorRuntimeException(400, invalid);
    }

    string? ValidationProblem(int resourceId, CellGrid grid, NpcEditorSpawnSettings original, NpcEditorSpawnSettings settings)
    {
        if (!HasValidShape(settings))
            return "Use finite coordinates, levels 1–65535 in ascending order, nonnegative respawn seconds, and chance 0–100.";
        if ((settings.FacingDegrees != original.FacingDegrees && settings.FacingDegrees is < -360 or > 360)
            || (settings.FacingWidthDegrees != original.FacingWidthDegrees && settings.FacingWidthDegrees is < 0 or > 360))
            return "Use a facing centre between -360 and 360 degrees and a facing width between 0 and 360 degrees.";
        if ((settings.X != original.X || settings.Y != original.Y || settings.Z != original.Z)
            && (!grid.ContainsWorldPosition(settings.X, settings.Z)
                || !grid.TryGetCellId(new Vector3(settings.X, settings.Y, settings.Z), out _, clampToGrid: false)))
            return "The primary site is outside the playfield cell grid.";
        if (settings.Hash != original.Hash && (_gameData.SpawnExclusions.Excludes(settings.Hash, resourceId)
            || !_gameData.TryGetMobTemplate(settings.Hash, out MobTemplate template)
            || template.UnresolvedPlaceholder || !NpcTemplateValidation.CanSpawn(template)))
        {
            return "Reassignment requires an exact, enabled NPC template hash in this playfield.";
        }

        return null;
    }

    static bool HasValidShape(NpcEditorSpawnSettings? settings)
        => settings != null && float.IsFinite(settings.X) && float.IsFinite(settings.Y) && float.IsFinite(settings.Z)
            && settings.MinLevel is >= 1 and <= ushort.MaxValue && settings.MaxLevel >= settings.MinLevel
            && settings.MaxLevel <= ushort.MaxValue && settings.RespawnSeconds >= 0 && settings.RespawnChance is >= 0 and <= 100
            && !string.IsNullOrWhiteSpace(settings.Hash) && settings.Hash.Length <= 64;

    SpawnJournal Read()
    {
        byte[]? bytes = _file.Read();
        if (bytes == null)
            return _journal;
        SpawnJournal journal = JsonSerializer.Deserialize<SpawnJournal>(bytes, JsonOptions)
            ?? throw new InvalidDataException("The spawn override document is empty.");
        if (journal.Version != 1 || journal.Points == null)
            throw new InvalidDataException("Unsupported spawn override document.");

        foreach ((string key, SpawnHistory point) in journal.Points)
        {
            if (point == null || point.ResourcePlayfieldId <= 0 || point.AuthoredIndex < 0
                || key != Key(point.ResourcePlayfieldId, point.AuthoredIndex)
                || point.AuthoredSignature is not { Length: 64 }
                || point.Revisions is not { Length: >= 1 and <= MaxRevisions })
                throw new InvalidDataException($"Invalid spawn overlay placement {key}.");
            for (int index = 0; index < point.Revisions.Length; index++)
            {
                NpcEditorSpawnRevision revision = point.Revisions[index];
                if (revision == null || revision.Revision != index + 1 || string.IsNullOrWhiteSpace(revision.Actor)
                    || revision.Actor.Length > 128 || revision.AtUtc == default || revision.Action is not ("save" or "restore")
                    || !HasValidShape(revision.Before) || !HasValidShape(revision.After)
                    || (index > 0 && revision.Before != point.Revisions[index - 1].After))
                    throw new InvalidDataException($"Invalid spawn overlay revision at {key}.");
            }
        }

        return journal with { Fingerprint = NpcEditorOverrideFile.Fingerprint(bytes) };
    }

    internal static string Signature(PlayfieldSpawnEntry authored)
        => NpcEditorOverrideFile.Fingerprint(JsonSerializer.SerializeToUtf8Bytes(authored, JsonOptions));

    static string Key(int resourceId, int index)
        => string.Create(CultureInfo.InvariantCulture, $"{resourceId}/{index}");

    sealed record SpawnHistory(int ResourcePlayfieldId, int AuthoredIndex, string AuthoredSignature,
        string? SpawnPointKey, NpcEditorSpawnRevision[] Revisions);

    sealed record SpawnJournal(int Version, Dictionary<string, SpawnHistory> Points)
    {
        [JsonIgnore]
        internal string? Fingerprint { get; init; }
        [JsonIgnore]
        internal string? LoadError { get; init; }
    }
}
