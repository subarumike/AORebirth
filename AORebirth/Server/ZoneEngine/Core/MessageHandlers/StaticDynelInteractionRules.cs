namespace ZoneEngine.Core.MessageHandlers
{
    public enum StaticDynelInteractionRouteMode
    {
        None,
        PoolOnUseOrTrade
    }

    public static class StaticDynelInteractionRules
    {
        public static StaticDynelInteractionRouteMode ResolveRouteMode(bool poolContainsTarget)
        {
            return poolContainsTarget
                       ? StaticDynelInteractionRouteMode.PoolOnUseOrTrade
                       : StaticDynelInteractionRouteMode.None;
        }
    }
}
