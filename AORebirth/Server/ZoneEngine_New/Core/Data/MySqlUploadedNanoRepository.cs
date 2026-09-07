namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using MySqlConnector;

    using ZoneEngine_New.Core.Logging;

    public sealed class MySqlUploadedNanoRepository : IUploadedNanoRepository
    {
        private const string SelectSql =
            "SELECT NanoId FROM charactersuploadednanos WHERE CharacterId = @CharacterId";

        private const string InsertSql =
            "INSERT INTO charactersuploadednanos (CharacterId, NanoId) "
            + "SELECT @CharacterId, @NanoId FROM DUAL "
            + "WHERE NOT EXISTS ("
            + "SELECT 1 FROM charactersuploadednanos "
            + "WHERE CharacterId = @CharacterId AND NanoId = @NanoId)";

        private readonly IZoneLogger _logger;
        private readonly string _connectionString;

        public MySqlUploadedNanoRepository(IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _connectionString = MySqlConnectionSettings.GetRequiredConnectionString();
        }

        public IReadOnlyList<int> GetForCharacter(int characterId)
        {
            if (characterId <= 0)
                return [];

            List<int> nanoIds = [];
            try
            {
                using MySqlConnection connection = new MySqlConnection(_connectionString);
                connection.Open();

                using MySqlCommand command = new MySqlCommand(SelectSql, connection);
                command.Parameters.AddWithValue("@CharacterId", characterId);

                using MySqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                    nanoIds.Add(reader.GetInt32(reader.GetOrdinal("NanoId")));
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "UploadedNanoRepository.GetForCharacter failed for {0}",
                        characterId));
                throw;
            }

            return nanoIds;
        }

        internal void WriteInsertMissing(
            int characterId,
            IReadOnlyList<int> nanoIds,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(nanoIds);
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);

            if (characterId <= 0 || nanoIds.Count == 0)
                return;

            for (int i = 0; i < nanoIds.Count; i++)
            {
                int nanoId = nanoIds[i];
                if (nanoId <= 0)
                    continue;

                using MySqlCommand command = new MySqlCommand(InsertSql, connection, transaction);
                command.Parameters.AddWithValue("@CharacterId", characterId);
                command.Parameters.AddWithValue("@NanoId", nanoId);
                command.ExecuteNonQuery();
            }
        }
    }
}
