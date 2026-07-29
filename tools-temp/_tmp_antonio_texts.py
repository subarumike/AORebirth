# Extract KnuBotAppendText strings and VendingMachineFullUpdate for Antonio.
from __future__ import print_function
import csv
import re
import struct
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-Antonio-Stacklund")
out = Path(r"tools-temp/_tmp_antonio_texts.txt")
lines = []


def p(*a):
    lines.append(" ".join(str(x) for x in a))


def decode_hex(h):
    h = re.sub(r"[^0-9A-Fa-f]", "", h)
    if len(h) % 2:
        h = h[:-1]
    return bytes.fromhex(h)


def extract_strings(payload, min_len=8):
    # AO strings often: 2-byte BE length then ASCII, or 4-byte BE length
    found = []
    i = 0
    while i + 2 < len(payload):
        # try u16be
        n = (payload[i] << 8) | payload[i + 1]
        if 8 <= n <= 2000 and i + 2 + n <= len(payload):
            chunk = payload[i + 2 : i + 2 + n]
            if all(32 <= b <= 126 or b in (9, 10, 13) for b in chunk):
                try:
                    s = chunk.decode("ascii")
                    if any(c.isalpha() for c in s):
                        found.append((i, "u16", s))
                        i += 2 + n
                        continue
                except Exception:
                    pass
        # try u32be
        if i + 4 < len(payload):
            n4 = struct.unpack_from(">I", payload, i)[0]
            if 8 <= n4 <= 2000 and i + 4 + n4 <= len(payload):
                chunk = payload[i + 4 : i + 4 + n4]
                if all(32 <= b <= 126 or b in (9, 10, 13) for b in chunk):
                    try:
                        s = chunk.decode("ascii")
                        if any(c.isalpha() for c in s):
                            found.append((i, "u32", s))
                            i += 4 + n4
                            continue
                    except Exception:
                        pass
        i += 1
    return found


# Scan raw-packets for KnubotAppend / AppendText / VendingMachineFullUpdate
with (cap / "raw-packets.csv").open(newline="", encoding="utf-8", errors="replace") as f:
    rows = list(csv.DictReader(f))

p("cols", list(rows[0].keys()) if rows else None)
type_keys = set()
append_packets = []
vend_packets = []
for row in rows:
    typ = (
        row.get("N3MessageType")
        or row.get("DecodedType")
        or row.get("Type")
        or row.get("MessageType")
        or ""
    )
    blob = " ".join(str(v) for v in row.values())
    type_keys.add(typ)
    if "Append" in typ or "Append" in blob[:200]:
        append_packets.append(row)
    if "VendingMachineFull" in typ or "VendingMachineFull" in blob:
        vend_packets.append(row)

p("unique types sample", sorted([t for t in type_keys if t])[:80])
p("append packet count", len(append_packets))
p("vend packet count", len(vend_packets))

# Prefer packets.hex.log lines with KnubotAppendText / type name
hexlog = (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines()
append_hex_lines = []
vend_hex_lines = []
for line in hexlog:
    if "KnubotAppend" in line or "KnuBotAppend" in line or "AppendText" in line:
        append_hex_lines.append(line)
    if "VendingMachineFull" in line:
        vend_hex_lines.append(line)
p("hexlog append lines", len(append_hex_lines))
p("hexlog vend lines", len(vend_hex_lines))
for line in append_hex_lines[:5]:
    p("APPENDLINE", line[:200])
for line in vend_hex_lines[:5]:
    p("VENDLINE", line[:200])

# If type names not in hexlog, decode by n3 type id. Known AO: look for ShopUpdate seq 149 nearby.
# From events: ShopUpdate #149 at 19:24:07 - find that hex packet and nearby appends.
# Also decode all IN packets around dialogue opens for strings containing upgrade/weapon/Welcome

dialogue_strings = []
vend_templates = []
for line in hexlog:
    m = re.search(r"n3=(\w+).*?hex=([0-9A-Fa-f]+)", line)
    if not m:
        # alternate format
        m = re.search(r"hex=([0-9A-Fa-f]+)", line)
        if not m:
            continue
        typ = ""
        hx = m.group(1)
    else:
        typ = m.group(1)
        hx = m.group(2)
    if typ and typ not in (
        "KnubotAppendText",
        "KnuBotAppendText",
        "AppendText",
        "VendingMachineFullUpdate",
        "ShopUpdate",
        "Trade",
        "KnubotAnswerList",
        "KnuBotAnswerList",
        "KnubotOpenChatWindow",
    ):
        # still scan Append-looking by size? skip huge combat
        if len(hx) < 80:
            continue
        # only keep if likely dialogue
        pass

    try:
        payload = decode_hex(hx)
    except Exception:
        continue

    if "Vending" in typ or (b"\x12\xe7\x72\x0d" in payload or b"\x0d\x72\xe7\x12" in payload):
        # look StaticInstance-ish ints
        for off in range(0, min(len(payload) - 4, 200), 1):
            val = struct.unpack_from(">I", payload, off)[0]
            if 200000 <= val <= 400000:
                vend_templates.append((typ, off, val, line[:120]))

    strs = extract_strings(payload)
    for _, kind, s in strs:
        low = s.lower()
        if any(
            k in low
            for k in (
                "welcome",
                "stacklund",
                "antonio",
                "upgrade",
                "weapon",
                "shopping",
                "bracer",
                "leather",
                "hud",
                "assault",
                "rifle",
                "pistol",
                "shotgun",
                "grenade",
                "dagger",
                "sword",
                "hammer",
                "blade",
                "energy",
                "naja",
                "oak",
                "bat",
                "bow",
                "submachine",
                "combine",
                "sell",
                "general",
                "teach",
                "vest",
                "device",
                "matter",
                "only have",
                "press",
                "cart",
            )
        ):
            dialogue_strings.append((typ or "?", s))

# unique preserve
seen = set()
uniq = []
for typ, s in dialogue_strings:
    key = s
    if key in seen:
        continue
    seen.add(key)
    uniq.append((typ, s))

p("\n=== DIALOGUE STRINGS ===", len(uniq))
for typ, s in uniq:
    p("---", typ)
    p(s)

p("\n=== VEND TEMPLATE CANDIDATES ===")
seen_v = set()
for typ, off, val, preview in vend_templates:
    if val in seen_v:
        continue
    seen_v.add(val)
    p(typ, "off", off, "val", val, preview)

# Also try events for KnubotAppendText DETAIL - maybe Text field elsewhere
events = (cap / "events.log").read_text(encoding="utf-8", errors="replace")
for pat in ("KnubotAppendText", "KnuBotAppendText", "AppendTextMessage", "Text="):
    p("events count", pat, events.count(pat))

# Find Text= in knubot related lines
for i, line in enumerate(events.splitlines()):
    if "78E0FC7C" in line and ("Text=" in line or "Append" in line):
        p("EVT", line[:500])

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "lines", len(lines), "dialogues", len(uniq))
