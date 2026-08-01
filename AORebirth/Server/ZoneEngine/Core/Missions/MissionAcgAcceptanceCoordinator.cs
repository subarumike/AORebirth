namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    internal static class MissionAcgAcceptanceCoordinator
    {
        private const int RepairArtifactIdentityType = 0x0000C73D;

        private static readonly object AcceptanceSync = new object();

        internal static bool TryAccept(
            IZoneClient client,
            ICharacter character,
            Identity originalOfferIdentity,
            out MissionAcgBindingRecord accepted,
            out string failure)
        {
            accepted = null;
            failure = string.Empty;
            if (client == null
                || character == null
                || originalOfferIdentity == null
                || originalOfferIdentity.Instance <= 0)
            {
                failure = "Player and exact original offer identity are required.";
                return false;
            }

            lock (AcceptanceSync)
            {
                MissionAcgBindingRuntime.Initialize();

                MissionAcgAcceptedProjection existingProjection;
                if (MissionAcgAcceptedProjectionRuntime.TryGetByOwnerOffer(
                    (int)character.Identity.Type,
                    character.Identity.Instance,
                    (int)originalOfferIdentity.Type,
                    originalOfferIdentity.Instance,
                    out existingProjection))
                {
                    return TryResumeAcceptance(
                        client,
                        character,
                        existingProjection,
                        out accepted,
                        out failure);
                }

                MissionAcgBindingRecord unsafeLegacyBinding;
                if (MissionAcgBindingRuntime.TryGetByOwnerOffer(
                    (int)character.Identity.Type,
                    character.Identity.Instance,
                    (int)originalOfferIdentity.Type,
                    originalOfferIdentity.Instance,
                    out unsafeLegacyBinding))
                {
                    failure =
                        "This generated mission has an incomplete legacy accepted record and cannot be accepted again.";
                    return false;
                }

                MissionOfferRecord offerRecord;
                if (!MissionOfferStore.TryClaimForAcceptance(
                    character.Identity.Instance,
                    originalOfferIdentity,
                    DateTime.UtcNow,
                    out offerRecord,
                    out failure))
                {
                    return false;
                }

                bool durableClaimCreated = false;
                MissionAcgAllocationService allocator = MissionAcgBindingRuntime.Allocator;
                MissionAcgIdentityRecord acceptedIdentity = null;
                MissionAcgIdentityRecord keyIdentity = null;
                int livePlayfield2 = 0;
                try
                {
                    QuestInfo offer = offerRecord.Offer;
                    MissionRollType missionType =
                        MissionTypeCatalog.TypeFromIcon(offer.MissionIconId);
                    string validationFailure;
                    if (!ValidateOffer(offerRecord, missionType, out validationFailure))
                    {
                        failure = validationFailure;
                        return false;
                    }

                    if (!allocator.TryReserveAcceptedQuestIdentity(out acceptedIdentity))
                    {
                        failure = "Accepted mission identity range is exhausted.";
                        return false;
                    }

                    MissionAcgIdentityRecord ownerIdentity = ToRecord(character.Identity);
                    int missionSeed = DeriveMissionSeed(offer, ownerIdentity, missionType);
                    MissionAcgLayoutBundle bundle =
                        MissionAcgLayoutSelector.Select(
                            MissionAcgBindingRuntime.Catalog,
                            new MissionAcgSelectionInput(
                                missionSeed,
                                missionType,
                                offer.Quality,
                                ownerIdentity));

                    if (!allocator.TryReservePlayfield(
                        acceptedIdentity,
                        out livePlayfield2))
                    {
                        failure = "Mission PF2 allocation range is exhausted.";
                        return false;
                    }

                    if (!allocator.TryReserveMissionKeyIdentity(out keyIdentity))
                    {
                        failure = "Mission key identity range is exhausted.";
                        return false;
                    }

                    QuestActionList action = offer.QuestActions[0];
                    DateTime acceptedUtc = DateTime.UtcNow;
                    var binding = MissionAcgInstanceBinding.CreateDurable(
                        acceptedIdentity,
                        ToRecord(offer.QuestIdentity),
                        ownerIdentity,
                        null,
                        missionType,
                        offer.Quality,
                        missionSeed,
                        keyIdentity,
                        ToRecord(action.Playfield),
                        action.Unknown18,
                        action.Unknown19,
                        action.X,
                        action.Y,
                        action.Z,
                        ToRecord(offer.Unknown5),
                        bundle,
                        livePlayfield2,
                        acceptedUtc,
                        acceptedUtc.AddSeconds(
                            MissionAcceptService.MissionDurationSeconds));

                    int qfuVersion;
                    int qfuFlag;
                    ResolveQfuContract(missionType, out qfuVersion, out qfuFlag);
                    MissionAcgIdentityRecord reservedArtifact =
                        missionType == MissionRollType.RepairMachine
                            ? new MissionAcgIdentityRecord(
                                RepairArtifactIdentityType,
                                acceptedIdentity.Instance)
                            : null;
                    int repairArtifactLowId = 0;
                    int repairArtifactHighId = 0;
                    if (missionType == MissionRollType.RepairMachine
                        && !MissionKeyGrantService.TryResolveRepairTemplateIds(
                            out repairArtifactLowId,
                            out repairArtifactHighId))
                    {
                        failure =
                            "Repair component templates are unavailable; acceptance failed closed.";
                        return false;
                    }

                    MissionAcgAcceptedProjection projection =
                        MissionAcgAcceptedProjection.Create(
                            binding,
                            offerRecord.SerializedRollPayload,
                            offerRecord.OfferIndex,
                            (byte)offerRecord.LevelSlider,
                            offerRecord.GoodBadSlider,
                            offerRecord.OrderChaosSlider,
                            offerRecord.OpenHiddenSlider,
                            offerRecord.PhysicalMysticalSlider,
                            offerRecord.HeadOnStealthSlider,
                            offerRecord.MoneyExperienceSlider,
                            offerRecord.IssuedUtc,
                            offerRecord.ExpiresUtc,
                            qfuVersion,
                            qfuFlag,
                            MissionAcgAcceptancePhase.OfferClaimed,
                            null,
                            reservedArtifact,
                            repairArtifactLowId,
                            repairArtifactHighId,
                            MissionAcgLifecycleState.Reserved,
                            MissionAcgCleanupState.None,
                            acceptedUtc);

                    MissionAcgAcceptedProjection persistedProjection;
                    if (!MissionAcgAcceptedProjectionRuntime.TryCreate(
                        projection,
                        out persistedProjection,
                        out failure))
                    {
                        return false;
                    }

                    durableClaimCreated = true;
                    MissionOfferStore.MarkDurablyClaimed(
                        character.Identity.Instance,
                        originalOfferIdentity);
                    return TryResumeAcceptance(
                        client,
                        character,
                        persistedProjection,
                        out accepted,
                        out failure);
                }
                catch (Exception ex)
                {
                    failure = "Generated mission acceptance failed closed: " + ex.Message;
                    return false;
                }
                finally
                {
                    if (!durableClaimCreated)
                    {
                        allocator.RollbackUnpersisted(
                            acceptedIdentity,
                            keyIdentity,
                            livePlayfield2);
                        MissionOfferStore.ReleaseClaim(
                            character.Identity.Instance,
                            originalOfferIdentity);
                    }
                }
            }
        }

        internal static bool TryRecoverOwned(
            IZoneClient client,
            ICharacter character,
            out ISet<int> deliveredAcceptedQuestInstances,
            out string failure)
        {
            deliveredAcceptedQuestInstances = new HashSet<int>();
            failure = string.Empty;
            if (client == null || character == null)
            {
                failure = "Player connection is required for acceptance recovery.";
                return false;
            }

            lock (AcceptanceSync)
            {
                MissionAcgBindingRuntime.Initialize();
                IList<MissionAcgAcceptedProjection> projections =
                    MissionAcgAcceptedProjectionRuntime.GetOwned(
                        character.Identity.Instance);
                for (int i = 0; i < projections.Count; i++)
                {
                    MissionAcgAcceptedProjection projection = projections[i];
                    if (projection.AcceptancePhase == MissionAcgAcceptancePhase.QfuSent
                        || projection.LifecycleState == MissionAcgLifecycleState.Completed
                        || projection.LifecycleState == MissionAcgLifecycleState.Abandoned
                        || projection.LifecycleState == MissionAcgLifecycleState.Expired
                        || projection.LifecycleState == MissionAcgLifecycleState.Cleaned
                        || projection.LifecycleState == MissionAcgLifecycleState.Invalid)
                    {
                        continue;
                    }

                    MissionAcgBindingRecord ignored;
                    if (!TryResumeAcceptance(
                        client,
                        character,
                        projection,
                        out ignored,
                        out failure))
                    {
                        return false;
                    }

                    deliveredAcceptedQuestInstances.Add(
                        projection.Binding.AcceptedQuestIdentity.Instance);
                }

                return true;
            }
        }

        private static bool TryResumeAcceptance(
            IZoneClient client,
            ICharacter character,
            MissionAcgAcceptedProjection projection,
            out MissionAcgBindingRecord accepted,
            out string failure)
        {
            accepted = null;
            failure = string.Empty;
            MissionAcgInstanceBinding binding = projection.Binding;
            if (binding.OwnerIdentity.Type != (int)character.Identity.Type
                || binding.OwnerIdentity.Instance != character.Identity.Instance)
            {
                failure = "Accepted mission owner does not match the connected player.";
                return false;
            }

            if ((projection.LifecycleState != MissionAcgLifecycleState.Reserved
                 && projection.LifecycleState != MissionAcgLifecycleState.Accepted
                 && projection.LifecycleState != MissionAcgLifecycleState.Active)
                || projection.CleanupState != MissionAcgCleanupState.None)
            {
                failure =
                    "Accepted mission lifecycle no longer permits acceptance recovery.";
                return false;
            }

            MissionAcgBindingRecord bindingRecord;
            bool hasBinding = MissionAcgBindingRuntime.TryGetByAcceptedQuest(
                binding.AcceptedQuestIdentity.Instance,
                out bindingRecord);
            if (binding.ExpiryUtc <= DateTime.UtcNow)
            {
                if (!hasBinding
                    && projection.AcceptancePhase
                        == MissionAcgAcceptancePhase.OfferClaimed)
                {
                    MissionAcgAcceptedProjection expiredProjection;
                    string expiryPersistenceFailure;
                    if (MissionAcgAcceptedProjectionRuntime.TryReplace(
                        projection,
                        projection.WithLifecycle(
                            MissionAcgLifecycleState.Cleaned,
                            MissionAcgCleanupState.Completed,
                            DateTime.UtcNow),
                        out expiredProjection,
                        out expiryPersistenceFailure))
                    {
                        MissionAcgBindingRuntime.Allocator.RollbackUnpersisted(
                            binding.AcceptedQuestIdentity,
                            binding.MissionKeyIdentity,
                            binding.AllocatedLivePlayfield2);
                    }
                    else
                    {
                        failure =
                            "Expired accepted projection cleanup is pending: "
                            + expiryPersistenceFailure;
                        return false;
                    }
                }

                failure = "Accepted mission projection has expired and cannot grant artifacts.";
                return false;
            }

            if (hasBinding
                && ((bindingRecord.State.LifecycleState
                         != MissionAcgLifecycleState.Reserved
                     && bindingRecord.State.LifecycleState
                         != MissionAcgLifecycleState.Accepted
                     && bindingRecord.State.LifecycleState
                         != MissionAcgLifecycleState.Active)
                    || bindingRecord.State.CleanupState
                        != MissionAcgCleanupState.None))
            {
                failure =
                    "Durable mission binding no longer permits acceptance recovery.";
                return false;
            }

            if (!hasBinding)
            {
                if (projection.AcceptancePhase != MissionAcgAcceptancePhase.OfferClaimed)
                {
                    failure = "Accepted projection requires a missing durable binding.";
                    return false;
                }

                var reserved = new MissionAcgBindingRecord(
                    binding,
                    new MissionAcgInstanceState(
                        MissionAcgLifecycleState.Reserved,
                        MissionAcgCleanupState.None,
                        projection.UpdatedUtc,
                        null),
                    string.Empty);
                if (!MissionAcgBindingRuntime.TryPersistNew(
                    reserved,
                    out bindingRecord,
                    out failure))
                {
                    return false;
                }
            }

            if ((int)projection.AcceptancePhase
                < (int)MissionAcgAcceptancePhase.BindingPersisted)
            {
                if (!MissionAcgAcceptedProjectionRuntime.TryAdvancePhase(
                    projection,
                    MissionAcgAcceptancePhase.BindingPersisted,
                    DateTime.UtcNow,
                    out projection,
                    out failure))
                {
                    return false;
                }
            }

            MissionAcgObjectiveRecord objectiveRecord;
            if (!MissionAcgObjectiveRuntime.TryGetByAccepted(
                binding.OwnerIdentity.Instance,
                binding.AcceptedQuestIdentity.Instance,
                out objectiveRecord))
            {
                if (!MissionAcgObjectiveRuntime.TryCreateForBinding(
                    bindingRecord,
                    out objectiveRecord,
                    out failure))
                {
                    return false;
                }
            }

            if (projection.RuntimeObjectiveIdentity == null)
            {
                if (!MissionAcgAcceptedProjectionRuntime.TrySetObjective(
                    projection,
                    objectiveRecord.Binding.RuntimeObjectiveIdentity,
                    DateTime.UtcNow,
                    out projection,
                    out failure))
                {
                    return false;
                }
            }
            else if (!projection.RuntimeObjectiveIdentity.Equals(
                objectiveRecord.Binding.RuntimeObjectiveIdentity))
            {
                failure = "Objective identity does not match the accepted projection.";
                CleanupIrrecoverableAcceptance(
                    bindingRecord,
                    projection,
                    client,
                    character);
                return false;
            }

            if ((int)projection.AcceptancePhase
                < (int)MissionAcgAcceptancePhase.ObjectivePersisted)
            {
                if (!MissionAcgAcceptedProjectionRuntime.TryAdvancePhase(
                    projection,
                    MissionAcgAcceptancePhase.ObjectivePersisted,
                    DateTime.UtcNow,
                    out projection,
                    out failure))
                {
                    return false;
                }
            }

            if ((int)projection.AcceptancePhase
                < (int)MissionAcgAcceptancePhase.KeyGrantPending)
            {
                if (!MissionAcgAcceptedProjectionRuntime.TryAdvancePhase(
                    projection,
                    MissionAcgAcceptancePhase.KeyGrantPending,
                    DateTime.UtcNow,
                    out projection,
                    out failure))
                {
                    return false;
                }
            }

            var acceptedQuest = ToIdentity(binding.AcceptedQuestIdentity);
            IItem existingMissionKey;
            if (!MissionKeyGrantService.TryFindReservedMissionArtifact(
                character,
                binding.MissionKeyIdentity.Instance,
                false,
                out existingMissionKey))
            {
                InventoryError keyError;
                if (!MissionKeyGrantService.TryGrantReservedMissionKey(
                    client,
                    character,
                    binding.MissionKeyIdentity.Instance,
                    "Mission key",
                    out keyError))
                {
                    failure = "Mission key grant is durably pending: " + keyError + ".";
                    return false;
                }
            }

            MissionKeyStore.Register(
                character.Identity.Instance,
                acceptedQuest,
                binding.MissionKeyIdentity.Instance);
            if ((int)projection.AcceptancePhase
                < (int)MissionAcgAcceptancePhase.KeyGranted)
            {
                if (!MissionAcgAcceptedProjectionRuntime.TryAdvancePhase(
                    projection,
                    MissionAcgAcceptancePhase.KeyGranted,
                    DateTime.UtcNow,
                    out projection,
                    out failure))
                {
                    return false;
                }
            }

            if ((int)projection.AcceptancePhase
                < (int)MissionAcgAcceptancePhase.ArtifactGrantPending)
            {
                if (!MissionAcgAcceptedProjectionRuntime.TryAdvancePhase(
                    projection,
                    MissionAcgAcceptancePhase.ArtifactGrantPending,
                    DateTime.UtcNow,
                    out projection,
                    out failure))
                {
                    return false;
                }
            }

            if (binding.MissionType == MissionRollType.RepairMachine)
            {
                if (projection.MissionArtifactIdentity == null)
                {
                    failure = "Repair acceptance lacks its reserved component identity.";
                    return false;
                }

                IItem existingRepairItem;
                if (!MissionKeyGrantService.TryFindReservedMissionArtifact(
                    character,
                    projection.MissionArtifactIdentity.Instance,
                    true,
                    out existingRepairItem))
                {
                    InventoryError repairError;
                    if (!MissionKeyGrantService.TryGrantReservedRepairItem(
                        client,
                        character,
                        projection.MissionArtifactIdentity.Instance,
                        projection.RepairArtifactLowId,
                        projection.RepairArtifactHighId,
                        out repairError))
                    {
                        failure =
                            "Repair component grant is durably pending: "
                            + repairError
                            + ".";
                        return false;
                    }
                }

                MissionKeyStore.RegisterRepairKit(
                    character.Identity.Instance,
                    acceptedQuest,
                    projection.MissionArtifactIdentity.Instance);
                if (objectiveRecord.State.MissionItemIdentity == null)
                {
                    if (!MissionAcgObjectiveRuntime.TrySetMissionItem(
                        objectiveRecord,
                        projection.MissionArtifactIdentity,
                        out objectiveRecord,
                        out failure))
                    {
                        return false;
                    }
                }
                else if (!objectiveRecord.State.MissionItemIdentity.Equals(
                    projection.MissionArtifactIdentity))
                {
                    failure = "Repair objective owns a different component identity.";
                    CleanupIrrecoverableAcceptance(
                        bindingRecord,
                        projection,
                        client,
                        character);
                    return false;
                }
            }

            if ((int)projection.AcceptancePhase
                < (int)MissionAcgAcceptancePhase.ArtifactsGranted)
            {
                if (!MissionAcgAcceptedProjectionRuntime.TryAdvancePhase(
                    projection,
                    MissionAcgAcceptancePhase.ArtifactsGranted,
                    DateTime.UtcNow,
                    out projection,
                    out failure))
                {
                    return false;
                }
            }

            MissionAcgObjectiveLifecycle expectedObjectiveLifecycle =
                objectiveRecord.State.MissionItemIdentity == null
                    ? MissionAcgObjectiveLifecycle.Exposed
                    : MissionAcgObjectiveLifecycle.ItemPossessed;
            if (objectiveRecord.State.Lifecycle == MissionAcgObjectiveLifecycle.Reserved)
            {
                if (!MissionAcgObjectiveRuntime.TrySetLifecycle(
                    objectiveRecord,
                    expectedObjectiveLifecycle,
                    out objectiveRecord,
                    out failure))
                {
                    return false;
                }
            }
            else if (objectiveRecord.State.Lifecycle != expectedObjectiveLifecycle)
            {
                failure = "Objective is not in a recoverable acceptance lifecycle.";
                return false;
            }

            if ((int)projection.AcceptancePhase
                < (int)MissionAcgAcceptancePhase.ObjectiveExposed)
            {
                if (!MissionAcgAcceptedProjectionRuntime.TryAdvancePhase(
                    projection,
                    MissionAcgAcceptancePhase.ObjectiveExposed,
                    DateTime.UtcNow,
                    out projection,
                    out failure))
                {
                    return false;
                }
            }

            if (bindingRecord.State.LifecycleState == MissionAcgLifecycleState.Reserved)
            {
                if (!MissionAcgBindingRuntime.TryTransition(
                    bindingRecord,
                    MissionAcgLifecycleState.Accepted,
                    MissionAcgCleanupState.None,
                    DateTime.UtcNow,
                    out bindingRecord,
                    out failure))
                {
                    return false;
                }

                if (!MissionAcgAcceptedProjectionRuntime.TryGetByAcceptedQuest(
                    binding.AcceptedQuestIdentity.Instance,
                    out projection))
                {
                    failure = "Accepted projection disappeared after binding commit.";
                    return false;
                }
            }
            else if (bindingRecord.State.LifecycleState
                         != MissionAcgLifecycleState.Accepted
                     && bindingRecord.State.LifecycleState
                         != MissionAcgLifecycleState.Active)
            {
                failure = "Accepted mission is no longer in an enterable lifecycle.";
                return false;
            }

            if ((int)projection.AcceptancePhase
                < (int)MissionAcgAcceptancePhase.AcceptanceCommitted)
            {
                if (!MissionAcgAcceptedProjectionRuntime.TryAdvancePhase(
                    projection,
                    MissionAcgAcceptancePhase.AcceptanceCommitted,
                    DateTime.UtcNow,
                    out projection,
                    out failure))
                {
                    return false;
                }
            }

            if (!MissionAcceptedStore.TryRegisterGeneratedProjection(
                projection,
                out failure))
            {
                CleanupIrrecoverableAcceptance(
                    bindingRecord,
                    projection,
                    client,
                    character);
                return false;
            }

            if ((int)projection.AcceptancePhase
                < (int)MissionAcgAcceptancePhase.QfuPending)
            {
                if (!MissionAcgAcceptedProjectionRuntime.TryAdvancePhase(
                    projection,
                    MissionAcgAcceptancePhase.QfuPending,
                    DateTime.UtcNow,
                    out projection,
                    out failure))
                {
                    return false;
                }
            }

            QuestInfo acceptedOffer;
            try
            {
                acceptedOffer = projection.ReconstructOffer();
            }
            catch (Exception ex)
            {
                failure = "Accepted QFU reconstruction failed closed: " + ex.Message;
                return false;
            }

            if (!MissionAcceptService.SendAcceptedGeneratedMission(
                character,
                acceptedOffer,
                bindingRecord))
            {
                failure = "Accepted mission QFU delivery remains durably pending.";
                return false;
            }

            if ((int)projection.AcceptancePhase
                < (int)MissionAcgAcceptancePhase.QfuSent)
            {
                if (!MissionAcgAcceptedProjectionRuntime.TryAdvancePhase(
                    projection,
                    MissionAcgAcceptancePhase.QfuSent,
                    DateTime.UtcNow,
                    out projection,
                    out failure))
                {
                    return false;
                }
            }

            accepted = bindingRecord;
            return true;
        }

        private static void CleanupIrrecoverableAcceptance(
            MissionAcgBindingRecord bindingRecord,
            MissionAcgAcceptedProjection projection,
            IZoneClient client,
            ICharacter character)
        {
            if (bindingRecord == null || projection == null || character == null)
            {
                return;
            }

            string cleanupFailure;
            MissionAcgBindingRecord cleanupPending = bindingRecord;
            if (bindingRecord.State.LifecycleState
                    != MissionAcgLifecycleState.CleanupPending
                && bindingRecord.State.LifecycleState
                    != MissionAcgLifecycleState.Cleaned
                && !MissionAcgBindingRuntime.TryTransition(
                    bindingRecord,
                    MissionAcgLifecycleState.CleanupPending,
                    MissionAcgCleanupState.InstanceReleasePending,
                    DateTime.UtcNow,
                    out cleanupPending,
                    out cleanupFailure))
            {
                MissionDiagnostics.Log(
                    "ACG-ACCEPT-INVALID-CLEANUP-PENDING accepted={0}:{1} reason={2}",
                    bindingRecord.Binding.AcceptedQuestIdentity.Type,
                    bindingRecord.Binding.AcceptedQuestIdentity.Instance,
                    cleanupFailure);
                return;
            }

            MissionAcgBindingRecord cleaned;
            if (!MissionAcgLifecycleService.TryCleanupOwnedRecord(
                client,
                character,
                cleanupPending,
                out cleaned,
                out cleanupFailure))
            {
                MissionDiagnostics.Log(
                    "ACG-ACCEPT-INVALID-CLEANUP-PENDING accepted={0}:{1} reason={2}",
                    bindingRecord.Binding.AcceptedQuestIdentity.Type,
                    bindingRecord.Binding.AcceptedQuestIdentity.Instance,
                    cleanupFailure);
            }
        }

        private static bool ValidateOffer(
            MissionOfferRecord record,
            MissionRollType missionType,
            out string failure)
        {
            failure = string.Empty;
            QuestInfo offer = record == null ? null : record.Offer;
            if (offer == null
                || missionType == MissionRollType.Unknown
                || offer.Quality <= 0
                || offer.QuestIdentity == null
                || offer.QuestIdentity.Instance <= 0
                || offer.Unknown5 == null
                || offer.Unknown5.Instance <= 0
                || offer.QuestActions == null
                || offer.QuestActions.Length == 0
                || offer.QuestActions[0] == null
                || offer.QuestActions[0].Playfield == null
                || offer.QuestActions[0].Playfield.Instance <= 0
                || record.SerializedRollPayload == null
                || record.SerializedRollPayload.Length == 0
                || record.ExpiresUtc <= DateTime.UtcNow)
            {
                failure = "Offer lacks exact generated-mission projection or entrance evidence.";
                return false;
            }

            return true;
        }

        private static void ResolveQfuContract(
            MissionRollType missionType,
            out int version,
            out int flag)
        {
            flag = 0;
            switch (missionType)
            {
                case MissionRollType.KillPerson:
                    version = 16;
                    return;
                case MissionRollType.FindPerson:
                    version = 16;
                    flag = 64;
                    return;
                case MissionRollType.FindItem:
                    version = 15;
                    return;
                case MissionRollType.FindItemReturn:
                    version = 8;
                    return;
                case MissionRollType.RepairMachine:
                    version = 16;
                    return;
                default:
                    throw new ArgumentOutOfRangeException("missionType");
            }
        }

        private static int DeriveMissionSeed(
            QuestInfo offer,
            MissionAcgIdentityRecord owner,
            MissionRollType missionType)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + offer.QuestIdentity.Type.GetHashCode();
                hash = (hash * 31) + offer.QuestIdentity.Instance;
                hash = (hash * 31) + owner.Type;
                hash = (hash * 31) + owner.Instance;
                hash = (hash * 31) + (int)missionType;
                hash = (hash * 31) + offer.Quality;
                return hash;
            }
        }

        private static MissionAcgIdentityRecord ToRecord(Identity identity)
        {
            if (identity == null || identity.Instance <= 0)
            {
                throw new ArgumentException("Concrete identity is required.", "identity");
            }

            return new MissionAcgIdentityRecord((int)identity.Type, identity.Instance);
        }

        private static Identity ToIdentity(MissionAcgIdentityRecord identity)
        {
            return new Identity
                   {
                       Type = (IdentityType)identity.Type,
                       Instance = identity.Instance
                   };
        }
    }
}
