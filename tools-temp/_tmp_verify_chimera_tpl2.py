from pathlib import Path
import re
cs = Path(r"AORebirth/Server/ZoneEngine/Core/Packets/CorpseFullUpdate.cs").read_text(encoding="utf-8")
m = re.search(r'CapturedBarkingChimeraTemplate = HexToBytes\(\s*((?:"[^"]+"\s*\+?\s*)+)\s*\);', cs)
if not m:
    raise SystemExit("template not found")
parts = re.findall(r'"([0-9A-Fa-f]+)"', m.group(1))
tpl = "".join(parts).upper()
cap = Path(r"tools-temp/_tmp_chimera_corpse_template.hex").read_text().strip().upper()
print("tpl_len", len(tpl)//2, "match", tpl == cap)
live = "000007E26C6F7732" + ("00"*28) + "00033049" + ("00"*7) + "01"
print("live_in_tpl", live.upper() in tpl)
