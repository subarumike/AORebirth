#!/usr/bin/env python3
"""Build the next mission capture wave from the retained destination corpus.

This is an offline evidence/planning generator. It never launches AO, contacts a
server, changes runtime mission behavior, or treats observed frequencies as the
server's probability model.
"""

from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import csv
import gzip
import hashlib
import io
import json
import math
from pathlib import Path
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parent.parent
ELIGIBILITY = ROOT / "docs/generated/missions/destination-eligibility-analysis"
INVENTORY = ELIGIBILITY / "mission-offer-analysis-inventory.jsonl.gz"
ELIGIBILITY_SUMMARY = ELIGIBILITY / "mission-destination-eligibility-summary.json"
ELIGIBILITY_MANIFEST = ELIGIBILITY / "mission-destination-eligibility-manifest.json"
MISSION_LEVELS = ROOT / "AORebirth/Server/ZoneEngine/XML Data/MissionLevels.csv"
HELPBOT = ROOT / "docs/evidence/data/helpbot-mission-ql-levels-1-149.json"
LEGACY_PLAN = ROOT / "docs/mission-harvest/mission-ql-1-250-plan.json"
PLAYFIELDS = ROOT / "AORebirth/Server/ZoneEngine/XML Data/Playfields.xml"
HARVESTER = ROOT / "Tools/AOSharpMissionOfferHarvester/Main.cs"
SLIDERS = ROOT / "Tools/AOSharpMissionOfferHarvester/MissionSliderState.cs"
OUTPUT = ROOT / "docs/generated/missions/capture-wave-plan"
REPORT = ROOT / "docs/evidence/MISSION_CAPTURE_WAVE_FROM_PROVEN_COVERAGE_GAPS.md"

SCHEMA_VERSION = 1
CAPTURED_LEVELS = (2, 7, 13, 25, 35, 37)
HIGH_TABLE_ONLY_QLS = (194, 203, 209, 213, 221, 228, 229, 230, 231, 233, 234, 240, 241, 242, 244, 247, 249)
BORDER_GAP_QLS = (34, 36, 39, 41, 43, 46, 47, 49, 50, 51, 53, 54, 56, 57, 58, 59, 60, 61, 63, 64, 65, 67)
BROAD_TARGETS = tuple(
    list(BORDER_GAP_QLS)
    + [75, 85, 95, 105, 115, 125, 135, 145, 155, 165, 175, 185]
    + list(HIGH_TABLE_ONLY_QLS)
    + [250]
)
BROAD_REQUESTS = 50
CONTROL_REQUESTS = 40
FACTION_CONTROL_REQUESTS = 20
PRESET = "FIND_ITEM_PERSON_SUPPLEMENT"
INTERVAL = "1.5"


class PlanError(ValueError):
    pass


def canonical(value: object) -> str:
    return json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n"


def canonical_line(value: object) -> str:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False) + "\n"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def compact_ranges(values) -> str:
    values = sorted(set(int(value) for value in values))
    if not values:
        return "none"
    ranges = []
    start = previous = values[0]
    for value in values[1:]:
        if value == previous + 1:
            previous = value
            continue
        ranges.append(str(start) if start == previous else f"{start}-{previous}")
        start = previous = value
    ranges.append(str(start) if start == previous else f"{start}-{previous}")
    return ", ".join(ranges)


def load_table() -> dict[int, tuple[int, ...]]:
    with MISSION_LEVELS.open("r", encoding="utf-8", newline="") as stream:
        reader = csv.DictReader(stream)
        expected = ["Level", *[f"Q{index}" for index in range(11)], "Tokens"]
        if reader.fieldnames != expected:
            raise PlanError("MissionLevels.csv header is not canonical")
        table = {
            int(row["Level"]): tuple(int(row[f"Q{index}"]) for index in range(11))
            for row in reader
        }
    if sorted(table) != list(range(1, 221)):
        raise PlanError("MissionLevels.csv must contain levels 1 through 220")
    return table


def load_helpbot(table: dict[int, tuple[int, ...]]) -> dict[int, set[int]]:
    document = json.loads(HELPBOT.read_text(encoding="utf-8"))
    result = {}
    for row in document["levels"]:
        level = int(row["character_level"])
        if tuple(int(value) for value in row["derived_detent_qls"]) != table[level]:
            raise PlanError(f"Helpbot derived detents differ from MissionLevels.csv at level {level}")
        result[level] = set(int(value) for value in row["published_mission_qls"])
    return result


def load_inventory() -> list[dict[str, object]]:
    rows = []
    with gzip.open(INVENTORY, "rt", encoding="utf-8", newline="") as stream:
        for line in stream:
            rows.append(json.loads(line))
    return rows


def load_playfield_names() -> dict[int, str]:
    root = ET.parse(PLAYFIELDS).getroot()
    result = {}
    for element in root.findall("Playfield"):
        name = element.findtext("Name")
        if name:
            result[int(element.attrib["id"])] = name
    return result


def evidence_edges(
    table: dict[int, tuple[int, ...]], helpbot: dict[int, set[int]], ql: int
) -> list[dict[str, object]]:
    rows = []
    for level in range(2, 221):
        detents = [index + 1 for index, value in enumerate(table[level]) if value == ql]
        if not detents:
            continue
        rows.append(
            {
                "character_level": level,
                "difficulty_detents": detents,
                "evidence": "PROVEN_HELPBOT" if level in helpbot and ql in helpbot[level] else "LOCAL_TABLE_ONLY",
            }
        )
    return rows


