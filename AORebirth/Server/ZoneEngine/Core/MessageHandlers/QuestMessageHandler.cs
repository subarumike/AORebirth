namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Handles the client's mission-journal actions. When the player deletes a mission from the mission
    /// window the client sends a QuestMessage with Action=Delete. The captured official flow
    /// (capture 20260717-185345) is: the server echoes the Delete back to confirm the window removal, then
    /// destroys the associated mission key in the inventory. Without this the mission stays in the window and
    /// the key is orphaned.
    /// </summary>
    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class QuestMessageHandler : BaseMessageHandler<QuestMessage, QuestMessageHandler>
    {
        protected override void Read(QuestMessage message, IZoneClient client)
        {
            if (client == null || client.Controller == null || client.Controller.Character == null)
            {
                return;
            }

            if (message.Action != QuestAction.Delete)
            {
                return;
            }

            ICharacter character = client.Controller.Character;

            try
            {
                // Prefer the stored DAC3 identity — client Delete sometimes sends Type=0 / wrong type,
                // and echoing that leaves the journal entry stuck (LOGIN-RESYNC resurrects it).
                Identity deleteMission = message.Mission;
                MissionAcceptedStore.AcceptedMission stored;
                if (MissionAcceptedStore.TryResolve(character.Identity.Instance, message.Mission, out stored)
                    && stored != null && stored.QuestIdentity != null)
                {
                    deleteMission = stored.QuestIdentity;
                }

                if (deleteMission == null || deleteMission.Instance == 0)
                {
                    MissionDiagnostics.Log(
                        "JOURNAL-DELETE-FAIL char={0} reason=no-mission-id",
                        character.Identity.Instance);
                    return;
                }

                client.SendCompressed(
                    new QuestMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        Action = QuestAction.Delete,
                        Unknown1 = 0,
                        Mission = deleteMission,
                        Unknown2 = 0,
                        Unknown3 = 0
                    });

                MissionAcgBindingRecord generatedBinding;
                if (MissionAcgBindingRuntime.TryGetOwnedByAcceptedQuest(
                    character.Identity.Instance,
                    deleteMission.Instance,
                    out generatedBinding))
                {
                    MissionAcgBindingRecord abandoned;
                    string lifecycleFailure;
                    if (!MissionAcgBindingRuntime.TryTransition(
                        generatedBinding,
                        MissionAcgLifecycleState.Abandoned,
                        MissionAcgCleanupState.KeyRemovalPending,
                        DateTime.UtcNow,
                        out abandoned,
                        out lifecycleFailure))
                    {
                        MissionDiagnostics.Log(
                            "JOURNAL-DELETE-FAIL char={0} mission={1:X8} lifecycle={2}",
                            character.Identity.Instance,
                            deleteMission.Instance,
                            lifecycleFailure);
                        return;
                    }

                    int exactKey = generatedBinding.Binding.MissionKeyIdentity.Instance;
                    bool exactKeyRemoved = MissionKeyGrantService.TryRemoveMissionKey(
                        client,
                        character,
                        exactKey);
                    int exactKit;
                    bool exactKitRemoved =
                        MissionKeyStore.TryTakeRepairKit(
                            character.Identity.Instance,
                            deleteMission,
                            out exactKit)
                        && MissionKeyGrantService.TryRemoveRepairItem(
                            client,
                            character,
                            exactKit);
                    int ignoredKey;
                    MissionKeyStore.TryTake(
                        character.Identity.Instance,
                        deleteMission,
                        out ignoredKey);
                    bool exactStoreRemoved =
                        MissionAcceptedStore.Remove(
                            character.Identity.Instance,
                            deleteMission);

                    MissionAcgBindingRecord cleanupPending;
                    MissionAcgBindingRecord cleaned;
                    if (!MissionAcgBindingRuntime.TryTransition(
                        abandoned,
                        MissionAcgLifecycleState.CleanupPending,
                        MissionAcgCleanupState.InstanceReleasePending,
                        DateTime.UtcNow,
                        out cleanupPending,
                        out lifecycleFailure)
                        || !MissionAcgBindingRuntime.TryTransition(
                            cleanupPending,
                            MissionAcgLifecycleState.Cleaned,
                            MissionAcgCleanupState.Completed,
                            DateTime.UtcNow,
                            out cleaned,
                            out lifecycleFailure))
                    {
                        MissionDiagnostics.Log(
                            "JOURNAL-DELETE-CLEANUP-PENDING char={0} mission={1:X8} reason={2}",
                            character.Identity.Instance,
                            deleteMission.Instance,
                            lifecycleFailure);
                        return;
                    }

                    MissionDiagnostics.Log(
                        "JOURNAL-DELETE-BOUND char={0} mission={1:X8} livePf2={2} keyRemoved={3} kitRemoved={4} storeRemoved={5}",
                        character.Identity.Instance,
                        deleteMission.Instance,
                        generatedBinding.Binding.AllocatedLivePlayfield2,
                        exactKeyRemoved,
                        exactKitRemoved,
                        exactStoreRemoved);
                    return;
                }

                int kitInstance;
                bool kitRemoved = false;
                if (MissionKeyStore.TryTakeRepairKit(character.Identity.Instance, deleteMission, out kitInstance))
                {
                    kitRemoved = MissionKeyGrantService.TryRemoveRepairItem(client, character, kitInstance);
                }
                else if (IsDeletedRepairMission(character.Identity.Instance, deleteMission))
                {
                    kitRemoved = MissionKeyGrantService.TryRemoveAnyRepairItem(client, character);
                }

                int keyInstance;
                bool keyRemoved = false;
                if (MissionKeyStore.TryTake(character.Identity.Instance, deleteMission, out keyInstance))
                {
                    keyRemoved = MissionKeyGrantService.TryRemoveMissionKey(client, character, keyInstance);
                }

                if (!keyRemoved)
                {
                    keyRemoved = MissionKeyGrantService.TryRemoveAnyMissionKey(client, character);
                }

                bool storeRemoved = MissionAcceptedStore.Remove(character.Identity.Instance, deleteMission);
                MissionTokenProgressTracker.ClearCharacter(character.Identity.Instance);
                MissionFindItemService.ClearCharacter(character.Identity.Instance);

                MissionDiagnostics.Log(
                    "JOURNAL-DELETE char={0} mission={1:X8} type={2:X} kitRemoved={3} keyRemoved={4} storeRemoved={5}",
                    character.Identity.Instance,
                    deleteMission.Instance,
                    (int)deleteMission.Type,
                    kitRemoved,
                    keyRemoved,
                    storeRemoved);

                client.Server.Info(
                    client,
                    "Quest delete mission={0} kitRemoved={1} keyRemoved={2} storeRemoved={3}",
                    deleteMission,
                    kitRemoved,
                    keyRemoved,
                    storeRemoved);
            }
            catch (Exception ex)
            {
                MissionDiagnostics.Log(
                    "JOURNAL-DELETE-FAIL char={0} err={1}",
                    character.Identity.Instance,
                    ex.Message);
                client.Server.Info(client, "Quest delete failed: {0}", ex);
            }
        }

        private static bool IsDeletedRepairMission(int characterInstance, Identity mission)
        {
            if (mission == null)
            {
                return false;
            }

            List<MissionAcceptedStore.AcceptedMission> all = MissionAcceptedStore.GetAll(characterInstance);
            for (int i = 0; i < all.Count; i++)
            {
                MissionAcceptedStore.AcceptedMission entry = all[i];
                if (entry == null || entry.QuestIdentity == null)
                {
                    continue;
                }

                if (entry.QuestIdentity.Instance == mission.Instance)
                {
                    return MissionRepairService.IsRepairMission(entry);
                }
            }

            return false;
        }
    }
}
