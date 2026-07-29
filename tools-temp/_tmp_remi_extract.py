# Extract Remi AppendText, tips, rewards from capture
import csv
import os
import re
import binascii

cap = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260727-204902"
out = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_remi_extract.txt"

def decode_strings(hexbody):
    try:
        b = binascii.unhexlify(hexbody.replace(" ", ""))
    except Exception:
        return []
    # AO strings often length-prefixed
    texts = []
    i = 0
    while i < len(b) - 2:
        # try 2-byte BE length
        n = (b[i] << 8) | b[i + 1]
        if 2 <= n <= 200 and i + 2 + n <= len(b):
            chunk = b[i + 2 : i + 2 + n]
            if all(32 <= c < 127 or c in (9, 10, 13) for c in chunk):
                texts.append(chunk.decode("ascii", errors="replace"))
                i += 2 + n
                continue
        i += 1
    # also printable runs
    runs = re.findall(rb"[\x20-\x7e]{8,}", b)
    for r in runs:
        s = r.decode("ascii")
        if s not in texts:
            texts.append(s)
    return texts

with open(out, "w", encoding="utf-8") as f:
    # events with AppendText / TemplateAction / FormatFeedback / QuestFull / Trade
    for ln in open(os.path.join(cap, "events.log"), encoding="utf-8", errors="replace"):
        if any(k in ln for k in (
            "AppendText", "TemplateAction", "FormatFeedback", "QuestFull",
            "QuestMessage", "Trade", "Hellfyre", "SANDSTORM", "Remi", "556B5E"
        )):
            f.write(ln[:600] + ("\n" if not ln.endswith("\n") else ""))

    f.write("\n==== raw packets interesting ====\n")
    with open(os.path.join(cap, "raw-packets.csv"), encoding="utf-8-sig", newline="") as cf:
        for row in csv.DictReader(cf):
            name = row.get("N3TypeName") or ""
            if name not in (
                "KnuBotAppendText", "KnubotAppendText", "TemplateAction",
                "FormatFeedback", "QuestFullUpdate", "Quest", "CharacterAction",
                "KnuBotStartTrade", "KnuBotEndTrade", "KnuBotTrade",
                "KnuBotFinishTrade", "InventoryUpdate", "ContainerAddItem"
            ):
                # also fuzzy
                if "Append" not in name and "Template" not in name and "Feedback" not in name and "Quest" not in name and "Trade" not in name:
                    continue
            hx = row.get("RawHex") or ""
            texts = decode_strings(hx)
            f.write("%s %s id=%s texts=%s\n" % (
                row.get("CapturedUtc"), name, row.get("IdentityInstance"), texts[:12]))
            if "Append" in name or name == "FormatFeedback" or name == "TemplateAction":
                f.write("  HEX %s\n" % hx[:400])

    f.write("\n==== inventory ====\n")
    with open(os.path.join(cap, "inventory-updates.csv"), encoding="utf-8-sig", newline="") as cf:
        for row in csv.DictReader(cf):
            f.write(str(dict(row))[:400] + "\n")

print("wrote", out)
