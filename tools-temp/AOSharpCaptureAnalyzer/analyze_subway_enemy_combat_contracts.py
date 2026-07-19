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
    "20260708-004038",
    "20260708-143600",
    "20260709-193914",
    "20260709-205921",
    "20260709-210452",
    "20260709-212115",
    "20260709-212336",
    "20260709-213711",
    "20260709-220439",
    "20260709-222339",
    "20260709-225408",
    "20260710-202132",
    "20260710-205400",
    "20260710-211430",
    "20260716-033326",
    "20260716-034104",
    "20260716-034433",
    "20260716-034559",
    "20260716-034656",
    "20260716-220400",
    "20260716-221358",
    "20260716-222201",
    "20260717-214612",
    "20260717-214751",
    "20260717-215250",
)
CAPTURE_ENEMY_FILTERS = {
    "20260708-004038": frozenset({"Filth Flea"}),
    "20260708-143600": frozenset(
        {"Deranged Shopper", "Discarded Pet", "Disobedient Bot"}
    ),
    "20260709-213711": frozenset({"Architect Striker", "Workman Striker"}),
    "20260710-202132": frozenset({"Deranged Shopper"}),
    "20260716-034433": frozenset({"Vergil Aeneid"}),
    "20260716-034559": frozenset({"Melded Patterns"}),
    "20260716-034656": frozenset({"Slum Runner"}),
    "20260716-220400": frozenset({"Abmouth Supremus"}),
    "20260717-214612": frozenset({"Eumenides"}),
    "20260717-214751": frozenset({"Eumenides"}),
    "20260717-215250": frozenset({"Eumenides"}),
}
ENEMY_ATTACK_CAPTURE_FILTERS = {
    "Filth Flea": frozenset({"20260708-004038", "20260709-193914"}),
}
# Admit only the reviewed source from a broader capture whose other combat
# rows are outside the focused enemy evidence boundary.
REVIEWED_ATTACK_INFO_SOURCES = {
    ("Filth Flea", "20260709-205921"): frozenset(
        {"(SimpleChar:79531748)"}
    ),
}
# Keep local-player, player-owned-pet, and other-player targets separate.  Only
# local-player hits feed the player-facing aggregate used by runtime reviews.
TARGET_ROLE_EVIDENCE_ENEMIES = frozenset(
    {
        "Abmouth Supremus",
        "Architect Striker",
        "Strike Foreman",
        "Vergil Aeneid",
        "Workman Striker",
    }
)
PLAYER_OWNED_PET_TARGETS = {
    "20260709-213711": frozenset({"(SimpleChar:7953AE99)"}),
    "20260716-034433": frozenset({"(SimpleChar:796D400B)"}),
    "20260716-220400": frozenset(
        {"(SimpleChar:7970253A)", "(SimpleChar:7970253C)"}
    ),
}
OTHER_PLAYER_TARGETS = {
    "20260709-222339": frozenset({"(SimpleChar:794D8062)"}),
}
REVIEWED_EVENT_IDENTITIES = {
    "20260709-213711": {
        "(SimpleChar:7953AE99)": {
            "name": "Killer",
            "monsterData": 96195,
            "level": 13,
            "player": False,
            "npc": False,
            "pet": True,
        },
        "(SimpleChar:7953AFBC)": {
            "name": "Workman Striker",
            "monsterData": 203854,
            "level": 16,
            "player": False,
            "npc": True,
            "pet": False,
        },
        "(SimpleChar:7953AFDA)": {
            "name": "Architect Striker",
            "monsterData": 203743,
            "level": 15,
            "player": False,
            "npc": True,
            "pet": False,
        },
        "(SimpleChar:7953AFDD)": {
            "name": "Workman Striker",
            "monsterData": 203854,
            "level": 16,
            "player": False,
            "npc": True,
            "pet": False,
        },
    }
}
REVIEWED_RAW_TARGET_ROLE_PACKETS = {
    "20260709-222339": (
        {
            "capturedUtc": "2026-07-10T03:28:45.1013226Z",
            "sequence": 4771,
            "length": 38,
            "messageType": "Attack",
            "rawHex": "133D000A0001002600000DB97944C065284940700000C3507954512E000000C350794D806200",
            "source": "(SimpleChar:7954512E)",
            "target": "(SimpleChar:794D8062)",
            "targetRole": "otherPlayer",
        },
        {
            "capturedUtc": "2026-07-10T03:28:50.6684934Z",
            "sequence": 4870,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "13A0000A0001003D00000DB97944C06546002F160000C3507954512E000000002800000013000000060000C350794D8062000000000000000400000000",
            "source": "(SimpleChar:7954512E)",
            "target": "(SimpleChar:794D8062)",
            "targetRole": "otherPlayer",
            "amount": 40,
            "weaponSlot": 6,
            "attackInfoUnknown": 0,
            "hitType": "Critical",
            "weaponInstance": 0,
        },
        {
            "capturedUtc": "2026-07-10T03:28:55.5176374Z",
            "sequence": 4963,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "13FD000A0001003D00000DB97944C06546002F160000C3507954512E000000001200000012000000060000C350794D8062000000000000000300000000",
            "source": "(SimpleChar:7954512E)",
            "target": "(SimpleChar:794D8062)",
            "targetRole": "otherPlayer",
            "amount": 18,
            "weaponSlot": 6,
            "attackInfoUnknown": 0,
            "hitType": "Normal",
            "weaponInstance": 0,
        },
        {
            "capturedUtc": "2026-07-10T03:29:00.5184911Z",
            "sequence": 5107,
            "length": 61,
            "messageType": "AttackInfo",
            "rawHex": "148D000A0001003D00000DB97944C06546002F160000C3507954512E000000001200000011000000060000C350794D8062000000000000000300000000",
            "source": "(SimpleChar:7954512E)",
            "target": "(SimpleChar:794D8062)",
            "targetRole": "otherPlayer",
            "amount": 18,
            "weaponSlot": 6,
            "attackInfoUnknown": 0,
            "hitType": "Normal",
            "weaponInstance": 0,
        },
    )
}
CADENCE_UNRESOLVED_ENEMIES = frozenset({"Vergil Aeneid"})
OUTPUT = REPO / "docs" / "generated" / "subway_enemy_combat_contracts.json"

