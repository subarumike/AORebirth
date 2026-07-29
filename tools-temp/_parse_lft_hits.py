import re

p = r"C:\Users\nermi\.cursor\projects\c-Users-nermi-source-repos-AORebirth\agent-tools\c76a8591-7c17-4a5b-800b-d4f62b263dd9.txt"
pat = re.compile(
    r"(LFT|LookingForTeam|TeamSearch|Looking For Team|unknown message 1500|unknown message 1502|case 1500|case 1502)",
    re.I,
)
seen = set()
with open(p, encoding="utf-8", errors="replace") as f:
    for i, line in enumerate(f, 1):
        m = pat.search(line)
        if not m:
            continue
        tm = re.search(r"agent-transcripts\\([0-9a-f-]{36})", line)
        tid = tm.group(1) if tm else "?"
        key = (tid, m.group(1).lower())
        if key in seen:
            continue
        seen.add(key)
        s = max(0, m.start() - 60)
        e = min(len(line), m.end() + 140)
        snip = line[s:e].replace("\n", " ")
        print("%s | %s | %s" % (tid, m.group(1), snip[:220]))
