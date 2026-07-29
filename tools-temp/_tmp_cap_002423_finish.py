from pathlib import Path
import re

p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-002423/events.log")
out = Path(r"tools-temp/_tmp_cap_002423_finish.txt")
lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
buf = []

buf.append("=== InfoRequest OUT ===")
for l in lines:
    if "InfoRequest" in l and "OUT-N3" in l:
        buf.append(l[:260])

buf.append("\n=== KEY finish messages ===")
for l in lines:
    if any(
        x in l
        for x in (
            "Side tokens",
            "upped to",
            "Received reward",
            "Action=Delete",
            "Mission chance",
        )
    ):
        buf.append(l[:320])

buf.append("\n=== DYNEL NPC unique (non-outdoor filter by name heuristics) ===")
seen = set()
skip = (
    "Unicorn",
    "OT ",
    "Guard",
    "Supplier",
    "Armorer",
    "Getkeep",
    "Squadleader",
)
pat = re.compile(
    r"name=([^=]+?) player=.*?hp=(\d+)/(\d+).*?level=(\d+).*?pos=\(([^)]+)\).*?monsterData=(\d+)"
)
for l in lines:
    if "[DYNEL-SPAWNED]" not in l or "player=False" not in l:
        continue
    m = pat.search(l)
    if not m:
        continue
    name = m.group(1)
    if any(s in name for s in skip):
        continue
    if name in seen:
        continue
    seen.add(name)
    buf.append(
        f"NPC {name} hp={m.group(2)}/{m.group(3)} lvl={m.group(4)} pos={m.group(5)} md={m.group(6)}"
    )

buf.append("\n=== SCFU CharacterFlags 1342706177 names ===")
pat2 = re.compile(
    r'Name="([^"]+)".*?CharacterFlags=1342706177.*?Level=(\d+) Health=(\d+).*?Position=\(([^)]+)\).*?MonsterData=(\d+)'
)
seen2 = set()
for l in lines:
    if "CharacterFlags=1342706177" not in l or "PlayfieldId=1443840" not in l:
        continue
    m = pat2.search(l)
    if not m:
        continue
    key = (m.group(1), m.group(4))
    if key in seen2:
        continue
    seen2.add(key)
    buf.append(
        f"FINDFLAG {m.group(1)} lvl={m.group(2)} hp={m.group(3)} pos={m.group(4)} md={m.group(5)}"
    )

# last InfoRequest target SCFU name
buf.append("\n=== Last few InfoRequest + nearby SCFU names ===")
idx = [i for i, l in enumerate(lines) if "InfoRequest" in l and "OUT-N3" in l]
for i in idx[-8:]:
    buf.append(lines[i][:260])
    m = re.search(r"Target=\(SimpleChar:([0-9A-F]+)\)", lines[i])
    if not m:
        continue
    tid = m.group(1)
    for j in range(max(0, i - 40), min(len(lines), i + 40)):
        if tid in lines[j] and 'Name="' in lines[j] and "SCFU" in lines[j].upper() or (
            tid in lines[j] and 'Name="' in lines[j]
        ):
            nm = re.search(r'Name="([^"]+)"', lines[j])
            if nm:
                buf.append(f"  -> {tid} name={nm.group(1)} line={j}")
                break
        if tid in lines[j] and "name=" in lines[j] and "DYNEL" in lines[j]:
            nm = re.search(r"name=([^=]+?) player=", lines[j])
            if nm:
                buf.append(f"  -> {tid} dynel={nm.group(1)} line={j}")
                break

out.write_text("\n".join(buf), encoding="utf-8")
print("wrote", out, "lines", len(buf))
