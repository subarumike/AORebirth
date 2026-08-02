@echo off
setlocal EnableExtensions DisableDelayedExpansion

if not exist "%~dp0AORebirth\Built\Debug\WebEngine.exe" (
    echo [AORebirth WebCore Validate] WebEngine.exe is missing; run tools\build_aorebirth_debug.cmd first.
    exit /b 1
)

pushd "%~dp0AORebirth\Built\Debug" >nul
if errorlevel 1 (
    echo [AORebirth WebCore Validate] Failed to enter the WebEngine directory.
    exit /b 1
)

WebEngine.exe /validate-webcore-assets
set "VALIDATE_EXIT=%ERRORLEVEL%"
popd >nul
exit /b %VALIDATE_EXIT%
