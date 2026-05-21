#!/usr/bin/env python3
"""Build review-only mob loot seed files from live loot observations.

This does not connect to MySQL and does not mutate the CellAO database. It reads
loot_body_observations.csv / loot_drop_observations.csv files produced by
Export-LiveLootObservations.py, correlates them with the checked-in
mobtemplate.sql, and writes:

- ObservedLiveLootSeed.csv
- ObservedLiveLootSeed.review.sql

The SQL file is intentionally review-only. Apply nothing from it until the
observed sample sizes and chosen rates are acceptable.
"""

from __future__ import annotations

import argparse
import csv
import re
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable


@dataclass
class MobTemplate:
    hash: str
    name: str
    min_level: int
    max_level: int
    npc_family: int
    health: int
    monster_data: int


@dataclass(frozen=True)
class ItemKey:
    low_id: int
    high_id: int
    name: str


@dataclass
class ItemObservation:
    bodies: set[str] = field(default_factory=set)
    qualities: list[int] = field(default_factory=list)


@dataclass
class MobObservation:
    enemy_name: str
    bodies: set[str] = field(default_factory=set)
    levels: list[int] = field(default_factory=list)
    max_health_values: list[int] = field(default_factory=list)
    credits: list[int] = field(default_factory=list)
    items: dict[ItemKey, ItemObservation] = field(default_factory=dict)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "captures",
        nargs="*",
        type=Path,
        help="Capture folders or loot observation CSV files. Defaults to tools-temp/live-pcaps/** observations.",
    )
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path(__file__).resolve().parents[2],
        help="CellAO repo root.",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=None,
        help="Output directory. Defaults to CellAO/Documentation/MobLootCoverage.",
    )
    return parser.parse_args()


def int_or_none(value: str | None) -> int | None:
    if value is None or str(value).strip() == "":
        return None

    try:
        return int(float(str(value).strip()))
    except ValueError:
        return None


