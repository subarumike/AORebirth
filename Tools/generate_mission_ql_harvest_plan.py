#!/usr/bin/env python3
"""Generate and validate the complete live mission-QL harvesting runbook."""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
from pathlib import Path
import sys


REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
MISSION_TABLE = (
    REPOSITORY_ROOT
    / "AORebirth"
    / "Server"
    / "ZoneEngine"
    / "XML Data"
    / "MissionLevels.csv"
)
HELPBOT_REFERENCE = (
    REPOSITORY_ROOT
    / "docs"
    / "evidence"
    / "data"
    / "helpbot-mission-ql-levels-1-149.json"
)
OUTPUT_ROOT = REPOSITORY_ROOT / "docs" / "mission-harvest"
OUTPUT_MARKDOWN = OUTPUT_ROOT / "mission-ql-1-250-plan.md"
OUTPUT_ROLLABILITY = OUTPUT_ROOT / "mission-ql-rollability.csv"
OUTPUT_ASSIGNMENT = OUTPUT_ROOT / "mission-ql-assignment.csv"
OUTPUT_JSON = OUTPUT_ROOT / "mission-ql-1-250-plan.json"

TARGET_QLS = tuple(range(1, 251))
ELIGIBLE_LEVELS = tuple(range(2, 221))
REQUEST_COUNT = 1
EXPECTED_OFFERS_PER_COMPLETE_COHORT = 5
HELPBOT_REVISION_URL = (
    "https://wiki.aodb.us/index.php?title=Level_Parameters&oldid=44808"
)

# Integer-programming witness. The generator independently validates every
# covered QL and records the certified lower bound without claiming optimality.
MATHEMATICAL_ROSTER = (
    2,
    7,
    17,
    23,
    25,
    35,
    46,
    48,
    67,
    68,
    71,
    76,
    78,
    94,
    103,
    119,
    121,
    124,
    129,
    139,
    154,
    158,
    165,
    184,
    188,
    189,
    190,
    191,
    192,
    194,
    195,
    196,
    197,
    199,
    201,
    203,
    204,
    212,
    219,
    220,
)
PRACTICAL_ROSTER = (
    2,
    7,
    17,
    18,
    35,
    42,
    49,
    51,
    64,
    68,
    79,
    87,
    97,
    105,
    110,
    112,
    115,
    118,
    120,
    121,
    122,
    124,
    125,
    127,
    128,
    129,
    130,
    131,
    132,
    133,
    134,
    135,
    136,
    137,
    138,
    139,
    140,
    142,
    143,
    144,
    146,
    147,
    149,
    156,
    163,
    165,
    177,
    178,
    180,
    185,
    201,
    202,
    208,
    209,
)
MATHEMATICAL_LOWER_BOUND = 36
MATHEMATICAL_UPPER_BOUND = len(MATHEMATICAL_ROSTER)
MATHEMATICAL_PROOF_STATUS = (
    "ORTOOLS_CP_SAT_9_15_6755_FEASIBLE_40_BOUND_36_AFTER_1200_SECONDS"
)
PRACTICAL_PROOF_STATUS = (
    "SCIPY_HIGHS_MILP_OPTIMAL_ZERO_GAP_54_EVIDENCE_PRESERVING_CHARACTERS"
)


class PlanError(ValueError):
    pass


def canonical_json(value: object) -> bytes:
    return (json.dumps(value, indent=2, sort_keys=True) + "\n").encode("utf-8")


def load_table() -> tuple[dict[int, tuple[int, ...]], str]:
    raw = MISSION_TABLE.read_bytes()
    text = raw.decode("utf-8").replace("\r\n", "\n").replace("\r", "\n")
    if not text.endswith("\n"):
        raise PlanError("Mission-level table must end with a newline.")
    source_sha256 = hashlib.sha256(text.encode("utf-8")).hexdigest()
    with io.StringIO(text, newline="") as stream:
        reader = csv.DictReader(stream)
        expected = ["Level", *[f"Q{index}" for index in range(11)], "Tokens"]
        if reader.fieldnames != expected:
            raise PlanError("Mission-level table header is not canonical.")
        table = {
            int(row["Level"]): tuple(int(row[f"Q{index}"]) for index in range(11))
            for row in reader
        }
    if sorted(table) != list(range(1, 221)):
        raise PlanError("Mission-level table must contain exactly levels 1..220.")
    return table, source_sha256


