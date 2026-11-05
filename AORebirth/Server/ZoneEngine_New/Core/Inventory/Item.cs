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
    using ZoneEngine_New.Core.Playfield.Locality;

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
        /// Assign a fresh allocation to an unowned item, including its wire occupancy
        /// identity. Allocation is not persistence; the existing transaction owns that.
        /// </summary>
        public void AssignInstanceId(int instanceId)
        {
            if (instanceId <= 0) throw new ArgumentOutOfRangeException(nameof(instanceId));
            if (InstanceId != 0 || IsPersisted || Identity.Instance != 0)
                throw new InvalidOperationException("Only an unallocated, unpersisted item may receive a fresh identity.");
            IdentityType type = Identity.Type;
            if (type == IdentityType.None)
                type = (IdentityType)(Definition.DynelType != 0 ? Definition.DynelType : Definition.ItemType);
            InstanceId = instanceId;
            // Preserve the existing zero-type representation; do not synthesize a
            // dynel type for ordinary catalog items that carry no world identity.
            if (type != IdentityType.None)
                Identity = new Identity { Type = type, Instance = instanceId };
            ApplyContainerIdentityIfBag();
        }

        public int ResolvedItemType => Identity.Type != IdentityType.None ? (int)Identity.Type : Definition.ItemType;

        public int LowId { get; init; }

        public int HighId { get; init; }

        public int Quality { get; init; }

        public int StackCount { get; set; } = 1;

        public ItemSource Source { get; init; } = ItemSource.Other;

        /// <summary>
        /// True while this instance is mid-move (delayed equip/unequip) or mid-use (delayed use). Locked items cannot be
        /// removed or relocated by other packets (trade, delete, a second inventory move).
        /// </summary>
        public bool Locked { get; set; }

        /// <summary>
        /// Over-equipped level 0-4 (25% penalty per level) while worn, set by the wearer's rebase
        /// (<see cref="OverEquip"/>). Runtime only.
        /// </summary>
        public int OverEquipLevel { get; internal set; }

        /// <summary>
        /// Rounds in this weapon's clip (<see cref="WeaponAmmo"/>); null means a full clip. Runtime only: a relog starts
        /// with a full clip.
        /// </summary>
        public int? ClipRounds { get; internal set; }

        /// <summary>
        /// The WeaponInstance identity this wielded weapon is announced under (WeaponItemFullUpdate). Kept for as long as
        /// the weapon stays on <see cref="WeaponInstancePlayfield"/>: retail keeps one per weapon (C74A:25ED9085 across
        /// two captures), and the owner's client knows the weapon by it, so a Reload names it. Runtime only.
        /// </summary>
        public Identity? WeaponInstanceIdentity { get; internal set; }

        /// <summary>The playfield instance <see cref="WeaponInstanceIdentity"/> was allocated on.</summary>
        public int WeaponInstancePlayfield { get; internal set; }

        /// <summary>
        /// Worn but disabled: the wearer does not meet its PlayShiftRequirements in this playfield
        /// (<see cref="PlayshiftItems"/>). No effects are active and the client gets <see cref="AORebirth.Enums.ItemFlags.Disabled"/>
        /// in the slot flags. Runtime only; cleared when the item is equipped again.
        /// </summary>
        public bool PlayshiftSuppressed { get; internal set; }

        public ItemTemplate Definition { get; init; } = null!;

        public string Name => Definition.Name;

        public int Flags => Definition.Flags;

        public Dictionary<EventType, List<ItemSpell>> SpellList => Definition.SpellList;

        /// <summary>Spells this item runs while worn.</summary>
        public IReadOnlyList<ItemSpell> WearSpells =>
            SpellList.TryGetValue(EventType.OnWear, out List<ItemSpell>? spells) ? spells : [];

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
            // In the slot field 0x02 means "bound to an instance announced under its own identity" (PlayerInventory
            // adds it for announced hand weapons). The template's ModifiedDescription bit has the same value and must
            // not leak in: the client then runs its temporary-item test on the slot (Gamecode.dll 0x1002ac44 gate,
            // TimeExist check at 0x1004cbd2) and refuses the bag move ("Temporary items can't be moved into a bag!").
            const int announcedInstanceSlotFlag = 0x0002;

            // A worn item its PlayShiftRequirements hold off is shown disabled.
            int disabled = PlayshiftSuppressed ? (int)AORebirth.Enums.ItemFlags.Disabled : 0;
            int flags = Flags & 0xFFFF & ~announcedInstanceSlotFlag;
            if ((flags & visibilityNibble) == 0)
                return unchecked((short)(baselinePacketFlags | disabled));

            // An owned item always shows in its container. Some templates (Blackmane's Stat Buffer, the Rings of
            // Tattered Flame / Weeping Flesh / Eternal Night) carry no Visible bit, and the client then leaves their
            // slots looking empty even though they are occupied.
            return unchecked((short)(flags | (int)AORebirth.Enums.ItemFlags.Visible | disabled));
        }

        /// <summary>Durable <c>item_instances</c> row for this instance at the given location.</summary>
        public ItemInstanceRecord ToRecord(Identity container, int placement, int count)
            => new()
            {
                InstanceId = InstanceId, ContainerType = (int)container.Type,
                ContainerInstance = container.Instance, ContainerPlacement = placement,
                ItemType = ResolvedItemType,
                LowId = LowId, HighId = HighId, Quality = Quality, StackCount = count, Source = Source
            };

        /// <summary>True when the item's Can stat includes all of <paramref name="flags"/>.</summary>
        public bool Can(CanFlags flags)
            => ((CanFlags)(uint)GetStat(CharacterStat.Can) & flags) == flags;

        /// <summary>
        /// True when using this item spends or destroys it: the Consume flag or an OnUse DestroyItem.
        /// <see cref="DestroyOne"/> cannot remove from wear pages, so such items are not usable while equipped.
        /// </summary>
        public bool DestroysOnUse
            => Can(CanFlags.Consume)
                || (SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells)
                    && spells.Exists(spell => spell.FunctionType == (int)FunctionType.DestroyItem));

        public bool IsWieldableCombatWeapon()
            => (ItemClass)GetStat(CharacterStat.ItemClass) == ItemClass.Weapon;

        public bool IsMaCombinedWeapon()
            => GetStat(CharacterStat.MartialArts) > 0;

        public WeaponFlags GetWeaponFlags()
            => Definition.GetWeaponFlags();

        /// <summary>
        /// Inventory/worn GenericCmd Use entry point. Bag open/reopen/close-toggle, then OnUse spells.
        /// Consumable items spend one charge after those functions succeed.
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

            if (IsBackpackUse)
            {
                if (TryUseBackpack(player, slotIdentity, inventoryRepository, items))
                    return true;
            }

            if (!CanBeginUse(player))
                return false;
            return ExecuteUse(player, slotIdentity, inventoryRepository, items);
        }

        /// <summary>Bag open/close toggle; never delayed.</summary>
        public bool IsBackpackUse
            => Identity.Type == IdentityType.Container && Identity.Instance != 0 && Can(CanFlags.Use);

        /// <summary>Gates checked when a use starts: <see cref="CanStillUse"/> plus the ToUse requirements.</summary>
        public bool CanBeginUse(Player player, Character? target = null)
            => CanStillUse(player)
                && Definition.MeetsActionRequirements(stat => player.Stats.Get(stat), ActionType.ToUse,
                    ItemTemplate.WithActionTarget(player.ResolvePerkRequirement, player, target ?? player),
                    target == null ? null : stat => target.Stats.Get(stat),
                    target == null ? null : ItemTemplate.WithActionTarget(target.ResolveRequirement, player, target));

        /// <summary>
        /// Gates checked again when a delayed use completes. ToUse requirements are not among them: met when the use
        /// started, it fires. The item stays locked in its slot meanwhile (ItemUseService), so it cannot be moved,
        /// traded or used twice; skill locks are enforced when its functions run.
        /// </summary>
        public bool CanStillUse(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            if (player.Session == null || player.Playfield == null || !player.Inventory.IsHydrated)
                return false;
            if (Locked || !Can(CanFlags.Use))
                return false;
            // A function aimed at the fighting target would otherwise fall back to the user.
            return !UsesFightingTarget || player.TryResolveFightingTarget() != null;
        }

        /// <summary>True when an OnUse function applies to the user's fighting target.</summary>
        public bool UsesFightingTarget
            => SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells)
                && spells.Exists(spell => spell.Target == (int)ItemTarget.Fightingtarget);

        /// <summary>
        /// Runs OnUse spells and spends a consumable charge. Callers gate with <see cref="CanBeginUse"/>.
        /// <paramref name="target"/> is who Target functions land on, resolved before queuing by <see cref="UseTargetRules"/>.
        /// </summary>
        public bool ExecuteUse(
            Player player,
            Identity slotIdentity,
            IInventoryRepository inventoryRepository,
            IItemBuilder items,
            Character? target = null)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            ArgumentNullException.ThrowIfNull(items);

            // The user is the source: Fightingtarget functions resolve through it and damage is credited to it.
            if (!Definition.ExecuteOnUseSpells(
                    target ?? player,
                    inventoryRepository,
                    items,
                    source: player,
                    criteria: new SpellCriteria { Subject = this, SubjectSlot = slotIdentity }))
                return false;
            AnnounceUsed(player, slotIdentity, target ?? player);
            if (Can(CanFlags.Consume))
                ConsumeCharge(player, slotIdentity);
            return true;
        }

        /// <summary>
        /// TemplateAction Use after OnUse: official other-target Health Regeneration Stim capture
        /// 2026-09-23T02:59:57.5534400Z has performer Identity, the user's inventory Placement, and recipient
        /// Unknown3/Unknown4. The self-use Health and Nano Stim capture 2026-10-02T15:43:13Z has the same layout.
        /// The client plays the item's use effect (its GfxEffect) from this announcement.
        /// </summary>
        void AnnounceUsed(Player player, Identity slotIdentity, Character target)
        {
            var used = new TemplateActionMessage
            {
                Identity = player.Identity,
                ItemLowId = LowId,
                ItemHighId = HighId,
                Quality = Quality,
                Unknown1 = 1,
                Action = TemplateActionType.Use,
                Placement = slotIdentity,
                Unknown3 = (int)target.Identity.Type,
                Unknown4 = target.Identity.Instance
            };
            player.Playfield?.GetRequiredService<PlayfieldLocality>().Announce(player, used, includeSelf: true);
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

            DestroyOne(player, slotIdentity);
        }

        /// <summary>
        /// Removes one charge from <paramref name="slotIdentity"/>. The last charge leaves inventory,
        /// is retired so a relog cannot bring it back, and the client is told to drop the slot.
        /// </summary>
        internal bool DestroyOne(Player player, Identity slotIdentity)
        {
            ArgumentNullException.ThrowIfNull(player);

            PlayerInventory inventory = player.Inventory;
            Container page;
            int placement;
            if (slotIdentity.Type == IdentityType.Backpack)
            {
                if (!inventory.TryGetCarriedBackpackItem(slotIdentity, out page, out placement, out _))
                    return false;
            }
            else
            {
                placement = slotIdentity.Instance;
                if (!inventory.TryResolvePageByPlacement(placement, out page, out bool isWearPage) || isWearPage)
                    return false;
            }

            if (!page.Content.TryGetValue(placement, out Item? occupant) || !ReferenceEquals(occupant, this))
                return false;

            if (Locked)
                return false;

            if (StackCount > 1)
            {
                StackCount--;
                inventory.MarkDirty(this, page, placement);
            }
            else
            {
                if (page.Remove(placement) == null)
                    return false;

                StackCount = 0;
                inventory.Discard(this, ConsumedGraveyard(player));
                // A bag slot is dropped by the packed Backpack identity the client used for it.
                if (slotIdentity.Type == IdentityType.Backpack)
                    SendDeleteItem(player, slotIdentity.Type, slotIdentity.Instance);
                else
                    SendDeleteItem(player, page.Identity.Type, placement);
            }

            Modules.ModuleHooks.ItemMoved(player, this, ItemPlaces.Of(page),
                Modules.ModuleHooks.Place(AORebirth.Modules.Events.ItemPlaceKind.Destroyed), 1);
            player.Playfield?.GetRequiredService<InventoryFlushService>().NotifyDirty(player);
            return true;
        }

        /// <summary>
        /// Takes <paramref name="count"/> from this stack in the main-inventory slot <paramref name="placement"/> (ammo for
        /// a reload). A stack taken to 0 leaves inventory and is retired; the caller tells the client (a Reload's
        /// RemoveAmmoStack drops the slot), so no DeleteItem is sent here.
        /// </summary>
        internal bool TakeFromStack(Player player, Container page, int placement, int count)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(page);
            if (count <= 0 || count > StackCount || Locked)
                return false;

            PlayerInventory inventory = player.Inventory;
            if (!page.Content.TryGetValue(placement, out Item? occupant) || !ReferenceEquals(occupant, this))
                return false;

            if (count < StackCount)
            {
                StackCount -= count;
                inventory.MarkDirty(this, page, placement);
            }
            else
            {
                if (page.Remove(placement) == null)
                    return false;

                StackCount = 0;
                inventory.Discard(this, ConsumedGraveyard(player));
            }

            Modules.ModuleHooks.ItemMoved(player, this, Modules.ModuleHooks.Place(AORebirth.Modules.Events.ItemPlaceKind.Inventory),
                Modules.ModuleHooks.Place(AORebirth.Modules.Events.ItemPlaceKind.Destroyed), count);
            player.Playfield?.GetRequiredService<InventoryFlushService>().NotifyDirty(player);
            return true;
        }

        /// <summary>
        /// Location a consumed item's row is parked at. No carried, bank, or backpack query selects
        /// <see cref="IdentityType.None"/>, and placement is the instance id, so it cannot collide.
        /// </summary>
        static Identity ConsumedGraveyard(Player player)
            => new() { Type = IdentityType.None, Instance = player.Identity.Instance };

        static void SendDeleteItem(Player player, IdentityType pageType, int placement)
        {
            if (player.Session == null)
                return;

            player.Session.Send(
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
            // A page without a handle has not been introduced to the client on this playfield.
            bool pageKnown = inventory.TryGetBackpackPage(containerIdentity, out Container? page)
                && page!.InventoryHandle != 0;
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

            // The bag's ChestItemFullUpdate names the playfield the client thinks it is in (ClientPlayfieldId): in a proxy
            // interior instance the server's own instance id is unknown to the client, which then drops the bag and no
            // backpack opens there.
            Playfield playfield = player.Playfield!;
            if (page.Content.Count > 0)
            {
                int handle = EnsureHandle(page, inventory, containerIdentity);
                if (handle == 0)
                    return false;
                player.Session!.Send(
                    page.BuildChestItemFullUpdate(player.Identity, playfield.ClientPlayfieldId, slotIdentity));
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
                int introduceHandle = inventory.AllocateClientContainerHandle();
                inventory.RegisterBackpackHandle(introduceHandle, containerIdentity);
                int openHandle = EnsureHandle(page, inventory, containerIdentity);
                if (introduceHandle == 0 || openHandle == 0)
                    return false;
                player.Session!.Send(
                    page.BuildInventoryUpdateMessage(
                        player.Identity,
                        containerIdentity,
                        introduceHandle,
                        unknown1: BackpackInventoryUpdateUnknown1,
                        unknown2: 0));
                player.Session.Send(
                    page.BuildChestItemFullUpdate(player.Identity, playfield.ClientPlayfieldId, slotIdentity));
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
            Identity containerIdentity)
        {
            if (page.InventoryHandle == 0)
            {
                page.InventoryHandle = inventory.AllocateClientContainerHandle();
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

        /// <summary>Confirms a bag closed at the client's request (it was picked up while open).</summary>
        public static void CloseBackpack(Player player, Container page, Identity containerIdentity)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(page);
            if (player.Session != null)
                SendCloseAction(player, containerIdentity);
            page.IsOpen = false;
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
