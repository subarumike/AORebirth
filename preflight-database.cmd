@echo off
setlocal EnableExtensions DisableDelayedExpansion

set "ROOT=%~dp0"
set "PREFLIGHT_DIR=%ROOT%AORebirth\Built\Debug\DatabasePreflight"
set "PREFLIGHT_EXE=%PREFLIGHT_DIR%\DatabasePreflight.exe"

if not exist "%PREFLIGHT_EXE%" (
    echo [Database Preflight] FAIL ^(18^): DatabasePreflight.exe is missing; run NewZoneEngineBuild\build.cmd first.
    exit /b 18
)

if "%~1"=="" goto RunPreflight
if /I "%~1"=="--self-test" if "%~2"=="" goto RunSelfTest

echo [Database Preflight] FAIL ^(18^): unsupported command arguments.
exit /b 18

:RunPreflight
pushd "%PREFLIGHT_DIR%" >nul
"%PREFLIGHT_EXE%"
set "PREFLIGHT_EXIT=%ERRORLEVEL%"
popd >nul
exit /b %PREFLIGHT_EXIT%

:RunSelfTest
pushd "%PREFLIGHT_DIR%" >nul
"%PREFLIGHT_EXE%" --self-test
set "PREFLIGHT_EXIT=%ERRORLEVEL%"
popd >nul
exit /b %PREFLIGHT_EXIT%
