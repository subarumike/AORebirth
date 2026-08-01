import pathlib, csv, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-162433")
PET = "79AA2FEE"

# Get AttackInfo full detail from enemy-combat RawDetail - was empty earlier, try other columns
with (p/"enemy-combat.csv").open(encoding="utf-8-sig", newline="") as fh:
    r = csv.DictReader(fh)
    cols = r.fieldnames
    print("cols", cols)
    for row in r:
        if PET not in (row.get("SourceIdentity") or ""):
            continue
        print("---", row.get("MessageType"), row.get("CapturedUtc"))
        for c in cols:
            v = row.get(c)
            if v:
                print(" ", c, "=", v[:200] if len(v)>200 else v)

# Also search raw for AttackInfo hex for pet
print("=== AttackInfo raw samples")
with (p/"raw-packets.csv").open(encoding="utf-8-sig", newline="") as fh:
    r = csv.DictReader(fh)
    n = 0
    for row in r:
        if row.get("N3TypeName") != "AttackInfo":
            continue
        hx = row.get("RawHex") or ""
        if "79AA2FEE" not in hx.upper():
            continue
        print(row.get("CapturedUtc"), hx)
        n += 1
        if n >= 3:
            break

print("=== SAW raw")
with (p/"raw-packets.csv").open(encoding="utf-8-sig", newline="") as fh:
    r = csv.DictReader(fh)
    for row in r:
        if row.get("N3TypeName") == "SpecialAttackWeapon" and "79AA2FEE" in (row.get("RawHex") or "").upper():
            print(row.get("CapturedUtc"), row.get("RawHex"))
            break

print("=== Attack raw")
with (p/"raw-packets.csv").open(encoding="utf-8-sig", newline="") as fh:
    r = csv.DictReader(fh)
    for row in r:
        if row.get("N3TypeName") == "Attack" and "79AA2FEE" in (row.get("RawHex") or "").upper():
            print(row.get("CapturedUtc"), row.get("RawHex"))
            break

# StopFight from pet?
print("=== StopFight from pet?")
with (p/"raw-packets.csv").open(encoding="utf-8-sig", newline="") as fh:
    r = csv.DictReader(fh)
    for row in r:
        if row.get("N3TypeName") == "StopFight" and "79AA2FEE" in (row.get("RawHex") or "").upper():
            print(row.get("CapturedUtc"), row.get("RawHex")[:100])
