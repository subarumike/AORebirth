#!/usr/bin/env python3
"""Resolve captured NPC observations to official placements without guessing identity."""

from __future__ import annotations

import argparse
import hashlib
import itertools
import json
import math
import os
import re
import statistics
import struct
import sys
from collections import Counter, defaultdict
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence

try:
    from Tools import npc_observation_harvester as harvester
except ModuleNotFoundError:  # Direct invocation from Tools/.
    import npc_observation_harvester as harvester  # type: ignore[no-redef]


SCHEMA_VERSION = 1
EXACT_EPSILON = 1.0e-6
EVIDENCE_PROVEN = "proven"
EVIDENCE_CORROBORATING = "corroborating"
EVIDENCE_HEURISTIC = "heuristic"
MATCH_UNIQUE = "unique-proven"
MATCH_AMBIGUOUS = "ambiguous"
MATCH_UNMATCHED = "unmatched"
MATCH_CONFLICT = "conflict"
TELEPORT_PROXY_PATTERN = re.compile(
    r"destPf=\(51102:(?P<proxy>[0-9A-Fa-f]+)\).*?changePf=\(Playfield2:(?P<runtime>[0-9A-Fa-f]+)\)"
)


class ResolverError(RuntimeError):
    """Raised when the deterministic evidence boundary cannot be maintained."""


@dataclass(frozen=True)
class CoordinateTransform:
    """One authoritative representation of a placement-to-runtime transform."""

    name: str
    axis_order: tuple[int, int, int] = (0, 1, 2)
    signs: tuple[int, int, int] = (1, 1, 1)
    scale: float = 1.0
    offset: tuple[float, float, float] = (0.0, 0.0, 0.0)
    quantization: float | None = None
    district_centre_mode: str = "none"
    evidence_class: str = EVIDENCE_HEURISTIC
    proven: bool = False
    proof: str = ""


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=Path("build-verify/npc-placement-identity-resolver"),
    )
    parser.add_argument("--tests-status", default="NOT_RUN")
    parser.add_argument("--commit", default="PENDING")
    return parser.parse_args(argv)


def optional_int(value: Any) -> int | None:
    if value is None or (isinstance(value, str) and not value.strip()):
        return None
    try:
        return int(str(value).strip())
    except (TypeError, ValueError):
        return None


def optional_float(value: Any) -> float | None:
    if value is None or (isinstance(value, str) and not value.strip()):
        return None
    try:
        return float(str(value).strip())
    except (TypeError, ValueError):
        return None


def position_from_values(values: Sequence[Any]) -> tuple[float, float, float] | None:
    parsed = tuple(optional_float(value) for value in values)
    if len(parsed) != 3 or any(value is None for value in parsed):
        return None
    return tuple(float(value) for value in parsed)  # type: ignore[arg-type,return-value]


def euclidean(left: Sequence[float], right: Sequence[float]) -> float:
    return math.sqrt(sum((float(left[index]) - float(right[index])) ** 2 for index in range(3)))


def apply_coordinate_transform(
    position: Sequence[float],
    transform: CoordinateTransform,
    *,
    district_centre: Sequence[float] | None = None,
    require_proven: bool = True,
) -> tuple[float, float, float]:
    """Apply transform math from one helper; production identity requires proof."""

    if require_proven and not transform.proven:
        raise ResolverError("Coordinate transform is not proven: " + transform.name)
    if len(position) != 3:
        raise ResolverError("Coordinate position must contain exactly three values.")
    source = tuple(float(value) for value in position)
    ordered = tuple(source[index] for index in transform.axis_order)
    result = tuple(
        ordered[index] * transform.signs[index] * transform.scale + transform.offset[index]
        for index in range(3)
    )
    if transform.district_centre_mode != "none":
        if district_centre is None or len(district_centre) != 3:
            raise ResolverError("District-centre candidate lacks a decoded centre value.")
        centre = tuple(float(value) for value in district_centre)
        if transform.district_centre_mode == "add-all":
            result = tuple(result[index] + centre[index] for index in range(3))
        elif transform.district_centre_mode == "subtract-all":
            result = tuple(result[index] - centre[index] for index in range(3))
        elif transform.district_centre_mode == "add-xz":
            result = (result[0] + centre[0], result[1], result[2] + centre[2])
        elif transform.district_centre_mode == "subtract-xz":
            result = (result[0] - centre[0], result[1], result[2] - centre[2])
        else:
            raise ResolverError("Unsupported district-centre mode: " + transform.district_centre_mode)
    if transform.quantization:
        step = transform.quantization
        result = tuple(round(value / step) * step for value in result)
    return tuple(float(value) for value in result)


