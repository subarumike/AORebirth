namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using MySqlConnector;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Logging;

    public sealed class MySqlInventoryRepository : IInventoryRepository
    {
        private static readonly string SelectCarriedSql =
            "SELECT InstanceId, ContainerType, ContainerInstance, ContainerPlacement, ItemType, "
            + "LowId, HighId, Quality, StackCount "
            + "FROM item_instances WHERE ContainerInstance = @CharacterId "
            + "AND ContainerType IN ("
            + (int)IdentityType.Inventory + ", "
            + (int)IdentityType.WeaponPage + ", "
            + (int)IdentityType.ArmorPage + ", "
            + (int)IdentityType.ImplantPage + ", "
            + (int)IdentityType.SocialPage + ")";

        private static readonly string SelectBankSql =
            "SELECT InstanceId, ContainerType, ContainerInstance, ContainerPlacement, ItemType, "
            + "LowId, HighId, Quality, StackCount "
            + "FROM item_instances WHERE ContainerType = "
            + (int)IdentityType.Bank
            + " AND ContainerInstance = @CharacterId";

        private static readonly string SelectContainerSql =
            "SELECT InstanceId, ContainerType, ContainerInstance, ContainerPlacement, ItemType, "
            + "LowId, HighId, Quality, StackCount "
            + "FROM item_instances WHERE ContainerType = "
            + (int)IdentityType.Container
            + " AND ContainerInstance = @ContainerInstanceId";

        private const string InsertSql =
            "INSERT INTO item_instances "
            + "(ContainerType, ContainerInstance, ContainerPlacement, ItemType, LowId, HighId, Quality, StackCount) "
            + "VALUES (@ContainerType, @ContainerInstance, @ContainerPlacement, @ItemType, @LowId, @HighId, @Quality, @StackCount)";

        private const string UpdateLocationSql =
            "UPDATE item_instances SET ContainerType = @ContainerType, "
            + "ContainerInstance = @ContainerInstance, ContainerPlacement = @ContainerPlacement "
            + "WHERE InstanceId = @InstanceId";

        private readonly IZoneLogger _logger;
        private readonly string _connectionString;

        public MySqlInventoryRepository(IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _connectionString = MySqlConnectionSettings.GetRequiredConnectionString();
        }

        public IReadOnlyList<ItemInstanceRecord> GetCarriedItems(int characterId)
        {
            if (characterId <= 0)
                return [];

            return Query(
                SelectCarriedSql,
                "@CharacterId",
                characterId,
                "GetCarriedItems");
        }

        public IReadOnlyList<ItemInstanceRecord> GetBankItems(int characterId)
        {
            if (characterId <= 0)
                return [];

            return Query(
                SelectBankSql,
                "@CharacterId",
                characterId,
                "GetBankItems");
        }

        public IReadOnlyList<ItemInstanceRecord> GetContainerItems(int containerInstanceId)
        {
            if (containerInstanceId <= 0)
                return [];

            return Query(
                SelectContainerSql,
                "@ContainerInstanceId",
                containerInstanceId,
                "GetContainerItems");
        }

        public ItemInstanceRecord Insert(ItemInstanceRecord item)
        {
            ArgumentNullException.ThrowIfNull(item);

            try
            {
                using MySqlConnection connection = new MySqlConnection(_connectionString);
                connection.Open();

                using MySqlCommand command = new MySqlCommand(InsertSql, connection);
                command.Parameters.AddWithValue("@ContainerType", item.ContainerType);
                command.Parameters.AddWithValue("@ContainerInstance", item.ContainerInstance);
                command.Parameters.AddWithValue("@ContainerPlacement", item.ContainerPlacement);
                command.Parameters.AddWithValue("@ItemType", item.ItemType);
                command.Parameters.AddWithValue("@LowId", item.LowId);
                command.Parameters.AddWithValue("@HighId", item.HighId);
                command.Parameters.AddWithValue("@Quality", item.Quality);
                command.Parameters.AddWithValue("@StackCount", item.StackCount);

                command.ExecuteNonQuery();
                int instanceId = checked((int)command.LastInsertedId);
                if (instanceId <= 0)
                    throw new InvalidOperationException("Insert did not return a positive InstanceId.");

                return new ItemInstanceRecord
                {
                    InstanceId = instanceId,
                    ContainerType = item.ContainerType,
                    ContainerInstance = item.ContainerInstance,
                    ContainerPlacement = item.ContainerPlacement,
                    ItemType = item.ItemType,
                    LowId = item.LowId,
                    HighId = item.HighId,
                    Quality = item.Quality,
                    StackCount = item.StackCount
                };
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "InventoryRepository.Insert failed low={0} placement={1}",
                        item.LowId,
                        item.ContainerPlacement));
                throw;
            }
        }

        public void UpdateLocation(int instanceId, int containerType, int containerInstance, int containerPlacement)
        {
            if (instanceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(instanceId));

            try
            {
                using MySqlConnection connection = new MySqlConnection(_connectionString);
                connection.Open();

                using MySqlCommand command = new MySqlCommand(UpdateLocationSql, connection);
                command.Parameters.AddWithValue("@InstanceId", instanceId);
                command.Parameters.AddWithValue("@ContainerType", containerType);
                command.Parameters.AddWithValue("@ContainerInstance", containerInstance);
                command.Parameters.AddWithValue("@ContainerPlacement", containerPlacement);

                int rows = command.ExecuteNonQuery();
                if (rows == 0)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "UpdateLocation found no row for InstanceId={0}",
                            instanceId));
                }
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "InventoryRepository.UpdateLocation failed for {0}",
                        instanceId));
                throw;
            }
        }

        private IReadOnlyList<ItemInstanceRecord> Query(
            string sql,
            string parameterName,
            int parameterValue,
            string operation)
        {
            List<ItemInstanceRecord> items = [];
            try
            {
                using MySqlConnection connection = new MySqlConnection(_connectionString);
                connection.Open();

                using MySqlCommand command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue(parameterName, parameterValue);

                using MySqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                    items.Add(ReadItem(reader));
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "InventoryRepository.{0} failed for {1}",
                        operation,
                        parameterValue));
                throw;
            }

            return items;
        }

        private static ItemInstanceRecord ReadItem(MySqlDataReader reader)
        {
            return new ItemInstanceRecord
            {
                InstanceId = reader.GetInt32(reader.GetOrdinal("InstanceId")),
                ContainerType = reader.GetInt32(reader.GetOrdinal("ContainerType")),
                ContainerInstance = reader.GetInt32(reader.GetOrdinal("ContainerInstance")),
                ContainerPlacement = reader.GetInt32(reader.GetOrdinal("ContainerPlacement")),
                ItemType = reader.GetInt32(reader.GetOrdinal("ItemType")),
                LowId = reader.GetInt32(reader.GetOrdinal("LowId")),
                HighId = reader.GetInt32(reader.GetOrdinal("HighId")),
                Quality = reader.GetInt32(reader.GetOrdinal("Quality")),
                StackCount = reader.GetInt32(reader.GetOrdinal("StackCount"))
            };
        }
    }
}
