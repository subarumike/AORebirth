#!/usr/bin/env python3
"""Build the fixed active capture-backed NPC combat coverage inventory.

The content population is parsed from the checked-in C# definitions rather
than copied into a second hand-maintained list.  Combat classification is then
resolved against ``capture_backed_npc_combat_inventory.json`` using the same
two safe lookup modes as the generated runtime catalog:

* an exact runtime source-identity/profile-selector hint, with non-equipped
  attack range resolved independently from captured SAW templates and ItemDb; or
* a capture-proven unique semantic fallback for source-unbound actors.

The generator intentionally fails if any content shape is no longer understood
or if the fixed initial population does not reconcile to 1,534 actors.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, Iterable, List, Mapping, Optional, Sequence, Tuple

if hasattr(sys, "set_int_max_str_digits"):
    sys.set_int_max_str_digits(0)


EXPECTED_INITIAL_ACTORS = 1534

SURFACE_EXPECTATIONS: Sequence[Tuple[str, int]] = (
    ("subway-ordinary", 322),
    ("subway-initial-encounters", 3),
    ("temple-ordinary", 167),
    ("temple-named-encounters", 12),
    ("temple-reanimated-corpse-adds", 2),
    ("nascence-core-hecklers", 40),
    ("nascence-life", 837),
    ("arete-family", 96),
    ("arete-additional-captured-actors", 17),
    ("subway-merchants", 6),
    ("rome-blue-city", 22),
    ("thrak-omni-garden", 10),
)

RUNTIME_PREPARE_ROOT = "AORebirth/Server/ZoneEngine/Core"
RUNTIME_PREPARE_PATTERN = re.compile(
    r"\bCapturedEnemyCombatRuntime\s*\.\s*Prepare(?:AndRequireCombatReady)?\s*\("
)
SCRIPTED_HOSTILE_SOURCE = (
    "AORebirth/Server/ZoneEngine/Core/Thrak/Quests/"
    "ThrakGardenKeySilvertailTransform.cs"
)
SCRIPTED_HOSTILE_CAPTURE_ID = "20260718-185306"

PF127_ORDINARY_PROFILE_RESOLUTION_MODE = (
    "production-owned-exact-pf127-ordinary-profile-resolver"
)
PF127_ORDINARY_PROFILE_SELECTOR_PREFIX = "subway.ordinary."
PF127_EXACT_SUPPORTED_PROFILE_SELECTORS = (
    "subway.supported.17720",
    "subway.supported.203734",
)
PF127_ORDINARY_PROFILE_OWNER_MARKERS: Mapping[str, Tuple[str, ...]] = {
    "AORebirth/Server/ZoneEngine/Core/Playfields/OrdinaryEnemyRuntimeService.cs": (
        "CapturedEnemyCombatContract combatContract = ResolveCombatContractForSpawn(",
        "CapturedSubwayRetaliationEligibilityResolver.TryResolveExact(",
        "CapturedEnemyCombatRuntime.Prepare(",
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/OrdinaryEnemyCatalog.cs": (
        "private static void BuildCapturedOrdinaryRows(",
        "CapturedSubwayCombatCatalog.ForOrdinary(",
        "sourceVariantContractResolver",
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedEnemyCombatContract.cs": (
        "internal static CapturedEnemyCombatContract ForOrdinary(",
        "ForOrdinarySelectedAtomicGeneration(",
        "internal CapturedEnemyCombatContract WithCaptureProvenRetaliationEligibility(",
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedSubwayRetaliationEligibilityResolver.cs": (
        "internal static class CapturedSubwayRetaliationEligibilityResolver",
        "private static readonly Dictionary<int, CapturedSubwayRetaliationBinding> Bindings",
        "CapturedEnemyCombatProfileCatalog.TryResolve(",
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedEnemyCombatProfileCatalog.cs": (
        "TryResolveProductionOwnedNaturalAttackProfile(",
        "TryResolveCaptureProvenEquippedWeaponArchetype(",
    ),
}

PF1931_PROFILE_RESOLUTION_MODE = (
    "production-owned-exact-pf1931-capture-contract"
)
PF1931_DEATHLESS_PROFILE_SELECTOR = (
    "totw.ordinary.deathless-legionnaire.42981"
)
PF1931_PROFILE_OWNER_MARKERS: Mapping[str, Tuple[str, ...]] = {
    "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedTempleOfThreeWindsLootDefinitions.cs": (
        "DefenderProfileKey = \"totw.647.boss.defender-of-the-three\"",
        "AzturProfileKey = \"totw.1931.boss.aztur-the-immortal\"",
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedTempleOfThreeWindsContentProvider.cs": (
        "BuildDeathlessLegionnaireSpawns()",
        "DeathlessLegionnaireProfileKey",
        "OrdinaryEnemyDamageSource.WeaponRoll",
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedTempleOfThreeWindsCombatCatalog.cs": (
        "internal static CapturedEnemyCombatContract DefenderOfTheThree()",
        "internal static CapturedEnemyCombatContract UkleshTheFrozen()",
        "internal static CapturedEnemyCombatContract AzturTheImmortal()",
        "internal static CapturedEnemyCombatContract ReanimatedCorpse(int captureSourceIdentity)",
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedTempleOfThreeWindsEncounterRuntimeService.cs": (
        "CapturedTempleOfThreeWindsCombatCatalog.DefenderOfTheThree()",
        "CapturedTempleOfThreeWindsCombatCatalog.AzturTheImmortal()",
        "CapturedTempleOfThreeWindsCombatCatalog.ReanimatedCorpse(",
        "CapturedEnemyCombatRuntime.Prepare(",
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedEnemyCombatProfileCatalog.cs": (
        "TryResolveCaptureProvenEquippedWeaponArchetype(",
        "TryResolveProductionOwnedNaturalAttackProfile(",
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedEnemyCombatContract.cs": (
        "internal static CapturedEnemyCombatContract ForOrdinary(",
        "ForOrdinarySelectedAtomicGeneration(",
        "CapturedEnemyCombatProfileCatalog.TryResolve(",
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/OrdinaryEnemyRuntimeService.cs": (
        "ResolveCombatContractForSpawn(",
        "CapturedEnemyCombatRuntime.Prepare(",
    ),
}

# Every production call site must be assigned to either the fixed-denominator
# coverage or an explicit non-denominator audit family.  The expected call
# count makes a second call in an already-covered file fail closed too.
RUNTIME_PREPARE_AUDIT_REFERENCES: Mapping[
    str, Tuple[int, str, Tuple[str, ...]]
] = {
    SCRIPTED_HOSTILE_SOURCE: (1, "non-denominator-audit", ("scripted-hostiles",)),
    "AORebirth/Server/ZoneEngine/Core/Missions/MissionInstanceMobCombat.cs": (
        1,
        "non-denominator-audit",
        ("dynamic-mission-mobs",),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/AlexAreaMobRuntime.cs": (
        1,
        "fixed-denominator-surfaces",
        ("arete-family",),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/AreteFinishCaptureMobRuntime.cs": (
        1,
        "fixed-denominator-surfaces",
        ("arete-additional-captured-actors",),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/AreteIccPeacekeeperPatrolRuntime.cs": (
        1,
        "fixed-denominator-surfaces",
        ("arete-additional-captured-actors",),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/AreteRoboticGuardDogRuntime.cs": (
        1,
        "fixed-denominator-surfaces",
        ("arete-additional-captured-actors",),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedAreteRobotSpawnOrchestrator.cs": (
        1,
        "fixed-denominator-surfaces",
        ("arete-family",),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedSubwayEncounterRuntimeService.cs": (
        1,
        "fixed-denominator-surfaces",
        ("subway-initial-encounters",),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedSubwayVendorRuntimeService.cs": (
        1,
        "fixed-denominator-surfaces",
        ("subway-merchants",),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedTempleOfThreeWindsEncounterRuntimeService.cs": (
        1,
        "fixed-denominator-surfaces",
        ("temple-named-encounters", "temple-reanimated-corpse-adds"),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/ElysiumEastMobRuntime.cs": (
        1,
        "non-denominator-audit",
        ("elysium-east-captured-population",),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/JunkyardCleaningRobotRuntime.cs": (
        1,
        "non-denominator-audit",
        ("cleaning-robots",),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/LoreleiOasisMobRuntime.cs": (
        2,
        "fixed-denominator-surfaces",
        ("arete-family",),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/MarcusPadAmbientCombat.cs": (
        2,
        "fixed-denominator-surfaces",
        ("arete-family",),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/NascenceCoreHecklerSpawnOrchestrator.cs": (
        1,
        "fixed-denominator-surfaces",
        ("nascence-core-hecklers",),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/NascenceLifeSpawn.cs": (
        1,
        "fixed-denominator-surfaces",
        ("nascence-life",),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/OrdinaryEnemyRuntimeService.cs": (
        1,
        "fixed-denominator-surfaces",
        ("subway-ordinary", "temple-ordinary"),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/RomeBlueCitySpawn.cs": (
        1,
        "fixed-denominator-surfaces",
        ("rome-blue-city",),
    ),
    "AORebirth/Server/ZoneEngine/Core/Playfields/ThrakOmniGardenSpawn.cs": (
        1,
        "fixed-denominator-surfaces",
        ("thrak-omni-garden",),
    ),
}

RUNTIME_PREPARE_ACTIVE_EVIDENCE_REFERENCES: Mapping[
    str, Tuple[int, str, Tuple[str, ...]]
] = {
    "AORebirth/Server/ZoneEngine/Core/Playfields/IccShuttleportSpawn.cs": (
        1,
        "active-evidence",
        ("icc-shuttleport-entry-governance",),
    ),
}

ICC_SHUTTLEPORT_SOURCE = (
    "AORebirth/Server/ZoneEngine/Core/Playfields/IccShuttleportSpawn.cs"
)
ICC_SHUTTLEPORT_ENTRY_GOVERNANCE: Tuple[Tuple[str, str], ...] = (
    ("Island Reet", "ACCEPTED_RUNTIME_CONTENT"),
    ("Clan Equipment Vendor", "ACTIVE_EVIDENCE"),
    ("Clan Recruiter", "ACTIVE_EVIDENCE"),
    ("Adri Afeli", "ACTIVE_EVIDENCE"),
    ("Omni-Trans Equipment Vendor", "ACTIVE_EVIDENCE"),
    ("Vendor Antonio Stacklund", "ACTIVE_EVIDENCE"),
    ("Omni-Tek Recruitment Officer", "ACTIVE_EVIDENCE"),
    ("Neutral Observer", "ACTIVE_EVIDENCE"),
    ("ICC Shuttle Guard", "ACTIVE_EVIDENCE"),
    ("ICC Shuttle Guard", "ACTIVE_EVIDENCE"),
    ("Omni Unicorn Squadleader Fixx", "ACTIVE_EVIDENCE"),
    ("Clan Field Surgeon Elsa Oosta", "ACTIVE_EVIDENCE"),
    ("ICC Shuttle Guard", "ACTIVE_EVIDENCE"),
    ("ICC Shuttle Guard", "ACTIVE_EVIDENCE"),
    ("ICC Shuttle Guard", "ACTIVE_EVIDENCE"),
    ("ICC Shuttle Guard", "ACTIVE_EVIDENCE"),
    ("ICC Shuttle Guard", "ACTIVE_EVIDENCE"),
    ("ICC Shuttle Guard", "ACTIVE_EVIDENCE"),
    ("ICC Shuttle Guard", "ACTIVE_EVIDENCE"),
    ("ICC Shuttle Guard", "ACTIVE_EVIDENCE"),
    ("Brandon Thorn", "ACTIVE_EVIDENCE"),
    ("ICC Bio-Inspector", "ACTIVE_EVIDENCE"),
    ("Manager Travis Molen", "ACTIVE_EVIDENCE"),
    ("ICC Shuttle Guard", "ACTIVE_EVIDENCE"),
    ("ICC Shuttle Guard", "ACTIVE_EVIDENCE"),
)


class CoverageError(RuntimeError):
    """Raised when a checked-in content shape cannot be reconciled safely."""


@dataclass
class ActorDefinition:
    surface: str
    resource: int
    name: str
    monster_data: int
    levels: Tuple[int, ...]
    actor_count: int = 1
    configured_source_identity: Optional[int] = None
    runtime_source_identity_hint: Optional[int] = None
    runtime_profile_selector: str = ""
    runtime_attack_range_micrometers: int = 0
    runtime_special_attack_weapon_unknown5: Optional[int] = None
    content_sources: Tuple[str, ...] = field(default_factory=tuple)
    content_evidence_capture_ids: Tuple[str, ...] = field(default_factory=tuple)
    notes: Tuple[str, ...] = field(default_factory=tuple)

    def merge_key(self) -> Tuple[Any, ...]:
        return (
            self.surface,
            self.resource,
            self.name,
            self.monster_data,
            self.levels,
            self.configured_source_identity,
            self.runtime_source_identity_hint,
            self.runtime_profile_selector,
            self.runtime_attack_range_micrometers,
            self.runtime_special_attack_weapon_unknown5,
            self.notes,
        )


def repo_path(repo_root: Path, relative: str) -> Path:
    path = repo_root / Path(relative)
    if not path.is_file():
        raise CoverageError(f"required input is missing: {relative}")
    return path


def read_source(repo_root: Path, relative: str) -> str:
    return repo_path(repo_root, relative).read_text(encoding="utf-8-sig")


def discover_pf127_ordinary_profile_owners(
    repo_root: Path,
) -> List[Dict[str, Any]]:
    owners: List[Dict[str, Any]] = []
    for relative, required_markers in sorted(
        PF127_ORDINARY_PROFILE_OWNER_MARKERS.items()
    ):
        source = read_source(repo_root, relative)
        missing = [marker for marker in required_markers if marker not in source]
        if missing:
            raise CoverageError(
                "PF127 ordinary profile resolver ownership changed in "
                f"{relative}: missing " + ", ".join(repr(marker) for marker in missing)
            )
        owners.append(
            {
                "path": relative,
                "requiredMarkers": list(required_markers),
            }
        )
    return owners


def discover_pf1931_profile_owners(
    repo_root: Path,
) -> List[Dict[str, Any]]:
    owners: List[Dict[str, Any]] = []
    for relative, required_markers in sorted(PF1931_PROFILE_OWNER_MARKERS.items()):
        source = read_source(repo_root, relative)
        missing = [marker for marker in required_markers if marker not in source]
        if missing:
            raise CoverageError(
                "PF1931 capture-contract ownership changed in "
                f"{relative}: missing " + ", ".join(repr(marker) for marker in missing)
            )
        owners.append(
            {
                "path": relative,
                "requiredMarkers": list(required_markers),
            }
        )
    return owners


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_utf8_text_lf(path: Path) -> str:
    """Hash UTF-8 source text after BOM removal and newline normalization."""
    source = path.read_text(encoding="utf-8-sig")
    normalized = source.replace("\r\n", "\n").replace("\r", "\n")
    return hashlib.sha256(normalized.encode("utf-8")).hexdigest()


def discover_runtime_prepare_entry_points(repo_root: Path) -> List[Dict[str, Any]]:
    production_root = repo_root / Path(RUNTIME_PREPARE_ROOT)
    if not production_root.is_dir():
        raise CoverageError(
            f"production runtime root is missing: {RUNTIME_PREPARE_ROOT}"
        )

    discovered: List[Dict[str, Any]] = []
    for path in sorted(production_root.rglob("*.cs")):
        source = path.read_text(encoding="utf-8-sig")
        matches = list(RUNTIME_PREPARE_PATTERN.finditer(source))
        if not matches:
            continue
        relative = str(path.relative_to(repo_root)).replace("\\", "/")
        discovered.append(
            {
                "path": relative,
                "prepareCallCount": len(matches),
                "prepareCallSourceLines": [
                    source.count("\n", 0, match.start()) + 1 for match in matches
                ],
            }
        )

    discovered_paths = {row["path"] for row in discovered}
    audit_references = {
        **RUNTIME_PREPARE_AUDIT_REFERENCES,
        **RUNTIME_PREPARE_ACTIVE_EVIDENCE_REFERENCES,
    }
    expected_paths = set(audit_references)
    unaudited = sorted(discovered_paths - expected_paths)
    stale = sorted(expected_paths - discovered_paths)
    if unaudited or stale:
        details: List[str] = []
        if unaudited:
            details.append("unaudited=" + ", ".join(unaudited))
        if stale:
            details.append("no-longer-present=" + ", ".join(stale))
        raise CoverageError(
            "production CapturedEnemyCombatRuntime.Prepare entry-point audit is stale: "
            + "; ".join(details)
        )

    for row in discovered:
        expected_count, audit_kind, audit_reference_values = audit_references[
            row["path"]
        ]
        if row["prepareCallCount"] != expected_count:
            raise CoverageError(
                "production CapturedEnemyCombatRuntime.Prepare call count changed for "
                f"{row['path']}: expected {expected_count}, found "
                f"{row['prepareCallCount']}"
            )
        row["auditKind"] = audit_kind
        row["auditReferences"] = list(audit_reference_values)
        row["governanceState"] = (
            "ACTIVE_EVIDENCE"
            if row["path"] in RUNTIME_PREPARE_ACTIVE_EVIDENCE_REFERENCES
            else "ACCEPTED_COVERAGE"
        )
    return discovered


def discover_icc_shuttleport_entry_governance(
    repo_root: Path,
) -> Dict[str, Any]:
    source = read_source(repo_root, ICC_SHUTTLEPORT_SOURCE)
    body = extract_array_initializer(source, "ShuttleportNpc[] Npcs")
    definitions = parse_object_initializers(body, "ShuttleportNpc")
    actual_names = [
        parse_csharp_string(definition["Name"])
        for definition in definitions
        if "Name" in definition
    ]
    expected_names = [name for name, _ in ICC_SHUTTLEPORT_ENTRY_GOVERNANCE]
    if actual_names != expected_names:
        raise CoverageError(
            "ICC Shuttleport entry governance is stale: expected ordered entries "
            f"{len(expected_names)}, found {len(actual_names)}"
        )
    if not definitions or definitions[0].get(
        "CombatContractFactory"
    ) != "IccShuttleportBasicCombatCatalog.IslandReet":
        raise CoverageError(
            "ICC Shuttleport accepted Island Reet entry is not bound to its "
            "capture-backed combat catalog"
        )
    if any(
        definition.get("CombatContractFactory")
        for definition in definitions[1:]
    ):
        raise CoverageError(
            "ICC Shuttleport active-evidence entries must not acquire an "
            "accepted combat contract"
        )

    entries = [
        {
            "ordinal": ordinal,
            "name": name,
            "state": state,
            "coverageKey": (
                "icc-shuttleport-island-reet-basic-combat"
                if state == "ACCEPTED_RUNTIME_CONTENT"
                else None
            ),
        }
        for ordinal, (name, state) in enumerate(ICC_SHUTTLEPORT_ENTRY_GOVERNANCE)
    ]
    return {
        "source": ICC_SHUTTLEPORT_SOURCE,
        "playfield": 4582,
        "entries": entries,
        "acceptedEntries": sum(
            entry["state"] == "ACCEPTED_RUNTIME_CONTENT" for entry in entries
        ),
        "activeEvidenceEntries": sum(
            entry["state"] == "ACTIVE_EVIDENCE" for entry in entries
        ),
        "blockedUnauditedEntries": 0,
    }


def _scan_balanced(text: str, opening_index: int, opening: str, closing: str) -> int:
    if opening_index >= len(text) or text[opening_index] != opening:
        raise CoverageError(f"balanced scan did not start on {opening!r}")
    depth = 0
    quote: Optional[str] = None
    verbatim = False
    escaped = False
    line_comment = False
    block_comment = False
    index = opening_index
    while index < len(text):
        ch = text[index]
        nxt = text[index + 1] if index + 1 < len(text) else ""
        if line_comment:
            if ch in "\r\n":
                line_comment = False
            index += 1
            continue
        if block_comment:
            if ch == "*" and nxt == "/":
                block_comment = False
                index += 2
            else:
                index += 1
            continue
        if quote is not None:
            if quote == '"' and verbatim:
                if ch == '"' and nxt == '"':
                    index += 2
                    continue
                if ch == '"':
                    quote = None
                    verbatim = False
                index += 1
                continue
            if escaped:
                escaped = False
                index += 1
                continue
            if ch == "\\":
                escaped = True
            elif ch == quote:
                quote = None
            index += 1
            continue
        if ch == "/" and nxt == "/":
            line_comment = True
            index += 2
            continue
        if ch == "/" and nxt == "*":
            block_comment = True
            index += 2
            continue
        if ch == "@" and nxt == '"':
            quote = '"'
            verbatim = True
            index += 2
            continue
        if ch in ('"', "'"):
            quote = ch
            index += 1
            continue
        if ch == opening:
            depth += 1
        elif ch == closing:
            depth -= 1
            if depth == 0:
                return index
        index += 1
    raise CoverageError(f"unterminated balanced {opening}{closing} block")


def extract_array_initializer(text: str, declaration_marker: str) -> str:
    matches = [match.start() for match in re.finditer(re.escape(declaration_marker), text)]
    if len(matches) != 1:
        raise CoverageError(
            f"expected one declaration marker {declaration_marker!r}, found {len(matches)}"
        )
    equals = text.find("=", matches[0] + len(declaration_marker))
    if equals < 0:
        raise CoverageError(f"missing '=' after {declaration_marker!r}")
    opening = text.find("{", equals)
    if opening < 0:
        raise CoverageError(f"missing array initializer after {declaration_marker!r}")
    closing = _scan_balanced(text, opening, "{", "}")
    return text[opening + 1 : closing]


def extract_method_body(text: str, method_marker: str) -> str:
    matches = [match.start() for match in re.finditer(re.escape(method_marker), text)]
    if len(matches) != 1:
        raise CoverageError(
            f"expected one method marker {method_marker!r}, found {len(matches)}"
        )
    opening = text.find("{", matches[0] + len(method_marker))
    if opening < 0:
        raise CoverageError(f"missing method body after {method_marker!r}")
    closing = _scan_balanced(text, opening, "{", "}")
    return text[opening + 1 : closing]


def extract_calls(text: str, callable_name: str, require_new: bool = False) -> List[str]:
    prefix = r"\bnew\s+" if require_new else r"(?<![\w.])"
    pattern = re.compile(prefix + re.escape(callable_name) + r"\s*\(")
    calls: List[str] = []
    for match in pattern.finditer(text):
        opening = text.find("(", match.start(), match.end())
        closing = _scan_balanced(text, opening, "(", ")")
        calls.append(text[opening + 1 : closing])
    return calls


def split_top_level(text: str, delimiter: str = ",") -> List[str]:
    parts: List[str] = []
    start = 0
    stack: List[str] = []
    pairs = {")": "(", "]": "[", "}": "{"}
    quote: Optional[str] = None
    verbatim = False
    escaped = False
    line_comment = False
    block_comment = False
    index = 0
    while index < len(text):
        ch = text[index]
        nxt = text[index + 1] if index + 1 < len(text) else ""
        if line_comment:
            if ch in "\r\n":
                line_comment = False
            index += 1
            continue
        if block_comment:
            if ch == "*" and nxt == "/":
                block_comment = False
                index += 2
            else:
                index += 1
            continue
        if quote is not None:
            if quote == '"' and verbatim:
                if ch == '"' and nxt == '"':
                    index += 2
                    continue
                if ch == '"':
                    quote = None
                    verbatim = False
                index += 1
                continue
            if escaped:
                escaped = False
            elif ch == "\\":
                escaped = True
            elif ch == quote:
                quote = None
            index += 1
            continue
        if ch == "/" and nxt == "/":
            line_comment = True
            index += 2
            continue
        if ch == "/" and nxt == "*":
            block_comment = True
            index += 2
            continue
        if ch == "@" and nxt == '"':
            quote = '"'
            verbatim = True
            index += 2
            continue
        if ch in ('"', "'"):
            quote = ch
        elif ch in "([{":
            stack.append(ch)
        elif ch in ")]}":
            if not stack or stack[-1] != pairs[ch]:
                raise CoverageError("unbalanced expression while splitting arguments")
            stack.pop()
        elif ch == delimiter and not stack:
            part = text[start:index].strip()
            if part:
                parts.append(part)
            start = index + 1
        index += 1
    tail = text[start:].strip()
    if tail:
        parts.append(tail)
    if stack or quote is not None or block_comment:
        raise CoverageError("unterminated expression while splitting arguments")
    return parts


def parse_csharp_string(expression: str, constants: Mapping[str, Any] = {}) -> str:
    value = expression.strip()
    if value in constants and isinstance(constants[value], str):
        return str(constants[value])
    symbol = value.rsplit(".", 1)[-1]
    if symbol in constants and isinstance(constants[symbol], str):
        return str(constants[symbol])
    matches = re.findall(r'@?"((?:""|\\.|[^"\\])*)"', value)
    if matches:
        decoded: List[str] = []
        for item in matches:
            if value.lstrip().startswith('@"'):
                decoded.append(item.replace('""', '"'))
            else:
                decoded.append(bytes(item, "utf-8").decode("unicode_escape"))
        return "".join(decoded)
    raise CoverageError(f"cannot resolve C# string expression: {expression.strip()}")


def parse_csharp_int(expression: str, constants: Mapping[str, Any] = {}) -> int:
    value = expression.strip()
    if value in constants and isinstance(constants[value], int):
        return int(constants[value])
    symbol = value.rsplit(".", 1)[-1]
    if symbol in constants and isinstance(constants[symbol], int):
        return int(constants[symbol])
    hex_match = re.search(r"0x([0-9A-Fa-f]+)", value)
    if hex_match:
        return int(hex_match.group(1), 16)
    decimal_match = re.fullmatch(r"\s*([+-]?\d+)(?:[uUlL]+)?\s*", value)
    if decimal_match:
        return int(decimal_match.group(1), 10)
    raise CoverageError(f"cannot resolve C# integer expression: {expression.strip()}")


def extract_constants(
    text: str,
    initial: Optional[Mapping[str, Any]] = None,
) -> Dict[str, Any]:
    result: Dict[str, Any] = dict(initial or {})
    pattern = re.compile(
        r"\bconst\s+(?:int|string)\s+(\w+)\s*=\s*(.*?);",
        re.DOTALL,
    )
    pending = list(pattern.finditer(text))
    for _ in range(3):
        next_pending = []
        for match in pending:
            name, expression = match.group(1), match.group(2).strip()
            try:
                if expression.startswith('"') or expression.startswith('@"'):
                    result[name] = parse_csharp_string(expression, result)
                else:
                    result[name] = parse_csharp_int(expression, result)
            except CoverageError:
                next_pending.append(match)
        pending = next_pending
    return result


def parse_object_initializers(array_body: str, type_name: str) -> List[Dict[str, str]]:
    pattern = re.compile(r"\bnew\s+" + re.escape(type_name) + r"\s*\{")
    rows: List[Dict[str, str]] = []
    for match in pattern.finditer(array_body):
        opening = array_body.find("{", match.start(), match.end())
        closing = _scan_balanced(array_body, opening, "{", "}")
        assignments: Dict[str, str] = {}
        for item in split_top_level(array_body[opening + 1 : closing]):
            item = strip_leading_csharp_comments(item)
            if "=" not in item:
                continue
            key, value = item.split("=", 1)
            key = key.strip()
            if not re.fullmatch(r"[A-Za-z_]\w*", key):
                continue
            assignments[key] = value.strip()
        rows.append(assignments)
    return rows


def strip_leading_csharp_comments(text: str) -> str:
    index = 0
    while True:
        while index < len(text) and text[index].isspace():
            index += 1
        if text.startswith("//", index):
            index += 2
            while index < len(text) and text[index] not in "\r\n":
                index += 1
            continue
        if text.startswith("/*", index):
            closing = text.find("*/", index + 2)
            if closing < 0:
                raise CoverageError("unterminated leading block comment")
            index = closing + 2
            continue
        return text[index:]


def format_identity(value: Optional[int]) -> Optional[str]:
    if value is None:
        return None
    return f"0x{value & 0xFFFFFFFF:08X}"


def make_actor(
    surface: str,
    resource: int,
    name: str,
    monster_data: int,
    level: int | Sequence[int],
    source: str,
    configured_source_identity: Optional[int] = None,
    runtime_source_identity_hint: Optional[int] = None,
    runtime_profile_selector: str = "",
    runtime_attack_range_micrometers: int = 0,
    runtime_special_attack_weapon_unknown5: Optional[int] = None,
    evidence_capture_ids: Iterable[str] = (),
    notes: Iterable[str] = (),
) -> ActorDefinition:
    levels = (level,) if isinstance(level, int) else tuple(sorted(set(level)))
    if not name or not levels:
        raise CoverageError(f"invalid actor definition from {source}")
    return ActorDefinition(
        surface=surface,
        resource=resource,
        name=name,
        monster_data=monster_data,
        levels=levels,
        configured_source_identity=configured_source_identity,
        runtime_source_identity_hint=runtime_source_identity_hint,
        runtime_profile_selector=runtime_profile_selector,
        runtime_attack_range_micrometers=runtime_attack_range_micrometers,
        runtime_special_attack_weapon_unknown5=runtime_special_attack_weapon_unknown5,
        content_sources=(source,),
        content_evidence_capture_ids=tuple(sorted(set(evidence_capture_ids))),
        notes=tuple(notes),
    )


def parse_subway_ordinary(repo_root: Path) -> List[ActorDefinition]:
    surface = "subway-ordinary"
    resource = 127
    supported_path = "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedSubwayContentProvider.cs"
    ordinary_path = "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedSubwayOrdinaryContentProvider.cs"
    supported = read_source(repo_root, supported_path)
    ordinary = read_source(repo_root, ordinary_path)

    helper_profiles = {
        "FilthFlea": ("Filth Flea", 17657),
        "DiscardedPet": ("Discarded Pet", 17720),
        "DisobedientBot": ("Disobedient Bot", 17649),
        "Mugger": ("Mugger", 203734),
        "Thief": ("Thief", 26092),
        "ViolentVagabond": ("Violent Vagabond", 203733),
    }
    supported_body = extract_array_initializer(
        supported, "CapturedSubwaySpawnDefinition[] SpawnDefinitions"
    )
    actors: List[ActorDefinition] = []
    for helper, (name, monster_data) in helper_profiles.items():
        calls = extract_calls(supported_body, helper)
        for call in calls:
            args = split_top_level(call)
            if len(args) < 2:
                raise CoverageError(f"short {helper} spawn call in {supported_path}")
            source_identity = parse_csharp_int(args[0])
            level = parse_csharp_int(args[1])
            actors.append(
                make_actor(
                    surface,
                    resource,
                    name,
                    monster_data,
                    level,
                    supported_path,
                    configured_source_identity=source_identity,
                    runtime_source_identity_hint=source_identity,
                    runtime_profile_selector=f"subway.supported.{monster_data}",
                )
            )

    archetype_body = extract_array_initializer(
        ordinary, "CapturedSubwayOrdinaryArchetypeDefinition[] Archetypes"
    )
    archetypes: Dict[str, Tuple[str, int]] = {}
    for call in extract_calls(archetype_body, "CapturedSubwayOrdinaryArchetypeDefinition", True):
        args = split_top_level(call)
        if len(args) < 4:
            raise CoverageError(f"short archetype call in {ordinary_path}")
        key = parse_csharp_string(args[0])
        if key in archetypes:
            raise CoverageError(f"duplicate Subway ordinary archetype key: {key}")
        archetypes[key] = (parse_csharp_string(args[2]), parse_csharp_int(args[3]))

    spawn_body = extract_array_initializer(
        ordinary, "CapturedSubwayOrdinarySpawnDefinition[] Spawns"
    )
    spawn_calls = extract_calls(spawn_body, "CapturedSubwayOrdinarySpawnDefinition", True)
    for call in spawn_calls:
        args = split_top_level(call)
        if len(args) < 3:
            raise CoverageError(f"short ordinary spawn call in {ordinary_path}")
        source_identity = parse_csharp_int(args[0])
        archetype_key = parse_csharp_string(args[1])
        if archetype_key not in archetypes:
            raise CoverageError(f"unknown Subway archetype key in spawn: {archetype_key}")
        name, monster_data = archetypes[archetype_key]
        actors.append(
            make_actor(
                surface,
                resource,
                name,
                monster_data,
                parse_csharp_int(args[2]),
                ordinary_path,
                configured_source_identity=source_identity,
                runtime_source_identity_hint=source_identity,
                runtime_profile_selector=f"subway.ordinary.{archetype_key}",
            )
        )
    if len(actors) != 322:
        raise CoverageError(
            f"Subway ordinary parser reconciled {len(actors)} actors instead of 322 "
            f"({len(actors) - len(spawn_calls)} supported + {len(spawn_calls)} ordinary)"
        )
    return actors


def parse_subway_encounters(repo_root: Path) -> List[ActorDefinition]:
    path = "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedSubwayEncounterRuntimeService.cs"
    text = read_source(repo_root, path)
    constants = extract_constants(text)
    actors: List[ActorDefinition] = []

    fixed_methods = (
        ("private static CapturedEncounterRuntimeDefinition CreateBossDefinition()", "Abmouth Supremus", "AbmouthMonsterData", 30, "AbmouthProfileKey"),
        ("private static CapturedEncounterRuntimeDefinition CreateEumenidesDefinition()", "Eumenides", "EumenidesMonsterData", 20, "EumenidesProfileKey"),
    )
    for method, name, monster_constant, level, selector in fixed_methods:
        body = extract_method_body(text, method)
        calls = extract_calls(body, "CapturedEncounterRuntimeDefinition", True)
        if len(calls) != 1:
            raise CoverageError(f"expected one encounter definition in {method}")
        args = split_top_level(calls[0])
        parsed_name = parse_csharp_string(args[3], constants)
        parsed_md = parse_csharp_int(args[4], constants)
        parsed_level = parse_csharp_int(args[7], constants)
        if (parsed_name, parsed_md, parsed_level) != (
            name,
            parse_csharp_int(monster_constant, constants),
            level,
        ):
            raise CoverageError(f"unexpected encounter identity in {method}")
        actors.append(
            make_actor(
                "subway-initial-encounters",
                127,
                name,
                parsed_md,
                level,
                path,
                runtime_profile_selector=selector,
            )
        )

    variants_body = extract_array_initializer(
        text, "CapturedEncounterLevelHealthVariant[] VergilAeneidVariants"
    )
    variant_calls = extract_calls(
        variants_body, "CapturedEncounterLevelHealthVariant", True
    )
    variant_levels = tuple(
        parse_csharp_int(split_top_level(call)[0], constants) for call in variant_calls
    )
    if variant_levels != (29, 30, 31):
        raise CoverageError(f"unexpected Vergil runtime level variants: {variant_levels}")
    actors.append(
        make_actor(
            "subway-initial-encounters",
            127,
            "Vergil Aeneid",
            parse_csharp_int("VergilAeneidMonsterData", constants),
            variant_levels,
            path,
            runtime_profile_selector="VergilAeneidProfileKey",
            notes=("one runtime actor selects one of the listed captured level variants",),
        )
    )
    return actors


def parse_temple_ordinary(repo_root: Path) -> List[ActorDefinition]:
    path = "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedTempleOfThreeWindsContentProvider.cs"
    text = read_source(repo_root, path)
    profiles_body = extract_array_initializer(text, "ProfileSeed[] ProfileSeeds")
    profiles: Dict[str, int] = {}
    for call in extract_calls(profiles_body, "ProfileSeed", True):
        args = split_top_level(call)
        profiles[parse_csharp_string(args[0])] = parse_csharp_int(args[1])
    if len(profiles) != 7:
        raise CoverageError(f"Temple Cultist profile parser found {len(profiles)} profiles")

    actors: List[ActorDefinition] = []
    spawn_body = extract_array_initializer(text, "SpawnSeed[] SpawnSeeds")
    for call in extract_calls(spawn_body, "SpawnSeed", True):
        args = split_top_level(call)
        source_identity = parse_csharp_int(args[0])
        profile_key = parse_csharp_string(args[1])
        if profile_key not in profiles:
            raise CoverageError(f"unknown Temple profile key in spawn: {profile_key}")
        actors.append(
            make_actor(
                "temple-ordinary",
                1931,
                "Cultist",
                profiles[profile_key],
                parse_csharp_int(args[2]),
                path,
                configured_source_identity=source_identity,
                runtime_source_identity_hint=source_identity,
                runtime_profile_selector=profile_key,
                evidence_capture_ids=(parse_csharp_string(args[-1]),),
            )
        )
    if len(actors) != 149:
        raise CoverageError(f"Temple Cultist parser found {len(actors)} spawns")

    sentinel_body = extract_method_body(
        text,
        "private static OrdinaryEnemySpawnDefinition[] BuildEternalSentinelSpawns()",
    )
    sentinel_calls = extract_calls(sentinel_body, "BuildEternalSentinelSpawn")
    if len(sentinel_calls) != 3:
        raise CoverageError(f"Temple Sentinel parser found {len(sentinel_calls)} spawns")
    for call in sentinel_calls:
        args = split_top_level(call)
        source_identity = parse_csharp_int(args[0])
        actors.append(
            make_actor(
                "temple-ordinary",
                1931,
                "Eternal Sentinel",
                41690,
                parse_csharp_int(args[1]),
                path,
                configured_source_identity=source_identity,
                runtime_source_identity_hint=source_identity,
                runtime_profile_selector="totw.ordinary.eternal-sentinel.41690",
                evidence_capture_ids=("20260721-041439",),
            )
        )

    deathless_body = extract_method_body(
        text,
        "private static OrdinaryEnemySpawnDefinition[] BuildDeathlessLegionnaireSpawns()",
    )
    deathless_calls = extract_calls(
        deathless_body,
        "BuildDeathlessLegionnaireSpawn",
    )
    if len(deathless_calls) != 14:
        raise CoverageError(
            f"Temple Deathless Legionnaire parser found {len(deathless_calls)} spawns"
        )
    for call in deathless_calls:
        args = split_top_level(call)
        source_identity = parse_csharp_int(args[0])
        actors.append(
            make_actor(
                "temple-ordinary",
                1931,
                "Deathless Legionnaire",
                42981,
                parse_csharp_int(args[1]),
                path,
                configured_source_identity=source_identity,
                runtime_source_identity_hint=source_identity,
                runtime_profile_selector=PF1931_DEATHLESS_PROFILE_SELECTOR,
                evidence_capture_ids=(
                    "20260722-042930",
                    "20260722-043108",
                    "20260722-044315",
                ),
            )
        )

    murial_body = extract_method_body(
        text, "private static OrdinaryEnemySpawnDefinition BuildMurialSpawn()"
    )
    murial_calls = extract_calls(murial_body, "OrdinaryEnemySpawnDefinition", True)
    if len(murial_calls) != 1:
        raise CoverageError("Temple Murial spawn constructor shape changed")
    murial_args = split_top_level(murial_calls[0])
    murial_source = parse_csharp_int(murial_args[1])
    actors.append(
        make_actor(
            "temple-ordinary",
            1931,
            "Murial the Faithful",
            26090,
            parse_csharp_int(murial_args[4]),
            path,
            configured_source_identity=murial_source,
            runtime_source_identity_hint=murial_source,
            runtime_profile_selector="totw.ordinary.main-room.murial-the-faithful.26090",
            evidence_capture_ids=("20260721-232051", "20260721-234614"),
        )
    )
    if len(actors) != 167:
        raise CoverageError(f"Temple ordinary parser reconciled {len(actors)} actors")
    return actors


def parse_temple_encounters(repo_root: Path) -> List[ActorDefinition]:
    path = "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedTempleOfThreeWindsEncounterRuntimeService.cs"
    text = read_source(repo_root, path)
    loot_constants = extract_constants(
        read_source(
            repo_root,
            "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedTempleOfThreeWindsLootDefinitions.cs",
        )
    )
    constants = extract_constants(text, loot_constants)
    actors: List[ActorDefinition] = []
    methods = (
        ("internal static CapturedEncounterRuntimeDefinition CreateDefenderDefinition()", "CapturedEncounterRuntimeDefinition", True),
        ("internal static CapturedEncounterRuntimeDefinition CreateYatilaDefinition()", "HumanDefinition", False),
        ("internal static CapturedEncounterRuntimeDefinition CreateGulardDefinition()", "HumanDefinition", False),
        ("internal static CapturedEncounterRuntimeDefinition CreateReAnimatorDefinition()", "HumanDefinition", False),
        ("internal static CapturedEncounterRuntimeDefinition CreateBetanyDefinition()", "HumanDefinition", False),
        ("internal static CapturedEncounterRuntimeDefinition CreateCuratorDefinition()", "CapturedEncounterRuntimeDefinition", True),
        ("internal static CapturedEncounterRuntimeDefinition CreateNematetDefinition()", "CapturedEncounterRuntimeDefinition", True),
        ("internal static CapturedEncounterRuntimeDefinition CreateGuardianDefinition()", "CapturedEncounterRuntimeDefinition", True),
        ("internal static CapturedEncounterRuntimeDefinition CreateGartuaDefinition()", "CapturedEncounterRuntimeDefinition", True),
        ("internal static CapturedEncounterRuntimeDefinition CreateUkleshDefinition()", "CapturedEncounterRuntimeDefinition", True),
        ("internal static CapturedEncounterRuntimeDefinition CreateKhalumDefinition()", "CapturedEncounterRuntimeDefinition", True),
        ("internal static CapturedEncounterRuntimeDefinition CreateAzturDefinition()", "CapturedEncounterRuntimeDefinition", True),
    )
    for method, call_name, require_new in methods:
        body = extract_method_body(text, method)
        calls = extract_calls(body, call_name, require_new)
        if len(calls) != 1:
            raise CoverageError(f"expected one {call_name} call in {method}")
        args = split_top_level(calls[0])
        name = parse_csharp_string(args[3], constants)
        monster_data = parse_csharp_int(args[4], constants)
        level_index = 5 if call_name == "HumanDefinition" else 7
        level = parse_csharp_int(args[level_index], constants)
        actors.append(
            make_actor(
                "temple-named-encounters",
                1931,
                name,
                monster_data,
                level,
                path,
                runtime_profile_selector=parse_csharp_string(args[0], constants),
            )
        )
    reanimated_body = extract_method_body(
        text,
        "private static CapturedEncounterRuntimeDefinition CreateReanimatedDefinition(",
    )
    reanimated_calls = extract_calls(
        reanimated_body, "CapturedEncounterRuntimeDefinition", True
    )
    if len(reanimated_calls) != 1:
        raise CoverageError("Temple Reanimated Corpse definition shape changed")
    args = split_top_level(reanimated_calls[0])
    reanimated = make_actor(
        "temple-reanimated-corpse-adds",
        1931,
        parse_csharp_string(args[3], constants),
        parse_csharp_int(args[4], constants),
        parse_csharp_int(args[7], constants),
        path,
        runtime_profile_selector=parse_csharp_string(args[0], constants),
        notes=("two fixed Re-Animator encounter slots",),
    )
    reanimated.actor_count = 2
    actors.append(reanimated)
    return actors


def parse_nascence_core(repo_root: Path) -> List[ActorDefinition]:
    path = "AORebirth/Server/ZoneEngine/Core/Playfields/NascenceCoreHecklerContentProvider.cs"
    text = read_source(repo_root, path)
    constants = extract_constants(text)
    body = extract_array_initializer(
        text, "NascenceCoreHecklerSpawnDefinition[] Spawns"
    )
    actors: List[ActorDefinition] = []
    for call in extract_calls(body, "NascenceCoreHecklerSpawnDefinition", True):
        args = split_top_level(call)
        source_identity = parse_csharp_int(args[0])
        actors.append(
            make_actor(
                "nascence-core-hecklers",
                parse_csharp_int("PlayfieldInstance", constants),
                parse_csharp_string(args[1]),
                parse_csharp_int("MonsterData", constants),
                parse_csharp_int(args[2]),
                path,
                configured_source_identity=source_identity,
                runtime_source_identity_hint=None,
                runtime_profile_selector="NascenceCoreHecklerContentProvider",
                evidence_capture_ids=(parse_csharp_string("CaptureId", constants),),
                notes=("runtime spawner does not retain the official capture source identity as a resolver hint",),
            )
        )
    if len(actors) != 40:
        raise CoverageError(f"Nascence Core parser found {len(actors)} Hecklers")
    return actors


def parse_structured_npc_array(
    repo_root: Path,
    relative_path: str,
    declaration_marker: str,
    type_name: str,
    surface: str,
    fixed_resource: Optional[int] = None,
    capture_field: Optional[str] = None,
    constants: Mapping[str, Any] = {},
) -> List[ActorDefinition]:
    text = read_source(repo_root, relative_path)
    body = extract_array_initializer(text, declaration_marker)
    rows = parse_object_initializers(body, type_name)
    actors: List[ActorDefinition] = []
    for row in rows:
        required = {"Name", "Level", "MonsterData"}
        missing = sorted(required - set(row))
        if missing:
            raise CoverageError(
                f"{relative_path} {type_name} row missing fields: {', '.join(missing)}"
            )
        resource = fixed_resource
        if resource is None:
            if "PlayfieldId" not in row:
                raise CoverageError(f"{relative_path} row lacks PlayfieldId")
            resource = parse_csharp_int(row["PlayfieldId"], constants)
        evidence: Tuple[str, ...] = ()
        if capture_field and capture_field in row:
            evidence = (parse_csharp_string(row[capture_field], constants),)
        actors.append(
            make_actor(
                surface,
                resource,
                parse_csharp_string(row["Name"], constants),
                parse_csharp_int(row["MonsterData"], constants),
                parse_csharp_int(row["Level"], constants),
                relative_path,
                evidence_capture_ids=evidence,
            )
        )
    return actors


def parse_nascence_life(repo_root: Path) -> List[ActorDefinition]:
    path = "AORebirth/Server/ZoneEngine/Core/Playfields/NascenceLifeSpawn.cs"
    module_path = (
        "AORebirth/Server/ZoneEngine/Core/Playfields/Content/"
        "NascenceLifeContentModule.cs"
    )
    module_constants = extract_constants(read_source(repo_root, module_path))
    constants = dict(module_constants)
    constants.update(
        {
            "NascenceLifeContentModule." + name: value
            for name, value in module_constants.items()
        }
    )
    actors = parse_structured_npc_array(
        repo_root,
        path,
        "LifeNpc[] Npcs",
        "LifeNpc",
        "nascence-life",
        capture_field="CaptureFolder",
        constants=constants,
    )
    for actor in actors:
        actor.content_sources = (path, module_path)
    counts = defaultdict(int)
    for actor in actors:
        counts[actor.resource] += actor.actor_count
    expected = {4001: 1, 4310: 245, 4311: 387, 4312: 197, 4531: 7}
    if dict(counts) != expected or len(actors) != 837:
        raise CoverageError(
            f"Nascence Life parser found {len(actors)} actors with playfield counts {dict(counts)}"
        )
    return actors


def parse_arete_family(repo_root: Path) -> List[ActorDefinition]:
    resource = 6553
    actors: List[ActorDefinition] = []

    alex_path = "AORebirth/Server/ZoneEngine/Core/Playfields/AlexAreaMobRuntime.cs"
    alex = read_source(repo_root, alex_path)
    alex_constants = extract_constants(alex)
    alex_body = extract_array_initializer(alex, "MobSlot[] Slots")
    for call in extract_calls(alex_body, "MobSlot", True):
        args = split_top_level(call)
        actors.append(
            make_actor(
                "arete-family",
                resource,
                parse_csharp_string(args[0]),
                parse_csharp_int(args[2]),
                parse_csharp_int(args[3]),
                alex_path,
                runtime_source_identity_hint=(
                    parse_csharp_int(args[14], alex_constants)
                    if len(args) >= 17
                    else None
                ),
                runtime_profile_selector=(
                    parse_csharp_string(args[13], alex_constants)
                    if len(args) >= 17
                    else ""
                ),
                runtime_attack_range_micrometers=(
                    parse_csharp_int(args[15], alex_constants)
                    if len(args) >= 17
                    else 0
                ),
                runtime_special_attack_weapon_unknown5=(
                    parse_csharp_int(args[16], alex_constants)
                    if len(args) >= 17
                    else None
                ),
                evidence_capture_ids=("20260720-204431",),
            )
        )

    junkyard_path = "AORebirth/Server/ZoneEngine/Core/Playfields/JunkyardCleaningRobotRuntime.cs"
    junkyard = read_source(repo_root, junkyard_path)
    junkyard_constants = extract_constants(junkyard)
    junkyard_body = extract_array_initializer(junkyard, "float[][] SpawnSlots")
    junkyard_slots = len(extract_calls(junkyard_body, "new[]"))
    # ``new[]`` is lexical punctuation rather than a callable; count the exact
    # three-float rows after validating the whole initializer shape.
    if junkyard_slots == 0:
        junkyard_slots = len(re.findall(r"\bnew\s*\[\]\s*\{", junkyard_body))
    if junkyard_slots != 14:
        raise CoverageError(f"Junkyard Cleaning Robot parser found {junkyard_slots} slots")
    robot = make_actor(
        "arete-family",
        resource,
        parse_csharp_string("RobotName", junkyard_constants),
        parse_csharp_int("RobotMonsterData", junkyard_constants),
        parse_csharp_int("RobotLevel", junkyard_constants),
        junkyard_path,
        runtime_source_identity_hint=parse_csharp_int(
            "CombatEvidenceSourceIdentity", junkyard_constants
        ),
        runtime_profile_selector=parse_csharp_string(
            "CombatProfileSelector", junkyard_constants
        ),
        runtime_attack_range_micrometers=parse_csharp_int(
            "CombatAttackRangeMicrometers", junkyard_constants
        ),
        runtime_special_attack_weapon_unknown5=0,
        evidence_capture_ids=("20260720-212302",),
    )
    robot.actor_count = junkyard_slots
    actors.append(robot)

    lorelei_path = "AORebirth/Server/ZoneEngine/Core/Playfields/LoreleiOasisMobRuntime.cs"
    lorelei = read_source(repo_root, lorelei_path)
    lorelei_constants = extract_constants(lorelei)
    excluded_greedy = 0
    for marker in ("MobSlot[] DesertReetSlots", "MobSlot[] RollerratSlots"):
        body = extract_array_initializer(lorelei, marker)
        for call in extract_calls(body, "MobSlot", True):
            args = split_top_level(call)
            name = parse_csharp_string(args[0])
            if name == "Greedy Desert Reet":
                excluded_greedy += 1
                continue
            monster_constant = (
                "RollerratMonsterData"
                if marker.endswith("RollerratSlots")
                else "ReetMonsterData"
            )
            actors.append(
                make_actor(
                    "arete-family",
                    resource,
                    name,
                    parse_csharp_int(monster_constant, lorelei_constants),
                    parse_csharp_int(args[1]),
                    lorelei_path,
                    runtime_source_identity_hint=(
                        parse_csharp_int(args[8], lorelei_constants)
                        if len(args) >= 11
                        else None
                    ),
                    runtime_profile_selector=(
                        parse_csharp_string(args[7], lorelei_constants)
                        if len(args) >= 11
                        else ""
                    ),
                    runtime_attack_range_micrometers=(
                        parse_csharp_int(args[9], lorelei_constants)
                        if len(args) >= 11
                        else 0
                    ),
                    runtime_special_attack_weapon_unknown5=(
                        parse_csharp_int(args[10], lorelei_constants)
                        if len(args) >= 11
                        else None
                    ),
                    evidence_capture_ids=("20260721-loralei",),
                )
            )
    if excluded_greedy != 1:
        raise CoverageError(f"expected one attack-immune Greedy Desert Reet, found {excluded_greedy}")

    landing_path = "AORebirth/Server/ZoneEngine/Core/Playfields/AreteLandingSpawn.cs"
    landing = read_source(repo_root, landing_path)
    landing_body = extract_array_initializer(landing, "AreteNpc[] Npcs")
    marcus_rows = [
        fields
        for fields in parse_object_initializers(landing_body, "AreteNpc")
        if parse_csharp_string(fields.get("Name", "\"\"")) == "Marcus Stone"
    ]
    if len(marcus_rows) != 1:
        raise CoverageError(f"Arete Marcus parser found {len(marcus_rows)} definitions")
    marcus_fields = marcus_rows[0]
    actors.append(
        make_actor(
            "arete-family",
            resource,
            "Marcus Stone",
            parse_csharp_int(marcus_fields["MonsterData"]),
            parse_csharp_int(marcus_fields["Level"]),
            landing_path,
            configured_source_identity=0x78E0FC62,
            runtime_source_identity_hint=None,
            runtime_profile_selector="MarcusPadAmbientCombat.QuarantineMarcus",
            evidence_capture_ids=("20260719-do-flint-bio-com", "20260720-064523"),
            notes=(
                "capture identity 0x78E0FC62 is retained only in the content evidence comment, not as a runtime resolver hint",
            ),
        )
    )

    marcus_path = "AORebirth/Server/ZoneEngine/Core/Playfields/MarcusPadAmbientCombat.cs"
    marcus = read_source(repo_root, marcus_path)
    marcus_constants = extract_constants(marcus)
    actors.append(
        make_actor(
            "arete-family",
            resource,
            parse_csharp_string("BurningRobotName", marcus_constants),
            297023,
            parse_csharp_int("RobotLevel", marcus_constants),
            marcus_path,
            evidence_capture_ids=("20260720-064523",),
        )
    )
    if sum(actor.actor_count for actor in actors) != 96:
        raise CoverageError(
            "Arete family parser reconciled "
            f"{sum(actor.actor_count for actor in actors)} actors instead of 96"
        )
    return actors


def parse_arete_additional(repo_root: Path) -> List[ActorDefinition]:
    robot_path = "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedAreteRobotContentProvider.cs"
    robot_text = read_source(repo_root, robot_path)
    robot_constants = extract_constants(robot_text)
    body = extract_array_initializer(
        robot_text, "CapturedAreteRobotSpawnDefinition[] SpawnDefinitions"
    )
    actors: List[ActorDefinition] = []
    for call in extract_calls(body, "CapturedAreteRobotSpawnDefinition", True):
        args = split_top_level(call)
        source_identity = parse_csharp_int(args[0])
        actors.append(
            make_actor(
                "arete-additional-captured-actors",
                6553,
                parse_csharp_string("RobotName", robot_constants),
                parse_csharp_int("MonsterData", robot_constants),
                parse_csharp_int(args[5]),
                robot_path,
                configured_source_identity=source_identity,
                runtime_source_identity_hint=parse_csharp_int(
                    args[11], robot_constants
                ),
                runtime_profile_selector=parse_csharp_string(
                    args[10], robot_constants
                ),
                runtime_attack_range_micrometers=0,
                runtime_special_attack_weapon_unknown5=0,
                evidence_capture_ids=("20260629-193121", "20260719-Rex-Markus-stone"),
                notes=(
                    "spawn/patrol capture identity is distinct from the exact combat-evidence source identity",
                ),
            )
        )
    if len(actors) != 11:
        raise CoverageError(f"Arete captured robot parser found {len(actors)} spawns")

    automaton_path = "AORebirth/Server/ZoneEngine/Core/Playfields/AreteFinishCaptureMobRuntime.cs"
    automaton = read_source(repo_root, automaton_path)
    constants = extract_constants(automaton)
    actors.append(
        make_actor(
            "arete-additional-captured-actors",
            6553,
            "Engineer Automaton I",
            parse_csharp_int("AutomatonMonsterData", constants),
            5,
            automaton_path,
            configured_source_identity=0x7985CD86,
            runtime_source_identity_hint=parse_csharp_int(
                "AutomatonCombatEvidenceSourceIdentity", constants
            ),
            runtime_profile_selector=parse_csharp_string(
                "AutomatonCombatProfileSelector", constants
            ),
            evidence_capture_ids=("20260721-finish",),
            notes=(
                "expected exclusion: the exact captured actor has no capture-certified combat profile",
            ),
        )
    )

    landing_path = "AORebirth/Server/ZoneEngine/Core/Playfields/AreteLandingSpawn.cs"
    landing = read_source(repo_root, landing_path)
    landing_body = extract_array_initializer(landing, "AreteNpc[] Npcs")
    landing_rows = [
        fields
        for fields in parse_object_initializers(landing_body, "AreteNpc")
        if parse_csharp_string(fields.get("Name", '""'))
        in ("ICC Peacekeeper", "Robotic Guard Dog")
    ]
    peacekeeper_path = (
        "AORebirth/Server/ZoneEngine/Core/Playfields/"
        "AreteIccPeacekeeperPatrolRuntime.cs"
    )
    guard_dog_path = (
        "AORebirth/Server/ZoneEngine/Core/Playfields/"
        "AreteRoboticGuardDogRuntime.cs"
    )
    combat_constants = extract_constants(read_source(repo_root, peacekeeper_path))
    combat_constants = extract_constants(
        read_source(repo_root, guard_dog_path), combat_constants
    )
    for fields in landing_rows:
        name = parse_csharp_string(fields["Name"])
        actors.append(
            make_actor(
                "arete-additional-captured-actors",
                6553,
                name,
                parse_csharp_int(fields["MonsterData"]),
                parse_csharp_int(fields["Level"]),
                landing_path,
                configured_source_identity=parse_csharp_int(
                    fields["CaptureInstance"]
                ),
                runtime_source_identity_hint=parse_csharp_int(
                    fields["CombatEvidenceSourceIdentity"], combat_constants
                ),
                runtime_profile_selector=parse_csharp_string(
                    fields["CombatProfileSelector"], combat_constants
                ),
                evidence_capture_ids=(
                    "20260722-235510"
                    if name == "ICC Peacekeeper"
                    else "20260722-212421",
                ),
                notes=(
                    "spawn capture identity is retained separately from the combat-evidence selector",
                ),
            )
        )

    if len(landing_rows) != 5 or len(actors) != 17:
        raise CoverageError(
            "Arete additional parser expected 11 cleaning robots, one Engineer, "
            "four ICC Peacekeepers, and one Robotic Guard Dog"
        )

    return actors


def parse_subway_merchants(repo_root: Path) -> List[ActorDefinition]:
    path = "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedSubwayVendorContentProvider.cs"
    text = read_source(repo_root, path)
    body = extract_array_initializer(
        text,
        "ReadOnlyCollection<CapturedSubwayVendorDefinition> CapturedDefinitions",
    )
    actors: List[ActorDefinition] = []
    for call in extract_calls(body, "Create"):
        args = split_top_level(call)
        if len(args) < 16:
            raise CoverageError("short Subway merchant Create call")
        source_identity = parse_csharp_int(args[0])
        actors.append(
            make_actor(
                "subway-merchants",
                127,
                parse_csharp_string(args[2]),
                parse_csharp_int(args[14]),
                180,
                path,
                configured_source_identity=source_identity,
                runtime_source_identity_hint=None,
                evidence_capture_ids=("20260709-212115",),
                notes=("vendor runtime does not retain the official capture source identity as a resolver hint",),
            )
        )
    if len(actors) != 6:
        raise CoverageError(f"Subway merchant parser found {len(actors)} actors")
    return actors


def parse_city_and_garden(repo_root: Path) -> List[ActorDefinition]:
    rome_path = "AORebirth/Server/ZoneEngine/Core/Playfields/RomeBlueCitySpawn.cs"
    rome = parse_structured_npc_array(
        repo_root,
        rome_path,
        "CityNpc[] Npcs",
        "CityNpc",
        "rome-blue-city",
        fixed_resource=735,
    )
    if len(rome) != 22:
        raise CoverageError(f"Rome Blue parser found {len(rome)} actors")
    for actor in rome:
        actor.content_evidence_capture_ids = ("20260717-210219",)

    thrak_path = "AORebirth/Server/ZoneEngine/Core/Playfields/ThrakOmniGardenSpawn.cs"
    thrak = parse_structured_npc_array(
        repo_root,
        thrak_path,
        "GardenNpc[] Npcs",
        "GardenNpc",
        "thrak-omni-garden",
        fixed_resource=4677,
    )
    if len(thrak) != 10:
        raise CoverageError(f"Thrak Garden parser found {len(thrak)} actors")
    for actor in thrak:
        actor.content_evidence_capture_ids = ("20260718-165625",)
    return rome + thrak


def parse_all_actors(repo_root: Path) -> List[ActorDefinition]:
    actors: List[ActorDefinition] = []
    actors.extend(parse_subway_ordinary(repo_root))
    actors.extend(parse_subway_encounters(repo_root))
    actors.extend(parse_temple_ordinary(repo_root))
    actors.extend(parse_temple_encounters(repo_root))
    actors.extend(parse_nascence_core(repo_root))
    actors.extend(parse_nascence_life(repo_root))
    actors.extend(parse_arete_family(repo_root))
    actors.extend(parse_arete_additional(repo_root))
    actors.extend(parse_subway_merchants(repo_root))
    actors.extend(parse_city_and_garden(repo_root))

    actual_by_surface = defaultdict(int)
    for actor in actors:
        actual_by_surface[actor.surface] += actor.actor_count
    expected_by_surface = dict(SURFACE_EXPECTATIONS)
    if dict(actual_by_surface) != expected_by_surface:
        raise CoverageError(
            "surface population mismatch: expected "
            f"{expected_by_surface}, parsed {dict(actual_by_surface)}"
        )
    total = sum(actual_by_surface.values())
    if total != EXPECTED_INITIAL_ACTORS:
        raise CoverageError(
            f"fixed population parsed {total} actors instead of {EXPECTED_INITIAL_ACTORS}"
        )
    return actors


def merge_actors(actors: Iterable[ActorDefinition]) -> List[ActorDefinition]:
    merged: Dict[Tuple[Any, ...], ActorDefinition] = {}
    for actor in actors:
        key = actor.merge_key()
        existing = merged.get(key)
        if existing is None:
            merged[key] = ActorDefinition(**actor.__dict__)
            continue
        existing.actor_count += actor.actor_count
        existing.content_sources = tuple(
            sorted(set(existing.content_sources) | set(actor.content_sources))
        )
        existing.content_evidence_capture_ids = tuple(
            sorted(
                set(existing.content_evidence_capture_ids)
                | set(actor.content_evidence_capture_ids)
            )
        )
    return list(merged.values())


def parse_combat_profile_key(profile_key: str) -> Optional[Tuple[int, int, int, str]]:
    if profile_key == "unresolved-metadata":
        return None
    match = re.fullmatch(
        r"resource=(unmapped|-?\d+)\|md=(-?\d+)\|level=(-?\d+)\|name=(.*)",
        profile_key,
    )
    if not match:
        raise CoverageError(f"unrecognized combat inventory profile key: {profile_key}")
    if match.group(1) == "unmapped":
        return None
    return int(match.group(1)), int(match.group(2)), int(match.group(3)), match.group(4)


def parse_unmapped_combat_profile_key(
    profile_key: str,
) -> Optional[Tuple[int, int, str]]:
    match = re.fullmatch(
        r"resource=unmapped\|md=(-?\d+)\|level=(-?\d+)\|name=(.*)",
        profile_key,
    )
    if match is None:
        return None
    return int(match.group(1)), int(match.group(2)), match.group(3)


def parse_elysium_population_audit(repo_root: Path) -> Dict[str, Any]:
    path = "AORebirth/Server/ZoneEngine/Core/Playfields/ElysiumEastMobRuntime.cs"
    text = read_source(repo_root, path)
    body = extract_array_initializer(text, "MobSlot[] Slots")
    rows = parse_object_initializers(body, "MobSlot")
    if not rows:
        raise CoverageError("Elysium East/South parser found no captured slots")

    identities: set[Tuple[int, int, int, str]] = set()
    playfields: set[int] = set()
    heckler_slots = 0
    for row in rows:
        missing = sorted(
            {"Name", "PlayfieldId", "MonsterData", "Level"} - set(row)
        )
        if missing:
            raise CoverageError(
                "Elysium captured slot is missing exact identity fields: "
                + ", ".join(missing)
            )
        name = parse_csharp_string(row["Name"])
        playfield = parse_csharp_int(row["PlayfieldId"])
        monster_data = parse_csharp_int(row["MonsterData"])
        level = parse_csharp_int(row["Level"])
        playfields.add(playfield)
        identities.add((playfield, monster_data, level, name))
        if name.startswith("Heckler of "):
            heckler_slots += 1

    if playfields != {4540, 4543}:
        raise CoverageError(
            f"Elysium captured population changed playfields: {sorted(playfields)}"
        )
    return {
        "path": path,
        "slotCount": len(rows),
        "profileIdentityCount": len(identities),
        "hecklerSlotCount": heckler_slots,
        "playfields": sorted(playfields),
        "captureIds": (
            "20260727-182451",
            "20260727-190145",
            "20260727-193914",
            "20260727-201436",
        ),
    }


def parse_dynamic_mission_profiles(repo_root: Path) -> List[Dict[str, Any]]:
    catalog_path = (
        "AORebirth/Server/ZoneEngine/Core/Playfields/MissionInstanceShapeCatalog.cs"
    )
    spawn_path = "AORebirth/Server/ZoneEngine/Core/Playfields/MissionInstanceSpawn.cs"
    definitions: Dict[Tuple[str, int, int], Dict[str, Any]] = {}

    def add_definition(fields: Mapping[str, str], source_path: str) -> None:
        role_expression = fields.get("Role", "")
        if role_expression not in (
            "MissionNpcRole.Trash",
            "MissionNpcRole.KillBoss",
            "MissionNpcRole.KillGuard",
        ):
            return
        name = parse_csharp_string(fields["Name"])
        monster_data = parse_csharp_int(fields["MonsterData"])
        level = parse_csharp_int(fields["Level"])
        key = (name, monster_data, level)
        row = definitions.setdefault(
            key,
            {
                "family": "dynamic-mission-mobs",
                "name": name,
                "monsterData": monster_data,
                "level": level,
                "roles": set(),
                "configuredDefinitionCount": 0,
                "contentSources": set(),
            },
        )
        row["roles"].add(role_expression.rsplit(".", 1)[-1])
        row["configuredDefinitionCount"] += 1
        row["contentSources"].add(source_path)

    catalog_text = read_source(repo_root, catalog_path)
    for fields in parse_object_initializers(catalog_text, "MissionNpc"):
        add_definition(fields, catalog_path)

    spawn_text = read_source(repo_root, spawn_path)
    for fields in parse_object_initializers(spawn_text, "MissionNpc"):
        name_expression = fields.get("Name", '""')
        try:
            parsed_name = parse_csharp_string(name_expression)
        except CoverageError:
            continue
        if parsed_name == "Mission Cache":
            add_definition(fields, spawn_path)

    rows: List[Dict[str, Any]] = []
    for key in sorted(definitions, key=lambda value: (value[0], value[1], value[2])):
        row = definitions[key]
        row["roles"] = sorted(row["roles"])
        row["contentSources"] = sorted(row["contentSources"])
        rows.append(row)
    if len(rows) != 155 or sum(row["configuredDefinitionCount"] for row in rows) != 196:
        raise CoverageError(
            "dynamic mission profile parser expected 155 profiles / 196 configured "
            f"definitions, found {len(rows)} / "
            f"{sum(row['configuredDefinitionCount'] for row in rows)}"
        )
    return rows


def sorted_unique(values: Iterable[Any]) -> List[Any]:
    return sorted({value for value in values if value is not None and value != ""})


def observation_missing_fields(
    profile: Mapping[str, Any], source_identity: Optional[str]
) -> List[str]:
    observations = list(profile.get("incompleteObservations", []))
    if source_identity is not None:
        exact = [
            row for row in observations if row.get("sourceIdentity") == source_identity
        ]
        if exact:
            observations = exact
    missing: List[str] = []
    for row in observations:
        missing.extend(str(value) for value in row.get("missingEvidence", []))
        for conflict in row.get("conflicts", []):
            field_name = conflict.get("field")
            if field_name:
                missing.append(f"unambiguous {field_name}")
    return sorted_unique(missing)


def unresolved_evidence_rows(
    profile: Mapping[str, Any], source_identity: Optional[str]
) -> List[Dict[str, Any]]:
    observations: List[Tuple[str, Mapping[str, Any]]] = []
    for field_name, observation_type in (
        ("incompleteObservations", "incomplete-normal-attack"),
        ("nonNormalObservations", "non-normal-attack"),
        ("unsupportedSequences", "unsupported-sequence"),
    ):
        observations.extend(
            (observation_type, row) for row in profile.get(field_name, [])
        )
    if source_identity is not None:
        observations = [
            pair for pair in observations if pair[1].get("sourceIdentity") == source_identity
        ]
    result: List[Dict[str, Any]] = []
    for observation_type, row in observations:
        result.append(
            {
                "observationType": observation_type,
                "classification": row.get("classification"),
                "sourceIdentity": row.get("sourceIdentity"),
                "observationCount": row.get("observationCount", 0),
                "captureSessions": sorted_unique(row.get("captureSessions", [])),
                "samplePacketIds": sorted_unique(row.get("samplePacketIds", [])),
                "evidenceFound": row.get("evidenceFound", {}),
                "missingEvidence": sorted_unique(row.get("missingEvidence", [])),
                "conflicts": row.get("conflicts", []),
                "runtimeSupport": row.get("runtimeSupport"),
            }
        )
    return result


def classify_pf127_ordinary_profile_level(
    actor: ActorDefinition,
    level: int,
    profile: Optional[Mapping[str, Any]],
    family_profiles: Sequence[Mapping[str, Any]],
    resolver_owners: Sequence[Mapping[str, Any]],
) -> Optional[Dict[str, Any]]:
    if actor.surface != "subway-ordinary":
        return None
    if actor.resource != 127:
        raise CoverageError(
            "Subway ordinary profile resolver escaped PF127: "
            f"resource={actor.resource} selector={actor.runtime_profile_selector}"
        )
    if (
        not actor.runtime_profile_selector.startswith(
            PF127_ORDINARY_PROFILE_SELECTOR_PREFIX
        )
        and actor.runtime_profile_selector
        not in PF127_EXACT_SUPPORTED_PROFILE_SELECTORS
    ):
        return None
    if (
        actor.configured_source_identity is None
        or actor.runtime_source_identity_hint is None
        or actor.configured_source_identity != actor.runtime_source_identity_hint
    ):
        raise CoverageError(
            "PF127 ordinary profile resolver requires one exact configured/runtime "
            f"source identity: selector={actor.runtime_profile_selector}"
        )
    if not resolver_owners:
        raise CoverageError("PF127 ordinary profile resolver has no production owner")
    if profile is None:
        return None

    exact_level_variants = [
        variant
        for variant in profile.get("variants", [])
        if variant.get("captureCertified") is True
        and variant.get("captureEvidenceSafe") is True
    ]
    family_variants = [
        (family_profile, variant)
        for family_profile in family_profiles
        for variant in family_profile.get("variants", [])
        if variant.get("captureCertified") is True
        and variant.get("captureEvidenceSafe") is True
    ]
    evidence_scope = "exact-level-profile"
    if exact_level_variants:
        evidence_profile = profile
        evidence_variants = exact_level_variants
    elif family_variants:
        evidence_scope = "same-archetype-profile"
        evidence_profile, representative = family_variants[0]
        evidence_variants = [variant for _, variant in family_variants]
    else:
        return None

    representative = evidence_variants[0]
    packet_ids = [
        representative.get("representativeWifuPacketId"),
        representative.get("representativeSawPacketId"),
        representative.get("representativeAttackPacketId"),
    ]
    for stream in representative.get("streams", []):
        packet_ids.extend(stream.get("attackInfoPacketIds", [])[:1])

    source_identity = format_identity(actor.runtime_source_identity_hint)
    owner_paths = sorted_unique(owner.get("path") for owner in resolver_owners)
    capture_sessions = sorted_unique(
        session
        for variant in evidence_variants
        for session in variant.get("captureSessions", [])
    )
    retaliation_eligibility_promoted = (
        actor.runtime_profile_selector in PF127_EXACT_SUPPORTED_PROFILE_SELECTORS
    )
    return {
        "level": level,
        "combatProfileKey": profile.get("profileKey"),
        "classification": "certified",
        "resolutionMode": PF127_ORDINARY_PROFILE_RESOLUTION_MODE,
        "captureSessions": capture_sessions,
        "evidencePacketIds": sorted_unique(packet_ids),
        "evidenceFound": [
            {
                "observationType": (
                    "exact-level-profile-consumed-by-production-owned-pf127-"
                    "ordinary-resolver"
                ),
                "runtimeProfileSelector": actor.runtime_profile_selector,
                "runtimeSourceIdentity": source_identity,
                "exactCombatProfileKey": profile.get("profileKey"),
                "evidenceScope": evidence_scope,
                "evidenceCombatProfileKey": evidence_profile.get("profileKey"),
                "evidenceLevel": (evidence_profile.get("metadata") or {}).get(
                    "level"
                ),
                "captureCertifiedVariantCount": len(evidence_variants),
                "captureSessions": capture_sessions,
                "representativeEvidencePacketIds": sorted_unique(packet_ids),
            }
        ],
        "missingEvidence": [],
        "runtimeContractReady": True,
        "runtimeMissingEvidence": [],
        "disabledGameplayCapability": None,
        "runtimeProfileSelector": actor.runtime_profile_selector,
        "runtimeResolverSources": owner_paths,
        "exactRuntimeSourceIdentityRequired": True,
        "allConcreteRuntimeVariantsMustResolveRetaliationEligible": True,
        "allConcreteRuntimeVariantsMustResolveFinalCombatReady": True,
        "crossPlayfieldFallbackAllowed": False,
        "promotedCapability": (
            "source-bound combat-contract ownership and exact retaliation eligibility"
            if retaliation_eligibility_promoted
            else "combat-contract ownership only"
        ),
        "retaliationEligibilityPromoted": retaliation_eligibility_promoted,
        "automaticAggressionPolicyPromoted": False,
        "automaticCombatActivationPromoted": False,
    }


def classify_pf1931_owned_profile_level(
    actor: ActorDefinition,
    level: int,
    profile: Optional[Mapping[str, Any]],
    family_profiles: Sequence[Mapping[str, Any]],
    resolver_owners: Sequence[Mapping[str, Any]],
) -> Optional[Dict[str, Any]]:
    is_named_or_add = actor.surface in (
        "temple-named-encounters",
        "temple-reanimated-corpse-adds",
    )
    is_deathless = (
        actor.surface == "temple-ordinary"
        and actor.runtime_profile_selector == PF1931_DEATHLESS_PROFILE_SELECTOR
    )
    if not (is_named_or_add or is_deathless):
        return None
    if actor.resource != 1931:
        raise CoverageError(
            "PF1931 capture-contract resolver escaped PF1931: "
            f"resource={actor.resource} selector={actor.runtime_profile_selector}"
        )
    if not resolver_owners:
        raise CoverageError("PF1931 capture-contract resolver has no production owner")
    if profile is None and not family_profiles:
        return None

    evidence_profiles = (
        list(family_profiles)
        if is_deathless or profile is None
        else [profile]
    )
    capture_variants = [
        variant
        for evidence_profile in evidence_profiles
        for variant in evidence_profile.get("variants", [])
        if variant.get("captureCertified") is True
        and variant.get("captureEvidenceSafe") is True
    ]
    evidence_rows = [
        row
        for evidence_profile in evidence_profiles
        for row in unresolved_evidence_rows(evidence_profile, None)
    ]
    if not capture_variants and not evidence_rows:
        return None
    if is_deathless and not any(
        variant.get("runtimeContractReady") is True
        for variant in capture_variants
    ):
        return None

    capture_sessions = sorted_unique(
        [
            session
            for evidence_profile in evidence_profiles
            for session in evidence_profile.get("captureSessionsSearched", [])
        ]
        + [
            session
            for variant in capture_variants
            for session in variant.get("captureSessions", [])
        ]
        + [
            session
            for row in evidence_rows
            for session in row.get("captureSessions", [])
        ]
    )
    packet_ids = sorted_unique(
        [
            variant.get(field)
            for variant in capture_variants
            for field in (
                "representativeWifuPacketId",
                "representativeSawPacketId",
                "representativeAttackPacketId",
            )
        ]
        + [
            packet_id
            for variant in capture_variants
            for stream in variant.get("streams", [])
            for packet_id in stream.get("attackInfoPacketIds", [])[:1]
        ]
        + [
            packet_id
            for row in evidence_rows
            for packet_id in row.get("samplePacketIds", [])
        ]
    )
    owner_paths = sorted_unique(owner.get("path") for owner in resolver_owners)
    evidence_scope = (
        "capture-proven equipped-weapon archetype with production item-derived values"
        if is_deathless
        else "exact capture-backed encounter contract and packet fixture"
    )
    return {
        "level": level,
        "combatProfileKey": (
            profile.get("profileKey")
            if profile is not None
            else (
                f"resource={actor.resource}|md={actor.monster_data}|"
                f"level={level}|name={actor.name}"
            )
        ),
        "classification": "certified",
        "resolutionMode": PF1931_PROFILE_RESOLUTION_MODE,
        "captureSessions": capture_sessions,
        "evidencePacketIds": packet_ids,
        "evidenceFound": [
            {
                "observationType": "production-owned-exact-pf1931-contract",
                "runtimeProfileSelector": actor.runtime_profile_selector,
                "evidenceScope": evidence_scope,
                "captureCertifiedVariantCount": len(capture_variants),
                "correlatedPacketObservationCount": len(evidence_rows),
                "captureSessions": capture_sessions,
                "representativeEvidencePacketIds": packet_ids,
            }
        ],
        "missingEvidence": [],
        "runtimeContractReady": True,
        "runtimeMissingEvidence": [],
        "disabledGameplayCapability": None,
        "runtimeProfileSelector": actor.runtime_profile_selector,
        "runtimeResolverSources": owner_paths,
        "capturedRuntimeIdentityMappingUsed": False,
        "crossPlayfieldFallbackAllowed": False,
        "automaticAggressionPolicyPromoted": False,
        "automaticCombatActivationPromoted": False,
    }


def classify_arete_content_selector_level(
    actor: ActorDefinition,
    level: int,
    profile: Optional[Mapping[str, Any]],
) -> Optional[Dict[str, Any]]:
    if (
        actor.resource != 6553
        or profile is None
        or actor.runtime_source_identity_hint is None
        or not actor.runtime_profile_selector
        or actor.runtime_special_attack_weapon_unknown5 is None
    ):
        return None

    source_hint = format_identity(actor.runtime_source_identity_hint)
    selected = [
        variant
        for variant in profile.get("variants", [])
        if variant.get("captureCertified") is True
        and variant.get("captureEvidenceSafe") is True
        and variant.get("semanticProfileId") == actor.runtime_profile_selector
        and source_hint in variant.get("sourceIdentities", [])
        and variant.get("baseSignature", {}).get("weaponContextKind")
        in ("natural", "natural-or-special")
    ]
    if len(selected) != 1:
        return None

    variant = selected[0]
    captured_range = variant.get("capturedAttackRangeMeters")
    captured_range_evidence = variant.get("capturedAttackRangeEvidence")
    stream_ranges = {
        stream.get("capturedAttackRange")
        for stream in variant.get("streams", [])
    }
    if (
        not isinstance(captured_range, (int, float))
        or captured_range <= 0
        or not isinstance(captured_range_evidence, dict)
        or captured_range_evidence.get("attackRangeMeters") != captured_range
        or captured_range_evidence.get("statId") != 287
        or stream_ranges != {captured_range}
    ):
        return None
    source_saw_states = {
        row.get("unknown5")
        for row in variant.get("mutableSawStateObservations", [])
        if row.get("sourceIdentity") == source_hint
    }
    if source_saw_states != {actor.runtime_special_attack_weapon_unknown5}:
        return None

    streams = [
        stream
        for stream in variant.get("streams", [])
        if stream.get("capturedTerminalHitOnly") is not True
    ]
    if not streams:
        return None
    if any(
        not stream.get("damageObservations")
        or not stream.get("attackStartDelayObservationsSeconds")
        or not stream.get("firstHitDelayObservationsSeconds")
        or len(stream.get("attackStartDelayObservationsSeconds", []))
        != len(stream.get("firstHitDelayObservationsSeconds", []))
        or len(stream.get("initialAmmoCandidates", [])) != 1
        for stream in streams
    ):
        return None
    attack_start_delays = [
        delay
        for stream in streams
        for delay in stream.get("attackStartDelayObservationsSeconds", [])
    ]
    source_attack_start_delays = {
        timing.get("attackStartDelaySeconds")
        for stream in streams
        for timing in stream.get("pairedFightTimingObservations", [])
        if timing.get("sourceIdentity") == source_hint
    }
    if (
        not attack_start_delays
        or any(delay < 0 for delay in attack_start_delays)
        or source_attack_start_delays != {0.0}
        or not any(
            stream.get("landedIntervalObservationsSeconds") for stream in streams
        )
    ):
        return None

    packet_fields = (
        "representativeWifuPacketId",
        "representativeSawPacketId",
        "representativeAttackPacketId",
    )
    packet_ids: List[str] = [variant.get(field) for field in packet_fields]
    for stream in variant.get("streams", []):
        packet_ids.extend(stream.get("attackInfoPacketIds", [])[:1])
    return {
        "level": level,
        "combatProfileKey": (
            f"resource={actor.resource}|md={actor.monster_data}|level={level}|name={actor.name}"
        ),
        "classification": "certified",
        "resolutionMode": "exact-arete-generated-range-profile-selector",
        "captureSessions": sorted_unique(variant.get("captureSessions", [])),
        "evidencePacketIds": sorted_unique(packet_ids),
        "evidenceSourceIdentities": [source_hint],
        "evidenceFound": [
            {
                "observationType": "exact-captured-combat-profile-with-itemdb-range",
                "semanticProfileId": variant.get("semanticProfileId"),
                "sourceIdentity": source_hint,
                "capturedAttackRangeMeters": captured_range,
                "attackRangeEvidenceId": captured_range_evidence.get("evidenceId"),
                "attackRangeItemDatabaseSha256": captured_range_evidence.get(
                    "itemDatabaseSha256"
                ),
                "attackRangeStatId": captured_range_evidence.get("statId"),
                "capturedSpecialAttackWeaponPacketId": captured_range_evidence.get(
                    "representativeSawPacketId"
                ),
                "specialAttackWeaponUnknown5": actor.runtime_special_attack_weapon_unknown5,
                "capturedAttackStartDelaySeconds": 0.0,
                "capturedAttackStreamCount": len(streams),
            }
        ],
        "missingEvidence": [],
        "runtimeContractReady": True,
        "runtimeMissingEvidence": [],
        "disabledGameplayCapability": None,
        "semanticProfileId": variant.get("semanticProfileId"),
        "capturedAttackRangeMeters": captured_range,
        "attackRangeEvidenceId": captured_range_evidence.get("evidenceId"),
    }


def classify_level(
    actor: ActorDefinition,
    level: int,
    profiles_by_identity: Mapping[Tuple[int, int, int, str], Mapping[str, Any]],
    metadata_by_identity: Mapping[Tuple[int, int, int, str], Sequence[Mapping[str, Any]]],
    mathematical_bindings: Optional[
        Mapping[Tuple[int, int, int, str, Optional[str]], Mapping[str, Any]]
    ] = None,
    pf127_ordinary_profile_owners: Sequence[Mapping[str, Any]] = (),
    pf1931_profile_owners: Sequence[Mapping[str, Any]] = (),
    profiles_by_archetype: Optional[
        Mapping[Tuple[int, int, str], Sequence[Mapping[str, Any]]]
    ] = None,
) -> Dict[str, Any]:
    identity = (actor.resource, actor.monster_data, level, actor.name)
    profile = profiles_by_identity.get(identity)
    source_hint = format_identity(actor.runtime_source_identity_hint)
    configured_source = format_identity(actor.configured_source_identity)
    result: Dict[str, Any] = {
        "level": level,
        "combatProfileKey": (
            f"resource={actor.resource}|md={actor.monster_data}|level={level}|name={actor.name}"
        ),
        "classification": "unresolved",
        "resolutionMode": "none",
        "captureSessions": [],
        "evidencePacketIds": [],
        "evidenceFound": [],
        "missingEvidence": [],
        "runtimeContractReady": False,
        "runtimeMissingEvidence": [],
        "disabledGameplayCapability": "NPC auto-attack emission and damage application",
    }
    mathematical_binding = (
        mathematical_bindings.get(identity + (configured_source,))
        or mathematical_bindings.get(identity + (None,))
        if mathematical_bindings is not None
        else None
    )
    if mathematical_binding is not None:
        result.update(
            {
                "classification": "certified",
                "resolutionMode": "exact-mathematical-combat-setup",
                "captureSessions": mathematical_binding.get(
                    "captureSessions", []
                ),
                "evidencePacketIds": mathematical_binding.get(
                    "evidencePacketIds", []
                ),
                "evidenceFound": [
                    {
                        "observationType": "capture-validated-mathematical-combat-setup",
                        "formulaId": mathematical_binding.get("formulaId"),
                        "semanticProfileId": mathematical_binding.get(
                            "compatibleSemanticProfileId"
                        ),
                        "semanticProfileIds": mathematical_binding.get(
                            "compatibleSemanticProfileIds"
                        ),
                        "generatedSpecialAttackWeaponValue": (
                            mathematical_binding.get(
                                "generatedSpecialAttackWeaponValue"
                            )
                        ),
                        "generatedSpecialAttackWeaponValues": (
                            mathematical_binding.get(
                                "generatedSpecialAttackWeaponValues"
                            )
                        ),
                    }
                ],
                "missingEvidence": [],
                "runtimeContractReady": True,
                "runtimeMissingEvidence": [],
                "disabledGameplayCapability": None,
                "formulaId": mathematical_binding.get("formulaId"),
                "semanticProfileId": mathematical_binding.get(
                    "compatibleSemanticProfileId"
                ),
                "semanticProfileIds": mathematical_binding.get(
                    "compatibleSemanticProfileIds"
                ),
            }
        )
        return result
    pf1931_owned_profile = classify_pf1931_owned_profile_level(
        actor,
        level,
        profile,
        (profiles_by_archetype or {}).get(
            (actor.resource, actor.monster_data, actor.name),
            (),
        ),
        pf1931_profile_owners,
    )
    if pf1931_owned_profile is not None:
        return pf1931_owned_profile

    if profile is None:
        metadata = list(metadata_by_identity.get(identity, []))
        result["captureSessions"] = sorted_unique(row.get("capture") for row in metadata)
        result["metadataGenerationKeys"] = sorted_unique(
            row.get("generationKey") for row in metadata
        )
        result["evidenceFound"] = [
            {
                "observationType": "exact-profile-metadata-generation",
                "generationKey": row.get("generationKey"),
                "captureSession": row.get("capture"),
                "sourceIdentity": row.get("sourceIdentity"),
                "sequence": row.get("sequence"),
                "packetSha256": row.get("packetSha256"),
                "projection": row.get("projection"),
            }
            for row in metadata
        ]
        if metadata:
            result["missingEvidence"] = [
                "owner-linked WeaponItemFullUpdate weapon definition",
                "SpecialAttackWeapon packet in the same fight boundary",
                "Attack packet in the same fight boundary",
                "normal AttackInfo packet correlated to the same source generation",
            ]
            result["unresolvedReason"] = (
                "exact spawn metadata exists, but the canonical inventory contains no "
                "coherent normal-attack profile for this runtime identity"
            )
        else:
            result["missingEvidence"] = [
                "exact profile identity in the canonical capture corpus",
                "owner-linked WeaponItemFullUpdate weapon definition",
                "SpecialAttackWeapon packet in the same fight boundary",
                "Attack packet in the same fight boundary",
                "normal AttackInfo packet correlated to the same source generation",
            ]
            result["unresolvedReason"] = (
                "no exact spawn metadata or coherent normal-attack profile was recovered "
                "for this runtime identity"
            )
        result["runtimeMissingEvidence"] = list(result["missingEvidence"])
        return result

    capture_certified_variants = [
        variant for variant in profile.get("variants", []) if variant.get("captureCertified")
    ]
    runtime_ready_variants = [
        variant
        for variant in capture_certified_variants
        if variant.get("runtimeContractReady") is True
    ]
    capture_safe_variants = [
        variant
        for variant in capture_certified_variants
        if variant.get("captureEvidenceSafe") is True
    ]
    arete_content_selector = classify_arete_content_selector_level(
        actor, level, profile
    )
    if arete_content_selector is not None:
        return arete_content_selector

    selected: List[Mapping[str, Any]] = []
    if source_hint is not None:
        exact_source_matches = [
            variant
            for variant in runtime_ready_variants
            if source_hint in variant.get("sourceIdentities", [])
            and (
                actor.resource != 6553
                or not actor.runtime_profile_selector
                or variant.get("semanticProfileId")
                == actor.runtime_profile_selector
            )
        ]
        if len(exact_source_matches) == 1:
            selected = exact_source_matches
            result["classification"] = "certified"
            result["resolutionMode"] = (
                "exact-runtime-source-and-profile-selector"
                if actor.resource == 6553 and actor.runtime_profile_selector
                else "exact-runtime-source-identity"
            )
    if not selected and profile.get("semanticFallbackCaptureProven"):
        if len(runtime_ready_variants) == 1:
            selected = runtime_ready_variants
            result["classification"] = "certified"
            result["resolutionMode"] = "capture-proven-unique-semantic-fallback"

    result["combatInventoryStatus"] = profile.get("status")
    result["semanticFallbackCaptureProven"] = bool(
        profile.get("semanticFallbackCaptureProven")
    )
    result["runtimeContractReady"] = bool(selected)
    if selected:
        result["disabledGameplayCapability"] = None
        result["captureSessions"] = sorted_unique(
            session for variant in selected for session in variant.get("captureSessions", [])
        )
        packet_fields = (
            "representativeWifuPacketId",
            "representativeSawPacketId",
            "representativeAttackPacketId",
        )
        packet_ids: List[str] = []
        for variant in selected:
            packet_ids.extend(variant.get(field) for field in packet_fields)
            for stream in variant.get("streams", []):
                packet_ids.extend(stream.get("attackInfoPacketIds", [])[:1])
        result["evidencePacketIds"] = sorted_unique(packet_ids)
        result["evidenceSourceIdentities"] = sorted_unique(
            source for variant in selected for source in variant.get("sourceIdentities", [])
        )
        result["evidenceFound"] = [
            {
                "captureSessions": sorted_unique(variant.get("captureSessions", [])),
                "sourceIdentities": sorted_unique(variant.get("sourceIdentities", [])),
                "representativeWifuPacketId": variant.get("representativeWifuPacketId"),
                "representativeSawPacketId": variant.get("representativeSawPacketId"),
                "representativeAttackPacketId": variant.get("representativeAttackPacketId"),
                "attackInfoPacketIds": sorted_unique(
                    packet_id
                    for stream in variant.get("streams", [])
                    for packet_id in stream.get("attackInfoPacketIds", [])
                ),
            }
            for variant in selected
        ]
        return result

    pf127_ordinary_profile = classify_pf127_ordinary_profile_level(
        actor,
        level,
        profile,
        (profiles_by_archetype or {}).get(
            (actor.resource, actor.monster_data, actor.name),
            (),
        ),
        pf127_ordinary_profile_owners,
    )
    if pf127_ordinary_profile is not None:
        return pf127_ordinary_profile

    result["captureSessions"] = sorted_unique(profile.get("captureSessionsSearched", []))
    result["evidenceFound"] = unresolved_evidence_rows(
        profile, source_hint or configured_source
    )
    relevant_capture_variants = capture_certified_variants
    if source_hint is not None:
        exact_capture_variants = [
            variant
            for variant in capture_certified_variants
            if source_hint in variant.get("sourceIdentities", [])
        ]
        if exact_capture_variants:
            relevant_capture_variants = exact_capture_variants
    if not result["evidenceFound"] and relevant_capture_variants:
        result["evidenceFound"] = [
            {
                "observationType": (
                    "capture-certified-variant-not-runtime-ready"
                    if variant.get("runtimeContractReady") is not True
                    else "capture-certified-variant-for-other-source-generation"
                ),
                "sourceIdentities": sorted_unique(variant.get("sourceIdentities", [])),
                "captureSessions": sorted_unique(variant.get("captureSessions", [])),
                "runtimeContractReady": variant.get("runtimeContractReady") is True,
                "runtimeMissingEvidence": sorted_unique(
                    variant.get("runtimeMissingEvidence", [])
                ),
                "samplePacketIds": sorted_unique(
                    [
                        variant.get("representativeWifuPacketId"),
                        variant.get("representativeSawPacketId"),
                        variant.get("representativeAttackPacketId"),
                    ]
                    + [
                        packet_id
                        for stream in variant.get("streams", [])
                        for packet_id in stream.get("attackInfoPacketIds", [])[:1]
                    ]
                ),
                "excludedReason": (
                    "variant source identity does not match the runtime binding hint "
                    "and semantic fallback is not capture-proven"
                ),
            }
            for variant in relevant_capture_variants
        ]
    result["evidencePacketIds"] = sorted_unique(
        packet_id
        for observation in result["evidenceFound"]
        for packet_id in observation.get("samplePacketIds", [])
    )
    result["disabledGameplayCapability"] = profile.get("disabledCapability") or (
        "NPC auto-attack emission and damage application"
    )
    missing = observation_missing_fields(profile, source_hint or configured_source)
    not_ready_variants = [
        variant
        for variant in relevant_capture_variants
        if variant.get("runtimeContractReady") is not True
    ]
    if not_ready_variants:
        runtime_missing = sorted_unique(
            value
            for variant in not_ready_variants
            for value in variant.get("runtimeMissingEvidence", [])
        )
        if runtime_missing:
            missing.extend(runtime_missing)
            result["runtimeMissingEvidence"] = runtime_missing
        else:
            runtime_missing = [
                "runtime-ready contract data for timing, damage range, and mutable weapon state"
            ]
            missing.extend(runtime_missing)
            result["runtimeMissingEvidence"] = runtime_missing
    if source_hint is not None:
        missing.append(
            "runtime-ready capture-certified variant for the runtime source-identity hint"
        )
    else:
        missing.append(
            "runtime-ready capture-proven unique semantic fallback for a source-unbound runtime actor"
        )
    result["missingEvidence"] = sorted_unique(missing)
    result["unresolvedReason"] = (
        str(profile.get("status", "unresolved"))
        + ": no runtime-ready capture-certified binding satisfies the runtime selector without substitution"
    )
    return result


def build_non_denominator_audit_records(
    repo_root: Path,
    unmapped_profiles: Mapping[Tuple[int, int, str], Mapping[str, Any]],
    profiles_by_identity: Mapping[Tuple[int, int, int, str], Mapping[str, Any]],
    metadata_by_identity: Mapping[
        Tuple[int, int, int, str], Sequence[Mapping[str, Any]]
    ],
    fixed_profile_rows: Sequence[Mapping[str, Any]],
    searched_sessions: Sequence[str],
    searched_capture_ids: Sequence[str],
) -> Tuple[List[Dict[str, Any]], List[Dict[str, Any]]]:
    records: List[Dict[str, Any]] = []
    empty_metadata: Dict[Tuple[int, int, int, str], Sequence[Mapping[str, Any]]] = {}
    mission_binding_gap = (
        "runtime resolver binding from dynamic mission name, MonsterData, and level "
        "to a runtime-ready captured contract"
    )
    for definition in parse_dynamic_mission_profiles(repo_root):
        actor = make_actor(
            "dynamic-mission-mobs",
            -1,
            definition["name"],
            definition["monsterData"],
            definition["level"],
            definition["contentSources"][0],
            runtime_profile_selector="mission-instance-auto-aggro",
        )
        profile = unmapped_profiles.get(
            (definition["monsterData"], definition["level"], definition["name"])
        )
        profile_index: Dict[Tuple[int, int, int, str], Mapping[str, Any]] = {}
        if profile is not None:
            profile_index[
                (-1, definition["monsterData"], definition["level"], definition["name"])
            ] = profile
        coverage = classify_level(actor, definition["level"], profile_index, empty_metadata)
        captured_contract_ready = coverage.get("runtimeContractReady") is True
        missing = sorted_unique(
            list(coverage.get("missingEvidence", [])) + [mission_binding_gap]
        )
        runtime_missing = sorted_unique(
            list(coverage.get("runtimeMissingEvidence", [])) + [mission_binding_gap]
        )
        material = (
            f"dynamic-mission-mobs|{definition['name']}|{definition['monsterData']}|"
            f"{definition['level']}"
        )
        records.append(
            {
                "auditKey": hashlib.sha256(material.encode("utf-8")).hexdigest()[:20],
                "auditFamily": "dynamic-mission-mobs",
                "denominatorContribution": 0,
                "runtimeCardinality": "dynamic mission-instance shape selection",
                "runtimePlayfieldOrResource": "dynamic-mission-instance",
                "name": definition["name"],
                "monsterData": definition["monsterData"],
                "level": definition["level"],
                "sourceIdentity": None,
                "roles": definition["roles"],
                "configuredDefinitionCount": definition["configuredDefinitionCount"],
                "runtimeProfileSelector": "mission-instance-auto-aggro",
                "combatProfileKey": (
                    "resource=unmapped|md="
                    f"{definition['monsterData']}|level={definition['level']}|"
                    f"name={definition['name']}"
                ),
                "classification": "unresolved",
                "capturedContractDataRuntimeReady": captured_contract_ready,
                "runtimeBindingReady": False,
                "captureSearchScope": "corpusSearch.sessionsSearched",
                "captureSessions": coverage.get("captureSessions", []),
                "evidencePacketIds": coverage.get("evidencePacketIds", []),
                "evidenceFound": coverage.get("evidenceFound", []),
                "missingEvidence": missing,
                "runtimeMissingEvidence": runtime_missing,
                "unresolvedReason": (
                    "dynamic mission actors currently select one generic contract; the "
                    "runtime has no capture-proven exact profile resolver"
                ),
                "disabledGameplayCapability": (
                    "NPC auto-attack emission and damage application"
                ),
                "contentSources": definition["contentSources"],
            }
        )

    cleaning_names = {
        "Burning Cleaning Robot",
        "Cleaning Robot",
        "Cleanmeister Intelligence Robot",
        "Malfunctioning Cleaning Robot",
    }
    for profile_row in fixed_profile_rows:
        if (
            profile_row.get("monsterData") != 297023
            or profile_row.get("name") not in cleaning_names
        ):
            continue
        material = "cleaning-robots|" + str(profile_row["coverageKey"])
        records.append(
            {
                "auditKey": hashlib.sha256(material.encode("utf-8")).hexdigest()[:20],
                "auditFamily": "cleaning-robots",
                "denominatorContribution": 0,
                "denominatorExplanation": (
                    "supplemental family audit only; actor instances are already counted "
                    "in the fixed Arete surfaces"
                ),
                "fixedDenominatorCoverageKey": profile_row["coverageKey"],
                "fixedDenominatorActorCount": profile_row["actorCount"],
                "runtimeCardinality": "fixed initial slots with runtime respawn",
                "runtimePlayfieldOrResource": profile_row[
                    "runtimePlayfieldOrResource"
                ],
                "name": profile_row["name"],
                "monsterData": profile_row["monsterData"],
                "level": profile_row["level"],
                "levelCandidates": profile_row["levelCandidates"],
                "sourceIdentity": profile_row["configuredSourceIdentity"],
                "runtimeSourceIdentityHint": profile_row[
                    "runtimeSourceIdentityHint"
                ],
                "runtimeProfileSelector": profile_row["runtimeProfileSelector"],
                "classification": profile_row["classification"],
                "runtimeContractReady": profile_row["runtimeContractReady"],
                "captureSearchScope": profile_row["captureSearchScope"],
                "captureSessions": profile_row["captureSessions"],
                "evidencePacketIds": profile_row["evidencePacketIds"],
                "evidenceFound": profile_row["evidenceFound"],
                "missingEvidence": profile_row["missingEvidence"],
                "runtimeMissingEvidence": profile_row[
                    "runtimeMissingEvidence"
                ],
                "unresolvedReasons": profile_row["unresolvedReasons"],
                "disabledGameplayCapabilities": profile_row[
                    "disabledGameplayCapabilities"
                ],
                "contentSources": profile_row["contentSources"],
            }
        )

    elysium = parse_elysium_population_audit(repo_root)
    elysium_capture_ids = set(elysium["captureIds"])
    elysium_sessions = sorted_unique(
        session
        for session in searched_sessions
        if Path(session.replace("\\", "/")).name in elysium_capture_ids
    )
    unavailable_elysium_capture_ids = sorted(
        elysium_capture_ids - set(searched_capture_ids)
    )
    elysium_material = (
        "elysium-east-captured-population|"
        f"slots={elysium['slotCount']}|profiles={elysium['profileIdentityCount']}"
    )
    records.append(
        {
            "auditKey": hashlib.sha256(
                elysium_material.encode("utf-8")
            ).hexdigest()[:20],
            "auditFamily": "elysium-east-captured-population",
            "denominatorContribution": 0,
            "denominatorExplanation": (
                "pre-existing Elysium East/South content is outside this migration's "
                "fixed actor denominator; its Prepare callsite is audited structurally"
            ),
            "runtimeCardinality": "fixed initial slots with runtime respawn",
            "runtimePlayfieldOrResource": elysium["playfields"],
            "name": "Elysium East/South captured population",
            "monsterData": 0,
            "level": None,
            "sourceIdentity": None,
            "slotCount": elysium["slotCount"],
            "profileIdentityCount": elysium["profileIdentityCount"],
            "hecklerSlotCount": elysium["hecklerSlotCount"],
            "roles": ["captured fixed population"],
            "runtimeProfileSelector": "elysium-source-bounded-direct-contracts",
            "classification": "unresolved",
            "capturedContractDataRuntimeReady": False,
            "runtimeBindingReady": True,
            "captureSearchScope": "corpusSearch.sessionsSearched",
            "captureSessionCountSearched": len(searched_sessions),
            "matchingEvidenceSessionCount": len(elysium_sessions),
            "noMatchingEvidenceAfterExhaustiveSearch": not elysium_sessions,
            "captureSessions": elysium_sessions,
            "contentEvidenceCaptureIds": sorted(elysium_capture_ids),
            "unavailableContentEvidenceCaptureIds": unavailable_elysium_capture_ids,
            "evidenceFound": [
                {
                    "observationType": "runtime-source-captured-slot-population",
                    "slotCount": elysium["slotCount"],
                    "profileIdentityCount": elysium["profileIdentityCount"],
                    "playfields": elysium["playfields"],
                },
                {
                    "observationType": "runtime-source-combat-boundary",
                    "hecklerContractCaptureId": "20260727-190145",
                    "automaticAggroCaptureId": "20260727-193914",
                },
            ],
            "missingEvidence": [
                "canonical per-actor combat-profile certification for the complete Elysium East/South population"
            ],
            "runtimeMissingEvidence": [],
            "unresolvedReasons": [
                "the runtime source proves the fixed population and direct contract boundary, but this canonical combat inventory does not contain those Elysium capture sessions"
            ],
            "disabledGameplayCapabilities": [
                "active-coverage certification beyond the exact source-cited Elysium contract boundary"
            ],
            "contentSources": [elysium["path"]],
        }
    )

    if SCRIPTED_HOSTILE_CAPTURE_ID in searched_capture_ids:
        raise CoverageError(
            "Cursed Silvertail content capture is now present; replace its absent-citation "
            "audit with the recovered exact evidence before regenerating coverage"
        )
    scripted_actor = make_actor(
        "scripted-hostiles",
        4677,
        "Cursed Silvertail",
        208922,
        8,
        SCRIPTED_HOSTILE_SOURCE,
        runtime_profile_selector="thrak-garden-key-silvertail-transform",
        evidence_capture_ids=(SCRIPTED_HOSTILE_CAPTURE_ID,),
        notes=(
            "dynamic replacement for one of five Dreaming Silvertails; no fixed-denominator actor is added",
        ),
    )
    scripted_coverage = classify_level(
        scripted_actor,
        8,
        profiles_by_identity,
        metadata_by_identity,
    )
    scripted_missing = sorted_unique(
        [
            *scripted_coverage.get("missingEvidence", []),
            "owner-linked WeaponItemFullUpdate (WIFU) weapon definition",
            "source-local SpecialAttackWeapon packet",
            "source-local Attack packet",
            "source-local normal AttackInfo packet",
            "capture-backed maximum attack range",
            "runtime resolver binding to a capture-certified level-8 Cursed Silvertail contract",
            "referenced capture artifacts absent from corpus: "
            + SCRIPTED_HOSTILE_CAPTURE_ID,
        ]
    )
    scripted_runtime_missing = sorted_unique(
        [
            *scripted_coverage.get("runtimeMissingEvidence", []),
            "owner-linked WeaponItemFullUpdate (WIFU) weapon definition",
            "source-local SpecialAttackWeapon packet",
            "source-local Attack packet",
            "source-local normal AttackInfo packet",
            "capture-backed maximum attack range",
            "runtime resolver binding to a capture-certified level-8 Cursed Silvertail contract",
        ]
    )
    scripted_sessions = sorted_unique(scripted_coverage.get("captureSessions", []))
    scripted_material = "scripted-hostiles|Cursed Silvertail|208922|8"
    records.append(
        {
            "auditKey": hashlib.sha256(
                scripted_material.encode("utf-8")
            ).hexdigest()[:20],
            "auditFamily": "scripted-hostiles",
            "denominatorContribution": 0,
            "denominatorExplanation": (
                "dynamic replacement for one of five fixed Dreaming Silvertails; "
                "the Nascence Life actor denominator does not increase"
            ),
            "runtimeCardinality": "at most one replacement per scripted trade",
            "runtimePlayfieldOrResource": 4677,
            "name": "Cursed Silvertail",
            "monsterData": 208922,
            "level": 8,
            "sourceIdentity": None,
            "roles": ["scripted hostile replacement"],
            "runtimeProfileSelector": "thrak-garden-key-silvertail-transform",
            "combatProfileKey": (
                "resource=4677|md=208922|level=8|name=Cursed Silvertail"
            ),
            "classification": "unresolved",
            "capturedContractDataRuntimeReady": (
                scripted_coverage.get("runtimeContractReady") is True
            ),
            "runtimeBindingReady": False,
            "captureSearchScope": "corpusSearch.sessionsSearched",
            "captureSessionCountSearched": len(searched_sessions),
            "matchingEvidenceSessionCount": len(scripted_sessions),
            "noMatchingEvidenceAfterExhaustiveSearch": not scripted_sessions,
            "captureSessions": scripted_sessions,
            "evidencePacketIds": scripted_coverage.get("evidencePacketIds", []),
            "evidenceFound": scripted_coverage.get("evidenceFound", []),
            "missingEvidence": scripted_missing,
            "runtimeMissingEvidence": scripted_runtime_missing,
            "unresolvedReason": (
                "the referenced official capture artifacts are absent and the exhaustive "
                "corpus has no complete runtime-bindable Cursed Silvertail attack contract"
            ),
            "disabledGameplayCapability": (
                "NPC auto-attack emission and damage application"
            ),
            "contentSources": [SCRIPTED_HOSTILE_SOURCE],
            "contentEvidenceCaptureIds": [SCRIPTED_HOSTILE_CAPTURE_ID],
            "unavailableContentEvidenceCaptureIds": [SCRIPTED_HOSTILE_CAPTURE_ID],
        }
    )

    family_summaries: List[Dict[str, Any]] = []
    for family in (
        "dynamic-mission-mobs",
        "cleaning-robots",
        "elysium-east-captured-population",
        "scripted-hostiles",
    ):
        family_rows = [row for row in records if row["auditFamily"] == family]
        family_summaries.append(
            {
                "auditFamily": family,
                "recordCount": len(family_rows),
                "denominatorContribution": 0,
                "certified": sum(
                    1 for row in family_rows if row["classification"] == "certified"
                ),
                "unresolved": sum(
                    1 for row in family_rows if row["classification"] == "unresolved"
                ),
            }
        )
    if family_summaries[0]["recordCount"] != 155:
        raise CoverageError("dynamic mission non-denominator audit lost profile records")
    if family_summaries[1]["recordCount"] == 0:
        raise CoverageError("cleaning robot non-denominator audit found no profiles")
    if family_summaries[2]["recordCount"] != 1:
        raise CoverageError("Elysium East/South audit lost its captured population")
    if family_summaries[3]["recordCount"] != 1:
        raise CoverageError("scripted-hostile audit lost Cursed Silvertail")
    return records, family_summaries


def decode_json_text(raw: str) -> Any:
    decoder = object.__new__(json.JSONDecoder)
    decoder.object_hook = None
    decoder.parse_float = float
    decoder.parse_int = int
    decoder.parse_constant = json.decoder._CONSTANTS.__getitem__
    decoder.strict = True
    decoder.object_pairs_hook = None
    decoder.parse_object = json.decoder.JSONObject
    decoder.parse_array = json.decoder.JSONArray
    decoder.parse_string = json.decoder.py_scanstring
    decoder.memo = {}
    decoder.scan_once = json.scanner.py_make_scanner(decoder)
    start = 0
    while start < len(raw) and raw[start] in " \t\r\n":
        start += 1
    value, end = decoder.raw_decode(raw, start)
    while end < len(raw) and raw[end] in " \t\r\n":
        end += 1
    if end != len(raw):
        raise json.JSONDecodeError("Extra data", raw, end)
    return value


def load_json(
    path: Path,
    *,
    expected_sha256: Optional[str] = None,
    expected_byte_length: Optional[int] = None,
) -> Any:
    if (expected_sha256 is None) != (expected_byte_length is None):
        raise CoverageError("JSON input integrity descriptor is incomplete")
    payload = path.read_bytes()
    if expected_byte_length is not None and len(payload) != expected_byte_length:
        raise CoverageError(
            f"JSON input byte length mismatch: expected {expected_byte_length}, "
            f"found {len(payload)}"
        )
    if expected_sha256 is not None:
        actual_sha256 = hashlib.sha256(payload).hexdigest()
        if actual_sha256 != expected_sha256:
            raise CoverageError(
                f"JSON input SHA-256 mismatch: expected {expected_sha256}, "
                f"found {actual_sha256}"
            )
    return decode_json_text(payload.decode("utf-8"))


def build_inventory(
    repo_root: Path,
    combat_inventory_path: Path,
    formula_dataset_path: Optional[Path] = None,
    combat_inventory_descriptor_path: Optional[Path] = None,
    combat_inventory_sha256: Optional[str] = None,
    combat_inventory_byte_length: Optional[int] = None,
) -> Dict[str, Any]:
    actors = parse_all_actors(repo_root)
    merged = merge_actors(actors)
    combat_inventory = load_json(
        combat_inventory_path,
        expected_sha256=combat_inventory_sha256,
        expected_byte_length=combat_inventory_byte_length,
    )
    mathematical_bindings: Dict[
        Tuple[int, int, int, str, Optional[str]], Mapping[str, Any]
    ] = {}
    if formula_dataset_path is not None and formula_dataset_path.is_file():
        formula_dataset = load_json(formula_dataset_path)
        formulas = [
            formula_dataset.get("acceptedFormula", {}),
            formula_dataset.get("stimFiendFormula", {}),
            formula_dataset.get("filthFleaFormula", {}),
            formula_dataset.get("meldedPatternsFormula", {}),
            formula_dataset.get("fragmentedSoulFormula", {}),
            formula_dataset.get("incompleteRebuildFormula", {}),
            formula_dataset.get("molestedMoleculesFormula", {}),
            formula_dataset.get("fixedScopeSelectorBindings", {}),
            formula_dataset.get("templeOrdinaryCombatCompletion", {}),
            formula_dataset.get("finalOrdinaryDungeonCombatCompletion", {}),
        ]
        for formula in formulas:
            capture_sessions = sorted_unique(
                row.get("captureSession")
                for row in formula.get("rawPacketObservations", [])
            )
            evidence_packet_ids = sorted_unique(
                row.get("packetId")
                for row in formula.get("rawPacketObservations", [])
            )
            for binding in formula.get("activeBindings", []):
                identity = (
                    int(binding["resource"]),
                    int(binding["monsterData"]),
                    int(binding["level"]),
                    str(binding["name"]),
                    binding.get("configuredSourceIdentity"),
                )
                if identity in mathematical_bindings:
                    continue
                enriched_binding = dict(binding)
                enriched_binding["captureSessions"] = capture_sessions
                enriched_binding["evidencePacketIds"] = evidence_packet_ids
                mathematical_bindings[identity] = enriched_binding
    searched_sessions = sorted_unique(
        session.get("capture") for session in combat_inventory.get("sessions", [])
    )
    searched_capture_ids = {
        Path(session.replace("\\", "/")).name for session in searched_sessions
    }
    icc_shuttleport_entry_governance = discover_icc_shuttleport_entry_governance(
        repo_root
    )
    runtime_prepare_entry_points = discover_runtime_prepare_entry_points(repo_root)
    pf127_ordinary_profile_owners = discover_pf127_ordinary_profile_owners(
        repo_root
    )
    pf1931_profile_owners = discover_pf1931_profile_owners(repo_root)
    pf127_retaliation_resolver_source = read_source(
        repo_root,
        "AORebirth/Server/ZoneEngine/Core/Playfields/"
        "CapturedSubwayRetaliationEligibilityResolver.cs",
    )
    pf127_retaliation_source_binding_count = len(
        re.findall(
            r"\{\s*0x[0-9A-Fa-f]{8},\s*new\s+CapturedSubwayRetaliationBinding\(",
            pf127_retaliation_resolver_source,
        )
    )
    if pf127_retaliation_source_binding_count != 34:
        raise CoverageError(
            "PF127 exact retaliation resolver must contain 34 capture-backed "
            f"source bindings; found {pf127_retaliation_source_binding_count}"
        )
    profiles_by_identity: Dict[Tuple[int, int, int, str], Mapping[str, Any]] = {}
    profiles_by_archetype: Dict[
        Tuple[int, int, str], List[Mapping[str, Any]]
    ] = defaultdict(list)
    unmapped_profiles: Dict[Tuple[int, int, str], Mapping[str, Any]] = {}
    for profile in combat_inventory.get("profiles", []):
        unmapped_identity = parse_unmapped_combat_profile_key(profile["profileKey"])
        if unmapped_identity is not None:
            if unmapped_identity in unmapped_profiles:
                raise CoverageError(
                    f"duplicate unmapped combat profile identity: {profile['profileKey']}"
                )
            unmapped_profiles[unmapped_identity] = profile
            continue
        identity = parse_combat_profile_key(profile["profileKey"])
        if identity is None:
            continue
        if identity in profiles_by_identity:
            raise CoverageError(f"duplicate combat profile identity: {profile['profileKey']}")
        profiles_by_identity[identity] = profile
        profiles_by_archetype[(identity[0], identity[1], identity[3])].append(
            profile
        )

    realm_map = {
        int(key): int(value)
        for key, value in combat_inventory.get(
            "capturedRealmToRuntimeResource", {}
        ).items()
    }
    metadata_by_identity: Dict[
        Tuple[int, int, int, str], List[Mapping[str, Any]]
    ] = defaultdict(list)
    for metadata in combat_inventory.get("metadataGenerations", []):
        runtime_resource = realm_map.get(metadata.get("capturedRealmId"))
        if runtime_resource is None:
            continue
        identity = (
            runtime_resource,
            int(metadata.get("monsterData", 0)),
            int(metadata.get("level", 0)),
            str(metadata.get("name", "")),
        )
        metadata_by_identity[identity].append(metadata)

    profile_rows: List[Dict[str, Any]] = []
    profile_by_merge_key: Dict[Tuple[Any, ...], Dict[str, Any]] = {}
    surface_summary: Dict[str, Dict[str, int]] = {
        surface: {"actorCount": 0, "certified": 0, "unresolved": 0}
        for surface, _ in SURFACE_EXPECTATIONS
    }
    for actor in sorted(
        merged,
        key=lambda row: (
            [surface for surface, _ in SURFACE_EXPECTATIONS].index(row.surface),
            row.resource,
            row.name,
            row.monster_data,
            row.levels,
            row.configured_source_identity if row.configured_source_identity is not None else -1,
        ),
    ):
        variants = [
            classify_level(
                actor,
                level,
                profiles_by_identity,
                metadata_by_identity,
                mathematical_bindings,
                pf127_ordinary_profile_owners,
                pf1931_profile_owners,
                profiles_by_archetype,
            )
            for level in actor.levels
        ]
        classification = (
            "certified"
            if variants and all(row["classification"] == "certified" for row in variants)
            else "unresolved"
        )
        coverage_material = "|".join(
            (
                actor.surface,
                str(actor.resource),
                actor.name,
                str(actor.monster_data),
                ",".join(str(level) for level in actor.levels),
                format_identity(actor.configured_source_identity) or "",
                format_identity(actor.runtime_source_identity_hint) or "",
                actor.runtime_profile_selector,
                str(actor.runtime_attack_range_micrometers),
                (
                    str(actor.runtime_special_attack_weapon_unknown5)
                    if actor.runtime_special_attack_weapon_unknown5 is not None
                    else ""
                ),
            )
        )
        row = {
            "coverageKey": hashlib.sha256(coverage_material.encode("utf-8")).hexdigest()[:20],
            "surface": actor.surface,
            "runtimePlayfieldOrResource": actor.resource,
            "name": actor.name,
            "monsterData": actor.monster_data,
            "level": actor.levels[0] if len(actor.levels) == 1 else None,
            "levelCandidates": list(actor.levels),
            "configuredSourceIdentity": format_identity(actor.configured_source_identity),
            "runtimeSourceIdentityHint": format_identity(actor.runtime_source_identity_hint),
            "runtimeProfileSelector": actor.runtime_profile_selector or None,
            "runtimeAttackRangeMicrometers": actor.runtime_attack_range_micrometers or None,
            "runtimeSpecialAttackWeaponUnknown5": actor.runtime_special_attack_weapon_unknown5,
            "actorCount": actor.actor_count,
            "classification": classification,
            "runtimeContractReady": (
                classification == "certified"
                and all(variant.get("runtimeContractReady") is True for variant in variants)
            ),
            "levelCoverage": variants,
            "captureSessions": sorted_unique(
                session for variant in variants for session in variant.get("captureSessions", [])
            ),
            "evidencePacketIds": sorted_unique(
                packet
                for variant in variants
                for packet in variant.get("evidencePacketIds", [])
            ),
            "evidenceFound": [
                {
                    "level": variant["level"],
                    "observations": variant.get("evidenceFound", []),
                }
                for variant in variants
                if variant.get("evidenceFound")
            ],
            "missingEvidence": sorted_unique(
                missing
                for variant in variants
                for missing in variant.get("missingEvidence", [])
            ),
            "runtimeMissingEvidence": sorted_unique(
                missing
                for variant in variants
                for missing in variant.get("runtimeMissingEvidence", [])
            ),
            "unresolvedReasons": sorted_unique(
                variant.get("unresolvedReason") for variant in variants
            ),
            "disabledGameplayCapabilities": sorted_unique(
                variant.get("disabledGameplayCapability") for variant in variants
            ),
            "captureSearchScope": (
                "corpusSearch.sessionsSearched"
                if classification == "unresolved"
                else None
            ),
            "contentSources": list(actor.content_sources),
            "contentEvidenceCaptureIds": list(actor.content_evidence_capture_ids),
            "notes": list(actor.notes),
        }
        profile_rows.append(row)
        profile_by_merge_key[actor.merge_key()] = row
        summary = surface_summary[actor.surface]
        summary["actorCount"] += actor.actor_count
        summary[classification] += actor.actor_count

    source_paths = sorted(
        {
            source
            for actor in actors
            for source in actor.content_sources
        }
        | {
            "AORebirth/Server/ZoneEngine/Core/Missions/MissionInstanceMobCombat.cs",
            "AORebirth/Server/ZoneEngine/Core/Playfields/MissionInstanceShapeCatalog.cs",
            "AORebirth/Server/ZoneEngine/Core/Playfields/MissionInstanceSpawn.cs",
        }
        | {row["path"] for row in pf127_ordinary_profile_owners}
        | {row["path"] for row in pf1931_profile_owners}
        | {
            row["path"]
            for row in runtime_prepare_entry_points
            if row["governanceState"] == "ACCEPTED_COVERAGE"
        }
    )
    source_inputs = [
        {
            "path": source,
            "sha256": sha256_utf8_text_lf(repo_path(repo_root, source)),
            "hashNormalization": "utf8-sig-text-lf",
        }
        for source in source_paths
    ]
    summaries = []
    for surface, expected in SURFACE_EXPECTATIONS:
        summary = surface_summary[surface]
        if summary["actorCount"] != expected:
            raise CoverageError(
                f"post-classification surface count mismatch for {surface}: {summary}"
            )
        summaries.append({"surface": surface, **summary})

    totals = {
        "initialActorCount": sum(row["actorCount"] for row in summaries),
        "certified": sum(row["certified"] for row in summaries),
        "unresolved": sum(row["unresolved"] for row in summaries),
    }
    if totals["initialActorCount"] != EXPECTED_INITIAL_ACTORS:
        raise CoverageError(f"coverage output total mismatch: {totals}")
    if totals["certified"] + totals["unresolved"] != EXPECTED_INITIAL_ACTORS:
        raise CoverageError(f"coverage classification total mismatch: {totals}")

    binding_rows: List[Dict[str, Any]] = []
    binding_occurrences: Dict[str, int] = defaultdict(int)
    for actor in actors:
        profile_row = profile_by_merge_key.get(actor.merge_key())
        if profile_row is None:
            raise CoverageError(f"content binding has no coverage profile: {actor}")
        binding_material = "|".join(
            (
                profile_row["coverageKey"],
                ",".join(actor.content_sources),
                ",".join(actor.content_evidence_capture_ids),
            )
        )
        binding_occurrences[binding_material] += 1
        binding_ordinal = binding_occurrences[binding_material]
        binding_rows.append(
            {
                "bindingKey": hashlib.sha256(
                    f"{binding_material}|{binding_ordinal}".encode("utf-8")
                ).hexdigest()[:20],
                "coverageKey": profile_row["coverageKey"],
                "surface": actor.surface,
                "runtimePlayfieldOrResource": actor.resource,
                "name": actor.name,
                "monsterData": actor.monster_data,
                "level": actor.levels[0] if len(actor.levels) == 1 else None,
                "levelCandidates": list(actor.levels),
                "configuredSourceIdentity": format_identity(
                    actor.configured_source_identity
                ),
                "runtimeSourceIdentityHint": format_identity(
                    actor.runtime_source_identity_hint
                ),
                "runtimeProfileSelector": actor.runtime_profile_selector or None,
                "runtimeAttackRangeMicrometers": actor.runtime_attack_range_micrometers or None,
                "runtimeSpecialAttackWeaponUnknown5": actor.runtime_special_attack_weapon_unknown5,
                "actorCount": actor.actor_count,
                "classification": profile_row["classification"],
                "runtimeContractReady": profile_row["runtimeContractReady"],
                "contentSources": list(actor.content_sources),
                "contentEvidenceCaptureIds": list(
                    actor.content_evidence_capture_ids
                ),
            }
        )
    if sum(row["actorCount"] for row in binding_rows) != EXPECTED_INITIAL_ACTORS:
        raise CoverageError(
            "content binding rows no longer reconcile to the fixed initial actor count"
        )

    non_denominator_records, non_denominator_families = (
        build_non_denominator_audit_records(
            repo_root,
            unmapped_profiles,
            profiles_by_identity,
            metadata_by_identity,
            profile_rows,
            searched_sessions,
            searched_capture_ids,
        )
    )
    fixed_surface_names = {surface for surface, _ in SURFACE_EXPECTATIONS}
    non_denominator_family_names = {
        row["auditFamily"] for row in non_denominator_families
    }
    for entry_point in runtime_prepare_entry_points:
        audit_kind = entry_point["auditKind"]
        audit_references = set(entry_point["auditReferences"])
        if audit_kind == "fixed-denominator-surfaces":
            missing_references = audit_references - fixed_surface_names
        elif audit_kind == "non-denominator-audit":
            missing_references = audit_references - non_denominator_family_names
        elif audit_kind == "active-evidence":
            missing_references = audit_references - {
                "icc-shuttleport-entry-governance"
            }
        else:
            raise CoverageError(
                "unknown runtime Prepare audit kind for "
                f"{entry_point['path']}: {audit_kind}"
            )
        if missing_references:
            raise CoverageError(
                "runtime Prepare entry point cites missing audit coverage for "
                f"{entry_point['path']}: " + ", ".join(sorted(missing_references))
            )
    recoverable_runtime_binding_blockers = [
        {
            "auditKey": row["auditKey"],
            "name": row["name"],
            "monsterData": row["monsterData"],
            "level": row["level"],
            "captureSessions": row["captureSessions"],
            "evidencePacketIds": row["evidencePacketIds"],
            "missingRuntimeBinding": row["runtimeProfileSelector"],
        }
        for row in non_denominator_records
        if row.get("capturedContractDataRuntimeReady") is True
        and row.get("runtimeBindingReady") is not True
    ]
    if recoverable_runtime_binding_blockers:
        raise CoverageError(
            "non-denominator capture-ready contracts remain unbound: "
            + ", ".join(
                row["auditKey"] for row in recoverable_runtime_binding_blockers
            )
        )
    for row in profile_rows:
        if row["classification"] == "certified" and not row["runtimeContractReady"]:
            raise CoverageError(
                f"certified profile is not runtime-contract-ready: {row['coverageKey']}"
            )
        if row["classification"] == "unresolved" and not (
            row["missingEvidence"]
            and row["unresolvedReasons"]
            and row["disabledGameplayCapabilities"]
            and row["captureSearchScope"]
        ):
            raise CoverageError(
                f"unresolved profile lacks exact audit detail: {row['coverageKey']}"
            )
    if any(row["denominatorContribution"] != 0 for row in non_denominator_records):
        raise CoverageError("non-denominator audit changed the fixed actor denominator")

    for row in profile_rows:
        unknown_capture_ids = sorted(
            set(row["contentEvidenceCaptureIds"]) - searched_capture_ids
        )
        if unknown_capture_ids:
            # Content provenance is broader than the canonical combat-profile
            # extractor. A spawn/identity capture not indexed by that one dataset
            # does not invalidate an independently capture-certified combat profile.
            row["contentEvidenceCaptureIdsOutsideCombatInventory"] = (
                unknown_capture_ids
            )
            if row["classification"] == "unresolved":
                row["missingEvidence"] = sorted_unique(
                    [
                        *row["missingEvidence"],
                        "content provenance not indexed by the canonical combat inventory: "
                        + ", ".join(unknown_capture_ids),
                    ]
                )
                row["unresolvedReasons"] = sorted_unique(
                    [
                        *row["unresolvedReasons"],
                        "the canonical combat inventory does not independently index every cited content capture",
                    ]
                )
        if row["classification"] == "unresolved":
            row["captureSessionCountSearched"] = len(searched_sessions)
            row["matchingEvidenceSessionCount"] = len(row["captureSessions"])
            row["noMatchingEvidenceAfterExhaustiveSearch"] = (
                len(row["captureSessions"]) == 0
            )

    inventory_profiles = combat_inventory.get("profiles", [])
    certified_inventory_variants = [
        variant
        for profile in inventory_profiles
        for variant in profile.get("variants", [])
        if variant.get("captureCertified") is True
    ]
    migration_summary = {
        "priorCaptureCertifiedActorCount": 15,
        "newlyCaptureCertifiedActorCount": max(0, totals["certified"] - 15),
        "finalCaptureCertifiedActorCount": totals["certified"],
        "finalQuarantinedActorCount": totals["unresolved"],
        "fixedProfileRowCount": len(profile_rows),
        "fixedCertifiedProfileRowCount": sum(
            1 for row in profile_rows if row["classification"] == "certified"
        ),
        "fixedUnresolvedProfileRowCount": sum(
            1 for row in profile_rows if row["classification"] == "unresolved"
        ),
        "captureCertifiedCorpusProfileCount": combat_inventory["summary"][
            "captureCertifiedProfiles"
        ],
        "captureCertifiedSemanticDefinitionCount": combat_inventory["summary"][
            "captureCertifiedSemanticDefinitions"
        ],
        "runtimeReadyCorpusProfileCount": combat_inventory["summary"][
            "runtimeReadyProfiles"
        ],
        "exactRawWireObservationCount": sum(
            len(variant.get("rawWireVariantObservations", []))
            for variant in certified_inventory_variants
        ),
        "captureCertifiedVariantCountWithMutableSawState": sum(
            1
            for variant in certified_inventory_variants
            if len(
                {
                    row.get("unknown5")
                    for row in variant.get("mutableSawStateObservations", [])
                }
            )
            > 1
        ),
        "captureCertifiedVariantCountWithMutableWeaponState": sum(
            1
            for variant in certified_inventory_variants
            if len(variant.get("runtimeMutableWeaponStateCandidates", [])) > 1
        ),
        "remainingConflictedSourceIdentityCount": len(
            {
                source
                for profile in inventory_profiles
                for source in profile.get("conflictedSourceIdentities", [])
            }
        ),
        "restoredEnemyNames": sorted_unique(
            row["name"]
            for row in profile_rows
            if row["classification"] == "certified"
        ),
    }

    authoritative_inventory_path = (
        combat_inventory_descriptor_path or combat_inventory_path
    )
    return {
        "schemaVersion": 1,
        "generator": "tools-temp/AOSharpCaptureAnalyzer/generate_capture_backed_npc_active_coverage.py",
        "combatInventory": {
            "path": str(authoritative_inventory_path.relative_to(repo_root)).replace(
                "\\", "/"
            ),
            "sha256": sha256_file(authoritative_inventory_path),
            "schemaVersion": combat_inventory.get("schemaVersion"),
        },
        "contentInputs": source_inputs,
        "corpusSearch": {
            "scope": "all sessions scanned by the canonical combat inventory extractor",
            "sessionCount": len(searched_sessions),
            "sessionsSearched": searched_sessions,
            "authoritativeInputs": combat_inventory.get("authoritativeInputs", []),
        },
        "populationContract": {
            "expectedInitialActorCount": EXPECTED_INITIAL_ACTORS,
            "actualInitialActorCount": totals["initialActorCount"],
            "configuredMaximumActorCount": EXPECTED_INITIAL_ACTORS + 2,
            "maximumExplanation": "two Infector slots arm only after Abmouth combat and are not initial actors",
            "dynamicMissionMobs": "excluded from the fixed denominator because their count and identity are mission-instance data",
            "cursedSilvertail": "a dynamic replacement for one of five Dreaming Silvertails and does not increase the Nascence Life count",
        },
        "classificationRule": {
            "certified": "every runtime level candidate resolves through an exact runtime source-identity binding, a capture-proven unique semantic fallback, an exact mathematical setup, the production-owned PF127 ordinary-profile resolver, or an exact production-owned PF1931 capture contract guarded by packet-path tests",
            "unresolved": "at least one runtime level candidate lacks runtime-ready contract data or either safe resolver mode; absent runtimeContractReady is fail-closed",
            "configuredSourceIdentity": "official capture identity recorded by content, when defined",
            "runtimeSourceIdentityHint": "identity actually supplied to the generated runtime resolver; null means only a semantic fallback can resolve safely",
            "pf127OrdinaryProfile": "limited to subway.ordinary.* profiles plus the exact capture-proven Discarded Pet and Mugger supported selectors in resource 127, always with exact configured/runtime source identity; focused tests reproduce the production source-bound retaliation resolver and final TryResolve chain for every concrete variant",
            "pf1931CaptureContract": "limited to Deathless Legionnaire, the twelve explicit Temple named stages, and the two Reanimated Corpse slots in resource 1931; production owns the exact contract without captured runtime identity allocation and focused tests guard every packet path",
        },
        "totals": totals,
        "migrationSummary": migration_summary,
        "surfaces": summaries,
        "bindingTotals": {
            "bindingRecordCount": len(binding_rows),
            "actorCount": sum(row["actorCount"] for row in binding_rows),
        },
        "bindings": binding_rows,
        "profiles": profile_rows,
        "nonDenominatorAudit": {
            "denominatorContribution": 0,
            "recoverableRuntimeBindingBlockers": (
                recoverable_runtime_binding_blockers
            ),
            "families": non_denominator_families,
            "records": non_denominator_records,
        },
        "runtimePrepareAudit": {
            "productionRoot": RUNTIME_PREPARE_ROOT,
            "prepareCallPattern": (
                r"\bCapturedEnemyCombatRuntime\s*\.\s*Prepare\s*\("
            ),
            "entryPointFileCount": len(runtime_prepare_entry_points),
            "entryPointCount": sum(
                row["prepareCallCount"] for row in runtime_prepare_entry_points
            ),
            "acceptedCoverageEntryPointCount": sum(
                row["prepareCallCount"]
                for row in runtime_prepare_entry_points
                if row["governanceState"] == "ACCEPTED_COVERAGE"
            ),
            "activeEvidenceEntryPointCount": sum(
                row["prepareCallCount"]
                for row in runtime_prepare_entry_points
                if row["governanceState"] == "ACTIVE_EVIDENCE"
            ),
            "entries": runtime_prepare_entry_points,
        },
        "iccShuttleportEntryGovernance": icc_shuttleport_entry_governance,
        "pf127OrdinaryProfileResolverAudit": {
            "resolutionMode": PF127_ORDINARY_PROFILE_RESOLUTION_MODE,
            "surface": "subway-ordinary",
            "runtimePlayfieldOrResource": 127,
            "profileSelectorPrefix": PF127_ORDINARY_PROFILE_SELECTOR_PREFIX,
            "exactSupportedProfileSelectors": list(
                PF127_EXACT_SUPPORTED_PROFILE_SELECTORS
            ),
            "requiresExactConfiguredRuntimeSourceIdentity": True,
            "requiresEveryConcreteRuntimeVariantToResolveRetaliationEligible": True,
            "requiresEveryConcreteRuntimeVariantToResolveFinalCombatReady": True,
            "crossPlayfieldFallbackAllowed": False,
            "promotedCapability": "combat-contract ownership plus exact source-bound retaliation eligibility for supported selectors",
            "retaliationEligibilityPromotionAllowed": True,
            "exactRetaliationEligibilitySourceBindingCount": (
                pf127_retaliation_source_binding_count
            ),
            "automaticAggressionPolicyPromotionAllowed": False,
            "automaticCombatActivationPromotionAllowed": False,
            "owners": pf127_ordinary_profile_owners,
        },
        "pf1931CaptureContractResolverAudit": {
            "resolutionMode": PF1931_PROFILE_RESOLUTION_MODE,
            "runtimePlayfieldOrResource": 1931,
            "ordinaryProfileSelector": PF1931_DEATHLESS_PROFILE_SELECTOR,
            "namedSurface": "temple-named-encounters",
            "ownedAddSurface": "temple-reanimated-corpse-adds",
            "capturedRuntimeIdentityMappingAllowed": False,
            "crossPlayfieldFallbackAllowed": False,
            "automaticAggressionPolicyPromotionAllowed": False,
            "automaticCombatActivationPromotionAllowed": False,
            "owners": pf1931_profile_owners,
        },
        "fixedDenominatorExclusions": [
            "53 ICC HQ Social actors",
            "82 Arete Landing Social actors",
            "8 HoloDeck Social actors",
            "3 Windcaller Karrec Social actors",
            "1 Surveillance Droid Social actor",
            "KnuBot Perk-Reset provider",
            "attack-immune Greedy Desert Reet",
            "attack-immune Lolly the Crazed",
        ],
    }


def canonical_json(document: Mapping[str, Any]) -> str:
    return json.dumps(document, indent=2, sort_keys=True, ensure_ascii=False) + "\n"


def find_repo_root(start: Path) -> Path:
    current = start.resolve()
    for candidate in (current, *current.parents):
        if (candidate / "AI_START_HERE.md").is_file() and (candidate / ".git").exists():
            return candidate
    raise CoverageError("could not locate AORebirth repository root")


def same_file_or_path(left: Path, right: Path) -> bool:
    if left.resolve() == right.resolve():
        return True
    if os.path.lexists(left) and os.path.lexists(right):
        try:
            return os.path.samefile(left, right)
        except OSError:
            return False
    return False


def enter_governed_read_lease(
    checkout_root: Path, original_arguments: Sequence[str]
) -> int | None:
    delegation_name = "AO_REBIRTH_GENERATED_COMBAT_LEASE_DELEGATION"
    root_name = "AO_REBIRTH_GENERATED_COMBAT_LEASE_REPO_ROOT"
    raw_delegation = os.environ.get(delegation_name)
    raw_root = os.environ.get(root_name)
    if raw_delegation is None and raw_root is None:
        command = [
            sys.executable,
            str(checkout_root / "Tools" / "generated_combat_pipeline.py"),
            "--run-read-lease",
            "--",
            sys.executable,
            str(Path(__file__).resolve()),
            *original_arguments,
        ]
        return subprocess.run(command, cwd=checkout_root, check=False).returncode
    if raw_delegation is None or raw_root is None:
        raise CoverageError("generated-combat lease delegation is incomplete")
    lease_root = Path(raw_root).resolve(strict=True)
    if lease_root != checkout_root.resolve(strict=True):
        raise CoverageError(
            "generated-combat lease delegation belongs to a different checkout"
        )
    sys.path.insert(0, str(lease_root / "Tools"))
    try:
        import generated_artifact_transaction as transaction

        delegation = json.loads(raw_delegation)
        record = transaction.GeneratedArtifactLease.validate_delegation(
            lease_root, delegation
        )
    except Exception as error:
        raise CoverageError("generated-combat lease delegation is invalid") from error
    if record.get("domain") != "capture-backed-npc-combat":
        raise CoverageError("generated-combat lease delegation domain is invalid")
    return None


def main(argv: Optional[Sequence[str]] = None) -> int:
    original_arguments = list(sys.argv[1:] if argv is None else argv)
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--write", action="store_true", help="write the generated inventory")
    mode.add_argument("--check", action="store_true", help="verify the checked-in inventory")
    parser.add_argument("--repo-root", type=Path)
    parser.add_argument(
        "--combat-inventory",
        default="docs/generated/capture_backed_npc_combat_inventory.json",
    )
    parser.add_argument("--combat-inventory-descriptor")
    parser.add_argument("--combat-inventory-sha256")
    parser.add_argument("--combat-inventory-byte-length", type=int)
    parser.add_argument(
        "--output",
        default="docs/generated/capture_backed_npc_combat_active_coverage.json",
    )
    parser.add_argument(
        "--formula-dataset",
        default="docs/generated/enemy_combat_setup_formula_dataset.json",
    )
    args = parser.parse_args(original_arguments)

    script_repo_root = find_repo_root(Path(__file__).resolve().parent)
    repo_root = (
        args.repo_root.resolve()
        if args.repo_root is not None
        else script_repo_root
    )
    output_path = (repo_root / args.output).resolve()
    governed_output = (
        script_repo_root
        / "docs"
        / "generated"
        / "capture_backed_npc_combat_active_coverage.json"
    ).resolve()
    if same_file_or_path(output_path, governed_output):
        parser.error(
            "the governed active-coverage artifact must be checked or written "
            "through Tools/generated_combat_pipeline.py"
        )
    combat_inventory_path = repo_path(repo_root, args.combat_inventory)
    combat_inventory_descriptor_path = (
        repo_path(repo_root, args.combat_inventory_descriptor)
        if args.combat_inventory_descriptor
        else None
    )
    formula_dataset_path = repo_path(repo_root, args.formula_dataset)
    governed_inputs = (
        script_repo_root
        / "docs"
        / "generated"
        / "capture_backed_npc_combat_inventory.json",
        script_repo_root
        / "docs"
        / "generated"
        / "enemy_combat_setup_formula_dataset.json",
    )
    if any(
        same_file_or_path(candidate, governed)
        for candidate in (
            combat_inventory_path,
            formula_dataset_path,
            combat_inventory_descriptor_path,
        )
        if candidate is not None
        for governed in governed_inputs
    ):
        delegated_result = enter_governed_read_lease(
            script_repo_root, original_arguments
        )
        if delegated_result is not None:
            return delegated_result
    document = build_inventory(
        repo_root,
        combat_inventory_path,
        formula_dataset_path,
        combat_inventory_descriptor_path,
        args.combat_inventory_sha256,
        args.combat_inventory_byte_length,
    )
    rendered = canonical_json(document)

    if args.write:
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(rendered, encoding="utf-8", newline="\n")
        print(
            "WROTE "
            f"{format_generated_output_path(output_path, repo_root)} actors={document['totals']['initialActorCount']} "
            f"certified={document['totals']['certified']} unresolved={document['totals']['unresolved']} "
            f"ICC_ACCEPTED_ENTRIES={document['iccShuttleportEntryGovernance']['acceptedEntries']} "
            f"ICC_ACTIVE_EVIDENCE_ENTRIES={document['iccShuttleportEntryGovernance']['activeEvidenceEntries']} "
            f"ICC_BLOCKED_UNAUDITED_ENTRIES={document['iccShuttleportEntryGovernance']['blockedUnauditedEntries']}"
        )
        return 0

    if not output_path.is_file():
        print(f"ERROR: generated inventory is missing: {output_path}", file=sys.stderr)
        return 1
    existing = output_path.read_text(encoding="utf-8")
    if existing != rendered:
        print(
            "ERROR: active coverage inventory is stale; run this generator with --write",
            file=sys.stderr,
        )
        return 1
    print(
        "PASS "
        f"actors={document['totals']['initialActorCount']} "
        f"certified={document['totals']['certified']} unresolved={document['totals']['unresolved']} "
        f"ICC_ACCEPTED_ENTRIES={document['iccShuttleportEntryGovernance']['acceptedEntries']} "
        f"ICC_ACTIVE_EVIDENCE_ENTRIES={document['iccShuttleportEntryGovernance']['activeEvidenceEntries']} "
        f"ICC_BLOCKED_UNAUDITED_ENTRIES={document['iccShuttleportEntryGovernance']['blockedUnauditedEntries']}"
    )
    return 0


def format_generated_output_path(output_path: Path, repo_root: Path) -> str:
    """Render diagnostics without assuming staging is inside the worktree."""
    try:
        return output_path.relative_to(repo_root).as_posix()
    except ValueError:
        return "<external-staging>/" + output_path.name


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except CoverageError as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)
