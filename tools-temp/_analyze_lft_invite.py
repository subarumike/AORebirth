from pathlib import Path
import csv
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# Find captures that might have LFT
root = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures")
if root.exists():
    for d in sorted(root.iterdir()):
        if not d.is_dir():
            continue
        # look for chat or LFT in filenames / csv
        names = [p.name for p in d.iterdir()]
        print(d.name, names[:8])

print("\n=== scan recent captures for TeamRequestInvite OUT targets ===")
for name in ("20260728-234012", "20260728-232300"):
    cap = root / name
    csvp = cap / "raw-packets.csv"
    if not csvp.exists():
        continue
    rows = list(csv.DictReader(csvp.open(encoding="utf-8-sig", newline="")))
    for idx, r in enumerate(rows):
        if (r.get("N3TypeName") or "") != "CharacterAction":
            continue
        if (r.get("Direction") or "") != "OUT":
            continue
        b = bytes.fromhex(r["RawHex"].strip())
        i = b.find(bytes.fromhex("5E477770"))
        if i < 0:
            continue
        rest = b[i+4:]
        act = int.from_bytes(rest[9:13], "big")
        if act != 0x1A:
            continue
        tt = int.from_bytes(rest[17:21], "big")
        ti = int.from_bytes(rest[21:25], "big")
        print(f"{name} #{idx} OUT 0x1A tgtType={tt:X} tgtInst={ti:X} ({ti})")
