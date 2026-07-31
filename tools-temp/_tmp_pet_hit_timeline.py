import pathlib, csv, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-151431")
out = []

# First attack window: PetCommand -> kill
out.append("=== first attack sequence pet 79AA2FEE")
with (p/"enemy-combat.csv").open(encoding="utf-8-sig", newline="") as fh:
    r = csv.DictReader(fh)
    for row in r:
        src = row.get("SourceIdentity") or ""
        tgt = row.get("TargetIdentity") or ""
        if "79AA2FEE" not in src and "79AA2FEE" not in tgt:
            continue
        ts = row.get("CapturedUtc","")
        if ts < "2026-07-30T13:14:48" or ts > "2026-07-30T13:14:52":
            continue
        out.append("%s %s %s->%s amt=%s u1=%s detail=%s" % (
            ts, row.get("MessageType"), src, tgt, row.get("Amount"), row.get("Unknown1"),
            (row.get("RawDetail") or row.get("Detail") or "")[:200]))

# Chat packets around Charge
out.append("=== chat/system around Charge")
for name in ["chat-dialogue.log", "events.log"]:
    text = (p/name).read_text(encoding="utf-8-sig", errors="replace")
    for line in text.splitlines():
        if "Charge" in line or "follow you" in line or "NpcMessage" in line or "ChatText" in line:
            out.append(line[:400])

# Raw hex for SystemMessage / ChatText near first Charge
out.append("=== raw IN packets near Charge time with text-like types")
with (p/"raw-packets.csv").open(encoding="utf-8-sig", newline="") as fh:
    r = csv.DictReader(fh)
    for row in r:
        ts = row.get("CapturedUtc","")
        if ts < "2026-07-30T13:14:48.0" or ts > "2026-07-30T13:14:49.0":
            continue
        name = row.get("N3TypeName") or ""
        out.append("%s %s %s %s hex=%s" % (ts, row.get("Direction"), name, row.get("IdentityInstance"), (row.get("RawHex") or "")[:120]))

# Full combat timeline for pet across session - message types only
out.append("=== all pet combat timeline")
with (p/"enemy-combat.csv").open(encoding="utf-8-sig", newline="") as fh:
    r = csv.DictReader(fh)
    for row in r:
        src = row.get("SourceIdentity") or ""
        if "79AA2FEE" not in src:
            continue
        out.append("%s %s tgt=%s amt=%s" % (row.get("CapturedUtc"), row.get("MessageType"), row.get("TargetIdentity"), row.get("Amount")))

path = p/"_pet_hit_timeline.txt"
path.write_text("\n".join(out), encoding="utf-8")
print("wrote", path, "lines", len(out))
