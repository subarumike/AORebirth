namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    public static class GuestKeyGeneratorInteractionRules
    {
        public const int CapturedPrivateCityGuestKeyTerminalInstance = unchecked((int)0x5751538B);

        public const int RuntimePrivateCityGuestKeyTerminalInstance = unchecked((int)0x574B84AB);

        public const int CapturedCityAccessCardTemplateId = 280642;

        public const int CapturedCityAccessCardOverflowSlot = 0x6F;

        public const int CityAccessCardLifetimeMilliseconds = 15 * 60 * 1000;

        public static bool IsPrivateCityGuestKeyTerminalTarget(Identity target)
        {
            return target.Type == IdentityType.Terminal
                   && (target.Instance == CapturedPrivateCityGuestKeyTerminalInstance
                       || target.Instance == RuntimePrivateCityGuestKeyTerminalInstance);
        }
    }
}
