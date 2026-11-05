namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AORebirth.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

[TestClass]
public sealed class MissionChestPersistenceTests
{
    [TestMethod]
    public void RestrictedStaleChestProjectionCannotTransferOrDeleteItsDurableRow()
    {
        using var world = new InventoryActionTests.World();
        var registry = new DynelRegistry();
        using var services = MissionChestProtocolTests.AttachChestServices(world, registry);
        Chest chest = MissionChestProtocolTests.CreateCapturedChest();
        chest.Playfield = world.Player.Playfield;
        chest.Position = world.Player.Position;
        registry.Register(chest);
        registry.Register(world.Player);
        Item item = TestWorld.CreateItem(instanceId: 901);
        item.IsPersisted = true;
        chest.Loot.Add(0, item);
        ItemInstanceRecord original = item.ToRecord(chest.Loot.Identity, 0, item.StackCount);
        world.Persistence.Rows[item.InstanceId] = original;
        Assert.IsTrue(chest.TryUse(world.Player));
        // Deliberately leave an obsolete in-memory slot to exercise packet/commit enforcement independently
        // of the normal Population.Refresh projection filter.
        chest.LootItemValidator = _ => false;
        Identity source = new() { Type = IdentityType.Backpack, Instance = chest.InventoryHandle << 16 };
        var moves = new InventoryMoveService(new StubLogger(), world.Flush, world.Actions);
        moves.Handle(world.Player, new ClientMoveItemToInventoryMessage { SourceContainer = source, TargetPlacement = 64 });
        Assert.IsFalse(world.Actions.TryDelete(world.Player, source, item.InstanceId));
        Assert.AreEqual(0, world.Persistence.Calls);
        Assert.AreSame(original, world.Persistence.Rows[item.InstanceId]);
        Assert.AreSame(item, chest.Loot.Content[0]);
        Assert.AreEqual(0, world.Player.Inventory.Inventory.Content.Count);
    }

    [TestMethod]
    public void TwoPlayersRespectTheSingleOpenerAndKeepDistinctOriginalLootInstances()
    {
        using var world = new InventoryActionTests.World();
        var registry = new DynelRegistry();
        using var services = MissionChestProtocolTests.AttachChestServices(world, registry);
        var otherSession = new InventoryActionTests.Session();
        Player other = TestWorld.CreatePlayer(222);
        other.Session = otherSession;
        otherSession.BindPlayer(other);
        other.Playfield = world.Player.Playfield;
        other.Position = world.Player.Position;
        Chest chest = MissionChestProtocolTests.CreateCapturedChest();
        chest.Playfield = world.Player.Playfield;
        chest.Position = world.Player.Position;
        registry.Register(chest); registry.Register(world.Player); registry.Register(other);
        for (int slot = 0; slot < 2; slot++)
        {
            Item item = TestWorld.CreateItem(instanceId: 901 + slot);
            chest.Loot.Add(slot, item);
            world.Persistence.Rows[item.InstanceId] = item.ToRecord(chest.Loot.Identity, slot, item.StackCount);
        }
        var moves = new InventoryMoveService(new StubLogger(), world.Flush, world.Actions);
        Assert.IsTrue(chest.TryUse(world.Player));
        Assert.IsFalse(chest.TryUse(other));
        var firstSource = new Identity { Type = IdentityType.Backpack, Instance = chest.InventoryHandle << 16 };
        moves.Handle(other, new ClientMoveItemToInventoryMessage { SourceContainer = firstSource, TargetPlacement = 64 });
        Assert.AreEqual(0, world.Persistence.Calls, "A player who did not open this chest cannot take its items.");
        Assert.AreEqual(2, chest.Loot.Content.Count);
        moves.Handle(world.Player, new ClientMoveItemToInventoryMessage { SourceContainer = firstSource, TargetPlacement = 64 });
        Assert.AreEqual(901, world.Player.Inventory.Inventory.Content[64].InstanceId);
        Assert.AreEqual(902, chest.Loot.Content.Single().Value.InstanceId);
        Assert.IsTrue(chest.TryUse(world.Player));
        Assert.IsTrue(chest.TryUse(other));
        moves.Handle(world.Player, new ClientMoveItemToInventoryMessage
        {
            SourceContainer = new Identity { Type = IdentityType.Backpack, Instance = chest.InventoryHandle << 16 | 1 },
            TargetPlacement = 65
        });
        Assert.AreEqual(1, world.Persistence.Calls, "The former opener cannot keep taking after ownership of the open window changes.");
        moves.Handle(other, new ClientMoveItemToInventoryMessage
        {
            SourceContainer = new Identity { Type = IdentityType.Backpack, Instance = chest.InventoryHandle << 16 | 1 },
            TargetPlacement = 64
        });
        Assert.AreEqual(902, other.Inventory.Inventory.Content[64].InstanceId);
        Assert.AreEqual(0, chest.Loot.Content.Count);
        Assert.AreEqual(2, world.Persistence.Calls);
        Assert.AreEqual(world.Player.Identity.Instance, world.Persistence.Rows[901].ContainerInstance);
        Assert.AreEqual(other.Identity.Instance, world.Persistence.Rows[902].ContainerInstance);
        Assert.AreEqual(1, world.Session.Messages.OfType<ContainerAddItemMessage>().Count());
        Assert.AreEqual(1, otherSession.Messages.OfType<ContainerAddItemMessage>().Count());
    }

