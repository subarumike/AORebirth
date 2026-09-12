namespace ZoneEngine_New.Tests;

using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Playfields;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Dialogue;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Trade;

[TestClass]
public sealed class AcceptedGardenVendorTests
{
    [TestMethod]
    public void ComposedCatalogHasTwentyTwoNpcActorsButOnlySixteenImplementedDialogueDomains()
    {
        Assert.AreEqual(22, AcceptedSocialNpcCatalog.Definitions.Count);
        Assert.AreEqual(17, AcceptedSocialNpcCatalog.Definitions.Count(value => value.Binding.HasDialogue));
        Assert.AreEqual(3, AcceptedAreteVendorCatalog.StandaloneDefinitions.Count);
        int enabled = 0;
        foreach (var definition in AcceptedSocialNpcCatalog.Definitions)
        {
            using var state = new AuthoredQuestTests.World(definition.Binding.PlayfieldId);
            if (new DialogueActionRouter(state.Service).TryOpen(state.Player, definition.Binding.ContentNpcIdentity, false, out _)) enabled++;
        }
        Assert.AreEqual(16, enabled, "Lorelei's shop cannot masquerade as her missing quest dialogue, and Zyvania transport is not connected.");
    }

    [TestMethod]
    public void ExactlyElevenUngatedSourcesKeepEachExistingStockRowAndExcludeBothKeyGatedOfficials()
    {
        Assert.AreEqual(11, AcceptedGardenVendorCatalog.Definitions.Count);
        Assert.AreEqual(5, AcceptedGardenVendorCatalog.Placements.Count(value => value.PlayfieldId == 4677));
        Assert.AreEqual(6, AcceptedGardenVendorCatalog.Placements.Count(value => value.PlayfieldId == 4676));
        Assert.AreEqual(11, AcceptedGardenVendorCatalog.Placements.Select(value => value.SourceNpcInstance).Distinct().Count());
        Assert.IsFalse(AcceptedGardenVendorCatalog.Placements.Any(value => value.SourceNpcInstance is 0x79758F40 or 0x7A2013BA));
        foreach (var placement in AcceptedGardenVendorCatalog.Placements)
        {
            (int Slot, int LowId, int HighId, int Quality)[] expected;
            if (placement.PlayfieldId == 4677)
            {
                var source = CapturedThrakGardenVendorContentProvider.Definitions.Single(value => value.SourceNpcInstance == placement.SourceNpcInstance);
                Assert.IsFalse(source.RequiresCompletedGardenKeyQuest); Assert.AreEqual(source.SourceVendorInstance, placement.SourceVendorInstance);
                expected = source.Stock.Select(row => (row.Slot, row.LowId, row.HighId, row.Quality)).ToArray();
            }
            else
            {
                var source = CapturedAbanGardenVendorContentProvider.Definitions.Single(value => value.SourceNpcInstance == placement.SourceNpcInstance);
                Assert.IsFalse(source.RequiresGardenKey); Assert.AreEqual(source.SourceVendorInstance, placement.SourceVendorInstance);
                Assert.AreEqual(source.ExpectedX, placement.X); Assert.AreEqual(source.ExpectedZ, placement.Z);
                expected = source.Stock.Select(row => (row.Slot, row.LowId, row.HighId, row.Quality)).ToArray();
            }
            Assert.IsTrue(expected.Length > 0);
            CollectionAssert.AreEqual(expected, placement.Stock.Select(row => (row.Slot, row.LowId, row.HighId, row.Quality)).ToArray());
        }
    }

