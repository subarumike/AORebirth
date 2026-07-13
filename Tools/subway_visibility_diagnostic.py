#!/usr/bin/env python3
"""Prepare and analyze opt-in PF127 visibility-isolation sessions."""

from __future__ import annotations

import argparse
import csv
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path


OUTCOMES = {
    "PASS_LOGIN_STABLE",
    "FAIL_CLIENT_CRASH",
    "FAIL_CLIENT_DISCONNECT",
    "FAIL_SERVER_EXCEPTION",
    "INCONCLUSIVE",
}
SLICE_MODES = {
    "NONE",
    "ALL_38",
    "SUPPORTED_29",
    "ORDINARY_9",
    "FIRST_N",
    "ORDINAL_RANGE",
    "IDENTITY_LIST",
    "FAMILY",
}
SESSION_ID_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")


def repository_root() -> Path:
    return Path(__file__).resolve().parent.parent


def manifest_path() -> Path:
    return repository_root() / "docs" / "generated" / "subway_pf127_visibility_diagnostic_manifest.csv"


def local_root() -> Path:
    return repository_root() / ".local" / "subway-visibility"


def session_dir(session_id: str) -> Path:
    validate_session_id(session_id)
    return local_root() / session_id


def validate_session_id(session_id: str) -> None:
    if not SESSION_ID_RE.fullmatch(session_id or ""):
        raise ValueError("session id must use 1-64 letters, digits, dot, underscore, or hyphen")


def load_manifest() -> list[dict]:
    with manifest_path().open(newline="", encoding="utf-8-sig") as handle:
        rows = list(csv.DictReader(handle))
    if len(rows) != 38:
        raise ValueError(f"diagnostic manifest must contain exactly 38 rows, found {len(rows)}")
    for row in rows:
        row["ordinal"] = int(row["Ordinal"])
        row["source_instance"] = int(row["SourceInstanceHex"], 16)
    if [row["ordinal"] for row in rows] != list(range(1, 39)):
        raise ValueError("diagnostic ordinals must be the deterministic inclusive range 1..38")
    if len({row["source_instance"] for row in rows}) != 38:
        raise ValueError("diagnostic source identities must be unique")
    supported = [row for row in rows if row["Classification"] == "SUPPORTED_FAMILY_RESTORE"]
    ordinary = [row for row in rows if row["Classification"] == "ORDINARY_ENEMY_REGENERATE"]
    if len(supported) != 29 or len(ordinary) != 9:
        raise ValueError("diagnostic manifest must contain exactly 29 supported and 9 ordinary rows")
    return rows


def normalize_identity(value: str) -> int:
    text = value.strip().upper()
    text = text.replace("(SIMPLECHAR:", "").replace("SIMPLECHAR:", "").replace(")", "")
    if text.startswith("0X"):
        text = text[2:]
    if not re.fullmatch(r"[0-9A-F]{1,8}", text):
        raise ValueError(f"invalid SimpleChar identity: {value}")
    return int(text, 16)


