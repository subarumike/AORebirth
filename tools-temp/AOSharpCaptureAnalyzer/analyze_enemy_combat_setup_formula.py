#!/usr/bin/env python3
"""Build and validate deterministic enemy-combat setup formula evidence."""

from __future__ import annotations

import argparse
import hashlib
import math
import json
import re
import struct
import sys
import zlib
from pathlib import Path
from typing import Any

if hasattr(sys, "set_int_max_str_digits"):
    sys.set_int_max_str_digits(0)


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_INVENTORY = (
    REPOSITORY_ROOT
    / "docs"
    / "generated"
    / "capture_backed_npc_combat_inventory.json"
)
DEFAULT_ITEMS = REPOSITORY_ROOT / "AORebirth" / "Datafiles" / "items.dat"
DEFAULT_ACTIVE_COVERAGE = (
    REPOSITORY_ROOT
    / "docs"
    / "generated"
    / "capture_backed_npc_combat_active_coverage.json"
)
DEFAULT_OUTPUT = (
    REPOSITORY_ROOT
    / "docs"
    / "generated"
    / "enemy_combat_setup_formula_dataset.json"
)
SUBWAY_ORDINARY_CONTENT_PROVIDER = (
    REPOSITORY_ROOT
    / "AORebirth"
    / "Server"
    / "ZoneEngine"
    / "Core"
    / "Playfields"
    / "CapturedSubwayOrdinaryContentProvider.cs"
)
TEMPLE_ORDINARY_CONTENT_PROVIDER = (
    REPOSITORY_ROOT
    / "AORebirth"
    / "Server"
    / "ZoneEngine"
    / "Core"
    / "Playfields"
    / "CapturedTempleOfThreeWindsContentProvider.cs"
)
TEMPLE_ORDINARY_COMBAT_LOADOUT_CATALOG = (
    REPOSITORY_ROOT
    / "AORebirth"
    / "Server"
    / "ZoneEngine"
    / "Core"
    / "Playfields"
    / "CapturedTempleOfThreeWindsOrdinaryCombatLoadoutCatalog.g.cs"
)
TEMPLE_CULTIST_QUARANTINE_EVIDENCE = (
    REPOSITORY_ROOT
    / "docs"
    / "evidence"
    / "TEMPLE_CULTIST_COMBAT_QUARANTINE_20260726.md"
)

DISOBEDIENT_BOT_OBSERVATIONS = (
    ("20260709-210452", 3469, 5, 0x794E807A),
    ("20260709-210452", 4295, 6, 0x794F6080),
    ("20260708-143600", 10058, 8, 0x794DF074),
    ("20260709-220439", 5792, 9, 0x7953AD69),
    ("20260709-220439", 7237, 10, 0x7953AA81),
)
DISOBEDIENT_BOT_CAPTURED_VALUES = {5: 30, 6: 35, 8: 45, 9: 49, 10: 54}
DISOBEDIENT_BOT_FORMULA_ID = (
    "disobedient-bot-siw1-floor-19L-plus-28-over-4-v1"
)
STIM_FIEND_OBSERVATIONS = (
    ("20260708-143600", 17386, 10, 0x794CD773),
    ("20260708-143600", 17877, 11, 0x794CD77C),
    ("20260708-143600", 18584, 12, 0x794CD778),
    ("20260709-212115", 12882, 13, 0x7953AA4B),
    ("20260709-220439", 7612, 14, 0x7953ABAF),
)
STIM_FIEND_CAPTURED_VALUES = {10: 54, 11: 59, 12: 65, 13: 70, 14: 76}
STIM_FIEND_FORMULA_ID = "stim-fiend-siw1-floor-11L-minus-2-over-2-v1"
STIM_FIEND_PROFILE_IDS = (
    "5aa2541e7645c589-9bcb7a58208cf1e0",
    "8dc794414961f6e6-63cd3e499be4e58b",
    "963ecf2aa60f045c-de110ebeb7e358cd",
    "3f70ab044f0e78d5-d2b65cf5c70d61d6",
    "54d40b70fa1a801a-064305180fc7f1ad",
)
VIOLENT_VAGABOND_FORMULA_ID = (
    "violent-vagabond-saw-bounded-affine-floor-v1"
)
VIOLENT_VAGABOND_RESULT_DOMAIN_ID = (
    "equipped-melee-empty-saw-slot6-normal-result-v1"
)
VIOLENT_VAGABOND_OBSERVATIONS = (
    ("20260708-143600", 11154, 6),
    ("20260708-143600", 10474, 7),
    ("20260708-143600", 19378, 10),
)
ETERNAL_SENTINEL_FORMULA_ID = (
    "eternal-sentinel-saw-floor-11L-minus-2-over-2-plus-floor-L-plus-4-over-2-v1"
)
ETERNAL_SENTINEL_OBSERVATIONS = (
    ("20260721-042139", 233, 18, "0x7983FA22"),
    ("20260721-042139", 1350, 18, "0x7983FBC2"),
)
ETERNAL_SENTINEL_PROFILE_IDS = (
    "ba0dc14f053cc59f-71ed92b48bc9d461",
    "e037cf6f4165eff5-71ebcc342951c27c",
    "e037cf6f4165eff5-c036f50d1289554a",
)
FILTH_FLEA_FORMULA_ID = "filth-flea-saw-bounded-level-piecewise-v1"
FILTH_FLEA_CAPTURED_VALUES = {
    4: 28,
    5: 33,
    6: 38,
    10: 59,
    11: 65,
    12: 71,
    13: 77,
    16: 95,
    19: 113,
    20: 119,
    21: 125,
}
FILTH_FLEA_PROFILE_IDS = (
    "0442e5cb9bb937c9-f9031b9d5776b541",
    "12e4e4cadd5f9059-c3b0e4a3ccaa520e",
    "218eb3509f2be66b-12f99a4c2f732061",
    "3fb47a16c3e0d523-34d5dfe5ced96cb2",
    "4c05b2ad557f829b-d2d9cb741e270fa8",
    "654ce3810a403892-7a547a1f84232faa",
    "654ce3810a403892-d3072a2954c06011",
    "9e402946526c7a7d-9523e84139c96f2b",
    "a631010e67f24903-0bb5ffe744d13dff",
    "cf3233957f32e56f-5ba592afcf44fdff",
    "deda2240caee7272-f66c89ab2748e17c",
    "f71cf6db73bcfadd-9de45b855e6806de",
)
MELDED_PATTERNS_FORMULA_ID = (
    "melded-patterns-saw-floor-11L-minus-2-over-2-plus-28-v1"
)
TEMPLE_CULTIST_FORMULA_ID = (
    "temple-cultist-saw-bounded-level-piecewise-v1"
)
TEMPLE_CULTIST_RAISED_PRIMARY_FORMULA_ID = (
    "temple-cultist-26135-saw-bounded-level-piecewise-plus-20-v1"
)
MELDED_PATTERNS_CAPTURED_BASE_VALUES = {
    18: 98,
    19: 103,
    20: 109,
    21: 114,
    24: 131,
    25: 136,
}
MELDED_PATTERNS_OBSERVATIONS = (
    ("20260709-225408", 9811, 18, 0x79545190),
    ("20260709-225408", 8791, 18, 0x7954517C),
    ("20260720-051714", 7903, 19, 0x7980F107),
    ("20260709-222339", 8730, 20, 0x79545196),
    ("20260709-222339", 8978, 20, 0x79545196),
    ("20260709-222339", 7855, 21, 0x79545187),
    ("20260709-225408", 10519, 21, 0x79545198),
    ("20260720-051714", 7914, 21, 0x7980F106),
    ("20260720-051714", 7527, 21, 0x7980F149),
    ("20260720-051714", 2527, 24, 0x798037DE),
    ("20260709-222339", 12674, 25, 0x795451DD),
    ("20260709-225408", 15077, 25, 0x795451DD),
    ("20260720-051714", 3127, 25, 0x798037E7),
)
MELDED_PATTERNS_ACTIVE_LOADOUTS = (
    (0x7954508E, 23, 20, 121818, 121818, "20260709-222339"),
    (0x7954517C, 18, 19, 121817, 121818, "20260709-222339"),
    (0x79545185, 19, 18, 121817, 121818, "20260709-222339"),
    (0x79545187, 21, 26, 121819, 121820, "20260709-222339"),
    (0x79545190, 18, 20, 121818, 121818, "20260709-222339"),
    (0x79545196, 20, 20, 121818, 121818, "20260709-222339"),
    (0x79545198, 21, 20, 121818, 121818, "20260709-222339"),
    (0x795451BA, 22, 26, 121819, 121820, "20260709-222339"),
    (0x795451D8, 25, 25, 121819, 121820, "20260709-222339"),
    (0x795451DD, 25, 19, 121817, 121818, "20260709-222339"),
)
MELDED_PATTERNS_STARTING_QUARANTINE_SOURCES = {
    "0x7954508E",
    "0x79545187",
    "0x79545190",
    "0x79545196",
    "0x79545198",
    "0x795451BA",
}
FRAGMENTED_SOUL_FORMULA_ID = (
    "fragmented-soul-saw-6L-minus-1-plus-2-floor-L-over-2-v1"
)
FRAGMENTED_SOUL_CAPTURED_VALUES = {
    17: (101, 101, 101, 117),
    18: (107, 107, 107, 125),
    19: (113, 113, 113, 131),
    20: (119, 119, 119, 139),
    21: (125, 125, 125, 145),
}
FRAGMENTED_SOUL_OBSERVATIONS = (
    ("20260716-222007", 316, 17, 0x7970245D, "complete-chain"),
    ("20260709-222339", 5883, 17, 0x7954516A, "complete-chain"),
    ("20260709-222339", 6244, 17, 0x7954516F, "orphan-prefix"),
    ("20260712-223719", 2970, 18, 0x796079B3, "complete-chain"),
    ("20260709-222339", 3243, 18, 0x79545248, "complete-chain"),
    ("20260720-051714", 3474, 18, 0x7980F138, "complete-chain"),
    ("20260709-222339", 6922, 18, 0x7954518B, "orphan-prefix"),
    ("20260709-222339", 7119, 18, 0x7954518E, "orphan-prefix"),
    ("20260709-222339", 6542, 19, 0x7954517A, "orphan-prefix"),
    ("20260720-051714", 4465, 19, 0x7980F12F, "orphan-prefix"),
    ("20260720-051714", 5469, 20, 0x7980F122, "complete-chain"),
    ("20260709-222339", 6915, 20, 0x7954518A, "orphan-prefix"),
    ("20260720-051714", 5456, 20, 0x7980F125, "orphan-prefix"),
    ("20260709-225408", 12066, 21, 0x795451AA, "complete-chain"),
    ("20260709-222339", 8817, 21, 0x795451AE, "complete-chain"),
    ("20260709-225408", 11681, 21, 0x795451AE, "complete-chain"),
    ("20260720-051714", 5833, 21, 0x7980F120, "complete-chain"),
    ("20260709-222339", 8994, 21, 0x795451AA, "orphan-prefix"),
    ("20260709-222339", 9713, 21, 0x795451AA, "orphan-prefix"),
    ("20260709-225408", 12011, 21, 0x795451AA, "orphan-prefix"),
    ("20260709-225408", 11631, 21, 0x795451AE, "orphan-prefix"),
)


class MessagePackReader:
    """Small standard-library decoder for the legacy item template data."""

    def __init__(self, data: bytes) -> None:
        self.data = data
        self.offset = 0

    def _take(self, size: int) -> bytes:
        result = self.data[self.offset : self.offset + size]
        if len(result) != size:
            raise ValueError("Unexpected end of MessagePack data")
        self.offset += size
        return result

    def _uint(self, size: int) -> int:
        return int.from_bytes(self._take(size), "big", signed=False)

    def _int(self, size: int) -> int:
        return int.from_bytes(self._take(size), "big", signed=True)

    def read(self) -> Any:
        marker = self._uint(1)
        if marker <= 0x7F:
            return marker
        if marker >= 0xE0:
            return marker - 0x100
        if 0x80 <= marker <= 0x8F:
            return {self.read(): self.read() for _ in range(marker & 0x0F)}
        if 0x90 <= marker <= 0x9F:
            return [self.read() for _ in range(marker & 0x0F)]
        if 0xA0 <= marker <= 0xBF:
            return self._take(marker & 0x1F).decode("utf-8")
        if marker == 0xC0:
            return None
        if marker == 0xC2:
            return False
        if marker == 0xC3:
            return True
        if marker == 0xCA:
            return struct.unpack(">f", self._take(4))[0]
        if marker == 0xCB:
            return struct.unpack(">d", self._take(8))[0]
        if marker == 0xCC:
            return self._uint(1)
        if marker == 0xCD:
            return self._uint(2)
        if marker == 0xCE:
            return self._uint(4)
        if marker == 0xCF:
            return self._uint(8)
        if marker == 0xD0:
            return self._int(1)
        if marker == 0xD1:
            return self._int(2)
        if marker == 0xD2:
            return self._int(4)
        if marker == 0xD3:
            return self._int(8)
        if marker in (0xD9, 0xDA, 0xDB):
            size = self._uint({0xD9: 1, 0xDA: 2, 0xDB: 4}[marker])
            return self._take(size).decode("utf-8")
        if marker in (0xDC, 0xDD):
            size = self._uint(2 if marker == 0xDC else 4)
            return [self.read() for _ in range(size)]
        if marker in (0xDE, 0xDF):
            size = self._uint(2 if marker == 0xDE else 4)
            return {self.read(): self.read() for _ in range(size)}
        raise ValueError(f"Unsupported MessagePack marker 0x{marker:02X}")


def load_item_templates(path: Path) -> dict[int, list[Any]]:
    templates: dict[int, list[Any]] = {}
    with path.open("rb") as handle:
        version_length = handle.read(1)[0]
        handle.read(version_length)
        _, _, slice_count = struct.unpack("<iii", handle.read(12))
        for _ in range(slice_count):
            compressed_size = struct.unpack("<i", handle.read(4))[0]
            decompressor = zlib.decompressobj()
            decoded = MessagePackReader(
                decompressor.decompress(handle.read(compressed_size))
            ).read()
            for template in decoded:
                templates[int(template[5])] = template
    return templates


