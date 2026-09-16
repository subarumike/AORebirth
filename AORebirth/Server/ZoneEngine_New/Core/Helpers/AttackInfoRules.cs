namespace ZoneEngine_New.Core.Helpers
{
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// AttackInfo field selection for auto-attack hits.
    /// Player synthetic fists must use the unarmed shape (ammo -1, slot 0, instance 0) — never RH/LH
    /// without WIFU. NPC tag-backed weapons use SAW tags in Unknown6.
    /// </summary>
    internal static class AttackInfoRules
    {
        /// <summary>AOEmu treats AmmoType 10 like melee for AttackInfo ammo.</summary>
        const int MeleeStyleAmmoType = 10;

        public static bool IsUnarmedPresentation(CharacterWeapon? armed, Item? weapon)
        {
            if (armed != null && armed.IsSyntheticFist)
                return true;

            if (weapon == null)
                return true;

            // PhysicalInit / Unarmed style.
            if ((weapon.GetWeaponFlags() & WeaponFlags.Unarmed) != 0)
                return true;

            // Synthetic MA fist from ArmMartialArtsFist uses Create() → InstanceId 0.
            return weapon.InstanceId <= 0;
        }

        public static bool IsUnarmedPresentation(Item? weapon)
            => IsUnarmedPresentation(armed: null, weapon);

        public static bool UsesMeleeAmmo(Item? weapon)
        {
            if (weapon == null)
                return true;

            WeaponFlags flags = weapon.GetWeaponFlags();
            if ((flags & (WeaponFlags.Melee | WeaponFlags.Unarmed)) != 0)
                return true;

            return weapon.GetStat(CharacterStat.AmmoType) == MeleeStyleAmmoType;
        }

        /// <summary>
        /// True when observers should receive WeaponItemFullUpdate for this hand item.
        /// Fists/unarmed and NPC tag-driven natural weapons stay AttackInfo/SAW-only.
        /// </summary>
        public static bool ShouldAnnounceWeaponItemFullUpdate(Item? item, CharacterWeapon? armed = null)
        {
            if (item == null || item.InstanceId <= 0 || !item.IsWieldableCombatWeapon())
                return false;

            // Tag-backed NPC weapons use SAW + AttackInfo instance=tag, not WIFU.
            if (armed != null && armed.SawTag != 0)
                return false;

            // Synthetic MA fists / unarmed stay AttackInfo-only.
            if ((armed != null && armed.IsSyntheticFist)
                || item.IsMaCombinedWeapon()
                || IsUnarmedPresentation(armed, item))
                return false;

            return true;
        }
    }
}
