@echo off
setlocal EnableExtensions

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-engines.ps1" -ValidateEngineSelectionOnly %*
if errorlevel 1 exit /b 1

pushd "%~dp0" >nul
if errorlevel 1 (
    echo [AORebirth Restart] Failed to switch to repository root.
    exit /b 1
)

echo [AORebirth Restart] Validating database before stopping any engine...
call "%~dp0preflight-database.cmd"
set PREFLIGHT_EXIT=%ERRORLEVEL%
if not "%PREFLIGHT_EXIT%"=="0" (
    echo [AORebirth Restart] Database preflight failed with exit code %PREFLIGHT_EXIT%; running engines were not stopped.
    popd >nul
    exit /b %PREFLIGHT_EXIT%
)

rem Validate NewEngine schema before stopping any running engine.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-engines.ps1" -ValidateSchemaOnly %*
set "SCHEMA_EXIT=%ERRORLEVEL%"
if not "%SCHEMA_EXIT%"=="0" (
    echo [AORebirth Restart] NewEngine schema readiness failed; running engines were not stopped.
    popd >nul
    exit /b %SCHEMA_EXIT%
)

echo [AORebirth Restart] Stopping engines...
call "%~dp0stop-engines.cmd"
set STOP_EXIT=%ERRORLEVEL%
if not "%STOP_EXIT%"=="0" (
    echo [AORebirth Restart] Stop failed with exit code %STOP_EXIT%.
    popd >nul
    exit /b %STOP_EXIT%
)

echo [AORebirth Restart] Building freshly stopped active checkout engines...
call "%~dp0NewZoneEngineBuild\build.cmd"
set BUILD_EXIT=%ERRORLEVEL%
if not "%BUILD_EXIT%"=="0" (
    echo [AORebirth Restart] Build failed with exit code %BUILD_EXIT%; no engine was started.
    popd >nul
    exit /b %BUILD_EXIT%
)

echo [AORebirth Restart] Starting engines...
call "%~dp0start-engines.cmd" %*
set START_EXIT=%ERRORLEVEL%
if not "%START_EXIT%"=="0" (
    echo [AORebirth Restart] Start failed with exit code %START_EXIT%.
    popd >nul
    exit /b %START_EXIT%
)

echo [AORebirth Restart] Restart complete.
popd >nul
exit /b 0
