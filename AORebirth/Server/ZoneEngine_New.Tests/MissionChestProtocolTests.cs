namespace ZoneEngine_New.Tests;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using AORebirth.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Serialization;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.MessageHandlers;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using AoQuaternion = AORebirth.Core.Vector.Quaternion;
using AoVector3 = AORebirth.Core.Vector.Vector3;
using PacketReader = SmokeLounge.AOtomation.Messaging.Serialization.StreamReader;
using PacketWriter = SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter;

[TestClass]
public sealed class MissionChestProtocolTests
{
    // Official mission-20261011-004128-b8e7bf2e raw-packets.csv, ordinal 480.
    // CSV SHA256 E08048B9AB6A3DF164068667E19263EF82801B142EEA2904F4D9FBBB28AB5141.
    // Complete network packet, not a reconstructed corpse or backpack packet.
    const string WorldTreasurePacket =
        "0031000A0001009B00000DB74A6CF26A465A5D730000C7490BBA6B1F000000000B00000000000000004374666740A33333429C999A0000000000000000000000003F8000000020D4BC000F424F00000007006F00001B9700000000200018410000001700027A8F000002BD00000001000002BE00027A8F000002BF00027A8F0000019C0000000100000000000000020000000F000003F100000003";

    [TestMethod]
    public void PositionedChestSpawnReproducesEveryVerifiedRetailBodyByte()
    {
        byte[] packet = Convert.FromHexString(WorldTreasurePacket);
        Assert.AreEqual(155, packet.Length);
        Assert.AreEqual("E86620C76173EE0D943B94C144C40E4EDBA87B5F27B406CD93E62884EB42FD31",
            Convert.ToHexString(SHA256.HashData(packet)));
        Chest chest = CapturedChest(packet);
        byte[] body = Serialize(chest.BuildSpawnMessage());
        CollectionAssert.AreEqual(packet[16..], body);
        Assert.AreEqual("D2332D057A7BABBBDE25C77E726685654D0456838E310DB11C43106A822E96AD",
            Convert.ToHexString(SHA256.HashData(body)));

        ChestItemFullUpdateMessage decoded = Deserialize<ChestItemFullUpdateMessage>(body);
        Assert.AreEqual(Identity.None, decoded.Owner);
        Assert.AreEqual(0x20D4BC, decoded.PlayfieldId);
        Assert.AreEqual(7, decoded.StateMachine.Instance);
        Assert.AreEqual((short)111, decoded.Unknown5);
        Assert.AreEqual(6, decoded.Stats.Length);
        Assert.AreEqual(162447u, decoded.Stats.Single(v => v.Value1 == CharacterStat.ACGItemTemplateID).Value2);
        Assert.AreEqual(15, decoded.Unknown8);
        Assert.AreEqual(0, decoded.UnknownArray.Length);
        Assert.AreEqual(3, decoded.Unknown9);
    }

    [TestMethod]
    public void SpawnUsesCurrentRuntimeIdentityPlayfieldAndTransform()
    {
        byte[] packet = Convert.FromHexString(WorldTreasurePacket);
        Chest original = CapturedChest(packet);
        var chest = new Chest(new Identity { Type = IdentityType.Container, Instance = 90001 })
        {
            SpawnDefinition = original.SpawnDefinition,
            Position = new AoVector3(17, 4, 29),
            Rotation = new AoQuaternion(0, 1, 0, 0),
            Playfield = BarePlayfield(123456)
        };
        var message = (ChestItemFullUpdateMessage)chest.BuildSpawnMessage();
        Assert.AreEqual(chest.Identity, message.Identity);
        Assert.AreEqual(123456, message.PlayfieldId);
        Assert.AreEqual(17f, message.Coordinate.X);
        Assert.AreEqual(4f, message.Coordinate.Y);
        Assert.AreEqual(29f, message.Coordinate.Z);
        Assert.AreEqual(1f, message.Heading.Y);
        Assert.AreEqual(0f, message.Heading.W);
        Assert.AreEqual(Identity.None, message.Owner);
    }

