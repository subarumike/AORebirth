namespace ZoneEngine.Core.Playfields
{
    using AORebirth.Core.Playfields;

    using ZoneEngine.Core;

    internal static class AreteRegularMobCombatProfileSelector
    {
        internal static CapturedEnemyCombatContract Create(
            string evidence,
            string profileSelector,
            int capturedSourceIdentity,
            int capturedAttackRangeMicrometers,
            int capturedSpecialAttackWeaponUnknown5,
            NpcAiProfile aiProfile)
        {
            double? capturedRange = capturedAttackRangeMicrometers > 0
                                        ? capturedAttackRangeMicrometers / 1000000.0d
                                        : (double?)null;
            int? capturedUnknown5 = string.IsNullOrWhiteSpace(profileSelector)
                                        ? (int?)null
                                        : capturedSpecialAttackWeaponUnknown5;
            return CapturedEnemyCombatContract.CapturedProfileSelector(
                evidence,
                capturedSourceIdentity,
                profileSelector,
                aiProfile,
                capturedRange,
                capturedUnknown5,
                string.IsNullOrWhiteSpace(profileSelector) ? (double?)null : 0.0d);
        }
    }
}
