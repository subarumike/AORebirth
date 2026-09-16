from pathlib import Path
import json
import re
import shutil
import subprocess
import sys
import tempfile


ROOT = Path(__file__).resolve().parents[2]


def read(relative_path):
    return (ROOT / relative_path).read_text(encoding="utf-8").replace("\r\n", "\n")


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def ordered(text, *tokens):
    position = -1
    for token in tokens:
        next_position = text.find(token, position + 1)
        require(next_position >= 0, "missing sequencing token: " + token)
        require(next_position > position, "startup sequencing is out of order: " + token)
        position = next_position


def main():
    start_cmd = read("start-engines.cmd")
    restart_cmd = read("restart-engines.cmd")
    start_web_cmd = read("start-web-engine.cmd")
    start_ps = read("start-engines.ps1")
    stop_ps = read("stop-engines.ps1")
    validator = read("AORebirth/Server/WebEngine/PhpRuntimeValidator.cs")
    handler = read("AORebirth/Server/WebEngine/Handlers/PHPHandler.cs")
    program = read("AORebirth/Server/WebEngine/Program.cs")
    mandatory_gate = read("tools/run_mandatory_integration_gate.cmd")
    windows_acceptance = read("Tools/accept_windows_source.cmd")
    build_cmd = read("tools/build_aorebirth_debug.cmd")
    preflight_cmd = read("preflight-database.cmd")
    solution = read("AORebirth/AORebirth.sln").replace("\\", "/")

    retired_root = ROOT / "AORebirth/Server/ZoneEngine"
    for retired_file in ("ZoneEngine.csproj", "Program.cs"):
        require(not (retired_root / retired_file).exists(),
            "retired engine project/entry point must not return: " + retired_file)
    normalized_build = build_cmd.replace("\\", "/").lower()
    require("zoneengine.csproj" not in normalized_build
        and "server/zoneengine/packages.config" not in normalized_build,
        "normal build must not build or restore the retired engine")
    require('"Server/ZoneEngine_New/ZoneEngine_New.csproj"' in solution
        and '"Server/ZoneEngine/ZoneEngine.csproj"' not in solution,
        "solution must contain only the supported NewEngine zone project")

    ordered(start_cmd, "-ValidateEngineSelectionOnly", "preflight-database.cmd",
            'start-engines.ps1" %*')
    require("-WebOnly" not in start_cmd and "-WithWeb" not in start_cmd,
            "normal startup must not opt into WebEngine")

    ordered(restart_cmd, "-ValidateEngineSelectionOnly", "preflight-database.cmd",
            "stop-engines.cmd", "start-engines.cmd")
    ordered(restart_cmd, "-ValidateSchemaOnly", "stop-engines.cmd", "start-engines.cmd")
    require("running engines were not stopped" in restart_cmd,
            "restart preflight failure must preserve running engines")

    ordered(
        start_web_cmd,
        "preflight-database.cmd",
        'if not exist "%~dp0AORebirth\\Built\\Debug\\WebEngine.exe"',
        "/validate-php-runtime",
        "/validate-webcore-assets",
        'start-engines.ps1" -WebOnly',
    )
    require("--prestart" in start_ps and "--engine-required" in start_ps,
            "startup must use ownership-safe prestart and launched-PID verification")
    require('$configPath = Join-Path $root "AORebirth\\Config\\Config.xml"' in start_ps,
            "startup status probes must use the repository configuration")
    require('$probeArguments = @("--config", $configPath, "--engine-dir", $engineDir) + $Arguments' in start_ps,
            "startup status probes must receive configuration and engine-directory arguments")
    require("Stop-LaunchedEngineProcess" in start_ps and "$launched" in start_ps,
            "startup must track and clean only processes launched by its invocation")
    require("$rollbackStopped" in start_ps,
            "startup rollback must retain metadata when an exact launched process cannot be stopped")
    require("Get-Process -Name" not in start_ps,
            "startup must not trust or manipulate processes by name alone")

    def select_backend(*arguments):
        return subprocess.run(
            ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
             str(ROOT / "start-engines.ps1"), "-PrintEngineSelection", *arguments],
            capture_output=True, text=True, check=False)

    normal = select_backend()
    require(normal.returncode == 0, "normal launcher selection failed")
    require(json.loads(normal.stdout) == {
        "Name": "ZoneEngine_New", "File": "ZoneEngine_New\\ZoneEngine_New.exe"},
        "normal Windows launcher must select the exact ZoneEngine_New backend")
    compatibility = select_backend("-NewZoneEngine")
    require(compatibility.returncode == 0 and json.loads(compatibility.stdout) == json.loads(normal.stdout),
        "NewZoneEngine compatibility switch must select the same sole backend")
    retired = select_backend("-LegacyZoneEngine")
    require(retired.returncode != 0 and "retired" in retired.stderr,
        "retired backend selection must fail before environment or process access")
    require(select_backend("-LegacyZoneEngine", "-NewZoneEngine").returncode != 0,
        "retired selection combined with the compatibility switch must fail closed")
    ordered(start_ps, "if ($LegacyZoneEngine)", "if ($ValidateEngineSelectionOnly)",
        "$processPath =", "$configuration.Load($configPath)")

    # Exercise real wrappers against inert fixtures. A retired selector must not
    # reach database preflight, a stop wrapper, or startup, regardless of installed binaries.
    with tempfile.TemporaryDirectory(prefix="aorebirth-retired-engine-") as directory:
        fixture = Path(directory)
        for name in ("start-engines.ps1", "start-engines.cmd", "restart-engines.cmd",
                     "start-engines-with-web.cmd", "start-web-engine.cmd"):
            shutil.copyfile(ROOT / name, fixture / name)
        for name in ("preflight-database.cmd", "stop-engines.cmd"):
            (fixture / name).write_text(
                '@echo off\necho touched>"%~dp0unexpected-operation.txt"\nexit /b 71\n',
                encoding="ascii")
        for name in ("start-engines.cmd", "restart-engines.cmd",
                     "start-engines-with-web.cmd", "start-web-engine.cmd"):
            rejected = subprocess.run(["cmd", "/d", "/c", str(fixture / name), "-LegacyZoneEngine"],
                capture_output=True, text=True, check=False)
            require(rejected.returncode != 0 and "retired" in rejected.stderr,
                name + " must reject the retired backend explicitly")
            require(not (fixture / "unexpected-operation.txt").exists(),
                name + " must reject the retired backend before preflight or shutdown")

    retired_stop = subprocess.run(
        ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
         str(ROOT / "stop-engines.ps1"), "-EngineName", "ZoneEngine"],
        capture_output=True, text=True, check=False)
    require(retired_stop.returncode != 0 and "ParameterArgumentValidationError" in retired_stop.stderr,
        "retired shutdown selection must fail parameter validation before process access")
    require('Name = "ZoneEngine"' not in start_ps + stop_ps,
        "engine management must not retain a runnable retired backend definition")
    require('status-engines.cmd") @args' in read("status-engines.ps1"),
        "PowerShell status compatibility entry point must use the exact ownership probe")
    require("taskkill" not in build_cmd.lower(),
        "normal backend build must not kill unrelated dotnet/runtime processes")
    require("run_zoneengine_new_tests.cmd" in mandatory_gate,
        "normal mandatory acceptance must exercise NewEngine tests and startup validation")
    ordered(start_ps, "$newZoneExecutable --validate-startup", "$newZoneExecutable --validate-database", "if ($ValidateSchemaOnly)")
    require(start_ps.count('$prestartExit = Invoke-EngineStatusProbe -Arguments @("--prestart", $processName)') == 1,
        "all engines must use the same ownership-safe and idempotent prestart")
    require("pre-start check requires no process" not in start_ps,
        "a healthy managed New backend must not be rejected by an obsolete special prestart")
    require("call Tools\\run_newengine_content_architecture_guard.cmd --check" in windows_acceptance
        and "if errorlevel 1 goto :content_failed" in windows_acceptance
        and "echo CONTENT_ARCHITECTURE_GUARD=PASS" in windows_acceptance,
        "Windows acceptance must validate the default NewEngine content architecture")
    require("for %%S in (2 3 4 5 7) do (" in windows_acceptance
        and "call SharedBuild\\verify-stage%%S-contracts.cmd" in windows_acceptance
        and "if errorlevel 1 goto :contracts_failed" in windows_acceptance,
        "Windows acceptance must refuse stale Windows contract baselines")
    ordered(windows_acceptance, "echo BUILD=%BUILD_RESULT%",
        "call SharedBuild\\verify-stage%%S-contracts.cmd", "echo WINDOWS_CONTRACTS=PASS")

    require("Get-Process -Name" not in stop_ps,
            "shutdown must not fall back to killing processes by name")
    require("metadataIsTrusted" in stop_ps and "StartedAt" in stop_ps and "--prestart" in stop_ps,
            "shutdown must validate managed PID path/start identity and released ports")
    require(stop_ps.count("foreach ($engine in $engines)") == 2,
            "port release checks must run after all selected engines stop")
    ordered(stop_ps, "Stop-EngineProcess -Process $metadataProcess",
            "# Stop every selected managed process", "--prestart ZoneEngine_New")
    require('$configPath = Join-Path $root "AORebirth\\Config\\Config.xml"' in stop_ps,
            "shutdown status probes must use the repository configuration")
    require("$statusProbe --config $configPath --engine-dir $engineDir --prestart $engine.Name" in stop_ps,
            "shutdown release probes must receive configuration and engine-directory arguments")
    require(not re.search(r"(?m)^\s*-and\b", stop_ps),
            "PowerShell continuation operators must remain on the preceding condition line")

    forbidden_php_download_tokens = (
        "php-5.5.10",
        "windows.php.net",
        "aocell.info/php.ini",
        "UrlDownloadFileCompleted",
    )
    php_sources = validator + handler + program
    for token in forbidden_php_download_tokens:
        require(token not in php_sources, "obsolete PHP downloader remains: " + token)

    require("WebClient" not in validator and "DownloadFile" not in validator,
            "PHP runtime validator must remain network-free")
    require("runtime.ExecutablePath" in handler and "runtime.RuntimeDirectory" in handler,
            "PHP execution must use canonical validated local paths")
    ordered(program, "/self-test-php-runtime", "/validate-php-runtime", "bool headless")
    ordered(program, "/self-test-webcore-assets", "/validate-webcore-assets", "bool headless")
    ordered(program, "/import-webcore-assets", "bool headless")

    normal_gate_sources = {
        "mandatory gate": mandatory_gate,
        "debug build": build_cmd,
        "database preflight": preflight_cmd,
        "start-engines.cmd": start_cmd,
        "start-engines.ps1": start_ps,
        "restart-engines.cmd": restart_cmd,
    }
    forbidden_capture_gate_tokens = (
        "AOSharpLiveCapture",
        "tools\\generate_capture_backed_npc_combat_inventory.cmd",
        "Tools\\generate_capture_backed_npc_combat_inventory.cmd",
        "run_generated_combat_concurrency_tests.cmd",
        "capture_backed_npc_combat_generation_manifest",
    )
    for name, source in normal_gate_sources.items():
        for token in forbidden_capture_gate_tokens:
            require(token not in source,
                    name + " must not depend on raw capture tooling: " + token)

    ordered(
        mandatory_gate,
        "captured combat packet serialization",
        "run_aotomation_messaging_tests.cmd",
        "CapturedEnemyCombatGeneratedPacketFixtureTests",
    )

    explicit_capture_tool = read("tools/generate_capture_backed_npc_combat_inventory.cmd")
    require("generated_combat_pipeline.py" in explicit_capture_tool,
            "explicit capture-backed combat generation tool must remain available")
    require("extract_capture_backed_npc_combat.py --self-test" in explicit_capture_tool,
            "explicit capture analyzer self-test must remain available")
    require(
        "call Tools\\run_retained_legacy_guard.cmd"
        in windows_acceptance,
        "Windows acceptance must reject retained Legacy implementations",
    )
    require(
        "validate-current" not in windows_acceptance,
        "Windows acceptance must not require unavailable historical raw capture roots",
    )

    print("[Engine Management Contracts] PASS")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except AssertionError as error:
        print("[Engine Management Contracts] FAIL: " + str(error))
        sys.exit(1)
