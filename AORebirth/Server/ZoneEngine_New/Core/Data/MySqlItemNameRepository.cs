namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using MySqlConnector;

    using ZoneEngine_New.Core.Logging;

    public sealed class MySqlItemNameRepository : IItemNameRepository
    {
        private const string SelectSql = "SELECT Id, Name FROM itemnames";

        private readonly IZoneLogger _logger;
        private readonly string _connectionString;
        private readonly Dictionary<int, string> _names;

        public MySqlItemNameRepository(IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _connectionString = MySqlConnectionSettings.GetRequiredConnectionString();
            _names = LoadAll();
        }

        public bool TryGetName(int aoid, out string name)
            => _names.TryGetValue(aoid, out name!);

        public IReadOnlyDictionary<int, string> GetAllNames()
            => _names;

        private Dictionary<int, string> LoadAll()
        {
            var names = new Dictionary<int, string>();
            try
            {
                using MySqlConnection connection = new MySqlConnection(_connectionString);
                connection.Open();

                using MySqlCommand command = new MySqlCommand(SelectSql, connection);
                using MySqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    int id = reader.GetInt32(reader.GetOrdinal("Id"));
                    string name = reader.IsDBNull(reader.GetOrdinal("Name"))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal("Name"));
                    names[id] = name;
                }

                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ItemNameRepository loaded {0} names",
                        names.Count));
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "ItemNameRepository failed to load itemnames");
                throw;
            }

            return names;
        }
    }
}
