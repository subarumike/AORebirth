# Build MissionInstanceShapeCatalog.cs + layout payloads + door/chest captures from extracted assets.
from __future__ import print_function
import os, struct

ROOT = r"C:\Users\nermi\source\repos\AORebirth"
ASSETS = os.path.join(ROOT, r"tools-temp\_tmp_mission_shapes_assets")
MISSIONS = os.path.join(ROOT, r"AORebirth\Server\ZoneEngine\Core\Missions")
PLAYFIELDS = os.path.join(ROOT, r"AORebirth\Server\ZoneEngine\Core\Playfields")

def hex_to_bytes(h):
    return bytes.fromhex(h.strip())

def extract_generator_payload(raw_hex):
    """Strip transport header (16) then parse PAF fields until PlayfieldId2; remainder = payload."""
    raw = hex_to_bytes(raw_hex)
    if len(raw) > 16 and raw[2:4] == b"\x00\x0A":
        body = raw[16:]
    else:
        body = raw
    # N3MessageType(4) + Identity(8) + Unknown(1) + Unknown1(4) + XYZ(12) + Unknown2(1)
    # + PlayfieldId1(8) + Unknown3(4) + Unknown4(4) + PlayfieldId2(8)
    off = 4 + 8 + 1 + 4 + 12 + 1 + 8 + 4 + 4 + 8
    if off >= len(body):
        return None
    payload = body[off:]
    if len(payload) < 4:
        return None
    first = struct.unpack_from(">i", payload, 0)[0]
    # Accept generator-looking payloads
    if first in (0x0000C79F, 0x0000C77D, 0x0000C748, 0x0000C73D) or (first & 0xFFFF0000) == 0x0000C700:
        return payload
    # Door identity type often 0x0000C748
    return payload

def csharp_byte_array(data, indent="                       "):
    lines = []
    for i in range(0, len(data), 8):
        chunk = data[i:i+8]
        parts = ", ".join("0x%02X" % b for b in chunk)
        if i + 8 < len(data):
            lines.append(indent + parts + ",")
        else:
            lines.append(indent + parts)
    return "\n".join(lines)

# --- payloads per shape ---
payloads = {}
for pf in (1419310, 1419335, 1419382):
    path = os.path.join(ASSETS, "paf_%d.hex" % pf)
    lines = [ln for ln in open(path).read().splitlines() if ln.strip()]
    # Prefer longest unique payload
    best = None
    for ln in lines:
        pl = extract_generator_payload(ln)
        if pl and (best is None or len(pl) > len(best)):
            best = pl
    payloads[pf] = best
    print("PF", pf, "payload_bytes", len(best) if best else 0, "first", "%08X" % struct.unpack_from(">I", best, 0)[0] if best else None)

# --- assemble shape catalog ---
frag = open(os.path.join(ASSETS, "shapes_fragment.cs"), encoding="utf-8").read()

# loot entries from capture
loot_entries = [
    (26137, 130209, 130210, 154),  # Veteran Ruffian
    (26135, 142916, 142917, 137),  # Tough Plunderer
    (26090, 121905, 121906, 127),  # Tough Nanogun
    (26139, 101406, 101334, 146),  # Hardened Nanohoarder
]

catalog_cs = r'''namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;

    #endregion

    /// <summary>
    /// Capture-backed RK mission interior shapes from
    /// <c>20260719-5-different-shape-fo-mish</c> (PFs 1419310 / 1419335 / 1419382).
    /// Holds precise SCFU textures/meshes/positions for trash + FindTarget + KillBoss roles.
    /// Layout generator payloads and door/chest wire are stored separately for replay.
    /// </summary>
    internal enum MissionNpcRole
    {
        Trash = 0,
        FindTarget = 1,
        KillBoss = 2,
        KillGuard = 3,
        BrokenMachine = 4
    }

    internal sealed class MissionNpc
    {
        public string Name;
        public MissionNpcRole Role;
        public int Level;
        public int Health;
        public int MonsterData;
        public int Scale;
        public int HeadMesh;
        public float X;
        public float Y;
        public float Z;
        public float Hx;
        public float Hy;
        public float Hz;
        public float Hw;
        public int[][] Textures;
        public int[][] Meshes;
    }

    internal sealed class MissionShape
    {
        public int CapturedPlayfieldId;
        public float SpawnX;
        public float SpawnY;
        public float SpawnZ;
        public MissionNpc[] Npcs;
        public byte[] GeneratorPayload;
    }

    internal static class MissionInstanceShapeCatalog
    {
        // Capture ACG building generator type/instance from enter teleports (ACGBuildingGeneratorData:D74044..).
        internal const int CapturedBuildingType = unchecked((int)0x0000C79F);
        // Prefer D74044 as default building instance; live used D74044/45/46/48 per enter.
        internal const int CapturedBuildingInstance = unchecked((int)0x00D74044);

        internal static readonly MissionShape[] Shapes =
        {
''' + frag + r'''
        };

        internal static MissionShape PickShape(int playfieldInstance, Random rng)
        {
            if (Shapes == null || Shapes.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < Shapes.Length; i++)
            {
                if (Shapes[i].CapturedPlayfieldId == playfieldInstance)
                {
                    return Shapes[i];
                }
            }

            if (rng == null)
            {
                rng = new Random(playfieldInstance);
            }

            return Shapes[Math.Abs(playfieldInstance) % Shapes.Length];
        }

        internal static bool IsCapturedShapePlayfield(int playfieldInstance)
        {
            for (int i = 0; i < Shapes.Length; i++)
            {
                if (Shapes[i].CapturedPlayfieldId == playfieldInstance)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Corpse loot observed inside capture 20260719-5-different-shape-fo-mish (Items=low:high:ql:qty).
    /// </summary>
    internal static class MissionInstanceLootCatalog
    {
        internal sealed class LootDrop
        {
            public int MonsterData;
            public int LowId;
            public int HighId;
            public int Quality;
        }

        internal static readonly LootDrop[] CapturedDrops =
        {
'''

