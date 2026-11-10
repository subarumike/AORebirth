namespace ZoneEngine_New.Core.Data
{
    using AORebirth.Interfaces.Persistence.Characters;

    /// <summary>Played time and creation time of characters (table <c>characterplayedborn</c>).</summary>
    public interface ICharacterPlayedBornRepository
    {
        /// <summary>Stored played time and creation time of one character, or null when no row exists.</summary>
        PersistedCharacterPlayedBornData? Load(int characterId);

        /// <summary>Stores the played time; the creation time is kept once recorded.</summary>
        void Save(int characterId, string characterName, long playedSeconds, long? createdAtUnix);
    }
}
