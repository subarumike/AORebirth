using System.Collections.Generic;
using System.Linq;
using AORebirth.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Inventory;

namespace ZoneEngine_New.Tests;

[TestClass]
public sealed class UploadNanoTests
{
    const int NanoId = 25984;

    [TestMethod]
    public void UsingACrystalUploadsThroughTheSpellFunctionAndConsumesTheItem()
    {
        using var world = new InventoryActionTests.World();
        var crystal = Crystal(world, NanoId);
        Assert.IsTrue(world.Use(crystal));
        CollectionAssert.Contains(world.Player.UploadedNanoIds, NanoId);
        Assert.IsFalse(world.Player.Inventory.Inventory.Content.ContainsKey(64));
        var action = world.Session.Messages.OfType<CharacterActionMessage>().Single(m => m.Action == CharacterActionType.UploadNano);
        Assert.AreEqual((int)IdentityType.NanoProgram, action.Parameter1);
        Assert.AreEqual(NanoId, action.Parameter2);
        Assert.IsTrue(world.Session.Messages.OfType<CharacterActionMessage>().Any(m => m.Action == CharacterActionType.DeleteItem));
    }

    [TestMethod]
    public void AKnownNanoDoesNotSpendItsCrystal()
    {
        using var world = new InventoryActionTests.World();
        var crystal = Crystal(world, NanoId);
        world.Player.TryAddUploadedNano(NanoId);
        Assert.IsFalse(world.Use(crystal));
        Assert.AreEqual(1, world.Player.UploadedNanoIds.Count);
        Assert.AreSame(crystal, world.Player.Inventory.Inventory.Content[64]);
        Assert.AreEqual(1, crystal.StackCount);
        Assert.AreEqual(0, world.Session.Messages.OfType<CharacterActionMessage>().Count());
        Assert.AreEqual(1, world.Session.Messages.OfType<ChatTextMessage>().Count());
    }

    [TestMethod]
    public void AnInvalidNanoFunctionCannotSpendTheCrystal()
    {
        using var world = new InventoryActionTests.World();
        var crystal = Crystal(world, 0);
        Assert.IsFalse(world.Use(crystal));
        Assert.AreSame(crystal, world.Player.Inventory.Inventory.Content[64]);
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
