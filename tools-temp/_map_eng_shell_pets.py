# Correlate Engineer CastNano -> shell TemplateAction -> Use -> pet SCFU
import csv
import json
import os
import re
from collections import defaultdict

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260808-131854"
OUT = r"tools-temp\_eng_shell_pet_map.json"
OUT_TXT = r"tools-temp\_eng_shell_pet_map.txt"

# Parse events into timeline entries
cast_re = re.compile(
    r"CharacterActionMessage \{ Action=CastNano .*? Parameter2=(?P<nano>\d+)",
)
finish_re = re.compile(
    r"CharacterActionMessage \{ Action=FinishNanoCasting .*? Parameter2=(?P<nano>\d+)",
)
shell_create_re = re.compile(
    r"TemplateActionMessage \{ ItemLowId=(?P<low>\d+) ItemHighId=(?P<high>\d+) Quality=(?P<ql>\d+) "
    r"Unknown1=(?P<u1>\d+) Unknown2=(?P<u2>\d+) Placement=\(OverflowWindow:0000\)",
)
shell_consume_re = re.compile(
    r"TemplateActionMessage \{ ItemLowId=(?P<low>\d+) ItemHighId=(?P<high>\d+) Quality=(?P<ql>\d+) "
    r"Unknown1=(?P<u1>\d+) Unknown2=(?P<u2>\d+) Placement=\(Inventory:",
)
use_out_re = re.compile(
    r"\[OUT-N3-DETAIL\] GenericCmdMessage \{ .*? Action=Use .*? Target=\(Inventory:(?P<slot>[0-9A-Fa-f]+)\)",
)
scfu_re = re.compile(
    r'Name="(?P<name>[^"]+)".*? Level=(?P<level>\d+) Health=(?P<health>\d+) .*? MonsterData=(?P<md>\d+) '
    r"MonsterScale=(?P<scale>\d+) VisualFlags=(?P<vf>\d+)",
)
dynel_re = re.compile(
    r"\[DYNEL-SPAWNED\] identity=\((?P<ident>[^)]+)\) name=(?P<name>[^,]+) .*? hp=(?P<hp>\d+)/\d+ .*? level=(?P<level>\d+) .*? monsterData=(?P<md>\d+)",
)
charseen_pet_re = re.compile(
    r"\[CHAR-SEEN\] identity=\((?P<ident>[^)]+)\) name=(?P<name>[^,]+) .*? pet=True .*? hp=(?P<hp>\d+)/\d+ .*? level=(?P<level>\d+) .*? monsterData=(?P<md>\d+)",
)

events = []
with open(os.path.join(CAP, "events.log"), encoding="utf-8", errors="replace") as f:
    for i, line in enumerate(f, 1):
        ts = line[:28] if len(line) > 28 else ""
        m = cast_re.search(line)
        if m and "[OUT-N3" in line:
            events.append({"i": i, "ts": ts, "kind": "cast", "nano": int(m.group("nano")), "raw": line.strip()[:300]})
            continue
        m = finish_re.search(line)
        if m:
            events.append({"i": i, "ts": ts, "kind": "finish", "nano": int(m.group("nano")), "raw": line.strip()[:300]})
            continue
        m = shell_create_re.search(line)
        if m:
            events.append({
                "i": i, "ts": ts, "kind": "shell_create",
                "low": int(m.group("low")), "high": int(m.group("high")), "ql": int(m.group("ql")),
                "u1": int(m.group("u1")), "u2": int(m.group("u2")),
                "raw": line.strip()[:300],
            })
            continue
        m = shell_consume_re.search(line)
        if m:
            events.append({
                "i": i, "ts": ts, "kind": "shell_consume",
                "low": int(m.group("low")), "high": int(m.group("high")), "ql": int(m.group("ql")),
                "u1": int(m.group("u1")), "u2": int(m.group("u2")),
                "raw": line.strip()[:300],
            })
            continue
        m = use_out_re.search(line)
        if m:
            events.append({"i": i, "ts": ts, "kind": "use_out", "slot": m.group("slot"), "raw": line.strip()[:300]})
            continue
        m = dynel_re.search(line)
        if m and "Engineer" in m.group("name"):
            events.append({
                "i": i, "ts": ts, "kind": "dynel",
                "ident": m.group("ident"), "name": m.group("name").strip(),
                "hp": int(m.group("hp")), "level": int(m.group("level")), "md": int(m.group("md")),
                "raw": line.strip()[:300],
            })
            continue
        m = charseen_pet_re.search(line)
        if m and "Engineer" in m.group("name"):
            events.append({
                "i": i, "ts": ts, "kind": "pet_seen",
                "ident": m.group("ident"), "name": m.group("name").strip(),
                "hp": int(m.group("hp")), "level": int(m.group("level")), "md": int(m.group("md")),
                "raw": line.strip()[:300],
            })
            continue
        if "SimpleCharFullUpdateMessage" in line and 'Name="Engineer' in line:
            m = scfu_re.search(line)
            if m:
                # identity from nearby earlier SMOKE/IN line? extract from previous pattern in same line if present
                ident_m = re.search(r"Identity=\((SimpleChar:[0-9A-Fa-f]+)\)", line)
                events.append({
                    "i": i, "ts": ts, "kind": "scfu",
                    "ident": ident_m.group(1) if ident_m else "",
                    "name": m.group("name"),
                    "level": int(m.group("level")),
                    "health": int(m.group("health")),
                    "md": int(m.group("md")),
                    "scale": int(m.group("scale")),
                    "vf": int(m.group("vf")),
                    "raw": line.strip()[:350],
                })

