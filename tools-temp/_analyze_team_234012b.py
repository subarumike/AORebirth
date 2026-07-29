from pathlib import Path
import csv
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-234012")
rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))

# Find TeamInvite by scanning events ordinals and raw for unknown types
print("=== packets around first team sequence (110-170) ===")
for idx in range(110, 170):
    r = rows[idx]
    name = r.get("N3TypeName") or "?"
    d = r.get("Direction")
    hx = (r.get("RawHex") or "").strip()
    extra = ""
    if name == "CharacterAction":
        b = bytes.fromhex(hx)
        i = b.find(bytes.fromhex("5E477770"))
        body = b[i+4:]
        act = int.from_bytes(body[9:13], "big")
        p1 = int.from_bytes(body[25:29], "big", signed=True)
        p2 = int.from_bytes(body[29:33], "big", signed=True)
        tgt = f"{int.from_bytes(body[17:21],'big'):X}:{int.from_bytes(body[21:25],'big'):X}"
        extra = f" act=0x{act:X} tgt={tgt} p1={p1} p2={p2}"
    elif name in ("TeamMember", "TeamMemberInfo"):
        b = bytes.fromhex(hx)
        # find member identity after type
        extra = f" hex={hx[40:120]}"
    elif name == "Stat":
        b = bytes.fromhex(hx)
        i = b.find(bytes.fromhex("2B333D6E"))
        body = b[i+4:]
        sid = int.from_bytes(body[13:17], "big")
        val = int.from_bytes(body[17:21], "big", signed=True)
        if sid in (6, 213, 521, 587):
            extra = f" stat={sid}={val}"
        else:
            continue
    print(f"{idx:3d} {d:3s} {name}{extra}")

print("\n=== Look for TeamInvite type value in events ===")
ev = (cap / "events.log").read_text(encoding="utf-8", errors="replace")
for line in ev.splitlines():
    if "TeamInvite" in line or "0x1C" in line or "Action=28" in line or "Action=Team" in line:
        print(line[:300])

print("\n=== ALL OUT packets ===")
for idx, r in enumerate(rows):
    if r.get("Direction") != "OUT":
        continue
    name = r.get("N3TypeName") or "?"
    hx = (r.get("RawHex") or "").strip()
    extra = ""
    if name == "CharacterAction":
        b = bytes.fromhex(hx)
        i = b.find(bytes.fromhex("5E477770"))
        body = b[i+4:]
        act = int.from_bytes(body[9:13], "big")
        p1 = int.from_bytes(body[25:29], "big", signed=True)
        p2 = int.from_bytes(body[29:33], "big", signed=True)
        tgt = f"{int.from_bytes(body[17:21],'big'):X}:{int.from_bytes(body[21:25],'big'):X}"
        extra = f" act=0x{act:X} tgt={tgt} p1={p1} p2={p2}"
    print(f"{idx:3d} OUT {name}{extra}")

# Decode TeamMember names in first join
print("\n=== TeamMember bodies first join ===")
for idx in (124, 126, 366, 369, 508, 511):
    r = rows[idx]
    b = bytes.fromhex(r["RawHex"].strip())
    # find 46312D2E
    i = b.find(bytes.fromhex("46312D2E"))
    body = b[i+4:]
    # identity + unk + member + team + unk4 + level + unk5 + namelen + name
    member = f"{int.from_bytes(body[9:13],'big'):X}:{int.from_bytes(body[13:17],'big'):X}"
    team = f"{int.from_bytes(body[17:21],'big'):X}:{int.from_bytes(body[21:25],'big'):X}"
    level = int.from_bytes(body[29:33], "big")
    unk5 = int.from_bytes(body[33:35], "big")
    nlen = int.from_bytes(body[35:39], "big")
    name = body[39:39+nlen].decode("utf-8", errors="replace")
    print(f"{idx} member={member} team={team} lvl={level} u5={unk5} name={name!r}")
