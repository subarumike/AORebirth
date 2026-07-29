namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;
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

                MissionAcgBindingRecord generatedBinding;
                if (MissionAcgBindingRuntime.TryGetOwnedByAcceptedQuest(
                    character.Identity.Instance,
                    deleteMission.Instance,
                    out generatedBinding))
                {
                    MissionAcgBindingRecord cleaned;
                    string lifecycleFailure;
                    if (!TryAbandonGeneratedMission(
                            client,
                            character,
                            generatedBinding,
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

                    SendDeleteAcknowledgement(client, character, deleteMission);
                    MissionDiagnostics.Log(
                        "JOURNAL-DELETE-BOUND char={0} mission={1:X8} livePf2={2} lifecycle={3} cleanup={4}",
                        character.Identity.Instance,
                        deleteMission.Instance,
                        generatedBinding.Binding.AllocatedLivePlayfield2,
                        cleaned.State.LifecycleState,
                        cleaned.State.CleanupState);
                    return;
                }

                if (stored == null)
                {
                    MissionDiagnostics.Log(
                        "JOURNAL-DELETE-IGNORE char={0} mission={1:X8} reason=not-owned-terminal-mission",
                        character.Identity.Instance,
                        deleteMission.Instance);
                    return;
                }

                int kitInstance;
                bool kitRemoved = false;
                if (MissionKeyStore.TryTakeRepairKit(character.Identity.Instance, deleteMission, out kitInstance))
                {
                    kitRemoved = MissionKeyGrantService.TryRemoveRepairItem(client, character, kitInstance);
                }

                int keyInstance;
                bool keyRemoved = false;
                if (MissionKeyStore.TryTakeExact(character.Identity.Instance, deleteMission, out keyInstance))
                {
                    keyRemoved = MissionKeyGrantService.TryRemoveMissionKey(client, character, keyInstance);
                }

                bool storeRemoved = MissionAcceptedStore.Remove(character.Identity.Instance, deleteMission);
                SendDeleteAcknowledgement(client, character, deleteMission);

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

        private static bool TryAbandonGeneratedMission(
            IZoneClient client,
            ICharacter character,
            MissionAcgBindingRecord generatedBinding,
            out MissionAcgBindingRecord cleaned,
            out string failure)
        {
            cleaned = null;
            failure = string.Empty;
            if (generatedBinding == null)
            {
                failure = "Exact generated-mission binding is required.";
                return false;
            }

            MissionAcgObjectiveRecord objective;
            if (!MissionAcgObjectiveRuntime.TryGetByAccepted(
                character.Identity.Instance,
                generatedBinding.Binding.AcceptedQuestIdentity.Instance,
                out objective))
            {
                failure = "Exact generated-mission objective record is unavailable.";
                return false;
            }

            MissionAcgObjectiveLifecycle targetObjectiveLifecycle;
            switch (generatedBinding.State.LifecycleState)
            {
                case MissionAcgLifecycleState.Accepted:
                case MissionAcgLifecycleState.Active:
                case MissionAcgLifecycleState.Abandoned:
                    targetObjectiveLifecycle = MissionAcgObjectiveLifecycle.Abandoned;
                    break;
                case MissionAcgLifecycleState.Expired:
                    targetObjectiveLifecycle = MissionAcgObjectiveLifecycle.Expired;
                    break;
                case MissionAcgLifecycleState.CleanupPending:
                case MissionAcgLifecycleState.Cleaned:
                    if (objective.State.Lifecycle
                        != MissionAcgObjectiveLifecycle.Abandoned
                        && objective.State.Lifecycle
                        != MissionAcgObjectiveLifecycle.Expired
                        && objective.State.Lifecycle
                        != MissionAcgObjectiveLifecycle.CleanupCompleted)
                    {
                        failure =
                            "Generated-mission cleanup has no durable abandonment or expiry owner.";
                        return false;
                    }

                    targetObjectiveLifecycle = objective.State.Lifecycle;
                    break;
                default:
                    failure =
                        "Generated-mission lifecycle is owned by completion or is not abandonable.";
                    return false;
            }

            bool objectiveNeedsTransition =
                objective.State.Lifecycle != targetObjectiveLifecycle
                && objective.State.Lifecycle
                   != MissionAcgObjectiveLifecycle.CleanupCompleted;
            if (objectiveNeedsTransition
                && (objective.State.Phase
                        >= MissionAcgCompletionPhase.RewardClaimStarted
                    || objective.State.Lifecycle
                       == MissionAcgObjectiveLifecycle.CompletionStarted
                    || objective.State.Lifecycle
                       == MissionAcgObjectiveLifecycle.Completed
                    || objective.State.Lifecycle
                       == MissionAcgObjectiveLifecycle.Abandoned
                    || objective.State.Lifecycle
                       == MissionAcgObjectiveLifecycle.Expired
                    || objective.State.Lifecycle
                       == MissionAcgObjectiveLifecycle.Invalid))
            {
                failure =
                    "Generated-mission objective lifecycle is owned by completion or another terminal outcome.";
                return false;
            }

            MissionAcgBindingRecord terminal = generatedBinding;
            if (generatedBinding.State.LifecycleState
                == MissionAcgLifecycleState.Accepted
                || generatedBinding.State.LifecycleState
                == MissionAcgLifecycleState.Active)
            {
                if (!MissionAcgBindingRuntime.TryTransition(
                    generatedBinding,
                    MissionAcgLifecycleState.Abandoned,
                    MissionAcgCleanupState.KeyRemovalPending,
                    DateTime.UtcNow,
                    out terminal,
                    out failure))
                {
                    return false;
                }
            }

            if (!MissionAcgObjectiveRuntime.TryGetByAccepted(
                character.Identity.Instance,
                generatedBinding.Binding.AcceptedQuestIdentity.Instance,
                out objective))
            {
                failure =
                    "Exact generated-mission objective record became unavailable.";
                return false;
            }

            if (objective.State.Lifecycle != targetObjectiveLifecycle
                && objective.State.Lifecycle
                   != MissionAcgObjectiveLifecycle.CleanupCompleted)
            {
                MissionAcgObjectiveRecord updatedObjective;
                if (!MissionAcgObjectiveRuntime.TrySetLifecycle(
                    objective,
                    targetObjectiveLifecycle,
                    out updatedObjective,
                    out failure))
                {
                    return false;
                }
            }

            return MissionAcgLifecycleService.TryCleanupOwnedRecord(
                client,
                character,
                terminal,
                out cleaned,
                out failure);
        }

        private static void SendDeleteAcknowledgement(
            IZoneClient client,
            ICharacter character,
            Identity mission)
        {
            client.SendCompressed(
                new QuestMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = QuestAction.Delete,
                    Unknown1 = 0,
                    Mission = mission,
                    Unknown2 = 0,
                    Unknown3 = 0
                });
        }
    }
}
