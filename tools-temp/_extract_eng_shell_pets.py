# Extract engineer shell + pet spawn evidence from capture 20260808-131854
import csv
import json
import os
import re
from collections import Counter, defaultdict

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260808-131854"
OUT = r"tools-temp\_eng_shell_pet_extract.txt"

def read_csv(name):
    path = os.path.join(CAP, name)
    with open(path, newline="", encoding="utf-8-sig", errors="replace") as f:
        return list(csv.DictReader(f))

lines = []
def w(s=""):
    lines.append(s)

scfu = read_csv("scfu-appearance.csv")
w("=== SCFU PETS (%d rows) ===" % len(scfu))
by_name = Counter(r["Name"] for r in scfu)
w("Names: " + json.dumps(by_name, ensure_ascii=False))

# group by name + monsterdata + scale + family
groups = defaultdict(list)
for r in scfu:
    key = (r["Name"], r["MonsterData"], r["MonsterScale"], r["NpcFamily"], r["VisualFlags"], r.get("Side",""), r.get("Breed",""))
    groups[key].append(r)

w("\n=== UNIQUE PET SPAWN PROFILES ===")
for key, rows in sorted(groups.items(), key=lambda kv: (kv[0][0], int(kv[0][1] or 0))):
    name, md, scale, fam, vf, side, breed = key
    levels = sorted({int(r["Level"]) for r in rows})
    healths = sorted({int(r["Health"]) for r in rows})
    speeds = sorted({int(r["RunSpeedBase"] or 0) for r in rows})
    ids = [r["Identity"] for r in rows]
    owners = sorted({r.get("Owner","") for r in rows})
    w("- name=%s md=%s scale=%s family=%s visualFlags=%s side=%s breed=%s" % (name, md, scale, fam, vf, side, breed))
    w("  count=%d levels=%s healths=%s runSpeeds=%s owners=%s" % (len(rows), levels, healths[:12], speeds, owners))
    w("  identities=%s" % ", ".join(ids))
    # sample first row extras
    r0 = rows[0]
    w("  textures=%s meshes=%s flags=%s flags2=%s headMesh=%s" % (
        r0.get("Textures"), r0.get("Meshes"), r0.get("Flags"), r0.get("Flags2"), r0.get("HeadMesh")))
    w("  pos=(%s,%s,%s) playfield=%s" % (r0.get("PositionX"), r0.get("PositionY"), r0.get("PositionZ"), r0.get("PlayfieldId")))

# lifecycle phases around pets / shells
life = read_csv("npc-lifecycle.csv")
w("\n=== LIFECYCLE PHASES ===")
phases = Counter(r["Phase"] for r in life)
w(json.dumps(phases, ensure_ascii=False))
w("\n=== LIFECYCLE PET/SHELL/CONTAINER ROWS ===")
for r in life:
    blob = " ".join(r.values())
    if re.search(r"pet|Shell|Container|Template|Overflow|GenericCmd|Cast|Nano|UseItem|AttackPet|Guardbot|Attackbot|Healbot|Warbot", blob, re.I):
        w("%s | %s | %s | %s | %s | %s" % (r.get("CapturedUtc"), r.get("Phase"), r.get("MessageType"), r.get("PrimaryIdentity"), r.get("Name"), (r.get("Detail") or "")[:350]))

# events
pat = re.compile(
    r"Shell|FinishNano|CastNano|TemplateAction|GenericCmd|ContainerAdd|CharacterAction|"
    r"Summon|Pet|AttackPet|Trade|InventoryUpdate|ItemStatus|NanoFormula|"
    r"AddPet|SetPet|DeleteStat|StatMessage.*Pet|TemplateAction|SpecialUsed",
    re.I,
)
w("\n=== FILTERED EVENTS (no CurrentNano spam) ===")
hits = 0
nano_casts = []
shell_adds = []
uses = []
with open(os.path.join(CAP, "events.log"), encoding="utf-8", errors="replace") as f:
    for i, line in enumerate(f, 1):
        if "CurrentNano=" in line and not re.search(r"Shell|Pet|FinishNano|CastNano|GenericCmd|TemplateAction|ContainerAdd|CharacterAction|Summon|AddPet", line, re.I):
            continue
        if not pat.search(line):
            continue
        hits += 1
        clip = line.rstrip()
        if len(clip) > 700:
            clip = clip[:700] + "..."
        w("%d: %s" % (i, clip))
        if re.search(r"FinishNano|CastNano|NanoFormula", line, re.I):
            nano_casts.append((i, clip))
        if re.search(r"TemplateAction|ContainerAdd|Overflow|Shell", line, re.I):
            shell_adds.append((i, clip))
        if re.search(r"GenericCmd|Use", line, re.I):
            uses.append((i, clip))

w("\nFILTERED EVENT HITS: %d" % hits)
w("NANO-RELATED: %d" % len(nano_casts))
w("SHELL/TEMPLATE/CONTAINER: %d" % len(shell_adds))
w("USE/GENERICCMD: %d" % len(uses))

# dossier
with open(os.path.join(CAP, "enemy-dossier.json"), encoding="utf-8-sig", errors="replace") as f:
    dossier = json.load(f)
w("\n=== DOSSIER TOP ===")
if isinstance(dossier, dict):
    w("keys=" + ",".join(list(dossier.keys())[:50]))
    ents = dossier.get("enemies") or dossier.get("entities") or dossier.get("byIdentity") or dossier.get("characters")
    if ents is None:
        # maybe list at root or nested
        for k,v in dossier.items():
            if isinstance(v, (list, dict)) and k.lower() not in ("meta","summary","capture"):
                ents = v
                w("using key="+k)
                break
    if isinstance(ents, list):
        for e in ents:
            name = e.get("name") or e.get("Name") or ""
            if "Engineer" in str(name) or e.get("isPet") or "IsPet" in str(e.get("flags","")):
                w(json.dumps(e, ensure_ascii=False)[:500])
    elif isinstance(ents, dict):
        for k,v in ents.items():
            if not isinstance(v, dict):
                continue
            name = str(v.get("name") or v.get("Name") or "")
            flags = str(v.get("flags") or v.get("Flags") or "")
            if "Engineer" in name or "IsPet" in flags or v.get("isPet"):
                w("%s %s" % (k, json.dumps({kk:v[kk] for kk in list(v)[:30]}, ensure_ascii=False)[:450]))

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(lines))
print("Wrote", OUT, "lines", len(lines))
