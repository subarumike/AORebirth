namespace ZoneEngine.Core.MessageHandlers;

public static class StatelInteractionRules
{
	public static StatelInteractionRouteMode ResolveRouteMode(bool higherPriorityRoutesRejected)
	{
		return higherPriorityRoutesRejected ? StatelInteractionRouteMode.StatelFallback : StatelInteractionRouteMode.None;
	}
}
