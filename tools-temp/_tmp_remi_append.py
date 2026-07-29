# Extract Remi KnubotAppendText strings from packets.hex.log
import os
import re
import binascii

cap = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260727-204902"
out = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_remi_append.txt"
hexlog = os.path.join(cap, "packets.hex.log")

# Remi instance bytes
remi = bytes.fromhex("78E0FC75")
# Also search AnswerList / Append around times

def extract_ascii(b):
    # length-prefixed BE short strings
    out = []
    i = 0
    while i + 2 < len(b):
        n = (b[i] << 8) | b[i + 1]
        if 3 <= n <= 400 and i + 2 + n <= len(b):
            chunk = b[i + 2 : i + 2 + n]
            if all(32 <= c < 127 or c in (9, 10, 13) for c in chunk):
                out.append(chunk.decode("ascii"))
                i += 2 + n
                continue
        i += 1
    return out

with open(out, "w", encoding="utf-8") as f:
    # raw-packets AppendText
    import csv
    with open(os.path.join(cap, "raw-packets.csv"), encoding="utf-8-sig", newline="") as cf:
        for row in csv.DictReader(cf):
            name = row.get("N3TypeName") or ""
            if "Append" not in name and "AnswerList" not in name:
                continue
            hx = (row.get("RawHex") or "").replace(" ", "")
            if not hx:
                continue
            b = binascii.unhexlify(hx)
            texts = extract_ascii(b)
            if texts:
                f.write("%s %s\n" % (row.get("CapturedUtc"), name))
                for t in texts:
                    f.write("  >> %s\n" % t)
                f.write("\n")

print("wrote", out)
