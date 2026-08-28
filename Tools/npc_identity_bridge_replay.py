#!/usr/bin/env python3
"""Reconstruct NPC identity bridge evidence from a finalized capture.

The replay consumes the bridge plugin's JSONL event stream plus the existing
offline analyzer's SCFU and Stat projections.  Packet evidence is joined only
through an explicit direction/sequence/global-ordinal reference and is then
checked against the recorded zone-epoch ordinal boundaries.  Timestamps,
names, proximity, appearance, and capture-folder labels never assign epochs.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any, Iterable, Mapping


SCHEMA_VERSION = 1
UNSET_SENTINEL = 1234567890
MODEL_RESOURCE_TYPE = 1000014
DEFAULT_LIVE_JSONL = "npc-identity-bridge-live.jsonl"
DEFAULT_SCFU_CSV = "scfu-appearance.csv"
DEFAULT_STAT_CSV = "npc-stat-observations.csv"
DEFAULT_OUTPUT_JSON = "npc-identity-bridge.json"

EVIDENCE_CLASSIFICATIONS = frozenset(
    {
        "packet-observed",
        "client-state-observed",
        "derived",
        "not-observed",
        "sentinel/default",
    }
)
DIRECT_CLASSIFICATIONS = frozenset({"packet-observed", "client-state-observed"})
SCFU_FLAG_HAS_PLAYFIELD = 0x00000040
SCFU_FLAG_HAS_HEAD_MESH = 0x00000080
SCFU_FLAG_HAS_HEADING = 0x00000200
IDENTITY_PATTERN = re.compile(r"^\((?P<type>[^:]+):(?P<instance>[0-9A-Fa-f]+)\)$")

SCALAR_FIELDS = (
    "runtime_identity_type",
    "runtime_identity_instance",
    "runtime_playfield",
    "base_playfield_direct",
    "district_id_direct",
    "cell_id_direct",
    "full_model_type_direct",
    "full_model_instance_direct",
    "monster_data",
    "template_id_direct",
    "heading",
    "orientation",
    "head_mesh",
    "textures",
    "meshes",
    "visual_flags",
    "breed",
    "gender",
    "profession",
    "level",
    "packet_scfu_heading",
    "packet_scfu_level",
    "packet_scfu_breed_derived",
    "packet_scfu_gender_derived",
    "epoch_scoped_identity_key",
    "lifecycle_lineage",
    "client_object_pointer_diagnostic",
    "owner",
    "client_visible_stats",
    "packet_stat_observations",
)
POSITION_SPACES = ("world", "local", "district", "cell", "packet_scfu")


class ReplayError(RuntimeError):
    pass


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture_folder", type=Path)
    parser.add_argument("--live-jsonl", type=Path)
    parser.add_argument("--scfu-csv", type=Path)
    parser.add_argument("--stat-csv", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--check", action="store_true")
    return parser.parse_args(argv)


def canonical_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def first(mapping: Mapping[str, Any], *names: str, default: Any = None) -> Any:
    for name in names:
        if name in mapping:
            return mapping[name]
    return default


def require_int(value: Any, label: str, *, minimum: int | None = None) -> int:
    if isinstance(value, bool):
        raise ReplayError(f"{label} must be an integer")
    try:
        result = int(value)
    except (TypeError, ValueError) as exc:
        raise ReplayError(f"{label} must be an integer") from exc
    if str(value).strip() != str(result) and not isinstance(value, int):
        raise ReplayError(f"{label} must be an exact integer")
    if minimum is not None and result < minimum:
        raise ReplayError(f"{label} must be at least {minimum}")
    return result


def optional_int(value: Any, label: str) -> int | None:
    if value is None or str(value).strip() == "":
        return None
    return require_int(value, label)


def missing_wrapper() -> dict[str, Any]:
    return {"value": None, "classification": "not-observed", "provenance": []}


def wrapper(
    value: Any,
    classification: str,
    provenance: Iterable[Mapping[str, Any]] | None = None,
) -> dict[str, Any]:
    if classification not in EVIDENCE_CLASSIFICATIONS:
        raise ReplayError(f"unsupported evidence classification: {classification}")
    normalized_provenance = [dict(item) for item in (provenance or [])]
    normalized_provenance.sort(key=canonical_bytes)
    if value == UNSET_SENTINEL or classification == "sentinel/default":
        value = None
        classification = "sentinel/default"
    return {
        "value": value,
        "classification": classification,
        "provenance": normalized_provenance,
    }


def normalize_wrapper(
    value: Any,
    label: str,
    fallback_provenance: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    if value is None:
        return missing_wrapper()
    if not isinstance(value, Mapping):
        raise ReplayError(f"{label} must use a value/classification/provenance wrapper")
    classification = str(value.get("classification", "")).strip()
    if not classification and ("state" in value or "provenance" in value):
        live_provenance = str(value.get("provenance", "")).strip()
        state = str(value.get("state", "")).strip()
        if live_provenance in EVIDENCE_CLASSIFICATIONS:
            classification = live_provenance
        elif state in {"not-observed", "missing"}:
            classification = "not-observed"
        elif state in {"observed", "partial"}:
            classification = "client-state-observed"
    if classification not in EVIDENCE_CLASSIFICATIONS:
        raise ReplayError(f"{label} has unsupported classification {classification!r}")
    provenance = value.get("provenance", [])
    if isinstance(provenance, str):
        detail = dict(fallback_provenance or {})
        detail["classification"] = provenance
        if value.get("source") is not None:
            detail["detail"] = value.get("source")
        if value.get("reason") is not None:
            detail["reason"] = value.get("reason")
        provenance = [detail]
    if provenance is None:
        provenance = []
    if isinstance(provenance, Mapping):
        provenance = [provenance]
    if not isinstance(provenance, list) or not all(isinstance(item, Mapping) for item in provenance):
        raise ReplayError(f"{label}.provenance must be an array of objects")
    return wrapper(value.get("value"), classification, provenance)


def live_field_wrapper(
    value: Any,
    label: str,
    provenance: Mapping[str, Any],
    *,
    scalar_classification: str = "client-state-observed",
) -> dict[str, Any]:
    if value is None:
        return missing_wrapper()
    if isinstance(value, Mapping) and ("classification" in value or "state" in value):
        return normalize_wrapper(value, label, provenance)
    return wrapper(value, scalar_classification, [provenance])


def identity_instance(value: Any) -> Any:
    if isinstance(value, Mapping):
        if "value" in value:
            return identity_instance(value.get("value"))
        return value.get("instance")
    return value


def parse_formatted_identity(value: str, label: str) -> dict[str, int]:
    match = IDENTITY_PATTERN.fullmatch(value.strip())
    if match is None:
        raise ReplayError(f"{label}: malformed formatted identity {value!r}")
    type_text = match.group("type").strip()
    known_types = {"None": 0, "SimpleChar": 0xC350}
    if type_text in known_types:
        identity_type = known_types[type_text]
    else:
        try:
            identity_type = int(type_text, 10)
        except ValueError:
            try:
                identity_type = int(type_text, 16)
            except ValueError as exc:
                raise ReplayError(f"{label}: malformed identity type {type_text!r}") from exc
    return {
        "type": identity_type,
        "instance": int(match.group("instance"), 16),
    }


def transform_wrapper_value(value: dict[str, Any], transform) -> dict[str, Any]:
    result = dict(value)
    if result["value"] is not None:
        result["value"] = transform(result["value"])
    return result


def normalize_client_stats(
    value: Any,
    label: str,
    provenance: Mapping[str, Any],
) -> dict[str, Any]:
    if value is None:
        return missing_wrapper()
    if isinstance(value, Mapping) and "classification" in value:
        return normalize_wrapper(value, label, provenance)
    if not isinstance(value, list):
        raise ReplayError(f"{label} must be an array")
    retained = []
    sentinel_seen = False
    for index, item in enumerate(value):
        if not isinstance(item, Mapping):
            raise ReplayError(f"{label}[{index}] must be an object")
        classification = str(item.get("provenance", "client-state-observed"))
        raw_value = item.get("raw_value", item.get("value"))
        if raw_value == UNSET_SENTINEL or classification == "sentinel/default":
            sentinel_seen = True
            continue
        if item.get("value") is None or classification == "not-observed":
            continue
        retained.append(
            {
                "stat_id": require_int(item.get("stat_id"), f"{label}[{index}].stat_id"),
                "value": require_int(item.get("value"), f"{label}[{index}].value"),
            }
        )
    retained.sort(key=lambda item: (item["stat_id"], item["value"]))
    classification = "sentinel/default" if sentinel_seen and not retained else "client-state-observed"
    return wrapper(retained if classification != "sentinel/default" else None, classification, [provenance])


def record_provenance(path: Path, line_number: int, record_type: str) -> dict[str, Any]:
    return {
        "source": path.name,
        "line": line_number,
        "record_type": record_type,
    }


def load_jsonl(path: Path) -> list[tuple[int, dict[str, Any]]]:
    if not path.is_file():
        raise ReplayError(f"missing live bridge JSONL: {path}")
    records: list[tuple[int, dict[str, Any]]] = []
    with path.open("r", encoding="utf-8-sig") as stream:
        for line_number, line in enumerate(stream, 1):
            if not line.strip():
                continue
            try:
                value = json.loads(line)
            except json.JSONDecodeError as exc:
                raise ReplayError(f"{path.name}:{line_number}: invalid JSON: {exc.msg}") from exc
            if not isinstance(value, dict):
                raise ReplayError(f"{path.name}:{line_number}: record must be an object")
            version = first(value, "schema_version", "schemaVersion", default=SCHEMA_VERSION)
            if require_int(version, f"{path.name}:{line_number}.schema_version") != SCHEMA_VERSION:
                raise ReplayError(f"{path.name}:{line_number}: unsupported schema version")
            records.append((line_number, value))
    if not records:
        raise ReplayError(f"live bridge JSONL is empty: {path}")
    return records


def record_type(record: Mapping[str, Any]) -> str:
    value = str(first(record, "record_type", "recordType", "type", default="")).strip()
    aliases = {
        "epoch": "zone_epoch",
        "zone-epoch": "zone_epoch",
        "snapshot": "npc_snapshot",
        "npc-snapshot": "npc_snapshot",
        "capture_header": "capture",
    }
    return aliases.get(value, value)


def normalize_epoch_field(
    record: Mapping[str, Any],
    name: str,
    aliases: tuple[str, ...],
    provenance: Mapping[str, Any],
) -> dict[str, Any]:
    value = first(record, name, *aliases)
    if value is None:
        return missing_wrapper()
    if isinstance(value, Mapping) and "classification" in value:
        return normalize_wrapper(value, "epoch." + name)
    return wrapper(value, "client-state-observed", [provenance])


def load_live_contract(
    path: Path,
) -> tuple[str, list[dict[str, Any]], list[dict[str, Any]], list[dict[str, Any]]]:
    capture_ids: set[str] = set()
    raw_epochs: list[dict[str, Any]] = []
    snapshots: list[dict[str, Any]] = []
    packet_records: list[dict[str, Any]] = []
    for line_number, record in load_jsonl(path):
        kind = record_type(record)
        capture_id = str(first(record, "capture_id", "captureId", default="")).strip()
        if capture_id:
            capture_ids.add(capture_id)
        if kind == "capture":
            continue
        if not capture_id:
            raise ReplayError(f"{path.name}:{line_number}: capture_id is required")
        provenance = record_provenance(path, line_number, kind)
        if kind == "zone_epoch":
            zone_epoch_id = str(first(record, "zone_epoch_id", "zoneEpochId", default="")).strip()
            if not zone_epoch_id:
                raise ReplayError(f"{path.name}:{line_number}: zone_epoch_id is required")
            start = require_int(
                first(record, "start_global_ordinal", "startGlobalOrdinal"),
                f"{path.name}:{line_number}.start_global_ordinal",
                minimum=0,
            )
            end = optional_int(
                first(record, "end_global_ordinal", "endGlobalOrdinal"),
                f"{path.name}:{line_number}.end_global_ordinal",
            )
            if end is not None and end < start:
                raise ReplayError(f"{path.name}:{line_number}: epoch ends before it starts")
            runtime_identity = first(
                record,
                "runtime_playfield",
                "runtimePlayfield",
                "runtime_playfield_identity",
            )
            model_identity = first(
                record,
                "base_playfield_direct",
                "basePlayfieldDirect",
                "model_playfield_identity",
            )
            runtime_value = first(
                record,
                "runtime_playfield_id",
                "runtimePlayfieldId",
                "runtime_playfield_id_hint",
                default=None,
            )
            if runtime_value is None:
                runtime_value = identity_instance(runtime_identity)
            base_value = first(
                record,
                "base_playfield_id_if_proven",
                "basePlayfieldIdIfProven",
                default=None,
            )
            if base_value is None:
                base_value = identity_instance(model_identity)
            live_validity = first(record, "validity", default=None)
            reported_valid = (
                str(live_validity) == "valid"
                if live_validity is not None
                else record.get("valid") is True
            )
            raw_epochs.append(
                {
                    "zone_epoch_id": zone_epoch_id,
                    "start_global_ordinal": start,
                    "end_global_ordinal": end,
                    "trigger": str(record.get("trigger", "recorded-boundary")),
                    "runtime_playfield": live_field_wrapper(
                        runtime_value, "epoch.runtime_playfield", provenance
                    ),
                    "base_playfield_direct": live_field_wrapper(
                        base_value, "epoch.base_playfield_direct", provenance
                    ),
                    "district_id_direct": normalize_epoch_field(
                        record, "district_id_direct", ("districtIdDirect",), provenance
                    ),
                    "cell_id_direct": normalize_epoch_field(
                        record, "cell_id_direct", ("cellIdDirect",), provenance
                    ),
                    "valid": reported_valid and end is not None,
                    "_line": line_number,
                }
            )
        elif kind == "npc_snapshot":
            snapshots.append({"record": record, "line": line_number, "provenance": provenance})
        elif kind in {"packet_scfu", "packet_stat", "packet_event"}:
            packet_records.append(
                {"record": record, "line": line_number, "provenance": provenance, "kind": kind}
            )
        else:
            raise ReplayError(f"{path.name}:{line_number}: unsupported record_type {kind!r}")
    if len(capture_ids) != 1:
        raise ReplayError("live bridge JSONL must contain exactly one non-empty capture_id")
    if not raw_epochs:
        raise ReplayError("live bridge JSONL contains no zone_epoch records")

    raw_epochs.sort(key=lambda item: (item["start_global_ordinal"], item["zone_epoch_id"]))
    ids: set[str] = set()
    for index, epoch in enumerate(raw_epochs):
        if epoch["zone_epoch_id"] in ids:
            raise ReplayError(f"duplicate zone_epoch_id: {epoch['zone_epoch_id']}")
        ids.add(epoch["zone_epoch_id"])
        if index:
            previous = raw_epochs[index - 1]
            if epoch["start_global_ordinal"] <= previous["start_global_ordinal"]:
                raise ReplayError(
                    f"epoch starts are not strictly increasing: {previous['zone_epoch_id']} and {epoch['zone_epoch_id']}"
                )
            previous_end = previous["end_global_ordinal"]
            if previous_end is None:
                previous["end_global_ordinal"] = epoch["start_global_ordinal"] - 1
                previous_end = previous["end_global_ordinal"]
            if previous_end >= epoch["start_global_ordinal"]:
                raise ReplayError(
                    f"overlapping epoch ordinal ranges: {previous['zone_epoch_id']} and {epoch['zone_epoch_id']}"
                )
    for epoch in raw_epochs:
        epoch.pop("_line", None)
    return next(iter(capture_ids)), raw_epochs, snapshots, packet_records


def epoch_for_ordinal(epochs: Iterable[Mapping[str, Any]], ordinal: int) -> str | None:
    matches = []
    for epoch in epochs:
        start = int(epoch["start_global_ordinal"])
        end = epoch.get("end_global_ordinal")
        if ordinal >= start and (end is None or ordinal <= int(end)):
            matches.append(str(epoch["zone_epoch_id"]))
    return matches[0] if len(matches) == 1 else None


def read_csv(path: Path, required: Iterable[str]) -> list[dict[str, str]]:
    if not path.is_file():
        raise ReplayError(f"missing analyzer output: {path}")
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        headers = set(reader.fieldnames or [])
        missing = [name for name in required if name not in headers]
        if missing:
            raise ReplayError(f"{path.name} is missing headers: {', '.join(missing)}")
        rows = []
        for row_number, row in enumerate(reader, 2):
            result = {key: value if value is not None else "" for key, value in row.items()}
            result["_row"] = str(row_number)
            rows.append(result)
        return rows


def packet_key(row: Mapping[str, str], path: Path) -> tuple[str, int, int]:
    row_number = row.get("_row", "?")
    direction = row.get("Direction", "").strip().upper()
    if direction not in {"IN", "OUT"}:
        raise ReplayError(f"{path.name}:{row_number}: invalid Direction")
    sequence = require_int(row.get("Sequence"), f"{path.name}:{row_number}.Sequence", minimum=1)
    ordinal = require_int(
        row.get("GlobalOrdinal"), f"{path.name}:{row_number}.GlobalOrdinal", minimum=1
    )
    return direction, sequence, ordinal


def packet_bytes(row: Mapping[str, str], path: Path) -> bytes:
    row_number = row.get("_row", "?")
    raw_hex = row.get("RawPacketHex", "").strip()
    try:
        packet = bytes.fromhex(raw_hex)
    except ValueError as exc:
        raise ReplayError(f"{path.name}:{row_number}: invalid RawPacketHex") from exc
    if len(packet) < 28:
        raise ReplayError(f"{path.name}:{row_number}: raw packet is too short for identity")
    return packet


def be_u32(packet: bytes, offset: int) -> int:
    return int.from_bytes(packet[offset : offset + 4], "big", signed=False)


def csv_int(row: Mapping[str, str], name: str) -> int | None:
    text = row.get(name, "").strip()
    return None if not text else int(text, 10)


def csv_float(row: Mapping[str, str], name: str) -> float | None:
    text = row.get(name, "").strip()
    return None if not text else float(text)


def split_retained(value: str) -> list[str]:
    return [] if not value.strip() else value.split("|")


def packet_provenance(
    path: Path,
    row: Mapping[str, str],
    kind: str,
    packet: bytes,
) -> dict[str, Any]:
    direction, sequence, ordinal = packet_key(row, path)
    result: dict[str, Any] = {
        "source": path.name,
        "row": int(row["_row"]),
        "kind": kind,
        "direction": direction,
        "sequence": sequence,
        "global_ordinal": ordinal,
        "raw_packet_sha256": sha256_bytes(packet),
    }
    if "DecodeStatus" in row:
        result["decode_status"] = row.get("DecodeStatus", "")
    return result


def sanitize_packet_scalar(
    value: Any,
    provenance: Mapping[str, Any],
    *,
    classification: str = "packet-observed",
) -> dict[str, Any]:
    if value is None:
        return wrapper(None, "not-observed", [provenance])
    if value == UNSET_SENTINEL:
        return wrapper(None, "sentinel/default", [provenance])
    return wrapper(value, classification, [provenance])


def scfu_fields(row: Mapping[str, str], path: Path) -> tuple[dict[str, Any], dict[str, Any]]:
    status = row.get("DecodeStatus", "").strip()
    fully_consumed = row.get("DecodeFullyConsumed", "").strip().lower()
    if status != "decoded_complete" or fully_consumed != "true":
        raise ReplayError(f"{path.name}:{row['_row']}: SCFU row is not completely decoded")
    packet = packet_bytes(row, path)
    provenance = packet_provenance(path, row, "scfu", packet)
    flags = csv_int(row, "FlagsNumeric") or 0
    appearance_value = csv_int(row, "AppearanceValue")
    if appearance_value is None:
        raise ReplayError(f"{path.name}:{row['_row']}: AppearanceValue is required")
    values: dict[str, Any] = {
        "runtime_identity_type": sanitize_packet_scalar(be_u32(packet, 20), provenance),
        "runtime_identity_instance": sanitize_packet_scalar(be_u32(packet, 24), provenance),
        "monster_data": sanitize_packet_scalar(csv_int(row, "MonsterData"), provenance),
        "visual_flags": sanitize_packet_scalar(csv_int(row, "VisualFlags"), provenance),
        "textures": sanitize_packet_scalar(split_retained(row.get("Textures", "")), provenance),
        "meshes": sanitize_packet_scalar(split_retained(row.get("Meshes", "")), provenance),
        "packet_scfu_level": sanitize_packet_scalar(csv_int(row, "Level"), provenance),
        "packet_scfu_breed_derived": sanitize_packet_scalar(
            (appearance_value & 255) >> 5, provenance, classification="derived"
        ),
        "packet_scfu_gender_derived": sanitize_packet_scalar(
            (appearance_value & 1023) >> 8, provenance, classification="derived"
        ),
    }
    if flags & SCFU_FLAG_HAS_PLAYFIELD:
        values["runtime_playfield"] = sanitize_packet_scalar(csv_int(row, "PlayfieldId"), provenance)
    if flags & SCFU_FLAG_HAS_HEAD_MESH:
        values["head_mesh"] = sanitize_packet_scalar(csv_int(row, "HeadMesh"), provenance)
    if flags & SCFU_FLAG_HAS_HEADING:
        values["packet_scfu_heading"] = sanitize_packet_scalar(
            {
                "x": csv_float(row, "HeadingX"),
                "y": csv_float(row, "HeadingY"),
                "z": csv_float(row, "HeadingZ"),
                "w": csv_float(row, "HeadingW"),
            },
            provenance,
        )
    owner = row.get("Owner", "").strip()
    if owner:
        values["owner"] = sanitize_packet_scalar(
            parse_formatted_identity(owner, f"{path.name}:{row['_row']}.Owner"),
            provenance,
        )
    values["positions.packet_scfu"] = sanitize_packet_scalar(
        {
            "x": csv_float(row, "PositionX"),
            "y": csv_float(row, "PositionY"),
            "z": csv_float(row, "PositionZ"),
        },
        provenance,
    )
    return values, provenance


def stat_fields(
    rows: Iterable[Mapping[str, str]], path: Path
) -> tuple[dict[str, Any], dict[str, Any], bool]:
    materialized = list(rows)
    if not materialized:
        raise ReplayError("internal error: empty Stat row group")
    first_row = materialized[0]
    packet = packet_bytes(first_row, path)
    provenance = packet_provenance(path, first_row, "stat", packet)
    identity_type = be_u32(packet, 20)
    identity_instance = be_u32(packet, 24)
    stats = []
    sentinel_seen = False
    for row in materialized:
        other_packet = packet_bytes(row, path)
        if other_packet != packet:
            raise ReplayError(f"{path.name}: Stat rows for one packet contain conflicting bytes")
        if row.get("DecodeStatus", "").strip() != "decoded_complete" or row.get(
            "DecodeFullyConsumed", ""
        ).strip().lower() != "true":
            raise ReplayError(f"{path.name}:{row['_row']}: Stat row is not completely decoded")
        stat_id = csv_int(row, "StatId")
        value = csv_int(row, "Value")
        if stat_id is None:
            continue
        if value == UNSET_SENTINEL:
            sentinel_seen = True
            continue
        stats.append({"stat_id": stat_id, "value": value})
    stats.sort(key=lambda item: (item["stat_id"], item["value"] if item["value"] is not None else -1))
    values = {
        "runtime_identity_type": sanitize_packet_scalar(identity_type, provenance),
        "runtime_identity_instance": sanitize_packet_scalar(identity_instance, provenance),
        "packet_stat_observations": wrapper(stats, "packet-observed", [provenance]),
    }
    return values, provenance, sentinel_seen


def build_packet_indexes(
    scfu_path: Path, stat_path: Path
) -> tuple[dict[tuple[str, int, int], dict[str, str]], dict[tuple[str, int, int], list[dict[str, str]]]]:
    common = ("Direction", "Sequence", "GlobalOrdinal", "RawPacketHex", "DecodeStatus")
    scfu_rows = read_csv(
        scfu_path,
        common
        + (
            "DecodeFullyConsumed",
            "FlagsNumeric",
            "PlayfieldId",
            "PositionX",
            "PositionY",
            "PositionZ",
            "HeadingX",
            "HeadingY",
            "HeadingZ",
            "HeadingW",
            "MonsterData",
            "VisualFlags",
            "HeadMesh",
            "Textures",
            "Meshes",
            "Breed",
            "Gender",
            "Level",
            "Owner",
            "AppearanceValue",
        ),
    )
    stat_rows = read_csv(
        stat_path,
        common + ("DecodeFullyConsumed", "StatId", "Value"),
    )
    scfu: dict[tuple[str, int, int], dict[str, str]] = {}
    for row in scfu_rows:
        key = packet_key(row, scfu_path)
        if key in scfu:
            raise ReplayError(f"{scfu_path.name}: duplicate packet key {key}")
        scfu[key] = row
    stats: dict[tuple[str, int, int], list[dict[str, str]]] = {}
    for row in stat_rows:
        stats.setdefault(packet_key(row, stat_path), []).append(row)
    return scfu, stats


def live_scfu_values(record: Mapping[str, Any]) -> dict[str, Any]:
    values: dict[str, Any] = {
        "runtime_identity_type": record.get("runtime_identity_type"),
        "runtime_identity_instance": record.get("runtime_identity_instance"),
        "runtime_playfield": record.get("runtime_playfield_id"),
        "positions.packet_scfu": record.get("position"),
        "packet_scfu_heading": record.get("heading"),
        "monster_data": record.get("monster_data"),
        "head_mesh": record.get("head_mesh"),
        "textures": split_retained(str(record.get("textures", ""))),
        "meshes": split_retained(str(record.get("meshes", ""))),
        "visual_flags": record.get("visual_flags"),
        "packet_scfu_level": record.get("level"),
        "packet_scfu_breed_derived": record.get("breed"),
        "packet_scfu_gender_derived": record.get("gender"),
    }
    if record.get("owner") is not None:
        values["owner"] = record.get("owner")
    return {name: value for name, value in values.items() if value is not None}


def live_stat_values(record: Mapping[str, Any]) -> dict[str, Any]:
    stats = []
    for index, item in enumerate(record.get("stats") or []):
        if not isinstance(item, Mapping):
            raise ReplayError(f"packet_stat.stats[{index}] must be an object")
        if item.get("provenance") == "sentinel/default" or item.get("raw_value") == UNSET_SENTINEL:
            continue
        if item.get("value") is None:
            continue
        stats.append(
            {
                "stat_id": require_int(item.get("stat_id"), f"packet_stat.stats[{index}].stat_id"),
                "value": require_int(item.get("value"), f"packet_stat.stats[{index}].value"),
            }
        )
    stats.sort(key=lambda item: (item["stat_id"], item["value"]))
    return {
        "runtime_identity_type": record.get("runtime_identity_type"),
        "runtime_identity_instance": record.get("runtime_identity_instance"),
        "packet_stat_observations": stats,
    }


def validate_live_packet_records(
    packet_records: Iterable[Mapping[str, Any]],
    epochs: list[dict[str, Any]],
    scfu_index: Mapping[tuple[str, int, int], Mapping[str, str]],
    stat_index: Mapping[tuple[str, int, int], list[Mapping[str, str]]],
    scfu_path: Path,
    stat_path: Path,
) -> tuple[
    dict[tuple[str, str, int, int], dict[str, Any]],
    dict[tuple[str, str, int, int], set[str]],
    list[dict[str, Any]],
    set[tuple[str, str, int, int]],
]:
    record_index: dict[tuple[str, str, int, int], dict[str, Any]] = {}
    validated_paths: dict[tuple[str, str, int, int], set[str]] = {}
    conflicts: list[dict[str, Any]] = []
    invalid_keys: set[tuple[str, str, int, int]] = set()
    epoch_valid = {str(epoch["zone_epoch_id"]): bool(epoch["valid"]) for epoch in epochs}
    for item in packet_records:
        record = item["record"]
        kind = item["kind"]
        reference = normalize_packet_reference(
            {**record, "kind": kind.removeprefix("packet_")},
            f"live packet line {item['line']}",
        )
        key = (reference["kind"], reference["direction"], reference["sequence"], reference["global_ordinal"])
        assigned_epoch = epoch_for_ordinal(epochs, reference["global_ordinal"])
        claimed_epoch_value = record.get("zone_epoch_id")
        claimed_epoch = None if claimed_epoch_value is None else str(claimed_epoch_value)
        record_valid = record.get("zone_epoch_valid") is True
        expected_valid = bool(assigned_epoch is not None and epoch_valid.get(assigned_epoch, False))
        explicitly_unassigned_transition = (
            "zone_epoch_id" in record
            and record.get("zone_epoch_id") is None
            and "zone_epoch_valid" in record
            and record.get("zone_epoch_valid") is False
        )
        if explicitly_unassigned_transition:
            invalid_keys.add(key)
        elif assigned_epoch != claimed_epoch or record_valid != expected_valid:
            conflicts.append(
                {
                    "reason": "live-packet-epoch-assignment-conflict",
                    "kind": reference["kind"],
                    "direction": reference["direction"],
                    "sequence": reference["sequence"],
                    "global_ordinal": reference["global_ordinal"],
                    "claimed_epoch": claimed_epoch,
                    "assigned_epoch": assigned_epoch,
                }
            )
            invalid_keys.add(key)
        if key in record_index:
            conflicts.append(
                {
                    "reason": "duplicate-live-packet-record",
                    "kind": reference["kind"],
                    "direction": reference["direction"],
                    "sequence": reference["sequence"],
                    "global_ordinal": reference["global_ordinal"],
                }
            )
            invalid_keys.add(key)
            continue
        record_index[key] = dict(record)
        if kind == "packet_event":
            validated_paths[key] = set()
            continue
        decode_error = str(record.get("decode_error") or "").strip()
        decode_fully_consumed = record.get("decode_fully_consumed") is True
        if decode_error or not decode_fully_consumed:
            invalid_keys.add(key)
            validated_paths[key] = set()
            continue
        analyzer_key = (reference["direction"], reference["sequence"], reference["global_ordinal"])
        if kind == "packet_scfu":
            analyzer_row = scfu_index.get(analyzer_key)
            if analyzer_row is None:
                conflicts.append({"reason": "live-packet-scfu-missing-offline-row", "key": list(key)})
                invalid_keys.add(key)
                continue
            offline, _ = scfu_fields(analyzer_row, scfu_path)
            live = live_scfu_values(record)
        else:
            analyzer_rows = stat_index.get(analyzer_key)
            if analyzer_rows is None:
                conflicts.append({"reason": "live-packet-stat-missing-offline-row", "key": list(key)})
                invalid_keys.add(key)
                continue
            offline, _, _ = stat_fields(analyzer_rows, stat_path)
            live = live_stat_values(record)
        paths = set()
        for path, live_value in sorted(live.items()):
            offline_wrapper = offline.get(path)
            sentinel_match = bool(
                offline_wrapper is not None
                and live_value == UNSET_SENTINEL
                and offline_wrapper["classification"] == "sentinel/default"
            )
            if offline_wrapper is None or not (
                sentinel_match or values_equal(live_value, offline_wrapper["value"])
            ):
                conflicts.append(
                    {
                        "reason": "live-packet-offline-projection-conflict",
                        "key": list(key),
                        "field": path,
                        "live_value": live_value,
                        "offline_value": None if offline_wrapper is None else offline_wrapper["value"],
                    }
                )
                invalid_keys.add(key)
            elif offline_wrapper["classification"] == "packet-observed":
                paths.add(path)
        validated_paths[key] = paths
    conflicts.sort(key=canonical_bytes)
    return record_index, validated_paths, conflicts, invalid_keys


def normalize_packet_reference(value: Mapping[str, Any], label: str) -> dict[str, Any]:
    direction = str(first(value, "direction", "Direction", default="")).strip().upper()
    if direction not in {"IN", "OUT"}:
        raise ReplayError(f"{label}.direction must be IN or OUT")
    sequence = require_int(first(value, "sequence", "Sequence"), label + ".sequence", minimum=1)
    ordinal = require_int(
        first(value, "global_ordinal", "globalOrdinal", "GlobalOrdinal"),
        label + ".global_ordinal",
        minimum=1,
    )
    kind = str(first(value, "kind", "message_type", "messageType", "source", default="")).strip().lower()
    aliases = {
        "simplecharfullupdate": "scfu",
        "simple-char-full-update": "scfu",
        "statmessage": "stat",
    }
    kind = aliases.get(kind, kind)
    result = dict(value)
    result.update(
        {
            "kind": kind,
            "direction": direction,
            "sequence": sequence,
            "global_ordinal": ordinal,
        }
    )
    return result


def values_equal(left: Any, right: Any) -> bool:
    if isinstance(left, bool) or isinstance(right, bool):
        return left is right
    if isinstance(left, (int, float)) and isinstance(right, (int, float)):
        return float(left) == float(right)
    if isinstance(left, Mapping) and isinstance(right, Mapping):
        return set(left) == set(right) and all(values_equal(left[key], right[key]) for key in left)
    if isinstance(left, list) and isinstance(right, list):
        return len(left) == len(right) and all(values_equal(a, b) for a, b in zip(left, right))
    return left == right


def field_at(observation: Mapping[str, Any], path: str) -> dict[str, Any]:
    if path.startswith("positions."):
        return observation["positions"][path.split(".", 1)[1]]
    return observation[path]


def set_field(observation: dict[str, Any], path: str, value: dict[str, Any]) -> None:
    if path.startswith("positions."):
        observation["positions"][path.split(".", 1)[1]] = value
    else:
        observation[path] = value


def normalize_snapshot(
    capture_id: str,
    item: Mapping[str, Any],
    epochs: list[dict[str, Any]],
) -> dict[str, Any]:
    record = item["record"]
    line_number = item["line"]
    label = f"snapshot line {line_number}"
    record_capture_id = str(first(record, "capture_id", "captureId", default="")).strip()
    if record_capture_id != capture_id:
        raise ReplayError(f"{label}: capture_id does not match the capture")
    epoch_id = str(first(record, "zone_epoch_id", "zoneEpochId", default="")).strip()
    if not epoch_id:
        raise ReplayError(f"{label}: zone_epoch_id is required")
    observation_sequence = require_int(
        first(record, "observation_sequence", "observationSequence"),
        label + ".observation_sequence",
        minimum=1,
    )
    observation_ordinal = require_int(
        first(record, "observation_global_ordinal", "observationGlobalOrdinal"),
        label + ".observation_global_ordinal",
        minimum=0,
    )
    fields = record.get("fields") if isinstance(record.get("fields"), Mapping) else record
    positions_input = first(record, "positions", "position_spaces", "positionSpaces", default={})
    if not isinstance(positions_input, Mapping):
        raise ReplayError(f"{label}.positions must be an object")
    observation: dict[str, Any] = {
        "observation_id": str(
            first(
                record,
                "observation_id",
                "observationId",
                default=f"{capture_id}|{epoch_id}|{observation_sequence:08d}",
            )
        ),
        "capture_id": capture_id,
        "zone_epoch_id": epoch_id,
        "observation_sequence": observation_sequence,
        "observation_global_ordinal": observation_ordinal,
        "timestamp": str(first(record, "timestamp", "captured_utc", "capturedUtc", default="")),
        "positions": {},
        "packet_provenance": [],
        "client_state_provenance": [],
        "bridge_state": "partial",
        "bridge_blockers": [],
        "coordinate_relation": {"state": "not-proven"},
        "acg_hash_used_as_runtime_identity": False,
    }
    live_provenance = item["provenance"]
    for name in SCALAR_FIELDS:
        if name == "runtime_playfield":
            raw = first(fields, "runtime_playfield_id", "runtimePlayfieldId")
            if raw is None:
                raw_identity = first(fields, "runtime_playfield", "runtimePlayfield")
                raw = identity_instance(raw_identity)
        elif name == "base_playfield_direct":
            raw = first(fields, "base_playfield_id_if_proven", "basePlayfieldIdIfProven")
            if raw is None:
                raw = identity_instance(first(fields, "base_playfield_direct", "basePlayfieldDirect"))
        elif name in {"epoch_scoped_identity_key", "lifecycle_lineage"}:
            raw = fields.get(name)
            if raw is None:
                observation[name] = missing_wrapper()
            else:
                normalized = live_field_wrapper(raw, f"{label}.{name}", live_provenance)
                observation[name] = wrapper(
                    normalized["value"],
                    "derived" if normalized["value"] is not None else normalized["classification"],
                    normalized["provenance"],
                )
            continue
        elif name == "client_visible_stats":
            observation[name] = normalize_client_stats(
                fields.get(name), f"{label}.{name}", live_provenance
            )
            continue
        elif name == "packet_stat_observations":
            observation[name] = missing_wrapper()
            continue
        else:
            raw = fields.get(name)
        observation[name] = live_field_wrapper(
            raw,
            f"{label}.{name}",
            live_provenance,
        )
        if name in {"textures", "meshes"}:
            observation[name] = transform_wrapper_value(
                observation[name],
                lambda value: split_retained(value) if isinstance(value, str) else value,
            )
    runtime_identity = first(record, "npc_runtime_identity", "npcRuntimeIdentity")
    if isinstance(runtime_identity, Mapping):
        identity_value = runtime_identity.get("value") if "value" in runtime_identity else runtime_identity
        if isinstance(identity_value, Mapping):
            if observation["runtime_identity_type"]["classification"] == "not-observed":
                observation["runtime_identity_type"] = wrapper(
                    identity_value.get("type"), "client-state-observed", [live_provenance]
                )
            if observation["runtime_identity_instance"]["classification"] == "not-observed":
                observation["runtime_identity_instance"] = wrapper(
                    identity_value.get("instance"), "client-state-observed", [live_provenance]
                )
    owner_raw = fields.get("owner")
    if isinstance(owner_raw, Mapping) and "value" not in owner_raw and (
        "type" in owner_raw or "instance" in owner_raw
    ):
        owner_value = {
            "type": owner_raw.get("type"),
            "instance": owner_raw.get("instance"),
        }
        owner_copy = dict(owner_raw)
        owner_copy["value"] = owner_value
        observation["owner"] = normalize_wrapper(owner_copy, f"{label}.owner", live_provenance)
    for space in POSITION_SPACES:
        observation["positions"][space] = normalize_wrapper(
            positions_input.get(space), f"{label}.positions.{space}", live_provenance
        )
    for provenance_name in ("packet_provenance", "client_state_provenance"):
        raw = first(record, provenance_name, "".join(
            [provenance_name.split("_")[0], "StateProvenance" if provenance_name.startswith("client") else "Provenance"]
        ), default=[])
        if raw is None:
            raw = []
        if isinstance(raw, Mapping):
            raw = [raw]
        if not isinstance(raw, list) or not all(isinstance(entry, Mapping) for entry in raw):
            raise ReplayError(f"{label}.{provenance_name} must be an array of objects")
        observation[provenance_name] = [dict(entry) for entry in raw]
        observation[provenance_name].sort(key=canonical_bytes)
    claimed_epoch = next(
        (epoch for epoch in epochs if str(epoch["zone_epoch_id"]) == epoch_id),
        None,
    )
    input_epoch_valid_present = "zone_epoch_valid" in record or "zoneEpochValid" in record
    observation["_snapshot_epoch_validity"] = {
        "ordinal_in_claimed_epoch": epoch_for_ordinal(epochs, observation_ordinal) == epoch_id,
        "finalized_epoch_valid": bool(
            claimed_epoch is not None
            and claimed_epoch["valid"]
            and claimed_epoch["end_global_ordinal"] is not None
        ),
        "input_epoch_valid_present": input_epoch_valid_present,
        "input_epoch_valid": first(record, "zone_epoch_valid", "zoneEpochValid", default=None),
    }
    raw_bridge_blockers = first(record, "bridge_blockers", "bridgeBlockers", default=[])
    blockers_valid = isinstance(raw_bridge_blockers, list) and all(
        isinstance(blocker, str) and bool(blocker.strip()) for blocker in raw_bridge_blockers
    )
    observation["_input_bridge_blockers"] = list(raw_bridge_blockers) if blockers_valid else []
    observation["_input_bridge_blockers_malformed"] = not blockers_valid
    observation["_input_bridge_state"] = str(
        first(record, "bridge_state", "bridgeState", default="partial")
    )
    observation["_input_acg_identity"] = bool(
        first(record, "acg_hash_used_as_runtime_identity", "acgHashUsedAsRuntimeIdentity", default=False)
    )
    return observation


def replay_observation(
    observation: dict[str, Any],
    epochs: list[dict[str, Any]],
    scfu_index: Mapping[tuple[str, int, int], Mapping[str, str]],
    stat_index: Mapping[tuple[str, int, int], list[Mapping[str, str]]],
    scfu_path: Path,
    stat_path: Path,
    live_packet_records: Mapping[tuple[str, str, int, int], Mapping[str, Any]],
    live_packet_paths: Mapping[tuple[str, str, int, int], set[str]],
    invalid_live_packet_keys: set[tuple[str, str, int, int]],
) -> tuple[list[str], list[str], list[str], list[dict[str, Any]]]:
    observation_id = observation["observation_id"]
    client_only_candidates = {
        path
        for path in list(SCALAR_FIELDS) + ["positions." + space for space in POSITION_SPACES]
        if field_at(observation, path)["classification"] == "client-state-observed"
    }
    live_packet_fields = {
        path
        for path in list(SCALAR_FIELDS) + ["positions." + space for space in POSITION_SPACES]
        if field_at(observation, path)["classification"] == "packet-observed"
    }
    offline_values: dict[str, dict[str, Any]] = {}
    decoded_provenance: list[dict[str, Any]] = []
    conflicts: list[dict[str, Any]] = []
    sentinel_seen = False

    input_bridge_blockers = observation.pop("_input_bridge_blockers")
    if observation.pop("_input_bridge_blockers_malformed"):
        conflicts.append({"observation_id": observation_id, "reason": "input-bridge-blockers-malformed"})
    epoch_validity = observation.pop("_snapshot_epoch_validity")
    if not epoch_validity["ordinal_in_claimed_epoch"]:
        conflicts.append({"observation_id": observation_id, "reason": "snapshot-ordinal-outside-claimed-epoch"})
    if not epoch_validity["finalized_epoch_valid"]:
        conflicts.append({"observation_id": observation_id, "reason": "snapshot-finalized-epoch-invalid"})
    if epoch_validity["input_epoch_valid_present"] and epoch_validity["input_epoch_valid"] is not True:
        conflicts.append({"observation_id": observation_id, "reason": "snapshot-input-zone-epoch-invalid"})
    input_bridge_state = observation.pop("_input_bridge_state").strip().lower().replace("_", "-")
    if input_bridge_state == "conflict":
        conflicts.append(
            {
                "observation_id": observation_id,
                "reason": "input-bridge-state-conflict",
                "input_bridge_state": input_bridge_state,
            }
        )
    elif input_bridge_state == "invalid-epoch":
        conflicts.append(
            {
                "observation_id": observation_id,
                "reason": "input-bridge-state-invalid-epoch",
                "input_bridge_state": input_bridge_state,
            }
        )
    elif input_bridge_state == "stale" or input_bridge_state.startswith("stale-"):
        conflicts.append(
            {
                "observation_id": observation_id,
                "reason": "input-bridge-state-stale-epoch",
                "input_bridge_state": input_bridge_state,
            }
        )
    if observation.pop("_input_acg_identity"):
        conflicts.append({"observation_id": observation_id, "reason": "acg-hash-cannot-be-runtime-identity"})

    references = []
    for index, raw_reference in enumerate(observation["packet_provenance"]):
        reference = normalize_packet_reference(raw_reference, f"{observation_id}.packet_provenance[{index}]")
        references.append(reference)
    references.sort(key=lambda item: (item["global_ordinal"], item["direction"], item["sequence"], item["kind"]))

    for reference in references:
        key = (reference["direction"], reference["sequence"], reference["global_ordinal"])
        live_key = (reference["kind"],) + key
        if live_key not in live_packet_records:
            conflicts.append(
                {
                    "observation_id": observation_id,
                    "reason": "referenced-live-packet-record-not-found",
                    "reference": reference,
                }
            )
        elif live_key in invalid_live_packet_keys:
            conflicts.append(
                {
                    "observation_id": observation_id,
                    "reason": "referenced-live-packet-record-invalid",
                    "reference": reference,
                }
            )
            continue
        else:
            live_packet_fields.update(live_packet_paths.get(live_key, set()))
        assigned_epoch = epoch_for_ordinal(epochs, reference["global_ordinal"])
        if assigned_epoch != observation["zone_epoch_id"]:
            conflicts.append(
                {
                    "observation_id": observation_id,
                    "reason": "packet-reference-outside-claimed-epoch",
                    "reference": reference,
                    "assigned_epoch": assigned_epoch,
                }
            )
            continue
        if reference["global_ordinal"] > observation["observation_global_ordinal"]:
            conflicts.append(
                {
                    "observation_id": observation_id,
                    "reason": "future-packet-reference",
                    "reference": reference,
                }
            )
            continue
        if reference["kind"] == "scfu":
            row = scfu_index.get(key)
            if row is None:
                conflicts.append(
                    {"observation_id": observation_id, "reason": "referenced-scfu-not-found", "reference": reference}
                )
                continue
            values, provenance = scfu_fields(row, scfu_path)
        elif reference["kind"] == "stat":
            rows = stat_index.get(key)
            if rows is None:
                conflicts.append(
                    {"observation_id": observation_id, "reason": "referenced-stat-not-found", "reference": reference}
                )
                continue
            values, provenance, stat_sentinel = stat_fields(rows, stat_path)
            sentinel_seen = sentinel_seen or stat_sentinel
        else:
            continue
        decoded_provenance.append(provenance)
        for path, value in values.items():
            existing = offline_values.get(path)
            if existing is not None and path in {"runtime_identity_type", "runtime_identity_instance"} and not values_equal(
                existing["value"], value["value"]
            ):
                conflicts.append(
                    {
                        "observation_id": observation_id,
                        "field": path,
                        "reason": "packet-identity-conflict-within-observation",
                        "first": existing["value"],
                        "second": value["value"],
                    }
                )
            offline_values[path] = value
            sentinel_seen = sentinel_seen or value["classification"] == "sentinel/default"

    offline_packet_fields = {
        path for path, value in offline_values.items() if value["classification"] == "packet-observed"
    }
    for path, offline in sorted(offline_values.items()):
        live = field_at(observation, path)
        if live["classification"] not in {"not-observed", "sentinel/default"} and not values_equal(
            live["value"], offline["value"]
        ):
            conflicts.append(
                {
                    "observation_id": observation_id,
                    "field": path,
                    "reason": "live-offline-value-conflict",
                    "live_value": live["value"],
                    "offline_value": offline["value"],
                }
            )
        if offline["classification"] != "sentinel/default" or live["classification"] in {
            "not-observed",
            "sentinel/default",
        }:
            set_field(observation, path, offline)

    observation["packet_provenance"] = decoded_provenance
    observation["packet_provenance"].sort(key=canonical_bytes)
    live_only = sorted(live_packet_fields - offline_packet_fields)
    offline_only = sorted(
        path
        for path in offline_packet_fields
        if path not in live_packet_fields and path not in client_only_candidates
    )
    client_only = sorted(client_only_candidates - set(offline_values))

    blockers = list(input_bridge_blockers)
    if conflicts:
        blockers.append("live/offline evidence conflict")
    if sentinel_seen or any(
        field_at(observation, path)["classification"] == "sentinel/default"
        for path in list(SCALAR_FIELDS) + ["positions." + space for space in POSITION_SPACES]
    ):
        blockers.append("sentinel/default evidence rejected")
    model_type = observation["full_model_type_direct"]
    model_instance = observation["full_model_instance_direct"]
    base_playfield = observation["base_playfield_direct"]
    direct_model = (
        model_type["classification"] in DIRECT_CLASSIFICATIONS
        and model_instance["classification"] in DIRECT_CLASSIFICATIONS
        and model_type["value"] == MODEL_RESOURCE_TYPE
        and model_instance["value"] is not None
    )
    direct_base = base_playfield["classification"] in DIRECT_CLASSIFICATIONS and base_playfield["value"] is not None
    runtime_type = observation["runtime_identity_type"]
    runtime_instance = observation["runtime_identity_instance"]
    direct_runtime = (
        runtime_type["classification"] in DIRECT_CLASSIFICATIONS
        and runtime_instance["classification"] in DIRECT_CLASSIFICATIONS
        and runtime_type["value"] is not None
        and runtime_instance["value"] is not None
    )
    if direct_runtime and runtime_type["value"] == 0xC350:
        harvested_instance = require_int(
            runtime_instance["value"],
            observation_id + ".runtime_identity_instance",
            minimum=0,
        )
        observation["harvested_observation_id"] = (
            f"{observation['capture_id']}|(SimpleChar:{harvested_instance:04X})"
        )
    if not direct_runtime:
        blockers.append("runtime NPC identity not directly observed")
    if not direct_model:
        blockers.append("full model identity not directly observed")
    if not direct_base:
        blockers.append("base playfield identity not directly observed")
    if conflicts:
        stale = any("epoch" in conflict.get("reason", "") for conflict in conflicts)
        observation["bridge_state"] = "invalid-epoch" if stale else "conflict"
    elif direct_runtime and direct_model and direct_base:
        observation["bridge_state"] = "direct-candidate"
    elif model_type["classification"] == "not-observed" and model_instance["classification"] == "not-observed":
        observation["bridge_state"] = "not-exposed"
    else:
        observation["bridge_state"] = "partial"
    observation["bridge_blockers"] = sorted(set(blockers))
    return (
        [f"{observation_id}:{path}" for path in client_only],
        [f"{observation_id}:{path}" for path in live_only],
        [f"{observation_id}:{path}" for path in offline_only],
        conflicts,
    )


def attach_offline_packet_references(
    raw_snapshots: list[dict[str, Any]],
    raw_packet_records: list[dict[str, Any]],
    epochs: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    """Recover exact same-epoch identity links omitted by live enrichment.

    This only adds references to already preserved, fully decoded raw SCFU/Stat
    records. It never synthesizes client fields and never crosses a lineage
    evidence floor or zone epoch.
    """
    packet_candidates: dict[tuple[str, int, int, str], list[dict[str, Any]]] = {}
    for item in raw_packet_records:
        record = item["record"]
        kind = str(item.get("kind", ""))
        if kind not in {"packet_scfu", "packet_stat"}:
            continue
        if record.get("bridge_link_eligible") is not True:
            continue
        if str(record.get("decode_error", "")).strip():
            continue
        if record.get("decode_fully_consumed") is not True:
            continue
        epoch_id = str(record.get("zone_epoch_id", "")).strip()
        identity_type = record.get("runtime_identity_type")
        identity_instance = record.get("runtime_identity_instance")
        if not epoch_id or not isinstance(identity_type, int) or not isinstance(identity_instance, int):
            continue
        source_kind = "scfu" if kind == "packet_scfu" else "stat"
        packet_candidates.setdefault(
            (epoch_id, identity_type, identity_instance, source_kind), []
        ).append(record)
    for records in packet_candidates.values():
        records.sort(
            key=lambda record: (
                int(record.get("global_ordinal", -1)),
                str(record.get("direction", "")),
                int(record.get("sequence", -1)),
            )
        )

    recovered: list[dict[str, Any]] = []
    for item in raw_snapshots:
        snapshot_item = dict(item)
        record = dict(item["record"])
        snapshot_item["record"] = record
        epoch_id = str(record.get("zone_epoch_id", "")).strip()
        identity_type = record.get("runtime_identity_type")
        identity_instance = record.get("runtime_identity_instance")
        observation_ordinal = record.get("observation_global_ordinal")
        if (
            not epoch_id
            or not isinstance(identity_type, int)
            or not isinstance(identity_instance, int)
            or not isinstance(observation_ordinal, int)
        ):
            recovered.append(snapshot_item)
            continue
        evidence_start = record.get("evidence_window_start_global_ordinal")
        if not isinstance(evidence_start, int):
            # Older snapshots do not prove their current lineage floor. They
            # remain unchanged instead of receiving a speculative packet link.
            recovered.append(snapshot_item)
            continue
        references = record.get("packet_provenance", [])
        if not isinstance(references, list):
            recovered.append(snapshot_item)
            continue
        references = [dict(reference) for reference in references if isinstance(reference, Mapping)]
        linked_kinds = {str(reference.get("kind", "")) for reference in references}
        recovered_kinds: list[str] = []
        for source_kind in ("scfu", "stat"):
            if source_kind in linked_kinds:
                continue
            candidates = packet_candidates.get(
                (epoch_id, identity_type, identity_instance, source_kind), []
            )
            eligible = [
                candidate
                for candidate in candidates
                if evidence_start <= int(candidate["global_ordinal"]) <= observation_ordinal
            ]
            if not eligible:
                continue
            candidate = eligible[-1]
            references.append(
                {
                    "kind": source_kind,
                    "source": "SimpleCharFullUpdate" if source_kind == "scfu" else "Stat",
                    "direction": candidate["direction"],
                    "sequence": candidate["sequence"],
                    "global_ordinal": candidate["global_ordinal"],
                    "captured_utc": candidate.get("captured_utc", ""),
                }
            )
            recovered_kinds.append(source_kind)
        references.sort(
            key=lambda reference: (
                int(reference.get("global_ordinal", -1)),
                str(reference.get("direction", "")),
                int(reference.get("sequence", -1)),
                str(reference.get("kind", "")),
            )
        )
        record["packet_provenance"] = references
        if recovered_kinds:
            record["offline_recovered_packet_reference_kinds"] = recovered_kinds
        recovered.append(snapshot_item)
    return recovered


def build_artifact(
    live_path: Path,
    scfu_path: Path,
    stat_path: Path,
) -> dict[str, Any]:
    capture_id, epochs, raw_snapshots, raw_packet_records = load_live_contract(live_path)
    scfu_index, stat_index = build_packet_indexes(scfu_path, stat_path)
    live_packet_records, live_packet_paths, packet_record_conflicts, invalid_packet_keys = (
        validate_live_packet_records(
            raw_packet_records,
            epochs,
            scfu_index,
            stat_index,
            scfu_path,
            stat_path,
        )
    )
    raw_snapshots = attach_offline_packet_references(
        raw_snapshots,
        raw_packet_records,
        epochs,
    )
    observations = [normalize_snapshot(capture_id, item, epochs) for item in raw_snapshots]
    observations.sort(
        key=lambda item: (
            item["observation_global_ordinal"],
            item["observation_sequence"],
            item["observation_id"],
        )
    )
    client_only: list[str] = []
    live_only: list[str] = []
    offline_only: list[str] = []
    conflicts: list[dict[str, Any]] = list(packet_record_conflicts)
    for observation in observations:
        result = replay_observation(
            observation,
            epochs,
            scfu_index,
            stat_index,
            scfu_path,
            stat_path,
            live_packet_records,
            live_packet_paths,
            invalid_packet_keys,
        )
        client_only.extend(result[0])
        live_only.extend(result[1])
        offline_only.extend(result[2])
        conflicts.extend(result[3])
    client_only = sorted(set(client_only))
    live_only = sorted(set(live_only))
    offline_only = sorted(set(offline_only))
    conflicts.sort(key=canonical_bytes)
    source_files = [
        {"kind": "live-jsonl", "path": live_path.name, "sha256": sha256_file(live_path)},
        {"kind": "scfu-csv", "path": scfu_path.name, "sha256": sha256_file(scfu_path)},
        {"kind": "stat-csv", "path": stat_path.name, "sha256": sha256_file(stat_path)},
    ]
    source_files.sort(key=lambda item: (item["kind"], item["path"]))
    artifact: dict[str, Any] = {
        "schema_version": SCHEMA_VERSION,
        "capture_id": capture_id,
        "epochs": epochs,
        "observations": observations,
        "parity": {
            "packet_fields_match": not live_only and not offline_only and not conflicts,
            "client_state_only_fields": client_only,
            "live_only_fields": live_only,
            "offline_only_fields": offline_only,
            "conflicts": conflicts,
        },
        "source_files": source_files,
    }
    artifact["digest"] = sha256_bytes(canonical_bytes(artifact))
    return artifact


def write_artifact(path: Path, artifact: Mapping[str, Any], *, check: bool) -> None:
    encoded = (json.dumps(artifact, indent=2, sort_keys=True, ensure_ascii=False) + "\n").encode("utf-8")
    if check:
        if not path.is_file() or path.read_bytes() != encoded:
            raise ReplayError(f"deterministic artifact differs: {path}")
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    pending = path.with_name(path.name + ".pending")
    pending.write_bytes(encoded)
    pending.replace(path)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    capture = args.capture_folder.resolve()
    live_path = (args.live_jsonl or capture / DEFAULT_LIVE_JSONL).resolve()
    scfu_path = (args.scfu_csv or capture / DEFAULT_SCFU_CSV).resolve()
    stat_path = (args.stat_csv or capture / DEFAULT_STAT_CSV).resolve()
    output_path = (args.output or capture / DEFAULT_OUTPUT_JSON).resolve()
    try:
        source_paths = {live_path, scfu_path, stat_path}
        pending_path = output_path.with_name(output_path.name + ".pending").resolve()
        if output_path in source_paths or pending_path in source_paths:
            raise ReplayError(
                f"output or atomic pending path collides with an input source: {output_path}"
            )
        artifact = build_artifact(live_path, scfu_path, stat_path)
        write_artifact(output_path, artifact, check=args.check)
    except (OSError, UnicodeError, csv.Error, ReplayError, ValueError) as exc:
        print("NPC_IDENTITY_BRIDGE_REPLAY=FAIL", file=sys.stderr)
        print("ERROR=" + str(exc), file=sys.stderr)
        return 1
    parity = artifact["parity"]
    print("NPC_IDENTITY_BRIDGE_REPLAY=PASS")
    print("CAPTURE_ID=" + artifact["capture_id"])
    print("ZONE_EPOCHS=" + str(len(artifact["epochs"])))
    print("OBSERVATIONS=" + str(len(artifact["observations"])))
    print("PACKET_FIELDS_MATCH=" + ("YES" if parity["packet_fields_match"] else "NO"))
    print("CLIENT_STATE_ONLY_FIELDS=" + str(len(parity["client_state_only_fields"])))
    print("LIVE_ONLY_FIELDS=" + str(len(parity["live_only_fields"])))
    print("OFFLINE_ONLY_FIELDS=" + str(len(parity["offline_only_fields"])))
    print("CONFLICTS=" + str(len(parity["conflicts"])))
    print("DETERMINISTIC_DIGEST=" + artifact["digest"])
    return 0 if not parity["conflicts"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
