from pathlib import Path
import re
replay = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215\mission-flow.replay.log").read_text(encoding="utf-8", errors="replace")
# finish related
for pat in ["UseItemOnItem", "Action=59", "QuestMessage", "Delete", "Received reward", "CONTAINER", "FIND", "FINISH", "complete"]:
    pass
lines = []
for line in replay.splitlines():
    u = line.upper()
    if any(x in u for x in ["USEITEMONITEM", "ACTION=59", "QUEST DELETE", "RECEIVED REWARD", "MISSION-ACTION", "DELETE", "CONTAINER-ADD", "FINDITEM"]):
        if "OFFER" in u and "DELETE" not in u:
            continue
        lines.append(line[:350])
Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_finish_lines.txt").write_text("\n".join(lines[-40:]), encoding="utf-8")
print("lines", len(lines))
for l in lines[-25:]:
    print(l[:300])
    print("---")
