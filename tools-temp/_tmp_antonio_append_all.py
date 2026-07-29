# Dump every KnubotAppendText fully, ordered, with prior answer context.
from __future__ import print_function
import re
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-Antonio-Stacklund")
out = Path(r"tools-temp/_tmp_antonio_append_all.txt")
hexlog = (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines()
chat = (cap / "chat-dialogue.log").read_text(encoding="utf-8", errors="replace").splitlines()

# Map time -> last answer index from chat
answers = []
for line in chat:
    m = re.search(r"^(\S+).*Answer=(\d+)", line)
    if m and "KnubotAnswer" in line and "OUT" in line:
        answers.append((m.group(1), int(m.group(2))))


def find_answer_before(ts):
    last = None
    for t, a in answers:
        if t <= ts:
            last = a
        else:
            break
    return last


def decode_append(hx):
    raw = bytes.fromhex(hx)
    # After target identity, Unknown2 (u32) then string with u32be length? From sample:
    # ...0000C35078E0FC7C 00000000 000000A2 <text>
    # Find Antonio instance then read u32be len
    marker = bytes.fromhex("C35078E0FC7C")
    idx = raw.find(marker)
    if idx < 0:
        # fallback printable
        return None, "".join(chr(b) if 32 <= b < 127 else "" for b in raw)
    rest = raw[idx + len(marker) :]
    # next 4 bytes Unknown2, next 4 bytes length
    if len(rest) < 8:
        return None, ""
    unk2 = int.from_bytes(rest[0:4], "big")
    n = int.from_bytes(rest[4:8], "big")
    text = rest[8 : 8 + n].decode("ascii", errors="replace")
    return unk2, text


lines = []
append_idx = 0
for line in hexlog:
    if "n3=KnubotAppendText" not in line:
        continue
    m = re.match(r"^(\S+).*hex=([0-9A-Fa-f]+)", line)
    if not m:
        continue
    ts, hx = m.group(1), m.group(2)
    unk2, text = decode_append(hx)
    ans = find_answer_before(ts)
    append_idx += 1
    lines.append("=== #%d ts=%s unk2=%s priorAnswer=%s ===" % (append_idx, ts, unk2, ans))
    lines.append(text)
    lines.append("")

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "appends", append_idx)
