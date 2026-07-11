import struct

cap = bytes.fromhex(
    open(
        r"c:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260710-185528\packets.hex.log",
        encoding="utf-8",
    )
    .readlines()[9]
    .split("hex=")[1]
    .strip()
)[31:]
mid = cap[20:55]
for i in range(0, len(mid), 4):
    chunk = mid[i : i + 4]
    if len(chunk) == 4:
        print(i, chunk.hex(), struct.unpack("<I", chunk)[0])