    [TestMethod]
    public void FullPlayerInventoryRejectsChestLootWithoutChangingEitherOwner()
    {
        using var world = new InventoryActionTests.World();
        var registry = new DynelRegistry();
        using var services = MissionChestProtocolTests.AttachChestServices(world, registry);
        Chest chest = MissionChestProtocolTests.CreateCapturedChest();
        chest.Playfield = world.Player.Playfield; chest.Position = world.Player.Position;
        registry.Register(chest); registry.Register(world.Player);
        Item loot = TestWorld.CreateItem(instanceId: 901);
        chest.Loot.Add(0, loot);
        ItemInstanceRecord original = loot.ToRecord(chest.Loot.Identity, 0, loot.StackCount);
        world.Persistence.Rows[loot.InstanceId] = original;
        Container inventory = world.Player.Inventory.Inventory;
        for (int index = 0; index < inventory.Capacity; index++)
            world.Add(2000 + index, 1, inventory.Offset + index, lowId: 3000 + index);
        Assert.IsFalse(world.Player.Inventory.HasFreeInventorySlots(1));
        Assert.IsTrue(chest.TryUse(world.Player));
        world.Session.Messages.Clear();
        new InventoryMoveService(new StubLogger(), world.Flush, world.Actions).Handle(world.Player,
            new ClientMoveItemToInventoryMessage
            {
                SourceContainer = new Identity { Type = IdentityType.Backpack, Instance = chest.InventoryHandle << 16 },
                TargetPlacement = inventory.Offset
            });
        Assert.AreSame(loot, chest.Loot.Content[0]);
        Assert.AreSame(original, world.Persistence.Rows[loot.InstanceId]);
        Assert.AreEqual(inventory.Capacity, inventory.Content.Count);
        for (int index = 0; index < inventory.Capacity; index++)
            Assert.AreEqual(2000 + index, inventory.Content[inventory.Offset + index].InstanceId);
        Assert.AreEqual(0, world.Persistence.Calls);
        Assert.AreEqual(0, world.Session.Messages.OfType<ContainerAddItemMessage>().Count());
    }

    [TestMethod]
    public void UniqueItemAlreadyHeldRejectsChestLootWithoutConsumingTheChestItem()
    {
        using var world = new InventoryActionTests.World();
        var registry = new DynelRegistry();
        using var services = MissionChestProtocolTests.AttachChestServices(world, registry);
        Chest chest = MissionChestProtocolTests.CreateCapturedChest();
        chest.Playfield = world.Player.Playfield; chest.Position = world.Player.Position;
        registry.Register(chest); registry.Register(world.Player);
        Item held = world.Add(902, 1, 64);
        Item loot = TestWorld.CreateItem(instanceId: 901, flags: ItemFlags.Unique);
        chest.Loot.Add(0, loot);
        ItemInstanceRecord original = loot.ToRecord(chest.Loot.Identity, 0, loot.StackCount);
        world.Persistence.Rows[loot.InstanceId] = original;
        Assert.IsTrue(world.Player.Inventory.HasFreeInventorySlots(1));
        Assert.IsTrue(chest.TryUse(world.Player));
        world.Session.Messages.Clear();
        new InventoryMoveService(new StubLogger(), world.Flush, world.Actions).Handle(world.Player,
            new ClientMoveItemToInventoryMessage
            {
                SourceContainer = new Identity { Type = IdentityType.Backpack, Instance = chest.InventoryHandle << 16 },
                TargetPlacement = 65
            });
        Assert.AreSame(loot, chest.Loot.Content[0]);
        Assert.AreSame(original, world.Persistence.Rows[loot.InstanceId]);
        Assert.AreSame(held, world.Player.Inventory.Inventory.Content[64]);
        Assert.IsFalse(world.Player.Inventory.Inventory.Content.ContainsKey(65));
        Assert.AreEqual(0, world.Persistence.Calls);
        Assert.AreEqual(0, world.Session.Messages.OfType<ContainerAddItemMessage>().Count());
    }

