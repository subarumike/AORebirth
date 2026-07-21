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
                    MissionKeyStore.Register(character.Identity.Instance, keyInstance);
                }
                else
                {
                    client.Server.Info(client, "CreateQuest mission key grant failed: {0}", inventoryError);
                }

                bool repairGranted = false;
                int repairInstance = 0;
                InventoryError repairError = InventoryError.Invalid;
                if (MissionRepairService.IsRepairOffer(offer))
                {
                    int repairQl = offer != null && offer.Quality > 0 ? offer.Quality : 1;
                    repairGranted = MissionKeyGrantService.TryGrantRepairItem(
                        client,
                        character,
                        repairQl,
                        out repairInstance,
                        out repairError);
                    if (!repairGranted)
                    {
                        client.Server.Info(client, "CreateQuest repair kit grant failed: {0}", repairError);
                    }
                }

                bool windowSent = MissionAcceptService.SendAcceptedMission(character, offer);

                MissionDiagnostics.Log(
                    "ACCEPT quest={0} matchedOffer={1} keyGranted={2} keyInstance={3} keyError={4} repairGranted={5} repairInstance={6} windowSent={7}",
                    acceptedQuestId,
                    matched,
                    granted,
                    keyInstance,
                    inventoryError,
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
