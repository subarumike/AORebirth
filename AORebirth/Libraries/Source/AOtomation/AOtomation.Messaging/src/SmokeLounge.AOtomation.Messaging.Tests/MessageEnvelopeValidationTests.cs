// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MessageEnvelopeValidationTests.cs" company="SmokeLounge">
//   Copyright (c) SmokeLounge.
// </copyright>
// <summary>
//   Locks the inbound message envelope validation boundary.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;

    [TestClass]
    public class MessageEnvelopeValidationTests
    {
        [TestMethod]
        public void ExactLengthPingEnvelopeDeserializes()
        {
            Message message = Deserialize(CreatePingPacket());

            Assert.IsNotNull(message);
            Assert.IsInstanceOfType(message.Body, typeof(PingMessage));
        }

        [TestMethod]
        public void ExactLengthKnownN3EnvelopeRoundTripsByteIdentically()
        {
            byte[] expected = CreateKnownN3Packet();

            byte[] actual = Serialize(Deserialize(expected));

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void DeclaredSizeSmallerThanAvailableBytesIsRejected()
        {
            byte[] packet = CreateKnownN3Packet();
            WriteDeclaredSize(packet, 20);

            Exception exception = CaptureDeserializeException(packet);

            Assert.IsInstanceOfType(exception, typeof(InvalidDataException));
            StringAssert.Contains(exception.Message, "size mismatch");
        }

        [TestMethod]
        public void DeclaredSizeSmallerThanRequiredDiscriminatorIsRejected()
        {
            byte[] packet = Truncate(CreateKnownN3Packet(), 19);
            WriteDeclaredSize(packet, packet.Length);

            Exception exception = CaptureDeserializeException(packet);

            Assert.IsInstanceOfType(exception, typeof(InvalidDataException));
            StringAssert.Contains(exception.Message, "discriminator at offset 16");
        }

        [TestMethod]
        public void DeclaredSizeLargerThanAvailableBytesIsRejected()
        {
            byte[] packet = Truncate(CreateKnownN3Packet(), 28);

            Exception exception = CaptureDeserializeException(packet);

            Assert.IsInstanceOfType(exception, typeof(InvalidDataException));
            StringAssert.Contains(exception.Message, "size mismatch");
        }

        [TestMethod]
        public void TruncatedHeaderIsRejected()
        {
            byte[] packet = Truncate(CreatePingPacket(), 15);

            Exception exception = CaptureDeserializeException(packet);

            Assert.IsInstanceOfType(exception, typeof(InvalidDataException));
            StringAssert.Contains(exception.Message, "header is truncated");
        }

        [TestMethod]
        public void TruncatedKnownBodyIsRejected()
        {
            byte[] packet = Truncate(CreateKnownN3Packet(), 28);
            WriteDeclaredSize(packet, packet.Length);

            Exception exception = CaptureDeserializeException(packet);

            Assert.AreEqual(typeof(Exception), exception.GetType());
        }

        [TestMethod]
        public void KnownPacketWithDeclaredTrailingByteIsRejected()
        {
            byte[] packet = Append(CreatePingPacket(), 0x7f);
            WriteDeclaredSize(packet, packet.Length);

            Exception exception = CaptureDeserializeException(packet);

            Assert.IsInstanceOfType(exception, typeof(InvalidDataException));
            StringAssert.Contains(exception.Message, "body consumption mismatch");
        }

        [TestMethod]
        public void UnknownN3SubtypeWithValidEnvelopeReturnsNull()
        {
            byte[] packet = CreateKnownN3Packet();
            packet[16] = 0x7f;
            packet[17] = 0xff;
            packet[18] = 0xff;
            packet[19] = 0xff;

            Message message = Deserialize(packet);

            Assert.IsNull(message);
        }

        [TestMethod]
        public void MalformedEnvelopeDoesNotInvokeDownstreamHandler()
        {
            bool invoked = false;
            byte[] packet = Truncate(CreatePingPacket(), 15);

            try
            {
                DispatchAfterDeserialize(packet, delegate { invoked = true; });
            }
            catch (InvalidDataException)
            {
            }

            Assert.IsFalse(invoked);
        }

        [TestMethod]
        public void MalformedEnvelopeDoesNotMutateHandlerOwnedState()
        {
            int handlerState = 41;
            byte[] packet = Append(CreatePingPacket(), 0x01);

            try
            {
                DispatchAfterDeserialize(packet, delegate { handlerState++; });
            }
            catch (InvalidDataException)
            {
            }

            Assert.AreEqual(41, handlerState);
        }

        [TestMethod]
        public void ConcatenatedValidFramesAreRejectedWithoutDispatch()
        {
            bool invoked = false;
            byte[] packet = Append(CreatePingPacket(), CreatePingPacket());

            Exception exception = null;
            try
            {
                DispatchAfterDeserialize(packet, delegate { invoked = true; });
            }
            catch (Exception caught)
            {
                exception = caught;
            }

            Assert.IsInstanceOfType(exception, typeof(InvalidDataException));
            Assert.IsFalse(invoked);
        }

        [TestMethod]
        public void RepeatedMalformedEnvelopeHasDeterministicRejection()
        {
            byte[] packet = Append(CreatePingPacket(), 0x01);

            Exception first = CaptureDeserializeException(packet);
            Exception second = CaptureDeserializeException(packet);

            Assert.AreEqual(first.GetType(), second.GetType());
            Assert.AreEqual(first.Message, second.Message);
        }

        [TestMethod]
        public void LoginAndZonePublishOnlyAfterSuccessfulDeserialize()
        {
            string repositoryRoot = FindRepositoryRoot();
            string zoneClient = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\ZoneClient.cs"));
            string loginClient = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\LoginEngine\CoreClient\Client.cs"));

            AssertPublishFollowsDeserialize(
                zoneClient,
                "message = this.messageSerializer.Deserialize(packet);",
                "this.bus.Publish(wrapped);");
            AssertPublishFollowsDeserialize(
                loginClient,
                "message = this.messageSerializer.Deserialize(packet);",
                "this.bus.Publish(new MessageReceivedEvent(this, message));");
        }

        [TestMethod]
        public void LoginAndZoneDiagnosticReadsGuardShortPackets()
        {
            string repositoryRoot = FindRepositoryRoot();
            string zoneClient = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\ZoneClient.cs"));
            string loginClient = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\LoginEngine\CoreClient\Client.cs"));

            Assert.AreEqual(2, CountOccurrences(zoneClient, "segment.Length < 20"));
            Assert.AreEqual(2, CountOccurrences(loginClient, "segment.Length < 20"));
            Assert.AreEqual(2, CountOccurrences(zoneClient, "return ((uint)segment[16] << 24)"));
            Assert.AreEqual(2, CountOccurrences(loginClient, "return ((uint)segment[16] << 24)"));
        }

        private static byte[] Append(byte[] packet, byte value)
        {
            byte[] result = new byte[packet.Length + 1];
            Buffer.BlockCopy(packet, 0, result, 0, packet.Length);
            result[result.Length - 1] = value;
            return result;
        }

        private static byte[] Append(byte[] first, byte[] second)
        {
            byte[] result = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, result, 0, first.Length);
            Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
            return result;
        }

        private static void AssertPublishFollowsDeserialize(
            string source,
            string deserializeMarker,
            string publishMarker)
        {
            int deserializeIndex = source.IndexOf(deserializeMarker, StringComparison.Ordinal);
            int publishIndex = source.IndexOf(publishMarker, StringComparison.Ordinal);

            Assert.IsTrue(deserializeIndex >= 0, "Missing deserialize marker.");
            Assert.IsTrue(publishIndex > deserializeIndex, "Publish must follow successful deserialize.");
        }

        private static Exception CaptureDeserializeException(byte[] packet)
        {
            Exception exception = null;
            try
            {
                Deserialize(packet);
            }
            catch (Exception caught)
            {
                exception = caught;
            }

            Assert.IsNotNull(exception, "Expected packet deserialization to fail.");
            return exception;
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        private static byte[] CreateKnownN3Packet()
        {
            return Serialize(
                new Message
                    {
                        Header = new Header
                                     {
                                         MessageId = 0x1234,
                                         PacketType = PacketType.N3Message,
                                         Sender = 1001,
                                         Receiver = 2002
                                     },
                        Body = new CharInPlayMessage
                                   {
                                       Identity = new Identity
                                                      {
                                                          Type = IdentityType.CanbeAffected,
                                                          Instance = 1001
                                                      }
                                   }
                    });
        }

        private static byte[] CreatePingPacket()
        {
            return Serialize(
                new Message
                    {
                        Header = new Header
                                     {
                                         MessageId = 0x1234,
                                         PacketType = PacketType.PingMessage,
                                         Sender = 1001,
                                         Receiver = 2002
                                     },
                        Body = new PingMessage()
                    });
        }

        private static Message Deserialize(byte[] packet)
        {
            var serializer = new MessageSerializer();
            using (var stream = new MemoryStream(packet))
            {
                return serializer.Deserialize(stream);
            }
        }

        private static void DispatchAfterDeserialize(byte[] packet, Action<Message> handler)
        {
            Message message = Deserialize(packet);
            if (message != null)
            {
                handler(message);
            }
        }

        private static string FindRepositoryRoot([CallerFilePath] string sourcePath = null)
        {
            string current = Path.GetDirectoryName(sourcePath);
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(Path.Combine(current, "AGENTS.md"))
                    && Directory.Exists(Path.Combine(current, @"AORebirth\Server")))
                {
                    return current;
                }

                DirectoryInfo parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }

            Assert.Fail("Unable to find AORebirth repository root from " + sourcePath + ".");
            return string.Empty;
        }

        private static byte[] Serialize(Message message)
        {
            var serializer = new MessageSerializer();
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(stream, message);
                return stream.ToArray();
            }
        }

        private static byte[] Truncate(byte[] packet, int length)
        {
            byte[] result = new byte[length];
            Buffer.BlockCopy(packet, 0, result, 0, length);
            return result;
        }

        private static void WriteDeclaredSize(byte[] packet, int length)
        {
            packet[6] = (byte)(length >> 8);
            packet[7] = (byte)length;
        }
    }
}
