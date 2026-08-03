#!/usr/bin/env python3
"""Stress the generated-combat coordinator without DB, network, or engines."""

from __future__ import annotations

import argparse
import dataclasses
import datetime as dt
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path
from typing import Any, Mapping, Sequence

try:
    from Tools import generated_combat_pipeline as pipeline
except ModuleNotFoundError:
    import generated_combat_pipeline as pipeline


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_REPORT = Path("TestResults/generated-combat-pipeline-stress.json")


class StressError(RuntimeError):
    pass


def utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")


def git_status(repo_root: Path) -> str:
    completed = subprocess.run(
        ("git", "status", "--porcelain=v1", "--untracked-files=all"),
        cwd=repo_root,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="strict",
        check=False,
    )
    if completed.returncode != 0:
        raise StressError("git status failed")
    return completed.stdout


def target_hashes(repo_root: Path) -> dict[str, str]:
    hashes: dict[str, str] = {}
    for relative in list(pipeline.ARTIFACT_RELATIVE_PATHS.values()) + [
        pipeline.MANIFEST_RELATIVE_PATH
    ]:
        path = repo_root / Path(relative)
        if not path.is_file():
            raise StressError(f"governed target is missing: {relative.as_posix()}")
        hashes[relative.as_posix()] = pipeline.sha256_file(path)
    return hashes


def generated_artifact_control_root(repo_root: Path) -> Path:
    direct_git_dir = repo_root / ".git"
    if direct_git_dir.is_dir():
        return direct_git_dir.resolve() / "aorebirth-generated-artifacts"
    completed = subprocess.run(
        ("git", "rev-parse", "--absolute-git-dir"),
        cwd=repo_root,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="strict",
        check=False,
    )
    if completed.returncode != 0 or not completed.stdout.strip():
        raise StressError("git control directory lookup failed")
    return Path(completed.stdout.strip()).resolve() / "aorebirth-generated-artifacts"


def transaction_residue(repo_root: Path) -> tuple[str, ...]:
    control = generated_artifact_control_root(repo_root)
    values: list[str] = []
    staging = control / "staging"
    if staging.is_dir():
        for domain in staging.iterdir():
            if domain.is_dir():
                values.extend(
                    path.relative_to(control).as_posix() for path in domain.iterdir()
                )
    transactions = control / "transactions"
    if transactions.is_dir():
        values.extend(
            path.relative_to(control).as_posix() for path in transactions.rglob("*")
        )
    locks = control / "locks"
    if locks.is_dir():
        values.extend(
            path.relative_to(control).as_posix()
            for path in locks.iterdir()
        )
    return tuple(sorted(values))


@dataclasses.dataclass
class RunningProcess:
    name: str
    command: list[str]
    process: subprocess.Popen[str]
    started_utc: str


def start_process(
    name: str,
    command: Sequence[str],
    *,
    repo_root: Path,
    hash_seed: str,
) -> RunningProcess:
    environment = os.environ.copy()
    environment["PYTHONHASHSEED"] = hash_seed
    environment["PYTHONDONTWRITEBYTECODE"] = "1"
    process = subprocess.Popen(
        list(command),
        cwd=repo_root,
        env=environment,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        errors="replace",
        creationflags=(
            subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0
        ),
        start_new_session=os.name != "nt",
    )
    return RunningProcess(name, list(command), process, utc_now())


def finish_process(running: RunningProcess) -> dict[str, Any]:
    try:
        stdout, stderr = running.process.communicate(
            timeout=pipeline.CHILD_PROCESS_TIMEOUT_SECONDS
        )
    except subprocess.TimeoutExpired as error:
        stdout, stderr, cleanup = pipeline._terminate_process_tree(running.process)
        suffix = f" cleanup={cleanup}" if cleanup else ""
        raise StressError(
            f"{running.name} timed out after "
            f"{pipeline.CHILD_PROCESS_TIMEOUT_SECONDS}s pid={running.process.pid}{suffix}"
        ) from error
    record = {
        "name": running.name,
        "pid": running.process.pid,
        "startUtc": running.started_utc,
        "endUtc": utc_now(),
        "exitCode": running.process.returncode,
        "stdoutTail": stdout[-2000:],
        "stderrTail": stderr[-4000:],
    }
    if running.process.returncode != 0:
        raise StressError(
            f"{running.name} failed with exit code {running.process.returncode}: "
            f"{stderr[-2000:]}"
        )
    return record


