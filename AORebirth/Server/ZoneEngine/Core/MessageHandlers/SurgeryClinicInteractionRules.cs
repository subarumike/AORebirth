namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    public static class SurgeryClinicInteractionRules
    {
        public const int CapturedSurgeryClinicTerminalInstance = unchecked((int)0xC00204A2);

        public const int CapturedAlternateSurgeryClinicTerminalInstance = unchecked((int)0xC00004A2);

        public const int CapturedSurgeryClinicTemplateId = 43553;

        public const int CapturedImprovedSurgeryClinicTemplateId = 295742;

        public const int SurgeryClinicCreditCost = 300;

        public const int SurgeryClinicNanoId = 0x26732;

        public const int SurgeryClinicNanoDuration = 90000;

        public const int SurgeryClinicImplantAccessSeconds = 300;

        public const int SurgeryClinicSpecialStatId = 124;

        public const int SurgeryClinicSpecialLockSeconds = 5;

        public const int SurgeryClinicSpecialAvailableDelayMilliseconds = 3500;

        public static bool IsCapturedSurgeryClinicTerminal(Identity target, int statelTemplateId)
        {
            if (target.Type != IdentityType.Terminal)
            {
                return false;
            }

            if (target.Instance == CapturedSurgeryClinicTerminalInstance
                || target.Instance == CapturedAlternateSurgeryClinicTerminalInstance)
            {
                return true;
            }

            return statelTemplateId == CapturedSurgeryClinicTemplateId
                   || statelTemplateId == CapturedImprovedSurgeryClinicTemplateId;
        }
    }
}
