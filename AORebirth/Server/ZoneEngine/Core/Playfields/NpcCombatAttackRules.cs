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

        public const int CapturedSubwayThiefWeaponDamageMinimumOverride = 0;

        public const int CapturedSubwayThiefWeaponDamageMaximumOverride = 0;

        public const int CapturedSubwayThiefMonsterData = 26092;

        public const int CapturedSubwayDisobedientBotMonsterData = 17649;

        // Nine projected official-live hits plus eight authoritative raw-only hits
        // prove the normal local-player SIW1 damage envelope. No critical hit
        // has been observed and critical behavior remains unresolved.
        public const int CapturedSubwayDisobedientBotMinimumDamage = 8;

        public const int CapturedSubwayDisobedientBotMaximumDamage = 15;

        public const double CapturedSubwayDisobedientBotInitialAttackSeconds = 3.270444;

        // Focused raw traffic includes missed attempts and therefore preserves
        // the attack-attempt cadence instead of deriving recharge from landed hits.
        public const double CapturedSubwayDisobedientBotRechargeSeconds = 5.973723;

        public const int CapturedSubwayDisobedientBotWeaponSlot = 0;

        public const int CapturedSubwayDisobedientBotLowTemplate = 0x00023566;

        public const int CapturedSubwayDisobedientBotHighTemplate = 0x00023567;

        public const int CapturedSubwayDisobedientBotWeaponTag = 0x53495731;

        public const string CapturedSubwayDisobedientBotWeaponName = "SIW1";

        public const int CapturedSubwayDisobedientBotLevel5SpecialAttackWeaponValue = 30;

        public const int CapturedSubwayDisobedientBotLevel6SpecialAttackWeaponValue = 35;

        // Private midpoint policy between captured level-6 value 35 and level-8 value 45.
        public const int CapturedSubwayDisobedientBotLevel7SpecialAttackWeaponPolicyValue = 40;

        public const int CapturedSubwayDisobedientBotLevel8SpecialAttackWeaponValue = 45;

        public const int CapturedSubwayDisobedientBotLevel9SpecialAttackWeaponValue = 49;

        public const int CapturedSubwayDisobedientBotLevel10SpecialAttackWeaponValue = 54;

        public const int CapturedSubwayDisobedientBotLevel5SpecialAttackWeaponLastValue = 22;

        public const int CapturedSubwayDisobedientBotSpecialAttackWeaponLastValue = 0;

        public const int CapturedSubwayBloodcreeperMonsterData = 30379;

        public const int CapturedSubwayBloodcreeperBiteMinimumDamage = 21;

        public const int CapturedSubwayBloodcreeperBiteMaximumDamage = 35;

        public const double CapturedSubwayBloodcreeperBiteInitialSeconds = 6.088742;

        public const double CapturedSubwayBloodcreeperBiteRechargeSeconds = 7.509840;

        public const int CapturedSubwayBloodcreeperBiteWeaponSlot = 0;

        public const int CapturedSubwayBloodcreeperBiteLowTemplate = 121091;

        public const int CapturedSubwayBloodcreeperBiteHighTemplate = 121092;

        public const int CapturedSubwayBloodcreeperBiteTag = 0x534B5731;

        public const string CapturedSubwayBloodcreeperBiteName = "SKW1";

        public const int CapturedSubwayBloodcreeperSpitMinimumDamage = 21;

        public const int CapturedSubwayBloodcreeperSpitMaximumDamage = 41;

        public const double CapturedSubwayBloodcreeperSpitInitialSeconds = 3.057708;

        public const double CapturedSubwayBloodcreeperSpitRechargeSeconds = 7.389908;

        public const int CapturedSubwayBloodcreeperSpitWeaponSlot = 1;

        public const int CapturedSubwayBloodcreeperSpitLowTemplate = 121094;

        public const int CapturedSubwayBloodcreeperSpitHighTemplate = 121095;

        public const int CapturedSubwayBloodcreeperSpitTag = 0x534B5732;

        public const string CapturedSubwayBloodcreeperSpitName = "SKW2";

        public const int CapturedSubwayBloodcreeperSpecialAttackWeaponValue = 131;

        public const int CapturedSubwayBloodcreeperSpecialAttackWeaponLastValue = 37;

        public const int CapturedSubwayVergilMonsterData = 203748;

        public const int CapturedSubwayVergilWeaponTemplate = 122123;

        public const int CapturedSubwayVergilWeaponQuality = 23;

        public const int CapturedSubwayVergilWeaponDamageMinimumOverride = 0;

        public const int CapturedSubwayVergilWeaponDamageMaximumOverride = 0;

        public const double CapturedSubwayVergilAttackStartDelaySeconds = 0.646433;

        public const double CapturedSubwayVergilMovementTransitionDelaySeconds = 0.001000;

        public const double CapturedSubwayVergilFirstHitDelaySeconds = 2.787410;

        public const double CapturedSubwayVergilRechargeOverrideSeconds = 0.0;

        public const int CapturedSubwayVergilInitialAttackInfoAmmoCount = 19;

        public const int CapturedSubwayVergilAttackInfoUnknown = 0;

        public const int CapturedSubwayVergilSpecialAttackWeaponValue = 167;

        public const int CapturedSubwayVergilSpecialAttackWeaponLastValue = 0;

        public const int CapturedSubwayEumenidesMonsterData = 203726;

        public const int CapturedSubwayEumenidesWeaponLowTemplate = 123267;

        public const int CapturedSubwayEumenidesWeaponHighTemplate = 123268;

        public const int CapturedSubwayEumenidesWeaponQuality = 20;

        // Twenty-one observed local-player hits span 25..45 with a 4.311321s
        // median interval. They remain evidence, not hard-coded weapon rolls;
        // damage and recharge stay owned by the equipped item.
        public const int CapturedSubwayEumenidesWeaponDamageMinimumOverride = 0;

        public const int CapturedSubwayEumenidesWeaponDamageMaximumOverride = 0;

        public const double CapturedSubwayEumenidesRechargeOverrideSeconds = 0.0;

        public const double CapturedSubwayEumenidesAttackStartDelaySeconds = 0.001000;

        public const double CapturedSubwayEumenidesMovementTransitionDelaySeconds = 0.233124;

        public const double CapturedSubwayEumenidesFirstHitDelaySeconds = 5.199992;

        public const int CapturedSubwayEumenidesInitialAttackInfoAmmoCount = 19;

        public const int CapturedSubwayEumenidesAttackInfoUnknown = 0;

        public const int CapturedSubwayEumenidesSpecialAttackWeaponUnknown1 = 143;

        public const int CapturedSubwayEumenidesSpecialAttackWeaponUnknown2 = 171;

        public const int CapturedSubwayEumenidesSpecialAttackWeaponUnknown3 = 143;

        public const int CapturedSubwayEumenidesSpecialAttackWeaponUnknown4 = 143;

        public const int CapturedSubwayEumenidesSpecialAttackWeaponUnknown5 = 0;

        public const int CapturedSubwayMeldedPatternsMonsterData = 203747;

        public const int CapturedSubwayMeldedPatternsWeaponLowTemplate = 121817;

        public const int CapturedSubwayMeldedPatternsWeaponHighTemplate = 121818;

        public const int CapturedSubwayMeldedPatternsWeaponQuality = 20;

        public const int CapturedSubwayAbmouthMonsterData = 155962;

        public const int CapturedSubwayAbmouthXopzMinimumDamage = 74;

        public const int CapturedSubwayAbmouthXopzMaximumDamage = 96;

        public const double CapturedSubwayAbmouthXopzFirstInitialSeconds = 0.0;

        public const double CapturedSubwayAbmouthDenwInitialSeconds = 1.476528;

        public const double CapturedSubwayAbmouthXopzSecondInitialSeconds = 3.425454;

        public const double CapturedSubwayAbmouthAttackCycleSeconds = 6.3;

        public const int CapturedSubwayAbmouthXopzWeaponSlot = 1;

        public const int CapturedSubwayAbmouthXopzLowTemplate = 203781;

        public const int CapturedSubwayAbmouthXopzHighTemplate = 203782;

        public const int CapturedSubwayAbmouthXopzTag = 0x584F505A;

        public const string CapturedSubwayAbmouthXopzName = "XOPZ";

        public const int CapturedSubwayAbmouthDenwMinimumDamage = 115;

        public const int CapturedSubwayAbmouthDenwMaximumDamage = 126;

        public const int CapturedSubwayAbmouthDenwWeaponSlot = 0;

        public const int CapturedSubwayAbmouthDenwLowTemplate = 203778;

        public const int CapturedSubwayAbmouthDenwHighTemplate = 203779;

        public const int CapturedSubwayAbmouthDenwTag = 0x44454E57;

        public const string CapturedSubwayAbmouthDenwName = "DENW";

        public const int CapturedSubwayAbmouthSpecialAttackWeaponValue = 167;

        public const int CapturedSubwayAbmouthSpecialAttackWeaponLastValue = 0;

        public const int CapturedSubwayAbmouthInfectorMonsterData = 31909;

        public const int CapturedSubwayAbmouthInfectorMinimumDamage = 21;

        public const int CapturedSubwayAbmouthInfectorMaximumDamage = 26;

        public const double CapturedSubwayAbmouthInfectorInitialAttackSeconds = 2.2;

        public const double CapturedSubwayAbmouthInfectorRechargeSeconds = 3.7;

        public const int CapturedSubwayAbmouthInfectorWeaponSlot = 0;

        public const int CapturedSubwayAbmouthInfectorLowTemplate = 201062;

        public const int CapturedSubwayAbmouthInfectorHighTemplate = 201063;

        public const int CapturedSubwayAbmouthInfectorTag = 0x444D5846;

        public const string CapturedSubwayAbmouthInfectorName = "DMXF";

        public const int CapturedSubwayAbmouthInfectorSpecialAttackWeaponValue = 107;

        public const int CapturedSubwayAbmouthInfectorSpecialAttackWeaponLastValue = 100;

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

        public const int CapturedSubwayFilthFleaPoisonMinimumDamage = 14;

        public const int CapturedSubwayFilthFleaPoisonMaximumDamage = 24;

        public const int CapturedSubwayFilthFleaMeleeMinimumDamage = 3;

        public const int CapturedSubwayFilthFleaMeleeMaximumDamage = 10;

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
