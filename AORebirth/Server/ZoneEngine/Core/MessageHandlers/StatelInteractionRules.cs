namespace ZoneEngine.Core.MessageHandlers
{
    public enum StatelInteractionRouteMode
    {
        None,
        StatelFallback
    }

    public static class StatelInteractionRules
    {
        public static StatelInteractionRouteMode ResolveRouteMode(bool higherPriorityRoutesRejected)
        {
            return higherPriorityRoutesRejected
                       ? StatelInteractionRouteMode.StatelFallback
                       : StatelInteractionRouteMode.None;
        }
    }
}
