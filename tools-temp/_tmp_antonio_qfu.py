# Decode all Antonio recipe QuestFullUpdate packets from hexlog.
from __future__ import print_function
import re
import struct
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-Antonio-Stacklund")
out = Path(r"tools-temp/_tmp_antonio_qfu.txt")
hexlog = (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines()

mission_ids = {
    0x5569CDBF: "BO-18 Assault Rifle",
    0x5569CDC1: "Poison Injector Bracer",
    0x5569CDC2: "Leather Vest",
    0x5569CDC3: "Range Meter HUD",
    0x5569CDC4: "Wailing Bat",
    0x5569CDC5: "Grip Blade",
    0x5569CDCC: "Shaolin Sporting Bow",
    0x5569CDCD: "Electrical Surge Pistol",
    0x5569CDCE: "Injector Dagger",
    0x5569CDD9: "Wave Heated Plasma Energy Gun",
    0x5569CDE4: "Nizno Bomb Thrower",
    0x5569CDF1: "Balanced War Hammer",
    0x5569CDF7: "Cerset Zapper Rifle",
    0x5569CDF8: "Polished Eliminator Shotgun",
    0x5569CDF9: "Stabilized Silent SMG",
    0x5569CDFF: "Spine Sword",
    0x5569CE00: "Strong Oak Bo",
    0x5569CE11: "Surge Baseball Bat",
    0x5569CE17: "Hand Staff of Naja",
}


def extract_u16be_strings(payload):
    found = []
    i = 0
    while i + 2 < len(payload):
        n = (payload[i] << 8) | payload[i + 1]
        if 4 <= n <= 4000 and i + 2 + n <= len(payload):
            chunk = payload[i + 2 : i + 2 + n]
            if all(32 <= b <= 126 or b in (9, 10, 13) for b in chunk):
                s = chunk.decode("ascii", errors="replace")
                if sum(c.isalpha() for c in s) >= 3:
                    found.append(s)
                    i += 2 + n
                    continue
        i += 1
    return found


lines = []
seen_missions = set()
for line in hexlog:
    if "n3=QuestFullUpdate" not in line:
        continue
    m = re.search(r"hex=([0-9A-Fa-f]+)", line)
    if not m:
        continue
    raw = bytes.fromhex(m.group(1))
    # find mission instance ids
    for mid, label in mission_ids.items():
        be = struct.pack(">I", mid)
        if be not in raw:
            continue
        if mid in seen_missions:
            continue
        seen_missions.add(mid)
        strs = extract_u16be_strings(raw)
        # also try printable runs
        runs = re.findall(rb"[\x20-\x7e]{8,}", raw)
        run_s = [r.decode("ascii") for r in runs]
        lines.append("=== %08X %s ===" % (mid, label))
        lines.append("u16 strings:")
        for s in strs:
            lines.append("  " + s)
        lines.append("ascii runs:")
        for s in run_s:
            if any(k in s.lower() for k in ("assemble", "adapt", "itemref", "factory", "combine", "bracer", "vest", "meter", "weapon", "sample", "fluid", "<br", "font", "href")):
                lines.append("  " + s[:800])
        lines.append("")

# dump raw hex for first QFU for manual inspection
for line in hexlog:
    if "n3=QuestFullUpdate" in line and "5569CDBF" in line.upper().replace(" ", ""):
        pass
# find by bytes
for line in hexlog:
    if "n3=QuestFullUpdate" not in line:
        continue
    m = re.search(r"hex=([0-9A-Fa-f]+)", line)
    if not m:
        continue
    raw = bytes.fromhex(m.group(1))
    if struct.pack(">I", 0x5569CDBF) in raw:
        lines.append("=== FIRST QFU RAW HEX ===")
        lines.append(m.group(1))
        lines.append("len=%d" % len(raw))
        break

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "missions", len(seen_missions))
