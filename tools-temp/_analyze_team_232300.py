from pathlib import Path
import csv

p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-232300/raw-packets.csv")
rows = list(csv.DictReader(p.open(encoding="utf-8-sig", newline="")))
TEAM = {0x15, 0x18, 0x1A, 0x20, 0x23, 0x16, 0x19}

print("=== ALL CharacterAction ===")
for idx, r in enumerate(rows):
    if (r.get("N3TypeName") or "") != "CharacterAction":
        continue
    hx = (r.get("RawHex") or "").strip()
    b = bytes.fromhex(hx)
    i = b.find(bytes.fromhex("5E477770"))
    if i < 0:
        print(idx, r.get("Direction"), "NO MAGIC")
        continue
    body = b[i + 4 :]
    ident_t = int.from_bytes(body[0:4], "big")
    ident_i = int.from_bytes(body[4:8], "big")
    unk = body[8]
    act = int.from_bytes(body[9:13], "big")
    unk1 = int.from_bytes(body[13:17], "big")
    tgt_t = int.from_bytes(body[17:21], "big")
    tgt_i = int.from_bytes(body[21:25], "big")
    p1 = int.from_bytes(body[25:29], "big", signed=True)
    p2 = int.from_bytes(body[29:33], "big", signed=True)
    u2 = int.from_bytes(body[33:35], "big", signed=True) if len(body) >= 35 else None
    mark = " *" if act in TEAM else ""
    print(
        f"{idx:3d} {r.get('Direction'):3s} act=0x{act:X}({act}) "
        f"id={ident_t:X}:{ident_i:X} unk={unk} unk1={unk1} "
        f"tgt={tgt_t:X}:{tgt_i:X} p1={p1} p2={p2} u2={u2}{mark}"
    )

print("=== TeamMember / TeamMemberInfo full body ===")
for idx, r in enumerate(rows):
    name = r.get("N3TypeName") or ""
    if name not in ("TeamMember", "TeamMemberInfo"):
        continue
    hx = (r.get("RawHex") or "").strip()
    b = bytes.fromhex(hx)
    i = b.find(bytes.fromhex("5E477770"))
    body = b[i + 4 :] if i >= 0 else b
    print(f"{idx:3d} {r.get('Direction')} {name} len={len(body)}")
    print("  hex:", body.hex())
    if name == "TeamMember" and len(body) >= 30:
        # try layout A: N3 id(8)+unk(1) + Member(8)+Team(8)+unk4(4)+level(4)+unk5(2)+namelen(4)+name
        off = 9
        mem_t = int.from_bytes(body[off : off + 4], "big")
        mem_i = int.from_bytes(body[off + 4 : off + 8], "big")
        team_t = int.from_bytes(body[off + 8 : off + 12], "big")
        team_i = int.from_bytes(body[off + 12 : off + 16], "big")
        unk4 = int.from_bytes(body[off + 16 : off + 20], "big", signed=True)
        level = int.from_bytes(body[off + 20 : off + 24], "big", signed=True)
        unk5 = int.from_bytes(body[off + 24 : off + 26], "big", signed=True)
        nlen = int.from_bytes(body[off + 26 : off + 30], "big", signed=True)
        name_s = body[off + 30 : off + 30 + max(0, nlen)].decode("utf-8", errors="replace")
        print(
            f"  layoutA member={mem_t:X}:{mem_i:X} team={team_t:X}:{team_i:X} "
            f"unk4={unk4} level={level} unk5={unk5} name={name_s!r}"
        )
        # layout B with extra Character(8)+pad(1) after N3
        off = 9 + 8 + 1
        if len(body) >= off + 30:
            mem_t = int.from_bytes(body[off : off + 4], "big")
            mem_i = int.from_bytes(body[off + 4 : off + 8], "big")
            team_t = int.from_bytes(body[off + 8 : off + 12], "big")
            team_i = int.from_bytes(body[off + 12 : off + 16], "big")
            unk4 = int.from_bytes(body[off + 16 : off + 20], "big", signed=True)
            level = int.from_bytes(body[off + 20 : off + 24], "big", signed=True)
            unk5 = int.from_bytes(body[off + 24 : off + 26], "big", signed=True)
            nlen = int.from_bytes(body[off + 26 : off + 30], "big", signed=True)
            name_s = body[off + 30 : off + 30 + max(0, min(nlen, 64))].decode(
                "utf-8", errors="replace"
            )
            print(
                f"  layoutB(+Char) member={mem_t:X}:{mem_i:X} team={team_t:X}:{team_i:X} "
                f"unk4={unk4} level={level} unk5={unk5} nlen={nlen} name={name_s!r}"
            )

print("=== Timeline team-related packet names ===")
for idx, r in enumerate(rows):
    name = r.get("N3TypeName") or ""
    if name in (
        "CharacterAction",
        "TeamMember",
        "TeamMemberInfo",
        "ChatText",
        "Feedback",
    ) or (name == "Stat" and False):
        print(f"{idx:3d} {r.get('Direction'):3s} {name}")

print("=== Stat around team packets (indices 0..end with team stats) ===")
# dump Stat packets that mention teamside/social/team if identifiable in hex
for idx, r in enumerate(rows):
    if (r.get("N3TypeName") or "") != "Stat":
        continue
    hx = (r.get("RawHex") or "").strip()
    b = bytes.fromhex(hx)
    i = b.find(bytes.fromhex("5E477770"))
    body = b[i + 4 :] if i >= 0 else b
    # crude: look for stat ids 6(team?), 213 teamside, 521 socialstatus as big-endian int
    interesting = False
    for sid in (6, 213, 521, 51):  # guess team/numberofteammembers
        if sid.to_bytes(4, "big") in body[9:]:
            interesting = True
            break
    if interesting or idx > 100:
        print(f"{idx:3d} {r.get('Direction')} Stat body={body[:48].hex()}")
