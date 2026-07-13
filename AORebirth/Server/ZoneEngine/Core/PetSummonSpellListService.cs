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

    using System.Threading;



    using AORebirth.Core.Entities;



    using SmokeLounge.AOtomation.Messaging.GameData;



    #endregion



    /// <summary>

    /// Sends capture-backed SpellList wire packets with patched identities (20260711-013417).

    /// </summary>

    public static class PetSummonSpellListService

    {

        private const int ZoneServerSenderId = 0x356;



        private const int HeaderReceiverOffset = 12;



        private const int N3IdentityInstanceOffset = 24;



        private const int BodyStartOffset = 31;



        private const int OwnerBodyIdentityOffset1 = 124;



        private const int OwnerBodyIdentityOffset2 = 132;



        private const int PetBodyIdentityOffset = 56;



        private static readonly byte[] OwnerHealCaptureWire = HexToBytes(

            "004D000A000100C600000DBC35FE28684D4501140000C35035FE286800000007E20000CFAF0001EB32000000040000000200000000000002D00000005D00000000000000000000002A000000010000000000000001000000094D543039000000C0FFFFFFFF00000000000000030000000000000000000000000001ADB1000000010000008300000080000000000000035100000351000000000000C35035FE28680000C35035FE286800001443616C6C696E67206F662042656C616D6F727465000000000000");

        private const int OwnerHealCaptureHeaderLength = 31;

        private static readonly byte[] OwnerHealCaptureHeader = CopyHeader(OwnerHealCaptureWire, OwnerHealCaptureHeaderLength);

        private static readonly byte[] OwnerHealTierCaptureHeader = HexToBytes(
            "004D000A000100C600000DBD35FE28684D4501140000C35035FE2868000000");



        private static readonly byte[] OwnerAttackCaptureWire = HexToBytes(

            "0070000A000100E200000DBC35FE28684D4501140000C35035FE286800000007E20000CFAF0000AAD90000000400000005000000830000034E00000002000000820000034E00000002000000000000000000000004000000FB00000001000000420000000000000000000000040000000100000000000000010000000950543536000000BFFFFFFFFF0000000000000002000000000000000000000000000186A1000000010000008300000082000000000000034E0000034E000000000000C35035FE28680000C35035FE286800000C53756D6D6F6E2044656D6F6E000000000000");



        private static readonly byte[] PetHealCaptureWireA = HexToBytes(

            "005A000A0001006600000DBC35FE28684D4501140000C350795625EA00000007E20000CF2218620D8500000004000000000000000100000000000000000000000000000000000002B10000009600000000000000000000C350795625EA000000000000000000");



        private static readonly byte[] PetHealCaptureWireB = HexToBytes(

            "005B000A0001006600000DBC35FE28684D4501140000C350795625EA00000007E20000CF2218620D8600000004000000000000000100000000000000000000000000000000000002170000007D00000000000000000000C350795625EA000000000000000000");

        // Capture 20260713-142159 (Corporate Guardian shell summon pet SpellList).
        private static readonly byte[] BureaucratAttackPetSpellListWire = HexToBytes(
            "00C6000A0001006600000DB3762ABC214D4501140000C3507962366700000007E20000CF221864840D00000004000000000000000100000000000000000000000000000000000002B10000009600000000000000000000C35079623667000000000000000000");

        private const int BureaucratSpellListSubIdOffset = 40;

        private static int bureaucratWorkerSpellListSeq = 0x81C0;

        private static int bureaucratGuardianSpellListSeq = 0x840C;



        public static void SendOwnerPetSummon(

            ICharacter owner,

            int nanoId,

            string petHash,

            int petTypeId,

            int petSlotStrain)

        {

            Character ownerCharacter = owner as Character;

            if (ownerCharacter == null

                || ownerCharacter.Controller == null

                || ownerCharacter.Controller.Client == null

                || nanoId <= 0)

            {

                return;

            }

            byte[] captureWire;
            if (petSlotStrain == PetSlotClassifier.HealingPetStrain && nanoId != 125746)
            {
                byte[] body = PetSummonSpellListBuilder.BuildOwnerPayload(
                    ownerCharacter.Identity,
                    nanoId,
                    petHash,
                    petTypeId,
                    PetSlotClassifier.HealingSpellListSlot,
                    PetSummonNanoCatalog.GetSummonNanoDisplayName(nanoId));
                captureWire = CombineHeaderAndBody(OwnerHealTierCaptureHeader, body);
            }
            else
            {
                captureWire = petSlotStrain == PetSlotClassifier.HealingPetStrain
                    ? OwnerHealCaptureWire
                    : OwnerAttackCaptureWire;
            }



            SendPatchedCaptureWire(

                ownerCharacter,

                captureWire,

                owner.Identity,

                owner.Identity.Instance,

                true);

        }



        public static void SendPetSummonSpellLists(
            ICharacter owner,
            Identity petIdentity,
            int petSlotStrain,
            string petHash = null)
        {
            Character ownerCharacter = owner as Character;
            if (ownerCharacter == null
                || ownerCharacter.Controller == null
                || ownerCharacter.Controller.Client == null
                || petIdentity.Type == IdentityType.None)
            {
                return;
            }

            if (petSlotStrain != PetSlotClassifier.HealingPetStrain)
            {
                return;
            }

            byte[] wireA;
            byte[] wireB;
            if (PetHealingPetScfuCatalog.TryGetPetSpellListWires(petHash, out wireA, out wireB))
            {
                if (wireA != null)
                {
                    SendPatchedCaptureWire(
                        ownerCharacter,
                        wireA,
                        petIdentity,
                        ownerCharacter.Identity.Instance,
                        false);
                }

                if (wireB != null)
                {
                    SendPatchedCaptureWire(
                        ownerCharacter,
                        wireB,
                        petIdentity,
                        ownerCharacter.Identity.Instance,
                        false);
                }

                return;
            }

            SendPatchedCaptureWire(
                ownerCharacter,
                PetHealCaptureWireA,
                petIdentity,
                ownerCharacter.Identity.Instance,
                false);
            SendPatchedCaptureWire(
                ownerCharacter,
                PetHealCaptureWireB,
                petIdentity,
                ownerCharacter.Identity.Instance,
                false);
        }



        private static void SendPatchedCaptureWire(

            Character ownerCharacter,

            byte[] captureWire,

            Identity messageIdentity,

            int receiverInstance,

            bool patchOwnerBodyIdentities)

        {

            byte[] packet = (byte[])captureWire.Clone();

            WriteInt32BigEndian(packet, HeaderReceiverOffset, receiverInstance);

            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, messageIdentity.Instance);



            if (patchOwnerBodyIdentities)

            {

                PatchBodyIdentity(packet, BodyStartOffset + OwnerBodyIdentityOffset1, messageIdentity);

                PatchBodyIdentity(packet, BodyStartOffset + OwnerBodyIdentityOffset2, messageIdentity);

            }

            else

            {

                PatchBodyIdentity(packet, BodyStartOffset + PetBodyIdentityOffset, messageIdentity);

            }



            WriteInt32BigEndian(packet, 8, ZoneServerSenderId);

            ushort totalLength = (ushort)packet.Length;

            packet[6] = (byte)(totalLength >> 8);

            packet[7] = (byte)totalLength;



            var zoneClient = ownerCharacter.Controller.Client as ZoneClient;

            if (zoneClient == null)

            {

                return;

            }



            zoneClient.Server.Info(
                zoneClient,
                "SpellListSend identity={0} len={1} mode=capture-wire",
                messageIdentity,
                packet.Length);
            zoneClient.EnqueueOutboundCompressedBuffer(packet);

        }



        private static int AllocateBureaucratSpellListSubId(string petHash)
        {
            ushort lowId;
            if (string.Equals(petHash, "A141", StringComparison.OrdinalIgnoreCase)
                || string.Equals(petHash, "BCBG", StringComparison.OrdinalIgnoreCase))
            {
                lowId = (ushort)Interlocked.Increment(ref bureaucratGuardianSpellListSeq);
            }
            else
            {
                lowId = (ushort)Interlocked.Increment(ref bureaucratWorkerSpellListSeq);
            }

            return unchecked((int)(0x18640000u | lowId));
        }

        private static void PatchBodyIdentity(byte[] packet, int offset, Identity identity)
        {
            WriteUInt16BigEndian(packet, offset, (ushort)identity.Type);
            WriteInt32LittleEndian(packet, offset + 2, identity.Instance);
            WriteUInt16BigEndian(packet, offset + 6, 0);
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



        private static byte[] CopyHeader(byte[] captureWire, int headerLength)
        {
            var header = new byte[headerLength];
            Buffer.BlockCopy(captureWire, 0, header, 0, headerLength);
            return header;
        }

        private static byte[] CombineHeaderAndBody(byte[] header, byte[] body)
        {
            var packet = new byte[header.Length + body.Length];
            Buffer.BlockCopy(header, 0, packet, 0, header.Length);
            Buffer.BlockCopy(body, 0, packet, header.Length, body.Length);
            ushort totalLength = (ushort)packet.Length;
            packet[6] = (byte)(totalLength >> 8);
            packet[7] = (byte)totalLength;
            return packet;
        }

        private static byte[] HexToBytes(string hex)

        {

            if (hex.Length % 2 != 0)

            {

                throw new InvalidOperationException("Invalid capture hex length.");

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