def run_one(
    name: str,
    command: Sequence[str],
    *,
    repo_root: Path,
    hash_seed: str,
) -> dict[str, Any]:
    return finish_process(
        start_process(name, command, repo_root=repo_root, hash_seed=hash_seed)
    )


def run_concurrently(
    specifications: Sequence[tuple[str, Sequence[str], str]],
    *,
    repo_root: Path,
) -> list[dict[str, Any]]:
    running: list[RunningProcess] = []
    try:
        for name, command, seed in specifications:
            running.append(
                start_process(name, command, repo_root=repo_root, hash_seed=seed)
            )
    except Exception:
        for child in running:
            if child.process.poll() is None:
                pipeline._terminate_process_tree(child.process)
        raise
    records: list[dict[str, Any]] = []
    failures: list[str] = []
    try:
        for child in running:
            try:
                records.append(finish_process(child))
            except StressError as error:
                failures.append(str(error))
    finally:
        for child in running:
            if child.process.poll() is None:
                pipeline._terminate_process_tree(child.process)
    if failures:
        raise StressError("; ".join(failures))
    return records


def _fixture_snapshot_identity(variant: str) -> str:
    return hashlib.sha256(f"fixture-input:{variant}".encode("ascii")).hexdigest()


def create_fixture_candidate(fake_repo: Path, variant: str) -> pipeline.CandidateCohort:
    candidate_root = Path(
        tempfile.mkdtemp(
            prefix=f"candidate-{variant}-",
            dir=fake_repo / "TestResults" / "generated-combat-pipeline-candidates",
        )
    )
    artifacts = {
        role: candidate_root / Path(relative)
        for role, relative in pipeline.ARTIFACT_RELATIVE_PATHS.items()
    }
    documents: Mapping[str, Mapping[str, Any]] = {
        "inventory": {
            "schemaVersion": 1,
            "summary": {
                "captureSessionsDiscovered": 1,
                "canonicalValidSessions": 1,
                "completeAttackInfoChains": 1,
                "captureCertifiedProfiles": 1,
                "runtimeReadyProfiles": 1,
                "captureCertifiedSemanticDefinitions": 1,
                "runtimeReadyGeneratedSemanticDefinitions": 1,
                "unresolvedProfiles": 1,
                "decodeOrProjectionErrors": 0,
            },
            "fixtureVariant": variant,
        },
        "activeCoverage": {
            "schemaVersion": 1,
            "totals": {
                "initialActorCount": 2,
                "certified": 1,
                "unresolved": 1,
            },
            "profiles": [],
            "fixtureVariant": variant,
        },
        "formulaDataset": {
            "schemaVersion": 1,
            "profiles": [],
            "fixtureVariant": variant,
        },
    }
    for role, path in artifacts.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        if role in documents:
            path.write_bytes(pipeline.canonical_json_bytes(documents[role]))
        else:
            path.write_text(
                f"fixture-{role}-{variant}\n", encoding="utf-8", newline="\n"
            )
    generators = {
        name: {
            "path": logical.as_posix(),
            "sha256": hashlib.sha256(name.encode("ascii")).hexdigest(),
            "byteLength": len(name),
        }
        for name, logical in pipeline.GENERATOR_PATHS.items()
    }
    runtime = {
        "implementation": "fixture",
        "version": "1",
        "executableSha256": "0" * 64,
        "executableByteLength": 1,
    }
    plan_core = {
        "schemaVersion": 1,
        "generatorSources": [],
        "captures": [],
    }
    snapshot_core = {
        **plan_core,
        "planIdentity": pipeline.sha256_bytes(
            pipeline.identity_json_bytes(plan_core)
        ),
    }
    capture_snapshot = {
        **snapshot_core,
        "snapshotIdentity": pipeline.sha256_bytes(
            pipeline.identity_json_bytes(snapshot_core)
        ),
    }
    manifest, rendered = pipeline.build_generation_manifest(
        cohort_root=candidate_root,
        artifacts=artifacts,
        input_snapshot=capture_snapshot,
        auxiliary_input_identity=_fixture_snapshot_identity("auxiliary-" + variant),
        generators=generators,
        runtime=runtime,
    )
    manifest_path = candidate_root / Path(pipeline.MANIFEST_RELATIVE_PATH)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_bytes(rendered)
    pipeline.validate_cohort(candidate_root, verify_toolchain=False)
    return pipeline.CandidateCohort(
        root=candidate_root,
        artifacts=artifacts,
        manifest_path=manifest_path,
        capture_snapshot=capture_snapshot,
        generation_identity=manifest["generationIdentity"],
        input_snapshot_identity=manifest["inputSnapshot"]["identity"],
        fixed_point_rounds=1,
    )


