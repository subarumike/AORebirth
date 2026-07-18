#!/usr/bin/env python3
"""Build a deterministic content ledger for every Subway and mixed AOSharp capture.

Location classification remains owned by ``inventory_aosharp_captures.py``.  This
companion report projects the content already present in capture artifacts and
keeps official-live, AORebirth-private, and unresolved evidence separate.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Iterable

import inventory_aosharp_captures as location_inventory


SELECTED_CLASSIFICATIONS = {"SUBWAY", "MIXED"}
OFFICIAL_LIVE_PF127_INSTANCES = (
    location_inventory.PF127_CORPUS_RUNTIME_INSTANCES - {127}
)
IDENTITY_PATTERN = re.compile(
    r"\(?([A-Za-z][A-Za-z0-9]*):([0-9A-Fa-f]+)\)?"
)

OUTPUT_CSV = "docs/generated/aosharp_subway_capture_content.csv"
OUTPUT_MD = "docs/generated/aosharp_subway_capture_content.md"

CSV_SCHEMAS: dict[str, set[str]] = {
    "raw-packets.csv": {
        "CapturedUtc",
        "Direction",
        "GlobalOrdinal",
        "Sequence",
        "N3TypeName",
        "PreservationStatus",
        "RawHex",
    },
    "enemy-state.csv": {
        "timestamp",
        "entityId",
        "level",
        "currentHealth",
        "maxHealth",
        "eventType",
    },
    "enemy-full-updates.csv": {
        "CapturedUtc",
        "Identity",
        "Name",
        "PlayfieldId",
        "Level",
        "Health",
        "MonsterData",
    },
    "scfu-appearance.csv": {
        "CapturedUtc",
        "Identity",
        "Name",
        "Level",
        "Health",
        "MonsterData",
    },
    "enemy-combat.csv": {
        "CapturedUtc",
        "MessageType",
        "SourceRole",
        "SourceIdentity",
        "TargetRole",
        "TargetIdentity",
        "Action",
        "Amount",
    },
    "enemy-movement.csv": {
        "CapturedUtc",
        "IdentityRole",
        "Identity",
        "MessageType",
        "MoveType",
    },
    "movement-packets.csv": {
        "CapturedUtc",
        "MessageType",
        "SourceIdentity",
        "SourceName",
        "TargetIdentity",
        "TargetName",
        "FollowKind",
    },
    "enemy-stat-updates.csv": {
        "CapturedUtc",
        "IdentityRole",
        "Identity",
        "Stat",
        "Value",
    },
    "npc-lifecycle.csv": {
        "CapturedUtc",
        "Phase",
        "PrimaryIdentity",
        "RelatedIdentity",
        "Name",
    },
    "corpse-full-updates.csv": {
        "CapturedUtc",
        "CorpseIdentity",
        "CorpseName",
        "PlayfieldId",
        "DeadNpcIdentity",
        "CorpseCatMesh",
        "CorpseCredits",
        "CorpseMonsterData",
    },
    "corpse-loot-observations.csv": {
        "CapturedUtc",
        "CorpseIdentity",
        "InitialSnapshot",
        "ItemCount",
        "DeadNpcIdentity",
        "EnemyName",
        "MonsterData",
        "EnemyLevel",
        "CorpseCredits",
        "Items",
        "CorrelationStatus",
    },
    "inventory-updates.csv": {
        "CapturedUtc",
        "InventoryIdentity",
        "Slot",
        "Count",
        "LowId",
        "HighId",
        "Quality",
    },
    "enemy-respawns.csv": {
        "Status",
        "DeathIdentity",
        "Name",
        "MonsterData",
        "DeathUtc",
        "CorpseIdentity",
        "RespawnIdentity",
        "RespawnDelaySeconds",
        "CandidateCount",
    },
    "vendor-full-updates.csv": {
        "CapturedUtc",
        "Identity",
        "OwnerType",
        "OwnerInstance",
        "PlayfieldId",
        "Template",
    },
    "shop-updates.csv": {
        "CapturedUtc",
        "TerminalIdentity",
        "Slot",
        "LowId",
        "HighId",
        "Quality",
    },
    "pf127-door-state.csv": {
        "CapturedUtc",
        "ResourcePlayfieldId",
        "RuntimePlayfieldId",
        "Identity",
        "PositionX",
        "PositionY",
        "PositionZ",
        "IsOpen",
        "IsLocked",
    },
    "pf127-line-of-sight.csv": {
        "CapturedUtc",
        "ResourcePlayfieldId",
        "RuntimePlayfieldId",
        "TargetIdentity",
        "TargetMonsterData",
        "TargetName",
        "SimpleCharIsInLineOfSight",
        "PlayfieldLineOfSight",
        "RaycastHit",
        "Usable",
    },
}

PENDING_PROJECTION_FILES = {
    "scfu-appearance.csv": "scfu-appearance.pending.csv",
    "corpse-full-updates.csv": "corpse-full-updates.pending.csv",
    "corpse-loot-observations.csv": "corpse-loot-observations.pending.csv",
    "enemy-respawns.csv": "enemy-respawns.pending.csv",
}

DECLARED_COUNT_FIELDS = {
    "enemy-state.csv": "enemyStateRows",
    "enemy-full-updates.csv": "enemyFullUpdateRows",
    "enemy-combat.csv": "enemyCombatRows",
    "enemy-movement.csv": "enemyMovementRows",
    "movement-packets.csv": "movementPacketRows",
    "enemy-stat-updates.csv": "enemyStatUpdateRows",
    "npc-lifecycle.csv": "npcLifecycleRows",
    "corpse-full-updates.csv": "corpseFullUpdateRows",
    "corpse-loot-observations.csv": "corpseLootObservationRows",
    "inventory-updates.csv": "inventoryUpdateRows",
    "enemy-respawns.csv": "enemyRespawnRows",
    "vendor-full-updates.csv": "vendorFullUpdateMessages",
    "shop-updates.csv": "shopUpdateRows",
    "scfu-appearance.csv": "scfuAppearanceRows",
}

REFERENCE_EXTENSIONS = {".cs", ".py", ".cmd", ".ps1", ".json", ".csv", ".md", ".txt"}
REFERENCE_CATEGORIES = (
    "runtime",
    "tests",
    "generator",
    "generated",
    "documentation",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", default=".")
    parser.add_argument("--output-csv", default=OUTPUT_CSV)
    parser.add_argument("--output-md", default=OUTPUT_MD)
    return parser.parse_args()


def string_value(value: object) -> str:
    return str(value).strip() if value is not None else ""


def integer_value(value: object) -> int | None:
    if isinstance(value, bool):
        return None
    if isinstance(value, int):
        return value
    text = string_value(value)
    if not text:
        return None
    try:
        return int(text, 10)
    except ValueError:
        return None


def float_value(value: object) -> float | None:
    if isinstance(value, bool):
        return None
    if isinstance(value, (int, float)):
        return float(value)
    text = string_value(value)
    if not text:
        return None
    try:
        return float(text)
    except ValueError:
        return None


def boolean_value(value: object) -> bool | None:
    if isinstance(value, bool):
        return value
    text = string_value(value).lower()
    if text == "true":
        return True
    if text == "false":
        return False
    return None


def normalize_identity(value: object) -> str:
    text = string_value(value)
    match = IDENTITY_PATTERN.fullmatch(text)
    if not match:
        return ""
    return "({0}:{1:08X})".format(match.group(1), int(match.group(2), 16))


def identity_from_numeric(identity_type: object, instance: object) -> str:
    parsed_type = integer_value(identity_type)
    parsed_instance = integer_value(instance)
    if parsed_type is None or parsed_instance is None:
        return ""
    type_name = {
        50000: "SimpleChar",
        51016: "Door",
        51050: "Corpse",
        51035: "VendingMachine",
    }.get(parsed_type, "Identity{0}".format(parsed_type))
    return "({0}:{1:08X})".format(type_name, parsed_instance & 0xFFFFFFFF)


def parse_semicolon_integers(value: object) -> set[int]:
    result: set[int] = set()
    for part in string_value(value).split(";"):
        parsed = integer_value(part)
        if parsed is not None:
            result.add(parsed)
    return result


def playfield_scope(value: object) -> str:
    parsed = location_inventory.normalize_playfield(value)
    if parsed is None:
        return ""
    if parsed in location_inventory.PF127_CORPUS_RUNTIME_INSTANCES:
        return "subway_exact"
    return "elsewhere_exact"


def capture_realm(base_row: dict[str, object]) -> tuple[str, str]:
    capture_values = set()
    capture_playfield = integer_value(base_row.get("capture_playfield_id"))
    if capture_playfield is not None:
        capture_values.add(capture_playfield)
    event_values = parse_semicolon_integers(base_row.get("event_playfield_ids"))
    runtime_values = {
        value
        for value in (
            location_inventory.runtime_instance(part)
            for part in string_value(base_row.get("runtime_playfield_ids")).split(";")
        )
        if value is not None
    }
    observed = capture_values | event_values | runtime_values
    private = 127 in observed
    official = sorted(observed.intersection(OFFICIAL_LIVE_PF127_INSTANCES))
    basis_parts = []
    if capture_values:
        basis_parts.append("capture=" + ",".join(str(value) for value in sorted(capture_values)))
    if event_values:
        basis_parts.append("events=" + ",".join(str(value) for value in sorted(event_values)))
    if runtime_values:
        basis_parts.append("runtime=" + ",".join(str(value) for value in sorted(runtime_values)))
    basis = ";".join(basis_parts) or "no-explicit-runtime-playfield"
    if private and official:
        return "unknown", "conflicting-private-and-official-signals;" + basis
    if official:
        return "official_live", "mapped-official-runtime;" + basis
    if private:
        return "aorebirth_private", "runtime-playfield-127;" + basis
    return "unknown", basis


def refine_realm_from_projected_runtime(
    capture_path: Path,
    realm: str,
    basis: str,
) -> tuple[str, str]:
    """Use only explicit projected runtime-playfield fields to refine realm."""
    observed: set[int] = set()
    sources: set[str] = set()
    for filename in ("pf127-door-state.csv", "pf127-line-of-sight.csv"):
        path = capture_path / filename
        if not path.exists() or path.stat().st_size == 0:
            continue
        try:
            with path.open("r", encoding="utf-8-sig", errors="replace", newline="") as stream:
                reader = csv.DictReader(stream)
                if "RuntimePlayfieldId" not in (reader.fieldnames or []):
                    continue
                for row in reader:
                    parsed = location_inventory.normalize_playfield(row.get("RuntimePlayfieldId"))
                    if parsed is not None:
                        observed.add(parsed)
                        sources.add(filename + ":RuntimePlayfieldId=" + str(parsed))
        except (OSError, csv.Error):
            continue
    has_private = 127 in observed
    has_official = bool(observed.intersection(OFFICIAL_LIVE_PF127_INSTANCES))
    artifact_basis = ";".join(sorted(sources))
    if has_private and has_official:
        return "unknown", "conflicting-projected-runtime-signals;" + basis + ";" + artifact_basis
    projected_realm = "aorebirth_private" if has_private else (
        "official_live" if has_official else ""
    )
    if not projected_realm:
        return realm, basis
    if realm == "unknown":
        prefix = "runtime-playfield-127" if projected_realm == "aorebirth_private" else "mapped-official-runtime"
        return projected_realm, prefix + ";" + artifact_basis
    if realm == projected_realm:
        return realm, basis + ";" + artifact_basis
    return "unknown", "conflicting-session-and-projected-runtime;" + basis + ";" + artifact_basis


def canonical_json(value: object) -> str:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True)


def schema_fingerprint(fieldnames: Iterable[str]) -> str:
    joined = ",".join(fieldnames)
    return hashlib.sha256(joined.encode("utf-8")).hexdigest()[:16]


def file_digest(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def record_timestamp(values: Iterable[object]) -> tuple[str, str]:
    timestamps = sorted({string_value(value) for value in values if string_value(value)})
    return (timestamps[0], timestamps[-1]) if timestamps else ("", "")


@dataclass
class IdentityMetadata:
    names: set[str] = field(default_factory=set)
    monster_data: set[int] = field(default_factory=set)
    levels: set[int] = field(default_factory=set)


class IdentityScopeIndex:
    def __init__(self) -> None:
        self._scopes: dict[str, set[str]] = defaultdict(set)

    def register(self, identity: str, scope: str) -> None:
        if not identity:
            return
        if scope == "subway_exact":
            self._scopes[identity].add("subway")
        elif scope == "elsewhere_exact":
            self._scopes[identity].add("elsewhere")

    def resolve(self, identity: str, explicit_scope: str, classification: str) -> str:
        if explicit_scope:
            return explicit_scope
        scopes = self._scopes.get(identity, set())
        if scopes == {"subway"}:
            return "subway_joined"
        if scopes == {"elsewhere"}:
            return "elsewhere_joined"
        if len(scopes) > 1:
            return "scope_conflict"
        if classification == "SUBWAY":
            return "subway_session"
        return "unscoped_mixed"


@dataclass
class EvidenceRecord:
    capture_id: str
    capture_path: str
    capture_classification: str
    capture_confidence: str
    source_realm: str
    source_basis: str
    subject_scope: str
    subject_kind: str
    subject_identity: str
    related_identity: str
    evidence_kind: str
    source_artifact: str
    source_schema: str
    evidence_status: str = "observed"
    names: set[str] = field(default_factory=set)
    monster_data: set[int] = field(default_factory=set)
    levels: set[int] = field(default_factory=set)
    timestamps: set[str] = field(default_factory=set)
    observations: Counter[str] = field(default_factory=Counter)
    numeric_values: Counter[str] = field(default_factory=Counter)
    issues: set[str] = field(default_factory=set)

    def add(
        self,
        *,
        name: object = "",
        monster_data: object = "",
        level: object = "",
        timestamps: Iterable[object] = (),
        observation: dict[str, object] | None = None,
        numeric: object = None,
        issue: str = "",
    ) -> None:
        parsed_name = string_value(name)
        if parsed_name:
            self.names.add(parsed_name)
        parsed_monster_data = integer_value(monster_data)
        if parsed_monster_data is not None and parsed_monster_data > 0:
            self.monster_data.add(parsed_monster_data)
        parsed_level = integer_value(level)
        if parsed_level is not None and parsed_level >= 0:
            self.levels.add(parsed_level)
        for value in timestamps:
            parsed = string_value(value)
            if parsed:
                self.timestamps.add(parsed)
        if observation is not None:
            cleaned = {
                key: value
                for key, value in observation.items()
                if value not in (None, "")
            }
            self.observations[canonical_json(cleaned)] += 1
        parsed_numeric = float_value(numeric)
        if parsed_numeric is not None:
            key = format(parsed_numeric, ".15g")
            self.numeric_values[key] += 1
        if issue:
            self.issues.add(issue)

    @property
    def observation_count(self) -> int:
        return max(sum(self.observations.values()), sum(self.numeric_values.values()), 1)


def reference_category(relative: str, suffix: str) -> str:
    lowered = relative.lower()
    if "aotomation.messaging.tests" in lowered or "/tests/" in lowered:
        return "tests"
    if lowered.startswith("docs/generated/"):
        return "generated"
    if lowered.startswith("docs/"):
        return "documentation"
    if lowered.startswith("aorebirth/server/zoneengine/") and suffix == ".cs":
        return "runtime"
    return "generator"


def collect_reference_categories(
    repo_root: Path,
    capture_ids: set[str],
    excluded_outputs: set[str],
) -> dict[str, dict[str, set[str]]]:
    references: dict[str, dict[str, set[str]]] = {
        capture_id: {category: set() for category in REFERENCE_CATEGORIES}
        for capture_id in capture_ids
    }
    roots = (
        repo_root / "docs",
        repo_root / "AORebirth" / "Server" / "ZoneEngine",
        repo_root / "Tools",
        repo_root / "tools-temp" / "AOSharpCaptureAnalyzer",
    )
    ignored_parts = {".git", "bin", "obj", "packages", "captures"}
    for root in roots:
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if not path.is_file() or path.suffix.lower() not in REFERENCE_EXTENSIONS:
                continue
            relative = path.relative_to(repo_root).as_posix()
            if relative in excluded_outputs or ignored_parts.intersection(path.parts):
                continue
            try:
                text = path.read_text(encoding="utf-8-sig", errors="replace")
            except OSError:
                continue
            category = reference_category(relative, path.suffix.lower())
            for capture_id in set(location_inventory.CAPTURE_ID_IN_TEXT.findall(text)):
                if capture_id in references:
                    references[capture_id][category].add(relative)
    return references


class CaptureAnalyzer:
    def __init__(
        self,
        repo_root: Path,
        base_row: dict[str, object],
        references: dict[str, set[str]],
    ) -> None:
        self.repo_root = repo_root
        self.base = base_row
        self.capture_id = string_value(base_row["capture_id"])
        self.relative_path = string_value(base_row["capture_path"])
        self.path = repo_root / self.relative_path
        self.classification = string_value(base_row["classification"])
        self.confidence = string_value(base_row["confidence"])
        self.realm, self.realm_basis = refine_realm_from_projected_runtime(
            self.path,
            *capture_realm(base_row),
        )
        self.references = references
        self.info = location_inventory.load_json(self.path / "capture_info.json")
        self.health = location_inventory.load_json(self.path / "capture-health.json")
        self.dossier = location_inventory.load_json(self.path / "enemy-dossier.json")
        self.scope_index = IdentityScopeIndex()
        self.identity_metadata: dict[str, IdentityMetadata] = defaultdict(IdentityMetadata)
        self.vendor_owner_ids: set[str] = set()
        self.records: dict[tuple[str, ...], EvidenceRecord] = {}
        self.artifact_status: dict[str, str] = {}
        self.artifact_rows: dict[str, int] = {}
        self.artifact_schemas: dict[str, str] = {}
        self.issues: set[str] = set()

    def _read_csv(
        self,
        filename: str,
        *,
        schema_filename: str | None = None,
    ) -> tuple[list[dict[str, str]], str]:
        path = self.path / filename
        if not path.exists():
            self.artifact_status[filename] = "missing"
            return [], ""
        if path.stat().st_size == 0:
            self.artifact_status[filename] = "empty-file"
            self.artifact_rows[filename] = 0
            return [], ""
        try:
            with path.open("r", encoding="utf-8-sig", errors="replace", newline="") as stream:
                reader = csv.DictReader(stream)
                fieldnames = reader.fieldnames or []
                required = CSV_SCHEMAS[schema_filename or filename]
                missing = sorted(required.difference(fieldnames))
                schema = "csv:" + schema_fingerprint(fieldnames)
                self.artifact_schemas[filename] = schema
                if missing:
                    issue = filename + ":schema-missing=" + ",".join(missing)
                    self.artifact_status[filename] = "schema-invalid"
                    self.issues.add(issue)
                    return [], schema
                rows = list(reader)
        except (OSError, csv.Error) as error:
            issue = filename + ":read-error=" + type(error).__name__
            self.artifact_status[filename] = "read-error"
            self.issues.add(issue)
            return [], ""
        self.artifact_rows[filename] = len(rows)
        self.artifact_status[filename] = "rows={0}".format(len(rows))
        return rows, schema

    def _read_projection_csv(
        self,
        filename: str,
    ) -> tuple[list[dict[str, str]], str, str, bool]:
        """Read one projection generation without mixing final and pending rows."""
        final_path = self.path / filename
        if final_path.exists():
            rows, schema = self._read_csv(filename)
            return rows, schema, filename, False

        pending_filename = PENDING_PROJECTION_FILES[filename]
        pending_path = self.path / pending_filename
        if pending_path.exists():
            rows, schema = self._read_csv(
                pending_filename,
                schema_filename=filename,
            )
            self.artifact_status[filename] = "using-pending=" + pending_filename
            return rows, schema, pending_filename, True

        rows, schema = self._read_csv(filename)
        return rows, schema, filename, False

    def _metadata(
        self,
        identity: str,
        name: object = "",
        monster_data: object = "",
        level: object = "",
    ) -> None:
        if not identity:
            return
        metadata = self.identity_metadata[identity]
        parsed_name = string_value(name)
        if parsed_name:
            metadata.names.add(parsed_name)
        parsed_monster_data = integer_value(monster_data)
        if parsed_monster_data is not None and parsed_monster_data > 0:
            metadata.monster_data.add(parsed_monster_data)
        parsed_level = integer_value(level)
        if parsed_level is not None and parsed_level >= 0:
            metadata.levels.add(parsed_level)

    def _subject_kind(self, identity: str, role: object = "") -> str:
        parsed_role = string_value(role).lower()
        if parsed_role in {"enemy", "player", "pet", "corpse", "vendor"}:
            return "vendor_npc" if parsed_role == "vendor" else parsed_role
        if identity in self.vendor_owner_ids:
            return "vendor_npc"
        if identity.startswith("(Corpse:"):
            return "corpse"
        if identity.startswith("(VendingMachine:"):
            return "vendor_terminal"
        if identity.startswith("(Door:"):
            return "door"
        return "enemy"

    def _record(
        self,
        *,
        subject_kind: str,
        subject_identity: str,
        related_identity: str,
        evidence_kind: str,
        source_artifact: str,
        source_schema: str,
        explicit_scope: str = "",
    ) -> EvidenceRecord:
        scope = self.scope_index.resolve(
            subject_identity,
            explicit_scope,
            self.classification,
        )
        key = (
            scope,
            subject_kind,
            subject_identity,
            related_identity,
            evidence_kind,
            source_artifact,
        )
        if key not in self.records:
            self.records[key] = EvidenceRecord(
                capture_id=self.capture_id,
                capture_path=self.relative_path,
                capture_classification=self.classification,
                capture_confidence=self.confidence,
                source_realm=self.realm,
                source_basis=self.realm_basis,
                subject_scope=scope,
                subject_kind=subject_kind,
                subject_identity=subject_identity,
                related_identity=related_identity,
                evidence_kind=evidence_kind,
                source_artifact=source_artifact,
                source_schema=source_schema,
            )
        return self.records[key]

    def _parse_vendors(self) -> None:
        rows, schema = self._read_csv("vendor-full-updates.csv")
        for row in rows:
            terminal = normalize_identity(row.get("Identity"))
            owner = identity_from_numeric(row.get("OwnerType"), row.get("OwnerInstance"))
            scope = playfield_scope(row.get("PlayfieldId"))
            self.scope_index.register(terminal, scope)
            self.scope_index.register(owner, scope)
            if owner:
                self.vendor_owner_ids.add(owner)
            terminal_record = self._record(
                subject_kind="vendor_terminal",
                subject_identity=terminal,
                related_identity=owner,
                evidence_kind="vendor_full_update",
                source_artifact="vendor-full-updates.csv",
                source_schema=schema,
                explicit_scope=scope,
            )
            terminal_record.add(
                timestamps=(row.get("CapturedUtc"),),
                observation={
                    "playfield_id": integer_value(row.get("PlayfieldId")),
                    "position": [
                        float_value(row.get("PositionX")),
                        float_value(row.get("PositionY")),
                        float_value(row.get("PositionZ")),
                    ],
                    "template": integer_value(row.get("Template")),
                    "mesh": integer_value(row.get("Mesh")),
                    "buy_modifier": float_value(row.get("BuyModifier")),
                    "sell_modifier": float_value(row.get("SellModifier")),
                },
            )
            if owner:
                owner_record = self._record(
                    subject_kind="vendor_npc",
                    subject_identity=owner,
                    related_identity=terminal,
                    evidence_kind="vendor_owner_link",
                    source_artifact="vendor-full-updates.csv",
                    source_schema=schema,
                    explicit_scope=scope,
                )
                owner_record.add(timestamps=(row.get("CapturedUtc"),))

    def _parse_dossier(self) -> None:
        enemies = self.dossier.get("enemies")
        if not isinstance(enemies, list):
            self.artifact_status["enemy-dossier.json"] = (
                "missing" if not (self.path / "enemy-dossier.json").exists() else "no-enemy-list"
            )
            return
        self.artifact_status["enemy-dossier.json"] = "rows={0}".format(len(enemies))
        self.artifact_rows["enemy-dossier.json"] = len(enemies)
        schema = "json:enemy-dossier"
        for enemy in enemies:
            if not isinstance(enemy, dict):
                continue
            identity = normalize_identity(enemy.get("identity"))
            direct_scope = playfield_scope(enemy.get("resourcePlayfieldId"))
            if not direct_scope:
                direct_scope = playfield_scope(enemy.get("runtimePlayfieldId"))
            if not direct_scope:
                direct_scope = playfield_scope(enemy.get("capturePlayfieldIdentity"))
            self.scope_index.register(identity, direct_scope)
            self._metadata(identity, enemy.get("name"), enemy.get("monsterData"), enemy.get("level"))
            record = self._record(
                subject_kind=self._subject_kind(identity),
                subject_identity=identity,
                related_identity="",
                evidence_kind="population_dossier",
                source_artifact="enemy-dossier.json",
                source_schema=schema,
                explicit_scope=direct_scope,
            )
            record.add(
                name=enemy.get("name"),
                monster_data=enemy.get("monsterData"),
                level=enemy.get("level"),
                timestamps=(enemy.get("firstSeenUtc"), enemy.get("lastUpdateUtc")),
                observation={
                    "max_health": integer_value(enemy.get("maxHealth")),
                    "monster_scale": integer_value(enemy.get("monsterScale")),
                    "cat_mesh": integer_value(enemy.get("catMesh")),
                    "head_mesh": integer_value(enemy.get("headMesh")),
                    "run_speed": integer_value(enemy.get("runSpeed")),
                    "npc_family": integer_value(enemy.get("npcFamily")),
                    "los_height": integer_value(enemy.get("losHeight")),
                    "position": enemy.get("position"),
                    "death_observed": enemy.get("deathObserved"),
                    "population_observed": enemy.get("populationEvidenceObserved"),
                    "population_source": enemy.get("populationEvidenceSource"),
                },
            )

    def _parse_character_updates(self) -> None:
        rows, schema = self._read_csv("enemy-full-updates.csv")
        for row in rows:
            identity = normalize_identity(row.get("Identity"))
            scope = playfield_scope(row.get("PlayfieldId"))
            self.scope_index.register(identity, scope)
            self._metadata(identity, row.get("Name"), row.get("MonsterData"), row.get("Level"))
            record = self._record(
                subject_kind=self._subject_kind(identity),
                subject_identity=identity,
                related_identity=normalize_identity(row.get("FightingTargetIdentity")),
                evidence_kind="simple_char_full_update",
                source_artifact="enemy-full-updates.csv",
                source_schema=schema,
                explicit_scope=scope,
            )
            record.add(
                name=row.get("Name"),
                monster_data=row.get("MonsterData"),
                level=row.get("Level"),
                timestamps=(row.get("CapturedUtc"),),
                observation={
                    "health": integer_value(row.get("Health")),
                    "health_damage": integer_value(row.get("HealthDamage")),
                    "monster_scale": integer_value(row.get("MonsterScale")),
                    "npc_family": integer_value(row.get("NPCFamily")),
                    "los_height": integer_value(row.get("LosHeight")),
                    "head_mesh": integer_value(row.get("HeadMesh")),
                    "run_speed": integer_value(row.get("RunSpeedBase")),
                    "flags": row.get("Flags"),
                    "textures": row.get("Textures"),
                    "meshes": row.get("Meshes"),
                    "position": [
                        float_value(row.get("PositionX")),
                        float_value(row.get("PositionY")),
                        float_value(row.get("PositionZ")),
                    ],
                    "heading": [
                        float_value(row.get("HeadingX")),
                        float_value(row.get("HeadingY")),
                        float_value(row.get("HeadingZ")),
                        float_value(row.get("HeadingW")),
                    ],
                },
            )

        rows, schema, source_artifact, projection_pending = self._read_projection_csv(
            "scfu-appearance.csv"
        )
        for row in rows:
            identity = normalize_identity(row.get("Identity"))
            decode_status = string_value(row.get("DecodeStatus")).lower()
            fully_consumed = boolean_value(row.get("DecodeFullyConsumed"))
            decode_incomplete = (
                bool(decode_status) and decode_status != "decoded_complete"
            ) or fully_consumed is False
            scope = playfield_scope(row.get("PlayfieldId"))
            self.scope_index.register(identity, scope)
            self._metadata(identity, row.get("Name"), row.get("MonsterData"), row.get("Level"))
            owner = normalize_identity(row.get("Owner"))
            kind = "pet" if owner else self._subject_kind(identity)
            record = self._record(
                subject_kind=kind,
                subject_identity=identity,
                related_identity=owner,
                evidence_kind="scfu_appearance",
                source_artifact=source_artifact,
                source_schema=schema,
                explicit_scope=scope,
            )
            record.add(
                name=row.get("Name"),
                monster_data=row.get("MonsterData"),
                level=row.get("Level"),
                timestamps=(row.get("CapturedUtc"),),
                observation={
                    "decode_status": decode_status,
                    "fully_consumed": fully_consumed,
                    "health": integer_value(row.get("Health")),
                    "monster_scale": integer_value(row.get("MonsterScale")),
                    "npc_family": integer_value(row.get("NpcFamily")),
                    "los_height": integer_value(row.get("NpcLosHeight")),
                    "appearance": integer_value(row.get("AppearanceValue")),
                    "breed": row.get("Breed"),
                    "gender": row.get("Gender"),
                    "race": row.get("Race"),
                    "head_mesh": integer_value(row.get("HeadMesh")),
                    "run_speed": integer_value(row.get("RunSpeedBase")),
                    "active_nanos": row.get("ActiveNanos"),
                    "textures": row.get("Textures"),
                    "meshes": row.get("Meshes"),
                },
                issue="projection-pending" if projection_pending else "",
            )
            if decode_incomplete:
                record.issues.add("incomplete-decode-not-absence")
                record.evidence_status = (
                    "projection-pending-incomplete"
                    if projection_pending
                    else "incomplete-observation"
                )
            elif projection_pending and record.evidence_status == "observed":
                record.evidence_status = "projection-pending-observed"

    def _parse_state_and_stats(self) -> None:
        rows, schema = self._read_csv("enemy-state.csv")
        for row in rows:
            identity = normalize_identity(row.get("entityId"))
            record = self._record(
                subject_kind=self._subject_kind(identity),
                subject_identity=identity,
                related_identity="",
                evidence_kind="enemy_state",
                source_artifact="enemy-state.csv",
                source_schema=schema,
            )
            record.add(
                level=row.get("level"),
                timestamps=(row.get("timestamp"),),
                observation={
                    "event_type": row.get("eventType"),
                    "evidence_source": row.get("evidenceSource"),
                    "current_health": integer_value(row.get("currentHealth")),
                    "max_health": integer_value(row.get("maxHealth")),
                },
            )
            self._metadata(identity, level=row.get("level"))

        rows, schema = self._read_csv("enemy-stat-updates.csv")
        for row in rows:
            identity = normalize_identity(row.get("Identity"))
            record = self._record(
                subject_kind=self._subject_kind(identity, row.get("IdentityRole")),
                subject_identity=identity,
                related_identity="",
                evidence_kind="stat_update",
                source_artifact="enemy-stat-updates.csv",
                source_schema=schema,
            )
            record.add(
                timestamps=(row.get("CapturedUtc"),),
                observation={
                    "message_type": row.get("MessageType"),
                    "stat": row.get("Stat"),
                    "stat_id": integer_value(row.get("StatId")),
                    "value": integer_value(row.get("Value")),
                },
            )

    def _parse_combat(self) -> None:
        rows, schema = self._read_csv("enemy-combat.csv")
        for row in rows:
            source = normalize_identity(row.get("SourceIdentity"))
            target = normalize_identity(row.get("TargetIdentity"))
            common = {
                "message_type": row.get("MessageType"),
                "action": row.get("Action"),
                "amount": integer_value(row.get("Amount")),
                "target_hp": integer_value(row.get("TargetHp")),
            }
            if source:
                record = self._record(
                    subject_kind=self._subject_kind(source, row.get("SourceRole")),
                    subject_identity=source,
                    related_identity=target,
                    evidence_kind="combat_as_source",
                    source_artifact="enemy-combat.csv",
                    source_schema=schema,
                )
                record.add(
                    timestamps=(row.get("CapturedUtc"),),
                    numeric=row.get("Amount"),
                    observation={
                        **common,
                        "source_role": row.get("SourceRole"),
                        "target_role": row.get("TargetRole"),
                    },
                )
            if target:
                record = self._record(
                    subject_kind=self._subject_kind(target, row.get("TargetRole")),
                    subject_identity=target,
                    related_identity=source,
                    evidence_kind="combat_as_target",
                    source_artifact="enemy-combat.csv",
                    source_schema=schema,
                )
                record.add(
                    timestamps=(row.get("CapturedUtc"),),
                    numeric=row.get("Amount"),
                    observation={
                        **common,
                        "source_role": row.get("SourceRole"),
                        "target_role": row.get("TargetRole"),
                    },
                )

    def _parse_movement(self) -> None:
        rows, schema = self._read_csv("enemy-movement.csv")
        for row in rows:
            identity = normalize_identity(row.get("Identity"))
            record = self._record(
                subject_kind=self._subject_kind(identity, row.get("IdentityRole")),
                subject_identity=identity,
                related_identity="",
                evidence_kind="movement",
                source_artifact="enemy-movement.csv",
                source_schema=schema,
            )
            record.add(
                timestamps=(row.get("CapturedUtc"),),
                observation={
                    "message_type": row.get("MessageType"),
                    "move_type": row.get("MoveType"),
                },
            )

        rows, schema = self._read_csv("movement-packets.csv")
        for row in rows:
            source = normalize_identity(row.get("SourceIdentity"))
            target = normalize_identity(row.get("TargetIdentity"))
            self._metadata(source, row.get("SourceName"))
            self._metadata(target, row.get("TargetName"))
            record = self._record(
                subject_kind=self._subject_kind(source),
                subject_identity=source,
                related_identity=target,
                evidence_kind="movement_path",
                source_artifact="movement-packets.csv",
                source_schema=schema,
            )
            record.add(
                name=row.get("SourceName"),
                timestamps=(row.get("CapturedUtc"),),
                observation={
                    "message_type": row.get("MessageType"),
                    "follow_kind": row.get("FollowKind"),
                    "path_count": integer_value(row.get("PathCount")),
                    "speed": float_value(row.get("Speed")),
                },
            )

    def _parse_corpses_and_loot(self) -> None:
        rows, schema, source_artifact, projection_pending = self._read_projection_csv(
            "corpse-full-updates.csv"
        )
        for row in rows:
            corpse = normalize_identity(row.get("CorpseIdentity"))
            dead_npc = normalize_identity(row.get("DeadNpcIdentity"))
            corpse_name = row.get("CorpseName") or row.get("DeadNpcName")
            scope = playfield_scope(row.get("PlayfieldId"))
            self.scope_index.register(corpse, scope)
            self.scope_index.register(dead_npc, scope)
            self._metadata(corpse, corpse_name, row.get("CorpseMonsterData"))
            record = self._record(
                subject_kind="corpse",
                subject_identity=corpse,
                related_identity=dead_npc,
                evidence_kind="corpse_full_update",
                source_artifact=source_artifact,
                source_schema=schema,
                explicit_scope=scope,
            )
            record.add(
                name=corpse_name,
                monster_data=row.get("CorpseMonsterData"),
                timestamps=(row.get("CapturedUtc"),),
                numeric=row.get("CorpseCredits"),
                observation={
                    "corpse_name": row.get("CorpseName"),
                    "dead_npc_name": row.get("DeadNpcName"),
                    "playfield_id": integer_value(row.get("PlayfieldId")),
                    "cat_mesh": integer_value(row.get("CorpseCatMesh")),
                    "credits": integer_value(row.get("CorpseCredits")),
                    "monster_scale": integer_value(row.get("MonsterScale")),
                    "packet_length": integer_value(row.get("PacketLength")),
                    "position": [
                        float_value(row.get("PositionX")),
                        float_value(row.get("PositionY")),
                        float_value(row.get("PositionZ")),
                    ],
                },
                issue="projection-pending" if projection_pending else "",
            )
            if projection_pending:
                record.evidence_status = "projection-pending-observed"

        rows, schema, source_artifact, projection_pending = self._read_projection_csv(
            "corpse-loot-observations.csv"
        )
        for row in rows:
            corpse = normalize_identity(row.get("CorpseIdentity"))
            dead_npc = normalize_identity(row.get("DeadNpcIdentity"))
            self._metadata(dead_npc, row.get("EnemyName"), row.get("MonsterData"), row.get("EnemyLevel"))
            record = self._record(
                subject_kind="corpse",
                subject_identity=corpse,
                related_identity=dead_npc,
                evidence_kind="loot_snapshot",
                source_artifact=source_artifact,
                source_schema=schema,
            )
            record.add(
                name=row.get("EnemyName"),
                monster_data=row.get("MonsterData"),
                level=row.get("EnemyLevel"),
                timestamps=(row.get("CapturedUtc"),),
                numeric=row.get("CorpseCredits"),
                observation={
                    "open_ordinal": integer_value(row.get("OpenOrdinal")),
                    "initial_snapshot": boolean_value(row.get("InitialSnapshot")),
                    "item_count": integer_value(row.get("ItemCount")),
                    "credits": integer_value(row.get("CorpseCredits")),
                    "items": row.get("Items"),
                    "player_identity": normalize_identity(row.get("PlayerIdentity")),
                    "player_level": integer_value(row.get("PlayerLevel")),
                    "correlation_status": row.get("CorrelationStatus"),
                },
                issue="projection-pending" if projection_pending else "",
            )
            if projection_pending:
                record.evidence_status = "projection-pending-observed"

        rows, schema = self._read_csv("inventory-updates.csv")
        for row in rows:
            inventory = normalize_identity(row.get("InventoryIdentity"))
            record = self._record(
                subject_kind=self._subject_kind(inventory),
                subject_identity=inventory,
                related_identity=normalize_identity(row.get("ItemIdentity")),
                evidence_kind="inventory_item",
                source_artifact="inventory-updates.csv",
                source_schema=schema,
            )
            record.add(
                timestamps=(row.get("CapturedUtc"),),
                observation={
                    "handle": integer_value(row.get("Handle")),
                    "slot": integer_value(row.get("Slot")),
                    "count": integer_value(row.get("Count")),
                    "low_id": integer_value(row.get("LowId")),
                    "high_id": integer_value(row.get("HighId")),
                    "quality": integer_value(row.get("Quality")),
                },
            )

        rows, schema = self._read_csv("npc-lifecycle.csv")
        for row in rows:
            primary = normalize_identity(row.get("PrimaryIdentity"))
            related = normalize_identity(row.get("RelatedIdentity"))
            phase = string_value(row.get("Phase")).lower() or "unknown"
            self._metadata(primary, row.get("Name"))
            record = self._record(
                subject_kind=self._subject_kind(primary),
                subject_identity=primary,
                related_identity=related,
                evidence_kind="lifecycle_" + phase.replace(" ", "_"),
                source_artifact="npc-lifecycle.csv",
                source_schema=schema,
            )
            record.add(
                name=row.get("Name"),
                timestamps=(row.get("CapturedUtc"),),
                observation={"message_type": row.get("MessageType")},
            )

    def _parse_respawns(self) -> None:
        rows, schema, source_artifact, projection_pending = self._read_projection_csv(
            "enemy-respawns.csv"
        )
        for row in rows:
            death = normalize_identity(row.get("DeathIdentity"))
            respawn = normalize_identity(row.get("RespawnIdentity"))
            corpse = normalize_identity(row.get("CorpseIdentity"))
            status = string_value(row.get("Status")).lower() or "unknown"
            self._metadata(death, row.get("Name"), row.get("MonsterData"))
            record = self._record(
                subject_kind="enemy",
                subject_identity=death,
                related_identity=respawn,
                evidence_kind="respawn_" + status,
                source_artifact=source_artifact,
                source_schema=schema,
            )
            record.add(
                name=row.get("Name"),
                monster_data=row.get("MonsterData"),
                timestamps=(row.get("DeathUtc"), row.get("RespawnUtc")),
                numeric=row.get("RespawnDelaySeconds"),
                observation={
                    "status": status,
                    "corpse_identity": corpse,
                    "respawn_delay_seconds": float_value(row.get("RespawnDelaySeconds")),
                    "respawn_after_corpse_gone_seconds": float_value(
                        row.get("RespawnAfterCorpseGoneSeconds")
                    ),
                    "position_delta": float_value(row.get("PositionDelta")),
                    "candidate_count": integer_value(row.get("CandidateCount")),
                    "detail": row.get("Detail"),
                },
                issue=(
                    "projection-pending"
                    if projection_pending
                    else (
                        "incomplete-correlation-not-absence"
                        if status == "incomplete"
                        else ""
                    )
                ),
            )
            if status == "incomplete":
                record.issues.add("incomplete-correlation-not-absence")
                record.evidence_status = (
                    "projection-pending-incomplete"
                    if projection_pending
                    else "incomplete-observation"
                )
            elif projection_pending:
                record.evidence_status = "projection-pending-observed"

    def _parse_shops(self) -> None:
        rows, schema = self._read_csv("shop-updates.csv")
        slots_by_terminal: dict[str, set[int]] = defaultdict(set)
        for row in rows:
            terminal = normalize_identity(row.get("TerminalIdentity"))
            slot = integer_value(row.get("Slot"))
            record = self._record(
                subject_kind="vendor_terminal",
                subject_identity=terminal,
                related_identity="",
                evidence_kind="shop_stock",
                source_artifact="shop-updates.csv",
                source_schema=schema,
            )
            issue = ""
            if slot is not None and slot in slots_by_terminal[terminal]:
                issue = "duplicate-shop-slot={0}".format(slot)
                self.issues.add("shop-updates.csv:" + terminal + ":" + issue)
            if slot is not None:
                slots_by_terminal[terminal].add(slot)
            record.add(
                timestamps=(row.get("CapturedUtc"),),
                observation={
                    "slot": slot,
                    "low_id": integer_value(row.get("LowId")),
                    "high_id": integer_value(row.get("HighId")),
                    "quality": integer_value(row.get("Quality")),
                },
                issue=issue,
            )

    def _parse_statics(self) -> None:
        rows, schema = self._read_csv("pf127-door-state.csv")
        for row in rows:
            identity = normalize_identity(row.get("Identity"))
            scope = playfield_scope(row.get("ResourcePlayfieldId"))
            if not scope:
                scope = playfield_scope(row.get("RuntimePlayfieldId"))
            self.scope_index.register(identity, scope)
            record = self._record(
                subject_kind="door",
                subject_identity=identity,
                related_identity="",
                evidence_kind="door_state",
                source_artifact="pf127-door-state.csv",
                source_schema=schema,
                explicit_scope=scope,
            )
            record.add(
                name=row.get("Name"),
                timestamps=(row.get("CapturedUtc"),),
                observation={
                    "revision": integer_value(row.get("Revision")),
                    "position": [
                        float_value(row.get("PositionX")),
                        float_value(row.get("PositionY")),
                        float_value(row.get("PositionZ")),
                    ],
                    "rotation": [
                        float_value(row.get("RotationX")),
                        float_value(row.get("RotationY")),
                        float_value(row.get("RotationZ")),
                        float_value(row.get("RotationW")),
                    ],
                    "room_1": integer_value(row.get("Room1Instance")),
                    "room_2": integer_value(row.get("Room2Instance")),
                    "open": boolean_value(row.get("IsOpen")),
                    "locked": boolean_value(row.get("IsLocked")),
                },
            )

        geometry_path = self.path / "pf127-geometry.json"
        if geometry_path.exists() and geometry_path.stat().st_size > 0:
            geometry = location_inventory.load_json(geometry_path)
            rooms = geometry.get("roomInstances")
            triangles = geometry.get("triangles")
            room_count = len(rooms) if isinstance(rooms, list) else 0
            triangle_count = len(triangles) if isinstance(triangles, list) else 0
            self.artifact_status["pf127-geometry.json"] = "rooms={0};triangles={1}".format(
                room_count, triangle_count
            )
            self.artifact_rows["pf127-geometry.json"] = triangle_count
            scope = playfield_scope(geometry.get("playfieldResource"))
            model = geometry.get("modelIdentity")
            identity = ""
            if isinstance(model, dict):
                identity = identity_from_numeric(model.get("type"), model.get("instance"))
            record = self._record(
                subject_kind="geometry",
                subject_identity=identity,
                related_identity="",
                evidence_kind="collision_geometry",
                source_artifact="pf127-geometry.json",
                source_schema="json:pf127-geometry-v{0}".format(geometry.get("schemaVersion", "unknown")),
                explicit_scope=scope,
            )
            record.add(
                observation={
                    "schema_version": geometry.get("schemaVersion"),
                    "door_link_schema_version": geometry.get("doorLinkSchemaVersion"),
                    "room_count": room_count,
                    "triangle_count": triangle_count,
                    "coordinate_system": geometry.get("coordinateSystem"),
                }
            )
        else:
            self.artifact_status["pf127-geometry.json"] = "missing"

        rows, schema = self._read_csv("pf127-line-of-sight.csv")
        for row in rows:
            target = normalize_identity(row.get("TargetIdentity"))
            scope = playfield_scope(row.get("ResourcePlayfieldId"))
            if not scope:
                scope = playfield_scope(row.get("RuntimePlayfieldId"))
            self.scope_index.register(target, scope)
            self._metadata(target, row.get("TargetName"), row.get("TargetMonsterData"))
            record = self._record(
                subject_kind="enemy",
                subject_identity=target,
                related_identity=normalize_identity(row.get("LocalIdentity")),
                evidence_kind="line_of_sight",
                source_artifact="pf127-line-of-sight.csv",
                source_schema=schema,
                explicit_scope=scope,
            )
            record.add(
                name=row.get("TargetName"),
                monster_data=row.get("TargetMonsterData"),
                timestamps=(row.get("CapturedUtc"),),
                observation={
                    "trigger": row.get("Trigger"),
                    "probe_variant": row.get("ProbeVariant"),
                    "door_revision": integer_value(row.get("DoorStateRevision")),
                    "simple_char_los": boolean_value(row.get("SimpleCharIsInLineOfSight")),
                    "playfield_los": boolean_value(row.get("PlayfieldLineOfSight")),
                    "raycast_hit": boolean_value(row.get("RaycastHit")),
                    "usable": boolean_value(row.get("Usable")),
                    "error": row.get("Error"),
                },
            )

    def _capture_raw_summary(self) -> dict[str, object]:
        rows, _ = self._read_csv("raw-packets.csv")
        preservation = Counter(string_value(row.get("PreservationStatus")) or "blank" for row in rows)
        ordinals = [integer_value(row.get("GlobalOrdinal")) for row in rows]
        ordinal_values = [value for value in ordinals if value is not None]
        if len(ordinal_values) != len(set(ordinal_values)):
            self.issues.add("raw-packets.csv:duplicate-global-ordinal")
        if ordinal_values != sorted(ordinal_values):
            self.issues.add("raw-packets.csv:non-monotonic-global-ordinal")
        callback_health = self.health.get("callbackHealth")
        if not isinstance(callback_health, dict):
            callback_health = self.info.get("callbackHealth")
        if not isinstance(callback_health, dict):
            callback_health = {}
        packet_counts = self.info.get("packetCounts")
        if not isinstance(packet_counts, dict):
            packet_counts = {}
        validation = self.info.get("validation")
        if not isinstance(validation, dict):
            validation = {}
        summary = {
            "validation_status": validation.get("status"),
            "processing_allowed": validation.get("processingAllowed"),
            "recapture_required": validation.get("recaptureRequired"),
            "capture_end_utc": self.info.get("captureEndUtc"),
            "capture_finalized_utc": self.info.get("captureFinalizedUtc"),
            "quiet_period_passed": self.info.get("quietPeriodPassed"),
            "raw_packet_evidence": self.base.get("raw_packet_evidence"),
            "packets_hex_bytes": integer_value(self.base.get("packets_hex_bytes")),
            "raw_packets_bytes": integer_value(self.base.get("raw_packets_bytes")),
            "raw_packet_rows_actual": len(rows),
            "raw_packet_preservation": dict(sorted(preservation.items())),
            "raw_packet_rows_declared": packet_counts.get("rawPacketIndexRows"),
            "raw_packet_write_errors": packet_counts.get("rawPacketWriteErrors"),
            "raw_packet_projection_errors": packet_counts.get("rawPacketProjectionErrors"),
            "decoded_stage_errors": packet_counts.get("decodedN3StageErrors"),
            "callback_errors": callback_health.get("totalErrors"),
            "validation_issues": validation.get("issues"),
        }
        return summary

    def _validate_declared_counts(self) -> None:
        capture_counts = self.info.get("captureCounts")
        if not isinstance(capture_counts, dict):
            return
        validation = self.info.get("validation")
        status = validation.get("status") if isinstance(validation, dict) else ""
        prefix = "stale-declared-count" if status == "running" else "declared-count-mismatch"
        for filename, field_name in DECLARED_COUNT_FIELDS.items():
            declared = integer_value(capture_counts.get(field_name))
            actual = self.artifact_rows.get(filename)
            if declared is None or actual is None or declared == actual:
                continue
            self.issues.add(
                "{0}:{1}:{2}={3}:actual={4}".format(
                    prefix, filename, field_name, declared, actual
                )
            )

    def _evidence_digest(self) -> str:
        artifact_names = sorted(
            set(CSV_SCHEMAS)
            | set(PENDING_PROJECTION_FILES.values())
            | {
                "capture-session.json",
                "capture_info.json",
                "capture-health.json",
                "enemy-dossier.json",
                "events.log",
                "packets.hex.log",
                "pf127-geometry.json",
            }
        )
        digest = hashlib.sha256()
        for name in artifact_names:
            path = self.path / name
            if not path.exists() or not path.is_file():
                continue
            digest.update(name.encode("utf-8"))
            digest.update(b"\0")
            digest.update(file_digest(path).encode("ascii"))
            digest.update(b"\n")
        return digest.hexdigest()

    def _session_record(self, raw_summary: dict[str, object]) -> None:
        record = self._record(
            subject_kind="session",
            subject_identity="",
            related_identity="",
            evidence_kind="session",
            source_artifact="capture metadata",
            source_schema="json:mixed-capture-metadata",
            explicit_scope="subway_exact" if self.classification == "SUBWAY" else "unscoped_mixed",
        )
        record.add(
            timestamps=(
                self.info.get("captureStartUtc"),
                self.info.get("captureEndUtc"),
                self.info.get("captureFinalizedUtc"),
            ),
            observation={
                **raw_summary,
                "artifact_status": dict(sorted(self.artifact_status.items())),
                "artifact_rows": dict(sorted(self.artifact_rows.items())),
                "evidence_digest": self._evidence_digest(),
            },
        )

    def analyze(self) -> list[EvidenceRecord]:
        self._parse_vendors()
        self._parse_dossier()
        self._parse_character_updates()
        self._parse_state_and_stats()
        self._parse_combat()
        self._parse_movement()
        self._parse_corpses_and_loot()
        self._parse_respawns()
        self._parse_shops()
        self._parse_statics()
        raw_summary = self._capture_raw_summary()
        self._validate_declared_counts()
        self._session_record(raw_summary)

        for record in self.records.values():
            metadata = self.identity_metadata.get(record.subject_identity)
            if metadata is not None and record.evidence_kind != "corpse_full_update":
                record.names.update(metadata.names)
                record.monster_data.update(metadata.monster_data)
                record.levels.update(metadata.levels)
            if record.evidence_kind == "session":
                record.issues.update(self.issues)
        return list(self.records.values())

    def raw_completeness(self) -> str:
        validation = self.info.get("validation")
        if not isinstance(validation, dict):
            validation = {}
        status = string_value(validation.get("status")).lower()
        processing_allowed = validation.get("processingAllowed")
        recapture = validation.get("recaptureRequired") is True
        if string_value(self.base.get("raw_packet_evidence")) == "none":
            return "no-raw-sink"
        if recapture or status == "incomplete":
            return "incomplete"
        if status == "running" or self.info.get("captureEndUtc") is None:
            return "metadata-unfinalized"
        if status == "complete" and processing_allowed is True:
            return "validated-complete"
        if status == "complete":
            return "reported-complete"
        return "metadata-unknown"


def evidence_to_row(record: EvidenceRecord, references: dict[str, set[str]]) -> dict[str, object]:
    numeric_items = sorted(
        ((float(value), value, count) for value, count in record.numeric_values.items()),
        key=lambda item: item[0],
    )
    observed: dict[str, object] = {}
    if record.observations:
        observed["observations"] = [
            {"count": count, "value": json.loads(value)}
            for value, count in sorted(record.observations.items())
        ]
    if numeric_items:
        observed["numeric_histogram"] = [
            {"count": count, "value": value}
            for _, value, count in numeric_items
        ]
    first_utc, last_utc = record_timestamp(record.timestamps)
    capture_references = references if record.evidence_kind == "session" else {
        category: set() for category in REFERENCE_CATEGORIES
    }
    return {
        "capture_id": record.capture_id,
        "capture_path": record.capture_path,
        "capture_classification": record.capture_classification,
        "capture_confidence": record.capture_confidence,
        "source_realm": record.source_realm,
        "source_basis": record.source_basis,
        "subject_scope": record.subject_scope,
        "subject_kind": record.subject_kind,
        "subject_identity": record.subject_identity,
        "related_identity": record.related_identity,
        "subject_name": ";".join(sorted(record.names, key=str.lower)),
        "monster_data": ";".join(str(value) for value in sorted(record.monster_data)),
        "levels": ";".join(str(value) for value in sorted(record.levels)),
        "evidence_kind": record.evidence_kind,
        "observation_count": record.observation_count,
        "first_utc": first_utc,
        "last_utc": last_utc,
        "numeric_min": numeric_items[0][1] if numeric_items else "",
        "numeric_max": numeric_items[-1][1] if numeric_items else "",
        "observed_values_json": canonical_json(observed),
        "source_artifact": record.source_artifact,
        "source_schema": record.source_schema,
        "runtime_references": ";".join(sorted(capture_references["runtime"])),
        "test_references": ";".join(sorted(capture_references["tests"])),
        "generator_references": ";".join(sorted(capture_references["generator"])),
        "generated_references": ";".join(sorted(capture_references["generated"])),
        "documentation_references": ";".join(sorted(capture_references["documentation"])),
        "evidence_status": record.evidence_status,
        "issues": ";".join(sorted(record.issues)),
    }


def record_sort_key(row: dict[str, object]) -> tuple[str, ...]:
    return tuple(
        string_value(row[key])
        for key in (
            "capture_id",
            "subject_scope",
            "subject_kind",
            "subject_identity",
            "evidence_kind",
            "related_identity",
            "source_artifact",
        )
    )


def write_csv(path: Path, rows: list[dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0].keys()), lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def markdown_cell(value: object) -> str:
    return string_value(value).replace("|", "\\|").replace("\n", " ")


def count_observations(rows: Iterable[dict[str, object]], prefix: str) -> int:
    return sum(
        int(row["observation_count"])
        for row in rows
        if string_value(row["evidence_kind"]).startswith(prefix)
    )


def unique_subjects(
    rows: Iterable[dict[str, object]],
    kinds: set[str],
    evidence_prefixes: tuple[str, ...] = (),
) -> int:
    identities = {
        string_value(row["subject_identity"])
        for row in rows
        if row["subject_kind"] in kinds
        and string_value(row["subject_identity"])
        and (
            not evidence_prefixes
            or string_value(row["evidence_kind"]).startswith(evidence_prefixes)
        )
    }
    return len(identities)


def write_markdown(
    path: Path,
    rows: list[dict[str, object]],
    analyzers: dict[str, CaptureAnalyzer],
) -> None:
    by_capture: dict[str, list[dict[str, object]]] = defaultdict(list)
    for row in rows:
        by_capture[string_value(row["capture_id"])].append(row)
    realm_counts = Counter(analyzer.realm for analyzer in analyzers.values())
    classification_counts = Counter(analyzer.classification for analyzer in analyzers.values())
    lines = [
        "# AOSharp Subway Capture Content Matrix",
        "",
        "Generated by `Tools/inventory_aosharp_subway_content.py`. The existing location classifier selects captures; this report does not classify location from names or filenames. Official-live evidence, AORebirth-private validation, and unknown-realm evidence remain separate. Private evidence is never presented as an authoritative implementation gap.",
        "",
        "## Summary",
        "",
        "| Metric | Count |",
        "| --- | ---: |",
        "| Selected capture folders | {0} |".format(len(analyzers)),
        "| Subway-only captures | {0} |".format(classification_counts["SUBWAY"]),
        "| Mixed captures | {0} |".format(classification_counts["MIXED"]),
        "| Official-live sessions | {0} |".format(realm_counts["official_live"]),
        "| AORebirth-private sessions | {0} |".format(realm_counts["aorebirth_private"]),
        "| Unknown-realm sessions | {0} |".format(realm_counts["unknown"]),
        "| Content ledger rows | {0} |".format(len(rows)),
        "",
        "## Capture Matrix",
        "",
        "| Capture | Class | Realm | Validation | Raw | Enemies | Combat | Corpses | Loot snapshots/items | Respawn C/A/I | Vendor NPC/VM | Stock | Doors | Static projection | Scope S/E/U | Runtime refs | Action |",
        "| --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | --- | --- | ---: | ---: | --- | --- | ---: | --- |",
    ]
    official_without_runtime: list[str] = []
    unscoped_mixed: list[str] = []
    schema_issues: list[str] = []
    for capture_id in sorted(analyzers):
        analyzer = analyzers[capture_id]
        capture_rows = by_capture[capture_id]
        content_rows = [
            row for row in capture_rows if row["evidence_kind"] != "session"
        ]
        enemy_count = unique_subjects(
            capture_rows,
            {"enemy"},
            ("population", "simple_char", "scfu", "enemy_state"),
        )
        combat_count = unique_subjects(
            capture_rows,
            {"enemy"},
            ("combat_",),
        )
        corpse_count = unique_subjects(capture_rows, {"corpse"}, ("corpse_",))
        loot_count = count_observations(capture_rows, "loot_snapshot")
        inventory_item_count = count_observations(capture_rows, "inventory_item")
        respawn_complete = count_observations(capture_rows, "respawn_complete")
        respawn_ambiguous = count_observations(capture_rows, "respawn_ambiguous")
        respawn_incomplete = count_observations(capture_rows, "respawn_incomplete")
        vendor_npcs = unique_subjects(capture_rows, {"vendor_npc"})
        vendor_terminals = unique_subjects(capture_rows, {"vendor_terminal"})
        stock_count = count_observations(capture_rows, "shop_stock")
        door_count = unique_subjects(capture_rows, {"door"})
        has_geometry = any(row["evidence_kind"] == "collision_geometry" for row in capture_rows)
        static_projection = (
            "geometry+doors" if has_geometry and door_count else (
                "geometry" if has_geometry else ("doors-only" if door_count else "unprojected")
            )
        )
        subway_rows = sum(
            1 for row in content_rows if string_value(row["subject_scope"]).startswith("subway")
        )
        elsewhere_rows = sum(
            1 for row in content_rows if string_value(row["subject_scope"]).startswith("elsewhere")
        )
        unresolved_rows = len(content_rows) - subway_rows - elsewhere_rows
        runtime_ref_count = len(analyzer.references["runtime"])
        validation = ""
        validation_value = analyzer.info.get("validation")
        if isinstance(validation_value, dict):
            validation = string_value(validation_value.get("status"))
        invalid_schema = any(
            status in {"schema-invalid", "read-error"}
            for status in analyzer.artifact_status.values()
        )
        if invalid_schema:
            schema_issues.append(capture_id)
        if analyzer.realm == "aorebirth_private":
            action = "private validation only"
        elif invalid_schema:
            action = "inspect schema adapter"
        elif analyzer.classification == "MIXED" and unresolved_rows:
            action = "scope mixed evidence"
            unscoped_mixed.append(capture_id)
        elif analyzer.realm == "official_live" and runtime_ref_count == 0:
            action = "audit official unreferenced evidence"
            official_without_runtime.append(capture_id)
        elif analyzer.realm == "unknown":
            action = "resolve realm from explicit metadata"
        else:
            action = "runtime reference present"
        lines.append(
            "| {capture} | {classification} | {realm} | {validation} | {raw} | {enemies} | {combat} | {corpses} | {loot} | {respawn} | {vendors} | {stock} | {doors} | {statics} | {scopes} | {runtime_refs} | {action} |".format(
                capture=markdown_cell(capture_id),
                classification=markdown_cell(analyzer.classification),
                realm=markdown_cell(analyzer.realm),
                validation=markdown_cell(validation),
                raw=markdown_cell(analyzer.raw_completeness()),
                enemies=enemy_count,
                combat=combat_count,
                corpses=corpse_count,
                loot="{0}/{1}".format(loot_count, inventory_item_count),
                respawn="{0}/{1}/{2}".format(respawn_complete, respawn_ambiguous, respawn_incomplete),
                vendors="{0}/{1}".format(vendor_npcs, vendor_terminals),
                stock=stock_count,
                doors=door_count,
                statics=static_projection,
                scopes="{0}/{1}/{2}".format(subway_rows, elsewhere_rows, unresolved_rows),
                runtime_refs=runtime_ref_count,
                action=action,
            )
        )
    lines.extend(
        [
            "",
            "## Actionable Existing-Corpus Review",
            "",
            "- Official-live captures without a runtime-source reference: {0}.".format(
                ", ".join(official_without_runtime) if official_without_runtime else "none"
            ),
            "- Mixed captures with unscoped content rows: {0}.".format(
                ", ".join(unscoped_mixed) if unscoped_mixed else "none"
            ),
            "- Captures with an unsupported or unreadable projected schema: {0}.".format(
                ", ".join(schema_issues) if schema_issues else "none"
            ),
            "- General static dynels have no dedicated projected CSV in this corpus. Door state and collision geometry are reported separately; `unprojected` is not evidence that no static exists.",
            "- Missing artifacts and zero-row artifacts are inventory states, not proof that the gameplay event did not occur.",
            "",
        ]
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def validate(
    base_rows: list[dict[str, object]],
    rows: list[dict[str, object]],
    analyzers: dict[str, CaptureAnalyzer],
) -> None:
    selected_ids = {string_value(row["capture_id"]) for row in base_rows}
    if len(selected_ids) != 68:
        raise SystemExit("Expected 68 Subway/mixed captures, found {0}.".format(len(selected_ids)))
    if Counter(string_value(row["classification"]) for row in base_rows) != Counter(
        {"SUBWAY": 37, "MIXED": 31}
    ):
        raise SystemExit("Subway/mixed classification totals drifted.")
    session_counts = Counter(
        string_value(row["capture_id"])
        for row in rows
        if row["evidence_kind"] == "session"
    )
    if set(session_counts) != selected_ids or any(count != 1 for count in session_counts.values()):
        raise SystemExit("Every selected capture must have exactly one session sentinel row.")
    if set(analyzers) != selected_ids:
        raise SystemExit("Analyzer coverage does not match selected capture coverage.")
    keys = [record_sort_key(row) for row in rows]
    if len(keys) != len(set(keys)):
        raise SystemExit("Duplicate content-ledger composite keys were generated.")
    if keys != sorted(keys):
        raise SystemExit("Content ledger rows are not deterministically sorted.")
    for row in rows:
        if row["capture_classification"] == "MIXED" and row["subject_scope"] == "subway_session":
            raise SystemExit("Mixed capture content received blanket Subway scope.")
        if row["source_realm"] not in {"official_live", "aorebirth_private", "unknown"}:
            raise SystemExit("Unknown source realm value in content ledger.")


def main() -> int:
    args = parse_args()
    repo_root = Path(args.repo_root).resolve()
    documented, indexed = location_inventory.collect_repository_references(repo_root)
    capture_paths = location_inventory.discover_capture_directories(repo_root)
    all_rows = [
        location_inventory.inspect_capture(repo_root, path, documented, indexed)
        for path in capture_paths
    ]
    location_inventory.validate_reviewed_corpus(all_rows)
    selected = [
        row for row in all_rows if row["classification"] in SELECTED_CLASSIFICATIONS
    ]
    capture_ids = {string_value(row["capture_id"]) for row in selected}
    output_csv = (repo_root / args.output_csv).resolve()
    output_md = (repo_root / args.output_md).resolve()
    excluded_outputs = {
        output_csv.relative_to(repo_root).as_posix(),
        output_md.relative_to(repo_root).as_posix(),
        OUTPUT_CSV,
        OUTPUT_MD,
        "docs/generated/aosharp_capture_inventory.csv",
        "docs/generated/aosharp_capture_inventory.md",
    }
    references = collect_reference_categories(repo_root, capture_ids, excluded_outputs)
    analyzers: dict[str, CaptureAnalyzer] = {}
    evidence_records: list[EvidenceRecord] = []
    for base_row in selected:
        capture_id = string_value(base_row["capture_id"])
        analyzer = CaptureAnalyzer(repo_root, base_row, references[capture_id])
        analyzers[capture_id] = analyzer
        evidence_records.extend(analyzer.analyze())
    rows = [
        evidence_to_row(record, references[record.capture_id])
        for record in evidence_records
    ]
    rows.sort(key=record_sort_key)
    validate(selected, rows, analyzers)
    write_csv(output_csv, rows)
    write_markdown(output_md, rows, analyzers)
    print(
        "captures={0} ledger_rows={1} official={2} private={3} unknown={4}".format(
            len(analyzers),
            len(rows),
            sum(1 for analyzer in analyzers.values() if analyzer.realm == "official_live"),
            sum(1 for analyzer in analyzers.values() if analyzer.realm == "aorebirth_private"),
            sum(1 for analyzer in analyzers.values() if analyzer.realm == "unknown"),
        )
    )
    print("csv=" + str(output_csv))
    print("markdown=" + str(output_md))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
