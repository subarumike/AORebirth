# Decode Veronica QuestFullUpdate from capture 20260718-185306
import csv
import struct
from pathlib import Path

csv_path = Path(
    r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260718-185306\raw-packets.csv"
)

def find_strings(hex_payload):
    data = bytes.fromhex(hex_payload)
    # After N3 header-ish: look for int32-len strings after mission id area
    # Find ASCII runs
    texts = []
    i = 0
    while i < len(data) - 4:
        # try big-endian length-prefixed string
        n = struct.unpack_from(">I", data, i)[0]
        if 4 <= n <= 2000 and i + 4 + n <= len(data):
            chunk = data[i + 4 : i + 4 + n]
            if all(32 <= b < 127 or b in (10, 13) for b in chunk):
                texts.append((i, chunk.decode("latin1")))
                i += 4 + n
                continue
        i += 1
    return data, texts

with csv_path.open(newline="", encoding="utf-8") as f:
    reader = csv.DictReader(f)
    for row in reader:
        if row.get("N3TypeName") != "QuestFullUpdate":
            continue
        hex_payload = row.get("RawHex") or ""
        if "5556893A" not in hex_payload.upper():
            continue
        print("TIME", row.get("CapturedUtc"))
        print("LEN", row.get("PacketLength"))
        data, texts = find_strings(hex_payload)
        print("packet_len", len(data))
        out = Path(r"tools-temp\_tmp_veronica_qfu.hex")
        out.write_text(hex_payload, encoding="ascii")
        print("wrote", out)
        print("---STRINGS---")
        for off, t in texts[:10]:
            print(f"@{off}: {t[:500]}")
            print("---")
        break
