import re

p = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260714-shadowland-garden-save/events.log"
lines = open(p, encoding="utf-8", errors="ignore").readlines()
cur = None
for i, l in enumerate(lines):
    if "reason=teleport-ended" in l and "playfield=(Playfield2:" in l:
        m = re.search(r"Playfield2:([0-9A-Fa-f]+)", l)
        pf = int(m.group(1), 16) if m else None
        cur = pf
        print("ARRIVE pf=%s (%s) t=%s" % (m.group(1), pf, l[11:19]))
    if "Character stored" in l:
        print("  STORED @ %s pf=%s" % (l[11:19], cur))
    if "Character saved" in l and "FormatFeedback" in l:
        print("  SAVED  @ %s pf=%s" % (l[11:19], cur))
