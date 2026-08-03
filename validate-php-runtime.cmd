@echo off
setlocal

if not exist "%~dp0AORebirth\Built\Debug\WebEngine.exe" (
    echo [AORebirth PHP Validate] WebEngine.exe is missing; run tools\build_aorebirth_debug.cmd first.
    exit /b 2
)

pushd "%~dp0AORebirth\Built\Debug" >nul
if errorlevel 1 (
    echo [AORebirth PHP Validate] Failed to enter the WebEngine directory.
    exit /b 2
)

WebEngine.exe /validate-php-runtime
set "VALIDATE_EXIT=%ERRORLEVEL%"
popd >nul
exit /b %VALIDATE_EXIT%
