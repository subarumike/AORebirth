import re, collections
c = collections.Counter()
path = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260719-5-different-shape-fo-mish\events.log"
for line in open(path, encoding="utf-8", errors="replace"):
    m = re.search(r"identity=\((Terminal|Container):([0-9A-F]+)\) name=([^=]+) pos=", line)
    if m:
        c[(m.group(1), m.group(3).strip())] += 1
for k, v in sorted(c.items(), key=lambda x: (-x[1], x[0])):
    print(v, k[0], k[1])

print("---181214---")
c2 = collections.Counter()
path2 = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-181214\events.log"
for line in open(path2, encoding="utf-8", errors="replace"):
    m = re.search(r"identity=\((Terminal|Container):([0-9A-F]+)\) name=([^=]+) pos=", line)
    if m:
        c2[(m.group(1), m.group(3).strip())] += 1
for k, v in sorted(c2.items(), key=lambda x: (-x[1], x[0])):
    print(v, k[0], k[1])
