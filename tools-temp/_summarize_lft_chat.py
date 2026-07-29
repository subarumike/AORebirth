import json
import re

path = r"C:\Users\nermi\.cursor\projects\c-Users-nermi-source-repos-AORebirth\agent-transcripts\b4cb1f5c-bdfd-4af4-bfb4-623f7a549fa6\b4cb1f5c-bdfd-4af4-bfb4-623f7a549fa6.jsonl"
keys = re.compile(r"LFT|0x05D[CDE]|1500|1502|LookingFor|Parser\.cs|ChatEngine", re.I)
with open(path, encoding="utf-8", errors="replace") as f:
    for i, line in enumerate(f, 1):
        if not keys.search(line):
            continue
        try:
            obj = json.loads(line)
        except Exception:
            continue
        role = obj.get("role")
        msg = obj.get("message", {})
        content = msg.get("content")
        texts = []
        if isinstance(content, str):
            texts.append(content)
        elif isinstance(content, list):
            for c in content:
                if isinstance(c, dict) and c.get("type") == "text":
                    texts.append(c.get("text", ""))
                elif isinstance(c, dict) and c.get("type") == "tool_use":
                    name = c.get("name", "")
                    inp = c.get("input", {})
                    # show Write/StrReplace paths
                    if name in ("Write", "StrReplace", "Shell"):
                        p = inp.get("path") or inp.get("command") or ""
                        texts.append("%s: %s" % (name, str(p)[:180]))
        blob = " | ".join(t.replace("\n", " ")[:300] for t in texts if t)
        if blob:
            print("--- line %d role=%s ---" % (i, role))
            print(blob[:500])
            print()
