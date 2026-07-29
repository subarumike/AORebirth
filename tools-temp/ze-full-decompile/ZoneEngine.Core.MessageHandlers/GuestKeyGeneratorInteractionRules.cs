using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.MessageHandlers;

public static class GuestKeyGeneratorInteractionRules
{
	public const int CapturedPrivateCityGuestKeyTerminalInstance = 1464947595;

	public const int RuntimePrivateCityGuestKeyTerminalInstance = 1464566955;

	public const int CapturedCityAccessCardTemplateId = 280642;

	public const int CapturedCityAccessCardOverflowSlot = 111;

	public const int CityAccessCardLifetimeMilliseconds = 900000;

	public static bool IsPrivateCityGuestKeyTerminalTarget(Identity target)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		return (int)((Identity)(ref target)).Type == 51005 && (((Identity)(ref target)).Instance == 1464947595 || ((Identity)(ref target)).Instance == 1464566955);
	}
}
