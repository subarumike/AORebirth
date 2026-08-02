from pathlib import Path
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

    print("[Engine Management Contracts] PASS")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except AssertionError as error:
        print("[Engine Management Contracts] FAIL: " + str(error))
        sys.exit(1)
