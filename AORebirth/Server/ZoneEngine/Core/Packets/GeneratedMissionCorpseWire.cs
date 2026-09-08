namespace ZoneEngine.Core.Packets
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;

    /// <summary>Pure projection of the accepted L7 Tilda mission corpse, shared by both engines.</summary>
    public static class GeneratedMissionCorpseWire
    {
        // Unchanged 20260725-002423 Tilda Konecny CFU; no Biofreak or inferred material tail.
        private static readonly byte[] Template = Decode(
            "03E8000A000101A400000DB4797E30D74F474E050000C76A00F7482700000000080000000B0000000000000000437FA2D540"
            + "A051EC436AFD7100000000BF484630000000003F1F74C5001608000000000000000000006F00004AE3000000000018180500"
            + "00001700000000000002BD00000000000002BE00000000000002BF000000000000019C00000001000001680000005D000000"
            + "DF000000010000003B00000003000000040000000200000059000000010000019F0000C350000001A0799361EA0000002A00"
            + "00172E0000003D0000001D0000000800004650000000220000003C0000004000009D110000001952656D61696E73206F6620"
            + "54696C6461204B6F6E65636E79000000000200000032000003F100000003000007E20000CF273995AE700000000400000000"
            + "000000010000000000000000000000000000000000000000000001F5000000010000000400006619000000000000C3507993"
            + "61EA000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030001558E"
            + "0000000000000004000058630000000000000000");

        public static byte[] CopyTemplate() { return (byte[])Template.Clone(); }

        public static KeyValuePair<int, int>[] MissionCatMeshMappings()
        {
            // Existing CombatCorpseVisuals mission-only accepted map, not name matching.
            return new[] {
                new KeyValuePair<int, int>(26159, 17909), new KeyValuePair<int, int>(26139, 5914),
                new KeyValuePair<int, int>(26155, 23370), new KeyValuePair<int, int>(26137, 5934),
                new KeyValuePair<int, int>(26076, 17530), new KeyValuePair<int, int>(26101, 23366),
                new KeyValuePair<int, int>(26088, 17534), new KeyValuePair<int, int>(26103, 23366),
                new KeyValuePair<int, int>(26135, 5934), new KeyValuePair<int, int>(26074, 23366),
                new KeyValuePair<int, int>(26090, 5934), new KeyValuePair<int, int>(26092, 17530),
                new KeyValuePair<int, int>(26097, 23366), new KeyValuePair<int, int>(26123, 17530)
            };
        }

        public static byte[] Build(string name, int deadNpcInstance, int corpseInstance,
            int receiverInstance, int serverId, float x, float y, float z, int playfield,
            int scale, int sex, int breed, int race, int catMesh, int monsterData, int credits)
        {
            const int nameOffset = 239, originalSuffixOffset = 264;
            byte[] nameBytes = Encoding.ASCII.GetBytes("Remains of " + (name ?? "Unknown"));
            int nameLength = nameBytes.Length + 1;
            int suffixOffset = nameOffset + nameLength;
            int delta = suffixOffset - originalSuffixOffset;
            byte[] buffer = new byte[Template.Length + delta];
            Buffer.BlockCopy(Template, 0, buffer, 0, nameOffset);
            Buffer.BlockCopy(nameBytes, 0, buffer, nameOffset, nameBytes.Length);
            buffer[nameOffset + nameBytes.Length] = 0;
            Buffer.BlockCopy(Template, originalSuffixOffset, buffer, suffixOffset, Template.Length - originalSuffixOffset);
            buffer[6] = (byte)((buffer.Length >> 8) & 0xff); buffer[7] = (byte)(buffer.Length & 0xff);
            WriteInt(buffer, 8, serverId); WriteInt(buffer, 12, receiverInstance); WriteInt(buffer, 24, corpseInstance);
            WriteSingle(buffer, 45, x); WriteSingle(buffer, 49, y); WriteSingle(buffer, 53, z);
            WriteInt(buffer, 73, playfield); WriteInt(buffer, 143, scale);
            WriteInt(buffer, 159, sex); WriteInt(buffer, 167, breed); WriteInt(buffer, 175, race);
            WriteInt(buffer, 191, deadNpcInstance); WriteInt(buffer, 199, catMesh > 0 ? catMesh : 5934);
            WriteInt(buffer, 207, Math.Max(0, credits)); WriteInt(buffer, 235, nameLength);
            WriteInt(buffer, 336 + delta, monsterData > 0 ? monsterData : 26137);
            WriteInt(buffer, 348 + delta, deadNpcInstance);
            return buffer;
        }

        private static byte[] Decode(string hex)
        {
            byte[] result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return result;
        }
        private static void WriteInt(byte[] buffer, int offset, int value)
        {
            byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
        }
        private static void WriteSingle(byte[] buffer, int offset, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
        }
    }
}
