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
        require(next_position > position, "security check sequencing is out of order: " + token)
        position = next_position


def production_files():
    web_engine = ROOT / "AORebirth/Server/WebEngine"
    config = ROOT / "AORebirth/Config"
    extensions = {".cs", ".config", ".csproj", ".xml"}
    files = [path for path in web_engine.rglob("*") if path.is_file() and path.suffix.lower() in extensions]
    files.extend(path for path in config.rglob("*") if path.is_file() and path.suffix.lower() in extensions)
    files.extend(
        [
            ROOT / "AORebirth/Config/WebCoreAssets.manifest.xml",
            ROOT / "AORebirth/Libraries/Source/Utility/Config/Config.cs",
            ROOT / "start-web-engine.cmd",
            ROOT / "import-webcore-assets.cmd",
            ROOT / "validate-webcore-assets.cmd",
            ROOT / "start-engines.cmd",
            ROOT / "start-engines.ps1",
            ROOT / "restart-engines.cmd",
            ROOT / "Tools/run_web_engine_security_tests.cmd",
        ]
    )
    return sorted(set(files), key=lambda path: path.as_posix().lower())


def find_forbidden_tokens(path, text):
    lowered = text.lower()
    literal_tokens = (
        "https://github.com/cellao/cellao-webcore/archive/master.zip",
        "master.zip",
        "main.zip",
        "webcorerepo",
        "webcore.zip",
        "checkwebcore",
        "unzip2",
        '<compile include="checks.cs"',
        "webclient",
        "httpclient",
        "httpwebrequest",
        "ftpwebrequest",
        "webrequest.create",
        "socketshttphandler",
        "tcpclient",
        "udpclient",
        "restclient",
        "downloadfile",
        "downloadstring",
        "downloaddata",
        "invoke-webrequest",
        "invoke-restmethod",
        "start-bitstransfer",
        "bitsadmin",
        "certutil -urlcache",
        "curl.exe",
        "wget.exe",
        "urlretrieve",
        "urllib.request",
        "git clone",
        "git pull",
    )
    findings = [token for token in literal_tokens if token in lowered]

    mutable_reference_patterns = (
        r"github\.com/[^/\s]+/[^/\s]+/(?:archive|zipball|tarball)/",
        r"codeload\.github\.com/",
        r"/archive/(?:refs/heads/)?(?:master|main)(?:\.zip|\.tar\.gz)?",
        r"/releases/latest(?:/|$)",
        r"/latest/download(?:/|$)",
        r"refs/heads/(?:master|main)",
        r"raw\.githubusercontent\.com/[^/\s]+/[^/\s]+/(?:master|main)/",
        r"https?://[^\s\"']+/(?:archive|zipball|tarball)/(?:refs/heads/)?(?:master|main)(?:\.zip|\.tar\.gz)?",
        r"https?://[^\s\"']+/(?:master|main|latest)\.(?:zip|tar\.gz)",
        r"\bwebrequest\b",
        r"\bcurl(?:\.exe)?\b",
        r"\bwget(?:\.exe)?\b",
    )
    findings.extend(
        pattern
        for pattern in mutable_reference_patterns
        if re.search(pattern, lowered, flags=re.MULTILINE)
    )
    return findings


