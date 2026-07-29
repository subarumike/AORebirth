from __future__ import print_function
import os, binascii

base = r"tools-temp\_tmp_mission_shapes_assets"
for name in sorted(os.listdir(base)):
    if not name.startswith("paf_"):
        continue
    path = os.path.join(base, name)
    lines = open(path).read().strip().splitlines()
    print("===", name, "count", len(lines))
    for i, hx in enumerate(lines):
        hx = hx.strip().replace(" ", "").upper()
        raw = binascii.unhexlify(hx)
        print(" pkt", i, "len", len(raw))
        # skip N3 header-ish: find identities
        # print first 80 bytes as hex groups
        print(" head", hx[:160])
        positions = []
        for j in range(0, len(raw) - 3):
            if raw[j:j+4] == b"\x00\x00\xc7\x9f":
                positions.append(j)
        print(" C79F@", positions)
        if positions:
            p = positions[-1]
            payload = raw[p:]
            print(" payloadLen", len(payload))
            print(" payloadHead", binascii.hexlify(payload[:32]).decode())
            print(" payloadTail", binascii.hexlify(payload[-16:]).decode())
            # building instance at +4
            bi = (payload[4] << 24) | (payload[5] << 16) | (payload[6] << 8) | payload[7]
            print(" buildingInst", hex(bi))
        # PlayfieldId after Unknown2: rough scan for 15A82E etc
        for needle, label in [(b"\x00\x15\xa8\x2e", "1419310"), (b"\x00\x15\xa8\x76", "1419382"), (b"\x00\x15\xa8\x47", "1419335")]:
            at = raw.find(needle)
            if at >= 0:
                print(" pf", label, "at", at, "prevType", binascii.hexlify(raw[at-4:at]).decode() if at >= 4 else "?")
