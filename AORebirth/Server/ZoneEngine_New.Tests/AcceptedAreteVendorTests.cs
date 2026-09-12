namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Playfields;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Mobs;

[TestClass]
public sealed class AcceptedAreteVendorTests
{
    [TestMethod]
    public void MarcoAndLoreleiPreserveExactSourceAppearanceNotAnInterpolatedNpcTemplate()
    {
        Assert.AreEqual(2, AcceptedAreteVendorCatalog.Definitions.Count);
        var marco = AcceptedAreteVendorCatalog.Definitions[0].Create(new StubItemBuilder());
        Assert.AreEqual(0x78E0FC81, marco.Identity.Instance); Assert.AreEqual("Marco Spida", marco.Name);
        Assert.AreEqual(3407.67676f, marco.Position.xf); Assert.AreEqual(831.262451f, marco.Position.zf);
        Assert.AreEqual(26092, marco.Stats.GetOrZero(CharacterStat.MonsterData)); Assert.AreEqual(34, marco.Stats.GetOrZero(CharacterStat.RunSpeed));
        var spawn = marco.BuildSpawnMessage(); Assert.AreEqual(1576u, spawn.Appearance.Value);
        Assert.AreEqual(40694u, spawn.HeadMesh); Assert.AreEqual(1, spawn.Meshes.Length);
        CollectionAssert.AreEqual(new[] { 0, 247966, 9619, 247920, 9626 }, spawn.Textures.Select(texture => texture.Id).ToArray());
        var lorelei = AcceptedAreteVendorCatalog.Definitions[1].Create(new StubItemBuilder());
        Assert.AreEqual(0x78E0FC6B, lorelei.Identity.Instance); Assert.AreEqual("Lorelei the Bartender", lorelei.Name);
        Assert.AreEqual(3369.1416f, lorelei.Position.xf); Assert.AreEqual(17.315f, lorelei.Position.yf);
        Assert.AreEqual(26137, lorelei.Stats.GetOrZero(CharacterStat.MonsterData)); Assert.AreEqual(35, lorelei.Stats.GetOrZero(CharacterStat.RunSpeed));
        spawn = lorelei.BuildSpawnMessage(); Assert.AreEqual(1864u, spawn.Appearance.Value);
        Assert.AreEqual(40209u, spawn.HeadMesh); Assert.AreEqual(2, spawn.Meshes.Length);
        CollectionAssert.AreEqual(new[] { 0, 30862, 40903, 30839, 30886 }, spawn.Textures.Select(texture => texture.Id).ToArray());
        foreach (var npc in new[] { marco, lorelei })
        {
            Assert.AreEqual(10, npc.Stats.GetOrZero(CharacterStat.Level)); Assert.AreEqual(227, npc.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(279450113, npc.Stats.GetOrZero(CharacterStat.Flags));
            npc.Rebase(); npc.RebaseWeapons(); npc.StartFighting(new() { Type = IdentityType.CanbeAffected, Instance = 55 }, 0);
            Assert.AreEqual(Identity.None, npc.FightingTarget); Assert.AreEqual(0, npc.Weapons.Count);
            Assert.AreEqual(0, ((SimpleNpcInfo)npc.BuildSpawnMessage().CharacterInfo).Family);
        }
    }

    [TestMethod]
    public void MarcoAndLoreleiAttachOnlyExactCompleteFrozenStockToExactSourceOwner()
    {
        var catalog = Catalog(); int total = 0;
        foreach (var definition in AcceptedAreteVendorCatalog.Definitions)
        {
            var npc = definition.Create(new StubItemBuilder());
            Assert.IsTrue(AcceptedAreteVendorCatalog.TryAttachShop(npc, new StubItemBuilder(), catalog, out var failure), failure);
            bool marco = npc.Identity.Instance == 0x78E0FC81;
            var expected = marco ? CapturedAreteMarcoSpidaVendorContentProvider.Stock : CapturedAreteLoreleiVendorContentProvider.Stock;
            Assert.AreEqual(marco ? 0x12E77212 : 0x12E7720B, npc.Shop!.Identity.Instance);
            Assert.AreSame(npc, npc.Shop.OwnerNpc); Assert.IsTrue(npc.Shop.Stock.IsAcceptedSnapshot);
            CollectionAssert.AreEqual(expected.Select(row => (row.LowId, row.HighId, row.Quality)).ToArray(),
                npc.Shop.Stock.Slots.Select(row => (row.LowId, row.HighId, row.Quality)).ToArray());
            Assert.IsFalse(AcceptedAreteVendorCatalog.TryAttachShop(npc, new StubItemBuilder(), catalog, out _));
            total += npc.Shop.Stock.Slots.Count;
        }
        Assert.AreEqual(52, total);
    }

    [TestMethod]
    public void AlexAreaUsesThreeActualStandaloneMachinesIncludingExactLockpickSlotAndRotation()
    {
        using var w = new AuthoredQuestTests.World(6553); var catalog = Catalog(); int total = 0;
        Assert.AreEqual(3, AcceptedAreteVendorCatalog.StandaloneDefinitions.Count);
        foreach (var definition in AcceptedAreteVendorCatalog.StandaloneDefinitions)
        {
            Assert.IsTrue(AcceptedAreteVendorCatalog.TryCreateStandaloneShop(definition, w.Player.Playfield!, catalog, out var shop, out var failure), failure);
            Assert.AreEqual(IdentityType.VendingMachine, shop.Identity.Type); Assert.AreEqual(definition.SourceVendorInstance, shop.Identity.Instance);
            Assert.IsNull(shop.OwnerNpc); Assert.AreSame(w.Player.Playfield, shop.Playfield);
            Assert.AreEqual(definition.Content.TemplateId, shop.Template.Id); Assert.AreEqual(definition.Content.X, shop.Position.xf);
            Assert.IsTrue(shop.Stock.IsAcceptedSnapshot);
            CollectionAssert.AreEqual(definition.Content.Stock.Select(row => (row.LowId, row.HighId, row.Quality)).ToArray(),
                shop.Stock.Slots.Select(row => (row.LowId, row.HighId, row.Quality)).ToArray());
            var wire = (VendingMachineFullUpdateMessage)shop.BuildSpawnMessage(); Assert.AreEqual(Identity.None, wire.NpcIdentity);
            if (definition.SourceVendorInstance == 0x12E77208)
            {
                Assert.AreEqual(300946, shop.Template.Id); Assert.AreEqual(295999, shop.Stock.Slots[3].LowId);
                Assert.AreEqual(0.7057894f, wire.Heading.Y); Assert.AreEqual(-0.7084217f, wire.Heading.W);
                Assert.AreEqual(3442.931f, wire.Coordinates.X); Assert.AreEqual(822.4964f, wire.Coordinates.Z);
            }
            total += shop.Stock.Slots.Count;
        }
        Assert.AreEqual(27, total);
    }

    [TestMethod]
    public void MissingCapturedTemplateOrAnyStockEndpointRefusesWholeShopWithoutFallback()
    {
        using var w = new AuthoredQuestTests.World(6553);
        var standalone = AcceptedAreteVendorCatalog.StandaloneDefinitions[0];
        foreach (int missing in new[] { standalone.Content.TemplateId, standalone.Content.Stock[0].LowId, standalone.Content.Stock[0].HighId })
        {
            Assert.IsFalse(AcceptedAreteVendorCatalog.TryCreateStandaloneShop(standalone, w.Player.Playfield!, Catalog(missing), out var shop, out var failure));
            Assert.IsNull(shop); Assert.IsFalse(string.IsNullOrWhiteSpace(failure));
        }
        foreach (int missing in new[] { 248371, 248258, 297371, 297370 })
        {
            bool marco = missing is 248371 or 248258;
            var npc = AcceptedAreteVendorCatalog.Definitions[marco ? 0 : 1].Create(new StubItemBuilder());
            Assert.IsFalse(AcceptedAreteVendorCatalog.TryAttachShop(npc, new StubItemBuilder(), Catalog(missing), out var failure));
            Assert.IsNull(npc.Shop); Assert.IsFalse(string.IsNullOrWhiteSpace(failure));
        }
    }

    [TestMethod]
    public void ForgedSourceNameOrCopiedPlacementRecordNeverAcquiresAcceptedVendorAuthority()
    {
        using var w = new AuthoredQuestTests.World(6553); using var elsewhere = new AuthoredQuestTests.World(127);
        var original = AcceptedAreteVendorCatalog.StandaloneDefinitions[0];
        var copy = original with { };
        Assert.IsFalse(AcceptedAreteVendorCatalog.TryCreateStandaloneShop(copy, w.Player.Playfield!, Catalog(), out _, out _));
        Assert.IsFalse(AcceptedAreteVendorCatalog.TryCreateStandaloneShop(original, elsewhere.Player.Playfield!, Catalog(), out _, out _));
        var impostor = new NpcCharacter(new() { Type = IdentityType.CanbeAffected, Instance = 0x78E0FC81 }, new StubItemBuilder()) { Name = "Marco Spida" };
        Assert.IsFalse(AcceptedAreteVendorCatalog.TryAttachShop(impostor, new StubItemBuilder(), Catalog(), out _));
    }

    static StubCatalog Catalog(int missing = 0)
    {
        var result = new StubCatalog().Add(99634, 1); // A generic fallback must not make a missing exact endpoint succeed.
        var ids = new HashSet<int> { 248371, 297371 };
        var stock = CapturedAreteMarcoSpidaVendorContentProvider.Stock.Concat(CapturedAreteLoreleiVendorContentProvider.Stock);
        foreach (var definition in AcceptedAreteVendorCatalog.StandaloneDefinitions)
        { ids.Add(definition.Content.TemplateId); stock = stock.Concat(definition.Content.Stock); }
        foreach (var row in stock) { ids.Add(row.LowId); ids.Add(row.HighId); }
        foreach (int id in ids.Where(id => id != missing)) result.Add(id, 1);
        return result;
    }
}
