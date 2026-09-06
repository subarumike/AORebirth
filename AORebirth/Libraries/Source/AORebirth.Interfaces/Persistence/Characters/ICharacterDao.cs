namespace AORebirth.Interfaces.Persistence.Characters
{
    using System.Collections.Generic;

    /// <summary>
    /// Detached character directory and online-state persistence, not session authorization.
    /// Names are passed unchanged; matching follows the configured database collation.
    /// Persistence failures propagate and must not be interpreted as absence or offline state.
    /// </summary>
    public interface ICharacterDao
    {
        /// <summary>Returns the directory row, or null when the ID is absent.</summary>
        CharacterDirectoryData LoadById(int characterId);

        /// <summary>Returns a matching row or null. Selection among duplicate names is unspecified.</summary>
        CharacterDirectoryData LoadByName(string name);

        /// <summary>Returns a buffered, possibly empty list with no ordering guarantee.</summary>
        IList<CharacterDirectoryData> ListForAccount(string accountUsername);

        /// <summary>True only for exactly one matching account and character pair; not session authorization.</summary>
        bool IsOwnedByAccount(string accountUsername, uint characterId);

        /// <summary>
        /// Sets only this character's online value to 1 in one owned transaction.
        /// Returns the provider's affected-row count, including zero for missing rows;
        /// same-value counts depend on connection settings. A commit error may be durable.
        /// </summary>
        int MarkOnline(int characterId);

        /// <summary>Same write/error/count contract as MarkOnline, assigning 0.</summary>
        int MarkOffline(int characterId);

        /// <summary>Returns a buffered unordered list whose online value equals exactly 1.</summary>
        IList<CharacterDirectoryData> ListLoggedIn();

        /// <summary>
        /// Atomically clears captured nonzero online values after an exact expected-database check.
        /// The caller must establish exclusive runtime recovery safety before calling; this method
        /// does not check processes, ports or session ownership. Commit errors require a fresh read
        /// to reconcile the outcome before retrying. Empty captures end without a write or commit.
        /// </summary>
        StaleOnlineRecoveryData RecoverStaleOnline(string expectedDatabase);
    }
}
