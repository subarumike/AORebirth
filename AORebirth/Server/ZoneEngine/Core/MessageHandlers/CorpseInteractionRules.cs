namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    public enum CorpseInteractionRouteMode
    {
        None,
        DirectCorpse,
        DeadNpcCorpse
    }

    public static class CorpseInteractionRules
    {
        public const int CorpseUseAcknowledgeDelayMilliseconds = 550;

        public static CorpseInteractionRouteMode ResolveRouteMode(Identity target, bool deadNpcCorpseRouted)
        {
            if (target.Type == IdentityType.Corpse)
            {
                return CorpseInteractionRouteMode.DirectCorpse;
            }

            if (target.Type == IdentityType.CanbeAffected && deadNpcCorpseRouted)
            {
                return CorpseInteractionRouteMode.DeadNpcCorpse;
            }

            return CorpseInteractionRouteMode.None;
        }

        public static bool IsDirectCorpseTarget(Identity target)
        {
            return ResolveRouteMode(target, false) == CorpseInteractionRouteMode.DirectCorpse;
        }

        public static bool IsDeadNpcCorpseTarget(Identity target, bool deadNpcCorpseRouted)
        {
            return ResolveRouteMode(target, deadNpcCorpseRouted) == CorpseInteractionRouteMode.DeadNpcCorpse;
        }
    }
}
