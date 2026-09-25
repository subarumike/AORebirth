using System;

using AORebirth.Core.Teams;
using AORebirth.Interfaces.Persistence.Teams;

namespace ChatEngine.Lists
{
    /// <summary>
    /// LFT level filter windows from <c>GameData/Teams/LevelEligibility.json</c>
    /// via <see cref="ITeamLevelEligibilityDao"/>. No embedded C# range table.
    /// </summary>
    public static class TeamLevelRanges
    {
        static readonly ITeamLevelEligibilityDao Dao = JsonTeamLevelEligibilityDao.Current;

        public static bool TryGetRange(int level, out int minLevel, out int maxLevel)
            => Dao.TryGetRange(level, out minLevel, out maxLevel);

        /// <summary>
        /// True if <paramref name="candidateLevel"/> is inside the XP/SK share window
        /// for <paramref name="searcherLevel"/>. Not an invite permission check.
        /// </summary>
        public static bool IsCompatible(int searcherLevel, int candidateLevel)
            => Dao.IsCompatible(searcherLevel, candidateLevel);
    }
}
