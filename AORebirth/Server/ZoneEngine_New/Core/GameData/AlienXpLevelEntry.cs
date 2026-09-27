namespace ZoneEngine_New.Core.GameData
{
    /// <summary>
    /// One alien level row from GameData/AlienXp.json.
    /// NextLevelXp is the XP required to reach this alien level.
    /// FloorXp is the cumulative XP before that step.
    /// </summary>
    public sealed class AlienXpLevelEntry
    {
        public int Level { get; init; }

        public int NextLevelXp { get; init; }

        public int FloorXp { get; init; }
    }
}
