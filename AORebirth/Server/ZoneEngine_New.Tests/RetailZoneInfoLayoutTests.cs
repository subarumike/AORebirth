namespace ZoneEngine_New.Tests;

using System;
using System.Buffers.Binary;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;
using ZoneEngine_New.Core.Network;

[TestClass]
public sealed class RetailZoneInfoLayoutTests
{
    [TestMethod]
    public void ZoneInfoSerializesTheCapturedTwentySixByteRetailBody()
    {
        var body = new ZoneInfoMessage
        {
            CharacterId = unchecked((int)0x0D904118),
            ServerIpAddress = IPAddress.Parse("37.18.193.20"),
            ServerPort = 7501,
            Cookie1 = 0x11223344,
            Cookie2 = 0x55667788,
            EventServerType = 1,
            PlayerId = 0
        };

        byte[] packet = new ZoneMessageCodec().Serialize(body, 1, 0x1FB5);

        Assert.AreEqual(46, packet.Length);
        Assert.AreEqual(0x0D904118u, BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(20, 4)));
        CollectionAssert.AreEqual(new byte[] { 37, 18, 193, 20 }, packet[24..28]);
        Assert.AreEqual(7501, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(28, 2)));
        Assert.AreEqual(0x11223344u, BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(30, 4)));
        Assert.AreEqual(0x55667788u, BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(34, 4)));
        Assert.AreEqual(1u, BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(38, 4)));
        Assert.AreEqual(0u, BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(42, 4)));
    }
}
