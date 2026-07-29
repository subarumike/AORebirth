# Diff Assault Rifle tip body (ignore size prefix + mission id + expiry).
from pathlib import Path
import re

# Reconstruct old hex from TipSender by reading the C# string - easier: decode from known start
# Read current AssaultRifleHex from source
src = Path(r"AORebirth/Server/ZoneEngine/Core/Arete/Quests/AntonioStacklundTipSender.cs").read_text(encoding="utf-8")
m = re.search(r'AssaultRifleHex\s*=\s*((?:"[^"]+"\s*\+\s*)+"[^"]+");', src, re.S)
if not m:
    # fallback simpler
    m = re.search(r'private const string AssaultRifleHex =\s*((?:"[^"]+"\s*\+\s*)+);', src)
text = m.group(1)
parts = re.findall(r'"([0-9A-Fa-f]+)"', text)
old = bytes.fromhex("".join(parts))
new = bytes.fromhex(Path(r"tools-temp/_tmp_antonio_ar_054034.hex").read_text().strip())
print("old len", len(old), "new len", len(new))

def normalize(b, mid_old, mid_new, exp_old, exp_new):
    b = bytearray(b)
    # zero size prefix
    b[0] = 0
    b[1] = 0
    def repl(oldv, newv):
        ob = oldv.to_bytes(4, "big")
        nb = newv.to_bytes(4, "big")
        i = 0
        while True:
            j = b.find(ob, i)
            if j < 0:
                break
            b[j:j+4] = nb
            i = j + 4
    repl(mid_old, 0)
    repl(mid_new, 0)
    repl(exp_old, 0)
    repl(exp_new, 0)
    return bytes(b)

n_old = normalize(old, 0x5569CDBF, 0x556A8FC0, 0x5FA0E000, 0x5FFE3600)
n_new = normalize(new, 0x5569CDBF, 0x556A8FC0, 0x5FA0E000, 0x5FFE3600)
print("normalized equal?", n_old == n_new)
if n_old != n_new:
    # find first diff
    for i, (a, c) in enumerate(zip(n_old, n_new)):
        if a != c:
            print("first diff @", i, hex(a), hex(c))
            print("old ctx", n_old[max(0,i-8):i+16].hex())
            print("new ctx", n_new[max(0,i-8):i+16].hex())
            break
    print("len diff", len(n_old), len(n_new))
# extract mission objective text from both
for label, b in (("old", old), ("new", new)):
    t = b.split(b"Assemble a BO-18")[1] if b"Assemble a BO-18" in b else b""
    print(label, "has Worn Assault", b"Worn Assault Rifle" in b, "Fluid Sample", b"Fluid Sample" in b, "248347", b"248347" in b)