def load_helpbot(table: dict[int, tuple[int, ...]]) -> tuple[dict[int, set[int]], str]:
    document = json.loads(HELPBOT_REFERENCE.read_text(encoding="utf-8"))
    rows = {
        int(row["character_level"]): set(row["published_mission_qls"])
        for row in document["levels"]
    }
    if sorted(rows) != list(range(1, 150)):
        raise PlanError("Helpbot reference must contain exactly levels 1..149.")
    for row in document["levels"]:
        level = int(row["character_level"])
        if tuple(row["derived_detent_qls"]) != table[level]:
            raise PlanError(f"Mission table differs from Helpbot at level {level}.")
    return rows, document["source"]["raw_wikitext_sha256"]


def first_slot(row: tuple[int, ...], target_ql: int) -> int:
    try:
        return row.index(target_ql) + 1
    except ValueError as error:
        raise PlanError(f"QL {target_ql} is absent from an assigned row.") from error


def level_coverage(table: dict[int, tuple[int, ...]], level: int) -> set[int]:
    return set(table[level])


def roster_coverage(table: dict[int, tuple[int, ...]], roster: tuple[int, ...]) -> set[int]:
    return set().union(*(level_coverage(table, level) for level in roster))


def validate_roster(
    table: dict[int, tuple[int, ...]], roster: tuple[int, ...], label: str
) -> None:
    if len(roster) != len(set(roster)):
        raise PlanError(f"{label} roster contains duplicate character levels.")
    if any(level not in ELIGIBLE_LEVELS for level in roster):
        raise PlanError(f"{label} roster contains an ineligible character level.")
    missing = set(TARGET_QLS) - roster_coverage(table, roster)
    if missing:
        raise PlanError(f"{label} roster misses QLs {sorted(missing)}.")


def build_rollability(
    table: dict[int, tuple[int, ...]], helpbot: dict[int, set[int]]
) -> tuple[list[dict[str, object]], list[dict[str, object]]]:
    edges: list[dict[str, object]] = []
    matrix: list[dict[str, object]] = []
    for ql in TARGET_QLS:
        capable_levels = []
        eligible_levels = []
        helpbot_levels = []
        for level in sorted(table):
            slots = [index + 1 for index, value in enumerate(table[level]) if value == ql]
            if not slots:
                continue
            capable_levels.append(level)
            if level in ELIGIBLE_LEVELS:
                eligible_levels.append(level)
            if level in helpbot and ql in helpbot[level]:
                helpbot_levels.append(level)
            for slot in slots:
                edges.append(
                    {
                        "mission_ql": ql,
                        "character_level": level,
                        "slider_position": slot,
                        "ordinary_terminal_eligible": level in ELIGIBLE_LEVELS,
                        "evidence_status": (
                            "PROVEN_HELPBOT"
                            if level in helpbot and ql in helpbot[level]
                            else "INFERRED_LOCAL_TABLE"
                        ),
                    }
                )
        matrix.append(
            {
                "mission_ql": ql,
                "capable_character_levels": capable_levels,
                "ordinary_terminal_eligible_character_levels": eligible_levels,
                "helpbot_proven_character_levels": helpbot_levels,
                "rollability_status": (
                    "PROVEN_HELPBOT_ROLLABLE"
                    if helpbot_levels
                    else (
                        "INFERRED_HIGH_LEVEL_TABLE_ROLLABLE"
                        if eligible_levels
                        else "UNROLLABLE"
                    )
                ),
            }
        )
    return edges, matrix


