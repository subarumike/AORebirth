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
    /// Atomic, integrity-checked persistence for the minimal Stage 6 player-position state.
    /// Derived layout envelopes remain immutable-catalog products and are never serialized here.
    /// </summary>
    internal sealed class MissionAcgSpatialStateStore
    {
        private const string FileExtension = ".spatial";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false, true);

        private readonly string directory;

        internal MissionAcgSpatialStateStore(string missionStateDirectory)
        {
            if (string.IsNullOrWhiteSpace(missionStateDirectory))
            {
                throw new ArgumentException("Mission state directory is required.");
            }

            this.directory = Path.Combine(missionStateDirectory, "acg-spatial");
            Directory.CreateDirectory(this.directory);
        }

        internal string ResolvePath(MissionAcgIdentityRecord acceptedQuestIdentity)
        {
            if (acceptedQuestIdentity == null)
            {
                throw new ArgumentNullException("acceptedQuestIdentity");
            }

            return Path.Combine(
                this.directory,
                acceptedQuestIdentity.Type.ToString("X8", CultureInfo.InvariantCulture)
                + "-"
                + acceptedQuestIdentity.Instance.ToString("X8", CultureInfo.InvariantCulture)
                + FileExtension);
        }

        internal bool TryLoad(
            MissionAcgInstanceBinding binding,
            out MissionAcgSpatialState state,
            out bool exists,
            out string failure)
        {
            state = null;
            exists = false;
            failure = string.Empty;
            if (binding == null)
            {
                failure = "Binding is required.";
                return false;
            }

            string path = this.ResolvePath(binding.AcceptedQuestIdentity);
            exists = File.Exists(path);
            if (!exists)
            {
                return true;
            }

            try
            {
                string content = File.ReadAllText(path, Utf8NoBom);
                if (!TryParse(content, out state, out failure))
                {
                    failure = path + ": " + failure;
                    return false;
                }

                if (!MatchesBinding(state, binding))
                {
                    state = null;
                    failure = path + ": spatial state does not match its durable binding.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                state = null;
                failure = path + ": " + ex.Message;
                return false;
            }
        }

        internal bool TryWrite(
            MissionAcgSpatialState state,
            bool replaceExisting,
            out string failure)
        {
            failure = string.Empty;
            if (state == null)
            {
                failure = "Spatial state is required.";
                return false;
            }

            string path = this.ResolvePath(state.AcceptedQuestIdentity);
            string temporaryPath =
                path
                + ".tmp-"
                + System.Diagnostics.Process.GetCurrentProcess().Id.ToString(
                    CultureInfo.InvariantCulture)
                + "-"
                + Guid.NewGuid().ToString("N");
            string backupPath = path + ".replace-backup";
            try
            {
                if (!replaceExisting && File.Exists(path))
                {
                    failure = "Duplicate accepted quest spatial state.";
                    return false;
                }

                string serialized = Serialize(state);
                File.WriteAllText(temporaryPath, serialized, Utf8NoBom);

                MissionAcgSpatialState validated;
                string validationFailure;
                if (!TryParse(
                    File.ReadAllText(temporaryPath, Utf8NoBom),
                    out validated,
                    out validationFailure))
                {
                    failure = "Atomic spatial-state validation failed: " + validationFailure;
                    return false;
                }

                if (File.Exists(path))
                {
                    TryDeleteFile(backupPath);
                    File.Replace(temporaryPath, path, backupPath, true);
                    TryDeleteFile(backupPath);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }

                return true;
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                return false;
            }
            finally
            {
                TryDeleteFile(temporaryPath);
            }
        }

        internal bool TryDelete(
            MissionAcgIdentityRecord acceptedQuestIdentity,
            out string failure)
        {
            failure = string.Empty;
            try
            {
                string path = this.ResolvePath(acceptedQuestIdentity);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return true;
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                return false;
            }
        }

        internal static string Serialize(MissionAcgSpatialState state)
        {
            var lines = new List<string>
                            {
                                "acceptedInstance="
                                + state.AcceptedQuestIdentity.Instance.ToString(
                                    CultureInfo.InvariantCulture),
                                "acceptedType="
                                + state.AcceptedQuestIdentity.Type.ToString(
                                    CultureInfo.InvariantCulture),
                                "buildingInstance="
                                + state.BuildingIdentity.Instance.ToString(
                                    CultureInfo.InvariantCulture),
                                "buildingType="
                                + state.BuildingIdentity.Type.ToString(
                                    CultureInfo.InvariantCulture),
                                "bundleId=" + EncodeText(state.BundleId),
                                "cleanupState="
                                + ((int)state.CleanupState).ToString(
                                    CultureInfo.InvariantCulture),
                                "formatVersion="
                                + state.FormatVersion.ToString(CultureInfo.InvariantCulture),
                                "hasLastValidPlayerPosition="
                                + (state.HasLastValidPlayerPosition ? "1" : "0"),
                                "lastValidX="
                                + state.LastValidPlayerPosition.X.ToString(
                                    "R",
                                    CultureInfo.InvariantCulture),
                                "lastValidY="
                                + state.LastValidPlayerPosition.Y.ToString(
                                    "R",
                                    CultureInfo.InvariantCulture),
                                "lastValidZ="
                                + state.LastValidPlayerPosition.Z.ToString(
                                    "R",
                                    CultureInfo.InvariantCulture),
                                "livePf2="
                                + state.AllocatedLivePlayfield2.ToString(
                                    CultureInfo.InvariantCulture),
                                "ownerInstance="
                                + state.OwnerIdentity.Instance.ToString(
                                    CultureInfo.InvariantCulture),
                                "ownerType="
                                + state.OwnerIdentity.Type.ToString(
                                    CultureInfo.InvariantCulture),
                                "payloadSha256=" + state.BundlePayloadSha256,
                                "updatedTicks="
                                + state.UpdatedUtc.Ticks.ToString(CultureInfo.InvariantCulture)
                            };
            string canonical = string.Join("\n", lines.ToArray()) + "\n";
            return canonical + "sha256=" + ComputeSha256(canonical) + "\n";
        }

        internal static bool TryParse(
            string content,
            out MissionAcgSpatialState state,
            out string failure)
        {
            state = null;
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(content))
            {
                failure = "Spatial state is empty.";
                return false;
            }

            string normalized = content.Replace("\r\n", "\n");
            string[] rawLines = normalized.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (rawLines.Length != 17
                || !rawLines[rawLines.Length - 1].StartsWith(
                    "sha256=",
                    StringComparison.Ordinal))
            {
                failure = "Spatial state is truncated or has an unexpected field count.";
                return false;
            }

            string canonical =
                string.Join("\n", rawLines, 0, rawLines.Length - 1)
                + "\n";
            string expectedHash = rawLines[rawLines.Length - 1].Substring("sha256=".Length);
            if (!FixedTimeEquals(expectedHash, ComputeSha256(canonical)))
            {
                failure = "Spatial state integrity hash does not match.";
                return false;
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < rawLines.Length - 1; i++)
            {
                int separator = rawLines[i].IndexOf('=');
                if (separator <= 0
                    || separator == rawLines[i].Length - 1
                    || values.ContainsKey(rawLines[i].Substring(0, separator)))
                {
                    failure = "Spatial state contains a malformed or duplicate field.";
                    return false;
                }

                values.Add(
                    rawLines[i].Substring(0, separator),
                    rawLines[i].Substring(separator + 1));
            }

            int formatVersion;
            int acceptedType;
            int acceptedInstance;
            int ownerType;
            int ownerInstance;
            int livePf2;
            int buildingType;
            int buildingInstance;
            int cleanupState;
            long updatedTicks;
            float lastValidX;
            float lastValidY;
            float lastValidZ;
            string bundleId;
            bool hasLastValid;
            if (!TryInt(values, "formatVersion", out formatVersion)
                || formatVersion != MissionAcgSpatialState.CurrentFormatVersion)
            {
                failure = "Spatial state format version is unknown.";
                return false;
            }

            if (!TryInt(values, "acceptedType", out acceptedType)
                || !TryInt(values, "acceptedInstance", out acceptedInstance)
                || !TryInt(values, "ownerType", out ownerType)
                || !TryInt(values, "ownerInstance", out ownerInstance)
                || !TryInt(values, "livePf2", out livePf2)
                || !TryInt(values, "buildingType", out buildingType)
                || !TryInt(values, "buildingInstance", out buildingInstance)
                || !TryInt(values, "cleanupState", out cleanupState)
                || !TryLong(values, "updatedTicks", out updatedTicks)
                || !TryFloat(values, "lastValidX", out lastValidX)
                || !TryFloat(values, "lastValidY", out lastValidY)
                || !TryFloat(values, "lastValidZ", out lastValidZ)
                || !TryBooleanFlag(
                    values,
                    "hasLastValidPlayerPosition",
                    out hasLastValid)
                || !TryDecodeText(values, "bundleId", out bundleId)
                || !values.ContainsKey("payloadSha256"))
            {
                failure = "Spatial state contains malformed required values.";
                return false;
            }

            try
            {
                state =
                    new MissionAcgSpatialState(
                        formatVersion,
                        new MissionAcgIdentityRecord(acceptedType, acceptedInstance),
                        new MissionAcgIdentityRecord(ownerType, ownerInstance),
                        livePf2,
                        bundleId,
                        values["payloadSha256"],
                        new MissionAcgIdentityRecord(buildingType, buildingInstance),
                        hasLastValid,
                        new MissionAcgPointRecord(lastValidX, lastValidY, lastValidZ),
                        (MissionAcgSpatialCleanupState)cleanupState,
                        new DateTime(updatedTicks, DateTimeKind.Utc));
                return true;
            }
            catch (Exception ex)
            {
                state = null;
                failure = ex.Message;
                return false;
            }
        }

        private static bool MatchesBinding(
            MissionAcgSpatialState state,
            MissionAcgInstanceBinding binding)
        {
            return state != null
                   && state.AcceptedQuestIdentity.Equals(binding.AcceptedQuestIdentity)
                   && state.OwnerIdentity.Equals(binding.OwnerIdentity)
                   && state.AllocatedLivePlayfield2 == binding.AllocatedLivePlayfield2
                   && string.Equals(
                       state.BundleId,
                       binding.SelectedBundleId,
                       StringComparison.Ordinal)
                   && string.Equals(
                       state.BundlePayloadSha256,
                       binding.SelectedBundlePayloadSha256,
                       StringComparison.OrdinalIgnoreCase)
                   && state.BuildingIdentity.Equals(binding.AcgBuildingIdentity);
        }

        private static bool TryInt(
            IDictionary<string, string> values,
            string key,
            out int value)
        {
            value = 0;
            string text;
            return values.TryGetValue(key, out text)
                   && int.TryParse(
                       text,
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out value);
        }

        private static bool TryLong(
            IDictionary<string, string> values,
            string key,
            out long value)
        {
            value = 0;
            string text;
            return values.TryGetValue(key, out text)
                   && long.TryParse(
                       text,
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out value);
        }

        private static bool TryFloat(
            IDictionary<string, string> values,
            string key,
            out float value)
        {
            value = 0;
            string text;
            return values.TryGetValue(key, out text)
                   && float.TryParse(
                       text,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out value)
                   && !float.IsNaN(value)
                   && !float.IsInfinity(value);
        }

        private static bool TryBooleanFlag(
            IDictionary<string, string> values,
            string key,
            out bool value)
        {
            value = false;
            string text;
            if (!values.TryGetValue(key, out text)
                || (text != "0" && text != "1"))
            {
                return false;
            }

            value = text == "1";
            return true;
        }

        private static bool TryDecodeText(
            IDictionary<string, string> values,
            string key,
            out string value)
        {
            value = string.Empty;
            string encoded;
            if (!values.TryGetValue(key, out encoded))
            {
                return false;
            }

            try
            {
                value = Utf8NoBom.GetString(Convert.FromBase64String(encoded));
                return !string.IsNullOrWhiteSpace(value);
            }
            catch
            {
                return false;
            }
        }

        private static string EncodeText(string value)
        {
            return Convert.ToBase64String(Utf8NoBom.GetBytes(value ?? string.Empty));
        }

        private static string ComputeSha256(string content)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Utf8NoBom.GetBytes(content));
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static bool FixedTimeEquals(string first, string second)
        {
            if (first == null || second == null || first.Length != second.Length)
            {
                return false;
            }

            int difference = 0;
            for (int i = 0; i < first.Length; i++)
            {
                difference |= first[i] ^ second[i];
            }

            return difference == 0;
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Cleanup is best effort; the durable target was never replaced by this path.
            }
        }
    }
}
