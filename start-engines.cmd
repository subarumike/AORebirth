@echo off
setlocal EnableExtensions

pushd "%~dp0" >nul
if errorlevel 1 (
    echo [AORebirth Start] Failed to switch to repository root.
    exit /b 1
)

call "%~dp0preflight-database.cmd"
set "PREFLIGHT_EXIT=%ERRORLEVEL%"
if not "%PREFLIGHT_EXIT%"=="0" (
    echo [AORebirth Start] Database preflight failed with exit code %PREFLIGHT_EXIT%; no engine was started.
    popd >nul
    exit /b %PREFLIGHT_EXIT%
)

powershell -NoProfile -File "%~dp0start-engines.ps1"
set "START_EXIT=%ERRORLEVEL%"
popd >nul
exit /b %START_EXIT%
