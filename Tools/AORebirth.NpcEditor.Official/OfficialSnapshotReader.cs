using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Xml;

namespace AORebirth.NpcEditor.Official;

public sealed record OfficialSessionKey(int ProcessId, string SessionId);

public sealed record OfficialSessionChoice(OfficialSessionKey Key, string Label, string State, DateTimeOffset CapturedAtUtc);

public sealed record OfficialSelectionLock(
    OfficialSessionKey Session, string SelectionId, int PlayfieldId, int IdentityType, int InstanceId, long MinimumSequence)
{
    public static OfficialSelectionLock From(OfficialSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.State != "selected" || !OfficialSnapshotReader.IsValid(snapshot))
            throw new ArgumentException("A selected NPC observation is required.", nameof(snapshot));

        return new(new(snapshot.ProcessId, snapshot.SessionId), snapshot.SelectionId!, snapshot.PlayfieldId,
            snapshot.IdentityType, snapshot.InstanceId, snapshot.Sequence);
    }

    internal bool Matches(OfficialSnapshot snapshot)
        => snapshot.State == "selected" && snapshot.ProcessId == Session.ProcessId && snapshot.SessionId == Session.SessionId
            && snapshot.SelectionId == SelectionId && snapshot.PlayfieldId == PlayfieldId
            && snapshot.IdentityType == IdentityType && snapshot.InstanceId == InstanceId && snapshot.Sequence >= MinimumSequence;
}

public sealed record OfficialReadResult(
    OfficialSessionChoice[] Sessions, OfficialSessionKey? ActiveSession, OfficialSnapshot? Snapshot,
    string Status, string Message, bool LockInvalidated);

/// <summary>Reads the local AOSharp publication without retaining a stale target as active.</summary>
public sealed class OfficialSnapshotReader
{
    const int MaxSnapshots = 64;
    const int MaxRememberedSessions = MaxSnapshots * 2;
    const int MaxSnapshotBytes = 128 * 1024;
    const int MaxFields = 256;
    const int MaxEvents = 128;
    const string FilePrefix = "research-";
    const string FileSuffix = ".v2.json";

    static readonly TimeSpan Freshness = TimeSpan.FromSeconds(4);
    static readonly TimeSpan FutureTolerance = TimeSpan.FromSeconds(1);
    readonly string _directory;
    readonly TimeProvider _clock;
    readonly object _sync = new();
    readonly DataContractJsonSerializer _serializer = new(typeof(OfficialSnapshot), new DataContractJsonSerializerSettings
    {
        MaxItemsInObjectGraph = 4096
    });
    readonly Dictionary<OfficialSessionKey, Watermark> _watermarks = new();
    OfficialSelectionLock? _invalidatedLock;

    sealed record Watermark(long Sequence, string Fingerprint, long LastSeenTicks);

    public OfficialSnapshotReader() : this(OfficialLocalPaths.ResearchDirectory, TimeProvider.System) { }

    internal OfficialSnapshotReader(string directory, TimeProvider clock)
    {
        _directory = directory;
        _clock = clock;
    }

    public OfficialReadResult Read(OfficialSessionKey? session = null, OfficialSelectionLock? targetLock = null)
    {
        lock (_sync)
            return ReadCore(session, targetLock);
    }

