#!/usr/bin/env python3
"""Build a self-contained, deterministic deployment archive from accepted source."""
import argparse
import gzip
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import re
import subprocess
import tarfile
import tempfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
PREFIX = "LinuxBuild/deployment/production-release/"
ENTRY = PREFIX + "upgrade-active-services.sh"
ROOTS = {
    ENTRY: "operator / README.md",
    "LinuxBuild/Tools/content_provenance.py": "LinuxBuild/content-provenance.sh",
    PREFIX + "create-release-manifest.sh": "release builder",
    PREFIX + "package-release.py": "release operator",
    PREFIX + "README.md": "release operator",
    "LinuxBuild/deployment/systemd/ao-rebirth-loginengine.service": "release.manifest",
    "LinuxBuild/deployment/systemd/ao-rebirth-zoneengine.service": "release.manifest",
}
ARTIFACTS = {
    "LOGINENGINE_ARTIFACT_DIR": "LinuxBuild/artifacts/loginengine/linux-x64/self-contained",
    "ZONEENGINE_ARTIFACT_DIR": "LinuxBuild/artifacts/zoneengine/linux-x64/self-contained",
}
MANIFEST = "LinuxBuild/artifacts/production-release/release.manifest"
METADATA = "LinuxBuild/PACKAGE_IDENTITY.json"
INVENTORY = "LinuxBuild/PACKAGE_CONTENTS.tsv"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def safe_path(value):
    p = PurePosixPath(value)
    if not value or p.is_absolute() or ".." in p.parts or str(p) != value or any(c in value for c in "\r\n\t\\"):
        raise ValueError(f"unsafe package path: {value!r}")
    return value


def discover(root, roots=ROOTS):
    """Resolve every shell source recursively; unknown expressions fail closed."""
    found = dict(roots)
    pending = list(roots)
    while pending:
        relative = pending.pop()
        source = root / relative
        if not source.is_file() or source.is_symlink():
            raise ValueError(f"missing dependency: {relative}")
        if source.suffix != ".sh":
            continue
        for line in source.read_text().splitlines():
            match = re.match(r'^\s*(?:source|\.)\s+"([^"]+)"\s*(?:#.*)?$', line)
            if not match:
                if re.match(r"^\s*(?:source|\.)\s", line):
                    raise ValueError(f"unresolved dependency in {relative}: {line}")
                continue
            expression = match[1]
            for name, base in (("SCRIPT_DIR", source.parent), ("script_dir", source.parent), ("repository_root", root)):
                expression = expression.replace("${" + name + "}", str(base))
            if "$" in expression:
                raise ValueError(f"unresolved dependency in {relative}: {match[1]}")
            dependency = Path(expression).resolve().relative_to(root.resolve()).as_posix()
            safe_path(dependency)
            if dependency not in found:
                found[dependency] = relative
                pending.append(dependency)
    return found


def read_manifest(path):
    result = {}
    for line in path.read_text().splitlines():
        key, value = line.split("=", 1)
        if key in result:
            raise ValueError(f"duplicate manifest key: {key}")
        result[key] = value
    return result


def package_files(root, manifest, destination, identity):
    dependencies = discover(root)
    files = {}

    def add(relative, data, mode, consumer):
        safe_path(relative)
        if relative in files:
            raise ValueError(f"duplicate package entry: {relative}")
        files[relative] = (data, mode, consumer)

    for relative, consumer in sorted(dependencies.items()):
        path = root / relative
        # Shell dependencies are executable even when normally sourced.
        add(relative, path.read_bytes(), 0o755 if path.suffix in (".sh", ".py") else 0o644, consumer)
    bound = dict(manifest)
    for key, relative in ARTIFACTS.items():
        artifact = Path(manifest[key])
        if not artifact.is_dir() or artifact.is_symlink():
            raise ValueError(f"invalid artifact: {artifact}")
        for path in sorted(artifact.rglob("*")):
            if path.is_symlink() or not (path.is_dir() or path.is_file()):
                raise ValueError(f"unsafe artifact entry: {path}")
            if path.is_file():
                mode = 0o755 if path.stat().st_mode & 0o111 else 0o644
                add(relative + "/" + path.relative_to(artifact).as_posix(), path.read_bytes(), mode, key)
        apphost = "LoginEngine" if key.startswith("LOGIN") else "ZoneEngine_New"
        if files[relative + "/" + apphost][1] != 0o755:
            raise ValueError(f"artifact apphost is not executable: {apphost}")
        bound[key] = destination + "/" + relative
    for key, relative in (("LOGINENGINE_UNIT_PATH", "LinuxBuild/deployment/systemd/ao-rebirth-loginengine.service"),
                          ("ZONEENGINE_UNIT_PATH", "LinuxBuild/deployment/systemd/ao-rebirth-zoneengine.service")):
        if sha(files[relative][0]) != manifest[key.replace("_PATH", "_SHA256")]:
            raise ValueError(f"unit hash mismatch: {relative}")
        bound[key] = destination + "/" + relative
    add(MANIFEST, ("\n".join(f"{k}={v}" for k, v in bound.items()) + "\n").encode(), 0o600, ENTRY)
    if manifest.get("FORMAT") == "3":
        if manifest.get("ZONEENGINE_IMPLEMENTATION") != "new" or manifest.get("CONTENT_PROVENANCE_VERSION") != "1":
            raise ValueError("invalid editable content manifest mode")
        if not re.fullmatch(r"[0-9a-f]{64}", manifest.get("CONTENT_MANIFEST_SHA256", "")):
            raise ValueError("invalid editable content manifest digest")
        if any(not re.fullmatch(r"[1-9][0-9]*", manifest.get(k, "")) for k in ("CONTENT_FILE_COUNT", "CONTENT_TOTAL_BYTES")):
            raise ValueError("invalid editable content manifest counts")
        content_path = ARTIFACTS["ZONEENGINE_ARTIFACT_DIR"] + "/CONTENT_MANIFEST.json"
        if content_path not in files or sha(files[content_path][0]) != manifest["CONTENT_MANIFEST_SHA256"]:
            raise ValueError("editable content manifest hash mismatch")
        identity = dict(identity, world_content={"mode": "editable", "manifest_sha256": manifest["CONTENT_MANIFEST_SHA256"],
                                                "file_count": int(manifest["CONTENT_FILE_COUNT"]),
                                                "total_bytes": int(manifest["CONTENT_TOTAL_BYTES"])})
    identity = dict(identity, destination=destination, deployment_dependencies=[
        {"repository_path": p, "consumer": consumer, "required_in_archive": True,
         "archive_path": p, "executable_required": files[p][1] == 0o755, "sha256": sha(files[p][0])}
        for p, consumer in sorted(dependencies.items())])
    add(METADATA, (json.dumps(identity, sort_keys=True, indent=2) + "\n").encode(), 0o644, "release authority")
    rows = [f"{sha(data)}\t{mode:04o}\t{relative}" for relative, (data, mode, _) in sorted(files.items())]
    add(INVENTORY, ("\n".join(rows) + "\n").encode(), 0o644, "package integrity guard")
    return files