    [TestMethod]
    public void UniqueItemHeldInOverflowRejectsChestLootWithoutChangingDurableRows()
        => AssertHeldUniqueRejectsChestLoot(inUnopenedBag: false);

    [TestMethod]
    public void UniqueItemStoredInAnUnopenedCarriedBagRejectsChestLootWithoutHydratingTheBag()
        => AssertHeldUniqueRejectsChestLoot(inUnopenedBag: true);

    static void AssertHeldUniqueRejectsChestLoot(bool inUnopenedBag)
    {
        using var world = new InventoryActionTests.World();
        var registry = new DynelRegistry();
        var repository = new ReadOnlyContainerRepository(world.Persistence.Rows);
        using var services = new ServiceCollection().AddSingleton(registry)
            .AddSingleton(new PlayfieldLocality(4582, null))
            .AddSingleton<IInventoryRepository>(repository).BuildServiceProvider();
        typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(world.Player.Playfield, services);
        world.Player.Inventory.SeedClientContainerHandle(117);
        Chest chest = MissionChestProtocolTests.CreateCapturedChest();
        chest.Playfield = world.Player.Playfield; chest.Position = world.Player.Position;
        registry.Register(chest); registry.Register(world.Player);
        Item held = TestWorld.CreateItem(instanceId: 902, flags: ItemFlags.Unique);
        Container heldPage;
        Item? bag = null;
        if (inUnopenedBag)
        {
            bag = TestWorld.CreateItem(lowId: 2000, highId: 2000, instanceId: 903);
            bag.Identity = new Identity { Type = IdentityType.Container, Instance = bag.InstanceId };
            Assert.IsTrue(world.Player.Inventory.Inventory.Add(64, bag));
            world.Persistence.Rows[bag.InstanceId] = bag.ToRecord(world.Player.Inventory.Inventory.Identity, 64, 1);
            heldPage = world.Player.Inventory.GetOrCreateBackpackPage(bag, bag.Identity,
                new Identity { Type = IdentityType.Inventory, Instance = 64 });
            Assert.IsFalse(heldPage.IsHydrated);
            Assert.AreEqual(0, heldPage.Content.Count);
        }
        else
        {
            heldPage = world.Player.Inventory.Overflow;
            Assert.IsTrue(heldPage.Add(heldPage.Offset, held));
        }
        ItemInstanceRecord heldRow = held.ToRecord(heldPage.Identity, heldPage.Offset, held.StackCount);
        world.Persistence.Rows[held.InstanceId] = heldRow;
        Item loot = TestWorld.CreateItem(instanceId: 901, flags: ItemFlags.Unique);
        Assert.IsTrue(chest.Loot.Add(0, loot));
        ItemInstanceRecord chestRow = loot.ToRecord(chest.Loot.Identity, 0, loot.StackCount);
        world.Persistence.Rows[loot.InstanceId] = chestRow;
        Assert.IsTrue(chest.TryUse(world.Player));
        Assert.IsTrue(chest.InventoryHandle > 0);
        world.Session.Messages.Clear();

        new InventoryMoveService(new StubLogger(), world.Flush, world.Actions).Handle(world.Player,
            new ClientMoveItemToInventoryMessage
            {
                SourceContainer = new Identity { Type = IdentityType.Backpack, Instance = chest.InventoryHandle << 16 },
                TargetPlacement = 65
            });

        Assert.AreSame(loot, chest.Loot.Content[0]);
        Assert.AreSame(chestRow, world.Persistence.Rows[loot.InstanceId]);
        Assert.AreSame(heldRow, world.Persistence.Rows[held.InstanceId]);
        Assert.IsFalse(world.Player.Inventory.Inventory.Content.ContainsKey(65));
        Assert.AreEqual(0, world.Persistence.Calls);
        Assert.AreEqual(0, world.Session.Messages.OfType<ContainerAddItemMessage>().Count());
        if (inUnopenedBag)
        {
            Assert.AreSame(bag, world.Player.Inventory.Inventory.Content[64]);
            Assert.IsFalse(heldPage.IsHydrated);
            Assert.AreEqual(0, heldPage.Content.Count);
            CollectionAssert.AreEqual(new[] { bag!.InstanceId }, repository.ReadContainers);
        }
        else
        {
            Assert.AreSame(held, heldPage.Content[heldPage.Offset]);
            Assert.AreEqual(0, repository.ReadContainers.Count);
        }
    }

