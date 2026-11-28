namespace ZoneEngine_New.Tests;

using System;
using System.Linq;
using AORebirth.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Quests;

public partial class RewardExecutionModuleTests
{
    [TestMethod]
    public void FollowUpStarterStacksPlanExactCountAndPublishAddBeforeJoin()
    {
        var player = TestWorld.CreatePlayer(72);
        var session = new RecordingZoneSession();
        player.Session = session;
        var held = TestWorld.CreateItem(instanceId: 12100, stackCount: 4, can: CanFlags.Stackable);
        var first = TestWorld.CreateItem(instanceId: 12101, persisted: false, stackCount: 2, can: CanFlags.Stackable);
        var second = TestWorld.CreateItem(instanceId: 12102, persisted: false, stackCount: 3, can: CanFlags.Stackable);
        var main = player.Inventory.Inventory;
        Assert.IsTrue(main.Add(main.Offset, held));

        lock (player.PersistenceGate)
        {
            Assert.IsTrue(QuestStarterItemPlan.TryCreate(player, [first, second], out var plan));
            Assert.IsEmpty(plan.StarterGrants);
            var merge = plan.StackMerges.Single();
            Assert.AreEqual(held.InstanceId, merge.Existing.InstanceId);
            Assert.AreEqual(4, merge.Existing.StackCount);
            Assert.AreEqual(9, merge.FinalCount);
            Assert.AreEqual(4, held.StackCount);
            Assert.IsEmpty(session.Sent);
            plan.PublishAfterCommit();
            Assert.ThrowsExactly<InvalidOperationException>(plan.PublishAfterCommit);
        }

        Assert.AreEqual(9, held.StackCount);
        Assert.HasCount(1, main.Content);
        CollectionAssert.AreEqual(new[] { typeof(AddTemplateMessage), typeof(CharacterActionMessage),
            typeof(AddTemplateMessage), typeof(CharacterActionMessage) }, session.Sent.Select(packet => packet.GetType()).ToArray());
        Assert.IsTrue(session.Sent.OfType<CharacterActionMessage>().All(packet => packet.Action == CharacterActionType.JoinItems));
    }

    [TestMethod]
    public void FollowUpStarterOverflowPlansOldestRetirementAndPreservesPacketOrder()
    {
        var player = TestWorld.CreatePlayer(73);
        var session = new RecordingZoneSession();
        player.Session = session;
        TestWorld.FillInventory(player);
        var overflow = player.Inventory.Overflow;
        int capacity = OverflowGrants.Capacity(player);
        for (int i = 0; i < capacity; i++)
        {
            var held = TestWorld.CreateItem(instanceId: 12200 + i);
            Assert.IsTrue(overflow.Add(overflow.Offset + i, held));
            player.Inventory.NoteOverflowArrival(held);
        }
        var replacement = TestWorld.CreateItem(instanceId: 12300, persisted: false);

        lock (player.PersistenceGate)
        {
            Assert.IsTrue(QuestStarterItemPlan.TryCreate(player, [replacement], out var plan));
            Assert.AreEqual(12200, plan.RetiredOverflow.Single().InstanceId);
            var grant = plan.StarterGrants.Single();
            Assert.AreEqual((int)IdentityType.OverflowWindow, grant.ContainerType);
            Assert.AreEqual(overflow.Offset + capacity, grant.ContainerPlacement);
            Assert.AreEqual(12200, overflow.Content[overflow.Offset].InstanceId);
            Assert.IsEmpty(session.Sent);
            plan.PublishAfterCommit();
        }

        Assert.IsFalse(overflow.Content.ContainsKey(overflow.Offset));
        Assert.AreSame(replacement, overflow.Content[overflow.Offset + capacity]);
        Assert.IsTrue(replacement.IsPersisted);
        Assert.IsFalse(player.Inventory.HasDirtyEntries);
        CollectionAssert.AreEqual(new[] { typeof(TemplateActionMessage), typeof(ContainerAddItemMessage),
            typeof(CharacterActionMessage), typeof(InventoryUpdateMessage) }, session.Sent.Select(packet => packet.GetType()).ToArray());
        Assert.AreEqual(CharacterActionType.DeleteItem, session.Sent.OfType<CharacterActionMessage>().Single().Action);
    }

    [TestMethod]
    public void FollowUpStarterCapacityFailureLeavesEveryHeldItemUntouched()
    {
        var player = TestWorld.CreatePlayer(74);
        TestWorld.FillInventory(player);
        var overflow = player.Inventory.Overflow;
        for (int i = 0; i < overflow.Capacity; i++)
            Assert.IsTrue(overflow.Add(overflow.Offset + i, TestWorld.CreateItem(instanceId: 12400 + i)));
        var incoming = TestWorld.CreateItem(instanceId: 12500, persisted: false);
        var before = overflow.Content.Values.ToArray();
        lock (player.PersistenceGate)
            Assert.IsFalse(QuestStarterItemPlan.TryCreate(player, [incoming], out _));
        CollectionAssert.AreEqual(before, overflow.Content.Values.ToArray());
        Assert.IsFalse(incoming.IsPersisted);
        Assert.IsFalse(player.Inventory.HasDirtyEntries);
    }
}
