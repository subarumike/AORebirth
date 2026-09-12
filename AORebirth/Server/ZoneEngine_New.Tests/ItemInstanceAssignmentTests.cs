namespace ZoneEngine_New.Tests;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Inventory;

[TestClass]
public sealed class ItemInstanceAssignmentTests
{
    [TestMethod]
    public void FreshAllocationSetsBothIdsWithoutPersistingOrChangingTheStack()
    {
        var item = new Item { StackCount = 7, Definition = new ItemTemplate
            { ItemType = 1, DynelType = (int)IdentityType.WeaponInstance } };
        item.AssignInstanceId(910001);
        Assert.AreEqual(910001, item.InstanceId);
        Assert.AreEqual(new Identity { Type = IdentityType.WeaponInstance, Instance = 910001 }, item.Identity);
        Assert.IsFalse(item.IsPersisted);
        Assert.AreEqual(7, item.StackCount);
    }

    [TestMethod]
    public void BagAllocationUsesTheExistingContainerIdentityConversion()
    {
        var item = new Item { Definition = new ItemTemplate { ItemType = (int)IdentityType.Backpack } };
        item.AssignInstanceId(910002);
        Assert.AreEqual(IdentityType.Container, item.Identity.Type);
        Assert.AreEqual(item.InstanceId, item.Identity.Instance);
        Assert.IsFalse(item.IsPersisted);
    }

    [TestMethod]
    public void InvalidOrRepeatedAllocationCannotRewriteExistingOwnership()
    {
        var item = new Item { Definition = new ItemTemplate { ItemType = (int)IdentityType.WeaponInstance } };
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => item.AssignInstanceId(0));
        Assert.AreEqual(0, item.InstanceId);
        item.AssignInstanceId(910003);
        Assert.ThrowsExactly<InvalidOperationException>(() => item.AssignInstanceId(910004));
        Assert.AreEqual(910003, item.InstanceId);
        Assert.AreEqual(910003, item.Identity.Instance);
    }

    [TestMethod]
    public void PersistedOrPreboundItemsCannotBeRestamped()
    {
        var persisted = new Item { IsPersisted = true };
        Assert.ThrowsExactly<InvalidOperationException>(() => persisted.AssignInstanceId(910005));
        var bound = new Item { Identity = new Identity { Type = IdentityType.WeaponInstance, Instance = 25 } };
        Assert.ThrowsExactly<InvalidOperationException>(() => bound.AssignInstanceId(910006));
        Assert.AreEqual(25, bound.Identity.Instance);
        Assert.AreEqual(0, bound.InstanceId);
    }

    [TestMethod]
    public void CatalogWithoutAWorldTypeDoesNotAcquireAnInventedDynelIdentity()
    {
        var item = new Item { Definition = new ItemTemplate() };
        item.AssignInstanceId(910007);
        Assert.AreEqual(910007, item.InstanceId);
        Assert.AreEqual(Identity.None, item.Identity);
        Assert.IsFalse(item.IsPersisted);
    }
}