def solve_set_cover(
    universe: set[int],
    table: dict[int, tuple[int, ...]],
    allowed_edges: dict[int, set[int]] | None = None,
    seed_levels: list[int] | None = None,
) -> dict[str, object]:
    levels = list(range(2, 221))
    coverage = {}
    for level in levels:
        values = set(table[level]) & universe
        if allowed_edges is not None:
            values = {ql for ql in values if level in allowed_edges.get(ql, set())}
        if values:
            coverage[level] = values
    unreachable = [ql for ql in sorted(universe) if not any(ql in values for values in coverage.values())]
    if unreachable:
        raise PlanError(f"Set-cover universe has unreachable QLs: {unreachable}")

    reduced = {}
    for level in sorted(coverage):
        values = coverage[level]
        if any(values < other_values for other_level, other_values in coverage.items() if other_level != level):
            continue
        if any(other_level < level and other_values == values for other_level, other_values in coverage.items()):
            continue
        reduced[level] = values

    remaining = set(universe)
    greedy_selected = []
    while remaining:
        level = min(reduced, key=lambda candidate: (-len(reduced[candidate] & remaining), candidate))
        if not reduced[level] & remaining:
            raise PlanError("Greedy set-cover construction stalled")
        greedy_selected.append(level)
        remaining -= reduced[level]

    selected = list(greedy_selected)
    if seed_levels:
        seed = sorted({level for level in seed_levels if level in coverage})
        seed_coverage = set().union(*(coverage[level] for level in seed)) if seed else set()
        if seed_coverage == universe and len(seed) < len(selected):
            selected = seed

    changed = True
    while changed:
        changed = False
        for level in sorted(selected, reverse=True):
            trial = [candidate for candidate in selected if candidate != level]
            trial_coverage = set().union(*(coverage[candidate] for candidate in trial)) if trial else set()
            if trial_coverage == universe:
                selected = trial
                changed = True

    best = sorted(selected)
    candidates_by_ql = {
        ql: tuple(level for level in sorted(reduced) if ql in reduced[level])
        for ql in sorted(universe)
    }
    node_limit = 500_000
    nodes = 0
    exhausted = True
    memo: dict[frozenset[int], int] = {}

    def search(covered: set[int], chosen: list[int]) -> None:
        nonlocal best, nodes, exhausted
        if nodes >= node_limit:
            exhausted = False
            return
        nodes += 1
        if len(chosen) >= len(best):
            return
        remaining_values = universe - covered
        if not remaining_values:
            best = sorted(chosen)
            return
        key = frozenset(remaining_values)
        if memo.get(key, 10**9) <= len(chosen):
            return
        memo[key] = len(chosen)
        max_gain = max(len(values & remaining_values) for values in reduced.values())
        if len(chosen) + math.ceil(len(remaining_values) / max_gain) >= len(best):
            return
        pivot = min(
            remaining_values,
            key=lambda ql: (
                sum(1 for level in candidates_by_ql[ql] if reduced[level] & remaining_values),
                ql,
            ),
        )
        candidates = sorted(
            candidates_by_ql[pivot],
            key=lambda level: (-len(reduced[level] & remaining_values), level),
        )
        for level in candidates:
            search(covered | reduced[level], chosen + [level])
            if not exhausted:
                return

    search(set(), [])
    selected = best
    covered = set().union(*(coverage[level] for level in selected))
    if covered != universe:
        raise PlanError("Set-cover witness does not cover the requested universe")
    lower_bound = math.ceil(len(universe) / max(len(values) for values in reduced.values()))
    return {
        "character_levels": selected,
        "character_count": len(selected),
        "covered_qls": sorted(covered),
        "certified_simple_lower_bound": lower_bound,
        "proof_status": "DETERMINISTIC_BRANCH_AND_BOUND_EXHAUSTED" if exhausted else "BEST_KNOWN_DETERMINISTIC_WITNESS_NODE_LIMIT_REACHED",
        "optimality_proven": exhausted,
        "nodes_visited": nodes,
        "node_limit": node_limit,
    }


def first_detent(table: dict[int, tuple[int, ...]], level: int, ql: int) -> int:
    return table[level].index(ql) + 1


def assign_targets(
    targets: set[int],
    roster: list[int],
    table: dict[int, tuple[int, ...]],
    helpbot: dict[int, set[int]],
) -> list[dict[str, object]]:
    load = Counter()
    assignments = []
    for ql in sorted(targets, key=lambda value: (sum(value in table[level] for level in roster), value)):
        candidates = [level for level in roster if ql in table[level]]
        if not candidates:
            raise PlanError(f"Roster cannot assign QL {ql}")
        level = min(
            candidates,
            key=lambda value: (
                0 if value in helpbot and ql in helpbot[value] else 1,
                load[value],
                value,
            ),
        )
        load[level] += 1
        detent = first_detent(table, level, ql)
        assignments.append(
            {
                "mission_ql": ql,
                "character_level": level,
                "difficulty_detent": detent,
                "edge_evidence": "PROVEN_HELPBOT" if level in helpbot and ql in helpbot[level] else "LOCAL_TABLE_ONLY",
                "request_count": BROAD_REQUESTS,
                "command": f"/missionharvest start {detent} {BROAD_REQUESTS} {PRESET} {INTERVAL}",
            }
        )
    return sorted(assignments, key=lambda row: (row["character_level"], row["mission_ql"]))


def destination_key(row: dict[str, object]):
    identity = row.get("destination_identity")
    if not identity:
        return None
    if isinstance(identity, dict):
        return (int(identity.get("type", 56006)), int(identity.get("instance_uint32", identity.get("instance", 0))))
    return (56006, int(identity))


def new_in_tail(values: list[object], window: int) -> int:
    if not values:
        return 0
    actual = min(window, len(values))
    earlier = set(values[:-actual])
    return len(set(values[-actual:]) - earlier)


