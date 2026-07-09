namespace ZoneEngine.Core.Packets
{
    using System;
    using System.Net;
    using System.Text;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    public static class CorpseFullUpdate
    {
        private const int OriginalEncodedNameLength = 27;
        private const int NameOffset = 231;
        private const int NameLengthOffset = 227;
        private const int OriginalSuffixOffset = NameOffset + OriginalEncodedNameLength;

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
        // The captured template encodes corpse cash as stat id 61 at offset 203
        // followed by its 32-bit value at offset 207. The old template value was
        // hardcoded to 111, so this offset must patch the value word, not the
        // following word at 211.
        private const int CorpseCashValueOffset = 207;
        private const int CorpseMonsterDataOffset = 330;
        private const int TailDeadNpcInstanceOffset = 342;

        private const int SubwayPlayfieldResource = 127;
        private const int SubwayFilthFleaMonsterData = 17657;
        private const string SubwayFilthFleaName = "Filth Flea";
        private const int CapturedSubwayFilthFleaPacketLength = 457;
        private const int CapturedSubwayFilthFleaMonsterDataOffset = 325;
        private const int CapturedSubwayFilthFleaTailDeadNpcInstanceOffset = 337;

        private static readonly byte[] Template = HexToBytes(
            "0000000a0001019e000000003cac6f144f474e050000c76a00f0f00100000000080000000b00000000000000004504a4df41c5ea1244cb530d000000003e8fb30a000000003f75b5e0000002350000000000000000006f000046f200000000001818050000001700000000000002bd00000000000002be00000000000002bf000000000000019c000000010000016800000062000000df000000000000003b00000003000000040000000700000059000000010000019f0000c350000001a0776b95780000002a0000797e0000003d000000000000000800004650000000220000003c0000001b52656d61696e73206f66205268696e6f6d616e204d6f74686572000000000200000032000003f100000003000007e20000cf2738f46cbe0000000400000000000000010000000000000000000000000000000000000000000001f700000001000000040000798a000000000000c350776b9578000017a600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000000");

        // Official live Subway capture 20260709-164414, CorpseFullUpdate packet #587.
        // The flea corpse requires the captured Material #9 visual tail; the generic
        // corpse template is shorter and does not create a visible flea body.
        private static readonly byte[] CapturedSubwayFilthFleaTemplate = HexToBytes(
            "0000000A000101C900000DB970CBBEF34F474E050000C76A00F6E00900000000080000000B000000000000000042B45D5F42E73AE14397BF79000000"
            + "00BED08F91800000003F69CC57001220020000000000000000006F000046F200000000001818050000001700000000000002BD00000000000002BE00"
            + "000000000002BF000000000000019C000000010000016800000082000000DF000000000000003B000000000000000400000006000000590000000100"
            + "00019F0000C350000001A079528CA70000002A00003B7F0000003D000000170000000800004650000000220000003C0000001652656D61696E73206F"
            + "662046696C746820466C6561000000000200000032000003F100000003000007E20000CF273975F70E00000004000000000000000100000000000000"
            + "00000000000000000000000000000001F70000000100000004000044F9000000000000C35079528CA7000017A6000000000000000000000000000000"
            + "01000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000001000007E24D617465726961"
            + "6C20233900000000000000000000000000000000000000000000003B810000000000000001");

        public static byte[] Build(
            ICharacter deadNpc,
            Identity corpseIdentity,
            Identity receiver,
            int serverId,
            int corpseCatMesh,
            int corpseMonsterData,
            int corpseCredits)
        {
            if (IsCapturedSubwayFilthFlea(deadNpc))
            {
                return BuildCapturedSubwayFilthFlea(
                    deadNpc,
                    corpseIdentity,
                    receiver,
                    serverId,
                    corpseCatMesh,
                    corpseMonsterData,
                    corpseCredits);
            }

            string corpseName = "Remains of " + deadNpc.Name;
            byte[] nameBytes = Encoding.ASCII.GetBytes(corpseName);
            int encodedNameLength = nameBytes.Length + 1;

            // CorpseFullUpdate resumes immediately after the encoded string's trailing null.
            // Padding this to four bytes shifts the animation/identity tail and the client
            // never registers the spawned corpse dynel.
            int newSuffixOffset = NameOffset + encodedNameLength;
            int afterNameDelta = newSuffixOffset - OriginalSuffixOffset;
            byte[] buffer = new byte[Template.Length + afterNameDelta];

            Buffer.BlockCopy(Template, 0, buffer, 0, NameOffset);
            Buffer.BlockCopy(nameBytes, 0, buffer, NameOffset, nameBytes.Length);
            Buffer.BlockCopy(
                Template,
                OriginalSuffixOffset,
                buffer,
                newSuffixOffset,
                Template.Length - OriginalSuffixOffset);

            WritePacketLength(buffer, buffer.Length);
            WriteInt32(buffer, ServerIdOffset, serverId);
            WriteInt32(buffer, ReceiverInstanceOffset, receiver.Instance);
            WriteInt32(buffer, CorpseInstanceOffset, corpseIdentity.Instance);
            WriteSingle(buffer, PositionXOffset, deadNpc.RawCoordinates.X);
            WriteSingle(buffer, PositionYOffset, deadNpc.RawCoordinates.Y);
            WriteSingle(buffer, PositionZOffset, deadNpc.RawCoordinates.Z);
            WriteInt32(buffer, PlayfieldIdOffset, deadNpc.Playfield.Identity.Instance);
            WriteInt32(buffer, MonsterScaleOffset, deadNpc.Stats[StatIds.monsterscale].Value);
            WriteInt32(buffer, SexOffset, deadNpc.Stats[StatIds.sex].Value);
            WriteInt32(buffer, BreedOffset, deadNpc.Stats[StatIds.breed].Value);
            WriteInt32(buffer, RaceOffset, deadNpc.Stats[StatIds.race].Value);
            WriteInt32(buffer, DeadNpcInstanceOffset, deadNpc.Identity.Instance);
            WriteInt32(buffer, CorpseCatMeshOffset, corpseCatMesh);
            WriteInt32(buffer, CorpseCashValueOffset, Math.Max(0, corpseCredits));
            WriteInt32(buffer, NameLengthOffset, encodedNameLength);
            WriteInt32(buffer, CorpseMonsterDataOffset + afterNameDelta, corpseMonsterData);
            WriteInt32(buffer, TailDeadNpcInstanceOffset + afterNameDelta, deadNpc.Identity.Instance);

            return buffer;
        }

        private static bool IsCapturedSubwayFilthFlea(ICharacter deadNpc)
        {
            return deadNpc != null
                   && deadNpc.Playfield != null
                   && deadNpc.Playfield.Identity.Instance == SubwayPlayfieldResource
                   && deadNpc.Stats[StatIds.monsterdata].Value == SubwayFilthFleaMonsterData
                   && string.Equals(deadNpc.Name, SubwayFilthFleaName, StringComparison.Ordinal);
        }

        private static byte[] BuildCapturedSubwayFilthFlea(
            ICharacter deadNpc,
            Identity corpseIdentity,
            Identity receiver,
            int serverId,
            int corpseCatMesh,
            int corpseMonsterData,
            int corpseCredits)
        {
            byte[] buffer = (byte[])CapturedSubwayFilthFleaTemplate.Clone();
            if (buffer.Length != CapturedSubwayFilthFleaPacketLength)
            {
                throw new InvalidOperationException("Captured Subway Filth Flea corpse template length changed.");
            }

            WritePacketLength(buffer, buffer.Length);
            WriteInt32(buffer, ServerIdOffset, serverId);
            WriteInt32(buffer, ReceiverInstanceOffset, receiver.Instance);
            WriteInt32(buffer, CorpseInstanceOffset, corpseIdentity.Instance);
            WriteSingle(buffer, PositionXOffset, deadNpc.RawCoordinates.X);
            WriteSingle(buffer, PositionYOffset, deadNpc.RawCoordinates.Y);
            WriteSingle(buffer, PositionZOffset, deadNpc.RawCoordinates.Z);
            WriteInt32(buffer, PlayfieldIdOffset, deadNpc.Playfield.Identity.Instance);
            WriteInt32(buffer, DeadNpcInstanceOffset, deadNpc.Identity.Instance);
            WriteInt32(buffer, CorpseCatMeshOffset, corpseCatMesh);
            WriteInt32(buffer, CorpseCashValueOffset, Math.Max(0, corpseCredits));
            WriteInt32(buffer, CapturedSubwayFilthFleaMonsterDataOffset, corpseMonsterData);
            WriteInt32(
                buffer,
                CapturedSubwayFilthFleaTailDeadNpcInstanceOffset,
                deadNpc.Identity.Instance);

            return buffer;
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
        }

        private static void WritePacketLength(byte[] buffer, int length)
        {
            buffer[6] = (byte)((length >> 8) & 0xff);
            buffer[7] = (byte)(length & 0xff);
        }

        private static void WriteSingle(byte[] buffer, int offset, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
        }
    }
}
