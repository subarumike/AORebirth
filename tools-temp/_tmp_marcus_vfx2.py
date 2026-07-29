import csv
import os

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260721-marcus-animation-texture-dialogtext"
out = []

# All CharacterAction / SpellList / CastNano near Marcus fight window
for fname in ("enemy-combat.csv", "events.log", "raw-packets.csv"):
    path = os.path.join(cap, fname)
    if not os.path.isfile(path):
        continue
    out.append("==== " + fname)
    if fname.endswith(".csv"):
        rows = list(csv.DictReader(open(path, encoding="utf-8-sig", errors="replace")))
        for r in rows:
            blob = "|".join((r.get(k) or "") for k in r.keys())
            if "78E0FC62" not in blob:
                continue
            t = r.get("N3TypeName") or r.get("MessageType") or ""
            out.append(
                "%s %s id=%s tgt=%s sum=%s"
                % (
                    r.get("CapturedUtc"),
                    t,
                    r.get("Identity") or r.get("AttackerIdentity"),
                    r.get("TargetIdentity") or r.get("Target"),
                    (r.get("Summary") or r.get("Decoded") or blob[-200:])[:220],
                )
            )
    else:
        for line in open(path, encoding="utf-8", errors="replace"):
            if "78E0FC62" in line and any(
                x in line
                for x in (
                    "CharacterAction",
                    "SpellList",
                    "CastNano",
                    "ItemAnim",
                    "Texture",
                    "Mesh",
                    "SpecialAttack",
                    "AttackInfo",
                    "Weapon",
                )
            ):
                out.append(line.strip()[:320])

# Decode SpecialAttackWeapon raw hex for Marcus
rp = os.path.join(cap, "raw-packets.csv")
if os.path.isfile(rp):
    out.append("==== SpecialAttackWeapon raw for Marcus")
    rows = list(csv.DictReader(open(rp, encoding="utf-8-sig", errors="replace")))
    for r in rows:
        if r.get("N3TypeName") != "SpecialAttackWeapon":
            continue
        hx = (r.get("RawHex") or "").upper()
        if "78E0FC62" not in hx:
            continue
        out.append("%s len=%s hex=%s" % (r.get("CapturedUtc"), r.get("PacketLength"), hx))

open(r"tools-temp\_tmp_marcus_vfx2.txt", "w", encoding="utf-8").write("\n".join(out))
print("lines", len(out))
