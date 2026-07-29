"""Decode SpellList + SCFU ExtTex for Marcus (78E0FC62) and frequent SpellList ids."""
from pathlib import Path
import re
import struct

cap = Path(r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260721-marcus-animation-texture-dialogtext")
out = Path(r"tools-temp\_tmp_marcus_vfx_decode.txt")
lines_out = []

# From enemy-fight / events: Marcus = 78E0FC62
marcus = 0x78E0FC62
robot_candidates = set()

# Parse raw-packets or packets.hex for SpellList payloads
# Prefer events with detail if available; else hex log

def parse_identity(s):
    m = re.search(r"SimpleChar:([0-9A-Fa-f]+)", s)
    return int(m.group(1), 16) if m else None

# Collect AttackInfo / SpecialAttack for Marcus
fight = cap / "enemy-fight-events.log"
with fight.open("r", encoding="utf-8-sig", errors="replace") as f:
    for line in f:
        if "78E0FC62" in line or "79666CF1" in line:
            lines_out.append("FIGHT " + line.rstrip()[:400])

# SpellList decode from packets.hex if present
hexpath = cap / "packets.hex.log"
spell_hits = []
if hexpath.exists():
    with hexpath.open("r", encoding="utf-8-sig", errors="replace") as f:
        for line in f:
            if "SpellList" in line or "0x355" in line or "ExtTex" in line:
                spell_hits.append(line.rstrip()[:500])
                if len(spell_hits) >= 30:
                    break
lines_out.append(f"\nhex SpellList/ExtTex lines {len(spell_hits)}")
lines_out.extend(spell_hits[:30])

# Try decode from raw-packets.csv columns
raw = cap / "raw-packets.csv"
if raw.exists():
    lines_out.append("\n===== raw-packets SpellList sample =====")
    with raw.open("r", encoding="utf-8-sig", errors="replace") as f:
        hdr = f.readline()
        lines_out.append(hdr.rstrip()[:200])
        n = 0
        for line in f:
            if "SpellList" in line or "SpecialAttack" in line:
                lines_out.append(line.rstrip()[:500])
                n += 1
                if n >= 25:
                    break

# Check if another capture folder nearby has turn-in
parent = cap.parent
marcus_caps = [p.name for p in parent.iterdir() if p.is_dir() and "marcus" in p.name.lower()]
lines_out.append("\n===== marcus-named captures =====")
lines_out.extend(marcus_caps)

out.write_text("\n".join(lines_out), encoding="utf-8")
print("wrote", out)
