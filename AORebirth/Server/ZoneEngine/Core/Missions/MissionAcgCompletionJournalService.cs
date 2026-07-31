namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
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
                MissionAcceptedStore.TryResolve(
                    character.Identity.Instance,
                    ToIdentity(objective.Binding.AcceptedQuestIdentity),
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
            MissionAcceptedStore.TryResolve(
                character.Identity.Instance,
                ToIdentity(objective.Binding.AcceptedQuestIdentity),
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
            if (!MissionAcgExpiryRuntime.TryClaimObjectiveVerification(
                    bindingRecord,
                    objectiveRecord,
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

            MissionAcgObjectiveRecord verified;
            int acceptedInstance =
                claimedBinding.Binding.AcceptedQuestIdentity.Instance;
            try
            {
                if (!MissionAcgObjectiveContract.TryVerify(
                        claimedObjective,
                        observation,
                        out failure)
                    || !MissionTokenProgressTracker.SealGeneratedProgress(
                        claimedBinding,
                        claimedObjective,
                        out failure)
                    || !MissionAcgObjectiveRuntime.TryReplaceState(
                        claimedObjective,
                        claimedObjective.State.Copy(
                            lifecycle:
                                MissionAcgObjectiveLifecycle.Verified,
                            phase:
                                MissionAcgCompletionPhase.ObjectiveVerified),
                        out verified,
                        out failure))
                {
                    return false;
                }
            }
            finally
            {
                MissionAcgExpiryRuntime.ReleaseObjectiveVerificationClaim(
                    acceptedInstance);
            }

            MissionAcceptedStore.AcceptedMission accepted;
            if (!MissionAcceptedStore.TryResolve(
                character.Identity.Instance,
                ToIdentity(claimedBinding.Binding.AcceptedQuestIdentity),
                out accepted))
            {
                return false;
            }

            return TryCompleteVerified(
                client,
                character,
                accepted,
                claimedBinding,
                verified,
                reason);
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
                return Continue(
                    client,
                    character,
                    accepted,
                    bindingRecord,
                    objectiveRecord,
                    reason);
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

                if (accepted == null)
                {
                    return false;
                }

                int itemLow;
                int itemHigh;
                int itemQuality;
                int itemCount;
                ResolveItemReward(
                    accepted,
                    out itemLow,
                    out itemHigh,
                    out itemQuality,
                    out itemCount);
                string claimBase =
                    binding.Binding.AcceptedQuestIdentity.Type
                    + "-"
                    + binding.Binding.AcceptedQuestIdentity.Instance;
                MissionAcgObjectiveState frozen =
                    objective.State.Copy(
                        phase: MissionAcgCompletionPhase.RewardCalculationFrozen,
                        frozenCredits: MissionCompleteService.ResolveCashReward(accepted),
                        frozenXp: MissionCompleteService.ResolveXpReward(accepted),
                        frozenItemLowId: itemLow,
                        frozenItemHighId: itemHigh,
                        frozenItemQuality: itemQuality,
                        frozenItemCount: itemCount,
                        creditsState:
                            MissionCompleteService.ResolveCashReward(accepted) > 0
                                ? MissionAcgGrantState.NotStarted
                                : MissionAcgGrantState.ExplicitNone,
                        xpState:
                            MissionCompleteService.ResolveXpReward(accepted) > 0
                                ? MissionAcgGrantState.NotStarted
                                : MissionAcgGrantState.ExplicitNone,
                        itemState:
                            itemCount > 0
                                ? MissionAcgGrantState.NotStarted
                                : MissionAcgGrantState.ExplicitNone,
                        creditsClaimId: claimBase + "-credits",
                        xpClaimId: claimBase + "-xp",
                        itemClaimId: claimBase + "-item");
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

            if (objective.State.CreditsState == MissionAcgGrantState.NotStarted)
            {
                if (!Replace(
                    objective,
                    objective.State.Copy(creditsState: MissionAcgGrantState.Pending),
                    out objective,
                    out failure))
                {
                    return false;
                }

                MissionCompleteService.GrantCredits(
                    character,
                    objective.State.FrozenCredits);
                if (!Replace(
                    objective,
                    objective.State.Copy(
                        creditsState: MissionAcgGrantState.Granted,
                        phase: MissionAcgCompletionPhase.CreditsGranted),
                    out objective,
                    out failure))
                {
                    return false;
                }
            }
            else if (objective.State.CreditsState == MissionAcgGrantState.Pending)
            {
                return PendingGrant("credits", objective);
            }
            else if (objective.State.Phase < MissionAcgCompletionPhase.CreditsGranted)
            {
                if (!Replace(
                    objective,
                    objective.State.Copy(
                        phase: MissionAcgCompletionPhase.CreditsGranted),
                    out objective,
                    out failure))
                {
                    return false;
                }
            }

            if (objective.State.XpState == MissionAcgGrantState.NotStarted)
            {
                if (!Replace(
                    objective,
                    objective.State.Copy(xpState: MissionAcgGrantState.Pending),
                    out objective,
                    out failure))
                {
                    return false;
                }

                if (!CombatXpRuntimeService.AwardDirectXp(
                    character,
                    objective.State.FrozenXp,
                    "mission-claim-" + objective.State.XpClaimId))
                {
                    return false;
                }

                if (!Replace(
                    objective,
                    objective.State.Copy(
                        xpState: MissionAcgGrantState.Granted,
                        phase: MissionAcgCompletionPhase.XpGranted),
                    out objective,
                    out failure))
                {
                    return false;
                }
            }
            else if (objective.State.XpState == MissionAcgGrantState.Pending)
            {
                return PendingGrant("xp", objective);
            }
            else if (objective.State.Phase < MissionAcgCompletionPhase.XpGranted)
            {
                if (!Replace(
                    objective,
                    objective.State.Copy(phase: MissionAcgCompletionPhase.XpGranted),
                    out objective,
                    out failure))
                {
                    return false;
                }
            }

            if (objective.State.ItemState == MissionAcgGrantState.NotStarted)
            {
                if (!Replace(
                    objective,
                    objective.State.Copy(itemState: MissionAcgGrantState.Pending),
                    out objective,
                    out failure))
                {
                    return false;
                }

                int reservedItemInstance = RewardItemInstance(binding.Binding);
                int grantedItemInstance;
                if (!MissionCompleteService.TryGrantOfferItemReward(
                    client,
                    character,
                    accepted,
                    reservedItemInstance,
                    out grantedItemInstance))
                {
                    return false;
                }

                if (!Replace(
                    objective,
                    objective.State.Copy(
                        itemState: MissionAcgGrantState.Granted,
                        phase: MissionAcgCompletionPhase.ItemRewardGrantedOrNone,
                        grantedRewardItemInstance: grantedItemInstance),
                    out objective,
                    out failure))
                {
                    return false;
                }
            }
            else if (objective.State.ItemState == MissionAcgGrantState.Pending)
            {
                return PendingGrant("item", objective);
            }
            else if (objective.State.Phase
                     < MissionAcgCompletionPhase.ItemRewardGrantedOrNone)
            {
                if (!Replace(
                    objective,
                    objective.State.Copy(
                        phase: MissionAcgCompletionPhase.ItemRewardGrantedOrNone),
                    out objective,
                    out failure))
                {
                    return false;
                }
            }

            MissionCompleteService.SendRewardFeedback(
                character,
                objective.State.FrozenXp,
                objective.State.FrozenCredits);
            MissionCompleteService.SendMissionAccomplishedFeedback(character);

            if (!objective.State.ArtifactsRemoved)
            {
                if (!RemoveExactArtifacts(
                    client,
                    character,
                    binding.Binding,
                    objective,
                    out failure))
                {
                    return false;
                }

                MissionAcceptedStore.Remove(
                    character.Identity.Instance,
                    ToIdentity(binding.Binding.AcceptedQuestIdentity));
                if (!Replace(
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

            if (!objective.State.Action59Sent)
            {
                MissionCompleteService.SendMissionCompleteAction(
                    character,
                    ToIdentity(binding.Binding.AcceptedQuestIdentity));
                if (!Replace(
                    objective,
                    objective.State.Copy(
                        action59Sent: true,
                        phase: MissionAcgCompletionPhase.Action59Sent),
                    out objective,
                    out failure))
                {
                    return false;
                }
            }

            if (!objective.State.QuestDeleteSent)
            {
                MissionCompleteService.SendQuestDelete(
                    character,
                    ToIdentity(binding.Binding.AcceptedQuestIdentity));
                if (!Replace(
                    objective,
                    objective.State.Copy(
                        questDeleteSent: true,
                        phase: MissionAcgCompletionPhase.QuestDeleteSent),
                    out objective,
                    out failure))
                {
                    return false;
                }
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
                if (!MissionAcgRuntimeManager.Cleanup(binding, out failure)
                    || !Replace(
                        objective,
                        objective.State.Copy(
                            objectiveCleanupCompleted: true,
                            phase: MissionAcgCompletionPhase.ObjectiveCleanupCompleted),
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

        private static bool PendingGrant(
            string reward,
            MissionAcgObjectiveRecord objective)
        {
            MissionDiagnostics.Log(
                "ACG-COMPLETE-RECONCILE accepted={0}:{1} reward={2} state=pending path={3}",
                objective.Binding.AcceptedQuestIdentity.Type,
                objective.Binding.AcceptedQuestIdentity.Instance,
                reward,
                objective.RecordPath);
            return false;
        }

        private static void ResolveItemReward(
            MissionAcceptedStore.AcceptedMission accepted,
            out int low,
            out int high,
            out int quality,
            out int count)
        {
            low = 0;
            high = 0;
            quality = 0;
            count = 0;
            if (accepted == null
                || accepted.Offer == null
                || accepted.Offer.ItemRewards == null
                || accepted.Offer.ItemRewards.Length == 0
                || accepted.Offer.ItemRewards[0] == null
                || accepted.Offer.ItemRewards[0].LowId <= 0)
            {
                return;
            }

            QuestItemShort item = accepted.Offer.ItemRewards[0];
            low = item.LowId;
            high = item.HighId > 0 ? item.HighId : item.LowId;
            quality =
                item.Quality > 0
                    ? item.Quality
                    : accepted.Quality > 0 ? accepted.Quality : 1;
            count = 1;
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
