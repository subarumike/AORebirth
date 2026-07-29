from pathlib import Path
import csv
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-234012")
rows = list(csv.DictReader((cap/"raw-packets.csv").open(encoding="utf-8-sig", newline="")))

def parse_ca(hx):
    b = bytes.fromhex(hx.strip())
    i = b.find(bytes.fromhex("5E477770"))
    if i < 0: return None
    rest = b[i+4:]
    act = int.from_bytes(rest[9:13], "big")
    tgt_t = int.from_bytes(rest[17:21], "big")
    tgt_i = int.from_bytes(rest[21:25], "big")
    p1 = int.from_bytes(rest[25:29], "big", signed=True)
    p2 = int.from_bytes(rest[29:33], "big", signed=True)
    return act, tgt_t, tgt_i, p1, p2

print("=== All CharacterActions with act around team ===")
for idx, r in enumerate(rows):
    if (r.get("N3TypeName") or "") != "CharacterAction":
        continue
    p = parse_ca(r["RawHex"])
    if not p: continue
    act, tt, ti, p1, p2 = p
    if act in (0x15,0x18,0x1A,0x1B,0x1C,0x1D,0x1E,0x1F,0x20,0x21,0x22,0x23,0x24,0xA9,0x19):
        print(f"{idx} {r['Direction']} act=0x{act:X} tgt={tt:X}:{ti:X} p1={p1:#x} p2={p2}")

print("\n=== First join window 118-140 full ===")
for idx in range(118, 140):
    r = rows[idx]
    n3 = r.get("N3TypeName") or ""
    d = r.get("Direction")
    if n3 == "Stat":
        b = bytes.fromhex(r["RawHex"].strip())
        i = b.find(bytes.fromhex("2B333D6E"))
        body = b[i+4:]
        sid = int.from_bytes(body[13:17], "big")
        val = int.from_bytes(body[17:21], "big", signed=True)
        print(f"{idx} {d} Stat {sid}={val}")
    else:
        print(f"{idx} {d} {n3}")

print("\n=== Second join with AcceptTeamRequest 500-520 ===")
for idx in range(500, 520):
    r = rows[idx]
    n3 = r.get("N3TypeName") or ""
    d = r.get("Direction")
    if n3 == "Stat":
        b = bytes.fromhex(r["RawHex"].strip())
        i = b.find(bytes.fromhex("2B333D6E"))
        body = b[i+4:]
        sid = int.from_bytes(body[13:17], "big")
        val = int.from_bytes(body[17:21], "big", signed=True)
        print(f"{idx} {d} Stat {sid}={val}")
    elif n3:
        print(f"{idx} {d} {n3}")
        if n3 == "CharacterAction":
            print("   ", parse_ca(r["RawHex"]))