def saturation_label(offers: int, new100: int, new1000: int) -> str:
    if offers == 0:
        return "NOT_CAPTURED"
    if offers < 100:
        return "LOW_SAMPLE"
    if offers >= 1000 and new100 == 0 and new1000 == 0:
        return "SATURATED_FOR_DISCOVERY"
    if offers >= 500 and new100 <= 1 and new1000 <= 3:
        return "STABILIZING"
    return "EXPANDING"


def ql_statistics(rows: list[dict[str, object]]) -> tuple[dict[int, dict[str, object]], dict[int, set[object]]]:
    grouped = defaultdict(list)
    for row in rows:
        ql = row.get("analysis_mission_ql")
        key = destination_key(row)
        if ql is not None and key is not None:
            grouped[int(ql)].append(row)
    result = {}
    destinations_by_ql = {}
    for ql in range(1, 251):
        current = sorted(
            grouped.get(ql, []),
            key=lambda row: (
                str(row.get("request_timestamp_utc") or ""),
                str(row.get("session_id") or ""),
                int(row.get("sequence") or 0),
                int(row.get("offer_index") or 0),
            ),
        )
        keys = [destination_key(row) for row in current]
        destinations = set(keys)
        destinations_by_ql[ql] = destinations
        new100 = new_in_tail(keys, 100)
        new500 = new_in_tail(keys, 500)
        new1000 = new_in_tail(keys, 1000)
        window = min(1000, len(keys))
        result[ql] = {
            "offer_count": len(current),
            "request_count": len({row["request_id"] for row in current}),
            "unique_destinations": len(destinations),
            "unique_destination_playfields": len({int(row["destination_playfield"]) for row in current}),
            "new_destinations_last_100": new100,
            "new_destinations_last_500": new500,
            "new_destinations_last_1000": new1000,
            "marginal_new_destinations_per_1000": round(1000.0 * new1000 / window, 6) if window else None,
            "marginal_window_offer_count": window,
            "saturation": saturation_label(len(current), new100, new1000),
        }
    return result, destinations_by_ql


def similarity(left: set[object], right: set[object]):
    union = left | right
    if not union:
        return None
    return round(len(left & right) / len(union), 6)


def recommendation_for(ql: int, captured: set[int]) -> tuple[str, str]:
    if ql in captured:
        return "P4", "ALREADY_CAPTURED_NO_BROAD_REPEAT"
    if 34 <= ql <= 67:
        return "P0", "CLOSE_CURRENT_QL_FRONTIER_GAP"
    if ql in HIGH_TABLE_ONLY_QLS:
        return "P0", "VALIDATE_LOCAL_TABLE_ONLY_HIGH_QL_EDGE"
    if ql in BROAD_TARGETS:
        return "P1", "BROAD_UNOBSERVED_QL_BAND_SAMPLE"
    return "P2", "DEFER_UNTIL_BROAD_WAVE_MARGINAL_YIELD_REVIEW"


def build_ql_matrix(
    table: dict[int, tuple[int, ...]],
    helpbot: dict[int, set[int]],
    stats: dict[int, dict[str, object]],
    destinations: dict[int, set[object]],
    captured: set[int],
) -> list[dict[str, object]]:
    matrix = []
    for ql in range(1, 251):
        edges = evidence_edges(table, helpbot, ql)
        priority, reason = recommendation_for(ql, captured)
        matrix.append(
            {
                "mission_ql": ql,
                "captured": ql in captured,
                **stats[ql],
                "previous_ql_destination_jaccard": similarity(destinations[ql], destinations[ql - 1]) if ql > 1 else None,
                "next_ql_destination_jaccard": similarity(destinations[ql], destinations[ql + 1]) if ql < 250 else None,
                "candidate_level_count": len(edges),
                "candidate_edges": edges,
                "recommended_priority": priority,
                "recommendation_reason": reason,
                "included_in_broad_wave": ql in BROAD_TARGETS,
                "probability_inference": "NOT_PERFORMED",
            }
        )
    return matrix


def slider_signature(row: dict[str, object]):
    sliders = row.get("secondary_sliders") or {}
    return tuple((name, (sliders.get(name) or {}).get("semantic_state")) for name in sorted(sliders))


def build_condition_coverage(rows: list[dict[str, object]]) -> list[dict[str, object]]:
    groups = defaultdict(list)
    for row in rows:
        ql = row.get("analysis_mission_ql")
        key = destination_key(row)
        if ql is None or key is None:
            continue
        condition = (
            int(ql), int(row.get("character_level") or -1), int(row.get("difficulty_detent") or -1),
            str(row.get("faction_side") or "UNKNOWN"),
            json.dumps(row.get("mission_terminal_identity"), sort_keys=True),
            json.dumps(row.get("terminal_playfield"), sort_keys=True), slider_signature(row),
        )
        groups[condition].append(row)
    output = []
    for condition, values in sorted(groups.items(), key=lambda item: item[0]):
        keys = [destination_key(row) for row in values]
        new100 = new_in_tail(keys, 100)
        new1000 = new_in_tail(keys, 1000)
        output.append(
            {
                "mission_ql": condition[0], "character_level": condition[1], "difficulty_detent": condition[2],
                "faction_side": condition[3], "terminal_identity": json.loads(condition[4]),
                "terminal_playfield": json.loads(condition[5]),
                "secondary_sliders": {name: state for name, state in condition[6]},
                "offer_count": len(values), "request_count": len({row["request_id"] for row in values}),
                "unique_destinations": len(set(keys)),
                "saturation": saturation_label(len(values), new100, new1000),
                "aggregation_boundary": "COHERENT_CONDITION_GROUP_ONLY",
            }
        )
    return output


