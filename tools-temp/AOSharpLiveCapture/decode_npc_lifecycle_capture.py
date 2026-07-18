#!/usr/bin/env python3
"""Decode generic NPC corpse/lifecycle evidence from an AOSharpLiveCapture folder."""

import argparse
import collections
import csv
import datetime as dt
import json
import math
import re
import struct
import subprocess
import tempfile
from pathlib import Path


CORPSE_FULL_UPDATE = 0x4F474E05
SIMPLE_CHAR_FULL_UPDATE = 0x271B3A6B
MONSTER_DATA_SUFFIX_OFFSET = 72
TAIL_DEAD_NPC_TYPE_SUFFIX_OFFSET = 80
TAIL_DEAD_NPC_INSTANCE_SUFFIX_OFFSET = 84
RAW_PACKET_INDEX_NAME = "raw-packets.csv"

PACKET_RE = re.compile(
    r"^(?P<timestamp>\S+)\s+(?P<direction>IN|OUT)\s+#(?P<sequence>\d+)\s+"
    r"len=(?P<length>\d+)\s+n3=(?P<message>\S+)\s+hex=(?P<hex>[0-9A-Fa-f]+)$"
)
SOURCE_PACKET_RE = re.compile(
    r"^(?P<timestamp>\S+)\s+(?P<direction>IN|OUT)\s+#(?P<sequence>\d+)\s+"
    r"len=(?P<length>\d+)\s+(?:n3|type)=\S+\s+hex=(?P<hex>[0-9A-Fa-f]+)$"
)

CSV_HEADER = [
    "CapturedUtc", "Direction", "Sequence", "ReceiverInstance", "CorpseType",
    "CorpseInstance", "CorpseIdentity", "CorpseName", "PlayfieldId", "PositionX",
    "PositionY", "PositionZ", "MonsterScale", "Sex", "Breed", "Race",
    "DeadNpcType", "DeadNpcInstance", "DeadNpcIdentity", "DeadNpcName",
    "CorpseCatMesh", "CorpseCredits", "CorpseMonsterData", "TailDeadNpcType",
    "TailDeadNpcInstance", "TailDeadNpcIdentity", "PacketLength", "RawHex",
]

RESPAWN_CSV_HEADER = [
    "GeneratedUtc", "Status", "DeathIdentity", "Name", "MonsterData",
    "NpcFamily", "DeathUtc", "DeathX", "DeathY", "DeathZ",
    "CorpseIdentity", "CorpseSeenUtc", "CorpseGoneUtc", "RespawnIdentity", "RespawnUtc",
    "RespawnDelaySeconds", "RespawnAfterCorpseGoneSeconds", "RespawnX", "RespawnY", "RespawnZ",
    "PositionDelta", "ElapsedAfterDeathSeconds", "CandidateCount", "Detail",
]

RESPAWN_CANDIDATE_EVENTS = {"spawn", "population", "respawn"}
SAME_POSITION_THRESHOLD = 2.0
SCFU_MESSAGE_NAME = "SimpleCharFullUpdate"
SCFU_OUTPUT_NAME = "scfu-appearance.csv"
SCFU_ERROR_OUTPUT_NAME = "scfu-decode-errors.csv"


def packet_type_name(data):
    """Return the authoritative N3 type name from the packet bytes."""
    if len(data) < 20:
        return "Unknown"
    message_type = struct.unpack_from(">I", data, 16)[0]
    if message_type == SIMPLE_CHAR_FULL_UPDATE:
        return SCFU_MESSAGE_NAME
    if message_type == CORPSE_FULL_UPDATE:
        return "CorpseFullUpdate"
    return "Unknown"


def parse_packet_record(
    timestamp,
    direction,
    sequence,
    declared_length,
    raw_hex,
    source,
    global_ordinal=None,
):
    direction = (direction or "").strip().upper()
    if direction not in ("IN", "OUT"):
        raise ValueError(f"invalid direction {direction!r}")
    try:
        sequence = int(sequence)
    except (TypeError, ValueError) as exc:
        raise ValueError(f"invalid sequence {sequence!r}") from exc
    try:
        declared_length = int(declared_length)
    except (TypeError, ValueError) as exc:
        raise ValueError(f"invalid declared length {declared_length!r}") from exc
    raw_hex = (raw_hex or "").strip().upper()
    if not raw_hex or len(raw_hex) % 2:
        raise ValueError("raw hex is empty or has an odd number of characters")
    try:
        data = bytes.fromhex(raw_hex)
    except ValueError as exc:
        raise ValueError("raw hex is not valid hexadecimal") from exc
    if declared_length != len(data):
        raise ValueError(
            f"declared length {declared_length} does not match raw length {len(data)}"
        )
    message = packet_type_name(data)
    if message in (SCFU_MESSAGE_NAME, "CorpseFullUpdate"):
        frame_length = struct.unpack_from(">H", data, 6)[0]
        if frame_length != len(data):
            raise ValueError(
                f"frame header length {frame_length} does not match raw length {len(data)}"
            )
    if global_ordinal not in (None, ""):
        try:
            global_ordinal = int(global_ordinal)
        except (TypeError, ValueError) as exc:
            raise ValueError(f"invalid global ordinal {global_ordinal!r}") from exc
    else:
        global_ordinal = None
    return {
        "timestamp": (timestamp or "unknown").strip() or "unknown",
        "direction": direction,
        "sequence": sequence,
        "length": len(data),
        "rawHex": raw_hex,
        "message": message,
        "globalOrdinal": global_ordinal,
        "source": source,
    }


def load_capture_info(capture):
    result = {
        "available": False,
        "finalized": False,
        "captureStartUtc": "",
        "captureEndUtc": "",
        "observedPackets": None,
        "validationStatus": "",
        "validationRecaptureRequired": False,
        "error": "",
    }
    path = capture / "capture_info.json"
    if not path.exists():
        return result
    result["available"] = True
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        result["error"] = f"{type(exc).__name__}: {exc}"
        return result

    validation = payload.get("validation") or {}
    status = str(validation.get("status") or "").strip().lower()
    result["validationStatus"] = status
    result["captureStartUtc"] = str(payload.get("captureStartUtc") or "")
    result["captureEndUtc"] = str(
        payload.get("captureFinalizedUtc") or payload.get("captureEndUtc") or ""
    )
    result["finalized"] = bool(
        payload.get("captureEndUtc")
        or payload.get("captureFinalizedUtc")
        or status in {"complete", "incomplete", "failed", "invalid"}
    )
    if "recaptureRequired" in validation:
        result["validationRecaptureRequired"] = bool(
            validation.get("recaptureRequired")
        )
    else:
        result["validationRecaptureRequired"] = status in {"failed", "invalid"}
    if result["finalized"]:
        packet_counts = payload.get("packetCounts") or {}
        inbound = packet_counts.get("inboundRaw")
        outbound = packet_counts.get("outboundRaw")
        if isinstance(inbound, int) and isinstance(outbound, int):
            result["observedPackets"] = inbound + outbound
    return result


def new_source_state(path):
    return {
        "path": str(path),
        "exists": path.exists(),
        "records": {},
        "physicalRows": 0,
        "validRows": 0,
        "invalidRows": 0,
        "terminalPartialRows": 0,
        "terminalPartialRowNumbers": [],
        "duplicateRows": 0,
        "internalConflictCount": 0,
        "outsideCaptureWindowRows": 0,
        "issues": [],
    }


def add_source_record(state, record, row_number):
    key = (record["direction"], record["sequence"])
    existing = state["records"].get(key)
    if existing is None:
        state["records"][key] = record
        state["validRows"] += 1
        return
    if existing["rawHex"] == record["rawHex"]:
        state["duplicateRows"] += 1
        if existing["globalOrdinal"] is None:
            existing["globalOrdinal"] = record["globalOrdinal"]
        if existing["timestamp"] == "unknown" and record["timestamp"] != "unknown":
            existing["timestamp"] = record["timestamp"]
        return
    state["internalConflictCount"] += 1
    state["issues"].append(
        f"row {row_number}: conflicting raw bytes for {key[0]} sequence {key[1]}"
    )


def inside_capture_window(timestamp, capture_info):
    if not capture_info:
        return True
    observed = parse_timestamp(timestamp)
    start = parse_timestamp(capture_info.get("captureStartUtc"))
    end = parse_timestamp(capture_info.get("captureEndUtc"))
    if observed is None or (start is None and end is None):
        return True
    if observed.tzinfo is None:
        observed = observed.replace(tzinfo=dt.timezone.utc)
    if start is not None and start.tzinfo is None:
        start = start.replace(tzinfo=dt.timezone.utc)
    if end is not None and end.tzinfo is None:
        end = end.replace(tzinfo=dt.timezone.utc)
    return (start is None or observed >= start) and (end is None or observed <= end)


def packet_log_record_is_partial(match, error):
    """Return true only for a structurally valid row with a truncated payload."""
    raw_hex = match.group("hex")
    if len(raw_hex) % 2:
        return str(error) == "raw hex is empty or has an odd number of characters"
    try:
        raw_length = len(bytes.fromhex(raw_hex))
        declared_length = int(match.group("length"))
    except (TypeError, ValueError):
        return False
    return bool(
        raw_length < declared_length
        and str(error)
        == f"declared length {declared_length} does not match raw length {raw_length}"
    )


def load_packet_log_source(path, capture_info=None):
    state = new_source_state(path)
    if not state["exists"]:
        return state
    lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
    state["physicalRows"] = len(lines)
    for row_number, line in enumerate(lines, 1):
        match = SOURCE_PACKET_RE.match(line)
        if not match:
            state["invalidRows"] += 1
            state["issues"].append(f"row {row_number}: malformed packet log row")
            continue
        if not inside_capture_window(match.group("timestamp"), capture_info):
            state["outsideCaptureWindowRows"] += 1
            continue
        try:
            record = parse_packet_record(
                match.group("timestamp"),
                match.group("direction"),
                match.group("sequence"),
                match.group("length"),
                match.group("hex"),
                "packets.hex.log",
            )
        except ValueError as exc:
            state["invalidRows"] += 1
            state["issues"].append(f"row {row_number}: {exc}")
            if row_number == len(lines) and packet_log_record_is_partial(match, exc):
                state["terminalPartialRows"] += 1
                state["terminalPartialRowNumbers"].append(row_number)
            continue
        add_source_record(state, record, row_number)
    return state


