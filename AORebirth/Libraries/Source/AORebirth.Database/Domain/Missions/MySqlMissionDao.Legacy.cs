namespace AORebirth.Database.Domain.Missions
{
    // Only Legacy owns the global Connector. ZoneEngine_New supplies its scoped
    // connection factory to the same authoritative mission DAO implementation.
    public sealed partial class MySqlMissionDao
    {
        public MySqlMissionDao()
            : this(Connector.GetConnection)
        {
        }
    }
}
