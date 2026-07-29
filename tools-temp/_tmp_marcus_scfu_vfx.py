import csv
import os

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260721-marcus-animation-texture-dialogtext"
out = r"tools-temp\_tmp_marcus_scfu_vfx.txt"

lines = []
for fname in ("scfu-appearance.csv", "enemy-full-updates.csv"):
    path = os.path.join(cap, fname)
    lines.append("==== " + fname)
    if not os.path.isfile(path):
        lines.append("missing")
        continue
    rows = list(csv.DictReader(open(path, encoding="utf-8-sig", errors="replace")))
    for r in rows:
        name = r.get("Name") or ""
        ident = r.get("Identity") or ""
        if "Marcus" not in name and "78E0FC62" not in ident:
            continue
        lines.append("--- " + name + " " + ident)
        for k in sorted(r.keys()):
            v = r.get(k) or ""
            if not v:
                continue
            if "Raw" in k or "Hex" in k:
                lines.append("%s=len%d head=%s" % (k, len(v), v[:100]))
            elif len(v) < 500:
                lines.append("%s=%s" % (k, v))

# Marcus SpellList / CharacterAction / Mesh related around fight
combat = os.path.join(cap, "enemy-combat.csv")
lines.append("==== marcus-related combat rows")
if os.path.isfile(combat):
    rows = list(csv.DictReader(open(combat, encoding="utf-8-sig", errors="replace")))
    for r in rows:
        blob = " ".join((r.get(k) or "") for k in r.keys())
        if "78E0FC62" in blob or "78E0FC72" in blob:
            lines.append(
                "%s %s %s %s"
                % (
                    r.get("CapturedUtc"),
                    r.get("N3TypeName") or r.get("MessageType"),
                    r.get("Identity") or r.get("AttackerIdentity"),
                    (r.get("Summary") or r.get("Decoded") or "")[:180],
                )
            )

open(out, "w", encoding="utf-8").write("\n".join(lines))
print("wrote", out, "lines", len(lines))
