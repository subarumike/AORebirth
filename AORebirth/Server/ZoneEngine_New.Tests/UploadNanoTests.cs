using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Inventory;

namespace ZoneEngine_New.Tests;

// Item retirement and the uploaded nano commit together before client publication.
[TestClass]
public sealed class UploadNanoTests
{
    const int NanoId = 25984;
    static Identity Slot => new() { Type = IdentityType.Inventory, Instance = 64 };

    [TestMethod]
    public void UsingACrystalCommitsTheNanoAndItemBeforeTellingTheClient()
    {
        using var world = new InventoryActionTests.World();
        var crystal = Crystal(world, NanoId);
        Assert.IsTrue(world.Actions.TryUseNanoCrystal(world.Player, Slot, crystal));
        CollectionAssert.Contains(world.Player.UploadedNanoIds, NanoId);
        Assert.IsFalse(world.Player.HasDirtyUploadedNanos);
        Assert.AreEqual(1, world.Persistence.Calls);
        Assert.AreEqual((int)IdentityType.None, world.Persistence.Rows[11].ContainerType);
        Assert.IsFalse(world.Player.Inventory.Inventory.Content.ContainsKey(64));
        var action = world.Session.Messages.OfType<CharacterActionMessage>().Single(m => m.Action == CharacterActionType.UploadNano);
        Assert.AreEqual((int)IdentityType.NanoProgram, action.Parameter1);
        Assert.AreEqual(NanoId, action.Parameter2);
    }

    [TestMethod]
    public void AKnownNanoDoesNotSpendItsCrystalOrWriteTheDatabase()
    {
        using var world = new InventoryActionTests.World();
        var crystal = Crystal(world, NanoId);
        world.Player.TryAddUploadedNano(NanoId);
        Assert.IsFalse(world.Actions.TryUseNanoCrystal(world.Player, Slot, crystal));
        Assert.AreEqual(1, world.Player.UploadedNanoIds.Count);
        Assert.IsFalse(world.Player.HasDirtyUploadedNanos);
        Assert.AreSame(crystal, world.Player.Inventory.Inventory.Content[64]);
        Assert.AreEqual(1, crystal.StackCount);
        Assert.AreEqual(0, world.Persistence.Calls);
        Assert.AreEqual(0, world.Session.Messages.OfType<CharacterActionMessage>().Count());
        Assert.AreEqual(1, world.Session.Messages.OfType<ChatTextMessage>().Count());
    }

    [TestMethod]
    public void AnInvalidNanoFunctionCannotSpendTheCrystal()
    {
        using var world = new InventoryActionTests.World();
        var crystal = Crystal(world, 0);
        Assert.IsFalse(world.Actions.TryUseNanoCrystal(world.Player, Slot, crystal));
        Assert.AreSame(crystal, world.Player.Inventory.Inventory.Content[64]);
        Assert.AreEqual(0, world.Persistence.Calls);
    }

    [TestMethod]
    public void FailedUploadPreservesCrystalAndDoesNotAnnounceSuccess()
    {
        using var world = new InventoryActionTests.World();
        var crystal = Crystal(world, NanoId);
        world.Persistence.Failure = new InvalidOperationException("commit rejected");
        Assert.IsFalse(world.Actions.TryUseNanoCrystal(world.Player, Slot, crystal));
        Assert.AreSame(crystal, world.Player.Inventory.Inventory.Content[64]);
        Assert.AreEqual(0, world.Player.UploadedNanoIds.Count);
        Assert.AreEqual(0, world.Session.Messages.OfType<CharacterActionMessage>().Count());
    }

    static Item Crystal(InventoryActionTests.World world, int nanoId)
    {
        var item = world.Add(11, 1, lowId: 26015);
        item.Definition.Stats[CharacterStat.Can] = (int)(CanFlags.Use | CanFlags.Consume);
        item.SpellList[EventType.OnUse] = new List<ItemSpell>
        {
            new() { FunctionType = (int)FunctionType.UploadNano, Target = (int)ItemTarget.User, Arguments = new List<object> { nanoId } }
        };
        return item;
    }
}
