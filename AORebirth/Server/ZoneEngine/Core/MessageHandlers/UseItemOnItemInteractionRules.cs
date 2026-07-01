namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    public enum UseItemOnItemInteractionRouteMode
    {
        None,
        UseItemOnItem
    }

    public static class UseItemOnItemInteractionRules
    {
        public static UseItemOnItemInteractionRouteMode ResolveRouteMode(GenericCmdAction action)
        {
            return action == GenericCmdAction.UseItemOnItem
                       ? UseItemOnItemInteractionRouteMode.UseItemOnItem
                       : UseItemOnItemInteractionRouteMode.None;
        }
    }
}
