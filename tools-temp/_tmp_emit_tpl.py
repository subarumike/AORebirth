from pathlib import Path
hexstr = Path(r"tools-temp/_tmp_chimera_corpse_template.hex").read_text().strip().upper()
# emit C# concatenated string chunks of 120 chars
chunks = [hexstr[i:i+120] for i in range(0, len(hexstr), 120)]
for i, ch in enumerate(chunks):
    suffix = "" if i == len(chunks) - 1 else '"'
    prefix = '"' if i == 0 else '+ "'
    if i == len(chunks) - 1:
        print(f'{prefix}{ch}");')
    else:
        print(f'{prefix}{ch}"')
print("total_bytes", len(hexstr)//2)
