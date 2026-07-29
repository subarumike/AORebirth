# Decode Karli path + check dialogue completeness in 20260727-055715
from __future__ import print_function
import re
import struct
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-055715")
out = Path(r"tools-temp/_tmp_karli_path_decode.txt")
lines = []
KARLI = "799AD394"

def add(s=""):
    lines.append(s)

# Decode SCFU waypoint blob from known hex
scfu_tail = bytes.fromhex(
    "000003C8B500000000000000030004185100000000000000040004185000000000000017A600000418480003C4A0020000"
)
add("=== SCFU after textures/tail ===")
add(scfu_tail.hex().upper())
# try parse as waypoint list: count then xyz floats
for off in range(0, min(len(scfu_tail), 80)):
    try:
        # big-endian float triples
        pts = []
        i = off
        while i + 12 <= len(scfu_tail):
            x, y, z = struct.unpack_from(">fff", scfu_tail, i)
            if abs(x) < 200 and abs(z) < 200 and abs(y) < 50 and (abs(x)+abs(z)) > 1:
                pts.append((round(x,3), round(y,3), round(z,3)))
            i += 12
        if len(pts) >= 2:
            add("be_off=%d pts=%s" % (off, pts[:20]))
    except Exception:
        pass
    try:
        pts = []
        i = off
        while i + 12 <= len(scfu_tail):
            x, y, z = struct.unpack_from("<fff", scfu_tail, i)
            if abs(x) < 200 and abs(z) < 200 and abs(y) < 50 and (abs(x)+abs(z)) > 1:
                pts.append((round(x,3), round(y,3), round(z,3)))
            i += 12
        if len(pts) >= 2:
            add("le_off=%d pts=%s" % (off, pts[:20]))
    except Exception:
        pass

