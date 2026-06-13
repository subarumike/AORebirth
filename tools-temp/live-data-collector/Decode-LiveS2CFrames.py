#!/usr/bin/env python3
"""Decode server-to-client N3 frames from AO packet captures."""

from __future__ import annotations

import argparse
import bisect
import csv
import json
import re
import struct
import zlib
from collections import Counter
from pathlib import Path
from typing import Any


DEFAULT_REPO_ROOT = Path(__file__).resolve().parents[2]


def n3_type_file(repo_root: Path) -> Path:
    return (
        repo_root
        / "CellAO"
        / "Libraries"
        / "Source"
        / "AOtomation"
        / "AOtomation.Messaging"
        / "src"
        / "SmokeLounge.AOtomation.Messaging"
        / "Messages"
        / "N3MessageType.cs"
    )

IDENTITY_NAMES = {
    0: "None",
    0x68: "Inventory",
    0x6B: "Backpack",
    0x6F: "TradeWindow",
    0x9C50: "Playfield2",
    0xC350: "CanbeAffected",
    0xC74A: "WeaponInstance",
    0xC75B: "VendingMachine",
    0xC767: "TempBag",
    0xC76A: "Corpse",
}

STAT_NAMES = {
    0x01: "MaxHealth",
    0x1B: "Health",
    0x1C: "MaxHealth",
    0x22: "XP",
    0x25: "Level",
    0x34: "XP",
    0x3D: "Cash",
    0x98: "TitleLevel?",
    0x9A: "QuestFlag?",
    0xAD: "VisualBreed",
    0xD6: "VisualProfession",
    0xD7: "VisualSex?",
    0x40: "MissionBits0",
    0x185: "MissionBits0",
    0x189: "Quest?",
    0x19A: "QuestState?",
    0x209: "Quest?",
}


def read_u16(data: bytes, offset: int) -> int:
    return struct.unpack_from(">H", data, offset)[0]


def read_u32(data: bytes, offset: int) -> int:
    return struct.unpack_from(">I", data, offset)[0]


def identity(type_value: int, instance: int) -> str:
    return f"{IDENTITY_NAMES.get(type_value, str(type_value))}:{instance:08X}" if type_value else f"None:{instance}"


def load_n3_names(repo_root: Path) -> dict[int, str]:
    text = n3_type_file(repo_root).read_text(encoding="utf-8-sig", errors="replace")
    names: dict[int, str] = {}
    for match in re.finditer(r"^\s*(\w+)\s*=\s*0x([0-9a-fA-F]+)", text, re.MULTILINE):
        names[int(match.group(2), 16)] = match.group(1)
    return names


def load_packets(path: Path) -> list[dict[str, Any]]:
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


def is_zlib_header(data: bytes, offset: int) -> bool:
    if offset + 2 > len(data):
        return False
    cmf = data[offset]
    flg = data[offset + 1]
    return (cmf & 0x0F) == 8 and ((cmf << 8) + flg) % 31 == 0


def find_zlib_header(data: bytes) -> int:
    for offset in range(max(0, len(data) - 1)):
        if is_zlib_header(data, offset):
            return offset
    return -1


def extract_printable_strings(data: bytes) -> list[str]:
    strings: list[str] = []
    current = bytearray()
    for byte in data:
        if 32 <= byte <= 126 or byte in {9, 10, 13}:
            current.append(byte)
            continue
        if len(current) >= 3:
            text = current.decode("ascii", errors="replace").replace("\r", " ").replace("\n", " ").strip()
            if text and text not in strings:
                strings.append(text)
        current.clear()
    if len(current) >= 3:
        text = current.decode("ascii", errors="replace").replace("\r", " ").replace("\n", " ").strip()
        if text and text not in strings:
            strings.append(text)
    return strings


def parse_stat_summary(body: bytes) -> str:
    if len(body) < 21:
        return ""
    pos = 9
    try:
        count = read_u32(body, pos)
    except struct.error:
        return ""
    pos += 4
    if count > 200 or pos + count * 8 > len(body):
        return ""

    parts: list[str] = []
    for _ in range(count):
        stat_id = read_u32(body, pos)
        value = read_u32(body, pos + 4)
        pos += 8
        parts.append(f"{STAT_NAMES.get(stat_id, str(stat_id))}={value}")
    return "; ".join(parts)


