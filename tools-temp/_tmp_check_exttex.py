# -*- coding: utf-8 -*-
import csv
import re
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-loralei")
# expected arrays from LoreleiOasisMobRuntime
expected = {
    "Lolly": bytes([
        0x00, 0x00, 0x07, 0xE2, 0x63, 0x75, 0x74, 0x65, 0x5F, 0x62, 0x69, 0x72, 0x64, 0x79, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x76, 0x6F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    ]),
    "Desert Reet": bytes([
        0x00, 0x00, 0x07, 0xE2, 0x63, 0x75, 0x74, 0x65, 0x5F, 0x62, 0x69, 0x72, 0x64, 0x79, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x76, 0x72, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    ]),
    "Rollerrat": bytes([
        0x00, 0x00, 0x07, 0xE2, 0x4D, 0x61, 0x74, 0x65, 0x72, 0x69, 0x61, 0x6C, 0x20, 0x23, 0x31, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x9C, 0x1E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    ]),
}

# Search raw-packets for ExtTex marker 07E2cute_birdy or Material #1
raw = cap / "raw-packets.csv"
found = {k: False for k in expected}
if raw.exists():
    with raw.open(encoding="utf-8", errors="replace", newline="") as f:
        reader = csv.DictReader(f)
        cols = reader.fieldnames or []
        print("raw cols sample", cols[:12])
        for row in reader:
            blob = " ".join(str(v) for v in row.values() if v)
            # look for hex of material ids
            if "01766F" in blob.replace(" ", "").upper() or "01 76 6F" in blob.upper():
                found["Lolly"] = True
            if "017672" in blob.replace(" ", "").upper() or "01 76 72" in blob.upper():
                found["Desert Reet"] = True
            if "009C1E" in blob.replace(" ", "").upper() or "00 9C 1E" in blob.upper():
                found["Rollerrat"] = True

# Also check enemy-dossier for ExtTex fields
import json
dossier = json.loads((cap / "enemy-dossier.json").read_text(encoding="utf-8"))
items = dossier if isinstance(dossier, list) else dossier.get("enemies") or dossier.get("npcs") or []
print("dossier count", len(items) if isinstance(items, list) else type(items))
if isinstance(items, list) and items:
    print("sample keys", sorted(items[0].keys())[:40])
    for e in items[:5]:
        name = e.get("Name") or e.get("name") or e.get("DisplayName")
        print(" sample", name, {k: e.get(k) for k in e if "tex" in k.lower() or "ext" in k.lower() or "scfu" in k.lower() or "raw" in k.lower()})

print("material id hits in raw:", found)
print("expected Lolly last-12", expected["Lolly"][-12:].hex())
print("expected Reet last-12", expected["Desert Reet"][-12:].hex())
print("expected Rat last-12", expected["Rollerrat"][-12:].hex())
