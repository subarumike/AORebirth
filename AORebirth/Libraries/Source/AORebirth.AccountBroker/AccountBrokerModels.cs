namespace AORebirth.AccountBroker
{
    using System;

    public sealed class CreateAccountRequest
    {
        public string IdempotencyKey { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public string Email { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }
    }

    public sealed class AccountProvisioningResult
    {
        public long IdentityId { get; set; }

        public int GameAccountId { get; set; }

        public string CanonicalUsername { get; set; }

        public string NormalizedUsername { get; set; }

        public string ProvisioningState { get; set; }

        public bool CreatedGameAccount { get; set; }
    }

    public sealed class IdentityResult
    {
        public long IdentityId { get; set; }

        public string CanonicalUsername { get; set; }

        public string NormalizedUsername { get; set; }
    }

    public sealed class GameAccountSnapshot
    {
        public int Id { get; set; }

        public string Username { get; set; }

        public string PasswordHash { get; set; }

        public int Flags { get; set; }

        public int AccountFlags { get; set; }

        public int GM { get; set; }
    }

    public sealed class ExternalMappingResult
    {
        public long IdentityId { get; set; }

        public string Provider { get; set; }

        public string ExternalAccountId { get; set; }

        public string MappingState { get; set; }
    }

    public sealed class ProvisioningStatus
    {
        public string State { get; set; }

        public int Step { get; set; }

        public long? IdentityId { get; set; }

        public int? GameAccountId { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    public sealed class WebsiteAuthenticationResult
    {
        public bool IsAuthenticated { get; set; }

        public string FailureCode { get; set; }

        public AccountIdentitySnapshot Identity { get; set; }
    }

    public sealed class AccountIdentitySnapshot
    {
        public long IdentityId { get; set; }

        public string IdentityPublicId { get; set; }

        public string CanonicalUsername { get; set; }

        public string NormalizedUsername { get; set; }

        public string CanonicalEmail { get; set; }

        public bool EmailVerified { get; set; }

        public string IdentityStatus { get; set; }

        public int GameAccountId { get; set; }

        public string GameMappingState { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
