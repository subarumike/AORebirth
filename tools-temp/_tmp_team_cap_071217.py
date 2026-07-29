from pathlib import Path
import csv

p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-071217/raw-packets.csv")
with p.open(encoding="utf-8-sig", newline="") as f:
    rows = list(csv.DictReader(f))

for r in rows:
    name = r.get("N3TypeName") or ""
    if name not in ("CharacterAction", "TeamMember", "TeamMemberInfo"):
        continue
    hx = (r.get("RawHex") or "").strip()
    direction = r.get("Direction")
    if not hx:
        continue
    b = bytes.fromhex(hx)
    if name == "CharacterAction":
        i = b.find(bytes.fromhex("5E477770"))
        if i < 0:
            continue
        body = b[i + 4 :]
        # Identity(8) Action(4) Unknown1(4) Target(8) Parameter1(4) Parameter2(4) Unknown2(4)
        act = int.from_bytes(body[8:12], "big")
        unk1 = int.from_bytes(body[12:16], "big")
        tgt_t = int.from_bytes(body[16:20], "big")
        tgt_i = int.from_bytes(body[20:24], "big")
        p1 = int.from_bytes(body[24:28], "big", signed=True)
        p2 = int.from_bytes(body[28:32], "big", signed=True)
        print(
            f"{direction} CA act=0x{act:X} unk1={unk1} tgt={tgt_t:X}:{tgt_i:X} p1={p1} p2={p2}"
        )
    elif name == "TeamMember":
        i = b.find(bytes.fromhex("46312D2E"))
        body = b[i + 4 :]
        print(f"{direction} TeamMember body={body.hex()}")
    else:
        i = b.find(bytes.fromhex("28784248"))
        body = b[i + 4 :]
        print(f"{direction} TeamMemberInfo body={body.hex()}")