def load_raw_index_source(path, capture_info=None):
    state = new_source_state(path)
    if not state["exists"]:
        return state
    with path.open("r", encoding="utf-8-sig", errors="replace", newline="") as handle:
        for row_number, row in enumerate(csv.DictReader(handle), 2):
            if not inside_capture_window(row.get("CapturedUtc"), capture_info):
                state["outsideCaptureWindowRows"] += 1
                continue
            preservation = (row.get("PreservationStatus") or "").strip().lower()
            if preservation != "raw_complete":
                state["invalidRows"] += 1
                state["issues"].append(
                    f"row {row_number}: preservation status is {preservation!r}"
                )
                continue
            try:
                record = parse_packet_record(
                    row.get("CapturedUtc"),
                    row.get("Direction"),
                    row.get("Sequence"),
                    row.get("PacketLength"),
                    row.get("RawHex"),
                    RAW_PACKET_INDEX_NAME,
                    row.get("GlobalOrdinal"),
                )
            except ValueError as exc:
                state["invalidRows"] += 1
                state["issues"].append(f"row {row_number}: {exc}")
                continue
            add_source_record(state, record, row_number)
    return state


def source_is_clean(state):
    return (
        state["exists"]
        and state["invalidRows"] == 0
        and state["duplicateRows"] == 0
        and state["internalConflictCount"] == 0
    )


def is_start_only_legacy_capture(capture_info):
    return bool(
        capture_info["available"]
        and not capture_info["finalized"]
        and capture_info["captureStartUtc"]
        and not capture_info["captureEndUtc"]
        and capture_info["observedPackets"] is None
        and capture_info["validationStatus"] == "running"
        and not capture_info["validationRecaptureRequired"]
    )


def source_has_salvageable_terminal_partial(state, capture_info):
    return bool(
        is_start_only_legacy_capture(capture_info)
        and state["exists"]
        and state["validRows"] > 0
        and state["invalidRows"] == 1
        and state["terminalPartialRows"] == 1
        and state["terminalPartialRowNumbers"] == [state["physicalRows"]]
        and state["duplicateRows"] == 0
        and state["internalConflictCount"] == 0
    )


def public_source_summary(
    state, complete, positive_evidence_usable=False, terminal_partial_salvaged=False
):
    return {
        "path": state["path"],
        "exists": state["exists"],
        "physicalRows": state["physicalRows"],
        "validRows": state["validRows"],
        "invalidRows": state["invalidRows"],
        "terminalPartialRows": state["terminalPartialRows"],
        "terminalPartialRowNumbers": state["terminalPartialRowNumbers"],
        "terminalPartialSalvaged": terminal_partial_salvaged,
        "positiveEvidenceUsable": positive_evidence_usable,
        "duplicateRows": state["duplicateRows"],
        "internalConflictCount": state["internalConflictCount"],
        "outsideCaptureWindowRows": state["outsideCaptureWindowRows"],
        "complete": complete,
        "issues": state["issues"],
    }


def packet_sort_key(record):
    timestamp = parse_timestamp(record["timestamp"])
    if timestamp is not None and timestamp.tzinfo is None:
        timestamp = timestamp.replace(tzinfo=dt.timezone.utc)
    return (
        timestamp is None,
        timestamp or dt.datetime.max.replace(tzinfo=dt.timezone.utc),
        record["direction"],
        record["sequence"],
    )


def normalized_packet_line(record):
    return (
        f"{record['timestamp']} {record['direction']} #{record['sequence']} "
        f"len={record['length']} n3={record['message']} hex={record['rawHex']}"
    )


def load_packet_lines(capture):
    """Reconcile both durable raw sinks into one validated chronological stream."""
    capture_info = load_capture_info(capture)
    packet_log = load_packet_log_source(capture / "packets.hex.log", capture_info)
    raw_index = load_raw_index_source(
        capture / RAW_PACKET_INDEX_NAME, capture_info
    )
    sources = [packet_log, raw_index]
    observed = capture_info["observedPackets"]
    # Early capture builds wrote start-only capture_info.json metadata but never
    # persisted finalized packet counts.  In that case the durable raw sinks,
    # rather than the mere presence of provisional metadata, are the only
    # available completeness contract.  Keep the structural checks below
    # fail-closed: malformed, empty, conflicting, or non-superset sinks still
    # cannot become canonical.
    legacy_inference = observed is None
    terminal_partial_candidates = {
        source["path"]: source_has_salvageable_terminal_partial(source, capture_info)
        for source in sources
    }

    complete = {}
    for source in sources:
        if observed is not None:
            complete[source["path"]] = bool(
                source_is_clean(source) and source["validRows"] == observed
            )
        else:
            complete[source["path"]] = False

    if legacy_inference:
        existing = [source for source in sources if source["exists"]]
        if len(existing) == 1:
            only = existing[0]
            complete[only["path"]] = bool(source_is_clean(only) and only["validRows"] > 0)
        elif len(existing) == 2:
            for source, other in ((packet_log, raw_index), (raw_index, packet_log)):
                complete[source["path"]] = bool(
                    source_is_clean(source)
                    and source["validRows"] > 0
                    and set(source["records"]).issuperset(other["records"])
                )

    complete_sources = [source for source in sources if complete[source["path"]]]
    trusted_sources = [
        source
        for source in sources
        if complete[source["path"]] or terminal_partial_candidates[source["path"]]
    ]
    conflicts = []
    all_keys = set(packet_log["records"]) | set(raw_index["records"])
    for key in sorted(all_keys):
        left = packet_log["records"].get(key)
        right = raw_index["records"].get(key)
        if left and right and left["rawHex"] != right["rawHex"]:
            conflicts.append(
                {
                    "direction": key[0],
                    "sequence": key[1],
                    "packetLogRawHex": left["rawHex"],
                    "rawIndexRawHex": right["rawHex"],
                }
            )

    internal_conflicts = sum(source["internalConflictCount"] for source in sources)
    exactly_one_complete = len(complete_sources) == 1
    exactly_one_trusted = len(trusted_sources) == 1
    terminal_partial_candidate = any(terminal_partial_candidates.values())
    unresolved_conflicts = bool(conflicts or internal_conflicts) and (
        terminal_partial_candidate or not exactly_one_complete
    )

    canonical = {}
    if exactly_one_trusted:
        authoritative = trusted_sources[0]
        other = raw_index if authoritative is packet_log else packet_log
        for key, record in authoritative["records"].items():
            canonical[key] = dict(record)
            matching = other["records"].get(key)
            if matching and matching["rawHex"] == record["rawHex"]:
                if canonical[key]["globalOrdinal"] is None:
                    canonical[key]["globalOrdinal"] = matching["globalOrdinal"]
    else:
        for source in sources:
            for key, record in source["records"].items():
                existing = canonical.get(key)
                if existing is None:
                    canonical[key] = dict(record)
                elif existing["rawHex"] == record["rawHex"]:
                    if existing["globalOrdinal"] is None:
                        existing["globalOrdinal"] = record["globalOrdinal"]
                    if existing["timestamp"] == "unknown" and record["timestamp"] != "unknown":
                        existing["timestamp"] = record["timestamp"]

    if observed is not None:
        existing_sources = [source for source in sources if source["exists"]]
        finalized_sources_structurally_valid = bool(complete_sources) or bool(
            existing_sources
            and all(source_is_clean(source) for source in existing_sources)
        )
        canonical_valid = bool(
            finalized_sources_structurally_valid
            and len(canonical) == observed
            and not unresolved_conflicts
        )
    else:
        canonical_valid = bool(trusted_sources) and not unresolved_conflicts
    terminal_partial_salvaged = bool(
        terminal_partial_candidate and canonical_valid
    )
    capture_complete = bool(canonical_valid and observed is not None)
    recapture_required = bool(
        capture_info["validationRecaptureRequired"] or not canonical_valid
    )

    records = list(canonical.values())
    if records and all(record["globalOrdinal"] is not None for record in records):
        records.sort(key=lambda record: record["globalOrdinal"])
    else:
        records.sort(key=packet_sort_key)

    if recapture_required:
        capability_status = "raw_source_recapture_required"
    elif terminal_partial_salvaged:
        capability_status = "raw_source_legacy_terminal_tail_salvaged"
    elif legacy_inference:
        capability_status = "raw_source_legacy_inferred_complete"
    elif exactly_one_complete:
        capability_status = "raw_source_recovered_from_complete_sink"
    else:
        capability_status = "raw_source_complete"

    source_summary = {
        "capabilityStatus": capability_status,
        "canonicalValid": not recapture_required,
        "recaptureRequired": recapture_required,
        "legacyInference": legacy_inference,
        "captureComplete": capture_complete,
        "positiveEvidenceUsable": not recapture_required,
        "positiveEvidenceOnly": bool(not recapture_required and not capture_complete),
        "absenceInferenceAllowed": capture_complete,
        "terminalPartialTailSalvaged": terminal_partial_salvaged,
        "captureInfo": capture_info,
        "observedPackets": observed,
        "canonicalPackets": len(records),
        "conflictCount": len(conflicts) + internal_conflicts,
        "conflicts": conflicts,
        "packetLog": public_source_summary(
            packet_log,
            complete[packet_log["path"]],
            packet_log in trusted_sources and not recapture_required,
            terminal_partial_salvaged
            and terminal_partial_candidates[packet_log["path"]],
        ),
        "rawPacketIndex": public_source_summary(
            raw_index,
            complete[raw_index["path"]],
            raw_index in trusted_sources and not recapture_required,
            terminal_partial_salvaged
            and terminal_partial_candidates[raw_index["path"]],
        ),
    }
    return [normalized_packet_line(record) for record in records], source_summary


def u32(data, offset):
    return struct.unpack_from(">I", data, offset)[0]


def i32(data, offset):
    return struct.unpack_from(">i", data, offset)[0]


def f32(data, offset):
    return struct.unpack_from(">f", data, offset)[0]


def identity(identity_type, instance):
    names = {0xC350: "SimpleChar", 0xC76A: "Corpse"}
    return f"({names.get(identity_type, f'0x{identity_type:08X}')}:{instance:08X})"


