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
    using System.Text;

    using Cell.Core;

    using AORebirth.Communication.Messages;
    using AORebirth.Database.Dao;

    using ChatEngine.Channels;
    using ChatEngine.BotIntegration;
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
        private readonly ChatSessionOwnership<Client> clientOwnership = new ChatSessionOwnership<Client>();

        private readonly object privateGroupReconnectSync = new object();

        /// <summary>A bot's private group members, kept while the bot is away so they are re-invited when it returns.</summary>
        private readonly Dictionary<uint, HashSet<uint>> privateGroupReconnectMembers =
            new Dictionary<uint, HashSet<uint>>();

        public IReadOnlyDictionary<uint, Client> ConnectedClients { get { return this.clientOwnership.Clients; } }

        /// <summary>
        /// The "Org Msg" system channel as retail lists it: type 10, id 1, flags 0x8006. Org members join it at login with
        /// their organization channel; it is not in <see cref="Channels"/>, so nobody can speak in it.
        /// </summary>
        private readonly ChannelBase orgMsgChannel = new ChannelBase((ChannelFlags)0x8006, ChannelType.OrgMsg, 1, "Org Msg");

        /// <summary>
        /// </summary>
        public string MessageOfTheDay = string.Empty;

        /// <summary>
        /// Optional first-party bot adapter. It is distinct from normal chat clients and zone traffic through World.
        /// </summary>
        internal ChatEngineBotRouter BotRouter;

        /// <summary>
        /// Wire id of the Global channel (type General). Distinct from every Side value.
        /// </summary>
        internal const uint GlobalChannelId = 100;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// </summary>
        public ChatServer()
        {
            // Global channel. Its id must stay outside the Side range (0-7): the sided
            // General channels below are keyed (General, (uint)Side), so id 1 collided with Clan.
            this.Channels.Add(new GlobalChannel(ChannelFlags.None, ChannelType.General, GlobalChannelId, "Global"));

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

            // A reconnect is a new Client; drop the old one from its channels.
            foreach (ChannelBase channel in cl.Channels.ToArray())
            {
                channel.RemoveClient(cl);
            }

            cl.Channels.Clear();
            uint characterId = cl.Character.CharacterId;
            bool released = this.clientOwnership.Disconnect(characterId, cl, ClearOnlineIfUnowned, LftRegistry.Remove);

            // Buddy and private-group notices go out after the ownership lock is released: they send packets to other
            // clients and must not hold every other login and logout up while they do.
            if (released)
            {
                this.NotifyBuddyStatus(characterId, 0);
                this.RemoveClientFromPrivateGroup(cl);
            }
        }

        /// <summary>
        /// Login: sends the character its buddy list (names and online state), caches it on the client, and tells every
        /// connected player who has this character as a buddy that it is online.
        /// </summary>
        internal void InitializeBuddyPresence(Client client)
        {
            if (client == null || client.Character == null)
            {
                return;
            }

            List<int> buddyIds;
            try
            {
                buddyIds = BuddyList.LoadBuddyList(unchecked((int)client.Character.CharacterId));
            }
            catch (Exception exception)
            {
                LogUtil.Debug(DebugInfoDetail.Error,
                    string.Format("Could not load buddy list for character {0}: {1}",
                        client.Character.CharacterId, exception));
                return;
            }

            client.SetBuddies(buddyIds);
            foreach (int buddyId in buddyIds)
            {
                if (buddyId <= 0)
                {
                    continue;
                }

                try
                {
                    string buddyName = CharacterDao.Instance.GetCharacterNameById(buddyId);
                    if (string.IsNullOrWhiteSpace(buddyName))
                    {
                        continue;
                    }

                    uint buddyCharacterId = unchecked((uint)buddyId);
                    client.Send(PlayerName.Create(buddyCharacterId, buddyName));
                    if (!client.KnownClients.Contains(buddyCharacterId))
                    {
                        client.KnownClients.Add(buddyCharacterId);
                    }

                    uint online = this.ConnectedClients.ContainsKey(buddyCharacterId)
                        || CharacterDao.Instance.IsOnline(buddyId) > 0
                            ? 1u
                            : 0u;
                    client.Send(BuddyOnlineStatus.Create(
                        buddyCharacterId, online, client.GetBuddyStatusFlags(buddyCharacterId)));
                }
                catch (Exception exception)
                {
                    LogUtil.Debug(DebugInfoDetail.Error,
                        string.Format("Could not restore buddy {0} for character {1}: {2}",
                            buddyId, client.Character.CharacterId, exception));
                }
            }

            this.NotifyBuddyStatus(client.Character.CharacterId, 1);
        }

        /// <summary>Tells every connected player with <paramref name="characterId"/> as a buddy its online state.</summary>
        private void NotifyBuddyStatus(uint characterId, uint online)
        {
            foreach (Client recipient in this.ConnectedClients.Values.ToArray())
            {
                if (recipient == null || recipient.Character == null || !recipient.HasBuddy(characterId))
                {
                    continue;
                }

                try
                {
                    recipient.Send(BuddyOnlineStatus.Create(
                        characterId, online, recipient.GetBuddyStatusFlags(characterId)));
                }
                catch (Exception exception)
                {
                    LogUtil.Debug(DebugInfoDetail.Error,
                        string.Format("Could not notify buddy status for character {0} to {1}: {2}",
                            characterId, recipient.Character.characterName, exception));
                }
            }
        }

        /// <summary>
        /// Login: a bot re-invites the members its private group had when it left; a player is re-invited to every
        /// connected bot's group it was in.
        /// </summary>
        internal void RestorePrivateGroupInvitations(Client client)
        {
            if (client == null || client.Character == null)
            {
                return;
            }

            uint characterId = client.Character.CharacterId;
            if (client.IsBot)
            {
                uint[] previousMembers;
                lock (this.privateGroupReconnectSync)
                {
                    HashSet<uint> memberIds;
                    if (!this.privateGroupReconnectMembers.TryGetValue(characterId, out memberIds))
                    {
                        return;
                    }

                    previousMembers = memberIds.ToArray();
                }

                client.PrivateGroupId = characterId;
                foreach (uint memberId in previousMembers)
                {
                    this.InvitePrivateGroupPlayer(client, memberId);
                }

                return;
            }

            uint[] groupIds;
            lock (this.privateGroupReconnectSync)
            {
                groupIds = this.privateGroupReconnectMembers
                    .Where(group => group.Value.Contains(characterId))
                    .Select(group => group.Key)
                    .ToArray();
            }

            foreach (uint groupId in groupIds)
            {
                Client bot;
                if (this.ConnectedClients.TryGetValue(groupId, out bot) && bot != null && bot.IsBot)
                {
                    this.InvitePrivateGroupPlayer(bot, characterId);
                }
            }
        }

        /// <summary>
        /// Invites <paramref name="invitedPlayerId"/> to the inviter's own private group (its id is the inviter's). A
        /// member of someone else's group cannot invite into it, and while it sits in another group it hosts none.
        /// </summary>
        internal void InvitePrivateGroupPlayer(Client inviter, uint invitedPlayerId)
        {
            Client invitee;
            if (inviter.Character == null
                || inviter.Character.CharacterId == 0
                || invitedPlayerId == 0
                || !this.ConnectedClients.TryGetValue(invitedPlayerId, out invitee)
                || invitee == null)
            {
                inviter.Send(PlayerIdUnknown.Create(invitedPlayerId));
                return;
            }

            uint privateGroupId = inviter.Character.CharacterId;
            if (inviter.PrivateGroupId != 0 && inviter.PrivateGroupId != privateGroupId)
            {
                return;
            }

            inviter.PrivateGroupId = privateGroupId;
            if (inviter.IsBot)
            {
                this.RememberPrivateGroupMember(privateGroupId, invitedPlayerId);
            }

            if (invitee.PrivateGroupId == privateGroupId)
            {
                return;
            }

            this.SendKnownClient(invitee, inviter);
            invitee.AddPrivateGroupInvitation(privateGroupId);
            invitee.Send(PrivateGroupInvitation.Create(privateGroupId));
        }

        /// <summary>Joins a private group the client was invited to (or its own).</summary>
        internal void JoinPrivateGroup(Client joiningClient, uint privateGroupId)
        {
            if (joiningClient.Character == null || privateGroupId == 0)
            {
                return;
            }

            uint joiningPlayerId = joiningClient.Character.CharacterId;
            if (joiningClient.PrivateGroupId == privateGroupId)
            {
                joiningClient.ConsumePrivateGroupInvitation(privateGroupId);
                return;
            }

            bool groupIsOnline = this.ConnectedClients.Values.Any(
                connected => connected != null && connected.PrivateGroupId == privateGroupId);
            bool isGroupOwner = joiningPlayerId == privateGroupId;
            if (!groupIsOnline && !isGroupOwner)
            {
                joiningClient.Send(PlayerIdUnknown.Create(privateGroupId));
                return;
            }

            if (!isGroupOwner && !joiningClient.ConsumePrivateGroupInvitation(privateGroupId))
            {
                return;
            }

            if (joiningClient.PrivateGroupId != 0)
            {
                this.LeavePrivateGroup(joiningClient, joiningClient.PrivateGroupId);
            }

            Client[] existingMembers = this.ConnectedClients.Values
                .Where(connected => connected != null
                    && connected != joiningClient
                    && connected.PrivateGroupId == privateGroupId)
                .ToArray();

            joiningClient.PrivateGroupId = privateGroupId;

            Client groupOwner;
            if (this.ConnectedClients.TryGetValue(privateGroupId, out groupOwner)
                && groupOwner != null
                && groupOwner.IsBot)
            {
                this.RememberPrivateGroupMember(privateGroupId, joiningPlayerId);
            }

            foreach (Client member in existingMembers)
            {
                this.SendKnownClient(joiningClient, member);
                joiningClient.Send(PrivateGroupPlayerJoined.Create(privateGroupId, member.Character.CharacterId));
            }

            byte[] joinedPacket = PrivateGroupPlayerJoined.Create(privateGroupId, joiningPlayerId);
            foreach (Client member in existingMembers)
            {
                this.SendKnownClient(member, joiningClient);
                member.Send(joinedPacket);
            }

            joiningClient.Send(joinedPacket);
        }

        internal void LeavePrivateGroup(Client leavingClient, uint privateGroupId)
        {
            if (leavingClient.Character == null || privateGroupId == 0
                || leavingClient.PrivateGroupId != privateGroupId)
            {
                return;
            }

            leavingClient.PrivateGroupId = 0;
            this.ForgetPrivateGroupMember(privateGroupId, leavingClient.Character.CharacterId);
            this.SendToPrivateGroup(privateGroupId,
                PrivateGroupPlayerLeft.Create(privateGroupId, leavingClient.Character.CharacterId), leavingClient);
        }

        internal void SendPrivateGroupMessage(Client sender, uint privateGroupId, string message)
        {
            if (sender.Character == null || sender.PrivateGroupId != privateGroupId)
            {
                return;
            }

            this.SendToPrivateGroup(privateGroupId,
                PrivateGroupMessage.Create(privateGroupId, sender.Character.CharacterId, message, string.Empty), null);
        }

        /// <summary>The group owner removes one member.</summary>
        internal void KickPrivateGroupPlayer(Client kicker, uint kickedPlayerId)
        {
            uint privateGroupId = kicker.PrivateGroupId;
            Client kickedClient;
            if (privateGroupId == 0
                || kicker.Character == null
                || kicker.Character.CharacterId != privateGroupId
                || !this.ConnectedClients.TryGetValue(kickedPlayerId, out kickedClient)
                || kickedClient == null
                || kickedClient.PrivateGroupId != privateGroupId)
            {
                return;
            }

            kickedClient.PrivateGroupId = 0;
            this.ForgetPrivateGroupMember(privateGroupId, kickedPlayerId);
            kickedClient.Send(PrivateGroupKicked.Create(privateGroupId));
            this.SendToPrivateGroup(privateGroupId, PrivateGroupPlayerLeft.Create(privateGroupId, kickedPlayerId), kickedClient);
        }

        /// <summary>The group owner removes everyone else.</summary>
        internal void KickEveryoneFromPrivateGroup(Client kicker)
        {
            uint privateGroupId = kicker.PrivateGroupId;
            if (privateGroupId == 0
                || kicker.Character == null
                || kicker.Character.CharacterId != privateGroupId)
            {
                return;
            }

            foreach (Client member in this.PrivateGroupMembers(privateGroupId, kicker))
            {
                member.PrivateGroupId = 0;
                member.Send(PrivateGroupKicked.Create(privateGroupId));
            }

            lock (this.privateGroupReconnectSync)
            {
                this.privateGroupReconnectMembers.Remove(privateGroupId);
            }
        }

        /// <summary>
        /// A disconnecting client leaves its group. A bot that owns its group kicks the members but remembers them, to
        /// re-invite on its return; anyone else just leaves.
        /// </summary>
        private void RemoveClientFromPrivateGroup(Client client)
        {
            if (client == null || client.PrivateGroupId == 0 || client.Character == null)
            {
                return;
            }

            uint privateGroupId = client.PrivateGroupId;
            client.PrivateGroupId = 0;

            if (client.IsBot && client.Character.CharacterId == privateGroupId)
            {
                foreach (Client member in this.PrivateGroupMembers(privateGroupId, client))
                {
                    this.RememberPrivateGroupMember(privateGroupId, member.Character.CharacterId);
                    member.PrivateGroupId = 0;
                    member.Send(PrivateGroupKicked.Create(privateGroupId));
                }

                return;
            }

            this.ForgetPrivateGroupMember(privateGroupId, client.Character.CharacterId);
            this.SendToPrivateGroup(privateGroupId,
                PrivateGroupPlayerLeft.Create(privateGroupId, client.Character.CharacterId), client);
        }

        private Client[] PrivateGroupMembers(uint privateGroupId, Client except)
            => this.ConnectedClients.Values
                .Where(connected => connected != null && connected != except && connected.PrivateGroupId == privateGroupId)
                .ToArray();

        private void SendToPrivateGroup(uint privateGroupId, byte[] packet, Client except)
        {
            foreach (Client member in this.PrivateGroupMembers(privateGroupId, except))
            {
                member.Send(packet);
            }
        }

        private void RememberPrivateGroupMember(uint privateGroupId, uint memberId)
        {
            if (privateGroupId == 0 || memberId == 0 || memberId == privateGroupId)
            {
                return;
            }

            lock (this.privateGroupReconnectSync)
            {
                HashSet<uint> memberIds;
                if (!this.privateGroupReconnectMembers.TryGetValue(privateGroupId, out memberIds))
                {
                    memberIds = new HashSet<uint>();
                    this.privateGroupReconnectMembers.Add(privateGroupId, memberIds);
                }

                memberIds.Add(memberId);
            }
        }

        private void ForgetPrivateGroupMember(uint privateGroupId, uint memberId)
        {
            lock (this.privateGroupReconnectSync)
            {
                HashSet<uint> memberIds;
                if (this.privateGroupReconnectMembers.TryGetValue(privateGroupId, out memberIds))
                {
                    memberIds.Remove(memberId);
                    if (memberIds.Count == 0)
                    {
                        this.privateGroupReconnectMembers.Remove(privateGroupId);
                    }
                }
            }
        }

        private void SendKnownClient(Client receiver, Client subject)
        {
            uint characterId = subject.Character.CharacterId;
            if (receiver == subject || receiver.KnownClients.Contains(characterId))
            {
                return;
            }

            receiver.Send(PlayerName.Create(subject, characterId));
            receiver.KnownClients.Add(characterId);
        }

        /// <summary>The World link; set at startup before the chat listener accepts clients.</summary>
        internal static AORebirth.World.Contracts.WorldServiceLink World;

        internal bool RegisterClient(Client client)
        {
            Func<IDisposable> acquireOwnership = client.IsBot
                ? (Func<IDisposable>)(() => AcquireBotOwnership(unchecked((int)client.Character.CharacterId)))
                : null;
            return this.clientOwnership.Register(client.Character.CharacterId, client, acquireOwnership);
        }

        /// <summary>A bot is online through chat alone: it holds its own World lease and the Online flag.</summary>
        private static IDisposable AcquireBotOwnership(int characterId)
        {
            string reason;
            if (!World.TryAcquire(characterId, out reason))
            {
                throw new InvalidOperationException("World refused the bot character: " + reason);
            }

            try
            {
                CharacterDao.Instance.SetOnline(characterId);
            }
            catch
            {
                World.Release(characterId);
                throw;
            }

            return new BotOwnership(characterId);
        }

        /// <summary>Clears Online after a chat client leaves, unless a zone or another holder still owns it.</summary>
        private static void ClearOnlineIfUnowned(uint characterId)
        {
            int id = unchecked((int)characterId);
            if (World.Owned(new[] { id }).Contains(id))
            {
                return;
            }

            CharacterDao.Instance.SetOffline(id);
        }

        private sealed class BotOwnership : IDisposable
        {
            private readonly int characterId;
            private int disposed;

            public BotOwnership(int characterId)
            {
                this.characterId = characterId;
            }

            public void Dispose()
            {
                if (System.Threading.Interlocked.Exchange(ref this.disposed, 1) == 0)
                {
                    World.Release(this.characterId);
                }
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

            // Team and raid channels are joined only on the zone's #aorebirth-team-join / raid-convert for that
            // character (TeamChatBridge); joining every existing team channel here put each new login into every
            // team's chat.

            int characterId = (int)client.Character.CharacterId;
            DBStats orgStat = StatDao.Instance.GetById(50000, characterId, 5);
            int orgId = orgStat.StatValue;

            if (orgId > 0)
            {
                var org = OrganizationDao.Instance.Get(orgId);
                if (org != null)
                {
                    ChannelBase orgChannel;
                    lock (this.Channels)
                    {
                        orgChannel = this.Channels.FirstOrDefault(
                            x => x is OrganizationChannel && x.ChannelId == (uint)orgId);
                        if (orgChannel == null)
                        {
                            orgChannel = new OrganizationChannel(orgId);
                            this.Channels.Add(orgChannel);
                        }
                    }

                    orgChannel.AddClient(client);
                    this.orgMsgChannel.AddClient(client);
                }
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
        /// <summary>Zone traffic through World: vicinity chat to deliver, and commands about a character.</summary>
        internal void OnWorldMessage(AORebirth.World.Contracts.V1.WorldToService message)
        {
            if (message == null || message.BodyCase != AORebirth.World.Contracts.V1.WorldToService.BodyOneofCase.Chat)
            {
                return;
            }

            AORebirth.World.Contracts.V1.ChatEvent chat = message.Chat;
            try
            {
                switch (chat.BodyCase)
                {
                    case AORebirth.World.Contracts.V1.ChatEvent.BodyOneofCase.Vicinity when chat.Vicinity.Anonymous:
                        this.DistributeAnonymousVicinity(chat.Vicinity.Recipients, chat.Vicinity.Text,
                            chat.Vicinity.Payload.ToByteArray());
                        break;
                    case AORebirth.World.Contracts.V1.ChatEvent.BodyOneofCase.Vicinity:
                        this.DistributeVicinityChat(new VicinityChatMessage
                        {
                            CharacterIds = chat.Vicinity.Recipients.ToList(),
                            MessageType = (byte)chat.Vicinity.ChannelType,
                            Text = chat.Vicinity.Text,
                            SenderId = chat.Vicinity.Sender,
                            SenderName = string.IsNullOrEmpty(chat.Vicinity.SenderName) ? null : chat.Vicinity.SenderName
                        });
                        break;
                    case AORebirth.World.Contracts.V1.ChatEvent.BodyOneofCase.ToCharacter:
                        this.HandleZoneChatCommand(new ChatCommand
                        {
                            CharacterId = chat.ToCharacter.CharacterId,
                            ChatCommandString = chat.ToCharacter.Command
                        });
                        break;
                    case AORebirth.World.Contracts.V1.ChatEvent.BodyOneofCase.Organization:
                        this.SendOrganizationMessage(chat.Organization.OrganizationId, chat.Organization.Text);
                        break;
                }
            }
            catch (Exception e)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "World chat event failed type=" + chat.BodyCase + " err=" + e.Message);
                LogUtil.ErrorException(e);
            }
        }

        /// <summary>Delivers the captured server notice through the existing channel named "Org Msg".</summary>
        internal void SendOrganizationMessage(int organizationId, string text)
        {
            if (organizationId <= 0 || string.IsNullOrEmpty(text))
                return;

            ChannelBase channel = this.Channels.FirstOrDefault(
                x => x is OrganizationChannel && x.ChannelId == (uint)organizationId);
            channel?.BotMessage(0, "Org Msg", text, ChannelType.OrgMsg, 1);
        }

        /// <summary>A command for the zone instance that runs <paramref name="characterId"/> (World routes it).</summary>
        internal static void SendToZone(int characterId, string command)
        {
            AORebirth.World.Contracts.WorldServiceLink world = World;
            if (world == null || !world.IsConnected || string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            var chat = new AORebirth.World.Contracts.V1.ChatEvent
            {
                ToCharacter = new AORebirth.World.Contracts.V1.CharacterChatCommand { CharacterId = characterId, Command = command }
            };
            // On the registry stream: commands for a character reach its zone in the order they were sent.
            if (!world.Publish(chat))
            {
                LogUtil.Debug(DebugInfoDetail.Engine, "Zone command not sent: World is unreachable");
            }
        }

        private void HandleZoneChatCommand(ChatCommand chatCommand)
        {
            if (chatCommand == null || string.IsNullOrWhiteSpace(chatCommand.ChatCommandString))
            {
                return;
            }

            string text = chatCommand.ChatCommandString.Trim();
            if (TeamChatBridge.TryHandle(this, chatCommand))
            {
                return;
            }

            if (text.StartsWith(LftPlayfieldRegistry.PlayfieldCommandPrefix, StringComparison.Ordinal))
            {
                string[] parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int playfieldId;
                int expansion;

                if (parts.Length >= 3
                    && int.TryParse(parts[1], out playfieldId)
                    && playfieldId > 0
                    && int.TryParse(parts[2], out expansion)
                    && (expansion == 1 || expansion == 2))
                {
                    LftPlayfieldRegistry.Set(
                        unchecked((uint)chatCommand.CharacterId),
                        playfieldId,
                        expansion);
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

            Random rand = new Random();
            string wireServerSalt = CreateWireServerSalt(rand);

            client1.ServerSalt = FormatServerSalt(Encoding.ASCII.GetBytes(wireServerSalt));
            client1.Send(AuthenticationSeed.Create(wireServerSalt));
        }

        internal static string FormatServerSalt(byte[] salt)
        {
            if (salt == null)
            {
                throw new ArgumentNullException("salt");
            }

            var builder = new StringBuilder(salt.Length * 2);
            for (int i = 0; i < salt.Length; i++)
            {
                byte current = salt[i];
                if (current == 0)
                {
                    current = 42;
                }

                builder.AppendFormat("{0:x2}", current);
            }

            return builder.ToString();
        }

        internal static string CreateWireServerSalt(Random rand)
        {
            if (rand == null)
            {
                throw new ArgumentNullException("rand");
            }

            const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            char[] salt = new char[0x20];
            for (int i = 0; i < salt.Length; i++)
            {
                salt[i] = alphabet[rand.Next(alphabet.Length)];
            }

            return new string(salt);
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

            // Your Pets (ctch_mypet): live Unk1=0 + Text + Unk2=1 (capture 20260731-085057).
            // Client Subscribe Channels → Public Groups → Your Pets gates display.
            string source = systemChatMessage.Source ?? string.Empty;
            string body = systemChatMessage.Text ?? string.Empty;
            if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(body))
            {
                body = source + ": " + body;
            }
            else if (string.IsNullOrEmpty(body))
            {
                body = source;
            }

            int unk1 = systemChatMessage.Unk1;
            int unk2 = systemChatMessage.Unk2 != 0 ? systemChatMessage.Unk2 : 1;
            byte[] packet = MsgSystem.CreatePet(body, unk1, unk2);
            if (packet == null || packet.Length < 2 || packet[0] != 0x00 || packet[1] != 0x23)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "DistributeSystemChat refused non-YourPets packet (need type 35 / 0023)");
                return;
            }

            string hexPrefix = "????";
            if (packet.Length > 0)
            {
                int take = Math.Min(32, packet.Length);
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
                LogUtil.Debug(DebugInfoDetail.Network, ok);
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
                    LogUtil.Debug(DebugInfoDetail.Network, ok);
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
                    LogUtil.Debug(DebugInfoDetail.Network, ok);
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
        }

        /// <summary>
        /// Capture 20260806-063619 Daily Login PrivateMsg to the claiming character only.
        /// </summary>
        private void DistributePrivateSystem(PrivateSystemMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.Text))
            {
                return;
            }

            byte[] packet = MsgPrivate.Create(
                0,
                message.Text,
                message.Unk1 != 0 ? message.Unk1 : 3,
                message.Unk2);
            if (packet == null)
            {
                return;
            }

            Client cli;
            uint characterId = unchecked((uint)message.CharacterId);
            if (this.ConnectedClients.TryGetValue(characterId, out cli)
                && cli != null
                && cli.Character != null)
            {
                cli.Send(packet);
                return;
            }

            string wantName = message.CharacterName ?? string.Empty;
            foreach (Client connected in this.ConnectedClients.Values)
            {
                if (connected == null || connected.Character == null)
                {
                    continue;
                }

                if (wantName.Length > 0
                    && string.Equals(
                        connected.Character.characterName,
                        wantName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    connected.Send(packet);
                    return;
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Error,
                "DistributePrivateSystem: no chat client for CharacterId="
                + message.CharacterId
                + " name="
                + wantName);
        }

        /// <summary>
        /// A vicinity line from no one (type 0x23, empty name and blob), as live sends "All team members can loot these
        /// remains." to each team member.
        /// </summary>
        private void DistributeAnonymousVicinity(IEnumerable<int> characterIds, string text, byte[] blob)
        {
            byte[] packet = MsgAnonymousVicinity.Create(string.Empty, text ?? string.Empty, blob);
            var wanted = new HashSet<int>(characterIds);
            foreach (Client cli in this.ConnectedClients.Values)
            {
                if (cli.Character != null && wanted.Contains((int)cli.Character.CharacterId))
                    cli.Send(packet);
            }
        }

        private void DistributeVicinityChat(VicinityChatMessage vicinityChatMessage)
        {
            byte[] packet = MsgVicinity.Create(
                (uint)vicinityChatMessage.SenderId,
                vicinityChatMessage.Text,
                (byte)vicinityChatMessage.MessageType);

            string lookup = vicinityChatMessage.SenderName
                            ?? CharacterDao.Instance.GetCharacterNameById(vicinityChatMessage.SenderId);
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
