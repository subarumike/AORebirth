namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;

    #endregion

    public sealed class InventoryContainerInteractionHandler
    {
        public static readonly InventoryContainerInteractionHandler Default =
            new InventoryContainerInteractionHandler();

        private InventoryContainerInteractionHandler()
        {
        }

        public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            return InventoryContainerRuntimeService.Default.TryHandleGenericCmdUse(client, message, target);
        }
    }
}
