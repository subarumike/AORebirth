namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;
using SmokeLounge.AOtomation.Messaging.Serialization;

[TestClass]
public sealed class ZoneHandoffWireTests
{
    [TestMethod]
    public void RecoveredRetailLayoutRoundTripsBothCookiesWithoutAnAccountField()
    {
        // Synthetic values in the statically recovered 18.8.62_EP1 layout; not a retail capture.
        byte[] expected = Convert.FromHexString("0001000100010020000026AD000000020000001B000026AD1122334489ABCDEF");
        var serializer = new MessageSerializer();
        using var input = new MemoryStream(expected);
        var message = serializer.Deserialize(input);
        var login = (ZoneLoginMessage)message.Body;
        Assert.AreEqual(9901, login.CharacterId);
        Assert.AreEqual(0x11223344u, login.Cookie1);
        Assert.AreEqual(0x89ABCDEFu, login.Cookie2);
        using var output = new MemoryStream();
        serializer.Serialize(output, message);
        CollectionAssert.AreEqual(expected, output.ToArray());
    }
}
