#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Net;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;

    using ZoneEngine.Core.Controllers;

    #endregion

    /// <summary>
    /// Capture-backed guardian SimpleCharFullUpdate wire (20260713-153757 CEO Guardian).
    /// Live AO sends HasExtendedTextures on the initial spawn SCFU; serializer-built metadata
    /// breaks AORebirth pet linking, so guardians replay the captured packet body instead.
    /// </summary>
    internal static class PetBureaucratGuardianScfuWire
    {
        private const int CorporateGuardianNanoId = 235386;
        private const int CeoGuardianNanoId = 273300;

        private const int ZoneServerSenderId = 0x356;
        private const int HeaderReceiverOffset = 12;
        private const int HeaderSenderOffset = 8;
        private const int N3IdentityInstanceOffset = 24;
        private const int ScfuPlayfieldOffset = 34;
        private const int ScfuCoordOffset = 38;

        // Capture 20260713-142159 (Corporate Guardian). Mesh patched to 273304 to match CEO.
        private static readonly byte[] ScfuCorporateGuardian = Hex(
            "00B6000A0001017600000DB3762ABC21271B3A6B0000C35079623667003A0A0A6A530015902D4345E2AE40A051EC42FC602D0000000000000000000000003F800000000005E813436F72706F7261746520477561726469616E0010081201000000005F000A0000CD726800000379750082001F000000001C00000000000000000000000003010001000100010001000000020000038D00000FC468656C6C6661636532000000000000000000000000000000000000000000000000036C55000000000000000168656C6C3200000000000000000000000000000000000000000000000000000000036C56000000000000000068656C6C3100000000000000000000000000000000000000000000000000000000036C560000000000000000000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000007E20100042B980000000002000000020000");

        // Capture 20260713-153757 (CEO Guardian).
        private static readonly byte[] ScfuCeoGuardian = Hex(
            "00C3000A0001017000000DB1762ABC21271B3A6B0000C3507962B1E3003A0A0A6A530015781E4344C80E40A051EC42FAE48D0000000000000000000000003F800000000005E80D43454F20477561726469616E0010081201000000005F000A0000D786D10000037975007D001F000000001C00000000000000000000000003010001000100010001000000020000042600000FC468656C6C6661636532000000000000000000000000000000000000000000000000036C55000000000000000168656C6C3200000000000000000000000000000000000000000000000000000000036C56000000000000000068656C6C3100000000000000000000000000000000000000000000000000000000036C560000000000000000000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000007E20100042B980000000002000000020000");

        public static void SendToOwner(
            ZoneClient ownerClient,
            ICharacter owner,
            Character petCharacter,
            int summonNanoId)
        {
            if (ownerClient == null)
            {
                return;
            }

            SendToRecipient(ownerClient, owner, petCharacter, summonNanoId);
        }

        public static void SendToOtherPlayers(
            ICharacter owner,
            Character petCharacter,
            int summonNanoId)
        {
            if (owner == null || petCharacter == null || owner.Playfield == null)
            {
                return;
            }

            Playfield playfield = owner.Playfield as Playfield;
            if (playfield == null)
            {
                return;
            }

            foreach (ICharacter character in playfield.EnumerateActiveCharacters())
            {
                if (character == null
                    || character.Identity == owner.Identity
                    || !(character.Controller is PlayerController))
                {
                    continue;
                }

                ZoneClient recipientClient = character.Controller.Client as ZoneClient;
                if (recipientClient == null)
                {
                    continue;
                }

                SendToRecipient(recipientClient, owner, petCharacter, summonNanoId);
            }
        }

        public static void SendToRecipient(
            ZoneClient recipientClient,
            ICharacter owner,
            Character petCharacter,
            int summonNanoId)
        {
            if (recipientClient?.Controller?.Character == null)
            {
                return;
            }

            int recipientInstance = recipientClient.Controller.Character.Identity.Instance;
            byte[] packet = BuildPacket(owner, petCharacter, summonNanoId, recipientInstance);
            if (packet == null)
            {
                return;
            }

            recipientClient.Server.Info(
                recipientClient,
                "SummonWireSend GuardianSCFU recipient={0} pet={1} len={2} nano={3}",
                recipientInstance,
                petCharacter.Identity,
                packet.Length,
                summonNanoId);
            recipientClient.EnqueueOutboundCompressedBuffer(packet);
        }

        internal static byte[] BuildPacket(
            ICharacter owner,
            Character petCharacter,
            int summonNanoId,
            int recipientInstance)
        {
            if (owner == null || petCharacter == null || owner.Playfield == null)
            {
                return null;
            }

            byte[] template = summonNanoId == CeoGuardianNanoId
                ? ScfuCeoGuardian
                : summonNanoId == CorporateGuardianNanoId
                    ? ScfuCorporateGuardian
                    : null;
            if (template == null)
            {
                return null;
            }

            byte[] packet = (byte[])template.Clone();
            int petInstance = petCharacter.Identity.Instance;
            int playfieldId = owner.Playfield.Identity.Instance;
            Coordinate petCoord = petCharacter.CalculatePredictedPosition();

            PatchHeader(packet, recipientInstance);
            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, petInstance);
            WriteInt32BigEndian(packet, ScfuPlayfieldOffset, playfieldId);
            WriteFloat(packet, ScfuCoordOffset, petCoord.x);
            WriteFloat(packet, ScfuCoordOffset + 4, petCoord.y);
            WriteFloat(packet, ScfuCoordOffset + 8, petCoord.z);

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
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
