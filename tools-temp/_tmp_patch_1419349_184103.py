# Patch mission enter + map wire to fog gold 20260725-184103 (PF 1419349 / ACG D7418B).
from __future__ import print_function
import os
import re

ROOT = r"C:\Users\nermi\source\repos\AORebirth"
doors = open(os.path.join(ROOT, r"tools-temp\_tmp_doors_1419349.csfrag"), encoding="utf-8").read().strip()
chests = open(os.path.join(ROOT, r"tools-temp\_tmp_chests_1419349.csfrag"), encoding="utf-8").read().strip()
gen_hex = open(os.path.join(ROOT, r"tools-temp\_tmp_184103_gen.hex"), encoding="utf-8").read().strip()
gen = bytes.fromhex(gen_hex)

# --- DynelCapture doors/chests ---
dynel = os.path.join(ROOT, r"AORebirth\Server\ZoneEngine\Core\Missions\MissionInstanceDynelCapture.cs")
text = open(dynel, encoding="utf-8").read()
# Replace Doors_1419349 block through end of Chests_1419349
pat = re.compile(
    r"        // Capture 20260724-mission-find-person PF 1419349\r?\n"
    r"        public static readonly string\[\] Doors_1419349 =[\s\S]*?"
    r"        public static readonly string\[\] Chests_1419349 =[\s\S]*?"
    r"        \};",
    re.M,
)
repl = (
    "        // Capture 20260725-184103 PF 1419349 fog gold (ACG D7418B)\n"
    + doors
    + "\n\n"
    + chests
)
text2, n = pat.subn(repl, text, count=1)
if n != 1:
    raise SystemExit("DynelCapture doors/chests replace failed n=%d" % n)
open(dynel, "w", encoding="utf-8", newline="\n").write(text2)
print("patched DynelCapture doors/chests")

# --- Shape catalog spawn + generator ---
shape = os.path.join(ROOT, r"AORebirth\Server\ZoneEngine\Core\Playfields\MissionInstanceShapeCatalog.cs")
st = open(shape, encoding="utf-8").read()
old_spawn = """        // Shape playfield 1419349 from capture 20260724-mission-find-person (27 trash + FindPerson)
        new MissionShape
        {
            CapturedPlayfieldId = 1419349,
            // Capture 20260724-mission-find-person player start snapshot.
            SpawnX = 298.199f, SpawnY = 5.01f, SpawnZ = 85.01001f,"""
new_spawn = """        // Shape playfield 1419349 from capture 20260725-184103 (fog ACG D7418B)
        new MissionShape
        {
            CapturedPlayfieldId = 1419349,
            // Gold PAF CharacterCoordinates (enter).
            SpawnX = 1.80102539f, SpawnY = 5.01f, SpawnZ = 95.01001f,"""
if old_spawn not in st:
    raise SystemExit("spawn block not found")
st = st.replace(old_spawn, new_spawn, 1)

rows = []
for i in range(0, len(gen), 8):
    chunk = gen[i : i + 8]
    rows.append("                       " + ", ".join("0x%02X" % b for b in chunk) + ",")
gen_case = (
    "                case 1419349:\n"
    "                    // Fog gold ACG D7418B — capture 20260725-184103.\n"
    "                    return new byte[]\n"
    "                    {\n"
    + "\n".join(rows)
    + "\n"
    "                    };\n"
)
old_gen = (
    "                case 1419349:\n"
    "                    // Mid-instance find-person capture has no PlayfieldAnarchyF; reuse 1419335\n"
    "                    // until a true enter capture supplies the generator payload.\n"
    "                    goto case 1419335;\n"
)
if old_gen not in st:
    raise SystemExit("gen case not found")
st = st.replace(old_gen, gen_case, 1)
open(shape, "w", encoding="utf-8", newline="\n").write(st)
print("patched ShapeCatalog spawn+gen")

