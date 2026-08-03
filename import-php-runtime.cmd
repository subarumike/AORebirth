@echo off
setlocal

if "%~1"=="" goto Usage
if "%~2"=="" goto Usage

set "ARCHIVE_PATH=%~f1"
set "EXPECTED_VERSION=%~2"
set "MANIFEST_PATH=%~dp0AORebirth\Config\PhpRuntime.manifest.xml"
set "INI_PATH=%~dp0AORebirth\Config\WebEngine.php.ini"
set "TARGET_PATH=%~dp0AORebirth\Built\Debug\php"
set "SUPPLY_TOOL=%~dp0Tools\php_runtime_supply.py"

call "%~dp0status-engines.cmd" --prestart WebEngine >nul
if errorlevel 1 (
    echo [AORebirth PHP Import] WebEngine must be fully stopped with no conflicting process or listener; import was not attempted.
    exit /b 3
)

if not exist "%SUPPLY_TOOL%" (
    echo [AORebirth PHP Import] The checked-in PHP supply tool is missing; import was not attempted.
    exit /b 4
)

python "%SUPPLY_TOOL%" --manifest "%MANIFEST_PATH%" --ini "%INI_PATH%" import --archive "%ARCHIVE_PATH%" --version "%EXPECTED_VERSION%" --target "%TARGET_PATH%"
set "IMPORT_EXIT=%ERRORLEVEL%"
if not "%IMPORT_EXIT%"=="0" (
    echo [AORebirth PHP Import] Import failed; the prior runtime was preserved when rollback was possible.
    exit /b %IMPORT_EXIT%
)

echo [AORebirth PHP Import] PASS.
exit /b 0

:Usage
echo Usage: import-php-runtime.cmd ^<local-official-zip^> ^<exact-version^>
exit /b 2