def valid_frame_at(data: bytes, offset: int, n3_names: dict[int, str]) -> tuple[int, str, int] | None:
    if offset + 20 > len(data):
        return None
    packet_type = read_u16(data, offset + 2)
    unknown = read_u16(data, offset + 4)
    size = read_u16(data, offset + 6)
    if packet_type not in {0x000A, 0x000B} or unknown != 1:
        return None
    if size < 20 or offset + size > len(data):
        return None
    if packet_type == 0x000B:
        return size, "PacketType000B", 0

    n3_id = read_u32(data, offset + 16)
    name = n3_names.get(n3_id)
    if not name:
        return None
    return size, name, n3_id


def stream_payloads(packets: list[dict[str, Any]], session_id: str) -> list[dict[str, Any]]:
    rows = [
        row
        for row in packets
        if row.get("session_id") == session_id
        and row.get("direction") == "s2c"
        and row.get("payload_preview_hex")
    ]
    rows.sort(key=lambda row: int(row.get("packet_number") or 0))
    return rows


def decompress_session(rows: list[dict[str, Any]]) -> tuple[bytes, list[dict[str, Any]], bool, int | None]:
    output = bytearray()
    segments: list[dict[str, Any]] = []
    decompressor: zlib.Decompress | None = None
    compressed_started = False
    start_packet: int | None = None

    for row in rows:
        payload_hex = str(row.get("payload_preview_hex") or "")
        if not payload_hex:
            continue
        payload = bytes.fromhex(payload_hex)
        if not compressed_started:
            start = find_zlib_header(payload)
            if start < 0:
                continue
            payload = payload[start:]
            decompressor = zlib.decompressobj()
            compressed_started = True
            start_packet = int(row.get("packet_number") or 0)

        if decompressor is None:
            continue

        try:
            chunk = decompressor.decompress(payload)
        except zlib.error:
            continue
        if not chunk:
            continue

        start_offset = len(output)
        output.extend(chunk)
        segments.append(
            {
                "start": start_offset,
                "end": len(output),
                "packet_number": row.get("packet_number"),
                "relative_timestamp": row.get("relative_timestamp"),
                "session_id": row.get("session_id"),
                "stream_id": row.get("stream_id"),
            }
        )

    return bytes(output), segments, compressed_started, start_packet


def segment_for_offset(segments: list[dict[str, Any]], offset: int) -> dict[str, Any]:
    if not segments:
        return {}
    starts = [item["start"] for item in segments]
    index = bisect.bisect_right(starts, offset) - 1
    if index < 0:
        return {}
    return segments[index]


