# Splice shape 1419349 + loot props + IsGrey field wiring helpers.
from __future__ import print_function
import os, re

ROOT = r"C:\Users\nermi\source\repos\AORebirth"
shape = open(os.path.join(ROOT, r"tools-temp\_tmp_shape_1419349.csfrag"), encoding="utf-8").read().rstrip() + "\n\n"
loot = open(os.path.join(ROOT, r"tools-temp\_tmp_loot_1419349.csfrag"), encoding="utf-8").read().rstrip() + "\n\n"

catalog = os.path.join(ROOT, r"AORebirth\Server\ZoneEngine\Core\Playfields\MissionInstanceShapeCatalog.cs")
text = open(catalog, encoding="utf-8").read()
if "CapturedPlayfieldId = 1419349" in text:
    print("shape already present")
else:
    # insert IsGrey on MissionNpc if missing
    if "public bool IsGrey;" not in text:
        text = text.replace(
            "        public int[][] Meshes;\n    }",
            "        public int[][] Meshes;\n\n        /// <summary>Grey trash (no side textures) does not raise token %.</summary>\n        public bool IsGrey;\n    }")
        print("added IsGrey field")
    # update doc comment
    text = text.replace(
        "and Find-Item capture <c>20260724-mission-find-item</c> (PF 1441804).",
        "Find-Item capture <c>20260724-mission-find-item</c> (PF 1441804),\n    /// and Find-Person capture <c>20260724-mission-find-person</c> (PF 1419349).")
    marker = "\n        };\n\n        internal static MissionShape PickShape"
    if marker not in text:
        raise SystemExit("marker not found")
    text = text.replace(marker, "\n" + shape + "        };\n\n        internal static MissionShape PickShape", 1)
    open(catalog, "w", encoding="utf-8", newline="\n").write(text)
    print("spliced shape 1419349")

loot_path = os.path.join(ROOT, r"AORebirth\Server\ZoneEngine\Core\Missions\MissionLootContainerCatalog.cs")
lt = open(loot_path, encoding="utf-8").read()
if "Props_1419349" in lt:
    print("loot already present")
else:
    lt = lt.replace(
        "Find-Item capture 20260724-mission-find-item (PF 1441804).",
        "Find-Item capture 20260724-mission-find-item (PF 1441804),\n    /// Find-Person capture 20260724-mission-find-person (PF 1419349).")
    # insert props before ResolveProps
    insert_at = lt.find("        /// <summary>\n        /// Prefer shape-matched container layout")
    if insert_at < 0:
        raise SystemExit("loot insert point missing")
    lt = lt[:insert_at] + loot + lt[insert_at:]
    lt = lt.replace(
        """            if (capturedPlayfieldId == 1441804)
            {
                return Props_1441804;
            }

            return Props;""",
        """            if (capturedPlayfieldId == 1441804)
            {
                return Props_1441804;
            }

            if (capturedPlayfieldId == 1419349)
            {
                return Props_1419349;
            }

            return Props;""")
    open(loot_path, "w", encoding="utf-8", newline="\n").write(lt)
    print("spliced loot 1419349")
