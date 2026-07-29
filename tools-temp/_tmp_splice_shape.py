# Splice shape 1441804 into MissionInstanceShapeCatalog.cs and update loot catalog.
from __future__ import print_function
import os

shape = open(r"tools-temp\_tmp_shape_1441804.csfrag", encoding="utf-8").read().rstrip() + "\n\n"
catalog = r"AORebirth\Server\ZoneEngine\Core\Playfields\MissionInstanceShapeCatalog.cs"
text = open(catalog, encoding="utf-8").read()
marker = "\n        };\n\n        internal static MissionShape PickShape"
if marker not in text:
    raise SystemExit("marker not found")
if "CapturedPlayfieldId = 1441804" in text:
    print("shape already present")
else:
    text = text.replace(marker, "\n" + shape + "        };\n\n        internal static MissionShape PickShape", 1)
    open(catalog, "w", encoding="utf-8", newline="\n").write(text)
    print("spliced shape into catalog")

# Update MissionLootContainerCatalog
loot_path = r"AORebirth\Server\ZoneEngine\Core\Missions\MissionLootContainerCatalog.cs"
props = open(r"tools-temp\_tmp_loot_1441804.csfrag", encoding="utf-8").read().rstrip() + "\n"
new_loot = '''namespace ZoneEngine.Core.Missions
{
    /// <summary>
    /// Lootable mission-interior containers.
    /// Repair capture 20260724-repaair-machine-mish (PF 1419360) and
    /// Find-Item capture 20260724-mission-find-item (PF 1441804).
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

''' + props.replace("internal static readonly LootProp[] Props_1441804 =", "        internal static readonly LootProp[] Props_1441804 =") + '''
        /// <summary>
        /// Prefer shape-matched container layout; fall back to repair-capture props.
        /// </summary>
        internal static LootProp[] ResolveProps(int capturedPlayfieldId)
        {
            if (capturedPlayfieldId == 1441804)
            {
                return Props_1441804;
            }

            return Props;
        }
    }
}
'''
# Fix indentation of props block - the frag already has correct indent
open(loot_path, "w", encoding="utf-8", newline="\n").write(new_loot)
print("wrote loot catalog")
