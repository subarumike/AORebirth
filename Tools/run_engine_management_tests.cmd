@echo off
setlocal EnableExtensions DisableDelayedExpansion

pushd "%~dp0.." >nul
if errorlevel 1 (
    echo [Engine Management Tests] FAIL: cannot enter repository root.
    exit /b 1
)

call Tools\tests\test_engine_status_probe.cmd
if errorlevel 1 goto Fail

call Tools\run_database_preflight_tests.cmd
if errorlevel 1 goto Fail

set "AO_REBIRTH_MYSQL_CONNECTION="
call start-engines.cmd >nul
set "START_MISSING_EXIT=%ERRORLEVEL%"
if not "%START_MISSING_EXIT%"=="10" (
    echo [Engine Management Tests] FAIL: start missing-credential guard returned %START_MISSING_EXIT% instead of 10.
    goto Fail
)

call restart-engines.cmd >nul
set "RESTART_MISSING_EXIT=%ERRORLEVEL%"
if not "%RESTART_MISSING_EXIT%"=="10" (
    echo [Engine Management Tests] FAIL: restart missing-credential guard returned %RESTART_MISSING_EXIT% instead of 10.
    goto Fail
)

call start-web-engine.cmd >nul
set "WEB_MISSING_EXIT=%ERRORLEVEL%"
if not "%WEB_MISSING_EXIT%"=="10" (
    echo [Engine Management Tests] FAIL: Web start missing-credential guard returned %WEB_MISSING_EXIT% instead of 10.
    goto Fail
)

if /i not "%~1"=="--skip-web-engine-security" (
    call Tools\run_web_engine_security_tests.cmd
    if errorlevel 1 goto Fail
)

python Tools\tests\test_engine_management_contracts.py
if errorlevel 1 goto Fail

echo [Engine Management Tests] PASS.
popd >nul
exit /b 0

:Fail
echo [Engine Management Tests] FAIL.
popd >nul
exit /b 1
