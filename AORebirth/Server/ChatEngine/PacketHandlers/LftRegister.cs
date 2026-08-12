namespace ChatEngine.PacketHandlers
{
    using System;

    using ChatEngine.CoreClient;
    using ChatEngine.Lists;

    /// <summary>
    /// Looking for Team register (chat type 0x05DC / 1500).
    /// Uncheck reference: payloadLen=2 Comment: → Unlisted listed=False (remove from all searches).
    /// Capture 20260727-lft-list-search: /lft test → 05DC0006000474657374.
    /// </summary>
    public static class LftRegister
    {
        public static void Read(Client client, byte[] packet)
        {
            PacketReader reader = new PacketReader(ref packet);
            reader.ReadUInt16(); // type
            ushort payloadLen = reader.ReadUInt16();

            // Uncheck: payloadLen=2 is u16be strlen=0 (empty Comment). Also allow length 0.
            string comment = string.Empty;
            if (payloadLen >= 2)
            {
                comment = reader.ReadString() ?? string.Empty;
            }

            reader.Finish();

            // Treat whitespace-only like empty uncheck.
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
