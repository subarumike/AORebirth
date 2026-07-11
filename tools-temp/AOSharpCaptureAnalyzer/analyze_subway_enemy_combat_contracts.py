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
)
OUTPUT = REPO / "docs" / "generated" / "subway_enemy_combat_contracts.json"

ATTACK_DETAIL = re.compile(
    r"WeaponSlot=(?P<slot>-?\d+).*Unk1=(?P<unknown>-?\d+).*WeaponInstance=(?P<instance>-?\d+)"
)
WEAPON_UPDATE = re.compile(
    r"type=WeaponItemFullUpdate identity=\(WeaponInstance:(?P<weapon>[0-9A-F]+)\).*"
    r"Owner=(?P<owner>\(SimpleChar:[0-9A-F]+\)).*"
    r"ACGItemLevel=(?P<quality>\d+).*ACGItemTemplateID=(?P<template>\d+)"
)


def read_csv(path: Path):
    if not path.exists():
        return []
    with path.open(newline="", encoding="utf-8-sig") as handle:
        return list(csv.DictReader(handle))


def parse_time(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


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
            identities[row["Identity"]] = {
                "name": row["Name"],
                "monsterData": int(row["MonsterData"] or 0),
            }

        for row in read_csv(folder / "enemy-combat.csv"):
            source = row.get("SourceIdentity", "")
            enemy = identities.get(source)
            if not enemy or row.get("SourceRole") != "enemy":
                continue
            group = grouped[enemy["name"]]
            group["identities"].add(source)
            group["captures"].add(capture_name)
            group["monsterData"].add(enemy["monsterData"])
            if row.get("MessageType") == "Attack":
                group["retaliationRows"] += 1
            if row.get("MessageType") != "AttackInfo":
                continue
            amount = int(row.get("Amount") or 0)
            detail = row.get("Detail", "")
            match = ATTACK_DETAIL.search(detail)
            if amount <= 0 or not match:
                continue
            group["attacks"].append(
                {
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
                if not enemy:
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
        by_identity = defaultdict(list)
        for attack in attacks:
            by_identity[attack["identity"]].append(parse_time(attack["capturedUtc"]))
        for times in by_identity.values():
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
            "equippedWeaponObserved": bool(weapon_shapes),
            "equippedWeaponTemplateId": template_id,
            "equippedWeaponQuality": quality,
        }

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"archetypes={len(report)} output={OUTPUT}")


if __name__ == "__main__":
    main()
