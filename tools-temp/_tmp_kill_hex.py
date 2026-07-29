# extract CastNanoSpell hex around first sandstorm kill
from pathlib import Path
import re
log = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-204902/packets.hex.log").read_text(encoding="utf-8", errors="replace")
# lines around 18:53:22.197
for line in log.splitlines():
    if "18:53:22.19" in line or "18:53:22.41" in line or "18:53:22.93" in line or "18:53:22.94" in line:
        if any(t in line for t in ("CastNanoSpell", "SpellList", "HealthDamage", "AttackInfo", "FormatFeedback", "CorpseFullUpdate", "CharacterAction")):
            # trim hex
            m = re.search(r"n3=(\w+) hex=([0-9A-Fa-f]+)", line)
            if m:
                print(m.group(1), "len", len(m.group(2))//2, "hex", m.group(2)[:120], "...")
            else:
                print(line[:200])
