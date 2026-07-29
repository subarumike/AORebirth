# Pull Karli textures from scfu CSV + dump QFU hex tips
from __future__ import print_function
import csv
import re
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-Alien- quest-ncu")
# also check 055715 for SCFU
caps = [
    cap,
    Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-055715"),
]

for c in caps:
    p = c / "scfu-appearance.csv"
    if not p.exists():
        continue
    print("===", c.name, "===")
    with p.open(encoding="utf-8-sig", errors="replace") as f:
        for row in csv.DictReader(f):
            if "Karli" not in (row.get("Name") or "") and "799AD394" not in (row.get("Identity") or ""):
                continue
            for k in ("Name","PlayfieldId","PositionX","PositionY","PositionZ","HeadingY","HeadingW",
                      "CharacterFlags","Level","Health","MonsterData","MonsterScale","HeadMesh","RunSpeedBase",
                      "NpcFamily","AppearanceValue","Side","Breed","Gender","Race","Fatness",
                      "Textures","Meshes","Waypoints","ScfuUnknown1Hex"):
                print(k, "=", (row.get(k) or "")[:300])
            print()

# dump QuestFullUpdate hex packets
print("=== QFU hex ===")
for ln in (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "QuestFullUpdate" not in ln:
        continue
    m = re.search(r"hex=([0-9A-Fa-f]+)", ln)
    if not m:
        continue
    hx = m.group(1)
    print(ln[:100])
    print("len", len(hx)//2)
    # save
    Path(r"tools-temp/_tmp_karli_qfu_%d.hex" % (len(hx)//2)).write_text(hx, encoding="ascii")
    # find Find a Friend
    raw = bytes.fromhex(hx)
    if b"Find a Friend" in raw:
        print("HAS Find a Friend")
        Path(r"tools-temp/_tmp_karli_qfu_find_friend.hex").write_text(hx, encoding="ascii")
    if b"crashed" in raw.lower() or b"alien" in raw.lower() or b"kill" in raw.lower():
        print("HAS kill/alien text snippet:", raw[raw.find(b"Enter") if b"Enter" in raw else 0:][:80])
