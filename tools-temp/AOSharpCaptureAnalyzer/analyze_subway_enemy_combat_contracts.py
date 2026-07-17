#!/usr/bin/env python3
"""Aggregate capture-backed combat contracts for every observed Subway archetype."""

from __future__ import annotations

import csv
import json
import re
from collections import Counter, defaultdict
from datetime import datetime
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
CAPTURE_ROOT = REPO / "tools-temp" / "AOSharpLiveCapture" / "bin" / "Debug" / "captures"
CAPTURES = (
    "20260709-193914",
    "20260709-205921",
    "20260709-210452",
    "20260709-212115",
    "20260709-212336",
    "20260709-220439",
    "20260709-222339",
    "20260710-205400",
    "20260716-033326",
    "20260716-034104",
    "20260716-034559",
)
CAPTURE_ENEMY_FILTERS = {
    "20260716-034559": frozenset({"Melded Patterns"}),
}
# Melded Patterns captures include hits against the player's Healer pet. Keep
# player-facing retaliation and damage evidence restricted to the local player.
LOCAL_PLAYER_TARGET_ONLY_ENEMIES = frozenset({"Melded Patterns"})
OUTPUT = REPO / "docs" / "generated" / "subway_enemy_combat_contracts.json"

ATTACK_DETAIL = re.compile(
    r"WeaponSlot=(?P<slot>-?\d+).*Unk1=(?P<unknown>-?\d+).*WeaponInstance=(?P<instance>-?\d+)"
)
WEAPON_UPDATE = re.compile(
    r"type=WeaponItemFullUpdate identity=\(WeaponInstance:(?P<weapon>[0-9A-F]+)\).*"
    r"Owner=(?P<owner>\(SimpleChar:[0-9A-F]+\)).*"
    r"ACGItemLevel=(?P<quality>\d+).*ACGItemTemplateID=(?P<template>\d+)"
)
MONSTER_DATA_DETAIL = re.compile(r"\bmonsterData=(?P<monster_data>\d+)")


def read_csv(path: Path):
    if not path.exists():
        return []
    with path.open(newline="", encoding="utf-8-sig") as handle:
        return list(csv.DictReader(handle))