def decode_corpse_full_update(match):
    raw_hex = match.group("hex").upper()
    data = bytes.fromhex(raw_hex)
    if len(data) < 231 or u32(data, 16) != CORPSE_FULL_UPDATE:
        raise ValueError("packet is not a CorpseFullUpdate")

    name_offset = data.find(b"Remains of ")
    if name_offset < 4:
        raise ValueError("CorpseFullUpdate has no encoded Remains name marker")
    encoded_name_length = i32(data, name_offset - 4)
    suffix_offset = name_offset + encoded_name_length
    monster_data_offset = suffix_offset + MONSTER_DATA_SUFFIX_OFFSET
    tail_type_offset = suffix_offset + TAIL_DEAD_NPC_TYPE_SUFFIX_OFFSET
    tail_instance_offset = suffix_offset + TAIL_DEAD_NPC_INSTANCE_SUFFIX_OFFSET
    if (
        encoded_name_length <= 0
        or suffix_offset > len(data)
        or monster_data_offset < suffix_offset
        or tail_instance_offset + 4 > len(data)
    ):
        raise ValueError(
            f"invalid layout len={len(data)} nameLength={encoded_name_length} "
            f"monsterOffset={monster_data_offset} tailOffset={tail_instance_offset}"
        )

    corpse_type = u32(data, 20)
    corpse_instance = u32(data, 24)
    dead_npc_type = u32(data, 183)
    dead_npc_instance = u32(data, 191)
    tail_type = u32(data, tail_type_offset)
    tail_instance = u32(data, tail_instance_offset)
    corpse_name = data[name_offset:name_offset + encoded_name_length].rstrip(b"\0").decode("ascii")

    return [
        match.group("timestamp"),
        match.group("direction"),
        int(match.group("sequence")),
        u32(data, 12),
        f"0x{corpse_type:08X}",
        f"0x{corpse_instance:08X}",
        identity(corpse_type, corpse_instance),
        corpse_name,
        i32(data, 73),
        format(f32(data, 45), ".9g"),
        format(f32(data, 49), ".9g"),
        format(f32(data, 53), ".9g"),
        i32(data, 143),
        i32(data, 159),
        i32(data, 167),
        i32(data, 175),
        f"0x{dead_npc_type:08X}",
        f"0x{dead_npc_instance:08X}",
        identity(dead_npc_type, dead_npc_instance),
        "",
        i32(data, 199),
        i32(data, 207),
        i32(data, monster_data_offset),
        f"0x{tail_type:08X}",
        f"0x{tail_instance:08X}",
        identity(tail_type, tail_instance),
        len(data),
        raw_hex,
    ]


def count_event_lines(events_path):
    counts = {
        "deathActions": 0,
        "corpseSeen": 0,
        "corpseGone": 0,
        "corpseUses": 0,
        "lootMoveRequests": 0,
        "lootMoveResults": 0,
        "corpseDespawns": 0,
    }
    if not events_path.exists():
        return counts

    for line in events_path.read_text(encoding="utf-8-sig", errors="replace").splitlines():
        counts["deathActions"] += "Action=99" in line
        counts["corpseSeen"] += "[CORPSE-SEEN]" in line
        counts["corpseGone"] += "[CORPSE-GONE]" in line
        counts["corpseUses"] += "Target=(Corpse:" in line and "GenericCmd" in line
        counts["lootMoveRequests"] += "ClientMoveItemToInventory" in line
        counts["lootMoveResults"] += "ContainerAddItem" in line
        counts["corpseDespawns"] += "type=Despawn identity=(Corpse:" in line
    return counts


def count_corpse_inventory_updates(path):
    if not path.exists():
        return 0
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return sum(
            1
            for row in csv.DictReader(handle)
            if row.get("InventoryIdentity", "").startswith("(Corpse:")
        )


def count_csv_rows(path):
    if not path.exists():
        return 0
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return sum(1 for _ in csv.DictReader(handle))


def count_scfu_output_rows(path):
    counts = {"outputRows": 0, "decodedRows": 0, "pendingRows": 0}
    if not path.exists():
        return counts
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        for row in csv.DictReader(handle):
            counts["outputRows"] += 1
            if row.get("DecodeStatus") == "decoded_complete":
                counts["decodedRows"] += 1
            else:
                counts["pendingRows"] += 1
    return counts


def count_raw_scfu_packets(packet_lines):
    count = 0
    for line in packet_lines:
        match = PACKET_RE.match(line)
        if match and match.group("message") == SCFU_MESSAGE_NAME:
            count += 1
    return count


def count_raw_corpse_full_update_packets(packet_lines):
    count = 0
    for line in packet_lines:
        match = PACKET_RE.match(line)
        if match and match.group("message") == "CorpseFullUpdate":
            count += 1
    return count


def classify_corpse_decode(
    raw_packets, decoded_rows, decode_errors, local_evidence_observed
):
    decode_error_count = len(decode_errors)
    if raw_packets == 0:
        if decoded_rows or decode_error_count:
            return False, "offline_corpse_decode_required"
        return True, (
            "no_raw_corpse_full_update_observed"
            if local_evidence_observed
            else "no_corpse_full_update_observed"
        )

    fully_decoded = bool(
        decode_error_count == 0 and len(decoded_rows) == raw_packets
    )
    return (
        (True, "corpse_full_update_decode_complete")
        if fully_decoded
        else (False, "offline_corpse_decode_required")
    )


def pending_path(final_path):
    return final_path.with_name(f"{final_path.stem}.pending{final_path.suffix}")


def promote_pending_outputs(pairs, allowed):
    if not allowed:
        return False
    missing = [str(pending) for pending, _ in pairs if not pending.exists()]
    if missing:
        raise RuntimeError(f"cannot promote missing pending outputs: {', '.join(missing)}")
    for pending, final in pairs:
        pending.replace(final)
    return True


def run_scfu_analyzer(capture, packet_lines, raw_scfu_packets):
    analyzer_path = (
        Path(__file__).resolve().parents[1]
        / "AOSharpCaptureAnalyzer"
        / "bin"
        / "Debug"
        / "AOSharpCaptureAnalyzer.exe"
    )
    output_path = capture / SCFU_OUTPUT_NAME
    error_path = capture / SCFU_ERROR_OUTPUT_NAME
    pending_output_path = pending_path(output_path)
    pending_error_path = pending_path(error_path)
    result = {
        "analyzerPath": str(analyzer_path),
        "analyzerAvailable": analyzer_path.exists(),
        "analyzerInvoked": False,
        "analyzerExitCode": None,
        "analyzerError": "",
        "rawPackets": raw_scfu_packets,
        "outputRows": 0,
        "decodedRows": 0,
        "pendingRows": 0,
        "decodeErrors": 0,
        "outputCsv": str(output_path),
        "errorCsv": str(error_path),
        "pendingOutputCsv": str(pending_output_path),
        "pendingErrorCsv": str(pending_error_path),
    }

    if not analyzer_path.exists():
        if raw_scfu_packets == 0:
            result["capabilityStatus"] = "no_scfu_observed"
            result["recaptureRequired"] = False
            result["offlineDecodeRequired"] = False
            return result
        result["analyzerError"] = "Built AOSharpCaptureAnalyzer.exe is unavailable."
        result["capabilityStatus"] = "offline_analyzer_unavailable"
        result["recaptureRequired"] = False
        result["offlineDecodeRequired"] = True
        return result

    for path in (pending_output_path, pending_error_path):
        if path.exists():
            path.unlink()

    try:
        with tempfile.TemporaryDirectory(
            prefix=".npc-lifecycle-analyzer-", dir=str(capture)
        ) as staging_name:
            staging = Path(staging_name)
            (staging / "packets.hex.log").write_text(
                "\n".join(packet_lines) + ("\n" if packet_lines else ""),
                encoding="utf-8",
            )
            completed = subprocess.run(
                [str(analyzer_path), str(staging)],
                capture_output=True,
                text=True,
                timeout=60,
                check=False,
            )
            result["analyzerInvoked"] = True
            result["analyzerExitCode"] = completed.returncode
            if completed.returncode != 0:
                result["analyzerError"] = (completed.stderr or completed.stdout).strip()
            staging_output = staging / SCFU_OUTPUT_NAME
            if not staging_output.exists():
                staging_output = pending_path(staging_output)
            staging_errors = staging / SCFU_ERROR_OUTPUT_NAME
            if not staging_errors.exists():
                staging_errors = pending_path(staging_errors)
            if staging_output.exists():
                staging_output.replace(pending_output_path)
            if staging_errors.exists():
                staging_errors.replace(pending_error_path)
    except (OSError, subprocess.SubprocessError) as exc:
        result["analyzerError"] = f"{type(exc).__name__}: {exc}"

    if result["analyzerInvoked"]:
        output_counts = count_scfu_output_rows(pending_output_path)
        result.update(output_counts)
        error_rows = count_csv_rows(pending_error_path)
        unaccounted_packets = max(
            0, raw_scfu_packets - result["outputRows"] - error_rows
        )
        result["decodeErrors"] = error_rows + unaccounted_packets

    if raw_scfu_packets == 0:
        result["capabilityStatus"] = "no_scfu_observed"
        result["recaptureRequired"] = False
        result["offlineDecodeRequired"] = False
        return result

    fully_decoded = bool(
        result["analyzerInvoked"]
        and result["analyzerExitCode"] == 0
        and result["decodedRows"] == raw_scfu_packets
        and result["pendingRows"] == 0
        and result["decodeErrors"] == 0
    )
    result["capabilityStatus"] = (
        "scfu_decode_complete" if fully_decoded else "offline_scfu_decode_required"
    )
    result["recaptureRequired"] = False
    result["offlineDecodeRequired"] = not fully_decoded
    return result


def parse_timestamp(value):
    if not value:
        return None
    try:
        return dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        return None


def parse_float(value):
    if value in (None, ""):
        return None
    try:
        return float(value)
    except ValueError:
        return None


def position(row):
    x = parse_float(row.get("x"))
    y = parse_float(row.get("y"))
    z = parse_float(row.get("z"))
    if x is None or y is None or z is None:
        return None
    return x, y, z


def position_delta(first, second):
    first_pos = position(first)
    second_pos = position(second)
    if first_pos is None or second_pos is None:
        return None
    return math.dist(first_pos, second_pos)


def load_enemy_state_rows(path):
    rows = []
    skipped = 0
    if not path.exists():
        return rows, skipped

    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        for row in csv.DictReader(handle):
            if (
                row.get("timestamp")
                and row.get("entityId")
                and row.get("eventType")
                and parse_timestamp(row.get("timestamp")) is not None
            ):
                rows.append(row)
            else:
                skipped += 1
    rows.sort(key=lambda row: parse_timestamp(row["timestamp"]))
    return rows, skipped


def load_enemy_profiles(path):
    profiles = {}
    if not path.exists():
        return profiles

    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        for row in csv.DictReader(handle):
            identity_value = row.get("Identity", "")
            if not identity_value:
                continue
            profiles[identity_value] = {
                "Name": row.get("Name", ""),
                "MonsterData": row.get("MonsterData", ""),
                "NpcFamily": row.get("NPCFamily", "") or row.get("NpcFamily", ""),
                "Level": row.get("Level", ""),
            }
    return profiles


