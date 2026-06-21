@echo off
setlocal EnableExtensions

pushd "%~dp0" >nul
if errorlevel 1 (
    echo [AORebirth Restart] Failed to switch to repository root.
    exit /b 1
)

echo [AORebirth Restart] Stopping engines...
call "%~dp0stop-engines.cmd"
set STOP_EXIT=%ERRORLEVEL%
if not "%STOP_EXIT%"=="0" (
    echo [AORebirth Restart] Stop failed with exit code %STOP_EXIT%.
    popd >nul
    exit /b %STOP_EXIT%
)

echo [AORebirth Restart] Starting engines...
call "%~dp0start-engines.cmd"
set START_EXIT=%ERRORLEVEL%
if not "%START_EXIT%"=="0" (
    echo [AORebirth Restart] Start failed with exit code %START_EXIT%.
    popd >nul
    exit /b %START_EXIT%
)

echo [AORebirth Restart] Restart complete.
popd >nul
exit /b 0
