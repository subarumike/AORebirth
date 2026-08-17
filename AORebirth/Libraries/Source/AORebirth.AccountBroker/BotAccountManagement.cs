namespace AORebirth.AccountBroker
{
    using System;

    using AORebirth.BotService;

    public sealed class BotManagementContext
    {
        public long AuthenticatedIdentityId { get; set; }

        public string AuditIdentity { get; set; }
    }

    public sealed class BotManagementCreateRequest
    {
        public string DisplayName { get; set; }

        public long? OrganizationId { get; set; }

        public BotScope Scopes { get; set; }

        public string RateLimitProfile { get; set; }
    }

    public sealed class BotManagementResult
    {
        public BotPrincipal Principal { get; set; }

        public string OneTimeCredential { get; set; }

        public string PublicCredentialId { get; set; }
    }

    public interface IBotOrganizationAuthority
    {
        bool CanAssign(long authenticatedIdentityId, long organizationId);
    }

    public interface IBotScopePolicy
    {
        void Validate(BotScope scopes, long? organizationId);
    }

    public sealed class DefaultBotScopePolicy : IBotScopePolicy
    {
        private const BotScope AllKnownScopes = BotScope.TellReceive
            | BotScope.TellSend
            | BotScope.OrganizationRead
            | BotScope.OrganizationSend
            | BotScope.ChannelJoin
            | BotScope.ChannelLeave
            | BotScope.ChannelRead
            | BotScope.ChannelSend
            | BotScope.RosterRead
            | BotScope.CommandReceive
            | BotScope.CommandExecute;

        public void Validate(BotScope scopes, long? organizationId)
        {
            if (scopes == BotScope.None || (scopes & ~AllKnownScopes) != BotScope.None)
            {
                throw new InvalidOperationException("The requested bot scope set is invalid.");
            }

            BotScope organizationScopes = BotScope.OrganizationRead | BotScope.OrganizationSend;
            if ((scopes & organizationScopes) != BotScope.None && !organizationId.HasValue)
            {
                throw new InvalidOperationException("Organization scopes require an organization assignment.");
            }
        }
    }

    public sealed class BotAccountManagementService
    {
        private readonly IPersistentBotRepository repository;
        private readonly PersistentBotCredentialIssuer credentials;
        private readonly IBotOrganizationAuthority organizationAuthority;
        private readonly IBotScopePolicy scopePolicy;
        private readonly Func<DateTime> utcNow;

        public BotAccountManagementService(
            IPersistentBotRepository repository,
            PersistentBotCredentialIssuer credentials,
            IBotOrganizationAuthority organizationAuthority,
            IBotScopePolicy scopePolicy,
            Func<DateTime> utcNow = null)
        {
            this.repository = repository ?? throw new ArgumentNullException("repository");
            this.credentials = credentials ?? throw new ArgumentNullException("credentials");
            this.organizationAuthority = organizationAuthority ?? throw new ArgumentNullException("organizationAuthority");
            this.scopePolicy = scopePolicy ?? throw new ArgumentNullException("scopePolicy");
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public BotManagementResult Create(BotManagementContext context, BotManagementCreateRequest request)
        {
            RequireContext(context);
            if (request == null || string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length > 32)
            {
                throw new ArgumentException("A bot display name of 1-32 characters is required.", "request");
            }

            this.RequireOrganizationAuthority(context, request.OrganizationId);
            this.scopePolicy.Validate(request.Scopes, request.OrganizationId);
            DateTime now = this.utcNow();
            BotPrincipal principal = new BotPrincipal
            {
                BotId = Guid.NewGuid(),
                DisplayName = request.DisplayName.Trim(),
                OwningAccountId = context.AuthenticatedIdentityId,
                OrganizationId = request.OrganizationId,
                Enabled = true,
                CurrentCredentialVersion = 1,
                Scopes = request.Scopes,
                RateLimitProfile = string.IsNullOrWhiteSpace(request.RateLimitProfile) ? "default" : request.RateLimitProfile,
                AuditIdentity = "bot:" + context.AuthenticatedIdentityId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            BotCredentialRecord credential;
            BotCredentialIssue issue = this.credentials.Issue(principal.BotId, 1, out credential);
            this.repository.Create(principal, credential, Audit(context, principal, BotAuditKind.PrincipalCreated, "BOT_CREATED", now));
            return new BotManagementResult
            {
                Principal = principal.Copy(),
                OneTimeCredential = issue.Credential,
                PublicCredentialId = issue.PublicCredentialId
            };
        }

        public BotPrincipal[] List(BotManagementContext context)
        {
            RequireContext(context);
            return this.repository.ListPrincipals(context.AuthenticatedIdentityId);
        }

        public BotPrincipal Get(BotManagementContext context, Guid botId)
        {
            RequireContext(context);
            BotPrincipal principal = this.repository.FindPrincipal(botId);
            if (principal == null || principal.OwningAccountId != context.AuthenticatedIdentityId)
            {
                throw new InvalidOperationException("Bot principal was not found for the authenticated owner.");
            }

            return principal;
        }

        public void Disable(BotManagementContext context, Guid botId)
        {
            this.SetEnabled(context, botId, false);
        }

        public void Enable(BotManagementContext context, Guid botId)
        {
            this.SetEnabled(context, botId, true);
        }

        public BotManagementResult RotateCredential(BotManagementContext context, Guid botId)
        {
            BotPrincipal principal = this.Get(context, botId);
            DateTime now = this.utcNow();
            principal.CurrentCredentialVersion = checked(principal.CurrentCredentialVersion + 1);
            principal.UpdatedAtUtc = now;
            BotCredentialRecord credential;
            BotCredentialIssue issue = this.credentials.Issue(principal.BotId, principal.CurrentCredentialVersion, out credential);
            this.repository.Rotate(
                context.AuthenticatedIdentityId,
                principal,
                credential,
                Audit(context, principal, BotAuditKind.CredentialRotated, "CREDENTIAL_ROTATED", now));
            return new BotManagementResult
            {
                Principal = principal.Copy(),
                OneTimeCredential = issue.Credential,
                PublicCredentialId = issue.PublicCredentialId
            };
        }

        public void RevokeCredentials(BotManagementContext context, Guid botId)
        {
            BotPrincipal principal = this.Get(context, botId);
            DateTime now = this.utcNow();
            this.repository.RevokeAll(
                context.AuthenticatedIdentityId,
                botId,
                now,
                Audit(context, principal, BotAuditKind.CredentialRevoked, "CREDENTIALS_REVOKED", now));
        }

        public void UpdateScopes(BotManagementContext context, Guid botId, BotScope scopes)
        {
            BotPrincipal principal = this.Get(context, botId);
            this.scopePolicy.Validate(scopes, principal.OrganizationId);
            DateTime now = this.utcNow();
            this.repository.ReplaceScopes(
                context.AuthenticatedIdentityId,
                botId,
                scopes,
                Audit(context, principal, BotAuditKind.ScopesReplaced, "SCOPES_REPLACED", now));
        }

        public void AssignOrganization(BotManagementContext context, Guid botId, long? organizationId)
        {
            BotPrincipal principal = this.Get(context, botId);
            this.RequireOrganizationAuthority(context, organizationId);
            this.scopePolicy.Validate(principal.Scopes, organizationId);
            DateTime now = this.utcNow();
            this.repository.AssignOrganization(
                context.AuthenticatedIdentityId,
                botId,
                organizationId,
                Audit(context, principal, BotAuditKind.OrganizationAssigned, "ORGANIZATION_ASSIGNED", now));
        }

        public BotAuditEvent[] Audit(BotManagementContext context, Guid botId, int maximumCount)
        {
            this.Get(context, botId);
            return this.repository.ListAuditEvents(botId, maximumCount);
        }

        private void SetEnabled(BotManagementContext context, Guid botId, bool enabled)
        {
            BotPrincipal principal = this.Get(context, botId);
            DateTime now = this.utcNow();
            this.repository.SetEnabled(
                context.AuthenticatedIdentityId,
                botId,
                enabled,
                Audit(
                    context,
                    principal,
                    enabled ? BotAuditKind.PrincipalEnabled : BotAuditKind.PrincipalDisabled,
                    enabled ? "BOT_ENABLED" : "BOT_DISABLED",
                    now));
        }

        private void RequireOrganizationAuthority(BotManagementContext context, long? organizationId)
        {
            if (organizationId.HasValue && !this.organizationAuthority.CanAssign(context.AuthenticatedIdentityId, organizationId.Value))
            {
                throw new InvalidOperationException("The authenticated account cannot assign this organization.");
            }
        }

        private static void RequireContext(BotManagementContext context)
        {
            if (context == null || context.AuthenticatedIdentityId < 1)
            {
                throw new InvalidOperationException("An authenticated account identity is required.");
            }
        }

        private static BotAuditEvent Audit(
            BotManagementContext context,
            BotPrincipal principal,
            BotAuditKind kind,
            string reasonCode,
            DateTime timestampUtc)
        {
            return new BotAuditEvent
            {
                Kind = kind,
                BotId = principal.BotId,
                AccountId = context.AuthenticatedIdentityId,
                OrganizationId = principal.OrganizationId,
                Succeeded = true,
                ReasonCode = reasonCode,
                TimestampUtc = timestampUtc,
                AuditIdentity = context.AuditIdentity
            };
        }
    }
}