def load_scfu_profiles_and_state(path):
    profiles = {}
    state_rows = []
    seen_identities = set()
    if not path.exists():
        return profiles, state_rows

    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        for row in csv.DictReader(handle):
            if row.get("DecodeStatus") not in {
                "decoded_complete",
                "raw_complete_decode_pending",
            }:
                continue
            if row.get("CharacterInfoType") != "NPCInfo":
                continue
            identity_value = row.get("Identity", "")
            if not identity_value:
                continue
            profiles[identity_value] = {
                "Name": row.get("Name", ""),
                "MonsterData": row.get("MonsterData", ""),
                "NpcFamily": row.get("NpcFamily", "") or row.get("NPCFamily", ""),
                "Level": row.get("Level", ""),
            }
            if identity_value in seen_identities:
                continue
            captured_utc = row.get("CapturedUtc", "")
            if parse_timestamp(captured_utc) is None:
                continue
            seen_identities.add(identity_value)
            state_rows.append(
                {
                    "timestamp": captured_utc,
                    "direction": row.get("Direction", ""),
                    "sequence": row.get("Sequence", ""),
                    "messageType": "SimpleCharFullUpdate",
                    "evidenceSource": "OfflineRawScfuDecoder",
                    "entityId": identity_value,
                    "level": row.get("Level", ""),
                    "currentHealth": row.get("Health", ""),
                    "maxHealth": row.get("Health", ""),
                    "x": row.get("PositionX", ""),
                    "y": row.get("PositionY", ""),
                    "z": row.get("PositionZ", ""),
                    "eventType": "spawn",
                }
            )
    state_rows.sort(key=lambda row: parse_timestamp(row["timestamp"]))
    return profiles, state_rows


def load_enemy_dossier_profiles(path):
    profiles = {}
    if not path.exists():
        return profiles

    data = json.loads(path.read_text(encoding="utf-8-sig"))
    for enemy in data.get("enemies", []):
        identity_value = enemy.get("identity", "")
        if not identity_value:
            continue
        profiles[identity_value] = {
            "Name": str(enemy.get("name", "")),
            "MonsterData": str(enemy.get("monsterData", "")),
            "NpcFamily": str(enemy.get("npcFamily", "")),
            "Level": str(enemy.get("level", "")),
        }
    return profiles


def identity_key(value):
    value = (value or "").strip().strip("()")
    if ":" not in value:
        return value.upper()
    identity_type, instance = value.split(":", 1)
    try:
        instance = f"{int(instance, 16):08X}"
    except ValueError:
        instance = instance.upper()
    return f"{identity_type.upper()}:{instance}"


def load_corpse_gone_times(path):
    gone_times = collections.defaultdict(list)
    if not path.exists():
        return gone_times

    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        for row in csv.DictReader(handle):
            if row.get("Phase") != "corpse-gone":
                continue
            corpse_identity = identity_key(row.get("PrimaryIdentity", ""))
            captured_utc = row.get("CapturedUtc", "")
            if corpse_identity and parse_timestamp(captured_utc) is not None:
                gone_times[corpse_identity].append(captured_utc)
    for values in gone_times.values():
        values.sort(key=parse_timestamp)
    return gone_times


def load_corpse_by_dead_npc(path):
    corpses = {}
    if not path.exists():
        return corpses

    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        for row in csv.DictReader(handle):
            dead_identity = row.get("DeadNpcIdentity", "")
            if not dead_identity:
                continue
            corpses[dead_identity] = {
                "CorpseIdentity": row.get("CorpseIdentity", ""),
                "CorpseSeenUtc": row.get("CapturedUtc", ""),
            }
    return corpses


def rebind_corpse_loot_observations(
    capture, corpse_csv, output_csv, scfu_csv=None
):
    source_csv = capture / "corpse-loot-observations.csv"
    if not source_csv.exists():
        return {
            "available": False,
            "rows": 0,
            "linkedRows": 0,
            "unlinkedRows": 0,
            "ambiguousRows": 0,
            "outputCsv": str(output_csv),
        }

    with source_csv.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        fieldnames = reader.fieldnames or []
        observations = list(reader)

    gone_times = load_corpse_gone_times(capture / "npc-lifecycle.csv")
    generation_updates_by_corpse = collections.defaultdict(list)
    if corpse_csv.exists():
        with corpse_csv.open("r", encoding="utf-8-sig", newline="") as handle:
            for row in csv.DictReader(handle):
                seen_time = parse_timestamp(row.get("CapturedUtc", ""))
                corpse_key = identity_key(row.get("CorpseIdentity", ""))
                if seen_time is None or not corpse_key:
                    continue
                generation = dict(row)
                generation["SeenTime"] = seen_time
                generation_updates_by_corpse[corpse_key].append(generation)

    generations_by_corpse = collections.defaultdict(list)
    for corpse_key, updates in generation_updates_by_corpse.items():
        updates.sort(key=lambda row: row["SeenTime"])
        active_generation = None
        for update in updates:
            corpse_gone_times = [
                parse_timestamp(value)
                for value in gone_times.get(corpse_key, [])
            ]
            equal_time_boundary = any(
                gone_time == update["SeenTime"]
                for gone_time in corpse_gone_times
            )
            active_ended = active_generation is not None and any(
                active_generation["SeenTime"] < gone_time <= update["SeenTime"]
                for gone_time in corpse_gone_times
            )
            same_dead_npc = active_generation is not None and identity_key(
                active_generation.get("DeadNpcIdentity", "")
            ) == identity_key(update.get("DeadNpcIdentity", ""))
            if active_generation is not None and same_dead_npc and not active_ended:
                active_generation["Updates"].append(update)
                continue

            active_generation = dict(update)
            active_generation["Updates"] = [update]
            active_generation["AmbiguousStart"] = equal_time_boundary
            generations_by_corpse[corpse_key].append(active_generation)

    profiles = load_enemy_dossier_profiles(capture / "enemy-dossier.json")
    for identity_value, profile in load_enemy_profiles(
        capture / "enemy-full-updates.csv"
    ).items():
        existing = profiles.setdefault(identity_value, {})
        existing.update({key: value for key, value in profile.items() if value})
    scfu_profiles, _ = load_scfu_profiles_and_state(
        scfu_csv or capture / SCFU_OUTPUT_NAME
    )
    for identity_value, profile in scfu_profiles.items():
        existing = profiles.setdefault(identity_value, {})
        existing.update({key: value for key, value in profile.items() if value})
    profiles_by_key = {
        identity_key(identity_value): profile
        for identity_value, profile in profiles.items()
    }
    generation_open_counts = collections.defaultdict(int)
    linked_rows = 0
    unlinked_rows = 0
    ambiguous_rows = 0

    for observation in observations:
        observed_time = parse_timestamp(observation.get("CapturedUtc", ""))
        corpse_key = identity_key(observation.get("CorpseIdentity", ""))
        generations = generations_by_corpse.get(corpse_key, [])
        candidates = [
            generation
            for generation in generations
            if observed_time is not None and generation["SeenTime"] <= observed_time
        ]
        generation = candidates[-1] if candidates else None
        if generation is not None:
            same_time = [
                candidate
                for candidate in candidates
                if candidate["SeenTime"] == generation["SeenTime"]
            ]
            if len(same_time) > 1 or (
                generation.get("AmbiguousStart", False)
                and observed_time == generation["SeenTime"]
            ):
                generation = None
                ambiguous_rows += 1

        if generation is not None:
            generation_index = generations.index(generation)
            next_seen = (
                generations[generation_index + 1]["SeenTime"]
                if generation_index + 1 < len(generations)
                else None
            )
            generation_gone_times = []
            for value in gone_times.get(corpse_key, []):
                gone_time = parse_timestamp(value)
                starts_after_generation = (
                    gone_time > generation["SeenTime"]
                    if generation.get("AmbiguousStart", False)
                    else gone_time >= generation["SeenTime"]
                )
                if starts_after_generation and (
                    next_seen is None or gone_time < next_seen
                ):
                    generation_gone_times.append(gone_time)
            first_gone = generation_gone_times[0] if generation_gone_times else None
            if first_gone is not None:
                if observed_time == first_gone:
                    generation = None
                    ambiguous_rows += 1
                elif observed_time > first_gone:
                    generation = None

        if generation is None:
            # A partial capture may contain a valid live-linked loot row without
            # the corpse full update needed to reconstruct its generation
            # offline. Preserve that evidence verbatim instead of replacing it
            # with blank metadata. Rows that arrived without any correlation
            # status remain explicitly unresolved.
            if not observation.get("CorrelationStatus", "").strip():
                observation["CorrelationStatus"] = "unlinked-offline-generation"
            unlinked_rows += 1
            continue

        generation_key = (corpse_key, generation["CapturedUtc"])
        generation_open_counts[generation_key] += 1
        open_ordinal = generation_open_counts[generation_key]
        generation_update = [
            update
            for update in generation.get("Updates", [generation])
            if update["SeenTime"] <= observed_time
        ][-1]
        dead_npc_identity = generation_update.get("DeadNpcIdentity", "")
        profile = profiles_by_key.get(identity_key(dead_npc_identity), {})
        observation.update(
            {
                "OpenOrdinal": str(open_ordinal),
                "InitialSnapshot": "true" if open_ordinal == 1 else "false",
                "DeadNpcIdentity": dead_npc_identity,
                "EnemyName": profile.get("Name", ""),
                "MonsterData": generation_update.get("CorpseMonsterData", ""),
                "EnemyLevel": profile.get("Level", ""),
                "CorpseCredits": generation_update.get("CorpseCredits", ""),
                "PlayfieldId": generation_update.get("PlayfieldId", ""),
                "CorrelationStatus": "linked-offline-generation",
            }
        )
        linked_rows += 1

    with output_csv.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(observations)

    return {
        "available": True,
        "rows": len(observations),
        "linkedRows": linked_rows,
        "unlinkedRows": unlinked_rows,
        "ambiguousRows": ambiguous_rows,
        "outputCsv": str(output_csv),
    }


def same_profile(dead_profile, candidate_profile):
    required_fields = ("Name", "MonsterData")
    if not all(
        dead_profile.get(field)
        and candidate_profile.get(field)
        and dead_profile.get(field) == candidate_profile.get(field)
        for field in required_fields
    ):
        return False
    dead_family = dead_profile.get("NpcFamily", "")
    candidate_family = candidate_profile.get("NpcFamily", "")
    return not dead_family or not candidate_family or dead_family == candidate_family


