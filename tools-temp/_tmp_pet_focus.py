import pathlib
import csv
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-151431")
out = []

# Focused chat-like lines
for name in ["chat-dialogue.log", "system-messages.log", "events.log"]:
    f = p / name
    out.append("=== " + name)
    text = f.read_text(encoding="utf-8-sig", errors="replace")
    for line in text.splitlines():
        if any(x in line for x in ("Charge", "follow you", "Bureaucrat Worker", "received", "loot", "attacked by", "master")):
            out.append(line)

# enemy-combat: unique message types for pet identity
ec = p / "enemy-combat.csv"
out.append("=== enemy-combat types for 79AA2FEE / BUW1")
with ec.open(encoding="utf-8-sig", newline="") as fh:
    reader = csv.DictReader(fh)
    cols = reader.fieldnames or []
    out.append("cols=" + ",".join(cols[:20]))
    counts = {}
    samples = []
    for row in reader:
        blob = "|".join((row.get(c) or "") for c in cols)
        if "79AA2FEE" not in blob and "42555731" not in blob and "BUW1" not in blob:
            continue
        # try common type fields
        mtype = row.get("MessageType") or row.get("messageType") or row.get("Type") or row.get("PacketType") or ""
        if not mtype:
            # guess from blob
            for cand in ("SpecialAttackWeapon","AttackInfo","Attack","StopFight","FollowTarget","PetCommand","Stat","NpcMessage","ChatText"):
                if cand in blob:
                    mtype = cand
                    break
        counts[mtype or "unknown"] = counts.get(mtype or "unknown", 0) + 1
        if len(samples) < 40:
            samples.append(blob[:350])
    for k,v in sorted(counts.items(), key=lambda kv: -kv[1]):
        out.append("%s\t%d" % (k,v))
    out.append("--- samples ---")
    out.extend(samples)

# raw packets: PetCommand and NpcMessage with Charge/follow
raw = p / "raw-packets.csv"
out.append("=== raw PetCommand and pet NpcMessage")
with raw.open(encoding="utf-8-sig", newline="") as fh:
    reader = csv.DictReader(fh)
    cols = reader.fieldnames or []
    out.append("cols=" + ",".join(cols[:25]))
    n = 0
    for row in reader:
        blob = "|".join((row.get(c) or "") for c in cols)
        low = blob.lower()
        keep = False
        if "petcommand" in low:
            keep = True
        if "npcmessage" in low and ("charge" in low or "follow you" in low or "bureaucrat" in low or "master" in low):
            keep = True
        if "chattext" in low and ("charge" in low or "follow you" in low or "bureaucrat worker" in low):
            keep = True
        if keep:
            out.append(blob[:800])
            n += 1
            if n > 80:
                break

path = p / "_pet_focus.txt"
path.write_text("\n".join(out), encoding="utf-8")
print("wrote", path, "lines", len(out))
