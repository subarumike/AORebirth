# Saltworm fight anims + XP delta + corpse textures + living SCFU from hexlog
from __future__ import print_function
import re
import struct
from collections import Counter
from datetime import datetime
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-054719")
out = Path(r"tools-temp/_tmp_worm_054719_fight.txt")
lines = []
WORM = "799D3908"

def add(s=""):
    lines.append(s)

def tag4(v):
    v = int(v) & 0xFFFFFFFF
    b = struct.pack(">I", v)
    s = "".join(chr(x) if 32 <= x < 127 else "." for x in b)
    return "0x%08X/%d/'%s'" % (v, v, s)

# Fight from events.log AttackInfo where identity is worm
add("=== worm outgoing AttackInfo from events ===")
hits = []
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "AttackInfo" not in ln:
        continue
    if "identity=(SimpleChar:%s)" % WORM not in ln and "Identity=(SimpleChar:%s)" % WORM not in ln:
        # also IN-N3-DETAIL form
        if WORM not in ln:
            continue
        # only if worm is the attacker identity at start of AttackInfoMessage owner
        if "AttackInfoMessage" not in ln:
            continue
    if "AttackInfoMessage" in ln and ("Identity=(SimpleChar:%s)" % WORM) in ln:
        m_amt = re.search(r"Amount=(-?\d+)", ln)
        m_slot = re.search(r"WeaponSlot=(-?\d+)", ln)
        m_wi = re.search(r"WeaponInstance=(-?\d+)", ln)
        m_hit = re.search(r"HitType=(\w+)", ln)
        m_tgt = re.search(r"Target=\(SimpleChar:([0-9A-Fa-f]+)\)", ln)
        ts = ln.split("Z", 1)[0] + "Z"
        hits.append({
            "ts": ts,
            "amt": int(m_amt.group(1)) if m_amt else None,
            "slot": int(m_slot.group(1)) if m_slot else None,
            "wi": int(m_wi.group(1)) if m_wi else None,
            "hit": m_hit.group(1) if m_hit else None,
            "tgt": m_tgt.group(1) if m_tgt else None,
        })
        add("%s amt=%s slot=%s wi=%s hit=%s tgt=%s" % (
            ts, hits[-1]["amt"], hits[-1]["slot"],
            tag4(hits[-1]["wi"]) if hits[-1]["wi"] is not None else None,
            hits[-1]["hit"], hits[-1]["tgt"]))

add("hit_count=%d" % len(hits))
if hits:
    def parse_t(s):
        return datetime.strptime(s[:26], "%Y-%m-%dT%H:%M:%S.%f")
    ts = [parse_t(h["ts"]) for h in hits]
    deltas = [(ts[i] - ts[i - 1]).total_seconds() for i in range(1, len(ts))]
    add("deltas=%s" % [round(d, 3) for d in deltas])
    add("avg=%.3f min=%.3f max=%.3f" % (
        sum(deltas) / len(deltas), min(deltas), max(deltas)))
    add("wi=%s" % Counter(tag4(h["wi"]) for h in hits if h["wi"] is not None))
    add("slot=%s" % Counter(h["slot"] for h in hits))
    add("amts=%s" % [h["amt"] for h in hits])

# SAW for worm
add()
add("=== SAW worm ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "SpecialAttackWeapon" in ln and WORM in ln and "DETAIL" in ln:
        add(ln[:500])

# XP before/after
add()
add("=== all player XP stats ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "XP=" in ln and ("7996C028" in ln or "local" in ln.lower() or "StatMessage" in ln):
        if "AlienXP" in ln or re.search(r"\[XP=\d+\]", ln) or "Stats=count=1[XP=" in ln:
            add(ln[:300])

# Corpse textures from RawHex
add()
add("=== corpse textures from RawHex ===")
# From corpse-full-updates RawHex field - parse Texture entries
# Look in events for Textures= detail
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "Remains of Saltworm" in ln and "Textures" in ln:
        add(ln)
        # try extract texture ids if present as Id=
        for m in re.finditer(r"Texture[^\]]*|Id=(\d+)|Place=(\d+)", ln):
            pass
# decode from raw hex in csv
import csv
with (cap / "corpse-full-updates.csv").open(encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        if "Saltworm" not in (row.get("CorpseName") or ""):
            continue
        raw = bytes.fromhex(row["RawHex"])
        add("raw_len=%d catmesh_field=%s scale=%s" % (
            len(raw), row.get("CorpseCatMesh"), row.get("MonsterScale")))
        # search for known texture ids as BE ints near end
        # dump ascii and ints that look like texture ids (90000-300000)
        cands = []
        for i in range(len(raw) - 4):
            v = struct.unpack_from(">I", raw, i)[0]
            if 20000 <= v <= 400000:
                cands.append((i, v))
        add("int_cands=%s" % cands[:40])
        # After name Remains of Saltworm
        idx = raw.find(b"Remains of Saltworm")
        add("after_name=%s" % raw[idx:idx+120].hex().upper() if idx >= 0 else "?")

# Living SCFU in packets.hex.log
add()
add("=== packets.hex Saltworm SCFU ===")
salt_hex = "53616C74776F726D"
n = 0
for ln in (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if salt_hex.upper() not in ln.upper():
        continue
    if "SimpleCharFullUpdate" not in ln and "n3=SimpleCharFullUpdate" not in ln and "SCFU" not in ln:
        # still keep if hex has saltworm
        pass
    m = re.search(r"hex=([0-9A-Fa-f]+)", ln)
    if not m:
        continue
    raw = bytes.fromhex(m.group(1))
    if b"Saltworm" not in raw:
        continue
    n += 1
    add("pkt#%d len=%d head=%s" % (n, len(raw), raw[:32].hex().upper()))
    idx = raw.find(b"Saltworm")
    add("  around_name=%s" % raw[idx:idx+64].hex().upper())
    # ExtTex often after name null-terminated padding
    # look for 00 00 00 03 style material count
    if n >= 3:
        break
add("scfu_count=%d" % n)

# Compare existing loot
add()
add("=== loot items decoded ===")
items = "85748:85747:13:1;85678:27398:13:1;125043:125044:13:1;160242:160243:13:1;161524:161525:13:1;162497:162497:14:1;201164:201165:13:1"
for part in items.split(";"):
    a, b, ql, c = part.split(":")
    add("low=%s high=%s ql=%s count=%s" % (a, b, ql, c))

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "hits", len(hits), "scfu", n)
