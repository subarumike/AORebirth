# Deep extract AppendText, quest tip, NCU, exit from Karli quest capture
from __future__ import print_function
import re
import struct
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-Alien- quest-ncu")
out = Path(r"tools-temp/_tmp_karli_quest_deep.txt")
lines = []

def add(s=""):
    lines.append(s)

# AppendText / all knubot hex decode strings
add("=== KnubotAppendText / readable strings in packets ===")
for ln in (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "Knubot" not in ln and "KnuBot" not in ln and "AppendText" not in ln and "AnswerList" not in ln:
        # also check n3 type names
        if "n3=Knubot" not in ln and "n3=KnuBot" not in ln and "n3=Append" not in ln:
            if "799AD394" not in ln.upper():
                continue
    m = re.search(r"hex=([0-9A-Fa-f]+)", ln)
    if not m:
        continue
    raw = bytes.fromhex(m.group(1))
    # extract printable ascii runs
    texts = []
    cur = []
    for b in raw:
        if 32 <= b < 127:
            cur.append(chr(b))
        else:
            if len(cur) >= 4:
                texts.append("".join(cur))
            cur = []
    if len(cur) >= 4:
        texts.append("".join(cur))
    if texts:
        add("%s | %s" % (ln[:80], " || ".join(texts)))
add()

# events AppendText
add("=== events AppendText / QuestFull / tip ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if any(k in ln for k in ("AppendText", "QuestFull", "Quest ", "tip", "Tip", "301250", "NCU", "MaxNCU", "TemplateAction", "AddTemplate", "CharacterAction", "DeleteItem", "GenericCmd")):
        add(ln[:800])
add()

# npc-interactions full for Append
add("=== npc-interactions Append / text fields ===")
for ln in (cap / "npc-interactions.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "text=" in ln.lower() or "Append" in ln or "Karli" in ln:
        add(ln[:900])
add()

# scfu-appearance Karli textures
add("=== scfu / enemy-full Karli ===")
import csv
for path in ("scfu-appearance.csv", "enemy-full-updates.csv"):
    p = cap / path
    if not p.exists():
        continue
    with p.open(encoding="utf-8-sig", errors="replace") as f:
        r = csv.DictReader(f)
        add("cols %s: %s" % (path, r.fieldnames))
        for row in r:
            blob = " ".join((row.get(k) or "") for k in row)
            if "Karli" in blob or "799AD394" in blob:
                add(str({k: row[k] for k in row if row.get(k)}))
add()

# Decode SCFU from first Karli name hit properly
add("=== decode Karli SCFU textures ===")
name_hex = "4B61726C692043617070656C6C657269"
for ln in (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if name_hex.upper() not in ln.upper() or "SimpleCharFullUpdate" not in ln:
        continue
    m = re.search(r"hex=([0-9A-Fa-f]+)", ln)
    raw = bytes.fromhex(m.group(1))
    add("scfu_len=%d" % len(raw))
    # after waypoints marker 000017A6 for textures - find all
    marker = bytes.fromhex("000017A6")
    positions = []
    start = 0
    while True:
        i = raw.find(marker, start)
        if i < 0:
            break
        positions.append(i)
        start = i + 4
    add("markers=%s" % positions)
    if len(positions) >= 1:
        p = positions[0] + 4
        for t in range(8):
            if p + 12 > (positions[1] if len(positions) > 1 else len(raw)):
                break
            place, tid, unk = struct.unpack_from(">III", raw, p)
            add("tex%d place=%d id=%d unk=%d" % (t, place, tid, unk))
            p += 12
    if len(positions) >= 2:
        p = positions[1] + 4
        for t in range(6):
            if p + 10 > len(raw):
                break
            pos = raw[p]; p += 1
            mid = struct.unpack_from(">I", raw, p)[0]; p += 4
            ovr = struct.unpack_from(">i", raw, p)[0]; p += 4
            layer = raw[p]; p += 1
            add("mesh%d pos=%d id=%d ovr=%d layer=%d" % (t, pos, mid, ovr, layer))
    break

# Exit door details
add()
add("=== exit door Use + landing ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if any(k in ln for k in ("C0001F49", "108CD4D0", "DoorStatus", "N3Teleport", "ACGEntrance", "PlayfieldAnarchy")):
        add(ln[:750])

# Item 301250 / NCU around 03:55:43-03:56:00
add()
add("=== window 03:55:40 - 03:56:05 full events ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "03:55:4" in ln or "03:55:5" in ln or "03:56:0" in ln:
        if "CurrentNano" in ln:
            continue
        add(ln[:700])

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "n", len(lines))
