atk = bytes.fromhex(
    open(
        r"c:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260710-185528\packets.hex.log",
        encoding="utf-8",
    )
    .readlines()[45]
    .split("hex=")[1]
    .strip()
)[31:]
idx = atk.index(bytes.fromhex("c35035fe28680000"))
print("identity offset", idx)
print("pre identity", atk[idx - 16 : idx].hex())
print("len", len(atk))
