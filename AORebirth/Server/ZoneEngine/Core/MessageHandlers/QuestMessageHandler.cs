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
                // Echo the delete back so the client removes the entry from the mission window.
                client.SendCompressed(
                    new QuestMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        Action = QuestAction.Delete,
                        Unknown1 = 0,
                        Mission = message.Mission,
                        Unknown2 = 0,
                        Unknown3 = 0
                    });

                int keyInstance;
                bool keyRemoved = false;
                if (MissionKeyStore.TryTakeLatest(character.Identity.Instance, out keyInstance))
                {
                    keyRemoved = MissionKeyGrantService.TryRemoveMissionKey(client, character, keyInstance);
                }

                // Remove only the deleted mission — keep every other accepted mission.
                bool storeRemoved = MissionAcceptedStore.Remove(character.Identity.Instance, message.Mission);

                client.Server.Info(
                    client,
                    "Quest delete mission={0} keyRemoved={1} storeRemoved={2}",
                    message.Mission,
                    keyRemoved,
                    storeRemoved);
            }
            catch (Exception ex)
            {
                client.Server.Info(client, "Quest delete failed: {0}", ex);
            }
        }
    }
}