def fixture_operation(
    fake_repo: Path,
    operation: str,
    variant: str,
    acquired_marker: Path | None,
    hold_milliseconds: int,
) -> int:
    fake_repo = fake_repo.resolve(strict=True)
    if not 0 <= hold_milliseconds <= 5000:
        raise StressError("fixture hold must be between 0 and 5000 milliseconds")

    def announce(wait_milliseconds: int) -> None:
        if acquired_marker is not None:
            acquired_marker.parent.mkdir(parents=True, exist_ok=True)
            acquired_marker.write_bytes(
                pipeline.canonical_json_bytes(
                    {"pid": os.getpid(), "acquisitionWaitMs": wait_milliseconds}
                )
            )
        if hold_milliseconds:
            time.sleep(hold_milliseconds / 1000.0)
        print(f"fixture lease acquisitionWaitMs={wait_milliseconds}")

    if operation == "check":
        acquisition_started = time.monotonic()
        with pipeline._shared_lease(fake_repo, "read"):
            waited = int((time.monotonic() - acquisition_started) * 1000)
            announce(waited)
            pipeline.validate_cohort(fake_repo, verify_toolchain=False)
        return 0
    candidate = create_fixture_candidate(fake_repo, variant)
    try:
        acquisition_started = time.monotonic()
        with pipeline._shared_lease(fake_repo, "write") as lease:
            waited = int((time.monotonic() - acquisition_started) * 1000)
            announce(waited)
            pipeline._shared_publish(lease, candidate, lambda phase: None)
            pipeline.validate_cohort(fake_repo, verify_toolchain=False)
    finally:
        shutil.rmtree(candidate.root, ignore_errors=False)
    return 0


def prepare_fixture_repo(root: Path) -> None:
    (root / ".git").mkdir()
    (root / "TestResults" / "generated-combat-pipeline-candidates").mkdir(
        parents=True
    )
    candidate = create_fixture_candidate(root, "initial")
    try:
        with pipeline._shared_lease(root, "write") as lease:
            pipeline._shared_publish(lease, candidate, lambda phase: None)
            pipeline.validate_cohort(root, verify_toolchain=False)
    finally:
        shutil.rmtree(candidate.root, ignore_errors=False)


