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

        private const string LeaseAdvanceSql =
            "UPDATE item_instance_id_sequence "
            + "SET NextInstanceId = LAST_INSERT_ID(NextInstanceId) + @Count "
            + "WHERE Id = 1";

        private const string LeaseStartSql = "SELECT LAST_INSERT_ID()";

        private const string InsertSql =
            "INSERT INTO item_instances "
            + "(InstanceId, ContainerType, ContainerInstance, ContainerPlacement, ItemType, LowId, HighId, Quality, StackCount) "
            + "VALUES (@InstanceId, @ContainerType, @ContainerInstance, @ContainerPlacement, @ItemType, @LowId, @HighId, @Quality, @StackCount)";

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

        public int LeaseInstanceIdBlock(int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            try
            {
                using MySqlConnection connection = new MySqlConnection(_connectionString);
                connection.Open();
                using MySqlTransaction transaction = connection.BeginTransaction();

                using (MySqlCommand advance = new MySqlCommand(LeaseAdvanceSql, connection, transaction))
                {
                    advance.Parameters.AddWithValue("@Count", count);
                    if (advance.ExecuteNonQuery() == 0)
                    {
                        throw new InvalidOperationException(
                            "item_instance_id_sequence row Id=1 is missing; apply SqlTables/migrations.");
                    }
                }

                int start;
                using (MySqlCommand readStart = new MySqlCommand(LeaseStartSql, connection, transaction))
                {
                    object? result = readStart.ExecuteScalar();
                    if (result == null || result is DBNull)
                        throw new InvalidOperationException("LeaseInstanceIdBlock could not read LAST_INSERT_ID().");

                    start = Convert.ToInt32(result, CultureInfo.InvariantCulture);
                }

                if (start <= 0)
                    throw new InvalidOperationException("LeaseInstanceIdBlock returned a non-positive start.");

                transaction.Commit();
                return start;
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "InventoryRepository.LeaseInstanceIdBlock failed count={0}",
                        count));
                throw;
            }
        }

        public ItemInstanceRecord Insert(ItemInstanceRecord item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item.InstanceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(item), "Insert requires a pre-allocated InstanceId.");

            try
            {
                using MySqlConnection connection = new MySqlConnection(_connectionString);
                connection.Open();
                ExecuteInsert(item, connection, transaction: null);
                return item;
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "InventoryRepository.Insert failed id={0} low={1} placement={2}",
                        item.InstanceId,
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

        public void UpdateLocations(IReadOnlyList<ItemLocationUpdate> locations)
        {
            PersistNewAndUpdateLocations([], locations);
        }

        public void PersistNewAndUpdateLocations(
            IReadOnlyList<ItemInstanceRecord> inserts,
            IReadOnlyList<ItemLocationUpdate> updates)
        {
            ArgumentNullException.ThrowIfNull(inserts);
            ArgumentNullException.ThrowIfNull(updates);
            if (inserts.Count == 0 && updates.Count == 0)
                return;

            try
            {
                using MySqlConnection connection = new MySqlConnection(_connectionString);
                connection.Open();
                using MySqlTransaction transaction = connection.BeginTransaction();

                for (int i = 0; i < inserts.Count; i++)
                {
                    ItemInstanceRecord item = inserts[i];
                    if (item.InstanceId <= 0)
                        throw new ArgumentOutOfRangeException(nameof(inserts));
                    ExecuteInsert(item, connection, transaction);
                }

                // Park updates into unique negative placements first so swaps cannot collide.
                for (int i = 0; i < updates.Count; i++)
                {
                    ItemLocationUpdate update = updates[i];
                    if (update.InstanceId <= 0)
                        throw new ArgumentOutOfRangeException(nameof(updates));

                    using MySqlCommand park = new MySqlCommand(UpdateLocationSql, connection, transaction);
                    park.Parameters.AddWithValue("@InstanceId", update.InstanceId);
                    park.Parameters.AddWithValue("@ContainerType", update.ContainerType);
                    park.Parameters.AddWithValue("@ContainerInstance", update.ContainerInstance);
                    park.Parameters.AddWithValue("@ContainerPlacement", -(i + 1));
                    if (park.ExecuteNonQuery() == 0)
                    {
                        throw new InvalidOperationException(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "UpdateLocations park found no row for InstanceId={0}",
                                update.InstanceId));
                    }
                }

                foreach (ItemLocationUpdate update in updates)
                {
                    using MySqlCommand commit = new MySqlCommand(UpdateLocationSql, connection, transaction);
                    commit.Parameters.AddWithValue("@InstanceId", update.InstanceId);
                    commit.Parameters.AddWithValue("@ContainerType", update.ContainerType);
                    commit.Parameters.AddWithValue("@ContainerInstance", update.ContainerInstance);
                    commit.Parameters.AddWithValue("@ContainerPlacement", update.ContainerPlacement);
                    if (commit.ExecuteNonQuery() == 0)
                    {
                        throw new InvalidOperationException(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "UpdateLocations commit found no row for InstanceId={0}",
                                update.InstanceId));
                    }
                }

                transaction.Commit();
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "InventoryRepository.PersistNewAndUpdateLocations failed");
                throw;
            }
        }

        private static void ExecuteInsert(
            ItemInstanceRecord item,
            MySqlConnection connection,
            MySqlTransaction? transaction)
        {
            using MySqlCommand command = transaction == null
                ? new MySqlCommand(InsertSql, connection)
                : new MySqlCommand(InsertSql, connection, transaction);
            command.Parameters.AddWithValue("@InstanceId", item.InstanceId);
            command.Parameters.AddWithValue("@ContainerType", item.ContainerType);
            command.Parameters.AddWithValue("@ContainerInstance", item.ContainerInstance);
            command.Parameters.AddWithValue("@ContainerPlacement", item.ContainerPlacement);
            command.Parameters.AddWithValue("@ItemType", item.ItemType);
            command.Parameters.AddWithValue("@LowId", item.LowId);
            command.Parameters.AddWithValue("@HighId", item.HighId);
            command.Parameters.AddWithValue("@Quality", item.Quality);
            command.Parameters.AddWithValue("@StackCount", item.StackCount);
            command.ExecuteNonQuery();
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
