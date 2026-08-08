import csv
from collections import Counter
p = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260808-Warp-single\raw-packets.csv"
rows = list(csv.DictReader(open(p, encoding="utf-8-sig")))
print("N3 types:", Counter(r.get("N3TypeName") or "?" for r in rows).most_common())
print("\nAround finish (elapsed ~23s):")
for r in rows:
    # print interesting
    name = r.get("N3TypeName") or ""
    if name in (
        "CastNanoSpell", "CharacterAction", "Feedback", "SimpleCharFullUpdate",
        "AppearanceUpdate", "CharInPlay", "SpellList", "TeamMemberInfo",
        "WeaponItemFullUpdate", "SimpleItemFullUpdate", "ChestFullUpdate",
        "SpecialAttackWeapon", "InGameEffect", "CastEffect"
    ) or "Spell" in name or "Effect" in name or "Gfx" in name or "Teleport" in name:
        print(
            r.get("CapturedUtc"),
            r.get("Direction"),
            r.get("Sequence"),
            name,
            r.get("IdentityType"),
            r.get("IdentityInstance"),
            "len",
            r.get("PacketLength"),
        )
