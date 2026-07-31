import pathlib, csv, binascii, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-162433")
out = []

for name in ["chat-dialogue.log", "events.log"]:
    f = p / name
    out.append("=== " + name)
    if not f.exists():
        out.append("MISSING")
        continue
    text = f.read_text(encoding="utf-8-sig", errors="replace")
    for line in text.splitlines():
        if any(k in line for k in ("Charge", "follow you", "Bureaucrat", "pet,", "NpcMessage", "NpcMessage", "PetCommand", "Attack", "StopFight", "SpecialAttack")):
            out.append(line[:450])

out.append("=== enemy-combat pet rows")
ec = p / "enemy-combat.csv"
if ec.exists():
    with ec.open(encoding="utf-8-sig", newline="") as fh:
        r = csv.DictReader(fh)
        cols = r.fieldnames or []
        out.append("cols=" + ",".join(cols[:18]))
        for row in r:
            blob = "|".join((row.get(c) or "") for c in cols)
            mt = row.get("MessageType") or ""
            if mt in ("SpecialAttackWeapon", "Attack", "AttackInfo", "StopFight", "FollowTarget", "PetCommand") or "BUW1" in blob or "42555731" in blob:
                # keep all combat types; filter later by identity if needed
                src = row.get("SourceIdentity") or ""
                # include all SAW/Attack/AttackInfo/StopFight
                if mt in ("SpecialAttackWeapon", "Attack", "AttackInfo", "StopFight"):
                    out.append("%s %s src=%s tgt=%s amt=%s detail=%s" % (
                        row.get("CapturedUtc"), mt, src, row.get("TargetIdentity"), row.get("Amount"),
                        (row.get("RawDetail") or "")[:180]))

out.append("=== raw PetCommand + SAW + Attack + AttackInfo + StopFight + chat text")
needles = [b"Charge!", b"follow you wherever", b"Catcraty"]
with (p/"raw-packets.csv").open(encoding="utf-8-sig", newline="") as fh:
    r = csv.DictReader(fh)
    n = 0
    for row in r:
        name = row.get("N3TypeName") or ""
        hx = row.get("RawHex") or ""
        keep = name in ("PetCommand", "SpecialAttackWeapon", "Attack", "AttackInfo", "StopFight")
        try:
            raw = binascii.unhexlify(hx) if hx else b""
        except Exception:
            raw = b""
        if any(x in raw for x in needles):
            keep = True
        if keep:
            # filter to pet-looking SpecialAttackWeapon/Attack by scanning hex for common pet instances later
            out.append("%s %s %s hex=%s" % (row.get("CapturedUtc"), row.get("Direction"), name, hx[:160]))
            n += 1
            if n > 200:
                break

path = p / "_pet_fresh_scan.txt"
path.write_text("\n".join(out), encoding="utf-8")
print("wrote", path, "lines", len(out))
