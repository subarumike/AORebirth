namespace ZoneEngine.Core.Playfields
{
    public static class NpcCombatAttackRules
    {
        public const double MaxMeleeCombatDistance = 8.0;

        public const double DefaultCombatTickSeconds = 2.0;

        public const double OutOfRangeRetrySeconds = 1.0;

        public const double CapturedCleaningRobotCombatTickSeconds = 2.7;

        public const int CapturedCleaningRobotRightHandDamage = 10;

        public const int CapturedCleaningRobotLeftHandDamage = 8;

        public const int CapturedCleaningRobotLeftWeaponTemplate = 0x0001E960;

        public const int CapturedCleaningRobotRightWeaponTemplate = 0x0001E95D;

        public const int CapturedCleaningRobotLeftWeaponTag = 0x4C495732;

        public const int CapturedCleaningRobotRightWeaponTag = 0x4C495731;

        public const int CapturedCleaningRobotSpecialAttackWeaponValue = 8;

        public const int CapturedCleaningRobotSpecialAttackWeaponLastValue = 0;

        public const int UnarmedAttackInfoAmmoCount = -1;

        public const int NpcUnarmedRightAttackInfoWeaponSlot = 0;

        public const int NpcUnarmedLeftAttackInfoWeaponSlot = 1;

        public const int NpcUnarmedRightAttackInfoWeaponInstance = 1279874865;

        public const int NpcUnarmedLeftAttackInfoWeaponInstance = 1279874866;

        public const int NormalAttackInfoHitType = 3;

        public static bool ShouldSendCapturedCleaningRobotAttackStartContext(
            bool isCapturedCleaningRobot,
            bool usesEquippedWeapon,
            int? previousTargetInstance,
            int targetInstance)
        {
            return isCapturedCleaningRobot
                   && !usesEquippedWeapon
                   && (!previousTargetInstance.HasValue || previousTargetInstance.Value != targetInstance);
        }
    }
}
