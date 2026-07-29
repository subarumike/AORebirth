from pathlib import Path
import csv
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-232300/raw-packets.csv")
rows = list(csv.DictReader(p.open(encoding="utf-8-sig", newline="")))


def find_n3(b: bytes):
    # common N3 markers in this capture pipeline
    for magic in ("5E477770", "F6312D2E", "3B2545A8"):
        m = bytes.fromhex(magic)
        i = b.find(m)
        if i >= 0:
            return magic, i, b[i + 4 :]
    return None, -1, b


print("=== TeamMember / Info with markers ===")
for idx, r in enumerate(rows):
    name = r.get("N3TypeName") or ""
    if name not in ("TeamMember", "TeamMemberInfo"):
        continue
    hx = (r.get("RawHex") or "").strip()
    b = bytes.fromhex(hx)
    print(f"\n{idx} {r.get('Direction')} {name} rawlen={len(b)}")
    print(" raw:", hx)
    # search for C350 identity of capturer
    key = bytes.fromhex("0000C350765A6D34")
    positions = []
    start = 0
    while True:
        j = b.find(key)
        if j < 0:
            # also without leading zeros variant
            break
        positions.append(j)
        b2 = b
        break
    # find all C350 occurrences
    needle = bytes.fromhex("0000C350")
    pos = 0
    while True:
        j = b.find(needle, pos)
        if j < 0:
            break
        inst = int.from_bytes(b[j + 4 : j + 8], "big")
        print(f"  C350@{j} inst={inst:X}")
        pos = j + 1
    # DEA9 team window
    pos = 0
    needle = bytes.fromhex("0000DEA9")
    while True:
        j = b.find(needle, pos)
        if j < 0:
            break
        inst = int.from_bytes(b[j + 4 : j + 8], "big")
        print(f"  DEA9@{j} inst={inst:X}")
        pos = j + 1
    # ASCII names
    for s in (b"Engynera", b"engynera"):
        j = b.find(s)
        if j >= 0:
            print(f"  name@{j} {s!r} context={b[j-8:j+20].hex()}")

print("\n=== Full chronological interesting ===")
for idx, r in enumerate(rows):
    name = r.get("N3TypeName") or ""
    if name not in ("CharacterAction", "TeamMember", "TeamMemberInfo", "Feedback", "Stat"):
        continue
    if name == "Stat":
        hx = (r.get("RawHex") or "").strip()
        b = bytes.fromhex(hx)
        # team=6? teamside=213=0xD5 social=521=0x209 numberofteammembers?
        hit = None
        for sid, label in ((213, "teamside"), (521, "socialstatus"), (6, "team?"), (51, "num?")):
            if sid.to_bytes(2, "big") in b or sid.to_bytes(4, "big") in b:
                hit = label
                break
        if not hit:
            continue
        print(f"{idx:3d} {r.get('Direction'):3s} Stat ~{hit}")
        continue
    if name == "CharacterAction":
        hx = (r.get("RawHex") or "").strip()
        b = bytes.fromhex(hx)
        i = b.find(bytes.fromhex("5E477770"))
        body = b[i + 4 :] if i >= 0 else b
        act = int.from_bytes(body[9:13], "big")
        p1 = int.from_bytes(body[25:29], "big", signed=True)
        p2 = int.from_bytes(body[29:33], "big", signed=True)
        tgt = f"{int.from_bytes(body[17:21],'big'):X}:{int.from_bytes(body[21:25],'big'):X}"
        print(f"{idx:3d} {r.get('Direction'):3s} CA act=0x{act:X} tgt={tgt} p1={p1} p2={p2}")
    else:
        print(f"{idx:3d} {r.get('Direction'):3s} {name}")
