#!/usr/bin/env python3
"""Deterministic cross-reference audit for secondary NPC-combat evidence.

This tool inventories capture artifacts and earlier evidence reports.  It is an
audit surface only: none of the derived reports inspected here are allowed to
create or alter a production combat contract.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import tempfile
import time
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable


REPO_ROOT = Path(__file__).resolve().parents[2]
PRIMARY_CAPTURE_ROOT = (
    REPO_ROOT / "tools-temp" / "AOSharpLiveCapture" / "bin" / "Debug" / "captures"
)
LEGACY_CAPTURE_ROOT = REPO_ROOT / "For Repo"
OUTPUT_PATH = REPO_ROOT / "docs" / "generated" / "capture_backed_npc_secondary_evidence_audit.json"
COMBAT_INVENTORY_PATH = (
    REPO_ROOT / "docs" / "generated" / "capture_backed_npc_combat_inventory.json"
)
SUBWAY_REPORT_PATH = REPO_ROOT / "docs" / "generated" / "subway_enemy_combat_contracts.json"
REQUIRED_EVIDENCE_DOCUMENT_PATHS = (
    SUBWAY_REPORT_PATH,
    REPO_ROOT / "docs" / "project" / "ORDINARY_ENEMY_RUNTIME.md",
    REPO_ROOT / "docs" / "project" / "DAMAGE_EVIDENCE_MATRIX.md",
    REPO_ROOT / "docs" / "evidence" / "CAPTURE_BACKED_NPC_COMBAT_AUDIT_20260722.md",
    REPO_ROOT / "docs" / "generated" / "malfunctioning_cleaning_robot_enemy_20260623_115502.md",
    REPO_ROOT / "docs" / "generated" / "arete_malfunctioning_cleaning_robot_spawn_result.md",
)
EVIDENCE_DISCOVERY_ROOTS = (
    REPO_ROOT / "docs",
    REPO_ROOT / "tools-temp" / "AOSharpCaptureAnalyzer",
    REPO_ROOT / "tools-temp" / "AOSharpCaptureProtocol",
    REPO_ROOT / "tools-temp" / "AOSharpLiveCapture",
    REPO_ROOT / "tools-temp" / "AOSharpLiveInjector",
    REPO_ROOT / "tools",
)
EVIDENCE_DOCUMENT_EXTENSIONS = {".csv", ".json", ".log", ".md", ".txt"}
EVIDENCE_PATH_TOKENS = (
    "aosharp",
    "arete",
    "attack",
    "capture",
    "cleaning-robot",
    "cleaning_robot",
    "combat",
    "corpse",
    "damage",
    "dungeon",
    "enemy",
    "lifecycle",
    "loot",
    "movement",
    "nascence",
    "npc",
    "respawn",
    "subway",
    "temple",
    "weapon",
)
EVIDENCE_CONTENT_RE = re.compile(
    r"\b(?:attack|capture|combat|corpse|damage|enemy|lifecycle|loot|movement|npc|respawn|weapon)\b",
    re.IGNORECASE,
)
EVIDENCE_EXCLUDED_PATHS = {
    COMBAT_INVENTORY_PATH.resolve(),
    OUTPUT_PATH.resolve(),
}

SCHEMA_VERSION = 2
MAX_OUTPUT_BYTES = 10 * 1024 * 1024
CAPTURE_ID_RE = re.compile(r"(?<!\d)(20\d{6}-\d{6})(?!\d)")
SHA256_RE = re.compile(r"^(?:sha256:)?([0-9a-fA-F]{64})$")
CAPTURE_MARKER_NAMES = {
    "capture-session.json",
    "capture_info.json",
    "events.log",
    "packets.hex.log",
    "raw-packets.csv",
}
DERIVED_CAPTURE_MARKER_NAMES = {
    "capture-health.json",
    "corpse-full-updates.csv",
    "corpse-loot-observations.csv",
    "enemy-combat.csv",
    "enemy-dossier.json",
    "enemy-full-updates.csv",
    "enemy-movement.csv",
    "enemy-respawns.csv",
    "enemy-state.csv",
    "enemy-state.json",
    "enemy-stat-updates.csv",
    "inventory-updates.csv",
    "movement-packets.csv",
    "movement-summary.json",
    "npc-lifecycle.csv",
    "npc-lifecycle-summary.json",
    "scfu-appearance.csv",
    "scfu-decode-errors.csv",
}


def relative_path(path: Path) -> str:
    return path.resolve().relative_to(REPO_ROOT.resolve()).as_posix()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while True:
            chunk = stream.read(1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()


def stable_sha256_file(path: Path, attempts: int = 3) -> str:
    error: Exception | None = None
    for attempt in range(attempts):
        try:
            before = path.stat()
            digest = sha256_file(path)
            after = path.stat()
            if (before.st_size, before.st_mtime_ns) == (after.st_size, after.st_mtime_ns):
                return digest
            error = RuntimeError(f"file changed while being hashed: {relative_path(path)}")
        except OSError as exception:
            error = exception
        if attempt + 1 < attempts:
            time.sleep(0.25)
    raise RuntimeError(f"unable to hash stable file {relative_path(path)}: {error}")


def artifact_categories(path: Path) -> tuple[str, ...]:
    """Return every applicable combat-audit category for a capture artifact."""

    name = path.name.lower()
    stem = name.replace("_", "-")
    categories: set[str] = set()

    if name in {"packets.hex.log", "raw-packets.csv"} or stem.startswith("raw-packet"):
        categories.add("raw-packets")

    if (
        name in {"capture_info.json", "capture-session.json", "capture-health.json"}
        or any(token in stem for token in ("manifest", "checksum", "integrity"))
        or stem.startswith("capture-validation")
        or stem.startswith("capture-status")
    ):
        categories.add("capture-manifest")

    if any(
        token in stem
        for token in (
            "npc-lifecycle",
            "respawn",
            "corpse",
            "despawn",
            "death-event",
        )
    ):
        categories.add("lifecycle")

    if any(
        token in stem
        for token in (
            "scfu",
            "simple-char",
            "simplechar",
            "dynel",
            "enemy-state",
            "enemy-full-update",
            "enemy-dossier",
            "enemy-stat-update",
            "appearance",
            "corpse-full-update",
        )
    ):
        categories.add("dynel-state")

    if (
        any(token in stem for token in ("combat", "attack", "fight", "damage", "miss-event", "nano-event"))
        or name in {"system-messages.log", "events.log"}
    ):
        categories.add("combat")

    if any(token in stem for token in ("inventory", "loot", "item-transfer", "item-update")):
        categories.add("inventory-loot")

    if (
        any(token in stem for token in ("weapon", "wifu", "ammo", "special-attack"))
        or name in {"enemy-combat.csv", "inventory-updates.csv"}
    ):
        categories.add("weapon-context")

    if any(
        token in stem
        for token in ("movement", "follow-target", "followtarget", "set-pos", "setpos", "stop-moving")
    ):
        categories.add("movement")

    if any(token in stem for token in ("decode", "decoded")):
        categories.add("decoded-projection")

    if categories and "raw-packets" not in categories and "capture-manifest" not in categories:
        categories.add("decoded-projection")

    return tuple(sorted(categories))


def capture_id_for_session(session: Path) -> str:
    match = CAPTURE_ID_RE.search(session.name)
    return match.group(1) if match else session.name


def normalize_digest(value: Any) -> str | None:
    if not isinstance(value, str):
        return None
    match = SHA256_RE.fullmatch(value.strip())
    return match.group(1).lower() if match else None


def extract_manifest_digests(document: Any) -> dict[str, str]:
    """Recover only explicit SHA-256 values associated with artifact paths."""

    result: dict[str, str] = {}
    path_keys = ("path", "relativePath", "relative_path", "file", "fileName", "filename", "name")
    digest_keys = ("sha256", "sha256Digest", "sha256_digest", "contentSha256", "digest", "hash")

    def visit(node: Any) -> None:
        if isinstance(node, dict):
            artifact_path = next(
                (node[key] for key in path_keys if isinstance(node.get(key), str)),
                None,
            )
            artifact_digest = next(
                (normalize_digest(node.get(key)) for key in digest_keys if normalize_digest(node.get(key))),
                None,
            )
            if artifact_path and artifact_digest:
                normalized = str(artifact_path).replace("\\", "/")
                result[normalized] = artifact_digest
                result[Path(normalized).name] = artifact_digest

            for key, value in node.items():
                direct_digest = normalize_digest(value)
                if direct_digest and isinstance(key, str) and ("." in key or "/" in key or "\\" in key):
                    normalized = key.replace("\\", "/")
                    result[normalized] = direct_digest
                    result[Path(normalized).name] = direct_digest
                visit(value)
        elif isinstance(node, list):
            for value in node:
                visit(value)

    visit(document)
    return result


def manifest_digests_for_session(session: Path, manifest_paths: Iterable[Path]) -> dict[str, str]:
    digests: dict[str, str] = {}
    for path in sorted(set(manifest_paths), key=lambda item: item.as_posix().lower()):
        if path.suffix.lower() != ".json":
            continue
        try:
            document = json.loads(path.read_text(encoding="utf-8-sig"))
        except (OSError, UnicodeDecodeError, json.JSONDecodeError):
            continue
        for key, value in extract_manifest_digests(document).items():
            digests.setdefault(key, value)
    return digests


def hash_record(path: Path, session: Path | None, manifest_digests: dict[str, str]) -> dict[str, Any]:
    content_digest = stable_sha256_file(path)
    candidates = [path.name, relative_path(path)]
    if session is not None:
        candidates.insert(0, path.relative_to(session).as_posix())
    declared_digest: str | None = None
    for candidate in candidates:
        digest = manifest_digests.get(candidate)
        if digest:
            declared_digest = digest
            break
    return {
        "hashStatus": "content-sha256",
        "sha256": content_digest,
        "manifestSha256": declared_digest,
        "manifestDigestMatches": (
            None if declared_digest is None else declared_digest == content_digest
        ),
    }


def immediate_file_names(directory: Path) -> set[str]:
    try:
        return {path.name.lower() for path in directory.iterdir() if path.is_file()}
    except OSError:
        return set()


def discover_main_corpus_sessions() -> list[tuple[Path, str]]:
    """Mirror the capture-directory boundary used by the production extractor."""

    sessions: set[tuple[Path, str]] = set()
    if PRIMARY_CAPTURE_ROOT.exists():
        for path in PRIMARY_CAPTURE_ROOT.rglob("*"):
            if not path.is_dir() or not CAPTURE_ID_RE.fullmatch(path.name):
                continue
            if CAPTURE_MARKER_NAMES.intersection(immediate_file_names(path)):
                sessions.add((path, "primary"))
    if LEGACY_CAPTURE_ROOT.exists():
        sessions.update(
            (packet_log.parent, "legacy-for-repo")
            for packet_log in LEGACY_CAPTURE_ROOT.rglob("packets.hex.log")
        )
    return sorted(sessions, key=lambda value: relative_path(value[0]).lower())


def discover_artifact_bearing_capture_directories() -> set[Path]:
    """Find capture-shaped directories independently of the main-corpus gate."""

    candidates: set[Path] = set()
    if PRIMARY_CAPTURE_ROOT.exists():
        for path in PRIMARY_CAPTURE_ROOT.rglob("*"):
            if not path.is_dir() or not CAPTURE_ID_RE.fullmatch(path.name):
                continue
            names = immediate_file_names(path)
            if (CAPTURE_MARKER_NAMES | DERIVED_CAPTURE_MARKER_NAMES).intersection(names):
                candidates.add(path)
    if LEGACY_CAPTURE_ROOT.exists():
        signal_names = CAPTURE_MARKER_NAMES | DERIVED_CAPTURE_MARKER_NAMES
        for path in LEGACY_CAPTURE_ROOT.rglob("*"):
            if path.is_file() and path.name.lower() in signal_names:
                candidates.add(path.parent)
    return candidates


def discover_artifacts() -> tuple[
    list[dict[str, Any]],
    list[dict[str, Any]],
    set[str],
    dict[str, Any],
]:
    main_sessions = discover_main_corpus_sessions()
    main_session_paths = {path.resolve() for path, _ in main_sessions}
    artifact_bearing_paths = {path.resolve() for path in discover_artifact_bearing_capture_directories()}
    outside_main = sorted(
        artifact_bearing_paths - main_session_paths,
        key=lambda path: relative_path(path).lower(),
    )

    candidates: list[tuple[Path, Path, str, tuple[str, ...]]] = []
    seen_paths: set[Path] = set()
    for session, root_kind in main_sessions:
        for path in session.rglob("*"):
            resolved = path.resolve()
            if not path.is_file() or resolved in seen_paths:
                continue
            categories = artifact_categories(path)
            if not categories:
                continue
            seen_paths.add(resolved)
            candidates.append((path, session, root_kind, categories))

    candidates.sort(key=lambda value: relative_path(value[0]).lower())
    manifest_paths_by_session: dict[Path, list[Path]] = defaultdict(list)
    for path, session, _, categories in candidates:
        if "capture-manifest" in categories:
            manifest_paths_by_session[session].append(path)

    manifest_digests = {
        session: manifest_digests_for_session(session, paths)
        for session, paths in manifest_paths_by_session.items()
    }

    artifacts: list[dict[str, Any]] = []
    session_rows: dict[tuple[str, str], dict[str, Any]] = {}
    available_capture_ids: set[str] = set()
    for path, session, root_kind, categories in candidates:
        capture_id = capture_id_for_session(session)
        if CAPTURE_ID_RE.fullmatch(capture_id):
            available_capture_ids.add(capture_id)
        path_text = relative_path(path)
        session_text = relative_path(session)
        size = path.stat().st_size
        hash_details = hash_record(path, session, manifest_digests.get(session, {}))
        artifacts.append(
            {
                "path": path_text,
                "session": session_text,
                "captureId": capture_id,
                "rootKind": root_kind,
                "categories": list(categories),
                "sizeBytes": size,
                **hash_details,
            }
        )

        session_key = (root_kind, session_text)
        if session_key not in session_rows:
            session_rows[session_key] = {
                "path": session_text,
                "captureId": capture_id,
                "rootKind": root_kind,
                "artifactCount": 0,
                "totalBytes": 0,
                "categoryCounts": Counter(),
                "hashStatusCounts": Counter(),
            }
        row = session_rows[session_key]
        row["artifactCount"] += 1
        row["totalBytes"] += size
        row["categoryCounts"].update(categories)
        row["hashStatusCounts"][hash_details["hashStatus"]] += 1

    sessions: list[dict[str, Any]] = []
    for key in sorted(session_rows):
        row = session_rows[key]
        row["categoryCounts"] = dict(sorted(row["categoryCounts"].items()))
        row["hashStatusCounts"] = dict(sorted(row["hashStatusCounts"].items()))
        sessions.append(row)
    main_session_texts = {relative_path(path) for path, _ in main_sessions}
    audited_session_texts = {row["path"] for row in sessions}
    missing_audited_sessions = sorted(main_session_texts - audited_session_texts)
    unexpected_audited_sessions = sorted(audited_session_texts - main_session_texts)
    reconciliation = {
        "mainExtractorSessionCount": len(main_sessions),
        "auditedSessionCount": len(sessions),
        "sessionSetsMatch": not missing_audited_sessions and not unexpected_audited_sessions,
        "mainCorpusSessionsMissingFromAudit": missing_audited_sessions,
        "unexpectedAuditSessions": unexpected_audited_sessions,
        "artifactBearingDirectoriesOutsideMainCorpus": [
            relative_path(path) for path in outside_main
        ],
        "artifactBearingDirectoriesOutsideMainCorpusCount": len(outside_main),
        "status": (
            "PASS"
            if not outside_main and not missing_audited_sessions and not unexpected_audited_sessions
            else "FAIL"
        ),
    }
    return artifacts, sessions, available_capture_ids, reconciliation


def extract_capture_references(path: Path) -> list[str]:
    try:
        text = path.read_text(encoding="utf-8-sig", errors="replace")
    except OSError:
        return []
    return sorted(set(CAPTURE_ID_RE.findall(text)))


def stable_json_load(path: Path, attempts: int = 3) -> Any:
    error: Exception | None = None
    for attempt in range(attempts):
        try:
            before = path.stat()
            document = json.loads(path.read_text(encoding="utf-8-sig"))
            after = path.stat()
            if before.st_size == after.st_size and before.st_mtime_ns == after.st_mtime_ns:
                return document
            error = RuntimeError(f"file changed while being read: {relative_path(path)}")
        except (OSError, UnicodeDecodeError, ValueError) as exception:
            error = exception
        if attempt + 1 < attempts:
            time.sleep(0.25)
    raise RuntimeError(f"unable to read stable JSON {relative_path(path)}: {error}")


def stable_json_load_and_sha256(path: Path, attempts: int = 3) -> tuple[Any, str]:
    error: Exception | None = None
    for attempt in range(attempts):
        try:
            before = path.stat()
            document = json.loads(path.read_text(encoding="utf-8-sig"))
            middle = path.stat()
            digest = sha256_file(path)
            after = path.stat()
            signatures = {
                (value.st_size, value.st_mtime_ns)
                for value in (before, middle, after)
            }
            if len(signatures) == 1:
                return document, digest
            error = RuntimeError(f"file changed while being parsed and hashed: {relative_path(path)}")
        except (OSError, UnicodeDecodeError, ValueError) as exception:
            error = exception
        if attempt + 1 < attempts:
            time.sleep(0.25)
    raise RuntimeError(
        f"unable to parse and hash stable JSON {relative_path(path)}: {error}"
    )


PROFILE_RESOURCE_RE = re.compile(r"(?:^|\|)resource=(\d+)(?:\||$)")
SIMPLE_CHAR_IDENTITY_RE = re.compile(r"^\(SimpleChar:([0-9A-Fa-f]+)\)$")


def normalize_source_identity(value: Any) -> str | None:
    if not isinstance(value, str):
        return None
    stripped = value.strip()
    simple_char = SIMPLE_CHAR_IDENTITY_RE.fullmatch(stripped)
    if simple_char:
        return f"0x{int(simple_char.group(1), 16):08X}"
    if stripped.lower().startswith("0x"):
        try:
            return f"0x{int(stripped[2:], 16):08X}"
        except ValueError:
            return None
    return None


def source_values(value: Any) -> set[str]:
    if isinstance(value, list):
        return {
            normalized
            for item in value
            if (normalized := normalize_source_identity(item)) is not None
        }
    normalized = normalize_source_identity(value)
    return {normalized} if normalized is not None else set()


def profile_resource(profile_key: str) -> int | None:
    match = PROFILE_RESOURCE_RE.search(profile_key)
    return int(match.group(1)) if match else None


def empty_source_evidence() -> dict[str, Any]:
    return {
        "captureCertifiedForSource": False,
        "completeNormalVariantCount": 0,
        "variantChainCountAggregate": 0,
        "incompleteAttackInfoObservationCount": 0,
        "evidenceRoles": set(),
    }


def inventory_profile_evidence_index(
    inventory: dict[str, Any],
) -> tuple[dict[tuple[int, str, int], list[dict[str, Any]]], int]:
    indexed: dict[tuple[int, str, int], list[dict[str, Any]]] = defaultdict(list)
    profiles = inventory.get("profiles", [])
    if not isinstance(profiles, list):
        return {}, 0
    for profile in profiles:
        if not isinstance(profile, dict):
            continue
        metadata = profile.get("metadata")
        if not isinstance(metadata, dict):
            continue
        profile_key = str(profile.get("profileKey", ""))
        resource = profile_resource(profile_key)
        name = metadata.get("name")
        monster_data = metadata.get("monsterData")
        level = metadata.get("level")
        if (
            resource is None
            or not isinstance(name, str)
            or not isinstance(monster_data, int)
            or not isinstance(level, int)
        ):
            continue
        source_evidence: dict[str, dict[str, Any]] = defaultdict(empty_source_evidence)
        metadata_source = normalize_source_identity(metadata.get("sourceIdentity"))
        if metadata_source is not None:
            source_evidence[metadata_source]["evidenceRoles"].add("profile-metadata")
        variants = profile.get("variants", [])
        profile_capture_certified = any(
            isinstance(variant, dict) and variant.get("captureCertified") is True
            for variant in variants
        )
        for variant in variants:
            if not isinstance(variant, dict):
                continue
            certified_sources = source_values(variant.get("sourceIdentities"))
            variant_source_fields = (
                "sourceIdentities",
                "excludedConflictedSourceIdentities",
                "excludedInferredMetadataSourceIdentities",
                "unresolvedBehaviorSourceIdentities",
                "representativeEvidenceSourceIdentity",
            )
            all_variant_sources: set[str] = set()
            for field in variant_source_fields:
                values = source_values(variant.get(field))
                all_variant_sources.update(values)
                for source in values:
                    source_evidence[source]["evidenceRoles"].add(f"variant-{field}")
            chain_count = variant.get("chainCount", 0)
            if not isinstance(chain_count, int):
                chain_count = 0
            for source in all_variant_sources:
                evidence = source_evidence[source]
                if chain_count > 0:
                    evidence["completeNormalVariantCount"] += 1
                    evidence["variantChainCountAggregate"] += chain_count
                if variant.get("captureCertified") is True and source in certified_sources:
                    evidence["captureCertifiedForSource"] = True

        for observation in profile.get("incompleteObservations", []):
            if not isinstance(observation, dict):
                continue
            source = normalize_source_identity(observation.get("sourceIdentity"))
            if source is None:
                continue
            evidence = source_evidence[source]
            evidence["evidenceRoles"].add("incomplete-observation")
            if observation.get("messageType") == "AttackInfo":
                count = observation.get("observationCount", 0)
                if isinstance(count, int):
                    evidence["incompleteAttackInfoObservationCount"] += count

        normalized_source_evidence: dict[str, dict[str, Any]] = {}
        for source, evidence in source_evidence.items():
            normalized_source_evidence[source] = {
                **evidence,
                "evidenceRoles": sorted(evidence["evidenceRoles"]),
            }
        indexed[(resource, name, monster_data)].append(
            {
                "profileKey": profile_key,
                "resource": resource,
                "name": name,
                "monsterData": monster_data,
                "level": level,
                "status": profile.get("status"),
                "profileCaptureCertified": profile_capture_certified,
                "normalCompleteChainCount": (
                    profile.get("normalCompleteChainCount", 0)
                    if isinstance(profile.get("normalCompleteChainCount", 0), int)
                    else 0
                ),
                "incompleteAttackInfoCount": (
                    profile.get("incompleteAttackInfoCount", 0)
                    if isinstance(profile.get("incompleteAttackInfoCount", 0), int)
                    else 0
                ),
                "sourceEvidence": normalized_source_evidence,
            }
        )
    for summaries in indexed.values():
        summaries.sort(key=lambda row: (row["level"], row["profileKey"]))
    return dict(indexed), len(profiles)


def semantic_identity_key(
    resource: int,
    name: str,
    monster_data: int,
    level: int | None,
    source: str | None,
) -> str:
    return (
        f"resource={resource}|name={name}|md={monster_data}|"
        f"level={level if level is not None else '?'}|source={source or '?'}"
    )


def classify_profile_source(
    profile: dict[str, Any],
    source: str | None,
) -> tuple[str, bool, dict[str, Any]]:
    evidence = profile["sourceEvidence"].get(source, {}) if source is not None else {}
    if evidence.get("captureCertifiedForSource") is True:
        return "source-capture-certified", False, evidence
    if evidence.get("completeNormalVariantCount", 0) > 0:
        return "complete-raw-sequence-present-but-source-not-certified", True, evidence
    if evidence.get("incompleteAttackInfoObservationCount", 0) > 0:
        return "incomplete-raw-packet-context", False, evidence
    if source is None and profile["profileCaptureCertified"]:
        return "profile-capture-certified-source-unavailable", False, evidence
    if source is None and profile["normalCompleteChainCount"] > 0:
        return "complete-raw-sequence-present-but-source-unattributed", True, evidence
    if profile["profileCaptureCertified"]:
        return "profile-certified-for-different-or-unattributed-source", False, evidence
    return "no-source-specific-complete-raw-contract", False, evidence


def semantic_cross_reference_rows(
    resource: int,
    name: str,
    monster_data: int,
    report_levels: set[int],
    report_sources: set[str],
    profiles: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    eligible_profiles = [
        profile
        for profile in profiles
        if not report_levels or profile["level"] in report_levels
    ]
    rows: list[dict[str, Any]] = []
    sources_to_match = set(report_sources)
    sources_to_match.update(
        source
        for profile in eligible_profiles
        for source in profile["sourceEvidence"]
    )
    if not sources_to_match:
        sources_to_match.add(None)  # type: ignore[arg-type]

    for source in sorted(sources_to_match, key=lambda value: value or ""):
        matching_profiles = [
            profile
            for profile in eligible_profiles
            if source is None or source in profile["sourceEvidence"]
        ]
        if not matching_profiles:
            rows.append(
                {
                    "semanticIdentityKey": semantic_identity_key(
                        resource, name, monster_data, None, source
                    ),
                    "resource": resource,
                    "name": name,
                    "monsterData": monster_data,
                    "level": None,
                    "sourceIdentity": source,
                    "profileKey": None,
                    "reportedBySubwayDocument": source in report_sources,
                    "classification": "reported-source-not-resolved-to-raw-profile",
                    "blocker": False,
                    "profileCaptureCertified": False,
                    "sourceEvidence": {},
                }
            )
            continue
        for profile in matching_profiles:
            classification, blocker, evidence = classify_profile_source(profile, source)
            rows.append(
                {
                    "semanticIdentityKey": semantic_identity_key(
                        resource, name, monster_data, profile["level"], source
                    ),
                    "resource": resource,
                    "name": name,
                    "monsterData": monster_data,
                    "level": profile["level"],
                    "sourceIdentity": source,
                    "profileKey": profile["profileKey"],
                    "reportedBySubwayDocument": source in report_sources,
                    "classification": classification,
                    "blocker": blocker,
                    "profileCaptureCertified": profile["profileCaptureCertified"],
                    "profileNormalCompleteChainCount": profile["normalCompleteChainCount"],
                    "profileIncompleteAttackInfoCount": profile["incompleteAttackInfoCount"],
                    "profileStatus": profile["status"],
                    "sourceEvidence": evidence,
                }
            )
    rows.sort(
        key=lambda row: (
            row["resource"],
            row["name"],
            row["monsterData"],
            -1 if row["level"] is None else row["level"],
            row["sourceIdentity"] or "",
            row["profileKey"] or "",
        )
    )
    return rows


def build_subway_cross_reference(
    subway_report: dict[str, Any],
    inventory: dict[str, Any] | None,
    available_capture_ids: set[str],
) -> dict[str, Any]:
    referenced_capture_ids: set[str] = set()
    entries_with_normal = 0
    unresolved_entries: list[dict[str, Any]] = []
    profile_index: dict[tuple[int, str, int], list[dict[str, Any]]] = {}
    profiles_scanned = 0
    if inventory is not None:
        profile_index, profiles_scanned = inventory_profile_evidence_index(inventory)

    all_semantic_rows: list[dict[str, Any]] = []

    for name in sorted(subway_report):
        entry = subway_report[name]
        if not isinstance(entry, dict):
            continue
        captures = sorted(
            value for value in entry.get("captures", []) if isinstance(value, str)
        )
        referenced_capture_ids.update(captures)
        normal_rows = entry.get("normalAttackInfoRows", 0)
        if not isinstance(normal_rows, int) or normal_rows <= 0:
            continue
        entries_with_normal += 1
        resource_values = {
            value
            for field in ("resource", "resources", "playfield", "playfields", "playfieldId")
            for value in (
                entry.get(field, [])
                if isinstance(entry.get(field), list)
                else [entry.get(field)]
            )
            if isinstance(value, int)
        }
        if not resource_values:
            resource_values = {127}
        report_levels = {
            value
            for field in ("level", "levels")
            for value in (
                entry.get(field, [])
                if isinstance(entry.get(field), list)
                else [entry.get(field)]
            )
            if isinstance(value, int)
        }
        report_sources = {
            normalized
            for value in entry.get("identities", [])
            if (normalized := normalize_source_identity(value)) is not None
        }
        monster_data_values = sorted(
            set(value for value in entry.get("monsterData", []) if isinstance(value, int))
        )
        semantic_rows: list[dict[str, Any]] = []
        matching_profiles: list[dict[str, Any]] = []
        for resource in sorted(resource_values):
            for monster_data in monster_data_values:
                profiles = profile_index.get((resource, name, monster_data), [])
                matching_profiles.extend(
                    profile
                    for profile in profiles
                    if not report_levels or profile["level"] in report_levels
                )
                semantic_rows.extend(
                    semantic_cross_reference_rows(
                        resource,
                        name,
                        monster_data,
                        report_levels,
                        report_sources,
                        profiles,
                    )
                )
        all_semantic_rows.extend(semantic_rows)
        any_profile_certified = any(
            profile["profileCaptureCertified"] for profile in matching_profiles
        )
        contract_gap_classifications = {
            "complete-raw-sequence-present-but-source-not-certified",
            "complete-raw-sequence-present-but-source-unattributed",
            "incomplete-raw-packet-context",
        }
        semantic_contract_gaps = [
            row
            for row in semantic_rows
            if row["classification"] in contract_gap_classifications
        ]
        if inventory is None or not any_profile_certified or semantic_contract_gaps:
            blocker = any(row["blocker"] for row in semantic_contract_gaps)
            unresolved_evidence = semantic_contract_gaps or semantic_rows
            unresolved_entries.append(
                {
                    "resource": sorted(resource_values),
                    "name": name,
                    "monsterData": monster_data_values,
                    "levels": sorted(report_levels),
                    "reportedSourceIdentities": sorted(report_sources),
                    "normalAttackInfoRows": normal_rows,
                    "captures": captures,
                    "matchedProfileKeys": sorted(
                        {profile["profileKey"] for profile in matching_profiles}
                    ),
                    "captureCertifiedProfileKeys": sorted(
                        {
                            profile["profileKey"]
                            for profile in matching_profiles
                            if profile["profileCaptureCertified"]
                        }
                    ),
                    "contractState": (
                        "unavailable"
                        if inventory is None
                        else "partial-source-specific-gaps"
                        if any_profile_certified and semantic_contract_gaps
                        else "no-capture-certified-profile-identity"
                    ),
                    "blocker": blocker,
                    "unresolvedSemanticIdentityKeys": [
                        row["semanticIdentityKey"] for row in unresolved_evidence
                    ],
                    "unresolvedEvidence": unresolved_evidence,
                    "reason": (
                        "combat inventory unavailable"
                        if inventory is None
                        else "normal AttackInfo evidence has at least one unresolved full resource/name/MonsterData/level/source identity"
                    ),
                }
            )

    referenced = sorted(referenced_capture_ids)
    missing = sorted(referenced_capture_ids - available_capture_ids)
    return {
        "reportPath": relative_path(SUBWAY_REPORT_PATH),
        "reportEntryCount": len(subway_report),
        "referencedCaptureIds": referenced,
        "missingReferencedCaptureIds": missing,
        "allReferencedCapturesExist": not missing,
        "normalAttackInfoEntryCount": entries_with_normal,
        "inventoryAvailable": inventory is not None,
        "inventoryProfilesScanned": profiles_scanned,
        "entriesWithNormalAttackInfoButNoCompleteRawContractCount": len(unresolved_entries),
        "blockingCompleteRawSequenceEntryCount": sum(row["blocker"] for row in unresolved_entries),
        "blockingCompleteRawSequenceEntries": [
            row["name"] for row in unresolved_entries if row["blocker"]
        ],
        "semanticProfileCrossReferenceCount": len(all_semantic_rows),
        "semanticProfileCrossReferences": all_semantic_rows,
        "entriesWithNormalAttackInfoButNoCompleteRawContract": unresolved_entries,
    }


def path_is_under(path: Path, parent: Path) -> bool:
    try:
        path.resolve().relative_to(parent.resolve())
        return True
    except ValueError:
        return False


def content_has_capture_evidence(path: Path) -> bool:
    capture_seen = False
    semantic_seen = False
    overlap = ""
    try:
        with path.open("r", encoding="utf-8-sig", errors="replace") as stream:
            while True:
                chunk = stream.read(64 * 1024)
                if not chunk:
                    break
                text = overlap + chunk
                capture_seen = capture_seen or CAPTURE_ID_RE.search(text) is not None
                semantic_seen = semantic_seen or EVIDENCE_CONTENT_RE.search(text) is not None
                if capture_seen and semantic_seen:
                    return True
                overlap = text[-128:]
    except OSError:
        return False
    return False


def evidence_discovery_reasons(path: Path) -> list[str]:
    resolved = path.resolve()
    if resolved in EVIDENCE_EXCLUDED_PATHS:
        return []
    if path.suffix.lower() not in EVIDENCE_DOCUMENT_EXTENSIONS:
        return []
    if path_is_under(path, PRIMARY_CAPTURE_ROOT):
        return []
    lowered_parts = {part.lower() for part in path.parts}
    if lowered_parts.intersection({".git", "bin", "obj", "packages", "__pycache__"}):
        return []

    reasons: list[str] = []
    evidence_root = REPO_ROOT / "docs" / "evidence"
    if path_is_under(path, evidence_root):
        reasons.append("docs-evidence-directory")
    relative_lower = relative_path(path).lower()
    if any(token in relative_lower for token in EVIDENCE_PATH_TOKENS):
        reasons.append("semantic-path-token")
    if not reasons and content_has_capture_evidence(path):
        reasons.append("capture-id-and-semantic-content")
    return reasons


def discover_evidence_document_paths(
    roots: Iterable[Path] = EVIDENCE_DISCOVERY_ROOTS,
) -> list[tuple[Path, list[str]]]:
    discovered: dict[Path, list[str]] = {}
    for root in roots:
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if not path.is_file():
                continue
            reasons = evidence_discovery_reasons(path)
            if reasons:
                discovered[path.resolve()] = reasons
    return [
        (path, discovered[path])
        for path in sorted(discovered, key=lambda value: relative_path(value).lower())
    ]


def evidence_document_records(available_capture_ids: set[str]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    discovered = discover_evidence_document_paths()
    discovered_paths = {path.resolve() for path, _ in discovered}
    missing_required = [
        relative_path(path)
        for path in REQUIRED_EVIDENCE_DOCUMENT_PATHS
        if not path.exists() or path.resolve() not in discovered_paths
    ]
    if missing_required:
        raise RuntimeError(
            "required prior evidence documents missing from deterministic discovery: "
            + ", ".join(missing_required)
        )
    for path, reasons in discovered:
        references = extract_capture_references(path)
        rows.append(
            {
                "path": relative_path(path),
                "exists": True,
                "discoveryReasons": reasons,
                "sizeBytes": path.stat().st_size,
                **hash_record(path, None, {}),
                "referencedCaptureIds": references,
                "missingReferencedCaptureIds": sorted(set(references) - available_capture_ids),
            }
        )
    return rows


def inventory_input_record(content_sha256: str | None = None) -> dict[str, Any]:
    if not COMBAT_INVENTORY_PATH.exists():
        return {
            "path": relative_path(COMBAT_INVENTORY_PATH),
            "exists": False,
            "sizeBytes": None,
            "hashStatus": "missing",
            "sha256": None,
        }
    return {
        "path": relative_path(COMBAT_INVENTORY_PATH),
        "exists": True,
        "sizeBytes": COMBAT_INVENTORY_PATH.stat().st_size,
        **(
            {
                "hashStatus": "content-sha256",
                "sha256": content_sha256,
                "manifestSha256": None,
                "manifestDigestMatches": None,
            }
            if content_sha256 is not None
            else hash_record(COMBAT_INVENTORY_PATH, None, {})
        ),
    }


def build_audit() -> dict[str, Any]:
    artifacts, sessions, available_capture_ids, reconciliation = discover_artifacts()
    if reconciliation["status"] != "PASS":
        raise RuntimeError(
            "capture corpus reconciliation failed: "
            + json.dumps(reconciliation, separators=(",", ":"))
        )
    if not COMBAT_INVENTORY_PATH.exists():
        raise RuntimeError(
            f"missing combat inventory required for semantic cross-reference: {relative_path(COMBAT_INVENTORY_PATH)}"
        )
    loaded_inventory, inventory_sha256 = stable_json_load_and_sha256(COMBAT_INVENTORY_PATH)
    if not isinstance(loaded_inventory, dict):
        raise RuntimeError("combat inventory root must be an object")
    inventory: dict[str, Any] = loaded_inventory
    evidence_documents = evidence_document_records(available_capture_ids)
    inventory_input = inventory_input_record(inventory_sha256)
    unhashed_paths = [
        row["path"]
        for row in [*artifacts, *evidence_documents, inventory_input]
        if row.get("exists", True) and row.get("hashStatus") != "content-sha256"
    ]
    if unhashed_paths:
        raise RuntimeError(
            "authoritative artifacts without streaming SHA-256: " + ", ".join(unhashed_paths)
        )
    manifest_mismatches = [
        row["path"]
        for row in artifacts
        if row.get("manifestDigestMatches") is False
    ]
    if manifest_mismatches:
        raise RuntimeError(
            "capture artifacts disagree with manifest SHA-256: "
            + ", ".join(manifest_mismatches)
        )

    subway_report_document = stable_json_load(SUBWAY_REPORT_PATH)
    if not isinstance(subway_report_document, dict):
        raise RuntimeError("Subway combat report root must be an object")

    category_counts: Counter[str] = Counter()
    hash_status_counts: Counter[str] = Counter()
    total_bytes = 0
    for artifact in artifacts:
        category_counts.update(artifact["categories"])
        hash_status_counts[artifact["hashStatus"]] += 1
        total_bytes += artifact["sizeBytes"]

    return {
        "schemaVersion": SCHEMA_VERSION,
        "generator": relative_path(Path(__file__)),
        "authority": "cross-reference audit only; derived evidence is never a production combat-contract input",
        "scope": {
            "captureRoots": [
                {"path": relative_path(PRIMARY_CAPTURE_ROOT), "kind": "primary"},
                {"path": relative_path(LEGACY_CAPTURE_ROOT), "kind": "legacy-for-repo"},
            ],
            "hashPolicy": "streaming SHA-256 for every authoritative capture artifact, prior evidence document, and inventory cross-reference input",
            "unhashedArtifactCount": 0,
            "manifestDigestMismatchCount": 0,
        },
        "summary": {
            "captureSessionCount": len(sessions),
            "captureArtifactCount": len(artifacts),
            "captureArtifactBytes": total_bytes,
            "categoryCounts": dict(sorted(category_counts.items())),
            "hashStatusCounts": dict(sorted(hash_status_counts.items())),
            "evidenceDocumentCount": len(evidence_documents),
            "missingEvidenceDocumentCount": sum(not row["exists"] for row in evidence_documents),
        },
        "mainCorpusReconciliation": reconciliation,
        "combatInventoryInput": inventory_input,
        "evidenceDocuments": evidence_documents,
        "subwayCrossReference": build_subway_cross_reference(
            subway_report_document,
            inventory,
            available_capture_ids,
        ),
        "sessions": sessions,
        "artifacts": artifacts,
    }


def serialize(document: dict[str, Any]) -> bytes:
    return (json.dumps(document, indent=2, ensure_ascii=False) + "\n").encode("utf-8")


def write_output(document: dict[str, Any]) -> None:
    payload = serialize(document)
    if len(payload) >= MAX_OUTPUT_BYTES:
        raise RuntimeError(
            f"secondary evidence audit would be {len(payload)} bytes; compact-output limit is {MAX_OUTPUT_BYTES}"
        )
    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    temporary = OUTPUT_PATH.with_suffix(OUTPUT_PATH.suffix + ".tmp")
    temporary.write_bytes(payload)
    temporary.replace(OUTPUT_PATH)


def check_output(document: dict[str, Any]) -> None:
    expected = serialize(document)
    if not OUTPUT_PATH.exists():
        raise RuntimeError(f"missing generated output: {relative_path(OUTPUT_PATH)}")
    actual = OUTPUT_PATH.read_bytes()
    if actual != expected:
        raise RuntimeError(
            f"generated output is stale: run {relative_path(Path(__file__))} --write"
        )


def run_self_test() -> None:
    assert artifact_categories(Path("raw-packets.csv")) == ("raw-packets",)
    assert "movement" in artifact_categories(Path("movement-packets.csv"))
    assert "weapon-context" in artifact_categories(Path("enemy-combat.csv"))
    assert "capture-manifest" in artifact_categories(Path("capture-session.json"))

    digest = "a" * 64
    extracted = extract_manifest_digests(
        {"artifacts": [{"relativePath": "raw/large.bin", "sha256": digest}]}
    )
    assert extracted["raw/large.bin"] == digest
    assert extracted["large.bin"] == digest
    assert normalize_source_identity("(SimpleChar:000000AA)") == "0x000000AA"
    assert normalize_source_identity("0xaa") == "0x000000AA"

    report = {
        "Covered": {
            "captures": ["20260701-010101"],
            "monsterData": [10],
            "identities": ["(SimpleChar:000000AA)"],
            "normalAttackInfoRows": 2,
        },
        "Missing": {
            "captures": ["20260701-020202"],
            "monsterData": [20],
            "levels": [7],
            "identities": ["(SimpleChar:000000BB)"],
            "normalAttackInfoRows": 1,
        },
    }
    inventory = {
        "profiles": [
            {
                "profileKey": "resource=127|md=10|level=5|name=Covered",
                "metadata": {
                    "name": "Covered",
                    "monsterData": 10,
                    "level": 5,
                    "sourceIdentity": "0x000000AA",
                },
                "variants": [
                    {
                        "captureCertified": True,
                        "sourceIdentities": ["0x000000AA"],
                        "representativeEvidenceSourceIdentity": "0x000000AA",
                        "chainCount": 1,
                    }
                ],
                "normalCompleteChainCount": 1,
                "incompleteAttackInfoCount": 0,
            },
            {
                "profileKey": "resource=127|md=20|level=7|name=Missing",
                "metadata": {
                    "name": "Missing",
                    "monsterData": 20,
                    "level": 7,
                    "sourceIdentity": "0x000000BB",
                },
                "variants": [
                    {
                        "captureCertified": False,
                        "excludedConflictedSourceIdentities": ["0x000000BB"],
                        "representativeEvidenceSourceIdentity": "0x000000BB",
                        "chainCount": 4,
                    }
                ],
                "normalCompleteChainCount": 4,
                "incompleteAttackInfoCount": 1,
                "status": "unresolved",
            },
            {
                "profileKey": "resource=127|md=20|level=8|name=Missing",
                "metadata": {
                    "name": "Missing",
                    "monsterData": 20,
                    "level": 8,
                    "sourceIdentity": "0x000000CC",
                },
                "variants": [
                    {
                        "captureCertified": True,
                        "sourceIdentities": ["0x000000CC"],
                        "chainCount": 2,
                    }
                ],
                "normalCompleteChainCount": 2,
                "incompleteAttackInfoCount": 0,
                "status": "certified",
            },
        ]
    }
    cross_reference = build_subway_cross_reference(
        report,
        inventory,
        {"20260701-010101"},
    )
    assert cross_reference["allReferencedCapturesExist"] is False
    assert cross_reference["missingReferencedCaptureIds"] == ["20260701-020202"]
    assert cross_reference["entriesWithNormalAttackInfoButNoCompleteRawContractCount"] == 1
    assert cross_reference["entriesWithNormalAttackInfoButNoCompleteRawContract"][0]["name"] == "Missing"
    assert cross_reference["blockingCompleteRawSequenceEntries"] == ["Missing"]
    assert (
        cross_reference["entriesWithNormalAttackInfoButNoCompleteRawContract"][0][
            "unresolvedEvidence"
        ][0]["classification"]
        == "complete-raw-sequence-present-but-source-not-certified"
    )
    missing_semantic = cross_reference["entriesWithNormalAttackInfoButNoCompleteRawContract"][0][
        "unresolvedEvidence"
    ][0]
    assert missing_semantic["semanticIdentityKey"] == (
        "resource=127|name=Missing|md=20|level=7|source=0x000000BB"
    )
    assert missing_semantic["level"] == 7
    assert missing_semantic["sourceIdentity"] == "0x000000BB"

    with tempfile.TemporaryDirectory(dir=REPO_ROOT / "tools-temp") as temporary_directory:
        temporary_root = Path(temporary_directory)
        content_discovered = temporary_root / "opaque.md"
        content_discovered.write_text(
            "Capture 20260701-010101 records combat packet evidence.\n",
            encoding="utf-8",
        )
        path_discovered = temporary_root / "npc_report.txt"
        path_discovered.write_text("No timestamp is required for a semantic path.\n", encoding="utf-8")
        unrelated = temporary_root / "unrelated.txt"
        unrelated.write_text("ordinary documentation\n", encoding="utf-8")
        discovered_paths = {
            path.resolve()
            for path, _ in discover_evidence_document_paths([temporary_root])
        }
        assert content_discovered.resolve() in discovered_paths
        assert path_discovered.resolve() in discovered_paths
        assert unrelated.resolve() not in discovered_paths

        streamed = temporary_root / "streamed.bin"
        streamed.write_bytes(b"x" * (1024 * 1024 + 17))
        stream_record = hash_record(streamed, None, {})
        assert stream_record["hashStatus"] == "content-sha256"
        assert stream_record["sha256"] == sha256_file(streamed)
        assert stream_record["sha256"] is not None
        manifested_record = hash_record(
            streamed,
            None,
            {streamed.name: stream_record["sha256"]},
        )
        assert manifested_record["manifestDigestMatches"] is True

    first = serialize({"schemaVersion": 1, "rows": [1, 2]})
    second = serialize({"schemaVersion": 1, "rows": [1, 2]})
    assert first == second
    print("PASS audit_capture_backed_npc_secondary_evidence self-test")


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write", action="store_true", help="write the deterministic generated audit")
    mode.add_argument("--check", action="store_true", help="verify the generated audit is current")
    mode.add_argument("--self-test", action="store_true", help="run deterministic unit checks")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    try:
        if args.self_test:
            run_self_test()
            return 0
        document = build_audit()
        if args.write:
            write_output(document)
            summary = document["summary"]
            subway = document["subwayCrossReference"]
            print(
                "WROTE "
                f"{relative_path(OUTPUT_PATH)} sessions={summary['captureSessionCount']} "
                f"artifacts={summary['captureArtifactCount']} "
                f"subwayMissingCaptures={len(subway['missingReferencedCaptureIds'])} "
                f"subwayNormalWithoutRawContract={subway['entriesWithNormalAttackInfoButNoCompleteRawContractCount']}"
            )
        else:
            check_output(document)
            print(f"PASS {relative_path(OUTPUT_PATH)} is current")
        return 0
    except Exception as exception:  # deterministic CLI boundary
        print(f"ERROR: {exception}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
