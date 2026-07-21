namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;

    using StreamReader = SmokeLounge.AOtomation.Messaging.Serialization.StreamReader;
    using StreamWriter = SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter;

    /// <summary>
    /// Capture 20260719-182611 Inspect Equipment wire contract.
    /// </summary>
    [TestClass]
    public class InspectMessageContractTests
    {
        [TestMethod]
        public void CharacterActionInspectEnumIs0x105()
        {
            Assert.AreEqual(0x00000105, (int)CharacterActionType.Inspect);
        }

        [TestMethod]
        public void EmptyInspectMatchesCapture41CC226B()
        {
            // N3 body from IN Inspect (empty gear) after envelope: type+identity+unknown+target+X3F1
            byte[] wire = HexToBytes(
                "5A585F650000C350762ABC21000000C35041CC226B000003F1");

            var message = (InspectMessage)Deserialize<InspectMessage>(wire);
            Assert.AreEqual(N3MessageType.Inspect, message.N3MessageType);
            Assert.AreEqual(IdentityType.CanbeAffected, message.Identity.Type);
            Assert.AreEqual(unchecked((int)0x762ABC21), message.Identity.Instance);
            Assert.AreEqual(0, message.Unknown);
            Assert.AreEqual(IdentityType.CanbeAffected, message.Target.Type);
            Assert.AreEqual(unchecked((int)0x41CC226B), message.Target.Instance);
            Assert.IsNotNull(message.Items);
            Assert.AreEqual(0, message.Items.Length);

            byte[] roundTrip = Serialize(message);
            CollectionAssert.AreEqual(wire, roundTrip);
        }

        [TestMethod]
        public void SingleItemInspectMatchesCapture6B9C9206()
        {
            byte[] wire = HexToBytes(
                "5A585F650000C350762ABC21000000C3506B9C9206000007E2"
                + "0000003300A10001000000000000000000041FF900041FFA0000005A00000000");

            var message = (InspectMessage)Deserialize<InspectMessage>(wire);
            Assert.AreEqual(1, message.Items.Length);
            InventorySlot slot = message.Items[0];
            Assert.AreEqual(0x33, slot.Placement);
            Assert.AreEqual(unchecked((short)0x00A1), slot.Flags);
            Assert.AreEqual(1, slot.Count);
            Assert.AreEqual(0x041FF9, slot.ItemLowId);
            Assert.AreEqual(0x041FFA, slot.ItemHighId);
            Assert.AreEqual(0x5A, slot.Quality);

            byte[] roundTrip = Serialize(message);
            CollectionAssert.AreEqual(wire, roundTrip);
        }

        private static byte[] Serialize(MessageBody body)
        {
            var resolver = new SerializerResolverBuilder<MessageBody>().Build();
            var serializer = resolver.GetSerializer(body.GetType());
            using (var memoryStream = new MemoryStream())
            {
                using (var writer = new StreamWriter(memoryStream))
                {
                    serializer.Serialize(writer, new SerializationContext(resolver), body);
                    return memoryStream.ToArray();
                }
            }
        }

        private static MessageBody Deserialize<T>(byte[] bytes)
            where T : MessageBody
        {
            var resolver = new SerializerResolverBuilder<MessageBody>().Build();
            var serializer = resolver.GetSerializer(typeof(T));
            using (var memoryStream = new MemoryStream(bytes))
            {
                using (var reader = new StreamReader(memoryStream))
                {
                    return (MessageBody)serializer.Deserialize(reader, new SerializationContext(resolver));
                }
            }
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = System.Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
