namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Data;

    public sealed class PlayerInventory
    {
        private readonly Dictionary<int, Container> _pages = new();

        public Container Inventory { get; private set; } = null!;

        public Container Equipment { get; private set; } = null!;

        public Container Armor { get; private set; } = null!;

        public Container Implant { get; private set; } = null!;

        public Container Social { get; private set; } = null!;

        public IReadOnlyDictionary<int, Container> Pages => _pages;

        public bool IsHydrated =>
            Inventory != null
            && Equipment != null
            && Armor != null
            && Implant != null
            && Social != null;

        public void Apply(CharacterHydrationResult hydration, int characterId, IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(hydration);
            ArgumentNullException.ThrowIfNull(items);

            CreatePages(characterId);

            foreach (ItemRecord row in hydration.Items)
            {
                if (!items.TryFromRecord(row, out Item item))
                    continue;

                AddItem(row.ContainerInstance, row.ContainerPlacement, item);
            }

            foreach (InstancedItemRecord row in hydration.InstancedItems)
            {
                if (!items.TryFromInstancedRecord(row, out Item item))
                    continue;

                AddItem(row.ContainerInstance, row.ContainerPlacement, item);
            }
        }

        public IEnumerable<InventorySlot> BuildInventorySlots()
        {
            foreach (KeyValuePair<int, Container> pageEntry in _pages)
            {
                int pageId = pageEntry.Key;
                Container page = pageEntry.Value;
                IdentityType pageType = (IdentityType)pageId;

                foreach (KeyValuePair<int, Item> slotEntry in page.Content)
                {
                    Item item = slotEntry.Value;
                    yield return new InventorySlot
                    {
                        Placement = slotEntry.Key,
                        Flags = (short)item.Flags,
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

        private void CreatePages(int characterId)
        {
            _pages.Clear();

            Inventory = new Container(IdentityType.Inventory, 0x40, 30, characterId);
            Equipment = new Container(IdentityType.WeaponPage, 0x01, 15, characterId);
            Armor = new Container(IdentityType.ArmorPage, 0x11, 15, characterId);
            Implant = new Container(IdentityType.ImplantPage, 0x21, 15, characterId);
            Social = new Container(IdentityType.SocialPage, 0x31, 15, characterId);

            _pages[(int)IdentityType.Inventory] = Inventory;
            _pages[(int)IdentityType.WeaponPage] = Equipment;
            _pages[(int)IdentityType.ArmorPage] = Armor;
            _pages[(int)IdentityType.ImplantPage] = Implant;
            _pages[(int)IdentityType.SocialPage] = Social;
        }

        private void AddItem(int pageId, int slot, Item item)
        {
            if (!_pages.TryGetValue(pageId, out Container? page))
                return;

            page.Add(slot, item);
        }
    }
}