    [TestMethod]
    public void SourceFactoriesRetainActualLegacySeedOverridesMeshLayerAndPassiveLifetime()
    {
        foreach (var pair in AcceptedGardenVendorCatalog.Definitions.Zip(AcceptedGardenVendorCatalog.Placements))
        {
            var npc = pair.First.Create(new StubItemBuilder()); var content = pair.Second; bool aban = content.PlayfieldId == 4676;
            Assert.AreEqual(content.SourceNpcInstance, npc.Identity.Instance);
            Assert.AreEqual(content.X, npc.Position.xf); Assert.AreEqual(content.Y, npc.Position.yf); Assert.AreEqual(content.Z, npc.Position.zf);
            var spawn = npc.BuildSpawnMessage(); Assert.AreEqual(content.HeadingY, spawn.Heading.Y); Assert.AreEqual(content.HeadingW, spawn.Heading.W);
            Assert.AreEqual(30, (int)spawn.Level); Assert.AreEqual(32800, spawn.Health);
            Assert.AreEqual(aban ? 236640 : 208640, (int)spawn.MonsterData); Assert.AreEqual(40694u, spawn.HeadMesh);
            Assert.AreEqual(aban ? 103 : 513, (int)spawn.RunSpeedBase); Assert.AreEqual(0, spawn.Textures.Length);
            Assert.AreEqual(1, spawn.Meshes.Length); Assert.AreEqual((uint)content.Mesh, spawn.Meshes[0].Id);
            Assert.AreEqual((byte)1, spawn.Meshes[0].Position); Assert.AreEqual((byte)2, spawn.Meshes[0].Layer);
            Assert.AreEqual(137, ((SimpleNpcInfo)spawn.CharacterInfo).Family); Assert.AreEqual(15, ((SimpleNpcInfo)spawn.CharacterInfo).LosHeight);
            npc.Stats.Set(CharacterStat.Health, 10); npc.Rebase(); npc.RebaseWeapons(); npc.StartFighting(new() { Type = IdentityType.CanbeAffected, Instance = 5 }, 0);
            npc.Tick(600); Assert.AreEqual(10, npc.Stats.GetOrZero(CharacterStat.Health)); Assert.AreEqual(Identity.None, npc.FightingTarget);
            Assert.AreEqual(0, npc.Weapons.Count);
        }
    }

    [TestMethod]
    public void BothOrMadaProtectionActorsKeepDistinctSourceEndpointPoseAndFrozenStock()
    {
        var a = AcceptedGardenVendorCatalog.Placements.Single(value => value.SourceNpcInstance == 0x7A2013B4);
        var b = AcceptedGardenVendorCatalog.Placements.Single(value => value.SourceNpcInstance == 0x7A2013B6);
        Assert.AreEqual(a.Name, b.Name); Assert.AreNotEqual(a.SourceVendorInstance, b.SourceVendorInstance);
        Assert.AreEqual(0x130B7810, a.SourceVendorInstance); Assert.AreEqual(0x130B7812, b.SourceVendorInstance);
        Assert.AreEqual(404.2271f, a.X); Assert.AreEqual(421.6585f, b.X); Assert.AreNotEqual(a.HeadingY, b.HeadingY);
        Assert.IsFalse(a.Stock.SequenceEqual(b.Stock));
    }

    [TestMethod]
    public void MissingAnyGardenEndpointRejectsWholeShopAndImpersonationCannotAttach()
    {
        foreach (var pair in AcceptedGardenVendorCatalog.Definitions.Zip(AcceptedGardenVendorCatalog.Placements))
        {
            foreach (int missing in new[] { pair.Second.VendorTemplateId, pair.Second.Stock[0].LowId, pair.Second.Stock[0].HighId })
            {
                var npc = pair.First.Create(new StubItemBuilder());
                Assert.IsFalse(AcceptedGardenVendorCatalog.TryAttachShop(npc, new StubItemBuilder(), Catalog(missing), out var failure));
                Assert.IsNull(npc.Shop); Assert.IsFalse(string.IsNullOrWhiteSpace(failure));
            }
            var impostor = new NpcCharacter(new() { Type = IdentityType.CanbeAffected, Instance = pair.Second.SourceNpcInstance }, new StubItemBuilder()) { Name = pair.Second.Name };
            Assert.IsFalse(AcceptedGardenVendorCatalog.TryAttachShop(impostor, new StubItemBuilder(), Catalog(), out _));
        }
    }

    [TestMethod]
    public void EveryAcceptedGardenActorOpensExactShopFromBothBusinessAnswerAndGenericUse()
    {
        foreach (var placement in AcceptedGardenVendorCatalog.Placements)
        {
            using (var w = new World(placement))
            {
                Assert.IsTrue(w.Dialogue.Open(w.State.Session, w.Npc.Identity)); w.Drain();
                Assert.IsTrue(w.Dialogue.Answer(w.State.Session, w.Npc.Identity, 0));
                AssertShop(w, placement);
            }
            using (var w = new World(placement))
            {
                Assert.IsTrue(w.Npc.TryUse(w.State.Player)); AssertShop(w, placement);
            }
        }
    }

