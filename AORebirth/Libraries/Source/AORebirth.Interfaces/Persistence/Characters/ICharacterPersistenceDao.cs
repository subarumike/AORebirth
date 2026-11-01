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
        /// <summary>The organizations row for an org id (stats Clan), or null when none exists.</summary>
        PersistedOrganizationData LoadOrganization(int organizationId);
        /// <summary>True when another organization already uses this name (case-insensitive).</summary>
        bool IsOrganizationNameTaken(string name, int exceptOrganizationId);
        /// <summary>Updates one text column of the organizations row.</summary>
        void SaveOrganizationText(int organizationId, OrganizationTextField field, string value);
        /// <summary>
        /// Sets organizations.GovernmentForm and, in the same transaction, resets the rank stat (ClanLevel) of every
        /// member except the leader to <paramref name="lowestRank"/>.
        /// </summary>
        void SaveOrganizationGoverningForm(int organizationId, int governingForm, int leaderId, int lowestRank);
        IList<PersistedItemData> LoadCarriedItems(int characterId);
        IList<PersistedItemData> LoadBankItems(int characterId);
        IList<PersistedItemData> LoadContainerItems(int containerInstanceId);
        /// <summary>The contents of several containers in one query (bags kept in the bank, read without opening them).</summary>
        IList<PersistedItemData> LoadContainerItems(IList<int> containerInstanceIds);
        IDictionary<int, string> LoadItemNames();
        IList<int> LoadUploadedNanos(int characterId);
        IList<PersistedActiveNanoData> LoadActiveNanos(int characterId);
        IList<PersistedSkillLockData> LoadSkillLocks(int characterId);
        /// <summary>Trained perk ids (charactersperks.PacketId).</summary>
        IList<int> LoadTrainedPerks(int characterId);
        /// <summary>Replaces the character's trained perk ids in one transaction.</summary>
        void SaveTrainedPerks(int characterId, IList<int> perkIds);
        /// <summary>LockPerk cooldowns (characterperklocks).</summary>
        IList<PersistedPerkLockData> LoadPerkLocks(int characterId);
        /// <summary>Replaces the character's perk locks in one transaction.</summary>
        void SavePerkLocks(int characterId, IList<PersistedPerkLockData> perkLocks);
        /// <summary>Nano line cooldowns (characternanolinelocks).</summary>
        IList<PersistedNanoLineLockData> LoadNanoLineLocks(int characterId);
        /// <summary>Replaces the character's nano line locks in one transaction.</summary>
        void SaveNanoLineLocks(int characterId, IList<PersistedNanoLineLockData> nanoLineLocks);
        /// <summary>Pet names per pet type (characterpetnames).</summary>
        IList<PersistedPetNameData> LoadPetNames(int characterId);
        /// <summary>Replaces the character's pet names in one transaction.</summary>
        void SavePetNames(int characterId, IList<PersistedPetNameData> petNames);
        /// <summary>Personal research tiers in progress (characterresearch).</summary>
        IList<PersistedResearchData> LoadResearch(int characterId);
        /// <summary>Replaces the character's research progress in one transaction.</summary>
        void SaveResearch(int characterId, IList<PersistedResearchData> research);
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
        /// <paramref name="skillLocks"/> follows the same rule for the character's skill locks.
        /// </summary>
        void SaveInventoryAndUploadedNanos(int characterId, IList<PersistedItemData> inserts,
            IList<ItemLocationData> locations, IList<int> uploadedNanoIds,
            IList<PersistedActiveNanoData>? activeNanos = null,
            IList<PersistedSkillLockData>? skillLocks = null);
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
