# Extract NPC/mobs from 20260720-goldman and emit AreteLandingSpawn fragments.
import csv
import re
from pathlib import Path
from collections import OrderedDict

CAP = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260720-goldman")
OUT = Path(r"tools-temp/_tmp_goldman_spawn_npcs.csfrag")
SUMMARY = Path(r"tools-temp/_tmp_goldman_spawn_summary.txt")

# Already owned by dedicated runtimes / existing AreteLandingSpawn quest set.
SKIP_NAMES = re.compile(
    r"^(Malfunctioning |Burning )?Cleaning Robot$|^Cleanmeister",
    re.I,
)
# Skip other players' pets (HasOwner) — do not spawn remote-player pets.
SKIP_IF_OWNER = True

BREED = {"Solitus": 1, "Opifex": 2, "Nanomage": 3, "Atrox": 4, "Human": 1, "HumanMonster": 5, "Monster": 6, "Robot": 7}
GENDER = {"Male": 2, "Female": 3, "Uni": 1, "Neuter": 1, "None": 1, "Unknown": 1}
SIDE = {"Neutral": 0, "Clan": 1, "Omni": 2, "OmniTek": 2, "Monster": 3, "None": 0}
FATNESS = {"Normal": 1, "Thin": 0, "Fat": 2}


def i(v, d=0):
    try:
        return int(float(v)) if v not in (None, "") else d
    except Exception:
        return d


def f(v, d=0.0):
    try:
        return float(v) if v not in (None, "") else d
    except Exception:
        return d


def parse_tex(s):
    if not s:
        return None
    out = []
    for part in s.split("|"):
        bits = part.split(":")
        if len(bits) >= 2:
            out.append([int(bits[0]), int(bits[1])])
    return out or None


def parse_mesh(s):
    if not s:
        return None
    out = []
    for part in s.split("|"):
        bits = part.split(":")
        if len(bits) >= 4:
            out.append([int(bits[0]), int(bits[1]), int(bits[2]), int(bits[3])])
        elif len(bits) == 3:
            out.append([int(bits[0]), int(bits[1]), int(bits[2]), 0])
    return out or None


with open(CAP / "scfu-appearance.csv", newline="", encoding="utf-8-sig") as fh:
    scfu = list(csv.DictReader(fh))

by_id = OrderedDict()
for r in scfu:
    name = (r.get("Name") or "").strip()
    if not name or SKIP_NAMES.match(name):
        continue
    ctype = (r.get("CharacterInfoType") or "").strip()
    flags = r.get("Flags") or ""
    if ctype == "PlayerInfo":
        continue
    if "IsNpc" not in flags and ctype != "NPCInfo":
        continue
    m = re.search(r"SimpleChar:([0-9A-Fa-f]+)", r.get("Identity") or "")
    if not m:
        continue
    iid = m.group(1).upper()
    x, y, z = f(r.get("PositionX")), f(r.get("PositionY")), f(r.get("PositionZ"))
    if abs(x) < 0.01 and abs(z) < 0.01:
        continue
    owner = (r.get("Owner") or "").strip()
    if SKIP_IF_OWNER and owner and owner not in ("", "0", "(None:0)", "(None:0000)"):
        # Still keep non-pet NPCs; owner field on pets looks like SimpleChar:...
        if "HasOwner" in (r.get("Flags2") or "") or "HasOwner" in flags:
            continue
    if iid in by_id:
        continue
    by_id[iid] = r

# Also pull combat mobs from enemy-dossier if missing
import json

dossier_path = CAP / "enemy-dossier.json"
if dossier_path.exists():
    dossier = json.loads(dossier_path.read_text(encoding="utf-8-sig"))
    for e in dossier.get("enemies") or []:
        name = (e.get("name") or "").strip()
        if not name or SKIP_NAMES.match(name):
            continue
        m = re.search(r"SimpleChar:([0-9A-Fa-f]+)", e.get("identity") or "")
        if not m:
            continue
        iid = m.group(1).upper()
        if iid in by_id:
            continue
        pos = e.get("position") or {}
        x, y, z = f(pos.get("x")), f(pos.get("y")), f(pos.get("z"))
        if abs(x) < 0.01 and abs(z) < 0.01:
            continue
        # synthesize minimal row
        by_id[iid] = {
            "Name": name,
            "Identity": e.get("identity"),
            "Level": e.get("level") or 1,
            "Health": e.get("maxHealth") or e.get("currentHealth") or 100,
            "MonsterData": e.get("monsterData") or 0,
            "MonsterScale": e.get("monsterScale") or 100,
            "VisualFlags": e.get("visualFlags") or 31,
            "HeadMesh": e.get("headMesh") or 0,
            "RunSpeedBase": e.get("runSpeed") or 20,
            "NpcFamily": e.get("npcFamily") or 0,
            "NpcLosHeight": e.get("losHeight") or 0,
            "CharacterFlags": 268964353,
            "AppearanceValue": 1576,
            "Side": "Neutral",
            "Breed": "Monster",
            "Gender": "Uni",
            "Race": 1,
            "Fatness": "Normal",
            "PositionX": x,
            "PositionY": y,
            "PositionZ": z,
            "HeadingX": 0,
            "HeadingY": 0,
            "HeadingZ": 0,
            "HeadingW": 1,
            "Textures": "",
            "Meshes": "",
            "_from": "dossier",
        }