    [TestMethod]
    public void DespawnedOrForeignLifetimeGardenActorCannotOpenShopOrContinueBusinessDialogue()
    {
        var placement = AcceptedGardenVendorCatalog.Placements[0];
        using var w = new World(placement); Assert.IsTrue(w.Dialogue.Open(w.State.Session, w.Npc.Identity)); w.Drain();
        var impostor = new NpcCharacter(w.Npc.Identity, new StubItemBuilder()) { Name = w.Npc.Name, Playfield = w.Npc.Playfield };
        w.State.Registry.Register(impostor);
        Assert.IsFalse(w.Dialogue.Answer(w.State.Session, w.Npc.Identity, 0));
        Assert.IsFalse(w.Npc.TryUse(w.State.Player)); Assert.IsFalse(w.Trade.TryGetSession(w.State.Player, out _));
        Assert.AreEqual(0, w.State.Session.Messages.OfType<ShopUpdateMessage>().Count());
        Assert.AreEqual(0, w.State.Dao.Calls);
    }

    static void AssertShop(World w, AcceptedGardenVendorCatalog.Placement placement)
    {
        Assert.IsTrue(w.Trade.TryGetSession(w.State.Player, out var session)); Assert.AreSame(w.Npc.Shop, session.Machine);
        var packet = w.State.Session.Messages.OfType<ShopUpdateMessage>().Single();
        Assert.AreEqual(placement.SourceVendorInstance, w.Npc.Shop!.Identity.Instance);
        CollectionAssert.AreEqual(placement.Stock.Select(row => (row.LowId, row.HighId, row.Quality)).ToArray(),
            packet.VendingMachineSlots.Select(row => (row.ItemLowId, row.ItemHighId, row.Quality)).ToArray());
        Assert.AreEqual(0, w.State.Dao.Calls);
    }

    static StubCatalog Catalog(int missing = 0)
    {
        var catalog = new StubCatalog();
        foreach (var placement in AcceptedGardenVendorCatalog.Placements)
        {
            if (placement.VendorTemplateId != missing) catalog.Add(placement.VendorTemplateId, 1);
            foreach (var row in placement.Stock)
            {
                if (row.LowId != missing) catalog.Add(row.LowId, row.Quality, price: 100);
                if (row.HighId != missing) catalog.Add(row.HighId, row.Quality, price: 100);
            }
        }
        return catalog;
    }

    sealed class World : IDisposable
    {
        internal readonly AuthoredQuestTests.World State;
        internal readonly NpcCharacter Npc;
        internal readonly DialogueService Dialogue;
        internal readonly TradeService Trade;
        readonly ServiceProvider _services;
        long _now;
        internal World(AcceptedGardenVendorCatalog.Placement placement)
        {
            State = new(placement.PlayfieldId); var catalog = Catalog(); var items = new StubItemBuilder();
            var data = new StubGameData(HashItemCatalog.Parse("{}", "{}"));
            Trade = new(new StubLogger(), data, catalog, new HashItemMinter(data, catalog, items), new Ids(), State.Flush, new NoMutation());
            var playfield = State.Player.Playfield!; var locality = playfield.GetRequiredService<PlayfieldLocality>();
            _services = new ServiceCollection().AddSingleton(State.Registry).AddSingleton(State.Npcs).AddSingleton(locality).AddSingleton(Trade).BuildServiceProvider();
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(playfield, _services);
            State.Npcs.Activate();
            Assert.IsTrue(State.Registry.TryGet(new() { Type = IdentityType.CanbeAffected, Instance = placement.SourceNpcInstance }, out var actor));
            Npc = (NpcCharacter)actor;
            Assert.IsTrue(State.Npcs.TryGetBinding(Npc, out _)); Assert.IsNotNull(Npc.Shop);
            State.Player.Position = new(Npc.Position.xf, Npc.Position.yf, Npc.Position.zf);
            Dialogue = new(DialogueCatalog.Load(AppContext.BaseDirectory), new DialogueActionRouter(State.Service), () => _now);
        }
        internal void Drain() { for (int i = 0; i < 10; i++) { _now += 20; Dialogue.Tick(State.Player.Playfield!); } }
        public void Dispose() { Dialogue.Shutdown(State.Player.Playfield!); Trade.Cancel(State.Player, "fixture complete"); _services.Dispose(); State.Dispose(); }
    }
    sealed class Ids : IItemInstanceIdAllocator { public int Allocate() => throw new InvalidOperationException("Opening frozen stock must not mint persistent inventory."); }
    sealed class NoMutation : ITradePersistence { public void Persist(TradePersistenceBatch batch) => throw new InvalidOperationException("Opening stock must not persist player inventory."); }
}
