using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

public static class UseItemOnItemInteractionRules
{
	public static UseItemOnItemInteractionRouteMode ResolveRouteMode(GenericCmdAction action)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Invalid comparison between Unknown and I4
		return ((int)action == 5) ? UseItemOnItemInteractionRouteMode.UseItemOnItem : UseItemOnItemInteractionRouteMode.None;
	}
}
