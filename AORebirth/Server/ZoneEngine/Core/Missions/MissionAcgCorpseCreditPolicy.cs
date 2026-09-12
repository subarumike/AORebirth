namespace ZoneEngine.Core.Missions
{
    // Pure shared portion of the existing operational ACG corpse policy. These are
    // captured currency amounts, not item-drop odds, damage or distance defaults.
    internal static class MissionAcgCorpseCreditPolicy
    {
        internal const int MinimumCredits = 21;
        internal const int MaximumCredits = 87;
        // NpcCorpseLifecycleRules + CombatCorpseRules: distinct generated death timers.
        internal const int SpawnDelayMilliseconds = 600;
        internal const int DeadNpcDespawnMilliseconds = 10000;
        internal const int LifetimeMilliseconds = 60000;
        internal static bool TryResolve(int runtimeNpcInstance, int livePlayfield, out int credits)
        {
            credits = 0;
            int encodedPlayfield, ordinal, salt;
            if (runtimeNpcInstance <= 0 || livePlayfield < MissionAcgIdentityRanges.MinimumLivePlayfield2
                || livePlayfield > MissionAcgIdentityRanges.MaximumLivePlayfield2
                || !MissionAcgRuntimeMaterializer.TryReverseRuntimeInstance(runtimeNpcInstance, out encodedPlayfield, out ordinal)
                || encodedPlayfield != livePlayfield || ordinal <= 0
                || !TryResolveSignedSalt(runtimeNpcInstance, livePlayfield, 131u, out salt)) return false;
            long magnitude = salt < 0 ? -(long)salt : salt;
            credits = MinimumCredits + (int)(magnitude % (MaximumCredits - MinimumCredits + 1));
            return true;
        }

        internal static bool TryResolveSignedSalt(int left, int right, uint multiplier, out int salt)
        {
            salt = 0;
            if (left <= 0 || right < 0 || multiplier == 0) return false;
            ulong product = (ulong)(uint)left * multiplier;
            uint lowBits = (uint)(product & uint.MaxValue);
            salt = unchecked((int)(lowBits ^ (uint)right));
            return salt != int.MinValue;
        }
    }
}