# Full SCFU hex from packets
add()
add("=== Full Karli SCFU parse ===")
name_hex = "4B61726C692043617070656C6C657269"
for ln in (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if name_hex.upper() not in ln.upper():
        continue
    m = re.search(r"hex=([0-9A-Fa-f]+)", ln)
    if not m:
        continue
    raw = bytes.fromhex(m.group(1))
    # skip header to position - find name
    idx = raw.find(b"Karli Cappelleri")
    add("raw_len=%d name_at=%d" % (len(raw), idx))
    # dump all plausible xyz near end (after name)
    body = raw[idx + len("Karli Cappelleri") + 1:]
    add("post_name_hex=%s" % body.hex().upper())
    # known SCFU waypoint encoding in AO often: count (ushort/int) then repeated (x,y,z) floats BE
    # also dump every float in post_name
    floats = []
    for i in range(0, len(body) - 3, 4):
        v = struct.unpack_from(">f", body, i)[0]
        if v == v and abs(v) < 5000:
            floats.append((i, round(v, 4)))
    add("be_floats=%s" % floats)
    # Look for waypoint section: after textures marker often 0x0000 then coords
    # From hex end: 03C8B5 ... 041851 ... 041850 ... 041848 03C4A0
    # Those look like identity-ish ints not floats. Try decode PathInfo FollowTarget instead.
    break

# FollowTarget hex for Karli
add()
add("=== FollowTarget NpcPath hex for Karli ===")
n = 0
pts_all = []
for ln in (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "FollowTarget" not in ln and "0x2714" not in ln.lower():
        # FollowTarget N3 type - search by identity
        if KARLI.upper() not in ln.upper():
            continue
        if "Follow" not in ln and "2714" not in ln and "follow" not in ln.lower():
            # still might be hex with identity only
            if "799AD394" not in ln.upper():
                continue
    if KARLI.upper() not in ln.upper():
        continue
    m = re.search(r"hex=([0-9A-Fa-f]+)", ln)
    if not m:
        continue
    raw = bytes.fromhex(m.group(1))
    # FollowTarget identity is SimpleChar:799AD394 = C0 00 00 00 79 9A D3 94 typically
    if b"\x79\x9A\xD3\x94" not in raw and b"\x94\xD3\x9A\x79" not in raw:
        # identity may be in header differently
        if "799AD394" not in ln.upper():
            continue
    n += 1
    add("ft#%d len=%d hex=%s" % (n, len(raw), raw.hex().upper()))
    # PathInfo typically has dest xyz floats
    for endian in (">", "<"):
        for off in range(0, min(len(raw), 40)):
            if off + 12 > len(raw):
                break
            x, y, z = struct.unpack_from(endian + "fff", raw, off)
            if 0 < abs(x) < 200 and abs(y) < 30 and 0 < abs(z) < 200:
                pts_all.append((round(x,3), round(y,3), round(z,3), endian, off, n))
    if n >= 20:
        break
add("follow_pkts=%d" % n)
# unique dests
uniq = []
seen = set()
for t in pts_all:
    key = (t[0], t[1], t[2])
    if key in seen:
        continue
    seen.add(key)
    uniq.append(t)
add("unique_dests=%s" % uniq[:50])

# Also parse enemy-movement RawPacketHex if present
add()
add("=== enemy-movement columns + sample ===")
import csv
with (cap / "enemy-movement.csv").open(encoding="utf-8-sig") as f:
    r = csv.DictReader(f)
    add("cols=%s" % r.fieldnames)
    for row in r:
        if KARLI not in (row.get("Identity") or "") and KARLI not in str(row.values()):
            continue
        add(str({k: row.get(k) for k in (r.fieldnames or []) if row.get(k)}))
        break

# Dialogue completeness check
add()
add("=== dialogue completeness ===")
for path in ("npc-interactions.log", "chat-dialogue.log", "knubot.log", "quest.log"):
    p = cap / path
    add("%s exists=%s size=%s" % (path, p.exists(), p.stat().st_size if p.exists() else 0))

# Count knubot types in events after 04:06
knu = 0
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "04:0" not in ln and "04:1" not in ln:
        continue
    if "Knu" in ln or "AppendText" in ln or "AnswerList" in ln:
        knu += 1
        add("knu: " + ln[:400])
add("knubot_lines_after_0406=%d" % knu)

# Search entire capture for XP buff / NCU related strings
add()
add("=== XP buff / NCU string hits ===")
patterns = ("NCU", "Experience", "XP Bonus", "team", "buff", "Karli", "Cappelleri", "291082", "297274")
for path in sorted(cap.glob("*")):
    if path.suffix.lower() not in (".log", ".csv", ".txt", ".json"):
        continue
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except Exception:
        continue
    for pat in patterns:
        if pat in text and pat in ("NCU", "XP Bonus", "Experience Bonus", "291082", "Karli"):
            # count
            c = text.count(pat)
            if c and pat in ("NCU", "XP Bonus", "291082"):
                add("%s count(%s)=%d" % (path.name, pat, c))

# Item names from inventory for 291082
add()
add("=== item 291082 / template around XP ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "291082" in ln or "297274" in ln or "NCU" in ln:
        add(ln[:500])

# XP deltas by kill type
add()
add("=== XP deltas by kill ===")
kills = [
    ("03:57:55", "Specialist", 50882, 750),
    ("03:59:45", "Specialist", 50944, 900),
    ("04:03:24", "Scout", 50999, 1050),
    ("04:03:47", "Scout", 51047, 1200),
    ("04:04:04", "Scout", 51095, 1350),
    ("04:04:37", "Specialist+levelup", 51150, 0),
    ("04:05:11", "Specialist", 51205, 485),
    ("04:05:41", "Specialist", 51246, 843),
    ("04:06:24", "Specialist", 51308, 1399),
]
prev_xp = None
prev_aixp = None
for t, name, xp, aixp in kills:
    dx = None if prev_xp is None else xp - prev_xp
    da = None if prev_aixp is None else (aixp - prev_aixp if aixp >= prev_aixp or prev_aixp == 0 else "level+"+str(aixp))
    add("%s %s XP=%d dXP=%s AIXP=%s dAIXP=%s" % (t, name, xp, dx, aixp, da))
    prev_xp, prev_aixp = xp, aixp

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "lines", len(lines))
