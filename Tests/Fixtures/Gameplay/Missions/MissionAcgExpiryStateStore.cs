namespace ZoneEngine.Core.Missions
{
    #region Usings

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    #endregion

    internal sealed class MissionAcgExpiryLoadResult
    {
        internal MissionAcgExpiryLoadResult(
            IEnumerable<MissionAcgExpiryRecord> records,
            IEnumerable<string> diagnostics)
        {
            this.Records = new List<MissionAcgExpiryRecord>(records).AsReadOnly();
            this.Diagnostics = new List<string>(diagnostics).AsReadOnly();
        }

        internal IList<MissionAcgExpiryRecord> Records { get; private set; }

        internal IList<string> Diagnostics { get; private set; }

        internal bool IsValid
        {
            get
            {
                return this.Diagnostics.Count == 0;
            }
        }
    }

    /// <summary>
    /// Versioned, integrity-checked, atomic persistence for generated-mission expiry cleanup.
    /// </summary>
    internal sealed class MissionAcgExpiryStateStore
    {
        internal const string DirectoryName = "acg-expiry";

        internal const string FileExtension = ".expiry";

        private const string Header = "AORebirth-MissionAcgExpiryState";

        private const int ExpectedFieldCount = 38;

        private readonly string directoryPath;

        internal MissionAcgExpiryStateStore(string missionStateDirectory)
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

        internal string PathFor(MissionAcgIdentityRecord acceptedQuestIdentity)
        {
            if (acceptedQuestIdentity == null
                || acceptedQuestIdentity.Type == 0
                || acceptedQuestIdentity.Instance == 0)
            {
                throw new ArgumentException(
                    "Accepted quest identity is required.",
                    "acceptedQuestIdentity");
            }

            return Path.Combine(
                this.directoryPath,
                acceptedQuestIdentity.Type.ToString("X8", CultureInfo.InvariantCulture)
                + "-"
                + acceptedQuestIdentity.Instance.ToString("X8", CultureInfo.InvariantCulture)
                + FileExtension);
        }

        internal MissionAcgExpiryLoadResult LoadAll()
        {
            var records = new List<MissionAcgExpiryRecord>();
            var diagnostics = new List<string>();
            if (!Directory.Exists(this.directoryPath))
            {
                return new MissionAcgExpiryLoadResult(records, diagnostics);
            }

            string[] paths = Directory.GetFiles(
                this.directoryPath,
                "*" + FileExtension,
                SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            var accepted = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < paths.Length; i++)
            {
                MissionAcgExpiryRecord record;
                string failure;
                if (!this.TryRead(paths[i], out record, out failure))
                {
                    diagnostics.Add(paths[i] + ": " + failure);
                    continue;
                }

                string acceptedKey = IdentityKey(record.State.AcceptedQuestIdentity);
                if (!accepted.Add(acceptedKey))
                {
                    diagnostics.Add(
                        paths[i]
                        + ": duplicate accepted quest id "
                        + acceptedKey
                        + ".");
                    continue;
                }

                records.Add(record);
            }

            return new MissionAcgExpiryLoadResult(records, diagnostics);
        }

        internal MissionAcgExpiryLoadResult LoadAll(
            IEnumerable<MissionAcgBindingRecord> bindingRecords)
        {
            MissionAcgExpiryLoadResult loaded = this.LoadAll();
            var records = new List<MissionAcgExpiryRecord>(loaded.Records);
            var diagnostics = new List<string>(loaded.Diagnostics);
            var bindings =
                new Dictionary<string, MissionAcgInstanceBinding>(StringComparer.Ordinal);
            if (bindingRecords == null)
            {
                diagnostics.Add("Binding records are required to validate expiry state.");
                return new MissionAcgExpiryLoadResult(records, diagnostics);
            }

            foreach (MissionAcgBindingRecord bindingRecord in bindingRecords)
            {
                if (bindingRecord == null || bindingRecord.Binding == null)
                {
                    diagnostics.Add("Null binding record cannot validate expiry state.");
                    continue;
                }

                string key = IdentityKey(bindingRecord.Binding.AcceptedQuestIdentity);
                if (bindings.ContainsKey(key))
                {
                    diagnostics.Add(
                        "Duplicate accepted quest id "
                        + key
                        + " in binding set.");
                    continue;
                }

                bindings.Add(key, bindingRecord.Binding);
            }

            for (int i = 0; i < records.Count; i++)
            {
                MissionAcgExpiryRecord record = records[i];
                string key = IdentityKey(record.State.AcceptedQuestIdentity);
                MissionAcgInstanceBinding binding;
                if (!bindings.TryGetValue(key, out binding))
                {
                    diagnostics.Add(
                        record.RecordPath
                        + ": orphan expiry state for accepted quest "
                        + key
                        + ".");
                    continue;
                }

                string failure;
                if (!record.State.MatchesBinding(binding, out failure))
                {
                    diagnostics.Add(record.RecordPath + ": " + failure);
                }
            }

            return new MissionAcgExpiryLoadResult(records, diagnostics);
        }

        internal bool TryLoad(
            MissionAcgIdentityRecord acceptedQuestIdentity,
            out MissionAcgExpiryRecord record,
            out string failure)
        {
            record = null;
            failure = string.Empty;
            string path;
            try
            {
                path = this.PathFor(acceptedQuestIdentity);
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }

            if (!File.Exists(path))
            {
                failure = "Expiry sidecar does not exist: " + path;
                return false;
            }

            return this.TryRead(path, out record, out failure);
        }

        internal bool TryCreate(
            MissionAcgExpiryState state,
            out MissionAcgExpiryRecord persisted,
            out string failure)
        {
            return this.TryCreate(
                state == null ? null : new MissionAcgExpiryRecord(state, string.Empty),
                out persisted,
                out failure);
        }

        internal bool TryCreate(
            MissionAcgExpiryRecord record,
            out MissionAcgExpiryRecord persisted,
            out string failure)
        {
            persisted = null;
            failure = string.Empty;
            if (record == null)
            {
                failure = "Expiry state is required.";
                return false;
            }

            Directory.CreateDirectory(this.directoryPath);
            string path = this.PathFor(record.State.AcceptedQuestIdentity);
            if (File.Exists(path))
            {
                failure =
                    "Duplicate accepted quest id "
                    + IdentityKey(record.State.AcceptedQuestIdentity)
                    + ".";
                return false;
            }

            var withPath = new MissionAcgExpiryRecord(record.State, path);
            if (!this.TryWriteAtomic(withPath, false, out failure))
            {
                return false;
            }

            persisted = withPath;
            return true;
        }

        internal bool TryReplace(
            MissionAcgExpiryRecord record,
            out MissionAcgExpiryRecord persisted,
            out string failure)
        {
            persisted = null;
            failure = string.Empty;
            if (record == null)
            {
                failure = "Expiry state is required.";
                return false;
            }

            string path = string.IsNullOrWhiteSpace(record.RecordPath)
                              ? this.PathFor(record.State.AcceptedQuestIdentity)
                              : record.RecordPath;
            if (!File.Exists(path))
            {
                failure = "Expiry sidecar does not exist: " + path;
                return false;
            }

            var current = new MissionAcgExpiryRecord(record.State, path);
            MissionAcgExpiryRecord existing;
            string readFailure;
            if (!this.TryRead(path, out existing, out readFailure))
            {
                failure = "Existing expiry sidecar is invalid: " + readFailure;
                return false;
            }

            if (!CanReplace(existing.State, current.State, out failure))
            {
                return false;
            }

            if (!this.TryWriteAtomic(current, true, out failure))
            {
                return false;
            }

            persisted = current;
            return true;
        }

        private bool TryRead(
            string path,
            out MissionAcgExpiryRecord record,
            out string failure)
        {
            record = null;
            failure = string.Empty;
            try
            {
                string[] lines = File.ReadAllLines(path, new UTF8Encoding(false, true));
                if (lines.Length < 2
                    || !string.Equals(lines[0], Header, StringComparison.Ordinal))
                {
                    failure = "Missing expiry header or truncated sidecar.";
                    return false;
                }

                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 1; i < lines.Length; i++)
                {
                    int separator = lines[i].IndexOf('=');
                    if (separator <= 0)
                    {
                        failure = "Malformed field at line " + (i + 1) + ".";
                        return false;
                    }

                    string key = lines[i].Substring(0, separator);
                    string value = lines[i].Substring(separator + 1);
                    if (values.ContainsKey(key))
                    {
                        failure = "Duplicate field " + key + ".";
                        return false;
                    }

                    values.Add(key, value);
                }

                string suppliedHash = Require(values, "RecordSha256");
                values.Remove("RecordSha256");
                if (values.Count != ExpectedFieldCount)
                {
                    failure =
                        "Expiry field set is incomplete or contains unknown fields.";
                    return false;
                }

                string canonical = SerializeValues(values);
                string computedHash = ComputeSha256(canonical);
                if (!string.Equals(
                    suppliedHash,
                    computedHash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    failure = "Record SHA-256 mismatch.";
                    return false;
                }

                int version = ParseInt(Require(values, "FormatVersion"), "FormatVersion");
                if (version != MissionAcgExpiryState.CurrentFormatVersion)
                {
                    failure = "Unknown expiry format version " + version + ".";
                    return false;
                }

                MissionAcgIdentityRecord accepted = ParseIdentity(values, "AcceptedQuest");
                MissionAcgIdentityRecord originalOffer = ParseIdentity(values, "OriginalOffer");
                MissionAcgIdentityRecord owner = ParseIdentity(values, "Owner");
                bool explicitNoTeam =
                    ParseBool(Require(values, "ExplicitNoTeam"), "ExplicitNoTeam");
                MissionAcgIdentityRecord persistedTeam =
                    ParseIdentityAllowZero(values, "Team");
                if (explicitNoTeam
                    && (persistedTeam.Type != 0 || persistedTeam.Instance != 0))
                {
                    failure = "Explicit no-team expiry state contains a team identity.";
                    return false;
                }

                MissionAcgIdentityRecord team = explicitNoTeam ? null : persistedTeam;
                MissionRollType missionType =
                    (MissionRollType)ParseInt(Require(values, "MissionType"), "MissionType");
                int missionQuality =
                    ParseInt(Require(values, "MissionQuality"), "MissionQuality");
                int missionSeed = ParseInt(Require(values, "MissionSeed"), "MissionSeed");
                MissionAcgIdentityRecord missionKey = ParseIdentity(values, "MissionKey");
                MissionAcgIdentityRecord exterior =
                    ParseIdentity(values, "ExteriorEntrance");
                int exteriorLow =
                    ParseInt(Require(values, "ExteriorEntranceLow"), "ExteriorEntranceLow");
                int exteriorHigh =
                    ParseInt(Require(values, "ExteriorEntranceHigh"), "ExteriorEntranceHigh");
                float exteriorX = ParseFloat(Require(values, "ExteriorX"), "ExteriorX");
                float exteriorY = ParseFloat(Require(values, "ExteriorY"), "ExteriorY");
                float exteriorZ = ParseFloat(Require(values, "ExteriorZ"), "ExteriorZ");
                MissionAcgIdentityRecord terminal =
                    ParseIdentity(values, "IssuingTerminal");
                string bundleId = Require(values, "SelectedBundleId");
                string payloadSha = Require(values, "SelectedBundlePayloadSha256");
                MissionAcgIdentityRecord building = ParseIdentity(values, "AcgBuilding");
                int livePlayfield = ParseInt(
                    Require(values, "AllocatedLivePlayfield2"),
                    "AllocatedLivePlayfield2");
                DateTime acceptedUtc = ParseUtc(Require(values, "AcceptedUtc"), "AcceptedUtc");
                DateTime expiryUtc = ParseUtc(Require(values, "ExpiryUtc"), "ExpiryUtc");
                DateTime firstDetected =
                    ParseUtc(Require(values, "FirstDetectedUtc"), "FirstDetectedUtc");
                DateTime updatedUtc =
                    ParseUtc(Require(values, "UpdatedUtc"), "UpdatedUtc");
                MissionAcgExpiryCheckpoint checkpoints =
                    (MissionAcgExpiryCheckpoint)ParseLong(
                        Require(values, "Checkpoints"),
                        "Checkpoints");
                MissionAcgExpiryStatus status =
                    (MissionAcgExpiryStatus)ParseInt(
                        Require(values, "Status"),
                        "Status");
                bool requiresOwnerReconciliation =
                    ParseBool(
                        Require(values, "RequiresOwnerReconciliation"),
                        "RequiresOwnerReconciliation");
                int retryCount = ParseInt(Require(values, "RetryCount"), "RetryCount");
                string lastFailure =
                    DecodeUtf8Base64(
                        Require(values, "LastFailureUtf8Base64"),
                        "LastFailureUtf8Base64");

                var state = new MissionAcgExpiryState(
                    version,
                    accepted,
                    originalOffer,
                    owner,
                    team,
                    explicitNoTeam,
                    missionType,
                    missionQuality,
                    missionSeed,
                    missionKey,
                    exterior,
                    exteriorLow,
                    exteriorHigh,
                    exteriorX,
                    exteriorY,
                    exteriorZ,
                    terminal,
                    bundleId,
                    payloadSha,
                    building,
                    livePlayfield,
                    acceptedUtc,
                    expiryUtc,
                    firstDetected,
                    updatedUtc,
                    checkpoints,
                    status,
                    requiresOwnerReconciliation,
                    retryCount,
                    lastFailure);
                record = new MissionAcgExpiryRecord(state, path);
                return true;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private bool TryWriteAtomic(
            MissionAcgExpiryRecord record,
            bool replace,
            out string failure)
        {
            failure = string.Empty;
            string target = record.RecordPath;
            string temporary =
                target
                + "."
                + Guid.NewGuid().ToString("N")
                + ".tmp";
            try
            {
                var values = BuildValues(record.State);
                string canonical = SerializeValues(values);
                string complete =
                    Header
                    + "\r\n"
                    + canonical
                    + "RecordSha256="
                    + ComputeSha256(canonical)
                    + "\r\n";
                byte[] bytes = new UTF8Encoding(false).GetBytes(complete);
                using (var stream = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                MissionAcgExpiryRecord roundTrip;
                string readFailure;
                if (!this.TryRead(temporary, out roundTrip, out readFailure))
                {
                    failure = "Atomic write validation failed: " + readFailure;
                    return false;
                }

                if (replace)
                {
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

        private static bool CanReplace(
            MissionAcgExpiryState existing,
            MissionAcgExpiryState replacement,
            out string failure)
        {
            failure = string.Empty;
            string matchFailure;
            if (!replacement.MatchesBinding(ToBindingIdentitySnapshot(existing), out matchFailure))
            {
                failure = "Expiry identity fields cannot be replaced.";
                return false;
            }

            if ((replacement.Checkpoints & existing.Checkpoints) != existing.Checkpoints)
            {
                failure = "Expiry checkpoints cannot regress.";
                return false;
            }

            if ((int)replacement.Status < (int)existing.Status)
            {
                failure = "Expiry status cannot regress.";
                return false;
            }

            if (replacement.UpdatedUtc < existing.UpdatedUtc
                || replacement.FirstDetectedUtc != existing.FirstDetectedUtc
                || replacement.RequiresOwnerReconciliation
                   != existing.RequiresOwnerReconciliation
                || replacement.RetryCount < existing.RetryCount)
            {
                failure =
                    "Expiry timestamps, owner-reconciliation requirement, or retry count "
                    + "cannot be replaced or regressed.";
                return false;
            }

            if ((existing.Status == MissionAcgExpiryStatus.TerminalFailure
                 || existing.Status == MissionAcgExpiryStatus.Complete)
                && (replacement.Checkpoints != existing.Checkpoints
                    || replacement.Status != existing.Status))
            {
                failure = "Terminal expiry state cannot be replaced.";
                return false;
            }

            return true;
        }

        private static MissionAcgInstanceBinding ToBindingIdentitySnapshot(
            MissionAcgExpiryState state)
        {
            return new MissionAcgInstanceBinding(
                MissionAcgInstanceBinding.CurrentFormatVersion,
                state.AcceptedQuestIdentity,
                state.OriginalOfferIdentity,
                state.OwnerIdentity,
                state.TeamIdentity,
                state.MissionType,
                state.MissionQuality,
                state.DeterministicSeed,
                state.MissionKeyIdentity,
                state.ExteriorEntranceIdentity,
                state.ExteriorEntranceLow,
                state.ExteriorEntranceHigh,
                state.ExteriorX,
                state.ExteriorY,
                state.ExteriorZ,
                state.IssuingTerminalIdentity,
                state.SelectedBundleId,
                state.SelectedBundlePayloadSha256,
                state.AcgBuildingIdentity,
                state.AllocatedLivePlayfield2,
                state.AcceptedUtc,
                state.ExpiryUtc,
                state.ExplicitNoTeam);
        }

        private static SortedDictionary<string, string> BuildValues(
            MissionAcgExpiryState state)
        {
            return new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                { "AcceptedQuestInstance", FormatInt(state.AcceptedQuestIdentity.Instance) },
                { "AcceptedQuestType", FormatInt(state.AcceptedQuestIdentity.Type) },
                { "AcceptedUtc", FormatUtc(state.AcceptedUtc) },
                { "AcgBuildingInstance", FormatInt(state.AcgBuildingIdentity.Instance) },
                { "AcgBuildingType", FormatInt(state.AcgBuildingIdentity.Type) },
                { "AllocatedLivePlayfield2", FormatInt(state.AllocatedLivePlayfield2) },
                { "Checkpoints", FormatLong((long)state.Checkpoints) },
                { "ExplicitNoTeam", state.ExplicitNoTeam ? "true" : "false" },
                { "ExpiryUtc", FormatUtc(state.ExpiryUtc) },
                { "ExteriorEntranceHigh", FormatInt(state.ExteriorEntranceHigh) },
                { "ExteriorEntranceInstance", FormatInt(state.ExteriorEntranceIdentity.Instance) },
                { "ExteriorEntranceLow", FormatInt(state.ExteriorEntranceLow) },
                { "ExteriorEntranceType", FormatInt(state.ExteriorEntranceIdentity.Type) },
                { "ExteriorX", state.ExteriorX.ToString("R", CultureInfo.InvariantCulture) },
                { "ExteriorY", state.ExteriorY.ToString("R", CultureInfo.InvariantCulture) },
                { "ExteriorZ", state.ExteriorZ.ToString("R", CultureInfo.InvariantCulture) },
                { "FirstDetectedUtc", FormatUtc(state.FirstDetectedUtc) },
                { "FormatVersion", FormatInt(state.FormatVersion) },
                { "IssuingTerminalInstance", FormatInt(state.IssuingTerminalIdentity.Instance) },
                { "IssuingTerminalType", FormatInt(state.IssuingTerminalIdentity.Type) },
                {
                    "LastFailureUtf8Base64",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(state.LastFailure))
                },
                { "MissionKeyInstance", FormatInt(state.MissionKeyIdentity.Instance) },
                { "MissionKeyType", FormatInt(state.MissionKeyIdentity.Type) },
                { "MissionQuality", FormatInt(state.MissionQuality) },
                { "MissionSeed", FormatInt(state.DeterministicSeed) },
                { "MissionType", FormatInt((int)state.MissionType) },
                { "OriginalOfferInstance", FormatInt(state.OriginalOfferIdentity.Instance) },
                { "OriginalOfferType", FormatInt(state.OriginalOfferIdentity.Type) },
                { "OwnerInstance", FormatInt(state.OwnerIdentity.Instance) },
                { "OwnerType", FormatInt(state.OwnerIdentity.Type) },
                { "RetryCount", FormatInt(state.RetryCount) },
                {
                    "RequiresOwnerReconciliation",
                    state.RequiresOwnerReconciliation ? "true" : "false"
                },
                { "SelectedBundleId", state.SelectedBundleId },
                { "SelectedBundlePayloadSha256", state.SelectedBundlePayloadSha256 },
                { "Status", FormatInt((int)state.Status) },
                {
                    "TeamInstance",
                    state.TeamIdentity == null ? "0" : FormatInt(state.TeamIdentity.Instance)
                },
                {
                    "TeamType",
                    state.TeamIdentity == null ? "0" : FormatInt(state.TeamIdentity.Type)
                },
                { "UpdatedUtc", FormatUtc(state.UpdatedUtc) }
            };
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

        private static MissionAcgIdentityRecord ParseIdentity(
            IDictionary<string, string> values,
            string prefix)
        {
            MissionAcgIdentityRecord identity = ParseIdentityAllowZero(values, prefix);
            if (identity.Type == 0 || identity.Instance == 0)
            {
                throw new FormatException("Identity " + prefix + " is not concrete.");
            }

            return identity;
        }

        private static MissionAcgIdentityRecord ParseIdentityAllowZero(
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
                throw new FormatException("Missing field " + key + ".");
            }

            return value;
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

        private static long ParseLong(string value, string field)
        {
            long parsed;
            if (!long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed))
            {
                throw new FormatException("Invalid long field " + field + ".");
            }

            return parsed;
        }

        private static float ParseFloat(string value, string field)
        {
            float parsed;
            if (!float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out parsed)
                || float.IsNaN(parsed)
                || float.IsInfinity(parsed))
            {
                throw new FormatException("Invalid float field " + field + ".");
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

        private static string DecodeUtf8Base64(string value, string field)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(value);
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (Exception ex)
            {
                throw new FormatException(
                    "Invalid UTF-8/base64 field " + field + ".",
                    ex);
            }
        }

        private static string FormatInt(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatLong(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        private static string IdentityKey(MissionAcgIdentityRecord identity)
        {
            return identity.Type.ToString(CultureInfo.InvariantCulture)
                   + ":"
                   + identity.Instance.ToString(CultureInfo.InvariantCulture);
        }

        private static string ComputeSha256(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            using (SHA256 sha = SHA256.Create())
            {
                return MissionAcgHash.ToHex(sha.ComputeHash(bytes)).ToLowerInvariant();
            }
        }
    }
}
