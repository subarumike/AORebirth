# Compare in-range same-lvl60 team+leave captures (both clients)
from pathlib import Path
import csv
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

caps = [
    Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260729-173311"),
    Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260729-173411"),
]

CA_MARK = bytes.fromhex("5E477770")

def decode_ca(hexstr):
    b = bytes.fromhex(hexstr.strip())
    i = b.find(CA_MARK)
    if i < 0:
        return None
    rest = b[i+4:]
    if len(rest) < 25:
        return None
    act = int.from_bytes(rest[9:13], "big")
    p1 = int.from_bytes(rest[13:17], "big")
    tt = int.from_bytes(rest[17:21], "big")
    ti = int.from_bytes(rest[21:25], "big")
    p2 = int.from_bytes(rest[25:29], "big") if len(rest) >= 29 else 0
    return act, p1, tt, ti, p2

ACT = {
    0x1A: "TeamRequestInvite",
    0xA9: "TeamInviteAck",
    0x1C: "ClientTeamInviteReply?",
    0x15: "TeamRequestReply/Accept?",
    0x69: "InfoRequest",
    0x62: "Other62",
}

TEAMISH = ("Team", "Social", "Invite", "Stat", "Info", "LookAt", "CharacterAction")

for cap in caps:
    print("=" * 72)
    print(cap.name)
    info = cap / "capture_info.json"
    if info.exists():
        import json
        j = json.loads(info.read_text(encoding="utf-8-sig"))
        print(f"  char={j.get('characterName')} pf={j.get('playfieldId')} outs={j.get('packetCounts',{}).get('outboundRaw')} ins={j.get('packetCounts',{}).get('inboundRaw')}")

    rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))
    print(f"  rows={len(rows)}")

    print("\n  --- ALL OUT ---")
    for idx, r in enumerate(rows):
        if r.get("Direction") != "OUT":
            continue
        name = r.get("N3TypeName") or ""
        extra = ""
        if name == "CharacterAction":
            d = decode_ca(r["RawHex"])
            if d:
                act, p1, tt, ti, p2 = d
                extra = f" {ACT.get(act, hex(act))} act={act:#x} p1={p1} p2={p2} tgt={tt:X}:{ti:X}"
        print(f"  #{idx:03d} OUT {name}{extra} t={r.get('CapturedUtc','')}")

    print("\n  --- CharacterAction team-relevant (both dirs) ---")
    for idx, r in enumerate(rows):
        if (r.get("N3TypeName") or "") != "CharacterAction":
            continue
        d = decode_ca(r["RawHex"])
        if not d:
            continue
        act, p1, tt, ti, p2 = d
        if act not in (0x1A, 0xA9, 0x1C, 0x15, 0x69, 0x14, 0x23, 0xA8, 168, 169, 21, 28) and act not in ACT:
            # still print if action name known-ish
            if act not in (0x1A, 0xA9, 0x1C, 0x15, 0x14, 0xA8, 0x23):
                continue
        print(f"  #{idx:03d} {r.get('Direction'):3} {ACT.get(act, hex(act))} act={act:#x} p1={p1} p2={p2} tgt={tt:X}:{ti}({ti})")

    # events team lines
    print("\n  --- events team/invite/leave ---")
    ev = cap / "events.log"
    if ev.exists():
        for line in ev.read_text(encoding="utf-8", errors="replace").splitlines():
            low = line.lower()
            if any(x in low for x in ("team", "invite", "leave", "socialstatus", "0x1a", "0x1c", "accept")):
                if "enemy" in low and "team" not in low:
                    continue
                print(" ", line[:260])

    # TeamMember / TeamMemberInfo / TeamMemberLeft message types
    print("\n  --- N3 types containing Team/Social ---")
    for idx, r in enumerate(rows):
        name = r.get("N3TypeName") or ""
        if "Team" in name or "Social" in name or name in ("Stat",) and False:
            print(f"  #{idx:03d} {r.get('Direction'):3} {name}")
        elif "Team" in name or name.endswith("TeamMember") or "Invite" in name:
            print(f"  #{idx:03d} {r.get('Direction'):3} {name}")
