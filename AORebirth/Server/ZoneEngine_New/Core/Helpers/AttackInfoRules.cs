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
        /// <summary>Player equipped melee / player unarmed ammo (0xFFFFFFFF).</summary>
        public const int PlayerMeleeAmmoCount = -1;

        /// <summary>NPC natural / tag-backed melee ammo used by live private-server swing rows.</summary>
        public const int NaturalMeleeAmmoCount = 0;

        /// <summary>Legacy/private-server equipped ranged placeholder ammo.</summary>
        public const int RangedAmmoCount = 40;

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

        public static int ResolveAmmoCount(CharacterWeapon? armed, Item? weapon, bool attackerIsPlayer)
        {
            // NPC template / natural melee uses ammo 0.
            if (!attackerIsPlayer || (armed != null && armed.WireSlot >= 0))
                return NaturalMeleeAmmoCount;

            // Player unarmed and player melee both use -1 (private-server / AOEmu melee rows).
            if (IsUnarmedPresentation(armed, weapon) || UsesMeleeAmmo(weapon))
                return PlayerMeleeAmmoCount;

            return RangedAmmoCount;
        }

        public static int ResolveAmmoCount(Item? weapon)
            => ResolveAmmoCount(armed: null, weapon, attackerIsPlayer: true);

        public static int ResolveWeaponSlot(
            CharacterWeapon? armed,
            WeaponSlot logicalSlot,
            Item? weapon,
            bool attackerIsPlayer = true)
        {
            if (armed != null && armed.WireSlot >= 0)
                return armed.WireSlot;

            // Player synthetic fist / PhysicalInit and NPC MA-fist fallback: slot 0.
            if (!attackerIsPlayer || IsUnarmedPresentation(armed, weapon))
                return 0;

            return logicalSlot switch
            {
                WeaponSlot.OffHand => (int)WeaponSlots.LeftHand,
                _ => (int)WeaponSlots.Righthand
            };
        }

        public static int ResolveWeaponSlot(WeaponSlot slot, Item? weapon)
            => ResolveWeaponSlot(armed: null, slot, weapon, attackerIsPlayer: true);

        public static int ResolveWeaponInstance(CharacterWeapon? armed, Item? weapon, bool attackerIsPlayer)
        {
            // NPC natural weapons: AttackInfo Unknown6 must equal SAW SpecialAttack.Unknown3.
            if (armed != null && armed.SawTag != 0)
                return armed.SawTag;

            // Private-server player unarmed rows use instance 0 (not AOEmu's 100).
            _ = weapon;
            _ = attackerIsPlayer;
            return 0;
        }

        public static int ResolveWeaponInstance(Item? weapon, bool attackerIsPlayer)
            => ResolveWeaponInstance(armed: null, weapon, attackerIsPlayer);

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
