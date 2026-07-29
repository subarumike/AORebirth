# Analyze 20260725-151009: combat range, finish, PAF/map, SCFU vs doors
import csv
import collections
import json
import os
import struct

CAP = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-151009"
OUT = r"tools-temp/_tmp_cap_151009_brief.txt"


def be_i(h, off):
    return struct.unpack(">i", bytes.fromhex(h[off * 2 : (off + 4) * 2]))[0]


def be_f(h, off):
    return struct.unpack(">f", bytes.fromhex(h[off * 2 : (off + 4) * 2]))[0]


lines = []


def p(s=""):
    lines.append(s)


p("=== enemy-combat (sample) ===")
combat_path = os.path.join(CAP, "enemy-combat.csv")
if os.path.exists(combat_path):
    with open(combat_path, encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f))
    p("rows=%d" % len(rows))
    cols = list(rows[0].keys()) if rows else []
    p("cols=%s" % cols[:20])
    for r in rows[:20]:
        p("  " + " | ".join("%s=%s" % (k, (r.get(k) or "")[:50]) for k in cols[:10]))

p("\n=== fight-events head ===")
fe = os.path.join(CAP, "enemy-fight-events.log")
if os.path.exists(fe):
    with open(fe, encoding="utf-8", errors="replace") as f:
        for i, line in enumerate(f):
            if i > 50:
                break
            p(line.rstrip()[:220])

p("\n=== mission-flow ===")
mf = os.path.join(CAP, "mission-flow.log")
if os.path.exists(mf):
    with open(mf, encoding="utf-8", errors="replace") as f:
        for line in f:
            p(line.rstrip()[:240])

counts = collections.Counter()
paf = []
attacks = []
saw = []
finish = []
door_early = []
scfu_pos = []

with open(os.path.join(CAP, "raw-packets.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        nt = row.get("N3TypeName") or ""
        d = row.get("Direction") or ""
        ts = row.get("Timestamp") or ""
        detail = row.get("Detail") or ""
        hx = (row.get("RawHex") or "").replace(" ", "").upper()
        if d.startswith("IN"):
            counts[nt] += 1
        if nt == "PlayfieldAnarchyF" and d.startswith("IN"):
            paf.append((ts, hx, detail))
        if nt == "SpecialAttackWeapon" and d.startswith("IN"):
            saw.append((ts, detail, hx[:140]))
        if nt in ("Attack", "AttackInfo", "WeaponItemFullUpdate") and d.startswith("IN"):
            attacks.append((ts, nt, detail, hx[:180]))
        if nt in ("FormatFeedback", "Quest", "CreateItem", "Feedback", "Stat", "TemplateAction") and d.startswith("IN"):
            finish.append((ts, nt, detail[:220]))
        if "Door" in nt and d.startswith("IN"):
            door_early.append((ts, nt, len(hx) // 2, detail[:120]))
        if nt == "SimpleCharFullUpdate" and d.startswith("IN") and len(hx) > 120:
            # position floats often around offset 45 in Corpse/SCFU-like frames; try common SCFU layout
            try:
                # Many SCFUs: after header, identity at ~12-16, pos later — use decoded detail if present
                scfu_pos.append((ts, detail[:200], len(hx) // 2))
            except Exception:
                pass

p("\n=== top IN types ===")
for k, v in counts.most_common(30):
    p("  %s: %d" % (k, v))

p("\n=== PAF (%d) ===" % len(paf))
for ts, hx, det in paf[:2]:
    p("ts=%s bytes=%d det=%s" % (ts, len(hx) // 2, (det or "")[:200]))
    bidx = hx.find("0000C79F")
    p("  C79F@%d building=%s" % (bidx // 2 if bidx >= 0 else -1, hx[bidx + 8 : bidx + 16] if bidx >= 0 else "?"))
    idx = hx.find("00009C50")
    if idx < 0:
        idx = hx.find("0000C350")
    p("  pf2marker@%d slice=%s" % (idx // 2 if idx >= 0 else -1, hx[idx : idx + 96] if idx >= 0 else "?"))
    # PlayfieldX/Z near start of message body — dump int words
    words = []
    for i in range(16, 48):
        try:
            words.append("%d=%d" % (i, be_i(hx, i)))
        except Exception:
            break
    p("  i16-47: %s" % " ".join(words))

p("\n=== SAW (%d) ===" % len(saw))
for ts, det, hx in saw[:12]:
    p("%s %s" % (ts, (det or "")[:200]))
    p("  hx=%s" % hx)

p("\n=== Attack/AttackInfo/WIFU (%d) ===" % len(attacks))
ai_weapon_slots = []
for ts, nt, det, hx in attacks:
    p("%s %s %s" % (ts, nt, (det or "")[:180]))
    if nt == "AttackInfo" and len(hx) >= 80:
        # dump trailing ints — weapon slot often in AttackInfo body
        try:
            body = bytes.fromhex(hx)
            ints = [struct.unpack_from(">i", body, i)[0] for i in range(0, min(len(body) - 3, 64), 4)]
            p("  ints=%s" % ints)
        except Exception as ex:
            p("  parseerr %s" % ex)

p("\n=== finish-like (%d) ===" % len(finish))
for ts, nt, det in finish[:50]:
    low = (det or "").lower()
    if nt in ("FormatFeedback", "Quest", "CreateItem", "Feedback", "TemplateAction") or "reward" in low or "xp" in low or "credit" in low or "quest" in low:
        p("%s %s %s" % (ts, nt, (det or "")[:200]))

p("\n=== Door IN first 20 ===")
for row in door_early[:20]:
    p("  %s %s len=%d %s" % row)

p("\n=== SCFU IN first 25 (detail) ===")
for ts, det, ln in scfu_pos[:25]:
    p("  %s len=%d %s" % (ts, ln, det))

p("\n=== enemy-dossier names ===")
ed = os.path.join(CAP, "enemy-dossier.json")
if os.path.exists(ed):
    data = json.load(open(ed, encoding="utf-8-sig"))
    if isinstance(data, list):
        items = data
    elif isinstance(data, dict):
        items = data.get("enemies") or data.get("Enemies") or data.get("npcs") or []
        if isinstance(items, dict):
            items = list(items.values())
        if not items:
            # flat dict of id->enemy
            items = [v for v in data.values() if isinstance(v, dict) and ("name" in v or "Name" in v)]
    else:
        items = []
    for e in items[:40]:
        name = e.get("name") or e.get("Name") or "?"
        md = e.get("monsterData") or e.get("MonsterData") or e.get("md")
        lvl = e.get("level") or e.get("Level")
        p("  name=%s md=%s lvl=%s" % (name, md, lvl))

p("\n=== scfu-appearance sample ===")
sap = os.path.join(CAP, "scfu-appearance.csv")
if os.path.exists(sap):
    with open(sap, encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f))
    p("rows=%d" % len(rows))
    if rows:
        p("cols=%s" % list(rows[0].keys()))
    for r in rows[:15]:
        p("  %s" % {k: r[k] for k in list(r.keys())[:12]})

# movement summary
p("\n=== movement-summary ===")
ms = os.path.join(CAP, "movement-summary.json")
if os.path.exists(ms):
    p(json.dumps(json.load(open(ms, encoding="utf-8-sig")), indent=2)[:1500])

open(OUT, "w", encoding="utf-8").write("\n".join(lines))
print("wrote", OUT, "n=", len(lines))
