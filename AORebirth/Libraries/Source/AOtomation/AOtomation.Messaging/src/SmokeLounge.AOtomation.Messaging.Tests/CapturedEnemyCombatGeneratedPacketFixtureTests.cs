namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using WireReader = SmokeLounge.AOtomation.Messaging.Serialization.StreamReader;
    using WireWriter = SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter;

    [TestClass]
    public class CapturedEnemyCombatGeneratedPacketFixtureTests
    {
        // The oracle is original captured bytes, independent of gameplay factories.
        [TestMethod]
        public void CapturedCombatPacketsRoundTripWithoutLegacyGameplay()
        {
            var fixtures = CapturedEnemyCombatGeneratedPacketFixtures.Create();
            Assert.IsTrue(fixtures.Length > 0);
            int packets = 0;
            foreach (var fixture in fixtures)
            {
                foreach (var packet in fixture.WeaponPackets)
                { RoundTrip(typeof(WeaponItemFullUpdateMessage), packet.BodyHex, packet.PacketId); packets++; }
                foreach (var packet in fixture.SpecialAttackWeaponPackets)
                { RoundTrip(typeof(SpecialAttackWeaponMessage), packet.BodyHex, packet.PacketId); packets++; }
                foreach (var packet in fixture.AttackPackets)
                { RoundTrip(typeof(AttackMessage), packet.BodyHex, packet.PacketId); packets++; }
                foreach (var packet in fixture.AttackInfoPackets)
                {
                    var body = (AttackInfoMessage)RoundTrip(typeof(AttackInfoMessage), packet.BodyHex, packet.PacketId);
                    Assert.AreEqual(packet.HitTypeWire, body.Unknown5, packet.PacketId);
                    packets++;
                }
            }
            Assert.IsTrue(packets > 0);
        }

        private static object RoundTrip(Type type, string hex, string evidence)
        {
            byte[] expected = Enumerable.Range(0, hex.Length / 2)
                .Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
            var resolver = new SerializerResolverBuilder<MessageBody>().Build();
            var serializer = resolver.GetSerializer(type);
            object body;
            using (var input = new MemoryStream(expected))
            using (var reader = new WireReader(input))
            {
                body = serializer.Deserialize(reader, new SerializationContext(resolver));
                Assert.AreEqual(input.Length, input.Position, evidence);
            }
            using (var output = new MemoryStream())
            using (var writer = new WireWriter(output))
            {
                serializer.Serialize(writer, new SerializationContext(resolver), body);
                CollectionAssert.AreEqual(expected, output.ToArray(), evidence);
            }
            return body;
        }
    }
}
