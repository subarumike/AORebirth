namespace ZoneEngine_New.Tests;

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Playfield.Locality;
using Vector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
public sealed class AcceptedQuestPropTests
{
    static Identity Slot => new() { Type = IdentityType.Inventory, Instance = 64 };
    static Identity Target(int instance) => new() { Type = IdentityType.Terminal, Instance = instance };
    static AcceptedQuestPropService Create(AuthoredQuestTests.World w, bool complete = true)
    {
        var catalog = new StubCatalog();
        if (complete) catalog.Add(295604, 1).Add(295620, 1);
        return new(w.Player.Playfield!, w.Registry, w.Player.Playfield!.GetRequiredService<PlayfieldLocality>(), catalog, w.Service);
    }

    [TestMethod]
    public void BothAcceptedPropsMaterializeExactIdentityTemplatePositionAndEightCapturedStatsOnce()
    {
        using var w = new AuthoredQuestTests.World(); var service = Create(w); service.Activate(); service.Activate();
        Assert.AreEqual(3, w.Registry.Dynels().Count()); // two props and the actual player
        foreach (var definition in AcceptedQuestPropService.Definitions)
        {
            Assert.IsTrue(w.Registry.TryGet(Target(definition.Instance), out var prop));
            var wire = (SimpleItemFullUpdateMessage)prop!.BuildSpawnMessage();
            Assert.AreEqual(definition.Identity, wire.Identity);
            Assert.AreEqual(definition.X, wire.Coordinate.X); Assert.AreEqual(definition.Y, wire.Coordinate.Y);
            Assert.AreEqual(definition.Z, wire.Coordinate.Z); Assert.AreEqual(6553, wire.Playfield);
            Assert.AreEqual(8, wire.Stats.Length);
            Assert.AreEqual(0x80003201u, wire.Stats.Single(stat => stat.Value1 == CharacterStat.Flags).Value2);
            Assert.AreEqual((uint)definition.TemplateId, wire.Stats.Single(stat => stat.Value1 == CharacterStat.ACGItemTemplateID).Value2);
        }
    }

    [TestMethod]
    public void MissingCatalogOrConflictingExistingIdentityCannotMintAQuestCapability()
    {
        using var w = new AuthoredQuestTests.World(); var missing = Create(w, false); missing.Activate();
        Assert.AreEqual(2, missing.Unavailable.Count); Assert.AreEqual(1, w.Registry.Dynels().Count());
        var impostor = new Dynel(Target(AcceptedQuestPropService.RemainsInstance)) { Playfield = w.Player.Playfield };
        w.Registry.Register(impostor); var conflict = Create(w); conflict.Activate();
        Assert.AreEqual(1, conflict.Unavailable.Count);
        Assert.IsTrue(w.Registry.TryGet(impostor.Identity, out var still)); Assert.AreSame(impostor, still);
        Assert.IsFalse(conflict.TryUseRemains(w.Session, impostor.Identity, () => Assert.Fail("Unaccepted target emitted success.")));
        Assert.AreEqual(0, w.Dao.Calls);
    }

    [TestMethod]
    public void StrongboxRequiresExactLiveTargetAndDurableOwnedLockpickThenAcknowledgesAfterCommit()
    {
        using var w = new AuthoredQuestTests.World(); var service = Create(w); service.Activate();
        var pick = w.Add(95577); w.Activate(AuthoredQuestService.Strongbox);
        var target = Target(AcceptedQuestPropService.StrongboxInstance);
        w.Player.Position = new Vector3(3409.956f, 9.01f, 893.5452f);
        int acknowledgements = 0;
        w.Dao.BeforeCommit = _ => Assert.AreEqual(0, acknowledgements);
        Assert.IsTrue(service.TryUseStrongbox(w.Session, Slot, target, () => acknowledgements++));
        Assert.AreEqual(1, acknowledgements); Assert.AreSame(pick, w.Player.Inventory.Inventory.Content[64]);
        Assert.AreEqual(1, w.Player.Inventory.Inventory.Content.Values.Count(item => item.LowId == 248306));
    }

    [TestMethod]
    public void RangeWrongSourceSlotReplacedTargetAndShutdownCannotProgressStrongbox()
    {
        using var w = new AuthoredQuestTests.World(); var service = Create(w); service.Activate();
        w.Add(95577); w.Activate(AuthoredQuestService.Strongbox);
        var target = Target(AcceptedQuestPropService.StrongboxInstance);
        Action noAck = () => Assert.Fail("Rejected target emitted success.");
        Assert.IsFalse(service.TryUseStrongbox(w.Session, Slot, target, noAck));
        w.Player.Position = new Vector3(3409.956f, 9.01f, 893.5452f);
        Assert.IsFalse(service.TryUseStrongbox(w.Session, new() { Type = IdentityType.Inventory, Instance = 65 }, target, noAck));
        w.Registry.Register(new Dynel(target) { Playfield = w.Player.Playfield, Position = w.Player.Position });
        Assert.IsFalse(service.TryUseStrongbox(w.Session, Slot, target, noAck));
        service.Shutdown(); Assert.IsFalse(service.TryUseStrongbox(w.Session, Slot, target, noAck));
        Assert.AreEqual(0, w.Dao.Calls);
    }

    [TestMethod]
    public void RemainsRejectForeignSessionAndPreserveQuestOnRolledBackItemGrant()
    {
        using var w = new AuthoredQuestTests.World(); var service = Create(w); service.Activate();
        w.Activate(AuthoredQuestService.FindThief);
        w.Player.Position = new Vector3(3424.016f, 0.01011355f, 887.8564f);
        var target = Target(AcceptedQuestPropService.RemainsInstance); int acks = 0;
        using var foreign = new AuthoredQuestTests.World();
        Assert.IsFalse(service.TryUseRemains(foreign.Session, target, () => acks++));
        w.Dao.BeforeCommit = _ => throw new InvalidOperationException("injected rollback after planning");
        Assert.IsFalse(service.TryUseRemains(w.Session, target, () => acks++));
        Assert.AreEqual(0, acks); Assert.IsFalse(w.Player.Inventory.Inventory.Content.Values.Any(item => item.LowId == 295618));
        w.Dao.BeforeCommit = null;
        Assert.IsTrue(service.TryUseRemains(w.Session, target, () => acks++));
        Assert.AreEqual(1, acks);
        Assert.AreEqual(200, w.Player.Inventory.Inventory.Content.Values.Single(item => item.LowId == 295618).Quality);
    }
}
