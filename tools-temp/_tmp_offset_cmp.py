# -*- coding: utf-8 -*-
"""Compare field offsets between sandstorm and minibull/thief templates."""
from pathlib import Path
import re, struct

def be32(b, o):
    return struct.unpack_from(">I", b, o)[0]

def load_tpl(name):
    src = Path(r"AORebirth/Server/ZoneEngine/Core/Packets/CorpseFullUpdate.cs").read_text(encoding="utf-8")
    m = re.search(name + r' = HexToBytes\(\s*((?:"[0-9A-Fa-f]+"\s*\+?\s*)+)\);', src)
    hexs = "".join(re.findall(r'"([0-9A-Fa-f]+)"', m.group(1)))
    return bytes.fromhex(hexs)

offsets = {
    "ServerId": 8,
    "Receiver": 12,
    "CorpseInst": 24,
    "PosX": 45,
    "Playfield": 73,
    "Scale": 143,
    "Sex": 159,
    "Breed": 167,
    "Race": 175,
    "DeadNpc": 191,
    "CatMesh": 199,
    "Cash": 207,
}

for name in ["CapturedAreteSandstormMarauderTemplate", "CapturedAreteMinibullTemplate", "CapturedSubwayThiefTemplate"]:
    b = load_tpl(name)
    print("===", name, "len", len(b))
    for k, o in offsets.items():
        if k.startswith("Pos"):
            print(f"  {k}@{o}", struct.unpack_from(">f", b, o)[0])
        else:
            print(f"  {k}@{o}", be32(b, o), hex(be32(b, o)))
    print("  Remains at", b.find(b"Remains"))
