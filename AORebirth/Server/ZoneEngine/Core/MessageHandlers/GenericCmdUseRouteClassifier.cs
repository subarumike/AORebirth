namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    public enum GenericCmdUseRoute
    {
        RexB18DBoxProgress,
        InventoryItem,
        WearOrSocialBackpack,
        BackpackContainer,
        PrivateCityGuestKeyGenerator,
        PrivateCityController,
        DirectCorpse,
        DeadNpcCorpse,
        CapturedGridTerminal,
        GridEnterTerminal,
        SurgeryClinic,
        PoolOnUseOrTrade,
        StatelFallback
    }

    public sealed class GenericCmdUseRouteContext
    {
        public GenericCmdUseRouteContext(Identity target)
        {
            this.Target = target;
        }

        public Identity Target { get; private set; }

        public bool RexB18DBoxProgressMatched { get; set; }

        public bool IsPrivateCityPlayfield { get; set; }

        public bool DeadNpcCorpseRouted { get; set; }

        public bool CapturedGridTerminalRouteMatched { get; set; }

        public bool GridEnterTerminalMatched { get; set; }

        public bool SurgeryClinicTerminalMatched { get; set; }

        public bool PoolContainsTarget { get; set; }
    }

    public static class GenericCmdUseRouteClassifier
    {
        public const int CapturedPrivateCityGuestKeyTerminalInstance =
            GuestKeyGeneratorInteractionRules.CapturedPrivateCityGuestKeyTerminalInstance;

        public const int RuntimePrivateCityGuestKeyTerminalInstance =
            GuestKeyGeneratorInteractionRules.RuntimePrivateCityGuestKeyTerminalInstance;

        public const int CapturedCityControllerInstance = 0x009C182E;

        public const int RuntimeCityControllerInstance = 0x009C6010;

        public const int CapturedNonOrgCityControllerInstance = 0x009CA011;

        public const int CapturedBorealisGridTerminalInstance = unchecked((int)0xC0040320);

        public const int CapturedSurgeryClinicTerminalInstance = unchecked((int)0xC00204A2);

        public const int CapturedAlternateSurgeryClinicTerminalInstance = unchecked((int)0xC00004A2);

        public const int CapturedSurgeryClinicTemplateId = 43553;

        public const int CapturedImprovedSurgeryClinicTemplateId = 295742;

        public static readonly GenericCmdUseRoute[] CurrentRouteOrder =
        {
            GenericCmdUseRoute.RexB18DBoxProgress,
            GenericCmdUseRoute.InventoryItem,
            GenericCmdUseRoute.WearOrSocialBackpack,
            GenericCmdUseRoute.BackpackContainer,
            GenericCmdUseRoute.PrivateCityGuestKeyGenerator,
            GenericCmdUseRoute.PrivateCityController,
            GenericCmdUseRoute.DirectCorpse,
            GenericCmdUseRoute.DeadNpcCorpse,
            GenericCmdUseRoute.CapturedGridTerminal,
            GenericCmdUseRoute.GridEnterTerminal,
            GenericCmdUseRoute.SurgeryClinic,
            GenericCmdUseRoute.PoolOnUseOrTrade,
            GenericCmdUseRoute.StatelFallback
        };

        public static GenericCmdUseRoute Classify(GenericCmdUseRouteContext context)
        {
            Identity target = context.Target;

            if (context.RexB18DBoxProgressMatched)
            {
                return GenericCmdUseRoute.RexB18DBoxProgress;
            }

            if (target.Type == IdentityType.Inventory)
            {
                return GenericCmdUseRoute.InventoryItem;
            }

            if (target.Type == IdentityType.ArmorPage || target.Type == IdentityType.SocialPage)
            {
                return GenericCmdUseRoute.WearOrSocialBackpack;
            }

            if (target.Type == IdentityType.Container)
            {
                return GenericCmdUseRoute.BackpackContainer;
            }

            if (context.IsPrivateCityPlayfield && IsPrivateCityGuestKeyTerminalTarget(target))
            {
                return GenericCmdUseRoute.PrivateCityGuestKeyGenerator;
            }

            if (IsPrivateCityControllerTarget(target))
            {
                return GenericCmdUseRoute.PrivateCityController;
            }

            if (CorpseInteractionRules.IsDirectCorpseTarget(target))
            {
                return GenericCmdUseRoute.DirectCorpse;
            }

            if (CorpseInteractionRules.IsDeadNpcCorpseTarget(target, context.DeadNpcCorpseRouted))
            {
                return GenericCmdUseRoute.DeadNpcCorpse;
            }

            if (context.CapturedGridTerminalRouteMatched)
            {
                return GenericCmdUseRoute.CapturedGridTerminal;
            }

            if (context.GridEnterTerminalMatched)
            {
                return GenericCmdUseRoute.GridEnterTerminal;
            }

            if (context.SurgeryClinicTerminalMatched)
            {
                return GenericCmdUseRoute.SurgeryClinic;
            }

            if (context.PoolContainsTarget)
            {
                return GenericCmdUseRoute.PoolOnUseOrTrade;
            }

            return GenericCmdUseRoute.StatelFallback;
        }

        public static bool IsPrivateCityGuestKeyTerminalTarget(Identity target)
        {
            return GuestKeyGeneratorInteractionRules.IsPrivateCityGuestKeyTerminalTarget(target);
        }

        public static bool IsPrivateCityControllerTarget(Identity target)
        {
            return target.Type == IdentityType.CityController
                   && (target.Instance == CapturedCityControllerInstance
                       || target.Instance == RuntimeCityControllerInstance
                       || target.Instance == CapturedNonOrgCityControllerInstance);
        }
    }
}