# These directories are overlapping projections of the same running official
# client.  The first tuple member is the canonical provenance.  A 20 ms bound
# covers the audited maximum 16.871 ms logger skew across 212115/212336 and
# the 17 ms Workman critical skew in 213711.  No other capture pair is eligible
# for cross-capture event deduplication.
OVERLAPPING_COMBAT_CAPTURE_RULES = {
    ("20260709-212115", "20260709-212336"): 0.020,
    ("20260709-212115", "20260709-213711"): 0.020,
}

ATTACK_DETAIL = re.compile(
    r"WeaponSlot=(?P<slot>-?\d+).*Unk1=(?P<unknown>-?\d+).*"
    r"HitType=(?P<hit_type>\w+).*WeaponInstance=(?P<instance>-?\d+)"
)
ATTACK_TARGET_DETAIL = re.compile(
    r"\bTarget=(?P<target>\(SimpleChar:[0-9A-F]+\))"
)
MESSAGE_IDENTITY_DETAIL = re.compile(
    r"\bIdentity=(?P<identity>\(SimpleChar:[0-9A-F]+\))"
)
WEAPON_UPDATE = re.compile(
    r"WeaponItemFullUpdateMessage .*"
    r"Owner=(?P<owner>\(SimpleChar:[0-9A-F]+\)).*"
    r"ACGItemLevel=(?P<quality>\d+).*ACGItemTemplateID=(?P<template>\d+).*"
    r"ACGItemTemplateID2=(?P<template2>\d+).*"
    r"Identity=\(WeaponInstance:(?P<weapon>[0-9A-F]+)\)"
)
MISSED_ATTACK_INFO = re.compile(
    r"^(?P<captured_utc>\S+).*MissedAttackInfoMessage .*"
    r"Unknown1=(?P<ammo>-?\d+).*Unknown2=(?P<slot>-?\d+).*"
    r"Attacker=(?P<attacker>\(SimpleChar:[0-9A-F]+\)).*"
    r"Defender=(?P<defender>\(SimpleChar:[0-9A-F]+\)).*"
    r"Unknown3=(?P<unknown>-?\d+)"
)
MONSTER_DATA_DETAIL = re.compile(r"\bmonsterData=(?P<monster_data>\d+)")
CHARACTER_EVENT = re.compile(
    r"\[(?:CHAR-SEEN|DYNEL-SPAWNED)\] "
    r"identity=(?P<identity>\(SimpleChar:[0-9A-F]+\)) "
    r"name=(?P<name>.*?) "
    r"player=(?P<player>True|False) npc=(?P<npc>True|False) "
    r"pet=(?P<pet>True|False).*?\blevel=(?P<level>\d+).*?"
    r"\bmonsterData=(?P<monster_data>\d+)"
)


def read_csv(path: Path):
    if not path.exists():
        return []
    with path.open(newline="", encoding="utf-8-sig") as handle:
        return list(csv.DictReader(handle))


