namespace AORebirth.Database.Domain.Missions
{
    // Only Legacy owns the global Connector. ZoneEngine_New supplies its scoped
    // connection factory to the same authoritative mission DAO implementation.
    public sealed partial class MySqlMissionDao
    {
        public MySqlMissionDao()
            : this(OpenConfiguredMySqlConnection)
        {
        }

        private static System.Data.IDbConnection OpenConfiguredMySqlConnection()
        {
            System.Data.IDbConnection connection = Connector.GetConnection();
            if (connection is MySqlConnector.MySqlConnection)
            {
                return connection;
            }

            if (connection != null)
            {
                connection.Dispose();
            }

            throw new System.NotSupportedException("Mission persistence requires the configured MySQL provider.");
        }
    }
}
