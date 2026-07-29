# Generate Rex Malfunctioning robot spawn defs + patrol CSV from 20260721-Rox-robots
from __future__ import print_function
import csv, os
from collections import defaultdict
from datetime import datetime

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260721-Rox-robots"
CSV_OUT = r"AORebirth\Server\ZoneEngine\Content\Captured\Arete\cleaning_robot_patrol_replay.csv"
CS_SNIP = r"tools-temp\_tmp_rex_robot_spawns.csfrag"

# Initial wave identities (lifecycle character-seen at capture start, pet=False)
INITIAL = [
    (0x79866553, 3594.546000, 51.745000, 799.167700),
    (0x79866565, 3595.688480, 51.745000, 798.922058),
    (0x797D36A5, 3596.811770, 51.745000, 788.208900),
    (0x79543CB6, 3596.979000, 51.745000, 783.935852),
    (0x79866547, 3602.961000, 52.135000, 787.817261),
    (0x7986655E, 3609.403810, 52.135000, 791.897034),
    (0x7986653C, 3612.843260, 52.135000, 787.514200),
    (0x79866562, 3612.874510, 52.135000, 787.537500),
    (0x79866518, 3612.924000, 52.135000, 787.641200),
    (0x79866560, 3617.227780, 51.745000, 785.991800),
    (0x7986655D, 3622.508540, 51.745000, 798.139500),
]
spawn_ids = set(x[0] for x in INITIAL)

# Collect first NpcPath dest per source + all rows for CSV
first_dest = {}
rows_by = defaultdict(list)
header = None
with open(os.path.join(CAP, "movement-packets.csv"), encoding="utf-8-sig", newline="") as f:
    r = csv.DictReader(f)
    header = r.fieldnames
    for row in r:
        if row.get("SourceName") != "Malfunctioning Cleaning Robot":
            continue
        if row.get("MessageType") != "FollowTarget":
            continue
        if row.get("FollowKind") != "NpcPath":
            continue
        try:
            si = int(row.get("SourceInstance") or "0", 16)
        except Exception:
            # decimal?
            try:
                si = int(row.get("SourceInstance") or "0")
            except Exception:
                continue
        # SourceInstance in this capture is hex string without 0x
        try:
            si = int(str(row.get("SourceInstance")), 16)
        except Exception:
            continue
        rows_by[si].append(row)
        if si in spawn_ids and si not in first_dest:
            try:
                first_dest[si] = (
                    float(row["DestinationX"]),
                    float(row["DestinationY"]),
                    float(row["DestinationZ"]),
                )
            except Exception:
                pass

# Write spawn fragment
lines = []
lines.append("        // Capture 20260721-Rox-robots: 11 concurrent Malfunctioning Cleaning Robots")
lines.append("        // on Rex platform (lifecycle pet=False). Excludes Burning/Cleaning Robot.")
lines.append("        private static readonly CapturedAreteRobotSpawnDefinition[] SpawnDefinitions =")
lines.append("        {")
for sid, x, y, z in INITIAL:
    if sid in first_dest:
        px, py, pz = first_dest[sid]
    else:
        # tiny local hover fallback (matches local-jitter bots)
        px, py, pz = x - 0.75, y, z + 0.25
    lines.append(
        "            new CapturedAreteRobotSpawnDefinition(0x%X, %.6ff, %.6ff, %.6ff, 12, 1, 6, %.6ff, %.6ff, %.6ff),"
        % (sid, x, y, z, px, py, pz)
    )
lines.append("        };")
with open(CS_SNIP, "w", encoding="utf-8") as f:
    f.write("\n".join(lines) + "\n")

# Write patrol CSV: only Malfunctioning NpcPath for spawn ids (plus keep all malf for replay if identity remapped - only spawn ids)
# Include ALL Malfunctioning NpcPath rows for our spawn source instances only (replay keyed at spawn).
out_rows = []
for sid in sorted(spawn_ids):
    out_rows.extend(rows_by.get(sid, []))

# sort by time
def ts(row):
    return row.get("CapturedUtc") or ""
out_rows.sort(key=ts)

with open(CSV_OUT, "w", encoding="utf-8", newline="") as f:
    w = csv.DictWriter(f, fieldnames=header, quoting=csv.QUOTE_ALL)
    w.writeheader()
    for row in out_rows:
        # normalize SourceInstance to hex uppercase without 0x (loader uses HexNumber)
        try:
            si = int(str(row.get("SourceInstance")), 16)
            row = dict(row)
            row["SourceInstance"] = "%X" % si
            row["SourceIdentity"] = "SimpleChar:%X" % si
        except Exception:
            pass
        w.writerow(row)

print("spawns", len(INITIAL))
print("patrol_rows", len(out_rows))
print("with_first_dest", len(first_dest))
print("missing_dest", [hex(s) for s in spawn_ids if s not in first_dest])
print("wrote", CSV_OUT, CS_SNIP)
