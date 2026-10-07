namespace ZoneEngine_New.Core.GameData
{
    /// <summary>
    /// One level row from GameData/Xp.json. FloorXp is the cumulative XP at the start of this level.
    /// </summary>
    public sealed class XpLevelEntry
    {
        public int Level { get; init; }

        public int KillAward { get; init; }

        public int NextLevelXp { get; init; }

        /// <summary>
        /// XPKillRange (275): levels below this one a kill still gives full XP. The client colors a target gray below
        /// level - XPKillRange (N3Msg_Consider, Gamecode.dll 0x100174fe). 0 when the row has none.
        /// </summary>
        public int XpKillRange { get; init; }

        public int FloorXp { get; init; }
    }
}
