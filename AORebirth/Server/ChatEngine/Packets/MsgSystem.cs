#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ChatEngine.Packets
{
    /// <summary>
    /// System-message and owner-pet chat packet construction.
    /// </summary>
    public static class MsgSystem
    {
        #region Public Methods and Operators

        public static byte[] Create(string message)
        {
            PacketWriter writer = new PacketWriter((ushort)MessageType.SystemMessage);
            writer.WriteString(message ?? string.Empty);
            return writer.Finish();
        }

        /// <summary>
        /// Public Groups → Your Pets. Live 20260731-085057 uses type 35 with
        /// short Unk1, string Text, and short Unk2.
        /// </summary>
        public static byte[] CreatePet(string message, int unk1, int unk2)
        {
            PacketWriter writer = new PacketWriter((ushort)MessageType.AnonymousMessage);
            writer.WriteUInt16((ushort)unk1);
            writer.WriteString(message ?? string.Empty);
            writer.WriteUInt16((ushort)unk2);
            return writer.Finish();
        }

        public static byte[] Create(string source, string message)
        {
            if (string.IsNullOrEmpty(source))
            {
                return Create(message);
            }

            if (string.IsNullOrEmpty(message))
            {
                return Create(source);
            }

            return Create(source + ": " + message);
        }

        #endregion
    }
}
