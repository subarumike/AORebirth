import csv
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-lft-list-search")
print("exists", cap.exists())

info = (cap / "capture_info.json").read_text(encoding="utf-8-sig", errors="replace")
print("==== capture_info ====")
print(info[:2500])

chat = (cap / "chat-dialogue.log").read_text(encoding="utf-8-sig", errors="replace")
print("==== chat-dialogue ====")
print(chat)

events = (cap / "events.log").read_text(encoding="utf-8-sig", errors="replace")
print("==== events (filtered) ====")
for line in events.splitlines():
    low = line.lower()
    if any(k in low for k in ("lft", "05dc", "05dd", "05de", "chat", "team", "1500", "1501", "1502")):
        print(line[:400])

rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))
print("raw rows", len(rows))

def find_chat(code):
    needle = code.to_bytes(2, "big")
    out = []
    for idx, r in enumerate(rows):
        hx = (r.get("RawHex") or "").replace(" ", "")
        if not hx:
            continue
        b = bytes.fromhex(hx)
        pos = b.find(needle)
        if pos < 0:
            continue
        if pos + 4 <= len(b):
            ln = int.from_bytes(b[pos + 2 : pos + 4], "big")
            end = pos + ln if ln >= 4 else len(b)
            payload = b[pos + 4 : end]
        else:
            payload = b[pos + 2 :]
        out.append((idx, r.get("Direction"), r.get("ElapsedMilliseconds"), payload, b[pos : pos + min(80, len(b) - pos)].hex()))
    return out

for label, code in (("REG 05DC", 0x05DC), ("REPLY 05DD", 0x05DD), ("SEARCH 05DE", 0x05DE)):
    found = find_chat(code)
    print("\n==", label, "count", len(found), "==")
    for idx, d, el, payload, head in found:
        print("#%d %s el=%s head=%s" % (idx, d, el, head))
        if code == 0x05DD and payload:
            i = 0
            mode = payload[i]
            i += 1
            if len(payload) < 5:
                print("  mode", mode, "short", payload.hex())
                continue
            cid = int.from_bytes(payload[i : i + 4], "big")
            i += 4
            nlen = int.from_bytes(payload[i : i + 2], "big")
            i += 2
            name = payload[i : i + nlen].decode("latin1", "replace")
            i += nlen
            if i + 10 > len(payload):
                print("  mode", mode, "cid", cid, "name", repr(name), "trunc", payload.hex())
                continue
            level = int.from_bytes(payload[i : i + 4], "big")
            i += 4
            pf = int.from_bytes(payload[i : i + 4], "big")
            i += 4
            side = payload[i]
            i += 1
            prof = payload[i]
            i += 1
            clen = int.from_bytes(payload[i : i + 2], "big")
            i += 2
            comment = payload[i : i + clen].decode("latin1", "replace")
            print(
                "  mode=%d id=%u name=%r level=%u pf=%u side=%u prof=%u comment=%r"
                % (mode, cid, name, level, pf, side, prof, comment)
            )
        elif code in (0x05DC, 0x05DE) and payload:
            print("  payload", payload.hex())
