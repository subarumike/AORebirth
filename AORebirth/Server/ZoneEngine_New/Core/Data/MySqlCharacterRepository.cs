namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Globalization;

    using MySqlConnector;

    using Utility.Config;

    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// Thin MySQL read of characters for ZoneEngine_New (net10). Same table/columns as CharacterDao.
    /// </summary>
    public sealed class MySqlCharacterRepository : ICharacterRepository
    {
        private const string SelectSql =
            "SELECT Id, Name, Playfield, X, Y, Z, HeadingW, HeadingX, HeadingY, HeadingZ "
            + "FROM characters WHERE Id = @Id LIMIT 1";

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
    }
}
