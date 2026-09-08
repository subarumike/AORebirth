namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.GameData;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;

    /// <summary>
    /// Serves a fixed <see cref="HashItemCatalog"/> and vending machine table. Everything a hash roll
    /// does not touch throws, so a test that starts depending on playfield data fails loudly.
    /// </summary>
    internal sealed class StubGameData : IGameData
    {
        readonly HashItemCatalog _hashItems;
        readonly Dictionary<int, VendingMachineDefinition> _machines;

        public StubGameData(
            HashItemCatalog hashItems,
            Dictionary<int, VendingMachineDefinition>? machines = null)
        {
            _hashItems = hashItems;
            _machines = machines ?? new Dictionary<int, VendingMachineDefinition>();
        }

        public string RootPath => string.Empty;

        public int MobTemplateCount => 0;

        public int HashTemplateCount => _hashItems.CategoryCount;

        public int HashInstanceCount => _hashItems.InstanceCount;

        public int MonsterDataCount => 0;

        public int XpLevelCount => 0;

        public bool TryGetHashTemplate(string hash, out IReadOnlyList<string> childHashes)
            => _hashItems.TryGetCategory(hash, out childHashes);

        public bool TryGetHashInstance(string hash, out HashInstance instance)
            => _hashItems.TryGetInstance(hash, out instance);

        public bool TryResolveHashInstance(string hash, out HashInstance instance)
            => _hashItems.TryResolveInstance(hash, out instance);

        public void CollectHashLeafInstances(string hash, List<HashInstance> into)
            => _hashItems.CollectLeafInstances(hash, into);

        public bool TryGetVendingMachine(int templateId, out VendingMachineDefinition definition)
            => _machines.TryGetValue(templateId, out definition!);

        public bool TryGetXpLevel(int level, out XpLevelEntry entry) => throw new NotSupportedException();

        public bool TryGetMobTemplate(string hash, out MobTemplate template) => throw new NotSupportedException();

        public MobTemplate RequireMobTemplate(string hash) => throw new NotSupportedException();

        public bool TryGetCatMesh(int monsterData, out int catMesh) => throw new NotSupportedException();

        public PlayfieldMetaData? GetPlayfieldMetaData(int playfieldId) => throw new NotSupportedException();

        public PlayfieldSpawnsData GetPlayfieldSpawns(int playfieldId) => throw new NotSupportedException();

        public PlayfieldGeometryData GetPlayfieldGeometry(int playfieldId) => throw new NotSupportedException();

        public IReadOnlyList<int> GetExitProxyDoorInstances(int playfieldId) => throw new NotSupportedException();
    }

    /// <summary>Only templates added through <see cref="Add"/> resolve; everything else misses.</summary>
    internal sealed class StubCatalog : IItemTemplateCatalog
    {
        readonly Dictionary<int, ItemTemplate> _templates = new();

        public StubCatalog Add(int id, int quality, int price = 0, int flags = 0)
        {
            _templates[id] = new ItemTemplate
            {
                Id = id,
                Quality = quality,
                Flags = flags,
                Stats = new Dictionary<CharacterStat, int> { [CharacterStat.Value] = price }
            };
            return this;
        }

        public bool TryGet(int aoid, out ItemTemplate template)
            => _templates.TryGetValue(aoid, out template!);

        public ItemTemplate Require(int aoid) => _templates[aoid];
    }

    internal sealed class StubLogger : IZoneLogger
    {
        public void Debug(string message)
        {
        }

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message)
        {
        }

        public void Error(Exception exception, string message)
        {
        }

        public IZoneLogger CreateForPlayfield(int playfieldId) => this;
    }

    internal static class TestWorld
    {
        /// <summary>A hydrated, playfield-less player. Enough for inventory and trade-offer rules.</summary>
        public static Player CreatePlayer(int instance, string firstName = "Test")
        {
            var player = new Player(
                new Identity { Type = IdentityType.CanbeAffected, Instance = instance },
                new StubLogger(),
                new StubItemBuilder())
            {
                FirstName = firstName
            };

            player.Inventory.Apply(new CharacterHydrationResult(), instance, new StubItemBuilder());
            return player;
        }

        /// <summary>
        /// Builds an item without touching the catalog so tests can set flags and durability directly.
        /// </summary>
        public static Item CreateItem(
            int lowId = 1000,
            int highId = 1000,
            int quality = 1,
            ItemFlags flags = 0,
            int instanceId = 1,
            bool persisted = true,
            string name = "Item")
            => new()
            {
                InstanceId = instanceId,
                IsPersisted = persisted,
                LowId = lowId,
                HighId = highId,
                Quality = quality,
                Source = ItemSource.Command,
                Definition = new ItemTemplate
                {
                    Id = lowId,
                    Name = name,
                    Quality = quality,
                    Flags = (int)flags
                }
            };

        /// <summary>Fills main inventory so the next placement has to fall through to overflow.</summary>
        public static void FillInventory(Player player)
        {
            int slot;
            int id = 5000;
            while ((slot = player.Inventory.Inventory.FindFreeSlot()) >= 0)
            {
                player.Inventory.Inventory.Add(slot, CreateItem(lowId: id, highId: id, instanceId: id));
                id++;
            }
        }
    }

    internal sealed class StubItemBuilder : IItemBuilder
    {
        public Item Create(
            int lowId,
            int highId,
            int quality,
            ItemSource source,
            int stackCount = 1,
            int instanceId = 0,
            Identity? identity = null,
            byte[]? statsBlob = null)
            => new()
            {
                LowId = lowId,
                HighId = highId,
                Quality = quality,
                Source = source,
                StackCount = stackCount,
                InstanceId = instanceId,
                Identity = identity ?? Identity.None,
                Definition = CreateTemplate(lowId, highId, quality)
            };

        public ItemTemplate CreateTemplate(int lowId, int highId, int quality)
            => new()
            {
                Id = lowId,
                Quality = quality
            };

        public bool TryFromInstanceRecord(ItemInstanceRecord row, out Item item)
            => throw new NotSupportedException();
    }
}
