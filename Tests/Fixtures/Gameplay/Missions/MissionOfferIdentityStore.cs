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
    /// Result of loading the durable generated-mission offer identity cursor.
    /// </summary>
    internal sealed class MissionOfferIdentityLoadResult
    {
        internal MissionOfferIdentityLoadResult(
            bool isValid,
            bool stateExists,
            int lastAllocatedOfferId,
            string statePath,
            string diagnostic)
        {
            this.IsValid = isValid;
            this.StateExists = stateExists;
            this.LastAllocatedOfferId = lastAllocatedOfferId;
            this.StatePath = statePath;
            this.Diagnostic = diagnostic ?? string.Empty;
        }

        internal bool IsValid { get; private set; }

        internal bool StateExists { get; private set; }

        internal int LastAllocatedOfferId { get; private set; }

        internal string StatePath { get; private set; }

        internal string Diagnostic { get; private set; }
    }

    /// <summary>
    /// Result of reserving a new generated-mission offer identity.
    /// </summary>
    internal sealed class MissionOfferIdentityAllocationResult
    {
        internal MissionOfferIdentityAllocationResult(
            bool succeeded,
            int offerId,
            string statePath,
            string diagnostic)
        {
            this.Succeeded = succeeded;
            this.OfferId = offerId;
            this.StatePath = statePath;
            this.Diagnostic = diagnostic ?? string.Empty;
        }

        internal bool Succeeded { get; private set; }

        internal int OfferId { get; private set; }

        internal string StatePath { get; private set; }

        internal string Diagnostic { get; private set; }
    }

    /// <summary>
    /// Restart-safe, collision-checked identity allocation for generated mission offers.
    /// The durable cursor is published only after a complete hash-validated replacement.
    /// </summary>
    internal sealed class MissionOfferIdentityStore
    {
        internal const int CurrentFormatVersion = 1;

        internal const int MinimumOfferId = 0x55569000;

        internal const int MaximumOfferId = 0x55FFFFFF;

        internal const string DirectoryName = "offer-identities";

        internal const string StateFileName = "generated-offer-id.cursor";

        private const string Header = "AORebirth-MissionOfferIdentityCursor";

        private const int ExpectedFieldCount = 2;

        private static readonly object GateRegistryLock = new object();

        private static readonly Dictionary<string, object> PathGates =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private readonly string directoryPath;

        private readonly string statePath;

        private readonly object allocationGate;

        internal MissionOfferIdentityStore(string missionStateDirectory)
        {
            if (string.IsNullOrWhiteSpace(missionStateDirectory))
            {
                throw new ArgumentException(
                    "Mission state directory is required.",
                    "missionStateDirectory");
            }

            this.directoryPath = Path.GetFullPath(
                Path.Combine(missionStateDirectory, DirectoryName));
            this.statePath = Path.Combine(this.directoryPath, StateFileName);
            this.allocationGate = GetPathGate(this.statePath);
        }

        internal string DirectoryPath
        {
            get
            {
                return this.directoryPath;
            }
        }

        internal string StatePath
        {
            get
            {
                return this.statePath;
            }
        }

        internal MissionOfferIdentityLoadResult Load()
        {
            lock (this.allocationGate)
            {
                return this.LoadUnsafe();
            }
        }

        /// <summary>
        /// Allocates and durably publishes the next available identity. The predicate
        /// must return true when the proposed identity collides with another owner.
        /// </summary>
        internal MissionOfferIdentityAllocationResult TryAllocate(
            Func<int, bool> isOfferIdentityInUse)
        {
            if (isOfferIdentityInUse == null)
            {
                throw new ArgumentNullException("isOfferIdentityInUse");
            }

            lock (this.allocationGate)
            {
                MissionOfferIdentityLoadResult loaded = this.LoadUnsafe();
                if (!loaded.IsValid)
                {
                    return AllocationFailure(loaded.Diagnostic, this.statePath);
                }

                int candidate = loaded.LastAllocatedOfferId;
                while (candidate < MaximumOfferId)
                {
                    candidate++;

                    bool collides;
                    try
                    {
                        collides = isOfferIdentityInUse(candidate);
                    }
                    catch (Exception ex)
                    {
                        return AllocationFailure(
                            "Offer identity collision validation failed: " + ex.Message,
                            this.statePath);
                    }

                    if (collides)
                    {
                        continue;
                    }

                    string failure;
                    if (!this.TryWriteAtomic(candidate, out failure))
                    {
                        return AllocationFailure(failure, this.statePath);
                    }

                    return new MissionOfferIdentityAllocationResult(
                        true,
                        candidate,
                        this.statePath,
                        string.Empty);
                }

                return AllocationFailure(
                    "Generated mission offer identity range is exhausted.",
                    this.statePath);
            }
        }

        private MissionOfferIdentityLoadResult LoadUnsafe()
        {
            if (!File.Exists(this.statePath))
            {
                return new MissionOfferIdentityLoadResult(
                    true,
                    false,
                    MinimumOfferId - 1,
                    this.statePath,
                    string.Empty);
            }

            int lastAllocatedOfferId;
            string failure;
            if (!TryReadState(this.statePath, out lastAllocatedOfferId, out failure))
            {
                return new MissionOfferIdentityLoadResult(
                    false,
                    true,
                    0,
                    this.statePath,
                    failure);
            }

            return new MissionOfferIdentityLoadResult(
                true,
                true,
                lastAllocatedOfferId,
                this.statePath,
                string.Empty);
        }

        private bool TryWriteAtomic(int lastAllocatedOfferId, out string failure)
        {
            failure = string.Empty;
            string writeId = Guid.NewGuid().ToString("N");
            string temporary =
                this.statePath
                + "."
                + writeId
                + ".tmp";
            string backup = this.statePath + "." + writeId + ".bak";

            try
            {
                ValidateLastAllocatedOfferId(lastAllocatedOfferId);
                Directory.CreateDirectory(this.directoryPath);

                var values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    {
                        "FormatVersion",
                        CurrentFormatVersion.ToString(CultureInfo.InvariantCulture)
                    },
                    {
                        "LastAllocatedOfferId",
                        lastAllocatedOfferId.ToString(CultureInfo.InvariantCulture)
                    }
                };
                string canonical = SerializeValues(values);
                string complete =
                    Header
                    + "\r\n"
                    + canonical
                    + "RecordSha256="
                    + ComputeSha256(canonical)
                    + "\r\n";
                byte[] bytes = new UTF8Encoding(false, true).GetBytes(complete);

                using (var stream = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                int roundTrip;
                string readFailure;
                if (!TryReadState(temporary, out roundTrip, out readFailure)
                    || roundTrip != lastAllocatedOfferId)
                {
                    failure =
                        "Atomic offer identity state validation failed: "
                        + readFailure;
                    return false;
                }

                if (File.Exists(this.statePath))
                {
                    File.Replace(temporary, this.statePath, backup, true);
                    TryDelete(backup);
                }
                else
                {
                    File.Move(temporary, this.statePath);
                }

                return true;
            }
            catch (Exception ex)
            {
                failure = "Offer identity state write failed: " + ex.Message;
                return false;
            }
            finally
            {
                TryDelete(temporary);
                TryDelete(backup);
            }
        }

        private static bool TryReadState(
            string path,
            out int lastAllocatedOfferId,
            out string failure)
        {
            lastAllocatedOfferId = 0;
            failure = string.Empty;

            try
            {
                string[] lines = File.ReadAllLines(path, new UTF8Encoding(false, true));
                if (lines.Length != ExpectedFieldCount + 2
                    || !string.Equals(lines[0], Header, StringComparison.Ordinal))
                {
                    failure =
                        "Missing offer identity header or malformed/truncated state.";
                    return false;
                }

                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                string suppliedHash = null;
                for (int i = 1; i < lines.Length; i++)
                {
                    int separator = lines[i].IndexOf('=');
                    if (separator <= 0)
                    {
                        failure = "Malformed offer identity state field.";
                        return false;
                    }

                    string key = lines[i].Substring(0, separator);
                    string value = lines[i].Substring(separator + 1);
                    if (string.Equals(key, "RecordSha256", StringComparison.Ordinal))
                    {
                        if (i != lines.Length - 1 || suppliedHash != null)
                        {
                            failure = "Duplicate or misplaced offer identity SHA-256 field.";
                            return false;
                        }

                        suppliedHash = value;
                        continue;
                    }

                    if (!string.Equals(key, "FormatVersion", StringComparison.Ordinal)
                        && !string.Equals(
                            key,
                            "LastAllocatedOfferId",
                            StringComparison.Ordinal))
                    {
                        failure = "Unknown offer identity state field " + key + ".";
                        return false;
                    }

                    if (values.ContainsKey(key))
                    {
                        failure = "Duplicate offer identity state field " + key + ".";
                        return false;
                    }

                    values.Add(key, value);
                }

                if (values.Count != ExpectedFieldCount || suppliedHash == null)
                {
                    failure = "Offer identity state field set is incomplete.";
                    return false;
                }

                if (!IsSha256(suppliedHash))
                {
                    failure = "Malformed offer identity state SHA-256.";
                    return false;
                }

                string canonical = SerializeValues(values);
                string computedHash = ComputeSha256(canonical);
                if (!string.Equals(
                    suppliedHash,
                    computedHash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    failure = "Offer identity state SHA-256 mismatch.";
                    return false;
                }

                int version = ParseInt(values["FormatVersion"], "FormatVersion");
                if (version != CurrentFormatVersion)
                {
                    failure = "Unknown offer identity state version " + version + ".";
                    return false;
                }

                lastAllocatedOfferId = ParseInt(
                    values["LastAllocatedOfferId"],
                    "LastAllocatedOfferId");
                ValidateLastAllocatedOfferId(lastAllocatedOfferId);
                return true;
            }
            catch (Exception ex)
            {
                failure = "Offer identity state load failed: " + ex.Message;
                return false;
            }
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

        private static void ValidateLastAllocatedOfferId(int offerId)
        {
            if (offerId < MinimumOfferId || offerId > MaximumOfferId)
            {
                throw new FormatException(
                    "Last allocated offer identity is outside the supported range.");
            }
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool isDigit = character >= '0' && character <= '9';
                bool isLower = character >= 'a' && character <= 'f';
                bool isUpper = character >= 'A' && character <= 'F';
                if (!isDigit && !isLower && !isUpper)
                {
                    return false;
                }
            }

            return true;
        }

        private static string ComputeSha256(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            byte[] hash;
            using (SHA256 sha = SHA256.Create())
            {
                hash = sha.ComputeHash(bytes);
            }

            var builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static object GetPathGate(string path)
        {
            lock (GateRegistryLock)
            {
                object gate;
                if (!PathGates.TryGetValue(path, out gate))
                {
                    gate = new object();
                    PathGates.Add(path, gate);
                }

                return gate;
            }
        }

        private static MissionOfferIdentityAllocationResult AllocationFailure(
            string diagnostic,
            string statePath)
        {
            return new MissionOfferIdentityAllocationResult(
                false,
                0,
                statePath,
                diagnostic);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // A stale temporary or backup file is never treated as valid state.
            }
        }
    }
}
