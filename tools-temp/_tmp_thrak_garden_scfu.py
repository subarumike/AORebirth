import csv
import collections
import json
import os

base = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260718-165625"
rows = list(csv.DictReader(open(os.path.join(base, "scfu-appearance.csv"), encoding="utf-8-sig")))
print("scfu rows", len(rows))
print("playfields", collections.Counter(r.get("PlayfieldId") for r in rows))

# Prefer NPC rows (non-empty name, not player). Dedupe by Identity keeping first full decode.
by_id = {}
for r in rows:
    name = (r.get("Name") or "").strip()
    ident = (r.get("Identity") or "").strip()
    if not name or not ident:
        continue
    if name == "Cratonera":
        continue
    status = (r.get("DecodeStatus") or "").strip().lower()
    if status and status not in ("ok", "success", "full", ""):
        # still keep if we have textures
        pass
    if ident not in by_id:
        by_id[ident] = r

print("unique npc identities", len(by_id))
for ident, r in sorted(by_id.items(), key=lambda kv: kv[1].get("Name") or ""):
    print(
        "NAME={0}|ID={1}|LVL={2}|HP={3}|MD={4}|SCALE={5}|VF={6}|HEAD={7}|PF={8}|POS={9},{10},{11}|H={12},{13},{14},{15}|TEX={16}|MESH={17}".format(
            r.get("Name"),
            ident,
            r.get("Level"),
            r.get("Health"),
            r.get("MonsterData"),
            r.get("MonsterScale"),
            r.get("VisualFlags"),
            r.get("HeadMesh"),
            r.get("PlayfieldId"),
            r.get("PositionX"),
            r.get("PositionY"),
            r.get("PositionZ"),
            r.get("HeadingX"),
            r.get("HeadingY"),
            r.get("HeadingZ"),
            r.get("HeadingW"),
            r.get("Textures"),
            r.get("Meshes"),
        )
    )

d = json.load(open(os.path.join(base, "enemy-dossier.json"), encoding="utf-8-sig"))
print("dossier runtime", d.get("runtimePlayfieldId"), "object", d.get("capturePlayfieldObjectId"))
print("dossier enemies", len(d.get("enemies", [])))
for e in d.get("enemies", []):
    print(" D", e.get("name"), e.get("level"), e.get("monsterData"), e.get("position"))
