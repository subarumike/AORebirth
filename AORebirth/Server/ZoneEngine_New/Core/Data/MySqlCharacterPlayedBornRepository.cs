namespace ZoneEngine_New.Core.Data
{
    using AORebirth.Interfaces.Persistence.Characters;
    using static SharedCharacterPersistence;

    /// <summary>Reads and writes <c>characterplayedborn</c> through the shared character persistence DAO.</summary>
    public sealed class MySqlCharacterPlayedBornRepository : ICharacterPlayedBornRepository
    {
        readonly ICharacterPersistenceDao _persistence;

        public MySqlCharacterPlayedBornRepository(ICharacterPersistenceDao? persistence = null)
            => _persistence = persistence ?? Create();

        public PersistedCharacterPlayedBornData? Load(int characterId)
            => _persistence.LoadCharacterPlayedBorn(characterId);

        public void Save(int characterId, string characterName, long playedSeconds, long? createdAtUnix)
            => _persistence.SaveCharacterPlayedBorn(characterId, characterName, playedSeconds, createdAtUnix);
    }
}
