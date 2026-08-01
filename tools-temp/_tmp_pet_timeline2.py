import pathlib, csv, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-162433")
out=[]

# Find pet identity from events
events = (p/"events.log").read_text(encoding="utf-8-sig", errors="replace")
for line in events.splitlines():
    if "Bureaucrat Worker" in line and "CHAR-SEEN" in line:
        out.append(line[:350])
        break
    if "Charge" in line or "follow you" in line:
        out.append(line[:350])

# Combat timeline: find pet instance from SAW rows with BUW1
out.append("=== combat timeline all SAW/Attack/AttackInfo/StopFight")
with (p/"enemy-combat.csv").open(encoding="utf-8-sig", newline="") as fh:
    r = csv.DictReader(fh)
    rows = list(r)
out.append("total combat rows %d" % len(rows))

# Find identities that send BUW1 / SpecialAttackWeapon with unknowns 16
pet_ids = set()
for row in rows:
    detail = row.get("RawDetail") or ""
    if "BUW1" in detail or "42555731" in detail or "WeaponInstance=1112889137" in detail:
        pet_ids.add(row.get("SourceIdentity") or "")
    if row.get("MessageType") == "SpecialAttackWeapon" and "Unknown1=16" in detail:
        pet_ids.add(row.get("SourceIdentity") or "")

out.append("pet_ids=" + str(pet_ids))

for row in rows:
    src = row.get("SourceIdentity") or ""
    mt = row.get("MessageType") or ""
    if mt not in ("SpecialAttackWeapon", "Attack", "AttackInfo", "StopFight"):
        continue
    if pet_ids and src not in pet_ids and mt != "StopFight":
        # still include StopFight for pet
        if src not in pet_ids:
            continue
    if pet_ids and src not in pet_ids:
        continue
    out.append("%s %s src=%s tgt=%s amt=%s u1=%s" % (
        row.get("CapturedUtc"), mt, src, row.get("TargetIdentity"),
        row.get("Amount"), row.get("Unknown1")))

# Also dump ALL SAW/Attack/AttackInfo around first Charge time
out.append("=== window around Charge 14:24:37")
for row in rows:
    ts = row.get("CapturedUtc") or ""
    if ts < "2026-07-30T14:24:36" or ts > "2026-07-30T14:25:10":
        continue
    mt = row.get("MessageType") or ""
    if mt in ("SpecialAttackWeapon", "Attack", "AttackInfo", "StopFight"):
        out.append("%s %s src=%s tgt=%s amt=%s detail=%s" % (
            ts, mt, row.get("SourceIdentity"), row.get("TargetIdentity"),
            row.get("Amount"), (row.get("RawDetail") or "")[:200]))

# PetCommand raw
out.append("=== PetCommand OUT")
with (p/"raw-packets.csv").open(encoding="utf-8-sig", newline="") as fh:
    r = csv.DictReader(fh)
    for row in r:
        if row.get("N3TypeName") == "PetCommand":
            out.append("%s %s hex=%s" % (row.get("CapturedUtc"), row.get("Direction"), (row.get("RawHex") or "")[:120]))

path = p/"_pet_timeline.txt"
path.write_text("\n".join(out), encoding="utf-8")
print("wrote", len(out))
for x in out[:80]:
    print(x[:220])
