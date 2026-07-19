namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    internal static class QuestNpcOutboundTransportDiagnostics
    {
        internal const int QuestNpcPlayfieldId = 655;

        internal const int KarrecRuntimeInstance = 1000000;

        internal const int AnnoyingDudeRuntimeInstance = 1000001;

        internal const int MaddyCardileRuntimeInstance = 1000002;

        internal const int TestClientRuntimeInstance = 22;

        private const int MessageHeaderLength = 16;

        private const int MaximumPendingPackets = 64;

        private static readonly object PendingSync = new object();

        private static readonly Dictionary<byte[], TrackedPacket> PendingPackets =
            new Dictionary<byte[], TrackedPacket>(new ByteArrayReferenceComparer());

        private static bool capacityExhaustionReported;

        internal static int PendingCount
        {
            get
            {
                lock (PendingSync)
                {
                    return PendingPackets.Count;
                }
            }
        }

        internal static bool IsTrackedMessage(MessageBody body, int playfieldId, Identity clientIdentity)
        {
            if (playfieldId != QuestNpcPlayfieldId
                || clientIdentity.Type != IdentityType.CanbeAffected
                || clientIdentity.Instance != TestClientRuntimeInstance)
            {
                return false;
            }

            var n3Message = body as N3Message;
            if (n3Message == null
                || (!(body is SimpleCharFullUpdateMessage) && !(body is CharInPlayMessage))
                || n3Message.Identity.Type != IdentityType.CanbeAffected)
            {
                return false;
            }

            return IsTrackedRuntimeInstance(n3Message.Identity.Instance);
        }

        internal static bool IsTrackedBuffer(byte[] buffer)
        {
            if (buffer == null || buffer.Length < 29)
            {
                return false;
            }

            int opcode = ReadInt32BigEndian(buffer, 16);
            if (opcode != 0x271B3A6B && opcode != 0x570C2039)
            {
                return false;
            }

            return ReadInt32BigEndian(buffer, 12) == TestClientRuntimeInstance
                   && ReadInt32BigEndian(buffer, 20) == (int)IdentityType.CanbeAffected
                   && IsTrackedRuntimeInstance(ReadInt32BigEndian(buffer, 24));
        }

        internal static string NameForRuntimeInstance(int runtimeInstance)
        {
            switch (runtimeInstance)
            {
                case KarrecRuntimeInstance:
                    return "Windcaller Karrec";
                case AnnoyingDudeRuntimeInstance:
                    return "Annoying Dude";
                case MaddyCardileRuntimeInstance:
                    return "Maddy Cardile";
                default:
                    return string.Empty;
            }
        }

        internal static bool OnSerialized(
            string sessionId,
            Identity clientIdentity,
            string clientName,
            int playfieldId,
            MessageBody body,
            byte[] buffer,
            Action<string> emit)
        {
            try
            {
                if (!IsTrackedMessage(body, playfieldId, clientIdentity))
                {
                    return false;
                }

                var n3Message = (N3Message)body;
                var record = new TrackedPacket(
                    sessionId,
                    clientIdentity,
                    clientName,
                    playfieldId,
                    n3Message.Identity,
                    NameForRuntimeInstance(n3Message.Identity.Instance),
                    body.GetType().Name,
                    buffer ?? new byte[0]);
                if (buffer == null || buffer.Length < 29)
                {
                    record.QueueResult = "DROPPED";
                    WriteEvent(
                        record,
                        "DROPPED_INVALID_BUFFER",
                        -1,
                        "serialized message is shorter than the minimum tracked wrapper",
                        emit);
                    return false;
                }

                bool capacityExhausted;
                bool reportCapacityExhaustion;
                lock (PendingSync)
                {
                    capacityExhausted = PendingPackets.Count >= MaximumPendingPackets;
                    reportCapacityExhaustion = capacityExhausted && !capacityExhaustionReported;
                    if (reportCapacityExhaustion)
                    {
                        capacityExhaustionReported = true;
                    }

                    if (!capacityExhausted)
                    {
                        PendingPackets[buffer] = record;
                    }
                }

                if (capacityExhausted)
                {
                    if (reportCapacityExhaustion)
                    {
                        record.QueueResult = "NOT_TRACKED";
                        WriteEvent(
                            record,
                            "TRACKING_CAPACITY_EXHAUSTED",
                            -1,
                            "pending diagnostic capacity is 64 packets",
                            emit);
                    }

                    return false;
                }

                WriteEvent(record, "SERIALIZED", -1, string.Empty, emit);
                return true;
            }
            catch
            {
                // Diagnostics must never alter packet delivery.
                return false;
            }
        }

        internal static void OnEnqueued(byte[] buffer, int queueDepth, Action<string> emit)
        {
            MarkEnqueued(buffer);
            EmitEnqueued(buffer, queueDepth, emit);
        }

        internal static void MarkEnqueued(byte[] buffer)
        {
            TrackedPacket record = Find(buffer);
            if (record == null)
            {
                return;
            }

            record.QueueResult = "ENQUEUED";
        }

        internal static void EmitEnqueued(byte[] buffer, int queueDepth, Action<string> emit)
        {
            TrackedPacket record = Find(buffer);
            if (record == null)
            {
                return;
            }

            WriteEvent(record, "ENQUEUED", queueDepth, string.Empty, emit);
        }

        internal static void OnQueueFailed(byte[] buffer, Exception exception, Action<string> emit)
        {
            TrackedPacket record = Take(buffer);
            if (record == null)
            {
                return;
            }

            record.QueueResult = "FAILED";
            WriteEvent(record, "QUEUE_FAILED", -1, ExceptionText(exception), emit);
        }

        internal static void OnDequeued(byte[] buffer, int remainingQueueDepth, Action<string> emit)
        {
            TrackedPacket record = Find(buffer);
            if (record != null)
            {
                WriteEvent(record, "DEQUEUED", remainingQueueDepth, string.Empty, emit);
            }
        }

        internal static void OnPacketNumberAssigned(byte[] buffer)
        {
            TrackedPacket record = Find(buffer);
            if (record == null)
            {
                return;
            }

            record.PacketNumber = ReadUInt16BigEndian(buffer, 0);
        }

        internal static void OnWriteStarted(byte[] buffer)
        {
            TrackedPacket record = Find(buffer);
            if (record == null)
            {
                return;
            }

            record.TransportWriteCallStarted = true;
            record.TransportCall = "ZlibStream.Write";
        }

        internal static void OnWriteReturned(
            byte[] buffer,
            int bytesAccepted,
            long zlibTotalIn,
            long zlibTotalOut)
        {
            TrackedPacket record = Find(buffer);
            if (record == null)
            {
                return;
            }

            record.TransportBytesAccepted = bytesAccepted;
            record.TransportBytesKind = "uncompressed_input_to_ZlibStream.Write";
            record.ZlibTotalIn = zlibTotalIn;
            record.ZlibTotalOut = zlibTotalOut;
        }

        internal static void OnFlushReturned(
            byte[] buffer,
            long zlibTotalIn,
            long zlibTotalOut,
            Action<string> emit)
        {
            TrackedPacket record = Take(buffer);
            if (record == null)
            {
                return;
            }

            record.SocketWriteReached = true;
            record.ZlibTotalIn = zlibTotalIn;
            record.ZlibTotalOut = zlibTotalOut;
            WriteEvent(record, "FLUSH_RETURNED", -1, string.Empty, emit);
        }

        internal static void OnTransportUnavailable(byte[] buffer, string reason, Action<string> emit)
        {
            TrackedPacket record = Take(buffer);
            if (record != null)
            {
                WriteEvent(record, "DROPPED", -1, reason ?? string.Empty, emit);
            }
        }

        internal static void OnWriteFailed(
            byte[] buffer,
            Exception exception,
            long zlibTotalIn,
            long zlibTotalOut,
            Action<string> emit)
        {
            TrackedPacket record = Take(buffer);
            if (record == null)
            {
                return;
            }

            record.ZlibTotalIn = zlibTotalIn;
            record.ZlibTotalOut = zlibTotalOut;
            WriteEvent(record, "WRITE_FAILED", -1, ExceptionText(exception), emit);
        }

        internal static void OnFlushFailed(
            byte[] buffer,
            Exception exception,
            long zlibTotalIn,
            long zlibTotalOut,
            Action<string> emit)
        {
            TrackedPacket record = Take(buffer);
            if (record == null)
            {
                return;
            }

            record.ZlibTotalIn = zlibTotalIn;
            record.ZlibTotalOut = zlibTotalOut;
            WriteEvent(record, "FLUSH_FAILED", -1, ExceptionText(exception), emit);
        }

        internal static void OnSessionDisposed(string sessionId, Action<string> emit)
        {
            var abandoned = new List<TrackedPacket>();
            var keys = new List<byte[]>();
            lock (PendingSync)
            {
                foreach (KeyValuePair<byte[], TrackedPacket> entry in PendingPackets)
                {
                    if (string.Equals(entry.Value.SessionId, sessionId, StringComparison.Ordinal))
                    {
                        keys.Add(entry.Key);
                        abandoned.Add(entry.Value);
                    }
                }

                foreach (byte[] key in keys)
                {
                    PendingPackets.Remove(key);
                }

                if (PendingPackets.Count < MaximumPendingPackets)
                {
                    capacityExhaustionReported = false;
                }
            }

            foreach (TrackedPacket record in abandoned)
            {
                record.QueueResult = "DROPPED";
                WriteEvent(
                    record,
                    "SESSION_DISPOSED_DROP",
                    -1,
                    "client session disposed before transport completion",
                    emit);
            }
        }

        internal static void Reset()
        {
            lock (PendingSync)
            {
                PendingPackets.Clear();
                capacityExhaustionReported = false;
            }
        }

        private static bool IsTrackedRuntimeInstance(int runtimeInstance)
        {
            return runtimeInstance == KarrecRuntimeInstance
                   || runtimeInstance == AnnoyingDudeRuntimeInstance
                   || runtimeInstance == MaddyCardileRuntimeInstance;
        }

        private static TrackedPacket Find(byte[] buffer)
        {
            if (buffer == null)
            {
                return null;
            }

            lock (PendingSync)
            {
                TrackedPacket record;
                PendingPackets.TryGetValue(buffer, out record);
                return record;
            }
        }

        private static TrackedPacket Take(byte[] buffer)
        {
            if (buffer == null)
            {
                return null;
            }

            lock (PendingSync)
            {
                TrackedPacket record;
                if (!PendingPackets.TryGetValue(buffer, out record))
                {
                    return null;
                }

                PendingPackets.Remove(buffer);
                if (PendingPackets.Count < MaximumPendingPackets)
                {
                    capacityExhaustionReported = false;
                }

                return record;
            }
        }

        private static void WriteEvent(
            TrackedPacket record,
            string eventName,
            int queueDepth,
            string exception,
            Action<string> emit)
        {
            try
            {
                var builder = new StringBuilder();
                builder.Append("QUEST_NPC_OUTBOUND ");
                builder.Append('{');
                AppendJson(builder, "timestamp_utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), true);
                AppendJson(builder, "session_id", record.SessionId, false);
                AppendJson(builder, "client_identity_type", record.ClientIdentityType, false);
                AppendJson(builder, "client_identity_instance", record.ClientIdentityInstance, false);
                AppendJson(builder, "client_name", record.ClientName, false);
                AppendJson(builder, "playfield_id", record.PlayfieldId, false);
                AppendJson(builder, "target_identity_type", record.TargetIdentityType, false);
                AppendJson(builder, "target_identity_instance", record.TargetIdentityInstance, false);
                AppendJson(builder, "npc_name", record.NpcName, false);
                AppendJson(builder, "message_type", record.MessageType, false);
                AppendJson(builder, "message_opcode", ReadUInt32BigEndianHex(record.Buffer, 16), false);
                AppendJson(builder, "total_serialized_length", record.Buffer.Length, false);
                AppendJson(builder, "declared_length", ReadUInt16BigEndian(record.Buffer, 6), false);
                AppendJson(builder, "body_length", Math.Max(0, record.Buffer.Length - MessageHeaderLength), false);
                AppendJson(builder, "header_receiver", ReadInt32BigEndian(record.Buffer, 12), false);
                AppendJson(builder, "event", eventName, false);
                AppendJson(builder, "queue_result", record.QueueResult, false);
                AppendJson(builder, "queue_depth", queueDepth, false);
                AppendJson(builder, "packet_number", record.PacketNumber, false);
                AppendJson(builder, "transport_write_call_started", record.TransportWriteCallStarted, false);
                AppendJson(builder, "socket_write_reached", record.SocketWriteReached, false);
                AppendJson(builder, "transport_call", record.TransportCall, false);
                AppendJson(builder, "transport_bytes_accepted", record.TransportBytesAccepted, false);
                AppendJson(builder, "transport_bytes_kind", record.TransportBytesKind, false);
                AppendJson(builder, "zlib_total_in", record.ZlibTotalIn, false);
                AppendJson(builder, "zlib_total_out", record.ZlibTotalOut, false);
                AppendJson(builder, "exception", exception ?? string.Empty, false);
                if (IncludesPayload(eventName))
                {
                    AppendJson(builder, "sha256", Sha256(record.Buffer), false);
                    AppendJson(builder, "full_hex", BitConverter.ToString(record.Buffer).Replace("-", string.Empty), false);
                }

                builder.Append('}');
                SafeEmit(emit, builder.ToString());
            }
            catch
            {
                // Diagnostics must never alter packet delivery.
            }
        }

        private static bool IncludesPayload(string eventName)
        {
            return eventName == "SERIALIZED"
                   || eventName == "FLUSH_RETURNED"
                   || eventName == "WRITE_FAILED"
                   || eventName == "FLUSH_FAILED"
                   || eventName == "QUEUE_FAILED"
                   || eventName == "DROPPED"
                   || eventName == "DROPPED_INVALID_BUFFER"
                   || eventName == "TRACKING_CAPACITY_EXHAUSTED"
                   || eventName == "SESSION_DISPOSED_DROP";
        }

        private static string ExceptionText(Exception exception)
        {
            return exception == null ? string.Empty : exception.ToString();
        }

        private static string Sha256(byte[] buffer)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(buffer)).Replace("-", string.Empty);
            }
        }

        private static ushort ReadUInt16BigEndian(byte[] buffer, int offset)
        {
            if (buffer == null || offset < 0 || buffer.Length < offset + 2)
            {
                return 0;
            }

            return (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
        }

        private static int ReadInt32BigEndian(byte[] buffer, int offset)
        {
            if (buffer == null || offset < 0 || buffer.Length < offset + 4)
            {
                return 0;
            }

            return (buffer[offset] << 24)
                   | (buffer[offset + 1] << 16)
                   | (buffer[offset + 2] << 8)
                   | buffer[offset + 3];
        }

        private static string ReadUInt32BigEndianHex(byte[] buffer, int offset)
        {
            if (buffer == null || offset < 0 || buffer.Length < offset + 4)
            {
                return string.Empty;
            }

            uint value = ((uint)buffer[offset] << 24)
                         | ((uint)buffer[offset + 1] << 16)
                         | ((uint)buffer[offset + 2] << 8)
                         | buffer[offset + 3];
            return "0x" + value.ToString("X8", CultureInfo.InvariantCulture);
        }

        private static void SafeEmit(Action<string> emit, string message)
        {
            if (emit == null)
            {
                return;
            }

            try
            {
                emit(message);
            }
            catch
            {
                // Diagnostics must never alter packet delivery.
            }
        }

        private static void AppendJson(StringBuilder builder, string name, string value, bool first)
        {
            AppendName(builder, name, first);
            builder.Append('"');
            builder.Append(EscapeJson(value ?? string.Empty));
            builder.Append('"');
        }

        private static void AppendJson(StringBuilder builder, string name, int value, bool first)
        {
            AppendName(builder, name, first);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendJson(StringBuilder builder, string name, long value, bool first)
        {
            AppendName(builder, name, first);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendJson(StringBuilder builder, string name, bool value, bool first)
        {
            AppendName(builder, name, first);
            builder.Append(value ? "true" : "false");
        }

        private static void AppendName(StringBuilder builder, string name, bool first)
        {
            if (!first)
            {
                builder.Append(',');
            }

            builder.Append('"');
            builder.Append(name);
            builder.Append("\":");
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private sealed class TrackedPacket
        {
            internal TrackedPacket(
                string sessionId,
                Identity clientIdentity,
                string clientName,
                int playfieldId,
                Identity targetIdentity,
                string npcName,
                string messageType,
                byte[] buffer)
            {
                this.SessionId = sessionId ?? string.Empty;
                this.ClientIdentityType = (int)clientIdentity.Type;
                this.ClientIdentityInstance = clientIdentity.Instance;
                this.ClientName = clientName ?? string.Empty;
                this.PlayfieldId = playfieldId;
                this.TargetIdentityType = (int)targetIdentity.Type;
                this.TargetIdentityInstance = targetIdentity.Instance;
                this.NpcName = npcName ?? string.Empty;
                this.MessageType = messageType ?? string.Empty;
                this.Buffer = buffer;
                this.QueueResult = "PENDING";
                this.PacketNumber = -1;
                this.TransportCall = string.Empty;
                this.TransportBytesKind = string.Empty;
                this.TransportBytesAccepted = 0;
                this.ZlibTotalIn = -1;
                this.ZlibTotalOut = -1;
            }

            internal string SessionId { get; private set; }

            internal int ClientIdentityType { get; private set; }

            internal int ClientIdentityInstance { get; private set; }

            internal string ClientName { get; private set; }

            internal int PlayfieldId { get; private set; }

            internal int TargetIdentityType { get; private set; }

            internal int TargetIdentityInstance { get; private set; }

            internal string NpcName { get; private set; }

            internal string MessageType { get; private set; }

            internal byte[] Buffer { get; private set; }

            internal string QueueResult { get; set; }

            internal int PacketNumber { get; set; }

            internal bool TransportWriteCallStarted { get; set; }

            internal bool SocketWriteReached { get; set; }

            internal string TransportCall { get; set; }

            internal int TransportBytesAccepted { get; set; }

            internal string TransportBytesKind { get; set; }

            internal long ZlibTotalIn { get; set; }

            internal long ZlibTotalOut { get; set; }
        }

        private sealed class ByteArrayReferenceComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] first, byte[] second)
            {
                return ReferenceEquals(first, second);
            }

            public int GetHashCode(byte[] value)
            {
                return RuntimeHelpers.GetHashCode(value);
            }
        }
    }
}
