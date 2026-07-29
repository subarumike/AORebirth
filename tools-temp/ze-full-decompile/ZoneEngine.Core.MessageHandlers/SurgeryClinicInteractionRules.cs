using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.MessageHandlers;

public static class SurgeryClinicInteractionRules
{
	public const int CapturedSurgeryClinicTerminalInstance = -1073609566;

	public const int CapturedAlternateSurgeryClinicTerminalInstance = -1073740638;

	public const int CapturedSurgeryClinicTemplateId = 43553;

	public const int CapturedImprovedSurgeryClinicTemplateId = 295742;

	public const int SurgeryClinicCreditCost = 300;

	public const int SurgeryClinicNanoId = 157490;

	public const int SurgeryClinicNanoDuration = 90000;

	public const int SurgeryClinicImplantAccessSeconds = 300;

	public const int SurgeryClinicSpecialStatId = 124;

	public const int SurgeryClinicSpecialLockSeconds = 5;

	public const int SurgeryClinicSpecialAvailableDelayMilliseconds = 3500;

	public static bool IsCapturedSurgeryClinicTerminal(Identity target, int statelTemplateId)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		if ((int)((Identity)(ref target)).Type != 51005)
		{
			return false;
		}
		if (((Identity)(ref target)).Instance == -1073609566 || ((Identity)(ref target)).Instance == -1073740638)
		{
			return true;
		}
		return statelTemplateId == 43553 || statelTemplateId == 295742;
	}
}
