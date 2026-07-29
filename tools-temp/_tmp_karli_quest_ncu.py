# Extract Karli quest / dialog / NCU / exit from 20260727-Alien- quest-ncu
from __future__ import print_function
import csv
import re
import struct
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-Alien- quest-ncu")
out = Path(r"tools-temp/_tmp_karli_quest_ncu.txt")
lines = []

def add(s=""):
    lines.append(s)

add("=== capture files ===")
for p in sorted(cap.iterdir()):
    add("%s %s" % (p.name, p.stat().st_size))
add()

KARLI = "799AD394"
name_hex = "4B61726C692043617070656C6C657269"

# SCFU Karli
add("=== Karli SCFU ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "Karli" in ln or KARLI in ln:
        if any(k in ln for k in ("SimpleCharFullUpdate", "CHAR-SEEN", "FollowTarget", "Knubot", "KnuBot", "Append", "Answer", "Quest", "Template", "NCU", "Teleport", "ACGEntrance", "Door")):
            add(ln[:900])
add()

# All knubot / dialogue
add("=== Knubot / dialogue / chat with Karli ===")
for path in ("events.log", "npc-interactions.log", "chat-dialogue.log", "system-messages.log"):
    p = cap / path
    if not p.exists():
        continue
    for ln in p.read_text(encoding="utf-8", errors="replace").splitlines():
        if any(k in ln for k in ("KnuBot", "Knubot", "AppendText", "AnswerList", "OpenChat", "CloseChat", "Karli", "NCU", "Experience", "XP")):
            if "CurrentNano" in ln and "Karli" not in ln and "NCU" not in ln:
                continue
            add("[%s] %s" % (path, ln[:800]))
add()

# Teleports / exit
add("=== Teleport / door / exit ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if any(k in ln for k in ("N3Teleport", "Teleport", "ACGEntrance", "Door:", "PlayfieldId=", "ChangePlayfield", "Crashed")):
        add(ln[:700])
add()

# Quest tips / QFU
add("=== QuestFullUpdate / tip ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "Quest" in ln or "Tip" in ln or "FormatFeedback" in ln:
        add(ln[:700])
add()

# TemplateAction / items / NCU
add("=== Item grants / TemplateAction / NCU stats ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if any(k in ln for k in ("TemplateAction", "AddTemplate", "WeaponItemFull", "SimpleItemFull", "NCU", "MaxNCU", "ItemLowId", "ContainerAdd")):
        add(ln[:650])
add()

# FormatFeedback clean
add("=== FormatFeedback messages ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "FormatFeedback" in ln and "FormattedMessage=" in ln:
        m = re.search(r'FormattedMessage="([^"]*)"', ln)
        if m and m.group(1) and not m.group(1).startswith("~&"):
            add(m.group(1))
add()

# Decode Karli textures from SCFU hex
add("=== Karli SCFU hex textures/meshes ===")
for ln in (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if name_hex.upper() not in ln.upper():
        continue
    m = re.search(r"hex=([0-9A-Fa-f]+)", ln)
    if not m:
        continue
    raw = bytes.fromhex(m.group(1))
    add("len=%d" % len(raw))
    # find 000017A6 markers
    marker = bytes.fromhex("000017A6")
    idx = 0
    found = 0
    while True:
        i = raw.find(marker, idx)
        if i < 0:
            break
        found += 1
        add("marker#%d at %d" % (found, i))
        p = i + 4
        if found == 1:
            for t in range(5):
                if p + 12 > len(raw):
                    break
                place, tid, unk = struct.unpack_from(">III", raw, p)
                add("  tex place=%s id=%s unk=%s (place>>8=%s id_shift=%s)" % (
                    place, tid, unk, place >> 8, ((place & 0xFF) << 24) | (tid >> 8)))
                # alternate decode: place as byte-aligned 0000000N
                alt_place = struct.unpack_from(">I", raw, p)[0]
                # try reading as place int correctly if bytes are 00 00 00 NN
                p += 12
        else:
            for t in range(5):
                if p + 10 > len(raw):
                    break
                pos = raw[p]; p += 1
                mid = struct.unpack_from(">I", raw, p)[0]; p += 4
                ovr = struct.unpack_from(">i", raw, p)[0]; p += 4
                layer = raw[p]; p += 1
                add("  mesh pos=%s id=%s ovr=%s layer=%s" % (pos, mid, ovr, layer))
        idx = i + 4
        if found >= 2:
            break
    # also dump detail from events for Waypoints Textures
    break

# GenericCmd Use around door/exit
add()
add("=== GenericCmd Use targets near end ===")
uses = []
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "GenericCmd" in ln and ("OUT" in ln or "DETAIL" in ln):
        uses.append(ln[:500])
for u in uses[-40:]:
    add(u)

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "lines", len(lines), "cap_exists", cap.exists())
