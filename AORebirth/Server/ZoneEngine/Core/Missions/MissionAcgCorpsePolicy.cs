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
            return MissionAcgCorpseCreditPolicy.TryResolve(runtimeNpcInstance, allocatedLivePlayfield2, out credits);
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
            return MissionAcgCorpseCreditPolicy.TryResolveSignedSalt(left, right, multiplier, out salt);
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

        internal static bool IsPersistedKillDeathWitnessEligible(
            MissionAcgBindingRecord binding,
            MissionAcgOperationalState operational,
            MissionAcgObjectiveRecord objective,
            MissionAcgNpcRuntimeState exactTarget)
        {
            if (binding == null
                || binding.Binding == null
                || binding.State == null
                || operational == null
                || objective == null
                || exactTarget == null
                || binding.Binding.MissionType != MissionRollType.KillPerson
                || objective.Binding.MissionType != MissionRollType.KillPerson
                || objective.Binding.RequiredInteraction
                   != MissionAcgObjectiveInteraction.TargetDeath
                || objective.State.Lifecycle != MissionAcgObjectiveLifecycle.Exposed
                || objective.State.Phase >= MissionAcgCompletionPhase.ObjectiveVerified
                || binding.Binding.TeamIdentity != null
                || !binding.Binding.ExplicitNoTeam
                || objective.Binding.TeamIdentity != null
                || !objective.Binding.ExplicitNoTeam
                || binding.State.LifecycleState == MissionAcgLifecycleState.Abandoned
                || binding.State.LifecycleState == MissionAcgLifecycleState.Expired
                || binding.State.LifecycleState == MissionAcgLifecycleState.CleanupPending
                || binding.State.LifecycleState == MissionAcgLifecycleState.Cleaned
                || binding.State.LifecycleState == MissionAcgLifecycleState.Invalid
                || binding.State.CleanupState != MissionAcgCleanupState.None
                || operational.CleanupState != MissionAcgOperationalCleanupState.Active)
            {
                return false;
            }

            MissionAcgInstanceBinding instance = binding.Binding;
            MissionAcgObjectiveBinding objectiveBinding = objective.Binding;
            return operational.AcceptedQuestIdentity.Equals(
                       instance.AcceptedQuestIdentity)
                   && operational.OwnerIdentity.Equals(instance.OwnerIdentity)
                   && operational.AllocatedLivePlayfield2
                      == instance.AllocatedLivePlayfield2
                   && string.Equals(
                       operational.BundleId,
                       instance.SelectedBundleId,
                       StringComparison.Ordinal)
                   && string.Equals(
                       operational.BundlePayloadSha256,
                       instance.SelectedBundlePayloadSha256,
                       StringComparison.OrdinalIgnoreCase)
                   && operational.BuildingIdentity.Equals(instance.AcgBuildingIdentity)
                   && objectiveBinding.AcceptedQuestIdentity.Equals(
                       instance.AcceptedQuestIdentity)
                   && objectiveBinding.OwnerIdentity.Equals(instance.OwnerIdentity)
                   && objectiveBinding.AllocatedLivePlayfield2
                      == instance.AllocatedLivePlayfield2
                   && string.Equals(
                       objectiveBinding.BundleId,
                       instance.SelectedBundleId,
                       StringComparison.Ordinal)
                   && string.Equals(
                       objectiveBinding.BundlePayloadSha256,
                       instance.SelectedBundlePayloadSha256,
                       StringComparison.OrdinalIgnoreCase)
                   && objectiveBinding.BuildingIdentity.Equals(instance.AcgBuildingIdentity)
                   && exactTarget.CapturedSlot == objectiveBinding.CapturedObjectiveSlot
                   && exactTarget.CapturedIdentity.Equals(
                       objectiveBinding.CapturedObjectiveIdentity)
                   && exactTarget.RuntimeIdentity.Equals(
                       objectiveBinding.RuntimeObjectiveIdentity)
                   && exactTarget.TemplateId == objectiveBinding.ObjectiveTemplateId
                   && string.Equals(
                       exactTarget.Name,
                       objectiveBinding.ObjectiveName,
                       StringComparison.Ordinal)
                   && exactTarget.Role == MissionAcgNpcRole.KillTarget
                   && exactTarget.LifeState == MissionAcgNpcLifeState.Dead
                   && exactTarget.CurrentHealth == 0
                   && !exactTarget.CleanupCompleted
                   && exactTarget.DeathHookCheckpoint
                      >= MissionAcgNpcDeathHookCheckpoint.DeathPersisted
                   && exactTarget.DiedAtUtc.HasValue
                   && exactTarget.DeathSpawnGeneration == exactTarget.SpawnGeneration
                   && exactTarget.DeathCreditedAttackerIdentity != null
                   && exactTarget.DeathCreditedOwnerIdentity != null
                   && exactTarget.DeathCreditedOwnerIdentity.Equals(instance.OwnerIdentity)
                   && exactTarget.DeathCreditedAttackerIdentity.Equals(instance.OwnerIdentity);
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
