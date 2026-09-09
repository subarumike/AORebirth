namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Playfields;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Trade;
using ZoneEngine_New.Core.WorldSimulation;

[TestClass]
public sealed class AcceptedSubwayShopRuntimeTests
{
    [TestMethod]
    public void ActualAcceptedActorOpensFrozenStockAndCommitsPurchaseSaleCashBeforePublication()
    {
        using var w = new World();
        Assert.IsTrue(w.Merchant.TryUse(w.Player), "Accepted shop must not require an unrelated VendingMachines.json roll.");
        Assert.IsTrue(w.Trade.TryGetSession(w.Player, out var trade));
        Assert.AreSame(w.Merchant.Shop, trade.Machine);
        var stock = w.Merchant.Shop!.Stock.Slots[0];
        Assert.AreEqual(stock.LowId, w.Session.Messages.OfType<ShopUpdateMessage>().Single().VendingMachineSlots[0].ItemLowId);
        var sold = TestWorld.CreateItem(instanceId: 99001);
        w.Player.Inventory.Inventory.Content[64] = sold;
        w.Trade.Handle(w.Player, new TradeMessage { Identity = w.Player.Identity, Target = w.Player.Identity,
            Action = TradeAction.AddItem, Container = new() { Type = IdentityType.Inventory, Instance = 64 } });
        w.Trade.Handle(w.Player, new TradeMessage { Identity = w.Player.Identity, Target = w.Merchant.Shop.Identity,
            Action = TradeAction.AddItem, Container = new() { Type = IdentityType.ShopInventory, Instance = 0 } });
        w.Session.Messages.Clear();
        w.Persistence.BeforeCommit = batch =>
        {
            Assert.IsTrue(Monitor.IsEntered(w.Player.PersistenceGate));
            Assert.AreEqual(1000, w.Player.Stats.GetOrZero(CharacterStat.Cash));
            Assert.AreEqual(950, batch.Characters.Single().Cash);
            Assert.AreEqual(stock.LowId, batch.Inserts.Single().LowId);
            Assert.AreEqual(stock.HighId, batch.Inserts.Single().HighId);
            Assert.AreEqual(stock.Quality, batch.Inserts.Single().Quality);
            Assert.AreEqual(sold.InstanceId, batch.Updates.Single().InstanceId);
            Assert.AreEqual(w.Merchant.Shop.Identity.Instance, batch.Updates.Single().ContainerInstance);
            Assert.AreSame(sold, trade.InitiatorOffer.Items.Single().Value);
            Assert.AreEqual(0, w.Session.Messages.Count);
        };
        w.Accept();
        Assert.AreEqual(1, w.Persistence.Calls); Assert.AreEqual(950, w.Player.Stats.GetOrZero(CharacterStat.Cash));
        Assert.IsTrue(w.Player.Inventory.Inventory.Content.Values.Single().IsPersisted);
        Assert.AreEqual(stock.LowId, w.Player.Inventory.Inventory.Content.Values.Single().LowId);
        Assert.IsTrue(w.Session.Messages.OfType<TradeMessage>().Any(message => message.Action == TradeAction.Complete));
        Assert.IsFalse(w.Trade.TryGetSession(w.Player, out _));
        w.Accept(); Assert.AreEqual(1, w.Persistence.Calls);
    }

    [TestMethod]
    public void AcceptedShopRejectsStaleActorForeignWorldClosedOrUnownedSessionBeforeOpen()
    {
        foreach (string invalid in new[] { "replacement", "world", "closed", "quarantined", "session-owner" })
        {
            using var w = new World(); w.Invalidate(invalid);
            Assert.IsFalse(w.Trade.TryOpenShop(w.Player, w.Merchant.Shop!), invalid);
            Assert.IsFalse(w.Trade.TryGetSession(w.Player, out _)); Assert.AreEqual(0, w.Persistence.Calls);
        }
    }

    [TestMethod]
    public void AcceptedShopRevalidatesActorAndWorldAgainBeforeCommittingChosenStock()
    {
        foreach (string invalid in new[] { "replacement", "world", "closed", "quarantined", "session-owner" })
        {
            using var w = new World(); Assert.IsTrue(w.Merchant.TryUse(w.Player));
            w.Trade.Handle(w.Player, new TradeMessage { Identity = w.Player.Identity, Target = w.Merchant.Shop!.Identity,
                Action = TradeAction.AddItem, Container = new() { Type = IdentityType.ShopInventory, Instance = 0 } });
            w.Session.Messages.Clear(); w.Invalidate(invalid); w.Accept();
            Assert.AreEqual(0, w.Persistence.Calls, invalid);
            Assert.AreEqual(1000, w.Player.Stats.GetOrZero(CharacterStat.Cash));
            Assert.AreEqual(0, w.Player.Inventory.Inventory.Content.Count);
            Assert.IsFalse(w.Session.Messages.OfType<TradeMessage>().Any(message => message.Action == TradeAction.Complete));
        }
    }

