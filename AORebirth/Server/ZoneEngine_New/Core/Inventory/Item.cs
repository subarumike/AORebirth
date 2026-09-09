namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

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

        /// <summary>
        /// Item type for occupancy identities and the <c>item_instances.ItemType</c> column: the live
        /// <see cref="Identity"/> type when set, otherwise the catalog value.
        /// </summary>
        public int ResolvedItemType
            => Identity.Type != IdentityType.None
                ? (int)Identity.Type
                : Definition.ItemType;

        public int LowId { get; init; }

        public int HighId { get; init; }

        public int Quality { get; init; }

        public int StackCount { get; set; } = 1;

        public ItemSource Source { get; init; } = ItemSource.Other;

        /// <summary>
        /// True while this instance is mid-move (delayed equip/unequip). Locked items cannot be
        /// removed or relocated by other packets (trade, delete, a second inventory move).
        /// </summary>
        public bool Locked { get; set; }

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

        public WeaponFlags GetWeaponFlags()
            => Definition.GetWeaponFlags();

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

            if (Locked)
                return false;

            if (Identity.Type == IdentityType.Container && Identity.Instance != 0 && Can(CanFlags.Use))
            {
                if (TryUseBackpack(player, slotIdentity, inventoryRepository, items))
                    return true;
            }

            if (!Can(CanFlags.Use))
                return false;

            if (!Definition.ExecuteOnUseSpells(player, inventoryRepository, items))
                return false;

            ConsumeCharge(player, slotIdentity);
            return true;
        }

        /// <summary>
        /// Spends one charge of a consumable after its OnUse functions ran. The item is destroyed
        /// once the last charge is gone: the slot is cleared, the row is retired so a relog cannot
        /// bring it back, and the client is told to drop the slot.
        /// </summary>
        internal void ConsumeCharge(Player player, Identity slotIdentity)
        {
            if (!Can(CanFlags.Consume) || InstanceId <= 0)
                return;

            PlayerInventory inventory = player.Inventory;
            int placement = slotIdentity.Instance;
            if (!inventory.TryResolvePageByPlacement(placement, out Container page, out bool isWearPage)
                || isWearPage)
                return;

            if (!page.Content.TryGetValue(placement, out Item? occupant) || !ReferenceEquals(occupant, this))
                return;

            if (StackCount > 1)
            {
                StackCount--;
                inventory.MarkDirty(this, page, placement);
            }
            else
            {
                // A locked item is mid-move; leave the charge alone rather than half-destroy it.
                if (page.Remove(placement) == null)
                    return;

                StackCount = 0;
                inventory.Discard(this, ConsumedGraveyard(player));
                SendDeleteItem(player, page.Identity.Type, placement);
            }

            player.Playfield?.GetRequiredService<InventoryFlushService>().NotifyDirty(player);
        }

        /// <summary>
        /// Location a consumed item's row is parked at. No carried, bank, or backpack query selects
        /// <see cref="IdentityType.None"/>, and placement is the instance id, so it cannot collide.
        /// </summary>
        static Identity ConsumedGraveyard(Player player)
            => new() { Type = IdentityType.None, Instance = player.Identity.Instance };

        static void SendDeleteItem(Player player, IdentityType pageType, int placement)
        {
            player.Session!.Send(
                new CharacterActionMessage
                {
                    Identity = player.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.DeleteItem,
                    Unknown1 = 0,
                    Target = new Identity { Type = pageType, Instance = placement },
                    Parameter1 = 0,
                    Parameter2 = 0,
                    Unknown2 = 0
                });
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
                RegisterParentSlotHandle(inventory, slotIdentity, containerIdentity);
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
                inventory.RegisterBackpackHandle(introduceHandle, containerIdentity);
                RegisterParentSlotHandle(inventory, slotIdentity, containerIdentity);
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

        /// <summary>
        /// Stamps a freshly allocated id onto a newly minted item and derives the occupancy
        /// <see cref="Identity"/> from it. The item stays unpersisted until a flush inserts its row.
        /// </summary>
        public void AssignInstanceId(int instanceId)
        {
            if (instanceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(instanceId));
            if (InstanceId > 0)
                throw new InvalidOperationException("Item already has an InstanceId.");

            int itemType = ResolvedItemType;
            InstanceId = instanceId;
            IsPersisted = false;
            if (itemType != 0)
                Identity = new Identity { Type = (IdentityType)itemType, Instance = instanceId };

            ApplyContainerIdentityIfBag();
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

        static void RegisterParentSlotHandle(
            PlayerInventory inventory,
            Identity slotIdentity,
            Identity containerIdentity)
        {
            if (slotIdentity.Type == IdentityType.Inventory && slotIdentity.Instance > 0)
                inventory.RegisterBackpackHandle(slotIdentity.Instance, containerIdentity);
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
