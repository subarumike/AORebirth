namespace AORebirth.Interfaces.Persistence.Accounts
{
    /// <summary>
    /// Legacy game-account persistence only. Strings and integer bitfields pass through
    /// unchanged; comparison is governed by the configured database, not authentication policy.
    /// Persistence failures propagate, never masquerading as a missing account.
    /// Every call owns its resources. No cross-call transaction or identity workflow is implied.
    /// </summary>
    public interface IAccountDao
    {
        /// <summary>Returns the first matching account's authentication data, or null.</summary>
        GameAccountAuthenticationData LoadForAuthentication(string username);

        /// <summary>Returns the first matching account, or null. Does not normalize the username.</summary>
        GameAccountData LoadByUsername(string username);

        /// <summary>
        /// Read-only resolution with distinct missing-character, missing-name and missing-account
        /// outcomes. The two reads are not an atomic snapshot. No character entity is returned.
        /// </summary>
        GameAccountLookupResult LoadByCharacterId(int characterId);

        long CountRegisteredAccounts();

        /// <summary>
        /// True only for exactly one match, preserving the legacy existence method.
        /// Equivalence to an any-match availability check requires the governed unique-name invariant.
        /// Null does not match; empty is passed through unchanged.
        /// </summary>
        bool UsernameExists(string username);

        /// <summary>
        /// Persists all supplied values and the application's local current time.
        /// Returns the provider's affected-row count, not a generated identity.
        /// Does not hash passwords or substitute account defaults.
        /// A null command throws ArgumentNullException before acquiring resources.
        /// </summary>
        int CreateGameAccount(NewGameAccountData account);

        /// <summary>
        /// Stores the supplied hash unchanged for at most one matching account.
        /// Returns the provider's affected-row count; same-value counts depend on provider settings.
        /// Unlike the legacy helper, failures propagate instead of logging and returning zero.
        /// </summary>
        int ChangePassword(string username, string passwordHash);

        /// <summary>
        /// Persists the supplied expansion integer for matching accounts.
        /// Returns the provider's affected-row count; failures propagate instead of being swallowed.
        /// </summary>
        int SetExpansions(string username, int expansions);
    }
}
