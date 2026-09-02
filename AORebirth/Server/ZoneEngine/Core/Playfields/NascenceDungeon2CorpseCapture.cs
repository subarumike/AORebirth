namespace AORebirth.Core.Playfields
{
    using System;
    using System.Net;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Capture 20260823-182854 CorpseFullUpdate for Nascence Dungeon 2 mobs.
    /// Bound Dryad / Shadows / Vortexoid etc. — not D1 spirit/rafter templates.
    /// </summary>
    internal static class NascenceDungeon2CorpseCapture
    {
        private const int ServerIdOffset = 8;
        private const int ReceiverInstanceOffset = 12;
        private const int CorpseInstanceOffset = 24;
        private const int PositionXOffset = 45;
        private const int PositionYOffset = 49;
        private const int PositionZOffset = 53;
        private const int PlayfieldIdOffset = 73;
        private const int MonsterScaleOffset = 143;
        private const int SexOffset = 159;
        private const int BreedOffset = 167;
        private const int RaceOffset = 175;
        private const int DeadNpcInstanceOffset = 191;
        private const int CorpseCatMeshOffset = 199;
        private const int CorpseCashValueOffset = 207;

        private const int BoundDryadMonsterData = 209082;
        private const int BoundDryadMonsterDataOffset = 326;
        private const int BoundDryadTailDeadNpcInstanceOffset = 338;
        private const int BoundDryadPacketLength = 410;

        private static readonly byte[] BoundDryadTemplate = StripSeq(HexToBytes(
            "0B19000A0001019A00000DB17A1ADE694F474E050000C76A00FDC80B00000000080000000B0000000000000000446B73BB42500A3D43618184000000003F754C06000000003E927F6F002080470000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C000000010000016800000076000000DF000000000000003B00000000000000040000000600000059000000010000019F0000C350000001A07A24CB810000002A000330360000003D000000000000000800004650000000220000003C0000001752656D61696E73206F6620426F756E64204472796164000000000200000032000003F100000003000007E20000CF2739D61EB40000000400000000000000010000000000000000000000000000000000000000000001F40000000100000004000330BA000000000000C3507A24CB81000017A600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000000"));

        private const int InfernalVortexoidMonsterData = 209458;
        private const int InfernalVortexoidMonsterDataOffset = 333;
        private const int InfernalVortexoidTailDeadNpcInstanceOffset = 345;
        private const int InfernalVortexoidPacketLength = 417;

        private static readonly byte[] InfernalVortexoidTemplate = StripSeq(HexToBytes(
            "037E000A000101A100000DB17A1ADE694F474E050000C76A00FDC81300000000080000000B000000000000000044578F7B42500A3D43181233000000003F4219A9000000003F26EA9C002080470000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C000000010000016800000062000000DF000000000000003B00000000000000040000000600000059000000010000019F0000C350000001A07A24CB800000002A0003320F0000003D000000000000000800004650000000220000003C0000001E52656D61696E73206F6620496E6665726E616C20566F727465786F6964000000000200000032000003F100000003000007E20000CF2739D61E650000000400000000000000010000000000000000000000000000000000000000000001F4000000010000000400033232000000000000C3507A24CB80000017A600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000000"));

        private const int MalahFamaMonsterData = 209252;
        private const int MalahFamaMonsterDataOffset = 325;
        private const int MalahFamaTailDeadNpcInstanceOffset = 337;
        private const int MalahFamaPacketLength = 409;

        private static readonly byte[] MalahFamaTemplate = StripSeq(HexToBytes(
            "02BF000A0001019900000DB17A1ADE694F474E050000C76A00FDC80400000000080000000B000000000000000044551F2642500A3D43389708000000003EFC0896000000003F5ED5F4002080470000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C000000010000016800000062000000DF000000000000003B00000000000000040000000600000059000000010000019F0000C350000001A07A24CB8F0000002A000330960000003D000000000000000800004650000000220000003C0000001652656D61696E73206F66204D616C61682D46616D61000000000200000032000003F100000003000007E20000CF2739D61E590000000400000000000000010000000000000000000000000000000000000000000001F4000000010000000400033164000000000000C3507A24CB8F000017A600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000000"));

        private const int WeaverofMaliceMonsterData = 209354;
        private const int WeaverofMaliceMonsterDataOffset = 331;
        private const int WeaverofMaliceTailDeadNpcInstanceOffset = 343;
        private const int WeaverofMalicePacketLength = 463;

        private static readonly byte[] WeaverofMaliceTemplate = StripSeq(HexToBytes(
            "0122000A000101CF00000DB17A1ADE694F474E050000C76A00FDC81800000000080000000B0000000000000000444FEF9242500A3D4339665D00000000BF3333C2800000003F36D185002080470000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C000000010000016800000028000000DF000000000000003B00000000000000040000000600000059000000010000019F0000C350000001A07A24CB7C0000002A000331880000003D000000000000000800004650000000220000003C0000001C52656D61696E73206F6620576561766572206F66204D616C696365000000000200000032000003F100000003000007E20000CF2739D61E490000000400000000000000010000000000000000000000000000000000000000000001F40000000100000004000331CA000000000000C3507A24CB7C000017A600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000001000007E24D6174657269616C202331330000000000000000000000000000000000000000000396DA0000000000000001"));

        private const int BurningShadowMonsterData = 209136;
        private const int BurningShadowMonsterDataOffset = 329;
        private const int BurningShadowTailDeadNpcInstanceOffset = 341;
        private const int BurningShadowPacketLength = 461;

        private static readonly byte[] BurningShadowTemplate = StripSeq(HexToBytes(
            "12C1000A000101CD00000DB17A1ADE694F474E050000C76A00FDC81B00000000080000000B000000000000000044109A2642500F5B43670EFC00000000BD433407800000003F7FB589002080470000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C000000010000016800000062000000DF000000000000003B00000000000000040000000600000059000000010000019F0000C350000001A07A24CB910000002A0003303B0000003D000000000000000800004650000000220000003C0000001A52656D61696E73206F66204275726E696E6720536861646F77000000000200000032000003F100000003000007E20000CF2739D61F400000000400000000000000010000000000000000000000000000000000000000000001F40000000100000004000330F0000000000000C3507A24CB91000017A600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000001000007E24D6174657269616C2023326200000000000000000000000000000000000000000003303D0000000000000001"));

        private const int IcyShadowMonsterData = 209125;
        private const int IcyShadowMonsterDataOffset = 325;
        private const int IcyShadowTailDeadNpcInstanceOffset = 337;
        private const int IcyShadowPacketLength = 457;

        private static readonly byte[] IcyShadowTemplate = StripSeq(HexToBytes(
            "16D4000A000101C900000DB17A1ADE694F474E050000C76A00FDC80400000000080000000B00000000000000004406289B42500D5C438220B700000000BF76277D000000003E8CA0DE002080470000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C000000010000016800000062000000DF000000000000003B00000000000000040000000600000059000000010000019F0000C350000001A07A24CB850000002A0003303B0000003D000000000000000800004650000000220000003C0000001652656D61696E73206F662049637920536861646F77000000000200000032000003F100000003000007E20000CF2739D61F9C0000000400000000000000010000000000000000000000000000000000000000000001F40000000100000004000330E5000000000000C3507A24CB85000017A600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000001000007E24D6174657269616C2023326200000000000000000000000000000000000000000003303F0000000000000001"));

        private const int CroakerofSolitudeMonsterData = 209326;
        private const int CroakerofSolitudeMonsterDataOffset = 334;
        private const int CroakerofSolitudeTailDeadNpcInstanceOffset = 346;
        private const int CroakerofSolitudePacketLength = 418;

        private static readonly byte[] CroakerofSolitudeTemplate = StripSeq(HexToBytes(
            "031C000A000101A200000DB17A1ADE694F474E050000C76A00FDC81300000000080000000B00000000000000004455DA3F42500A3D4338493B00000000BE07D9C6800000003F7DBCB5002080470000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C000000010000016800000062000000DF000000000000003B00000000000000040000000600000059000000010000019F0000C350000001A07A24CB7A0000002A000331700000003D000000000000000800004650000000220000003C0000001F52656D61696E73206F662043726F616B6572206F6620536F6C6974756465000000000200000032000003F100000003000007E20000CF2739D61E5F0000000400000000000000010000000000000000000000000000000000000000000001F40000000100000004000331AE000000000000C3507A24CB7A000017A600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000000"));

        private const int SmellyWeaverMonsterData = 209347;
        private const int SmellyWeaverMonsterDataOffset = 328;
        private const int SmellyWeaverTailDeadNpcInstanceOffset = 340;
        private const int SmellyWeaverPacketLength = 460;

        private static readonly byte[] SmellyWeaverTemplate = StripSeq(HexToBytes(
            "0827000A000101CC00000DB17A1ADE694F474E050000C76A00FDC81D00000000080000000B0000000000000000446635BB425005EF4333C2BF00000000BF582C33000000003F0921FA002080470000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C000000010000016800000028000000DF000000000000003B00000000000000040000000600000059000000010000019F0000C350000001A07A24CC370000002A000331880000003D000000000000000800004650000000220000003C0000001952656D61696E73206F6620536D656C6C7920576561766572000000000200000032000003F100000003000007E20000CF2739D61E8F0000000400000000000000010000000000000000000000000000000000000000000001F40000000100000004000331C3000000000000C3507A24CC37000017A600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000001000007E24D6174657269616C202331330000000000000000000000000000000000000000000396DA0000000000000001"));

        internal static byte[] TryBuild(
            ICharacter deadNpc,
            Identity corpseIdentity,
            Identity receiver,
            int serverId,
            int corpseCatMesh,
            int corpseMonsterData,
            int corpseCredits)
        {
            if (deadNpc == null
                || deadNpc.Playfield == null
                || !NascenceDungeon2Rules.IsDungeonPlayfield(deadNpc.Playfield.Identity.Instance))
            {
                return null;
            }

            byte[] template;
            int expectedLength;
            int monsterDataOffset;
            int tailDeadNpcOffset;
            int monsterDataValue;
            if (string.Equals(deadNpc.Name, "Bound Dryad", StringComparison.OrdinalIgnoreCase))
            {
                template = BoundDryadTemplate;
                expectedLength = BoundDryadPacketLength;
                monsterDataOffset = BoundDryadMonsterDataOffset;
                tailDeadNpcOffset = BoundDryadTailDeadNpcInstanceOffset;
                monsterDataValue = BoundDryadMonsterData;
            }
            else if (string.Equals(deadNpc.Name, "Infernal Vortexoid", StringComparison.OrdinalIgnoreCase))
            {
                template = InfernalVortexoidTemplate;
                expectedLength = InfernalVortexoidPacketLength;
                monsterDataOffset = InfernalVortexoidMonsterDataOffset;
                tailDeadNpcOffset = InfernalVortexoidTailDeadNpcInstanceOffset;
                monsterDataValue = InfernalVortexoidMonsterData;
            }
            else if (string.Equals(deadNpc.Name, "Malah-Fama", StringComparison.OrdinalIgnoreCase))
            {
                template = MalahFamaTemplate;
                expectedLength = MalahFamaPacketLength;
                monsterDataOffset = MalahFamaMonsterDataOffset;
                tailDeadNpcOffset = MalahFamaTailDeadNpcInstanceOffset;
                monsterDataValue = MalahFamaMonsterData;
            }
            else if (string.Equals(deadNpc.Name, "Weaver of Malice", StringComparison.OrdinalIgnoreCase))
            {
                template = WeaverofMaliceTemplate;
                expectedLength = WeaverofMalicePacketLength;
                monsterDataOffset = WeaverofMaliceMonsterDataOffset;
                tailDeadNpcOffset = WeaverofMaliceTailDeadNpcInstanceOffset;
                monsterDataValue = WeaverofMaliceMonsterData;
            }
            else if (string.Equals(deadNpc.Name, "Burning Shadow", StringComparison.OrdinalIgnoreCase))
            {
                template = BurningShadowTemplate;
                expectedLength = BurningShadowPacketLength;
                monsterDataOffset = BurningShadowMonsterDataOffset;
                tailDeadNpcOffset = BurningShadowTailDeadNpcInstanceOffset;
                monsterDataValue = BurningShadowMonsterData;
            }
            else if (string.Equals(deadNpc.Name, "Icy Shadow", StringComparison.OrdinalIgnoreCase))
            {
                template = IcyShadowTemplate;
                expectedLength = IcyShadowPacketLength;
                monsterDataOffset = IcyShadowMonsterDataOffset;
                tailDeadNpcOffset = IcyShadowTailDeadNpcInstanceOffset;
                monsterDataValue = IcyShadowMonsterData;
            }
            else if (string.Equals(deadNpc.Name, "Croaker of Solitude", StringComparison.OrdinalIgnoreCase))
            {
                template = CroakerofSolitudeTemplate;
                expectedLength = CroakerofSolitudePacketLength;
                monsterDataOffset = CroakerofSolitudeMonsterDataOffset;
                tailDeadNpcOffset = CroakerofSolitudeTailDeadNpcInstanceOffset;
                monsterDataValue = CroakerofSolitudeMonsterData;
            }
            else if (string.Equals(deadNpc.Name, "Smelly Weaver", StringComparison.OrdinalIgnoreCase))
            {
                template = SmellyWeaverTemplate;
                expectedLength = SmellyWeaverPacketLength;
                monsterDataOffset = SmellyWeaverMonsterDataOffset;
                tailDeadNpcOffset = SmellyWeaverTailDeadNpcInstanceOffset;
                monsterDataValue = SmellyWeaverMonsterData;
            }
            else
            {
                return null;
            }

            byte[] buffer = (byte[])template.Clone();
            if (buffer.Length != expectedLength)
            {
                return null;
            }

            WritePacketLength(buffer, buffer.Length);
            WriteInt32(buffer, ServerIdOffset, serverId);
            WriteInt32(buffer, ReceiverInstanceOffset, receiver.Instance);
            WriteInt32(buffer, CorpseInstanceOffset, corpseIdentity.Instance);
            WriteSingle(buffer, PositionXOffset, (float)deadNpc.Position.x);
            WriteSingle(buffer, PositionYOffset, (float)deadNpc.Position.y);
            WriteSingle(buffer, PositionZOffset, (float)deadNpc.Position.z);
            WriteInt32(buffer, PlayfieldIdOffset, deadNpc.Playfield.Identity.Instance);
            WriteInt32(buffer, MonsterScaleOffset, deadNpc.Stats[StatIds.monsterscale].Value);
            WriteInt32(buffer, SexOffset, deadNpc.Stats[StatIds.sex].Value);
            WriteInt32(buffer, BreedOffset, deadNpc.Stats[StatIds.breed].Value);
            WriteInt32(buffer, RaceOffset, deadNpc.Stats[StatIds.race].Value);
            WriteInt32(buffer, DeadNpcInstanceOffset, deadNpc.Identity.Instance);
            // Preserve capture CatMesh when caller has no usable mapping (0 → neon/missing mesh).
            if (corpseCatMesh > 0)
            {
                WriteInt32(buffer, CorpseCatMeshOffset, corpseCatMesh);
            }

            WriteInt32(buffer, CorpseCashValueOffset, Math.Max(0, corpseCredits));
            WriteInt32(buffer, monsterDataOffset, monsterDataValue);
            WriteInt32(buffer, tailDeadNpcOffset, deadNpc.Identity.Instance);
            return buffer;
        }

        /// <summary>
        /// AOSharp seq → CFU frame: zero the 2-byte seq in place. Same length as capture
        /// (do not pad +2 — that broke PacketLength checks / framed trailing zeros).
        /// </summary>
        private static byte[] StripSeq(byte[] packet)
        {
            if (packet == null || packet.Length < 6)
            {
                return packet;
            }

            if (packet[2] == 0x00 && packet[3] == 0x0A)
            {
                var stripped = (byte[])packet.Clone();
                stripped[0] = 0;
                stripped[1] = 0;
                return stripped;
            }

            return packet;
        }

        private static void WritePacketLength(byte[] buffer, int length)
        {
            // Match CorpseFullUpdate: length lives at bytes 6–7 of the 0000 000A frame.
            buffer[6] = (byte)((length >> 8) & 0xFF);
            buffer[7] = (byte)(length & 0xFF);
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            // Capture CFU integers are big-endian (same as CorpseFullUpdate).
            byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
        }

        private static void WriteSingle(byte[] buffer, int offset, double value)
        {
            byte[] bytes = BitConverter.GetBytes((float)value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
        }

        private static byte[] HexToBytes(string hex)
        {
            int length = hex.Length / 2;
            var bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
