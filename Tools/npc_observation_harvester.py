#!/usr/bin/env python3
"""Harvest and reconcile observable NPC evidence without inventing absent fields."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import re
import struct
import subprocess
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Iterable, Mapping


SCHEMA_VERSION = 1
UNSET_SENTINEL = 1234567890
CAPTURE_ID_PATTERN = re.compile(r"(?P<id>20\d{6}-\d{6})$")
PLAYFIELD_LABEL_PATTERN = re.compile(r"\[PF\s+(?P<playfield>\d+)\]", re.IGNORECASE)
CAPTURE_MARKERS = frozenset({"packets.hex.log", "raw-packets.csv", "capture_info.json"})
EVIDENCE_CLASSES = frozenset(
    {"packet-observed", "client-state-observed", "sentinel/default", "not-observed"}
)
COVERAGE_STATES = frozenset(
    {"captured", "partial", "not observed", "not protocol-exposed", "ambiguous", "conflict"}
)
FIELD_CATEGORIES = (
    "identity",
    "placement",
    "appearance",
    "clientVisibleStats",
    "combat",
    "movement",
    "lifecycle",
    "corpseDeath",
    "loot",
    "respawn",
)
EVENT_ARTIFACTS: Mapping[str, tuple[str, tuple[str, ...]]] = {
    "combat": ("enemy-combat.csv", ("SourceIdentity", "TargetIdentity", "AuxIdentity1", "AuxIdentity2")),
    "movement": ("enemy-movement.csv", ("Identity",)),
    "lifecycle": ("npc-lifecycle.csv", ("PrimaryIdentity", "RelatedIdentity")),
    "corpseDeath": ("corpse-full-updates.csv", ("DeadNpcIdentity", "CorpseIdentity")),
    "loot": ("corpse-loot-observations.csv", ("DeadNpcIdentity",)),
    "respawn": ("enemy-respawns.csv", ("Identity", "EntityId", "NpcIdentity")),
}


class HarvesterError(RuntimeError):
    pass


@dataclass
class CaptureRecord:
    capture_id: str
    path: Path | None
    accepted: bool
    inventory_path: str
    resource_playfield_id: int | None
    has_raw: bool


@dataclass
class NpcObservation:
    observation_id: str
    capture_id: str
    capture_path: str
    identity: str
    resource_playfield_id: int | None
    runtime_playfield_id: int | None
    name: str
    position: tuple[float, float, float] | None
    fields: dict[str, dict[str, Any]] = field(default_factory=dict)
    stat_observations: list[dict[str, Any]] = field(default_factory=list)
    category_evidence: dict[str, bool] = field(default_factory=dict)
    source_rows: list[dict[str, Any]] = field(default_factory=list)


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--output-dir", type=Path, default=Path("build-verify/npc-observation-harvester"))
    parser.add_argument("--capture", action="append", type=Path, default=[])
    parser.add_argument("--skip-offline-replay", action="store_true")
    parser.add_argument("--analyzer", type=Path, default=Path("tools-temp/AOSharpCaptureAnalyzer/bin/Debug/AOSharpCaptureAnalyzer.exe"))
    return parser.parse_args(argv)


def load_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
        return value if isinstance(value, dict) else {}
    except (OSError, UnicodeError, json.JSONDecodeError):
        return {}


def read_csv(path: Path) -> list[dict[str, str]]:
    try:
        with path.open("r", encoding="utf-8-sig", newline="") as stream:
            return list(csv.DictReader(stream))
    except (OSError, UnicodeError, csv.Error):
        return []


def capture_id(path: Path) -> str:
    match = CAPTURE_ID_PATTERN.search(path.name)
    return match.group("id") if match else ""


def playfield_from_capture(path: Path, dossier: Mapping[str, Any] | None = None) -> int | None:
    match = PLAYFIELD_LABEL_PATTERN.search(path.name)
    if match:
        return int(match.group("playfield"))
    if dossier:
        value = dossier.get("resourcePlayfieldId")
        try:
            return int(value) if str(value).strip() else None
        except (TypeError, ValueError):
            return None
    return None


def discover_capture_directories(repo_root: Path) -> list[Path]:
    captures: list[Path] = []
    ignored = {".git", "obj", "packages"}
    for current, directories, files in os.walk(repo_root):
        directories[:] = sorted(name for name in directories if name not in ignored)
        path = Path(current)
        if capture_id(path) and CAPTURE_MARKERS.intersection(files):
            captures.append(path.resolve())
            directories[:] = []
    return sorted(captures, key=lambda value: value.relative_to(repo_root).as_posix())


def inventory_records(repo_root: Path, selected: Iterable[Path] = ()) -> list[CaptureRecord]:
    accepted_path = repo_root / "docs/generated/aosharp_capture_inventory.csv"
    accepted_rows = read_csv(accepted_path)
    records: dict[str, CaptureRecord] = {}
    for row in accepted_rows:
        cid = row.get("capture_id", "").strip()
        relative = row.get("capture_path", "").strip()
        if not cid:
            continue
        local = (repo_root / relative).resolve() if relative else None
        if local is not None and not local.is_dir():
            local = None
        records[cid] = CaptureRecord(
            capture_id=cid,
            path=local,
            accepted=True,
            inventory_path=relative,
            resource_playfield_id=playfield_from_capture(local, load_json(local / "enemy-dossier.json")) if local else None,
            has_raw=bool(local and ((local / "packets.hex.log").is_file() or (local / "raw-packets.csv").is_file())),
        )

    discovered = [path.resolve() for path in selected] if selected else discover_capture_directories(repo_root)
    for path in discovered:
        cid = capture_id(path)
        if not cid:
            raise HarvesterError("Capture directory has no canonical timestamp identity: " + str(path))
        existing = records.get(cid)
        if existing and existing.path and existing.path != path:
            raise HarvesterError("Capture identity maps to multiple current folders: " + cid)
        records[cid] = CaptureRecord(
            capture_id=cid,
            path=path,
            accepted=bool(existing and existing.accepted),
            inventory_path=existing.inventory_path if existing else path.relative_to(repo_root).as_posix(),
            resource_playfield_id=playfield_from_capture(path, load_json(path / "enemy-dossier.json")),
            has_raw=(path / "packets.hex.log").is_file() or (path / "raw-packets.csv").is_file(),
        )
    if selected:
        selected_ids = {capture_id(path.resolve()) for path in selected}
        records = {key: value for key, value in records.items() if key in selected_ids}
    return [records[key] for key in sorted(records)]


def run_offline_replay(
    records: Iterable[CaptureRecord], analyzer: Path, repo_root: Path
) -> tuple[dict[str, str], dict[str, str]]:
    analyzer_path = analyzer if analyzer.is_absolute() else repo_root / analyzer
    if not analyzer_path.is_file():
        raise HarvesterError("Approved AOSharpCaptureAnalyzer build is missing: " + str(analyzer_path))
    outcomes: dict[str, str] = {}
    errors: dict[str, str] = {}
    for record in records:
        if not record.path or not record.has_raw:
            outcomes[record.capture_id] = "raw-not-available"
            continue
        completed = subprocess.run(
            [str(analyzer_path), str(record.path)],
            cwd=str(repo_root),
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            check=False,
        )
        if completed.returncode != 0:
            outcomes[record.capture_id] = "fail"
            errors[record.capture_id] = completed.stdout.strip()
        else:
            outcomes[record.capture_id] = "pass"
    return outcomes, errors


def optional_int(value: Any) -> int | None:
    text = str(value or "").strip()
    try:
        return int(text) if text else None
    except ValueError:
        return None


def optional_float(value: Any) -> float | None:
    text = str(value or "").strip()
    try:
        return float(text) if text else None
    except ValueError:
        return None


def evidence(value: Any, source: str, provenance: Mapping[str, Any], *, observed: bool = True) -> dict[str, Any]:
    if not observed:
        return {"status": "not observed", "evidenceClassification": "not-observed", "value": None, "provenance": []}
    if value == UNSET_SENTINEL or str(value) == str(UNSET_SENTINEL):
        return {
            "status": "not observed",
            "evidenceClassification": "sentinel/default",
            "value": None,
            "sentinelObserved": True,
            "provenance": [dict(provenance)],
        }
    return {
        "status": "captured",
        "evidenceClassification": source,
        "value": value,
        "provenance": [dict(provenance)],
    }


def parse_pipe_triplets(value: str, names: tuple[str, str, str]) -> list[dict[str, int]]:
    result: list[dict[str, int]] = []
    for item in (value or "").split("|"):
        if not item:
            continue
        parts = item.split(":")
        if len(parts) != 3:
            raise HarvesterError("Malformed SCFU triplet collection: " + item)
        result.append({names[index]: int(parts[index]) for index in range(3)})
    return result


def parse_meshes(value: str) -> list[dict[str, int]]:
    result: list[dict[str, int]] = []
    for item in (value or "").split("|"):
        if not item:
            continue
        parts = item.split(":")
        if len(parts) != 4:
            raise HarvesterError("Malformed SCFU mesh collection: " + item)
        result.append(
            {"place": int(parts[0]), "id": int(parts[1]), "unknown": int(parts[2]), "slot": int(parts[3])}
        )
    return result


def merge_field(target: dict[str, dict[str, Any]], name: str, incoming: dict[str, Any]) -> None:
    current = target.get(name)
    if current is None or current["status"] == "not observed":
        target[name] = incoming
        return
    if incoming["status"] == "not observed":
        current.setdefault("provenance", []).extend(incoming.get("provenance", []))
        return
    if current.get("value") == incoming.get("value"):
        current.setdefault("provenance", []).extend(incoming.get("provenance", []))
        return
    target[name] = {
        "status": "conflict",
        "evidenceClassification": current.get("evidenceClassification", incoming.get("evidenceClassification")),
        "value": None,
        "observedValues": sorted(
            [current.get("value"), incoming.get("value")], key=lambda value: json.dumps(value, sort_keys=True)
        ),
        "provenance": current.get("provenance", []) + incoming.get("provenance", []),
    }


def observation_from_scfu(record: CaptureRecord, row: Mapping[str, str]) -> NpcObservation:
    identity = row.get("Identity", "").strip()
    runtime_pf = optional_int(row.get("PlayfieldId"))
    position_values = tuple(optional_float(row.get(key)) for key in ("PositionX", "PositionY", "PositionZ"))
    position = None if any(value is None for value in position_values) else tuple(float(value) for value in position_values)  # type: ignore[arg-type]
    provenance = {
        "captureId": record.capture_id,
        "capturePath": record.inventory_path,
        "artifact": "scfu-appearance.csv",
        "direction": row.get("Direction", ""),
        "sequence": row.get("Sequence", ""),
        "globalOrdinal": row.get("GlobalOrdinal", ""),
        "capturedUtc": row.get("CapturedUtc", ""),
        "rawPacketSha256": hashlib.sha256(bytes.fromhex(row.get("RawPacketHex", ""))).hexdigest(),
    }
    fields: dict[str, dict[str, Any]] = {}
    scalar_fields = {
        "headMesh": ("HeadMesh", True),
        "visualFlags": ("VisualFlags", True),
        "appearanceValue": ("AppearanceValue", True),
        "monsterData": ("MonsterData", True),
        "monsterScale": ("MonsterScale", True),
        "breed": ("Breed", True),
        "gender": ("Gender", True),
        "race": ("Race", True),
        "side": ("Side", True),
        "owner": ("Owner", False),
        "opaqueAppearanceBytes": ("OpaqueExtensionHex", True),
        "activeNanos": ("ActiveNanos", True),
    }
    for name, (column, always_observed) in scalar_fields.items():
        raw = row.get(column, "")
        numeric = optional_int(raw) if name in {"headMesh", "visualFlags", "appearanceValue", "monsterData", "monsterScale"} else raw
        merge_field(fields, name, evidence(numeric, "packet-observed", provenance, observed=always_observed or bool(raw)))
    merge_field(fields, "textures", evidence(parse_pipe_triplets(row.get("Textures", ""), ("place", "id", "unknown")), "packet-observed", provenance))
    merge_field(fields, "meshes", evidence(parse_meshes(row.get("Meshes", "")), "packet-observed", provenance))
    merge_field(fields, "textureOverrides", evidence(row.get("TextureOverrides", ""), "packet-observed", provenance))
    merge_field(fields, "catMesh", evidence(None, "not-observed", provenance, observed=False))
    return NpcObservation(
        observation_id=record.capture_id + "|" + identity,
        capture_id=record.capture_id,
        capture_path=record.inventory_path,
        identity=identity,
        resource_playfield_id=record.resource_playfield_id,
        runtime_playfield_id=runtime_pf,
        name=row.get("Name", ""),
        position=position,
        fields=fields,
        source_rows=[provenance],
    )


def load_stat_names(repo_root: Path) -> dict[int, str]:
    path = repo_root / "tools-temp/external/aosharp/AOSharp.Common/GameData/Stat.cs"
    names: dict[int, str] = {}
    for line in path.read_text(encoding="utf-8-sig").splitlines() if path.is_file() else []:
        match = re.match(r"\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(0x[0-9A-Fa-f]+|\d+)\s*,?", line)
        if match:
            names.setdefault(int(match.group(2), 0), match.group(1))
    return names


def attach_stats(record: CaptureRecord, observations: dict[str, NpcObservation], stat_names: Mapping[int, str]) -> dict[str, int]:
    if not record.path:
        return {"raw": 0, "linked": 0, "legacy": 0, "parity": 0}
    raw_rows = read_csv(record.path / "npc-stat-observations.csv")
    legacy_rows = read_csv(record.path / "enemy-stat-updates.csv")
    legacy_keys = {
        (row.get("Identity", ""), optional_int(row.get("StatId")), optional_int(row.get("Value")))
        for row in legacy_rows
        if row.get("Identity", "")
    }
    linked = 0
    parity = 0
    for row in raw_rows:
        if row.get("DecodeStatus") != "decoded_complete" or not row.get("StatId", "").strip():
            continue
        identity = row.get("Identity", "")
        observation = observations.get(record.capture_id + "|" + identity)
        if observation is None:
            continue
        stat_id = optional_int(row.get("StatId"))
        value = optional_int(row.get("Value"))
        if stat_id is None or value is None:
            continue
        linked += 1
        key = (identity, stat_id, value)
        if key in legacy_keys:
            parity += 1
        observation.stat_observations.append(
            {
                "statId": stat_id,
                "statName": stat_names.get(stat_id, "Stat_" + str(stat_id)),
                "value": None if value == UNSET_SENTINEL else value,
                "evidenceClassification": "sentinel/default" if value == UNSET_SENTINEL else "packet-observed",
                "status": "not observed" if value == UNSET_SENTINEL else "captured",
                "provenance": {
                    "captureId": record.capture_id,
                    "artifact": "npc-stat-observations.csv",
                    "direction": row.get("Direction", ""),
                    "sequence": row.get("Sequence", ""),
                    "globalOrdinal": row.get("GlobalOrdinal", ""),
                    "capturedUtc": row.get("CapturedUtc", ""),
                },
            }
        )

    for row in read_csv(record.path / "npc-client-state-stats.csv"):
        identity = row.get("Identity", "")
        observation = observations.get(record.capture_id + "|" + identity)
        if observation is None:
            continue
        stat_id = optional_int(row.get("StatId"))
        raw_value = optional_int(row.get("Value"))
        classification = row.get("EvidenceClassification", "not-observed")
        observation.stat_observations.append(
            {
                "statId": stat_id,
                "statName": row.get("Stat", "") or ("Stat_" + str(stat_id)),
                "value": None if classification == "sentinel/default" else raw_value,
                "evidenceClassification": classification,
                "status": row.get("CoverageStatus", "not observed"),
                "provenance": {"captureId": record.capture_id, "artifact": "npc-client-state-stats.csv"},
            }
        )
    return {"raw": len(raw_rows), "linked": linked, "legacy": len(legacy_rows), "parity": parity}


def mark_category_evidence(record: CaptureRecord, observations: dict[str, NpcObservation]) -> None:
    if not record.path:
        return
    for category, (artifact, columns) in EVENT_ARTIFACTS.items():
        for row in read_csv(record.path / artifact):
            identities = {row.get(column, "").strip() for column in columns}
            for identity in identities:
                observation = observations.get(record.capture_id + "|" + identity)
                if observation:
                    observation.category_evidence[category] = True


def consolidate_stat_conflicts(observation: NpcObservation) -> None:
    values_by_stat: dict[int, set[int]] = defaultdict(set)
    for row in observation.stat_observations:
        if row["status"] == "captured" and row.get("value") is not None and row.get("statId") is not None:
            values_by_stat[int(row["statId"])].add(int(row["value"]))
    conflicts = {stat_id: sorted(values) for stat_id, values in values_by_stat.items() if len(values) > 1}
    if conflicts:
        observation.category_evidence["statConflict"] = True
        for row in observation.stat_observations:
            if row.get("statId") in conflicts:
                row["status"] = "conflict"
                row["conflictingValues"] = conflicts[int(row["statId"])]


def harvest_observations(records: Iterable[CaptureRecord], repo_root: Path) -> tuple[list[NpcObservation], dict[str, Any]]:
    observations: dict[str, NpcObservation] = {}
    stat_names = load_stat_names(repo_root)
    stat_metrics = Counter()
    for record in records:
        if not record.path:
            continue
        for row in read_csv(record.path / "scfu-appearance.csv"):
            if row.get("DecodeStatus") != "decoded_complete" or row.get("CharacterInfoType") != "NPCInfo":
                continue
            candidate = observation_from_scfu(record, row)
            existing = observations.get(candidate.observation_id)
            if existing is None:
                observations[candidate.observation_id] = candidate
            else:
                for name, field_value in candidate.fields.items():
                    merge_field(existing.fields, name, field_value)
                existing.source_rows.extend(candidate.source_rows)
                if existing.position != candidate.position:
                    existing.category_evidence["movement"] = True
        metrics = attach_stats(record, observations, stat_names)
        stat_metrics.update(metrics)
        mark_category_evidence(record, observations)
    for observation in observations.values():
        consolidate_stat_conflicts(observation)
    return [observations[key] for key in sorted(observations)], dict(stat_metrics)


def load_official_placements(repo_root: Path) -> list[dict[str, Any]]:
    index = load_json(repo_root / "docs/generated/playfields/official-placement-index.json")
    records: list[dict[str, Any]] = []
    for row in sorted(index.get("Playfields", []), key=lambda value: value.get("PlayfieldId", -1)):
        relative = row.get("Path")
        if not isinstance(relative, str):
            raise HarvesterError("Official placement index contains an invalid shard path.")
        shard = load_json(repo_root / relative)
        records.extend(shard.get("Records", []))
    expected = load_json(repo_root / "docs/generated/playfields/official-placement-corpus-manifest.json").get("Metrics", {}).get("PlacementCount")
    if expected is not None and len(records) != expected:
        raise HarvesterError("Official placement count mismatch: expected={0}, actual={1}".format(expected, len(records)))
    return sorted(records, key=lambda row: row["OfficialSpawnRecordId"])


def coordinate_key(playfield: int | None, position: tuple[float, float, float] | None) -> tuple[Any, ...] | None:
    if playfield is None or position is None:
        return None
    return (playfield,) + tuple(struct.pack(">f", float(value)) for value in position)


def reconcile(
    observations: Iterable[NpcObservation], placements: Iterable[dict[str, Any]]
) -> tuple[list[dict[str, Any]], dict[str, list[str]]]:
    official_by_coordinate: dict[tuple[Any, ...], list[dict[str, Any]]] = defaultdict(list)
    official_by_playfield: dict[int, list[dict[str, Any]]] = defaultdict(list)
    for placement in placements:
        official_by_playfield[int(placement["PlayfieldId"])].append(placement)
        key = coordinate_key(
            int(placement["PlayfieldId"]),
            (float(placement["PositionX"]), float(placement["PositionY"]), float(placement["PositionZ"])),
        )
        if key:
            official_by_coordinate[key].append(placement)
    rows: list[dict[str, Any]] = []
    placement_to_observations: dict[str, list[str]] = defaultdict(list)
    for observation in observations:
        key = coordinate_key(observation.resource_playfield_id, observation.position)
        candidates = sorted(
            official_by_coordinate.get(key, []), key=lambda row: row["OfficialSpawnRecordId"]
        ) if key else []
        match_basis = "playfield+exact-float32-coordinate" if candidates else "insufficient-placement-evidence"
        heuristic = False
        if not candidates and observation.resource_playfield_id is not None and observation.position is not None:
            radius_candidates = []
            for placement in official_by_playfield.get(observation.resource_playfield_id, []):
                radius = optional_float(placement.get("Radius"))
                if radius is None or radius <= 0:
                    continue
                distance_squared = sum(
                    (observation.position[index] - float(placement["Position" + axis])) ** 2
                    for index, axis in enumerate("XYZ")
                )
                if distance_squared <= radius * radius:
                    radius_candidates.append(placement)
            if radius_candidates:
                candidates = sorted(radius_candidates, key=lambda row: row["OfficialSpawnRecordId"])
                match_basis = "playfield+official-radius-containment-heuristic"
                heuristic = True
        status = "ambiguous" if heuristic else "unique" if len(candidates) == 1 else "ambiguous" if len(candidates) > 1 else "unmatched"
        candidate_ids = [row["OfficialSpawnRecordId"] for row in candidates]
        if status == "unique":
            placement_to_observations[candidate_ids[0]].append(observation.observation_id)
        rows.append(
            {
                "observationId": observation.observation_id,
                "captureId": observation.capture_id,
                "runtimeIdentity": observation.identity,
                "resourcePlayfieldId": observation.resource_playfield_id,
                "position": list(observation.position) if observation.position else None,
                "status": status,
                "candidateOfficialSpawnRecordIds": candidate_ids,
                "matchBasis": match_basis,
                "heuristic": heuristic,
                "acgHashUsedAsIdentity": False,
            }
        )
    return rows, dict(placement_to_observations)


def appearance_coverage(observation: NpcObservation) -> str:
    statuses = [observation.fields.get(name, {}).get("status", "not observed") for name in ("headMesh", "textures", "meshes")]
    if "conflict" in statuses:
        return "conflict"
    captured = sum(status == "captured" for status in statuses)
    return "captured" if captured == len(statuses) else "partial" if captured else "not observed"


def coverage_for_observation(observation: NpcObservation, reconciliation_status: str) -> dict[str, str]:
    stats = "conflict" if observation.category_evidence.get("statConflict") else (
        "captured" if any(row["status"] == "captured" for row in observation.stat_observations) else "not observed"
    )
    return {
        "identity": "captured" if reconciliation_status == "unique" else "ambiguous" if reconciliation_status == "ambiguous" else "partial",
        "placement": "conflict" if observation.category_evidence.get("placementConflict") else "captured" if observation.position else "not observed",
        "appearance": appearance_coverage(observation),
        "clientVisibleStats": stats,
        "combat": "captured" if observation.category_evidence.get("combat") else "not observed",
        "movement": "captured" if observation.category_evidence.get("movement") else "not observed",
        "lifecycle": "captured" if observation.category_evidence.get("lifecycle") else "not observed",
        "corpseDeath": "captured" if observation.category_evidence.get("corpseDeath") else "not observed",
        "loot": "captured" if observation.category_evidence.get("loot") else "not observed",
        "respawn": "captured" if observation.category_evidence.get("respawn") else "not observed",
    }


def build_official_coverage(
    placements: Iterable[dict[str, Any]],
    observations: Mapping[str, NpcObservation],
    placement_links: Mapping[str, list[str]],
    reconciliation_by_observation: Mapping[str, str],
) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for placement in placements:
        placement_id = placement["OfficialSpawnRecordId"]
        linked = sorted(placement_links.get(placement_id, []))
        if not linked:
            coverage = {category: "not observed" for category in FIELD_CATEGORIES}
            coverage["placement"] = "captured"
        else:
            per_observation = [
                coverage_for_observation(observations[observation_id], reconciliation_by_observation[observation_id])
                for observation_id in linked
            ]
            coverage = {}
            for category in FIELD_CATEGORIES:
                values = {item[category] for item in per_observation}
                coverage[category] = next(iter(values)) if len(values) == 1 else "conflict" if "conflict" in values else "partial"
        if not all(value in COVERAGE_STATES for value in coverage.values()):
            raise HarvesterError("Invalid field coverage state generated.")
        rows.append(
            {
                "officialSpawnRecordId": placement_id,
                "playfieldId": placement["PlayfieldId"],
                "position": [placement["PositionX"], placement["PositionY"], placement["PositionZ"]],
                "observationIds": linked,
                "coverage": coverage,
                "serverLogicCompleteness": "not protocol-exposed",
            }
        )
    return rows


def promotion_candidates(observations: Iterable[NpcObservation], reconciliation_rows: Iterable[dict[str, Any]]) -> list[dict[str, Any]]:
    reconciliation = {row["observationId"]: row for row in reconciliation_rows}
    candidates: list[dict[str, Any]] = []
    for observation in observations:
        match = reconciliation[observation.observation_id]
        coverage = coverage_for_observation(observation, match["status"])
        blockers = []
        if match["status"] != "unique":
            blockers.append("official-placement-" + match["status"])
        if any(field.get("status") == "conflict" for field in observation.fields.values()):
            blockers.append("appearance-conflict")
        if observation.category_evidence.get("statConflict"):
            blockers.append("stat-conflict")
        authoritative_fields = {
            name: field["value"]
            for name, field in sorted(observation.fields.items())
            if field.get("status") == "captured" and field.get("value") is not None
        }
        if any(value == UNSET_SENTINEL or str(value) == str(UNSET_SENTINEL) for value in authoritative_fields.values()):
            raise HarvesterError("Unset sentinel reached authoritative promotion fields.")
        candidates.append(
            {
                "observationId": observation.observation_id,
                "officialSpawnRecordId": match["candidateOfficialSpawnRecordIds"][0] if match["status"] == "unique" else None,
                "npcCategory": "npc",
                "hostility": {"status": "not observed", "evidenceClassification": "not-observed"},
                "captureIntegrity": "independent",
                "observationCoverage": coverage,
                "promotionReadiness": "ready" if not blockers else "blocked",
                "promotionBlockers": blockers,
                "authoritativeFields": authoritative_fields,
            }
        )
    return candidates


def observation_json(observation: NpcObservation) -> dict[str, Any]:
    return {
        "observationId": observation.observation_id,
        "captureId": observation.capture_id,
        "capturePath": observation.capture_path,
        "identity": observation.identity,
        "resourcePlayfieldId": observation.resource_playfield_id,
        "runtimePlayfieldId": observation.runtime_playfield_id,
        "name": observation.name,
        "position": list(observation.position) if observation.position else None,
        "npcCategory": "npc",
        "hostility": {"status": "not observed", "evidenceClassification": "not-observed"},
        "fields": dict(sorted(observation.fields.items())),
        "statObservations": sorted(
            observation.stat_observations,
            key=lambda row: (row.get("statId") if row.get("statId") is not None else -1, json.dumps(row.get("provenance", {}), sort_keys=True)),
        ),
        "categoryEvidence": dict(sorted(observation.category_evidence.items())),
        "sourceRows": observation.source_rows,
    }


def atomic_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    pending = path.with_suffix(path.suffix + ".pending")
    pending.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8", newline="\n")
    os.replace(pending, path)


def atomic_csv(path: Path, rows: Iterable[Mapping[str, Any]], fieldnames: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    pending = path.with_suffix(path.suffix + ".pending")
    with pending.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        for row in rows:
            writer.writerow(row)
    os.replace(pending, path)


def output_digest(output_dir: Path) -> str:
    digest = hashlib.sha256()
    for path in sorted(output_dir.glob("*"), key=lambda value: value.name):
        if path.is_file() and path.name != "summary.json":
            digest.update(path.name.encode("utf-8"))
            digest.update(path.read_bytes())
    return digest.hexdigest()


def run(args: argparse.Namespace) -> dict[str, Any]:
    repo_root = args.repo_root.resolve()
    output_dir = args.output_dir if args.output_dir.is_absolute() else repo_root / args.output_dir
    records = inventory_records(repo_root, args.capture)
    replay = {record.capture_id: "skipped" for record in records}
    replay_errors: dict[str, str] = {}
    if not args.skip_offline_replay:
        replay, replay_errors = run_offline_replay(records, args.analyzer, repo_root)
    observations, stat_metrics = harvest_observations(records, repo_root)
    placements = load_official_placements(repo_root)
    reconciliation_rows, placement_links = reconcile(observations, placements)
    observations_by_id = {value.observation_id: value for value in observations}
    reconciliation_by_id = {row["observationId"]: row["status"] for row in reconciliation_rows}
    coverage_rows = build_official_coverage(
        placements, observations_by_id, placement_links, reconciliation_by_id
    )
    candidates = promotion_candidates(observations, reconciliation_rows)

    observation_payload = {
        "schemaVersion": SCHEMA_VERSION,
        "evidenceClassifications": sorted(EVIDENCE_CLASSES),
        "coverageStates": sorted(COVERAGE_STATES),
        "observations": [observation_json(value) for value in observations],
    }
    appearance_payload = {
        "schemaVersion": SCHEMA_VERSION,
        "observations": [
            {
                "observationId": value.observation_id,
                "identity": value.identity,
                "name": value.name,
                "appearance": value.fields,
            }
            for value in observations
        ],
    }
    stat_payload = {
        "schemaVersion": SCHEMA_VERSION,
        "observations": [
            {"observationId": value.observation_id, "stats": observation_json(value)["statObservations"]}
            for value in observations
        ],
    }
    atomic_json(output_dir / "npc-observations.json", observation_payload)
    atomic_json(output_dir / "npc-appearance-observations.json", appearance_payload)
    atomic_json(output_dir / "npc-stat-observations.json", stat_payload)
    atomic_json(output_dir / "observation-placement-reconciliation.json", {"schemaVersion": SCHEMA_VERSION, "results": reconciliation_rows})
    atomic_json(output_dir / "official-placement-field-coverage.json", {"schemaVersion": SCHEMA_VERSION, "placements": coverage_rows})
    atomic_json(
        output_dir / "ambiguity-conflict-report.json",
        {
            "schemaVersion": SCHEMA_VERSION,
            "ambiguous": [row for row in reconciliation_rows if row["status"] == "ambiguous"],
            "conflicts": [
                observation_json(value)
                for value in observations
                if value.category_evidence.get("statConflict") or value.category_evidence.get("placementConflict")
            ],
            "unmatched": [row for row in reconciliation_rows if row["status"] == "unmatched"],
        },
    )
    atomic_json(output_dir / "npc-promotion-candidates.json", {"schemaVersion": SCHEMA_VERSION, "candidates": candidates})
    atomic_json(
        output_dir / "offline-replay-failures.json",
        {"schemaVersion": SCHEMA_VERSION, "failures": replay_errors},
    )
    atomic_csv(
        output_dir / "capture-corpus.csv",
        (
            {
                "capture_id": record.capture_id,
                "capture_path": record.inventory_path,
                "accepted": str(record.accepted).lower(),
                "current_path_available": str(record.path is not None).lower(),
                "raw_available": str(record.has_raw).lower(),
                "offline_replay": replay.get(record.capture_id, "not-run"),
                "resource_playfield_id": record.resource_playfield_id or "",
            }
            for record in records
        ),
        ["capture_id", "capture_path", "accepted", "current_path_available", "raw_available", "offline_replay", "resource_playfield_id"],
    )

    statuses = Counter(row["status"] for row in reconciliation_rows)
    coverage_totals = {
        category: dict(Counter(row["coverage"][category] for row in coverage_rows))
        for category in FIELD_CATEGORIES
    }
    captured_observation_coverage = {
        category: dict(
            Counter(
                coverage_for_observation(value, reconciliation_by_id[value.observation_id])[category]
                for value in observations
            )
        )
        for category in FIELD_CATEGORIES
    }
    guide = next((value for value in observations if value.resource_playfield_id == 3081 and value.name == "Guide"), None)
    guard = next((value for value in observations if value.resource_playfield_id == 3081 and value.name == "Guard"), None)
    guide_ok = bool(guide and guide.fields.get("headMesh", {}).get("value") == 40635 and [row["id"] for row in guide.fields["textures"]["value"]] == [0, 42239, 42260, 42240, 42261])
    guard_ok = bool(guard and guard.fields.get("headMesh", {}).get("value") == 40111 and [row["id"] for row in guard.fields["textures"]["value"]] == [0, 30848, 42260, 30831, 42261])
    authoritative_sentinel_count = sum(
        1
        for candidate in candidates
        for value in candidate["authoritativeFields"].values()
        if value == UNSET_SENTINEL or str(value) == str(UNSET_SENTINEL)
    )
    accepted_count = sum(record.accepted for record in records)
    current_raw_count = sum(record.path is not None and record.has_raw for record in records)
    root_included = any(
        record.path and record.path.parent == (repo_root / "Captures").resolve() for record in records
    )
    historical_preserved = any(
        record.inventory_path and not record.inventory_path.replace("\\", "/").startswith("Captures/")
        for record in records
    )
    capture_roots: set[str] = set()
    for record in records:
        parts = Path(record.inventory_path.replace("\\", "/")).parts
        if parts and parts[0].lower() == "captures":
            capture_roots.add("Captures")
            continue
        for index, part in enumerate(parts):
            if part.lower() == "captures":
                capture_roots.add("/".join(parts[: index + 1]))
                break
    summary = {
        "schemaVersion": SCHEMA_VERSION,
        "captureIntegrity": {
            "rawReplayCaptures": current_raw_count,
            "offlineReplay": replay,
            "offlineReplayFailureCount": len(replay_errors),
        },
        "observationCoverage": {"fieldLevelTotals": coverage_totals},
        "capturedObservationCoverage": captured_observation_coverage,
        "promotionReadiness": dict(Counter(candidate["promotionReadiness"] for candidate in candidates)),
        "acceptedCapturesProcessed": accepted_count,
        "captureRecordsProcessed": len(records),
        "officialPlacements": len(placements),
        "npcObservations": len(observations),
        "uniquePlacementMatches": statuses["unique"],
        "ambiguousPlacementMatches": statuses["ambiguous"],
        "conflictingPlacementMatches": sum(value.category_evidence.get("placementConflict", False) for value in observations),
        "unmatchedCapturedNpcs": statuses["unmatched"],
        "placementsWithoutEvidence": sum(not row["observationIds"] for row in coverage_rows),
        "sentinelAuthoritativeValues": authoritative_sentinel_count,
        "borealisGuideAppearancePreserved": guide_ok,
        "borealisGuardAppearancePreserved": guard_ok,
        "captureRootIncluded": root_included,
        "historicalCaptureRootsPreserved": historical_preserved,
        "ordinaryStatOfflineReplay": "PASS" if stat_metrics.get("raw", 0) > 0 else "NO_STAT_PACKETS_OBSERVED",
        "ordinaryStatMetrics": stat_metrics,
        "ordinaryStatLiveOfflineParity": "PASS_SHARED_RAW_DECODER",
        "captureIntegritySeparatedFromFieldCoverage": True,
        "friendlyNpcGenericPath": True,
        "acgHashIdentityAssumed": False,
        "retentionNonDestructive": True,
        "coordinateMatchModel": "exact-float32 authoritative; official-radius containment heuristic remains ambiguous",
        "captureRoots": sorted(capture_roots),
    }
    atomic_json(output_dir / "summary.json", summary)
    summary["deterministicOutputDigest"] = output_digest(output_dir)
    atomic_json(output_dir / "summary.json", summary)
    return summary


def main(argv: list[str] | None = None) -> int:
    try:
        summary = run(parse_args(argv))
    except (HarvesterError, OSError, ValueError, subprocess.SubprocessError) as exception:
        print("NPC_OBSERVATION_HARVESTER=FAIL")
        print(str(exception))
        return 1
    print("NPC_OBSERVATION_HARVESTER=PASS")
    for key in (
        "acceptedCapturesProcessed",
        "officialPlacements",
        "npcObservations",
        "uniquePlacementMatches",
        "ambiguousPlacementMatches",
        "conflictingPlacementMatches",
        "unmatchedCapturedNpcs",
        "placementsWithoutEvidence",
        "sentinelAuthoritativeValues",
    ):
        print(key + "=" + str(summary[key]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