def selected_rows(args: argparse.Namespace, rows: list[dict]) -> tuple[str, list[dict]]:
    mode = (args.slice or "").upper()
    inferred = [
        ("FIRST_N", args.first is not None),
        ("ORDINAL_RANGE", args.ordinal_range is not None),
        ("IDENTITY_LIST", args.identity_list is not None),
        ("FAMILY", args.family is not None),
    ]
    inferred_modes = [name for name, active in inferred if active]
    if not mode:
        if len(inferred_modes) != 1:
            raise ValueError("specify --slice or exactly one slice selector")
        mode = inferred_modes[0]
    if mode not in SLICE_MODES:
        raise ValueError("unknown slice mode: " + mode)
    if inferred_modes and any(name != mode for name in inferred_modes):
        raise ValueError("slice-specific arguments must match the selected slice mode")

    if mode == "NONE":
        selected = []
    elif mode == "ALL_38":
        selected = rows[:]
    elif mode == "SUPPORTED_29":
        selected = [row for row in rows if row["Classification"] == "SUPPORTED_FAMILY_RESTORE"]
    elif mode == "ORDINARY_9":
        selected = [row for row in rows if row["Classification"] == "ORDINARY_ENEMY_REGENERATE"]
    elif mode == "FIRST_N":
        if args.first is None or args.first < 0 or args.first > 38:
            raise ValueError("--first must be between 0 and 38")
        selected = rows[: args.first]
    elif mode == "ORDINAL_RANGE":
        match = re.fullmatch(r"(\d+)-(\d+)", args.ordinal_range or "")
        if not match:
            raise ValueError("--ordinal-range must use inclusive START-END syntax")
        start, end = int(match.group(1)), int(match.group(2))
        if start < 1 or end > 38 or start > end:
            raise ValueError("ordinal range must stay within 1..38")
        selected = [row for row in rows if start <= row["ordinal"] <= end]
    elif mode == "IDENTITY_LIST":
        requested = {normalize_identity(value) for value in (args.identity_list or "").split(",") if value.strip()}
        known = {row["source_instance"] for row in rows}
        unknown = sorted(requested - known)
        if unknown:
            raise ValueError("unknown quarantined identities: " + ",".join(f"{value:08X}" for value in unknown))
        selected = [row for row in rows if row["source_instance"] in requested]
    else:
        family = (args.family or "").strip()
        if not family:
            raise ValueError("--family is required for FAMILY")
        selected = [row for row in rows if row["Family"].casefold() == family.casefold()]
        if not selected:
            raise ValueError("unknown quarantined family: " + family)
    return mode, selected


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def write_json(path: Path, payload: dict) -> None:
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def prepare(args: argparse.Namespace) -> int:
    rows = load_manifest()
    mode, selected = selected_rows(args, rows)
    root = local_root()
    target = session_dir(args.session_id)
    root.mkdir(parents=True, exist_ok=True)
    active = root / "active-session.cfg"
    if active.exists():
        raise ValueError("an active diagnostic session already exists; finish it before preparing another")
    target.mkdir(parents=False, exist_ok=False)
    selected_hex = [row["SourceInstanceHex"] for row in selected]
    payload = {
        "schema_version": "1.0",
        "session_id": args.session_id,
        "created_utc": utc_now(),
        "playfield_id": 127,
        "slice": mode,
        "expected_quarantined_row_count": len(selected),
        "selected_ordinals": [row["ordinal"] for row in selected],
        "selected_identities": [f"SimpleChar:{value}" for value in selected_hex],
        "manifest": str(manifest_path()),
        "runtime_log": str(target / "runtime-events.jsonl"),
        "send_ledger": str(target / "per-enemy-send-ledger.csv"),
        "snapshot_summary": str(target / "snapshot-summary.jsonl"),
    }
    write_json(target / "session.json", payload)
    (target / "selected-identities.txt").write_text("\n".join(selected_hex) + ("\n" if selected_hex else ""), encoding="ascii")
    instructions = (
        f"PF127 visibility diagnostic session: {args.session_id}\n"
        f"Slice: {mode}; quarantined rows selected: {len(selected)}\n\n"
        "1. Restart engines with the existing repository wrapper.\n"
        "2. Log one test character into PF127 and observe the client.\n"
        "3. Do not infer an outcome from logs alone.\n"
        "4. Record the observed outcome with tools\\subway_visibility_diagnostic.cmd record.\n"
        "5. Run tools\\subway_visibility_diagnostic.cmd analyze.\n"
    )
    (target / "operator-instructions.txt").write_text(instructions, encoding="utf-8")
    config_lines = [
        "enabled=1",
        f"session_id={args.session_id}",
        f"slice={mode}",
        "selected_source_instances=" + ",".join(selected_hex),
        f"expected_quarantined_row_count={len(selected)}",
        f"artifact_directory={target.resolve()}",
    ]
    active.write_text("\n".join(config_lines) + "\n", encoding="utf-8")
    print(f"Prepared {args.session_id}: slice={mode} rows={len(selected)}")
    print(f"Session directory: {target}")
    return 0


def status(args: argparse.Namespace) -> int:
    target = session_dir(args.session_id)
    session = read_json(target / "session.json")
    active = local_root() / "active-session.cfg"
    is_active = active.exists() and f"session_id={args.session_id}" in active.read_text(encoding="utf-8")
    outcome = read_json(target / "outcome.json").get("outcome") if (target / "outcome.json").exists() else "NOT_RECORDED"
    print(f"session={args.session_id} active={str(is_active).lower()} slice={session['slice']} selected={session['expected_quarantined_row_count']} outcome={outcome}")
    return 0


def finish(args: argparse.Namespace) -> int:
    target = session_dir(args.session_id)
    if not (target / "session.json").exists():
        raise ValueError("session does not exist: " + args.session_id)
    active = local_root() / "active-session.cfg"
    if active.exists():
        text = active.read_text(encoding="utf-8")
        if f"session_id={args.session_id}" not in text:
            raise ValueError("a different diagnostic session is active")
        active.unlink()
    print(f"Finished {args.session_id}; artifacts retained at {target}")
    print("Restart engines to return the running ZoneEngine to the default quarantined population.")
    return 0


