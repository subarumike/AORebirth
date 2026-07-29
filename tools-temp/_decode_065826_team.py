import csv
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-065826")
info = (cap / "capture_info.json").read_text(encoding="utf-8-sig", errors="replace")
print("==== info ====")
print(info[:1500])

rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))
print("rows", len(rows))

def ca(raw):
    b = bytes.fromhex(raw.replace(" ", ""))
    i = b.find(bytes.fromhex("5E477770"))
    if i < 0 or len(b) < i + 37:
        return None
    rest = b[i + 4 :]
    a = int.from_bytes(rest[9:13], "big")
    tt = int.from_bytes(rest[17:21], "big")
    ti = int.from_bytes(rest[21:25], "big")
    p1 = int.from_bytes(rest[25:29], "big")
    p2 = int.from_bytes(rest[29:33], "big")
    # signed p2
    if p2 >= 0x80000000:
        p2s = p2 - 0x100000000
    else:
        p2s = p2
    return a, tt, ti, p1, p2s

print("==== team-related ====")
for i, r in enumerate(rows):
    n = r.get("N3TypeName") or ""
    d = r.get("Direction") or ""
    extra = ""
    if n == "CharacterAction" and r.get("RawHex"):
        dec = ca(r["RawHex"])
        if not dec:
            continue
        a, tt, ti, p1, p2 = dec
        if a not in (0x1A, 0x1C, 0x15, 0x18, 0x20, 0x23, 0x69):
            continue
        extra = " act=0x%X tgt=%X:%X p1=%d p2=%d" % (a, tt, ti, p1, p2)
    elif n not in ("TeamInvite", "TeamMember", "TeamMemberInfo", "TeamMemberLeave"):
        continue
    print("#%d %s %s%s el=%s" % (i, d, n, extra, r.get("ElapsedMilliseconds")))
