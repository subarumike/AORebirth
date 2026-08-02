@echo off
setlocal EnableExtensions DisableDelayedExpansion

set "STATUS_PROBE=%~dp0Tools\engine_status_probe.js"
set "STATUS_CONFIG=%~dp0AORebirth\Config\Config.xml"
set "STATUS_ENGINE_DIR=%~dp0AORebirth\Built\Debug"
set "CSCRIPT_EXE=%SystemRoot%\System32\cscript.exe"

if not exist "%CSCRIPT_EXE%" (
    echo [AORebirth Status] FAIL - Windows Script Host is unavailable.
    exit /b 2
)

if not exist "%STATUS_PROBE%" (
    echo [AORebirth Status] FAIL - engine ownership probe is missing.
    exit /b 2
)

if not exist "%STATUS_CONFIG%" (
    echo [AORebirth Status] FAIL - repository configuration is missing.
    exit /b 2
)

"%CSCRIPT_EXE%" //nologo "%STATUS_PROBE%" --config "%STATUS_CONFIG%" --engine-dir "%STATUS_ENGINE_DIR%" %*
set "STATUS_EXIT=%ERRORLEVEL%"
exit /b %STATUS_EXIT%
