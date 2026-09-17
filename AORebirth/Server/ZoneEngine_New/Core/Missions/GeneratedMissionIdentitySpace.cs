namespace ZoneEngine.Core.Missions;

// Identity schema contract: 20260908_generated_mission_state.sql owns the allocator bounds.
internal static class GeneratedMissionIdentitySpace
{
    internal const int MinimumLivePlayfield2 = 1441792;
    internal const int MaximumLivePlayfield2 = 1507327;
    // Historical shared-world identities must never be treated as a private DAO lease.
    internal const int LegacySharedPlayfield2 = 1419349;
}
