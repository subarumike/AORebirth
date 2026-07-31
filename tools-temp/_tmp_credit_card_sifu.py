# -*- coding: utf-8 -*-
import pathlib, re, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = pathlib.Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260730-214622/capture.log")
lines = p.read_text(encoding="utf-8", errors="replace").splitlines()

# Full SIFU for credit card
for i, l in enumerate(lines):
    if "297315" in l and "SimpleItemFullUpdate" in l:
        print("SIFU", i, l[:500])
        print("---")

# OUT GenericCmd targeting 57A9CCBE
print("=== OUT/IN Use sequence ===")
for i, l in enumerate(lines):
    if "57A9CCBE" in l and ("[OUT" in l or "Action=Use" in l or "Despawn" in l):
        print(i, l[:300])

# QuestFullUpdate tip text near pickup
print("=== tip / steal reward ===")
for i, l in enumerate(lines):
    if any(x in l for x in ["You pick up", "15000", "Steal the credits", "Deliver the Lost", "5572F3E9", "5572F3EA", "Received reward"]):
        print(i, l[:350])
