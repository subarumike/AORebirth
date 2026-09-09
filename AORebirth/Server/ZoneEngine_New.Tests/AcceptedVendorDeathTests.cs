namespace ZoneEngine_New.Tests;

using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Playfield.Locality;

[TestClass]
public sealed class AcceptedVendorDeathTests
{
    [DataTestMethod]
    [DataRow(127)]
    [DataRow(6553)]
    public void AcceptedVendorDeathClosesTradeImmediatelyAndRealDelayedSpawnCreatesOneCorpse(int playfieldId)
    {
        using var w = new AcceptedSubwayShopRuntimeTests.World(playfieldId: playfieldId);
        var playfield = w.Player.Playfield!;
        var shop = w.Merchant.Shop!;
        Assert.IsTrue(w.Merchant.TryUse(w.Player));
        var offered = TestWorld.CreateItem(instanceId: 99001);
        w.Player.Inventory.Inventory.Content[64] = offered;
        w.Trade.Handle(w.Player, new TradeMessage { Identity = w.Player.Identity, Target = w.Player.Identity,
            Action = TradeAction.AddItem, Container = new() { Type = IdentityType.Inventory, Instance = 64 } });
        int deaths = 0;
        w.Merchant.Died += _ => deaths++;

        w.Merchant.OnDeath(); w.Merchant.OnDeath();
        Assert.IsTrue(w.Merchant.IsDead);
        Assert.IsFalse(w.Trade.TryGetSession(w.Player, out _));
        Assert.AreSame(offered, w.Player.Inventory.Inventory.Content[64]);
        Assert.IsFalse(w.Merchant.TryUse(w.Player));
        Assert.IsFalse(w.Activation.TryGetBinding(w.Merchant, out _));
        Assert.AreSame(playfield, w.Merchant.Playfield);
        Assert.AreEqual(0, w.Registry.Dynels().OfType<Corpse>().Count());
        Assert.AreEqual(0, deaths);

        w.Merchant.Tick(2.0);
        Assert.AreEqual(0, w.Registry.Dynels().OfType<Corpse>().Count());
        w.Merchant.Tick(0.5);
        var corpse = w.Registry.Dynels().OfType<Corpse>().Single();
        Assert.AreEqual(w.Merchant.Identity, corpse.Owner);
        Assert.AreSame(playfield, corpse.Playfield);
        Assert.IsNotNull(corpse.Cell);
        Assert.IsNull(w.Merchant.Playfield); Assert.IsNull(w.Merchant.Cell); Assert.IsNull(shop.Playfield);
        Assert.IsFalse(w.Registry.TryGet(w.Merchant.Identity, out _));
        Assert.AreEqual(1, deaths);
        w.Merchant.OnDeath(); w.Merchant.Tick(10);
        Assert.AreSame(corpse, w.Registry.Dynels().OfType<Corpse>().Single());
        Assert.AreEqual(1, deaths);
        Assert.AreEqual(0, w.Persistence.Calls); Assert.AreEqual(0, w.Snapshots.Writes.Count);
        // This proves lifecycle only. Generic Corpse still has Biofreak-specific visual constants.
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DelayedDeathDetachesOldActorWithoutDeletingReplacementRegistryOrVisibilityOwner(bool registerReplacementVisibility)
    {
        using var w = new AcceptedSubwayShopRuntimeTests.World(allowMissingCatMesh: registerReplacementVisibility);
        var playfield = w.Player.Playfield!;
        var locality = playfield.GetRequiredService<PlayfieldLocality>();
        if (registerReplacementVisibility)
        {
            locality.RegisterDynel(w.Player);
            locality.ActivatePlayerVisibility(w.Player);
        }
        w.Merchant.OnDeath();
        var replacement = new NpcCharacter(w.Merchant.Identity, new StubItemBuilder())
        {
            Name = "Replacement owner", Playfield = playfield, Position = w.Merchant.Position
        };
        w.Registry.Register(replacement);
        if (registerReplacementVisibility)
        {
            locality.RegisterDynel(replacement);
            Assert.IsTrue(locality.SnapshotObservers(replacement).Contains(w.Player));
        }
        w.Session.Messages.Clear();

        w.Merchant.Tick(2.5);
        Assert.IsTrue(w.Registry.TryGet(replacement.Identity, out var current));
        Assert.AreSame(replacement, current); Assert.AreSame(playfield, replacement.Playfield);
        Assert.IsNull(w.Merchant.Playfield); Assert.IsNull(w.Merchant.Cell); Assert.IsNull(w.Merchant.Shop!.Playfield);
        Assert.IsFalse(w.Activation.TryGetBinding(w.Merchant, out _));
        Assert.IsFalse(w.Activation.TryGetBinding(replacement, out _));
        Assert.AreEqual(1, w.Registry.Dynels().OfType<Corpse>().Count());
        if (registerReplacementVisibility)
        {
            Assert.IsNotNull(replacement.Cell);
            Assert.IsTrue(replacement.Cell.Occupants.Contains(replacement));
            Assert.IsTrue(locality.SnapshotObservers(replacement).Contains(w.Player));
            Assert.IsFalse(w.Session.Messages.OfType<DespawnMessage>().Any(message => message.Identity == replacement.Identity));
            Assert.IsFalse(w.Session.Messages.OfType<CorpseFullUpdateMessage>().Single().Stats
                .Any(stat => stat.Value1 == CharacterStat.CATMesh)); // Missing lookup remains missing, not an invented corpse mesh.
            w.Session.Messages.Clear();
            var announcement = new CharacterActionMessage { Identity = replacement.Identity, Action = CharacterActionType.Death };
            locality.Announce(replacement, announcement);
            Assert.AreSame(announcement, w.Session.Messages.Single());
        }
    }

    [TestMethod]
    public void CorpseFixtureMissingCatMeshIsExplicitAndDoesNotWeakenTheDefaultGameDataStub()
    {
        var ordinary = new StubGameData(HashItemCatalog.Parse("{}", "{}"));
        Assert.ThrowsException<NotSupportedException>(() => ordinary.TryGetCatMesh(208640, out _));
        var corpse = new StubGameData(HashItemCatalog.Parse("{}", "{}"), allowMissingCatMesh: true);
        Assert.IsFalse(corpse.TryGetCatMesh(208640, out int catMesh)); Assert.AreEqual(0, catMesh);
    }

    [TestMethod]
    public void RealSpawnServiceExpiresCorpseAndClosesAnEmptyOpenedCorpseWithoutWallClockWaits()
    {
        using var w = new AcceptedSubwayShopRuntimeTests.World();
        w.Merchant.OnDeath(); w.Merchant.Tick(2.5);
        var expired = w.Registry.Dynels().OfType<Corpse>().Single();
        // Only the test deadline changes; production expiry and cleanup methods execute unchanged.
        typeof(Corpse).GetField("<ExpiresAtUtc>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(expired, DateTime.UtcNow.AddMinutes(-1));
        w.Spawns.Tick();
        Assert.IsFalse(w.Registry.TryGet(expired.Identity, out _));
        Assert.IsNull(expired.Playfield); Assert.IsNull(expired.Cell);

        var empty = w.Spawns.SpawnCorpse(w.Merchant);
        Assert.AreEqual(0, empty.Loot.Content.Count); Assert.IsFalse(empty.ShouldDespawnEmpty);
        w.Spawns.Tick(); Assert.IsTrue(w.Registry.TryGet(empty.Identity, out _)); // Unopened empty corpses do not expire early.
        Assert.IsTrue(empty.TryUse(w.Player)); Assert.IsTrue(empty.IsOpen); Assert.IsTrue(empty.WasOpened);
        Assert.AreEqual(1, empty.InventoryHandle);
        typeof(LootableDynel).GetField("_emptyDespawnAtUtc", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(empty, (DateTime?)DateTime.UtcNow.AddMinutes(-1));
        w.Session.Messages.Clear(); w.Spawns.Tick();
        Assert.IsFalse(w.Registry.TryGet(empty.Identity, out _));
        Assert.IsFalse(empty.IsOpen); Assert.IsNull(empty.Playfield); Assert.IsNull(empty.Cell);
        Assert.IsTrue(w.Session.Messages.OfType<ActionMessage>().Any(message => message.Identity == empty.Identity
            && message.ActionIdentity == 0x66 && message.Target == w.Player.Identity));
        Assert.AreEqual(0, w.Persistence.Calls); Assert.AreEqual(0, w.Snapshots.Writes.Count);
    }
}
