namespace AORebirth.Database.Dao
{
    /// <summary>
    /// Compatibility entry points for existing callers. The shared mission DAO
    /// owns all SQL and preserves the legacy false/null failure behavior.
    /// </summary>
    public static class NewCharacterStartAreaSelectionDao
    {
        public const string PendingState = AORebirth.Interfaces.Persistence.Missions.MissionStartAreaSelectionStates.Pending;

        public const string AreteState = AORebirth.Interfaces.Persistence.Missions.MissionStartAreaSelectionStates.Arete;

        public const string IccShuttleportState = AORebirth.Interfaces.Persistence.Missions.MissionStartAreaSelectionStates.IccShuttleport;

        public static bool MarkPending(int characterId)
        {
            return DatabaseDaoFactory.CreateMissionDao().MarkStartAreaSelectionPending(characterId);
        }

        public static string GetState(int characterId)
        {
            return DatabaseDaoFactory.CreateMissionDao().GetStartAreaSelectionState(characterId);
        }

        public static bool TryComplete(int characterId, string selectedState)
        {
            return DatabaseDaoFactory.CreateMissionDao().TryCompleteStartAreaSelection(characterId, selectedState);
        }
    }
}
