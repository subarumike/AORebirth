namespace AORebirth.Database
{
    using AORebirth.Database.Domain.Missions;
    using AORebirth.Interfaces.Persistence.Missions;

    /// <summary>
    /// Explicit construction boundary for domain-oriented database access.
    /// </summary>
    public static class DatabaseDaoFactory
    {
        public static IMissionDao CreateMissionDao()
        {
            return new MySqlMissionDao();
        }
    }
}
