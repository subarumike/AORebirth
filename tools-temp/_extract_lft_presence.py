import json
from pathlib import Path

p = Path(r"C:\Users\nermi\.cursor\projects\c-Users-nermi-source-repos-AORebirth\agent-transcripts\576cb4ef-4ed6-4ab8-94b8-d9353a5763af\576cb4ef-4ed6-4ab8-94b8-d9353a5763af.jsonl")
out_dir = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp")
n = 0
for line in p.open(encoding="utf-8"):
    try:
        o = json.loads(line)
    except Exception:
        continue
    c = o.get("message", {}).get("content")
    if not isinstance(c, list):
        continue
    for b in c:
        if b.get("type") != "tool_use" or b.get("name") != "Write":
            continue
        inp = b.get("input") or {}
        path = inp.get("path") or ""
        if "LftInviteClientPresence" not in path:
            continue
        text = inp.get("contents") or ""
        n += 1
        out = out_dir / ("_lft_presence_hist_%02d.cs" % n)
        out.write_text(text, encoding="utf-8")
        print(out.name, "len", len(text), "lines", text.count("\n") + 1)
print("total", n)
