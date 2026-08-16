#!/usr/bin/env python3
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
FIXTURE_DIR = ROOT / "docs" / "reference" / "captures"
HASH_RE = re.compile(r"^[0-9a-f]{64}$")


def fail(message):
    raise AssertionError(message)


def load_fixture(path):
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def require(condition, message):
    if not condition:
        fail(message)


def require_int(value, field, minimum=0):
    require(isinstance(value, int), "%s must be an integer" % field)
    require(value >= minimum, "%s must be >= %d" % (field, minimum))


def validate_source_file(row, fixture_id):
    required_fields = {"path", "sha256", "bytes", "line_count", "role"}
    missing = required_fields.difference(row)
    require(not missing, "%s source file missing fields: %s" % (fixture_id, sorted(missing)))
    require(not Path(row["path"]).is_absolute(), "%s source path must be relative: %s" % (fixture_id, row["path"]))
    require("\\" not in row["path"], "%s source path must use forward slashes: %s" % (fixture_id, row["path"]))
    require(HASH_RE.match(row["sha256"]) is not None, "%s invalid sha256 for %s" % (fixture_id, row["path"]))
    require_int(row["bytes"], "%s:%s bytes" % (fixture_id, row["path"]), minimum=1)
    require_int(row["line_count"], "%s:%s line_count" % (fixture_id, row["path"]), minimum=1)
    if "data_row_count" in row:
        require_int(row["data_row_count"], "%s:%s data_row_count" % (fixture_id, row["path"]), minimum=0)


def validate_fixture(data, path):
    fixture_id = data.get("fixture_id", path.name)
    require(data.get("schema_version") == 1, "%s schema_version must be 1" % fixture_id)
    require(data.get("capture_id"), "%s capture_id is required" % fixture_id)
    require(data.get("confidence") in {"high", "medium", "low"}, "%s confidence is invalid" % fixture_id)
    require(data.get("required_for_windows_lane") is True, "%s must be marked required for the Windows lane" % fixture_id)
    require(data.get("promotion_gate", {}).get("windows_first") is True, "%s must be Windows-first gated" % fixture_id)

    source_files = data.get("source_files")
    require(isinstance(source_files, list) and source_files, "%s source_files must be non-empty" % fixture_id)
    seen_paths = set()
    for row in source_files:
        validate_source_file(row, fixture_id)
        require(row["path"] not in seen_paths, "%s duplicate source path: %s" % (fixture_id, row["path"]))
        seen_paths.add(row["path"])

    require("mission-flow.log" in seen_paths, "%s must include mission-flow.log" % fixture_id)
    require("raw-packets.csv" in seen_paths, "%s must include raw-packets.csv" % fixture_id)

    packet_summary = data.get("packet_summary", {})
    directions = packet_summary.get("directions", {})
    require(directions.get("IN", 0) > 0, "%s packet_summary requires inbound packets" % fixture_id)
    require(directions.get("OUT", 0) > 0, "%s packet_summary requires outbound packets" % fixture_id)

    combat_summary = data.get("combat_summary", {})
    require_int(combat_summary.get("total_rows"), "%s combat_summary.total_rows" % fixture_id, minimum=1)

    lifecycle_summary = data.get("npc_lifecycle_summary", {})
    require_int(lifecycle_summary.get("total_rows"), "%s npc_lifecycle_summary.total_rows" % fixture_id, minimum=1)


def main():
    fixtures = sorted(FIXTURE_DIR.glob("*.fixture.json"))
    require(fixtures, "No capture fixture files found")
    for fixture in fixtures:
        validate_fixture(load_fixture(fixture), fixture)
    print("PASS capture evidence fixtures")


if __name__ == "__main__":
    main()
