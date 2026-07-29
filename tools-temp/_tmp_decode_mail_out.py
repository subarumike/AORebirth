# Decode OUT Mail packets from capture 20260714-182726 raw-packets.csv
import csv
path = r'tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260714-182726/raw-packets.csv'
# find header
with open(path, 'r', encoding='utf-8', errors='ignore') as f:
    reader = csv.reader(f)
    header = next(reader)
    print('header', header)
    for row in reader:
        line = ','.join(row)
        if 'Mail' not in line:
            continue
        if 'OUT' not in line and 'Out' not in line:
            continue
        print(row[:12])
        # find hex payload column
        for i, col in enumerate(row):
            if len(col) > 40 and all(c in '0123456789abcdefABCDEF' for c in col.replace(' ','')):
                hexdata = col.replace(' ','')
                raw = bytes.fromhex(hexdata)
                # find N3 type 0x333B2867
                print(' payload len', len(raw), 'hex head', hexdata[:80])
                # action is after n3type(4)+identity(8?)+unknown(1) = need actual layout
                # N3: type int32, identity (type int32 + instance int32), unknown byte, action int16
                if len(raw) >= 19:
                    import struct
                    # might have packet header before N3
                    for off in range(0, min(32, len(raw)-15)):
                        n3 = struct.unpack_from('>I', raw, off)[0]  # try BE
                        n3le = struct.unpack_from('<I', raw, off)[0]
                        if n3 == 0x333B2867 or n3le == 0x333B2867:
                            endian = '>' if n3 == 0x333B2867 else '<'
                            print(' found Mail at', off, 'endian', endian)
                            # read identity + unknown + action
                            o = off + 4
                            idType = struct.unpack_from(endian + 'I', raw, o)[0]; o += 4
                            idInst = struct.unpack_from(endian + 'I', raw, o)[0]; o += 4
                            unk = raw[o]; o += 1
                            action = struct.unpack_from(endian + 'h', raw, o)[0]; o += 2
                            print('  idType=%08X idInst=%08X unk=%d action=%d rest=%s' % (idType, idInst, unk, action, raw[o:o+16].hex()))
                break
        print('---')