def build_enemy_respawns(capture, corpse_csv, output_csv=None, scfu_csv=None):
    state_rows, skipped_state_rows = load_enemy_state_rows(capture / "enemy-state.csv")
    profiles = load_enemy_dossier_profiles(capture / "enemy-dossier.json")
    for identity_value, profile in load_enemy_profiles(capture / "enemy-full-updates.csv").items():
        existing = profiles.setdefault(identity_value, {})
        existing.update({key: value for key, value in profile.items() if value})
    scfu_profiles, scfu_state_rows = load_scfu_profiles_and_state(
        scfu_csv or capture / SCFU_OUTPUT_NAME
    )
    for identity_value, profile in scfu_profiles.items():
        existing = profiles.setdefault(identity_value, {})
        existing.update({key: value for key, value in profile.items() if value})
    existing_candidate_identities = {
        row.get("entityId", "")
        for row in state_rows
        if row.get("eventType") in RESPAWN_CANDIDATE_EVENTS
    }
    state_rows.extend(
        row
        for row in scfu_state_rows
        if row.get("entityId", "") not in existing_candidate_identities
    )
    state_rows.sort(key=lambda row: parse_timestamp(row["timestamp"]))
    corpses = load_corpse_by_dead_npc(corpse_csv)
    corpse_gone_times = load_corpse_gone_times(capture / "npc-lifecycle.csv")
    capture_end = parse_timestamp(state_rows[-1]["timestamp"]) if state_rows else None
    generated_utc = dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")
    observations = []

    for death in [row for row in state_rows if row.get("eventType") == "death"]:
        dead_identity = death.get("entityId", "")
        dead_profile = profiles.get(dead_identity, {})
        death_time = parse_timestamp(death.get("timestamp"))
        if death_time is None:
            continue

        candidates = []
        for candidate in state_rows:
            candidate_time = parse_timestamp(candidate.get("timestamp"))
            if candidate_time is None or candidate_time <= death_time:
                continue
            if candidate.get("entityId") == dead_identity:
                continue
            if candidate.get("eventType") not in RESPAWN_CANDIDATE_EVENTS:
                continue
            candidate_profile = profiles.get(candidate.get("entityId", ""), {})
            if not same_profile(dead_profile, candidate_profile):
                continue
            delta = position_delta(death, candidate)
            if delta is None or delta > SAME_POSITION_THRESHOLD:
                continue
            candidates.append((candidate_time, delta, candidate))

        candidates.sort(key=lambda item: item[0])
        selected = candidates[0] if candidates else None
        status = "complete" if selected else "incomplete"
        if len(candidates) > 1 and (candidates[1][0] - selected[0]).total_seconds() <= 2.0:
            status = "ambiguous"

        corpse = corpses.get(dead_identity, {})
        corpse_seen_time = parse_timestamp(corpse.get("CorpseSeenUtc", ""))
        corpse_gone_utc = next(
            (
                value
                for value in corpse_gone_times.get(
                    identity_key(corpse.get("CorpseIdentity", "")), []
                )
                if corpse_seen_time is None or parse_timestamp(value) >= corpse_seen_time
            ),
            "",
        )
        corpse_gone_time = parse_timestamp(corpse_gone_utc)
        selected_time = selected[0] if selected else None
        selected_delta = selected[1] if selected else None
        selected_row = selected[2] if selected else {}
        elapsed_end = capture_end or death_time
        observations.append({
            "GeneratedUtc": generated_utc,
            "Status": status,
            "DeathIdentity": dead_identity,
            "Name": dead_profile.get("Name", ""),
            "MonsterData": dead_profile.get("MonsterData", ""),
            "NpcFamily": dead_profile.get("NpcFamily", ""),
            "DeathUtc": death.get("timestamp", ""),
            "DeathX": death.get("x", ""),
            "DeathY": death.get("y", ""),
            "DeathZ": death.get("z", ""),
            "CorpseIdentity": corpse.get("CorpseIdentity", ""),
            "CorpseSeenUtc": corpse.get("CorpseSeenUtc", ""),
            "CorpseGoneUtc": corpse_gone_utc,
            "RespawnIdentity": selected_row.get("entityId", "") if selected else "",
            "RespawnUtc": selected_row.get("timestamp", "") if selected else "",
            "RespawnDelaySeconds": (
                f"{max(0, (selected_time - death_time).total_seconds()):.3f}"
                if selected_time
                else ""
            ),
            "RespawnAfterCorpseGoneSeconds": (
                f"{max(0, (selected_time - corpse_gone_time).total_seconds()):.3f}"
                if selected_time and corpse_gone_time
                else ""
            ),
            "RespawnX": selected_row.get("x", "") if selected else "",
            "RespawnY": selected_row.get("y", "") if selected else "",
            "RespawnZ": selected_row.get("z", "") if selected else "",
            "PositionDelta": f"{selected_delta:.3f}" if selected_delta is not None else "",
            "ElapsedAfterDeathSeconds": f"{max(0, (elapsed_end - death_time).total_seconds()):.3f}",
            "CandidateCount": str(len(candidates)),
            "Detail": (
                "Matched later same-name/same-monsterData/same-position spawn."
                if selected
                else "No later same-name/same-monsterData/same-position spawn was observed before capture stop/crash."
            ),
        })

    output_csv = output_csv or capture / "enemy-respawns.csv"
    with output_csv.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=RESPAWN_CSV_HEADER)
        writer.writeheader()
        writer.writerows(observations)

    return {
        "outputCsv": output_csv,
        "rows": len(observations),
        "completeRows": sum(1 for row in observations if row["Status"] == "complete"),
        "ambiguousRows": sum(1 for row in observations if row["Status"] == "ambiguous"),
        "incompleteRows": sum(1 for row in observations if row["Status"] == "incomplete"),
        "skippedStateRows": skipped_state_rows,
    }


def make_raw_packet(message_type, marker):
    data = bytearray(24)
    struct.pack_into(">H", data, 6, len(data))
    struct.pack_into(">I", data, 16, message_type)
    data[-1] = marker
    return bytes(data)


def write_raw_index(path, rows):
    header = [
        "CapturedUtc", "ElapsedMilliseconds", "Direction", "GlobalOrdinal",
        "Sequence", "PacketLength", "N3TypeValue", "N3TypeName",
        "IdentityType", "IdentityInstance", "PreservationStatus", "RawHex",
    ]
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=header)
        writer.writeheader()
        writer.writerows(rows)


def self_test_check(condition, message):
    if not condition:
        raise RuntimeError(f"self-test failed: {message}")


