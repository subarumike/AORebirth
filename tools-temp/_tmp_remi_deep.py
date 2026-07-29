# Deep Remi Gallois capture decode -> UTF-8 out
import csv
import os
import re
import binascii
from collections import OrderedDict

cap = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260727-204902"
out = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_remi_deep.txt"

def w(f, *a):
    f.write(" ".join(str(x) for x in a) + "\n")

with open(out, "w", encoding="utf-8") as f:
    # Remi identity from SCFU
    remi_ids = set()
    with open(os.path.join(cap, "scfu-appearance.csv"), encoding="utf-8-sig", newline="") as cf:
        for row in csv.DictReader(cf):
            name = row.get("Name") or ""
            if "Remi" in name or "Gallois" in name:
                remi_ids.add(row.get("Identity"))
                w(f, "SCFU", row.get("Identity"), name, "pf", row.get("PlayfieldId"),
                  "xyz", row.get("PositionX"), row.get("PositionY"), row.get("PositionZ"),
                  "md", row.get("MonsterData"), "lvl", row.get("Level"), "hp", row.get("Health"),
                  "side", row.get("Side"), "hm", row.get("HeadMesh"),
                  "tex", (row.get("Textures") or "")[:80],
                  "mesh", (row.get("Meshes") or "")[:80])

    w(f, "\n==== chat-dialogue ====")
    chat = open(os.path.join(cap, "chat-dialogue.log"), encoding="utf-8-sig", errors="replace").read()
    f.write(chat)
    w(f, "\n==== npc-interactions (filter Remi/KnuBot/Quest) ====")
    for ln in open(os.path.join(cap, "npc-interactions.log"), encoding="utf-8-sig", errors="replace"):
        if any(k in ln for k in ("Remi", "Gallois", "KnuBot", "Quest", "Tip", "Mission", "FormatFeedback", "TemplateAction", "Inventory", "XP", "credit")):
            f.write(ln)

    w(f, "\n==== mission-flow ====")
    f.write(open(os.path.join(cap, "mission-flow.log"), encoding="utf-8-sig", errors="replace").read())

    w(f, "\n==== system-messages ====")
    f.write(open(os.path.join(cap, "system-messages.log"), encoding="utf-8-sig", errors="replace").read())

    w(f, "\n==== inventory-updates ====")
    with open(os.path.join(cap, "inventory-updates.csv"), encoding="utf-8-sig", newline="") as cf:
        for row in csv.DictReader(cf):
            f.write(str(dict(row)) + "\n")

    w(f, "\n==== events DETAIL Remi/KnuBot/Quest/Feedback/Template ====")
    keys = ("Remi", "Gallois", "KnuBot", "Knubot", "Quest", "FormatFeedback", "TemplateAction",
            "Mission", "Tip", "Trade", "Finish", "OpenChat", "CloseChat", "Answer")
    for ln in open(os.path.join(cap, "events.log"), encoding="utf-8", errors="replace"):
        if "DETAIL" not in ln and "IN-N3" not in ln and "OUT-N3" not in ln:
            # still keep DETAIL-ish
            pass
        if any(k in ln for k in keys):
            f.write(ln[:500] + ("\n" if not ln.endswith("\n") else ""))

print("wrote", out)
