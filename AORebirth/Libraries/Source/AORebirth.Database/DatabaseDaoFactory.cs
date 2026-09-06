namespace AORebirth.Database
{
    using AORebirth.Database.Domain.Missions;
    using AORebirth.Interfaces.Persistence.Missions;

    /// <summary>
    /// Explicit construction boundary for domain-oriented database access.
    /// </summary>
    public static class DatabaseDaoFactory
    {
        /// <summary>
        /// Creates the configured MySQL mission DAO without opening a connection.
        /// Each operation owns its connection; unsupported configured providers are
        /// rejected before mission SQL is executed. No runtime initialization occurs here.
        /// </summary>
        public static IMissionDao CreateMissionDao()
        {
            return new MySqlMissionDao();
        }
    }
}
