#!/usr/bin/env python3
"""Generate the compiled mission-level graph from its canonical CSV.

The checked-in Helpbot artifact governs levels 1-149. The earlier ODS is retained
as conflicting legacy provenance only, and levels 150-220 remain outside the
Helpbot reference's coverage.
"""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path
import sys

from helpbot_mission_ql_reference import (
    DEFAULT_REFERENCE as HELPBOT_REFERENCE,
    ReferenceError,
    derive_detents,
    load_reference,
)


REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SOURCE = (
    REPOSITORY_ROOT
    / "AORebirth"
    / "Server"
    / "ZoneEngine"
    / "XML Data"
    / "MissionLevels.csv"
)
DEFAULT_OUTPUT = (
    REPOSITORY_ROOT
    / "AORebirth"
    / "Server"
    / "ZoneEngine"
    / "Core"
    / "Missions"
    / "MissionLevelGraphData.g.cs"
)

SOURCE_REPOSITORY_PATH = (
    "AORebirth/Server/ZoneEngine/XML Data/MissionLevels.csv"
)
UPSTREAM_ODS_FILE = "Mission_Tables_Level_Restrictions_Teaming_Levels.ods"
UPSTREAM_ODS_SHA256 = (
    "5efdba9a2e8310253246d82a9e733d90b32bb4b360a035c157f9d81832f4a0e7"
)
UPSTREAM_ODS_VERIFICATION = (
    "Legacy provenance only. It conflicts with the pinned Helpbot reference "
    "below level 134, and levels 134-220 are precision-coerced and not exact."
)
HELPBOT_REFERENCE_REPOSITORY_PATH = (
    "docs/evidence/data/helpbot-mission-ql-levels-1-149.json"
)
HELPBOT_REFERENCE_REVISION_URL = (
    "https://wiki.aodb.us/index.php?title=Level_Parameters&oldid=44808"
)
HELPBOT_REFERENCE_RAW_SHA256 = (
    "f8841253af7ed9b63aa2d9d1a2d48e487239b4f8e44e57b225cc7b3855c04488"
)
HELPBOT_REFERENCE_VERIFICATION = (
    "Published mission-QL lists are proven for levels 1-149; the eleven "
    "difficulty detents are deterministically derived and exhaustively checked."
)

MIN_LEVEL = 1
MAX_LEVEL = 220
DIFFICULTY_COUNT = 11
MIN_QUALITY = 1
MAX_QUALITY = 250
MIN_TOKENS = 1
MAX_TOKENS = 9
EXPECTED_HEADER = [
    "Level",
    *[f"Q{index}" for index in range(DIFFICULTY_COUNT)],
    "Tokens",
]


class ValidationError(ValueError):
    pass


def parse_canonical_unsigned(token: str, label: str) -> int:
    if not token:
        raise ValidationError(f"{label} is empty.")
    if any(character < "0" or character > "9" for character in token):
        raise ValidationError(f"{label} is not an unsigned decimal integer.")
    if len(token) > 1 and token[0] == "0":
        raise ValidationError(f"{label} has a non-canonical leading zero.")
    return int(token)


def normalize_source(raw: bytes) -> str:
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError as error:
        raise ValidationError("Source CSV is not UTF-8.") from error

    if text.startswith("\ufeff"):
        raise ValidationError("Source CSV must not contain a UTF-8 BOM.")

    text = text.replace("\r\n", "\n")
    if "\r" in text:
        raise ValidationError("Source CSV contains a bare carriage return.")
    if not text.endswith("\n"):
        text += "\n"
    return text


def validate_canonical_csv(text: str) -> list[str]:
    if not text:
        raise ValidationError("Source CSV is empty.")
    if "\r" in text:
        raise ValidationError("Canonical CSV must use LF line endings.")
    if not text.endswith("\n"):
        raise ValidationError("Canonical CSV must end with one newline.")

    rows = text[:-1].split("\n")
    if len(rows) != 1 + MAX_LEVEL:
        raise ValidationError(
            f"Expected one header and {MAX_LEVEL} level rows; found {len(rows)} rows."
        )
    if rows[0].split(",") != EXPECTED_HEADER:
        raise ValidationError("Mission-level CSV header is not canonical.")

    qualities: list[list[int]] = []
    tokens: list[int] = []
    for expected_level, row in enumerate(rows[1:], start=MIN_LEVEL):
        if not row:
            raise ValidationError(f"Level {expected_level} row is empty.")

        cells = row.split(",")
        if len(cells) != 2 + DIFFICULTY_COUNT:
            raise ValidationError(
                f"Level {expected_level} must contain exactly "
                f"{2 + DIFFICULTY_COUNT} columns."
            )

        level = parse_canonical_unsigned(cells[0], "Level")
        if level != expected_level:
            raise ValidationError(
                f"Expected level {expected_level}; found level {level}."
            )

        row_qualities = []
        for difficulty_index in range(DIFFICULTY_COUNT):
            quality = parse_canonical_unsigned(
                cells[1 + difficulty_index],
                f"Level {level} Q{difficulty_index}",
            )
            if quality < MIN_QUALITY or quality > MAX_QUALITY:
                raise ValidationError(
                    f"Level {level} Q{difficulty_index} is outside "
                    f"{MIN_QUALITY}..{MAX_QUALITY}."
                )
            row_qualities.append(quality)

        if any(
            row_qualities[index] > row_qualities[index + 1]
            for index in range(DIFFICULTY_COUNT - 1)
        ):
            raise ValidationError(
                f"Level {level} mission qualities are not nondecreasing."
            )
        if row_qualities[5] != level:
            raise ValidationError(
                f"Level {level} neutral difficulty must equal the character level."
            )

        token_count = parse_canonical_unsigned(cells[-1], f"Level {level} Tokens")
        if token_count < MIN_TOKENS or token_count > MAX_TOKENS:
            raise ValidationError(
                f"Level {level} token count is outside {MIN_TOKENS}..{MAX_TOKENS}."
            )

        qualities.append(row_qualities)
        tokens.append(token_count)

    for level_index in range(MAX_LEVEL - 1):
        if tokens[level_index] > tokens[level_index + 1]:
            raise ValidationError(
                f"Token count decreases between levels "
                f"{level_index + 1} and {level_index + 2}."
            )

    return rows


