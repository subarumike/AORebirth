# Test FixedAttackOnSight IsCombatReady logic roughly
# Also extract chat SystemMessage wire from events if any

from pathlib import Path
import re

# Find SystemMessage in events for 234537
p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260730-234537/events.log")
if p.exists():
    count=0
    with p.open(encoding='utf-8-sig', errors='replace') as fh:
        for line in fh:
            if 'SystemMessage' in line or 'I will follow' in line:
                print(line[:500])
                count += 1
                if count>=8:
                    break
    print('count', count)
else:
    print('no events')
