namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using MySqlConnector;

    using ZoneEngine_New.Core.Logging;

    public sealed class MySqlCharacterCoalesceCommit : ICharacterCoalesceCommit
    {
        private readonly MySqlInventoryRepository _inventory;
        private readonly MySqlUploadedNanoRepository _nanos;
        private readonly IZoneLogger _logger;
        private readonly string _connectionString;

        public MySqlCharacterCoalesceCommit(
            MySqlInventoryRepository inventory,
            MySqlUploadedNanoRepository nanos,
            IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(inventory);
            ArgumentNullException.ThrowIfNull(nanos);
            ArgumentNullException.ThrowIfNull(logger);

            _inventory = inventory;
            _nanos = nanos;
            _logger = logger;
            _connectionString = MySqlConnectionSettings.GetRequiredConnectionString();
        }

        public void Persist(
            IReadOnlyList<ItemInstanceRecord> inserts,
            IReadOnlyList<ItemLocationUpdate> updates,
            int characterId,
            IReadOnlyList<int> uploadedNanoIds)
        {
            ArgumentNullException.ThrowIfNull(inserts);
            ArgumentNullException.ThrowIfNull(updates);
            ArgumentNullException.ThrowIfNull(uploadedNanoIds);

            if (inserts.Count == 0 && updates.Count == 0 && uploadedNanoIds.Count == 0)
                return;

            try
            {
                using MySqlConnection connection = new MySqlConnection(_connectionString);
                connection.Open();
                using MySqlTransaction transaction = connection.BeginTransaction();

                _inventory.WritePersist(inserts, updates, connection, transaction);
                _nanos.WriteInsertMissing(characterId, uploadedNanoIds, connection, transaction);

                try { transaction.Commit(); }
                catch (Exception exception) { throw new DatabaseCommitOutcomeUnknownException(exception); }
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "CharacterCoalesceCommit failed character={0} inserts={1} updates={2} nanos={3}",
                        characterId,
                        inserts.Count,
                        updates.Count,
                        uploadedNanoIds.Count));
                throw;
            }
        }
    }
}