def sql_string(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


def split_sql_values(payload: str) -> list[str]:
    values: list[str] = []
    current: list[str] = []
    in_quote = False
    quote = ""
    i = 0
    while i < len(payload):
        ch = payload[i]
        if in_quote:
            if ch == "\\" and i + 1 < len(payload):
                current.append(payload[i + 1])
                i += 2
                continue
            if ch == quote:
                if i + 1 < len(payload) and payload[i + 1] == quote:
                    current.append(quote)
                    i += 2
                    continue
                in_quote = False
                i += 1
                continue
            current.append(ch)
            i += 1
            continue

        if ch in ("'", '"'):
            in_quote = True
            quote = ch
            i += 1
            continue

        if ch == ",":
            values.append("".join(current).strip())
            current = []
            i += 1
            continue

        current.append(ch)
        i += 1

    values.append("".join(current).strip())
    return values


def load_mob_templates(repo_root: Path) -> dict[str, MobTemplate]:
    path = repo_root / "CellAO" / "Libraries" / "Source" / "CellAO.Database" / "SqlTables" / "mobtemplate.sql"
    templates: dict[str, MobTemplate] = {}
    if not path.exists():
        return templates

    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        if not line.lower().startswith("insert into `mobtemplate`"):
            continue

        match = re.search(r"VALUES\s*\((.*)\)\s*;?\s*$", line, re.IGNORECASE)
        if not match:
            continue

        values = split_sql_values(match.group(1))
        if len(values) < 13:
            continue

        name = values[8]
        templates[name.lower()] = MobTemplate(
            hash=values[0],
            name=name,
            min_level=int(values[1]),
            max_level=int(values[2]),
            npc_family=int(values[10]),
            health=int(values[11]),
            monster_data=int(values[12]),
        )

    return templates


def observation_files(inputs: Iterable[Path], repo_root: Path) -> list[Path]:
    paths = list(inputs)
    if not paths:
        paths = list((repo_root / "tools-temp" / "live-pcaps").glob("**/loot_drop_observations.csv"))

    files: list[Path] = []
    for path in paths:
        if path.is_dir():
            candidate = path / "loot_drop_observations.csv"
            if candidate.exists():
                files.append(candidate)
            continue

        if path.name == "loot_drop_observations.csv" and path.exists():
            files.append(path)

    return sorted(set(files))


def body_key(row: dict[str, str]) -> str:
    return f"{row.get('capture', '')}|{row.get('corpse', '')}"


def read_observations(drop_files: Iterable[Path]) -> dict[str, MobObservation]:
    observations: dict[str, MobObservation] = {}

    for drop_file in drop_files:
        body_file = drop_file.with_name("loot_body_observations.csv")
        if body_file.exists():
            with body_file.open("r", encoding="utf-8-sig", newline="") as handle:
                for row in csv.DictReader(handle):
                    enemy_name = (row.get("enemy_name") or "").strip()
                    if not enemy_name:
                        continue

                    mob = observations.setdefault(enemy_name.lower(), MobObservation(enemy_name=enemy_name))
                    key = body_key(row)
                    mob.bodies.add(key)

                    level = int_or_none(row.get("enemy_level"))
                    if level is not None:
                        mob.levels.append(level)

                    max_health = int_or_none(row.get("enemy_max_health"))
                    if max_health is not None:
                        mob.max_health_values.append(max_health)

                    credits = int_or_none(row.get("credits"))
                    if credits is not None:
                        mob.credits.append(credits)

        with drop_file.open("r", encoding="utf-8-sig", newline="") as handle:
            for row in csv.DictReader(handle):
                enemy_name = (row.get("enemy_name") or "").strip()
                if not enemy_name:
                    continue

                mob = observations.setdefault(enemy_name.lower(), MobObservation(enemy_name=enemy_name))
                mob.bodies.add(body_key(row))

                if (row.get("loot_kind") or "").strip().lower() != "item":
                    continue

                low_id = int_or_none(row.get("low_id"))
                high_id = int_or_none(row.get("high_id")) or low_id
                quality = int_or_none(row.get("loot_quality"))
                if low_id is None or high_id is None or quality is None:
                    continue

                item = ItemKey(
                    low_id=low_id,
                    high_id=high_id,
                    name=(row.get("loot_name") or "").strip(),
                )
                item_observation = mob.items.setdefault(item, ItemObservation())
                item_observation.bodies.add(body_key(row))
                item_observation.qualities.append(quality)

    return observations


def min_or_blank(values: list[int]) -> int | str:
    return min(values) if values else ""


def max_or_blank(values: list[int]) -> int | str:
    return max(values) if values else ""


def average_or_blank(values: list[int]) -> str:
    return f"{sum(values) / len(values):.2f}" if values else ""


def drop_group_hash(template_hash: str, index: int) -> str:
    return f"OBS{template_hash.upper()}{index:02d}"


def build_rows(observations: dict[str, MobObservation], templates: dict[str, MobTemplate]) -> list[dict[str, object]]:
    rows: list[dict[str, object]] = []

    for key in sorted(observations):
        observation = observations[key]
        template = templates.get(key)
        body_count = len(observation.bodies)
        sorted_items = sorted(
            observation.items.items(),
            key=lambda item: (item[0].name.lower(), item[0].low_id, item[0].high_id),
        )

        if not sorted_items:
            rows.append(
                base_row(observation, template, body_count)
                | {
                    "drop_group_hash": "",
                    "item_name": "",
                    "low_id": "",
                    "high_id": "",
                    "min_ql": "",
                    "max_ql": "",
                    "observed_item_bodies": 0,
                    "observed_drop_rate_basis_points": 0,
                    "sql_ready": False,
                }
            )
            continue

        for index, (item, item_observation) in enumerate(sorted_items):
            observed_bodies = len(item_observation.bodies)
            rate = 0 if body_count == 0 else round((observed_bodies / body_count) * 10000)
            rows.append(
                base_row(observation, template, body_count)
                | {
                    "drop_group_hash": drop_group_hash(template.hash, index) if template else "",
                    "item_name": item.name,
                    "low_id": item.low_id,
                    "high_id": item.high_id,
                    "min_ql": min_or_blank(item_observation.qualities),
                    "max_ql": max_or_blank(item_observation.qualities),
                    "observed_item_bodies": observed_bodies,
                    "observed_drop_rate_basis_points": rate,
                    "sql_ready": template is not None and item.low_id > 0,
                }
            )

    return rows


def base_row(observation: MobObservation, template: MobTemplate | None, body_count: int) -> dict[str, object]:
    return {
        "enemy_name": observation.enemy_name,
        "template_hash": template.hash if template else "",
        "template_found": template is not None,
        "template_min_level": template.min_level if template else "",
        "template_max_level": template.max_level if template else "",
        "template_health": template.health if template else "",
        "template_monster_data": template.monster_data if template else "",
        "observed_body_count": body_count,
        "observed_min_level": min_or_blank(observation.levels),
        "observed_max_level": max_or_blank(observation.levels),
        "observed_min_health": min_or_blank(observation.max_health_values),
        "observed_max_health": max_or_blank(observation.max_health_values),
        "credits_min": min_or_blank(observation.credits),
        "credits_max": max_or_blank(observation.credits),
        "credits_average": average_or_blank(observation.credits),
    }


def write_csv(path: Path, rows: list[dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fieldnames = [
        "enemy_name",
        "template_hash",
        "template_found",
        "template_min_level",
        "template_max_level",
        "template_health",
        "template_monster_data",
        "observed_body_count",
        "observed_min_level",
        "observed_max_level",
        "observed_min_health",
        "observed_max_health",
        "credits_min",
        "credits_max",
        "credits_average",
        "drop_group_hash",
        "item_name",
        "low_id",
        "high_id",
        "min_ql",
        "max_ql",
        "observed_item_bodies",
        "observed_drop_rate_basis_points",
        "sql_ready",
    ]
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def write_review_sql(path: Path, rows: list[dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    grouped: dict[str, list[dict[str, object]]] = defaultdict(list)
    for row in rows:
        if row["sql_ready"]:
            grouped[str(row["template_hash"])].append(row)

    lines = [
        "-- Review-only observed live loot seed.",
        "-- Do not run this file directly without checking sample sizes and rates.",
        "-- Generated from passive loot observation CSVs; it does not represent complete live drop tables.",
        "",
    ]

    for template_hash in sorted(grouped):
        items = grouped[template_hash]
        enemy_name = str(items[0]["enemy_name"])
        lines.append(f"-- {enemy_name} ({template_hash}), observed bodies: {items[0]['observed_body_count']}")

        hashes: list[str] = []
        slots: list[str] = []
        rates: list[str] = []
        for slot, row in enumerate(items):
            hash_value = str(row["drop_group_hash"])
            hashes.append(hash_value)
            slots.append(str(slot))
            rates.append(str(row["observed_drop_rate_basis_points"]))
            lines.append(
                "INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES "
                f"({sql_string(hash_value)}, {row['low_id']}, {row['high_id']}, {row['min_ql']}, {row['max_ql']}, 0); "
                f"-- {row['item_name']} observed on {row['observed_item_bodies']} bodies"
            )

        lines.append(
            "-- UPDATE `mobtemplate` SET "
            f"`DropHashes`={sql_string(','.join(hashes))}, "
            f"`DropSlots`={sql_string(','.join(slots))}, "
            f"`DropRates`={sql_string(','.join(rates))} "
            f"WHERE `Hash`={sql_string(template_hash)};"
        )
        lines.append("")

    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    args = parse_args()
    repo_root = args.repo_root.resolve()
    output_dir = args.output_dir or repo_root / "CellAO" / "Documentation" / "MobLootCoverage"

    files = observation_files(args.captures, repo_root)
    templates = load_mob_templates(repo_root)
    observations = read_observations(files)
    rows = build_rows(observations, templates)

    csv_path = output_dir / "ObservedLiveLootSeed.csv"
    sql_path = output_dir / "ObservedLiveLootSeed.review.sql"
    write_csv(csv_path, rows)
    write_review_sql(sql_path, rows)

    print(f"Observation files: {len(files)}")
    print(f"Observed mobs: {len(observations)}")
    print(f"Seed rows: {len(rows)}")
    print(f"Wrote {csv_path}")
    print(f"Wrote {sql_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
