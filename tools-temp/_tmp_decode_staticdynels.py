import struct
import subprocess

PLAYFIELDS = list(range(4676, 4700)) + list(range(4310, 4314))

sql = (
    "SELECT Id, Instance, Playfield, X, Y, Z, HEX(stats) "
    "FROM staticdynels WHERE Playfield IN ("
    + ",".join(str(p) for p in PLAYFIELDS)
    + ") ORDER BY Playfield, Id;"
)

proc = subprocess.run(
    [r"C:\xampp\mysql\bin\mysql.exe", "-u", "root", "cellao_codex_test", "-N", "-B", "-e", sql],
    capture_output=True,
    text=True,
    check=True,
)


def read_string(data, i):
    b = data[i]
    if (b & 0xE0) == 0xA0:
        ln = b & 0x1F
        i += 1
    elif b == 0xD9:
        ln = data[i + 1]
        i += 2
    elif b == 0xDA:
        ln = struct.unpack_from(">H", data, i + 1)[0]
        i += 3
    else:
        return None, i
    s = data[i : i + ln].decode("ascii", errors="replace")
    return s, i + ln


def read_value(data, i):
    b = data[i]
    if b == 0xCE:
        return struct.unpack_from(">I", data, i + 1)[0], i + 5
    if b == 0xD2:
        return struct.unpack_from(">i", data, i + 1)[0], i + 5
    if b == 0x01:
        return 1, i + 1
    if b == 0x00:
        return 0, i + 1
    return b, i + 1


def decode_template(hexstats):
    data = bytes.fromhex(hexstats)
    i = 0
    b = data[i]
    if b in (0x96, 0x98, 0x99):
        count = b & 0x0F
        i += 1
    elif b == 0xDE:
        count = struct.unpack_from(">H", data, 1)[0]
        i = 3
    elif b == 0xDF:
        count = struct.unpack_from(">I", data, 1)[0]
        i = 5
    else:
        return None

    template = None
    for _ in range(count):
        key, i = read_string(data, i)
        if key is None:
            break
        val, i = read_value(data, i)
        if key in ("ACGItemTemplateID", "StaticInstance"):
            template = val
    return template


print("Id\tInstance\tPlayfield\tX\tY\tZ\tTemplate")
for line in proc.stdout.splitlines():
    parts = line.split("\t")
    if len(parts) < 7:
        continue
    template = decode_template(parts[6])
    print(f"{parts[0]}\t{parts[1]}\t{parts[2]}\t{parts[3]}\t{parts[4]}\t{parts[5]}\t{template}")
