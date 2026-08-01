namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    #endregion

    internal sealed class MissionAcgAcceptedProjectionLoadResult
    {
        internal MissionAcgAcceptedProjectionLoadResult(
            IList<MissionAcgAcceptedProjection> projections,
            IList<string> diagnostics)
        {
            this.Projections = projections;
            this.Diagnostics = diagnostics;
        }

        internal IList<MissionAcgAcceptedProjection> Projections { get; private set; }

        internal IList<string> Diagnostics { get; private set; }

        internal bool IsValid
        {
            get { return this.Diagnostics.Count == 0; }
        }
    }

    /// <summary>
    /// Versioned, deterministic and integrity-protected accepted-mission projections. One record owns
    /// the exact selected roll body and one complete ACG binding; no field is reconstructed from a
    /// mutable roll template after acceptance.
    /// </summary>
    internal sealed class MissionAcgAcceptedProjectionStore
    {
        internal const string DirectoryName = "acg-accepted-projections";

        internal const string FileExtension = ".accepted";

        private const string Header = "AORebirth-MissionAcgAcceptedProjection";

        private const int ExpectedFieldCount = 65;

        private readonly object sync = new object();

        private readonly string directoryPath;

        private readonly MissionAcgLayoutCatalog catalog;

        internal MissionAcgAcceptedProjectionStore(
            string missionStateDirectory,
            MissionAcgLayoutCatalog catalog)
        {
            if (string.IsNullOrWhiteSpace(missionStateDirectory) || catalog == null)
            {
                throw new ArgumentException(
                    "Mission state directory and ACG catalog are required.");
            }

            this.directoryPath = Path.Combine(missionStateDirectory, DirectoryName);
            this.catalog = catalog;
        }

        internal string DirectoryPath
        {
            get { return this.directoryPath; }
        }

        internal MissionAcgAcceptedProjectionLoadResult LoadAll()
        {
            lock (this.sync)
            {
                return this.LoadAll_NoLock();
            }
        }

        internal bool TryCreate(
            MissionAcgAcceptedProjection projection,
            out MissionAcgAcceptedProjection persisted,
            out string failure)
        {
            persisted = null;
            failure = string.Empty;
            if (projection == null)
            {
                failure = "Accepted projection is required.";
                return false;
            }

            lock (this.sync)
            {
                try
                {
                    this.ValidateProjection(projection);
                    MissionAcgAcceptedProjectionLoadResult existing =
                        this.LoadAll_NoLock();
                    if (!existing.IsValid)
                    {
                        failure =
                            "Existing accepted projection set is invalid: "
                            + existing.Diagnostics[0];
                        return false;
                    }

                    for (int i = 0; i < existing.Projections.Count; i++)
                    {
                        MissionAcgAcceptedProjection candidate = existing.Projections[i];
                        if (candidate.Binding.AcceptedQuestIdentity.Equals(
                                projection.Binding.AcceptedQuestIdentity))
                        {
                            failure =
                                "Duplicate accepted quest "
                                + IdentityKey(projection.Binding.AcceptedQuestIdentity)
                                + ".";
                            return false;
                        }

                        if (SameOwnerOffer(candidate, projection))
                        {
                            failure =
                                "Offer "
                                + IdentityKey(projection.Binding.OriginalOfferIdentity)
                                + " is already claimed by owner "
                                + IdentityKey(projection.Binding.OwnerIdentity)
                                + ".";
                            return false;
                        }
                    }

                    Directory.CreateDirectory(this.directoryPath);
                    string path = this.PathFor(
                        projection.Binding.AcceptedQuestIdentity);
                    if (File.Exists(path))
                    {
                        failure = "Accepted projection already exists at " + path + ".";
                        return false;
                    }

                    if (!this.TryWriteAtomic(projection, path, false, out failure))
                    {
                        return false;
                    }

                    persisted = projection;
                    return true;
                }
                catch (Exception ex)
                {
                    failure = ex.GetType().Name + ": " + ex.Message;
                    return false;
                }
            }
        }

        internal bool TryReplace(
            MissionAcgAcceptedProjection projection,
            out MissionAcgAcceptedProjection persisted,
            out string failure)
        {
            persisted = null;
            failure = string.Empty;
            if (projection == null)
            {
                failure = "Accepted projection is required.";
                return false;
            }

            lock (this.sync)
            {
                try
                {
                    this.ValidateProjection(projection);
                    string path = this.PathFor(
                        projection.Binding.AcceptedQuestIdentity);
                    if (!File.Exists(path))
                    {
                        failure = "Accepted projection does not exist at " + path + ".";
                        return false;
                    }

                    MissionAcgAcceptedProjection current;
                    if (!this.TryRead(path, out current, out failure))
                    {
                        return false;
                    }

                    if (!string.Equals(
                            ImmutableCanonical(current),
                            ImmutableCanonical(projection),
                            StringComparison.Ordinal))
                    {
                        failure =
                            "Immutable accepted projection fields cannot be replaced.";
                        return false;
                    }

                    if ((int)projection.AcceptancePhase < (int)current.AcceptancePhase)
                    {
                        failure = "Accepted projection phase cannot move backwards.";
                        return false;
                    }

                    if ((current.RuntimeObjectiveIdentity != null
                            && !current.RuntimeObjectiveIdentity.Equals(
                                projection.RuntimeObjectiveIdentity))
                        || (current.MissionArtifactIdentity != null
                            && !current.MissionArtifactIdentity.Equals(
                                projection.MissionArtifactIdentity)))
                    {
                        failure =
                            "Accepted objective and artifact identities cannot be replaced.";
                        return false;
                    }

                    if (!this.TryWriteAtomic(projection, path, true, out failure))
                    {
                        return false;
                    }

                    persisted = projection;
                    return true;
                }
                catch (Exception ex)
                {
                    failure = ex.GetType().Name + ": " + ex.Message;
                    return false;
                }
            }
        }

        internal bool TryGetByAcceptedQuest(
            MissionAcgIdentityRecord acceptedQuestIdentity,
            out MissionAcgAcceptedProjection projection,
            out string failure)
        {
            projection = null;
            failure = string.Empty;
            if (!IsConcrete(acceptedQuestIdentity))
            {
                failure = "A concrete accepted quest identity is required.";
                return false;
            }

            lock (this.sync)
            {
                string path = this.PathFor(acceptedQuestIdentity);
                if (!File.Exists(path))
                {
                    failure = "Accepted projection was not found.";
                    return false;
                }

                return this.TryRead(path, out projection, out failure);
            }
        }

        internal bool TryGetByOwnerOffer(
            MissionAcgIdentityRecord ownerIdentity,
            MissionAcgIdentityRecord originalOfferIdentity,
            out MissionAcgAcceptedProjection projection,
            out string failure)
        {
            projection = null;
            failure = string.Empty;
            if (!IsConcrete(ownerIdentity) || !IsConcrete(originalOfferIdentity))
            {
                failure = "Concrete owner and original-offer identities are required.";
                return false;
            }

            lock (this.sync)
            {
                MissionAcgAcceptedProjectionLoadResult loaded = this.LoadAll_NoLock();
                if (!loaded.IsValid)
                {
                    failure = loaded.Diagnostics[0];
                    return false;
                }

                for (int i = 0; i < loaded.Projections.Count; i++)
                {
                    MissionAcgAcceptedProjection candidate = loaded.Projections[i];
                    if (candidate.Binding.OwnerIdentity.Equals(ownerIdentity)
                        && candidate.Binding.OriginalOfferIdentity.Equals(
                            originalOfferIdentity))
                    {
                        projection = candidate;
                        return true;
                    }
                }

                failure = "Accepted projection was not found for owner and offer.";
                return false;
            }
        }

        private MissionAcgAcceptedProjectionLoadResult LoadAll_NoLock()
        {
            var projections = new List<MissionAcgAcceptedProjection>();
            var diagnostics = new List<string>();
            if (!Directory.Exists(this.directoryPath))
            {
                return new MissionAcgAcceptedProjectionLoadResult(
                    projections,
                    diagnostics);
            }

            string[] paths =
                Directory.GetFiles(
                    this.directoryPath,
                    "*" + FileExtension,
                    SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            var accepted = new HashSet<string>(StringComparer.Ordinal);
            var ownerOffers = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < paths.Length; i++)
            {
                MissionAcgAcceptedProjection projection;
                string failure;
                if (!this.TryRead(paths[i], out projection, out failure))
                {
                    diagnostics.Add(paths[i] + ": " + failure);
                    continue;
                }

                string acceptedKey =
                    IdentityKey(projection.Binding.AcceptedQuestIdentity);
                if (!accepted.Add(acceptedKey))
                {
                    diagnostics.Add(
                        paths[i] + ": duplicate accepted quest " + acceptedKey + ".");
                    continue;
                }

                string ownerOfferKey = OwnerOfferKey(projection);
                if (!ownerOffers.Add(ownerOfferKey))
                {
                    diagnostics.Add(
                        paths[i] + ": duplicate owner plus original offer "
                        + ownerOfferKey + ".");
                    continue;
                }

                projections.Add(projection);
            }

            if (diagnostics.Count != 0)
            {
                projections.Clear();
            }

            return new MissionAcgAcceptedProjectionLoadResult(
                projections,
                diagnostics);
        }

        private bool TryWriteAtomic(
            MissionAcgAcceptedProjection projection,
            string target,
            bool replace,
            out string failure)
        {
            failure = string.Empty;
            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                this.ValidateProjection(projection);
                SortedDictionary<string, string> values = BuildValues(projection);
                string canonical = Serialize(values);
                string complete =
                    Header
                    + "\r\n"
                    + canonical
                    + "RecordSha256="
                    + MissionAcgHash.ComputeSha256(new UTF8Encoding(false).GetBytes(canonical))
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

                MissionAcgAcceptedProjection roundTrip;
                string readFailure;
                if (!this.TryRead(temporary, out roundTrip, out readFailure)
                    || !string.Equals(
                        Serialize(BuildValues(projection)),
                        Serialize(BuildValues(roundTrip)),
                        StringComparison.Ordinal))
                {
                    failure =
                        "Atomic accepted-projection validation failed: "
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
            out MissionAcgAcceptedProjection projection,
            out string failure)
        {
            projection = null;
            failure = string.Empty;
            try
            {
                string[] lines = File.ReadAllLines(path, new UTF8Encoding(false, true));
                if (lines.Length < 2
                    || !string.Equals(lines[0], Header, StringComparison.Ordinal))
                {
                    failure = "Missing accepted-projection header or truncated sidecar.";
                    return false;
                }

                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 1; i < lines.Length; i++)
                {
                    int separator = lines[i].IndexOf('=');
                    if (separator <= 0)
                    {
                        failure =
                            "Malformed accepted-projection field at line "
                            + (i + 1) + ".";
                        return false;
                    }

                    string field = lines[i].Substring(0, separator);
                    if (values.ContainsKey(field))
                    {
                        failure =
                            "Duplicate accepted-projection field " + field + ".";
                        return false;
                    }

                    values.Add(field, lines[i].Substring(separator + 1));
                }

                string suppliedHash = Require(values, "RecordSha256");
                values.Remove("RecordSha256");
                if (values.Count != ExpectedFieldCount)
                {
                    failure =
                        "Accepted-projection field set is incomplete or contains unknown fields.";
                    return false;
                }

                string canonical = Serialize(values);
                string computedHash = MissionAcgHash.ComputeSha256(
                    new UTF8Encoding(false).GetBytes(canonical));
                if (!string.Equals(
                        suppliedHash,
                        computedHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    failure = "Accepted-projection SHA-256 mismatch.";
                    return false;
                }

                int version = Int(values, "FormatVersion");
                if (version != MissionAcgAcceptedProjection.CurrentFormatVersion)
                {
                    failure =
                        "Unknown accepted-projection format version " + version + ".";
                    return false;
                }

                bool explicitNoTeam = Bool(values, "ExplicitNoTeam");
                MissionAcgIdentityRecord team = IdentityAllowZero(values, "Team");
                if (explicitNoTeam)
                {
                    if (team.Type != 0 || team.Instance != 0)
                    {
                        failure =
                            "Explicit no-team accepted projection contains a team identity.";
                        return false;
                    }

                    team = null;
                }

                var binding =
                    new MissionAcgInstanceBinding(
                        Int(values, "BindingFormatVersion"),
                        Identity(values, "AcceptedQuest"),
                        Identity(values, "OriginalOffer"),
                        Identity(values, "Owner"),
                        team,
                        (MissionRollType)Int(values, "MissionType"),
                        Int(values, "MissionQuality"),
                        Int(values, "MissionSeed"),
                        Identity(values, "MissionKey"),
                        Identity(values, "ExteriorEntrance"),
                        Int(values, "ExteriorEntranceLow"),
                        Int(values, "ExteriorEntranceHigh"),
                        Float(values, "ExteriorX"),
                        Float(values, "ExteriorY"),
                        Float(values, "ExteriorZ"),
                        Identity(values, "IssuingTerminal"),
                        Require(values, "SelectedBundleId"),
                        Require(values, "SelectedBundlePayloadSha256"),
                        Identity(values, "AcgBuilding"),
                        Int(values, "AllocatedLivePlayfield2"),
                        Utc(values, "AcceptedUtc"),
                        Utc(values, "ExpiryUtc"),
                        explicitNoTeam);

                int rawLevel = Int(values, "RawLevelSlider");
                if (rawLevel < byte.MinValue || rawLevel > byte.MaxValue)
                {
                    failure = "Raw level slider is outside the byte range.";
                    return false;
                }

                byte[] selectedRollBody = DecodeBytes(
                    Require(values, "SelectedRollBody"),
                    "SelectedRollBody");
                projection =
                    new MissionAcgAcceptedProjection(
                        version,
                        binding,
                        selectedRollBody,
                        Require(values, "SelectedRollBodySha256"),
                        Int(values, "SelectedOfferIndex"),
                        (byte)rawLevel,
                        Int(values, "SliderGoodBad"),
                        Int(values, "SliderOrderChaos"),
                        Int(values, "SliderOpenHidden"),
                        Int(values, "SliderPhysicalMystical"),
                        Int(values, "SliderHeadOnStealth"),
                        Int(values, "SliderMoneyExperience"),
                        Utc(values, "OfferedUtc"),
                        Utc(values, "OfferExpiryUtc"),
                        Int(values, "MissionIconId"),
                        DecodeText(Require(values, "MissionTitle"), "MissionTitle"),
                        DecodeText(
                            Require(values, "MissionDescription"),
                            "MissionDescription"),
                        Int(values, "FrozenCashReward"),
                        Int(values, "FrozenExperienceReward"),
                        Int(values, "FrozenItemLowId"),
                        Int(values, "FrozenItemHighId"),
                        Int(values, "FrozenItemQuality"),
                        Int(values, "FrozenItemCount"),
                        Int(values, "QfuVersion"),
                        Int(values, "QfuQuestIdentityFlag"),
                        (MissionAcgAcceptancePhase)Int(values, "AcceptancePhase"),
                        IdentityAllowZeroOrNull(values, "RuntimeObjective"),
                        IdentityAllowZeroOrNull(values, "MissionArtifact"),
                        Int(values, "RepairArtifactLowId"),
                        Int(values, "RepairArtifactHighId"),
                        (MissionAcgLifecycleState)Int(values, "LifecycleState"),
                        (MissionAcgCleanupState)Int(values, "CleanupState"),
                        Utc(values, "UpdatedUtc"));
                this.ValidateProjection(projection);
                return true;
            }
            catch (Exception ex)
            {
                projection = null;
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private void ValidateProjection(MissionAcgAcceptedProjection projection)
        {
            MissionAcgInstanceBinding binding = projection.Binding;
            MissionAcgLayoutBundle bundle =
                this.catalog.FindByLayoutId(binding.SelectedBundleId);
            if (bundle == null
                || !bundle.IsSelectable
                || !bundle.Completeness.IsSelectionComplete
                || !bundle.SupportsMission(binding.MissionType, binding.MissionQuality)
                || !bundle.BuildingIdentity.Equals(binding.AcgBuildingIdentity)
                || !string.Equals(
                    bundle.GeneratorPayloadSha256,
                    binding.SelectedBundlePayloadSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Accepted projection does not match a selectable immutable ACG bundle.");
            }

            if (binding.AllocatedLivePlayfield2 == bundle.SourcePlayfield2
                || binding.AllocatedLivePlayfield2 == 1419349)
            {
                throw new InvalidOperationException(
                    "Captured or shared PF2 cannot own a live accepted projection.");
            }
        }

        private static SortedDictionary<string, string> BuildValues(
            MissionAcgAcceptedProjection projection)
        {
            MissionAcgInstanceBinding binding = projection.Binding;
            return new SortedDictionary<string, string>(StringComparer.Ordinal)
                   {
                       { "AcceptancePhase", F((int)projection.AcceptancePhase) },
                       { "AcceptedQuestInstance", F(binding.AcceptedQuestIdentity.Instance) },
                       { "AcceptedQuestType", F(binding.AcceptedQuestIdentity.Type) },
                       { "AcceptedUtc", U(binding.AcceptedUtc) },
                       { "AcgBuildingInstance", F(binding.AcgBuildingIdentity.Instance) },
                       { "AcgBuildingType", F(binding.AcgBuildingIdentity.Type) },
                       { "AllocatedLivePlayfield2", F(binding.AllocatedLivePlayfield2) },
                       { "BindingFormatVersion", F(binding.BindingFormatVersion) },
                       { "CleanupState", F((int)projection.CleanupState) },
                       { "ExplicitNoTeam", B(binding.ExplicitNoTeam) },
                       { "ExpiryUtc", U(binding.ExpiryUtc) },
                       { "ExteriorEntranceHigh", F(binding.ExteriorEntranceHigh) },
                       { "ExteriorEntranceInstance", F(binding.ExteriorEntranceIdentity.Instance) },
                       { "ExteriorEntranceLow", F(binding.ExteriorEntranceLow) },
                       { "ExteriorEntranceType", F(binding.ExteriorEntranceIdentity.Type) },
                       { "ExteriorX", R(binding.ExteriorX) },
                       { "ExteriorY", R(binding.ExteriorY) },
                       { "ExteriorZ", R(binding.ExteriorZ) },
                       { "FormatVersion", F(projection.FormatVersion) },
                       { "FrozenCashReward", F(projection.FrozenCashReward) },
                       { "FrozenExperienceReward", F(projection.FrozenExperienceReward) },
                       { "FrozenItemCount", F(projection.FrozenItemCount) },
                       { "FrozenItemHighId", F(projection.FrozenItemHighId) },
                       { "FrozenItemLowId", F(projection.FrozenItemLowId) },
                       { "FrozenItemQuality", F(projection.FrozenItemQuality) },
                       { "IssuingTerminalInstance", F(binding.IssuingTerminalIdentity.Instance) },
                       { "IssuingTerminalType", F(binding.IssuingTerminalIdentity.Type) },
                       { "LifecycleState", F((int)projection.LifecycleState) },
                       {
                           "MissionArtifactInstance",
                           projection.MissionArtifactIdentity == null
                               ? "0"
                               : F(projection.MissionArtifactIdentity.Instance)
                       },
                       {
                           "MissionArtifactType",
                           projection.MissionArtifactIdentity == null
                               ? "0"
                               : F(projection.MissionArtifactIdentity.Type)
                       },
                       { "MissionDescription", EncodeText(projection.Description) },
                       { "MissionIconId", F(projection.MissionIconId) },
                       { "MissionKeyInstance", F(binding.MissionKeyIdentity.Instance) },
                       { "MissionKeyType", F(binding.MissionKeyIdentity.Type) },
                       { "MissionQuality", F(binding.MissionQuality) },
                       { "MissionSeed", F(binding.DeterministicSeed) },
                       { "MissionTitle", EncodeText(projection.Title) },
                       { "MissionType", F((int)binding.MissionType) },
                       { "OfferExpiryUtc", U(projection.OfferExpiryUtc) },
                       { "OfferedUtc", U(projection.OfferedUtc) },
                       { "OriginalOfferInstance", F(binding.OriginalOfferIdentity.Instance) },
                       { "OriginalOfferType", F(binding.OriginalOfferIdentity.Type) },
                       { "OwnerInstance", F(binding.OwnerIdentity.Instance) },
                       { "OwnerType", F(binding.OwnerIdentity.Type) },
                       { "QfuQuestIdentityFlag", F(projection.QfuQuestIdentityFlag) },
                       { "QfuVersion", F(projection.QfuVersion) },
                       { "RawLevelSlider", F(projection.RawLevelSlider) },
                       { "RepairArtifactHighId", F(projection.RepairArtifactHighId) },
                       { "RepairArtifactLowId", F(projection.RepairArtifactLowId) },
                       {
                           "RuntimeObjectiveInstance",
                           projection.RuntimeObjectiveIdentity == null
                               ? "0"
                               : F(projection.RuntimeObjectiveIdentity.Instance)
                       },
                       {
                           "RuntimeObjectiveType",
                           projection.RuntimeObjectiveIdentity == null
                               ? "0"
                               : F(projection.RuntimeObjectiveIdentity.Type)
                       },
                       { "SelectedBundleId", binding.SelectedBundleId },
                       { "SelectedBundlePayloadSha256", binding.SelectedBundlePayloadSha256 },
                       { "SelectedOfferIndex", F(projection.SelectedOfferIndex) },
                       { "SelectedRollBody", Convert.ToBase64String(projection.SelectedRollBody) },
                       { "SelectedRollBodySha256", projection.SelectedRollBodySha256 },
                       { "SliderGoodBad", F(projection.GoodBadSlider) },
                       { "SliderHeadOnStealth", F(projection.HeadOnStealthSlider) },
                       { "SliderMoneyExperience", F(projection.MoneyExperienceSlider) },
                       { "SliderOpenHidden", F(projection.OpenHiddenSlider) },
                       { "SliderOrderChaos", F(projection.OrderChaosSlider) },
                       { "SliderPhysicalMystical", F(projection.PhysicalMysticalSlider) },
                       {
                           "TeamInstance",
                           binding.TeamIdentity == null ? "0" : F(binding.TeamIdentity.Instance)
                       },
                       {
                           "TeamType",
                           binding.TeamIdentity == null ? "0" : F(binding.TeamIdentity.Type)
                       },
                       { "UpdatedUtc", U(projection.UpdatedUtc) }
                   };
        }

        private static string ImmutableCanonical(
            MissionAcgAcceptedProjection projection)
        {
            SortedDictionary<string, string> values = BuildValues(projection);
            values.Remove("AcceptancePhase");
            values.Remove("RuntimeObjectiveType");
            values.Remove("RuntimeObjectiveInstance");
            values.Remove("MissionArtifactType");
            values.Remove("MissionArtifactInstance");
            values.Remove("LifecycleState");
            values.Remove("CleanupState");
            values.Remove("UpdatedUtc");
            return Serialize(values);
        }

        private string PathFor(MissionAcgIdentityRecord acceptedQuestIdentity)
        {
            return Path.Combine(
                this.directoryPath,
                acceptedQuestIdentity.Type.ToString("X8", CultureInfo.InvariantCulture)
                + "-"
                + acceptedQuestIdentity.Instance.ToString("X8", CultureInfo.InvariantCulture)
                + FileExtension);
        }

        private static bool SameOwnerOffer(
            MissionAcgAcceptedProjection left,
            MissionAcgAcceptedProjection right)
        {
            return left.Binding.OwnerIdentity.Equals(right.Binding.OwnerIdentity)
                   && left.Binding.OriginalOfferIdentity.Equals(
                       right.Binding.OriginalOfferIdentity);
        }

        private static string OwnerOfferKey(MissionAcgAcceptedProjection projection)
        {
            return IdentityKey(projection.Binding.OwnerIdentity)
                   + "|"
                   + IdentityKey(projection.Binding.OriginalOfferIdentity);
        }

        private static string IdentityKey(MissionAcgIdentityRecord identity)
        {
            return identity.Type.ToString(CultureInfo.InvariantCulture)
                   + ":"
                   + identity.Instance.ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsConcrete(MissionAcgIdentityRecord identity)
        {
            return identity != null && identity.Type != 0 && identity.Instance != 0;
        }

        private static MissionAcgIdentityRecord Identity(
            IDictionary<string, string> values,
            string prefix)
        {
            MissionAcgIdentityRecord identity = IdentityAllowZero(values, prefix);
            if (!IsConcrete(identity))
            {
                throw new FormatException("Identity " + prefix + " is not concrete.");
            }

            return identity;
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
            MissionAcgIdentityRecord identity = IdentityAllowZero(values, prefix);
            if (identity.Type == 0 && identity.Instance == 0)
            {
                return null;
            }

            if (!IsConcrete(identity))
            {
                throw new FormatException(
                    "Optional identity " + prefix + " is partially specified.");
            }

            return identity;
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

        private static string Require(IDictionary<string, string> values, string field)
        {
            string value;
            if (!values.TryGetValue(field, out value))
            {
                throw new FormatException("Missing field " + field + ".");
            }

            return value;
        }

        private static int Int(IDictionary<string, string> values, string field)
        {
            int parsed;
            if (!int.TryParse(
                    Require(values, field),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out parsed))
            {
                throw new FormatException("Invalid integer field " + field + ".");
            }

            return parsed;
        }

        private static float Float(IDictionary<string, string> values, string field)
        {
            float parsed;
            if (!float.TryParse(
                    Require(values, field),
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

        private static bool Bool(IDictionary<string, string> values, string field)
        {
            bool parsed;
            if (!bool.TryParse(Require(values, field), out parsed))
            {
                throw new FormatException("Invalid boolean field " + field + ".");
            }

            return parsed;
        }

        private static DateTime Utc(IDictionary<string, string> values, string field)
        {
            DateTime parsed;
            if (!DateTime.TryParseExact(
                    Require(values, field),
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out parsed)
                || parsed.Kind != DateTimeKind.Utc)
            {
                throw new FormatException("Invalid UTC field " + field + ".");
            }

            return parsed;
        }

        private static byte[] DecodeBytes(string value, string field)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(value);
                if (bytes.Length == 0)
                {
                    throw new FormatException("Empty Base64 field " + field + ".");
                }

                return bytes;
            }
            catch (FormatException ex)
            {
                throw new FormatException("Invalid Base64 field " + field + ".", ex);
            }
        }

        private static string DecodeText(string value, string field)
        {
            try
            {
                return new UTF8Encoding(false, true).GetString(
                    Convert.FromBase64String(value));
            }
            catch (Exception ex)
            {
                throw new FormatException(
                    "Invalid Base64 UTF-8 field " + field + ".",
                    ex);
            }
        }

        private static string EncodeText(string value)
        {
            return Convert.ToBase64String(
                new UTF8Encoding(false).GetBytes(value ?? string.Empty));
        }

        private static string F(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string B(bool value)
        {
            return value ? "true" : "false";
        }

        private static string R(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string U(DateTime value)
        {
            return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }
    }
}
