namespace ZoneEngine.Core.Missions
{
    using System;

    [Flags]
    internal enum MissionAcgExpiryCheckpoint : long
    {
        None = 0,
        ExpiryDetected = 1L << 0,
        CleanupStarted = 1L << 1,
        InteractionsBlocked = 1L << 2,
        OccupantsEvacuated = 1L << 3,
        NpcsRemoved = 1L << 4,
        ObjectivesRemoved = 1L << 5,
        ContainersRemoved = 1L << 6,
        CorpsesRemoved = 1L << 7,
        InventoryArtifactsRemoved = 1L << 8,
        RuntimeRegistrationsRemoved = 1L << 9,
        OperationalStateFinalized = 1L << 10,
        ClientMissionRemoved = 1L << 11,
        ObjectiveCorpseCleanupVerified = 1L << 12,
        BindingReleaseReady = 1L << 13,
        Pf2ReleaseAttempted = 1L << 14,
        Pf2ReleaseConfirmed = 1L << 15,
        CleanupComplete = 1L << 16
    }

    internal enum MissionAcgExpiryStatus
    {
        InProgress = 1,
        RetryPending = 2,
        TerminalFailure = 3,
        Complete = 4
    }

    /// <summary>
    /// Durable, monotonic cleanup journal for one expired generated terminal mission.
    /// The embedded identities are an immutable snapshot of the authoritative binding.
    /// </summary>
    internal sealed class MissionAcgExpiryState
    {
        internal const int CurrentFormatVersion = 1;

        private const MissionAcgExpiryCheckpoint AllKnownCheckpoints =
            MissionAcgExpiryCheckpoint.ExpiryDetected
            | MissionAcgExpiryCheckpoint.CleanupStarted
            | MissionAcgExpiryCheckpoint.InteractionsBlocked
            | MissionAcgExpiryCheckpoint.OccupantsEvacuated
            | MissionAcgExpiryCheckpoint.NpcsRemoved
            | MissionAcgExpiryCheckpoint.ObjectivesRemoved
            | MissionAcgExpiryCheckpoint.ContainersRemoved
            | MissionAcgExpiryCheckpoint.CorpsesRemoved
            | MissionAcgExpiryCheckpoint.InventoryArtifactsRemoved
            | MissionAcgExpiryCheckpoint.RuntimeRegistrationsRemoved
            | MissionAcgExpiryCheckpoint.OperationalStateFinalized
            | MissionAcgExpiryCheckpoint.ClientMissionRemoved
            | MissionAcgExpiryCheckpoint.ObjectiveCorpseCleanupVerified
            | MissionAcgExpiryCheckpoint.BindingReleaseReady
            | MissionAcgExpiryCheckpoint.Pf2ReleaseAttempted
            | MissionAcgExpiryCheckpoint.Pf2ReleaseConfirmed
            | MissionAcgExpiryCheckpoint.CleanupComplete;

        internal MissionAcgExpiryState(
            int formatVersion,
            MissionAcgIdentityRecord acceptedQuestIdentity,
            MissionAcgIdentityRecord originalOfferIdentity,
            MissionAcgIdentityRecord ownerIdentity,
            MissionAcgIdentityRecord teamIdentity,
            bool explicitNoTeam,
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
            DateTime firstDetectedUtc,
            DateTime updatedUtc,
            MissionAcgExpiryCheckpoint checkpoints,
            MissionAcgExpiryStatus status,
            bool requiresOwnerReconciliation,
            int retryCount,
            string lastFailure)
        {
            if (formatVersion != CurrentFormatVersion)
            {
                throw new ArgumentOutOfRangeException("formatVersion");
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
                    "Expiry state must contain either a concrete team or explicit no-team state.",
                    "explicitNoTeam");
            }

            if (!Enum.IsDefined(typeof(MissionRollType), missionType)
                || missionType == MissionRollType.Unknown)
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

            DateTime accepted = RequireUtcValue(acceptedUtc, "acceptedUtc");
            DateTime expiry = RequireUtcValue(expiryUtc, "expiryUtc");
            DateTime firstDetected = RequireUtcValue(firstDetectedUtc, "firstDetectedUtc");
            DateTime updated = RequireUtcValue(updatedUtc, "updatedUtc");
            if (expiry <= accepted)
            {
                throw new ArgumentException("Expiry must follow acceptance.", "expiryUtc");
            }

