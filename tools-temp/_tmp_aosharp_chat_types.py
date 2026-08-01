# -*- coding: utf-8 -*-
from pathlib import Path
import re
data = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/AOSharp.Common.dll").read_bytes()
# extract ascii strings length>=8
strings = re.findall(rb"[\x20-\x7e]{8,}", data)
chatty = [s.decode("ascii") for s in strings if any(x in s.decode("ascii").lower() for x in ("chat", "systemmessage", "npcmessage", "vicinity", "packettype"))]
for s in sorted(set(chatty)):
    if any(x in s for x in ("SystemMessage", "NpcMessage", "ChatMessage", "NpcMessage", "Vicinity", "PacketType", "SimpleSystem")):
        print(s)