def run_fixture_matrix(repo_root: Path) -> list[dict[str, Any]]:
    fixture_parent = repo_root / "TestResults"
    fixture_parent.mkdir(exist_ok=True)
    fixture_root = Path(
        tempfile.mkdtemp(prefix="generated-combat-stress-fixture-", dir=fixture_parent)
    )
    try:
        prepare_fixture_repo(fixture_root)
        fixture_base = [
            sys.executable,
            str(repo_root / "Tools" / "stress_generated_combat_pipeline.py"),
            "--_fixture-root",
            str(fixture_root),
        ]
        acquired_marker = fixture_root / "check-acquired.json"
        check_process = start_process(
            "fixture-check",
            fixture_base
            + [
                "--_fixture-operation",
                "check",
                "--_fixture-acquired-marker",
                str(acquired_marker),
                "--_fixture-hold-milliseconds",
                "1200",
            ],
            repo_root=repo_root,
            hash_seed="101",
        )
        try:
            ready_deadline = time.monotonic() + 20.0
            while not acquired_marker.is_file():
                if check_process.process.poll() is not None:
                    finish_process(check_process)
                    raise StressError("fixture check exited before its acquisition barrier")
                if time.monotonic() >= ready_deadline:
                    pipeline._terminate_process_tree(check_process.process)
                    raise StressError("fixture check acquisition barrier timed out")
                time.sleep(0.025)
            writer_records = run_concurrently(
                (
                    (
                        "fixture-write-a",
                        fixture_base
                        + [
                            "--_fixture-operation",
                            "write",
                            "--_fixture-variant",
                            "writer-a",
                            "--_fixture-hold-milliseconds",
                            "300",
                        ],
                        "103",
                    ),
                    (
                        "fixture-write-b",
                        fixture_base
                        + [
                            "--_fixture-operation",
                            "write",
                            "--_fixture-variant",
                            "writer-b",
                            "--_fixture-hold-milliseconds",
                            "300",
                        ],
                        "107",
                    ),
                ),
                repo_root=repo_root,
            )
            check_record = finish_process(check_process)
        finally:
            if check_process.process.poll() is None:
                pipeline._terminate_process_tree(check_process.process)
        records = [check_record, *writer_records]
        writer_waits = []
        for record in writer_records:
            match = re.search(r"acquisitionWaitMs=(\d+)", record["stdoutTail"])
            if match is None:
                raise StressError("fixture writer did not report lease acquisition wait")
            writer_waits.append(int(match.group(1)))
        if min(writer_waits) < 100:
            raise StressError(
                "fixture writers did not overlap the held reader lease: "
                + ",".join(str(value) for value in writer_waits)
            )
        pipeline.validate_cohort(fixture_root, verify_toolchain=False)
        fixture_candidate_parent = (
            fixture_root / "TestResults" / "generated-combat-pipeline-candidates"
        )
        if any(fixture_candidate_parent.iterdir()):
            raise StressError("fixture candidate residue remains after matrix")
        residue = transaction_residue(fixture_root)
        if residue:
            raise StressError(
                "fixture lock/staging/transaction residue remains after matrix: "
                + ",".join(residue)
            )
        return records
    finally:
        shutil.rmtree(fixture_root, ignore_errors=False)


def run_stress(repo_root: Path, report_path: Path) -> int:
    repo_root = repo_root.resolve(strict=True)
    report_path = report_path if report_path.is_absolute() else repo_root / report_path
    started = utc_now()
    status_before = git_status(repo_root)
    if status_before:
        raise StressError("stress runner requires a clean worktree")
    residue_before = transaction_residue(repo_root)
    if residue_before:
        raise StressError("generated-combat candidate residue exists before stress")
    hashes_before = target_hashes(repo_root)
    manifest_before = pipeline.validate_cohort(repo_root, verify_toolchain=True)

    command = [
        sys.executable,
        str(repo_root / "Tools" / "generated_combat_pipeline.py"),
        "--check",
    ]
    phases: list[dict[str, Any]] = []
    phases.append(
        {
            "name": "real-sequential-checks",
            "processes": [
                run_one(
                    "sequential-check-seed-1",
                    command,
                    repo_root=repo_root,
                    hash_seed="1",
                ),
                run_one(
                    "sequential-check-seed-777",
                    command,
                    repo_root=repo_root,
                    hash_seed="777",
                ),
            ],
        }
    )
    phases.append(
        {
            "name": "real-concurrent-check-check",
            "processes": run_concurrently(
                (
                    ("concurrent-check-a", command, "11"),
                    ("concurrent-check-b", command, "29"),
                ),
                repo_root=repo_root,
            ),
        }
    )

    phases.append(
        {
            "name": "fixture-check-write-write",
            "processes": run_fixture_matrix(repo_root),
        }
    )

    hashes_after = target_hashes(repo_root)
    manifest_after = pipeline.validate_cohort(repo_root, verify_toolchain=True)
    if hashes_after != hashes_before:
        raise StressError("governed artifact hashes drifted during stress")
    if manifest_after["generationIdentity"] != manifest_before["generationIdentity"]:
        raise StressError("generation identity drifted during stress")
    if git_status(repo_root) != status_before:
        raise StressError("worktree changed during stress")
    if transaction_residue(repo_root) != residue_before:
        raise StressError("generated-combat candidate residue changed during stress")

    report = {
        "schemaVersion": 1,
        "pipeline": pipeline.PIPELINE_NAME,
        "pid": os.getpid(),
        "startUtc": started,
        "endUtc": utc_now(),
        "inputIdentity": manifest_before["inputSnapshot"]["identity"],
        "generationIdentity": manifest_before["generationIdentity"],
        "targets": hashes_before,
        "temporaryRoot": str(generated_artifact_control_root(repo_root) / "staging"),
        "lease": {
            "domain": pipeline.PIPELINE_NAME,
            "lockDirectory": str(
                generated_artifact_control_root(repo_root) / "locks"
            ),
        },
        "phases": phases,
        "result": "PASS",
    }
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_bytes(pipeline.canonical_json_bytes(report))
    print(f"generated-combat stress PASS report={report_path}")
    return 0


