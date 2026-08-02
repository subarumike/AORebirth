@echo off
setlocal EnableExtensions

pushd "%~dp0" >nul
if errorlevel 1 (
    echo [AORebirth Web Start] Failed to switch to repository root.
    exit /b 1
)

call "%~dp0preflight-database.cmd"
set "PREFLIGHT_EXIT=%ERRORLEVEL%"
if not "%PREFLIGHT_EXIT%"=="0" (
    echo [AORebirth Web Start] Database preflight failed with exit code %PREFLIGHT_EXIT%; WebEngine was not started.
    popd >nul
    exit /b %PREFLIGHT_EXIT%
)

if not exist "%~dp0AORebirth\Built\Debug\WebEngine.exe" (
    echo [AORebirth Web Start] WebEngine.exe is missing; run tools\build_aorebirth_debug.cmd first.
    popd >nul
    exit /b 1
)

pushd "%~dp0AORebirth\Built\Debug" >nul
WebEngine.exe /validate-php-runtime
set "PHP_EXIT=%ERRORLEVEL%"
popd >nul
if not "%PHP_EXIT%"=="0" (
    echo [AORebirth Web Start] Local PHP runtime validation failed; WebEngine was not started.
    popd >nul
    exit /b %PHP_EXIT%
)

pushd "%~dp0AORebirth\Built\Debug" >nul
WebEngine.exe /validate-webcore-assets
set "WEBCORE_EXIT=%ERRORLEVEL%"
popd >nul
if not "%WEBCORE_EXIT%"=="0" (
    echo [AORebirth Web Start] Local WebCore asset validation failed; WebEngine was not started.
    popd >nul
    exit /b %WEBCORE_EXIT%
)

powershell -NoProfile -File "%~dp0start-engines.ps1" -WebOnly
set "START_EXIT=%ERRORLEVEL%"
popd >nul
exit /b %START_EXIT%
