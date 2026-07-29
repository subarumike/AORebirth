namespace ChatEngine.Packets
{
    /// <summary>
    /// Looking for Team search reply (chat type 0x05DD / 1501).
    /// Client PPJ format BISIIBBS (GUI.dll HandleLFTMessage / LftQueryReply_t):
    ///   B mode (0=add row, 2=clear/empty)
    ///   I characterId
    ///   S name
    ///   I level
    ///   I playfield/location
    ///   B side
    ///   B profession
    ///   S comment
    /// Empty capture 20260727-104625 matches mode=2 with all-zero fields.
    /// One chat message per row (not a counted array).
    /// </summary>
    public static class LftQueryReply
    {
        public const byte ModeAdd = 0;

        public const byte ModeClear = 2;

        public sealed class Entry
        {
            public uint CharacterId;

            public string Name;

            public uint Level;

            public uint Playfield;

            public byte Side;

            public byte Profession;

            public string Comment;
        }

        public static byte[] CreateClear()
        {
            // Exact empty reply from live capture 20260727-104625.
            PacketWriter writer = new PacketWriter(1501);
            writer.WriteByte(ModeClear);
            writer.WriteUInt32(0);
            WritePpjString(writer, string.Empty);
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);
            writer.WriteByte(0);
            writer.WriteByte(0);
            WritePpjString(writer, string.Empty);
            return writer.Finish();
        }

        public static byte[] CreateEntry(Entry entry)
        {
            PacketWriter writer = new PacketWriter(1501);
            writer.WriteByte(ModeAdd);
            writer.WriteUInt32(entry.CharacterId);
            WritePpjString(writer, entry.Name ?? string.Empty);
            writer.WriteUInt32(entry.Level);
            writer.WriteUInt32(entry.Playfield);
            writer.WriteByte(entry.Side);
            writer.WriteByte(entry.Profession);
            WritePpjString(writer, entry.Comment ?? string.Empty);
            return writer.Finish();
        }

        /// <summary>
        /// PPJ S: u16be length + bytes. Empty is 00 00 (not PacketWriter's 00 01 00).
        /// </summary>
        private static void WritePpjString(PacketWriter writer, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                writer.WriteUInt16(0);
                return;
            }

            writer.WriteString(value);
        }
    }
}