    OfficialReadResult ReadCore(OfficialSessionKey? session, OfficialSelectionLock? targetLock)
    {
        if (_invalidatedLock != targetLock)
            _invalidatedLock = null;

        OfficialSessionKey? requested = targetLock?.Session ?? session;
        try
        {
            string directory = OfficialLocalPaths.RequireLocalDirectory(_directory);
            string[] paths = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, FilePrefix + "*" + FileSuffix).Take(MaxSnapshots + 1).ToArray()
                : [];
            if (paths.Length > MaxSnapshots)
                return Unavailable([], requested, "invalid", "Too many local research snapshots.", targetLock);

            var active = new Dictionary<OfficialSessionKey, OfficialSnapshot>();
            bool rejected = false;
            long now = _clock.GetUtcNow().UtcTicks;
            foreach (string path in paths)
            {
                if (!TryRead(path, now, out OfficialSessionKey? key, out OfficialSnapshot? snapshot))
                {
                    rejected = true;
                    continue;
                }
                active.Add(key!, snapshot!);
            }
            TrimWatermarks();

            OfficialSessionChoice[] choices = active.OrderBy(pair => pair.Key.ProcessId)
                .ThenBy(pair => pair.Key.SessionId, StringComparer.Ordinal).Select(pair =>
                    new OfficialSessionChoice(pair.Key, $"Client {pair.Key.ProcessId} ({pair.Key.SessionId[..8]})", pair.Value.State,
                        new DateTimeOffset(pair.Value.CapturedUtcTicks, TimeSpan.Zero))).ToArray();

            if (targetLock != null && (_invalidatedLock == targetLock || (session != null && session != targetLock.Session)
                || !active.TryGetValue(targetLock.Session, out OfficialSnapshot? locked) || !targetLock.Matches(locked)))
                return Unavailable(choices, requested, "lockedTargetUnavailable", "The locked NPC is no longer available. Unlock to follow a target.", targetLock);

            if (requested == null)
            {
                if (choices.Length > 1)
                    return Unavailable(choices, null, "chooseSession", "Multiple clients are reporting. Choose a client session.", null);
                if (choices.Length == 0)
                    return Unavailable(choices, null, rejected ? "invalid" : "noSnapshots",
                        rejected ? "No valid fresh research snapshot." : "No AOSharp research snapshot detected.", null);
                requested = choices[0].Key;
            }

            if (!active.TryGetValue(requested, out OfficialSnapshot? selected))
                return Unavailable(choices, requested, "sessionUnavailable", "The selected client session has no fresh valid snapshot.", targetLock);

            string message = selected.State switch
            {
                "selected" => "Selected NPC observed.",
                "zoning" => "The selected client is zoning.",
                "none" => "No NPC is selected in the selected client.",
                _ => "The selected client is temporarily unavailable."
            };
            return new(choices, requested, selected.State == "selected" ? selected : null, selected.State, message, false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Unavailable([], requested, "invalid", "Local research snapshots could not be read safely.", targetLock);
        }
    }

    OfficialReadResult Unavailable(OfficialSessionChoice[] choices, OfficialSessionKey? requested, string status,
        string message, OfficialSelectionLock? targetLock)
    {
        if (targetLock != null)
            _invalidatedLock = targetLock;
        return new(choices, requested, null, status, message, targetLock != null);
    }

    bool TryRead(string path, long now, out OfficialSessionKey? key, out OfficialSnapshot? snapshot)
    {
        snapshot = null;
        key = FileSession(Path.GetFileName(path));
        if (key == null)
            return false;

        try
        {
            OfficialLocalPaths.RejectLink(path);
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (file.Length is < 2 or > MaxSnapshotBytes)
                return false;
            byte[] bytes = new byte[(int)file.Length];
            file.ReadExactly(bytes);
            if (file.ReadByte() != -1)
                return false;
            OfficialLocalPaths.RejectLink(path);

            using var input = new MemoryStream(bytes, writable: false);
            if (_serializer.ReadObject(input) is not OfficialSnapshot parsed || !IsValid(parsed)
                || parsed.ProcessId != key.ProcessId || parsed.SessionId != key.SessionId
                || parsed.CapturedUtcTicks < now - Freshness.Ticks || parsed.CapturedUtcTicks > now + FutureTolerance.Ticks)
                return false;

            string fingerprint = Convert.ToHexString(SHA256.HashData(bytes));
            if (_watermarks.TryGetValue(key, out Watermark? old)
                && (parsed.Sequence < old.Sequence || (parsed.Sequence == old.Sequence && fingerprint != old.Fingerprint)))
                return false;
            _watermarks[key] = new(parsed.Sequence, fingerprint, now);
            snapshot = parsed;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SerializationException or XmlException)
        {
            return false;
        }
    }

