namespace ZoneEngine_New.Tests;

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Mobs;

[TestClass]
public sealed class AcceptedStandaloneShopRuntimeTests
{
    const int IccTechSource = 317157896;
    static Identity ShopIdentity => new() { Type = IdentityType.VendingMachine, Instance = IccTechSource };

    static VendingMachine Shop(AcceptedSubwayShopRuntimeTests.World world)
    {
        Assert.IsTrue(world.Registry.TryGet(ShopIdentity, out var actor));
        var shop = (VendingMachine)actor;
        Assert.IsNull(shop.OwnerNpc);
        world.Player.Position = new(shop.Position.x, shop.Position.y, shop.Position.z);
        shop.Stats.Set(CharacterStat.SellModifier, 100);
        return shop;
    }

    static void SelectLockpickBox(AcceptedSubwayShopRuntimeTests.World world) => world.Trade.Handle(world.Player,
        new TradeMessage { Identity = world.Player.Identity, Target = ShopIdentity, Action = TradeAction.AddItem,
            Container = new() { Type = IdentityType.ShopInventory, Instance = 3 } });

    [TestMethod]
    public void AcceptedWorldVendorHasNoFakeNpcAndPurchasesExactCapturedLockpickBoxOnce()
    {
        using var world = new AcceptedSubwayShopRuntimeTests.World(playfieldId: 6553);
        var shop = Shop(world);
        Assert.IsTrue(world.Activation.TryGetShopBinding(shop, out var binding));
        Assert.AreEqual(6553, binding.PlayfieldId);
        var wire = (VendingMachineFullUpdateMessage)shop.BuildSpawnMessage();
        Assert.AreEqual(Identity.None, wire.NpcIdentity);
        Assert.AreEqual(295999, shop.Stock.Slots[3].LowId);
        Assert.IsTrue(shop.TryUse(world.Player));
        SelectLockpickBox(world);
        world.Session.Messages.Clear();
        world.Persistence.BeforeCommit = batch =>
        {
            Assert.AreEqual(0, world.Session.Messages.Count);
            Assert.AreEqual(0, world.Player.Inventory.Inventory.Content.Count);
            Assert.AreEqual(295999, batch.Inserts.Single().LowId);
            Assert.AreEqual(900, batch.Characters.Single().Cash);
        };
        world.Accept(); world.Accept();
        Assert.AreEqual(1, world.Persistence.Calls);
        Assert.AreEqual(900, world.Player.Stats.GetOrZero(CharacterStat.Cash));
        Assert.AreEqual(295999, world.Player.Inventory.Inventory.Content.Values.Single().LowId);
        Assert.AreEqual(1, world.Session.Messages.OfType<TradeMessage>().Count(message => message.Action == TradeAction.Complete));
    }

    [TestMethod]
    public void SameIdentityReplacementForeignWorldClosedTransportAndShutdownCannotCommitWorldShop()
    {
        foreach (var invalid in new[] { "replacement", "world", "closed", "shutdown" })
        {
            using var world = new AcceptedSubwayShopRuntimeTests.World(playfieldId: 6553);
            var shop = Shop(world);
            Assert.IsTrue(shop.TryUse(world.Player)); SelectLockpickBox(world);
            world.Session.Messages.Clear();
            if (invalid == "replacement") world.Registry.Register(new VendingMachine(ShopIdentity, new StubCatalog().Add(300946, 1).Require(300946))
                { Playfield = shop.Playfield, Position = shop.Position });
            else if (invalid == "shutdown") world.Activation.Shutdown();
            else world.Invalidate(invalid);
            world.Accept();
            Assert.AreEqual(0, world.Persistence.Calls, invalid);
            Assert.AreEqual(0, world.Player.Inventory.Inventory.Content.Count, invalid);
            Assert.IsFalse(world.Session.Messages.OfType<TradeMessage>().Any(message => message.Action == TradeAction.Complete), invalid);
        }
    }

    [TestMethod]
    public void ExactAlreadyLoadedStaticMachineAcquiresAcceptedStockWithoutReplacingItsWorldIdentity()
    {
        VendingMachine? loaded = null;
        using var world = new AcceptedSubwayShopRuntimeTests.World(playfieldId: 6553,
            beforeActivate: (registry, playfield, catalog) =>
            {
                var content = AcceptedAreteVendorCatalog.StandaloneDefinitions.Single(value => value.Content.SourceVendorInstance == IccTechSource).Content;
                loaded = new VendingMachine(ShopIdentity, catalog.Require(content.TemplateId))
                {
                    Playfield = playfield, SpawnSource = SpawnSource.StaticDynel,
                    Position = new(content.X, content.Y, content.Z),
                    Rotation = new(content.HeadingX, content.HeadingY, content.HeadingZ, content.HeadingW)
                };
                registry.Register(loaded);
                playfield.GetRequiredService<ZoneEngine_New.Core.Playfield.Locality.PlayfieldLocality>().RegisterDynel(loaded);
            });
        Assert.AreSame(loaded, Shop(world));
        Assert.IsTrue(world.Activation.TryGetShopBinding(loaded!, out _));
        Assert.IsTrue(loaded!.TryUse(world.Player));
        Assert.AreEqual(295999, loaded.Stock.Slots[3].LowId);
        world.Activation.Activate();
        Assert.AreSame(loaded, Shop(world));
    }

    [TestMethod]
    public void MissingCapturedLockpickEndpointDoesNotCreatePartialOrRandomizedWorldShop()
    {
        using var world = new AcceptedSubwayShopRuntimeTests.World(missingTemplate: 295999, playfieldId: 6553);
        Assert.IsFalse(world.Registry.TryGet(ShopIdentity, out _));
        var definition = AcceptedAreteVendorCatalog.StandaloneDefinitions.Single(value => value.Content.SourceVendorInstance == IccTechSource);
        Assert.IsTrue(world.Activation.UnavailableVendorEndpoints.ContainsKey(definition.PlacementIdentity));
        Assert.AreEqual(0, world.Persistence.Calls);
    }
}
