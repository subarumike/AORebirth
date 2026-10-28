namespace ZoneEngine_New.Tests;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

using AORebirth.CaptureProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Serialization;

[TestClass]
public sealed class MissionTrapPacketTests
{
    const string FixtureRootVariable = "AO_REBIRTH_MISSION_TRAP_FIXTURE_ROOT";

    [TestMethod, Timeout(30000)]
    public void All221RetainedMineAndTrapPacketsPreserveTheirExactWireFieldsAndBytes()
    {
        using JsonDocument document = Fixture("decoder/trap-packets.decoded.json");
        JsonElement root = document.RootElement;
        Assert.AreEqual(221, root.GetProperty("Selected").GetInt32());
        Assert.AreEqual(0, root.GetProperty("Failures").GetInt32());
        var serializer = new MessageSerializer();
        int mines = 0, traps = 0, parented = 0, positioned = 0;
        foreach (JsonElement record in root.GetProperty("Records").EnumerateArray())
        {
            byte[] packet = Convert.FromHexString(record.GetProperty("RawHex").GetString()!);
            Assert.AreEqual(record.GetProperty("PacketSha256").GetString(),
                Convert.ToHexStringLower(SHA256.HashData(packet)));
            RawTrapFullUpdate independent = RawTrapFullUpdateDecoder.DecodePacket(packet);
            using var input = new MemoryStream(packet, writable: false);
            Message message = serializer.Deserialize(input);
            Assert.AreEqual(input.Length, input.Position, "Full source packet must be consumed.");
            var body = (N3Message)message.Body;
            Assert.AreEqual(independent.Identity.Type, (int)body.Identity.Type);
            Assert.AreEqual(independent.Identity.Instance, body.Identity.Instance);
            Assert.AreEqual(independent.PassOn, body.Unknown);
            if (independent.PacketKey == RawTrapFullUpdateDecoder.MineKey)
            {
                mines++;
                var mine = (MineFullUpdateMessage)body;
                Assert.AreEqual(independent.MineMarker, mine.MineMarker);
                Assert.AreEqual(independent.UnknownMineValue, mine.UnknownMineValue);
                Assert.AreEqual(Identity.None, mine.Parent);
            }
            else
            {
                traps++;
                Assert.IsInstanceOfType<TrapItemFullUpdateMessage>(body);
            }
            VerifyCommonAndTrapFields(independent, body);
            if (independent.Parent.Type == 0)
                positioned++;
            else
                parented++;
            using var output = new MemoryStream();
            serializer.Serialize(output, message);
            CollectionAssert.AreEqual(packet, output.ToArray(), "Captured packet round-trip must be byte exact.");
        }
        Assert.AreEqual(173, mines);
        Assert.AreEqual(48, traps);
        Assert.AreEqual(48, parented);
        Assert.AreEqual(173, positioned);
    }