def validate_helpbot_reference(rows: list[str]) -> None:
    try:
        _, reference_rows = load_reference(HELPBOT_REFERENCE)
    except ReferenceError as error:
        raise ValidationError(str(error)) from error

    for level, published in reference_rows.items():
        cells = rows[level].split(",")
        actual = [int(cells[1 + index]) for index in range(DIFFICULTY_COUNT)]
        expected = derive_detents(level, published)
        if actual != expected:
            raise ValidationError(
                f"Level {level} differs from the pinned Helpbot reference: "
                f"expected {expected}, found {actual}."
            )


def csharp_string(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


def generate_source(rows: list[str], source_sha256: str, payload_sha256: str) -> str:
    generated_rows = "\n".join(
        f"            {csharp_string(row)}," for row in rows
    )
    return f"""// <auto-generated />
// Generated by tools/generate_mission_level_graph.py. Do not edit by hand.
namespace ZoneEngine.Core.Missions
{{
    using System;

    internal static class MissionLevelGraphData
    {{
        internal const int FormatVersion = 1;

        internal const string SourceRepositoryPath =
            {csharp_string(SOURCE_REPOSITORY_PATH)};

        internal const string SourceSha256 =
            {csharp_string(source_sha256)};

        internal const string CanonicalPayloadSha256 =
            {csharp_string(payload_sha256)};

        internal const string UpstreamOdsFileName =
            {csharp_string(UPSTREAM_ODS_FILE)};

        internal const string UpstreamOdsSha256 =
            {csharp_string(UPSTREAM_ODS_SHA256)};

        internal const string UpstreamOdsVerification =
            {csharp_string(UPSTREAM_ODS_VERIFICATION)};

        internal const string HelpbotReferenceRepositoryPath =
            {csharp_string(HELPBOT_REFERENCE_REPOSITORY_PATH)};

        internal const string HelpbotReferenceRevisionUrl =
            {csharp_string(HELPBOT_REFERENCE_REVISION_URL)};

        internal const string HelpbotReferenceRawSha256 =
            {csharp_string(HELPBOT_REFERENCE_RAW_SHA256)};

        internal const string HelpbotReferenceVerification =
            {csharp_string(HELPBOT_REFERENCE_VERIFICATION)};

        private static readonly string[] Rows =
        {{
{generated_rows}
        }};

        internal static readonly string CanonicalCsv =
            string.Join("\\n", Rows) + "\\n";
    }}
}}
"""


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument(
        "--check",
        action="store_true",
        help="Fail unless the checked-in generated artifact is byte-for-byte current.",
    )
    args = parser.parse_args()

    source_bytes = args.source.read_bytes()
    canonical_csv = normalize_source(source_bytes)
    source_sha256 = hashlib.sha256(canonical_csv.encode("utf-8")).hexdigest()
    rows = validate_canonical_csv(canonical_csv)
    validate_helpbot_reference(rows)
    payload_sha256 = hashlib.sha256(canonical_csv.encode("utf-8")).hexdigest()
    generated = generate_source(rows, source_sha256, payload_sha256)
    generated_bytes = generated.encode("utf-8")

    if args.check:
        if not args.output.exists():
            print(f"Generated artifact is missing: {args.output}", file=sys.stderr)
            return 1
        if args.output.read_bytes() != generated_bytes:
            print(
                f"Generated artifact is stale: {args.output}",
                file=sys.stderr,
            )
            return 1
        print(
            "Mission-level graph artifact is reproducible: "
            f"source={source_sha256} payload={payload_sha256}"
        )
        return 0

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(generated_bytes)
    print(
        f"Generated {args.output} from {args.source} "
        f"source={source_sha256} payload={payload_sha256}"
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValidationError) as error:
        print(f"Mission-level graph generation failed: {error}", file=sys.stderr)
        raise SystemExit(1)
