namespace ZoneEngine.Core.MessageHandlers
{
    public enum RexB18DInteractionRouteMode
    {
        None,
        RexB18DBoxProgress
    }

    public static class RexB18DInteractionRules
    {
        public static RexB18DInteractionRouteMode ResolveRouteMode(bool boxProgressObserved)
        {
            return boxProgressObserved
                       ? RexB18DInteractionRouteMode.RexB18DBoxProgress
                       : RexB18DInteractionRouteMode.None;
        }
    }
}
