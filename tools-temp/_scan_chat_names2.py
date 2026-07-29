import csv
from pathlib import Path

# Decode Name20 packets properly with both length interpretations
p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260729-030954/raw-packets.csv")
rows = list(csv.DictReader(p.open(encoding="utf-8-sig", newline="")))
print("looking for chat frames starting with 0014 or 0015")
for i, r in enumerate(rows):
    hx = (r.get("RawHex") or "").replace(" ", "")
    if not hx:
        continue
    b = bytes.fromhex(hx)
    if len(b) < 4:
        continue
    t = int.from_bytes(b[0:2], "big")
    ln = int.from_bytes(b[2:4], "big")
    if t not in (0x14, 0x15, 0x5DD, 0x5DE, 0x5DC):
        continue
    payload = b[4:]
    print(
        "#%d %s type=%04X declared_ln=%d actual_len=%d payload=%s ascii=%r"
        % (
            i,
            r.get("Direction"),
            t,
            ln,
            len(b),
            payload[:100].hex(),
            payload[:80],
        )
    )
