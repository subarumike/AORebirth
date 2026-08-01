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

    internal sealed class MissionAcgObjectiveLoadResult
    {
        internal MissionAcgObjectiveLoadResult(
            IList<MissionAcgObjectiveRecord> records,
            IList<string> diagnostics)
        {
            this.Records = records;
            this.Diagnostics = diagnostics;
        }

        internal IList<MissionAcgObjectiveRecord> Records { get; private set; }

        internal IList<string> Diagnostics { get; private set; }

        internal bool IsValid
        {
            get { return this.Diagnostics.Count == 0; }
        }
    }

    /// <summary>
    /// Atomic, SHA-256 protected objective and completion journal sidecars. The immutable objective
    /// binding and mutable completion state are replaced as one record, preventing independent
    /// objective, reward, or cleanup rerolls.
    /// </summary>
    internal sealed class MissionAcgObjectiveStore
    {
        internal const string DirectoryName = "acg-objectives";

        internal const string FileExtension = ".objective";

        private const string Header = "AORebirth-MissionAcgObjective";

        private const int LegacyExpectedFieldCount = 49;

        private const int ExpectedFieldCount = 115;

        private readonly object sync = new object();

        private readonly string directoryPath;

        private readonly MissionAcgLayoutCatalog catalog;

        internal MissionAcgObjectiveStore(
            string missionStateDirectory,
            MissionAcgLayoutCatalog catalog)
        {
            if (string.IsNullOrWhiteSpace(missionStateDirectory) || catalog == null)
            {
                throw new ArgumentException("Mission state directory and catalog are required.");
            }

            this.directoryPath = Path.Combine(missionStateDirectory, DirectoryName);
            this.catalog = catalog;
        }

        internal string DirectoryPath
        {
            get { return this.directoryPath; }
        }

        internal MissionAcgObjectiveLoadResult LoadAll()
        {
            var records = new List<MissionAcgObjectiveRecord>();
            var diagnostics = new List<string>();
            if (!Directory.Exists(this.directoryPath))
            {
                return new MissionAcgObjectiveLoadResult(records, diagnostics);
            }

            string[] paths =
                Directory.GetFiles(
                    this.directoryPath,
                    "*" + FileExtension,
                    SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            var accepted = new HashSet<string>(StringComparer.Ordinal);
            var runtimeIdentities = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < paths.Length; i++)
            {
                MissionAcgObjectiveRecord record;
                string failure;
                if (!this.TryRead(paths[i], out record, out failure))
                {
                    diagnostics.Add(paths[i] + ": " + failure);
                    continue;
                }

                string acceptedKey = Key(record.Binding.AcceptedQuestIdentity);
                if (!accepted.Add(acceptedKey))
                {
                    diagnostics.Add(paths[i] + ": duplicate accepted quest " + acceptedKey + ".");
                    continue;
                }

                string runtimeKey =
                    record.Binding.AllocatedLivePlayfield2
                    + "|"
                    + Key(record.Binding.RuntimeObjectiveIdentity);
                if (!runtimeIdentities.Add(runtimeKey))
                {
                    diagnostics.Add(paths[i] + ": duplicate runtime objective " + runtimeKey + ".");
                    continue;
                }

                records.Add(record);
            }

            return new MissionAcgObjectiveLoadResult(records, diagnostics);
        }

        internal bool TryCreate(
            MissionAcgObjectiveRecord record,
            out MissionAcgObjectiveRecord persisted,
            out string failure)
        {
            persisted = null;
            failure = string.Empty;
            if (record == null)
            {
                failure = "Objective record is required.";
                return false;
            }

            Directory.CreateDirectory(this.directoryPath);
            string path = this.PathFor(record.Binding.AcceptedQuestIdentity);
            if (File.Exists(path))
            {
                failure = "Objective record already exists for accepted mission.";
                return false;
            }

            var withPath =
                new MissionAcgObjectiveRecord(record.Binding, record.State, path);
            if (!this.TryWriteAtomic(withPath, false, out failure))
            {
                return false;
            }

            persisted = withPath;
            return true;
        }

        internal bool TryReplace(
            MissionAcgObjectiveRecord expected,
            MissionAcgObjectiveRecord record,
            out MissionAcgObjectiveRecord persisted,
            out string failure)
        {
            persisted = null;
            failure = string.Empty;
            if (expected == null || record == null)
            {
                failure = "Expected and replacement objective records are required.";
                return false;
            }

            lock (this.sync)
            {
                string path =
                    string.IsNullOrWhiteSpace(record.RecordPath)
                        ? this.PathFor(record.Binding.AcceptedQuestIdentity)
                        : record.RecordPath;
                if (!File.Exists(path))
                {
                    failure = "Objective record does not exist.";
                    return false;
                }

                MissionAcgObjectiveRecord current;
                if (!this.TryRead(path, out current, out failure))
                {
                    return false;
                }

                if (!string.Equals(
                        Serialize(BuildValues(current)),
                        Serialize(BuildValues(expected)),
                        StringComparison.Ordinal))
                {
                    failure =
                        "Objective record changed after the expected record was read.";
                    return false;
                }

                var withPath =
                    new MissionAcgObjectiveRecord(record.Binding, record.State, path);
                if (!this.TryWriteAtomic(withPath, true, out failure))
                {
                    return false;
                }

                persisted = withPath;
                return true;
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

        private bool TryWriteAtomic(
            MissionAcgObjectiveRecord record,
            bool replace,
            out string failure)
        {
            failure = string.Empty;
            string target = record.RecordPath;
            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                this.Validate(record);
                SortedDictionary<string, string> values = BuildValues(record);
                string canonical = Serialize(values);
                byte[] bytes =
                    new UTF8Encoding(false).GetBytes(
                        Header
                        + "\r\n"
                        + canonical
                        + "RecordSha256="
                        + Hash(canonical)
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

                MissionAcgObjectiveRecord roundTrip;
                string readFailure;
                if (!this.TryRead(temporary, out roundTrip, out readFailure)
                    || !string.Equals(
                        Serialize(BuildValues(record)),
                        Serialize(BuildValues(roundTrip)),
                        StringComparison.Ordinal))
                {
                    failure =
                        "Atomic objective write validation failed: "
                        + (readFailure ?? "round-trip mismatch");
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

        private bool TryRead(
            string path,
            out MissionAcgObjectiveRecord record,
            out string failure)
        {
            record = null;
            failure = string.Empty;
            try
            {
                string[] lines = File.ReadAllLines(path, new UTF8Encoding(false, true));
                if (lines.Length < 2 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
                {
                    failure = "Missing objective header or truncated sidecar.";
                    return false;
                }

                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 1; i < lines.Length; i++)
                {
                    int separator = lines[i].IndexOf('=');
                    if (separator <= 0)
                    {
                        failure = "Malformed objective field at line " + (i + 1) + ".";
                        return false;
                    }

                    string field = lines[i].Substring(0, separator);
                    if (values.ContainsKey(field))
                    {
                        failure = "Duplicate objective field " + field + ".";
                        return false;
                    }

                    values.Add(field, lines[i].Substring(separator + 1));
                }

                string suppliedHash = Require(values, "RecordSha256");
                values.Remove("RecordSha256");
                string canonical = Serialize(values);
                if (!string.Equals(suppliedHash, Hash(canonical), StringComparison.OrdinalIgnoreCase))
                {
                    failure = "Objective record SHA-256 mismatch.";
                    return false;
                }

                int version = Int(values, "FormatVersion");
                int expectedFieldCount =
                    version == MissionAcgObjectiveBinding.LegacyFormatVersion
                        ? LegacyExpectedFieldCount
                        : ExpectedFieldCount;
                if (version != MissionAcgObjectiveBinding.CurrentFormatVersion
                    && version != MissionAcgObjectiveBinding.LegacyFormatVersion)
                {
                    failure = "Unknown objective format version " + version + ".";
                    return false;
                }

                if (values.Count != expectedFieldCount)
                {
                    failure =
                        "Objective field set is incomplete or contains unknown fields (actual="
                        + values.Count
                        + ", expected="
                        + expectedFieldCount
                        + ").";
                    return false;
                }

                bool explicitNoTeam = Bool(values, "ExplicitNoTeam");
                MissionAcgIdentityRecord team = IdentityAllowZero(values, "Team");
                if (explicitNoTeam)
                {
                    if (team.Type != 0 || team.Instance != 0)
                    {
                        failure = "Explicit no-team objective contains a team identity.";
                        return false;
                    }

                    team = null;
                }

                var binding =
                    new MissionAcgObjectiveBinding(
                        version,
                        Identity(values, "AcceptedQuest"),
                        Identity(values, "Owner"),
                        team,
                        explicitNoTeam,
                        (MissionRollType)Int(values, "MissionType"),
                        Int(values, "AllocatedLivePlayfield2"),
                        Require(values, "BundleId"),
                        Require(values, "BundlePayloadSha256"),
                        Identity(values, "Building"),
                        Int(values, "CapturedObjectiveSlot"),
                        Identity(values, "CapturedObjective"),
                        Identity(values, "RuntimeObjective"),
                        Int(values, "ObjectiveTemplateId"),
                        Decode(Require(values, "ObjectiveName")),
                        (MissionAcgObjectiveInteraction)Int(values, "RequiredInteraction"),
                        IdentityAllowZeroOrNull(values, "IssuingTerminal"),
                        Int(values, "RequiredMissionItemTemplateId"),
                        Int(values, "RequiredMachineTemplateId"));
                MissionAcgObjectiveState state =
                    new MissionAcgObjectiveState(
                        (MissionAcgObjectiveLifecycle)Int(values, "Lifecycle"),
                        (MissionAcgCompletionPhase)Int(values, "CompletionPhase"),
                        IdentityAllowZeroOrNull(values, "MissionItem"),
                        Int(values, "FrozenCredits"),
                        Int(values, "FrozenXp"),
                        Int(values, "FrozenItemLowId"),
                        Int(values, "FrozenItemHighId"),
                        Int(values, "FrozenItemQuality"),
                        Int(values, "FrozenItemCount"),
                        (MissionAcgGrantState)Int(values, "CreditsState"),
                        (MissionAcgGrantState)Int(values, "XpState"),
                        (MissionAcgGrantState)Int(values, "ItemState"),
                        Decode(Require(values, "CreditsClaimId")),
                        Decode(Require(values, "XpClaimId")),
                        Decode(Require(values, "ItemClaimId")),
                        Int(values, "GrantedRewardItemInstance"),
                        Bool(values, "ArtifactsRemoved"),
                        Bool(values, "Action59Sent"),
                        Bool(values, "QuestDeleteSent"),
                        Bool(values, "ObjectiveCleanupCompleted"),
                        Bool(values, "MissionCleanupCompleted"),
                        Utc(values, "UpdatedUtc"));
                if (version == MissionAcgObjectiveBinding.CurrentFormatVersion)
                {
                    state = new MissionAcgObjectiveState(
                        state.Lifecycle,
                        state.Phase,
                        state.MissionItemIdentity,
                        state.FrozenCredits,
                        state.FrozenXp,
                        state.FrozenItemLowId,
                        state.FrozenItemHighId,
                        state.FrozenItemQuality,
                        state.FrozenItemCount,
                        state.CreditsState,
                        state.XpState,
                        state.ItemState,
                        state.CreditsClaimId,
                        state.XpClaimId,
                        state.ItemClaimId,
                        state.GrantedRewardItemInstance,
                        state.ArtifactsRemoved,
                        state.Action59Sent,
                        state.QuestDeleteSent,
                        state.ObjectiveCleanupCompleted,
                        state.MissionCleanupCompleted,
                        state.UpdatedUtc,
                        ReadClaim(values, "Credits"),
                        ReadClaim(values, "Xp"),
                        ReadClaim(values, "Item"),
                        ReadClaim(values, "Token"),
                        (MissionAcgDeliveryPhase)Int(values, "RewardFeedbackDelivery"),
                        (MissionAcgDeliveryPhase)Int(values, "MissionAccomplishedDelivery"),
                        (MissionAcgDeliveryPhase)Int(values, "Action59Delivery"),
                        (MissionAcgDeliveryPhase)Int(values, "QuestDeleteDelivery"),
                        (MissionAcgDeliveryPhase)Int(values, "MissionListRemovalDelivery"),
                        (MissionAcgDeliveryPhase)Int(values, "CleanupHandoffDelivery"));
                }
                record = new MissionAcgObjectiveRecord(binding, state, path);
                this.Validate(record);
                return true;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private void Validate(MissionAcgObjectiveRecord record)
        {
            MissionAcgObjectiveBinding binding = record.Binding;
            string stateFailure;
            if (!MissionAcgCompletionRules.IsConsistent(
                record.State,
                out stateFailure))
            {
                throw new InvalidOperationException(stateFailure);
            }

            if (binding.FormatVersion
                    == MissionAcgObjectiveBinding.CurrentFormatVersion
                && (!DurableInventoryClaimIsComplete(record.State.ItemClaim)
                    || !DurableInventoryClaimIsComplete(record.State.TokenClaim)))
            {
                throw new InvalidOperationException(
                    "Reserved item claims require exact item and target-container identities.");
            }

            MissionAcgLayoutBundle bundle = this.catalog.FindByLayoutId(binding.BundleId);
            if (bundle == null
                || !string.Equals(
                    bundle.GeneratorPayloadSha256,
                    binding.BundlePayloadSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !bundle.BuildingIdentity.Equals(binding.BuildingIdentity)
                || binding.CapturedObjectiveSlot >= bundle.ObjectiveSlots.Count)
            {
                throw new InvalidOperationException(
                    "Objective binding does not match its immutable layout bundle.");
            }

            MissionAcgObjectiveSlotRecord slot =
                bundle.ObjectiveSlots[binding.CapturedObjectiveSlot];
            if (!slot.CapturedIdentity.Equals(binding.CapturedObjectiveIdentity)
                || slot.TemplateId != binding.ObjectiveTemplateId
                || !slot.CompatibleMissionTypes.Contains(binding.MissionType))
            {
                throw new InvalidOperationException(
                    "Objective slot identity, template, or mission type mismatch.");
            }

            int encodedPlayfield;
            int ordinal;
            if (!MissionAcgRuntimeMaterializer.TryReverseRuntimeInstance(
                    binding.RuntimeObjectiveIdentity.Instance,
                    out encodedPlayfield,
                    out ordinal)
                || encodedPlayfield != binding.AllocatedLivePlayfield2
                || ordinal <= 0)
            {
                throw new InvalidOperationException(
                    "Runtime objective identity does not belong to allocated PF2.");
            }
        }

        private static bool DurableInventoryClaimIsComplete(
            MissionAcgDurableRewardClaim claim)
        {
            return claim == null
                   || claim.ItemCount <= 0
                   || claim.Phase < MissionAcgDurableClaimPhase.ClaimReserved
                   || (claim.ReservedItemIdentity != null
                       && claim.TargetContainerIdentity != null);
        }

        private static SortedDictionary<string, string> BuildValues(
            MissionAcgObjectiveRecord record)
        {
            MissionAcgObjectiveBinding binding = record.Binding;
            MissionAcgObjectiveState state = record.State;
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                { "AcceptedQuestInstance", F(binding.AcceptedQuestIdentity.Instance) },
                { "AcceptedQuestType", F(binding.AcceptedQuestIdentity.Type) },
                { "Action59Sent", B(state.Action59Sent) },
                { "AllocatedLivePlayfield2", F(binding.AllocatedLivePlayfield2) },
                { "ArtifactsRemoved", B(state.ArtifactsRemoved) },
                { "BuildingInstance", F(binding.BuildingIdentity.Instance) },
                { "BuildingType", F(binding.BuildingIdentity.Type) },
                { "BundleId", binding.BundleId },
                { "BundlePayloadSha256", binding.BundlePayloadSha256 },
                { "CapturedObjectiveInstance", F(binding.CapturedObjectiveIdentity.Instance) },
                { "CapturedObjectiveSlot", F(binding.CapturedObjectiveSlot) },
                { "CapturedObjectiveType", F(binding.CapturedObjectiveIdentity.Type) },
                { "CompletionPhase", F((int)state.Phase) },
                { "CreditsClaimId", Encode(state.CreditsClaimId) },
                { "CreditsState", F((int)state.CreditsState) },
                { "ExplicitNoTeam", B(binding.ExplicitNoTeam) },
                { "FormatVersion", F(binding.FormatVersion) },
                { "FrozenCredits", F(state.FrozenCredits) },
                { "FrozenItemCount", F(state.FrozenItemCount) },
                { "FrozenItemHighId", F(state.FrozenItemHighId) },
                { "FrozenItemLowId", F(state.FrozenItemLowId) },
                { "FrozenItemQuality", F(state.FrozenItemQuality) },
                { "FrozenXp", F(state.FrozenXp) },
                { "GrantedRewardItemInstance", F(state.GrantedRewardItemInstance) },
                { "IssuingTerminalInstance", F(binding.IssuingTerminalIdentity == null ? 0 : binding.IssuingTerminalIdentity.Instance) },
                { "IssuingTerminalType", F(binding.IssuingTerminalIdentity == null ? 0 : binding.IssuingTerminalIdentity.Type) },
                { "ItemClaimId", Encode(state.ItemClaimId) },
                { "ItemState", F((int)state.ItemState) },
                { "Lifecycle", F((int)state.Lifecycle) },
                { "MissionCleanupCompleted", B(state.MissionCleanupCompleted) },
                { "MissionItemInstance", F(state.MissionItemIdentity == null ? 0 : state.MissionItemIdentity.Instance) },
                { "MissionItemType", F(state.MissionItemIdentity == null ? 0 : state.MissionItemIdentity.Type) },
                { "MissionType", F((int)binding.MissionType) },
                { "ObjectiveCleanupCompleted", B(state.ObjectiveCleanupCompleted) },
                { "ObjectiveName", Encode(binding.ObjectiveName) },
                { "ObjectiveTemplateId", F(binding.ObjectiveTemplateId) },
                { "OwnerInstance", F(binding.OwnerIdentity.Instance) },
                { "OwnerType", F(binding.OwnerIdentity.Type) },
                { "QuestDeleteSent", B(state.QuestDeleteSent) },
                { "RequiredInteraction", F((int)binding.RequiredInteraction) },
                { "RequiredMachineTemplateId", F(binding.RequiredMachineTemplateId) },
                { "RequiredMissionItemTemplateId", F(binding.RequiredMissionItemTemplateId) },
                { "RuntimeObjectiveInstance", F(binding.RuntimeObjectiveIdentity.Instance) },
                { "RuntimeObjectiveType", F(binding.RuntimeObjectiveIdentity.Type) },
                { "TeamInstance", F(binding.TeamIdentity == null ? 0 : binding.TeamIdentity.Instance) },
                { "TeamType", F(binding.TeamIdentity == null ? 0 : binding.TeamIdentity.Type) },
                { "UpdatedUtc", state.UpdatedUtc.ToString("O", CultureInfo.InvariantCulture) },
                { "XpClaimId", Encode(state.XpClaimId) },
                { "XpState", F((int)state.XpState) }
            };
            if (binding.FormatVersion
                == MissionAcgObjectiveBinding.CurrentFormatVersion)
            {
                AddClaim(values, "Credits", state.CreditsClaim);
                AddClaim(values, "Xp", state.XpClaim);
                AddClaim(values, "Item", state.ItemClaim);
                AddClaim(values, "Token", state.TokenClaim);
                values.Add(
                    "RewardFeedbackDelivery",
                    F((int)state.RewardFeedbackDelivery));
                values.Add(
                    "MissionAccomplishedDelivery",
                    F((int)state.MissionAccomplishedDelivery));
                values.Add("Action59Delivery", F((int)state.Action59Delivery));
                values.Add("QuestDeleteDelivery", F((int)state.QuestDeleteDelivery));
                values.Add(
                    "MissionListRemovalDelivery",
                    F((int)state.MissionListRemovalDelivery));
                values.Add(
                    "CleanupHandoffDelivery",
                    F((int)state.CleanupHandoffDelivery));
            }
            return values;
        }

        private static void AddClaim(
            IDictionary<string, string> values,
            string prefix,
            MissionAcgDurableRewardClaim claim)
        {
            values.Add(prefix + "ClaimPhase", F((int)claim.Phase));
            values.Add(prefix + "ClaimIdentity", Encode(claim.ClaimId));
            values.Add(prefix + "ClaimAmount", claim.Amount.ToString(CultureInfo.InvariantCulture));
            values.Add(prefix + "ClaimItemLowId", F(claim.ItemLowId));
            values.Add(prefix + "ClaimItemHighId", F(claim.ItemHighId));
            values.Add(prefix + "ClaimItemQuality", F(claim.ItemQuality));
            values.Add(prefix + "ClaimItemCount", F(claim.ItemCount));
            values.Add(
                prefix + "ClaimReservedItemType",
                F(claim.ReservedItemIdentity == null ? 0 : claim.ReservedItemIdentity.Type));
            values.Add(
                prefix + "ClaimReservedItemInstance",
                F(claim.ReservedItemIdentity == null ? 0 : claim.ReservedItemIdentity.Instance));
            values.Add(
                prefix + "ClaimTargetContainerType",
                F(claim.TargetContainerIdentity == null ? 0 : claim.TargetContainerIdentity.Type));
            values.Add(
                prefix + "ClaimTargetContainerInstance",
                F(claim.TargetContainerIdentity == null ? 0 : claim.TargetContainerIdentity.Instance));
            values.Add(
                prefix + "ClaimPreApplyValue",
                claim.PreApplyValue.ToString(CultureInfo.InvariantCulture));
            values.Add(
                prefix + "ClaimExpectedPostValue",
                claim.ExpectedPostValue.ToString(CultureInfo.InvariantCulture));
            values.Add(
                prefix + "ClaimPreApplyFingerprint",
                Encode(claim.PreApplyFingerprint));
            values.Add(prefix + "ClaimFailure", Encode(claim.Failure));
        }

        private static MissionAcgDurableRewardClaim ReadClaim(
            IDictionary<string, string> values,
            string prefix)
        {
            return new MissionAcgDurableRewardClaim(
                (MissionAcgDurableClaimPhase)Int(values, prefix + "ClaimPhase"),
                Decode(Require(values, prefix + "ClaimIdentity")),
                Long(values, prefix + "ClaimAmount"),
                Int(values, prefix + "ClaimItemLowId"),
                Int(values, prefix + "ClaimItemHighId"),
                Int(values, prefix + "ClaimItemQuality"),
                Int(values, prefix + "ClaimItemCount"),
                IdentityAllowZeroOrNull(values, prefix + "ClaimReservedItem"),
                IdentityAllowZeroOrNull(values, prefix + "ClaimTargetContainer"),
                Long(values, prefix + "ClaimPreApplyValue"),
                Long(values, prefix + "ClaimExpectedPostValue"),
                Decode(Require(values, prefix + "ClaimPreApplyFingerprint")),
                Decode(Require(values, prefix + "ClaimFailure")));
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

        private static string Serialize(IDictionary<string, string> values)
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

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return MissionAcgHash.ToHex(
                    sha.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
            }
        }

        private static string Require(IDictionary<string, string> values, string field)
        {
            string value;
            if (!values.TryGetValue(field, out value))
            {
                throw new FormatException("Missing objective field " + field + ".");
            }

            return value;
        }

        private static int Int(IDictionary<string, string> values, string field)
        {
            int value;
            if (!int.TryParse(
                Require(values, field),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value))
            {
                throw new FormatException("Invalid integer objective field " + field + ".");
            }

            return value;
        }

        private static long Long(IDictionary<string, string> values, string field)
        {
            long value;
            if (!long.TryParse(
                Require(values, field),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value))
            {
                throw new FormatException("Invalid Int64 objective field " + field + ".");
            }

            return value;
        }

        private static bool Bool(IDictionary<string, string> values, string field)
        {
            bool value;
            if (!bool.TryParse(Require(values, field), out value))
            {
                throw new FormatException("Invalid boolean objective field " + field + ".");
            }

            return value;
        }

        private static DateTime Utc(IDictionary<string, string> values, string field)
        {
            DateTime value;
            if (!DateTime.TryParseExact(
                    Require(values, field),
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out value)
                || value.Kind != DateTimeKind.Utc)
            {
                throw new FormatException("Invalid UTC objective field " + field + ".");
            }

            return value;
        }

        private static MissionAcgIdentityRecord Identity(
            IDictionary<string, string> values,
            string prefix)
        {
            return new MissionAcgIdentityRecord(
                Int(values, prefix + "Type"),
                Int(values, prefix + "Instance"));
        }

        private static MissionAcgIdentityRecord IdentityAllowZero(
            IDictionary<string, string> values,
            string prefix)
        {
            return new MissionAcgIdentityRecord(
                Int(values, prefix + "Type"),
                Int(values, prefix + "Instance"));
        }

        private static MissionAcgIdentityRecord IdentityAllowZeroOrNull(
            IDictionary<string, string> values,
            string prefix)
        {
            MissionAcgIdentityRecord value = IdentityAllowZero(values, prefix);
            return value.Type == 0 && value.Instance == 0 ? null : value;
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(
                Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string Decode(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty));
        }

        private static string F(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string B(bool value)
        {
            return value ? "true" : "false";
        }

        private static string Key(MissionAcgIdentityRecord identity)
        {
            return identity.Type + ":" + identity.Instance;
        }
    }
}
