from pathlib import Path
import re

cap = Path(r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260721-marcus-animation-texture-dialogtext")

def scan(name, needles, max_hits=40):
    p = cap / name
    if not p.exists():
        print("MISSING", name)
        return
    print(f"\n===== {name} =====")
    hits = 0
    with p.open("r", encoding="utf-8", errors="replace") as f:
        for line in f:
            low = line.lower()
            if any(n.lower() in low for n in needles):
                print(line.rstrip()[:300])
                hits += 1
                if hits >= max_hits:
                    print("...(truncated)")
                    break
    print("hits", hits)

scan("npc-interactions.log", ["Marcus", "KnuBot", "Trade", "Accept", "Finish", "Reject", "OpenTrade"])
scan("mission-flow.log", ["Marcus", "B19", "fire", "quest", "Accept", "Complete", "Item"])
scan("chat-dialogue.log", ["Marcus", "fire", "Accept", "gas", "robot"])
scan("inventory-updates.csv", ["Marcus", "fire", "1560", "Item", "High", "Low"])
scan("enemy-fight-events.log", ["Marcus", "ExtTex", "Texture", "SpellList", "Anim", "Gfx", "VFX", "Flamethrower", "Burning"])
scan("events.log", ["ExtTex", "Texture", "SpellList", "Marcus", "FinishTrade", "Accepted", "KnuBot"])
scan("system-messages.log", ["Marcus", "ExtTex", "SpellList", "Texture", "Accept", "Trade"])
