"""Publish a canonical validation check from an independently trusted private run."""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import zipfile

from windows_release_contract import PUBLIC_REPOSITORY, pairs, profile_for, runtime_content_path, validate_manifest


CHECK = "dao-canonical-validation"
REQUIRED_STEPS = {
    "Verify exact proposed source",
    "Validate evaluated source graph and mutation tests",
    "Validate canonical manifest and references",
    "Exercise affected production loaders",
    "Verify schema compatibility",
    "Verify gameplay authorization",
    "Reject missing or invalid mandatory content",
}
MAX_ARCHIVE_BYTES = 4 * 1024 * 1024
MAX_MANIFEST_BYTES = 16 * 1024 * 1024


def require(condition, message):
    if not condition:
        raise ValueError(message)


def is_sha(value, digits=40):
    return isinstance(value, str) and re.fullmatch(f"[0-9a-f]{{{digits}}}", value) is not None


def api(endpoint, *, token=None, method="GET", payload=None, binary=False):
    env = os.environ.copy()
    env.pop("AO_REBIRTH_DAO_APP_TOKEN", None)
    if token is not None:
        env["GH_TOKEN"] = token
    command = ["gh", "api", "--hostname", "github.com", "--method", method, endpoint]
    if payload is not None:
        command += ["--input", "-"]
    result = subprocess.run(command, input=None if payload is None else json.dumps(payload).encode(),
                            capture_output=True, env=env, timeout=90, check=False)
    require(result.returncode == 0, "GitHub request to a configured validation authority failed")
    return result.stdout if binary else json.loads(result.stdout, object_pairs_hook=pairs)


def read_evidence(data, expected_digest):
    require(len(data) <= MAX_ARCHIVE_BYTES and expected_digest == "sha256:" + hashlib.sha256(data).hexdigest(),
            "Private evidence archive identity differs")
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        files = archive.infolist()
        require(sorted(entry.filename for entry in files) == ["manifest.json", "receipt.json"],
                "Private evidence must contain exactly the receipt and manifest")
        require(archive.getinfo("receipt.json").file_size <= 16384
                and archive.getinfo("manifest.json").file_size <= MAX_MANIFEST_BYTES,
                "Private evidence exceeds its allowed size")
        receipt = json.loads(archive.read("receipt.json"), object_pairs_hook=pairs)
        manifest = json.loads(archive.read("manifest.json"), object_pairs_hook=pairs)
    manifest_id = validate_manifest(manifest)
    require(all(runtime_content_path(row["path"]) for row in manifest), "Manifest contains an unadmitted runtime path")
    require(isinstance(receipt, dict) and receipt.get("GAMEDATA_MANIFEST_SHA256") == manifest_id,
            "Receipt does not bind the exact validated manifest")
    return receipt


def verify_run(run, settings, run_id):
    require(run.get("id") == run_id and run.get("workflow_id") == settings["WORKFLOW_ID"]
            and run.get("head_sha") == settings["WORKFLOW_SOURCE_SHA"]
            and run.get("event") == "workflow_dispatch"
            and run.get("status") == "completed" and run.get("conclusion") == "success"
            and run.get("repository", {}).get("full_name", "").lower() == settings["REPOSITORY"].lower()
            and run.get("head_repository", {}).get("full_name", "").lower() == settings["REPOSITORY"].lower()
            and type(run.get("run_attempt")) is int and run["run_attempt"] > 0,
            "Required private validation did not pass on the pinned workflow source")
    return run["run_attempt"]


def verify_jobs(response):
    jobs = response.get("jobs", [])
    require(len(jobs) == response.get("total_count"), "Private validation job inventory is incomplete")
    selected = [job for job in jobs if job.get("name") == CHECK]
    require(len(selected) == 1 and selected[0].get("conclusion") == "success",
            "Required private validation job did not pass")
    steps = selected[0].get("steps", [])
    for name in REQUIRED_STEPS:
        matches = [step for step in steps if step.get("name") == name]
        require(len(matches) == 1 and matches[0].get("status") == "completed"
                and matches[0].get("conclusion") == "success", "Missing, skipped or unsuccessful private step: " + name)