def bool_choice(value: str | None) -> bool | None:
    if value is None or value == "UNKNOWN":
        return None
    return value == "YES"


def record(args: argparse.Namespace) -> int:
    target = session_dir(args.session_id)
    if not (target / "session.json").exists():
        raise ValueError("session does not exist: " + args.session_id)
    payload = {
        "schema_version": "1.0",
        "session_id": args.session_id,
        "recorded_utc": utc_now(),
        "outcome": args.outcome,
        "time_to_failure_seconds": args.time_to_failure,
        "login_completed": bool_choice(args.login_completed),
        "world_rendered": bool_choice(args.world_rendered),
        "movement_possible": bool_choice(args.movement_possible),
        "last_visible_log_timestamp": args.last_visible_log_timestamp,
        "operator_note": args.note,
        "client_state_source": "operator_observed",
    }
    write_json(target / "outcome.json", payload)
    print(f"Recorded {args.session_id}: {args.outcome}")
    return 0


def load_last_summary(target: Path) -> dict | None:
    path = target / "snapshot-summary.jsonl"
    if not path.exists():
        return None
    rows = [json.loads(line) for line in path.read_text(encoding="utf-8-sig").splitlines() if line.strip()]
    return rows[-1] if rows else None


def session_evidence(target: Path) -> dict:
    session = read_json(target / "session.json")
    outcome = read_json(target / "outcome.json") if (target / "outcome.json").exists() else None
    summary = load_last_summary(target)
    return {"session": session, "outcome": outcome, "summary": summary}


