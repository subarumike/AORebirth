namespace AORebirth.Database.Domain.Characters
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using AORebirth.Interfaces.Persistence.Characters;
    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>Storage for full NewEngine hydration and its existing atomic save boundaries.</summary>
    public sealed partial class MySqlCharacterPersistenceDao : ICharacterPersistenceDao
    {
        private readonly Func<IDbConnection> connectionFactory;

        public MySqlCharacterPersistenceDao(Func<IDbConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        private T Read<T>(Func<IDbConnection, T> action, bool committedWrite = false)
        {
            IDbConnection connection = null;
            Exception failure = null;
            try
            {
                connection = connectionFactory() ?? throw new InvalidOperationException("Connection factory returned null.");
                if (connection.State != ConnectionState.Open) connection.Open();
                return action(connection);
            }
            catch (Exception exception) { failure = exception; throw; }
            finally { DisposePreserving(connection, failure, committedWrite); }
        }

        private T Transaction<T>(Func<IDbConnection, IDbTransaction, T> action,
            IsolationLevel isolation = IsolationLevel.Unspecified)
        {
            return Read(connection =>
            {
                IDbTransaction transaction = null;
                Exception failure = null;
                bool commitAttempted = false;
                try
                {
                    transaction = isolation == IsolationLevel.Unspecified
                        ? connection.BeginTransaction() : connection.BeginTransaction(isolation);
                    T result = action(connection, transaction);
                    commitAttempted = true;
                    try { transaction.Commit(); }
                    catch (Exception exception) { throw new CharacterPersistenceCommitOutcomeUnknownException(exception); }
                    return result;
                }
                catch (Exception exception)
                {
                    failure = exception;
                    // After COMMIT is sent a rollback cannot establish whether the write persisted.
                    if (!commitAttempted && transaction != null)
                    {
                        try { transaction.Rollback(); }
                        catch (Exception rollback) { exception.Data["CharacterPersistence.RollbackFailure"] = rollback; }
                    }
                    throw;
                }
                finally { DisposePreserving(transaction, failure, commitAttempted); }
            }, committedWrite: true);
        }

        private static void DisposePreserving(IDisposable resource, Exception failure, bool commitAttempted = false)
        {
            if (resource == null) return;
            try { resource.Dispose(); }
            catch (Exception exception)
            {
                if (failure == null)
                {
                    // A cleanup failure after success must not become a safely retryable write failure.
                    if (commitAttempted) throw new CharacterPersistenceCommitOutcomeUnknownException(exception);
                    throw;
                }
                failure.Data["CharacterPersistence.DisposeFailure." + resource.GetType().Name] = exception;
            }
        }

        private static IDbCommand Command(IDbConnection connection, IDbTransaction transaction,
            string sql, params object[] parameters)
        {
            IDbCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            for (int i = 0; i < parameters.Length; i += 2)
            {
                IDbDataParameter parameter = command.CreateParameter();
                parameter.ParameterName = (string)parameters[i];
                parameter.Value = parameters[i + 1] ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
            return command;
        }

        private static int Execute(IDbConnection connection, IDbTransaction transaction,
            string sql, params object[] parameters)
        {
            using (IDbCommand command = Command(connection, transaction, sql, parameters))
                return command.ExecuteNonQuery();
        }

        private IList<T> Query<T>(string sql, Func<IDataRecord, T> map, params object[] parameters)
        {
            return Read<IList<T>>(connection =>
            {
                var rows = new List<T>();
                using (IDbCommand command = Command(connection, null, sql, parameters))
                using (IDataReader reader = command.ExecuteReader())
                    while (reader.Read()) rows.Add(map(reader));
                return rows;
            });
        }

        public CharacterStateData LoadCharacter(int characterId)
        {
            if (characterId <= 0) return null;
            var rows = Query("SELECT Id,Name,FirstName,LastName,Playfield,X,Y,Z,HeadingW,HeadingX,HeadingY,HeadingZ "
                + "FROM characters WHERE Id=@Id LIMIT 1", r => new CharacterStateData
                {
                    Id = r.GetInt32(0), Name = r.IsDBNull(1) ? null : r.GetString(1),
                    FirstName = r.IsDBNull(2) ? null : r.GetString(2), LastName = r.IsDBNull(3) ? null : r.GetString(3),
                    Playfield = r.GetInt32(4), X = r.GetFloat(5), Y = r.GetFloat(6), Z = r.GetFloat(7),
                    HeadingW = r.GetFloat(8), HeadingX = r.GetFloat(9), HeadingY = r.GetFloat(10), HeadingZ = r.GetFloat(11)
                }, "@Id", characterId);
            return rows.Count == 0 ? null : rows[0];
        }

        public IList<CharacterStatData> LoadStats(int characterId)
        {
            if (characterId <= 0) return new List<CharacterStatData>();
            return Query("SELECT StatId,StatValue FROM stats WHERE Type=@Type AND Instance=@Instance",
                r => new CharacterStatData { StatId = r.GetInt32(0), StatValue = r.GetInt32(1) },
                "@Type", (int)IdentityType.CanbeAffected, "@Instance", characterId);
        }

        public IDictionary<int, string> LoadItemNames()
        {
            var result = new Dictionary<int, string>();
            foreach (var row in Query("SELECT Id,Name FROM itemnames", r =>
                new KeyValuePair<int, string>(r.GetInt32(0), r.IsDBNull(1) ? string.Empty : r.GetString(1))))
                result[row.Key] = row.Value;
            return result;
        }

        public IList<int> LoadUploadedNanos(int characterId)
        {
            if (characterId <= 0) return new List<int>();
            return Query("SELECT NanoId FROM charactersuploadednanos WHERE CharacterId=@Id", r => r.GetInt32(0), "@Id", characterId);
        }

        public void SaveLocation(CharacterStateData character, int online)
        {
            ValidateCharacter(character);
            Transaction((c, t) => { WriteLocation(c, t, character, online); return 0; });
        }

        public void SaveSnapshot(CharacterStateData character, int online, IList<CharacterStatData> stats)
        {
            ValidateCharacter(character);
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            Transaction((c, t) => { WriteLocation(c, t, character, online); WriteStats(c, t, character.Id, stats); return 0; });
        }

        private static void ValidateCharacter(CharacterStateData character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (character.Id <= 0 || character.Playfield <= 0) throw new ArgumentOutOfRangeException(nameof(character));
        }

        private static void WriteLocation(IDbConnection c, IDbTransaction t, CharacterStateData v, int online)
        {
            if (Execute(c, t, "UPDATE characters SET Playfield=@Playfield,X=@X,Y=@Y,Z=@Z,"
                + "HeadingW=@W,HeadingX=@HX,HeadingY=@HY,HeadingZ=@HZ,Online=@Online WHERE Id=@Id",
                "@Playfield", v.Playfield, "@X", v.X, "@Y", v.Y, "@Z", v.Z,
                "@W", v.HeadingW, "@HX", v.HeadingX, "@HY", v.HeadingY, "@HZ", v.HeadingZ,
                "@Online", online, "@Id", v.Id) != 1)
                throw new InvalidOperationException("Character row is missing during snapshot persistence.");
        }

        public void SaveStats(int characterId, IList<CharacterStatData> stats)
        {
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            if (characterId <= 0 || stats.Count == 0) return;
            Transaction((c, t) => { WriteStats(c, t, characterId, stats); return 0; });
        }

        private static void WriteStats(IDbConnection c, IDbTransaction t, int characterId, IList<CharacterStatData> stats)
        {
            foreach (var stat in stats)
                Execute(c, t, "INSERT INTO stats (Type,Instance,StatId,StatValue) VALUES (@Type,@Instance,@StatId,@Value) "
                    + "ON DUPLICATE KEY UPDATE StatValue=@Value", "@Type", (int)IdentityType.CanbeAffected,
                    "@Instance", characterId, "@StatId", stat.StatId, "@Value", stat.StatValue);
        }

        private static void LockCharacter(IDbConnection c, IDbTransaction t, int characterId)
        {
            if (characterId <= 0) throw new ArgumentOutOfRangeException(nameof(characterId));
            using (var command = Command(c, t, "SELECT Id FROM characters WHERE Id=@Id FOR UPDATE", "@Id", characterId))
                if (command.ExecuteScalar() == null) throw new InvalidOperationException("Transaction participant no longer exists.");
        }

        private static void WriteUploadedNanos(IDbConnection c, IDbTransaction t, int characterId, IList<int> nanos)
        {
            if (characterId <= 0) return;
            foreach (int nano in nanos)
            {
                if (nano <= 0) continue;
                Execute(c, t, "INSERT INTO charactersuploadednanos (CharacterId,NanoId) SELECT @Id,@Nano FROM DUAL "
                    + "WHERE NOT EXISTS (SELECT 1 FROM charactersuploadednanos WHERE CharacterId=@Id AND NanoId=@Nano)",
                    "@Id", characterId, "@Nano", nano);
            }
        }
    }
}
