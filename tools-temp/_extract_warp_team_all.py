import csv
import os
import re
from collections import Counter

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260808-warp-team"
OUT = r"tools-temp\_warp_team_all_extract.txt"

info = open(os.path.join(CAP, "capture_info.json"), encoding="utf-8-sig").read()
open(r"tools-temp\_warp_team_info.json", "w", encoding="utf-8").write(info)

ev = open(os.path.join(CAP, "events.log"), encoding="utf-8-sig", errors="replace").read().splitlines()
lines = []
pat = re.compile(
    r"CastNano|FinishNano|CastNanoSpell|Feedback|SimpleCharFullUpdate|AppearanceUpdate|CharInPlay|"
    r"WeaponItem|SimpleItem|ChestFull|TeamMember|DYNEL|CHAR-SEEN|CHAR-IN-PLAY|Parameter2=",
    re.I,
)
for i, line in enumerate(ev, 1):
    if "CurrentNano=" in line and "CastNano" not in line and "FinishNano" not in line:
        continue
    if pat.search(line):
        lines.append("%d: %s" % (i, line[:1100]))

# SCFU rows
scfu_path = os.path.join(CAP, "scfu-appearance.csv")
scfu = list(csv.DictReader(open(scfu_path, encoding="utf-8-sig"))) if os.path.exists(scfu_path) else []
lines.append("\n=== SCFU ROWS %d ===" % len(scfu))
for r in scfu:
    lines.append(
        "%s | %s | lvl=%s hp=%s pos=(%s,%s,%s) flags=%s"
        % (r.get("Identity"), r.get("Name"), r.get("Level"), r.get("Health"), r.get("PositionX"), r.get("PositionY"), r.get("PositionZ"), r.get("Flags"))
    )

# raw types around cast
rows = list(csv.DictReader(open(os.path.join(CAP, "raw-packets.csv"), encoding="utf-8-sig")))
lines.append("\n=== RAW N3 TYPES ===")
lines.append(str(Counter(r.get("N3TypeName") for r in rows).most_common()))
lines.append("\n=== RAW INTERESTING ===")
for r in rows:
    name = r.get("N3TypeName") or ""
    if name in (
        "CastNanoSpell", "CharacterAction", "Feedback", "SimpleCharFullUpdate",
        "AppearanceUpdate", "CharInPlay", "WeaponItemFullUpdate", "SimpleItemFullUpdate",
        "ChestFullUpdate", "TeamMemberInfo", "SpellList"
    ) or "Spell" in name or "Effect" in name:
        lines.append(
            "%s %s seq=%s %s id=%s:%s"
            % (r.get("CapturedUtc"), r.get("Direction"), r.get("Sequence"), name, r.get("IdentityType"), r.get("IdentityInstance"))
        )

# nanos cast
lines.append("\n=== CAST/FINISH NANOS ===")
for line in lines:
    m = re.search(r"Action=CastNano .*?Target=\(([^)]+)\).*?Parameter2=(\d+)", line)
    if m:
        lines.append("CAST target=%s nano=%s" % (m.group(1), m.group(2)))
    m = re.search(r"Action=FinishNanoCasting .*?Parameter2=(\d+)", line)
    if m:
        lines.append("FINISH nano=%s" % m.group(1))

text = "\n".join(lines)
open(OUT, "w", encoding="utf-8").write(text)
print("wrote", OUT, "lines", len(lines), "scfu", len(scfu))
# print key bits
for line in lines:
    if any(x in line for x in ("CAST ", "FINISH ", "SCFU", "SimpleCharFullUpdate", "CastNano ", "FinishNano", "CHAR-SEEN", "DYNEL", "Appearance", "CharInPlay", "=== ")):
        print(line[:300])
