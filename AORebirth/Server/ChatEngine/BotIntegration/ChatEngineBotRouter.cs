namespace ChatEngine.BotIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.BotService;

    using ChatEngine.Channels;
    using ChatEngine.CoreClient;
    using ChatEngine.CoreServer;
    using ChatEngine.Packets;

    internal sealed class ChatEngineBotRouter : IBotChatRequestHandler
    {
        private const uint FirstBotWireId = 0xE0000000;
        private readonly ChatServer server;
        private readonly BotAuthorizationEvaluator authorization = new BotAuthorizationEvaluator();
        private readonly object sync = new object();
        private readonly Dictionary<Guid, uint> botWireIds = new Dictionary<Guid, uint>();
        private readonly Dictionary<uint, string> botNames = new Dictionary<uint, string>();
        private readonly Dictionary<Guid, HashSet<string>> channelSubscriptions =
            new Dictionary<Guid, HashSet<string>>();
        private readonly BotInboundDeliveryQueue inbound = new BotInboundDeliveryQueue();
        private uint nextWireId = FirstBotWireId;

        public ChatEngineBotRouter(ChatServer server)
        {
            this.server = server ?? throw new ArgumentNullException("server");
        }

        public BotOperationResult Handle(BotSession session, BotChatRequest request)
        {
            if (session == null || request == null)
            {
                return BotOperationResult.Denied("AUTHORIZATION_CONTEXT_MISSING");
            }

            uint wireId = this.GetOrCreateWireId(session.BotId, session.DisplayName);
            this.inbound.Register(session, wireId);
            if (request.Operation == BotOperation.EventPoll)
            {
                return this.inbound.Poll(session);
            }

            BotAuthorizationResult decision = this.authorization.Authorize(session, request);
            if (!decision.Allowed)
            {
                return BotOperationResult.Denied(decision.ReasonCode);
            }

            switch (request.Operation)
            {
                case BotOperation.TellSend:
                    return this.SendTell(wireId, session.DisplayName, request);
                case BotOperation.OrganizationSend:
                    return this.SendOrganizationMessage(wireId, session.DisplayName, request);
                case BotOperation.ChannelJoin:
                    return this.JoinChannel(session, request);
                case BotOperation.ChannelLeave:
                    return this.LeaveChannel(session, request);
                case BotOperation.ChannelRead:
                    return this.CheckChannelRead(session, request);
                case BotOperation.ChannelSend:
                    return this.SendChannelMessage(wireId, session.DisplayName, request);
                default:
                    return BotOperationResult.Denied("CHAT_OPERATION_NOT_IMPLEMENTED");
            }
        }

        public bool TryResolveBotName(string displayName, out uint wireId)
        {
            lock (this.sync)
            {
                foreach (KeyValuePair<uint, string> bot in this.botNames)
                {
                    if (string.Equals(bot.Value, displayName, StringComparison.OrdinalIgnoreCase))
                    {
                        wireId = bot.Key;
                        return true;
                    }
                }
            }

            wireId = uint.MaxValue;
            return false;
        }

        public bool TryPublishTell(uint targetWireId, uint senderCharacterId, string senderName, string text)
        {
            return this.inbound.TryPublishTell(targetWireId, senderCharacterId, senderName, text, DateTime.UtcNow);
        }

        public int PublishChannelMessage(
            byte channelType,
            uint channelId,
            uint senderCharacterId,
            string senderName,
            string text)
        {
            return this.inbound.PublishChannel(
                channelType,
                channelId,
                senderCharacterId,
                senderName,
                text,
                DateTime.UtcNow,
                (byte)ChannelType.Organization);
        }

        private BotOperationResult SendTell(uint wireId, string displayName, BotChatRequest request)
        {
            Client target;
            if (!this.server.ConnectedClients.TryGetValue(request.TargetCharacterId, out target)
                || target == null)
            {
                return BotOperationResult.Denied("TELL_TARGET_OFFLINE");
            }

            EnsureKnownBot(target, wireId, displayName);
            target.Send(MsgPrivate.Create(wireId, request.Text, 3, 0));
            return BotOperationResult.Allowed("TELL_SENT");
        }

        private BotOperationResult SendOrganizationMessage(uint wireId, string displayName, BotChatRequest request)
        {
            if (!request.OrganizationId.HasValue)
            {
                return BotOperationResult.Denied("ORGANIZATION_CONTEXT_REQUIRED");
            }

            ChannelBase channel = this.FindChannel((byte)ChannelType.Organization, checked((uint)request.OrganizationId.Value));
            if (channel == null)
            {
                return BotOperationResult.Denied("ORGANIZATION_CHANNEL_NOT_FOUND");
            }

            channel.BotMessage(wireId, displayName, request.Text);
            return BotOperationResult.Allowed("ORGANIZATION_MESSAGE_SENT");
        }

        private BotOperationResult JoinChannel(BotSession session, BotChatRequest request)
        {
            if (this.FindChannel(request.ChannelType, request.ChannelId) == null)
            {
                return BotOperationResult.Denied("CHANNEL_NOT_FOUND");
            }

            lock (this.sync)
            {
                HashSet<string> subscriptions;
                if (!this.channelSubscriptions.TryGetValue(session.BotId, out subscriptions))
                {
                    subscriptions = new HashSet<string>(StringComparer.Ordinal);
                    this.channelSubscriptions[session.BotId] = subscriptions;
                }

                subscriptions.Add(ChannelKey(request.ChannelType, request.ChannelId));
            }

            this.inbound.Subscribe(session, request.ChannelType, request.ChannelId);

            return BotOperationResult.Allowed("CHANNEL_JOINED");
        }

        private BotOperationResult LeaveChannel(BotSession session, BotChatRequest request)
        {
            lock (this.sync)
            {
                HashSet<string> subscriptions;
                if (this.channelSubscriptions.TryGetValue(session.BotId, out subscriptions))
                {
                    subscriptions.Remove(ChannelKey(request.ChannelType, request.ChannelId));
                }
            }

            this.inbound.Unsubscribe(session, request.ChannelType, request.ChannelId);

            return BotOperationResult.Allowed("CHANNEL_LEFT");
        }

        private BotOperationResult CheckChannelRead(BotSession session, BotChatRequest request)
        {
            lock (this.sync)
            {
                HashSet<string> subscriptions;
                if (!this.channelSubscriptions.TryGetValue(session.BotId, out subscriptions)
                    || !subscriptions.Contains(ChannelKey(request.ChannelType, request.ChannelId)))
                {
                    return BotOperationResult.Denied("CHANNEL_NOT_JOINED");
                }
            }

            return BotOperationResult.Allowed("CHANNEL_READ_AUTHORIZED");
        }

        private BotOperationResult SendChannelMessage(uint wireId, string displayName, BotChatRequest request)
        {
            ChannelBase channel = this.FindChannel(request.ChannelType, request.ChannelId);
            if (channel == null)
            {
                return BotOperationResult.Denied("CHANNEL_NOT_FOUND");
            }

            channel.BotMessage(wireId, displayName, request.Text);
            return BotOperationResult.Allowed("CHANNEL_MESSAGE_SENT");
        }

        private ChannelBase FindChannel(byte channelType, uint channelId)
        {
            return this.server.Channels.FirstOrDefault(
                channel => (byte)channel.channelType == channelType && channel.ChannelId == channelId);
        }

        private uint GetOrCreateWireId(Guid botId, string displayName)
        {
            lock (this.sync)
            {
                uint wireId;
                if (this.botWireIds.TryGetValue(botId, out wireId))
                {
                    return wireId;
                }

                do
                {
                    wireId = this.nextWireId++;
                }
                while (this.botNames.ContainsKey(wireId) || this.server.ConnectedClients.ContainsKey(wireId));

                this.botWireIds[botId] = wireId;
                this.botNames[wireId] = displayName ?? "AORebirth Bot";
                return wireId;
            }
        }

        private static string ChannelKey(byte channelType, uint channelId)
        {
            return channelType + ":" + channelId;
        }

        private static void EnsureKnownBot(Client target, uint wireId, string displayName)
        {
            if (!target.KnownClients.Contains(wireId))
            {
                target.Send(NameLookupResult.Create(wireId, displayName ?? "AORebirth Bot"));
                target.KnownClients.Add(wireId);
            }
        }
    }
}
