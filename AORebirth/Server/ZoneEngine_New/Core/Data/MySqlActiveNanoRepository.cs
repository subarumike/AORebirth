namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MySqlConnector;
    using ZoneEngine_New.Core.Nanos;

    /// <summary>Scoped transaction against the pre-existing active nano table; no migration/retry.</summary>
    public sealed class MySqlActiveNanoRepository : IActiveNanoRepository
    {
        private readonly string _connectionString;
        public MySqlActiveNanoRepository() => _connectionString = MySqlConnectionSettings.GetRequiredConnectionString();

        public IReadOnlyList<ActiveNanoRecord> Load(int characterId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(characterId);
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var command = new MySqlCommand(
                "SELECT NanoId,Strain,NanoInstance,DurationCentiseconds,ExpiresAtUtcTicks "
                + "FROM charactersactivenanos WHERE CharacterId=@id ORDER BY Strain,NanoInstance", connection);
            command.Parameters.AddWithValue("@id", characterId);
            using var reader = command.ExecuteReader();
            var rows = new List<ActiveNanoRecord>();
            while (reader.Read()) rows.Add(new ActiveNanoRecord(reader.GetInt32(0), reader.GetInt32(1),
                reader.GetInt32(2), reader.GetInt32(3), reader.GetInt64(4)));
            return rows;
        }

        public void Commit(IReadOnlyList<NanoCharacterWrite> characters)
        {
            ArgumentNullException.ThrowIfNull(characters);
            if (characters.Count == 0 || characters.Any(c => c.CharacterId <= 0)
                || characters.Select(c => c.CharacterId).Distinct().Count() != characters.Count)
                throw new ArgumentException("Distinct positive nano owners are required.", nameof(characters));
            foreach (NanoCharacterWrite character in characters)
            {
                if (character.ActiveNanos.Any(n => n.NanoId <= 0 || n.NanoInstance <= 0
                    || n.DurationCentiseconds < 0 || n.ExpiresAtUtcTicks < 0)
                    || character.ActiveNanos.Select(n => n.Strain).Distinct().Count() != character.ActiveNanos.Count
                    || character.ActiveNanos.Select(n => n.NanoInstance).Distinct().Count() != character.ActiveNanos.Count)
                    throw new ArgumentException("Invalid or duplicate active nano identity.", nameof(characters));
            }
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            foreach (NanoCharacterWrite character in characters.OrderBy(c => c.CharacterId))
                MySqlCharacterRepository.LockForTransaction(connection, transaction, character.CharacterId);
            foreach (NanoCharacterWrite character in characters.OrderBy(c => c.CharacterId))
            {
                using (var remove = new MySqlCommand("DELETE FROM charactersactivenanos WHERE CharacterId=@id", connection, transaction))
                {
                    remove.Parameters.AddWithValue("@id", character.CharacterId);
                    remove.ExecuteNonQuery();
                }
                foreach (ActiveNanoRecord nano in character.ActiveNanos)
                {
                    using var insert = new MySqlCommand("INSERT INTO charactersactivenanos "
                        + "(CharacterId,NanoId,Strain,NanoInstance,DurationCentiseconds,ExpiresAtUtcTicks) "
                        + "VALUES (@id,@nano,@strain,@instance,@duration,@expiry)", connection, transaction);
                    insert.Parameters.AddWithValue("@id", character.CharacterId);
                    insert.Parameters.AddWithValue("@nano", nano.NanoId);
                    insert.Parameters.AddWithValue("@strain", nano.Strain);
                    insert.Parameters.AddWithValue("@instance", nano.NanoInstance);
                    insert.Parameters.AddWithValue("@duration", nano.DurationCentiseconds);
                    insert.Parameters.AddWithValue("@expiry", nano.ExpiresAtUtcTicks);
                    insert.ExecuteNonQuery();
                }
                MySqlStatRepository.UpsertForCharacter(connection, transaction, character.CharacterId, character.BaseStats);
            }
            try { transaction.Commit(); }
            catch (Exception exception) { throw new DatabaseCommitOutcomeUnknownException(exception); }
        }
    }
}
