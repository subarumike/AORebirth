import subprocess
out = subprocess.check_output(["tasklist", "/V", "/FO", "CSV"], text=True, errors="replace")
for line in out.splitlines():
    if "Anarchy" in line or ",\"5000\"," in line or "5000" in line.split(",")[1:2]:
        print(line)
# also print any line containing 5000 as pid field
import csv
from io import StringIO
r = csv.reader(StringIO(out))
header = next(r)
print("header", header)
for row in r:
    if len(row) < 2:
        continue
    name, pid = row[0], row[1]
    if pid == "5000" or "Anarchy" in name or "AOSharp" in name:
        print(pid, name, row[-1] if row else "")