    void TrimWatermarks()
    {
        int remove = _watermarks.Count - MaxRememberedSessions;
        if (remove <= 0)
            return;
        foreach (OfficialSessionKey key in _watermarks.OrderBy(pair => pair.Value.LastSeenTicks)
            .Take(remove).Select(pair => pair.Key).ToArray())
            _watermarks.Remove(key);
    }

    static OfficialSessionKey? FileSession(string name)
    {
        if (!name.StartsWith(FilePrefix, StringComparison.Ordinal) || !name.EndsWith(FileSuffix, StringComparison.Ordinal))
            return null;
        string[] parts = name[FilePrefix.Length..^FileSuffix.Length].Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int pid)
            || pid <= 0 || parts[0] != pid.ToString(CultureInfo.InvariantCulture) || !ValidGuid(parts[1]))
            return null;
        return new(pid, parts[1]);
    }

    internal static bool IsValid(OfficialSnapshot snapshot)
    {
        if (snapshot.Version != OfficialSnapshot.CurrentVersion || snapshot.ProcessId <= 0 || !ValidGuid(snapshot.SessionId)
            || !ValidTicks(snapshot.CapturedUtcTicks) || snapshot.Sequence <= 0 || snapshot.Fields == null
            || snapshot.Fields.Length > MaxFields || snapshot.Events == null || snapshot.Events.Length > MaxEvents)
            return false;

        if (snapshot.State != "selected")
            return snapshot.State is "none" or "zoning" or "unavailable" && snapshot.PlayfieldId == 0
                && snapshot.IdentityType == 0 && snapshot.InstanceId == 0 && snapshot.SelectionId == null
                && snapshot.Name == null && snapshot.Level == null && snapshot.MonsterData == null
                && snapshot.Fields.Length == 0 && snapshot.Events.Length == 0;

        return ValidGuid(snapshot.SelectionId) && snapshot.PlayfieldId > 0 && snapshot.IdentityType > 0 && snapshot.InstanceId != 0
            && ValidText(snapshot.Name, 256) && snapshot.MonsterData is not <= 0 && snapshot.Level is not (<= 0 or > ushort.MaxValue)
            && snapshot.Fields.All(field => field != null && RequiredText(field.Key, 160) && ValidText(field.Value, 2048)
                && ValidText(field.Unit, 40) && RequiredText(field.Source, 256) && ValidClassification(field.Classification)
                && ValidText(field.Confidence, 64))
            && snapshot.Events.All(row => row != null && ValidTicks(row.CapturedUtcTicks) && row.CapturedUtcTicks <= snapshot.CapturedUtcTicks
                && RequiredText(row.Kind, 120) && RequiredText(row.Detail, 2048) && RequiredText(row.Source, 256)
                && ValidClassification(row.Classification) && ValidText(row.Confidence, 64));
    }

    internal static bool ValidGuid(string? value)
        => Guid.TryParseExact(value, "N", out Guid id) && id != Guid.Empty && id.ToString("N") == value;

    internal static bool ValidTicks(long value) => value > 0 && value <= DateTime.MaxValue.Ticks;
    internal static bool ValidText(string? value, int maxLength) => value == null || (value.Length <= maxLength && !value.Contains('\0'));
    static bool RequiredText(string? value, int maxLength) => !string.IsNullOrWhiteSpace(value) && ValidText(value, maxLength);

    static bool ValidClassification(string value) => value is "CLIENT_EXPOSED" or "GAMEPLAY_OBSERVED" or "RDB_REFERENCE"
        or "DERIVED_FROM_OBSERVATIONS" or "UNVERIFIED" or "NOT_AVAILABLE";
}