# Load SCFU CSV for complete pet rows keyed by identity
scfu_rows = list(csv.DictReader(open(os.path.join(CAP, "scfu-appearance.csv"), encoding="utf-8-sig")))
scfu_by_ident = {r["Identity"].strip("()"): r for r in scfu_rows}
# also with parentheses
for r in scfu_rows:
    scfu_by_ident[r["Identity"]] = r

# Correlate cycles: cast -> finish(same nano) -> shell_create -> use_out -> scfu/dynel
cycles = []
pending_cast = None
pending_finish = None
pending_shell = None
pending_use = None

for e in events:
    kind = e["kind"]
    if kind == "cast":
        pending_cast = e
        pending_finish = None
        pending_shell = None
        pending_use = None
    elif kind == "finish":
        pending_finish = e
    elif kind == "shell_create":
        pending_shell = e
    elif kind == "use_out":
        pending_use = e
    elif kind in ("dynel", "scfu", "pet_seen"):
        # close a cycle when we see the pet after a use
        if pending_use is None and pending_shell is None and pending_finish is None:
            continue
        # Prefer dynel/scfu once per cycle
        if kind == "pet_seen" and cycles and cycles[-1].get("petIdent") == e.get("ident"):
            continue
        if kind == "scfu" and cycles and cycles[-1].get("petIdent") and cycles[-1].get("petName") == e.get("name") and cycles[-1].get("petLevel") == e.get("level"):
            # enrich scale
            if not cycles[-1].get("monsterScale"):
                cycles[-1]["monsterScale"] = e.get("scale")
                cycles[-1]["visualFlags"] = e.get("vf")
                cycles[-1]["health"] = e.get("health")
            continue
        if kind == "dynel" and cycles and cycles[-1].get("petIdent") == e.get("ident"):
            continue

        cycle = {
            "castNano": pending_finish["nano"] if pending_finish else (pending_cast["nano"] if pending_cast else None),
            "castTs": pending_cast["ts"] if pending_cast else None,
            "finishTs": pending_finish["ts"] if pending_finish else None,
            "shellLowId": pending_shell["low"] if pending_shell else None,
            "shellHighId": pending_shell["high"] if pending_shell else None,
            "shellQl": pending_shell["ql"] if pending_shell else None,
            "shellTemplateUnknown2": pending_shell["u2"] if pending_shell else None,
            "useSlot": pending_use["slot"] if pending_use else None,
            "useTs": pending_use["ts"] if pending_use else None,
            "petIdent": e.get("ident"),
            "petName": e.get("name"),
            "petLevel": e.get("level"),
            "petHp": e.get("hp") or e.get("health"),
            "monsterData": e.get("md"),
            "monsterScale": e.get("scale"),
            "visualFlags": e.get("vf"),
            "sourceKind": kind,
            "eventIndex": e["i"],
        }
        # enrich from CSV
        ident_key = e.get("ident") or ""
        row = scfu_by_ident.get(ident_key) or scfu_by_ident.get("(" + ident_key + ")")
        if row:
            cycle["monsterScale"] = int(row["MonsterScale"] or 0)
            cycle["visualFlags"] = int(row["VisualFlags"] or 0)
            cycle["runSpeedBase"] = int(row["RunSpeedBase"] or 0)
            cycle["npcFamily"] = int(row["NpcFamily"] or 0)
            cycle["health"] = int(row["Health"] or 0)
            cycle["flags"] = row.get("Flags")
            cycle["breed"] = row.get("Breed")
            cycle["side"] = row.get("Side")
            cycle["textures"] = row.get("Textures")
            cycle["playfieldId"] = row.get("PlayfieldId")
            cycle["pos"] = [float(row["PositionX"]), float(row["PositionY"]), float(row["PositionZ"])]
        cycles.append(cycle)
        # reset use so next pet needs new use; keep shell cleared after consume typically
        pending_use = None
        pending_shell = None
        # keep cast/finish until next cast

