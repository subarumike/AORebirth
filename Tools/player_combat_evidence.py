"""Reprocess direct player combat evidence without inventing combat statistics."""

from __future__ import annotations

import argparse
import csv
import json
import re
from datetime import datetime
from pathlib import Path
from typing import Any


SENTINEL = "1234567890"
PLAYER_COMBAT_FIELDS = {
    "MinDamage",
    "MaxDamage",
    "CriticalBonus",
    "AttackDelay",
    "RechargeDelay",
    "AttackRange",
}
ATTACK_INFO_PATTERN = re.compile(
    r"Amount=(?P<amount>-?\d+).*?Target=(?P<target>\([^)]*\))"
    r".*?HitType=(?P<hit_type>[^ ]+).*?WeaponInstance=(?P<weapon_instance>[^ ]+)"
)
SPECIAL_INFO_PATTERN = re.compile(
    r"EquipSlot=(?P<weapon_slot>-?\d+) Amount=(?P<amount>-?\d+)"
    r".*?Target=(?P<target>\([^)]*\)).*?Stat=(?P<stat>[^ ]+)"
)


def _unproven(value: str | None) -> bool:
    return value is None or not value.strip() or value.strip() == SENTINEL


def _profile_value(row: dict[str, str], field: str) -> str:
    value = row.get(field, "")
    return "UNPROVEN" if _unproven(value) else value


def _profile_source(row: dict[str, str], field: str) -> str:
    value = row.get(field, "")
    if not value.strip():
        return "missing"
    return "sentinel/default" if _unproven(value) else "runtime/client-state-observed"


def _parse_timestamp(line: str) -> str:
    return line.split(" ", 1)[0]


def _parse_raw_combat(path: Path, player_identity: str) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        if f"Identity={player_identity}" not in line or "IN-N3" not in line:
            continue

        timestamp = _parse_timestamp(line)
        sequence_match = re.search(r"#(?P<sequence>\d+)", line)
        sequence = int(sequence_match.group("sequence")) if sequence_match else None

        if "SpecialAttackInfoMessage {" in line:
            match = SPECIAL_INFO_PATTERN.search(line)
            if not match:
                continue
            stat = match.group("stat")
            rows.append(
                {
                    "timestamp": timestamp,
                    "sequence": sequence,
                    "message_type": "SpecialAttackInfo",
                    "target": match.group("target"),
                    "attack_kind": "Brawl" if stat.lower() == "brawl" else "Special",
                    "amount": int(match.group("amount")),
                    "hit_type": "Special",
                    "stat": stat,
                    "weapon_slot": int(match.group("weapon_slot")),
                    "weapon_instance": "UNPROVEN",
                    "ammo_count": _extract_int(line, "AmmoCount"),
                    "evidence_source": "direct-protocol-message",
                }
            )
            continue

        if "AttackInfoMessage {" in line:
            match = ATTACK_INFO_PATTERN.search(line)
            if not match:
                continue
            rows.append(
                {
                    "timestamp": timestamp,
                    "sequence": sequence,
                    "message_type": "AttackInfo",
                    "target": match.group("target"),
                    "attack_kind": "Normal",
                    "amount": int(match.group("amount")),
                    "hit_type": match.group("hit_type"),
                    "stat": "",
                    "weapon_slot": _extract_int(line, "WeaponSlot"),
                    "weapon_instance": match.group("weapon_instance"),
                    "ammo_count": _extract_int(line, "AmmoCount"),
                    "evidence_source": "direct-protocol-message",
                }
            )
    return [row for row in rows if row["amount"] > 0]


def _extract_int(line: str, name: str) -> int | None:
    match = re.search(rf"{re.escape(name)}=(-?\d+)", line)
    return int(match.group(1)) if match else None


