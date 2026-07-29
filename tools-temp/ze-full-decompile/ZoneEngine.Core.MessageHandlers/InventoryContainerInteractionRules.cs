using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.MessageHandlers;

public static class InventoryContainerInteractionRules
{
	public static InventoryContainerInteractionRouteMode ResolveRouteMode(Identity target)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Invalid comparison between Unknown and I4
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Invalid comparison between Unknown and I4
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Invalid comparison between Unknown and I4
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Invalid comparison between Unknown and I4
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Invalid comparison between Unknown and I4
		if ((int)((Identity)(ref target)).Type == 104 || (int)((Identity)(ref target)).Type == 101)
		{
			return InventoryContainerInteractionRouteMode.InventoryItem;
		}
		if ((int)((Identity)(ref target)).Type == 102 || (int)((Identity)(ref target)).Type == 115)
		{
			return InventoryContainerInteractionRouteMode.WearOrSocialBackpack;
		}
		if ((int)((Identity)(ref target)).Type == 51017)
		{
			return InventoryContainerInteractionRouteMode.BackpackContainer;
		}
		return InventoryContainerInteractionRouteMode.None;
	}
}
