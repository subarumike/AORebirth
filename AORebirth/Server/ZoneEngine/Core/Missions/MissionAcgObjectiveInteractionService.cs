namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Playfields;

    #endregion

    internal static class MissionAcgObjectiveInteractionService
    {
        internal static bool TryHandleRuntimeUse(
            IZoneClient client,
            MissionAcgMaterializedInstance instance,
            MissionAcgRuntimeObject runtimeObject,
            out bool accepted)
        {
            accepted = false;
            if (client == null
                || client.Controller == null
                || instance == null
                || runtimeObject == null)
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            MissionAcgObjectiveRecord objective;
            if (character == null
                || !MissionAcgObjectiveRuntime.TryResolveRuntime(
                    character.Identity.Instance,
                    instance.BindingRecord.Binding.AllocatedLivePlayfield2,
                    runtimeObject.Identity.RuntimeIdentity,
                    out objective))
            {
                return false;
            }

            if (objective.Binding.MissionType != MissionRollType.FindItem
                && objective.Binding.MissionType != MissionRollType.FindItemReturn)
            {
                return true;
            }

            if (objective.State.MissionItemIdentity != null)
            {
                return true;
            }

            MissionAcgObjectiveRecord withItem = objective;
            int itemInstance;
            InventoryError error;
            if (!MissionKeyGrantService.TryGrantNamedItem(
                client,
                character,
                objective.Binding.ObjectiveTemplateId,
                objective.Binding.ObjectiveTemplateId,
                instance.BindingRecord.Binding.MissionQuality,
                objective.Binding.MissionType == MissionRollType.FindItem
                    ? "Mission Find Item"
                    : "Mission Return Item",
                out itemInstance,
                out error))
            {
                return true;
            }

            string failure;
            if (!MissionAcgObjectiveRuntime.TrySetMissionItem(
                objective,
                new MissionAcgIdentityRecord(0x0000C76D, itemInstance),
                out withItem,
                out failure))
            {
                string cleanupFailure;
                MissionAcgCompletionJournalService.TryRemoveExactInventoryItem(
                    client,
                    character,
                    new MissionAcgIdentityRecord(0x0000C76D, itemInstance),
                    out cleanupFailure);
                return true;
            }

            if (objective.Binding.MissionType == MissionRollType.FindItemReturn)
            {
                accepted = true;
                return true;
            }

            accepted = Complete(
                client,
                character,
                instance.BindingRecord,
                withItem,
                MissionAcgObjectiveInteraction.StaticItemPickup,
                runtimeObject.Identity.RuntimeIdentity,
                runtimeObject.TemplateId,
                runtimeObject.Name,
                withItem.State.MissionItemIdentity,
                0,
                null,
                "FindItemPickup");
            return true;
        }

        internal static bool TryHandleInfoRequest(
            IZoneClient client,
            Identity runtimeIdentity)
        {
            if (client == null
                || client.Controller == null
                || runtimeIdentity == null)
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            MissionAcgObjectiveRecord objective;
            MissionAcgBindingRecord binding;
            if (!MissionAcgObjectiveRuntime.TryResolveRuntime(
                    character.Identity.Instance,
                    character.Playfield.Identity.Instance,
                    ToRecord(runtimeIdentity),
                    out objective)
                || objective.Binding.MissionType != MissionRollType.FindPerson
                || !MissionAcgBindingRuntime.TryGetOwnedByAcceptedQuest(
                    character.Identity.Instance,
                    objective.Binding.AcceptedQuestIdentity.Instance,
                    out binding))
            {
                return false;
            }

            string spatialFailure;
            if (!MissionAcgSpatialRuntime.TryValidateObjectiveRuntimeInteraction(
                character,
                binding,
                objective.Binding.RuntimeObjectiveIdentity,
                NpcCombatAttackRules.MaxMeleeCombatDistance,
                "find-person-info",
                out spatialFailure))
            {
                return true;
            }

            return Complete(
                client,
                character,
                binding,
                objective,
                MissionAcgObjectiveInteraction.InfoRequest,
                objective.Binding.RuntimeObjectiveIdentity,
                objective.Binding.ObjectiveTemplateId,
                objective.Binding.ObjectiveName,
                null,
                0,
                null,
                "FindPersonInfoRequest");
        }

        internal static bool TryHandleTargetDeath(
            ICharacter attacker,
            ICharacter victim)
        {
            if (attacker == null
                || victim == null
                || attacker.Controller == null
                || victim.Playfield == null)
            {
                return false;
            }

            var client = attacker.Controller.Client as IZoneClient;
            MissionAcgObjectiveRecord objective;
            MissionAcgBindingRecord binding;
            if (client == null
                || !MissionAcgObjectiveRuntime.TryResolveRuntime(
                    attacker.Identity.Instance,
                    victim.Playfield.Identity.Instance,
                    ToRecord(victim.Identity),
                    out objective)
                || objective.Binding.MissionType != MissionRollType.KillPerson
                || !MissionAcgBindingRuntime.TryGetOwnedByAcceptedQuest(
                    attacker.Identity.Instance,
                    objective.Binding.AcceptedQuestIdentity.Instance,
                    out binding))
            {
                return false;
            }

            return Complete(
                client,
                attacker,
                binding,
                objective,
                MissionAcgObjectiveInteraction.TargetDeath,
                ToRecord(victim.Identity),
                objective.Binding.ObjectiveTemplateId,
                objective.Binding.ObjectiveName,
                null,
                0,
                null,
                "KillTarget");
        }

        internal static bool TryResumePersistedTargetDeath(
            IZoneClient client,
            ICharacter character,
            MissionAcgBindingRecord binding,
            MissionAcgOperationalState operational,
            out bool completed)
        {
            completed = false;
            if (client == null
                || character == null
                || binding == null
                || operational == null
                || binding.Binding.MissionType != MissionRollType.KillPerson
                || binding.Binding.OwnerIdentity.Instance != character.Identity.Instance
                || operational.OwnerIdentity.Instance != character.Identity.Instance
                || !operational.AcceptedQuestIdentity.Equals(
                    binding.Binding.AcceptedQuestIdentity)
                || operational.AllocatedLivePlayfield2
                   != binding.Binding.AllocatedLivePlayfield2)
            {
                return false;
            }

            MissionAcgObjectiveRecord objective;
            if (!MissionAcgObjectiveRuntime.TryGetByAccepted(
                    character.Identity.Instance,
                    binding.Binding.AcceptedQuestIdentity.Instance,
                    out objective)
                || objective.Binding.MissionType != MissionRollType.KillPerson)
            {
                return false;
            }

            MissionAcgNpcRuntimeState exactTarget = null;
            for (int i = 0; i < operational.Npcs.Count; i++)
            {
                MissionAcgNpcRuntimeState candidate = operational.Npcs[i];
                if (candidate.RuntimeIdentity.Equals(
                        objective.Binding.RuntimeObjectiveIdentity))
                {
                    exactTarget = candidate;
                    break;
                }
            }

            if (!MissionAcgCorpsePolicy.IsVerifiedKillDeathRecoveryEligible(
                    objective,
                    exactTarget))
            {
                return false;
            }

            MissionAcceptedStore.AcceptedMission acceptedMission;
            if (!MissionAcceptedStore.TryResolve(
                    character.Identity.Instance,
                    new Identity
                    {
                        Type =
                            (IdentityType)objective.Binding
                                .AcceptedQuestIdentity.Type,
                        Instance =
                            objective.Binding.AcceptedQuestIdentity.Instance
                    },
                    out acceptedMission))
            {
                return true;
            }

            completed = MissionAcgCompletionJournalService.TryCompleteVerified(
                client,
                character,
                acceptedMission,
                binding,
                objective,
                "KillTargetRestartRecovery");
            return true;
        }

        internal static bool TryHandleUseItemOnItem(
            IZoneClient client,
            GenericCmdMessage message)
        {
            if (client == null
                || client.Controller == null
                || message == null
                || message.Target == null
                || message.Target.Length < 2)
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            IItem item;
            if (character == null
                || !TryGetSourceItem(character, message.Target[0], out item)
                || item.Identity == null)
            {
                return false;
            }

            MissionAcgObjectiveRecord objective;
            if (character.Playfield != null
                && MissionAcgObjectiveRuntime.TryResolveRuntime(
                    character.Identity.Instance,
                    character.Playfield.Identity.Instance,
                    ToRecord(message.Target[1]),
                    out objective)
                && objective.Binding.MissionType == MissionRollType.RepairMachine)
            {
                if (!MissionKeyGrantService.IsRepairTool(item))
                {
                    return true;
                }

                MissionAcgBindingRecord binding;
                if (!MissionAcgBindingRuntime.TryGetOwnedByAcceptedQuest(
                    character.Identity.Instance,
                    objective.Binding.AcceptedQuestIdentity.Instance,
                    out binding))
                {
                    return true;
                }

                string spatialFailure;
                if (!MissionAcgSpatialRuntime.TryValidateObjectiveRuntimeInteraction(
                    character,
                    binding,
                    ToRecord(message.Target[1]),
                    NpcCombatAttackRules.MaxMeleeCombatDistance,
                    "repair-machine",
                    out spatialFailure))
                {
                    return true;
                }

                return Complete(
                    client,
                    character,
                    binding,
                    objective,
                    MissionAcgObjectiveInteraction.UseComponentOnMachine,
                    ToRecord(message.Target[1]),
                    objective.Binding.ObjectiveTemplateId,
                    objective.Binding.ObjectiveName,
                    ToRecord(item.Identity),
                    MissionAcgObjectiveContract.RepairComponentTemplateId,
                    null,
                    "RepairMachine");
            }

            if (MissionAcgObjectiveRuntime.TryResolveReturnItem(
                character.Identity.Instance,
                ToRecord(item.Identity),
                ToRecord(message.Target[1]),
                out objective))
            {
                if (item.LowID != objective.Binding.RequiredMissionItemTemplateId
                    && item.HighID != objective.Binding.RequiredMissionItemTemplateId)
                {
                    return true;
                }

                MissionAcgBindingRecord binding;
                if (!MissionAcgBindingRuntime.TryGetOwnedByAcceptedQuest(
                    character.Identity.Instance,
                    objective.Binding.AcceptedQuestIdentity.Instance,
                    out binding))
                {
                    return true;
                }

                return Complete(
                    client,
                    character,
                    binding,
                    objective,
                    MissionAcgObjectiveInteraction.ReturnItemToTerminal,
                    objective.Binding.RuntimeObjectiveIdentity,
                    objective.Binding.ObjectiveTemplateId,
                    objective.Binding.ObjectiveName,
                    ToRecord(item.Identity),
                    objective.Binding.RequiredMissionItemTemplateId,
                    ToRecord(message.Target[1]),
                    "ReturnItem");
            }

            return false;
        }

        private static bool Complete(
            IZoneClient client,
            ICharacter character,
            MissionAcgBindingRecord binding,
            MissionAcgObjectiveRecord objective,
            MissionAcgObjectiveInteraction interaction,
            MissionAcgIdentityRecord runtimeIdentity,
            int objectiveTemplate,
            string objectiveName,
            MissionAcgIdentityRecord missionItem,
            int missionItemTemplate,
            MissionAcgIdentityRecord terminal,
            string reason)
        {
            var observation =
                new MissionAcgObjectiveEvent
                {
                    OwnerInstance = character.Identity.Instance,
                    TeamIdentity = null,
                    AcceptedQuestInstance =
                        objective.Binding.AcceptedQuestIdentity.Instance,
                    AllocatedLivePlayfield2 =
                        objective.Binding.AllocatedLivePlayfield2,
                    RuntimeObjectiveIdentity = runtimeIdentity,
                    Interaction = interaction,
                    ObjectiveTemplateId = objectiveTemplate,
                    ObjectiveName = objectiveName,
                    MissionItemIdentity = missionItem,
                    MissionItemTemplateId = missionItemTemplate,
                    IssuingTerminalIdentity = terminal,
                    ObservationId =
                        objective.Binding.AcceptedQuestIdentity.Instance
                        + ":"
                        + interaction
                        + ":"
                        + runtimeIdentity.Instance
                };
            return MissionAcgCompletionJournalService.TryVerifyAndComplete(
                client,
                character,
                binding,
                objective,
                observation,
                reason);
        }

        private static bool TryGetSourceItem(
            ICharacter character,
            Identity source,
            out IItem item)
        {
            item = null;
            if (character == null
                || source == null)
            {
                return false;
            }

            return MissionKeyGrantService.TryGetExactInventoryItem(
                character,
                source,
                out item);
        }

        private static MissionAcgIdentityRecord ToRecord(Identity identity)
        {
            return identity == null
                       ? null
                       : new MissionAcgIdentityRecord(
                           (int)identity.Type,
                           identity.Instance);
        }
    }
}
