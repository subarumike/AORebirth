#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ChatEngine.Packets
{
    /// <summary>
    /// Public Groups → Your Pets (orange, client prepends ": ").
    /// Live 20260731-085057: type 35, Unk1=0, Text="{owner}'s pet, {pet}: {line}", Unk2=1.
    /// AOSharp NpcMessage: short Unk1 + string Text + short Unk2 (NOT SSS "\0" blob).
    /// Trailing 00 01 00 (string "\0") was style 0 → System yellow; use short Unk2=1 only.
    /// GUI.dll chat group: ctch_mypet ("Your Pets").
    /// </summary>
    public static class MsgSystem
    {
        #region Public Methods and Operators

        public static byte[] Create(string message)
        {
            PacketWriter writer = new PacketWriter((ushort)MessageType.AnonymousMessage);

            // Unk1 = 0 (true empty — never WriteString("")).
            writer.WriteUInt16(0);

            // Full live Text.
            writer.WriteString(message ?? string.Empty);

            // Unk2 = 1 (AOSharp short — not length-prefixed "\0").
            writer.WriteUInt16(1);

            return writer.Finish();
        }

        public static byte[] Create(string message, int unk1, int unk2)
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
