namespace AORebirth.Interfaces.Persistence.Characters
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Authoritative storage operations used by full gameplay hydration and character/item saves.
    /// Directory/admission ownership remains ICharacterDao. Callers serialize current-session
    /// snapshots and mutations under their existing persistence ownership before invoking writes.
    /// Compound methods commit once; they never acknowledge or replay an indeterminate commit.
    /// </summary>
    public interface ICharacterPersistenceDao
    {
        CharacterStateData LoadCharacter(int characterId);
        IList<CharacterStatData> LoadStats(int characterId);
        IList<PersistedItemData> LoadCarriedItems(int characterId);
        IList<PersistedItemData> LoadBankItems(int characterId);
        IList<PersistedItemData> LoadContainerItems(int containerInstanceId);
        IDictionary<int, string> LoadItemNames();
        IList<int> LoadUploadedNanos(int characterId);
        IList<PersistedActiveNanoData> LoadActiveNanos(int characterId);
        int LeaseItemInstanceIds(int count);
        void SaveLocation(CharacterStateData character, int online);
        void SaveSnapshot(CharacterStateData character, int online, IList<CharacterStatData> stats);
        void SaveStats(int characterId, IList<CharacterStatData> stats);
        /// <summary>
        /// Online write-behind checkpoint. Locks the character row and writes only while it is still
        /// marked online, so a checkpoint can never land after the logout snapshot; returns false when
        /// skipped. A null <paramref name="location"/> leaves the stored location untouched. Never writes Online.
        /// </summary>
        bool SaveOnlineCheckpoint(int characterId, CharacterStateData location, IList<CharacterStatData> stats);
        void InsertItem(PersistedItemData item);
        void UpdateItemLocation(ItemLocationData location);
        void SaveItemLocations(IList<PersistedItemData> inserts, IList<ItemLocationData> locations);
        /// <summary>
        /// <paramref name="activeNanos"/> null means NCU did not change and must not be touched;
        /// a non-null list (including an empty one) replaces the character's stored NCU.
        /// </summary>
        void SaveInventoryAndUploadedNanos(int characterId, IList<PersistedItemData> inserts,
            IList<ItemLocationData> locations, IList<int> uploadedNanoIds,
            IList<PersistedActiveNanoData>? activeNanos = null);
        void CommitInventoryMutation(CharacterInventoryMutationData mutation);
        // Retains the existing item/credit boundary; this does not implement a new trade feature.
        void CommitItemCredits(ItemCreditMutationData mutation);
        // Retains active-nano/base-stat coupling without moving gameplay nano policy into storage.
        void CommitActiveNanos(IList<CharacterActiveNanoData> characters);
    }

    /// <summary>COMMIT was attempted but its durable outcome was not acknowledged. Never blindly retry.</summary>
    public sealed class CharacterPersistenceCommitOutcomeUnknownException : Exception
    {
        public CharacterPersistenceCommitOutcomeUnknownException(Exception inner)
            : base("Character persistence commit outcome is unknown; authoritative reload is required.", inner) { }
    }
}
