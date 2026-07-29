from pathlib import Path
import sys

cap = Path(r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260721-marcus-animation-texture-dialogtext")
out = Path(r"tools-temp\_tmp_marcus_cap_out.txt")

def scan(name, needles, max_hits=60):
    p = cap / name
    lines = []
    if not p.exists():
        return [f"MISSING {name}"]
    lines.append(f"\n===== {name} =====")
    hits = 0
    with p.open("r", encoding="utf-8-sig", errors="replace") as f:
        for line in f:
            low = line.lower()
            if any(n.lower() in low for n in needles):
                lines.append(line.rstrip()[:350])
                hits += 1
                if hits >= max_hits:
                    lines.append("...(truncated)")
                    break
    lines.append(f"hits {hits}")
    return lines

chunks = []
chunks += scan("npc-interactions.log", ["Marcus", "KnuBot", "Trade", "Accept", "Finish", "Reject", "StartTrade", "FinishTrade", "Answer"])
chunks += scan("mission-flow.log", ["Marcus", "B19", "fire", "quest", "Accept", "Complete", "Item", "QFU", "Quest"])
chunks += scan("chat-dialogue.log", ["Marcus", "fire", "Accept", "gas", "robot", "valve", "extinguish"])
chunks += scan("inventory-updates.csv", ["Add", "Remove", "Template", "HighId", "LowId", "Quality"])
chunks += scan("enemy-fight-events.log", ["Marcus", "ExtTex", "Texture", "SpellList", "Anim", "Gfx", "Flamethrower", "Burning", "SpecialAttack", "AttackInfo"])
chunks += scan("events.log", ["ExtTex", "Texture", "SpellList", "FinishTrade", "StartTrade", "Accepted", "KnuBotTrade", "Rejected"])
chunks += scan("system-messages.log", ["ExtTex", "SpellList", "Texture", "Trade", "Accept", "Finish"])
chunks += scan("raw-packets.csv", ["ExtTex", "SpellList", "KnuBotFinish", "KnuBotTrade", "KnuBotStart", "Texture"])

# Also search packets.hex for known type ids if csv thin
hexlog = cap / "packets.hex.log"
if hexlog.exists():
    chunks.append("\n===== packets.hex.log keyword lines =====")
    hits = 0
    with hexlog.open("r", encoding="utf-8-sig", errors="replace") as f:
        for line in f:
            if any(x in line for x in ("ExtTex", "SpellList", "KnuBotFinish", "KnuBotTrade", "KnuBotStart", "FinishTrade")):
                chunks.append(line.rstrip()[:300])
                hits += 1
                if hits >= 40:
                    chunks.append("...(truncated)")
                    break
    chunks.append(f"hits {hits}")

out.write_text("\n".join(chunks), encoding="utf-8")
print("wrote", out, "chars", out.stat().st_size)
