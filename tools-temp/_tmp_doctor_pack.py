# Extract Doctor nano pack open sequence only from nanoprogramsvendor capture.
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-nanoprogramsvendor")
out = Path(r"tools-temp/_tmp_doctor_pack_out.txt")
events = (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines()

# Find Use of 248258 / Inventory package open
idxs = []
for i, line in enumerate(events):
    if "248258" in line or ("TemplateAction" in line and "ItemLowId=" in line):
        idxs.append(i)

lines = []
lines.append(f"248258/TemplateAction hit lines: {len(idxs)}")
# Find GenericCmd Use Inventory near TemplateActions with doctor nanos
doctor_ids = ("248258", "43384", "42423", "99589", "43960", "43978", "223373")
for i, line in enumerate(events):
    if any(x in line for x in doctor_ids) or (
        "TemplateAction" in line and "ItemLowId=" in line and any(
            f"ItemLowId={x}" in line for x in ("43384", "42423", "99589", "43960", "43978", "223373", "248258")
        )
    ):
        lines.append(f"{i+1}:{line[:450]}")

# Also window around first doctor TemplateAction after Use
use_pack = None
for i, line in enumerate(events):
    if "OUT-N3-DETAIL] GenericCmd" in line and "Action=Use" in line and "Inventory:" in line:
        # look ahead for 248258 delete or 43384 grant
        window = "\n".join(events[i:i+40])
        if any(x in window for x in ("43384", "248258", "223373", "99589")):
            use_pack = i
            break

if use_pack is not None:
    lines.append("\n=== pack-open window ===")
    for j in range(use_pack, min(len(events), use_pack + 80)):
        line = events[j]
        if any(k in line for k in (
            "GenericCmd", "TemplateAction", "ContainerAddItem", "DeleteItem", "Quest",
            "CharacterAction", "FormatFeedback", "Feedback", "248258", "43384", "42423",
            "99589", "43960", "43978", "223373", "Inventory:", "Stat "
        )):
            lines.append(f"{j+1}:{line[:500]}")

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "n", len(lines))
