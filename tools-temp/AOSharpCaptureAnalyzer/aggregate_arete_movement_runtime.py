#!/usr/bin/env python3
"""Deterministically aggregate corrected Arete movement runtime datasets."""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
import math
import sys
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable


SCRIPT_DIR = Path(__file__).resolve().parent
REPOSITORY_ROOT = SCRIPT_DIR.parents[1]

BEHAVIORS = ("patrol", "spawn", "chase", "flee", "leash")
ANALYSIS_BEHAVIORS = BEHAVIORS + ("scripted",)
CAPTURED_ARETE_PLAYFIELD_ID = 1044525
RUNTIME_ARETE_PLAYFIELD_ID = 6553
SOURCE_SCHEMA_VERSION = 3
AGGREGATE_SCHEMA_VERSION = 4

DEFAULT_SOURCE_RUNTIME_DIRS = (
    REPOSITORY_ROOT
    / "docs"
    / "generated"
    / "arete_20260722_104809_movement"
    / "runtime",
    REPOSITORY_ROOT
    / "docs"
    / "generated"
    / "arete_20260722_152454_movement"
    / "runtime",
)
DEFAULT_OUTPUT_DIR = (
    REPOSITORY_ROOT
    / "AORebirth"
    / "Server"
    / "ZoneEngine"
    / "Content"
    / "Captured"
    / "Arete"
    / "movement-full"
)
DEFAULT_REPORT = (
    REPOSITORY_ROOT
    / "docs"
    / "generated"
    / "arete_full_corpus_movement_promotion_audit.md"
)

EXPECTED_SOURCE_PROMOTABLE = 20573
EXPECTED_RUNTIME_ROWS = 20267
EXPECTED_RUNTIME_BEHAVIORS = {
    "patrol": 18402,
    "spawn": 1384,
    "chase": 164,
    "flee": 54,
    "leash": 263,
}

SOURCE_COLUMNS = (
    "ObservationId",
    "EquivalentObservationCount",
    "CapturedUtc",
    "Sequence",
    "Behavior",
    "NpcFamily",
    "MonsterData",
    "Level",
    "CapturedPlayfieldId",
    "RuntimePlayfieldId",
    "Name",
    "SourceIdentity",
    "SourceGeneration",
    "RouteSignature",
    "StartX",
    "StartY",
    "StartZ",
    "EndX",
    "EndY",
    "EndZ",
    "DelayAfterSeconds",
    "PathCount",
)
OUTPUT_COLUMNS = (
    "ObservationId",
    "CaptureId",
) + SOURCE_COLUMNS[1:]


@dataclass(frozen=True)
class MovementSource:
    capture_id: str
    runtime_dir: Path
    runtime_manifest_path: Path
    runtime_manifest: dict[str, Any]
    analysis_manifest_path: Path
    analysis_manifest: dict[str, Any]
    runtime_rows: dict[str, list[dict[str, str]]]
    decision_reasons: dict[str, Counter[str]]
    input_paths: tuple[Path, ...]


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write", action="store_true")
    mode.add_argument("--check", action="store_true")
    parser.add_argument(
        "--source-runtime-dir",
        action="append",
        type=Path,
        dest="source_runtime_dirs",
        help="Corrected per-capture runtime directory; repeat for each capture.",
    )
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT_DIR)
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT)
    return parser.parse_args(argv)


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as stream:
        value = json.load(stream)
    if not isinstance(value, dict):
        raise RuntimeError(f"JSON object required: {relative_path(path)}")
    return value


def read_csv(path: Path, expected_columns: tuple[str, ...]) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        if tuple(reader.fieldnames or ()) != expected_columns:
            raise RuntimeError(f"header mismatch: {relative_path(path)}")
        return list(reader)


