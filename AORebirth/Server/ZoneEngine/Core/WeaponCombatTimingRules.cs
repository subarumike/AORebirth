namespace ZoneEngine.Core
{
    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using ZoneEngine.Core.Playfields;

    /// <summary>
    /// Player auto-attack cycle from weapon AttackDelay/RechargeDelay (centiseconds),
    /// AggDef, and Ranged / Melee / Physical initiative.
    ///
    /// Listed speeds are at ~87.5% Aggressive. Full Aggressive is 0.25s faster on each
    /// side; full Defensive is 1.75s slower. Initiative: 600 → −1s attack / −2s recharge
    /// up to 1200, then further init /3. Floor 1.0s per side.
    /// </summary>
    internal static class WeaponCombatTimingRules
    {
        private const int MissingItemStatValue = 1234567890;

        private const double MinimumSideSeconds = 1.0;

        private const double AdvertisedAggDefPercent = 87.5;

        private const double AggDefStepPercent = 12.5;

        private const double AggDefStepSeconds = 0.25;

        private const int InitiativeSoftCap = 1200;

        private const int AttackInitiativePerSecond = 600;

        private const int RechargeInitiativePerSecond = 300;

        internal static double CalculateCycleSeconds(
            ICharacter attacker,
            int attackDelayCentiseconds,
            int rechargeDelayCentiseconds,
            IItem weapon)
        {
            int attackCs = NormalizeDelayCentiseconds(attackDelayCentiseconds);
            int rechargeCs = NormalizeDelayCentiseconds(rechargeDelayCentiseconds);
            if (attackCs <= 0 && rechargeCs <= 0)
            {
                attackCs = 100;
                rechargeCs = 100;
            }

            if (attackCs <= 0)
            {
                attackCs = 100;
            }

            if (rechargeCs <= 0)
            {
                rechargeCs = 100;
            }

            double listedAttack = attackCs / 100.0;
            double listedRecharge = rechargeCs / 100.0;
            double aggShift = ResolveAggDefShiftSeconds(attacker);
            int initiative = ResolveInitiative(attacker, weapon);
            int effectiveInitiative = ApplyInitiativeSoftCap(initiative);

            double adjustedAttack = Math.Max(
                MinimumSideSeconds,
                listedAttack + aggShift - (effectiveInitiative / (double)AttackInitiativePerSecond));
            double adjustedRecharge = Math.Max(
                MinimumSideSeconds,
                listedRecharge + aggShift - (effectiveInitiative / (double)RechargeInitiativePerSecond));

            return Math.Max(0.25, adjustedAttack + adjustedRecharge);
        }

        private static double ResolveAggDefShiftSeconds(ICharacter attacker)
        {
            if (attacker == null || attacker.Stats == null)
            {
                return 0.0;
            }

            // 100 = full Aggressive, 0 = full Defensive.
            double aggDef = Math.Max(0.0, Math.Min(100.0, attacker.Stats[StatIds.aggdef].Value));
            return ((AdvertisedAggDefPercent - aggDef) / AggDefStepPercent) * AggDefStepSeconds;
        }

        private static int ResolveInitiative(ICharacter attacker, IItem weapon)
        {
            if (attacker == null || attacker.Stats == null)
            {
                return 0;
            }

            int initiativeType = weapon == null
                                     ? 0
                                     : NormalizeDelayCentiseconds(weapon.GetAttribute((int)StatIds.initiativetype));

            switch (initiativeType)
            {
                case 2:
                    return Math.Max(0, attacker.Stats[StatIds.distanceweaponinitiative].Value);
                case 3:
                    return Math.Max(0, attacker.Stats[StatIds.physicalprowessinitiative].Value);
                case 1:
                    return Math.Max(0, attacker.Stats[StatIds.closecombatinitiative].Value);
            }

            int inferred = InferInitiativeStatId(weapon);
            if (inferred == (int)StatIds.distanceweaponinitiative)
            {
                return Math.Max(0, attacker.Stats[StatIds.distanceweaponinitiative].Value);
            }

            if (inferred == (int)StatIds.physicalprowessinitiative)
            {
                return Math.Max(0, attacker.Stats[StatIds.physicalprowessinitiative].Value);
            }

            return Math.Max(0, attacker.Stats[StatIds.closecombatinitiative].Value);
        }

        private static int InferInitiativeStatId(IItem weapon)
        {
            if (weapon == null)
            {
                // Fists / MA → Physical Prowess.
                return (int)StatIds.physicalprowessinitiative;
            }

            ItemTemplate template;
            if (!ItemLoader.ItemList.TryGetValue(weapon.LowID, out template)
                || template.Attack == null
                || template.Attack.Count == 0)
            {
                return (int)StatIds.closecombatinitiative;
            }

            int primarySkill = 0;
            int primaryWeight = -1;
            foreach (var pair in template.Attack)
            {
                if (pair.Value > primaryWeight)
                {
                    primaryWeight = pair.Value;
                    primarySkill = pair.Key;
                }
            }

            if (primarySkill == (int)StatIds.bow
                || primarySkill == (int)StatIds.pistol
                || primarySkill == (int)StatIds.rifle
                || primarySkill == (int)StatIds.submachinegun
                || primarySkill == (int)StatIds.shotgun
                || primarySkill == (int)StatIds.assaultrifle
                || primarySkill == (int)StatIds.grenade
                || primarySkill == (int)StatIds.throwingknife
                || primarySkill == (int)StatIds.throwngrapplingweapons)
            {
                return (int)StatIds.distanceweaponinitiative;
            }

            if (primarySkill == (int)StatIds.martialarts
                || primarySkill == (int)StatIds.meleemultiple)
            {
                return (int)StatIds.physicalprowessinitiative;
            }

            return (int)StatIds.closecombatinitiative;
        }

        private static int ApplyInitiativeSoftCap(int initiative)
        {
            if (initiative <= InitiativeSoftCap)
            {
                return initiative;
            }

            return InitiativeSoftCap + ((initiative - InitiativeSoftCap) / 3);
        }

        private static int NormalizeDelayCentiseconds(int value)
        {
            if (value == MissingItemStatValue || value < 0)
            {
                return 0;
            }

            // Guard only — corrupt multi-second-per-side stats must not become 15s cycles.
            return value > 500 ? 100 : value;
        }
    }
}
