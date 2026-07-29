# Generate MissionLocationPool.cs from capture 20260718-053650
from __future__ import print_function
import csv, struct, collections, os

raw = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260718-053650/raw-packets.csv"
locs = []
with open(raw, newline="", encoding="utf-8-sig", errors="replace") as f:
    reader = csv.DictReader(f)
    for row in reader:
        if row.get("N3TypeName") != "QuestAlternative":
            continue
        if row.get("Direction") != "IN":
            continue
        hx = row.get("RawHex") or ""
        if len(hx) < 400:
            continue
        data = bytes.fromhex(hx if len(hx) % 2 == 0 else hx[:-1])
        for i in range(0, len(data) - 28):
            if data[i:i+4] != b"\x00\x00\x9c\x50":
                continue
            pf = struct.unpack(">I", data[i+4:i+8])[0]
            u18 = struct.unpack(">i", data[i+8:i+12])[0]
            u19 = struct.unpack(">i", data[i+12:i+16])[0]
            x, y, z = struct.unpack(">fff", data[i+16:i+28])
            if 1 <= pf <= 5000 and -100 < y < 500 and 0 < x < 5000 and 0 < z < 5000:
                locs.append((pf, x, y, z, u18, u19))

# unique by rounded pf/x/z, prefer diversity of playfields
uniq = collections.OrderedDict()
for L in locs:
    key = (L[0], int(L[1] // 25), int(L[3] // 25))
    if key not in uniq:
        uniq[key] = L

# cap ~120, round-robin by playfield for spread
by_pf = collections.defaultdict(list)
for L in uniq.values():
    by_pf[L[0]].append(L)
selected = []
while by_pf and len(selected) < 120:
    for pf in list(by_pf.keys()):
        if not by_pf[pf]:
            del by_pf[pf]
            continue
        selected.append(by_pf[pf].pop(0))
        if len(selected) >= 120:
            break

out = r"AORebirth/Server/ZoneEngine/Core/Missions/MissionLocationPool.cs"
os.makedirs(os.path.dirname(out), exist_ok=True)
lines = []
lines.append("namespace ZoneEngine.Core.Missions")
lines.append("{")
lines.append("    /// <summary>")
lines.append("    /// Capture-backed RK mission marker locations (playfield + XYZ + entrance ids)")
lines.append("    /// extracted from live QuestAlternative rolls in capture 20260718-053650.")
lines.append("    /// </summary>")
lines.append("    internal static class MissionLocationPool")
lines.append("    {")
lines.append("        internal sealed class Spot")
lines.append("        {")
lines.append("            public int Playfield;")
lines.append("            public float X;")
lines.append("            public float Y;")
lines.append("            public float Z;")
lines.append("            public int EntranceLow;")
lines.append("            public int EntranceHigh;")
lines.append("        }")
lines.append("")
lines.append("        internal static readonly Spot[] Spots =")
lines.append("        {")
for pf, x, y, z, u18, u19 in selected:
    lines.append(
        "            new Spot { Playfield = %d, X = %sF, Y = %sF, Z = %sF, EntranceLow = %d, EntranceHigh = %d },"
        % (pf, repr(round(x, 3)), repr(round(y, 3)), repr(round(z, 3)), u18, u19)
    )
lines.append("        };")
lines.append("    }")
lines.append("}")
open(out, "w", encoding="utf-8").write("\n".join(lines) + "\n")
print("wrote", out, "spots", len(selected), "playfields", len(set(s[0] for s in selected)))
