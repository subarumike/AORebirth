#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ChatEngine.Packets
{
    using ChatEngine.CoreClient;

    /// <summary>
    /// Line flags in a chat line's block (the length-prefixed data after the text). The block is a run of entries, a
    /// tag byte then its data: tag 0 IIISS, tag 1 a text macro BBSS, tag 2 one flags byte. Flag bit 1 makes the client
    /// show the sender's name red with " (GM)", and an ignore does not drop the line.
    /// </summary>
    public static class ChatLineFlags
    {
        /// <summary>The flags entry for a GM's line: tag 2, flags 1.</summary>
        public static byte[] GmEntry => new byte[] { 0x02, 0x01 };

        /// <summary>True when <paramref name="sender"/> is a character with a GM level above 0.</summary>
        public static bool IsGm(Client sender)
        {
            return sender != null && sender.Character != null && sender.Character.CharacterGMLevel > 0;
        }
    }

    /// <summary>
    /// A tell (PrivateMessage, 30): sender, text, then the block. The block's first byte is 00 and the line-flag
    /// entries follow it (<see cref="ChatLineFlags"/>): 00 for anyone, 00 02 01 for a GM.
    /// </summary>
    public static class MsgPrivate
    {
        public static byte[] Create(uint senderId, string message, bool gm = false)
        {
            PacketWriter writer = new PacketWriter((ushort)MessageType.PrivateMessage);
            writer.WriteUInt32(senderId);
            writer.WriteString(message ?? string.Empty);
            writer.WriteUInt16((ushort)(gm ? 3 : 1));
            writer.WriteByte(0x00);
            if (gm)
                writer.WriteBytes(ChatLineFlags.GmEntry);
            return writer.Finish();
        }
    }
}
