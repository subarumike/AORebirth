namespace ZoneEngine.Core.Playfields.Locality
{
    using System;

    using Utility.Config;

    internal sealed class PlayfieldLocalityPolicy
    {
        private const int DefaultVisibilityNeighborLevel = 2;
        private const int DefaultHotNeighborLevel = 1;
        private const int DefaultWarmNeighborLevel = 2;
        private const int DefaultCellSleepTimeSeconds = 30;

        private PlayfieldLocalityPolicy(
            int visibilityNeighborLevel,
            int hotNeighborLevel,
            int warmNeighborLevel,
            int cellSleepTimeSeconds)
        {
            VisibilityNeighborLevel = visibilityNeighborLevel;
            HotNeighborLevel = hotNeighborLevel;
            WarmNeighborLevel = warmNeighborLevel;
            CellSleepTimeSeconds = cellSleepTimeSeconds;
        }

        internal int VisibilityNeighborLevel { get; private set; }

        internal int HotNeighborLevel { get; private set; }

        internal int WarmNeighborLevel { get; private set; }

        internal int CellSleepTimeSeconds { get; private set; }

        internal static PlayfieldLocalityPolicy FromConfig(LocalitySettings settings)
        {
            int visibility = DefaultVisibilityNeighborLevel;
            int hot = DefaultHotNeighborLevel;
            int warm = DefaultWarmNeighborLevel;
            int sleep = DefaultCellSleepTimeSeconds;

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

            return new PlayfieldLocalityPolicy(visibility, hot, warm, sleep);
        }
    }
}
