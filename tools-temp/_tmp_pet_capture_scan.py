import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

p = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-151431")
out = p / "_pet_scan.txt"
keys = ("charge", "follow you", "bureaucrat", "pet,", " xp", "loot", "petcommand", "follow", "attack", "wait", "guard", "behind")

lines_out = []
def emit(s):
    lines_out.append(s)

for name in ["chat-dialogue.log", "system-messages.log", "events.log", "npc-interactions.log"]:
    f = p / name
    emit("=== %s size %s" % (name, f.stat().st_size if f.exists() else "missing"))
    if not f.exists():
        continue
    text = f.read_text(encoding="utf-8-sig", errors="replace")
    for line in text.splitlines():
        low = line.lower()
        if any(k in low for k in keys):
            emit(line[:500])

emit("=== enemy-combat header + worker rows")
ec = p / "enemy-combat.csv"
if ec.exists():
    lines = ec.read_text(encoding="utf-8-sig", errors="replace").splitlines()
    if lines:
        emit(lines[0][:500])
    for line in lines[1:]:
        if any(x in line for x in ("79AA2FEE", "BUW1", "42555731", "PetCommand", "SpecialAttackWeapon", "AttackInfo", "StopFight")):
            emit(line[:600])

emit("=== raw PetCommand / NpcMessage samples")
raw = p / "raw-packets.csv"
if raw.exists():
    for i, line in enumerate(raw.read_text(encoding="utf-8-sig", errors="replace").splitlines()):
        if i == 0:
            emit(line[:400])
            continue
        low = line.lower()
        if "petcommand" in low or "npcmessage" in low or "chattext" in low:
            if "79aa2fee" in low or "bureaucrat" in low or "charge" in low or "follow you" in low or "petcommand" in low:
                emit(line[:700])

out.write_text("\n".join(lines_out), encoding="utf-8")
print("wrote", out, "lines", len(lines_out))