def verify_receipt(receipt, candidate, base, upstream, tree, run_id, attempt):
    expected = {"FORMAT_VERSION": 1, "SOURCE_SHA": candidate, "BASE_SHA": base, "CONTENT_DUMP_SHA": upstream,
                "CONTENT_DUMP_TREE_SHA": tree, "RUN_ID": run_id, "RUN_ATTEMPT": attempt,
                "CHECKS": {step: "PASS" for step in REQUIRED_STEPS}}
    require(set(receipt) == {*expected, "GAMEDATA_MANIFEST_SHA256"} and type(receipt["FORMAT_VERSION"]) is int
            and all(receipt[key] == value for key, value in expected.items())
            and is_sha(receipt["GAMEDATA_MANIFEST_SHA256"], 64),
            "Private receipt does not match the exact source, upstream, attempt and required checks")


def verified_inputs(profile, settings, candidate):
    base = api(f"repos/{PUBLIC_REPOSITORY}/branches/master")["commit"]["sha"]
    require(is_sha(base), "Current master identity is unavailable")
    compare = api(f"repos/{PUBLIC_REPOSITORY}/compare/{base}...{candidate}")
    require(compare.get("merge_base_commit", {}).get("sha") == base
            and compare.get("status") == "ahead", "Proposed commit is not ahead of current master")
    private = profile["PRIVATE_CONTENT_REPOSITORY"]
    branch = profile["PRIVATE_CONTENT_BRANCH"]
    content = api(f"repos/{private}/commits/{branch}")
    upstream, tree = content["sha"], content["commit"]["tree"]["sha"]
    require(is_sha(upstream) and is_sha(tree), "Current private authority identity is unavailable")
    validator = api(f"repos/{settings['REPOSITORY']}")
    require(validator.get("private") is True, "Canonical validation workflow must be private")
    ruleset_id = settings["MASTER_RULESET_ID"]
    ruleset = api(f"repos/{PUBLIC_REPOSITORY}/rulesets/{ruleset_id}")
    rules = api(f"repos/{PUBLIC_REPOSITORY}/rules/branches/master")
    applied = [row for row in rules if row.get("ruleset_id") == ruleset_id]
    status = [row.get("parameters", {}) for row in applied if row.get("type") == "required_status_checks"]
    require(ruleset.get("enforcement") == "active" and ruleset.get("bypass_actors") == []
            and {"deletion", "non_fast_forward", "pull_request"}.issubset(row.get("type") for row in applied)
            and len(status) == 1 and status[0].get("strict_required_status_checks_policy") is True
            and {("dao-runtime-structure", 15368), (CHECK, settings["APP_ID"])}.issubset(
                (row.get("context"), row.get("integration_id")) for row in status[0].get("required_status_checks", [])),
            "The trusted App and public check must be bound to active no-bypass master protection")
    public = api(f"repos/{PUBLIC_REPOSITORY}/commits/{candidate}/check-runs"
                 "?check_name=dao-runtime-structure&filter=latest&per_page=100")
    checks = public.get("check_runs", [])
    expected_checks = [row for row in checks if row.get("app", {}).get("id") == 15368]
    require(len(checks) == public.get("total_count") and expected_checks
            and all(row.get("head_sha") == candidate and row.get("status") == "completed"
                    and row.get("conclusion") == "success" for row in expected_checks),
            "Public structural validation has not passed for the exact proposed commit")
    return base, upstream, tree


