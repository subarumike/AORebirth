namespace ZoneEngine.Core.Packets
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Linq;
    using System.Buffers.Binary;
    using ZoneEngine.Core.Missions;

    /// <summary>Generic projection of the configured mission corpse packet shape.</summary>
    public static class GeneratedMissionCorpseWire
    {
        // Existing wire layout is preserved as editable content.
        private static byte[] Template => Decode(MissionCorpseContent.Current.TemplateHex);

        public static byte[] CopyTemplate() { return (byte[])Template.Clone(); }

        public static KeyValuePair<int, int>[] MissionCatMeshMappings()
        {
            return MissionCorpseContent.Current.CatMeshes.ToArray();
        }

        public static byte[] Build(string name, int deadNpcInstance, int corpseInstance,
            int receiverInstance, int serverId, float x, float y, float z, int playfield,
            int scale, int sex, int breed, int race, int catMesh, int monsterData, int credits)
        {
            const int nameOffset = 239;
            byte[] template = Template;
            int originalSuffixOffset = checked(nameOffset + BinaryPrimitives.ReadInt32BigEndian(template.AsSpan(235, 4)));
            byte[] nameBytes = Encoding.ASCII.GetBytes("Remains of " + (name ?? "Unknown"));
            int nameLength = nameBytes.Length + 1;
            int suffixOffset = nameOffset + nameLength;
            int delta = suffixOffset - originalSuffixOffset;
            byte[] buffer = new byte[template.Length + delta];
            Buffer.BlockCopy(template, 0, buffer, 0, nameOffset);
            Buffer.BlockCopy(nameBytes, 0, buffer, nameOffset, nameBytes.Length);
            buffer[nameOffset + nameBytes.Length] = 0;
            Buffer.BlockCopy(template, originalSuffixOffset, buffer, suffixOffset, template.Length - originalSuffixOffset);
            buffer[6] = (byte)((buffer.Length >> 8) & 0xff); buffer[7] = (byte)(buffer.Length & 0xff);
            WriteInt(buffer, 8, serverId); WriteInt(buffer, 12, receiverInstance); WriteInt(buffer, 24, corpseInstance);
            WriteSingle(buffer, 45, x); WriteSingle(buffer, 49, y); WriteSingle(buffer, 53, z);
            WriteInt(buffer, 73, playfield); WriteInt(buffer, 143, scale);
            WriteInt(buffer, 159, sex); WriteInt(buffer, 167, breed); WriteInt(buffer, 175, race);
            WriteInt(buffer, 191, deadNpcInstance); WriteInt(buffer, 199, catMesh > 0 ? catMesh : MissionCorpseContent.Current.DefaultCatMesh);
            WriteInt(buffer, 207, Math.Max(0, credits)); WriteInt(buffer, 235, nameLength);
            WriteInt(buffer, 336 + delta, monsterData > 0 ? monsterData : MissionCorpseContent.Current.DefaultMonsterData);
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
