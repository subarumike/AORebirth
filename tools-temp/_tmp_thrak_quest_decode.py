# Decode Thrak garden quest capture into structured evidence.
import re, os, csv, json, binascii
from collections import defaultdict

base = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260718-185306"
out = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_thrak_quest_decode.txt"

# identity -> name from scfu + known
names = {}
with open(os.path.join(base, "scfu-appearance.csv"), encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        names[r.get("Identity", "")] = r.get("Name", "")

# Scientist from prior capture / this capture hint (Veronica)
names["(SimpleChar:787B54B2)"] = "Scientist Veronica Escobar"

hex_re = re.compile(
    r"^(?P<ts>\S+) (?P<dir>IN|OUT) #(?P<seq>\d+) len=\d+ n3=(?P<n3>\S+) hex=(?P<hex>[0-9A-Fa-f]+)"
)


def decode_append(hexstr):
    raw = bytes.fromhex(hexstr)
    # Find target SimpleChar after common header: look for 00C350 + identity
    # Layout after N3 header varies; text is length-prefixed near end.
    # From samples: ... 0000C350 <player> 00000200 00C350 <npc> <u32> <u32 len> <text>
    # Find last 00C350 before text
    idx = hexstr.upper().rfind("00C350")
    # Better: parse from end - last 4 bytes before ascii is length
    # Find ASCII run
    # Standard AO KnuBotAppendText body after identity fields:
    # Unknown1(4)=2, Target(Identity 8), Unknown2(4), Unknown3(4)=len?, Text
    # From hex dump pattern after second 00C350NNNNNNNN:
    # 00000001 or 00000000 then 000000NN length then text
    m = re.search(
        r"00C350([0-9A-F]{8})000000([0-9A-F]{2})000000([0-9A-F]{2})([0-9A-F]*)$",
        hexstr.upper(),
    )
    # Simpler approach: find length-prefixed string at end
    data = raw
    # scan for plausible length + printable
    for i in range(len(data) - 5, 20, -1):
        if i + 4 > len(data):
            continue
        # try big-endian length at i
        ln = int.from_bytes(data[i : i + 4], "big")
        if 1 <= ln <= 500 and i + 4 + ln <= len(data) and i + 4 + ln >= len(data) - 2:
            text = data[i + 4 : i + 4 + ln]
            try:
                s = text.decode("latin-1")
            except Exception:
                continue
            if all(32 <= c < 127 or c in (10, 13) for c in text) or b"\\n" in text:
                # extract npc from second 00C350
                hm = re.findall(r"00C350([0-9A-F]{8})", hexstr.upper())
                npc = hm[1] if len(hm) >= 2 else "?"
                return npc, s.replace("\\n", "\n")
    # fallback: take trailing printable
    hm = re.findall(r"00C350([0-9A-F]{8})", hexstr.upper())
    npc = hm[1] if len(hm) >= 2 else "?"
    # length at known offset: after second identity (8 bytes after 00C350+id = 4+4?)
    # Find second identity end
    parts = hexstr.upper().split("00C350")
    if len(parts) >= 3:
        rest = parts[2]  # id(8) + flags(8) + len(8) + text
        if len(rest) >= 24:
            npc = rest[:8]
            ln = int(rest[16:24], 16)
            text_hex = rest[24 : 24 + ln * 2]
            try:
                s = bytes.fromhex(text_hex).decode("latin-1")
                return npc, s.replace("\\n", "\n")
            except Exception:
                pass
    return npc, ""


lines_out = []
dialogue_by_npc = defaultdict(list)
answers = []  # chronological options/answers

with open(os.path.join(base, "packets.hex.log"), encoding="utf-8", errors="replace") as f:
    for line in f:
        m = hex_re.match(line.strip())
        if not m:
            continue
        n3 = m.group("n3")
        hx = m.group("hex")
        ts = m.group("ts")
        direction = m.group("dir")
        if n3 == "KnubotAppendText" and direction == "IN":
            npc, text = decode_append(hx)
            name = names.get("(SimpleChar:%s)" % npc, npc)
            dialogue_by_npc[npc].append((ts, text))
            lines_out.append("[%s] APPEND %s (%s): %s" % (ts, name, npc, repr(text)))
        elif n3 == "KnubotAnswerList" and direction == "IN":
            # decode options similarly - options are in chat-dialogue already
            pass

# Also parse chat-dialogue for answer lists chronologically
chat = os.path.join(base, "chat-dialogue.log")
with open(chat, encoding="utf-8", errors="replace") as f:
    for line in f:
        if "KnubotAnswerList" in line and "[IN-N3]" in line:
            tm = re.search(r"Target=\(SimpleChar:([0-9A-F]+)\)", line)
            opts = re.search(r"type=KnubotAnswerList text=(.*?) detail=", line)
            npc = tm.group(1) if tm else "?"
            name = names.get("(SimpleChar:%s)" % npc, npc)
            opt_text = opts.group(1) if opts else ""
            lines_out.append("OPTIONS %s (%s): %s" % (name, npc, opt_text))
        if "KnubotAnswer " in line and "[OUT-N3]" in line:
            tm = re.search(r"Target=\(SimpleChar:([0-9A-F]+)\)", line)
            ans = re.search(r"Answer=(\d+)", line)
            npc = tm.group(1) if tm else "?"
            name = names.get("(SimpleChar:%s)" % npc, npc)
            lines_out.append("ANSWER %s (%s): %s" % (name, npc, ans.group(1) if ans else "?"))
        if "KnubotStartTrade" in line and "[IN-N3]" in line:
            tm = re.search(r"Target=\(SimpleChar:([0-9A-F]+)\)", line)
            msg = re.search(r'Message="([^"]*)"', line)
            npc = tm.group(1) if tm else "?"
            name = names.get("(SimpleChar:%s)" % npc, npc)
            lines_out.append("TRADE_START %s (%s): %s" % (name, npc, msg.group(1) if msg else ""))
        if "KnubotTrade " in line and "[OUT-N3]" in line:
            tm = re.search(r"Target=\(SimpleChar:([0-9A-F]+)\)", line)
            cont = re.search(r"Container=\(Inventory:([0-9A-F]+)\)", line)
            npc = tm.group(1) if tm else "?"
            name = names.get("(SimpleChar:%s)" % npc, npc)
            lines_out.append("TRADE_ITEM %s (%s): Inventory:%s" % (name, npc, cont.group(1) if cont else "?"))

# mission flow
lines_out.append("\n=== MISSION FLOW ===")
with open(os.path.join(base, "mission-flow.log"), encoding="utf-8") as f:
    lines_out.extend(l.rstrip() for l in f)

# corpse loot
lines_out.append("\n=== LOOT ===")
with open(os.path.join(base, "corpse-loot-observations.csv"), encoding="utf-8-sig") as f:
    lines_out.extend(l.rstrip() for l in f)

# cursed transform from enemy dossier / fight
lines_out.append("\n=== CURSED / DREAMING EVENTS (events.log snippets) ===")
with open(os.path.join(base, "events.log"), encoding="utf-8", errors="replace") as f:
    for line in f:
        if any(
            x in line
            for x in [
                "Cursed",
                "Dreaming",
                "797652A0",
                "797652F5",
                "797652A7",
                "797654FD",
                "214788",
                "214789",
                "Ancient",
                "Insignia",
                "Analyzer",
                "555689",
                "55563C",
                "555659",
            ]
        ):
            if len(line) > 350:
                line = line[:350] + "...\n"
            lines_out.append(line.rstrip())

with open(out, "w", encoding="utf-8") as f:
    f.write("\n".join(lines_out))

print("wrote", out, "lines", len(lines_out))
print("npcs with append:", {k: len(v) for k, v in dialogue_by_npc.items()})
for npc, texts in dialogue_by_npc.items():
    name = names.get("(SimpleChar:%s)" % npc, npc)
    print("---", name, npc, "---")
    for ts, t in texts:
        print(" ", repr(t)[:120])