def analyze(args: argparse.Namespace) -> int:
    target = session_dir(args.session_id)
    current = session_evidence(target)
    all_sessions = []
    for child in sorted(local_root().iterdir()) if local_root().exists() else []:
        if child.is_dir() and (child / "session.json").exists():
            all_sessions.append(session_evidence(child))
    findings = []
    outcome = current["outcome"]
    summary = current["summary"]
    if outcome is None:
        findings.append("INCONCLUSIVE")
    elif summary is None:
        findings.append("INCONCLUSIVE")
    else:
        failed = outcome["outcome"].startswith("FAIL_")
        completed = bool(summary.get("snapshot_completed"))
        if not completed:
            findings.append("SERVER_SEND_SEQUENCE_INCOMPLETE")
        elif failed:
            findings.append("SERVER_SEND_SEQUENCE_COMPLETE_BEFORE_CLIENT_FAILURE")

    usable = [item for item in all_sessions if item["outcome"] and item["summary"]]
    passing = [item for item in usable if item["outcome"]["outcome"] == "PASS_LOGIN_STABLE"]
    failing = [item for item in usable if item["outcome"]["outcome"].startswith("FAIL_")]
    pass_counts = [int(item["summary"].get("total_npcs_sent", 0)) for item in passing]
    fail_counts = [int(item["summary"].get("total_npcs_sent", 0)) for item in failing]
    pass_bytes = [int(item["summary"].get("total_serialized_bytes", 0)) for item in passing]
    fail_bytes = [int(item["summary"].get("total_serialized_bytes", 0)) for item in failing]
    if pass_counts and fail_counts and max(pass_counts) < min(fail_counts):
        findings.append("FAILURE_CORRELATES_WITH_NPC_COUNT")
    if pass_bytes and fail_bytes and max(pass_bytes) < min(fail_bytes):
        findings.append("FAILURE_CORRELATES_WITH_BYTE_COUNT")

    failing_sets = [set(item["session"]["selected_identities"]) for item in failing]
    passing_sets = [set(item["session"]["selected_identities"]) for item in passing]
    common_failing = sorted(set.intersection(*failing_sets)) if failing_sets else []
    present_in_passing = sorted(set.union(*passing_sets)) if passing_sets else []
    unique_to_failing = sorted(set(common_failing) - set(present_in_passing))
    if len(failing_sets) >= 2 and len(unique_to_failing) == 1:
        findings.append("FAILURE_FOLLOWS_SPECIFIC_IDENTITY")
    passing_slices = {item["session"]["slice"] for item in passing}
    failing_slices = {item["session"]["slice"] for item in failing}
    if (
        "SUPPORTED_29" in failing_slices
        and "ORDINARY_9" in passing_slices
        or "ORDINARY_9" in failing_slices
        and "SUPPORTED_29" in passing_slices
    ):
        findings.append("FAILURE_FOLLOWS_GROUP")
    if (
        "ALL_38" in failing_slices
        and "SUPPORTED_29" in passing_slices
        and "ORDINARY_9" in passing_slices
    ):
        findings.append("FAILURE_REQUIRES_COMBINATION")
    if not findings:
        findings.append("INCONCLUSIVE")

    selected = current["session"]["selected_identities"]
    recommendation = "Run the documented NONE, SUPPORTED_29, and ORDINARY_9 broad split."
    if outcome and outcome["outcome"].startswith("FAIL_") and len(selected) > 1:
        midpoint = (len(selected) + 1) // 2
        first = ",".join(value.split(":", 1)[1] for value in selected[:midpoint])
        recommendation = (
            "Prepare the first deterministic half with: tools\\subway_visibility_diagnostic.cmd prepare "
            f"--session-id {args.session_id}-half-a --identity-list {first}"
        )
    elif outcome and outcome["outcome"] == "PASS_LOGIN_STABLE":
        recommendation = "Continue with the next broad group or the complementary deterministic half."

    report = {
        "schema_version": "1.0",
        "generated_utc": utc_now(),
        "session_id": args.session_id,
        "findings": sorted(set(findings)),
        "selected_identities": selected,
        "snapshot": summary,
        "operator_outcome": outcome,
        "cross_session": {
            "smallest_passing_npc_count": min(pass_counts) if pass_counts else None,
            "largest_passing_npc_count": max(pass_counts) if pass_counts else None,
            "smallest_failing_npc_count": min(fail_counts) if fail_counts else None,
            "smallest_passing_byte_total": min(pass_bytes) if pass_bytes else None,
            "largest_passing_byte_total": max(pass_bytes) if pass_bytes else None,
            "smallest_failing_byte_total": min(fail_bytes) if fail_bytes else None,
            "identities_common_to_all_failing_slices": common_failing,
            "identities_present_in_passing_slices": present_in_passing,
            "identities_unique_to_failing_slices": unique_to_failing,
        },
        "causality_warning": "LAST_COMPLETED_BEFORE_FAILURE is not a PROVEN_CAUSAL_ENEMY without repeatable controlled-slice evidence.",
        "recommended_next_step": recommendation,
    }
    write_json(target / "analysis.json", report)
    markdown = [
        f"# PF127 Visibility Analysis: {args.session_id}",
        "",
        "## Findings",
        "",
        *[f"- `{finding}`" for finding in report["findings"]],
        "",
        "## Causality boundary",
        "",
        report["causality_warning"],
        "",
        "## Recommended next step",
        "",
        recommendation,
        "",
    ]
    (target / "analysis.md").write_text("\n".join(markdown), encoding="utf-8")
    print(f"Analysis written: {target / 'analysis.md'}")
    print("Findings: " + ", ".join(report["findings"]))
    print("Next: " + recommendation)
    return 0


def add_session(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--session-id", required=True)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    prepared = sub.add_parser("prepare")
    add_session(prepared)
    prepared.add_argument("--slice", choices=sorted(SLICE_MODES))
    prepared.add_argument("--first", type=int)
    prepared.add_argument("--ordinal-range")
    prepared.add_argument("--identity-list")
    prepared.add_argument("--family")
    prepared.set_defaults(func=prepare)
    for name, func in (("status", status), ("finish", finish), ("analyze", analyze)):
        child = sub.add_parser(name)
        add_session(child)
        child.set_defaults(func=func)
    recorded = sub.add_parser("record")
    add_session(recorded)
    recorded.add_argument("--outcome", required=True, choices=sorted(OUTCOMES))
    recorded.add_argument("--time-to-failure", type=float)
    recorded.add_argument("--login-completed", choices=("YES", "NO", "UNKNOWN"), default="UNKNOWN")
    recorded.add_argument("--world-rendered", choices=("YES", "NO", "UNKNOWN"), default="UNKNOWN")
    recorded.add_argument("--movement-possible", choices=("YES", "NO", "UNKNOWN"), default="UNKNOWN")
    recorded.add_argument("--last-visible-log-timestamp")
    recorded.add_argument("--note", default="")
    recorded.set_defaults(func=record)
    return parser


def main(argv: list[str] | None = None) -> int:
    try:
        args = build_parser().parse_args(argv)
        return args.func(args)
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print("ERROR: " + str(error), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
