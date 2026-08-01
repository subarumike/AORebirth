namespace ZoneEngine.Core.Missions
{
    using System;

    internal sealed class MissionAcgObjectiveEvent
    {
        internal int OwnerInstance { get; set; }

        internal MissionAcgIdentityRecord TeamIdentity { get; set; }

        internal int AcceptedQuestInstance { get; set; }

        internal int AllocatedLivePlayfield2 { get; set; }

        internal MissionAcgIdentityRecord RuntimeObjectiveIdentity { get; set; }

        internal MissionAcgObjectiveInteraction Interaction { get; set; }

        internal int ObjectiveTemplateId { get; set; }

        internal string ObjectiveName { get; set; }

        internal MissionAcgIdentityRecord MissionItemIdentity { get; set; }

        internal int MissionItemTemplateId { get; set; }

        internal MissionAcgIdentityRecord IssuingTerminalIdentity { get; set; }

        internal string ObservationId { get; set; }
    }

    internal static class MissionAcgObjectiveContract
    {
        internal const int RepairComponentTemplateId = 100348;

        internal const int RepairMachineTemplateId = 100358;

        internal static MissionAcgObjectiveInteraction InteractionFor(
            MissionRollType missionType)
        {
            switch (missionType)
            {
                case MissionRollType.KillPerson:
                    return MissionAcgObjectiveInteraction.TargetDeath;
                case MissionRollType.FindPerson:
                    return MissionAcgObjectiveInteraction.InfoRequest;
                case MissionRollType.FindItem:
                    return MissionAcgObjectiveInteraction.StaticItemPickup;
                case MissionRollType.FindItemReturn:
                    return MissionAcgObjectiveInteraction.ReturnItemToTerminal;
                case MissionRollType.RepairMachine:
                    return MissionAcgObjectiveInteraction.UseComponentOnMachine;
                default:
                    throw new InvalidOperationException(
                        "Unsupported generated mission objective type.");
            }
        }

        internal static int AcceptedQfuVersionFor(MissionRollType missionType)
        {
            switch (missionType)
            {
                case MissionRollType.FindItemReturn:
                    return 8;
                case MissionRollType.FindItem:
                    return 15;
                case MissionRollType.KillPerson:
                case MissionRollType.FindPerson:
                case MissionRollType.RepairMachine:
                    return 16;
                default:
                    throw new InvalidOperationException(
                        "Unsupported generated mission accepted QFU type.");
            }
        }

        internal static int AcceptedQfuQuestIdentityFlagFor(
            MissionRollType missionType)
        {
            return missionType == MissionRollType.FindPerson ? 64 : 0;
        }

        internal static bool TryVerify(
            MissionAcgObjectiveRecord record,
            MissionAcgObjectiveEvent observation,
            out string failure)
        {
            failure = string.Empty;
            if (record == null || observation == null)
            {
                failure = "Objective record and observation are required.";
                return false;
            }

            MissionAcgObjectiveBinding binding = record.Binding;
            MissionAcgObjectiveState state = record.State;
            if (state.Lifecycle == MissionAcgObjectiveLifecycle.Completed
                || state.Lifecycle == MissionAcgObjectiveLifecycle.Abandoned
                || state.Lifecycle == MissionAcgObjectiveLifecycle.Expired
                || state.Lifecycle == MissionAcgObjectiveLifecycle.CleanupCompleted
                || state.Lifecycle == MissionAcgObjectiveLifecycle.Invalid
                || state.Phase >= MissionAcgCompletionPhase.ObjectiveVerified)
            {
                failure = "Objective is no longer completable.";
                return false;
            }

            if (observation.OwnerInstance != binding.OwnerIdentity.Instance
                || observation.AcceptedQuestInstance
                   != binding.AcceptedQuestIdentity.Instance
                || observation.AllocatedLivePlayfield2
                   != binding.AllocatedLivePlayfield2
                || observation.RuntimeObjectiveIdentity == null
                || !observation.RuntimeObjectiveIdentity.Equals(
                    binding.RuntimeObjectiveIdentity)
                || observation.Interaction != binding.RequiredInteraction
                || observation.ObjectiveTemplateId != binding.ObjectiveTemplateId)
            {
                failure = "Objective owner, accepted mission, PF2, runtime identity, or contract differs.";
                return false;
            }

            if (binding.ExplicitNoTeam)
            {
                if (observation.TeamIdentity != null)
                {
                    failure = "Solo objective cannot be redirected through a team.";
                    return false;
                }
            }
            else if (observation.TeamIdentity == null
                     || !observation.TeamIdentity.Equals(binding.TeamIdentity))
            {
                failure = "Objective team identity differs from the accepted binding.";
                return false;
            }

            if (!string.IsNullOrEmpty(binding.ObjectiveName)
                && !string.Equals(
                    observation.ObjectiveName ?? string.Empty,
                    binding.ObjectiveName,
                    StringComparison.Ordinal))
            {
                failure = "Objective name differs from captured slot.";
                return false;
            }

            if (binding.MissionType == MissionRollType.FindItemReturn
                || binding.MissionType == MissionRollType.RepairMachine)
            {
                if (state.MissionItemIdentity == null
                    || observation.MissionItemIdentity == null
                    || !state.MissionItemIdentity.Equals(observation.MissionItemIdentity)
                    || observation.MissionItemTemplateId
                       != binding.RequiredMissionItemTemplateId)
                {
                    failure = "Exact mission inventory instance or template differs.";
                    return false;
                }
            }

            if (binding.MissionType == MissionRollType.FindItemReturn
                && (binding.IssuingTerminalIdentity == null
                    || observation.IssuingTerminalIdentity == null
                    || !binding.IssuingTerminalIdentity.Equals(
                        observation.IssuingTerminalIdentity)))
            {
                failure = "Return Item issuing terminal differs.";
                return false;
            }

            if (binding.MissionType == MissionRollType.RepairMachine
                && (binding.RequiredMissionItemTemplateId
                    != RepairComponentTemplateId
                    || binding.RequiredMachineTemplateId
                    != RepairMachineTemplateId
                    || binding.ObjectiveTemplateId
                    != RepairMachineTemplateId))
            {
                failure = "Repair component-to-machine contract differs from capture.";
                return false;
            }

            return true;
        }
    }

    internal static class MissionAcgCompletionRules
    {
        internal static bool CanReplace(
            MissionAcgObjectiveState current,
            MissionAcgObjectiveState next,
            out string failure)
        {
            failure = string.Empty;
            if (current == null || next == null)
            {
                failure = "Completion states are required.";
                return false;
            }

            if (!IsConsistent(next, out failure))
            {
                return false;
            }

            if (!LifecycleCanAdvance(
                current.Lifecycle,
                next.Lifecycle,
                current.Phase,
                out failure))
            {
                return false;
            }

            if (next.Phase < current.Phase
                || (int)next.Phase > (int)current.Phase + 1)
            {
                failure = "Completion phase must advance monotonically one durable step.";
                return false;
            }

            if (!GrantCanAdvance(current.CreditsState, next.CreditsState)
                || !GrantCanAdvance(current.XpState, next.XpState)
                || !GrantCanAdvance(current.ItemState, next.ItemState))
            {
                failure = "Reward grant state cannot regress or change after terminal state.";
                return false;
            }

            if (!DurableClaimCanAdvance(current.CreditsClaim, next.CreditsClaim)
                || !DurableClaimCanAdvance(current.XpClaim, next.XpClaim)
                || !DurableClaimCanAdvance(current.ItemClaim, next.ItemClaim)
                || !DurableClaimCanAdvance(current.TokenClaim, next.TokenClaim))
            {
                failure = "Durable reward claim cannot regress or change frozen ownership.";
                return false;
            }

            if (!DeliveryCanAdvance(
                    current.RewardFeedbackDelivery,
                    next.RewardFeedbackDelivery)
                || !DeliveryCanAdvance(
                    current.MissionAccomplishedDelivery,
                    next.MissionAccomplishedDelivery)
                || !DeliveryCanAdvance(current.Action59Delivery, next.Action59Delivery)
                || !DeliveryCanAdvance(
                    current.QuestDeleteDelivery,
                    next.QuestDeleteDelivery)
                || !DeliveryCanAdvance(
                    current.MissionListRemovalDelivery,
                    next.MissionListRemovalDelivery)
                || !DeliveryCanAdvance(
                    current.CleanupHandoffDelivery,
                    next.CleanupHandoffDelivery))
            {
                failure = "Durable completion delivery state cannot regress.";
                return false;
            }

            if (current.MissionItemIdentity != null
                && (next.MissionItemIdentity == null
                    || !current.MissionItemIdentity.Equals(next.MissionItemIdentity)))
            {
                failure = "Exact mission inventory identity cannot be replaced.";
                return false;
            }

            if ((current.ArtifactsRemoved && !next.ArtifactsRemoved)
                || (current.Action59Sent && !next.Action59Sent)
                || (current.QuestDeleteSent && !next.QuestDeleteSent)
                || (current.ObjectiveCleanupCompleted
                    && !next.ObjectiveCleanupCompleted)
                || (current.MissionCleanupCompleted
                    && !next.MissionCleanupCompleted))
            {
                failure = "Durable completion acknowledgement cannot regress.";
                return false;
            }

            return true;
        }

        private static bool LifecycleCanAdvance(
            MissionAcgObjectiveLifecycle current,
            MissionAcgObjectiveLifecycle next,
            MissionAcgCompletionPhase phase,
            out string failure)
        {
            failure = string.Empty;
            if (current == next)
            {
                return true;
            }

            if ((next == MissionAcgObjectiveLifecycle.Expired
                 || next == MissionAcgObjectiveLifecycle.Abandoned)
                && phase >= MissionAcgCompletionPhase.RewardClaimStarted)
            {
                failure =
                    "Durable reward claim owns the completion-versus-cleanup race.";
                return false;
            }

            if (current == MissionAcgObjectiveLifecycle.Expired
                || current == MissionAcgObjectiveLifecycle.Abandoned)
            {
                if (next == MissionAcgObjectiveLifecycle.CleanupCompleted)
                {
                    return true;
                }

                failure = "Expired or abandoned objective state cannot be resurrected.";
                return false;
            }

            if (current == MissionAcgObjectiveLifecycle.Completed)
            {
                if (next == MissionAcgObjectiveLifecycle.CleanupCompleted)
                {
                    return true;
                }

                failure = "Completed objective state cannot be replaced.";
                return false;
            }

            if (current == MissionAcgObjectiveLifecycle.CleanupCompleted
                || current == MissionAcgObjectiveLifecycle.Invalid)
            {
                failure = "Terminal objective state cannot be replaced.";
                return false;
            }

            return true;
        }

        internal static bool IsConsistent(
            MissionAcgObjectiveState state,
            out string failure)
        {
            failure = string.Empty;
            if (state == null)
            {
                failure = "Completion state is required.";
                return false;
            }

            if (!ClaimIsConsistent(state.CreditsClaim)
                || !ClaimIsConsistent(state.XpClaim)
                || !ClaimIsConsistent(state.ItemClaim)
                || !ClaimIsConsistent(state.TokenClaim))
            {
                failure = "Durable reward claim is structurally incomplete.";
                return false;
            }

            if (state.Phase < MissionAcgCompletionPhase.RewardCalculationFrozen)
            {
                if (state.CreditsState != MissionAcgGrantState.NotStarted
                    || state.XpState != MissionAcgGrantState.NotStarted
                    || state.ItemState != MissionAcgGrantState.NotStarted
                    || state.FrozenCredits != 0
                    || state.FrozenXp != 0
                    || state.FrozenItemLowId != 0
                    || state.FrozenItemHighId != 0
                    || state.FrozenItemQuality != 0
                    || state.FrozenItemCount != 0
                    || !string.IsNullOrEmpty(state.CreditsClaimId)
                    || !string.IsNullOrEmpty(state.XpClaimId)
                    || !string.IsNullOrEmpty(state.ItemClaimId))
                {
                    failure = "Rewards cannot change before calculation is frozen.";
                    return false;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(state.CreditsClaimId)
                    || string.IsNullOrEmpty(state.XpClaimId)
                    || string.IsNullOrEmpty(state.ItemClaimId)
                    || (state.FrozenCredits == 0
                        && state.CreditsState != MissionAcgGrantState.ExplicitNone)
                    || (state.FrozenXp == 0
                        && state.XpState != MissionAcgGrantState.ExplicitNone)
                    || (state.FrozenItemCount == 0
                        && state.ItemState != MissionAcgGrantState.ExplicitNone)
                    || (state.FrozenItemCount > 0
                        && (state.FrozenItemLowId <= 0
                            || state.FrozenItemHighId <= 0
                            || state.FrozenItemQuality <= 0)))
                {
                    failure = "Frozen reward values and stable claim identities are incomplete.";
                    return false;
                }
            }

            if (state.Phase < MissionAcgCompletionPhase.RewardClaimStarted
                && (IsStarted(state.CreditsState)
                    || IsStarted(state.XpState)
                    || IsStarted(state.ItemState)))
            {
                failure = "A reward grant cannot start before the durable claim boundary.";
                return false;
            }

            if ((state.CreditsState == MissionAcgGrantState.Granted
                 && state.Phase < MissionAcgCompletionPhase.CreditsGranted)
                || (state.XpState == MissionAcgGrantState.Granted
                    && state.Phase < MissionAcgCompletionPhase.XpGranted)
                || (state.ItemState == MissionAcgGrantState.Granted
                    && state.Phase
                       < MissionAcgCompletionPhase.ItemRewardGrantedOrNone)
                || (state.Phase >= MissionAcgCompletionPhase.CreditsGranted
                    && !IsTerminal(state.CreditsState))
                || (state.Phase >= MissionAcgCompletionPhase.XpGranted
                    && !IsTerminal(state.XpState))
                || (state.Phase
                    >= MissionAcgCompletionPhase.ItemRewardGrantedOrNone
                    && !IsTerminal(state.ItemState)))
            {
                failure = "Reward phase and per-reward grant state disagree.";
                return false;
            }

            bool abandonmentCleanup =
                state.Lifecycle == MissionAcgObjectiveLifecycle.Abandoned
                || state.Lifecycle == MissionAcgObjectiveLifecycle.Expired
                || state.Lifecycle == MissionAcgObjectiveLifecycle.CleanupCompleted;
            if (!abandonmentCleanup
                && ((state.Phase
                        >= MissionAcgCompletionPhase.MissionArtifactsRemoved
                     && !state.ArtifactsRemoved)
                    || (state.Phase >= MissionAcgCompletionPhase.Action59Sent
                        && !state.Action59Sent)
                    || (state.Phase >= MissionAcgCompletionPhase.QuestDeleteSent
                        && !state.QuestDeleteSent)
                    || (state.Phase
                            >= MissionAcgCompletionPhase.ObjectiveCleanupCompleted
                        && !state.ObjectiveCleanupCompleted)
                    || (state.Phase
                            >= MissionAcgCompletionPhase.MissionCleanupCompleted
                        && !state.MissionCleanupCompleted)))
            {
                failure = "Completion cleanup phase is missing its durable marker.";
                return false;
            }


            if (!abandonmentCleanup
                && state.Phase >= MissionAcgCompletionPhase.MissionArtifactsRemoved
                && (!ClaimReadyForCleanup(state.CreditsClaim)
                    || !ClaimReadyForCleanup(state.XpClaim)
                    || !ClaimReadyForCleanup(state.ItemClaim)
                    || !ClaimReadyForCleanup(state.TokenClaim)
                    || state.RewardFeedbackDelivery != MissionAcgDeliveryPhase.Sent
                    || state.MissionAccomplishedDelivery
                       != MissionAcgDeliveryPhase.Sent))
            {
                failure =
                    "Completion cleanup cannot start before durable claims and send attempts finish.";
                return false;
            }

            return true;
        }

        private static bool IsStarted(MissionAcgGrantState state)
        {
            return state == MissionAcgGrantState.Pending
                   || state == MissionAcgGrantState.Granted;
        }

        private static bool IsTerminal(MissionAcgGrantState state)
        {
            return state == MissionAcgGrantState.Granted
                   || state == MissionAcgGrantState.ExplicitNone;
        }

        private static bool GrantCanAdvance(
            MissionAcgGrantState current,
            MissionAcgGrantState next)
        {
            if (current == next)
            {
                return true;
            }

            if (current == MissionAcgGrantState.NotStarted)
            {
                return next == MissionAcgGrantState.Pending
                       || next == MissionAcgGrantState.ExplicitNone;
            }

            return current == MissionAcgGrantState.Pending
                   && next == MissionAcgGrantState.Granted;
        }

        private static bool DurableClaimCanAdvance(
            MissionAcgDurableRewardClaim current,
            MissionAcgDurableRewardClaim next)
        {
            if (current == null || next == null)
            {
                return false;
            }

            if (next.Phase < current.Phase)
            {
                return false;
            }

            if (current.Phase == MissionAcgDurableClaimPhase.TerminalFailure)
            {
                return next.Phase == current.Phase
                       && SameClaim(current, next);
            }

            if (current.Phase == MissionAcgDurableClaimPhase.NotEligible)
            {
                return next.Phase == current.Phase && SameClaim(current, next);
            }

            if (next.Phase == current.Phase)
            {
                return SameClaim(current, next);
            }

            if (current.Phase == MissionAcgDurableClaimPhase.Uninitialized)
            {
                return next.Phase == MissionAcgDurableClaimPhase.NotEligible
                       || next.Phase
                          == MissionAcgDurableClaimPhase.EligibleFrozen;
            }

            if (current.Phase == MissionAcgDurableClaimPhase.EligibleFrozen)
            {
                if (next.Phase == MissionAcgDurableClaimPhase.ClaimReserved)
                {
                    return SameRewardDefinition(current, next);
                }

                return next.Phase == MissionAcgDurableClaimPhase.TerminalFailure
                       && SameFrozenClaim(current, next);
            }

            if (!SameFrozenClaim(current, next))
            {
                return false;
            }

            if (next.Phase == MissionAcgDurableClaimPhase.TerminalFailure)
            {
                return true;
            }

            MissionAcgDurableClaimPhase expected;
            switch (current.Phase)
            {
                case MissionAcgDurableClaimPhase.ClaimReserved:
                    expected = MissionAcgDurableClaimPhase.ApplicationPending;
                    break;
                case MissionAcgDurableClaimPhase.ApplicationPending:
                    expected = MissionAcgDurableClaimPhase.DurablyApplied;
                    break;
                case MissionAcgDurableClaimPhase.DurablyApplied:
                    expected = MissionAcgDurableClaimPhase.ClientNotificationPending;
                    break;
                case MissionAcgDurableClaimPhase.ClientNotificationPending:
                    expected = MissionAcgDurableClaimPhase.ClientNotificationSent;
                    break;
                case MissionAcgDurableClaimPhase.ClientNotificationSent:
                    return next.Phase == current.Phase && SameClaim(current, next);
                default:
                    return false;
            }

            return next.Phase == expected;
        }

        private static bool SameFrozenClaim(
            MissionAcgDurableRewardClaim left,
            MissionAcgDurableRewardClaim right)
        {
            return string.Equals(left.ClaimId, right.ClaimId, StringComparison.Ordinal)
                   && left.Amount == right.Amount
                   && left.ItemLowId == right.ItemLowId
                   && left.ItemHighId == right.ItemHighId
                   && left.ItemQuality == right.ItemQuality
                   && left.ItemCount == right.ItemCount
                   && SameIdentity(left.ReservedItemIdentity, right.ReservedItemIdentity)
                   && SameIdentity(left.TargetContainerIdentity, right.TargetContainerIdentity)
                   && left.PreApplyValue == right.PreApplyValue
                   && left.ExpectedPostValue == right.ExpectedPostValue
                   && string.Equals(
                       left.PreApplyFingerprint,
                       right.PreApplyFingerprint,
                       StringComparison.Ordinal);
        }

        private static bool SameRewardDefinition(
            MissionAcgDurableRewardClaim left,
            MissionAcgDurableRewardClaim right)
        {
            return string.Equals(left.ClaimId, right.ClaimId, StringComparison.Ordinal)
                   && left.Amount == right.Amount
                   && left.ItemLowId == right.ItemLowId
                   && left.ItemHighId == right.ItemHighId
                   && left.ItemQuality == right.ItemQuality
                   && left.ItemCount == right.ItemCount;
        }

        private static bool SameClaim(
            MissionAcgDurableRewardClaim left,
            MissionAcgDurableRewardClaim right)
        {
            return SameFrozenClaim(left, right)
                   && string.Equals(left.Failure, right.Failure, StringComparison.Ordinal);
        }

        private static bool SameIdentity(
            MissionAcgIdentityRecord left,
            MissionAcgIdentityRecord right)
        {
            return left == null
                       ? right == null
                       : right != null && left.Equals(right);
        }

        private static bool ClaimIsConsistent(MissionAcgDurableRewardClaim claim)
        {
            if (claim == null)
            {
                return false;
            }

            if (claim.Phase >= MissionAcgDurableClaimPhase.EligibleFrozen
                && string.IsNullOrEmpty(claim.ClaimId))
            {
                return false;
            }

            if (claim.ItemCount > 0
                && claim.Phase >= MissionAcgDurableClaimPhase.ClaimReserved
                && (claim.ItemLowId <= 0
                    || claim.ItemHighId <= 0
                    || claim.ItemQuality <= 0))
            {
                return false;
            }

            return claim.Phase != MissionAcgDurableClaimPhase.TerminalFailure
                   || !string.IsNullOrEmpty(claim.Failure);
        }

        private static bool DeliveryCanAdvance(
            MissionAcgDeliveryPhase current,
            MissionAcgDeliveryPhase next)
        {
            return next >= current
                   && (current != MissionAcgDeliveryPhase.TerminalFailure
                       || next == current);
        }

        private static bool ClaimReadyForCleanup(
            MissionAcgDurableRewardClaim claim)
        {
            return claim.Phase == MissionAcgDurableClaimPhase.NotEligible
                   || claim.Phase
                      == MissionAcgDurableClaimPhase.ClientNotificationSent;
        }
    }
}