def terminal_matrix(
    summary: dict[str, object], names: dict[int, str], offers: list[dict[str, object]]
) -> list[dict[str, object]]:
    result = []
    for row in summary["terminal_analysis"]["terminals"]:
        terminal = row["terminal"]
        playfield = int(terminal["playfield"]["instance_uint32"])
        identity = int(terminal["identity"]["instance_uint32"])
        coordinates = sorted(
            {
                (
                    float(offer["terminal_coordinates"]["x"]),
                    float(offer["terminal_coordinates"]["y"]),
                    float(offer["terminal_coordinates"]["z"]),
                )
                for offer in offers
                if offer.get("terminal_coordinates")
                and int((offer.get("mission_terminal_identity") or {}).get("instance_uint32", -1)) == identity
                and int((offer.get("terminal_playfield") or {}).get("instance_uint32", -1)) == playfield
            }
        )
        result.append(
            {
                "terminal_identity": terminal["identity"],
                "terminal_name": terminal["name"],
                "terminal_playfield": playfield,
                "terminal_region": names.get(playfield),
                "observed_terminal_local_xyz": [
                    {"x": value[0], "y": value[1], "z": value[2]} for value in coordinates
                ],
                "requests": row["requests"], "offers": row["offers"],
                "unique_destinations": row["unique_destinations"],
                "unique_destination_playfields": row["unique_destination_playfields"],
                "same_playfield_offers": row["same_playfield_offers"],
                "cross_playfield_distance_model": "UNAVAILABLE_UNPROVEN_COMMON_COORDINATE_SYSTEM",
                "backend_classification": "ORDINARY_MISSION_TERMINAL_INSTANCE_PROVENANCE_ONLY",
            }
        )
    return sorted(result, key=lambda row: row["terminal_playfield"])


def build_controls(table: dict[int, tuple[int, ...]]) -> list[dict[str, object]]:
    controls = []
    for level, playfield, terminal_id, purpose in (
        (25, 655, 3221226127, "LEVEL_CONTROL_AT_FIXED_TERMINAL"),
        (37, 655, 3221226127, "LEVEL_AND_TERMINAL_BASELINE"),
        (37, 800, 3221226272, "TERMINAL_GEOGRAPHY_CONTROL"),
    ):
        for ql in (25, 44):
            detent = first_detent(table, level, ql)
            controls.append(
                {
                    "character_level": level, "mission_ql": ql, "difficulty_detent": detent,
                    "terminal_playfield": playfield, "terminal_identity_instance": terminal_id,
                    "request_count": CONTROL_REQUESTS, "purpose": purpose,
                    "command": f"/missionharvest start {detent} {CONTROL_REQUESTS} {PRESET} {INTERVAL}",
                }
            )
    return controls


def build_faction_controls(table: dict[int, tuple[int, ...]]) -> list[dict[str, object]]:
    detent = first_detent(table, 37, 29)
    return [
        {
            "faction": faction, "character_level": 37, "mission_ql": 29,
            "difficulty_detent": detent, "terminal_playfield": 800,
            "terminal_identity_instance": 3221226272,
            "request_count": FACTION_CONTROL_REQUESTS,
            "command": f"/missionharvest start {detent} {FACTION_CONTROL_REQUESTS} {PRESET} {INTERVAL}",
            "execution_gate": "RUN_ONLY_IF_THE_EXACT_BOREALIS_TERMINAL_IS_ACCESSIBLE_TO_ALL_THREE_FACTIONS",
        }
        for faction in ("Omni", "Clan", "Neutral")
    ]


def build_character_history(rows: list[dict[str, object]]) -> list[dict[str, object]]:
    grouped = defaultdict(list)
    for row in rows:
        if row.get("character_identity_surrogate") and row.get("character_level") is not None:
            grouped[str(row["character_identity_surrogate"])].append(row)
    result = []
    for surrogate, values in sorted(grouped.items()):
        levels = sorted({int(row["character_level"]) for row in values})
        timestamps = sorted(str(row.get("request_timestamp_utc") or "") for row in values)
        result.append(
            {
                "character_identity_surrogate": surrogate,
                "historically_observed_levels": levels,
                "latest_observed_level": int(max(values, key=lambda row: str(row.get("request_timestamp_utc") or ""))["character_level"]),
                "first_observation_utc": timestamps[0], "last_observation_utc": timestamps[-1],
                "availability_now": "NOT_PROVEN_BY_OFFLINE_CORPUS",
            }
        )
    return result


def csv_matrix(matrix: list[dict[str, object]]) -> str:
    fields = [
        "mission_ql", "captured", "offer_count", "request_count", "unique_destinations",
        "unique_destination_playfields", "new_destinations_last_100", "new_destinations_last_500",
        "new_destinations_last_1000", "marginal_new_destinations_per_1000", "saturation",
        "previous_ql_destination_jaccard", "next_ql_destination_jaccard", "candidate_level_count",
        "candidate_levels", "candidate_detents", "recommended_priority", "recommendation_reason",
        "included_in_broad_wave", "probability_inference",
    ]
    stream = io.StringIO(newline="")
    writer = csv.DictWriter(stream, fieldnames=fields, lineterminator="\n")
    writer.writeheader()
    for row in matrix:
        flat = {key: row.get(key) for key in fields}
        flat["candidate_levels"] = ";".join(str(edge["character_level"]) for edge in row["candidate_edges"])
        flat["candidate_detents"] = ";".join(
            f'{edge["character_level"]}:{"/".join(map(str, edge["difficulty_detents"]))}'
            for edge in row["candidate_edges"]
        )
        writer.writerow(flat)
    return stream.getvalue()


def format_terminal_coordinates(row: dict[str, object]) -> str:
    return ", ".join(
        f'({value["x"]}, {value["y"]}, {value["z"]})'
        for value in row["observed_terminal_local_xyz"]
    )