def parse_frames(data: bytes, segments: list[dict[str, Any]], n3_names: dict[int, str]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    offset = 0
    while offset + 20 <= len(data):
        valid = valid_frame_at(data, offset, n3_names)
        if not valid:
            offset += 1
            continue

        size, name, n3_id = valid
        frame = data[offset : offset + size]
        body = frame[20:]
        segment = segment_for_offset(segments, offset)
        body_identity = ""
        if len(body) >= 8:
            body_identity = identity(read_u32(body, 0), read_u32(body, 4))

        rows.append(
            {
                "packet_number": segment.get("packet_number", ""),
                "relative_timestamp": f"{float(segment.get('relative_timestamp') or 0):.3f}",
                "direction": "s2c",
                "session_id": segment.get("session_id", ""),
                "stream_id": segment.get("stream_id", ""),
                "decompressed_offset": offset,
                "message_id": read_u16(frame, 0),
                "packet_type": f"0x{read_u16(frame, 2):04x}",
                "size": size,
                "sender": f"0x{read_u32(frame, 8):08x}",
                "receiver": f"0x{read_u32(frame, 12):08x}",
                "n3_id": f"0x{n3_id:08x}" if n3_id else "",
                "name": name,
                "identity": body_identity,
                "stat_summary": parse_stat_summary(body) if name == "Stat" else "",
                "strings": extract_printable_strings(body),
                "frame_hex": frame.hex(),
            }
        )
        offset += size
    return rows


def write_outputs(capture_dir: Path, rows: list[dict[str, Any]], stream_summaries: list[dict[str, Any]]) -> None:
    (capture_dir / "s2c_frames.jsonl").write_text(
        "".join(json.dumps(row, sort_keys=True) + "\n" for row in rows),
        encoding="utf-8",
    )

    keys: list[str] = []
    for row in rows:
        for key in row:
            if key not in keys:
                keys.append(key)

    with (capture_dir / "s2c_frames.csv").open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=keys)
        writer.writeheader()
        for row in rows:
            clean = dict(row)
            clean["strings"] = json.dumps(clean.get("strings") or [])
            writer.writerow(clean)

    questish_names = {
        "ChatText",
        "ContainerAddItem",
        "CorpseFullUpdate",
        "Feedback",
        "FormatFeedback",
        "InventoryUpdate",
        "KnuBotAnswerList",
        "KnuBotAppendText",
        "KnuBotOpenChatWindow",
        "Quest",
        "QuestFullUpdate",
        "Stat",
    }
    string_keywords = (
        "mission",
        "quest",
        "reward",
        "remains",
        "molen",
        "brandon",
        "shuttle",
        "rollerrat",
        "salamander",
        "snake",
    )
    questish = [
        row
        for row in rows
        if row["name"] in questish_names
        or any(keyword in " ".join(row.get("strings") or []).lower() for keyword in string_keywords)
    ]
    lines = [
        "# AO S2C Decode Summary",
        "",
        f"- Frames decoded: {len(rows)}",
        f"- Quest/dialog/string candidate frames: {len(questish)}",
        "",
        "## Streams",
    ]
    for item in stream_summaries:
        lines.append(
            f"- {item['session_id']}: chunks={item['chunks']} compressed_started={item['compressed_started']} "
            f"start_packet={item.get('start_packet')} decompressed_bytes={item['decompressed_bytes']} frames={item['frames']}"
        )

    lines.extend(["", "## Frame Counts"])
    for name, count in Counter(row["name"] for row in rows).most_common():
        lines.append(f"- {name}: {count}")

    lines.extend(["", "## Quest/Dialog/String Candidates"])
    for row in questish[:160]:
        text = "; ".join(row.get("strings") or [])
        if len(text) > 500:
            text = text[:500] + "..."
        summary = row.get("stat_summary") or text
        lines.append(
            f"- pkt {row['packet_number']} t+{row['relative_timestamp']} {row['name']} {row.get('identity', '')} {summary}"
        )
    if len(questish) > 160:
        lines.append(f"- ... {len(questish) - 160} more rows in s2c_frames.csv")

    (capture_dir / "s2c_quest_decode_summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture_dir", type=Path)
    parser.add_argument("--repo-root", type=Path, default=DEFAULT_REPO_ROOT)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--allow-write", action="store_true")
    args = parser.parse_args()

    capture_dir = args.capture_dir
    repo_root = args.repo_root.resolve()
    planned_outputs = [
        capture_dir / "s2c_frames.jsonl",
        capture_dir / "s2c_frames.csv",
        capture_dir / "s2c_quest_decode_summary.md",
    ]
    print(f"repo_root={repo_root}")
    print(f"capture_dir={capture_dir}")
    print(f"n3_type_file={n3_type_file(repo_root)}")
    print("planned_outputs=" + ", ".join(str(path) for path in planned_outputs))
    if args.dry_run or not args.allow_write:
        print("No S2C decode files written. Pass --allow-write to write decoded outputs.")
        return 0

    packets = load_packets(capture_dir / "packets.jsonl")
    n3_names = load_n3_names(repo_root)

    rows: list[dict[str, Any]] = []
    stream_summaries: list[dict[str, Any]] = []
    for session_id in sorted({str(row.get("session_id")) for row in packets if row.get("direction") == "s2c"}):
        payload_rows = stream_payloads(packets, session_id)
        if not payload_rows:
            continue
        decompressed, segments, compressed_started, start_packet = decompress_session(payload_rows)
        session_rows = parse_frames(decompressed, segments, n3_names) if compressed_started else []
        if session_rows:
            rows.extend(session_rows)
        stream_summaries.append(
            {
                "session_id": session_id,
                "chunks": len(payload_rows),
                "compressed_started": compressed_started,
                "start_packet": start_packet,
                "decompressed_bytes": len(decompressed),
                "frames": len(session_rows),
            }
        )

    rows.sort(key=lambda row: (float(row.get("relative_timestamp") or 0), int(row.get("decompressed_offset") or 0)))
    write_outputs(capture_dir, rows, stream_summaries)
    print(f"s2c frames={len(rows)}")
    print(capture_dir / "s2c_quest_decode_summary.md")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
