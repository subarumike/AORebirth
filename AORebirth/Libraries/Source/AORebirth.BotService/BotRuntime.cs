namespace AORebirth.BotService
{
    using System;
    using System.Collections.Generic;

    public sealed class BotSessionService : IBotSessionValidator
    {
        private readonly IBotIdentityRepository repository;
        private readonly BotCredentialAuthenticator authenticator;
        private readonly IBotAuditSink audit;
        private readonly Func<DateTime> utcNow;

        public BotSessionService(
            IBotIdentityRepository repository,
            BotCredentialAuthenticator authenticator,
            IBotAuditSink audit,
            Func<DateTime> utcNow = null)
        {
            this.repository = repository ?? throw new ArgumentNullException("repository");
            this.authenticator = authenticator ?? throw new ArgumentNullException("authenticator");
            this.audit = audit ?? throw new ArgumentNullException("audit");
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public BotSession AuthenticateAndCreate(string credential)
        {
            BotAuthenticationResult authentication = this.authenticator.Authenticate(credential);
            if (!authentication.Succeeded)
            {
                return null;
            }

            BotPrincipal principal = authentication.Principal;
            BotSession session = new BotSession
            {
                SessionId = Guid.NewGuid(),
                BotId = principal.BotId,
                DisplayName = principal.DisplayName,
                OwningAccountId = principal.OwningAccountId,
                OrganizationId = principal.OrganizationId,
                PublicCredentialId = authentication.Credential.PublicCredentialId,
                CredentialVersion = authentication.Credential.Version,
                GrantedScopes = principal.Scopes,
                RateLimitProfile = principal.RateLimitProfile,
                AuditIdentity = principal.AuditIdentity,
                CreatedAtUtc = this.utcNow(),
                EnabledSnapshot = principal.Enabled
            };
            this.audit.Record(BotCredentialAuthenticator.CreateAudit(
                BotAuditKind.SessionCreated,
                principal,
                session,
                true,
                "SESSION_CREATED",
                this.utcNow()));
            return session;
        }

        public BotOperationResult Validate(BotSession session)
        {
            if (session == null)
            {
                return BotOperationResult.Denied("SESSION_REQUIRED");
            }

            BotPrincipal principal = this.repository.FindPrincipal(session.BotId);
            if (principal == null || !principal.Enabled)
            {
                return BotOperationResult.Denied("BOT_DISABLED_OR_MISSING");
            }

            BotCredentialRecord credential = this.repository.FindCredential(session.PublicCredentialId);
            if (credential == null || credential.Revoked)
            {
                return BotOperationResult.Denied("CREDENTIAL_REVOKED_OR_MISSING");
            }

            if (credential.BotId != session.BotId
                || credential.Version != session.CredentialVersion
                || principal.CurrentCredentialVersion != session.CredentialVersion)
            {
                return BotOperationResult.Denied("SESSION_CREDENTIAL_STALE");
            }

            return BotOperationResult.Allowed();
        }

        public void End(BotSession session, string reasonCode)
        {
            if (session == null)
            {
                return;
            }

            BotPrincipal principal = this.repository.FindPrincipal(session.BotId);
            this.audit.Record(BotCredentialAuthenticator.CreateAudit(
                BotAuditKind.SessionEnded,
                principal,
                session,
                true,
                reasonCode ?? "SESSION_ENDED",
                this.utcNow()));
        }
    }

    public sealed class InMemoryBotRateLimitPolicyResolver : IBotRateLimitPolicyResolver
    {
        private readonly Dictionary<string, Dictionary<BotOperation, BotRateLimitRule>> profiles =
            new Dictionary<string, Dictionary<BotOperation, BotRateLimitRule>>(StringComparer.Ordinal);

        public void SetRule(string profile, BotOperation operation, int limit, TimeSpan window)
        {
            if (string.IsNullOrWhiteSpace(profile) || limit < 1 || window <= TimeSpan.Zero)
            {
                throw new ArgumentException("A valid rate-limit profile, limit, and window are required.");
            }

            Dictionary<BotOperation, BotRateLimitRule> rules;
            if (!this.profiles.TryGetValue(profile, out rules))
            {
                rules = new Dictionary<BotOperation, BotRateLimitRule>();
                this.profiles[profile] = rules;
            }

            rules[operation] = new BotRateLimitRule { Limit = limit, Window = window };
        }

        public BotRateLimitRule Resolve(string profile, BotOperation operation)
        {
            Dictionary<BotOperation, BotRateLimitRule> rules;
            BotRateLimitRule rule;
            return profile != null
                && this.profiles.TryGetValue(profile, out rules)
                && rules.TryGetValue(operation, out rule)
                ? rule
                : null;
        }
    }

    public sealed class InMemoryBotRateLimiter : IBotRateLimiter
    {
        private readonly object sync = new object();
        private readonly IBotRateLimitPolicyResolver policies;
        private readonly Dictionary<string, Queue<DateTime>> observations =
            new Dictionary<string, Queue<DateTime>>(StringComparer.Ordinal);

        public InMemoryBotRateLimiter(IBotRateLimitPolicyResolver policies)
        {
            this.policies = policies ?? throw new ArgumentNullException("policies");
        }

        public BotRateLimitDecision TryAcquire(
            Guid botId,
            string profile,
            BotOperation operation,
            DateTime timestampUtc)
        {
            BotRateLimitRule rule = this.policies.Resolve(profile, operation);
            if (rule == null)
            {
                return new BotRateLimitDecision { Allowed = false, ReasonCode = "RATE_POLICY_MISSING" };
            }

            string key = botId.ToString("N") + ":" + (int)operation;
            lock (this.sync)
            {
                Queue<DateTime> queue;
                if (!this.observations.TryGetValue(key, out queue))
                {
                    queue = new Queue<DateTime>();
                    this.observations[key] = queue;
                }

                DateTime cutoff = timestampUtc - rule.Window;
                while (queue.Count > 0 && queue.Peek() <= cutoff)
                {
                    queue.Dequeue();
                }

                if (queue.Count >= rule.Limit)
                {
                    return new BotRateLimitDecision
                    {
                        Allowed = false,
                        ReasonCode = "RATE_LIMIT_EXCEEDED",
                        RetryAfter = rule.Window - (timestampUtc - queue.Peek())
                    };
                }

                queue.Enqueue(timestampUtc);
                return new BotRateLimitDecision { Allowed = true, ReasonCode = "ALLOWED" };
            }
        }
    }

    public sealed class BotRuntime
    {
        private readonly BotSessionService sessions;
        private readonly BotAuthorizationEvaluator authorization;
        private readonly IBotRateLimiter rateLimiter;
        private readonly IBotChatGateway gateway;
        private readonly IBotAuditSink audit;
        private readonly Func<DateTime> utcNow;

        public BotRuntime(
            BotSessionService sessions,
            BotAuthorizationEvaluator authorization,
            IBotRateLimiter rateLimiter,
            IBotChatGateway gateway,
            IBotAuditSink audit,
            Func<DateTime> utcNow = null)
        {
            this.sessions = sessions ?? throw new ArgumentNullException("sessions");
            this.authorization = authorization ?? throw new ArgumentNullException("authorization");
            this.rateLimiter = rateLimiter ?? throw new ArgumentNullException("rateLimiter");
            this.gateway = gateway ?? throw new ArgumentNullException("gateway");
            this.audit = audit ?? throw new ArgumentNullException("audit");
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public BotSession Authenticate(string credential)
        {
            return this.sessions.AuthenticateAndCreate(credential);
        }

        public BotOperationResult Execute(BotSession session, BotChatRequest request)
        {
            BotOperationResult validity = this.sessions.Validate(session);
            if (!validity.Succeeded)
            {
                this.Record(session, request, BotAuditKind.CredentialInvalid, false, validity.ReasonCode);
                return validity;
            }

            BotAuthorizationResult authorizationResult = this.authorization.Authorize(session, request);
            if (!authorizationResult.Allowed)
            {
                this.Record(session, request, BotAuditKind.PermissionDenied, false, authorizationResult.ReasonCode);
                return BotOperationResult.Denied(authorizationResult.ReasonCode);
            }

            BotRateLimitDecision rateLimit = this.rateLimiter.TryAcquire(
                session.BotId,
                session.RateLimitProfile,
                request.Operation,
                this.utcNow());
            if (!rateLimit.Allowed)
            {
                this.Record(session, request, BotAuditKind.RateLimitViolation, false, rateLimit.ReasonCode);
                return BotOperationResult.Denied(rateLimit.ReasonCode);
            }

            BotOperationResult result = this.gateway.Execute(session, request);
            this.Record(session, request, AuditKindFor(request.Operation), result.Succeeded, result.ReasonCode);
            return result;
        }

        private void Record(
            BotSession session,
            BotChatRequest request,
            BotAuditKind kind,
            bool succeeded,
            string reasonCode)
        {
            this.audit.Record(
                new BotAuditEvent
                {
                    Kind = kind,
                    BotId = session == null ? (Guid?)null : session.BotId,
                    AccountId = session == null ? (long?)null : session.OwningAccountId,
                    OrganizationId = session == null ? (long?)null : session.OrganizationId,
                    SessionId = session == null ? (Guid?)null : session.SessionId,
                    Operation = request == null ? BotOperation.Unknown : request.Operation,
                    Succeeded = succeeded,
                    ReasonCode = reasonCode,
                    TimestampUtc = this.utcNow(),
                    AuditIdentity = session == null ? null : session.AuditIdentity
                });
        }

        private static BotAuditKind AuditKindFor(BotOperation operation)
        {
            switch (operation)
            {
                case BotOperation.TellSend:
                    return BotAuditKind.TellSend;
                case BotOperation.OrganizationSend:
                    return BotAuditKind.OrganizationMessageSend;
                case BotOperation.ChannelJoin:
                    return BotAuditKind.ChannelJoin;
                case BotOperation.ChannelLeave:
                    return BotAuditKind.ChannelLeave;
                case BotOperation.ChannelRead:
                    return BotAuditKind.ChannelRead;
                default:
                    return BotAuditKind.ChannelSend;
            }
        }
    }

    public sealed class DelegatingBotChatGateway : IBotChatGateway
    {
        private readonly Func<BotSession, BotChatRequest, BotOperationResult> execute;

        public DelegatingBotChatGateway(Func<BotSession, BotChatRequest, BotOperationResult> execute)
        {
            this.execute = execute ?? throw new ArgumentNullException("execute");
        }

        public BotOperationResult Execute(BotSession session, BotChatRequest request)
        {
            return this.execute(session, request);
        }
    }
}
