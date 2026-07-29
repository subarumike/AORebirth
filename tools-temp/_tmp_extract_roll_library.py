# Extract IN QuestAlternative N3 bodies from Rolling capture into C# template array.
from __future__ import print_function
import csv
import os
import struct

CAP = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260719-Rolling different mishes"
OUT = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_mission_roll_library.csfrag"

ICONS = {
    11329: "FindItemA",
    11330: "KillPerson",
    11335: "FindPerson",
    11337: "FindItemB",
    11342: "RepairMachine",
}

def find_icons(body):
    hits = []
    for icon, name in ICONS.items():
        needle = struct.pack(">I", icon)
        start = 0
        while True:
            j = body.find(needle, start)
            if j < 0:
                break
            hits.append(name)
            start = j + 1
    return hits

rolls = []
with open(os.path.join(CAP, "raw-packets.csv"), newline="", encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        if (row.get("N3TypeName") or "").strip() != "QuestAlternative":
            continue
        if (row.get("Direction") or "").strip().upper() != "IN":
            continue
        raw = bytes.fromhex((row.get("RawHex") or "").strip())
        if len(raw) <= 16:
            continue
        body = raw[16:]
        icons = find_icons(body)
        rolls.append({
            "utc": row.get("CapturedUtc"),
            "icons": icons,
            "hex": body.hex().upper(),
            "len": len(body),
        })

print("extracted", len(rolls), "rolls")
for i, r in enumerate(rolls):
    print(i, r["len"], r["icons"])

# Emit C# fragment
lines = []
lines.append("        // Capture 20260719-Rolling different mishes — 13 live IN QuestAlternative N3 bodies.")
lines.append("        // Each body is already a full 5-offer roll (icons match texts). Prefer whole-roll")
lines.append("        // selection over icon-swapping a single shell.")
lines.append("        private static readonly string[] CapturedRollBodiesHex =")
lines.append("        {")
for i, r in enumerate(rolls):
    comment = ",".join(r["icons"])
    lines.append('            // roll %d icons=[%s] bytes=%d' % (i, comment, r["len"]))
    # split hex into ~120-char chunks for readability
    h = r["hex"]
    chunk = 120
    parts = [h[j:j+chunk] for j in range(0, len(h), chunk)]
    lines.append('            "' + parts[0] + '"')
    for p in parts[1:]:
        lines.append('            + "' + p + '"')
    lines.append("            ,")
lines.append("        };")
open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
print("wrote", OUT)
