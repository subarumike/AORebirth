namespace ZoneEngine.Core.MessageHandlers
{
    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class ClientContainerAddItemMessageHandler :
        BaseMessageHandler<ClientContainerAddItemMessage, ClientContainerAddItemMessageHandler>
    {
        protected override void Read(ClientContainerAddItemMessage message, IZoneClient client)
        {
            ICharacter character = client != null && client.Controller != null
                ? client.Controller.Character
                : null;

            if (character == null || character.BaseInventory == null)
            {
                return;
            }

            if (InventoryContainerRuntimeService.Default.TryMoveInventoryItemToBackpack(character, message))
            {
                return;
            }

            if (InventoryContainerRuntimeService.Default.TryDepositInventoryItemToBank(character, message))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "Unhandled ClientContainerAddItem char={0} source={1} target={2}",
                    character.Identity,
                    message.Source,
                    message.Target));
        }
    }
}
