namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    [TestClass]
    public sealed class ItemChargeTests
    {
        [TestMethod]
        public void SpendingOneOfSeveralChargesKeepsTheItemInItsSlot()
        {
            Player player = CreatePlayerWithSession(out RecordingZoneSession session);
            Item item = Place(player, stackCount: 3, out int placement);

            item.ConsumeCharge(player, Slot(placement));

            Assert.AreEqual(2, item.StackCount);
            Assert.AreSame(item, player.Inventory.Inventory.Content[placement]);
            Assert.IsTrue(player.Inventory.HasDirtyEntries);
            Assert.AreEqual(0, session.Sent.Count);
        }

        [TestMethod]
        public void SpendingTheLastChargeDestroysTheItemAndTellsTheClient()
        {
            Player player = CreatePlayerWithSession(out RecordingZoneSession session);
            Item item = Place(player, stackCount: 1, out int placement);

            item.ConsumeCharge(player, Slot(placement));

            Assert.AreEqual(0, item.StackCount);
            Assert.IsFalse(player.Inventory.Inventory.Content.ContainsKey(placement));

            CharacterActionMessage action = SingleAction(session);
            Assert.AreEqual(CharacterActionType.DeleteItem, action.Action);
            Assert.AreEqual(IdentityType.Inventory, action.Target.Type);
            Assert.AreEqual(placement, action.Target.Instance);
        }

        [TestMethod]
        public void ADestroyedItemRowIsRehomedSoARelogCannotBringItBack()
        {
            Player player = CreatePlayerWithSession(out _);
            Item item = Place(player, stackCount: 1, out int placement);

            item.ConsumeCharge(player, Slot(placement));

            PlayerInventory.InventoryDirtyFlush? flush = player.Inventory.TakeDirty();
            Assert.IsNotNull(flush);
            Assert.AreEqual(1, flush!.Updates.Count);
            Assert.AreEqual(item.InstanceId, flush.Updates[0].InstanceId);
            Assert.AreEqual((int)IdentityType.None, flush.Updates[0].ContainerType);
            Assert.AreEqual(item.InstanceId, flush.Updates[0].ContainerPlacement);
        }

        [TestMethod]
        public void AnItemThatWasNeverPersistedLeavesNoPendingRowBehind()
        {
            Player player = CreatePlayerWithSession(out _);
            Item item = Place(player, stackCount: 1, out int placement, persisted: false);
            player.Inventory.MarkDirty(item, player.Inventory.Inventory, placement);

            item.ConsumeCharge(player, Slot(placement));

            Assert.IsFalse(player.Inventory.HasDirtyEntries);
        }

        [TestMethod]
        public void AnItemWithoutTheConsumeFlagKeepsItsCount()
        {
            Player player = CreatePlayerWithSession(out RecordingZoneSession session);
            Item item = Place(player, stackCount: 1, out int placement, can: CanFlags.Use);

            item.ConsumeCharge(player, Slot(placement));

            Assert.AreEqual(1, item.StackCount);
            Assert.AreSame(item, player.Inventory.Inventory.Content[placement]);
            Assert.AreEqual(0, session.Sent.Count);
        }

        [TestMethod]
        public void ALockedItemIsLeftAloneSoAMoveInFlightCannotLoseIt()
        {
            Player player = CreatePlayerWithSession(out RecordingZoneSession session);
            Item item = Place(player, stackCount: 1, out int placement);
            item.Locked = true;

            item.ConsumeCharge(player, Slot(placement));

            Assert.AreEqual(1, item.StackCount);
            Assert.AreSame(item, player.Inventory.Inventory.Content[placement]);
            Assert.AreEqual(0, session.Sent.Count);
        }

        [TestMethod]
        public void SpentChargesAreCarriedIntoTheFlushSoTheySurviveARelog()
        {
            Player player = CreatePlayerWithSession(out _);
            Item item = Place(player, stackCount: 5, out int placement);

            item.ConsumeCharge(player, Slot(placement));

            PlayerInventory.InventoryDirtyFlush? flush = player.Inventory.TakeDirty();
            Assert.IsNotNull(flush);
            Assert.AreEqual(1, flush!.Updates.Count);
            Assert.AreEqual(4, flush.Updates[0].StackCount);
        }

        [TestMethod]
        public void AStoredItemKeepsItsOwnCountInsteadOfTheTemplateCount()
        {
            var builder = new ItemBuilder(
                new StubCatalog().Add(id: 4242, quality: 1, multipleCount: 10),
                new StubInstanceIdAllocator(),
                new StubLogger());

            Item stored = builder.Create(4242, 4242, 1, ItemSource.Loot, stackCount: 3, instanceId: 77);
            Item minted = builder.Create(4242, 4242, 1, ItemSource.Vendor);

            Assert.AreEqual(3, stored.StackCount);
            Assert.AreEqual(10, minted.StackCount);
        }

        static Player CreatePlayerWithSession(out RecordingZoneSession session)
        {
            Player player = TestWorld.CreatePlayer(1);
            session = new RecordingZoneSession();
            session.BindPlayer(player);
            player.Session = session;
            return player;
        }

        static Item Place(
            Player player,
            int stackCount,
            out int placement,
            bool persisted = true,
            CanFlags can = CanFlags.Use | CanFlags.Consume)
        {
            Item item = TestWorld.CreateItem(
                instanceId: 4711,
                persisted: persisted,
                can: can,
                stackCount: stackCount);

            Assert.IsTrue(player.Inventory.TryPlace(item, out _, out placement));
            return item;
        }

        static Identity Slot(int placement)
            => new() { Type = IdentityType.Inventory, Instance = placement };

        static CharacterActionMessage SingleAction(RecordingZoneSession session)
        {
            var actions = new List<CharacterActionMessage>();
            foreach (MessageBody body in session.Sent)
            {
                if (body is CharacterActionMessage action)
                    actions.Add(action);
            }

            Assert.AreEqual(1, actions.Count);
            return actions[0];
        }
    }
}