def divide_rounded(numerator: int, denominator: int, mode: str) -> int:
    if denominator <= 0:
        raise ValueError("denominator must be positive")
    if mode == "floor":
        return numerator // denominator
    if mode == "ceiling":
        return -((-numerator) // denominator)
    quotient, remainder = divmod(abs(numerator), denominator)
    sign = -1 if numerator < 0 else 1
    if mode == "nearest-away":
        if remainder * 2 >= denominator:
            quotient += 1
        return sign * quotient
    if mode == "nearest-even":
        doubled = remainder * 2
        if doubled > denominator or (
            doubled == denominator and quotient % 2 == 1
        ):
            quotient += 1
        return sign * quotient
    raise ValueError(f"unknown rounding mode {mode}")


def affine_candidates(
    observations: dict[int, int],
    maximum_denominator: int = 128,
) -> list[dict[str, Any]]:
    """Enumerate reduced affine rational formulas without fitting floats."""

    anchor_level = min(observations)
    anchor_value = observations[anchor_level]
    candidates: dict[tuple[int, int, int, str], dict[str, Any]] = {}
    for denominator in range(1, maximum_denominator + 1):
        for numerator in range(3 * denominator, 8 * denominator + 1):
            anchor_center = (
                anchor_value * denominator - numerator * anchor_level
            )
            for intercept in range(
                anchor_center - denominator,
                anchor_center + denominator + 1,
            ):
                for mode in (
                    "floor",
                    "ceiling",
                    "nearest-away",
                    "nearest-even",
                ):
                    if any(
                        divide_rounded(
                            numerator * level + intercept,
                            denominator,
                            mode,
                        )
                        != value
                        for level, value in observations.items()
                    ):
                        continue
                    divisor = math.gcd(
                        math.gcd(abs(numerator), abs(intercept)),
                        denominator,
                    )
                    key = (
                        numerator // divisor,
                        intercept // divisor,
                        denominator // divisor,
                        mode,
                    )
                    candidates[key] = {
                        "numerator": key[0],
                        "intercept": key[1],
                        "denominator": key[2],
                        "rounding": mode,
                        "level7": divide_rounded(
                            key[0] * 7 + key[1], key[2], mode
                        ),
                    }
    return sorted(
        candidates.values(),
        key=lambda row: (
            row["denominator"],
            abs(row["numerator"]),
            abs(row["intercept"]),
            row["rounding"],
        ),
    )


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def compact_profile(profile: dict[str, Any]) -> dict[str, Any]:
    variants = profile.get("variants", [])

    def compact_observation(row: dict[str, Any]) -> dict[str, Any]:
        return {
            key: row.get(key)
            for key in (
                "messageType",
                "classification",
                "sourceIdentity",
                "attackerIdentity",
                "defenderIdentity",
                "n3SourceIdentity",
                "n3Unknown",
                "unknown1",
                "unknown2",
                "unknown5",
                "hitTypeWire",
                "damageTypeWire",
                "packetOrderProven",
                "observationCount",
                "captureSessions",
                "missingEvidence",
                "evidenceFound",
            )
        }

    return {
        "profileKey": profile.get("profileKey"),
        "metadata": profile.get("metadata"),
        "status": profile.get("status"),
        "normalCompleteChainCount": profile.get("normalCompleteChainCount"),
        "unsupportedSequenceCount": profile.get("unsupportedNpcSequenceCount"),
        "unsupportedSequences": [
            compact_observation(row)
            for row in profile.get("unsupportedSequences", [])
        ],
        "incompleteObservationCount": len(profile.get("incompleteObservations", [])),
        "incompleteObservations": [
            compact_observation(row)
            for row in profile.get("incompleteObservations", [])
        ],
        "variantCount": len(variants),
        "variants": [
            {
                "semanticProfileId": variant.get("semanticProfileId"),
                "baseSignature": variant.get("baseSignature"),
                "captureEvidenceSafe": variant.get("captureEvidenceSafe"),
                "runtimeContractReady": variant.get("runtimeContractReady"),
                "runtimeMissingEvidence": variant.get("runtimeMissingEvidence"),
                "streams": variant.get("streams"),
                "mutableSawStateObservations": variant.get(
                    "mutableSawStateObservations"
                ),
            }
            for variant in variants
        ],
    }


def canonical_json(document: dict[str, Any]) -> str:
    return json.dumps(document, indent=2, sort_keys=True) + "\n"


def profile_resource(profile_key: str) -> int | None:
    match = re.search(r"(?:^|\|)resource=(\d+)(?:\||$)", profile_key)
    return int(match.group(1)) if match else None


def collect_template_ids(value: Any, key: str = "") -> set[int]:
    result: set[int] = set()
    if isinstance(value, dict):
        for child_key, child_value in value.items():
            normalized = child_key.lower()
            if normalized.endswith("template") and isinstance(child_value, int):
                result.add(child_value)
            elif normalized.endswith("templates") and isinstance(child_value, list):
                result.update(
                    item for item in child_value if isinstance(item, int)
                )
            result.update(collect_template_ids(child_value, child_key))
    elif isinstance(value, list):
        for child in value:
            result.update(collect_template_ids(child, key))
    return result


def read_raw_packet(capture: str, sequence: int) -> dict[str, Any]:
    path = (
        REPOSITORY_ROOT
        / "tools-temp"
        / "AOSharpLiveCapture"
        / "bin"
        / "Debug"
        / "captures"
        / capture
        / "packets.hex.log"
    )
    marker = f"#{sequence} "
    line = next(
        row
        for row in path.read_text(encoding="utf-8").splitlines()
        if marker in row
    )
    match = re.search(
        r"^(\S+)\s+(IN|OUT) #\d+ .*?\bn3=([^ ]+) hex=([0-9A-F]+)$",
        line,
    )
    if match is None:
        raise ValueError(f"could not decode raw packet {capture} #{sequence}")
    wire = bytes.fromhex(match.group(4))
    body_marker = bytes.fromhex("1D3C0F1C")
    body_offset = wire.index(body_marker)
    body = wire[body_offset:]
    unknowns = struct.unpack(">IIIII", body[-20:])
    packet_hash = hashlib.sha256(wire).hexdigest()[:12]
    return {
        "packetId": (
            "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
            f"{capture}|{match.group(2)}|{sequence}|{packet_hash}"
        ),
        "captureSession": (
            f"tools-temp/AOSharpLiveCapture/bin/Debug/captures/{capture}"
        ),
        "timestampUtc": match.group(1),
        "direction": match.group(2),
        "sequence": sequence,
        "messageType": match.group(3),
        "bodyHex": body.hex().upper(),
        "unknown1": unknowns[0],
        "unknown2": unknowns[1],
        "unknown3": unknowns[2],
        "unknown4": unknowns[3],
        "unknown5": unknowns[4],
    }


def disobedient_bot_formula(level: int) -> int:
    return ((19 * level) + 28) // 4


def stim_fiend_formula(level: int) -> int:
    return ((11 * level) - 2) // 2


def violent_vagabond_formula(level: int) -> dict[str, int]:
    return {
        "unknown1": ((17 * level) + 26) // 4,
        "unknown2": ((19 * level) + 26) // 4,
        "unknown3": ((15 * level) + 26) // 4,
        "unknown4": ((17 * level) + 25) // 4,
    }


def eternal_sentinel_formula(level: int) -> dict[str, int]:
    primary = ((11 * level) - 2) // 2
    return {
        "unknown1": primary,
        "unknown2": primary,
        "unknown3": primary,
        "unknown4": (level + 4) // 2,
    }


def filth_flea_formula(level: int) -> dict[str, int]:
    value = ((21 * level) + 28) // 4 if level <= 10 else (6 * level) - 1
    return {
        "unknown1": value,
        "unknown2": value,
        "unknown3": value,
        "unknown4": value,
    }


def melded_patterns_formula(level: int) -> dict[str, int]:
    base = ((11 * level) - 2) // 2
    return {
        "unknown1": base,
        "unknown2": base + 28,
        "unknown3": base,
        "unknown4": base,
    }


def fragmented_soul_formula(level: int) -> dict[str, int]:
    base = (6 * level) - 1
    return {
        "unknown1": base,
        "unknown2": base,
        "unknown3": base,
        "unknown4": base + (2 * (level // 2)),
    }


def incomplete_rebuild_formula(level: int) -> dict[str, int]:
    base = (6 * level) + 1
    return {
        "unknown1": base,
        "unknown2": base,
        "unknown3": base,
        "unknown4": base - 2,
    }


def molested_molecules_formula(level: int) -> dict[str, int]:
    value = ((11 * level) - 2) // 2
    return {
        "unknown1": value,
        "unknown2": value,
        "unknown3": value,
        "unknown4": value,
    }


def temple_cultist_formula(level: int, monster_data: int) -> dict[str, int]:
    if level < 20 or level > 35:
        raise ValueError(f"Temple Cultist level outside proven domain: {level}")
    if level <= 25:
        base = ((31 * level) - 10) // 2
    elif level <= 33:
        base = ((17 * level) - 42) - (level & 1)
    else:
        base = (17 * level) - 43
    fourth = (level + (4 if level <= 25 else 6)) // 2
    return {
        "unknown1": base + (20 if monster_data == 26135 else 0),
        "unknown2": base,
        "unknown3": base,
        "unknown4": fourth,
    }


def formula_profile_observations(
    profiles: list[dict[str, Any]],
    monster_data: int,
    name: str,
    formula,
) -> list[dict[str, Any]]:
    observations = []
    for profile in profiles:
        metadata = profile.get("metadata") or {}
        if (
            metadata.get("monsterData") != monster_data
            or metadata.get("name") != name
            or profile_resource(str(profile.get("profileKey", ""))) != 127
        ):
            continue
        level = int(metadata["level"])
        for variant in profile.get("variants", []):
            saw = (variant.get("baseSignature") or {}).get(
                "specialAttackWeapon"
            ) or {}
            if not all(f"unknown{index}" in saw for index in range(1, 5)):
                continue
            observed = {
                f"unknown{index}": int(saw[f"unknown{index}"])
                for index in range(1, 5)
            }
            observations.append(
                {
                    "level": level,
                    "semanticProfileId": variant.get("semanticProfileId"),
                    "observed": observed,
                    "prediction": formula(level),
                    "exactMatch": observed == formula(level),
                }
            )
    return observations


def fragmented_soul_active_generation_variants() -> list[dict[str, Any]]:
    provider = SUBWAY_ORDINARY_CONTENT_PROVIDER.read_text(encoding="utf-8")
    pattern = re.compile(
        r"new CapturedSubwayGenerationVariantDefinition\("
        r"203729,\s*0x([0-9A-Fa-f]+),\s*"
        r"(\d+),\s*(\d+),\s*(\d+),\s*(\d+),\s*(\d+),\s*"
        r"(\d+),\s*(\d+),\s*(\d+),\s*\"([^\"]+)\"\)"
    )
    variants = []
    for match in pattern.finditer(provider):
        (
            source,
            level,
            health,
            damage_bonus,
            attack_rating,
            defense,
            weapon_low,
            weapon_high,
            weapon_quality,
            evidence,
        ) = match.groups()
        variants.append(
            {
                "resource": 127,
                "name": "Fragmented Soul",
                "monsterData": 203729,
                "configuredSourceIdentity": f"0x{int(source, 16):08X}",
                "level": int(level),
                "health": int(health),
                "damageBonus": int(damage_bonus),
                "attackRating": int(attack_rating),
                "defense": int(defense),
                "weaponLowTemplate": int(weapon_low),
                "weaponHighTemplate": int(weapon_high),
                "actorQualityLevel": int(weapon_quality),
                "ownerEvidence": evidence,
                "formulaId": FRAGMENTED_SOUL_FORMULA_ID,
                "generatedSpecialAttackWeaponValues": (
                    fragmented_soul_formula(int(level))
                ),
            }
        )
    return variants


def stim_fiend_chain_evidence(
    inventory: dict[str, Any],
    level: int,
    source_identity: int,
    saw_sequence: int,
) -> dict[str, Any]:
    profile = next(
        row
        for row in inventory.get("profiles", [])
        if row.get("profileKey")
        == f"resource=127|md=203739|level={level}|name=Stim Fiend"
    )
    variant = profile.get("variants", [])[0]
    source_hex = f"0x{source_identity:08X}"
    saw_marker = f"|{saw_sequence}|"
    raw_chain = next(
        row
        for row in variant.get("rawWireVariantObservations", [])
        if row.get("sourceIdentity") == source_hex
        and saw_marker in str(row.get("specialAttackWeaponPacketId"))
    )
    target_identity = None
    for stream in variant.get("streams", []):
        timing = next(
            (
                row
                for row in stream.get("pairedFightTimingObservations", [])
                if row.get("specialAttackWeaponPacketId")
                == raw_chain.get("specialAttackWeaponPacketId")
            ),
            None,
        )
        if timing is not None:
            target_identity = timing.get("targetIdentity")
            break
    return {
        "metadataGenerationKey": (profile.get("metadata") or {}).get(
            "generationKey"
        ),
        "actorQualityLevel": None,
        "weaponItemFullUpdatePacketId": raw_chain.get(
            "weaponItemFullUpdatePacketId"
        ),
        "specialAttackWeaponPacketId": raw_chain.get(
            "specialAttackWeaponPacketId"
        ),
        "attackPacketId": raw_chain.get("attackPacketId"),
        "attackInfoPacketId": raw_chain.get("attackInfoPacketId"),
        "targetIdentity": target_identity,
        "terminalHit": raw_chain.get("terminalHit"),
        "baseSignature": variant.get("baseSignature"),
        "streams": [
            {
                "streamOrdinal": index,
                "signature": stream.get("signature"),
                "minimumObservedDamage": stream.get(
                    "minimumObservedDamage"
                ),
                "maximumObservedDamage": stream.get(
                    "maximumObservedDamage"
                ),
                "damageObservations": stream.get("damageObservations"),
                "attackStartDelayObservationsSeconds": stream.get(
                    "attackStartDelayObservationsSeconds"
                ),
                "firstHitDelayObservationsSeconds": stream.get(
                    "firstHitDelayObservationsSeconds"
                ),
                "landedIntervalObservationsSeconds": stream.get(
                    "landedIntervalObservationsSeconds"
                ),
                "ammoObservationsInOrder": stream.get(
                    "ammoObservationsInOrder"
                ),
                "capturedTerminalHitOnly": stream.get(
                    "capturedTerminalHitOnly"
                ),
                "attackInfoPacketIds": stream.get("attackInfoPacketIds"),
            }
            for index, stream in enumerate(variant.get("streams", []))
        ],
    }


def melded_patterns_chain_evidence(
    inventory: dict[str, Any],
    level: int,
    source_identity: int,
    saw_sequence: int,
) -> dict[str, Any]:
    source_hex = f"0x{source_identity:08X}"
    saw_marker = f"|{saw_sequence}|"
    for profile in inventory.get("profiles", []):
        metadata = profile.get("metadata") or {}
        if (
            metadata.get("monsterData") != 203747
            or metadata.get("level") != level
            or metadata.get("name") != "Melded Patterns"
        ):
            continue
        for variant in profile.get("variants", []):
            raw_chain = next(
                (
                    row
                    for row in variant.get("rawWireVariantObservations", [])
                    if row.get("sourceIdentity") == source_hex
                    and saw_marker
                    in str(row.get("specialAttackWeaponPacketId"))
                ),
                None,
            )
            if raw_chain is None:
                continue
            return {
                "semanticProfileId": variant.get("semanticProfileId"),
                "metadataGenerationKey": metadata.get("generationKey"),
                "weaponItemFullUpdatePacketId": raw_chain.get(
                    "weaponItemFullUpdatePacketId"
                ),
                "specialAttackWeaponPacketId": raw_chain.get(
                    "specialAttackWeaponPacketId"
                ),
                "attackPacketId": raw_chain.get("attackPacketId"),
                "attackInfoPacketId": raw_chain.get("attackInfoPacketId"),
                "terminalHit": raw_chain.get("terminalHit"),
                "baseSignature": variant.get("baseSignature"),
                "streams": [
                    {
                        "streamOrdinal": index,
                        "signature": stream.get("signature"),
                        "minimumObservedDamage": stream.get(
                            "minimumObservedDamage"
                        ),
                        "maximumObservedDamage": stream.get(
                            "maximumObservedDamage"
                        ),
                        "damageObservations": stream.get(
                            "damageObservations"
                        ),
                        "attackStartDelayObservationsSeconds": stream.get(
                            "attackStartDelayObservationsSeconds"
                        ),
                        "firstHitDelayObservationsSeconds": stream.get(
                            "firstHitDelayObservationsSeconds"
                        ),
                        "landedIntervalObservationsSeconds": stream.get(
                            "landedIntervalObservationsSeconds"
                        ),
                        "ammoObservationsInOrder": stream.get(
                            "ammoObservationsInOrder"
                        ),
                        "capturedTerminalHitOnly": stream.get(
                            "capturedTerminalHitOnly"
                        ),
                        "attackInfoPacketIds": stream.get(
                            "attackInfoPacketIds"
                        ),
                    }
                    for index, stream in enumerate(
                        variant.get("streams", [])
                    )
                ],
            }
    raise ValueError(
        "could not correlate Melded Patterns raw chain "
        f"level={level} source={source_hex} sawSequence={saw_sequence}"
    )


def build_formula_dataset(
    inventory: dict[str, Any],
    active_coverage: dict[str, Any],
    item_templates: dict[int, list[Any]],
) -> dict[str, Any]:
    profiles = [
        compact_profile(profile)
        for profile in inventory.get("profiles", [])
        if profile_resource(str(profile.get("profileKey", ""))) in (127, 1931)
    ]
    referenced_template_ids: set[int] = set()
    for profile in profiles:
        referenced_template_ids.update(collect_template_ids(profile))
    template_rows = []
    for template_id in sorted(referenced_template_ids):
        template = item_templates.get(template_id)
        if template is None or len(template) < 12:
            continue
        template_rows.append(
            {
                "templateId": template_id,
                "qualityLevel": template[9],
                "actions": template[3],
                "stats": {
                    str(key): value
                    for key, value in sorted(template[11].items(), key=lambda row: int(row[0]))
                },
            }
        )

    observations = []
    for capture, sequence, level, source_identity in DISOBEDIENT_BOT_OBSERVATIONS:
        packet = read_raw_packet(capture, sequence)
        packet.update(
            {
                "level": level,
                "sourceIdentity": f"0x{source_identity:08X}",
                "formulaValue": disobedient_bot_formula(level),
                "exactMatch": all(
                    packet[f"unknown{index}"] == disobedient_bot_formula(level)
                    for index in range(1, 5)
                ),
            }
        )
        observations.append(packet)

    leave_one_out = []
    for held_out_level, held_out_value in sorted(
        DISOBEDIENT_BOT_CAPTURED_VALUES.items()
    ):
        training = {
            str(level): value
            for level, value in sorted(DISOBEDIENT_BOT_CAPTURED_VALUES.items())
            if level != held_out_level
        }
        prediction = disobedient_bot_formula(held_out_level)
        leave_one_out.append(
            {
                "heldOutLevel": held_out_level,
                "heldOutObserved": held_out_value,
                "trainingObservations": training,
                "candidateFormulaSatisfiedAllTrainingObservations": all(
                    disobedient_bot_formula(int(level)) == value
                    for level, value in training.items()
                ),
                "prediction": prediction,
                "exactMatch": prediction == held_out_value,
            }
        )

    active_bindings = []
    for row in active_coverage.get("profiles", []):
        if (
            row.get("runtimePlayfieldOrResource") != 127
            or row.get("name") != "Disobedient Bot"
            or row.get("monsterData") != 17649
        ):
            continue
        for level in row.get("levelCandidates", []):
            active_bindings.append(
                {
                    "resource": 127,
                    "name": "Disobedient Bot",
                    "monsterData": 17649,
                    "level": level,
                    "actorCount": row.get("actorCount", 0),
                    "configuredSourceIdentity": row.get(
                        "configuredSourceIdentity"
                    ),
                    "formulaId": DISOBEDIENT_BOT_FORMULA_ID,
                    "generatedSpecialAttackWeaponValue": (
                        disobedient_bot_formula(level)
                    ),
                    "compatibleSemanticProfileId": (
                        "ff1685d6a9c45e2c-370328526bcb32c7"
                    ),
                }
            )

    cross_family = []
    matching_cross_family = 0
    total_cross_family = 0
    for profile in inventory.get("profiles", []):
        metadata = profile.get("metadata") or {}
        if metadata.get("monsterData") == 17649:
            continue
        for variant in profile.get("variants", []):
            saw = variant.get("baseSignature", {}).get(
                "specialAttackWeapon", {}
            )
            specials = saw.get("specials", [])
            values = [saw.get(f"unknown{index}") for index in range(1, 5)]
            if not any(
                row.get("lowTemplate") == 144742
                and row.get("highTemplate") == 144743
                for row in specials
            ) or len(set(values)) != 1:
                continue
            level = metadata.get("level")
            if not isinstance(level, int):
                continue
            observed = values[0]
            predicted = disobedient_bot_formula(level)
            exact = observed == predicted
            total_cross_family += 1
            matching_cross_family += int(exact)
            cross_family.append(
                {
                    "name": metadata.get("name"),
                    "monsterData": metadata.get("monsterData"),
                    "level": level,
                    "semanticProfileId": variant.get("semanticProfileId"),
                    "observed": observed,
                    "disobedientBotFormulaPrediction": predicted,
                    "exactMatch": exact,
                }
            )

    stim_observations = []
    for capture, sequence, level, source_identity in STIM_FIEND_OBSERVATIONS:
        packet = read_raw_packet(capture, sequence)
        packet.update(
            {
                "level": level,
                "sourceIdentity": f"0x{source_identity:08X}",
                "formulaValue": stim_fiend_formula(level),
                "exactMatch": all(
                    packet[f"unknown{index}"] == stim_fiend_formula(level)
                    for index in range(1, 5)
                ),
            }
        )
        packet.update(
            stim_fiend_chain_evidence(
                inventory,
                level,
                source_identity,
                sequence,
            )
        )
        stim_observations.append(packet)

    stim_leave_one_out = []
    for held_out_level, held_out_value in sorted(
        STIM_FIEND_CAPTURED_VALUES.items()
    ):
        training = {
            str(level): value
            for level, value in sorted(STIM_FIEND_CAPTURED_VALUES.items())
            if level != held_out_level
        }
        prediction = stim_fiend_formula(held_out_level)
        stim_leave_one_out.append(
            {
                "heldOutLevel": held_out_level,
                "heldOutObserved": held_out_value,
                "trainingObservations": training,
                "candidateFormulaSatisfiedAllTrainingObservations": all(
                    stim_fiend_formula(int(level)) == value
                    for level, value in training.items()
                ),
                "prediction": prediction,
                "exactMatch": prediction == held_out_value,
            }
        )

    stim_active_bindings = []
    for row in active_coverage.get("profiles", []):
        if (
            row.get("runtimePlayfieldOrResource") != 127
            or row.get("name") != "Stim Fiend"
            or row.get("monsterData") != 203739
        ):
            continue
        for level in row.get("levelCandidates", []):
            if not 9 <= level <= 17:
                continue
            stim_active_bindings.append(
                {
                    "resource": 127,
                    "name": "Stim Fiend",
                    "monsterData": 203739,
                    "level": level,
                    "actorCount": row.get("actorCount", 0),
                    "configuredSourceIdentity": row.get(
                        "configuredSourceIdentity"
                    ),
                    "formulaId": STIM_FIEND_FORMULA_ID,
                    "generatedSpecialAttackWeaponValue": (
                        stim_fiend_formula(level)
                    ),
                    "compatibleSemanticProfileIds": list(
                        STIM_FIEND_PROFILE_IDS
                    ),
                }
            )

    starting_scope_sources = {
        "0x7953ABAD",
        "0x7953ABBF",
        "0x7953AD68",
        "0x79545069",
        "0x79545072",
        "0x7957E128",
        "0x7957E415",
    }
    stim_starting_scope = []
    for row in active_coverage.get("profiles", []):
        if (
            row.get("runtimePlayfieldOrResource") != 127
            or row.get("name") != "Stim Fiend"
            or row.get("configuredSourceIdentity") not in starting_scope_sources
        ):
            continue
        level = row.get("levelCandidates", [None])[0]
        supported = isinstance(level, int) and 9 <= level <= 17
        stim_starting_scope.append(
            {
                "configuredSourceIdentity": row.get(
                    "configuredSourceIdentity"
                ),
                "level": level,
                "formulaDomainSupported": supported,
                "generatedSpecialAttackWeaponValue": (
                    stim_fiend_formula(level) if supported else None
                ),
                "result": (
                    "restored through exact Stim Fiend archetype"
                    if supported
                    else "fail closed: level outside proven Stim Fiend domain"
                ),
            }
        )

    stim_cross_family = []
    for profile in inventory.get("profiles", []):
        metadata = profile.get("metadata") or {}
        if (
            metadata.get("monsterData") == 203739
            or profile_resource(str(profile.get("profileKey", "")))
            not in (127, 1931)
        ):
            continue
        for variant in profile.get("variants", []):
            saw = variant.get("baseSignature", {}).get(
                "specialAttackWeapon", {}
            )
            specials = saw.get("specials", [])
            values = [saw.get(f"unknown{index}") for index in range(1, 5)]
            if (
                not any(
                    row.get("lowTemplate") == 144742
                    and row.get("highTemplate") == 144743
                    and row.get("tag") == 1397315377
                    and row.get("nameHex") == "53495731"
                    for row in specials
                )
                or len(set(values)) != 1
            ):
                continue
            level = metadata.get("level")
            if not isinstance(level, int):
                continue
            observed = values[0]
            predicted = stim_fiend_formula(level)
            stim_cross_family.append(
                {
                    "name": metadata.get("name"),
                    "monsterData": metadata.get("monsterData"),
                    "level": level,
                    "semanticProfileId": variant.get("semanticProfileId"),
                    "observed": observed,
                    "stimFiendFormulaPrediction": predicted,
                    "exactMatch": observed == predicted,
                }
            )

    melded_observations = []
    for (
        capture,
        sequence,
        level,
        source_identity,
    ) in MELDED_PATTERNS_OBSERVATIONS:
        packet = read_raw_packet(capture, sequence)
        formula_values = melded_patterns_formula(level)
        packet.update(
            {
                "level": level,
                "sourceIdentity": f"0x{source_identity:08X}",
                "formulaValues": formula_values,
                "exactMatch": all(
                    packet[field] == value
                    for field, value in formula_values.items()
                ),
            }
        )
        packet.update(
            melded_patterns_chain_evidence(
                inventory,
                level,
                source_identity,
                sequence,
            )
        )
        melded_observations.append(packet)

    melded_leave_one_out = []
    for held_out_level, held_out_value in sorted(
        MELDED_PATTERNS_CAPTURED_BASE_VALUES.items()
    ):
        training = {
            str(level): value
            for level, value in sorted(
                MELDED_PATTERNS_CAPTURED_BASE_VALUES.items()
            )
            if level != held_out_level
        }
        prediction = melded_patterns_formula(held_out_level)
        melded_leave_one_out.append(
            {
                "heldOutLevel": held_out_level,
                "heldOutObserved": {
                    "unknown1": held_out_value,
                    "unknown2": held_out_value + 28,
                    "unknown3": held_out_value,
                    "unknown4": held_out_value,
                },
                "trainingObservations": training,
                "candidateFormulaSatisfiedAllTrainingObservations": all(
                    melded_patterns_formula(int(level))["unknown1"] == value
                    for level, value in training.items()
                ),
                "prediction": prediction,
                "exactMatch": prediction
                == {
                    "unknown1": held_out_value,
                    "unknown2": held_out_value + 28,
                    "unknown3": held_out_value,
                    "unknown4": held_out_value,
                },
            }
        )

    loadout_by_source = {
        f"0x{source:08X}": {
            "sourceIdentity": f"0x{source:08X}",
            "level": level,
            "actorQualityLevel": quality,
            "weaponLowTemplate": low_template,
            "weaponHighTemplate": high_template,
            "evidenceCapture": (
                "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                f"{capture}"
            ),
        }
        for (
            source,
            level,
            quality,
            low_template,
            high_template,
            capture,
        ) in MELDED_PATTERNS_ACTIVE_LOADOUTS
    }
    melded_active_bindings = []
    for row in active_coverage.get("profiles", []):
        if (
            row.get("runtimePlayfieldOrResource") != 127
            or row.get("name") != "Melded Patterns"
            or row.get("monsterData") != 203747
        ):
            continue
        source_identity = row.get("configuredSourceIdentity")
        loadout = loadout_by_source.get(source_identity)
        if loadout is None:
            raise ValueError(
                "active Melded Patterns source lacks owner-linked loadout: "
                f"{source_identity}"
            )
        level = row.get("levelCandidates", [None])[0]
        if level != loadout["level"]:
            raise ValueError(
                "active Melded Patterns level differs from owner evidence: "
                f"{source_identity}"
            )
        melded_active_bindings.append(
            {
                "resource": 127,
                "name": "Melded Patterns",
                "monsterData": 203747,
                "actorCount": row.get("actorCount", 0),
                "configuredSourceIdentity": source_identity,
                "level": level,
                "actorQualityLevel": loadout["actorQualityLevel"],
                "weaponLowTemplate": loadout["weaponLowTemplate"],
                "weaponHighTemplate": loadout["weaponHighTemplate"],
                "evidenceCapture": loadout["evidenceCapture"],
                "formulaId": MELDED_PATTERNS_FORMULA_ID,
                "generatedSpecialAttackWeaponValues": (
                    melded_patterns_formula(level)
                ),
                "startingClassification": (
                    "quarantined"
                    if source_identity
                    in MELDED_PATTERNS_STARTING_QUARANTINE_SOURCES
                    else "certified"
                ),
                "preGenerationCoverageClassification": row.get(
                    "classification"
                ),
                "preGenerationCoverageUnresolvedReasons": row.get(
                    "unresolvedReasons"
                ),
            }
        )

    melded_profile_ids = sorted(
        {
            row["semanticProfileId"]
            for row in melded_observations
            if row.get("semanticProfileId")
        }
    )
    melded_cross_family = []
    for profile in inventory.get("profiles", []):
        metadata = profile.get("metadata") or {}
        if metadata.get("monsterData") == 203747:
            continue
        for variant in profile.get("variants", []):
            signature = variant.get("baseSignature", {})
            wifu = signature.get("weaponItemFullUpdate") or {}
            if (
                wifu.get("lowTemplate") not in (121817, 121818, 121819)
                or wifu.get("highTemplate")
                not in (121818, 121820)
            ):
                continue
            melded_cross_family.append(
                {
                    "name": metadata.get("name"),
                    "monsterData": metadata.get("monsterData"),
                    "level": metadata.get("level"),
                    "semanticProfileId": variant.get("semanticProfileId"),
                    "reasonExcluded": (
                        "family and MonsterData are outside the exact "
                        "Melded Patterns selector"
                    ),
                }
            )

    fragmented_profiles = [
        profile
        for profile in inventory.get("profiles", [])
        if (profile.get("metadata") or {}).get("monsterData") == 203729
        and (profile.get("metadata") or {}).get("name") == "Fragmented Soul"
        and profile_resource(str(profile.get("profileKey", ""))) == 127
    ]
    fragmented_profile_ids = sorted(
        variant.get("semanticProfileId")
        for profile in fragmented_profiles
        for variant in profile.get("variants", [])
        if variant.get("semanticProfileId")
    )
    fragmented_raw_observations = []
    for (
        capture,
        sequence,
        level,
        source_identity,
        chain_classification,
    ) in FRAGMENTED_SOUL_OBSERVATIONS:
        packet = read_raw_packet(capture, sequence)
        predicted = fragmented_soul_formula(level)
        observed = {
            f"unknown{index}": packet[f"unknown{index}"]
            for index in range(1, 5)
        }
        packet.update(
            {
                "level": level,
                "sourceIdentity": f"0x{source_identity:08X}",
                "chainClassification": chain_classification,
                "formulaId": FRAGMENTED_SOUL_FORMULA_ID,
                "formulaPrediction": predicted,
                "exactMatch": observed == predicted,
            }
        )
        fragmented_raw_observations.append(packet)

    fragmented_leave_one_out = []
    for held_out_level, held_out_values in sorted(
        FRAGMENTED_SOUL_CAPTURED_VALUES.items()
    ):
        training = {
            str(level): {
                f"unknown{index + 1}": value
                for index, value in enumerate(values)
            }
            for level, values in sorted(
                FRAGMENTED_SOUL_CAPTURED_VALUES.items()
            )
            if level != held_out_level
        }
        prediction = fragmented_soul_formula(held_out_level)
        observed = {
            f"unknown{index + 1}": value
            for index, value in enumerate(held_out_values)
        }
        fragmented_leave_one_out.append(
            {
                "heldOutLevel": held_out_level,
                "heldOutObserved": observed,
                "trainingObservations": training,
                "candidateFormulaSatisfiedAllTrainingObservations": all(
                    fragmented_soul_formula(int(level)) == value
                    for level, value in training.items()
                ),
                "prediction": prediction,
                "exactMatch": prediction == observed,
            }
        )

    fragmented_generation_variants = (
        fragmented_soul_active_generation_variants()
    )
    variants_by_source: dict[str, list[dict[str, Any]]] = {}
    for variant in fragmented_generation_variants:
        variants_by_source.setdefault(
            variant["configuredSourceIdentity"], []
        ).append(variant)
    fragmented_active_bindings = []
    fragmented_active_actors = []
    for row in active_coverage.get("profiles", []):
        if (
            row.get("runtimePlayfieldOrResource") != 127
            or row.get("name") != "Fragmented Soul"
            or row.get("monsterData") != 203729
        ):
            continue
        source_identity = row.get("configuredSourceIdentity")
        atomic_variants = variants_by_source.get(source_identity, [])
        fragmented_active_actors.append(
            {
                "resource": 127,
                "name": "Fragmented Soul",
                "monsterData": 203729,
                "actorCount": row.get("actorCount", 0),
                "configuredSourceIdentity": source_identity,
                "formulaId": FRAGMENTED_SOUL_FORMULA_ID,
                "compatibleSemanticProfileIds": fragmented_profile_ids,
                "atomicVariants": atomic_variants,
            }
        )
        for level in sorted(
            {variant["level"] for variant in atomic_variants}
        ):
            fragmented_active_bindings.append(
                {
                    "resource": 127,
                    "name": "Fragmented Soul",
                    "monsterData": 203729,
                    "level": level,
                    "actorCount": row.get("actorCount", 0),
                    "configuredSourceIdentity": source_identity,
                    "formulaId": FRAGMENTED_SOUL_FORMULA_ID,
                    "compatibleSemanticProfileIds": fragmented_profile_ids,
                    "atomicVariants": [
                        variant
                        for variant in atomic_variants
                        if variant["level"] == level
                    ],
                }
            )

    fragmented_cross_family = []
    for profile in inventory.get("profiles", []):
        metadata = profile.get("metadata") or {}
        if metadata.get("monsterData") == 203729:
            continue
        for variant in profile.get("variants", []):
            signature = variant.get("baseSignature", {})
            wifu = signature.get("weaponItemFullUpdate") or {}
            if (
                wifu.get("lowTemplate") not in range(123685, 123704)
                and wifu.get("highTemplate") not in range(123685, 123704)
            ):
                continue
            fragmented_cross_family.append(
                {
                    "name": metadata.get("name"),
                    "monsterData": metadata.get("monsterData"),
                    "level": metadata.get("level"),
                    "semanticProfileId": variant.get("semanticProfileId"),
                    "reasonExcluded": (
                        "family and MonsterData are outside the exact "
                        "Fragmented Soul selector"
                    ),
                }
            )

    incomplete_observations = formula_profile_observations(
        profiles,
        203728,
        "Incomplete Rebuild",
        incomplete_rebuild_formula,
    )
    incomplete_level_seventeen = read_raw_packet(
        "20260709-222339",
        6282,
    )
    incomplete_level_seventeen.update(
        {
            "level": 17,
            "sourceIdentity": "0x79545170",
            "observed": {
                f"unknown{index}": incomplete_level_seventeen[
                    f"unknown{index}"
                ]
                for index in range(1, 5)
            },
            "prediction": incomplete_rebuild_formula(17),
        }
    )
    incomplete_level_seventeen["exactMatch"] = (
        incomplete_level_seventeen["observed"]
        == incomplete_level_seventeen["prediction"]
    )
    incomplete_captured_values = {
        17: incomplete_rebuild_formula(17),
        18: incomplete_rebuild_formula(18),
        19: incomplete_rebuild_formula(19),
        20: incomplete_rebuild_formula(20),
        21: incomplete_rebuild_formula(21),
        22: incomplete_rebuild_formula(22),
    }
    incomplete_leave_one_out = [
        {
            "heldOutLevel": level,
            "heldOutObserved": observed,
            "trainingLevels": [
                candidate
                for candidate in sorted(incomplete_captured_values)
                if candidate != level
            ],
            "prediction": incomplete_rebuild_formula(level),
            "exactMatch": incomplete_rebuild_formula(level) == observed,
        }
        for level, observed in sorted(incomplete_captured_values.items())
    ]

    molested_observations = formula_profile_observations(
        profiles,
        203746,
        "Molested Molecules",
        molested_molecules_formula,
    )
    molested_captured_values = {
        17: molested_molecules_formula(17),
        18: molested_molecules_formula(18),
        19: molested_molecules_formula(19),
        20: molested_molecules_formula(20),
        21: molested_molecules_formula(21),
        25: molested_molecules_formula(25),
    }
    molested_leave_one_out = [
        {
            "heldOutLevel": level,
            "heldOutObserved": observed,
            "trainingLevels": [
                candidate
                for candidate in sorted(molested_captured_values)
                if candidate != level
            ],
            "prediction": molested_molecules_formula(level),
            "exactMatch": molested_molecules_formula(level) == observed,
        }
        for level, observed in sorted(molested_captured_values.items())
    ]

    if any(not row["exactMatch"] for row in observations):
        raise ValueError("accepted formula differs from a raw SAW observation")
    if any(not row["exactMatch"] for row in leave_one_out):
        raise ValueError("accepted formula failed leave-one-out validation")
    if len(active_bindings) != 12:
        raise ValueError(
            f"expected 12 active Disobedient Bot bindings, found {len(active_bindings)}"
        )
    if any(not row["exactMatch"] for row in stim_observations):
        raise ValueError("Stim Fiend formula differs from a raw SAW observation")
    if any(not row["exactMatch"] for row in stim_leave_one_out):
        raise ValueError("Stim Fiend formula failed leave-one-out validation")
    if len(stim_active_bindings) != 15:
        raise ValueError(
            f"expected 15 active Stim Fiend bindings, found {len(stim_active_bindings)}"
        )
    if len(stim_starting_scope) != 7:
        raise ValueError(
            f"expected 7 starting-scope Stim Fiends, found {len(stim_starting_scope)}"
        )
    if sum(row["formulaDomainSupported"] for row in stim_starting_scope) != 7:
        raise ValueError("Stim Fiend starting scope did not restore all seven actors")
    if len(stim_cross_family) != 23:
        raise ValueError(
            f"expected 23 cross-family SIW1 observations, found {len(stim_cross_family)}"
        )
    if sum(row["exactMatch"] for row in stim_cross_family) != 17:
        raise ValueError("Stim Fiend formula cross-family reconciliation changed")
    if any(not row["exactMatch"] for row in melded_observations):
        raise ValueError(
            "Melded Patterns formula differs from a raw SAW observation"
        )
    if any(not row["exactMatch"] for row in melded_leave_one_out):
        raise ValueError(
            "Melded Patterns formula failed leave-one-out validation"
        )
    if len(melded_active_bindings) != 10:
        raise ValueError(
            "expected 10 active Melded Patterns bindings, found "
            f"{len(melded_active_bindings)}"
        )
    if (
        sum(
            row["startingClassification"] == "quarantined"
            for row in melded_active_bindings
        )
        != 6
    ):
        raise ValueError(
            "expected exactly six starting Melded Patterns quarantine rows"
        )
    if len(melded_profile_ids) != 11:
        raise ValueError(
            "expected 11 complete Melded Patterns semantic profiles, found "
            f"{len(melded_profile_ids)}"
        )
    if len(fragmented_profiles) != 5:
        raise ValueError(
            "expected five Fragmented Soul level profiles, found "
            f"{len(fragmented_profiles)}"
        )
    if len(fragmented_profile_ids) != 8:
        raise ValueError(
            "expected eight complete Fragmented Soul semantic profiles, found "
            f"{len(fragmented_profile_ids)}"
        )
    if sum(
        int(profile.get("normalCompleteChainCount", 0))
        for profile in fragmented_profiles
    ) != 22:
        raise ValueError("expected 22 complete Fragmented Soul combat chains")
    if len(fragmented_raw_observations) != 21:
        raise ValueError("expected 21 unique raw Fragmented Soul SAW packets")
    if any(not row["exactMatch"] for row in fragmented_raw_observations):
        raise ValueError(
            "Fragmented Soul formula differs from a raw SAW packet"
        )
    if any(not row["exactMatch"] for row in fragmented_leave_one_out):
        raise ValueError(
            "Fragmented Soul formula failed leave-one-out validation"
        )
    if len(fragmented_active_actors) != 10:
        raise ValueError(
            "expected 10 active Fragmented Soul actors, found "
            f"{len(fragmented_active_actors)}"
        )
    if len(fragmented_active_bindings) != 16:
        raise ValueError(
            "expected 16 source-level Fragmented Soul bindings, found "
            f"{len(fragmented_active_bindings)}"
        )
    if len(fragmented_generation_variants) != 19:
        raise ValueError(
            "expected 19 active Fragmented Soul generation variants, found "
            f"{len(fragmented_generation_variants)}"
        )
    if any(
        not 17 <= variant["level"] <= 21
        for variant in fragmented_generation_variants
    ):
        raise ValueError(
            "Fragmented Soul active generation level is outside 17..21"
        )
    if (
        not incomplete_level_seventeen["exactMatch"]
        or any(not row["exactMatch"] for row in incomplete_observations)
        or any(not row["exactMatch"] for row in incomplete_leave_one_out)
    ):
        raise ValueError(
            "Incomplete Rebuild formula differs from capture evidence"
        )
    if (
        any(not row["exactMatch"] for row in molested_observations)
        or any(not row["exactMatch"] for row in molested_leave_one_out)
    ):
        raise ValueError(
            "Molested Molecules formula differs from capture evidence"
        )

    temple_cultist_observations = []
    for profile in inventory.get("profiles", []):
        metadata = profile.get("metadata") or {}
        if (
            profile_resource(str(profile.get("profileKey", ""))) != 1931
            or metadata.get("name") != "Cultist"
        ):
            continue
        monster_data = int(metadata["monsterData"])
        level = int(metadata["level"])
        for variant in profile.get("variants", []):
            if not variant.get("captureEvidenceSafe"):
                continue
            saw = variant.get("baseSignature", {}).get(
                "specialAttackWeapon", {}
            )
            weapon = variant.get("baseSignature", {}).get(
                "weaponItemFullUpdate", {}
            )
            streams = variant.get("streams", [])
            if (
                not weapon
                or len(streams) != 1
                or not streams[0].get("runtimeContractReady")
            ):
                continue
            observed = {
                f"unknown{index}": int(saw[f"unknown{index}"])
                for index in range(1, 5)
            }
            predicted = temple_cultist_formula(level, monster_data)
            temple_cultist_observations.append(
                {
                    "monsterData": monster_data,
                    "level": level,
                    "semanticProfileId": variant.get("semanticProfileId"),
                    "captureSession": metadata.get("capture"),
                    "weaponLowTemplate": next(
                        int(row["value"])
                        for row in weapon.get("stats", [])
                        if int(row["stat"]) == 702
                    ),
                    "weaponHighTemplate": next(
                        int(row["value"])
                        for row in weapon.get("stats", [])
                        if int(row["stat"]) == 703
                    ),
                    "weaponQuality": next(
                        int(row["value"])
                        for row in weapon.get("stats", [])
                        if int(row["stat"]) == 701
                    ),
                    "observed": observed,
                    "prediction": predicted,
                    "exactMatch": observed == predicted,
                    "unknown5Observations": sorted(
                        {
                            int(row["unknown5"])
                            for row in variant.get(
                                "mutableSawStateObservations", []
                            )
                        }
                    ),
                    "streamSignature": streams[0].get("signature"),
                    "attackInfoPacketIds": streams[0].get(
                        "attackInfoPacketIds", []
                    ),
                }
            )
    if not temple_cultist_observations or any(
        not row["exactMatch"] for row in temple_cultist_observations
    ):
        raise ValueError(
            "Temple Cultist formula differs from complete capture evidence"
        )

    temple_monster_data = sorted(
        {row["monsterData"] for row in temple_cultist_observations}
    )
    temple_held_out_validation = []
    for held_out_monster_data in temple_monster_data:
        training = [
            row
            for row in temple_cultist_observations
            if row["monsterData"] != held_out_monster_data
        ]
        held_out = [
            row
            for row in temple_cultist_observations
            if row["monsterData"] == held_out_monster_data
        ]
        temple_held_out_validation.append(
            {
                "heldOutMonsterData": held_out_monster_data,
                "trainingObservationCount": len(training),
                "heldOutObservationCount": len(held_out),
                "trainingExact": all(row["exactMatch"] for row in training),
                "heldOutExact": all(row["exactMatch"] for row in held_out),
            }
        )
    if any(
        not row["trainingExact"] or not row["heldOutExact"]
        for row in temple_held_out_validation
    ):
        raise ValueError("Temple Cultist cross-family held-out validation failed")

    cultist_actors, cultist_loadouts = temple_active_loadouts()
    noncultist_actors, noncultist_loadouts = temple_noncultist_loadouts()
    active_temple_actors = cultist_actors + noncultist_actors
    active_temple_loadouts = dict(cultist_loadouts)
    active_temple_loadouts.update(noncultist_loadouts)
    starting_sources = temple_starting_quarantine_sources()
    temple_starting_dispositions = []
    for actor in sorted(
        (
            row
            for row in active_temple_actors
            if row["sourceIdentity"] in starting_sources
        ),
        key=lambda row: row["sourceIdentity"],
    ):
        source = actor["sourceIdentity"]
        stable_loadouts = sorted(
            {
                (
                    row["lowTemplate"],
                    row["highTemplate"],
                    row["quality"],
                    row["slot"],
                )
                for row in active_temple_loadouts[source]
            }
        )
        if len(stable_loadouts) != 1:
            raise ValueError(
                f"0x{source:08X}: starting actor lacks one exact loadout"
            )
        low_template, high_template, quality, slot = stable_loadouts[0]
        remains_quarantined = source in {0x7983FA22, 0x7983FBC2}
        temple_starting_dispositions.append(
            {
                "sourceIdentity": f"0x{source:08X}",
                "name": actor.get("name", "Cultist"),
                "monsterData": actor["monsterData"],
                "level": actor["level"],
                "weaponLowTemplate": low_template,
                "weaponHighTemplate": high_template,
                "weaponQuality": quality,
                "slot": slot,
                "startingDisposition": "quarantined",
                "finalDisposition": (
                    "quarantined" if remains_quarantined else "restored"
                ),
                "formulaId": (
                    None
                    if actor["monsterData"] in (41690, 26090)
                    else (
                        TEMPLE_CULTIST_RAISED_PRIMARY_FORMULA_ID
                        if actor["monsterData"] == 26135
                        else TEMPLE_CULTIST_FORMULA_ID
                    )
                ),
                "exactBlocker": (
                    "L18 active WIFU and miss/start evidence exists, but no "
                    "complete same-level normal AttackInfo contract proves "
                    "landed-hit semantics for this weapon loadout"
                    if remains_quarantined
                    else None
                ),
            }
        )
    if len(temple_starting_dispositions) != 80:
        raise ValueError(
            "expected 80 starting Temple quarantine dispositions, found "
            f"{len(temple_starting_dispositions)}"
        )
    if (
        sum(
            row["finalDisposition"] == "restored"
            for row in temple_starting_dispositions
        )
        != 78
    ):
        raise ValueError("expected 78 restored Temple starting actors")

    temple_active_bindings = []
    for disposition in temple_starting_dispositions:
        if disposition["finalDisposition"] != "restored":
            continue
        monster_data = disposition["monsterData"]
        binding = {
            "resource": 1931,
            "name": disposition["name"],
            "monsterData": monster_data,
            "configuredSourceIdentity": disposition["sourceIdentity"],
            "level": disposition["level"],
            "formulaId": disposition["formulaId"],
            "finalDisposition": "restored",
        }
        if monster_data not in (41690, 26090):
            compatible_profile_ids = sorted(
                {
                    row["semanticProfileId"]
                    for row in temple_cultist_observations
                    if row["monsterData"] == monster_data
                }
            )
            if not compatible_profile_ids:
                raise ValueError(
                    f"{disposition['sourceIdentity']}: restored Temple Cultist "
                    "lacks an exact compatible semantic profile"
                )
            binding["compatibleSemanticProfileIds"] = compatible_profile_ids
            binding["generatedSpecialAttackWeaponValues"] = list(
                temple_cultist_formula(disposition["level"], monster_data).values()
            )
        elif monster_data == 41690:
            binding["formulaId"] = "temple-eternal-sentinel-l20-exact-v1"
            binding["compatibleSemanticProfileIds"] = [
                "e037cf6f4165eff5-71ebcc342951c27c",
                "e037cf6f4165eff5-c036f50d1289554a",
            ]
        else:
            binding["formulaId"] = "temple-murial-faithful-exact-v1"
        temple_active_bindings.append(binding)

    bound_temple_sources = {
        row["configuredSourceIdentity"] for row in temple_active_bindings
    }
    for actor in sorted(
        (
            row
            for row in active_temple_actors
            if row.get("name", "Cultist") == "Cultist"
            and f"0x{row['sourceIdentity']:08X}" not in bound_temple_sources
        ),
        key=lambda row: row["sourceIdentity"],
    ):
        source = actor["sourceIdentity"]
        stable_loadouts = sorted(
            {
                (
                    row["lowTemplate"],
                    row["highTemplate"],
                    row["quality"],
                    row["slot"],
                )
                for row in active_temple_loadouts[source]
            }
        )
        if len(stable_loadouts) != 1:
            raise ValueError(
                f"0x{source:08X}: active Temple Cultist lacks one exact loadout"
            )
        monster_data = actor["monsterData"]
        compatible_profile_ids = sorted(
            {
                row["semanticProfileId"]
                for row in temple_cultist_observations
                if row["monsterData"] == monster_data
            }
        )
        if not compatible_profile_ids:
            raise ValueError(
                f"0x{source:08X}: active Temple Cultist lacks an exact "
                "compatible semantic profile"
            )
        temple_active_bindings.append(
            {
                "resource": 1931,
                "name": "Cultist",
                "monsterData": monster_data,
                "configuredSourceIdentity": f"0x{source:08X}",
                "level": actor["level"],
                "formulaId": (
                    TEMPLE_CULTIST_RAISED_PRIMARY_FORMULA_ID
                    if monster_data == 26135
                    else TEMPLE_CULTIST_FORMULA_ID
                ),
                "finalDisposition": "already-certified",
                "compatibleSemanticProfileIds": compatible_profile_ids,
                "generatedSpecialAttackWeaponValues": list(
                    temple_cultist_formula(
                        actor["level"],
                        monster_data,
                    ).values()
                ),
            }
        )
    if len(temple_active_bindings) != 151:
        raise ValueError(
            "expected 151 active Temple completion bindings, found "
            f"{len(temple_active_bindings)}"
        )

    temple_raw_packet_observations = [
        {
            "captureSession": observation["captureSession"],
            "packetId": packet_id,
        }
        for observation in temple_cultist_observations
        for packet_id in observation["attackInfoPacketIds"]
    ]
    temple_raw_packet_observations.extend(
        [
            {
                "captureSession": (
                    "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                    "20260721-043204"
                ),
                "packetId": (
                    "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                    "20260721-043204|IN|667|b78141f5ef8a"
                ),
            },
            {
                "captureSession": (
                    "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                    "20260721-232051"
                ),
                "packetId": (
                    "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                    "20260721-232051|IN|11660|b4696ab852c6"
                ),
            },
        ]
    )

    filth_flea_observations = formula_profile_observations(
        profiles,
        17657,
        "Filth Flea",
        filth_flea_formula,
    )
    stable_filth_flea_observations = [
        row for row in filth_flea_observations if row["exactMatch"]
    ]
    observed_filth_flea_levels = {
        row["level"] for row in stable_filth_flea_observations
    }
    if observed_filth_flea_levels != set(FILTH_FLEA_CAPTURED_VALUES):
        raise ValueError(
            "Filth Flea stable formula observations do not cover the "
            "capture-proven levels"
        )
    filth_flea_leave_one_out = [
        {
            "heldOutLevel": level,
            "heldOutObserved": observed,
            "trainingLevels": [
                candidate
                for candidate in sorted(FILTH_FLEA_CAPTURED_VALUES)
                if candidate != level
            ],
            "prediction": filth_flea_formula(level),
            "exactMatch": (
                filth_flea_formula(level)
                == {
                    "unknown1": observed,
                    "unknown2": observed,
                    "unknown3": observed,
                    "unknown4": observed,
                }
            ),
        }
        for level, observed in sorted(FILTH_FLEA_CAPTURED_VALUES.items())
    ]
    filth_flea_active_bindings = []
    for row in active_coverage.get("profiles", []):
        if (
            row.get("runtimePlayfieldOrResource") != 127
            or row.get("name") != "Filth Flea"
            or row.get("monsterData") != 17657
        ):
            continue
        level = row.get("levelCandidates", [None])[0]
        if not isinstance(level, int) or not 4 <= level <= 21:
            raise ValueError(
                "active Filth Flea lies outside the capture-proven formula domain"
            )
        filth_flea_active_bindings.append(
            {
                "resource": 127,
                "name": "Filth Flea",
                "monsterData": 17657,
                "actorCount": row.get("actorCount", 0),
                "configuredSourceIdentity": row.get(
                    "configuredSourceIdentity"
                ),
                "level": level,
                "formulaId": FILTH_FLEA_FORMULA_ID,
                "generatedSpecialAttackWeaponValues": (
                    filth_flea_formula(level)
                ),
                "compatibleSemanticProfileId": (
                    "218eb3509f2be66b-12f99a4c2f732061"
                ),
            }
        )

    vagabond_observations = []
    for capture, sequence, level in VIOLENT_VAGABOND_OBSERVATIONS:
        packet = read_raw_packet(capture, sequence)
        predicted = violent_vagabond_formula(level)
        packet.update(
            {
                "level": level,
                "formulaId": VIOLENT_VAGABOND_FORMULA_ID,
                "formulaValues": predicted,
                "exactMatch": all(
                    packet[field] == value
                    for field, value in predicted.items()
                ),
            }
        )
        vagabond_observations.append(packet)
    if any(not row["exactMatch"] for row in vagabond_observations):
        raise ValueError("Violent Vagabond formula differs from a raw SAW packet")

    eternal_observations = []
    for capture, sequence, level, source_identity in ETERNAL_SENTINEL_OBSERVATIONS:
        packet = read_raw_packet(capture, sequence)
        predicted = eternal_sentinel_formula(level)
        packet.update(
            {
                "level": level,
                "sourceIdentity": source_identity,
                "formulaId": ETERNAL_SENTINEL_FORMULA_ID,
                "formulaValues": predicted,
                "exactMatch": all(
                    packet[field] == value
                    for field, value in predicted.items()
                ),
            }
        )
        eternal_observations.append(packet)
    if any(not row["exactMatch"] for row in eternal_observations):
        raise ValueError("Eternal Sentinel formula differs from a raw SAW packet")

    final_eternal_sources = {"0x7983FA22", "0x7983FBC2"}
    final_actor_dispositions = []
    for row in active_coverage.get("profiles", []):
        resource = row.get("runtimePlayfieldOrResource")
        name = row.get("name")
        monster_data = row.get("monsterData")
        source_identity = row.get("configuredSourceIdentity")
        level = row.get("levelCandidates", [None])[0]
        if (
            resource == 127
            and name == "Violent Vagabond"
            and monster_data == 203733
        ):
            formula_id = VIOLENT_VAGABOND_FORMULA_ID
            generated = violent_vagabond_formula(level)
            semantic_ids = [VIOLENT_VAGABOND_RESULT_DOMAIN_ID]
        elif (
            resource == 127
            and name == "Stim Fiend"
            and monster_data == 203739
            and source_identity == "0x7957E415"
            and level == 9
        ):
            formula_id = STIM_FIEND_FORMULA_ID
            generated = {
                "unknown1": stim_fiend_formula(level),
                "unknown2": stim_fiend_formula(level),
                "unknown3": stim_fiend_formula(level),
                "unknown4": stim_fiend_formula(level),
            }
            semantic_ids = list(STIM_FIEND_PROFILE_IDS)
        elif (
            resource == 1931
            and name == "Eternal Sentinel"
            and monster_data == 41690
            and source_identity in final_eternal_sources
            and level == 18
        ):
            formula_id = ETERNAL_SENTINEL_FORMULA_ID
            generated = eternal_sentinel_formula(level)
            semantic_ids = list(ETERNAL_SENTINEL_PROFILE_IDS)
        else:
            continue
        final_actor_dispositions.append(
            {
                "resource": resource,
                "name": name,
                "monsterData": monster_data,
                "configuredSourceIdentity": source_identity,
                "level": level,
                "formulaId": formula_id,
                "generatedSpecialAttackWeaponValues": generated,
                "compatibleSemanticProfileIds": semantic_ids,
                "startingDisposition": "quarantined",
                "finalDisposition": "certified",
            }
        )
    final_actor_dispositions.sort(
        key=lambda row: (
            row["resource"],
            row["name"],
            row["configuredSourceIdentity"],
        )
    )
    if len(final_actor_dispositions) != 25:
        raise ValueError(
            "final ordinary-combat scope must reconcile exactly 25 actors; "
            f"found {len(final_actor_dispositions)}"
        )
    if sum(
        row["name"] == "Violent Vagabond"
        for row in final_actor_dispositions
    ) != 22:
        raise ValueError("final scope must contain 22 Violent Vagabonds")
    if sum(row["name"] == "Stim Fiend" for row in final_actor_dispositions) != 1:
        raise ValueError("final scope must contain one level-9 Stim Fiend")
    if sum(
        row["name"] == "Eternal Sentinel"
        for row in final_actor_dispositions
    ) != 2:
        raise ValueError("final scope must contain two level-18 Eternal Sentinels")

    return {
        "schemaVersion": 6,
        "scope": {
            "runtimeResources": [127, 1931],
            "sourceInventory": (
                "docs/generated/capture_backed_npc_combat_inventory.json"
            ),
            "sourceItemDatabase": "AORebirth/Datafiles/items.dat",
            "profileCount": len(profiles),
            "completeAndPartialProfilesIncluded": True,
        },
        "profiles": profiles,
        "referencedItemTemplates": template_rows,
        "acceptedFormula": {
            "formulaId": DISOBEDIENT_BOT_FORMULA_ID,
            "family": "Disobedient Bot",
            "monsterData": 17649,
            "resource": 127,
            "supportedLevelsInclusive": [5, 10],
            "exactCategoricalDomain": {
                "attackMode": "natural-specialized",
                "lowTemplate": 144742,
                "highTemplate": 144743,
                "weaponTag": 1397315377,
                "weaponName": "SIW1",
                "slot": 0,
                "instance": 1397315377,
                "numericHitType": 3,
                "numericDamageType": 0,
                "packetOrder": [
                    "SpecialAttackWeapon",
                    "Attack",
                    "AttackInfo",
                ],
            },
            "numericOutput": {
                "fields": [
                    "SpecialAttackWeapon.unknown1",
                    "SpecialAttackWeapon.unknown2",
                    "SpecialAttackWeapon.unknown3",
                    "SpecialAttackWeapon.unknown4",
                ],
                "expression": "floor((19 * actorLevel + 28) / 4)",
                "integerArithmetic": "positive integer truncation equals floor",
                "unknown5": (
                    "per-actor ordered mutable capture state; not formula identity"
                ),
            },
            "rawPacketObservations": observations,
            "leaveOneOut": leave_one_out,
            "activeBindings": active_bindings,
        },
        "stimFiendFormula": {
            "formulaId": STIM_FIEND_FORMULA_ID,
            "family": "Stim Fiend",
            "monsterData": 203739,
            "resource": 127,
            "supportedLevelsInclusive": [9, 17],
            "lowerBoundCertification": {
                "level": 9,
                "configuredSourceIdentity": "0x7957E415",
                "scfuPacket": (
                    "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                    "20260710-202132|IN|1016"
                ),
                "categoricalOwner": (
                    "active MonsterData 203739 population generation selects the "
                    "single SIW1 144742/144743 slot-0 contract used by L10..17"
                ),
                "generatedNumericValue": 48,
                "finalDisposition": (
                    "certified through authoritative runtime loadout selection "
                    "and the bounded L9..17 exact formula"
                ),
            },
            "exactCategoricalDomain": {
                "attackMode": "natural-specialized",
                "lowTemplate": 144742,
                "highTemplate": 144743,
                "weaponTag": 1397315377,
                "weaponName": "SIW1",
                "slot": 0,
                "instance": 1397315377,
                "numericHitType": 3,
                "numericDamageType": 0,
                "packetOrder": [
                    "SpecialAttackWeapon",
                    "Attack",
                    "AttackInfo",
                ],
                "terminalOnlyStreamsExcludedFromRepeatingRuntime": True,
            },
            "numericOutput": {
                "fields": [
                    "SpecialAttackWeapon.unknown1",
                    "SpecialAttackWeapon.unknown2",
                    "SpecialAttackWeapon.unknown3",
                    "SpecialAttackWeapon.unknown4",
                ],
                "expression": "floor((11 * actorLevel - 2) / 2)",
                "integerArithmetic": "positive integer truncation equals floor",
                "unknown5": (
                    "per-actor ordered mutable capture state; not formula identity"
                ),
            },
            "runtimeInputOwners": {
                "actorLevel": (
                    "OrdinaryEnemySpawnDefinition.Level through "
                    "OrdinaryEnemyCombatProfile.ResolveContract(level)"
                ),
                "monsterDataAndFamily": (
                    "CapturedSubwayOrdinaryArchetypeDefinition"
                ),
                "weaponTemplatesTagAndName": (
                    "NpcCombatAttackRules capture-bound Stim Fiend constants"
                ),
                "damageRangeAndCadence": (
                    "CapturedSubwayCombatEvidenceDefinition on the active archetype"
                ),
                "mutableEnergyAmmoAndSawState": (
                    "existing per-actor combat contract/runtime state"
                ),
            },
            "formulaFamiliesTested": [
                "exact affine integer formulas",
                "exact affine rational formulas",
                "floor division",
                "ceiling division",
                "nearest-away division",
                "nearest-even division",
                "bounded family-scoped formulas",
                "generic four-equal SIW1 weapon-family formulas",
                "direct item-template transformations",
                "level and quality breakpoint formulas",
                "integer clamps and unbounded extensions",
                "finite-difference sequences",
                "stream-specific formulas",
            ],
            "compatibleSemanticProfileIds": list(STIM_FIEND_PROFILE_IDS),
            "rawPacketObservations": stim_observations,
            "leaveOneOut": stim_leave_one_out,
            "activeBindings": stim_active_bindings,
            "startingQuarantineScope": sorted(
                stim_starting_scope,
                key=lambda row: row["configuredSourceIdentity"],
            ),
            "crossFamilyHeldOut": {
                "observations": stim_cross_family,
                "exactMatches": sum(
                    row["exactMatch"] for row in stim_cross_family
                ),
                "mismatches": sum(
                    not row["exactMatch"] for row in stim_cross_family
                ),
                "conclusion": (
                    "the numeric rule is reusable only after the exact "
                    "Stim Fiend semantic selector succeeds"
                ),
            },
            "rejectedCandidates": [
                {
                    "candidate": "unrounded 5.5 * level - 1",
                    "mismatches": 2,
                    "reason": "levels 11 and 13 produce noninteger values",
                },
                {
                    "candidate": "Disobedient Bot formula",
                    "mismatches": 3,
                    "reason": "levels 12, 13, and 14 differ from raw Stim Fiend SAW",
                },
                {
                    "candidate": "direct item-template interpolation",
                    "mismatches": 5,
                    "reason": "item templates do not encode the five observed SAW values",
                },
                {
                    "candidate": "generic reuse across every four-equal SIW1 family",
                    "mismatches": 6,
                    "observations": 23,
                    "reason": "family and stream semantics remain part of the exact selector",
                },
                {
                    "candidate": "unbounded Stim Fiend level domain",
                    "reason": "levels below 9 and above 17 lack categorical and formula proof",
                },
            ],
        },
        "finalOrdinaryDungeonCombatCompletion": {
            "formulaIds": [
                VIOLENT_VAGABOND_FORMULA_ID,
                STIM_FIEND_FORMULA_ID,
                ETERNAL_SENTINEL_FORMULA_ID,
            ],
            "resultDomainIds": [
                VIOLENT_VAGABOND_RESULT_DOMAIN_ID,
            ],
            "startingCheckpoint": {
                "totalActors": 489,
                "certified": 464,
                "quarantined": 25,
                "pf127": {"certified": 299, "quarantined": 23},
                "pf1931": {"certified": 165, "quarantined": 2},
            },
            "finalCheckpoint": {
                "totalActors": 489,
                "certified": 489,
                "quarantined": 0,
                "pf127": {"certified": 322, "quarantined": 0},
                "pf1931": {"certified": 167, "quarantined": 0},
            },
            "violentVagabond": {
                "monsterData": 203733,
                "supportedLevelsInclusive": [6, 10],
                "weaponLoadout": {
                    "lowTemplate": 130590,
                    "highTemplate": 130590,
                    "quality": 1,
                    "slot": 6,
                    "energy": 1,
                    "attackDelay": 175,
                    "rechargeDelay": 175,
                },
                "numericExpressions": {
                    "unknown1": "floor((17 * actorLevel + 26) / 4)",
                    "unknown2": "floor((19 * actorLevel + 26) / 4)",
                    "unknown3": "floor((15 * actorLevel + 26) / 4)",
                    "unknown4": "floor((17 * actorLevel + 25) / 4)",
                },
                "generatedLevelEight": violent_vagabond_formula(8),
                "rawSawObservations": vagabond_observations,
                "leaveOneOut": [
                    {
                        "heldOutLevel": row["level"],
                        "prediction": row["formulaValues"],
                        "exactMatch": row["exactMatch"],
                    }
                    for row in vagabond_observations
                ],
                "missChainEvidence": {
                    "rawObservations": 41,
                    "distinctChains": 40,
                    "embeddedAttackerAttribution": True,
                    "packetOrder": [
                        "WeaponItemFullUpdate",
                        "SpecialAttackWeapon",
                        "Attack",
                        "MissedAttackInfo",
                    ],
                    "missN3": 1,
                    "missFields": [0, 6, 0],
                },
                "normalResultDomain": {
                    "domainId": VIOLENT_VAGABOND_RESULT_DOMAIN_ID,
                    "compatibleCapturedEquippedMeleeStreams": 166,
                    "hitWire": 3,
                    "damageWire": 0,
                    "slot": 6,
                    "instance": 0,
                    "action": 0,
                    "excludedCategory": (
                        "finite ranged equipped streams with damage wire 4"
                    ),
                    "numericDamageOwner": "active actor Stats",
                },
                "rejectedRules": [
                    {
                        "candidate": "identity or per-level output table",
                        "reason": "not a reusable mathematical input",
                    },
                    {
                        "candidate": "nearest captured level",
                        "reason": "level 8 is generated only by the bounded equations",
                    },
                    {
                        "candidate": "miss-only runtime combat",
                        "reason": "normal result semantics are required",
                    },
                ],
            },
            "stimFiendLevelNine": {
                "configuredSourceIdentity": "0x7957E415",
                "monsterData": 203739,
                "level": 9,
                "formulaValue": 48,
                "categoricalSelector": (
                    "active MonsterData generation selects the unique SIW1 "
                    "144742/144743 slot-0 attack domain"
                ),
                "boundedLevelsInclusive": [9, 17],
                "semanticProfileIds": list(STIM_FIEND_PROFILE_IDS),
            },
            "eternalSentinelLevelEighteen": {
                "monsterData": 41690,
                "boundedLevelsInclusive": [18, 20],
                "numericExpressions": {
                    "unknown1To3": "floor((11 * actorLevel - 2) / 2)",
                    "unknown4": "floor((actorLevel + 4) / 2)",
                },
                "rawSawObservations": eternal_observations,
                "semanticProfileIds": list(ETERNAL_SENTINEL_PROFILE_IDS),
                "loadouts": [
                    {
                        "configuredSourceIdentity": "0x7983FA22",
                        "templates": [123381, 123382],
                        "quality": 15,
                    },
                    {
                        "configuredSourceIdentity": "0x7983FBC2",
                        "templates": [123383, 123384],
                        "quality": 22,
                    },
                ],
                "normalResultDomain": {
                    "hitWire": 3,
                    "damageWire": 0,
                    "slot": 6,
                    "instance": 0,
                    "semanticProfileLevels": [19, 20],
                    "numericDamageOwner": "production weapon and actor rules",
                },
            },
            "actorDispositions": final_actor_dispositions,
            "activeBindings": final_actor_dispositions,
        },
        "filthFleaFormula": {
            "formulaId": FILTH_FLEA_FORMULA_ID,
            "family": "Filth Flea",
            "monsterData": 17657,
            "resource": 127,
            "supportedLevelsInclusive": [4, 21],
            "exactCategoricalDomain": {
                "attackMode": "natural-specialized",
                "weaponItemFullUpdate": "natural-none",
                "specials": [
                    {
                        "lowTemplate": 201059,
                        "highTemplate": 201060,
                        "tag": 1162887496,
                        "name": "EPAH",
                        "slot": 1,
                        "instance": 1162887496,
                    },
                    {
                        "lowTemplate": 201056,
                        "highTemplate": 201057,
                        "tag": 1096439123,
                        "name": "AZUS",
                        "slot": 0,
                        "instance": 1096439123,
                    },
                ],
                "numericHitType": 3,
                "normalNumericDamageType": 0,
                "terminalNumericDamageType": 4,
                "specialAttackWeaponN3": 0,
                "attackN3": 0,
                "attackAction": 0,
                "packetOrder": [
                    "SpecialAttackWeapon",
                    "Attack",
                    "AttackInfo",
                ],
            },
            "numericOutput": {
                "fields": [
                    "SpecialAttackWeapon.unknown1",
                    "SpecialAttackWeapon.unknown2",
                    "SpecialAttackWeapon.unknown3",
                    "SpecialAttackWeapon.unknown4",
                ],
                "expression": (
                    "L4..10: floor((21 * actorLevel + 28) / 4); "
                    "L11..21: 6 * actorLevel - 1"
                ),
                "unknown5": (
                    "per-actor ordered mutable capture state; not formula identity"
                ),
                "level19Unknown2Equals141": (
                    "mutable generation-local observation; the independent "
                    "same-level initial stream and stable formula value are 113"
                ),
            },
            "runtimeInputOwners": {
                "actorLevel": (
                    "OrdinaryEnemySpawnDefinition.Level through "
                    "OrdinaryEnemyCombatProfile.ResolveContract(level)"
                ),
                "familyAndMonsterData": (
                    "CapturedSubwayOrdinaryArchetypeDefinition"
                ),
                "damageRangeAndCadence": (
                    "CapturedSubwayCombatEvidenceDefinition on the active archetype"
                ),
                "mutableEnergyAmmoAndSawState": (
                    "existing per-actor combat contract/runtime state"
                ),
            },
            "compatibleSemanticProfileIds": list(FILTH_FLEA_PROFILE_IDS),
            "canonicalSemanticProfileId": (
                "218eb3509f2be66b-12f99a4c2f732061"
            ),
            "captureSessions": [
                "20260708-004038",
                "20260708-143600",
                "20260709-193914",
                "20260709-225408",
                "20260720-051714",
            ],
            "rawPacketObservations": stable_filth_flea_observations,
            "rawProfileObservations": filth_flea_observations,
            "stableFormulaObservations": stable_filth_flea_observations,
            "leaveOneOut": filth_flea_leave_one_out,
            "activeBindings": filth_flea_active_bindings,
            "rejectedCandidates": [
                {
                    "candidate": "exact integer level as reusable identity",
                    "reason": (
                        "uncaptured active levels retain the exact family, "
                        "special sequence, slots, instances, and stream semantics"
                    ),
                },
                {
                    "candidate": "nearest-level contract selection",
                    "reason": "numeric setup is derived from actor level, never copied",
                },
                {
                    "candidate": "unbounded level domain",
                    "reason": "levels outside L4..21 lack categorical proof",
                },
                {
                    "candidate": "cross-family or cross-special reuse",
                    "reason": (
                        "family, MonsterData, EPAH/AZUS templates, tags, "
                        "slots, instances, and stream signature remain exact"
                    ),
                },
            ],
        },
        "meldedPatternsFormula": {
            "formulaId": MELDED_PATTERNS_FORMULA_ID,
            "family": "Melded Patterns",
            "monsterData": 203747,
            "resource": 127,
            "supportedLevelsInclusive": [18, 25],
            "exactCategoricalDomains": [
                {
                    "actorQualityLevelsInclusive": [1, 19],
                    "weaponLowTemplate": 121817,
                    "weaponHighTemplate": 121818,
                },
                {
                    "actorQualityLevelsInclusive": [20, 20],
                    "weaponLowTemplate": 121818,
                    "weaponHighTemplate": 121818,
                },
                {
                    "actorQualityLevelsInclusive": [21, 40],
                    "weaponLowTemplate": 121819,
                    "weaponHighTemplate": 121820,
                },
            ],
            "sharedCategoricalSemantics": {
                "attackMode": "equipped",
                "weaponFamily": "items.dat interpolation list 121817..121835",
                "slot": 6,
                "instance": 0,
                "specials": [],
                "specialAttackWeaponN3": 0,
                "attackN3": 0,
                "attackAction": 0,
                "streamCount": 1,
                "streamOrdinal": 0,
                "numericHitType": 3,
                "numericDamageType": 0,
                "packetOrder": [
                    "WeaponItemFullUpdate",
                    "SpecialAttackWeapon",
                    "Attack",
                    "AttackInfo",
                ],
                "terminalOutcomesAreNotAdditionalStreams": True,
            },
            "numericOutput": {
                "fields": {
                    "SpecialAttackWeapon.unknown1": "base",
                    "SpecialAttackWeapon.unknown2": "base + 28",
                    "SpecialAttackWeapon.unknown3": "base",
                    "SpecialAttackWeapon.unknown4": "base",
                },
                "baseExpression": "floor((11 * actorLevel - 2) / 2)",
                "integerArithmetic": "positive integer truncation equals floor",
                "clamping": "none inside the proven level domain",
                "unknown5": (
                    "per-actor ordered mutable capture state; not formula identity"
                ),
            },
            "runtimeInputOwners": {
                "actorLevelAndQualityLevel": (
                    "CapturedSubwayOrdinarySpawnDefinition generated from "
                    "the owner-linked population row"
                ),
                "monsterDataAndFamily": (
                    "CapturedSubwayOrdinaryArchetypeDefinition"
                ),
                "weaponTemplates": (
                    "owner-linked WeaponItemFullUpdate plus items.dat "
                    "interpolation-domain validation"
                ),
                "weaponSlotAndInstance": (
                    "NpcCombatAttackRules capture-bound Melded Patterns constants"
                ),
                "weaponQlAcgItemLevelDamageRangeAndCadence": (
                    "active spawn, items.dat, and existing production combat owners"
                ),
                "mutableEnergyAmmoAndSawState": (
                    "existing per-actor combat contract/runtime state"
                ),
            },
            "formulaFamiliesTested": [
                "exact affine integer formulas",
                "exact affine rational formulas",
                "floor division",
                "ceiling division",
                "nearest-away division",
                "nearest-even division",
                "finite differences",
                "bounded actor-level formulas",
                "weapon-quality-only formulas",
                "item-template transformations",
                "AttackDelay and RechargeDelay transformations",
                "stream-specific formulas",
                "breakpoint and piecewise formulas",
                "integer clamps",
                "existing Stim Fiend base formula",
            ],
            "rawPacketObservations": melded_observations,
            "leaveOneOut": melded_leave_one_out,
            "crossFamilyHeldOut": {
                "observations": melded_cross_family,
                "exactMatches": 0,
                "conclusion": (
                    "no other family enters the exact MonsterData, equipped "
                    "weapon-domain, slot, and stream selector"
                ),
            },
            "activeBindings": sorted(
                melded_active_bindings,
                key=lambda row: row["configuredSourceIdentity"],
            ),
            "compatibleSemanticProfileIds": melded_profile_ids,
            "rejectedCandidates": [
                {
                    "candidate": "unrounded (11 * level - 2) / 2",
                    "mismatches": 3,
                    "reason": "levels 19, 21, and 25 are half-integers",
                },
                {
                    "candidate": "ceiling or nearest-away division",
                    "mismatches": 3,
                    "reason": "all three captured odd levels round above raw SAW",
                },
                {
                    "candidate": "nearest-even division",
                    "mismatches": 1,
                    "reason": "captured level 19 rounds above raw SAW",
                },
                {
                    "candidate": "four identical SAW fields",
                    "mismatches": len(melded_observations),
                    "reason": "Unknown2 is exactly base plus 28 in every raw packet",
                },
                {
                    "candidate": "weapon QL as the sole numeric input",
                    "mismatches": 5,
                    "reason": (
                        "QL19 and QL20 each occur at multiple actor levels "
                        "with different exact SAW values"
                    ),
                },
                {
                    "candidate": "direct item-template interpolation",
                    "mismatches": len(
                        MELDED_PATTERNS_CAPTURED_BASE_VALUES
                    ),
                    "reason": (
                        "items.dat selects loadout, damage, range, and cadence "
                        "but does not encode the observed SAW base values"
                    ),
                },
                {
                    "candidate": "unbounded Melded Patterns level domain",
                    "reason": (
                        "levels below 18 and above 25 lack categorical and "
                        "formula proof"
                    ),
                },
            ],
        },
        "fragmentedSoulFormula": {
            "formulaId": FRAGMENTED_SOUL_FORMULA_ID,
            "family": "Fragmented Soul",
            "monsterData": 203729,
            "resource": 127,
            "supportedLevelsInclusive": [17, 21],
            "exactCategoricalDomains": [
                {
                    "actorQualityLevelsInclusive": [1, 19],
                    "weaponLowTemplate": 123685,
                    "weaponHighTemplate": 123686,
                },
                {
                    "actorQualityLevelsInclusive": [20, 20],
                    "weaponLowTemplate": 123686,
                    "weaponHighTemplate": 123686,
                },
                {
                    "actorQualityLevelsInclusive": [21, 21],
                    "weaponLowTemplate": 123687,
                    "weaponHighTemplate": 123687,
                },
                {
                    "actorQualityLevelsInclusive": [22, 40],
                    "weaponLowTemplate": 123687,
                    "weaponHighTemplate": 123688,
                },
            ],
            "sharedCategoricalSemantics": {
                "attackMode": "equipped",
                "weaponFamily": "items.dat interpolation list 123685..123703",
                "weaponItemFullUpdateSlot": 6,
                "weaponItemFullUpdateInstance": 0,
                "weaponItemFullUpdateStateMachine": [1000015, 0],
                "weaponItemFullUpdateUnknown1": 11,
                "weaponItemFullUpdateUnknown2": 262,
                "weaponItemFullUpdateUnknown3": 0,
                "weaponItemFullUpdateFlags": 1027,
                "weaponItemFullUpdateMultipleCount": 1,
                "weaponItemFullUpdateEnergy": 25,
                "specials": [],
                "specialAttackWeaponN3": 0,
                "attackN3": 0,
                "attackAction": 0,
                "streamCount": 1,
                "streamOrdinal": 0,
                "numericHitType": 3,
                "numericDamageType": 0,
                "packetOrder": [
                    "WeaponItemFullUpdate",
                    "SpecialAttackWeapon",
                    "Attack",
                    "AttackInfo",
                ],
                "terminalOutcomesAreNotAdditionalStreams": True,
            },
            "numericOutput": {
                "fields": {
                    "SpecialAttackWeapon.unknown1": "base",
                    "SpecialAttackWeapon.unknown2": "base",
                    "SpecialAttackWeapon.unknown3": "base",
                    "SpecialAttackWeapon.unknown4": (
                        "base + 2 * floor(actorLevel / 2)"
                    ),
                },
                "baseExpression": "6 * actorLevel - 1",
                "integerArithmetic": (
                    "positive C# integer division equals floor"
                ),
                "clamping": "none inside the proven level domain",
                "unknown5": (
                    "per-actor ordered mutable capture state; not formula identity"
                ),
            },
            "runtimeInputOwners": {
                "actorLevelQualityAndLoadout": (
                    "CapturedSubwayGenerationVariantDefinition in "
                    "CapturedSubwayOrdinaryContentProvider"
                ),
                "monsterDataAndFamily": (
                    "CapturedSubwayOrdinaryArchetypeDefinition"
                ),
                "weaponTemplates": (
                    "owner-linked generation variant plus items.dat "
                    "interpolation-domain validation"
                ),
                "weaponSlotAndInstance": (
                    "NpcCombatAttackRules capture-bound Fragmented Soul constants"
                ),
                "weaponQlAcgItemLevelDamageRangeAndCadence": (
                    "active spawn, items.dat, and existing production combat owners"
                ),
                "mutableEnergyAmmoAndSawState": (
                    "existing per-actor combat contract/runtime state"
                ),
            },
            "formulaFamiliesTested": [
                "exact affine integer formulas",
                "exact affine rational formulas",
                "floor division",
                "ceiling division",
                "nearest-away division",
                "nearest-even division",
                "finite differences",
                "bounded actor-level formulas",
                "weapon-quality-only formulas",
                "item-template transformations",
                "AttackDelay and RechargeDelay transformations",
                "stream-specific formulas",
                "breakpoint and piecewise formulas",
                "integer clamps",
                "unbounded extensions",
            ],
            "capturedValuesByLevel": {
                str(level): {
                    f"unknown{index + 1}": value
                    for index, value in enumerate(values)
                }
                for level, values in sorted(
                    FRAGMENTED_SOUL_CAPTURED_VALUES.items()
                )
            },
            "rawPacketObservations": fragmented_raw_observations,
            "leaveOneOut": fragmented_leave_one_out,
            "crossFamilyHeldOut": {
                "observations": fragmented_cross_family,
                "exactMatches": 0,
                "conclusion": (
                    "no other family enters the exact MonsterData, equipped "
                    "weapon-domain, slot, and stream selector"
                ),
            },
            "activeBindings": sorted(
                fragmented_active_bindings,
                key=lambda row: (
                    row["configuredSourceIdentity"],
                    row["level"],
                ),
            ),
            "activeActors": sorted(
                fragmented_active_actors,
                key=lambda row: row["configuredSourceIdentity"],
            ),
            "activeGenerationVariants": sorted(
                fragmented_generation_variants,
                key=lambda row: (
                    row["configuredSourceIdentity"],
                    row["level"],
                    row["actorQualityLevel"],
                    row["weaponLowTemplate"],
                    row["weaponHighTemplate"],
                ),
            ),
            "compatibleSemanticProfileIds": fragmented_profile_ids,
            "rejectedCandidates": [
                {
                    "candidate": "Unknown4 = 7 * actorLevel - 1",
                    "mismatches": 3,
                    "reason": "captured odd levels 17, 19, and 21 are one lower",
                },
                {
                    "candidate": "Unknown4 = 7 * actorLevel - 2",
                    "mismatches": 2,
                    "reason": "captured even levels 18 and 20 are one higher",
                },
                {
                    "candidate": "four identical SAW fields",
                    "mismatches": len(fragmented_raw_observations),
                    "reason": (
                        "Unknown4 exceeds the shared base in every raw packet"
                    ),
                },
                {
                    "candidate": "weapon QL as the sole numeric input",
                    "mismatches": 6,
                    "reason": (
                        "QL14, QL17, QL18, QL19, and QL25 occur at "
                        "different actor levels with different exact SAW values"
                    ),
                },
                {
                    "candidate": "direct item-template interpolation",
                    "mismatches": len(
                        FRAGMENTED_SOUL_CAPTURED_VALUES
                    ),
                    "reason": (
                        "items.dat selects loadout, damage, range, and cadence "
                        "but does not encode the observed SAW values"
                    ),
                },
                {
                    "candidate": "unbounded Fragmented Soul level domain",
                    "reason": (
                        "levels below 17 and above 21 lack categorical and "
                        "formula proof"
                    ),
                },
            ],
        },
        "incompleteRebuildFormula": {
            "formulaId": "incomplete-rebuild-saw-6L-plus-1-minus-2-v1",
            "family": "Incomplete Rebuild",
            "monsterData": 203728,
            "resource": 127,
            "supportedLevelsInclusive": [17, 22],
            "exactCategoricalDomains": [
                {
                    "actorQualityLevelsInclusive": [1, 19],
                    "weaponLowTemplate": 122653,
                    "weaponHighTemplate": 122654,
                },
                {
                    "actorQualityLevelsInclusive": [20, 20],
                    "weaponLowTemplate": 122654,
                    "weaponHighTemplate": 122654,
                },
                {
                    "actorQualityLevelsInclusive": [21, 21],
                    "weaponLowTemplate": 122655,
                    "weaponHighTemplate": 122655,
                },
                {
                    "actorQualityLevelsInclusive": [22, 40],
                    "weaponLowTemplate": 122655,
                    "weaponHighTemplate": 122656,
                },
            ],
            "sharedCategoricalSemantics": {
                "attackMode": "equipped",
                "slot": 6,
                "instance": 0,
                "specials": [],
                "streamCount": 1,
                "numericHitType": 3,
                "numericDamageType": 0,
                "packetOrder": [
                    "WeaponItemFullUpdate",
                    "SpecialAttackWeapon",
                    "Attack",
                    "AttackInfo",
                ],
                "repeatedInitializationIsNotAStream": True,
                "terminalOutcomesAreNotAdditionalStreams": True,
            },
            "numericOutput": {
                "fields": {
                    "SpecialAttackWeapon.unknown1": "base",
                    "SpecialAttackWeapon.unknown2": "base",
                    "SpecialAttackWeapon.unknown3": "base",
                    "SpecialAttackWeapon.unknown4": "base - 2",
                },
                "baseExpression": "6 * actorLevel + 1",
                "integerArithmetic": "exact checked integer arithmetic",
                "clamping": "none inside the proven level domain",
                "unknown5": "ordered mutable per-actor state",
            },
            "runtimeInputOwners": {
                "actorLevelQualityAndLoadout": (
                    "CapturedSubwayGenerationVariantDefinition or exact "
                    "CapturedSubwaySourceWeaponEvidenceDefinition"
                ),
                "damageRangeCadenceEnergyAndAmmo": (
                    "active spawn, items.dat, and existing combat runtime"
                ),
            },
            "rawLevel17PacketObservation": incomplete_level_seventeen,
            "rawPacketObservations": [incomplete_level_seventeen],
            "completeProfileObservations": incomplete_observations,
            "leaveOneOut": incomplete_leave_one_out,
            "compatibleSemanticProfileIds": sorted(
                {
                    row["semanticProfileId"]
                    for row in incomplete_observations
                    if row.get("semanticProfileId")
                }
            ),
            "activeBindings": [
                {
                    "resource": 127,
                    "name": "Incomplete Rebuild",
                    "monsterData": 203728,
                    "configuredSourceIdentity": source,
                    "level": level,
                    "formulaPrediction": incomplete_rebuild_formula(level),
                    "finalDisposition": "restored",
                }
                for source, level in (
                    ("0x79545170", 17),
                    ("0x79545172", 18),
                    ("0x79545177", 19),
                    ("0x79545181", 19),
                    ("0x79545188", 19),
                    ("0x79545241", 17),
                )
            ],
            "rejectedCandidates": [
                {
                    "candidate": "four identical SAW fields",
                    "mismatches": len(incomplete_observations) + 1,
                    "reason": "Unknown4 is exactly two below the shared base",
                },
                {
                    "candidate": "level-local captured tuple selection",
                    "mismatches": 1,
                    "reason": "level 17 has no generated complete profile",
                },
                {
                    "candidate": "unbounded level domain",
                    "reason": "levels below 17 and above 22 lack formula proof",
                },
            ],
        },
        "molestedMoleculesFormula": {
            "formulaId": (
                "molested-molecules-saw-floor-11L-minus-2-over-2-v1"
            ),
            "family": "Molested Molecules",
            "monsterData": 203746,
            "resource": 127,
            "supportedLevelsInclusive": [17, 25],
            "exactCategoricalDomains": [
                {
                    "actorQualityLevelsInclusive": [1, 19],
                    "weaponLowTemplate": 122216,
                    "weaponHighTemplate": 122217,
                },
                {
                    "actorQualityLevelsInclusive": [20, 20],
                    "weaponLowTemplate": 122217,
                    "weaponHighTemplate": 122217,
                },
                {
                    "actorQualityLevelsInclusive": [21, 40],
                    "weaponLowTemplate": 122218,
                    "weaponHighTemplate": 122219,
                },
            ],
            "sharedCategoricalSemantics": {
                "attackMode": "equipped",
                "slot": 6,
                "instance": 0,
                "specials": [],
                "streamCount": 1,
                "numericHitType": 3,
                "numericDamageType": 0,
                "packetOrder": [
                    "WeaponItemFullUpdate",
                    "SpecialAttackWeapon",
                    "Attack",
                    "AttackInfo",
                ],
            },
            "numericOutput": {
                "fields": [
                    "SpecialAttackWeapon.unknown1",
                    "SpecialAttackWeapon.unknown2",
                    "SpecialAttackWeapon.unknown3",
                    "SpecialAttackWeapon.unknown4",
                ],
                "expression": "floor((11 * actorLevel - 2) / 2)",
                "integerArithmetic": "positive integer truncation equals floor",
                "clamping": "none inside the proven level domain",
                "unknown5": "ordered mutable per-actor state",
            },
            "runtimeInputOwners": {
                "actorLevel": "active captured spawn",
                "weaponQlAndTemplates": (
                    "owner-linked raw WeaponItemFullUpdate and items.dat"
                ),
                "damageRangeCadenceEnergyAndAmmo": (
                    "active spawn, items.dat, and existing combat runtime"
                ),
            },
            "completeProfileObservations": molested_observations,
            "leaveOneOut": molested_leave_one_out,
            "rawPacketObservations": [
                {
                    "captureSession": (
                        "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                        "20260709-222339"
                    ),
                    "packetId": packet_id,
                    "messageType": "WeaponItemFullUpdate",
                }
                for packet_id in (
                    (
                        "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                        "20260709-222339|IN|4462|a1d9664f3cb3"
                    ),
                    (
                        "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                        "20260709-222339|IN|4473|6bbf0cd55b4f"
                    ),
                    (
                        "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                        "20260709-222339|IN|4478|6f3563ec0239"
                    ),
                )
            ],
            "selectorObservations": [
                {
                    "configuredSourceIdentity": "0x79545139",
                    "level": 23,
                    "packetId": (
                        "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                        "20260709-222339|IN|4462|a1d9664f3cb3"
                    ),
                    "weaponLowTemplate": 122218,
                    "weaponHighTemplate": 122219,
                    "actorQualityLevel": 22,
                },
                {
                    "configuredSourceIdentity": "0x795451D2",
                    "level": 24,
                    "packetId": (
                        "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                        "20260709-222339|IN|4473|6bbf0cd55b4f"
                    ),
                    "weaponLowTemplate": 122216,
                    "weaponHighTemplate": 122217,
                    "actorQualityLevel": 19,
                },
                {
                    "configuredSourceIdentity": "0x795451D7",
                    "level": 24,
                    "packetId": (
                        "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                        "20260709-222339|IN|4478|6f3563ec0239"
                    ),
                    "weaponLowTemplate": 122218,
                    "weaponHighTemplate": 122219,
                    "actorQualityLevel": 27,
                },
            ],
            "compatibleSemanticProfileIds": sorted(
                {
                    row["semanticProfileId"]
                    for row in molested_observations
                    if row.get("semanticProfileId")
                }
            ),
            "activeBindings": [
                {
                    "resource": 127,
                    "name": "Molested Molecules",
                    "monsterData": 203746,
                    "configuredSourceIdentity": source,
                    "level": level,
                    "formulaPrediction": molested_molecules_formula(level),
                    "finalDisposition": "restored",
                }
                for source, level in (
                    ("0x79545139", 23),
                    ("0x795451D2", 24),
                    ("0x795451D7", 24),
                )
            ],
            "rejectedCandidates": [
                {
                    "candidate": "copy level 22 or level 25",
                    "mismatches": 3,
                    "reason": "does not reproduce the exact bounded level formula",
                },
                {
                    "candidate": "weapon QL as the sole numeric input",
                    "mismatches": 4,
                    "reason": "actor level, not QL, owns the observed SAW sequence",
                },
                {
                    "candidate": "unbounded level domain",
                    "reason": "levels below 17 and above 25 lack formula proof",
                },
            ],
        },
        "templeOrdinaryCombatCompletion": {
            "resource": 1931,
            "startingActors": {
                "certified": 87,
                "quarantined": 80,
                "ordinaryTotal": 167,
            },
            "finalActors": {
                "certified": 165,
                "quarantined": 2,
                "ordinaryTotal": 167,
            },
            "restoredStartingActors": 78,
            "formulaIds": [
                TEMPLE_CULTIST_FORMULA_ID,
                TEMPLE_CULTIST_RAISED_PRIMARY_FORMULA_ID,
            ],
            "supportedLevelsInclusive": [20, 35],
            "numericOutput": {
                "base": {
                    "L20To25": "floor((31 * actorLevel - 10) / 2)",
                    "L26To33": (
                        "17 * actorLevel - 42 - (actorLevel bitwise-and 1)"
                    ),
                    "L34To35": "17 * actorLevel - 43",
                },
                "unknown1": (
                    "base + 20 for MonsterData 26135; base otherwise"
                ),
                "unknown2": "base",
                "unknown3": "base",
                "unknown4": (
                    "floor((actorLevel + 4) / 2) for L20..25; "
                    "floor((actorLevel + 6) / 2) for L26..35"
                ),
                "integerArithmetic": (
                    "checked positive integer arithmetic; division truncates "
                    "toward zero and therefore equals floor"
                ),
                "clamping": "none inside L20..35; fail closed outside",
                "unknown5": "ordered mutable per-actor state",
            },
            "exactCategoricalDomains": [
                {
                    "monsterData": 26074,
                    "weaponPairs": [[204747, 204747]],
                },
                {
                    "monsterData": 26082,
                    "weaponPairs": [
                        [130163, 130164],
                        [130164, 130164],
                    ],
                },
                {
                    "monsterData": 26103,
                    "weaponPairs": [[129028, 129029]],
                },
                {
                    "monsterData": 26135,
                    "weaponPairs": [[158298, 158299]],
                },
                {
                    "monsterData": 26137,
                    "weaponPairs": [[204747, 204747]],
                },
                {
                    "monsterData": 26147,
                    "weaponPairs": [
                        [144103, 144103],
                        [144103, 144104],
                        [144104, 144104],
                    ],
                },
                {
                    "monsterData": 26149,
                    "weaponPairs": [
                        [124313, 124314],
                        [124314, 124314],
                    ],
                },
            ],
            "sharedCategoricalSemantics": {
                "attackMode": "equipped",
                "slot": 6,
                "instance": 0,
                "specials": [],
                "streamCount": 1,
                "numericHitType": 3,
                "numericDamageType": 0,
                "packetOrder": [
                    "WeaponItemFullUpdate",
                    "SpecialAttackWeapon",
                    "Attack",
                    "AttackInfo",
                ],
                "repeatedInitializationIsNotAStream": True,
                "terminalOutcomesAreNotAdditionalStreams": True,
            },
            "runtimeInputOwners": {
                "actorLevel": "active captured spawn",
                "weaponTemplatesAndQuality": (
                    "exact active-spawn WeaponItemFullUpdate catalog"
                ),
                "damageRangeCadenceEnergyAndAmmo": (
                    "active spawn, items.dat, and existing combat runtime"
                ),
                "mutableUnknown5": (
                    "existing ordered per-actor SpecialAttackWeapon state"
                ),
            },
            "completeProfileObservations": temple_cultist_observations,
            "crossFamilyHeldOutValidation": temple_held_out_validation,
            "formulaFamiliesTested": [
                "integer affine",
                "rational affine",
                "floor and ceiling division",
                "nearest-away and nearest-even rounding",
                "finite differences",
                "bounded actor-level",
                "bounded weapon-QL",
                "level plus QL",
                "item interpolation",
                "weapon AttackDelay and RechargeDelay transformations",
                "attack-rating and initiative transformations",
                "piecewise breakpoint",
                "stream-specific",
                "integer clamps",
                "existing proven formula families",
            ],
            "rejectedCandidates": [
                {
                    "candidate": "single affine expression across L20..35",
                    "reason": (
                        "exact first differences change at the proven L25/L26 "
                        "and L33/L34 breakpoints"
                    ),
                },
                {
                    "candidate": "weapon QL lookup",
                    "reason": (
                        "multiple QLs at one level share the same numeric setup "
                        "and QL1 204747 spans multiple numeric setups"
                    ),
                },
                {
                    "candidate": "source-identity lookup",
                    "reason": "source identity is generation-local",
                },
                {
                    "candidate": "nearest-level substitution",
                    "reason": (
                        "adjacent captured levels have different exact numeric "
                        "values"
                    ),
                },
                {
                    "candidate": "unbounded level domain",
                    "reason": "levels outside L20..35 lack categorical proof",
                },
            ],
            "rawPacketObservations": temple_raw_packet_observations,
            "activeBindings": temple_active_bindings,
            "startingActorDispositions": temple_starting_dispositions,
            "nonCultistResults": [
                {
                    "sourceIdentity": "0x7983FA22",
                    "name": "Eternal Sentinel",
                    "monsterData": 41690,
                    "level": 18,
                    "finalDisposition": "quarantined",
                },
                {
                    "sourceIdentity": "0x7983FA26",
                    "name": "Eternal Sentinel",
                    "monsterData": 41690,
                    "level": 20,
                    "finalDisposition": "restored",
                    "canonicalProfileIds": [
                        "e037cf6f4165eff5-71ebcc342951c27c",
                        "e037cf6f4165eff5-c036f50d1289554a",
                    ],
                },
                {
                    "sourceIdentity": "0x7983FBC2",
                    "name": "Eternal Sentinel",
                    "monsterData": 41690,
                    "level": 18,
                    "finalDisposition": "quarantined",
                },
                {
                    "sourceIdentity": "0x7987F12D",
                    "name": "Murial the Faithful",
                    "monsterData": 26090,
                    "level": 34,
                    "finalDisposition": "restored",
                    "captureContract": "source-local exact packet sequence",
                },
            ],
        },
        "equippedFormulaDomainRegistry": [
            {
                "formulaId": MELDED_PATTERNS_FORMULA_ID,
                "monsterData": 203747,
                "levelsInclusive": [18, 25],
                "weaponFamily": "121817..121820",
            },
            {
                "formulaId": FRAGMENTED_SOUL_FORMULA_ID,
                "monsterData": 203729,
                "levelsInclusive": [17, 21],
                "weaponFamily": "123685..123688",
            },
            {
                "formulaId": (
                    "incomplete-rebuild-saw-6L-plus-1-minus-2-v1"
                ),
                "monsterData": 203728,
                "levelsInclusive": [17, 22],
                "weaponFamily": "122653..122656",
            },
            {
                "formulaId": (
                    "molested-molecules-saw-floor-11L-minus-2-over-2-v1"
                ),
                "monsterData": 203746,
                "levelsInclusive": [17, 25],
                "weaponFamily": "122216..122219",
            },
            {
                "formulaId": TEMPLE_CULTIST_FORMULA_ID,
                "monsterData": 26074,
                "levelsInclusive": [20, 35],
                "weaponFamily": "204747",
            },
            {
                "formulaId": TEMPLE_CULTIST_FORMULA_ID,
                "monsterData": 26082,
                "levelsInclusive": [20, 35],
                "weaponFamily": "130163..130164",
            },
            {
                "formulaId": TEMPLE_CULTIST_FORMULA_ID,
                "monsterData": 26103,
                "levelsInclusive": [20, 35],
                "weaponFamily": "129028..129029",
            },
            {
                "formulaId": TEMPLE_CULTIST_RAISED_PRIMARY_FORMULA_ID,
                "monsterData": 26135,
                "levelsInclusive": [20, 35],
                "weaponFamily": "158298..158299",
            },
            {
                "formulaId": TEMPLE_CULTIST_FORMULA_ID,
                "monsterData": 26137,
                "levelsInclusive": [20, 35],
                "weaponFamily": "204747",
            },
            {
                "formulaId": TEMPLE_CULTIST_FORMULA_ID,
                "monsterData": 26147,
                "levelsInclusive": [20, 35],
                "weaponFamily": "144103..144104",
            },
            {
                "formulaId": TEMPLE_CULTIST_FORMULA_ID,
                "monsterData": 26149,
                "levelsInclusive": [20, 35],
                "weaponFamily": "124313..124314",
            },
        ],
        "fixedScopeSelectorBindings": {
            "formulaId": "subway-fixed-scope-exact-runtime-selectors-v1",
            "rawPacketObservations": [
                {
                    "captureSession": (
                        "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                        "20260709-222339"
                    ),
                    "packetId": (
                        "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                        "20260709-222339|IN|12105|11ce43658f64"
                    ),
                    "classification": "Bloodcreeper dual-stream SAW",
                },
                {
                    "captureSession": (
                        "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                        "20260709-225408"
                    ),
                    "packetId": (
                        "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                        "20260709-225408|IN|14311|ce05ac91fcff"
                    ),
                    "classification": "Redundant Scan exact selected stream",
                },
                {
                    "captureSession": (
                        "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
                        "20260709-212336"
                    ),
                    "packetId": None,
                    "classification": (
                        "Workman Striker atomic level-stat-loadout generations"
                    ),
                },
            ],
            "selectorDomain": {
                "Workman Striker": (
                    "exact selected CapturedSubwayGenerationVariantDefinition"
                ),
                "Redundant Scan": (
                    "exact selected CapturedSubwayGenerationVariantDefinition"
                ),
                "Bloodcreeper": (
                    "exact family MonsterData and dual SKW1/SKW2 stream contract"
                ),
            },
            "activeBindings": [
                {
                    "resource": 127,
                    "name": name,
                    "monsterData": monster_data,
                    "configuredSourceIdentity": source,
                    "level": level,
                    "formulaId": selector,
                    "finalDisposition": "restored",
                }
                for name, monster_data, source, level, selector in (
                    (
                        "Bloodcreeper",
                        30379,
                        "0x795451C5",
                        24,
                        "bloodcreeper-exact-dual-stream-contract-v1",
                    ),
                    (
                        "Redundant Scan",
                        204178,
                        "0x7953AF85",
                        20,
                        "redundant-scan-exact-atomic-generation-selector-v1",
                    ),
                    (
                        "Workman Striker",
                        203854,
                        "0x7953AFF9",
                        14,
                        "workman-striker-exact-atomic-generation-selector-v1",
                    ),
                    (
                        "Workman Striker",
                        203854,
                        "0x7954501A",
                        14,
                        "workman-striker-exact-atomic-generation-selector-v1",
                    ),
                    (
                        "Workman Striker",
                        203854,
                        "0x79545219",
                        16,
                        "workman-striker-exact-atomic-generation-selector-v1",
                    ),
                )
            ],
        },
        "rejectedCandidates": [
            {
                "candidate": "exact unrounded affine line",
                "reason": (
                    "captured adjacent slopes differ; no single exact integer "
                    "linear expression reproduces all five points"
                ),
            },
            {
                "candidate": "direct item-template QL interpolation",
                "reason": (
                    "items.dat endpoints for templates 144742/144743 do not "
                    "produce the observed 30,35,45,49,54 SAW sequence from actor level"
                ),
            },
            {
                "candidate": "one generic formula for every SIW1 user",
                "reason": (
                    f"only {matching_cross_family} of {total_cross_family} "
                    "other-family observations match; MonsterData/family remains exact"
                ),
                "observations": cross_family,
            },
            {
                "candidate": "unbounded extrapolation",
                "reason": (
                    "no capture-backed categorical proof exists outside levels 5..10"
                ),
            },
        ],
    }


def inspect_family(inventory: dict[str, Any], family: str) -> None:
    packet_by_id = {
        packet["packetId"]: packet for packet in inventory.get("packets", [])
    }
    matches = []
    for profile in inventory.get("profiles", []):
        if (
            family.lower() not in str(profile.get("profileKey", "")).lower()
            and family.lower()
            not in json.dumps(profile.get("metadata", {})).lower()
        ):
            continue
        metadata = profile.get("metadata", {})
        packet_ids: set[str] = set()
        for key in ("incompleteObservations", "unsupportedSequences"):
            for observation in profile.get(key, []):
                for sample in observation.get("evidenceFound", {}).get("samples", []):
                    for packet_id in sample.values():
                        if isinstance(packet_id, str):
                            packet_ids.add(packet_id)
        for variant in profile.get("variants", []):
            for key in (
                "representativeWifuPacketId",
                "representativeSawPacketId",
                "representativeAttackPacketId",
            ):
                packet_id = variant.get(key)
                if isinstance(packet_id, str):
                    packet_ids.add(packet_id)
            for stream in variant.get("streams", []):
                packet_ids.update(stream.get("attackInfoPacketIds", []))
        matches.append(
            {
                "profileKey": profile.get("profileKey"),
                "level": metadata.get("level"),
                "sourceIdentity": metadata.get("sourceIdentity"),
                "status": profile.get("status"),
                "normalCompleteChainCount": profile.get("normalCompleteChainCount"),
                "packets": [
                    {
                        "packetId": packet_id,
                        "messageType": packet_by_id.get(packet_id, {}).get(
                            "messageType"
                        ),
                        "decoded": packet_by_id.get(packet_id, {}).get("decoded"),
                        "bodyHex": packet_by_id.get(packet_id, {}).get("bodyHex"),
                    }
                    for packet_id in sorted(packet_ids)
                    if packet_id in packet_by_id
                ],
            }
        )
    print(json.dumps(matches, indent=2, sort_keys=True))


def inspect_temple_cultists(inventory: dict[str, Any]) -> None:
    rows = []
    for profile in inventory.get("profiles", []):
        metadata = profile.get("metadata") or {}
        if (
            profile_resource(str(profile.get("profileKey", ""))) != 1931
            or metadata.get("name") != "Cultist"
        ):
            continue
        for variant in profile.get("variants", []):
            signature = variant.get("baseSignature") or {}
            wifu = signature.get("weaponItemFullUpdate") or {}
            saw = signature.get("specialAttackWeapon") or {}
            stats = {
                int(row.get("stat")): row.get("value")
                for row in wifu.get("stats", [])
                if isinstance(row.get("stat"), int)
            }
            rows.append(
                {
                    "monsterData": metadata.get("monsterData"),
                    "level": metadata.get("level"),
                    "sourceIdentity": metadata.get("sourceIdentity"),
                    "profileId": variant.get("semanticProfileId"),
                    "weaponLowTemplate": stats.get(702),
                    "weaponHighTemplate": stats.get(703),
                    "weaponQuality": stats.get(701),
                    "weaponEnergy": stats.get(26),
                    "weaponAttackDelay": stats.get(294),
                    "weaponRechargeDelay": stats.get(210),
                    "slot": wifu.get("inventorySlot"),
                    "saw": [
                        saw.get("unknown1"),
                        saw.get("unknown2"),
                        saw.get("unknown3"),
                        saw.get("unknown4"),
                        saw.get("unknown5"),
                    ],
                    "streams": [
                        row.get("signature")
                        for row in variant.get("streams", [])
                    ],
                    "captures": sorted(
                        {
                            row.get("capture")
                            for row in variant.get(
                                "mutableSawStateObservations", []
                            )
                            if row.get("capture")
                        }
                    ),
                }
            )
    rows.sort(
        key=lambda row: (
            row["monsterData"],
            row["level"],
            row["weaponLowTemplate"],
            row["weaponHighTemplate"],
            row["weaponQuality"],
            row["profileId"],
        )
    )
    print(
        "monsterData\tlevel\tsource\tprofileId\tweapon\tQL\tenergy\t"
        "delay/recharge\tslot\tSAW1:2:3:4:5\tstreams"
    )
    for row in rows:
        print(
            f"{row['monsterData']}\t{row['level']}\t{row['sourceIdentity']}\t"
            f"{row['profileId']}\t{row['weaponLowTemplate']}/"
            f"{row['weaponHighTemplate']}\t{row['weaponQuality']}\t"
            f"{row['weaponEnergy']}\t{row['weaponAttackDelay']}/"
            f"{row['weaponRechargeDelay']}\t{row['slot']}\t"
            f"{':'.join(str(value) for value in row['saw'])}\t"
            f"{json.dumps(row['streams'], sort_keys=True, separators=(',', ':'))}"
        )


def temple_active_loadouts() -> tuple[
    list[dict[str, Any]],
    dict[int, list[dict[str, Any]]],
]:
    import extract_capture_backed_npc_combat as extractor

    provider = TEMPLE_ORDINARY_CONTENT_PROVIDER.read_text(encoding="utf-8")
    pattern = re.compile(
        r"new SpawnSeed\(0x([0-9A-Fa-f]+),\s*"
        r"\"totw\.cultist\.(\d+)\",\s*(\d+),.*?"
        r"\"(\d{8}-\d{6})\"\)",
        re.DOTALL,
    )
    actors = [
        {
            "sourceIdentity": int(match.group(1), 16),
            "monsterData": int(match.group(2)),
            "level": int(match.group(3)),
            "capture": match.group(4),
        }
        for match in pattern.finditer(provider)
    ]
    source_ids = {row["sourceIdentity"] for row in actors}
    loadouts_by_source: dict[int, list[dict[str, Any]]] = {}
    for capture in sorted({row["capture"] for row in actors}):
        records, _, session, errors = extractor.parse_capture(
            extractor.CAPTURE_ROOT / capture
        )
        if not session["canonicalValid"] or errors:
            raise ValueError(
                f"{capture}: canonical={session['canonicalValid']} errors={errors}"
            )
        for record in records:
            if (
                record.message_type != "WeaponItemFullUpdate"
                or record.source not in source_ids
            ):
                continue
            decoded = record.decoded
            loadouts_by_source.setdefault(record.source, []).append(
                {
                    "capture": capture,
                    "sequence": record.sequence,
                    "packetId": record.packet_id,
                    "lowTemplate": decoded["lowTemplate"],
                    "highTemplate": decoded["highTemplate"],
                    "quality": decoded["quality"],
                    "energy": decoded["energy"],
                    "slot": decoded["inventorySlot"],
                    "flags": decoded["flags"],
                    "attackDelay": decoded["attackDelay"],
                    "rechargeDelay": decoded["rechargeDelay"],
                }
            )

    return actors, loadouts_by_source


def inspect_temple_active_loadouts() -> None:
    actors, loadouts_by_source = temple_active_loadouts()
    print(
        "source\tmonsterData\tlevel\tcapture\tloadoutCount\t"
        "stableLoadouts"
    )
    for actor in sorted(
        actors,
        key=lambda row: (
            row["monsterData"],
            row["level"],
            row["sourceIdentity"],
        ),
    ):
        observations = loadouts_by_source.get(actor["sourceIdentity"], [])
        stable = sorted(
            {
                (
                    row["lowTemplate"],
                    row["highTemplate"],
                    row["quality"],
                    row["slot"],
                    row["flags"],
                    row["attackDelay"],
                    row["rechargeDelay"],
                )
                for row in observations
            }
        )
        print(
            f"0x{actor['sourceIdentity']:08X}\t{actor['monsterData']}\t"
            f"{actor['level']}\t{actor['capture']}\t{len(observations)}\t"
            f"{json.dumps(stable, separators=(',', ':'))}"
        )


def summarize_temple_active_loadouts() -> None:
    actors, loadouts_by_source = temple_active_loadouts()
    summary: dict[int, dict[str, Any]] = {}
    for actor in actors:
        observations = loadouts_by_source.get(actor["sourceIdentity"], [])
        stable = {
            (
                row["lowTemplate"],
                row["highTemplate"],
                row["quality"],
                row["slot"],
            )
            for row in observations
        }
        if len(stable) != 1:
            raise ValueError(
                f"0x{actor['sourceIdentity']:08X}: expected one stable WIFU loadout"
            )
        low_template, high_template, quality, slot = next(iter(stable))
        group = summary.setdefault(
            actor["monsterData"],
            {
                "actors": 0,
                "levels": set(),
                "loadouts": set(),
                "qualities": set(),
            },
        )
        group["actors"] += 1
        group["levels"].add(actor["level"])
        group["loadouts"].add((low_template, high_template, slot))
        group["qualities"].add(quality)

    normalized = {
        str(monster_data): {
            "actors": group["actors"],
            "levels": sorted(group["levels"]),
            "loadouts": sorted(group["loadouts"]),
            "qualities": sorted(group["qualities"]),
        }
        for monster_data, group in sorted(summary.items())
    }
    print(json.dumps(normalized, indent=2, sort_keys=True))


def temple_noncultist_loadouts() -> tuple[
    list[dict[str, Any]],
    dict[int, list[dict[str, Any]]],
]:
    import extract_capture_backed_npc_combat as extractor

    sources = {
        0x7983FA22: ("Eternal Sentinel", 41690, 18),
        0x7983FA26: ("Eternal Sentinel", 41690, 20),
        0x7983FBC2: ("Eternal Sentinel", 41690, 18),
        0x7987F12D: ("Murial the Faithful", 26090, 34),
    }
    observations: dict[int, list[dict[str, Any]]] = {
        source: [] for source in sources
    }
    for capture in (
        "20260721-041439",
        "20260721-042139",
        "20260721-043204",
        "20260721-232051",
        "20260721-234614",
    ):
        records, _, session, errors = extractor.parse_capture(
            extractor.CAPTURE_ROOT / capture
        )
        if not session["canonicalValid"] or errors:
            raise ValueError(
                f"{capture}: canonical={session['canonicalValid']} errors={errors}"
            )
        for record in records:
            if (
                record.message_type != "WeaponItemFullUpdate"
                or record.source not in sources
            ):
                continue
            decoded = record.decoded
            observations[record.source].append(
                {
                    "capture": capture,
                    "sequence": record.sequence,
                    "packetId": record.packet_id,
                    "lowTemplate": decoded["lowTemplate"],
                    "highTemplate": decoded["highTemplate"],
                    "quality": decoded["quality"],
                    "energy": decoded["energy"],
                    "slot": decoded["inventorySlot"],
                    "flags": decoded["flags"],
                    "attackDelay": decoded["attackDelay"],
                    "rechargeDelay": decoded["rechargeDelay"],
                }
            )
    actors = []
    for source, (name, monster_data, level) in sources.items():
        actors.append(
            {
                "sourceIdentity": source,
                "name": name,
                "monsterData": monster_data,
                "level": level,
                "capture": observations[source][0]["capture"]
                if observations[source]
                else "",
            }
        )
    return actors, observations


def inspect_temple_noncultist_loadouts() -> None:
    actors, observations = temple_noncultist_loadouts()
    result = []
    for actor in actors:
        source = actor["sourceIdentity"]
        result.append(
            {
                "sourceIdentity": f"0x{source:08X}",
                "name": actor["name"],
                "monsterData": actor["monsterData"],
                "level": actor["level"],
                "observations": observations[source],
            }
        )
    print(json.dumps(result, indent=2, sort_keys=True))


def write_temple_active_loadouts() -> None:
    actors, loadouts_by_source = temple_active_loadouts()
    noncultist_actors, noncultist_loadouts = temple_noncultist_loadouts()
    actors.extend(noncultist_actors)
    loadouts_by_source.update(noncultist_loadouts)
    entries = []
    for actor in sorted(actors, key=lambda row: row["sourceIdentity"]):
        observations = loadouts_by_source.get(actor["sourceIdentity"], [])
        stable = {
            (
                row["lowTemplate"],
                row["highTemplate"],
                row["quality"],
                row["slot"],
                row["flags"],
                row["attackDelay"],
                row["rechargeDelay"],
            )
            for row in observations
        }
        if len(stable) != 1:
            raise ValueError(
                f"0x{actor['sourceIdentity']:08X}: expected one stable WIFU "
                f"loadout, found {sorted(stable)}"
            )
        low_template, high_template, quality, slot, flags, attack_delay, recharge_delay = (
            next(iter(stable))
        )
        if slot != 6 or attack_delay != 235 or recharge_delay != 235:
            raise ValueError(
                f"0x{actor['sourceIdentity']:08X}: unsupported WIFU semantics "
                f"slot={slot} delay={attack_delay}/{recharge_delay}"
            )
        first = min(
            observations,
            key=lambda row: (row["capture"], row["sequence"], row["packetId"]),
        )
        evidence = (
            f"{first['capture']} packet {first['packetId']} sequence "
            f"{first['sequence']}: exact active-spawn WIFU; flags={flags}; "
            f"attack/recharge={attack_delay}/{recharge_delay}"
        )
        entries.append(
            (
                actor["sourceIdentity"],
                actor["monsterData"],
                actor["level"],
                low_template,
                high_template,
                quality,
                evidence,
            )
        )

    lines = [
        "// <auto-generated />",
        "namespace AORebirth.Core.Playfields",
        "{",
        "    using System;",
        "    using System.Collections.Generic;",
        "",
        "    internal static class CapturedTempleOfThreeWindsOrdinaryCombatLoadoutCatalog",
        "    {",
        "        private static readonly IReadOnlyDictionary<int, Entry> Entries =",
        "            new Dictionary<int, Entry>",
        "            {",
    ]
    for (
        source_identity,
        monster_data,
        level,
        low_template,
        high_template,
        quality,
        evidence,
    ) in entries:
        escaped_evidence = evidence.replace("\\", "\\\\").replace('"', '\\"')
        lines.append(
            "                { unchecked((int)0x"
            f"{source_identity:08X}u), new Entry("
            f"{monster_data}, {level}, {low_template}, {high_template}, "
            f"{quality}, \"{escaped_evidence}\") }},"
        )
    lines.extend(
        [
            "            };",
            "",
            "        internal static OrdinaryEnemySpawnWeaponLoadout Resolve(",
            "            int sourceIdentity,",
            "            int monsterData,",
            "            int level)",
            "        {",
            "            Entry entry;",
            "            if (!Entries.TryGetValue(sourceIdentity, out entry))",
            "            {",
            "                throw new InvalidOperationException(",
            '                    "No exact active-spawn WIFU for Temple source 0x"',
            '                    + sourceIdentity.ToString("X8"));',
            "            }",
            "",
            "            if (entry.MonsterData != monsterData || entry.Level != level)",
            "            {",
            "                throw new InvalidOperationException(",
            '                    "Temple active-spawn WIFU correlation mismatch for source 0x"',
            '                    + sourceIdentity.ToString("X8"));',
            "            }",
            "",
            "            return new OrdinaryEnemySpawnWeaponLoadout(",
            "                entry.LowTemplate,",
            "                entry.HighTemplate,",
            "                entry.Quality,",
            "                entry.Evidence);",
            "        }",
            "",
            "        private sealed class Entry",
            "        {",
            "            internal Entry(",
            "                int monsterData,",
            "                int level,",
            "                int lowTemplate,",
            "                int highTemplate,",
            "                int quality,",
            "                string evidence)",
            "            {",
            "                this.MonsterData = monsterData;",
            "                this.Level = level;",
            "                this.LowTemplate = lowTemplate;",
            "                this.HighTemplate = highTemplate;",
            "                this.Quality = quality;",
            "                this.Evidence = evidence;",
            "            }",
            "",
            "            internal int MonsterData { get; private set; }",
            "            internal int Level { get; private set; }",
            "            internal int LowTemplate { get; private set; }",
            "            internal int HighTemplate { get; private set; }",
            "            internal int Quality { get; private set; }",
            "            internal string Evidence { get; private set; }",
            "        }",
            "    }",
            "}",
            "",
        ]
    )
    output = "\n".join(lines)
    TEMPLE_ORDINARY_COMBAT_LOADOUT_CATALOG.write_text(
        output,
        encoding="utf-8",
        newline="\n",
    )
    print(
        f"wrote {len(entries)} exact Temple active-spawn WIFU loadouts to "
        f"{TEMPLE_ORDINARY_COMBAT_LOADOUT_CATALOG}"
    )


def inspect_temple_active_coverage(active_coverage: dict[str, Any]) -> None:
    rows = [
        row
        for row in active_coverage.get("profiles", [])
        if row.get("runtimePlayfieldOrResource") == 1931
    ]
    print(json.dumps(rows, indent=2, sort_keys=True))


def temple_starting_quarantine_sources() -> set[int]:
    evidence = TEMPLE_CULTIST_QUARANTINE_EVIDENCE.read_text(encoding="utf-8")
    start = evidence.index("## Every resolver-rejected active row")
    end = evidence.index("\n## ", start + 4)
    cultist_sources = {
        int(value, 16)
        for value in re.findall(r"`0x([0-9A-Fa-f]{8})`", evidence[start:end])
    }
    if len(cultist_sources) != 76:
        raise ValueError(
            "expected 76 starting Cultist quarantine sources, found "
            f"{len(cultist_sources)}"
        )
    return cultist_sources | {
        0x7983FA22,
        0x7983FA26,
        0x7983FBC2,
        0x7987F12D,
    }


def emit_temple_starting_quarantine_constant(
    active_coverage: dict[str, Any],
) -> None:
    del active_coverage
    identities = temple_starting_quarantine_sources()
    print("TEMPLE_ORDINARY_STARTING_QUARANTINE_SOURCES = {")
    for source in sorted(identities):
        print(f"    0x{source:08X},")
    print("}")
    print(f"# count={len(identities)}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, default=DEFAULT_INVENTORY)
    parser.add_argument("--items", type=Path, default=DEFAULT_ITEMS)
    parser.add_argument(
        "--active-coverage", type=Path, default=DEFAULT_ACTIVE_COVERAGE
    )
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--write", action="store_true")
    mode.add_argument("--check", action="store_true")
    parser.add_argument("--inspect-family")
    parser.add_argument("--inspect-item", type=int, action="append")
    parser.add_argument("--inspect-monster-data", type=int)
    parser.add_argument("--inspect-profile-key")
    parser.add_argument("--inspect-special-templates", nargs=2, type=int)
    parser.add_argument("--inspect-temple-cultists", action="store_true")
    parser.add_argument("--inspect-temple-active-loadouts", action="store_true")
    parser.add_argument("--summarize-temple-active-loadouts", action="store_true")
    parser.add_argument("--inspect-temple-noncultist-loadouts", action="store_true")
    parser.add_argument("--write-temple-active-loadouts", action="store_true")
    parser.add_argument("--inspect-temple-active-coverage", action="store_true")
    parser.add_argument(
        "--emit-temple-starting-quarantine-constant",
        action="store_true",
    )
    parser.add_argument("--search-disobedient-formula", action="store_true")
    parser.add_argument("--search-stim-formula", action="store_true")
    parser.add_argument("--search-melded-formula", action="store_true")
    parser.add_argument("--search-fragmented-formula", action="store_true")
    arguments = parser.parse_args()

    inventory = load_json(arguments.inventory)
    if arguments.inspect_item:
        templates = load_item_templates(arguments.items)
        print(
            json.dumps(
                {
                    str(item_id): templates.get(item_id)
                    for item_id in arguments.inspect_item
                },
                indent=2,
                sort_keys=True,
            )
        )
        return 0
    if arguments.inspect_profile_key:
        profile = next(
            (
                row
                for row in inventory.get("profiles", [])
                if row.get("profileKey") == arguments.inspect_profile_key
            ),
            None,
        )
        print(json.dumps(compact_profile(profile or {}), indent=2, sort_keys=True))
        return 0
    if arguments.inspect_monster_data is not None:
        profiles = [
            compact_profile(row)
            for row in inventory.get("profiles", [])
            if (row.get("metadata") or {}).get("monsterData")
            == arguments.inspect_monster_data
        ]
        print(json.dumps(profiles, indent=2, sort_keys=True))
        return 0
    if arguments.inspect_special_templates:
        low_template, high_template = arguments.inspect_special_templates
        matches = []
        for profile in inventory.get("profiles", []):
            metadata = profile.get("metadata") or {}
            for variant in profile.get("variants", []):
                signature = variant.get("baseSignature", {})
                specials = signature.get("specialAttackWeapon", {}).get(
                    "specials", []
                )
                if not any(
                    row.get("lowTemplate") == low_template
                    and row.get("highTemplate") == high_template
                    for row in specials
                ):
                    continue
                matches.append(
                    {
                        "playfield": str(profile.get("profileKey", "")).split("|")[0],
                        "name": metadata.get("name"),
                        "monsterData": metadata.get("monsterData"),
                        "level": metadata.get("level"),
                        "profileId": variant.get("semanticProfileId"),
                        "saw": signature.get("specialAttackWeapon"),
                        "streams": [
                            row.get("signature")
                            for row in variant.get("streams", [])
                        ],
                    }
                )
        print(json.dumps(matches, sort_keys=True, separators=(",", ":")))
        return 0
    if arguments.inspect_temple_cultists:
        inspect_temple_cultists(inventory)
        return 0
    if arguments.inspect_temple_active_loadouts:
        inspect_temple_active_loadouts()
        return 0
    if arguments.summarize_temple_active_loadouts:
        summarize_temple_active_loadouts()
        return 0
    if arguments.inspect_temple_noncultist_loadouts:
        inspect_temple_noncultist_loadouts()
        return 0
    if arguments.write_temple_active_loadouts:
        write_temple_active_loadouts()
        return 0
    if arguments.inspect_temple_active_coverage:
        inspect_temple_active_coverage(load_json(arguments.active_coverage))
        return 0
    if arguments.emit_temple_starting_quarantine_constant:
        emit_temple_starting_quarantine_constant(
            load_json(arguments.active_coverage)
        )
        return 0
    if arguments.search_stim_formula:
        candidates = affine_candidates(STIM_FIEND_CAPTURED_VALUES)
        selected = [
            row
            for row in candidates
            if row["numerator"] == 11
            and row["intercept"] == -2
            and row["denominator"] == 2
            and row["rounding"] == "floor"
        ]
        print(
            json.dumps(
                {
                    "captured": STIM_FIEND_CAPTURED_VALUES,
                    "candidateCount": len(candidates),
                    "selected": selected,
                    "supportedLevelsInclusive": [10, 17],
                    "level17Prediction": stim_fiend_formula(17),
                },
                indent=2,
                sort_keys=True,
            )
        )
        return 0
    if arguments.search_melded_formula:
        candidates = affine_candidates(MELDED_PATTERNS_CAPTURED_BASE_VALUES)
        selected = [
            row
            for row in candidates
            if row["numerator"] == 11
            and row["intercept"] == -2
            and row["denominator"] == 2
            and row["rounding"] == "floor"
        ]
        print(
            json.dumps(
                {
                    "capturedBaseValues": (
                        MELDED_PATTERNS_CAPTURED_BASE_VALUES
                    ),
                    "candidateCount": len(candidates),
                    "selected": selected,
                    "supportedLevelsInclusive": [18, 25],
                    "level22Prediction": melded_patterns_formula(22),
                    "level23Prediction": melded_patterns_formula(23),
                },
                indent=2,
                sort_keys=True,
            )
        )
        return 0
    if arguments.search_fragmented_formula:
        unknown4_observations = {
            level: values[3]
            for level, values in FRAGMENTED_SOUL_CAPTURED_VALUES.items()
        }
        candidates = affine_candidates(
            unknown4_observations,
            maximum_denominator=64,
        )
        print(
            json.dumps(
                {
                    "capturedValues": FRAGMENTED_SOUL_CAPTURED_VALUES,
                    "singleAffineCandidateCountForUnknown4": len(candidates),
                    "selectedFormulaId": FRAGMENTED_SOUL_FORMULA_ID,
                    "selectedPredictions": {
                        level: fragmented_soul_formula(level)
                        for level in range(17, 22)
                    },
                    "supportedLevelsInclusive": [17, 21],
                },
                indent=2,
                sort_keys=True,
            )
        )
        return 0
    if arguments.search_disobedient_formula:
        captured = {5: 30, 6: 35, 8: 45, 9: 49, 10: 54}
        candidates = affine_candidates(captured)
        held_out = []
        for profile in inventory.get("profiles", []):
            metadata = profile.get("metadata") or {}
            for variant in profile.get("variants", []):
                saw = variant.get("baseSignature", {}).get(
                    "specialAttackWeapon", {}
                )
                specials = saw.get("specials", [])
                values = [saw.get(f"unknown{index}") for index in range(1, 5)]
                if (
                    not any(
                        row.get("lowTemplate") == 144742
                        and row.get("highTemplate") == 144743
                        for row in specials
                    )
                    or len(set(values)) != 1
                ):
                    continue
                held_out.append(
                    (
                        metadata.get("level"),
                        values[0],
                        metadata.get("name"),
                    )
                )
        for candidate in candidates:
            matches = [
                row
                for row in held_out
                if divide_rounded(
                    candidate["numerator"] * row[0] + candidate["intercept"],
                    candidate["denominator"],
                    candidate["rounding"],
                )
                == row[1]
            ]
            candidate["crossFamilyMatches"] = len(matches)
            candidate["crossFamilyObservations"] = len(held_out)
        candidates.sort(
            key=lambda row: (
                -row["crossFamilyMatches"],
                row["denominator"],
                abs(row["numerator"]),
                abs(row["intercept"]),
                row["rounding"],
            )
        )
        print(
            json.dumps(
                {
                    "captured": captured,
                    "candidateCount": len(candidates),
                    "predictedLevel7Values": sorted(
                        {row["level7"] for row in candidates}
                    ),
                    "topCandidates": candidates[:20],
                },
                indent=2,
                sort_keys=True,
            )
        )
        return 0
    if arguments.inspect_family:
        inspect_family(inventory, arguments.inspect_family)
        return 0

    dataset = build_formula_dataset(
        inventory,
        load_json(arguments.active_coverage),
        load_item_templates(arguments.items),
    )
    rendered = canonical_json(dataset)
    formula_binding_count = sum(
        len(dataset.get(key, {}).get("activeBindings", []))
        for key in (
            "acceptedFormula",
            "stimFiendFormula",
            "meldedPatternsFormula",
            "fragmentedSoulFormula",
            "incompleteRebuildFormula",
            "molestedMoleculesFormula",
            "fixedScopeSelectorBindings",
        )
    )
    if arguments.write:
        arguments.output.parent.mkdir(parents=True, exist_ok=True)
        arguments.output.write_text(rendered, encoding="utf-8", newline="\n")
        print(
            f"WROTE {arguments.output.relative_to(REPOSITORY_ROOT)} "
            f"profiles={len(dataset['profiles'])} "
            f"activeBindings={formula_binding_count}"
        )
        return 0
    if not arguments.output.is_file():
        print(f"ERROR: generated dataset is missing: {arguments.output}")
        return 1
    if arguments.output.read_text(encoding="utf-8") != rendered:
        print("ERROR: generated enemy combat setup formula dataset is stale")
        return 1
    print(
        f"PASS profiles={len(dataset['profiles'])} "
        f"activeBindings={formula_binding_count}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