def build_assignments(
    table: dict[int, tuple[int, ...]],
    helpbot: dict[int, set[int]],
    roster: tuple[int, ...],
) -> list[dict[str, object]]:
    loads = {level: 0 for level in roster}
    candidates = {
        ql: [level for level in roster if ql in table[level]] for ql in TARGET_QLS
    }
    assignments: dict[int, dict[str, object]] = {}
    for ql in sorted(TARGET_QLS, key=lambda value: (len(candidates[value]), value)):
        if not candidates[ql]:
            raise PlanError(f"Practical roster cannot assign QL {ql}.")
        level = min(
            candidates[ql],
            key=lambda candidate: (
                0 if candidate in helpbot and ql in helpbot[candidate] else 1,
                loads[candidate],
                candidate,
            ),
        )
        slot = first_slot(table[level], ql)
        loads[level] += 1
        assignments[ql] = {
            "mission_ql": ql,
            "character_level": level,
            "slider_position": slot,
            "evidence_status": (
                "PROVEN_HELPBOT"
                if level in helpbot and ql in helpbot[level]
                else "INFERRED_LOCAL_TABLE"
            ),
            "request_count": REQUEST_COUNT,
            "expected_offers_per_complete_cohort": EXPECTED_OFFERS_PER_COMPLETE_COHORT,
            "harvest_command": f"/missionharvest start {ql} {REQUEST_COUNT}",
            "status_command": "/missionharvest status",
            "stop_command": "/missionharvest stop",
        }
    result = [assignments[ql] for ql in TARGET_QLS]
    if len(result) != 250 or len({item["mission_ql"] for item in result}) != 250:
        raise PlanError("Assignment does not contain every target QL exactly once.")
    unused_levels = [level for level, count in loads.items() if count == 0]
    if unused_levels:
        raise PlanError(f"Practical roster has unused levels: {unused_levels}.")
    return result


def csv_bytes(fieldnames: list[str], rows: list[dict[str, object]]) -> bytes:
    stream = io.StringIO(newline="")
    writer = csv.DictWriter(stream, fieldnames=fieldnames, lineterminator="\n")
    writer.writeheader()
    for row in rows:
        writer.writerow(row)
    return stream.getvalue().encode("utf-8")


