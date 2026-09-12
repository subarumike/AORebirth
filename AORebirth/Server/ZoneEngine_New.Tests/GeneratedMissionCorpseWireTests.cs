namespace ZoneEngine_New.Tests;

using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine.Core.Packets;

[TestClass]
public sealed class GeneratedMissionCorpseWireTests
{
    [TestMethod]
    public void SharedMissionCorpseTemplateIsUnchangedFromAcceptedLegacySource()
    {
        var source = GeneratedMissionCorpseWire.CopyTemplate();
        Assert.AreEqual(420, source.Length);
        Assert.AreEqual("9FC20E6EE46F641E30AC0BD25B48C0E3198E11329E5F70ADFE0E5CFF69A9384D",
            Convert.ToHexString(SHA256.HashData(source)));
        // Exact captured inputs reproduce every byte, including variable-length name and material tail.
        var built = GeneratedMissionCorpseWire.Build("Tilda Konecny", 0x799361EA, 0x00F74827,
            0x797E30D7, 0xDB4,
            BinaryPrimitives.ReadSingleBigEndian(source.AsSpan(45, 4)),
            BinaryPrimitives.ReadSingleBigEndian(source.AsSpan(49, 4)),
            BinaryPrimitives.ReadSingleBigEndian(source.AsSpan(53, 4)),
            0x160800, 93, 3, 2, 1, 5934, 26137, 29);
        CollectionAssert.AreEqual(source, built);
        source[0] = 0;
        Assert.AreEqual(3, GeneratedMissionCorpseWire.CopyTemplate()[0]);
    }

    [TestMethod]
    public void VariableNamesKeepNullAndTailWithoutPaddingAndCashPatchesOnlyValue()
    {
        var packet = GeneratedMissionCorpseWire.Build("A", 1000001, 1000001, 3, 9,
            1, 2, 3, 0x160001, 100, 2, 1, 1, 0, 0, 87);
        Assert.AreEqual(408, packet.Length);
        Assert.AreEqual(packet.Length, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(6, 2)));
        Assert.AreEqual(13, BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(235, 4)));
        Assert.AreEqual(0, packet[251]);
        Assert.AreEqual(61, BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(203, 4)));
        Assert.AreEqual(87, BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(207, 4)));
        Assert.AreEqual(5934, BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(199, 4)));
        Assert.AreEqual(26137, BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(324, 4)));
        Assert.AreEqual(1000001, BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(336, 4)));
    }
}
