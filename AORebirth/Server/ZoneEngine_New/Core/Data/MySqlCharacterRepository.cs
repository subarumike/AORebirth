namespace ZoneEngine_New.Core.Data
{
    using System;
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
            "SELECT Id, Name, Playfield, X, Y, Z, HeadingW, HeadingX, HeadingY, HeadingZ "
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

            Config config = ConfigReadWrite.Instance.CurrentConfig;
            string? connectionString = config?.MysqlConnection;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("MysqlConnection is not configured.");
            }

            _connectionString = connectionString;
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

                using MySqlCommand command = new MySqlCommand(UpdateLocationSql, connection);
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

                int updated = command.ExecuteNonQuery();
                if (updated == 0)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "CharacterRepository.SaveLocation found no row for {0}",
                            character.Id));
                }
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
    }
}
