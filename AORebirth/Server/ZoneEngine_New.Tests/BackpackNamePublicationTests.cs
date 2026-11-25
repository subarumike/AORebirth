namespace ZoneEngine_New.Tests;

using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Network;

[TestClass]
public sealed class BackpackNamePublicationTests
{
    // The client resolves a slot to its backpack's durable identity only when this bit is set.
    // It reads the custom label from the per-character file keyed by that identity.
    const short InstanceIdentityFlag = 0x02;

    [TestMethod]
    public void FullCharacterReconstructsNamesForDistinctBagsWithTheSameTemplate()
    {
        Player player = TestWorld.CreatePlayer(1);
        Item first = Backpack(17);
        Item second = Backpack(18);
        Assert.IsTrue(player.Inventory.Inventory.Add(64, first));
        Assert.IsTrue(player.Inventory.Inventory.Add(65, second));
        Assert.IsTrue(player.Inventory.Inventory.Add(66, TestWorld.CreateItem(instanceId: 19)));

        var packet = RoundTrip(player.BuildFullCharacterMessage(), player.Identity);

        var firstSlot = packet.InventorySlots.Single(slot => slot.Placement == 64);
        var secondSlot = packet.InventorySlots.Single(slot => slot.Placement == 65);
        Assert.AreEqual(first.Identity, firstSlot.Identity);
        Assert.AreEqual(second.Identity, secondSlot.Identity);
        Assert.AreEqual(firstSlot.ItemHighId, secondSlot.ItemHighId);
        Assert.AreEqual(InstanceIdentityFlag, (short)(firstSlot.Flags & InstanceIdentityFlag));
        Assert.AreEqual(InstanceIdentityFlag, (short)(secondSlot.Flags & InstanceIdentityFlag));
        Assert.AreEqual(0, packet.InventorySlots.Single(slot => slot.Placement == 66).Flags & InstanceIdentityFlag);
    }

    [TestMethod]
    public void EveryBankRefreshCarriesTheNamedBagIdentityAtItsRealPlacement()
    {
        Player player = TestWorld.CreatePlayer(1);
        Item bag = Backpack(17);
        Assert.IsTrue(player.Inventory.Bank.Add(12, bag));
        Assert.IsTrue(player.Inventory.Bank.Add(13, TestWorld.CreateItem(instanceId: 18)));

        for (int refresh = 0; refresh < 2; refresh++)
        {
            var packet = RoundTrip(player.Inventory.BuildBankMessage(player.Identity), player.Identity);
            var slot = packet.BankSlots.Single(entry => entry.Placement == 12);
            Assert.AreEqual(bag.Identity, slot.Identity);
            Assert.AreEqual(bag.HighId, slot.ItemHighId);
            Assert.AreEqual(1, (int)slot.Count);
            Assert.AreEqual(InstanceIdentityFlag, (short)(slot.Flags & InstanceIdentityFlag));
            Assert.AreEqual(0, packet.BankSlots.Single(entry => entry.Placement == 13).Flags & InstanceIdentityFlag);
        }
    }

    [TestMethod]
    public void BackpackBankRoundTripAndReconnectKeepTheSameRowsAndWireIdentity()
    {
        using var world = new InventoryActionTests.World();
        Player player = world.Player;
        Item bag = Backpack(17);
        Item child = TestWorld.CreateItem(instanceId: 18);
        Container carried = player.Inventory.Inventory;
        Container bank = player.Inventory.Bank;
        bank.IsHydrated = true;
        var originalSlot = new Identity { Type = IdentityType.Inventory, Instance = 64 };
        Assert.IsTrue(carried.Add(originalSlot.Instance, bag));
        world.Persistence.Rows[bag.InstanceId] = bag.ToRecord(carried.Identity, originalSlot.Instance, 1);
        Container bagPage = player.Inventory.GetOrCreateBackpackPage(bag, bag.Identity, originalSlot);
        Assert.IsTrue(bagPage.Add(3, child));
        world.Persistence.Rows[child.InstanceId] = child.ToRecord(bag.Identity, 3, 1);

        var moves = new InventoryMoveService(new StubLogger(), world.Flush, world.Actions);
        var bankAccess = new Identity { Type = IdentityType.Bank, Instance = player.Identity.Instance };
        moves.Handle(player, new ClientContainerAddItemMessage { Source = originalSlot, Target = bankAccess });
        var initialBank = RoundTrip(player.Inventory.BuildBankMessage(player.Identity), player.Identity).BankSlots.Single();
        Assert.AreEqual(bag.Identity, initialBank.Identity);
        Assert.AreEqual(InstanceIdentityFlag, (short)(initialBank.Flags & InstanceIdentityFlag));

        moves.Handle(player, new ClientMoveItemToInventoryMessage
        {
            SourceContainer = new Identity { Type = IdentityType.BankByRef, Instance = initialBank.Placement },
            TargetPlacement = 66
        });
        Assert.AreSame(bag, carried.Content[66]);
        Assert.IsFalse(bank.Content.Any());

        moves.Handle(player, new ClientContainerAddItemMessage
        {
            Source = new Identity { Type = IdentityType.Inventory, Instance = 66 }, Target = bankAccess
        });
        var reopenedBank = RoundTrip(player.Inventory.BuildBankMessage(player.Identity), player.Identity).BankSlots.Single();
        Assert.AreEqual(bag.Identity, reopenedBank.Identity);
        Assert.AreEqual(InstanceIdentityFlag, (short)(reopenedBank.Flags & InstanceIdentityFlag));
        Assert.AreEqual(bag.InstanceId, world.Persistence.Rows[child.InstanceId].ContainerInstance);
        Assert.AreEqual(3, world.Persistence.Rows[child.InstanceId].ContainerPlacement);

        moves.Handle(player, new ClientMoveItemToInventoryMessage
        {
            SourceContainer = new Identity { Type = IdentityType.BankByRef, Instance = reopenedBank.Placement },
            TargetPlacement = 67
        });
        var catalog = new StubCatalog().Add(bag.LowId, quality: 1);
        var fresh = TestWorld.CreatePlayer(player.Identity.Instance);
        fresh.Inventory.Apply(new CharacterHydrationResult { Items = [world.Persistence.Rows[bag.InstanceId]] },
            fresh.Identity.Instance, new ItemBuilder(catalog, new StubLogger()));
        var reconnect = RoundTrip(fresh.BuildFullCharacterMessage(), fresh.Identity);
        var slot = reconnect.InventorySlots.Single(entry => entry.Placement == 67);
        Assert.AreEqual(bag.Identity, slot.Identity);
        Assert.AreEqual(InstanceIdentityFlag, (short)(slot.Flags & InstanceIdentityFlag));
    }