# Also map unique nano->shell from finish+shell_create pairs regardless of use
nano_shells = {}
last_finish_nano = None
for e in events:
    if e["kind"] == "finish":
        last_finish_nano = e["nano"]
    elif e["kind"] == "shell_create" and last_finish_nano is not None:
        nano_shells.setdefault(last_finish_nano, [])
        entry = {"low": e["low"], "high": e["high"], "ql": e["ql"], "u2": e["u2"], "ts": e["ts"]}
        if entry not in nano_shells[last_finish_nano]:
            nano_shells[last_finish_nano].append(entry)

# Deduplicate cycles by petIdent
seen = set()
unique_cycles = []
for c in cycles:
    key = c.get("petIdent") or (c.get("petName"), c.get("petLevel"), c.get("eventIndex"))
    if key in seen:
        continue
    seen.add(key)
    unique_cycles.append(c)

# Summary tables
by_nano = defaultdict(list)
for c in unique_cycles:
    by_nano[c.get("castNano")].append(c)

shell_to_pet = defaultdict(list)
for c in unique_cycles:
    sk = (c.get("shellLowId"), c.get("shellHighId"), c.get("shellQl"))
    shell_to_pet[sk].append({
        "nano": c.get("castNano"),
        "petName": c.get("petName"),
        "level": c.get("petLevel"),
        "md": c.get("monsterData"),
        "scale": c.get("monsterScale"),
        "hp": c.get("health") or c.get("petHp"),
        "run": c.get("runSpeedBase"),
        "ident": c.get("petIdent"),
    })

result = {
    "capture": "20260808-131854",
    "character": "Engnera",
    "petScfuCount": len(scfu_rows),
    "correlatedCycles": len(unique_cycles),
    "nanoToShells": {str(k): v for k, v in sorted(nano_shells.items())},
    "cycles": unique_cycles,
    "shellToPets": {("%s/%s QL%s" % (k[0], k[1], k[2])): v for k, v in sorted(shell_to_pet.items(), key=lambda x: (x[0][0] or 0, x[0][2] or 0))},
}

with open(OUT, "w", encoding="utf-8") as f:
    json.dump(result, f, indent=2)

# Human summary
txt = []
txt.append("Capture 20260808-131854 Engineer shell/pet map (Engnera)")
txt.append("SCFU pets: %d | correlated cycles: %d" % (len(scfu_rows), len(unique_cycles)))
txt.append("")
txt.append("=== NANO -> SHELL (from FinishNanoCasting + Overflow TemplateAction) ===")
for nano, shells in sorted(nano_shells.items()):
    for s in shells:
        txt.append("nano %s -> shell low=%s high=%s ql=%s (templateUnknown2=%s) @ %s" % (
            nano, s["low"], s["high"], s["ql"], s["u2"], s["ts"]))
txt.append("")
txt.append("=== SHELL USE -> PET SPAWN ===")
txt.append("flow: CastNano -> FinishNanoCasting -> TemplateAction(Overflow shell) -> ContainerAddItem -> GenericCmd Use(Inventory) -> SCFU IsPet -> TemplateAction+DeleteItem(consume shell)")
txt.append("")
for c in unique_cycles:
    txt.append(
        "nano=%s | shell=%s/%s QL%s | pet=%s lvl=%s hp=%s md=%s scale=%s run=%s family=%s | %s"
        % (
            c.get("castNano"), c.get("shellLowId"), c.get("shellHighId"), c.get("shellQl"),
            c.get("petName"), c.get("petLevel"), c.get("health") or c.get("petHp"),
            c.get("monsterData"), c.get("monsterScale"), c.get("runSpeedBase"), c.get("npcFamily"),
            c.get("petIdent"),
        )
    )

# Unique nano profiles
txt.append("")
txt.append("=== UNIQUE NANO PROFILES (first shell + pet range) ===")
for nano, lst in sorted(by_nano.items(), key=lambda kv: (kv[0] is None, kv[0] or 0)):
    shells = sorted({(c.get("shellLowId"), c.get("shellHighId"), c.get("shellQl")) for c in lst})
    pets = sorted({(c.get("petName"), c.get("petLevel"), c.get("monsterData"), c.get("monsterScale")) for c in lst})
    txt.append("nano %s shells=%s pets=%s count=%d" % (nano, shells, pets, len(lst)))

with open(OUT_TXT, "w", encoding="utf-8") as f:
    f.write("\n".join(txt))

print("cycles", len(unique_cycles))
print("nanos", sorted(nano_shells.keys()))
print("Wrote", OUT_TXT)
print("\n".join(txt[:80]))
print("...")
print("\n".join(txt[-40:]))
