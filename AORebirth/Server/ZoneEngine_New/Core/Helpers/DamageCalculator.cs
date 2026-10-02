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
            CharacterStat? specialAttackStat = null,
            int? minDamageOverride = null,
            int? maxDamageOverride = null,
            int? critBonusOverride = null,
            bool alwaysHits = false,
            bool canCrit = true,
            int? attackSkillOverride = null)
        {
            ArgumentNullException.ThrowIfNull(attacker);
            ArgumentNullException.ThrowIfNull(target);

            int weaponMin;
            int weaponMax;
            int weaponCritBonus;
            int rawDamageType;
            int amsCap;
            ItemTemplate? attackDefendSource;

            if (weapon != null && weapon.LowId > 0)
            {
                weaponMin = NormalizeStat(weapon.GetStat(CharacterStat.MinDamage));
                weaponMax = Math.Max(weaponMin, NormalizeStat(weapon.GetStat(CharacterStat.MaxDamage)));
                weaponCritBonus = NormalizeStat(weapon.GetStat(CharacterStat.DamageBonus));
                rawDamageType = NormalizeStat(weapon.GetStat(CharacterStat.DamageType));
                // Fists, Martial Arts and Brawl items carry no DamageType: they hit as melee like any melee weapon, so
                // melee damage modifiers and Melee AC apply.
                if (rawDamageType == 0)
                    rawDamageType = MeleeDamageType;
                amsCap = NormalizeStat(weapon.GetStat(CharacterStat.AMSCap));
                attackDefendSource = weapon.Definition;

                // Fist / incomplete templates often lack min/max. Fall back to natural damage so
                // the swing still lands and AttackInfo can drive the client animation.
                if (weaponMin <= 0 && weaponMax <= 0)
                {
                    weaponMin = Math.Max(
                        NormalizeStat(attacker.Stats.GetOrZero(CharacterStat.MinDamage)),
                        NormalizeStat(attacker.Stats.GetOrZero(CharacterStat.MaxDamage)));
                    weaponMax = Math.Max(weaponMin, 1);
                    if (weaponCritBonus <= 0)
                        weaponCritBonus = NormalizeStat(attacker.Stats.GetOrZero(CharacterStat.DamageBonus));
                    attackDefendSource = null;
                }
            }
            else
            {
                weaponMin = Math.Max(
                    NormalizeStat(attacker.Stats.GetOrZero(CharacterStat.MinDamage)),
                    NormalizeStat(attacker.Stats.GetOrZero(CharacterStat.MaxDamage)));
                weaponMax = Math.Max(weaponMin, 1);
                weaponCritBonus = NormalizeStat(attacker.Stats.GetOrZero(CharacterStat.DamageBonus));
                rawDamageType = 0;
                amsCap = 0;
                attackDefendSource = null;
            }

            // An over-equipped hand weapon hits for less: MinDamage, MaxDamage and DamageBonus lose 25% per level
            // (Gamecode.dll FUN_1009b337; level 4 = no damage).
            if (weapon != null && attackDefendSource != null && weapon.OverEquipLevel > 0)
            {
                weaponMin = Inventory.OverEquip.ScaleDamage(weaponMin, weapon.OverEquipLevel);
                weaponMax = Math.Max(weaponMin, Inventory.OverEquip.ScaleDamage(weaponMax, weapon.OverEquipLevel));
                weaponCritBonus = Inventory.OverEquip.ScaleDamage(weaponCritBonus, weapon.OverEquipLevel);
            }

            // Existing accepted NPC contracts may own numeric damage while the real
            // equipped template continues to own its attack/defense skill definition.
            weaponMin = minDamageOverride ?? weaponMin;
            weaponMax = maxDamageOverride ?? weaponMax;
            weaponCritBonus = critBonusOverride ?? weaponCritBonus;
            if (weaponMin < 0 || weaponMax < weaponMin)
                throw new ArgumentOutOfRangeException(nameof(minDamageOverride));

            // Specials use the weapon's own attack rating; the special's skill only shortens its recharge.
            // Backstab uses the Sneak Attack skill as its attack rating instead of the weapon's attack lines.
            int attackRating = attackSkillOverride.HasValue
                ? NormalizeStat(attackSkillOverride.Value) + NormalizeStat(attacker.Stats.GetOrZero(CharacterStat.AMSModifier))
                : ResolveAttackRating(attacker, attackDefendSource);
            int defenseRating = ResolveDefenseRating(target, attackDefendSource);
            int cappedAttackRating = amsCap > 0 ? Math.Min(attackRating, amsCap) : attackRating;

            if (!alwaysHits && !ResolveHit(attackRating, defenseRating))
                return new DamageResult(false, 0, HitType.Normal);

            int overrideType = NormalizeStat(attacker.Stats.GetOrZero(CharacterStat.DamageOverrideType));
            if (overrideType > 0)
                rawDamageType = overrideType;

            int damageBonus = TryGetAddDamageStat(rawDamageType, out CharacterStat addDamageStat)
                ? NormalizeStat(attacker.Stats.GetOrZero(addDamageStat))
                : 0;

            int targetArmorClass = TryGetArmorStat(rawDamageType, out CharacterStat armorStat)
                ? NormalizeStat(target.Stats.GetOrZero(armorStat))
                : 0;

            // Aimed Shot and Sneak Attack / Backstab ignore armour and start from the weapon's max damage.
            if (specialAttackStat is CharacterStat.AimedShot or CharacterStat.SneakAttack)
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
            int critIncrease = NormalizeStat(attacker.Stats.GetOrZero(CharacterStat.CriticalIncrease));
            if (canCrit && !isBurst && NextInt(0, 100) < critIncrease)
            {
                hitType = HitType.Critical;
                minDamage = maxDamage + weaponCritBonus;
                maxDamage = minDamage;
            }

            int rolledMaximum = Math.Max(maxDamage, minDamage);
            int damage = minDamage >= rolledMaximum
                ? minDamage
                : NextInt(minDamage, rolledMaximum + 1);

            return new DamageResult(true, Math.Max(1, damage), hitType);
        }

        /// <summary>Stub for nano/spell damage; returns a miss until spell math is implemented.</summary>
        public static DamageResult CalculateFromSpell(Character attacker, Character target)
        {
            ArgumentNullException.ThrowIfNull(attacker);
            ArgumentNullException.ThrowIfNull(target);
            return new DamageResult(false, 0, HitType.Normal);
        }

        static int ResolveAttackRating(Character attacker, ItemTemplate? template)
        {
            int attackRating = 0;
            if (template?.Attack is { Count: > 0 })
            {
                foreach (System.Collections.Generic.KeyValuePair<CharacterStat, int> entry in template.Attack)
                {
                    attackRating += (entry.Value / 100) * NormalizeStat(attacker.Stats.GetOrZero(entry.Key));
                }
            }

            return attackRating + NormalizeStat(attacker.Stats.GetOrZero(CharacterStat.AMSModifier));
        }

        static int ResolveDefenseRating(Character target, ItemTemplate? template)
        {
            int defenseRating = 0;
            if (template?.Defend is { Count: > 0 })
            {
                foreach (System.Collections.Generic.KeyValuePair<CharacterStat, int> entry in template.Defend)
                    defenseRating += (entry.Value / 100) * NormalizeStat(target.Stats.GetOrZero(entry.Key));
            }

            return defenseRating + NormalizeStat(target.Stats.GetOrZero(CharacterStat.DMSModifier));
        }

        static bool ResolveHit(int attackRating, int defenseRating)
        {
            double hitPercentage =
                (HitCoefficientA * (attackRating + HitCoefficientK) / (defenseRating + HitCoefficientL))
                + HitCoefficientB;
            return NextDouble() <= hitPercentage;
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

        const int MeleeDamageType = 91;

        /// <summary>The attacker's "Add. X Dam." stat for a damage type (90-97, the same ids as the AC stats).</summary>
        internal static bool TryGetAddDamageStat(int rawDamageType, out CharacterStat addDamageStat)
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

        // Floor negatives after Unset→0; combat math never wants either.
        static int NormalizeStat(int value)
            => value < 0 ? 0 : StatCollection.Normalize(value);

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
