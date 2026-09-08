namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using AORebirth.Enums;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Extensions.DependencyInjection;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class InventoryActionTests
    {
        static Identity Slot(int placement = 64) => new() { Type = IdentityType.Inventory, Instance = placement };

        [TestMethod]
        public void SplitPreservesQuantityAndOriginalInstanceAndPublishesOnlyAfterCommit()
        {
            using var w = new World();
            Item original = w.Add(11, 10);
            w.Persistence.BeforeCommit = batch =>
            {
                Assert.AreEqual(10, original.StackCount);
                Assert.AreEqual(1, w.Player.Inventory.Inventory.Content.Count);
                Assert.AreEqual(0, w.Session.Messages.Count);
                Assert.AreEqual(11, batch.Stacks.Single().InstanceId);
                Assert.AreEqual(10, batch.Stacks.Single().ExpectedCount);
                Assert.AreEqual(7, batch.Stacks.Single().FinalCount);
                Assert.AreEqual(3, batch.Inserts.Single().StackCount);
            };
            Assert.IsTrue(w.Actions.TrySplit(w.Player, Slot(), 11, 3));
            Assert.AreSame(original, w.Player.Inventory.Inventory.Content[64]);
            Assert.AreEqual(7, original.StackCount);
            Item split = w.Player.Inventory.Inventory.Content[65];
            Assert.AreNotEqual(original.InstanceId, split.InstanceId);
            Assert.AreEqual(3, split.StackCount);
            Assert.IsTrue(split.IsPersisted);
            Assert.AreEqual(10, w.Player.Inventory.Inventory.Content.Values.Sum(i => i.StackCount));
            Assert.AreEqual(10, w.Persistence.Rows.Values.Sum(r => r.StackCount));
        }

        [DataTestMethod]
        [DataRow(0)] [DataRow(-1)] [DataRow(10)] [DataRow(11)] [DataRow(int.MaxValue)]
        public void SplitRejectsInvalidAmountsWithoutAllocatingOrWriting(int amount)
        {
            using var w = new World(); w.Add(11, 10);
            Assert.IsFalse(w.Actions.TrySplit(w.Player, Slot(), 11, amount));
            Assert.AreEqual(0, w.Persistence.Calls);
            Assert.AreEqual(0, w.Ids.Calls);
        }

        [TestMethod]
        public void SplitRejectsFullDestinationCantSplitLockedAndStaleInstances()
        {
            using var w = new World(); Item item = w.Add(11, 10);
            Assert.IsFalse(w.Actions.TrySplit(w.Player, Slot(), 999, 1));
            Assert.IsFalse(w.Actions.TrySplit(w.Player, Slot(999), 11, 1));
            Assert.IsFalse(w.Actions.TrySplit(w.Player, new Identity { Type = IdentityType.Container, Instance = 11 }, 11, 1));
            item.Locked = true;
            Assert.IsFalse(w.Actions.TrySplit(w.Player, Slot(), 11, 1));
            item.Locked = false;
            item.Definition.Stats[CharacterStat.Can] |= (int)CanFlags.CantSplit;
            Assert.IsFalse(w.Actions.TrySplit(w.Player, Slot(), 11, 1));
            item.Definition.Stats[CharacterStat.Can] = (int)CanFlags.Stackable;
            TestWorld.FillInventory(w.Player);
            Assert.IsFalse(w.Actions.TrySplit(w.Player, Slot(), 11, 1));
            Assert.AreEqual(0, w.Persistence.Calls);
        }

        [TestMethod]
        public void RepeatedValidSlotAddressedSplitNeverCreatesQuantity()
        {
            using var w = new World(); w.Add(11, 10);
            Assert.IsTrue(w.Actions.TrySplit(w.Player, Slot(), 11, 3));
            Assert.IsTrue(w.Actions.TrySplit(w.Player, Slot(), 11, 3));
            Assert.AreEqual(10, w.Player.Inventory.Inventory.Content.Values.Sum(i => i.StackCount));
            Assert.AreEqual(10, w.Persistence.Rows.Values.Sum(r => r.StackCount));
            Assert.IsFalse(w.Actions.TrySplit(w.Player, Slot(), 11, 5));
        }

        [TestMethod]
        public void FailedSplitLeavesBothMemoryAndDurableStateUntouched()
        {
            using var w = new World(); Item item = w.Add(11, 10);
            w.Persistence.Failure = new InvalidOperationException("late statement failure");
            Assert.IsFalse(w.Actions.TrySplit(w.Player, Slot(), 11, 3));
            Assert.AreEqual(10, item.StackCount);
            Assert.AreEqual(1, w.Player.Inventory.Inventory.Content.Count);
            Assert.AreEqual(1, w.Persistence.Rows.Count);
            Assert.AreEqual(10, w.Persistence.Rows[11].StackCount);
            Assert.AreEqual(0, w.Session.Messages.Count);
            w.Flush.HardFlush(w.Player);
        }

        [TestMethod]
        public void UnknownMutationCommitQuarantinesAndNeverRetries()
        {
            using var w = new World(); w.Add(11, 10);
            w.Persistence.Failure = new DatabaseCommitOutcomeUnknownException(new Exception("lost commit reply"));
            Assert.IsFalse(w.Actions.TrySplit(w.Player, Slot(), 11, 3));
            Assert.IsTrue(w.Player.IsPersistenceQuarantined);
            Assert.AreEqual(SessionState.Closed, w.Session.State);
            Assert.IsFalse(w.Actions.TrySplit(w.Player, Slot(), 11, 3));
            Assert.AreEqual(1, w.Persistence.Calls);
            Assert.AreEqual(0, w.Session.Messages.Count);
        }

        [TestMethod]
        public void DeleteRetiresCorrectInstanceBeforeAcknowledgingAndDuplicateIsNoOp()
        {
            using var w = new World(); Item original = w.Add(11, 10);
            w.Add(12, 4, 65);
            var request = new CharacterActionMessage { Identity = w.Player.Identity, Action = CharacterActionType.DeleteItem, Target = Slot(), Parameter1 = 123, Parameter2 = 4 };
            w.Persistence.BeforeCommit = _ =>
            {
                Assert.AreSame(original, w.Player.Inventory.Inventory.Content[64]);
                Assert.AreEqual(0, w.Session.Messages.Count);
            };
            w.Actions.Handle(w.Player, request);
            Assert.IsFalse(w.Player.Inventory.Inventory.Content.ContainsKey(64));
            Assert.AreEqual((int)IdentityType.None, w.Persistence.Rows[11].ContainerType);
            Assert.AreEqual(11, w.Persistence.Rows[11].ContainerPlacement);
            Assert.AreEqual(12, w.Player.Inventory.Inventory.Content[65].InstanceId);
            var ack = w.Session.Messages.OfType<CharacterActionMessage>().Single();
            Assert.AreEqual(0, ack.Parameter1); Assert.AreEqual(0, ack.Parameter2);
            Assert.AreEqual(request.Target, ack.Target);
            w.Actions.Handle(w.Player, request);
            Assert.AreEqual(1, w.Persistence.Calls);
            Assert.AreEqual(1, w.Session.Messages.Count);
        }

        [TestMethod]
        public void DeleteRejectsOtherOwnerStaleInstancePermanentKeyAndFailure()
        {
            using var w = new World(); Item item = w.Add(11, 1);
            Player other = TestWorld.CreatePlayer(222); other.Session = new Session();
            Assert.IsFalse(w.Actions.TryDelete(other, Slot(), 11));
            Assert.IsFalse(w.Actions.TryDelete(w.Player, Slot(), 12));
            w.Persistence.Failure = new InvalidOperationException();
            Assert.IsFalse(w.Actions.TryDelete(w.Player, Slot(), 11));
            Assert.AreSame(item, w.Player.Inventory.Inventory.Content[64]);
            w.Flush.HardFlush(w.Player);
            w.Player.Inventory.Inventory.Content[64] = TestWorld.CreateItem(lowId: 226994, instanceId: 99);
            Assert.IsFalse(w.Actions.TryDelete(w.Player, Slot(), 99));
        }

        [TestMethod]
        public void NonemptyUnhydratedBagDeleteRollsBackParentAndPreservesChildren()
        {
            using var w = new World(); Item bag = w.Add(11, 1);
            bag.Identity = new Identity { Type = IdentityType.Container, Instance = 11 };
            var child = TestWorld.CreateItem(instanceId: 12);
            w.Persistence.Rows[12] = InventoryActionService.ToRecord(child, bag.Identity, 0, 1);
            // No backpack page has been hydrated: the durable transaction must reject it.
            Assert.IsFalse(w.Actions.TryDelete(w.Player, Slot(), 11));
            Assert.AreSame(bag, w.Player.Inventory.Inventory.Content[64]);
            Assert.AreEqual((int)IdentityType.Inventory, w.Persistence.Rows[11].ContainerType);
            Assert.AreEqual(11, w.Persistence.Rows[12].ContainerInstance);
            Assert.AreEqual(0, w.Session.Messages.Count);
            w.Flush.HardFlush(w.Player);
        }

        [TestMethod]
        public void EmptyBagDeleteSucceedsButKnownNonemptyBagNeverWrites()
        {
            using var w = new World(); Item bag = w.Add(11, 1);
            bag.Identity = new Identity { Type = IdentityType.Container, Instance = 11 };
            Container content = w.Player.Inventory.GetOrCreateBackpackPage(bag, bag.Identity, Slot());
            content.Add(0, TestWorld.CreateItem(instanceId: 12));
            Assert.IsFalse(w.Actions.TryDelete(w.Player, Slot(), 11));
            Assert.AreEqual(0, w.Persistence.Calls);
            content.Content.Clear();
            Assert.IsTrue(w.Actions.TryDelete(w.Player, Slot(), 11));
            Assert.AreEqual((int)IdentityType.None, w.Persistence.Rows[11].ContainerType);
            Assert.AreEqual(0, w.Player.Inventory.Inventory.Content.Count);
        }

        [TestMethod]
        public void MovePacketCommitsBeforeAckAndRejectsDuplicateSource()
        {
            using var w = new World(); Item item = w.Add(11, 5);
            var moves = new InventoryMoveService(new StubLogger(), w.Flush, w.Actions);
            var message = new ClientMoveItemToInventoryMessage { SourceContainer = Slot(), TargetPlacement = 65 };
            w.Persistence.BeforeCommit = _ => Assert.AreSame(item, w.Player.Inventory.Inventory.Content[64]);
            moves.Handle(w.Player, message);
            Assert.AreSame(item, w.Player.Inventory.Inventory.Content[65]);
            Assert.AreEqual(65, w.Persistence.Rows[11].ContainerPlacement);
            Assert.AreEqual(65, w.Session.Messages.OfType<ContainerAddItemMessage>().Single().TargetPlacement);
            moves.Handle(w.Player, message);
            Assert.AreEqual(1, w.Persistence.Calls);
        }

        [TestMethod]
        public void FailedMoveDoesNotPublishOrLoseOriginalSlot()
        {
            using var w = new World(); Item item = w.Add(11, 5);
            w.Persistence.Failure = new InvalidOperationException();
            new InventoryMoveService(new StubLogger(), w.Flush, w.Actions).Handle(w.Player,
                new ClientMoveItemToInventoryMessage { SourceContainer = Slot(), TargetPlacement = 65 });
            Assert.AreSame(item, w.Player.Inventory.Inventory.Content[64]);
            Assert.IsFalse(w.Player.Inventory.Inventory.Content.ContainsKey(65));
            Assert.AreEqual(0, w.Session.Messages.Count);
            w.Flush.HardFlush(w.Player);
        }

        [TestMethod]
        public void BankToOwnedBackpackKeepsInstanceAndRejectsUnownedDestination()
        {
            using var w = new World(); Item bag = w.Add(21, 1);
            bag.Identity = new Identity { Type = IdentityType.Container, Instance = bag.InstanceId };
            Container backpack = w.Player.Inventory.GetOrCreateBackpackPage(bag, bag.Identity, Slot());
            backpack.IsHydrated = true;
            Item stored = TestWorld.CreateItem(instanceId: 22);
            Container bank = w.Player.Inventory.Bank; bank.IsHydrated = true;
            bank.Add(bank.Offset, stored);
            w.Persistence.Rows[22] = InventoryActionService.ToRecord(stored, bank.Identity, bank.Offset, 1);
            var moves = new InventoryMoveService(new StubLogger(), w.Flush, w.Actions);
            moves.Handle(w.Player, new ClientContainerAddItemMessage
            {
                Source = new Identity { Type = IdentityType.BankByRef, Instance = bank.Offset },
                Target = new Identity { Type = IdentityType.Container, Instance = 999 }
            });
            Assert.AreEqual(0, w.Persistence.Calls);
            moves.Handle(w.Player, new ClientContainerAddItemMessage
            {
                Source = new Identity { Type = IdentityType.BankByRef, Instance = bank.Offset }, Target = bag.Identity
            });
            Assert.AreSame(stored, backpack.Content[0]);
            Assert.AreEqual(22, w.Persistence.Rows[22].InstanceId);
            Assert.AreEqual(21, w.Persistence.Rows[22].ContainerInstance);
            Assert.AreEqual((int)IdentityType.Container, w.Persistence.Rows[22].ContainerType);
            Assert.AreEqual(0, bank.Content.Count);
            Assert.AreEqual(1, w.Session.Messages.OfType<ContainerAddItemMessage>().Count());
        }

        [TestMethod]
        public void StoredStackCountSurvivesHydrationInsteadOfTemplateDefault()
        {
            var catalog = new StubCatalog().Add(1000, 1);
            catalog.Require(1000).Stats[CharacterStat.MultipleCount] = 50;
            var builder = new ItemBuilder(catalog, new StubLogger());
            var row = new ItemInstanceRecord { InstanceId = 31, ItemType = (int)IdentityType.WeaponInstance, LowId = 1000, HighId = 1000, Quality = 1, StackCount = 7 };
            Assert.IsTrue(builder.TryFromInstanceRecord(row, out Item item));
            Assert.AreEqual(7, item.StackCount);
            Assert.AreEqual(31, item.InstanceId);
            Assert.IsTrue(item.IsPersisted);
            Assert.IsFalse(builder.TryFromInstanceRecord(new ItemInstanceRecord { InstanceId = 32, LowId = 1000, HighId = 1000, Quality = 1, StackCount = 0 }, out _));
        }

        [TestMethod]
        public void DisconnectRejectsMutationAndReconnectRetainsAuthoritativeStack()
        {
            using var w = new World(); w.Add(11, 10);
            w.Session.Close();
            Assert.IsFalse(w.Actions.TrySplit(w.Player, Slot(), 11, 3));
            Assert.AreEqual(0, w.Persistence.Calls);
            w.Session.State = SessionState.InPlay;
            Assert.IsTrue(w.Actions.TrySplit(w.Player, Slot(), 11, 3));
            var builder = new ItemBuilder(new StubCatalog().Add(1000, 1), new StubLogger());
            var reloaded = TestWorld.CreatePlayer(111);
            reloaded.Inventory.Apply(new ZoneEngine_New.Core.Characters.CharacterHydrationResult { Items = w.Persistence.Rows.Values.ToArray() }, 111, builder);
            Assert.AreEqual(10, reloaded.Inventory.Inventory.Content.Values.Sum(i => i.StackCount));
            Assert.AreEqual(7, reloaded.Inventory.Inventory.Content[64].StackCount);
        }

        [TestMethod]
        public void NanoCrystalConsumesOneAndCommitsProgramTogether()
        {
            using var w = new World(); Item item = w.Add(11, 2);
            item.Definition.Stats[CharacterStat.Can] |= (int)(CanFlags.Consume | CanFlags.Use);
            item.SpellList[EventType.OnUse] = new List<ItemSpell> { new() { FunctionType = (int)FunctionType.UploadNano, Target = (int)ItemTarget.User, Arguments = new List<object> { 12345 } } };
            w.Persistence.BeforeCommit = batch =>
            {
                Assert.AreEqual(2, item.StackCount);
                CollectionAssert.AreEqual(new[] { 12345 }, batch.UploadedNanoIds.ToArray());
                Assert.AreEqual(1, batch.Stacks.Single().FinalCount);
                Assert.AreEqual(0, w.Session.Messages.Count);
            };
            Assert.IsTrue(w.Actions.TryUseNanoCrystal(w.Player, Slot(), item));
            Assert.AreEqual(1, item.StackCount);
            Assert.AreEqual(3, ((TemplateActionMessage)w.Session.Messages[0]).Unknown2);
            Assert.IsTrue(w.Session.Messages.OfType<CharacterActionMessage>().Any(m => m.Action == CharacterActionType.UploadNano));
            w.Persistence.BeforeCommit = null;
            w.Persistence.Failure = new InvalidOperationException();
            int messagesBeforeFailure = w.Session.Messages.Count;
            Assert.IsFalse(w.Actions.TryUseNanoCrystal(w.Player, Slot(), item));
            Assert.AreSame(item, w.Player.Inventory.Inventory.Content[64]);
            Assert.AreEqual(1, item.StackCount);
            Assert.AreEqual(2, w.Persistence.Calls);
            Assert.AreEqual(messagesBeforeFailure, w.Session.Messages.Count);
            w.Flush.HardFlush(w.Player);
        }

        [TestMethod]
        public void UnsupportedCompoundUseIsRejectedBeforeEarlierEffectRuns()
        {
            using var w = new World(); Item item = w.Add(11, 1);
            item.SpellList[EventType.OnUse] = new List<ItemSpell>
            {
                new() { FunctionType = (int)FunctionType.Set, Target = (int)ItemTarget.User, Arguments = new List<object> { (int)CharacterStat.Cash, 999 } },
                new() { FunctionType = int.MaxValue, Target = (int)ItemTarget.User }
            };
            w.Player.Stats.Set(CharacterStat.Cash, 100);
            Assert.IsFalse(item.Definition.ExecuteOnUseSpells(w.Player, new RejectingInventory(), new StubItemBuilder()));
            Assert.AreEqual(100, w.Player.Stats.GetOrZero(CharacterStat.Cash));
            Assert.AreEqual(0, w.Session.Messages.Count);
            Assert.IsFalse(ItemTemplate.EvaluateRequirement(100, new ItemRequirement { Operator = int.MaxValue, Value = 1 }));
        }

        [TestMethod]
        public void MissionGrantPlanIsPureCapacityCheckedAndPublishedOnce()
        {
            using var w = new World(); Item reward = TestWorld.CreateItem(instanceId: 88, persisted: false);
            Assert.ThrowsExactly<InvalidOperationException>(() => InventoryGrantPlan.TryCreate(w.Player, new[] { reward }, out _));
            lock (w.Player.PersistenceGate)
            {
                Assert.IsTrue(InventoryGrantPlan.TryCreate(w.Player, new[] { reward }, out var plan));
                Assert.AreEqual(0, w.Persistence.Calls);
                Assert.AreEqual(0, w.Player.Inventory.Inventory.Content.Count);
                Assert.AreEqual(88, plan.Rows.Single().InstanceId);
                Assert.AreEqual(w.Player.Identity.Instance, plan.Rows.Single().ContainerInstance);
                // The mission transaction is intentionally owned by the mission DAO, not this planner.
                Assert.IsTrue(plan.PublishAfterCommit());
                Assert.IsFalse(plan.PublishAfterCommit());
            }
            Assert.AreEqual(1, w.Session.Messages.OfType<AddTemplateMessage>().Count());
            TestWorld.FillInventory(w.Player);
            lock (w.Player.PersistenceGate)
                Assert.IsFalse(InventoryGrantPlan.TryCreate(w.Player, new[] { TestWorld.CreateItem(instanceId: 89, persisted: false) }, out _));
        }

        [TestMethod]
        public void MissionGrantCanPublishSilentlyForItsOwnCapturedPacketContract()
        {
            using var w = new World(); Item reward = TestWorld.CreateItem(instanceId: 88, persisted: false);
            lock (w.Player.PersistenceGate)
            {
                Assert.IsTrue(InventoryGrantPlan.TryCreate(w.Player, new[] { reward }, out var plan));
                Assert.IsTrue(plan.PublishAfterCommit(notify: false));
                Assert.IsFalse(plan.PublishAfterCommit());
            }
            Assert.AreSame(reward, w.Player.Inventory.Inventory.Content[64]);
            Assert.IsTrue(reward.IsPersisted);
            Assert.AreEqual(0, w.Session.Messages.Count);
        }

        [TestMethod]
        public void QuabbitExchangeCommitsBothRowsThenSendsCapturedPacketOrder()
        {
            using var w = new World(); Item package = w.Add(11, 1, lowId: 301782);
            w.Persistence.BeforeCommit = batch =>
            {
                Assert.AreSame(package, w.Player.Inventory.Inventory.Content[64]);
                Assert.AreEqual(0, w.Session.Messages.Count);
                Assert.AreEqual(301749, batch.Inserts.Single().LowId);
                Assert.AreEqual((int)IdentityType.Inventory, batch.Inserts.Single().ContainerType);
                Assert.AreEqual((int)IdentityType.None, batch.Locations.Single().ContainerType);
            };
            Assert.IsTrue(w.Actions.TryOpenQuabbit(w.Player, Slot(), package));
            Assert.AreEqual(301749, w.Player.Inventory.Inventory.Content.Values.Single().LowId);
            Assert.AreEqual(4, w.Session.Messages.Count);
            var grant = (TemplateActionMessage)w.Session.Messages[0];
            Assert.AreEqual(301749, grant.ItemLowId); Assert.AreEqual(87, grant.Unknown2);
            Assert.AreEqual(IdentityType.OverflowWindow, grant.Placement.Type);
            Assert.AreEqual(0x6f, ((ContainerAddItemMessage)w.Session.Messages[1]).TargetPlacement);
            var consume = (TemplateActionMessage)w.Session.Messages[2];
            Assert.AreEqual(301782, consume.ItemLowId); Assert.AreEqual(3, consume.Unknown2);
            Assert.AreEqual(50000, consume.Unknown3); Assert.AreEqual(w.Player.Identity.Instance, consume.Unknown4);
            Assert.AreEqual(CharacterActionType.DeleteItem, ((CharacterActionMessage)w.Session.Messages[3]).Action);
            Assert.IsFalse(w.Actions.TryOpenQuabbit(w.Player, Slot(), package));
            Assert.AreEqual(1, w.Persistence.Calls);
        }

        [TestMethod]
        public void QuabbitFailureOrFullInventoryNeverConsumesOrAcknowledgesPackage()
        {
            using var w = new World(); Item package = w.Add(11, 1, lowId: 301782);
            w.Persistence.Failure = new InvalidOperationException("grant write failure");
            Assert.IsFalse(w.Actions.TryOpenQuabbit(w.Player, Slot(), package));
            Assert.AreSame(package, w.Player.Inventory.Inventory.Content[64]);
            Assert.AreEqual(0, w.Session.Messages.Count);
            Assert.AreEqual(1, w.Persistence.Rows.Count);
            w.Flush.HardFlush(w.Player);
            TestWorld.FillInventory(w.Player);
            Assert.IsFalse(w.Actions.TryOpenQuabbit(w.Player, Slot(), package));
            Assert.AreEqual(1, w.Persistence.Calls);
        }

        [TestMethod]
        public void AlreadyOwnedQuabbitConsumesSealedWithoutDuplicatingOpenedItem()
        {
            using var w = new World(); Item package = w.Add(11, 1, lowId: 301782);
            w.Add(12, 1, 65, 301749);
            Assert.IsTrue(w.Actions.TryOpenQuabbit(w.Player, Slot(), package));
            Assert.AreEqual(1, w.Player.Inventory.Inventory.Content.Count);
            Assert.AreEqual(2, w.Session.Messages.Count);
            Assert.AreEqual(301782, ((TemplateActionMessage)w.Session.Messages[0]).ItemLowId);
        }

        [TestMethod]
        public void StimCommitsVitalRestoreAndConsumptionBeforePacketsAndEnforcesSkillDeadline()
        {
            using var w = new World(); Item stim = w.Add(11, 2, lowId: 291043);
            var clock = new TestClock(); w.Actions.Clock = clock;
            SetVitals(w.Player, 20, 100, 10, 80);
            w.Persistence.BeforeCommit = batch =>
            {
                Assert.AreEqual(2, stim.StackCount);
                Assert.AreEqual(20, w.Player.Stats.GetOrZero(CharacterStat.Health));
                Assert.AreEqual(0, w.Session.Messages.Count);
                Assert.AreEqual(1, batch.Stacks.Single().FinalCount);
                Assert.AreEqual(50, batch.FinalStats.Single(s => s.StatId == (int)CharacterStat.Health).StatValue);
                Assert.AreEqual(40, batch.FinalStats.Single(s => s.StatId == (int)CharacterStat.CurrentNano).StatValue);
            };
            Assert.IsTrue(w.Actions.TryUseVitalItem(w.Player, Slot(), stim));
            Assert.AreEqual(1, stim.StackCount);
            Assert.AreEqual(50, w.Player.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(40, w.Player.Stats.GetOrZero(CharacterStat.CurrentNano));
            Assert.AreEqual(50, w.Persistence.Stats[(int)CharacterStat.Health]);
            var locked = w.Session.Messages.OfType<CharacterActionMessage>().Single();
            Assert.AreEqual(CharacterActionType.SpecialUnavailable, locked.Action);
            Assert.AreEqual((int)CharacterStat.FirstAid, locked.Parameter1); Assert.AreEqual(40, locked.Parameter2);
            Assert.IsFalse(w.Actions.TryUseVitalItem(w.Player, Slot(), stim));
            Assert.AreEqual(1, w.Persistence.Calls);
            clock.Advance(39); w.Actions.Tick(w.Player.Playfield!);
            Assert.IsFalse(w.Session.Messages.OfType<CharacterActionMessage>().Any(m => m.Action == CharacterActionType.SpecialAvailable));
            clock.Advance(1); w.Actions.Tick(w.Player.Playfield!);
            var available = w.Session.Messages.OfType<CharacterActionMessage>().Single(m => m.Action == CharacterActionType.SpecialAvailable);
            Assert.AreEqual(0, available.Parameter1); Assert.AreEqual((int)CharacterStat.FirstAid, available.Parameter2);
            w.Persistence.BeforeCommit = null;
            Assert.IsTrue(w.Actions.TryUseVitalItem(w.Player, Slot(), stim));
            Assert.IsFalse(w.Player.Inventory.Inventory.Content.ContainsKey(64));
            Assert.AreEqual((int)IdentityType.None, w.Persistence.Rows[11].ContainerType);
        }

        [TestMethod]
        public void RechargerUsesDeclaredAmountsAndDurationWithoutConsumingStack()
        {
            using var w = new World(); Item recharger = w.Add(11, 50, lowId: 291082);
            SetVitals(w.Player, 90, 100, 70, 80);
            recharger.SpellList[EventType.OnUse] = new List<ItemSpell>
            {
                new() { FunctionType = (int)FunctionType.Hit, Arguments = new List<object> { (int)CharacterStat.Health, 37 } },
                new() { FunctionType = (int)FunctionType.Hit, Arguments = new List<object> { (int)CharacterStat.CurrentNano, 43 } },
                new() { FunctionType = (int)FunctionType.LockSkill, Arguments = new List<object> { (int)CharacterStat.Treatment, 19 } }
            };
            Assert.IsTrue(w.Actions.TryUseVitalItem(w.Player, Slot(), recharger));
            Assert.AreEqual(50, recharger.StackCount);
            Assert.AreEqual(50, w.Persistence.Rows[11].StackCount);
            Assert.AreEqual(100, w.Player.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(80, w.Player.Stats.GetOrZero(CharacterStat.CurrentNano));
            Assert.AreEqual(19, w.Session.Messages.OfType<CharacterActionMessage>().Single().Parameter2);
            Assert.IsFalse(w.Actions.TryUseVitalItem(w.Player, Slot(), recharger));
        }

        [TestMethod]
        public void FailedStimPreservesVitalsQuantityAndLockAvailability()
        {
            using var w = new World(); Item stim = w.Add(11, 1, lowId: 291043);
            SetVitals(w.Player, 20, 100, 10, 80);
            w.Persistence.Failure = new InvalidOperationException("late vital stat failure");
            Assert.IsFalse(w.Actions.TryUseVitalItem(w.Player, Slot(), stim));
            Assert.AreEqual(1, stim.StackCount);
            Assert.AreEqual(20, w.Player.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(10, w.Player.Stats.GetOrZero(CharacterStat.CurrentNano));
            Assert.AreEqual(0, w.Persistence.Stats.Count);
            Assert.AreEqual((int)IdentityType.Inventory, w.Persistence.Rows[11].ContainerType);
            Assert.AreEqual(0, w.Session.Messages.Count);
            w.Flush.HardFlush(w.Player);
            w.Persistence.Failure = null;
            Assert.IsTrue(w.Actions.TryUseVitalItem(w.Player, Slot(), stim));
        }

        static void SetVitals(Player player, int health, int maxHealth, int nano, int maxNano)
        {
            player.Stats.Set(CharacterStat.MaxHealth, maxHealth);
            player.Stats.Set(CharacterStat.Health, health);
            player.Stats.Set(CharacterStat.MaxNanoEnergy, maxNano);
            player.Stats.Set(CharacterStat.CurrentNano, nano);
        }

        sealed class TestClock : TimeProvider
        {
            DateTimeOffset _now = DateTimeOffset.UnixEpoch;
            public override DateTimeOffset GetUtcNow() => _now;
            public void Advance(int seconds) => _now = _now.AddSeconds(seconds);
        }

        sealed class World : IDisposable
        {
            public readonly Player Player = TestWorld.CreatePlayer(111);
            public readonly Session Session = new();
            public readonly Persistence Persistence = new();
            public readonly Ids Ids = new();
            public readonly InventoryFlushService Flush;
            public readonly InventoryActionService Actions;
            readonly ServiceProvider _services;
            public World()
            {
                Player.Session = Session; Session.BindPlayer(Player);
                Player.Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
                _services = new ServiceCollection().AddSingleton(new PlayfieldLocality(4582, null)).BuildServiceProvider();
                typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Player.Playfield, _services);
                var manager = (PlayfieldManager)RuntimeHelpers.GetUninitializedObject(typeof(PlayfieldManager));
                typeof(PlayfieldManager).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, new Lock());
                typeof(PlayfieldManager).GetField("_playersByCharacterId", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, new Dictionary<int, Player>());
                Flush = new InventoryFlushService(new Lazy<PlayfieldManager>(() => manager), new Coalesce(), new StubLogger());
                Actions = new InventoryActionService(Persistence, Flush, Ids, new StubLogger(),
                    new StubCatalog().Add(301749, 1), new StubItemBuilder());
            }
            public Item Add(int id, int count, int slot = 64, int lowId = 1000)
            {
                Item item = TestWorld.CreateItem(instanceId: id, lowId: lowId, highId: lowId);
                item.StackCount = count;
                item.Definition.Stats[CharacterStat.Can] = (int)CanFlags.Stackable;
                Player.Inventory.Inventory.Add(slot, item);
                Persistence.Rows[id] = InventoryActionService.ToRecord(item, Player.Inventory.Inventory.Identity, slot, count);
                return item;
            }
            public void Dispose() { Flush.Dispose(); _services.Dispose(); }
        }
        sealed class Ids : IItemInstanceIdAllocator { int _next = 100; public int Calls; public int Allocate() { Calls++; return _next++; } }
        sealed class Coalesce : ICharacterCoalesceCommit
        {
            public void Persist(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates, int characterId, IReadOnlyList<int> uploadedNanoIds) => throw new InvalidOperationException("No unexpected independent flush.");
        }
        sealed class Persistence : IInventoryMutationPersistence
        {
            public Dictionary<int, ItemInstanceRecord> Rows = new();
            public Dictionary<int, int> Stats = new();
            public int Calls;
            public Exception? Failure;
            public Action<InventoryMutationBatch>? BeforeCommit;
            public void Persist(InventoryMutationBatch batch)
            {
                Calls++; BeforeCommit?.Invoke(batch);
                var next = new Dictionary<int, ItemInstanceRecord>(Rows);
                var nextStats = new Dictionary<int, int>(Stats);
                foreach (var row in batch.Inserts) next.Add(row.InstanceId, row);
                foreach (var location in batch.Locations)
                {
                    var row = next[location.InstanceId];
                    next[location.InstanceId] = Copy(row, location.ContainerType, location.ContainerInstance, location.ContainerPlacement, row.StackCount);
                }
                foreach (var stack in batch.Stacks)
                {
                    var row = next[stack.InstanceId];
                    if (row.StackCount != stack.ExpectedCount) throw new InvalidOperationException("stale stack");
                    next[stack.InstanceId] = Copy(row, row.ContainerType, row.ContainerInstance, row.ContainerPlacement, stack.FinalCount);
                }
                foreach (int container in batch.EmptyContainersBeforeRetire)
                    if (next.Values.Any(row => row.ContainerType == (int)IdentityType.Container && row.ContainerInstance == container))
                        throw new InvalidOperationException("nonempty container");
                foreach (StatRecord stat in batch.FinalStats) nextStats[stat.StatId] = stat.StatValue;
                if (Failure != null) throw Failure;
                Rows = next;
                Stats = nextStats;
            }
            static ItemInstanceRecord Copy(ItemInstanceRecord r, int type, int owner, int placement, int count) => new()
            {
                InstanceId = r.InstanceId, ContainerType = type, ContainerInstance = owner, ContainerPlacement = placement,
                ItemType = r.ItemType, LowId = r.LowId, HighId = r.HighId, Quality = r.Quality, StackCount = count, Source = r.Source
            };
        }
        sealed class Session : IZoneSession
        {
            public readonly List<MessageBody> Messages = new();
            public SessionState State { get; set; } = SessionState.InPlay;
            public Player? Player { get; private set; }
            public void BindPlayer(Player player) => Player = player;
            public void UnbindPlayer() => Player = null;
            public void TransferToPlayfield(Playfield destination, Vector3 landing) => throw new NotSupportedException();
            public void Send(byte[] packet) => throw new NotSupportedException();
            public void Send(Message message) => throw new NotSupportedException();
            public void Send(MessageBody message) => Messages.Add(message);
            public void Send(MessageBody message, int sender, int receiver) => Messages.Add(message);
            public void SendInitiateCompression() => throw new NotSupportedException();
            public void Close() => State = SessionState.Closed;
        }
        sealed class RejectingInventory : IInventoryRepository
        {
            public IReadOnlyList<ItemInstanceRecord> GetCarriedItems(int id) => throw new NotSupportedException();
            public IReadOnlyList<ItemInstanceRecord> GetBankItems(int id) => throw new NotSupportedException();
            public IReadOnlyList<ItemInstanceRecord> GetContainerItems(int id) => throw new NotSupportedException();
            public int LeaseInstanceIdBlock(int count) => throw new NotSupportedException();
            public ItemInstanceRecord Insert(ItemInstanceRecord item) => throw new NotSupportedException();
            public void UpdateLocation(int id, int type, int owner, int placement) => throw new NotSupportedException();
            public void UpdateLocations(IReadOnlyList<ItemLocationUpdate> updates) => throw new NotSupportedException();
            public void PersistNewAndUpdateLocations(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates) => throw new NotSupportedException();
        }
    }
}
