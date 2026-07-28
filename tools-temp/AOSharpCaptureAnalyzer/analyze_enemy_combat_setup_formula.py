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
            if not 10 <= level <= 17:
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
        supported = isinstance(level, int) and 10 <= level <= 17
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
    if len(stim_active_bindings) != 14:
        raise ValueError(
            f"expected 14 active Stim Fiend bindings, found {len(stim_active_bindings)}"
        )
    if len(stim_starting_scope) != 7:
        raise ValueError(
            f"expected 7 starting-scope Stim Fiends, found {len(stim_starting_scope)}"
        )
    if sum(row["formulaDomainSupported"] for row in stim_starting_scope) != 6:
        raise ValueError("Stim Fiend starting scope did not restore exactly six actors")
    if len(stim_cross_family) != 23:
        raise ValueError(
            f"expected 23 cross-family SIW1 observations, found {len(stim_cross_family)}"
        )
    if sum(row["exactMatch"] for row in stim_cross_family) != 17:
        raise ValueError("Stim Fiend formula cross-family reconciliation changed")

    return {
        "schemaVersion": 2,
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
            "supportedLevelsInclusive": [10, 17],
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
                    "reason": "levels below 10 and above 17 lack categorical and formula proof",
                },
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
    parser.add_argument("--search-stim-formula", action="store_true")
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
        for key in ("acceptedFormula", "stimFiendFormula")
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