def _parse_structured_combat(path: Path, player_identity: str) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    with path.open(newline="", encoding="utf-8-sig") as handle:
        for row in csv.DictReader(handle):
            if row.get("AttackerIdentity") != player_identity:
                continue
            try:
                amount = int(row.get("Amount", ""))
            except ValueError:
                continue
            if amount <= 0:
                continue
            attack_kind = row.get("AttackKind", "") or "UNRESOLVED"
            rows.append(
                {
                    "timestamp": row.get("CapturedUtc", ""),
                    "sequence": _as_int(row.get("Sequence")),
                    "message_type": row.get("MessageType", ""),
                    "target": row.get("TargetIdentity", ""),
                    "attack_kind": attack_kind,
                    "amount": amount,
                    "hit_type": row.get("HitType", ""),
                    "stat": row.get("Stat", ""),
                    "weapon_slot": _as_int(row.get("WeaponSlot")),
                    "weapon_instance": row.get("WeaponInstance", "") or "UNPROVEN",
                    "ammo_count": _as_int(row.get("AmmoCount")),
                    "monotonic_ticks": _as_int(row.get("MonotonicTicks")),
                    "monotonic_frequency": _as_int(row.get("MonotonicFrequency")),
                    "damage_type": row.get("DamageType", ""),
                    "damage_type_source": row.get("DamageTypeSource", ""),
                    "event_phase": row.get("EventPhase", ""),
                    "equipment_snapshot_id": row.get("EquipmentSnapshotId", ""),
                    "active_weapon_correlation": row.get("ActiveWeaponCorrelation", ""),
                    "player_position": [row.get("PlayerPositionX", ""), row.get("PlayerPositionY", ""), row.get("PlayerPositionZ", "")],
                    "target_position": [row.get("TargetPositionX", ""), row.get("TargetPositionY", ""), row.get("TargetPositionZ", "")],
                    "evidence_source": row.get("EvidenceSource", "direct-protocol-message"),
                }
            )
    return rows


def _as_int(value: str | None) -> int | None:
    try:
        return int(value) if value not in (None, "") else None
    except ValueError:
        return None


def _observed_intervals(rows: list[dict[str, Any]]) -> list[float]:
    monotonic_rows = [
        row
        for row in rows
        if row.get("monotonic_ticks") is not None
        and row.get("monotonic_frequency")
    ]
    if len(monotonic_rows) >= 2:
        monotonic_rows.sort(key=lambda row: int(row["monotonic_ticks"]))
        return [
            round(
                (right["monotonic_ticks"] - left["monotonic_ticks"])
                * 1000
                / right["monotonic_frequency"],
                3,
            )
            for left, right in zip(monotonic_rows, monotonic_rows[1:])
        ]

    timestamps: list[datetime] = []
    for row in rows:
        try:
            timestamps.append(datetime.fromisoformat(row["timestamp"].replace("Z", "+00:00")))
        except (TypeError, ValueError):
            continue
    return [round((right - left).total_seconds() * 1000, 3) for left, right in zip(timestamps, timestamps[1:])]


def _load_combat_states(capture_root: Path) -> list[dict[str, Any]]:
    path = capture_root / "player-combat-state.csv"
    if not path.exists():
        return []

    with path.open(newline="", encoding="utf-8-sig") as handle:
        return list(csv.DictReader(handle))


def _json_object(value: str | None, default: Any) -> Any:
    try:
        return json.loads(value or "")
    except (TypeError, ValueError, json.JSONDecodeError):
        return default


def _valid_state_stat(value: Any) -> bool:
    if not isinstance(value, dict):
        return False
    raw_value = str(value.get("rawValue", ""))
    return bool(raw_value.strip()) and raw_value != SENTINEL and value.get("status") == "observed"


def _state_stat_evidence(states: list[dict[str, Any]], field: str) -> dict[str, str]:
    candidates: list[dict[str, str]] = []
    for state in states:
        player_stats = _json_object(state.get("PlayerStatsJson"), {})
        player_value = player_stats.get(field)
        if _valid_state_stat(player_value) and not (field == "AttackRange" and str(player_value.get("rawValue")) == "0"):
            candidates.append(
                {
                    "value": str(player_value["rawValue"]),
                    "source": str(player_value.get("source", "runtime/client-state-observed")),
                }
            )

        for weapon in _json_object(state.get("ActiveWeaponsJson"), []):
            template_value = weapon.get("templateStats", {}).get(field)
            if _valid_state_stat(template_value) and not (field == "AttackRange" and str(template_value.get("rawValue")) == "0"):
                candidates.append(
                    {
                        "value": str(template_value["rawValue"]),
                        "source": str(template_value.get("source", "resolved-active-template")),
                    }
                )

    if candidates:
        return candidates[0]

    if field == "AttackRange":
        for state in states:
            value = state.get("AttackRangeRuntime", "")
            if value and value != SENTINEL and value != "0":
                return {"value": value, "source": state.get("AttackRangeSource", "runtime/client-state-observed")}
        return {"value": "UNPROVEN", "source": "runtime-zero-or-missing"}

    if field == "AttackSkill":
        return {"value": "UNPROVEN", "source": "no-accessible-AOSharp-Stat-enum-member"}
    if field == "DefenseSkill":
        return {"value": "UNPROVEN", "source": "no-observed-ItemOpposedSkill-source"}
    return {"value": "UNPROVEN", "source": "missing-or-sentinel"}


