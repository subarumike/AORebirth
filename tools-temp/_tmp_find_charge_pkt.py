import pathlib, csv, binascii
p = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-151431")
needle = b"Charge!"
needle2 = b"follow you wherever"
out=[]
with (p/"raw-packets.csv").open(encoding="utf-8-sig", newline="") as fh:
    r=csv.DictReader(fh)
    for row in r:
        hx = row.get("RawHex") or ""
        try:
            raw = binascii.unhexlify(hx)
        except Exception:
            continue
        if needle in raw or needle2 in raw:
            out.append("%s %s %s n3=%s len=%s hex=%s" % (
                row.get("CapturedUtc"), row.get("Direction"), row.get("N3TypeName"),
                row.get("N3TypeValue"), row.get("PacketLength"), hx[:200]))

# Also search packets.hex.log
ph = p/"packets.hex.log"
if ph.exists():
    text = ph.read_text(encoding="utf-8", errors="replace")
    for line in text.splitlines():
        if "436861726765" in line or "Charge" in line or "666f6c6c6f7720796f75" in line.lower():
            out.append("HEXLOG "+line[:300])

(p/"_pet_chat_packet.txt").write_text("\n".join(out) if out else "NONE", encoding="utf-8")
print("matches", len(out))
for x in out[:20]:
    print(x[:250])