    [TestMethod]
    public void SpawnUsesClientResourceIdentityInsideAProxyPlayfield()
    {
        Chest chest = CreateCapturedChest();
        chest.Playfield = BarePlayfield(ProxyPlayfieldIds.First + 123, resourceId: 456);
        Assert.AreNotEqual(chest.Playfield.Identity.Instance, chest.Playfield.ClientPlayfieldId);

        var message = Deserialize<ChestItemFullUpdateMessage>(Serialize(chest.BuildSpawnMessage()));
        Assert.AreEqual(456, message.PlayfieldId);
        Assert.AreEqual(chest.Identity, message.Identity);
        Assert.AreEqual(ProxyPlayfieldIds.First + 123, chest.Playfield.Identity.Instance);
    }

    [TestMethod]
    public void OwnedBackpackControlRetainsItsDifferentParentedWireShape()
    {
        byte[] body = Convert.FromHexString(
            "465A5D730000C7490A22540A000000000B0000C3503CAC6F14000002DF000F424F00000000014400001B97000000000000000100000017000462C4000002BD00000001000002BE000462C4000002BF000462C40000019C00000001000000000000000200000032000003F100000003");
        ChestItemFullUpdateMessage message = Deserialize<ChestItemFullUpdateMessage>(body);
        Assert.AreEqual(IdentityType.CanbeAffected, message.Owner.Type);
        CollectionAssert.AreEqual(body, Serialize(message));
    }

    [TestMethod]
    public void RetailContentsAndTakeUseExactChestHandleAndSlotSeparatelyFromSpawn()
    {
        // Same official session, ordinals 1094, 1105 and 1106. Contents count is not a generation policy.
        byte[] contents = Convert.FromHexString(
            "4E5369760000C3504A6CF26A010000001500000002000013B5000000000021000100000000000000000001AFBC0001AFBD0000000200000000000000010021000100000000000000000001A8260001A8270000000200000000000000020021000100000000000000000000DBBA0000DBBA000000040000000000000003002100010000000000000000000217FB000217FC00000002000000000000C7490BBA6B1F0000007500000001");
        var inventory = Deserialize<InventoryUpdateMessage>(contents);
        Assert.AreEqual(new Identity { Type = IdentityType.Container, Instance = 196766495 }, inventory.BagIdentity);
        Assert.AreEqual(117, inventory.SlotnumberInMainInventory);
        Assert.AreEqual(21, inventory.NumberOfSlots);
        Assert.AreEqual(4, inventory.Entries.Length);
        var item = inventory.Entries.Single(v => v.Slotnumber == 2);
        Assert.AreEqual(56250, item.LowId);
        Assert.AreEqual(56250, item.HighId);
        Assert.AreEqual(4, item.Quality);
        CollectionAssert.AreEqual(contents, Serialize(inventory));

        byte[] request = Convert.FromHexString("5469373F0000C3504A6CF26A000000006B007500020000006F");
        var move = Deserialize<ClientMoveItemToInventoryMessage>(request);
        Assert.AreEqual(IdentityType.Backpack, move.SourceContainer.Type);
        Assert.AreEqual((117 << 16) | 2, move.SourceContainer.Instance);
        CollectionAssert.AreEqual(request, Serialize(move));

        byte[] acknowledgement = Convert.FromHexString("47537A240000C3504A6CF26A000000006B007500020000C3504A6CF26A0000006F");
        var added = Deserialize<ContainerAddItemMessage>(acknowledgement);
        Assert.AreEqual(move.SourceContainer, added.SourceContainer);
        Assert.AreEqual(inventory.Identity, added.Target);
        CollectionAssert.AreEqual(acknowledgement, Serialize(added));
    }

