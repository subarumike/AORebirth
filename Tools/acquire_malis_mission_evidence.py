#!/usr/bin/env python3
"""Archive the public Malis/AOSharp evidence used by the offline analyzer."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import urllib.request


REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_OUTPUT = REPOSITORY_ROOT / "docs" / "reference" / "missions" / "malis"
MALIS_URL = "https://gitlab.com/Pixelmania/malis-mission-roller-2.0.git"
MALIS_COMMIT = "3ac9943a4943b8cb80eda9e40359729e656686b0"
MALIS_BRANCH = "main"
LEVEL_80_COMMIT = "e19bb1ddc25e2647688c7996c8b09d50198fc486"
QL200_COMMIT = "7e5b921cebabee99051252a4883f324b38a519fc"
GITLAB_PROJECT_ID = "37047089"
GITLAB_API = f"https://gitlab.com/api/v4/projects/{GITLAB_PROJECT_ID}"
MEGA_FOLDER_URL = "https://mega.nz/folder/XThTRBKD#w0JD-dp-Cxg9syfNEcIwiA"
MEGA_RELEASE_NAME = "Malis-AO-Toolkit-27-01-26.zip"
MEGA_RELEASE_NODE = "ePAg0JLZ"
MEGA_RELEASE_SIZE = 11_839_297
NUGET_URL = "https://api.nuget.org/v3-flatcontainer/aosharpsdk/1.0.106/aosharpsdk.1.0.106.nupkg"
NUGET_VERSION = "1.0.106"
AOSHARP_URL = "https://gitlab.com/never-knows-best/aosharp.git"
AOSHARP_SOURCE_COMMIT = "b45b7a05f9ffd9676d37e620f2f7d481b82ed212"


class AcquisitionError(RuntimeError):
    pass


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def run_git(repository: Path, *arguments: str, text: bool = True) -> str | bytes:
    result = subprocess.run(
        ["git", "-C", str(repository), *arguments],
        check=False,
        capture_output=True,
        text=text,
    )
    if result.returncode != 0:
        stderr = result.stderr if text else result.stderr.decode("utf-8", errors="replace")
        raise AcquisitionError(f"git {' '.join(arguments)} failed: {stderr.strip()}")
    return result.stdout


def atomic_write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_bytes(data)
    temporary.replace(path)


def copy_exact(source: Path, destination: Path) -> None:
    if not source.is_file():
        raise AcquisitionError(f"Required source artifact is missing: {source}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_name(destination.name + ".tmp")
    shutil.copyfile(source, temporary)
    temporary.replace(destination)


def request_json(url: str) -> bytes:
    request = urllib.request.Request(
        url,
        headers={
            "Accept": "application/json",
            "Accept-Encoding": "identity",
            "User-Agent": "AORebirth Malis mission evidence collector/1.0",
        },
    )
    with urllib.request.urlopen(request, timeout=30) as response:
        data = response.read()
        if response.status != 200:
            raise AcquisitionError(f"Unexpected HTTP {response.status} for {url}")
    json.loads(data.decode("utf-8"))
    return data


def archive_git_source(repository: Path, commit: str, prefix: str, destination: Path) -> None:
    with tempfile.TemporaryDirectory() as temporary:
        candidate = Path(temporary) / destination.name
        run_git(repository, "archive", "--format=zip", f"--prefix={prefix}/", f"--output={candidate}", commit)
        copy_exact(candidate, destination)


def archive_git_bundle(repository: Path, destination: Path) -> None:
    with tempfile.TemporaryDirectory() as temporary:
        candidate = Path(temporary) / destination.name
        run_git(repository, "bundle", "create", str(candidate), "--all")
        run_git(repository, "bundle", "verify", str(candidate))
        copy_exact(candidate, destination)


def write_history(repository: Path, raw_root: Path) -> None:
    log_text = run_git(
        repository,
        "log",
        "--all",
        "--reverse",
        "--date=iso-strict",
        "--format=%H%x09%aI%x09%cI%x09%an%x09%s",
    )
    commits = []
    for line in log_text.splitlines():
        commit, authored, committed, author, subject = line.split("\t", 4)
        commits.append(
            {
                "Author": author,
                "AuthorDate": authored,
                "CommitDate": committed,
                "Sha": commit,
                "Subject": subject,
            }
        )
    atomic_write(
        raw_root / "malis-commit-history.json",
        (json.dumps(commits, indent=2, sort_keys=True) + "\n").encode("utf-8"),
    )

    for label, commit in (("level-80-fix", LEVEL_80_COMMIT), ("ql200-above-200", QL200_COMMIT)):
        patch = run_git(repository, "show", "--format=fuller", "--no-ext-diff", commit)
        atomic_write(raw_root / f"malis-{label}-{commit}.patch", patch.encode("utf-8"))


def add_artifact(artifacts: list[dict[str, object]], root: Path, path: Path, role: str, source: str) -> None:
    artifacts.append(
        {
            "ByteLength": path.stat().st_size,
            "RelativePath": path.relative_to(root).as_posix(),
            "Role": role,
            "Sha256": sha256(path),
            "Source": source,
        }
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--malis-repository", type=Path, required=True)
    parser.add_argument("--aosharp-repository", type=Path, required=True)
    parser.add_argument("--release-package", type=Path, required=True)
    parser.add_argument("--aosharp-nupkg", type=Path, required=True)
    parser.add_argument("--retrieved-at-utc", required=True)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()

    output = args.output.resolve()
    raw_root = output / "raw"
    raw_root.mkdir(parents=True, exist_ok=True)

    malis_repo = args.malis_repository.resolve()
    aosharp_repo = args.aosharp_repository.resolve()
    if run_git(malis_repo, "rev-parse", "HEAD").strip() != MALIS_COMMIT:
        raise AcquisitionError("Malis repository HEAD does not match the governed source commit.")
    if run_git(malis_repo, "branch", "--show-current").strip() != MALIS_BRANCH:
        raise AcquisitionError("Malis repository is not on the expected main branch.")
    if run_git(malis_repo, "status", "--short").strip():
        raise AcquisitionError("Malis repository is dirty.")
    if run_git(malis_repo, "remote", "get-url", "origin").strip() != MALIS_URL:
        raise AcquisitionError("Malis origin URL does not match the governed public repository.")
    run_git(malis_repo, "fetch", "--all", "--tags", "--prune")
    if run_git(aosharp_repo, "cat-file", "-t", AOSHARP_SOURCE_COMMIT).strip() != "commit":
        raise AcquisitionError("AOSharp source correlation commit is unavailable.")

    source_zip = raw_root / f"malis-source-{MALIS_COMMIT}.zip"
    bundle = raw_root / f"malis-repository-{MALIS_COMMIT}.bundle"
    aosharp_zip = raw_root / f"aosharp-source-{AOSHARP_SOURCE_COMMIT}.zip"
    release = raw_root / MEGA_RELEASE_NAME
    nupkg = raw_root / f"aosharpsdk.{NUGET_VERSION}.nupkg"
    archive_git_source(malis_repo, MALIS_COMMIT, f"malis-{MALIS_COMMIT[:8]}", source_zip)
    archive_git_bundle(malis_repo, bundle)
    archive_git_source(aosharp_repo, AOSHARP_SOURCE_COMMIT, f"aosharp-{AOSHARP_SOURCE_COMMIT[:8]}", aosharp_zip)
    copy_exact(args.release_package.resolve(), release)
    copy_exact(args.aosharp_nupkg.resolve(), nupkg)
    if release.stat().st_size != MEGA_RELEASE_SIZE:
        raise AcquisitionError("Malis release size does not match the public MEGA node metadata.")

    write_history(malis_repo, raw_root)

    api_sources = {
        "gitlab-project.json": GITLAB_API,
        "gitlab-releases.json": f"{GITLAB_API}/releases",
        "gitlab-issues.json": f"{GITLAB_API}/issues?scope=all&per_page=100",
        "gitlab-merge-requests.json": f"{GITLAB_API}/merge_requests?scope=all&per_page=100",
        f"gitlab-{LEVEL_80_COMMIT}-comments.json": f"{GITLAB_API}/repository/commits/{LEVEL_80_COMMIT}/comments",
        f"gitlab-{QL200_COMMIT}-comments.json": f"{GITLAB_API}/repository/commits/{QL200_COMMIT}/comments",
    }
    metadata_sources = {
        **api_sources,
        "malis-commit-history.json": MALIS_URL,
        "mega-release-metadata.json": MEGA_FOLDER_URL,
    }
    for filename, url in api_sources.items():
        atomic_write(raw_root / filename, request_json(url))

    release_metadata = {
        "ByteLength": MEGA_RELEASE_SIZE,
        "DecryptedFileName": MEGA_RELEASE_NAME,
        "FolderUrl": MEGA_FOLDER_URL,
        "NodeHandle": MEGA_RELEASE_NODE,
        "RetrievedAtUtc": args.retrieved_at_utc,
        "Safety": "Archive inspected without executing bundled binaries.",
    }
    atomic_write(
        raw_root / "mega-release-metadata.json",
        (json.dumps(release_metadata, indent=2, sort_keys=True) + "\n").encode("utf-8"),
    )

    artifacts: list[dict[str, object]] = []
    for path, role, source in (
        (source_zip, "MALIS_EXACT_SOURCE_TREE", MALIS_URL),
        (bundle, "MALIS_COMPLETE_GIT_HISTORY", MALIS_URL),
        (aosharp_zip, "AOSHARP_PUBLIC_SOURCE_CORRELATION", AOSHARP_URL),
        (release, "MALIS_PUBLIC_TOOLKIT_RELEASE", MEGA_FOLDER_URL),
        (nupkg, "AOSHARP_SDK_EXACT_PACKAGE", NUGET_URL),
    ):
        add_artifact(artifacts, output, path, role, source)
    for path in sorted(raw_root.glob("*.json")):
        add_artifact(artifacts, output, path, "PUBLIC_METADATA_OR_HISTORY", metadata_sources[path.name])
    for path in sorted(raw_root.glob("*.patch")):
        add_artifact(artifacts, output, path, "MALIS_HISTORICAL_PATCH", MALIS_URL)

    manifest = {
        "Acquisition": {
            "AOSharpNuGetVersion": NUGET_VERSION,
            "AOSharpSourceCorrelationCommit": AOSHARP_SOURCE_COMMIT,
            "Branch": MALIS_BRANCH,
            "MalisCommit": MALIS_COMMIT,
            "MalisRepository": MALIS_URL,
            "ReleaseBinariesExecuted": False,
            "RetrievedAtUtc": args.retrieved_at_utc,
        },
        "Artifacts": sorted(artifacts, key=lambda item: str(item["RelativePath"])),
    }
    atomic_write(
        output / "source-manifest.json",
        (json.dumps(manifest, indent=2, sort_keys=True) + "\n").encode("utf-8"),
    )
    print(f"Malis evidence acquisition archived {len(artifacts)} artifacts at {MALIS_COMMIT}.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (AcquisitionError, OSError, ValueError, json.JSONDecodeError) as error:
        print(f"Malis evidence acquisition failed: {error}")
        raise SystemExit(1)
