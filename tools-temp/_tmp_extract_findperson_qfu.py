# Extract Find Person QuestFullUpdate (icon 0x2C47 / 11335) from gold capture.
import os
import struct

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-224228"
paths = []
for root, _, files in os.walk(cap):
    for f in files:
        if f.endswith((".hex.log", ".csv", ".bin", ".log")):
            paths.append(os.path.join(root, f))

print("files", len(paths))
for p in paths[:30]:
    print(" ", p)

# Prefer packets.hex.log / raw-packets
candidates = [p for p in paths if "packets.hex" in p.lower() or "raw-packets" in p.lower()]
if not candidates:
    candidates = paths

ICON = b"\x00\x00\x2C\x47"  # FindPerson
KILL_ICON = b"\x00\x00\x2C\x42"

found = []
for p in candidates:
    try:
        data = open(p, "rb").read()
    except Exception as e:
        print("skip", p, e)
        continue
    # also try text hex log
    text = None
    try:
        text = open(p, "r", encoding="utf-8", errors="ignore").read()
    except Exception:
        pass
    if text and ("00002C47" in text or "00002c47" in text.lower()):
        # scan hex lines for Quest / 0340
        for line in text.splitlines():
            h = "".join(ch for ch in line if ch in "0123456789abcdefABCDEF")
            if len(h) < 200:
                continue
            if "00002C47" not in h.upper():
                continue
            if "00010340" not in h.upper() and "0340" not in h.upper():
                continue
            found.append((p, h.upper()))
            if len(found) >= 5:
                break
    if len(found) >= 5:
        break

print("found", len(found))
out = r"tools-temp\_tmp_findperson_qfu_out.txt"
with open(out, "w", encoding="utf-8") as w:
    for i, (p, h) in enumerate(found[:3]):
        w.write("=== %d from %s len=%d ===\n" % (i, p, len(h)//2))
        # try decode short/info ascii
        b = bytes.fromhex(h if len(h) % 2 == 0 else h[:-1])
        # find printable runs
        runs = []
        cur = []
        for x in b:
            if 32 <= x < 127:
                cur.append(chr(x))
            else:
                if len(cur) >= 12:
                    runs.append("".join(cur))
                cur = []
        if len(cur) >= 12:
            runs.append("".join(cur))
        for r in runs[:8]:
            w.write("TXT: %s\n" % r)
        w.write("HEX_START: %s\n" % h[:120])
        w.write("HEX_LEN: %d\n" % (len(h)//2))
        # write full hex to separate file
        open(r"tools-temp\_tmp_findperson_qfu_%d.hex" % i, "w").write(h)
        w.write("wrote tools-temp\\_tmp_findperson_qfu_%d.hex\n\n" % i)

print("wrote", out)
