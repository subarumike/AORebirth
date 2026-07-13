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
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Textures;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Packets;

    #endregion

    /// <summary>
    /// Capture-backed appearance for Bureaucrat A141 guardian pets.
    /// Source: captures 20260713-153757 (CEO Guardian texture/mesh reference).
    /// Owner spawn SCFU is replayed from capture wire; this class equips the sword and
    /// builds the playfield announce SCFU for other players.
    /// </summary>
    internal static class PetBureaucratGuardianAppearance
    {
        private const int CorporateGuardianNanoId = 235386;
        private const int CeoGuardianNanoId = 273300;

        // Live capture 20260713-153757: both guardians share mesh 273304 on monster data 227701.
        private const int GuardianBodyMeshId = 273304;

        private const int CorporateGuardianWeaponId = 154505;
        private const int CeoGuardianWeaponId = 273306;

        private const int GuardianWeaponQuality = 200;
        private const int CorporateGuardianWeaponFlags = 0x403;
        private const int CeoGuardianWeaponFlags = 0x401;

        private const int GuardianBodyMeshPosition = 1;
        private const int GuardianBodyMeshLayer = 2;

        public static bool IsGuardianNano(int summonNanoId)
        {
            return summonNanoId == CorporateGuardianNanoId || summonNanoId == CeoGuardianNanoId;
        }

        public static bool IsGuardianPet(ICharacter pet)
        {
            if (pet == null || !PetCombatRules.IsPlayerOwnedAttackPet(pet))
            {
                return false;
            }

            return string.Equals(pet.Name, "Corporate Guardian", StringComparison.Ordinal)
                   || string.Equals(pet.Name, "CEO Guardian", StringComparison.Ordinal);
        }

        public static void Apply(Character petCharacter, int summonNanoId)
        {
            if (petCharacter == null || !IsGuardianNano(summonNanoId))
            {
                return;
            }

            petCharacter.Textures.Clear();
            for (int place = 0; place < 5; place++)
            {
                petCharacter.Textures.Add(new AOTextures(place, 0));
            }

            petCharacter.MeshLayer.Clear();
            petCharacter.SocialMeshLayer.Clear();
            petCharacter.MeshLayer.AddMesh(
                GuardianBodyMeshPosition,
                GuardianBodyMeshId,
                0,
                GuardianBodyMeshLayer);
            petCharacter.SocialMeshLayer.AddMesh(
                GuardianBodyMeshPosition,
                GuardianBodyMeshId,
                0,
                GuardianBodyMeshLayer);

            TryEquipGuardianWeapon(petCharacter, summonNanoId);
        }

        public static SimpleCharFullUpdateMessage BuildAnnounceVisualUpdate(
            Character petCharacter,
            int summonNanoId)
        {
            SimpleCharFullUpdateMessage message = SimpleCharFullUpdate.ConstructMessage(petCharacter);
            PetSummonScfuExtensions.ApplyCapturedGuardianPetMetadata(message, summonNanoId);
            return message;
        }

        private static bool TryEquipGuardianWeapon(Character petCharacter, int summonNanoId)
        {
            if (petCharacter == null || petCharacter.BaseInventory == null)
            {
                return false;
            }

            int weaponItemId = summonNanoId == CeoGuardianNanoId
                ? CeoGuardianWeaponId
                : CorporateGuardianWeaponId;
            int weaponFlags = summonNanoId == CeoGuardianNanoId
                ? CeoGuardianWeaponFlags
                : CorporateGuardianWeaponFlags;

            PetShellDisplayItemCatalog.EnsureRegistered(weaponItemId, weaponItemId);
            if (!ItemLoader.ItemList.ContainsKey(weaponItemId))
            {
                return false;
            }

            IInventoryPage weaponPage;
            if (!petCharacter.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage))
            {
                return false;
            }

            int leftHandSlot = (int)WeaponSlots.LeftHand;
            if (weaponPage.ValidSlot(leftHandSlot) && weaponPage[leftHandSlot] != null)
            {
                weaponPage.Remove(leftHandSlot);
            }

            int rightHandSlot = (int)WeaponSlots.Righthand;
            if (!weaponPage.ValidSlot(rightHandSlot))
            {
                return false;
            }

            if (weaponPage[rightHandSlot] != null)
            {
                weaponPage.Remove(rightHandSlot);
            }

            var weapon = new Item(
                GuardianWeaponQuality,
                weaponItemId,
                weaponItemId)
            {
                MultipleCount = 1,
                Flags = weaponFlags
            };

            return weaponPage.Add(rightHandSlot, weapon) == InventoryError.OK;
        }
    }
}
