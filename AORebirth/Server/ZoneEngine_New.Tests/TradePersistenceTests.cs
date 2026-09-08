namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Trade;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class TradePersistenceTests
    {
        [TestMethod]
        public void PlayerTradeCommitsBothOwnersAndCashBeforeAnyGrantOrCompletion()
        {
            using var world = new Fixture();
            Item firstItem = TestWorld.CreateItem(instanceId: 31);
            Item secondItem = TestWorld.CreateItem(instanceId: 32, persisted: false);
            world.Trade.InitiatorOffer.Add(firstItem);
            world.Trade.PartnerOffer.Add(secondItem);
            world.Trade.InitiatorOffer.Credits = 25;
            world.Trade.PartnerOffer.Credits = 10;
            world.Persistence.BeforeCommit = batch =>
            {
                Assert.AreEqual(100, world.First.Stats.GetOrZero(CharacterStat.Cash));
                Assert.AreEqual(200, world.Second.Stats.GetOrZero(CharacterStat.Cash));
                Assert.AreEqual(1, world.Trade.InitiatorOffer.Count);
                Assert.AreEqual(1, world.Trade.PartnerOffer.Count);
                Assert.AreEqual(0, world.First.Inventory.Inventory.Content.Count);
                Assert.AreEqual(0, world.Second.Inventory.Inventory.Content.Count);
                Assert.AreEqual(0, world.FirstSession.Messages.Count + world.SecondSession.Messages.Count);
                Assert.AreEqual(31, batch.Updates.Single().InstanceId);
                Assert.AreEqual(world.Second.Identity.Instance, batch.Updates.Single().ContainerInstance);
                Assert.AreEqual(32, batch.Inserts.Single().InstanceId);
                Assert.AreEqual(world.First.Identity.Instance, batch.Inserts.Single().ContainerInstance);
                CollectionAssert.AreEquivalent(new[] { 85, 215 }, batch.Characters.Select(c => c.Cash).ToArray());
            };
            world.Commit();
            Assert.AreSame(secondItem, world.First.Inventory.Inventory.Content.Values.Single());
            Assert.AreSame(firstItem, world.Second.Inventory.Inventory.Content.Values.Single());
            Assert.IsTrue(secondItem.IsPersisted);
            Assert.AreEqual(85, world.First.Stats.GetOrZero(CharacterStat.Cash));
            Assert.AreEqual(215, world.Second.Stats.GetOrZero(CharacterStat.Cash));
            Assert.IsTrue(world.FirstSession.Messages.OfType<AddTemplateMessage>().Any());
            Assert.IsTrue(world.FirstSession.Messages.OfType<TradeMessage>().Any(m => m.Action == TradeAction.Complete));
            Assert.AreEqual(0, world.Trade.InitiatorOffer.Count + world.Trade.PartnerOffer.Count);
            Assert.IsFalse(world.First.Inventory.HasDirtyEntries || world.Second.Inventory.HasDirtyEntries);
            world.Commit();
            Assert.AreEqual(1, world.Persistence.Calls, "A duplicate final packet cannot replay the transaction.");
        }

        [TestMethod]
        public void FailedCommitPreservesOfferLocationsCashAndDirtyRowsWithoutCompletion()
        {
            using var world = new Fixture();
            Item item = TestWorld.CreateItem(instanceId: 33, persisted: false);
            world.Trade.InitiatorOffer.Add(item);
            world.First.Inventory.MarkDirty(item, world.First.Inventory.Inventory, 64);
            world.Trade.InitiatorOffer.Credits = 25;
            world.Persistence.BeforeCommit = _ => throw new InvalidOperationException("simulated statement failure");
            world.Commit();
            Assert.AreSame(item, world.Trade.InitiatorOffer.Items.Single().Value);
            Assert.IsTrue(item.Locked);
            Assert.IsFalse(item.IsPersisted);
            Assert.AreEqual(100, world.First.Stats.GetOrZero(CharacterStat.Cash));
            Assert.AreEqual(200, world.Second.Stats.GetOrZero(CharacterStat.Cash));
            Assert.AreEqual(0, world.Second.Inventory.Inventory.Content.Count);
            Assert.IsTrue(world.First.Inventory.HasDirtyEntries);
            Assert.IsFalse(world.Trade.Committing);
            AssertNoGrantsOrComplete(world.FirstSession, world.SecondSession);
            world.Flush.HardFlush(world.First); // Cancels the retry schedule and verifies dirty restoration.
            Assert.AreEqual(1, world.Coalesce.Calls);
        }

        [TestMethod]
        public void PendingNewRowsAreMergedAtTheirFinalOwnerNotInsertedTwice()
        {
            using var world = new Fixture();
            Item offered = TestWorld.CreateItem(instanceId: 34, persisted: false);
            world.First.Inventory.MarkDirty(offered, world.First.Inventory.Inventory, 64);
            world.Trade.InitiatorOffer.Add(offered);
            world.Persistence.BeforeCommit = batch =>
            {
                Assert.AreEqual(1, batch.Inserts.Count);
                Assert.AreEqual(world.Second.Identity.Instance, batch.Inserts[0].ContainerInstance);
            };
            world.Commit();
            Assert.AreEqual(0, world.Coalesce.Calls, "There must be no independent preflush before the atomic trade.");
            Assert.IsTrue(offered.IsPersisted);
            Assert.IsFalse(world.First.Inventory.HasDirtyEntries);
        }

        [TestMethod]
        public void UnknownCommitQuarantinesBothPlayersAndNeverQueuesAReplay()
        {
            using var world = new Fixture();
            Item item = TestWorld.CreateItem(instanceId: 35, persisted: false);
            world.First.Inventory.MarkDirty(item, world.First.Inventory.Inventory, 64);
            world.Trade.InitiatorOffer.Add(item);
            world.Persistence.BeforeCommit = _ => throw new DatabaseCommitOutcomeUnknownException(new Exception("connection lost"));
            world.Commit();
            Assert.IsTrue(world.First.IsPersistenceQuarantined && world.Second.IsPersistenceQuarantined);
            Assert.AreEqual(SessionState.Closed, world.FirstSession.State);
            Assert.AreEqual(SessionState.Closed, world.SecondSession.State);
            Assert.IsFalse(world.First.Inventory.HasDirtyEntries);
            Assert.AreEqual(0, world.Coalesce.Calls);
            AssertNoGrantsOrComplete(world.FirstSession, world.SecondSession);
        }

        [TestMethod]
        public void WriteBehindCannotPassParticipantGateDuringTrade()
        {
            using var world = new Fixture();
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var flushStarted = new ManualResetEventSlim();
            Item item = TestWorld.CreateItem(instanceId: 36, persisted: false);
            world.First.Inventory.MarkDirty(item, world.First.Inventory.Inventory, 64);
            Task trade = Task.Run(() => world.Flush.WithExclusivePlayers(world.Second, world.First, () =>
            {
                entered.Set();
                Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
            }));
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            Task flush = Task.Run(() => { flushStarted.Set(); world.Flush.HardFlush(world.First); });
            Assert.IsTrue(flushStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsFalse(flush.Wait(TimeSpan.FromMilliseconds(100)), "The writer must wait on the participant's gate, not just see an empty dirty set.");
            Assert.AreEqual(0, world.Coalesce.Calls);
            release.Set();
            Assert.IsTrue(Task.WaitAll(new[] { trade, flush }, TimeSpan.FromSeconds(5)));
            Assert.AreEqual(1, world.Coalesce.Calls);
            Assert.IsFalse(world.First.Inventory.HasDirtyEntries);
        }

        [TestMethod]
        public void UnknownWriteBehindCommitQuarantinesWithoutRestoringDirtyWrites()
        {
            using var world = new Fixture();
            Item item = TestWorld.CreateItem(instanceId: 39, persisted: false);
            world.First.Inventory.MarkDirty(item, world.First.Inventory.Inventory, 64);
            world.Coalesce.Failure = new DatabaseCommitOutcomeUnknownException(new Exception("connection lost"));
            Assert.ThrowsExactly<DatabaseCommitOutcomeUnknownException>(() => world.Flush.HardFlush(world.First));
            Assert.IsTrue(world.First.IsPersistenceQuarantined);
            Assert.AreEqual(SessionState.Closed, world.FirstSession.State);
            Assert.IsFalse(world.First.Inventory.HasDirtyEntries);
            Assert.AreEqual(1, world.Coalesce.Calls);
        }

        [TestMethod]
        public void ShopRetirementPurchaseAndCashCommitBeforeAcknowledgement()
        {
            using var world = new Fixture();
            TradeSession shop = world.CreateShop();
            Item sold = TestWorld.CreateItem(instanceId: 37);
            shop.InitiatorOffer.Add(sold);
            shop.AddShopPick(0);
            world.Persistence.BeforeCommit = batch =>
            {
                Assert.AreEqual(1, batch.Characters.Count);
                Assert.AreEqual(1, batch.Inserts.Count);
                Assert.AreEqual(world.First.Identity.Instance, batch.Inserts.Single().ContainerInstance);
                Assert.AreEqual(37, batch.Updates.Single().InstanceId);
                Assert.AreEqual(shop.Machine!.Identity.Instance, batch.Updates.Single().ContainerInstance);
                Assert.AreEqual(50, batch.Characters.Single().Cash);
                Assert.AreEqual(100, world.First.Stats.GetOrZero(CharacterStat.Cash));
                Assert.AreEqual(1, shop.InitiatorOffer.Count);
                Assert.AreEqual(0, world.FirstSession.Messages.Count);
            };
            world.CommitShop(shop);
            Assert.AreEqual(50, world.First.Stats.GetOrZero(CharacterStat.Cash));
            Assert.AreEqual(0, shop.InitiatorOffer.Count);
            Assert.IsTrue(world.First.Inventory.Inventory.Content.Values.Single().IsPersisted);
            Assert.IsTrue(world.FirstSession.Messages.OfType<TradeMessage>().Any(m => m.Action == TradeAction.Complete));
        }

        [TestMethod]
        public void FailedShopCommitRestoresSaleOfferAndChargesNothing()
        {
            using var world = new Fixture();
            TradeSession shop = world.CreateShop();
            Item sold = TestWorld.CreateItem(instanceId: 38);
            shop.InitiatorOffer.Add(sold);
            shop.AddShopPick(0);
            world.Persistence.BeforeCommit = _ => throw new InvalidOperationException("simulated cash write failure");
            world.CommitShop(shop);
            Assert.AreSame(sold, shop.InitiatorOffer.Items.Single().Value);
            Assert.IsTrue(sold.Locked);
            Assert.AreEqual(100, world.First.Stats.GetOrZero(CharacterStat.Cash));
            Assert.AreEqual(0, world.First.Inventory.Inventory.Content.Count);
            Assert.IsFalse(shop.Committing);
            AssertNoGrantsOrComplete(world.FirstSession);
            world.Flush.HardFlush(world.First);
        }

        [TestMethod]
        public void ShopNeverChargesForAnItemThatCouldOnlyLandInMemoryOnlyOverflow()
        {
            using var world = new Fixture();
            TradeSession shop = world.CreateShop();
            shop.AddShopPick(0);
            TestWorld.FillInventory(world.First);
            world.CommitShop(shop);
            Assert.AreEqual(0, world.Persistence.Calls);
            Assert.AreEqual(100, world.First.Stats.GetOrZero(CharacterStat.Cash));
            Assert.AreEqual(0, world.First.Inventory.Overflow.Content.Count);
            AssertNoGrantsOrComplete(world.FirstSession);
        }

        static void AssertNoGrantsOrComplete(params RecordingSession[] sessions)
        {
            Assert.IsFalse(sessions.SelectMany(s => s.Messages).OfType<AddTemplateMessage>().Any());
            Assert.IsFalse(sessions.SelectMany(s => s.Messages).OfType<TradeMessage>().Any(m => m.Action == TradeAction.Complete));
            Assert.IsFalse(sessions.SelectMany(s => s.Messages).OfType<StatMessage>().Any());
        }

        sealed class Fixture : IDisposable
        {
            public readonly Player First = TestWorld.CreatePlayer(11);
            public readonly Player Second = TestWorld.CreatePlayer(12);
            public readonly RecordingSession FirstSession = new();
            public readonly RecordingSession SecondSession = new();
            public readonly RecordingPersistence Persistence = new();
            public readonly RecordingCoalesce Coalesce = new();
            public readonly InventoryFlushService Flush;
            public readonly TradeSession Trade;
            readonly TradeService _service;
            readonly HashItemMinter _minter;
            public Fixture()
            {
                First.Session = FirstSession;
                Second.Session = SecondSession;
                First.Stats.Set(CharacterStat.Cash, 100);
                Second.Stats.Set(CharacterStat.Cash, 200);
                // Empty initialized registry; no world, DB or heartbeat is needed by this boundary.
                var manager = (PlayfieldManager)RuntimeHelpers.GetUninitializedObject(typeof(PlayfieldManager));
                typeof(PlayfieldManager).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, new Lock());
                typeof(PlayfieldManager).GetField("_playersByCharacterId", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(manager, new Dictionary<int, Player>());
                Flush = new InventoryFlushService(new Lazy<PlayfieldManager>(() => manager), Coalesce, new StubLogger());
                var data = new StubGameData(HashItemCatalog.Parse("{}", "{\"ITEM\":{\"TemplateId\":[1000],\"MinLevel\":1,\"MaxLevel\":1}}"));
                var catalog = new StubCatalog().Add(1000, quality: 1, price: 100);
                _minter = new HashItemMinter(data, catalog, new StubItemBuilder());
                _service = new TradeService(new StubLogger(), data, catalog,
                    _minter, new Ids(), Flush, Persistence);
                Trade = new TradeSession(new Identity { Type = IdentityType.TradeWindow, Instance = 90 }, TradeKind.Player, First, Second, null);
            }
            public void Commit() => typeof(TradeService).GetMethod("CommitPlayerTrade", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_service, new object[] { Trade });
            public TradeSession CreateShop()
            {
                var machine = new VendingMachine(new Identity { Type = IdentityType.VendingMachine, Instance = 91 }, new ItemTemplate { Id = 9000 });
                machine.Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
                machine.Stats.Set(CharacterStat.BuyModifier, 50);
                machine.Stats.Set(CharacterStat.SellModifier, 100);
                machine.Stock.EnsureFresh(new VendingMachineDefinition
                {
                    Inventory = new List<VendingMachineStockEntry> { new() { Hash = "ITEM", MinLevel = 1, MaxLevel = 1, Repeats = 1, Chance = 100 } }
                }, _minter, new Random(1));
                return new TradeSession(new Identity { Type = IdentityType.TradeWindow, Instance = 92 }, TradeKind.Shop, First, null, machine);
            }
            public void CommitShop(TradeSession session) => typeof(TradeService).GetMethod("CommitShop", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_service, new object[] { First, session });
            public void Dispose() => Flush.Dispose();
        }

        sealed class Ids : IItemInstanceIdAllocator { int _next = 100; public int Allocate() => _next++; }
        sealed class RecordingPersistence : ITradePersistence
        {
            public Action<TradePersistenceBatch>? BeforeCommit;
            public int Calls;
            public void Persist(TradePersistenceBatch batch) { Calls++; BeforeCommit?.Invoke(batch); }
        }
        sealed class RecordingCoalesce : ICharacterCoalesceCommit
        {
            public int Calls;
            public Exception? Failure;
            public void Persist(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates, int characterId, IReadOnlyList<int> uploadedNanoIds)
            {
                Calls++;
                if (Failure != null) throw Failure;
            }
        }
        sealed class RecordingSession : IZoneSession
        {
            public readonly List<MessageBody> Messages = new();
            public SessionState State { get; set; } = SessionState.InPlay;
            public Player? Player { get; private set; }
            public void BindPlayer(Player player) => Player = player;
            public void UnbindPlayer() => Player = null;
            public void TransferToPlayfield(Playfield destination, Vector3 landing) => throw new NotSupportedException();
            public void Send(byte[] packet) => throw new NotSupportedException();
            public void Send(Message message) => throw new NotSupportedException();
            public void Send(MessageBody body) => Messages.Add(body);
            public void Send(MessageBody body, int sender, int receiver) => Messages.Add(body);
            public void SendInitiateCompression() => throw new NotSupportedException();
            public void Close() => State = SessionState.Closed;
        }
    }
}
