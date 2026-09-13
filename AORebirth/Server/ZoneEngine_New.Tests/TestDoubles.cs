namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.GameData;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    using Vector3 = AORebirth.Core.Vector.Vector3;

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

        public int NpcFamilyStatTemplateCount => 0;

        public int NpcStatTemplateCount => 0;

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

        public bool TryGetNpcFamilyStatTemplate(int family, out NpcFamilyStatTemplate template)
        {
            template = null!;
            return false;
        }

        public bool TryResolveNpcFamilyStatTemplate(int family, out NpcFamilyStatTemplate template)
        {
            template = null!;
            return false;
        }

        public bool TryGetNpcStatTemplate(int id, out NpcStatTemplate template)
        {
            template = null!;
            return false;
        }

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

        public StubCatalog Add(int id, int quality, int price = 0, int flags = 0, int multipleCount = 0)
        {
            var stats = new Dictionary<CharacterStat, int> { [CharacterStat.Value] = price };
            if (multipleCount > 0)
                stats[CharacterStat.MultipleCount] = multipleCount;

            _templates[id] = new ItemTemplate
            {
                Id = id,
                Quality = quality,
                Flags = flags,
                MultipleCount = multipleCount,
                Stats = stats
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
            string name = "Item",
            CanFlags can = 0,
            int stackCount = 1,
            IEnumerable<ItemSpell>? onUse = null)
        {
            var spellList = new Dictionary<EventType, List<ItemSpell>>();
            if (onUse != null)
                spellList[EventType.OnUse] = new List<ItemSpell>(onUse);

            return new Item
            {
                InstanceId = instanceId,
                IsPersisted = persisted,
                LowId = lowId,
                HighId = highId,
                Quality = quality,
                StackCount = stackCount,
                Source = ItemSource.Command,
                Definition = new ItemTemplate
                {
                    Id = lowId,
                    Name = name,
                    Quality = quality,
                    Flags = (int)flags,
                    Stats = new Dictionary<CharacterStat, int> { [CharacterStat.Can] = unchecked((int)can) },
                    SpellList = spellList
                }
            };
        }

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

    /// <summary>Builds nano definitions from the item attribute ids a real nano template uses.</summary>
    internal static class TestNanos
    {
        public static NanoSpell Create(
            int nanoId,
            int durationCentiseconds = 6000,
            int ncuCost = 10,
            int strain = 0,
            int stackingOrder = 0,
            int nanoPointCost = 0,
            int attackDelay = 0,
            int attackDelayCap = 0,
            int rechargeDelay = 0,
            int rechargeDelayCap = 0,
            CanFlags can = CanFlags.ApplyOnFriendly,
            bool canCancel = true,
            IEnumerable<ItemSpell>? modifiers = null)
        {
            var stats = new Dictionary<CharacterStat, int>
            {
                [CharacterStat.TimeExist] = durationCentiseconds,
                [NanoSpell.NcuCostStat] = ncuCost,
                [NanoSpell.NanoStrainStat] = strain,
                [CharacterStat.StackingOrder] = stackingOrder,
                [CharacterStat.NanoPoints] = nanoPointCost,
                [CharacterStat.AttackDelay] = attackDelay,
                [CharacterStat.AttackDelayCap] = attackDelayCap,
                [CharacterStat.RechargeDelay] = rechargeDelay,
                [CharacterStat.RechargeDelayCap] = rechargeDelayCap,
                [CharacterStat.Can] = unchecked((int)can)
            };

            var spellList = new Dictionary<EventType, List<ItemSpell>>();
            if (modifiers != null)
                spellList[EventType.OnUse] = new List<ItemSpell>(modifiers);

            return NanoSpell.From(
                new ItemTemplate
                {
                    Id = nanoId,
                    Name = "Nano " + nanoId,
                    Quality = 1,
                    Stats = stats,
                    SpellList = spellList,
                    CanCancel = canCancel
                });
        }

        /// <summary>A Modify function, the same shape worn equipment uses for stat bonuses.</summary>
        public static ItemSpell Modify(CharacterStat stat, int delta)
            => new()
            {
                FunctionType = (int)FunctionType.Modify,
                Arguments = new List<object> { (int)stat, delta },
                Requirements = new List<ItemRequirement>()
            };
    }

    internal sealed class StubItemBuilder : IItemBuilder
    {
        int _nextInstanceId = 90000;

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

        public Item CreateWithNewInstance(
            int lowId,
            int highId,
            int quality,
            ItemSource source,
            int stackCount = 1)
        {
            Item item = Create(lowId, highId, quality, source, stackCount);
            item.AssignInstanceId(++_nextInstanceId);
            return item;
        }

        public ItemTemplate CreateTemplate(int lowId, int highId, int quality)
            => new()
            {
                Id = lowId,
                Quality = quality
            };

        public bool TryFromInstanceRecord(ItemInstanceRecord row, out Item item)
            => throw new NotSupportedException();
    }

    /// <summary>Every call throws: item use paths must not reach the database.</summary>
    internal sealed class StubInventoryRepository : IInventoryRepository
    {
        public IReadOnlyList<ItemInstanceRecord> GetCarriedItems(int characterId)
            => throw new NotSupportedException();

        public IReadOnlyList<ItemInstanceRecord> GetBankItems(int characterId)
            => throw new NotSupportedException();

        public IReadOnlyList<ItemInstanceRecord> GetContainerItems(int containerInstanceId)
            => throw new NotSupportedException();

        public int LeaseInstanceIdBlock(int count) => throw new NotSupportedException();

        public ItemInstanceRecord Insert(ItemInstanceRecord item) => throw new NotSupportedException();

        public void UpdateLocation(int instanceId, int containerType, int containerInstance, int containerPlacement)
            => throw new NotSupportedException();

        public void UpdateLocations(IReadOnlyList<ItemLocationUpdate> locations)
            => throw new NotSupportedException();

        public void PersistNewAndUpdateLocations(
            IReadOnlyList<ItemInstanceRecord> inserts,
            IReadOnlyList<ItemLocationUpdate> updates)
            => throw new NotSupportedException();
    }

    internal sealed class StubInstanceIdAllocator : IItemInstanceIdAllocator
    {
        int _next = 500000;

        public int Allocate() => ++_next;
    }

    internal sealed class RecordingZoneSession : IZoneSession
    {
        public SessionState State { get; set; } = SessionState.Connected;

        public Player? Player { get; private set; }

        public bool IsClosed { get; private set; }

        public List<MessageBody> Sent { get; } = new();

        public void BindPlayer(Player player) => Player = player;

        public void UnbindPlayer() => Player = null;

        public void TransferToPlayfield(Playfield destination, Vector3 landing)
            => throw new NotSupportedException();

        public void SendSamePlayfieldRespawnTeleport(Vector3 landing)
            => throw new NotSupportedException();

        public void Send(byte[] packet)
        {
        }

        public void Send(Message message)
        {
            if (message?.Body != null)
                Sent.Add(message.Body);
        }

        public void Send(MessageBody body)
        {
            if (body != null)
                Sent.Add(body);
        }

        public void Send(MessageBody body, int sender, int receiver) => Send(body);

        public void SendInitiateCompression()
        {
        }

        public void Close() => IsClosed = true;
    }
}
