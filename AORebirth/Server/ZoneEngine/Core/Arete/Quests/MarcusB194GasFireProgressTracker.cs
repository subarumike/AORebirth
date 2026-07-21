namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Playfields;

    #endregion

    /// <summary>
    /// Capture 20260719-Rex-Markus-stone: UseItemOnItem Compact Fire Suppressant (296780)
    /// on Gas Fire Terminal (template 295883) completes B194 and projects Return to Marcus (B196).
    /// </summary>
    public static class MarcusB194GasFireProgressTracker
    {
        private const int AreteLandingPlayfieldId = 6553;

        private const int GasFireTemplateId = 295883;

        private const int CompactFireSuppressantItemId = 296780;

        private const string MissionId = "Mission:5514B194";

        private const string ObjectiveId = "mission_5514b194_objective_questfullupdate";

        private const string ExtinguishFeedback = "~&!!!\":!!!)<s\u001dYou extinguish the Gas Fire.";

        public static bool TryHandleUseItemOnItem(IZoneClient client, GenericCmdMessage message)
        {
            if (client == null || message == null || message.Target == null || message.Target.Length < 2)
            {
                return false;
            }

            if (UseItemOnItemInteractionRules.ResolveRouteMode(message.Action)
                != UseItemOnItemInteractionRouteMode.UseItemOnItem)
            {
                return false;
            }

            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null || character.Playfield == null
                || character.Playfield.Identity.Instance != AreteLandingPlayfieldId
                || !(character.Controller is PlayerController))
            {
                return false;
            }

            Identity itemIdentity = message.Target[0];
            Identity fireIdentity = message.Target[1];
            if (fireIdentity.Type != IdentityType.Terminal)
            {
                return false;
            }

            StaticDynel fire = Pool.Instance.GetObject<StaticDynel>(character.Playfield.Identity, fireIdentity);
            if (fire == null || !IsGasFire(fire))
            {
                return false;
            }

            IItem item = ResolveInventoryItem(character, itemIdentity);
            if (item == null || !IsSuppressant(item))
            {
                return false;
            }

            if (!MissionRuntime.IsInitialized)
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(character.Identity.Instance, MissionId);
            ZoneEngine.Core.Missions.MissionStateRecord b196 =
                MissionRuntime.Service.GetMission(character.Identity.Instance, MissionRuntime.RexB196QuestId);
            bool b194Tracked = mission != null
                               && (mission.State == MissionLifecycleState.Active
                                   || mission.State == MissionLifecycleState.Completed
                                   || mission.State == MissionLifecycleState.Offered);
            bool b196Tracked = b196 != null
                               && (b196.State == MissionLifecycleState.Active
                                   || b196.State == MissionLifecycleState.Completed
                                   || b196.State == MissionLifecycleState.Offered);

            // Capture path: suppressant on gas fire always extinguishes. Persistence may lag behind
            // the client B194 window; still project Action59+Delete+B196.
            if (!b194Tracked && !b196Tracked
                && InventoryContainerRuntimeService.Default.CountCharacterItemInCarriedInventory(
                       character,
                       CompactFireSuppressantItemId) <= 0)
            {
                return false;
            }

            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            // Client mission swap must always run even if despawn/persistence throws.
            // Capture: Action59 + B194 Delete + B196 QFU (Return to Marcus).
            try
            {
                int characterId = character.Identity.Instance;

                // Always force B194 completed + B196 active — Offered/missing states previously
                // left B194 Active so Marcus opened Extinguish/post-complete while client had B196.
                if (mission != null && mission.State == MissionLifecycleState.Offered)
                {
                    MissionRuntime.Service.AcceptMission(characterId, MissionId);
                    mission = MissionRuntime.Service.GetMission(characterId, MissionId);
                }

                if (mission != null && mission.State == MissionLifecycleState.Active)
                {
                    MissionRuntime.Service.ObserveObjective(
                        new MissionObjectiveObservation
                        {
                            CharacterId = characterId,
                            QuestId = MissionId,
                            ObjectiveId = ObjectiveId,
                            ObservationKey = "gas-fire:" + fireIdentity.ToString(true),
                            Amount = 1,
                            EventType = "GenericCmd:UseItemOnItem",
                            SourceIdentity = character.Identity.ToString(true),
                            TargetIdentity = fireIdentity.ToString(true)
                        });
                }

                MissionOperationResult completion = MissionRuntime.Service.CompleteAndActivateNextMission(
                    characterId,
                    MissionId,
                    MissionRuntime.RexB196QuestId);
                if (completion.Status != MissionOperationStatus.Applied
                    && completion.Status != MissionOperationStatus.AlreadyApplied)
                {
                    MissionRuntime.Service.CompleteMission(characterId, MissionId);
                    MissionRuntime.Service.OfferMission(characterId, MissionRuntime.RexB196QuestId);
                    MissionRuntime.Service.AcceptMission(characterId, MissionRuntime.RexB196QuestId);
                }

                try
                {
                    character.Controller.Client.SendCompressed(
                        new FormatFeedbackMessage
                        {
                            Identity = character.Identity,
                            Unknown = 1,
                            Unknown1 = 0,
                            FormattedMessage = ExtinguishFeedback,
                            Unknown2 = 0
                        });
                }
                catch (Exception e)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "ARETE_MARCUS_B194 extinguish feedback failed: " + e.Message);
                }

                RexQuestPreviewEmissionResult handoff =
                    SafeQuestFullUpdateSender.TrySendB194ToB196Handoff(character);
                if (handoff == null || !handoff.Emitted)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "ARETE_MARCUS_B194 B194→B196 handoff failed: "
                        + (handoff == null ? "null" : handoff.Message));
                    SafeQuestFullUpdateSender.TrySendB194QuestDelete(character);
                    SafeQuestFullUpdateSender.TrySendB196Preview(character);
                }
            }
            finally
            {
                try
                {
                    character.Playfield.Despawn(fireIdentity);
                }
                catch (Exception e)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "ARETE_MARCUS_B194 gas fire despawn failed: " + e.Message);
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "ARETE_MARCUS_B194 gas fire extinguished character="
                + character.Identity.ToString(true)
                + " fire="
                + fireIdentity.ToString(true)
                + " item="
                + CompactFireSuppressantItemId);

            return true;
        }

        private static bool IsGasFire(StaticDynel fire)
        {
            if (fire == null)
            {
                return false;
            }

            if (fire.Template != null && fire.Template.ID == GasFireTemplateId)
            {
                return true;
            }

            int template;
            if (fire.Stats != null
                && (fire.Stats.TryGetValue((int)StatIds.acgitemtemplateid, out template)
                    || fire.Stats.TryGetValue((int)StatIds.staticinstance, out template)))
            {
                return template == GasFireTemplateId;
            }

            return false;
        }

        private static bool IsSuppressant(IItem item)
        {
            return item != null
                   && (item.HighID == CompactFireSuppressantItemId
                       || item.LowID == CompactFireSuppressantItemId);
        }

        private static IItem ResolveInventoryItem(ICharacter character, Identity itemIdentity)
        {
            IInventoryPage sourcePage;
            if (character.BaseInventory != null
                && character.BaseInventory.Pages.TryGetValue((int)itemIdentity.Type, out sourcePage)
                && sourcePage != null)
            {
                return sourcePage[itemIdentity.Instance];
            }

            sourcePage = Pool.Instance.GetObject<IInventoryPage>(
                new Identity
                {
                    Type = (IdentityType)character.Identity.Instance,
                    Instance = (int)itemIdentity.Type
                });
            return sourcePage == null ? null : sourcePage[itemIdentity.Instance];
        }
    }
}
