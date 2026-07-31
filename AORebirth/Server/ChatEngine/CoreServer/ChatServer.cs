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

namespace ChatEngine.CoreServer
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;

    using Cell.Core;

    using AORebirth.Communication.Messages;
    using AORebirth.Database.Dao;

    using ChatEngine.Channels;
    using ChatEngine.CoreClient;
    using ChatEngine.Lists;
    using ChatEngine.Packets;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;
    using Utility.Config;

    #endregion

    /// <summary>
    /// The server.
    /// </summary>
    public class ChatServer : ServerBase
    {
        #region Fields

        /// <summary>
        /// </summary>
        public HashSet<ChannelBase> Channels = new HashSet<ChannelBase>();

        /// <summary>
        /// </summary>
        public Dictionary<uint, Client> ConnectedClients = new Dictionary<uint, Client>();

        /// <summary>
        /// </summary>
        public string MessageOfTheDay = string.Empty;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// </summary>
        public ChatServer()
        {
            // Global channel
            this.Channels.Add(new GlobalChannel(ChannelFlags.None, ChannelType.General, 1, "Global"));

            // Shopping channels (at the moment just level restricted, no sides)
            this.Channels.Add(new LevelRestrictedChannel(1, 1, 50));
            this.Channels.Add(new LevelRestrictedChannel(2, 51, 150));
            this.Channels.Add(new LevelRestrictedChannel(3, 151, 220));

            // Restricted channels (GM, sided channels)
            this.Channels.Add(new RestrictedChannel(Side.Gm, ChannelFlags.None, ChannelType.GM));
            this.Channels.Add(new RestrictedChannel(Side.Clan, ChannelFlags.None, ChannelType.General));
            this.Channels.Add(new RestrictedChannel(Side.Omni, ChannelFlags.None, ChannelType.General));
            this.Channels.Add(new RestrictedChannel(Side.Neutral, ChannelFlags.None, ChannelType.General));

            this.ClientConnected += this.ClientConnectedToChat;
            this.ClientDisconnected += this.OnClientDisconnect;

            // server welcome message
            this.MessageOfTheDay = ConfigReadWrite.Instance.CurrentConfig.Motd;
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <typeparam name="T">
        /// </typeparam>
        /// <returns>
        /// </returns>
        public List<ChannelBase> ChannelsByType<T>()
        {
            return this.Channels.Where(x => x is T).ToList();
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <param name="forced">
        /// </param>
        public void OnClientDisconnect(IClient client, bool forced)
        {
            Client cl = (Client)client;
            if (cl.Character.CharacterId != 0)
            {
                LftRegistry.Remove(cl.Character.CharacterId);
                CharacterDao.Instance.SetOffline((int)cl.Character.CharacterId);
                this.ConnectedClients.Remove(cl.Character.CharacterId);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        internal void AddClientToChannels(Client client)
        {
            // Automatically add client to its appropriate channels
            foreach (ChannelBase channel in this.ChannelsByType<GlobalChannel>())
            {
                channel.AddClient(client);
            }

            foreach (ChannelBase channel in this.ChannelsByType<RestrictedChannel>())
            {
                channel.AddClient(client);
            }

            foreach (ChannelBase channel in this.ChannelsByType<LevelRestrictedChannel>())
            {
                channel.AddClient(client);
            }

            foreach (ChannelBase channel in this.ChannelsByType<TeamChannel>())
            {
                channel.AddClient(client);
            }

            foreach (ChannelBase channel in this.ChannelsByType<OrganizationChannel>())
            {
                channel.AddClient(client);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="packet">
        /// </param>
        /// <returns>
        /// </returns>
        internal ChannelBase GetChannel(byte[] packet)
        {
            byte channelType = packet[4];
            uint chanid = (uint)IPAddress.NetworkToHostOrder((int)BitConverter.ToUInt32(packet, 5));

            foreach (ChannelBase ce in this.Channels)
            {
                if ((ce.ChannelId == chanid) && ((byte)ce.channelType == channelType))
                {
                    return ce;
                }
            }

            return null;
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <param name="messageObject">
        /// </param>
        internal void ISComDataReceived(object sender, DynamicMessage messageObject)
        {
            if (messageObject == null)
            {
                return;
            }

            try
            {
                var ping = messageObject.DataObject as Ping;
                if (ping != null)
                {
                    // Zone keepalive — no client delivery.
                    return;
                }

                var message = messageObject.DataObject as VicinityChatMessage;
                if (message != null)
                {
                    this.DistributeVicinityChat(message);
                    return;
                }

                var systemChat = messageObject.DataObject as SystemChatMessage;
                if (systemChat != null)
                {
                    // Owner-only pet SystemMessage (CharacterId match). Not playfield broadcast.
                    this.DistributeSystemChat(systemChat);
                    return;
                }

                var chatCommand = messageObject.DataObject as ChatCommand;
                if (chatCommand != null)
                {
                    this.HandleZoneChatCommand(chatCommand);
                    return;
                }

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "ISComDataReceived: unhandled type="
                    + (messageObject.TypeName ?? (messageObject.DataObject == null
                                                      ? "null"
                                                      : messageObject.DataObject.GetType().FullName)));
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "ISComDataReceived failed type="
                    + (messageObject.TypeName ?? "?")
                    + " err="
                    + e.Message);
                LogUtil.ErrorException(e);
            }
        }

        private void HandleZoneChatCommand(ChatCommand chatCommand)
        {
            if (chatCommand == null || string.IsNullOrWhiteSpace(chatCommand.ChatCommandString))
            {
                return;
            }

            string text = chatCommand.ChatCommandString.Trim();
            if (text.StartsWith(LftPlayfieldRegistry.PlayfieldCommandPrefix, StringComparison.Ordinal))
            {
                string[] parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int playfieldId;
                if (parts.Length >= 2 && int.TryParse(parts[1], out playfieldId) && playfieldId > 0)
                {
                    LftPlayfieldRegistry.Set(
                        unchecked((uint)chatCommand.CharacterId),
                        playfieldId);
                }
            }
        }

        /// <summary>
        /// The on client connected.
        /// </summary>
        /// <param name="client">
        /// </param>
        protected void ClientConnectedToChat(IClient client)
        {
            Client client1 = (Client)client;

            byte[] welcomePacket = new byte[]
                                   {
                                       0x00, 0x00, 0x00, 0x22, 0x00, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
                                       // Server Salt (32 Bytes)
                                       0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                       0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
                                   };

            byte[] salt = new byte[0x20];
            Random rand = new Random();

            rand.NextBytes(salt);

            client1.ServerSalt = string.Empty;

            for (int i = 0; i < 32; i++)
            {
                // 0x00 Breaks Things
                if (salt[i] == 0)
                {
                    salt[i] = 42; // So we change it to something nicer
                }

                welcomePacket[6 + i] = salt[i];

                client1.ServerSalt += string.Format("{0:x2}", salt[i]);
            }

            client1.Send(welcomePacket);
        }

        /// <summary>
        /// The create client.
        /// </summary>
        /// <returns>
        /// </returns>
        protected override IClient CreateClient(IPAddress address)
        {
            return new Client(this);
        }

        /// <summary>
        /// The on receive udp.
        /// </summary>
        /// <param name="num_bytes">
        /// </param>
        /// <param name="buf">
        /// </param>
        /// <param name="ip">
        /// </param>
        protected override void OnReceiveUDP(int num_bytes, byte[] buf, IPEndPoint ip)
        {
        }

        /// <summary>
        /// The on send to.
        /// </summary>
        /// <param name="clientIP">
        /// </param>
        /// <param name="num_bytes">
        /// </param>
        protected override void OnSendTo(IPEndPoint clientIP, int num_bytes)
        {
        }

        /// <summary>
        /// Owner-only brown pet announce on the owner's chat client.
        /// Capture 20260731-054922: AOSharp NpcMessage type=35 Unk1=0 Text=… Unk2=1.
        /// CharacterId match only — never Vicinity (34) / never playfield broadcast.
        /// </summary>
        /// <param name="systemChatMessage">
        /// </param>
        private void DistributeSystemChat(SystemChatMessage systemChatMessage)
        {
            if (systemChatMessage == null || string.IsNullOrEmpty(systemChatMessage.Text))
            {
                return;
            }

            // Type 35 NpcMessage (AOSharp SystemMessage). Live capture Unk1=0 Unk2=1.
            // Type 36 SimpleSystemMessage was wrong — client showed it under Vicinity.
            byte[] packet = MsgSystem.Create(
                systemChatMessage.Text,
                systemChatMessage.Unk1,
                systemChatMessage.Unk2 == 0 ? 1 : systemChatMessage.Unk2);
            if (packet == null || packet.Length < 2 || packet[0] != 0x00 || packet[1] != 0x23)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "DistributeSystemChat refused non-type-35 packet (need 0023 NpcMessage, not Vicinity 0022 / SimpleSystem 0024)");
                return;
            }

            string hexPrefix = "????";
            if (packet.Length > 0)
            {
                int take = Math.Min(16, packet.Length);
                var sb = new System.Text.StringBuilder(take * 2);
                for (int i = 0; i < take; i++)
                {
                    sb.AppendFormat(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "{0:X2}",
                        packet[i]);
                }

                hexPrefix = sb.ToString();
            }

            Client cli;
            uint characterId = unchecked((uint)systemChatMessage.CharacterId);
            if (this.ConnectedClients.TryGetValue(characterId, out cli)
                && cli != null
                && cli.Character != null)
            {
                cli.Send(packet);
                string ok = "DistributeSystemChat ok key="
                            + characterId
                            + " wire="
                            + hexPrefix
                            + " len="
                            + (packet == null ? 0 : packet.Length)
                            + " text="
                            + systemChatMessage.Text;
                LogUtil.Debug(DebugInfoDetail.Error, ok);
                Console.WriteLine(ok);
                return;
            }

            string wantName = systemChatMessage.CharacterName ?? string.Empty;
            foreach (Client connected in this.ConnectedClients.Values)
            {
                if (connected == null || connected.Character == null)
                {
                    continue;
                }

                if (connected.Character.CharacterId == characterId)
                {
                    connected.Send(packet);
                    string ok = "DistributeSystemChat ok scanId="
                                + characterId
                                + " wire="
                                + hexPrefix
                                + " len="
                                + (packet == null ? 0 : packet.Length)
                                + " text="
                                + systemChatMessage.Text;
                    LogUtil.Debug(DebugInfoDetail.Error, ok);
                    Console.WriteLine(ok);
                    return;
                }

                if (wantName.Length > 0
                    && string.Equals(
                        connected.Character.characterName,
                        wantName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    connected.Send(packet);
                    string ok = "DistributeSystemChat ok name="
                                + wantName
                                + " wire="
                                + hexPrefix
                                + " len="
                                + (packet == null ? 0 : packet.Length)
                                + " text="
                                + systemChatMessage.Text;
                    LogUtil.Debug(DebugInfoDetail.Error, ok);
                    Console.WriteLine(ok);
                    return;
                }
            }

            string miss = "DistributeSystemChat: no connected chat client for CharacterId="
                          + systemChatMessage.CharacterId
                          + " name="
                          + wantName
                          + " clients="
                          + this.ConnectedClients.Count;
            LogUtil.Debug(DebugInfoDetail.Error, miss);
            Console.WriteLine(miss);
        }

        private void DistributeVicinityChat(VicinityChatMessage vicinityChatMessage)
        {
            byte[] packet = MsgVicinity.Create(
                (uint)vicinityChatMessage.SenderId,
                vicinityChatMessage.Text,
                (byte)vicinityChatMessage.MessageType);

            string lookup = CharacterDao.Instance.GetCharacterNameById(vicinityChatMessage.SenderId);
            byte[] nameLookup = NameLookupResult.Create((uint)vicinityChatMessage.SenderId, lookup);

            foreach (int charId in vicinityChatMessage.CharacterIds)
            {
                foreach (Client cli in this.ConnectedClients.Values)
                {
                    if (cli.Character.CharacterId == charId)
                    {
                        if (!cli.KnownClients.Contains((uint)vicinityChatMessage.SenderId))
                        {
                            // Name lookup
                            cli.Send(nameLookup);
                            cli.KnownClients.Add((uint)vicinityChatMessage.SenderId);
                        }

                        cli.Send(packet);
                    }
                }
            }
        }

        #endregion
    }
}
