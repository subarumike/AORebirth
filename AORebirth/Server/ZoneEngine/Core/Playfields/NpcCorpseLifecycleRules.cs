namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    #endregion

    public static class NpcCorpseLifecycleRules
    {
        public static readonly TimeSpan DeadNpcDespawnDelay = TimeSpan.FromSeconds(10);

        public static readonly TimeSpan CorpseSpawnDelay = TimeSpan.FromMilliseconds(600);

        public const int CapturedCleaningRobotDeathActionParameter2 = 500;
    }
}
