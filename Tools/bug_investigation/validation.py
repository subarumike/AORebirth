"""Run the operator-approved scoped build and regression."""

import os
from pathlib import Path
import re
import shutil
import subprocess
from threading import Thread
import uuid
from xml.etree import ElementTree


class ValidationError(RuntimeError):
    pass


class StoragePreflightError(ValidationError):
    pass


OS_ENV = {
    "APPDATA", "COMSPEC", "HOME", "HOMEDRIVE", "HOMEPATH", "LOCALAPPDATA",
    "OS", "PATH", "PATHEXT", "PROGRAMDATA", "PROGRAMFILES", "PROGRAMFILES(X86)",
    "PROGRAMW6432", "SYSTEMDRIVE", "SYSTEMROOT", "TEMP", "TMP", "USERPROFILE", "WINDIR",
}
PROJECT = re.compile(r"[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.-]+)*\.csproj")
FILTER = re.compile(r"FullyQualifiedName~[A-Za-z_][A-Za-z0-9_.]*")
ZONE_TEST_PROJECT = "AORebirth/Server/ZoneEngine_New.Tests/ZoneEngine_New.Tests.csproj"
ZONE_TEST_SCOPES = {"NpcAiFocusedTests", "MissionDestinationFocusedTests"}
TRX_NAMESPACE = {"trx": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
TRX_NAME = "regression.trx"
LOG_LIMIT = 256 * 1024


def storage_preflight(worktree, source):
    if os.name != "nt":
        raise StoragePreflightError("Product validation requires the Windows storage preflight.")
    env = {key: value for key, value in os.environ.items() if key.upper() in OS_ENV}
    try:
        profile = Path(os.environ["USERPROFILE"])
        script = profile / ".codex" / "storage-preflight.ps1"
        shell = Path(os.environ["SystemRoot"]) / "System32/WindowsPowerShell/v1.0/powershell.exe"
        if not script.is_file() or not shell.is_file():
            raise ValueError
        growth = {}
        for path, reserve in ((worktree, 8), (source, 1), (profile, 2),
                              (Path(os.environ["TEMP"]), 1)):
            drive = path.drive.upper()
            if not re.fullmatch(r"[A-Z]:", drive):
                raise ValueError
            growth[drive[0]] = growth.get(drive[0], 0) + reserve
        env["AOREBIRTH_BUG_STORAGE_PREFLIGHT"] = str(script)
        entries = "; ".join(f"{drive} = {size}" for drive, size in sorted(growth.items()))
        command = f"& $env:AOREBIRTH_BUG_STORAGE_PREFLIGHT -GrowthGiB @{{ {entries} }}"
        result = subprocess.run(
            [str(shell), "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", command],
            cwd=source, env=env, stdin=subprocess.DEVNULL, capture_output=True,
            text=True, errors="replace", timeout=30, check=False,
        )
    except (KeyError, ValueError, OSError, subprocess.SubprocessError):
        raise StoragePreflightError("Could not run the existing Windows storage preflight.") from None
    if result.returncode:
        detail = re.search(r"Storage preflight refused [A-Z]: [^\r\n]+", result.stderr)
        raise StoragePreflightError(detail[0] if detail else "Windows storage preflight refused the repair.")


def executable(value, label, root):
    if not isinstance(value, (str, Path)) or not Path(value).is_absolute():
        raise ValidationError(f"{label} must be an absolute executable path.")
    path = Path(value).resolve()
    if (not path.is_file() or path.is_relative_to(root)
            or (os.name == "nt" and path.suffix.lower() != ".exe") or not os.access(path, os.X_OK)):
        raise ValidationError(f"{label} must be an executable outside the worktree.")
    return str(path)


def runner_command(plan, worktree):
    runner = plan.get("validation_runner")
    if (not isinstance(runner, list) or not runner
            or any(not isinstance(arg, str) or "\0" in arg for arg in runner)):
        raise ValidationError("validation_runner must be trusted executable argv.")
    return [executable(runner[0], "validation_runner", worktree), *runner[1:]]


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


def record_step(command, root, env, timeout, log):
    process = subprocess.Popen(command, cwd=root, env=env, stdin=subprocess.DEVNULL,
                               stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    tail = bytearray()
    omitted = 0
    read_error = []

    def drain():
        nonlocal omitted
        try:
            with process.stdout as stream:
                while chunk := stream.read1(64 * 1024):
                    tail.extend(chunk)
                    if len(tail) > LOG_LIMIT:
                        excess = len(tail) - LOG_LIMIT
                        del tail[:excess]
                        omitted += excess
        except OSError as error:
            read_error.append(error)

    reader = Thread(target=drain, daemon=True)
    reader.start()
    try:
        try:
            status = process.wait(timeout=timeout)
        except (subprocess.TimeoutExpired, KeyboardInterrupt):
            # The trusted runner owns its descendants and must terminate them when its handle closes.
            process.kill()
            process.wait(timeout=5)
            raise
    finally:
        reader.join(timeout=5)
        with log.open("xb") as stream:
            if omitted:
                stream.write(b"[Earlier output omitted to bound log size]\n")
            stream.write(bytes(tail))
        if reader.is_alive() or read_error:
            raise ValidationError("The trusted validation process did not close its output stream.")
    return subprocess.CompletedProcess(command, status, bytes(tail))


def run_step(command, label, root, env, timeout, marker=None, *, log=None):
    try:
        if log is not None:
            result = record_step(command, root, env, timeout, log)
        else:
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


def passed_regression(path, selector, *, archive=None):
    try:
        if path.resolve(strict=True) != path or not path.is_file() or path.stat().st_nlink != 1:
            raise ValueError
        with path.open("rb") as file:
            data = file.read(4_194_305)
        if len(data) > 4_194_304 or b"\0" in data or b"<!DOCTYPE" in data:
            raise ValueError
        root = ElementTree.fromstring(data)
        if root.tag != f"{{{TRX_NAMESPACE['trx']}}}TestRun":
            raise ValueError
        counters = root.find("trx:ResultSummary/trx:Counters", TRX_NAMESPACE)
        executed, passed, failed = (int(counters.attrib[name]) for name in ("executed", "passed", "failed"))
    except (OSError, RuntimeError, AttributeError, KeyError, ValueError, ElementTree.ParseError):
        raise ValidationError("Focused regression did not produce a valid VSTest TRX result.") from None

    expected = selector.split("~", 1)[1].casefold()
    matching_ids = set()
    for test in root.findall("trx:TestDefinitions/trx:UnitTest", TRX_NAMESPACE):
        method = test.find("trx:TestMethod", TRX_NAMESPACE)
        if method is None:
            continue
        class_name = method.get("className", "").partition(",")[0].strip()
        method_name = method.get("name", "")
        test_id = test.get("id")
        if class_name and method_name and test_id and expected in f"{class_name}.{method_name}".casefold():
            matching_ids.add(test_id)
    results = root.findall("trx:Results//trx:UnitTestResult", TRX_NAMESPACE)
    matching = sum(result.get("testId") in matching_ids and result.get("outcome") == "Passed"
                   for result in results)
    if not 0 < matching <= passed <= executed or failed != 0:
        raise ValidationError("Focused regression has no successful result for the approved test filter.")
    if archive is not None:
        with archive.open("xb") as stream:
            stream.write(data)
    return matching


def run_regression(command, worktree, env, timeout, selector, *, evidence=None):
    directory = worktree / ".local" / f"bug-regression-{uuid.uuid4().hex}"
    result = directory / TRX_NAME
    if directory.resolve() != directory or re.search(r'[&|<>()^%!"\r\n]', str(directory)):
        raise ValidationError("Expected a direct, safe regression result path inside the worktree.")
    try:
        ignored = subprocess.run(
            ["git", "check-ignore", "-q", "--", result.relative_to(worktree).as_posix()],
            cwd=worktree, env=env, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL, check=False, timeout=15,
        )
        if ignored.returncode:
            raise ValidationError("The regression result path must be Git-ignored.")
        directory.mkdir(parents=True)
    except (OSError, subprocess.SubprocessError):
        raise ValidationError("Could not create a fresh regression result directory.") from None

    try:
        argv = [*command, "--logger", f"trx;LogFileName={TRX_NAME}", "--results-directory", str(directory)]
        if evidence is None:
            run_step(argv, "Focused regression", worktree, env, timeout)
        else:
            run_step(argv, "Focused regression", worktree, env, timeout, log=evidence / "regression.log")
        matching = passed_regression(result, selector, archive=evidence / TRX_NAME if evidence else None)
        return f"Focused regression: {matching} matching test result(s) passed"
    finally:
        resolved = directory.resolve()
        if resolved == directory and resolved.is_relative_to(worktree) and directory.is_dir():
            shutil.rmtree(directory)


def validate(worktree, plan, *, baseline, evidence_dir=None):
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
        shell = Path(os.environ.get("SystemRoot", "C:/Windows")) / "System32" / "cmd.exe"
        if re.search(r"[&|<>()^%!\"\r\n]", dotnet):
            raise ValidationError("dotnet has an unsafe path for the Windows release wrapper.")
        prefix = [
            *runner_command(plan, worktree),
            executable(shell, "cmd.exe", worktree), "/d", "/c",
            r"tools\windows_release_contract.cmd", "run-feature-validation", "--",
        ]
        regression = ["test", test_project, "--filter", selector, "--nologo"]
        scope = plan.get("test_scope")
        if scope is not None:
            if (test_project != ZONE_TEST_PROJECT or not isinstance(scope, str)
                    or scope not in ZONE_TEST_SCOPES):
                raise ValidationError("test_scope must name an approved ZoneEngine focused test target.")
            regression.append(f"-p:{scope}=true")
    evidence = None
    if evidence_dir is not None and plan["mode"] == "aorebirth":
        if (evidence_dir.resolve(strict=True) != evidence_dir or not evidence_dir.is_dir()
                or evidence_dir.is_relative_to(worktree)):
            raise ValidationError("Validation evidence needs a private directory outside the worktree.")
        evidence = evidence_dir / f"validation-{uuid.uuid4().hex}"
        evidence.mkdir()
    command = [*prefix, dotnet, *build]
    built = (run_step(command, "Scoped build", worktree, env, timeout, log=evidence / "build.log")
             if evidence is not None else run_step(command, "Scoped build", worktree, env, timeout))
    command = [*prefix, dotnet, *regression]
    if plan["mode"] == "fixture":
        tested = run_step(command, "Focused regression", worktree, env, timeout, marker)
    elif evidence is None:
        tested = run_regression(command, worktree, env, timeout, selector)
    else:
        tested = run_regression(command, worktree, env, timeout, selector, evidence=evidence)
    return [built, tested]
