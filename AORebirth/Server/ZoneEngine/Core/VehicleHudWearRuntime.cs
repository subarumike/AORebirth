namespace ZoneEngine.Core
{
    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    /// <summary>
    /// Capture 20260723-133842: Air/Water/Ground vehicles (IsVehicle) wear on WeaponPage Hud1.
    /// ClientMove Inventory→Hud1 → ContainerAddItem + TemplateAction(Unknown2=6, WeaponPage:0001);
    /// unequip WeaponPage:Hud1→0x6F → TemplateAction(Unknown2=7) + ContainerAddItem.
    /// OnWear sets MonsterData/IsVehicle; that must be cleared on unequip or ToWield (MonsterData==0) blocks re-wear.
    /// </summary>
    internal static class VehicleHudWearRuntime
    {
        private const int MissingStat = 1234567890;

        private const int PlacementStatId = 298;

        private const int IsVehicleStatId = 658;

        private const int MonsterScaleStatId = 360;

        internal static bool IsVehicleItem(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            int isVehicle = item.GetAttribute(IsVehicleStatId);
            return isVehicle != MissingStat && isVehicle != 0;
        }

        /// <summary>
        /// AO Placement (298) bitfield: allowed when (placement &amp; (1 &lt;&lt; slot)) != 0.
        /// Captured yalm 117322 Placement=2 → Hud1 only.
        /// </summary>
        internal static bool AllowsWeaponSlot(IItem item, int slot)
        {
            if (item == null || slot < 0 || slot > 31)
            {
                return false;
            }

            int placement = item.GetAttribute(PlacementStatId);
            if (placement == MissingStat || placement <= 0)
            {
                // No placement data: do not block; ToWield still gates skills.
                return true;
            }

            return (placement & (1 << slot)) != 0;
        }

        internal static void NoteEquipped(ICharacter character, IItem item, int slot)
        {
            if (character == null || !IsVehicleItem(item))
            {
                return;
            }

            // CalculateSkills already runs OnWear (MonsterShape/CanFly/Modify/…).
            // Mark that morph came from HUD vehicle equipment so unequip can reverse it.
            AdventurerMorphFlightRuntime.MarkEquipmentVehicleMorph(character);
        }

        internal static void NoteUnequipped(ICharacter character, IItem item)
        {
            if (character == null || !IsVehicleItem(item))
            {
                return;
            }

            AdventurerMorphFlightRuntime.ClearEquipmentVehicleMorph(character);
        }

        /// <summary>
        /// ToWield requires MonsterData==0 and ExpansionPlayfield==0. Equipment morph and
        /// unset ExpansionPlayfield sentinel must not permanently block Hud1 vehicle wear.
        /// </summary>
        internal static bool EvaluateVehicleWieldRequirements(
            ICharacter character,
            IItem itemToEquip,
            IItem currentlyEquippedInSlot,
            Func<bool> checkRequirements)
        {
            if (character == null || checkRequirements == null)
            {
                return false;
            }

            if (!IsVehicleItem(itemToEquip))
            {
                return checkRequirements();
            }

            int previousMonsterData = character.Stats[StatIds.monsterdata].Value;
            int previousExpansion = character.Stats[StatIds.expansionplayfield].Value;
            bool clearedMonsterData = false;
            bool fixedExpansion = false;

            try
            {
                if (previousExpansion == MissingStat)
                {
                    int playfieldId = character.Playfield != null
                                         ? character.Playfield.Identity.Instance
                                         : 0;
                    AdventurerMorphFlightRuntime.SyncExpansionPlayfield(character, playfieldId);
                    fixedExpansion = true;
                }

                // Hot-swap or stale equipment morph: pretend MonsterData is clear for the check.
                if (previousMonsterData != 0
                    && (IsVehicleItem(currentlyEquippedInSlot)
                        || AdventurerMorphFlightRuntime.HasEquipmentVehicleMorph(character)))
                {
                    character.Stats[StatIds.monsterdata].Value = 0;
                    clearedMonsterData = true;
                }

                return checkRequirements();
            }
            finally
            {
                if (clearedMonsterData)
                {
                    character.Stats[StatIds.monsterdata].Value = previousMonsterData;
                }

                // Leave ExpansionPlayfield at the synced value when we fixed the sentinel.
                if (!fixedExpansion)
                {
                    // no-op
                }
            }
        }

        internal static void SetMonsterScale(Character character, int scale)
        {
            if (character == null)
            {
                return;
            }

            character.Stats[StatIds.monsterscale].Value = scale;
        }
    }
}
