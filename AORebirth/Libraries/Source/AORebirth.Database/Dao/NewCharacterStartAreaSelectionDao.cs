namespace AORebirth.Database.Dao
{
    using System;
    using System.Data;
    using System.Linq;

    using Dapper;

    using Utility;

    /// <summary>
    /// Persists the one-time Rubi-Ka starting-area selection without adding a schema.
    /// Rows are created only for new Rubi-Ka characters, so existing and Shadowlands
    /// characters remain outside this workflow.
    /// </summary>
    public static class NewCharacterStartAreaSelectionDao
    {
        public const string PendingState = "pending";

        public const string AreteState = "arete";

        public const string IccShuttleportState = "icc_shuttleport";

        private const string QuestId = "system.new_character_start_area";

        private const string FlagKey = "selection";

        public static bool MarkPending(int characterId)
        {
            if (characterId <= 0)
            {
                return false;
            }

            const string Sql =
                "INSERT INTO missionflags "
                + "(CharacterId, QuestId, FlagKey, `Value`, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version) "
                + "VALUES (@CharacterId, @QuestId, @FlagKey, @Value, @NowUtcTicks, @NowUtcTicks, 1) "
                + "ON DUPLICATE KEY UPDATE `Value`=`Value`";

            try
            {
                using (IDbConnection connection = Connector.GetConnection())
                {
                    connection.Execute(
                        Sql,
                        new
                        {
                            CharacterId = characterId,
                            QuestId,
                            FlagKey,
                            Value = PendingState,
                            NowUtcTicks = DateTime.UtcNow.Ticks
                        });
                }

                return string.Equals(GetState(characterId), PendingState, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception exception)
            {
                LogUtil.ErrorException(exception);
                return false;
            }
        }

        public static string GetState(int characterId)
        {
            if (characterId <= 0)
            {
                return null;
            }

            const string Sql =
                "SELECT `Value` FROM missionflags "
                + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND FlagKey=@FlagKey LIMIT 1";

            try
            {
                using (IDbConnection connection = Connector.GetConnection())
                {
                    return connection.Query<string>(
                            Sql,
                            new { CharacterId = characterId, QuestId, FlagKey })
                        .FirstOrDefault();
                }
            }
            catch (Exception exception)
            {
                LogUtil.ErrorException(exception);
                return null;
            }
        }

        public static bool TryComplete(int characterId, string selectedState)
        {
            if (characterId <= 0 || !IsCompletedState(selectedState))
            {
                return false;
            }

            const string Sql =
                "UPDATE missionflags SET `Value`=@SelectedState, UpdatedAtUtcTicks=@NowUtcTicks, Version=Version+1 "
                + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND FlagKey=@FlagKey AND `Value`=@PendingState";

            try
            {
                using (IDbConnection connection = Connector.GetConnection())
                {
                    return connection.Execute(
                               Sql,
                               new
                               {
                                   CharacterId = characterId,
                                   QuestId,
                                   FlagKey,
                                   PendingState,
                                   SelectedState = selectedState,
                                   NowUtcTicks = DateTime.UtcNow.Ticks
                               }) == 1;
                }
            }
            catch (Exception exception)
            {
                LogUtil.ErrorException(exception);
                return false;
            }
        }

        private static bool IsCompletedState(string state)
        {
            return string.Equals(state, AreteState, StringComparison.Ordinal)
                   || string.Equals(state, IccShuttleportState, StringComparison.Ordinal);
        }
    }
}
