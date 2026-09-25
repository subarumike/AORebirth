namespace ZoneEngine_New.Tests
{
    using System.IO;
    using System.Text;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    [TestClass]
    public sealed class WorldItemNameTests
    {
        [TestMethod]
        public void SpawnNameLengthIncludesTheTrailingNul()
        {
            const string name = "Shere's Sanctuary Statue";
            var item = new WorldItem(
                new Identity { Type = IdentityType.Terminal, Instance = 1 },
                new ItemTemplate { Name = name },
                244831,
                244831,
                1);

            var body = (SimpleItemFullUpdateMessage)item.BuildSpawnMessage();
            Assert.AreEqual(name + "\0", body.Name);

            byte[] packet = Serialize(body);
            byte[] ascii = Encoding.ASCII.GetBytes(name);
            int index = IndexOf(packet, ascii);
            Assert.IsTrue(index >= 4);
            Assert.AreEqual(ascii.Length + 1, packet[index - 1]);
            Assert.AreEqual(0, packet[index - 2]);
            Assert.AreEqual(0, packet[index - 3]);
            Assert.AreEqual(0, packet[index - 4]);
            Assert.AreEqual(0, packet[index + ascii.Length]);
        }

        [TestMethod]
        public void EmptySpawnNameStaysLengthZero()
        {
            var item = new WorldItem(
                new Identity { Type = IdentityType.Terminal, Instance = 1 },
                new ItemTemplate(),
                1,
                1,
                1);

            var body = (SimpleItemFullUpdateMessage)item.BuildSpawnMessage();
            Assert.AreEqual(string.Empty, body.Name);
        }

        static byte[] Serialize(MessageBody body)
        {
            var serializer = new MessageSerializer();
            using var stream = new MemoryStream();
            serializer.Serialize(
                stream,
                new Message
                {
                    Header = new Header
                    {
                        MessageId = 0xdfdf,
                        PacketType = body.PacketType,
                        Unknown = 1,
                        Sender = 4544,
                        Receiver = 1
                    },
                    Body = body
                });
            return stream.ToArray();
        }

        static int IndexOf(byte[] buffer, byte[] pattern)
        {
            for (int i = 0; i <= buffer.Length - pattern.Length; i++)
            {
                int j = 0;
                for (; j < pattern.Length; j++)
                {
                    if (buffer[i + j] != pattern[j])
                        break;
                }

                if (j == pattern.Length)
                    return i;
            }

            return -1;
        }
    }
}
