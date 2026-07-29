from pathlib import Path
lines = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-shiny-sword-nano/packets.hex.log").read_text(encoding="utf-8-sig").splitlines()
for ln in lines:
    if "QuestFullUpdate hex=" in ln:
        hx = ln.split("hex=")[1].strip()
        Path(r"tools-temp/_tmp_shiny_qfu.hex").write_text(hx)
        raw = bytes.fromhex(hx)
        print("len", len(hx), "bytes", len(raw))
        print("mission", [i for i in range(len(raw) - 3) if int.from_bytes(raw[i:i+4], "big") == 0x5565CD87])
        print("player", [i for i in range(len(raw) - 3) if int.from_bytes(raw[i:i+4], "big") == 0x7995EF26])
        print("D2F1", [i for i in range(len(raw) - 1) if raw[i] == 0xD2 and raw[i+1] == 0xF1])
        # Leonora deliver expiry at 464; compare structure after D2F1
        for i in [j for j in range(len(raw) - 1) if raw[j] == 0xD2 and raw[j+1] == 0xF1]:
            print("after D2F1", i, raw[i:i+24].hex())
            print("ints", [int.from_bytes(raw[i+k:i+k+4], "big") for k in range(0, 24, 4)])
        # AbsoluteTime zero area - find non-zero near end like 5DF2C3
        for i in range(len(raw) - 3):
            v = int.from_bytes(raw[i:i+4], "big")
            if 0x5DF00000 <= v <= 0x5E000000:
                print("expiry-like", i, hex(v))
        break
