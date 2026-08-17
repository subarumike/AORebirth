namespace AORebirth.BotService
{
    using System;
    using System.Collections.Generic;

    [Flags]
    public enum BotScope : long
    {
        None = 0,
        TellReceive = 1L << 0,
        TellSend = 1L << 1,
        OrganizationRead = 1L << 2,
        OrganizationSend = 1L << 3,
        ChannelJoin = 1L << 4,
        ChannelLeave = 1L << 5,
        ChannelRead = 1L << 6,
        ChannelSend = 1L << 7,
        RosterRead = 1L << 8,
        CommandReceive = 1L << 9,
        CommandExecute = 1L << 10
    }

    public enum BotOperation
    {
        Unknown = 0,
        TellReceive = 1,
        TellSend = 2,
        OrganizationRead = 3,
        OrganizationSend = 4,
        ChannelJoin = 5,
        ChannelLeave = 6,
        ChannelRead = 7,
        ChannelSend = 8,
        RosterRead = 9,
        CommandReceive = 10,
        CommandExecute = 11,
        EventPoll = 12
    }

    public enum BotAuthenticationFailure
    {
        None = 0,
        InvalidCredential = 1,
        DisabledBot = 2,
        RevokedCredential = 3,
        StaleCredentialVersion = 4
    }

    public enum BotAuditKind
    {
        AuthenticationSuccess,
        AuthenticationFailure,
        SessionCreated,
        SessionEnded,
        CredentialInvalid,
        PermissionDenied,
        TellSend,
        OrganizationMessageSend,
        ChannelJoin,
        ChannelLeave,
        ChannelRead,
        ChannelSend,
        RateLimitViolation,
        PrincipalCreated,
        PrincipalEnabled,
        PrincipalDisabled,
        CredentialRotated,
        CredentialRevoked,
        ScopesReplaced,
        OrganizationAssigned,
        InboundEventDelivered
    }

    public sealed class BotPrincipal
    {
        public Guid BotId { get; set; }

        public string DisplayName { get; set; }

        public long OwningAccountId { get; set; }

        public long? OrganizationId { get; set; }

        public bool Enabled { get; set; }

        public int CurrentCredentialVersion { get; set; }

        public BotScope Scopes { get; set; }

        public string RateLimitProfile { get; set; }

        public string AuditIdentity { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime UpdatedAtUtc { get; set; }

        public BotPrincipal Copy()
        {
            return (BotPrincipal)this.MemberwiseClone();
        }
    }

    public sealed class BotCredentialRecord
    {
        public string PublicCredentialId { get; set; }

        public Guid BotId { get; set; }

        public int Version { get; set; }

        public string Algorithm { get; set; }

        public int Iterations { get; set; }

        public byte[] Salt { get; set; }

        public byte[] Verifier { get; set; }

        public bool Revoked { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? RevokedAtUtc { get; set; }

        public BotCredentialRecord Copy()
        {
            BotCredentialRecord copy = (BotCredentialRecord)this.MemberwiseClone();
            copy.Salt = this.Salt == null ? null : (byte[])this.Salt.Clone();
            copy.Verifier = this.Verifier == null ? null : (byte[])this.Verifier.Clone();
            return copy;
        }

        public override string ToString()
        {
            return "BotCredentialRecord bot=" + this.BotId.ToString("N")
                + " version=" + this.Version
                + " state=" + (this.Revoked ? "revoked" : "active");
        }
    }

    public sealed class BotCredentialIssue
    {
        public string Credential { get; set; }

        public string PublicCredentialId { get; set; }

        public Guid BotId { get; set; }

        public int Version { get; set; }

        public override string ToString()
        {
            return "BotCredentialIssue bot=" + this.BotId.ToString("N")
                + " version=" + this.Version
                + " credential=[REDACTED]";
        }
    }

    public sealed class BotAuthenticationResult
    {
        public bool Succeeded { get; set; }

        public BotAuthenticationFailure Failure { get; set; }

        public BotPrincipal Principal { get; set; }

        public BotCredentialRecord Credential { get; set; }

        public override string ToString()
        {
            return this.Succeeded
                ? "BotAuthenticationResult success bot=" + this.Principal.BotId.ToString("N")
                : "BotAuthenticationResult denied reason=" + this.Failure;
        }
    }

    public sealed class BotSession
    {
        public Guid SessionId { get; set; }

        public Guid BotId { get; set; }

        public string DisplayName { get; set; }

        public long OwningAccountId { get; set; }

        public long? OrganizationId { get; set; }

        public string PublicCredentialId { get; set; }

        public int CredentialVersion { get; set; }

        public BotScope GrantedScopes { get; set; }

        public string RateLimitProfile { get; set; }

        public string AuditIdentity { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public bool EnabledSnapshot { get; set; }

        public BotSession Copy()
        {
            return (BotSession)this.MemberwiseClone();
        }
    }

    public sealed class BotChatRequest
    {
        public BotOperation Operation { get; set; }

        public uint TargetCharacterId { get; set; }

        public byte ChannelType { get; set; }

        public uint ChannelId { get; set; }

        public long? OrganizationId { get; set; }

        public string Text { get; set; }

        public static BotChatRequest Tell(uint targetCharacterId, string text)
        {
            return new BotChatRequest
            {
                Operation = BotOperation.TellSend,
                TargetCharacterId = targetCharacterId,
                Text = text
            };
        }

        public static BotChatRequest Organization(long organizationId, string text)
        {
            return new BotChatRequest
            {
                Operation = BotOperation.OrganizationSend,
                OrganizationId = organizationId,
                ChannelType = 3,
                ChannelId = checked((uint)organizationId),
                Text = text
            };
        }
    }

    public sealed class BotOperationResult
    {
        public bool Succeeded { get; set; }

        public string ReasonCode { get; set; }

        public string Detail { get; set; }

        public static BotOperationResult Allowed(string detail = null)
        {
            return new BotOperationResult { Succeeded = true, ReasonCode = "ALLOWED", Detail = detail };
        }

        public static BotOperationResult Denied(string reasonCode, string detail = null)
        {
            return new BotOperationResult { Succeeded = false, ReasonCode = reasonCode, Detail = detail };
        }

        public override string ToString()
        {
            return "BotOperationResult result=" + (this.Succeeded ? "allowed" : "denied")
                + " reason=" + (this.ReasonCode ?? string.Empty);
        }
    }

    public sealed class BotAuthorizationResult
    {
        public bool Allowed { get; set; }

        public BotScope RequiredScope { get; set; }

        public string ReasonCode { get; set; }
    }

    public sealed class BotAuditEvent
    {
        public BotAuditKind Kind { get; set; }

        public Guid? BotId { get; set; }

        public long? AccountId { get; set; }

        public long? OrganizationId { get; set; }

        public Guid? SessionId { get; set; }

        public BotOperation Operation { get; set; }

        public bool Succeeded { get; set; }

        public string ReasonCode { get; set; }

        public DateTime TimestampUtc { get; set; }

        public string AuditIdentity { get; set; }

        public override string ToString()
        {
            return "BotAuditEvent kind=" + this.Kind
                + " bot=" + (this.BotId.HasValue ? this.BotId.Value.ToString("N") : "unknown")
                + " result=" + (this.Succeeded ? "success" : "denied")
                + " reason=" + (this.ReasonCode ?? string.Empty);
        }
    }

    public interface IBotIdentityRepository
    {
        BotPrincipal FindPrincipal(Guid botId);

        BotCredentialRecord FindCredential(string publicCredentialId);

        void SavePrincipal(BotPrincipal principal);

        void SaveCredential(BotCredentialRecord credential);

        void RevokeCredential(string publicCredentialId, DateTime revokedAtUtc);

        void RevokeOtherCredentials(Guid botId, string exceptPublicCredentialId, DateTime revokedAtUtc);
    }

    public interface IBotAuditSink
    {
        void Record(BotAuditEvent auditEvent);
    }

    public interface IBotSessionValidator
    {
        BotOperationResult Validate(BotSession session);
    }

    public interface IBotChatGateway
    {
        BotOperationResult Execute(BotSession session, BotChatRequest request);
    }

    public interface IBotChatRequestHandler
    {
        BotOperationResult Handle(BotSession session, BotChatRequest request);
    }

    public interface IBotRateLimitPolicyResolver
    {
        BotRateLimitRule Resolve(string profile, BotOperation operation);
    }

    public interface IBotRateLimiter
    {
        BotRateLimitDecision TryAcquire(Guid botId, string profile, BotOperation operation, DateTime timestampUtc);
    }

    public sealed class BotRateLimitRule
    {
        public int Limit { get; set; }

        public TimeSpan Window { get; set; }
    }

    public sealed class BotRateLimitDecision
    {
        public bool Allowed { get; set; }

        public string ReasonCode { get; set; }

        public TimeSpan RetryAfter { get; set; }
    }

    public sealed class BotAuthorizationEvaluator
    {
        private static readonly IDictionary<BotOperation, BotScope> RequiredScopes =
            new Dictionary<BotOperation, BotScope>
            {
                { BotOperation.TellReceive, BotScope.TellReceive },
                { BotOperation.TellSend, BotScope.TellSend },
                { BotOperation.OrganizationRead, BotScope.OrganizationRead },
                { BotOperation.OrganizationSend, BotScope.OrganizationSend },
                { BotOperation.ChannelJoin, BotScope.ChannelJoin },
                { BotOperation.ChannelLeave, BotScope.ChannelLeave },
                { BotOperation.ChannelRead, BotScope.ChannelRead },
                { BotOperation.ChannelSend, BotScope.ChannelSend },
                { BotOperation.RosterRead, BotScope.RosterRead },
                { BotOperation.CommandReceive, BotScope.CommandReceive },
                { BotOperation.CommandExecute, BotScope.CommandExecute }
            };

        public BotAuthorizationResult Authorize(BotSession session, BotChatRequest request)
        {
            if (session == null || request == null)
            {
                return Denied(BotScope.None, "AUTHORIZATION_CONTEXT_MISSING");
            }

            BotScope requiredScope;
            if (!RequiredScopes.TryGetValue(request.Operation, out requiredScope))
            {
                return Denied(BotScope.None, "OPERATION_NOT_AUTHORIZED");
            }

            if ((session.GrantedScopes & requiredScope) != requiredScope)
            {
                return Denied(requiredScope, "SCOPE_REQUIRED");
            }

            bool organizationOperation = request.Operation == BotOperation.OrganizationRead
                || request.Operation == BotOperation.OrganizationSend
                || ((request.Operation == BotOperation.ChannelJoin
                    || request.Operation == BotOperation.ChannelLeave
                    || request.Operation == BotOperation.ChannelRead
                    || request.Operation == BotOperation.ChannelSend)
                    && request.ChannelType == 3);
            if (organizationOperation)
            {
                long? targetOrganization = request.OrganizationId;
                if (!targetOrganization.HasValue && request.ChannelType == 3)
                {
                    targetOrganization = request.ChannelId;
                }

                if (!session.OrganizationId.HasValue || !targetOrganization.HasValue)
                {
                    return Denied(requiredScope, "ORGANIZATION_CONTEXT_REQUIRED");
                }

                if (session.OrganizationId.Value != targetOrganization.Value)
                {
                    return Denied(requiredScope, "ORGANIZATION_MISMATCH");
                }
            }

            return new BotAuthorizationResult
            {
                Allowed = true,
                RequiredScope = requiredScope,
                ReasonCode = "ALLOWED"
            };
        }

        private static BotAuthorizationResult Denied(BotScope requiredScope, string reasonCode)
        {
            return new BotAuthorizationResult
            {
                Allowed = false,
                RequiredScope = requiredScope,
                ReasonCode = reasonCode
            };
        }
    }

    public sealed class NullBotAuditSink : IBotAuditSink
    {
        public void Record(BotAuditEvent auditEvent)
        {
        }
    }

    public sealed class RecordingBotAuditSink : IBotAuditSink
    {
        private readonly object sync = new object();
        private readonly List<BotAuditEvent> events = new List<BotAuditEvent>();

        public BotAuditEvent[] Events
        {
            get
            {
                lock (this.sync)
                {
                    return this.events.ToArray();
                }
            }
        }

        public void Record(BotAuditEvent auditEvent)
        {
            if (auditEvent == null)
            {
                throw new ArgumentNullException("auditEvent");
            }

            lock (this.sync)
            {
                this.events.Add(auditEvent);
            }
        }
    }
}