    [TestMethod]
    public void TradeViewRefreshPreservesPrivateWeaponBindingsForWornAndUnequippedRows()
    {
        using var world = new InventoryActionTests.World();
        Player player = world.Player;
        Item worn = TestWorld.CreateItem(instanceId: 19);
        Item unequipped = TestWorld.CreateItem(instanceId: 20);
        Assert.IsTrue(player.Inventory.Equipment.Add(6, worn));
        Assert.IsTrue(player.Inventory.Equipment.Add(8, unequipped));

        var wornAlias = new Identity { Type = IdentityType.WeaponInstance, Instance = 1019 };
        var carriedAlias = new Identity { Type = IdentityType.WeaponInstance, Instance = 1020 };
        player.SendOwnWeaponInstance(worn, new WeaponItemFullUpdateMessage { Identity = wornAlias });
        player.SendOwnWeaponInstance(unequipped, new WeaponItemFullUpdateMessage { Identity = carriedAlias });
        Assert.AreSame(unequipped, player.Inventory.Equipment.Remove(8));
        Assert.IsTrue(player.Inventory.Inventory.Add(64, unequipped));

        var view = RoundTrip(player.Inventory.BuildCarriedInventoryUpdate(player.Identity), player.Identity);
        var weaponRow = view.Entries.Single(entry => entry.Slotnumber == 6);
        var carriedRow = view.Entries.Single(entry => entry.Slotnumber == 64);
        Assert.AreEqual(wornAlias, weaponRow.Identity);
        Assert.AreEqual(carriedAlias, carriedRow.Identity);
        Assert.AreEqual(InstanceIdentityFlag, (short)(weaponRow.UnknownFlags & InstanceIdentityFlag));
        Assert.AreEqual(InstanceIdentityFlag, (short)(carriedRow.UnknownFlags & InstanceIdentityFlag));

        player.Inventory.ResetClientWeaponBindings(player.BuildFullCharacterMessage().InventorySlots);
        var fresh = RoundTrip(player.Inventory.BuildCarriedInventoryUpdate(player.Identity), player.Identity);
        Assert.AreNotEqual(carriedAlias, fresh.Entries.Single(entry => entry.Slotnumber == 64).Identity);
    }

    [TestMethod]
    public void TradeViewRefreshPreservesAWeaponBindingFromFullCharacterAfterUnequipping()
    {
        Player player = TestWorld.CreatePlayer(1);
        Item weapon = TestWorld.CreateItem(instanceId: 21);
        weapon.Identity = new Identity { Type = IdentityType.WeaponInstance, Instance = weapon.InstanceId };
        weapon.Definition.Stats[CharacterStat.ItemClass] = (int)AORebirth.Enums.ItemClass.Weapon;
        weapon.Definition.Stats[CharacterStat.WeaponMesh] = 1;
        const int rightHand = (int)AORebirth.Enums.WeaponSlots.Righthand;
        Assert.IsTrue(player.Inventory.Equipment.Add(rightHand, weapon));

        var full = RoundTrip(player.BuildFullCharacterMessage(), player.Identity);
        var equippedRow = full.InventorySlots.Single(slot => slot.Placement == rightHand);
        Assert.AreEqual(InstanceIdentityFlag, (short)(equippedRow.Flags & InstanceIdentityFlag));
        player.Inventory.ResetClientWeaponBindings(full.InventorySlots);
        Assert.AreSame(weapon, player.Inventory.Equipment.Remove(rightHand));
        Assert.IsTrue(player.Inventory.Inventory.Add(64, weapon));

        var view = RoundTrip(player.Inventory.BuildCarriedInventoryUpdate(player.Identity), player.Identity);
        var carriedRow = view.Entries.Single(entry => entry.Slotnumber == 64);
        Assert.AreEqual(equippedRow.Identity, carriedRow.Identity);
        Assert.AreEqual(equippedRow.Flags, carriedRow.UnknownFlags);
    }

    static Item Backpack(int instance)
    {
        var item = new Item
        {
            InstanceId = instance,
            IsPersisted = true,
            Identity = new Identity { Type = IdentityType.Backpack, Instance = instance },
            LowId = 1000,
            HighId = 1000,
            Quality = 1,
            Definition = new ItemTemplate
            {
                Id = 1000,
                Name = "Fixture Backpack",
                ItemType = (int)IdentityType.Backpack
            }
        };
        item.ApplyContainerIdentityIfBag();
        return item;
    }

    static T RoundTrip<T>(T body, Identity recipient) where T : MessageBody
    {
        var codec = new ZoneMessageCodec();
        return (T)codec.Deserialize(codec.Serialize(body, 0, recipient.Instance))!.Body;
    }
}
