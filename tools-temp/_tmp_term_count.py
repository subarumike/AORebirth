# Count Terminal/SimpleItem in shape capture + extract all unique terminal SIFUs from 181214
from __future__ import print_function
import csv, collections, os

caps = [
    r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260719-5-different-shape-fo-mish",
    r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-181214",
]
for CAP in caps:
    print("===", os.path.basename(CAP))
    counts=collections.Counter()
    terminals=[]
    with open(os.path.join(CAP,"raw-packets.csv"),newline="",encoding="utf-8-sig") as f:
        for r in csv.DictReader(f):
            if (r.get("Direction") or "").upper()!="IN":
                continue
            n3=(r.get("N3TypeName") or "").strip()
            counts[n3]+=1
            hx=(r.get("RawHex") or "").strip().upper().replace(" ","")
            if n3=="SimpleItemFullUpdate" and "0000C73D" in hx:
                terminals.append(hx)
    print(" SimpleItemFullUpdate", counts.get("SimpleItemFullUpdate",0))
    print(" ChestFullUpdate", counts.get("ChestFullUpdate",0))
    print(" DoorFullUpdate", counts.get("DoorFullUpdate",0))
    print(" PlayfieldAnarchyF", counts.get("PlayfieldAnarchyF",0))
    print(" terminal SIFU", len(terminals), "unique", len(set(terminals)))
    # static instances
    sis=collections.Counter()
    for hx in set(terminals):
        i=hx.find("00000020")  # wrong
        # StaticInstance often after Flags; look for 00018806 pattern or parse after playfield
        # From radar: 00018806 appears as static
        import re
        for m in re.finditer(r"0000([0-9A-F]{4})000002BD", hx):
            pass
        idx=hx.find("000002BD")
        if idx>=8:
            # preceding int may be static? radar: 00018806000002BD
            sis[hx[idx-8:idx]] += 1
    print(" before 02BD:", sis.most_common(10))