def read_json(path: Path):
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8-sig"))


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
            "identity",
        )
    )
    name = first_value(row, "Name", "NpcName", "NPCName", "EnemyName", "name")
    if not identity or not name or name.startswith("Remains of "):
        return
    monster_data_value = first_value(
        row, "MonsterData", "MonsterDataId", "monsterData"
    )
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


def add_reviewed_event_identities(
    capture_name: str,
    events_path: Path,
    identities: dict[str, dict[str, object]],
) -> None:
    reviewed = REVIEWED_EVENT_IDENTITIES.get(capture_name, {})
    if not reviewed or not events_path.exists():
        return
    for line in events_path.read_text(
        encoding="utf-8-sig", errors="replace"
    ).splitlines():
        match = CHARACTER_EVENT.search(line)
        if not match:
            continue
        identity = match.group("identity")
        expected = reviewed.get(identity)
        if expected is None:
            continue
        observed = {
            "name": match.group("name"),
            "monsterData": int(match.group("monster_data")),
            "level": int(match.group("level")),
            "player": match.group("player") == "True",
            "npc": match.group("npc") == "True",
            "pet": match.group("pet") == "True",
        }
        if observed != expected or expected["pet"] or expected["player"]:
            continue
        identities[identity] = {
            "name": expected["name"],
            "monsterData": expected["monsterData"],
        }


def capture_includes_enemy(capture_name: str, enemy_name: str) -> bool:
    allowed_enemies = CAPTURE_ENEMY_FILTERS.get(capture_name)
    return allowed_enemies is None or enemy_name in allowed_enemies


def attack_evidence(
    row: dict[str, str],
    capture_name: str,
    source: str,
    provenance_captures: set[str] | None = None,
):
    amount = int(row.get("Amount") or 0)
    detail = row.get("Detail", "")
    match = ATTACK_DETAIL.search(detail)
    if amount <= 0 or not match:
        return None
    return {
        "capture": capture_name,
        "identity": source,
        "capturedUtc": row["CapturedUtc"],
        "amount": amount,
        "weaponSlot": int(match.group("slot")),
        "attackInfoUnknown": int(match.group("unknown")),
        "hitType": match.group("hit_type"),
        "weaponInstance": int(match.group("instance")),
        "provenanceCaptures": provenance_captures or {capture_name},
    }


def target_evidence_role(capture_name: str, row: dict[str, str]) -> str:
    if row.get("TargetRole") == "local-player":
        return "localPlayer"
    if row.get("TargetIdentity") in PLAYER_OWNED_PET_TARGETS.get(
        capture_name, frozenset()
    ):
        return "playerOwnedPet"
    if row.get("TargetIdentity") in OTHER_PLAYER_TARGETS.get(
        capture_name, frozenset()
    ):
        return "otherPlayer"
    return ""


def combat_event_fingerprint(
    row: dict[str, str], evidence_role: str
) -> tuple[object, ...] | None:
    message_type = row.get("MessageType", "")
    if message_type not in {"Attack", "AttackInfo"}:
        return None
    source = simple_char_identity(row.get("SourceIdentity", ""))
    if not source:
        return None
    target = simple_char_identity(row.get("TargetIdentity", ""))
    if not target:
        target_match = ATTACK_TARGET_DETAIL.search(row.get("Detail", ""))
        target = target_match.group("target") if target_match else ""
    target_role = evidence_role or row.get("TargetRole", "")
    if message_type == "AttackInfo":
        detail_match = ATTACK_DETAIL.search(row.get("Detail", ""))
        amount = int(row.get("Amount") or 0)
        if detail_match is None or amount <= 0:
            return None
        hit_shape = (
            int(detail_match.group("slot")),
            int(detail_match.group("unknown")),
            detail_match.group("hit_type"),
            int(detail_match.group("instance")),
        )
    else:
        amount = int(row.get("Amount") or 0)
        hit_shape = (0, 0, "", 0)
    return (source, target, target_role, message_type, amount, *hit_shape)


