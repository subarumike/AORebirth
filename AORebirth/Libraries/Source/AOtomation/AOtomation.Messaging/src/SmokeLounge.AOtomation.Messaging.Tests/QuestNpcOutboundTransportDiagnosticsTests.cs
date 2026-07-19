namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;

    [TestClass]
    public class QuestNpcOutboundTransportDiagnosticsTests
    {
        [TestMethod]
        public void FilterAcceptsOnlyTheThreePf655QuestNpcVisibilityPackets()
        {
            int[] runtimeInstances =
                {
                    QuestNpcOutboundTransportDiagnostics.KarrecRuntimeInstance,
                    QuestNpcOutboundTransportDiagnostics.AnnoyingDudeRuntimeInstance,
                    QuestNpcOutboundTransportDiagnostics.MaddyCardileRuntimeInstance
                };
            string[] names = { "Windcaller Karrec", "Annoying Dude", "Maddy Cardile" };
            Identity testClient = CharacterIdentity(
                QuestNpcOutboundTransportDiagnostics.TestClientRuntimeInstance);

            for (int index = 0; index < runtimeInstances.Length; index++)
            {
                Identity identity = CharacterIdentity(runtimeInstances[index]);
                Assert.IsTrue(
                    QuestNpcOutboundTransportDiagnostics.IsTrackedMessage(
                        new SimpleCharFullUpdateMessage { Identity = identity },
                        QuestNpcOutboundTransportDiagnostics.QuestNpcPlayfieldId,
                        testClient));
                Assert.IsTrue(
                    QuestNpcOutboundTransportDiagnostics.IsTrackedMessage(
                        new CharInPlayMessage { Identity = identity },
                        QuestNpcOutboundTransportDiagnostics.QuestNpcPlayfieldId,
                        testClient));
                Assert.AreEqual(
                    names[index],
                    QuestNpcOutboundTransportDiagnostics.NameForRuntimeInstance(runtimeInstances[index]));
            }

            Assert.IsFalse(
                QuestNpcOutboundTransportDiagnostics.IsTrackedMessage(
                    new SimpleCharFullUpdateMessage
                    {
                        Identity = CharacterIdentity(
                            QuestNpcOutboundTransportDiagnostics.KarrecRuntimeInstance)
                    },
                    127,
                    testClient));
            Assert.IsFalse(
                QuestNpcOutboundTransportDiagnostics.IsTrackedMessage(
                    new SimpleCharFullUpdateMessage { Identity = CharacterIdentity(1000003) },
                    QuestNpcOutboundTransportDiagnostics.QuestNpcPlayfieldId,
                    testClient));
            Assert.IsFalse(
                QuestNpcOutboundTransportDiagnostics.IsTrackedMessage(
                    new SimpleCharFullUpdateMessage
                    {
                        Identity = new Identity
                                   {
                                       Type = IdentityType.Terminal,
                                       Instance = QuestNpcOutboundTransportDiagnostics.KarrecRuntimeInstance
                                   }
                    },
                    QuestNpcOutboundTransportDiagnostics.QuestNpcPlayfieldId,
                    testClient));
            Assert.IsFalse(
                QuestNpcOutboundTransportDiagnostics.IsTrackedMessage(
                    new SimpleCharFullUpdateMessage
                    {
                        Identity = CharacterIdentity(
                            QuestNpcOutboundTransportDiagnostics.KarrecRuntimeInstance)
                    },
                    QuestNpcOutboundTransportDiagnostics.QuestNpcPlayfieldId,
                    CharacterIdentity(23)),
                "Diagnostics must remain limited to the captured test client identity.");
        }

        [TestMethod]
        public void InterleavedBuffersRetainTargetCorrelationThroughQueueAndWrite()
        {
            QuestNpcOutboundTransportDiagnostics.Reset();
            var events = new List<string>();
            try
            {
                Identity clientIdentity = CharacterIdentity(
                    QuestNpcOutboundTransportDiagnostics.TestClientRuntimeInstance);
                var karrec = new SimpleCharFullUpdateMessage
                             {
                                 Identity = CharacterIdentity(
                                     QuestNpcOutboundTransportDiagnostics.KarrecRuntimeInstance)
                             };
                var annoying = new CharInPlayMessage
                               {
                                   Identity = CharacterIdentity(
                                       QuestNpcOutboundTransportDiagnostics.AnnoyingDudeRuntimeInstance)
                               };
                byte[] karrecBuffer = BuildBuffer(
                    270,
                    0x271B3A6B,
                    QuestNpcOutboundTransportDiagnostics.KarrecRuntimeInstance,
                    clientIdentity.Instance);
                byte[] annoyingBuffer = BuildBuffer(
                    29,
                    0x570C2039,
                    QuestNpcOutboundTransportDiagnostics.AnnoyingDudeRuntimeInstance,
                    clientIdentity.Instance);

                QuestNpcOutboundTransportDiagnostics.OnSerialized(
                    "session-test",
                    clientIdentity,
                    "Transport Tester",
                    QuestNpcOutboundTransportDiagnostics.QuestNpcPlayfieldId,
                    karrec,
                    karrecBuffer,
                    events.Add);
                QuestNpcOutboundTransportDiagnostics.OnSerialized(
                    "session-test",
                    clientIdentity,
                    "Transport Tester",
                    QuestNpcOutboundTransportDiagnostics.QuestNpcPlayfieldId,
                    annoying,
                    annoyingBuffer,
                    events.Add);
                QuestNpcOutboundTransportDiagnostics.OnEnqueued(karrecBuffer, 1, events.Add);
                QuestNpcOutboundTransportDiagnostics.OnEnqueued(annoyingBuffer, 2, events.Add);

                Assert.AreEqual(2, QuestNpcOutboundTransportDiagnostics.PendingCount);
                QuestNpcOutboundTransportDiagnostics.OnDequeued(annoyingBuffer, 1, events.Add);
                CompleteSuccessfulWrite(annoyingBuffer, 2, 100, 200, events);
                QuestNpcOutboundTransportDiagnostics.OnDequeued(karrecBuffer, 0, events.Add);
                CompleteSuccessfulWrite(karrecBuffer, 3, 300, 400, events);

                Assert.AreEqual(0, QuestNpcOutboundTransportDiagnostics.PendingCount);
                string joined = string.Join("\n", events.ToArray());
                Assert.IsTrue(joined.Contains("\"session_id\":\"session-test\""));
                Assert.IsTrue(joined.Contains("\"target_identity_instance\":1000000"));
                Assert.IsTrue(joined.Contains("\"target_identity_instance\":1000001"));
                Assert.IsFalse(joined.Contains("\"target_identity_instance\":1000002"));
                Assert.IsTrue(joined.Contains("\"queue_result\":\"ENQUEUED\""));
                Assert.IsTrue(joined.Contains("\"event\":\"FLUSH_RETURNED\""));
                Assert.IsTrue(joined.Contains("\"transport_write_call_started\":true"));
                Assert.IsTrue(joined.Contains("\"socket_write_reached\":true"));
                Assert.IsTrue(joined.Contains("\"transport_bytes_accepted\":270"));
                Assert.IsTrue(joined.Contains("\"transport_bytes_accepted\":29"));
                Assert.IsTrue(
                    joined.Contains(
                        "\"transport_bytes_kind\":\"uncompressed_input_to_ZlibStream.Write\""));
                Assert.IsTrue(
                    joined.Contains(
                        "\"full_hex\":\"" + BitConverter.ToString(karrecBuffer).Replace("-", string.Empty) + "\""));
                Assert.IsTrue(
                    joined.Contains(
                        "\"full_hex\":\"" + BitConverter.ToString(annoyingBuffer).Replace("-", string.Empty) + "\""));
            }
            finally
            {
                QuestNpcOutboundTransportDiagnostics.Reset();
            }
        }

        [TestMethod]
        public void TypedCorrelationSurvivesMalformedWireFieldsThroughTransport()
        {
            QuestNpcOutboundTransportDiagnostics.Reset();
            var events = new List<string>();
            Identity clientIdentity = CharacterIdentity(
                QuestNpcOutboundTransportDiagnostics.TestClientRuntimeInstance);
            var karrec = new SimpleCharFullUpdateMessage
                         {
                             Identity = CharacterIdentity(
                                 QuestNpcOutboundTransportDiagnostics.KarrecRuntimeInstance)
                         };
            var malformedBuffer = new byte[270];

            try
            {
                bool tracked = QuestNpcOutboundTransportDiagnostics.OnSerialized(
                    "session-malformed-test",
                    clientIdentity,
                    "Transport Tester",
                    QuestNpcOutboundTransportDiagnostics.QuestNpcPlayfieldId,
                    karrec,
                    malformedBuffer,
                    events.Add);

                Assert.IsTrue(tracked, "Typed target ownership must establish correlation before wire validation.");
                Assert.IsFalse(
                    QuestNpcOutboundTransportDiagnostics.IsTrackedBuffer(malformedBuffer),
                    "The malformed wire fixture must not accidentally satisfy expected field parsing.");
                QuestNpcOutboundTransportDiagnostics.OnEnqueued(malformedBuffer, 1, events.Add);
                QuestNpcOutboundTransportDiagnostics.OnDequeued(malformedBuffer, 0, events.Add);
                CompleteSuccessfulWrite(malformedBuffer, 9, 100, 200, events);

                Assert.AreEqual(0, QuestNpcOutboundTransportDiagnostics.PendingCount);
                string joined = string.Join("\n", events.ToArray());
                Assert.IsTrue(joined.Contains("\"session_id\":\"session-malformed-test\""));
                Assert.IsTrue(joined.Contains("\"target_identity_instance\":1000000"));
                Assert.IsTrue(joined.Contains("\"message_opcode\":\"0x00000000\""));
                Assert.IsTrue(joined.Contains("\"header_receiver\":0"));
                Assert.IsTrue(joined.Contains("\"event\":\"FLUSH_RETURNED\""));
                Assert.IsTrue(joined.Contains("\"socket_write_reached\":true"));
            }
            finally
            {
                QuestNpcOutboundTransportDiagnostics.Reset();
            }
        }

        [TestMethod]
        public void QueueStateIsMarkedBeforeDeferredEnqueueEmission()
        {
            QuestNpcOutboundTransportDiagnostics.Reset();
            var events = new List<string>();
            Identity clientIdentity = CharacterIdentity(
                QuestNpcOutboundTransportDiagnostics.TestClientRuntimeInstance);
            var message = new SimpleCharFullUpdateMessage
                          {
                              Identity = CharacterIdentity(
                                  QuestNpcOutboundTransportDiagnostics.KarrecRuntimeInstance)
                          };
            byte[] buffer = BuildBuffer(270, 0x271B3A6B, 1000000, clientIdentity.Instance);

            try
            {
                Track(message, buffer, clientIdentity, events);
                QuestNpcOutboundTransportDiagnostics.MarkEnqueued(buffer);
                QuestNpcOutboundTransportDiagnostics.OnDequeued(buffer, 0, events.Add);
                CompleteSuccessfulWrite(buffer, 10, 200, 300, events);

                string terminal = events.Single(value => value.Contains("\"event\":\"FLUSH_RETURNED\""));
                Assert.IsTrue(terminal.Contains("\"queue_result\":\"ENQUEUED\""));
                Assert.AreEqual(0, QuestNpcOutboundTransportDiagnostics.PendingCount);
            }
            finally
            {
                QuestNpcOutboundTransportDiagnostics.Reset();
            }
        }

        [TestMethod]
        public void PendingCapacityExhaustionEmitsEvidenceAndSessionCleanupRecovers()
        {
            QuestNpcOutboundTransportDiagnostics.Reset();
            var events = new List<string>();
            Identity clientIdentity = CharacterIdentity(
                QuestNpcOutboundTransportDiagnostics.TestClientRuntimeInstance);
            var message = new SimpleCharFullUpdateMessage
                          {
                              Identity = CharacterIdentity(
                                  QuestNpcOutboundTransportDiagnostics.KarrecRuntimeInstance)
                          };

            try
            {
                for (int index = 0; index < 64; index++)
                {
                    Assert.IsTrue(
                        QuestNpcOutboundTransportDiagnostics.OnSerialized(
                            "session-capacity-test",
                            clientIdentity,
                            "Transport Tester",
                            QuestNpcOutboundTransportDiagnostics.QuestNpcPlayfieldId,
                            message,
                            BuildBuffer(270, 0x271B3A6B, 1000000, clientIdentity.Instance),
                            events.Add));
                }

                Assert.AreEqual(64, QuestNpcOutboundTransportDiagnostics.PendingCount);
                Assert.IsFalse(
                    QuestNpcOutboundTransportDiagnostics.OnSerialized(
                        "session-capacity-test",
                        clientIdentity,
                        "Transport Tester",
                        QuestNpcOutboundTransportDiagnostics.QuestNpcPlayfieldId,
                        message,
                        BuildBuffer(270, 0x271B3A6B, 1000000, clientIdentity.Instance),
                        events.Add));
                Assert.IsFalse(
                    QuestNpcOutboundTransportDiagnostics.OnSerialized(
                        "session-capacity-test",
                        clientIdentity,
                        "Transport Tester",
                        QuestNpcOutboundTransportDiagnostics.QuestNpcPlayfieldId,
                        message,
                        BuildBuffer(270, 0x271B3A6B, 1000000, clientIdentity.Instance),
                        events.Add));
                Assert.AreEqual(
                    1,
                    events.Count(value => value.Contains("\"event\":\"TRACKING_CAPACITY_EXHAUSTED\"")),
                    "A stalled transport must produce one bounded capacity warning, not log spam.");

                QuestNpcOutboundTransportDiagnostics.OnSessionDisposed("session-capacity-test", events.Add);
                Assert.AreEqual(0, QuestNpcOutboundTransportDiagnostics.PendingCount);
            }
            finally
            {
                QuestNpcOutboundTransportDiagnostics.Reset();
            }
        }

        [TestMethod]
        public void QueueDropAndWriteFailureRecordEvidenceAndReleaseCorrelation()
        {
            QuestNpcOutboundTransportDiagnostics.Reset();
            var events = new List<string>();
            Identity clientIdentity = CharacterIdentity(
                QuestNpcOutboundTransportDiagnostics.TestClientRuntimeInstance);
            var message = new SimpleCharFullUpdateMessage
                          {
                              Identity = CharacterIdentity(
                                  QuestNpcOutboundTransportDiagnostics.MaddyCardileRuntimeInstance)
                          };

            try
            {
                byte[] dropped = BuildBuffer(
                    282,
                    0x271B3A6B,
                    QuestNpcOutboundTransportDiagnostics.MaddyCardileRuntimeInstance,
                    clientIdentity.Instance);
                Track(message, dropped, clientIdentity, events);
                QuestNpcOutboundTransportDiagnostics.OnEnqueued(dropped, 1, events.Add);
                QuestNpcOutboundTransportDiagnostics.OnTransportUnavailable(
                    dropped,
                    "network stream is not writable",
                    events.Add);
                Assert.AreEqual(0, QuestNpcOutboundTransportDiagnostics.PendingCount);

                byte[] failed = BuildBuffer(
                    282,
                    0x271B3A6B,
                    QuestNpcOutboundTransportDiagnostics.MaddyCardileRuntimeInstance,
                    clientIdentity.Instance);
                Track(message, failed, clientIdentity, events);
                QuestNpcOutboundTransportDiagnostics.OnEnqueued(failed, 1, events.Add);
                QuestNpcOutboundTransportDiagnostics.OnDequeued(failed, 0, events.Add);
                QuestNpcOutboundTransportDiagnostics.OnWriteStarted(failed);
                QuestNpcOutboundTransportDiagnostics.OnWriteFailed(
                    failed,
                    new IOException("synthetic write failure"),
                    10,
                    20,
                    events.Add);
                Assert.AreEqual(0, QuestNpcOutboundTransportDiagnostics.PendingCount);

                byte[] queueFailed = BuildBuffer(
                    282,
                    0x271B3A6B,
                    QuestNpcOutboundTransportDiagnostics.MaddyCardileRuntimeInstance,
                    clientIdentity.Instance);
                Track(message, queueFailed, clientIdentity, events);
                QuestNpcOutboundTransportDiagnostics.OnQueueFailed(
                    queueFailed,
                    new InvalidOperationException("synthetic queue failure"),
                    events.Add);
                Assert.AreEqual(0, QuestNpcOutboundTransportDiagnostics.PendingCount);

                byte[] flushFailed = BuildBuffer(
                    282,
                    0x271B3A6B,
                    QuestNpcOutboundTransportDiagnostics.MaddyCardileRuntimeInstance,
                    clientIdentity.Instance);
                Track(message, flushFailed, clientIdentity, events);
                QuestNpcOutboundTransportDiagnostics.OnEnqueued(flushFailed, 1, events.Add);
                QuestNpcOutboundTransportDiagnostics.OnDequeued(flushFailed, 0, events.Add);
                QuestNpcOutboundTransportDiagnostics.OnWriteStarted(flushFailed);
                QuestNpcOutboundTransportDiagnostics.OnWriteReturned(flushFailed, 282, 282, 100);
                QuestNpcOutboundTransportDiagnostics.OnFlushFailed(
                    flushFailed,
                    new IOException("synthetic flush failure"),
                    282,
                    100,
                    events.Add);
                Assert.AreEqual(0, QuestNpcOutboundTransportDiagnostics.PendingCount);

                byte[] abandoned = BuildBuffer(
                    282,
                    0x271B3A6B,
                    QuestNpcOutboundTransportDiagnostics.MaddyCardileRuntimeInstance,
                    clientIdentity.Instance);
                Track(message, abandoned, clientIdentity, events);
                QuestNpcOutboundTransportDiagnostics.OnEnqueued(abandoned, 1, events.Add);
                QuestNpcOutboundTransportDiagnostics.OnSessionDisposed("session-failure-test", events.Add);
                Assert.AreEqual(0, QuestNpcOutboundTransportDiagnostics.PendingCount);

                QuestNpcOutboundTransportDiagnostics.OnSerialized(
                    "session-invalid-test",
                    clientIdentity,
                    "Transport Tester",
                    QuestNpcOutboundTransportDiagnostics.QuestNpcPlayfieldId,
                    message,
                    new byte[1],
                    events.Add);
                Assert.AreEqual(0, QuestNpcOutboundTransportDiagnostics.PendingCount);

                string joined = string.Join("\n", events.ToArray());
                Assert.IsTrue(joined.Contains("\"event\":\"DROPPED\""));
                Assert.IsTrue(joined.Contains("network stream is not writable"));
                Assert.IsTrue(joined.Contains("\"event\":\"WRITE_FAILED\""));
                Assert.IsTrue(joined.Contains("synthetic write failure"));
                Assert.IsTrue(joined.Contains("\"event\":\"QUEUE_FAILED\""));
                Assert.IsTrue(joined.Contains("synthetic queue failure"));
                Assert.IsTrue(joined.Contains("\"event\":\"FLUSH_FAILED\""));
                Assert.IsTrue(joined.Contains("synthetic flush failure"));
                Assert.IsTrue(joined.Contains("\"event\":\"SESSION_DISPOSED_DROP\""));
                Assert.IsTrue(joined.Contains("\"event\":\"DROPPED_INVALID_BUFFER\""));
                Assert.IsFalse(
                    joined.Contains("\"socket_write_reached\":true"),
                    "Failed or abandoned packets must not claim a successful socket-boundary flush.");
            }
            finally
            {
                QuestNpcOutboundTransportDiagnostics.Reset();
            }
        }

        [TestMethod]
        public void ZoneClientDoesNotReadZlibCountersBeforeTheFirstTransportWrite()
        {
            string repositoryRoot = FindRepositoryRoot();
            string zoneClientText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\ZoneClient.cs"));
            string sendCompressed = ExtractMethodBlock(
                zoneClientText,
                "private void SendCompressed(byte[] buffer, bool traceQuestNpcTransport)");
            string enqueueMessage = ExtractMethodBlock(
                zoneClientText,
                "public void SendCompressed(MessageBody messageBody, int sender)");
            string dispatchMessages = ExtractMethodBlock(zoneClientText, "private void DispatchMessages()");
            int writeIndex = sendCompressed.IndexOf(
                "this.zStream.Write(buffer, 0, buffer.Length);",
                StringComparison.Ordinal);
            int flushIndex = sendCompressed.IndexOf("this.zStream.Flush();", StringComparison.Ordinal);
            int terminalEmitIndex = sendCompressed.IndexOf(
                "QuestNpcOutboundTransportDiagnostics.OnFlushReturned(",
                StringComparison.Ordinal);

            Assert.IsTrue(writeIndex >= 0, "Expected the compressed socket-write boundary.");
            Assert.IsTrue(flushIndex > writeIndex, "Flush must follow the compressed transport write.");
            string beforeWrite = sendCompressed.Substring(0, writeIndex);
            Assert.IsFalse(
                beforeWrite.Contains("this.zStream.TotalIn") || beforeWrite.Contains("this.zStream.TotalOut"),
                "Ionic.Zlib counters are not initialized until the first write and must not be read beforehand.");
            Assert.IsTrue(
                sendCompressed.IndexOf("ZlibTotalInOrUnavailable(this.zStream)", StringComparison.Ordinal) > writeIndex
                && sendCompressed.IndexOf("ZlibTotalOutOrUnavailable(this.zStream)", StringComparison.Ordinal) > writeIndex,
                "Transport diagnostics may sample guarded zlib counters only after the write returns.");
            Assert.IsFalse(
                sendCompressed.Substring(writeIndex, flushIndex - writeIndex)
                    .Contains("EmitQuestNpcOutboundTransportDiagnostic"),
                "Diagnostics must not log between ZlibStream.Write and Flush.");
            Assert.IsTrue(
                terminalEmitIndex > flushIndex
                && sendCompressed.IndexOf(
                    "if (traceQuestNpcTransport && flushReturned)",
                    StringComparison.Ordinal) < terminalEmitIndex,
                "The successful terminal diagnostic must emit only after Flush and the transport lock complete.");
            Assert.IsTrue(
                zoneClientText.Contains("new QueuedOutboundPacket(buffer, traceQuestNpcTransport)")
                && zoneClientText.Contains(
                    "this.SendCompressed(queuedPacket.Buffer, queuedPacket.TraceQuestNpcTransport)"),
                "Typed diagnostic correlation must travel with the queued byte-array reference even when wire fields are malformed.");
            AssertTextBefore(
                enqueueMessage,
                "this.sendQueue.Enqueue(queuedPacket);",
                "QuestNpcOutboundTransportDiagnostics.MarkEnqueued(buffer);");
            Assert.IsFalse(
                enqueueMessage.Contains("EmitEnqueued("),
                "Enqueue diagnostics must not perform log I/O while the send queue is locked.");
            AssertTextBefore(
                dispatchMessages,
                "QuestNpcOutboundTransportDiagnostics.EmitEnqueued(",
                "QuestNpcOutboundTransportDiagnostics.OnDequeued(");
            AssertTextBefore(
                dispatchMessages,
                "QuestNpcOutboundTransportDiagnostics.OnDequeued(",
                "this.SendCompressed(queuedPacket.Buffer, queuedPacket.TraceQuestNpcTransport);");
        }

        private static void Track(
            SimpleCharFullUpdateMessage message,
            byte[] buffer,
            Identity clientIdentity,
            ICollection<string> events)
        {
            Assert.IsTrue(
                QuestNpcOutboundTransportDiagnostics.OnSerialized(
                    "session-failure-test",
                    clientIdentity,
                    "Transport Tester",
                    QuestNpcOutboundTransportDiagnostics.QuestNpcPlayfieldId,
                    message,
                    buffer,
                    events.Add));
        }

        private static void CompleteSuccessfulWrite(
            byte[] buffer,
            int packetNumber,
            long totalIn,
            long totalOut,
            ICollection<string> events)
        {
            buffer[0] = (byte)(packetNumber >> 8);
            buffer[1] = (byte)packetNumber;
            QuestNpcOutboundTransportDiagnostics.OnPacketNumberAssigned(buffer);
            QuestNpcOutboundTransportDiagnostics.OnWriteStarted(buffer);
            QuestNpcOutboundTransportDiagnostics.OnWriteReturned(
                buffer,
                buffer.Length,
                totalIn + buffer.Length,
                totalOut + 10);
            QuestNpcOutboundTransportDiagnostics.OnFlushReturned(
                buffer,
                totalIn + buffer.Length,
                totalOut + 20,
                events.Add);
        }

        private static byte[] BuildBuffer(int length, int opcode, int targetInstance, int receiverInstance)
        {
            var buffer = new byte[length];
            buffer[0] = 0xDF;
            buffer[1] = 0xDF;
            buffer[2] = 0x00;
            buffer[3] = 0x0A;
            buffer[4] = 0x00;
            buffer[5] = 0x01;
            buffer[6] = (byte)(length >> 8);
            buffer[7] = (byte)length;
            WriteInt32BigEndian(buffer, 12, receiverInstance);
            WriteInt32BigEndian(buffer, 16, opcode);
            WriteInt32BigEndian(buffer, 20, (int)IdentityType.CanbeAffected);
            WriteInt32BigEndian(buffer, 24, targetInstance);
            return buffer;
        }

        private static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static Identity CharacterIdentity(int instance)
        {
            return new Identity { Type = IdentityType.CanbeAffected, Instance = instance };
        }

        private static string ExtractMethodBlock(string source, string methodMarker)
        {
            int methodStart = source.IndexOf(methodMarker, StringComparison.Ordinal);
            Assert.IsTrue(methodStart >= 0, "Missing method marker: " + methodMarker);
            int braceStart = source.IndexOf('{', methodStart);
            Assert.IsTrue(braceStart >= 0, "Missing method body for: " + methodMarker);
            int depth = 0;
            for (int index = braceStart; index < source.Length; index++)
            {
                if (source[index] == '{')
                {
                    depth++;
                }
                else if (source[index] == '}' && --depth == 0)
                {
                    return source.Substring(methodStart, index - methodStart + 1);
                }
            }

            Assert.Fail("Unterminated method body for: " + methodMarker);
            return string.Empty;
        }

        private static void AssertTextBefore(string text, string first, string second)
        {
            int firstIndex = text.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = text.IndexOf(second, StringComparison.Ordinal);
            Assert.IsTrue(firstIndex >= 0, "Missing expected text: " + first);
            Assert.IsTrue(secondIndex >= 0, "Missing expected text: " + second);
            Assert.IsTrue(firstIndex < secondIndex, first + " must occur before " + second);
        }

        private static string FindRepositoryRoot([CallerFilePath] string sourcePath = null)
        {
            string current = Path.GetDirectoryName(sourcePath);
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(Path.Combine(current, @"AORebirth\Server\ZoneEngine\Core")))
                {
                    return current;
                }

                current = Directory.GetParent(current) == null ? null : Directory.GetParent(current).FullName;
            }

            Assert.Fail("Could not locate the AORebirth repository root.");
            return string.Empty;
        }
    }
}
