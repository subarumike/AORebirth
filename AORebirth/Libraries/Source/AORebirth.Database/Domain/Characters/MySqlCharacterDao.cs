namespace AORebirth.Database.Domain.Characters
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;

    using AORebirth.Interfaces.Persistence.Characters;
    using Dapper;
    using MySqlConnector;

    /// <summary>
    /// MySQL-only directory and online-state persistence. No runtime, ownership-lock or aggregate policy.
    /// Each operation owns its connection. The injected seam must return a fresh MySQL-capable connection.
    /// </summary>
    public sealed class MySqlCharacterDao : ICharacterDao
    {
        private const string DirectoryColumns =
            "Id AS CharacterId, Username AS AccountUsername, Name, FirstName, LastName, Playfield, Online";

        private readonly Func<IDbConnection> connectionFactory;

        public MySqlCharacterDao()
            : this(OpenConfiguredMySqlConnection)
        {
        }

        public MySqlCharacterDao(Func<IDbConnection> connectionFactory)
        {
            if (connectionFactory == null)
            {
                throw new ArgumentNullException("connectionFactory");
            }

            this.connectionFactory = connectionFactory;
        }

        public CharacterDirectoryData LoadById(int characterId)
        {
            return this.WithConnection(connection => connection.Query<CharacterDirectoryData>(
                "SELECT " + DirectoryColumns + " FROM characters WHERE Id=@CharacterId",
                new { CharacterId = characterId }).SingleOrDefault());
        }

        public CharacterDirectoryData LoadByName(string name)
        {
            // Name is not unique in the canonical schema. Do not invent ordering or duplicate rejection.
            return this.WithConnection(connection => connection.Query<CharacterDirectoryData>(
                "SELECT " + DirectoryColumns + " FROM characters WHERE Name=@Name",
                new { Name = name }).FirstOrDefault());
        }

        public IList<CharacterDirectoryData> ListForAccount(string accountUsername)
        {
            return this.WithConnection(connection => connection.Query<CharacterDirectoryData>(
                "SELECT " + DirectoryColumns + " FROM characters WHERE Username=@AccountUsername",
                new { AccountUsername = accountUsername }).ToList());
        }

        public bool IsOwnedByAccount(string accountUsername, uint characterId)
        {
            return this.WithConnection(connection => connection.Query<int>(
                "SELECT Id FROM characters WHERE Username=@AccountUsername AND Id=@CharacterId",
                new { AccountUsername = accountUsername, CharacterId = characterId }).Count() == 1);
        }

        public int MarkOnline(int characterId)
        {
            return this.WriteOnline(characterId, 1);
        }

        public int MarkOffline(int characterId)
        {
            return this.WriteOnline(characterId, 0);
        }

        public IList<CharacterDirectoryData> ListLoggedIn()
        {
            return this.WithConnection(connection => connection.Query<CharacterDirectoryData>(
                "SELECT " + DirectoryColumns + " FROM characters WHERE Online=@Online",
                new { Online = 1 }).ToList());
        }

        public StaleOnlineRecoveryData RecoverStaleOnline(string expectedDatabase)
        {
            if (string.IsNullOrWhiteSpace(expectedDatabase))
            {
                throw new ArgumentException("An exact expected database name is required.", "expectedDatabase");
            }

            return this.WithConnection(connection => InTransaction(
                connection, IsolationLevel.Serializable, transaction =>
                {
                    string databaseName = connection.Query<string>("SELECT DATABASE()", transaction: transaction).Single();
                    if (!string.Equals(databaseName, expectedDatabase, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("Stale-online recovery database does not match the expected database.");
                    }

                    List<StaleRow> captured = connection.Query<StaleRow>(
                        "SELECT Id, Online FROM characters WHERE Online IS NOT NULL AND Online<>0 ORDER BY Id FOR UPDATE",
                        transaction: transaction).ToList();
                    var rows = captured.Select(row => new StaleOnlineCharacterData(row.Id, row.Online)).ToList();
                    if (rows.Count == 0)
                    {
                        // Match the legacy no-cleanup branch: no UPDATE, post-count or COMMIT.
                        return new TransactionOutcome<StaleOnlineRecoveryData>(
                            new StaleOnlineRecoveryData(databaseName, rows, 0, null), false);
                    }

                    int updated = connection.Execute(
                        "UPDATE characters SET Online=0 WHERE Online IS NOT NULL AND Online<>0 AND Id IN @CharacterIds",
                        new { CharacterIds = captured.Select(row => row.Id).ToArray() }, transaction);
                    if (updated != rows.Count)
                    {
                        throw new InvalidDataException("Stale-online recovery did not update exactly the captured rows.");
                    }

                    long remaining = connection.Query<long>(
                        "SELECT COUNT(*) FROM characters WHERE Online IS NOT NULL AND Online<>0",
                        transaction: transaction).Single();
                    if (remaining != 0)
                    {
                        throw new InvalidDataException("Stale-online recovery verification found remaining nonzero rows.");
                    }

                    return new TransactionOutcome<StaleOnlineRecoveryData>(
                        new StaleOnlineRecoveryData(databaseName, rows, updated, remaining), true);
                }));
        }

        private int WriteOnline(int characterId, int online)
        {
            // Legacy CharacterDao.SetOnline/SetOffline call generic Save, which owns a default-isolation transaction.
            return this.WithConnection(connection => InTransaction(
                connection, null, transaction => new TransactionOutcome<int>(
                    connection.Execute("UPDATE characters SET Online=@Online WHERE Id=@CharacterId",
                        new { CharacterId = characterId, Online = online }, transaction), true)));
        }

        private static T InTransaction<T>(
            IDbConnection connection, IsolationLevel? isolation,
            Func<IDbTransaction, TransactionOutcome<T>> operation)
        {
            IDbTransaction transaction = isolation.HasValue
                ? connection.BeginTransaction(isolation.Value)
                : connection.BeginTransaction();
            if (transaction == null)
            {
                throw new InvalidOperationException("Character connection returned no transaction.");
            }

            Exception failure = null;
            bool rollbackAttempted = false;
            try
            {
                TransactionOutcome<T> outcome = operation(transaction);
                if (outcome.Commit)
                {
                    // A thrown acknowledgement may follow a durable commit. Never automatically retry.
                    transaction.Commit();
                }
                else
                {
                    rollbackAttempted = true;
                    transaction.Rollback();
                }

                return outcome.Value;
            }
            catch (Exception error)
            {
                failure = error;
                if (!rollbackAttempted)
                {
                    try
                    {
                        transaction.Rollback();
                    }
                    catch (Exception rollbackFailure)
                    {
                        error.Data["CharacterDao.RollbackFailure"] = rollbackFailure;
                    }
                }

                throw;
            }
            finally
            {
                DisposeOwned(transaction, failure, "CharacterDao.TransactionDisposeFailure");
            }
        }

        private T WithConnection<T>(Func<IDbConnection, T> operation)
        {
            IDbConnection connection = this.connectionFactory();
            if (connection == null)
            {
                throw new InvalidOperationException("Character connection factory returned null.");
            }

            Exception failure = null;
            try
            {
                if (connection.State == ConnectionState.Closed)
                {
                    connection.Open();
                }

                return operation(connection);
            }
            catch (Exception error)
            {
                failure = error;
                throw;
            }
            finally
            {
                DisposeOwned(connection, failure, "CharacterDao.ConnectionDisposeFailure");
            }
        }

        private static IDbConnection OpenConfiguredMySqlConnection()
        {
            // Connector opens before returning; errors before return remain owned by that shared infrastructure.
            IDbConnection connection = Connector.GetConnection();
            if (connection is MySqlConnection)
            {
                return connection;
            }

            var failure = new NotSupportedException("Character persistence requires the configured MySQL provider.");
            DisposeOwned(connection, failure, "CharacterDao.ConnectionDisposeFailure");
            throw failure;
        }

        private static void DisposeOwned(IDisposable resource, Exception primaryFailure, string diagnosticKey)
        {
            if (resource == null)
            {
                return;
            }

            try
            {
                resource.Dispose();
            }
            catch (Exception disposalFailure)
            {
                if (primaryFailure == null)
                {
                    throw;
                }

                primaryFailure.Data[diagnosticKey] = disposalFailure;
            }
        }

        private sealed class StaleRow
        {
            public int Id { get; set; }
            public int Online { get; set; }
        }

        private sealed class TransactionOutcome<T>
        {
            public TransactionOutcome(T value, bool commit)
            {
                this.Value = value;
                this.Commit = commit;
            }

            public T Value { get; private set; }
            public bool Commit { get; private set; }
        }
    }
}
