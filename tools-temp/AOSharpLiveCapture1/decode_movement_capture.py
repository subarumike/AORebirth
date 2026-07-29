import csv
import json
import os
import re
import struct
import sys
from datetime import datetime, timezone


FOLLOW_TARGET = 0x260F3671
SET_POS = 0x195E496E
STOP_MOVING_CMD = 0x742E2314

IDENTITY_TYPES = {
    50000: "SimpleChar",
}

HEADER = [
    "CapturedUtc",
    "Direction",
    "Sequence",
    "MessageType",
    "SourceType",
    "SourceInstance",
    "SourceIdentity",
    "SourceName",
    "TargetType",
    "TargetInstance",
    "TargetIdentity",
    "TargetName",
    "FollowKind",
    "CurrentX",
    "CurrentY",
    "CurrentZ",
    "DestinationX",
    "DestinationY",
    "DestinationZ",
    "Speed",
    "Animation",
    "Flags",
    "PathCount",
    "RawParams",
    "RawTailHex",
]

LINE_RE = re.compile(
    r"^(?P<time>\S+)\s+(?P<direction>\S+)\s+#(?P<sequence>\d+)\s+len=(?P<length>\d+)\s+n3=(?P<n3>\S+)\s+hex=(?P<hex>[0-9A-Fa-f]+)"
)


def read_u32(data, offset):
    return struct.unpack_from(">I", data, offset)[0]


def read_i32(data, offset):
    return struct.unpack_from(">i", data, offset)[0]


def read_f32(data, offset):
    return struct.unpack_from(">f", data, offset)[0]


def fmt_float(value):
    return format(value, ".9g")


def identity_type_name(value):
    return IDENTITY_TYPES.get(value, str(value))


def instance_hex(value):
    return format(value, "08X")


def identity_text(identity_type, instance):
    return identity_type_name(identity_type) + ":" + instance_hex(instance)


def read_identity(data, offset):
    if len(data) < offset + 8:
        return None
    return read_u32(data, offset), read_u32(data, offset + 4)


def tail_hex(data, offset):
    if offset >= len(data):
        return ""
    return data[offset:].hex().upper()


def base_row(timestamp, direction, sequence, message_type, source):
    source_type, source_instance = source
    return {
        "CapturedUtc": timestamp,
        "Direction": direction,
        "Sequence": sequence,
        "MessageType": message_type,
        "SourceType": identity_type_name(source_type),
        "SourceInstance": instance_hex(source_instance),
        "SourceIdentity": identity_text(source_type, source_instance),
        "SourceName": "",
        "TargetType": "",
        "TargetInstance": "",
        "TargetIdentity": "",
        "TargetName": "",
        "FollowKind": "",
        "CurrentX": "",
        "CurrentY": "",
        "CurrentZ": "",
        "DestinationX": "",
        "DestinationY": "",
        "DestinationZ": "",
        "Speed": "",
        "Animation": "",
        "Flags": "",
        "PathCount": "",
        "RawParams": "",
        "RawTailHex": "",
    }