def _state_evidence(states: list[dict[str, Any]]) -> dict[str, Any]:
    if not states:
        return {
            "schemaVersion": None,
            "snapshotCount": 0,
        "playerMode": "UNRESOLVED",
            "activeWeapons": [],
            "naturalAttack": {},
            "provenance": "player-combat-state.csv missing",
        }

    active_weapons: list[dict[str, Any]] = []
    natural_modes = []
    for state in states:
        active_weapons.extend(_json_object(state.get("ActiveWeaponsJson"), []))
        if state.get("NaturalAttackMode"):
            natural_modes.append(state["NaturalAttackMode"])

    if active_weapons:
        player_mode = "WEAPON_ACTIVE"
    elif any("natural-unarmed" in mode for mode in natural_modes):
        player_mode = "UNARMED"
    else:
        player_mode = "UNRESOLVED"

    first = states[0]
    return {
        "schemaVersion": first.get("SchemaVersion"),
        "snapshotCount": len(states),
        "snapshotIds": [state.get("SnapshotId", "") for state in states],
        "playerMode": player_mode,
        "activeWeapons": active_weapons,
        "naturalAttack": {
            "mode": first.get("NaturalAttackMode", ""),
            "specialAttacks": first.get("NaturalSpecialAttacks", ""),
            "martialArts": first.get("MartialArts", ""),
            "unarmedTemplateInstance": first.get("UnarmedTemplateInstance", ""),
            "source": "AOSharp.LocalPlayer.Weapons/SimpleChar.SpecialAttacks/GetStat",
        },
        "provenance": first.get("Provenance", ""),
    }