            if (firstDetected < expiry)
            {
                throw new ArgumentException(
                    "Expiry cannot be recorded before its authoritative timestamp.",
                    "firstDetectedUtc");
            }

            if (updated < firstDetected)
            {
                throw new ArgumentException(
                    "Expiry state update precedes first detection.",
                    "updatedUtc");
            }

            if (!Enum.IsDefined(typeof(MissionAcgExpiryStatus), status))
            {
                throw new ArgumentOutOfRangeException("status");
            }

            if (retryCount < 0)
            {
                throw new ArgumentOutOfRangeException("retryCount");
            }

            ValidateCheckpoints(checkpoints, status);

            this.FormatVersion = formatVersion;
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
            this.AcceptedUtc = accepted;
            this.ExpiryUtc = expiry;
            this.FirstDetectedUtc = firstDetected;
            this.UpdatedUtc = updated;
            this.Checkpoints = checkpoints;
            this.Status = status;
            this.RequiresOwnerReconciliation = requiresOwnerReconciliation;
            this.RetryCount = retryCount;
            this.LastFailure = (lastFailure ?? string.Empty).Trim();
        }

        internal int FormatVersion { get; private set; }

        internal MissionAcgIdentityRecord AcceptedQuestIdentity { get; private set; }

        internal MissionAcgIdentityRecord OriginalOfferIdentity { get; private set; }

        internal MissionAcgIdentityRecord OwnerIdentity { get; private set; }

        internal MissionAcgIdentityRecord TeamIdentity { get; private set; }

        internal bool ExplicitNoTeam { get; private set; }

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

        internal DateTime FirstDetectedUtc { get; private set; }

        internal DateTime UpdatedUtc { get; private set; }

        internal MissionAcgExpiryCheckpoint Checkpoints { get; private set; }

        internal MissionAcgExpiryStatus Status { get; private set; }

        internal bool RequiresOwnerReconciliation { get; private set; }

        internal int RetryCount { get; private set; }

        internal string LastFailure { get; private set; }

        internal bool IsComplete
        {
            get
            {
                return this.Status == MissionAcgExpiryStatus.Complete
                       && this.HasCheckpoint(MissionAcgExpiryCheckpoint.CleanupComplete);
            }
        }

        internal static MissionAcgExpiryState Create(
            MissionAcgInstanceBinding binding,
            DateTime detectedUtc)
        {
            return Create(binding, detectedUtc, false);
        }

        internal static MissionAcgExpiryState Create(
            MissionAcgInstanceBinding binding,
            DateTime detectedUtc,
            bool requiresOwnerReconciliation)
        {
            if (binding == null)
            {
                throw new ArgumentNullException("binding");
            }

            return new MissionAcgExpiryState(
                CurrentFormatVersion,
                binding.AcceptedQuestIdentity,
                binding.OriginalOfferIdentity,
                binding.OwnerIdentity,
                binding.TeamIdentity,
                binding.ExplicitNoTeam,
                binding.MissionType,
                binding.MissionQuality,
                binding.DeterministicSeed,
                binding.MissionKeyIdentity,
                binding.ExteriorEntranceIdentity,
                binding.ExteriorEntranceLow,
                binding.ExteriorEntranceHigh,
                binding.ExteriorX,
                binding.ExteriorY,
                binding.ExteriorZ,
                binding.IssuingTerminalIdentity,
                binding.SelectedBundleId,
                binding.SelectedBundlePayloadSha256,
                binding.AcgBuildingIdentity,
                binding.AllocatedLivePlayfield2,
                binding.AcceptedUtc,
                binding.ExpiryUtc,
                detectedUtc,
                detectedUtc,
                MissionAcgExpiryCheckpoint.ExpiryDetected,
                MissionAcgExpiryStatus.InProgress,
                requiresOwnerReconciliation,
                0,
                string.Empty);
        }

        internal bool HasCheckpoint(MissionAcgExpiryCheckpoint checkpoint)
        {
            return checkpoint != MissionAcgExpiryCheckpoint.None
                   && (this.Checkpoints & checkpoint) == checkpoint;
        }

