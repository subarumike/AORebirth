"""Decode SpellList/Buff for Sparrow Flight cast from capture raw-packets.csv"""
import csv
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260723-053632/raw-packets.csv")
# N3 type values of interest - look at SpellList and Buff around cast time
# From events: SpellList #42, Buff #45 at cast; also CastNanoSpell

# Known N3 type hashes from AOtomation if needed - just dump hex for rows near cast
with cap.open(encoding="utf-8", newline="") as f:
    r = csv.DictReader(f)
    for row in r:
        name = row["N3TypeName"]
        if name in ("SpellList", "Buff", "CastNanoSpell", "Stat", "CharacterAction", "Feedback"):
            seq = row["Sequence"]
            # focus around cast sequences 35-50 and remove 84-90
            s = int(seq)
            if (35 <= s <= 50) or (80 <= s <= 95) or name in ("SpellList", "Buff"):
                print(f"seq={seq} dir={row['Direction']} type={name} len={row['PacketLength']}")
                print(row["RawHex"][:200], "...")
                print("---")
