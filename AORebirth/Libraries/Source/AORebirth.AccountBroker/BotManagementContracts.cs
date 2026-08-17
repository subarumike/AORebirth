namespace AORebirth.AccountBroker
{
    using System;

    using AORebirth.BotService;

    /// <summary>
    /// Future storage-backed Account Broker boundary. No production endpoint or implementation is supplied
    /// until an approved bot persistence schema exists.
    /// </summary>
    public interface IBotAccountManagementStore
    {
        BotPrincipal Create(BotCreateRequest request);

        void Disable(long owningAccountId, Guid botId);

        BotCredentialIssue RotateCredential(long owningAccountId, Guid botId);

        void AssignOrganization(long owningAccountId, Guid botId, long? organizationId);

        void UpdateScopes(long owningAccountId, Guid botId, BotScope scopes);

        BotPrincipal[] List(long owningAccountId);
    }

    public sealed class BotCreateRequest
    {
        public long OwningAccountId { get; set; }

        public string DisplayName { get; set; }

        public long? OrganizationId { get; set; }

        public BotScope Scopes { get; set; }

        public string RateLimitProfile { get; set; }
    }
}
