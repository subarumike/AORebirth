namespace ZoneEngine_New.Core.Helpers
{
    using System;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// Side-effect-free hit/damage rolls. Ports legacy CombatStrikeDamageCalculator weapon math.
    /// </summary>
    public static class DamageCalculator
    {
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
            int? attackSkillOverride = null,
            Item? damageTypeSource = null)
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
                // The type comes from the weapon the hit is shown as (CharacterWeapon.DamageTypeItem) when that differs.
                rawDamageType = NormalizeStat((damageTypeSource ?? weapon).GetStat(CharacterStat.DamageType));
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

            if (!alwaysHits && !ResolveHit(attackRating, defenseRating, NormalizeStat(target.Stats.GetOrZero(CharacterStat.AggDef))))
                return new DamageResult(false, 0, HitType.Normal);

            int overrideType = NormalizeStat(attacker.Stats.GetOrZero(CharacterStat.DamageOverrideType));
            if (overrideType > 0)
                rawDamageType = overrideType;

            // Add Damage can be negative (retail): it then takes off the hit, which still does at least 1.
            int damageBonus = TryGetAddDamageStat(rawDamageType, out CharacterStat addDamageStat)
                ? StatCollection.Normalize(attacker.Stats.GetOrZero(addDamageStat))
                : 0;

            // Armour only ever reduces damage: a negative AC adds none (live: a level 13 Workman Striker hits a
            // -405 Melee AC character for 14-32, its weapon's own range).
            int targetArmorClass = TryGetArmorStat(rawDamageType, out CharacterStat armorStat)
                ? Math.Max(0, NormalizeStat(target.Stats.GetOrZero(armorStat)))
                : 0;

            // Aimed Shot and Sneak Attack / Backstab ignore armour and start from the weapon's max damage.
            if (specialAttackStat is CharacterStat.AimedShot or CharacterStat.SneakAttack)
            {
                targetArmorClass = 0;
                weaponMin = weaponMax;
            }

            double multiplier = cappedAttackRating < 1000
                ? 1 + (cappedAttackRating / 400.0)
                : 3.5 + ((cappedAttackRating - 1000) * Post1000DamageReduction / 400.0);
            int minDamage = (int)(weaponMin * multiplier + damageBonus);
            // AC/10 comes off the maximum once, never below the minimum.
            int maxDamage = Math.Max((int)((weaponMax * multiplier) + damageBonus - (targetArmorClass / 10.0)), minDamage);

            HitType hitType = HitType.Normal;
            bool isBurst = specialAttackStat == CharacterStat.Burst;
            // Critical Increase is the crit percent: Character.BaseCriticalIncrease plus gear, buffs and perks.
            double critChance = NormalizeStat(attacker.Stats.GetOrZero(CharacterStat.CriticalIncrease));
            // PvP rules (Gameplay.json pvp.critChancePercent) scale the chance against a player.
            critChance = attacker.Playfield?.GetService<Configuration.GameplaySettings>()?.Pvp
                .ScaleCritChance(attacker, target, critChance) ?? critChance;
            // The target's Critical Decrease (stat 391, CritialResistance here) takes points off the chance.
            critChance -= NormalizeStat(target.Stats.GetOrZero(CharacterStat.CritialResistance));
            if (canCrit && !isBurst && NextInt(0, 100) < critChance)
            {
                hitType = HitType.Critical;
                // The weapon's crit bonus scales with the same attack rating multiplier as its damage, on top of the
                // max hit after armour.
                minDamage = maxDamage + (int)(weaponCritBonus * multiplier);
                maxDamage = minDamage;
            }

            int rolledMaximum = Math.Max(maxDamage, minDamage);
            int damage = minDamage >= rolledMaximum
                ? minDamage
                : NextInt(minDamage, rolledMaximum + 1);

            // TODO: Determine what to do with Rocket Launchers. Rocket-ammo weapons (Rocket Launcher 266989-266993,
            // Man Portable Anti-Vehicular Missile Launcher 264084) hit for 1 until then; a GM's hit for full damage.
            if (weapon != null && (AmmoType)NormalizeStat(weapon.GetStat(CharacterStat.AmmoType)) == AmmoType.Rockets
                && attacker.Stats.GetOrZero(CharacterStat.GmLevel) <= 0)
                damage = 1;

            return new DamageResult(true, Math.Max(1, damage), hitType);
        }

        /// <summary>Stub for nano/spell damage; returns a miss until spell math is implemented.</summary>
        public static DamageResult CalculateFromSpell(Character attacker, Character target)
        {
            ArgumentNullException.ThrowIfNull(attacker);
            ArgumentNullException.ThrowIfNull(target);
            return new DamageResult(false, 0, HitType.Normal);
        }

        /// <summary>
        /// Whether a hostile nano lands or is resisted. The nano's Attack skills (its nano skill lines) plus the caster's
        /// Add All Off. make the attack rating; its Defend skills plus the target's Add All Def. make the defense rating.
        /// A nano without Defend skills defends with Nano Resist. The two ratings go through the weapon hit curve.
        /// NOTE: the retail nano-resist formula is not known. This reuses the weapon hit curve so a nano skill against an
        /// equal Nano Resist lands about as often as an equal weapon skill hits; it may need tuning once players report
        /// land rates.
        /// </summary>
        public static bool ResolveNanoLand(
            Character caster,
            Character target,
            ItemTemplate nano,
            out int attackRating,
            out int defenseRating,
            out int landChancePercent)
        {
            ArgumentNullException.ThrowIfNull(caster);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(nano);

            attackRating = ResolveAttackRating(caster, nano);
            defenseRating = nano.Defend is { Count: > 0 }
                ? ResolveDefenseRating(target, nano)
                : NormalizeStat(target.Stats.GetOrZero(CharacterStat.NanoAC))
                    + NormalizeStat(target.Stats.GetOrZero(CharacterStat.DMSModifier));
            double hitPercentage = HitPercentage(attackRating, defenseRating, aggDef: 0);
            landChancePercent = (int)Math.Round(Math.Clamp(hitPercentage, 0, 1) * 100);
            return NextDouble() <= hitPercentage;
        }

        /// <summary>
        /// The character's attack rating (the AMS stat, 22): its wielded weapon's Attack skills weighted against the
        /// character, plus Add All Off., as a swing with that weapon rolls it. The main hand, else the combined martial
        /// arts slot, else the off hand; with no weapon, Add All Off. alone.
        /// </summary>
        public static int ResolveAttackRating(Character attacker)
        {
            ArgumentNullException.ThrowIfNull(attacker);
            CharacterWeapon? weapon = null;
            foreach (WeaponSlot slot in (ReadOnlySpan<WeaponSlot>)[WeaponSlot.MainHand, WeaponSlot.CombinedMA, WeaponSlot.OffHand])
            {
                if (attacker.Weapons.TryGetValue(slot, out weapon) && weapon != null)
                    break;
            }

            return ResolveAttackRating(attacker, weapon?.DamageItem?.Definition);
        }

        static int ResolveAttackRating(Character attacker, ItemTemplate? template)
        {
            int attackRating = 0;
            if (template?.Attack is { Count: > 0 })
            {
                foreach (System.Collections.Generic.KeyValuePair<CharacterStat, int> entry in template.Attack)
                {
                    attackRating += (int)((long)entry.Value * NormalizeStat(attacker.Stats.GetOrZero(entry.Key)) / 100);
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
                    defenseRating += (int)((long)entry.Value * NormalizeStat(target.Stats.GetOrZero(entry.Key)) / 100);
            }

            return defenseRating + NormalizeStat(target.Stats.GetOrZero(CharacterStat.DMSModifier));
        }

        static bool ResolveHit(int attackRating, int defenseRating, int aggDef)
            => NextDouble() <= HitPercentage(attackRating, defenseRating, aggDef);

        /// <summary>
        /// Hit chance = 1 / sqrt(2) x AR / DR + AggDef / 500, held to 0..1. AR is the attack rating (attack skills + Add
        /// All Off.), DR the defense rating (defense skills + Add All Def.), AggDef the defender's aggressive/defensive
        /// bar, -100 (fully defensive, harder to hit) to 100. Equal ratings at AggDef 0 hit about 71% of the time; AR at
        /// sqrt(2) x DR or more always hits. A DR under 1 counts as 1.
        /// </summary>
        static double HitPercentage(int attackRating, int defenseRating, int aggDef)
        {
            double ratio = Math.Max(0, attackRating) / (Math.Sqrt(2) * Math.Max(1, defenseRating));
            double aggression = Math.Clamp(aggDef, NanoDelayCalculator.AggDefMin, NanoDelayCalculator.AggDefMax) / 500.0;
            return Math.Clamp(ratio + aggression, 0, 1);
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

        /// <summary>Whether <paramref name="stat"/> is an absorb layer (Absorb Projectile AC 238 to Absorb Nano AC 246).</summary>
        internal static bool IsAbsorbStat(CharacterStat stat)
            => stat is >= CharacterStat.AbsorbProjectileAC and <= CharacterStat.AbsorbNanoAC;

        /// <summary>The absorb layer a hit of damage type <paramref name="rawDamageType"/> (90-97) is taken out of first.</summary>
        internal static bool TryGetAbsorbStat(int rawDamageType, out CharacterStat absorbStat)
        {
            absorbStat = rawDamageType switch
            {
                90 => CharacterStat.AbsorbProjectileAC,
                91 => CharacterStat.AbsorbMeleeAC,
                92 => CharacterStat.AbsorbEnergyAC,
                93 => CharacterStat.AbsorbChemicalAC,
                94 => CharacterStat.AbsorbRadiationAC,
                95 => CharacterStat.AbsorbColdAC,
                96 => CharacterStat.AbsorbPoisonAC,
                97 => CharacterStat.AbsorbFireAC,
                _ => 0
            };
            return absorbStat != 0;
        }

        const int MeleeDamageType = 91;

        /// <summary>
        /// The damage type (90-97, the AC stat ids) a swing with <paramref name="weapon"/> deals, as
        /// <see cref="CalculateFromWeapon"/> resolves it: the weapon's DamageType, melee for a weapon without one, and the
        /// attacker's DamageOverrideType over either. 0 with no weapon and no override.
        /// </summary>
        internal static int ResolveDamageType(Character attacker, Item? weapon)
        {
            ArgumentNullException.ThrowIfNull(attacker);
            int rawDamageType = 0;
            if (weapon != null && weapon.LowId > 0)
            {
                rawDamageType = NormalizeStat(weapon.GetStat(CharacterStat.DamageType));
                if (rawDamageType == 0)
                    rawDamageType = MeleeDamageType;
            }

            int overrideType = NormalizeStat(attacker.Stats.GetOrZero(CharacterStat.DamageOverrideType));
            return overrideType > 0 ? overrideType : rawDamageType;
        }

        /// <summary>
        /// The defender's Reflect AC (share of the damage turned back, in percent) and Max Reflected Damage (most the
        /// attacker takes back) for a damage type (90-97).
        /// </summary>
        internal static bool TryGetReflectStats(int rawDamageType, out CharacterStat reflectStat, out CharacterStat maxReflectedStat)
        {
            (reflectStat, maxReflectedStat) = rawDamageType switch
            {
                90 => (CharacterStat.ReflectProjectileAC, CharacterStat.MaxReflectedProjectileDmg),
                91 => (CharacterStat.ReflectMeleeAC, CharacterStat.MaxReflectedMeleeDmg),
                92 => (CharacterStat.ReflectEnergyAC, CharacterStat.MaxReflectedEnergyDmg),
                93 => (CharacterStat.ReflectChemicalAC, CharacterStat.MaxReflectedChemicalDmg),
                94 => (CharacterStat.ReflectRadiationAC, CharacterStat.MaxReflectedRadiationDmg),
                95 => (CharacterStat.ReflectColdAC, CharacterStat.MaxReflectedColdDmg),
                96 => (CharacterStat.ReflectPoisonAC, CharacterStat.MaxReflectedPoisonDmg),
                97 => (CharacterStat.ReflectFireAC, CharacterStat.MaxReflectedFireDmg),
                _ => (CharacterStat.Unset, CharacterStat.Unset)
            };
            return reflectStat != CharacterStat.Unset;
        }

        /// <summary>The defender's Shield AC (damage every hit of the type costs the attacker) for a damage type (90-97).</summary>
        internal static bool TryGetShieldStat(int rawDamageType, out CharacterStat shieldStat)
        {
            shieldStat = rawDamageType switch
            {
                90 => CharacterStat.ShieldProjectileAC,
                91 => CharacterStat.ShieldMeleeAC,
                92 => CharacterStat.ShieldEnergyAC,
                93 => CharacterStat.ShieldChemicalAC,
                94 => CharacterStat.ShieldRadiationAC,
                95 => CharacterStat.ShieldColdAC,
                96 => CharacterStat.ShieldPoisonAC,
                97 => CharacterStat.ShieldFireAC,
                _ => 0
            };
            return shieldStat != 0;
        }

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
