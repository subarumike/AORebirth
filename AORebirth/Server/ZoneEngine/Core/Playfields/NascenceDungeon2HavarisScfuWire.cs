namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Net;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;

    using ZoneEngine.Core.Controllers;

    /// <summary>
    /// Capture 20260824-220326 SCFU SimpleChar:7A2B9829 (325B body).
    /// Side=Monster, Breed=Monster, Appearance=1227, ExtTex self-illumination + CF1B, IsPet flags.
    /// Chat taunts on trash confirm Monster-side hostility; Havaris shares that SCFU Side.
    /// </summary>
    internal static class NascenceDungeon2HavarisScfuWire
    {
        // Same framing as PetBureaucratGuardianScfuWire: leading seq word + N3 body.
        // Capture 220326 RawPacketHex seq was 1632; outbound compressor replaces seq.
        private const int HeaderSenderOffset = 8;
        private const int HeaderReceiverOffset = 12;
        private const int LengthOffset = 6;
        private const int N3IdentityInstanceOffset = 24;
        private const int ScfuPlayfieldOffset = 34;
        private const int ScfuCoordOffset = 38;
        private const int ScfuHealthOffset = 93;

        // Capture RawPacketHex (seq placeholder 0000 + body). Length field 0x0149 includes seq word.
        private static readonly byte[] ScfuTemplate = Hex(
            "0000000A0001014900000DAF7A1ADE69271B3A6B0000C3507A2B9829003A0A2A4A53002080EE42FAE930428007AE432DF9D00000000000000000000000003F800000000004CB0848617661726973001008120100000000AF000000002D33F40000033F6E007D001F000000001C000000000000000000000000030100010001000100010000000200009A00000BD373656C6620696C6C756D696E6174696F6E20626F6479200000000000000000000003380A00000000000000015B325D206F706163697479206D617073000000000000000000000000000000000003380A0000000000000000000007E20000CF1B0003AA2800000000006B94570068A3B4000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000003F10000000000");

        internal static bool IsHavaris(Character character)
        {
            return character != null
                   && string.Equals(character.Name, "Havaris", StringComparison.OrdinalIgnoreCase)
                   && character.Playfield != null
                   && NascenceDungeon2Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance);
        }

        internal static void SendToRecipient(ZoneClient recipientClient, Character havaris)
        {
            if (recipientClient?.Controller?.Character == null || havaris == null)
            {
                return;
            }

            int recipientInstance = recipientClient.Controller.Character.Identity.Instance;
            byte[] packet = BuildPacket(havaris, recipientInstance);
            if (packet == null)
            {
                return;
            }

            recipientClient.EnqueueOutboundCompressedBuffer(packet);
        }

        internal static byte[] BuildPacket(Character havaris, int recipientInstance)
        {
            if (havaris == null || havaris.Playfield == null)
            {
                return null;
            }

            byte[] packet = (byte[])ScfuTemplate.Clone();
            Coordinate coord = havaris.CalculatePredictedPosition();
            int health = havaris.Stats[StatIds.health].Value;
            if (health < 0)
            {
                health = 0;
            }
            else if (health > ushort.MaxValue)
            {
                health = ushort.MaxValue;
            }

            PatchHeader(packet, recipientInstance);
            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, havaris.Identity.Instance);
            WriteInt32BigEndian(packet, ScfuPlayfieldOffset, havaris.Playfield.Identity.Instance);
            WriteFloat(packet, ScfuCoordOffset, coord.x);
            WriteFloat(packet, ScfuCoordOffset + 4, coord.y);
            WriteFloat(packet, ScfuCoordOffset + 8, coord.z);
            WriteUInt16BigEndian(packet, ScfuHealthOffset, (ushort)health);

            return packet;
        }

        private static void PatchHeader(byte[] packet, int recipientInstance)
        {
            WriteInt32BigEndian(packet, HeaderSenderOffset, 0x00000DAF);
            WriteInt32BigEndian(packet, HeaderReceiverOffset, recipientInstance);
            ushort totalLength = (ushort)packet.Length;
            packet[LengthOffset] = (byte)(totalLength >> 8);
            packet[LengthOffset + 1] = (byte)totalLength;
        }

        private static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
        {
            byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
        }

        private static void WriteUInt16BigEndian(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte)value;
        }

        private static void WriteFloat(byte[] buffer, int offset, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
        }

        private static byte[] Hex(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
