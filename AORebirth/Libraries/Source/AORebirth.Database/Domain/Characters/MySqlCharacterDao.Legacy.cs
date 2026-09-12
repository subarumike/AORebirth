namespace AORebirth.Database.Domain.Characters
{
    using System;
    using System.Data;
    using MySqlConnector;

    public sealed partial class MySqlCharacterDao
    {
        public MySqlCharacterDao()
            : this(OpenConfiguredMySqlConnection)
        {
        }

        private static IDbConnection OpenConfiguredMySqlConnection()
        {
            // Connector owns failures before it returns the opened connection.
            IDbConnection connection = Connector.GetConnection();
            if (connection is MySqlConnection) return connection;

            var failure = new NotSupportedException("Character persistence requires the configured MySQL provider.");
            DisposeOwned(connection, failure, "CharacterDao.ConnectionDisposeFailure");
            throw failure;
        }
    }
}
