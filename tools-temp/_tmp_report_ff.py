# -*- coding: utf-8 -*-
# FormatFeedback report line from capture
s = r'''2026-07-30T21:46:05.2751592Z [IN-N3] #520 type=FormatFeedback text=~&!!!":$*)e`sBureaucrat Workeri!!!"0i!!!"0i!!!!!i!!!!+i!!!KPi!!!*7 detail=FormatFeedbackMessage { Unknown1=0 Message="~&!!!":$*)e`sBureaucrat Workeri!!!"0i!!!"0i!!!!!i!!!!+i!!!KPi!!!*'''
import re
m = re.search(r'Message="([^"]+)"', s)
# get from text=
m2 = re.search(r'text=(~&[^ ]+)', s)
print(repr(m2.group(1) if m2 else None))
# raw from events
from pathlib import Path
ev = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260730-234537/system-messages.log").read_text(encoding="utf-8-sig", errors="replace")
for line in ev.splitlines():
    if "FormatFeedback" in line and "Bureaucrat" in line:
        # extract Message=
        i = line.find('Message="')
        if i>=0:
            j = line.find('"', i+9)
            # message may have weird chars - find FormattedMessage or end
            rest = line[i+9:]
            # take until " Unknown2 or " Formatted
            k = rest.find('" Unknown')
            if k<0: k = rest.find('" Formatted')
            msg = rest[:k] if k>=0 else rest[:80]
            print("MSG", repr(msg))
            print("HEX", msg.encode('latin-1', errors='replace').hex())