def render_csv(rows: Iterable[dict[str, str]]) -> bytes:
    stream = io.StringIO(newline="")
    writer = csv.DictWriter(stream, fieldnames=OUTPUT_COLUMNS, lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    return stream.getvalue().encode("utf-8")


def relative_path(path: Path) -> str:
    resolved = path.resolve()
    try:
        return resolved.relative_to(REPOSITORY_ROOT).as_posix()
    except ValueError:
        return resolved.as_posix()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while block := stream.read(1024 * 1024):
            digest.update(block)
    return digest.hexdigest()


def file_evidence(path: Path) -> dict[str, Any]:
    return {
        "path": relative_path(path),
        "bytes": path.stat().st_size,
        "sha256": sha256_file(path),
    }


def integer(value: Any, field: str) -> int:
    try:
        return int(str(value), 10)
    except (TypeError, ValueError) as exception:
        raise RuntimeError(f"invalid integer {field}: {value!r}") from exception


def validate_runtime_row(
    row: dict[str, str], behavior: str, path: Path, ordinal: int
) -> None:
    if row["Behavior"] != behavior:
        raise RuntimeError(
            f"behavior mismatch: {relative_path(path)} row {ordinal}"
        )
    if integer(row["CapturedPlayfieldId"], "CapturedPlayfieldId") != CAPTURED_ARETE_PLAYFIELD_ID:
        raise RuntimeError(
            f"captured playfield mismatch: {relative_path(path)} row {ordinal}"
        )
    if integer(row["RuntimePlayfieldId"], "RuntimePlayfieldId") != RUNTIME_ARETE_PLAYFIELD_ID:
        raise RuntimeError(
            f"runtime playfield mismatch: {relative_path(path)} row {ordinal}"
        )
    if not row["SourceIdentity"].startswith("SimpleChar:"):
        raise RuntimeError(
            f"source identity mismatch: {relative_path(path)} row {ordinal}"
        )
    if not row["ObservationId"] or not row["Name"] or not row["RouteSignature"]:
        raise RuntimeError(f"required evidence missing: {relative_path(path)} row {ordinal}")
    if integer(row["EquivalentObservationCount"], "EquivalentObservationCount") <= 0:
        raise RuntimeError(
            f"equivalent count invalid: {relative_path(path)} row {ordinal}"
        )
    if integer(row["SourceGeneration"], "SourceGeneration") < 0:
        raise RuntimeError(
            f"source generation invalid: {relative_path(path)} row {ordinal}"
        )
    if integer(row["PathCount"], "PathCount") <= 0:
        raise RuntimeError(f"path count invalid: {relative_path(path)} row {ordinal}")
    try:
        delay = float(row["DelayAfterSeconds"])
        coordinates = [
            float(row[column])
            for column in ("StartX", "StartY", "StartZ", "EndX", "EndY", "EndZ")
        ]
    except ValueError as exception:
        raise RuntimeError(
            f"geometry or timing invalid: {relative_path(path)} row {ordinal}"
        ) from exception
    if delay < 0.0 or not math.isfinite(delay) or any(
        not math.isfinite(value) for value in coordinates
    ):
        raise RuntimeError(
            f"geometry or timing invalid: {relative_path(path)} row {ordinal}"
        )


def load_source(runtime_dir: Path) -> MovementSource:
    runtime_dir = runtime_dir.resolve()
    runtime_manifest_path = runtime_dir / "manifest.json"
    analysis_manifest_path = runtime_dir.parent / "manifest.json"
    runtime_manifest = load_json(runtime_manifest_path)
    analysis_manifest = load_json(analysis_manifest_path)

    capture_id = str(runtime_manifest.get("captureId", "")).strip()
    if not capture_id or analysis_manifest.get("captureId") != capture_id:
        raise RuntimeError(f"capture identity mismatch: {relative_path(runtime_dir)}")
    if integer(runtime_manifest.get("schemaVersion"), "schemaVersion") != SOURCE_SCHEMA_VERSION:
        raise RuntimeError(f"unsupported runtime schema: {capture_id}")
    if integer(analysis_manifest.get("schemaVersion"), "schemaVersion") != SOURCE_SCHEMA_VERSION:
        raise RuntimeError(f"unsupported analysis schema: {capture_id}")
    if integer(runtime_manifest.get("capturedPlayfieldId"), "capturedPlayfieldId") != CAPTURED_ARETE_PLAYFIELD_ID:
        raise RuntimeError(f"captured playfield mismatch: {capture_id}")
    if integer(runtime_manifest.get("runtimePlayfieldId"), "runtimePlayfieldId") != RUNTIME_ARETE_PLAYFIELD_ID:
        raise RuntimeError(f"runtime playfield mismatch: {capture_id}")
    if integer(runtime_manifest.get("scriptedRuntimeRows"), "scriptedRuntimeRows") != 0:
        raise RuntimeError(f"scripted runtime rows are forbidden: {capture_id}")

    runtime_rows: dict[str, list[dict[str, str]]] = {}
    input_paths = [runtime_manifest_path, analysis_manifest_path]
    seen_ids: set[str] = set()
    runtime_behaviors = runtime_manifest.get("behaviors")
    analysis_behaviors = analysis_manifest.get("behaviors")
    if not isinstance(runtime_behaviors, dict) or not isinstance(analysis_behaviors, dict):
        raise RuntimeError(f"behavior manifest missing: {capture_id}")

    for behavior in BEHAVIORS:
        runtime_path = runtime_dir / f"{behavior}.csv"
        rows = read_csv(runtime_path, SOURCE_COLUMNS)
        input_paths.append(runtime_path)
        behavior_manifest = runtime_behaviors.get(behavior)
        if not isinstance(behavior_manifest, dict):
            raise RuntimeError(f"runtime behavior manifest missing: {capture_id}:{behavior}")
        if len(rows) != integer(behavior_manifest.get("runtimeRows"), "runtimeRows"):
            raise RuntimeError(f"runtime row count mismatch: {capture_id}:{behavior}")
        equivalent_total = 0
        for ordinal, row in enumerate(rows, start=2):
            validate_runtime_row(row, behavior, runtime_path, ordinal)
            if row["ObservationId"] in seen_ids:
                raise RuntimeError(
                    f"duplicate per-capture observation id: {capture_id}:{row['ObservationId']}"
                )
            seen_ids.add(row["ObservationId"])
            equivalent_total += integer(
                row["EquivalentObservationCount"], "EquivalentObservationCount"
            )
        source_observations = integer(
            behavior_manifest.get("sourceObservations"), "sourceObservations"
        )
        if equivalent_total != source_observations:
            raise RuntimeError(
                f"source observation reconciliation failed: {capture_id}:{behavior}"
            )
        analysis_behavior = analysis_behaviors.get(behavior)
        if not isinstance(analysis_behavior, dict) or integer(
            analysis_behavior.get("promotable"), "promotable"
        ) != source_observations:
            raise RuntimeError(
                f"analysis/runtime promotion mismatch: {capture_id}:{behavior}"
            )
        runtime_rows[behavior] = rows

    source_promotable = integer(
        runtime_manifest.get("sourcePromotableObservations"),
        "sourcePromotableObservations",
    )
    runtime_total = integer(
        runtime_manifest.get("deduplicatedRuntimeRows"),
        "deduplicatedRuntimeRows",
    )
    if source_promotable != sum(
        integer(runtime_behaviors[behavior]["sourceObservations"], "sourceObservations")
        for behavior in BEHAVIORS
    ):
        raise RuntimeError(f"source promotable total mismatch: {capture_id}")
    if source_promotable != integer(
        analysis_manifest.get("totals", {}).get("promotable"), "promotable"
    ):
        raise RuntimeError(f"analysis promotable total mismatch: {capture_id}")
    if runtime_total != sum(len(runtime_rows[behavior]) for behavior in BEHAVIORS):
        raise RuntimeError(f"runtime total mismatch: {capture_id}")

    decision_reasons: dict[str, Counter[str]] = {
        "Promotable": Counter(),
        "Ambiguous": Counter(),
        "Rejected": Counter(),
    }
    for behavior in ANALYSIS_BEHAVIORS:
        analysis_path = runtime_dir.parent / f"{behavior}.csv"
        with analysis_path.open("r", encoding="utf-8-sig", newline="") as stream:
            reader = csv.DictReader(stream)
            required = {"Behavior", "Disposition", "DecisionReasons"}
            if not required.issubset(reader.fieldnames or []):
                raise RuntimeError(f"analysis header mismatch: {relative_path(analysis_path)}")
            observed = Counter()
            for row in reader:
                if row["Behavior"] != behavior or row["Disposition"] not in decision_reasons:
                    raise RuntimeError(
                        f"analysis row mismatch: {relative_path(analysis_path)}"
                    )
                observed[row["Disposition"]] += 1
                reasons = [value for value in row["DecisionReasons"].split(";") if value]
                decision_reasons[row["Disposition"]].update(reasons or ["none_recorded"])
        input_paths.append(analysis_path)
        expected_behavior = analysis_behaviors.get(behavior)
        if not isinstance(expected_behavior, dict):
            raise RuntimeError(f"analysis behavior manifest missing: {capture_id}:{behavior}")
        for disposition in ("promotable", "ambiguous", "rejected"):
            if observed[disposition.title()] != integer(
                expected_behavior.get(disposition), disposition
            ):
                raise RuntimeError(
                    f"analysis disposition mismatch: {capture_id}:{behavior}:{disposition}"
                )

    return MovementSource(
        capture_id=capture_id,
        runtime_dir=runtime_dir,
        runtime_manifest_path=runtime_manifest_path,
        runtime_manifest=runtime_manifest,
        analysis_manifest_path=analysis_manifest_path,
        analysis_manifest=analysis_manifest,
        runtime_rows=runtime_rows,
        decision_reasons=decision_reasons,
        input_paths=tuple(input_paths),
    )


def output_row(capture_id: str, source: dict[str, str]) -> dict[str, str]:
    result = {
        "ObservationId": f"{capture_id}:{source['ObservationId']}",
        "CaptureId": capture_id,
    }
    result.update({column: source[column] for column in SOURCE_COLUMNS[1:]})
    return result


def row_sort_key(row: dict[str, str]) -> tuple[Any, ...]:
    return (
        row["CaptureId"],
        row["SourceIdentity"],
        integer(row["SourceGeneration"], "SourceGeneration"),
        row["CapturedUtc"],
        integer(row["Sequence"], "Sequence"),
        row["ObservationId"],
    )


def render_reason_table(counter: Counter[str]) -> list[str]:
    if not counter:
        return ["None.", ""]
    lines = [
        "| Exact decision reason | Observation incidences |",
        "| --- | ---: |",
    ]
    for reason, count in sorted(counter.items(), key=lambda item: (-item[1], item[0])):
        lines.append(f"| `{reason}` | {count:,} |")
    lines.append("")
    return lines


def render_report(
    sources: list[MovementSource],
    output_dir: Path,
    aggregate_rows: dict[str, list[dict[str, str]]],
) -> bytes:
    totals = Counter()
    behavior_totals: dict[str, Counter[str]] = {
        behavior: Counter() for behavior in ANALYSIS_BEHAVIORS
    }
    reason_totals = {
        "Promotable": Counter(),
        "Ambiguous": Counter(),
        "Rejected": Counter(),
    }
    for source in sources:
        totals.update(source.analysis_manifest["totals"])
        for behavior in ANALYSIS_BEHAVIORS:
            values = source.analysis_manifest["behaviors"][behavior]
            behavior_totals[behavior].update(
                {
                    key: integer(values[key], key)
                    for key in ("observations", "promotable", "ambiguous", "rejected")
                }
            )
        for disposition in reason_totals:
            reason_totals[disposition].update(source.decision_reasons[disposition])

    lines = [
        "# Arete full-corpus movement promotion audit",
        "",
        "## Result",
        "",
        (
            f"The two complete Arete movement captures reconcile deterministically to "
            f"**{sum(integer(source.analysis_manifest['reconciledObservations'], 'reconciledObservations') for source in sources):,}** "
            f"independently classified paths: **{totals['promotable']:,} promotable**, "
            f"**{totals['ambiguous']:,} ambiguous**, and **{totals['rejected']:,} rejected**."
        ),
        "",
        (
            f"The aggregate runtime dataset contains **{sum(len(rows) for rows in aggregate_rows.values()):,}** "
            f"deduplicated patrol, spawn, chase, flee, and leash observations. Scripted runtime rows: **0**."
        ),
        "",
        "## Evidence searched",
        "",
        "Complete corrected packet projections and their input hashes were read for:",
        "",
    ]
    for source in sources:
        lines.append(f"- Capture `{source.capture_id}`:")
        for evidence in source.analysis_manifest.get("inputs", []):
            lines.append(
                f"  - `{evidence['path']}` (`sha256 {evidence['sha256']}`)"
            )
        lines.extend(
            [
                f"  - `{relative_path(source.analysis_manifest_path)}`",
                f"  - `{relative_path(source.runtime_manifest_path)}`",
                f"  - six behavior analysis CSVs and five non-scripted runtime CSVs under `{relative_path(source.runtime_dir.parent)}`",
            ]
        )
    lines.extend(
        [
            "",
            "The corrected audit resolves identity metadata from the complete capture, including movement before SCFU; classifies and scores each observation before grouping; does not compare one packet's destination with the next packet's start as teleport evidence; and does not require a loop, repeated edge, or multiple runtime identities for patrol evidence.",
            "",
            "## Deterministic reconciliation",
            "",
            "| Capture | Reconciled | Promotable | Ambiguous | Rejected | Runtime rows | Route groups |",
            "| --- | ---: | ---: | ---: | ---: | ---: | ---: |",
        ]
    )
    for source in sources:
        analysis = source.analysis_manifest
        runtime = source.runtime_manifest
        lines.append(
            f"| `{source.capture_id}` | {integer(analysis['reconciledObservations'], 'reconciledObservations'):,} "
            f"| {integer(analysis['totals']['promotable'], 'promotable'):,} "
            f"| {integer(analysis['totals']['ambiguous'], 'ambiguous'):,} "
            f"| {integer(analysis['totals']['rejected'], 'rejected'):,} "
            f"| {integer(runtime['deduplicatedRuntimeRows'], 'deduplicatedRuntimeRows'):,} "
            f"| {integer(analysis['routeGroups'], 'routeGroups'):,} |"
        )
    lines.extend(
        [
            "",
            "| Behavior | Observations | Promotable | Ambiguous | Rejected | Aggregate runtime rows |",
            "| --- | ---: | ---: | ---: | ---: | ---: |",
        ]
    )
    for behavior in ANALYSIS_BEHAVIORS:
        values = behavior_totals[behavior]
        lines.append(
            f"| {behavior} | {values['observations']:,} | {values['promotable']:,} "
            f"| {values['ambiguous']:,} | {values['rejected']:,} "
            f"| {len(aggregate_rows.get(behavior, [])):,} |"
        )
    lines.extend(
        [
            "",
            "## Promotable observations",
            "",
            "Every promotable observation retains its captured family, template, level, captured/runtime playfield constraints, name, source identity, source generation, timestamp, sequence, route signature, coordinates, path count, and inter-observation delay. The only transformed fields are:",
            "",
            "- `CaptureId`, added to make regenerated runtime identities capture-scoped.",
            "- `ObservationId`, prefixed with `CaptureId:` to prevent cross-capture ID collisions.",
            "",
            "Per-capture exact equivalents remain collapsed by the corrected audit. Distinct observations from different captures are retained because capture provenance, timestamps, identities, or ordering make them separate evidence; the aggregator does not invent or splice routes.",
            "",
        ]
    )
    for behavior in BEHAVIORS:
        lines.append(
            f"- {behavior}: **{len(aggregate_rows[behavior]):,}** rows in `{relative_path(output_dir / (behavior + '.csv'))}`"
        )
    lines.extend(
        [
            "",
            "Combat and player influence is preserved in the matching chase, flee, and leash behavior class. It is not used to contaminate independently clean patrol or spawn observations.",
            "",
            "## Ambiguous observations — exact reasons",
            "",
            "These observations remain in the per-capture analysis datasets with confidence and exact reasons, but are not promoted to runtime. Counts below are reason incidences; a single observation can carry more than one reason.",
            "",
        ]
    )
    lines.extend(render_reason_table(reason_totals["Ambiguous"]))
    lines.extend(
        [
            "## Rejected observations — exact reasons",
            "",
            "Rejected rows remain traceable in the per-capture behavior CSVs. They cannot contaminate promotable rows in the same route group.",
            "",
        ]
    )
    lines.extend(render_reason_table(reason_totals["Rejected"]))
    lines.extend(
        [
            "## Remaining movement gaps",
            "",
            "- Scripted movement trigger semantics remain unresolved for the observations classified as scripted; all scripted rows are deliberately excluded from runtime.",
            "- Ambiguous observations remain unresolved only for the exact decision reasons above. The complete rows, confidence scores, influences, geometry, metadata resolution, and packet provenance remain available in the generated per-capture CSVs.",
            "- Rejected observations remain unsupported for promotion for their exact recorded reasons; no clean observation is rejected merely because another observation sharing its route group is bad.",
            "",
            "No available movement evidence was ignored because of a required loop, repeated edge, multiple identities, pre-existing runtime state, cross-packet destination comparison, or a rule that combat/player influence invalidates the appropriate combat movement class.",
            "",
            f"Aggregate manifest: `{relative_path(output_dir / 'manifest.json')}` (schema {AGGREGATE_SCHEMA_VERSION}).",
            "",
        ]
    )
    return "\n".join(lines).encode("utf-8")


def build_artifacts(
    source_runtime_dirs: Iterable[Path],
    output_dir: Path,
    report: Path,
    *,
    expected_source_promotable: int | None = EXPECTED_SOURCE_PROMOTABLE,
    expected_runtime_rows: int | None = EXPECTED_RUNTIME_ROWS,
    expected_runtime_behaviors: dict[str, int] | None = EXPECTED_RUNTIME_BEHAVIORS,
) -> tuple[dict[Path, bytes], dict[str, int]]:
    output_dir = output_dir.resolve()
    report = report.resolve()
    sources = sorted(
        (load_source(path) for path in source_runtime_dirs),
        key=lambda source: source.capture_id,
    )
    if not sources:
        raise RuntimeError("at least one source runtime dataset is required")
    capture_ids = [source.capture_id for source in sources]
    if len(capture_ids) != len(set(capture_ids)):
        raise RuntimeError("duplicate capture id in aggregate sources")

    aggregate_rows: dict[str, list[dict[str, str]]] = {}
    seen_output_ids: set[str] = set()
    for behavior in BEHAVIORS:
        rows = [
            output_row(source.capture_id, row)
            for source in sources
            for row in source.runtime_rows[behavior]
        ]
        rows.sort(key=row_sort_key)
        for row in rows:
            if row["ObservationId"] in seen_output_ids:
                raise RuntimeError(
                    f"aggregate observation id collision: {row['ObservationId']}"
                )
            seen_output_ids.add(row["ObservationId"])
        aggregate_rows[behavior] = rows

    source_promotable = sum(
        integer(
            source.runtime_manifest["sourcePromotableObservations"],
            "sourcePromotableObservations",
        )
        for source in sources
    )
    runtime_rows = sum(len(rows) for rows in aggregate_rows.values())
    if expected_source_promotable is not None and source_promotable != expected_source_promotable:
        raise RuntimeError(
            f"aggregate source promotable mismatch: {source_promotable} != {expected_source_promotable}"
        )
    if expected_runtime_rows is not None and runtime_rows != expected_runtime_rows:
        raise RuntimeError(
            f"aggregate runtime row mismatch: {runtime_rows} != {expected_runtime_rows}"
        )
    if expected_runtime_behaviors is not None:
        observed = {behavior: len(aggregate_rows[behavior]) for behavior in BEHAVIORS}
        if observed != expected_runtime_behaviors:
            raise RuntimeError(
                f"aggregate behavior reconciliation mismatch: {observed!r} != {expected_runtime_behaviors!r}"
            )

    artifacts = {
        output_dir / f"{behavior}.csv": render_csv(aggregate_rows[behavior])
        for behavior in BEHAVIORS
    }
    source_summaries = []
    all_input_paths: list[Path] = []
    for source in sources:
        all_input_paths.extend(source.input_paths)
        source_summaries.append(
            {
                "captureId": source.capture_id,
                "analysisManifest": relative_path(source.analysis_manifest_path),
                "runtimeManifest": relative_path(source.runtime_manifest_path),
                "sourcePromotableObservations": integer(
                    source.runtime_manifest["sourcePromotableObservations"],
                    "sourcePromotableObservations",
                ),
                "deduplicatedRuntimeRows": integer(
                    source.runtime_manifest["deduplicatedRuntimeRows"],
                    "deduplicatedRuntimeRows",
                ),
                "reconciledObservations": integer(
                    source.analysis_manifest["reconciledObservations"],
                    "reconciledObservations",
                ),
                "ambiguousObservations": integer(
                    source.analysis_manifest["totals"]["ambiguous"], "ambiguous"
                ),
                "rejectedObservations": integer(
                    source.analysis_manifest["totals"]["rejected"], "rejected"
                ),
            }
        )

    manifest = {
        "schemaVersion": AGGREGATE_SCHEMA_VERSION,
        "datasetKind": "arete-movement-runtime-aggregate",
        "captureIds": capture_ids,
        "capturedPlayfieldId": CAPTURED_ARETE_PLAYFIELD_ID,
        "runtimePlayfieldId": RUNTIME_ARETE_PLAYFIELD_ID,
        "sourcePromotableObservations": source_promotable,
        "deduplicatedRuntimeRows": runtime_rows,
        "scriptedRuntimeRows": 0,
        "identityScope": ["CaptureId", "SourceIdentity", "SourceGeneration"],
        "observationIdFormat": "{CaptureId}:{SourceObservationId}",
        "ordering": [
            "CaptureId",
            "SourceIdentity",
            "SourceGeneration",
            "CapturedUtc",
            "Sequence",
            "ObservationId",
        ],
        "behaviors": {
            behavior: {
                "path": relative_path(output_dir / f"{behavior}.csv"),
                "sourceObservations": sum(
                    integer(
                        source.runtime_manifest["behaviors"][behavior][
                            "sourceObservations"
                        ],
                        "sourceObservations",
                    )
                    for source in sources
                ),
                "runtimeRows": len(aggregate_rows[behavior]),
            }
            for behavior in BEHAVIORS
        },
        "sources": source_summaries,
        "inputs": [
            file_evidence(path)
            for path in sorted(set(all_input_paths), key=relative_path)
        ],
    }
    artifacts[output_dir / "manifest.json"] = (
        json.dumps(manifest, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")
    artifacts[report] = render_report(sources, output_dir, aggregate_rows)
    return artifacts, {
        "captures": len(sources),
        "sourcePromotable": source_promotable,
        "runtimeRows": runtime_rows,
    }


def write_artifacts(artifacts: dict[Path, bytes]) -> None:
    for path, payload in sorted(artifacts.items(), key=lambda item: str(item[0])):
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_suffix(path.suffix + ".tmp")
        temporary.write_bytes(payload)
        temporary.replace(path)


def check_artifacts(artifacts: dict[Path, bytes]) -> None:
    stale = [
        relative_path(path)
        for path, payload in artifacts.items()
        if not path.exists() or path.read_bytes() != payload
    ]
    if stale:
        raise RuntimeError("stale aggregate artifacts: " + ", ".join(sorted(stale)))


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    source_runtime_dirs = args.source_runtime_dirs or list(DEFAULT_SOURCE_RUNTIME_DIRS)
    try:
        artifacts, summary = build_artifacts(
            source_runtime_dirs,
            args.output_dir,
            args.report,
        )
        if args.write:
            write_artifacts(artifacts)
            print(
                "WROTE Arete movement aggregate "
                f"captures={summary['captures']} "
                f"sourcePromotable={summary['sourcePromotable']} "
                f"runtimeRows={summary['runtimeRows']}"
            )
        else:
            check_artifacts(artifacts)
            print("PASS Arete movement aggregate artifacts are current")
        return 0
    except Exception as exception:
        print(f"ERROR: {exception}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
