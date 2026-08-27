namespace AORebirth.CaptureProtocol
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    internal static class RawStatDecoder
    {
        internal const int StatMessageType = 0x2B333D6E;
        private const int MaximumStatCount = 4096;

        internal static bool IsStatPacket(byte[] packet)
        {
            return packet != null
                   && packet.Length >= RawSimpleCharFullUpdateDecoder.N3BodyOffset + 4
                   && ReadInt32BigEndian(packet, RawSimpleCharFullUpdateDecoder.N3BodyOffset)
                   == StatMessageType;
        }

        internal static bool TryDecodePacket(byte[] packet, out RawStatMessage result, out string error)
        {
            try
            {
                result = DecodePacket(packet);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                result = null;
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        internal static RawStatMessage DecodePacket(byte[] packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException("packet");
            }

            if (packet.Length < RawSimpleCharFullUpdateDecoder.N3BodyOffset + 17)
            {
                throw new InvalidDataException("Stat packet is too short for its fixed fields.");
            }

            int declaredPacketLength = (packet[6] << 8) | packet[7];
            if (declaredPacketLength != packet.Length)
            {
                throw new InvalidDataException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Stat frame length mismatch: header={0}, actual={1}.",
                        declaredPacketLength,
                        packet.Length));
            }

            var body = new byte[packet.Length - RawSimpleCharFullUpdateDecoder.N3BodyOffset];
            Buffer.BlockCopy(packet, RawSimpleCharFullUpdateDecoder.N3BodyOffset, body, 0, body.Length);
            var reader = new RawStatBigEndianReader(body);
            int messageType = reader.ReadInt32("N3MessageType");
            if (messageType != StatMessageType)
            {
                throw new InvalidDataException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Expected Stat message type 0x{0:X8}, received 0x{1:X8}.",
                        StatMessageType,
                        messageType));
            }

            var result = new RawStatMessage
            {
                RawPacket = (byte[])packet.Clone(),
                RawBody = body,
                N3MessageType = messageType,
                Identity = new RawScfuIdentity
                {
                    Type = reader.ReadInt32("IdentityType"),
                    Instance = reader.ReadInt32("IdentityInstance")
                },
                HeaderUnknown = reader.ReadByte("HeaderUnknown")
            };

            int count = reader.ReadInt32("StatCount");
            if (count < 0 || count > MaximumStatCount)
            {
                throw new InvalidDataException("Stat count is outside the supported fail-closed range: " + count + ".");
            }

            if (reader.Remaining < count * 8)
            {
                throw new EndOfStreamException("Stat packet ends before all declared stat/value pairs.");
            }

            var values = new List<RawStatValue>(count);
            for (int index = 0; index < count; index++)
            {
                values.Add(
                    new RawStatValue
                    {
                        StatId = reader.ReadInt32("StatId"),
                        Value = reader.ReadUInt32("StatValue")
                    });
            }

            result.Stats = values.ToArray();
            result.BytesConsumed = reader.Position;
            result.UndecodedTail = reader.ReadRemaining();
            result.DecodeFullyConsumed = result.UndecodedTail.Length == 0;
            return result;
        }

        private static int ReadInt32BigEndian(byte[] bytes, int offset)
        {
            return unchecked(
                (int)(((uint)bytes[offset] << 24)
                      | ((uint)bytes[offset + 1] << 16)
                      | ((uint)bytes[offset + 2] << 8)
                      | bytes[offset + 3]));
        }

        private sealed class RawStatBigEndianReader
        {
            private readonly byte[] bytes;
            private int position;

            internal RawStatBigEndianReader(byte[] bytes)
            {
                this.bytes = bytes ?? new byte[0];
            }

            internal int Position { get { return this.position; } }
            internal int Remaining { get { return this.bytes.Length - this.position; } }

            internal byte ReadByte(string field)
            {
                this.EnsureRemaining(1, field);
                return this.bytes[this.position++];
            }

            internal int ReadInt32(string field)
            {
                return unchecked((int)this.ReadUInt32(field));
            }

            internal uint ReadUInt32(string field)
            {
                this.EnsureRemaining(4, field);
                uint value = ((uint)this.bytes[this.position] << 24)
                             | ((uint)this.bytes[this.position + 1] << 16)
                             | ((uint)this.bytes[this.position + 2] << 8)
                             | this.bytes[this.position + 3];
                this.position += 4;
                return value;
            }

            internal byte[] ReadRemaining()
            {
                var value = new byte[this.Remaining];
                Buffer.BlockCopy(this.bytes, this.position, value, 0, value.Length);
                this.position = this.bytes.Length;
                return value;
            }

            private void EnsureRemaining(int count, string field)
            {
                if (count < 0 || this.Remaining < count)
                {
                    throw new EndOfStreamException(field + " exceeds the retained Stat packet.");
                }
            }
        }
    }

    internal sealed class RawStatMessage
    {
        internal int N3MessageType { get; set; }
        internal RawScfuIdentity Identity { get; set; }
        internal byte HeaderUnknown { get; set; }
        internal RawStatValue[] Stats { get; set; }
        internal int BytesConsumed { get; set; }
        internal bool DecodeFullyConsumed { get; set; }
        internal byte[] UndecodedTail { get; set; }
        internal byte[] RawPacket { get; set; }
        internal byte[] RawBody { get; set; }
    }

    internal sealed class RawStatValue
    {
        internal int StatId { get; set; }
        internal uint Value { get; set; }
    }

    internal static class RawStatObservationCsv
    {
        internal const string Header =
            "CapturedUtc,ElapsedMilliseconds,Direction,GlobalOrdinal,Sequence,PacketLength,DecodeStatus,DecodeError,Identity,HeaderUnknown,StatOrdinal,StatId,Value,BytesConsumed,DecodeFullyConsumed,UndecodedTailHex,RawPacketHex";

        internal static IEnumerable<string> FormatRows(
            RawScfuCaptureMetadata metadata,
            byte[] packet,
            RawStatMessage message,
            string decodeError)
        {
            string status = message == null
                                ? "decode_failed"
                                : message.DecodeFullyConsumed ? "decoded_complete" : "decoded_incomplete";
            RawStatValue[] stats = message == null || message.Stats == null
                                       ? new RawStatValue[0]
                                       : message.Stats;
            int rows = Math.Max(1, stats.Length);
            for (int index = 0; index < rows; index++)
            {
                RawStatValue stat = index < stats.Length ? stats[index] : null;
                yield return string.Join(
                    ",",
                    Csv(metadata == null ? string.Empty : metadata.CapturedUtc),
                    Csv(metadata == null ? string.Empty : metadata.ElapsedMilliseconds),
                    Csv(metadata == null ? string.Empty : metadata.Direction),
                    Csv(metadata == null ? string.Empty : metadata.GlobalOrdinal),
                    Csv(metadata == null ? string.Empty : metadata.Sequence),
                    Csv(packet == null ? string.Empty : packet.Length.ToString(CultureInfo.InvariantCulture)),
                    Csv(status),
                    Csv(decodeError),
                    Csv(message == null ? string.Empty : message.Identity.ToString()),
                    Csv(message == null ? string.Empty : message.HeaderUnknown.ToString(CultureInfo.InvariantCulture)),
                    Csv(stat == null ? string.Empty : index.ToString(CultureInfo.InvariantCulture)),
                    Csv(stat == null ? string.Empty : stat.StatId.ToString(CultureInfo.InvariantCulture)),
                    Csv(stat == null ? string.Empty : stat.Value.ToString(CultureInfo.InvariantCulture)),
                    Csv(message == null ? string.Empty : message.BytesConsumed.ToString(CultureInfo.InvariantCulture)),
                    Csv(message == null ? string.Empty : message.DecodeFullyConsumed ? "true" : "false"),
                    Csv(message == null ? string.Empty : RawScfuFormatting.ToHex(message.UndecodedTail)),
                    Csv(RawScfuFormatting.ToHex(packet)));
            }
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }
    }
}
