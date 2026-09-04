namespace Utility.Config
{
    public class LocalitySettings
    {
        public bool EnableCellHeatScheduling { get; set; }

        public int VisibilityNeighborLevel { get; set; }

        public int HotNeighborLevel { get; set; }

        public int WarmNeighborLevel { get; set; }

        public int CellSleepTime { get; set; }

        /// <summary>Max NPC hash-spawns per awake cell tick (default 1).</summary>
        public int SpawnRate { get; set; }
    }
}
