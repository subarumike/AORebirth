namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    #endregion

    /// <summary>
    /// Hash-protected mutable ACG runtime state. This sidecar never contains captured payload bytes.
    /// </summary>
    internal sealed class MissionAcgRuntimeStateStore
    {
        internal const string DirectoryName = "acg-runtime";

        internal const string FileExtension = ".state";

        private const string Header = "AORebirth-MissionAcgRuntimeState";

        private readonly string directoryPath;

        internal MissionAcgRuntimeStateStore(string missionStateDirectory)
        {
            if (string.IsNullOrWhiteSpace(missionStateDirectory))
            {
                throw new ArgumentException(
                    "Mission state directory is required.",
                    "missionStateDirectory");
            }

            this.directoryPath = Path.Combine(missionStateDirectory, DirectoryName);
        }

        internal string DirectoryPath
        {
            get
            {
                return this.directoryPath;
            }
        }

        internal bool TryLoad(
            MissionAcgInstanceBinding binding,
            MissionAcgLayoutBundle bundle,
            out MissionAcgRuntimeState state,
            out bool exists,
            out string failure)
        {
            state = null;
            failure = string.Empty;
            string path = this.PathFor(binding.AcceptedQuestIdentity);
            exists = File.Exists(path);
            if (!exists)
            {
                return true;
            }

            if (!this.TryRead(path, out state, out failure))
            {
                return false;
            }

            if (!state.AcceptedQuestIdentity.Equals(binding.AcceptedQuestIdentity)
                || state.AllocatedLivePlayfield2 != binding.AllocatedLivePlayfield2
                || !state.BuildingIdentity.Equals(binding.AcgBuildingIdentity)
                || !state.BuildingIdentity.Equals(bundle.BuildingIdentity)
                || !string.Equals(
                    state.BundleId,
                    binding.SelectedBundleId,
                    StringComparison.Ordinal)
                || !string.Equals(state.BundleId, bundle.LayoutId, StringComparison.Ordinal)
                || !string.Equals(
                    state.BundlePayloadSha256,
                    binding.SelectedBundlePayloadSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    state.BundlePayloadSha256,
                    bundle.GeneratorPayloadSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                state = null;
                failure =
                    "Runtime state does not match its persisted binding and immutable bundle.";
                return false;
            }

            return true;
        }

        internal bool TryWrite(
            MissionAcgRuntimeState state,
            bool replace,
            out string failure)
        {
            failure = string.Empty;
            if (state == null)
            {
                failure = "Runtime state is required.";
                return false;
            }

            Directory.CreateDirectory(this.directoryPath);
            string target = this.PathFor(state.AcceptedQuestIdentity);
            if (!replace && File.Exists(target))
            {
                failure = "Runtime state already exists for accepted mission.";
                return false;
            }

            string temporary =
                target
                + "."
                + Guid.NewGuid().ToString("N")
                + ".tmp";
            try
            {
                SortedDictionary<string, string> values = BuildValues(state);
                string canonical = SerializeValues(values);
                byte[] bytes =
                    new UTF8Encoding(false).GetBytes(
                        Header
                        + "\r\n"
                        + canonical
                        + "RecordSha256="
                        + ComputeSha256(canonical)
                        + "\r\n");
                using (var stream = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                MissionAcgRuntimeState roundTrip;
                string readFailure;
                if (!this.TryRead(
                    temporary,
                    out roundTrip,
                    out readFailure)
                    || !string.Equals(
                        SerializeValues(BuildValues(state)),
                        SerializeValues(BuildValues(roundTrip)),
                        StringComparison.Ordinal))
                {
                    failure =
                        "Atomic runtime-state validation failed: "
                        + (readFailure ?? "round-trip mismatch");
                    return false;
                }

                if (replace)
                {
                    if (!File.Exists(target))
                    {
                        failure = "Runtime state does not exist for replacement.";
                        return false;
                    }

                    string backup = target + ".bak";
                    File.Replace(temporary, target, backup, true);
                    if (File.Exists(backup))
                    {
                        File.Delete(backup);
                    }
                }
                else
                {
                    File.Move(temporary, target);
                }

                return true;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        internal bool TryDelete(
            MissionAcgIdentityRecord acceptedQuestIdentity,
            out string failure)
        {
            failure = string.Empty;
            try
            {
                string path = this.PathFor(acceptedQuestIdentity);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return true;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private bool TryRead(
            string path,
            out MissionAcgRuntimeState state,
            out string failure)
        {
            state = null;
            failure = string.Empty;
            try
            {
                string[] lines = File.ReadAllLines(path, new UTF8Encoding(false, true));
                if (lines.Length < 2
                    || !string.Equals(lines[0], Header, StringComparison.Ordinal))
                {
                    failure = "Missing runtime-state header or truncated sidecar.";
                    return false;
                }

                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 1; i < lines.Length; i++)
                {
                    int separator = lines[i].IndexOf('=');
                    if (separator <= 0)
                    {
                        failure = "Malformed runtime-state field at line " + (i + 1) + ".";
                        return false;
                    }

                    string key = lines[i].Substring(0, separator);
                    if (values.ContainsKey(key))
                    {
                        failure = "Duplicate runtime-state field " + key + ".";
                        return false;
                    }

                    values.Add(key, lines[i].Substring(separator + 1));
                }

                string suppliedHash = Require(values, "RecordSha256");
                values.Remove("RecordSha256");
                string canonical = SerializeValues(values);
                if (!string.Equals(
                    suppliedHash,
                    ComputeSha256(canonical),
                    StringComparison.OrdinalIgnoreCase))
                {
                    failure = "Runtime-state SHA-256 mismatch.";
                    return false;
                }

                int version = ParseInt(Require(values, "FormatVersion"), "FormatVersion");
                if (version != MissionAcgRuntimeState.CurrentFormatVersion)
                {
                    failure = "Unknown runtime-state format version " + version + ".";
                    return false;
                }

                MissionAcgIdentityRecord accepted = ParseIdentity(values, "AcceptedQuest");
                MissionAcgIdentityRecord building = ParseIdentity(values, "Building");
                string bundleId = Require(values, "BundleId");
                string payloadHash = Require(values, "BundlePayloadSha256");
                int livePlayfield =
                    ParseInt(
                        Require(values, "AllocatedLivePlayfield2"),
                        "AllocatedLivePlayfield2");
                DateTime updated =
                    ParseUtc(Require(values, "LastUpdatedUtc"), "LastUpdatedUtc");
                int identityCount =
                    ParseCount(Require(values, "IdentityCount"), "IdentityCount");
                int doorCount = ParseCount(Require(values, "DoorCount"), "DoorCount");
                int chestCount = ParseCount(Require(values, "ChestCount"), "ChestCount");

                int expectedFieldCount =
                    12
                    + identityCount
                    + doorCount
                    + chestCount;
                if (values.Count != expectedFieldCount)
                {
                    failure =
                        "Runtime-state field set is incomplete or contains unknown fields.";
                    return false;
                }

                var identities = new List<MissionAcgRuntimeIdentityEntry>();
                for (int i = 0; i < identityCount; i++)
                {
                    identities.Add(
                        ParseIdentityEntry(
                            Require(values, Indexed("Identity", i)),
                            i));
                }

                var doors = new List<MissionAcgRuntimeDoorState>();
                for (int i = 0; i < doorCount; i++)
                {
                    doors.Add(ParseDoor(Require(values, Indexed("Door", i)), i));
                }

                var chests = new List<MissionAcgRuntimeChestState>();
                for (int i = 0; i < chestCount; i++)
                {
                    chests.Add(ParseChest(Require(values, Indexed("Chest", i)), i));
                }

                state = new MissionAcgRuntimeState(
                    version,
                    accepted,
                    bundleId,
                    payloadHash,
                    building,
                    livePlayfield,
                    identities,
                    doors,
                    chests,
                    updated);
                return true;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static SortedDictionary<string, string> BuildValues(
            MissionAcgRuntimeState state)
        {
            var identities =
                new List<MissionAcgRuntimeIdentityEntry>(state.IdentityEntries);
            identities.Sort(
                delegate(
                    MissionAcgRuntimeIdentityEntry left,
                    MissionAcgRuntimeIdentityEntry right)
                {
                    int type =
                        left.CapturedIdentity.Type.CompareTo(right.CapturedIdentity.Type);
                    return type != 0
                               ? type
                               : left.CapturedIdentity.Instance.CompareTo(
                                   right.CapturedIdentity.Instance);
                });
            var doors = new List<MissionAcgRuntimeDoorState>(state.DoorStates);
            doors.Sort(
                delegate(
                    MissionAcgRuntimeDoorState left,
                    MissionAcgRuntimeDoorState right)
                {
                    return left.RuntimeInstance.CompareTo(right.RuntimeInstance);
                });
            var chests = new List<MissionAcgRuntimeChestState>(state.ChestStates);
            chests.Sort(
                delegate(
                    MissionAcgRuntimeChestState left,
                    MissionAcgRuntimeChestState right)
                {
                    return left.RuntimeInstance.CompareTo(right.RuntimeInstance);
                });

            var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                {
                    "AcceptedQuestInstance",
                    FormatInt(state.AcceptedQuestIdentity.Instance)
                },
                { "AcceptedQuestType", FormatInt(state.AcceptedQuestIdentity.Type) },
                { "AllocatedLivePlayfield2", FormatInt(state.AllocatedLivePlayfield2) },
                { "BuildingInstance", FormatInt(state.BuildingIdentity.Instance) },
                { "BuildingType", FormatInt(state.BuildingIdentity.Type) },
                { "BundleId", state.BundleId },
                { "BundlePayloadSha256", state.BundlePayloadSha256 },
                { "ChestCount", FormatInt(chests.Count) },
                { "DoorCount", FormatInt(doors.Count) },
                { "FormatVersion", FormatInt(state.FormatVersion) },
                { "IdentityCount", FormatInt(identities.Count) },
                { "LastUpdatedUtc", FormatUtc(state.LastUpdatedUtc) }
            };
            for (int i = 0; i < identities.Count; i++)
            {
                MissionAcgRuntimeIdentityEntry entry = identities[i];
                values.Add(
                    Indexed("Identity", i),
                    FormatInt((int)entry.Kind)
                    + ","
                    + FormatInt(entry.Slot)
                    + ","
                    + FormatInt(entry.CapturedIdentity.Type)
                    + ","
                    + FormatInt(entry.CapturedIdentity.Instance)
                    + ","
                    + FormatInt(entry.RuntimeIdentity.Type)
                    + ","
                    + FormatInt(entry.RuntimeIdentity.Instance));
            }

            for (int i = 0; i < doors.Count; i++)
            {
                values.Add(
                    Indexed("Door", i),
                    FormatInt(doors[i].RuntimeInstance)
                    + ","
                    + FormatBool(doors[i].IsOpen)
                    + ","
                    + FormatBool(doors[i].IsLocked));
            }

            for (int i = 0; i < chests.Count; i++)
            {
                values.Add(
                    Indexed("Chest", i),
                    FormatInt(chests[i].RuntimeInstance)
                    + ","
                    + FormatBool(chests[i].IsOpen));
            }

            return values;
        }

        private static MissionAcgRuntimeIdentityEntry ParseIdentityEntry(
            string value,
            int index)
        {
            string[] fields = value.Split(',');
            if (fields.Length != 6)
            {
                throw new FormatException(
                    "Invalid runtime identity entry " + index + ".");
            }

            MissionAcgRuntimeObjectKind kind =
                (MissionAcgRuntimeObjectKind)ParseInt(
                    fields[0],
                    "IdentityKind");
            if (!Enum.IsDefined(typeof(MissionAcgRuntimeObjectKind), kind))
            {
                throw new FormatException("Unknown runtime object kind.");
            }

            return new MissionAcgRuntimeIdentityEntry(
                new MissionAcgIdentityRecord(
                    ParseInt(fields[2], "CapturedType"),
                    ParseInt(fields[3], "CapturedInstance")),
                new MissionAcgIdentityRecord(
                    ParseInt(fields[4], "RuntimeType"),
                    ParseInt(fields[5], "RuntimeInstance")),
                kind,
                ParseInt(fields[1], "IdentitySlot"));
        }

        private static MissionAcgRuntimeDoorState ParseDoor(string value, int index)
        {
            string[] fields = value.Split(',');
            if (fields.Length != 3)
            {
                throw new FormatException("Invalid door state entry " + index + ".");
            }

            return new MissionAcgRuntimeDoorState(
                ParseInt(fields[0], "DoorRuntimeInstance"),
                ParseBool(fields[1], "DoorOpen"),
                ParseBool(fields[2], "DoorLocked"));
        }

        private static MissionAcgRuntimeChestState ParseChest(string value, int index)
        {
            string[] fields = value.Split(',');
            if (fields.Length != 2)
            {
                throw new FormatException("Invalid chest state entry " + index + ".");
            }

            return new MissionAcgRuntimeChestState(
                ParseInt(fields[0], "ChestRuntimeInstance"),
                ParseBool(fields[1], "ChestOpen"));
        }

        private string PathFor(MissionAcgIdentityRecord accepted)
        {
            return Path.Combine(
                this.directoryPath,
                accepted.Type.ToString("X8", CultureInfo.InvariantCulture)
                + "-"
                + accepted.Instance.ToString("X8", CultureInfo.InvariantCulture)
                + FileExtension);
        }

        private static string Indexed(string prefix, int index)
        {
            return prefix
                   + "."
                   + index.ToString("D3", CultureInfo.InvariantCulture);
        }

        private static MissionAcgIdentityRecord ParseIdentity(
            IDictionary<string, string> values,
            string prefix)
        {
            return new MissionAcgIdentityRecord(
                ParseInt(Require(values, prefix + "Type"), prefix + "Type"),
                ParseInt(Require(values, prefix + "Instance"), prefix + "Instance"));
        }

        private static string Require(IDictionary<string, string> values, string key)
        {
            string value;
            if (!values.TryGetValue(key, out value))
            {
                throw new FormatException("Missing runtime-state field " + key + ".");
            }

            return value;
        }

        private static int ParseCount(string value, string field)
        {
            int parsed = ParseInt(value, field);
            if (parsed < 0 || parsed > 255)
            {
                throw new FormatException("Invalid runtime-state count " + field + ".");
            }

            return parsed;
        }

        private static int ParseInt(string value, string field)
        {
            int parsed;
            if (!int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed))
            {
                throw new FormatException("Invalid integer field " + field + ".");
            }

            return parsed;
        }

        private static bool ParseBool(string value, string field)
        {
            bool parsed;
            if (!bool.TryParse(value, out parsed))
            {
                throw new FormatException("Invalid boolean field " + field + ".");
            }

            return parsed;
        }

        private static DateTime ParseUtc(string value, string field)
        {
            DateTime parsed;
            if (!DateTime.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out parsed)
                || parsed.Kind != DateTimeKind.Utc)
            {
                throw new FormatException("Invalid UTC timestamp field " + field + ".");
            }

            return parsed;
        }

        private static string FormatInt(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatBool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        private static string SerializeValues(IDictionary<string, string> values)
        {
            var keys = new List<string>(values.Keys);
            keys.Sort(StringComparer.Ordinal);
            var builder = new StringBuilder();
            for (int i = 0; i < keys.Count; i++)
            {
                builder.Append(keys[i]);
                builder.Append('=');
                builder.Append(values[keys[i]]);
                builder.Append("\r\n");
            }

            return builder.ToString();
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return MissionAcgHash.ToHex(
                    sha.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
            }
        }
    }
}
