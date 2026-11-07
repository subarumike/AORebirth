namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using ZoneEngine_New.Core.Entities;
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
}

/// <summary>Writes the editor's own versioned document to a separately configured GameData overlay.</summary>
internal sealed class NpcEditorLootJournalFile
{
    internal const string FileName = "NpcEditorLootOverrides.json";
    internal const int MaxRowsPerTemplate = 64;
    internal const int MaxRevisionsPerTemplate = 256;
    const int MaxDocumentBytes = 8 * 1024 * 1024;
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    readonly string? _overlayRoot;
    readonly string _dataRoot;

    internal NpcEditorLootJournalFile(string dataRoot, string? overlayRoot)
    {
        _dataRoot = dataRoot;
        _overlayRoot = overlayRoot;
        ConfigurationProblem = CheckConfiguration();
    }

    internal string? ConfigurationProblem { get; }

    string? CheckConfiguration()
    {
        if (_overlayRoot == null)
            return "Configure a separate AO_REBIRTH_GAMEDATA_OVERRIDE_PATH on ZoneEngine to save loot.";
        if (IsInside(_overlayRoot, _dataRoot) || IsInside(_dataRoot, _overlayRoot))
            return "The GameData override and runtime GameData folders must be separate.";
        if (!Directory.Exists(_overlayRoot))
            return "The configured GameData override folder does not exist.";
        return null;
    }

    internal NpcEditorLootJournal Read(NpcTemplateCatalog catalog, Func<string, bool> itemExists)
    {
        if (ConfigurationProblem != null)
            return NpcEditorLootJournal.Empty;

        string path = RequirePath();
        byte[]? bytes = ReadBytes(path);
        if (bytes == null)
            return NpcEditorLootJournal.Empty;

        NpcEditorLootJournal journal = JsonSerializer.Deserialize<NpcEditorLootJournal>(bytes, JsonOptions)
            ?? throw new InvalidDataException("The NPC loot override document is empty.");
        if (journal.Version != 1 || journal.Templates == null)
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
                    || revision.Entries.Any(entry => !ValidEntry(entry)))
                {
                    throw new InvalidDataException("Invalid NPC loot override revision for " + hash + ".");
                }
            }

            if (history.Revisions[^1].Entries.Any(entry => !itemExists(entry.ItemHash)))
                throw new InvalidDataException("The NPC loot override references an unavailable item family: " + hash);
        }

        return journal with { FileFingerprint = Fingerprint(bytes) };
    }

    internal NpcEditorLootJournal Write(NpcEditorLootJournal current, NpcEditorLootJournal updated)
    {
        string path = RequirePath();
        string lockPath = path + ".lock";
        RejectReparsePoint(lockPath);
        using var writeLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        byte[]? disk = ReadBytes(path);
        string? diskFingerprint = disk == null ? null : Fingerprint(disk);
        if (diskFingerprint != current.FileFingerprint)
            throw new NpcEditorLootConflictException("The loot override file changed. Reload NpcTemplates and refresh before saving.");

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(updated, JsonOptions);
        if (bytes.Length > MaxDocumentBytes)
            throw new InvalidOperationException("The loot override history has reached the document size limit.");

        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }

        return updated with { FileFingerprint = Fingerprint(bytes) };
    }

    internal static bool ValidEntry(NpcEditorLootEntry? entry)
        => entry != null && !string.IsNullOrWhiteSpace(entry.ItemHash) && entry.ItemHash.Length <= 64
            && entry.Chance is >= 0 and <= 100 && entry.Repeats is >= 1 and <= Corpse.LootCapacity
            && entry.LevelMod is >= 0 and <= 100;

    internal static string TemplateSignature(NpcLeaf leaf)
        => Fingerprint(JsonSerializer.SerializeToUtf8Bytes(leaf.Bands, JsonOptions));

    string RequirePath()
    {
        if (ConfigurationProblem is string problem)
            throw new InvalidOperationException(problem);

        RejectDirectoryLinks(_overlayRoot!);
        RejectDirectoryLinks(_dataRoot);
        return Path.Combine(_overlayRoot!, FileName);
    }

    static byte[]? ReadBytes(string path)
    {
        RejectReparsePoint(path);
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (file.Length is < 1 or > MaxDocumentBytes)
                throw new InvalidDataException("Invalid NPC loot override document size.");
            byte[] bytes = new byte[file.Length];
            file.ReadExactly(bytes);
            return bytes;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    static bool IsInside(string path, string root)
    {
        string relative = Path.GetRelativePath(root, path);
        return !Path.IsPathRooted(relative) && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    static void RejectDirectoryLinks(string path)
    {
        for (DirectoryInfo? directory = new(path); directory != null; directory = directory.Parent)
            RejectReparsePoint(directory.FullName);
    }

    static void RejectReparsePoint(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("NPC loot overrides require paths without symlinks or junctions.");
        }
        catch (FileNotFoundException)
        {
            // An override or lock file can be absent before the first save.
        }
    }

    static string Fingerprint(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
