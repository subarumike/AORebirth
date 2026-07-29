# Deep Find-Person: spawn, doors, specials, Use on Gary, combat timing.
from __future__ import print_function
import csv, collections, os, re

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-mission-find-person"
OUT = r"tools-temp\_tmp_find_person_deep.txt"
PF = "1419349"

def rows(name):
    with open(os.path.join(CAP, name), encoding="utf-8-sig", errors="replace") as f:
        return list(csv.DictReader(f))

out = []
def p(s=""):
    out.append(s)

# Player movement first positions in PF
p("=== MOVEMENT first/last ===")
mov = rows("movement-packets.csv")
p("cols=%s rows=%d" % (list(mov[0].keys()) if mov else [], len(mov)))
for r in mov[:5]:
    p(str({k: r[k] for k in ("CapturedUtc","MessageType","PositionX","PositionY","PositionZ","PlayfieldId","Detail") if k in r and r[k]}))
p("...")
for r in mov[-5:]:
    p(str({k: r[k] for k in ("CapturedUtc","MessageType","PositionX","PositionY","PositionZ","PlayfieldId") if k in r and r[k]}))

# SCFU specials + texture overrides + fighting
scfu = rows("scfu-appearance.csv")
p("\n=== SCFU SpecialAttacks / TextureOverrides / FightingTarget ===")
by_id = {}
for r in scfu:
    if (r.get("PlayfieldId") or "") != PF:
        continue
    ident = r.get("Identity") or ""
    if ident and ident not in by_id:
        by_id[ident] = r

for ident, r in sorted(by_id.items(), key=lambda kv: kv[1].get("Name") or ""):
    name = r.get("Name") or "?"
    specials = r.get("SpecialAttacks") or ""
    texov = r.get("TextureOverrides") or ""
    fight = r.get("FightingTarget") or ""
    side = r.get("Side") or ""
    tex = r.get("Textures") or ""
    # colored if any non-zero texture id
    colored = any(part.split(":")[1] not in ("0", "", None) for part in tex.split("|") if ":" in part) if tex else False
    p("%s id=%s side=%s colored=%s specials=%s texov=%s fight=%s tex=%s" % (
        name, ident, side, colored, specials[:120], texov[:80], fight, tex[:80]))

# Identify 79907F5A / 79907F3F fought
p("\n=== FOUGHT IDENTITIES names from enemy-full / dossier ===")
enemy = rows("enemy-full-updates.csv")
wanted = {"79907F5A", "79907F3F", "79907F41", "798E2358"}
for r in enemy:
    ident = r.get("Identity") or ""
    for w in wanted:
        if w in ident:
            p("enemy %s name=%s lvl=%s md=%s specials-in-detail?" % (ident, r.get("Name"), r.get("Level"), r.get("MonsterData")))
            break

# npc interactions Use/tag
p("\n=== NPC-INTERACTIONS Use/Generic/Gary ===")
with open(os.path.join(CAP, "npc-interactions.log"), encoding="utf-8", errors="replace") as f:
    for line in f:
        if re.search(r"(?i)use|generic|gary|arnall|798E2358|tag|quest|complete", line):
            p(line.rstrip()[:450])

# events Door / Generator / Terminal / Chest
p("\n=== EVENTS Door/Chest/Generator/Terminal samples ===")
counts = collections.Counter()
samples = collections.defaultdict(list)
with open(os.path.join(CAP, "events.log"), encoding="utf-8", errors="replace") as f:
    for line in f:
        for key in ("DoorFullUpdate", "ChestFullUpdate", "PlayfieldGenerator", "Terminal",
                    "Container", "Quest", "Gary", "Arnall", "PlayfieldAnon", "ResourceId"):
            if key in line:
                counts[key] += 1
                if len(samples[key]) < 3:
                    samples[key].append(line.strip()[:400])
p("counts=%s" % counts)
for k, ss in samples.items():
    p("-- %s --" % k)
    for s in ss:
        p(s)

# combat summary by MessageType from enemy
p("\n=== COMBAT MessageType counts ===")
combat = rows("enemy-combat.csv")
p(str(collections.Counter(r.get("MessageType") for r in combat).most_common(30)))
p("Action counts: %s" % collections.Counter(r.get("Action") for r in combat if r.get("Action")).most_common(30))

# enemy attack timing: Attack from enemy sources
p("\n=== ENEMY Attack / AttackInfo / SpecialAttackWeapon from enemies ===")
atk = collections.Counter()
for r in combat:
    if r.get("SourceRole") == "enemy":
        atk[(r.get("MessageType"), r.get("Action") or "")] += 1
p(str(atk.most_common(40)))

# SpecialAttackWeapon unknown values from enemies
p("\n=== Enemy SpecialAttackWeapon Unknown1 samples ===")
for r in combat:
    if r.get("MessageType") == "SpecialAttackWeapon" and r.get("SourceRole") == "enemy":
        p("%s src=%s u1=%s u2=%s detail=%s" % (r.get("CapturedUtc"), r.get("SourceIdentity"), r.get("Unknown1"), r.get("Unknown2"), (r.get("Detail") or "")[:200]))
        if atk[("SpecialAttackWeapon","")] > 20:
            break

# lifecycle for terminal / door
p("\n=== NPC-LIFECYCLE name hints ===")
life = rows("npc-lifecycle.csv")
p("cols=%s" % (list(life[0].keys()) if life else []))
names = collections.Counter()
for r in life:
    n = r.get("Name") or r.get("DynelName") or ""
    if n:
        names[n] += 1
p("names=%s" % names.most_common(40))

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("wrote", OUT)
