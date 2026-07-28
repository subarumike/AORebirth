namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Network;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    internal static class MissionAcgAcceptanceCoordinator
    {
        private const int MissionDurationSeconds = 48 * 60 * 60;

        internal static bool TryAccept(
            IZoneClient client,
            ICharacter character,
            QuestInfo offer,
            out MissionAcgBindingRecord accepted,
            out string failure)
        {
            accepted = null;
            failure = string.Empty;
            if (client == null || character == null || offer == null)
            {
                failure = "Player and selected offer are required.";
                return false;
            }

            MissionRollType missionType =
                MissionTypeCatalog.TypeFromIcon(offer.MissionIconId);
            if (missionType == MissionRollType.Unknown
                || offer.Quality <= 0
                || offer.QuestIdentity == null
                || offer.QuestIdentity.Instance == 0
                || offer.Unknown5 == null
                || offer.Unknown5.Instance == 0
                || offer.QuestActions == null
                || offer.QuestActions.Length == 0
                || offer.QuestActions[0] == null
                || offer.QuestActions[0].Playfield == null
                || offer.QuestActions[0].Playfield.Instance == 0)
            {
                failure = "Offer lacks exact generated-mission identity or entrance evidence.";
                return false;
            }

            MissionAcgAllocationService allocator =
                MissionAcgBindingRuntime.Allocator;
            MissionAcgIdentityRecord acceptedIdentity;
            MissionAcgIdentityRecord keyIdentity = null;
            int livePlayfield2 = 0;
            if (!allocator.TryReserveAcceptedQuestIdentity(out acceptedIdentity))
            {
                failure = "Accepted mission identity range is exhausted.";
                return false;
            }

            MissionAcgIdentityRecord ownerIdentity =
                ToRecord(character.Identity);
            int missionSeed = DeriveMissionSeed(
                offer,
                ownerIdentity,
                missionType);
            MissionAcgLayoutBundle bundle;
            try
            {
                bundle = MissionAcgLayoutSelector.Select(
                    MissionAcgBindingRuntime.Catalog,
                    new MissionAcgSelectionInput(
                        missionSeed,
                        missionType,
                        offer.Quality,
                        ownerIdentity));
            }
            catch (Exception ex)
            {
                allocator.RollbackUnpersisted(acceptedIdentity, null, 0);
                failure = "ACG bundle selection failed: " + ex.Message;
                return false;
            }

            if (!allocator.TryReservePlayfield(out livePlayfield2))
            {
                allocator.RollbackUnpersisted(acceptedIdentity, null, 0);
                failure = "Mission PF2 allocation range is exhausted.";
                return false;
            }

            if (!allocator.TryReserveMissionKeyIdentity(out keyIdentity))
            {
                allocator.RollbackUnpersisted(
                    acceptedIdentity,
                    null,
                    livePlayfield2);
                failure = "Mission key identity range is exhausted.";
                return false;
            }

            QuestActionList action = offer.QuestActions[0];
            DateTime acceptedUtc = DateTime.UtcNow;
            DateTime expiryUtc = acceptedUtc.AddSeconds(MissionDurationSeconds);
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
                expiryUtc);
            var reserved = new MissionAcgBindingRecord(
                binding,
                new MissionAcgInstanceState(
                    MissionAcgLifecycleState.Reserved,
                    MissionAcgCleanupState.None,
                    acceptedUtc,
                    null),
                string.Empty);

            MissionAcgBindingRecord persisted;
            if (!MissionAcgBindingRuntime.TryPersistNew(
                reserved,
                out persisted,
                out failure))
            {
                allocator.RollbackUnpersisted(
                    acceptedIdentity,
                    keyIdentity,
                    livePlayfield2);
                return false;
            }

            var acceptedQuest =
                new Identity
                {
                    Type = (IdentityType)acceptedIdentity.Type,
                    Instance = acceptedIdentity.Instance
                };
            if (!MissionAcceptedStore.TryRegisterGenerated(
                character.Identity.Instance,
                acceptedQuest,
                offer,
                expiryUtc,
                out failure))
            {
                CleanupFailedAcceptance(persisted, client, character, false, 0, false, 0);
                return false;
            }

            InventoryError inventoryError;
            if (!MissionKeyGrantService.TryGrantReservedMissionKey(
                client,
                character,
                keyIdentity.Instance,
                "Mission key",
                out inventoryError))
            {
                failure = "Mission key grant failed: " + inventoryError + ".";
                MissionAcceptedStore.Remove(character.Identity.Instance, acceptedQuest);
                CleanupFailedAcceptance(persisted, client, character, false, 0, false, 0);
                return false;
            }

            MissionKeyStore.Register(
                character.Identity.Instance,
                acceptedQuest,
                keyIdentity.Instance);

            bool repairGranted = false;
            int repairInstance = 0;
            if (missionType == MissionRollType.RepairMachine)
            {
                repairGranted = MissionKeyGrantService.TryGrantRepairItem(
                    client,
                    character,
                    1,
                    out repairInstance,
                    out inventoryError);
                if (!repairGranted)
                {
                    failure = "Repair tool grant failed: " + inventoryError + ".";
                    MissionAcceptedStore.Remove(character.Identity.Instance, acceptedQuest);
                    CleanupFailedAcceptance(
                        persisted,
                        client,
                        character,
                        true,
                        keyIdentity.Instance,
                        false,
                        0);
                    return false;
                }

                MissionKeyStore.RegisterRepairKit(
                    character.Identity.Instance,
                    acceptedQuest,
                    repairInstance);
            }

            if (!MissionAcceptService.SendAcceptedGeneratedMission(
                character,
                offer,
                persisted))
            {
                failure = "Accepted mission QFU send failed.";
                MissionAcceptedStore.Remove(character.Identity.Instance, acceptedQuest);
                CleanupFailedAcceptance(
                    persisted,
                    client,
                    character,
                    true,
                    keyIdentity.Instance,
                    repairGranted,
                    repairInstance);
                return false;
            }

            MissionAcgBindingRecord finalized;
            if (!MissionAcgBindingRuntime.TryTransition(
                persisted,
                MissionAcgLifecycleState.Accepted,
                MissionAcgCleanupState.None,
                DateTime.UtcNow,
                out finalized,
                out failure))
            {
                // The reserved durable record is deliberately retained for startup reconciliation.
                MissionAcceptedStore.Remove(character.Identity.Instance, acceptedQuest);
                int ignoredMappedKey;
                MissionKeyStore.TryTake(
                    character.Identity.Instance,
                    acceptedQuest,
                    out ignoredMappedKey);
                MissionKeyGrantService.TryRemoveMissionKey(
                    client,
                    character,
                    keyIdentity.Instance);
                if (repairGranted)
                {
                    int ignoredMappedRepair;
                    MissionKeyStore.TryTakeRepairKit(
                        character.Identity.Instance,
                        acceptedQuest,
                        out ignoredMappedRepair);
                    MissionKeyGrantService.TryRemoveRepairItem(
                        client,
                        character,
                        repairInstance);
                }

                failure =
                    "Acceptance-complete persistence failed; reserved record remains recoverable: "
                    + failure;
                return false;
            }

            accepted = finalized;
            return true;
        }

        private static void CleanupFailedAcceptance(
            MissionAcgBindingRecord persisted,
            IZoneClient client,
            ICharacter character,
            bool removeKey,
            int keyInstance,
            bool removeRepair,
            int repairInstance)
        {
            if (removeKey)
            {
                int ignoredKey;
                MissionKeyStore.TryTake(
                    character.Identity.Instance,
                    ToIdentity(persisted.Binding.AcceptedQuestIdentity),
                    out ignoredKey);
                MissionKeyGrantService.TryRemoveMissionKey(client, character, keyInstance);
            }

            if (removeRepair)
            {
                int ignoredRepair;
                MissionKeyStore.TryTakeRepairKit(
                    character.Identity.Instance,
                    ToIdentity(persisted.Binding.AcceptedQuestIdentity),
                    out ignoredRepair);
                MissionKeyGrantService.TryRemoveRepairItem(
                    client,
                    character,
                    repairInstance);
            }

            MissionAcgBindingRecord cleanupPending;
            string ignored;
            if (MissionAcgBindingRuntime.TryTransition(
                persisted,
                MissionAcgLifecycleState.CleanupPending,
                MissionAcgCleanupState.InstanceReleasePending,
                DateTime.UtcNow,
                out cleanupPending,
                out ignored))
            {
                MissionAcgBindingRecord cleaned;
                MissionAcgBindingRuntime.TryTransition(
                    cleanupPending,
                    MissionAcgLifecycleState.Cleaned,
                    MissionAcgCleanupState.Completed,
                    DateTime.UtcNow,
                    out cleaned,
                    out ignored);
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
            return identity == null
                       ? null
                       : new MissionAcgIdentityRecord(
                           (int)identity.Type,
                           identity.Instance);
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