def build_player_combat_evidence(capture_root: Path) -> dict[str, Any]:
    profile_path = capture_root / "player-profile.csv"
    with profile_path.open(newline="", encoding="utf-8-sig") as handle:
        profiles = list(csv.DictReader(handle))
    if not profiles:
        raise ValueError("player-profile.csv contains no rows")

    start_profile = next((row for row in profiles if row.get("Phase") == "capture-start"), profiles[0])
    end_profile = next((row for row in profiles if row.get("Phase") == "capture-end"), profiles[-1])
    player_identity = start_profile.get("Identity", "")
    if not player_identity:
        raise ValueError("player identity is missing from player-profile.csv")

    structured_path = capture_root / "player-combat.csv"
    if structured_path.exists():
        combat_rows = _parse_structured_combat(structured_path, player_identity)
        input_source = "player-combat.csv"
    else:
        combat_rows = _parse_raw_combat(capture_root / "enemy-fight-events.log", player_identity)
        input_source = "enemy-fight-events.log"

    normal_rows = [row for row in combat_rows if row["attack_kind"] == "Normal"]
    brawl_rows = [row for row in combat_rows if row["attack_kind"] == "Brawl"]
    normal_damage = [row["amount"] for row in normal_rows if row["hit_type"].lower() == "normal"]
    critical_damage = [row["amount"] for row in normal_rows if row["hit_type"].lower() == "critical"]
    brawl_damage = [row["amount"] for row in brawl_rows]

    combat_states = _load_combat_states(capture_root)
    state_evidence = _state_evidence(combat_states)
    profile_evidence = {
        field: _state_stat_evidence(combat_states, field)
        if combat_states
        else {"value": _profile_value(start_profile, field), "source": _profile_source(start_profile, field)}
        for field in ("MinDamage", "MaxDamage", "CriticalBonus", "AttackDelay", "RechargeDelay", "AttackRange", "DefaultAttackType", "DamageType1", "DamageType2", "AttackSkill", "DefenseSkill", "AmmoType", "ClipSize")
    }
    profile_evidence["AttackRange"] = _state_stat_evidence(combat_states, "AttackRange") if combat_states else {"value": "UNPROVEN", "source": "not-protocol-proven"}
    profile_evidence["EquippedWeapons"] = {
        "value": _profile_value(start_profile, "EquippedWeapons"),
        "source": "runtime/client-state-character-stat-only",
        "meaning": "character stat; not active attack weapon evidence",
    }

    weapon_instances = sorted({str(row["weapon_instance"]) for row in combat_rows})
    ammo_counts = sorted({row["ammo_count"] for row in combat_rows if row["ammo_count"] is not None})
    attack_mode = state_evidence["playerMode"]

    mandatory_unproven = [
        field
        for field in ("MinDamage", "MaxDamage", "AttackDelay", "RechargeDelay", "AttackRange")
        if profile_evidence[field]["value"] == "UNPROVEN"
    ]
    if attack_mode == "UNRESOLVED":
        mandatory_unproven.append("AttackMode")

    snapshot_ids = {row.get("equipment_snapshot_id", "") for row in combat_rows}
    known_snapshot_ids = set(state_evidence.get("snapshotIds", []))
    missing_snapshot_ids = sorted(snapshot_ids - known_snapshot_ids - {""})
    normal_hit_rows = [row for row in normal_rows if row.get("event_phase", "hit") == "hit"]
    remaining_unproven_fields = {
        "MinDamage": "No valid active natural/weapon template MinDamage source was captured.",
        "MaxDamage": "No valid active natural/weapon template MaxDamage source was captured.",
        "AttackDelay": "No valid active template/runtime AttackDelay source was captured.",
        "RechargeDelay": "No valid active template/runtime RechargeDelay source was captured.",
        "AttackRange": "No valid active template/natural/runtime AttackRange source was captured.",
    }
    remaining_unproven_fields = {
        field: reason
        for field, reason in remaining_unproven_fields.items()
        if profile_evidence[field]["value"] == "UNPROVEN"
    }
    if attack_mode == "UNRESOLVED":
        remaining_unproven_fields["AttackMode"] = "No authoritative active weapon or natural-attack state was captured."
    player_combat_complete = not mandatory_unproven

    capture_id = capture_root.name.split(" - ", 1)[-1].strip()
    evidence = {
        "captureId": capture_id,
        "captureRoot": str(capture_root),
        "inputSource": input_source,
        "player": {
            "identity": player_identity,
            "name": start_profile.get("Name", ""),
            "level": _as_int(start_profile.get("Level")),
            "breed": _as_int(start_profile.get("Breed")),
            "profession": _as_int(start_profile.get("Profession")),
            "maxHealth": _as_int(start_profile.get("MaxHealth")),
            "xpStart": _as_int(start_profile.get("XP")),
            "xpEnd": _as_int(end_profile.get("XP")),
        },
        "damage": {
            "damageEvents": len(combat_rows),
            "normalAttacks": len(normal_rows),
            "brawlAttacks": len(brawl_rows),
            "observedNormalDamageMin": min(normal_damage) if normal_damage else None,
            "observedNormalDamageMax": max(normal_damage) if normal_damage else None,
            "observedCriticalDamage": critical_damage,
            "observedBrawlDamage": brawl_damage,
            "totalObservedDamage": sum(row["amount"] for row in combat_rows),
            "events": combat_rows,
        },
        "statEvidence": profile_evidence,
        "packetEvidence": {
            "WeaponInstance": {"values": weapon_instances, "source": "direct-protocol-message"},
            "AmmoCount": {"values": ammo_counts, "source": "direct-protocol-message"},
            "observedAttackIntervalsMs": _observed_intervals(combat_rows),
            "observedNormalAttackIntervalsMs": _observed_intervals(normal_hit_rows),
            "attackDelay": profile_evidence["AttackDelay"]["value"],
            "rechargeDelay": profile_evidence["RechargeDelay"]["value"],
        },
        "attackMode": attack_mode,
        "combatState": state_evidence,
        "stateCorrelation": {
            "combatRowsWithSnapshot": sum(1 for row in combat_rows if row.get("equipment_snapshot_id")),
            "combatRowsWithoutSnapshot": sum(1 for row in combat_rows if not row.get("equipment_snapshot_id")),
            "missingSnapshotIds": missing_snapshot_ids,
            "chronological": not missing_snapshot_ids,
        },
        "playerCombatComplete": player_combat_complete,
        "governedPromotion": {
            "status": "READY_AUTHORITATIVE_PLAYER_COMBAT_FIELDS" if player_combat_complete else "BLOCKED_UNPROVEN_PLAYER_COMBAT_FIELDS",
            "mandatoryUnprovenFields": mandatory_unproven,
        },
        "remainingUnprovenFields": remaining_unproven_fields,
    }
    return evidence


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("capture_root", type=Path)
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    evidence = build_player_combat_evidence(args.capture_root)
    output = json.dumps(evidence, indent=2, sort_keys=True)
    if args.write:
        (args.capture_root / "player-combat-evidence.json").write_text(output + "\n", encoding="utf-8")
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