class OverlappingCombatEventDeduplicator:
    """One-to-one dedup across only the audited overlapping capture pairs."""

    def __init__(self) -> None:
        capture_order = {capture: index for index, capture in enumerate(CAPTURES)}
        for canonical, duplicate in OVERLAPPING_COMBAT_CAPTURE_RULES:
            if capture_order[canonical] >= capture_order[duplicate]:
                raise ValueError(
                    "combat overlap canonical capture order drifted: "
                    + canonical
                    + " -> "
                    + duplicate
                )
        self._events = defaultdict(list)
        self._ordinal = 0
        self.exclusions = []

    def observe(
        self,
        capture_name: str,
        row: dict[str, str],
        evidence_role: str,
    ) -> tuple[dict[str, object] | None, bool]:
        fingerprint = combat_event_fingerprint(row, evidence_role)
        captured_utc = row.get("CapturedUtc", "")
        if fingerprint is None or not captured_utc:
            return None, False
        captured_time = parse_time(captured_utc)
        candidates = []
        for canonical in self._events[fingerprint]:
            tolerance = OVERLAPPING_COMBAT_CAPTURE_RULES.get(
                (canonical["capture"], capture_name)
            )
            if (
                tolerance is None
                or capture_name in canonical["provenanceCaptures"]
            ):
                continue
            delta = abs((captured_time - canonical["capturedTime"]).total_seconds())
            if delta <= tolerance:
                candidates.append((delta, canonical["ordinal"], canonical))
        if candidates:
            _, _, canonical = min(candidates, key=lambda item: (item[0], item[1]))
            canonical["provenanceCaptures"].add(capture_name)
            self.exclusions.append(
                {
                    "canonicalCapture": canonical["capture"],
                    "duplicateCapture": capture_name,
                    "canonicalCapturedUtc": canonical["capturedUtc"],
                    "duplicateCapturedUtc": captured_utc,
                    "fingerprint": fingerprint,
                }
            )
            return canonical, True
        event = {
            "capture": capture_name,
            "capturedUtc": captured_utc,
            "capturedTime": captured_time,
            "ordinal": self._ordinal,
            "provenanceCaptures": {capture_name},
        }
        self._ordinal += 1
        self._events[fingerprint].append(event)
        return event, False

    def validate(self) -> None:
        for exclusion in self.exclusions:
            pair = (
                exclusion["canonicalCapture"],
                exclusion["duplicateCapture"],
            )
            if pair not in OVERLAPPING_COMBAT_CAPTURE_RULES:
                raise ValueError(
                    "unreviewed capture pair reached combat dedup: "
                    + " -> ".join(pair)
                )


def validate_workman_striker_distinct_combat(
    attacks: list[dict[str, object]],
    report_entry: dict[str, object],
) -> None:
    expected_counts = {
        "attackInfoRows": 53,
        "normalAttackInfoRows": 47,
        "criticalAttackInfoRows": 6,
    }
    for field, expected in expected_counts.items():
        if report_entry[field] != expected:
            raise ValueError(
                f"Workman Striker distinct {field} drifted: "
                f"expected={expected} actual={report_entry[field]}"
            )
    if report_entry["medianRechargeSeconds"] != 5.092328:
        raise ValueError("Workman Striker distinct cadence drifted")
    shapes = report_entry["attackShapes"]
    if (
        len(shapes) != 1
        or shapes[0]["rows"] != 53
        or shapes[0]["intervalRows"] != 41
        or shapes[0]["medianIntervalSeconds"] != 5.092328
    ):
        raise ValueError("Workman Striker distinct attack-shape cadence drifted")
    canonical_critical = [
        row
        for row in attacks
        if row["capture"] == "20260709-212115"
        and row["identity"] == "(SimpleChar:7953AFBC)"
        and row["capturedUtc"] == "2026-07-10T02:37:39.1002433Z"
        and row["hitType"] == "Critical"
        and row["amount"] == 42
    ]
    if (
        len(canonical_critical) != 1
        or canonical_critical[0]["provenanceCaptures"]
        != {"20260709-212115", "20260709-213711"}
    ):
        raise ValueError("Workman Striker critical canonical provenance drifted")
    unrelated_rows = [
        row for row in attacks if row["capture"] == "20260709-220439"
    ]
    if (
        Counter(row["hitType"] for row in unrelated_rows)
        != Counter({"Normal": 14, "Critical": 2})
        or any(
            row["provenanceCaptures"] != {"20260709-220439"}
            for row in unrelated_rows
        )
    ):
        raise ValueError("non-overlapping Workman Striker combat rows changed")


def validate_discarded_pet_combat(report_entry: dict[str, object]) -> None:
    required_captures = {"20260708-143600", "20260709-210452"}
    if not required_captures.issubset(set(report_entry["captures"])):
        raise ValueError("Discarded Pet focused combat captures are missing")
    if (
        report_entry["normalAttackInfoRows"] != 37
        or report_entry["normalMinDamage"] != 9
        or report_entry["normalMaxDamage"] != 18
        or report_entry["criticalAttackInfoRows"] != 4
        or report_entry["criticalMinDamage"] != 30
        or report_entry["criticalMaxDamage"] != 33
        or report_entry["weaponSlot"] != 0
        or report_entry["attackInfoUnknown"] != 0
        or report_entry["attackInfoWeaponInstance"] != 0x53495731
    ):
        raise ValueError("Discarded Pet SIW1 local-player evidence drifted")
    shapes = report_entry["attackShapes"]
    if (
        len(shapes) != 1
        or shapes[0]["rows"] != 41
        or shapes[0]["intervalRows"] != 25
        or shapes[0]["minIntervalSeconds"] != 4.609299
        or shapes[0]["medianIntervalSeconds"] != 5.079568
        or shapes[0]["maxIntervalSeconds"] != 5.950416
    ):
        raise ValueError("Discarded Pet SIW1 local-player cadence drifted")


