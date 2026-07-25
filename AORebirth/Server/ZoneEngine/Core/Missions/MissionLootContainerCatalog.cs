namespace ZoneEngine.Core.Missions
{
    /// <summary>
    /// Lootable mission-interior containers.
    /// Repair capture 20260724-repaair-machine-mish (PF 1419360) and
    /// Find-Item capture 20260724-mission-find-item (PF 1441804),
    /// Find-Person capture 20260724-mission-find-person (PF 1419349).
    /// Some require UseItemOnItem (locked); others open with plain Use.
    /// </summary>
    internal static class MissionLootContainerCatalog
    {
        internal sealed class LootProp
        {
            public string Name;

            public float X;

            public float Y;

            public float Z;

            /// <summary>True when live capture used UseItemOnItem (lockpick/key) before Use.</summary>
            public bool Locked;
        }

        // Absolute interior positions from DYNEL-SPAWNED Container rows on PF 15A860.
        internal static readonly LootProp[] Props =
        {
            new LootProp { Name = "Small Crate", X = 237.8369f, Y = 5.1f, Z = 287.8768f, Locked = false },
            new LootProp { Name = "Treasure", X = 245.1313f, Y = 5.1f, Z = 281.7034f, Locked = false },
            new LootProp { Name = "Bottles and Garbage", X = 284.99f, Y = 5.1f, Z = 261.5294f, Locked = false },
            new LootProp { Name = "A Crashed Android", X = 245.01f, Y = 5.1f, Z = 245.01f, Locked = false },
            new LootProp { Name = "Barrel", X = 261.7f, Y = 5.1f, Z = 236.6f, Locked = false },
            new LootProp { Name = "A Skull", X = 211.6f, Y = 5.1f, Z = 261.3f, Locked = false },
            new LootProp { Name = "Bottles and Garbage", X = 228.2f, Y = 5.1f, Z = 267.01f, Locked = false },
            new LootProp { Name = "Garbage", X = 244.01f, Y = 5.1f, Z = 268.4f, Locked = false },
            new LootProp { Name = "Small Crate", X = 268.3f, Y = 5.100602f, Z = 202.01f, Locked = false },
            new LootProp { Name = "Treasure", X = 223.3f, Y = 5.1f, Z = 231.5f, Locked = false },
            new LootProp { Name = "Shadow Rift of the Phoenix", X = 225.2f, Y = 5.1f, Z = 238.7f, Locked = false },
            new LootProp { Name = "Treasure", X = 216.8f, Y = 5.1f, Z = 281.3f, Locked = false },
            new LootProp { Name = "Treasure", X = 247.7f, Y = 5.1f, Z = 297.9f, Locked = true },
            new LootProp { Name = "A blasted Skeleton", X = 247.6f, Y = 5.1f, Z = 292.1f, Locked = false },
            new LootProp { Name = "Barrel", X = 291.2f, Y = 5.1f, Z = 292.6f, Locked = false },
            new LootProp { Name = "Treasure", X = 251.8403f, Y = 5.1f, Z = 295.2358f, Locked = false },
            new LootProp { Name = "A Broken Android", X = 254.8471f, Y = 5.1f, Z = 291.8049f, Locked = false },
            new LootProp { Name = "Treasure", X = 254.8213f, Y = 5.1f, Z = 298.5485f, Locked = false },
            new LootProp { Name = "Bottles and Garbage", X = 278.4f, Y = 5.1f, Z = 238.3f, Locked = false },
            new LootProp { Name = "Barrel", X = 271.4f, Y = 5.1f, Z = 231.4f, Locked = false },
            new LootProp { Name = "Treasure", X = 208.5f, Y = 5.1f, Z = 261.5f, Locked = false },
            new LootProp { Name = "Treasure", X = 205.2f, Y = 5.1f, Z = 268.7f, Locked = false },
            new LootProp { Name = "Barrel", X = 201.5f, Y = 5.1f, Z = 265.01f, Locked = false },
            new LootProp { Name = "Treasure", X = 251.4f, Y = 5.1f, Z = 274.1f, Locked = true },
            new LootProp { Name = "Treasure", X = 274.8f, Y = 5.1f, Z = 227.2f, Locked = false },
        };

        // Capture 20260724-mission-find-item PF 1441804 containers
        internal static readonly LootProp[] Props_1441804 =
        {
            new LootProp { Name = "Treasure", X = 31.60001f, Y = 5.1f, Z = 193.2f, Locked = true },
            new LootProp { Name = "Broken Machine", X = 38.60001f, Y = 5.1f, Z = 216.4f, Locked = false },
            new LootProp { Name = "Treasure", X = 46.60001f, Y = 5.1f, Z = 208.4f, Locked = false },
            new LootProp { Name = "A blasted Skeleton", X = 29.5f, Y = 5.1f, Z = 238.4f, Locked = false },
            new LootProp { Name = "Barrel", X = 57.70001f, Y = 5.1f, Z = 227.5f, Locked = true },
            new LootProp { Name = "A Crashed Android", X = 53.29999f, Y = 5.1f, Z = 201.6f, Locked = false },
            new LootProp { Name = "Broken Machine", X = 76.59999f, Y = 5.1f, Z = 150.3f, Locked = false },
            new LootProp { Name = "Barrel", X = 59.2f, Y = 5.1f, Z = 198.9f, Locked = false },
            new LootProp { Name = "Small Crate", X = 79.5f, Y = 5.1f, Z = 188.1f, Locked = false },
            new LootProp { Name = "Treasure", X = 81.10001f, Y = 5.1f, Z = 167.7f, Locked = false },
            new LootProp { Name = "Treasure", X = 21.1933f, Y = 5.1f, Z = 178.8232f, Locked = false },
            new LootProp { Name = "Skeleton", X = 68.39999f, Y = 5.1f, Z = 208.01f, Locked = false },
            new LootProp { Name = "Treasure", X = 48.2f, Y = 5.1f, Z = 124.4f, Locked = false },
            new LootProp { Name = "Barrel", X = 65.01f, Y = 5.1f, Z = 125.4f, Locked = false },
            new LootProp { Name = "Bottles and Garbage", X = 88.10001f, Y = 5.1f, Z = 158.3f, Locked = false },
            new LootProp { Name = "Barrel", X = 78.60001f, Y = 5.1f, Z = 194.8f, Locked = false },
            new LootProp { Name = "Treasure", X = 71.70001f, Y = 5.1f, Z = 191.5f, Locked = false },
            new LootProp { Name = "A bag", X = 98.41754f, Y = 5.100035f, Z = 178.7181f, Locked = false },
        };

        // Capture 20260724-mission-find-person PF 1419349 containers
        internal static readonly LootProp[] Props_1419349 =
        {
            new LootProp { Name = "A blasted Skeleton", X = 203.5f, Y = 5.1f, Z = 117.9f, Locked = false },
            new LootProp { Name = "Barrel", X = 238.6f, Y = 5.1f, Z = 111.7f, Locked = false },
            new LootProp { Name = "Treasure", X = 200.01f, Y = 5.1f, Z = 83.5f, Locked = false },
            new LootProp { Name = "Treasure", X = 238.8f, Y = 5.1f, Z = 41.5f, Locked = false },
            new LootProp { Name = "Treasure", X = 242.8f, Y = 5.1f, Z = 31.79999f, Locked = false },
            new LootProp { Name = "Barrel", X = 191.6f, Y = 5.1f, Z = 61.5f, Locked = false },
            new LootProp { Name = "Treasure", X = 207.2f, Y = 5.1f, Z = 148.2f, Locked = false },
            new LootProp { Name = "A Crashed Android", X = 221.2f, Y = 5.1f, Z = 144.3f, Locked = false },
            new LootProp { Name = "A Skull", X = 233.9f, Y = 5.1f, Z = 138.3f, Locked = false },
            new LootProp { Name = "Shadow Rift of the Eagle", X = 264.6f, Y = 5.1f, Z = 106.5f, Locked = false },
            new LootProp { Name = "A Broken Android", X = 278.01f, Y = 5.1f, Z = 62.01f, Locked = false },
            new LootProp { Name = "Treasure", X = 191.4f, Y = 5.1f, Z = 116.8f, Locked = false },
            new LootProp { Name = "Treasure", X = 186.7f, Y = 5.1f, Z = 118.6f, Locked = false },
            new LootProp { Name = "Barrel", X = 181.4f, Y = 5.1f, Z = 116.8f, Locked = false },
            new LootProp { Name = "Barrel", X = 171.7f, Y = 5.1f, Z = 94.4f, Locked = false },
            new LootProp { Name = "Broken Machine", X = 175.7f, Y = 5.1f, Z = 91.2f, Locked = false },
            new LootProp { Name = "Shadow Rift of the Firefly", X = 178.8f, Y = 5.1f, Z = 81.5f, Locked = false },
            new LootProp { Name = "A bag", X = 246.8f, Y = 5.1f, Z = 78.10001f, Locked = false },
        };

        /// <summary>
        /// Prefer shape-matched container layout; fall back to repair-capture props.
        /// </summary>
        internal static LootProp[] ResolveProps(int capturedPlayfieldId)
        {
            if (capturedPlayfieldId == 1441804)
            {
                return Props_1441804;
            }

            if (capturedPlayfieldId == 1419349)
            {
                return Props_1419349;
            }

            return Props;
        }
    }
}
