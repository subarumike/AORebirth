namespace AORebirth.Core.Playfields
{
    using ZoneEngine.Core.Playfields;

    internal static class CapturedTempleOfThreeWindsCombatCatalog
    {
        internal const int DefenderNormalDamage = 43;
        internal const double DefenderFirstSuccessfulHitDelaySeconds = 10.915985;
        internal const double DefenderAttackRechargePolicySeconds = 10.915985;
        internal const int DefenderAttackInfoWeaponInstance = 1465538645;
        internal const int DefenderSpecialAttackLowTemplate = 205877;
        internal const int DefenderSpecialAttackHighTemplate = 205878;
        internal const string DefenderSpecialAttackName = "WZXU";

        internal static CapturedEnemyCombatContract DefenderOfTheThree()
        {
            var normalAttack = new CapturedEnemyCombatAttackDefinition(
                DefenderNormalDamage,
                DefenderNormalDamage,
                0,
                NpcCombatAttackRules.MaxMeleeCombatDistance,
                DefenderAttackRechargePolicySeconds,
                false,
                -1,
                0,
                0,
                0,
                DefenderAttackInfoWeaponInstance,
                true);
            return CapturedEnemyCombatContract.CapturedSpecialSequence(
                "20260721-035526/040324: two normal local-player AttackInfo outcomes, both 43; "
                + "weapon slot 0, unknown 0, ammo -1, weapon instance 1465538645; "
                + "SpecialAttackWeapon contains 205877/205878 tag 1465538645 name WZXU and "
                + "239/239/239/25/0; repeat cadence is unresolved, so the observed "
                + "10.915985-second attack-to-first-success interval is the private policy",
                new CapturedEnemySpecialAttackSequenceDefinition(
                    DefenderFirstSuccessfulHitDelaySeconds,
                    null,
                    normalAttack,
                    new[]
                    {
                        new CapturedEnemySpecialAttackDefinition(
                            DefenderSpecialAttackLowTemplate,
                            DefenderSpecialAttackHighTemplate,
                            DefenderAttackInfoWeaponInstance,
                            DefenderSpecialAttackName)
                    },
                    239,
                    239,
                    239,
                    25,
                    0));
        }
    }
}
