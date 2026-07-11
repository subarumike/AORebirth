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

    using AORebirth.Core.Vector;



    using SmokeLounge.AOtomation.Messaging.GameData;



    #endregion



    /// <summary>

    /// Replays capture-backed Belamorte summon wire packets (20260711-013417) in exact order.

    /// </summary>

    internal static class PetSummonCaptureWireReplayer

    {

        private const int ZoneServerSenderId = 0x356;



        private const int HeaderReceiverOffset = 12;



        private const int HeaderSenderOffset = 8;



        private const int N3IdentityInstanceOffset = 24;



        private const int AddPetUnknownOffset = 28;

        private const int AddPetPetIdentityOffset = 31;

        private const int StatValueOffset = 36;



        private const int ScfuPlayfieldOffset = 34;

        private const int ScfuCoordOffset = 38;



        private const int SetPosCoordOffset = 31;



        private const int SpellListBodyStartOffset = 31;



        private const int OwnerSpellListBodyIdentityOffset1 = 124;



        private const int OwnerSpellListBodyIdentityOffset2 = 132;



        private const int PetSpellListBodyIdentityOffset = 56;



        private static readonly byte[] ScfuBelamorte = Hex(

            "00C7000A0001010B00000DAD35FE2868271B3A6B0000C3507957F058003A0A2A6A530015300B4372FE6C40D2C26C42D2494A0000000000000000000000003F800000000005E80A42656C616D6F72746500100812010000000060000B0000C0265700000177C10078001F000000001C0000000000000000000000000301000100010001000100000002000002E1000007E26D6574617065745F6865616C696E670000000000000000000000000000000000000467DA0000000000000001000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000003F1000000020000");



        private static readonly byte[] StatPetmaster = Hex(

            "00C8000A0001002900000DAD35FE28682B333D6E0000C3507957F0580000000001000000C435FE2868");



        private static readonly byte[] AddPet = Hex(

            "00C9000A0001002500000DAD35FE2868194E4F760000C35035FE2868010000C3507957F058");



        private static readonly byte[] StatFlags = Hex(

            "00CA000A0001002900000DAD35FE28682B333D6E0000C3507957F05800000000010000000018081201");



        private static readonly byte[] SpellListOwner = Hex(

            "00CB000A000100C600000DAD35FE28684D4501140000C35035FE286800000007E20000CFAF0001EB32000000040000000200000000000002D00000005D00000000000000000000002A000000010000000000000001000000094D543039000000C0FFFFFFFF00000000000000030000000000000000000000000001ADB1000000010000008300000080000000000000035100000351000000000000C35035FE28680000C35035FE286800001443616C6C696E67206F662042656C616D6F727465000000000000");



        private static readonly byte[] StatPetState = Hex(

            "00CD000A0001002900000DAD35FE28682B333D6E0000C3507957F05800000000010000000500232801");



        private static readonly byte[] StatSide = Hex(

            "00CE000A0001002900000DAD35FE28682B333D6E0000C3507957F05800000000010000002100000002");



        private static readonly byte[] StatBattleStationSide = Hex(

            "00CF000A0001002900000DAD35FE28682B333D6E0000C3507957F05800000000010000029C00000000");



        private static readonly byte[] StatRunSpeed = Hex(

            "00D0000A0001002900000DAD35FE28682B333D6E0000C3507957F05800000000010000009C0000054F");



        private static readonly byte[] StatExpansion = Hex(

            "00D1000A0001002900000DAD35FE28682B333D6E0000C3507957F05800000000010000018500000001");



        private static readonly byte[] StatUnknownA = Hex(

            "00D2000A0001002900000DAD35FE28682B333D6E0000C3507957F0580000000001000004A500000000");



        private static readonly byte[] StatUnknownB = Hex(

            "00D3000A0001002900000DAD35FE28682B333D6E0000C3507957F0580000000001000004A000000000");



        private static readonly byte[] SetWantedDirection = Hex(

            "00D4000A0001002900000DAD35FE286860201D0E0000C3507957F05800BF8000000000000000000000");



        private static readonly byte[] SpellListPetA = Hex(

            "00D5000A0001006600000DAD35FE28684D4501140000C3507957F05800000007E20000CF2218620D8500000004000000000000000100000000000000000000000000000000000002B10000009600000000000000000000C3507957F058000000000000000000");



        private static readonly byte[] SpellListPetB = Hex(

            "00D6000A0001006600000DAD35FE28684D4501140000C3507957F05800000007E20000CF2218620D8600000004000000000000000100000000000000000000000000000000000002170000007D00000000000000000000C3507957F058000000000000000000");



        private static readonly byte[] SetPos = Hex(

            "005C000A0001002F00000DBC35FE2868195E496E0000C350795625EA00436830DA40C0A3D8432383BB010000000000");



        public static void EnqueueHealingPetSummonLink(
            ZoneClient ownerClient,
            ICharacter owner,
            Character petCharacter,
            uint mobFlags)
        {
            if (ownerClient == null || owner == null || petCharacter == null)
            {
                return;
            }

            int ownerInstance = owner.Identity.Instance;
            int petInstance = petCharacter.Identity.Instance;

            EnqueueStatPetmaster(ownerClient, ownerInstance, petInstance);
            EnqueueAddPet(ownerClient, ownerInstance, petInstance);
            EnqueueStatFlags(ownerClient, petInstance, mobFlags);
        }

        public static void EnqueueHealingPetSummonPostStats(ZoneClient ownerClient, Character petCharacter)
        {
            if (ownerClient == null || petCharacter == null)
            {
                return;
            }

            int petInstance = petCharacter.Identity.Instance;

            EnqueueStatPetState(ownerClient, petInstance);
            EnqueueStatSide(ownerClient, petInstance);
            EnqueueStatTemplate(ownerClient, StatBattleStationSide, petInstance, "battlestationside");
            EnqueueStatTemplate(ownerClient, StatRunSpeed, petInstance, "runspeed");
            EnqueueStatTemplate(ownerClient, StatExpansion, petInstance, "expansion");
            EnqueueStatTemplate(ownerClient, StatUnknownA, petInstance, "statA");
            EnqueueStatTemplate(ownerClient, StatUnknownB, petInstance, "statB");
        }

        public static void SendBelamorteScfuToOwner(
            ZoneClient ownerClient,
            ICharacter owner,
            Character petCharacter)
        {
            SendHealingPetScfuToOwner(ownerClient, owner, petCharacter, "BSLX");
        }

        public static void SendHealingPetScfuToOwner(
            ZoneClient ownerClient,
            ICharacter owner,
            Character petCharacter,
            string petHash)
        {
            if (ownerClient == null || owner == null || petCharacter == null)
            {
                return;
            }

            int ownerInstance = owner.Identity.Instance;
            int petInstance = petCharacter.Identity.Instance;
            int playfieldId = owner.Playfield.Identity.Instance;
            Coordinate petCoord = petCharacter.Coordinates();

            EnqueueScfuForRebirth(
                ownerClient,
                ownerInstance,
                petInstance,
                playfieldId,
                petCoord,
                petHash);
        }

        public static void ReplayBelamorteSummonPostScfu(
            ZoneClient ownerClient,
            ICharacter owner,
            Character petCharacter,
            uint mobFlags)
        {
            if (ownerClient == null || owner == null || petCharacter == null)
            {
                return;
            }

            int ownerInstance = owner.Identity.Instance;
            int petInstance = petCharacter.Identity.Instance;

            EnqueueOwnerSpellList(ownerClient, ownerInstance);
            EnqueueStatPetState(ownerClient, petInstance);
            EnqueueStatSide(ownerClient, petInstance);
            EnqueueStatTemplate(ownerClient, StatBattleStationSide, petInstance, "battlestationside");
            EnqueueStatTemplate(ownerClient, StatRunSpeed, petInstance, "runspeed");
            EnqueueStatTemplate(ownerClient, StatExpansion, petInstance, "expansion");
            EnqueueStatTemplate(ownerClient, StatUnknownA, petInstance, "statA");
            EnqueueStatTemplate(ownerClient, StatUnknownB, petInstance, "statB");
            EnqueueSetWantedDirection(ownerClient, ownerInstance, petInstance);
            EnqueuePetSpellList(ownerClient, ownerInstance, petInstance, SpellListPetA);
            EnqueuePetSpellList(ownerClient, ownerInstance, petInstance, SpellListPetB);
        }

        public static void ReplayBelamorteSummonSafe(
            ZoneClient ownerClient,
            ICharacter owner,
            Character petCharacter,
            uint mobFlags)
        {
            if (ownerClient == null || owner == null || petCharacter == null)
            {
                return;
            }

            int ownerInstance = owner.Identity.Instance;
            int petInstance = petCharacter.Identity.Instance;
            Coordinate petCoord = petCharacter.Coordinates();

            EnqueueScfuMinimal(ownerClient, ownerInstance, petInstance);
            EnqueueStatPetmaster(ownerClient, ownerInstance, petInstance);
            EnqueueAddPet(ownerClient, ownerInstance, petInstance);
            EnqueueStatFlags(ownerClient, petInstance, mobFlags);
            EnqueueOwnerSpellList(ownerClient, ownerInstance);
            EnqueueStatPetState(ownerClient, petInstance);
            EnqueueStatSide(ownerClient, petInstance);
            EnqueueStatTemplate(ownerClient, StatBattleStationSide, petInstance, "battlestationside");
            EnqueueStatTemplate(ownerClient, StatRunSpeed, petInstance, "runspeed");
            EnqueueStatTemplate(ownerClient, StatExpansion, petInstance, "expansion");
            EnqueueStatTemplate(ownerClient, StatUnknownA, petInstance, "statA");
            EnqueueStatTemplate(ownerClient, StatUnknownB, petInstance, "statB");
            EnqueueSetWantedDirection(ownerClient, ownerInstance, petInstance);
            EnqueuePetSpellList(ownerClient, ownerInstance, petInstance, SpellListPetA);
            EnqueuePetSpellList(ownerClient, ownerInstance, petInstance, SpellListPetB);
        }

        public static void ReplayBelamorteSummonAfterScfu(
            ZoneClient ownerClient,
            ICharacter owner,
            Character petCharacter)
        {
            if (ownerClient == null || owner == null || petCharacter == null)
            {
                return;
            }

            int ownerInstance = owner.Identity.Instance;
            int petInstance = petCharacter.Identity.Instance;

            EnqueueOwnerSpellList(ownerClient, ownerInstance);
            EnqueueStatPetState(ownerClient, petInstance);
            EnqueueStatSide(ownerClient, petInstance);
            EnqueueStatTemplate(ownerClient, StatBattleStationSide, petInstance, "battlestationside");
            EnqueueStatTemplate(ownerClient, StatRunSpeed, petInstance, "runspeed");
            EnqueueStatTemplate(ownerClient, StatExpansion, petInstance, "expansion");
            EnqueueStatTemplate(ownerClient, StatUnknownA, petInstance, "statA");
            EnqueueStatTemplate(ownerClient, StatUnknownB, petInstance, "statB");
            EnqueueSetWantedDirection(ownerClient, ownerInstance, petInstance);
            EnqueuePetSpellList(ownerClient, ownerInstance, petInstance, SpellListPetA);
            EnqueuePetSpellList(ownerClient, ownerInstance, petInstance, SpellListPetB);
        }

        public static void ReplayBelamorteSummon(

            ZoneClient ownerClient,

            ICharacter owner,

            Character petCharacter,

            uint mobFlags)

        {

            if (ownerClient == null || owner == null || petCharacter == null)

            {

                return;

            }



            int ownerInstance = owner.Identity.Instance;

            int petInstance = petCharacter.Identity.Instance;

            int playfieldId = owner.Playfield.Identity.Instance;

            Coordinate petCoord = petCharacter.Coordinates();



            EnqueueScfu(ownerClient, ownerInstance, petInstance, playfieldId, petCoord);

            EnqueueStatPetmaster(ownerClient, ownerInstance, petInstance);

            EnqueueAddPet(ownerClient, ownerInstance, petInstance);

            EnqueueStatFlags(ownerClient, petInstance, mobFlags);

            EnqueueOwnerSpellList(ownerClient, ownerInstance);

            EnqueueStatPetState(ownerClient, petInstance);

            EnqueueStatSide(ownerClient, petInstance);

            EnqueueStatTemplate(ownerClient, StatBattleStationSide, petInstance, "battlestationside");

            EnqueueStatTemplate(ownerClient, StatRunSpeed, petInstance, "runspeed");

            EnqueueStatTemplate(ownerClient, StatExpansion, petInstance, "expansion");

            EnqueueStatTemplate(ownerClient, StatUnknownA, petInstance, "statA");

            EnqueueStatTemplate(ownerClient, StatUnknownB, petInstance, "statB");

            EnqueueSetWantedDirection(ownerClient, ownerInstance, petInstance);

            EnqueuePetSpellList(ownerClient, ownerInstance, petInstance, SpellListPetA);

            EnqueuePetSpellList(ownerClient, ownerInstance, petInstance, SpellListPetB);

        }



        private static void EnqueueScfuForRebirth(
            ZoneClient ownerClient,
            int ownerInstance,
            int petInstance,
            int playfieldId,
            Coordinate petCoord,
            string petHash)
        {
            byte[] template;
            if (!PetHealingPetScfuCatalog.TryGetScfuWire(petHash, out template))
            {
                template = (byte[])ScfuBelamorte.Clone();
            }

            byte[] packet = (byte[])template.Clone();
            PatchHeader(packet, ownerInstance);
            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, petInstance);
            WriteInt32BigEndian(packet, ScfuPlayfieldOffset, playfieldId);
            WriteFloat(packet, ScfuCoordOffset, petCoord.x);
            WriteFloat(packet, ScfuCoordOffset + 4, petCoord.y);
            WriteFloat(packet, ScfuCoordOffset + 8, petCoord.z);
            Enqueue(ownerClient, packet, "SCFU");
        }

        private static void EnqueueScfuMinimal(ZoneClient ownerClient, int ownerInstance, int petInstance)
        {
            byte[] packet = (byte[])ScfuBelamorte.Clone();
            PatchHeader(packet, ownerInstance);
            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, petInstance);
            Enqueue(ownerClient, packet, "SCFU");
        }

        private static void EnqueueScfu(

            ZoneClient ownerClient,

            int ownerInstance,

            int petInstance,

            int playfieldId,

            Coordinate petCoord)

        {

            byte[] packet = (byte[])ScfuBelamorte.Clone();

            PatchHeader(packet, ownerInstance);

            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, petInstance);

            WriteInt32BigEndian(packet, ScfuPlayfieldOffset, playfieldId);

            WriteFloat(packet, ScfuCoordOffset, petCoord.x);

            WriteFloat(packet, ScfuCoordOffset + 4, petCoord.y);

            WriteFloat(packet, ScfuCoordOffset + 8, petCoord.z);

            Enqueue(ownerClient, packet, "SCFU");

        }



        private static void EnqueueStatPetmaster(ZoneClient ownerClient, int ownerInstance, int petInstance)

        {

            byte[] packet = (byte[])StatPetmaster.Clone();

            PatchHeader(packet, ownerInstance);

            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, petInstance);

            WriteInt32BigEndian(packet, StatValueOffset, ownerInstance);

            Enqueue(ownerClient, packet, "Stat-petmaster");

        }



        private static void EnqueueAddPet(ZoneClient ownerClient, int ownerInstance, int petInstance)

        {

            byte[] packet = (byte[])AddPet.Clone();

            PatchHeader(packet, ownerInstance);

            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, ownerInstance);

            WriteUInt16LittleEndian(packet, AddPetUnknownOffset, 1);
            PatchCompactBodyIdentity(packet, AddPetPetIdentityOffset, petInstance);

            Enqueue(ownerClient, packet, "AddPet");

        }



        private static void EnqueueStatFlags(ZoneClient ownerClient, int petInstance, uint mobFlags)

        {

            int ownerInstance = ownerClient.Controller.Character.Identity.Instance;

            byte[] packet = (byte[])StatFlags.Clone();

            PatchHeader(packet, ownerInstance);

            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, petInstance);

            WriteInt32BigEndian(packet, StatValueOffset, (int)mobFlags);

            Enqueue(ownerClient, packet, "Stat-flags");

        }



        private static void EnqueueOwnerSpellList(ZoneClient ownerClient, int ownerInstance)

        {

            byte[] packet = (byte[])SpellListOwner.Clone();

            PatchHeader(packet, ownerInstance);

            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, ownerInstance);

            var ownerIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = ownerInstance };

            PatchBodyIdentity(packet, SpellListBodyStartOffset + OwnerSpellListBodyIdentityOffset1, ownerIdentity);

            PatchBodyIdentity(packet, SpellListBodyStartOffset + OwnerSpellListBodyIdentityOffset2, ownerIdentity);

            Enqueue(ownerClient, packet, "SpellList-owner");

        }



        private static void EnqueueStatPetState(ZoneClient ownerClient, int petInstance)

        {

            byte[] packet = (byte[])StatPetState.Clone();

            PatchHeader(packet, ownerClient.Controller.Character.Identity.Instance);

            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, petInstance);

            Enqueue(ownerClient, packet, "Stat-petstate");

        }



        private static void EnqueueStatSide(ZoneClient ownerClient, int petInstance)

        {

            byte[] packet = (byte[])StatSide.Clone();

            PatchHeader(packet, ownerClient.Controller.Character.Identity.Instance);

            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, petInstance);

            Enqueue(ownerClient, packet, "Stat-side");

        }



        private static void EnqueueStatTemplate(

            ZoneClient ownerClient,

            byte[] template,

            int petInstance,

            string label)

        {

            byte[] packet = (byte[])template.Clone();

            PatchHeader(packet, ownerClient.Controller.Character.Identity.Instance);

            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, petInstance);

            Enqueue(ownerClient, packet, "Stat-" + label);

        }



        public static void EnqueueSetWantedDirection(ZoneClient ownerClient, int ownerInstance, int petInstance)

        {

            byte[] packet = (byte[])SetWantedDirection.Clone();

            PatchHeader(packet, ownerInstance);

            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, petInstance);

            Enqueue(ownerClient, packet, "SetWantedDirection");

        }



        private static void EnqueuePetSpellList(

            ZoneClient ownerClient,

            int ownerInstance,

            int petInstance,

            byte[] template)

        {

            byte[] packet = (byte[])template.Clone();

            PatchHeader(packet, ownerInstance);

            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, petInstance);

            var petIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = petInstance };

            PatchBodyIdentity(packet, SpellListBodyStartOffset + PetSpellListBodyIdentityOffset, petIdentity);

            Enqueue(ownerClient, packet, "SpellList-pet");

        }



        private static void EnqueueSetPos(

            ZoneClient ownerClient,

            int ownerInstance,

            int petInstance,

            Coordinate petCoord)

        {

            byte[] packet = (byte[])SetPos.Clone();

            PatchHeader(packet, ownerInstance);

            WriteInt32BigEndian(packet, N3IdentityInstanceOffset, petInstance);

            WriteFloat(packet, SetPosCoordOffset, petCoord.x);

            WriteFloat(packet, SetPosCoordOffset + 4, petCoord.y);

            WriteFloat(packet, SetPosCoordOffset + 8, petCoord.z);

            Enqueue(ownerClient, packet, "SetPos");

        }



        private static void Enqueue(ZoneClient ownerClient, byte[] packet, string label)

        {

            ownerClient.Server.Info(

                ownerClient,

                "SummonWireSend {0} len={1}",

                label,

                packet.Length);

            ownerClient.EnqueueOutboundCompressedBuffer(packet);

        }



        private static void PatchHeader(byte[] packet, int ownerInstance)

        {

            WriteInt32BigEndian(packet, HeaderSenderOffset, ZoneServerSenderId);

            WriteInt32BigEndian(packet, HeaderReceiverOffset, ownerInstance);

            ushort totalLength = (ushort)packet.Length;

            packet[6] = (byte)(totalLength >> 8);

            packet[7] = (byte)totalLength;

        }



        private static void PatchCompactBodyIdentity(byte[] packet, int offset, int instance)
        {
            WriteUInt16BigEndian(packet, offset, (ushort)IdentityType.CanbeAffected);
            WriteInt32LittleEndian(packet, offset + 2, instance);
        }

        private static void WriteUInt16LittleEndian(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
        }

        private static void PatchBodyIdentity(byte[] packet, int offset, Identity identity)
        {
            WriteUInt16BigEndian(packet, offset, (ushort)identity.Type);
            WriteInt32LittleEndian(packet, offset + 2, identity.Instance);
            WriteUInt16BigEndian(packet, offset + 6, 0);
        }



        private static void WriteIdentityBigEndian(byte[] packet, int offset, IdentityType type, int instance)

        {

            WriteInt32BigEndian(packet, offset, (int)type);

            WriteInt32BigEndian(packet, offset + 4, instance);

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


