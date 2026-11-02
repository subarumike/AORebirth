namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine_New.Core.Configuration;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;

    /// <summary>
    /// A player's ranged weapon clips, ammo and reloading, as retail plays them (capture 2026-10-23: a 50-round clip
    /// counting down in AttackInfo, a Burst taking 3, a Reload in the same tick the clip hit 0, a manual top-up from 48).
    /// <list type="bullet">
    ///   <item>A weapon keeps a clip when it has a clip size (MaxEnergy, 212) and an ammo type (AmmoType, 420, not the
    ///     melee-style 10) and lacks the NoAmmo Can flag. Its rounds live on the item (<see cref="Item.ClipRounds"/>).</item>
    ///   <item>Every shot spends a round, hit or miss; AttackInfo and SpecialAttackInfo carry the rounds left, and the
    ///     client sets the weapon's Energy (26) from them (Gamecode.dll 1006ac19).</item>
    ///   <item>A clip that runs dry reloads at once from the first main-inventory stack of the same AmmoType: the client
    ///     never asks for that reload itself. The reload button (CharacterAction 210) and dropping ammo on the weapon
    ///     (GenericCmd UseItemOnItem) top a clip up.</item>
    ///   <item>Ammo matches on AmmoType, with ItemClass (76) 0 (client 1006940f). A reload fills the clip from the stack;
    ///     a stack that runs out leaves inventory.</item>
    ///   <item>The weapon then pauses for its RechargeDelay (210), the client's own reload timer, and fires nothing,
    ///     specials included, until it ends.</item>
    ///   <item>Gameplay.json <c>requireAmmo</c> off: reloads happen just the same but fill the clip without ammo.</item>
    /// </list>
    /// </summary>
    public static class WeaponAmmo
    {
        /// <summary>AmmoType that marks a weapon as melee (AttackInfoRules).</summary>
        const int MeleeStyleAmmoType = 10;

        public enum ReloadResult
        {
            Reloaded,

            /// <summary>Not a weapon that keeps a clip.</summary>
            NoClip,

            AlreadyFull,

            AlreadyReloading,

            /// <summary>No stack of this weapon's ammo in the main inventory.</summary>
            NoAmmo,

            /// <summary>The offered item is not this weapon's ammo.</summary>
            AmmoMismatch,
        }

        public static bool UsesAmmo(Item? weapon)
            => weapon != null
               && !weapon.Can(CanFlags.NoAmmo)
               && ClipSize(weapon) > 0
               && AmmoType(weapon) is > 0 and not MeleeStyleAmmoType;

        public static int ClipSize(Item weapon) => Math.Max(0, StatCollection.Normalize(weapon.GetStat(CharacterStat.MaxEnergy)));

        /// <summary>Rounds in the clip: a weapon never fired or reloaded holds a full clip.</summary>
        public static int Rounds(Item weapon) => Math.Clamp(weapon.ClipRounds ?? ClipSize(weapon), 0, ClipSize(weapon));

        static int AmmoType(Item item) => StatCollection.Normalize(item.GetStat(CharacterStat.AmmoType));

        /// <summary>
        /// True when <paramref name="weapon"/> can fire now: it keeps no clip, or it is not reloading and has a round. An
        /// empty clip with ammo to hand starts a reload instead (and the shot waits for it).
        /// </summary>
        public static bool CanFire(Player player, CharacterWeapon weapon)
        {
            if (weapon.Item is not Item item || !UsesAmmo(item))
                return true;
            if (weapon.IsReloading)
                return false;
            if (Rounds(item) > 0)
                return true;

            TryReload(player, weapon, ammoSlot: null);
            return false;
        }

        /// <summary>
        /// Spends <paramref name="count"/> rounds (at most what the clip holds) and returns the rounds left, or null when
        /// the weapon keeps no clip. A clip spent to 0 is reloaded by <see cref="ReloadIfEmptied"/> once the shot is shown.
        /// </summary>
        public static int? Spend(CharacterWeapon weapon, int count)
        {
            if (weapon.Item is not Item item || !UsesAmmo(item))
                return null;

            int left = Math.Max(0, Rounds(item) - Math.Max(0, count));
            item.ClipRounds = left;
            if (left == 0)
                weapon.ReloadPending = true;
            LogUtil.Debug(DebugInfoDetail.Engine, string.Format(CultureInfo.InvariantCulture,
                "Ammo spend weapon={0} rounds={1} left={2}/{3}", item.LowId, count, left, ClipSize(item)));
            return left;
        }

        /// <summary>Reloads a clip the last shot emptied, after that shot's AttackInfo, as retail orders them.</summary>
        public static void ReloadIfEmptied(Player player, CharacterWeapon weapon)
        {
            if (!weapon.ReloadPending)
                return;

            weapon.ReloadPending = false;
            ReloadOrSayNoAmmo(player, weapon);
        }

        /// <summary>
        /// A reload the player asked for, or the one an emptying shot starts, told to the player as retail does, once per
        /// attempt (captures 2026-10-23, one per press): "No ammo was found for &lt;weapon&gt;!" or "&lt;weapon&gt; is
        /// already fully loaded."
        /// </summary>
        static void ReloadOrSayNoAmmo(Player player, CharacterWeapon weapon)
        {
            ReloadResult result = TryReload(player, weapon, ammoSlot: null);
            if (weapon.Item is not Item item)
                return;

            if (result == ReloadResult.NoAmmo)
                ClientFeedback.SendFormatted(player, ClientFeedback.NoAmmoFoundFor, item.Name ?? string.Empty);
            else if (result == ReloadResult.AlreadyFull)
                ClientFeedback.SendFormatted(player, ClientFeedback.AlreadyFullyLoaded, item.Name ?? string.Empty);
        }

        /// <summary>
        /// Tops up <paramref name="weapon"/>'s clip from the stack at <paramref name="ammoSlot"/> (a main-inventory slot or a
        /// carried bag's packed Backpack slot), or from the first main-inventory stack of its ammo when null, then starts
        /// the reload pause and sends Reload.
        /// </summary>
        public static ReloadResult TryReload(Player player, CharacterWeapon weapon, Identity? ammoSlot)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(weapon);

            ReloadResult result = Reload(player, weapon, ammoSlot);
            LogUtil.Debug(DebugInfoDetail.Engine, string.Format(CultureInfo.InvariantCulture,
                "Ammo reload character={0} weapon={1} result={2} clip={3}", player.Identity.Instance,
                weapon.Item?.LowId ?? 0, result, weapon.Item is Item armed ? Rounds(armed) : 0));
            return result;
        }

        static ReloadResult Reload(Player player, CharacterWeapon weapon, Identity? ammoSlot)
        {
            if (weapon.Item is not Item item || !UsesAmmo(item) || !player.Inventory.IsHydrated)
                return ReloadResult.NoClip;
            if (weapon.IsReloading)
                return ReloadResult.AlreadyReloading;

            int clip = ClipSize(item);
            int rounds = Rounds(item);
            if (rounds >= clip)
                return ReloadResult.AlreadyFull;

            Container page = player.Inventory.Inventory;
            int placement = 0;
            Identity usedSlot = Identity.None;
            Item? ammo = null;
            if (ammoSlot is Identity offered)
            {
                bool found;
                if (offered.Type == IdentityType.Backpack)
                    found = player.Inventory.TryGetCarriedBackpackItem(offered, out page, out placement, out ammo);
                else
                {
                    placement = offered.Instance;
                    found = offered.Type == IdentityType.Inventory && page.Content.TryGetValue(placement, out ammo);
                }

                if (!found || ammo == null || !IsAmmoFor(ammo, item))
                    return ReloadResult.AmmoMismatch;
                usedSlot = offered;
            }
            else
            {
                KeyValuePair<int, Item> found = page.Content
                    .Where(slot => IsAmmoFor(slot.Value, item))
                    .OrderBy(slot => slot.Key)
                    .FirstOrDefault();
                if (found.Value != null)
                {
                    placement = found.Key;
                    ammo = found.Value;
                    usedSlot = new Identity { Type = IdentityType.Inventory, Instance = placement };
                }
            }

            // Gameplay.json requireAmmo off: the clip fills without spending ammo; a stack in hand is only shown.
            bool requireAmmo = player.Playfield?.GetService<GameplaySettings>()?.RequireAmmo ?? true;
            int take = clip - rounds;
            if (requireAmmo)
            {
                if (ammo == null)
                    return ReloadResult.NoAmmo;
                take = Math.Min(take, ammo.StackCount);
                if (!ammo.TakeFromStack(player, page, placement, take))
                    return ReloadResult.NoAmmo;
            }

            item.ClipRounds = rounds + take;
            weapon.ReloadPending = false;
            weapon.ReloadingUntilUtc = DateTime.UtcNow.AddSeconds(ReloadSeconds(item));

            player.Session?.Send(new ReloadMessage
            {
                Identity = player.Identity,
                Unknown = 1,
                // The slot as the client named it: a bag's stack by its packed Backpack identity.
                AmmoSlot = ammo == null ? Identity.None : usedSlot,
                Weapon = item.WeaponInstanceIdentity ?? item.Identity,
                AmmoStackCount = ammo?.StackCount ?? 0,
                ClipCount = rounds + take,
                RemoveAmmoStack = ammo is { StackCount: 0 } ? 1 : 0
            });
            return ReloadResult.Reloaded;
        }

        /// <summary>
        /// The reload button: one weapon per press, the right hand first. The first gun whose clip is not full and finds
        /// ammo reloads. When none does, the player hears about the first gun that had no ammo, or, with every clip full,
        /// that the primary (right-hand, else left-hand) gun is already fully loaded.
        /// </summary>
        public static void ReloadAll(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            Item? primary = null;
            Item? noAmmo = null;
            foreach (WeaponSlot hand in new[] { WeaponSlot.MainHand, WeaponSlot.OffHand })
            {
                if (!player.Weapons.TryGetValue(hand, out CharacterWeapon? weapon) || weapon?.Item is not Item item
                    || !UsesAmmo(item))
                    continue;

                primary ??= item;
                switch (TryReload(player, weapon, ammoSlot: null))
                {
                    case ReloadResult.Reloaded:
                    case ReloadResult.AlreadyReloading:
                        return;
                    case ReloadResult.NoAmmo:
                        noAmmo ??= item;
                        break;
                }
            }

            if (noAmmo != null)
                ClientFeedback.SendFormatted(player, ClientFeedback.NoAmmoFoundFor, noAmmo.Name ?? string.Empty);
            else if (primary != null)
                ClientFeedback.SendFormatted(player, ClientFeedback.AlreadyFullyLoaded, primary.Name ?? string.Empty);
        }

        /// <summary>The armed weapon whose item has <paramref name="identity"/>, for ammo dropped on a weapon.</summary>
        public static bool TryGetArmedWeapon(Player player, Identity identity, out CharacterWeapon weapon)
        {
            foreach (CharacterWeapon armed in player.Weapons.Values)
            {
                if (armed?.Item is Item item && !armed.IsSyntheticFist && item.InstanceId > 0
                    && (item.WeaponInstanceIdentity == identity || item.Identity == identity
                        || item.InstanceId == identity.Instance))
                {
                    weapon = armed;
                    return true;
                }
            }

            weapon = null!;
            return false;
        }

        /// <summary>
        /// Ammo for <paramref name="weapon"/>: the same AmmoType and ItemClass 0. A box carries AmmoType and MaxEnergy (its
        /// rounds) just as a gun does, so ItemClass (weapons are 1) is what tells them apart.
        /// </summary>
        static bool IsAmmoFor(Item candidate, Item weapon)
            => !candidate.Locked
               && candidate.StackCount > 0
               && AmmoType(candidate) == AmmoType(weapon)
               && StatCollection.Normalize(candidate.GetStat(CharacterStat.ItemClass)) == 0;

        /// <summary>The client's reload timer: the weapon's RechargeDelay, in centiseconds.</summary>
        static double ReloadSeconds(Item weapon)
            => Math.Max(0, StatCollection.Normalize(weapon.GetStat(CharacterStat.RechargeDelay))) / 100.0;
    }
}
