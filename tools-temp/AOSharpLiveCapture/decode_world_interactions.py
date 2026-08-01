import csv
import io
import os
import re
import struct
import sys


N3_TELEPORT = 0x43197D22
PLAYFIELD_ANARCHY_F = 0x5F4B1A39
GENERIC_CMD = 0x52526858
DOOR_STATUS_UPDATE = 0x4C7D403B
CHEST_FULL_UPDATE = 0x465A5D73

LINE_RE = re.compile(
    r"^(?P<time>\S+)\s+(?P<direction>\S+)\s+#(?P<sequence>\d+)\s+"
    r"len=(?P<length>\d+)\s+n3=(?P<n3>\S+)\s+hex=(?P<hex>[0-9A-Fa-f]+)"
)

HEADER = [
    "CapturedUtc",
    "Direction",
    "Sequence",
    "MessageType",
    "SourceIdentity",
    "Action",
    "TargetIdentity",
    "Details",
    "RawTailHex",
]


def read_i32(data, offset):
    return struct.unpack_from(">i", data, offset)[0]


def read_u32(data, offset):
    return struct.unpack_from(">I", data, offset)[0]


def read_f32(data, offset):
    return struct.unpack_from(">f", data, offset)[0]


def identity(data, offset):
    return read_u32(data, offset), read_u32(data, offset + 4)


def identity_text(value):
    return "{0}:{1:08X}".format(value[0], value[1])


def vector(data, offset):
    return tuple(read_f32(data, offset + index * 4) for index in range(3))


def quaternion(data, offset):
    return tuple(read_f32(data, offset + index * 4) for index in range(4))


def floats(value):
    return ",".join(format(component, ".9g") for component in value)


def base_row(timestamp, direction, sequence, message_type, source):
    return {
        "CapturedUtc": timestamp,
        "Direction": direction,
        "Sequence": sequence,
        "MessageType": message_type,
        "SourceIdentity": identity_text(source),
        "Action": "",
        "TargetIdentity": "",
        "Details": "",
        "RawTailHex": "",
    }


def decode_teleport(timestamp, direction, sequence, data):
    if len(data) < 102:
        raise ValueError("truncated N3Teleport")
    row = base_row(timestamp, direction, sequence, "N3Teleport", identity(data, 20))
    payload_length = read_i32(data, 98)
    payload_end = 102 + max(payload_length, 0)
    if payload_end > len(data):
        raise ValueError("truncated N3Teleport payload")
    row["Details"] = (
        "destination={0};heading={1};playfield={2};game_server={3};sg={4:08X};"
        "change_playfield={5};unknown4={6};unknown5={7};playfield2={8};payload_length={9}"
    ).format(
        floats(vector(data, 29)),
        floats(quaternion(data, 41)),
        identity_text(identity(data, 58)),
        read_i32(data, 66),
        read_u32(data, 70),
        identity_text(identity(data, 74)),
        read_i32(data, 82),
        read_i32(data, 86),
        identity_text(identity(data, 90)),
        payload_length,
    )
    row["RawTailHex"] = data[102:payload_end].hex().upper()
    return row


def decode_playfield(timestamp, direction, sequence, data):
    if len(data) < 70:
        raise ValueError("truncated PlayfieldAnarchyF")
    row = base_row(
        timestamp,
        direction,
        sequence,
        "PlayfieldAnarchyF",
        identity(data, 20),
    )
    row["Details"] = (
        "unknown1={0};coordinates={1};playfield1={2};unknown3={3};unknown4={4:08X};"
        "playfield2={5};tail_length={6}"
    ).format(
        read_i32(data, 29),
        floats(vector(data, 33)),
        identity_text(identity(data, 46)),
        read_i32(data, 54),
        read_u32(data, 58),
        identity_text(identity(data, 62)),
        len(data) - 70,
    )
    row["RawTailHex"] = data[70:].hex().upper()
    return row


def decode_generic_cmd(timestamp, direction, sequence, data):
    if len(data) != 61:
        raise ValueError("unsupported GenericCmd length")
    row = base_row(timestamp, direction, sequence, "GenericCmd", identity(data, 20))
    row["Action"] = str(read_i32(data, 37))
    row["TargetIdentity"] = identity_text(identity(data, 53))
    row["Details"] = "temp1={0};count={1};temp4={2};user={3}".format(
        read_i32(data, 29),
        read_i32(data, 33),
        read_i32(data, 41),
        identity_text(identity(data, 45)),
    )
    return row


def decode_simple(timestamp, direction, sequence, data, message_type):
    row = base_row(timestamp, direction, sequence, message_type, identity(data, 20))
    row["Details"] = "packet_length={0}".format(len(data))
    row["RawTailHex"] = data[29:].hex().upper()
    return row


def decode_line(line):
    match = LINE_RE.match(line.strip())
    if not match:
        return None
    data = bytes.fromhex(match.group("hex"))
    if len(data) < 29:
        return None
    message_type = read_u32(data, 16)
    args = (match.group("time"), match.group("direction"), match.group("sequence"), data)
    if message_type == N3_TELEPORT:
        return decode_teleport(*args)
    if message_type == PLAYFIELD_ANARCHY_F:
        return decode_playfield(*args)
    if message_type == GENERIC_CMD:
        return decode_generic_cmd(*args)
    if message_type == DOOR_STATUS_UPDATE:
        return decode_simple(*args, "DoorStatusUpdate")
    if message_type == CHEST_FULL_UPDATE:
        return decode_simple(*args, "ChestFullUpdate")
    return None


def main():
    if len(sys.argv) not in (2, 3):
        print("usage: python decode_world_interactions.py <capture-folder> [output.csv|-]")
        return 2
    packet_log = os.path.join(os.path.abspath(sys.argv[1]), "packets.hex.log")
    if not os.path.exists(packet_log):
        print("missing packets.hex.log: " + packet_log)
        return 1

    rows = []
    errors = 0
    previous_legacy = None
    with open(packet_log, "r", encoding="utf-8-sig", errors="replace") as handle:
        for line in handle:
            match = LINE_RE.match(line.strip())
            legacy_signature = None
            if match:
                legacy_signature = (
                    match.group("time"),
                    match.group("direction"),
                    match.group("length"),
                    match.group("n3"),
                    match.group("hex"),
                )
            if legacy_signature is not None and legacy_signature == previous_legacy:
                continue
            previous_legacy = legacy_signature
            try:
                row = decode_line(line)
            except (ValueError, struct.error):
                errors += 1
                continue
            if row is not None:
                rows.append(row)

    output = sys.argv[2] if len(sys.argv) == 3 else os.path.join(
        os.path.abspath(sys.argv[1]), "world-interactions.csv"
    )
    if output == "-":
        target = io.StringIO()
        writer = csv.DictWriter(target, fieldnames=HEADER)
        writer.writeheader()
        writer.writerows(rows)
        sys.stdout.write(target.getvalue())
    else:
        with open(output, "w", newline="", encoding="utf-8") as handle:
            writer = csv.DictWriter(handle, fieldnames=HEADER)
            writer.writeheader()
            writer.writerows(rows)
        print("world interaction CSV: " + os.path.abspath(output))
    print("decoded world interactions={0} errors={1}".format(len(rows), errors), file=sys.stderr)
    return 0 if errors == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