def build_report(plan: dict[str, object], ql_matrix: list[dict[str, object]], terminals: list[dict[str, object]]) -> str:
    captured = plan["coverage"]["captured_qls"]
    missing = plan["coverage"]["missing_qls"]
    broad = plan["broad_wave"]
    mathematical = plan["all_missing_character_solutions"]["mathematical"]
    practical = plan["all_missing_character_solutions"]["practical_evidence_preserving"]
    observed_rows = [row for row in ql_matrix if row["captured"]]
    terminal_lines = "\n".join(
        f'- PF {row["terminal_playfield"]} ({row["terminal_region"]}), `{row["terminal_name"]}` '
        f'identity `{row["terminal_identity"]["instance_uint32"]}`, local XYZ '
        f'`{format_terminal_coordinates(row)}`: {row["offers"]:,} offers; instance provenance only.'
        for row in terminals
    )
    command_blocks = []
    grouped = defaultdict(list)
    for row in broad["assignments"]:
        grouped[row["character_level"]].append(row)
    for level in sorted(grouped):
        lines = [f"### Broad wave - character level {level}", "", "Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:", "", "```text"]
        for row in grouped[level]:
            lines.append(f'# QL {row["mission_ql"]} ({row["edge_evidence"]})')
            lines.append(row["command"])
            lines.append("/missionharvest status")
        lines.extend(["```", ""])
        command_blocks.extend(lines)
    control_blocks = []
    for row in plan["control_wave"]["assignments"]:
        control_blocks.extend(
            [f'### Control - level {row["character_level"]}, QL {row["mission_ql"]}, PF {row["terminal_playfield"]}', "", "```text", row["command"], "/missionharvest status", "```", ""]
        )

    lines = [
        "# Mission Capture Wave from Proven Coverage Gaps", "",
        "Generated deterministically from the retained destination-eligibility corpus and canonical mission-level graph. This is a capture plan only.", "",
        "## Outcome", "",
        f'- Current exact expected-QL coverage is **{len(captured)}/250**: `{compact_ranges(captured)}`.',
        f'- Missing expected QLs are **{len(missing)}** values: `{compact_ranges(missing)}`.',
        f'- The broad wave samples **{len(broad["target_qls"])}** missing QLs with **{broad["character_count"]}** level-locked characters, {BROAD_REQUESTS} requests / {BROAD_REQUESTS * 5} offered missions per target.',
        f'- The matched control wave reuses levels 25 and 37 at PF 655 and PF 800 for QLs 25 and 44: {plan["control_wave"]["request_count"]} requests total.',
        '- All commands use explicit detents. The plan does not depend on treating the response-side QL candidate as authoritative.', "",
        "| Wave | Variable isolated | QLs | Level-locked characters | Requests | Execution status |", 
        "| --- | --- | --- | ---: | ---: | --- |",
        f'| Broad QL discovery | expected QL | {len(broad["target_qls"])} missing values | {broad["character_count"]} | {broad["request_count"]} | ready |',
        f'| Matched level/terminal control | level or terminal geography | 25, 44 | 2 reusable levels | {plan["control_wave"]["request_count"]} | ready if exact saved levels remain |',
        f'| Faction control | faction at fixed PF 800 terminal | 29 | 3 faction characters | {FACTION_CONTROL_REQUESTS * 3} | conditional access gate |', "",
        "## Proven model and boundaries", "",
        "- Destination reconstruction baseline: `c09869d5028ad455569eef70c7a4abc86480b253`.",
        "- Destination eligibility baseline: `ec5c2ac9600fcaebead785009e6fc5590f9bb848`.",
        "- Terminal identity, terminal playfield, coordinates, side, level, detent, sliders, and complete five-offer cohorts are experimental provenance.",
        "- Terminal instances are not separate backend loot tables unless matched controls prove a difference. No backend variation is assumed.",
        "- Cross-playfield local-coordinate distances are not compared. Only within-playfield local distances remain valid.",
        "- Offer frequencies, Jaccard overlap, saturation labels, and marginal discovery are diagnostics, never inferred Funcom weights.",
        "- Duplicate offers in a five-offer cohort are preserved as evidence and require no special capture wave.", "",
        "## 1. Exact conditions already captured", "",
        f'- Exact raw-backed destination offers: **{plan["corpus"]["exact_destination_offers"]:,}** in **{plan["corpus"]["sessions"]}** sessions; unresolved offers: **{plan["corpus"]["unresolved_offers"]}**.',
        f'- Character levels: `{compact_ranges(CAPTURED_LEVELS)}`. Expected QLs: `{compact_ranges(captured)}`. Side: Omni only.',
        f'- Expected QL source: static mission-level graph. Captured secondary-slider inputs include centered, the `{PRESET}` supplement, and the completed level-2 one-variable matrix; the 174-row coherent-condition artifact preserves every exact combination separately.',
        f'- Coherent condition groups: **{plan["corpus"]["coherent_condition_groups"]}**. Aggregating them is allowed for coverage inventory, not for causal or probability claims.',
        terminal_lines, "",
        "## 2. QL gap and saturation matrix", "",
        "The authoritative 250-row matrix is `docs/generated/missions/capture-wave-plan/expected-ql-capture-gap-matrix.csv` (and JSON). It includes candidate levels/detents, exact counts, last-100/500/1000 discoveries, marginal discoveries per 1,000 offers, neighbor overlap, saturation, and priority.", "",
        f'- Captured QLs with nonzero observations: {len(observed_rows)}. Missing QLs: {len(missing)}.',
        '- Existing QLs marked saturated are saturated only for destination discovery under their coherent captured conditions; they are not probability-complete.', "",
        "## 3. Character-level solutions", "",
        f'- Mathematical all-missing solution: **{mathematical["character_count"]}** levels `{compact_ranges(mathematical["character_levels"])}`; `{mathematical["proof_status"]}`.',
        f'- Practical evidence-preserving all-missing solution: **{practical["character_count"]}** levels `{compact_ranges(practical["character_levels"])}`; `{practical["proof_status"]}`. Helpbot edges are required wherever available; local-table-only edges are used only for the 17 QLs without Helpbot proof.',
        f'- Executable broad-wave roster: **{broad["character_count"]}** levels `{compact_ranges(broad["character_levels"])}`.',
        '- Historical captured levels are observations, not proof those character snapshots still exist. Reuse an exact saved level only when Mike confirms it has not advanced.', "",
        "### KEEP / SAFE_TO_LEVEL", "",
        f'- `KEEP`: broad-wave levels `{compact_ranges(broad["character_levels"])}` until every assigned QL completes.',
        '- `KEEP`: the level-25 character and current level-37 character until the matched level/terminal controls complete.',
        '- `SAFE_TO_LEVEL_FOR_THIS_PLAN`: historical levels 2, 7, and 13 after confirming they are not one of the level-locked broad-wave characters. Existing capture evidence remains valid if they advance.',
        '- The level-35 observation and level-37 observation share one surrogate; the corpus proves that character advanced. Do not plan around a separate existing level-35 character.',
        '- Do not accept or complete missions during offer capture. A level-locked character must not gain XP until all commands assigned to that level are complete.', "",
        "## 4. Variable decisions", "",
        "1. **Mission QL:** primary next-wave dimension. Easy/Hard is operationally the detent selecting expected QL; no independent Easy/Hard effect is claimed.",
        "2. **Character level:** independently testable because 14 same-QL level comparisons exist, but none is proven causal. QLs 25 and 44 at reusable levels 25/37 are the next matched controls.",
        "3. **Terminal geography:** current data has PF 655 and PF 800 but zero same-level/QL/side/slider multi-terminal groups. Matched PF 655/PF 800 controls are required.",
        "4. **Faction:** every exact offer is Omni. Faction restriction/effect is unproven. A tiny three-faction PF 800 control is conditional on proving all three sides can use the exact same terminal; it is not part of the unconditional wave.",
        "5. **Secondary sliders:** the 27-state level-2 discovery is complete. Money/XP has a definite credits/XP compensation effect; destination effects remain only possible at discovery scale. Keep all six sliders fixed at the supplement preset in this wave.",
        "6. **Live mission QL:** AOSharp does not expose an authoritative field. `MissionInfo.UnkChunk3` bytes 16-19 are a strong candidate, with 67,405 matches, 10 mismatches, and 165 un-compared observations among 67,580 candidates; zero authoritative live decodes. Classification remains `STRONG_CANDIDATE_NOT_RUNTIME_PROMOTION`; no decoder change is justified.", "",
        "## 5. Terminal-region plan", "",
        "Use only the two terminal regions already proven by captured identity and playfield:", "",
        terminal_lines, "",
        "No new named terminal is invented. A same-playfield second-terminal experiment remains blocked until repository/capture evidence identifies its exact identity and position. PF 800 is new to the level-37 matched control, not a newly asserted backend region.", "",
        "## 6. Broad wave rationale", "",
        f'- Close every gap from QL 34 through 67, then sample 12 ten-QL-spaced points from 75 through 185, all 17 local-table-only high QLs, and QL 250. Total: {len(broad["target_qls"])} QLs.',
        '- This wave spans the whole unseen range without chasing all 205 missing QLs or all 2,242 placement records.',
        '- Each QL starts with 50 requests (normally 250 offers). That is a discovery sample, not an exhaustion or probability threshold.', "",
        "## 7. Adaptive stopping", "",
        "After the broad wave, regenerate this analysis before any extension:", "",
        "- Stop a QL when its last 500 offers add no destinations and its last 1,000 add at most one, unless it is a deliberate control.",
        "- Extend an `EXPANDING` QL by 50 requests only when it adds at least 4 destinations per latest 1,000 offers or opens a new destination playfield.",
        "- Stop a character level when all assigned target QLs satisfy their initial 50 requests; do not roll its other detents just because the character exists.",
        "- Stop terminal expansion after the PF 655/PF 800 matched cells unless destination/playfield support differs enough to justify a named follow-up hypothesis.",
        "- Preserve every complete five-offer cohort and duplicate. Never convert these rules into server probability claims.", "",
        "A cell is invalid for comparison if the character levels, the exact terminal identity/playfield, faction, detent, preset bytes, request/response linkage, or five-offer completeness differ from the plan. Retain invalid or partial raw evidence, but do not count it as a matched cell.", "",
        "## 8. Decision register", "",
        f'1. Captured conditions: {plan["corpus"]["coherent_condition_groups"]} coherent groups across levels `{compact_ranges(CAPTURED_LEVELS)}`, Omni side, and two proven terminals.',
        f'2. Captured/missing QLs: `{compact_ranges(captured)}` / `{compact_ranges(missing)}`.',
        f'3. Smallest mathematical witness found: {mathematical["character_count"]} characters; optimum not claimed unless its proof status says exhausted.',
        f'4. Practical all-missing set: {practical["character_count"]} characters with Helpbot edges preserved wherever available.',
        f'5. Next broad wave: {len(broad["target_qls"])} QLs, {broad["character_count"]} characters, {broad["request_count"]} requests.',
        '6. Terminal regions: proven PF 655 Andromeda and PF 800 Borealis only.',
        '7. New terminal locations: none asserted; PF 800 is a new matched condition for level 37.',
        '8. Terminal identity treatment: capture-instance provenance, not backend identity.',
        '9. Character level: independently testable; levels 25/37 at QLs 25/44 isolate it.',
        '10. Faction: unproven; only the gated three-side PF 800 control is recommended.',
        '11. Secondary sliders: discovery complete; freeze them for this wave.',
        '12. Easy/Hard: treat as expected-QL detent unless a future matched analysis proves an independent effect.',
        '13. Live QL field: strong UnkChunk3 candidate remains unpromoted; no decoder change.',
        '14. Per-QL saturation: recorded in the 250-row matrix with exact 100/500/1000 windows.',
        '15. Neighboring-QL similarity: Jaccard diagnostics recorded for previous and next QL; no pool equivalence inferred.',
        '16. Five-offer duplicates: preserve them; no extra duplicate-specific capture.',
        '17. Missing-QL priority: P0 frontier and local-table-only validation, P1 broad band, P2 deferred.',
        f'18. New broad-wave characters required: up to {broad["character_count"]}; none of those levels is proven currently available.',
        '19. Existing characters to keep: exact level 25 and current level 37 for controls; level 35 has already advanced in the corpus.',
        '20. Runbooks: explicit commands are grouped below by level and control cell.',
        f'21. Initial sample: {BROAD_REQUESTS} requests per broad QL and {CONTROL_REQUESTS} per matched control cell.',
        '22. Stop/continue: apply the marginal-yield and new-playfield rules above only after regeneration.',
        '23. Invalidating mismatches: level, terminal, side, detent, slider bytes, linkage, or cohort-size mismatch.',
        f'24. Shortest practical path selected: the proven-edge {broad["character_count"]}-character broad roster, followed by two reusable control levels.', "",
        "## 9. Ready-to-execute commands - broad wave", "",
        "These are AO chat commands for Mike to run. They are documented here and were not executed by Codex.", "",
        *command_blocks,
        "## 10. Ready-to-execute commands - matched controls", "",
        "Select the exact recorded terminal before each block and hold side Omni plus every secondary slider fixed.", "",
        *control_blocks,
        "## 11. Conditional faction control", "",
        "Run only after confirming the exact PF 800 terminal identity `3221226272` is usable by level-37 Omni, Clan, and Neutral characters. Otherwise skip; substituting different terminals would confound faction with geography.", "",
        "```text",
        *[f'# {row["faction"]}: QL 29\n{row["command"]}\n/missionharvest status' for row in plan["conditional_faction_control"]],
        "```", "",
        "## 12. Deterministic offline validation", "",
        "```cmd",
        "cmd /d /c Tools\\mission_capture_wave_planner.cmd --check",
        "cmd /d /c Tools\\test_mission_capture_wave_planner.cmd",
        "cmd /d /c tools\\generate_mission_level_graph.cmd --check",
        "cmd /d /c Tools\\mission_destination_eligibility_analysis.cmd generate --check",
        "```", "",
        "## 13. Required declarations", "",
        "```text",
        "LIVE_MISSION_CAPTURE_PERFORMED: NO",
        "RUNTIME_MISSION_LOGIC_CHANGED: NO",
        "TERMINAL_BACKEND_VARIATION_ASSUMED: NO",
        "TERMINAL_GEOGRAPHIC_COVERAGE_REQUIRED: YES",
        "DESTINATION_PROBABILITIES_INFERRED: NO",
        "```", "",
    ]
    return "\n".join(lines)


