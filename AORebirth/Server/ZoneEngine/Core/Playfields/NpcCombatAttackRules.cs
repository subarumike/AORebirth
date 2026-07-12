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

        public const int CapturedSubwayThiefAttackInfoAmmoCount = -1;

        public const int CapturedSubwayThiefAttackInfoUnknown = 0;

        public const int CapturedSubwayThiefSpecialAttackWeaponUnknown1 = 32;

        public const int CapturedSubwayThiefSpecialAttackWeaponUnknown2 = 32;

        public const int CapturedSubwayThiefSpecialAttackWeaponUnknown3 = 32;

        public const int CapturedSubwayThiefSpecialAttackWeaponUnknown4 = 32;

        public const int CapturedSubwayThiefSpecialAttackWeaponUnknown5 = 0;

        public const double CapturedSubwayThiefAttackStartDelaySeconds = 1.409765;

        public const double CapturedSubwayThiefMovementTransitionDelaySeconds = 0.219999;

        public const double CapturedSubwayThiefFirstHitDelaySeconds = 11.409643;

        public const double CapturedSubwayThiefRechargeSeconds = 6.0;

        public const int CapturedSubwayThiefDamage = 9;

        public const int CapturedSubwayThiefMonsterData = 26092;

        public const int NpcUnarmedRightAttackInfoWeaponSlot = 0;

        public const int NpcUnarmedLeftAttackInfoWeaponSlot = 1;

        public const int NpcUnarmedRightAttackInfoWeaponInstance = 1279874865;

        public const int NpcUnarmedLeftAttackInfoWeaponInstance = 1279874866;

        public const int NormalAttackInfoHitType = 3;

        public const int CapturedSubwayFilthFleaMonsterData = 17657;

        public const int CapturedSubwayPlayfield = 127;

        public const double CapturedSubwayFilthFleaInitialAttackSeconds = 3.65;

        public const double CapturedSubwayFilthFleaPoisonRechargeSeconds = 1.58;

        public const double CapturedSubwayFilthFleaMeleeRechargeSeconds = 2.8;

        public const int CapturedSubwayFilthFleaPoisonDamage = 15;

        public const int CapturedSubwayFilthFleaMeleeDamage = 3;

        public const int CapturedSubwayFilthFleaPoisonWeaponSlot = 1;

        public const int CapturedSubwayFilthFleaMeleeWeaponSlot = 0;

        public const int CapturedSubwayFilthFleaStickToHeadLowTemplate = 201059;

        public const int CapturedSubwayFilthFleaStickToHeadHighTemplate = 201060;

        public const int CapturedSubwayFilthFleaStickToHeadTag = 0x45504148;

        public const string CapturedSubwayFilthFleaStickToHeadName = "EPAH";

        public const int CapturedSubwayFilthFleaArmsLowTemplate = 201056;

        public const int CapturedSubwayFilthFleaArmsHighTemplate = 201057;

        public const int CapturedSubwayFilthFleaArmsTag = 0x415A5553;

        public const string CapturedSubwayFilthFleaArmsName = "AZUS";

        public const int CapturedSubwayFilthFleaSpecialAttackWeaponValue = 33;

        public const int CapturedSubwayFilthFleaSpecialAttackWeaponLastValue = 0;

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

        public static bool ShouldSendPlayerOwnedAttackPetAttackStartContext(
            bool isPlayerOwnedAttackPet,
            int? previousTargetInstance,
            int targetInstance)
        {
            return isPlayerOwnedAttackPet
                   && (!previousTargetInstance.HasValue || previousTargetInstance.Value != targetInstance);
        }
    }
}
