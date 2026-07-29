using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.MessageHandlers;

public static class CorpseInteractionRules
{
	public const int CorpseUseAcknowledgeDelayMilliseconds = 550;

	public static CorpseInteractionRouteMode ResolveRouteMode(Identity target, bool deadNpcCorpseRouted)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Invalid comparison between Unknown and I4
		if ((int)((Identity)(ref target)).Type == 51050)
		{
			return CorpseInteractionRouteMode.DirectCorpse;
		}
		if ((int)((Identity)(ref target)).Type == 50000 && deadNpcCorpseRouted)
		{
			return CorpseInteractionRouteMode.DeadNpcCorpse;
		}
		return CorpseInteractionRouteMode.None;
	}

	public static bool IsDirectCorpseTarget(Identity target)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return ResolveRouteMode(target, deadNpcCorpseRouted: false) == CorpseInteractionRouteMode.DirectCorpse;
	}

	public static bool IsDeadNpcCorpseTarget(Identity target, bool deadNpcCorpseRouted)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return ResolveRouteMode(target, deadNpcCorpseRouted) == CorpseInteractionRouteMode.DeadNpcCorpse;
	}
}
