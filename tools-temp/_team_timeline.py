import json
from pathlib import Path

p = Path(
    r"C:\Users\nermi\.cursor\projects\c-Users-nermi-source-repos-AORebirth\agent-transcripts"
    r"\b4cb1f5c-bdfd-4af4-bfb4-623f7a549fa6\b4cb1f5c-bdfd-4af4-bfb4-623f7a549fa6.jsonl"
)
lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
start = None
for i, l in enumerate(lines):
    if "team UI packets are now wired" in l:
        start = i
        break
print("start", start, "total", len(lines))
out = []
count = 0
for i in range(start, len(lines)):
    try:
        o = json.loads(lines[i])
    except Exception:
        continue
    role = o.get("role")
    msg = o.get("message", {})
    content = msg.get("content") if isinstance(msg, dict) else None
    texts = []
    if isinstance(content, list):
        for c in content:
            if isinstance(c, dict) and c.get("type") == "text":
                texts.append(c.get("text") or "")
    elif isinstance(content, str):
        texts.append(content)
    t = "\n".join(texts).strip()
    if not t:
        continue
    if role == "user":
        out.append("--- USER line %d ---\n%s\n" % (i, t[:1000]))
        count += 1
    elif role == "assistant":
        low = t.lower()
        if any(
            k in low
            for k in (
                "team",
                "invite",
                "accept",
                "leave",
                "lft",
                "socialstatus",
                "parameter",
                "roster",
                "decline",
                "noname",
                "xp warn",
                "looking for",
            )
        ):
            # skip very short tool narrations
            if len(t) < 120:
                continue
            out.append("--- ASST line %d ---\n%s\n" % (i, t[:1500]))
            count += 1
    if count > 80:
        break

Path(r"tools-temp\_team_timeline_extract.txt").write_text(
    "\n".join(out), encoding="utf-8"
)
print("wrote", len(out), "blocks")
