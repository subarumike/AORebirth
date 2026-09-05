namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Dumb runtime item: occupancy ids + builder-baked effective <see cref="ItemTemplate"/>.
    /// </summary>
    public sealed class Item
    {
        const int BackpackInventoryUpdateUnknown1 = 3;
        const int OpenActionIdentity = 0x64;
        const int CloseActionIdentity = 0x66;

        /// <summary>
        /// Unique key for this item instance. Allocated in-memory for ephemeral loot; becomes the
        /// <c>item_instances.InstanceId</c> PK on first persist. 0 only for non-authority stubs
        /// (e.g. fist placeholders) that never enter MarkDirty.
        /// </summary>
        public int InstanceId { get; set; }

        /// <summary>
        /// True once a matching <c>item_instances</c> row exists (hydrated from DB or flushed insert).
        /// Ephemeral loot stays false until looted and flushed.
        /// </summary>
        public bool IsPersisted { get; set; }

        public Identity Identity { get; set; }

        public int LowId { get; init; }

        public int HighId { get; init; }

        public int Quality { get; init; }

        public int StackCount { get; set; } = 1;

        public ItemTemplate Definition { get; init; } = null!;

        public string Name => Definition.Name;

        public int Flags => Definition.Flags;

        public Dictionary<EventType, List<ItemSpell>> SpellList => Definition.SpellList;

        public int GetStat(CharacterStat stat)
            => Definition.Stats.TryGetValue(stat, out int value) ? value : 0;

        /// <summary>
        /// 16-bit flags for FullCharacter / InventoryUpdate / Bank slots.
        /// <see cref="Flags"/> comes from items.dat and may be wider than the packet field;
        /// keep the low 16 bits when the live visibility nibble (0xA0) is present, otherwise
        /// use the baseline packet flags the client expects for carried items (0x00A1).
        /// </summary>
        public short ToInventoryPacketFlags()
        {
            const int baselinePacketFlags = 0x00A1;
            const int visibilityNibble = 0x00A0;

            int flags = Flags & 0xFFFF;
            if ((flags & visibilityNibble) == 0)
                return unchecked((short)baselinePacketFlags);

            return unchecked((short)flags);
        }

        /// <summary>True when the item's Can stat includes all of <paramref name="flags"/>.</summary>
        public bool Can(CanFlags flags)
            => ((CanFlags)(uint)GetStat(CharacterStat.Can) & flags) == flags;

        public bool IsWieldableCombatWeapon()
            => (ItemClass)GetStat(CharacterStat.ItemClass) == ItemClass.Weapon;

        public bool IsMaCombinedWeapon()
            => GetStat(CharacterStat.MartialArts) > 0;

        /// <summary>
        /// Inventory/worn GenericCmd Use entry point. Bag open/reopen/close-toggle, then OnUse spells.
        /// </summary>
        public bool Use(
            Player player,
            Identity slotIdentity,
            IInventoryRepository inventoryRepository,
            IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            ArgumentNullException.ThrowIfNull(items);

            if (player.Session == null || player.Playfield == null || !player.Inventory.IsHydrated)
                return false;

            if (Identity.Type == IdentityType.Container && Identity.Instance != 0 && Can(CanFlags.Use))
            {
                if (TryUseBackpack(player, slotIdentity, inventoryRepository, items))
                    return true;
            }

            if (!Can(CanFlags.Use))
                return false;

            return ExecuteOnUse(player, inventoryRepository, items);
        }

        bool TryUseBackpack(
            Player player,
            Identity slotIdentity,
            IInventoryRepository inventoryRepository,
            IItemBuilder items)
        {
            Identity containerIdentity = Identity;
            PlayerInventory inventory = player.Inventory;
            bool pageKnown = inventory.TryGetBackpackPage(containerIdentity, out Container? page);
            if (pageKnown && page!.IsOpen)
            {
                SendCloseAction(player, containerIdentity);
                page.IsOpen = false;
                return true;
            }

            if (pageKnown)
            {
                SendOpenAction(player, containerIdentity);
                page!.IsOpen = true;
                return true;
            }

            page = inventory.GetOrCreateBackpackPage(this, containerIdentity, slotIdentity);
            inventory.HydrateBackpack(page, inventoryRepository, items);

            Playfield playfield = player.Playfield!;
            if (page.Content.Count > 0)
            {
                int handle = EnsureHandle(page, inventory, playfield, containerIdentity);
                player.Session!.Send(
                    page.BuildChestItemFullUpdate(player.Identity, playfield.Identity.Instance, slotIdentity));
                player.Session.Send(
                    page.BuildInventoryUpdateMessage(
                        player.Identity,
                        containerIdentity,
                        handle,
                        unknown1: BackpackInventoryUpdateUnknown1,
                        unknown2: 1));
            }
            else
            {
                int introduceHandle = playfield.AllocateContainerInventoryHandle();
                int openHandle = EnsureHandle(page, inventory, playfield, containerIdentity);
                player.Session!.Send(
                    page.BuildInventoryUpdateMessage(
                        player.Identity,
                        containerIdentity,
                        introduceHandle,
                        unknown1: BackpackInventoryUpdateUnknown1,
                        unknown2: 0));
                player.Session.Send(
                    page.BuildChestItemFullUpdate(player.Identity, playfield.Identity.Instance, slotIdentity));
                player.Session.Send(
                    page.BuildInventoryUpdateMessage(
                        player.Identity,
                        containerIdentity,
                        openHandle,
                        unknown1: BackpackInventoryUpdateUnknown1,
                        unknown2: 1));
            }

            page.IsOpen = true;
            return true;
        }

        bool ExecuteOnUse(Player player, IInventoryRepository inventoryRepository, IItemBuilder items)
        {
            if (!SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells) || spells.Count == 0)
                return false;

            bool any = false;
            foreach (ItemSpell spell in spells)
            {
                if (ExecuteSpell(player, spell, inventoryRepository, items))
                    any = true;
            }

            return any;
        }

        bool ExecuteSpell(
            Player player,
            ItemSpell spell,
            IInventoryRepository inventoryRepository,
            IItemBuilder items)
        {
            switch ((FunctionType)spell.FunctionType)
            {
                case FunctionType.OpenBank:
                    return OpenBank(player, inventoryRepository, items);

                default:
                    LogUtil.Debug(
                        DebugInfoDetail.Network,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Unhandled OnUse FunctionType={0} item={1}/{2} character={3}",
                            spell.FunctionType,
                            LowId,
                            HighId,
                            player.Identity.Instance));
                    return false;
            }
        }

        static bool OpenBank(Player player, IInventoryRepository inventoryRepository, IItemBuilder items)
        {
            if (player.Session == null)
                return false;

            int characterId = player.Identity.Instance;
            player.Inventory.EnsureBankHydrated(characterId, inventoryRepository, items);
            player.Session.Send(player.Inventory.BuildBankMessage(player.Identity));
            return true;
        }

        /// <summary>
        /// Sets <see cref="Identity"/> to Container when ItemType is Backpack.
        /// Called from <see cref="ItemBuilder"/> on create — not during Use.
        /// </summary>
        public void ApplyContainerIdentityIfBag()
        {
            if (Identity.Type != IdentityType.Backpack)
                return;

            int instance = InstanceId > 0
                ? InstanceId
                : Identity.Instance;
            if (instance <= 0)
                return;

            Identity = new Identity { Type = IdentityType.Container, Instance = instance };
        }

        static int EnsureHandle(
            Container page,
            PlayerInventory inventory,
            Playfield playfield,
            Identity containerIdentity)
        {
            if (page.InventoryHandle == 0)
            {
                page.InventoryHandle = playfield.AllocateContainerInventoryHandle();
                inventory.RegisterBackpackHandle(page.InventoryHandle, containerIdentity);
            }

            return page.InventoryHandle;
        }

        static void SendOpenAction(Player player, Identity containerIdentity)
        {
            player.Session!.Send(
                new ActionMessage
                {
                    Identity = containerIdentity,
                    Unknown = 0,
                    ActionCode = 1,
                    ActionIdentity = OpenActionIdentity,
                    Target = player.Identity
                });
        }

        static void SendCloseAction(Player player, Identity containerIdentity)
        {
            player.Session!.Send(
                new ActionMessage
                {
                    Identity = containerIdentity,
                    Unknown = 1,
                    ActionCode = 1,
                    ActionIdentity = CloseActionIdentity,
                    Target = player.Identity
                });
        }
    }
}
