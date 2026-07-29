# Focused Karli 8009 + alien XP/loot from 20260727-055715
from __future__ import print_function
import csv
import json
import re
import struct
from collections import Counter, defaultdict
from datetime import datetime
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-055715")
out = Path(r"tools-temp/_tmp_karli_055715_deep.txt")
lines = []
KARLI = "799AD394"

def add(s=""):
    lines.append(s)

# --- SCFU / waypoints from packets ---
add("=== Karli SCFU / waypoints ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if KARLI in ln or "Karli" in ln:
        if any(k in ln for k in ("SimpleCharFullUpdate", "Waypoint", "CHAR-SEEN", "Follow", "NpcPath", "Movement", "Knubot", "Quest", "Template")):
            add(ln[:900])
add()

# hexlog Karli name
name_hex = "4B61726C692043617070656C6C657269"  # Karli Cappelleri
add("=== packets.hex Karli SCFU bodies ===")
n = 0
for ln in (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if name_hex.upper() not in ln.upper():
        continue
    m = re.search(r"hex=([0-9A-Fa-f]+)", ln)
    if not m:
        continue
    raw = bytes.fromhex(m.group(1))
    n += 1
    add("pkt#%d len=%d n3=%s" % (n, len(raw), ln[ln.find("n3="):ln.find("n3=")+40] if "n3=" in ln else "?"))
    # find floats near end that look like waypoints
    idx = raw.find(b"Karli Cappelleri")
    add("  after_name=%s" % raw[idx:idx+200].hex().upper() if idx >= 0 else "?")
    if n >= 5:
        break
add("scfu_pkts=%d" % n)
add()

# --- all knubot after 04:06 ---
add("=== Knubot / Append / Answer after ship entry ===")
for path in ("events.log", "npc-interactions.log", "chat-dialogue.log"):
    p = cap / path
    if not p.exists():
        continue
    for ln in p.read_text(encoding="utf-8", errors="replace").splitlines():
        if "04:0" not in ln and "04:1" not in ln:
            # also include if Karli target
            if KARLI not in ln and "Karli" not in ln:
                continue
        if any(k in ln for k in ("Knubot", "KnuBot", "AppendText", "AnswerList", "Answer ", "QuestFull", "OpenChat", "CloseChat")):
            add("[%s] %s" % (path, ln[:700]))
add()

# --- movement for Karli identity ---
add("=== movement-packets / enemy-movement Karli coords ===")
pts = []
for path in ("enemy-movement.csv", "movement-packets.csv"):
    p = cap / path
    if not p.exists():
        continue
    with p.open(encoding="utf-8-sig", errors="replace") as f:
        r = csv.DictReader(f)
        for row in r:
            blob = " ".join((row.get(k) or "") for k in row)
            if KARLI not in blob and "Karli" not in blob:
                continue
            x = row.get("PositionX") or row.get("X") or row.get("DestX")
            y = row.get("PositionY") or row.get("Y") or row.get("DestY")
            z = row.get("PositionZ") or row.get("Z") or row.get("DestZ")
            add("%s %s %s,%s,%s type=%s" % (
                path, row.get("CapturedUtc"), x, y, z,
                row.get("MessageType") or row.get("Action") or ""))
            try:
                pts.append((float(x), float(y), float(z)))
            except Exception:
                pass
add("unique_pts=%d" % len(set((round(a,2), round(b,2), round(c,2)) for a,b,c in pts)))
# cluster unique rounded
uniq = []
seen = set()
for a,b,c in pts:
    key = (round(a,1), round(b,2), round(c,1))
    if key in seen:
        continue
    seen.add(key)
    uniq.append(key)
add("path_sample=%s" % uniq[:80])
add()

# --- alien XP deltas ---
add("=== AlienXP / XP kill correlation ===")
# deaths of aliens + nearby XP
deaths = []
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "Remains of" in ln and ("Alien" in ln or "Spider" in ln or "Scout" in ln or "Specialist" in ln or "Minibull" in ln or "Saltworm" in ln or "Harvey" in ln):
        deaths.append(ln[:300])
    if "AlienXP=" in ln or ( "Stats=count=1[XP=" in ln and "7996C028" in ln):
        add(ln[:350])
add("deaths:")
for d in deaths[:40]:
    add("  " + d)
add()

# enemy-stat AlienXP sequence
add("=== AlienXP timeline ===")
vals = []
with (cap / "enemy-stat-updates.csv").open(encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        if row.get("Stat") == "AlienXP":
            vals.append((row.get("CapturedUtc"), int(row.get("Value") or 0)))
            add("%s AlienXP=%s" % (row.get("CapturedUtc"), row.get("Value")))
        if row.get("Stat") == "XP" and (row.get("IdentityRole") == "local-player" or "7996C028" in (row.get("Identity") or "")):
            add("%s XP=%s" % (row.get("CapturedUtc"), row.get("Value")))
if len(vals) >= 2:
    for i in range(1, len(vals)):
        add("delta %s -> %s = %+d" % (vals[i-1][1], vals[i][1], vals[i][1]-vals[i-1][1]))
add()

# --- loot ---
add("=== all corpse loot ===")
with (cap / "corpse-loot-observations.csv").open(encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        add("%s md=%s lvl=%s cred=%s items=%s dead=%s" % (
            row.get("EnemyName"), row.get("MonsterData"), row.get("EnemyLevel"),
            row.get("CorpseCredits"), row.get("Items"), row.get("DeadNpcIdentity")))
add()

# --- FormatFeedback / NCU / buff item ---
add("=== FormatFeedback all ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "FormatFeedback" in ln and "FormattedMessage=" in ln:
        m = re.search(r'FormattedMessage="([^"]*)"', ln)
        if m:
            add("%s | %s" % (ln[:30], m.group(1)))
add()

# TemplateAction / AddTemplate / ContainerAdd after 04:06
add("=== item grants after 04:06 ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "04:0" not in ln and "04:1" not in ln:
        continue
    if any(k in ln for k in ("TemplateAction", "AddTemplate", "ContainerAdd", "ACGItem", "NCU", "QuestFull", "DeleteItem", "WeaponItemFull")):
        add(ln[:650])
add()

# scfu-appearance Karli
add("=== scfu-appearance Karli ===")
with (cap / "scfu-appearance.csv").open(encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        raw = row.get("RawPacketHex") or ""
        if name_hex.upper() in raw.upper():
            add("utc=%s len=%s" % (row.get("CapturedUtc"), row.get("PacketLength")))
            raw_b = bytes.fromhex(raw)
            # decode appearance / flags from known SCFU layout is hard; dump floats
            floats = []
            for i in range(0, len(raw_b)-4, 4):
                v = struct.unpack_from(">f", raw_b, i)[0]
                if abs(v) < 5000 and abs(v) > 0.01 and (v == v):
                    floats.append((i, round(v, 3)))
            add("floats_sample=%s" % floats[:60])

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "n", len(lines), "path_pts", len(pts), "aixp", len(vals))
