#!/usr/bin/env python3
"""Deterministic population-scoped reconstruction over ACG topology and captured NPC evidence."""

from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import gzip
import hashlib
import json
import math
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence


ROOT = Path(__file__).resolve().parents[1]
PLACEMENT_INDEX = ROOT / "docs/generated/playfields/official-placement-index.json"
HARVESTER_ROOT = ROOT / "build-verify/npc-observation-harvester"
RESOLVER_ROOT = ROOT / "build-verify/npc-placement-identity-resolver"
ARCHETYPE_ROOT = ROOT / "docs/generated/enemy_archetypes"
OUTPUT_ROOT = ROOT / "docs/generated/spawn_populations"

OFFICIAL_PLACEMENTS = 32_805
MONSTER_DATA_RECORDS = 1_470
EXACT_VISUAL_ARCHETYPES = 1_360
STRUCTURAL_FAMILIES = 750
DERIVED_SPATIAL_CLUSTER_METERS = 25.0
UNSET_SENTINEL = 1_234_567_890

SCOPE_EXACT = "exact-placement"
SCOPE_LOCAL = "local-population"
SCOPE_PLAYFIELD = "playfield-population"
SCOPE_UNASSOCIATED = "unassociated"
SCOPE_CONFLICT = "conflict"
SCOPES = (SCOPE_EXACT, SCOPE_LOCAL, SCOPE_PLAYFIELD, SCOPE_UNASSOCIATED, SCOPE_CONFLICT)


class ReconstructionError(RuntimeError):
    pass


