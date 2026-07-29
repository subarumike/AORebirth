# -*- coding: utf-8 -*-
from pathlib import Path
term = "574187C3"
for cap_name in ["20260721-finish", "20260721-loralei", "20260721-194434"]:
    base = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures") / cap_name
    if not base.exists():
        continue
    for f in base.glob("*"):
        if f.suffix.lower() not in {".log", ".csv", ".json", ".hex"}:
            continue
        try:
            text = f.read_text(encoding="utf-8-sig", errors="ignore")
        except Exception:
            continue
        if term in text:
            # find SimpleItem lines
            for i, line in enumerate(text.splitlines(), 1):
                if term in line and ("SimpleItem" in line or "SIFU" in line or "DYNEL" in line or "ACGItem" in line or "Position" in line):
                    print(f"{cap_name}/{f.name}:{i}: {line[:400]}")
            print(f"--- {cap_name}/{f.name} has term ---")