    sealed class ReadOnlyContainerRepository(IReadOnlyDictionary<int, ItemInstanceRecord> rows) : IInventoryRepository
    {
        public List<int> ReadContainers { get; } = [];
        public IReadOnlyList<ItemInstanceRecord> GetContainerItems(int id)
        {
            ReadContainers.Add(id);
            return rows.Values.Where(row => row.ContainerType == (int)IdentityType.Container
                && row.ContainerInstance == id).ToArray();
        }
        public IReadOnlyList<ItemInstanceRecord> GetCarriedItems(int id) => throw new NotSupportedException();
        public IReadOnlyList<ItemInstanceRecord> GetBankItems(int id) => throw new NotSupportedException();
        public int LeaseInstanceIdBlock(int count) => throw new NotSupportedException();
        public ItemInstanceRecord Insert(ItemInstanceRecord item) => throw new NotSupportedException();
        public void UpdateLocation(int id, int type, int owner, int placement) => throw new NotSupportedException();
        public void UpdateLocations(IReadOnlyList<ItemLocationUpdate> updates) => throw new NotSupportedException();
        public void PersistNewAndUpdateLocations(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates)
            => throw new NotSupportedException();
    }

    [TestMethod]
    public void OrdinaryTransientCorpseLootStillInsertsOnTakeWithoutChestSourceGuards()
    {
        using var world = new InventoryActionTests.World();
        var registry = new DynelRegistry();
        using var services = MissionChestProtocolTests.AttachChestServices(world, registry);
        Player dead = TestWorld.CreatePlayer(222);
        dead.Playfield = world.Player.Playfield;
        dead.Position = world.Player.Position;
        var corpse = new Corpse(new Identity { Type = IdentityType.Corpse, Instance = 900 }, dead,
            new StubGameData(new HashItemCatalog(new(), new())));
        registry.Register(corpse); registry.Register(world.Player);
        Item loot = TestWorld.CreateItem(instanceId: 901, persisted: false);
        corpse.Loot.Add(0, loot);
        Assert.IsTrue(corpse.TryUse(world.Player));
        world.Session.Messages.Clear();
        world.Persistence.BeforeCommit = batch =>
        {
            Assert.AreEqual(0, batch.ExpectedSources.Count);
            Assert.AreEqual(901, batch.Inserts.Single().InstanceId);
            Assert.AreSame(loot, corpse.Loot.Content[0]);
        };
        new InventoryMoveService(new StubLogger(), world.Flush, world.Actions).Handle(world.Player,
            new ClientMoveItemToInventoryMessage
            {
                SourceContainer = new Identity { Type = IdentityType.Backpack, Instance = corpse.InventoryHandle << 16 },
                TargetPlacement = 64
            });
        Assert.AreEqual(1, world.Persistence.Calls);
        Assert.AreEqual(0, corpse.Loot.Content.Count);
        Assert.AreSame(loot, world.Player.Inventory.Inventory.Content[64]);
        Assert.IsTrue(loot.IsPersisted);
        Assert.AreEqual(world.Player.Identity.Instance, world.Persistence.Rows[901].ContainerInstance);
        Assert.AreEqual(1, world.Session.Messages.OfType<ContainerAddItemMessage>().Count());
    }

