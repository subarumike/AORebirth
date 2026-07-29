namespace ZoneEngine.Core
{
    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Packets;

    /// <summary>
    /// Capture-backed Actions→Reload (V). Client CharacterAction 0xD2; server replies Reload N3.
    /// Gold: 20260728-221109 OUT CharacterAction Action=0xD2 → IN Reload Status=1 + Inventory ammo id.
    /// </summary>
    internal static class WeaponReloadRuntimeService
    {
        private const int MissingItemStatValue = 1234567890;

        internal static bool TryHandleReload(IZoneClient client, CharacterActionMessage message)
        {
            if (client == null || client.Controller == null || client.Controller.Character == null)
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            if (character.BaseInventory == null)
            {
                return false;
            }

            IInventoryPage weaponPage;
            if (!character.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage))
            {
                return false;
            }

            IItem weapon = weaponPage[(int)WeaponSlots.Righthand];
            if (weapon == null)
            {
                weapon = weaponPage[(int)WeaponSlots.LeftHand];
            }

            if (weapon == null || !IsReloadableRangedWeapon(weapon))
            {
                return true;
            }

            int clipSize = ResolveClipSize(weapon);
            Identity ammoIdentity = new Identity();
            IInventoryPage inventoryPage;
            if (character.BaseInventory.Pages.TryGetValue((int)IdentityType.Inventory, out inventoryPage))
            {
                int ammoType = weapon.GetAttribute((int)StatIds.ammotype);
                if (ammoType > 0 && ammoType != MissingItemStatValue)
                {
                    TryConsumeAmmo(character, inventoryPage, ammoType, clipSize, out ammoIdentity);
                }
            }

            int filled = clipSize > 0 ? clipSize : 1;
            weapon.SetAttribute((int)StatIds.energy, filled);

            character.Send(
                new ReloadMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Status = 1,
                    AmmoIdentity = ammoIdentity.Type != 0
                                       ? ammoIdentity
                                       : new Identity
                                         {
                                             Type = IdentityType.Inventory,
                                             Instance = 0
                                         }
                });

            WeaponItemFullUpdate.SendWeaponDefinition(character, weapon);
            return true;
        }

        private static bool IsReloadableRangedWeapon(IItem weapon)
        {
            if (weapon == null)
            {
                return false;
            }

            int can = weapon.GetAttribute((int)StatIds.can);
            if (can == MissingItemStatValue)
            {
                can = 0;
            }

            if ((can & (int)CanFlags.NoAmmo) != 0)
            {
                return false;
            }

            int ammoType = weapon.GetAttribute((int)StatIds.ammotype);
            int energy = weapon.GetAttribute((int)StatIds.energy);
            int maxEnergy = weapon.GetAttribute((int)StatIds.maxenergy);
            return (ammoType > 0 && ammoType != MissingItemStatValue)
                   || (energy >= 0 && energy != MissingItemStatValue)
                   || (maxEnergy > 0 && maxEnergy != MissingItemStatValue);
        }

        private static int ResolveClipSize(IItem weapon)
        {
            int maxEnergy = weapon.GetAttribute((int)StatIds.maxenergy);
            if (maxEnergy > 0 && maxEnergy != MissingItemStatValue)
            {
                return maxEnergy;
            }

            int energy = weapon.GetAttribute((int)StatIds.energy);
            if (energy > 0 && energy != MissingItemStatValue)
            {
                return energy;
            }

            return 1;
        }

        private static bool TryConsumeAmmo(
            ICharacter character,
            IInventoryPage inventoryPage,
            int ammoType,
            int clipSize,
            out Identity ammoIdentity)
        {
            ammoIdentity = new Identity();
            if (inventoryPage == null || ammoType <= 0)
            {
                return false;
            }

            for (int slot = inventoryPage.FirstSlotNumber;
                 slot < inventoryPage.FirstSlotNumber + inventoryPage.MaxSlots;
                 slot++)
            {
                IItem stack = inventoryPage[slot];
                if (stack == null)
                {
                    continue;
                }

                int stackAmmo = stack.GetAttribute((int)StatIds.ammotype);
                if (stackAmmo != ammoType)
                {
                    continue;
                }

                ammoIdentity = new Identity
                               {
                                   Type = IdentityType.Inventory,
                                   Instance = slot
                               };

                int take = clipSize > 0 ? clipSize : 1;
                if (stack.MultipleCount > take)
                {
                    stack.MultipleCount -= take;
                }
                else if (stack.MultipleCount > 1)
                {
                    stack.MultipleCount = 1;
                }

                // Do not delete the last unit — live often keeps a 1-count marker stack visible.
                return true;
            }

            return false;
        }
    }
}
