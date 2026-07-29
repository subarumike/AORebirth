from pathlib import Path
import csv
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

rows = list(csv.DictReader(Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-234012/raw-packets.csv").open(encoding="utf-8-sig", newline="")))

for idx in (121, 362, 478, 354):
    r = rows[idx]
    hx = r["RawHex"].strip()
    print(f"\n=== {idx} {r['Direction']} {r['N3TypeName']} ===")
    print(hx)
    b = bytes.fromhex(hx)
    # find all possible magics
    for mag in ("5E477770", "4D2A313B", "46312D2E"):
        i = b.find(bytes.fromhex(mag))
        print(f"  {mag} at {i}")
    # try interpret from end: many OUT have trailing
    # Find C350765A6D34 then parse action after identity+unk
    key = bytes.fromhex("0000C350765A6D34")
    i = b.find(key)
    print(f"  identity at {i}")
    if i >= 0:
        # after identity: unk byte + action?
        rest = b[i+8:]
        print(f"  after id: {rest[:40].hex()}")
        if len(rest) >= 25:
            unk = rest[0]
            act = int.from_bytes(rest[1:5], "big")
            unk1 = int.from_bytes(rest[5:9], "big")
            tgt_t = int.from_bytes(rest[9:13], "big")
            tgt_i = int.from_bytes(rest[13:17], "big")
            p1 = int.from_bytes(rest[17:21], "big", signed=True)
            p2 = int.from_bytes(rest[21:25], "big", signed=True)
            print(f"  parseA unk={unk} act=0x{act:X} unk1={unk1} tgt={tgt_t:X}:{tgt_i:X} p1={p1} p2={p2}")

# Find TeamInvite packet by type 4D2A313B
print("\n=== packets with TeamInvite magic 4D2A313B ===")
for idx, r in enumerate(rows):
    hx = (r.get("RawHex") or "").strip()
    if not hx:
        continue
    b = bytes.fromhex(hx)
    i = b.find(bytes.fromhex("4D2A313B"))
    if i < 0:
        continue
    print(f"{idx} {r.get('Direction')} N3={r.get('N3TypeName')} at={i}")
    print(f"  {hx}")
    body = b[i+4:]
    print(f"  body={body.hex()}")
