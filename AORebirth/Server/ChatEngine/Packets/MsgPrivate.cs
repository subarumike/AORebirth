#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ChatEngine.Packets
{
    /// <summary>
    /// Capture 20260806-063619 PrivateMsg:
    /// type=PrivateMessage(30) Sender=0 Text=… Unk1=3 Unk2=0.
    /// </summary>
    public static class MsgPrivate
    {
        public static byte[] Create(uint senderId, string message, int unk1, int unk2)
        {
            PacketWriter writer = new PacketWriter((ushort)MessageType.PrivateMessage);
            writer.WriteUInt32(senderId);
            writer.WriteString(message ?? string.Empty);
            writer.WriteUInt16((ushort)unk1);
            writer.WriteUInt16((ushort)unk2);
            return writer.Finish();
        }
    }
}
