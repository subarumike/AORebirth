namespace ZoneEngine_New.Core.GameData
{
    /// <summary>
    /// One level row from GameData/Xp.json. FloorXp is the cumulative XP at the start of this level.
    /// </summary>
    public sealed class XpLevelEntry
    {
        public int Level { get; init; }

        public int KillAward { get; init; }

        public int LevelDelta { get; init; }

        public int NextLevelXp { get; init; }

        public int FloorXp { get; init; }
    }
}
