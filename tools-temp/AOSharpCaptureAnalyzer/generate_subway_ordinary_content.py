from __future__ import annotations

import argparse
import csv
import math
import os
import re
import tempfile
from collections import Counter, defaultdict
from datetime import datetime
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
CAPTURE_ROOT = REPO / "tools-temp" / "AOSharpLiveCapture" / "bin" / "Debug" / "captures"
CAPTURES = (
    "20260709-205921",
    "20260709-210452",
    "20260709-212115",
    "20260709-212336",
    "20260709-220439",
    "20260709-222339",
    "20260709-225408",
    "20260710-202132",
    "20260710-211430",
    "20260716-033326",
    "20260716-034104",
    "20260716-034559",
    "20260716-034656",
    "20260716-221358",
    "20260716-222201",
)
SPAWN_CAPTURES = (
    "20260709-212336",
    "20260709-222339",
    "20260709-225408",
    "20260710-202132",
)
CAPTURE_ARCHETYPE_FILTERS = {
    "20260710-202132": frozenset(("Looter", "Stim Fiend", "Deranged Shopper")),
    "20260716-034559": frozenset(("Melded Patterns",)),
    "20260716-034656": frozenset(("Slum Runner",)),
    "20260716-221358": frozenset(("Molested Molecules", "Neural Burnout")),
    "20260716-222201": frozenset(
        (
            "Fragmented Soul",
            "Incomplete Rebuild",
            "Melded Patterns",
            "Molested Molecules",
            "Neural Burnout",
            "Redundant Scan",
            "Slum Runner",
        )
    ),
}
CAPTURE_CORPSE_EVIDENCE_FILTERS = {
    "20260712-155528": frozenset(("Filth Flea",)),
    "20260712-161506": frozenset(("Filth Flea", "Thief")),
    "20260713-013906": frozenset(("Discarded Pet", "Mugger")),
    "20260712-223719": frozenset(
        (
            "Infector",
            "Melded Patterns",
            "Molested Molecules",
            "Neural Burnout",
            "Premature Pattern",
            "Shadow",
        )
    ),
    "20260712-224608": frozenset(("Fragmented Soul", "Premature Pattern")),
    "20260716-034656": frozenset(("Slum Runner",)),
    "20260716-215947": frozenset(
        (
            "Melded Patterns",
            "Molested Molecules",
            "Premature Pattern",
            "Shadow",
            "Slum Runner",
        )
    ),
    "20260716-221748": frozenset(("Neural Burnout",)),
}
ARCHETYPE_CAPTURE_FILTERS = {
    "Deranged Shopper": frozenset(("20260710-202132",)),
}
ARCHETYPE_SPAWN_IDENTITY_FILTERS = {
    "Bloodcreeper": frozenset(("(SimpleChar:795451C5)",)),
}
CAPTURE_SPAWN_IDENTITY_FILTERS = {
    "20260709-225408": frozenset(
        ("(SimpleChar:79545356)", "(SimpleChar:79545367)")
    ),
}
OUTPUT = (
    REPO
    / "AORebirth"
    / "Server"
    / "ZoneEngine"
    / "Core"
    / "Playfields"
    / "CapturedSubwayOrdinaryContentProvider.cs"
)

ARCHETYPES = {
    "Shadow": ("shadow", "shadow"),
    "Stim Fiend": ("stim_fiend", "stim_fiend"),
    "Workman Striker": ("workman_striker", "striker"),
    "Architect Striker": ("architect_striker", "striker"),
    "Infected Attendant": ("infected_attendant", "infected_attendant"),
    "Slum Runner": ("slum_runner", "slum_runner"),
    "Looter": ("looter", "looter"),
    "Infector": ("infector", "infector"),
    "Lost Thought": ("lost_thought", "lost_thought"),
    "Neural Burnout": ("neural_burnout", "neural_burnout"),
    "Bloodcreeper": ("bloodcreeper", "bloodcreeper"),
    "Deranged Shopper": ("deranged_shopper", "deranged_shopper"),
    "Empty Shell": ("empty_shell", "empty_shell"),
    "Fragmented Soul": ("fragmented_soul", "fragmented_soul"),
    "Incomplete Rebuild": ("incomplete_rebuild", "incomplete_rebuild"),
    "Melded Patterns": ("melded_patterns", "melded_patterns"),
    "Molested Molecules": ("molested_molecules", "molested_molecules"),
    "Premature Pattern": ("premature_pattern", "premature_pattern"),
    "Redundant Scan": ("redundant_scan", "redundant_scan"),
    "Uncontrollable Anger": ("uncontrollable_anger", "uncontrollable_anger"),
}

SUPPORTED_CORPSE_NAMES_BY_MONSTER_DATA = {
    17657: "Filth Flea",
    17720: "Discarded Pet",
    26092: "Thief",
    203734: "Mugger",
}
CORPSE_EVIDENCE_NAMES = frozenset(ARCHETYPES) | frozenset(
    SUPPORTED_CORPSE_NAMES_BY_MONSTER_DATA.values()
)
EXPECTED_CORPSE_MONSTER_DATA = {
    "Discarded Pet": 17720,
    "Filth Flea": 17657,
    "Fragmented Soul": 203729,
    "Infector": 31909,
    "Melded Patterns": 203747,
    "Molested Molecules": 203746,
    "Mugger": 203734,
    "Neural Burnout": 203730,
    "Premature Pattern": 203727,
    "Shadow": 30464,
    "Slum Runner": 55648,
    "Thief": 26092,
}

NAMED_BOSSES = frozenset(
    (
        "Abmouth Supremus",
        "Bitaxel",
        "Eumenides",
        "Strike Foreman",
        "Vergil Aeneid",
    )
)
OWNED_SUMMON_NAMES = frozenset(("Healer",))

CANDIDATE_ACCEPTED = "ACCEPTED_ORDINARY"
CANDIDATE_NAMED_BOSS = "NAMED_BOSS_EXCLUDED"
CANDIDATE_OWNED_SUMMON = "OWNED_SUMMON_EXCLUDED"
CANDIDATE_UNSUPPORTED = "UNSUPPORTED_EXCLUDED"
CANDIDATE_MALFORMED = "MALFORMED_EXCLUDED"

