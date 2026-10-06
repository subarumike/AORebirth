namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;

    public sealed class PlayerInventory
    {
        public const int BackpackCapacity = 21;

        public const int BankCapacity = 104;

        /// <summary>Matches the legacy OverflowInventoryPage geometry (64 slots starting at 0).</summary>
        public const int OverflowCapacity = 0x40;

        private readonly Dictionary<int, Container> _backpackPages = new();
        private readonly Dictionary<int, Identity> _handleToContainer = new();
        private readonly Dictionary<int, DirtyEntry> _dirty = new();
        private readonly object _dirtyGate = new();

        /// <summary>Pending durable write for an item keyed by unique InstanceId.</summary>
        internal readonly struct DirtyEntry
        {
            public DirtyEntry(Item item, int containerType, int containerInstance, int containerPlacement)
            {
                Item = item;
                ContainerType = containerType;
                ContainerInstance = containerInstance;
                ContainerPlacement = containerPlacement;
            }

            public Item Item { get; }

            public int InstanceId => Item.InstanceId;

            public int ContainerType { get; }

            public int ContainerInstance { get; }

            public int ContainerPlacement { get; }
        }

        public Container Inventory { get; private set; } = null!;

        public Container Equipment { get; private set; } = null!;

        public Container Armor { get; private set; } = null!;

        public Container Implant { get; private set; } = null!;

        public Container Social { get; private set; } = null!;

        public Container Bank { get; private set; } = null!;

        /// <summary>
        /// Landing page for grants that do not fit in <see cref="Inventory"/>. Remove-only and never
        /// persisted: <see cref="MarkDirty"/> ignores it and <see cref="TryPlace"/> refuses to move an
        /// already-durable item here, so a crash can never leave a DB row pointing at overflow.
        /// Contents are lost on logout, which is the intended in-memory-only behaviour.
        /// </summary>
        public Container Overflow { get; private set; } = null!;

        public bool IsHydrated =>
            Inventory != null
            && Equipment != null
            && Armor != null
            && Implant != null
            && Social != null
            && Bank != null
            && Overflow != null;

        public void Apply(CharacterHydrationResult hydration, int characterId, IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(hydration);
            ArgumentNullException.ThrowIfNull(items);

            CreatePages(characterId);
            _backpackPages.Clear();
            _handleToContainer.Clear();
            lock (_dirtyGate)
                _dirty.Clear();

            foreach (ItemInstanceRecord row in hydration.Items)
            {
                if (!items.TryFromInstanceRecord(row, out Item item))
                    continue;

                // ContainerType = page IdentityType; ContainerInstance = characterId
                // Bank is intentionally not loaded at login — OpenBank hydrates it.
                switch ((IdentityType)row.ContainerType)
                {
                    case IdentityType.Inventory:
                        Inventory.Add(row.ContainerPlacement, item);
                        break;
                    case IdentityType.WeaponPage:
                        Equipment.Add(row.ContainerPlacement, item);
                        break;
                    case IdentityType.ArmorPage:
                        Armor.Add(row.ContainerPlacement, item);
                        break;
                    case IdentityType.ImplantPage:
                        Implant.Add(row.ContainerPlacement, item);
                        break;
                    case IdentityType.SocialPage:
                        Social.Add(row.ContainerPlacement, item);
                        break;
                }
            }
        }

        public bool TryGetItem(IdentityType pageType, int placement, out Item item)
        {
            item = null!;
            Container? page = pageType switch
            {
                IdentityType.Inventory => Inventory,
                IdentityType.WeaponPage => Equipment,
                IdentityType.ArmorPage => Armor,
                IdentityType.ImplantPage => Implant,
                IdentityType.SocialPage => Social,
                IdentityType.OverflowWindow => Overflow,
                IdentityType.BankByRef => Bank.IsHydrated ? Bank : null,
                _ => null
            };

            if (page == null)
                return false;

            return page.Content.TryGetValue(placement, out item!);
        }

        /// <summary>
        /// Item Use slot: a carried or wear page slot, or a slot in a bag the player carries (Backpack identity,
        /// instance = bag handle << 16 | slot). The bag must be hydrated, removable from and not locked
        /// (mid-move/trade); bags in the bank or on the ground are not usable.
        /// </summary>
        public bool TryGetUseItem(Identity slot, out Item item)
        {
            if (slot.Type != IdentityType.Backpack)
                return TryGetItem(slot.Type, slot.Instance, out item);

            return TryGetCarriedBackpackItem(slot, out _, out _, out item);
        }

        /// <summary>The page, placement and item a carried bag's packed Backpack slot names.</summary>
        public bool TryGetCarriedBackpackItem(Identity slot, out Container page, out int placement, out Item item)
        {
            item = null!;
            placement = (int)((uint)slot.Instance & 0xffff);
            int handle = (int)(((uint)slot.Instance >> 16) & 0xffff);
            if (slot.Type != IdentityType.Backpack || !IsHydrated || handle == 0
                || !TryGetOwnedBackpackPageByHandle(handle, out page))
            {
                page = null!;
                return false;
            }

            return page.IsHydrated && page.LinkedItem is { Locked: false }
                && (page.Flags & ContainerFlags.CanRemove) != 0
                && placement >= page.Offset && placement < page.Offset + page.Capacity
                && page.Content.TryGetValue(placement, out item!);
        }

        /// <summary>
        /// Resolves a carried/wear page from placement range. Ignores client IdentityType.
        /// </summary>
        public bool TryResolvePageByPlacement(int placement, out Container page, out bool isWearPage)
        {
            page = null!;
            isWearPage = false;

            if (!IsHydrated)
                return false;

            if (placement >= Equipment.Offset && placement < Equipment.Offset + Equipment.Capacity)
            {
                page = Equipment;
                isWearPage = true;
                return true;
            }

            if (placement >= Armor.Offset && placement < Armor.Offset + Armor.Capacity)
            {
                page = Armor;
                isWearPage = true;
                return true;
            }

            if (placement >= Implant.Offset && placement < Implant.Offset + Implant.Capacity)
            {
                page = Implant;
                isWearPage = true;
                return true;
            }

            if (placement >= Social.Offset && placement < Social.Offset + Social.Capacity)
            {
                page = Social;
                isWearPage = true;
                return true;
            }

            if (placement >= Inventory.Offset && placement < Inventory.Offset + Inventory.Capacity)
            {
                page = Inventory;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Single funnel for handing an item to a player: main inventory first, overflow when it is full.
        /// Returns false when the item cannot be placed anywhere, which callers must treat as
        /// "the grant did not happen" — no packet, no state change.
        /// </summary>
        /// <remarks>
        /// A persisted item is never allowed into overflow. Overflow is not written to the database,
        /// so parking a durable row there would leave the row at its previous location and duplicate
        /// the item on the next restart. Callers moving durable items (player-to-player trades) must
        /// check <see cref="HasFreeInventorySlots"/> up front and fail the whole operation instead.
        /// </remarks>
        public bool TryPlace(Item item, out Container page, out int slot)
        {
            ArgumentNullException.ThrowIfNull(item);

            page = null!;
            slot = -1;
            if (!IsHydrated)
                return false;

            slot = Inventory.FindFreeSlot();
            if (slot >= 0)
            {
                page = Inventory;
                return Inventory.Add(slot, item);
            }

            if (item.IsPersisted)
                return false;

            slot = Overflow.FindFreeSlot();
            if (slot < 0)
                return false;

            page = Overflow;
            return Overflow.Add(slot, item);
        }

        /// <summary>True when main inventory has at least <paramref name="count"/> empty slots.</summary>
        public bool HasFreeInventorySlots(int count)
        {
            if (count <= 0)
                return true;

            if (!IsHydrated)
                return false;

            return Inventory.Capacity - Inventory.Content.Count >= count;
        }

        public bool IsBagMarkerTarget(int targetPlacement)
        {
            return targetPlacement == (int)IdentityType.TradeWindow
                || targetPlacement == (int)IdentityType.Inventory
                || targetPlacement == (int)IdentityType.OverflowWindow
                || targetPlacement == 0x6F;
        }

        public bool TryResolveTargetSlot(int targetPlacement, out Container page, out int slot, out bool isWearPage)
        {
            page = null!;
            slot = -1;
            isWearPage = false;

            if (!IsHydrated)
                return false;

            if (IsBagMarkerTarget(targetPlacement))
            {
                page = Inventory;
                slot = Inventory.FindFreeSlot();
                return slot >= 0;
            }

            if (!TryResolvePageByPlacement(targetPlacement, out page, out isWearPage))
                return false;

            slot = targetPlacement;
            return true;
        }

        /// <summary>
        /// The item may not change hands or move: it is locked, or it is a bag holding a locked item (an item use
        /// waiting on its AttackDelay). Moving such a bag would carry the pending use away with it.
        /// </summary>
        public bool IsLockedForTransfer(Item item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item.Locked)
                return true;
            if (!InventoryMoveService.IsBagItem(item) || !TryGetBackpackPage(item.Identity, out Container page))
                return false;

            foreach (Item content in page.Content.Values)
            {
                if (content.Locked)
                    return true;
            }

            return false;
        }

        public bool TryGetBackpackPage(Identity containerIdentity, out Container page)
        {
            page = null!;
            if (containerIdentity.Type != IdentityType.Container || containerIdentity.Instance == 0)
                return false;

            return _backpackPages.TryGetValue(containerIdentity.Instance, out page!);
        }

        public bool TryGetOwnedBackpackPage(Identity containerIdentity, out Container page)
        {
            page = null!;
            if (!TryGetBackpackPage(containerIdentity, out page))
                return false;

            Item? bag = page.LinkedItem;
            if (bag == null)
                return false;

            return ContainsCarriedItem(bag);
        }

        /// <summary>
        /// Every item the character is holding: carried pages, overflow, and the interior of any bag
        /// whose page has been hydrated. Bank is excluded — unique rules only cover carried items.
        /// </summary>
        public IEnumerable<Item> EnumerateHeldItems()
        {
            if (!IsHydrated)
                yield break;

            Container[] pages = [Inventory, Equipment, Armor, Implant, Social, Overflow];
            foreach (Container page in pages)
            {
                foreach (Item item in page.Content.Values)
                    yield return item;
            }

            foreach (Container page in _backpackPages.Values)
            {
                Item? bag = page.LinkedItem;
                if (bag == null || !ContainsCarriedItem(bag))
                    continue;

                foreach (Item item in page.Content.Values)
                    yield return item;
            }
        }

        public bool ContainsCarriedItem(Item item)
        {
            ArgumentNullException.ThrowIfNull(item);
            return ContainsItem(Inventory, item)
                || ContainsItem(Equipment, item)
                || ContainsItem(Armor, item)
                || ContainsItem(Implant, item)
                || ContainsItem(Social, item);
        }

        static bool ContainsItem(Container page, Item item)
        {
            foreach (Item occupant in page.Content.Values)
            {
                if (ReferenceEquals(occupant, item))
                    return true;
            }

            return false;
        }

        public bool TryGetLinkedItem(Identity containerIdentity, out Item item)
        {
            item = null!;
            if (!TryGetBackpackPage(containerIdentity, out Container page) || page.LinkedItem == null)
                return false;

            item = page.LinkedItem;
            return true;
        }

        public Container GetOrCreateBackpackPage(Item bagItem, Identity containerIdentity, Identity parentSlot)
        {
            ArgumentNullException.ThrowIfNull(bagItem);

            if (_backpackPages.TryGetValue(containerIdentity.Instance, out Container? existing))
            {
                existing.LinkedItem = bagItem;
                existing.ParentSlot = parentSlot;
                return existing;
            }

            var page = new Container(IdentityType.Container, offset: 0, capacity: BackpackCapacity, instanceId: containerIdentity.Instance)
            {
                Flags = ContainerFlags.Backpack | ContainerFlags.CanAdd | ContainerFlags.CanRemove,
                LinkedItem = bagItem,
                ParentSlot = parentSlot,
                IsHydrated = false
            };

            _backpackPages[containerIdentity.Instance] = page;
            return page;
        }

        public void HydrateBackpack(Container page, IInventoryRepository repository, IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(page);
            ArgumentNullException.ThrowIfNull(repository);
            ArgumentNullException.ThrowIfNull(items);

            if (page.IsHydrated)
                return;

            page.Content.Clear();
            int containerInstance = page.Identity.Instance;
            if (containerInstance <= 0 && page.LinkedItem != null)
                containerInstance = page.LinkedItem.InstanceId;

            if (containerInstance > 0)
            {
                foreach (ItemInstanceRecord row in repository.GetContainerItems(containerInstance))
                {
                    if (!items.TryFromInstanceRecord(row, out Item content))
                        continue;

                    page.Add(row.ContainerPlacement, content);
                }
            }

            page.IsHydrated = true;
        }

        /// <summary>Lazy-load bank contents (OpenBank). Safe to call when already hydrated.</summary>
        public void EnsureBankHydrated(int characterId, IInventoryRepository repository, IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(repository);
            ArgumentNullException.ThrowIfNull(items);

            if (!IsHydrated)
                CreatePages(characterId);

            if (Bank.IsHydrated)
                return;

            Bank.Content.Clear();
            foreach (ItemInstanceRecord row in repository.GetBankItems(characterId))
            {
                if (!items.TryFromInstanceRecord(row, out Item content))
                    continue;

                Bank.Add(row.ContainerPlacement, content);
            }

            Bank.IsHydrated = true;
        }

        public BankMessage BuildBankMessage(Identity owner)
        {
            BankSlot[] slots = Bank.Content
                .OrderBy(static pair => pair.Key)
                .Select(pair =>
                {
                    Item item = pair.Value;
                    return new BankSlot
                    {
                        Placement = pair.Key,
                        Flags = item.ToInventoryPacketFlags(),
                        Count = InventoryStacking.WireCount(item.StackCount),
                        Identity = item.Identity.Instance != 0
                            ? item.Identity
                            : new Identity { Type = IdentityType.BankByRef, Instance = pair.Key },
                        ItemLowId = item.LowId,
                        ItemHighId = item.HighId,
                        Quality = item.Quality,
                        Unknown = 0
                    };
                })
                .ToArray();

            return new BankMessage
            {
                Identity = owner,
                BankSlots = slots,
                Unknown1 = 0,
                Unknown2 = Identity.None
            };
        }

        /// <summary>
        /// The client drops every bag window and bag inventory when it changes playfield, and handles
        /// belong to the playfield that allocated them. Server-side contents stay; the next use of
        /// each bag introduces it again on the new playfield.
        /// </summary>
        public void ResetBackpackClientState()
        {
            foreach (Container page in _backpackPages.Values)
            {
                page.IsOpen = false;
                page.InventoryHandle = 0;
            }

            _handleToContainer.Clear();
        }

        public void RegisterBackpackHandle(int handle, Identity containerIdentity)
        {
            if (handle <= 0 || containerIdentity.Type != IdentityType.Container || containerIdentity.Instance == 0)
                return;

            _handleToContainer[handle] = containerIdentity;
        }

        public bool TryGetContainerByHandle(int handle, out Identity containerIdentity)
        {
            return _handleToContainer.TryGetValue(handle, out containerIdentity);
        }

        public bool TryGetBackpackPageByHandle(int handle, out Container page)
        {
            page = null!;
            if (handle <= 0)
                return false;

            if (TryGetContainerByHandle(handle, out Identity containerIdentity)
                && TryGetBackpackPage(containerIdentity, out page))
                return true;

            foreach (Container candidate in _backpackPages.Values)
            {
                if (candidate.InventoryHandle != handle)
                    continue;

                page = candidate;
                return true;
            }

            return false;
        }

        public bool TryGetOwnedBackpackPageByHandle(int handle, out Container page)
        {
            page = null!;
            if (!TryGetBackpackPageByHandle(handle, out page))
                return false;

            Item? bag = page.LinkedItem;
            if (bag == null)
                return false;

            return ContainsCarriedItem(bag);
        }

        public bool TryGetUniqueOwnedBackpackSlot(int slot, out Container page, out Item item)
        {
            page = null!;
            item = null!;
            Container? found = null;

            foreach (Container candidate in _backpackPages.Values)
            {
                Item? bag = candidate.LinkedItem;
                if (bag == null || !ContainsCarriedItem(bag))
                    continue;

                if (!candidate.Content.TryGetValue(slot, out Item? occupant))
                    continue;

                if (found != null)
                    return false;

                found = candidate;
                item = occupant;
            }

            if (found == null)
                return false;

            page = found;
            return true;
        }

        public void MarkDirty(Item item, Container page, int placement)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(page);

            // Dirty set is keyed by unique InstanceId. Skip unkeyed stubs.
            if (item.InstanceId <= 0)
                return;

            // Overflow is in-memory only; writing a row for it would resurrect the item on restart.
            if (page.Identity.Type == IdentityType.OverflowWindow)
                return;

            var entry = new DirtyEntry(
                item,
                (int)page.Identity.Type,
                page.Identity.Instance,
                placement);

            lock (_dirtyGate)
                _dirty[item.InstanceId] = entry;
        }

        /// <summary>
        /// Retires a durable item by re-homing its row under <paramref name="graveyard"/>. The item
        /// instance repository has no delete, and simply forgetting the item in memory would let the
        /// row resurrect it at next login. Placement is the instance id so the location unique index
        /// can never collide.
        /// </summary>
        public void MarkOrphaned(Item item, Identity graveyard)
        {
            ArgumentNullException.ThrowIfNull(item);

            if (item.InstanceId <= 0 || !item.IsPersisted)
                return;

            var entry = new DirtyEntry(
                item,
                (int)graveyard.Type,
                graveyard.Instance,
                item.InstanceId);

            lock (_dirtyGate)
                _dirty[item.InstanceId] = entry;
        }

        /// <summary>
        /// Retires an item that no longer exists anywhere. A persisted item is re-homed under
        /// <paramref name="graveyard"/>; an item that was never written is dropped from the pending
        /// set instead, so no row is ever created for it.
        /// </summary>
        public void Discard(Item item, Identity graveyard)
        {
            ArgumentNullException.ThrowIfNull(item);

            if (item.InstanceId <= 0)
                return;

            if (item.IsPersisted)
            {
                MarkOrphaned(item, graveyard);
                return;
            }

            lock (_dirtyGate)
                _dirty.Remove(item.InstanceId);
        }

        public bool HasDirtyEntries
        {
            get
            {
                lock (_dirtyGate)
                    return _dirty.Count > 0;
            }
        }

        /// <summary>
        /// Hard-flush dirty inventory. Inserts newly looted (unpersisted) rows and updates
        /// existing locations in one repository transaction.
        /// </summary>
        public void FlushDirty(IInventoryRepository repository)
        {
            ArgumentNullException.ThrowIfNull(repository);

            InventoryDirtyFlush? taken = TakeDirty();
            if (taken == null)
                return;

            try
            {
                repository.PersistNewAndUpdateLocations(taken.Inserts, taken.Updates);
                taken.MarkNewlyPersisted();
            }
            catch
            {
                RestoreDirty(taken);
                throw;
            }
        }

        public InventoryDirtyFlush? TakeDirty()
        {
            DirtyEntry[] pending;
            lock (_dirtyGate)
            {
                if (_dirty.Count == 0)
                    return null;

                pending = new DirtyEntry[_dirty.Count];
                _dirty.Values.CopyTo(pending, 0);
                _dirty.Clear();
            }

            var inserts = new List<ItemInstanceRecord>();
            var updates = new List<ItemLocationUpdate>();
            var newlyPersisted = new List<Item>();

            for (int i = 0; i < pending.Length; i++)
            {
                DirtyEntry entry = pending[i];
                Item item = entry.Item;
                if (item.InstanceId <= 0)
                    continue;

                if (!item.IsPersisted)
                {
                    inserts.Add(
                        new ItemInstanceRecord
                        {
                            InstanceId = item.InstanceId,
                            ContainerType = entry.ContainerType,
                            ContainerInstance = entry.ContainerInstance,
                            ContainerPlacement = entry.ContainerPlacement,
                            ItemType = item.ResolvedItemType,
                            LowId = item.LowId,
                            HighId = item.HighId,
                            Quality = item.Quality,
                            StackCount = item.StackCount,
                            Source = item.Source
                        });
                    newlyPersisted.Add(item);
                }
                else
                {
                    updates.Add(
                        new ItemLocationUpdate(
                            item.InstanceId,
                            entry.ContainerType,
                            entry.ContainerInstance,
                            entry.ContainerPlacement,
                            item.StackCount));
                }
            }

            return new InventoryDirtyFlush(pending, inserts, updates, newlyPersisted);
        }

        public void RestoreDirty(InventoryDirtyFlush flush)
        {
            ArgumentNullException.ThrowIfNull(flush);

            lock (_dirtyGate)
            {
                foreach (DirtyEntry entry in flush.Pending)
                {
                    if (!_dirty.ContainsKey(entry.InstanceId))
                        _dirty[entry.InstanceId] = entry;
                }
            }
        }

        public sealed class InventoryDirtyFlush
        {
            internal InventoryDirtyFlush(
                DirtyEntry[] pending,
                List<ItemInstanceRecord> inserts,
                List<ItemLocationUpdate> updates,
                List<Item> newlyPersisted)
            {
                Pending = pending;
                Inserts = inserts;
                Updates = updates;
                NewlyPersisted = newlyPersisted;
            }

            internal DirtyEntry[] Pending { get; }

            public IReadOnlyList<ItemInstanceRecord> Inserts { get; }

            public IReadOnlyList<ItemLocationUpdate> Updates { get; }

            List<Item> NewlyPersisted { get; }

            public void MarkNewlyPersisted()
            {
                for (int i = 0; i < NewlyPersisted.Count; i++)
                    NewlyPersisted[i].IsPersisted = true;
            }
        }

        /// <summary>
        /// Clears character Bonus, then reapplies wear/wield <c>Modify</c> and <c>ScalingModify</c>
        /// from equipped items. Weapons use OnWear+OnWield; armor and implants use OnWear. Social clothing is
        /// cosmetic (meshes and textures only) and adds nothing.
        /// </summary>
        public void ApplyWearBonuses(StatCollection stats)
        {
            ArgumentNullException.ThrowIfNull(stats);
            stats.ClearBonuses(dirty: true);
            if (!IsHydrated)
                return;

            WearBonusApplier.ApplyContainer(Equipment, includeWield: true, stats);
            WearBonusApplier.ApplyContainer(Armor, includeWield: false, stats);
            WearBonusApplier.ApplyContainer(Implant, includeWield: false, stats);
        }

        /// <summary>Clears every worn item's over-equipped level (before the unpenalised bonus pass).</summary>
        public void ResetOverEquip()
        {
            foreach (Container page in WornPages())
                foreach (Item item in page.Content.Values)
                    item.OverEquipLevel = 0;
        }

        /// <summary>
        /// Sets each worn item's over-equipped level from <paramref name="stats"/> (the full, unpenalised
        /// values). True when any item is over-equipped.
        /// </summary>
        public bool ApplyOverEquip(StatCollection stats)
        {
            ArgumentNullException.ThrowIfNull(stats);
            bool any = false;
            foreach (Container page in WornPages())
            {
                foreach (KeyValuePair<int, Item> slot in page.Content)
                {
                    slot.Value.OverEquipLevel = OverEquip.ComputeLevel(slot.Value, page.Identity.Type, slot.Key, stats);
                    any |= slot.Value.OverEquipLevel > 0;
                }
            }

            return any;
        }

        /// <summary>Social page slot (1-15) of the right hand: placement 0x3D (client drop, 2026-10-06).</summary>
        public const int SocialRightHandSlot = 13;

        /// <summary>Social page slot (1-15) of the left hand: placement 0x3F.</summary>
        public const int SocialLeftHandSlot = 15;

        /// <summary>
        /// The weapon-page hand a social hand placement stands for: the social right and left hands are weapon slots
        /// (fitted by the weapon's Righthand / LeftHand Slot bits). False for other social slots.
        /// </summary>
        public static bool TryGetSocialHand(Container socialPage, int slot, out WeaponSlots hand)
        {
            ArgumentNullException.ThrowIfNull(socialPage);
            hand = default;
            if (socialPage.Identity.Type != IdentityType.SocialPage)
                return false;

            int relative = slot - socialPage.Offset + 1;
            if (relative == SocialRightHandSlot)
                hand = WeaponSlots.Righthand;
            else if (relative == SocialLeftHandSlot)
                hand = WeaponSlots.LeftHand;
            else
                return false;
            return true;
        }

        /// <summary>
        /// True when an item of template <paramref name="itemId"/> (low or high id) is equipped: on the weapon page when
        /// <paramref name="wieldedOnly"/>, else on the weapon, armor or implant page. Social clothing never counts.
        /// <paramref name="ignore"/> and <paramref name="ignoreToo"/> are items being moved, which do not count where
        /// they stand (moving a one-only item between hands, or swapping, must not be blocked by the item itself).
        /// </summary>
        public bool IsEquipped(int itemId, bool wieldedOnly, Item? ignore = null, Item? ignoreToo = null)
        {
            if (!IsHydrated)
                return false;

            foreach (Container page in WornPages())
            {
                if (wieldedOnly && !ReferenceEquals(page, Equipment))
                    continue;
                foreach (Item item in page.Content.Values)
                {
                    if (ReferenceEquals(item, ignore) || ReferenceEquals(item, ignoreToo))
                        continue;
                    if (item.LowId == itemId || item.HighId == itemId)
                        return true;
                }
            }

            return false;
        }

        /// <summary>Pages whose items count as equipped (stats, over-equipping, worn checks); social clothing is cosmetic.</summary>
        IEnumerable<Container> WornPages()
        {
            if (!IsHydrated)
                yield break;

            yield return Equipment;
            yield return Armor;
            yield return Implant;
        }

        public IEnumerable<InventorySlot> BuildInventorySlots()
        {
            foreach (InventorySlot slot in BuildPageSlots(IdentityType.Inventory, Inventory))
                yield return slot;
            foreach (InventorySlot slot in BuildPageSlots(IdentityType.WeaponPage, Equipment))
                yield return slot;
            foreach (InventorySlot slot in BuildPageSlots(IdentityType.ArmorPage, Armor))
                yield return slot;
            foreach (InventorySlot slot in BuildPageSlots(IdentityType.ImplantPage, Implant))
                yield return slot;
            foreach (InventorySlot slot in BuildPageSlots(IdentityType.SocialPage, Social))
                yield return slot;
        }

        private void CreatePages(int characterId)
        {
            Inventory = new Container(IdentityType.Inventory, 0x40, 30, characterId)
            {
                Flags = ContainerFlags.Inventory | ContainerFlags.CanAdd | ContainerFlags.CanRemove
            };
            Equipment = new Container(IdentityType.WeaponPage, 0x01, 15, characterId)
            {
                Flags = ContainerFlags.CanAdd | ContainerFlags.CanRemove
            };
            Armor = new Container(IdentityType.ArmorPage, 0x11, 15, characterId)
            {
                Flags = ContainerFlags.CanAdd | ContainerFlags.CanRemove
            };
            Implant = new Container(IdentityType.ImplantPage, 0x21, 15, characterId)
            {
                Flags = ContainerFlags.CanAdd | ContainerFlags.CanRemove
            };
            Social = new Container(IdentityType.SocialPage, 0x31, 15, characterId)
            {
                Flags = ContainerFlags.CanAdd | ContainerFlags.CanRemove
            };
            Bank = new Container(IdentityType.BankByRef, offset: 0, capacity: BankCapacity, instanceId: characterId)
            {
                Flags = ContainerFlags.Bank | ContainerFlags.CanAdd | ContainerFlags.CanRemove,
                IsHydrated = false
            };
            // No CanAdd: the client may only drag items out of overflow, never into it.
            Overflow = new Container(
                IdentityType.OverflowWindow,
                offset: 0,
                capacity: OverflowCapacity,
                instanceId: characterId)
            {
                Flags = ContainerFlags.CanRemove
            };
        }

        static IEnumerable<InventorySlot> BuildPageSlots(IdentityType pageType, Container page)
        {
            foreach (KeyValuePair<int, Item> slotEntry in page.Content)
            {
                Item item = slotEntry.Value;
                yield return new InventorySlot
                {
                    Placement = slotEntry.Key,
                    Flags = item.ToInventoryPacketFlags(),
                    Count = InventoryStacking.WireCount(item.StackCount),
                    Identity = item.Identity.Instance != 0
                        ? item.Identity
                        : new Identity { Type = pageType, Instance = slotEntry.Key },
                    ItemLowId = item.LowId,
                    ItemHighId = item.HighId,
                    Quality = item.Quality,
                    Unknown = 0
                };
            }
        }
    }
}
