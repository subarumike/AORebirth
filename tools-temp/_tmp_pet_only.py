import pathlib, csv, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-162433")
PET = "79AA2FEE"
out = []

out.append("=== PET-ONLY combat")
with (p/"enemy-combat.csv").open(encoding="utf-8-sig", newline="") as fh:
    r = csv.DictReader(fh)
    prev = None
    for row in r:
        src = row.get("SourceIdentity") or ""
        if PET not in src:
            continue
        ts = row.get("CapturedUtc")
        mt = row.get("MessageType")
        detail = row.get("RawDetail") or ""
        out.append("%s %s tgt=%s amt=%s" % (ts, mt, row.get("TargetIdentity"), row.get("Amount")))
        out.append("  " + detail[:300])
        if prev and mt == "AttackInfo":
            # delta
            pass
        prev = ts

out.append("=== PetCommand")
with (p/"raw-packets.csv").open(encoding="utf-8-sig", newline="") as fh:
    r = csv.DictReader(fh)
    for row in r:
        if row.get("N3TypeName") == "PetCommand":
            hx = row.get("RawHex") or ""
            # command id near end: look for 00000007 attack / 00000001 follow
            out.append("%s hex=%s" % (row.get("CapturedUtc"), hx))

out.append("=== DesiredTargetDistance / StopFight for pet")
with (p/"system-messages.log").open(encoding="utf-8-sig", errors="replace") as fh:
    for line in fh:
        if PET in line and ("DesiredTargetDistance" in line or "StopFight" in line or "Follow" in line):
            out.append(line[:280].rstrip())

# FollowTarget for pet
out.append("=== FollowTarget involving pet")
with (p/"raw-packets.csv").open(encoding="utf-8-sig", newline="") as fh:
    r = csv.DictReader(fh)
    n = 0
    for row in r:
        if row.get("N3TypeName") != "FollowTarget":
            continue
        hx = row.get("RawHex") or ""
        if PET.lower() not in hx.lower() and PET not in hx:
            continue
        out.append("%s %s hex=%s" % (row.get("CapturedUtc"), row.get("Direction"), hx[:140]))
        n += 1
        if n > 40:
            break

(p/"_pet_only.txt").write_text("\n".join(out), encoding="utf-8")
print("\n".join(out[:100]))