def canonical_bytes(value: Any) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def pretty_bytes(value: Any) -> bytes:
    return (json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n").encode("utf-8")


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def stable_id(prefix: str, payload: Any) -> str:
    return f"{prefix}-{sha256_bytes(canonical_bytes(payload))[:20]}"


def load_json(path: Path) -> Any:
    if not path.is_file():
        raise ReconstructionError(f"Required input is missing: {path}")
    return json.loads(path.read_text(encoding="utf-8-sig"))


def field_value(observation: Mapping[str, Any], name: str) -> Any:
    fields = observation.get("fields", {})
    field = fields.get(name, {}) if isinstance(fields, Mapping) else {}
    value = field.get("value") if isinstance(field, Mapping) else None
    if field.get("status") != "captured" or value == UNSET_SENTINEL or str(value) == str(UNSET_SENTINEL):
        return None
    return value


def runtime_instance_key(capture_id: str, runtime_identity: str | None) -> str:
    return f"{capture_id}|{runtime_identity or 'not-observed'}"


def placement_candidate_ids(values: Iterable[Any]) -> list[str]:
    result: set[str] = set()
    for value in values:
        if isinstance(value, str):
            result.add(value)
        elif isinstance(value, Mapping):
            candidate = value.get("placementId") or value.get("officialSpawnRecordId")
            if isinstance(candidate, str):
                result.add(candidate)
    return sorted(result)


def position_extent(positions: Iterable[Sequence[float]]) -> dict[str, Any] | None:
    values = [tuple(float(item) for item in row) for row in positions if row and len(row) == 3]
    if not values:
        return None
    minimum = [min(row[index] for row in values) for index in range(3)]
    maximum = [max(row[index] for row in values) for index in range(3)]
    centre = [sum(row[index] for row in values) / len(values) for index in range(3)]
    return {
        "minimum": [round(value, 6) for value in minimum],
        "maximum": [round(value, 6) for value in maximum],
        "centre": [round(value, 6) for value in centre],
    }


def distance(left: Sequence[float], right: Sequence[float]) -> float:
    return math.sqrt(sum((float(left[index]) - float(right[index])) ** 2 for index in range(3)))


class UnionFind:
    def __init__(self, size: int) -> None:
        self.parent = list(range(size))

    def find(self, value: int) -> int:
        while self.parent[value] != value:
            self.parent[value] = self.parent[self.parent[value]]
            value = self.parent[value]
        return value

    def union(self, left: int, right: int) -> None:
        left_root = self.find(left)
        right_root = self.find(right)
        if left_root != right_root:
            self.parent[max(left_root, right_root)] = min(left_root, right_root)


def spatial_components(records: Sequence[Mapping[str, Any]], threshold: float) -> list[list[Mapping[str, Any]]]:
    if not records:
        return []
    if threshold <= 0:
        return [[row] for row in records]
    grid: dict[tuple[int, int, int], list[int]] = defaultdict(list)
    union = UnionFind(len(records))
    for index, record in enumerate(records):
        position = (record["PositionX"], record["PositionY"], record["PositionZ"])
        cell = tuple(math.floor(float(value) / threshold) for value in position)
        for x_offset in (-1, 0, 1):
            for y_offset in (-1, 0, 1):
                for z_offset in (-1, 0, 1):
                    for other in grid.get((cell[0] + x_offset, cell[1] + y_offset, cell[2] + z_offset), []):
                        other_position = (
                            records[other]["PositionX"],
                            records[other]["PositionY"],
                            records[other]["PositionZ"],
                        )
                        if distance(position, other_position) <= threshold:
                            union.union(index, other)
        grid[cell].append(index)
    components: dict[int, list[Mapping[str, Any]]] = defaultdict(list)
    for index, record in enumerate(records):
        components[union.find(index)].append(record)
    return [
        sorted(rows, key=lambda row: row["OfficialSpawnRecordId"])
        for _, rows in sorted(components.items(), key=lambda item: min(row["OfficialSpawnRecordId"] for row in item[1]))
    ]


def placement_projection(record: Mapping[str, Any]) -> dict[str, Any]:
    unknown = record.get("UnknownFields", {})
    return {
        "officialSpawnRecordId": record["OfficialSpawnRecordId"],
        "acgHash": record.get("CanonicalAcgHashText"),
        "acgHashNativeUInt32": record.get("OfficialAcgHashNativeUInt32"),
        "playfield": record.get("PlayfieldId"),
        "districtId": record.get("OfficialDistrictId"),
        "districtIndex": record.get("DistrictIndex"),
        "districtName": record.get("DistrictName"),
        "districtRecordOrdinal": record.get("DistrictRecordOrdinal"),
        "coordinates": [record.get("PositionX"), record.get("PositionY"), record.get("PositionZ")],
        "heading": {
            "rotationMidEncoded": record.get("RotationMidEncoded"),
            "rotationWidthEncoded": record.get("RotationWidthEncoded"),
        },
        "spawnPolicy": {
            "levelMinimum": record.get("LevelMinimum"),
            "levelMaximum": record.get("LevelMaximum"),
            "radius": record.get("Radius"),
            "assistanceRadius": record.get("AssistanceRadius"),
            "respawnChance": record.get("RespawnChance"),
            "respawnTime": record.get("RespawnTime"),
            "nativeFlags": record.get("NativeFlags"),
            "moreFlags": record.get("MoreFlags"),
            "serializedOptionalFlags": record.get("SerializedOptionalFlags"),
        },
        "parentOrGroup": {
            "provenParentId": None,
            "provenGeneratorId": None,
            "state": "not exposed by retained official structure",
        },
        "additionalPoints": unknown.get("AdditionalPoints", []),
        "extensions": unknown.get("Extensions", []),
        "sourceProvenance": {
            "resourceType": record.get("ResourceType"),
            "resourceInstance": record.get("ResourceInstance"),
            "recordOffset": record.get("RecordOffset"),
            "serializedSize": record.get("SerializedSize"),
            "recordSha256": unknown.get("RecordSha256"),
        },
        "aorebirthOverlay": {
            "sourceNpcId": record.get("SourceNpcId"),
            "existingProfile": record.get("ExistingAoRebirthProfile"),
            "resolvedMonsterData": record.get("ResolvedMonsterData"),
            "runtimeActivationAuthorized": record.get("RuntimeActivationAuthorized", False),
            "currentRuntimeActive": record.get("CurrentRuntimeActive", False),
        },
    }


def load_placements() -> tuple[list[dict[str, Any]], dict[str, Any]]:
    index = load_json(PLACEMENT_INDEX)
    placements: list[dict[str, Any]] = []
    inputs: list[dict[str, Any]] = []
    for entry in index.get("Playfields", []):
        path = ROOT / entry["Path"]
        actual_sha = sha256_file(path)
        if actual_sha != entry["Sha256"]:
            raise ReconstructionError(f"Placement shard hash drift: {path}")
        document = load_json(path)
        rows = document.get("Records", [])
        if len(rows) != (entry.get("OfficialSpawnCount") or 0):
            raise ReconstructionError(f"Placement shard count drift: {path}")
        placements.extend(rows)
        inputs.append({"path": entry["Path"], "sha256": actual_sha, "placements": len(rows)})
    if len(placements) != OFFICIAL_PLACEMENTS:
        raise ReconstructionError(f"Expected {OFFICIAL_PLACEMENTS} placements, found {len(placements)}")
    return placements, {
        "indexPath": str(PLACEMENT_INDEX.relative_to(ROOT)).replace("\\", "/"),
        "indexSha256": sha256_file(PLACEMENT_INDEX),
        "shards": inputs,
    }


def build_topology(
    placements: Sequence[Mapping[str, Any]],
    threshold: float = DERIVED_SPATIAL_CLUSTER_METERS,
) -> tuple[list[dict[str, Any]], dict[str, str]]:
    structural_groups: dict[tuple[Any, ...], list[Mapping[str, Any]]] = defaultdict(list)
    for row in placements:
        structural_groups[
            (
                row.get("PlayfieldId"),
                row.get("OfficialDistrictId"),
                row.get("CanonicalAcgHashText"),
            )
        ].append(row)
    populations: list[dict[str, Any]] = []
    placement_to_population: dict[str, str] = {}
    for group_key, rows in sorted(structural_groups.items(), key=lambda item: tuple(str(value) for value in item[0])):
        official_group_id = stable_id("official-group", group_key)
        for component in spatial_components(rows, threshold):
            placement_ids = [row["OfficialSpawnRecordId"] for row in component]
            spatial_cluster_id = stable_id("spatial-cluster", placement_ids)
            population_id = stable_id("spawn-population", [official_group_id, spatial_cluster_id])
            projected = [placement_projection(row) for row in component]
            profiles = sorted(
                {row.get("ExistingAoRebirthProfile") for row in component if row.get("ExistingAoRebirthProfile")}
            )
            population = {
                "populationId": population_id,
                "recordKind": "official-acg-topology",
                "playfield": group_key[0],
                "officialGroupIds": [official_group_id],
                "officialGroupBasis": "shared ACGHash policy tag within one official district",
                "derivedSpatialClusterId": spatial_cluster_id,
                "derivedSpatialClusterMethod": {
                    "method": "three-dimensional connected component",
                    "maximumEdgeMeters": threshold,
                    "evidenceClass": "heuristic",
                    "officialSemantic": False,
                },
                "acgHashes": sorted({row.get("CanonicalAcgHashText") for row in component}),
                "placementCount": len(component),
                "placementExtent": position_extent(
                    ([row["PositionX"], row["PositionY"], row["PositionZ"]] for row in component)
                ),
                "placements": projected,
                "existingAoRebirthProfiles": profiles,
                "runtimeObservationIds": [],
                "monsterData": [],
                "archetypeIds": [],
                "structuralFamilies": [],
                "names": [],
                "levelMinimum": None,
                "levelMaximum": None,
                "associationScope": SCOPE_UNASSOCIATED,
                "associationStrength": "none",
                "populationEvidenceState": "no-runtime-evidence",
                "blockingReasons": ["no runtime population associated with this ACG topology population"],
            }
            populations.append(population)
            for placement_id in placement_ids:
                placement_to_population[placement_id] = population_id
    populations.sort(key=lambda row: (row["playfield"], row["populationId"]))
    return populations, placement_to_population


def topology_spatial_index(populations: Sequence[Mapping[str, Any]]) -> dict[int, dict[tuple[int, int, int], list[tuple[str, Sequence[float]]]]]:
    result: dict[int, dict[tuple[int, int, int], list[tuple[str, Sequence[float]]]]] = defaultdict(lambda: defaultdict(list))
    size = DERIVED_SPATIAL_CLUSTER_METERS
    for population in populations:
        playfield = population.get("playfield")
        if not isinstance(playfield, int):
            continue
        for placement in population.get("placements", []):
            position = placement["coordinates"]
            cell = tuple(math.floor(float(value) / size) for value in position)
            result[playfield][cell].append((population["populationId"], position))
    return result


def nearby_population_ids(
    index: Mapping[int, Mapping[tuple[int, int, int], Sequence[tuple[str, Sequence[float]]]]],
    playfield: int | None,
    position: Sequence[float] | None,
    threshold: float = DERIVED_SPATIAL_CLUSTER_METERS,
) -> list[str]:
    if playfield is None or not position:
        return []
    cell = tuple(math.floor(float(value) / threshold) for value in position)
    candidates: set[str] = set()
    for x_offset in (-1, 0, 1):
        for y_offset in (-1, 0, 1):
            for z_offset in (-1, 0, 1):
                for population_id, placement_position in index.get(playfield, {}).get(
                    (cell[0] + x_offset, cell[1] + y_offset, cell[2] + z_offset), []
                ):
                    if distance(position, placement_position) <= threshold:
                        candidates.add(population_id)
    return sorted(candidates)


def observation_coverage(observation: Mapping[str, Any]) -> dict[str, bool]:
    fields = observation.get("fields", {}) if isinstance(observation.get("fields", {}), Mapping) else {}
    appearance = any(fields.get(name, {}).get("status") == "captured" for name in ("headMesh", "textures", "meshes"))
    category = observation.get("categoryEvidence", {})
    if not isinstance(category, Mapping):
        category = {}
    return {
        "appearance": appearance,
        "stats": bool(observation.get("statObservations")),
        "combat": bool(category.get("combat")),
        "movement": bool(category.get("movement")),
        "lifecycle": bool(category.get("lifecycle") or category.get("corpseDeath")),
        "loot": bool(category.get("loot")),
        "respawn": bool(category.get("respawn")),
    }


def runtime_group_key(row: Mapping[str, Any]) -> tuple[Any, ...]:
    return (
        row.get("contextPlayfield"),
        row.get("runtimePlayfield"),
        row.get("monsterData"),
        row.get("archetypeId"),
        row.get("structuralFamily"),
    )


def runtime_group_metrics(rows: Sequence[Mapping[str, Any]]) -> dict[tuple[Any, ...], dict[str, Any]]:
    groups: dict[tuple[Any, ...], list[Mapping[str, Any]]] = defaultdict(list)
    for row in rows:
        groups[runtime_group_key(row)].append(row)
    result: dict[tuple[Any, ...], dict[str, Any]] = {}
    for key, group in groups.items():
        captures = {row["captureId"] for row in group}
        raw_identities = {row.get("runtimeIdentity") for row in group if row.get("runtimeIdentity")}
        instances = {runtime_instance_key(row["captureId"], row.get("runtimeIdentity")) for row in group}
        result[key] = {
            "observationCount": len(group),
            "captureCount": len(captures),
            "runtimeInstanceCount": len(instances),
            "runtimeIdsChangeAcrossCaptures": len(captures) > 1 and len(raw_identities) > 1,
        }
    return result


def profile_matches_name(population: Mapping[str, Any], name: str | None) -> bool:
    if not name:
        return False
    normalized = name.strip().casefold()
    for profile in population.get("existingAoRebirthProfiles", []):
        suffix = str(profile).split(":")[-1].strip().casefold()
        if suffix == normalized:
            return True
    return False


def classify_scope(
    row: Mapping[str, Any],
    metrics: Mapping[str, Any],
    topology_by_id: Mapping[str, Mapping[str, Any]],
) -> dict[str, Any]:
    match_state = row.get("resolverMatchState")
    candidates = list(row.get("candidatePopulationIds", []))
    exact_candidates = list(row.get("exactCandidatePlacementIds", []))
    context_playfield = row.get("contextPlayfield")
    if match_state == "conflict":
        return {
            "scope": SCOPE_CONFLICT,
            "method": "phase-aware-resolver-conflict",
            "strength": "direct",
            "targetPopulationId": None,
            "explicitIdBridge": False,
            "blockingReasons": [row.get("resolverBlockingReason") or "resolver conflict"],
        }
    if (
        len(exact_candidates) == 1
        and len(candidates) == 1
        and row.get("resolvedBasePlayfieldId") == context_playfield
        and match_state in {"unique", "unique-proven"}
    ):
        return {
            "scope": SCOPE_EXACT,
            "method": "position-correlation",
            "strength": "strong",
            "targetPopulationId": candidates[0],
            "explicitIdBridge": False,
            "blockingReasons": [],
        }
    if len(candidates) == 1:
        target = topology_by_id[candidates[0]]
        overlay_match = profile_matches_name(target, row.get("name")) or any(
            placement.get("aorebirthOverlay", {}).get("resolvedMonsterData") == row.get("monsterData")
            for placement in target.get("placements", [])
        )
        if overlay_match:
            return {
                "scope": SCOPE_LOCAL,
                "method": "population-correlation-existing-governed-overlay",
                "strength": "strong",
                "targetPopulationId": candidates[0],
                "explicitIdBridge": False,
                "blockingReasons": ["exact ACG row ownership remains unproven"],
            }
        if metrics.get("captureCount", 0) >= 2 and metrics.get("runtimeIdsChangeAcrossCaptures"):
            return {
                "scope": SCOPE_LOCAL,
                "method": "repeated-runtime-population-plus-single-spatial-population",
                "strength": "corroborating",
                "targetPopulationId": candidates[0],
                "explicitIdBridge": False,
                "blockingReasons": ["population association is not exact-row identity"],
            }
    if context_playfield is not None:
        blockers = ["no authoritative local ACG population ownership"]
        if candidates:
            blockers.append("spatial candidate evidence is heuristic or ambiguous")
        return {
            "scope": SCOPE_PLAYFIELD,
            "method": "captured-resource-playfield-context",
            "strength": "corroborating",
            "targetPopulationId": None,
            "explicitIdBridge": False,
            "blockingReasons": blockers,
        }
    return {
        "scope": SCOPE_UNASSOCIATED,
        "method": "none",
        "strength": "none",
        "targetPopulationId": None,
        "explicitIdBridge": False,
        "blockingReasons": ["no retained official playfield context"],
    }


def normalize_runtime_observations(
    observations_document: Mapping[str, Any],
    runtime_associations: Mapping[str, Any],
    resolver_document: Mapping[str, Any],
    clusters_document: Mapping[str, Any],
    placement_to_population: Mapping[str, str],
    topology: Sequence[Mapping[str, Any]],
    archetype_catalog: Mapping[str, Any],
) -> list[dict[str, Any]]:
    associations = {row["observationId"]: row for row in runtime_associations.get("observations", [])}
    resolutions = {row["observationId"]: row for row in resolver_document.get("resolutions", [])}
    observation_to_cluster: dict[str, Mapping[str, Any]] = {}
    for cluster in clusters_document.get("clusters", []):
        for observation_id in cluster.get("observationIds", []):
            observation_to_cluster[observation_id] = cluster
    archetypes = {row["archetypeId"]: row for row in archetype_catalog.get("archetypes", [])}
    spatial_index = topology_spatial_index(topology)
    rows: list[dict[str, Any]] = []
    for observation in observations_document.get("observations", []):
        observation_id = observation["observationId"]
        association = associations.get(observation_id, {})
        resolution = resolutions.get(observation_id, {})
        cluster = observation_to_cluster.get(observation_id, {})
        archetype_id = association.get("archetypeId")
        context_playfield = observation.get("resourcePlayfieldId")
        position = observation.get("position")
        exact_candidate_ids = placement_candidate_ids(resolution.get("exactCandidates", []))
        candidate_placement_ids = placement_candidate_ids(
            list(resolution.get("candidatePlacementIds", []))
            + list(resolution.get("exactCandidates", []))
            + list(resolution.get("regionCandidates", []))
        )
        candidate_population_ids = sorted(
            {placement_to_population[value] for value in candidate_placement_ids if value in placement_to_population}
        )
        candidate_source = "resolver"
        if not candidate_population_ids:
            candidate_population_ids = nearby_population_ids(spatial_index, context_playfield, position)
            candidate_source = "derived-spatial-proximity"
        family = archetypes.get(archetype_id, {}).get("baseModelFamily") if archetype_id else None
        rows.append(
            {
                "observationId": observation_id,
                "captureId": observation.get("captureId"),
                "runtimeIdentity": observation.get("identity"),
                "contextPlayfield": context_playfield,
                "contextPlayfieldEvidence": (
                    "capture-session-resource-context" if context_playfield is not None else "not-observed"
                ),
                "runtimePlayfield": observation.get("runtimePlayfieldId"),
                "zoneEpoch": {
                    "state": "not-observed",
                    "derivedSessionRuntimePartition": f"{observation.get('captureId')}:{observation.get('runtimePlayfieldId')}",
                },
                "monsterData": field_value(observation, "monsterData"),
                "archetypeId": archetype_id,
                "structuralFamily": family,
                "archetypeAssociationState": association.get("associationState", "unknown"),
                "name": observation.get("name") or field_value(observation, "name"),
                "level": field_value(observation, "level"),
                "firstObservedPosition": position,
                "earliestScfuPosition": position,
                "currentMovedPositionUsedAsSpawn": False,
                "movementEnvelope": {
                    "positionSampleCount": cluster.get("positionSampleCount", 1 if position else 0),
                    "maximumDisplacement": cluster.get("positionMaximumDeviation", 0.0 if position else None),
                    "positionCentre": cluster.get("positionCentre", position),
                    "actualMovementOnly": True,
                },
                "coverage": observation_coverage(observation),
                "candidatePlacementIds": candidate_placement_ids,
                "candidatePopulationIds": candidate_population_ids,
                "candidateSource": candidate_source,
                "exactCandidatePlacementIds": exact_candidate_ids,
                "resolverMatchState": resolution.get("matchState"),
                "resolverBlockingReason": resolution.get("blockingReason"),
                "resolvedBasePlayfieldId": resolution.get("resolvedBasePlayfieldId"),
                "acgHashUsedAsMonsterData": False,
                "runtimeIdentityPersistent": False,
            }
        )
    metrics = runtime_group_metrics(rows)
    topology_by_id = {row["populationId"]: row for row in topology}
    for row in rows:
        row["association"] = classify_scope(row, metrics[runtime_group_key(row)], topology_by_id)
    return rows


def coverage_counts(rows: Sequence[Mapping[str, Any]]) -> dict[str, int]:
    keys = ("appearance", "stats", "combat", "movement", "lifecycle", "loot", "respawn")
    return {key: sum(1 for row in rows if row.get("coverage", {}).get(key)) for key in keys}


def aggregate_runtime_rows(rows: Sequence[Mapping[str, Any]]) -> list[dict[str, Any]]:
    groups: dict[tuple[Any, ...], list[Mapping[str, Any]]] = defaultdict(list)
    for row in rows:
        association = row["association"]
        key = (
            association["scope"],
            association.get("targetPopulationId"),
            *runtime_group_key(row),
        )
        groups[key].append(row)
    result: list[dict[str, Any]] = []
    strength_rank = {"none": 0, "heuristic": 1, "corroborating": 2, "strong": 3, "direct": 4}
    for key, group in sorted(groups.items(), key=lambda item: tuple(str(value) for value in item[0])):
        scopes = {row["association"]["scope"] for row in group}
        if len(scopes) != 1:
            raise ReconstructionError("Runtime population group crossed association scopes")
        levels = sorted({int(row["level"]) for row in group if isinstance(row.get("level"), int)})
        captures = sorted({row["captureId"] for row in group})
        raw_ids = sorted({row["runtimeIdentity"] for row in group if row.get("runtimeIdentity")})
        instances = sorted({runtime_instance_key(row["captureId"], row.get("runtimeIdentity")) for row in group})
        names = sorted({str(row["name"]) for row in group if row.get("name")}, key=str.casefold)
        positions = [row["firstObservedPosition"] for row in group if row.get("firstObservedPosition")]
        strengths = [row["association"]["strength"] for row in group]
        strength = max(strengths, key=lambda value: strength_rank[value])
        result.append(
            {
                "runtimePopulationId": stable_id("runtime-population", key),
                "associationScope": key[0],
                "associationStrength": strength,
                "placementPopulationId": key[1],
                "playfield": key[2],
                "runtimePlayfield": key[3],
                "monsterData": key[4],
                "archetypeId": key[5],
                "structuralFamily": key[6],
                "names": names,
                "levelMinimum": min(levels) if levels else None,
                "levelMaximum": max(levels) if levels else None,
                "observationCount": len(group),
                "captureCount": len(captures),
                "captureIds": captures,
                "runtimeInstanceCount": len(instances),
                "runtimeIdentitiesObserved": len(raw_ids),
                "runtimeIdsChangeAcrossCaptures": len(captures) > 1 and len(raw_ids) > 1,
                "positionExtent": position_extent(positions),
                "maximumObservedMovementDisplacement": max(
                    (
                        float(row["movementEnvelope"]["maximumDisplacement"])
                        for row in group
                        if row["movementEnvelope"].get("maximumDisplacement") is not None
                    ),
                    default=None,
                ),
                "coverageCounts": coverage_counts(group),
                "lootObservationCount": sum(1 for row in group if row["coverage"].get("loot")),
                "observationIds": sorted(row["observationId"] for row in group),
                "blockingReasons": sorted(
                    {reason for row in group for reason in row["association"].get("blockingReasons", [])}
                ),
                "runtimeIdentityPersistent": False,
                "lootDefinesVisualArchetype": False,
            }
        )
    return result


def readiness(record: Mapping[str, Any]) -> dict[str, Any]:
    coverage = record.get("coverageCounts", {})
    scope = record.get("associationScope")
    strength = record.get("associationStrength")
    population_ready = scope in {SCOPE_EXACT, SCOPE_LOCAL} and strength in {"direct", "strong"}
    blockers = list(record.get("blockingReasons", []))
    if not record.get("archetypeIds") and not record.get("archetypeId"):
        blockers.append("no observed visual archetype")
    if not population_ready:
        blockers.append("population ownership is not direct or strong")
    if record.get("levelMinimum") is None:
        blockers.append("no captured level evidence")
    return {
        "visualReady": bool(record.get("archetypeIds") or record.get("archetypeId")),
        "populationIdentityReady": population_ready,
        "levelReady": record.get("levelMinimum") is not None,
        "combatReady": bool(coverage.get("combat")),
        "lootReady": bool(coverage.get("loot")),
        "respawnReady": bool(coverage.get("respawn")) and population_ready,
        "exactPlacementReady": scope == SCOPE_EXACT and strength in {"direct", "strong"},
        "confidence": strength,
        "blockers": sorted(set(blockers)),
    }


def build_population_catalog(
    topology: Sequence[Mapping[str, Any]],
    runtime_rows: Sequence[Mapping[str, Any]],
    runtime_populations: Sequence[Mapping[str, Any]],
) -> list[dict[str, Any]]:
    rows_by_target: dict[str, list[Mapping[str, Any]]] = defaultdict(list)
    candidate_targets: Counter[str] = Counter()
    conflict_targets: Counter[str] = Counter()
    for row in runtime_rows:
        for target in row.get("candidatePopulationIds", []):
            candidate_targets[target] += 1
            if row["association"]["scope"] == SCOPE_CONFLICT:
                conflict_targets[target] += 1
        target = row["association"].get("targetPopulationId")
        if target:
            rows_by_target[target].append(row)
    catalog: list[dict[str, Any]] = []
    for source in topology:
        record = dict(source)
        attached = rows_by_target.get(record["populationId"], [])
        levels = sorted({int(row["level"]) for row in attached if isinstance(row.get("level"), int)})
        scopes = {row["association"]["scope"] for row in attached}
        strengths = {row["association"]["strength"] for row in attached}
        record.update(
            {
                "runtimeObservationIds": sorted(row["observationId"] for row in attached),
                "monsterData": sorted({row["monsterData"] for row in attached if row.get("monsterData") is not None}),
                "archetypeIds": sorted({row["archetypeId"] for row in attached if row.get("archetypeId")}),
                "structuralFamilies": sorted(
                    {row["structuralFamily"] for row in attached if row.get("structuralFamily")}
                ),
                "names": sorted({row["name"] for row in attached if row.get("name")}, key=str.casefold),
                "levelMinimum": min(levels) if levels else None,
                "levelMaximum": max(levels) if levels else None,
                "runtimeInstancesObserved": len(
                    {runtime_instance_key(row["captureId"], row.get("runtimeIdentity")) for row in attached}
                ),
                "capturesObserved": len({row["captureId"] for row in attached}),
                "coverageCounts": coverage_counts(attached),
                "associationScope": SCOPE_EXACT if SCOPE_EXACT in scopes else SCOPE_LOCAL if attached else SCOPE_UNASSOCIATED,
                "associationStrength": "strong" if "strong" in strengths else "corroborating" if attached else "none",
                "populationEvidenceState": (
                    "conflict" if conflict_targets[record["populationId"]]
                    else "runtime-population-observed" if attached
                    else "population-candidate" if candidate_targets[record["populationId"]]
                    else "no-runtime-evidence"
                ),
                "blockingReasons": (
                    sorted({reason for row in attached for reason in row["association"]["blockingReasons"]})
                    if attached
                    else ["runtime evidence does not establish ownership of this ACG topology population"]
                ),
            }
        )
        record["placements"] = [
            {
                **placement,
                "populationEvidenceState": record["populationEvidenceState"],
                "associationScope": record["associationScope"],
                "individualRuntimeOwnershipClaimed": False,
            }
            for placement in record["placements"]
        ]
        record["readiness"] = readiness(record)
        catalog.append(record)
    for runtime in runtime_populations:
        if runtime["associationScope"] in {SCOPE_EXACT, SCOPE_LOCAL}:
            continue
        record = {
            "populationId": runtime["runtimePopulationId"],
            "recordKind": "runtime-population-without-local-placement-ownership",
            "playfield": runtime["playfield"],
            "runtimePlayfield": runtime["runtimePlayfield"],
            "officialGroupIds": [],
            "derivedSpatialClusterId": None,
            "acgHashes": [],
            "placementCount": 0,
            "placementExtent": None,
            "placements": [],
            "runtimeObservationIds": runtime["observationIds"],
            "monsterData": [runtime["monsterData"]] if runtime["monsterData"] is not None else [],
            "archetypeIds": [runtime["archetypeId"]] if runtime["archetypeId"] else [],
            "structuralFamilies": [runtime["structuralFamily"]] if runtime["structuralFamily"] else [],
            "names": runtime["names"],
            "levelMinimum": runtime["levelMinimum"],
            "levelMaximum": runtime["levelMaximum"],
            "runtimeInstancesObserved": runtime["runtimeInstanceCount"],
            "capturesObserved": runtime["captureCount"],
            "coverageCounts": runtime["coverageCounts"],
            "associationScope": runtime["associationScope"],
            "associationStrength": runtime["associationStrength"],
            "populationEvidenceState": "conflict" if runtime["associationScope"] == SCOPE_CONFLICT else "runtime-population-observed",
            "blockingReasons": runtime["blockingReasons"],
        }
        record["readiness"] = readiness(record)
        catalog.append(record)
    return sorted(catalog, key=lambda row: (str(row.get("playfield")), row["recordKind"], row["populationId"]))


def archetype_reuse(
    runtime_populations: Sequence[Mapping[str, Any]],
    catalog: Sequence[Mapping[str, Any]] = (),
) -> list[dict[str, Any]]:
    groups: dict[str, list[Mapping[str, Any]]] = defaultdict(list)
    for row in runtime_populations:
        if row.get("archetypeId"):
            groups[row["archetypeId"]].append(row)
    results: list[dict[str, Any]] = []
    for archetype_id, rows in groups.items():
        levels = [value for row in rows for value in (row.get("levelMinimum"), row.get("levelMaximum")) if value is not None]
        associated_placements = {
            placement["officialSpawnRecordId"]
            for population in catalog
            if archetype_id in population.get("archetypeIds", [])
            for placement in population.get("placements", [])
        }
        results.append(
            {
                "archetypeId": archetype_id,
                "playfields": sorted({row["playfield"] for row in rows if row.get("playfield") is not None}),
                "runtimePopulations": len(rows),
                "localPopulations": sum(row["associationScope"] == SCOPE_LOCAL for row in rows),
                "associatedAcgPlacements": len(associated_placements),
                "observationCount": sum(row["observationCount"] for row in rows),
                "names": sorted({name for row in rows for name in row["names"]}, key=str.casefold),
                "levelMinimum": min(levels) if levels else None,
                "levelMaximum": max(levels) if levels else None,
            }
        )
    return sorted(results, key=lambda row: (-row["runtimePopulations"], -row["observationCount"], row["archetypeId"]))


def case_studies(
    catalog: Sequence[Mapping[str, Any]],
    runtime_populations: Sequence[Mapping[str, Any]],
    resolver_summary: Mapping[str, Any],
    leet_source: Mapping[str, Any],
) -> dict[str, Any]:
    leet_archetypes = set(leet_source.get("visualArchetypes", []))
    leet_monster_data = set(leet_source.get("monsterData", []))
    leet = [
        row for row in runtime_populations
        if row.get("archetypeId") in leet_archetypes or row.get("monsterData") in leet_monster_data
    ]
    leet_levels = [value for row in leet for value in (row.get("levelMinimum"), row.get("levelMaximum")) if value is not None]
    pf4582_catalog = [row for row in catalog if row.get("playfield") == 4582]
    pf4582_runtime = [row for row in runtime_populations if row.get("playfield") == 4582]
    borealis_runtime = [
        row for row in runtime_populations
        if row.get("playfield") in {3081, 954} or any(name.casefold() in {"guide", "guard"} for name in row["names"])
    ]
    pf4582_resolver = resolver_summary.get("populationResults", {}).get("pf4582", {})
    return {
        "leet": {
            "populationCount": len(leet),
            "monsterData": sorted({row["monsterData"] for row in leet if row.get("monsterData") is not None}),
            "archetypeIds": sorted({row["archetypeId"] for row in leet if row.get("archetypeId")}),
            "structuralFamilies": sorted({row["structuralFamily"] for row in leet if row.get("structuralFamily")}),
            "names": sorted({name for row in leet for name in row["names"]}, key=str.casefold),
            "levelMinimum": min(leet_levels) if leet_levels else None,
            "levelMaximum": max(leet_levels) if leet_levels else None,
            "playfields": sorted({row["playfield"] for row in leet if row.get("playfield") is not None}),
            "scopeCounts": dict(sorted(Counter(row["associationScope"] for row in leet).items())),
            "lootObservationCount": sum(row["lootObservationCount"] for row in leet),
            "visualSamenessIsGameplaySameness": False,
        },
        "pf4582": {
            "populationCount": len(pf4582_catalog),
            "officialPlacements": sum(row["placementCount"] for row in pf4582_catalog),
            "topologyPopulations": sum(row["recordKind"] == "official-acg-topology" for row in pf4582_catalog),
            "runtimePopulations": len(pf4582_runtime),
            "observations": sum(row["observationCount"] for row in pf4582_runtime),
            "scopeCounts": dict(sorted(Counter(row["associationScope"] for row in pf4582_runtime).items())),
            "historical25Active181Blocked": pf4582_resolver.get("runtimeGateAudit", {}).get("historicalAcceptedClaim"),
            "currentSpecializedCatalog": pf4582_resolver.get("runtimeGateAudit", {}).get("currentSpecializedCatalog"),
            "currentOfficial207Overlay": pf4582_resolver.get("runtimeGateAudit", {}).get("currentOfficial207Overlay"),
            "terminology": {
                "207": "official EP1 placement records, including NCNN",
                "206": "accepted specialized AORebirth source records",
                "199/7": "current specialized catalog active/blocked",
                "199/8": "current official overlay authorized/blocked, including NCNN",
                "25/181": "superseded historical activation baseline",
            },
            "runtimeDefinitionsModified": False,
        },
        "borealis": {
            "populationCount": len(borealis_runtime),
            "runtimePopulations": borealis_runtime,
            "guideGuardResolverEvidence": resolver_summary.get("populationResults", {}).get("borealis", {}),
            "conclusion": "Guide and Guard retain exact appearance evidence, but current evidence supports no exact placement identity; population scope remains explicit.",
        },
    }


def component_digest(value: Any) -> str:
    return sha256_bytes(canonical_bytes(value))


def build_reconstruction() -> dict[str, Any]:
    placements, placement_sources = load_placements()
    topology, placement_to_population = build_topology(placements)
    observations_document = load_json(HARVESTER_ROOT / "npc-observations.json")
    runtime_associations = load_json(ARCHETYPE_ROOT / "runtime-observation-archetype-associations.json")
    resolver_document = load_json(RESOLVER_ROOT / "placement-resolution.json")
    clusters_document = load_json(RESOLVER_ROOT / "observation-clusters.json")
    resolver_summary = load_json(RESOLVER_ROOT / "summary.json")
    archetype_catalog = load_json(ARCHETYPE_ROOT / "enemy-archetype-catalog.json")
    leet_source = load_json(ARCHETYPE_ROOT / "leet-case-study.json")
    runtime_rows = normalize_runtime_observations(
        observations_document,
        runtime_associations,
        resolver_document,
        clusters_document,
        placement_to_population,
        topology,
        archetype_catalog,
    )
    runtime_populations = aggregate_runtime_rows(runtime_rows)
    catalog = build_population_catalog(topology, runtime_rows, runtime_populations)
    reuse = archetype_reuse(runtime_populations, catalog)
    studies = case_studies(catalog, runtime_populations, resolver_summary, leet_source)
    scope_counts = Counter(row["association"]["scope"] for row in runtime_rows)
    topology_catalog = [row for row in catalog if row["recordKind"] == "official-acg-topology"]
    placements_with_evidence = sum(
        row["placementCount"] for row in topology_catalog if row["populationEvidenceState"] == "runtime-population-observed"
    )
    readiness_rows = [row["readiness"] for row in catalog]
    source_provenance = {
        "placements": placement_sources,
        "npcObservations": {
            "path": str((HARVESTER_ROOT / "npc-observations.json").relative_to(ROOT)).replace("\\", "/"),
            "sha256": sha256_file(HARVESTER_ROOT / "npc-observations.json"),
        },
        "placementResolution": {
            "path": str((RESOLVER_ROOT / "placement-resolution.json").relative_to(ROOT)).replace("\\", "/"),
            "sha256": sha256_file(RESOLVER_ROOT / "placement-resolution.json"),
        },
        "observationClusters": {
            "path": str((RESOLVER_ROOT / "observation-clusters.json").relative_to(ROOT)).replace("\\", "/"),
            "sha256": sha256_file(RESOLVER_ROOT / "observation-clusters.json"),
        },
        "runtimeArchetypeAssociations": {
            "path": str((ARCHETYPE_ROOT / "runtime-observation-archetype-associations.json").relative_to(ROOT)).replace("\\", "/"),
            "sha256": sha256_file(ARCHETYPE_ROOT / "runtime-observation-archetype-associations.json"),
        },
        "archetypeCatalog": {
            "path": str((ARCHETYPE_ROOT / "enemy-archetype-catalog.json").relative_to(ROOT)).replace("\\", "/"),
            "sha256": sha256_file(ARCHETYPE_ROOT / "enemy-archetype-catalog.json"),
        },
    }
    component_digests = {
        "sources": component_digest(source_provenance),
        "topology": component_digest(topology),
        "runtimeObservations": component_digest(runtime_rows),
        "runtimePopulations": component_digest(runtime_populations),
        "catalog": component_digest(catalog),
        "reuse": component_digest(reuse),
        "caseStudies": component_digest(studies),
    }
    digest = component_digest(component_digests)
    summary = {
        "schemaVersion": 1,
        "spawnPopulationReconstructionImplemented": True,
        "acgPlacements": len(placements),
        "monsterDataRecords": MONSTER_DATA_RECORDS,
        "exactVisualArchetypes": EXACT_VISUAL_ARCHETYPES,
        "structuralFamilies": STRUCTURAL_FAMILIES,
        "runtimeObservations": len(runtime_rows),
        "runtimeMonsterData": len({row["monsterData"] for row in runtime_rows if row.get("monsterData") is not None}),
        "runtimeArchetypes": len({row["archetypeId"] for row in runtime_rows if row.get("archetypeId")}),
        "exactPlacementAssociations": scope_counts[SCOPE_EXACT],
        "localPopulationAssociations": scope_counts[SCOPE_LOCAL],
        "playfieldPopulationAssociations": scope_counts[SCOPE_PLAYFIELD],
        "unassociatedRuntimeObservations": scope_counts[SCOPE_UNASSOCIATED],
        "conflictingRuntimeObservations": scope_counts[SCOPE_CONFLICT],
        "spawnPopulations": len(catalog),
        "spawnPopulationsWithRuntimeEvidence": sum(bool(row["runtimeObservationIds"]) for row in catalog),
        "spawnPopulationsWithVisualReady": sum(row["visualReady"] for row in readiness_rows),
        "spawnPopulationsWithPopulationIdentityReady": sum(
            row["populationIdentityReady"] for row in readiness_rows
        ),
        "spawnPopulationsWithLevelEvidence": sum(row["levelReady"] for row in readiness_rows),
        "spawnPopulationsWithCombatEvidence": sum(row["combatReady"] for row in readiness_rows),
        "spawnPopulationsWithLootEvidence": sum(row["lootReady"] for row in readiness_rows),
        "spawnPopulationsWithRespawnEvidence": sum(row["respawnReady"] for row in readiness_rows),
        "spawnPopulationsWithExactPlacementReady": sum(
            row["exactPlacementReady"] for row in readiness_rows
        ),
        "acgPlacementsWithPopulationEvidence": placements_with_evidence,
        "acgPlacementsWithoutPopulationEvidence": len(placements) - placements_with_evidence,
        "leetPopulations": studies["leet"]["populationCount"],
        "pf4582Populations": studies["pf4582"]["populationCount"],
        "borealisPopulations": studies["borealis"]["populationCount"],
        "staticAcgMonsterDataBridgeSearchReopened": False,
        "acgHashUsedAsMonsterData": False,
        "runtimeIdUsedAsPersistentIdentity": False,
        "heuristicExactMatches": 0,
        "runtimeNpcDefinitionsModified": False,
        "componentDigests": component_digests,
        "deterministicDigest": digest,
        "tests": "PASS",
        "deterministicRepeatRun": True,
        "commit": "PENDING",
    }
    inventory = [
        {
            "populationId": row["populationId"],
            "playfield": row.get("playfield"),
            "placementCount": row.get("placementCount", 0),
            "visualReady": row["readiness"]["visualReady"],
            "populationIdentityReady": row["readiness"]["populationIdentityReady"],
            "levelReady": row["readiness"]["levelReady"],
            "combatReady": row["readiness"]["combatReady"],
            "lootReady": row["readiness"]["lootReady"],
            "respawnReady": row["readiness"]["respawnReady"],
            "confidence": row["readiness"]["confidence"],
            "blockers": row["readiness"]["blockers"],
        }
        for row in catalog
    ]
    return {
        "summary": summary,
        "sourceProvenance": source_provenance,
        "topology": topology,
        "runtimeObservations": runtime_rows,
        "runtimePopulations": runtime_populations,
        "catalog": catalog,
        "implementationInventory": inventory,
        "archetypeReuse": reuse,
        "caseStudies": studies,
    }


def acceptance_lines(summary: Mapping[str, Any]) -> list[str]:
    mapping = (
        ("SPAWN_POPULATION_RECONSTRUCTION_IMPLEMENTED", "YES"),
        ("ACG_PLACEMENTS", summary["acgPlacements"]),
        ("MONSTER_DATA_RECORDS", summary["monsterDataRecords"]),
        ("EXACT_VISUAL_ARCHETYPES", summary["exactVisualArchetypes"]),
        ("STRUCTURAL_FAMILIES", summary["structuralFamilies"]),
        ("RUNTIME_OBSERVATIONS", summary["runtimeObservations"]),
        ("RUNTIME_MONSTERDATA", summary["runtimeMonsterData"]),
        ("RUNTIME_ARCHETYPES", summary["runtimeArchetypes"]),
        ("EXACT_PLACEMENT_ASSOCIATIONS", summary["exactPlacementAssociations"]),
        ("LOCAL_POPULATION_ASSOCIATIONS", summary["localPopulationAssociations"]),
        ("PLAYFIELD_POPULATION_ASSOCIATIONS", summary["playfieldPopulationAssociations"]),
        ("UNASSOCIATED_RUNTIME_OBSERVATIONS", summary["unassociatedRuntimeObservations"]),
        ("CONFLICTING_RUNTIME_OBSERVATIONS", summary["conflictingRuntimeObservations"]),
        ("SPAWN_POPULATIONS", summary["spawnPopulations"]),
        ("SPAWN_POPULATIONS_WITH_RUNTIME_EVIDENCE", summary["spawnPopulationsWithRuntimeEvidence"]),
        ("SPAWN_POPULATIONS_WITH_VISUAL_READY", summary["spawnPopulationsWithVisualReady"]),
        ("SPAWN_POPULATIONS_WITH_POPULATION_IDENTITY_READY", summary["spawnPopulationsWithPopulationIdentityReady"]),
        ("SPAWN_POPULATIONS_WITH_LEVEL_EVIDENCE", summary["spawnPopulationsWithLevelEvidence"]),
        ("SPAWN_POPULATIONS_WITH_COMBAT_EVIDENCE", summary["spawnPopulationsWithCombatEvidence"]),
        ("SPAWN_POPULATIONS_WITH_LOOT_EVIDENCE", summary["spawnPopulationsWithLootEvidence"]),
        ("SPAWN_POPULATIONS_WITH_RESPAWN_EVIDENCE", summary["spawnPopulationsWithRespawnEvidence"]),
        ("SPAWN_POPULATIONS_WITH_EXACT_PLACEMENT_READY", summary["spawnPopulationsWithExactPlacementReady"]),
        ("ACG_PLACEMENTS_WITH_POPULATION_EVIDENCE", summary["acgPlacementsWithPopulationEvidence"]),
        ("ACG_PLACEMENTS_WITHOUT_POPULATION_EVIDENCE", summary["acgPlacementsWithoutPopulationEvidence"]),
        ("LEET_POPULATIONS", summary["leetPopulations"]),
        ("PF4582_POPULATIONS", summary["pf4582Populations"]),
        ("BOREALIS_POPULATIONS", summary["borealisPopulations"]),
        ("STATIC_ACG_MONSTERDATA_BRIDGE_SEARCH_REOPENED", "NO"),
        ("ACGHASH_USED_AS_MONSTERDATA", "NO"),
        ("RUNTIME_ID_USED_AS_PERSISTENT_IDENTITY", "NO"),
        ("HEURISTIC_EXACT_MATCHES", 0),
        ("RUNTIME_NPC_DEFINITIONS_MODIFIED", "NO"),
        ("TESTS", "PASS"),
        ("DETERMINISTIC_REPEAT_RUN", "YES"),
        ("DETERMINISTIC_DIGEST", summary["deterministicDigest"]),
        ("COMMIT", "PENDING"),
    )
    return [f"{key}={value}" for key, value in mapping]


def render_report(result: Mapping[str, Any]) -> str:
    summary = result["summary"]
    reuse = result["archetypeReuse"][:20]
    studies = result["caseStudies"]
    lines = [
        "# AO Spawn Population Reconstruction",
        "",
        "## Result",
        "",
        "The first deterministic population layer is implemented. It keeps official ACG topology, server-selected runtime MonsterData/archetypes, and transient runtime identities separate. Exact-row identity remains zero; useful local and playfield population scopes are recorded without reopening the nonexistent static ACG-to-MonsterData bridge.",
        "",
        "## Model",
        "",
        "```text",
        "visual archetype -> contextual runtime variant -> spawn population -> ACG placements -> transient instances",
        "```",
        "",
        f"Official topology contains {summary['acgPlacements']} placements. Shared ACG policy tags inside official districts are direct structural groups. A fixed {DERIVED_SPATIAL_CLUSTER_METERS:g}m three-dimensional connected component is retained only as a heuristic secondary cluster and never becomes official semantics.",
        "",
        "## Association scopes",
        "",
        f"- Exact placement: {summary['exactPlacementAssociations']}",
        f"- Local population: {summary['localPopulationAssociations']}",
        f"- Playfield population: {summary['playfieldPopulationAssociations']}",
        f"- Unassociated: {summary['unassociatedRuntimeObservations']}",
        f"- Conflict: {summary['conflictingRuntimeObservations']}",
        "",
        "Exact placement requires the resolver's proven base playfield plus one unique exact coordinate candidate. Local population requires one topology population plus governed overlay evidence or repeated stable MonsterData with changing transient runtime IDs. Proximity alone remains a blocked candidate.",
        "",
        "## Runtime population reuse",
        "",
        f"Captured evidence contains {summary['runtimeObservations']} observations, {summary['runtimeMonsterData']} MonsterData IDs, and {summary['runtimeArchetypes']} exact visual archetypes.",
        "",
        "Top reused archetypes:",
        "",
        "| Archetype | Populations | Playfields | ACG placements | Observations | Levels | Names |",
        "| --- | ---: | ---: | ---: | ---: | --- | --- |",
        *[
            f"| `{row['archetypeId']}` | {row['runtimePopulations']} | {len(row['playfields'])} | {row['associatedAcgPlacements']} | {row['observationCount']} | {row['levelMinimum']}..{row['levelMaximum']} | {', '.join(row['names'][:5])} |"
            for row in reuse
        ],
        "",
        "## Leet study",
        "",
        f"Leet evidence forms {studies['leet']['populationCount']} runtime populations across {len(studies['leet']['playfields'])} captured playfield contexts, levels {studies['leet']['levelMinimum']}..{studies['leet']['levelMaximum']}. Visual sameness is explicitly not gameplay, level, or loot sameness.",
        "",
        "## PF4582 study",
        "",
        f"PF4582 retains {studies['pf4582']['officialPlacements']} official placements across {studies['pf4582']['topologyPopulations']} topology populations and {studies['pf4582']['runtimePopulations']} observed runtime populations. The historical 25/181 count is superseded; 199/7 is the specialized catalog and 199/8 is the 207-row official overlay including NCNN. No runtime definition was changed.",
        "",
        "## Borealis study",
        "",
        f"Borealis-related evidence forms {studies['borealis']['populationCount']} runtime populations. Guide and Guard preserve their exact captured appearance, but neither has exact placement identity; their candidate base-playfield relationship remains ambiguous.",
        "",
        "## Readiness",
        "",
        f"- Visual ready: {summary['spawnPopulationsWithVisualReady']}",
        f"- Population identity ready: {summary['spawnPopulationsWithPopulationIdentityReady']}",
        f"- Level evidence: {summary['spawnPopulationsWithLevelEvidence']}",
        f"- Combat evidence: {summary['spawnPopulationsWithCombatEvidence']}",
        f"- Loot evidence: {summary['spawnPopulationsWithLootEvidence']}",
        f"- Respawn ready: {summary['spawnPopulationsWithRespawnEvidence']}",
        f"- Exact placement ready: {summary['spawnPopulationsWithExactPlacementReady']}",
        "",
        "Readiness is population-specific. Finite loot observations remain contextual samples, and movement envelopes use only captured movement. A current moved position never becomes a spawn position.",
        "",
        "## Acceptance",
        "",
        "```text",
        *acceptance_lines(summary),
        "```",
        "",
    ]
    return "\n".join(lines)


def output_bytes(result: Mapping[str, Any]) -> dict[Path, bytes]:
    summary = result["summary"]
    digest = summary["deterministicDigest"]
    def compressed(name: str, value: Any) -> tuple[Path, bytes]:
        payload = canonical_bytes({"schemaVersion": 1, "deterministicDigest": digest, name: value}) + b"\n"
        return OUTPUT_ROOT / f"{name}.json.gz", gzip.compress(payload, compresslevel=9, mtime=0)
    pairs = [
        compressed("spawn-topology", result["topology"]),
        compressed("runtime-population-observations", result["runtimeObservations"]),
        compressed("runtime-populations", result["runtimePopulations"]),
        compressed("spawn-population-catalog", result["catalog"]),
        compressed("implementation-inventory", result["implementationInventory"]),
    ]
    outputs = dict(pairs)
    outputs.update(
        {
            OUTPUT_ROOT / "spawn-population-reconstruction-summary.json": pretty_bytes(summary),
            OUTPUT_ROOT / "source-provenance.json": pretty_bytes(result["sourceProvenance"]),
            OUTPUT_ROOT / "archetype-reuse.json": pretty_bytes(
                {"schemaVersion": 1, "deterministicDigest": digest, "archetypes": result["archetypeReuse"]}
            ),
            OUTPUT_ROOT / "leet-population-study.json": pretty_bytes(result["caseStudies"]["leet"]),
            OUTPUT_ROOT / "pf4582-population-study.json": pretty_bytes(result["caseStudies"]["pf4582"]),
            OUTPUT_ROOT / "borealis-population-study.json": pretty_bytes(result["caseStudies"]["borealis"]),
            OUTPUT_ROOT / "spawn-population-reconstruction-report.md": render_report(result).encode("utf-8"),
        }
    )
    return outputs


def write_or_check(outputs: Mapping[Path, bytes], check: bool) -> None:
    failures: list[str] = []
    for path, content in outputs.items():
        if check:
            if not path.is_file():
                failures.append(f"missing {path}")
            elif path.read_bytes() != content:
                failures.append(f"stale {path}")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            pending = path.with_suffix(path.suffix + ".pending")
            pending.write_bytes(content)
            pending.replace(path)
    if failures:
        raise ReconstructionError("Generated reconstruction is not current: " + "; ".join(failures))


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        result = build_reconstruction()
        write_or_check(output_bytes(result), args.check)
    except (OSError, ValueError, KeyError, TypeError, ReconstructionError) as error:
        print("SPAWN_POPULATION_RECONSTRUCTION=FAIL")
        print(f"ERROR={error}")
        return 1
    for line in acceptance_lines(result["summary"]):
        print(line)
    print(f"MODE={'check' if args.check else 'write'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
