# Dig enemy AttackInfo weapon slots + finish order + zone-in SCFU positions
import csv, struct, os, re

CAP = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-151009"
OUT = r"tools-temp/_tmp_cap_151009_combat2.txt"
lines = []

def p(s=""):
    lines.append(s)

# From enemy-combat.csv: AttackInfo where SourceRole=enemy
with open(os.path.join(CAP, "enemy-combat.csv"), encoding="utf-8-sig") as f:
    rows = list(csv.DictReader(f))

p("=== enemy AttackInfo / MissedAttackInfo ===")
for r in rows:
    mt = r.get("MessageType") or ""
    sr = r.get("SourceRole") or ""
    if sr == "enemy" and mt in ("AttackInfo", "MissedAttackInfo", "SpecialAttackWeapon", "Attack", "WeaponItemFullUpdate"):
        p("%s %s src=%s tgt=%s amt=%s u1=%s u2=%s u3=%s u4=%s u5=%s action=%s" % (
            r.get("CapturedUtc"), mt, r.get("SourceIdentity"), r.get("TargetIdentity"),
            r.get("Amount"), r.get("Unknown1"), r.get("Unknown2"), r.get("Unknown3"),
            r.get("Unknown4"), r.get("Unknown5"), r.get("Action")))

p("\n=== SAW enemy hex decode ===")
# SpecialAttackWeapon: after identity, specials
# From hx: ...C35079944F75 00000007E2 00023566 00023567 53495731...
# Unknowns 20,20,20,20,0

p("\n=== raw AttackInfo with enemy identity 79944F ===")
with open(os.path.join(CAP, "raw-packets.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        if row.get("N3TypeName") != "AttackInfo":
            continue
        if not (row.get("Direction") or "").startswith("IN"):
            continue
        det = row.get("Detail") or ""
        hx = (row.get("RawHex") or "").replace(" ", "").upper()
        if "79944F" in hx or "79944F" in det.upper() or "WeaponSlot" in det:
            # Prefer detail decode
            if "AttackInfoMessage" in det or "WeaponSlot" in det:
                p("%s %s" % (row.get("Timestamp"), det[:260]))
            elif "79944F" in hx:
                # identity of attacker is in AttackInfo as message identity
                p("%s hxiden attacker? detail=%s hx=%s" % (row.get("Timestamp"), det[:120], hx[:160]))

p("\n=== finish packets chronological ===")
want = ("FormatFeedback", "Feedback", "Quest", "CreateItem", "Stat", "TemplateAction", "InventoryUpdate", "Bank")
with open(os.path.join(CAP, "raw-packets.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        nt = row.get("N3TypeName") or ""
        if nt not in want:
            continue
        if not (row.get("Direction") or "").startswith("IN"):
            continue
        ts = row.get("Timestamp") or ""
        # only around finish ~13:13
        if "13:12" not in ts and "13:13" not in ts and "13:11" not in ts:
            continue
        det = row.get("Detail") or ""
        p("%s %s %s" % (ts, nt, det[:220]))

p("\n=== ALL FormatFeedback / CreateItem / Quest Delete ===")
with open(os.path.join(CAP, "raw-packets.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        nt = row.get("N3TypeName") or ""
        if nt not in ("FormatFeedback", "CreateItem", "Quest", "Feedback", "TemplateAction"):
            continue
        if not (row.get("Direction") or "").startswith("IN"):
            continue
        p("%s %s %s" % (row.get("Timestamp"), nt, (row.get("Detail") or "")[:240]))

p("\n=== zone-in SCFU positions from enemy-full-updates ===")
efu = os.path.join(CAP, "enemy-full-updates.csv")
if os.path.exists(efu):
    with open(efu, encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f))
    p("cols=%s" % list(rows[0].keys()) if rows else [])
    for r in rows[:30]:
        p("  %s" % {k: r.get(k) for k in list(r.keys())[:14]})

p("\n=== WeaponItemFullUpdate ===")
with open(os.path.join(CAP, "raw-packets.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        if row.get("N3TypeName") == "WeaponItemFullUpdate":
            p("%s %s %s" % (row.get("Timestamp"), row.get("Direction"), (row.get("Detail") or "")[:300]))
            hx = (row.get("RawHex") or "").replace(" ", "").upper()
            p("  hxlen=%d head=%s" % (len(hx)//2, hx[:120]))

open(OUT, "w", encoding="utf-8").write("\n".join(lines))
print("wrote", OUT, len(lines))