def markdown(
    source_sha256: str,
    helpbot_sha256: str,
    matrix: list[dict[str, object]],
    assignments: list[dict[str, object]],
) -> bytes:
    proven_qls = [row["mission_ql"] for row in matrix if row["helpbot_proven_character_levels"]]
    inferred_qls = [row["mission_ql"] for row in matrix if not row["helpbot_proven_character_levels"]]
    unrollable = [row["mission_ql"] for row in matrix if row["rollability_status"] == "UNROLLABLE"]
    lines = [
        "# Mission QL 1-250 Live Harvest Plan",
        "",
        "Generated: 2026-09-01",
        "",
        "## Validation summary",
        "",
        "- TOTAL TARGET QLS = 250",
        f"- ROLLABLE = {250 - len(unrollable)}",
        f"- UNROLLABLE = {len(unrollable)} ({unrollable})",
        "- ASSIGNED = 250",
        "- DUPLICATE ASSIGNMENTS = 0",
        "- MISSING = 0",
        f"- Helpbot-proven target QLs = {len(proven_qls)}",
        f"- High-level local-table target QLs awaiting live confirmation = {inferred_qls}",
        f"- Mission table SHA-256 = `{source_sha256}`",
        f"- Helpbot raw source SHA-256 = `{helpbot_sha256}`",
        f"- Helpbot revision = {HELPBOT_REVISION_URL}",
        "",
        "## Character rosters",
        "",
        "Mathematical set-cover result:",
        "",
        f"- Certified lower bound = {MATHEMATICAL_LOWER_BOUND} characters",
        f"- Best valid upper bound = {MATHEMATICAL_UPPER_BOUND} characters",
        "- Exact optimum is unresolved; do not call the 40-character witness minimal",
        "",
        f"Best-known valid roster ({len(MATHEMATICAL_ROSTER)} characters):",
        "",
        "`" + ", ".join(map(str, MATHEMATICAL_ROSTER)) + "`",
        "",
        f"Proof status: `{MATHEMATICAL_PROOF_STATUS}`.",
        "",
        f"Recommended practical roster ({len(PRACTICAL_ROSTER)} characters):",
        "",
        "`" + ", ".join(map(str, PRACTICAL_ROSTER)) + "`",
        "",
        f"Proof status: `{PRACTICAL_PROOF_STATUS}`.",
        "",
        "The practical roster is the exact minimum-count evidence-preserving roster:",
        "every Helpbot-proven target is assigned through a pinned Helpbot level/QL",
        "edge, and only the 17 targets absent from Helpbot use high-level local-table",
        "edges. Its mixed-integer solve finished optimal with zero gap. Character",
        "level 2 is required for QL1 without relying on blocked level-1 terminal",
        "access; level 201 is required by the current table for QL221.",
        "",
        "## MissionHarvest contract",
        "",
        "1. Log into the exact listed character level.",
        "2. Select/use an ordinary Rubi-Ka mission terminal once.",
        "3. Run the listed target-QL command. The plugin resolves the first exact",
        "   matching one-based slot and sends nothing if the QL is absent.",
        "4. Wait for `requested_count_completed` feedback, then run status.",
        "5. Accept the target only when status reports `completeCohorts=1` and",
        "   `harvestedOffers` is positive. Otherwise rerun that exact target.",
        "6. `/missionharvest stop` safely stops a partial target and reports its",
        "   session/output summary.",
        "",
        "Output is written to",
        "`<AOSharp plugin local-data>\\sessions\\<session-id>\\events.jsonl`.",
        "One request is one terminal refresh and normally records five offers.",
        "Harvester 1.2 records the request-time terminal identity/playfield/coordinates,",
        "mission destination playfield/coordinates, capture-backed mission type, reward",
        "item low/high IDs and QL, title, description, credits, XP, and raw unknown fields",
        "for every offer. Complete per-roll capture does not prove that a finite sample",
        "has exhausted AO's possible items, destinations, or probabilities.",
        "",
        "## Complete QL-to-character rollability matrix",
        "",
        "| Mission QL | Ordinary-terminal eligible character levels | Evidence |",
        "| ---: | --- | --- |",
    ]
    for row in matrix:
        levels = ", ".join(map(str, row["ordinary_terminal_eligible_character_levels"]))
        lines.append(
            f"| {row['mission_ql']} | {levels} | `{row['rollability_status']}` |"
        )

    lines.extend(["", "## Complete copy/paste runbook", ""])
    by_level = {level: [] for level in PRACTICAL_ROSTER}
    for assignment in assignments:
        by_level[int(assignment["character_level"])].append(assignment)
    for level in PRACTICAL_ROSTER:
        lines.extend(
            [
                f"### Character level {level}",
                "",
                "Select/use an ordinary Rubi-Ka mission terminal, then run each target",
                "below separately.",
                "",
            ]
        )
        for assignment in sorted(by_level[level], key=lambda item: item["mission_ql"]):
            lines.extend(
                [
                    f"#### Target QL {assignment['mission_ql']} — exact slot {assignment['slider_position']}",
                    "",
                    f"Evidence status: `{assignment['evidence_status']}`.",
                    "",
                    "```text",
                    str(assignment["harvest_command"]),
                    "```",
                    "",
                    "Wait for completion feedback, then:",
                    "",
                    "```text",
                    "/missionharvest status",
                    "```",
                    "",
                ]
            )
    return ("\n".join(lines).rstrip("\n") + "\n").encode("utf-8")


