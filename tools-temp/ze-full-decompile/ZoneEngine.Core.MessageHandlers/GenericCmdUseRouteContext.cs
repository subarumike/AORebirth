using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.MessageHandlers;

public sealed class GenericCmdUseRouteContext
{
	public Identity Target { get; private set; }

	public bool RexB18DBoxProgressMatched { get; set; }

	public bool IsPrivateCityPlayfield { get; set; }

	public bool DeadNpcCorpseRouted { get; set; }

	public bool CapturedGridTerminalRouteMatched { get; set; }

	public bool GridEnterTerminalMatched { get; set; }

	public bool SurgeryClinicTerminalMatched { get; set; }

	public bool PoolContainsTarget { get; set; }

	public GenericCmdUseRouteContext(Identity target)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		Target = target;
	}
}
