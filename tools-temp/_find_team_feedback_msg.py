# Find feedback message 0x076C7CA9 / 124550313 references and nearby team strings
from pathlib import Path

needle = b"\x07\x6c\x7c\xa9"
needle2 = "124550313"
roots = [
    Path(r"AORebirth/Datafiles"),
    Path(r"AORebirth/Libraries/Source/Translations"),
    Path(r"tools-temp"),
]
for root in roots:
    if not root.exists():
        continue
    for p in root.rglob("*"):
        if not p.is_file():
            continue
        if p.stat().st_size > 50_000_000:
            continue
        try:
            data = p.read_bytes()
        except Exception:
            continue
        if needle in data or needle2.encode() in data:
            print("HIT", p)
        # also ascii team level phrases
        try:
            text = data.decode("utf-8", errors="ignore")
        except Exception:
            continue
        for phrase in ("too high", "too low", "share experience", "level to team", "cannot team"):
            if phrase in text.lower():
                print("PHRASE", phrase, p)
                break
