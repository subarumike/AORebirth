import re
ev = open(r"tools-temp\_warp_full_events.txt", encoding="utf-8").read().splitlines()
for i, line in enumerate(ev):
    if any(x in line for x in (
        "AppearanceUpdate", "CastNanoSpell", "FinishNano", "Feedback", "SimpleCharFullUpdate",
        "WeaponItem", "SimpleItem", "ChestFull", "TeamMember", "CharInPlay", "Action=128",
        "CastNano ", "Parameter2=154914", "DYNEL", "CHAR-SEEN", "Madamp", "Engnera"
    )):
        print("%d: %s" % (i+1, line[:1000]))
        print("---")
