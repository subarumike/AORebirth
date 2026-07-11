#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.IO;
    using System.Net;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Capture-backed SpellList builder for MP pet summons (20260710-185528).
    /// </summary>
    internal static class PetSummonSpellListBuilder
    {
        private const int OwnerIdentityOffset1 = 124;

        private const int OwnerIdentityOffset2 = 132;

        private const int OwnerNameLengthOffset = 140;

        private const int PetIdentityOffset = 56;

        private static readonly byte[] OwnerHealBodyTemplate = HexToBytes(
            "07E20000CFAF0001EB32000000040000000200000000000002D00000005D00000000000000000000002A000000010000000000000001000000"
            + "094D543039000000C0FFFFFFFF00000000000000030000000000000000000000000001ADB1000000010000008300000080000000000000035100000351000000000000"
            + "C35035FE28680000C35035FE286800001443616C6C696E67206F662042656C616D6F727465000000000000");

        private static readonly byte[] OwnerAttackBodyTemplate = HexToBytes(
            "07E20000CFAF0000AAD90000000400000005000000830000034E00000002000000820000034E00000002000000000000000000000004000000FB000000010000004200000000000000000000"
            + "00040000000100000000000000010000000950543536000000BFFFFFFFFF0000000000000002000000000000000000000000000186A1000000010000008300000082000000000000034E0000034E000000000000"
            + "C35035FE28680000C35035FE286800000C53756D6D6F6E2044656D6F6E000000000000");

        private static readonly byte[] PetHealSpellListBodyTemplateA = HexToBytes(
            "07E20000CF221860BA6600000004000000000000000100000000000000000000000000000000000002B10000009600000000000000000000C350795625EA000000000000000000");

        private static readonly byte[] PetHealSpellListBodyTemplateB = HexToBytes(
            "07E20000CF221860BA6700000004000000000000000100000000000000000000000000000000000002170000007D00000000000000000000C350795625EA000000000000000000");

        public static byte[] BuildOwnerPayload(
            Identity owner,
            int nanoId,
            string petHash,
            int petTypeId,
            int spellListSlot,
            string nanoName)
        {
            byte[] body = spellListSlot == PetSlotClassifier.HealingSpellListSlot
                ? (byte[])OwnerHealBodyTemplate.Clone()
                : (byte[])OwnerAttackBodyTemplate.Clone();

            PatchNanoId(body, nanoId);
            PatchSpellListSlot(body, spellListSlot);
            PatchIdentity(body, OwnerIdentityOffset1, owner);
            PatchIdentity(body, OwnerIdentityOffset2, owner);
            PatchName(body, OwnerNameLengthOffset, nanoName ?? string.Empty);
            return body;
        }

        public static byte[][] BuildPetPayloads(Identity petIdentity)
        {
            return new[]
            {
                BuildPetPayload(PetHealSpellListBodyTemplateA, petIdentity),
                BuildPetPayload(PetHealSpellListBodyTemplateB, petIdentity),
            };
        }

        private static byte[] BuildPetPayload(byte[] template, Identity petIdentity)
        {
            byte[] body = (byte[])template.Clone();
            PatchIdentity(body, PetIdentityOffset, petIdentity);
            return body;
        }

        private static void PatchNanoId(byte[] body, int nanoId)
        {
            if (nanoId <= 0xFFFF)
            {
                WriteUInt16BigEndian(body, 6, 0);
                WriteUInt16BigEndian(body, 8, (ushort)nanoId);
                WriteUInt16BigEndian(body, 10, 0);
            }
            else
            {
                WriteInt32BigEndian(body, 6, nanoId);
            }
        }

        private static void PatchSpellListSlot(byte[] body, int spellListSlot)
        {
            WriteInt32BigEndian(body, 14, spellListSlot);
        }

        private static void PatchIdentity(byte[] body, int offset, Identity identity)
        {
            WriteUInt16BigEndian(body, offset, (ushort)identity.Type);
            WriteInt32LittleEndian(body, offset + 2, identity.Instance);
            WriteUInt16BigEndian(body, offset + 6, 0);
        }

        private static void PatchName(byte[] body, int lengthOffset, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            if (bytes.Length > byte.MaxValue)
            {
                throw new InvalidOperationException("Pet summon SpellList nano name is too long.");
            }

            body[lengthOffset] = (byte)bytes.Length;
            int stringOffset = lengthOffset + 1;
            Array.Clear(body, stringOffset, body.Length - stringOffset);
            Buffer.BlockCopy(bytes, 0, body, stringOffset, bytes.Length);
        }

        private static void WriteUInt16BigEndian(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte)value;
        }

        private static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
        {
            byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
        }

        private static void WriteInt32LittleEndian(byte[] buffer, int offset, int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
        }

        private static byte[] HexToBytes(string hex)
        {
            if (hex.Length % 2 != 0)
            {
                throw new InvalidOperationException("Invalid hex template length.");
            }

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
