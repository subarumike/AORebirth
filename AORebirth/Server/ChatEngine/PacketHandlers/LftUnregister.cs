namespace ChatEngine.PacketHandlers
{
    using ChatEngine.CoreClient;
    using ChatEngine.Lists;

    /// <summary>
    /// Looking for Team uncheck (client → server chat type 0x05DD / 1501).
    /// Mike: uncheck sends 05DD (often 05DD0000) — remove from LFT for all searches.
    /// Note: server→client 1501 is also LftQueryReply rows; client→server 1501 is uncheck only.
    /// </summary>
    public static class LftUnregister
    {
        public static void Read(Client client, byte[] packet)
        {
            if (client == null || client.Character == null || client.Character.CharacterId == 0)
            {
                return;
            }

            ushort payloadLen = 0;
            if (packet != null && packet.Length >= 4)
            {
                // type already known as 1501; length is bytes 2-3 big-endian
                payloadLen = (ushort)((packet[2] << 8) | packet[3]);
            }

            bool wasListed = LftRegistry.IsListed(client.Character.CharacterId);

            // Force unlist (sticky). Empty Apply matches clear-description path.
            LftRegistry.ApplyResult result = LftRegistry.Apply(
                client.Character.CharacterId,
                string.Empty,
                client.ChatAuthenticatedUtc);

            bool listed = LftRegistry.IsListed(client.Character.CharacterId);

            client.Server.Debug(
                client,
                "{0} >> LftUnregister(05DD): result={1} wasListed={2} listed={3} payloadLen={4}",
                client.Character.characterName,
                result,
                wasListed,
                listed,
                payloadLen);
        }
    }
}