def main():
    detector_cases = (
        "https://example.invalid/project/main.zip",
        "https://example.invalid/archive/main",
        "https://raw.githubusercontent.com/example/project/main/file.php",
        "certutil -urlcache https://example.invalid/payload payload.zip",
        "System.Net.WebClient",
    )
    for detector_case in detector_cases:
        require(find_forbidden_tokens(Path("detector"), detector_case),
                "source guard missed outbound or mutable detector case: " + detector_case)
    require(
        not find_forbidden_tokens(
            Path("detector"),
            "https://github.com/CellAO/CellAO-WebCore 765c3850767b63af1cd259bab7f2f7ca3e97adf9",
        ),
        "source guard rejected immutable repository provenance without an acquisition path",
    )

    failures = []
    for path in production_files():
        require(path.is_file(), "missing production/config/startup input: " + str(path.relative_to(ROOT)))
        text = path.read_text(encoding="utf-8").replace("\r\n", "\n")
        for finding in find_forbidden_tokens(path, text):
            failures.append(str(path.relative_to(ROOT)) + ": " + finding)

    require(not failures, "mutable WebCore bootstrap token remains: " + "; ".join(failures))

    asset_manager = read("AORebirth/Server/WebEngine/WebCoreAssetManager.cs")
    for token in (
        "using System.Net",
        "using System.Diagnostics",
        "Process.Start",
        "Dns.",
        "Socket",
        "TcpClient",
        "UdpClient",
    ):
        require(token not in asset_manager,
                "WebCore asset manager must remain local-only: " + token)
    ordered(
        asset_manager,
        "EnsureExistingAncestorsHaveNoReparsePoints(parentDirectory);",
        "Directory.CreateDirectory(parentDirectory);",
        "EnsureExistingAncestorsHaveNoReparsePoints(parentDirectory);",
    )

    program = read("AORebirth/Server/WebEngine/Program.cs")
    require(program.count("ReleaseWebCoreAssetLease();") == 2,
            "a successful WebEngine start must retain the WebCore lease for process lifetime")
    require("/validate-webcore-manifest" in program,
            "production must expose deterministic parsing of the checked-in manifest authority")

    start_web = read("start-web-engine.cmd")
    require("/validate-webcore-assets" in start_web,
            "WebEngine startup must validate local WebCore assets")
    require("/validate-php-runtime" in start_web,
            "WebEngine startup must retain local PHP validation")
    require("/import-webcore-assets" not in start_web,
            "WebEngine startup must never import WebCore assets implicitly")

    import_webcore = read("import-webcore-assets.cmd")
    require("/import-webcore-assets" in import_webcore,
            "explicit WebCore import wrapper must invoke the offline importer")
    require("%~f1" in import_webcore,
            "explicit WebCore import wrapper must canonicalize the supplied local path before changing directory")
    ordered(
        import_webcore,
        'set "ARCHIVE_PATH=%~f1"',
        'status-engines.cmd" --prestart WebEngine',
        'set "PRESTART_EXIT=%ERRORLEVEL%"',
        'if not "%PRESTART_EXIT%"=="0"',
        "pushd",
        'WebEngine.exe /import-webcore-assets "%ARCHIVE_PATH%" "%EXPECTED_VERSION%"',
    )
    validate_webcore = read("validate-webcore-assets.cmd")
    require("/validate-webcore-assets" in validate_webcore,
            "explicit WebCore validation wrapper must invoke local validation")

    security_runner = read("Tools/run_web_engine_security_tests.cmd")
    require("/validate-webcore-manifest" in security_runner,
            "WebEngine security runner must parse the checked-in manifest with production code")
    require("/self-test-webcore-assets" in security_runner,
            "WebEngine security runner must execute the WebCore asset self-test")
    require("test_webcore_bootstrap_contracts.py" in security_runner,
            "WebEngine security runner must execute the WebCore bootstrap source contract")
    require("HTTP_PROXY=http://127.0.0.1:9" in security_runner and
            "HTTPS_PROXY=http://127.0.0.1:9" in security_runner and
            "ALL_PROXY=http://127.0.0.1:9" in security_runner and
            'set "NO_PROXY="' in security_runner,
            "WebCore security checks must run behind invalid outbound proxy settings")
    ordered(
        security_runner,
        "HTTP_PROXY=http://127.0.0.1:9",
        "test_webcore_bootstrap_contracts.py",
        "/t:Build",
        "fc /b",
        "/validate-webcore-manifest",
        "/self-test-webcore-assets",
    )

    print("[WebCore Bootstrap Contracts] PASS")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except AssertionError as error:
        print("[WebCore Bootstrap Contracts] FAIL: " + str(error))
        sys.exit(1)
