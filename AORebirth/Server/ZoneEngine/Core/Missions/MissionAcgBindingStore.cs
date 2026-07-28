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

    internal sealed class MissionAcgBindingLoadResult
    {
        internal MissionAcgBindingLoadResult(
            IEnumerable<MissionAcgBindingRecord> records,
            IEnumerable<string> diagnostics)
        {
            this.Records = new List<MissionAcgBindingRecord>(records).AsReadOnly();
            this.Diagnostics = new List<string>(diagnostics).AsReadOnly();
        }

        internal IList<MissionAcgBindingRecord> Records { get; private set; }

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
    /// Versioned, hash-protected sidecars for generated terminal-mission ACG bindings.
    /// Records are replaced atomically and never merged with authored-quest persistence.
    /// </summary>
    internal sealed class MissionAcgBindingStore
    {
        internal const string DirectoryName = "acg-bindings";

        internal const string FileExtension = ".acg";

        private const string Header = "AORebirth-MissionAcgBinding";

        private const int ExpectedFieldCount = 35;

        private readonly string directoryPath;

        private readonly MissionAcgLayoutCatalog catalog;

        internal MissionAcgBindingStore(string missionStateDirectory, MissionAcgLayoutCatalog catalog)
        {
            if (string.IsNullOrWhiteSpace(missionStateDirectory))
            {
                throw new ArgumentException(
                    "Mission state directory is required.",
                    "missionStateDirectory");
            }

            if (catalog == null)
            {
                throw new ArgumentNullException("catalog");
            }

            this.directoryPath = Path.Combine(missionStateDirectory, DirectoryName);
            this.catalog = catalog;
        }

        internal string DirectoryPath
        {
            get
            {
                return this.directoryPath;
            }
        }

        internal MissionAcgBindingLoadResult LoadAll()
        {
            var records = new List<MissionAcgBindingRecord>();
            var diagnostics = new List<string>();
            if (!Directory.Exists(this.directoryPath))
            {
                return new MissionAcgBindingLoadResult(records, diagnostics);
            }

            string[] paths = Directory.GetFiles(
                this.directoryPath,
                "*" + FileExtension,
                SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            var accepted = new HashSet<string>(StringComparer.Ordinal);
            var activePlayfields = new Dictionary<int, string>();
            for (int i = 0; i < paths.Length; i++)
            {
                MissionAcgBindingRecord record;
                string failure;
                if (!this.TryRead(paths[i], out record, out failure))
                {
                    diagnostics.Add(paths[i] + ": " + failure);
                    continue;
                }

                string acceptedKey = IdentityKey(record.Binding.AcceptedQuestIdentity);
                if (!accepted.Add(acceptedKey))
                {
                    diagnostics.Add(
                        paths[i]
                        + ": duplicate accepted quest id "
                        + acceptedKey
                        + ".");
                    continue;
                }

                if (record.State.ReservesPlayfield)
                {
                    string existingPath;
                    if (activePlayfields.TryGetValue(
                        record.Binding.AllocatedLivePlayfield2,
                        out existingPath))
                    {
                        diagnostics.Add(
                            paths[i]
                            + ": duplicate active PF2 "
                            + record.Binding.AllocatedLivePlayfield2
                            + " also owned by "
                            + existingPath
                            + ".");
                        continue;
                    }

                    activePlayfields.Add(record.Binding.AllocatedLivePlayfield2, paths[i]);
                }

                records.Add(record);
            }

            return new MissionAcgBindingLoadResult(records, diagnostics);
        }

        internal bool TryCreate(
            MissionAcgBindingRecord record,
            out MissionAcgBindingRecord persisted,
            out string failure)
        {
            persisted = null;
            failure = string.Empty;
            if (record == null)
            {
                failure = "Binding record is required.";
                return false;
            }

            Directory.CreateDirectory(this.directoryPath);
            string path = this.PathFor(record.Binding.AcceptedQuestIdentity);
            if (File.Exists(path))
            {
                failure =
                    "Duplicate accepted quest id "
                    + IdentityKey(record.Binding.AcceptedQuestIdentity)
                    + ".";
                return false;
            }

            var withPath =
                new MissionAcgBindingRecord(record.Binding, record.State, path);
            if (!this.TryWriteAtomic(withPath, false, out failure))
            {
                return false;
            }

            persisted = withPath;
            return true;
        }

        internal bool TryReplace(
            MissionAcgBindingRecord record,
            out MissionAcgBindingRecord persisted,
            out string failure)
        {
            persisted = null;
            failure = string.Empty;
            if (record == null)
            {
                failure = "Binding record is required.";
                return false;
            }

            string path = string.IsNullOrWhiteSpace(record.RecordPath)
                              ? this.PathFor(record.Binding.AcceptedQuestIdentity)
                              : record.RecordPath;
            if (!File.Exists(path))
            {
                failure = "Binding sidecar does not exist: " + path;
                return false;
            }

            var withPath =
                new MissionAcgBindingRecord(record.Binding, record.State, path);
            if (!this.TryWriteAtomic(withPath, true, out failure))
            {
                return false;
            }

            persisted = withPath;
            return true;
        }

        private bool TryRead(
            string path,
            out MissionAcgBindingRecord record,
            out string failure)
        {
            record = null;
            failure = string.Empty;
            try
            {
                string[] lines = File.ReadAllLines(path, new UTF8Encoding(false, true));
                if (lines.Length < 2 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
                {
                    failure = "Missing binding header or truncated sidecar.";
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
                        "Binding field set is incomplete or contains unknown fields.";
                    return false;
                }

                string canonical = SerializeValues(values);
                string computedHash = ComputeSha256(canonical);
                if (!string.Equals(suppliedHash, computedHash, StringComparison.OrdinalIgnoreCase))
                {
                    failure = "Record SHA-256 mismatch.";
                    return false;
                }

                int version = ParseInt(Require(values, "FormatVersion"), "FormatVersion");
                if (version != MissionAcgInstanceBinding.CurrentFormatVersion)
                {
                    failure = "Unknown binding format version " + version + ".";
                    return false;
                }

                MissionAcgIdentityRecord accepted =
                    ParseIdentity(values, "AcceptedQuest");
                MissionAcgIdentityRecord offer = ParseIdentity(values, "OriginalOffer");
                MissionAcgIdentityRecord owner = ParseIdentity(values, "Owner");
                bool explicitNoTeam = ParseBool(Require(values, "ExplicitNoTeam"), "ExplicitNoTeam");
                MissionAcgIdentityRecord persistedTeam =
                    ParseIdentityAllowZero(values, "Team");
                if (explicitNoTeam
                    && (persistedTeam.Type != 0 || persistedTeam.Instance != 0))
                {
                    failure = "Explicit no-team record contains a team identity.";
                    return false;
                }

                MissionAcgIdentityRecord team =
                    explicitNoTeam ? null : persistedTeam;
                MissionRollType missionType =
                    (MissionRollType)ParseInt(Require(values, "MissionType"), "MissionType");
                int missionQuality =
                    ParseInt(Require(values, "MissionQuality"), "MissionQuality");
                int seed = ParseInt(Require(values, "MissionSeed"), "MissionSeed");
                MissionAcgIdentityRecord missionKey =
                    ParseIdentity(values, "MissionKey");
                MissionAcgIdentityRecord exterior =
                    ParseIdentity(values, "ExteriorEntrance");
                int entranceLow =
                    ParseInt(Require(values, "ExteriorEntranceLow"), "ExteriorEntranceLow");
                int entranceHigh =
                    ParseInt(Require(values, "ExteriorEntranceHigh"), "ExteriorEntranceHigh");
                float exteriorX = ParseFloat(Require(values, "ExteriorX"), "ExteriorX");
                float exteriorY = ParseFloat(Require(values, "ExteriorY"), "ExteriorY");
                float exteriorZ = ParseFloat(Require(values, "ExteriorZ"), "ExteriorZ");
                MissionAcgIdentityRecord terminal =
                    ParseIdentity(values, "IssuingTerminal");
                string bundleId = Require(values, "SelectedBundleId");
                string payloadHash = Require(values, "SelectedBundlePayloadSha256");
                MissionAcgIdentityRecord building = ParseIdentity(values, "AcgBuilding");
                int livePlayfield =
                    ParseInt(Require(values, "AllocatedLivePlayfield2"), "AllocatedLivePlayfield2");
                DateTime acceptedUtc =
                    ParseUtc(Require(values, "AcceptedUtc"), "AcceptedUtc");
                DateTime expiryUtc = ParseUtc(Require(values, "ExpiryUtc"), "ExpiryUtc");
                MissionAcgLifecycleState lifecycle =
                    (MissionAcgLifecycleState)ParseInt(
                        Require(values, "LifecycleState"),
                        "LifecycleState");
                MissionAcgCleanupState cleanup =
                    (MissionAcgCleanupState)ParseInt(
                        Require(values, "CleanupState"),
                        "CleanupState");
                DateTime updated =
                    ParseUtc(Require(values, "LastUpdatedUtc"), "LastUpdatedUtc");
                DateTime? cleanupStarted = ParseOptionalUtc(
                    Require(values, "CleanupStartedUtc"),
                    "CleanupStartedUtc");

                MissionAcgLayoutBundle bundle = this.catalog.FindByLayoutId(bundleId);
                if (bundle == null)
                {
                    failure =
                        "Accepted quest "
                        + IdentityKey(accepted)
                        + " references missing bundle "
                        + bundleId
                        + ".";
                    return false;
                }

                if (!bundle.BuildingIdentity.Equals(building))
                {
                    failure =
                        "Accepted quest "
                        + IdentityKey(accepted)
                        + " has a building identity mismatch.";
                    return false;
                }

                if (!string.Equals(
                    bundle.GeneratorPayloadSha256,
                    payloadHash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    failure =
                        "Accepted quest "
                        + IdentityKey(accepted)
                        + " has a bundle payload hash mismatch.";
                    return false;
                }

                var binding = new MissionAcgInstanceBinding(
                    version,
                    accepted,
                    offer,
                    owner,
                    team,
                    missionType,
                    missionQuality,
                    seed,
                    missionKey,
                    exterior,
                    entranceLow,
                    entranceHigh,
                    exteriorX,
                    exteriorY,
                    exteriorZ,
                    terminal,
                    bundleId,
                    payloadHash,
                    building,
                    livePlayfield,
                    acceptedUtc,
                    expiryUtc,
                    explicitNoTeam);
                var state =
                    new MissionAcgInstanceState(lifecycle, cleanup, updated, cleanupStarted);
                record = new MissionAcgBindingRecord(binding, state, path);
                return true;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private bool TryWriteAtomic(
            MissionAcgBindingRecord record,
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
                ValidateRecord(record);
                var values = BuildValues(record);
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

                MissionAcgBindingRecord roundTrip;
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

        private static SortedDictionary<string, string> BuildValues(
            MissionAcgBindingRecord record)
        {
            MissionAcgInstanceBinding binding = record.Binding;
            MissionAcgInstanceState state = record.State;
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                { "AcceptedQuestInstance", FormatInt(binding.AcceptedQuestIdentity.Instance) },
                { "AcceptedQuestType", FormatInt(binding.AcceptedQuestIdentity.Type) },
                { "AcceptedUtc", FormatUtc(binding.AcceptedUtc) },
                { "AcgBuildingInstance", FormatInt(binding.AcgBuildingIdentity.Instance) },
                { "AcgBuildingType", FormatInt(binding.AcgBuildingIdentity.Type) },
                { "AllocatedLivePlayfield2", FormatInt(binding.AllocatedLivePlayfield2) },
                {
                    "CleanupStartedUtc",
                    binding == null || !state.CleanupStartedUtc.HasValue
                        ? string.Empty
                        : FormatUtc(state.CleanupStartedUtc.Value)
                },
                { "CleanupState", FormatInt((int)state.CleanupState) },
                { "ExplicitNoTeam", binding.ExplicitNoTeam ? "true" : "false" },
                { "ExpiryUtc", FormatUtc(binding.ExpiryUtc) },
                { "ExteriorEntranceHigh", FormatInt(binding.ExteriorEntranceHigh) },
                { "ExteriorEntranceInstance", FormatInt(binding.ExteriorEntranceIdentity.Instance) },
                { "ExteriorEntranceLow", FormatInt(binding.ExteriorEntranceLow) },
                { "ExteriorEntranceType", FormatInt(binding.ExteriorEntranceIdentity.Type) },
                { "ExteriorX", binding.ExteriorX.ToString("R", CultureInfo.InvariantCulture) },
                { "ExteriorY", binding.ExteriorY.ToString("R", CultureInfo.InvariantCulture) },
                { "ExteriorZ", binding.ExteriorZ.ToString("R", CultureInfo.InvariantCulture) },
                { "FormatVersion", FormatInt(binding.BindingFormatVersion) },
                { "IssuingTerminalInstance", FormatInt(binding.IssuingTerminalIdentity.Instance) },
                { "IssuingTerminalType", FormatInt(binding.IssuingTerminalIdentity.Type) },
                { "LastUpdatedUtc", FormatUtc(state.LastUpdatedUtc) },
                { "LifecycleState", FormatInt((int)state.LifecycleState) },
                { "MissionKeyInstance", FormatInt(binding.MissionKeyIdentity.Instance) },
                { "MissionKeyType", FormatInt(binding.MissionKeyIdentity.Type) },
                { "MissionQuality", FormatInt(binding.MissionQuality) },
                { "MissionSeed", FormatInt(binding.DeterministicSeed) },
                { "MissionType", FormatInt((int)binding.MissionType) },
                { "OriginalOfferInstance", FormatInt(binding.OriginalOfferIdentity.Instance) },
                { "OriginalOfferType", FormatInt(binding.OriginalOfferIdentity.Type) },
                { "OwnerInstance", FormatInt(binding.OwnerIdentity.Instance) },
                { "OwnerType", FormatInt(binding.OwnerIdentity.Type) },
                { "SelectedBundleId", binding.SelectedBundleId },
                { "SelectedBundlePayloadSha256", binding.SelectedBundlePayloadSha256 },
                {
                    "TeamInstance",
                    binding.TeamIdentity == null ? "0" : FormatInt(binding.TeamIdentity.Instance)
                },
                {
                    "TeamType",
                    binding.TeamIdentity == null ? "0" : FormatInt(binding.TeamIdentity.Type)
                }
            };
            return values;
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

        private static void ValidateRecord(MissionAcgBindingRecord record)
        {
            if (record.Binding.BindingFormatVersion
                != MissionAcgInstanceBinding.CurrentFormatVersion)
            {
                throw new InvalidOperationException("Cannot persist an unknown binding version.");
            }
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

        private static string IdentityKey(MissionAcgIdentityRecord identity)
        {
            return identity.Type.ToString(CultureInfo.InvariantCulture)
                   + ":"
                   + identity.Instance.ToString(CultureInfo.InvariantCulture);
        }

        private static MissionAcgIdentityRecord ParseIdentity(
            IDictionary<string, string> values,
            string prefix)
        {
            return new MissionAcgIdentityRecord(
                ParseInt(Require(values, prefix + "Type"), prefix + "Type"),
                ParseInt(Require(values, prefix + "Instance"), prefix + "Instance"));
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

        private static DateTime? ParseOptionalUtc(string value, string field)
        {
            return string.IsNullOrEmpty(value)
                       ? (DateTime?)null
                       : ParseUtc(value, field);
        }

        private static string FormatInt(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
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
