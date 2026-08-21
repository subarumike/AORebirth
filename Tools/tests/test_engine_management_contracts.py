from pathlib import Path
import re
import sys


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
    linux_publish_scripts = {
        "LinuxBuild/build-linux.cmd": read("LinuxBuild/build-linux.cmd"),
        "LinuxBuild/publish-zoneengine.cmd": read("LinuxBuild/publish-zoneengine.cmd"),
        "LinuxBuild/publish-loginengine.cmd": read("LinuxBuild/publish-loginengine.cmd"),
        "LinuxBuild/publish-chatengine.cmd": read("LinuxBuild/publish-chatengine.cmd"),
    }

    ordered(start_cmd, "preflight-database.cmd", "start-engines.ps1")
    require("-WebOnly" not in start_cmd and "-WithWeb" not in start_cmd,
            "normal startup must not opt into WebEngine")

    ordered(restart_cmd, "preflight-database.cmd", "stop-engines.cmd", "start-engines.cmd")
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

    require("Get-Process -Name" not in stop_ps,
            "shutdown must not fall back to killing processes by name")
    require("metadataIsTrusted" in stop_ps and "StartedAt" in stop_ps and "--prestart" in stop_ps,
            "shutdown must validate managed PID path/start identity and released ports")
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
        **linux_publish_scripts,
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
        "generated combat runtime contracts",
        "run_aotomation_messaging_tests.cmd",
        "CapturedEnemyCombatProfileCatalog",
    )

    explicit_capture_tool = read("tools/generate_capture_backed_npc_combat_inventory.cmd")
    require("generated_combat_pipeline.py" in explicit_capture_tool,
            "explicit capture-backed combat generation tool must remain available")
    require("extract_capture_backed_npc_combat.py --self-test" in explicit_capture_tool,
            "explicit capture analyzer self-test must remain available")
    require(
        "call Tools\\generate_capture_backed_npc_combat_inventory.cmd --check"
        in windows_acceptance,
        "Windows acceptance must validate raw-independent generated-combat integrity",
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