# --- Force enter PF 1419349 ---
svc = os.path.join(ROOT, r"AORebirth\Server\ZoneEngine\Core\Missions\MissionInstanceService.cs")
sv = open(svc, encoding="utf-8").read()
old_res = """        /// <summary>
        /// Fog gold 080425 / 151009: Playfield2 is always 1441800 with ACG D7417D.
        /// </summary>
        internal static int ResolveInstancePlayfieldId(ICharacter character)
        {
            int[] doorShapes = MissionInstanceDynelCapture.ShapePlayfieldIds;
            if (doorShapes == null || doorShapes.Length == 0)
            {
                return InstancePlayfieldId;
            }

            // Remapping to 0x160009+ made the client fail ACG → full open/grey PF Map.
            // Live reuses 1441800 every enter; fog still starts black.
            const int fogShapePf = 1441800;
            StampShapeSource(fogShapePf, fogShapePf);
            return fogShapePf;
        }"""
new_res = """        /// <summary>
        /// Fog gold 20260725-184103: Playfield2 = 1419349 with ACG D7418B.
        /// </summary>
        internal static int ResolveInstancePlayfieldId(ICharacter character)
        {
            int[] doorShapes = MissionInstanceDynelCapture.ShapePlayfieldIds;
            if (doorShapes == null || doorShapes.Length == 0)
            {
                return InstancePlayfieldId;
            }

            // Exact gold PF id + building; remap / foreign ACG → open grey map.
            const int fogShapePf = 1419349;
            StampShapeSource(fogShapePf, fogShapePf);
            return fogShapePf;
        }"""
if old_res not in sv:
    raise SystemExit("ResolveInstancePlayfieldId block not found")
sv = sv.replace(old_res, new_res, 1)

old_clear = """                case 1419349:
                    // Capture start faces into the mish along -X from (298,85).
                    x -= 6.0f;
                    break;"""
new_clear = """                case 1419349:
                    // Gold 184103 PAF spawn (1.8,95) already clear of exit door — no nudge.
                    break;"""
if old_clear not in sv:
    raise SystemExit("clearance case not found")
sv = sv.replace(old_clear, new_clear, 1)
open(svc, "w", encoding="utf-8", newline="\n").write(sv)
print("patched MissionInstanceService")

# --- N3Teleport ACG entry dest ---
tel = os.path.join(ROOT, r"AORebirth\Server\ZoneEngine\Core\MessageHandlers\TeleportMessageHandler.cs")
tt = open(tel, encoding="utf-8").read()
old_dest = """                    // Gold 20260725-151009: N3Teleport Destination is ACG entry
                    // (495.77, 5.28, 200.95). Interior spawn / PAF coords are (~298, 5, 235).
                    // Sending spawn XYZ here made the client load ACG wrong → open PF Map.
                    float destX = (float)destination.x;
                    float destY = (float)destination.y;
                    float destZ = (float)destination.z;
                    int shapePf;
                    if (playfield.Instance == 1441800
                        || (ZoneEngine.Core.Missions.MissionInstanceService.TryGetShapeSource(
                                playfield.Instance,
                                out shapePf)
                            && shapePf == 1441800))
                    {
                        destX = 495.77f;
                        destY = 5.28f;
                        destZ = 200.95f;
                    }"""
new_dest = """                    // Gold 20260725-184103: N3Teleport Destination is ACG entry
                    // (545.43, 8.51, 350.52). Interior spawn / PAF coords are (~1.8, 5, 95).
                    // Sending spawn XYZ here made the client load ACG wrong → open PF Map.
                    float destX = (float)destination.x;
                    float destY = (float)destination.y;
                    float destZ = (float)destination.z;
                    int shapePf;
                    if (playfield.Instance == 1419349
                        || (ZoneEngine.Core.Missions.MissionInstanceService.TryGetShapeSource(
                                playfield.Instance,
                                out shapePf)
                            && shapePf == 1419349))
                    {
                        destX = 545.43f;
                        destY = 8.51f;
                        destZ = 350.52f;
                    }"""
if old_dest not in tt:
    raise SystemExit("teleport dest block not found")
tt = tt.replace(old_dest, new_dest, 1)
open(tel, "w", encoding="utf-8", newline="\n").write(tt)
print("patched TeleportMessageHandler")
print("done")
