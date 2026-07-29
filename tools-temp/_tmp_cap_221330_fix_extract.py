# -*- coding: utf-8 -*-
import csv, json, os
cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260723-221330"
out = r"tools-temp\_tmp_cap_221330_fix_extract.txt"
d = json.load(open(os.path.join(cap, "enemy-dossier.json"), encoding="utf-8-sig"))
info = json.load(open(os.path.join(cap, "capture_info.json"), encoding="utf-8-sig"))
focus = set(x.strip("()").split(":")[-1].upper() for x in info["focusedEnemyIdentities"])
lines = []
lines.append("=== FOCUSED DOSSIER ===")
for e in d["enemies"]:
    idh = e["identity"].strip("()").split(":")[-1].upper()
    if idh not in focus:
        continue
    lines.append(
        "%s | %s | runtimePf=%s captureObj=%s md=%s hm=%s flags=? pos=(%.3f,%.3f,%.3f)"
        % (
            idh,
            e["name"],
            e.get("runtimePlayfieldId"),
            e.get("capturePlayfieldObjectId"),
            e.get("monsterData"),
            e.get("headMesh"),
            e["position"]["x"],
            e["position"]["y"],
            e["position"]["z"],
        )
    )

# SCFU rows for focused humanoids
lines.append("\n=== SCFU APPEARANCE (focused / named) ===")
wanted = (
    "Drake",
    "Creehan",
    "Emissary",
    "Guardian",
    "Goldman",
    "Monaghan",
    "Glowtail",
    "Gavrillo",
    "McDougal",
    "Erke",
    "Falker",
    "Chimera",
    "Silvertail",
    "Yuttos",
)
with open(os.path.join(cap, "scfu-appearance.csv"), encoding="utf-8-sig", newline="") as f:
    for row in csv.DictReader(f):
        name = row.get("Name") or ""
        ident = row.get("Identity") or row.get("NpcIdentity") or ""
        idh = ident.strip("()").split(":")[-1].upper() if ":" in ident else ""
        if idh in focus or any(w in name for w in wanted):
            lines.append(
                "%s | %s | pf=%s hm=%s flags=%s md=%s tex=%s mesh=%s"
                % (
                    idh,
                    name,
                    row.get("PlayfieldId"),
                    row.get("HeadMesh"),
                    row.get("CharacterFlags") or row.get("FlagsNumeric"),
                    row.get("MonsterData"),
                    (row.get("Textures") or "")[:120],
                    (row.get("Meshes") or "")[:140],
                )
            )

# FollowTarget sample for Barking Chimera 798E09BC and a few others
lines.append("\n=== FOLLOWTARGET PATH SAMPLES (Chimera) ===")
chimera_ids = set()
for e in d["enemies"]:
    if e["name"] == "Barking Chimera":
        chimera_ids.add(e["identity"].strip("()").split(":")[-1].upper())

paths = {}
with open(os.path.join(cap, "movement-packets.csv"), encoding="utf-8-sig", newline="") as f:
    for row in csv.DictReader(f):
        if (row.get("MessageType") or "") != "FollowTarget":
            continue
        ident = row.get("Identity") or row.get("SourceIdentity") or ""
        idh = ident.strip("()").split(":")[-1].upper() if ":" in ident else ""
        if idh not in chimera_ids and idh not in focus:
            continue
        try:
            x = float(row.get("DestinationX") or row.get("X") or 0)
            y = float(row.get("DestinationY") or row.get("Y") or 0)
            z = float(row.get("DestinationZ") or row.get("Z") or 0)
        except Exception:
            continue
        paths.setdefault(idh, []).append((x, y, z))

for idh, pts in list(paths.items())[:12]:
    if len(pts) < 2:
        continue
    # downsample to ~4 waypoints
    idxs = [0, len(pts) // 3, (2 * len(pts)) // 3, len(pts) - 1]
    sample = [pts[i] for i in idxs]
    name = next((e["name"] for e in d["enemies"] if e["identity"].upper().endswith(idh)), "?")
    lines.append("%s %s n=%d sample=%s" % (idh, name, len(pts), sample))

# corpse catmesh from corpse-full-updates
lines.append("\n=== CORPSE FULL UPDATES ===")
with open(os.path.join(cap, "corpse-full-updates.csv"), encoding="utf-8-sig", newline="") as f:
    for row in csv.DictReader(f):
        lines.append(str({k: row.get(k) for k in row.keys() if row.get(k)}))

open(out, "w", encoding="utf-8").write("\n".join(lines))
print("wrote", out, "lines", len(lines))