    [TestMethod]
    public void PackedChestMoveUsesExpectedSourceAndDuplicateIsNoOp()
    {
        using var world = new InventoryActionTests.World();
        var registry = new DynelRegistry();
        using var services = MissionChestProtocolTests.AttachChestServices(world, registry);
        Chest chest = MissionChestProtocolTests.CreateCapturedChest();
        chest.Playfield = world.Player.Playfield; chest.Position = world.Player.Position;
        registry.Register(chest); registry.Register(world.Player);
        Item item = TestWorld.CreateItem(instanceId: 901);
        chest.Loot.Add(0, item);
        world.Persistence.Rows[item.InstanceId] = item.ToRecord(chest.Loot.Identity, 0, item.StackCount);
        Assert.IsTrue(chest.TryUse(world.Player));
        world.Session.Messages.Clear();
        var source = new Identity { Type = IdentityType.Backpack, Instance = chest.InventoryHandle << 16 };
        world.Persistence.BeforeCommit = batch =>
        {
            Assert.AreEqual(1, batch.ExpectedSources.Count);
            Assert.AreEqual(chest.Identity.Instance, batch.ExpectedSources[0].ContainerInstance);
        };
        var moves = new InventoryMoveService(new StubLogger(), world.Flush, world.Actions);
        moves.Handle(world.Player, new ClientMoveItemToInventoryMessage { SourceContainer = source, TargetPlacement = 64 });
        Assert.AreSame(item, world.Player.Inventory.Inventory.Content[64]);
        Assert.AreEqual(0, chest.Loot.Content.Count);
        Assert.AreEqual(1, world.Session.Messages.OfType<ContainerAddItemMessage>().Count());
        moves.Handle(world.Player, new ClientMoveItemToInventoryMessage { SourceContainer = source, TargetPlacement = 65 });
        Assert.AreEqual(1, world.Persistence.Calls);
        Assert.IsFalse(world.Actions.TrySplit(world.Player, source, item.InstanceId, 1));
        Assert.IsFalse(world.Actions.TryJoin(world.Player, source, source));
        Assert.AreEqual(1, world.Persistence.Calls);
    }

