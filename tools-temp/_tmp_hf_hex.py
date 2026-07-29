# parse capture CastNanoSpell + SpellList for Hellfyre rocket
from pathlib import Path
import re
log = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-204902/packets.hex.log").read_text(encoding="utf-8", errors="replace")
for line in log.splitlines():
    if "18:53:22.197" in line or "18:53:22.198" in line:
        m = re.search(r"n3=(\w+) hex=([0-9A-Fa-f]+)", line)
        if not m: continue
        name, hexs = m.group(1), m.group(2)
        b = bytes.fromhex(hexs)
        print(name, "len", len(b))
        if name == "CastNanoSpell":
            # find nano id 000483CF
            print(" ", hexs)
        if name == "SpellList":
            print(" ", hexs)
        if name == "HealthDamage":
            print(" ", hexs)
