from pathlib import Path
import csv
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-234012")
rows = list(csv.DictReader((cap/"raw-packets.csv").open(encoding="utf-8-sig", newline="")))

def parse_stat(hx):
    b = bytes.fromhex(hx.strip())
    i = b.find(bytes.fromhex("2B333D6E"))
    if i < 0:
        return None
    body = b[i+4:]
    sid = int.from_bytes(body[13:17], "big")
    val = int.from_bytes(body[17:21], "big", signed=True)
    return sid, val

def parse_ca(hx):
    b = bytes.fromhex(hx.strip())
    i = b.find(bytes.fromhex("5E477770"))
    if i < 0:
        return None
    rest = b[i+4:]
    # identity 8 + unk 1
    unk = rest[8]
    act = int.from_bytes(rest[9:13], "big")
    unk1 = int.from_bytes(rest[13:17], "big")
    tgt_t = int.from_bytes(rest[17:21], "big")
    tgt_i = int.from_bytes(rest[21:25], "big")
    p1 = int.from_bytes(rest[25:29], "big", signed=True)
    p2 = int.from_bytes(rest[29:33], "big", signed=True)
    return act, tgt_t, tgt_i, p1, p2

def parse_tm(hx):
    b = bytes.fromhex(hx.strip())
    i = b.find(bytes.fromhex("0C1E213E"))  # TeamMember?
    # try common magics
    for mag in ("0C1E213E", "5465616D", "3433312A"):
        j = b.find(bytes.fromhex(mag))
        if j >= 0:
            body = b[j+4:]
            return mag, body.hex()
    # fallback: after N3 type in header
    i = b.find(bytes.fromhex("765A6D34"))
    # find after second identity area - dump
    return None, hx[:120]

print("=== AcceptTeamRequest / TeamMemberLeft / TeamKick / Leave actions ===")
for idx, r in enumerate(rows):
    if (r.get("N3TypeName") or "") != "CharacterAction":
        continue
    p = parse_ca(r["RawHex"])
    if not p:
        continue
    act, tt, ti, p1, p2 = p
    # AcceptTeamRequest often 0x23, Leave 0x18, MemberLeft 0x20, TransferLeader?
    if act in (0x23, 0x18, 0x20, 0x1B, 0x19, 0x1D, 0x1E, 0x1F, 0x21, 0x22, 0x24, 0xA9, 0x15):
        print(f"{idx} {r['Direction']} act=0x{act:X} tgt={tt:X}:{ti:X} p1={p1}({p1:#x}) p2={p2}({p2:#x})")

print("\n=== Stat team(6)/teamside(213)/social(521)/members(587)/level(54) around joins ===")
interesting = {6, 213, 521, 587, 54, 33}
for idx, r in enumerate(rows):
    if (r.get("N3TypeName") or "") != "Stat":
        continue
    p = parse_stat(r["RawHex"])
    if not p:
        continue
    sid, val = p
    if sid in interesting:
        print(f"{idx} {r['Direction']} Stat {sid}={val} ({val:#x})")

print("\n=== TeamMember packets ===")
for idx, r in enumerate(rows):
    if (r.get("N3TypeName") or "") != "TeamMember":
        continue
    hx = r["RawHex"].strip()
    b = bytes.fromhex(hx)
    # Find N3 TeamMember type - search known from handler comments
    # Dump body after identity
    i = b.find(bytes.fromhex("765A6D34"))
    print(f"{idx} {r['Direction']} len={len(b)} hex={hx}")
