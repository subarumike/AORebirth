# -*- coding: utf-8 -*-
import pathlib, csv, binascii, struct, re
p = pathlib.Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260730-234537")

# Find PetCommand packets and map to chat lines chronologically
print("=== chat-dialogue SystemMessage all ===")
cd = p / "chat-dialogue.log"
for line in cd.read_text(encoding="utf-8-sig", errors="replace").splitlines():
    if "SystemMessage" in line or "Health:" in line or "Many tasks" in line:
        print(line[:400])

print("\n=== PetCommand from events/raw ===")
# events PetCommand
ev = (p/"events.log").read_text(encoding="utf-8-sig", errors="replace")
for m in re.finditer(r".{0,40}PetCommand.{0,200}", ev):
    print(m.group(0)[:280])

print("\n=== raw PetCommand Unknown2 ===")
csv_path = p/"raw-packets.csv"
if csv_path.exists():
    with csv_path.open(encoding="utf-8-sig", newline="") as fh:
        for row in csv.DictReader(fh):
            if (row.get("N3TypeName") or "") != "PetCommand":
                continue
            hx = (row.get("RawHex") or "").replace(" ","")
            raw = binascii.unhexlify(hx)
            # last ints
            ints = [struct.unpack(">I", raw[i:i+4])[0] for i in range(len(raw)-16, len(raw)-3, 4)]
            print(row.get("CapturedUtc"), "tail", ints, "hexend", hx[-48:])
