namespace ChatEngine.PacketHandlers
{
    using ChatEngine.CoreClient;
    using ChatEngine.Lists;

    /// <summary>
    /// Looking for Team register (chat type 0x05DC / 1500).
    /// Capture 20260727-104625: payload = u16be strlen + ASCII comment.
    /// </summary>
    public static class LftRegister
    {
        public static void Read(Client client, byte[] packet)
        {
            PacketReader reader = new PacketReader(ref packet);
            reader.ReadUInt16(); // type
            reader.ReadUInt16(); // length
            string comment = reader.ReadString();
            reader.Finish();

            if (client == null || client.Character == null || client.Character.CharacterId == 0)
            {
                return;
            }

            LftRegistry.Upsert(client.Character.CharacterId, comment);

            client.Server.Debug(
                client,
                "{0} >> LftRegister: Comment: {1}",
                client.Character.characterName,
                comment ?? string.Empty);
        }
    }
}
