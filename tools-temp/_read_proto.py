from pathlib import Path
p = Path(r"C:\Users\nermi\Desktop\zadaily\prototyprsymb.txt")
lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
print("lines", len(lines))
for line in lines[:12]:
    print(repr(line))
print("---")
ids = []
for line in lines:
    parts = line.replace("\t", " ").split()
    # expect Name ... QL ID or ID Name QL
    nums = [int(x) for x in parts if x.isdigit()]
    if len(nums) >= 2:
        # last two often ql and id, or id alone
        pass
    if nums:
        ids.append(nums[-1] if len(nums) >= 1 else 0)
print("sample nums last per line:", [ [int(x) for x in line.replace('\t',' ').split() if x.isdigit()] for line in lines[:5]])
# try parse from prior extract script format
