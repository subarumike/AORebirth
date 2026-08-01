namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Durable, ordered completion executor for generated ACG missions. Each server-controlled phase
    /// is persisted before the next effect. Pending grant states are fail-closed because the legacy
    /// character/inventory stores expose no transactional acknowledgement token.
    /// </summary>
    internal static class MissionAcgCompletionJournalService
    {
        private static readonly object Gate = new object();

        private static readonly HashSet<int> InFlight = new HashSet<int>();

        private static readonly Dictionary<int, object> OwnerCompletionGates =
            new Dictionary<int, object>();

        internal static void ResumeForCharacter(
            IZoneClient client,
            ICharacter character)
        {
            if (client == null || character == null)
            {
                return;
            }

            IList<MissionAcgObjectiveRecord> work =
                MissionAcgObjectiveRuntime.GetOwnedCompletionWork(
                    character.Identity.Instance);
            for (int i = 0; i < work.Count; i++)
            {
                MissionAcgObjectiveRecord objective = work[i];
                MissionAcgBindingRecord binding;
                if (!MissionAcgBindingRuntime.TryGetOwnedByAcceptedQuest(
                    character.Identity.Instance,
                    objective.Binding.AcceptedQuestIdentity.Instance,
                    out binding))
                {
                    continue;
                }

                MissionAcceptedStore.AcceptedMission accepted;
                TryResolveAcceptedMission(
                    character,
                    objective.Binding.AcceptedQuestIdentity,
                    out accepted);
                TryCompleteVerified(
                    client,
                    character,
                    accepted,
                    binding,
                    objective,
                    "RestartResume");
            }
        }

        internal static bool ResumeForAccepted(
            IZoneClient client,
            ICharacter character,
            int acceptedQuestInstance)
        {
            if (client == null || character == null || acceptedQuestInstance <= 0)
            {
                return false;
            }

            MissionAcgObjectiveRecord objective;
            MissionAcgBindingRecord binding;
            if (!MissionAcgObjectiveRuntime.TryGetByAccepted(
                    character.Identity.Instance,
                    acceptedQuestInstance,
                    out objective)
                || !MissionAcgLifecyclePolicy.IsCompletionResumeEligible(
                    objective.State)
                || !MissionAcgBindingRuntime.TryGetOwnedByAcceptedQuest(
                    character.Identity.Instance,
                    acceptedQuestInstance,
                    out binding))
            {
                return false;
            }

            MissionAcceptedStore.AcceptedMission accepted;
            TryResolveAcceptedMission(
                character,
                objective.Binding.AcceptedQuestIdentity,
                out accepted);
            return TryCompleteVerified(
                client,
                character,
                accepted,
                binding,
                objective,
                "ExactCorpseRetired");
        }

        internal static bool TryVerifyAndComplete(
            IZoneClient client,
            ICharacter character,
            MissionAcgBindingRecord bindingRecord,
            MissionAcgObjectiveRecord objectiveRecord,
            MissionAcgObjectiveEvent observation,
            string reason)
        {
            string failure;
            MissionAcgBindingRecord claimedBinding;
            MissionAcgObjectiveRecord claimedObjective;
            if (!TryPersistObjectiveVerification(
                    bindingRecord,
                    objectiveRecord,
                    observation,
                    out claimedBinding,
                    out claimedObjective,
                    out failure))
            {
                MissionDiagnostics.Log(
                    "ACG-OBJECTIVE-REJECT char={0} accepted={1}:{2} livePf2={3} reason={4}",
                    character == null ? 0 : character.Identity.Instance,
                    objectiveRecord == null ? 0 : objectiveRecord.Binding.AcceptedQuestIdentity.Type,
                    objectiveRecord == null ? 0 : objectiveRecord.Binding.AcceptedQuestIdentity.Instance,
                    objectiveRecord == null ? 0 : objectiveRecord.Binding.AllocatedLivePlayfield2,
                    failure);
                return false;
            }

            MissionAcceptedStore.AcceptedMission accepted;
            if (!TryResolveAcceptedMission(
                character,
                claimedBinding.Binding.AcceptedQuestIdentity,
                out accepted))
            {
                return false;
            }

            return TryCompleteVerified(
                client,
                character,
                accepted,
                claimedBinding,
                claimedObjective,
                reason);
        }

        internal static bool TryPersistObjectiveVerification(
            MissionAcgBindingRecord bindingRecord,
            MissionAcgObjectiveRecord objectiveRecord,
            MissionAcgObjectiveEvent observation,
            out MissionAcgBindingRecord claimedBinding,
            out MissionAcgObjectiveRecord verified,
            out string failure)
        {
            claimedBinding = null;
            verified = null;
            failure = string.Empty;
            MissionAcgObjectiveRecord claimedObjective;
            if (!MissionAcgExpiryRuntime.TryClaimObjectiveVerification(
                    bindingRecord,
                    objectiveRecord,
                    out claimedBinding,
                    out claimedObjective,
                    out failure))
            {
                return false;
            }

            int acceptedInstance =
                claimedBinding.Binding.AcceptedQuestIdentity.Instance;
            try
            {
                return MissionAcgObjectiveContract.TryVerify(
                           claimedObjective,
                           observation,
                           out failure)
                       && MissionTokenProgressTracker.SealGeneratedProgress(
                           claimedBinding,
                           claimedObjective,
                           out failure)
                       && MissionAcgObjectiveRuntime.TryReplaceState(
                           claimedObjective,
                           claimedObjective.State.Copy(
                               lifecycle: MissionAcgObjectiveLifecycle.Verified,
                               phase: MissionAcgCompletionPhase.ObjectiveVerified),
                           out verified,
                           out failure);
            }
            finally
            {
                MissionAcgExpiryRuntime.ReleaseObjectiveVerificationClaim(
                    acceptedInstance);
            }
        }

        internal static bool TryCompleteVerified(
            IZoneClient client,
            ICharacter character,
            MissionAcceptedStore.AcceptedMission accepted,
            MissionAcgBindingRecord bindingRecord,
            MissionAcgObjectiveRecord objectiveRecord,
            string reason)
        {
            if (client == null
                || character == null
                || bindingRecord == null
                || objectiveRecord == null
                || objectiveRecord.State.Phase
                   < MissionAcgCompletionPhase.ObjectiveVerified)
            {
                return false;
            }

            string expiryFailure;
            if (!MissionAcgExpiryRuntime.CanContinueCompletion(
                bindingRecord,
                objectiveRecord,
                DateTime.UtcNow,
                out expiryFailure))
            {
                return false;
            }

            int acceptedInstance =
                bindingRecord.Binding.AcceptedQuestIdentity.Instance;
            lock (Gate)
            {
                if (!InFlight.Add(acceptedInstance))
                {
                    return false;
                }
            }

            try
            {
                object ownerGate;
                lock (Gate)
                {
                    if (!OwnerCompletionGates.TryGetValue(
                        character.Identity.Instance,
                        out ownerGate))
                    {
                        ownerGate = new object();
                        OwnerCompletionGates.Add(
                            character.Identity.Instance,
                            ownerGate);
                    }
                }

                lock (ownerGate)
                {
                    return Continue(
                        client,
                        character,
                        accepted,
                        bindingRecord,
                        objectiveRecord,
                        reason);
                }
            }
            finally
            {
                lock (Gate)
                {
                    InFlight.Remove(acceptedInstance);
                }
            }
        }

        private static bool Continue(
            IZoneClient client,
            ICharacter character,
            MissionAcceptedStore.AcceptedMission accepted,
            MissionAcgBindingRecord bindingRecord,
            MissionAcgObjectiveRecord objectiveRecord,
            string reason)
        {
            string failure;
            MissionAcgBindingRecord binding = bindingRecord;
            MissionAcgObjectiveRecord objective = objectiveRecord;
            if (!MissionAcgExpiryRuntime.CanContinueCompletion(
                binding,
                objective,
                DateTime.UtcNow,
                out failure))
            {
                return false;
            }

            if (objective.State.Phase == MissionAcgCompletionPhase.ObjectiveVerified)
            {
                int acceptedInstance =
                    binding.Binding.AcceptedQuestIdentity.Instance;
                if (!MissionAcgExpiryRuntime.TryClaimCompletionTransition(
                    binding,
                    objective,
                    out binding,
                    out failure))
                {
                    return false;
                }

                try
                {
                    bool bindingTransitionPersisted =
                        binding.State.LifecycleState
                        == MissionAcgLifecycleState.CompletionStarted;
                    if (!MissionTokenProgressTracker.SealGeneratedProgress(
                            binding,
                            objective,
                            out failure)
                        || (!bindingTransitionPersisted
                         && !MissionAcgBindingRuntime.TryTransition(
                             binding,
                             MissionAcgLifecycleState.CompletionStarted,
                             MissionAcgCleanupState.None,
                             DateTime.UtcNow,
                             out binding,
                             out failure))
                        || !Replace(
                            objective,
                            objective.State.Copy(
                                lifecycle:
                                    MissionAcgObjectiveLifecycle.CompletionStarted,
                                phase:
                                    MissionAcgCompletionPhase.CompletionStarted),
                            out objective,
                            out failure))
                    {
                        return false;
                    }
                }
                finally
                {
                    MissionAcgExpiryRuntime.ReleaseCompletionTransitionClaim(
                        acceptedInstance);
                }
            }

            if (objective.State.Phase == MissionAcgCompletionPhase.CompletionStarted)
            {
                if (!MissionAcgExpiryRuntime.CanContinueCompletion(
                    binding,
                    objective,
                    DateTime.UtcNow,
                    out failure))
                {
                    return false;
                }

                MissionAcgObjectiveState frozen;
                if (!TryFreezeAcceptedRewards(
                    character,
                    accepted,
                    binding,
                    objective,
                    out frozen,
                    out failure))
                {
                    MissionDiagnostics.Log(
                        "ACG-COMPLETE-FROZEN-REJECT accepted={0}:{1} reason={2}",
                        binding.Binding.AcceptedQuestIdentity.Type,
                        binding.Binding.AcceptedQuestIdentity.Instance,
                        failure);
                    return false;
                }

                if (!Replace(objective, frozen, out objective, out failure))
                {
                    return false;
                }
            }

            if (objective.State.Phase == MissionAcgCompletionPhase.RewardCalculationFrozen)
            {
                int acceptedInstance =
                    binding.Binding.AcceptedQuestIdentity.Instance;
                if (!MissionAcgExpiryRuntime.TryClaimCompletionReward(
                    binding,
                    objective,
                    out failure))
                {
                    return false;
                }

                if (!Replace(
                    objective,
                    objective.State.Copy(
                        phase: MissionAcgCompletionPhase.RewardClaimStarted),
                    out objective,
                    out failure))
                {
                    MissionAcgExpiryRuntime.ReleaseCompletionRewardClaim(
                        acceptedInstance);
                    return false;
                }

                MissionAcgExpiryRuntime.ConfirmCompletionRewardClaim(
                    acceptedInstance);
            }

            if (!ProcessCredits(character, objective, out objective, out failure)
                || !ProcessXp(character, objective, out objective, out failure)
                || !ProcessInventoryClaim(
                    client,
                    character,
                    binding.Binding,
                    objective,
                    false,
                    out objective,
                    out failure)
                || !ProcessInventoryClaim(
                    client,
                    character,
                    binding.Binding,
                    objective,
                    true,
                    out objective,
                    out failure)
                || !DeliverRewardNotifications(
                    character,
                    objective,
                    out objective,
                    out failure)
                || !DeliverMissionNotifications(
                    character,
                    binding.Binding,
                    objective,
                    out objective,
                    out failure))
            {
                return false;
            }

            if (objective.State.MissionListRemovalDelivery
                == MissionAcgDeliveryPhase.NotStarted)
            {
                if (!Replace(
                    objective,
                    objective.State.Copy(
                        missionListRemovalDelivery: MissionAcgDeliveryPhase.Pending),
                    out objective,
                    out failure))
                {
                    return false;
                }
            }

            if (objective.State.MissionListRemovalDelivery
                == MissionAcgDeliveryPhase.Pending)
            {
                if (!MissionAcceptedStore.TryRemoveExactPersisted(
                    character.Identity.Instance,
                    ToIdentity(binding.Binding.AcceptedQuestIdentity),
                    out failure)
                    || !Replace(
                        objective,
                        objective.State.Copy(
                            missionListRemovalDelivery: MissionAcgDeliveryPhase.Sent),
                        out objective,
                        out failure))
                {
                    return false;
                }
            }

            if (!objective.State.ArtifactsRemoved)
            {
                if (!RemoveExactArtifacts(
                    client,
                    character,
                    binding.Binding,
                    objective,
                    out failure)
                    || !Replace(
                        objective,
                        objective.State.Copy(
                            artifactsRemoved: true,
                            phase: MissionAcgCompletionPhase.MissionArtifactsRemoved),
                        out objective,
                        out failure))
                {
                    return false;
                }
            }

            if (objective.State.Phase < MissionAcgCompletionPhase.Action59Sent
                && !Replace(
                    objective,
                    objective.State.Copy(phase: MissionAcgCompletionPhase.Action59Sent),
                    out objective,
                    out failure))
            {
                return false;
            }

            if (objective.State.Phase < MissionAcgCompletionPhase.QuestDeleteSent
                && !Replace(
                    objective,
                    objective.State.Copy(phase: MissionAcgCompletionPhase.QuestDeleteSent),
                    out objective,
                    out failure))
            {
                return false;
            }

            if (!objective.State.ObjectiveCleanupCompleted
                && MissionAcgOperationalRuntime.ShouldDeferKillCompletionCleanup(
                    binding,
                    objective))
            {
                MissionDiagnostics.Log(
                    "ACG-COMPLETE-DEFER-CORPSE char={0} accepted={1}:{2} livePf2={3} phase={4}",
                    character.Identity.Instance,
                    binding.Binding.AcceptedQuestIdentity.Type,
                    binding.Binding.AcceptedQuestIdentity.Instance,
                    binding.Binding.AllocatedLivePlayfield2,
                    objective.State.Phase);
                return true;
            }

            if (!objective.State.ObjectiveCleanupCompleted)
            {
                if (objective.State.CleanupHandoffDelivery
                    == MissionAcgDeliveryPhase.NotStarted)
                {
                    if (!Replace(
                        objective,
                        objective.State.Copy(
                            cleanupHandoffDelivery: MissionAcgDeliveryPhase.Pending),
                        out objective,
                        out failure))
                    {
                        return false;
                    }
                }

                if (!MissionAcgRuntimeManager.Cleanup(binding, out failure)
                    || !Replace(
                        objective,
                        objective.State.Copy(
                            objectiveCleanupCompleted: true,
                            phase: MissionAcgCompletionPhase.ObjectiveCleanupCompleted,
                            cleanupHandoffDelivery: MissionAcgDeliveryPhase.Sent),
                        out objective,
                        out failure))
                {
                    return false;
                }
            }

            if (binding.State.LifecycleState
                == MissionAcgLifecycleState.CompletionStarted)
            {
                if (!MissionAcgBindingRuntime.TryTransition(
                    binding,
                    MissionAcgLifecycleState.Completed,
                    MissionAcgCleanupState.KeyRemovalPending,
                    DateTime.UtcNow,
                    out binding,
                    out failure))
                {
                    return false;
                }
            }

            if (binding.State.LifecycleState == MissionAcgLifecycleState.Completed)
            {
                if (!MissionAcgBindingRuntime.TryTransition(
                    binding,
                    MissionAcgLifecycleState.CleanupPending,
                    MissionAcgCleanupState.InstanceReleasePending,
                    DateTime.UtcNow,
                    out binding,
                    out failure))
                {
                    return false;
                }
            }

            if (!MissionAcgBindingRuntime.TryCompleteRuntimeCleanup(
                binding,
                out failure))
            {
                return false;
            }

            if (!objective.State.MissionCleanupCompleted
                && !Replace(
                    objective,
                    objective.State.Copy(
                        lifecycle: MissionAcgObjectiveLifecycle.CleanupCompleted,
                        missionCleanupCompleted: true,
                        phase: MissionAcgCompletionPhase.MissionCleanupCompleted),
                    out objective,
                    out failure))
            {
                return false;
            }

            if (binding.State.LifecycleState
                == MissionAcgLifecycleState.CleanupPending)
            {
                if (!MissionAcgBindingRuntime.TryTransition(
                    binding,
                    MissionAcgLifecycleState.Cleaned,
                    MissionAcgCleanupState.Completed,
                    DateTime.UtcNow,
                    out binding,
                    out failure))
                {
                    return false;
                }
            }

            if (!MissionAcgBindingRuntime.TryReleaseAfterDurableCleanup(
                binding,
                objective,
                out failure))
            {
                return false;
            }

            MissionDiagnostics.Log(
                "ACG-COMPLETE char={0} accepted={1}:{2} livePf2={3} reason={4} credits={5} xp={6} item={7}",
                character.Identity.Instance,
                binding.Binding.AcceptedQuestIdentity.Type,
                binding.Binding.AcceptedQuestIdentity.Instance,
                binding.Binding.AllocatedLivePlayfield2,
                reason ?? string.Empty,
                objective.State.FrozenCredits,
                objective.State.FrozenXp,
                objective.State.GrantedRewardItemInstance);
            return true;
        }

        private static bool Replace(
            MissionAcgObjectiveRecord record,
            MissionAcgObjectiveState state,
            out MissionAcgObjectiveRecord updated,
            out string failure)
        {
            return MissionAcgObjectiveRuntime.TryReplaceState(
                record,
                state,
                out updated,
                out failure);
        }

        private static bool TryFreezeAcceptedRewards(
            ICharacter character,
            MissionAcceptedStore.AcceptedMission accepted,
            MissionAcgBindingRecord binding,
            MissionAcgObjectiveRecord objective,
            out MissionAcgObjectiveState frozen,
            out string failure)
        {
            frozen = null;
            failure = string.Empty;
            if (character == null
                || accepted == null
                || !accepted.HasFrozenAcceptedRewards
                || accepted.Projection == null
                || accepted.QuestIdentity == null
                || accepted.QuestIdentity.Instance
                   != binding.Binding.AcceptedQuestIdentity.Instance
                || accepted.Projection.Binding == null
                || !accepted.Projection.Binding.AcceptedQuestIdentity.Equals(
                    binding.Binding.AcceptedQuestIdentity)
                || !accepted.Projection.Binding.OwnerIdentity.Equals(
                    binding.Binding.OwnerIdentity)
                || accepted.Projection.Binding.AllocatedLivePlayfield2
                   != binding.Binding.AllocatedLivePlayfield2
                || accepted.CashReward < 0
                || accepted.ExperienceReward < 0
                || accepted.FrozenItemRewardCount < 0
                || (accepted.FrozenItemRewardCount > 0
                    && (accepted.FrozenItemRewardLowId <= 0
                        || accepted.FrozenItemRewardHighId <= 0
                        || accepted.FrozenItemRewardQuality <= 0)))
            {
                failure =
                    "Complete exact frozen accepted projection is required before any reward claim.";
                return false;
            }

            string claimBase =
                binding.Binding.AcceptedQuestIdentity.Type
                + "-"
                + binding.Binding.AcceptedQuestIdentity.Instance;
            MissionAcgDurableRewardClaim credits =
                FrozenScalarClaim(
                    claimBase + "-credits",
                    accepted.CashReward);
            MissionAcgDurableRewardClaim xp =
                FrozenScalarClaim(
                    claimBase + "-xp",
                    accepted.ExperienceReward);
            MissionAcgDurableRewardClaim item =
                accepted.FrozenItemRewardCount > 0
                    ? new MissionAcgDurableRewardClaim(
                        MissionAcgDurableClaimPhase.EligibleFrozen,
                        claimBase + "-item",
                        0,
                        accepted.FrozenItemRewardLowId,
                        accepted.FrozenItemRewardHighId,
                        accepted.FrozenItemRewardQuality,
                        accepted.FrozenItemRewardCount,
                        null,
                        null,
                        0,
                        0,
                        string.Empty,
                        string.Empty)
                    : MissionAcgDurableRewardClaim.Empty(
                        MissionAcgDurableClaimPhase.NotEligible);

            MissionAcgTokenProgressState progress;
            MissionAcgTokenClaimResolution tokenResolution;
            if (!MissionAcgTokenProgressRuntime.TryGetSealedProgress(
                    binding,
                    objective,
                    out progress,
                    out failure)
                || !MissionAcgTokenClaimPolicy.TryResolve(
                    progress,
                    (int)character.Stats[StatIds.level].Value,
                    (Side)character.Stats[StatIds.side].Value,
                    out tokenResolution,
                    out failure))
            {
                return false;
            }

            MissionAcgDurableRewardClaim token;
            if (tokenResolution.IsEligible)
            {
                token = new MissionAcgDurableRewardClaim(
                    MissionAcgDurableClaimPhase.EligibleFrozen,
                    claimBase + "-token",
                    tokenResolution.TokenCount,
                    tokenResolution.TokenLowId,
                    tokenResolution.TokenHighId,
                    tokenResolution.TokenQuality,
                    tokenResolution.TokenCount,
                    null,
                    null,
                    (int)character.Stats[StatIds.level].Value,
                    tokenResolution.Percent,
                    "token-progress-percent=" + tokenResolution.Percent,
                    string.Empty);
            }
            else
            {
                token = new MissionAcgDurableRewardClaim(
                    MissionAcgDurableClaimPhase.NotEligible,
                    claimBase + "-token",
                    0,
                    0,
                    0,
                    0,
                    0,
                    null,
                    null,
                    (int)character.Stats[StatIds.level].Value,
                    tokenResolution.Percent,
                    "token-progress-percent=" + tokenResolution.Percent,
                    tokenResolution.IsExplicitNone
                        ? string.Empty
                        : "Official probability below exact 100 percent is unresolved; no claim was created.");
            }

            frozen = objective.State.Copy(
                phase: MissionAcgCompletionPhase.RewardCalculationFrozen,
                frozenCredits: accepted.CashReward,
                frozenXp: accepted.ExperienceReward,
                frozenItemLowId: accepted.FrozenItemRewardLowId,
                frozenItemHighId: accepted.FrozenItemRewardHighId,
                frozenItemQuality: accepted.FrozenItemRewardQuality,
                frozenItemCount: accepted.FrozenItemRewardCount,
                creditsState:
                    accepted.CashReward > 0
                        ? MissionAcgGrantState.NotStarted
                        : MissionAcgGrantState.ExplicitNone,
                xpState:
                    accepted.ExperienceReward > 0
                        ? MissionAcgGrantState.NotStarted
                        : MissionAcgGrantState.ExplicitNone,
                itemState:
                    accepted.FrozenItemRewardCount > 0
                        ? MissionAcgGrantState.NotStarted
                        : MissionAcgGrantState.ExplicitNone,
                creditsClaimId: claimBase + "-credits",
                xpClaimId: claimBase + "-xp",
                itemClaimId: claimBase + "-item",
                creditsClaim: credits,
                xpClaim: xp,
                itemClaim: item,
                tokenClaim: token);
            return true;
        }

        private static MissionAcgDurableRewardClaim FrozenScalarClaim(
            string claimId,
            int amount)
        {
            return amount > 0
                       ? new MissionAcgDurableRewardClaim(
                           MissionAcgDurableClaimPhase.EligibleFrozen,
                           claimId,
                           amount,
                           0,
                           0,
                           0,
                           0,
                           null,
                           null,
                           0,
                           0,
                           string.Empty,
                           string.Empty)
                       : MissionAcgDurableRewardClaim.Empty(
                           MissionAcgDurableClaimPhase.NotEligible);
        }

        private static bool ProcessCredits(
            ICharacter character,
            MissionAcgObjectiveRecord source,
            out MissionAcgObjectiveRecord updated,
            out string failure)
        {
            updated = source;
            failure = string.Empty;
            MissionAcgDurableRewardClaim claim = updated.State.CreditsClaim;
            bool applicationWasAlreadyPending =
                claim.Phase == MissionAcgDurableClaimPhase.ApplicationPending;
            if (claim.Phase == MissionAcgDurableClaimPhase.NotEligible)
            {
                return AdvanceRewardPhase(
                    updated,
                    MissionAcgCompletionPhase.CreditsGranted,
                    out updated,
                    out failure);
            }

            if (claim.Phase == MissionAcgDurableClaimPhase.EligibleFrozen)
            {
                long before = MissionCompleteService.GetCashBalance(character);
                if (before < 0 || claim.Amount > int.MaxValue - before)
                {
                    return FailClaim(
                        updated,
                        false,
                        false,
                        false,
                        "Frozen credit reward cannot be applied in full without exceeding the production balance bound.",
                        out updated,
                        out failure);
                }

                long after = before + claim.Amount;
                if (!Replace(
                        updated,
                        updated.State.Copy(
                            creditsClaim: claim.Copy(
                                phase: MissionAcgDurableClaimPhase.ClaimReserved,
                                preApplyValue: before,
                                expectedPostValue: after)),
                        out updated,
                        out failure))
                {
                    return false;
                }

                claim = updated.State.CreditsClaim;
            }

            if (claim.Phase == MissionAcgDurableClaimPhase.ClaimReserved)
            {
                if (!Replace(
                    updated,
                    updated.State.Copy(
                        creditsState: MissionAcgGrantState.Pending,
                        creditsClaim: claim.Copy(
                            phase: MissionAcgDurableClaimPhase.ApplicationPending)),
                    out updated,
                    out failure))
                {
                    return false;
                }

                claim = updated.State.CreditsClaim;
            }

            if (claim.Phase == MissionAcgDurableClaimPhase.ApplicationPending)
            {
                long current = MissionCompleteService.GetCashBalance(character);
                if (current == claim.PreApplyValue)
                {
                    if (!MissionCompleteService.TryPersistFrozenCashTarget(
                        character,
                        claim.PreApplyValue,
                        claim.ExpectedPostValue,
                        out failure))
                    {
                        return FailClaim(
                            updated,
                            false,
                            false,
                            false,
                            failure,
                            out updated,
                            out failure);
                    }
                }
                else if (current == claim.ExpectedPostValue
                         && applicationWasAlreadyPending)
                {
                    return FailClaim(
                        updated,
                        false,
                        false,
                        false,
                        "Credit application is ambiguous after restart because the production cash owner has no durable claim identity.",
                        out updated,
                        out failure);
                }
                else
                {
                    return FailClaim(
                        updated,
                        false,
                        false,
                        false,
                        "Credit application is ambiguous because the balance matches neither reserved boundary.",
                        out updated,
                        out failure);
                }

                if (!Replace(
                    updated,
                    updated.State.Copy(
                        creditsState: MissionAcgGrantState.Granted,
                        creditsClaim: claim.Copy(
                            phase: MissionAcgDurableClaimPhase.DurablyApplied),
                        phase: MissionAcgCompletionPhase.CreditsGranted),
                    out updated,
                    out failure))
                {
                    return false;
                }

                claim = updated.State.CreditsClaim;
            }

            if (claim.Phase == MissionAcgDurableClaimPhase.TerminalFailure)
            {
                failure = claim.Failure;
                return false;
            }

            return AdvanceRewardPhase(
                updated,
                MissionAcgCompletionPhase.CreditsGranted,
                out updated,
                out failure);
        }

        private static bool ProcessXp(
            ICharacter character,
            MissionAcgObjectiveRecord source,
            out MissionAcgObjectiveRecord updated,
            out string failure)
        {
            updated = source;
            failure = string.Empty;
            MissionAcgDurableRewardClaim claim = updated.State.XpClaim;
            if (claim.Phase == MissionAcgDurableClaimPhase.NotEligible)
            {
                return AdvanceRewardPhase(
                    updated,
                    MissionAcgCompletionPhase.XpGranted,
                    out updated,
                    out failure);
            }

            if (claim.Phase == MissionAcgDurableClaimPhase.EligibleFrozen)
            {
                string fingerprint =
                    CombatXpRuntimeService.GetDirectXpClaimFingerprint(character);
                if (string.IsNullOrEmpty(fingerprint)
                    || !Replace(
                        updated,
                        updated.State.Copy(
                            xpClaim: claim.Copy(
                                phase: MissionAcgDurableClaimPhase.ClaimReserved,
                                preApplyFingerprint: fingerprint)),
                        out updated,
                        out failure))
                {
                    return false;
                }

                claim = updated.State.XpClaim;
            }

            if (claim.Phase == MissionAcgDurableClaimPhase.ClaimReserved)
            {
                if (!Replace(
                    updated,
                    updated.State.Copy(
                        xpState: MissionAcgGrantState.Pending,
                        xpClaim: claim.Copy(
                            phase: MissionAcgDurableClaimPhase.ApplicationPending)),
                    out updated,
                    out failure))
                {
                    return false;
                }

                claim = updated.State.XpClaim;
            }

            if (claim.Phase == MissionAcgDurableClaimPhase.ApplicationPending)
            {
                string current =
                    CombatXpRuntimeService.GetDirectXpClaimFingerprint(character);
                if (!string.Equals(
                    current,
                    claim.PreApplyFingerprint,
                    StringComparison.Ordinal))
                {
                    return FailClaim(
                        updated,
                        true,
                        false,
                        false,
                        "XP application is ambiguous because the reserved pre-apply fingerprint changed.",
                        out updated,
                        out failure);
                }

                if (!CombatXpRuntimeService.AwardDirectXp(
                    character,
                    (int)claim.Amount,
                    "mission-claim-" + claim.ClaimId))
                {
                    string afterFailure =
                        CombatXpRuntimeService.GetDirectXpClaimFingerprint(character);
                    if (!string.Equals(
                        afterFailure,
                        claim.PreApplyFingerprint,
                        StringComparison.Ordinal))
                    {
                        return FailClaim(
                            updated,
                            true,
                            false,
                            false,
                            "XP owner returned failure after changing the reserved fingerprint.",
                            out updated,
                            out failure);
                    }

                    failure = "XP owner did not apply the frozen claim; retry remains safe.";
                    return false;
                }

                if (!Replace(
                    updated,
                    updated.State.Copy(
                        xpState: MissionAcgGrantState.Granted,
                        xpClaim: claim.Copy(
                            phase: MissionAcgDurableClaimPhase.DurablyApplied),
                        phase: MissionAcgCompletionPhase.XpGranted),
                    out updated,
                    out failure))
                {
                    return false;
                }

                claim = updated.State.XpClaim;
            }

            if (claim.Phase == MissionAcgDurableClaimPhase.TerminalFailure)
            {
                failure = claim.Failure;
                return false;
            }

            return AdvanceRewardPhase(
                updated,
                MissionAcgCompletionPhase.XpGranted,
                out updated,
                out failure);
        }

        private static bool ProcessInventoryClaim(
            IZoneClient client,
            ICharacter character,
            MissionAcgInstanceBinding binding,
            MissionAcgObjectiveRecord source,
            bool token,
            out MissionAcgObjectiveRecord updated,
            out string failure)
        {
            updated = source;
            failure = string.Empty;
            MissionAcgDurableRewardClaim claim =
                token ? updated.State.TokenClaim : updated.State.ItemClaim;
            if (claim.Phase == MissionAcgDurableClaimPhase.NotEligible)
            {
                return token
                           ? true
                           : AdvanceRewardPhase(
                               updated,
                               MissionAcgCompletionPhase.ItemRewardGrantedOrNone,
                               out updated,
                               out failure);
            }

            if (claim.Phase == MissionAcgDurableClaimPhase.EligibleFrozen)
            {
                if (character.BaseInventory == null)
                {
                    failure = "Owner inventory is unavailable for exact reward reservation.";
                    return false;
                }

                var reserved = new MissionAcgIdentityRecord(
                    MissionKeyGrantService.MissionKeyIdentityType,
                    token ? TokenItemInstance(binding) : RewardItemInstance(binding));
                var container = new MissionAcgIdentityRecord(
                    character.BaseInventory.StandardPage,
                    character.Identity.Instance);
                claim = claim.Copy(
                    phase: MissionAcgDurableClaimPhase.ClaimReserved,
                    reservedItemIdentity: reserved,
                    targetContainerIdentity: container);
                if (!Replace(
                    updated,
                    CopyInventoryClaim(updated.State, token, claim),
                    out updated,
                    out failure))
                {
                    return false;
                }
            }

            claim = token ? updated.State.TokenClaim : updated.State.ItemClaim;
            if (claim.Phase == MissionAcgDurableClaimPhase.ClaimReserved)
            {
                claim = claim.Copy(
                    phase: MissionAcgDurableClaimPhase.ApplicationPending);
                MissionAcgObjectiveState pending =
                    CopyInventoryClaim(updated.State, token, claim);
                if (!token)
                {
                    pending = pending.Copy(itemState: MissionAcgGrantState.Pending);
                }

                if (!Replace(updated, pending, out updated, out failure))
                {
                    return false;
                }
            }

            claim = token ? updated.State.TokenClaim : updated.State.ItemClaim;
            if (claim.Phase == MissionAcgDurableClaimPhase.ApplicationPending)
            {
                IItem existing;
                MissionReservedItemLookupResult lookup =
                    MissionKeyGrantService.InspectReservedNamedItem(
                        character,
                        claim.ReservedItemIdentity.Type,
                        claim.ReservedItemIdentity.Instance,
                        claim.ItemLowId,
                        claim.ItemHighId,
                        claim.ItemQuality,
                        claim.ItemCount,
                        out existing);
                if (lookup == MissionReservedItemLookupResult.Conflict)
                {
                    return FailClaim(
                        updated,
                        false,
                        !token,
                        token,
                        "Reserved reward item identity conflicts with existing inventory data.",
                        out updated,
                        out failure);
                }

                if (lookup == MissionReservedItemLookupResult.Missing)
                {
                    int granted;
                    InventoryError inventoryError;
                    if (!MissionKeyGrantService.TryGrantReservedNamedItem(
                        client,
                        character,
                        claim.ItemLowId,
                        claim.ItemHighId,
                        claim.ItemQuality,
                        token ? TokenName(claim.ItemLowId) : "Mission Reward",
                        claim.ReservedItemIdentity.Instance,
                        claim.ItemCount,
                        out granted,
                        out inventoryError))
                    {
                        if (inventoryError == InventoryError.InventoryIsFull)
                        {
                            failure =
                                "Inventory is full; exact reserved reward claim remains pending.";
                            return false;
                        }

                        return FailClaim(
                            updated,
                            false,
                            !token,
                            token,
                            "Exact reserved reward item grant failed: " + inventoryError,
                            out updated,
                            out failure);
                    }

                    lookup = MissionKeyGrantService.InspectReservedNamedItem(
                        character,
                        claim.ReservedItemIdentity.Type,
                        claim.ReservedItemIdentity.Instance,
                        claim.ItemLowId,
                        claim.ItemHighId,
                        claim.ItemQuality,
                        claim.ItemCount,
                        out existing);
                    if (lookup != MissionReservedItemLookupResult.Exact)
                    {
                        return FailClaim(
                            updated,
                            false,
                            !token,
                            token,
                            "Granted reward item could not be reconciled by exact identity.",
                            out updated,
                            out failure);
                    }
                }

                claim = claim.Copy(
                    phase: MissionAcgDurableClaimPhase.DurablyApplied);
                MissionAcgObjectiveState applied =
                    CopyInventoryClaim(updated.State, token, claim);
                if (!token)
                {
                    applied = applied.Copy(
                        itemState: MissionAcgGrantState.Granted,
                        phase: MissionAcgCompletionPhase.ItemRewardGrantedOrNone,
                        grantedRewardItemInstance:
                            claim.ReservedItemIdentity.Instance);
                }

                if (!Replace(updated, applied, out updated, out failure))
                {
                    return false;
                }
            }

            claim = token ? updated.State.TokenClaim : updated.State.ItemClaim;
            if (claim.Phase == MissionAcgDurableClaimPhase.TerminalFailure)
            {
                failure = claim.Failure;
                return false;
            }

            return token
                       ? true
                       : AdvanceRewardPhase(
                           updated,
                           MissionAcgCompletionPhase.ItemRewardGrantedOrNone,
                           out updated,
                           out failure);
        }

        private static bool DeliverRewardNotifications(
            ICharacter character,
            MissionAcgObjectiveRecord source,
            out MissionAcgObjectiveRecord updated,
            out string failure)
        {
            updated = source;
            failure = string.Empty;
            if (updated.State.RewardFeedbackDelivery
                == MissionAcgDeliveryPhase.NotStarted)
            {
                MissionAcgObjectiveState pending = updated.State.Copy(
                    rewardFeedbackDelivery: MissionAcgDeliveryPhase.Pending,
                    creditsClaim: NotificationPending(updated.State.CreditsClaim),
                    xpClaim: NotificationPending(updated.State.XpClaim));
                if (!Replace(updated, pending, out updated, out failure))
                {
                    return false;
                }
            }

            if (updated.State.RewardFeedbackDelivery
                == MissionAcgDeliveryPhase.Pending)
            {
                if (updated.State.CreditsClaim.Phase
                        == MissionAcgDurableClaimPhase.ClientNotificationPending
                    && updated.State.CreditsClaim.ExpectedPostValue > 0)
                {
                    MissionCompleteService.SendFrozenCashNotification(
                        character,
                        MissionCompleteService.GetCashBalance(character));
                }
                MissionCompleteService.SendRewardFeedback(
                    character,
                    updated.State.FrozenXp,
                    updated.State.FrozenCredits);
                if (!Replace(
                    updated,
                    updated.State.Copy(
                        rewardFeedbackDelivery: MissionAcgDeliveryPhase.Sent,
                        creditsClaim: NotificationSent(updated.State.CreditsClaim),
                        xpClaim: NotificationSent(updated.State.XpClaim)),
                    out updated,
                    out failure))
                {
                    return false;
                }
            }

            if (!DeliverInventoryNotification(
                character,
                updated,
                false,
                out updated,
                out failure)
                || !DeliverInventoryNotification(
                    character,
                    updated,
                    true,
                    out updated,
                    out failure))
            {
                return false;
            }

            if (updated.State.MissionAccomplishedDelivery
                == MissionAcgDeliveryPhase.NotStarted)
            {
                if (!Replace(
                    updated,
                    updated.State.Copy(
                        missionAccomplishedDelivery: MissionAcgDeliveryPhase.Pending),
                    out updated,
                    out failure))
                {
                    return false;
                }
            }

            if (updated.State.MissionAccomplishedDelivery
                == MissionAcgDeliveryPhase.Pending)
            {
                MissionCompleteService.SendMissionAccomplishedFeedback(character);
                if (!Replace(
                    updated,
                    updated.State.Copy(
                        missionAccomplishedDelivery: MissionAcgDeliveryPhase.Sent),
                    out updated,
                    out failure))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool DeliverInventoryNotification(
            ICharacter character,
            MissionAcgObjectiveRecord source,
            bool token,
            out MissionAcgObjectiveRecord updated,
            out string failure)
        {
            updated = source;
            failure = string.Empty;
            MissionAcgDurableRewardClaim claim =
                token ? source.State.TokenClaim : source.State.ItemClaim;
            if (claim.Phase == MissionAcgDurableClaimPhase.DurablyApplied)
            {
                claim = claim.Copy(
                    phase: MissionAcgDurableClaimPhase.ClientNotificationPending);
                if (!Replace(
                    updated,
                    CopyInventoryClaim(updated.State, token, claim),
                    out updated,
                    out failure))
                {
                    return false;
                }
            }

            claim = token ? updated.State.TokenClaim : updated.State.ItemClaim;
            if (claim.Phase
                == MissionAcgDurableClaimPhase.ClientNotificationPending)
            {
                if (token)
                {
                    MissionCompleteService.SendTokenAwardedFeedback(character);
                }
                else
                {
                    MissionCompleteService.SendYellowFeedback(
                        character,
                        "You've received an item as mission reward!");
                }

                claim = claim.Copy(
                    phase: MissionAcgDurableClaimPhase.ClientNotificationSent);
                if (!Replace(
                    updated,
                    CopyInventoryClaim(updated.State, token, claim),
                    out updated,
                    out failure))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool DeliverMissionNotifications(
            ICharacter character,
            MissionAcgInstanceBinding binding,
            MissionAcgObjectiveRecord source,
            out MissionAcgObjectiveRecord updated,
            out string failure)
        {
            updated = source;
            failure = string.Empty;
            if (updated.State.Action59Delivery == MissionAcgDeliveryPhase.NotStarted)
            {
                if (!Replace(
                    updated,
                    updated.State.Copy(action59Delivery: MissionAcgDeliveryPhase.Pending),
                    out updated,
                    out failure))
                {
                    return false;
                }
            }

            if (updated.State.Action59Delivery == MissionAcgDeliveryPhase.Pending)
            {
                MissionCompleteService.SendMissionCompleteAction(
                    character,
                    ToIdentity(binding.AcceptedQuestIdentity));
                if (!Replace(
                    updated,
                    updated.State.Copy(
                        action59Sent: true,
                        action59Delivery: MissionAcgDeliveryPhase.Sent),
                    out updated,
                    out failure))
                {
                    return false;
                }
            }

            if (updated.State.QuestDeleteDelivery == MissionAcgDeliveryPhase.NotStarted)
            {
                if (!Replace(
                    updated,
                    updated.State.Copy(questDeleteDelivery: MissionAcgDeliveryPhase.Pending),
                    out updated,
                    out failure))
                {
                    return false;
                }
            }

            if (updated.State.QuestDeleteDelivery == MissionAcgDeliveryPhase.Pending)
            {
                MissionCompleteService.SendQuestDelete(
                    character,
                    ToIdentity(binding.AcceptedQuestIdentity));
                if (!Replace(
                    updated,
                    updated.State.Copy(
                        questDeleteSent: true,
                        questDeleteDelivery: MissionAcgDeliveryPhase.Sent),
                    out updated,
                    out failure))
                {
                    return false;
                }
            }

            return true;
        }

        private static MissionAcgObjectiveState CopyInventoryClaim(
            MissionAcgObjectiveState state,
            bool token,
            MissionAcgDurableRewardClaim claim)
        {
            return token
                       ? state.Copy(tokenClaim: claim)
                       : state.Copy(itemClaim: claim);
        }

        private static MissionAcgDurableRewardClaim NotificationPending(
            MissionAcgDurableRewardClaim claim)
        {
            return claim.Phase == MissionAcgDurableClaimPhase.DurablyApplied
                       ? claim.Copy(
                           phase: MissionAcgDurableClaimPhase.ClientNotificationPending)
                       : claim;
        }

        private static MissionAcgDurableRewardClaim NotificationSent(
            MissionAcgDurableRewardClaim claim)
        {
            return claim.Phase
                   == MissionAcgDurableClaimPhase.ClientNotificationPending
                       ? claim.Copy(
                           phase: MissionAcgDurableClaimPhase.ClientNotificationSent)
                       : claim;
        }

        private static bool AdvanceRewardPhase(
            MissionAcgObjectiveRecord source,
            MissionAcgCompletionPhase phase,
            out MissionAcgObjectiveRecord updated,
            out string failure)
        {
            updated = source;
            failure = string.Empty;
            if (source.State.Phase >= phase)
            {
                return true;
            }

            return Replace(
                source,
                source.State.Copy(phase: phase),
                out updated,
                out failure);
        }

        private static bool FailClaim(
            MissionAcgObjectiveRecord source,
            bool xp,
            bool item,
            bool token,
            string diagnostic,
            out MissionAcgObjectiveRecord updated,
            out string failure)
        {
            MissionAcgDurableRewardClaim claim =
                token
                    ? source.State.TokenClaim
                    : xp
                          ? source.State.XpClaim
                          : item
                                ? source.State.ItemClaim
                                : source.State.CreditsClaim;
            claim = claim.Copy(
                phase: MissionAcgDurableClaimPhase.TerminalFailure,
                failure: string.IsNullOrWhiteSpace(diagnostic)
                    ? "Reward application failed closed."
                    : diagnostic);
            MissionAcgObjectiveState state =
                token
                    ? source.State.Copy(tokenClaim: claim)
                    : xp
                          ? source.State.Copy(xpClaim: claim)
                          : item
                                ? source.State.Copy(itemClaim: claim)
                                : source.State.Copy(creditsClaim: claim);

            if (!Replace(source, state, out updated, out failure))
            {
                return false;
            }

            failure = claim.Failure;
            return false;
        }

        private static int TokenItemInstance(MissionAcgInstanceBinding binding)
        {
            unchecked
            {
                return 0x66000000
                       | (binding.AcceptedQuestIdentity.Instance & 0x00FFFFFF);
            }
        }

        private static string TokenName(int lowId)
        {
            return lowId == MissionAcgTokenClaimPolicy.ClanTokenLowId
                       ? "Clan Token"
                       : "Omni Token";
        }

        private static bool TryResolveAcceptedMission(
            ICharacter character,
            MissionAcgIdentityRecord acceptedQuestIdentity,
            out MissionAcceptedStore.AcceptedMission accepted)
        {
            accepted = null;
            if (character == null || acceptedQuestIdentity == null)
            {
                return false;
            }

            Identity questIdentity = ToIdentity(acceptedQuestIdentity);
            return MissionAcceptedStore.TryResolve(
                       character.Identity.Instance,
                       questIdentity,
                       out accepted)
                   || MissionAcceptedStore.TryResolveGeneratedProjection(
                       character.Identity.Instance,
                       questIdentity,
                       out accepted);
        }

        internal static bool RemoveExactArtifacts(
            IZoneClient client,
            ICharacter character,
            MissionAcgInstanceBinding binding,
            MissionAcgObjectiveRecord objective,
            out string failure)
        {
            failure = string.Empty;
            bool keyPresent =
                MissionKeyGrantService.HasMissionKeyInstance(
                    character,
                    binding.MissionKeyIdentity.Instance);
            if (keyPresent
                && !MissionKeyGrantService.TryRemoveMissionKey(
                    client,
                    character,
                    binding.MissionKeyIdentity.Instance))
            {
                failure = "Exact mission key persistence failed.";
                return false;
            }

            if (MissionKeyGrantService.HasMissionKeyInstance(
                character,
                binding.MissionKeyIdentity.Instance))
            {
                failure = "Exact mission key persistence failed.";
                return false;
            }

            MissionKeyStore.ForgetExact(
                character.Identity.Instance,
                ToIdentity(binding.AcceptedQuestIdentity),
                binding.MissionKeyIdentity.Instance);

            if (objective.State.MissionItemIdentity != null
                && (binding.MissionType == MissionRollType.FindItemReturn
                    || binding.MissionType == MissionRollType.RepairMachine)
                && !TryRemoveExactInventoryItem(
                    client,
                    character,
                    objective.State.MissionItemIdentity,
                    objective.Binding.RequiredMissionItemTemplateId,
                    binding.MissionType == MissionRollType.RepairMachine,
                    out failure))
            {
                return false;
            }

            int ignored;
            MissionKeyStore.TryTakeRepairKit(
                character.Identity.Instance,
                ToIdentity(binding.AcceptedQuestIdentity),
                out ignored);
            return true;
        }

        internal static bool TryRemoveExactInventoryItem(
            IZoneClient client,
            ICharacter character,
            MissionAcgIdentityRecord identity,
            out string failure)
        {
            return TryRemoveExactInventoryItem(
                client,
                character,
                identity,
                0,
                false,
                out failure);
        }

        private static bool TryRemoveExactInventoryItem(
            IZoneClient client,
            ICharacter character,
            MissionAcgIdentityRecord identity,
            int requiredTemplateId,
            bool requireRepairTool,
            out string failure)
        {
            failure = string.Empty;
            if (identity == null)
            {
                failure = "Exact inventory artifact identity is required.";
                return false;
            }

            return MissionKeyGrantService.TryRemoveExactMissionArtifact(
                client,
                character,
                identity.Type,
                identity.Instance,
                requiredTemplateId,
                requireRepairTool,
                out failure);
        }

        private static int RewardItemInstance(MissionAcgInstanceBinding binding)
        {
            unchecked
            {
                return 0x65000000
                       | (binding.AcceptedQuestIdentity.Instance & 0x0FFFFFFF);
            }
        }

        private static Identity ToIdentity(MissionAcgIdentityRecord identity)
        {
            return identity == null
                       ? Identity.None
                       : new Identity
                         {
                             Type = (IdentityType)identity.Type,
                             Instance = identity.Instance
                         };
        }
    }
}