def add_reviewed_raw_target_role_evidence(
    capture_name: str,
    folder: Path,
    identities: dict[str, dict[str, object]],
    grouped,
    derived_source_keys: set[tuple[str, str]],
) -> None:
    reviewed = REVIEWED_RAW_TARGET_ROLE_PACKETS.get(capture_name, ())
    packets_path = folder / "packets.hex.log"
    if not reviewed or not packets_path.exists():
        return
    raw_lines = set(
        packets_path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
    )
    for packet in reviewed:
        source = packet["source"]
        enemy = identities.get(source)
        if (
            not enemy
            or enemy["name"] != "Strike Foreman"
            or enemy["monsterData"] != 203744
            or (source, packet["messageType"]) in derived_source_keys
        ):
            continue
        expected_line = (
            f"{packet['capturedUtc']} IN #{packet['sequence']} "
            f"len={packet['length']} n3={packet['messageType']} "
            f"hex={packet['rawHex']}"
        )
        if expected_line not in raw_lines:
            continue
        group = grouped[enemy["name"]]
        group["identities"].add(source)
        group["captures"].add(capture_name)
        group["monsterData"].add(enemy["monsterData"])
        role_evidence = group["targetRoleEvidence"][packet["targetRole"]]
        role_evidence["captures"].add(capture_name)
        role_evidence["targetIdentities"].add(packet["target"])
        if packet["messageType"] == "Attack":
            role_evidence["retaliationRows"] += 1
            continue
        role_evidence["attacks"].append(
            {
                "capture": capture_name,
                "identity": source,
                "capturedUtc": packet["capturedUtc"],
                "amount": packet["amount"],
                "weaponSlot": packet["weaponSlot"],
                "attackInfoUnknown": packet["attackInfoUnknown"],
                "hitType": packet["hitType"],
                "weaponInstance": packet["weaponInstance"],
                "provenanceCaptures": {capture_name},
            }
        )


