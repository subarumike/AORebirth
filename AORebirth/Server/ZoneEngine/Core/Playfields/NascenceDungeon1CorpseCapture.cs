namespace AORebirth.Core.Playfields
{
    using System;
    using System.Net;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Capture 20260823-171238 corpse-full-updates for Nascence SL ACG interior mobs.
    /// Mission-trash 420B template breaks Spirit/Croaker/Weaver corpses (wrong length/tail).
    /// </summary>
    internal static class NascenceDungeon1CorpseCapture
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

        // Capture Remains of Panicked Coral Rafter (420B).
        private const int CoralRafterMonsterData = 212846;
        private const int CoralRafterMonsterDataOffset = 330;
        private const int CoralRafterTailDeadNpcInstanceOffset = 342;
        private const int CoralRafterPacketLength = 420;

        // Capture Remains of Wailing Spirit (513B, ExtTex spirit tail).
        private const int WailingSpiritMonsterData = 217022;
        private const int WailingSpiritMonsterDataOffset = 330;
        private const int WailingSpiritTailDeadNpcInstanceOffset = 342;
        private const int WailingSpiritPacketLength = 513;

        // Capture Remains of Smelly Weaver (460B, Material #13 tail).
        private const int SmellyWeaverMonsterData = 209347;
        private const int SmellyWeaverMonsterDataOffset = 330;
        private const int SmellyWeaverTailDeadNpcInstanceOffset = 342;
        private const int SmellyWeaverPacketLength = 460;

        // Capture Remains of Crippler of Destiny (418B).
        private const int CripplerMonsterData = 209340;
        private const int CripplerMonsterDataOffset = 330;
        private const int CripplerTailDeadNpcInstanceOffset = 342;
        private const int CripplerPacketLength = 418;

        // Capture Remains of Croaker of Desolation (512B).
        private const int CroakerDesolationMonsterData = 209319;
        private const int CroakerDesolationMonsterDataOffset = 330;
        private const int CroakerDesolationTailDeadNpcInstanceOffset = 342;
        private const int CroakerDesolationPacketLength = 512;

        // Capture Remains of Croaker of Solitude (418B).
        private const int CroakerSolitudeMonsterData = 209326;
        private const int CroakerSolitudeMonsterDataOffset = 330;
        private const int CroakerSolitudeTailDeadNpcInstanceOffset = 342;
        private const int CroakerSolitudePacketLength = 418;

        // Capture Remains of Havaris (498B, ExtTex Velf tail). Capture 20260824-175852.
        // Shares monsterData 212846 with Coral Rafter — route by mob name.
        private const int HavarisMonsterData = 212846;
        private const int HavarisMonsterDataOffset = 330;
        private const int HavarisTailDeadNpcInstanceOffset = 342;
        private const int HavarisPacketLength = 498;

        private static readonly byte[] HavarisTemplate = StripSeq(HexToBytes(
            "1D1E000A000101F200000DAE78D840404F474E050000C76A00FE900100000000080000000B000000000000000042FAE930428007AE432DF9D0000000003E8027F3000000003F77D9CC002080660000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C00000001000001680000007D000000DF000000000000003B00000000000000040000000600000059000000010000019F0000C350000001A07A2969BE0000002A000338080000003D00000000000000080002BF20000000220000003C0000001352656D61696E73206F662048617661726973000000000200000032000003F100000003000007E20000CF2739D7F5470000000400000000000000010000000000000000000000000000000000000000000001F5000000010000000400033F6E000000000000C3507A2969BE000017A60000000000000000000000000000000100000000000000000000000200000000000000000000000300000000000000000000000400000000000000000000000100000BD373656656C6620696C6C756D696E6174696F6E20626F6479200000000000000000000003380A00000000000000015B325D206F706163697479206D617073000000000000000000000000000000000003380A0000000000000000"));

        private static readonly byte[] CoralRafterTemplate = StripSeq(HexToBytes(
            "00B7000A000101A400000DB67A1ADE694F474E050000C76A00FD900100000000080000000B0000000000000000444F9BF342500A3D43062B7F000000003D2FFBC5000000003F7FC37C001F804E0000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C00000001000001680000007D000000DF000000000000003B00000000000000040000000600000059000000010000019F0000C350000001A07A24999B0000002A000338080000003D000000000000000800004650000000220000003C0000002152656D61696E73206F662050616E69636B656420436F72616C20526166746572000000000200000032000003F100000003000007E20000CF2739D3D5C00000000400000000000000010000000000000000000000000000000000000000000001F4000000010000000400033F6E000000000000C3507A24999B000017A600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000000"));

        private static readonly byte[] WailingSpiritTemplate = StripSeq(HexToBytes(
            "0170000A0001020100000DB67A1ADE694F474E050000C76A00FD900200000000080000000B0000000000000000445ACA8842500A3D431AEDBE000000003F299388000000003F3FC7E3001F804E0000000000000000006F00004AE300000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C000000010000016800000062000000DF000000000000003B00000003000000040000000100000059000000010000019F0000C350000001A07A2499AC0000002A0003478D0000003D000000000000000800004650000000220000003C00000040000000000000001A52656D61696E73206F66205761696C696E6720537069726974000000000200000032000003F100000003000007E20000CF2739D3D5C10000000400000000000000010000000000000000000000000000000000000000000001F4000000010000000400034FBE000000000000C3507A2499AC000017A60000000000000000000000000000000100000000000000000000000200000000000000000000000300000000000000000000000400000000000000000000000100000BD3686561645F7370697269742072656465656D65642066656D616C650000000000000396B9000000000000000072656465656D656420626F647920000000000000000000000000000000000000000396B70000000000000000"));

        private static readonly byte[] SmellyWeaverTemplate = StripSeq(HexToBytes(
            "04BF000A000101CC00000DB67A1ADE694F474E050000C76A00FD900200000000080000000B00000000000000004450192142500A3D43387429000000003F395304000000003F309C07001F804E0000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C000000010000016800000028000000DF000000000000003B00000000000000040000000600000059000000010000019F0000C350000001A07A2499C00000002A000331880000003D000000000000000800004650000000220000003C0000001952656D61696E73206F6620536D656C6C7920576561766572000000000200000032000003F100000003000007E20000CF2739D3D5C70000000400000000000000010000000000000000000000000000000000000000000001F40000000100000004000331C3000000000000C3507A2499C0000017A600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000001000007E24D6174657269616C202331330000000000000000000000000000000000000000000396DA0000000000000001"));

        private static readonly byte[] CripplerTemplate = StripSeq(HexToBytes(
            "0597000A000101A200000DB67A1ADE694F474E050000C76A00FD900300000000080000000B0000000000000000445AE85042500A3D4353A2B400000000BE288D80800000003F7C8217001F804E0000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C000000010000016800000062000000DF000000000000003B00000000000000040000000600000059000000010000019F0000C350000001A07A2499A00000002A0003317B0000003D000000000000000800004650000000220000003C0000001F52656D61696E73206F662043726970706C6572206F662044657374696E79000000000200000032000003F100000003000007E20000CF2739D3D5C80000000400000000000000010000000000000000000000000000000000000000000001F40000000100000004000331BC000000000000C3507A2499A0000017A600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000000"));

        private static readonly byte[] CroakerDesolationTemplate = StripSeq(HexToBytes(
            "12FB000A0001020000000DB67A1ADE694F474E050000C76A00FD900100000000080000000B00000000000000004426B56342500D5C433C5A9900000000BF787D28000000003E763A6C001F804E0000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C000000010000016800000062000000DF000000000000003B00000000000000040000000600000059000000010000019F0000C350000001A07A2499B20000002A000331700000003D000000000000000800004650000000220000003C0000002152656D61696E73206F662043726F616B6572206F66204465736F6C6174696F6E000000000200000032000003F100000003000007E20000CF2739D3D61A0000000400000000000000010000000000000000000000000000000000000000000001F40000000100000004000331A7000000000000C3507A2499B2000017A60000000000000000000000000000000100000000000000000000000200000000000000000000000300000000000000000000000400000000000000000000000100000BD3616E7669616E31206F7061000000000000000000000000000000000000000000000331740000000000000001616E7669616E312073656C660000000000000000000000000000000000000000000331740000000000000001"));

        private static readonly byte[] CroakerSolitudeTemplate = StripSeq(HexToBytes(
            "15A1000A000101A200000DB67A1ADE694F474E050000C76A00FD900E000000000800000000B0000000000000000441C4DD542540F5B435C983000000000BF261D99800000003F42C93A001F804E0000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00000000000002BF000000000000019C000000010000016800000063000000DF000000000000003B00000000000000040000000600000059000000010000019F0000C350000001A07A2499BC0000002A000331700000003D000000000000000800004650000000220000003C0000001F52656D61696E73206F662043726F616B6572206F6620536F6C6974756465000000000200000032000003F100000003000007E20000CF2739D3D6290000000400000000000000010000000000000000000000000000000000000000000001F40000000100000004000331AE000000000000C3507A2499BC000017A600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000000"));

        internal static bool IsCapturedCorpseMonsterData(int monsterData)
        {
            return monsterData == CoralRafterMonsterData
                   || monsterData == WailingSpiritMonsterData
                   || monsterData == SmellyWeaverMonsterData
                   || monsterData == CripplerMonsterData
                   || monsterData == CroakerDesolationMonsterData
                   || monsterData == CroakerSolitudeMonsterData;
        }

        internal static byte[] TryBuild(
            ICharacter deadNpc,
            Identity corpseIdentity,
            Identity receiver,
            int serverId,
            int corpseCatMesh,
            int corpseMonsterData,
            int corpseCredits)
        {
            if (deadNpc == null)
            {
                return null;
            }

            byte[] template;
            int expectedLength;
            int monsterDataOffset;
            int tailDeadNpcOffset;
            int monsterDataValue;
            if (string.Equals(deadNpc.Name, "Havaris", StringComparison.OrdinalIgnoreCase))
            {
                template = HavarisTemplate;
                expectedLength = HavarisPacketLength;
                monsterDataOffset = HavarisMonsterDataOffset;
                tailDeadNpcOffset = HavarisTailDeadNpcInstanceOffset;
                monsterDataValue = HavarisMonsterData;
            }
            else if (!IsCapturedCorpseMonsterData(corpseMonsterData))
            {
                return null;
            }
            else
            {
                monsterDataValue = corpseMonsterData;
                switch (corpseMonsterData)
                {
                    case CoralRafterMonsterData:
                        template = CoralRafterTemplate;
                        expectedLength = CoralRafterPacketLength;
                        monsterDataOffset = CoralRafterMonsterDataOffset;
                        tailDeadNpcOffset = CoralRafterTailDeadNpcInstanceOffset;
                        break;
                    case WailingSpiritMonsterData:
                        template = WailingSpiritTemplate;
                        expectedLength = WailingSpiritPacketLength;
                        monsterDataOffset = WailingSpiritMonsterDataOffset;
                        tailDeadNpcOffset = WailingSpiritTailDeadNpcInstanceOffset;
                        break;
                    case SmellyWeaverMonsterData:
                        template = SmellyWeaverTemplate;
                        expectedLength = SmellyWeaverPacketLength;
                        monsterDataOffset = SmellyWeaverMonsterDataOffset;
                        tailDeadNpcOffset = SmellyWeaverTailDeadNpcInstanceOffset;
                        break;
                    case CripplerMonsterData:
                        template = CripplerTemplate;
                        expectedLength = CripplerPacketLength;
                        monsterDataOffset = CripplerMonsterDataOffset;
                        tailDeadNpcOffset = CripplerTailDeadNpcInstanceOffset;
                        break;
                    case CroakerDesolationMonsterData:
                        template = CroakerDesolationTemplate;
                        expectedLength = CroakerDesolationPacketLength;
                        monsterDataOffset = CroakerDesolationMonsterDataOffset;
                        tailDeadNpcOffset = CroakerDesolationTailDeadNpcInstanceOffset;
                        break;
                    case CroakerSolitudeMonsterData:
                        template = CroakerSolitudeTemplate;
                        expectedLength = CroakerSolitudePacketLength;
                        monsterDataOffset = CroakerSolitudeMonsterDataOffset;
                        tailDeadNpcOffset = CroakerSolitudeTailDeadNpcInstanceOffset;
                        break;
                    default:
                        return null;
                }
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
            WriteInt32(buffer, CorpseCatMeshOffset, corpseCatMesh);
            WriteInt32(buffer, CorpseCashValueOffset, Math.Max(0, corpseCredits));
            WriteInt32(buffer, monsterDataOffset, monsterDataValue);
            WriteInt32(buffer, tailDeadNpcOffset, deadNpc.Identity.Instance);
            return buffer;
        }

        private static byte[] StripSeq(byte[] packet)
        {
            if (packet == null || packet.Length < 6)
            {
                return packet;
            }

            // AOSharp seq-stripped frame: leading 0000 + 000A… payload.
            if (packet[2] == 0x00 && packet[3] == 0x0A)
            {
                var stripped = new byte[packet.Length + 2];
                stripped[0] = 0;
                stripped[1] = 0;
                Buffer.BlockCopy(packet, 2, stripped, 2, packet.Length - 2);
                return stripped;
            }

            return packet;
        }

        private static void WritePacketLength(byte[] buffer, int length)
        {
            buffer[0] = (byte)((length >> 8) & 0xFF);
            buffer[1] = (byte)(length & 0xFF);
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
        }

        private static void WriteSingle(byte[] buffer, int offset, double value)
        {
            byte[] bytes = BitConverter.GetBytes((float)value);
            if (!BitConverter.IsLittleEndian)
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
