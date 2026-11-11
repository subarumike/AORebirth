namespace ZoneEngine_New.Core.GameData;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

using ZoneEngine_New.Core.NpcEditor;

/// <summary>Bounded, atomic storage for versioned NPC editor documents in the isolated GameData overlay.</summary>
internal sealed class NpcEditorOverrideFile
{
    const int MaxDocumentBytes = 8 * 1024 * 1024;
    readonly string _dataRoot;
    readonly string? _overlayRoot;
    readonly string _fileName;
    readonly string _subject;

    internal NpcEditorOverrideFile(string dataRoot, string? overlayRoot, string fileName, string subject)
    {
        _dataRoot = dataRoot;
        _overlayRoot = overlayRoot;
        _fileName = fileName;
        _subject = subject;
        ConfigurationProblem = CheckConfiguration();
    }

    internal string? ConfigurationProblem { get; }

    string? CheckConfiguration()
    {
        if (_overlayRoot == null)
            return "Configure a separate AO_REBIRTH_GAMEDATA_OVERRIDE_PATH on ZoneEngine to save " + _subject + ".";
        if (IsInside(_overlayRoot, _dataRoot) || IsInside(_dataRoot, _overlayRoot))
            return "The GameData override and runtime GameData folders must be separate.";
        if (!Directory.Exists(_overlayRoot))
            return "The configured GameData override folder does not exist.";
        return null;
    }

    internal byte[]? Read() => ConfigurationProblem == null ? ReadBytes(RequirePath()) : null;

    internal (byte[] Bytes, string Fingerprint) Write<T>(
        string? expectedFingerprint, T document, JsonSerializerOptions options)
    {
        string path = RequirePath();
        string lockPath = path + ".lock";
        RejectReparsePoint(lockPath);
        using var writeLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        byte[]? disk = ReadBytes(path);
        string? diskFingerprint = disk == null ? null : Fingerprint(disk);
        if (diskFingerprint != expectedFingerprint)
            throw new NpcEditorRuntimeException(409, $"The {_subject} override file changed. Reload NpcTemplates before saving.");

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, options);
        if (bytes.Length > MaxDocumentBytes)
            throw new InvalidOperationException($"The {_subject} override history has reached the document size limit.");

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
        return (bytes, Fingerprint(bytes));
    }

    string RequirePath()
    {
        if (ConfigurationProblem is string problem)
            throw new InvalidOperationException(problem);
        RejectDirectoryLinks(_overlayRoot!);
        RejectDirectoryLinks(_dataRoot);
        return Path.Combine(_overlayRoot!, _fileName);
    }

    byte[]? ReadBytes(string path)
    {
        RejectReparsePoint(path);
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (file.Length is < 1 or > MaxDocumentBytes)
                throw new InvalidDataException($"Invalid NPC {_subject} override document size.");
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
                throw new InvalidDataException("NPC editor overrides require paths without symlinks or junctions.");
        }
        catch (FileNotFoundException)
        {
            // An override or lock file can be absent before the first save.
        }
    }

    internal static string Fingerprint(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
