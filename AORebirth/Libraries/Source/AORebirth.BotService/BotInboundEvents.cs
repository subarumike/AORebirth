namespace AORebirth.BotService
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    public enum BotInboundEventKind
    {
        Tell = 1,
        Organization = 2,
        Channel = 3
    }

    public sealed class BotInboundEvent
    {
        public Guid EventId { get; set; }

        public BotInboundEventKind Kind { get; set; }

        public uint SenderCharacterId { get; set; }

        public string SenderName { get; set; }

        public byte ChannelType { get; set; }

        public uint ChannelId { get; set; }

        public long? OrganizationId { get; set; }

        public string Text { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class BotInboundDeliveryQueue
    {
        private const int MaximumQueuedEventsPerBot = 256;
        private readonly object sync = new object();
        private readonly Dictionary<Guid, BotSession> sessions = new Dictionary<Guid, BotSession>();
        private readonly Dictionary<uint, Guid> botIdsByWireId = new Dictionary<uint, Guid>();
        private readonly Dictionary<Guid, HashSet<string>> subscriptions = new Dictionary<Guid, HashSet<string>>();
        private readonly Dictionary<Guid, Queue<BotInboundEvent>> events = new Dictionary<Guid, Queue<BotInboundEvent>>();

        public void Register(BotSession session, uint wireId)
        {
            if (session == null)
            {
                throw new ArgumentNullException("session");
            }

            lock (this.sync)
            {
                this.sessions[session.BotId] = session.Copy();
                this.botIdsByWireId[wireId] = session.BotId;
                if (!this.events.ContainsKey(session.BotId))
                {
                    this.events.Add(session.BotId, new Queue<BotInboundEvent>());
                }
            }
        }

        public void Subscribe(BotSession session, byte channelType, uint channelId)
        {
            lock (this.sync)
            {
                this.RequireCurrent(session);
                HashSet<string> botSubscriptions;
                if (!this.subscriptions.TryGetValue(session.BotId, out botSubscriptions))
                {
                    botSubscriptions = new HashSet<string>(StringComparer.Ordinal);
                    this.subscriptions.Add(session.BotId, botSubscriptions);
                }

                botSubscriptions.Add(ChannelKey(channelType, channelId));
            }
        }

        public void Unsubscribe(BotSession session, byte channelType, uint channelId)
        {
            lock (this.sync)
            {
                this.RequireCurrent(session);
                HashSet<string> botSubscriptions;
                if (this.subscriptions.TryGetValue(session.BotId, out botSubscriptions))
                {
                    botSubscriptions.Remove(ChannelKey(channelType, channelId));
                }
            }
        }

        public bool IsSubscribed(BotSession session, byte channelType, uint channelId)
        {
            lock (this.sync)
            {
                if (!this.IsCurrent(session))
                {
                    return false;
                }

                HashSet<string> botSubscriptions;
                return this.subscriptions.TryGetValue(session.BotId, out botSubscriptions)
                    && botSubscriptions.Contains(ChannelKey(channelType, channelId));
            }
        }

        public bool TryPublishTell(uint targetWireId, uint senderCharacterId, string senderName, string text, DateTime createdAtUtc)
        {
            lock (this.sync)
            {
                Guid botId;
                BotSession session;
                if (!this.botIdsByWireId.TryGetValue(targetWireId, out botId)
                    || !this.sessions.TryGetValue(botId, out session)
                    || (session.GrantedScopes & BotScope.TellReceive) != BotScope.TellReceive)
                {
                    return false;
                }

                this.Enqueue(botId, new BotInboundEvent
                {
                    EventId = Guid.NewGuid(),
                    Kind = BotInboundEventKind.Tell,
                    SenderCharacterId = senderCharacterId,
                    SenderName = senderName,
                    Text = text,
                    CreatedAtUtc = createdAtUtc
                });
                return true;
            }
        }

        public int PublishChannel(
            byte channelType,
            uint channelId,
            uint senderCharacterId,
            string senderName,
            string text,
            DateTime createdAtUtc,
            byte organizationChannelType)
        {
            lock (this.sync)
            {
                int delivered = 0;
                foreach (KeyValuePair<Guid, BotSession> entry in this.sessions)
                {
                    BotSession session = entry.Value;
                    HashSet<string> botSubscriptions;
                    if (!this.subscriptions.TryGetValue(entry.Key, out botSubscriptions)
                        || !botSubscriptions.Contains(ChannelKey(channelType, channelId)))
                    {
                        continue;
                    }

                    bool organization = channelType == organizationChannelType;
                    BotScope required = organization ? BotScope.OrganizationRead : BotScope.ChannelRead;
                    if ((session.GrantedScopes & required) != required)
                    {
                        continue;
                    }

                    if (organization && (!session.OrganizationId.HasValue || session.OrganizationId.Value != channelId))
                    {
                        continue;
                    }

                    this.Enqueue(entry.Key, new BotInboundEvent
                    {
                        EventId = Guid.NewGuid(),
                        Kind = organization ? BotInboundEventKind.Organization : BotInboundEventKind.Channel,
                        SenderCharacterId = senderCharacterId,
                        SenderName = senderName,
                        ChannelType = channelType,
                        ChannelId = channelId,
                        OrganizationId = organization ? (long?)channelId : null,
                        Text = text,
                        CreatedAtUtc = createdAtUtc
                    });
                    delivered++;
                }

                return delivered;
            }
        }

        public BotOperationResult Poll(BotSession session)
        {
            lock (this.sync)
            {
                if (!this.IsCurrent(session))
                {
                    return BotOperationResult.Denied("BOT_SESSION_REPLACED");
                }

                Queue<BotInboundEvent> queue;
                if (!this.events.TryGetValue(session.BotId, out queue) || queue.Count == 0)
                {
                    return BotOperationResult.Allowed();
                }

                return BotOperationResult.Allowed(BotInboundEventCodec.Encode(queue.Dequeue()));
            }
        }

        private void Enqueue(Guid botId, BotInboundEvent inboundEvent)
        {
            Queue<BotInboundEvent> queue;
            if (!this.events.TryGetValue(botId, out queue))
            {
                queue = new Queue<BotInboundEvent>();
                this.events.Add(botId, queue);
            }

            while (queue.Count >= MaximumQueuedEventsPerBot)
            {
                queue.Dequeue();
            }

            queue.Enqueue(inboundEvent);
        }

        private bool IsCurrent(BotSession session)
        {
            BotSession current;
            return session != null
                && this.sessions.TryGetValue(session.BotId, out current)
                && current.SessionId == session.SessionId
                && current.CredentialVersion == session.CredentialVersion;
        }

        private void RequireCurrent(BotSession session)
        {
            if (!this.IsCurrent(session))
            {
                throw new InvalidOperationException("The bot session is no longer current.");
            }
        }

        private static string ChannelKey(byte channelType, uint channelId)
        {
            return channelType + ":" + channelId;
        }
    }

    public static class BotInboundEventCodec
    {
        private const int Version = 1;

        public static string Encode(BotInboundEvent value)
        {
            if (value == null)
            {
                return null;
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Version);
                writer.Write(value.EventId.ToByteArray());
                writer.Write((int)value.Kind);
                writer.Write(value.SenderCharacterId);
                writer.Write(value.SenderName ?? string.Empty);
                writer.Write(value.ChannelType);
                writer.Write(value.ChannelId);
                writer.Write(value.OrganizationId.HasValue);
                if (value.OrganizationId.HasValue)
                {
                    writer.Write(value.OrganizationId.Value);
                }

                writer.Write(value.Text ?? string.Empty);
                writer.Write(value.CreatedAtUtc.ToBinary());
                writer.Flush();
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        public static BotInboundEvent Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded))
            {
                return null;
            }

            using (MemoryStream stream = new MemoryStream(Convert.FromBase64String(encoded), false))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.ReadInt32() != Version)
                {
                    throw new InvalidDataException("The bot inbound event version is unsupported.");
                }

                BotInboundEvent result = new BotInboundEvent
                {
                    EventId = new Guid(reader.ReadBytes(16)),
                    Kind = (BotInboundEventKind)reader.ReadInt32(),
                    SenderCharacterId = reader.ReadUInt32(),
                    SenderName = reader.ReadString(),
                    ChannelType = reader.ReadByte(),
                    ChannelId = reader.ReadUInt32()
                };
                result.OrganizationId = reader.ReadBoolean() ? (long?)reader.ReadInt64() : null;
                result.Text = reader.ReadString();
                result.CreatedAtUtc = DateTime.FromBinary(reader.ReadInt64()).ToUniversalTime();
                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException("The bot inbound event contains trailing data.");
                }

                return result;
            }
        }
    }
}
