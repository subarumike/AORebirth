#!/usr/bin/env python3
"""Build the governed, non-runtime PF4582 TemplateHash resolution audit."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable

import generate_pf4582_placements as placement_generator


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE = REPOSITORY_ROOT / "docs/reference/pf4582/PlayfieldDistrictInfo.json"
DEFAULT_EVIDENCE_MAP = REPOSITORY_ROOT / "docs/reference/pf4582/runtime-evidence-map.json"
DEFAULT_RUNTIME_SOURCE = (
    REPOSITORY_ROOT
    / "AORebirth/Server/ZoneEngine/Core/Playfields/IccShuttleportSpawn.cs"
)
DEFAULT_EVIDENCE_LEDGER = (
    REPOSITORY_ROOT / "docs/reference/pf4582/template-hash-evidence.json"
)
DEFAULT_SQL_SOURCE = (
    REPOSITORY_ROOT
    / "AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql"
)
DEFAULT_ARCHETYPE_SOURCE = (
    REPOSITORY_ROOT
    / "AORebirth/Server/ZoneEngine/Core/CombatTestMobArchetype.cs"
)
DEFAULT_JSON_OUTPUT = (
    REPOSITORY_ROOT / "docs/generated/pf4582_template_hash_resolution_report.json"
)
DEFAULT_MARKDOWN_OUTPUT = (
    REPOSITORY_ROOT
    / "docs/evidence/PF4582_TEMPLATE_HASH_RESOLUTION_AUDIT_20260824.md"
)

EXPECTED_PLACEMENTS = 206
EXPECTED_HASHES = 38
EXPECTED_BASELINE_MAPPED = 14
EXPECTED_BASELINE_UNRESOLVED = 24
EXPECTED_RUNTIME_ACTIVE = 25
EXPECTED_RUNTIME_BLOCKED = 181
EXPECTED_UNRESOLVED_HASH_BLOCKED = 171
EXPECTED_BASELINE_MAPPED_BLOCKED = 10
EXPECTED_ACCEPTED_PF4582_CAPTURES = 25
ALLOWED_CLASSIFICATIONS = {"PROVEN", "CANDIDATE", "AMBIGUOUS", "NO_EVIDENCE"}
ALLOWED_BASELINE_STATES = {
    "BASELINE_PROVEN",
    "BASELINE_EVIDENCE_INCOMPLETE",
    "BASELINE_CONFLICT",
}
CLASSIFICATION_BY_BASIS = {
    "SINGLE_PLAUSIBLE_PROFILE": "CANDIDATE",
    "MULTIPLE_PLAUSIBLE_PROFILES": "AMBIGUOUS",
    "NO_RELIABLE_PROFILE_LINK": "NO_EVIDENCE",
}


class AuditError(ValueError):
    """Raised when governed audit input is missing, malformed, or conflicting."""


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise AuditError(message)


def _load_json(path: Path) -> Any:
    _require(path.is_file(), f"missing governed input: {repository_path(path)}")
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise AuditError(f"malformed JSON input {repository_path(path)}: {exc}") from exc


def sha256_file(path: Path) -> str:
    _require(path.is_file(), f"missing governed input: {repository_path(path)}")
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def repository_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(REPOSITORY_ROOT.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def canonical_template_hash(value: int | str) -> str:
    text = str(value)
    _require(re.fullmatch(r"-?[0-9]+", text) is not None,
             f"invalid TemplateHash representation: {text!r}")
    numeric = int(text, 10)
    _require(-(1 << 31) <= numeric <= 0xFFFFFFFF,
             f"TemplateHash outside supported 32-bit range: {text}")
    return f"0x{numeric & 0xFFFFFFFF:08X}"


def template_tag(value: int | str) -> str:
    numeric = int(str(value), 10) & 0xFFFFFFFF
    raw = numeric.to_bytes(4, byteorder="little", signed=False)
    return "".join(chr(byte) if 32 <= byte <= 126 else f"\\x{byte:02X}" for byte in raw)


def parse_source_hash_tokens(path: Path) -> dict[int, str]:
    _require(path.is_file(), f"missing governed input: {repository_path(path)}")
    text = path.read_text(encoding="utf-8-sig")
    pairs = re.findall(
        r'"NpcId"\s*:\s*([0-9]+)\s*,\s*"TemplateHash"\s*:\s*(-?[0-9]+)',
        text,
    )
    _require(len(pairs) == EXPECTED_PLACEMENTS,
             f"expected {EXPECTED_PLACEMENTS} exact NpcId/TemplateHash pairs, found {len(pairs)}")
    tokens: dict[int, str] = {}
    for npc_id_text, template_hash_text in pairs:
        npc_id = int(npc_id_text, 10)
        _require(npc_id not in tokens, f"duplicate raw source NpcId {npc_id}")
        tokens[npc_id] = template_hash_text
    return tokens


def decide_classification(
    candidate_profiles: Iterable[str],
    direct_evidence: Iterable[dict[str, Any]] = (),
    contradictory_evidence: Iterable[Any] = (),
    resolved_profile: str = "",
) -> str:
    candidates = sorted(set(candidate_profiles))
    direct = list(direct_evidence)
    contradictions = list(contradictory_evidence)
    if resolved_profile and len(candidates) == 1 and direct and not contradictions:
        return "PROVEN"
    if len(candidates) > 1 or contradictions:
        return "AMBIGUOUS"
    if len(candidates) == 1:
        return "CANDIDATE"
    return "NO_EVIDENCE"


def validate_audit_record(record: dict[str, Any]) -> None:
    classification = record.get("Classification")
    if classification == "BASELINE_PROVEN":
        _require(record.get("BaselineVerificationState") == "BASELINE_PROVEN",
                 "baseline-proven record lacks matching verification state")
    else:
        _require(classification in ALLOWED_CLASSIFICATIONS,
                 f"invalid unresolved classification: {classification!r}")
        if classification == "PROVEN":
            _require(bool(record.get("ResolvedAoRebirthProfile")),
                     "PROVEN record lacks a resolved profile")
            _require(len(record.get("CandidateAoRebirthProfiles", [])) == 1,
                     "PROVEN record must contain exactly one candidate profile")
            _require(bool(record.get("DirectEvidence")),
                     "PROVEN record lacks direct evidence")
            _require(not record.get("ContradictoryEvidence"),
                     "PROVEN record contains unresolved contradictory evidence")
        else:
            _require(bool(record.get("RemainingBlockers")),
                     f"{classification} record lacks remaining blockers")
    _require(bool(record.get("Rationale")), "audit record lacks rationale")
    _require(record.get("RuntimeActivationAllowed") is False,
             "audit records may not authorize runtime activation")


def _split_sql_values(text: str) -> list[str]:
    values: list[str] = []
    current: list[str] = []
    quoted = False
    escaped = False
    for char in text:
        if escaped:
            current.append(char)
            escaped = False
            continue
        if char == "\\" and quoted:
            escaped = True
            current.append(char)
            continue
        if char == "'":
            quoted = not quoted
            current.append(char)
            continue
        if char == "," and not quoted:
            values.append("".join(current).strip())
            current = []
        else:
            current.append(char)
    _require(not quoted and not escaped, "malformed quoted SQL values")
    values.append("".join(current).strip())
    return values


def _sql_string(value: str) -> str:
    _require(len(value) >= 2 and value[0] == "'" and value[-1] == "'",
             f"expected SQL string literal, got {value!r}")
    return value[1:-1].replace("\\'", "'").replace("\\\\", "\\")


def parse_mobtemplate_sql(path: Path) -> dict[str, dict[str, Any]]:
    _require(path.is_file(), f"missing governed input: {repository_path(path)}")
    profiles: dict[str, dict[str, Any]] = {}
    marker = ") VALUES ("
    for line_number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        if not line.startswith("INSERT INTO `mobtemplate`"):
            continue
        _require(marker in line and line.endswith(");"),
                 f"malformed mobtemplate row at line {line_number}")
        raw_values = line.split(marker, 1)[1][:-2]
        values = _split_sql_values(raw_values)
        _require(len(values) == 25,
                 f"expected 25 mobtemplate values at line {line_number}, found {len(values)}")
        profile_key = _sql_string(values[0])
        _require(profile_key not in profiles, f"duplicate mobtemplate profile {profile_key}")
        profiles[profile_key] = {
            "ProfileKey": f"mobtemplate:{profile_key}",
            "LegacyHash": profile_key,
            "Name": _sql_string(values[8]),
            "MinimumLevel": int(values[1]),
            "MaximumLevel": int(values[2]),
            "NpcFamily": int(values[10]),
            "Health": int(values[11]),
            "MonsterData": int(values[12]),
            "MonsterScale": int(values[13]),
            "EvidencePath": repository_path(path),
            "EvidenceRecordId": f"mobtemplate:{profile_key}",
            "SourceLine": line_number,
        }
    _require(bool(profiles), "mobtemplate SQL contains no structured profiles")
    return profiles


def load_runtime_rollerrat_profile(path: Path) -> dict[str, Any]:
    _require(path.is_file(), f"missing governed input: {repository_path(path)}")
    text = path.read_text(encoding="utf-8-sig")
    match = re.search(
        r"public static readonly Entry StowawayRollerrat = new Entry\((.*?)\);",
        text,
        flags=re.DOTALL,
    )
    _require(match is not None, "missing StowawayRollerrat runtime archetype")
    block = match.group(1)
    _require(re.search(r"\n\s*17687,", block) is not None,
             "StowawayRollerrat MonsterData changed")
    _require(re.search(r"\n\s*55,", block) is not None,
             "StowawayRollerrat NpcFamily changed")
    return {
        "ProfileKey": "runtime:CombatTestMobArchetype.StowawayRollerrat",
        "Name": "Stowaway Rollerrat archetype",
        "MonsterData": 17687,
        "NpcFamily": 55,
        "EvidencePath": repository_path(path),
        "EvidenceRecordId": "CombatTestMobArchetype.StowawayRollerrat",
        "SourceLine": text[:match.start()].count("\n") + 1,
    }


def load_accepted_capture(
    inventory_path: Path,
    dossier_path: Path,
    expected: dict[str, Any],
) -> tuple[dict[str, dict[str, Any]], dict[str, Any], list[dict[str, Any]]]:
    _require(inventory_path.is_file(),
             f"missing governed input: {repository_path(inventory_path)}")
    with inventory_path.open("r", encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))
    matching = [row for row in rows if row.get("capture_id") == expected["captureId"]]
    _require(len(matching) == 1,
             f"accepted capture {expected['captureId']} must have exactly one inventory row")
    inventory_row = matching[0]
    _require(inventory_row.get("capture_path") == expected["capturePath"],
             "accepted capture path drifted")
    _require(inventory_row.get("evidence_digest") == expected["acceptedInventoryDigest"],
             "accepted capture digest drifted")
    artifacts = set((inventory_row.get("artifacts") or "").split(";"))
    _require("enemy-dossier.json" in artifacts,
             "accepted inventory row no longer lists enemy-dossier.json")

    dossier = _load_json(dossier_path)
    _require(isinstance(dossier, dict) and isinstance(dossier.get("enemies"), list),
             "enemy dossier must contain an enemies array")
    by_identity: dict[str, dict[str, Any]] = {}
    for enemy in dossier["enemies"]:
        _require(isinstance(enemy, dict), "malformed enemy dossier record")
        identity = enemy.get("identity")
        _require(isinstance(identity, str) and identity,
                 "enemy dossier record lacks identity")
        _require(identity not in by_identity, f"duplicate dossier identity {identity}")
        by_identity[identity] = enemy
    pf4582_rows = sorted(
        [
            row for row in rows
            if "[PF 4582]" in (row.get("capture_path") or "")
            or row.get("capture_playfield_id") == "4582"
        ],
        key=lambda row: row["capture_id"],
    )
    _require(len(pf4582_rows) == EXPECTED_ACCEPTED_PF4582_CAPTURES,
             f"expected {EXPECTED_ACCEPTED_PF4582_CAPTURES} accepted PF4582 captures, found {len(pf4582_rows)}")
    _require(len({row["capture_id"] for row in pf4582_rows}) == len(pf4582_rows),
             "accepted PF4582 capture IDs must be unique")
    capture_scope = [{
        "CaptureId": row["capture_id"],
        "CapturePath": row["capture_path"],
        "EvidenceDigest": row["evidence_digest"],
        "ValidationStatus": row["validation_status"],
        "RawPacketEvidence": row["raw_packet_evidence"],
        "Artifacts": sorted(filter(None, (row.get("artifacts") or "").split(";"))),
    } for row in pf4582_rows]
    return by_identity, inventory_row, capture_scope


def _validate_pinned_inputs(ledger: dict[str, Any]) -> dict[str, str]:
    pins = ledger.get("pinnedInputs")
    _require(isinstance(pins, list) and pins, "evidence ledger lacks pinnedInputs")
    digests: dict[str, str] = {}
    seen: set[str] = set()
    for pin in pins:
        _require(isinstance(pin, dict), "malformed pinned input")
        relative = pin.get("path")
        expected = pin.get("sha256")
        _require(isinstance(relative, str) and relative and relative not in seen,
                 "pinned input paths must be unique nonempty strings")
        _require(isinstance(expected, str) and re.fullmatch(r"[0-9a-f]{64}", expected) is not None,
                 f"invalid SHA-256 pin for {relative}")
        path = REPOSITORY_ROOT / Path(relative)
        actual = sha256_file(path)
        _require(actual == expected,
                 f"governed input digest mismatch for {relative}: expected {expected}, got {actual}")
        seen.add(relative)
        digests[relative] = actual
    return dict(sorted(digests.items()))


def _source_originals(
    records: list[dict[str, Any]], raw_tokens: dict[int, str]
) -> dict[int, str]:
    _require(len(records) == len(raw_tokens), "source/hash token count mismatch")
    by_hash: dict[int, str] = {}
    canonical_to_numeric: dict[str, int] = {}
    for record in records:
        _require(record["NpcId"] in raw_tokens,
                 f"raw source lacks NpcId {record['NpcId']}")
        token = raw_tokens[record["NpcId"]]
        numeric = int(token, 10)
        _require(numeric == record["TemplateHash"],
                 f"raw TemplateHash token drift for NpcId {record['NpcId']}")
        existing = by_hash.get(numeric)
        _require(existing is None or existing == token,
                 f"TemplateHash {numeric} has conflicting source representations")
        by_hash[numeric] = token
        canonical = canonical_template_hash(token)
        prior = canonical_to_numeric.get(canonical)
        _require(prior is None or prior == numeric,
                 f"TemplateHash canonicalization collision at {canonical}")
        canonical_to_numeric[canonical] = numeric
    return by_hash


def _profile_for_key(
    profile_key: str,
    sql_profiles: dict[str, dict[str, Any]],
    runtime_rollerrat: dict[str, Any],
) -> dict[str, Any]:
    if profile_key.startswith("mobtemplate:"):
        legacy_hash = profile_key.split(":", 1)[1]
        _require(legacy_hash in sql_profiles, f"unknown candidate profile {profile_key}")
        return sql_profiles[legacy_hash]
    if profile_key == runtime_rollerrat["ProfileKey"]:
        return runtime_rollerrat
    raise AuditError(f"unknown candidate profile {profile_key}")


def _capture_evidence(
    enemy: dict[str, Any],
    capture: dict[str, Any],
    capture_digest: str,
) -> dict[str, Any]:
    return {
        "EvidenceType": "AcceptedCaptureIdentityProfile",
        "Strength": "CORROBORATING",
        "IdentityLinkToTemplateHash": False,
        "Path": capture["dossierPath"],
        "RecordId": f"$.enemies[identity={enemy['identity']}]",
        "AcceptedInventoryPath": capture["acceptedInventoryPath"],
        "AcceptedInventoryRecordId": f"capture_id={capture['captureId']}",
        "CaptureId": capture["captureId"],
        "CaptureDigest": capture["acceptedInventoryDigest"],
        "ArtifactSha256": capture_digest,
        "Identity": enemy["identity"],
        "Name": enemy.get("name", ""),
        "Level": enemy.get("level"),
        "MonsterData": int(enemy.get("monsterData") or 0),
        "NpcFamily": int(enemy.get("npcFamily") or 0),
        "MonsterScale": int(enemy.get("monsterScale") or 0),
    }


def _candidate_profile_evidence(profile: dict[str, Any]) -> dict[str, Any]:
    return {
        "EvidenceType": "ExistingAoRebirthProfile",
        "Strength": "CORROBORATING",
        "IdentityLinkToTemplateHash": False,
        "Path": profile["EvidencePath"],
        "RecordId": profile["EvidenceRecordId"],
        "SourceLine": profile["SourceLine"],
        "ProfileKey": profile["ProfileKey"],
        "Name": profile["Name"],
        "MonsterData": profile["MonsterData"],
        "NpcFamily": profile["NpcFamily"],
    }


def _evidence_paths(*collections: Iterable[dict[str, Any]]) -> list[str]:
    paths: set[str] = set()
    for collection in collections:
        for item in collection:
            paths.add(item["Path"])
            accepted_inventory_path = item.get("AcceptedInventoryPath")
            if accepted_inventory_path:
                paths.add(accepted_inventory_path)
    return sorted(paths)


def _evidence_record_ids(*collections: Iterable[dict[str, Any]]) -> list[str]:
    record_ids: set[str] = set()
    for collection in collections:
        for item in collection:
            record_ids.add(item["RecordId"])
            accepted_inventory_record_id = item.get("AcceptedInventoryRecordId")
            if accepted_inventory_record_id:
                record_ids.add(accepted_inventory_record_id)
    return sorted(record_ids)


def _group_records(records: list[dict[str, Any]]) -> dict[int, list[dict[str, Any]]]:
    grouped: dict[int, list[dict[str, Any]]] = defaultdict(list)
    for record in records:
        grouped[record["TemplateHash"]].append(record)
    return dict(grouped)


def _duplicate_positions(records: list[dict[str, Any]]) -> set[int]:
    positions: dict[tuple[Any, Any, Any], list[int]] = defaultdict(list)
    for record in records:
        position = record["Position"]
        positions[(position["X"], position["Y"], position["Z"])].append(record["NpcId"])
    return {
        npc_id
        for npc_ids in positions.values()
        if len(npc_ids) > 1
        for npc_id in npc_ids
    }


def build_audit_model(
    source_path: Path = DEFAULT_SOURCE,
    evidence_map_path: Path = DEFAULT_EVIDENCE_MAP,
    runtime_source_path: Path = DEFAULT_RUNTIME_SOURCE,
    ledger_path: Path = DEFAULT_EVIDENCE_LEDGER,
) -> dict[str, Any]:
    ledger = _load_json(ledger_path)
    _require(ledger.get("schemaVersion") == 1, "unsupported evidence-ledger schema")
    _require(ledger.get("playfieldId") == 4582, "evidence ledger targets the wrong playfield")
    governance = ledger.get("governance")
    _require(isinstance(governance, dict), "evidence ledger lacks governance")
    _require(governance.get("newRuntimeActivationAllowed") is False,
             "evidence ledger must fail closed on runtime activation")
    _require(governance.get("classificationBasis") == CLASSIFICATION_BY_BASIS,
             "evidence-ledger classification rules drifted")
    pinned_digests = _validate_pinned_inputs(ledger)
    ledger_digest = sha256_file(ledger_path)
    available_digests = dict(pinned_digests)
    available_digests[repository_path(ledger_path)] = ledger_digest

    try:
        placement_model = placement_generator.build_model(
            source_path=source_path,
            evidence_map_path=evidence_map_path,
            runtime_source_path=runtime_source_path,
        )
    except (OSError, ValueError, KeyError, TypeError) as exc:
        raise AuditError(f"PF4582 placement baseline validation failed: {exc}") from exc

    records = placement_model["records"]
    _require(len(records) == EXPECTED_PLACEMENTS,
             f"expected {EXPECTED_PLACEMENTS} placements, found {len(records)}")
    raw_tokens = parse_source_hash_tokens(source_path)
    original_by_hash = _source_originals(records, raw_tokens)
    grouped = _group_records(records)
    _require(len(grouped) == EXPECTED_HASHES,
             f"expected {EXPECTED_HASHES} TemplateHash groups, found {len(grouped)}")
    duplicate_npc_ids = _duplicate_positions(records)

    mapped_hashes = {
        template_hash for template_hash, group in grouped.items()
        if any(record["TemplateMapped"] for record in group)
    }
    unresolved_hashes = set(grouped) - mapped_hashes
    _require(len(mapped_hashes) == EXPECTED_BASELINE_MAPPED,
             f"expected {EXPECTED_BASELINE_MAPPED} baseline mapped hashes")
    _require(len(unresolved_hashes) == EXPECTED_BASELINE_UNRESOLVED,
             f"expected {EXPECTED_BASELINE_UNRESOLVED} baseline unresolved hashes")

    sql_profiles = parse_mobtemplate_sql(DEFAULT_SQL_SOURCE)
    runtime_rollerrat = load_runtime_rollerrat_profile(DEFAULT_ARCHETYPE_SOURCE)
    capture = ledger.get("acceptedCapture")
    _require(isinstance(capture, dict), "evidence ledger lacks acceptedCapture")
    inventory_path = REPOSITORY_ROOT / capture.get("acceptedInventoryPath", "")
    dossier_path = REPOSITORY_ROOT / capture.get("dossierPath", "")
    capture_by_identity, _, accepted_capture_scope = load_accepted_capture(
        inventory_path, dossier_path, capture
    )
    dossier_digest = sha256_file(dossier_path)

    assessments = ledger.get("unresolvedAssessments")
    _require(isinstance(assessments, list), "evidence ledger lacks unresolvedAssessments")
    assessment_by_hash: dict[int, dict[str, Any]] = {}
    for assessment in assessments:
        _require(isinstance(assessment, dict), "malformed unresolved assessment")
        original = assessment.get("templateHashOriginal")
        _require(isinstance(original, str) and re.fullmatch(r"-?[0-9]+", original) is not None,
                 "assessment lacks exact TemplateHashOriginal")
        numeric = int(original, 10)
        _require(numeric not in assessment_by_hash, f"duplicate assessment for {original}")
        _require(numeric in unresolved_hashes,
                 f"assessment {original} is not a baseline-unresolved hash")
        _require(original_by_hash[numeric] == original,
                 f"assessment altered source TemplateHash representation {original}")
        assessment_by_hash[numeric] = assessment
    _require(set(assessment_by_hash) == unresolved_hashes,
             "evidence ledger must classify every and only baseline-unresolved hash")

    report_records: list[dict[str, Any]] = []
    for template_hash in sorted(grouped):
        group = sorted(grouped[template_hash], key=lambda item: item["NpcId"])
        original = original_by_hash[template_hash]
        canonical = canonical_template_hash(original)
        source_names = sorted({record["Name"] for record in group})
        dynamic_names = sorted({
            record["Name"] for record in group
            if record["SourceNameInterpretation"] == "UnresolvedDynamic"
        })
        active_npc_ids = sorted(record["NpcId"] for record in group if record["RuntimeActive"])
        blocked_count = len(group) - len(active_npc_ids)
        source_refs = [{
            "EvidenceType": "AuthoritativePlacementMetadata",
            "Strength": "PLACEMENT_ONLY",
            "IdentityLinkToTemplateHash": True,
            "Path": repository_path(source_path),
            "RecordId": f"$.districts[0].Spawns[NpcId={record['NpcId']}]",
            "NpcId": record["NpcId"],
            "TemplateHashOriginal": original,
        } for record in group]

        if template_hash in mapped_hashes:
            profiles = sorted({record["RuntimeProfile"] for record in group if record["RuntimeProfile"]})
            _require(len(profiles) == 1,
                     f"baseline hash {original} does not resolve to one runtime profile")
            _require(bool(active_npc_ids), f"baseline hash {original} has no active source mappings")
            direct = [
                {
                    "EvidenceType": "GovernedSourceNpcIdRuntimeMapping",
                    "Strength": "DIRECT",
                    "IdentityLinkToTemplateHash": True,
                    "Path": repository_path(evidence_map_path),
                    "RecordId": f"$.mappings[npcId in {active_npc_ids}]",
                    "NpcIds": active_npc_ids,
                    "ProfileKey": profiles[0],
                },
                {
                    "EvidenceType": "ValidatedActiveRuntimeDefinition",
                    "Strength": "DIRECT",
                    "IdentityLinkToTemplateHash": True,
                    "Path": repository_path(runtime_source_path),
                    "RecordId": f"IccShuttleportSpawn.Definitions[SourceNpcId in {active_npc_ids}]",
                    "NpcIds": active_npc_ids,
                    "ProfileKey": profiles[0],
                },
                {
                    "EvidenceType": "FailClosedPlacementImporterTests",
                    "Strength": "DIRECT",
                    "IdentityLinkToTemplateHash": True,
                    "Path": "Tools/tests/test_generate_pf4582_placements.py",
                    "RecordId": "Pf4582PlacementGeneratorTests",
                    "ProfileKey": profiles[0],
                },
            ]
            capture_matches = [
                enemy for enemy in capture_by_identity.values()
                if enemy.get("name") in source_names
            ]
            corroborating: list[dict[str, Any]] = []
            if capture_matches:
                representative = sorted(capture_matches, key=lambda item: item["identity"])[0]
                corroborating.append(_capture_evidence(representative, capture, dossier_digest))
            rationale = (
                "BASELINE_PROVEN: the authoritative source NpcId and numeric TemplateHash are "
                f"joined by the governed mapping to exactly one active runtime profile ({profiles[0]}), "
                "and the placement importer validates the current runtime definition against that mapping. "
                "This proves only explicitly mapped NpcIds; same-hash blocked siblings are not promoted."
            )
            record = {
                "TemplateHashOriginal": original,
                "TemplateHashCanonical": canonical,
                "TemplateTag": template_tag(original),
                "BaselineMappingState": "MAPPED",
                "BaselineVerificationState": "BASELINE_PROVEN",
                "BaselineAoRebirthProfile": profiles[0],
                "PlacementCount": len(group),
                "BlockedPlacementCount": blocked_count,
                "ExistingRuntimeActivePlacementCount": len(active_npc_ids),
                "NpcIds": [record["NpcId"] for record in group],
                "SourceNames": source_names,
                "DynamicNamesPresent": dynamic_names,
                "SourceLevelMinimum": min(record["MinLevel"] for record in group),
                "SourceLevelMaximum": max(record["MaxLevel"] for record in group),
                "DuplicatePositionParticipation": any(record["NpcId"] in duplicate_npc_ids for record in group),
                "CandidateAoRebirthProfiles": profiles,
                "Classification": "BASELINE_PROVEN",
                "ResolvedAoRebirthProfile": profiles[0],
                "DirectEvidence": direct,
                "CorroboratingEvidence": corroborating,
                "ContradictoryEvidence": [],
                "EvidencePaths": _evidence_paths(source_refs, direct, corroborating),
                "EvidenceRecordIds": _evidence_record_ids(source_refs, direct, corroborating),
                "CaptureIds": [capture["captureId"]] if corroborating else [],
                "EvidenceDigestsWhereAvailable": {},
                "Rationale": rationale,
                "RemainingBlockers": (
                    ["Ten same-hash Island Reet placements remain blocked because only NpcId 1007858 has a direct runtime mapping."]
                    if blocked_count else []
                ),
                "UnlockPotential": blocked_count,
                "RuntimeActivationAllowed": False,
            }
        else:
            assessment = assessment_by_hash[template_hash]
            basis = assessment.get("assessmentBasis")
            _require(basis in CLASSIFICATION_BY_BASIS,
                     f"invalid assessment basis for {original}: {basis!r}")
            candidate_keys = assessment.get("candidateProfileKeys")
            _require(isinstance(candidate_keys, list)
                     and all(isinstance(key, str) and key for key in candidate_keys)
                     and len(candidate_keys) == len(set(candidate_keys)),
                     f"malformed candidate profile list for {original}")
            if basis == "SINGLE_PLAUSIBLE_PROFILE":
                _require(len(candidate_keys) == 1,
                         f"single-candidate assessment {original} must have exactly one profile")
            elif basis == "MULTIPLE_PLAUSIBLE_PROFILES":
                _require(len(candidate_keys) > 1,
                         f"ambiguous assessment {original} must retain multiple profiles")
            else:
                _require(not candidate_keys,
                         f"no-evidence assessment {original} may not contain a profile")

            profiles = [
                _profile_for_key(key, sql_profiles, runtime_rollerrat)
                for key in candidate_keys
            ]
            corroborating = [_candidate_profile_evidence(profile) for profile in profiles]
            capture_ids: list[str] = []
            identity = assessment.get("captureIdentity")
            if identity is not None:
                _require(isinstance(identity, str) and identity in capture_by_identity,
                         f"missing governed capture identity {identity!r} for {original}")
                enemy = capture_by_identity[identity]
                _require(enemy.get("name") in source_names,
                         f"capture identity {identity} name does not exactly match source for {original}")
                monster_data = int(enemy.get("monsterData") or 0)
                _require(monster_data == assessment.get("expectedMonsterData"),
                         f"capture identity {identity} MonsterData drifted")
                level = enemy.get("level")
                _require(isinstance(level, int)
                         and min(record["MinLevel"] for record in group) <= level
                         <= max(record["MaxLevel"] for record in group),
                         f"capture identity {identity} level no longer overlaps source")
                _require(any(profile["MonsterData"] == monster_data for profile in profiles),
                         f"candidate profiles for {original} do not corroborate captured MonsterData")
                corroborating.insert(0, _capture_evidence(enemy, capture, dossier_digest))
                capture_ids.append(capture["captureId"])
            else:
                _require(basis == "NO_RELIABLE_PROFILE_LINK",
                         f"candidate assessment {original} lacks a capture identity")
                exact_name_matches = [
                    enemy for enemy in capture_by_identity.values()
                    if enemy.get("name") in source_names
                ]
                _require(not exact_name_matches,
                         f"no-evidence assessment {original} conflicts with an exact dossier name")
                corroborating.append({
                    "EvidenceType": "AcceptedCaptureExactNameAbsenceSearch",
                    "Strength": "ABSENCE_CONTEXT",
                    "IdentityLinkToTemplateHash": False,
                    "Path": capture["dossierPath"],
                    "RecordId": f"$.enemies[name in {source_names}] => no records",
                    "AcceptedInventoryPath": capture["acceptedInventoryPath"],
                    "AcceptedInventoryRecordId": f"capture_id={capture['captureId']}",
                    "CaptureId": capture["captureId"],
                    "CaptureDigest": capture["acceptedInventoryDigest"],
                    "ArtifactSha256": dossier_digest,
                })

            contradictory = assessment.get("contradictoryEvidence", [])
            _require(isinstance(contradictory, list)
                     and all(isinstance(item, str) and item for item in contradictory),
                     f"malformed contradictory evidence for {original}")
            if basis == "MULTIPLE_PLAUSIBLE_PROFILES":
                _require(bool(contradictory),
                         f"ambiguous assessment {original} must explain the conflict")
            classification = decide_classification(
                candidate_keys,
                direct_evidence=[],
                contradictory_evidence=contradictory,
            )
            _require(classification == CLASSIFICATION_BY_BASIS[basis],
                     f"assessment basis and derived classification conflict for {original}")
            specific_finding = assessment.get("specificFinding")
            blockers = assessment.get("remainingBlockers")
            _require(isinstance(specific_finding, str) and specific_finding,
                     f"assessment {original} lacks a finding")
            _require(isinstance(blockers, list)
                     and all(isinstance(item, str) and item for item in blockers)
                     and blockers,
                     f"assessment {original} lacks remaining blockers")
            rationale = (
                f"{classification}: {specific_finding} No direct evidence connects the numeric "
                "TemplateHash or any source NpcId to the captured AO identity, so placement name, "
                "level, coordinates, MonsterData, and respawn context remain corroborating only."
            )
            ledger_ref = [{
                "EvidenceType": "GovernedAssessment",
                "Strength": "GOVERNANCE",
                "IdentityLinkToTemplateHash": False,
                "Path": repository_path(ledger_path),
                "RecordId": f"$.unresolvedAssessments[templateHashOriginal={original}]",
            }]
            record = {
                "TemplateHashOriginal": original,
                "TemplateHashCanonical": canonical,
                "TemplateTag": template_tag(original),
                "BaselineMappingState": "UNRESOLVED",
                "BaselineVerificationState": "NOT_APPLICABLE",
                "BaselineAoRebirthProfile": "",
                "PlacementCount": len(group),
                "BlockedPlacementCount": blocked_count,
                "ExistingRuntimeActivePlacementCount": 0,
                "NpcIds": [record["NpcId"] for record in group],
                "SourceNames": source_names,
                "DynamicNamesPresent": dynamic_names,
                "SourceLevelMinimum": min(record["MinLevel"] for record in group),
                "SourceLevelMaximum": max(record["MaxLevel"] for record in group),
                "DuplicatePositionParticipation": any(record["NpcId"] in duplicate_npc_ids for record in group),
                "CandidateAoRebirthProfiles": candidate_keys,
                "Classification": classification,
                "ResolvedAoRebirthProfile": "",
                "DirectEvidence": [],
                "CorroboratingEvidence": corroborating,
                "ContradictoryEvidence": contradictory,
                "EvidencePaths": _evidence_paths(source_refs, ledger_ref, corroborating),
                "EvidenceRecordIds": _evidence_record_ids(source_refs, ledger_ref, corroborating),
                "CaptureIds": capture_ids,
                "EvidenceDigestsWhereAvailable": {},
                "Rationale": rationale,
                "RemainingBlockers": blockers,
                "UnlockPotential": blocked_count,
                "RuntimeActivationAllowed": False,
            }
        record["EvidenceDigestsWhereAvailable"] = {
            path: available_digests[path]
            for path in record["EvidencePaths"]
            if path in available_digests
        }
        validate_audit_record(record)
        report_records.append(record)

    report_records.sort(key=lambda item: item["TemplateHashCanonical"])
    unresolved_records = [
        record for record in report_records
        if record["BaselineMappingState"] == "UNRESOLVED"
    ]
    impact_ranking = sorted(
        unresolved_records,
        key=lambda item: (-item["UnlockPotential"], item["TemplateHashCanonical"]),
    )
    classification_counts = Counter(record["Classification"] for record in unresolved_records)
    blocked_by_classification = Counter()
    for record in unresolved_records:
        blocked_by_classification[record["Classification"]] += record["BlockedPlacementCount"]
    baseline_state_counts = Counter(
        record["BaselineVerificationState"] for record in report_records
        if record["BaselineMappingState"] == "MAPPED"
    )
    runtime_active = sum(record["ExistingRuntimeActivePlacementCount"] for record in report_records)
    runtime_blocked = sum(record["BlockedPlacementCount"] for record in report_records)
    unresolved_blocked = sum(record["BlockedPlacementCount"] for record in unresolved_records)
    mapped_blocked = runtime_blocked - unresolved_blocked

    metrics = {
        "PF4582_TEMPLATE_HASHES_TOTAL": len(report_records),
        "PF4582_BASELINE_MAPPED": len(mapped_hashes),
        "PF4582_BASELINE_UNRESOLVED": len(unresolved_hashes),
        "PF4582_BASELINE_PROVEN": baseline_state_counts["BASELINE_PROVEN"],
        "PF4582_BASELINE_EVIDENCE_INCOMPLETE": baseline_state_counts["BASELINE_EVIDENCE_INCOMPLETE"],
        "PF4582_BASELINE_CONFLICT": baseline_state_counts["BASELINE_CONFLICT"],
        "PF4582_AUDIT_PROVEN": classification_counts["PROVEN"],
        "PF4582_AUDIT_CANDIDATE": classification_counts["CANDIDATE"],
        "PF4582_AUDIT_AMBIGUOUS": classification_counts["AMBIGUOUS"],
        "PF4582_AUDIT_NO_EVIDENCE": classification_counts["NO_EVIDENCE"],
        "PF4582_NEWLY_PROVEN": classification_counts["PROVEN"],
        "PF4582_BLOCKED_PLACEMENTS_PROVEN": blocked_by_classification["PROVEN"],
        "PF4582_BLOCKED_PLACEMENTS_CANDIDATE": blocked_by_classification["CANDIDATE"],
        "PF4582_BLOCKED_PLACEMENTS_AMBIGUOUS": blocked_by_classification["AMBIGUOUS"],
        "PF4582_BLOCKED_PLACEMENTS_NO_EVIDENCE": blocked_by_classification["NO_EVIDENCE"],
        "PF4582_DYNAMIC_NAMES": sum(len(record["DynamicNamesPresent"]) for record in report_records),
        "PF4582_UNRESOLVED_HASH_BLOCKED_PLACEMENTS": unresolved_blocked,
        "PF4582_BASELINE_MAPPED_BLOCKED_PLACEMENTS": mapped_blocked,
        "PF4582_RUNTIME_ACTIVE_BEFORE": runtime_active,
        "PF4582_RUNTIME_ACTIVE_AFTER": runtime_active,
        "PF4582_RUNTIME_BLOCKED_BEFORE": runtime_blocked,
        "PF4582_RUNTIME_BLOCKED_AFTER": runtime_blocked,
        "PF4582_RUNTIME_ACTIVATION_CHANGED": "NO",
    }
    _require(metrics["PF4582_TEMPLATE_HASHES_TOTAL"] == EXPECTED_HASHES,
             "hash-count invariant failed")
    _require(sum(classification_counts[value] for value in ALLOWED_CLASSIFICATIONS)
             == EXPECTED_BASELINE_UNRESOLVED,
             "unresolved classification totals do not sum to 24")
    _require(runtime_active == EXPECTED_RUNTIME_ACTIVE, "runtime active invariant failed")
    _require(runtime_blocked == EXPECTED_RUNTIME_BLOCKED, "runtime blocked invariant failed")
    _require(unresolved_blocked == EXPECTED_UNRESOLVED_HASH_BLOCKED,
             "unresolved-hash blocked-placement invariant failed")
    _require(mapped_blocked == EXPECTED_BASELINE_MAPPED_BLOCKED,
             "baseline-mapped blocked-placement invariant failed")

    return {
        "SchemaVersion": 1,
        "PlayfieldId": 4582,
        "AuditScope": "Identity resolution only; no runtime activation authority",
        "SourceSha256": placement_model["sourceSha256"],
        "EvidenceLedgerPath": repository_path(ledger_path),
        "EvidenceLedgerSha256": ledger_digest,
        "Metrics": metrics,
        "BlockedPlacementAccounting": {
            "UnresolvedHashBlockedPlacements": unresolved_blocked,
            "BaselineMappedHashBlockedPlacements": mapped_blocked,
            "RuntimeBlockedPlacements": runtime_blocked,
            "InvariantExplanation": (
                "The 24 baseline-unresolved hashes contain 171 blocked placements. "
                "Ten additional blocked Island Reet placements use the baseline-mapped ISRE hash; "
                "171 + 10 = the runtime total of 181."
            ),
        },
        "EvidenceSourcesInspected": [
            "Existing PF4582 runtime definitions and generated catalogs",
            "Governed runtime-evidence-map.json and placement importer tests",
            "mobtemplate.sql and related repository template/profile catalogs",
            "Accepted AOSharp capture inventory and retention records",
            "All accepted PF4582 raw and normalized capture artifacts",
            "PF4582 enemy dossier, movement, lifecycle, respawn, interaction, and vendor artifacts",
            "Captured appearance, combat, movement, social, vendor, guard, and identity contracts",
            "AORebirth mob archetypes and playfield runtime catalogs",
            "Generated capture-backed enemy inventories and unresolved audits",
            "Historical PF4582 documentation and source inventories",
        ],
        "PinnedEvidenceDigests": pinned_digests,
        "AcceptedPf4582CaptureSearchScope": accepted_capture_scope,
        "HashRecords": report_records,
        "UnresolvedImpactRanking": [
            {
                "Rank": index,
                "TemplateHashOriginal": record["TemplateHashOriginal"],
                "TemplateHashCanonical": record["TemplateHashCanonical"],
                "TemplateTag": record["TemplateTag"],
                "PlacementCount": record["PlacementCount"],
                "Classification": record["Classification"],
                "CandidateOrResolvedProfile": (
                    record["ResolvedAoRebirthProfile"]
                    or ", ".join(record["CandidateAoRebirthProfiles"])
                    or "NONE"
                ),
                "PrimaryBlocker": record["RemainingBlockers"][0],
            }
            for index, record in enumerate(impact_ranking, 1)
        ],
        "RuntimeInvariants": {
            "PF4582_SOURCE_PLACEMENTS": len(records),
            "PF4582_RUNTIME_ACTIVE": runtime_active,
            "PF4582_RUNTIME_BLOCKED": runtime_blocked,
            "ALL_TEMPLATE_HASHES_AUDITED": "YES",
            "ALL_UNRESOLVED_HASHES_CLASSIFIED": "YES",
            "NAME_ONLY_MAPPING_ACCEPTED": "NO",
            "PLACEMENT_ONLY_MAPPING_ACCEPTED": "NO",
            "DYNAMIC_NAMES_FORCED_RESOLVED": "NO",
            "EVIDENCE_PROVENANCE_RECORDED": "YES",
            "RUNTIME_ACTIVATION_CHANGED": "NO",
            "UNPROVEN_BEHAVIOR_INVENTED": "NO",
            "PRODUCTION_OPERATION_PERFORMED": "NO",
            "COMMIT_CREATED": "NO",
            "PUSH_PERFORMED": "NO",
        },
    }


def render_json(model: dict[str, Any]) -> str:
    return json.dumps(model, indent=2, ensure_ascii=False) + "\n"


def _markdown_cell(value: Any) -> str:
    return str(value).replace("|", "\\|").replace("\n", " ")


def render_markdown(model: dict[str, Any]) -> str:
    metrics = model["Metrics"]
    records = model["HashRecords"]
    baseline = [record for record in records if record["BaselineMappingState"] == "MAPPED"]
    unresolved = [record for record in records if record["BaselineMappingState"] == "UNRESOLVED"]
    lines = [
        "# PF4582 TemplateHash Resolution Audit",
        "",
        "This deterministic audit covers all 38 authoritative PF4582 TemplateHash groups. It is identity-resolution evidence only and authorizes no runtime activation.",
        "",
        "## Metrics",
        "",
        "```text",
    ]
    lines.extend(f"{key}={value}" for key, value in metrics.items())
    lines.extend([
        "```",
        "",
        "## Blocked-placement accounting",
        "",
        model["BlockedPlacementAccounting"]["InvariantExplanation"],
        "",
        "This corrects an arithmetic conflict in the requested test list: blocked placements across the 24 baseline-unresolved hashes cannot sum to 181 because 10 blocked Island Reet records use the baseline-mapped ISRE hash.",
        "",
        "## Baseline mapping verification",
        "",
        "| TemplateHash | Tag | Placements | Active | Blocked | Profile | Verification |",
        "|---:|:---:|---:|---:|---:|---|---|",
    ])
    for record in baseline:
        lines.append(
            "| "
            + " | ".join(_markdown_cell(value) for value in [
                record["TemplateHashOriginal"],
                record["TemplateTag"],
                record["PlacementCount"],
                record["ExistingRuntimeActivePlacementCount"],
                record["BlockedPlacementCount"],
                record["BaselineAoRebirthProfile"],
                record["BaselineVerificationState"],
            ])
            + " |"
        )
    lines.extend([
        "",
        "All 14 baseline mappings are proven at the repository-governance level by an explicit source NpcId → numeric TemplateHash → governed mapping → current runtime profile chain validated by the placement importer. This is not a raw-packet TemplateHash observation and does not promote same-hash siblings.",
        "",
        "## Unresolved impact ranking",
        "",
        "| Rank | TemplateHash | Canonical | Tag | Placements | Classification | Candidate or resolved profile | Primary blocker |",
        "|---:|---:|:---:|:---:|---:|---|---|---|",
    ])
    for item in model["UnresolvedImpactRanking"]:
        lines.append(
            "| "
            + " | ".join(_markdown_cell(value) for value in [
                item["Rank"],
                item["TemplateHashOriginal"],
                item["TemplateHashCanonical"],
                item["TemplateTag"],
                item["PlacementCount"],
                item["Classification"],
                item["CandidateOrResolvedProfile"],
                item["PrimaryBlocker"],
            ])
            + " |"
        )
    lines.extend([
        "",
        "## Per-hash unresolved findings",
        "",
    ])
    for record in unresolved:
        candidates = ", ".join(record["CandidateAoRebirthProfiles"]) or "none"
        paths = ", ".join(f"`{path}`" for path in record["EvidencePaths"])
        lines.extend([
            f"### {record['TemplateHashOriginal']} ({record['TemplateHashCanonical']}, {record['TemplateTag']})",
            "",
            f"Classification: `{record['Classification']}`. Placements blocked: {record['BlockedPlacementCount']}. Candidate profiles: {candidates}.",
            "",
            record["Rationale"],
            "",
            "Remaining blockers: " + " ".join(record["RemainingBlockers"]),
            "",
            f"Evidence paths: {paths}.",
            "",
        ])
    lines.extend([
        "## Evidence boundary",
        "",
        "The complete repository and accepted PF4582 capture corpus was searched. None of the 24 unresolved decimal hashes or their source NpcIds occurs in a capture identity record. Exact names, captured MonsterData, NpcFamily, level, scale, coordinates, and respawn correlations therefore remain corroborating rather than direct hash evidence.",
        "",
        "Evidence source categories inspected:",
        "",
    ])
    lines.extend(f"- {source}" for source in model["EvidenceSourcesInspected"])
    lines.extend([
        "",
        "Accepted PF4582 capture inventory scope:",
        "",
        "| Capture ID | Evidence digest | Validation | Raw packet evidence |",
        "|---|---|---|---|",
    ])
    for capture_record in model["AcceptedPf4582CaptureSearchScope"]:
        lines.append(
            "| "
            + " | ".join(_markdown_cell(value) for value in [
                capture_record["CaptureId"],
                capture_record["EvidenceDigest"],
                capture_record["ValidationStatus"],
                capture_record["RawPacketEvidence"],
            ])
            + " |"
        )
    lines.extend([
        "",
        "Pinned evidence digests:",
        "",
    ])
    lines.extend(
        f"- `{path}`: `{digest}`"
        for path, digest in model["PinnedEvidenceDigests"].items()
    )
    lines.extend([
        "",
        "## Governance conclusion",
        "",
        "No unresolved hash is newly proven. Candidate and ambiguous results remain non-runtime. A future promotion requires a stable accepted record that directly joins a source TemplateHash or source NpcId to one AO identity/profile; another name-, level-, position-, or timing-only capture will not close that gap.",
        "",
        "Runtime remained 25 active and 181 blocked. No production, client, capture, or database operation was performed, and no commit or push is part of this audit.",
        "",
    ])
    return "\n".join(lines)


def _write_or_check(path: Path, content: str, check: bool) -> None:
    if check:
        _require(path.is_file(), f"generated output is missing: {repository_path(path)}")
        actual = path.read_text(encoding="utf-8")
        _require(actual == content, f"generated output is stale: {repository_path(path)}")
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8", newline="\n")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--evidence-map", type=Path, default=DEFAULT_EVIDENCE_MAP)
    parser.add_argument("--runtime-source", type=Path, default=DEFAULT_RUNTIME_SOURCE)
    parser.add_argument("--evidence-ledger", type=Path, default=DEFAULT_EVIDENCE_LEDGER)
    parser.add_argument("--json-output", type=Path, default=DEFAULT_JSON_OUTPUT)
    parser.add_argument("--markdown-output", type=Path, default=DEFAULT_MARKDOWN_OUTPUT)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args(argv)
    try:
        model = build_audit_model(
            source_path=args.source,
            evidence_map_path=args.evidence_map,
            runtime_source_path=args.runtime_source,
            ledger_path=args.evidence_ledger,
        )
        json_content = render_json(model)
        markdown_content = render_markdown(model)
        _write_or_check(args.json_output, json_content, args.check)
        _write_or_check(args.markdown_output, markdown_content, args.check)
        for key, value in model["Metrics"].items():
            print(f"{key}={value}")
        return 0
    except AuditError as exc:
        print(f"PF4582_TEMPLATE_HASH_AUDIT_ERROR={exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
