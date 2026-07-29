# -*- coding: utf-8 -*-
"""Verify sandstorm template hex length and CATMesh at offset 199."""
from pathlib import Path
import re
src = Path(r"AORebirth/Server/ZoneEngine/Core/Packets/CorpseFullUpdate.cs").read_text(encoding="utf-8")
m = re.search(r'CapturedAreteSandstormMarauderTemplate = HexToBytes\(\s*((?:"[0-9A-Fa-f]+"\s*\+?\s*)+)\);', src)
hexs = "".join(re.findall(r'"([0-9A-Fa-f]+)"', m.group(1)))
b = bytes.fromhex(hexs)
print("template len", len(b))
print("catmesh@199", int.from_bytes(b[199:203], "big"))
print("md@341", int.from_bytes(b[341:345], "big"))
print("name", b[239:269])
