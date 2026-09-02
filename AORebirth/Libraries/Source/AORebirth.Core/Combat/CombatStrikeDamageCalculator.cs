namespace AORebirth.Core.Combat
{
    using System;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    public sealed class CombatStrikeDamageResult
    {
        public bool IsHit { get; set; }

        public HitType HitType { get; set; } = HitType.Normal;

        public int Damage { get; set; }

        public int RawDamageType { get; set; }

        public int AttackRating { get; set; }

        public int DefenseRating { get; set; }

        public int CappedAttackRating { get; set; }
    }

    public interface ICombatStrikeRandomSource
    {
        double NextDouble();

        int NextInt(int minimumInclusive, int maximumExclusive);
    }

    public sealed class CombatStrikeRandomSource : ICombatStrikeRandomSource
    {
        private static readonly Random SharedRandom = new Random();

        private static readonly object Sync = new object();

        private readonly Random instanceRandom;

        public CombatStrikeRandomSource()
        {
        }

        public CombatStrikeRandomSource(int seed)
        {
            this.instanceRandom = new Random(seed);
        }

        public double NextDouble()
        {
            lock (Sync)
            {
                return this.instanceRandom != null
                           ? this.instanceRandom.NextDouble()
                           : SharedRandom.NextDouble();
            }
        }

        public int NextInt(int minimumInclusive, int maximumExclusive)
        {
            if (maximumExclusive <= minimumInclusive)
            {
                return minimumInclusive;
            }

            lock (Sync)
            {
                return this.instanceRandom != null
                           ? this.instanceRandom.Next(minimumInclusive, maximumExclusive)
                           : SharedRandom.Next(minimumInclusive, maximumExclusive);
            }
        }
    }

    public static class CombatStrikeDamageCalculator
    {
        private const int UnsetStatSentinel = 1234567890;

        private const float HitCoefficientA = 0.6944f;

        private const float HitCoefficientB = 0.11317f;

        private const float HitCoefficientK = 45.85f;

        private const float HitCoefficientL = 38.98f;

        private const float Post1000DamageReduction = 0.3f;

        private static readonly ICombatStrikeRandomSource DefaultRandom = new CombatStrikeRandomSource();

        public static CombatStrikeDamageResult Calculate(
            Character attacker,
            Character target,
            CombatStrikeContext context)
        {
            return Calculate(attacker, target, context, DefaultRandom);
        }

        public static CombatStrikeDamageResult Calculate(
            Character attacker,
            Character target,
            CombatStrikeContext context,
            ICombatStrikeRandomSource randomSource)
        {
            if (attacker == null || target == null || context == null || randomSource == null)
            {
                return new CombatStrikeDamageResult { IsHit = false, Damage = 0 };
            }

            if (!HasRequiredWeapon(context))
            {
                return new CombatStrikeDamageResult { IsHit = false, Damage = 0 };
            }

            ItemTemplate template = ResolveWeaponTemplate(context);
            int attackRating = ResolveAttackRating(attacker, template, context.SpecialAttackStat);
            int defenseRating = ResolveDefenseRating(template, target);
            int cappedAttackRating = ResolveCappedAttackRating(attackRating, context, template);

            CombatStrikeDamageResult result = new CombatStrikeDamageResult
                                              {
                                                  AttackRating = attackRating,
                                                  DefenseRating = defenseRating,
                                                  CappedAttackRating = cappedAttackRating
                                              };

            if (!ResolveHit(attackRating, defenseRating, randomSource))
            {
                result.IsHit = false;
                result.Damage = 0;
                return result;
            }

            result.IsHit = true;
            int rawDamageType = ResolveRawDamageType(attacker, context);
            result.RawDamageType = rawDamageType;

            int weaponMin = context.MinDamage;
            int weaponMax = Math.Max(weaponMin, context.MaxDamage);
            int weaponCritBonus = context.DamageBonus;
            ApplySpecialAttackWeaponScaling(context, template, ref weaponMin, ref weaponMax);

            StatIds addDamageStat;
            int damageBonus = TryGetAddDamageStat(rawDamageType, out addDamageStat)
                                  ? attacker.Stats[addDamageStat].Value
                                  : 0;

            StatIds armorStat;
            int targetArmorClass = TryGetArmorStat(rawDamageType, out armorStat)
                                    ? target.Stats[armorStat].Value
                                    : 0;

            if (context.SpecialAttackStat.HasValue && context.SpecialAttackStat.Value == StatIds.aimedshot)
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

            bool isBurst = context.SpecialAttackStat.HasValue && context.SpecialAttackStat.Value == StatIds.burst;
            int critIncrease = attacker.Stats[StatIds.criticalincrease].Value; // TODO: Set base to 3 after new stat system is implemented.
            if (!isBurst && randomSource.NextInt(0, 100) < critIncrease)
            {
                result.HitType = HitType.Critical;
                minDamage = maxDamage + weaponCritBonus;
                maxDamage = minDamage;
            }

            int rolledMaximum = Math.Max(maxDamage, minDamage);
            int damage = minDamage >= rolledMaximum
                             ? minDamage
                             : randomSource.NextInt(minDamage, rolledMaximum + 1);

            if (context.SpecialAttackStat.HasValue && context.SpecialAttackStat.Value == StatIds.aimedshot)
            {
                damage *= randomSource.NextInt(1, 5);
                damage = Math.Min(13000, damage);
            }

            result.Damage = Math.Max(1, damage);
            if (context.OutgoingDamageScale > 1)
            {
                result.Damage = Math.Max(1, result.Damage * context.OutgoingDamageScale);
            }

            return result;
        }

        private static bool HasRequiredWeapon(CombatStrikeContext context)
        {
            return context.UsesEquippedWeapon && context.WeaponLowId > 0;
        }

        private static ItemTemplate ResolveWeaponTemplate(CombatStrikeContext context)
        {
            ItemTemplate template;
            if (ItemLoader.ItemList != null
                && ItemLoader.ItemList.TryGetValue(context.WeaponLowId, out template))
            {
                return template;
            }

            return null;
        }

        private static int ResolveAttackRating(Character attacker, ItemTemplate template, StatIds? specialAttackStat)
        {
            int attackRating = 0;
            if (template != null && template.Attack != null && template.Attack.Count > 0)
            {
                foreach (var entry in template.Attack)
                {
                    StatIds skill = specialAttackStat ?? (StatIds)entry.Key;
                    attackRating += ((entry.Value / 100) * attacker.Stats[skill].Value);
                }
            }

            return attackRating + attacker.Stats[StatIds.amsmodifier].Value;
        }

        private static int ResolveDefenseRating(ItemTemplate template, Character target)
        {
            int defenseRating = 0;
            if (template != null && template.Defend != null && template.Defend.Count > 0)
            {
                foreach (var entry in template.Defend)
                {
                    defenseRating += ((entry.Value / 100) * target.Stats[(StatIds)entry.Key].Value);
                }
            }

            return defenseRating + target.Stats[StatIds.dmsmodifier].Value;
        }

        private static int ResolveCappedAttackRating(int attackRating, CombatStrikeContext context, ItemTemplate template)
        {
            int amsCap = context.WeaponLowId > 0
                             ? NormalizeStat(ReadWeaponStat(context, StatIds.amscap))
                             : 0;
            if (amsCap > 0)
            {
                return Math.Min(attackRating, amsCap);
            }

            return attackRating;
        }

        private static int ReadWeaponStat(CombatStrikeContext context, StatIds statId)
        {
            if (ItemLoader.ItemList == null || !ItemLoader.ItemList.ContainsKey(context.WeaponLowId))
            {
                return 0;
            }

            ItemTemplate template = ItemLoader.ItemList[context.WeaponLowId];
            if (template == null || template.Stats == null || !template.Stats.ContainsKey((int)statId))
            {
                return 0;
            }

            return template.Stats[(int)statId];
        }

        private static bool ResolveHit(int attackRating, int defenseRating, ICombatStrikeRandomSource randomSource)
        {
            double hitPercentage = (HitCoefficientA * (attackRating + HitCoefficientK) / (defenseRating + HitCoefficientL))
                                   + HitCoefficientB;
            return randomSource.NextDouble() <= hitPercentage;
        }

        private static int ResolveRawDamageType(Character attacker, CombatStrikeContext context)
        {
            int overrideType = attacker.Stats[StatIds.damageoverridetype].Value;
            if (overrideType > 0)
            {
                return overrideType;
            }

            return context.RawDamageType;
        }

        private static void ApplySpecialAttackWeaponScaling(
            CombatStrikeContext context,
            ItemTemplate template,
            ref int weaponMin,
            ref int weaponMax)
        {
            if (!context.SpecialAttackStat.HasValue)
            {
                return;
            }

            switch (context.SpecialAttackStat.Value)
            {
                case StatIds.burst:
                    weaponMin *= 3;
                    weaponMax *= 3;
                    break;
                case StatIds.fullauto:
                    int clip = ReadWeaponStat(context, StatIds.maxenergy);
                    if (clip <= 0 && template != null && template.Stats != null && template.Stats.ContainsKey((int)StatIds.maxenergy))
                    {
                        clip = template.Stats[(int)StatIds.maxenergy];
                    }

                    clip = Math.Max(1, clip);
                    weaponMin *= clip;
                    weaponMax *= clip;
                    break;
            }
        }

        private static bool TryGetArmorStat(int rawDamageType, out StatIds armorStat)
        {
            switch (rawDamageType)
            {
                case 90:
                    armorStat = StatIds.projectileac;
                    return true;
                case 91:
                    armorStat = StatIds.meleeac;
                    return true;
                case 92:
                    armorStat = StatIds.energyac;
                    return true;
                case 93:
                    armorStat = StatIds.chemicalac;
                    return true;
                case 94:
                    armorStat = StatIds.radiationac;
                    return true;
                case 95:
                    armorStat = StatIds.coldac;
                    return true;
                case 96:
                    armorStat = StatIds.poisonac;
                    return true;
                case 97:
                    armorStat = StatIds.fireac;
                    return true;
                default:
                    armorStat = 0;
                    return false;
            }
        }

        private static bool TryGetAddDamageStat(int rawDamageType, out StatIds addDamageStat)
        {
            switch (rawDamageType)
            {
                case 90:
                    addDamageStat = StatIds.projectiledamagemodifier;
                    return true;
                case 91:
                    addDamageStat = StatIds.meleedamagemodifier;
                    return true;
                case 92:
                    addDamageStat = StatIds.energydamagemodifier;
                    return true;
                case 93:
                    addDamageStat = StatIds.chemicaldamagemodifier;
                    return true;
                case 94:
                    addDamageStat = StatIds.radiationdamagemodifier;
                    return true;
                case 95:
                    addDamageStat = StatIds.colddamagemodifier;
                    return true;
                case 96:
                    addDamageStat = StatIds.poisondamagemodifier;
                    return true;
                case 97:
                    addDamageStat = StatIds.firedamagemodifier;
                    return true;
                default:
                    addDamageStat = 0;
                    return false;
            }
        }

        private static int NormalizeStat(int value)
        {
            return value < 0 ? 0 : value;
        }
    }
}
