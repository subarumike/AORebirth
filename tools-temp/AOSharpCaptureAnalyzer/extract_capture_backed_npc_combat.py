#!/usr/bin/env python3
"""Extract capture-backed NPC combat profiles from the canonical raw corpus.

The extractor reconciles the two durable AOSharp raw sinks before decoding any
combat packet.  Reusable profiles exclude only capture-proven mutable fields
(runtime identities, targets, landed amount, WIFU MultipleCount/current
ammunition, and SpecialAttackWeapon Unknown5 state) while the inventory retains
their exact raw provenance.
"""

from __future__ import annotations

import argparse
import binascii
import csv
import datetime as dt
import gc
import hashlib
import json
import os
import random
import signal
import shutil
import struct
import subprocess
import sys
import tempfile
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable

if hasattr(sys, "set_int_max_str_digits"):
    sys.set_int_max_str_digits(0)

REPO_ROOT = Path(__file__).resolve().parents[2]
PRIMARY_CAPTURE_REPO_ROOT_ENVIRONMENT = (
    "AO_REBIRTH_GENERATED_COMBAT_PRIMARY_CAPTURE_REPO_ROOT"
)
CAPTURE_REPO_ROOT = Path(
    os.environ.get(PRIMARY_CAPTURE_REPO_ROOT_ENVIRONMENT, str(REPO_ROOT))
).resolve(strict=True)
CAPTURE_ROOT = (
    CAPTURE_REPO_ROOT
    / "tools-temp"
    / "AOSharpLiveCapture"
    / "bin"
    / "Debug"
    / "captures"
)
LEGACY_CAPTURE_ROOT = CAPTURE_REPO_ROOT / "For Repo"
OUTPUT = REPO_ROOT / "docs" / "generated" / "capture_backed_npc_combat_inventory.json"
CATALOG_OUTPUT = (
    REPO_ROOT
    / "AORebirth"
    / "Server"
    / "ZoneEngine"
    / "Core"
    / "Playfields"
    / "CapturedEnemyCombatProfileCatalog.g.cs"
)
FIXTURE_OUTPUT = (
    REPO_ROOT
    / "AORebirth"
    / "Libraries"
    / "Source"
    / "AOtomation"
    / "AOtomation.Messaging"
    / "src"
    / "SmokeLounge.AOtomation.Messaging.Tests"
    / "CapturedEnemyCombatProfileCatalogFixtures.g.cs"
)
SCFU_ANALYZER_SOURCE = (
    REPO_ROOT
    / "tools-temp"
    / "AOSharpCaptureAnalyzer"
    / "bin"
    / "Debug"
    / "AOSharpCaptureAnalyzer.exe"
)
SCFU_ANALYZER_PROJECT_SOURCE = (
    REPO_ROOT
    / "tools-temp"
    / "AOSharpCaptureAnalyzer"
    / "AOSharpCaptureAnalyzer.csproj"
)
SCFU_ANALYZER_OVERRIDE_ENVIRONMENT = (
    "AO_REBIRTH_GENERATED_COMBAT_PRIMARY_SCFU_ANALYZER"
)
SCFU_ANALYZER = Path(
    os.environ.get(
        SCFU_ANALYZER_OVERRIDE_ENVIRONMENT,
        str(SCFU_ANALYZER_SOURCE),
    )
).resolve()
CAPTURE_WORKER_MAX_ATTEMPTS = 8
AGGREGATE_WORKER_MAX_ATTEMPTS = 8
CAPTURE_WORKER_TIMEOUT_SECONDS = 600
AGGREGATE_WORKER_TIMEOUT_SECONDS = 7200
CHILD_CLEANUP_TIMEOUT_SECONDS = 10
AGGREGATE_WORKER_SUMMARY_NAME = "aggregate-summary.json"
AGGREGATE_INPUT_PLAN_NAME = "capture-input-plan.json"
AGGREGATE_INPUT_SNAPSHOT_NAME = "capture-input-snapshot.json"
CAPTURE_SNAPSHOT_DIRECTORY_NAME = "capture-shards"
CAPTURE_SNAPSHOT_SCHEMA_VERSION = 1
CAPTURE_SNAPSHOT_SOURCE_NAMES = (
    "capture_info.json",
    "packets.hex.log",
    "raw-packets.csv",
    "scfu-appearance.csv",
)
AGGREGATE_WORKER_ARTIFACT_NAMES = {
    "inventory": "capture-backed-npc-combat-inventory.json",
    "catalog": "CapturedEnemyCombatProfileCatalog.g.cs",
    "fixtures": "CapturedEnemyCombatProfileCatalogFixtures.g.cs",
}
AGGREGATE_CLI_SUMMARY_KEYS = (
    "captureSessionsDiscovered",
    "canonicalValidSessions",
    "completeAttackInfoChains",
    "captureCertifiedProfiles",
    "runtimeReadyProfiles",
    "captureCertifiedSemanticDefinitions",
    "runtimeReadyGeneratedSemanticDefinitions",
    "unresolvedProfiles",
    "decodeOrProjectionErrors",
)
ATTACK_RANGE_STAT_ID = 287
ARETE_EXACT_NATURAL_ATTACK_RANGE_PROFILES = {
    "resource=6553|md=17657|level=2|name=Garbage Flea": (
        ("LEW2", 120913, 120914),
        ("LEW1", 120910, 120911),
    ),
    "resource=6553|md=17687|level=5|name=Rollerrat": (
        ("LEW2", 120913, 120914),
        ("LEW1", 120910, 120911),
    ),
    "resource=6553|md=17687|level=6|name=Rollerrat": (
        ("LEW2", 120913, 120914),
        ("LEW1", 120910, 120911),
    ),
    "resource=6553|md=17714|level=2|name=Waste Collector": (
        ("LEW2", 120913, 120914),
        ("LEW1", 120910, 120911),
    ),
    "resource=6553|md=297023|level=1|name=Cleaning Robot": (
        ("LEW2", 120913, 120914),
        ("LEW1", 120910, 120911),
    ),
    "resource=6553|md=297023|level=2|name=Cleanmeister Intelligence Robot": (
        ("LEW2", 120913, 120914),
        ("LEW1", 120910, 120911),
    ),
    "resource=6553|md=30365|level=6|name=Desert Reet": (
        ("REW2", 121041, 121042),
        ("REW1", 121038, 121039),
    ),
}
ARETE_EXACT_NATURAL_ATTACK_RANGE_TEMPLATE_IDS = tuple(
    sorted(
        {
            template_id
            for families in ARETE_EXACT_NATURAL_ATTACK_RANGE_PROFILES.values()
            for _, low_template, high_template in families
            for template_id in (low_template, high_template)
        }
    )
)

sys.path.insert(0, str(REPO_ROOT / "tools-temp" / "AOSharpLiveCapture"))
sys.path.insert(0, str(REPO_ROOT / "tools"))
from decode_npc_lifecycle_capture import load_packet_records  # noqa: E402
from inventory_aosharp_captures import discover_capture_directories  # noqa: E402


class CaptureAggregationInvariantError(RuntimeError):
    """A deterministic aggregate snapshot or cross-reference invariant failed."""


class CaptureShardMaterializationError(CaptureAggregationInvariantError):
    """A worker shard was missing, unreadable, or not valid serialized JSON."""


class BoundedChildTimeout(RuntimeError):
    """A supervised child exceeded its deterministic wall-clock bound."""


_SELF_TEST_FAULT_HOOK: Any = None


def _invoke_self_test_fault(event: str, context: dict[str, Any]) -> None:
    if _SELF_TEST_FAULT_HOOK is not None:
        _SELF_TEST_FAULT_HOOK(event, context)


SIMPLE_CHAR = 0x0000C350
MESSAGE_TYPES = {
    0x271B3A6B: "SimpleCharFullUpdate",
    0x3B1D2268: "WeaponItemFullUpdate",
    0x1D3C0F1C: "SpecialAttackWeapon",
    0x28494070: "Attack",
    0x46002F16: "AttackInfo",
    0x5C654B28: "MissedAttackInfo",
    0x754F1115: "SpecialAttackInfo",
    0x25314D6D: "CastNanoSpell",
    0x4A41203E: "StopFight",
    0x5E477770: "CharacterAction",
    0x36510078: "Despawn",
}
COMBAT_MESSAGE_TYPES = set(MESSAGE_TYPES.values()) - {"SimpleCharFullUpdate"}
FULL_WEAPON_STAT_ORDER = (0, 23, 701, 702, 703, 412, 26, 294, 210)
MUTABLE_WIFU_STAT_IDS = frozenset({26, 412})
MUTABLE_SAW_FIELD_NAMES = frozenset({"unknown5"})
WEAPON_STAT_NAMES = {
    0: "Flags",
    23: "StaticInstance",
    701: "ACGItemLevel",
    702: "ACGItemTemplateID",
    703: "ACGItemTemplateID2",
    412: "MultipleCount",
    26: "Energy",
    294: "AttackDelay",
    210: "RechargeDelay",
}


class DecodeError(ValueError):
    pass


def signed32(value: int) -> int:
    return value - 0x100000000 if value & 0x80000000 else value


def signed16(value: int) -> int:
    return value - 0x10000 if value & 0x8000 else value


def hex_identity(value: int) -> str:
    return f"0x{value & 0xFFFFFFFF:08X}"


def canonical(value: Any) -> str:
    return json.JSONEncoder(
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=True,
    ).encode(value)


def digest(value: Any, length: int = 16) -> str:
    return hashlib.sha256(canonical(value).encode("utf-8")).hexdigest()[:length]


PACKET_AUDIT_LEDGER_COLUMNS = (
    "captureSessionIndex",
    "artifactIndex",
    "directionIndex",
    "sequence",
    "globalOrdinal",
    "messageTypeIndex",
    "packetLength",
    "bodyLength",
    "packetSha256Base64",
    "bodySha256Base64",
    "decodedFieldsSha256Base64",
    "metadataGenerationIndex",
    "metadataResolutionIndex",
    "sourceIdentitySigned",
    "targetIdentitySigned",
    "auditGroupIndexes",
)
PACKET_AUDIT_PACKET_ID_DERIVATION = (
    "{sessions[captureSessionIndex].capture}|"
    "{packetAuditDirectionTable[directionIndex]}|{sequence}|"
    "{first 12 hexadecimal characters decoded from packetSha256Base64}"
)


def sha256_canonical(value: Any) -> str:
    return hashlib.sha256(canonical(value).encode("utf-8")).hexdigest()


def packet_reference_sha256(packet_ids: Iterable[str]) -> str:
    return sha256_canonical(sorted(set(packet_ids)))


def _json_ascii_string(value: str) -> str:
    """Encode a string exactly like json.dumps(..., ensure_ascii=True)."""
    if (
        value.isascii()
        and value.isprintable()
        and '"' not in value
        and "\\" not in value
    ):
        return '"' + value + '"'
    escaped = ['"']
    short_escapes = {
        '"': '\\"',
        "\\": "\\\\",
        "\b": "\\b",
        "\f": "\\f",
        "\n": "\\n",
        "\r": "\\r",
        "\t": "\\t",
    }
    for character in value:
        replacement = short_escapes.get(character)
        if replacement is not None:
            escaped.append(replacement)
            continue
        codepoint = ord(character)
        if 0x20 <= codepoint <= 0x7E:
            escaped.append(character)
        elif codepoint <= 0xFFFF:
            escaped.append(f"\\u{codepoint:04x}")
        else:
            surrogate = codepoint - 0x10000
            escaped.append(f"\\u{0xD800 + (surrogate >> 10):04x}")
            escaped.append(f"\\u{0xDC00 + (surrogate & 0x3FF):04x}")
    escaped.append('"')
    return "".join(escaped)


def _positional_json(value: Any) -> str:
    """Serialize compact ledger values without invoking the native JSON encoder."""
    value_type = type(value)
    if value is None:
        return "null"
    if value_type is int:
        return str(value)
    if value_type is str:
        return _json_ascii_string(value)
    if value_type is list:
        members: list[str] = []
        for member in value:
            members.append(_positional_json(member))
        return "[" + ",".join(members) + "]"
    raise TypeError(
        "packet audit ledger contains unsupported positional JSON type "
        f"{value_type.__name__}"
    )


def positional_ledger_sha256(rows: Iterable[list[Any]]) -> str:
    result = hashlib.sha256()
    result.update(b"[")
    for index, row in enumerate(rows):
        if index:
            result.update(b",")
        result.update(_positional_json(row).encode("ascii"))
    result.update(b"]")
    return result.hexdigest()


def valid_sha256(value: Any) -> bool:
    if not isinstance(value, str) or len(value) != 64:
        return False
    for character in value:
        if character not in "0123456789abcdef":
            return False
    return True


def _projected_item_attack_range(
    templates: dict[str, Any],
    template_id: int,
) -> int:
    template = templates.get(str(template_id))
    if not isinstance(template, dict):
        raise ValueError(
            f"item-template projection is missing attack template {template_id}"
        )
    stats = template.get("stats")
    if not isinstance(stats, dict):
        raise ValueError(f"item template {template_id} has no stat projection")
    value = stats.get(str(ATTACK_RANGE_STAT_ID))
    if type(value) is not int or value <= 0:
        raise ValueError(
            f"item template {template_id} has no positive Stat {ATTACK_RANGE_STAT_ID}"
        )
    return value


def apply_arete_item_template_attack_range_authority(
    inventory: dict[str, Any],
    item_template_projection: dict[str, Any],
    item_database_sha256: str,
    item_database_byte_length: int,
) -> None:
    if not valid_sha256(item_database_sha256):
        raise ValueError("item database SHA-256 descriptor is invalid")
    if type(item_database_byte_length) is not int or item_database_byte_length <= 0:
        raise ValueError("item database byte-length descriptor is invalid")
    templates = item_template_projection.get("templates")
    if not isinstance(templates, dict):
        raise ValueError("item-template projection root is invalid")
    expected_template_ids = set(ARETE_EXACT_NATURAL_ATTACK_RANGE_TEMPLATE_IDS)
    try:
        actual_template_ids = {int(value) for value in templates}
    except (TypeError, ValueError) as error:
        raise ValueError("item-template projection contains an invalid template ID") from error
    if actual_template_ids != expected_template_ids:
        raise ValueError(
            "Arete attack-range item projection membership is not exact: "
            f"expected={sorted(expected_template_ids)} actual={sorted(actual_template_ids)}"
        )

    profiles_by_key = {
        profile.get("profileKey"): profile
        for profile in inventory.get("profiles", [])
        if isinstance(profile, dict)
    }
    authority_rows = []
    for profile_key, expected_families in sorted(
        ARETE_EXACT_NATURAL_ATTACK_RANGE_PROFILES.items()
    ):
        profile = profiles_by_key.get(profile_key)
        if profile is None:
            raise ValueError(f"Arete attack-range profile is missing: {profile_key}")
        variants = [
            variant
            for variant in profile.get("variants", [])
            if variant.get("captureCertified") is True
        ]
        if len(variants) != 1:
            raise ValueError(
                f"Arete attack-range profile must have one certified variant: {profile_key}"
            )
        variant = variants[0]
        specials = variant.get("baseSignature", {}).get(
            "specialAttackWeapon", {}
        ).get("specials", [])
        if len(specials) != len(expected_families):
            raise ValueError(
                f"Arete attack-range SAW family count changed: {profile_key}"
            )

        endpoint_rows = []
        range_values = set()
        for special, expected in zip(specials, expected_families):
            family_name, low_template, high_template = expected
            captured_name = bytes.fromhex(special.get("nameHex", "")).decode(
                "latin-1"
            )
            captured_low = special.get("lowTemplate")
            captured_high = special.get("highTemplate")
            if (
                captured_name != family_name
                or captured_low != low_template
                or captured_high not in (low_template, high_template)
            ):
                raise ValueError(
                    f"Arete attack-range captured SAW family changed: {profile_key} "
                    f"expected={expected} actual={(captured_name, captured_low, captured_high)}"
                )
            low_range = _projected_item_attack_range(templates, low_template)
            high_range = _projected_item_attack_range(templates, high_template)
            if low_range != high_range:
                raise ValueError(
                    f"item-template attack range is not invariant across {family_name} "
                    f"endpoints {low_template}/{high_template}"
                )
            range_values.add(low_range)
            endpoint_rows.append(
                {
                    "weaponName": family_name,
                    "capturedLowTemplate": captured_low,
                    "capturedHighTemplate": captured_high,
                    "itemDatabaseLowTemplate": low_template,
                    "itemDatabaseHighTemplate": high_template,
                    "lowTemplateAttackRangeMeters": low_range,
                    "highTemplateAttackRangeMeters": high_range,
                }
            )
        if len(range_values) != 1:
            raise ValueError(
                f"Arete attack-range SAW families disagree: {profile_key}"
            )
        attack_range = float(next(iter(range_values)))
        evidence = {
            "evidenceId": digest(
                {
                    "profileKey": profile_key,
                    "semanticProfileId": variant["semanticProfileId"],
                    "representativeSawPacketId": variant[
                        "representativeSawPacketId"
                    ],
                    "itemDatabaseSha256": item_database_sha256,
                    "statId": ATTACK_RANGE_STAT_ID,
                    "templateEndpoints": endpoint_rows,
                    "attackRangeMeters": attack_range,
                }
            ),
            "authority": "captured SpecialAttackWeapon templates joined to immutable ItemDb stats",
            "sourceItemDatabase": "AORebirth/Datafiles/items.dat",
            "itemDatabaseSha256": item_database_sha256,
            "itemDatabaseByteLength": item_database_byte_length,
            "statId": ATTACK_RANGE_STAT_ID,
            "statName": "AttackRange",
            "representativeSawPacketId": variant["representativeSawPacketId"],
            "templateEndpoints": endpoint_rows,
            "attackRangeMeters": attack_range,
        }
        variant["capturedAttackRangeMeters"] = attack_range
        variant["capturedAttackRangeEvidence"] = evidence
        variant["runtimeMissingEvidence"] = [
            reason
            for reason in variant.get("runtimeMissingEvidence", [])
            if reason != "capture-backed non-equipped attack range"
        ]
        for stream in variant.get("streams", []):
            stream["capturedAttackRange"] = attack_range
            stream["capturedAttackRangeEvidenceId"] = evidence["evidenceId"]
            stream["runtimeMissingEvidence"] = [
                reason
                for reason in stream.get("runtimeMissingEvidence", [])
                if reason != "capture-backed non-equipped attack range"
            ]
            stream["runtimeContractReady"] = not stream["runtimeMissingEvidence"]
        variant["runtimeContractReady"] = (
            len(variant.get("streams", [])) == 1
            and variant["streams"][0]["runtimeContractReady"]
        )
        profile["runtimeReadyVariantCount"] = sum(
            candidate.get("captureCertified") is True
            and candidate.get("runtimeContractReady") is True
            for candidate in profile.get("variants", [])
        )
        authority_rows.append(
            {
                "profileKey": profile_key,
                "semanticProfileId": variant["semanticProfileId"],
                "evidenceId": evidence["evidenceId"],
                "attackRangeMeters": attack_range,
            }
        )

    inventory["attackRangeAuthority"] = {
        "authority": "captured SAW identity plus immutable ItemDb Stat 287",
        "sourceItemDatabase": "AORebirth/Datafiles/items.dat",
        "itemDatabaseSha256": item_database_sha256,
        "itemDatabaseByteLength": item_database_byte_length,
        "statId": ATTACK_RANGE_STAT_ID,
        "templateIds": list(ARETE_EXACT_NATURAL_ATTACK_RANGE_TEMPLATE_IDS),
        "profiles": authority_rows,
    }
    authoritative_inputs = inventory.setdefault("authoritativeInputs", [])
    range_input = (
        "captured SpecialAttackWeapon template identities joined to immutable "
        "AORebirth/Datafiles/items.dat Stat 287"
    )
    if range_input not in authoritative_inputs:
        authoritative_inputs.append(range_input)
    summary = inventory["summary"]
    summary["itemTemplateAttackRangeProfiles"] = len(authority_rows)
    summary["itemTemplateAttackRangeSemanticDefinitions"] = len(authority_rows)
    summary["runtimeReadyProfiles"] = sum(
        profile.get("runtimeReadyVariantCount", 0) > 0
        for profile in inventory.get("profiles", [])
    )
    summary["runtimeReadyGeneratedSemanticDefinitions"] = sum(
        1
        for profile in inventory.get("profiles", [])
        if runtime_resource_from_public(profile.get("metadata")) is not None
        for variant in profile.get("variants", [])
        if variant.get("captureCertified") is True
        and variant.get("runtimeContractReady") is True
    )
    summary["runtimeUnresolvedProfiles"] = (
        len(inventory.get("profiles", [])) - summary["runtimeReadyProfiles"]
    )


def validate_arete_item_template_attack_range_authority(
    inventory: dict[str, Any],
) -> None:
    authority = inventory.get("attackRangeAuthority")
    if not isinstance(authority, dict):
        raise ValueError("generated inventory has no Arete attack-range authority")
    if (
        authority.get("statId") != ATTACK_RANGE_STAT_ID
        or authority.get("templateIds")
        != list(ARETE_EXACT_NATURAL_ATTACK_RANGE_TEMPLATE_IDS)
        or not valid_sha256(authority.get("itemDatabaseSha256"))
        or type(authority.get("itemDatabaseByteLength")) is not int
        or authority["itemDatabaseByteLength"] <= 0
    ):
        raise ValueError("generated Arete attack-range authority descriptor is invalid")
    rows = authority.get("profiles")
    if not isinstance(rows, list) or len(rows) != len(
        ARETE_EXACT_NATURAL_ATTACK_RANGE_PROFILES
    ):
        raise ValueError("generated Arete attack-range authority profile count is stale")
    rows_by_key = {row.get("profileKey"): row for row in rows}
    profiles_by_key = {
        profile.get("profileKey"): profile
        for profile in inventory.get("profiles", [])
    }
    if set(rows_by_key) != set(ARETE_EXACT_NATURAL_ATTACK_RANGE_PROFILES):
        raise ValueError("generated Arete attack-range authority membership is stale")
    for profile_key, authority_row in rows_by_key.items():
        profile = profiles_by_key[profile_key]
        variants = [
            variant
            for variant in profile.get("variants", [])
            if variant.get("captureCertified") is True
        ]
        if len(variants) != 1:
            raise ValueError(
                f"generated Arete attack-range variant count is stale: {profile_key}"
            )
        variant = variants[0]
        evidence = variant.get("capturedAttackRangeEvidence")
        if (
            variant.get("capturedAttackRangeMeters") != 2.0
            or not isinstance(evidence, dict)
            or evidence.get("evidenceId") != authority_row.get("evidenceId")
            or evidence.get("itemDatabaseSha256")
            != authority.get("itemDatabaseSha256")
            or evidence.get("attackRangeMeters") != 2.0
            or variant.get("semanticProfileId")
            != authority_row.get("semanticProfileId")
        ):
            raise ValueError(
                f"generated Arete attack-range evidence is stale: {profile_key}"
            )
        if any(
            stream.get("capturedAttackRange") != 2.0
            or stream.get("capturedAttackRangeEvidenceId")
            != evidence.get("evidenceId")
            or "capture-backed non-equipped attack range"
            in stream.get("runtimeMissingEvidence", [])
            for stream in variant.get("streams", [])
        ):
            raise ValueError(
                f"generated Arete stream attack-range evidence is stale: {profile_key}"
            )
        if "capture-backed non-equipped attack range" in variant.get(
            "runtimeMissingEvidence", []
        ):
            raise ValueError(
                f"generated Arete variant still reports missing range: {profile_key}"
            )
    summary = inventory.get("summary", {})
    if (
        summary.get("itemTemplateAttackRangeProfiles") != len(rows)
        or summary.get("itemTemplateAttackRangeSemanticDefinitions") != len(rows)
    ):
        raise ValueError("generated Arete attack-range authority summary is stale")


def sha256_hex_to_base64(value: str) -> str:
    if not valid_sha256(value):
        raise ValueError("invalid hexadecimal SHA-256")
    return binascii.b2a_base64(
        binascii.unhexlify(value), newline=False
    ).decode("ascii")


def sha256_base64_to_hex(value: Any) -> str | None:
    if (
        not isinstance(value, str)
        or len(value) != 44
        or value[43] != "="
    ):
        return None
    try:
        raw = binascii.a2b_base64(value, strict_mode=True)
    except (binascii.Error, ValueError):
        return None
    if len(raw) != 32:
        return None
    return raw.hex()


def packet_audit_ledger_packet_id(
    row: list[Any],
    sessions: list[dict[str, Any]],
    direction_table: list[str],
) -> str:
    capture_index = row[0]
    direction = direction_table[row[2]]
    sequence = row[3]
    packet_sha256 = sha256_base64_to_hex(row[8])
    if packet_sha256 is None:
        raise ValueError("compact packet evidence has invalid packet SHA-256")
    return (
        f"{sessions[capture_index]['capture']}|{direction}|{sequence}|"
        f"{packet_sha256[:12]}"
    )


def parse_timestamp(value: str) -> dt.datetime | None:
    text = (value or "").strip()
    if not text or text == "unknown":
        return None
    if text.endswith("Z"):
        text = text[:-1] + "+00:00"
    try:
        parsed = dt.datetime.fromisoformat(text)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=dt.timezone.utc)
    return parsed.astimezone(dt.timezone.utc)


class BodyReader:
    """Big-endian body reader that records byte provenance for every field."""

    def __init__(self, body: bytes):
        self.body = body
        self.offset = 0
        self.provenance: dict[str, dict[str, Any]] = {}

    def take(self, name: str, length: int) -> bytes:
        start = self.offset
        end = start + length
        if end > len(self.body):
            raise DecodeError(
                f"packet ended at {len(self.body)} while reading {name} ({length} bytes at {start})"
            )
        raw = self.body[start:end]
        self.offset = end
        self.provenance[name] = {
            "bodyOffset": start,
            "packetOffset": start + 16,
            "length": length,
            "rawHex": raw.hex().upper(),
        }
        return raw

    def u8(self, name: str) -> int:
        value = self.take(name, 1)[0]
        self.provenance[name]["value"] = value
        return value

    def u16(self, name: str) -> int:
        value = struct.unpack(">H", self.take(name, 2))[0]
        self.provenance[name]["value"] = value
        return value

    def i16(self, name: str) -> int:
        value = signed16(self.u16(name))
        self.provenance[name]["value"] = value
        return value

    def u32(self, name: str) -> int:
        value = struct.unpack(">I", self.take(name, 4))[0]
        self.provenance[name]["value"] = value
        return value

    def i32(self, name: str) -> int:
        value = signed32(self.u32(name))
        self.provenance[name]["value"] = value
        return value

    def identity(self, name: str) -> dict[str, Any]:
        identity_type = self.u32(name + ".type")
        raw_instance = self.u32(name + ".instance")
        return {
            "type": identity_type,
            "instance": signed32(raw_instance),
            "instanceHex": f"{raw_instance:08X}",
        }

    def finish(self) -> None:
        if self.offset != len(self.body):
            raise DecodeError(f"decoder consumed {self.offset} of {len(self.body)} bytes")


def finish(reader: BodyReader, fields: dict[str, Any]) -> dict[str, Any]:
    reader.finish()
    fields["fieldProvenance"] = reader.provenance
    return fields


def decode_weapon_item_full_update(body: bytes) -> dict[str, Any]:
    reader = BodyReader(body)
    message_id = reader.u32("messageId")
    weapon = reader.identity("weapon")
    n3_unknown = reader.u8("n3Unknown")
    unknown1 = reader.i32("unknown1")
    owner = reader.identity("owner")
    prefix = {
        "messageId": message_id,
        "weapon": weapon,
        "n3Unknown": n3_unknown,
        "unknown1": unknown1,
        "owner": owner,
    }
    if owner["type"] != SIMPLE_CHAR or owner["instance"] == 0:
        prefix["npcOwnerLinked"] = False
        prefix["ignoredReason"] = "WeaponItemFullUpdate owner is not a nonzero SimpleChar"
        prefix["fieldProvenance"] = reader.provenance
        return prefix

    playfield_id = reader.i32("playfieldId")
    state_machine = reader.identity("stateMachine")
    unknown2 = reader.i16("unknown2")
    encoded_count = reader.i32("encodedStatCount")
    if encoded_count <= 0 or encoded_count % 0x03F1 != 0:
        raise DecodeError(f"invalid WIFU X3F1 count {encoded_count}")
    stat_count = encoded_count // 0x03F1 - 1
    if stat_count < 0 or stat_count > 256:
        raise DecodeError(f"invalid WIFU stat count {stat_count}")
    stats = []
    for index in range(stat_count):
        stat = reader.i32(f"stats[{index}].stat")
        raw_value = reader.u32(f"stats[{index}].value")
        stats.append(
            {
                "index": index,
                "stat": stat,
                "name": WEAPON_STAT_NAMES.get(stat, f"Stat{stat}"),
                "value": signed32(raw_value),
                "rawValue": raw_value,
            }
        )
    unknown3 = reader.i32("unknown3")
    stat_order = tuple(row["stat"] for row in stats)
    stat_values = {row["stat"]: row["value"] for row in stats}
    problems = []
    if unknown1 != 0x0B:
        problems.append("unknown1 must equal 11")
    inventory_slot = unknown2 & 0xFF
    if inventory_slot <= 0:
        problems.append("inventory slot must be positive")
    if unknown2 != (0x0100 | inventory_slot):
        problems.append("unknown2 must encode 0x0100 plus the inventory slot")
    if state_machine["type"] == 0:
        problems.append("state-machine identity type must be nonzero")
    if stat_order != FULL_WEAPON_STAT_ORDER:
        problems.append("ordered WIFU stats must be exactly " + ",".join(map(str, FULL_WEAPON_STAT_ORDER)))
    if len(set(stat_order)) != len(stat_order):
        problems.append("WIFU stat identifiers must not repeat")
    low_template = stat_values.get(702, 0)
    if stat_values.get(23, 0) != low_template:
        problems.append("StaticInstance must equal the low weapon template")
    for stat in (701, 702, 703, 412):
        if stat_values.get(stat, 0) <= 0:
            problems.append(f"{WEAPON_STAT_NAMES[stat]} must be positive")
    result = {
        **prefix,
        "npcOwnerLinked": True,
        "playfieldId": playfield_id,
        "stateMachine": state_machine,
        "unknown2": unknown2,
        "inventorySlot": inventory_slot,
        "stats": stats,
        "unknown3": unknown3,
        "definitionComplete": not problems,
        "definitionProblems": problems,
        "flags": stat_values.get(0),
        "staticInstance": stat_values.get(23),
        "quality": stat_values.get(701),
        "lowTemplate": low_template,
        "highTemplate": stat_values.get(703),
        "multipleCount": stat_values.get(412),
        "energy": stat_values.get(26),
        "attackDelay": stat_values.get(294),
        "rechargeDelay": stat_values.get(210),
    }
    return finish(reader, result)


def decode_special_attack_weapon(body: bytes) -> dict[str, Any]:
    reader = BodyReader(body)
    result = {
        "messageId": reader.u32("messageId"),
        "source": reader.identity("source"),
        "n3Unknown": reader.u8("n3Unknown"),
    }
    encoded_count = reader.i32("encodedSpecialCount")
    if encoded_count <= 0 or encoded_count % 0x03F1 != 0:
        raise DecodeError(f"invalid SAW X3F1 count {encoded_count}")
    count = encoded_count // 0x03F1 - 1
    if count < 0 or count > 64:
        raise DecodeError(f"invalid SAW special count {count}")
    specials = []
    for index in range(count):
        tag_raw = None
        low = reader.i32(f"specials[{index}].lowTemplate")
        high = reader.i32(f"specials[{index}].highTemplate")
        tag_raw = reader.u32(f"specials[{index}].tag")
        name_raw = reader.take(f"specials[{index}].name", 4)
        reader.provenance[f"specials[{index}].name"]["value"] = name_raw.decode("latin-1")
        specials.append(
            {
                "index": index,
                "lowTemplate": low,
                "highTemplate": high,
                "tag": signed32(tag_raw),
                "tagHex": f"{tag_raw:08X}",
                "name": name_raw.decode("latin-1"),
                "nameHex": name_raw.hex().upper(),
            }
        )
    result["specials"] = specials
    for index in range(1, 6):
        result[f"unknown{index}"] = reader.i32(f"unknown{index}")
    return finish(reader, result)


def decode_attack(body: bytes) -> dict[str, Any]:
    reader = BodyReader(body)
    return finish(
        reader,
        {
            "messageId": reader.u32("messageId"),
            "source": reader.identity("source"),
            "n3Unknown": reader.u8("n3Unknown"),
            "target": reader.identity("target"),
            "action": reader.u8("action"),
        },
    )


def decode_attack_info(body: bytes) -> dict[str, Any]:
    reader = BodyReader(body)
    weapon_raw = None
    result = {
        "messageId": reader.u32("messageId"),
        "source": reader.identity("source"),
        "n3Unknown": reader.u8("n3Unknown"),
        "amount": reader.i32("amount"),
        "ammo": reader.i32("ammo"),
        "weaponSlot": reader.i32("weaponSlot"),
        "target": reader.identity("target"),
        "damageTypeWire": reader.i32("damageTypeWire"),
        "hitTypeWire": reader.i32("hitTypeWire"),
    }
    weapon_raw = reader.u32("weaponInstance")
    result["weaponInstance"] = signed32(weapon_raw)
    result["weaponInstanceHex"] = f"{weapon_raw:08X}"
    return finish(reader, result)


def decode_missed_attack_info(body: bytes) -> dict[str, Any]:
    reader = BodyReader(body)
    return finish(
        reader,
        {
            "messageId": reader.u32("messageId"),
            "source": reader.identity("source"),
            "n3Unknown": reader.u8("n3Unknown"),
            "unknown1": reader.i32("unknown1"),
            "unknown2": reader.i32("unknown2"),
            "attacker": reader.identity("attacker"),
            "defender": reader.identity("defender"),
            "unknown5": reader.i32("unknown5"),
        },
    )


def decode_special_attack_info(body: bytes) -> dict[str, Any]:
    reader = BodyReader(body)
    return finish(
        reader,
        {
            "messageId": reader.u32("messageId"),
            "source": reader.identity("source"),
            "n3Unknown": reader.u8("n3Unknown"),
            "unknown1": reader.i32("unknown1"),
            "unknown2": reader.i32("unknown2"),
            "unknown3": reader.i32("unknown3"),
            "target": reader.identity("target"),
            "unknown4": reader.i32("unknown4"),
            "unknown5": reader.i32("unknown5"),
        },
    )


def decode_cast_nano_spell(body: bytes) -> dict[str, Any]:
    reader = BodyReader(body)
    return finish(
        reader,
        {
            "messageId": reader.u32("messageId"),
            "source": reader.identity("source"),
            "n3Unknown": reader.u8("n3Unknown"),
            "nanoId": reader.i32("nanoId"),
            "target": reader.identity("target"),
            "unknown1": reader.i32("unknown1"),
            "caster": reader.identity("caster"),
        },
    )


def decode_stop_fight(body: bytes) -> dict[str, Any]:
    reader = BodyReader(body)
    return finish(
        reader,
        {
            "messageId": reader.u32("messageId"),
            "source": reader.identity("source"),
            "n3Unknown": reader.u8("n3Unknown"),
            "unknown1": reader.i32("unknown1"),
        },
    )


def decode_despawn(body: bytes) -> dict[str, Any]:
    reader = BodyReader(body)
    return finish(
        reader,
        {
            "messageId": reader.u32("messageId"),
            "source": reader.identity("source"),
            "n3Unknown": reader.u8("n3Unknown"),
        },
    )


def decode_character_action(body: bytes) -> dict[str, Any]:
    reader = BodyReader(body)
    result = finish(
        reader,
        {
            "messageId": reader.u32("messageId"),
            "source": reader.identity("source"),
            "n3Unknown": reader.u8("n3Unknown"),
            "action": reader.i32("action"),
            "unknown1": reader.i32("unknown1"),
            "target": reader.identity("target"),
            "parameter1": reader.i32("parameter1"),
            "parameter2": reader.i32("parameter2"),
            "unknown2": reader.i16("unknown2"),
        },
    )
    result["unsupported"] = True
    return result


DECODERS = {
    "WeaponItemFullUpdate": decode_weapon_item_full_update,
    "SpecialAttackWeapon": decode_special_attack_weapon,
    "Attack": decode_attack,
    "AttackInfo": decode_attack_info,
    "MissedAttackInfo": decode_missed_attack_info,
    "SpecialAttackInfo": decode_special_attack_info,
    "CastNanoSpell": decode_cast_nano_spell,
    "StopFight": decode_stop_fight,
    "Despawn": decode_despawn,
    "CharacterAction": decode_character_action,
}


@dataclass
class MetadataGeneration:
    capture: str
    capture_id: str
    sequence: int
    global_ordinal: int | None
    source: int
    name: str
    monster_data: int
    level: int
    captured_realm_id: int | None
    projection: str
    packet_sha256: str
    scfu_special_attacks: str
    owner_identity: str

    @property
    def profile(self) -> tuple[str, int, int]:
        return self.name, self.monster_data, self.level

    @property
    def generation_key(self) -> str:
        return f"{self.capture}|{hex_identity(self.source)}|scfu={self.sequence}"

    def public(self) -> dict[str, Any]:
        return {
            "generationKey": self.generation_key,
            "capture": self.capture,
            "captureId": self.capture_id,
            "sequence": self.sequence,
            "globalOrdinal": self.global_ordinal,
            "sourceIdentity": hex_identity(self.source),
            "name": self.name,
            "monsterData": self.monster_data,
            "level": self.level,
            "capturedRealmId": self.captured_realm_id,
            "projection": self.projection,
            "packetSha256": self.packet_sha256,
            "scfuSpecialAttacks": self.scfu_special_attacks,
            "ownerIdentity": self.owner_identity,
        }


@dataclass
class PacketRecord:
    packet_id: str
    capture: str
    capture_id: str
    captured_utc: str
    direction: str
    sequence: int
    global_ordinal: int | None
    message_type: str
    packet_hex: str
    body_hex: str
    packet_sha256: str
    body_sha256: str
    canonical_source: str
    decoded: dict[str, Any]
    packet_sha256_base64: str = ""
    body_sha256_base64: str = ""
    decoded_sha256_base64: str = ""
    metadata: MetadataGeneration | None = None
    metadata_resolution: str = ""

    @property
    def source(self) -> int | None:
        fields = self.decoded
        if self.message_type == "WeaponItemFullUpdate":
            identity = fields.get("owner")
        elif self.message_type == "MissedAttackInfo":
            identity = fields.get("attacker")
        else:
            identity = fields.get("source") or fields.get("caster")
        return identity.get("instance") if identity else None

    @property
    def target(self) -> int | None:
        identity = (
            self.decoded.get("defender")
            if self.message_type == "MissedAttackInfo"
            else self.decoded.get("target")
        )
        return identity.get("instance") if identity else None

    @property
    def time(self) -> dt.datetime | None:
        return parse_timestamp(self.captured_utc)

    def provenance(self) -> dict[str, Any]:
        return {
            "packetId": self.packet_id,
            "capture": self.capture,
            "captureId": self.capture_id,
            "artifact": self.canonical_source,
            "direction": self.direction,
            "sequence": self.sequence,
            "globalOrdinal": self.global_ordinal,
            "capturedUtc": self.captured_utc,
            "messageType": self.message_type,
            "packetLength": len(self.packet_hex) // 2,
            "packetSha256": self.packet_sha256,
            "bodySha256": self.body_sha256,
            "packetHex": self.packet_hex,
            "bodyHex": self.body_hex,
            "fields": self.decoded,
        }


def read_csv_rows(path: Path) -> Iterable[dict[str, str]]:
    if not path.exists():
        return ()

    def rows() -> Iterable[dict[str, str]]:
        with path.open("r", encoding="utf-8-sig", errors="replace", newline="") as handle:
            sanitized = (line.replace("\0", "") for line in handle)
            yield from csv.DictReader(sanitized)

    return rows()


def first_value(row: dict[str, Any], *names: str) -> str:
    for name in names:
        value = row.get(name)
        if value is not None and str(value).strip():
            return str(value).strip()
    return ""


def parse_int(value: str) -> int | None:
    text = (value or "").strip()
    if not text:
        return None
    try:
        return int(text, 0)
    except ValueError:
        return None


def parse_identity(value: str) -> int | None:
    text = (value or "").strip()
    colon = text.find(":")
    close = text.find(")", colon + 1)
    if colon < 0 or close < 0:
        return None
    try:
        return signed32(int(text[colon + 1 : close], 16))
    except ValueError:
        return None


def load_scfu_projection_rows(
    capture: Path,
    canonical_by_sequence: dict[tuple[str, int], dict[str, Any]],
    capture_key: str | None = None,
) -> tuple[list[dict[str, str]], str, list[dict[str, Any]]]:
    logical_capture = capture_key or capture.relative_to(CAPTURE_REPO_ROOT).as_posix()
    canonical_scfu = []
    for (direction, sequence), record in sorted(
        canonical_by_sequence.items(),
        key=lambda value: (
            value[1].get("globalOrdinal") is None,
            value[1].get("globalOrdinal") or value[0][1],
            value[0][1],
        ),
    ):
        if direction != "IN":
            continue
        raw = bytes.fromhex(record["rawHex"])
        if len(raw) < 20 or struct.unpack_from(">I", raw, 16)[0] != 0x271B3A6B:
            continue
        timestamp = record.get("timestamp")
        if not timestamp:
            raise RuntimeError(
                f"{capture}: canonical SCFU sequence {sequence} has no timestamp"
            )
        canonical_scfu.append(
            (
                sequence,
                record["rawHex"].upper(),
                f"{timestamp} IN #{sequence} len={len(raw)} "
                f"n3=SimpleCharFullUpdate hex={record['rawHex'].upper()}",
            )
        )

    if not canonical_scfu:
        return [], "canonical-raw-no-SCFU", []

    def analyze_canonical_rows(
        selected: list[tuple[int, str, str]],
    ) -> tuple[list[dict[str, str]], str, list[dict[str, Any]]]:
        if not selected:
            return [], "", []
        if not SCFU_ANALYZER.exists():
            raise RuntimeError(
                "raw SCFU metadata extraction requires the repository analyzer at "
                + str(SCFU_ANALYZER)
            )
        analyzer_sha256 = hashlib.sha256(SCFU_ANALYZER.read_bytes()).hexdigest()
        projection = (
            "canonical-raw-via-AOSharpCaptureAnalyzer.exe@sha256:"
            + analyzer_sha256
        )
        with tempfile.TemporaryDirectory(
            prefix="aorebirth-npc-combat-scfu-"
        ) as staging_name:
            staging = Path(staging_name)
            (staging / "packets.hex.log").write_text(
                "\n".join(row[2] for row in selected) + "\n",
                encoding="utf-8",
            )
            completed = subprocess.run(
                [str(SCFU_ANALYZER), str(staging)],
                capture_output=True,
                text=True,
                timeout=120,
                check=False,
            )
            output_path = staging / "scfu-appearance.csv"
            if not output_path.exists():
                output_path = staging / "scfu-appearance.pending.csv"
            error_path = staging / "scfu-decode-errors.csv"
            if not error_path.exists():
                error_path = staging / "scfu-decode-errors.pending.csv"
            if not output_path.exists():
                raise RuntimeError(
                    f"{capture}: canonical raw SCFU analyzer produced no projection; "
                    f"exit={completed.returncode} stderr={completed.stderr.strip()}"
                )
            rows = list(read_csv_rows(output_path))
            error_rows = list(read_csv_rows(error_path)) if error_path.exists() else []
            if len(rows) != len(selected):
                raise RuntimeError(
                    f"{capture}: canonical raw SCFU analyzer accounted for "
                    f"{len(rows)} projection rows for {len(selected)} raw packets"
                )
            errors = [
                {
                    "capture": logical_capture,
                    "artifact": projection,
                    "sequence": parse_int(first_value(row, "Sequence")),
                    "error": first_value(row, "DecodeError")
                    or "canonical raw SCFU decode failed",
                }
                for row in error_rows
            ]
            return rows, projection, errors

    source_path = capture / "scfu-appearance.csv"
    if not source_path.exists():
        return analyze_canonical_rows(canonical_scfu)

    existing_rows = list(read_csv_rows(source_path))
    existing_by_sequence: dict[int, list[dict[str, str]]] = defaultdict(list)
    for row in existing_rows:
        if first_value(row, "Direction").upper() != "IN":
            continue
        sequence = parse_int(first_value(row, "Sequence"))
        if sequence is not None:
            existing_by_sequence[sequence].append(row)

    accepted = []
    fallback = []
    for canonical in canonical_scfu:
        sequence, raw_hex, _ = canonical
        candidates = existing_by_sequence.get(sequence, [])
        if (
            len(candidates) == 1
            and first_value(candidates[0], "RawPacketHex").upper() == raw_hex
            and first_value(candidates[0], "DecodeStatus")
            in {"decoded_complete", "decoded"}
            and first_value(candidates[0], "DecodeFullyConsumed").lower()
            not in {"false", "0"}
        ):
            accepted.append(candidates[0])
        else:
            fallback.append(canonical)

    recovered, fallback_projection, errors = analyze_canonical_rows(fallback)
    if fallback:
        projection = (
            "scfu-appearance.csv reconciled with " + fallback_projection
        )
    else:
        projection = "raw-validated scfu-appearance.csv"
    merged = accepted + recovered
    if len(merged) != len(canonical_scfu):
        raise RuntimeError(
            f"{capture}: reconciled {len(merged)} SCFU rows for "
            f"{len(canonical_scfu)} canonical raw packets"
        )
    merged.sort(key=lambda row: parse_int(first_value(row, "Sequence")) or -1)
    return merged, projection, errors


def load_metadata_generations(
    capture: Path,
    capture_key: str,
    canonical_by_sequence: dict[tuple[str, int], dict[str, Any]],
) -> tuple[list[MetadataGeneration], list[dict[str, Any]]]:
    capture_id = Path(capture_key).name
    generations: list[MetadataGeneration] = []
    rows, projection, errors = load_scfu_projection_rows(
        capture, canonical_by_sequence, capture_key
    )
    for row_number, row in enumerate(rows, 2):
        if first_value(row, "Direction").upper() != "IN":
            continue
        if first_value(row, "CharacterInfoType") != "NPCInfo":
            continue
        if first_value(row, "DecodeStatus") not in {"decoded_complete", "decoded"}:
            continue
        source = parse_identity(first_value(row, "Identity"))
        sequence = parse_int(first_value(row, "Sequence"))
        name = first_value(row, "Name")
        monster_data = parse_int(first_value(row, "MonsterData"))
        level = parse_int(first_value(row, "Level"))
        realm = parse_int(first_value(row, "PlayfieldId"))
        if source is None or sequence is None or not name or not monster_data or not level:
            errors.append(
                {
                    "capture": capture_key,
                    "artifact": "scfu-appearance.csv",
                    "row": row_number,
                    "error": "complete NPC SCFU projection is missing identity, sequence, name, MonsterData, or level",
                }
            )
            continue
        canonical_record = canonical_by_sequence.get(("IN", sequence))
        projected_hex = first_value(row, "RawPacketHex").upper()
        if canonical_record is None:
            errors.append(
                {
                    "capture": capture_key,
                    "artifact": "scfu-appearance.csv",
                    "row": row_number,
                    "sequence": sequence,
                    "error": "SCFU projection has no canonical raw packet",
                }
            )
            continue
        if projected_hex and projected_hex != canonical_record["rawHex"]:
            errors.append(
                {
                    "capture": capture_key,
                    "artifact": "scfu-appearance.csv",
                    "row": row_number,
                    "sequence": sequence,
                    "error": "SCFU projection raw bytes disagree with canonical raw packet",
                }
            )
            continue
        raw = bytes.fromhex(canonical_record["rawHex"])
        if len(raw) < 20 or struct.unpack_from(">I", raw, 16)[0] != 0x271B3A6B:
            errors.append(
                {
                    "capture": capture_key,
                    "artifact": "scfu-appearance.csv",
                    "row": row_number,
                    "sequence": sequence,
                    "error": "SCFU projection points to a different N3 message type",
                }
            )
            continue
        generations.append(
            MetadataGeneration(
                capture=capture_key,
                capture_id=capture_id,
                sequence=sequence,
                global_ordinal=canonical_record.get("globalOrdinal"),
                source=source,
                name=name,
                monster_data=monster_data,
                level=level,
                captured_realm_id=realm,
                projection=projection,
                packet_sha256=hashlib.sha256(raw).hexdigest(),
                scfu_special_attacks=first_value(row, "SpecialAttacks"),
                owner_identity=first_value(row, "Owner"),
            )
        )
    return generations, errors


def source_from_decoded(message_type: str, decoded: dict[str, Any]) -> int | None:
    if message_type == "WeaponItemFullUpdate":
        identity = decoded.get("owner")
    elif message_type == "MissedAttackInfo":
        identity = decoded.get("attacker")
    else:
        identity = decoded.get("source") or decoded.get("caster")
    return identity.get("instance") if identity else None


def retain_combat_evidence_record(record: dict[str, Any]) -> bool:
    """Retain only inbound packet types consumed by this extractor.

    The lifecycle loader still validates and fingerprints every durable raw row;
    this predicate only limits the materialized canonical records returned to
    the combat extractor.
    """
    if record["direction"] != "IN":
        return False
    raw_hex = record["rawHex"]
    if len(raw_hex) < 40:
        return False
    return int(raw_hex[32:40], 16) in MESSAGE_TYPES


def parse_capture(
    capture: Path,
    capture_key: str | None = None,
) -> tuple[list[PacketRecord], list[MetadataGeneration], dict[str, Any], list[dict[str, Any]]]:
    capture_key = capture_key or capture.relative_to(CAPTURE_REPO_ROOT).as_posix()
    capture_id = Path(capture_key).name
    raw_records, source_summary = load_packet_records(
        capture, retain_record=retain_combat_evidence_record
    )
    session = {
        "capture": capture_key,
        "captureId": capture_id,
        "capabilityStatus": source_summary["capabilityStatus"],
        "canonicalValid": source_summary["canonicalValid"],
        "recaptureRequired": source_summary["recaptureRequired"],
        "captureComplete": source_summary["captureComplete"],
        "positiveEvidenceOnly": source_summary["positiveEvidenceOnly"],
        "absenceInferenceAllowed": source_summary["absenceInferenceAllowed"],
        "canonicalPackets": source_summary["canonicalPackets"],
        "conflictCount": source_summary["conflictCount"],
        "packetLog": source_summary["packetLog"],
        "rawPacketIndex": source_summary["rawPacketIndex"],
    }
    if not source_summary["canonicalValid"]:
        return [], [], session, []

    canonical_by_sequence = {
        (row["direction"], row["sequence"]): row for row in raw_records
    }
    metadata, metadata_errors = load_metadata_generations(
        capture, capture_key, canonical_by_sequence
    )
    parsed: list[PacketRecord] = []
    errors = list(metadata_errors)
    for raw_record in raw_records:
        if raw_record["direction"] != "IN":
            continue
        packet_hex = raw_record["rawHex"].upper()
        try:
            packet = bytes.fromhex(packet_hex)
            if len(packet) < 20:
                continue
            message_numeric = struct.unpack_from(">I", packet, 16)[0]
            message_type = MESSAGE_TYPES.get(message_numeric)
            if message_type not in COMBAT_MESSAGE_TYPES:
                continue
            frame_length = struct.unpack_from(">H", packet, 6)[0]
            if frame_length != len(packet):
                raise DecodeError(
                    f"transport frame length {frame_length} does not match {len(packet)}"
                )
            decoder = DECODERS.get(message_type)
            if decoder is None:
                decoded = {
                    "unsupported": True,
                    "reason": f"{message_type} is retained but not decoded by the runtime contract model",
                }
            else:
                decoded = decoder(packet[16:])
            if message_type == "WeaponItemFullUpdate" and not decoded.get("npcOwnerLinked"):
                continue
        except (ValueError, DecodeError, struct.error) as exc:
            errors.append(
                {
                    "capture": capture_key,
                    "artifact": raw_record["source"],
                    "direction": raw_record["direction"],
                    "sequence": raw_record["sequence"],
                    "globalOrdinal": raw_record["globalOrdinal"],
                    "messageType": MESSAGE_TYPES.get(message_numeric, hex(message_numeric)),
                    "error": str(exc),
                }
            )
            continue
        body = packet[16:]
        packet_hash = hashlib.sha256(packet).hexdigest()
        packet_id = (
            f"{capture_key}|{raw_record['direction']}|"
            f"{raw_record['sequence']}|{packet_hash[:12]}"
        )
        parsed.append(
            PacketRecord(
                packet_id=packet_id,
                capture=capture_key,
                capture_id=capture_id,
                captured_utc=raw_record["timestamp"],
                direction="IN",
                sequence=raw_record["sequence"],
                global_ordinal=raw_record["globalOrdinal"],
                message_type=message_type,
                packet_hex=packet_hex,
                body_hex=body.hex().upper(),
                packet_sha256=packet_hash,
                body_sha256=hashlib.sha256(body).hexdigest(),
                canonical_source=raw_record["source"],
                decoded=decoded,
            )
        )
    return parsed, metadata, session, errors


def _portable_capture_session(
    session: dict[str, Any],
    capture_source_root: Path | None = None,
) -> dict[str, Any]:
    capture_key = session.get("capture")
    if not isinstance(capture_key, str) or not capture_key:
        raise RuntimeError("capture session has no canonical logical capture key")
    result = dict(session)
    for member_name, source_name in (
        ("packetLog", "packets.hex.log"),
        ("rawPacketIndex", "raw-packets.csv"),
    ):
        member = result.get(member_name)
        if member is None:
            continue
        if not isinstance(member, dict):
            raise RuntimeError(f"capture session {member_name} is not an object")
        portable = dict(member)
        source_path = portable.get("path")
        if not isinstance(source_path, str) or not source_path:
            raise RuntimeError(f"capture session {member_name} path is invalid")
        unresolved = Path(source_path)
        _reject_symlink_or_reparse(
            unresolved,
            f"capture session {member_name}",
            include_parent=True,
        )
        expected = (Path(capture_key) / source_name).as_posix()
        if unresolved.name != source_name:
            raise RuntimeError(
                f"capture session {member_name} path does not match {expected}"
            )
        if capture_source_root is not None:
            expected_physical = (capture_source_root / source_name).resolve()
            if unresolved.resolve() != expected_physical:
                raise RuntimeError(
                    f"capture session {member_name} path escaped its frozen capture"
                )
        portable["path"] = expected
        result[member_name] = portable
    return result


def _rehydrate_capture_session(
    session: dict[str, Any], capture_key: str
) -> dict[str, Any]:
    result = dict(session)
    for member_name, source_name in (
        ("packetLog", "packets.hex.log"),
        ("rawPacketIndex", "raw-packets.csv"),
    ):
        member = result.get(member_name)
        if member is None:
            continue
        if not isinstance(member, dict):
            raise RuntimeError(f"capture shard {member_name} is not an object")
        portable = dict(member)
        expected = (Path(capture_key) / source_name).as_posix()
        if portable.get("path") != expected:
            raise RuntimeError(
                f"capture shard {member_name} path is not canonical"
            )
        portable["path"] = str(CAPTURE_REPO_ROOT / Path(expected))
        result[member_name] = portable
    return result


def _parse_capture_payload(
    result: tuple[
        list[PacketRecord],
        list[MetadataGeneration],
        dict[str, Any],
        list[dict[str, Any]],
    ],
    capture_source_root: Path | None = None,
) -> dict[str, Any]:
    records, metadata, session, errors = result
    portable_session = _portable_capture_session(session, capture_source_root)
    metadata_rows = [
        {
            "capture": value.capture,
            "capture_id": value.capture_id,
            "sequence": value.sequence,
            "global_ordinal": value.global_ordinal,
            "source": value.source,
            "name": value.name,
            "monster_data": value.monster_data,
            "level": value.level,
            "captured_realm_id": value.captured_realm_id,
            "projection": value.projection,
            "packet_sha256": value.packet_sha256,
            "scfu_special_attacks": value.scfu_special_attacks,
            "owner_identity": value.owner_identity,
        }
        for value in metadata
    ]
    record_rows = []
    for value in records:
        linked_metadata = value.metadata
        record_rows.append(
            {
                "packet_id": value.packet_id,
                "capture": value.capture,
                "capture_id": value.capture_id,
                "captured_utc": value.captured_utc,
                "direction": value.direction,
                "sequence": value.sequence,
                "global_ordinal": value.global_ordinal,
                "message_type": value.message_type,
                "packet_hex": value.packet_hex,
                "body_hex": value.body_hex,
                "packet_sha256": value.packet_sha256,
                "body_sha256": value.body_sha256,
                "packet_sha256_base64": sha256_hex_to_base64(
                    value.packet_sha256
                ),
                "body_sha256_base64": sha256_hex_to_base64(
                    value.body_sha256
                ),
                "decoded_sha256_base64": sha256_hex_to_base64(
                    sha256_canonical(value.decoded)
                ),
                "canonical_source": value.canonical_source,
                "decoded": value.decoded,
                "metadata": None
                if linked_metadata is None
                else {
                    "capture": linked_metadata.capture,
                    "capture_id": linked_metadata.capture_id,
                    "sequence": linked_metadata.sequence,
                    "global_ordinal": linked_metadata.global_ordinal,
                    "source": linked_metadata.source,
                    "name": linked_metadata.name,
                    "monster_data": linked_metadata.monster_data,
                    "level": linked_metadata.level,
                    "captured_realm_id": linked_metadata.captured_realm_id,
                    "projection": linked_metadata.projection,
                    "packet_sha256": linked_metadata.packet_sha256,
                    "scfu_special_attacks": linked_metadata.scfu_special_attacks,
                    "owner_identity": linked_metadata.owner_identity,
                },
                "metadata_resolution": value.metadata_resolution,
            }
        )
    return {
        "schemaVersion": CAPTURE_SNAPSHOT_SCHEMA_VERSION,
        "capture": portable_session["capture"],
        "records": record_rows,
        "metadata": metadata_rows,
        "session": portable_session,
        "errors": errors,
    }


def _parse_capture_result(
    payload: dict[str, Any],
) -> tuple[list[PacketRecord], list[MetadataGeneration], dict[str, Any], list[dict[str, Any]]]:
    try:
        if payload["schemaVersion"] != CAPTURE_SNAPSHOT_SCHEMA_VERSION:
            raise ValueError("capture shard schema version is unsupported")
        capture_key = payload["capture"]
        record_rows = payload["records"]
        metadata_rows = payload["metadata"]
        session = payload["session"]
        errors = payload["errors"]
        if not all(
            isinstance(value, expected)
            for value, expected in (
                (record_rows, list),
                (metadata_rows, list),
                (session, dict),
                (errors, list),
            )
        ):
            raise TypeError("capture shard members have invalid types")
        if not isinstance(capture_key, str) or not capture_key:
            raise TypeError("capture shard identity is invalid")
        if session.get("capture") != capture_key:
            raise ValueError("capture shard session identity does not match its capture")
        session = _rehydrate_capture_session(session, capture_key)
        records = []
        for value in record_rows:
            row = dict(value)
            linked_metadata = row.get("metadata")
            if linked_metadata is not None:
                row["metadata"] = MetadataGeneration(**linked_metadata)
            records.append(PacketRecord(**row))
        metadata = [MetadataGeneration(**dict(value)) for value in metadata_rows]
    except (KeyError, TypeError, ValueError) as error:
        raise RuntimeError(f"invalid capture worker shard: {error}") from error
    return records, metadata, session, errors


@dataclass(frozen=True)
class CaptureShardSnapshot:
    capture: str
    path: Path
    relative_path: str
    byte_length: int
    sha256: str


def _write_parse_capture_worker_shard(
    capture: Path,
    shard: Path,
    capture_key: str,
) -> None:
    _reject_symlink_or_reparse(
        capture, "capture worker input", include_parent=True
    )
    capture = capture.resolve(strict=True)
    logical_capture = Path(capture_key)
    if (
        not capture_key
        or logical_capture.is_absolute()
        or ".." in logical_capture.parts
        or logical_capture.as_posix() != capture_key
    ):
        raise RuntimeError("capture worker logical key is invalid")
    temporary_root = Path(tempfile.gettempdir()).resolve(strict=True)
    try:
        capture.relative_to(temporary_root)
    except ValueError as error:
        raise RuntimeError(f"capture worker input is outside temporary staging: {capture}") from error

    _reject_symlink_or_reparse(shard, "capture worker shard", include_parent=True)
    shard = shard.resolve()
    try:
        shard.relative_to(temporary_root)
    except ValueError as error:
        raise RuntimeError(
            f"capture worker shards must stay under {temporary_root}"
        ) from error
    if not shard.parent.is_dir():
        raise RuntimeError(f"capture worker shard directory is missing: {shard.parent}")
    if shard in {OUTPUT.resolve(), CATALOG_OUTPUT.resolve(), FIXTURE_OUTPUT.resolve()}:
        raise RuntimeError("capture worker cannot write a production generated output")

    payload = json.JSONEncoder(
        ensure_ascii=True,
        separators=(",", ":"),
        sort_keys=True,
    ).encode(
        _parse_capture_payload(
            parse_capture(capture, capture_key),
            capture,
        )
    ).encode("utf-8")
    try:
        _write_deterministic_temporary_file(
            shard,
            payload,
            "capture worker shard",
        )
        persisted = shard.read_bytes()
        if persisted != payload:
            raise RuntimeError(
                "capture worker shard bytes changed during atomic publication"
            )
        json.loads(persisted)
    except (OSError, ValueError, RuntimeError) as error:
        raise RuntimeError(
            f"capture worker shard materialization failed: {error}"
        ) from error


def _path_is_symlink_or_reparse(path: Path) -> bool:
    if not os.path.lexists(path):
        return False
    metadata = os.lstat(path)
    return bool(
        path.is_symlink()
        or getattr(metadata, "st_file_attributes", 0) & 0x400
    )


def _reject_symlink_or_reparse(path: Path, label: str, include_parent: bool = False) -> None:
    candidates = (path, path.parent) if include_parent else (path,)
    for candidate in candidates:
        if _path_is_symlink_or_reparse(candidate):
            raise RuntimeError(f"{label} cannot be a symlink or reparse point: {candidate}")


def _generator_script_path() -> Path:
    script = Path(__file__)
    if not script.is_absolute():
        script = REPO_ROOT / script
    _reject_symlink_or_reparse(
        script, "capture aggregation generator", include_parent=True
    )
    return script.resolve(strict=True)


def _remove_owned_directory_tree(
    owner_root: Path,
    directory: Path,
    required_prefix: str,
) -> None:
    _reject_symlink_or_reparse(owner_root, "owned temporary root")
    _reject_symlink_or_reparse(directory, "owned temporary directory")
    root = owner_root.resolve(strict=True)
    candidate = directory.resolve(strict=True)
    if candidate.parent != root or not candidate.name.startswith(required_prefix):
        raise RuntimeError(f"refusing to remove unowned temporary directory: {candidate}")
    for current, directories, files in os.walk(candidate, topdown=True, followlinks=False):
        current_path = Path(current)
        for name in (*directories, *files):
            child = current_path / name
            if _path_is_symlink_or_reparse(child):
                raise RuntimeError(
                    f"refusing to remove temporary tree containing a reparse point: {child}"
                )
    shutil.rmtree(candidate)


def _is_cancellation_child_failure(return_code: int) -> bool:
    normalized = return_code & 0xFFFFFFFF
    return normalized == 0xC000013A or return_code in {
        -signal.SIGINT,
        -signal.SIGTERM,
    }


def _is_native_child_failure(return_code: int) -> bool:
    if _is_cancellation_child_failure(return_code):
        return False
    if return_code < 0:
        return True
    normalized = return_code & 0xFFFFFFFF
    return 0xC0000000 <= normalized <= 0xCFFFFFFF


def _terminate_child_process_tree(process: subprocess.Popen[str]) -> None:
    if process.poll() is not None:
        return
    if os.name == "nt":
        try:
            subprocess.run(
                ["taskkill", "/PID", str(process.pid), "/T", "/F"],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                timeout=CHILD_CLEANUP_TIMEOUT_SECONDS,
                check=False,
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
            )
        except (OSError, subprocess.TimeoutExpired):
            pass
    else:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except (OSError, ProcessLookupError):
            pass
    if process.poll() is None:
        process.kill()
    try:
        process.wait(timeout=CHILD_CLEANUP_TIMEOUT_SECONDS)
    except subprocess.TimeoutExpired as error:
        raise RuntimeError(
            f"child process {process.pid} did not terminate after cleanup"
        ) from error


def _run_bounded_child(
    command: list[str],
    *,
    cwd: Path,
    timeout_seconds: int,
    label: str,
    environment: dict[str, str] | None = None,
) -> subprocess.CompletedProcess[str]:
    creationflags = (
        getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0)
        if os.name == "nt"
        else 0
    )
    process = subprocess.Popen(
        command,
        cwd=cwd,
        env=environment,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        errors="replace",
        creationflags=creationflags,
        start_new_session=os.name != "nt",
    )
    try:
        stdout, stderr = process.communicate(timeout=timeout_seconds)
    except subprocess.TimeoutExpired as error:
        _terminate_child_process_tree(process)
        stdout, stderr = process.communicate()
        completed = subprocess.CompletedProcess(
            command,
            process.returncode if process.returncode is not None else -1,
            stdout,
            stderr,
        )
        detail = _capture_worker_failure_detail(completed)
        suffix = f": {detail}" if detail else ""
        raise BoundedChildTimeout(
            f"{label} timed out after {timeout_seconds} seconds{suffix}"
        ) from error
    except BaseException:
        _terminate_child_process_tree(process)
        raise
    return subprocess.CompletedProcess(
        command,
        process.returncode,
        stdout,
        stderr,
    )


def _capture_worker_failure_detail(completed: subprocess.CompletedProcess[str]) -> str:
    detail = (completed.stderr or completed.stdout or "").strip()
    if len(detail) > 2000:
        detail = detail[-2000:]
    return detail


def _is_interpreter_corruption_detail(detail: str) -> bool:
    return (
        "Windows fatal exception: access violation" in detail
        or "TypeError: 'str_ascii_iterator' object is not callable" in detail
        or "SystemError:" in detail
        or (
            "AttributeError: 'datetime.timezone' object has no attribute 'astimezone'"
            in detail
        )
        or "AttributeError: 'function' object has no attribute 'strip'" in detail
        or (
            "decode_npc_lifecycle_capture.py" in detail
            and ("TypeError:" in detail or "AttributeError:" in detail)
        )
        or (
            "ValueError: invalid literal for int() with base 10:" in detail
            and ("json\\decoder.py" in detail or "json/decoder.py" in detail)
        )
        or (
            ("TypeError:" in detail or "AttributeError:" in detail)
            and ("json\\decoder.py" in detail or "json/decoder.py" in detail)
            and ("json\\scanner.py" in detail or "json/scanner.py" in detail)
        )
        or (
            "extract_capture_backed_npc_combat.py" in detail
            and "TypeError:" in detail
            and ("parse_capture" in detail or "decode_" in detail)
        )
        or (
            "TypeError:" in detail
            and "object is not iterable" in detail
            and "context_candidates" in detail
        )
    )


def _is_capture_worker_materialization_detail(detail: str) -> bool:
    return (
        "capture worker shard materialization failed:" in detail
        or "capture aggregation snapshot materialization failed " in detail
    )


def _capture_snapshot_failure(
    capture: str,
    path: Path | str,
    phase: str,
    snapshot_identity: str,
    reason: str,
) -> CaptureAggregationInvariantError:
    return CaptureAggregationInvariantError(
        "capture aggregation snapshot invariant failed "
        f"capture={capture} path={path} phase={phase} "
        f"snapshot={snapshot_identity}: {reason}"
    )


def _capture_snapshot_materialization_failure(
    capture: str,
    path: Path | str,
    phase: str,
    snapshot_identity: str,
    reason: str,
) -> CaptureShardMaterializationError:
    return CaptureShardMaterializationError(
        "capture aggregation snapshot materialization failed "
        f"capture={capture} path={path} phase={phase} "
        f"snapshot={snapshot_identity}: {reason}"
    )


def _read_capture_snapshot_shard_bytes(
    snapshot: CaptureShardSnapshot,
    phase: str,
    snapshot_identity: str,
) -> bytes:
    path = snapshot.path
    try:
        _reject_symlink_or_reparse(path, "snapshot shard", include_parent=True)
        if not path.is_file():
            raise RuntimeError("snapshot shard is not a regular file")
        payload = path.read_bytes()
        actual_length = len(payload)
        actual_sha256 = hashlib.sha256(payload).hexdigest()
        if actual_length != snapshot.byte_length or actual_sha256 != snapshot.sha256:
            raise RuntimeError(
                "snapshot shard identity changed "
                f"expectedLength={snapshot.byte_length} actualLength={actual_length} "
                f"expectedSha256={snapshot.sha256} actualSha256={actual_sha256}"
            )
        return payload
    except (OSError, RuntimeError) as error:
        raise _capture_snapshot_materialization_failure(
            snapshot.capture,
            path,
            phase,
            snapshot_identity,
            str(error),
        ) from error


def _verify_capture_snapshot_shard_identity(
    snapshot: CaptureShardSnapshot,
    phase: str,
    snapshot_identity: str,
) -> None:
    _read_capture_snapshot_shard_bytes(snapshot, phase, snapshot_identity)


def _load_capture_snapshot_shard(
    snapshot: CaptureShardSnapshot,
    phase: str,
    snapshot_identity: str,
) -> tuple[list[PacketRecord], list[MetadataGeneration], dict[str, Any], list[dict[str, Any]]]:
    path = snapshot.path
    payload_bytes = _read_capture_snapshot_shard_bytes(
        snapshot, phase, snapshot_identity
    )
    try:
        payload = json.loads(payload_bytes)
    except ValueError as error:
        raise _capture_snapshot_materialization_failure(
            snapshot.capture,
            path,
            phase,
            snapshot_identity,
            str(error),
        ) from error
    try:
        result = _parse_capture_result(payload)
        records, metadata, session, _ = result
        if session.get("capture") != snapshot.capture:
            raise RuntimeError(
                "snapshot shard capture identity changed "
                f"actual={session.get('capture')}"
            )
        if any(record.capture != snapshot.capture for record in records):
            raise RuntimeError("snapshot shard contains a cross-capture packet")
        if any(generation.capture != snapshot.capture for generation in metadata):
            raise RuntimeError("snapshot shard contains cross-capture metadata")
        return result
    except (OSError, RuntimeError) as error:
        raise _capture_snapshot_failure(
            snapshot.capture,
            path,
            phase,
            snapshot_identity,
            str(error),
        ) from error


def _capture_source_descriptors_by_name(
    source_entry: dict[str, Any],
) -> dict[str, dict[str, Any]]:
    capture_key = source_entry.get("capture")
    source_files = source_entry.get("sourceFiles")
    if not isinstance(capture_key, str) or not isinstance(source_files, list):
        raise CaptureAggregationInvariantError("capture source entry is malformed")
    result: dict[str, dict[str, Any]] = {}
    for descriptor in source_files:
        if not isinstance(descriptor, dict):
            raise CaptureAggregationInvariantError("capture source descriptor is malformed")
        logical_path = descriptor.get("path")
        if not isinstance(logical_path, str):
            raise CaptureAggregationInvariantError("capture source path is malformed")
        relative = Path(logical_path)
        expected_parent = Path(capture_key)
        if relative.parent != expected_parent or relative.name not in CAPTURE_SNAPSHOT_SOURCE_NAMES:
            raise CaptureAggregationInvariantError(
                f"capture source path is not canonical: {logical_path}"
            )
        if relative.name in result:
            raise CaptureAggregationInvariantError(
                f"capture source name is duplicated: {relative.name}"
            )
        result[relative.name] = descriptor
    if set(result) != set(CAPTURE_SNAPSHOT_SOURCE_NAMES):
        raise CaptureAggregationInvariantError("capture source file set is incomplete")
    return result


def _verify_frozen_capture_inputs(
    frozen_capture: Path,
    source_entry: dict[str, Any],
    phase: str,
    snapshot_identity: str,
) -> None:
    descriptors = _capture_source_descriptors_by_name(source_entry)
    for name in sorted(CAPTURE_SNAPSHOT_SOURCE_NAMES):
        descriptor = descriptors[name]
        _verify_snapshot_file_identity(
            frozen_capture / name,
            descriptor,
            str(source_entry["capture"]),
            phase,
            snapshot_identity,
            verify_hash=True,
        )


def _freeze_capture_inputs(
    snapshot_directory: Path,
    ordinal: int,
    source_entry: dict[str, Any],
    snapshot_identity: str,
) -> Path:
    _verify_capture_source_entry(
        source_entry,
        "capture-freeze-preflight",
        snapshot_identity,
        verify_hash=True,
    )
    frozen_capture = Path(tempfile.mkdtemp(
        prefix=f"frozen-capture-{ordinal:06d}-",
        dir=snapshot_directory,
    ))
    descriptors = _capture_source_descriptors_by_name(source_entry)
    for name in sorted(CAPTURE_SNAPSHOT_SOURCE_NAMES):
        descriptor = descriptors[name]
        if not descriptor["exists"]:
            continue
        source = CAPTURE_REPO_ROOT / Path(descriptor["path"])
        _reject_symlink_or_reparse(source, "capture source", include_parent=True)
        destination = frozen_capture / name
        with source.open("rb") as reader, destination.open("xb") as writer:
            shutil.copyfileobj(reader, writer, 1024 * 1024)
            writer.flush()
            os.fsync(writer.fileno())
        copied = _snapshot_file_identity(destination, descriptor["path"])
        if copied != descriptor:
            raise _capture_snapshot_failure(
                str(source_entry["capture"]),
                descriptor["path"],
                "capture-freeze-copy",
                snapshot_identity,
                "frozen input copy does not match the initial plan",
            )
    _verify_frozen_capture_inputs(
        frozen_capture,
        source_entry,
        "capture-freeze-complete",
        snapshot_identity,
    )
    _invoke_self_test_fault(
        "after_capture_freeze",
        {
            "capture": source_entry["capture"],
            "directory": str(frozen_capture),
        },
    )
    return frozen_capture


def parse_capture_isolated_to_snapshot(
    capture: Path,
    snapshot_directory: Path,
    ordinal: int,
    source_entry: dict[str, Any],
    snapshot_identity: str,
) -> CaptureShardSnapshot:
    _reject_symlink_or_reparse(
        capture, "capture source directory", include_parent=True
    )
    capture = capture.resolve(strict=True)
    capture_key = capture.relative_to(CAPTURE_REPO_ROOT).as_posix()
    if source_entry.get("capture") != capture_key:
        raise _capture_snapshot_failure(
            capture_key,
            capture,
            "capture-worker-prepare",
            snapshot_identity,
            "logical capture key does not match the source plan",
        )
    _reject_symlink_or_reparse(
        snapshot_directory, "capture snapshot directory"
    )
    snapshot_directory = snapshot_directory.resolve(strict=True)
    final_shard = snapshot_directory / f"capture-{ordinal:06d}.json"
    if os.path.lexists(final_shard):
        raise _capture_snapshot_failure(
            capture_key,
            final_shard,
            "capture-worker-prepare",
            snapshot_identity,
            "immutable snapshot shard target already exists",
        )
    frozen_capture = _freeze_capture_inputs(
        snapshot_directory,
        ordinal,
        source_entry,
        snapshot_identity,
    )
    for attempt in range(1, CAPTURE_WORKER_MAX_ATTEMPTS + 1):
        _verify_frozen_capture_inputs(
            frozen_capture,
            source_entry,
            f"capture-worker-attempt-{attempt}-preflight",
            snapshot_identity,
        )
        shard = snapshot_directory / (
            f"capture-{ordinal:06d}.attempt-{attempt}.json"
        )
        if os.path.lexists(shard):
            _reject_symlink_or_reparse(shard, "capture worker shard")
            shard.unlink()
        command = [
            sys.executable,
            "-B",
            "-I",
            "-X",
            "faulthandler",
            str(_generator_script_path()),
            "--_parse-capture-worker",
            str(frozen_capture),
            "--_parse-capture-key",
            capture_key,
            "--_parse-capture-shard",
            str(shard),
        ]
        _invoke_self_test_fault(
            "before_capture_worker_attempt",
            {"capture": capture_key, "attempt": attempt},
        )
        try:
            completed = _run_bounded_child(
                command,
                cwd=REPO_ROOT,
                timeout_seconds=CAPTURE_WORKER_TIMEOUT_SECONDS,
                label=f"capture worker {capture_key} attempt {attempt}",
            )
        except BoundedChildTimeout as error:
            raise _capture_snapshot_failure(
                capture_key,
                shard,
                f"capture-worker-attempt-{attempt}",
                snapshot_identity,
                str(error),
            ) from error
        if completed.returncode == 0:
            try:
                if not shard.is_file():
                    raise CaptureShardMaterializationError(
                        "capture worker succeeded without writing its shard"
                    )
                try:
                    sha256, byte_length = _stream_file_sha256_and_length(shard)
                except OSError as error:
                    raise CaptureShardMaterializationError(str(error)) from error
                candidate = CaptureShardSnapshot(
                    capture=capture_key,
                    path=shard,
                    relative_path=(
                        f"{CAPTURE_SNAPSHOT_DIRECTORY_NAME}/capture-{ordinal:06d}.json"
                    ),
                    byte_length=byte_length,
                    sha256=sha256,
                )
                _load_capture_snapshot_shard(
                    candidate,
                    "capture-worker-result",
                    snapshot_identity,
                )
            except CaptureShardMaterializationError as error:
                _verify_frozen_capture_inputs(
                    frozen_capture,
                    source_entry,
                    "capture-worker-retry-frozen-input",
                    snapshot_identity,
                )
                _verify_capture_source_entry(
                    source_entry,
                    "capture-worker-retry-live-input",
                    snapshot_identity,
                    verify_hash=True,
                )
                if os.path.lexists(shard):
                    _reject_symlink_or_reparse(shard, "capture worker shard")
                    shard.unlink()
                if attempt < CAPTURE_WORKER_MAX_ATTEMPTS:
                    continue
                raise _capture_snapshot_failure(
                    capture_key,
                    shard,
                    "capture-worker-result",
                    snapshot_identity,
                    "capture worker shard materialization failed on attempt "
                    f"{attempt}/{CAPTURE_WORKER_MAX_ATTEMPTS}: {error}",
                ) from error
            _verify_frozen_capture_inputs(
                frozen_capture,
                source_entry,
                "capture-worker-result-frozen-input",
                snapshot_identity,
            )
            _verify_capture_source_entry(
                source_entry,
                "capture-worker-postflight",
                snapshot_identity,
                verify_hash=True,
            )
            os.replace(shard, final_shard)
            result = CaptureShardSnapshot(
                capture=capture_key,
                path=final_shard,
                relative_path=candidate.relative_path,
                byte_length=byte_length,
                sha256=sha256,
            )
            _invoke_self_test_fault(
                "before_frozen_capture_cleanup",
                {"capture": capture_key, "directory": str(frozen_capture)},
            )
            _remove_owned_directory_tree(
                snapshot_directory,
                frozen_capture,
                f"frozen-capture-{ordinal:06d}-",
            )
            return result

        detail = _capture_worker_failure_detail(completed)
        cancelled = _is_cancellation_child_failure(completed.returncode)
        materialization_failure = _is_capture_worker_materialization_detail(detail)
        retryable = not cancelled and (
            _is_native_child_failure(completed.returncode)
            or _is_interpreter_corruption_detail(detail)
            or materialization_failure
        )
        if retryable and attempt < CAPTURE_WORKER_MAX_ATTEMPTS:
            continue
        if cancelled:
            kind = "cancelled capture worker"
        elif materialization_failure:
            kind = "materialization capture worker"
        elif retryable:
            kind = "native capture worker"
        else:
            kind = "deterministic capture worker"
        suffix = f": {detail}" if detail else ""
        raise _capture_snapshot_failure(
            capture_key,
            shard,
            f"capture-worker-attempt-{attempt}",
            snapshot_identity,
            f"{kind} failed with exit code {completed.returncode} "
            f"on attempt {attempt}/{CAPTURE_WORKER_MAX_ATTEMPTS}{suffix}",
        )
    raise AssertionError("capture worker retry loop exited unexpectedly")


def choose_metadata(
    record: PacketRecord,
    local_by_capture_source: dict[tuple[str, int], list[MetadataGeneration]],
    corpus_by_source: dict[int, list[MetadataGeneration]],
) -> tuple[MetadataGeneration | None, str]:
    source = record.source
    if source is None:
        return None, "source-identity-missing"
    local = local_by_capture_source.get((record.capture, source), [])
    before = [row for row in local if row.sequence <= record.sequence]
    if before:
        chosen = max(before, key=lambda row: row.sequence)
        return chosen, "capture-local-generation"
    local_profiles = {row.profile for row in local}
    if len(local_profiles) == 1:
        return min(local, key=lambda row: row.sequence), "capture-local-previsibility-stitch"
    if len(local_profiles) > 1:
        return None, "capture-local-metadata-conflict"
    corpus = corpus_by_source.get(source, [])
    profiles = {row.profile for row in corpus}
    if len(profiles) == 1:
        return min(corpus, key=lambda row: (row.capture, row.sequence)), "corpus-exact-identity-stitch"
    if len(profiles) > 1:
        return None, "corpus-source-identity-reuse-conflict"
    return None, "raw-SCFU-metadata-missing"


def packet_sort_key(record: PacketRecord) -> tuple[Any, ...]:
    return (
        record.time is None,
        record.time or dt.datetime.max.replace(tzinfo=dt.timezone.utc),
        record.capture,
        record.global_ordinal is None,
        record.global_ordinal if record.global_ordinal is not None else record.sequence,
    )


def saw_signature_from_decoded(
    value: dict[str, Any], excluded_field_names: frozenset[str] = frozenset()
) -> dict[str, Any]:
    result = {
        "n3Unknown": value["n3Unknown"],
        "specials": [
            {
                "lowTemplate": row["lowTemplate"],
                "highTemplate": row["highTemplate"],
                "tag": row["tag"],
                "nameHex": row["nameHex"],
            }
            for row in value["specials"]
        ],
        "unknown1": value["unknown1"],
        "unknown2": value["unknown2"],
        "unknown3": value["unknown3"],
        "unknown4": value["unknown4"],
        "unknown5": value["unknown5"],
    }
    for field_name in excluded_field_names:
        result.pop(field_name, None)
    return result


def saw_signature(record: PacketRecord) -> dict[str, Any]:
    return saw_signature_from_decoded(record.decoded)


def invariant_saw_signature(record: PacketRecord) -> dict[str, Any]:
    return saw_signature_from_decoded(record.decoded, MUTABLE_SAW_FIELD_NAMES)


def attack_signature_from_decoded(value: dict[str, Any]) -> dict[str, Any]:
    return {
        "n3Unknown": value["n3Unknown"],
        "action": value["action"],
    }


def attack_signature(record: PacketRecord) -> dict[str, Any]:
    return attack_signature_from_decoded(record.decoded)


def wifu_signature_from_decoded(
    value: dict[str, Any], excluded_stat_ids: frozenset[int] = frozenset()
) -> dict[str, Any]:
    stats = []
    for row in value["stats"]:
        if row["stat"] in excluded_stat_ids:
            continue
        stats.append(
            {
                "stat": row["stat"],
                "value": row["rawValue"],
            }
        )
    return {
        "n3Unknown": value["n3Unknown"],
        "unknown1": value["unknown1"],
        "inventorySlot": value["inventorySlot"],
        "stateMachineType": value["stateMachine"]["type"],
        "stateMachineInstance": value["stateMachine"]["instance"],
        "unknown2": value["unknown2"],
        "stats": stats,
        "unknown3": value["unknown3"],
    }


def wifu_signature(record: PacketRecord) -> dict[str, Any]:
    return wifu_signature_from_decoded(record.decoded)


def invariant_wifu_signature(record: PacketRecord) -> dict[str, Any]:
    return wifu_signature_from_decoded(record.decoded, MUTABLE_WIFU_STAT_IDS)


def attack_info_stream_signature(record: PacketRecord) -> dict[str, Any]:
    value = record.decoded
    ammo_mode = "unlimited" if value["ammo"] == -1 else "finite-mutable"
    return {
        "n3Unknown": value["n3Unknown"],
        "weaponSlot": value["weaponSlot"],
        "damageTypeWire": value["damageTypeWire"],
        "hitTypeWire": value["hitTypeWire"],
        "weaponInstance": value["weaponInstance"],
        "ammoMode": ammo_mode,
    }


RESOURCE_BY_CAPTURED_REALM = {
    477565: 1931,
    478032: 6553,
    1187842: 127,
    1363982: 127,
    1388552: 127,
    1407006: 127,
    1419333: 127,
    938000: 1931,
    1044525: 6553,
    1277953: 6553,
    1304579: 6553,
    655: 655,
}

RESOURCE_MAPPING_PROVENANCE = {
    655: {
        "captureEvidenceSessions": (
            "tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260510-030142",
        ),
        "runtimeBindingSource": "AORebirth/Server/ZoneEngine/Core/Playfields/Content/AndromedaIccHqContentModule.cs",
        "runtimeBindingLiteral": "private const int AndromedaPlayfieldInstance = 655;",
        "mappingBasis": "captured realm and server runtime content resource are both ICC HQ Andromeda 655",
    },
    477565: {
        "captureEvidenceSessions": (
            "tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260528-190456",
        ),
        "runtimeBindingSource": "AORebirth/Server/ZoneEngine/Core/Playfields/Content/TempleOfThreeWindsContentModule.cs",
        "runtimeBindingLiteral": "private const int TempleOfThreeWindsPlayfieldInstance = 1931;",
        "mappingBasis": "captured Temple population is bound by the dedicated Temple content module",
    },
    478032: {
        "captureEvidenceSessions": (
            "tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-202500",
        ),
        "runtimeBindingSource": "AORebirth/Server/ZoneEngine/Core/Playfields/Content/AreteContentModule.cs",
        "runtimeBindingLiteral": "private const int PrivateAretePlayfieldInstance = 6553;",
        "mappingBasis": "captured Arete population is bound by the dedicated Arete content module",
    },
    938000: {
        "captureEvidenceSessions": (
            "tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-033006",
        ),
        "runtimeBindingSource": "AORebirth/Server/ZoneEngine/Core/Playfields/Content/TempleOfThreeWindsContentModule.cs",
        "runtimeBindingLiteral": "private const int TempleOfThreeWindsPlayfieldInstance = 1931;",
        "mappingBasis": "docs/evidence/TEMPLE_OF_THREE_WINDS_20260721_ENTRANCE_TO_FIRST_BOSS.md records official-live realm 938000 as Temple resource 1931",
    },
    1044525: {
        "captureEvidenceSessions": (
            "tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260618-075746",
        ),
        "runtimeBindingSource": "AORebirth/Server/ZoneEngine/Core/Playfields/Content/AreteContentModule.cs",
        "runtimeBindingLiteral": "private const int PrivateAretePlayfieldInstance = 6553;",
        "mappingBasis": "captured Arete population is bound by the dedicated Arete content module",
    },
    1187842: {
        "captureEvidenceSessions": (
            "tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260708-143600",
        ),
        "runtimeBindingSource": "AORebirth/Server/ZoneEngine/Core/Playfields/Content/SubwayContentModule.cs",
        "runtimeBindingLiteral": "private const int SubwayPlayfieldInstance = 127;",
        "mappingBasis": "docs/project/PROJECT_STATE.md records official-live realm 1187842 as Subway content resource 127",
    },
    1277953: {
        "captureEvidenceSessions": (
            "tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260629-142800",
        ),
        "runtimeBindingSource": "AORebirth/Server/ZoneEngine/Core/Playfields/Content/AreteContentModule.cs",
        "runtimeBindingLiteral": "private const int PrivateAretePlayfieldInstance = 6553;",
        "mappingBasis": "captured Arete population is bound by the dedicated Arete content module",
    },
    1304579: {
        "captureEvidenceSessions": (
            "tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260629-193121",
        ),
        "runtimeBindingSource": "AORebirth/Server/ZoneEngine/Core/Playfields/Content/AreteContentModule.cs",
        "runtimeBindingLiteral": "private const int PrivateAretePlayfieldInstance = 6553;",
        "mappingBasis": "captured Arete robot population is bound by the dedicated Arete content module",
    },
    1363982: {
        "captureEvidenceSessions": (
            "tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260710-202553",
        ),
        "runtimeBindingSource": "AORebirth/Server/ZoneEngine/Core/Playfields/Content/SubwayContentModule.cs",
        "runtimeBindingLiteral": "private const int SubwayPlayfieldInstance = 127;",
        "mappingBasis": "captured Subway population is bound by the dedicated Subway content module",
    },
    1388552: {
        "captureEvidenceSessions": (
            "tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260712-154941",
        ),
        "runtimeBindingSource": "AORebirth/Server/ZoneEngine/Core/Playfields/Content/SubwayContentModule.cs",
        "runtimeBindingLiteral": "private const int SubwayPlayfieldInstance = 127;",
        "mappingBasis": "captured Subway population is bound by the dedicated Subway content module",
    },
    1407006: {
        "captureEvidenceSessions": (
            "tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260716-034104",
        ),
        "runtimeBindingSource": "AORebirth/Server/ZoneEngine/Core/Playfields/Content/SubwayContentModule.cs",
        "runtimeBindingLiteral": "private const int SubwayPlayfieldInstance = 127;",
        "mappingBasis": "captured Subway population is bound by the dedicated Subway content module",
    },
    1419333: {
        "captureEvidenceSessions": (
            "tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260719-021022",
        ),
        "runtimeBindingSource": "AORebirth/Server/ZoneEngine/Core/Playfields/Content/SubwayContentModule.cs",
        "runtimeBindingLiteral": "private const int SubwayPlayfieldInstance = 127;",
        "mappingBasis": "captured Subway population is bound by the dedicated Subway content module",
    },
}


def runtime_resource(metadata: MetadataGeneration | None) -> int | None:
    if metadata is None or metadata.captured_realm_id is None:
        return None
    return RESOURCE_BY_CAPTURED_REALM.get(metadata.captured_realm_id)


def record_is_before(left: PacketRecord, right: PacketRecord) -> bool:
    if left.capture == right.capture:
        left_order = left.global_ordinal if left.global_ordinal is not None else left.sequence
        right_order = right.global_ordinal if right.global_ordinal is not None else right.sequence
        return left_order < right_order
    if left.time is not None and right.time is not None:
        return left.time < right.time
    return left.capture < right.capture


def context_candidates(
    records: list[PacketRecord],
    source: int,
    profile: tuple[str, int, int],
    before: PacketRecord,
) -> list[PacketRecord]:
    generation_key = (
        before.metadata.generation_key if before.metadata is not None else None
    )
    return [
        row
        for row in records
        if row.capture == before.capture
        and row.source == source
        and row.metadata is not None
        and row.metadata.profile == profile
        and row.metadata.generation_key == generation_key
        and record_is_before(row, before)
    ]


def choose_unique_context(
    candidates: list[PacketRecord],
    signature,
) -> tuple[PacketRecord | None, str, list[str]]:
    if not candidates:
        return None, "missing", []
    grouped: dict[str, list[PacketRecord]] = defaultdict(list)
    for row in candidates:
        grouped[digest(signature(row), 64)].append(row)
    if len(grouped) != 1:
        return None, "conflicting", sorted(grouped)
    rows = next(iter(grouped.values()))
    return max(rows, key=packet_sort_key), "unique", []


def choose_latest_context(
    candidates: list[PacketRecord],
) -> tuple[PacketRecord | None, str, list[str]]:
    if not candidates:
        return None, "missing", []
    return max(candidates, key=packet_sort_key), "latest-same-generation", []


def has_boundary(
    all_records: list[PacketRecord],
    attack: PacketRecord,
    attack_info: PacketRecord,
) -> str:
    source = attack.source
    for row in all_records:
        if row.capture != attack.capture or row.sequence <= attack.sequence or row.sequence >= attack_info.sequence:
            continue
        if row.source != source:
            continue
        if row.message_type in {"StopFight", "Despawn"}:
            return row.message_type
    return ""


def resolve_attack(
    attacks: list[PacketRecord],
    all_records: list[PacketRecord],
    attack_info: PacketRecord,
) -> tuple[PacketRecord | None, str]:
    candidates = [
        row
        for row in attacks
        if row.capture == attack_info.capture
        and row.source == attack_info.source
        and row.target == attack_info.target
        and row.sequence < attack_info.sequence
        and row.metadata is not None
        and attack_info.metadata is not None
        and row.metadata.generation_key == attack_info.metadata.generation_key
    ]
    candidates.sort(key=lambda row: row.sequence, reverse=True)
    for candidate in candidates:
        boundary = has_boundary(all_records, candidate, attack_info)
        if not boundary:
            return candidate, "capture-local-fight"
    return None, "Attack missing inside the current fight boundary"


def weapon_context_kind(
    attack_info: PacketRecord,
    saw: PacketRecord | None,
    wifu: PacketRecord | None,
) -> tuple[str, list[str]]:
    value = attack_info.decoded
    slot = value["weaponSlot"]
    instance = value["weaponInstance"]
    missing: list[str] = []
    saw_tags = set()
    if saw is not None:
        saw_tags = {row["tag"] for row in saw.decoded["specials"]}
    if instance != 0 and instance in saw_tags:
        return "natural-or-special", missing
    if slot == 0 and wifu is None:
        metadata = attack_info.metadata
        if metadata is None:
            missing.append("raw SCFU natural/special attack context")
        return "natural", missing
    if wifu is not None and wifu.decoded.get("inventorySlot") == slot:
        if not wifu.decoded.get("definitionComplete"):
            missing.extend(
                "owner-linked WIFU: " + value
                for value in wifu.decoded.get("definitionProblems", [])
            )
        return "equipped", missing
    if instance != 0:
        missing.append("SAW special tag matching AttackInfo weapon instance")
        return "unresolved-special", missing
    missing.append(f"owner-linked complete WIFU for AttackInfo slot {slot}")
    return "unresolved-equipped", missing


def chain_base_signature(
    kind: str,
    saw: PacketRecord,
    attack: PacketRecord,
    wifu: PacketRecord | None,
) -> dict[str, Any]:
    result: dict[str, Any] = {
        "weaponContextKind": kind,
        "specialAttackWeapon": saw_signature(saw),
        "attack": attack_signature(attack),
    }
    if wifu is not None:
        result["weaponItemFullUpdate"] = wifu_signature(wifu)
    return result


def chain_invariant_contract_signature(
    kind: str,
    saw: PacketRecord,
    attack: PacketRecord,
    wifu: PacketRecord | None,
) -> dict[str, Any]:
    result: dict[str, Any] = {
        "weaponContextKind": kind,
        "specialAttackWeapon": invariant_saw_signature(saw),
        "attack": attack_signature(attack),
    }
    if wifu is not None:
        result["weaponItemFullUpdate"] = invariant_wifu_signature(wifu)
    return result


def correlate(
    records: list[PacketRecord],
) -> tuple[list[dict[str, Any]], list[dict[str, Any]], list[dict[str, Any]]]:
    saws = [row for row in records if row.message_type == "SpecialAttackWeapon"]
    attacks = [row for row in records if row.message_type == "Attack"]
    wifus = [row for row in records if row.message_type == "WeaponItemFullUpdate"]
    attack_infos = [row for row in records if row.message_type == "AttackInfo"]
    complete: list[dict[str, Any]] = []
    incomplete: list[dict[str, Any]] = []
    unsupported: list[dict[str, Any]] = []
    terminal_action_max_sequence: dict[tuple[str, dt.datetime, Identity], int] = {}
    for row in records:
        if (
            row.message_type != "CharacterAction"
            or row.source is None
            or row.decoded.get("action") != 99
        ):
            continue
        action_time = row.time
        if action_time is None:
            continue
        key = (row.capture, action_time, row.source)
        terminal_action_max_sequence[key] = max(
            terminal_action_max_sequence.get(key, -1),
            row.sequence,
        )

    def is_terminal_hit(attack_info: PacketRecord) -> bool:
        if attack_info.target is None or attack_info.time is None:
            return False
        key = (attack_info.capture, attack_info.time, attack_info.target)
        return terminal_action_max_sequence.get(key, -1) > attack_info.sequence

    for record in records:
        if record.message_type in {
            "MissedAttackInfo",
            "SpecialAttackInfo",
            "CastNanoSpell",
        } or record.decoded.get("unsupported"):
            if record.message_type == "MissedAttackInfo":
                classification = "miss"
            elif record.message_type == "SpecialAttackInfo":
                classification = "special"
            elif record.message_type == "CastNanoSpell":
                classification = "nano"
            else:
                classification = "unsupported-message"
            observation = {
                "packetId": record.packet_id,
                "capture": record.capture,
                "capturedUtc": record.captured_utc,
                "messageType": record.message_type,
                "sourceIdentity": (
                    hex_identity(record.source)
                    if record.source is not None
                    else None
                ),
                "metadata": record.metadata.public() if record.metadata else None,
                "metadataResolution": record.metadata_resolution,
                "classification": classification,
                "runtimeSupport": (
                    "inventory-only; current shared NPC auto-attack runtime "
                    "does not emit this sequence"
                ),
            }
            if record.message_type == "MissedAttackInfo":
                attack_candidates = [
                    row
                    for row in attacks
                    if row.capture == record.capture
                    and row.source == record.source
                    and row.target == record.target
                    and row.sequence < record.sequence
                    and not has_boundary(records, row, record)
                ]
                attack_candidates.sort(key=lambda row: row.sequence, reverse=True)
                attack = attack_candidates[0] if attack_candidates else None
                saw_candidates = [
                    row
                    for row in saws
                    if attack is not None
                    and row.capture == attack.capture
                    and row.source == attack.source
                    and row.sequence < attack.sequence
                    and not has_boundary(records, row, attack)
                ]
                saw_candidates.sort(key=lambda row: row.sequence, reverse=True)
                saw = saw_candidates[0] if saw_candidates else None
                wifu_candidates = [
                    row
                    for row in wifus
                    if attack is not None
                    and row.capture == attack.capture
                    and row.source == attack.source
                    and row.sequence < attack.sequence
                    and not has_boundary(records, row, attack)
                ]
                wifu_candidates.sort(key=lambda row: row.sequence, reverse=True)
                wifu = wifu_candidates[0] if wifu_candidates else None
                missing_evidence = []
                if record.metadata_resolution != "capture-local-generation":
                    missing_evidence.append(
                        "same-capture preceding SCFU generation metadata"
                    )
                if attack is None:
                    missing_evidence.append(
                        "originating Attack inside the current fight boundary"
                    )
                if saw is None:
                    missing_evidence.append(
                        "SpecialAttackWeapon before originating Attack"
                    )
                if wifu is None:
                    missing_evidence.append(
                        "owner-linked WIFU or independently proven natural attack mode"
                    )
                missing_evidence.append(
                    "normal landed or critical AttackInfo semantics"
                )
                observation.update(
                    {
                        "attackerIdentity": (
                            hex_identity(record.source)
                            if record.source is not None
                            else None
                        ),
                        "defenderIdentity": (
                            hex_identity(record.target)
                            if record.target is not None
                            else None
                        ),
                        "n3SourceIdentity": hex_identity(
                            record.decoded["source"]["instance"]
                        ),
                        "n3Unknown": record.decoded["n3Unknown"],
                        "unknown1": record.decoded["unknown1"],
                        "unknown2": record.decoded["unknown2"],
                        "unknown5": record.decoded["unknown5"],
                        "missingEvidence": missing_evidence,
                        "evidenceFound": {
                            "WeaponItemFullUpdate": (
                                wifu.packet_id if wifu is not None else None
                            ),
                            "SpecialAttackWeapon": (
                                saw.packet_id if saw is not None else None
                            ),
                            "Attack": (
                                attack.packet_id if attack is not None else None
                            ),
                            "MissedAttackInfo": record.packet_id,
                        },
                        "packetOrderProven": bool(
                            saw is not None
                            and attack is not None
                            and saw.sequence < attack.sequence < record.sequence
                        ),
                        "runtimeSupport": (
                            "evidence-only miss; the packet contains no landed, "
                            "critical, terminal, damage-type, hit-type, weapon-slot, "
                            "or weapon-instance result semantics"
                        ),
                    }
                )
            unsupported.append(observation)

    for attack_info in attack_infos:
        metadata = attack_info.metadata
        missing: list[str] = []
        conflicts: list[dict[str, Any]] = []
        if metadata is None:
            missing.append(attack_info.metadata_resolution or "raw SCFU metadata")
            profile = None
        else:
            profile = metadata.profile
            if attack_info.metadata_resolution != "capture-local-generation":
                missing.append(
                    "same-capture preceding SCFU generation metadata"
                )

        attack, attack_resolution = resolve_attack(attacks, records, attack_info)
        if attack is None:
            missing.append(attack_resolution)

        saw = None
        if profile is not None and attack is not None:
            saw_candidates = context_candidates(
                saws, attack_info.source, profile, attack
            )
            saw_candidates = [
                row
                for row in saw_candidates
                if not has_boundary(records, row, attack)
            ]
            saw, resolution, variants = choose_latest_context(saw_candidates)
            if saw is None:
                missing.append(
                    "SpecialAttackWeapon before Attack"
                    if resolution == "missing"
                    else "unambiguous SpecialAttackWeapon before Attack"
                )
                if variants:
                    conflicts.append({"field": "SpecialAttackWeapon", "variantHashes": variants})

        wifu = None
        wifu_candidates: list[PacketRecord] = []
        wifu_resolution = "missing"
        wifu_variants: list[str] = []
        if profile is not None and saw is not None:
            wifu_candidates = [
                row
                for row in context_candidates(
                    wifus, attack_info.source, profile, saw
                )
                if row.decoded.get("inventorySlot") == attack_info.decoded["weaponSlot"]
            ]
            wifu, wifu_resolution, wifu_variants = choose_latest_context(
                wifu_candidates
            )
            if wifu is None and wifu_resolution == "conflicting":
                conflicts.append({"field": "WeaponItemFullUpdate", "variantHashes": wifu_variants})

        kind, context_missing = weapon_context_kind(attack_info, saw, wifu)
        missing.extend(context_missing)
        if saw is None:
            kind = "unresolved"
        for label, context_record in (
            ("WeaponItemFullUpdate", wifu),
            ("SpecialAttackWeapon", saw),
            ("Attack", attack),
        ):
            if (
                context_record is not None
                and context_record.metadata_resolution != "capture-local-generation"
            ):
                missing.append(
                    f"{label} same-capture preceding SCFU generation metadata"
                )
        missing = sorted(set(missing))
        base = {
            "packetId": attack_info.packet_id,
            "attackInfoPacketId": attack_info.packet_id,
            "capture": attack_info.capture,
            "messageType": "AttackInfo",
            "sourceIdentity": hex_identity(attack_info.source),
            "targetIdentity": hex_identity(attack_info.target),
            "metadataResolution": attack_info.metadata_resolution,
            "metadata": metadata.public() if metadata else None,
            "classification": "normal-landed" if attack_info.decoded["hitTypeWire"] == 3 else "non-normal-landed",
            "hitTypeWire": attack_info.decoded["hitTypeWire"],
            "damageTypeWire": attack_info.decoded["damageTypeWire"],
            "terminalHit": is_terminal_hit(attack_info),
            "missingEvidence": missing,
            "conflicts": conflicts,
            "evidenceFound": {
                "WeaponItemFullUpdate": wifu.packet_id if wifu else None,
                "WeaponItemFullUpdateResolution": wifu_resolution,
                "WeaponItemFullUpdateDefinitionComplete": (
                    wifu.decoded.get("definitionComplete") if wifu else None
                ),
                "WeaponItemFullUpdateProblems": (
                    wifu.decoded.get("definitionProblems", []) if wifu else []
                ),
                "SpecialAttackWeapon": saw.packet_id if saw else None,
                "Attack": attack.packet_id if attack else None,
                "AttackInfo": attack_info.packet_id,
            },
        }
        if missing or conflicts or attack is None or saw is None or profile is None:
            incomplete.append(base)
            continue

        base_signature = chain_base_signature(kind, saw, attack, wifu)
        invariant_contract_signature = chain_invariant_contract_signature(
            kind, saw, attack, wifu
        )
        first_hit_delay = None
        if attack.time is not None and attack_info.time is not None:
            first_hit_delay = (attack_info.time - attack.time).total_seconds()
        attack_start_delay = None
        if saw.time is not None and attack.time is not None:
            attack_start_delay = (attack.time - saw.time).total_seconds()
        complete.append(
            {
                **base,
                "missingEvidence": [],
                "conflicts": [],
                "resourceId": runtime_resource(metadata),
                "weaponContextKind": kind,
                "packetOrder": [
                    row.packet_id
                    for row in sorted(
                        (row for row in (wifu, saw, attack, attack_info) if row is not None),
                        key=packet_sort_key,
                    )
                ],
                "weaponItemFullUpdatePacketId": wifu.packet_id if wifu else None,
                "specialAttackWeaponPacketId": saw.packet_id,
                "attackPacketId": attack.packet_id,
                "baseSignature": base_signature,
                "baseSignatureId": digest(base_signature),
                "invariantContractSignature": invariant_contract_signature,
                "invariantContractSignatureId": digest(
                    invariant_contract_signature
                ),
                "streamSignature": attack_info_stream_signature(attack_info),
                "streamSignatureId": digest(attack_info_stream_signature(attack_info)),
                "amount": attack_info.decoded["amount"],
                "ammo": attack_info.decoded["ammo"],
                "attackStartDelaySeconds": attack_start_delay,
                "firstHitDelaySeconds": first_hit_delay,
                "wifuEnergy": wifu.decoded.get("energy") if wifu else None,
            }
        )

    referenced_prefix_packet_ids = {
        packet_id
        for row in complete
        for packet_id in row["packetOrder"]
    }
    for row in incomplete:
        referenced_prefix_packet_ids.update(
            packet_id
            for key, packet_id in row.get("evidenceFound", {}).items()
            if key in {"WeaponItemFullUpdate", "SpecialAttackWeapon", "Attack"}
            and isinstance(packet_id, str)
        )
    for record in sorted(
        (
            row
            for row in records
            if row.message_type
            in {"WeaponItemFullUpdate", "SpecialAttackWeapon", "Attack"}
            and row.packet_id not in referenced_prefix_packet_ids
        ),
        key=packet_sort_key,
    ):
        evidence_found = {
            "WeaponItemFullUpdate": None,
            "WeaponItemFullUpdateResolution": "missing",
            "WeaponItemFullUpdateDefinitionComplete": None,
            "WeaponItemFullUpdateProblems": [],
            "SpecialAttackWeapon": None,
            "Attack": None,
            "AttackInfo": None,
        }
        evidence_found[record.message_type] = record.packet_id
        if record.message_type == "WeaponItemFullUpdate":
            evidence_found["WeaponItemFullUpdateResolution"] = "orphan-prefix"
            evidence_found["WeaponItemFullUpdateDefinitionComplete"] = (
                record.decoded.get("definitionComplete")
            )
            evidence_found["WeaponItemFullUpdateProblems"] = record.decoded.get(
                "definitionProblems", []
            )
        incomplete.append(
            {
                "packetId": record.packet_id,
                "attackInfoPacketId": None,
                "capture": record.capture,
                "messageType": record.message_type,
                "sourceIdentity": (
                    hex_identity(record.source) if record.source is not None else None
                ),
                "targetIdentity": (
                    hex_identity(record.target) if record.target is not None else None
                ),
                "metadataResolution": record.metadata_resolution,
                "metadata": record.metadata.public() if record.metadata else None,
                "classification": "orphan-combat-prefix",
                "hitTypeWire": None,
                "damageTypeWire": None,
                "missingEvidence": [
                    "AttackInfo correlated after this packet inside the same capture and SCFU generation"
                ],
                "conflicts": [],
                "evidenceFound": evidence_found,
            }
        )
    return complete, incomplete, unsupported


def profile_key(resource: int | None, metadata: dict[str, Any]) -> str:
    resource_text = str(resource) if resource is not None else "unmapped"
    return (
        f"resource={resource_text}|md={metadata['monsterData']}|"
        f"level={metadata['level']}|name={metadata['name']}"
    )


def deduplicate_chains(
    chains: list[dict[str, Any]], packet_by_id: dict[str, PacketRecord]
) -> tuple[list[dict[str, Any]], int]:
    result = []
    seen = set()
    duplicate_count = 0
    for chain in sorted(
        chains,
        key=lambda row: packet_sort_key(packet_by_id[row["attackInfoPacketId"]]),
    ):
        packet = packet_by_id[chain["attackInfoPacketId"]]
        key = (
            packet.captured_utc,
            packet.body_sha256,
            chain["baseSignatureId"],
            chain["streamSignatureId"],
        )
        if key in seen:
            duplicate_count += 1
            continue
        seen.add(key)
        result.append(chain)
    return result, duplicate_count


def captured_weapon_cycle_seconds(weapon_stats: dict[int, int]) -> float | None:
    attack_delay_raw = weapon_stats.get(294)
    recharge_delay_raw = weapon_stats.get(210)
    if (
        not isinstance(attack_delay_raw, int)
        or not isinstance(recharge_delay_raw, int)
    ):
        return None
    attack_delay = signed32(attack_delay_raw & 0xFFFFFFFF)
    recharge_delay = signed32(recharge_delay_raw & 0xFFFFFFFF)
    if attack_delay <= 0 or recharge_delay <= 0:
        return None
    return (attack_delay + recharge_delay) / 100.0


def damage_observations_are_runtime_ready(amounts: list[int]) -> bool:
    return bool(amounts)


def semantic_fallback_is_capture_proven(
    eligible_invariant_variants: list[dict[str, Any]],
    invariant_normal_contract_count: int,
) -> bool:
    return (
        invariant_normal_contract_count == 1
        and len(eligible_invariant_variants) == 1
    )


def find_conflicted_normal_sources(
    variants_grouped: dict[str, list[dict[str, Any]]],
) -> list[str]:
    """Find true raw-contract contradictions for the same captured attack stream.

    One NPC can emit several independently identified AttackInfo streams under a
    shared fight opening.  Distinct stream signatures are parallel attack modes,
    not contradictory profile observations.  A source is conflicted only when
    the same exact AttackInfo stream signature resolves to multiple invariant
    wire contracts.  Runtime variant selection remains independently fail-closed.
    """
    source_stream_variants: dict[tuple[str, str], set[str]] = defaultdict(set)
    for variant_id, rows in variants_grouped.items():
        for row in rows:
            source_stream_variants[
                (row["sourceIdentity"], row["streamSignatureId"])
            ].add(variant_id)
    return sorted(
        {
            source
            for (source, _stream_id), variants in source_stream_variants.items()
            if len(variants) > 1
        }
    )


def correlated_generation_is_owned(row: dict[str, Any]) -> bool:
    """Return whether the exact SCFU generation correlated to a chain is owned."""
    metadata = row.get("metadata")
    return bool(
        isinstance(metadata, dict)
        and str(metadata.get("ownerIdentity", "")).strip()
    )


def capture_certifiable_source_identities(
    rows: list[dict[str, Any]],
    conflicted_sources: list[str],
    correlation_conflicted_sources: list[str],
) -> list[str]:
    """Select unowned sources from each chain's correlated SCFU generation.

    A source identity can be reused by a later generation.  A later owner-bearing
    SCFU must not retroactively turn an earlier, explicitly unowned hostile chain
    into pet evidence.
    """
    return sorted(
        {
            row["sourceIdentity"]
            for row in rows
            if row["sourceIdentity"] not in conflicted_sources
            and row["sourceIdentity"] not in correlation_conflicted_sources
            and row["metadataResolution"] == "capture-local-generation"
            and not correlated_generation_is_owned(row)
        }
    )


def capture_evidence_is_safe(variant: dict[str, Any]) -> bool:
    if (
        not variant["captureCertified"]
        or not variant["streams"]
        or not variant["rawWireVariantObservations"]
    ):
        return False
    weapon_context_kind = variant["baseSignature"]["weaponContextKind"]
    if any(
        not observation["specialAttackWeaponPacketId"]
        or not observation["attackPacketId"]
        or not observation["attackInfoPacketId"]
        or (
            weapon_context_kind == "equipped"
            and not observation["weaponItemFullUpdatePacketId"]
        )
        for observation in variant["rawWireVariantObservations"]
    ):
        return False
    return all(
        stream["damageObservations"]
        and stream["attackInfoPacketIds"]
        for stream in variant["streams"]
    )


def build_profiles(
    complete: list[dict[str, Any]],
    incomplete: list[dict[str, Any]],
    unsupported: list[dict[str, Any]],
    packet_by_id: dict[str, PacketRecord],
    metadata_generations: list[MetadataGeneration],
) -> list[dict[str, Any]]:
    complete_grouped: dict[str, list[dict[str, Any]]] = defaultdict(list)
    incomplete_grouped: dict[str, list[dict[str, Any]]] = defaultdict(list)
    unsupported_grouped: dict[str, list[dict[str, Any]]] = defaultdict(list)
    metadata_by_key: dict[str, list[MetadataGeneration]] = defaultdict(list)

    for generation in metadata_generations:
        key = profile_key(
            RESOURCE_BY_CAPTURED_REALM.get(generation.captured_realm_id),
            generation.public(),
        )
        metadata_by_key[key].append(generation)
    for chain in complete:
        key = profile_key(chain["resourceId"], chain["metadata"])
        complete_grouped[key].append(chain)
    for observation in incomplete:
        metadata = observation.get("metadata")
        key = profile_key(
            runtime_resource_from_public(metadata), metadata
        ) if metadata else "unresolved-metadata"
        incomplete_grouped[key].append(observation)
    for observation in unsupported:
        metadata = observation.get("metadata")
        key = profile_key(
            runtime_resource_from_public(metadata), metadata
        ) if metadata else "unresolved-metadata"
        unsupported_grouped[key].append(observation)

    profiles = []
    all_keys = (
        set(complete_grouped)
        | set(incomplete_grouped)
        | set(unsupported_grouped)
        | set(metadata_by_key)
    )
    for key in sorted(all_keys):
        chains = complete_grouped.get(key, [])
        incomplete_rows = incomplete_grouped.get(key, [])
        unsupported_rows = unsupported_grouped.get(key, [])
        metadata = None
        if chains:
            metadata = chains[0]["metadata"]
        elif incomplete_rows:
            metadata = incomplete_rows[0].get("metadata")
        elif unsupported_rows:
            metadata = unsupported_rows[0].get("metadata")
        elif metadata_by_key.get(key):
            metadata = metadata_by_key[key][0].public()

        normal = [row for row in chains if row["classification"] == "normal-landed"]
        non_normal = [row for row in chains if row["classification"] != "normal-landed"]
        variants_grouped: dict[str, list[dict[str, Any]]] = defaultdict(list)
        for row in normal:
            variants_grouped[row["invariantContractSignatureId"]].append(row)

        conflicted_sources = find_conflicted_normal_sources(variants_grouped)
        correlation_conflicted_sources = sorted(
            {
                row["sourceIdentity"]
                for row in incomplete_rows
                if row.get("sourceIdentity") and row.get("conflicts")
            }
        )
        owned_sources = sorted(
            {
                row["sourceIdentity"]
                for row in normal
                if correlated_generation_is_owned(row)
            }
        )
        variants = []
        for variant_id in sorted(variants_grouped):
            rows = variants_grouped[variant_id]
            source_identities = capture_certifiable_source_identities(
                rows,
                conflicted_sources,
                correlation_conflicted_sources,
            )
            representative_rows = [
                row
                for row in rows
                if row["sourceIdentity"] in source_identities
                and not correlated_generation_is_owned(row)
            ]
            representative = min(
                representative_rows or rows,
                key=lambda row: packet_sort_key(
                    packet_by_id[row["attackInfoPacketId"]]
                ),
            )
            weapon_stats = {
                row["stat"]: row["value"]
                for row in representative["baseSignature"]
                    .get("weaponItemFullUpdate", {})
                    .get("stats", [])
            }
            captured_attack_delay = weapon_stats.get(294)
            captured_recharge_delay = weapon_stats.get(210)
            captured_weapon_cycle = captured_weapon_cycle_seconds(weapon_stats)
            weapon_context_kind = representative["baseSignature"]["weaponContextKind"]
            saw_unknown5_candidates = sorted(
                {
                    packet_by_id[row["specialAttackWeaponPacketId"]]
                    .decoded["unknown5"]
                    for row in rows
                }
            )
            saw_state_selection_missing = (
                []
                if len(saw_unknown5_candidates) == 1
                else [
                    "deterministic runtime SpecialAttackWeapon Unknown5 state "
                    f"selection across captured values {saw_unknown5_candidates}"
                ]
            )
            equipped_initial_ammo = None
            equipped_initial_ammo_candidates: list[int] = []
            mutable_weapon_state_candidates: list[dict[str, int]] = []
            ammo_sequence_problems: list[str] = []
            if weapon_context_kind == "equipped":
                mutable_weapon_states = set()
                for row in rows:
                    wifu_packet_id = row.get("weaponItemFullUpdatePacketId")
                    if not wifu_packet_id:
                        continue
                    mutable_stats = {
                        stat["stat"]: stat["value"]
                        for stat in packet_by_id[wifu_packet_id].decoded["stats"]
                        if stat["stat"] in MUTABLE_WIFU_STAT_IDS
                    }
                    if 26 in mutable_stats and 412 in mutable_stats:
                        mutable_weapon_states.add(
                            (mutable_stats[26], mutable_stats[412])
                        )
                mutable_weapon_state_candidates = [
                    {"energy": energy, "multipleCount": multiple_count}
                    for energy, multiple_count in sorted(mutable_weapon_states)
                ]
                (
                    equipped_initial_ammo,
                    equipped_initial_ammo_candidates,
                    ammo_sequence_problems,
                ) = (
                    validate_equipped_ammo_sequence(
                        rows,
                        packet_by_id,
                        representative["weaponItemFullUpdatePacketId"],
                    )
                )
            weapon_state_selection_missing = (
                []
                if weapon_context_kind != "equipped"
                or len(mutable_weapon_state_candidates) == 1
                else [
                    "deterministic runtime WIFU Energy/MultipleCount state "
                    f"selection across captured values {mutable_weapon_state_candidates}"
                ]
            )
            streams_grouped: dict[str, list[dict[str, Any]]] = defaultdict(list)
            for row in rows:
                streams_grouped[row["streamSignatureId"]].append(row)
            streams = []
            for stream_id in sorted(streams_grouped):
                stream_rows = sorted(
                    streams_grouped[stream_id],
                    key=lambda row: packet_sort_key(
                        packet_by_id[row["attackInfoPacketId"]]
                    ),
                )
                amounts = [row["amount"] for row in stream_rows]
                ammo_observations = [row["ammo"] for row in stream_rows]
                fight_timings = observed_fight_timings(
                    stream_rows, packet_by_id
                )
                complete_fight_timings = [
                    row
                    for row in fight_timings
                    if row["attackStartDelaySeconds"] is not None
                    and row["firstHitDelaySeconds"] is not None
                ]
                attack_start_delays = [
                    row["attackStartDelaySeconds"] for row in complete_fight_timings
                ]
                first_delays = [
                    row["firstHitDelaySeconds"] for row in complete_fight_timings
                ]
                intervals = observed_intervals(stream_rows, packet_by_id)
                initial_ammo_candidates = observed_initial_ammo(
                    stream_rows, packet_by_id
                )
                distinct_first_delays = sorted(set(first_delays))
                distinct_intervals = sorted(set(intervals))
                streams.append(
                    {
                        "streamSignatureId": stream_id,
                        "signature": stream_rows[0]["streamSignature"],
                        "minimumObservedDamage": min(amounts),
                        "maximumObservedDamage": max(amounts),
                        "damageObservations": amounts,
                        "ammoObservationsInOrder": ammo_observations,
                        "initialAmmoCandidates": initial_ammo_candidates,
                        "attackStartDelayObservationsSeconds": attack_start_delays,
                        "firstHitDelayObservationsSeconds": first_delays,
                        "pairedFightTimingObservations": fight_timings,
                        "capturedFightCount": len(fight_timings),
                        "completeFightTimingCount": len(complete_fight_timings),
                        "landedIntervalObservationsSeconds": intervals,
                        "capturedWeaponAttackDelayCentiseconds": captured_attack_delay,
                        "capturedWeaponRechargeDelayCentiseconds": captured_recharge_delay,
                        "capturedWeaponCycleSeconds": captured_weapon_cycle,
                        "representativeFirstHitDelaySeconds": (
                            distinct_first_delays[0]
                            if len(distinct_first_delays) == 1
                            else None
                        ),
                        "representativeRechargeSeconds": (
                            captured_weapon_cycle
                            if captured_weapon_cycle is not None
                            else distinct_intervals[0]
                            if len(distinct_intervals) == 1
                            else None
                        ),
                        "capturedTerminalHitOnly": all(
                            row["terminalHit"] for row in stream_rows
                        ),
                        "chainCount": len(stream_rows),
                        "attackInfoPacketIds": [row["attackInfoPacketId"] for row in stream_rows],
                    }
                )
            for stream in streams:
                runtime_missing = []
                runtime_initial_ammo = None
                if weapon_context_kind == "equipped":
                    runtime_initial_ammo = equipped_initial_ammo
                    runtime_missing.extend(ammo_sequence_problems)
                elif len(stream["initialAmmoCandidates"]) == 1:
                    runtime_initial_ammo = stream["initialAmmoCandidates"][0]
                if runtime_initial_ammo is None:
                    runtime_missing.append(
                        "unambiguous capture-backed initial AttackInfo ammunition state"
                    )
                if not damage_observations_are_runtime_ready(
                    stream["damageObservations"]
                ):
                    runtime_missing.append("normal landed AttackInfo damage observation")
                if not stream["attackStartDelayObservationsSeconds"]:
                    runtime_missing.append("captured SpecialAttackWeapon-to-Attack delay")
                if not stream["firstHitDelayObservationsSeconds"]:
                    runtime_missing.append("captured Attack-to-first-AttackInfo delay")
                if len(stream["attackStartDelayObservationsSeconds"]) != len(
                    stream["firstHitDelayObservationsSeconds"]
                ):
                    runtime_missing.append(
                        "one paired SAW-to-Attack and Attack-to-first-hit delay per captured fight"
                    )
                if stream["completeFightTimingCount"] != stream["capturedFightCount"]:
                    runtime_missing.append(
                        "complete paired timing timestamps for every captured fight"
                    )
                if weapon_context_kind == "equipped":
                    if captured_weapon_cycle is None:
                        runtime_missing.append(
                            "positive captured WIFU AttackDelay and positive RechargeDelay"
                        )
                else:
                    if not stream["landedIntervalObservationsSeconds"]:
                        runtime_missing.append(
                            "captured same-fight landed AttackInfo interval"
                        )
                    runtime_missing.append(
                        "capture-backed non-equipped attack range"
                    )
                runtime_missing.extend(saw_state_selection_missing)
                runtime_missing.extend(weapon_state_selection_missing)
                stream["runtimeInitialAmmoCount"] = runtime_initial_ammo
                stream["ammoTransitionValidation"] = {
                    "valid": not ammo_sequence_problems,
                    "problems": ammo_sequence_problems,
                }
                stream["runtimeContractReady"] = not runtime_missing
                stream["runtimeMissingEvidence"] = runtime_missing
            ordered_rows = sorted(
                rows,
                key=lambda row: packet_sort_key(
                    packet_by_id[row["attackInfoPacketId"]]
                ),
            )
            raw_wire_variant_observations = [
                {
                    "capture": row["capture"],
                    "sourceIdentity": row["sourceIdentity"],
                    "baseSignatureId": row["baseSignatureId"],
                    "weaponItemFullUpdatePacketId": row[
                        "weaponItemFullUpdatePacketId"
                    ],
                    "specialAttackWeaponPacketId": row[
                        "specialAttackWeaponPacketId"
                    ],
                    "attackPacketId": row["attackPacketId"],
                    "attackInfoPacketId": row["attackInfoPacketId"],
                    "terminalHit": row["terminalHit"],
                }
                for row in ordered_rows
            ]
            mutable_saw_state_observations = []
            seen_saw_packet_ids = set()
            for row in ordered_rows:
                saw_packet_id = row["specialAttackWeaponPacketId"]
                if saw_packet_id in seen_saw_packet_ids:
                    continue
                seen_saw_packet_ids.add(saw_packet_id)
                saw = packet_by_id[saw_packet_id]
                mutable_saw_state_observations.append(
                    {
                        "packetId": saw_packet_id,
                        "capture": row["capture"],
                        "sourceIdentity": row["sourceIdentity"],
                        "unknown5": saw.decoded["unknown5"],
                        "unknown5RawHex": saw.decoded["fieldProvenance"][
                            "unknown5"
                        ]["rawHex"],
                    }
                )
            mutable_wifu_state_observations = []
            seen_wifu_packet_ids = set()
            for row in ordered_rows:
                wifu_packet_id = row["weaponItemFullUpdatePacketId"]
                if not wifu_packet_id or wifu_packet_id in seen_wifu_packet_ids:
                    continue
                seen_wifu_packet_ids.add(wifu_packet_id)
                wifu = packet_by_id[wifu_packet_id]
                mutable_stats = {
                    stat["stat"]: stat
                    for stat in wifu.decoded["stats"]
                    if stat["stat"] in MUTABLE_WIFU_STAT_IDS
                }
                energy = mutable_stats[26]["value"]
                derived_initial_ammo = (
                    -1
                    if energy == -1
                    else 0
                    if energy == 0
                    else energy - 1
                    if energy > 0
                    else None
                )
                mutable_wifu_state_observations.append(
                    {
                        "packetId": wifu_packet_id,
                        "capture": row["capture"],
                        "sourceIdentity": row["sourceIdentity"],
                        "multipleCount": mutable_stats[412]["value"],
                        "multipleCountRawValue": mutable_stats[412]["rawValue"],
                        "energy": energy,
                        "energyRawValue": mutable_stats[26]["rawValue"],
                        "derivedInitialAttackInfoAmmo": derived_initial_ammo,
                    }
                )
            variants.append(
                {
                    "semanticProfileId": f"{digest(key)}-{variant_id}",
                    "baseSignatureId": representative["baseSignatureId"],
                    "baseSignature": representative["baseSignature"],
                    "invariantContractSignatureId": variant_id,
                    "invariantContractSignature": representative[
                        "invariantContractSignature"
                    ],
                    "rawWireVariantObservations": raw_wire_variant_observations,
                    "mutableSawStateObservations": mutable_saw_state_observations,
                    "mutableWifuStateObservations": mutable_wifu_state_observations,
                    "captureCertified": bool(source_identities),
                    "sourceIdentities": source_identities,
                    "excludedConflictedSourceIdentities": sorted(
                        source for source in conflicted_sources if any(row["sourceIdentity"] == source for row in rows)
                    ),
                    "excludedCorrelationConflictSourceIdentities": sorted(
                        source
                        for source in correlation_conflicted_sources
                        if any(row["sourceIdentity"] == source for row in rows)
                    ),
                    "metadataResolutions": sorted(
                        {row["metadataResolution"] for row in rows}
                    ),
                    "excludedInferredMetadataSourceIdentities": sorted(
                        {
                            row["sourceIdentity"]
                            for row in rows
                            if row["metadataResolution"]
                            != "capture-local-generation"
                        }
                    ),
                    "unresolvedBehaviorSourceIdentities": [],
                    "representativeEvidenceSourceIdentity": representative["sourceIdentity"],
                    "representativeWifuPacketId": representative["weaponItemFullUpdatePacketId"],
                    "representativeSawPacketId": representative["specialAttackWeaponPacketId"],
                    "representativeAttackPacketId": representative["attackPacketId"],
                    "captureSessions": sorted({row["capture"] for row in rows}),
                    "streams": streams,
                    "runtimeInitialAmmoCandidates": (
                        equipped_initial_ammo_candidates
                        if weapon_context_kind == "equipped"
                        else sorted(
                            {
                                candidate
                                for stream in streams
                                for candidate in stream["initialAmmoCandidates"]
                            }
                        )
                    ),
                    "runtimeMutableWeaponStateCandidates": (
                        mutable_weapon_state_candidates
                    ),
                    "deterministicRuntimeInitializationProven": (
                        len(saw_unknown5_candidates) == 1
                        and (
                            len(equipped_initial_ammo_candidates) == 1
                            and len(mutable_weapon_state_candidates) == 1
                            if weapon_context_kind == "equipped"
                            else all(
                                len(stream["initialAmmoCandidates"]) == 1
                                for stream in streams
                            )
                        )
                    ),
                    "runtimeContractReady": (
                        len(streams) == 1 and streams[0]["runtimeContractReady"]
                    ),
                    "runtimeMissingEvidence": (
                        [
                            "exact specialized runtime sequence for multiple captured AttackInfo streams"
                        ]
                        + sorted(
                            {
                                missing
                                for stream in streams
                                for missing in stream["runtimeMissingEvidence"]
                            }
                        )
                        if len(streams) != 1
                        else streams[0]["runtimeMissingEvidence"]
                    ),
                    "chainCount": len(rows),
                }
            )

        actual_normal_signature_conflict = bool(conflicted_sources)
        for variant in variants:
            variant["captureEvidenceSafe"] = capture_evidence_is_safe(variant)
        certified_variants = [row for row in variants if row["captureCertified"]]
        runtime_ready_variants = [
            row
            for row in certified_variants
            if row["runtimeContractReady"]
        ]
        semantic_fallback_safe_variants = [
            row
            for row in certified_variants
            if row["captureEvidenceSafe"]
            and row["deterministicRuntimeInitializationProven"]
        ]
        semantic_fallback = semantic_fallback_is_capture_proven(
            semantic_fallback_safe_variants,
            len(variants_grouped),
        )
        if certified_variants:
            status = "capture-certified"
        elif non_normal:
            status = "unresolved-critical-or-non-normal-only"
        elif unsupported_rows:
            status = "unresolved-unsupported-sequence-only"
        else:
            status = "unresolved-incomplete-sequence"
        searched_sessions = sorted(
            {generation.capture for generation in metadata_by_key.get(key, [])}
            | {row["capture"] for row in chains}
            | {row["capture"] for row in incomplete_rows}
        )
        profiles.append(
            {
                "profileKey": key,
                "metadata": metadata,
                "status": status,
                "semanticFallbackCaptureProven": semantic_fallback,
                "actualNormalSignatureConflict": actual_normal_signature_conflict,
                "invariantNormalContractCount": len(variants_grouped),
                "captureCertifiedVariantCount": len(certified_variants),
                "runtimeReadyVariantCount": len(runtime_ready_variants),
                "normalCompleteChainCount": len(normal),
                "nonNormalCompleteChainCount": len(non_normal),
                "incompleteAttackInfoCount": len(incomplete_rows),
                "unsupportedNpcSequenceCount": len(unsupported_rows),
                "captureSessionsSearched": searched_sessions,
                "ownedOrPetSourceIdentitiesExcluded": owned_sources,
                "conflictedSourceIdentities": conflicted_sources,
                "correlationConflictedSourceIdentities": correlation_conflicted_sources,
                "variants": variants,
                "nonNormalObservations": aggregate_observations(
                    non_normal, "attackInfoPacketId"
                ),
                "incompleteObservations": aggregate_observations(
                    incomplete_rows, "packetId"
                ),
                "unsupportedSequences": aggregate_observations(
                    unsupported_rows, "packetId"
                ),
                "disabledCapability": (
                    None
                    if runtime_ready_variants
                    else "NPC auto-attack emission and damage application"
                ),
            }
        )
    return profiles


def aggregate_observations(
    rows: list[dict[str, Any]], packet_field: str
) -> list[dict[str, Any]]:
    grouped: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for row in rows:
        signature = {
            "sourceIdentity": row.get("sourceIdentity"),
            "attackerIdentity": row.get("attackerIdentity"),
            "defenderIdentity": row.get("defenderIdentity"),
            "n3SourceIdentity": row.get("n3SourceIdentity"),
            "n3Unknown": row.get("n3Unknown"),
            "unknown1": row.get("unknown1"),
            "unknown2": row.get("unknown2"),
            "unknown5": row.get("unknown5"),
            "packetOrderProven": row.get("packetOrderProven"),
            "classification": row.get("classification"),
            "messageType": row.get("messageType"),
            "hitTypeWire": row.get("hitTypeWire"),
            "damageTypeWire": row.get("damageTypeWire"),
            "missingEvidence": row.get("missingEvidence", []),
            "conflicts": row.get("conflicts", []),
            "runtimeSupport": row.get("runtimeSupport"),
        }
        grouped[canonical(signature)].append(row)
    result = []
    for signature_text in sorted(grouped):
        values = grouped[signature_text]
        signature = json.loads(signature_text)
        context_packet_ids = sorted(
            {
                packet_id
                for row in values
                for key, packet_id in row.get("evidenceFound", {}).items()
                if key
                in {
                    "WeaponItemFullUpdate",
                    "SpecialAttackWeapon",
                    "Attack",
                    "AttackInfo",
                    "MissedAttackInfo",
                }
                and isinstance(packet_id, str)
            }
        )
        result.append(
            {
                **signature,
                "observationCount": len(values),
                "captureSessions": sorted(
                    {
                        row.get("capture")
                        or (row.get("metadata") or {}).get("capture")
                        for row in values
                        if row.get("capture") or (row.get("metadata") or {}).get("capture")
                    }
                ),
                "packetIds": sorted(
                    {row.get(packet_field) for row in values if row.get(packet_field)}
                ),
                "samplePacketIds": sorted(
                    {row.get(packet_field) for row in values if row.get(packet_field)}
                )[:3],
                "contextPacketIds": context_packet_ids,
                "evidenceFound": {
                    "AttackInfo": any(
                        bool(row.get("evidenceFound", {}).get("AttackInfo"))
                        for row in values
                    ),
                    "MissedAttackInfo": any(
                        bool(row.get("evidenceFound", {}).get("MissedAttackInfo"))
                        for row in values
                    ),
                    "metadataResolution": sorted(
                        {row.get("metadataResolution", "") for row in values if row.get("metadataResolution")}
                    ),
                    "samples": [
                        row.get("evidenceFound", {}) for row in values[:3]
                    ],
                },
            }
        )
    return result


def add_packet_audit_group(
    groups_by_id: dict[str, dict[str, Any]],
    memberships_by_packet_id: dict[str, set[str]],
    derivation: dict[str, Any],
    packet_ids: Iterable[str],
) -> dict[str, Any] | None:
    members = sorted(set(packet_ids))
    if not members:
        return None
    group_id = "packet-audit-" + digest(derivation, 32)
    group = {
        "auditGroupId": group_id,
        "derivation": derivation,
        "packetReferenceCount": len(members),
        "packetReferenceSha256": packet_reference_sha256(members),
        "samplePacketIds": members[:3],
    }
    existing = groups_by_id.get(group_id)
    if existing is not None and existing != group:
        raise ValueError(f"packet audit group collision: {group_id}")
    groups_by_id[group_id] = group
    for packet_id in members:
        memberships_by_packet_id[packet_id].add(group_id)
    return group


def packet_audit_reference(group: dict[str, Any] | None) -> dict[str, Any]:
    if group is None:
        return {
            "auditGroupId": None,
            "packetReferenceCount": 0,
            "packetReferenceSha256": packet_reference_sha256([]),
            "samplePacketIds": [],
        }
    return {
        key: group[key]
        for key in (
            "auditGroupId",
            "packetReferenceCount",
            "packetReferenceSha256",
            "samplePacketIds",
        )
    }


def variant_packet_references(variant: dict[str, Any]) -> set[str]:
    packet_ids = {
        packet_id
        for observation in variant["rawWireVariantObservations"]
        for packet_id in (
            observation.get("weaponItemFullUpdatePacketId"),
            observation.get("specialAttackWeaponPacketId"),
            observation.get("attackPacketId"),
            observation.get("attackInfoPacketId"),
        )
        if packet_id
    }
    packet_ids.update(
        packet_id
        for stream in variant["streams"]
        for packet_id in stream["attackInfoPacketIds"]
    )
    packet_ids.update(
        row["packetId"] for row in variant["mutableWifuStateObservations"]
    )
    packet_ids.update(
        packet_id
        for packet_id in (
            variant.get("representativeWifuPacketId"),
            variant.get("representativeSawPacketId"),
            variant.get("representativeAttackPacketId"),
        )
        if packet_id
    )
    return packet_ids


def _metadata_generation_index_for_packet(
    record: PacketRecord,
    metadata_index_by_key: dict[str, int],
    snapshot_identity: str,
    phase: str,
) -> int | None:
    if record.metadata is None:
        return None
    generation_key = record.metadata.generation_key
    if generation_key not in metadata_index_by_key:
        raise CaptureAggregationInvariantError(
            "capture aggregation metadata invariant failed "
            f"missingGenerationKey={generation_key} "
            f"owningPacket={record.packet_id} owningSession={record.capture} "
            f"snapshot={snapshot_identity} phase={phase}"
        )
    return metadata_index_by_key[generation_key]


def compact_packet_evidence(
    profiles: list[dict[str, Any]],
    lifecycle_records: list[PacketRecord],
    complete: list[dict[str, Any]],
    packet_by_id: dict[str, PacketRecord],
    sessions: list[dict[str, Any]],
    metadata_generations: list[dict[str, Any]],
    snapshot_identity: str = "self-test",
    phase: str = "packet-evidence-compaction",
) -> dict[str, Any]:
    groups_by_id: dict[str, dict[str, Any]] = {}
    memberships_by_packet_id: dict[str, set[str]] = defaultdict(set)
    full_provenance_packet_ids: set[str] = set()
    legacy_referenced_packet_ids = {
        packet_id for row in complete for packet_id in row["packetOrder"]
    }

    for profile in profiles:
        profile_key_value = profile["profileKey"]
        for variant in profile["variants"]:
            packet_ids = variant_packet_references(variant)
            group = add_packet_audit_group(
                groups_by_id,
                memberships_by_packet_id,
                {
                    "kind": "combat-variant",
                    "profileKey": profile_key_value,
                    "semanticProfileId": variant["semanticProfileId"],
                },
                packet_ids,
            )
            variant["packetAudit"] = packet_audit_reference(group)
            if variant["captureCertified"]:
                full_provenance_packet_ids.update(packet_ids)

        for section in (
            "nonNormalObservations",
            "incompleteObservations",
            "unsupportedSequences",
        ):
            for observation_index, observation in enumerate(profile[section]):
                packet_ids = observation.pop("packetIds")
                context_packet_ids = observation.pop("contextPacketIds")
                observation.pop("samplePacketIds")
                legacy_referenced_packet_ids.update(packet_ids)
                legacy_referenced_packet_ids.update(context_packet_ids)
                derivation = {
                    "kind": "aggregated-observation",
                    "profileKey": profile_key_value,
                    "section": section,
                    "observationIndex": observation_index,
                }
                primary_group = add_packet_audit_group(
                    groups_by_id,
                    memberships_by_packet_id,
                    {**derivation, "packetRole": "primary"},
                    packet_ids,
                )
                context_group = add_packet_audit_group(
                    groups_by_id,
                    memberships_by_packet_id,
                    {**derivation, "packetRole": "correlation-context"},
                    context_packet_ids,
                )
                observation["packetAudit"] = packet_audit_reference(primary_group)
                observation["contextPacketAudit"] = packet_audit_reference(
                    context_group
                )

    lifecycle_classifications = []
    for message_type in sorted(
        {record.message_type for record in lifecycle_records}
    ):
        packet_ids = [
            record.packet_id
            for record in lifecycle_records
            if record.message_type == message_type
        ]
        group = add_packet_audit_group(
            groups_by_id,
            memberships_by_packet_id,
            {
                "kind": "lifecycle-boundary",
                "messageType": message_type,
            },
            packet_ids,
        )
        lifecycle_classifications.append(
            {
                "messageType": message_type,
                **packet_audit_reference(group),
            }
        )
        legacy_referenced_packet_ids.update(packet_ids)

    grouped_packet_ids = set(memberships_by_packet_id)
    if grouped_packet_ids != legacy_referenced_packet_ids:
        missing = sorted(legacy_referenced_packet_ids - grouped_packet_ids)[:3]
        extra = sorted(grouped_packet_ids - legacy_referenced_packet_ids)[:3]
        raise ValueError(
            "packet audit grouping changed exhaustive legacy reference coverage; "
            f"missing={missing} extra={extra}"
        )
    unknown_packet_ids = grouped_packet_ids - set(packet_by_id)
    if unknown_packet_ids:
        raise ValueError(
            "packet audit grouping references unknown packets: "
            + ", ".join(sorted(unknown_packet_ids)[:3])
        )

    groups = sorted(groups_by_id.values(), key=lambda row: row["auditGroupId"])
    group_index_by_id = {
        group["auditGroupId"]: index for index, group in enumerate(groups)
    }
    session_rows = sorted(sessions, key=lambda row: row["capture"])
    session_index_by_capture = {
        row["capture"]: index for index, row in enumerate(session_rows)
    }
    if len(session_index_by_capture) != len(session_rows):
        raise ValueError("capture session index contains duplicate capture locators")
    metadata_index_by_key = {
        row["generationKey"]: index
        for index, row in enumerate(metadata_generations)
    }
    if len(metadata_index_by_key) != len(metadata_generations):
        raise ValueError("metadata generation index contains duplicate keys")

    audit_records = [
        packet_by_id[packet_id]
        for packet_id in grouped_packet_ids - full_provenance_packet_ids
    ]
    audit_records.sort(
        key=lambda record: (
            f"{record.capture}|{record.direction}|{record.sequence}|"
            f"{record.packet_sha256[:12]}"
        )
    )
    artifact_table = sorted({record.canonical_source for record in audit_records})
    artifact_index = {value: index for index, value in enumerate(artifact_table)}
    message_type_table = sorted({record.message_type for record in audit_records})
    message_type_index = {
        value: index for index, value in enumerate(message_type_table)
    }
    direction_table = sorted({record.direction for record in audit_records})
    direction_index = {
        value: index for index, value in enumerate(direction_table)
    }
    metadata_resolution_table = sorted(
        {record.metadata_resolution for record in audit_records}
    )
    metadata_resolution_index = {
        value: index for index, value in enumerate(metadata_resolution_table)
    }

    ledger = []
    for record in audit_records:
        member_group_ids = memberships_by_packet_id[record.packet_id]
        if not isinstance(member_group_ids, set):
            raise ValueError(
                f"{record.packet_id}: packet audit memberships have invalid type "
                f"{type(member_group_ids).__name__}"
            )
        memberships = sorted(
            group_index_by_id[group_id]
            for group_id in member_group_ids
        )
        metadata_index = _metadata_generation_index_for_packet(
            record,
            metadata_index_by_key,
            snapshot_identity,
            phase,
        )
        row = [
            session_index_by_capture[record.capture],
            artifact_index[record.canonical_source],
            direction_index[record.direction],
            record.sequence,
            record.global_ordinal,
            message_type_index[record.message_type],
            len(record.packet_hex) // 2,
            len(record.body_hex) // 2,
            record.packet_sha256_base64
            or sha256_hex_to_base64(record.packet_sha256),
            record.body_sha256_base64
            or sha256_hex_to_base64(record.body_sha256),
            record.decoded_sha256_base64
            or sha256_hex_to_base64(sha256_canonical(record.decoded)),
            metadata_index,
            metadata_resolution_index[record.metadata_resolution],
            signed32(record.source & 0xFFFFFFFF)
            if record.source is not None
            else None,
            signed32(record.target & 0xFFFFFFFF)
            if record.target is not None
            else None,
            memberships,
        ]
        ledger.append(row)

    packets = []
    for packet_id in sorted(full_provenance_packet_ids):
        packet = packet_by_id[packet_id].provenance()
        packet["auditGroupIndexes"] = sorted(
            group_index_by_id[group_id]
            for group_id in memberships_by_packet_id[packet_id]
        )
        packets.append(packet)

    lifecycle_packet_ids = [record.packet_id for record in lifecycle_records]
    return {
        "packetAuditGroups": groups,
        "packetAuditArtifactTable": artifact_table,
        "packetAuditDirectionTable": direction_table,
        "packetAuditMessageTypeTable": message_type_table,
        "packetAuditMetadataResolutionTable": metadata_resolution_table,
        "packetAuditLedgerColumns": list(PACKET_AUDIT_LEDGER_COLUMNS),
        "packetAuditLedgerPacketIdDerivation": (
            PACKET_AUDIT_PACKET_ID_DERIVATION
        ),
        "packetAuditLedgerSha256": positional_ledger_sha256(ledger),
        "packetAuditLedger": ledger,
        "lifecycleBoundarySummary": {
            "observationCount": len(lifecycle_packet_ids),
            "packetReferenceSha256": packet_reference_sha256(
                lifecycle_packet_ids
            ),
            "samplePacketIds": sorted(set(lifecycle_packet_ids))[:3],
            "classifications": lifecycle_classifications,
            "runtimeSupport": (
                "fight-boundary evidence used to prevent cross-fight packet "
                "correlation; lifecycle emission remains owned by the existing "
                "shared combat runtime"
            ),
        },
        "packets": packets,
    }


def runtime_resource_from_public(metadata: dict[str, Any] | None) -> int | None:
    if not metadata:
        return None
    realm = metadata.get("capturedRealmId")
    return RESOURCE_BY_CAPTURED_REALM.get(realm)


def observed_intervals(
    rows: list[dict[str, Any]], packet_by_id: dict[str, PacketRecord]
) -> list[float]:
    grouped: dict[tuple[str, str, str, str], list[PacketRecord]] = defaultdict(list)
    for row in rows:
        packet = packet_by_id[row["attackInfoPacketId"]]
        grouped[
            (
                packet.capture,
                row["sourceIdentity"],
                row["targetIdentity"],
                row["attackPacketId"],
            )
        ].append(packet)
    intervals = []
    for packets in grouped.values():
        packets.sort(key=packet_sort_key)
        for left, right in zip(packets, packets[1:]):
            if left.time is None or right.time is None:
                continue
            value = (right.time - left.time).total_seconds()
            if value > 0:
                intervals.append(value)
    return intervals


def observed_fight_timings(
    rows: list[dict[str, Any]], packet_by_id: dict[str, PacketRecord]
) -> list[dict[str, Any]]:
    grouped: dict[tuple[str, str, str, str], list[dict[str, Any]]] = defaultdict(list)
    for row in rows:
        grouped[
            (
                row["capture"],
                row["sourceIdentity"],
                row["targetIdentity"],
                row["attackPacketId"],
            )
        ].append(row)
    observations = []
    for group_rows in grouped.values():
        first = min(
            group_rows,
            key=lambda row: packet_sort_key(packet_by_id[row["attackInfoPacketId"]]),
        )
        attack_start = first["attackStartDelaySeconds"]
        first_hit = first["firstHitDelaySeconds"]
        observations.append(
            {
                "capture": first["capture"],
                "sourceIdentity": first["sourceIdentity"],
                "targetIdentity": first["targetIdentity"],
                "specialAttackWeaponPacketId": first["specialAttackWeaponPacketId"],
                "attackPacketId": first["attackPacketId"],
                "firstAttackInfoPacketId": first["attackInfoPacketId"],
                "attackStartDelaySeconds": (
                    attack_start
                    if attack_start is not None and attack_start >= 0
                    else None
                ),
                "firstHitDelaySeconds": (
                    first_hit if first_hit is not None and first_hit >= 0 else None
                ),
            }
        )
    observations.sort(
        key=lambda row: packet_sort_key(packet_by_id[row["attackPacketId"]])
    )
    return observations


def observed_initial_ammo(
    rows: list[dict[str, Any]], packet_by_id: dict[str, PacketRecord]
) -> list[int]:
    grouped: dict[tuple[str, str, str, str], list[dict[str, Any]]] = defaultdict(list)
    for row in rows:
        grouped[
            (
                row["capture"],
                row["sourceIdentity"],
                row["targetIdentity"],
                row["attackPacketId"],
            )
        ].append(row)
    observations = []
    for group_rows in grouped.values():
        first = min(
            group_rows,
            key=lambda row: packet_sort_key(packet_by_id[row["attackInfoPacketId"]]),
        )
        observations.append(first["ammo"])
    return sorted(set(observations))


def validate_equipped_ammo_sequence(
    rows: list[dict[str, Any]],
    packet_by_id: dict[str, PacketRecord],
    representative_wifu_packet_id: str | None,
) -> tuple[int | None, list[int], list[str]]:
    grouped: dict[tuple[str, str, str], list[dict[str, Any]]] = defaultdict(list)
    for row in rows:
        wifu_id = row.get("weaponItemFullUpdatePacketId")
        if not wifu_id:
            return None, [], ["equipped AttackInfo has no owner-linked WIFU packet"]
        grouped[(row["capture"], row["sourceIdentity"], wifu_id)].append(row)

    initial_by_wifu_packet_id: dict[str, int] = {}
    problems = []
    for (capture, source, wifu_id), group_rows in grouped.items():
        wifu = packet_by_id[wifu_id]
        energy = wifu.decoded.get("energy")
        if energy == -1:
            initial = -1
        elif energy == 0:
            initial = 0
        elif isinstance(energy, int) and energy > 0:
            initial = energy - 1
        else:
            problems.append(
                f"{capture} source={source} WIFU has invalid Energy {energy}"
            )
            continue
        initial_by_wifu_packet_id[wifu_id] = initial
        ordered = sorted(
            group_rows,
            key=lambda row: packet_sort_key(packet_by_id[row["attackInfoPacketId"]]),
        )
        remaining = energy
        for index, row in enumerate(ordered):
            if energy == -1:
                expected = -1
            elif energy == 0:
                expected = 0
            elif remaining > 0:
                remaining -= 1
                expected = remaining
            else:
                problems.append(
                    f"{capture} source={source} WIFU {wifu_id} has AttackInfo after finite Energy exhaustion"
                )
                break
            if row["ammo"] != expected:
                problems.append(
                    f"{capture} source={source} WIFU {wifu_id} AttackInfo index={index} "
                    f"ammo={row['ammo']} expected={expected}"
                )
                break
    if representative_wifu_packet_id not in initial_by_wifu_packet_id:
        problems.append(
            "representative WIFU has no validated initial AttackInfo ammunition state"
        )
        return None, sorted(set(initial_by_wifu_packet_id.values())), problems
    return (
        initial_by_wifu_packet_id[representative_wifu_packet_id],
        sorted(set(initial_by_wifu_packet_id.values())),
        problems,
    )


def audit_uncertified_complete_chains(
    profiles: list[dict[str, Any]],
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    exclusions = []
    blockers = []
    exclusion_fields = (
        (
            "excludedConflictedSourceIdentities",
            "same capture-local source has conflicting invariant raw contracts",
        ),
        (
            "excludedCorrelationConflictSourceIdentities",
            "raw fight correlation has conflicting packet context",
        ),
        (
            "excludedInferredMetadataSourceIdentities",
            "capture-local generation metadata is absent or ambiguous",
        ),
    )
    for profile in profiles:
        if (
            profile["normalCompleteChainCount"] == 0
            or profile["captureCertifiedVariantCount"] > 0
        ):
            continue
        source_reasons: dict[str, set[str]] = defaultdict(set)
        for source in profile["ownedOrPetSourceIdentitiesExcluded"]:
            source_reasons[source].add(
                "capture-local SCFU has a nonempty owner identity; source is an owned/pet actor"
            )
        observed_sources = set()
        for variant in profile["variants"]:
            observed_sources.update(
                row["sourceIdentity"]
                for row in variant["rawWireVariantObservations"]
            )
            for field, reason in exclusion_fields:
                for source in variant[field]:
                    source_reasons[source].add(reason)
        unexplained_sources = sorted(observed_sources - set(source_reasons))
        metadata = profile.get("metadata") or {}
        row = {
            "profileKey": profile["profileKey"],
            "name": metadata.get("name"),
            "monsterData": metadata.get("monsterData"),
            "level": metadata.get("level"),
            "capturedRealmId": metadata.get("capturedRealmId"),
            "captureSessionsSearched": profile["captureSessionsSearched"],
            "normalCompleteChainCount": profile["normalCompleteChainCount"],
            "observedSourceIdentities": sorted(observed_sources),
            "documentedExclusions": [
                {
                    "sourceIdentity": source,
                    "reasons": sorted(reasons),
                }
                for source, reasons in sorted(source_reasons.items())
            ],
            "unexplainedSourceIdentities": unexplained_sources,
            "disabledGameplayCapability": profile["disabledCapability"],
        }
        exclusions.append(row)
        if unexplained_sources:
            blockers.append(row)
    return exclusions, blockers


def _referenced_packet_ids(
    value: Any, available_packet_ids: set[str], result: set[str]
) -> None:
    stack: list[Any] = [value]
    while stack:
        current = stack.pop()
        if isinstance(current, str):
            if current in available_packet_ids:
                result.add(current)
            continue
        if isinstance(current, dict):
            stack.extend(reversed(tuple(current.values())))
            continue
        if isinstance(current, (list, tuple)):
            stack.extend(reversed(current))


def _discover_combat_capture_paths() -> list[Path]:
    captures = set(discover_capture_directories(CAPTURE_ROOT))
    if LEGACY_CAPTURE_ROOT.exists():
        captures.update(
            packet_log.parent
            for packet_log in LEGACY_CAPTURE_ROOT.rglob("packets.hex.log")
        )
    return sorted(
        captures,
        key=lambda path: path.relative_to(CAPTURE_REPO_ROOT).as_posix(),
    )


def _snapshot_file_identity(path: Path, display_path: str) -> dict[str, Any]:
    _reject_symlink_or_reparse(path, "snapshot source", include_parent=True)
    path = path.resolve()
    if not path.exists():
        return {
            "path": display_path,
            "exists": False,
        }
    if not path.is_file() or path.is_symlink():
        raise CaptureAggregationInvariantError(
            f"snapshot source is not a regular file: {display_path}"
        )
    before = path.stat()
    sha256, byte_length = _stream_file_sha256_and_length(path)
    after = path.stat()
    before_identity = (
        before.st_size,
        before.st_mtime_ns,
        getattr(before, "st_ino", 0),
    )
    after_identity = (
        after.st_size,
        after.st_mtime_ns,
        getattr(after, "st_ino", 0),
    )
    if before_identity != after_identity or byte_length != after.st_size:
        raise CaptureAggregationInvariantError(
            f"snapshot source changed while hashing: {display_path}"
        )
    return {
        "path": display_path,
        "exists": True,
        "byteLength": byte_length,
        "sha256": sha256,
    }


def _snapshot_file_descriptor(
    path: Path, logical_root: Path = REPO_ROOT
) -> dict[str, Any]:
    _reject_symlink_or_reparse(path, "snapshot source", include_parent=True)
    path = path.resolve()
    relative_path = path.relative_to(logical_root).as_posix()
    return _snapshot_file_identity(path, relative_path)


def _verify_snapshot_file_identity(
    path: Path,
    expected: dict[str, Any],
    capture: str,
    phase: str,
    snapshot_identity: str,
    verify_hash: bool = False,
) -> None:
    _reject_symlink_or_reparse(path, "snapshot source", include_parent=True)
    path = path.resolve()
    display_path = str(expected.get("path"))
    try:
        if not path.exists():
            actual = {
                "path": display_path,
                "exists": False,
            }
        else:
            if not path.is_file() or path.is_symlink():
                raise RuntimeError("source is not a regular file")
            if verify_hash:
                actual = _snapshot_file_identity(path, display_path)
            else:
                stat = path.stat()
                actual = {
                    "path": display_path,
                    "exists": True,
                    "byteLength": stat.st_size,
                    "sha256": expected.get("sha256"),
                }
    except (OSError, RuntimeError) as error:
        raise _capture_snapshot_failure(
            capture,
            display_path,
            phase,
            snapshot_identity,
            f"could not revalidate input source: {error}",
        ) from error
    if actual != expected:
        raise _capture_snapshot_failure(
            capture,
            display_path,
            phase,
            snapshot_identity,
            "live input source identity changed",
        )


def _capture_source_entry(capture: Path) -> dict[str, Any]:
    _reject_symlink_or_reparse(
        capture, "capture source directory", include_parent=True
    )
    capture = capture.resolve()
    capture_key = capture.relative_to(CAPTURE_REPO_ROOT).as_posix()
    return {
        "capture": capture_key,
        "sourceFiles": [
            _snapshot_file_descriptor(capture / name, CAPTURE_REPO_ROOT)
            for name in sorted(CAPTURE_SNAPSHOT_SOURCE_NAMES)
        ],
    }


def _generator_source_descriptors() -> list[dict[str, Any]]:
    generator_script = Path(__file__)
    if not generator_script.is_absolute():
        generator_script = REPO_ROOT / generator_script
    paths = {
        generator_script,
        REPO_ROOT / "Tools" / "inventory_aosharp_captures.py",
        REPO_ROOT
        / "tools-temp"
        / "AOSharpLiveCapture"
        / "decode_npc_lifecycle_capture.py",
        SCFU_ANALYZER_PROJECT_SOURCE,
    }
    paths.update(
        REPO_ROOT / provenance["runtimeBindingSource"]
        for provenance in RESOURCE_MAPPING_PROVENANCE.values()
    )
    return [
        _snapshot_file_descriptor(path)
        for path in sorted(paths, key=lambda value: value.relative_to(REPO_ROOT).as_posix())
    ]


def _stage_short_scfu_analyzer(staging_root: Path) -> Path:
    _reject_symlink_or_reparse(
        SCFU_ANALYZER.parent,
        "frozen SCFU analyzer directory",
        include_parent=True,
    )
    staging_root = staging_root.resolve(strict=True)
    destination_root = staging_root / "a"
    destination_root.mkdir()
    source_root = SCFU_ANALYZER.parent.resolve(strict=True)
    copied = 0
    for source in sorted(source_root.rglob("*"), key=lambda path: path.as_posix()):
        _reject_symlink_or_reparse(source, "frozen SCFU analyzer member")
        if source.is_dir():
            continue
        if not source.is_file():
            raise RuntimeError(f"frozen SCFU analyzer member is not regular: {source}")
        relative = source.relative_to(source_root)
        destination = destination_root / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        with source.open("rb") as reader, destination.open("xb") as writer:
            shutil.copyfileobj(reader, writer, 1024 * 1024)
            writer.flush()
            os.fsync(writer.fileno())
        if _stream_file_sha256_and_length(destination) != (
            _stream_file_sha256_and_length(source)
        ):
            raise RuntimeError(f"short SCFU analyzer snapshot changed: {relative}")
        copied += 1
    executable = destination_root / SCFU_ANALYZER.name
    if copied == 0 or not executable.is_file():
        raise RuntimeError("short SCFU analyzer snapshot is incomplete")
    return executable


def _build_capture_source_plan(
    captures: Iterable[Path] | None = None,
) -> dict[str, Any]:
    capture_paths = list(
        captures if captures is not None else _discover_combat_capture_paths()
    )
    for capture_path in capture_paths:
        _reject_symlink_or_reparse(
            capture_path, "capture source directory", include_parent=True
        )
    capture_paths.sort(
        key=lambda path: path.resolve().relative_to(CAPTURE_REPO_ROOT).as_posix()
    )
    capture_entries = [_capture_source_entry(path) for path in capture_paths]
    core = {
        "schemaVersion": CAPTURE_SNAPSHOT_SCHEMA_VERSION,
        "generatorSources": _generator_source_descriptors(),
        "captures": capture_entries,
    }
    return {
        **core,
        "planIdentity": sha256_canonical(core),
    }


def _validate_capture_source_plan(plan: dict[str, Any]) -> None:
    if plan.get("schemaVersion") != CAPTURE_SNAPSHOT_SCHEMA_VERSION:
        raise CaptureAggregationInvariantError("capture input plan schema is invalid")
    captures = plan.get("captures")
    generator_sources = plan.get("generatorSources")
    if not isinstance(captures, list) or not isinstance(generator_sources, list):
        raise CaptureAggregationInvariantError("capture input plan members are invalid")
    capture_keys = [entry.get("capture") for entry in captures]
    if not all(isinstance(value, str) and value for value in capture_keys):
        raise CaptureAggregationInvariantError(
            "capture input plan contains an invalid capture path"
        )
    if capture_keys != sorted(capture_keys) or len(capture_keys) != len(set(capture_keys)):
        raise CaptureAggregationInvariantError(
            "capture input plan capture list is not sorted and unique"
        )
    generator_paths = [entry.get("path") for entry in generator_sources]
    if not all(isinstance(value, str) and value for value in generator_paths):
        raise CaptureAggregationInvariantError(
            "capture input plan contains an invalid generator source path"
        )
    if generator_paths != sorted(generator_paths):
        raise CaptureAggregationInvariantError(
            "capture input plan generator sources are not sorted"
        )
    for entry in captures:
        capture_path = Path(entry["capture"])
        if capture_path.is_absolute() or ".." in capture_path.parts:
            raise CaptureAggregationInvariantError(
                f"capture input plan path is unsafe: {entry['capture']}"
            )
        source_files = entry.get("sourceFiles")
        if not isinstance(source_files, list):
            raise CaptureAggregationInvariantError(
                f"capture input plan source list is invalid: {entry.get('capture')}"
            )
        source_paths = [source.get("path") for source in source_files]
        if source_paths != sorted(source_paths):
            raise CaptureAggregationInvariantError(
                f"capture input plan sources are not sorted: {entry.get('capture')}"
            )
    for descriptor in [
        *generator_sources,
        *(source for entry in captures for source in entry["sourceFiles"]),
    ]:
        if not isinstance(descriptor, dict):
            raise CaptureAggregationInvariantError(
                "capture input plan contains a non-object source descriptor"
            )
        expected_keys = (
            {"path", "exists", "byteLength", "sha256"}
            if descriptor.get("exists") is True
            else {"path", "exists"}
        )
        if set(descriptor) != expected_keys:
            raise CaptureAggregationInvariantError(
                f"capture input source descriptor is malformed: {descriptor.get('path')}"
            )
        descriptor_path = Path(str(descriptor.get("path")))
        if descriptor_path.is_absolute() or ".." in descriptor_path.parts:
            raise CaptureAggregationInvariantError(
                f"capture input source descriptor path is unsafe: {descriptor.get('path')}"
            )
        if descriptor.get("exists") is True:
            if (
                type(descriptor["byteLength"]) is not int
                or descriptor["byteLength"] < 0
                or not valid_sha256(descriptor["sha256"])
            ):
                raise CaptureAggregationInvariantError(
                    f"capture input source descriptor identity is invalid: {descriptor['path']}"
                )
    core = {
        "schemaVersion": plan["schemaVersion"],
        "generatorSources": generator_sources,
        "captures": captures,
    }
    expected_identity = sha256_canonical(core)
    if plan.get("planIdentity") != expected_identity:
        raise CaptureAggregationInvariantError(
            "capture input plan identity does not match its content"
        )


def _verify_capture_source_entry(
    expected: dict[str, Any],
    phase: str,
    snapshot_identity: str,
    verify_hash: bool = False,
) -> None:
    capture_key = str(expected.get("capture"))
    capture_path = CAPTURE_REPO_ROOT / Path(capture_key)
    _reject_symlink_or_reparse(
        capture_path, "capture source directory", include_parent=True
    )
    capture_path = capture_path.resolve()
    expected_paths = [
        (Path(capture_key) / name).as_posix()
        for name in sorted(CAPTURE_SNAPSHOT_SOURCE_NAMES)
    ]
    source_files = expected.get("sourceFiles", [])
    if [entry.get("path") for entry in source_files] != expected_paths:
        raise _capture_snapshot_failure(
            capture_key,
            capture_path,
            phase,
            snapshot_identity,
            "capture source manifest paths are invalid",
        )
    for source in source_files:
        _verify_snapshot_file_identity(
            CAPTURE_REPO_ROOT / Path(source["path"]),
            source,
            capture_key,
            phase,
            snapshot_identity,
            verify_hash=verify_hash,
        )


def _verify_capture_source_plan(
    plan: dict[str, Any],
    phase: str,
    snapshot_identity: str,
    verify_hash: bool = False,
) -> None:
    _validate_capture_source_plan(plan)
    expected_captures = [entry["capture"] for entry in plan["captures"]]
    actual_captures = [
        path.relative_to(CAPTURE_REPO_ROOT).as_posix()
        for path in _discover_combat_capture_paths()
    ]
    if actual_captures != expected_captures:
        changed_capture = next(
            (
                capture
                for capture in sorted(set(actual_captures) | set(expected_captures))
                if (capture in actual_captures) != (capture in expected_captures)
            ),
            "<capture-discovery>",
        )
        raise _capture_snapshot_failure(
            changed_capture,
            changed_capture,
            phase,
            snapshot_identity,
            "live capture discovery changed",
        )
    for source in plan["generatorSources"]:
        _verify_snapshot_file_identity(
            REPO_ROOT / Path(source["path"]),
            source,
            "<generator>",
            phase,
            snapshot_identity,
            verify_hash=verify_hash,
        )
    for entry in plan["captures"]:
        _verify_capture_source_entry(
            entry,
            phase,
            snapshot_identity,
            verify_hash=verify_hash,
        )


def _capture_session_snapshot_state(session: dict[str, Any]) -> dict[str, Any]:
    keys = (
        "capabilityStatus",
        "canonicalValid",
        "recaptureRequired",
        "captureComplete",
        "positiveEvidenceOnly",
        "absenceInferenceAllowed",
        "canonicalPackets",
        "conflictCount",
    )
    return {
        "disposition": "accepted" if session.get("canonicalValid") else "quarantined",
        **{key: session.get(key) for key in keys},
    }


def _build_input_snapshot_document(
    plan: dict[str, Any],
    snapshots: Iterable[CaptureShardSnapshot],
    sessions_by_capture: dict[str, dict[str, Any]],
) -> dict[str, Any]:
    source_by_capture = {
        entry["capture"]: entry["sourceFiles"] for entry in plan["captures"]
    }
    capture_entries = []
    for snapshot in sorted(snapshots, key=lambda value: value.capture):
        session = sessions_by_capture[snapshot.capture]
        capture_entries.append(
            {
                "capture": snapshot.capture,
                "captureId": session.get("captureId"),
                "sourceFiles": source_by_capture[snapshot.capture],
                "sessionState": _capture_session_snapshot_state(session),
                "shard": {
                    "path": snapshot.relative_path,
                    "byteLength": snapshot.byte_length,
                    "sha256": snapshot.sha256,
                },
            }
        )
    core = {
        "schemaVersion": CAPTURE_SNAPSHOT_SCHEMA_VERSION,
        "planIdentity": plan["planIdentity"],
        "generatorSources": plan["generatorSources"],
        "captures": capture_entries,
    }
    return {
        **core,
        "snapshotIdentity": sha256_canonical(core),
    }


def _input_snapshot_bytes(document: dict[str, Any]) -> bytes:
    return (canonical(document) + "\n").encode("utf-8")


def build_inventory(
    snapshot_directory: Path,
    source_plan: dict[str, Any],
) -> tuple[dict[str, Any], dict[str, Any]]:
    _validate_capture_source_plan(source_plan)
    plan_identity = source_plan["planIdentity"]
    captures = [
        CAPTURE_REPO_ROOT / Path(entry["capture"])
        for entry in source_plan["captures"]
    ]
    snapshot_directory.mkdir()
    snapshots = []
    for ordinal, (capture, source_entry) in enumerate(
        zip(captures, source_plan["captures"])
    ):
        snapshots.append(
            parse_capture_isolated_to_snapshot(
                capture,
                snapshot_directory,
                ordinal,
                source_entry,
                plan_identity,
            )
        )
    all_metadata: list[MetadataGeneration] = []
    sessions = []
    decode_errors = []
    sessions_by_capture: dict[str, dict[str, Any]] = {}
    for snapshot in snapshots:
        try:
            _, metadata, session, errors = _load_capture_snapshot_shard(
                snapshot,
                "metadata-index",
                plan_identity,
            )
        except Exception as error:
            raise RuntimeError(
                f"capture parsing failed for {snapshot.capture}: {error}"
            ) from error
        portable_session = _portable_capture_session(
            session,
            CAPTURE_REPO_ROOT / Path(snapshot.capture),
        )
        all_metadata.extend(metadata)
        sessions.append(portable_session)
        sessions_by_capture[snapshot.capture] = portable_session
        decode_errors.extend(errors)

    snapshot_document = _build_input_snapshot_document(
        source_plan,
        snapshots,
        sessions_by_capture,
    )
    snapshot_identity = snapshot_document["snapshotIdentity"]
    _verify_capture_source_plan(
        source_plan,
        "post-metadata-index",
        snapshot_identity,
    )

    corpus_metadata: dict[int, list[MetadataGeneration]] = defaultdict(list)
    for generation in all_metadata:
        corpus_metadata[generation.source].append(generation)

    complete: list[dict[str, Any]] = []
    incomplete: list[dict[str, Any]] = []
    unsupported: list[dict[str, Any]] = []
    lifecycle_records: list[PacketRecord] = []
    packet_by_id: dict[str, PacketRecord] = {}
    relevant_npc_packets_decoded = 0
    for snapshot in snapshots:
        try:
            records, metadata, _, _ = _load_capture_snapshot_shard(
                snapshot,
                "correlation",
                snapshot_identity,
            )
        except Exception as error:
            raise RuntimeError(
                f"capture correlation failed for {snapshot.capture}: {error}"
            ) from error

        local_metadata: dict[tuple[str, int], list[MetadataGeneration]] = defaultdict(
            list
        )
        for generation in metadata:
            local_metadata[(generation.capture, generation.source)].append(generation)
        for values in local_metadata.values():
            values.sort(key=lambda row: row.sequence)

        for record in records:
            selected_metadata, resolution = choose_metadata(
                record, local_metadata, corpus_metadata
            )
            record.metadata = selected_metadata
            record.metadata_resolution = resolution
            if selected_metadata is not None:
                relevant_npc_packets_decoded += 1

        capture_complete, capture_incomplete, capture_unsupported = correlate(records)
        complete.extend(capture_complete)
        incomplete.extend(capture_incomplete)
        unsupported.extend(capture_unsupported)

        local_packet_by_id = {record.packet_id: record for record in records}
        retained_packet_ids: set[str] = set()
        _referenced_packet_ids(
            (
                capture_complete,
                capture_incomplete,
                capture_unsupported,
            ),
            set(local_packet_by_id),
            retained_packet_ids,
        )
        for record in records:
            if record.message_type in {"StopFight", "Despawn"}:
                lifecycle_records.append(record)
                retained_packet_ids.add(record.packet_id)
        for packet_id in sorted(retained_packet_ids):
            packet_by_id[packet_id] = local_packet_by_id[packet_id]

        del (
            records,
            metadata,
            local_metadata,
            local_packet_by_id,
            retained_packet_ids,
            capture_complete,
            capture_incomplete,
            capture_unsupported,
        )
        gc.collect()

    del corpus_metadata
    complete, duplicate_chains = deduplicate_chains(complete, packet_by_id)
    profiles = build_profiles(
        complete,
        incomplete,
        unsupported,
        packet_by_id,
        all_metadata,
    )
    lifecycle_records.sort(key=packet_sort_key)
    sorted_sessions = sorted(sessions, key=lambda row: row["capture"])
    public_metadata_generations = [
        row.public()
        for row in sorted(
            all_metadata,
            key=lambda value: (value.capture, value.sequence, value.source),
        )
    ]
    capture_sessions_discovered = len(captures)
    capture_sessions_with_raw_sink = sum(
        1
        for row in sessions
        if row["packetLog"]["exists"] or row["rawPacketIndex"]["exists"]
    )
    canonical_valid_sessions = sum(1 for row in sessions if row["canonicalValid"])
    recapture_required_sessions = sum(
        1 for row in sessions if row["recaptureRequired"]
    )
    incomplete_observation_count = len(incomplete)
    incomplete_attack_info_count = sum(
        1 for row in incomplete if row.get("messageType") == "AttackInfo"
    )
    orphan_prefix_count = sum(
        1
        for row in incomplete
        if row.get("classification") == "orphan-combat-prefix"
    )
    unsupported_observation_count = len(unsupported)
    del captures, sessions, all_metadata, incomplete, unsupported
    gc.collect()
    compact_evidence = compact_packet_evidence(
        profiles,
        lifecycle_records,
        complete,
        packet_by_id,
        sorted_sessions,
        public_metadata_generations,
        snapshot_identity,
        "packet-evidence-compaction",
    )
    certified_profiles = [row for row in profiles if row["status"] == "capture-certified"]
    runtime_ready_profiles = [
        row for row in profiles if row["runtimeReadyVariantCount"] > 0
    ]
    semantic_definitions = sum(row["captureCertifiedVariantCount"] for row in profiles)
    runtime_generated_definitions = sum(
        row["captureCertifiedVariantCount"]
        for row in profiles
        if runtime_resource_from_public(row.get("metadata")) is not None
    )
    runtime_ready_definitions = sum(
        1
        for row in profiles
        if runtime_resource_from_public(row.get("metadata")) is not None
        for variant in row["variants"]
        if variant["captureCertified"] and variant["runtimeContractReady"]
    )
    complete_chain_exclusion_audit, recoverable_evidence_blockers = (
        audit_uncertified_complete_chains(profiles)
    )
    inventory = {
        "schemaVersion": 3,
        "generator": "tools-temp/AOSharpCaptureAnalyzer/extract_capture_backed_npc_combat.py",
        "authoritativeInputs": [
            "canonical reconciliation of packets.hex.log and raw-packets.csv",
            "canonical raw reconciliation of every SCFU against projections and the current decoder",
            "raw-only legacy capture directories under For Repo",
        ],
        "mutableRuntimeFields": [
            "source identity",
            "target identity",
            "landed amount",
            "WIFU MultipleCount",
            "current Energy/ammunition",
            "SpecialAttackWeapon Unknown5 state",
        ],
        "capturedRealmToRuntimeResource": {
            str(key): value for key, value in sorted(RESOURCE_BY_CAPTURED_REALM.items())
        },
        "capturedRealmMappingProvenance": {
            str(key): value for key, value in sorted(RESOURCE_MAPPING_PROVENANCE.items())
        },
        "summary": {
            "captureSessionsDiscovered": capture_sessions_discovered,
            "captureSessionsWithRawSink": capture_sessions_with_raw_sink,
            "canonicalValidSessions": canonical_valid_sessions,
            "recaptureRequiredSessions": recapture_required_sessions,
            "relevantNpcPacketsDecoded": relevant_npc_packets_decoded,
            "completeAttackInfoChains": len(complete),
            "deduplicatedOverlappingChains": duplicate_chains,
            "incompleteCombatSequenceObservations": incomplete_observation_count,
            "incompleteAttackInfoObservations": incomplete_attack_info_count,
            "orphanCombatPrefixObservations": orphan_prefix_count,
            "npcUnsupportedSequenceObservations": unsupported_observation_count,
            "npcLifecycleBoundaryObservations": len(lifecycle_records),
            "fullPacketProvenanceRecords": len(compact_evidence["packets"]),
            "compactPacketAuditLedgerRecords": len(
                compact_evidence["packetAuditLedger"]
            ),
            "packetAuditGroups": len(compact_evidence["packetAuditGroups"]),
            "captureCertifiedProfiles": len(certified_profiles),
            "runtimeReadyProfiles": len(runtime_ready_profiles),
            "captureCertifiedSemanticDefinitions": semantic_definitions,
            "runtimeGeneratedSemanticDefinitions": runtime_generated_definitions,
            "runtimeReadyGeneratedSemanticDefinitions": runtime_ready_definitions,
            "unresolvedProfiles": len(profiles) - len(certified_profiles),
            "runtimeUnresolvedProfiles": len(profiles) - len(runtime_ready_profiles),
            "uncertifiedCompleteChainProfilesWithDocumentedExclusions": len(
                complete_chain_exclusion_audit
            ),
            "recoverableEvidenceBlockers": len(recoverable_evidence_blockers),
            "decodeOrProjectionErrors": len(decode_errors),
        },
        "uncertifiedCompleteChainExclusionAudit": complete_chain_exclusion_audit,
        "recoverableEvidenceBlockers": recoverable_evidence_blockers,
        "sessions": sorted_sessions,
        "decodeOrProjectionErrors": sorted(
            decode_errors,
            key=lambda row: (
                row.get("capture", ""),
                row.get("sequence", -1),
                row.get("row", -1),
            ),
        ),
        "metadataGenerations": public_metadata_generations,
        "profiles": profiles,
        **compact_evidence,
    }
    _verify_capture_source_plan(
        source_plan,
        "inventory-return",
        snapshot_identity,
    )
    return inventory, snapshot_document


def canonical_json(value: dict[str, Any]) -> str:
    rows = []
    for key, member in value.items():
        rows.append(
            "  "
            + json.dumps(key, ensure_ascii=True)
            + ":"
            + json.dumps(
                member,
                ensure_ascii=True,
                separators=(",", ":"),
                sort_keys=False,
            )
        )
    return "{\n" + ",\n".join(rows) + "\n}\n"


def require_packet_provenance(
    packet: dict[str, Any],
    expected_message_type: str,
    required_fields: Iterable[str],
) -> None:
    packet_id = packet["packetId"]
    if packet["messageType"] != expected_message_type:
        raise ValueError(
            f"{packet_id}: expected {expected_message_type}, got {packet['messageType']}"
        )
    body = bytes.fromhex(packet["bodyHex"])
    if hashlib.sha256(body).hexdigest() != packet["bodySha256"]:
        raise ValueError(f"{packet_id}: body SHA-256 does not match bodyHex")
    provenance = packet["fields"].get("fieldProvenance") or {}
    for field in required_fields:
        value = provenance.get(field)
        if not value or not value.get("rawHex"):
            raise ValueError(f"{packet_id}: missing raw provenance for {field}")


def validate_compact_packet_evidence(inventory: dict[str, Any]) -> None:
    if inventory.get("schemaVersion") != 3:
        raise ValueError("capture-backed NPC combat inventory must use schema version 3")
    if "lifecycleBoundaryObservations" in inventory:
        raise ValueError("schema-v3 inventory retained expanded lifecycle observations")
    if inventory.get("packetAuditLedgerColumns") != list(
        PACKET_AUDIT_LEDGER_COLUMNS
    ):
        raise ValueError("packet audit ledger column contract is stale")
    if inventory.get("packetAuditLedgerPacketIdDerivation") != (
        PACKET_AUDIT_PACKET_ID_DERIVATION
    ):
        raise ValueError("packet audit ledger identity derivation is stale")

    groups = inventory["packetAuditGroups"]
    group_ids = [row["auditGroupId"] for row in groups]
    if group_ids != sorted(set(group_ids)):
        raise ValueError("packet audit groups are not uniquely deterministic")
    for group in groups:
        expected_group_id = "packet-audit-" + digest(
            group["derivation"], 32
        )
        if group["auditGroupId"] != expected_group_id:
            raise ValueError(
                f"{group['auditGroupId']}: group id does not match derivation"
            )
        if not valid_sha256(group["packetReferenceSha256"]):
            raise ValueError(
                f"{group['auditGroupId']}: invalid packet reference SHA-256"
            )

    tables = (
        "packetAuditArtifactTable",
        "packetAuditDirectionTable",
        "packetAuditMessageTypeTable",
        "packetAuditMetadataResolutionTable",
    )
    for table_name in tables:
        values = inventory[table_name]
        if values != sorted(set(values)):
            raise ValueError(f"{table_name} is not uniquely deterministic")

    sessions = inventory["sessions"]
    metadata_generations = inventory["metadataGenerations"]
    full_packets = inventory["packets"]
    full_packet_by_id = {row["packetId"]: row for row in full_packets}
    if len(full_packet_by_id) != len(full_packets):
        raise ValueError("generated full packet inventory contains duplicate identifiers")
    if [row["packetId"] for row in full_packets] != sorted(full_packet_by_id):
        raise ValueError("full packet provenance is not in deterministic packet order")

    members_by_group_index: dict[int, list[str]] = defaultdict(list)
    for packet in full_packets:
        packet_id = packet["packetId"]
        memberships = packet.get("auditGroupIndexes")
        if memberships != sorted(set(memberships or [])) or not memberships:
            raise ValueError(f"{packet_id}: invalid full-packet audit memberships")
        packet_bytes = bytes.fromhex(packet["packetHex"])
        body_bytes = bytes.fromhex(packet["bodyHex"])
        if hashlib.sha256(packet_bytes).hexdigest() != packet["packetSha256"]:
            raise ValueError(f"{packet_id}: full packet SHA-256 mismatch")
        if hashlib.sha256(body_bytes).hexdigest() != packet["bodySha256"]:
            raise ValueError(f"{packet_id}: full body SHA-256 mismatch")
        if packet["packetLength"] != len(packet_bytes):
            raise ValueError(f"{packet_id}: full packet length mismatch")
        for group_index in memberships:
            if not isinstance(group_index, int) or not 0 <= group_index < len(groups):
                raise ValueError(f"{packet_id}: invalid packet audit group index")
            members_by_group_index[group_index].append(packet_id)

    ledger = inventory["packetAuditLedger"]
    if inventory.get("packetAuditLedgerSha256") != positional_ledger_sha256(
        ledger
    ):
        raise ValueError("packet audit ledger SHA-256 is stale")
    ledger_packet_ids: set[str] = set()
    ledger_packet_id_order: list[str] = []
    artifact_table = inventory["packetAuditArtifactTable"]
    direction_table = inventory["packetAuditDirectionTable"]
    message_type_table = inventory["packetAuditMessageTypeTable"]
    metadata_resolution_table = inventory[
        "packetAuditMetadataResolutionTable"
    ]
    for row_index, row in enumerate(ledger):
        if len(row) != len(PACKET_AUDIT_LEDGER_COLUMNS):
            raise ValueError("packet audit ledger row has the wrong positional width")
        (
            capture_index,
            artifact_index,
            direction_index,
            sequence,
            global_ordinal,
            message_type_index,
            packet_length,
            body_length,
            packet_sha256_base64,
            body_sha256_base64,
            decoded_fields_sha256_base64,
            metadata_index,
            metadata_resolution_index,
            source_identity,
            target_identity,
            memberships,
        ) = row
        locator = f"packetAuditLedger[{row_index}]"
        if not isinstance(capture_index, int) or not 0 <= capture_index < len(sessions):
            raise ValueError(f"{locator}: invalid capture session index")
        if not isinstance(artifact_index, int) or not 0 <= artifact_index < len(artifact_table):
            raise ValueError(f"{locator}: invalid raw artifact index")
        if not isinstance(direction_index, int) or not 0 <= direction_index < len(direction_table):
            raise ValueError(f"{locator}: invalid packet direction index")
        if not isinstance(message_type_index, int) or not 0 <= message_type_index < len(message_type_table):
            raise ValueError(f"{locator}: invalid message type index")
        if not isinstance(metadata_resolution_index, int) or not 0 <= metadata_resolution_index < len(metadata_resolution_table):
            raise ValueError(f"{locator}: invalid metadata resolution index")
        if metadata_index is not None and (
            not isinstance(metadata_index, int)
            or not 0 <= metadata_index < len(metadata_generations)
        ):
            raise ValueError(f"{locator}: invalid metadata generation reference")
        direction = direction_table[direction_index]
        if direction not in {"IN", "OUT"} or not isinstance(sequence, int):
            raise ValueError(f"{locator}: invalid packet position")
        packet_sha256 = sha256_base64_to_hex(packet_sha256_base64)
        if packet_sha256 is None:
            raise ValueError(f"{locator}: invalid packet SHA-256")
        capture = sessions[capture_index]["capture"]
        packet_id = packet_audit_ledger_packet_id(row, sessions, direction_table)
        if packet_id in ledger_packet_ids or packet_id in full_packet_by_id:
            raise ValueError(f"{packet_id}: duplicate compact packet evidence")
        ledger_packet_ids.add(packet_id)
        ledger_packet_id_order.append(packet_id)
        expected_packet_prefix = f"{capture}|{direction}|{sequence}|"
        if not packet_id.startswith(expected_packet_prefix):
            raise ValueError(f"{packet_id}: positional raw artifact locator mismatch")
        if not packet_id.endswith(packet_sha256[:12]):
            raise ValueError(f"{packet_id}: packet hash locator mismatch")
        if not isinstance(packet_length, int) or packet_length < 16:
            raise ValueError(f"{packet_id}: invalid packet length")
        if body_length != packet_length - 16:
            raise ValueError(f"{packet_id}: invalid N3 body length")
        if global_ordinal is not None and not isinstance(global_ordinal, int):
            raise ValueError(f"{packet_id}: invalid global ordinal")
        for value, label in (
            (packet_sha256_base64, "packet"),
            (body_sha256_base64, "body"),
            (decoded_fields_sha256_base64, "decoded fields"),
        ):
            if sha256_base64_to_hex(value) is None:
                raise ValueError(f"{packet_id}: invalid {label} SHA-256")
        for identity, label in (
            (source_identity, "source"),
            (target_identity, "target"),
        ):
            if identity is not None and (
                not isinstance(identity, int)
                or identity < -0x80000000
                or identity > 0x7FFFFFFF
            ):
                raise ValueError(f"{packet_id}: invalid {label} identity")
        if memberships != sorted(set(memberships or [])) or not memberships:
            raise ValueError(f"{packet_id}: invalid compact audit memberships")
        for group_index in memberships:
            if not isinstance(group_index, int) or not 0 <= group_index < len(groups):
                raise ValueError(f"{packet_id}: invalid packet audit group index")
            members_by_group_index[group_index].append(packet_id)

    if ledger_packet_id_order != sorted(ledger_packet_id_order):
        raise ValueError("packet audit ledger is not in deterministic packet order")
    summary = inventory["summary"]
    if summary["fullPacketProvenanceRecords"] != len(full_packets):
        raise ValueError("full packet provenance summary count is stale")
    if summary["compactPacketAuditLedgerRecords"] != len(ledger):
        raise ValueError("compact packet audit ledger summary count is stale")
    if summary["packetAuditGroups"] != len(groups):
        raise ValueError("packet audit group summary count is stale")

    group_by_id = {row["auditGroupId"]: row for row in groups}
    group_index_by_id = {
        group_id: index for index, group_id in enumerate(group_ids)
    }
    for group_index, group in enumerate(groups):
        members = sorted(set(members_by_group_index.get(group_index, [])))
        if group["packetReferenceCount"] != len(members):
            raise ValueError(
                f"{group['auditGroupId']}: packet reference count mismatch"
            )
        if group["packetReferenceSha256"] != packet_reference_sha256(members):
            raise ValueError(
                f"{group['auditGroupId']}: packet reference digest mismatch"
            )
        if group["samplePacketIds"] != members[:3]:
            raise ValueError(
                f"{group['auditGroupId']}: packet reference samples mismatch"
            )

    referenced_group_ids: set[str] = set()

    def validate_reference(reference: dict[str, Any]) -> set[str]:
        group_id = reference["auditGroupId"]
        if group_id is None:
            if reference != packet_audit_reference(None):
                raise ValueError("empty packet audit reference is not deterministic")
            return set()
        group = group_by_id.get(group_id)
        if group is None:
            raise ValueError(f"{group_id}: packet audit reference has no group")
        expected = packet_audit_reference(group)
        if reference != expected:
            raise ValueError(f"{group_id}: packet audit reference summary mismatch")
        referenced_group_ids.add(group_id)
        group_index = group_index_by_id[group_id]
        return set(members_by_group_index[group_index])

    def require_reference_derivation(
        reference: dict[str, Any], expected: dict[str, Any]
    ) -> None:
        group_id = reference["auditGroupId"]
        if group_id is None:
            return
        if group_by_id[group_id]["derivation"] != expected:
            raise ValueError(f"{group_id}: packet audit derivation is stale")

    certified_packet_ids: set[str] = set()
    for profile in inventory["profiles"]:
        profile_key_value = profile["profileKey"]
        for variant in profile["variants"]:
            direct_packet_ids = variant_packet_references(variant)
            if validate_reference(variant["packetAudit"]) != direct_packet_ids:
                raise ValueError(
                    f"{variant['semanticProfileId']}: variant packet audit coverage mismatch"
                )
            require_reference_derivation(
                variant["packetAudit"],
                {
                    "kind": "combat-variant",
                    "profileKey": profile_key_value,
                    "semanticProfileId": variant["semanticProfileId"],
                },
            )
            if variant["captureCertified"]:
                certified_packet_ids.update(direct_packet_ids)
        for section in (
            "nonNormalObservations",
            "incompleteObservations",
            "unsupportedSequences",
        ):
            for observation_index, observation in enumerate(profile[section]):
                if any(
                    key in observation
                    for key in (
                        "packetIds",
                        "contextPacketIds",
                        "samplePacketIds",
                    )
                ):
                    raise ValueError(
                        f"{profile['profileKey']}: expanded packet identifiers remain in {section}"
                    )
                validate_reference(observation["packetAudit"])
                validate_reference(observation["contextPacketAudit"])
                derivation = {
                    "kind": "aggregated-observation",
                    "profileKey": profile_key_value,
                    "section": section,
                    "observationIndex": observation_index,
                }
                require_reference_derivation(
                    observation["packetAudit"],
                    {**derivation, "packetRole": "primary"},
                )
                require_reference_derivation(
                    observation["contextPacketAudit"],
                    {**derivation, "packetRole": "correlation-context"},
                )

    if set(full_packet_by_id) != certified_packet_ids:
        raise ValueError(
            "full packet provenance is not exactly the capture-certified variant corpus"
        )

    lifecycle = inventory["lifecycleBoundarySummary"]
    lifecycle_packet_ids: set[str] = set()
    for classification in lifecycle["classifications"]:
        reference = {
            key: classification[key]
            for key in (
                "auditGroupId",
                "packetReferenceCount",
                "packetReferenceSha256",
                "samplePacketIds",
            )
        }
        lifecycle_packet_ids.update(validate_reference(reference))
        require_reference_derivation(
            reference,
            {
                "kind": "lifecycle-boundary",
                "messageType": classification["messageType"],
            },
        )
    if lifecycle["observationCount"] != len(lifecycle_packet_ids):
        raise ValueError("lifecycle boundary compact count mismatch")
    if lifecycle["packetReferenceSha256"] != packet_reference_sha256(
        lifecycle_packet_ids
    ):
        raise ValueError("lifecycle boundary compact digest mismatch")
    if lifecycle["samplePacketIds"] != sorted(lifecycle_packet_ids)[:3]:
        raise ValueError("lifecycle boundary compact samples mismatch")
    if lifecycle["observationCount"] != inventory["summary"][
        "npcLifecycleBoundaryObservations"
    ]:
        raise ValueError("lifecycle boundary summary count is stale")
    if referenced_group_ids != set(group_ids):
        raise ValueError("packet audit group mapping contains orphan groups")


def validate_inventory(inventory: dict[str, Any]) -> None:
    if set(RESOURCE_BY_CAPTURED_REALM) != set(RESOURCE_MAPPING_PROVENANCE):
        missing = sorted(set(RESOURCE_BY_CAPTURED_REALM) - set(RESOURCE_MAPPING_PROVENANCE))
        extra = sorted(set(RESOURCE_MAPPING_PROVENANCE) - set(RESOURCE_BY_CAPTURED_REALM))
        raise ValueError(
            "captured-realm runtime mapping provenance is incomplete: "
            f"missing={missing} extra={extra}"
        )
    metadata_realms_by_session: dict[str, set[int]] = defaultdict(set)
    for generation in inventory.get("metadataGenerations", []):
        captured_realm = generation.get("capturedRealmId")
        if isinstance(captured_realm, int):
            metadata_realms_by_session[str(generation.get("capture", ""))].add(
                captured_realm
            )
    binding_source_text: dict[str, str] = {}
    required_provenance_fields = {
        "captureEvidenceSessions",
        "runtimeBindingSource",
        "runtimeBindingLiteral",
        "mappingBasis",
    }
    for captured_realm, runtime_resource in sorted(
        RESOURCE_BY_CAPTURED_REALM.items()
    ):
        provenance = RESOURCE_MAPPING_PROVENANCE[captured_realm]
        if set(provenance) != required_provenance_fields:
            raise ValueError(
                f"captured realm {captured_realm}: malformed runtime mapping provenance"
            )
        evidence_sessions = provenance["captureEvidenceSessions"]
        if not isinstance(evidence_sessions, tuple) or not evidence_sessions:
            raise ValueError(
                f"captured realm {captured_realm}: no capture evidence session"
            )
        for capture in evidence_sessions:
            if captured_realm not in metadata_realms_by_session.get(capture, set()):
                raise ValueError(
                    f"captured realm {captured_realm}: provenance session {capture} "
                    "does not contain matching raw-derived SCFU metadata"
                )
        binding_source = provenance["runtimeBindingSource"]
        binding_literal = provenance["runtimeBindingLiteral"]
        mapping_basis = provenance["mappingBasis"]
        if not all(
            isinstance(value, str) and value.strip()
            for value in (binding_source, binding_literal, mapping_basis)
        ):
            raise ValueError(
                f"captured realm {captured_realm}: empty runtime mapping provenance"
            )
        source_path = REPO_ROOT / binding_source
        _reject_symlink_or_reparse(
            source_path, "runtime binding source", include_parent=True
        )
        source_path = source_path.resolve()
        try:
            source_path.relative_to(REPO_ROOT)
        except ValueError as error:
            raise ValueError(
                f"captured realm {captured_realm}: runtime binding source is outside the repository"
            ) from error
        if binding_source not in binding_source_text:
            if not source_path.is_file():
                raise ValueError(
                    f"captured realm {captured_realm}: runtime binding source is missing"
                )
            binding_source_text[binding_source] = source_path.read_text(
                encoding="utf-8"
            )
        if binding_literal not in binding_source_text[binding_source]:
            raise ValueError(
                f"captured realm {captured_realm}: runtime binding literal is stale"
            )
        if f"= {runtime_resource};" not in binding_literal:
            raise ValueError(
                f"captured realm {captured_realm}: runtime binding literal does not prove "
                f"resource {runtime_resource}"
            )
    expected_resource_mapping = {
        str(key): value for key, value in sorted(RESOURCE_BY_CAPTURED_REALM.items())
    }
    expected_mapping_provenance = {
        str(key): value for key, value in sorted(RESOURCE_MAPPING_PROVENANCE.items())
    }
    if inventory.get("capturedRealmToRuntimeResource") != expected_resource_mapping:
        raise ValueError("captured-realm runtime resource mapping is stale")
    if inventory.get("capturedRealmMappingProvenance") != expected_mapping_provenance:
        raise ValueError("captured-realm runtime mapping provenance is stale")
    validate_compact_packet_evidence(inventory)
    exclusion_audit, recoverable_blockers = audit_uncertified_complete_chains(
        inventory["profiles"]
    )
    if inventory.get("uncertifiedCompleteChainExclusionAudit") != exclusion_audit:
        raise ValueError("uncertified complete-chain exclusion audit is stale")
    if inventory.get("recoverableEvidenceBlockers") != recoverable_blockers:
        raise ValueError("recoverable-evidence blocker audit is stale")
    if inventory["summary"].get(
        "uncertifiedCompleteChainProfilesWithDocumentedExclusions"
    ) != len(exclusion_audit):
        raise ValueError("uncertified complete-chain exclusion count is stale")
    if inventory["summary"].get("recoverableEvidenceBlockers") != len(
        recoverable_blockers
    ):
        raise ValueError("recoverable-evidence blocker count is stale")
    if recoverable_blockers:
        raise ValueError(
            "complete normal raw chains remain unintegrated without an exact "
            "conflict, ownership, or metadata exclusion"
        )
    packet_by_id = {row["packetId"]: row for row in inventory["packets"]}
    if len(packet_by_id) != len(inventory["packets"]):
        raise ValueError("generated packet inventory contains duplicate packet identifiers")

    certified_variants = 0
    for profile in inventory["profiles"]:
        for variant in profile["variants"]:
            if variant["captureEvidenceSafe"] != capture_evidence_is_safe(variant):
                raise ValueError(
                    f"{variant['semanticProfileId']}: capture-evidence safety flag is stale"
                )
            if not variant["captureCertified"]:
                continue
            certified_variants += 1
            sources = variant["sourceIdentities"]
            representative = variant["representativeEvidenceSourceIdentity"]
            if not sources or representative not in sources:
                raise ValueError(
                    f"{variant['semanticProfileId']}: representative source is not certified"
                )

            saw = packet_by_id[variant["representativeSawPacketId"]]
            saw_fields = saw["fields"]
            saw_required = [
                "messageId",
                "source.type",
                "source.instance",
                "n3Unknown",
                "encodedSpecialCount",
                "unknown1",
                "unknown2",
                "unknown3",
                "unknown4",
                "unknown5",
            ]
            for index in range(len(saw_fields["specials"])):
                saw_required.extend(
                    [
                        f"specials[{index}].lowTemplate",
                        f"specials[{index}].highTemplate",
                        f"specials[{index}].tag",
                        f"specials[{index}].name",
                    ]
                )
            require_packet_provenance(saw, "SpecialAttackWeapon", saw_required)
            if int(saw_fields["source"]["instanceHex"], 16) != int(
                representative, 16
            ):
                raise ValueError(
                    f"{variant['semanticProfileId']}: representative SAW source mismatch "
                    f"actual=0x{saw_fields['source']['instanceHex']} expected={representative}"
                )

            attack = packet_by_id[variant["representativeAttackPacketId"]]
            require_packet_provenance(
                attack,
                "Attack",
                (
                    "messageId",
                    "source.type",
                    "source.instance",
                    "n3Unknown",
                    "target.type",
                    "target.instance",
                    "action",
                ),
            )
            if int(attack["fields"]["source"]["instanceHex"], 16) != int(
                representative, 16
            ):
                raise ValueError(
                    f"{variant['semanticProfileId']}: representative Attack source mismatch "
                    f"actual=0x{attack['fields']['source']['instanceHex']} expected={representative}"
                )

            wifu_id = variant["representativeWifuPacketId"]
            if wifu_id:
                wifu = packet_by_id[wifu_id]
                wifu_fields = wifu["fields"]
                wifu_required = [
                    "messageId",
                    "weapon.type",
                    "weapon.instance",
                    "n3Unknown",
                    "unknown1",
                    "owner.type",
                    "owner.instance",
                    "playfieldId",
                    "stateMachine.type",
                    "stateMachine.instance",
                    "unknown2",
                    "encodedStatCount",
                    "unknown3",
                ]
                for index in range(len(wifu_fields["stats"])):
                    wifu_required.extend(
                        [f"stats[{index}].stat", f"stats[{index}].value"]
                    )
                require_packet_provenance(
                    wifu, "WeaponItemFullUpdate", wifu_required
                )
                if not wifu_fields.get("definitionComplete"):
                    raise ValueError(
                        f"{variant['semanticProfileId']}: generated WIFU is incomplete"
                    )
                if int(wifu_fields["owner"]["instanceHex"], 16) != int(
                    representative, 16
                ):
                    raise ValueError(
                        f"{variant['semanticProfileId']}: representative WIFU owner mismatch "
                        f"actual=0x{wifu_fields['owner']['instanceHex']} expected={representative}"
                    )

            stream_attack_info_packet_ids = {
                packet_id
                for stream in variant["streams"]
                for packet_id in stream["attackInfoPacketIds"]
            }
            raw_saw_packet_ids = set()
            raw_wifu_packet_ids = set()
            for observation in variant["rawWireVariantObservations"]:
                raw_saw = packet_by_id[observation["specialAttackWeaponPacketId"]]
                raw_saw_packet_ids.add(observation["specialAttackWeaponPacketId"])
                raw_attack = packet_by_id[observation["attackPacketId"]]
                raw_attack_info_id = observation["attackInfoPacketId"]
                if raw_attack_info_id not in stream_attack_info_packet_ids:
                    raise ValueError(
                        f"{variant['semanticProfileId']}: raw wire observation "
                        f"{raw_attack_info_id} is absent from its captured streams"
                    )
                raw_signature = {
                    "weaponContextKind": variant["baseSignature"][
                        "weaponContextKind"
                    ],
                    "specialAttackWeapon": saw_signature_from_decoded(
                        raw_saw["fields"]
                    ),
                    "attack": attack_signature_from_decoded(
                        raw_attack["fields"]
                    ),
                }
                raw_wifu_id = observation["weaponItemFullUpdatePacketId"]
                if raw_wifu_id:
                    raw_wifu_packet_ids.add(raw_wifu_id)
                    raw_signature["weaponItemFullUpdate"] = (
                        wifu_signature_from_decoded(
                            packet_by_id[raw_wifu_id]["fields"]
                        )
                    )
                if digest(raw_signature) != observation["baseSignatureId"]:
                    raise ValueError(
                        f"{variant['semanticProfileId']}: raw wire base signature "
                        f"does not match {raw_attack_info_id}"
                    )
                invariant_signature = dict(raw_signature)
                invariant_signature["specialAttackWeapon"] = (
                    saw_signature_from_decoded(
                        raw_saw["fields"], MUTABLE_SAW_FIELD_NAMES
                    )
                )
                if raw_wifu_id:
                    invariant_signature["weaponItemFullUpdate"] = (
                        wifu_signature_from_decoded(
                            packet_by_id[raw_wifu_id]["fields"],
                            MUTABLE_WIFU_STAT_IDS,
                        )
                    )
                if (
                    digest(invariant_signature)
                    != variant["invariantContractSignatureId"]
                ):
                    raise ValueError(
                        f"{variant['semanticProfileId']}: mutable raw packet variant changes "
                        "an invariant packet-contract field"
                    )

            mutable_saw_by_packet_id = {
                row["packetId"]: row
                for row in variant["mutableSawStateObservations"]
            }
            if set(mutable_saw_by_packet_id) != raw_saw_packet_ids:
                raise ValueError(
                    f"{variant['semanticProfileId']}: mutable SAW observation ledger "
                    "does not cover every raw SAW packet variant"
                )
            for raw_saw_id in sorted(raw_saw_packet_ids):
                raw_saw = packet_by_id[raw_saw_id]
                ledger = mutable_saw_by_packet_id[raw_saw_id]
                if (
                    ledger["unknown5"] != raw_saw["fields"]["unknown5"]
                    or ledger["unknown5RawHex"]
                    != raw_saw["fields"]["fieldProvenance"]["unknown5"][
                        "rawHex"
                    ]
                ):
                    raise ValueError(
                        f"{variant['semanticProfileId']}: mutable SAW state ledger "
                        f"does not reproduce {raw_saw_id}"
                    )

            mutable_wifu_by_packet_id = {
                row["packetId"]: row
                for row in variant["mutableWifuStateObservations"]
            }
            if set(mutable_wifu_by_packet_id) != raw_wifu_packet_ids:
                raise ValueError(
                    f"{variant['semanticProfileId']}: mutable WIFU observation ledger "
                    "does not cover every raw WIFU packet variant"
                )
            for raw_wifu_id in sorted(raw_wifu_packet_ids):
                raw_wifu = packet_by_id[raw_wifu_id]
                raw_wifu_fields = raw_wifu["fields"]
                raw_wifu_required = [
                    "messageId",
                    "weapon.type",
                    "weapon.instance",
                    "n3Unknown",
                    "unknown1",
                    "owner.type",
                    "owner.instance",
                    "playfieldId",
                    "stateMachine.type",
                    "stateMachine.instance",
                    "unknown2",
                    "encodedStatCount",
                    "unknown3",
                ]
                for index in range(len(raw_wifu_fields["stats"])):
                    raw_wifu_required.extend(
                        [f"stats[{index}].stat", f"stats[{index}].value"]
                    )
                require_packet_provenance(
                    raw_wifu, "WeaponItemFullUpdate", raw_wifu_required
                )
                mutable_stats = {
                    row["stat"]: row for row in raw_wifu_fields["stats"]
                }
                ledger = mutable_wifu_by_packet_id[raw_wifu_id]
                energy = mutable_stats[26]["value"]
                derived_initial_ammo = (
                    -1
                    if energy == -1
                    else 0
                    if energy == 0
                    else energy - 1
                    if energy > 0
                    else None
                )
                if (
                    ledger["multipleCount"] != mutable_stats[412]["value"]
                    or ledger["multipleCountRawValue"]
                    != mutable_stats[412]["rawValue"]
                    or ledger["energy"] != mutable_stats[26]["value"]
                    or ledger["energyRawValue"] != mutable_stats[26]["rawValue"]
                    or ledger["derivedInitialAttackInfoAmmo"]
                    != derived_initial_ammo
                ):
                    raise ValueError(
                        f"{variant['semanticProfileId']}: mutable WIFU state ledger "
                        f"does not reproduce {raw_wifu_id}"
                    )

            expected_weapon_state_candidates = [
                {"energy": energy, "multipleCount": multiple_count}
                for energy, multiple_count in sorted(
                    {
                        (row["energy"], row["multipleCount"])
                        for row in mutable_wifu_by_packet_id.values()
                    }
                )
            ]
            if (
                variant["runtimeMutableWeaponStateCandidates"]
                != expected_weapon_state_candidates
            ):
                raise ValueError(
                    f"{variant['semanticProfileId']}: runtime mutable weapon-state "
                    "candidates do not match the exact WIFU ledger"
                )
            expected_deterministic_initialization = (
                len({row["unknown5"] for row in mutable_saw_by_packet_id.values()})
                == 1
                and (
                    len(expected_weapon_state_candidates) == 1
                    if variant["baseSignature"]["weaponContextKind"] == "equipped"
                    else all(
                        len(stream["initialAmmoCandidates"]) == 1
                        for stream in variant["streams"]
                    )
                )
            )
            if (
                variant["deterministicRuntimeInitializationProven"]
                != expected_deterministic_initialization
            ):
                raise ValueError(
                    f"{variant['semanticProfileId']}: deterministic runtime state "
                    "classification does not match exact mutable evidence"
                )

            for stream in variant["streams"]:
                if not stream["damageObservations"]:
                    raise ValueError(
                        f"{variant['semanticProfileId']}: stream has no captured damage"
                    )
                attack_info_packets = [
                    packet_by_id[packet_id]
                    for packet_id in stream["attackInfoPacketIds"]
                ]
                for attack_info in attack_info_packets:
                    require_packet_provenance(
                        attack_info,
                        "AttackInfo",
                        (
                            "messageId",
                            "source.type",
                            "source.instance",
                            "n3Unknown",
                            "amount",
                            "ammo",
                            "weaponSlot",
                            "target.type",
                            "target.instance",
                            "damageTypeWire",
                            "hitTypeWire",
                            "weaponInstance",
                        ),
                    )
                observed_damage = [
                    row["fields"]["amount"] for row in attack_info_packets
                ]
                if observed_damage != stream["damageObservations"]:
                    raise ValueError(
                        f"{variant['semanticProfileId']}: damage observations are not raw-exact"
                    )
                observed_ammo = [
                    row["fields"]["ammo"] for row in attack_info_packets
                ]
                if observed_ammo != stream["ammoObservationsInOrder"]:
                    raise ValueError(
                        f"{variant['semanticProfileId']}: ammunition observations are not raw-exact"
                    )

    expected = inventory["summary"]["captureCertifiedSemanticDefinitions"]
    if certified_variants != expected:
        raise ValueError(
            "capture-certified semantic definition count does not match profile inventory"
        )
    validate_arete_item_template_attack_range_authority(inventory)


def csharp_string(value: str) -> str:
    return '"' + (value or "").replace("\\", "\\\\").replace('"', '\\"') + '"'


def csharp_int(value: int) -> str:
    return f"unchecked((int)0x{value & 0xFFFFFFFF:08X})"


def csharp_double(value: float | None) -> str:
    if value is None or value <= 0:
        return "0d"
    return format(value, ".12g") + "d"


def csharp_int_array(values: Iterable[int]) -> str:
    rows = list(values)
    return "new int[0]" if not rows else "new[] { " + ", ".join(map(str, rows)) + " }"


def csharp_double_array(values: Iterable[float]) -> str:
    rows = list(values)
    return (
        "new double[0]"
        if not rows
        else "new[] { " + ", ".join(format(value, ".12g") + "d" for value in rows) + " }"
    )


def should_emit_mutable_saw_replay(variant: dict[str, Any]) -> bool:
    weapon_context_kind = variant["baseSignature"]["weaponContextKind"]
    runtime_missing_evidence = variant["runtimeMissingEvidence"]
    legacy_reasons_only = all(
        reason == "capture-backed non-equipped attack range"
        or (
            weapon_context_kind in {"natural", "natural-or-special"}
            and reason
            == "exact specialized runtime sequence for multiple captured AttackInfo streams"
        )
        or reason.startswith(
            "deterministic runtime SpecialAttackWeapon Unknown5 state selection"
        )
        for reason in runtime_missing_evidence
    )
    has_production_ammo_state = any(
        " AttackInfo index=" in reason
        and " ammo=" in reason
        and " expected=" in reason
        for reason in runtime_missing_evidence
    )
    has_production_wifu_state = any(
        reason.startswith(
            "deterministic runtime WIFU Energy/MultipleCount state selection"
        )
        for reason in runtime_missing_evidence
    )
    two_state_production_extension = (
        weapon_context_kind == "equipped"
        and len(variant["mutableSawStateObservations"]) == 2
        and has_production_ammo_state
        and has_production_wifu_state
        and all(
            reason.startswith(
                "deterministic runtime SpecialAttackWeapon Unknown5 state selection"
            )
            or reason.startswith(
                "deterministic runtime WIFU Energy/MultipleCount state selection"
            )
            or (
                " AttackInfo index=" in reason
                and " ammo=" in reason
                and " expected=" in reason
            )
            for reason in runtime_missing_evidence
        )
    )
    return (
        variant["captureEvidenceSafe"]
        and not variant["deterministicRuntimeInitializationProven"]
        and len(variant["mutableSawStateObservations"]) > 1
        and (legacy_reasons_only or two_state_production_extension)
    )


def render_generated_catalog(inventory: dict[str, Any]) -> str:
    packet_by_id = {row["packetId"]: row for row in inventory["packets"]}
    definitions = []
    for profile in inventory["profiles"]:
        metadata = profile.get("metadata") or {}
        resource = runtime_resource_from_public(metadata)
        if resource is None:
            continue
        for variant in profile["variants"]:
            if not variant["captureCertified"] or not variant["sourceIdentities"]:
                continue
            saw_packet = packet_by_id[variant["representativeSawPacketId"]]
            saw = saw_packet["fields"]
            wifu_packet_id = variant["representativeWifuPacketId"]
            wifu_expression = "null"
            if wifu_packet_id:
                wifu_packet = packet_by_id[wifu_packet_id]
                wifu = wifu_packet["fields"]
                stat_rows = ",\n".join(
                    "                        new CapturedEnemyWeaponStatDefinition("
                    + f"(CharacterStat){row['stat']}, 0x{row['rawValue']:08X}u)"
                    for row in wifu["stats"]
                )
                wifu_expression = (
                    "new CapturedEnemyWeaponDefinition(\n"
                    f"                    {csharp_string(wifu_packet_id)},\n"
                    f"                    {csharp_int(int(variant['representativeEvidenceSourceIdentity'], 16))},\n"
                    f"                    (byte){wifu['n3Unknown']},\n"
                    f"                    {wifu['unknown1']},\n"
                    f"                    {wifu['inventorySlot']},\n"
                    f"                    {wifu['stateMachine']['type']},\n"
                    f"                    {wifu['stateMachine']['instance']},\n"
                    f"                    (short){wifu['unknown2']},\n"
                    "                    new[]\n"
                    "                    {\n"
                    f"{stat_rows}\n"
                    "                    },\n"
                    f"                    {wifu['unknown3']})"
                )
            special_rows = ",\n".join(
                "                        new CapturedEnemySpecialAttackDefinition("
                + f"{row['lowTemplate']}, {row['highTemplate']}, {row['tag']}, "
                + f"{csharp_string(bytes.fromhex(row['nameHex']).decode('latin-1'))})"
                for row in saw["specials"]
            )
            special_expression = (
                "new CapturedEnemySpecialAttackDefinition[0]"
                if not special_rows
                else "new[]\n                    {\n" + special_rows + "\n                    }"
            )
            stream_rows = []
            for stream in variant["streams"]:
                signature = stream["signature"]
                initial_ammo = stream["runtimeInitialAmmoCount"]
                if initial_ammo is None:
                    initial_ammo = -2147483648
                weapon_context_kind = variant["baseSignature"]["weaponContextKind"]
                captured_attack_range = stream.get("capturedAttackRange")
                captured_attack_range_expression = (
                    "null"
                    if captured_attack_range is None
                    else csharp_double(captured_attack_range)
                )
                stream_rows.append(
                    "                        new CapturedEnemyCombatProfileStreamDefinition("
                    + f"{stream['minimumObservedDamage']}, {stream['maximumObservedDamage']}, "
                    + f"{initial_ammo}, {signature['weaponSlot']}, "
                    + f"{signature['damageTypeWire']}, {signature['hitTypeWire']}, "
                    + f"{signature['weaponInstance']}, (byte){signature['n3Unknown']}, "
                    + f"{csharp_double(stream['representativeRechargeSeconds'])}, "
                    + f"{csharp_int_array(stream['damageObservations'])}, "
                    + f"{csharp_double_array(stream['attackStartDelayObservationsSeconds'])}, "
                    + f"{csharp_double_array(stream['firstHitDelayObservationsSeconds'])}, "
                    + f"{csharp_double_array(stream['landedIntervalObservationsSeconds'])}, "
                    + "0, "
                    + ("true" if weapon_context_kind == "equipped" else "false")
                    + f", {captured_attack_range_expression}, true, "
                    + ("true" if stream["capturedTerminalHitOnly"] else "false")
                    + ")"
                )
            sources = ", ".join(
                csharp_int(int(value, 16)) for value in variant["sourceIdentities"]
            )
            wifu_evidence_packet_ids = sorted({
                row["packetId"]
                for row in variant["mutableWifuStateObservations"]
            })
            saw_evidence_packet_ids = sorted({
                row["packetId"]
                for row in variant["mutableSawStateObservations"]
            })
            attack_evidence_packet_ids = sorted({
                row["attackPacketId"]
                for row in variant["rawWireVariantObservations"]
            })
            evidence = (
                "wifu="
                + (
                    ",".join(wifu_evidence_packet_ids)
                    if wifu_evidence_packet_ids
                    else "natural-none"
                )
                + "; saw="
                + ",".join(saw_evidence_packet_ids)
                + "; attack="
                + ",".join(attack_evidence_packet_ids)
                + "; attackInfo="
                + ",".join(
                    packet_id
                    for stream in variant["streams"]
                    for packet_id in stream["attackInfoPacketIds"]
                )
            )
            attack_range_evidence = variant.get("capturedAttackRangeEvidence")
            if attack_range_evidence is not None:
                evidence += (
                    "; attackRange="
                    + str(attack_range_evidence["evidenceId"])
                    + "@ItemDb.Stat287"
                )
            mutable_saw_state_observations = []
            seen_mutable_saw_packet_ids = set()
            for sibling in profile["variants"]:
                if (
                    sibling["sourceIdentities"] != variant["sourceIdentities"]
                    or sibling["invariantContractSignature"][
                        "specialAttackWeapon"
                    ]
                    != variant["invariantContractSignature"][
                        "specialAttackWeapon"
                    ]
                    or sibling["invariantContractSignature"]["attack"]
                    != variant["invariantContractSignature"]["attack"]
                ):
                    continue
                for observation in sibling["mutableSawStateObservations"]:
                    if observation["packetId"] in seen_mutable_saw_packet_ids:
                        continue
                    seen_mutable_saw_packet_ids.add(observation["packetId"])
                    mutable_saw_state_observations.append(observation)
            mutable_saw_state_observations.sort(
                key=lambda observation: (
                    packet_by_id[observation["packetId"]]["capture"],
                    (
                        packet_by_id[observation["packetId"]]["globalOrdinal"]
                        if packet_by_id[observation["packetId"]]["globalOrdinal"]
                        is not None
                        else packet_by_id[observation["packetId"]]["sequence"]
                    ),
                )
            )
            mutable_saw_observations = [
                row["unknown5"]
                for row in mutable_saw_state_observations
            ]
            mutable_saw_replay_is_complete = should_emit_mutable_saw_replay(
                variant
            )
            mutable_saw_argument = (
                ",\n                    "
                + csharp_int_array(mutable_saw_observations)
                if mutable_saw_replay_is_complete
                else ""
            )
            definition = (
                "                new CapturedEnemyCombatProfileDefinition(\n"
                f"                    {csharp_string(variant['semanticProfileId'])},\n"
                f"                    {csharp_string(evidence)},\n"
                f"                    {resource},\n"
                f"                    {csharp_string(metadata['name'])},\n"
                f"                    {metadata['monsterData']},\n"
                f"                    {metadata['level']},\n"
                f"                    {str(bool(profile['semanticFallbackCaptureProven'])).lower()},\n"
                f"                    {str(bool(variant['captureEvidenceSafe'])).lower()},\n"
                f"                    {str(bool(variant['deterministicRuntimeInitializationProven'])).lower()},\n"
                f"                    new[] {{ {sources} }},\n"
                f"                    {csharp_int(int(variant['representativeEvidenceSourceIdentity'], 16))},\n"
                f"                    {wifu_expression},\n"
                f"                    {special_expression},\n"
                f"                    (byte){saw['n3Unknown']},\n"
                f"                    {saw['unknown1']}, {saw['unknown2']}, {saw['unknown3']}, {saw['unknown4']}, {saw['unknown5']},\n"
                f"                    (byte){variant['baseSignature']['attack']['n3Unknown']},\n"
                f"                    (byte){variant['baseSignature']['attack']['action']},\n"
                "                    new[]\n"
                "                    {\n"
                + ",\n".join(stream_rows)
                + "\n                    }"
                + mutable_saw_argument
                + ")"
            )
            definitions.append((variant["semanticProfileId"], definition))
    definitions.sort(key=lambda row: row[0])
    body = ",\n".join(row[1] for row in definitions)
    return (
        "// <auto-generated />\n"
        "namespace AORebirth.Core.Playfields\n"
        "{\n"
        "    using SmokeLounge.AOtomation.Messaging.GameData;\n\n"
        "    internal static class CapturedEnemyCombatGeneratedProfiles\n"
        "    {\n"
        "        internal static CapturedEnemyCombatProfileDefinition[] Create()\n"
        "        {\n"
        "            return new[]\n"
        "            {\n"
        + body
        + "\n            };\n"
        "        }\n"
        "    }\n"
        "}\n"
    )


def render_generated_packet_fixtures(inventory: dict[str, Any]) -> str:
    def fixture_array(type_name: str, rows: list[str]) -> str:
        if not rows:
            return f"new {type_name}[0]"
        return "new[]\n                    {\n" + ",\n".join(rows) + "\n                    }"

    packet_by_id = {row["packetId"]: row for row in inventory["packets"]}
    definitions = []
    for profile in inventory["profiles"]:
        metadata = profile.get("metadata") or {}
        if runtime_resource_from_public(metadata) is None:
            continue
        for variant in profile["variants"]:
            if (
                not variant["captureCertified"]
                or not variant["sourceIdentities"]
            ):
                continue
            raw_observations = variant["rawWireVariantObservations"]
            mutable_wifu_by_id = {
                row["packetId"]: row
                for row in variant["mutableWifuStateObservations"]
            }
            mutable_saw_by_id = {
                row["packetId"]: row
                for row in variant["mutableSawStateObservations"]
            }
            wifu_rows = []
            for packet_id in sorted(
                {
                    row["weaponItemFullUpdatePacketId"]
                    for row in raw_observations
                    if row["weaponItemFullUpdatePacketId"]
                }
            ):
                packet = packet_by_id[packet_id]
                fields = packet["fields"]
                mutable = mutable_wifu_by_id[packet_id]
                wifu_rows.append(
                    "                        new CapturedEnemyWeaponPacketFixture("
                    + f"{csharp_string(packet_id)}, {csharp_string(packet['bodyHex'])}, "
                    + f"{csharp_int(fields['owner']['type'])}, {csharp_int(fields['owner']['instance'])}, "
                    + f"{fields['playfieldId']}, {csharp_int(fields['weapon']['type'])}, "
                    + f"{csharp_int(fields['weapon']['instance'])}, {mutable['energy']}, "
                    + f"{mutable['multipleCount']})"
                )
            saw_rows = []
            for packet_id in sorted(
                {row["specialAttackWeaponPacketId"] for row in raw_observations}
            ):
                packet = packet_by_id[packet_id]
                fields = packet["fields"]
                saw_rows.append(
                    "                        new CapturedEnemySpecialAttackWeaponPacketFixture("
                    + f"{csharp_string(packet_id)}, {csharp_string(packet['bodyHex'])}, "
                    + f"{csharp_int(fields['source']['type'])}, {csharp_int(fields['source']['instance'])}, "
                    + f"{mutable_saw_by_id[packet_id]['unknown5']})"
                )
            attack_rows = []
            for packet_id in sorted(
                {row["attackPacketId"] for row in raw_observations}
            ):
                packet = packet_by_id[packet_id]
                fields = packet["fields"]
                attack_rows.append(
                    "                        new CapturedEnemyAttackPacketFixture("
                    + f"{csharp_string(packet_id)}, {csharp_string(packet['bodyHex'])}, "
                    + f"{csharp_int(fields['source']['type'])}, {csharp_int(fields['source']['instance'])}, "
                    + f"{csharp_int(fields['target']['type'])}, {csharp_int(fields['target']['instance'])})"
                )
            attack_info_rows = []
            for stream in variant["streams"]:
                for packet_id in stream["attackInfoPacketIds"]:
                    packet = packet_by_id[packet_id]
                    fields = packet["fields"]
                    attack_info_rows.append(
                        "                        new CapturedEnemyAttackInfoPacketFixture("
                        + f"{csharp_string(stream['streamSignatureId'])}, "
                        + f"{csharp_string(packet_id)}, {csharp_string(packet['bodyHex'])}, "
                        + f"{csharp_int(fields['source']['type'])}, {csharp_int(fields['source']['instance'])}, "
                        + f"{csharp_int(fields['target']['type'])}, {csharp_int(fields['target']['instance'])}, "
                        + f"{fields['amount']}, {fields['ammo']}, {fields['weaponSlot']}, "
                        + f"{fields['damageTypeWire']}, {fields['hitTypeWire']}, "
                        + f"{fields['weaponInstance']}, (byte){fields['n3Unknown']})"
                    )
            definition = (
                "                new CapturedEnemyCombatPacketFixture(\n"
                f"                    {csharp_string(variant['semanticProfileId'])},\n"
                f"                    {fixture_array('CapturedEnemyWeaponPacketFixture', wifu_rows)},\n"
                f"                    {fixture_array('CapturedEnemySpecialAttackWeaponPacketFixture', saw_rows)},\n"
                f"                    {fixture_array('CapturedEnemyAttackPacketFixture', attack_rows)},\n"
                f"                    {fixture_array('CapturedEnemyAttackInfoPacketFixture', attack_info_rows)})"
            )
            definitions.append((variant["semanticProfileId"], definition))
    definitions.sort(key=lambda row: row[0])
    body = ",\n".join(row[1] for row in definitions)
    return (
        "// <auto-generated />\n"
        "namespace SmokeLounge.AOtomation.Messaging.Tests\n"
        "{\n"
        "    internal sealed class CapturedEnemyWeaponPacketFixture\n"
        "    {\n"
        "        internal CapturedEnemyWeaponPacketFixture(string packetId, string bodyHex, int ownerType, int ownerIdentity, int playfieldId, int weaponIdentityType, int weaponIdentityInstance, int energy, int multipleCount)\n"
        "        {\n"
        "            this.PacketId = packetId; this.BodyHex = bodyHex; this.OwnerType = ownerType; this.OwnerIdentity = ownerIdentity; this.PlayfieldId = playfieldId; this.WeaponIdentityType = weaponIdentityType; this.WeaponIdentityInstance = weaponIdentityInstance; this.Energy = energy; this.MultipleCount = multipleCount;\n"
        "        }\n"
        "        internal string PacketId { get; private set; }\n"
        "        internal string BodyHex { get; private set; }\n"
        "        internal int OwnerType { get; private set; }\n"
        "        internal int OwnerIdentity { get; private set; }\n"
        "        internal int PlayfieldId { get; private set; }\n"
        "        internal int WeaponIdentityType { get; private set; }\n"
        "        internal int WeaponIdentityInstance { get; private set; }\n"
        "        internal int Energy { get; private set; }\n"
        "        internal int MultipleCount { get; private set; }\n"
        "    }\n\n"
        "    internal sealed class CapturedEnemySpecialAttackWeaponPacketFixture\n"
        "    {\n"
        "        internal CapturedEnemySpecialAttackWeaponPacketFixture(string packetId, string bodyHex, int sourceType, int sourceIdentity, int unknown5)\n"
        "        {\n"
        "            this.PacketId = packetId; this.BodyHex = bodyHex; this.SourceType = sourceType; this.SourceIdentity = sourceIdentity; this.Unknown5 = unknown5;\n"
        "        }\n"
        "        internal string PacketId { get; private set; }\n"
        "        internal string BodyHex { get; private set; }\n"
        "        internal int SourceType { get; private set; }\n"
        "        internal int SourceIdentity { get; private set; }\n"
        "        internal int Unknown5 { get; private set; }\n"
        "    }\n\n"
        "    internal sealed class CapturedEnemyAttackPacketFixture\n"
        "    {\n"
        "        internal CapturedEnemyAttackPacketFixture(string packetId, string bodyHex, int sourceType, int sourceIdentity, int targetType, int targetIdentity)\n"
        "        {\n"
        "            this.PacketId = packetId; this.BodyHex = bodyHex; this.SourceType = sourceType; this.SourceIdentity = sourceIdentity; this.TargetType = targetType; this.TargetIdentity = targetIdentity;\n"
        "        }\n"
        "        internal string PacketId { get; private set; }\n"
        "        internal string BodyHex { get; private set; }\n"
        "        internal int SourceType { get; private set; }\n"
        "        internal int SourceIdentity { get; private set; }\n"
        "        internal int TargetType { get; private set; }\n"
        "        internal int TargetIdentity { get; private set; }\n"
        "    }\n\n"
        "    internal sealed class CapturedEnemyAttackInfoPacketFixture\n"
        "    {\n"
        "        internal CapturedEnemyAttackInfoPacketFixture(string streamSignatureId, string packetId, string bodyHex, int sourceType, int sourceIdentity, int targetType, int targetIdentity, int amount, int ammo, int weaponSlot, int damageTypeWire, int hitTypeWire, int weaponInstance, byte n3Unknown)\n"
        "        {\n"
        "            this.StreamSignatureId = streamSignatureId; this.PacketId = packetId; this.BodyHex = bodyHex; this.SourceType = sourceType; this.SourceIdentity = sourceIdentity; this.TargetType = targetType; this.TargetIdentity = targetIdentity; this.Amount = amount; this.Ammo = ammo; this.WeaponSlot = weaponSlot; this.DamageTypeWire = damageTypeWire; this.HitTypeWire = hitTypeWire; this.WeaponInstance = weaponInstance; this.N3Unknown = n3Unknown;\n"
        "        }\n"
        "        internal string StreamSignatureId { get; private set; }\n"
        "        internal string PacketId { get; private set; }\n"
        "        internal string BodyHex { get; private set; }\n"
        "        internal int SourceType { get; private set; }\n"
        "        internal int SourceIdentity { get; private set; }\n"
        "        internal int TargetType { get; private set; }\n"
        "        internal int TargetIdentity { get; private set; }\n"
        "        internal int Amount { get; private set; }\n"
        "        internal int Ammo { get; private set; }\n"
        "        internal int WeaponSlot { get; private set; }\n"
        "        internal int DamageTypeWire { get; private set; }\n"
        "        internal int HitTypeWire { get; private set; }\n"
        "        internal int WeaponInstance { get; private set; }\n"
        "        internal byte N3Unknown { get; private set; }\n"
        "    }\n\n"
        "    internal sealed class CapturedEnemyCombatPacketFixture\n"
        "    {\n"
        "        internal CapturedEnemyCombatPacketFixture(string profileId, CapturedEnemyWeaponPacketFixture[] weaponPackets, CapturedEnemySpecialAttackWeaponPacketFixture[] specialAttackWeaponPackets, CapturedEnemyAttackPacketFixture[] attackPackets, CapturedEnemyAttackInfoPacketFixture[] attackInfoPackets)\n"
        "        {\n"
        "            this.ProfileId = profileId; this.WeaponPackets = weaponPackets; this.SpecialAttackWeaponPackets = specialAttackWeaponPackets; this.AttackPackets = attackPackets; this.AttackInfoPackets = attackInfoPackets;\n"
        "        }\n"
        "        internal string ProfileId { get; private set; }\n"
        "        internal CapturedEnemyWeaponPacketFixture[] WeaponPackets { get; private set; }\n"
        "        internal CapturedEnemySpecialAttackWeaponPacketFixture[] SpecialAttackWeaponPackets { get; private set; }\n"
        "        internal CapturedEnemyAttackPacketFixture[] AttackPackets { get; private set; }\n"
        "        internal CapturedEnemyAttackInfoPacketFixture[] AttackInfoPackets { get; private set; }\n"
        "    }\n\n"
        "    internal static class CapturedEnemyCombatGeneratedPacketFixtures\n"
        "    {\n"
        "        internal static CapturedEnemyCombatPacketFixture[] Create()\n"
        "        {\n"
        "            return new[]\n"
        "            {\n"
        + body
        + "\n            };\n"
        "        }\n"
        "    }\n"
        "}\n"
    )


@dataclass(frozen=True)
class AggregateWorkerResult:
    directory: Path
    summary: dict[str, int]
    artifacts: dict[str, dict[str, Any]]
    input_snapshot: dict[str, Any]
    plan_identity: str


def render_accepted_inventory_artifacts(
    inventory_path: Path,
    catalog_output: Path,
    fixture_output: Path,
) -> None:
    """Render runtime artifacts from the promoted canonical inventory only."""
    inventory_payload = inventory_path.read_bytes()
    try:
        inventory = json.loads(inventory_payload.decode("utf-8"))
    except (UnicodeError, json.JSONDecodeError) as error:
        raise RuntimeError("accepted combat inventory is malformed") from error
    if not isinstance(inventory, dict):
        raise RuntimeError("accepted combat inventory root must be an object")
    if inventory_payload.replace(b"\r\n", b"\n") != canonical_json(inventory).encode(
        "utf-8"
    ):
        raise RuntimeError("accepted combat inventory is not canonical JSON")
    if not isinstance(inventory.get("profiles"), list) or not isinstance(
        inventory.get("packets"), list
    ):
        raise RuntimeError("accepted combat inventory lacks profiles or packets")

    outputs = {
        catalog_output: render_generated_catalog(inventory).encode("utf-8"),
        fixture_output: render_generated_packet_fixtures(inventory).encode("utf-8"),
    }
    identities = [os.path.normcase(str(path.resolve())) for path in outputs]
    if len(set(identities)) != len(identities):
        raise RuntimeError("accepted-inventory outputs must be distinct")
    for path, payload in outputs.items():
        _reject_symlink_or_reparse(
            path, "accepted-inventory generated output", include_parent=True
        )
        if not path.parent.is_dir():
            raise RuntimeError(
                f"accepted-inventory output parent is missing: {path.parent}"
            )
        temporary_path: Path | None = None
        try:
            with tempfile.NamedTemporaryFile(
                mode="wb",
                prefix=f".{path.name}.",
                suffix=".tmp",
                dir=path.parent,
                delete=False,
            ) as handle:
                temporary_path = Path(handle.name)
                handle.write(payload)
                handle.flush()
                os.fsync(handle.fileno())
            os.replace(temporary_path, path)
            temporary_path = None
        finally:
            if temporary_path is not None:
                temporary_path.unlink(missing_ok=True)


def _stream_file_sha256_and_length(path: Path) -> tuple[str, int]:
    checksum = hashlib.sha256()
    length = 0
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(1024 * 1024)
            if not chunk:
                break
            checksum.update(chunk)
            length += len(chunk)
    return checksum.hexdigest(), length


def _validate_temporary_path(
    path: Path,
    label: str,
    must_exist: bool,
) -> Path:
    _reject_symlink_or_reparse(path, label, include_parent=True)
    path = path.resolve()
    temporary_root = Path(tempfile.gettempdir()).resolve(strict=True)
    try:
        relative = path.relative_to(temporary_root)
    except ValueError as error:
        raise RuntimeError(f"{label} must stay under {temporary_root}") from error
    if not relative.parts:
        raise RuntimeError(f"{label} cannot target the temporary root")
    if not path.parent.is_dir():
        raise RuntimeError(f"{label} parent is not a regular directory: {path.parent}")
    if must_exist:
        if not path.is_file():
            raise RuntimeError(f"{label} is not a regular file: {path}")
    elif path.exists() and not path.is_file():
        raise RuntimeError(f"{label} target is not a regular file: {path}")
    return path


def _write_deterministic_temporary_file(
    path: Path,
    payload: bytes,
    label: str,
) -> None:
    path = _validate_temporary_path(path, label, must_exist=False)
    temporary_path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="wb",
            prefix=f".{path.name}.",
            suffix=".tmp",
            dir=path.parent,
            delete=False,
        ) as handle:
            temporary_path = Path(handle.name)
            handle.write(payload)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary_path, path)
        temporary_path = None
    finally:
        if temporary_path is not None:
            temporary_path.unlink(missing_ok=True)


def _load_capture_source_plan(path: Path) -> dict[str, Any]:
    path = _validate_temporary_path(
        path,
        "aggregate input snapshot plan",
        must_exist=True,
    )
    try:
        with path.open("r", encoding="utf-8") as handle:
            plan = json.load(handle)
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise RuntimeError(f"aggregate input snapshot plan is malformed: {error}") from error
    if not isinstance(plan, dict):
        raise RuntimeError("aggregate input snapshot plan is not an object")
    _validate_capture_source_plan(plan)
    return plan


def _snapshot_plan_from_document(document: dict[str, Any]) -> dict[str, Any]:
    return {
        "schemaVersion": document["schemaVersion"],
        "generatorSources": document["generatorSources"],
        "captures": [
            {
                "capture": entry["capture"],
                "sourceFiles": entry["sourceFiles"],
            }
            for entry in document["captures"]
        ],
        "planIdentity": document["planIdentity"],
    }


def _validate_input_snapshot_document(
    document: dict[str, Any],
    snapshot_root: Path,
    phase: str,
    validate_shards: bool = True,
    parse_shards: bool = False,
) -> tuple[dict[str, Any], list[CaptureShardSnapshot]]:
    required_keys = {
        "schemaVersion",
        "planIdentity",
        "snapshotIdentity",
        "generatorSources",
        "captures",
    }
    if not isinstance(document, dict) or set(document) != required_keys:
        raise RuntimeError("capture input snapshot schema members are invalid")
    if document["schemaVersion"] != CAPTURE_SNAPSHOT_SCHEMA_VERSION:
        raise RuntimeError("capture input snapshot schema version is invalid")
    captures = document["captures"]
    if not isinstance(captures, list):
        raise RuntimeError("capture input snapshot captures are invalid")
    if not all(isinstance(entry, dict) for entry in captures):
        raise RuntimeError("capture input snapshot contains a non-object capture")
    capture_keys = [entry.get("capture") for entry in captures]
    if capture_keys != sorted(capture_keys) or len(capture_keys) != len(set(capture_keys)):
        raise RuntimeError("capture input snapshot captures are not sorted and unique")
    core = {
        "schemaVersion": document["schemaVersion"],
        "planIdentity": document["planIdentity"],
        "generatorSources": document["generatorSources"],
        "captures": captures,
    }
    expected_identity = sha256_canonical(core)
    if document["snapshotIdentity"] != expected_identity:
        raise RuntimeError("capture input snapshot identity does not match its content")
    plan = _snapshot_plan_from_document(document)
    _validate_capture_source_plan(plan)
    snapshots = []
    _reject_symlink_or_reparse(
        snapshot_root, "capture input snapshot root", include_parent=True
    )
    snapshot_root = snapshot_root.resolve(strict=True)
    for entry in captures:
        if set(entry) != {
            "capture",
            "captureId",
            "sourceFiles",
            "sessionState",
            "shard",
        }:
            raise RuntimeError(
                f"capture input snapshot entry is malformed: {entry.get('capture')}"
            )
        shard = entry["shard"]
        if not isinstance(shard, dict) or set(shard) != {
            "path",
            "byteLength",
            "sha256",
        }:
            raise RuntimeError(
                f"capture input snapshot shard is malformed: {entry['capture']}"
            )
        if (
            not isinstance(entry["capture"], str)
            or not isinstance(entry["captureId"], str)
            or not isinstance(entry["sessionState"], dict)
            or not isinstance(shard["path"], str)
            or type(shard["byteLength"]) is not int
            or shard["byteLength"] < 0
            or not valid_sha256(shard["sha256"])
        ):
            raise RuntimeError(
                f"capture input snapshot entry identity is invalid: {entry.get('capture')}"
            )
        relative_path = Path(shard["path"])
        if relative_path.is_absolute() or ".." in relative_path.parts:
            raise RuntimeError(
                f"capture input snapshot shard path is unsafe: {entry['capture']}"
            )
        unresolved_shard_path = snapshot_root / relative_path
        _reject_symlink_or_reparse(
            unresolved_shard_path,
            "capture input snapshot shard",
            include_parent=True,
        )
        shard_path = unresolved_shard_path.resolve()
        try:
            shard_path.relative_to(snapshot_root)
        except ValueError as error:
            raise RuntimeError(
                f"capture input snapshot shard escaped its root: {entry['capture']}"
            ) from error
        snapshot = CaptureShardSnapshot(
            capture=entry["capture"],
            path=shard_path,
            relative_path=shard["path"],
            byte_length=shard["byteLength"],
            sha256=shard["sha256"],
        )
        if validate_shards:
            if parse_shards:
                _, _, session, _ = _load_capture_snapshot_shard(
                    snapshot,
                    phase,
                    document["snapshotIdentity"],
                )
                if entry["captureId"] != session.get("captureId"):
                    raise RuntimeError(
                        f"capture input snapshot captureId is inconsistent: {entry['capture']}"
                    )
                if entry["sessionState"] != _capture_session_snapshot_state(session):
                    raise RuntimeError(
                        f"capture input snapshot session state is inconsistent: {entry['capture']}"
                    )
            else:
                _verify_capture_snapshot_shard_identity(
                    snapshot,
                    phase,
                    document["snapshotIdentity"],
                )
        snapshots.append(snapshot)
    return plan, snapshots


def _load_and_revalidate_input_snapshot(
    path: Path,
    snapshot_root: Path,
    phase: str,
    revalidate_live: bool = True,
    verify_hash: bool = False,
    validate_shards: bool = False,
    parse_shards: bool = False,
) -> dict[str, Any]:
    path = _validate_temporary_path(
        path,
        "capture input snapshot manifest",
        must_exist=True,
    )
    try:
        with path.open("r", encoding="utf-8") as handle:
            document = json.load(handle)
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise RuntimeError(f"capture input snapshot manifest is malformed: {error}") from error
    plan, _ = _validate_input_snapshot_document(
        document,
        snapshot_root,
        phase,
        validate_shards=validate_shards,
        parse_shards=parse_shards,
    )
    if revalidate_live:
        _verify_capture_source_plan(
            plan,
            phase,
            document["snapshotIdentity"],
            verify_hash=verify_hash,
        )
    return document


def _validate_aggregate_worker_directory(directory: Path) -> Path:
    _reject_symlink_or_reparse(directory, "aggregate worker target")
    directory = directory.resolve(strict=True)
    if not directory.is_dir():
        raise RuntimeError(f"aggregate worker target is not a directory: {directory}")
    temporary_root = Path(tempfile.gettempdir()).resolve(strict=True)
    try:
        relative = directory.relative_to(temporary_root)
    except ValueError as error:
        raise RuntimeError(
            f"aggregate worker outputs must stay under {temporary_root}"
        ) from error
    if not relative.parts:
        raise RuntimeError("aggregate worker cannot target the temporary root itself")
    if next(directory.iterdir(), None) is not None:
        raise RuntimeError(f"aggregate worker target must be empty: {directory}")

    production_outputs = {
        OUTPUT.resolve(),
        CATALOG_OUTPUT.resolve(),
        FIXTURE_OUTPUT.resolve(),
    }
    for name in (
        *AGGREGATE_WORKER_ARTIFACT_NAMES.values(),
        AGGREGATE_WORKER_SUMMARY_NAME,
        AGGREGATE_INPUT_SNAPSHOT_NAME,
    ):
        target = directory / name
        if target.resolve() in production_outputs:
            raise RuntimeError("aggregate worker cannot write a production generated output")
    return directory


def _write_aggregate_worker_outputs(
    directory: Path,
    source_plan_path: Path,
    item_template_projection_path: Path,
    item_template_projection_sha256: str,
    item_template_projection_byte_length: int,
    item_database_sha256: str,
    item_database_byte_length: int,
) -> None:
    directory = _validate_aggregate_worker_directory(directory)
    source_plan = _load_capture_source_plan(source_plan_path)
    _verify_capture_source_plan(
        source_plan,
        "aggregate-worker-start",
        source_plan["planIdentity"],
        verify_hash=True,
    )
    inventory, input_snapshot_document = build_inventory(
        directory / CAPTURE_SNAPSHOT_DIRECTORY_NAME,
        source_plan,
    )
    projection_sha256, projection_byte_length = _stream_file_sha256_and_length(
        item_template_projection_path
    )
    if (
        projection_sha256 != item_template_projection_sha256
        or projection_byte_length != item_template_projection_byte_length
    ):
        raise RuntimeError("Arete attack-range item projection descriptor is stale")
    try:
        with item_template_projection_path.open("r", encoding="utf-8") as handle:
            item_template_projection = json.load(handle)
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise RuntimeError(
            f"Arete attack-range item projection is malformed: {error}"
        ) from error
    apply_arete_item_template_attack_range_authority(
        inventory,
        item_template_projection,
        item_database_sha256,
        item_database_byte_length,
    )
    validate_inventory(inventory)

    renderers = {
        "inventory": canonical_json,
        "catalog": render_generated_catalog,
        "fixtures": render_generated_packet_fixtures,
    }
    artifacts: dict[str, dict[str, Any]] = {}
    for kind, name in AGGREGATE_WORKER_ARTIFACT_NAMES.items():
        target = directory / name
        target.write_text(renderers[kind](inventory), encoding="utf-8")
        sha256, byte_length = _stream_file_sha256_and_length(target)
        artifacts[kind] = {
            "file": name,
            "byteLength": byte_length,
            "sha256": sha256,
        }

    input_snapshot_path = directory / AGGREGATE_INPUT_SNAPSHOT_NAME
    plan, _ = _validate_input_snapshot_document(
        input_snapshot_document,
        directory,
        "aggregate-worker-final",
        validate_shards=True,
        parse_shards=True,
    )
    _verify_capture_source_plan(
        plan,
        "aggregate-worker-final",
        input_snapshot_document["snapshotIdentity"],
        verify_hash=True,
    )
    _write_deterministic_temporary_file(
        input_snapshot_path,
        _input_snapshot_bytes(input_snapshot_document),
        "capture input snapshot manifest",
    )
    written_snapshot_document = _load_and_revalidate_input_snapshot(
        input_snapshot_path,
        directory,
        "aggregate-worker-return",
        validate_shards=True,
        parse_shards=True,
    )
    if (
        written_snapshot_document["snapshotIdentity"]
        != input_snapshot_document["snapshotIdentity"]
    ):
        raise RuntimeError("aggregate worker input snapshot changed before return")
    input_snapshot_sha256, input_snapshot_length = (
        _stream_file_sha256_and_length(input_snapshot_path)
    )
    input_snapshot_descriptor = {
        "file": AGGREGATE_INPUT_SNAPSHOT_NAME,
        "byteLength": input_snapshot_length,
        "sha256": input_snapshot_sha256,
        "snapshotIdentity": input_snapshot_document["snapshotIdentity"],
    }

    inventory_summary = inventory.get("summary")
    if not isinstance(inventory_summary, dict):
        raise RuntimeError("aggregate inventory has no summary")
    summary: dict[str, int] = {}
    for key in AGGREGATE_CLI_SUMMARY_KEYS:
        value = inventory_summary.get(key)
        if type(value) is not int:
            raise RuntimeError(f"aggregate inventory summary field is invalid: {key}")
        summary[key] = value
    payload = {
        "schemaVersion": 2,
        "summary": summary,
        "artifacts": artifacts,
        "inputSnapshot": input_snapshot_descriptor,
    }
    (directory / AGGREGATE_WORKER_SUMMARY_NAME).write_text(
        json.dumps(payload, ensure_ascii=True, separators=(",", ":"), sort_keys=True)
        + "\n",
        encoding="utf-8",
    )


def _load_aggregate_worker_result(
    directory: Path,
    expected_plan_identity: str | None = None,
) -> AggregateWorkerResult:
    _reject_symlink_or_reparse(
        directory, "aggregate worker result", include_parent=True
    )
    directory = directory.resolve(strict=True)
    summary_path = directory / AGGREGATE_WORKER_SUMMARY_NAME
    _reject_symlink_or_reparse(summary_path, "aggregate worker summary")
    if not summary_path.is_file():
        raise RuntimeError("aggregate worker did not write a regular summary file")
    if summary_path.stat().st_size > 1024 * 1024:
        raise RuntimeError("aggregate worker summary exceeds the compact size limit")
    try:
        with summary_path.open("r", encoding="utf-8") as handle:
            payload = json.load(handle)
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise RuntimeError(f"aggregate worker summary is malformed: {error}") from error
    if type(payload) is not dict or payload.get("schemaVersion") != 2:
        raise RuntimeError("aggregate worker summary schema is invalid")

    summary = payload.get("summary")
    if type(summary) is not dict or set(summary) != set(AGGREGATE_CLI_SUMMARY_KEYS):
        raise RuntimeError("aggregate worker CLI summary fields are invalid")
    for key in AGGREGATE_CLI_SUMMARY_KEYS:
        if type(summary[key]) is not int:
            raise RuntimeError(f"aggregate worker CLI summary field is invalid: {key}")

    artifacts = payload.get("artifacts")
    if type(artifacts) is not dict or set(artifacts) != set(
        AGGREGATE_WORKER_ARTIFACT_NAMES
    ):
        raise RuntimeError("aggregate worker artifact manifest is invalid")
    validated_artifacts: dict[str, dict[str, Any]] = {}
    for kind, expected_name in AGGREGATE_WORKER_ARTIFACT_NAMES.items():
        descriptor = artifacts[kind]
        if type(descriptor) is not dict or set(descriptor) != {
            "file",
            "byteLength",
            "sha256",
        }:
            raise RuntimeError(f"aggregate worker {kind} descriptor is invalid")
        if descriptor["file"] != expected_name:
            raise RuntimeError(f"aggregate worker {kind} filename is invalid")
        byte_length = descriptor["byteLength"]
        sha256 = descriptor["sha256"]
        if type(byte_length) is not int or byte_length < 0:
            raise RuntimeError(f"aggregate worker {kind} byte length is invalid")
        if (
            type(sha256) is not str
            or len(sha256) != 64
            or any(character not in "0123456789abcdef" for character in sha256)
        ):
            raise RuntimeError(f"aggregate worker {kind} SHA-256 is invalid")
        artifact_path = directory / expected_name
        _reject_symlink_or_reparse(
            artifact_path, f"aggregate worker {kind} artifact"
        )
        if not artifact_path.is_file():
            raise RuntimeError(f"aggregate worker {kind} artifact is missing")
        actual_sha256, actual_length = _stream_file_sha256_and_length(artifact_path)
        if actual_length != byte_length or actual_sha256 != sha256:
            raise RuntimeError(f"aggregate worker {kind} artifact is malformed")
        validated_artifacts[kind] = dict(descriptor)

    input_snapshot = payload.get("inputSnapshot")
    if type(input_snapshot) is not dict or set(input_snapshot) != {
        "file",
        "byteLength",
        "sha256",
        "snapshotIdentity",
    }:
        raise RuntimeError("aggregate worker input snapshot descriptor is invalid")
    if input_snapshot["file"] != AGGREGATE_INPUT_SNAPSHOT_NAME:
        raise RuntimeError("aggregate worker input snapshot filename is invalid")
    if type(input_snapshot["byteLength"]) is not int or input_snapshot["byteLength"] < 0:
        raise RuntimeError("aggregate worker input snapshot byte length is invalid")
    if not valid_sha256(input_snapshot["sha256"]):
        raise RuntimeError("aggregate worker input snapshot SHA-256 is invalid")
    if not valid_sha256(input_snapshot["snapshotIdentity"]):
        raise RuntimeError("aggregate worker input snapshot identity is invalid")
    input_snapshot_path = directory / AGGREGATE_INPUT_SNAPSHOT_NAME
    _reject_symlink_or_reparse(
        input_snapshot_path, "aggregate worker input snapshot"
    )
    actual_sha256, actual_length = _stream_file_sha256_and_length(
        input_snapshot_path
    )
    if (
        actual_length != input_snapshot["byteLength"]
        or actual_sha256 != input_snapshot["sha256"]
    ):
        raise RuntimeError("aggregate worker input snapshot manifest is malformed")
    input_snapshot_document = _load_and_revalidate_input_snapshot(
        input_snapshot_path,
        directory,
        "aggregate-parent-load",
        revalidate_live=False,
        validate_shards=True,
        parse_shards=True,
    )
    if (
        input_snapshot_document["snapshotIdentity"]
        != input_snapshot["snapshotIdentity"]
    ):
        raise RuntimeError("aggregate worker input snapshot identity is inconsistent")
    plan_identity = input_snapshot_document["planIdentity"]
    if (
        expected_plan_identity is not None
        and plan_identity != expected_plan_identity
    ):
        raise RuntimeError(
            "aggregate worker result plan identity does not match its parent plan"
        )

    return AggregateWorkerResult(
        directory=directory,
        summary={key: summary[key] for key in AGGREGATE_CLI_SUMMARY_KEYS},
        artifacts=validated_artifacts,
        input_snapshot=dict(input_snapshot),
        plan_identity=plan_identity,
    )


def _run_aggregate_worker_isolated(
    staging_root: Path,
    item_template_projection_path: Path,
    item_template_projection_sha256: str,
    item_template_projection_byte_length: int,
    item_database_sha256: str,
    item_database_byte_length: int,
) -> AggregateWorkerResult:
    script = _generator_script_path()
    _reject_symlink_or_reparse(staging_root, "aggregate staging root")
    staging_root = staging_root.resolve(strict=True)
    source_plan = _build_capture_source_plan()
    _validate_capture_source_plan(source_plan)
    _verify_capture_source_plan(
        source_plan,
        "aggregate-parent-plan",
        source_plan["planIdentity"],
        verify_hash=True,
    )
    source_plan_path = staging_root / AGGREGATE_INPUT_PLAN_NAME
    source_plan_payload = _input_snapshot_bytes(source_plan)
    _write_deterministic_temporary_file(
        source_plan_path,
        source_plan_payload,
        "aggregate input snapshot plan",
    )
    for attempt in range(1, AGGREGATE_WORKER_MAX_ATTEMPTS + 1):
        _reject_symlink_or_reparse(
            source_plan_path, "aggregate input snapshot plan", include_parent=True
        )
        if source_plan_path.read_bytes() != source_plan_payload:
            raise RuntimeError("aggregate input snapshot plan changed between attempts")
        _verify_capture_source_plan(
            source_plan,
            f"aggregate-parent-attempt-{attempt}",
            source_plan["planIdentity"],
        )
        attempt_directory = Path(tempfile.mkdtemp(
            prefix=f"attempt-{attempt}-",
            dir=staging_root,
        ))
        command = [
            sys.executable,
            "-B",
            "-I",
            "-u",
            "-X",
            "faulthandler",
            str(script),
            "--_aggregate-worker-directory",
            str(attempt_directory),
            "--_input-snapshot-plan",
            str(source_plan_path),
            "--item-template-projection",
            str(item_template_projection_path),
            "--item-template-projection-sha256",
            item_template_projection_sha256,
            "--item-template-projection-byte-length",
            str(item_template_projection_byte_length),
            "--item-database-sha256",
            item_database_sha256,
            "--item-database-byte-length",
            str(item_database_byte_length),
        ]
        _invoke_self_test_fault(
            "before_aggregate_worker_attempt",
            {"attempt": attempt, "directory": str(attempt_directory)},
        )
        try:
            with tempfile.TemporaryDirectory(prefix="aorg-") as analyzer_root_name:
                child_environment = None
                if os.name == "nt":
                    staged_analyzer = _stage_short_scfu_analyzer(
                        Path(analyzer_root_name)
                    )
                    child_environment = os.environ.copy()
                    child_environment[SCFU_ANALYZER_OVERRIDE_ENVIRONMENT] = str(
                        staged_analyzer
                    )
                completed = _run_bounded_child(
                    command,
                    cwd=REPO_ROOT,
                    timeout_seconds=AGGREGATE_WORKER_TIMEOUT_SECONDS,
                    label=f"aggregate worker attempt {attempt}",
                    environment=child_environment,
                )
        except BoundedChildTimeout as error:
            raise RuntimeError(
                f"deterministic aggregate worker timeout on attempt "
                f"{attempt}/{AGGREGATE_WORKER_MAX_ATTEMPTS}: {error}"
            ) from error
        if completed.returncode == 0:
            try:
                return _load_aggregate_worker_result(
                    attempt_directory,
                    expected_plan_identity=source_plan["planIdentity"],
                )
            except (OSError, RuntimeError) as error:
                raise RuntimeError(
                    "deterministic aggregate worker result validation failed "
                    f"on attempt {attempt}/{AGGREGATE_WORKER_MAX_ATTEMPTS}: {error}"
                ) from error

        detail = _capture_worker_failure_detail(completed)
        cancelled = _is_cancellation_child_failure(completed.returncode)
        native_failure = _is_native_child_failure(completed.returncode)
        interpreter_corruption = (
            not cancelled and _is_interpreter_corruption_detail(detail)
        )
        materialization_failure = (
            not cancelled and _is_capture_worker_materialization_detail(detail)
        )
        suffix = f": {detail}" if detail else ""
        if (
            (native_failure or interpreter_corruption or materialization_failure)
            and attempt < AGGREGATE_WORKER_MAX_ATTEMPTS
        ):
            _invoke_self_test_fault(
                "before_failed_aggregate_attempt_cleanup",
                {"attempt": attempt, "directory": str(attempt_directory)},
            )
            _remove_owned_directory_tree(
                staging_root,
                attempt_directory,
                f"attempt-{attempt}-",
            )
            continue
        if cancelled:
            kind = "cancelled aggregate worker"
        elif materialization_failure:
            kind = "materialization aggregate worker"
        elif native_failure or interpreter_corruption:
            kind = "native aggregate worker"
        else:
            kind = "deterministic aggregate worker"
        raise RuntimeError(
            f"{kind} failed with exit code {completed.returncode} "
            f"on attempt {attempt}/{AGGREGATE_WORKER_MAX_ATTEMPTS}{suffix}"
        )
    raise AssertionError("aggregate worker retry loop exited unexpectedly")


def _artifact_destinations(
    output: Path,
    catalog_output: Path,
    fixture_output: Path,
    input_snapshot_manifest: Path | None = None,
) -> dict[str, Path]:
    destinations = {
        "inventory": output,
        "catalog": catalog_output,
        "fixtures": fixture_output,
    }
    collision_paths = [*destinations.values()]
    if input_snapshot_manifest is not None:
        collision_paths.append(input_snapshot_manifest)
    identities = []
    for path in collision_paths:
        _reject_symlink_or_reparse(
            path, "generated output", include_parent=True
        )
        identities.append(os.path.normcase(str(path.resolve())))
    hardlink_collision = any(
        os.path.lexists(left)
        and os.path.lexists(right)
        and os.path.samefile(left, right)
        for index, left in enumerate(collision_paths)
        for right in collision_paths[index + 1 :]
    )
    if len(set(identities)) != len(identities) or hardlink_collision:
        raise RuntimeError(
            "generated outputs and the input snapshot manifest must be distinct"
        )
    return destinations


def _write_aggregate_artifacts_atomically(
    result: AggregateWorkerResult, destinations: dict[str, Path]
) -> None:
    prepared: dict[str, Path] = {}
    try:
        for kind, destination in destinations.items():
            destination.parent.mkdir(parents=True, exist_ok=True)
            source = result.directory / AGGREGATE_WORKER_ARTIFACT_NAMES[kind]
            descriptor = result.artifacts[kind]
            temporary_path: Path | None = None
            try:
                with tempfile.NamedTemporaryFile(
                    mode="wb",
                    prefix=f".{destination.name}.",
                    suffix=".tmp",
                    dir=destination.parent,
                    delete=False,
                ) as temporary_handle:
                    temporary_path = Path(temporary_handle.name)
                    with source.open("rb") as source_handle:
                        shutil.copyfileobj(
                            source_handle, temporary_handle, 1024 * 1024
                        )
                    temporary_handle.flush()
                    os.fsync(temporary_handle.fileno())
                actual_sha256, actual_length = _stream_file_sha256_and_length(
                    temporary_path
                )
                if (
                    actual_length != descriptor["byteLength"]
                    or actual_sha256 != descriptor["sha256"]
                ):
                    raise RuntimeError(f"atomic {kind} staging copy did not verify")
                prepared[kind] = temporary_path
            except BaseException:
                if temporary_path is not None:
                    temporary_path.unlink(missing_ok=True)
                raise

        for kind, destination in destinations.items():
            os.replace(prepared.pop(kind), destination)
    finally:
        for temporary_path in prepared.values():
            temporary_path.unlink(missing_ok=True)

    for kind, destination in destinations.items():
        descriptor = result.artifacts[kind]
        actual_sha256, actual_length = _stream_file_sha256_and_length(destination)
        if (
            actual_length != descriptor["byteLength"]
            or actual_sha256 != descriptor["sha256"]
        ):
            raise RuntimeError(f"atomic {kind} output verification failed: {destination}")


def _aggregate_artifact_matches(
    result: AggregateWorkerResult, kind: str, destination: Path
) -> bool:
    source = result.directory / AGGREGATE_WORKER_ARTIFACT_NAMES[kind]
    _reject_symlink_or_reparse(source, f"aggregate candidate {kind}")
    _reject_symlink_or_reparse(
        destination, f"governed golden {kind}", include_parent=True
    )
    if not source.is_file() or not destination.is_file():
        return False
    descriptor = result.artifacts[kind]
    if source.stat().st_size != descriptor["byteLength"]:
        return False
    if destination.stat().st_size != descriptor["byteLength"]:
        return False
    with source.open("rb") as candidate, destination.open("rb") as golden:
        while True:
            candidate_chunk = candidate.read(1024 * 1024)
            golden_chunk = golden.read(1024 * 1024)
            if candidate_chunk != golden_chunk:
                return False
            if not candidate_chunk:
                return True


def _revalidate_aggregate_input_snapshot(
    result: AggregateWorkerResult,
    phase: str,
    verify_hash: bool = False,
) -> dict[str, Any]:
    path = result.directory / AGGREGATE_INPUT_SNAPSHOT_NAME
    descriptor = result.input_snapshot
    actual_sha256, actual_length = _stream_file_sha256_and_length(path)
    if (
        actual_length != descriptor["byteLength"]
        or actual_sha256 != descriptor["sha256"]
    ):
        raise RuntimeError(
            f"aggregate input snapshot changed phase={phase} path={path}"
        )
    document = _load_and_revalidate_input_snapshot(
        path,
        result.directory,
        phase,
        verify_hash=verify_hash,
    )
    if document["snapshotIdentity"] != descriptor["snapshotIdentity"]:
        raise RuntimeError(
            f"aggregate input snapshot identity changed phase={phase} path={path}"
        )
    if document["planIdentity"] != result.plan_identity:
        raise RuntimeError(
            f"aggregate input plan identity changed phase={phase} path={path}"
        )
    return document


def _export_input_snapshot_manifest(
    result: AggregateWorkerResult,
    destination: Path,
) -> None:
    destination = _validate_temporary_path(
        destination,
        "requested input snapshot manifest",
        must_exist=False,
    )
    source = result.directory / AGGREGATE_INPUT_SNAPSHOT_NAME
    payload = source.read_bytes()
    _write_deterministic_temporary_file(
        destination,
        payload,
        "requested input snapshot manifest",
    )
    actual_sha256, actual_length = _stream_file_sha256_and_length(destination)
    if (
        actual_length != result.input_snapshot["byteLength"]
        or actual_sha256 != result.input_snapshot["sha256"]
    ):
        raise RuntimeError(
            f"requested input snapshot manifest copy did not verify: {destination}"
        )


def _validate_exported_input_snapshot_manifest(path: Path) -> dict[str, Any]:
    path = _validate_temporary_path(
        path,
        "exported input snapshot manifest",
        must_exist=True,
    )
    return _load_and_revalidate_input_snapshot(
        path,
        path.parent,
        "coordinator-publication-validation",
        verify_hash=True,
        validate_shards=False,
    )


def _self_test_arete_attack_range_authority() -> None:
    templates = {
        str(template_id): {
            "stats": {str(ATTACK_RANGE_STAT_ID): 2},
        }
        for template_id in ARETE_EXACT_NATURAL_ATTACK_RANGE_TEMPLATE_IDS
    }
    profiles = []
    for profile_key, families in sorted(
        ARETE_EXACT_NATURAL_ATTACK_RANGE_PROFILES.items()
    ):
        specials = [
            {
                "nameHex": name.encode("latin-1").hex().upper(),
                "lowTemplate": low_template,
                "highTemplate": (
                    low_template
                    if "name=Cleaning Robot" in profile_key
                    else high_template
                ),
            }
            for name, low_template, high_template in families
        ]
        profiles.append(
            {
                "profileKey": profile_key,
                "metadata": None,
                "runtimeReadyVariantCount": 0,
                "variants": [
                    {
                        "semanticProfileId": digest(profile_key),
                        "captureCertified": True,
                        "representativeSawPacketId": "self-test|IN|1|000000000000",
                        "baseSignature": {
                            "specialAttackWeapon": {"specials": specials}
                        },
                        "runtimeMissingEvidence": [
                            "exact specialized runtime sequence for multiple captured AttackInfo streams",
                            "capture-backed non-equipped attack range",
                        ],
                        "streams": [
                            {
                                "runtimeMissingEvidence": [
                                    "capture-backed non-equipped attack range"
                                ],
                                "runtimeContractReady": False,
                            }
                        ],
                    }
                ],
            }
        )
    inventory = {
        "authoritativeInputs": [],
        "profiles": profiles,
        "summary": {},
    }
    item_database_sha256 = "1" * 64
    apply_arete_item_template_attack_range_authority(
        inventory,
        {"templates": templates},
        item_database_sha256,
        1234,
    )
    validate_arete_item_template_attack_range_authority(inventory)
    assert all(
        variant["capturedAttackRangeMeters"] == 2.0
        and all(
            stream["capturedAttackRange"] == 2.0
            for stream in variant["streams"]
        )
        for profile in profiles
        for variant in profile["variants"]
    )
    conflicting_projection = json.loads(json.dumps({"templates": templates}))
    conflicting_projection["templates"]["120911"]["stats"][
        str(ATTACK_RANGE_STAT_ID)
    ] = 3
    try:
        apply_arete_item_template_attack_range_authority(
            {
                "authoritativeInputs": [],
                "profiles": json.loads(json.dumps(profiles)),
                "summary": {},
            },
            conflicting_projection,
            item_database_sha256,
            1234,
        )
    except ValueError as error:
        assert "not invariant" in str(error)
    else:
        raise AssertionError("conflicting ItemDb endpoint ranges were accepted")


def self_test() -> None:
    global _SELF_TEST_FAULT_HOOK

    _self_test_arete_attack_range_authority()
    assert _is_native_child_failure(-11)
    assert _is_native_child_failure(0xC0000005)
    assert _is_cancellation_child_failure(0xC000013A)
    assert _is_cancellation_child_failure(-signal.SIGINT)
    assert not _is_native_child_failure(0xC000013A)
    assert not _is_native_child_failure(-signal.SIGINT)
    assert not _is_native_child_failure(1)
    assert _is_interpreter_corruption_detail(
        "SystemError: unknown opcode 204"
    )
    assert _is_interpreter_corruption_detail(
        "SystemError: bad format string: O|OO:startswith"
    )
    assert _is_interpreter_corruption_detail(
        "AttributeError: 'datetime.timezone' object has no attribute 'astimezone'"
    )
    assert _is_interpreter_corruption_detail(
        "C:\\Python\\Lib\\json\\decoder.py\n"
        "ValueError: invalid literal for int() with base 10: '0'"
    )
    assert _is_interpreter_corruption_detail(
        "direction = (direction or '').strip().upper()\n"
        "AttributeError: 'function' object has no attribute 'strip'"
    )
    assert _is_interpreter_corruption_detail(
        "C:\\repo\\tools-temp\\AOSharpLiveCapture\\decode_npc_lifecycle_capture.py\n"
        "TypeError: 'int' object is not subscriptable"
    )
    assert _is_interpreter_corruption_detail(
        "C:\\repo\\tools-temp\\AOSharpCaptureAnalyzer\\"
        "extract_capture_backed_npc_combat.py\n"
        "in decode_special_attack_weapon\n"
        "TypeError: 'bytes' object does not support item assignment"
    )
    assert not _is_interpreter_corruption_detail(
        "TypeError: deterministic capture schema failure"
    )
    assert not _is_interpreter_corruption_detail(
        "ValueError: deterministic capture failure"
    )
    assert _is_capture_worker_materialization_detail(
        "RuntimeError: capture worker shard materialization failed: malformed JSON"
    )
    assert _is_capture_worker_materialization_detail(
        "RuntimeError: capture aggregation snapshot materialization failed "
        "capture=session phase=metadata-index: malformed JSON"
    )
    assert not _is_capture_worker_materialization_detail(
        "RuntimeError: deterministic capture failure"
    )
    assert AGGREGATE_WORKER_MAX_ATTEMPTS == 8
    historical_generation_key = (
        "tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260529-212034"
        "|0x788DA39B|scfu=105263"
    )
    missing_key_record = type(
        "MissingKeyRecord",
        (),
        {
            "metadata": type(
                "MissingKeyMetadata",
                (),
                {"generation_key": historical_generation_key},
            )(),
            "packet_id": "self-test-packet",
            "capture": "self-test-session",
        },
    )()
    try:
        _metadata_generation_index_for_packet(
            missing_key_record,
            {},
            "a" * 64,
            "self-test-controlled-fault",
        )
    except CaptureAggregationInvariantError as error:
        diagnostic = str(error)
        assert f"missingGenerationKey={historical_generation_key}" in diagnostic
        assert "owningPacket=self-test-packet" in diagnostic
        assert "owningSession=self-test-session" in diagnostic
        assert f"snapshot={'a' * 64}" in diagnostic
        assert "phase=self-test-controlled-fault" in diagnostic
    else:
        raise AssertionError("missing aggregation key was accepted")
    try:
        _run_bounded_child(
            [sys.executable, "-B", "-I", "-c", "import time; time.sleep(30)"],
            cwd=REPO_ROOT,
            timeout_seconds=1,
            label="self-test child",
        )
    except BoundedChildTimeout as error:
        assert "self-test child timed out after 1 seconds" in str(error)
    else:
        raise AssertionError("bounded child timeout was not enforced")

    fault_events: list[tuple[str, dict[str, Any]]] = []
    previous_fault_hook = _SELF_TEST_FAULT_HOOK
    try:
        _SELF_TEST_FAULT_HOOK = (
            lambda event, context: fault_events.append((event, dict(context)))
        )
        _invoke_self_test_fault("self-test-fault", {"attempt": 2})
    finally:
        _SELF_TEST_FAULT_HOOK = previous_fault_hook
    assert fault_events == [("self-test-fault", {"attempt": 2})]

    with tempfile.TemporaryDirectory(
        prefix="aorebirth-npc-combat-aggregate-self-test-"
    ) as aggregate_test_name:
        aggregate_test_root = Path(aggregate_test_name)
        logical_capture = (
            "tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260701-000001"
        )
        portable_sessions = []
        for checkout_name in ("checkout-a", "checkout-b"):
            frozen_capture = aggregate_test_root / checkout_name / Path(logical_capture)
            frozen_capture.mkdir(parents=True)
            for source_name in ("packets.hex.log", "raw-packets.csv"):
                (frozen_capture / source_name).write_bytes(b"")
            portable_sessions.append(
                _portable_capture_session(
                    {
                        "capture": logical_capture,
                        "packetLog": {
                            "path": str(frozen_capture / "packets.hex.log")
                        },
                        "rawPacketIndex": {
                            "path": str(frozen_capture / "raw-packets.csv")
                        },
                    },
                    frozen_capture,
                )
            )
        assert canonical(portable_sessions[0]) == canonical(portable_sessions[1])
        portable_identity_path = aggregate_test_root / "portable-identity.bin"
        portable_identity_path.write_bytes(b"portable identity")
        portable_identity_before = _snapshot_file_identity(
            portable_identity_path,
            "self-test/portable-identity.bin",
        )
        portable_replacement = aggregate_test_root / "portable-replacement.bin"
        portable_replacement.write_bytes(b"portable identity")
        os.replace(portable_replacement, portable_identity_path)
        portable_identity_after = _snapshot_file_identity(
            portable_identity_path,
            "self-test/portable-identity.bin",
        )
        assert portable_identity_before == portable_identity_after

        owned_test_root = aggregate_test_root / "owned-retries"
        owned_test_root.mkdir()
        owned_attempt = owned_test_root / "attempt-1-self-test"
        owned_attempt.mkdir()
        (owned_attempt / "partial-result").write_bytes(b"partial")
        _remove_owned_directory_tree(
            owned_test_root,
            owned_attempt,
            "attempt-1-",
        )
        assert not owned_attempt.exists()
        unowned_attempt = owned_test_root / "unowned"
        unowned_attempt.mkdir()
        try:
            _remove_owned_directory_tree(
                owned_test_root,
                unowned_attempt,
                "attempt-1-",
            )
        except RuntimeError as error:
            assert "refusing to remove unowned temporary directory" in str(error)
        else:
            raise AssertionError("retry cleanup removed an unowned directory")
        assert unowned_attempt.is_dir()

        worker_directory = aggregate_test_root / "worker"
        worker_directory.mkdir()
        assert _validate_aggregate_worker_directory(worker_directory) == (
            worker_directory.resolve()
        )

        randomized_capture_paths = [
            REPO_ROOT / "__snapshot-self-test__" / "20260701-000003",
            REPO_ROOT / "__snapshot-self-test__" / "20260701-000001",
            REPO_ROOT / "__snapshot-self-test__" / "20260701-000002",
        ]
        randomized = list(randomized_capture_paths)
        random.Random(8675309).shuffle(randomized)
        assert _build_capture_source_plan(randomized) == (
            _build_capture_source_plan(reversed(randomized_capture_paths))
        )

        shard_test_root = aggregate_test_root / "snapshot-shard-tests"
        shard_test_root.mkdir()

        def write_self_test_shard(capture: str, ordinal: int) -> CaptureShardSnapshot:
            session = {
                "capture": capture,
                "captureId": Path(capture).name,
                "capabilityStatus": "raw_source_complete",
                "canonicalValid": True,
                "recaptureRequired": False,
                "captureComplete": True,
                "positiveEvidenceOnly": False,
                "absenceInferenceAllowed": True,
                "canonicalPackets": 0,
                "conflictCount": 0,
            }
            payload = _parse_capture_payload(([], [], session, []))
            path = shard_test_root / f"capture-{ordinal:06d}.json"
            path.write_text(canonical(payload), encoding="utf-8")
            sha256, byte_length = _stream_file_sha256_and_length(path)
            return CaptureShardSnapshot(
                capture=capture,
                path=path,
                relative_path=(
                    "snapshot-shard-tests/"
                    f"capture-{ordinal:06d}.json"
                ),
                byte_length=byte_length,
                sha256=sha256,
            )

        self_test_snapshots = [
            write_self_test_shard("self-test/20260701-000001", 0),
            write_self_test_shard("self-test/20260701-000002", 1),
        ]
        for snapshot in self_test_snapshots:
            first_load = _parse_capture_payload(
                _load_capture_snapshot_shard(
                    snapshot,
                    "self-test-first-load",
                    "a" * 64,
                )
            )
            second_load = _parse_capture_payload(
                _load_capture_snapshot_shard(
                    snapshot,
                    "self-test-second-load",
                    "a" * 64,
                )
            )
            assert first_load == second_load

        original_json_loads = json.loads

        def raise_json_value_error(_: object) -> Any:
            raise ValueError("invalid literal for int() with base 10: '0'")

        json.loads = raise_json_value_error
        try:
            try:
                _load_capture_snapshot_shard(
                    self_test_snapshots[0],
                    "self-test-json-value-error",
                    "a" * 64,
                )
            except CaptureShardMaterializationError as error:
                assert "phase=self-test-json-value-error" in str(error)
                assert "invalid literal for int() with base 10: '0'" in str(error)
                assert isinstance(error.__cause__, ValueError)
            else:
                raise AssertionError("plain JSON ValueError bypassed shard retry handling")
        finally:
            json.loads = original_json_loads

        snapshot_plan_core = {
            "schemaVersion": CAPTURE_SNAPSHOT_SCHEMA_VERSION,
            "generatorSources": [],
            "captures": [
                {
                    "capture": snapshot.capture,
                    "sourceFiles": [],
                }
                for snapshot in self_test_snapshots
            ],
        }
        snapshot_plan = {
            **snapshot_plan_core,
            "planIdentity": sha256_canonical(snapshot_plan_core),
        }
        snapshot_sessions = {
            snapshot.capture: _load_capture_snapshot_shard(
                snapshot,
                "self-test-session-load",
                snapshot_plan["planIdentity"],
            )[2]
            for snapshot in self_test_snapshots
        }
        randomized_snapshots = list(self_test_snapshots)
        random.Random(314159).shuffle(randomized_snapshots)
        first_snapshot_document = _build_input_snapshot_document(
            snapshot_plan,
            randomized_snapshots,
            snapshot_sessions,
        )
        second_snapshot_document = _build_input_snapshot_document(
            snapshot_plan,
            reversed(randomized_snapshots),
            snapshot_sessions,
        )
        assert _input_snapshot_bytes(first_snapshot_document) == (
            _input_snapshot_bytes(second_snapshot_document)
        )
        _validate_input_snapshot_document(
            first_snapshot_document,
            aggregate_test_root,
            "self-test-manifest-cross-check",
            validate_shards=True,
            parse_shards=True,
        )
        capture_id_mismatch = json.loads(json.dumps(first_snapshot_document))
        capture_id_mismatch["captures"][0]["captureId"] = "wrong-capture-id"
        capture_id_core = {
            key: value
            for key, value in capture_id_mismatch.items()
            if key != "snapshotIdentity"
        }
        capture_id_mismatch["snapshotIdentity"] = sha256_canonical(capture_id_core)
        try:
            _validate_input_snapshot_document(
                capture_id_mismatch,
                aggregate_test_root,
                "self-test-capture-id-mismatch",
                validate_shards=True,
                parse_shards=True,
            )
        except RuntimeError as error:
            assert "captureId is inconsistent" in str(error)
        else:
            raise AssertionError("manifest captureId mismatch was accepted")

        session_state_mismatch = json.loads(json.dumps(first_snapshot_document))
        session_state_mismatch["captures"][0]["sessionState"][
            "canonicalPackets"
        ] = 99
        session_state_core = {
            key: value
            for key, value in session_state_mismatch.items()
            if key != "snapshotIdentity"
        }
        session_state_mismatch["snapshotIdentity"] = sha256_canonical(
            session_state_core
        )
        try:
            _validate_input_snapshot_document(
                session_state_mismatch,
                aggregate_test_root,
                "self-test-session-state-mismatch",
                validate_shards=True,
                parse_shards=True,
            )
        except RuntimeError as error:
            assert "session state is inconsistent" in str(error)
        else:
            raise AssertionError("manifest session state mismatch was accepted")

        tampered_snapshot = self_test_snapshots[0]
        original_shard_payload = tampered_snapshot.path.read_bytes()
        tampered_snapshot.path.write_bytes(original_shard_payload + b"tamper")
        try:
            _load_capture_snapshot_shard(
                tampered_snapshot,
                "self-test-shard-tamper",
                first_snapshot_document["snapshotIdentity"],
            )
        except CaptureAggregationInvariantError as error:
            assert "snapshot shard identity changed" in str(error)
            assert "phase=self-test-shard-tamper" in str(error)
        else:
            raise AssertionError("tampered capture snapshot shard was accepted")
        tampered_snapshot.path.write_bytes(b'{"schemaVersion":')
        partial_sha256, partial_length = _stream_file_sha256_and_length(
            tampered_snapshot.path
        )
        partial_snapshot = CaptureShardSnapshot(
            capture=tampered_snapshot.capture,
            path=tampered_snapshot.path,
            relative_path=tampered_snapshot.relative_path,
            byte_length=partial_length,
            sha256=partial_sha256,
        )
        try:
            _load_capture_snapshot_shard(
                partial_snapshot,
                "self-test-partial-json",
                first_snapshot_document["snapshotIdentity"],
            )
        except CaptureAggregationInvariantError as error:
            assert "phase=self-test-partial-json" in str(error)
        else:
            raise AssertionError("partial capture snapshot JSON was accepted")
        tampered_snapshot.path.write_bytes(original_shard_payload)

        mutable_input = aggregate_test_root / "mutable-input.log"
        mutable_input.write_bytes(b"before")
        expected_input = _snapshot_file_identity(
            mutable_input,
            "self-test/mutable-input.log",
        )
        mutable_input.write_bytes(b"after")
        try:
            _verify_snapshot_file_identity(
                mutable_input,
                expected_input,
                "self-test/20260701-000001",
                "self-test-input-change",
                first_snapshot_document["snapshotIdentity"],
                verify_hash=True,
            )
        except CaptureAggregationInvariantError as error:
            assert "self-test/mutable-input.log" in str(error)
            assert "phase=self-test-input-change" in str(error)
        else:
            raise AssertionError("changed snapshot input was accepted")

        missing_metadata = MetadataGeneration(
            capture="self-test/20260701-000001",
            capture_id="20260701-000001",
            sequence=7,
            global_ordinal=7,
            source=1,
            name="Self Test",
            monster_data=2,
            level=3,
            captured_realm_id=127,
            projection="self-test",
            packet_sha256="0" * 64,
            scfu_special_attacks="",
            owner_identity="",
        )
        missing_metadata_packet = PacketRecord(
            packet_id="self-test|IN|8|missing",
            capture=missing_metadata.capture,
            capture_id=missing_metadata.capture_id,
            captured_utc="2026-07-01T00:00:00Z",
            direction="IN",
            sequence=8,
            global_ordinal=8,
            message_type="AttackInfo",
            packet_hex="",
            body_hex="",
            packet_sha256="0" * 64,
            body_sha256="0" * 64,
            canonical_source="self-test",
            decoded={},
            metadata=missing_metadata,
        )
        try:
            _metadata_generation_index_for_packet(
                missing_metadata_packet,
                {},
                first_snapshot_document["snapshotIdentity"],
                "self-test-metadata-key",
            )
        except CaptureAggregationInvariantError as error:
            message = str(error)
            assert missing_metadata.generation_key in message
            assert missing_metadata_packet.packet_id in message
            assert missing_metadata_packet.capture in message
            assert first_snapshot_document["snapshotIdentity"] in message
            assert "phase=self-test-metadata-key" in message
        else:
            raise AssertionError("missing metadata generation key was accepted")

        artifacts: dict[str, dict[str, Any]] = {}
        artifact_payloads: dict[str, bytes] = {}
        for kind, name in AGGREGATE_WORKER_ARTIFACT_NAMES.items():
            payload = (kind + "-self-test\r\n").encode("ascii")
            artifact_payloads[kind] = payload
            artifact_path = worker_directory / name
            artifact_path.write_bytes(payload)
            sha256, byte_length = _stream_file_sha256_and_length(artifact_path)
            artifacts[kind] = {
                "file": name,
                "byteLength": byte_length,
                "sha256": sha256,
            }
        summary = {
            key: index for index, key in enumerate(AGGREGATE_CLI_SUMMARY_KEYS)
        }
        plan_core = {
            "schemaVersion": CAPTURE_SNAPSHOT_SCHEMA_VERSION,
            "generatorSources": [],
            "captures": [],
        }
        empty_plan = {
            **plan_core,
            "planIdentity": sha256_canonical(plan_core),
        }
        input_snapshot_document = _build_input_snapshot_document(
            empty_plan,
            [],
            {},
        )
        input_snapshot_path = worker_directory / AGGREGATE_INPUT_SNAPSHOT_NAME
        input_snapshot_path.write_bytes(
            _input_snapshot_bytes(input_snapshot_document)
        )
        input_snapshot_sha256, input_snapshot_length = (
            _stream_file_sha256_and_length(input_snapshot_path)
        )
        input_snapshot_descriptor = {
            "file": AGGREGATE_INPUT_SNAPSHOT_NAME,
            "byteLength": input_snapshot_length,
            "sha256": input_snapshot_sha256,
            "snapshotIdentity": input_snapshot_document["snapshotIdentity"],
        }
        (worker_directory / AGGREGATE_WORKER_SUMMARY_NAME).write_text(
            json.dumps(
                {
                    "schemaVersion": 2,
                    "summary": summary,
                    "artifacts": artifacts,
                    "inputSnapshot": input_snapshot_descriptor,
                },
                ensure_ascii=True,
                separators=(",", ":"),
                sort_keys=True,
            )
            + "\n",
            encoding="utf-8",
        )
        try:
            _load_aggregate_worker_result(
                worker_directory,
                expected_plan_identity="f" * 64,
            )
        except RuntimeError as error:
            assert "plan identity does not match its parent plan" in str(error)
        else:
            raise AssertionError("aggregate result accepted the wrong parent plan")
        aggregate_result = _load_aggregate_worker_result(
            worker_directory,
            expected_plan_identity=empty_plan["planIdentity"],
        )
        assert aggregate_result.summary == summary
        exported_snapshot = aggregate_test_root / "exported-input-snapshot.json"
        _export_input_snapshot_manifest(aggregate_result, exported_snapshot)
        assert exported_snapshot.read_bytes() == input_snapshot_path.read_bytes()

        destination_root = aggregate_test_root / "destinations"
        destinations = _artifact_destinations(
            destination_root / "inventory.json",
            destination_root / "catalog.g.cs",
            destination_root / "fixtures.g.cs",
        )
        try:
            _artifact_destinations(
                destination_root / "collision.json",
                destination_root / "catalog-collision.g.cs",
                destination_root / "fixtures-collision.g.cs",
                destination_root / "collision.json",
            )
        except RuntimeError as error:
            assert "input snapshot manifest must be distinct" in str(error)
        else:
            raise AssertionError("hidden snapshot output collision was accepted")
        _write_aggregate_artifacts_atomically(aggregate_result, destinations)
        assert all(
            destinations[kind].read_bytes() == artifact_payloads[kind]
            for kind in AGGREGATE_WORKER_ARTIFACT_NAMES
        )
        assert all(
            _aggregate_artifact_matches(aggregate_result, kind, destinations[kind])
            for kind in AGGREGATE_WORKER_ARTIFACT_NAMES
        )
        destinations["inventory"].write_bytes(b"stale")
        assert not _aggregate_artifact_matches(
            aggregate_result, "inventory", destinations["inventory"]
        )

        worker_inventory = (
            worker_directory / AGGREGATE_WORKER_ARTIFACT_NAMES["inventory"]
        )
        worker_inventory.write_bytes(artifact_payloads["inventory"] + b"corrupt")
        try:
            _load_aggregate_worker_result(worker_directory)
        except RuntimeError as error:
            assert "artifact is malformed" in str(error)
        else:
            raise AssertionError("malformed aggregate worker artifact was accepted")
        worker_inventory.write_bytes(artifact_payloads["inventory"])

        try:
            _validate_aggregate_worker_directory(REPO_ROOT)
        except RuntimeError:
            pass
        else:
            raise AssertionError("aggregate worker accepted a repository target")
    assert sha256_hex_to_base64("0" * 64) == ("A" * 43) + "="
    codec_sample = "0123456789abcdef" * 4
    assert sha256_base64_to_hex(sha256_hex_to_base64(codec_sample)) == codec_sample
    assert sha256_base64_to_hex(("A" * 43) + "A") is None
    assert captured_weapon_cycle_seconds({294: 125, 210: 175}) == 3.0
    assert captured_weapon_cycle_seconds({294: 0, 210: 175}) is None
    assert captured_weapon_cycle_seconds({294: 125, 210: 0}) is None
    assert captured_weapon_cycle_seconds({294: 0xFFFFFFFF, 210: 175}) is None
    assert damage_observations_are_runtime_ready([15, 17])
    assert not damage_observations_are_runtime_ready([])
    assert semantic_fallback_is_capture_proven(
        [{"captureEvidenceSafe": True, "runtimeContractReady": False}], 1
    )
    assert not semantic_fallback_is_capture_proven(
        [{"captureEvidenceSafe": True, "runtimeContractReady": False}], 2
    )
    assert not semantic_fallback_is_capture_proven([], 1)
    parallel_modes = {
        "natural-special": [
            {"sourceIdentity": "0x00000001", "streamSignatureId": "slot-1"},
            {"sourceIdentity": "0x00000001", "streamSignatureId": "slot-2"},
        ],
        "equipped": [
            {"sourceIdentity": "0x00000001", "streamSignatureId": "slot-6"}
        ],
    }
    assert find_conflicted_normal_sources(parallel_modes) == []
    contradictory_stream = json.loads(json.dumps(parallel_modes))
    contradictory_stream["equipped"][0]["streamSignatureId"] = "slot-1"
    assert find_conflicted_normal_sources(contradictory_stream) == [
        "0x00000001"
    ]
    unowned_generation_chain = {
        "sourceIdentity": "0x00000002",
        "metadataResolution": "capture-local-generation",
        "metadata": {"ownerIdentity": ""},
    }
    later_owned_generation_chain = {
        "sourceIdentity": "0x00000002",
        "metadataResolution": "capture-local-generation",
        "metadata": {"ownerIdentity": "(SimpleChar:00000003)"},
    }
    assert not correlated_generation_is_owned(unowned_generation_chain)
    assert correlated_generation_is_owned(later_owned_generation_chain)
    assert capture_certifiable_source_identities(
        [unowned_generation_chain], [], []
    ) == ["0x00000002"]
    assert capture_certifiable_source_identities(
        [later_owned_generation_chain], [], []
    ) == []
    assert capture_certifiable_source_identities(
        [unowned_generation_chain, later_owned_generation_chain], [], []
    ) == ["0x00000002"]
    unexplained_complete_profile = {
        "profileKey": "resource=1|md=2|level=3|name=Self Test",
        "metadata": {
            "name": "Self Test",
            "monsterData": 2,
            "level": 3,
            "capturedRealmId": 1,
        },
        "normalCompleteChainCount": 1,
        "captureCertifiedVariantCount": 0,
        "captureSessionsSearched": ["self-test"],
        "ownedOrPetSourceIdentitiesExcluded": [],
        "disabledCapability": "NPC auto-attack emission and damage application",
        "variants": [
            {
                "rawWireVariantObservations": [
                    {"sourceIdentity": "0x00000001"}
                ],
                "excludedConflictedSourceIdentities": [],
                "excludedCorrelationConflictSourceIdentities": [],
                "excludedInferredMetadataSourceIdentities": [],
            }
        ],
    }
    exclusions, blockers = audit_uncertified_complete_chains(
        [unexplained_complete_profile]
    )
    assert len(exclusions) == 1 and len(blockers) == 1
    owned_complete_profile = json.loads(json.dumps(unexplained_complete_profile))
    owned_complete_profile["ownedOrPetSourceIdentitiesExcluded"] = [
        "0x00000001"
    ]
    exclusions, blockers = audit_uncertified_complete_chains(
        [owned_complete_profile]
    )
    assert len(exclusions) == 1 and not blockers
    exact_multistream_variant = {
        "captureCertified": True,
        "baseSignature": {"weaponContextKind": "natural"},
        "rawWireVariantObservations": [
            {
                "weaponItemFullUpdatePacketId": None,
                "specialAttackWeaponPacketId": "saw",
                "attackPacketId": "attack",
                "attackInfoPacketId": "attack-info-1",
            },
            {
                "weaponItemFullUpdatePacketId": None,
                "specialAttackWeaponPacketId": "saw",
                "attackPacketId": "attack",
                "attackInfoPacketId": "attack-info-2",
            },
        ],
        "streams": [
            {
                "damageObservations": [3],
                "attackInfoPacketIds": ["attack-info-1"],
                "ammoTransitionValidation": {"valid": True},
                "runtimeContractReady": False,
            },
            {
                "damageObservations": [17],
                "attackInfoPacketIds": ["attack-info-2"],
                "ammoTransitionValidation": {"valid": True},
                "runtimeContractReady": False,
            },
        ],
    }
    assert capture_evidence_is_safe(exact_multistream_variant)
    invalid_ammo_variant = json.loads(json.dumps(exact_multistream_variant))
    invalid_ammo_variant["streams"][0]["ammoTransitionValidation"]["valid"] = False
    assert capture_evidence_is_safe(invalid_ammo_variant)
    incomplete_wire_variant = json.loads(json.dumps(exact_multistream_variant))
    incomplete_wire_variant["rawWireVariantObservations"][0]["attackPacketId"] = None
    assert not capture_evidence_is_safe(incomplete_wire_variant)
    aggregated_context = aggregate_observations(
        [
            {
                "packetId": "attack-info",
                "sourceIdentity": "0x00000001",
                "classification": "incomplete",
                "messageType": "AttackInfo",
                "evidenceFound": {
                    "WeaponItemFullUpdate": "wifu",
                    "SpecialAttackWeapon": "saw",
                    "Attack": "attack",
                    "AttackInfo": "attack-info",
                },
            }
        ],
        "packetId",
    )[0]
    assert aggregated_context["packetIds"] == ["attack-info"]
    assert aggregated_context["contextPacketIds"] == [
        "attack",
        "attack-info",
        "saw",
        "wifu",
    ]

    thief_wifu = bytes.fromhex(
        "3B1D22680000C74A2573BACB000000000B0000C350795B5DB200153008"
        "000F424F0000000001060000276A0000000004000401000000170001DADF"
        "000002BD00000001000002BE0001DADF000002BF0001DADF0000019C00000001"
        "0000001AFFFFFFFF00000126000000EB000000D2000000EB00000000"
    )
    weapon = decode_weapon_item_full_update(thief_wifu)
    assert weapon["definitionComplete"]
    assert weapon["owner"]["instanceHex"] == "795B5DB2"
    assert weapon["inventorySlot"] == 6
    assert weapon["energy"] == -1
    assert weapon["fieldProvenance"]["stats[6].value"]["rawHex"] == "FFFFFFFF"
    mutable_weapon = json.loads(json.dumps(weapon))
    for stat in mutable_weapon["stats"]:
        if stat["stat"] == 412:
            stat["rawValue"] += 1
            stat["value"] += 1
        elif stat["stat"] == 26:
            stat["rawValue"] = 7
            stat["value"] = 7
    assert wifu_signature_from_decoded(weapon) != wifu_signature_from_decoded(
        mutable_weapon
    )
    assert wifu_signature_from_decoded(
        weapon, MUTABLE_WIFU_STAT_IDS
    ) == wifu_signature_from_decoded(mutable_weapon, MUTABLE_WIFU_STAT_IDS)
    assert [
        row["stat"]
        for row in wifu_signature_from_decoded(
            weapon, MUTABLE_WIFU_STAT_IDS
        )["stats"]
    ] == [stat for stat in FULL_WEAPON_STAT_ORDER if stat not in MUTABLE_WIFU_STAT_IDS]
    stable_weapon_change = json.loads(json.dumps(weapon))
    next(
        stat for stat in stable_weapon_change["stats"] if stat["stat"] == 294
    )["rawValue"] += 1
    assert wifu_signature_from_decoded(
        weapon, MUTABLE_WIFU_STAT_IDS
    ) != wifu_signature_from_decoded(
        stable_weapon_change, MUTABLE_WIFU_STAT_IDS
    )

    def stub_record(
        packet_id: str,
        sequence: int,
        decoded: dict[str, Any],
        message_type: str = "AttackInfo",
    ) -> PacketRecord:
        return PacketRecord(
            packet_id=packet_id,
            capture="self-test",
            capture_id="self-test",
            captured_utc=f"2026-07-22T00:00:{sequence:02d}Z",
            direction="IN",
            sequence=sequence,
            global_ordinal=sequence,
            message_type=message_type,
            packet_hex="",
            body_hex="",
            packet_sha256="",
            body_sha256="",
            canonical_source="self-test",
            decoded=decoded,
        )

    def audit_stub_record(sequence: int, message_type: str) -> PacketRecord:
        packet = bytes(15) + bytes([sequence]) + struct.pack(">I", sequence)
        packet_sha256 = hashlib.sha256(packet).hexdigest()
        packet_id = f"self-test|IN|{sequence}|{packet_sha256[:12]}"
        identity = {"instance": sequence}
        decoded = (
            {"owner": identity}
            if message_type == "WeaponItemFullUpdate"
            else {"source": identity, "target": {"instance": sequence + 100}}
        )
        return PacketRecord(
            packet_id=packet_id,
            capture="self-test",
            capture_id="self-test",
            captured_utc=f"2026-07-22T00:01:{sequence:02d}Z",
            direction="IN",
            sequence=sequence,
            global_ordinal=sequence,
            message_type=message_type,
            packet_hex=packet.hex().upper(),
            body_hex=packet[16:].hex().upper(),
            packet_sha256=packet_sha256,
            body_sha256=hashlib.sha256(packet[16:]).hexdigest(),
            canonical_source="packets.hex.log",
            decoded=decoded,
            metadata_resolution="self-test-no-metadata",
        )

    compact_records_by_label = {
        "saw": audit_stub_record(1, "SpecialAttackWeapon"),
        "attack": audit_stub_record(2, "Attack"),
        "attack-info-1": audit_stub_record(3, "AttackInfo"),
        "attack-info-2": audit_stub_record(4, "AttackInfo"),
        "wifu": audit_stub_record(5, "WeaponItemFullUpdate"),
        "orphan-attack-info": audit_stub_record(6, "AttackInfo"),
        "stop": audit_stub_record(7, "StopFight"),
    }
    compact_ids = {
        label: record.packet_id
        for label, record in compact_records_by_label.items()
    }
    compact_variant = json.loads(json.dumps(exact_multistream_variant))
    for observation, attack_info_label in zip(
        compact_variant["rawWireVariantObservations"],
        ("attack-info-1", "attack-info-2"),
    ):
        observation["specialAttackWeaponPacketId"] = compact_ids["saw"]
        observation["attackPacketId"] = compact_ids["attack"]
        observation["attackInfoPacketId"] = compact_ids[attack_info_label]
    for stream, attack_info_label in zip(
        compact_variant["streams"],
        ("attack-info-1", "attack-info-2"),
    ):
        stream["attackInfoPacketIds"] = [compact_ids[attack_info_label]]
    compact_variant.update(
        {
            "semanticProfileId": "self-test-profile",
            "representativeWifuPacketId": None,
            "representativeSawPacketId": compact_ids["saw"],
            "representativeAttackPacketId": compact_ids["attack"],
            "mutableWifuStateObservations": [],
        }
    )
    compact_aggregate = aggregate_observations(
        [
            {
                "packetId": compact_ids["orphan-attack-info"],
                "sourceIdentity": "0x00000006",
                "classification": "incomplete",
                "messageType": "AttackInfo",
                "evidenceFound": {
                    "WeaponItemFullUpdate": compact_ids["wifu"],
                    "SpecialAttackWeapon": compact_ids["saw"],
                    "Attack": compact_ids["attack"],
                    "AttackInfo": compact_ids["orphan-attack-info"],
                },
            }
        ],
        "packetId",
    )[0]
    compact_profile = {
        "profileKey": "self-test-key",
        "variants": [compact_variant],
        "nonNormalObservations": [],
        "incompleteObservations": [compact_aggregate],
        "unsupportedSequences": [],
    }
    compact_packet_by_id = {
        record.packet_id: record for record in compact_records_by_label.values()
    }
    compact_sessions = [{"capture": "self-test"}]
    compacted = compact_packet_evidence(
        [compact_profile],
        [compact_records_by_label["stop"]],
        [
            {
                "packetOrder": [
                    compact_ids["saw"],
                    compact_ids["attack"],
                    compact_ids["attack-info-1"],
                    compact_ids["attack-info-2"],
                ]
            }
        ],
        compact_packet_by_id,
        compact_sessions,
        [],
    )
    assert compacted["packetAuditLedgerColumns"] == list(
        PACKET_AUDIT_LEDGER_COLUMNS
    )
    assert {row["packetId"] for row in compacted["packets"]} == {
        compact_ids["saw"],
        compact_ids["attack"],
        compact_ids["attack-info-1"],
        compact_ids["attack-info-2"],
    }
    assert len(compacted["packetAuditLedger"]) == 3
    assert compacted["packetAuditLedgerSha256"] == positional_ledger_sha256(
        compacted["packetAuditLedger"]
    )
    positional_hash_fixture = [
        [0, None, -2147483648, "AQID+/=", [0, 2]],
        [1, "quote\"slash\\control\n", "\u2603\U0001f642", []],
    ]
    assert positional_ledger_sha256(positional_hash_fixture) == sha256_canonical(
        positional_hash_fixture
    )
    try:
        positional_ledger_sha256([[True]])
    except TypeError:
        pass
    else:
        raise AssertionError("positional ledger accepted an unsupported JSON type")
    compact_observation = compact_profile["incompleteObservations"][0]
    assert "packetIds" not in compact_observation
    assert "contextPacketIds" not in compact_observation
    assert compact_observation["packetAudit"]["packetReferenceCount"] == 1
    assert compact_observation["contextPacketAudit"]["packetReferenceCount"] == 4
    assert compacted["lifecycleBoundarySummary"]["observationCount"] == 1
    compact_validation_inventory = {
        "schemaVersion": 3,
        "sessions": compact_sessions,
        "metadataGenerations": [],
        "profiles": [compact_profile],
        "summary": {
            "npcLifecycleBoundaryObservations": 1,
            "fullPacketProvenanceRecords": len(compacted["packets"]),
            "compactPacketAuditLedgerRecords": len(
                compacted["packetAuditLedger"]
            ),
            "packetAuditGroups": len(compacted["packetAuditGroups"]),
        },
        **compacted,
    }
    validate_compact_packet_evidence(compact_validation_inventory)
    assert packet_reference_sha256(["b", "a", "a"]) == packet_reference_sha256(
        ["a", "b"]
    )
    compact_render_fixture = {
        "schemaVersion": 3,
        "packetAuditLedger": [["packet-a", 1], ["packet-b", 2]],
    }
    compact_rendered = canonical_json(compact_render_fixture)
    assert json.loads(compact_rendered) == compact_render_fixture
    assert '["packet-a",1]' in compact_rendered

    mutable_ammo_packets = {
        "wifu-a": stub_record("wifu-a", 1, {"energy": 5}),
        "ai-a-1": stub_record("ai-a-1", 2, {}),
        "ai-a-2": stub_record("ai-a-2", 3, {}),
        "wifu-b": stub_record("wifu-b", 4, {"energy": 8}),
        "ai-b-1": stub_record("ai-b-1", 5, {}),
    }
    representative_ammo, initial_ammo_candidates, ammo_problems = (
        validate_equipped_ammo_sequence(
            [
                {
                    "capture": "self-test",
                    "sourceIdentity": "0x00000001",
                    "weaponItemFullUpdatePacketId": "wifu-a",
                    "attackInfoPacketId": "ai-a-1",
                    "ammo": 4,
                },
                {
                    "capture": "self-test",
                    "sourceIdentity": "0x00000001",
                    "weaponItemFullUpdatePacketId": "wifu-a",
                    "attackInfoPacketId": "ai-a-2",
                    "ammo": 3,
                },
                {
                    "capture": "self-test",
                    "sourceIdentity": "0x00000001",
                    "weaponItemFullUpdatePacketId": "wifu-b",
                    "attackInfoPacketId": "ai-b-1",
                    "ammo": 7,
                },
            ],
            mutable_ammo_packets,
            "wifu-a",
        )
    )
    assert representative_ammo == 4
    assert initial_ammo_candidates == [4, 7]
    assert not ammo_problems

    cultist_wifu = bytes.fromhex(
        "3B1D22680000C74A257E9F52000000000B0000C35079834DCE000E5010"
        "000F424F0000000001060000276A00000000040004210000001700031FCB"
        "000002BD00000001000002BE00031FCB000002BF00031FCB0000019C00000001"
        "0000001AFFFFFFFF00000126000000EB000000D2000000EB00000000"
    )
    cultist_weapon = decode_weapon_item_full_update(cultist_wifu)
    assert cultist_weapon["definitionComplete"], cultist_weapon["definitionProblems"]

    flea_saw = bytes.fromhex(
        "1D3C0F1C0000C350795317610000000BD300031163000311644550414845504148"
        "0003116000031161415A5553415A55530000002100000021000000210000002100000000"
    )
    saw = decode_special_attack_weapon(flea_saw)
    assert [row["tagHex"] for row in saw["specials"]] == ["45504148", "415A5553"]
    mutable_saw = json.loads(json.dumps(saw))
    mutable_saw["unknown5"] = 95
    assert saw_signature_from_decoded(saw) != saw_signature_from_decoded(
        mutable_saw
    )
    assert saw_signature_from_decoded(
        saw, MUTABLE_SAW_FIELD_NAMES
    ) == saw_signature_from_decoded(mutable_saw, MUTABLE_SAW_FIELD_NAMES)
    stable_saw_change = json.loads(json.dumps(saw))
    stable_saw_change["unknown4"] += 1
    assert saw_signature_from_decoded(
        saw, MUTABLE_SAW_FIELD_NAMES
    ) != saw_signature_from_decoded(stable_saw_change, MUTABLE_SAW_FIELD_NAMES)
    mutable_saw_replay_variant = {
        "captureEvidenceSafe": True,
        "deterministicRuntimeInitializationProven": False,
        "baseSignature": {"weaponContextKind": "equipped"},
        "mutableSawStateObservations": [
            {"packetId": "saw-initial", "unknown5": 0},
            {"packetId": "saw-next", "unknown5": 85},
        ],
        "runtimeMissingEvidence": [
            "capture source=0x00000001 WIFU packet AttackInfo index=0 ammo=0 expected=6",
            "deterministic runtime SpecialAttackWeapon Unknown5 state selection across captured values [0, 85]",
            "deterministic runtime WIFU Energy/MultipleCount state selection across captured values",
        ],
    }
    assert should_emit_mutable_saw_replay(mutable_saw_replay_variant)
    mutable_saw_replay_variant["mutableSawStateObservations"].append(
        {"packetId": "saw-third", "unknown5": 90}
    )
    assert not should_emit_mutable_saw_replay(mutable_saw_replay_variant)
    mutable_saw_replay_variant["mutableSawStateObservations"].pop()
    mutable_saw_replay_variant["runtimeMissingEvidence"].append(
        "unsupported semantic stream difference"
    )
    assert not should_emit_mutable_saw_replay(mutable_saw_replay_variant)
    mutable_saw_replay_variant["runtimeMissingEvidence"].pop()
    mutable_saw_replay_variant["captureEvidenceSafe"] = False
    assert not should_emit_mutable_saw_replay(mutable_saw_replay_variant)

    cultist_ai = bytes.fromhex(
        "46002F160000C3507984B379000000000F0000000E000000060000C35070CBBEF3"
        "000000000000000300000000"
    )
    attack_info = decode_attack_info(cultist_ai)
    assert attack_info["amount"] == 15
    assert attack_info["ammo"] == 14
    assert attack_info["weaponSlot"] == 6
    assert attack_info["damageTypeWire"] == 0
    assert attack_info["hitTypeWire"] == 3

    vagabond_miss_body = bytes.fromhex(
        "5C654B280000C3507944C0650100000000000000060000C350797B885C"
        "0000C3507944C06500000000"
    )
    missed_attack_info = decode_missed_attack_info(vagabond_miss_body)
    assert missed_attack_info["source"]["instanceHex"] == "7944C065"
    assert missed_attack_info["attacker"]["instanceHex"] == "797B885C"
    assert missed_attack_info["defender"]["instanceHex"] == "7944C065"
    assert missed_attack_info["n3Unknown"] == 1
    assert missed_attack_info["unknown1"] == 0
    assert missed_attack_info["unknown2"] == 6
    assert missed_attack_info["unknown5"] == 0
    miss_record = stub_record(
        "vagabond-miss",
        9,
        missed_attack_info,
        "MissedAttackInfo",
    )
    assert hex_identity(miss_record.source) == "0x797B885C"
    assert hex_identity(miss_record.target) == "0x7944C065"

    flea_target_death = decode_character_action(
        bytes.fromhex(
            "5E4777700000C350794ADBC800000000630000000000000000"
            "0000000000000000000001F40000"
        )
    )
    assert flea_target_death["source"]["instanceHex"] == "794ADBC8"
    assert flea_target_death["action"] == 99
    assert flea_target_death["parameter2"] == 500
    assert flea_target_death["unsupported"]

    ground_item_prefix = bytes.fromhex(
        "3B1D22680000C74A256F9561000000000B0000000000000000"
    )
    ignored = decode_weapon_item_full_update(ground_item_prefix)
    assert not ignored["npcOwnerLinked"]

    fixture_capture = CAPTURE_ROOT / "20260721-032547"
    if fixture_capture.exists():
        fixture_result = parse_capture(fixture_capture)
        fixture_records, _, fixture_session, fixture_errors = fixture_result
        assert fixture_session["canonicalValid"]
        assert not fixture_errors
        fixture_payload = json.loads(json.dumps(_parse_capture_payload(fixture_result)))
        assert _parse_capture_payload(_parse_capture_result(fixture_payload)) == fixture_payload
        captured_cultist_weapon = next(
            row
            for row in fixture_records
            if row.message_type == "WeaponItemFullUpdate"
            and row.decoded["owner"]["instanceHex"] == "79834DCE"
        )
        assert captured_cultist_weapon.decoded["definitionComplete"], (
            captured_cultist_weapon.decoded["stats"],
            captured_cultist_weapon.decoded["definitionProblems"],
        )

    aztur_capture = CAPTURE_ROOT / "20260722-045835"
    if aztur_capture.exists():
        aztur_records, aztur_metadata, aztur_session, aztur_errors = parse_capture(
            aztur_capture
        )
        assert aztur_session["canonicalValid"]
        assert not aztur_errors
        aztur_local_metadata: dict[
            tuple[str, int], list[MetadataGeneration]
        ] = defaultdict(list)
        aztur_corpus_metadata: dict[int, list[MetadataGeneration]] = defaultdict(list)
        for generation in aztur_metadata:
            aztur_local_metadata[(generation.capture, generation.source)].append(
                generation
            )
            aztur_corpus_metadata[generation.source].append(generation)
        for generations in aztur_local_metadata.values():
            generations.sort(key=lambda row: row.sequence)
        for record in aztur_records:
            record.metadata, record.metadata_resolution = choose_metadata(
                record, aztur_local_metadata, aztur_corpus_metadata
            )
        aztur_complete, aztur_incomplete, aztur_unsupported = correlate(
            aztur_records
        )
        aztur_packet_by_id = {row.packet_id: row for row in aztur_records}
        aztur_complete, _ = deduplicate_chains(
            aztur_complete, aztur_packet_by_id
        )
        aztur_profiles = build_profiles(
            aztur_complete,
            aztur_incomplete,
            aztur_unsupported,
            aztur_packet_by_id,
            aztur_metadata,
        )
        aztur_profile = next(
            row
            for row in aztur_profiles
            if row["profileKey"]
            == "resource=1931|md=159966|level=74|name=Aztur the Immortal"
        )
        assert aztur_profile["normalCompleteChainCount"] == 107
        assert aztur_profile["invariantNormalContractCount"] == 2
        assert aztur_profile["captureCertifiedVariantCount"] == 2
        assert not aztur_profile["actualNormalSignatureConflict"]
        assert aztur_profile["conflictedSourceIdentities"] == []
        assert not aztur_profile["semanticFallbackCaptureProven"]
        assert all(
            row["sourceIdentities"] == ["0x7988C153"]
            and row["captureEvidenceSafe"]
            and len(
                {
                    observation["baseSignatureId"]
                    for observation in row["rawWireVariantObservations"]
                }
            )
            == 2
            and {
                observation["unknown5"]
                for observation in row["mutableSawStateObservations"]
            }
            == {0, 62}
            for row in aztur_profile["variants"]
        )
        aztur_by_kind = {
            row["baseSignature"]["weaponContextKind"]: row
            for row in aztur_profile["variants"]
        }
        assert set(aztur_by_kind) == {"natural-or-special", "equipped"}
        natural_streams = aztur_by_kind["natural-or-special"]["streams"]
        equipped_streams = aztur_by_kind["equipped"]["streams"]
        assert {
            (row["signature"]["weaponSlot"], row["signature"]["weaponInstance"])
            for row in natural_streams
        } == {
            (1, 1263026755),
            (2, 1497912661),
            (3, 1179993922),
        }
        assert {
            (row["signature"]["weaponSlot"], row["signature"]["weaponInstance"])
            for row in equipped_streams
        } == {(6, 0)}
        natural_signature = aztur_by_kind["natural-or-special"][
            "invariantContractSignature"
        ]
        equipped_signature = aztur_by_kind["equipped"][
            "invariantContractSignature"
        ]
        assert natural_signature["specialAttackWeapon"] == equipped_signature[
            "specialAttackWeapon"
        ]
        assert natural_signature["attack"] == equipped_signature["attack"]
        assert "weaponItemFullUpdate" not in natural_signature
        assert equipped_signature["weaponItemFullUpdate"] == {
            "n3Unknown": 0,
            "unknown1": 11,
            "inventorySlot": 6,
            "stateMachineType": 1000015,
            "stateMachineInstance": 0,
            "unknown2": 262,
            "stats": [
                {"stat": 0, "value": 67109921},
                {"stat": 23, "value": 160031},
                {"stat": 701, "value": 1},
                {"stat": 702, "value": 160031},
                {"stat": 703, "value": 160031},
                {"stat": 294, "value": 235},
                {"stat": 210, "value": 235},
            ],
            "unknown3": 0,
        }

    sandstorm_capture = CAPTURE_ROOT / "20260614-215831"
    if sandstorm_capture.exists():
        sandstorm_records, sandstorm_metadata, sandstorm_session, sandstorm_errors = (
            parse_capture(sandstorm_capture)
        )
        assert sandstorm_session["canonicalValid"]
        assert not sandstorm_errors
        sandstorm_local_metadata: dict[
            tuple[str, int], list[MetadataGeneration]
        ] = defaultdict(list)
        sandstorm_corpus_metadata: dict[int, list[MetadataGeneration]] = defaultdict(
            list
        )
        for generation in sandstorm_metadata:
            sandstorm_local_metadata[
                (generation.capture, generation.source)
            ].append(generation)
            sandstorm_corpus_metadata[generation.source].append(generation)
        for generations in sandstorm_local_metadata.values():
            generations.sort(key=lambda row: row.sequence)
        for record in sandstorm_records:
            record.metadata, record.metadata_resolution = choose_metadata(
                record, sandstorm_local_metadata, sandstorm_corpus_metadata
            )
        sandstorm_complete, sandstorm_incomplete, sandstorm_unsupported = correlate(
            sandstorm_records
        )
        sandstorm_packet_by_id = {row.packet_id: row for row in sandstorm_records}
        sandstorm_complete, _ = deduplicate_chains(
            sandstorm_complete, sandstorm_packet_by_id
        )
        sandstorm_chains = [
            row
            for row in sandstorm_complete
            if row["classification"] == "normal-landed"
            and row["sourceIdentity"] == "0x78D30B0B"
            and row["metadata"]["name"] == "SANDSTORM Marauder"
            and row["metadata"]["monsterData"] == 287217
            and row["metadata"]["level"] == 7
        ]
        assert {
            sandstorm_packet_by_id[row["attackInfoPacketId"]].sequence
            for row in sandstorm_chains
        } == {11402, 11420, 11492}
        assert all(
            row["metadataResolution"] == "capture-local-generation"
            and not row["metadata"]["ownerIdentity"]
            for row in sandstorm_chains
        )
        assert any(
            generation.source == signed32(0x78D30B0B)
            and generation.sequence in {11888, 11889}
            and generation.owner_identity == "(SimpleChar:3F81C)"
            for generation in sandstorm_metadata
        )
        sandstorm_profiles = build_profiles(
            sandstorm_complete,
            sandstorm_incomplete,
            sandstorm_unsupported,
            sandstorm_packet_by_id,
            sandstorm_metadata,
        )
        sandstorm_profile = next(
            row
            for row in sandstorm_profiles
            if row["profileKey"]
            == "resource=6553|md=287217|level=7|name=SANDSTORM Marauder"
        )
        assert sandstorm_profile["normalCompleteChainCount"] == 3
        assert sandstorm_profile["captureCertifiedVariantCount"] == 1
        assert sandstorm_profile["ownedOrPetSourceIdentitiesExcluded"] == []
        assert sandstorm_profile["conflictedSourceIdentities"] == []
        assert sandstorm_profile["variants"][0]["sourceIdentities"] == [
            "0x78D30B0B"
        ]
        assert sandstorm_profile["variants"][0]["captureEvidenceSafe"]

    legacy_arete_capture = CAPTURE_ROOT / "20260623-111355"
    if legacy_arete_capture.exists():
        legacy_projection = legacy_arete_capture / "scfu-appearance.csv"
        projection_existed = legacy_projection.exists()
        raw_records, source_summary = load_packet_records(legacy_arete_capture)
        assert source_summary["canonicalValid"]
        canonical_by_sequence = {
            (row["direction"], row["sequence"]): row for row in raw_records
        }
        metadata, _ = load_metadata_generations(
            legacy_arete_capture,
            legacy_arete_capture.relative_to(CAPTURE_REPO_ROOT).as_posix(),
            canonical_by_sequence,
        )
        assert any(
            row.source == signed32(0x78FCAE56)
            and row.name == "Malfunctioning Cleaning Robot"
            and row.monster_data == 297023
            and row.level == 1
            for row in metadata
        )
        assert any(
            row.source == signed32(0x78E0FC62)
            and row.name == "Marcus Stone"
            and row.monster_data == 258744
            and row.level == 15
            for row in metadata
        )
        legacy_records, legacy_metadata, legacy_session, _ = parse_capture(
            legacy_arete_capture
        )
        assert legacy_session["canonicalValid"]
        local_metadata: dict[
            tuple[str, int], list[MetadataGeneration]
        ] = defaultdict(list)
        corpus_metadata: dict[int, list[MetadataGeneration]] = defaultdict(list)
        for generation in legacy_metadata:
            local_metadata[(generation.capture, generation.source)].append(generation)
            corpus_metadata[generation.source].append(generation)
        for values in local_metadata.values():
            values.sort(key=lambda row: row.sequence)
        for record in legacy_records:
            record.metadata, record.metadata_resolution = choose_metadata(
                record, local_metadata, corpus_metadata
            )
        complete, incomplete, unsupported = correlate(legacy_records)
        assert any(
            row["metadata"]["name"] == "Malfunctioning Cleaning Robot"
            for row in complete
        )
        marcus_incomplete = [
            row
            for row in incomplete
            if (row.get("metadata") or {}).get("name") == "Marcus Stone"
            and row.get("messageType") == "AttackInfo"
        ]
        assert marcus_incomplete
        assert all(
            row["metadataResolution"] == "capture-local-previsibility-stitch"
            and "same-capture preceding SCFU generation metadata"
            in row["missingEvidence"]
            for row in marcus_incomplete
        )
        assert not any(
            row["metadata"]["name"] == "Marcus Stone" for row in complete
        )
        character_action_ids = {
            row.packet_id
            for row in legacy_records
            if row.message_type == "CharacterAction"
        }
        assert character_action_ids
        assert character_action_ids.issubset(
            {row["packetId"] for row in unsupported}
        )
        orphan_prefix_ids = {
            row["packetId"]
            for row in incomplete
            if row.get("classification") == "orphan-combat-prefix"
        }
        assert orphan_prefix_ids
        assert all(
            row["messageType"]
            in {"WeaponItemFullUpdate", "SpecialAttackWeapon", "Attack"}
            for row in incomplete
            if row.get("classification") == "orphan-combat-prefix"
        )
        assert legacy_projection.exists() == projection_existed
    print("capture-backed NPC combat extractor self-test PASS")


def main() -> int:
    parser = argparse.ArgumentParser()
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--write", action="store_true", help="write deterministic generated output")
    mode.add_argument("--check", action="store_true", help="fail if generated output is stale")
    mode.add_argument("--self-test", action="store_true")
    mode.add_argument(
        "--render-accepted-inventory",
        type=Path,
        help="render catalog and fixtures from promoted canonical inventory",
    )
    mode.add_argument(
        "--_parse-capture-worker", type=Path, help=argparse.SUPPRESS
    )
    mode.add_argument(
        "--_aggregate-worker-directory", type=Path, help=argparse.SUPPRESS
    )
    mode.add_argument(
        "--_validate-exported-input-snapshot", type=Path, help=argparse.SUPPRESS
    )
    parser.add_argument(
        "--_parse-capture-shard", type=Path, help=argparse.SUPPRESS
    )
    parser.add_argument(
        "--_parse-capture-key", help=argparse.SUPPRESS
    )
    parser.add_argument(
        "--_input-snapshot-plan", type=Path, help=argparse.SUPPRESS
    )
    parser.add_argument(
        "--_input-snapshot-manifest", type=Path, help=argparse.SUPPRESS
    )
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--catalog-output", type=Path, default=CATALOG_OUTPUT)
    parser.add_argument("--fixture-output", type=Path, default=FIXTURE_OUTPUT)
    parser.add_argument("--item-template-projection", type=Path)
    parser.add_argument("--item-template-projection-sha256")
    parser.add_argument("--item-template-projection-byte-length", type=int)
    parser.add_argument("--item-database-sha256")
    parser.add_argument("--item-database-byte-length", type=int)
    args = parser.parse_args()
    range_authority_arguments = (
        args.item_template_projection,
        args.item_template_projection_sha256,
        args.item_template_projection_byte_length,
        args.item_database_sha256,
        args.item_database_byte_length,
    )
    if any(value is not None for value in range_authority_arguments) and not all(
        value is not None for value in range_authority_arguments
    ):
        parser.error(
            "Arete attack-range authority requires projection and ItemDb descriptors"
        )
    capture_worker_members = (
        args._parse_capture_worker,
        args._parse_capture_shard,
        args._parse_capture_key,
    )
    if any(value is not None for value in capture_worker_members) and not all(
        value is not None for value in capture_worker_members
    ):
        parser.error(
            "private capture worker mode requires capture, logical key, and shard arguments"
        )
    if args._validate_exported_input_snapshot is not None:
        if args._input_snapshot_manifest is not None:
            parser.error(
                "exported input snapshot validation cannot export another manifest"
            )
        snapshot = _validate_exported_input_snapshot_manifest(
            args._validate_exported_input_snapshot
        )
        print(
            "capture-backed NPC combat input snapshot PASS "
            f"identity={snapshot['snapshotIdentity']}"
        )
        return 0
    if args._parse_capture_worker is not None:
        if args._input_snapshot_plan is not None:
            parser.error("capture worker mode cannot accept an aggregate input plan")
        _write_parse_capture_worker_shard(
            args._parse_capture_worker,
            args._parse_capture_shard,
            args._parse_capture_key,
        )
        return 0
    if args._aggregate_worker_directory is not None:
        if args._input_snapshot_plan is None:
            parser.error("aggregate worker mode requires its input snapshot plan")
        if args._input_snapshot_manifest is not None:
            parser.error("aggregate worker mode cannot export a caller snapshot manifest")
        if not all(value is not None for value in range_authority_arguments):
            parser.error("aggregate worker mode requires Arete attack-range authority")
        _write_aggregate_worker_outputs(
            args._aggregate_worker_directory,
            args._input_snapshot_plan,
            args.item_template_projection,
            args.item_template_projection_sha256,
            args.item_template_projection_byte_length,
            args.item_database_sha256,
            args.item_database_byte_length,
        )
        return 0
    if args._input_snapshot_plan is not None:
        parser.error("input snapshot plan is private to aggregate worker mode")
    if args.self_test:
        if args._input_snapshot_manifest is not None:
            parser.error("self-test mode cannot export an input snapshot manifest")
        self_test()
        return 0
    if args.render_accepted_inventory is not None:
        if args._input_snapshot_manifest is not None:
            parser.error(
                "accepted-inventory rendering cannot export a capture input snapshot"
            )
        if any(value is not None for value in range_authority_arguments):
            parser.error(
                "accepted-inventory rendering does not accept raw-promotion range authority"
            )
        governed_outputs = {
            CATALOG_OUTPUT.resolve(),
            FIXTURE_OUTPUT.resolve(),
        }
        requested_outputs = {
            args.catalog_output.resolve(),
            args.fixture_output.resolve(),
        }
        governed_alias = any(
            os.path.lexists(requested)
            and os.path.lexists(governed)
            and os.path.samefile(requested, governed)
            for requested in requested_outputs
            for governed in governed_outputs
        )
        if governed_outputs & requested_outputs or governed_alias:
            parser.error(
                "governed generated-combat outputs must be written through "
                "Tools/generated_combat_pipeline.py"
            )
        render_accepted_inventory_artifacts(
            args.render_accepted_inventory,
            args.catalog_output,
            args.fixture_output,
        )
        print(
            "accepted combat inventory rendered "
            f"inventory={args.render_accepted_inventory} "
            f"catalog={args.catalog_output} fixtures={args.fixture_output}"
        )
        return 0
    if not all(value is not None for value in range_authority_arguments):
        parser.error(
            "generated inventory requires the governed Arete attack-range authority"
        )
    if args._input_snapshot_manifest is not None:
        _validate_temporary_path(
            args._input_snapshot_manifest,
            "requested input snapshot manifest",
            must_exist=False,
        )
    destinations = _artifact_destinations(
        args.output,
        args.catalog_output,
        args.fixture_output,
        args._input_snapshot_manifest,
    )
    governed_outputs = {
        OUTPUT.resolve(),
        CATALOG_OUTPUT.resolve(),
        FIXTURE_OUTPUT.resolve(),
    }
    requested_outputs = {
        args.output.resolve(),
        args.catalog_output.resolve(),
        args.fixture_output.resolve(),
    }
    governed_alias = any(
        os.path.lexists(requested)
        and os.path.lexists(governed)
        and os.path.samefile(requested, governed)
        for requested in requested_outputs
        for governed in governed_outputs
    )
    if governed_outputs & requested_outputs or governed_alias:
        parser.error(
            "governed generated-combat outputs must be checked or written through "
            "Tools/generated_combat_pipeline.py"
        )
    destinations = {
        kind: destination.resolve() for kind, destination in destinations.items()
    }
    output = destinations["inventory"]
    catalog_output = destinations["catalog"]
    fixture_output = destinations["fixtures"]
    with tempfile.TemporaryDirectory(
        prefix="aorebirth-npc-combat-aggregate-parent-"
    ) as staging_name:
        result = _run_aggregate_worker_isolated(
            Path(staging_name),
            args.item_template_projection,
            args.item_template_projection_sha256,
            args.item_template_projection_byte_length,
            args.item_database_sha256,
            args.item_database_byte_length,
        )
        _revalidate_aggregate_input_snapshot(
            result,
            "aggregate-parent-pre-publish" if args.write else "aggregate-parent-pre-check",
            verify_hash=True,
        )
        if args.write:
            _write_aggregate_artifacts_atomically(result, destinations)
            verb = "generated"
        else:
            checks = (
                ("inventory", output, "generated inventory"),
                ("catalog", catalog_output, "generated runtime catalog"),
                ("fixtures", fixture_output, "generated exact-byte fixtures"),
            )
            for kind, destination, label in checks:
                if not destination.exists():
                    print(f"ERROR: {label} is missing: {destination}", file=sys.stderr)
                    return 1
                if not _aggregate_artifact_matches(result, kind, destination):
                    print(f"ERROR: {label} is stale: {destination}", file=sys.stderr)
                    return 1
            verb = "deterministic"
        _revalidate_aggregate_input_snapshot(
            result,
            "aggregate-parent-post-publish" if args.write else "aggregate-parent-post-check",
        )
        if args._input_snapshot_manifest is not None:
            _export_input_snapshot_manifest(
                result,
                args._input_snapshot_manifest,
            )
        summary = result.summary
        print(
            "capture-backed NPC combat inventory " + verb
            + f" sessions={summary['captureSessionsDiscovered']}"
            + f" canonical={summary['canonicalValidSessions']}"
            + f" completeChains={summary['completeAttackInfoChains']}"
            + f" certifiedProfiles={summary['captureCertifiedProfiles']}"
            + f" runtimeReadyProfiles={summary['runtimeReadyProfiles']}"
            + f" semanticDefinitions={summary['captureCertifiedSemanticDefinitions']}"
            + f" runtimeReadyDefinitions={summary['runtimeReadyGeneratedSemanticDefinitions']}"
            + f" unresolvedProfiles={summary['unresolvedProfiles']}"
            + f" errors={summary['decodeOrProjectionErrors']}"
            + f" output={output} catalog={catalog_output} fixtures={fixture_output}"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