lines = []
summary = []
for iid, r in sorted(by_id.items(), key=lambda kv: ((kv[1].get("Name") or ""), kv[0])):
    name = (r.get("Name") or "").strip()
    tex = parse_tex(r.get("Textures") or "")
    mesh = parse_mesh(r.get("Meshes") or "")
    level = i(r.get("Level"), 1)
    health = i(r.get("Health"), 100)
    md = i(r.get("MonsterData"))
    scale = i(r.get("MonsterScale"), 100) or 100
    vf = i(r.get("VisualFlags"), 31) or 31
    head = i(r.get("HeadMesh"))
    run = i(r.get("RunSpeedBase") or r.get("RunSpeed"), 20)
    fam = i(r.get("NpcFamily"), 0)
    los = i(r.get("NpcLosHeight") or r.get("LosHeight"), 0)
    cflags = i(r.get("CharacterFlags"), 268964353) or 268964353
    app = i(r.get("AppearanceValue"), 1576)
    side = SIDE.get(str(r.get("Side") or ""), i(r.get("Side"), 0))
    breed = BREED.get(str(r.get("Breed") or ""), i(r.get("Breed"), 1))
    gender = GENDER.get(str(r.get("Gender") or ""), i(r.get("Gender"), 2))
    race = i(r.get("Race"), 1) or 1
    fat = FATNESS.get(str(r.get("Fatness") or ""), i(r.get("Fatness"), 1)) or 1
    x, y, z = f(r.get("PositionX")), f(r.get("PositionY")), f(r.get("PositionZ"))
    hx, hy, hz, hw = f(r.get("HeadingX")), f(r.get("HeadingY")), f(r.get("HeadingZ")), f(r.get("HeadingW"), 1.0)
    src = r.get("_from") or "scfu"
    summary.append(f"{iid}\t{name}\tL{level}\tmd={md}\t{x:.2f},{y:.2f},{z:.2f}\t{src}")

    lines.append("            new AreteNpc")
    lines.append("            {")
    lines.append(f"                // Capture 20260720-goldman {iid} ({src})")
    lines.append(f"                CaptureInstance = unchecked((int)0x{iid}),")
    # Dialogue/tip text uses Stan Goodman; live SCFU name is Stanley Goodman.
    display = "Stan Goodman" if name == "Stanley Goodman" else name
    lines.append(f"                Name = \"{display}\",")
    lines.append(
        f"                Level = {level}, Health = {health}, MonsterData = {md}, Scale = {scale}, VisualFlags = {vf}, HeadMesh = {head}, RunSpeed = {run},"
    )
    lines.append(
        f"                NpcFamily = {fam}, LosHeight = {los}, CharacterFlags = {cflags}, AppearanceValue = {app},"
    )
    lines.append(
        f"                Side = {side}, Breed = {breed}, Gender = {gender}, Race = {race}, Fatness = {fat}, MovementMode = 3,"
    )
    lines.append(f"                X = {x}f, Y = {y}f, Z = {z}f,")
    lines.append(f"                Hx = {hx}f, Hy = {hy}f, Hz = {hz}f, Hw = {hw}f,")
    if tex:
        t = ", ".join(f"new[] {{ {a}, {b} }}" for a, b in tex)
        lines.append(f"                Textures = new[] {{ {t} }},")
    else:
        lines.append(
            "                Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },"
        )
    if mesh:
        mtxt = ", ".join(f"new[] {{ {a}, {b}, {c}, {d} }}" for a, b, c, d in mesh)
        lines.append(f"                Meshes = new[] {{ {mtxt} }},")
    else:
        lines.append("                Meshes = null,")
    lines.append("            },")

OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
SUMMARY.write_text(f"count={len(by_id)}\n" + "\n".join(summary) + "\n", encoding="utf-8")
print(f"wrote {OUT} count={len(by_id)}")
print(f"wrote {SUMMARY}")
for s in summary:
    print(s)
