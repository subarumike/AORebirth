"""Run the operator-approved scoped build and regression."""

import os
from pathlib import Path
import re
import shutil
import subprocess


class ValidationError(RuntimeError):
    pass


OS_ENV = {
    "APPDATA", "COMSPEC", "HOME", "HOMEDRIVE", "HOMEPATH", "LOCALAPPDATA",
    "PATH", "PATHEXT", "PROGRAMDATA", "PROGRAMFILES", "PROGRAMFILES(X86)",
    "PROGRAMW6432", "SYSTEMDRIVE", "SYSTEMROOT", "TEMP", "TMP", "USERPROFILE", "WINDIR",
}
PROJECT = re.compile(r"[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.-]+)*\.csproj")
FILTER = re.compile(r"FullyQualifiedName~[A-Za-z_][A-Za-z0-9_.]*")


def executable(value, label, root):
    if not isinstance(value, (str, Path)) or not Path(value).is_absolute():
        raise ValidationError(f"{label} must be an absolute executable path.")
    path = Path(value).resolve()
    if (not path.is_file() or path.is_relative_to(root)
            or (os.name == "nt" and path.suffix.lower() != ".exe") or not os.access(path, os.X_OK)):
        raise ValidationError(f"{label} must be an executable outside the worktree.")
    return str(path)


def tracked_project(name, root, env):
    if (not isinstance(name, str) or not PROJECT.fullmatch(name)
            or any(part in {".", ".."} for part in name.split("/"))):
        raise ValidationError("Expected a safe, relative .csproj path.")
    path = root / name
    if not path.is_file() or path.resolve() != path:
        raise ValidationError("A validation project is missing or is a link.")
    try:
        result = subprocess.run(
            ["git", "cat-file", "-t", f"HEAD:{name}"], cwd=root, env=env,
            stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
            check=False, timeout=15,
        )
    except (OSError, subprocess.SubprocessError):
        raise ValidationError("Could not check the pinned validation project.") from None
    if result.returncode or result.stdout.strip() != b"blob":
        raise ValidationError("A validation project is absent from the pinned commit.")
    return name


def run_step(command, label, root, env, timeout, marker=None):
    try:
        result = subprocess.run(
            command, cwd=root, env=env, stdin=subprocess.DEVNULL, check=False, timeout=timeout,
            stdout=subprocess.PIPE if marker else subprocess.DEVNULL, stderr=subprocess.STDOUT,
        )
    except subprocess.TimeoutExpired:
        raise ValidationError(f"{label} timed out after {timeout} seconds.") from None
    except OSError:
        raise ValidationError(f"{label} could not start.") from None
    if marker:
        if result.returncode == 1 and any(line.startswith(marker.encode()) for line in result.stdout.splitlines()):
            return f"{label}: expected failure reproduced (exit 1)"
        raise ValidationError(f"{label} did not reproduce the expected failure (exit {result.returncode}).")
    if result.returncode:
        raise ValidationError(f"{label} failed (exit {result.returncode}).")
    return f"{label}: passed (exit 0)"


def validate(worktree, plan, *, baseline):
    worktree = worktree.resolve(strict=True)
    timeout = plan.get("validation_timeout")
    if type(timeout) is not int or not 1 <= timeout <= 1800:
        raise ValidationError("validation_timeout must be 1–1800 seconds per step.")
    env = {key: value for key, value in os.environ.items() if key.upper() in OS_ENV}
    env.update(DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1",
               DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1")
    build_project = tracked_project(plan.get("build_project"), worktree, env)
    test_project = tracked_project(plan.get("test_project"), worktree, env)
    dotnet = executable(shutil.which("dotnet"), "dotnet", worktree)
    build = ["build", build_project, "--nologo"]
    prefix = []
    marker = None
    if plan["mode"] == "fixture":
        argument = plan.get("fixture_test_arg")
        if not isinstance(argument, str) or not re.fullmatch(r"--[a-z][a-z0-9-]{0,63}", argument):
            raise ValidationError("Invalid fixture regression argument.")
        if baseline:
            marker = plan.get("fixture_failure_marker")
            if not isinstance(marker, str) or not 1 <= len(marker) <= 256 or "\n" in marker or "\r" in marker:
                raise ValidationError("A fixture baseline needs an exact expected-failure marker.")
        regression = ["run", "--no-build", "--project", test_project, "--", argument]
    else:
        selector = plan.get("test_filter")
        if not isinstance(selector, str) or not FILTER.fullmatch(selector):
            raise ValidationError("The product plan needs one approved FullyQualifiedName~ test filter.")
        runner = plan.get("validation_runner")
        if not isinstance(runner, list) or not runner or any(not isinstance(arg, str) or "\0" in arg for arg in runner):
            raise ValidationError("validation_runner must be trusted executable argv.")
        shell = Path(os.environ.get("SystemRoot", "C:/Windows")) / "System32" / "cmd.exe"
        if re.search(r"[&|<>()^%!\"\r\n]", dotnet):
            raise ValidationError("dotnet has an unsafe path for the Windows release wrapper.")
        prefix = [
            executable(runner[0], "validation_runner", worktree), *runner[1:],
            executable(shell, "cmd.exe", worktree), "/d", "/c",
            r"tools\windows_release_contract.cmd", "run-feature-validation", "--",
        ]
        regression = ["test", test_project, "--filter", selector, "--nologo"]
    return [
        run_step([*prefix, dotnet, *build], "Scoped build", worktree, env, timeout),
        run_step([*prefix, dotnet, *regression], "Focused regression", worktree, env, timeout, marker),
    ]