def publish(profile, candidate, run_id, token):
    settings = profile.get("DAO_TRUSTED_VALIDATOR", {})
    require(isinstance(settings, dict)
            and set(settings) == {"REPOSITORY", "WORKFLOW_ID", "WORKFLOW_SOURCE_SHA", "APP_ID", "MASTER_RULESET_ID"}
            and isinstance(settings.get("REPOSITORY"), str)
            and re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", settings["REPOSITORY"])
            and all(type(settings[key]) is int and settings[key] > 0 for key in ("WORKFLOW_ID", "APP_ID", "MASTER_RULESET_ID"))
            and is_sha(settings.get("WORKFLOW_SOURCE_SHA")), "Trusted private workflow and App binding are required")
    require(token is not None and token.startswith("ghs_"), "GitHub App installation token is required")
    require(is_sha(candidate) and run_id > 0, "An exact proposed commit and private run ID are required")
    base, upstream, tree = verified_inputs(profile, settings, candidate)

    private = f"repos/{settings['REPOSITORY']}/actions"
    run_path = f"{private}/runs/{run_id}"
    attempt = verify_run(api(run_path), settings, run_id)
    verify_jobs(api(f"{run_path}/attempts/{attempt}/jobs?per_page=100"))
    response = api(f"{run_path}/artifacts?per_page=100")
    artifacts = response.get("artifacts", [])
    require(len(artifacts) == response.get("total_count"), "Private artifact inventory is incomplete")
    matched = [row for row in artifacts if row.get("name") == f"dao-canonical-evidence-{attempt}"]
    require(len(matched) == 1, "Exactly one evidence archive is required for the validated attempt")
    artifact = matched[0]
    require(artifact.get("expired") is False and type(artifact.get("size_in_bytes")) is int
            and 0 < artifact["size_in_bytes"] <= MAX_ARCHIVE_BYTES and type(artifact.get("id")) is int
            and artifact["id"] > 0 and artifact.get("workflow_run", {}).get("id") == run_id,
            "Private evidence is expired, unbound or too large")
    receipt = read_evidence(api(f"{private}/artifacts/{artifact['id']}/zip", binary=True), artifact.get("digest"))
    verify_receipt(receipt, candidate, base, upstream, tree, run_id, attempt)

    check_path = f"repos/{PUBLIC_REPOSITORY}/check-runs"
    check = api(check_path, token=token, method="POST", payload={"name": CHECK, "head_sha": candidate,
                "status": "in_progress", "external_id": artifact["digest"]})
    require(check.get("app", {}).get("id") == settings["APP_ID"] and check.get("name") == CHECK
            and check.get("head_sha") == candidate and check.get("status") == "in_progress"
            and type(check.get("id")) is int and check["id"] > 0,
            "Installation token did not create the expected App check. No success was published")
    require(verify_run(api(run_path), settings, run_id) == attempt
            and verified_inputs(profile, settings, candidate) == (base, upstream, tree),
            "Private run or authority moved before publication. Check remains pending")
    completed = api(f"{check_path}/{check['id']}", token=token, method="PATCH", payload={"status": "completed",
                    "conclusion": "success", "output": {"title": "Canonical validation passed",
                    "summary": "Validated source `" + candidate + "` with restricted evidence `" + artifact["digest"] + "`."}})
    require(completed.get("conclusion") == "success" and completed.get("status") == "completed"
            and completed.get("name") == CHECK and completed.get("head_sha") == candidate
            and completed.get("app", {}).get("id") == settings["APP_ID"], "Published check could not be verified")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", type=Path, required=True)
    parser.add_argument("--candidate", required=True)
    parser.add_argument("--private-run", type=int, required=True)
    args = parser.parse_args()
    try:
        profile = profile_for(Path(__file__).resolve().parents[1], args.profile)
        publish(profile, args.candidate, args.private_run, os.environ.get("AO_REBIRTH_DAO_APP_TOKEN"))
    except (OSError, ValueError, TypeError, KeyError, AttributeError, subprocess.SubprocessError, zipfile.BadZipFile) as error:
        print(f"DAO_TRUSTED_PRIVATE_CHECK=BLOCKED {error}", file=sys.stderr)
        return 1
    print("DAO_TRUSTED_PRIVATE_CHECK=PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
