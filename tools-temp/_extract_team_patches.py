import json
import re

path = r"C:\Users\nermi\.cursor\projects\c-Users-nermi-source-repos-AORebirth\agent-transcripts\b4cb1f5c-bdfd-4af4-bfb4-623f7a549fa6\b4cb1f5c-bdfd-4af4-bfb4-623f7a549fa6.jsonl"
out = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_team_fix_extract.txt"
wanted = ("SendTeamInviteRequest", "TeamMemberMessage", "TeamSide", "Parameter2 == 17", "TeamMemberLeft", "LeaveTeam = 0x00000018")
with open(path, encoding="utf-8", errors="replace") as f, open(out, "w", encoding="utf-8") as o:
    for i, line in enumerate(f, 1):
        if not any(w in line for w in wanted):
            continue
        try:
            obj = json.loads(line)
        except Exception:
            continue
        content = obj.get("message", {}).get("content")
        if not isinstance(content, list):
            continue
        for c in content:
            if not isinstance(c, dict) or c.get("type") != "tool_use":
                continue
            name = c.get("name")
            inp = c.get("input") or {}
            if name == "StrReplace" and "new_string" in inp:
                ns = inp["new_string"]
                if any(w in ns for w in wanted) or "SendTeam" in ns or "TeamMember" in ns:
                    o.write("=== line %d StrReplace path=%s ===\n" % (i, inp.get("path")))
                    o.write(ns)
                    o.write("\n\n")
            if name == "Write" and "contents" in inp:
                ct = inp["contents"]
                if "TeamMember" in ct or "SendTeam" in ct:
                    o.write("=== line %d Write path=%s ===\n" % (i, inp.get("path")))
                    o.write(ct[:8000])
                    o.write("\n\n")
print("wrote", out)
