using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.MessageHandlers;

public static class GenericCmdUseRouteClassifier
{
	public const int CapturedPrivateCityGuestKeyTerminalInstance = 1464947595;

	public const int RuntimePrivateCityGuestKeyTerminalInstance = 1464566955;

	public const int CapturedCityControllerInstance = 10229806;

	public const int RuntimeCityControllerInstance = 10248208;

	public const int CapturedNonOrgCityControllerInstance = 10264593;

	public const int CapturedBorealisGridTerminalInstance = -1073478880;

	public const int CapturedSurgeryClinicTerminalInstance = -1073609566;

	public const int CapturedAlternateSurgeryClinicTerminalInstance = -1073740638;

	public const int CapturedSurgeryClinicTemplateId = 43553;

	public const int CapturedImprovedSurgeryClinicTemplateId = 295742;

	public static readonly GenericCmdUseRoute[] CurrentRouteOrder = new GenericCmdUseRoute[13]
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
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		Identity target = context.Target;
		if (RexB18DInteractionRules.ResolveRouteMode(context.RexB18DBoxProgressMatched) == RexB18DInteractionRouteMode.RexB18DBoxProgress)
		{
			return GenericCmdUseRoute.RexB18DBoxProgress;
		}
		switch (InventoryContainerInteractionRules.ResolveRouteMode(target))
		{
		case InventoryContainerInteractionRouteMode.InventoryItem:
			return GenericCmdUseRoute.InventoryItem;
		case InventoryContainerInteractionRouteMode.WearOrSocialBackpack:
			return GenericCmdUseRoute.WearOrSocialBackpack;
		case InventoryContainerInteractionRouteMode.BackpackContainer:
			return GenericCmdUseRoute.BackpackContainer;
		default:
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
			switch (GridTerminalInteractionRules.ResolveRouteMode(context.CapturedGridTerminalRouteMatched, context.GridEnterTerminalMatched))
			{
			case GridTerminalInteractionRouteMode.CapturedGridTerminal:
				return GenericCmdUseRoute.CapturedGridTerminal;
			case GridTerminalInteractionRouteMode.GridEnterTerminal:
				return GenericCmdUseRoute.GridEnterTerminal;
			default:
				if (context.SurgeryClinicTerminalMatched)
				{
					return GenericCmdUseRoute.SurgeryClinic;
				}
				if (StaticDynelInteractionRules.ResolveRouteMode(context.PoolContainsTarget) == StaticDynelInteractionRouteMode.PoolOnUseOrTrade)
				{
					return GenericCmdUseRoute.PoolOnUseOrTrade;
				}
				if (StatelInteractionRules.ResolveRouteMode(higherPriorityRoutesRejected: true) == StatelInteractionRouteMode.StatelFallback)
				{
					return GenericCmdUseRoute.StatelFallback;
				}
				return GenericCmdUseRoute.StatelFallback;
			}
		}
	}

	public static bool IsPrivateCityGuestKeyTerminalTarget(Identity target)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return GuestKeyGeneratorInteractionRules.IsPrivateCityGuestKeyTerminalTarget(target);
	}

	public static bool IsPrivateCityControllerTarget(Identity target)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		return (int)((Identity)(ref target)).Type == 50200 && (((Identity)(ref target)).Instance == 10229806 || ((Identity)(ref target)).Instance == 10248208 || ((Identity)(ref target)).Instance == 10264593);
	}
}
