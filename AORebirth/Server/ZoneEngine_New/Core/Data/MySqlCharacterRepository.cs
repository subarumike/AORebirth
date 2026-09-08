namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using MySqlConnector;

    using Utility.Config;

    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// Thin MySQL characters access for ZoneEngine_New (net10). Same table/columns as CharacterDao.
    /// </summary>
    public sealed class MySqlCharacterRepository : ICharacterRepository
    {
        private const string SelectSql =
            "SELECT Id, Name, FirstName, LastName, Playfield, X, Y, Z, HeadingW, HeadingX, HeadingY, HeadingZ "
            + "FROM characters WHERE Id = @Id LIMIT 1";

        private const string UpdateLocationSql =
            "UPDATE characters SET Playfield = @Playfield, X = @X, Y = @Y, Z = @Z, "
            + "HeadingW = @HeadingW, HeadingX = @HeadingX, HeadingY = @HeadingY, HeadingZ = @HeadingZ, "
            + "Online = @Online WHERE Id = @Id";

        private readonly IZoneLogger _logger;
        private readonly string _connectionString;

        public MySqlCharacterRepository(IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            _logger = logger;

            _connectionString = MySqlConnectionSettings.GetRequiredConnectionString();
        }

        public CharacterRecord? GetById(int characterId)
        {
            if (characterId <= 0)
            {
                return null;
            }

            try
            {
                using MySqlConnection connection = new MySqlConnection(_connectionString);
                connection.Open();

                using MySqlCommand command = new MySqlCommand(SelectSql, connection);
                command.Parameters.AddWithValue("@Id", characterId);

                using MySqlDataReader reader = command.ExecuteReader();
                if (!reader.Read())
                {
                    return null;
                }

                return new CharacterRecord
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    Name = reader.IsDBNull(reader.GetOrdinal("Name"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("Name")),
                    FirstName = reader.IsDBNull(reader.GetOrdinal("FirstName"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("FirstName")),
                    LastName = reader.IsDBNull(reader.GetOrdinal("LastName"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("LastName")),
                    Playfield = reader.GetInt32(reader.GetOrdinal("Playfield")),
                    X = reader.GetFloat(reader.GetOrdinal("X")),
                    Y = reader.GetFloat(reader.GetOrdinal("Y")),
                    Z = reader.GetFloat(reader.GetOrdinal("Z")),
                    HeadingW = reader.GetFloat(reader.GetOrdinal("HeadingW")),
                    HeadingX = reader.GetFloat(reader.GetOrdinal("HeadingX")),
                    HeadingY = reader.GetFloat(reader.GetOrdinal("HeadingY")),
                    HeadingZ = reader.GetFloat(reader.GetOrdinal("HeadingZ"))
                };
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "CharacterRepository.GetById failed for {0}",
                        characterId));
                throw;
            }
        }

        public void SaveLocation(CharacterRecord character, int online)
        {
            ArgumentNullException.ThrowIfNull(character);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(character.Id);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(character.Playfield);

            try
            {
                using MySqlConnection connection = new MySqlConnection(_connectionString);
                connection.Open();

                SaveLocation(connection, null, character, online);
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "CharacterRepository.SaveLocation failed for {0}",
                        character.Id));
                throw;
            }
        }

        public void SetOnline(int characterId)
            => SetOnlineState(characterId, 1);

        internal static void LockForTransaction(MySqlConnection connection, MySqlTransaction transaction, int characterId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(characterId);
            using var row = new MySqlCommand("SELECT Id FROM characters WHERE Id=@Id FOR UPDATE", connection, transaction);
            row.Parameters.AddWithValue("@Id", characterId);
            if (row.ExecuteScalar() == null)
                throw new InvalidOperationException("Transaction participant no longer exists.");
        }

        public void SetOffline(int characterId)
            => SetOnlineState(characterId, 0);

        private void SetOnlineState(int characterId, int online)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(characterId);
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var command = new MySqlCommand("UPDATE characters SET Online = @Online WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", characterId);
            command.Parameters.AddWithValue("@Online", online);
            if (command.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("Character row is missing during online ownership acquisition.");
        }

        public void SaveSnapshot(CharacterRecord character, int online, IReadOnlyList<StatRecord> stats)
        {
            ArgumentNullException.ThrowIfNull(character);
            ArgumentNullException.ThrowIfNull(stats);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(character.Id);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(character.Playfield);

            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using MySqlTransaction transaction = connection.BeginTransaction();
            SaveLocation(connection, transaction, character, online);
            MySqlStatRepository.UpsertForCharacter(connection, transaction, character.Id, stats);
            try
            {
                transaction.Commit();
            }
            catch (Exception exception)
            {
                throw new DatabaseCommitOutcomeUnknownException(exception);
            }
        }

        private static void SaveLocation(MySqlConnection connection, MySqlTransaction? transaction,
            CharacterRecord character, int online)
        {
            using var command = new MySqlCommand(UpdateLocationSql, connection, transaction);
            command.Parameters.AddWithValue("@Id", character.Id);
            command.Parameters.AddWithValue("@Playfield", character.Playfield);
            command.Parameters.AddWithValue("@X", character.X);
            command.Parameters.AddWithValue("@Y", character.Y);
            command.Parameters.AddWithValue("@Z", character.Z);
            command.Parameters.AddWithValue("@HeadingW", character.HeadingW);
            command.Parameters.AddWithValue("@HeadingX", character.HeadingX);
            command.Parameters.AddWithValue("@HeadingY", character.HeadingY);
            command.Parameters.AddWithValue("@HeadingZ", character.HeadingZ);
            command.Parameters.AddWithValue("@Online", online);
            if (command.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("Character row is missing during snapshot persistence.");
        }
    }
}
