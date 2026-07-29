from pathlib import Path
b = bytes.fromhex(Path(r"tools-temp/_tmp_chimera_corpse_template.hex").read_text())
print("len", len(b))
print("md330", int.from_bytes(b[330:334], "big"))
print("tail342", hex(int.from_bytes(b[342:346], "big")))
print("dead191", hex(int.from_bytes(b[191:195], "big")))
print("cash207", int.from_bytes(b[207:211], "big"))
print("cat199", int.from_bytes(b[199:203], "big"))
print("scale143", int.from_bytes(b[143:147], "big"))
print("sex159", int.from_bytes(b[159:163], "big"))
print("breed167", int.from_bytes(b[167:171], "big"))
print("race175", int.from_bytes(b[175:179], "big"))
# material count at 413
print("mat413", b[413:418].hex())
