import os, csv, re

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-093728"
out = open(r"tools-temp\_tmp_vgtp_out.txt", "w", encoding="utf-8")

def p(*a):
    out.write(" ".join(str(x) for x in a) + "\n")

p("exists", os.path.isdir(cap), "files", sorted(os.listdir(cap)) if os.path.isdir(cap) else None)

pat = re.compile(rb"vgtp://[a-zA-Z0-9._\-/]+", re.I)
ascii_pat = re.compile(r"vgtp://[^\s\"'<>]+|uwg\.(store|daily)[^\s\"'<>]*|icc-rk", re.I)

# text logs
for name in sorted(os.listdir(cap)) if os.path.isdir(cap) else []:
    path = os.path.join(cap, name)
    if not os.path.isfile(path):
        continue
    if name.endswith((".csv", ".log", ".json", ".txt")):
        try:
            text = open(path, encoding="utf-8-sig", errors="replace").read()
        except Exception as e:
            p("read fail", name, e)
            continue
        hits = ascii_pat.findall(text)
        # findall with groups returns tuples for some patterns - normalize
        found = set()
        for m in ascii_pat.finditer(text):
            found.add(m.group(0))
        if found:
            p("====", name)
            for h in sorted(found):
                p(" ", h)

# raw hex packets for vgtp strings
raw = os.path.join(cap, "raw-packets.csv")
if os.path.isfile(raw):
    rows = list(csv.DictReader(open(raw, encoding="utf-8-sig", errors="replace")))
    p("==== raw-packets vgtp")
    n = 0
    seen = set()
    for r in rows:
        hx = r.get("RawHex") or ""
        if not hx or "76677470" not in hx.lower():  # 'vgtp' ascii hex
            # also search decoded
            continue
        try:
            b = bytes.fromhex(hx)
        except Exception:
            continue
        for m in pat.finditer(b):
            s = m.group(0).decode("ascii", errors="replace")
            key = (r.get("CapturedUtc"), r.get("N3TypeName"), s)
            if key in seen:
                continue
            seen.add(key)
            p(r.get("CapturedUtc"), r.get("Direction"), r.get("N3TypeName"), s)
            n += 1
    # broader: any packet with vgtp ascii regardless of hex filter miss
    if n == 0:
        for r in rows:
            hx = r.get("RawHex") or ""
            if not hx:
                continue
            try:
                b = bytes.fromhex(hx)
            except Exception:
                continue
            if b"vgtp://" in b or b"uwg." in b:
                for m in pat.finditer(b):
                    s = m.group(0).decode("ascii", errors="replace")
                    p(r.get("CapturedUtc"), r.get("Direction"), r.get("N3TypeName"), s)
                    n += 1
    p("raw hits", n)

out.close()
print("done")
