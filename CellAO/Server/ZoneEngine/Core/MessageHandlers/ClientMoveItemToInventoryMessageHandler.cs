namespace ZoneEngine.Core.MessageHandlers
{
    using CellAO.Core.Components;
    using CellAO.Core.Entities;
    using CellAO.Core.Network;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Packets;

    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class ClientMoveItemToInventoryMessageHandler :
        BaseMessageHandler<ClientMoveItemToInventoryMessage, ClientMoveItemToInventoryMessageHandler>
    {
        protected override void Read(ClientMoveItemToInventoryMessage message, IZoneClient client)
        {
            ICharacter character = client.Controller.Character;
            if (character.Playfield.TryLootCorpseItem(
                character,
                message.SourceContainer,
                character.Identity,
                message.TargetPlacement))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "Unhandled ClientMoveItemToInventory source={0} targetPlacement={1} character={2}",
                    message.SourceContainer,
                    message.TargetPlacement,
                    character.Identity));
        }
    }
}
