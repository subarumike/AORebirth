namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;

    public sealed class PlayerInventory
    {
        public const int BackpackCapacity = 21;

        public const int BankCapacity = 104;

        private readonly Dictionary<int, Container> _backpackPages = new();
        private readonly Dictionary<int, Identity> _handleToContainer = new();
        private readonly Dictionary<int, DirtyEntry> _dirty = new();
        private readonly object _dirtyGate = new();

        /// <summary>Pending durable write for an item keyed by unique InstanceId.</summary>
        private readonly struct DirtyEntry
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

        public bool IsHydrated =>
            Inventory != null
            && Equipment != null
            && Armor != null
            && Implant != null
            && Social != null
            && Bank != null;

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
                IdentityType.Bank => Bank.IsHydrated ? Bank : null,
                _ => null
            };

            if (page == null)
                return false;

            return page.Content.TryGetValue(placement, out item!);
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

        public bool TryGetBackpackPage(Identity containerIdentity, out Container page)
        {
            page = null!;
            if (containerIdentity.Type != IdentityType.Container || containerIdentity.Instance == 0)
                return false;

            return _backpackPages.TryGetValue(containerIdentity.Instance, out page!);
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
                ParentSlot = parentSlot
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
                        Count = (short)Math.Clamp(Math.Max(1, item.StackCount), 1, short.MaxValue),
                        Identity = item.Identity.Instance != 0
                            ? item.Identity
                            : new Identity { Type = IdentityType.Bank, Instance = pair.Key },
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
            if (!TryGetContainerByHandle(handle, out Identity containerIdentity))
                return false;

            return TryGetBackpackPage(containerIdentity, out page);
        }

        public void MarkDirty(Item item, Container page, int placement)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(page);

            // Dirty set is keyed by unique InstanceId. Skip unkeyed stubs.
            if (item.InstanceId <= 0)
                return;

            var entry = new DirtyEntry(
                item,
                (int)page.Identity.Type,
                page.Identity.Instance,
                placement);

            lock (_dirtyGate)
                _dirty[item.InstanceId] = entry;
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

            DirtyEntry[] pending;
            lock (_dirtyGate)
            {
                if (_dirty.Count == 0)
                    return;

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
                    int itemType = item.Identity.Type != IdentityType.None
                        ? (int)item.Identity.Type
                        : item.Definition.ItemType;

                    inserts.Add(
                        new ItemInstanceRecord
                        {
                            InstanceId = item.InstanceId,
                            ContainerType = entry.ContainerType,
                            ContainerInstance = entry.ContainerInstance,
                            ContainerPlacement = entry.ContainerPlacement,
                            ItemType = itemType,
                            LowId = item.LowId,
                            HighId = item.HighId,
                            Quality = item.Quality,
                            StackCount = item.StackCount
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
                            entry.ContainerPlacement));
                }
            }

            try
            {
                repository.PersistNewAndUpdateLocations(inserts, updates);
                for (int i = 0; i < newlyPersisted.Count; i++)
                    newlyPersisted[i].IsPersisted = true;
            }
            catch
            {
                lock (_dirtyGate)
                {
                    foreach (DirtyEntry entry in pending)
                    {
                        if (!_dirty.ContainsKey(entry.InstanceId))
                            _dirty[entry.InstanceId] = entry;
                    }
                }

                throw;
            }
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
            Bank = new Container(IdentityType.Bank, offset: 0, capacity: BankCapacity, instanceId: characterId)
            {
                Flags = ContainerFlags.Bank | ContainerFlags.CanAdd | ContainerFlags.CanRemove,
                IsHydrated = false
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
                    Count = (short)Math.Clamp(item.StackCount, short.MinValue, short.MaxValue),
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
