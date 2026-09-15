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

    /// <summary>
    /// Atomic, integrity-checked persistence for Stage 5 mutable NPC, corpse, and chest state.
    /// </summary>
    internal sealed class MissionAcgOperationalStateStore
    {
        private const string Header = "AORebirth.MissionAcgOperationalState";

        private readonly string directory;

        internal MissionAcgOperationalStateStore(string missionStateDirectory)
        {
            if (string.IsNullOrWhiteSpace(missionStateDirectory))
            {
                throw new ArgumentException("Mission state directory is required.");
            }

            this.directory = Path.Combine(missionStateDirectory, "acg-operational");
        }

        internal string PathFor(MissionAcgIdentityRecord acceptedQuestIdentity)
        {
            return Path.Combine(
                this.directory,
                acceptedQuestIdentity.Type.ToString(CultureInfo.InvariantCulture)
                + "-"
                + acceptedQuestIdentity.Instance.ToString(CultureInfo.InvariantCulture)
                + ".operational");
        }

        internal bool TryLoad(
            MissionAcgInstanceBinding binding,
            out MissionAcgOperationalState state,
            out bool exists,
            out string failure)
        {
            state = null;
            exists = false;
            failure = string.Empty;
            string path = this.PathFor(binding.AcceptedQuestIdentity);
            if (!File.Exists(path))
            {
                return true;
            }

            exists = true;
            if (!this.TryRead(path, out state, out failure))
            {
                return false;
            }

            if (!state.AcceptedQuestIdentity.Equals(binding.AcceptedQuestIdentity)
                || !state.OwnerIdentity.Equals(binding.OwnerIdentity)
                || state.AllocatedLivePlayfield2 != binding.AllocatedLivePlayfield2
                || !string.Equals(
                    state.BundleId,
                    binding.SelectedBundleId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    state.BundlePayloadSha256,
                    binding.SelectedBundlePayloadSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !state.BuildingIdentity.Equals(binding.AcgBuildingIdentity))
            {
                state = null;
                failure = "Operational state does not match its durable mission binding.";
                return false;
            }

            return true;
        }

        internal bool TryWrite(
            MissionAcgOperationalState state,
            bool replace,
            out string failure)
        {
            failure = string.Empty;
            Directory.CreateDirectory(this.directory);
            string target = this.PathFor(state.AcceptedQuestIdentity);
            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                if (!replace && File.Exists(target))
                {
                    failure = "Operational state already exists.";
                    return false;
                }

                SortedDictionary<string, string> values = BuildValues(state);
                string canonical = SerializeValues(values);
                values.Add("RecordSha256", ComputeSha256(canonical));
                string payload = Header + "\r\n" + SerializeValues(values);
                File.WriteAllText(temporary, payload, new UTF8Encoding(false, true));

                MissionAcgOperationalState roundTrip;
                string readFailure;
                if (!this.TryRead(temporary, out roundTrip, out readFailure)
                    || !string.Equals(
                        SerializeValues(BuildValues(state)),
                        SerializeValues(BuildValues(roundTrip)),
                        StringComparison.Ordinal))
                {
                    failure =
                        "Atomic operational-state validation failed: "
                        + (readFailure ?? "round-trip mismatch");
                    return false;
                }

                if (replace)
                {
                    if (!File.Exists(target))
                    {
                        failure = "Operational state does not exist for replacement.";
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
            out MissionAcgOperationalState state,
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
                    failure = "Missing operational-state header or truncated sidecar.";
                    return false;
                }

                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 1; i < lines.Length; i++)
                {
                    int separator = lines[i].IndexOf('=');
                    if (separator <= 0)
                    {
                        failure = "Malformed operational-state field at line " + (i + 1) + ".";
                        return false;
                    }

                    string key = lines[i].Substring(0, separator);
                    if (values.ContainsKey(key))
                    {
                        failure = "Duplicate operational-state field " + key + ".";
                        return false;
                    }

                    values.Add(key, lines[i].Substring(separator + 1));
                }

                string suppliedHash = Require(values, "RecordSha256");
                values.Remove("RecordSha256");
                if (!string.Equals(
                    suppliedHash,
                    ComputeSha256(SerializeValues(values)),
                    StringComparison.OrdinalIgnoreCase))
                {
                    failure = "Operational-state SHA-256 mismatch.";
                    return false;
                }

                int formatVersion = ParseInt(Require(values, "FormatVersion"), "FormatVersion");
                if (formatVersion
                        != MissionAcgOperationalState.LegacyCapturedDifficultyFormatVersion
                    && formatVersion
                       != MissionAcgOperationalState.LegacyDeathWitnessFormatVersion
                    && formatVersion != MissionAcgOperationalState.CurrentFormatVersion)
                {
                    failure = "Unknown operational-state format version " + formatVersion + ".";
                    return false;
                }

                MissionAcgIdentityRecord accepted = ParseIdentity(values, "AcceptedQuest");
                MissionAcgIdentityRecord owner = ParseIdentity(values, "Owner");
                MissionAcgIdentityRecord building = ParseIdentity(values, "Building");
                int playfield =
                    ParseInt(
                        Require(values, "AllocatedLivePlayfield2"),
                        "AllocatedLivePlayfield2");
                string bundleId = DecodeText(Require(values, "BundleId"), "BundleId");
                string payloadHash = Require(values, "BundlePayloadSha256");
                MissionAcgOperationalCleanupState cleanup =
                    ParseEnum<MissionAcgOperationalCleanupState>(
                        Require(values, "CleanupState"),
                        "CleanupState");
                DateTime updated = ParseUtc(Require(values, "UpdatedUtc"), "UpdatedUtc");
                int npcCount = ParseCount(Require(values, "NpcCount"), "NpcCount");
                int chestCount = ParseCount(Require(values, "ChestCount"), "ChestCount");

                var npcs = new List<MissionAcgNpcRuntimeState>(npcCount);
                for (int i = 0; i < npcCount; i++)
                {
                    string prefix = "Npc." + i.ToString("D3", CultureInfo.InvariantCulture) + ".";
                    MissionAcgIdentityRecord corpse = ParseOptionalIdentity(values, prefix + "Corpse");
                    MissionAcgIdentityRecord deathCreditedAttacker = null;
                    MissionAcgIdentityRecord deathCreditedOwner = null;
                    DateTime? diedAtUtc = null;
                    int deathSpawnGeneration = 0;
                    MissionAcgNpcDeathHookCheckpoint deathHookCheckpoint =
                        MissionAcgNpcDeathHookCheckpoint.None;
                    if (formatVersion >= MissionAcgOperationalState.CurrentFormatVersion)
                    {
                        deathCreditedAttacker =
                            ParseOptionalIdentity(values, prefix + "DeathCreditedAttacker");
                        deathCreditedOwner =
                            ParseOptionalIdentity(values, prefix + "DeathCreditedOwner");
                        diedAtUtc =
                            ParseOptionalUtc(
                                Require(values, prefix + "DiedAtUtc"),
                                prefix + "DiedAtUtc");
                        deathSpawnGeneration =
                            ParseInt(
                                Require(values, prefix + "DeathSpawnGeneration"),
                                prefix + "DeathSpawnGeneration");
                        deathHookCheckpoint =
                            ParseEnum<MissionAcgNpcDeathHookCheckpoint>(
                                Require(values, prefix + "DeathHookCheckpoint"),
                                prefix + "DeathHookCheckpoint");
                    }

                    npcs.Add(
                        new MissionAcgNpcRuntimeState(
                            ParseInt(Require(values, prefix + "CapturedSlot"), prefix + "CapturedSlot"),
                            ParseIdentity(values, prefix + "Captured"),
                            ParseIdentity(values, prefix + "Runtime"),
                            ParsePoint(values, prefix + "Position"),
                            ParseRotation(values, prefix + "Heading"),
                            ParseInt(Require(values, prefix + "TemplateId"), prefix + "TemplateId"),
                            ParseInt(Require(values, prefix + "MonsterData"), prefix + "MonsterData"),
                            ParseInt(Require(values, prefix + "Level"), prefix + "Level"),
                            ParseInt(
                                Require(values, prefix + "MaximumHealth"),
                                prefix + "MaximumHealth"),
                            ParseInt(
                                Require(values, prefix + "CurrentHealth"),
                                prefix + "CurrentHealth"),
                            ParseInt(
                                Require(values, prefix + "MonsterScale"),
                                prefix + "MonsterScale"),
                            ParseNullableInt(
                                Require(values, prefix + "HeadMesh"),
                                prefix + "HeadMesh"),
                            DecodeText(Require(values, prefix + "Name"), prefix + "Name"),
                            ParseEnum<MissionAcgNpcRole>(
                                Require(values, prefix + "Role"),
                                prefix + "Role"),
                            ParseEnum<MissionAcgNpcLifeState>(
                                Require(values, prefix + "LifeState"),
                                prefix + "LifeState"),
                            ParseEnum<MissionAcgNpcCombatState>(
                                Require(values, prefix + "CombatState"),
                                prefix + "CombatState"),
                            corpse,
                            ParseEnum<MissionAcgCorpseState>(
                                Require(values, prefix + "CorpseState"),
                                prefix + "CorpseState"),
                            ParseInt(
                                Require(values, prefix + "SpawnGeneration"),
                                prefix + "SpawnGeneration"),
                            ParseBool(
                                Require(values, prefix + "CleanupCompleted"),
                                prefix + "CleanupCompleted"),
                            deathCreditedAttacker,
                            deathCreditedOwner,
                            diedAtUtc,
                            deathSpawnGeneration,
                            deathHookCheckpoint));
                }

                var chests = new List<MissionAcgChestRuntimeState>(chestCount);
                for (int i = 0; i < chestCount; i++)
                {
                    string prefix = "Chest." + i.ToString("D3", CultureInfo.InvariantCulture) + ".";
                    chests.Add(
                        new MissionAcgChestRuntimeState(
                            ParseInt(
                                Require(values, prefix + "CapturedSlot"),
                                prefix + "CapturedSlot"),
                            ParseIdentity(values, prefix + "Captured"),
                            ParseIdentity(values, prefix + "Runtime"),
                            ParseEnum<MissionAcgLootAuthority>(
                                Require(values, prefix + "LootAuthority"),
                                prefix + "LootAuthority"),
                            ParseBool(Require(values, prefix + "IsOpen"), prefix + "IsOpen"),
                            ParseBool(
                                Require(values, prefix + "IsExhausted"),
                                prefix + "IsExhausted"),
                            ParseInt(
                                Require(values, prefix + "TransferredItemCount"),
                                prefix + "TransferredItemCount"),
                            ParseBool(
                                Require(values, prefix + "CleanupCompleted"),
                                prefix + "CleanupCompleted")));
                }

                int npcFieldCount =
                    formatVersion >= MissionAcgOperationalState.CurrentFormatVersion
                        ? 38
                        : 29;
                int expectedFields = 14 + (npcCount * npcFieldCount) + (chestCount * 10);
                if (values.Count != expectedFields)
                {
                    failure = "Operational-state field count is inconsistent.";
                    return false;
                }

                state = new MissionAcgOperationalState(
                    formatVersion,
                    accepted,
                    owner,
                    playfield,
                    bundleId,
                    payloadHash,
                    building,
                    npcs,
                    chests,
                    cleanup,
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
            MissionAcgOperationalState state)
        {
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
                         {
                             {
                                 "FormatVersion",
                                 state.FormatVersion.ToString(CultureInfo.InvariantCulture)
                             },
                             { "AcceptedQuest.Type", Int(state.AcceptedQuestIdentity.Type) },
                             { "AcceptedQuest.Instance", Int(state.AcceptedQuestIdentity.Instance) },
                             { "Owner.Type", Int(state.OwnerIdentity.Type) },
                             { "Owner.Instance", Int(state.OwnerIdentity.Instance) },
                             { "AllocatedLivePlayfield2", Int(state.AllocatedLivePlayfield2) },
                             { "BundleId", EncodeText(state.BundleId) },
                             { "BundlePayloadSha256", state.BundlePayloadSha256 },
                             { "Building.Type", Int(state.BuildingIdentity.Type) },
                             { "Building.Instance", Int(state.BuildingIdentity.Instance) },
                             { "CleanupState", Int((int)state.CleanupState) },
                             { "UpdatedUtc", state.UpdatedUtc.ToString("o", CultureInfo.InvariantCulture) },
                             { "NpcCount", Int(state.Npcs.Count) },
                             { "ChestCount", Int(state.Chests.Count) }
                         };

            for (int i = 0; i < state.Npcs.Count; i++)
            {
                MissionAcgNpcRuntimeState npc = state.Npcs[i];
                string prefix = "Npc." + i.ToString("D3", CultureInfo.InvariantCulture) + ".";
                values.Add(prefix + "CapturedSlot", Int(npc.CapturedSlot));
                AddIdentity(values, prefix + "Captured", npc.CapturedIdentity);
                AddIdentity(values, prefix + "Runtime", npc.RuntimeIdentity);
                AddPoint(values, prefix + "Position", npc.Position);
                AddRotation(values, prefix + "Heading", npc.Heading);
                values.Add(prefix + "TemplateId", Int(npc.TemplateId));
                values.Add(prefix + "MonsterData", Int(npc.MonsterData));
                values.Add(prefix + "Level", Int(npc.Level));
                values.Add(prefix + "MaximumHealth", Int(npc.MaximumHealth));
                values.Add(prefix + "CurrentHealth", Int(npc.CurrentHealth));
                values.Add(prefix + "MonsterScale", Int(npc.MonsterScale));
                values.Add(
                    prefix + "HeadMesh",
                    npc.HeadMesh.HasValue ? Int(npc.HeadMesh.Value) : string.Empty);
                values.Add(prefix + "Name", EncodeText(npc.Name));
                values.Add(prefix + "Role", Int((int)npc.Role));
                values.Add(prefix + "LifeState", Int((int)npc.LifeState));
                values.Add(prefix + "CombatState", Int((int)npc.CombatState));
                AddOptionalIdentity(values, prefix + "Corpse", npc.CorpseIdentity);
                values.Add(prefix + "CorpseState", Int((int)npc.CorpseState));
                values.Add(prefix + "SpawnGeneration", Int(npc.SpawnGeneration));
                values.Add(prefix + "CleanupCompleted", Bool(npc.CleanupCompleted));
                if (state.FormatVersion >= MissionAcgOperationalState.CurrentFormatVersion)
                {
                    AddOptionalIdentity(
                        values,
                        prefix + "DeathCreditedAttacker",
                        npc.DeathCreditedAttackerIdentity);
                    AddOptionalIdentity(
                        values,
                        prefix + "DeathCreditedOwner",
                        npc.DeathCreditedOwnerIdentity);
                    values.Add(
                        prefix + "DiedAtUtc",
                        npc.DiedAtUtc.HasValue
                            ? npc.DiedAtUtc.Value.ToString("o", CultureInfo.InvariantCulture)
                            : string.Empty);
                    values.Add(
                        prefix + "DeathSpawnGeneration",
                        Int(npc.DeathSpawnGeneration));
                    values.Add(
                        prefix + "DeathHookCheckpoint",
                        Int((int)npc.DeathHookCheckpoint));
                }
            }

            for (int i = 0; i < state.Chests.Count; i++)
            {
                MissionAcgChestRuntimeState chest = state.Chests[i];
                string prefix = "Chest." + i.ToString("D3", CultureInfo.InvariantCulture) + ".";
                values.Add(prefix + "CapturedSlot", Int(chest.CapturedSlot));
                AddIdentity(values, prefix + "Captured", chest.CapturedIdentity);
                AddIdentity(values, prefix + "Runtime", chest.RuntimeIdentity);
                values.Add(prefix + "LootAuthority", Int((int)chest.LootAuthority));
                values.Add(prefix + "IsOpen", Bool(chest.IsOpen));
                values.Add(prefix + "IsExhausted", Bool(chest.IsExhausted));
                values.Add(prefix + "TransferredItemCount", Int(chest.TransferredItemCount));
                values.Add(prefix + "CleanupCompleted", Bool(chest.CleanupCompleted));
            }

            return values;
        }

        private static void AddIdentity(
            IDictionary<string, string> values,
            string prefix,
            MissionAcgIdentityRecord identity)
        {
            values.Add(prefix + ".Type", Int(identity.Type));
            values.Add(prefix + ".Instance", Int(identity.Instance));
        }

        private static void AddOptionalIdentity(
            IDictionary<string, string> values,
            string prefix,
            MissionAcgIdentityRecord identity)
        {
            values.Add(prefix + ".Present", Bool(identity != null));
            values.Add(prefix + ".Type", identity == null ? string.Empty : Int(identity.Type));
            values.Add(prefix + ".Instance", identity == null ? string.Empty : Int(identity.Instance));
        }

        private static void AddPoint(
            IDictionary<string, string> values,
            string prefix,
            MissionAcgPointRecord point)
        {
            values.Add(prefix + ".X", Float(point.X));
            values.Add(prefix + ".Y", Float(point.Y));
            values.Add(prefix + ".Z", Float(point.Z));
        }

        private static void AddRotation(
            IDictionary<string, string> values,
            string prefix,
            MissionAcgRotationRecord rotation)
        {
            values.Add(prefix + ".X", Float(rotation.X));
            values.Add(prefix + ".Y", Float(rotation.Y));
            values.Add(prefix + ".Z", Float(rotation.Z));
            values.Add(prefix + ".W", Float(rotation.W));
        }

        private static MissionAcgIdentityRecord ParseIdentity(
            IDictionary<string, string> values,
            string prefix)
        {
            return new MissionAcgIdentityRecord(
                ParseInt(Require(values, prefix + ".Type"), prefix + ".Type"),
                ParseInt(Require(values, prefix + ".Instance"), prefix + ".Instance"));
        }

        private static MissionAcgIdentityRecord ParseOptionalIdentity(
            IDictionary<string, string> values,
            string prefix)
        {
            bool present = ParseBool(Require(values, prefix + ".Present"), prefix + ".Present");
            string type = Require(values, prefix + ".Type");
            string instance = Require(values, prefix + ".Instance");
            if (!present)
            {
                if (type.Length != 0 || instance.Length != 0)
                {
                    throw new FormatException(prefix + " contains values while absent.");
                }

                return null;
            }

            return new MissionAcgIdentityRecord(
                ParseInt(type, prefix + ".Type"),
                ParseInt(instance, prefix + ".Instance"));
        }

        private static MissionAcgPointRecord ParsePoint(
            IDictionary<string, string> values,
            string prefix)
        {
            return new MissionAcgPointRecord(
                ParseFloat(Require(values, prefix + ".X"), prefix + ".X"),
                ParseFloat(Require(values, prefix + ".Y"), prefix + ".Y"),
                ParseFloat(Require(values, prefix + ".Z"), prefix + ".Z"));
        }

        private static MissionAcgRotationRecord ParseRotation(
            IDictionary<string, string> values,
            string prefix)
        {
            return new MissionAcgRotationRecord(
                ParseFloat(Require(values, prefix + ".X"), prefix + ".X"),
                ParseFloat(Require(values, prefix + ".Y"), prefix + ".Y"),
                ParseFloat(Require(values, prefix + ".Z"), prefix + ".Z"),
                ParseFloat(Require(values, prefix + ".W"), prefix + ".W"));
        }

        private static string Require(IDictionary<string, string> values, string key)
        {
            string value;
            if (!values.TryGetValue(key, out value))
            {
                throw new FormatException("Missing operational-state field " + key + ".");
            }

            return value;
        }

        private static int ParseCount(string value, string field)
        {
            int parsed = ParseInt(value, field);
            if (parsed < 0 || parsed > 4096)
            {
                throw new FormatException(field + " is outside the supported range.");
            }

            return parsed;
        }

        private static int ParseInt(string value, string field)
        {
            int parsed;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                throw new FormatException(field + " is not an integer.");
            }

            return parsed;
        }

        private static int? ParseNullableInt(string value, string field)
        {
            return value.Length == 0 ? (int?)null : ParseInt(value, field);
        }

        private static float ParseFloat(string value, string field)
        {
            float parsed;
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                || float.IsNaN(parsed)
                || float.IsInfinity(parsed))
            {
                throw new FormatException(field + " is not a finite float.");
            }

            return parsed;
        }

        private static bool ParseBool(string value, string field)
        {
            if (value == "1")
            {
                return true;
            }

            if (value == "0")
            {
                return false;
            }

            throw new FormatException(field + " is not a boolean.");
        }

        private static T ParseEnum<T>(string value, string field)
            where T : struct
        {
            int parsed = ParseInt(value, field);
            if (!Enum.IsDefined(typeof(T), parsed))
            {
                throw new FormatException(field + " is an unknown enum value.");
            }

            return (T)Enum.ToObject(typeof(T), parsed);
        }

        private static DateTime ParseUtc(string value, string field)
        {
            DateTime parsed;
            if (!DateTime.TryParseExact(
                value,
                "o",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out parsed))
            {
                throw new FormatException(field + " is not an ISO-8601 timestamp.");
            }

            return parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
        }

        private static DateTime? ParseOptionalUtc(string value, string field)
        {
            return value.Length == 0 ? (DateTime?)null : ParseUtc(value, field);
        }

        private static string EncodeText(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string DecodeText(string value, string field)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch (Exception ex)
            {
                throw new FormatException(field + " is not valid base64 UTF-8.", ex);
            }
        }

        private static string Int(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Float(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Bool(bool value)
        {
            return value ? "1" : "0";
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