def generate_outputs() -> dict[Path, bytes]:
    table, source_sha256 = load_table()
    helpbot, helpbot_sha256 = load_helpbot(table)
    validate_roster(table, MATHEMATICAL_ROSTER, "Best-known")
    validate_roster(table, PRACTICAL_ROSTER, "Practical")
    edges, matrix = build_rollability(table, helpbot)
    assignments = build_assignments(table, helpbot, PRACTICAL_ROSTER)

    unrollable = [
        row["mission_ql"] for row in matrix if row["rollability_status"] == "UNROLLABLE"
    ]
    if unrollable:
        raise PlanError(f"Complete table has unrollable QLs: {unrollable}.")
    expected_inferred = {
        ql
        for ql in TARGET_QLS
        if not any(ql in published_qls for published_qls in helpbot.values())
    }
    actual_inferred = {
        int(assignment["mission_ql"])
        for assignment in assignments
        if assignment["evidence_status"] == "INFERRED_LOCAL_TABLE"
    }
    if actual_inferred != expected_inferred:
        raise PlanError(
            "Practical assignment does not preserve every available Helpbot edge: "
            f"expected inferred {sorted(expected_inferred)}, got {sorted(actual_inferred)}."
        )
    for assignment in assignments:
        level = int(assignment["character_level"])
        ql = int(assignment["mission_ql"])
        slot = int(assignment["slider_position"])
        if table[level][slot - 1] != ql:
            raise PlanError(f"Assignment mismatch for QL {ql}.")

    assignment_rows = [
        {
            "mission_ql": item["mission_ql"],
            "character_level": item["character_level"],
            "slider_position": item["slider_position"],
            "rollable": "true",
            "evidence_status": item["evidence_status"],
            "request_count": item["request_count"],
            "expected_offers_per_complete_cohort": item[
                "expected_offers_per_complete_cohort"
            ],
            "harvest_command": item["harvest_command"],
        }
        for item in assignments
    ]
    document = {
        "schema_version": 1,
        "sources": {
            "mission_level_table": "AORebirth/Server/ZoneEngine/XML Data/MissionLevels.csv",
            "mission_level_table_sha256": source_sha256,
            "helpbot_reference": "docs/evidence/data/helpbot-mission-ql-levels-1-149.json",
            "helpbot_raw_source_sha256": helpbot_sha256,
            "helpbot_revision_url": HELPBOT_REVISION_URL,
        },
        "mathematical_roster": {
            "character_levels": list(MATHEMATICAL_ROSTER),
            "character_count": len(MATHEMATICAL_ROSTER),
            "certified_lower_bound": MATHEMATICAL_LOWER_BOUND,
            "valid_upper_bound": MATHEMATICAL_UPPER_BOUND,
            "optimality_proven": False,
            "proof_status": MATHEMATICAL_PROOF_STATUS,
        },
        "practical_roster": {
            "character_levels": list(PRACTICAL_ROSTER),
            "character_count": len(PRACTICAL_ROSTER),
            "proof_status": PRACTICAL_PROOF_STATUS,
            "selection_reason": "minimum character count subject to preserving every available Helpbot level and QL edge",
        },
        "rollability": matrix,
        "assignments": assignments,
        "validation": {
            "total_target_qls": 250,
            "rollable": 250,
            "unrollable": [],
            "assigned": 250,
            "duplicate_assignments": 0,
            "missing": [],
        },
    }
    return {
        OUTPUT_MARKDOWN: markdown(source_sha256, helpbot_sha256, matrix, assignments),
        OUTPUT_ROLLABILITY: csv_bytes(
            [
                "mission_ql",
                "character_level",
                "slider_position",
                "ordinary_terminal_eligible",
                "evidence_status",
            ],
            edges,
        ),
        OUTPUT_ASSIGNMENT: csv_bytes(
            [
                "mission_ql",
                "character_level",
                "slider_position",
                "rollable",
                "evidence_status",
                "request_count",
                "expected_offers_per_complete_cohort",
                "harvest_command",
            ],
            assignment_rows,
        ),
        OUTPUT_JSON: canonical_json(document),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    outputs = generate_outputs()
    if args.check:
        stale = [path for path, content in outputs.items() if not path.exists() or path.read_bytes() != content]
        if stale:
            print(
                "Mission QL harvest plan is missing or stale: "
                + ", ".join(str(path) for path in stale),
                file=sys.stderr,
            )
            return 1
    else:
        for path, content in outputs.items():
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)
    print(
        "MISSION_QL_HARVEST_PLAN=PASS targets=250 rollable=250 "
        "assigned=250 duplicates=0 missing=0"
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, PlanError, ValueError, KeyError, json.JSONDecodeError) as error:
        print(f"Mission QL harvest plan failed: {error}", file=sys.stderr)
        raise SystemExit(1)
