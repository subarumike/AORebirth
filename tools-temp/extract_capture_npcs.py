import re, json, os

cap = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260717-210219"
events = os.path.join(cap, "events.log")

# 1) NPC spawns from DYNEL-SPAWNED npc=True lines (pos appears BEFORE monsterData)
npc_re = re.compile(r"identity=\(SimpleChar:([0-9A-Fa-f]+)\) name=(.*?) player=False npc=True .*?level=(\d+).*?pos=\(([-\d.]+), ([-\d.]+), ([-\d.]+)\).*?monsterData=(\d+)")
npcs = {}
with open(events, "r", encoding="utf-8", errors="replace") as f:
    for line in f:
        if "npc=True" not in line or "DYNEL-SPAWNED" not in line:
            continue
        m = npc_re.search(line)
        if not m:
            continue
        inst, name, lvl, x, y, z, mdata = m.groups()
        npcs[inst] = {"inst": inst, "name": name, "level": int(lvl), "monsterData": int(mdata),
                      "pos": [round(float(x),3), round(float(y),3), round(float(z),3)]}

# 2) HeadMesh + MonsterScale + Health from SCFU detail lines (match by name+level, keyed by identity at end)
scfu_re = re.compile(r"SimpleCharFullUpdateMessage \{.*?PlayfieldId=(\d+).*?Name=\"(.*?)\".*?Level=(\d+) Health=(\d+).*?MonsterData=(\d+) MonsterScale=(\d+).*?HeadMesh=(\d+) RunSpeedBase=(\d+).*?Identity=\(SimpleChar:([0-9A-Fa-f]+)\)")
scfu = {}
with open(events, "r", encoding="utf-8", errors="replace") as f:
    for line in f:
        if "SimpleCharFullUpdateMessage" not in line or "IsNpc" not in line:
            continue
        m = scfu_re.search(line)
        if not m:
            continue
        pf, name, lvl, hp, mdata, scale, head, run, inst = m.groups()
        scfu[inst] = {"playfield": int(pf), "name": name, "level": int(lvl), "health": int(hp),
                      "monsterData": int(mdata), "scale": int(scale), "headMesh": int(head), "run": int(run)}

# merge
for inst, s in scfu.items():
    if inst in npcs:
        npcs[inst].update({k: s[k] for k in ("playfield","health","scale","headMesh","run")})
    else:
        npcs[inst] = {"inst": inst, "name": s["name"], "level": s["level"], "monsterData": s["monsterData"],
                      "pos": None, "playfield": s["playfield"], "health": s["health"],
                      "scale": s["scale"], "headMesh": s["headMesh"], "run": s["run"]}

items = list(npcs.values())
print("Total unique NPCs:", len(items))
# group by name
from collections import Counter, defaultdict
byname = defaultdict(list)
for it in items:
    byname[it["name"]].append(it)
print("\nBy name:")
for name in sorted(byname):
    lst = byname[name]
    lvls = sorted(set(x["level"] for x in lst))
    mds = sorted(set(x.get("monsterData") for x in lst))
    heads = sorted(set(x.get("headMesh") for x in lst if x.get("headMesh")))
    print("  %-28s count=%d levels=%s monsterData=%s headMesh=%s" % (name, len(lst), lvls, mds, heads))

print("\nFull list (name, level, monsterData, headMesh, scale, pos):")
for it in sorted(items, key=lambda x: (x["name"], x["level"])):
    print("  %-26s L%-4d md=%-7s head=%-6s scale=%-4s pos=%s" % (
        it["name"], it["level"], it.get("monsterData"), it.get("headMesh"), it.get("scale"), it.get("pos")))

with open(os.path.join(r"C:\Users\nermi\source\repos\AORebirth\tools-temp", "rome_blue_npcs.json"), "w") as f:
    json.dump(sorted(items, key=lambda x: (x["name"], x["level"])), f, indent=2)
print("\nWrote rome_blue_npcs.json")
