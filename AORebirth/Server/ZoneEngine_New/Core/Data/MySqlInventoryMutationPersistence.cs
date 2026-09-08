namespace ZoneEngine_New.Core.Data
{
    using System;
    using MySqlConnector;

    /// <summary>Transaction coordinator only; SQL remains in the existing repositories.</summary>
    public sealed class MySqlInventoryMutationPersistence : IInventoryMutationPersistence
    {
        readonly MySqlInventoryRepository _inventory;
        readonly MySqlUploadedNanoRepository _nanos;
        readonly string _connectionString;

        public MySqlInventoryMutationPersistence(MySqlInventoryRepository inventory, MySqlUploadedNanoRepository nanos)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _nanos = nanos ?? throw new ArgumentNullException(nameof(nanos));
            _connectionString = MySqlConnectionSettings.GetRequiredConnectionString();
        }

        public void Persist(InventoryMutationBatch batch)
        {
            ArgumentNullException.ThrowIfNull(batch);
            if (batch.CharacterId <= 0) throw new ArgumentOutOfRangeException(nameof(batch));
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            // The empty-container retirement guard needs gap locks even if the server's
            // session default is READ COMMITTED; keep its checked child range stable.
            using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead);
            _inventory.WritePersist(batch.Inserts, batch.Locations, connection, transaction);
            _inventory.AssertContainersEmpty(batch.EmptyContainersBeforeRetire, connection, transaction);
            _inventory.WriteStackCounts(batch.Stacks, connection, transaction);
            _nanos.WriteInsertMissing(batch.CharacterId, batch.UploadedNanoIds, connection, transaction);
            MySqlStatRepository.UpsertForCharacter(connection, transaction, batch.CharacterId, batch.FinalStats);
            try { transaction.Commit(); }
            catch (Exception exception) { throw new DatabaseCommitOutcomeUnknownException(exception); }
        }
    }
}
