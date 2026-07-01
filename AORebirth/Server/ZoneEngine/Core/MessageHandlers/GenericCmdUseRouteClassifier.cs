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

        public const int CapturedBorealisGridTerminalInstance =
            GridTerminalInteractionRules.CapturedBorealisGridTerminalInstance;

        public const int CapturedSurgeryClinicTerminalInstance =
            SurgeryClinicInteractionRules.CapturedSurgeryClinicTerminalInstance;

        public const int CapturedAlternateSurgeryClinicTerminalInstance =
            SurgeryClinicInteractionRules.CapturedAlternateSurgeryClinicTerminalInstance;

        public const int CapturedSurgeryClinicTemplateId =
            SurgeryClinicInteractionRules.CapturedSurgeryClinicTemplateId;

        public const int CapturedImprovedSurgeryClinicTemplateId =
            SurgeryClinicInteractionRules.CapturedImprovedSurgeryClinicTemplateId;

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

            if (RexB18DInteractionRules.ResolveRouteMode(context.RexB18DBoxProgressMatched)
                == RexB18DInteractionRouteMode.RexB18DBoxProgress)
            {
                return GenericCmdUseRoute.RexB18DBoxProgress;
            }

            InventoryContainerInteractionRouteMode inventoryRouteMode =
                InventoryContainerInteractionRules.ResolveRouteMode(target);
            if (inventoryRouteMode == InventoryContainerInteractionRouteMode.InventoryItem)
            {
                return GenericCmdUseRoute.InventoryItem;
            }

            if (inventoryRouteMode == InventoryContainerInteractionRouteMode.WearOrSocialBackpack)
            {
                return GenericCmdUseRoute.WearOrSocialBackpack;
            }

            if (inventoryRouteMode == InventoryContainerInteractionRouteMode.BackpackContainer)
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

            GridTerminalInteractionRouteMode gridTerminalRouteMode =
                GridTerminalInteractionRules.ResolveRouteMode(
                    context.CapturedGridTerminalRouteMatched,
                    context.GridEnterTerminalMatched);
            if (gridTerminalRouteMode == GridTerminalInteractionRouteMode.CapturedGridTerminal)
            {
                return GenericCmdUseRoute.CapturedGridTerminal;
            }

            if (gridTerminalRouteMode == GridTerminalInteractionRouteMode.GridEnterTerminal)
            {
                return GenericCmdUseRoute.GridEnterTerminal;
            }

            if (context.SurgeryClinicTerminalMatched)
            {
                return GenericCmdUseRoute.SurgeryClinic;
            }

            if (StaticDynelInteractionRules.ResolveRouteMode(context.PoolContainsTarget)
                == StaticDynelInteractionRouteMode.PoolOnUseOrTrade)
            {
                return GenericCmdUseRoute.PoolOnUseOrTrade;
            }

            if (StatelInteractionRules.ResolveRouteMode(true) == StatelInteractionRouteMode.StatelFallback)
            {
                return GenericCmdUseRoute.StatelFallback;
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
