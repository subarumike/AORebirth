namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    public enum InventoryContainerInteractionRouteMode
    {
        None,
        InventoryItem,
        WearOrSocialBackpack,
        BackpackContainer
    }

    public static class InventoryContainerInteractionRules
    {
        public static InventoryContainerInteractionRouteMode ResolveRouteMode(Identity target)
        {
            if (target.Type == IdentityType.Inventory)
            {
                return InventoryContainerInteractionRouteMode.InventoryItem;
            }

            if (target.Type == IdentityType.ArmorPage || target.Type == IdentityType.SocialPage)
            {
                return InventoryContainerInteractionRouteMode.WearOrSocialBackpack;
            }

            if (target.Type == IdentityType.Container)
            {
                return InventoryContainerInteractionRouteMode.BackpackContainer;
            }

            return InventoryContainerInteractionRouteMode.None;
        }
    }
}
