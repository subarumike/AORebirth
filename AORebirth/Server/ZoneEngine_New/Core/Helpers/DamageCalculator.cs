namespace ZoneEngine_New.Core.Helpers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// Side-effect-free hit/damage rolls. Ports legacy CombatStrikeDamageCalculator weapon math.
    /// </summary>
    public static class DamageCalculator
    {
        const float HitCoefficientA = 0.6944f;
        const float HitCoefficientB = 0.11317f;
        const float HitCoefficientK = 45.85f;
        const float HitCoefficientL = 38.98f;
        const float Post1000DamageReduction = 0.3f;

        static readonly Random SharedRandom = new();
        static readonly object RandomSync = new();

        public readonly struct DamageResult
        {
            public DamageResult(bool isHit, int damage, HitType hitType)
            {
                IsHit = isHit;
                Damage = damage;
                HitType = hitType;
            }

            public bool IsHit { get; }

            public int Damage { get; }

            public HitType HitType { get; }
        }

        public static DamageResult CalculateFromWeapon(
            Character attacker,
            Character target,
            Item? weapon,
            CharacterStat? specialAttackStat = null)
        {
            ArgumentNullException.ThrowIfNull(attacker);
            ArgumentNullException.ThrowIfNull(target);

            int weaponMin;
            int weaponMax;
            int weaponCritBonus;
            int rawDamageType;
            int amsCap;
            int fullAutoClip;
            ItemTemplate? attackDefendSource;

            if (weapon != null && weapon.LowId > 0)
            {
                weaponMin = NormalizeStat(weapon.GetStat(CharacterStat.MinDamage));
                weaponMax = Math.Max(weaponMin, NormalizeStat(weapon.GetStat(CharacterStat.MaxDamage)));
                weaponCritBonus = NormalizeStat(weapon.GetStat(CharacterStat.DamageBonus));
                rawDamageType = NormalizeStat(weapon.GetStat(CharacterStat.DamageType));
                amsCap = NormalizeStat(weapon.GetStat(CharacterStat.AMSCap));
                fullAutoClip = NormalizeStat(weapon.GetStat(CharacterStat.MaxEnergy));
                attackDefendSource = weapon.Definition;
                if (weaponMin <= 0 && weaponMax <= 0)
                    return new DamageResult(false, 0, HitType.Normal);
            }
            else
            {
                weaponMin = Math.Max(
                    NormalizeStat(attacker.Stats.Get(CharacterStat.MinDamage)),
                    NormalizeStat(attacker.Stats.Get(CharacterStat.MaxDamage)));
                weaponMax = weaponMin;
                weaponCritBonus = NormalizeStat(attacker.Stats.Get(CharacterStat.DamageBonus));
                rawDamageType = 0;
                amsCap = 0;
                fullAutoClip = 0;
                attackDefendSource = null;
            }

            int attackRating = ResolveAttackRating(attacker, attackDefendSource, specialAttackStat);
            int defenseRating = ResolveDefenseRating(target, attackDefendSource);
            int cappedAttackRating = amsCap > 0 ? Math.Min(attackRating, amsCap) : attackRating;

            if (!ResolveHit(attackRating, defenseRating))
                return new DamageResult(false, 0, HitType.Normal);

            int overrideType = NormalizeStat(attacker.Stats.Get(CharacterStat.DamageOverrideType));
            if (overrideType > 0)
                rawDamageType = overrideType;

            ApplySpecialAttackWeaponScaling(specialAttackStat, fullAutoClip, ref weaponMin, ref weaponMax);

            int damageBonus = TryGetAddDamageStat(rawDamageType, out CharacterStat addDamageStat)
                ? NormalizeStat(attacker.Stats.Get(addDamageStat))
                : 0;

            int targetArmorClass = TryGetArmorStat(rawDamageType, out CharacterStat armorStat)
                ? NormalizeStat(target.Stats.Get(armorStat))
                : 0;

            if (specialAttackStat == CharacterStat.AimedShot)
            {
                targetArmorClass = 0;
                weaponMin = weaponMax;
            }

            int minDamage;
            int maxDamage;
            if (cappedAttackRating < 1000)
            {
                minDamage = (int)(weaponMin * (1 + (cappedAttackRating / 400.0)) + damageBonus);
                maxDamage = Math.Max(
                    (int)((weaponMax * (1 + (cappedAttackRating / 400.0)) + damageBonus) - (targetArmorClass / 10.0)),
                    minDamage);
            }
            else
            {
                double multiplier = 3.5 + ((cappedAttackRating - 1000) * Post1000DamageReduction / 400.0);
                minDamage = (int)(weaponMin * multiplier + damageBonus);
                maxDamage = Math.Max((int)(weaponMax * multiplier + damageBonus), minDamage);
            }

            maxDamage -= targetArmorClass / 10;

            HitType hitType = HitType.Normal;
            bool isBurst = specialAttackStat == CharacterStat.Burst;
            int critIncrease = NormalizeStat(attacker.Stats.Get(CharacterStat.CriticalIncrease));
            if (!isBurst && NextInt(0, 100) < critIncrease)
            {
                hitType = HitType.Critical;
                minDamage = maxDamage + weaponCritBonus;
                maxDamage = minDamage;
            }

            int rolledMaximum = Math.Max(maxDamage, minDamage);
            int damage = minDamage >= rolledMaximum
                ? minDamage
                : NextInt(minDamage, rolledMaximum + 1);

            if (specialAttackStat == CharacterStat.AimedShot)
            {
                damage *= NextInt(1, 5);
                damage = Math.Min(13000, damage);
            }

            return new DamageResult(true, Math.Max(1, damage), hitType);
        }

        /// <summary>Stub for nano/spell damage; returns a miss until spell math is implemented.</summary>
        public static DamageResult CalculateFromSpell(Character attacker, Character target)
        {
            ArgumentNullException.ThrowIfNull(attacker);
            ArgumentNullException.ThrowIfNull(target);
            return new DamageResult(false, 0, HitType.Normal);
        }

        static int ResolveAttackRating(
            Character attacker,
            ItemTemplate? template,
            CharacterStat? specialAttackStat)
        {
            int attackRating = 0;
            if (template?.Attack is { Count: > 0 })
            {
                foreach (System.Collections.Generic.KeyValuePair<CharacterStat, int> entry in template.Attack)
                {
                    CharacterStat skill = specialAttackStat ?? entry.Key;
                    attackRating += (entry.Value / 100) * NormalizeStat(attacker.Stats.Get(skill));
                }
            }

            return attackRating + NormalizeStat(attacker.Stats.Get(CharacterStat.AMSModifier));
        }

        static int ResolveDefenseRating(Character target, ItemTemplate? template)
        {
            int defenseRating = 0;
            if (template?.Defend is { Count: > 0 })
            {
                foreach (System.Collections.Generic.KeyValuePair<CharacterStat, int> entry in template.Defend)
                    defenseRating += (entry.Value / 100) * NormalizeStat(target.Stats.Get(entry.Key));
            }

            return defenseRating + NormalizeStat(target.Stats.Get(CharacterStat.DMSModifier));
        }

        static bool ResolveHit(int attackRating, int defenseRating)
        {
            double hitPercentage =
                (HitCoefficientA * (attackRating + HitCoefficientK) / (defenseRating + HitCoefficientL))
                + HitCoefficientB;
            return NextDouble() <= hitPercentage;
        }

        static void ApplySpecialAttackWeaponScaling(
            CharacterStat? specialAttackStat,
            int fullAutoClip,
            ref int weaponMin,
            ref int weaponMax)
        {
            if (!specialAttackStat.HasValue)
                return;

            switch (specialAttackStat.Value)
            {
                case CharacterStat.Burst:
                    weaponMin *= 3;
                    weaponMax *= 3;
                    break;
                case CharacterStat.FullAuto:
                    int clip = Math.Max(1, fullAutoClip);
                    weaponMin *= clip;
                    weaponMax *= clip;
                    break;
            }
        }

        static bool TryGetArmorStat(int rawDamageType, out CharacterStat armorStat)
        {
            switch (rawDamageType)
            {
                case 90:
                    armorStat = CharacterStat.ProjectileAC;
                    return true;
                case 91:
                    armorStat = CharacterStat.MeleeAC;
                    return true;
                case 92:
                    armorStat = CharacterStat.EnergyAC;
                    return true;
                case 93:
                    armorStat = CharacterStat.ChemicalAC;
                    return true;
                case 94:
                    armorStat = CharacterStat.RadiationAC;
                    return true;
                case 95:
                    armorStat = CharacterStat.ColdAC;
                    return true;
                case 96:
                    armorStat = CharacterStat.PoisonAC;
                    return true;
                case 97:
                    armorStat = CharacterStat.FireAC;
                    return true;
                default:
                    armorStat = 0;
                    return false;
            }
        }

        static bool TryGetAddDamageStat(int rawDamageType, out CharacterStat addDamageStat)
        {
            switch (rawDamageType)
            {
                case 90:
                    addDamageStat = CharacterStat.ProjectileDamageModifier;
                    return true;
                case 91:
                    addDamageStat = CharacterStat.MeleeDamageModifier;
                    return true;
                case 92:
                    addDamageStat = CharacterStat.EnergyDamageModifier;
                    return true;
                case 93:
                    addDamageStat = CharacterStat.ChemicalDamageModifier;
                    return true;
                case 94:
                    addDamageStat = CharacterStat.RadiationDamageModifier;
                    return true;
                case 95:
                    addDamageStat = CharacterStat.ColdDamageModifier;
                    return true;
                case 96:
                    addDamageStat = CharacterStat.PoisonDamageModifier;
                    return true;
                case 97:
                    addDamageStat = CharacterStat.FireDamageModifier;
                    return true;
                default:
                    addDamageStat = 0;
                    return false;
            }
        }

        static int NormalizeStat(int value)
            => value < 0 || StatCollection.IsUnset(value) ? 0 : value;

        static double NextDouble()
        {
            lock (RandomSync)
                return SharedRandom.NextDouble();
        }

        static int NextInt(int minimumInclusive, int maximumExclusive)
        {
            if (maximumExclusive <= minimumInclusive)
                return minimumInclusive;

            lock (RandomSync)
                return SharedRandom.Next(minimumInclusive, maximumExclusive);
        }
    }
}
