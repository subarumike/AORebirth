import pathlib, csv, binascii, re
p = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-151431")
out=[]

# Search ALL hex sinks for Charge / follow text
needles = [b"Charge!", b"follow you wherever", b"Catcraty", b"Bureaucrat Worker:"]
for fname in ["raw-packets.csv", "packets.hex.log"]:
    f = p/fname
    if not f.exists():
        continue
    out.append("=== search "+fname)
    if fname.endswith(".csv"):
        with f.open(encoding="utf-8-sig", newline="") as fh:
            r=csv.DictReader(fh)
            for row in r:
                hx=row.get("RawHex") or ""
                try: raw=binascii.unhexlify(hx)
                except: continue
                if any(n in raw for n in needles):
                    out.append("%s %s %s %s" % (row.get("CapturedUtc"), row.get("Direction"), row.get("N3TypeName"), hx[:180]))
    else:
        for line in f.read_text(encoding="utf-8", errors="replace").splitlines():
            low=line.lower()
            if "43686172676521" in low or "666f6c6c6f7720796f75" in low or "catcraty" in low:
                out.append(line[:300])

# Decode SpecialAttackWeapon hex fully for first engage
out.append("=== SAW hex decode")
saw="6068000A0001004500000DC179AA68071D3C0F1C0000C35079AA2FEE00000007E20001D7520001D75342555731425557310000001000000010000000"
# also get full from csv
with (p/"raw-packets.csv").open(encoding="utf-8-sig", newline="") as fh:
    r=csv.DictReader(fh)
    for row in r:
        if row.get("N3TypeName")=="SpecialAttackWeapon" and "79AA2FEE" in (row.get("RawHex") or ""):
            out.append(row.get("CapturedUtc")+" FULL="+row.get("RawHex"))
            break

# Chat dialogue raw lines
out.append("=== chat-dialogue full")
out.append((p/"chat-dialogue.log").read_text(encoding="utf-8-sig", errors="replace"))

(p/"_pet_chat_deep.txt").write_text("\n".join(out), encoding="utf-8")
print("lines", len(out))
for x in out[:40]:
    print(x[:220])
