namespace AORebirth.BotService
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;

    public interface IBotInboundEventSink
    {
        void Receive(BotPrincipal principal, BotInboundEvent inboundEvent);
    }

    public interface IHostedBotChatGateway
    {
        BotInboundEvent Poll(BotSession session);
    }

    public sealed class PrivateTcpHostedBotChatGateway : IHostedBotChatGateway
    {
        private readonly BotPrivateTcpClient client;

        public PrivateTcpHostedBotChatGateway(IPEndPoint endpoint, byte[] serviceKey)
        {
            this.client = new BotPrivateTcpClient(endpoint, serviceKey);
        }

        public BotInboundEvent Poll(BotSession session)
        {
            BotOperationResult result = this.client.Execute(
                session,
                new BotChatRequest { Operation = BotOperation.EventPoll });
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("ChatEngine rejected hosted bot polling: " + result.ReasonCode);
            }

            return BotInboundEventCodec.Decode(result.Detail);
        }
    }

    public sealed class BotServiceHostLoop
    {
        private readonly IPersistentBotRepository repository;
        private readonly IHostedBotChatGateway gateway;
        private readonly IBotInboundEventSink sink;
        private readonly Dictionary<Guid, BotSession> sessions = new Dictionary<Guid, BotSession>();
        private readonly TimeSpan pollInterval;
        private readonly TimeSpan maximumReconnectDelay;
        private TimeSpan reconnectDelay;

        public BotServiceHostLoop(
            IPersistentBotRepository repository,
            IHostedBotChatGateway gateway,
            IBotInboundEventSink sink,
            TimeSpan pollInterval,
            TimeSpan initialReconnectDelay,
            TimeSpan maximumReconnectDelay)
        {
            this.repository = repository ?? throw new ArgumentNullException("repository");
            this.gateway = gateway ?? throw new ArgumentNullException("gateway");
            this.sink = sink ?? throw new ArgumentNullException("sink");
            if (pollInterval <= TimeSpan.Zero || initialReconnectDelay <= TimeSpan.Zero || maximumReconnectDelay < initialReconnectDelay)
            {
                throw new ArgumentException("Valid host polling and reconnect intervals are required.");
            }

            this.pollInterval = pollInterval;
            this.reconnectDelay = initialReconnectDelay;
            this.maximumReconnectDelay = maximumReconnectDelay;
        }

        public int SessionCount
        {
            get { return this.sessions.Count; }
        }

        public int RunCycle()
        {
            BotPrincipal[] enabled = this.repository.ListEnabledPrincipals();
            HashSet<Guid> active = new HashSet<Guid>();
            int delivered = 0;
            foreach (BotPrincipal principal in enabled)
            {
                BotCredentialRecord credential = this.repository.FindCurrentCredential(principal.BotId);
                if (credential == null || credential.Revoked)
                {
                    continue;
                }

                active.Add(principal.BotId);
                BotSession session;
                if (!this.sessions.TryGetValue(principal.BotId, out session)
                    || session.CredentialVersion != credential.Version
                    || session.GrantedScopes != principal.Scopes
                    || session.OrganizationId != principal.OrganizationId)
                {
                    session = CreateSession(principal, credential);
                    this.sessions[principal.BotId] = session;
                }

                BotInboundEvent inboundEvent = this.gateway.Poll(session);
                if (inboundEvent != null)
                {
                    this.sink.Receive(principal, inboundEvent);
                    delivered++;
                }
            }

            List<Guid> stale = new List<Guid>();
            foreach (Guid botId in this.sessions.Keys)
            {
                if (!active.Contains(botId))
                {
                    stale.Add(botId);
                }
            }

            foreach (Guid botId in stale)
            {
                this.sessions.Remove(botId);
            }

            return delivered;
        }

        public void Run(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan wait = this.pollInterval;
                try
                {
                    this.RunCycle();
                    this.reconnectDelay = TimeSpan.FromMilliseconds(Math.Min(this.reconnectDelay.TotalMilliseconds, this.maximumReconnectDelay.TotalMilliseconds));
                }
                catch (Exception)
                {
                    wait = this.reconnectDelay;
                    this.reconnectDelay = TimeSpan.FromMilliseconds(
                        Math.Min(this.maximumReconnectDelay.TotalMilliseconds, this.reconnectDelay.TotalMilliseconds * 2));
                }

                cancellationToken.WaitHandle.WaitOne(wait);
            }

            this.sessions.Clear();
        }

        private static BotSession CreateSession(BotPrincipal principal, BotCredentialRecord credential)
        {
            return new BotSession
            {
                SessionId = Guid.NewGuid(),
                BotId = principal.BotId,
                DisplayName = principal.DisplayName,
                OwningAccountId = principal.OwningAccountId,
                OrganizationId = principal.OrganizationId,
                PublicCredentialId = credential.PublicCredentialId,
                CredentialVersion = credential.Version,
                GrantedScopes = principal.Scopes,
                RateLimitProfile = principal.RateLimitProfile,
                AuditIdentity = principal.AuditIdentity,
                CreatedAtUtc = DateTime.UtcNow,
                EnabledSnapshot = principal.Enabled
            };
        }
    }
}
