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

            if (!matched || offer == null)
            {
                MissionDiagnostics.Log(
                    "ACCEPT-REJECT quest={0} reason=offer-not-found",
                    acceptedQuestId,
                    matched);
                return;
            }

            try
            {
                MissionAcgBindingRecord binding;
                string failure;
                if (!MissionAcgAcceptanceCoordinator.TryAccept(
                    client,
                    character,
                    offer,
                    out binding,
                    out failure))
                {
                    MissionDiagnostics.Log(
                        "ACCEPT-ROLLBACK offer={0} reason={1}",
                        acceptedQuestId,
                        failure);
                    client.Server.Info(client, "CreateQuest accept failed: {0}", failure);
                    return;
                }

                MissionDiagnostics.Log(
                    "ACCEPT-COMPLETE offer={0} accepted={1}:{2} bundle={3} building={4}:{5} livePf2={6} key={7}",
                    acceptedQuestId,
                    binding.Binding.AcceptedQuestIdentity.Type,
                    binding.Binding.AcceptedQuestIdentity.Instance,
                    binding.Binding.SelectedBundleId,
                    binding.Binding.AcgBuildingIdentity.Type,
                    binding.Binding.AcgBuildingIdentity.Instance,
                    binding.Binding.AllocatedLivePlayfield2,
                    binding.Binding.MissionKeyIdentity.Instance);
            }
            catch (Exception ex)
            {
                client.Server.Info(client, "CreateQuest accept failed closed: {0}", ex);
            }
        }
    }
}