def main():
    grouped = defaultdict(
        lambda: {
            "identities": set(),
            "captures": set(),
            "retaliationRows": 0,
            "attacks": [],
            "misses": [],
            "weapons": [],
            "weaponKeys": set(),
            "specialAttackWeapons": [],
            "monsterData": set(),
            "targetRoleEvidence": defaultdict(
                lambda: {
                    "captures": set(),
                    "targetIdentities": set(),
                    "retaliationRows": 0,
                    "attacks": [],
                }
            ),
        }
    )
    combat_deduplicator = OverlappingCombatEventDeduplicator()

    for capture_name in CAPTURES:
        folder = CAPTURE_ROOT / capture_name
        events_path = folder / "events.log"
        identities = {}
        for row in read_csv(folder / "enemy-full-updates.csv"):
            add_identity(identities, row)
        for row in read_csv(folder / "npc-lifecycle.csv"):
            add_identity(identities, row)
        for row in read_json(folder / "enemy-dossier.json").get("enemies", []):
            add_identity(identities, row)
        for row in read_csv(folder / "corpse-full-updates.csv"):
            corpse_name = first_value(row, "DeadNpcName", "CorpseName")
            if corpse_name.startswith("Remains of "):
                corpse_name = corpse_name[len("Remains of ") :]
            add_identity(
                identities,
                {
                    "Identity": first_value(
                        row, "DeadNpcIdentity", "TailDeadNpcIdentity"
                    ),
                    "Name": corpse_name,
                    "MonsterData": first_value(row, "CorpseMonsterData"),
                },
            )
        add_reviewed_event_identities(capture_name, events_path, identities)

        derived_source_keys = set()
        combat_rows = read_csv(folder / "enemy-combat.csv")
        local_player_identities = set()
        for row in combat_rows:
            detail = row.get("Detail", "")
            if row.get("SourceRole") == "local-player":
                identity_match = MESSAGE_IDENTITY_DETAIL.search(detail)
                if identity_match:
                    local_player_identities.add(identity_match.group("identity"))
            if row.get("TargetRole") == "local-player":
                target_match = ATTACK_TARGET_DETAIL.search(detail)
                if target_match:
                    local_player_identities.add(target_match.group("target"))

        for row in combat_rows:
            source = row.get("SourceIdentity", "")
            enemy = identities.get(source)
            if (
                not enemy
                or row.get("SourceRole") != "enemy"
                or not capture_includes_enemy(capture_name, enemy["name"])
            ):
                continue
            message_type = row.get("MessageType")
            derived_source_keys.add((source, message_type))
            group = grouped[enemy["name"]]
            group["identities"].add(source)
            group["captures"].add(capture_name)
            group["monsterData"].add(enemy["monsterData"])
            if message_type == "SpecialAttackWeapon":
                group["specialAttackWeapons"].append(
                    {
                        "capture": capture_name,
                        "identity": source,
                        "capturedUtc": row.get("CapturedUtc", ""),
                        "unknown1": int(row.get("Unknown1") or 0),
                        "unknown2": int(row.get("Unknown2") or 0),
                        "unknown3": int(row.get("Unknown3") or 0),
                        "unknown4": int(row.get("Unknown4") or 0),
                        "unknown5": int(row.get("Unknown5") or 0),
                    }
                )
            reviewed_attack_sources = REVIEWED_ATTACK_INFO_SOURCES.get(
                (enemy["name"], capture_name), frozenset()
            )
            attack_info_allowed = (
                message_type != "AttackInfo"
                or capture_name
                in ENEMY_ATTACK_CAPTURE_FILTERS.get(
                    enemy["name"], frozenset({capture_name})
                )
                or source in reviewed_attack_sources
            )
            evidence_role = ""
            role_evidence = None
            if (
                enemy["name"] in TARGET_ROLE_EVIDENCE_ENEMIES
                and message_type in {"Attack", "AttackInfo"}
            ):
                evidence_role = target_evidence_role(capture_name, row)
                if evidence_role:
                    role_evidence = group["targetRoleEvidence"][evidence_role]
                    role_evidence["captures"].add(capture_name)
                    target_identity = row.get("TargetIdentity", "")
                    if target_identity:
                        role_evidence["targetIdentities"].add(target_identity)
            combat_event = None
            duplicate_event = False
            if message_type == "Attack" or (
                message_type == "AttackInfo" and attack_info_allowed
            ):
                combat_event, duplicate_event = combat_deduplicator.observe(
                    capture_name, row, evidence_role
                )
            if duplicate_event:
                continue
            provenance_captures = (
                combat_event["provenanceCaptures"]
                if combat_event is not None
                else {capture_name}
            )
            parsed_attack = (
                attack_evidence(
                    row,
                    capture_name,
                    source,
                    provenance_captures,
                )
                if message_type == "AttackInfo" and attack_info_allowed
                else None
            )
            if role_evidence is not None:
                if message_type == "Attack":
                    role_evidence["retaliationRows"] += 1
                elif parsed_attack is not None:
                    role_evidence["attacks"].append(parsed_attack)
            if (
                message_type in {"Attack", "AttackInfo"}
                and row.get("TargetRole") != "local-player"
            ):
                continue
            if message_type == "Attack":
                group["retaliationRows"] += 1
            if message_type != "AttackInfo":
                continue
            if parsed_attack is None:
                continue
            group["attacks"].append(parsed_attack)

        add_reviewed_raw_target_role_evidence(
            capture_name,
            folder,
            identities,
            grouped,
            derived_source_keys,
        )
        if events_path.exists():
            for line in events_path.read_text(encoding="utf-8-sig", errors="replace").splitlines():
                match = WEAPON_UPDATE.search(line)
                if match:
                    enemy = identities.get(match.group("owner"))
                    if enemy and capture_includes_enemy(capture_name, enemy["name"]):
                        group = grouped[enemy["name"]]
                        weapon_key = (
                            capture_name,
                            match.group("owner"),
                            match.group("weapon"),
                            int(match.group("template")),
                            int(match.group("template2")),
                            int(match.group("quality")),
                        )
                        if weapon_key not in group["weaponKeys"]:
                            group["weaponKeys"].add(weapon_key)
                            group["identities"].add(match.group("owner"))
                            group["captures"].add(capture_name)
                            group["monsterData"].add(enemy["monsterData"])
                            group["weapons"].append(
                                {
                                    "capture": capture_name,
                                    "owner": match.group("owner"),
                                    "weaponIdentity": match.group("weapon"),
                                    "templateId": int(match.group("template")),
                                    "templateId2": int(match.group("template2")),
                                    "quality": int(match.group("quality")),
                                }
                            )

                miss = MISSED_ATTACK_INFO.search(line)
                if not miss or miss.group("defender") not in local_player_identities:
                    continue
                enemy = identities.get(miss.group("attacker"))
                if not enemy or not capture_includes_enemy(capture_name, enemy["name"]):
                    continue
                group = grouped[enemy["name"]]
                group["identities"].add(miss.group("attacker"))
                group["captures"].add(capture_name)
                group["monsterData"].add(enemy["monsterData"])
                group["misses"].append(
                    {
                        "capture": capture_name,
                        "identity": miss.group("attacker"),
                        "capturedUtc": miss.group("captured_utc"),
                        "ammoCount": int(miss.group("ammo")),
                        "weaponSlot": int(miss.group("slot")),
                        "unknown": int(miss.group("unknown")),
                    }
                )

    combat_deduplicator.validate()
    report = {}
    for name, group in sorted(grouped.items()):
        attacks = group["attacks"]
        normal_attacks = [row for row in attacks if row["hitType"] == "Normal"]
        critical_attacks = [row for row in attacks if row["hitType"] == "Critical"]
        shape_attacks = normal_attacks if name == "Filth Flea" else attacks
        intervals = []
        by_identity_shape = defaultdict(list)
        if name not in CADENCE_UNRESOLVED_ENEMIES:
            for attack in shape_attacks:
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
            for row in shape_attacks
        )
        critical_shapes = Counter(
            (row["weaponSlot"], row["attackInfoUnknown"], row["weaponInstance"])
            for row in critical_attacks
        )
        weapon_shapes = Counter(
            (row["templateId"], row["templateId2"], row["quality"])
            for row in group["weapons"]
        )
        weapon_shape_evidence = []
        for (low_id, high_id, weapon_quality), rows in sorted(
            weapon_shapes.items(),
            key=lambda item: (-item[1], item[0]),
        ):
            matching = [
                row
                for row in group["weapons"]
                if (
                    row["templateId"],
                    row["templateId2"],
                    row["quality"],
                )
                == (low_id, high_id, weapon_quality)
            ]
            weapon_shape_evidence.append(
                {
                    "lowId": low_id,
                    "highId": high_id,
                    "quality": weapon_quality,
                    "rows": rows,
                    "captures": sorted({row["capture"] for row in matching}),
                    "owners": sorted({row["owner"] for row in matching}),
                }
            )
        attack_shape_evidence = []
        for (shape_slot, shape_unknown, shape_instance), rows in sorted(
            attack_shapes.items(),
            key=lambda item: (-item[1], item[0]),
        ):
            matching = [
                row
                for row in shape_attacks
                if (
                    row["weaponSlot"],
                    row["attackInfoUnknown"],
                    row["weaponInstance"],
                )
                == (shape_slot, shape_unknown, shape_instance)
            ]
            shape_intervals = []
            if name not in CADENCE_UNRESOLVED_ENEMIES:
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
                    "captures": sorted(
                        {
                            capture
                            for row in matching
                            for capture in row["provenanceCaptures"]
                        }
                    ),
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
        critical_shape_evidence = []
        for (shape_slot, shape_unknown, shape_instance), rows in sorted(
            critical_shapes.items(),
            key=lambda item: (-item[1], item[0]),
        ):
            matching = [
                row
                for row in critical_attacks
                if (
                    row["weaponSlot"],
                    row["attackInfoUnknown"],
                    row["weaponInstance"],
                )
                == (shape_slot, shape_unknown, shape_instance)
            ]
            critical_shape_evidence.append(
                {
                    "weaponSlot": shape_slot,
                    "attackInfoUnknown": shape_unknown,
                    "weaponInstance": shape_instance,
                    "rows": rows,
                    "captures": sorted(
                        {
                            capture
                            for row in matching
                            for capture in row["provenanceCaptures"]
                        }
                    ),
                    "minDamage": min(row["amount"] for row in matching),
                    "maxDamage": max(row["amount"] for row in matching),
                }
            )
        template_id, template_id2, quality = (
            next(iter(weapon_shapes)) if len(weapon_shapes) == 1 else (0, 0, 0)
        )
        special_attack_weapon_shapes = Counter(
            (
                row["unknown1"],
                row["unknown2"],
                row["unknown3"],
                row["unknown4"],
                row["unknown5"],
            )
            for row in group["specialAttackWeapons"]
        )
        report_entry = {
            "monsterData": sorted(group["monsterData"]),
            "captures": sorted(group["captures"]),
            "identities": sorted(group["identities"]),
            "retaliationObserved": group["retaliationRows"] > 0,
            "retaliationRows": group["retaliationRows"],
            "attackInfoObserved": bool(attacks),
            "attackInfoRows": len(attacks),
            "minDamage": min((row["amount"] for row in attacks), default=0),
            "maxDamage": max((row["amount"] for row in attacks), default=0),
            "normalAttackInfoRows": len(normal_attacks),
            "normalMinDamage": min((row["amount"] for row in normal_attacks), default=0),
            "normalMaxDamage": max((row["amount"] for row in normal_attacks), default=0),
            "criticalAttackInfoRows": len(critical_attacks),
            "criticalMinDamage": min((row["amount"] for row in critical_attacks), default=0),
            "criticalMaxDamage": max((row["amount"] for row in critical_attacks), default=0),
            "missedAttackInfoRows": len(group["misses"]),
            "missedAttackShapes": [
                {
                    "ammoCount": ammo_count,
                    "weaponSlot": miss_slot,
                    "unknown": miss_unknown,
                    "rows": rows,
                    "captures": sorted(
                        {
                            row["capture"]
                            for row in group["misses"]
                            if (
                                row["ammoCount"],
                                row["weaponSlot"],
                                row["unknown"],
                            )
                            == (ammo_count, miss_slot, miss_unknown)
                        }
                    ),
                }
                for (ammo_count, miss_slot, miss_unknown), rows in sorted(
                    Counter(
                        (
                            row["ammoCount"],
                            row["weaponSlot"],
                            row["unknown"],
                        )
                        for row in group["misses"]
                    ).items(),
                    key=lambda item: (-item[1], item[0]),
                )
            ],
            "specialAttackWeaponRows": len(group["specialAttackWeapons"]),
            "specialAttackWeaponShapes": [
                {
                    "unknown1": unknown1,
                    "unknown2": unknown2,
                    "unknown3": unknown3,
                    "unknown4": unknown4,
                    "unknown5": unknown5,
                    "rows": rows,
                    "captures": sorted(
                        {
                            row["capture"]
                            for row in group["specialAttackWeapons"]
                            if (
                                row["unknown1"],
                                row["unknown2"],
                                row["unknown3"],
                                row["unknown4"],
                                row["unknown5"],
                            )
                            == (unknown1, unknown2, unknown3, unknown4, unknown5)
                        }
                    ),
                    "owners": sorted(
                        {
                            row["identity"]
                            for row in group["specialAttackWeapons"]
                            if (
                                row["unknown1"],
                                row["unknown2"],
                                row["unknown3"],
                                row["unknown4"],
                                row["unknown5"],
                            )
                            == (unknown1, unknown2, unknown3, unknown4, unknown5)
                        }
                    ),
                }
                for (unknown1, unknown2, unknown3, unknown4, unknown5), rows in sorted(
                    special_attack_weapon_shapes.items(),
                    key=lambda item: (-item[1], item[0]),
                )
            ],
            "medianRechargeSeconds": intervals[(len(intervals) - 1) // 2] if intervals else 0.0,
            "weaponSlot": slot,
            "attackInfoUnknown": unknown,
            "attackInfoWeaponInstance": instance,
            "attackShapes": attack_shape_evidence,
        }
        if name == "Filth Flea":
            report_entry["criticalAttackShapes"] = critical_shape_evidence
        if name in TARGET_ROLE_EVIDENCE_ENEMIES:
            target_role_evidence = {}
            evidence_roles = ["localPlayer", "playerOwnedPet"]
            if group["targetRoleEvidence"]["otherPlayer"]["captures"]:
                evidence_roles.append("otherPlayer")
            for evidence_role in evidence_roles:
                role_evidence = group["targetRoleEvidence"][evidence_role]
                role_attacks = role_evidence["attacks"]
                target_role_evidence[evidence_role] = {
                    "captures": sorted(role_evidence["captures"]),
                    "targetIdentities": sorted(role_evidence["targetIdentities"]),
                    "retaliationRows": role_evidence["retaliationRows"],
                    "attackInfoRows": len(role_attacks),
                    "minDamage": min(
                        (row["amount"] for row in role_attacks), default=0
                    ),
                    "maxDamage": max(
                        (row["amount"] for row in role_attacks), default=0
                    ),
                }
            report_entry["targetRoleEvidence"] = target_role_evidence
        if name in CADENCE_UNRESOLVED_ENEMIES:
            report_entry["cadenceStatus"] = "unresolved-mixed-target-fight"
        report_entry.update(
            {
                "equippedWeaponObserved": bool(weapon_shapes),
                "equippedWeaponAggregateResolved": len(weapon_shapes) == 1,
                "equippedWeaponTemplateId": template_id,
                "equippedWeaponHighTemplateId": template_id2,
                "equippedWeaponQuality": quality,
                "equippedWeaponShapes": weapon_shape_evidence,
            }
        )
        if name == "Workman Striker":
            validate_workman_striker_distinct_combat(attacks, report_entry)
        if name == "Discarded Pet":
            validate_discarded_pet_combat(report_entry)
        report[name] = report_entry

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"archetypes={len(report)} output={OUTPUT}")


if __name__ == "__main__":
    main()
