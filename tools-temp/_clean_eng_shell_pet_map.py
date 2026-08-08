import csv
import json
from collections import defaultdict

m = json.load(open(r"tools-temp\_eng_shell_pet_map.json", encoding="utf-8"))
rows = list(
    csv.DictReader(
        open(
            r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260808-131854\scfu-appearance.csv",
            encoding="utf-8-sig",
        )
    )
)
by_id = {r["Identity"]: r for r in rows}
for c in m["cycles"]:
    ident = c.get("petIdent") or ""
    if ident and not ident.startswith("("):
        ident = "(" + ident + ")"
    r = by_id.get(ident)
    if not r:
        continue
    c["petName"] = r["Name"]
    c["petLevel"] = int(r["Level"])
    c["health"] = int(r["Health"])
    c["monsterData"] = int(r["MonsterData"])
    c["monsterScale"] = int(r["MonsterScale"])
    c["runSpeedBase"] = int(r["RunSpeedBase"] or 0)
    c["npcFamily"] = int(r["NpcFamily"] or 0)
    c["visualFlags"] = int(r["VisualFlags"] or 0)

json.dump(m, open(r"tools-temp\_eng_shell_pet_map.json", "w", encoding="utf-8"), indent=2)

lines = []
lines.append("Capture: 20260808-131854 | Char: Engnera | Pets: %d" % len(m["cycles"]))
lines.append(
    "Flow: CastNano -> FinishNanoCasting -> TemplateAction shell(Overflow) -> Use shell -> SCFU IsPet -> Delete shell"
)
lines.append("")
lines.append("NANO | SHELL low/high QL | PET name | LVL | HP | MD | SCALE | RUN")
for c in sorted(m["cycles"], key=lambda x: (x.get("petName") or "", x.get("petLevel") or 0)):
    lines.append(
        "%s | %s/%s QL%s | %s | %s | %s | %s | %s | %s"
        % (
            c.get("castNano"),
            c.get("shellLowId"),
            c.get("shellHighId"),
            c.get("shellQl"),
            c.get("petName"),
            c.get("petLevel"),
            c.get("health"),
            c.get("monsterData"),
            c.get("monsterScale"),
            c.get("runSpeedBase"),
        )
    )

fam = defaultdict(list)
for c in m["cycles"]:
    fam[c["petName"]].append(c)
lines.append("")
lines.append("BY PET TYPE:")
for name, lst in sorted(fam.items()):
    md = sorted({c["monsterData"] for c in lst})
    lv = sorted(c["petLevel"] for c in lst)
    lines.append("- %s x%d | md=%s | levels=%s" % (name, len(lst), md, lv))

text = "\n".join(lines)
open(r"tools-temp\_eng_shell_pet_map_clean.txt", "w", encoding="utf-8").write(text)
print(text)
