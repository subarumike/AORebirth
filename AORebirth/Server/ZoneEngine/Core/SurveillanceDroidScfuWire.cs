namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Net;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture-backed Surveillance Droid SCFU wire (Arete Landing).
    /// </summary>
    internal static class SurveillanceDroidScfuWire
    {
        private const int ZoneServerSenderId = 854;

        private const int HeaderReceiverOffset = 12;

        private const int HeaderSenderOffset = 8;

        private const int N3IdentityInstanceOffset = 24;

        private const int ScfuPlayfieldOffset = 34;

        private const int ScfuCoordOffset = 38;

        private const int ScfuHeadingOffset = 50;

        private static readonly byte[] ScfuCapturePacket = Hex(
            "0000000A000101000000035600000000271B3A6B0000C35078E0FC8A003A0A2A4A53000FF02D455EF84A40A38520444D17E7800000003F145353800000003F50A6D9000005C8135375727665696C6C616E63652044726F69640010081201000000008900000000060045000003353E006E001F000000001C000000000000000000000000030100010001000100010000000300001400000FC463616D65726100000000000000000000000000000000000000000000000000000003351B000000000000000063616D65726120676C6F770000000000000000000000000000000000000000000003A8A6000000000000000063616D657261206C656E736500000000000000000000000000000000000000000003A8A80000000000000000000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000003F10000000000");

        public static bool IsSurveillanceDroid(Character character)
        {
            return character != null
                   && (string.Equals(character.Name, "Surveillance Droid", StringComparison.OrdinalIgnoreCase)
                       || character.Stats[StatIds.monsterdata].Value == 210238);
        }

        public static void SendToRecipient(ZoneClient recipientClient, Character droid)
        {
            if (recipientClient == null
                || recipientClient.Controller == null
                || recipientClient.Controller.Character == null
                || droid == null
                || droid.Playfield == null)
            {
                return;
            }

            int recipientInstance = recipientClient.Controller.Character.Identity.Instance;
            byte[] packet = BuildPacket(droid, recipientInstance);
            if (packet == null)
            {
                return;
            }

            recipientClient.Server.Info(
                recipientClient,
                "SurveillanceDroidWire SCFU recipient={0} droid={1} len={2}",
                recipientInstance,
                droid.Identity,
                packet.Length);
            recipientClient.EnqueueOutboundCompressedBuffer(packet);
        }

        internal static byte[] BuildPacket(Character droid, int recipientInstance)
        {
            if (droid == null || droid.Playfield == null)
            {
                return null;
            }

            byte[] packet = (byte[])ScfuCapturePacket.Clone();
            int droidInstance = droid.Identity.Instance;
            int playfieldId = droid.Playfield.Identity.Instance;
            Coordinate coord = droid.Coordinates();
            Quaternion heading = droid.Heading;

            PatchHeader(packet, recipientInstance);
            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, droidInstance);
            WriteInt32BigEndian(packet, ScfuPlayfieldOffset, playfieldId);
            WriteFloat(packet, ScfuCoordOffset, coord.x);
            WriteFloat(packet, ScfuCoordOffset + 4, coord.y);
            WriteFloat(packet, ScfuCoordOffset + 8, coord.z);
            WriteFloat(packet, ScfuHeadingOffset, heading.xf);
            WriteFloat(packet, ScfuHeadingOffset + 4, heading.yf);
            WriteFloat(packet, ScfuHeadingOffset + 8, heading.zf);
            WriteFloat(packet, ScfuHeadingOffset + 12, heading.wf);
            return packet;
        }

        private static void PatchHeader(byte[] packet, int recipientInstance)
        {
            WriteInt32BigEndian(packet, HeaderSenderOffset, ZoneServerSenderId);
            WriteInt32BigEndian(packet, HeaderReceiverOffset, recipientInstance);
            ushort totalLength = (ushort)packet.Length;
            packet[6] = (byte)(totalLength >> 8);
            packet[7] = (byte)totalLength;
        }

        private static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
        {
            byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
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
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