    [TestMethod]
    public void ActualMissingVendorTemplateKeepsSocialPlacementButCannotOpenAnInventedShop()
    {
        using var w = new World(missingTemplate: CapturedSubwayVendorContentProvider.Definitions[0].VendorTemplateId);
        Assert.IsTrue(w.Activation.TryGetBinding(w.Merchant, out var binding));
        Assert.AreEqual("SimpleChar:79135F51", binding.ContentNpcIdentity); Assert.IsTrue(binding.HasDialogue);
        Assert.IsNull(w.Merchant.Shop); Assert.IsFalse(w.Merchant.TryUse(w.Player));
        Assert.IsFalse(w.Trade.TryGetSession(w.Player, out _)); Assert.AreEqual(0, w.Persistence.Calls);
    }

    internal sealed class World : IDisposable
    {
        internal readonly Player Player = TestWorld.CreatePlayer(4401);
        internal readonly Session Session = new();
        internal readonly Persistence Persistence = new();
        internal readonly DynelRegistry Registry = new();
        internal readonly TradeService Trade;
        internal readonly AcceptedNpcActivationService Activation;
        internal readonly NpcCharacter Merchant;
        internal readonly PlayfieldTransferTests.SnapshotStore Snapshots = new();
        internal SpawnService Spawns => _services.GetRequiredService<SpawnService>();
        readonly InventoryFlushService _flush;
        readonly ServiceProvider _services;
        internal World(int missingTemplate = 0, int playfieldId = 127,
            Action<DynelRegistry, Playfield, StubCatalog>? beforeActivate = null, bool allowMissingCatMesh = false)
        {
            var playfield = NewPlayfield(playfieldId);
            var locality = new PlayfieldLocality(playfieldId, null);
            var catalog = new StubCatalog().Add(1000, 1, price: 100);
            foreach (var content in CapturedSubwayVendorContentProvider.Definitions)
            {
                if (content.VendorTemplateId != missingTemplate) catalog.Add(content.VendorTemplateId, 1);
                foreach (var row in content.Stock)
                {
                    if (row.LowId != missingTemplate) catalog.Add(row.LowId, row.Quality, price: 100);
                    if (row.HighId != missingTemplate) catalog.Add(row.HighId, row.Quality, price: 100);
                }
            }
            void AddAreteStock(int template, IReadOnlyList<CapturedAreteAlexAreaVendorStockDefinition> stock)
            {
                if (template != missingTemplate) catalog.Add(template, 1);
                foreach (var row in stock)
                {
                    if (row.LowId != missingTemplate) catalog.Add(row.LowId, row.Quality, price: 100);
                    if (row.HighId != missingTemplate) catalog.Add(row.HighId, row.Quality, price: 100);
                }
            }
            if (playfieldId == 6553)
            {
                AddAreteStock(CapturedAreteMarcoSpidaVendorContentProvider.CaptureVendorTemplateId,
                    CapturedAreteMarcoSpidaVendorContentProvider.Stock);
                AddAreteStock(CapturedAreteLoreleiVendorContentProvider.CaptureVendorTemplateId,
                    CapturedAreteLoreleiVendorContentProvider.Stock);
                foreach (var definition in AcceptedAreteVendorCatalog.StandaloneDefinitions)
                    AddAreteStock(definition.Content.TemplateId, definition.Content.Stock);
            }
            var manager = (PlayfieldManager)RuntimeHelpers.GetUninitializedObject(typeof(PlayfieldManager));
            typeof(PlayfieldManager).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, new Lock());
            typeof(PlayfieldManager).GetField("_playersByCharacterId", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(manager, new Dictionary<int, Player>());
            _flush = new InventoryFlushService(new Lazy<PlayfieldManager>(() => manager), new Coalesce(), new StubLogger());
            var data = new StubGameData(HashItemCatalog.Parse("{}", "{}"), allowMissingCatMesh: allowMissingCatMesh);
            var items = new StubItemBuilder();
            var minter = new HashItemMinter(data, catalog, items);
            var ids = new Ids();
            var actions = new InventoryActionService(new NoInventoryMutation(), _flush, ids, new StubLogger(), catalog, items);
            Trade = new TradeService(new StubLogger(), data, catalog, minter, ids, _flush, Persistence);
            Activation = new AcceptedNpcActivationService(playfield, Registry, locality, items, catalog);
            _services = new ServiceCollection().AddSingleton(Registry).AddSingleton(locality).AddSingleton(Activation).AddSingleton(Trade)
                .AddSingleton<IGameData>(data)
                .AddSingleton(new WorldSimulationAccess())
                .AddSingleton(actions).AddSingleton(new InventoryMoveService(new StubLogger(), _flush, actions))
                .AddSingleton<SpawnService>(services => new SpawnService(services, Registry, new StubLogger(), playfield,
                    manager, data, items, minter, ids, _flush, Trade,
                    new CharacterSnapshotService(Snapshots, Snapshots, new StubLogger())))
                .BuildServiceProvider();
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(playfield, _services);
            Player.Playfield = playfield; Player.Session = Session; Session.BindPlayer(Player);
            Player.Stats.Set(CharacterStat.Health, 100); Player.Stats.Set(CharacterStat.Cash, 1000);
            Registry.Register(Player); // Match the real session's live playfield ownership.
            beforeActivate?.Invoke(Registry, playfield, catalog);
            Activation.Activate();
            int merchantId = playfieldId == 6553 ? CapturedAreteMarcoSpidaVendorContentProvider.SourceNpcInstance : 0x79135F51;
            Assert.IsTrue(Registry.TryGet(new() { Type = IdentityType.CanbeAffected, Instance = merchantId }, out var actor));
            Merchant = (NpcCharacter)actor;
            Player.Position = new AORebirth.Core.Vector.Vector3(Merchant.Position.x, Merchant.Position.y, Merchant.Position.z);
            if (Merchant.Shop != null)
            { Merchant.Shop.Stats.Set(CharacterStat.SellModifier, 100); Merchant.Shop.Stats.Set(CharacterStat.BuyModifier, 50); }
        }
        internal void Invalidate(string condition)
        {
            if (condition == "replacement") Registry.Register(new NpcCharacter(Merchant.Identity, new StubItemBuilder())
                { Name = Merchant.Name, Playfield = Merchant.Playfield });
            if (condition == "world") Player.Playfield = NewPlayfield(Player.Playfield!.Identity.Instance); // Even same PF number is not the same lifetime.
            if (condition == "closed") Session.Close();
            if (condition == "quarantined") Player.QuarantinePersistence();
            if (condition == "session-owner") Session.UnbindPlayer();
        }
        internal void Accept() => Trade.Handle(Player, new TradeMessage { Identity = Player.Identity, Action = TradeAction.Accept });
        static Playfield NewPlayfield(int playfieldId)
        {
            var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            typeof(Playfield).GetField("<Identity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(playfield, new Identity { Type = IdentityType.Playfield2, Instance = playfieldId });
            typeof(Playfield).GetField("_nextContainerInventoryHandle", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(playfield, 1);
            return playfield;
        }
        public void Dispose()
        {
            // Restore only the test session's ownership so ordinary cancellation can release offers.
            Player.Session = Session; Session.BindPlayer(Player);
            Trade.Cancel(Player, "fixture complete"); _flush.Dispose(); _services.Dispose();
        }
    }
    sealed class Ids : IItemInstanceIdAllocator { int _next = 98000; public int Allocate() => _next++; }
    sealed class NoInventoryMutation : IInventoryMutationPersistence
    {
        public void Persist(InventoryMutationBatch batch) => throw new InvalidOperationException("Vendor death must not mutate persisted inventory.");
    }
    internal sealed class Persistence : ITradePersistence
    {
        internal int Calls;
        internal Action<TradePersistenceBatch>? BeforeCommit;
        public void Persist(TradePersistenceBatch batch) { Calls++; BeforeCommit?.Invoke(batch); }
    }
    sealed class Coalesce : ICharacterCoalesceCommit
    {
        public void Persist(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates, int characterId, IReadOnlyList<int> nanos) { }
    }
    internal sealed class Session : IZoneSession
    {
        internal readonly List<MessageBody> Messages = [];
        public SessionState State { get; set; } = SessionState.InPlay;
        public Player? Player { get; private set; }
        public void BindPlayer(Player player) => Player = player;
        public void UnbindPlayer() => Player = null;
        public void TransferToPlayfield(Playfield destination, AORebirth.Core.Vector.Vector3 landing) => throw new NotSupportedException();
        public void Send(byte[] packet) => throw new NotSupportedException();
        public void Send(Message message) => Messages.Add(message.Body);
        public void Send(MessageBody body) => Messages.Add(body);
        public void Send(MessageBody body, int sender, int receiver) => Send(body);
        public void SendInitiateCompression() => throw new NotSupportedException();
        public void Close() => State = SessionState.Closed;
    }
}
