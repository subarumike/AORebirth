#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Capture-backed MP attack-pet combat from 20260710-220653 (Summon Demon / PT56).
    /// </summary>
    internal static class PetCombatRules
    {
        public const int AttackPetLeftWeaponTemplate = 0x0001D73A;

        public const int AttackPetRightWeaponTemplate = 0x0001D73B;

        public const int AttackPetLeftWeaponHighTemplate = 0x0001D73D;

        public const int AttackPetRightWeaponHighTemplate = 0x0001D73E;

        private const int UnsetTemplateStatValue = 1234567890;

        public const int AttackPetLeftWeaponTag = 0x4D455732;

        public const int AttackPetRightWeaponTag = 0x4D455731;

        public const string AttackPetLeftWeaponName = "MEW2";

        public const string AttackPetRightWeaponName = "MEW1";

        public const int AttackPetSpecialAttackWeaponValue = 0x349;

        public const int AttackPetAttackInfoWeaponSlot = 1;

        public const int AttackPetAttackInfoUnk1 = 4;

        public const int AttackPetAttackInfoHitType = 1;

        // Used only when an attack pet has no capture-backed combat profile and no mob damage stats.
        public const int AttackPetFallbackDamage = 930;

        public const double AttackPetRechargeSeconds = 2.0;

        public const double HealCastRange = 20.0;

        public const double HealCastRetrySeconds = 2.5;

        public const int HealingPetCapturedCurrentNano = 13184;

        public const int HealingPetCapturedMaxNano = 13184;

        // Capture 20260711-172653: Metaphysical Demon regens +162 HP about every 1.0s
        // (19481 max life -> life/120). Live AO also sends Stat(Health) each tick.
        public const int PetHealthRegenMaxLifeDivisor = 120;

        public const double PetHealthRegenIntervalSeconds = 1.0;

        public const int PetNanoRegenMaxNanoDivisor = 120;

        public const double PetNanoRegenIntervalSeconds = 1.0;

        public static int ResolvePetHealthRegenDelta(int maxHealth)
        {
            return Math.Max(1, maxHealth / PetHealthRegenMaxLifeDivisor);
        }

        public static int ResolvePetNanoRegenDelta(int maxNano)
        {
            if (maxNano <= 0)
            {
                return 0;
            }

            return Math.Max(1, maxNano / PetNanoRegenMaxNanoDivisor);
        }

        // NPC regen: much lower than pets, every 5s, paused while under attack.
        public const int NpcHealthRegenMaxLifeDivisor = 2400;

        public const double NpcHealthRegenIntervalSeconds = 5.0;

        public static int ResolveNpcHealthRegenDelta(int maxHealth)
        {
            return Math.Max(1, maxHealth / NpcHealthRegenMaxLifeDivisor);
        }

        public static bool IsPlayerOwnedPet(ICharacter character)
        {
            if (character == null)
            {
                return false;
            }

            int petMaster = character.Stats[StatIds.petmaster].Value;
            return petMaster > 0 && petMaster != UnsetTemplateStatValue;
        }

        public static bool IsPlayerOwnedAttackPet(ICharacter pet)
        {
            if (!IsPlayerOwnedPet(pet) || pet.Playfield == null)
            {
                return false;
            }

            ICharacter owner = ResolvePetOwner(pet);
            if (owner == null)
            {
                return false;
            }

            ICharacter attackPet = PetRuntimeService.Default.GetActivePetInStrain(
                owner,
                PetSlotClassifier.RegularPetStrain);
            return attackPet != null && attackPet.Identity == pet.Identity;
        }

        public static bool IsPlayerOwnedHealingPet(ICharacter pet)
        {
            if (!IsPlayerOwnedPet(pet) || pet.Playfield == null)
            {
                return false;
            }

            ICharacter owner = ResolvePetOwner(pet);
            if (owner == null)
            {
                return false;
            }

            ICharacter healPet = PetRuntimeService.Default.GetActivePetInStrain(
                owner,
                PetSlotClassifier.HealingPetStrain);
            return healPet != null && healPet.Identity == pet.Identity;
        }

        public static ICharacter ResolvePetOwner(ICharacter pet)
        {
            if (!IsPlayerOwnedPet(pet))
            {
                return null;
            }

            return pet.Playfield.FindByIdentity<ICharacter>(
                new Identity
                {
                    Type = IdentityType.CanbeAffected,
                    Instance = pet.Stats[StatIds.petmaster].Value
                });
        }
    }
}
