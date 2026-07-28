namespace ZoneEngine.Core.Missions
{
    using System;

    /// <summary>
    /// Immutable persisted-stage descriptor for one accepted mission's selected ACG layout.
    /// This type carries the binding data only; acceptance persistence and runtime wiring are deferred.
    /// </summary>
    internal sealed class MissionAcgInstanceBinding
    {
        internal const int CurrentFormatVersion = 1;

        internal MissionAcgInstanceBinding(
            int bindingFormatVersion,
            MissionAcgIdentityRecord acceptedQuestIdentity,
            MissionAcgIdentityRecord ownerOrTeamIdentity,
            MissionRollType missionType,
            int missionQuality,
            MissionAcgIdentityRecord missionKeyIdentity,
            MissionAcgIdentityRecord exteriorEntranceIdentity,
            string selectedBundleId,
            MissionAcgIdentityRecord acgBuildingIdentity,
            int allocatedLivePlayfield2,
            DateTime expiryUtc,
            int? deterministicSeed)
        {
            if (bindingFormatVersion <= 0)
            {
                throw new ArgumentOutOfRangeException("bindingFormatVersion");
            }

            RequireIdentity(acceptedQuestIdentity, "acceptedQuestIdentity");
            RequireIdentity(ownerOrTeamIdentity, "ownerOrTeamIdentity");
            RequireIdentity(missionKeyIdentity, "missionKeyIdentity");
            RequireIdentity(exteriorEntranceIdentity, "exteriorEntranceIdentity");
            RequireIdentity(acgBuildingIdentity, "acgBuildingIdentity");

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

            if (allocatedLivePlayfield2 <= 0)
            {
                throw new ArgumentOutOfRangeException("allocatedLivePlayfield2");
            }

            if (expiryUtc == DateTime.MinValue)
            {
                throw new ArgumentOutOfRangeException("expiryUtc");
            }

            this.BindingFormatVersion = bindingFormatVersion;
            this.AcceptedQuestIdentity = acceptedQuestIdentity;
            this.OwnerOrTeamIdentity = ownerOrTeamIdentity;
            this.MissionType = missionType;
            this.MissionQuality = missionQuality;
            this.MissionKeyIdentity = missionKeyIdentity;
            this.ExteriorEntranceIdentity = exteriorEntranceIdentity;
            this.SelectedBundleId = selectedBundleId.Trim();
            this.AcgBuildingIdentity = acgBuildingIdentity;
            this.AllocatedLivePlayfield2 = allocatedLivePlayfield2;
            this.ExpiryUtc = expiryUtc.Kind == DateTimeKind.Utc ? expiryUtc : expiryUtc.ToUniversalTime();
            this.DeterministicSeed = deterministicSeed;
        }

        internal int BindingFormatVersion { get; private set; }

        internal MissionAcgIdentityRecord AcceptedQuestIdentity { get; private set; }

        internal MissionAcgIdentityRecord OwnerOrTeamIdentity { get; private set; }

        internal MissionRollType MissionType { get; private set; }

        internal int MissionQuality { get; private set; }

        internal MissionAcgIdentityRecord MissionKeyIdentity { get; private set; }

        internal MissionAcgIdentityRecord ExteriorEntranceIdentity { get; private set; }

        internal string SelectedBundleId { get; private set; }

        internal MissionAcgIdentityRecord AcgBuildingIdentity { get; private set; }

        internal int AllocatedLivePlayfield2 { get; private set; }

        internal DateTime ExpiryUtc { get; private set; }

        internal int? DeterministicSeed { get; private set; }

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
                ownerOrTeamIdentity,
                missionType,
                missionQuality,
                missionKeyIdentity,
                exteriorEntranceIdentity,
                selectedBundle.LayoutId,
                selectedBundle.BuildingIdentity,
                allocatedLivePlayfield2,
                expiryUtc,
                deterministicSeed);
        }

        private static void RequireIdentity(MissionAcgIdentityRecord identity, string parameterName)
        {
            if (identity == null || identity.Type == 0 || identity.Instance == 0)
            {
                throw new ArgumentException("A concrete identity is required.", parameterName);
            }
        }
    }
}