def run_self_tests():
    tests = []
    test_root = Path(__file__).resolve().parent
    with tempfile.TemporaryDirectory(prefix=".npc-lifecycle-self-test-", dir=test_root) as name:
        root = Path(name)

        raw_only = root / "raw-index-only"
        raw_only.mkdir()
        packet_one = make_raw_packet(SIMPLE_CHAR_FULL_UPDATE, 1)
        packet_two = make_raw_packet(CORPSE_FULL_UPDATE, 2)
        write_raw_index(
            raw_only / RAW_PACKET_INDEX_NAME,
            [
                {
                    "CapturedUtc": "2026-01-01T00:00:01Z", "Direction": "IN",
                    "GlobalOrdinal": 1, "Sequence": 10, "PacketLength": len(packet_one),
                    "N3TypeValue": 0, "N3TypeName": "WrongLabel",
                    "PreservationStatus": "raw_complete", "RawHex": packet_one.hex(),
                },
                {
                    "CapturedUtc": "2026-01-01T00:00:02Z", "Direction": "OUT",
                    "GlobalOrdinal": 2, "Sequence": 11, "PacketLength": len(packet_two),
                    "N3TypeValue": 0, "N3TypeName": "WrongLabel",
                    "PreservationStatus": "raw_complete", "RawHex": packet_two.hex(),
                },
            ],
        )
        lines, source = load_packet_lines(raw_only)
        self_test_check(source["canonicalValid"] and len(lines) == 2, "raw-index-only")
        self_test_check(
            "n3=SimpleCharFullUpdate" in lines[0] and "n3=CorpseFullUpdate" in lines[1],
            "raw-byte message types must override labels",
        )
        tests.append("raw-index-only")

        union = root / "union"
        union.mkdir()
        packets = [make_raw_packet(SIMPLE_CHAR_FULL_UPDATE, marker) for marker in (3, 4, 5)]
        (union / "packets.hex.log").write_text(
            "2026-01-01T00:00:01Z IN #20 len=24 type=unknown hex=" + packets[0].hex() + "\n"
            "2026-01-01T00:00:03Z IN #22 len=24 n3=Wrong hex=" + packets[2].hex() + "\n",
            encoding="utf-8",
        )
        write_raw_index(
            union / RAW_PACKET_INDEX_NAME,
            [
                {
                    "CapturedUtc": f"2026-01-01T00:00:0{index}Z", "Direction": "IN",
                    "GlobalOrdinal": index, "Sequence": 19 + index,
                    "PacketLength": len(packet), "PreservationStatus": "raw_complete",
                    "RawHex": packet.hex(),
                }
                for index, packet in enumerate(packets, 1)
            ],
        )
        lines, source = load_packet_lines(union)
        sequences = [int(PACKET_RE.match(line).group("sequence")) for line in lines]
        self_test_check(
            source["canonicalValid"] and sequences == [20, 21, 22],
            "canonical union ordering and deduplication",
        )
        tests.append("union-order-dedupe")

        finalized_window = root / "finalized-window"
        finalized_window.mkdir()
        window_packets = [
            make_raw_packet(SIMPLE_CHAR_FULL_UPDATE, marker)
            for marker in (31, 32, 33)
        ]
        (finalized_window / "capture_info.json").write_text(
            json.dumps(
                {
                    "captureStartUtc": "2026-01-01T00:00:01Z",
                    "captureEndUtc": "2026-01-01T00:00:02Z",
                    "packetCounts": {"inboundRaw": 2, "outboundRaw": 0},
                    "validation": {"status": "complete"},
                }
            ),
            encoding="utf-8",
        )
        (finalized_window / "packets.hex.log").write_text(
            "".join(
                f"2026-01-01T00:00:0{index}Z IN #{30 + index} "
                f"len=24 n3=Wrong hex={packet.hex()}\n"
                for index, packet in enumerate(window_packets, 1)
            ),
            encoding="utf-8",
        )
        write_raw_index(
            finalized_window / RAW_PACKET_INDEX_NAME,
            [
                {
                    "CapturedUtc": f"2026-01-01T00:00:0{index}Z",
                    "Direction": "IN",
                    "GlobalOrdinal": index,
                    "Sequence": 30 + index,
                    "PacketLength": len(packet),
                    "PreservationStatus": "raw_complete",
                    "RawHex": packet.hex(),
                }
                for index, packet in enumerate(window_packets, 1)
            ],
        )
        lines, source = load_packet_lines(finalized_window)
        self_test_check(
            source["canonicalValid"]
            and len(lines) == 2
            and source["packetLog"]["outsideCaptureWindowRows"] == 1
            and source["rawPacketIndex"]["outsideCaptureWindowRows"] == 1,
            "trailing rows after a finalized legacy capture must be excluded",
        )
        tests.append("finalized-window")

        projection_incomplete = root / "projection-incomplete"
        projection_incomplete.mkdir()
        (projection_incomplete / "capture_info.json").write_text(
            json.dumps(
                {
                    "captureEndUtc": "2026-01-01T00:00:03Z",
                    "packetCounts": {"inboundRaw": 1, "outboundRaw": 0},
                    "validation": {
                        "status": "incomplete",
                        "recaptureRequired": False,
                        "offlineDecodeRequired": True,
                    },
                }
            ),
            encoding="utf-8",
        )
        (projection_incomplete / "packets.hex.log").write_text(
            "2026-01-01T00:00:01Z IN #23 len=24 n3=Wrong hex="
            + packet_one.hex()
            + "\n",
            encoding="utf-8",
        )
        write_raw_index(
            projection_incomplete / RAW_PACKET_INDEX_NAME,
            [{
                "CapturedUtc": "2026-01-01T00:00:01Z", "Direction": "IN",
                "GlobalOrdinal": 1, "Sequence": 23, "PacketLength": len(packet_one),
                "PreservationStatus": "raw_complete", "RawHex": packet_one.hex(),
            }],
        )
        lines, source = load_packet_lines(projection_incomplete)
        self_test_check(
            source["canonicalValid"]
            and not source["recaptureRequired"]
            and len(lines) == 1,
            "projection-only incomplete capture must remain offline-decodable",
        )
        tests.append("projection-incomplete-offline-decode")

        start_only_metadata = root / "start-only-metadata"
        start_only_metadata.mkdir()
        (start_only_metadata / "capture_info.json").write_text(
            json.dumps(
                {
                    "captureStartUtc": "2026-01-01T00:00:01Z",
                    "packetCounts": {"inboundRaw": 0, "outboundRaw": 0},
                    "validation": {
                        "status": "running",
                        "processingAllowed": False,
                    },
                }
            ),
            encoding="utf-8",
        )
        (start_only_metadata / "packets.hex.log").write_text(
            "2026-01-01T00:00:01Z IN #24 len=24 n3=Wrong hex="
            + packet_one.hex()
            + "\n",
            encoding="utf-8",
        )
        lines, source = load_packet_lines(start_only_metadata)
        self_test_check(
            source["canonicalValid"]
            and source["legacyInference"]
            and not source["captureInfo"]["finalized"]
            and len(lines) == 1,
            "clean raw evidence must remain recoverable from start-only metadata",
        )
        tests.append("start-only-metadata-structural-inference")

        terminal_partial = root / "start-only-terminal-partial"
        terminal_partial.mkdir()
        (terminal_partial / "capture_info.json").write_text(
            json.dumps(
                {
                    "captureStartUtc": "2026-01-01T00:00:01Z",
                    "validation": {"status": "running"},
                }
            ),
            encoding="utf-8",
        )
        (terminal_partial / "packets.hex.log").write_text(
            "2026-01-01T00:00:01Z IN #25 len=24 n3=Wrong hex="
            + packet_one.hex()
            + "\n2026-01-01T00:00:02Z IN #26 len=24 n3=Wrong hex=001\n",
            encoding="utf-8",
        )
        lines, source = load_packet_lines(terminal_partial)
        self_test_check(
            source["canonicalValid"]
            and not source["recaptureRequired"]
            and source["terminalPartialTailSalvaged"]
            and source["capabilityStatus"]
            == "raw_source_legacy_terminal_tail_salvaged"
            and source["positiveEvidenceUsable"]
            and source["positiveEvidenceOnly"]
            and not source["captureComplete"]
            and not source["absenceInferenceAllowed"]
            and not source["packetLog"]["complete"]
            and source["packetLog"]["terminalPartialRows"] == 1
            and source["packetLog"]["terminalPartialRowNumbers"] == [2]
            and source["packetLog"]["terminalPartialSalvaged"]
            and len(lines) == 1,
            "one final partial write must expose only the valid positive prefix",
        )
        tests.append("start-only-terminal-partial-positive-evidence")

        internal_partial = root / "start-only-internal-partial"
        internal_partial.mkdir()
        (internal_partial / "capture_info.json").write_text(
            (terminal_partial / "capture_info.json").read_text(encoding="utf-8"),
            encoding="utf-8",
        )
        (internal_partial / "packets.hex.log").write_text(
            "2026-01-01T00:00:01Z IN #27 len=24 n3=Wrong hex=001\n"
            "2026-01-01T00:00:02Z IN #28 len=24 n3=Wrong hex="
            + packet_one.hex()
            + "\n",
            encoding="utf-8",
        )
        _, source = load_packet_lines(internal_partial)
        self_test_check(
            source["recaptureRequired"]
            and not source["canonicalValid"]
            and not source["terminalPartialTailSalvaged"],
            "an internal partial row must remain fail-closed",
        )
        tests.append("start-only-internal-partial-fails-closed")

        multiple_partial = root / "start-only-multiple-partial"
        multiple_partial.mkdir()
        (multiple_partial / "capture_info.json").write_text(
            (terminal_partial / "capture_info.json").read_text(encoding="utf-8"),
            encoding="utf-8",
        )
        (multiple_partial / "packets.hex.log").write_text(
            "2026-01-01T00:00:01Z IN #29 len=24 n3=Wrong hex="
            + packet_one.hex()
            + "\n2026-01-01T00:00:02Z IN #30 len=24 n3=Wrong hex=001\n"
            "2026-01-01T00:00:03Z IN #31 len=24 n3=Wrong hex=003\n",
            encoding="utf-8",
        )
        _, source = load_packet_lines(multiple_partial)
        self_test_check(
            source["recaptureRequired"]
            and not source["canonicalValid"]
            and source["packetLog"]["invalidRows"] == 2,
            "multiple malformed rows must remain fail-closed",
        )
        tests.append("start-only-multiple-partial-fails-closed")

        arbitrary_terminal = root / "start-only-arbitrary-terminal"
        arbitrary_terminal.mkdir()
        (arbitrary_terminal / "capture_info.json").write_text(
            (terminal_partial / "capture_info.json").read_text(encoding="utf-8"),
            encoding="utf-8",
        )
        (arbitrary_terminal / "packets.hex.log").write_text(
            "2026-01-01T00:00:01Z IN #32 len=24 n3=Wrong hex="
            + packet_one.hex()
            + "\ntruncated garbage\n",
            encoding="utf-8",
        )
        _, source = load_packet_lines(arbitrary_terminal)
        self_test_check(
            source["recaptureRequired"]
            and not source["canonicalValid"]
            and source["packetLog"]["terminalPartialRows"] == 0,
            "an arbitrary malformed final row must remain fail-closed",
        )
        tests.append("start-only-arbitrary-terminal-fails-closed")

        explicit_recapture = root / "start-only-explicit-recapture"
        explicit_recapture.mkdir()
        (explicit_recapture / "capture_info.json").write_text(
            json.dumps(
                {
                    "captureStartUtc": "2026-01-01T00:00:01Z",
                    "validation": {
                        "status": "running",
                        "recaptureRequired": True,
                    },
                }
            ),
            encoding="utf-8",
        )
        (explicit_recapture / "packets.hex.log").write_text(
            "2026-01-01T00:00:01Z IN #25 len=24 n3=Wrong hex="
            + packet_one.hex()
            + "\n2026-01-01T00:00:02Z IN #26 len=24 n3=Wrong hex=001\n",
            encoding="utf-8",
        )
        _, source = load_packet_lines(explicit_recapture)
        self_test_check(
            source["recaptureRequired"]
            and not source["canonicalValid"]
            and not source["terminalPartialTailSalvaged"],
            "explicit recapture-required metadata must remain fail-closed",
        )
        tests.append("start-only-explicit-recapture-fails-closed")

        terminal_partial_conflict = root / "terminal-partial-conflict"
        terminal_partial_conflict.mkdir()
        (terminal_partial_conflict / "capture_info.json").write_text(
            (terminal_partial / "capture_info.json").read_text(encoding="utf-8"),
            encoding="utf-8",
        )
        conflict_left = make_raw_packet(SIMPLE_CHAR_FULL_UPDATE, 41)
        conflict_right = make_raw_packet(SIMPLE_CHAR_FULL_UPDATE, 42)
        (terminal_partial_conflict / "packets.hex.log").write_text(
            "2026-01-01T00:00:01Z IN #33 len=24 n3=Wrong hex="
            + conflict_left.hex()
            + "\n2026-01-01T00:00:02Z IN #34 len=24 n3=Wrong hex=001\n",
            encoding="utf-8",
        )
        write_raw_index(
            terminal_partial_conflict / RAW_PACKET_INDEX_NAME,
            [{
                "CapturedUtc": "2026-01-01T00:00:01Z", "Direction": "IN",
                "GlobalOrdinal": 1, "Sequence": 33,
                "PacketLength": len(conflict_right),
                "PreservationStatus": "raw_complete", "RawHex": conflict_right.hex(),
            }],
        )
        _, source = load_packet_lines(terminal_partial_conflict)
        self_test_check(
            source["recaptureRequired"]
            and not source["canonicalValid"]
            and source["conflictCount"] == 1
            and not source["terminalPartialTailSalvaged"],
            "a terminal partial row must never mask a cross-sink conflict",
        )
        tests.append("terminal-partial-conflict-fails-closed")

        finalized_partial_mismatch = root / "finalized-partial-count-mismatch"
        finalized_partial_mismatch.mkdir()
        (finalized_partial_mismatch / "capture_info.json").write_text(
            json.dumps(
                {
                    "captureStartUtc": "2026-01-01T00:00:01Z",
                    "captureEndUtc": "2026-01-01T00:00:02Z",
                    "packetCounts": {"inboundRaw": 1, "outboundRaw": 0},
                    "validation": {"status": "complete"},
                }
            ),
            encoding="utf-8",
        )
        (finalized_partial_mismatch / "packets.hex.log").write_text(
            "2026-01-01T00:00:01Z IN #35 len=24 n3=Wrong hex="
            + packet_one.hex()
            + "\n2026-01-01T00:00:02Z IN #36 len=24 n3=Wrong hex=001\n",
            encoding="utf-8",
        )
        _, source = load_packet_lines(finalized_partial_mismatch)
        self_test_check(
            source["recaptureRequired"]
            and not source["canonicalValid"]
            and not source["terminalPartialTailSalvaged"]
            and source["observedPackets"] == 1,
            "a finalized count mismatch must never use terminal-tail salvage",
        )
        tests.append("finalized-partial-count-mismatch-fails-closed")

        conflict = root / "conflict"
        conflict.mkdir()
        left = make_raw_packet(SIMPLE_CHAR_FULL_UPDATE, 6)
        right = make_raw_packet(SIMPLE_CHAR_FULL_UPDATE, 7)
        (conflict / "packets.hex.log").write_text(
            f"2026-01-01T00:00:01Z IN #30 len=24 n3=Wrong hex={left.hex()}\n",
            encoding="utf-8",
        )
        write_raw_index(
            conflict / RAW_PACKET_INDEX_NAME,
            [{
                "CapturedUtc": "2026-01-01T00:00:01Z", "Direction": "IN",
                "GlobalOrdinal": 1, "Sequence": 30, "PacketLength": len(right),
                "PreservationStatus": "raw_complete", "RawHex": right.hex(),
            }],
        )
        _, source = load_packet_lines(conflict)
        self_test_check(
            source["recaptureRequired"] and source["conflictCount"] == 1,
            "direction and sequence conflict",
        )
        tests.append("conflict")

        mismatch = root / "length-mismatch"
        mismatch.mkdir()
        write_raw_index(
            mismatch / RAW_PACKET_INDEX_NAME,
            [{
                "CapturedUtc": "2026-01-01T00:00:01Z", "Direction": "IN",
                "GlobalOrdinal": 1, "Sequence": 40, "PacketLength": len(left) + 1,
                "PreservationStatus": "raw_complete", "RawHex": left.hex(),
            }],
        )
        _, source = load_packet_lines(mismatch)
        self_test_check(
            source["recaptureRequired"]
            and source["rawPacketIndex"]["invalidRows"] == 1,
            "raw length mismatch",
        )
        tests.append("length-mismatch")

        frame_mismatch = root / "frame-length-mismatch"
        frame_mismatch.mkdir()
        bad_frame = bytearray(left)
        struct.pack_into(">H", bad_frame, 6, len(bad_frame) - 1)
        write_raw_index(
            frame_mismatch / RAW_PACKET_INDEX_NAME,
            [{
                "CapturedUtc": "2026-01-01T00:00:01Z", "Direction": "IN",
                "GlobalOrdinal": 1, "Sequence": 41, "PacketLength": len(bad_frame),
                "PreservationStatus": "raw_complete", "RawHex": bad_frame.hex(),
            }],
        )
        _, source = load_packet_lines(frame_mismatch)
        self_test_check(
            source["recaptureRequired"]
            and source["rawPacketIndex"]["invalidRows"] == 1,
            "SCFU frame header length mismatch",
        )
        tests.append("frame-length-mismatch")

        reused_corpse = root / "reused-corpse-generation"
        reused_corpse.mkdir()
        corpse_csv = reused_corpse / "corpse-full-updates.csv"
        with corpse_csv.open("w", encoding="utf-8", newline="") as handle:
            writer = csv.DictWriter(
                handle,
                fieldnames=[
                    "CapturedUtc", "CorpseIdentity", "DeadNpcIdentity",
                    "CorpseMonsterData", "CorpseCredits", "PlayfieldId",
                ],
            )
            writer.writeheader()
            writer.writerows(
                [
                    {
                        "CapturedUtc": "2026-01-01T00:00:01Z",
                        "CorpseIdentity": "Corpse:00F69001",
                        "DeadNpcIdentity": "SimpleChar:00000011",
                        "CorpseMonsterData": "111",
                        "CorpseCredits": "11",
                        "PlayfieldId": "127",
                    },
                    {
                        "CapturedUtc": "2026-01-01T00:00:01.500Z",
                        "CorpseIdentity": "Corpse:00F69001",
                        "DeadNpcIdentity": "SimpleChar:00000011",
                        "CorpseMonsterData": "111",
                        "CorpseCredits": "12",
                        "PlayfieldId": "127",
                    },
                    {
                        "CapturedUtc": "2026-01-01T00:00:03Z",
                        "CorpseIdentity": "Corpse:00F69001",
                        "DeadNpcIdentity": "SimpleChar:00000011",
                        "CorpseMonsterData": "111",
                        "CorpseCredits": "13",
                        "PlayfieldId": "127",
                    },
                    {
                        "CapturedUtc": "2026-01-01T00:00:05Z",
                        "CorpseIdentity": "Corpse:00F69001",
                        "DeadNpcIdentity": "SimpleChar:00000022",
                        "CorpseMonsterData": "222",
                        "CorpseCredits": "22",
                        "PlayfieldId": "127",
                    },
                ]
            )
        loot_csv = reused_corpse / "corpse-loot-observations.csv"
        loot_fields = [
            "CapturedUtc", "CorpseIdentity", "OpenOrdinal", "InitialSnapshot",
            "DeadNpcIdentity", "EnemyName", "MonsterData", "EnemyLevel",
            "CorpseCredits", "PlayfieldId", "CorrelationStatus",
        ]
        with loot_csv.open("w", encoding="utf-8", newline="") as handle:
            writer = csv.DictWriter(handle, fieldnames=loot_fields)
            writer.writeheader()
            writer.writerows(
                [
                    {
                        "CapturedUtc": "2026-01-01T00:00:01.250Z",
                        "CorpseIdentity": "(Corpse:F69001)",
                    },
                    {
                        "CapturedUtc": "2026-01-01T00:00:02Z",
                        "CorpseIdentity": "(Corpse:F69001)",
                        "OpenOrdinal": "2",
                        "InitialSnapshot": "false",
                        "DeadNpcIdentity": "SimpleChar:00000011",
                    },
                    {
                        "CapturedUtc": "2026-01-01T00:00:03Z",
                        "CorpseIdentity": "(Corpse:F69001)",
                        "OpenOrdinal": "3",
                        "InitialSnapshot": "false",
                        "DeadNpcIdentity": "SimpleChar:00000011",
                        "EnemyName": "Boundary Evidence",
                        "CorrelationStatus": "linked-live-generation",
                    },
                    {
                        "CapturedUtc": "2026-01-01T00:00:04Z",
                        "CorpseIdentity": "(Corpse:F69001)",
                        "OpenOrdinal": "4",
                        "InitialSnapshot": "false",
                        "DeadNpcIdentity": "SimpleChar:00000011",
                    },
                    {
                        "CapturedUtc": "2026-01-01T00:00:06Z",
                        "CorpseIdentity": "(Corpse:F69001)",
                        "OpenOrdinal": "3",
                        "InitialSnapshot": "false",
                        "DeadNpcIdentity": "SimpleChar:00000011",
                    },
                    {
                        "CapturedUtc": "2026-01-01T00:00:07Z",
                        "CorpseIdentity": "(Corpse:F69002)",
                        "OpenOrdinal": "4",
                        "InitialSnapshot": "false",
                        "DeadNpcIdentity": "SimpleChar:00000033",
                        "EnemyName": "Preserved Enemy",
                        "MonsterData": "333",
                        "EnemyLevel": "33",
                        "CorpseCredits": "33",
                        "PlayfieldId": "127",
                        "CorrelationStatus": "linked-live-generation",
                    },
                ]
            )
        with (reused_corpse / "npc-lifecycle.csv").open(
            "w", encoding="utf-8", newline=""
        ) as handle:
            writer = csv.DictWriter(
                handle, fieldnames=["CapturedUtc", "Phase", "PrimaryIdentity"]
            )
            writer.writeheader()
            writer.writerows(
                [
                    {
                        "CapturedUtc": "2026-01-01T00:00:03Z",
                        "Phase": "corpse-gone",
                        "PrimaryIdentity": "(Corpse:F69001)",
                    },
                    {
                        "CapturedUtc": "",
                        "Phase": "corpse-gone",
                        "PrimaryIdentity": "(Corpse:F69001)",
                    },
                ]
            )
        (reused_corpse / "enemy-dossier.json").write_text(
            json.dumps(
                {
                    "enemies": [
                        {
                            "identity": "(SimpleChar:00000011)",
                            "name": "First Enemy",
                            "monsterData": "111",
                            "level": 11,
                        },
                        {
                            "identity": "(SimpleChar:00000022)",
                            "name": "Second Enemy",
                            "monsterData": "222",
                            "level": 22,
                        },
                    ]
                }
            ),
            encoding="utf-8",
        )
        rebound_csv = reused_corpse / "corpse-loot-observations.pending.csv"
        rebound = rebind_corpse_loot_observations(
            reused_corpse, corpse_csv, rebound_csv
        )
        with rebound_csv.open("r", encoding="utf-8", newline="") as handle:
            rebound_rows = list(csv.DictReader(handle))
        self_test_check(
            rebound["linkedRows"] == 4
            and rebound["unlinkedRows"] == 2
            and rebound["ambiguousRows"] == 1
            and [rebound_rows[index]["OpenOrdinal"] for index in (0, 1, 3, 4)]
            == ["1", "2", "1", "1"]
            and [rebound_rows[index]["InitialSnapshot"] for index in (0, 1, 3, 4)]
            == ["true", "false", "true", "true"]
            and [rebound_rows[index]["EnemyName"] for index in (0, 1, 3, 4)]
            == ["First Enemy", "First Enemy", "First Enemy", "Second Enemy"]
            and [rebound_rows[index]["CorpseCredits"] for index in (0, 1, 3, 4)]
            == ["11", "12", "13", "22"]
            and rebound_rows[2]["EnemyName"] == "Boundary Evidence"
            and rebound_rows[5]["EnemyName"] == "Preserved Enemy"
            and rebound_rows[5]["CorpseCredits"] == "33"
            and rebound_rows[5]["CorrelationStatus"] == "linked-live-generation",
            "reused corpse identities must bind separate loot generations",
        )
        tests.append("equal-time-generation-boundary-fails-closed")
        tests.append("repeated-cfu-same-generation")
        tests.append("unmatched-live-correlation-preservation")
        gone = load_corpse_gone_times(reused_corpse / "npc-lifecycle.csv")
        self_test_check(
            len(gone["CORPSE:00F69001"]) == 1,
            "gone events must remain generation-selectable",
        )
        tests.append("reused-corpse-generation")

        corpse_pattern_cases = (
            (
                "no-corpse-evidence",
                (0, [], [], False),
                (True, "no_corpse_full_update_observed"),
            ),
            (
                "snapshot-only-no-raw-cfu",
                (0, [], [], True),
                (True, "no_raw_corpse_full_update_observed"),
            ),
            (
                "raw-cfu-fully-decoded",
                (1, [["decoded"]], [], True),
                (True, "corpse_full_update_decode_complete"),
            ),
            (
                "raw-cfu-decode-error",
                (1, [], [{"line": 1, "error": "malformed"}], True),
                (False, "offline_corpse_decode_required"),
            ),
        )
        for case_name, arguments, expected in corpse_pattern_cases:
            self_test_check(
                classify_corpse_decode(*arguments) == expected,
                case_name,
            )
            tests.append(case_name)

        preservation = root / "preservation"
        preservation.mkdir()
        final = preservation / "derived.csv"
        pending = pending_path(final)
        final.write_text("good\n", encoding="utf-8")
        pending.write_text("bad\n", encoding="utf-8")
        promoted = promote_pending_outputs([(pending, final)], False)
        self_test_check(
            not promoted and final.read_text(encoding="utf-8") == "good\n",
            "failed validation must preserve prior outputs",
        )
        tests.append("prior-output-preservation")

    print("self-test PASS: " + ", ".join(tests))


