# -*- coding: utf-8 -*-
"""Extract ExtTex bytes for wildlife SCFUs from capture packets.hex.log"""
from __future__ import print_function
import re
from pathlib import Path

cap = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260726-spawn-mob-tll-alien")
# Prefer events for identity+name, then pull hex from packets by identity
# Marker for ExtTex: 00 00 07 E2 followed by material name

hexlog = cap / "packets.hex.log"
# Also try raw-packets.csv for payload hex columns
raw = cap / "raw-packets.csv"

# From events, known identities:
# Angry Minibull 7999110A, Saltworm 7999124A, Alien Spider - search
# ExtTex pattern in SCFU payload often: 000007E2 + ascii

def extract_exttex_candidates(data_hex):
    h = re.sub(r"[^0-9A-Fa-f]", "", data_hex).upper()
    out = []
    # look for 000007E2
    for m in re.finditer("000007E2", h):
        start = m.start()
        # take 48 or 64 bytes (96/128 hex chars) typical ExtTex blob
        for length in (48, 64, 80, 96):
            chunk = h[start:start + length * 2]
            if len(chunk) < length * 2:
                continue
            try:
                b = bytes.fromhex(chunk)
            except Exception:
                continue
            # must have printable-ish material name after 07E2
            name = b[4:20]
            if b[0:4] == b"\x00\x00\x07\xe2":
                out.append((length, b, name.split(b"\x00", 1)[0]))
    return out

# Scan packets.hex.log for lines containing MonsterData hex for 30360 / 17712 / 247728
# 30360 = 0x7698, 17712=0x4530, 247728=0x3C7B0
targets = {
    "minibull": "00007698",  # big-endian monsterdata in some layouts - try both
    "minibull_le": "98760000",
    "saltworm": "00004530",
    "saltworm_le": "30450000",
    "spider": "0003C7B0",
    "spider_le": "B0C70300",
}

found = {k: [] for k in ("minibull", "saltworm", "spider", "rollerrat")}
if hexlog.exists():
    with hexlog.open(encoding="utf-8", errors="replace") as f:
        for i, line in enumerate(f):
            u = line.upper().replace(" ", "")
            kind = None
            if "00007698" in u or "98760000" in u:
                # also require name ASCII ANGRY or similar - minibull monsterdata
                if "414E475259" in u or "4D494E4942" in u or True:
                    kind = "minibull"
            if "00004530" in u or "30450000" in u:
                if "53414C5457" in u or True:  # SALTW
                    kind = "saltworm" if kind is None else kind
            if "414E475259204D494E4942554C4C" in u:  # "ANGRY MINIBULL"
                kind = "minibull"
            if "53414C54574F524D" in u:  # SALTWORM
                kind = "saltworm"
            if "414C49454E20535049444552" in u:  # ALIEN SPIDER
                kind = "spider"
            if "524F4C4C4552524154" in u and "000007E2" in u:
                kind = "rollerrat"
            if kind and "000007E2" in u:
                for length, b, name in extract_exttex_candidates(u):
                    if length == 48:
                        found[kind].append((name, b.hex(), b))
                        break
            if all(found[k] for k in ("minibull", "saltworm", "spider")):
                break
            if i > 200000:
                break

out = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_tll_exttex_out.txt")
lines = []
for k, vals in found.items():
    lines.append("=== %s count=%d ===" % (k, len(vals)))
    seen = set()
    for name, hx, b in vals[:5]:
        key = hx
        if key in seen:
            continue
        seen.add(key)
        lines.append(" name=%r" % name)
        lines.append(" hex=%s" % hx)
        # C# bytes
        arr = ", ".join("0x%02X" % x for x in b)
        lines.append(" csharp={ %s }" % arr)
lines.append("done")
out.write_text("\n".join(lines), encoding="utf-8")
print(out.read_text(encoding="utf-8")[:3000])
