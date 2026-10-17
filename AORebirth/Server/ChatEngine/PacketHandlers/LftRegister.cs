namespace ChatEngine.PacketHandlers
{
    using System;

    using ChatEngine.CoreClient;
    using ChatEngine.Lists;

    /// <summary>
    /// Looking for Team register (chat type 0x05DC / 1500).
    /// Ticking the box sends 05DD then 05DC; an empty comment (payloadLen=2) still lists.
    /// Capture 20260727-lft-list-search: /lft test → 05DC0006000474657374.
    /// </summary>
    public static class LftRegister
    {
        public static void Read(Client client, byte[] packet)
        {
            PacketReader reader = new PacketReader(ref packet);
            reader.ReadUInt16(); // type
            ushort payloadLen = reader.ReadUInt16();

            // payloadLen=2 is u16be strlen=0 (no comment). Also allow length 0.
            string comment = string.Empty;
            if (payloadLen >= 2)
            {
                comment = reader.ReadString() ?? string.Empty;
            }

            reader.Finish();

            // Whitespace-only is no comment.
            if (string.IsNullOrWhiteSpace(comment))
            {
                comment = string.Empty;
            }

            if (client == null || client.Character == null || client.Character.CharacterId == 0)
            {
                return;
            }

            LftRegistry.ApplyResult result = LftRegistry.Apply(
                client.Character.CharacterId,
                comment,
                client.ChatAuthenticatedUtc);

            client.Server.Debug(
                client,
                "{0} >> LftRegister: result={1} listed={2} payloadLen={3} Comment: {4}",
                client.Character.characterName,
                result,
                LftRegistry.IsListed(client.Character.CharacterId),
                payloadLen,
                comment ?? string.Empty);
        }
    }
}
