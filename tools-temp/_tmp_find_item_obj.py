# Find mission-item grant + all container spawns + door packet samples.
from __future__ import print_function
import csv, os, re, collections

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-mission-find-item"
OUT = r"tools-temp\_tmp_find_item_obj.txt"

def p(s=""):
    out.append(s)
out=[]

# All DYNEL-SPAWNED Container lines
path=os.path.join(CAP,"events.log")
containers={}
doors=[]
with open(path,"r",encoding="utf-8",errors="replace") as f:
    for line in f:
        if "DYNEL-SPAWNED" in line and "Container:" in line:
            m=re.search(r"identity=\((Container:[A-F0-9]+)\) name=([^=]+) pos=\(([^)]+)\)", line)
            if m:
                containers[m.group(1)]={"name":m.group(2).strip(),"pos":m.group(3),"line":line.strip()[:220]}
        if "DoorFullUpdate" in line and len(doors)<5:
            doors.append(line.strip()[:250])
        if "ChestFullUpdate" in line:
            p("CHEST: "+line.strip()[:300])

p("=== CONTAINERS unique ===")
for ident,info in sorted(containers.items(), key=lambda x: x[1]["name"]):
    p("%s | %s | %s" % (info["name"], ident, info["pos"]))

# Search packets/events for find-item template ids and reward timing
ids=["100010","165839","165840","11329","11337","55631128"]
p("\n=== ID hits in events/system/mission-flow ===")
for fname in ("events.log","system-messages.log","mission-flow.log","npc-interactions.log"):
    fp=os.path.join(CAP,fname)
    with open(fp,"r",encoding="utf-8",errors="replace") as f:
        for i,line in enumerate(f):
            for idv in ids:
                if idv in line:
                    p("%s:%d %s" % (fname,i+1,line.strip()[:280]))
                    break

# Inventory Use Inventory:0040 - what was that?
p("\n=== Around Inventory:0040 / Chest / Quest Delete ===")
with open(os.path.join(CAP,"events.log"),"r",encoding="utf-8",errors="replace") as f:
    lines=f.readlines()
for i,line in enumerate(lines):
    if "09:37:36" in line or "09:39:0" in line or "SimpleItem" in line or "TemplateAction" in line:
        if any(k in line for k in ("Inventory:0040","Chest","SimpleItem","TemplateAction","FormatFeedback","Quest","CharacterAction","ContainerAdd","55631128","reward")):
            p(line.strip()[:320])

# GenericCmd Use targets sequence with timestamps for containers before complete
p("\n=== Container Use sequence ===")
with open(os.path.join(CAP,"npc-interactions.log"),"r",encoding="utf-8",errors="replace") as f:
    for line in f:
        if "Container:" in line or "Corpse:" in line or "Inventory:" in line or "UseItemOnItem" in line:
            p(line.strip()[:280])

p("\n=== DoorFullUpdate samples ===")
for d in doors:
    p(d)

with open(OUT,"w",encoding="utf-8") as f:
    f.write("\n".join(out))
print("wrote",OUT,"lines",len(out))