def decode_follow_target(timestamp, direction, sequence, data, counts):
    source = read_identity(data, 20)
    if source is None or len(data) < 31:
        counts["decodeErrors"] += 1
        return None

    base_unknown = data[28]
    follow_type = data[29]
    follow_unknown = data[30]
    flags = "base_unknown={0};follow_type={1};follow_unknown={2}".format(
        base_unknown, follow_type, follow_unknown
    )
    row = base_row(timestamp, direction, sequence, "FollowTarget", source)
    row["Animation"] = str(follow_unknown)
    row["Flags"] = flags
    counts["followTargetPackets"] += 1

    if follow_type == 1:
        row["FollowKind"] = "NpcPath"
        if len(data) < 32:
            row["RawParams"] = flags + ";path_count=missing"
            counts["decodeErrors"] += 1
            return row

        path_count = data[31]
        coordinate_offset = 32
        available = max(0, (len(data) - coordinate_offset) // 12)
        decoded = min(path_count, available)
        row["PathCount"] = str(path_count)

        if decoded:
            row["CurrentX"] = fmt_float(read_f32(data, coordinate_offset))
            row["CurrentY"] = fmt_float(read_f32(data, coordinate_offset + 4))
            row["CurrentZ"] = fmt_float(read_f32(data, coordinate_offset + 8))
            destination_offset = coordinate_offset + (decoded - 1) * 12
            row["DestinationX"] = fmt_float(read_f32(data, destination_offset))
            row["DestinationY"] = fmt_float(read_f32(data, destination_offset + 4))
            row["DestinationZ"] = fmt_float(read_f32(data, destination_offset + 8))

        tail_offset = coordinate_offset + decoded * 12
        row["RawParams"] = "{0};path_count={1};decoded_path_count={2}".format(
            flags, path_count, decoded
        )
        if decoded != path_count:
            row["RawParams"] += ";truncated=true"
            counts["decodeErrors"] += 1
        elif path_count > 0:
            counts["usableFollowTargetPackets"] += 1

        row["RawTailHex"] = tail_hex(data, tail_offset)
        return row

    if follow_type == 2:
        row["FollowKind"] = "Target"
        target = read_identity(data, 31)
        tail_offset = 31
        raw_params = flags
        if target is not None:
            target_type, target_instance = target
            row["TargetType"] = identity_type_name(target_type)
            row["TargetInstance"] = instance_hex(target_instance)
            row["TargetIdentity"] = identity_text(target_type, target_instance)
            raw_params += ";target=" + row["TargetIdentity"]
            tail_offset = 39

        if len(data) >= 55:
            raw_params += (
                ";target_unknown1={0};target_unknown2={1};target_unknown3={2};target_unknown4={3}".format(
                    read_i32(data, 39),
                    read_i32(data, 43),
                    read_i32(data, 47),
                    read_i32(data, 51),
                )
            )
            tail_offset = 55

        row["RawParams"] = raw_params
        row["RawTailHex"] = tail_hex(data, tail_offset)
        return row

    row["FollowKind"] = "Type" + str(follow_type)
    row["RawParams"] = flags
    row["RawTailHex"] = tail_hex(data, 31)
    return row


def decode_set_pos(timestamp, direction, sequence, data, counts):
    source = read_identity(data, 20)
    if source is None or len(data) < 41:
        counts["decodeErrors"] += 1
        return None

    row = base_row(timestamp, direction, sequence, "SetPos", source)
    base_unknown = data[28]
    flags = "base_unknown=" + str(base_unknown)
    row["CurrentX"] = fmt_float(read_f32(data, 29))
    row["CurrentY"] = fmt_float(read_f32(data, 33))
    row["CurrentZ"] = fmt_float(read_f32(data, 37))
    row["Flags"] = flags
    row["RawParams"] = flags
    row["RawTailHex"] = tail_hex(data, 41)
    counts["setPosPackets"] += 1
    return row


def decode_stop_moving_cmd(timestamp, direction, sequence, data, counts):
    source = read_identity(data, 20)
    if source is None or len(data) < 41:
        counts["decodeErrors"] += 1
        return None

    row = base_row(timestamp, direction, sequence, "StopMovingCmd", source)
    base_unknown = data[28]
    row["Flags"] = "base_unknown=" + str(base_unknown)
    row["RawParams"] = "base_unknown={0};unknown1={1};unknown2={2};unknown3={3}".format(
        base_unknown,
        read_i32(data, 29),
        read_i32(data, 33),
        read_i32(data, 37),
    )
    row["RawTailHex"] = tail_hex(data, 41)
    counts["stopMovingCmdPackets"] += 1
    return row


def decode_line(line, counts):
    match = LINE_RE.match(line.strip())
    if not match:
        return None

    data = bytes.fromhex(match.group("hex"))
    if len(data) < 20:
        return None

    message_type = read_u32(data, 16)
    timestamp = match.group("time")
    direction = match.group("direction")
    sequence = match.group("sequence")

    if message_type == FOLLOW_TARGET:
        return decode_follow_target(timestamp, direction, sequence, data, counts)
    if message_type == SET_POS:
        return decode_set_pos(timestamp, direction, sequence, data, counts)
    if message_type == STOP_MOVING_CMD:
        return decode_stop_moving_cmd(timestamp, direction, sequence, data, counts)
    return None


def main():
    if len(sys.argv) != 2:
        print("usage: python decode_movement_capture.py <capture-folder>")
        return 2

    capture_dir = os.path.abspath(sys.argv[1])
    packet_log = os.path.join(capture_dir, "packets.hex.log")
    if not os.path.exists(packet_log):
        print("missing packets.hex.log: " + packet_log)
        return 1

    rows = []
    counts = {
        "movementPacketRows": 0,
        "followTargetPackets": 0,
        "usableFollowTargetPackets": 0,
        "setPosPackets": 0,
        "stopMovingCmdPackets": 0,
        "decodeErrors": 0,
    }

    with open(packet_log, "r", encoding="utf-8-sig", errors="replace") as handle:
        for line in handle:
            row = decode_line(line, counts)
            if row is not None:
                rows.append(row)

    counts["movementPacketRows"] = len(rows)

    csv_path = os.path.join(capture_dir, "movement-packets.csv")
    with open(csv_path, "w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=HEADER)
        writer.writeheader()
        writer.writerows(rows)

    summary_path = os.path.join(capture_dir, "movement-summary.json")
    summary = {
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "captureFolderPath": capture_dir,
        "movementPacketsCsv": csv_path,
        "followTargetDecodedWithUsablePath": counts["usableFollowTargetPackets"] > 0,
        "counts": counts,
    }
    with open(summary_path, "w", encoding="utf-8") as handle:
        json.dump(summary, handle, indent=2)
        handle.write("\n")

    print(
        "decoded movement rows={0} followTarget={1} usableFollowTarget={2} setPos={3} stopMovingCmd={4} errors={5}".format(
            counts["movementPacketRows"],
            counts["followTargetPackets"],
            counts["usableFollowTargetPackets"],
            counts["setPosPackets"],
            counts["stopMovingCmdPackets"],
            counts["decodeErrors"],
        )
    )
    print("movement CSV: " + csv_path)
    print("movement summary: " + summary_path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
