namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Handles the client's mission-accept request. When the player clicks "Accept" on a rolled mission
    /// the client sends a CreateQuestMessage carrying the chosen offer's quest identity. Acceptance first
    /// resolves an existing durable owner+offer claim, then consults the current roll only for a genuinely
    /// new claim. This makes duplicate callbacks and restart recovery converge on one accepted mission.
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
            var originalOfferId = message.QuestIdentity;

            client.Server.Info(
                client,
                "CreateQuest accept offer={0}",
                originalOfferId);

            try
            {
                MissionAcgBindingRecord binding;
                string failure;
                if (!MissionAcgAcceptanceCoordinator.TryAccept(
                    client,
                    character,
                    originalOfferId,
                    out binding,
                    out failure))
                {
                    MissionDiagnostics.Log(
                        "ACCEPT-ROLLBACK offer={0} reason={1}",
                        originalOfferId,
                        failure);
                    client.Server.Info(client, "CreateQuest accept failed: {0}", failure);
                    return;
                }

                MissionDiagnostics.Log(
                    "ACCEPT-COMPLETE offer={0} accepted={1}:{2} bundle={3} building={4}:{5} livePf2={6} key={7}",
                    originalOfferId,
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
