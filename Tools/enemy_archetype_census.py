#!/usr/bin/env python3
"""Deterministic, evidence-first census of reusable AO NPC visual archetypes."""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE = REPO_ROOT / "build-verify/enemy-archetype-census/official-enemy-visual-sources.json"
DEFAULT_PLACEMENT_INDEX = REPO_ROOT / "docs/generated/playfields/official-placement-index.json"
DEFAULT_OBSERVATION_ROOT = REPO_ROOT / "build-verify/npc-observation-harvester"
DEFAULT_OUTPUT_ROOT = REPO_ROOT / "docs/generated/enemy_archetypes"
UNSET_SENTINEL = 1234567890


class CensusError(RuntimeError):
    pass


def canonical_json(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def canonical_digest(value: Any) -> str:
    return hashlib.sha256(canonical_json(value).encode("utf-8")).hexdigest()


def pretty_json(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"


def stable_id(prefix: str, payload: Any) -> str:
    return f"{prefix}-{canonical_digest(payload)[:16]}"


def load_json(path: Path) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        raise CensusError(f"Unable to read JSON {path}: {exc}") from exc


def state_value(field: Any) -> Any | None:
    if not isinstance(field, Mapping):
        return None
    if field.get("state") != "value":
        return None
    value = field.get("value")
    if value == UNSET_SENTINEL:
        return None
    return value


def normalize_official_state(field: Any) -> dict[str, Any]:
    if not isinstance(field, Mapping):
        return {"state": "absent", "value": None}
    state = str(field.get("state", "absent"))
    value = field.get("value")
    if value == UNSET_SENTINEL or state == "sentinel/default":
        return {"state": "sentinel/default", "value": None}
    if state != "value":
        return {"state": state, "value": None}
    return {"state": "value", "value": value}


def normalize_observed_field(field: Any) -> dict[str, Any]:
    if not isinstance(field, Mapping):
        return {"state": "not-observed", "value": None}
    classification = str(field.get("evidenceClassification", "not-observed"))
    status = str(field.get("status", "not observed"))
    value = field.get("value")
    if value == UNSET_SENTINEL or classification == "sentinel/default":
        return {"state": "sentinel/default", "value": None}
    if classification in {"not-observed", "not-protocol-exposed"} or status in {
        "not observed",
        "not protocol-exposed",
    }:
        return {"state": classification, "value": None}
    if status == "conflict":
        values = field.get("observedValues", [])
        return {"state": "conflict", "value": values}
    return {"state": "observed", "value": value}


def observed_value(field: Any) -> Any | None:
    normalized = normalize_observed_field(field)
    return normalized["value"] if normalized["state"] == "observed" else None


def normalize_slot_array(value: Any, keys: Sequence[str]) -> list[dict[str, Any]]:
    if not isinstance(value, list):
        return []
    normalized = []
    for row in value:
        if isinstance(row, Mapping):
            normalized.append({key: row.get(key) for key in keys})
    return sorted(normalized, key=lambda row: tuple(str(row.get(key)) for key in keys))


def build_official_archetypes(source: Mapping[str, Any]) -> dict[str, Any]:
    monster_records = source.get("monsterDataRecords", [])
    cat_mesh_records = source.get("catMeshRecords", [])
    if not isinstance(monster_records, list) or not isinstance(cat_mesh_records, list):
        raise CensusError("Official source catalog has an invalid schema.")

    cat_by_id = {int(row["recordId"]): row for row in cat_mesh_records}
    grouped: dict[str, list[dict[str, Any]]] = defaultdict(list)
    signature_payloads: dict[str, dict[str, Any]] = {}
    base_payloads: dict[str, dict[str, Any]] = {}
    monster_to_signature: dict[int, str] = {}
    cat_mesh_to_signatures: dict[int, set[str]] = defaultdict(set)

    for record in sorted(monster_records, key=lambda row: int(row["monsterData"])):
        monster_data = int(record["monsterData"])
        mesh_state = normalize_official_state(record.get("mesh"))
        head_state = normalize_official_state(record.get("headMesh"))
        features_state = normalize_official_state(record.get("features"))
        mesh_id = state_value(mesh_state)
        head_mesh_id = state_value(head_state)
        cat_mesh = cat_by_id.get(int(mesh_id)) if mesh_id is not None else None
        head_mesh = cat_by_id.get(int(head_mesh_id)) if head_mesh_id is not None else None

        cat_identity = (
            {"rawSha256": cat_mesh["rawSha256"]}
            if cat_mesh is not None
            else {"unresolvedResourceId": mesh_id, "state": mesh_state["state"]}
        )
        head_identity = {
            "state": head_state["state"],
            "resource": (
                {"rawSha256": head_mesh["rawSha256"]}
                if head_mesh is not None
                else ({"unresolvedResourceId": head_mesh_id} if head_mesh_id is not None else None)
            ),
        }
        visual_payload = {
            "catMesh": cat_identity,
            "headMesh": head_identity,
            "features": features_state,
            "animationGroupMapSha256": record.get("animationGroupMapSha256"),
        }
        visual_signature = canonical_digest(visual_payload)
        monster_to_signature[monster_data] = visual_signature
        signature_payloads[visual_signature] = visual_payload
        grouped[visual_signature].append(record)
        if mesh_id is not None:
            cat_mesh_to_signatures[int(mesh_id)].add(visual_signature)

        if cat_mesh is not None:
            base_payload = {
                "jointSha256": cat_mesh.get("jointSha256"),
                "meshStructureSha256": cat_mesh.get("meshStructureSha256"),
            }
        else:
            base_payload = {"unresolvedResourceId": mesh_id, "state": mesh_state["state"]}
        base_payloads[visual_signature] = base_payload

    archetypes: list[dict[str, Any]] = []
    signature_to_archetype: dict[str, str] = {}
    monster_to_archetype: dict[int, str] = {}
    cat_mesh_to_archetypes: dict[int, set[str]] = defaultdict(set)
    for signature in sorted(grouped):
        records = grouped[signature]
        archetype_id = stable_id("archetype", signature_payloads[signature])
        base_id = stable_id("base-model", base_payloads[signature])
        signature_to_archetype[signature] = archetype_id
        monster_data = sorted(int(row["monsterData"]) for row in records)
        for value in monster_data:
            monster_to_archetype[value] = archetype_id
        cat_mesh_ids = sorted(
            {
                int(value)
                for row in records
                for value in [state_value(normalize_official_state(row.get("mesh")))]
                if value is not None
            }
        )
        for value in cat_mesh_ids:
            cat_mesh_to_archetypes[value].add(archetype_id)
        head_meshes = [normalize_official_state(row.get("headMesh")) for row in records]
        head_values = sorted({int(value) for value in (state_value(row) for row in head_meshes) if value is not None})
        head_states = sorted({row["state"] for row in head_meshes})
        official_names = sorted({str(row.get("officialName", "")) for row in records if row.get("officialName")})
        archetypes.append(
            {
                "archetypeId": archetype_id,
                "baseModelFamily": base_id,
                "canonicalVisualSignature": signature,
                "canonicalVisualSignatureFields": signature_payloads[signature],
                "monsterData": monster_data,
                "catMeshes": cat_mesh_ids,
                "headMeshes": head_values,
                "headMeshEvidenceStates": head_states,
                "officialNames": official_names,
                "officialVisualRecordCount": len(records),
                "captureOverlay": {
                    "observationIds": [],
                    "observedNames": [],
                    "observedLevels": [],
                    "resourcePlayfields": [],
                    "runtimePlayfields": [],
                    "runtimeVisualVariants": [],
                    "coverage": {},
                },
                "acgPlacements": [],
                "ambiguities": [],
                "conflicts": [],
            }
        )

    archetypes.sort(key=lambda row: row["archetypeId"])
    return {
        "archetypes": archetypes,
        "catById": cat_by_id,
        "monsterToArchetype": monster_to_archetype,
        "catMeshToArchetypes": {key: sorted(values) for key, values in cat_mesh_to_archetypes.items()},
    }


def runtime_visual_variant_payload(observation: Mapping[str, Any], archetype_id: str) -> dict[str, Any]:
    fields = observation.get("fields", {}) if isinstance(observation.get("fields"), Mapping) else {}
    textures = normalize_observed_field(fields.get("textures"))
    meshes = normalize_observed_field(fields.get("meshes"))
    if textures["state"] == "observed":
        textures["value"] = normalize_slot_array(textures["value"], ("place", "id", "unknown"))
    if meshes["state"] == "observed":
        meshes["value"] = normalize_slot_array(meshes["value"], ("place", "slot", "id", "unknown"))
    return {
        "archetypeId": archetype_id,
        "appearanceValue": normalize_observed_field(fields.get("appearanceValue")),
        "headMesh": normalize_observed_field(fields.get("headMesh")),
        "textures": textures,
        "meshes": meshes,
        "breed": normalize_observed_field(fields.get("breed")),
        "gender": normalize_observed_field(fields.get("gender")),
        "race": normalize_observed_field(fields.get("race")),
        "visualFlags": normalize_observed_field(fields.get("visualFlags")),
    }


def resolve_runtime_observation(
    observation: Mapping[str, Any],
    monster_to_archetype: Mapping[int, str],
    cat_mesh_to_archetypes: Mapping[int, Sequence[str]],
) -> dict[str, Any]:
    fields = observation.get("fields", {}) if isinstance(observation.get("fields"), Mapping) else {}
    monster_data = observed_value(fields.get("monsterData"))
    cat_mesh = observed_value(fields.get("catMesh"))
    candidates: set[str] = set()
    basis = "none"
    if isinstance(monster_data, int) and monster_data in monster_to_archetype:
        candidates.add(monster_to_archetype[monster_data])
        basis = "direct-official-monsterdata-resource-chain"
    elif isinstance(cat_mesh, int) and cat_mesh in cat_mesh_to_archetypes:
        candidates.update(cat_mesh_to_archetypes[cat_mesh])
        basis = "direct-client-catmesh-resource"

    if len(candidates) == 1:
        state = "unique"
        archetype_id = next(iter(candidates))
        variant_payload = runtime_visual_variant_payload(observation, archetype_id)
        variant_id = stable_id("runtime-visual", variant_payload)
    elif len(candidates) > 1:
        state = "ambiguous"
        archetype_id = None
        variant_id = None
    else:
        state = "unknown"
        archetype_id = None
        variant_id = None

    return {
        "observationId": observation.get("observationId"),
        "runtimeIdentity": observation.get("identity"),
        "name": observation.get("name"),
        "monsterData": monster_data,
        "catMesh": cat_mesh,
        "resourcePlayfieldId": observation.get("resourcePlayfieldId"),
        "runtimePlayfieldId": observation.get("runtimePlayfieldId"),
        "associationState": state,
        "associationBasis": basis,
        "archetypeId": archetype_id,
        "candidateArchetypes": sorted(candidates),
        "runtimeVisualVariantId": variant_id,
    }


def official_model_reference(record: Mapping[str, Any]) -> tuple[int | None, str | None]:
    reference = record.get("OfficialModelReference") or record.get("officialModelReference")
    if not isinstance(reference, Mapping):
        return None, None
    provenance = str(reference.get("provenance", ""))
    if provenance not in {"direct-official", "indirect-official"}:
        return None, None
    value = reference.get("monsterData")
    return (int(value), provenance) if isinstance(value, int) else (None, None)


def classify_placement(record: Mapping[str, Any], monster_to_archetype: Mapping[int, str]) -> dict[str, Any]:
    monster_data, provenance = official_model_reference(record)
    archetype_id = monster_to_archetype.get(monster_data) if monster_data is not None else None
    if monster_data is None:
        state = "unresolved"
    elif archetype_id is None:
        state = "ambiguous"
    else:
        state = provenance
    return {
        "acgHash": record.get("CanonicalAcgHashText"),
        "playfield": record.get("PlayfieldId"),
        "positionX": record.get("PositionX"),
        "positionY": record.get("PositionY"),
        "positionZ": record.get("PositionZ"),
        "officialSpawnRecordId": record.get("OfficialSpawnRecordId"),
        "officialModelReference": monster_data,
        "archetypeId": archetype_id,
        "associationState": state,
    }


def load_placements(index_path: Path) -> list[dict[str, Any]]:
    index = load_json(index_path)
    records: list[dict[str, Any]] = []
    for entry in sorted(index.get("Playfields", []), key=lambda row: int(row["PlayfieldId"])):
        path_text = entry.get("Path")
        if not path_text:
            continue
        path = REPO_ROOT / str(path_text)
        shard = load_json(path)
        for record in shard.get("Records", []):
            records.append(record)
    return sorted(
        records,
        key=lambda row: (
            int(row.get("PlayfieldId", -1)),
            int(row.get("DistrictIndex", -1)),
            int(row.get("DistrictRecordOrdinal", -1)),
        ),
    )


def build_case_study(label: str, token: str, archetypes: Sequence[Mapping[str, Any]]) -> dict[str, Any]:
    token = token.casefold()
    direct = [
        row
        for row in archetypes
        if any(token in name.casefold() for name in row.get("officialNames", []))
        or any(token in name.casefold() for name in row.get("captureOverlay", {}).get("observedNames", []))
    ]
    family_ids = {row["baseModelFamily"] for row in direct}
    related = [row for row in archetypes if row["baseModelFamily"] in family_ids]
    official_names = sorted({name for row in related for name in row.get("officialNames", [])})
    observed_names = sorted(
        {name for row in related for name in row.get("captureOverlay", {}).get("observedNames", [])}
    )
    resource_playfields = sorted(
        {value for row in related for value in row.get("captureOverlay", {}).get("resourcePlayfields", [])}
    )
    runtime_playfields = sorted(
        {value for row in related for value in row.get("captureOverlay", {}).get("runtimePlayfields", [])}
    )
    levels = sorted({value for row in related for value in row.get("captureOverlay", {}).get("observedLevels", [])})
    return {
        "caseStudy": label,
        "seedRule": f"name token '{token}' only seeds official/runtime records; family expansion follows visual signatures",
        "directSeedArchetypes": sorted(row["archetypeId"] for row in direct),
        "baseModelFamilies": sorted(family_ids),
        "visualArchetypes": sorted(row["archetypeId"] for row in related),
        "monsterData": sorted({value for row in related for value in row.get("monsterData", [])}),
        "catMeshes": sorted({value for row in related for value in row.get("catMeshes", [])}),
        "officialNames": official_names,
        "observedNames": observed_names,
        "levelMinimum": min(levels) if levels else None,
        "levelMaximum": max(levels) if levels else None,
        "resourcePlayfields": resource_playfields,
        "runtimePlayfields": runtime_playfields,
        "resolvedAcgPlacements": None,
        "placementState": "unresolved-official-acg-to-monsterdata-join",
    }


def category_coverage(observation: Mapping[str, Any]) -> dict[str, str]:
    evidence = observation.get("categoryEvidence", {})
    if not isinstance(evidence, Mapping):
        return {}
    result: dict[str, str] = {}
    for key, value in evidence.items():
        if isinstance(value, Mapping):
            result[str(key)] = str(value.get("status", "not observed"))
        else:
            result[str(key)] = str(value)
    return result


def level_from_observation(observation: Mapping[str, Any]) -> int | None:
    fields = observation.get("fields", {})
    if not isinstance(fields, Mapping):
        return None
    value = observed_value(fields.get("level"))
    return int(value) if isinstance(value, int) else None


def build_census(
    source: Mapping[str, Any],
    placements: Sequence[Mapping[str, Any]],
    observations_document: Mapping[str, Any],
    harvester_summary: Mapping[str, Any],
) -> dict[str, Any]:
    official = build_official_archetypes(source)
    archetypes = official["archetypes"]
    archetype_by_id = {row["archetypeId"]: row for row in archetypes}

    placement_rows = [classify_placement(row, official["monsterToArchetype"]) for row in placements]
    placement_counts = Counter(row["associationState"] for row in placement_rows)
    for row in placement_rows:
        if row["archetypeId"]:
            archetype_by_id[row["archetypeId"]]["acgPlacements"].append(row["officialSpawnRecordId"])

    observation_rows = []
    coverage_counters: dict[str, Counter[str]] = defaultdict(Counter)
    runtime_variant_ids: set[str] = set()
    contextual_variants: set[tuple[str, str, int | None]] = set()
    for observation in observations_document.get("observations", []):
        resolved = resolve_runtime_observation(
            observation,
            official["monsterToArchetype"],
            official["catMeshToArchetypes"],
        )
        level = level_from_observation(observation)
        resolved["level"] = level
        resolved["coverage"] = category_coverage(observation)
        observation_rows.append(resolved)
        archetype_id = resolved["archetypeId"]
        if not archetype_id:
            continue
        overlay = archetype_by_id[archetype_id]["captureOverlay"]
        overlay["observationIds"].append(resolved["observationId"])
        if resolved.get("name"):
            overlay["observedNames"].append(str(resolved["name"]))
        if level is not None:
            overlay["observedLevels"].append(level)
        if resolved.get("resourcePlayfieldId") is not None:
            overlay["resourcePlayfields"].append(resolved["resourcePlayfieldId"])
        if resolved.get("runtimePlayfieldId") is not None:
            overlay["runtimePlayfields"].append(resolved["runtimePlayfieldId"])
        if resolved.get("runtimeVisualVariantId"):
            overlay["runtimeVisualVariants"].append(resolved["runtimeVisualVariantId"])
            runtime_variant_ids.add(resolved["runtimeVisualVariantId"])
        if resolved.get("name"):
            contextual_variants.add((archetype_id, str(resolved["name"]), level))
        for category, state in resolved["coverage"].items():
            coverage_counters[archetype_id][f"{category}:{state}"] += 1

    for row in archetypes:
        overlay = row["captureOverlay"]
        for key in (
            "observationIds",
            "observedNames",
            "observedLevels",
            "resourcePlayfields",
            "runtimePlayfields",
            "runtimeVisualVariants",
        ):
            overlay[key] = sorted(set(overlay[key]))
        overlay["coverage"] = dict(sorted(coverage_counters[row["archetypeId"]].items()))
        row["acgPlacements"] = sorted(set(row["acgPlacements"]))

    observation_counts = Counter(row["associationState"] for row in observation_rows)
    leet = build_case_study("Leet", "leet", archetypes)
    heckler = build_case_study("Heckler", "heckler", archetypes)

    top_reused = sorted(
        archetypes,
        key=lambda row: (
            -int(row["officialVisualRecordCount"]),
            -len(row["captureOverlay"]["observationIds"]),
            row["archetypeId"],
        ),
    )[:20]
    top_rows = [
        {
            "archetypeId": row["archetypeId"],
            "baseModelFamily": row["baseModelFamily"],
            "officialVisualRecords": row["officialVisualRecordCount"],
            "monsterData": row["monsterData"],
            "catMeshes": row["catMeshes"],
            "officialNames": row["officialNames"],
            "capturedObservations": len(row["captureOverlay"]["observationIds"]),
            "observedNames": row["captureOverlay"]["observedNames"],
            "observedLevelMinimum": (
                min(row["captureOverlay"]["observedLevels"])
                if row["captureOverlay"]["observedLevels"]
                else None
            ),
            "observedLevelMaximum": (
                max(row["captureOverlay"]["observedLevels"])
                if row["captureOverlay"]["observedLevels"]
                else None
            ),
            "resolvedAcgPlacements": len(row["acgPlacements"]),
            "placementState": "unresolved" if not row["acgPlacements"] else "official-associated",
        }
        for row in top_reused
    ]

    cat_by_id = official["catById"]
    monster_records = source["monsterDataRecords"]
    referenced_cat_mesh_ids = sorted(
        {
            int(value)
            for row in monster_records
            for value in [state_value(normalize_official_state(row.get("mesh")))]
            if value is not None
        }
    )
    referenced_cats = [cat_by_id[value] for value in referenced_cat_mesh_ids if value in cat_by_id]
    unresolved_cat_mesh_ids = [value for value in referenced_cat_mesh_ids if value not in cat_by_id]
    mesh_signatures = {row["rawSha256"] for row in referenced_cats}
    mesh_signatures.update(f"unresolved:{value}" for value in unresolved_cat_mesh_ids)
    texture_signatures = {row["textureSha256"] for row in referenced_cats}
    base_families = {row["baseModelFamily"] for row in archetypes}
    official_names = {str(row.get("officialName", "")) for row in monster_records if row.get("officialName")}
    records_per_archetype = [int(row["officialVisualRecordCount"]) for row in archetypes]

    boss_reuse = []
    for row in archetypes:
        names = row["officialNames"] + row["captureOverlay"]["observedNames"]
        if any("boss" in name.casefold() for name in names) and any(
            "boss" not in name.casefold() for name in names
        ):
            boss_reuse.append(
                {
                    "archetypeId": row["archetypeId"],
                    "names": sorted(set(names)),
                    "classification": "name-labeled boss contextual candidate sharing a visual archetype",
                }
            )

    summary = {
        "schemaVersion": 1,
        "sourceClientBuild": source.get("sourceClientBuild"),
        "sourceClientVariant": source.get("sourceClientVariant"),
        "sourceHashes": source.get("sourceHashes"),
        "enemyArchetypeCensusImplemented": True,
        "officialEnemySubsetIdentifiable": False,
        "officialEnemySubsetReason": (
            "MonsterData is a visual resource family used by enemies, friendly NPCs, and structures; "
            "the official corpus does not carry hostility or gameplay ownership."
        ),
        "archetypeCountSelected": False,
        "archetypeCountReason": (
            "Exact complete visual signatures and structural base families are reported separately; "
            "neither is relabeled as the uniquely correct enemy count."
        ),
        "officialPlacements": len(placement_rows),
        "officialNpcVisualRecords": len(monster_records),
        "uniqueNames": len(official_names),
        "uniqueMonsterData": len({int(row["monsterData"]) for row in monster_records}),
        "uniqueCatMesh": len(referenced_cat_mesh_ids),
        "uniqueMeshSignatures": len(mesh_signatures),
        "uniqueTextureSignatures": len(texture_signatures),
        "uniqueCompleteVisualSignatures": len(archetypes),
        "baseModelFamilies": len(base_families),
        "visualVariants": len(archetypes),
        "runtimeVisualVariants": len(runtime_variant_ids),
        "gameplayVariantsIdentified": len(contextual_variants),
        "monsterDataPerVisualVariantMedian": statistics.median(records_per_archetype),
        "monsterDataPerVisualVariantMax": max(records_per_archetype),
        "placementsPerArchetypeMedian": None,
        "placementsPerArchetypeMax": None,
        "placementAssociation": {
            "directOfficial": placement_counts["direct-official"],
            "indirectOfficial": placement_counts["indirect-official"],
            "ambiguous": placement_counts["ambiguous"],
            "unresolved": placement_counts["unresolved"],
        },
        "capturedNpcObservations": len(observation_rows),
        "captureObservationAssociation": {
            "uniqueArchetype": observation_counts["unique"],
            "ambiguousArchetype": observation_counts["ambiguous"],
            "unknownArchetype": observation_counts["unknown"],
        },
        "harvesterDigest": harvester_summary.get("deterministicOutputDigest"),
        "catMeshCoverage": {
            "indexedRecords": source.get("counts", {}).get("officialCatMeshIndexRecords"),
            "decodedRecords": source.get("counts", {}).get("decodedCatMeshRecords"),
            "decodeFailures": source.get("counts", {}).get("catMeshDecodeFailures"),
            "referencedIds": len(referenced_cat_mesh_ids),
            "unresolvedReferencedIds": unresolved_cat_mesh_ids,
        },
        "leet": leet,
        "heckler": heckler,
        "bossVisualReuseCandidates": boss_reuse,
        "runtimeToExactAcgRequiredForArchetype": False,
        "acgHashUsedAsRuntimeIdentity": False,
        "namesUsedAsArchetypeIdentity": False,
        "levelUsedAsArchetypeIdentity": False,
        "lootUsedAsVisualArchetypeIdentity": False,
    }

    catalog = {
        "schemaVersion": 1,
        "signatureMethod": {
            "baseModelFamily": "CATMesh joint hierarchy plus decoded mesh topology counts",
            "visualVariant": (
                "exact CATMesh raw resource hash plus HeadMesh resource state, MonsterData animation-map hash, "
                "and Features state"
            ),
            "missingValues": "preserved as explicit states; never converted to zero",
            "sentinel": "1234567890 rejected from signature values",
            "namesLevelsLootPlacements": "aggregated only; excluded from identity",
        },
        "sourceHashes": source.get("sourceHashes"),
        "archetypes": archetypes,
    }
    result_digest = canonical_digest(
        {
            "summary": summary,
            "catalog": catalog,
            "placements": placement_rows,
            "runtime": observation_rows,
            "top": top_rows,
        }
    )
    summary["deterministicDigest"] = result_digest
    catalog["deterministicDigest"] = result_digest
    return {
        "summary": summary,
        "catalog": catalog,
        "placements": placement_rows,
        "runtime": observation_rows,
        "top": top_rows,
        "leet": leet,
        "heckler": heckler,
    }


def render_placement_csv(rows: Sequence[Mapping[str, Any]]) -> str:
    stream = io.StringIO(newline="")
    fields = [
        "acgHash",
        "playfield",
        "positionX",
        "positionY",
        "positionZ",
        "officialSpawnRecordId",
        "officialModelReference",
        "archetypeId",
        "associationState",
    ]
    writer = csv.DictWriter(stream, fieldnames=fields, lineterminator="\n")
    writer.writeheader()
    for row in rows:
        writer.writerow({key: "" if row.get(key) is None else row.get(key) for key in fields})
    return stream.getvalue()


def markdown_list(values: Sequence[Any], limit: int = 12) -> str:
    if not values:
        return "none observed"
    shown = [str(value) for value in values[:limit]]
    suffix = f" (+{len(values) - limit} more)" if len(values) > limit else ""
    return ", ".join(shown) + suffix


def render_report(result: Mapping[str, Any]) -> str:
    summary = result["summary"]
    leet = result["leet"]
    heckler = result["heckler"]
    lines = [
        "# AO Enemy Archetype Census",
        "",
        "## Result",
        "",
        "The original client resource chain supports an exact census of reusable NPC visual records, complete "
        "visual signatures, and structural base-model families. It does not expose hostility/gameplay ownership "
        "for every MonsterData record, so the narrower count of enemy-only families remains unproven.",
        "",
        "MonsterData is not treated as the archetype by itself. Names, levels, loot, ACG hashes, placements, and "
        "runtime identities are excluded from visual identity.",
        "",
        "## Proven resource chain",
        "",
        "```text",
        "server-authored SimpleChar stat 359",
        "  -> ResourceDatabase 1040023:<MonsterData>",
        "  -> MonsterData stat 12 Mesh",
        "  -> ResourceDatabase 1010002:<CATMesh>",
        "  -> n3VisualDynel_t::SetCatMesh",
        "",
        "MonsterData stat 64 HeadMesh -> head/skin setup",
        "MonsterData group map 1 -> CAT-mesh animation/effect selection",
        "```",
        "",
        "ACG placement records are a separate official placement/spawn-policy corpus. The shipped client has no "
        "ACGHash-to-MonsterData resolver, so all current official placement-to-archetype rows remain unresolved "
        "rather than guessed.",
        "",
        "## Counts",
        "",
        "| Metric | Count |",
        "| --- | ---: |",
        f"| Official placements | {summary['officialPlacements']} |",
        f"| Official NPC visual records (MonsterData) | {summary['officialNpcVisualRecords']} |",
        f"| Unique official names | {summary['uniqueNames']} |",
        f"| Unique MonsterData IDs | {summary['uniqueMonsterData']} |",
        f"| Unique referenced CATMesh IDs | {summary['uniqueCatMesh']} |",
        f"| Unique exact mesh-resource signatures | {summary['uniqueMeshSignatures']} |",
        f"| Unique CATMesh texture/material signatures | {summary['uniqueTextureSignatures']} |",
        f"| Unique complete visual signatures | {summary['uniqueCompleteVisualSignatures']} |",
        f"| Structural base-model families | {summary['baseModelFamilies']} |",
        f"| Captured runtime visual variants | {summary['runtimeVisualVariants']} |",
        f"| Observed contextual name/level variants | {summary['gameplayVariantsIdentified']} |",
        "",
        "No single `ARCHETYPE_COUNT` is selected: exact complete visual signatures and broader structural families "
        "answer different questions, and neither can be narrowed to enemy-only records from MonsterData alone.",
        "",
        "## Canonical signature methodology",
        "",
        "- Exact CATMesh raw-resource hashes preserve full shipped mesh/material/texture differences.",
        "- HeadMesh absence, observed zero, and concrete resource references remain distinct.",
        "- MonsterData animation-map and Features states are included in complete visual signatures.",
        "- Structural base families use decoded joint hierarchy and mesh-topology counts; they intentionally group "
        "  visible variants that share a body/skeleton structure.",
        "- SCFU texture and mesh overrides preserve explicit slots and produce separate runtime visual variants.",
        "- `1234567890` is rejected; missing fields never become zero.",
        "",
        "## Leet case study",
        "",
        f"- Base-model families: {len(leet['baseModelFamilies'])}",
        f"- Complete visual archetypes in those families: {len(leet['visualArchetypes'])}",
        f"- MonsterData IDs: {markdown_list(leet['monsterData'])}",
        f"- CATMesh IDs: {markdown_list(leet['catMeshes'])}",
        f"- Official names: {markdown_list(leet['officialNames'])}",
        f"- Captured names: {markdown_list(leet['observedNames'])}",
        f"- Captured level range: {leet['levelMinimum']}..{leet['levelMaximum']}",
        "- ACG placement count: unresolved because the official client does not join ACGHash to MonsterData.",
        "",
        "The literal `leet` token is used only to seed known records. The case study then expands through shared "
        "client visual families, allowing Beach Leet, Leet, Eleet, Soleet, and named variants to converge when "
        "their actual resources converge and remain separate when their resources differ.",
        "",
        "## Heckler case study",
        "",
        f"- Base-model families: {len(heckler['baseModelFamilies'])}",
        f"- Complete visual archetypes in those families: {len(heckler['visualArchetypes'])}",
        f"- MonsterData IDs: {markdown_list(heckler['monsterData'])}",
        f"- CATMesh IDs: {markdown_list(heckler['catMeshes'])}",
        f"- Official names: {markdown_list(heckler['officialNames'])}",
        f"- Captured names: {markdown_list(heckler['observedNames'])}",
        f"- Captured level range: {heckler['levelMinimum']}..{heckler['levelMaximum']}",
        "- ACG placement count: unresolved for the same official-source boundary.",
        "",
        "## Top 20 reused complete visual signatures",
        "",
        "| Archetype | Official records | Captures | MonsterData | Names |",
        "| --- | ---: | ---: | --- | --- |",
    ]
    for row in result["top"]:
        lines.append(
            f"| `{row['archetypeId']}` | {row['officialVisualRecords']} | {row['capturedObservations']} | "
            f"{markdown_list(row['monsterData'], 5)} | {markdown_list(row['officialNames'], 4)} |"
        )
    placement = summary["placementAssociation"]
    captures = summary["captureObservationAssociation"]
    lines.extend(
        [
            "",
            "## ACG placement association",
            "",
            f"- Direct official: {placement['directOfficial']}",
            f"- Indirect official: {placement['indirectOfficial']}",
            f"- Ambiguous: {placement['ambiguous']}",
            f"- Unresolved: {placement['unresolved']}",
            "",
            "The unresolved result is evidence, not a census failure: the client retains placement records while "
            "the server supplies the live MonsterData selector independently.",
            "",
            "## Capture overlay",
            "",
            f"- Captured NPC observations: {summary['capturedNpcObservations']}",
            f"- Unique archetype: {captures['uniqueArchetype']}",
            f"- Ambiguous archetype: {captures['ambiguousArchetype']}",
            f"- Unknown archetype: {captures['unknownArchetype']}",
            "",
            "Runtime observations resolve through stable MonsterData/CATMesh evidence without requiring an exact "
            "placement. Runtime identities are retained only as observation provenance.",
            "",
            "## Deduplication",
            "",
            f"The {summary['officialNpcVisualRecords']} MonsterData records reduce to "
            f"{summary['uniqueCompleteVisualSignatures']} exact reusable visual variants and "
            f"{summary['baseModelFamilies']} broader structural families. Median MonsterData records per visual "
            f"variant is {summary['monsterDataPerVisualVariantMedian']}; maximum is "
            f"{summary['monsterDataPerVisualVariantMax']}.",
            "",
            "Placement-per-archetype statistics remain `not observed` because the official ACG-to-model association "
            "is absent. They are not reported as zero.",
            "",
            "## Remaining unknown relationships",
            "",
            "- Exact enemy-versus-friendly/structure ownership for every MonsterData record.",
            "- The Funcom server/tooling ACGHash-to-MonsterData association.",
            "- Four CATMesh records that the available AODB decoder cannot parse.",
            "- MonsterData group map 2 and some grouped animation/effect semantics.",
            "- Server-authored body, breed, gender, equipment, and texture overrides absent from MonsterData.",
            "",
            "## Acceptance",
            "",
            "```text",
            "ENEMY_ARCHETYPE_CENSUS_IMPLEMENTED=YES",
            f"OFFICIAL_PLACEMENTS={summary['officialPlacements']}",
            f"OFFICIAL_NPC_VISUAL_RECORDS={summary['officialNpcVisualRecords']}",
            f"UNIQUE_NAMES={summary['uniqueNames']}",
            f"UNIQUE_MONSTER_DATA={summary['uniqueMonsterData']}",
            f"UNIQUE_CAT_MESH={summary['uniqueCatMesh']}",
            f"UNIQUE_MESH_SIGNATURES={summary['uniqueMeshSignatures']}",
            f"UNIQUE_TEXTURE_SIGNATURES={summary['uniqueTextureSignatures']}",
            f"UNIQUE_COMPLETE_VISUAL_SIGNATURES={summary['uniqueCompleteVisualSignatures']}",
            f"BASE_MODEL_FAMILIES={summary['baseModelFamilies']}",
            f"VISUAL_VARIANTS={summary['visualVariants']}",
            f"GAMEPLAY_VARIANTS_IDENTIFIED={summary['gameplayVariantsIdentified']}",
            f"LEET_VISUAL_ARCHETYPES={len(leet['visualArchetypes'])}",
            f"LEET_NAMES={len(set(leet['officialNames'] + leet['observedNames']))}",
            "LEET_PLACEMENTS=NOT_OBSERVED_OFFICIAL_JOIN",
            f"LEET_PLAYFIELDS={len(set(leet['resourcePlayfields'] + leet['runtimePlayfields']))}",
            f"HECKLER_VISUAL_ARCHETYPES={len(heckler['visualArchetypes'])}",
            f"HECKLER_NAMES={len(set(heckler['officialNames'] + heckler['observedNames']))}",
            "HECKLER_PLACEMENTS=NOT_OBSERVED_OFFICIAL_JOIN",
            f"HECKLER_PLAYFIELDS={len(set(heckler['resourcePlayfields'] + heckler['runtimePlayfields']))}",
            f"ACG_PLACEMENTS_DIRECT_TO_ARCHETYPE={placement['directOfficial']}",
            f"ACG_PLACEMENTS_INDIRECT_TO_ARCHETYPE={placement['indirectOfficial']}",
            f"ACG_PLACEMENTS_AMBIGUOUS={placement['ambiguous']}",
            f"ACG_PLACEMENTS_UNRESOLVED={placement['unresolved']}",
            f"CAPTURED_NPC_OBSERVATIONS={summary['capturedNpcObservations']}",
            f"CAPTURE_OBSERVATIONS_UNIQUE_ARCHETYPE={captures['uniqueArchetype']}",
            f"CAPTURE_OBSERVATIONS_AMBIGUOUS_ARCHETYPE={captures['ambiguousArchetype']}",
            f"CAPTURE_OBSERVATIONS_UNKNOWN_ARCHETYPE={captures['unknownArchetype']}",
            "RUNTIME_TO_EXACT_ACG_REQUIRED_FOR_ARCHETYPE=NO",
            "ACGHASH_USED_AS_RUNTIME_IDENTITY=NO",
            "NAMES_USED_AS_ARCHETYPE_IDENTITY=NO",
            "LEVEL_USED_AS_ARCHETYPE_IDENTITY=NO",
            "LOOT_USED_AS_VISUAL_ARCHETYPE_IDENTITY=NO",
            f"DETERMINISTIC_DIGEST={summary['deterministicDigest']}",
            "```",
            "",
        ]
    )
    return "\n".join(lines)


def render_outputs(result: Mapping[str, Any]) -> dict[str, str]:
    return {
        "enemy-archetype-census-summary.json": pretty_json(result["summary"]),
        "enemy-archetype-catalog.json": pretty_json(result["catalog"]),
        "runtime-observation-archetype-associations.json": pretty_json(
            {
                "schemaVersion": 1,
                "deterministicDigest": result["summary"]["deterministicDigest"],
                "observations": result["runtime"],
            }
        ),
        "top-reused-visual-signatures.json": pretty_json(
            {
                "schemaVersion": 1,
                "deterministicDigest": result["summary"]["deterministicDigest"],
                "top20": result["top"],
            }
        ),
        "leet-case-study.json": pretty_json(result["leet"]),
        "heckler-case-study.json": pretty_json(result["heckler"]),
        "acg-placement-archetype-associations.csv": render_placement_csv(result["placements"]),
        "enemy-archetype-census-report.md": render_report(result),
    }


def write_or_check(outputs: Mapping[str, str], output_root: Path, check: bool) -> None:
    if check:
        failures = []
        for name, content in outputs.items():
            path = output_root / name
            if not path.is_file():
                failures.append(f"missing {path}")
            elif path.read_text(encoding="utf-8-sig") != content:
                failures.append(f"stale {path}")
        if failures:
            raise CensusError("Generated census is not current: " + "; ".join(failures))
        return
    output_root.mkdir(parents=True, exist_ok=True)
    for name, content in outputs.items():
        (output_root / name).write_text(content, encoding="utf-8", newline="")


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--placement-index", type=Path, default=DEFAULT_PLACEMENT_INDEX)
    parser.add_argument("--observation-root", type=Path, default=DEFAULT_OBSERVATION_ROOT)
    parser.add_argument("--output-root", type=Path, default=DEFAULT_OUTPUT_ROOT)
    parser.add_argument("--check", action="store_true")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv or sys.argv[1:])
    try:
        source = load_json(args.source)
        placements = load_placements(args.placement_index)
        observations = load_json(args.observation_root / "npc-observations.json")
        harvester_summary = load_json(args.observation_root / "summary.json")
        result = build_census(source, placements, observations, harvester_summary)
        outputs = render_outputs(result)
        write_or_check(outputs, args.output_root, args.check)
    except CensusError as exc:
        print(f"ENEMY_ARCHETYPE_CENSUS=FAIL\nERROR={exc}", file=sys.stderr)
        return 1

    summary = result["summary"]
    capture = summary["captureObservationAssociation"]
    print("ENEMY_ARCHETYPE_CENSUS=PASS")
    print(f"OFFICIAL_NPC_VISUAL_RECORDS={summary['officialNpcVisualRecords']}")
    print(f"UNIQUE_COMPLETE_VISUAL_SIGNATURES={summary['uniqueCompleteVisualSignatures']}")
    print(f"BASE_MODEL_FAMILIES={summary['baseModelFamilies']}")
    print(f"CAPTURED_NPC_OBSERVATIONS={summary['capturedNpcObservations']}")
    print(f"CAPTURE_OBSERVATIONS_UNIQUE_ARCHETYPE={capture['uniqueArchetype']}")
    print(f"DETERMINISTIC_DIGEST={summary['deterministicDigest']}")
    print(f"MODE={'check' if args.check else 'write'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