def parse_arguments(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=REPO_ROOT)
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT)
    parser.add_argument("--fixture-only", action="store_true")
    parser.add_argument("--_fixture-root", type=Path, help=argparse.SUPPRESS)
    parser.add_argument(
        "--_fixture-operation",
        choices=("check", "write"),
        help=argparse.SUPPRESS,
    )
    parser.add_argument("--_fixture-variant", default="fixture", help=argparse.SUPPRESS)
    parser.add_argument("--_fixture-acquired-marker", type=Path, help=argparse.SUPPRESS)
    parser.add_argument(
        "--_fixture-hold-milliseconds", type=int, default=0, help=argparse.SUPPRESS
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    arguments = parse_arguments(argv)
    if (arguments._fixture_root is None) != (arguments._fixture_operation is None):
        raise StressError("fixture mode requires both private fixture arguments")
    if arguments._fixture_root is not None:
        return fixture_operation(
            arguments._fixture_root,
            arguments._fixture_operation,
            arguments._fixture_variant,
            arguments._fixture_acquired_marker,
            arguments._fixture_hold_milliseconds,
        )
    if arguments.fixture_only:
        records = run_fixture_matrix(arguments.repo_root.resolve(strict=True))
        print(
            "generated-combat fixture stress PASS "
            + ",".join(f"{row['name']}={row['exitCode']}" for row in records)
        )
        return 0
    return run_stress(arguments.repo_root, arguments.report)


def write_failure_report(
    argv: Sequence[str], error: Exception, started_utc: str
) -> None:
    try:
        arguments = parse_arguments(argv)
        if arguments._fixture_root is not None or arguments.fixture_only:
            return
        repo_root = arguments.repo_root.resolve(strict=True)
        report_path = (
            arguments.report
            if arguments.report.is_absolute()
            else repo_root / arguments.report
        )
        input_identity = None
        generation_identity = None
        try:
            manifest = pipeline.validate_cohort(repo_root, verify_toolchain=False)
            input_identity = manifest["inputSnapshot"]["identity"]
            generation_identity = manifest["generationIdentity"]
        except Exception:
            pass
        try:
            targets = target_hashes(repo_root)
        except Exception:
            targets = {}
        report = {
            "schemaVersion": 1,
            "pipeline": pipeline.PIPELINE_NAME,
            "pid": os.getpid(),
            "startUtc": started_utc,
            "endUtc": utc_now(),
            "inputIdentity": input_identity,
            "generationIdentity": generation_identity,
            "targets": targets,
            "temporaryRoot": str(
                generated_artifact_control_root(repo_root) / "staging"
            ),
            "lease": {
                "domain": pipeline.PIPELINE_NAME,
                "lockDirectory": str(
                    generated_artifact_control_root(repo_root) / "locks"
                ),
            },
            "result": "FAIL",
            "error": f"{type(error).__name__}: {error}",
        }
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_bytes(pipeline.canonical_json_bytes(report))
    except Exception:
        # The original stress failure remains authoritative if recording itself
        # is impossible.
        return


if __name__ == "__main__":
    started_utc = utc_now()
    try:
        raise SystemExit(main())
    except Exception as error:
        write_failure_report(sys.argv[1:], error, started_utc)
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)