        internal MissionAcgExpiryState Advance(
            MissionAcgExpiryCheckpoint additionalCheckpoints,
            MissionAcgExpiryStatus nextStatus,
            DateTime updatedUtc,
            string failure)
        {
            if ((additionalCheckpoints & ~AllKnownCheckpoints) != 0)
            {
                throw new ArgumentOutOfRangeException("additionalCheckpoints");
            }

            if (!Enum.IsDefined(typeof(MissionAcgExpiryStatus), nextStatus))
            {
                throw new ArgumentOutOfRangeException("nextStatus");
            }

            if ((int)nextStatus < (int)this.Status)
            {
                throw new InvalidOperationException("Expiry status cannot regress.");
            }

            MissionAcgExpiryCheckpoint nextCheckpoints =
                this.Checkpoints | additionalCheckpoints;
            bool changesCheckpoints = nextCheckpoints != this.Checkpoints;
            bool changesStatus = nextStatus != this.Status;
            if ((this.Status == MissionAcgExpiryStatus.TerminalFailure
                 || this.Status == MissionAcgExpiryStatus.Complete)
                && (changesCheckpoints || changesStatus))
            {
                throw new InvalidOperationException(
                    "Terminal expiry state cannot be advanced.");
            }

            DateTime updated = RequireUtcValue(updatedUtc, "updatedUtc");
            if (updated < this.UpdatedUtc)
            {
                throw new InvalidOperationException(
                    "Expiry state timestamp cannot regress.");
            }

            string suppliedFailure = (failure ?? string.Empty).Trim();
            if (nextStatus == MissionAcgExpiryStatus.TerminalFailure
                && suppliedFailure.Length == 0)
            {
                throw new ArgumentException(
                    "Terminal expiry failure requires a diagnostic.",
                    "failure");
            }

            ValidateCheckpoints(nextCheckpoints, nextStatus);
            int retryCount = this.RetryCount;
            if (nextStatus == MissionAcgExpiryStatus.RetryPending
                && suppliedFailure.Length != 0)
            {
                retryCount++;
            }

            return new MissionAcgExpiryState(
                this.FormatVersion,
                this.AcceptedQuestIdentity,
                this.OriginalOfferIdentity,
                this.OwnerIdentity,
                this.TeamIdentity,
                this.ExplicitNoTeam,
                this.MissionType,
                this.MissionQuality,
                this.DeterministicSeed,
                this.MissionKeyIdentity,
                this.ExteriorEntranceIdentity,
                this.ExteriorEntranceLow,
                this.ExteriorEntranceHigh,
                this.ExteriorX,
                this.ExteriorY,
                this.ExteriorZ,
                this.IssuingTerminalIdentity,
                this.SelectedBundleId,
                this.SelectedBundlePayloadSha256,
                this.AcgBuildingIdentity,
                this.AllocatedLivePlayfield2,
                this.AcceptedUtc,
                this.ExpiryUtc,
                this.FirstDetectedUtc,
                updated,
                nextCheckpoints,
                nextStatus,
                this.RequiresOwnerReconciliation,
                retryCount,
                suppliedFailure.Length == 0 ? this.LastFailure : suppliedFailure);
        }

