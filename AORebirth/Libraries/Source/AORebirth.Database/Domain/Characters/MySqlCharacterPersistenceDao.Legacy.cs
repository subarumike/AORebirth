namespace AORebirth.Database.Domain.Characters
{
    using System;
    using System.Data;
    using MySqlConnector;

    public sealed partial class MySqlCharacterPersistenceDao
    {
        public MySqlCharacterPersistenceDao() : this(OpenConfiguredMySqlConnection) { }

        private static IDbConnection OpenConfiguredMySqlConnection()
        {
            IDbConnection connection = Connector.GetConnection();
            if (connection is MySqlConnection) return connection;
            var failure = new NotSupportedException("Character persistence requires the configured MySQL provider.");
            DisposePreserving(connection, failure);
            throw failure;
        }
    }
}
