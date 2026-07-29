# Decode Sarah QFU tip short/long from raw hex packets.
from pathlib import Path
import csv
import binascii

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-sara/raw-packets.csv")
out = Path(r"tools-temp/_tmp_sarah_qfu.txt")

def decode_ascii(hexstr):
    data = bytes.fromhex(hexstr)
    # extract printable runs
    runs = []
    cur = []
    for b in data:
        if 32 <= b < 127:
            cur.append(chr(b))
        else:
            if len(cur) >= 4:
                runs.append("".join(cur))
            cur = []
    if len(cur) >= 4:
        runs.append("".join(cur))
    return data, runs

lines = []
with cap.open(newline="", encoding="utf-8-sig", errors="replace") as f:
    rows = list(csv.DictReader(f))

for row in rows:
    hx = row.get("RawHex") or ""
    if "465A4061" not in hx.upper():  # FZ@a = QuestFullUpdate type marker? actually 46 5A 40 61
        # also check for mission ids
        if not any(m in hx.upper() for m in ("555CF53C", "555CF53F", "555CF540", "555CF538", "555BE9F3")):
            continue
    data, runs = decode_ascii(hx)
    lines.append(f"seq={row.get('Sequence')} dir={row.get('Direction')} n3={row.get('N3TypeName')} len={len(data)}")
    lines.append("runs: " + " | ".join(runs[:40]))
    # try to find short/long around mission id
    for mid in (b"\x55\x5c\xf5\x3c", b"\x55\x5c\xf5\x3f", b"\x55\x5c\xf5\x40", b"\x55\x5c\xf5\x38"):
        idx = data.find(mid)
        if idx >= 0:
            lines.append(f"  mission@{idx}: {mid.hex()} window={data[idx:idx+200]!r}")

# Also decode TemplateAction packet for 295618 quality
for row in rows:
    hx = (row.get("RawHex") or "").upper()
    if "000482C2" in hx or "0004867E" in hx:  # 295618=0x482C2, 296574=0x4867E
        data, runs = decode_ascii(hx)
        lines.append(f"TEMPLATE seq={row.get('Sequence')} runs={runs[:10]}")
        lines.append(hx[:200])

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out)