def build_outputs() -> tuple[dict[str, str], str]:
    table = load_table()
    helpbot = load_helpbot(table)
    rows = load_inventory()
    summary = json.loads(ELIGIBILITY_SUMMARY.read_text(encoding="utf-8"))
    legacy_plan = json.loads(LEGACY_PLAN.read_text(encoding="utf-8"))
    names = load_playfield_names()
    captured = set(int(value) for value in summary["mission_ql_availability"]["represented_values"])
    missing = set(range(1, 251)) - captured
    if len(captured) != 45 or len(missing) != 205:
        raise PlanError("Current QL coverage no longer matches the eligibility baseline")
    if not set(BROAD_TARGETS) <= missing:
        raise PlanError("Broad target list contains an already-captured QL")

    stats, destinations = ql_statistics(rows)
    ql_matrix = build_ql_matrix(table, helpbot, stats, destinations, captured)
    condition_rows = build_condition_coverage(rows)
    terminals = terminal_matrix(summary, names, rows)

    mathematical = solve_set_cover(
        missing,
        table,
        seed_levels=[int(value) for value in legacy_plan["mathematical_roster"]["character_levels"]],
    )
    allowed = {}
    for ql in missing:
        proven = {level for level in helpbot if level >= 2 and ql in helpbot[level] and ql in table[level]}
        allowed[ql] = proven or {level for level in range(2, 221) if ql in table[level]}
    practical = solve_set_cover(
        missing,
        table,
        allowed,
        seed_levels=[int(value) for value in legacy_plan["practical_roster"]["character_levels"]],
    )
    broad_allowed = {ql: allowed[ql] for ql in BROAD_TARGETS}
    broad_solution = solve_set_cover(set(BROAD_TARGETS), table, broad_allowed)
    broad_assignments = assign_targets(set(BROAD_TARGETS), broad_solution["character_levels"], table, helpbot)
    controls = build_controls(table)
    faction_controls = build_faction_controls(table)
    history = build_character_history(rows)

    if [row["mission_ql"] for row in ql_matrix] != list(range(1, 251)):
        raise PlanError("QL matrix is not an exact ordered 1-through-250 matrix")
    if len(condition_rows) != 174:
        raise PlanError("Coherent condition count differs from the accepted 174-group corpus")
    if [(row["terminal_playfield"], row["terminal_region"]) for row in terminals] != [(655, "Andromeda"), (800, "Borealis")]:
        raise PlanError("Captured terminal-region mapping changed")
    if {row["mission_ql"] for row in broad_assignments} != set(BROAD_TARGETS):
        raise PlanError("Broad assignments do not cover every target exactly once")

    plan = {
        "schema_version": SCHEMA_VERSION,
        "baselines": {
            "destination_reconstruction_sha": "c09869d5028ad455569eef70c7a4abc86480b253",
            "destination_eligibility_sha": "ec5c2ac9600fcaebead785009e6fc5590f9bb848",
        },
        "declarations": {
            "LIVE_MISSION_CAPTURE_PERFORMED": "NO",
            "RUNTIME_MISSION_LOGIC_CHANGED": "NO",
            "TERMINAL_BACKEND_VARIATION_ASSUMED": "NO",
            "TERMINAL_GEOGRAPHIC_COVERAGE_REQUIRED": "YES",
            "DESTINATION_PROBABILITIES_INFERRED": "NO",
        },
        "corpus": {
            "sessions": int(summary["populations"]["sessions"]),
            "total_offers": int(summary["populations"]["total_offers"]),
            "exact_destination_offers": int(summary["populations"]["RAW_BACKED_EXACT_DESTINATION"]),
            "unresolved_offers": int(summary["populations"]["NO_RAW_DESTINATION_UNRESOLVED"]),
            "coherent_condition_groups": len(condition_rows),
        },
        "coverage": {"captured_qls": sorted(captured), "missing_qls": sorted(missing)},
        "all_missing_character_solutions": {
            "mathematical": mathematical,
            "practical_evidence_preserving": practical,
        },
        "broad_wave": {
            "target_qls": sorted(BROAD_TARGETS),
            "character_levels": broad_solution["character_levels"],
            "character_count": broad_solution["character_count"],
            "proof_status": broad_solution["proof_status"],
            "requests_per_ql": BROAD_REQUESTS,
            "request_count": len(BROAD_TARGETS) * BROAD_REQUESTS,
            "expected_offer_count_if_all_cohorts_have_five_offers": len(BROAD_TARGETS) * BROAD_REQUESTS * 5,
            "assignments": broad_assignments,
        },
        "control_wave": {
            "character_levels": [25, 37],
            "target_qls": [25, 44],
            "terminal_playfields": [655, 800],
            "request_count": sum(row["request_count"] for row in controls),
            "assignments": controls,
        },
        "conditional_faction_control": faction_controls,
        "terminal_regions": terminals,
        "character_history": history,
        "live_mission_ql_assessment": {
            "authoritative_decoded_offers": int(summary["mission_ql_availability"]["live_decoded_offers"]),
            "candidate_offers": int(summary["mission_ql_availability"]["candidate_not_promoted_offers"]),
            "candidate_status_counts": summary["mission_ql_availability"]["candidate_status_counts"],
            "classification": "STRONG_CANDIDATE_NOT_RUNTIME_PROMOTION",
            "decoder_changed": False,
        },
        "slider_assessment": {
            "level2_27_state_discovery": "COMPLETE_NO_REPEAT_REQUIRED",
            "money_xp": "DEFINITE_CREDITS_AND_XP_COMPENSATION_EFFECT_REWARD_IDENTITY_AND_FORMULA_UNRESOLVED",
            "destination_effects": "POSSIBLE_NOT_PROVEN",
            "next_wave": f"FIX_ALL_SECONDARY_SLIDERS_AT_{PRESET}",
        },
    }

    outputs = {
        "capture-wave-plan.json": canonical(plan),
        "expected-ql-capture-gap-matrix.json": canonical({"schema_version": SCHEMA_VERSION, "rows": ql_matrix}),
        "expected-ql-capture-gap-matrix.csv": csv_matrix(ql_matrix),
        "coherent-condition-coverage.jsonl": "".join(canonical_line(row) for row in condition_rows),
        "terminal-coverage-matrix.json": canonical({"schema_version": SCHEMA_VERSION, "rows": terminals}),
    }
    report = build_report(plan, ql_matrix, terminals)
    manifest = {
        "schema_version": SCHEMA_VERSION,
        "generator": "Tools/mission_capture_wave_planner.py",
        "inputs": [
            {"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(path)}
            for path in (INVENTORY, ELIGIBILITY_SUMMARY, ELIGIBILITY_MANIFEST, MISSION_LEVELS, HELPBOT, LEGACY_PLAN, PLAYFIELDS, HARVESTER, SLIDERS)
        ],
        "outputs": [
            {"path": f"docs/generated/missions/capture-wave-plan/{name}", "sha256": hashlib.sha256(content.encode("utf-8")).hexdigest()}
            for name, content in sorted(outputs.items())
        ],
        "report": {
            "path": REPORT.relative_to(ROOT).as_posix(),
            "sha256": hashlib.sha256(report.encode("utf-8")).hexdigest(),
        },
        "runtime_mission_logic_changed": False,
    }
    outputs["manifest.json"] = canonical(manifest)
    return outputs, report


def write(outputs: dict[str, str], report: str) -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, content in outputs.items():
        (OUTPUT / name).write_text(content, encoding="utf-8", newline="\n")
    REPORT.write_text(report, encoding="utf-8", newline="\n")


def check(outputs: dict[str, str], report: str) -> None:
    stale = []
    for name, content in outputs.items():
        path = OUTPUT / name
        if not path.exists() or path.read_text(encoding="utf-8") != content:
            stale.append(path.relative_to(ROOT).as_posix())
    if not REPORT.exists() or REPORT.read_text(encoding="utf-8") != report:
        stale.append(REPORT.relative_to(ROOT).as_posix())
    if stale:
        raise SystemExit("STALE_MISSION_CAPTURE_WAVE_ARTIFACTS: " + ", ".join(stale))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write", action="store_true")
    mode.add_argument("--check", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    outputs, report = build_outputs()
    if args.write:
        write(outputs, report)
        print("MISSION_CAPTURE_WAVE_PLANNER_WRITE=PASS")
    else:
        check(outputs, report)
        print("MISSION_CAPTURE_WAVE_PLANNER_CHECK=PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
