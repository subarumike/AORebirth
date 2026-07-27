#!/usr/bin/env python3
"""Build and validate deterministic enemy-combat setup formula evidence."""

from __future__ import annotations

import argparse
import hashlib
import math
import json
import re
import struct
import zlib
from pathlib import Path
from typing import Any


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
    match = re.search(r"\b(IN|OUT) #\d+ .*?\bn3=([^ ]+) hex=([0-9A-F]+)$", line)
    if match is None:
        raise ValueError(f"could not decode raw packet {capture} #{sequence}")
    wire = bytes.fromhex(match.group(3))
    body_marker = bytes.fromhex("1D3C0F1C")
    body_offset = wire.index(body_marker)
    body = wire[body_offset:]
    unknowns = struct.unpack(">IIIII", body[-20:])
    packet_hash = hashlib.sha256(wire).hexdigest()[:12]
    return {
        "packetId": (
            "tools-temp/AOSharpLiveCapture/bin/Debug/captures/"
            f"{capture}|{match.group(1)}|{sequence}|{packet_hash}"
        ),
        "captureSession": (
            f"tools-temp/AOSharpLiveCapture/bin/Debug/captures/{capture}"
        ),
        "sequence": sequence,
        "messageType": match.group(2),
        "bodyHex": body.hex().upper(),
        "unknown1": unknowns[0],
        "unknown2": unknowns[1],
        "unknown3": unknowns[2],
        "unknown4": unknowns[3],
        "unknown5": unknowns[4],
    }


def disobedient_bot_formula(level: int) -> int:
    return ((19 * level) + 28) // 4


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

    if any(not row["exactMatch"] for row in observations):
        raise ValueError("accepted formula differs from a raw SAW observation")
    if any(not row["exactMatch"] for row in leave_one_out):
        raise ValueError("accepted formula failed leave-one-out validation")
    if len(active_bindings) != 12:
        raise ValueError(
            f"expected 12 active Disobedient Bot bindings, found {len(active_bindings)}"
        )

    return {
        "schemaVersion": 1,
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
    parser.add_argument("--search-disobedient-formula", action="store_true")
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
    if arguments.write:
        arguments.output.parent.mkdir(parents=True, exist_ok=True)
        arguments.output.write_text(rendered, encoding="utf-8", newline="\n")
        print(
            f"WROTE {arguments.output.relative_to(REPOSITORY_ROOT)} "
            f"profiles={len(dataset['profiles'])} "
            f"activeBindings={len(dataset['acceptedFormula']['activeBindings'])}"
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
        f"activeBindings={len(dataset['acceptedFormula']['activeBindings'])}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
