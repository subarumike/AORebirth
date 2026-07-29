import csv
from pathlib import Path

caps = [
    "20260727-lft-list-search",
    "20260729-011333",
    "20260729-011305",
    "20260729-030954",
]

for cap in caps:
    p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures") / cap / "raw-packets.csv"
    if not p.exists():
        print("missing", cap)
        continue
    rows = list(csv.DictReader(p.open(encoding="utf-8-sig", newline="")))
    print("====", cap, "rows", len(rows))
    hits = 0
    for i, r in enumerate(rows):
        hx = (r.get("RawHex") or "").replace(" ", "")
        if not hx:
            continue
        b = bytes.fromhex(hx)
        for code, label in [
            (0x14, "Name20"),
            (0x15, "Name21"),
            (0x5DD, "LFT"),
            (0x5DE, "LFTsearch"),
            (0x5DC, "LFTreg"),
        ]:
            needle = code.to_bytes(2, "big")
            pos = b.find(needle)
            if pos < 0 or pos + 4 > len(b):
                continue
            ln = int.from_bytes(b[pos + 2 : pos + 4], "big")
            if ln < 4 or ln > 800:
                continue
            # Prefer framed chat packets near start
            if pos > 8 and code in (0x14, 0x15):
                continue
            payload = b[pos + 4 : pos + ln]
            print(
                " #%d %s %s pos=%d ln=%d pay=%s"
                % (i, r.get("Direction"), label, pos, ln, payload[:80].hex())
            )
            hits += 1
            if hits > 40:
                break
        if hits > 40:
            break
    print("hits", hits)
