namespace AORebirth.Core.Combat
{
    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    public static class CharacterCombatStrikeBuilder
    {
        private const int MissingStatValue = 1234567890;

        private const int NormalAttackInfoAmmoCount = 40;

        private const int NormalAttackInfoHitType = (int)HitType.Normal;

        private const int PlayerUnarmedAttackInfoAmmoCount = -1;

        private const int PlayerUnarmedAttackInfoWeaponSlot = 0;

        private const int PlayerUnarmedAttackInfoWeaponInstance = 100;

        private const int PlayerUnarmedFallbackDamage = 15;

        public static CombatStrikeContext Build(Character attacker, WeaponSlot preferredSlot)
        {
            if (attacker == null)
            {
                return null;
            }

            IItem rightHand = null;
            IItem leftHand = null;
            if (attacker.BaseInventory != null
                && attacker.BaseInventory.Pages.ContainsKey((int)IdentityType.WeaponPage))
            {
                IInventoryPage weaponPage = attacker.BaseInventory.Pages[(int)IdentityType.WeaponPage];
                rightHand = weaponPage[(int)WeaponSlots.Righthand];
                leftHand = weaponPage[(int)WeaponSlots.LeftHand];
            }

            IItem weapon = ResolveWeaponForSlot(preferredSlot, rightHand, leftHand);
            if (weapon == null || weapon.LowID <= 0)
            {
                int unarmedDamage = Math.Max(
                    NormalizeStat(attacker.Stats[StatIds.mindamage].Value),
                    NormalizeStat(attacker.Stats[StatIds.maxdamage].Value));
                if (unarmedDamage <= 0)
                {
                    unarmedDamage = PlayerUnarmedFallbackDamage;
                }

                return new CombatStrikeContext
                       {
                           MinDamage = unarmedDamage,
                           MaxDamage = unarmedDamage,
                           DamageBonus = NormalizeStat(attacker.Stats[StatIds.damagebonus].Value),
                           Range = CharacterCombatRangeRules.MaxMeleeCombatDistance,
                           UsesEquippedWeapon = false,
                           AttackInfoAmmoCount = PlayerUnarmedAttackInfoAmmoCount,
                           AttackInfoWeaponSlot = PlayerUnarmedAttackInfoWeaponSlot,
                           AttackInfoHitType = NormalAttackInfoHitType,
                           AttackInfoWeaponInstance = PlayerUnarmedAttackInfoWeaponInstance,
                           DamageSource = CombatDamageSource.UnarmedAutoAttack,
                           WeaponSlot = preferredSlot
                       };
            }

            int weaponMin = NormalizeStat(weapon.GetAttribute((int)StatIds.mindamage));
            int weaponMax = NormalizeStat(weapon.GetAttribute((int)StatIds.maxdamage));
            double range = NormalizeStat(weapon.GetAttribute((int)StatIds.attackrange));
            if (range <= 0.0)
            {
                range = CharacterCombatRangeRules.MaxMeleeCombatDistance;
            }

            return new CombatStrikeContext
                   {
                       MinDamage = weaponMin,
                       MaxDamage = weaponMax > 0 ? weaponMax : weaponMin,
                       DamageBonus = NormalizeStat(weapon.GetAttribute((int)StatIds.damagebonus)),
                       Range = range,
                       UsesEquippedWeapon = true,
                       AttackInfoAmmoCount = NormalAttackInfoAmmoCount,
                       AttackInfoWeaponSlot = MapWeaponSlot(preferredSlot),
                       AttackInfoHitType = NormalAttackInfoHitType,
                       AttackInfoWeaponInstance = 0,
                       DamageSource = CombatDamageSource.WeaponAutoAttack,
                       WeaponSlot = preferredSlot,
                       WeaponLowId = weapon.LowID,
                       WeaponHighId = weapon.HighID,
                       WeaponQualityLevel = weapon.Quality,
                       RawDamageType = NormalizeStat(weapon.GetAttribute((int)StatIds.damagetype))
                   };
        }

        private static IItem ResolveWeaponForSlot(WeaponSlot slot, IItem rightHand, IItem leftHand)
        {
            switch (slot)
            {
                case WeaponSlot.MainHand:
                    return rightHand;
                case WeaponSlot.OffHand:
                    return leftHand;
                case WeaponSlot.CombinedMA:
                    return rightHand ?? leftHand;
                default:
                    return rightHand ?? leftHand;
            }
        }

        private static int MapWeaponSlot(WeaponSlot slot)
        {
            switch (slot)
            {
                case WeaponSlot.OffHand:
                    return (int)WeaponSlots.LeftHand;
                case WeaponSlot.CombinedMA:
                    return (int)WeaponSlots.Righthand;
                default:
                    return (int)WeaponSlots.Righthand;
            }
        }

        private static int NormalizeStat(int value)
        {
            return value < 0 || value == MissingStatValue ? 0 : value;
        }
    }
}
