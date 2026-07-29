# Parse PAF payload for repair capture and dump chest/dispenser hex
from pathlib import Path
import re

text = Path(r"tools-temp/_tmp_repair_machine_extract.txt").read_text(encoding="utf-8")
paf = None
dispenser = None
doors = []
chests = []
for line in text.splitlines():
    if line.startswith("PAF="):
        paf = line[4:]
    elif line.startswith("DISPENSER=") and not line.startswith("DISPENSERline"):
        dispenser = line[10:]
    elif line.startswith("DOOR_"):
        doors.append(line.split("=",1)[1])
    elif line.startswith("CHEST_"):
        chests.append(line.split("=",1)[1])

print("doors", len(doors), "chests", len(chests))
print("dispenser", dispenser is not None, "len", len(dispenser)//2 if dispenser else 0)
if dispenser:
    print("DISPENSER_HEX")
    print(dispenser)

raw = bytes.fromhex(paf)
# find C79F
i = raw.find(bytes.fromhex("C79F"))
print("PAF C79F at", i)
# payload often starts at C79F
# find second C79F or end
payload = raw[i:]
# trim trailing playfield ids - gold payload ends with FFFFFFFF
# From PAF structure after CharacterCoordinates:
# PlayfieldId1 type C79F + building + payload
print("from C79F", payload.hex())
# Extract like shape catalog: starts 00 00 C7 9F 00 D7 42 5E ...
idx = paf.upper().find("0000C79F00D7425E")
print("payload start idx", idx)
# Read until we hit next 00009C50 or end of useful payload
# Gold payloads end with FFFFFFFF
end = paf.upper().find("FFFFFFFF", idx)
print("ffffffff", end)
chunk = paf[idx:end+8] if end > 0 else paf[idx:]
print("PAYLOAD", chunk)
print("payload bytes", len(chunk)//2)

# Print chest templates
for i, hx in enumerate(chests):
    raw = bytes.fromhex(hx)
    j = raw.find(bytes.fromhex("00000170"))  # StaticInstance stat?
    # find 0000027B
    k = hx.upper().find("0000027B")
    print("chest", i, "27B at", k, hx[k:k+16] if k>=0 else "?", "len", len(hx)//2)

# Write C# arrays
out = Path(r"tools-temp/_tmp_repair_cs_frag.txt")
with out.open("w", encoding="utf-8") as f:
    f.write("// Doors_1493063\n")
    for hx in doors:
        # normalize: strip leading packet seq - keep from 000? Actually DoorReplay uses full hex
        # Retarget replaces character instance; keep as-is but replace 7996C028 with placeholder pattern
        f.write('            "%s",\n' % hx)
    f.write("// Chests_1493063\n")
    for hx in chests:
        f.write('            "%s",\n' % hx)
    if dispenser:
        f.write("// Terminals_1493063\n")
        f.write('            "%s",\n' % dispenser)
    if chunk:
        f.write("// ACG payload bytes\n")
        b = bytes.fromhex(chunk)
        # if chunk starts mid-packet without 0000 prefix for C79F
        if not chunk.upper().startswith("0000C79F"):
            # find
            pass
        f.write("payload hex %s\n" % chunk)
print("wrote", out)
