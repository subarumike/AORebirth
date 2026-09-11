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
        void InsertItem(PersistedItemData item);
        void UpdateItemLocation(ItemLocationData location);
        void SaveItemLocations(IList<PersistedItemData> inserts, IList<ItemLocationData> locations);
        void SaveInventoryAndUploadedNanos(int characterId, IList<PersistedItemData> inserts,
            IList<ItemLocationData> locations, IList<int> uploadedNanoIds);
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