    [TestMethod]
    public void OrdinaryRetailChestUsePublishesContentsBeforeSuccessAndCloseBeforeSuccess()
    {
        // Same official CSV: ordinary Use 913 -> contents 914 -> success 915.
        // Close 1447 -> close action 1449 -> success 1450 is a different chest; only identities are retargeted.
        byte[] useBytes = Convert.FromHexString("525268580000C3504A6CF26A01000000000000001500000003000000010000C3504A6CF26A0000C7490BBA6B26");
        byte[] contentsBytes = Convert.FromHexString("4E5369760000C3504A6CF26A010000001500000002000007E20000000000210001000000000000000000006AD000006AD000000001000000000000C7490BBA6B260000007300000001");
        byte[] successBytes = Convert.FromHexString("525268580000C3504A6CF26A00000000010000001500000003000000010000C3504A6CF26A0000C7490BBA6B26");
        var request = Deserialize<GenericCmdMessage>(useBytes);
        var contents = Deserialize<InventoryUpdateMessage>(contentsBytes);
        var success = Deserialize<GenericCmdMessage>(successBytes);
        CollectionAssert.AreEqual(useBytes, Serialize(request));
        CollectionAssert.AreEqual(contentsBytes, Serialize(contents));
        CollectionAssert.AreEqual(successBytes, Serialize(success));
        Assert.AreEqual(GenericCmdAction.Use, request.Action);
        Assert.AreEqual(1, request.Target.Length);

        using var world = new InventoryActionTests.World();
        var registry = new DynelRegistry();
        using ServiceProvider services = new ServiceCollection().AddSingleton(registry)
            .AddSingleton(new PlayfieldLocality(4582, null))
            .AddSingleton((QuestPropService)RuntimeHelpers.GetUninitializedObject(typeof(QuestPropService)))
            .BuildServiceProvider();
        typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(world.Player.Playfield, services);
        typeof(Playfield).GetField("_nextContainerInventoryHandle", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(world.Player.Playfield, contents.SlotnumberInMainInventory);
        var chest = new Chest(request.Target[0])
        {
            SpawnDefinition = CreateCapturedChest().SpawnDefinition,
            Playfield = world.Player.Playfield,
            Position = world.Player.Position
        };
        InventoryEntry entry = contents.Entries.Single();
        Assert.IsTrue(chest.Loot.Add(entry.Slotnumber, TestWorld.CreateItem(entry.LowId, entry.HighId,
            entry.Quality, (ItemFlags)entry.UnknownFlags, instanceId: 901, stackCount: entry.Unknown1)));
        registry.Register(world.Player);
        registry.Register(chest);
        request.Identity = request.User = world.Player.Identity;
        contents.Identity = success.Identity = success.User = world.Player.Identity;
        var handler = new GenericCmdMessageHandler(new StubInventoryRepository(), new StubItemBuilder());

        handler.Handle(request, world.Session);

        Assert.AreEqual(2, world.Session.Messages.Count);
        Assert.IsInstanceOfType<InventoryUpdateMessage>(world.Session.Messages[0]);
        Assert.IsInstanceOfType<GenericCmdMessage>(world.Session.Messages[1]);
        CollectionAssert.AreEqual(Serialize(contents), Serialize(world.Session.Messages[0]));
        CollectionAssert.AreEqual(Serialize(success), Serialize(world.Session.Messages[1]));
        Assert.IsTrue(chest.IsOpen);

        var close = Deserialize<GenericCmdMessage>(Convert.FromHexString("525268580000C3504A6CF26A01000000000000002400000003000000010000C3504A6CF26A0000C7490BBA6B24"));
        var closeAction = Deserialize<ActionMessage>(Convert.FromHexString("2049527C0000C7490BBA6B240100000001000000660000C3504A6CF26A"));
        var closeSuccess = Deserialize<GenericCmdMessage>(Convert.FromHexString("525268580000C3504A6CF26A00000000010000002400000003000000010000C3504A6CF26A0000C7490BBA6B24"));
        close.Identity = close.User = closeSuccess.Identity = closeSuccess.User = world.Player.Identity;
        close.Target[0] = closeSuccess.Target[0] = closeAction.Identity = chest.Identity;
        closeAction.Target = world.Player.Identity;
        world.Session.Messages.Clear();

        handler.Handle(close, world.Session);

        Assert.AreEqual(2, world.Session.Messages.Count);
        Assert.IsInstanceOfType<ActionMessage>(world.Session.Messages[0]);
        Assert.IsInstanceOfType<GenericCmdMessage>(world.Session.Messages[1]);
        CollectionAssert.AreEqual(Serialize(closeAction), Serialize(world.Session.Messages[0]));
        CollectionAssert.AreEqual(Serialize(closeSuccess), Serialize(world.Session.Messages[1]));
        Assert.IsFalse(chest.IsOpen);
    }

    [TestMethod]
    [DataRow(196766495, 117, 2, 56250, 56250, 4, 1,
        "5469373F0000C3504A6CF26A000000006B007500020000006F",
        "47537A240000C3504A6CF26A000000006B007500020000C3504A6CF26A0000006F")]
    [DataRow(196766496, 119, 0, 164780, 164781, 2, 100,
        "5469373F0000C3504A6CF26A000000006B007700000000006F",
        "47537A240000C3504A6CF26A000000006B007700000000C3504A6CF26A0000006F")]
    public void CapturedChestTakeKeepsTheRequestedBagMarkerInItsDurableAcknowledgement(
        int chestInstance, int handle, int slot, int lowId, int highId, int quality, int count,
        string requestHex, string acknowledgementHex)
    {
        // Official 1105/1106 after lockpick, and 1141/1142 after ordinary Use 1129/contents 1130/success 1131.
        var request = Deserialize<ClientMoveItemToInventoryMessage>(Convert.FromHexString(requestHex));
        var expected = Deserialize<ContainerAddItemMessage>(Convert.FromHexString(acknowledgementHex));
        CollectionAssert.AreEqual(Convert.FromHexString(requestHex), Serialize(request));
        CollectionAssert.AreEqual(Convert.FromHexString(acknowledgementHex), Serialize(expected));
        Assert.AreEqual((handle << 16) | slot, request.SourceContainer.Instance);
        using var world = new InventoryActionTests.World();
        var registry = new DynelRegistry();
        using ServiceProvider services = AttachChestServices(world, registry);
        typeof(Playfield).GetField("_nextContainerInventoryHandle", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(world.Player.Playfield, handle);
        var chest = new Chest(new Identity { Type = IdentityType.Container, Instance = chestInstance })
        {
            SpawnDefinition = CreateCapturedChest().SpawnDefinition,
            Playfield = world.Player.Playfield,
            Position = world.Player.Position
        };
        registry.Register(world.Player);
        registry.Register(chest);
        Item item = TestWorld.CreateItem(lowId: lowId, highId: highId, quality: quality, instanceId: 902, stackCount: count);
        Assert.IsTrue(chest.Loot.Add(slot, item));
        world.Persistence.Rows[item.InstanceId] = item.ToRecord(chest.Loot.Identity, slot, item.StackCount);
        Assert.IsTrue(chest.TryUse(world.Player));
        Assert.AreEqual(handle, chest.InventoryHandle);
        request.Identity = expected.Identity = expected.Target = world.Player.Identity;
        world.Session.Messages.Clear();
        world.Persistence.BeforeCommit = _ => Assert.AreEqual(0, world.Session.Messages.Count);

        new InventoryMoveService(new StubLogger(), world.Flush, world.Actions).Handle(world.Player, request);

        Assert.AreEqual(1, world.Persistence.Calls);
        Assert.IsFalse(chest.Loot.Content.ContainsKey(slot));
        Assert.AreSame(item, world.Player.Inventory.Inventory.Content[world.Player.Inventory.Inventory.Offset]);
        Assert.AreEqual(world.Player.Inventory.Inventory.Offset, world.Persistence.Rows[item.InstanceId].ContainerPlacement);
        Assert.AreEqual(count, world.Persistence.Rows[item.InstanceId].StackCount);
        var actual = world.Session.Messages.OfType<ContainerAddItemMessage>().Single();
        CollectionAssert.AreEqual(Serialize(expected), Serialize(actual));
    }

    [TestMethod]
    public void RetailLockpickActionFiveEvidenceIsDistinctFromOrdinaryUse()
    {
        // Ordinals 1091/1092/1093 precede 1094 contents: item-on-chest, not ordinary Use(3).
        byte[] requestBytes = Convert.FromHexString("525268580000C3504A6CF26A01000000000000001A00000005000000000000C3504A6CF26A00000068000000490000C7490BBA6B1F");
        byte[] replyBytes = Convert.FromHexString("525268580000C3504A6CF26A00000000010000001A00000005000000000000C3504A6CF26A00000068000000490000C7490BBA6B1F");
        byte[] actionBytes = Convert.FromHexString("2049527C0000C7490BBA6B1F0000000001000000730000C3504A6CF26A");
        var request = Deserialize<GenericCmdMessage>(requestBytes);
        var reply = Deserialize<GenericCmdMessage>(replyBytes);
        var action = Deserialize<ActionMessage>(actionBytes);
        Assert.AreEqual(GenericCmdAction.UseItemOnItem, request.Action);
        Assert.AreEqual(new Identity { Type = IdentityType.Inventory, Instance = 73 }, request.Target[0]);
        Assert.AreEqual(new Identity { Type = IdentityType.Container, Instance = 196766495 }, request.Target[1]);
        CollectionAssert.AreEqual(request.Target, reply.Target);
        Assert.AreEqual(request.Target[1], action.Identity);
        Assert.AreEqual(request.User, action.Target);
        Assert.AreEqual(1, action.ActionCode);
        Assert.AreEqual(0x73, action.ActionIdentity);
        CollectionAssert.AreEqual(requestBytes, Serialize(request));
        CollectionAssert.AreEqual(replyBytes, Serialize(reply));
        CollectionAssert.AreEqual(actionBytes, Serialize(action));
    }

    [TestMethod]
    public void ChestRefreshesOnlyNewOpensAndRejectsInvalidAccess()
    {
        using var world = new InventoryActionTests.World();
        var registry = new DynelRegistry();
        using ServiceProvider services = AttachChestServices(world, registry);
        Chest chest = CreateCapturedChest();
        chest.Playfield = world.Player.Playfield;
        chest.Position = world.Player.Position;
        registry.Register(world.Player);
        registry.Register(chest);
        int refreshes = 0;
        chest.RefreshLoot = _ => { refreshes++; return true; };
        chest.AccessValidator = _ => false;
        Assert.IsFalse(chest.TryUse(world.Player));
        Assert.AreEqual(0, refreshes);
        chest.AccessValidator = _ => true;
        Assert.IsTrue(chest.TryUse(world.Player));
        Assert.IsTrue(chest.IsOpen);
        Assert.AreEqual(1, refreshes);
        Assert.IsTrue(chest.TryUse(world.Player));
        Assert.IsFalse(chest.IsOpen);
        Assert.AreEqual(1, refreshes);
        Assert.IsTrue(chest.TryUse(world.Player));
        Assert.AreEqual(2, refreshes);
        chest.Close();
        chest.RefreshLoot = _ => false;
        Assert.IsFalse(chest.TryUse(world.Player));
        Assert.IsFalse(chest.IsOpen);
        chest.RefreshLoot = _ => { refreshes++; return true; };
        chest.Position = new AoVector3(100, 100, 100);
        Assert.IsFalse(chest.TryUse(world.Player));
        Assert.AreEqual(2, refreshes);
        chest.Position = world.Player.Position;
        registry.Unregister(chest.Identity);
        Assert.IsFalse(chest.TryUse(world.Player));
        Assert.AreEqual(2, refreshes);
    }

    [TestMethod]
    public void MissingAppearanceRemainsInert()
    {
        var chest = new Chest(new Identity { Type = IdentityType.Container, Instance = 42 });
        Assert.ThrowsExactly<InvalidOperationException>(() => chest.BuildSpawnMessage());
        Assert.IsFalse(chest.TryUse(TestWorld.CreatePlayer(111)));
    }

    [TestMethod]
    public void CollisionRadiusUsesConfiguredGeometryAndRejectsInvalidInputs()
    {
        var chest = new Chest(new Identity { Type = IdentityType.Container, Instance = 42 });
        chest.CollisionRadius = 0.75f;
        Assert.AreEqual(0.75, chest.GetCollisionRadius());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => chest.CollisionRadius = -1);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => chest.CollisionRadius = float.NaN);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => chest.CollisionRadius = float.PositiveInfinity);
        Assert.AreEqual(0.75, chest.GetCollisionRadius());
    }

    internal static Chest CreateCapturedChest(bool locked = false)
        => CapturedChest(Convert.FromHexString(WorldTreasurePacket), locked);

    internal static ServiceProvider AttachChestServices(InventoryActionTests.World world, DynelRegistry registry)
    {
        ServiceProvider services = new ServiceCollection().AddSingleton(registry)
            .AddSingleton(new PlayfieldLocality(4582, null)).BuildServiceProvider();
        typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(world.Player.Playfield, services);
        typeof(Playfield).GetField("_nextContainerInventoryHandle", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(world.Player.Playfield, 117);
        return services;
    }

    static Chest CapturedChest(byte[] packet, bool locked = true) => new(
        new Identity { Type = IdentityType.Container, Instance = 196766495 }, lockDifficulty: locked ? 1 : 0)
    {
        SpawnDefinition = new ChestSpawnDefinition
        {
            StateMachineType = 1000015, StateMachineInstance = 7, HeaderStatBytes = 111, LockableValue = 15,
            Stats =
            [
                new() { StatId = 0, Value = 0x20001841 },
                new() { StatId = 23, Value = 162447 },
                new() { StatId = 701, Value = 1 },
                new() { StatId = 702, Value = 162447 },
                new() { StatId = 703, Value = 162447 },
                new() { StatId = 412, Value = 1 }
            ]
        },
        Position = new AoVector3(ReadFloat(packet, 41), ReadFloat(packet, 45), ReadFloat(packet, 49)),
        Rotation = new AoQuaternion(ReadFloat(packet, 53), ReadFloat(packet, 57), ReadFloat(packet, 61), ReadFloat(packet, 65)),
        Playfield = BarePlayfield(0x20D4BC)
    };

    static float ReadFloat(byte[] packet, int offset) => BinaryPrimitives.ReadSingleBigEndian(packet.AsSpan(offset, 4));

    static Playfield BarePlayfield(int instance, int? resourceId = null)
    {
        var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
        typeof(Playfield).GetField("<Identity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(playfield, new Identity { Type = IdentityType.Playfield, Instance = instance });
        typeof(Playfield).GetField("<ResourceId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(playfield, resourceId ?? instance);
        return playfield;
    }

    internal static byte[] Serialize(MessageBody body)
    {
        var resolver = new SerializerResolverBuilder<MessageBody>().Build();
        using var stream = new MemoryStream();
        using var writer = new PacketWriter(stream);
        resolver.GetSerializer(body.GetType()).Serialize(writer, new SerializationContext(resolver), body);
        return stream.ToArray();
    }

    static T Deserialize<T>(byte[] body) where T : MessageBody
    {
        var resolver = new SerializerResolverBuilder<MessageBody>().Build();
        using var stream = new MemoryStream(body);
        using var reader = new PacketReader(stream);
        var message = (T)resolver.GetSerializer(typeof(T)).Deserialize(reader, new SerializationContext(resolver));
        Assert.AreEqual(stream.Length, stream.Position, "The complete captured body must be consumed.");
        return message;
    }
}
