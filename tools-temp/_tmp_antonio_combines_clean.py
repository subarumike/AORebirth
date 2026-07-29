# Pull every FormattedMessage combine + TemplateAction result from Antonio recipe caps.
from __future__ import print_function
import re
from pathlib import Path

caps = [
    Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-Antonio-1"),
    Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-antonio-2"),
]
out = Path(r"tools-temp/_tmp_antonio_combines_clean.txt")
lines = []

for cap in caps:
    lines.append("=== %s ===" % cap.name)
    events = (cap / "events.log").read_text(encoding="utf-8", errors="replace")
    for m in re.finditer(r'FormattedMessage="([^"]+)"', events):
        lines.append("FB: " + m.group(1))
    for m in re.finditer(
        r"TemplateActionMessage \{ ItemLowId=(\d+) ItemHighId=(\d+) Quality=(\d+)[^}]*Placement=\(([^)]+)\)",
        events,
    ):
        lines.append("TA: low=%s high=%s ql=%s place=%s" % m.groups())
    for m in re.finditer(r"Action=59[^}]*Target=\(Mission:([0-9A-Fa-f]+)\)", events):
        lines.append("TIP-DONE: Mission:%s" % m.group(1))
    for m in re.finditer(r"QuestMessage \{ Action=Delete[^}]*Mission=\(Mission:([0-9A-Fa-f]+)\)", events):
        lines.append("QUEST-DEL: Mission:%s" % m.group(1))
    lines.append("")

out.write_text("\n".join(lines), encoding="utf-8")
print(out.read_text(encoding="utf-8"))
