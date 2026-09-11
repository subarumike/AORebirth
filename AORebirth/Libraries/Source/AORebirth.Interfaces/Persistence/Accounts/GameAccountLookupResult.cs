namespace AORebirth.Interfaces.Persistence.Accounts
{
    /// <summary>Outcome of read-only character-to-account resolution; provider failures throw instead.</summary>
    public enum GameAccountLookupStatus
    {
        CharacterNotFound,
        CharacterUsernameMissing,
        AccountNotFound,
        Found
    }

    /// <summary>
    /// Preserves the resolution outcome and original account name without exposing a character entity.
    /// Account is present only for Found. Missing character has no name; a missing name stays null/empty.
    /// </summary>
    public sealed class GameAccountLookupResult
    {
        public GameAccountLookupResult(
            GameAccountLookupStatus status, string characterUsername, GameAccountData account)
        {
            this.Status = status;
            this.CharacterUsername = characterUsername;
            this.Account = account;
        }

        public GameAccountLookupStatus Status { get; private set; }
        public string CharacterUsername { get; private set; }
        public GameAccountData Account { get; private set; }
    }
}