        internal bool MatchesBinding(MissionAcgInstanceBinding binding, out string failure)
        {
            failure = string.Empty;
            if (binding == null)
            {
                failure = "Binding is required.";
                return false;
            }

            if (!this.AcceptedQuestIdentity.Equals(binding.AcceptedQuestIdentity)
                || !this.OriginalOfferIdentity.Equals(binding.OriginalOfferIdentity)
                || !this.OwnerIdentity.Equals(binding.OwnerIdentity)
                || !EqualIdentity(this.TeamIdentity, binding.TeamIdentity)
                || this.ExplicitNoTeam != binding.ExplicitNoTeam
                || this.MissionType != binding.MissionType
                || this.MissionQuality != binding.MissionQuality
                || this.DeterministicSeed != binding.DeterministicSeed
                || !this.MissionKeyIdentity.Equals(binding.MissionKeyIdentity)
                || !this.ExteriorEntranceIdentity.Equals(binding.ExteriorEntranceIdentity)
                || this.ExteriorEntranceLow != binding.ExteriorEntranceLow
                || this.ExteriorEntranceHigh != binding.ExteriorEntranceHigh
                || !this.ExteriorX.Equals(binding.ExteriorX)
                || !this.ExteriorY.Equals(binding.ExteriorY)
                || !this.ExteriorZ.Equals(binding.ExteriorZ)
                || !this.IssuingTerminalIdentity.Equals(binding.IssuingTerminalIdentity)
                || !string.Equals(
                    this.SelectedBundleId,
                    binding.SelectedBundleId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    this.SelectedBundlePayloadSha256,
                    binding.SelectedBundlePayloadSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !this.AcgBuildingIdentity.Equals(binding.AcgBuildingIdentity)
                || this.AllocatedLivePlayfield2 != binding.AllocatedLivePlayfield2
                || this.AcceptedUtc != binding.AcceptedUtc.ToUniversalTime()
                || this.ExpiryUtc != binding.ExpiryUtc.ToUniversalTime())
            {
                failure =
                    "Expiry state identity does not match accepted quest "
                    + binding.AcceptedQuestIdentity.Type
                    + ":"
                    + binding.AcceptedQuestIdentity.Instance
                    + ".";
                return false;
            }

            return true;
        }

        private static void ValidateCheckpoints(
            MissionAcgExpiryCheckpoint checkpoints,
            MissionAcgExpiryStatus status)
        {
            if ((checkpoints & ~AllKnownCheckpoints) != 0
                || (checkpoints & MissionAcgExpiryCheckpoint.ExpiryDetected) == 0)
            {
                throw new ArgumentException("Expiry checkpoint set is invalid.", "checkpoints");
            }

            RequirePredecessor(
                checkpoints,
                MissionAcgExpiryCheckpoint.CleanupStarted,
                MissionAcgExpiryCheckpoint.ExpiryDetected);
            RequirePredecessor(
                checkpoints,
                MissionAcgExpiryCheckpoint.InteractionsBlocked,
                MissionAcgExpiryCheckpoint.CleanupStarted);
            RequirePredecessor(
                checkpoints,
                MissionAcgExpiryCheckpoint.OccupantsEvacuated,
                MissionAcgExpiryCheckpoint.InteractionsBlocked);

            MissionAcgExpiryCheckpoint contentCleanup =
                MissionAcgExpiryCheckpoint.NpcsRemoved
                | MissionAcgExpiryCheckpoint.ObjectivesRemoved
                | MissionAcgExpiryCheckpoint.ContainersRemoved
                | MissionAcgExpiryCheckpoint.CorpsesRemoved
                | MissionAcgExpiryCheckpoint.InventoryArtifactsRemoved
                | MissionAcgExpiryCheckpoint.RuntimeRegistrationsRemoved
                | MissionAcgExpiryCheckpoint.ClientMissionRemoved;
            if ((checkpoints & contentCleanup) != 0)
            {
                RequirePredecessor(
                    checkpoints,
                    contentCleanup,
                    MissionAcgExpiryCheckpoint.InteractionsBlocked);
            }

            MissionAcgExpiryCheckpoint operationalPrerequisites =
                MissionAcgExpiryCheckpoint.NpcsRemoved
                | MissionAcgExpiryCheckpoint.ContainersRemoved
                | MissionAcgExpiryCheckpoint.CorpsesRemoved
                | MissionAcgExpiryCheckpoint.RuntimeRegistrationsRemoved;
            RequirePredecessor(
                checkpoints,
                MissionAcgExpiryCheckpoint.OperationalStateFinalized,
                operationalPrerequisites);
            RequirePredecessor(
                checkpoints,
                MissionAcgExpiryCheckpoint.ObjectiveCorpseCleanupVerified,
                MissionAcgExpiryCheckpoint.ObjectivesRemoved
                | MissionAcgExpiryCheckpoint.CorpsesRemoved);

            MissionAcgExpiryCheckpoint releasePrerequisites =
                MissionAcgExpiryCheckpoint.OccupantsEvacuated
                | MissionAcgExpiryCheckpoint.NpcsRemoved
                | MissionAcgExpiryCheckpoint.ObjectivesRemoved
                | MissionAcgExpiryCheckpoint.ContainersRemoved
                | MissionAcgExpiryCheckpoint.CorpsesRemoved
                | MissionAcgExpiryCheckpoint.InventoryArtifactsRemoved
                | MissionAcgExpiryCheckpoint.RuntimeRegistrationsRemoved
                | MissionAcgExpiryCheckpoint.OperationalStateFinalized
                | MissionAcgExpiryCheckpoint.ClientMissionRemoved
                | MissionAcgExpiryCheckpoint.ObjectiveCorpseCleanupVerified;
            RequirePredecessor(
                checkpoints,
                MissionAcgExpiryCheckpoint.BindingReleaseReady,
                releasePrerequisites);
            RequirePredecessor(
                checkpoints,
                MissionAcgExpiryCheckpoint.Pf2ReleaseAttempted,
                MissionAcgExpiryCheckpoint.BindingReleaseReady);
            RequirePredecessor(
                checkpoints,
                MissionAcgExpiryCheckpoint.Pf2ReleaseConfirmed,
                MissionAcgExpiryCheckpoint.Pf2ReleaseAttempted);
            RequirePredecessor(
                checkpoints,
                MissionAcgExpiryCheckpoint.CleanupComplete,
                MissionAcgExpiryCheckpoint.Pf2ReleaseConfirmed);

            bool hasComplete =
                (checkpoints & MissionAcgExpiryCheckpoint.CleanupComplete) != 0;
            if (hasComplete != (status == MissionAcgExpiryStatus.Complete))
            {
                throw new ArgumentException(
                    "Expiry completion status and checkpoint disagree.",
                    "status");
            }
        }

        private static void RequirePredecessor(
            MissionAcgExpiryCheckpoint checkpoints,
            MissionAcgExpiryCheckpoint checkpoint,
            MissionAcgExpiryCheckpoint predecessor)
        {
            if ((checkpoints & checkpoint) != 0
                && (checkpoints & predecessor) != predecessor)
            {
                throw new ArgumentException(
                    "Expiry checkpoint "
                    + checkpoint
                    + " is missing predecessor "
                    + predecessor
                    + ".",
                    "checkpoints");
            }
        }

        private static bool EqualIdentity(
            MissionAcgIdentityRecord left,
            MissionAcgIdentityRecord right)
        {
            return left == null ? right == null : left.Equals(right);
        }

        private static void RequireIdentity(
            MissionAcgIdentityRecord identity,
            string parameterName)
        {
            if (identity == null || identity.Type == 0 || identity.Instance == 0)
            {
                throw new ArgumentException("A concrete identity is required.", parameterName);
            }
        }

        private static DateTime RequireUtcValue(DateTime value, string parameterName)
        {
            if (value == DateTime.MinValue)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

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

    internal sealed class MissionAcgExpiryRecord
    {
        internal MissionAcgExpiryRecord(MissionAcgExpiryState state, string recordPath)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            this.State = state;
            this.RecordPath = recordPath ?? string.Empty;
        }

        internal MissionAcgExpiryState State { get; private set; }

        internal string RecordPath { get; private set; }

        internal MissionAcgExpiryRecord WithState(MissionAcgExpiryState state)
        {
            return new MissionAcgExpiryRecord(state, this.RecordPath);
        }
    }

    internal static class MissionAcgExpiryPolicy
    {
        internal static bool IsDue(DateTime nowUtc, DateTime expiryUtc)
        {
            return RequireUtc(nowUtc, "nowUtc") >= RequireUtc(expiryUtc, "expiryUtc");
        }

        internal static bool IsCompletionOwned(MissionAcgCompletionPhase completionPhase)
        {
            return completionPhase >= MissionAcgCompletionPhase.RewardClaimStarted;
        }

        internal static bool CanBeginExpiry(
            MissionAcgLifecycleState lifecycle,
            MissionAcgCompletionPhase completionPhase,
            bool completionClaimed,
            bool abandonmentClaimed,
            DateTime nowUtc,
            DateTime expiryUtc)
        {
            bool lifecycleCanExpire =
                lifecycle == MissionAcgLifecycleState.Reserved
                || lifecycle == MissionAcgLifecycleState.Accepted
                || lifecycle == MissionAcgLifecycleState.Active
                || lifecycle == MissionAcgLifecycleState.CompletionStarted;
            return lifecycleCanExpire
                   && !completionClaimed
                   && !abandonmentClaimed
                   && !IsCompletionOwned(completionPhase)
                   && IsDue(nowUtc, expiryUtc);
        }

        internal static bool CanBeginAbandonment(
            MissionAcgLifecycleState lifecycle,
            MissionAcgCompletionPhase completionPhase,
            bool expiryClaimed,
            bool completionClaimed,
            bool abandonmentOwned,
            DateTime nowUtc,
            DateTime expiryUtc)
        {
            if (abandonmentOwned)
            {
                return true;
            }

            return (lifecycle == MissionAcgLifecycleState.Accepted
                    || lifecycle == MissionAcgLifecycleState.Active)
                   && !expiryClaimed
                   && !completionClaimed
                   && !IsCompletionOwned(completionPhase)
                   && !IsDue(nowUtc, expiryUtc);
        }

        internal static bool CanBeginCompletion(
            MissionAcgLifecycleState lifecycle,
            MissionAcgCompletionPhase completionPhase,
            MissionAcgExpiryState expiryState,
            DateTime nowUtc,
            DateTime expiryUtc)
        {
            return expiryState == null
                   && completionPhase < MissionAcgCompletionPhase.RewardClaimStarted
                   && (lifecycle == MissionAcgLifecycleState.Accepted
                       || lifecycle == MissionAcgLifecycleState.Active
                       || lifecycle == MissionAcgLifecycleState.CompletionStarted)
                   && !IsDue(nowUtc, expiryUtc);
        }

        internal static bool CanContinueCompletion(
            MissionAcgCompletionPhase completionPhase,
            bool completionClaimed,
            MissionAcgExpiryState expiryState,
            DateTime nowUtc,
            DateTime expiryUtc)
        {
            if (completionClaimed || IsCompletionOwned(completionPhase))
            {
                return expiryState == null;
            }

            return expiryState == null && !IsDue(nowUtc, expiryUtc);
        }

        internal static bool BlocksNewAction(
            MissionAcgInstanceState bindingState,
            MissionAcgExpiryState expiryState,
            DateTime nowUtc,
            DateTime expiryUtc)
        {
            if (bindingState == null)
            {
                return true;
            }

            return expiryState != null
                   || !bindingState.CanEnter(nowUtc, expiryUtc)
                   || IsDue(nowUtc, expiryUtc);
        }

        internal static bool HasVerifiedReleaseCheckpoints(MissionAcgExpiryState state)
        {
            return state != null
                   && state.HasCheckpoint(MissionAcgExpiryCheckpoint.BindingReleaseReady)
                   && state.HasCheckpoint(
                       MissionAcgExpiryCheckpoint.ObjectiveCorpseCleanupVerified)
                   && state.HasCheckpoint(MissionAcgExpiryCheckpoint.OccupantsEvacuated)
                   && state.HasCheckpoint(MissionAcgExpiryCheckpoint.InventoryArtifactsRemoved)
                   && state.HasCheckpoint(MissionAcgExpiryCheckpoint.RuntimeRegistrationsRemoved);
        }

        internal static bool CanReleasePlayfield(
            MissionAcgExpiryState state,
            bool noOccupants,
            bool noResidualContent,
            bool exactOwnershipVerified)
        {
            return HasVerifiedReleaseCheckpoints(state)
                   && !state.HasCheckpoint(MissionAcgExpiryCheckpoint.Pf2ReleaseConfirmed)
                   && state.Status != MissionAcgExpiryStatus.TerminalFailure
                   && noOccupants
                   && noResidualContent
                   && exactOwnershipVerified;
        }

        internal static bool CanConfirmPreviouslyReleasedPlayfield(
            MissionAcgExpiryState state,
            bool noOccupants,
            bool noResidualContent,
            bool anyReservationExists,
            bool anyLiveBindingExists)
        {
            return HasVerifiedReleaseCheckpoints(state)
                   && state.HasCheckpoint(
                       MissionAcgExpiryCheckpoint.Pf2ReleaseAttempted)
                   && !state.HasCheckpoint(
                       MissionAcgExpiryCheckpoint.Pf2ReleaseConfirmed)
                   && state.Status != MissionAcgExpiryStatus.TerminalFailure
                   && noOccupants
                   && noResidualContent
                   && !anyReservationExists
                   && !anyLiveBindingExists;
        }

        private static DateTime RequireUtc(DateTime value, string parameterName)
        {
            if (value == DateTime.MinValue)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }
    }
}
