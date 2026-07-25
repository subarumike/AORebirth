namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Network;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Handles the client's mission-accept request. When the player clicks "Accept" on a rolled mission
    /// the client sends a CreateQuestMessage carrying the chosen offer's quest identity. The server looks
    /// the offer up from the last roll, grants the mission key into the inventory, and sends a
    /// QuestFullUpdate so the mission journal window fills in. Without this reply nothing happens on
    /// accept (no key, empty mission window).
    /// </summary>
    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class CreateQuestMessageHandler :
        BaseMessageHandler<CreateQuestMessage, CreateQuestMessageHandler>
    {
        protected override void Read(CreateQuestMessage message, IZoneClient client)
        {
            if (client == null || client.Controller == null || client.Controller.Character == null)
            {
                return;
            }

            ICharacter character = client.Controller.Character;
            Identity acceptedQuestId = message.QuestIdentity;

            QuestInfo offer;
            bool matched = MissionOfferStore.TryGetOffer(character.Identity.Instance, acceptedQuestId, out offer);

            client.Server.Info(
                client,
                "CreateQuest accept quest={0} matchedOffer={1}",
                acceptedQuestId,
                matched);

            try
            {
                bool repairGranted = false;
                int repairInstance = 0;
                InventoryError repairError = InventoryError.Invalid;
                bool isRepair = MissionRepairService.IsRepairOffer(offer)
                                || (matched && offer != null
                                    && MissionTypeCatalog.TypeFromIcon(offer.MissionIconId)
                                    == MissionRollType.RepairMachine);
                if (isRepair)
                {
                    // Grant kit before the key so a nearly-full bag still receives the repair item.
                    repairGranted = MissionKeyGrantService.TryGrantRepairItem(
                        client,
                        character,
                        1,
                        out repairInstance,
                        out repairError);
                    if (!repairGranted)
                    {
                        client.Server.Info(client, "CreateQuest repair kit grant failed: {0}", repairError);
                        MissionDiagnostics.Log(
                            "ACCEPT-REPAIR-FAIL quest={0} err={1}",
                            acceptedQuestId,
                            repairError);
                    }
                    else
                    {
                        MissionKeyStore.RegisterRepairKit(
                            character.Identity.Instance,
                            acceptedQuestId,
                            repairInstance);
                        MissionDiagnostics.Log(
                            "ACCEPT-REPAIR-OK quest={0} itemInstance={1}",
                            acceptedQuestId,
                            repairInstance);
                    }
                }

                int keyInstance;
                InventoryError inventoryError;
                bool granted = MissionKeyGrantService.TryGrantMissionKey(
                    client,
                    character,
                    "Mission key",
                    out keyInstance,
                    out inventoryError);

                if (granted)
                {
                    MissionKeyStore.Register(character.Identity.Instance, acceptedQuestId, keyInstance);
                }
                else
                {
                    client.Server.Info(client, "CreateQuest mission key grant failed: {0}", inventoryError);
                }

                bool windowSent = MissionAcceptService.SendAcceptedMission(character, offer);

                MissionDiagnostics.Log(
                    "ACCEPT quest={0} matchedOffer={1} keyGranted={2} keyInstance={3} keyError={4} isRepair={5} repairGranted={6} repairInstance={7} windowSent={8}",
                    acceptedQuestId,
                    matched,
                    granted,
                    keyInstance,
                    inventoryError,
                    isRepair,
                    repairGranted,
                    repairInstance,
                    windowSent);

                client.Server.Info(
                    client,
                    "CreateQuest accept complete quest={0} keyGranted={1} windowSent={2}",
                    acceptedQuestId,
                    granted,
                    windowSent);
            }
            catch (Exception ex)
            {
                client.Server.Info(client, "CreateQuest accept failed: {0}", ex);
            }
        }
    }
}