def skipped_scfu_summary(capture, raw_scfu_packets):
    analyzer_path = (
        Path(__file__).resolve().parents[1]
        / "AOSharpCaptureAnalyzer"
        / "bin"
        / "Debug"
        / "AOSharpCaptureAnalyzer.exe"
    )
    output_path = capture / SCFU_OUTPUT_NAME
    error_path = capture / SCFU_ERROR_OUTPUT_NAME
    return {
        "analyzerPath": str(analyzer_path),
        "analyzerAvailable": analyzer_path.exists(),
        "analyzerInvoked": False,
        "analyzerExitCode": None,
        "analyzerError": "Raw source validation failed; analyzer was not invoked.",
        "rawPackets": raw_scfu_packets,
        "outputRows": 0,
        "decodedRows": 0,
        "pendingRows": 0,
        "decodeErrors": 0,
        "outputCsv": str(output_path),
        "errorCsv": str(error_path),
        "pendingOutputCsv": str(pending_path(output_path)),
        "pendingErrorCsv": str(pending_path(error_path)),
        "capabilityStatus": "raw_source_recapture_required",
        "recaptureRequired": True,
        "offlineDecodeRequired": False,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("capture_folder", nargs="?", type=Path)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        run_self_tests()
        return
    if args.capture_folder is None:
        parser.error("capture_folder is required unless --self-test is used")

    capture = args.capture_folder.resolve()
    packet_lines, source_summary = load_packet_lines(capture)
    raw_source_valid = source_summary["canonicalValid"]
    raw_scfu_packets = count_raw_scfu_packets(packet_lines)
    raw_corpse_full_update_packets = count_raw_corpse_full_update_packets(
        packet_lines
    )
    scfu_summary = (
        run_scfu_analyzer(capture, packet_lines, raw_scfu_packets)
        if raw_source_valid
        else skipped_scfu_summary(capture, raw_scfu_packets)
    )

    rows = []
    errors = []
    for line_number, line in enumerate(packet_lines, 1):
        match = PACKET_RE.match(line)
        if not match or match.group("message") != "CorpseFullUpdate":
            continue
        try:
            rows.append(decode_corpse_full_update(match))
        except Exception as exc:
            errors.append({"line": line_number, "error": str(exc)})

    output_csv = capture / "corpse-full-updates.csv"
    pending_output_csv = pending_path(output_csv)
    with pending_output_csv.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(CSV_HEADER)
        writer.writerows(rows)

    event_counts = count_event_lines(capture / "events.log")
    corpse_inventory_updates = count_corpse_inventory_updates(capture / "inventory-updates.csv")
    respawn_csv = capture / "enemy-respawns.csv"
    pending_respawn_csv = pending_path(respawn_csv)
    respawn_summary = build_enemy_respawns(
        capture,
        pending_output_csv,
        output_csv=pending_respawn_csv,
        scfu_csv=Path(scfu_summary["pendingOutputCsv"]),
    )
    loot_observations_csv = capture / "corpse-loot-observations.csv"
    pending_loot_observations_csv = pending_path(loot_observations_csv)
    loot_rebind_summary = rebind_corpse_loot_observations(
        capture,
        pending_output_csv,
        pending_loot_observations_csv,
        scfu_csv=Path(scfu_summary["pendingOutputCsv"]),
    )
    corpse_evidence_observed = bool(
        rows
        or event_counts["corpseSeen"]
        or event_counts["corpseUses"]
        or corpse_inventory_updates
    )
    corpse_processing_allowed, corpse_capability_status = classify_corpse_decode(
        raw_corpse_full_update_packets,
        rows,
        errors,
        corpse_evidence_observed,
    )
    processing_allowed = bool(
        raw_source_valid
        and corpse_processing_allowed
        and loot_rebind_summary["ambiguousRows"] == 0
        and not scfu_summary["offlineDecodeRequired"]
    )
    if (
        source_summary["recaptureRequired"]
        or source_summary["terminalPartialTailSalvaged"]
    ):
        capability_status = source_summary["capabilityStatus"]
    elif not corpse_processing_allowed:
        capability_status = "offline_corpse_decode_required"
    else:
        capability_status = scfu_summary["capabilityStatus"]

    summary_path = capture / "npc-lifecycle-summary.json"
    pending_summary_path = pending_path(summary_path)
    promotion_pairs = [
        (pending_output_csv, output_csv),
        (pending_respawn_csv, respawn_csv),
    ]
    if loot_rebind_summary["available"]:
        promotion_pairs.append(
            (pending_loot_observations_csv, loot_observations_csv)
        )
    scfu_pending_pairs = [
        (Path(scfu_summary["pendingOutputCsv"]), Path(scfu_summary["outputCsv"])),
        (Path(scfu_summary["pendingErrorCsv"]), Path(scfu_summary["errorCsv"])),
    ]
    if all(pending.exists() for pending, _ in scfu_pending_pairs):
        promotion_pairs.extend(scfu_pending_pairs)

    summary = {
        "captureFolder": str(capture),
        "rawCorpseFullUpdatePackets": raw_corpse_full_update_packets,
        "corpseFullUpdateRows": len(rows),
        "corpseFullUpdateDecodeErrors": errors,
        "corpseFullUpdateDecodeErrorCount": len(errors),
        "corpseCapabilityStatus": corpse_capability_status,
        "localCorpseEvidenceObserved": corpse_evidence_observed,
        "corpseInventoryUpdateRows": corpse_inventory_updates,
        "enemyRespawnRows": respawn_summary["rows"],
        "enemyRespawnCompleteRows": respawn_summary["completeRows"],
        "enemyRespawnAmbiguousRows": respawn_summary["ambiguousRows"],
        "enemyRespawnIncompleteRows": respawn_summary["incompleteRows"],
        "enemyStateSkippedRows": respawn_summary["skippedStateRows"],
        "corpseLootRebind": loot_rebind_summary,
        "lifecycleCounts": event_counts,
        "rawSimpleCharFullUpdatePackets": scfu_summary["rawPackets"],
        "simpleCharFullUpdateOutputRows": scfu_summary["outputRows"],
        "decodedSimpleCharFullUpdateRows": scfu_summary["decodedRows"],
        "pendingSimpleCharFullUpdateRows": scfu_summary["pendingRows"],
        "simpleCharFullUpdateDecodeErrors": scfu_summary["decodeErrors"],
        "recaptureRequired": source_summary["recaptureRequired"],
        "captureComplete": source_summary["captureComplete"],
        "positiveEvidenceUsable": source_summary["positiveEvidenceUsable"],
        "positiveEvidenceOnly": source_summary["positiveEvidenceOnly"],
        "absenceInferenceAllowed": source_summary["absenceInferenceAllowed"],
        "offlineDecodeRequired": bool(
            not source_summary["recaptureRequired"]
            and (not corpse_processing_allowed or scfu_summary["offlineDecodeRequired"])
        ),
        "capabilityStatus": capability_status,
        "corpseProcessingAllowed": corpse_processing_allowed,
        "processingAllowed": processing_allowed,
        "outputsPromoted": processing_allowed,
        "rawSource": source_summary,
        "offlineAnalyzer": scfu_summary,
        "outputs": {
            "corpseFullUpdatesCsv": str(output_csv),
            "pendingCorpseFullUpdatesCsv": str(pending_output_csv),
            "enemyRespawnsCsv": str(respawn_csv),
            "pendingEnemyRespawnsCsv": str(pending_respawn_csv),
            "corpseLootObservationsCsv": str(loot_observations_csv),
            "pendingCorpseLootObservationsCsv": str(pending_loot_observations_csv),
            "simpleCharFullUpdatesCsv": scfu_summary["outputCsv"],
            "pendingSimpleCharFullUpdatesCsv": scfu_summary["pendingOutputCsv"],
            "simpleCharFullUpdateErrorsCsv": scfu_summary["errorCsv"],
            "pendingSimpleCharFullUpdateErrorsCsv": scfu_summary["pendingErrorCsv"],
            "summaryJson": str(summary_path),
            "pendingSummaryJson": str(pending_summary_path),
        },
    }
    pending_summary_path.write_text(
        json.dumps(summary, indent=2) + "\n", encoding="utf-8"
    )
    promotion_pairs.append((pending_summary_path, summary_path))
    promote_pending_outputs(promotion_pairs, processing_allowed)

    print(
        "rawCorpseFullUpdatePackets="
        f"{raw_corpse_full_update_packets} "
        f"corpseFullUpdateRows={len(rows)} "
        f"decodeErrors={len(errors)} "
        f"corpseCapabilityStatus={corpse_capability_status}"
    )
    print(
        "enemyRespawnRows="
        f"{respawn_summary['rows']} complete={respawn_summary['completeRows']} "
        f"ambiguous={respawn_summary['ambiguousRows']} "
        f"incomplete={respawn_summary['incompleteRows']} "
        f"skippedStateRows={respawn_summary['skippedStateRows']}"
    )
    print(
        "rawSimpleCharFullUpdatePackets="
        f"{scfu_summary['rawPackets']} "
        f"outputRows={scfu_summary['outputRows']} "
        f"decodedRows={scfu_summary['decodedRows']} "
        f"pendingRows={scfu_summary['pendingRows']} "
        f"decodeErrors={scfu_summary['decodeErrors']} "
        f"capabilityStatus={capability_status} "
        f"recaptureRequired={str(source_summary['recaptureRequired']).lower()} "
        f"offlineDecodeRequired={str(summary['offlineDecodeRequired']).lower()}"
    )
    print(
        "corpseLootRebindRows="
        f"{loot_rebind_summary['rows']} "
        f"linked={loot_rebind_summary['linkedRows']} "
        f"unlinked={loot_rebind_summary['unlinkedRows']} "
        f"ambiguous={loot_rebind_summary['ambiguousRows']}"
    )
    print(f"processingAllowed={str(processing_allowed).lower()}")
    print(output_csv if processing_allowed else pending_output_csv)
    print(respawn_csv if processing_allowed else pending_respawn_csv)
    print(summary_path if processing_allowed else pending_summary_path)
    if not processing_allowed:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
