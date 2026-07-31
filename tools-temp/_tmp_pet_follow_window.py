import pathlib, csv
p = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-151431")
# Decode PetCommand hex: command id at known offset
# From sample OUT PetCommand hex ends with ...00000007... for attack and 00000001 for follow
# Also dump FollowTarget / StopFight around follow time 13:15:06

out=[]
raw=p/"raw-packets.csv"
with raw.open(encoding="utf-8-sig", newline="") as fh:
    r=csv.DictReader(fh)
    for row in r:
        ts=row.get("CapturedUtc","")
        if not ts.startswith("2026-07-30T13:15:0"):
            continue
        name=row.get("N3TypeName","")
        ident=row.get("IdentityInstance","")
        if name in ("PetCommand","FollowTarget","StopFight","Stat","Attack","SpecialAttackWeapon","AttackInfo","ChatText","NpcMessage") or "79AA2FEE" in (row.get("RawHex") or ""):
            if "79AA2FEE" in (row.get("RawHex") or "") or name in ("PetCommand","FollowTarget","StopFight") or ident.upper() in ("79AA2FEE","2041200878"):
                out.append("%s %s %s %s" % (ts, row.get("Direction"), name, (row.get("RawHex") or "")[:80]))

(p/"_pet_follow_window.txt").write_text("\n".join(out), encoding="utf-8")
print(len(out))
