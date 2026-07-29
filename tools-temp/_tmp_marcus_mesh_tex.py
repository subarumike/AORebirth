import csv
import os

out_lines = []
root = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures"
for folder in [
    "20260719-do-flint-bio-com",
    "20260719-Rex-Markus-stone",
    "20260720-061810",
    "20260720-064523",
    "20260721-marcus-animation-texture-dialogtext",
]:
    p = os.path.join(root, folder, "scfu-appearance.csv")
    out_lines.append("=== %s exists=%s" % (folder, os.path.isfile(p)))
    if not os.path.isfile(p):
        continue
    rows = list(csv.DictReader(open(p, encoding="utf-8-sig", errors="replace")))
    out_lines.append("rows=%d names=%s" % (len(rows), sorted(set((r.get("Name") or "") for r in rows))[:20]))
    for r in rows:
        if "Marcus" in (r.get("Name") or "") or "78E0FC62" in (r.get("Identity") or ""):
            out_lines.append("FOUND Name=%s Id=%s" % (r.get("Name"), r.get("Identity")))
            for k in sorted(r.keys()):
                v = r.get(k) or ""
                if not v:
                    continue
                if "Raw" in k or "Hex" in k:
                    out_lines.append("  %s=len%d head=%s" % (k, len(v), v[:120]))
                elif len(v) < 400:
                    out_lines.append("  %s=%s" % (k, v))

# Also parse Mesh from events for Marcus
for folder in ["20260719-do-flint-bio-com", "20260720-064523"]:
    p = os.path.join(root, folder, "events.log")
    if not os.path.isfile(p):
        continue
    out_lines.append("=== events " + folder)
    for line in open(p, encoding="utf-8", errors="replace"):
        if "78E0FC62" in line and ("Mesh" in line or "Texture" in line or "292936" in line or "SpecialAttack" in line):
            out_lines.append(line.strip()[:300])

open(r"tools-temp\_tmp_marcus_mesh_tex.txt", "w", encoding="utf-8").write("\n".join(out_lines))
print("done", len(out_lines))
