namespace AORebirth.Database.Dao
{
    using System;
    using System.Data;
    using AORebirth.Database.Domain.Characters;

    /// <summary>Records the creation time of a new character inside character creation's transaction.</summary>
    public static class CharacterPlayedBornDao
    {
        public static void RecordCreation(int characterId, string characterName, long createdAtUnix,
            IDbConnection connection, IDbTransaction transaction)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            new MySqlCharacterPersistenceDao(connection, transaction)
                .SaveCharacterPlayedBorn(characterId, characterName, 0, createdAtUnix);
        }
    }
}
