namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    #endregion

    public static class NpcCorpseLifecycleRules
    {
        public static readonly TimeSpan DeadNpcDespawnDelay = TimeSpan.FromSeconds(10);

        public static readonly TimeSpan CorpseSpawnDelay = TimeSpan.FromMilliseconds(600);

        // Capture 20260722-keeper-exect-nano / CombatTestMobArchetype: Death Parameter2=503 (0x1F7).
        public const int CapturedCleaningRobotDeathActionParameter2 = 503;
    }
}
