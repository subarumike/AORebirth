namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using AORebirth.Core.Inventory;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

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
            switch (InventoryContainerInteractionRules.ResolveRouteMode(target))
            {
                case InventoryContainerInteractionRouteMode.InventoryItem:
                    client.Controller.UseItem(target);
                    GenericCmdMessageHandler.Default.Acknowledge(client.Controller.Character, message);
                    return true;

                case InventoryContainerInteractionRouteMode.WearOrSocialBackpack:
                    if (client.Controller.TryUseBackpackContainer(target))
                    {
                        GenericCmdMessageHandler.Default.Acknowledge(client.Controller.Character, message);
                    }

                    return true;

                case InventoryContainerInteractionRouteMode.BackpackContainer:
                    IInventoryPage backpackPage;
                    if (client.Controller.Character.BaseInventory.TryGetBackpackPage(target, out backpackPage))
                    {
                        BackpackContainerActionMessageHandler.Default.SendClose(client.Controller.Character, target);
                        client.Controller.Character.BaseInventory.MarkBackpackClosed(target);
                        GenericCmdMessageHandler.Default.Acknowledge(client.Controller.Character, message);
                    }

                    return true;
            }

            return false;
        }
    }
}
