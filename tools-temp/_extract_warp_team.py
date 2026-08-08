import re
from collections import Counter

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260808-Warp-single\events.log"
OUT = r"tools-temp\_warp_team_extract.txt"

pat = re.compile(
    r"Warp|CastNano|FinishNano|CastNanoSpell|Teleport|Team|FollowTarget|SetPos|"
    r"SpecialUsed|Playfield|CharacterAction|GenericCmd|Feedback|Effect|"
    r"NanoFormula|TemplateAction|StatMessage|SocialAction|Gfx|Particle|"
    r"InGameEffect|SpellEffect|AreaEffect|CharGrid|Despawn|Spawn",
    re.I,
)

lines = []
with open(CAP, encoding="utf-8", errors="replace") as f:
    for i, line in enumerate(f, 1):
        if "CurrentNano=" in line and not re.search(r"CastNano|FinishNano|Warp|Teleport|Team|Follow|SetPos|Effect", line, re.I):
            continue
        if not pat.search(line):
            continue
        clip = line.rstrip()
        if len(clip) > 900:
            clip = clip[:900] + "..."
        lines.append("%d: %s" % (i, clip))

open(OUT, "w", encoding="utf-8").write("\n".join(lines))
print("hits", len(lines), "wrote", OUT)

# also dump cast nanos and key message types
types = Counter()
nanos = []
for line in lines:
    m = re.search(r"type=(\w+)", line)
    if m:
        types[m.group(1)] += 1
    m = re.search(r"Action=CastNano .* Parameter2=(\d+)", line)
    if m:
        nanos.append(("cast", int(m.group(1)), line[:200]))
    m = re.search(r"Action=FinishNanoCasting .* Parameter2=(\d+)", line)
    if m:
        nanos.append(("finish", int(m.group(1)), line[:200]))
    if "Warp" in line or "Teleport" in line or "FollowTarget" in line or "SetPos" in line:
        nanos.append(("key", 0, line[:250]))

print("types", types.most_common(30))
print("nano/key events", len(nanos))
for n in nanos[:80]:
    print(n[0], n[1], n[2])
