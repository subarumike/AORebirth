"""Reclaim only worker-owned, durably published repair worktrees."""

import base64
import hashlib
import json
import os
from pathlib import Path
import re
import stat


class CleanupRetained(RuntimeError):
    pass


RECEIPT_DIR = re.compile(r"feature-validation-[0-9a-f]{32}")
OBSERVATION = re.compile(r"[0-9a-f]{32}\.json")
ARCHIVE_LIMIT = 8 * 1024 * 1024
BUILD_EXTENSIONS = {".dll", ".exe", ".pdb", ".so", ".dylib"}
GIT_OPERATIONS = {"MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "BISECT_START",
                  "rebase-apply", "rebase-merge", "sequencer"}


def direct_info(path):
    info = path.lstat()
    if (stat.S_ISLNK(info.st_mode)
            or getattr(info, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT
            or not (stat.S_ISDIR(info.st_mode) or stat.S_ISREG(info.st_mode))):
        raise CleanupRetained("The attempt contains a link or special file.")
    return info


def registration(source, path, git):
    for record in git(source, "worktree", "list", "--porcelain", "-z").split("\0\0"):
        name, *fields = record.split("\0")
        if name.startswith("worktree ") and Path(name.removeprefix("worktree ")) == path:
            return fields
    return None


def owner(source, path, branch, git):
    if path.resolve(strict=True) != path or not stat.S_ISDIR(direct_info(path).st_mode):
        raise CleanupRetained("The attempt path is not a direct directory.")
    common = Path(git(source, "rev-parse", "--path-format=absolute", "--git-common-dir").strip()).resolve()
    admin = Path(git(path, "rev-parse", "--absolute-git-dir").strip())
    pointer = direct_info(path / ".git")
    if (admin.resolve(strict=True) != admin or admin.parent != common / "worktrees"
            or not stat.S_ISREG(pointer.st_mode) or pointer.st_nlink != 1):
        raise CleanupRetained("The attempt is not an owned linked worktree.")
    metadata = direct_info(admin)
    root = direct_info(path)
    return {"path": str(path), "branch": branch, "admin": str(admin),
            "identity": [root.st_dev, root.st_ino, metadata.st_dev, metadata.st_ino]}


def verify_worktree(source, path, state, git):
    expected = state["worktree_owner"]
    branch = f"refs/heads/{expected['branch']}"
    current = registration(source, path, git)
    if current is None or not path.exists():
        if (current is None and not os.path.lexists(path) and state.get("cleanup_status") == "pending"
                and path.resolve() == path):
            return False
        raise CleanupRetained("The attempt path and Git registration disagree.")
    if set(current) != {f"HEAD {state['commit']}", f"branch {branch}"}:
        raise CleanupRetained("The attempt has a Git lock or a changed registration.")
    if owner(source, path, expected["branch"], git) != expected:
        raise CleanupRetained("The attempt no longer matches the worker ownership checkpoint.")
    admin = Path(expected["admin"])
    if (any(file.name.endswith(".lock") or file.name in GIT_OPERATIONS for file in admin.iterdir())
            or os.path.lexists(admin.parent.parent / (branch + ".lock"))):
        raise CleanupRetained("Git is using the repair attempt.")
    if (git(source, "rev-parse", "--verify", branch).strip() != state["commit"]
            or git(path, "rev-list", "--parents", "-n", "1", "HEAD").split()
            != [state["commit"], state["base_commit"]]
            or git(path, "rev-parse", "HEAD^{tree}").strip() != state["tree"]
            or git(path, "write-tree").strip() != state["tree"]
            or git(path, "-c", "core.fsmonitor=false", "status", "--porcelain", "--untracked-files=all",
                   "--ignore-submodules=none")):
        raise CleanupRetained("The attempt contains unpublished work or a changed index.")
    return True


def worktree_inventory(path, git):
    entries = git(path, "ls-files", "-v", "-z").rstrip("\0").split("\0")
    if any(not entry.startswith("H ") for entry in entries):
        raise CleanupRetained("The attempt contains sparse or specially marked Git files.")
    tracked = {Path(entry[2:]) for entry in entries}
    ignored = {Path(name) for name in
               git(path, "ls-files", "--others", "--ignored", "--exclude-standard", "-z").split("\0")
               if name}
    known = tracked | ignored | {Path(".git")}
    directories = {parent for name in known for parent in name.parents}
    pending = [Path(".")]
    untracked = {}
    size = 0
    while pending:
        directory = pending.pop()
        for item in (path / directory).iterdir():
            relative = item.relative_to(path)
            info = direct_info(item)
            if stat.S_ISDIR(info.st_mode) and relative in directories:
                pending.append(relative)
            elif stat.S_ISREG(info.st_mode) and relative in known:
                size += info.st_size
                if relative in ignored:
                    if info.st_nlink != 1:
                        raise CleanupRetained("The attempt contains a linked artifact.")
                    untracked[relative] = info
            else:
                raise CleanupRetained("The attempt contains an unrecognized file or directory.")
    if set(untracked) != ignored:
        raise CleanupRetained("The ignored artifact inventory changed during inspection.")
    return tracked, untracked, size


def generated_outputs(path, tracked, ignored):
    """Use MSBuild's own file lists and exact restore artifact names."""
    generated = set()
    for project in (name for name in tracked if name.suffix == ".csproj"):
        obj = project.parent / "obj"
        allowed = {"project.assets.json", "project.nuget.cache", f"{project.name}.nuget.dgspec.json",
                   f"{project.name}.nuget.g.props", f"{project.name}.nuget.g.targets"}
        generated.update(obj / name for name in allowed)
        for manifest, info in ignored.items():
            if (manifest.name != f"{project.name}.FileListAbsolute.txt"
                    or not manifest.is_relative_to(obj) or len(manifest.relative_to(obj).parts) != 3
                    or info.st_size > 1024 * 1024):
                continue
            listed = (path / manifest).read_text(encoding="utf-8-sig").splitlines()
            roots = (project.parent / "bin", obj)
            generated.add(manifest)
            for name in listed:
                absolute = Path(name)
                if not absolute.is_absolute() or not absolute.is_relative_to(path):
                    continue
                relative = absolute.relative_to(path)
                compiled = (relative.suffix in BUILD_EXTENSIONS
                            or relative.name in {project.stem, f"{project.stem}.deps.json",
                                                 f"{project.stem}.runtimeconfig.json"})
                if (compiled and relative in ignored and any(relative.is_relative_to(root) for root in roots)
                        and not {"gamedata", "captures", "logs", "testresults"}.intersection(
                            part.casefold() for part in relative.parts)):
                    generated.add(relative)
            configuration = manifest.parent
            generated.update(configuration / f"{project.stem}{ending}" for ending in (
                ".assets.cache", ".AssemblyInfo.cs", ".AssemblyInfoInputs.cache",
                ".GeneratedMSBuildEditorConfig.editorconfig", ".GlobalUsings.g.cs", ".genruntimeconfig.cache",
                ".sourcelink.json",
            ))
            generated.update(configuration / f"{project.name}{ending}" for ending in (
                ".AssemblyReference.cache", ".CoreCompileInputs.cache", ".Up2Date",
            ))
            generated.add(configuration / f".NETCoreApp,Version=v{configuration.name.removeprefix('net')}.AssemblyAttributes.cs")
    return generated


def receipt_roots(path, ignored, state):
    roots = set()
    for name, info in ignored.items():
        if (name.name != "feature-validation.json" or name.parts[0] != ".local"
                or not RECEIPT_DIR.fullmatch(name.parent.name) or info.st_size > ARCHIVE_LIMIT):
            continue
        receipt = json.loads((path / name).read_bytes())
        source = receipt.get("FEATURE_SOURCE") if isinstance(receipt, dict) else None
        if (isinstance(source, dict) and receipt.get("BUILD_PURPOSE") == "FEATURE_VALIDATION"
                and receipt.get("DEPLOYABLE") is False
                and receipt.get("WINDOWS_USER_ACCEPTED") == "NO" and receipt.get("VALIDATION_RESULT") == "PASS"
                and source.get("HEAD") == state["base_commit"]
                and source.get("BRANCH") == state["worktree_owner"]["branch"]):
            roots.add(name.parent)
    return roots


def validation_artifacts(path, state, git):
    tracked, ignored, _ = worktree_inventory(path, git)
    generated = generated_outputs(path, tracked, ignored)
    roots = receipt_roots(path, ignored, state)
    recorded = {}
    for name, info in ignored.items():
        archive = name.parent in roots and name.name in {"manifest.json", "feature-validation.json"}
        archive |= (name.parent.name == "build-observations" and name.parent.parent in roots
                    and OBSERVATION.fullmatch(name.name) is not None)
        if archive or name in generated:
            recorded[name.as_posix()] = [info.st_size, info.st_mtime_ns, info.st_dev, info.st_ino, archive]
    return recorded


def archive_artifacts(path, state, git):
    _, ignored, size = worktree_inventory(path, git)
    recorded = state.get("validation_artifacts", {})
    if set(recorded) != {name.as_posix() for name in ignored}:
        raise CleanupRetained("The attempt contains unrecognized ignored artifacts.")
    archived = {}
    total = 0
    for name, info in ignored.items():
        expected = recorded[name.as_posix()]
        if expected[:4] != [info.st_size, info.st_mtime_ns, info.st_dev, info.st_ino]:
            raise CleanupRetained("A validation artifact changed after the build.")
        if not expected[4]:
            continue
        total += info.st_size
        if total > ARCHIVE_LIMIT:
            raise CleanupRetained("Validation evidence exceeds the local archive limit.")
        with (path / name).open("rb") as file:
            data = file.read(info.st_size + 1)
        current = direct_info(path / name)
        if (len(data) != info.st_size or expected[:4] !=
                [current.st_size, current.st_mtime_ns, current.st_dev, current.st_ino]):
            raise CleanupRetained("Validation evidence changed during archival.")
        archived[name.as_posix()] = {"bytes": len(data), "sha256": hashlib.sha256(data).hexdigest(),
                                    "base64": base64.b64encode(data).decode("ascii")}
    return archived, size
