namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;

    #endregion

    public sealed class UseItemOnItemInteractionHandler
    {
        public static readonly UseItemOnItemInteractionHandler Default =
            new UseItemOnItemInteractionHandler();

        private UseItemOnItemInteractionHandler()
        {
        }

        public bool TryHandle(IZoneClient client, GenericCmdMessage message)
        {
            return InventoryContainerRuntimeService.Default.TryHandleUseItemOnItem(client, message);
        }
    }
}