def atomic_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    pending = path.with_suffix(path.suffix + ".pending")
    pending.write_text(
        json.dumps(value, indent=2, sort_keys=True, ensure_ascii=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    os.replace(pending, path)


def normalized_digest(output_dir: Path) -> str:
    digest = hashlib.sha256()
    for path in sorted(output_dir.glob("*.json"), key=lambda value: value.name):
        if path.name == "summary.json":
            continue
        digest.update(path.name.encode("utf-8"))
        digest.update(b"\0")
        digest.update(path.read_bytes())
    return digest.hexdigest()


def percentile(values: Sequence[float], fraction: float) -> float | None:
    if not values:
        return None
    ordered = sorted(float(value) for value in values)
    if len(ordered) == 1:
        return ordered[0]
    position = (len(ordered) - 1) * fraction
    low = math.floor(position)
    high = math.ceil(position)
    if low == high:
        return ordered[low]
    weight = position - low
    return ordered[low] * (1.0 - weight) + ordered[high] * weight


def round_metric(value: float | None) -> float | None:
    return None if value is None else round(float(value), 6)


def float32_bytes(value: float) -> bytes:
    return struct.pack(">f", float(value))


def walk_field_inventory(
    value: Any,
    path: str,
    inventory: dict[str, dict[str, Any]],
) -> None:
    if isinstance(value, Mapping):
        if not value:
            row = inventory.setdefault(path, {"occurrences": 0, "types": set(), "nonNull": 0})
            row["occurrences"] += 1
            row["types"].add("object")
        for key in sorted(value):
            walk_field_inventory(value[key], path + "." + str(key) if path else str(key), inventory)
        return
    if isinstance(value, list):
        if not value:
            row = inventory.setdefault(path + "[]", {"occurrences": 0, "types": set(), "nonNull": 0})
            row["occurrences"] += 1
            row["types"].add("empty-array")
        for item in value:
            walk_field_inventory(item, path + "[]", inventory)
        return
    row = inventory.setdefault(path, {"occurrences": 0, "types": set(), "nonNull": 0})
    row["occurrences"] += 1
    row["types"].add("null" if value is None else type(value).__name__)
    if value is not None:
        row["nonNull"] += 1


def finalized_field_inventory(inventory: Mapping[str, Mapping[str, Any]]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for path in sorted(inventory):
        value = inventory[path]
        lower = path.lower()
        opaque = any(
            token in lower
            for token in (
                "unknown",
                "opaque",
                "additionalpoints",
                "extensions",
                "shortpairs",
                "secondaryhashes",
                "spawninfo",
                "rotationpoints",
                "acghash",
            )
        )
        rows.append(
            {
                "fieldPath": path,
                "occurrences": int(value["occurrences"]),
                "nonNull": int(value["nonNull"]),
                "types": sorted(value["types"]),
                "semanticState": "opaque-or-structural-only" if opaque else "decoded-typed-field",
            }
        )
    return rows


def load_official_corpus(
    repo_root: Path,
) -> tuple[dict[str, Any], list[dict[str, Any]], list[dict[str, Any]], list[dict[str, Any]]]:
    manifest_path = repo_root / "docs/reference/playfields/official-placement-source-manifest.json"
    manifest = harvester.load_json(manifest_path)
    index = harvester.load_json(repo_root / "docs/generated/playfields/official-placement-index.json")
    resources: list[dict[str, Any]] = []
    placements: list[dict[str, Any]] = []
    field_inventory: dict[str, dict[str, Any]] = {}
    for entry in sorted(index.get("Playfields", []), key=lambda row: int(row.get("PlayfieldId", -1))):
        relative_path = entry.get("Path")
        if not isinstance(relative_path, str):
            raise ResolverError("Official placement index contains an invalid shard path.")
        shard = harvester.load_json(repo_root / relative_path)
        if not shard:
            raise ResolverError("Official placement shard is unreadable: " + relative_path)
        walk_field_inventory(shard, "Shard", field_inventory)
        districts = {
            int(row["DistrictIndex"]): row
            for row in shard.get("Districts", [])
            if row.get("DistrictIndex") is not None
        }
        resources.append(
            {
                "playfieldId": shard.get("PlayfieldId"),
                "resourceType": shard.get("ResourceType"),
                "resourceInstance": shard.get("ResourceInstance"),
                "formatVersion": shard.get("FormatVersion"),
                "sourceClientBuild": shard.get("SourceClientBuild"),
                "sourceClientVariant": shard.get("SourceClientVariant"),
                "parseStatus": shard.get("ParseStatus"),
                "parseError": shard.get("ParseError"),
                "districtCount": shard.get("DistrictCount"),
                "officialSpawnCount": shard.get("OfficialSpawnCount"),
                "districts": shard.get("Districts", []),
                "unknownFields": shard.get("UnknownFields", {}),
                "sourceShard": relative_path,
            }
        )
        for source_record in shard.get("Records", []):
            district = districts.get(int(source_record.get("DistrictIndex", -1)), {})
            source_position = position_from_values(
                (
                    source_record.get("PositionX"),
                    source_record.get("PositionY"),
                    source_record.get("PositionZ"),
                )
            )
            district_centre = position_from_values(district.get("UnknownFields", {}).get("Centre", ()))
            placements.append(
                {
                    "placementId": source_record.get("OfficialSpawnRecordId"),
                    "resourceType": source_record.get("ResourceType"),
                    "resourceInstance": source_record.get("ResourceInstance"),
                    "playfieldId": source_record.get("PlayfieldId"),
                    "districtId": district.get("OfficialDistrictId"),
                    "districtIndex": source_record.get("DistrictIndex"),
                    "districtName": source_record.get("DistrictName"),
                    "placementOrdinal": source_record.get("DistrictRecordOrdinal"),
                    "sourcePosition": list(source_position) if source_position else None,
                    "localPosition": None,
                    "worldPosition": None,
                    "positionSemanticState": "typed-vector-source-space; local-versus-world-not-proven",
                    "districtCentre": list(district_centre) if district_centre else None,
                    "districtCentreSemanticState": "decoded-vector; transform/origin semantics not proven",
                    "orientation": {
                        "rotationMidEncoded": source_record.get("RotationMidEncoded"),
                        "rotationWidthEncoded": source_record.get("RotationWidthEncoded"),
                        "heading": None,
                        "semanticState": "encoded-values-retained; heading conversion not proven",
                    },
                    "provenIdentifiers": {
                        "officialPlacementId": source_record.get("OfficialSpawnRecordId"),
                        "officialResourceId": source_record.get("UnknownFields", {}).get("OfficialResourceId"),
                        "officialDistrictId": district.get("OfficialDistrictId"),
                        "resourceType": source_record.get("ResourceType"),
                        "resourceInstance": source_record.get("ResourceInstance"),
                        "recordOffsetInResource": source_record.get("RecordOffset"),
                        "recordOffsetInDatabase": source_record.get("UnknownFields", {}).get(
                            "RecordOffsetInDatabase"
                        ),
                    },
                    "unprovenIndirection": {
                        "templateId": None,
                        "monsterData": None,
                        "spawnGroupId": None,
                        "parentId": None,
                        "objectArchiveId": None,
                        "playfieldObjectId": None,
                        "districtObjectId": None,
                        "runtimeInstanceId": None,
                        "status": "not encoded with proven semantics in the available normalized structure",
                    },
                    "spawnMetadata": {
                        "levelMinimum": source_record.get("LevelMinimum"),
                        "levelMaximum": source_record.get("LevelMaximum"),
                        "radius": source_record.get("Radius"),
                        "assistanceRadius": source_record.get("AssistanceRadius"),
                        "respawnChance": source_record.get("RespawnChance"),
                        "respawnTime": source_record.get("RespawnTime"),
                        "nativeFlags": source_record.get("NativeFlags"),
                        "moreFlags": source_record.get("MoreFlags"),
                        "serializedOptionalFlags": source_record.get("SerializedOptionalFlags"),
                        "unknownOptionalU8": source_record.get("UnknownOptionalU8"),
                    },
                    "pathPatrolMetadata": {
                        "additionalPoints": source_record.get("UnknownFields", {}).get("AdditionalPoints", []),
                        "semanticState": "retained-opaque; path-or-patrol semantics not proven",
                    },
                    "acgHash": {
                        "wireBytes": source_record.get("OfficialAcgHashWireBytes"),
                        "nativeUInt32": source_record.get("OfficialAcgHashNativeUInt32"),
                        "text": source_record.get("CanonicalAcgHashText"),
                        "semanticState": "packed-four-byte-scalar-tag; never runtime identity",
                    },
                    "aoRebirthOverlay": {
                        "sourceNpcId": source_record.get("SourceNpcId"),
                        "existingProfile": source_record.get("ExistingAoRebirthProfile"),
                        "resolvedMobTemplateId": source_record.get("ResolvedMobTemplateId"),
                        "resolvedMonsterData": source_record.get("ResolvedMonsterData"),
                        "identityResolutionStatus": source_record.get("IdentityResolutionStatus"),
                        "runtimeActivationAuthorized": source_record.get("RuntimeActivationAuthorized"),
                        "identityAuthority": "not native official placement identity",
                    },
                    "sourceRecord": source_record,
                }
            )
    placements.sort(key=lambda row: str(row["placementId"]))
    expected = manifest.get("SourceCorpusBoundary", {}).get("StaticHashSpawnRecords")
    if expected is None or int(expected) != len(placements):
        raise ResolverError(
            "Official placement count mismatch: expected={0}, actual={1}".format(expected, len(placements))
        )
    return manifest, resources, placements, finalized_field_inventory(field_inventory)


def official_provenance_payload(
    manifest: Mapping[str, Any],
    resources: Sequence[Mapping[str, Any]],
    placements: Sequence[Mapping[str, Any]],
    field_inventory: Sequence[Mapping[str, Any]],
) -> dict[str, Any]:
    opaque_byte_ranges: dict[str, dict[str, int]] = defaultdict(lambda: {"resources": 0, "bytes": 0})
    for resource in resources:
        source_unknown = resource.get("unknownFields", {}).get("SourceUnknownFields", {})
        for key in ("TrailingOpaqueRegion", "RecordAllocationSlack"):
            value = source_unknown.get(key)
            if isinstance(value, Mapping) and int(value.get("Length", 0)) > 0:
                opaque_byte_ranges[key]["resources"] += 1
                opaque_byte_ranges[key]["bytes"] += int(value.get("Length", 0))
    source_artifacts = manifest.get("SourceArtifacts", [])
    return {
        "schemaVersion": SCHEMA_VERSION,
        "sourceProvenance": {
            "resourceDatabaseInstalledRelativePath": "Anarchy Online/cd_image/data/db/ResourceDatabase.dat",
            "resourceDatabaseFiles": manifest.get("ResourceDatabaseSha256", {}),
            "resourceType": manifest.get("ResourceType"),
            "sourceClientBuild": manifest.get("SourceClientBuild"),
            "sourceClientVariant": manifest.get("SourceClientVariant"),
            "resourceInstanceRelationship": manifest.get("ResourceInstanceRelationship", {}),
            "sourceCorpusBoundary": manifest.get("SourceCorpusBoundary", {}),
            "sourceArtifacts": source_artifacts,
            "aoRebirthExtractionEntryPoint": "Tools/import_official_playfield_placements.py",
            "normalizedIndex": "docs/generated/playfields/official-placement-index.json",
            "normalizedShards": "docs/generated/playfields/placements/pf_<resource-instance>.json",
        },
        "retentionAudit": {
            "normalizedImporterDiscardedFieldsFound": [
                {
                    "field": "AcgHashNativeUInt32Hex",
                    "state": "validated upstream but not emitted; exactly derivable from OfficialAcgHashNativeUInt32",
                }
            ],
            "unexpectedUpstreamFieldsCanBeSilentlyDiscarded": True,
            "reason": (
                "source validation accepts supersets while resource, district, and record normalization use "
                "fixed projections; unavailable upstream shards prevent a complete unexpected-key audit"
            ),
            "rawResourceDatabasePayloadPresentInAoRebirth": False,
            "upstreamResourceShardPayloadsPresentInAoRebirth": False,
            "sourceArtifactsRetainedByHashOnly": [
                {"relativePath": row.get("RelativePath"), "sha256": row.get("Sha256")}
                for row in source_artifacts
            ],
            "unavailableOpaqueBytePayloads": [
                {
                    "field": name,
                    "resourceCount": metrics["resources"],
                    "byteCount": metrics["bytes"],
                    "retainedMetadata": "offset,length,sha256 only where present",
                }
                for name, metrics in sorted(opaque_byte_ranges.items())
            ],
            "decodeBoundary": (
                "AORebirth retains decoded typed values and explicit unknown-field metadata, but not the "
                "upstream 630 source shard payloads, raw ResourceDatabase bytes, or opaque-region bytes."
            ),
        },
        "fieldInventory": list(field_inventory),
        "officialFieldPathCount": len(field_inventory),
        "newlyDecodedFields": [],
        "resources": list(resources),
        "placements": list(placements),
        "placementCount": len(placements),
    }


def load_capture_position_history(
    records: Iterable[harvester.CaptureRecord],
) -> dict[str, list[dict[str, Any]]]:
    histories: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for record in records:
        if not record.path:
            continue
        for row in harvester.read_csv(record.path / "scfu-appearance.csv"):
            if row.get("DecodeStatus") != "decoded_complete" or row.get("CharacterInfoType") != "NPCInfo":
                continue
            identity = row.get("Identity", "").strip()
            if not identity:
                continue
            position = position_from_values(
                (row.get("PositionX"), row.get("PositionY"), row.get("PositionZ"))
            )
            if position is None:
                continue
            histories[record.capture_id + "|" + identity].append(
                {
                    "position": list(position),
                    "runtimePlayfieldId": optional_int(row.get("PlayfieldId")),
                    "sequence": optional_int(row.get("Sequence")),
                    "globalOrdinal": optional_int(row.get("GlobalOrdinal")),
                    "capturedUtc": row.get("CapturedUtc", ""),
                    "direction": row.get("Direction", ""),
                }
            )
    for rows in histories.values():
        rows.sort(
            key=lambda row: (
                row.get("globalOrdinal") if row.get("globalOrdinal") is not None else -1,
                row.get("sequence") if row.get("sequence") is not None else -1,
                row.get("capturedUtc", ""),
                row["position"],
            )
        )
    return dict(histories)


def official_resource_instances(manifest: Mapping[str, Any], resources: Sequence[Mapping[str, Any]]) -> set[int]:
    relationship = manifest.get("ResourceInstanceRelationship", {})
    if relationship.get("Status") != "PROVEN_FOR_ALL_VALIDATED_CONTROLS":
        return set()
    return {
        int(row["resourceInstance"])
        for row in resources
        if row.get("resourceInstance") is not None
    }


def capture_runtime_proxy_pairs(record: harvester.CaptureRecord) -> list[dict[str, Any]]:
    if not record.path:
        return []
    path = record.path / "events.log"
    try:
        lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
    except OSError:
        return []
    pairs: dict[tuple[int, int], dict[str, Any]] = {}
    for line in lines:
        match = TELEPORT_PROXY_PATTERN.search(line)
        if not match:
            continue
        proxy = int(match.group("proxy"), 16)
        runtime = int(match.group("runtime"), 16)
        pairs.setdefault(
            (runtime, proxy),
            {
                "runtimePlayfieldId": runtime,
                "destinationPlayfieldProxyId": proxy,
                "source": "events.log MISSION-FLOW IN-N3-TELEPORT",
                "evidenceClass": EVIDENCE_CORROBORATING,
                "semanticState": (
                    "packet directly pairs the values, but identity type 51102 is not proven to be the "
                    "official type-1000014 base partition"
                ),
            },
        )
    return [pairs[key] for key in sorted(pairs)]


def build_runtime_playfield_mapping(
    records: Sequence[harvester.CaptureRecord],
    observations: Sequence[harvester.NpcObservation],
    official_instances: set[int],
    same_epoch_model_evidence: Mapping[str, Mapping[str, Any]] | None = None,
) -> tuple[dict[str, Any], dict[str, dict[str, Any]]]:
    same_epoch_model_evidence = same_epoch_model_evidence or {}
    observations_by_capture: dict[str, list[harvester.NpcObservation]] = defaultdict(list)
    for observation in observations:
        observations_by_capture[observation.capture_id].append(observation)
    mappings: list[dict[str, Any]] = []
    mapping_by_capture: dict[str, dict[str, Any]] = {}
    pairs: dict[tuple[int, int], dict[str, Any]] = {}
    for record in records:
        capture_info = harvester.load_json(record.path / "capture_info.json") if record.path else {}
        capture_session = harvester.load_json(record.path / "capture-session.json") if record.path else {}
        info_runtime = optional_int(capture_info.get("playfieldId"))
        info_resource = optional_int(capture_info.get("resourcePlayfieldId"))
        session_resource = optional_int(capture_session.get("resourcePlayfieldId"))
        observation_runtime_ids = sorted(
            {
                int(value.runtime_playfield_id)
                for value in observations_by_capture.get(record.capture_id, [])
                if value.runtime_playfield_id is not None
            }
        )
        proxy_pairs = capture_runtime_proxy_pairs(record)
        evidence: list[dict[str, Any]] = []
        conflicts: list[str] = []
        if info_runtime is not None:
            evidence.append(
                {
                    "class": EVIDENCE_PROVEN,
                    "value": info_runtime,
                    "meaning": "runtime playfield identifier emitted from Game.PlayfieldInit",
                    "source": "capture_info.json:playfieldId",
                    "code": "tools-temp/AOSharpLiveCapture/Main.cs:1535,7190",
                }
            )
        if info_resource is not None:
            evidence.append(
                {
                    "class": EVIDENCE_PROVEN,
                    "value": info_resource,
                    "meaning": "client Playfield.ModelIdentity.Instance sampled at capture start",
                    "source": "capture_info.json:resourcePlayfieldId",
                    "code": "tools-temp/AOSharpLiveCapture/Main.cs:8708-8718,9080-9088",
                }
            )
        if session_resource is not None:
            evidence.append(
                {
                    "class": EVIDENCE_CORROBORATING,
                    "value": session_resource,
                    "meaning": "capture-start resource playfield duplicate",
                    "source": "capture-session.json:resourcePlayfieldId",
                }
            )
        if info_resource is not None and session_resource is not None and info_resource != session_resource:
            conflicts.append("capture_info and capture-session resource playfield identifiers disagree")
        if info_resource is not None and record.resource_playfield_id is not None and info_resource != record.resource_playfield_id:
            conflicts.append("capture_info resource playfield disagrees with capture path/inventory label")
        if info_runtime is not None and observation_runtime_ids and info_runtime not in observation_runtime_ids:
            conflicts.append("capture_info runtime playfield is absent from decoded NPC observations")
        base_official = info_resource in official_instances if info_resource is not None else False
        if info_resource is not None and not base_official:
            conflicts.append("client model identity has no validated official type-1000014 resource instance")
        phase_reference_runtime = info_runtime
        if (
            phase_reference_runtime is None
            and len(observation_runtime_ids) > 1
            and info_resource in observation_runtime_ids
        ):
            phase_reference_runtime = info_resource
        phase_conflicting_runtime_ids = [
            runtime_id
            for runtime_id in observation_runtime_ids
            if phase_reference_runtime is not None and runtime_id != phase_reference_runtime
        ]
        atomic_evidence = same_epoch_model_evidence.get(record.capture_id, {})
        atomic_runtime = optional_int(atomic_evidence.get("runtimePlayfieldId"))
        atomic_type = optional_int(atomic_evidence.get("modelIdentityType"))
        atomic_instance = optional_int(atomic_evidence.get("modelIdentityInstance"))
        atomic_epoch = str(atomic_evidence.get("zoneEpoch", "")).strip()
        atomic_evidence_complete = bool(
            atomic_runtime is not None
            and atomic_type == 1000014
            and atomic_instance in official_instances
            and atomic_epoch
            and atomic_runtime in observation_runtime_ids
        )
        if atomic_evidence:
            evidence.append(
                {
                    "class": EVIDENCE_PROVEN if atomic_evidence_complete else EVIDENCE_CORROBORATING,
                    "type": "same-zone-epoch-full-model-identity",
                    "value": dict(atomic_evidence),
                    "validation": (
                        "complete-and-bound-to-observed-runtime-zone-epoch"
                        if atomic_evidence_complete
                        else "rejected-incomplete-wrong-type-or-unbound-runtime-zone-epoch"
                    ),
                }
            )
        same_epoch_proven_mappings = (
            [
                {
                    "runtimePlayfieldId": int(atomic_runtime),
                    "basePlayfieldResourceId": int(atomic_instance),
                    "modelIdentityType": int(atomic_type),
                    "zoneEpoch": atomic_epoch,
                    "evidenceClass": EVIDENCE_PROVEN,
                }
            ]
            if atomic_evidence_complete
            else []
        )
        mapping_proven = bool(
            same_epoch_proven_mappings
            and observation_runtime_ids == [atomic_runtime]
        )
        if mapping_proven:
            info_runtime = atomic_runtime
            info_resource = atomic_instance
            base_official = True
        mapping_status = (
            "proven-single-observed-zone-epoch"
            if mapping_proven
            else "partial-same-epoch-proven"
            if same_epoch_proven_mappings
            else "conflict"
            if conflicts
            else "not-proven-phase-ambiguous"
        )
        row = {
            "captureId": record.capture_id,
            "capturePath": record.inventory_path,
            "currentPathAvailable": record.path is not None,
            "runtimePlayfieldId": info_runtime,
            "basePlayfieldResourceId": info_resource,
            "observationRuntimePlayfieldIds": observation_runtime_ids,
            "pathOrInventoryPlayfieldId": record.resource_playfield_id,
            "mappingStatus": mapping_status,
            "mappingProven": mapping_proven,
            "officialPartitionProven": base_official,
            "sameEpochPairObserved": atomic_evidence_complete,
            "sameEpochModelEvidence": dict(atomic_evidence) if atomic_evidence else None,
            "sameEpochProvenMappings": same_epoch_proven_mappings,
            "phaseConflictingRuntimePlayfieldIds": phase_conflicting_runtime_ids,
            "packetRuntimeDestinationProxyPairs": proxy_pairs,
            "evidence": evidence,
            "conflicts": conflicts,
        }
        mappings.append(row)
        mapping_by_capture[record.capture_id] = row
        if same_epoch_proven_mappings:
            proven_mapping = same_epoch_proven_mappings[0]
            key = (
                int(proven_mapping["runtimePlayfieldId"]),
                int(proven_mapping["basePlayfieldResourceId"]),
            )
            aggregate = pairs.setdefault(
                key,
                {
                    "runtimePlayfieldId": key[0],
                    "basePlayfieldResourceId": key[1],
                    "captureIds": [],
                    "evidenceClass": EVIDENCE_PROVEN,
                },
            )
            aggregate["captureIds"].append(record.capture_id)
    runtime_to_bases: dict[int, set[int]] = defaultdict(set)
    for runtime_pf, base_pf in pairs:
        runtime_to_bases[runtime_pf].add(base_pf)
    aggregate_conflicts = [
        {"runtimePlayfieldId": runtime_pf, "basePlayfieldResourceIds": sorted(base_ids)}
        for runtime_pf, base_ids in sorted(runtime_to_bases.items())
        if len(base_ids) > 1
    ]
    for row in pairs.values():
        row["captureIds"].sort()
    payload = {
        "schemaVersion": SCHEMA_VERSION,
        "mappingScope": "runtime instance to client model identity to official resource instance",
        "globalMappingClaim": False,
        "sourceSemantics": {
            "runtime": "Game.PlayfieldInit event value / Playfield.Identity runtime instance",
            "base": "Playfield.ModelIdentity.Instance",
            "officialPartition": "type-1000014 ResourceInstance; 630/630 validated controls in source manifest",
            "teleportDestinationProxy": (
                "N3Teleport identity type 51102 paired with ChangePlayfield; direct packet relation, but "
                "not proven equivalent to a type-1000014 base partition"
            ),
        },
        "captureMappings": mappings,
        "provenPairs": [pairs[key] for key in sorted(pairs)],
        "aggregateConflicts": aggregate_conflicts,
        "provenCaptureMappings": sum(bool(row["sameEpochProvenMappings"]) for row in mappings),
        "fullyProvenCaptureMappings": sum(row["mappingProven"] for row in mappings),
        "partiallyProvenCaptureMappings": sum(
            row["mappingStatus"] == "partial-same-epoch-proven" for row in mappings
        ),
        "notProvenCaptureMappings": sum(str(row["mappingStatus"]).startswith("not-proven") for row in mappings),
        "conflictingCaptureMappings": sum(row["mappingStatus"] == "conflict" for row in mappings),
        "conclusion": (
            "The two IDs are distinct. Current capture_info does not prove a pair: resourcePlayfieldId is "
            "frozen from Playfield.ModelIdentity.Instance at session start, while playfieldId is the latest "
            "PlayfieldInit/final runtime value. Multi-zone captures demonstrate that the capture-level "
            "resource label becomes stale for earlier/later SCFU rows. A mapping requires a same-zone-epoch "
            "sample of both values; no accepted production capture retains that pair."
        ),
    }
    return payload, mapping_by_capture


def mapping_for_observation_epoch(
    capture_mapping: Mapping[str, Any], runtime_playfield_id: int | None
) -> dict[str, Any]:
    """Apply only a same-zone-epoch pair bound to this observation's runtime."""

    mapping = dict(capture_mapping)
    mapping["conflicts"] = list(mapping.get("conflicts", []))
    matches = [
        dict(row)
        for row in mapping.get("sameEpochProvenMappings", [])
        if optional_int(row.get("runtimePlayfieldId")) == runtime_playfield_id
    ]
    base_ids = sorted(
        {
            int(base_id)
            for row in matches
            if (base_id := optional_int(row.get("basePlayfieldResourceId"))) is not None
        }
    )
    if len(base_ids) == 1:
        mapping["mappingStatus"] = "proven-observation-zone-epoch"
        mapping["mappingProven"] = True
        mapping["runtimePlayfieldId"] = runtime_playfield_id
        mapping["basePlayfieldResourceId"] = base_ids[0]
        mapping["officialPartitionProven"] = True
        mapping["appliedSameEpochMappings"] = matches
    elif len(base_ids) > 1:
        mapping["mappingStatus"] = "conflict"
        mapping["mappingProven"] = False
        mapping["basePlayfieldResourceId"] = None
        mapping["conflicts"].append(
            "same observation runtime and zone epoch map to multiple official base playfields"
        )
    else:
        mapping["mappingProven"] = False
        if mapping.get("sameEpochProvenMappings"):
            mapping["mappingStatus"] = "not-proven-for-observation-zone-epoch"
    return mapping


def field_value(observation: harvester.NpcObservation, name: str) -> Any:
    return observation.fields.get(name, {}).get("value")


def normalized_signature(value: Any) -> str:
    return hashlib.sha256(
        json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode("utf-8")
    ).hexdigest()


def observation_cluster_key(observation: harvester.NpcObservation) -> tuple[Any, ...]:
    return (
        observation.resource_playfield_id,
        observation.runtime_playfield_id,
        observation.identity,
    )


def build_observation_clusters(
    observations: Sequence[harvester.NpcObservation],
    position_history: Mapping[str, Sequence[Mapping[str, Any]]],
) -> tuple[list[dict[str, Any]], dict[str, str]]:
    grouped: dict[tuple[Any, ...], list[harvester.NpcObservation]] = defaultdict(list)
    for observation in observations:
        grouped[observation_cluster_key(observation)].append(observation)
    rows: list[dict[str, Any]] = []
    observation_to_cluster: dict[str, str] = {}
    for key in sorted(grouped, key=lambda value: json.dumps(value, sort_keys=True)):
        members = sorted(grouped[key], key=lambda value: value.observation_id)
        key_digest = hashlib.sha256(
            json.dumps(key, sort_keys=True, separators=(",", ":")).encode("utf-8")
        ).hexdigest()[:20]
        cluster_id = "npc-cluster-" + key_digest
        all_positions = [
            tuple(float(value) for value in row["position"])
            for member in members
            for row in position_history.get(member.observation_id, [])
            if row.get("position") is not None
        ]
        if not all_positions:
            all_positions = [member.position for member in members if member.position is not None]
        centre = (
            tuple(statistics.fmean(position[index] for position in all_positions) for index in range(3))
            if all_positions
            else None
        )
        distances = [euclidean(position, centre) for position in all_positions] if centre else []
        variance = (
            statistics.fmean(distance * distance for distance in distances) if distances else None
        )
        monster_values = sorted(
            {
                field_value(member, "monsterData")
                for member in members
                if field_value(member, "monsterData") is not None
            },
            key=lambda value: str(value),
        )
        appearance_values = sorted(
            {
                normalized_signature(
                    {
                        "headMesh": field_value(member, "headMesh"),
                        "textures": field_value(member, "textures"),
                        "meshes": field_value(member, "meshes"),
                        "breed": field_value(member, "breed"),
                        "gender": field_value(member, "gender"),
                        "race": field_value(member, "race"),
                    }
                )
                for member in members
            }
        )
        name_values = sorted({member.name for member in members})
        runtime_ids = sorted(
            {member.runtime_playfield_id for member in members if member.runtime_playfield_id is not None}
        )
        resource_ids = sorted(
            {member.resource_playfield_id for member in members if member.resource_playfield_id is not None}
        )
        conflicts = []
        if len(monster_values) > 1:
            conflicts.append("MonsterData changes inside conservative runtime lineage")
        if len(appearance_values) > 1:
            conflicts.append("appearance signature changes inside conservative runtime lineage")
        if len(name_values) > 1:
            conflicts.append("name changes inside conservative runtime lineage")
        row = {
            "clusterId": cluster_id,
            "observationIds": [member.observation_id for member in members],
            "observationCount": len(members),
            "captureCount": len({member.capture_id for member in members}),
            "captureIds": sorted({member.capture_id for member in members}),
            "playfields": {"resource": resource_ids, "runtime": runtime_ids},
            "runtimeIdentities": sorted({member.identity for member in members}),
            "stablePosition": list(centre) if centre is not None and max(distances, default=0.0) <= 0.1 else None,
            "positionCentre": list(centre) if centre is not None else None,
            "positionVariance": round_metric(variance),
            "positionMaximumDeviation": round_metric(max(distances)) if distances else None,
            "positionSampleCount": len(all_positions),
            "stableMonsterData": monster_values[0] if len(monster_values) == 1 else None,
            "stableAppearance": appearance_values[0] if len(appearance_values) == 1 else None,
            "stableName": name_values[0] if len(name_values) == 1 else None,
            "candidatePlacements": [],
            "conflicts": conflicts,
            "continuityEvidence": {
                "movementObservationIds": [
                    member.observation_id
                    for member in members
                    if member.category_evidence.get("movement", False)
                ],
                "lifecycleObservationIds": [
                    member.observation_id
                    for member in members
                    if member.category_evidence.get("lifecycle", False)
                ],
                "respawnObservationIds": [
                    member.observation_id
                    for member in members
                    if member.category_evidence.get("respawn", False)
                ],
            },
            "clusteringEvidence": {
                "class": EVIDENCE_CORROBORATING,
                "basis": (
                    "resource playfield + runtime playfield + exact runtime identity lineage; MonsterData, "
                    "appearance, name, and position are evaluated after grouping so contradictions cannot "
                    "be hidden by splitting the lineage"
                ),
            },
        }
        rows.append(row)
        for member in members:
            observation_to_cluster[member.observation_id] = cluster_id
    return rows, observation_to_cluster


def candidate_transforms() -> list[CoordinateTransform]:
    axes = "xyz"
    transforms: list[CoordinateTransform] = []
    for order in itertools.permutations(range(3)):
        label = "".join(axes[index] for index in order)
        for signs in itertools.product((1, -1), repeat=3):
            sign_label = "".join("+" if value > 0 else "-" for value in signs)
            name = "axis-order-" + label
            if signs != (1, 1, 1):
                name += "-signs-" + sign_label
            transforms.append(
                CoordinateTransform(name=name, axis_order=tuple(order), signs=tuple(signs))
            )
    for scale in (0.01, 0.1, 10.0, 100.0):
        transforms.append(CoordinateTransform(name="uniform-scale-" + str(scale), scale=scale))
    for step in (0.01, 0.1, 0.5, 1.0):
        transforms.append(
            CoordinateTransform(name="source-quantization-" + str(step), quantization=step)
        )
    for mode in ("add-all", "subtract-all", "add-xz", "subtract-xz"):
        transforms.append(
            CoordinateTransform(name="district-centre-" + mode, district_centre_mode=mode)
        )
    transforms.extend(
        [
            CoordinateTransform(name="yaw-origin-90", axis_order=(2, 1, 0), signs=(1, 1, -1)),
            CoordinateTransform(name="yaw-origin-180", signs=(-1, 1, -1)),
            CoordinateTransform(name="yaw-origin-270", axis_order=(2, 1, 0), signs=(-1, 1, 1)),
        ]
    )
    return transforms


def transformed_placement_position(
    placement: Mapping[str, Any],
    transform: CoordinateTransform,
    *,
    require_proven: bool,
) -> tuple[float, float, float] | None:
    source_position = placement.get("sourcePosition")
    if not isinstance(source_position, list) or len(source_position) != 3:
        return None
    district_centre = placement.get("districtCentre")
    try:
        return apply_coordinate_transform(
            source_position,
            transform,
            district_centre=district_centre if isinstance(district_centre, list) else None,
            require_proven=require_proven,
        )
    except ResolverError:
        return None


def axis_projection_diagnostics(
    samples: Sequence[tuple[harvester.NpcObservation, int, str]],
    placements_by_playfield: Mapping[int, Sequence[Mapping[str, Any]]],
) -> list[dict[str, Any]]:
    axes = "XYZ"
    rows: list[dict[str, Any]] = []
    for projection in ((0, 1), (0, 2), (1, 2)):
        indexes: dict[int, dict[tuple[bytes, bytes], list[Mapping[str, Any]]]] = {}
        for playfield in sorted({playfield for _, playfield, _ in samples}):
            index: dict[tuple[bytes, bytes], list[Mapping[str, Any]]] = defaultdict(list)
            for placement in placements_by_playfield.get(playfield, []):
                position = placement.get("sourcePosition")
                if not isinstance(position, list) or len(position) != 3:
                    continue
                key = tuple(float32_bytes(position[axis]) for axis in projection)
                index[key].append(placement)
            indexes[playfield] = index
        exact_observations = 0
        unique_observations = 0
        ambiguous_observations = 0
        placement_ids: set[str] = set()
        capture_ids: set[str] = set()
        pf4582_y_errors: list[float] = []
        pf4582_names_agree = 0
        pf4582_unique = 0
        pf4582_placement_ids: set[str] = set()
        pf4582_capture_ids: set[str] = set()
        pf4582_radius_counts = Counter()
        for observation, playfield, _ in samples:
            if observation.position is None:
                continue
            key = tuple(float32_bytes(observation.position[axis]) for axis in projection)
            candidates = indexes.get(playfield, {}).get(key, [])
            if not candidates:
                continue
            exact_observations += 1
            capture_ids.add(observation.capture_id)
            placement_ids.update(str(row.get("placementId")) for row in candidates)
            if len(candidates) == 1:
                unique_observations += 1
            else:
                ambiguous_observations += 1
            if playfield == 4582 and projection == (0, 2) and len(candidates) == 1:
                candidate = candidates[0]
                pf4582_unique += 1
                pf4582_capture_ids.add(observation.capture_id)
                pf4582_placement_ids.add(str(candidate.get("placementId")))
                source_position = candidate.get("sourcePosition")
                if isinstance(source_position, list):
                    pf4582_y_errors.append(abs(observation.position[1] - float(source_position[1])))
                profile = candidate.get("aoRebirthOverlay", {}).get("existingProfile")
                resolved_name = candidate.get("sourceRecord", {}).get("ResolvedMobTemplateName")
                compared_name = resolved_name or (str(profile).split(":")[-1] if profile else None)
                if compared_name and observation.name == compared_name:
                    pf4582_names_agree += 1
                radius = candidate.get("spawnMetadata", {}).get("radius")
                pf4582_radius_counts[str(radius)] += 1
        row = {
            "projection": "".join(axes[index] for index in projection),
            "samples": len(samples),
            "exactFloat32ObservationMatches": exact_observations,
            "uniqueCoordinateObservationMatches": unique_observations,
            "ambiguousCoordinateObservationMatches": ambiguous_observations,
            "distinctPlacementRecords": len(placement_ids),
            "captureCount": len(capture_ids),
            "evidenceClass": EVIDENCE_CORROBORATING,
            "identityEligible": False,
            "blockingReason": "a two-axis projection drops one required coordinate and cannot identify a placement",
        }
        if projection == (0, 2):
            row["pf4582"] = {
                "uniqueXzCoordinateObservations": pf4582_unique,
                "distinctPlacementRecords": len(pf4582_placement_ids),
                "captureCount": len(pf4582_capture_ids),
                "existingOverlayNameAgreements": pf4582_names_agree,
                "absoluteYErrorMedian": round_metric(statistics.median(pf4582_y_errors))
                if pf4582_y_errors
                else None,
                "absoluteYErrorP95": round_metric(percentile(pf4582_y_errors, 0.95)),
                "absoluteYErrorMaximum": round_metric(max(pf4582_y_errors))
                if pf4582_y_errors
                else None,
                "officialRadiusCounts": dict(sorted(pf4582_radius_counts.items())),
                "conclusion": (
                    "Exact float32 X/Z repetition across independent captures strongly corroborates the "
                    "same X/Z axes and scale. Y remains different and no evidence proves that Y may be "
                    "discarded or normalized, so these are not three-dimensional placement identities."
                ),
            }
        rows.append(row)
    return rows


def analyze_coordinate_transforms(
    observations: Sequence[harvester.NpcObservation],
    placements: Sequence[Mapping[str, Any]],
    mapping_by_capture: Mapping[str, Mapping[str, Any]],
) -> dict[str, Any]:
    placements_by_playfield: dict[int, list[Mapping[str, Any]]] = defaultdict(list)
    for placement in placements:
        playfield = optional_int(placement.get("playfieldId"))
        if playfield is not None:
            placements_by_playfield[playfield].append(placement)
    samples: list[tuple[harvester.NpcObservation, int, str]] = []
    partition_basis_counts = Counter()
    for observation in observations:
        if observation.position is None:
            continue
        mapping = mapping_by_capture.get(observation.capture_id, {})
        proxy_partitions = sorted(
            {
                int(pair["destinationPlayfieldProxyId"])
                for pair in mapping.get("packetRuntimeDestinationProxyPairs", [])
                if pair.get("runtimePlayfieldId") == observation.runtime_playfield_id
                and placements_by_playfield.get(int(pair.get("destinationPlayfieldProxyId", -1)))
            }
        )
        runtime_pf = observation.runtime_playfield_id
        resource_pf = observation.resource_playfield_id
        if len(proxy_partitions) == 1:
            samples.append((observation, proxy_partitions[0], "packet-destination-proxy-hypothesis"))
            partition_basis_counts["packet-destination-proxy-hypothesis"] += 1
        elif runtime_pf is not None and placements_by_playfield.get(runtime_pf):
            samples.append((observation, runtime_pf, "runtime-numeric-static-partition-hypothesis"))
            partition_basis_counts["runtime-numeric-static-partition-hypothesis"] += 1
        elif resource_pf is not None and placements_by_playfield.get(resource_pf):
            samples.append((observation, resource_pf, "capture-resource-label-hypothesis"))
            partition_basis_counts["capture-resource-label-hypothesis"] += 1
    analyses: list[dict[str, Any]] = []
    for transform in candidate_transforms():
        transformed_by_playfield: dict[int, list[tuple[float, float, float]]] = {}
        for playfield in sorted({playfield for _, playfield, _ in samples}):
            transformed_by_playfield[playfield] = [
                value
                for placement in placements_by_playfield[playfield]
                if (
                    value := transformed_placement_position(
                        placement, transform, require_proven=False
                    )
                )
                is not None
            ]
        errors: list[float] = []
        playfields: set[int] = set()
        for observation, playfield, _ in samples:
            candidates = transformed_by_playfield.get(playfield, [])
            if not candidates or observation.position is None:
                continue
            errors.append(min(euclidean(observation.position, candidate) for candidate in candidates))
            playfields.add(playfield)
        analyses.append(
            {
                "TRANSFORM_NAME": transform.name,
                "SAMPLES": len(errors),
                "PLAYFIELDS": len(playfields),
                "PLAYFIELD_IDS": sorted(playfields),
                "MEDIAN_ERROR": round_metric(statistics.median(errors)) if errors else None,
                "P95_ERROR": round_metric(percentile(errors, 0.95)),
                "MAX_ERROR": round_metric(max(errors)) if errors else None,
                "EXACT_MATCHES": sum(error <= EXACT_EPSILON for error in errors),
                "WITHIN_0_1": sum(error <= 0.1 for error in errors),
                "WITHIN_0_5": sum(error <= 0.5 for error in errors),
                "WITHIN_1_0": sum(error <= 1.0 for error in errors),
                "REJECTED_REASON": (
                    "Nearest-neighbor measurements use unpaired observations. No placement-specific "
                    "identifier or independent anchor proves which placement generated an observation, "
                    "so these metrics cannot prove a transform."
                ),
                "evidenceClass": EVIDENCE_HEURISTIC,
                "proven": False,
                "transform": asdict(transform),
            }
        )
    partition_conflicts: dict[tuple[int, int], list[harvester.NpcObservation]] = defaultdict(list)
    for observation in observations:
        if (
            observation.position is not None
            and observation.resource_playfield_id is not None
            and observation.runtime_playfield_id is not None
            and observation.resource_playfield_id != observation.runtime_playfield_id
            and placements_by_playfield.get(observation.resource_playfield_id)
            and placements_by_playfield.get(observation.runtime_playfield_id)
        ):
            partition_conflicts[
                (observation.resource_playfield_id, observation.runtime_playfield_id)
            ].append(observation)
    identity_transform = CoordinateTransform(name="axis-order-xyz")
    conflict_diagnostics: list[dict[str, Any]] = []
    for (resource_pf, runtime_pf), grouped in sorted(partition_conflicts.items()):
        resource_positions = [
            transformed_placement_position(row, identity_transform, require_proven=False)
            for row in placements_by_playfield[resource_pf]
        ]
        runtime_positions = [
            transformed_placement_position(row, identity_transform, require_proven=False)
            for row in placements_by_playfield[runtime_pf]
        ]
        resource_positions = [row for row in resource_positions if row is not None]
        runtime_positions = [row for row in runtime_positions if row is not None]
        resource_errors = [
            min(euclidean(observation.position, position) for position in resource_positions)
            for observation in grouped
            if observation.position is not None and resource_positions
        ]
        runtime_errors = [
            min(euclidean(observation.position, position) for position in runtime_positions)
            for observation in grouped
            if observation.position is not None and runtime_positions
        ]
        conflict_diagnostics.append(
            {
                "captureResourcePlayfieldId": resource_pf,
                "rowRuntimePlayfieldId": runtime_pf,
                "observations": len(grouped),
                "captureIds": sorted({row.capture_id for row in grouped}),
                "resourcePartitionMedianNearestError": round_metric(statistics.median(resource_errors))
                if resource_errors
                else None,
                "runtimeNumericPartitionMedianNearestError": round_metric(statistics.median(runtime_errors))
                if runtime_errors
                else None,
                "resourcePartitionWithin1": sum(value <= 1.0 for value in resource_errors),
                "runtimeNumericPartitionWithin1": sum(value <= 1.0 for value in runtime_errors),
                "evidenceClass": EVIDENCE_CORROBORATING,
                "conclusion": (
                    "The capture-level resource label cannot be applied to every row in this multi-zone "
                    "capture. Numeric runtime partitioning is a stronger coordinate diagnostic but is not "
                    "a proven runtime-to-base bridge."
                ),
            }
        )
    for name, reason in (
        (
            "global-fixed-offset",
            "No independently paired placement/observation anchors exist from which to derive or test a fixed offset.",
        ),
        (
            "playfield-specific-offset",
            "No independently paired anchors exist; fitting one offset per playfield would tune to the answer set.",
        ),
        (
            "district-origin-translation",
            "District Centre is decoded as a vector but its origin/translation semantics are not established.",
        ),
        (
            "district-rotation-matrix",
            "No transform matrix or proven district rotation field is present in the available schema.",
        ),
        (
            "cell-grid-transform",
            "No cell size, grid origin, or placement-to-cell identifier is present in the available schema.",
        ),
        (
            "instanced-playfield-transform",
            "Runtime-to-base playfield mapping is capture-scoped, but no instance-coordinate transform is exposed.",
        ),
    ):
        analyses.append(
            {
                "TRANSFORM_NAME": name,
                "SAMPLES": 0,
                "PLAYFIELDS": 0,
                "PLAYFIELD_IDS": [],
                "MEDIAN_ERROR": None,
                "P95_ERROR": None,
                "MAX_ERROR": None,
                "EXACT_MATCHES": 0,
                "WITHIN_0_1": 0,
                "WITHIN_0_5": 0,
                "WITHIN_1_0": 0,
                "REJECTED_REASON": reason,
                "evidenceClass": EVIDENCE_HEURISTIC,
                "proven": False,
                "transform": None,
            }
        )
    return {
        "schemaVersion": SCHEMA_VERSION,
        "sampleBasis": (
            "one consolidated captured observation versus its nearest official placement; rows use the "
            "numeric runtime partition when that number exists in the static corpus, otherwise the frozen "
            "capture resource label. Both partition choices are hypotheses and correspondence remains unknown"
        ),
        "compositionPolicy": (
            "All 48 axis-order/sign combinations are composed and measured. Uniform scale, quantization, "
            "district-centre translation, and yaw candidates are measured deterministically on the direct "
            "axis basis; no fitted per-NPC transform is permitted."
        ),
        "partitionBasisCounts": dict(sorted(partition_basis_counts.items())),
        "multiZonePartitionDiagnostics": conflict_diagnostics,
        "axisProjectionDiagnostics": axis_projection_diagnostics(samples, placements_by_playfield),
        "candidateTransforms": analyses,
        "coordinateSystemProven": False,
        "districtTransformDecoded": False,
        "authoritativeProductionTransform": None,
        "conclusion": (
            "The available data strongly favors direct X/Y/Z coordinates for several static numeric runtime "
            "partitions, and multi-zone captures explain major frozen-label outliers. It still cannot prove a "
            "placement-to-runtime transform because every error is based on an unpaired nearest neighbor. District Centre and "
            "other arrays remain structural/opaque, and no transform matrix, origin contract, or paired "
            "identity anchor is retained."
        ),
    }


def candidate_metadata(placement: Mapping[str, Any]) -> dict[str, Any]:
    return {
        "placementId": placement.get("placementId"),
        "playfieldId": placement.get("playfieldId"),
        "districtId": placement.get("districtId"),
        "districtName": placement.get("districtName"),
        "sourcePosition": placement.get("sourcePosition"),
        "worldPosition": placement.get("worldPosition"),
        "radius": placement.get("spawnMetadata", {}).get("radius"),
        "levelMinimum": placement.get("spawnMetadata", {}).get("levelMinimum"),
        "levelMaximum": placement.get("spawnMetadata", {}).get("levelMaximum"),
        "sourceRecordOffset": placement.get("provenIdentifiers", {}).get("recordOffsetInResource"),
        "templateId": placement.get("unprovenIndirection", {}).get("templateId"),
        "monsterDataIfProven": placement.get("unprovenIndirection", {}).get("monsterData"),
        "acgHash": placement.get("acgHash"),
    }


def independent_corroborating_elimination(
    observation: Mapping[str, Any], candidates: Sequence[Mapping[str, Any]]
) -> tuple[list[Mapping[str, Any]], list[dict[str, Any]]]:
    """Report independent contradictions without allowing them to create proof."""

    survivors: list[Mapping[str, Any]] = []
    eliminations: list[dict[str, Any]] = []
    observed = observation.get("corroborating", {})
    for candidate in candidates:
        metadata = candidate.get("provenCorroborating", {})
        contradictions = [
            name
            for name in sorted(set(observed).intersection(metadata))
            if observed.get(name) is not None
            and metadata.get(name) is not None
            and observed.get(name) != metadata.get(name)
        ]
        if contradictions:
            eliminations.append(
                {
                    "placementId": candidate.get("placementId"),
                    "contradictingFields": contradictions,
                    "evidenceClass": EVIDENCE_CORROBORATING,
                }
            )
        else:
            survivors.append(candidate)
    return survivors, eliminations


def resolve_candidate_set(
    observation: Mapping[str, Any],
    placements: Sequence[Mapping[str, Any]],
    mapping: Mapping[str, Any],
    transform: CoordinateTransform,
    *,
    cluster_conflict: bool = False,
) -> dict[str, Any]:
    position = position_from_values(observation.get("position", ()))
    evidence: list[dict[str, Any]] = []
    if mapping.get("mappingProven"):
        evidence.append(
            {
                "class": EVIDENCE_PROVEN,
                "type": "capture-scoped-runtime-to-base-playfield",
                "value": mapping.get("basePlayfieldResourceId"),
            }
        )
    elif mapping.get("mappingStatus") == "conflict":
        evidence.append(
            {
                "class": EVIDENCE_PROVEN,
                "type": "conflicting-playfield-evidence",
                "details": mapping.get("conflicts", []),
            }
        )
    else:
        evidence.append(
            {
                "class": mapping.get("candidateBaseEvidenceClass", EVIDENCE_HEURISTIC),
                "type": mapping.get("candidateBaseBasis", "path-or-inventory-playfield-label"),
                "value": mapping.get(
                    "candidateBasePlayfieldId", observation.get("resourcePlayfieldId")
                ),
            }
        )
    if transform.proven:
        evidence.append(
            {"class": EVIDENCE_PROVEN, "type": "placement-to-runtime-transform", "value": transform.name}
        )
    else:
        evidence.append(
            {
                "class": EVIDENCE_HEURISTIC,
                "type": "unproven-coordinate-hypothesis",
                "value": transform.name,
            }
        )
    if cluster_conflict:
        return {
            "matchState": MATCH_CONFLICT,
            "exactCandidates": [],
            "regionCandidates": [],
            "nearestCandidates": [],
            "identityEvidence": evidence,
            "blockingReason": "repeated observation cluster contradicts candidate metadata",
            "acgHashUsedAsRuntimeIdentity": False,
        }
    if mapping.get("mappingStatus") == "conflict":
        return {
            "matchState": MATCH_CONFLICT,
            "exactCandidates": [],
            "regionCandidates": [],
            "nearestCandidates": [],
            "identityEvidence": evidence,
            "blockingReason": "conflicting playfield evidence",
            "acgHashUsedAsRuntimeIdentity": False,
        }
    if position is None:
        return {
            "matchState": MATCH_UNMATCHED,
            "exactCandidates": [],
            "regionCandidates": [],
            "nearestCandidates": [],
            "identityEvidence": evidence,
            "blockingReason": "runtime position not observed",
            "acgHashUsedAsRuntimeIdentity": False,
        }
    distances: list[tuple[float, Mapping[str, Any]]] = []
    exact: list[Mapping[str, Any]] = []
    regions: list[Mapping[str, Any]] = []
    for placement in placements:
        transformed = transformed_placement_position(
            placement, transform, require_proven=transform.proven
        )
        if transformed is None:
            continue
        distance = euclidean(position, transformed)
        candidate = dict(placement)
        candidate["candidateRuntimePosition"] = list(transformed)
        candidate["distance"] = distance
        distances.append((distance, candidate))
        if distance <= EXACT_EPSILON:
            exact.append(candidate)
        radius = optional_float(placement.get("spawnMetadata", {}).get("radius"))
        if radius is not None and radius > 0.0 and distance <= radius:
            regions.append(candidate)
    distances.sort(key=lambda item: (item[0], str(item[1].get("placementId"))))
    exact.sort(key=lambda row: str(row.get("placementId")))
    regions.sort(key=lambda row: str(row.get("placementId")))
    _, exact_eliminations = independent_corroborating_elimination(observation, exact)
    _, region_eliminations = independent_corroborating_elimination(observation, regions)
    eliminations = exact_eliminations + [
        row for row in region_eliminations if row not in exact_eliminations
    ]
    proof_ready = bool(mapping.get("mappingProven") and transform.proven)
    if exact:
        evidence.append(
            {
                "class": EVIDENCE_PROVEN if proof_ready else EVIDENCE_HEURISTIC,
                "type": "transformed-exact-three-dimensional-coordinate",
                "placementIds": [row.get("placementId") for row in exact],
                "epsilon": EXACT_EPSILON,
            }
        )
    if regions:
        evidence.append(
            {
                "class": EVIDENCE_HEURISTIC,
                "type": "official-radius-containment",
                "placementIds": [row.get("placementId") for row in regions],
            }
        )
    if distances:
        evidence.append(
            {
                "class": EVIDENCE_HEURISTIC,
                "type": "nearest-placement-diagnostic",
                "placementIds": [row.get("placementId") for _, row in distances[:5]],
                "distances": [round_metric(distance) for distance, _ in distances[:5]],
            }
        )
    if eliminations:
        evidence.append(
            {
                "class": EVIDENCE_CORROBORATING,
                "type": "candidate-metadata-contradiction",
                "eliminations": eliminations,
            }
        )
    if proof_ready and len(exact) == 1 and not exact_eliminations:
        state = MATCH_UNIQUE
        blocker = None
    elif len(exact) == 1 and exact_eliminations:
        state = MATCH_CONFLICT
        blocker = "exact transformed placement contradicts independent candidate metadata"
    elif exact or regions:
        state = MATCH_AMBIGUOUS
        blockers: list[str] = []
        if not mapping.get("mappingProven"):
            blockers.append("base playfield mapping is not proven for this observation epoch")
        if not transform.proven:
            blockers.append("placement-to-runtime coordinate transform is not proven")
        if len(exact) > 1:
            blockers.append("multiple placements share the exact transformed coordinate")
        if regions and not exact:
            blockers.append("official spawn-region containment is not placement identity")
        blocker = "; ".join(blockers) or "candidate evidence does not establish unique placement identity"
    else:
        state = MATCH_UNMATCHED
        blocker = "runtime observation is outside all exact coordinates and positive-radius regions"
    return {
        "matchState": state,
        "exactCandidates": [candidate_metadata(row) for row in exact],
        "regionCandidates": [candidate_metadata(row) for row in regions],
        "nearestCandidates": [
            {**candidate_metadata(row), "distance": round_metric(distance)}
            for distance, row in distances[:5]
        ],
        "candidateEliminations": eliminations,
        "identityEvidence": evidence,
        "blockingReason": blocker,
        "acgHashUsedAsRuntimeIdentity": False,
    }


def observation_resolution_input(observation: harvester.NpcObservation) -> dict[str, Any]:
    return {
        "observationId": observation.observation_id,
        "resourcePlayfieldId": observation.resource_playfield_id,
        "runtimePlayfieldId": observation.runtime_playfield_id,
        "position": list(observation.position) if observation.position else None,
        "corroborating": {
            "name": observation.name or None,
            "monsterData": field_value(observation, "monsterData"),
            "headMesh": field_value(observation, "headMesh"),
            "textures": field_value(observation, "textures"),
            "meshes": field_value(observation, "meshes"),
            "breed": field_value(observation, "breed"),
            "gender": field_value(observation, "gender"),
            "race": field_value(observation, "race"),
        },
    }


def resolve_observations(
    observations: Sequence[harvester.NpcObservation],
    placements: Sequence[Mapping[str, Any]],
    mapping_by_capture: Mapping[str, Mapping[str, Any]],
    clusters: list[dict[str, Any]],
    observation_to_cluster: Mapping[str, str],
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    placements_by_playfield: dict[int, list[Mapping[str, Any]]] = defaultdict(list)
    for placement in placements:
        playfield = optional_int(placement.get("playfieldId"))
        if playfield is not None:
            placements_by_playfield[playfield].append(placement)
    cluster_by_id = {row["clusterId"]: row for row in clusters}
    production_transform = CoordinateTransform(
        name="identity-coordinate-hypothesis",
        evidence_class=EVIDENCE_HEURISTIC,
        proven=False,
        proof="unpaired nearest-neighbor diagnostics only",
    )
    rows: list[dict[str, Any]] = []
    candidate_rows: list[dict[str, Any]] = []
    cluster_candidate_ids: dict[str, set[str]] = defaultdict(set)
    for observation in sorted(observations, key=lambda value: value.observation_id):
        mapping = dict(mapping_by_capture.get(
            observation.capture_id,
            {
                "mappingStatus": "not-proven",
                "mappingProven": False,
                "basePlayfieldResourceId": None,
                "conflicts": [],
            },
        ))
        mapping = mapping_for_observation_epoch(mapping, observation.runtime_playfield_id)
        if (
            not mapping.get("mappingProven")
            and observation.runtime_playfield_id in mapping.get("phaseConflictingRuntimePlayfieldIds", [])
        ):
            mapping["mappingStatus"] = "conflict"
            mapping["mappingProven"] = False
            mapping["conflicts"] = list(mapping.get("conflicts", [])) + [
                "observation runtime playfield belongs to a different zone epoch than the capture-level resource label"
            ]
        proxy_candidates = sorted(
            {
                int(pair["destinationPlayfieldProxyId"])
                for pair in mapping.get("packetRuntimeDestinationProxyPairs", [])
                if pair.get("runtimePlayfieldId") == observation.runtime_playfield_id
                and int(pair.get("destinationPlayfieldProxyId", -1)) in placements_by_playfield
            }
        )
        if len(proxy_candidates) == 1:
            mapping["candidateBasePlayfieldId"] = proxy_candidates[0]
            mapping["candidateBaseBasis"] = "packet-runtime-to-destination-playfield-proxy"
            mapping["candidateBaseEvidenceClass"] = EVIDENCE_CORROBORATING
        elif len(proxy_candidates) > 1:
            mapping["mappingStatus"] = "conflict"
            mapping["mappingProven"] = False
            mapping["conflicts"] = list(mapping.get("conflicts", [])) + [
                "runtime playfield is paired with multiple destination playfield proxies"
            ]
        candidate_playfield = (
            optional_int(mapping.get("basePlayfieldResourceId"))
            if mapping.get("mappingProven")
            else optional_int(mapping.get("candidateBasePlayfieldId"))
            if mapping.get("candidateBasePlayfieldId") is not None
            else observation.resource_playfield_id
        )
        cluster_id = observation_to_cluster[observation.observation_id]
        cluster_conflict = bool(cluster_by_id[cluster_id]["conflicts"])
        result = resolve_candidate_set(
            observation_resolution_input(observation),
            placements_by_playfield.get(candidate_playfield, []) if candidate_playfield is not None else [],
            mapping,
            production_transform,
            cluster_conflict=cluster_conflict,
        )
        formal_ids = sorted(
            {
                str(candidate["placementId"])
                for category in ("exactCandidates", "regionCandidates")
                for candidate in result.get(category, [])
                if candidate.get("placementId") is not None
            }
        )
        cluster_candidate_ids[cluster_id].update(formal_ids)
        resolution = {
            "observationId": observation.observation_id,
            "captureId": observation.capture_id,
            "clusterId": cluster_id,
            "runtimeIdentity": observation.identity,
            "name": observation.name,
            "resourcePlayfieldId": observation.resource_playfield_id,
            "runtimePlayfieldId": observation.runtime_playfield_id,
            "resolvedBasePlayfieldId": candidate_playfield,
            "runtimePosition": list(observation.position) if observation.position else None,
            **result,
            "candidatePlacementIds": formal_ids,
            "promotionReady": result["matchState"] == MATCH_UNIQUE,
        }
        rows.append(resolution)
        candidate_rows.append(
            {
                "observationId": observation.observation_id,
                "clusterId": cluster_id,
                "playfieldId": candidate_playfield,
                "coordinateHypothesis": production_transform.name,
                "coordinateHypothesisProven": False,
                "exactCandidates": result.get("exactCandidates", []),
                "spawnRegionCandidates": result.get("regionCandidates", []),
                "nearestDiagnostics": result.get("nearestCandidates", []),
                "candidateEliminations": result.get("candidateEliminations", []),
                "formalCandidateCount": len(formal_ids),
            }
        )
    for cluster in clusters:
        cluster["candidatePlacements"] = sorted(cluster_candidate_ids.get(cluster["clusterId"], set()))
    return rows, candidate_rows


def category_state(observation: harvester.NpcObservation, category: str) -> str:
    if category == "appearance":
        statuses = [
            observation.fields.get(name, {}).get("status", "not observed")
            for name in ("headMesh", "textures", "meshes")
        ]
        if "conflict" in statuses:
            return "conflict"
        captured = sum(status == "captured" for status in statuses)
        return "captured" if captured == len(statuses) else "partial" if captured else "not-observed"
    if category == "stat":
        statuses = {row.get("status") for row in observation.stat_observations}
        if "conflict" in statuses:
            return "conflict"
        if "captured" in statuses:
            return "captured"
        return "not-observed"
    key = "corpseDeath" if category == "lifecycle" and observation.category_evidence.get("corpseDeath") else category
    return "captured" if observation.category_evidence.get(key, False) else "not-observed"


def build_promotion_eligibility(
    observations: Sequence[harvester.NpcObservation],
    resolutions: Sequence[Mapping[str, Any]],
) -> list[dict[str, Any]]:
    observations_by_id = {row.observation_id: row for row in observations}
    rows: list[dict[str, Any]] = []
    for resolution in resolutions:
        observation = observations_by_id[str(resolution["observationId"])]
        unique = resolution.get("matchState") == MATCH_UNIQUE
        placement_ids = resolution.get("candidatePlacementIds", [])
        rows.append(
            {
                "observationId": resolution.get("observationId"),
                "placementId": placement_ids[0] if unique and len(placement_ids) == 1 else None,
                "clusterId": resolution.get("clusterId"),
                "matchState": resolution.get("matchState"),
                "identityEvidence": resolution.get("identityEvidence", []),
                "appearanceState": category_state(observation, "appearance"),
                "statState": category_state(observation, "stat"),
                "combatState": category_state(observation, "combat"),
                "movementState": category_state(observation, "movement"),
                "lifecycleState": category_state(observation, "lifecycle"),
                "lootState": category_state(observation, "loot"),
                "respawnState": category_state(observation, "respawn"),
                "promotionReady": unique,
                "blockingReason": None if unique else resolution.get("blockingReason"),
            }
        )
    return rows


def texture_ids(observation: harvester.NpcObservation) -> list[int]:
    values = field_value(observation, "textures")
    if not isinstance(values, list):
        return []
    return [int(row["id"]) for row in values if row.get("id") not in (None, 0)]


def borealis_subject_result(
    name: str,
    expected_head: int,
    expected_textures: Sequence[int],
    observations: Sequence[harvester.NpcObservation],
    resolutions_by_id: Mapping[str, Mapping[str, Any]],
    candidates_by_id: Mapping[str, Mapping[str, Any]],
    position_history: Mapping[str, Sequence[Mapping[str, Any]]],
) -> dict[str, Any]:
    subjects = sorted(
        [
            observation
            for observation in observations
            if observation.resource_playfield_id == 3081 and observation.name == name
        ],
        key=lambda observation: observation.observation_id,
    )
    details: list[dict[str, Any]] = []
    for observation in subjects:
        resolution = resolutions_by_id[observation.observation_id]
        candidates = candidates_by_id[observation.observation_id]
        details.append(
            {
                "observationId": observation.observation_id,
                "runtimeIdentity": observation.identity,
                "captureStartResourcePlayfieldId": observation.resource_playfield_id,
                "runtimePlayfieldId": observation.runtime_playfield_id,
                "candidateBasePlayfieldId": resolution.get("resolvedBasePlayfieldId"),
                "runtimePositions": [row["position"] for row in position_history.get(observation.observation_id, [])]
                or ([list(observation.position)] if observation.position else []),
                "headMesh": field_value(observation, "headMesh"),
                "textures": texture_ids(observation),
                "appearanceExpected": {
                    "headMesh": expected_head,
                    "textures": list(expected_textures),
                },
                "appearancePreserved": (
                    field_value(observation, "headMesh") == expected_head
                    and texture_ids(observation) == list(expected_textures)
                ),
                "matchState": resolution["matchState"],
                "formalCandidateCount": candidates["formalCandidateCount"],
                "normalizedOfficialCandidatePositions": [
                    {
                        "placementId": candidate.get("placementId"),
                        "sourcePosition": candidate.get("sourcePosition"),
                        "distance": candidate.get("distance"),
                        "districtId": candidate.get("districtId"),
                        "radius": candidate.get("radius"),
                        "templateId": candidate.get("templateId"),
                        "monsterDataIfProven": candidate.get("monsterDataIfProven"),
                    }
                    for candidate in candidates["nearestDiagnostics"]
                ],
                "officialAppearanceComparison": "official placement structure contains no proven appearance fields",
                "identityConclusion": resolution["blockingReason"],
            }
        )
    return {
        "name": name,
        "observationCount": len(subjects),
        "unique": any(row["matchState"] == MATCH_UNIQUE for row in details),
        "details": details,
    }


def playfield_result(
    playfield_id: int,
    observations: Sequence[harvester.NpcObservation],
    resolutions: Sequence[Mapping[str, Any]],
) -> dict[str, Any]:
    ids = {
        observation.observation_id
        for observation in observations
        if observation.resource_playfield_id == playfield_id
    }
    selected = [row for row in resolutions if row.get("observationId") in ids]
    statuses = Counter(str(row.get("matchState")) for row in selected)
    return {
        "playfieldId": playfield_id,
        "observations": len(selected),
        "clusters": len({row.get("clusterId") for row in selected}),
        "uniqueProven": statuses[MATCH_UNIQUE],
        "ambiguous": statuses[MATCH_AMBIGUOUS],
        "unmatched": statuses[MATCH_UNMATCHED],
        "conflicts": statuses[MATCH_CONFLICT],
    }


def population_results(
    repo_root: Path,
    observations: Sequence[harvester.NpcObservation],
    resolutions: Sequence[Mapping[str, Any]],
    candidate_rows: Sequence[Mapping[str, Any]],
    position_history: Mapping[str, Sequence[Mapping[str, Any]]],
    placements: Sequence[Mapping[str, Any]],
) -> dict[str, Any]:
    resolutions_by_id = {str(row["observationId"]): row for row in resolutions}
    candidates_by_id = {str(row["observationId"]): row for row in candidate_rows}
    guide = borealis_subject_result(
        "Guide", 40635, (42239, 42260, 42240, 42261), observations,
        resolutions_by_id, candidates_by_id, position_history,
    )
    guard = borealis_subject_result(
        "Guard", 40111, (30848, 42260, 30831, 42261), observations,
        resolutions_by_id, candidates_by_id, position_history,
    )
    count_by_playfield = Counter(
        observation.resource_playfield_id
        for observation in observations
        if observation.resource_playfield_id is not None
    )
    additional = [
        playfield
        for playfield, _ in sorted(count_by_playfield.items(), key=lambda row: (-row[1], row[0]))
        if playfield not in {3081, 4582}
    ][:3]
    pf4582_source = harvester.load_json(repo_root / "docs/reference/pf4582/PlayfieldDistrictInfo.json")
    pf4582_runtime = harvester.load_json(repo_root / "docs/reference/pf4582/runtime-evidence-map.json")
    pf4582_report = harvester.load_json(repo_root / "docs/generated/pf4582_authoritative_placement_report.json")
    pf4582_placements = [
        placement for placement in placements if optional_int(placement.get("playfieldId")) == 4582
    ]
    current_authorized = sum(
        placement.get("sourceRecord", {}).get("RuntimeActivationAuthorized") is True
        for placement in pf4582_placements
    )
    pf4582 = playfield_result(4582, observations, resolutions)
    pf4582.update(
        {
            "officialPlacementRecords": sum(
                optional_int(placement.get("playfieldId")) == 4582 for placement in placements
            ),
            "acceptedSpecializedPlacements": len(
                pf4582_source.get("4582", {}).get("Spawns", [])
            ),
            "specializedRuntimeMappings": len(pf4582_runtime.get("runtimeMappings", [])),
            "runtimeGateAudit": {
                "historicalAcceptedClaim": {
                    "active": 25,
                    "blocked": 181,
                    "source": "docs/evidence/PF4582_OFFICIAL_SOURCE_RECONCILIATION_20260825.md",
                },
                "currentSpecializedCatalog": {
                    "active": optional_int(pf4582_report.get("PF4582_RUNTIME_ELIGIBLE")),
                    "blocked": optional_int(pf4582_report.get("PF4582_RUNTIME_BLOCKED")),
                    "explicitActive": optional_int(
                        pf4582_report.get("PF4582_EXPLICIT_RUNTIME_ACTIVE")
                    ),
                    "generatedProfileActive": optional_int(
                        pf4582_report.get("PF4582_GENERATED_PROFILE_ACTIVE")
                    ),
                    "source": "docs/generated/pf4582_authoritative_placement_report.json",
                },
                "currentOfficial207Overlay": {
                    "authorized": current_authorized,
                    "blocked": len(pf4582_placements) - current_authorized,
                },
                "claimMatchesCurrentBaseline": (
                    optional_int(pf4582_report.get("PF4582_RUNTIME_ELIGIBLE")) == 25
                    and optional_int(pf4582_report.get("PF4582_RUNTIME_BLOCKED")) == 181
                ),
                "identityBridgeEffect": "none",
            },
            "bridgeConclusion": (
                "The specialized SourceNpcId/profile overlay governs AORebirth definitions; it does not "
                "join a captured SimpleChar identity to an original official placement. ACGHash remains excluded."
            ),
        }
    )
    return {
        "borealis": {
            "playfield": playfield_result(3081, observations, resolutions),
            "guide": guide,
            "guard": guard,
        },
        "pf4582": pf4582,
        "additionalCaptureRichPlayfields": [
            playfield_result(playfield, observations, resolutions) for playfield in additional
        ],
    }


def write_outputs(
    output_dir: Path,
    *,
    official_payload: Mapping[str, Any],
    coordinate_payload: Mapping[str, Any],
    runtime_mapping_payload: Mapping[str, Any],
    clusters: Sequence[Mapping[str, Any]],
    candidate_rows: Sequence[Mapping[str, Any]],
    resolutions: Sequence[Mapping[str, Any]],
    promotions: Sequence[Mapping[str, Any]],
) -> None:
    unique = [row for row in resolutions if row.get("matchState") == MATCH_UNIQUE]
    ambiguous = [row for row in resolutions if row.get("matchState") == MATCH_AMBIGUOUS]
    unmatched = [row for row in resolutions if row.get("matchState") == MATCH_UNMATCHED]
    conflicts = [row for row in resolutions if row.get("matchState") == MATCH_CONFLICT]
    atomic_json(output_dir / "official-placement-expanded.json", official_payload)
    atomic_json(output_dir / "coordinate-transform-analysis.json", coordinate_payload)
    atomic_json(output_dir / "runtime-playfield-mapping.json", runtime_mapping_payload)
    atomic_json(
        output_dir / "observation-clusters.json",
        {"schemaVersion": SCHEMA_VERSION, "clusters": list(clusters)},
    )
    atomic_json(
        output_dir / "placement-candidates.json",
        {"schemaVersion": SCHEMA_VERSION, "candidates": list(candidate_rows)},
    )
    atomic_json(
        output_dir / "placement-resolution.json",
        {"schemaVersion": SCHEMA_VERSION, "resolutions": list(resolutions)},
    )
    atomic_json(
        output_dir / "unique-proven.json",
        {"schemaVersion": SCHEMA_VERSION, "matches": unique},
    )
    atomic_json(
        output_dir / "ambiguous.json",
        {"schemaVersion": SCHEMA_VERSION, "matches": ambiguous},
    )
    atomic_json(
        output_dir / "unmatched.json",
        {"schemaVersion": SCHEMA_VERSION, "matches": unmatched},
    )
    atomic_json(
        output_dir / "conflicts.json",
        {"schemaVersion": SCHEMA_VERSION, "matches": conflicts},
    )
    atomic_json(
        output_dir / "promotion-eligibility.json",
        {"schemaVersion": SCHEMA_VERSION, "records": list(promotions)},
    )


def run(args: argparse.Namespace) -> dict[str, Any]:
    repo_root = args.repo_root.resolve()
    output_dir = args.output_dir if args.output_dir.is_absolute() else repo_root / args.output_dir
    previous_summary = harvester.load_json(output_dir / "summary.json")
    previous_digest = previous_summary.get("deterministicDigest")

    manifest, resources, placements, field_inventory = load_official_corpus(repo_root)
    official_payload = official_provenance_payload(manifest, resources, placements, field_inventory)
    records = [record for record in harvester.inventory_records(repo_root) if record.accepted]
    observations, stat_metrics = harvester.harvest_observations(records, repo_root)
    position_history = load_capture_position_history(records)
    runtime_mapping_payload, mapping_by_capture = build_runtime_playfield_mapping(
        records, observations, official_resource_instances(manifest, resources)
    )
    clusters, observation_to_cluster = build_observation_clusters(observations, position_history)
    coordinate_payload = analyze_coordinate_transforms(observations, placements, mapping_by_capture)
    resolutions, candidate_rows = resolve_observations(
        observations, placements, mapping_by_capture, clusters, observation_to_cluster
    )
    promotions = build_promotion_eligibility(observations, resolutions)
    populations = population_results(
        repo_root,
        observations,
        resolutions,
        candidate_rows,
        position_history,
        placements,
    )
    write_outputs(
        output_dir,
        official_payload=official_payload,
        coordinate_payload=coordinate_payload,
        runtime_mapping_payload=runtime_mapping_payload,
        clusters=clusters,
        candidate_rows=candidate_rows,
        resolutions=resolutions,
        promotions=promotions,
    )
    digest = normalized_digest(output_dir)
    statuses = Counter(str(row.get("matchState")) for row in resolutions)
    guide_unique = bool(populations["borealis"]["guide"]["unique"])
    guard_unique = bool(populations["borealis"]["guard"]["unique"])
    pf4582_unique = int(populations["pf4582"]["uniqueProven"])
    promotion_ready = sum(bool(row.get("promotionReady")) for row in promotions)
    if promotion_ready != statuses[MATCH_UNIQUE]:
        raise ResolverError("Promotion eligibility count diverges from unique-proven identity count.")
    heuristic_promoted = sum(
        bool(row.get("promotionReady"))
        and any(evidence.get("class") == EVIDENCE_HEURISTIC for evidence in row.get("identityEvidence", []))
        for row in promotions
    )
    summary = {
        "schemaVersion": SCHEMA_VERSION,
        "placementIdentityResolverImplemented": True,
        "officialPlacementCount": len(placements),
        "officialFieldsExpanded": len(field_inventory),
        "districtTransformDecoded": False,
        "coordinateSystemProven": False,
        "runtimeBasePlayfieldMappingProven": (
            "PARTIAL_CAPTURE_SCOPED"
            if runtime_mapping_payload.get("provenCaptureMappings", 0)
            else "NO"
        ),
        "provenCaptureMappings": runtime_mapping_payload.get("provenCaptureMappings", 0),
        "observationsAnalyzed": len(observations),
        "observationClusters": len(clusters),
        "clusterMetrics": {
            "repeatedObservationClusters": sum(row["observationCount"] > 1 for row in clusters),
            "multiCaptureClusters": sum(row["captureCount"] > 1 for row in clusters),
            "stablePositionClusters": sum(row["stablePosition"] is not None for row in clusters),
            "conflictingClusters": sum(bool(row["conflicts"]) for row in clusters),
            "maximumObservationCount": max((row["observationCount"] for row in clusters), default=0),
            "maximumCaptureCount": max((row["captureCount"] for row in clusters), default=0),
        },
        "uniqueProvenMatches": statuses[MATCH_UNIQUE],
        "ambiguousMatches": statuses[MATCH_AMBIGUOUS],
        "unmatchedObservations": statuses[MATCH_UNMATCHED],
        "conflictingMatches": statuses[MATCH_CONFLICT],
        "borealisGuideUnique": guide_unique,
        "borealisGuardUnique": guard_unique,
        "pf4582UniqueMatches": pf4582_unique,
        "heuristicMatchesPromoted": heuristic_promoted,
        "acgHashUsedAsRuntimeIdentity": False,
        "runtimeNpcDefinitionsModified": False,
        "promotionEligibilityGenerated": len(promotions),
        "promotionReady": promotion_ready,
        "deterministicRepeatRun": previous_digest == digest if previous_digest else False,
        "tests": args.tests_status,
        "commit": args.commit,
        "deterministicDigest": digest,
        "acceptedCapturesProcessed": len(records),
        "positionHistoryRows": sum(len(rows) for rows in position_history.values()),
        "statMetrics": stat_metrics,
        "populationResults": populations,
        "sourceConclusion": {
            "resourceHierarchy": (
                "ResourceDatabase type/instance -> PlayfieldDistrictInfo_t -> DistrictData_t -> "
                "HashSpawnPoint_t -> ACGHash_t"
            ),
            "placementIdentifiers": (
                "OfficialSpawnRecordId and OfficialDistrictId are deterministic normalized identifiers, "
                "not native Funcom runtime identifiers"
            ),
            "templateIndirection": (
                "No proven native template, archive, object, parent, spawn-group, path, patrol, or "
                "runtime-instance reference is exposed by the retained structure"
            ),
            "missingBridge": (
                "No placement-specific value is present in both the official record and a captured NPC. "
                "The coordinate relation cannot be promoted from unpaired nearest-neighbor metrics, and "
                "the upstream raw/parser corpus required to inspect omitted/opaque bytes is not tracked "
                "inside AORebirth."
            ),
        },
    }
    atomic_json(output_dir / "summary.json", summary)
    return summary


def machine_value(value: Any) -> str:
    if isinstance(value, bool):
        return "YES" if value else "NO"
    return str(value)


def main(argv: list[str] | None = None) -> int:
    try:
        summary = run(parse_args(argv))
    except (ResolverError, OSError, ValueError, KeyError, TypeError) as exception:
        print("PLACEMENT_IDENTITY_RESOLVER_IMPLEMENTED=NO")
        print("ERROR=" + str(exception))
        return 1
    fields = (
        ("PLACEMENT_IDENTITY_RESOLVER_IMPLEMENTED", "placementIdentityResolverImplemented"),
        ("OFFICIAL_PLACEMENT_COUNT", "officialPlacementCount"),
        ("OFFICIAL_FIELDS_EXPANDED", "officialFieldsExpanded"),
        ("DISTRICT_TRANSFORM_DECODED", "districtTransformDecoded"),
        ("COORDINATE_SYSTEM_PROVEN", "coordinateSystemProven"),
        ("RUNTIME_BASE_PLAYFIELD_MAPPING_PROVEN", "runtimeBasePlayfieldMappingProven"),
        ("OBSERVATIONS_ANALYZED", "observationsAnalyzed"),
        ("OBSERVATION_CLUSTERS", "observationClusters"),
        ("UNIQUE_PROVEN_MATCHES", "uniqueProvenMatches"),
        ("AMBIGUOUS_MATCHES", "ambiguousMatches"),
        ("UNMATCHED_OBSERVATIONS", "unmatchedObservations"),
        ("CONFLICTING_MATCHES", "conflictingMatches"),
        ("BOREALIS_GUIDE_UNIQUE", "borealisGuideUnique"),
        ("BOREALIS_GUARD_UNIQUE", "borealisGuardUnique"),
        ("PF4582_UNIQUE_MATCHES", "pf4582UniqueMatches"),
        ("HEURISTIC_MATCHES_PROMOTED", "heuristicMatchesPromoted"),
        ("ACGHASH_USED_AS_RUNTIME_IDENTITY", "acgHashUsedAsRuntimeIdentity"),
        ("RUNTIME_NPC_DEFINITIONS_MODIFIED", "runtimeNpcDefinitionsModified"),
        ("PROMOTION_ELIGIBILITY_GENERATED", "promotionEligibilityGenerated"),
        ("DETERMINISTIC_REPEAT_RUN", "deterministicRepeatRun"),
        ("TESTS", "tests"),
        ("COMMIT", "commit"),
    )
    for label, key in fields:
        print(label + "=" + machine_value(summary[key]))
    print("DETERMINISTIC_DIGEST=" + str(summary["deterministicDigest"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
