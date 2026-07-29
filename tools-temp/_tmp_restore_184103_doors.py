# Restore zone-in doors/chests from 184103 enter capture (reliable spawn-side doors).
from __future__ import print_function
import re

dynel = r"AORebirth/Server/ZoneEngine/Core/Missions/MissionInstanceDynelCapture.cs"
doors = open(r"tools-temp/_tmp_doors_1419349.csfrag", encoding="utf-8").read().strip()
chests = open(r"tools-temp/_tmp_chests_1419349.csfrag", encoding="utf-8").read().strip()
text = open(dynel, encoding="utf-8").read()
pat = re.compile(
    r"        // Capture 20260725-185432 PF 1419349 \(doors/chests during clear\)\r?\n"
    r"        public static readonly string\[\] Doors_1419349 =[\s\S]*?"
    r"        public static readonly string\[\] Chests_1419349 =[\s\S]*?"
    r"        \};",
    re.M,
)
# fallback if comment differs
if pat.search(text) is None:
    pat = re.compile(
        r"        // Capture 20260725-\d+ PF 1419349[^\n]*\r?\n"
        r"        public static readonly string\[\] Doors_1419349 =[\s\S]*?"
        r"        public static readonly string\[\] Chests_1419349 =[\s\S]*?"
        r"        \};",
        re.M,
    )
repl = (
    "        // Capture 20260725-184103 PF 1419349 zone-in doors/chests (enter)\n"
    + doors
    + "\n\n"
    + chests
)
text2, n = pat.subn(repl, text, count=1)
if n != 1:
    raise SystemExit("door replace failed n=%d" % n)
open(dynel, "w", encoding="utf-8", newline="\n").write(text2)
print("restored 184103 doors/chests")
