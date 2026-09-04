namespace ZoneEngine_New.Core.Playfield.Locality
{
    using System;

    using Utility.Config;

    using Config = Utility.Config.ConfigReadWrite;

    internal sealed class LocalityPolicy
    {
        private const int DefaultVisibilityNeighborLevel = 2;
        private const int DefaultHotNeighborLevel = 1;
        private const int DefaultWarmNeighborLevel = 2;
        private const int DefaultCellSleepTimeSeconds = 30;
        private const int DefaultSpawnRate = 1;

        private LocalityPolicy(
            bool enableCellHeatScheduling,
            int visibilityNeighborLevel,
            int hotNeighborLevel,
            int warmNeighborLevel,
            int cellSleepTimeSeconds,
            int spawnRate)
        {
            EnableCellHeatScheduling = enableCellHeatScheduling;
            VisibilityNeighborLevel = visibilityNeighborLevel;
            HotNeighborLevel = hotNeighborLevel;
            WarmNeighborLevel = warmNeighborLevel;
            CellSleepTimeSeconds = cellSleepTimeSeconds;
            SpawnRate = spawnRate;
        }

        internal bool EnableCellHeatScheduling { get; }

        internal int VisibilityNeighborLevel { get; }

        internal int HotNeighborLevel { get; }

        internal int WarmNeighborLevel { get; }

        internal int CellSleepTimeSeconds { get; }

        /// <summary>Max hash-spawns per awake cell tick.</summary>
        internal int SpawnRate { get; }

        internal static LocalityPolicy FromConfig()
        {
            LocalitySettings? settings =
                Config.Instance.CurrentConfig == null ? null : Config.Instance.CurrentConfig.Locality;
            return FromConfig(settings);
        }

        internal static LocalityPolicy FromConfig(LocalitySettings? settings)
        {
            bool enableCellHeatScheduling = settings != null && settings.EnableCellHeatScheduling;
            int visibility = DefaultVisibilityNeighborLevel;
            int hot = DefaultHotNeighborLevel;
            int warm = DefaultWarmNeighborLevel;
            int sleep = DefaultCellSleepTimeSeconds;
            int spawnRate = DefaultSpawnRate;

            if (settings != null)
            {
                if (settings.VisibilityNeighborLevel > 0)
                {
                    visibility = settings.VisibilityNeighborLevel;
                }

                if (settings.HotNeighborLevel > 0)
                {
                    hot = settings.HotNeighborLevel;
                }

                if (settings.WarmNeighborLevel > 0)
                {
                    warm = settings.WarmNeighborLevel;
                }

                if (settings.CellSleepTime > 0)
                {
                    sleep = settings.CellSleepTime;
                }

                if (settings.SpawnRate > 0)
                {
                    spawnRate = settings.SpawnRate;
                }
            }

            if (hot > warm)
            {
                hot = DefaultHotNeighborLevel;
                warm = DefaultWarmNeighborLevel;
            }

            if (warm > visibility)
            {
                warm = Math.Min(warm, visibility);
                if (hot > warm)
                {
                    hot = DefaultHotNeighborLevel;
                    warm = DefaultWarmNeighborLevel;
                }
            }

            return new LocalityPolicy(
                enableCellHeatScheduling,
                visibility,
                hot,
                warm,
                sleep,
                spawnRate);
        }
    }
}