for md, low, high, ql in loot_entries:
    catalog_cs += "            new LootDrop { MonsterData = %d, LowId = %d, HighId = %d, Quality = %d },\n" % (md, low, high, ql)

catalog_cs += r'''        };

        internal static bool TryGetDrop(int monsterData, out LootDrop drop)
        {
            drop = null;
            for (int i = 0; i < CapturedDrops.Length; i++)
            {
                if (CapturedDrops[i].MonsterData == monsterData)
                {
                    drop = CapturedDrops[i];
                    return true;
                }
            }

            return false;
        }
    }
}
'''

# Inject payloads into shapes after CapturedPlayfieldId lines — simpler: patch GeneratorPayload in static ctor style
# Rewrite: append payload assignment helper method instead of embedding in each shape initializer.
# Add GetGeneratorPayload(pf) method to catalog.

payload_method = "\n        internal static byte[] GetGeneratorPayload(int capturedPlayfieldId)\n        {\n"
payload_method += "            switch (capturedPlayfieldId)\n            {\n"
for pf, pl in payloads.items():
    if not pl:
        continue
    payload_method += "                case %d:\n                    return new byte[]\n                    {\n" % pf
    payload_method += csharp_byte_array(pl) + "\n                    };\n"
payload_method += "                default:\n                    return null;\n            }\n        }\n"

catalog_cs = catalog_cs.replace(
    "        internal static bool IsCapturedShapePlayfield(int playfieldInstance)",
    payload_method + "\n        internal static bool IsCapturedShapePlayfield(int playfieldInstance)"
)

out_path = os.path.join(PLAYFIELDS, "MissionInstanceShapeCatalog.cs")
open(out_path, "w", encoding="utf-8", newline="\n").write(catalog_cs)
print("wrote", out_path, "bytes", len(catalog_cs))

# --- Door + Chest capture file ---
door_cs = '''namespace ZoneEngine.Core.Missions
{
    /// <summary>
    /// DoorFullUpdate + ChestFullUpdate packets from capture 20260719-5-different-shape-fo-mish.
    /// Keys are captured playfield ids (1419310 / 1419335 / 1419382). Replayed on zone-in.
    /// </summary>
    internal static class MissionInstanceDynelCapture
    {
        public const int CapturedCharacterInstance = unchecked((int)0x762ABC21);

        public static readonly int[] ShapePlayfieldIds = { 1419310, 1419335, 1419382 };

'''

for pf in (1419310, 1419335, 1419382):
    doors = [ln.strip() for ln in open(os.path.join(ASSETS, "doors_%d.hex" % pf)).read().splitlines() if ln.strip()]
    chests = [ln.strip() for ln in open(os.path.join(ASSETS, "chests_%d.hex" % pf)).read().splitlines() if ln.strip()]
    door_cs += "        public static readonly string[] Doors_%d =\n        {\n" % pf
    for d in doors:
        # chunk string
        door_cs += '            "' + d + '",\n'
    door_cs += "        };\n\n"
    door_cs += "        public static readonly string[] Chests_%d =\n        {\n" % pf
    for d in chests:
        door_cs += '            "' + d + '",\n'
    door_cs += "        };\n\n"

door_cs += '''
        public static string[] GetDoors(int playfieldId)
        {
            switch (playfieldId)
            {
                case 1419310: return Doors_1419310;
                case 1419335: return Doors_1419335;
                case 1419382: return Doors_1419382;
                default: return Doors_1419310;
            }
        }

        public static string[] GetChests(int playfieldId)
        {
            switch (playfieldId)
            {
                case 1419310: return Chests_1419310;
                case 1419335: return Chests_1419335;
                case 1419382: return Chests_1419382;
                default: return Chests_1419310;
            }
        }
    }
}
'''

dynel_path = os.path.join(MISSIONS, "MissionInstanceDynelCapture.cs")
open(dynel_path, "w", encoding="utf-8", newline="\n").write(door_cs)
print("wrote", dynel_path, "bytes", len(door_cs))