def parse_time(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def first_value(row: dict[str, str], *names: str) -> str:
    for name in names:
        value = row.get(name, "").strip()
        if value:
            return value
    return ""


def simple_char_identity(value: str) -> str:
    value = value.strip()
    if value.startswith("(SimpleChar:") and value.endswith(")"):
        return value
    if value.startswith("SimpleChar:"):
        return f"({value})"
    if re.fullmatch(r"[0-9A-Fa-f]{8}", value):
        return f"(SimpleChar:{value.upper()})"
    return ""


def add_identity(identities: dict[str, dict[str, object]], row: dict[str, str]):
    identity = simple_char_identity(
        first_value(
            row,
            "Identity",
            "NpcIdentity",
            "NPCIdentity",
            "EnemyIdentity",
            "CharacterIdentity",
            "PrimaryIdentity",
        )
    )
    name = first_value(row, "Name", "NpcName", "NPCName", "EnemyName")
    if not identity or not name or name.startswith("Remains of "):
        return
    monster_data_value = first_value(row, "MonsterData", "MonsterDataId")
    if not monster_data_value:
        match = MONSTER_DATA_DETAIL.search(row.get("Detail", ""))
        monster_data_value = match.group("monster_data") if match else ""
    monster_data = int(monster_data_value or 0)
    current = identities.get(identity)
    if current is None or (not current["monsterData"] and monster_data):
        identities[identity] = {
            "name": name,
            "monsterData": monster_data,
        }


def capture_includes_enemy(capture_name: str, enemy_name: str) -> bool:
    allowed_enemies = CAPTURE_ENEMY_FILTERS.get(capture_name)
    return allowed_enemies is None or enemy_name in allowed_enemies


def main():
    grouped = defaultdict(
        lambda: {
            "identities": set(),
            "captures": set(),
            "retaliationRows": 0,
            "attacks": [],
            "weapons": [],
            "monsterData": set(),
        }
    )

    for capture_name in CAPTURES:
        folder = CAPTURE_ROOT / capture_name
        identities = {}
        for row in read_csv(folder / "enemy-full-updates.csv"):
            add_identity(identities, row)
        for row in read_csv(folder / "npc-lifecycle.csv"):
            add_identity(identities, row)

        for row in read_csv(folder / "enemy-combat.csv"):
            source = row.get("SourceIdentity", "")
            enemy = identities.get(source)
            if (
                not enemy
                or row.get("SourceRole") != "enemy"
                or not capture_includes_enemy(capture_name, enemy["name"])
            ):
                continue
            message_type = row.get("MessageType")
            if (
                enemy["name"] in LOCAL_PLAYER_TARGET_ONLY_ENEMIES
                and message_type in {"Attack", "AttackInfo"}
                and row.get("TargetRole") != "local-player"
            ):
                continue
            group = grouped[enemy["name"]]
            group["identities"].add(source)
            group["captures"].add(capture_name)
            group["monsterData"].add(enemy["monsterData"])
            if message_type == "Attack":
                group["retaliationRows"] += 1
            if message_type != "AttackInfo":
                continue
            amount = int(row.get("Amount") or 0)
            detail = row.get("Detail", "")
            match = ATTACK_DETAIL.search(detail)
            if amount <= 0 or not match:
                continue
            group["attacks"].append(
                {
                    "capture": capture_name,
                    "identity": source,
                    "capturedUtc": row["CapturedUtc"],
                    "amount": amount,
                    "weaponSlot": int(match.group("slot")),
                    "attackInfoUnknown": int(match.group("unknown")),
                    "weaponInstance": int(match.group("instance")),
                }
            )

        events_path = folder / "events.log"
        if events_path.exists():
            for line in events_path.read_text(encoding="utf-8-sig", errors="replace").splitlines():
                match = WEAPON_UPDATE.search(line)
                if not match:
                    continue
                enemy = identities.get(match.group("owner"))
                if not enemy or not capture_includes_enemy(capture_name, enemy["name"]):
                    continue
                group = grouped[enemy["name"]]
                group["identities"].add(match.group("owner"))
                group["captures"].add(capture_name)
                group["monsterData"].add(enemy["monsterData"])
                group["weapons"].append(
                    {
                        "weaponIdentity": match.group("weapon"),
                        "templateId": int(match.group("template")),
                        "quality": int(match.group("quality")),
                    }
                )

    report = {}
    for name, group in sorted(grouped.items()):
        attacks = group["attacks"]
        intervals = []
        by_identity_shape = defaultdict(list)
        for attack in attacks:
            by_identity_shape[
                (
                    attack["capture"],
                    attack["identity"],
                    attack["weaponSlot"],
                    attack["attackInfoUnknown"],
                    attack["weaponInstance"],
                )
            ].append(parse_time(attack["capturedUtc"]))
        for times in by_identity_shape.values():
            times.sort()
            for previous, current in zip(times, times[1:]):
                seconds = (current - previous).total_seconds()
                if 0.5 <= seconds <= 10.0:
                    intervals.append(seconds)
        intervals.sort()
        attack_shapes = Counter(
            (row["weaponSlot"], row["attackInfoUnknown"], row["weaponInstance"])
            for row in attacks
        )
        weapon_shapes = Counter(
            (row["templateId"], row["quality"])
            for row in group["weapons"]
        )
        attack_shape_evidence = []
        for (shape_slot, shape_unknown, shape_instance), rows in sorted(
            attack_shapes.items(),
            key=lambda item: (-item[1], item[0]),
        ):
            matching = [
                row
                for row in attacks
                if (
                    row["weaponSlot"],
                    row["attackInfoUnknown"],
                    row["weaponInstance"],
                )
                == (shape_slot, shape_unknown, shape_instance)
            ]
            shape_intervals = []
            matching_by_identity = defaultdict(list)
            for row in matching:
                matching_by_identity[(row["capture"], row["identity"])].append(
                    parse_time(row["capturedUtc"])
                )
            for times in matching_by_identity.values():
                times.sort()
                for previous, current in zip(times, times[1:]):
                    seconds = (current - previous).total_seconds()
                    if 0.5 <= seconds <= 10.0:
                        shape_intervals.append(seconds)
            shape_intervals.sort()
            attack_shape_evidence.append(
                {
                    "weaponSlot": shape_slot,
                    "attackInfoUnknown": shape_unknown,
                    "weaponInstance": shape_instance,
                    "rows": rows,
                    "captures": sorted({row["capture"] for row in matching}),
                    "minDamage": min(row["amount"] for row in matching),
                    "maxDamage": max(row["amount"] for row in matching),
                    "intervalRows": len(shape_intervals),
                    "minIntervalSeconds": min(shape_intervals) if shape_intervals else None,
                    "medianIntervalSeconds": (
                        shape_intervals[(len(shape_intervals) - 1) // 2]
                        if shape_intervals
                        else None
                    ),
                    "maxIntervalSeconds": max(shape_intervals) if shape_intervals else None,
                }
            )
        slot, unknown, instance = attack_shapes.most_common(1)[0][0] if attack_shapes else (0, 0, 0)
        template_id, quality = weapon_shapes.most_common(1)[0][0] if weapon_shapes else (0, 0)
        report[name] = {
            "monsterData": sorted(group["monsterData"]),
            "captures": sorted(group["captures"]),
            "identities": sorted(group["identities"]),
            "retaliationObserved": group["retaliationRows"] > 0,
            "retaliationRows": group["retaliationRows"],
            "attackInfoObserved": bool(attacks),
            "attackInfoRows": len(attacks),
            "minDamage": min((row["amount"] for row in attacks), default=0),
            "maxDamage": max((row["amount"] for row in attacks), default=0),
            "medianRechargeSeconds": intervals[(len(intervals) - 1) // 2] if intervals else 0.0,
            "weaponSlot": slot,
            "attackInfoUnknown": unknown,
            "attackInfoWeaponInstance": instance,
            "attackShapes": attack_shape_evidence,
            "equippedWeaponObserved": bool(weapon_shapes),
            "equippedWeaponTemplateId": template_id,
            "equippedWeaponQuality": quality,
        }

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"archetypes={len(report)} output={OUTPUT}")


if __name__ == "__main__":
    main()