FLAG_VALUES = {
    "IsNpc": 0x00000001,
    "UnknownFlag": 0x00000002,
    "UnknownFlag6": 0x00000008,
    "HasExtendedTextures": 0x00000010,
    "HasFightingTarget": 0x00000020,
    "HasPlayfieldId": 0x00000040,
    "HasHeadMesh": 0x00000080,
    "HasNoWeaponPairs": 0x00000100,
    "HasHeading": 0x00000200,
    "IsUnderAttack": 0x00000400,
    "HasSmallHealth": 0x00000800,
    "HasExtendedLevel": 0x00001000,
    "HasExtendedRunSpeed": 0x00002000,
    "HasSmallHealthDamage": 0x00004000,
    "HasWaypoints": 0x00010000,
    "HasSmallNpcFamily": 0x00020000,
    "HasSmallNpcLosHeight": 0x00080000,
    "UnknownFlag7": 0x00200000,
    "UnknownFlag2": 0x00200000,
    "IsImmune": 0x00800000,
    "UnknownFlag3": 0x01000000,
    "UnknownDataFlag": 0x02000000,
    "HasOrgName": 0x04000000,
    "IsPet": 0x08000000,
    "UnknownFlag5": 0x10000000,
    "UnknownFlag4": 0x20000000,
}


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open(newline="", encoding="utf-8-sig") as handle:
        return list(csv.DictReader(handle))


