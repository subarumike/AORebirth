namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MySqlConnector;
    using SmokeLounge.AOtomation.Messaging.GameData;

    public sealed class MySqlTradePersistence : ITradePersistence
    {
        readonly MySqlInventoryRepository _inventory;
        readonly MySqlUploadedNanoRepository _nanos;
        readonly string _connectionString;

        public MySqlTradePersistence(MySqlInventoryRepository inventory, MySqlUploadedNanoRepository nanos)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _nanos = nanos ?? throw new ArgumentNullException(nameof(nanos));
            _connectionString = MySqlConnectionSettings.GetRequiredConnectionString();
        }

        public void Persist(TradePersistenceBatch batch)
        {
            ArgumentNullException.ThrowIfNull(batch);
            if (batch.Characters.Count is < 1 or > 2
                || batch.Characters.Any(c => c.CharacterId <= 0 || c.Cash < 0)
                || batch.Characters.Select(c => c.CharacterId).Distinct().Count() != batch.Characters.Count)
                throw new ArgumentException("A trade requires one or two distinct positive character ids and nonnegative cash.", nameof(batch));

            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            // Serialize participant rows in the same stable order as the in-memory gates.
            foreach (TradeCharacterWrite character in batch.Characters.OrderBy(c => c.CharacterId))
            {
                MySqlCharacterRepository.LockForTransaction(connection, transaction, character.CharacterId);
            }

            _inventory.WritePersist(batch.Inserts, batch.Updates, connection, transaction);
            foreach (TradeCharacterWrite character in batch.Characters.OrderBy(c => c.CharacterId))
            {
                _nanos.WriteInsertMissing(character.CharacterId, character.UploadedNanoIds, connection, transaction);
                MySqlStatRepository.UpsertForCharacter(connection, transaction, character.CharacterId,
                    [new StatRecord { StatId = (int)CharacterStat.Cash, StatValue = character.Cash }]);
            }

            try { transaction.Commit(); }
            catch (Exception exception) { throw new DatabaseCommitOutcomeUnknownException(exception); }
        }
    }
}
