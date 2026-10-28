namespace AORebirth.CaptureProtocol
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>Offline original FullUpdate reader. It never assigns semantics to neutral fields.</summary>
    internal static class RawTrapFullUpdateDecoder
    {
        internal const int MineKey = 0x215B5678;
        internal const int TrapItemKey = 0x59313928;

        internal static RawTrapFullUpdate DecodePacket(byte[] packet)
        {
            if (packet == null)
                throw new ArgumentNullException("packet");
            if (packet.Length < 29 || ((packet[6] << 8) | packet[7]) != packet.Length)
                throw new InvalidDataException("Trap frame length does not match the complete source packet.");

            var reader = new Reader(packet, 16);
            var result = new RawTrapFullUpdate { PacketKey = reader.Int32() };
            if (result.PacketKey != MineKey && result.PacketKey != TrapItemKey)
                throw new InvalidDataException("The source packet is neither MineFullUpdate nor TrapItemFullUpdate.");
            result.Identity = reader.Identity();
            result.PassOn = reader.Byte();
            if (result.PassOn > 1)
                throw new InvalidDataException("Original pass-on marker must be zero or one.");

            // Gamecode+A1661 common FullUpdate. Parented objects have no wire transform.
            result.CommonMarker = reader.Int32();
            if (result.CommonMarker != 11)
                throw new InvalidDataException("Common FullUpdate marker must equal eleven.");
            result.Parent = reader.Identity();
            if (result.Parent.Type == 0)
            {
                result.Coordinate = new[] { reader.Single(), reader.Single(), reader.Single() };
                result.Heading = new[] { reader.Single(), reader.Single(), reader.Single(), reader.Single() };
            }
            result.PlayfieldId = reader.Int32();
            result.NeutralIdentity = reader.Identity();
            result.HeaderStatBytes = new[] { reader.Byte(), reader.Byte() };
            result.EncodedStatCount = reader.Int32();
            if (result.EncodedStatCount < 1009 || result.EncodedStatCount % 1009 != 0)
                throw new InvalidDataException("Noncanonical original scaled stat count.");
            int count = result.EncodedStatCount / 1009 - 1;
            if (count > reader.Remaining / 8)
                throw new InvalidDataException("Declared trap stats exceed the remaining source bytes.");
            result.Stats = new List<RawTrapStat>();
            for (int index = 0; index < count; index++)
                result.Stats.Add(new RawTrapStat { StatId = reader.Int32(), Value = unchecked((uint)reader.Int32()) });
            int blobLength = reader.Int32();
            if (blobLength < 0 || blobLength > reader.Remaining)
                throw new InvalidDataException("Declared trap blob exceeds the remaining source bytes.");
            result.OpaqueData = reader.Bytes(blobLength);

            // Gamecode+A28CA: exact TrapItem tail, including all unclassified flag bits.
            result.TrapMarker = reader.Int32();
            if (result.TrapMarker != 2)
                throw new InvalidDataException("TrapItem marker must equal two.");
            result.Raw74 = reader.Int32();
            result.Identity78 = reader.Identity();
            result.Identity80 = reader.Identity();
            result.TrapFlags = reader.Byte();
            if (result.PacketKey == MineKey)
            {
                // Every retained Mine packet has this eight-byte extension. Its native
                // consumer meaning is not recovered; no gameplay meaning is inferred.
                result.MineMarker = reader.Int32();
                if (result.MineMarker != 1)
                    throw new InvalidDataException("Retained Mine extension marker must equal one.");
                result.UnknownMineValue = reader.Int32();
            }
            if (reader.Remaining != 0)
                throw new InvalidDataException("Unconsumed trap bytes: " + reader.Remaining + ".");
            result.DecodedBytes = packet.Length;
            return result;
        }

        private sealed class Reader
        {
            private readonly byte[] data;
            private int cursor;

            internal Reader(byte[] data, int offset)
            {
                this.data = data;
                this.cursor = offset;
            }

            internal int Remaining { get { return this.data.Length - this.cursor; } }

            internal byte Byte()
            {
                if (this.Remaining < 1)
                    throw new EndOfStreamException("Incomplete original trap field.");
                return this.data[this.cursor++];
            }

            internal byte[] Bytes(int count)
            {
                if (count < 0 || count > this.Remaining)
                    throw new EndOfStreamException("Incomplete original trap byte extent.");
                var value = new byte[count];
                Buffer.BlockCopy(this.data, this.cursor, value, 0, count);
                this.cursor += count;
                return value;
            }

            internal int Int32()
            {
                byte[] value = this.Bytes(4);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(value);
                return BitConverter.ToInt32(value, 0);
            }

            internal float Single()
            {
                byte[] value = this.Bytes(4);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(value);
                return BitConverter.ToSingle(value, 0);
            }

            internal RawTrapIdentity Identity()
            {
                return new RawTrapIdentity { Type = this.Int32(), Instance = this.Int32() };
            }
        }
    }

    internal sealed class RawTrapFullUpdate
    {
        public int PacketKey { get; set; }
        public RawTrapIdentity Identity { get; set; }
        public byte PassOn { get; set; }
        public int CommonMarker { get; set; }
        public RawTrapIdentity Parent { get; set; }
        public float[] Coordinate { get; set; }
        public float[] Heading { get; set; }
        public int PlayfieldId { get; set; }
        public RawTrapIdentity NeutralIdentity { get; set; }
        public byte[] HeaderStatBytes { get; set; }
        public int EncodedStatCount { get; set; }
        public List<RawTrapStat> Stats { get; set; }
        public byte[] OpaqueData { get; set; }
        public int TrapMarker { get; set; }
        public int Raw74 { get; set; }
        public RawTrapIdentity Identity78 { get; set; }
        public RawTrapIdentity Identity80 { get; set; }
        public byte TrapFlags { get; set; }
        public int? MineMarker { get; set; }
        public int? UnknownMineValue { get; set; }
        public int DecodedBytes { get; set; }
    }

    internal sealed class RawTrapIdentity
    {
        public int Type { get; set; }
        public int Instance { get; set; }
    }

    internal sealed class RawTrapStat
    {
        public int StatId { get; set; }
        public uint Value { get; set; }
    }
}