def write_archive(files, target, timestamp):
    # No host paths, owner IDs, gzip filenames or wall-clock times enter the tar.
    with target.open("xb") as output:
        with gzip.GzipFile(fileobj=output, mode="wb", filename="", mtime=0) as zipped:
            with tarfile.open(fileobj=zipped, mode="w", format=tarfile.PAX_FORMAT) as archive:
                for relative, (data, mode, _) in sorted(files.items()):
                    info = tarfile.TarInfo(relative)
                    info.size, info.mode, info.mtime = len(data), mode, timestamp
                    info.uid = info.gid = 0
                    archive.addfile(info, io.BytesIO(data))


def verify_archive(target, files):
    with tarfile.open(target, "r:gz") as archive:
        members = archive.getmembers()
        if [m.name for m in members] != sorted(files):
            raise ValueError("archive file set differs from dependency inventory")
        with tempfile.TemporaryDirectory(prefix="aorebirth-package-") as folder:
            root = Path(folder)
            for member in members:
                safe_path(member.name)
                data, mode, _ = files[member.name]
                if not member.isfile() or member.mode != mode or archive.extractfile(member).read() != data:
                    raise ValueError(f"archive content/mode mismatch: {member.name}")
                out = root / member.name
                out.parent.mkdir(parents=True, exist_ok=True)
                out.write_bytes(data)
                out.chmod(mode)
            # The actual entry point loads its transitive sources without a checkout.
            subprocess.run(["bash", str(root / ENTRY), "--help"], check=True, capture_output=True, cwd=folder)
            subprocess.run(["bash", str(root / (PREFIX + "package-integrity.sh")), str(root)], check=True, capture_output=True, cwd=folder)


def require_same_source(expected_sha, runtime_sha):
    if expected_sha != runtime_sha:
        raise ValueError("release tooling and runtime must use the exact same accepted source SHA")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--expected-sha", required=True)
    parser.add_argument("--runtime-source-sha", required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--destination", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    git = lambda *a: subprocess.check_output(["git", "-C", str(ROOT), *a], text=True).strip()
    for value in (args.expected_sha, args.runtime_source_sha):
        if not re.fullmatch(r"[0-9a-f]{40}", value):
            raise ValueError("source identity must be a full SHA")
    require_same_source(args.expected_sha, args.runtime_source_sha)
    if git("rev-parse", "HEAD") != args.expected_sha or git("status", "--porcelain", "--untracked-files=no"):
        raise ValueError("expected exact committed clean source required")
    if not re.fullmatch(r"/srv/[a-zA-Z0-9_-]+", args.destination):
        raise ValueError("destination must be a single release directory under /srv")
    manifest = read_manifest(args.manifest)
    if manifest["SOURCE_SHA"] != args.expected_sha:
        raise ValueError("manifest source mismatch")
    for key in ARTIFACTS:
        path = Path(manifest[key])
        if (path / "SOURCE_SHA").read_text().strip() != args.expected_sha:
            raise ValueError("artifact source mismatch")
        if "LINUX_ACCEPTANCE=PASS" not in (path / "LINUX_ACCEPTANCE.env").read_text().splitlines():
            raise ValueError("native Linux acceptance required")
    identity = {"accepted_runtime_source_sha": args.runtime_source_sha,
                "corrected_release_tooling_sha": args.expected_sha,
                "final_release_source_sha": args.expected_sha,
                "runtime_source_unchanged": True}
    files = package_files(ROOT, manifest, args.destination, identity)
    write_archive(files, args.output, int(git("show", "-s", "--format=%ct", "HEAD")))
    verify_archive(args.output, files)
    print("DEPLOYMENT_DEPENDENCY_COMPLETENESS_GUARD=PASS")
    print("EXTRACTED_DEPLOYMENT_ENTRYPOINT=PASS")
    print("FINAL_RELEASE_ARCHIVE_SHA256=" + sha(args.output.read_bytes()))
    print("FINAL_RELEASE_ARCHIVE_SIZE=" + str(args.output.stat().st_size))


if __name__ == "__main__":
    main()