def parse_time(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def stable_row_key(row: dict[str, str]) -> tuple:
    return (
        parse_time(row["CapturedUtc"]),
        row.get("EvidenceCapture", ""),
        int(row.get("EvidenceRowIndex", "0")),
        row.get("Identity", ""),
    )


def identity_hex(identity: str) -> str:
    return identity.removeprefix("(SimpleChar:").removesuffix(")")


def parse_flags(value: str) -> int:
    result = 0
    for name in (part.strip() for part in value.split(",")):
        if name:
            result |= FLAG_VALUES.get(name, 0)
    return result


def parse_triplets(value: str) -> list[tuple[str, str, str]]:
    result = []
    for part in value.split("|") if value else []:
        fields = part.split(":")
        if len(fields) == 3:
            result.append((fields[0], fields[1], fields[2]))
    return result


def parse_textures(value: str) -> list[tuple[int, int, int]]:
    return [(int(a), int(b), int(c)) for a, b, c in parse_triplets(value)]


def parse_meshes(value: str) -> list[tuple[int, int, int, int]]:
    result = []
    for part in value.split("|") if value else []:
        fields = part.split(":")
        if len(fields) == 4:
            result.append(tuple(int(field) for field in fields))
    return result


def canonical_position(row: dict[str, str]) -> tuple[float, float, float]:
    waypoints = parse_triplets(row.get("Waypoints", ""))
    if waypoints:
        return tuple(float(value) for value in waypoints[0])
    return (float(row["PositionX"]), float(row["PositionY"]), float(row["PositionZ"]))


def capture_allows_archetype(capture: str, name: str) -> bool:
    allowed_names = CAPTURE_ARCHETYPE_FILTERS.get(capture)
    if allowed_names is not None and name not in allowed_names:
        return False
    allowed_captures = ARCHETYPE_CAPTURE_FILTERS.get(name)
    return allowed_captures is None or capture in allowed_captures


def load_raw_scfu_rows(captures: tuple[str, ...]) -> list[dict[str, str]]:
    rows = []
    for capture in captures:
        path = CAPTURE_ROOT / capture / "scfu-appearance.csv"
        if not path.exists():
            path = CAPTURE_ROOT / capture / "scfu-appearance.pending.csv"
        if not path.exists():
            continue
        for row_index, row in enumerate(
            read_csv(path)
        ):
            if path.name.endswith(".pending.csv") and (
                row.get("DecodeStatus") != "decoded_complete"
                or row.get("DecodeFullyConsumed", "").lower() != "true"
            ):
                continue
            row = dict(row)
            row.setdefault("RunSpeed", row.get("RunSpeedBase", ""))
            row.setdefault("ScfuFlags", row.get("Flags", ""))
            row.setdefault("ScfuFlags2", row.get("Flags2Numeric", ""))
            row["EvidenceCapture"] = capture
            row["EvidenceRowIndex"] = str(row_index)
            rows.append(row)
    return rows


def capture_identity_names(capture: str) -> dict[str, str]:
    identities: dict[str, str] = {}
    for row in load_raw_scfu_rows((capture,)):
        identity = row.get("Identity", "")
        name = row.get("Name", "")
        if identity and name:
            identities[identity] = name
    full_updates_path = CAPTURE_ROOT / capture / "enemy-full-updates.csv"
    if full_updates_path.exists():
        for row in read_csv(full_updates_path):
            identity = row.get("Identity", "")
            name = row.get("Name", "")
            if identity and name:
                identities[identity] = name
    lifecycle_path = CAPTURE_ROOT / capture / "npc-lifecycle.csv"
    if lifecycle_path.exists():
        for row in read_csv(lifecycle_path):
            identity = row.get("PrimaryIdentity", "")
            name = row.get("Name", "")
            if identity and name and not name.startswith("Remains of "):
                identities[identity] = name
    return identities


def classify_spawn_candidate(row: dict[str, str]) -> str:
    if not row.get("Identity") or not row.get("Name"):
        return CANDIDATE_MALFORMED
    if row.get("Owner") or row["Name"] in OWNED_SUMMON_NAMES:
        return CANDIDATE_OWNED_SUMMON
    if row["Name"] in NAMED_BOSSES:
        return CANDIDATE_NAMED_BOSS
    if row["Name"] not in ARCHETYPES:
        return CANDIDATE_UNSUPPORTED
    if not capture_allows_archetype(row["EvidenceCapture"], row["Name"]):
        return CANDIDATE_UNSUPPORTED
    allowed_identities = ARCHETYPE_SPAWN_IDENTITY_FILTERS.get(row["Name"])
    if allowed_identities is not None and row["Identity"] not in allowed_identities:
        return CANDIDATE_UNSUPPORTED
    allowed_capture_identities = CAPTURE_SPAWN_IDENTITY_FILTERS.get(
        row["EvidenceCapture"]
    )
    if (
        allowed_capture_identities is not None
        and row["Identity"] not in allowed_capture_identities
    ):
        return CANDIDATE_UNSUPPORTED
    return CANDIDATE_ACCEPTED


def load_scfu_rows(captures: tuple[str, ...]) -> list[dict[str, str]]:
    return [
        row
        for row in load_raw_scfu_rows(captures)
        if row.get("Name") in ARCHETYPES
        and capture_allows_archetype(row["EvidenceCapture"], row["Name"])
    ]


def first_rows_by_identity(rows: list[dict[str, str]]) -> list[dict[str, str]]:
    result = {}
    for row in sorted(rows, key=stable_row_key):
        result.setdefault(row["Identity"], row)
    return list(result.values())


def select_spawns() -> list[dict[str, str]]:
    rows = first_rows_by_identity(
        [
            row
            for row in load_scfu_rows(SPAWN_CAPTURES)
            if classify_spawn_candidate(row) == CANDIDATE_ACCEPTED
        ]
    )
    selected: list[dict[str, str]] = []
    for row in sorted(rows, key=stable_row_key):
        position = canonical_position(row)
        duplicate = False
        for prior in selected:
            if prior["Name"] != row["Name"]:
                continue
            prior_position = canonical_position(prior)
            distance = math.sqrt(sum((a - b) ** 2 for a, b in zip(position, prior_position)))
            if distance <= 1.5:
                duplicate = True
                break
        if not duplicate:
            selected.append(row)
    return sorted(selected, key=lambda value: (value["Name"], value["Identity"]))


def visual_profile(row: dict[str, str]) -> tuple:
    return (
        row["AppearanceValue"],
        row["Side"],
        row["Fatness"],
        row["Breed"],
        row["Gender"],
        row["Race"],
        row["MonsterData"],
        row["NpcFamily"],
        row["NpcLosHeight"],
        row["CharacterFlags"],
        row["AccountFlags"],
        row["Expansions"],
        row["VisualFlags"],
        row["VisibleTitle"],
        row["HeadMesh"],
        row["Textures"],
        row["Meshes"],
        row["TextureOverrides"],
    )


def select_archetype_profiles(spawns: list[dict[str, str]]) -> dict[str, dict[str, str]]:
    profiles = {}
    for name in ARCHETYPES:
        candidates = [row for row in spawns if row["Name"] == name]
        if not candidates:
            raise ValueError("ordinary archetype has no captured profile: " + name)
        counts = Counter(visual_profile(row) for row in candidates)
        selected_profile = min(
            counts,
            key=lambda profile: (
                -counts[profile],
                min(
                    stable_row_key(row)
                    for row in candidates
                    if visual_profile(row) == profile
                ),
                tuple(str(value) for value in profile),
            ),
        )
        profiles[name] = min(
            (row for row in candidates if visual_profile(row) == selected_profile),
            key=stable_row_key,
        )
    return profiles


def combat_profiles() -> dict[str, dict[str, object]]:
    attacks = defaultdict(list)
    detail_pattern = re.compile(
        r"WeaponSlot=(?P<slot>-?\d+).*Unk1=(?P<unknown>-?\d+).*WeaponInstance=(?P<instance>-?\d+)"
    )
    for capture_index, capture in enumerate(CAPTURES):
        name_by_identity = capture_identity_names(capture)
        for row_index, row in enumerate(
            read_csv(CAPTURE_ROOT / capture / "enemy-combat.csv")
        ):
            source_identity = row["SourceIdentity"]
            name = name_by_identity.get(source_identity, "")
            if (
                row["MessageType"] != "AttackInfo"
                or row.get("SourceRole") != "enemy"
                or row.get("TargetRole") != "local-player"
                or name not in ARCHETYPES
                or not capture_allows_archetype(capture, name)
                or "HitType=Normal" not in row.get("Detail", "")
            ):
                continue
            if not row["Amount"].isdigit() or int(row["Amount"]) <= 0:
                continue
            match = detail_pattern.search(row["Detail"])
            if not match:
                continue
            attacks[name].append(
                {
                    "capture": capture,
                    "identity": source_identity,
                    "time": parse_time(row["CapturedUtc"]),
                    "amount": int(row["Amount"]),
                    "slot": int(match.group("slot")),
                    "unknown": int(match.group("unknown")),
                    "instance": int(match.group("instance")),
                    "order": (capture_index, row_index),
                }
            )

    result = {}
    for name in ARCHETYPES:
        rows = attacks[name]
        intervals = []
        by_identity = defaultdict(list)
        for row in rows:
            by_identity[(row["capture"], row["identity"])].append(row)
        for identity_rows in by_identity.values():
            times = sorted(row["time"] for row in identity_rows)
            for previous, current in zip(times, times[1:]):
                seconds = (current - previous).total_seconds()
                if 0.5 <= seconds <= 10.0:
                    intervals.append(seconds)
        intervals.sort()
        attack_contexts = Counter(
            (row["slot"], row["unknown"], row["instance"]) for row in rows
        )
        if attack_contexts:
            slot, unknown, instance = min(
                attack_contexts,
                key=lambda context: (
                    -attack_contexts[context],
                    min(row["order"] for row in rows if (
                        row["slot"], row["unknown"], row["instance"]
                    ) == context),
                    context,
                ),
            )
        else:
            slot, unknown, instance = (None, None, None)
        result[name] = {
            "observed": bool(rows),
            "min": min((row["amount"] for row in rows), default=None),
            "max": max((row["amount"] for row in rows), default=None),
            "recharge": intervals[(len(intervals) - 1) // 2] if intervals else None,
            "slot": slot,
            "unknown": unknown,
            "instance": instance,
            "rows": len(rows),
        }
    return result


def loot_profiles() -> dict[str, list[dict[str, int]]]:
    mapped = defaultdict(list)
    opened_by_name = defaultdict(list)
    corpse_pattern = re.compile(
        r"^(?P<time>\S+) \[CORPSE-SEEN\] identity=\((?P<identity>Corpse:[0-9A-F]+)\) "
        r"name=Remains of (?P<name>.+?) pos="
    )
    for capture in CAPTURES:
        strict_observations_path = CAPTURE_ROOT / capture / "corpse-loot-observations.csv"
        strict_observations_used = False
        if strict_observations_path.exists():
            for row in read_csv(strict_observations_path):
                name = row.get("EnemyName", "")
                if (
                    row.get("InitialSnapshot", "").lower() != "true"
                    or not row.get("CorrelationStatus", "").startswith("linked-")
                    or name not in ARCHETYPES
                    or not capture_allows_archetype(capture, name)
                ):
                    continue
                items = []
                for item in row.get("Items", "").split(";"):
                    if not item:
                        continue
                    low, high, quality, count = (int(value) for value in item.split(":"))
                    items.extend([(low, high, quality)] * count)
                opened_by_name[name].append(items)
                strict_observations_used = True
        if strict_observations_used:
            continue

        corpses = []
        with (CAPTURE_ROOT / capture / "events.log").open(encoding="utf-8-sig") as handle:
            for line in handle:
                match = corpse_pattern.search(line)
                if (
                    match
                    and match.group("name") in ARCHETYPES
                    and capture_allows_archetype(capture, match.group("name"))
                ):
                    corpses.append(
                        {
                            "time": parse_time(match.group("time")),
                            "identity": f"({match.group('identity')})",
                            "name": match.group("name"),
                        }
                    )
        corpse_loot = defaultdict(list)
        for row in read_csv(CAPTURE_ROOT / capture / "inventory-updates.csv"):
            if not row["InventoryIdentity"].startswith("(Corpse:"):
                continue
            timestamp = parse_time(row["CapturedUtc"])
            candidates = [
                corpse
                for corpse in corpses
                if corpse["identity"] == row["InventoryIdentity"] and corpse["time"] <= timestamp
            ]
            if not candidates:
                continue
            corpse = max(candidates, key=lambda value: value["time"])
            key = (capture, corpse["identity"], corpse["time"].isoformat(), corpse["name"])
            corpse_loot[key].append((int(row["LowId"]), int(row["HighId"]), int(row["Quality"])))
        for key, items in corpse_loot.items():
            opened_by_name[key[3]].append(items)
    for name, corpse_items in opened_by_name.items():
        opened_corpses = len(corpse_items)
        counts = Counter(item for items in corpse_items for item in items)
        for (low, high, quality), count in sorted(counts.items()):
            mapped[name].append(
                {
                    "low": low,
                    "high": high,
                    "quality": quality,
                    "count": count,
                    "corpses": opened_corpses,
                    "basis": min(10000, int(round(count * 10000.0 / opened_corpses))),
                }
            )
    return mapped


def corpse_profiles() -> dict[str, list[dict[str, object]]]:
    mapped = defaultdict(list)
    for capture, allowed_names in CAPTURE_CORPSE_EVIDENCE_FILTERS.items():
        death_levels = {}
        for row in read_csv(CAPTURE_ROOT / capture / "enemy-state.csv"):
            if str(row.get("eventType") or "").lower() != "death":
                continue
            dead_identity = row.get("entityId", "")
            if dead_identity.startswith("SimpleChar:"):
                dead_identity = f"({dead_identity})"
            try:
                enemy_level = int(row.get("level", ""))
            except ValueError:
                continue
            if dead_identity and enemy_level > 0:
                death_levels[dead_identity] = enemy_level

        for row in read_csv(CAPTURE_ROOT / capture / "corpse-full-updates.csv"):
            dead_identity = row.get("DeadNpcIdentity", "")
            if dead_identity.startswith("SimpleChar:"):
                dead_identity = f"({dead_identity})"
            corpse_identity = row.get("CorpseIdentity", "")
            if corpse_identity.startswith("Corpse:"):
                corpse_identity = f"({corpse_identity})"
            if (
                not dead_identity
                or not corpse_identity
                or dead_identity not in death_levels
            ):
                continue
            try:
                monster_data = int(row.get("CorpseMonsterData", ""))
                cat_mesh = int(row.get("CorpseCatMesh", ""))
                credits = int(row.get("CorpseCredits", ""))
            except ValueError:
                continue
            name = row.get("DeadNpcName", "") or SUPPORTED_CORPSE_NAMES_BY_MONSTER_DATA.get(
                monster_data, ""
            )
            if name not in allowed_names or name not in CORPSE_EVIDENCE_NAMES:
                continue
            if monster_data <= 0 or cat_mesh <= 0 or credits < 0:
                continue
            mapped[name].append(
                {
                    "capture": capture,
                    "capturedUtc": row.get("CapturedUtc", ""),
                    "corpseIdentity": corpse_identity,
                    "deadNpcIdentity": dead_identity,
                    "enemyLevel": death_levels[dead_identity],
                    "monsterData": monster_data,
                    "catMesh": cat_mesh,
                    "credits": credits,
                }
            )

    for records in mapped.values():
        records.sort(
            key=lambda value: (
                value["capturedUtc"],
                value["corpseIdentity"],
                value["deadNpcIdentity"],
            )
        )
    return mapped


def cs_string(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


def cs_float(value: str | float) -> str:
    text = str(value)
    if "e" in text.lower():
        text = format(float(text), ".9f").rstrip("0").rstrip(".")
    if "." not in text:
        text += ".0"
    return text + "f"


def emit_array(items: list[str], indent: str) -> str:
    if not items:
        return "new " + ("CapturedSubwayTextureDefinition[0]" if "Texture" in indent else "object[0]")
    return "\n".join(items)


def compatibility_int(value: int | None) -> int:
    # The checked-in provider's legacy constructor has non-nullable value fields.
    # Keep unresolved evidence as None in the generator model and translate only
    # at this compatibility serialization boundary.
    return 0 if value is None else value


def compatibility_float(value: float | None) -> float:
    return 0.0 if value is None else value


def corpse_definition(item: dict[str, object]) -> str:
    return (
        "new CapturedSubwayCorpseEvidenceDefinition("
        f"{cs_string(str(item['capture']))}, "
        f"{cs_string(str(item['capturedUtc']))}, "
        f"{cs_string(str(item['corpseIdentity']))}, "
        f"{cs_string(str(item['deadNpcIdentity']))}, "
        f"{int(item['enemyLevel'])}, {int(item['monsterData'])}, "
        f"{int(item['catMesh'])}, {int(item['credits'])})"
    )


def validate_content(
    spawns: list[dict[str, str]],
    profiles: dict[str, dict[str, str]],
    combat: dict[str, dict[str, object]],
    corpses: dict[str, list[dict[str, object]]],
) -> None:
    profile_keys = [key for key, _ in ARCHETYPES.values()]
    duplicate_profile_keys = sorted(
        key for key, count in Counter(profile_keys).items() if count > 1
    )
    if duplicate_profile_keys:
        raise ValueError(
            "duplicate ordinary profile keys: " + ", ".join(duplicate_profile_keys)
        )

    expected_names = set(ARCHETYPES)
    if set(profiles) != expected_names:
        missing = sorted(expected_names - set(profiles))
        unexpected = sorted(set(profiles) - expected_names)
        raise ValueError(
            "ordinary profile set mismatch missing="
            + ",".join(missing)
            + " unexpected="
            + ",".join(unexpected)
        )
    generated_profile_keys = {ARCHETYPES[name][0] for name in profiles}

    identities = [row["Identity"] for row in spawns]
    duplicate_identities = sorted(
        identity for identity, count in Counter(identities).items() if count > 1
    )
    if duplicate_identities:
        raise ValueError(
            "duplicate ordinary spawn identities: " + ", ".join(duplicate_identities)
        )

    selected_identities = set(identities)
    for row in spawns:
        disposition = classify_spawn_candidate(row)
        if disposition != CANDIDATE_ACCEPTED:
            raise ValueError(
                "rejected ordinary spawn reached output identity={0} name={1} disposition={2}".format(
                    row.get("Identity", ""), row.get("Name", ""), disposition
                )
            )
        profile_key = ARCHETYPES[row["Name"]][0]
        if row["Name"] not in profiles or profile_key not in generated_profile_keys:
            raise ValueError(
                "ordinary spawn has missing profile identity={0} profile={1}".format(
                    row["Identity"], profile_key
                )
            )

    for row in first_rows_by_identity(load_raw_scfu_rows(SPAWN_CAPTURES)):
        disposition = classify_spawn_candidate(row)
        if disposition != CANDIDATE_ACCEPTED and row.get("Identity") in selected_identities:
            raise ValueError(
                "excluded capture identity reached ordinary output identity={0} name={1} disposition={2}".format(
                    row.get("Identity", ""), row.get("Name", ""), disposition
                )
            )

    for name, evidence in combat.items():
        if evidence["observed"]:
            if evidence["min"] is None or evidence["max"] is None:
                raise ValueError("observed combat is missing damage evidence: " + name)
            if evidence["slot"] is None or evidence["unknown"] is None or evidence["instance"] is None:
                raise ValueError("observed combat is missing attack context: " + name)
        elif any(
            evidence[field] is not None
            for field in ("min", "max", "recharge", "slot", "unknown", "instance")
        ):
            raise ValueError("unobserved combat contains invented values: " + name)

    expected_level_credits = {
        "Discarded Pet": Counter({(9, 32): 1}),
        "Filth Flea": Counter({(4, 23): 1, (5, 29): 1}),
        "Fragmented Soul": Counter({(18, 111): 1}),
        "Infector": Counter({(25, 156): 2}),
        "Melded Patterns": Counter({(21, 131): 1, (24, 150): 1, (25, 156): 1}),
        "Molested Molecules": Counter({(22, 137): 1, (23, 144): 1, (24, 150): 1}),
        "Mugger": Counter({(5, 44): 1}),
        "Neural Burnout": Counter({(18, 111): 1, (23, 144): 1}),
        "Premature Pattern": Counter({(18, 111): 1, (23, 144): 2}),
        "Shadow": Counter({(22, 137): 2, (23, 144): 3}),
        "Slum Runner": Counter({(21, 131): 2, (22, 137): 2, (23, 144): 3}),
        "Thief": Counter({(5, 29): 1}),
    }
    expected_cat_meshes = {
        "Discarded Pet": 15929,
        "Filth Flea": 15231,
        "Fragmented Soul": 5921,
        "Infector": 31868,
        "Melded Patterns": 23368,
        "Molested Molecules": 5921,
        "Mugger": 17534,
        "Neural Burnout": 5941,
        "Premature Pattern": 5941,
        "Shadow": 30434,
        "Slum Runner": 31774,
        "Thief": 5907,
    }
    if set(corpses) != set(expected_level_credits):
        raise ValueError("ordinary corpse evidence profile set drifted")
    for name, expected in expected_level_credits.items():
        records = corpses[name]
        actual = Counter((row["enemyLevel"], row["credits"]) for row in records)
        if actual != expected:
            raise ValueError(name + " level-credit corpse evidence drifted")
        if {row["monsterData"] for row in records} != {
            EXPECTED_CORPSE_MONSTER_DATA[name]
        }:
            raise ValueError(name + " corpse MonsterData attachment drifted")
        if {row["catMesh"] for row in records} != {expected_cat_meshes[name]}:
            raise ValueError(name + " corpse CATMesh drifted")
        if any(row["credits"] <= 0 for row in records):
            raise ValueError(name + " contains excluded zero-credit corpse evidence")
        if len({(row["capture"], row["corpseIdentity"]) for row in records}) != len(records):
            raise ValueError(name + " corpse capture identities are not unique")
        if len({(row["capture"], row["deadNpcIdentity"]) for row in records}) != len(records):
            raise ValueError(name + " dead NPC capture identities are not unique")


def generate() -> str:
    spawns = select_spawns()
    profiles = select_archetype_profiles(spawns)
    combat = combat_profiles()
    loot = loot_profiles()
    corpses = corpse_profiles()
    validate_content(spawns, profiles, combat, corpses)
    evidence_captures = defaultdict(set)
    for capture in CAPTURES:
        for name in set(capture_identity_names(capture).values()):
            if name in ARCHETYPES and capture_allows_archetype(capture, name):
                evidence_captures[name].add(capture)
    for name, records in corpses.items():
        for record in records:
            evidence_captures[name].add(str(record["capture"]))
    supported_corpses = sorted(
        (
            record
            for name in SUPPORTED_CORPSE_NAMES_BY_MONSTER_DATA.values()
            for record in corpses.get(name, [])
        ),
        key=lambda value: (
            value["capturedUtc"],
            value["corpseIdentity"],
            value["deadNpcIdentity"],
        ),
    )
    lines = [
        "// <auto-generated>",
        "// Generated only from completed AOSharp Subway captures by generate_subway_ordinary_content.py.",
        "// Do not hand-edit captured values; regenerate from the checked capture evidence.",
        "// </auto-generated>",
        "namespace AORebirth.Core.Playfields",
        "{",
        "    using System;",
        "    using System.Collections.Generic;",
        "    using System.Linq;",
        "",
        "    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;",
        "",
        "    using ZoneEngine.Core;",
        "    using ZoneEngine.Core.Playfields;",
        "",
        "    internal sealed class CapturedSubwayOrdinaryContentProvider",
        "    {",
        "        public const int SubwayPlayfieldInstance = 127;",
        "",
        "        private static readonly CapturedSubwayCorpseEvidenceDefinition[] SupportedCorpseEvidence =",
        "        {",
        *[
            "            " + corpse_definition(item)
            + ("," if index < len(supported_corpses) - 1 else "")
            for index, item in enumerate(supported_corpses)
        ],
        "        };",
        "",
        "        private static readonly CapturedSubwayOrdinaryArchetypeDefinition[] Archetypes =",
        "        {",
    ]

    for name, (key, family_key) in ARCHETYPES.items():
        row = profiles[name]
        combat_row = combat[name]
        texture_lines = [
            f"new CapturedSubwayTextureDefinition({place}, {texture_id}, {unknown})"
            for place, texture_id, unknown in parse_textures(row["Textures"])
        ]
        mesh_lines = [
            f"new CapturedSubwayMeshDefinition({position}, {mesh_id}u, {override}, {layer})"
            for position, mesh_id, override, layer in parse_meshes(row["Meshes"])
        ]
        loot_lines = [
            "new CapturedSubwayLootEvidenceDefinition("
            f"{item['low']}, {item['high']}, {item['quality']}, {item['count']}, {item['corpses']}, {item['basis']})"
            for item in loot.get(name, [])
        ]
        corpse_lines = [corpse_definition(item) for item in corpses.get(name, [])]
        lines.extend(
            [
                "            new CapturedSubwayOrdinaryArchetypeDefinition(",
                f"                {cs_string(key)},",
                f"                {cs_string(family_key)},",
                f"                {cs_string(name)},",
                f"                {int(row['MonsterData'])},",
                f"                {int(row['NpcFamily'] or 0)},",
                f"                {int(row['NpcLosHeight'] or 0)},",
                f"                {int(row['CharacterFlags'])},",
                f"                {int(row['AccountFlags'])},",
                f"                {int(row['Expansions'])},",
                f"                {int(row['VisualFlags'])},",
                f"                {int(row['VisibleTitle'])},",
                f"                {int(row['AppearanceValue'])}u,",
                f"                {int(row['HeadMesh'] or 0)},",
                "                new CapturedSubwayTextureDefinition[]",
                "                {",
            ]
        )
        for index, item in enumerate(texture_lines):
            lines.append("                    " + item + ("," if index < len(texture_lines) - 1 else ""))
        lines.extend(["                },", "                new CapturedSubwayMeshDefinition[]", "                {"])
        for index, item in enumerate(mesh_lines):
            lines.append("                    " + item + ("," if index < len(mesh_lines) - 1 else ""))
        lines.extend(
            [
                "                },",
                "                new CapturedSubwayCombatEvidenceDefinition(",
                f"                    {str(combat_row['observed']).lower()},",
                f"                    {compatibility_int(combat_row['min'])},",
                f"                    {compatibility_int(combat_row['max'])},",
                f"                    {compatibility_float(combat_row['recharge']):.6f},",
                f"                    {compatibility_int(combat_row['slot'])},",
                f"                    {compatibility_int(combat_row['unknown'])},",
                f"                    {compatibility_int(combat_row['instance'])},",
                f"                    {combat_row['rows']}),",
                "                new CapturedSubwayLootEvidenceDefinition[]",
                "                {",
            ]
        )
        for index, item in enumerate(loot_lines):
            lines.append("                    " + item + ("," if index < len(loot_lines) - 1 else ""))
        lines.extend(
            [
                "                },",
                "                new CapturedSubwayCorpseEvidenceDefinition[]",
                "                {",
            ]
        )
        for index, item in enumerate(corpse_lines):
            lines.append("                    " + item + ("," if index < len(corpse_lines) - 1 else ""))
        lines.extend(
            [
                "                },",
                "                new string[]",
                "                {",
                *[
                    "                    " + cs_string(capture)
                    + ("," if i < len(evidence_captures[name]) - 1 else "")
                    for i, capture in enumerate(sorted(evidence_captures[name]))
                ],
                "                }),",
            ]
        )

    lines.extend(["        };", "", "        private static readonly CapturedSubwayOrdinarySpawnDefinition[] Spawns =", "        {"])
    for row in spawns:
        key, _ = ARCHETYPES[row["Name"]]
        x, y, z = canonical_position(row)
        waypoints = parse_triplets(row.get("Waypoints", ""))
        lines.extend(
            [
                "            new CapturedSubwayOrdinarySpawnDefinition(",
                f"                0x{identity_hex(row['Identity'])},",
                f"                {cs_string(key)},",
                f"                {int(row['Level'])},",
                f"                {int(row['Health'])},",
                f"                {int(row['HealthDamage'])},",
                f"                {int(row['MonsterScale'])},",
                f"                {int(row['RunSpeed'])},",
                f"                {cs_float(x)}, {cs_float(y)}, {cs_float(z)},",
                f"                {cs_float(row['HeadingX'])}, {cs_float(row['HeadingY'])}, {cs_float(row['HeadingZ'])}, {cs_float(row['HeadingW'])},",
                f"                (SimpleCharFullUpdateFlags)0x{parse_flags(row['ScfuFlags']):08X},",
                f"                {int(row['ScfuFlags2'].replace('HasOwner', '4') or 0) if row['ScfuFlags2'].isdigit() else 0},",
                f"                {cs_string(row['ScfuUnknown1Hex'])},",
                f"                {int(row['ScfuUnknown2'])},",
                "                new CapturedSubwayWaypointDefinition[]",
                "                {",
            ]
        )
        for index, waypoint in enumerate(waypoints):
            suffix = "," if index < len(waypoints) - 1 else ""
            lines.append(
                "                    new CapturedSubwayWaypointDefinition("
                f"{cs_float(waypoint[0])}, {cs_float(waypoint[1])}, {cs_float(waypoint[2])}){suffix}"
            )
        lines.extend(
            [
                "                },",
                f"                {cs_string(row['Owner'])},",
                f"                {cs_string(row['EvidenceCapture'])},",
                f"                {cs_string(row['CapturedUtc'])}),",
            ]
        )

    lines.extend(
        [
            "        };",
            "",
            "        private static readonly Dictionary<string, CapturedSubwayOrdinaryArchetypeDefinition> ArchetypesByKey =",
            "            Archetypes.ToDictionary(value => value.Key, StringComparer.Ordinal);",
            "",
            "        public CapturedSubwayOrdinaryArchetypeDefinition[] GetArchetypes()",
            "        {",
            "            return Archetypes.ToArray();",
            "        }",
            "",
            "        public CapturedSubwayCorpseEvidenceDefinition[] GetCorpseEvidence(int monsterData)",
            "        {",
            "            return SupportedCorpseEvidence",
            "                .Concat(Archetypes.SelectMany(value => value.CorpseEvidence))",
            "                .Where(value => value.MonsterData == monsterData)",
            "                .ToArray();",
            "        }",
            "",
            "        public CapturedSubwayOrdinarySpawnDefinition[] GetSpawns()",
            "        {",
            "            return Spawns",
            "                .Where(",
            "                    spawn => !string.Equals(spawn.EvidenceCapture, \"20260710-202132\", StringComparison.Ordinal)",
            "                             || SubwayVisibilityDiagnosticSelection.ShouldIncludeQuarantined(spawn.SourceInstance))",
            "                .ToArray();",
            "        }",
            "",
            "        internal CapturedSubwayOrdinarySpawnDefinition[] GetAllSpawns()",
            "        {",
            "            return Spawns.ToArray();",
            "        }",
            "",
            "        public bool TryGetArchetype(string key, out CapturedSubwayOrdinaryArchetypeDefinition archetype)",
            "        {",
            "            return ArchetypesByKey.TryGetValue(key, out archetype);",
            "        }",
            "",
            "        public CombatLootTableEntry[] BuildCapturedLootEntries()",
            "        {",
            "            var entries = new List<CombatLootTableEntry>();",
            "            foreach (CapturedSubwayOrdinaryArchetypeDefinition archetype in Archetypes)",
            "            {",
            "                int slot = 0;",
            "                foreach (CapturedSubwayLootEvidenceDefinition loot in archetype.LootEvidence)",
            "                {",
            "                    entries.Add(",
            "                        new CombatLootTableEntry",
            "                        {",
            "                            ExactName = archetype.Name,",
            "                            MonsterData = archetype.MonsterData,",
            "                            NpcFamily = archetype.NpcFamily,",
            "                            Slot = slot++,",
            "                            DropChanceBasisPoints = loot.ObservedBasisPoints,",
            "                            ItemTemplates =",
            "                                new[]",
            "                                {",
            "                                    new CombatLootItemTemplate",
            "                                    {",
            "                                        LowId = loot.LowId,",
            "                                        HighId = loot.HighId,",
            "                                        MinQuality = loot.Quality,",
            "                                        MaxQuality = loot.Quality,",
            "                                        RangeCheck = 0,",
            "                                        DropGroupHash = \"captured-subway-ordinary\"",
            "                                    }",
            "                                }",
            "                        });",
            "                }",
            "            }",
            "",
            "            return entries.ToArray();",
            "        }",
            "    }",
            "",
            "    internal sealed class CapturedSubwayOrdinaryArchetypeDefinition",
            "    {",
            "        public CapturedSubwayOrdinaryArchetypeDefinition(string key, string familyKey, string name, int monsterData, int npcFamily, int npcLosHeight, int characterFlags, int accountFlags, int expansions, int visualFlags, int visibleTitle, uint appearanceValue, int headMesh, CapturedSubwayTextureDefinition[] textures, CapturedSubwayMeshDefinition[] meshes, CapturedSubwayCombatEvidenceDefinition combat, CapturedSubwayLootEvidenceDefinition[] lootEvidence, CapturedSubwayCorpseEvidenceDefinition[] corpseEvidence, string[] evidenceCaptures)",
            "        {",
            "            this.Key = key; this.FamilyKey = familyKey; this.Name = name; this.MonsterData = monsterData; this.NpcFamily = npcFamily; this.NpcLosHeight = npcLosHeight; this.CharacterFlags = characterFlags; this.AccountFlags = accountFlags; this.Expansions = expansions; this.VisualFlags = visualFlags; this.VisibleTitle = visibleTitle; this.AppearanceValue = appearanceValue; this.HeadMesh = headMesh; this.Textures = textures ?? new CapturedSubwayTextureDefinition[0]; this.Meshes = meshes ?? new CapturedSubwayMeshDefinition[0]; this.Combat = combat; this.LootEvidence = lootEvidence ?? new CapturedSubwayLootEvidenceDefinition[0]; this.CorpseEvidence = corpseEvidence ?? new CapturedSubwayCorpseEvidenceDefinition[0]; this.EvidenceCaptures = evidenceCaptures ?? new string[0];",
            "        }",
            "        public string Key { get; private set; } public string FamilyKey { get; private set; } public string Name { get; private set; } public int MonsterData { get; private set; } public int NpcFamily { get; private set; } public int NpcLosHeight { get; private set; } public int CharacterFlags { get; private set; } public int AccountFlags { get; private set; } public int Expansions { get; private set; } public int VisualFlags { get; private set; } public int VisibleTitle { get; private set; } public uint AppearanceValue { get; private set; } public int HeadMesh { get; private set; } public CapturedSubwayTextureDefinition[] Textures { get; private set; } public CapturedSubwayMeshDefinition[] Meshes { get; private set; } public CapturedSubwayCombatEvidenceDefinition Combat { get; private set; } public CapturedSubwayLootEvidenceDefinition[] LootEvidence { get; private set; } public CapturedSubwayCorpseEvidenceDefinition[] CorpseEvidence { get; private set; } public string[] EvidenceCaptures { get; private set; }",
            "    }",
            "",
            "    internal sealed class CapturedSubwayOrdinarySpawnDefinition",
            "    {",
            "        public CapturedSubwayOrdinarySpawnDefinition(int sourceInstance, string archetypeKey, int level, int health, int healthDamage, int monsterScale, int runSpeed, float x, float y, float z, float headingX, float headingY, float headingZ, float headingW, SimpleCharFullUpdateFlags capturedFlags, int capturedFlags2, string unknown1Hex, int unknown2, CapturedSubwayWaypointDefinition[] waypoints, string sourceOwnerIdentity, string evidenceCapture, string evidenceTimestamp)",
            "        {",
            "            this.SourceInstance = sourceInstance; this.ArchetypeKey = archetypeKey; this.Level = level; this.Health = health; this.HealthDamage = healthDamage; this.MonsterScale = monsterScale; this.RunSpeed = runSpeed; this.X = x; this.Y = y; this.Z = z; this.HeadingX = headingX; this.HeadingY = headingY; this.HeadingZ = headingZ; this.HeadingW = headingW; this.CapturedFlags = capturedFlags; this.CapturedFlags2 = capturedFlags2; this.Unknown1 = HexToBytes(unknown1Hex); this.Unknown2 = unknown2; this.Waypoints = waypoints ?? new CapturedSubwayWaypointDefinition[0]; this.SourceOwnerIdentity = sourceOwnerIdentity; this.EvidenceCapture = evidenceCapture; this.EvidenceTimestamp = evidenceTimestamp;",
            "        }",
            "        public int SourceInstance { get; private set; } public string ArchetypeKey { get; private set; } public int Level { get; private set; } public int Health { get; private set; } public int HealthDamage { get; private set; } public int MonsterScale { get; private set; } public int RunSpeed { get; private set; } public float X { get; private set; } public float Y { get; private set; } public float Z { get; private set; } public float HeadingX { get; private set; } public float HeadingY { get; private set; } public float HeadingZ { get; private set; } public float HeadingW { get; private set; } public SimpleCharFullUpdateFlags CapturedFlags { get; private set; } public int CapturedFlags2 { get; private set; } public byte[] Unknown1 { get; private set; } public int Unknown2 { get; private set; } public CapturedSubwayWaypointDefinition[] Waypoints { get; private set; } public string SourceOwnerIdentity { get; private set; } public string EvidenceCapture { get; private set; } public string EvidenceTimestamp { get; private set; }",
            "        private static byte[] HexToBytes(string value) { if (string.IsNullOrEmpty(value)) return new byte[0]; var result = new byte[value.Length / 2]; for (int i = 0; i < result.Length; i++) result[i] = Convert.ToByte(value.Substring(i * 2, 2), 16); return result; }",
            "    }",
            "",
            "    internal sealed class CapturedSubwayTextureDefinition { public CapturedSubwayTextureDefinition(int place, int id, int unknown) { this.Place = place; this.Id = id; this.Unknown = unknown; } public int Place { get; private set; } public int Id { get; private set; } public int Unknown { get; private set; } }",
            "    internal sealed class CapturedSubwayMeshDefinition { public CapturedSubwayMeshDefinition(int position, uint id, int overrideTextureId, int layer) { this.Position = position; this.Id = id; this.OverrideTextureId = overrideTextureId; this.Layer = layer; } public int Position { get; private set; } public uint Id { get; private set; } public int OverrideTextureId { get; private set; } public int Layer { get; private set; } }",
            "    internal sealed class CapturedSubwayWaypointDefinition { public CapturedSubwayWaypointDefinition(float x, float y, float z) { this.X = x; this.Y = y; this.Z = z; } public float X { get; private set; } public float Y { get; private set; } public float Z { get; private set; } }",
            "    internal sealed class CapturedSubwayCombatEvidenceDefinition { public CapturedSubwayCombatEvidenceDefinition(bool observed, int minDamage, int maxDamage, double rechargeSeconds, int weaponSlot, int attackInfoUnknown, int weaponInstance, int observedRows) { this.Observed = observed; this.MinDamage = minDamage; this.MaxDamage = maxDamage; this.RechargeSeconds = rechargeSeconds; this.WeaponSlot = weaponSlot; this.AttackInfoUnknown = attackInfoUnknown; this.WeaponInstance = weaponInstance; this.ObservedRows = observedRows; } public bool Observed { get; private set; } public int MinDamage { get; private set; } public int MaxDamage { get; private set; } public double RechargeSeconds { get; private set; } public int WeaponSlot { get; private set; } public int AttackInfoUnknown { get; private set; } public int WeaponInstance { get; private set; } public int ObservedRows { get; private set; } }",
            "    internal sealed class CapturedSubwayLootEvidenceDefinition { public CapturedSubwayLootEvidenceDefinition(int lowId, int highId, int quality, int observedCount, int observedCorpses, int observedBasisPoints) { this.LowId = lowId; this.HighId = highId; this.Quality = quality; this.ObservedCount = observedCount; this.ObservedCorpses = observedCorpses; this.ObservedBasisPoints = observedBasisPoints; } public int LowId { get; private set; } public int HighId { get; private set; } public int Quality { get; private set; } public int ObservedCount { get; private set; } public int ObservedCorpses { get; private set; } public int ObservedBasisPoints { get; private set; } }",
            "    internal sealed class CapturedSubwayCorpseEvidenceDefinition { public CapturedSubwayCorpseEvidenceDefinition(string capture, string capturedUtc, string corpseIdentity, string deadNpcIdentity, int enemyLevel, int monsterData, int catMesh, int credits) { this.Capture = capture; this.CapturedUtc = capturedUtc; this.CorpseIdentity = corpseIdentity; this.DeadNpcIdentity = deadNpcIdentity; this.EnemyLevel = enemyLevel; this.MonsterData = monsterData; this.CatMesh = catMesh; this.Credits = credits; } public string Capture { get; private set; } public string CapturedUtc { get; private set; } public string CorpseIdentity { get; private set; } public string DeadNpcIdentity { get; private set; } public int EnemyLevel { get; private set; } public int MonsterData { get; private set; } public int CatMesh { get; private set; } public int Credits { get; private set; } }",
            "}",
            "",
        ]
    )
    return "\n".join(lines)


def canonicalize_checked_content(value: bytes) -> str:
    text = value.decode("utf-8-sig").replace("\r\n", "\n").replace("\r", "\n")
    return text[:-1] if text.endswith("\n") else text


def check_output(generated: bytes) -> None:
    if not OUTPUT.exists():
        raise SystemExit("generated provider is missing: " + str(OUTPUT))
    checked_in = OUTPUT.read_bytes()
    checked_content = canonicalize_checked_content(checked_in)
    generated_content = canonicalize_checked_content(generated)
    if checked_content != generated_content:
        checked_lines = checked_content.splitlines()
        generated_lines = generated_content.splitlines()
        first_difference = next(
            (
                index
                for index in range(max(len(checked_lines), len(generated_lines)))
                if index >= len(checked_lines)
                or index >= len(generated_lines)
                or checked_lines[index] != generated_lines[index]
            ),
            0,
        )
        raise SystemExit(
            "generated provider is stale at line {0}; checked-in={1!r} generated={2!r}; run with --write: {3}".format(
                first_difference + 1,
                checked_lines[first_difference][:160]
                if first_difference < len(checked_lines)
                else "<missing>",
                generated_lines[first_difference][:160]
                if first_difference < len(generated_lines)
                else "<missing>",
                OUTPUT,
            )
        )


def write_output_atomically(generated: bytes) -> None:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=OUTPUT.name + ".",
        suffix=".tmp",
        dir=str(OUTPUT.parent),
    )
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(generated)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(str(temporary_path), str(OUTPUT))
    finally:
        if temporary_path.exists():
            temporary_path.unlink()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate capture-backed ordinary Subway content."
    )
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument(
        "--check",
        action="store_true",
        help="validate and content-compare the checked-in provider without writing",
    )
    mode.add_argument(
        "--write",
        action="store_true",
        help="validate and atomically replace the checked-in provider",
    )
    mode.add_argument(
        "--dry-run",
        action="store_true",
        help="validate normalized profile/spawn input and content equivalence without writing",
    )
    return parser.parse_args()


def main() -> None:
    arguments = parse_args()
    generated = generate().replace("\n", "\r\n").encode("utf-8")
    if arguments.write:
        write_output_atomically(generated)
        print(f"generated {OUTPUT}")
        return
    check_output(generated)
    print(f"PASS content-equivalent {OUTPUT}")


if __name__ == "__main__":
    main()
