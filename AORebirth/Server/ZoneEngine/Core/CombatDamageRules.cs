namespace ZoneEngine.Core
{
    using System;

    public static class CombatDamageRules
    {
        public const int PlayerFallbackDamage = 15;

        public const int NpcFallbackDamage = 1;

        public static int Calculate(
            int minDamage,
            int maxDamage,
            int damageBonus,
            int level,
            bool isPlayer)
        {
            int normalizedMinDamage = Math.Max(0, minDamage);
            int normalizedMaxDamage = Math.Max(normalizedMinDamage, maxDamage);
            int normalizedDamageBonus = Math.Max(0, damageBonus);
            int fallbackDamage = isPlayer ? PlayerFallbackDamage : NpcFallbackDamage;

            if (normalizedMaxDamage > 0)
            {
                return Math.Max(fallbackDamage, normalizedMaxDamage + normalizedDamageBonus);
            }

            return Math.Max(fallbackDamage, level + normalizedDamageBonus);
        }
    }
}
