namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using MySqlConnector;

    using ZoneEngine_New.Core.Logging;

    public sealed class MySqlInventoryRepository : IInventoryRepository
    {
        private const string SelectItemsSql =
            "SELECT ContainerInstance, ContainerPlacement, LowId, HighId, Quality, MultipleCount "
            + "FROM items WHERE ContainerType = @CharacterId";

        private const string SelectInstancedSql =
            "SELECT Id, ContainerInstance, ContainerPlacement, Itemtype, LowId, HighId, Quality, MultipleCount, stats "
            + "FROM instanceditems WHERE ContainerType = @CharacterId";

        private readonly IZoneLogger _logger;
        private readonly string _connectionString;

        public MySqlInventoryRepository(IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _connectionString = MySqlConnectionSettings.GetRequiredConnectionString();
        }

        public IReadOnlyList<ItemRecord> GetItemsForCharacter(int characterId)
        {
            if (characterId <= 0)
            {
                return [];
            }

            List<ItemRecord> items = [];
            try
            {
                using MySqlConnection connection = new MySqlConnection(_connectionString);
                connection.Open();

                using MySqlCommand command = new MySqlCommand(SelectItemsSql, connection);
                command.Parameters.AddWithValue("@CharacterId", characterId);

                using MySqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    items.Add(ReadItem(reader));
                }
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "InventoryRepository.GetItemsForCharacter failed for {0}",
                        characterId));
                throw;
            }

            return items;
        }

        public IReadOnlyList<InstancedItemRecord> GetInstancedItemsForCharacter(int characterId)
        {
            if (characterId <= 0)
            {
                return [];
            }

            List<InstancedItemRecord> items = [];
            try
            {
                using MySqlConnection connection = new MySqlConnection(_connectionString);
                connection.Open();

                using MySqlCommand command = new MySqlCommand(SelectInstancedSql, connection);
                command.Parameters.AddWithValue("@CharacterId", characterId);

                using MySqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    items.Add(ReadInstancedItem(reader));
                }
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "InventoryRepository.GetInstancedItemsForCharacter failed for {0}",
                        characterId));
                throw;
            }

            return items;
        }

        private static ItemRecord ReadItem(MySqlDataReader reader)
        {
            return new ItemRecord
            {
                ContainerInstance = reader.GetInt32(reader.GetOrdinal("ContainerInstance")),
                ContainerPlacement = reader.GetInt32(reader.GetOrdinal("ContainerPlacement")),
                LowId = reader.GetInt32(reader.GetOrdinal("LowId")),
                HighId = reader.GetInt32(reader.GetOrdinal("HighId")),
                Quality = reader.GetInt32(reader.GetOrdinal("Quality")),
                MultipleCount = reader.GetInt32(reader.GetOrdinal("MultipleCount"))
            };
        }

        private static InstancedItemRecord ReadInstancedItem(MySqlDataReader reader)
        {
            int statsOrdinal = reader.GetOrdinal("stats");
            byte[]? statsBlob = reader.IsDBNull(statsOrdinal)
                ? null
                : (byte[])reader.GetValue(statsOrdinal);

            return new InstancedItemRecord
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                ContainerInstance = reader.GetInt32(reader.GetOrdinal("ContainerInstance")),
                ContainerPlacement = reader.GetInt32(reader.GetOrdinal("ContainerPlacement")),
                ItemType = reader.GetInt32(reader.GetOrdinal("Itemtype")),
                LowId = reader.GetInt32(reader.GetOrdinal("LowId")),
                HighId = reader.GetInt32(reader.GetOrdinal("HighId")),
                Quality = reader.GetInt32(reader.GetOrdinal("Quality")),
                MultipleCount = reader.GetInt32(reader.GetOrdinal("MultipleCount")),
                StatsBlob = statsBlob
            };
        }
    }
}
