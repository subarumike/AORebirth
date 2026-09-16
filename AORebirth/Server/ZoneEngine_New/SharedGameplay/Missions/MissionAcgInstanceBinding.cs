namespace ZoneEngine.Core.Missions
{
    using System;

    /// <summary>
    /// Immutable durable identity descriptor for one accepted mission's selected ACG layout.
    /// Mutable lifecycle state is intentionally held by <see cref="MissionAcgInstanceState"/>.
    /// </summary>
    internal sealed class MissionAcgInstanceBinding
    {
        internal const int CurrentFormatVersion = 2;

        internal MissionAcgInstanceBinding(
            int bindingFormatVersion,
            MissionAcgIdentityRecord acceptedQuestIdentity,
            MissionAcgIdentityRecord originalOfferIdentity,
            MissionAcgIdentityRecord ownerIdentity,
            MissionAcgIdentityRecord teamIdentity,
            MissionRollType missionType,
            int missionQuality,
            int deterministicSeed,
            MissionAcgIdentityRecord missionKeyIdentity,
            MissionAcgIdentityRecord exteriorEntranceIdentity,
            int exteriorEntranceLow,
            int exteriorEntranceHigh,
            float exteriorX,
            float exteriorY,
            float exteriorZ,
            MissionAcgIdentityRecord issuingTerminalIdentity,
            string selectedBundleId,
            string selectedBundlePayloadSha256,
            MissionAcgIdentityRecord acgBuildingIdentity,
            int allocatedLivePlayfield2,
            DateTime acceptedUtc,
            DateTime expiryUtc,
            bool explicitNoTeam)
        {
            if (bindingFormatVersion != CurrentFormatVersion)
            {
                throw new ArgumentOutOfRangeException("bindingFormatVersion");
            }

            RequireIdentity(acceptedQuestIdentity, "acceptedQuestIdentity");
            RequireIdentity(originalOfferIdentity, "originalOfferIdentity");
            RequireIdentity(ownerIdentity, "ownerIdentity");
            RequireIdentity(missionKeyIdentity, "missionKeyIdentity");
            RequireIdentity(exteriorEntranceIdentity, "exteriorEntranceIdentity");
            RequireIdentity(issuingTerminalIdentity, "issuingTerminalIdentity");
            RequireIdentity(acgBuildingIdentity, "acgBuildingIdentity");
            if (teamIdentity != null)
            {
                RequireIdentity(teamIdentity, "teamIdentity");
            }

            if (explicitNoTeam == (teamIdentity != null))
            {
                throw new ArgumentException(
                    "Binding must contain either a concrete team or explicit no-team state.",
                    "explicitNoTeam");
            }

            if (missionType == MissionRollType.Unknown)
            {
                throw new ArgumentOutOfRangeException("missionType");
            }

            if (missionQuality <= 0)
            {
                throw new ArgumentOutOfRangeException("missionQuality");
            }

            if (string.IsNullOrWhiteSpace(selectedBundleId))
            {
                throw new ArgumentException("Selected bundle id is required.", "selectedBundleId");
            }

            if (!IsSha256(selectedBundlePayloadSha256))
            {
                throw new ArgumentException(
                    "Selected bundle payload SHA-256 is required.",
                    "selectedBundlePayloadSha256");
            }

            if (allocatedLivePlayfield2 <= 0)
            {
                throw new ArgumentOutOfRangeException("allocatedLivePlayfield2");
            }

            if (acceptedUtc == DateTime.MinValue || expiryUtc == DateTime.MinValue)
            {
                throw new ArgumentOutOfRangeException("expiryUtc");
            }

            this.BindingFormatVersion = bindingFormatVersion;
            this.AcceptedQuestIdentity = acceptedQuestIdentity;
            this.OriginalOfferIdentity = originalOfferIdentity;
            this.OwnerIdentity = ownerIdentity;
            this.TeamIdentity = teamIdentity;
            this.ExplicitNoTeam = explicitNoTeam;
            this.MissionType = missionType;
            this.MissionQuality = missionQuality;
            this.DeterministicSeed = deterministicSeed;
            this.MissionKeyIdentity = missionKeyIdentity;
            this.ExteriorEntranceIdentity = exteriorEntranceIdentity;
            this.ExteriorEntranceLow = exteriorEntranceLow;
            this.ExteriorEntranceHigh = exteriorEntranceHigh;
            this.ExteriorX = exteriorX;
            this.ExteriorY = exteriorY;
            this.ExteriorZ = exteriorZ;
            this.IssuingTerminalIdentity = issuingTerminalIdentity;
            this.SelectedBundleId = selectedBundleId.Trim();
            this.SelectedBundlePayloadSha256 =
                selectedBundlePayloadSha256.Trim().ToLowerInvariant();
            this.AcgBuildingIdentity = acgBuildingIdentity;
            this.AllocatedLivePlayfield2 = allocatedLivePlayfield2;
            this.AcceptedUtc = ToUtc(acceptedUtc);
            this.ExpiryUtc = expiryUtc.Kind == DateTimeKind.Utc ? expiryUtc : expiryUtc.ToUniversalTime();
            if (this.ExpiryUtc <= this.AcceptedUtc)
            {
                throw new ArgumentException("Expiry must follow acceptance.", "expiryUtc");
            }
        }

        internal int BindingFormatVersion { get; private set; }

        internal MissionAcgIdentityRecord AcceptedQuestIdentity { get; private set; }

        internal MissionAcgIdentityRecord OriginalOfferIdentity { get; private set; }

        internal MissionAcgIdentityRecord OwnerIdentity { get; private set; }

        internal MissionAcgIdentityRecord TeamIdentity { get; private set; }

        internal bool ExplicitNoTeam { get; private set; }

        internal MissionAcgIdentityRecord OwnerOrTeamIdentity
        {
            get
            {
                return this.TeamIdentity ?? this.OwnerIdentity;
            }
        }

        internal MissionRollType MissionType { get; private set; }

        internal int MissionQuality { get; private set; }

        internal int DeterministicSeed { get; private set; }

        internal MissionAcgIdentityRecord MissionKeyIdentity { get; private set; }

        internal MissionAcgIdentityRecord ExteriorEntranceIdentity { get; private set; }

        internal int ExteriorEntranceLow { get; private set; }

        internal int ExteriorEntranceHigh { get; private set; }

        internal float ExteriorX { get; private set; }

        internal float ExteriorY { get; private set; }

        internal float ExteriorZ { get; private set; }

        internal MissionAcgIdentityRecord IssuingTerminalIdentity { get; private set; }

        internal string SelectedBundleId { get; private set; }

        internal string SelectedBundlePayloadSha256 { get; private set; }

        internal MissionAcgIdentityRecord AcgBuildingIdentity { get; private set; }

        internal int AllocatedLivePlayfield2 { get; private set; }

        internal DateTime AcceptedUtc { get; private set; }

        internal DateTime ExpiryUtc { get; private set; }

        internal static MissionAcgInstanceBinding Create(
            MissionAcgIdentityRecord acceptedQuestIdentity,
            MissionAcgIdentityRecord ownerOrTeamIdentity,
            MissionRollType missionType,
            int missionQuality,
            MissionAcgIdentityRecord missionKeyIdentity,
            MissionAcgIdentityRecord exteriorEntranceIdentity,
            MissionAcgLayoutBundle selectedBundle,
            int allocatedLivePlayfield2,
            DateTime expiryUtc,
            int? deterministicSeed)
        {
            if (selectedBundle == null)
            {
                throw new ArgumentNullException("selectedBundle");
            }

            if (!selectedBundle.IsSelectable
                || !selectedBundle.Completeness.IsSelectionComplete
                || !selectedBundle.SupportsMission(missionType, missionQuality))
            {
                throw new ArgumentException(
                    "Selected bundle is not complete/selectable/compatible.",
                    "selectedBundle");
            }

            return new MissionAcgInstanceBinding(
                CurrentFormatVersion,
                acceptedQuestIdentity,
                acceptedQuestIdentity,
                ownerOrTeamIdentity,
                null,
                missionType,
                missionQuality,
                deterministicSeed ?? 0,
                missionKeyIdentity,
                exteriorEntranceIdentity,
                0,
                0,
                0,
                0,
                0,
                exteriorEntranceIdentity,
                selectedBundle.LayoutId,
                selectedBundle.GeneratorPayloadSha256,
                selectedBundle.BuildingIdentity,
                allocatedLivePlayfield2,
                expiryUtc.ToUniversalTime().AddHours(-48),
                expiryUtc,
                true);
        }

        internal static MissionAcgInstanceBinding CreateDurable(
            MissionAcgIdentityRecord acceptedQuestIdentity,
            MissionAcgIdentityRecord originalOfferIdentity,
            MissionAcgIdentityRecord ownerIdentity,
            MissionAcgIdentityRecord teamIdentity,
            MissionRollType missionType,
            int missionQuality,
            int deterministicSeed,
            MissionAcgIdentityRecord missionKeyIdentity,
            MissionAcgIdentityRecord exteriorEntranceIdentity,
            int exteriorEntranceLow,
            int exteriorEntranceHigh,
            float exteriorX,
            float exteriorY,
            float exteriorZ,
            MissionAcgIdentityRecord issuingTerminalIdentity,
            MissionAcgLayoutBundle selectedBundle,
            int allocatedLivePlayfield2,
            DateTime acceptedUtc,
            DateTime expiryUtc)
        {
            if (selectedBundle == null
                || !selectedBundle.IsSelectable
                || !selectedBundle.Completeness.IsSelectionComplete
                || !selectedBundle.SupportsMission(missionType, missionQuality))
            {
                throw new ArgumentException(
                    "Selected bundle is not complete/selectable/compatible.",
                    "selectedBundle");
            }

            return new MissionAcgInstanceBinding(
                CurrentFormatVersion,
                acceptedQuestIdentity,
                originalOfferIdentity,
                ownerIdentity,
                teamIdentity,
                missionType,
                missionQuality,
                deterministicSeed,
                missionKeyIdentity,
                exteriorEntranceIdentity,
                exteriorEntranceLow,
                exteriorEntranceHigh,
                exteriorX,
                exteriorY,
                exteriorZ,
                issuingTerminalIdentity,
                selectedBundle.LayoutId,
                selectedBundle.GeneratorPayloadSha256,
                selectedBundle.BuildingIdentity,
                allocatedLivePlayfield2,
                acceptedUtc,
                expiryUtc,
                teamIdentity == null);
        }

        private static void RequireIdentity(MissionAcgIdentityRecord identity, string parameterName)
        {
            if (identity == null || identity.Type == 0 || identity.Instance == 0)
            {
                throw new ArgumentException("A concrete identity is required.", parameterName);
            }
        }

        private static DateTime ToUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length != 64)
            {
                return false;
            }

            try
            {
                return MissionAcgHash.ParseHex(value, "value").Length == 32;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
