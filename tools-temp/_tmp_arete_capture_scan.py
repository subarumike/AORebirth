# Analyze 20260719-Rex-Markus-stone Arete capture
import csv, json, re, collections
from pathlib import Path

CAP = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260719-Rex-Markus-stone")
OUT = Path(r"tools-temp/_tmp_arete_capture_report.txt")

lines = []
def p(*a):
    lines.append(" ".join(str(x) for x in a))

p("=== capture_info ===")
info = json.loads((CAP / "capture_info.json").read_text(encoding="utf-8-sig"))
p("pf", info.get("playfieldId"), "char", info.get("characterName"))
p("duration", info.get("sessionDurationSeconds"))
p("focused", info.get("focusedEnemyIdentities"))
p("counts", {k: info.get("captureCounts", {}).get(k) for k in [
    "npcInteractions", "chatDialogueMessages", "systemMessages", "enemyFullUpdateRows",
    "enemyMovementRows", "movementPacketRows", "enemyCombatRows"]})

p("\n=== chat-dialogue ===")
chat = (CAP / "chat-dialogue.log").read_text(encoding="utf-8-sig", errors="replace")
p(chat[:6000])

p("\n=== npc-interactions (first 5k) ===")
npc = (CAP / "npc-interactions.log").read_text(encoding="utf-8-sig", errors="replace")
p("lines", len(npc.splitlines()))
p(npc[:5000])

p("\n=== scfu-appearance unique ===")
with open(CAP / "scfu-appearance.csv", newline="", encoding="utf-8-sig") as f:
    rows = list(csv.DictReader(f))
p("rows", len(rows))
if rows:
    p("cols", list(rows[0].keys()))
by_name = collections.OrderedDict()
for r in rows:
    name = r.get("Name") or r.get("CharacterName") or r.get("name") or "?"
    ident = (r.get("Identity") or r.get("SimpleCharIdentity")
             or r.get("IdentityInstance") or r.get("IdentityText") or "?")
    key = (name, ident)
    if key not in by_name:
        by_name[key] = r
for (name, ident), r in by_name.items():
    tex = r.get("Textures") or r.get("TextureIds") or r.get("textures") or ""
    mesh = r.get("Meshes") or r.get("meshes") or ""
    md = r.get("MonsterData") or r.get("monsterdata") or ""
    hm = r.get("HeadMesh") or r.get("headmesh") or ""
    pos = (r.get("Position") or r.get("X") or "")
    p(f"  {name} | {ident} | md={md} head={hm} pos={str(pos)[:60]}")
    p(f"    tex={str(tex)[:160]}")
    p(f"    mesh={str(mesh)[:160]}")

p("\n=== enemy-dossier names ===")
dossier = json.loads((CAP / "enemy-dossier.json").read_text(encoding="utf-8-sig"))
if isinstance(dossier, dict):
    ents = dossier.get("enemies") or dossier.get("entities") or dossier.get("npcs") or dossier
    if isinstance(ents, list):
        for e in ents[:50]:
            p(e)
    elif isinstance(ents, dict):
        for k, v in list(ents.items())[:50]:
            p(k, v if not isinstance(v, dict) else {kk: v.get(kk) for kk in list(v)[:12]})

p("\n=== event keyword counts ===")
ev = (CAP / "events.log").read_text(encoding="utf-8-sig", errors="replace")
for pat in ["Rex", "Marcus", "Markus", "Stone", "Mongo", "Slam", "KnuBot",
            "CastNano", "InfoRequest", "LookAt", "HealthDamage", "Area", "FollowTarget"]:
    c = len(re.findall(pat, ev, re.I))
    if c:
        p(f"  {pat}: {c}")

p("\n=== CastNano / Mongo / HealthDamage lines ===")
for line in ev.splitlines():
    if re.search(r"Mongo|Slam|CastNano|SpellList|HealthDamage|AreaCast|FinishNano", line, re.I):
        p(line[:300])

p("\n=== LookAt OUT targets ===")
for line in ev.splitlines():
    if "OUT" in line and ("LookAt" in line or "InfoRequest" in line or "KnuBot" in line):
        p(line[:280])

p("\n=== movement-summary ===")
ms = (CAP / "movement-summary.json").read_text(encoding="utf-8-sig", errors="replace")
p(ms[:4000])

OUT.write_text("\n".join(lines), encoding="utf-8")
print("wrote", OUT, "chars", len("\n".join(lines)))