    [TestMethod]
    public void ChestMoveCannotSwapAnOccupiedWearItemBackIntoTheChest()
    {
        using var world = new InventoryActionTests.World();
        var registry = new DynelRegistry();
        using var services = MissionChestProtocolTests.AttachChestServices(world, registry);
        Chest chest = MissionChestProtocolTests.CreateCapturedChest();
        chest.Playfield = world.Player.Playfield; chest.Position = world.Player.Position;
        registry.Register(chest); registry.Register(world.Player);
        Item loot = TestWorld.CreateItem(instanceId: 901);
        Item worn = TestWorld.CreateItem(instanceId: 902);
        Container equipment = world.Player.Inventory.Equipment;
        int destination = equipment.Offset;
        chest.Loot.Add(0, loot); equipment.Add(destination, worn);
        world.Persistence.Rows[loot.InstanceId] = loot.ToRecord(chest.Loot.Identity, 0, loot.StackCount);
        world.Persistence.Rows[worn.InstanceId] = worn.ToRecord(equipment.Identity, destination, worn.StackCount);
        Assert.IsTrue(chest.TryUse(world.Player));
        var moves = new InventoryMoveService(new StubLogger(), world.Flush, world.Actions);
        bool moved = (bool)typeof(InventoryMoveService).GetMethod("ApplyMove", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(moves, [world.Player, chest.Loot, 0, equipment, destination, loot, chest])!;
        Assert.IsFalse(moved);
        Assert.AreSame(loot, chest.Loot.Content[0]);
        Assert.AreSame(worn, equipment.Content[destination]);
        Assert.AreEqual(0, world.Persistence.Calls);
        Assert.AreEqual(chest.Identity.Instance, world.Persistence.Rows[loot.InstanceId].ContainerInstance);
        Assert.AreEqual(world.Player.Identity.Instance, world.Persistence.Rows[worn.InstanceId].ContainerInstance);
    }

    [TestMethod]
    public void ChestTransferPreservesInstanceAndRemainingContentsAndPublishesAfterCommit()
    {
        using var world = new InventoryActionTests.World();
        var chest = new Container(IdentityType.Container, offset: 0, capacity: 3, instanceId: 900);
        Item first = TestWorld.CreateItem(instanceId: 901);
        Item remaining = TestWorld.CreateItem(instanceId: 902);
        chest.Add(0, first); chest.Add(1, remaining);
        world.Persistence.Rows[first.InstanceId] = first.ToRecord(chest.Identity, 0, first.StackCount);
        world.Persistence.Rows[remaining.InstanceId] = remaining.ToRecord(chest.Identity, 1, remaining.StackCount);
        world.Persistence.BeforeCommit = batch =>
        {
            Assert.AreSame(first, chest.Content[0]);
            Assert.IsFalse(world.Player.Inventory.Inventory.Content.ContainsKey(64));
            Assert.AreEqual(900, batch.ExpectedSources[0].ContainerInstance);
        };
        Assert.IsTrue(world.Actions.TryCommit(world.Player,
            [new InventoryRowChange(first, world.Player.Inventory.Inventory.Identity, 64, first.StackCount)],
            () => chest.Content.TryGetValue(0, out Item? item) && ReferenceEquals(item, first),
            () => { chest.Content.Remove(0); world.Player.Inventory.Inventory.Add(64, first); },
            expectedSources: [first.ToRecord(chest.Identity, 0, first.StackCount)]));
        Assert.AreSame(first, world.Player.Inventory.Inventory.Content[64]);
        Assert.AreSame(remaining, chest.Content[1]);
        Assert.AreEqual(world.Player.Identity.Instance, world.Persistence.Rows[first.InstanceId].ContainerInstance);
        Assert.AreEqual(900, world.Persistence.Rows[remaining.InstanceId].ContainerInstance);
    }

    [TestMethod]
    public void StaleSharedChestProjectionCannotStealAnItemAlreadyTransferredToAnotherPlayer()
    {
        using var world = new InventoryActionTests.World();
        Item stale = TestWorld.CreateItem(instanceId: 901);
        var chest = new Container(IdentityType.Container, offset: 0, capacity: 3, instanceId: 900);
        chest.Add(0, stale);
        world.Persistence.Rows[901] = stale.ToRecord(new Identity { Type = IdentityType.Inventory, Instance = 222 }, 64, stale.StackCount);
        Assert.IsFalse(world.Actions.TryCommit(world.Player,
            [new InventoryRowChange(stale, world.Player.Inventory.Inventory.Identity, 64, stale.StackCount)],
            () => true, () => Assert.Fail("A stale source must never publish."),
            expectedSources: [stale.ToRecord(chest.Identity, 0, stale.StackCount)]));
        Assert.AreEqual(222, world.Persistence.Rows[901].ContainerInstance);
        Assert.AreSame(stale, chest.Content[0]);
        Assert.AreEqual(0, world.Player.Inventory.Inventory.Content.Count);
        Assert.AreEqual(0, world.Session.Messages.Count);
    }

    [TestMethod]
    public void LostChestTransferCommitAcknowledgementClosesSessionWithoutRepeatingTheDurableMove()
    {
        using var world = new InventoryActionTests.World();
        Item item = TestWorld.CreateItem(instanceId: 901);
        var chest = new Container(IdentityType.Container, offset: 0, capacity: 3, instanceId: 900);
        chest.Add(0, item);
        world.Persistence.Rows[901] = item.ToRecord(chest.Identity, 0, item.StackCount);
        world.Persistence.AfterCommitFailure = new DatabaseCommitOutcomeUnknownException(new Exception("lost reply after durable commit"));
        bool Transfer() => world.Actions.TryCommit(world.Player,
            [new InventoryRowChange(item, world.Player.Inventory.Inventory.Identity, 64, item.StackCount)],
            () => true, () => Assert.Fail("Unknown commit must not publish."),
            expectedSources: [item.ToRecord(chest.Identity, 0, item.StackCount)]);
        Assert.IsFalse(Transfer());
        Assert.AreEqual(world.Player.Identity.Instance, world.Persistence.Rows[901].ContainerInstance);
        Assert.AreEqual(SessionState.Closed, world.Session.State);
        Assert.IsTrue(world.Player.IsPersistenceQuarantined);
        Assert.IsFalse(Transfer());
        Assert.AreEqual(1, world.Persistence.Calls);
        Assert.AreSame(item, chest.Content[0]);
        Assert.AreEqual(0, world.Session.Messages.Count);
    }
}
