# Focused Saltworm extract from 20260727-054719
from __future__ import print_function
import csv
import json
import re
import struct
from collections import Counter, defaultdict
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-054719")
out = Path(r"tools-temp/_tmp_worm_054719_deep.txt")
lines = []

def add(s=""):
    lines.append(s)

def tag4(v):
    try:
        v = int(v) & 0xFFFFFFFF
        b = struct.pack(">I", v)
        s = "".join(chr(x) if 32 <= x < 127 else "." for x in b)
        return "0x%08X '%s'" % (v, s)
    except Exception:
        return str(v)

WORM = "799D3908"

# --- dossier ---
for name in ("enemy-dossier.json", "enemy-state.json"):
    data = json.loads((cap / name).read_text(encoding="utf-8-sig"))
    add("=== %s ===" % name)
    text = json.dumps(data, indent=2)
    # find saltworm entries
    if isinstance(data, dict):
        for k, v in data.items():
            blob = json.dumps(v)
            if "Saltworm" in blob or WORM in blob or "17712" in blob:
                add("%s => %s" % (k, blob[:800]))
    elif isinstance(data, list):
        for e in data:
            blob = json.dumps(e)
            if "Saltworm" in blob or WORM in blob:
                add(blob[:1000])
    add()

# --- scfu appearance: find Saltworm name in raw hex ---
scfu = cap / "scfu-appearance.csv"
add("=== scfu rows containing Saltworm hex ===")
salt_hex = "53616C74776F726D"  # Saltworm
with scfu.open(encoding="utf-8-sig", errors="replace") as f:
    r = csv.DictReader(f)
    for row in r:
        raw = row.get("RawPacketHex") or ""
        if salt_hex in raw.upper() or "17712" in (row.get("MonsterData") or ""):
            add("utc=%s len=%s md=%s name=%s" % (
                row.get("CapturedUtc"), row.get("PacketLength"),
                row.get("MonsterData"), row.get("Name")))
            # decode name + look for ExtTex markers
            raw_b = bytes.fromhex(raw)
            # find ExtTex-ish: material id patterns after name
            idx = raw_b.find(b"Saltworm")
            add("  name@%d tail=%s" % (idx, raw_b[idx:idx+80].hex().upper() if idx>=0 else "?"))
            add("  fullhex=%s" % raw.upper())
add()

# --- events SCFU Saltworm ---
add("=== events Saltworm SCFU / ExtTex / Flags ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "Saltworm" in ln or (WORM in ln and ("SimpleCharFullUpdate" in ln or "ExtTex" in ln or "CHAR-SEEN" in ln or "Corpse" in ln)):
        add(ln[:700])
add()

# --- combat from worm only ---
add("=== worm AttackInfo / SAW / timing ===")
combat = cap / "enemy-combat.csv"
hits = []
saw = []
with combat.open(encoding="utf-8-sig", errors="replace") as f:
    r = csv.DictReader(f)
    for row in r:
        src = row.get("SourceIdentity") or ""
        tgt = row.get("TargetIdentity") or ""
        detail = row.get("Detail") or ""
        action = row.get("Action") or ""
        if WORM not in src and WORM not in tgt and WORM not in detail:
            continue
        if action in ("AttackInfo", "SpecialAttackWeapon", "SpecialAttackInfo", "MissedAttackInfo", "Death"):
            wi = row.get("Unknown5") or ""  # maybe not
            # parse WeaponInstance from detail
            m = re.search(r"WeaponInstance=(-?\d+)", detail)
            wi = m.group(1) if m else ""
            slots = re.search(r"WeaponSlot=(-?\d+)", detail)
            amt = row.get("Amount") or ""
            add("%s %s src=%s tgt=%s amt=%s slot=%s wi=%s" % (
                row.get("CapturedUtc"), action, src, tgt, amt,
                slots.group(1) if slots else "",
                tag4(wi) if wi else ""))
            if action == "AttackInfo" and WORM in src:
                hits.append(row)
            if action == "SpecialAttackWeapon" and WORM in src:
                saw.append(detail)
add("worm hit count=%d" % len(hits))
if hits:
    times = [row["CapturedUtc"] for row in hits]
    add("first=%s last=%s" % (times[0], times[-1]))
    # intervals
    from datetime import datetime
    def parse_t(s):
        return datetime.strptime(s[:26], "%Y-%m-%dT%H:%M:%S.%f")
    ts = [parse_t(t) for t in times]
    deltas = [(ts[i]-ts[i-1]).total_seconds() for i in range(1, len(ts))]
    add("deltas=%s" % [round(d,3) for d in deltas])
    add("avg_delta=%.3f" % (sum(deltas)/len(deltas) if deltas else 0))
    wis = Counter()
    slots = Counter()
    for row in hits:
        m = re.search(r"WeaponInstance=(-?\d+)", row.get("Detail") or "")
        if m:
            wis[tag4(m.group(1))] += 1
        m2 = re.search(r"WeaponSlot=(-?\d+)", row.get("Detail") or "")
        if m2:
            slots[m2.group(1)] += 1
        am = row.get("Amount")
        add("  hit amt=%s detail=%s" % (am, (row.get("Detail") or "")[:180]))
    add("wi counts=%s" % wis)
    add("slot counts=%s" % slots)
for d in saw:
    add("SAW %s" % d[:300])
add()

# --- loot full ---
add("=== saltworm loot full ===")
with (cap / "corpse-loot-observations.csv").open(encoding="utf-8-sig", errors="replace") as f:
    r = csv.DictReader(f)
    for row in r:
        if row.get("EnemyName") == "Saltworm" or "17712" in (row.get("MonsterData") or ""):
            add(json.dumps(row, indent=2)[:3000])
add()

# --- corpse CFU fields ---
add("=== saltworm corpse CFU ===")
with (cap / "corpse-full-updates.csv").open(encoding="utf-8-sig", errors="replace") as f:
    r = csv.DictReader(f)
    for row in r:
        if "Saltworm" in (row.get("CorpseName") or ""):
            for k, v in row.items():
                if v and v not in ("0", "False", ""):
                    add("  %s=%s" % (k, v[:300] if isinstance(v, str) else v))
add()

# --- XP / AIXP around worm death 03:48:12 ---
add("=== XP/AIXP around worm kill ===")
with (cap / "enemy-stat-updates.csv").open(encoding="utf-8-sig", errors="replace") as f:
    r = csv.DictReader(f)
    for row in r:
        st = row.get("Stat") or ""
        if st in ("XP", "AlienXP", "Level", "XPModifier"):
            add("%s %s id=%s role=%s val=%s" % (
                row.get("CapturedUtc"), st, row.get("Identity"),
                row.get("IdentityRole"), row.get("Value")))
add()

# system messages around kill
for ln in (cap / "system-messages.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "03:48:1" in ln or "AlienXP" in ln or "XP=" in ln or "xp" in ln.lower():
        add("SYS " + ln[:350])

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "n=", len(lines))
