# Mob damage amounts vs player from L7 capture
from __future__ import print_function
import re
from datetime import datetime

CAP=r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260725-002423\events.log"
PLAYER="797E30D7"
out=[]
# After mob SAW, HealthDamage on player
lines=open(CAP,encoding="utf-8",errors="replace").read().splitlines()
amounts=[]
for i,line in enumerate(lines):
    if "HealthDamageMessage" in line and PLAYER in line and "Target=(SimpleChar:%s)"%PLAYER in line:
        am=re.search(r"Amount=(\d+)", line)
        # was there a mob Attack shortly before?
        mob=None
        for j in range(max(0,i-8), i):
            if "AttackMessage" in lines[j] and "Target=(SimpleChar:%s)"%PLAYER in lines[j]:
                mm=re.search(r"Identity=\(SimpleChar:([0-9A-F]+)\)", lines[j])
                if mm and mm.group(1)!=PLAYER:
                    mob=mm.group(1)
        if am and mob:
            amounts.append(int(am.group(1)))
            if len(amounts)<=20:
                out.append("dmg=%s from=%s"%(am.group(1), mob))

if amounts:
    amounts.sort()
    out.append("n=%d min=%d med=%d max=%d"%(len(amounts),amounts[0],amounts[len(amounts)//2],amounts[-1]))
open(r"tools-temp\_tmp_cap_002423_dmg.txt","w").write("\n".join(out))
print("\n".join(out[-5:]))
