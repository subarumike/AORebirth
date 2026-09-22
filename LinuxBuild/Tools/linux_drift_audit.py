"""Require one clean source commit shared with authoritative public master."""
from __future__ import annotations

import argparse
from pathlib import Path
import re
import subprocess
import sys

PUBLIC_REPOSITORY = "https://github.com/subarumike/AORebirth.git"


def git(repository: Path, *args: str) -> str:
    return subprocess.check_output(
        ["git", "-C", str(repository), *args], text=True, stderr=subprocess.STDOUT
    ).strip()


def audit_repository(repository: Path, expected_sha: str, public_sha: str) -> dict[str, str]:
    for value in (expected_sha, public_sha):
        if not re.fullmatch(r"[0-9a-f]{40}", value):
            raise ValueError("Invalid exact source SHA")
    head = git(repository, "rev-parse", "HEAD")
    if head != expected_sha or head != public_sha:
        raise ValueError("HEAD, expected source and public master must be the same commit")
    if git(repository, "rev-parse", "refs/remotes/origin/master") != public_sha:
        raise ValueError("Fetched origin/master does not match authoritative public master")
    if git(repository, "status", "--porcelain", "--untracked-files=normal"):
        raise ValueError("Build source is dirty or contains untracked files")
    for obsolete in ("PRIVATE_BUILD_INPUTS.json", "patches/platform-compatibility.patch", "assemble_private_source.py"):
        if (repository / obsolete).exists():
            raise ValueError("Private source assembly is forbidden: " + obsolete)
    return {
        "PUBLIC_MASTER_SHA": public_sha,
        "LINUX_BUILD_SOURCE_SHA": head,
        "SOURCE_DIFF_PUBLIC_VS_LINUX_BUILD": "0",
        "DIRTY_BUILD_SOURCE": "NO",
        "LINUX_PATCH_COMMITS": "NONE",
        "LINUX_SOURCE_PATCH_LAYER": "NONE",
        "LINUX_DRIFT_AUDIT": "PASS",
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository-root", type=Path, default=Path.cwd())
    parser.add_argument("--expected-sha", required=True)
    args = parser.parse_args()
    try:
        remote = git(args.repository_root, "ls-remote", "--exit-code", PUBLIC_REPOSITORY, "refs/heads/master")
        fields = remote.split()
        if len(fields) != 2 or fields[1] != "refs/heads/master":
            raise ValueError("Public master could not be resolved uniquely")
        for key, value in audit_repository(args.repository_root.resolve(), args.expected_sha, fields[0]).items():
            print(f"{key}={value}")
        return 0
    except (ValueError, subprocess.CalledProcessError, OSError) as error:
        print(str(error), file=sys.stderr)
        print("LINUX_DRIFT_AUDIT=FAIL")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
