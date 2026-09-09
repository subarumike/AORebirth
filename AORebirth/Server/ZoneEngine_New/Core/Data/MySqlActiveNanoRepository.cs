namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using MySqlConnector;

    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// NCU rows in <c>charactersactivenanos</c>. A flush replaces the character's whole set:
    /// NCU is small and always known in full, so there is no per-row diff to get wrong.
    /// </summary>
    public sealed class MySqlActiveNanoRepository : IActiveNanoRepository
    {
        private const string SelectSql =
            "SELECT NanoId, Strain, NanoInstance, DurationCentiseconds, ExpiresAtUtcTicks "
            + "FROM charactersactivenanos WHERE CharacterId = @CharacterId";

        private const string DeleteSql =
            "DELETE FROM charactersactivenanos WHERE CharacterId = @CharacterId";

        private const string InsertSql =
            "INSERT INTO charactersactivenanos "
            + "(CharacterId, NanoId, Strain, NanoInstance, DurationCentiseconds, ExpiresAtUtcTicks) "
            + "VALUES (@CharacterId, @NanoId, @Strain, @NanoInstance, @Duration, @ExpiresAt)";

        private readonly IZoneLogger _logger;
        private readonly string _connectionString;

        public MySqlActiveNanoRepository(IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _connectionString = MySqlConnectionSettings.GetRequiredConnectionString();
        }

        public IReadOnlyList<ActiveNanoRecord> GetForCharacter(int characterId)
        {
            if (characterId <= 0)
                return [];

            List<ActiveNanoRecord> nanos = [];
            try
            {
                using MySqlConnection connection = new MySqlConnection(_connectionString);
                connection.Open();

                using MySqlCommand command = new MySqlCommand(SelectSql, connection);
                command.Parameters.AddWithValue("@CharacterId", characterId);

                using MySqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    nanos.Add(
                        new ActiveNanoRecord
                        {
                            NanoId = reader.GetInt32(reader.GetOrdinal("NanoId")),
                            Strain = reader.GetInt32(reader.GetOrdinal("Strain")),
                            NanoInstance = reader.GetInt32(reader.GetOrdinal("NanoInstance")),
                            DurationCentiseconds = reader.GetInt32(reader.GetOrdinal("DurationCentiseconds")),
                            ExpiresAtUtcTicks = reader.GetInt64(reader.GetOrdinal("ExpiresAtUtcTicks"))
                        });
                }
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ActiveNanoRepository.GetForCharacter failed for {0}",
                        characterId));
                throw;
            }

            return nanos;
        }

        internal void WriteReplaceAll(
            int characterId,
            IReadOnlyList<ActiveNanoRecord> nanos,
            MySqlConnection connection,
            MySqlTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(nanos);
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);

            if (characterId <= 0)
                return;

            using (MySqlCommand delete = new MySqlCommand(DeleteSql, connection, transaction))
            {
                delete.Parameters.AddWithValue("@CharacterId", characterId);
                delete.ExecuteNonQuery();
            }

            for (int i = 0; i < nanos.Count; i++)
            {
                ActiveNanoRecord nano = nanos[i];
                if (nano.NanoId <= 0)
                    continue;

                using MySqlCommand insert = new MySqlCommand(InsertSql, connection, transaction);
                insert.Parameters.AddWithValue("@CharacterId", characterId);
                insert.Parameters.AddWithValue("@NanoId", nano.NanoId);
                insert.Parameters.AddWithValue("@Strain", nano.Strain);
                insert.Parameters.AddWithValue("@NanoInstance", nano.NanoInstance);
                insert.Parameters.AddWithValue("@Duration", nano.DurationCentiseconds);
                insert.Parameters.AddWithValue("@ExpiresAt", nano.ExpiresAtUtcTicks);
                insert.ExecuteNonQuery();
            }
        }
    }
}