    [TestMethod, Timeout(30000)]
    public void ThirteenRecoveredEvent109DeliveriesKeepTrapAndActorIdentitiesSeparate()
    {
        using JsonDocument document = Fixture("historical-trap-fixtures.json");
        var serializer = new MessageSerializer();
        int count = 0;
        foreach (JsonElement record in document.RootElement.GetProperty("Packets").EnumerateArray())
        {
            byte[] packet = Convert.FromHexString(record.GetProperty("raw_hex").GetString()!);
            if (BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(16, 4)) != (int)N3MessageType.Action)
                continue;
            using var input = new MemoryStream(packet, writable: false);
            Message message = serializer.Deserialize(input);
            var action = (ActionMessage)message.Body;
            Assert.AreEqual(1, action.ActionCode);
            Assert.AreEqual(109, action.ActionIdentity);
            Assert.AreEqual(BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(20, 4)), (int)action.Identity.Type);
            Assert.AreEqual(BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(24, 4)), action.Identity.Instance);
            Assert.AreEqual(BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(37, 4)), (int)action.Target.Type);
            Assert.AreEqual(BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(41, 4)), action.Target.Instance);
            Assert.AreEqual(input.Length, input.Position);
            using var output = new MemoryStream();
            serializer.Serialize(output, message);
            CollectionAssert.AreEqual(packet, output.ToArray());
            count++;
        }
        Assert.AreEqual(13, count);
    }

    [TestMethod]
    public void TrapDisarmedUsesOnlyInheritedTargetAndPassOnWithoutInventedTail()
    {
        var original = new TrapDisarmedMessage
        {
            Identity = new Identity { Type = IdentityType.Trap, Instance = 7 },
            Unknown = 0
        };
        byte[] packet = Encode(original);
        Assert.AreEqual(29, packet.Length);
        using var input = new MemoryStream(packet, writable: false);
        var decoded = (TrapDisarmedMessage)new MessageSerializer().Deserialize(input).Body;
        Assert.AreEqual(original.Identity, decoded.Identity);
        Assert.AreEqual(original.Unknown, decoded.Unknown);
        Assert.AreEqual(input.Length, input.Position);
    }

    [DataTestMethod]
    [DataRow(0), DataRow(1), DataRow(2), DataRow(3), DataRow(130), DataRow(255)]
    public void TrapFlagsKeepDetectionEnabledRememberedAndUnclassifiedBitsSeparate(int flags)
    {
        TrapItemFullUpdateMessage original = ParentedTrap();
        original.TrapFlags = (byte)flags;
        byte[] packet = Encode(original);
        RawTrapFullUpdate decoded = RawTrapFullUpdateDecoder.DecodePacket(packet);
        Assert.AreEqual((byte)flags, decoded.TrapFlags);
        Assert.IsNull(decoded.Coordinate);
        Assert.IsNull(decoded.Heading);
    }

    [TestMethod]
    public void ParentlessTrapPublishesExactPositionQuaternionAndOpaqueBlob()
    {
        TrapItemFullUpdateMessage original = ParentedTrap();
        original.Parent = Identity.None;
        original.Coordinate = new Vector3 { X = 1.25f, Y = 5.5f, Z = -7.75f };
        original.Heading = new Quaternion { X = 0.25f, Y = -0.5f, Z = 0.75f, W = 1f };
        original.OpaqueData = new byte[] { 0, 1, 0, 255 };
        byte[] packet = Encode(original);
        RawTrapFullUpdate decoded = RawTrapFullUpdateDecoder.DecodePacket(packet);
        CollectionAssert.AreEqual(new[] { 1.25f, 5.5f, -7.75f }, decoded.Coordinate);
        CollectionAssert.AreEqual(new[] { 0.25f, -0.5f, 0.75f, 1f }, decoded.Heading);
        CollectionAssert.AreEqual(original.OpaqueData, decoded.OpaqueData);
    }

    [TestMethod]
    public void OfflineReaderRejectsTruncationNoncanonicalCountsMarkersAndExtraTail()
    {
        byte[] original = Encode(ParentedTrap());
        byte[] truncated = original[..^1];
        SetLength(truncated);
        Assert.ThrowsException<EndOfStreamException>(() => RawTrapFullUpdateDecoder.DecodePacket(truncated));
        byte[] marker = (byte[])original.Clone();
        BinaryPrimitives.WriteInt32BigEndian(marker.AsSpan(29, 4), 12);
        Assert.ThrowsException<InvalidDataException>(() => RawTrapFullUpdateDecoder.DecodePacket(marker));
        byte[] count = (byte[])original.Clone();
        BinaryPrimitives.WriteInt32BigEndian(count.AsSpan(55, 4), 1008);
        Assert.ThrowsException<InvalidDataException>(() => RawTrapFullUpdateDecoder.DecodePacket(count));
        byte[] extra = original.Concat(new byte[] { 0 }).ToArray();
        SetLength(extra);
        Assert.ThrowsException<InvalidDataException>(() => RawTrapFullUpdateDecoder.DecodePacket(extra));
    }

    static TrapItemFullUpdateMessage ParentedTrap() => new()
    {
        Identity = new Identity { Type = IdentityType.Trap, Instance = 7 },
        Parent = new Identity { Type = IdentityType.Door, Instance = 8 },
        Unknown = 0,
        Raw74 = 9,
        Identity78 = Identity.None,
        Identity80 = Identity.None,
        TrapFlags = 2
    };

    static byte[] Encode(N3Message body)
    {
        using var stream = new MemoryStream();
        new MessageSerializer().Serialize(stream, new Message
        {
            Header = new Header { PacketType = PacketType.N3Message },
            Body = body
        });
        return stream.ToArray();
    }

    static void SetLength(byte[] packet)
        => BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(6, 2), checked((ushort)packet.Length));

    static void VerifyCommonAndTrapFields(RawTrapFullUpdate expected, N3Message message)
    {
        object Field(string name) => message.GetType().GetProperty(name)!.GetValue(message)!;
        Assert.AreEqual(expected.CommonMarker, (int)Field("CommonMarker"));
        Assert.AreEqual(expected.Parent.Type, (int)((Identity)Field("Parent")).Type);
        Assert.AreEqual(expected.Parent.Instance, ((Identity)Field("Parent")).Instance);
        Assert.AreEqual(expected.PlayfieldId, (int)Field("PlayfieldId"));
        Assert.AreEqual(expected.NeutralIdentity.Type, (int)((Identity)Field("NeutralIdentity")).Type);
        Assert.AreEqual(expected.NeutralIdentity.Instance, ((Identity)Field("NeutralIdentity")).Instance);
        Assert.AreEqual(unchecked((short)((expected.HeaderStatBytes[0] << 8) | expected.HeaderStatBytes[1])),
            (short)Field("HeaderStatBytes"));
        var stats = (GameTuple<CharacterStat, uint>[])Field("Stats");
        Assert.AreEqual(expected.Stats.Count, stats.Length);
        for (int index = 0; index < stats.Length; index++)
        {
            Assert.AreEqual(expected.Stats[index].StatId, (int)stats[index].Value1);
            Assert.AreEqual(expected.Stats[index].Value, stats[index].Value2);
        }
        CollectionAssert.AreEqual(expected.OpaqueData, (byte[])Field("OpaqueData"));
        Assert.AreEqual(expected.TrapMarker, (int)Field("TrapMarker"));
        Assert.AreEqual(expected.Raw74, (int)Field("Raw74"));
        Assert.AreEqual(expected.Identity78.Type, (int)((Identity)Field("Identity78")).Type);
        Assert.AreEqual(expected.Identity78.Instance, ((Identity)Field("Identity78")).Instance);
        Assert.AreEqual(expected.Identity80.Type, (int)((Identity)Field("Identity80")).Type);
        Assert.AreEqual(expected.Identity80.Instance, ((Identity)Field("Identity80")).Instance);
        Assert.AreEqual(expected.TrapFlags, (byte)Field("TrapFlags"));
        if (expected.Parent.Type != 0)
        {
            Assert.IsNull(message.GetType().GetProperty("Coordinate")!.GetValue(message));
            Assert.IsNull(message.GetType().GetProperty("Heading")!.GetValue(message));
            return;
        }
        var position = (Vector3)Field("Coordinate");
        var heading = (Quaternion)Field("Heading");
        CollectionAssert.AreEqual(expected.Coordinate, new[] { position.X, position.Y, position.Z });
        CollectionAssert.AreEqual(expected.Heading, new[] { heading.X, heading.Y, heading.Z, heading.W });
    }

    static JsonDocument Fixture(string relativePath)
    {
        string? root = Environment.GetEnvironmentVariable(FixtureRootVariable);
        if (string.IsNullOrWhiteSpace(root))
        {
            Assert.Inconclusive("Private captured evidence requires " + FixtureRootVariable + ".");
            throw new InvalidOperationException();
        }
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))));
    }
}
