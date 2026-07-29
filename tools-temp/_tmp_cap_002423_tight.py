# Tight extract: finish/token, doors, PAF, mob levels, aggro timing for 20260725-002423
import os, re, struct, json
from collections import defaultdict

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260725-002423"
OUT = r"tools-temp\_tmp_cap_002423_tight.txt"
ASSET = r"tools-temp\_tmp_cap_002423_assets"
os.makedirs(ASSET, exist_ok=True)

def read(n):
    with open(os.path.join(CAP, n), "r", encoding="utf-8", errors="ignore") as f:
        return f.read()

hexlog = read("packets.hex.log")
lines = []
def w(s=""):
    lines.append(s)

# FormatFeedback / CharacterAction finish / token
w("=== FINISH / REWARD / TOKEN feedback ===")
for ln in hexlog.splitlines():
    if "FormatFeedback" in ln or "Quest " in ln or "n3=Quest " in ln:
        if any(k in ln for k in ("reward", "Reward", "token", "Token", "xp", "XP", "credit", "Delete", "Received", "%", "percent", "mission")):
            w(ln[:300])
    if "FormattedMessage=" in ln and any(k in ln.lower() for k in ("reward", "token", "xp", "credit", "bonus", "percent", "mission")):
        w(ln[:350])

# system messages with reward/token
w("\n=== system-messages reward/token ===")
for ln in read("system-messages.log").splitlines():
    if any(k in ln for k in ("reward", "Reward", "token", "Token", "Received", "xp was", "credits", "Delete", "SK,")):
        w(ln[:400])

# InfoRequest on NPC (find person complete?)
w("\n=== InfoRequest targets in instance ===")
for ln in hexlog.splitlines():
    if "InfoRequest" in ln and "799" in ln:  # instance mobs
        w(ln[:250])

# Door burst: extract hex for unique doors after PLAYFIELD 1443840
w("\n=== Door hex extract ===")
door_hexes = []
seen = set()
in_mish = False
for ln in hexlog.splitlines():
    if "PLAYFIELD-INIT" in ln or "pf=1443840" in ln or "PlayfieldAnarchyF" in ln and "160800" in ln:
        in_mish = True
    if not in_mish:
        continue
    if "n3=DoorFullUpdate" not in ln:
        continue
    m = re.search(r"hex=([0-9A-Fa-f]+)", ln)
    if not m:
        continue
    h = m.group(1).upper()
    # door identity near C748
    key = h[h.find("00C748"):h.find("00C748")+16] if "00C748" in h else h[-48:]
    if key in seen:
        continue
    seen.add(key)
    door_hexes.append(h)

w("unique doors=%d" % len(door_hexes))
open(os.path.join(ASSET, "doors_1443840.txt"), "w").write("\n".join(door_hexes))

# PAF
w("\n=== PlayfieldAnarchyF ===")
for ln in hexlog.splitlines():
    if "n3=PlayfieldAnarchyF" in ln and "160800" in ln:
        m = re.search(r"hex=([0-9A-Fa-f]+)", ln)
        if m:
            h = m.group(1).upper()
            open(os.path.join(ASSET, "paf_1443840.hex"), "w").write(h)
            w("paf len=%d" % (len(h)//2))
            # find generator payload: second C79F after body start
            b = bytes.fromhex(h)
            # find all C79F
            idxs = [i for i in range(len(b)-4) if b[i:i+4]==bytes.fromhex("0000C79F")]
            w("C79F at %s" % idxs[:8])
            if len(idxs) >= 2:
                pl = b[idxs[1]:]
                # trim trailing? find FFFFFFFF end
                end = pl.find(bytes.fromhex("FFFFFFFFFFFFFFFF"))
                if end > 0:
                    pl = pl[:end+8]
                open(os.path.join(ASSET, "paf_1443840_payload.hex"), "w").write(pl.hex().upper())
                w("payload len=%d head=%s" % (len(pl), pl[:16].hex()))
            break

# Chest
chest = []
seen_c = set()
for ln in hexlog.splitlines():
    if "n3=ChestFullUpdate" not in ln:
        continue
    if "160800" not in ln and "1443840" not in ln:
        # check hex for playfield
        m = re.search(r"hex=([0-9A-Fa-f]+)", ln)
        if not m:
            continue
        h = m.group(1).upper()
        if "00160800" not in h:
            continue
    else:
        m = re.search(r"hex=([0-9A-Fa-f]+)", ln)
        if not m:
            continue
        h = m.group(1).upper()
    key = h[-48:]
    if key in seen_c:
        continue
    seen_c.add(key)
    chest.append(h)
w("\nunique chests=%d" % len(chest))
open(os.path.join(ASSET, "chests_1443840.txt"), "w").write("\n".join(chest))

# Mob levels/HP from SCFU in instance
w("\n=== Instance SCFU mobs (lvl/hp) ===")
mobs = []
for ln in hexlog.splitlines():
    if "SimpleCharFullUpdate" not in ln or "PlayfieldId=1443840" not in ln:
        continue
    if 'Name="Getkeep"' in ln:
        continue
    name = re.search(r'Name="([^"]+)"', ln)
    lvl = re.search(r"Level=(\d+)", ln)
    hp = re.search(r"Health=(\d+)", ln)
    md = re.search(r"MonsterData=(\d+)", ln)
    pos = re.search(r"Position=\(([^)]+)\)", ln)
    if name and lvl and hp:
        mobs.append((name.group(1), int(lvl.group(1)), int(hp.group(1)), md.group(1) if md else "?", pos.group(1) if pos else "?"))

# unique by name+pos
uniq = {}
for m in mobs:
    uniq[(m[0], m[3])] = m
vals = list(uniq.values())
w("unique mobs=%d" % len(vals))
for m in sorted(vals, key=lambda x: x[1])[:40]:
    w("%s L%d HP%d md=%s pos=%s" % m)
if vals:
    lvls = [m[1] for m in vals]
    hps = [m[2] for m in vals]
    w("lvl range %d-%d hp range %d-%d avg hp/lvl=%.1f" % (min(lvls), max(lvls), min(hps), max(hps), sum(hps)/max(1,sum(lvls))*sum(lvls)/len(hps) if sum(lvls) else 0))
    ratios = [m[2]/m[1] for m in vals if m[1]>0]
    w("hp/level median-ish avg=%.2f" % (sum(ratios)/len(ratios)))

# Quest short text from mission-flow
w("\n=== Quest short from IN-QUEST-FULL ===")
for ln in read("mission-flow.log").splitlines():
    if "IN-QUEST-FULL" in ln:
        w(ln)

# CharacterAction 47 / token
w("\n=== CharacterAction finish-ish ===")
for ln in hexlog.splitlines():
    if "CharacterAction" in ln and ("Parameter1" in ln or "Action=" in ln):
        if any(k in ln for k in ("47", "Delete", "MissionKey", "Unknown2")):
            if "DETAIL" in ln or "Detail" in ln or "Action=" in ln:
                w(ln[:300])

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(lines))
print("wrote", OUT)
print("doors", len(door_hexes), "chests", len(chest), "mobs", len(vals))
