from pathlib import Path
c = Path(r"tools-temp/_tmp_chimera_corpse_template.hex").read_text().strip().upper()
i = c.find("01000007E26C6F7732")
print(c[i:])
print("len", len(c[i:]) // 2)
