namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// Exact owner/PF2/runtime-identity routing for captured ACG objects.
    /// Objective, reward, and loot outcomes remain deferred.
    /// </summary>
    internal static class MissionAcgRuntimeInteractionService
    {
        internal static bool TryHandleUse(
            IZoneClient client,
            GenericCmdMessage message,
            Identity target)
        {
            if (client == null
                || client.Controller == null
                || target == null)
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            if (character == null
                || character.Playfield == null
                || !MissionAcgBindingRuntime.IsBoundLivePlayfield(
                    character.Playfield.Identity.Instance))
            {
                return false;
            }

            if (!MissionAcgRuntimeManager.IsRuntimeIdentityCandidate(
                character.Playfield.Identity.Instance,
                target))
            {
                return false;
            }

            MissionAcgMaterializedInstance instance;
            MissionAcgRuntimeObject runtimeObject;
            if (!MissionAcgRuntimeManager.TryResolveObject(
                character.Identity.Instance,
                character.Playfield.Identity.Instance,
                target,
                out instance,
                out runtimeObject))
            {
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
            }

            switch (runtimeObject.Identity.Kind)
            {
                case MissionAcgRuntimeObjectKind.Exit:
                    if (!MissionInstanceService.TryExitMissionInstance(client))
                    {
                        GenericCmdMessageHandler.Default.AcknowledgeDenied(
                            character,
                            message);
                    }
                    else
                    {
                        GenericCmdMessageHandler.Default.Acknowledge(character, message);
                    }

                    return true;
                case MissionAcgRuntimeObjectKind.Door:
                    bool isOpen;
                    string doorFailure;
                    if (!MissionAcgRuntimeManager.TryToggleDoor(
                        instance,
                        target.Instance,
                        out isOpen,
                        out doorFailure))
                    {
                        GenericCmdMessageHandler.Default.AcknowledgeDenied(
                            character,
                            message);
                        return true;
                    }

                    GenericCmdMessageHandler.Default.Acknowledge(character, message);
                    MissionDiagnostics.Log(
                        "ACG-DOOR char={0} accepted={1}:{2} livePf2={3} runtime={4}:{5} open={6}",
                        character.Identity.Instance,
                        instance.BindingRecord.Binding.AcceptedQuestIdentity.Type,
                        instance.BindingRecord.Binding.AcceptedQuestIdentity.Instance,
                        instance.BindingRecord.Binding.AllocatedLivePlayfield2,
                        runtimeObject.Identity.RuntimeIdentity.Type,
                        runtimeObject.Identity.RuntimeIdentity.Instance,
                        isOpen ? 1 : 0);
                    return true;
                case MissionAcgRuntimeObjectKind.Chest:
                    bool alreadyOpen;
                    string chestFailure;
                    if (!MissionAcgRuntimeManager.TryOpenChest(
                        instance,
                        target.Instance,
                        out alreadyOpen,
                        out chestFailure))
                    {
                        GenericCmdMessageHandler.Default.AcknowledgeDenied(
                            character,
                            message);
                        return true;
                    }

                    GenericCmdMessageHandler.Default.Acknowledge(character, message);
                    MissionDiagnostics.Log(
                        "ACG-CHEST char={0} accepted={1}:{2} livePf2={3} runtime={4}:{5} alreadyOpen={6}",
                        character.Identity.Instance,
                        instance.BindingRecord.Binding.AcceptedQuestIdentity.Type,
                        instance.BindingRecord.Binding.AcceptedQuestIdentity.Instance,
                        instance.BindingRecord.Binding.AllocatedLivePlayfield2,
                        runtimeObject.Identity.RuntimeIdentity.Type,
                        runtimeObject.Identity.RuntimeIdentity.Instance,
                        alreadyOpen ? 1 : 0);
                    return true;
                case MissionAcgRuntimeObjectKind.MissionTerminal:
                case MissionAcgRuntimeObjectKind.RepairMachine:
                case MissionAcgRuntimeObjectKind.StaticObjective:
                case MissionAcgRuntimeObjectKind.ObjectiveNpc:
                    bool objectiveAccepted;
                    if (!MissionAcgObjectiveInteractionService.TryHandleRuntimeUse(
                        client,
                        instance,
                        runtimeObject,
                        out objectiveAccepted)
                        || !objectiveAccepted)
                    {
                        GenericCmdMessageHandler.Default.AcknowledgeDenied(
                            character,
                            message);
                    }
                    else
                    {
                        GenericCmdMessageHandler.Default.Acknowledge(
                            character,
                            message);
                    }

                    return true;
                case MissionAcgRuntimeObjectKind.AmbientNpc:
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                    return true;
                default:
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                    return true;
            }
        }
    }
}
