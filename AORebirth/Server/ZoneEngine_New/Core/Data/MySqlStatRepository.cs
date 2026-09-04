namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using MySqlConnector;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Logging;

    public sealed class MySqlStatRepository : IStatRepository
    {
        private const int CharacterStatOwnerType = (int)IdentityType.CanbeAffected;

        private const string SelectSql =
            "SELECT StatId, StatValue FROM stats "
            + "WHERE Type = @Type AND Instance = @Instance";

        private readonly IZoneLogger _logger;
        private readonly string _connectionString;

        public MySqlStatRepository(IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _connectionString = MySqlConnectionSettings.GetRequiredConnectionString();
        }

        public IReadOnlyList<StatRecord> GetForCharacter(int characterId)
        {
            if (characterId <= 0)
            {
                return [];
            }

            List<StatRecord> stats = [];
            try
            {
                using MySqlConnection connection = new MySqlConnection(_connectionString);
                connection.Open();

                using MySqlCommand command = new MySqlCommand(SelectSql, connection);
                command.Parameters.AddWithValue("@Type", CharacterStatOwnerType);
                command.Parameters.AddWithValue("@Instance", characterId);

                using MySqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    stats.Add(
                        new StatRecord
                        {
                            StatId = reader.GetInt32(reader.GetOrdinal("StatId")),
                            StatValue = reader.GetInt32(reader.GetOrdinal("StatValue"))
                        });
                }
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "StatRepository.GetForCharacter failed for {0}",
                        characterId));
                throw;
            }

            return stats;
        }
    }
}
