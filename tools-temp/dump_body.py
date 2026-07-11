line = open(
    r"c:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260710-185528\packets.hex.log",
    encoding="utf-8",
).readlines()[9]
body = bytes.fromhex(line.split("hex=")[1].strip())[32:]
for off in range(0, len(body), 4):
    chunk = body[off : off + 4]
    print(f"{off:3d}: {chunk.hex()}  {list(chunk)}")
