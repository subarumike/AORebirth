import re
from pathlib import Path

dll = Path(r"C:\Funcom\Anarchy Online\GUI.dll")
data = dll.read_bytes()
print("GUI.dll size", len(data))

needles = [
    b"uwg.store",
    b"uwg.daily",
    b"uwg.trade",
    b"aomarket",
    b"vgtp",
    b"icc-rk",
    b"index.app",
    b"Item Store",
    b"Daily Login",
    b"shop is currently",
    b"temporarily unavailable",
    b"temporily unavailable",
]

for n in needles:
    idx = 0
    count = 0
    while True:
        i = data.find(n, idx)
        if i < 0:
            break
        count += 1
        start = max(0, i - 80)
        end = min(len(data), i + len(n) + 120)
        chunk = data[start:end]
        printable = "".join(chr(b) if 32 <= b < 127 else "." for b in chunk)
        print(f"\n=== {n.decode()} @{i} ===")
        print(printable)
        idx = i + 1
        if count >= 8:
            print("...(more)")
            break
    if count == 0:
        print(f"NO {n.decode()}")

# also extract all uwg.* and *.icc-rk / *.omni-rk ascii strings
pat = re.compile(rb"[ -~]{0,40}(?:uwg\.|vgtp://|icc-rk|aomarket|index\.app)[ -~]{0,80}")
found = sorted(set(m.group(0).decode("latin1", "replace") for m in pat.finditer(data)))
print("\n=== regex hits", len(found), "===")
for s in found[:80]:
    print(s)
