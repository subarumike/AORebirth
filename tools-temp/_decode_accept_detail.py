from pathlib import Path
import csv
rows=list(csv.DictReader(Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260729-003950/raw-packets.csv").open(encoding="utf-8-sig",newline="")))
for idx in (132,136,161,167,171):
    r=rows[idx]
    hx=r["RawHex"].strip()
    b=bytes.fromhex(hx)
    i=b.find(bytes.fromhex("5E477770"))
    rest=b[i+4:]
    act=int.from_bytes(rest[9:13],"big")
    tt=int.from_bytes(rest[17:21],"big")
    ti=int.from_bytes(rest[21:25],"big")
    p1=int.from_bytes(rest[25:29],"big",signed=True)
    p2=int.from_bytes(rest[29:33],"big",signed=True)
    print(f"{idx} {r['Direction']} act=0x{act:X} tgt={tt:X}:{ti:X} p1={p1}({p1:#x}) p2={p2}")

# 003944 accept full hex
rows=list(csv.DictReader(Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260729-003944/raw-packets.csv").open(encoding="utf-8-sig",newline="")))
r=rows[43]
print("accept hex", r["RawHex"])
b=bytes.fromhex(r["RawHex"].strip())
i=b.find(bytes.fromhex("5E477770"))
rest=b[i+4:]
print("after type", rest.hex())
