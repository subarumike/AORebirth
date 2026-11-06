#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace ChatEngine.PacketHandlers
{
    #region Usings ...

    using System;

    using ChatEngine.Channels;
    using ChatEngine.CoreClient;
    using ChatEngine.Packets;

    #endregion

    /// <summary>
    /// The channel message.
    /// </summary>
    internal static class ChannelMessage
    {
        #region Public Methods and Operators

        /// <summary>
        /// The read.
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <param name="packet">
        /// </param>
        public static void Read(Client client, byte[] packet)
        {
            byte[] senderId = BitConverter.GetBytes(client.Character.CharacterId);
            Array.Reverse(senderId);

            ChannelBase channel = client.ChatServer().GetChannel(packet);

            // Team/raid channels are membership-only: a stale Team tab must not still reach the team. A ChatChannels.json
            // channel is too, or a character failing its join conditions could still post to it by its id.
            if (channel == null
                || ((channel.channelType == ChannelType.Team || channel.channelType == ChannelType.Raid
                        || channel is ConfiguredChannel)
                    && !client.Channels.Contains(channel)))
            {
                return;
            }

            PacketReader reader = new PacketReader(ref packet);
            reader.ReadUInt16();
            reader.ReadUInt16();
            reader.ReadUInt16();
            reader.ReadUInt16();
            reader.ReadByte();
            string text = reader.ReadString();
            string channelName = channel.ChannelName;

            // Check if it is a Chat Command (starting with a dot)
            if (!ProcessServerCommand(text, client))
            {
                byte[] newpacket = InsertSender(packet, senderId, ChatLineFlags.IsGm(client));

                foreach (Client recipient in client.ChatServer().ConnectedClients.Values)
                {
                    if (recipient.Channels.Contains(channel))
                    {
                        if (!recipient.KnownClients.Contains(client.Character.CharacterId)
                            && (recipient.Character.CharacterId != client.Character.CharacterId))
                        {
                            byte[] pname = PlayerName.Create(client, client.Character.CharacterId);
                            recipient.Send(pname);
                            recipient.KnownClients.Add(client.Character.CharacterId);
                        }

                        recipient.Send(newpacket);
                    }
                }

                if (client.ChatServer().BotRouter != null)
                {
                    client.ChatServer().BotRouter.PublishChannelMessage(
                        (byte)channel.channelType,
                        channel.ChannelId,
                        client.Character.CharacterId,
                        client.Character.characterName,
                        text);
                }
            }

            ChatLogger.WriteString(channelName, text, client.Character.characterName);
        }

        #endregion

        #region Methods

        /// <summary>
        /// The client's channel packet (type, length, channel type, channel id, text S, block S) relayed with the sender
        /// id after the channel id. A GM's block gets the line-flags entry 02 01 appended (entries start at the block's
        /// first byte); everyone else's block is relayed as sent.
        /// </summary>
        private static byte[] InsertSender(byte[] packet, byte[] senderId, bool gm)
        {
            const int TextOffset = 9;
            int blockEnd = -1;
            int blockLengthOffset = -1;
            if (gm && packet.Length >= TextOffset + 2)
            {
                blockLengthOffset = TextOffset + 2 + ((packet[TextOffset] << 8) | packet[TextOffset + 1]);
                if (blockLengthOffset + 2 <= packet.Length)
                {
                    blockEnd = blockLengthOffset + 2 + ((packet[blockLengthOffset] << 8) | packet[blockLengthOffset + 1]);
                    if (blockEnd > packet.Length)
                        blockEnd = -1;
                }
            }

            byte[] gmEntry = blockEnd >= 0 ? ChatLineFlags.GmEntry : Array.Empty<byte>();
            byte[] newpacket = new byte[packet.Length + senderId.Length + gmEntry.Length];
            Array.Copy(packet, 0, newpacket, 0, TextOffset);
            Array.Copy(senderId, 0, newpacket, TextOffset, senderId.Length);
            int shift = senderId.Length;
            if (gmEntry.Length == 0)
            {
                Array.Copy(packet, TextOffset, newpacket, TextOffset + shift, packet.Length - TextOffset);
            }
            else
            {
                Array.Copy(packet, TextOffset, newpacket, TextOffset + shift, blockEnd - TextOffset);
                Array.Copy(gmEntry, 0, newpacket, blockEnd + shift, gmEntry.Length);
                Array.Copy(packet, blockEnd, newpacket, blockEnd + shift + gmEntry.Length, packet.Length - blockEnd);
                int blockLength = ((packet[blockLengthOffset] << 8) | packet[blockLengthOffset + 1]) + gmEntry.Length;
                newpacket[blockLengthOffset + shift] = (byte)(blockLength >> 8);
                newpacket[blockLengthOffset + shift + 1] = (byte)blockLength;
            }

            int bodyLength = newpacket.Length - 4;
            newpacket[2] = (byte)(bodyLength >> 8);
            newpacket[3] = (byte)bodyLength;
            return newpacket;
        }

        /// <summary>
        /// </summary>
        /// <param name="text">
        /// </param>
        /// <param name="client">
        /// </param>
        /// <returns>
        /// </returns>
        private static bool ProcessServerCommand(string text, Client client)
        {
            if (text.StartsWith(".") || text.StartsWith("/"))
            {
                ChatEngine.CoreServer.ChatServer.SendToZone((int)client.Character.CharacterId, text);
                return true;
            }

            return false;
        }

        #endregion
    }
}
