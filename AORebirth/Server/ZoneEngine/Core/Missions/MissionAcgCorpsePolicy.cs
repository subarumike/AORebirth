namespace ZoneEngine.Core.Missions
{
    using System;

    /// <summary>
    /// Capture-backed generated-mission corpse credit and access policy.
    ///
    /// The inclusive 21-87 values are corpse credit currency amounts observed in
    /// mission capture 20260725-185432. They are not damage, distance, or objective
    /// contribution thresholds.
    /// </summary>
    internal static class MissionAcgCorpsePolicy
    {
        internal const int MinimumCapturedCorpseCredits = 21;

        internal const int MaximumCapturedCorpseCredits = 87;

        private const uint CapturedCorpseCreditValueCount =
            MaximumCapturedCorpseCredits - MinimumCapturedCorpseCredits + 1;

        internal static bool TryResolveCapturedCorpseCredits(
            int runtimeNpcInstance,
            int allocatedLivePlayfield2,
            out int credits)
        {
            credits = 0;
            int salt;
            int encodedPlayfield2;
            int ordinal;
            if (runtimeNpcInstance <= 0
                || !MissionAcgAllocationService.IsAllocatableRange(
                    allocatedLivePlayfield2)
                || !MissionAcgRuntimeMaterializer.TryReverseRuntimeInstance(
                    runtimeNpcInstance,
                    out encodedPlayfield2,
                    out ordinal)
                || encodedPlayfield2 != allocatedLivePlayfield2
                || ordinal <= 0
                || !TryResolveLegacySignedSalt(
                    runtimeNpcInstance,
                    allocatedLivePlayfield2,
                    131u,
                    out salt))
            {
                return false;
            }

            credits = MinimumCapturedCorpseCredits
                      + (int)(Magnitude(salt) % CapturedCorpseCreditValueCount);
            return true;
        }

        internal static bool IsCapturedCorpseCreditAmount(int credits)
        {
            return credits >= MinimumCapturedCorpseCredits
                   && credits <= MaximumCapturedCorpseCredits;
        }

        internal static bool TryResolveLegacySignedSalt(
            int left,
            int right,
            uint multiplier,
            out int salt)
        {
            salt = 0;
            if (left <= 0 || right < 0 || multiplier == 0)
            {
                return false;
            }

            // Preserve the established low-32-bit identity mix without ever
            // performing signed overflow. The historical int.MinValue result is
            // rejected because taking its signed absolute value was the crash.
            ulong product = (ulong)(uint)left * multiplier;
            uint lowBits = (uint)(product & uint.MaxValue);
            salt = unchecked((int)(lowBits ^ (uint)right));
            return salt != int.MinValue;
        }

        internal static int StableBucket(int salt, int bucketCount)
        {
            if (bucketCount <= 0)
            {
                throw new ArgumentOutOfRangeException("bucketCount");
            }

            return (int)(Magnitude(salt) % bucketCount);
        }

        private static long Magnitude(int value)
        {
            return value < 0 ? -(long)value : value;
        }

        internal static bool IsInteractionDistanceAllowed(
            double distance,
            double maximumDistance)
        {
            return IsFinite(distance)
                   && IsFinite(maximumDistance)
                   && distance >= 0.0
                   && maximumDistance >= 0.0
                   && distance <= maximumDistance;
        }

        internal static bool TryValidateAccess(
            MissionAcgOperationalState state,
            MissionAcgIdentityRecord registeredAcceptedQuest,
            MissionAcgIdentityRecord registeredOwner,
            int registeredPlayfield2,
            MissionAcgIdentityRecord registeredDeadNpc,
            MissionAcgIdentityRecord registeredCorpse,
            int looterInstance,
            bool bindingAccessible,
            bool requireInteractionDistance,
            double interactionDistance,
            double maximumInteractionDistance,
            out string failure)
        {
            failure = string.Empty;
            if (state == null
                || registeredAcceptedQuest == null
                || registeredOwner == null
                || registeredDeadNpc == null
                || registeredCorpse == null)
            {
                failure = "Generated-mission corpse ownership is incomplete.";
                return false;
            }

            if (!bindingAccessible
                || state.CleanupState != MissionAcgOperationalCleanupState.Active)
            {
                failure = "Generated-mission binding is not accessible.";
                return false;
            }

            if (!state.AcceptedQuestIdentity.Equals(registeredAcceptedQuest)
                || !state.OwnerIdentity.Equals(registeredOwner)
                || state.OwnerIdentity.Instance != looterInstance
                || state.AllocatedLivePlayfield2 != registeredPlayfield2)
            {
                failure = "Accepted quest, owner, or PF2 does not match.";
                return false;
            }

            MissionAcgNpcRuntimeState npc;
            if (!state.TryGetNpc(registeredDeadNpc.Instance, out npc)
                || !npc.RuntimeIdentity.Equals(registeredDeadNpc)
                || npc.LifeState != MissionAcgNpcLifeState.Dead
                || npc.CleanupCompleted
                || npc.CorpseState != MissionAcgCorpseState.Available
                || npc.CorpseIdentity == null
                || !npc.CorpseIdentity.Equals(registeredCorpse))
            {
                failure = "Runtime NPC death or corpse identity does not match.";
                return false;
            }

            if (requireInteractionDistance
                && !IsInteractionDistanceAllowed(
                    interactionDistance,
                    maximumInteractionDistance))
            {
                failure = "Corpse interaction distance is invalid.";
                return false;
            }

            return true;
        }

        internal static bool IsVerifiedKillDeathRecoveryEligible(
            MissionAcgObjectiveRecord objective,
            MissionAcgNpcRuntimeState exactTarget)
        {
            return objective != null
                   && exactTarget != null
                   && objective.Binding.MissionType == MissionRollType.KillPerson
                   && objective.State.Phase >= MissionAcgCompletionPhase.ObjectiveVerified
                   && exactTarget.RuntimeIdentity.Equals(
                       objective.Binding.RuntimeObjectiveIdentity)
                   && exactTarget.Role == MissionAcgNpcRole.KillTarget
                   && exactTarget.LifeState == MissionAcgNpcLifeState.Dead
                   && !exactTarget.CleanupCompleted;
        }

        internal static bool ShouldDeferKillCompletionCleanup(
            MissionAcgOperationalState state,
            MissionAcgObjectiveRecord objective,
            MissionAcgIdentityRecord acceptedQuestIdentity,
            int allocatedLivePlayfield2,
            bool hasExactLiveCorpseLease)
        {
            if (!hasExactLiveCorpseLease
                || state == null
                || objective == null
                || acceptedQuestIdentity == null
                || state.CleanupState != MissionAcgOperationalCleanupState.Active
                || !state.AcceptedQuestIdentity.Equals(acceptedQuestIdentity)
                || state.AllocatedLivePlayfield2 != allocatedLivePlayfield2
                || !objective.Binding.AcceptedQuestIdentity.Equals(
                    acceptedQuestIdentity)
                || objective.Binding.AllocatedLivePlayfield2
                   != allocatedLivePlayfield2
                || objective.State.Phase < MissionAcgCompletionPhase.QuestDeleteSent
                || objective.State.Phase
                   >= MissionAcgCompletionPhase.ObjectiveCleanupCompleted)
            {
                return false;
            }

            MissionAcgNpcRuntimeState exactTarget;
            if (!state.TryGetNpc(
                    objective.Binding.RuntimeObjectiveIdentity.Instance,
                    out exactTarget)
                || !IsVerifiedKillDeathRecoveryEligible(objective, exactTarget)
                || exactTarget.CorpseIdentity == null)
            {
                return false;
            }

            return exactTarget.CorpseState == MissionAcgCorpseState.Pending
                   || exactTarget.CorpseState == MissionAcgCorpseState.Available;
        }

        internal static bool IsBindingAccessibleForCorpse(
            bool ordinarilyAccessible,
            bool completionOwned,
            MissionAcgLifecycleState lifecycleState,
            MissionAcgCleanupState cleanupState,
            bool reservesPlayfield)
        {
            return ordinarilyAccessible
                   || (completionOwned
                       && reservesPlayfield
                       && lifecycleState
                          == MissionAcgLifecycleState.CompletionStarted
                       && cleanupState == MissionAcgCleanupState.None);
        }

        internal static bool ShouldResumeCompletionAfterCorpseRetirement(
            MissionAcgObjectiveRecord objective,
            MissionAcgIdentityRecord acceptedQuestIdentity,
            MissionAcgIdentityRecord ownerIdentity,
            int allocatedLivePlayfield2,
            MissionAcgIdentityRecord deadNpcIdentity)
        {
            return objective != null
                   && acceptedQuestIdentity != null
                   && ownerIdentity != null
                   && deadNpcIdentity != null
                   && objective.Binding.MissionType == MissionRollType.KillPerson
                   && objective.Binding.AcceptedQuestIdentity.Equals(
                       acceptedQuestIdentity)
                   && objective.Binding.OwnerIdentity.Equals(ownerIdentity)
                   && objective.Binding.AllocatedLivePlayfield2
                      == allocatedLivePlayfield2
                   && objective.Binding.RuntimeObjectiveIdentity.Equals(
                       deadNpcIdentity)
                   && objective.State.Phase
                      >= MissionAcgCompletionPhase.ObjectiveVerified
                   && MissionAcgLifecyclePolicy.IsCompletionResumeEligible(
                       objective.State);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
