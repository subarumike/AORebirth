@echo off
setlocal EnableExtensions DisableDelayedExpansion

if "%~1"=="" goto Usage
if "%~2"=="" goto Usage

set "ARCHIVE_PATH=%~f1"
set "EXPECTED_VERSION=%~2"
set "COMPATIBILITY_TOOL=%~dp0Tools\webcore_php_compatibility.py"

for %%I in (python.exe) do set "PYTHON_EXE=%%~$PATH:I"
if not defined PYTHON_EXE (
    echo [AORebirth WebCore Import] python.exe is unavailable; import was not attempted.
    exit /b 5
)

if not exist "%COMPATIBILITY_TOOL%" (
    echo [AORebirth WebCore Import] The approved compatibility tool is missing; import was not attempted.
    exit /b 5
)

call "%~dp0status-engines.cmd" --prestart WebEngine >nul
set "PRESTART_EXIT=%ERRORLEVEL%"
if not "%PRESTART_EXIT%"=="0" (
    echo [AORebirth WebCore Import] WebEngine must be fully stopped with no conflicting process or listener; import was not attempted.
    exit /b %PRESTART_EXIT%
)

if not exist "%~dp0AORebirth\Built\Debug\WebEngine.exe" (
    echo [AORebirth WebCore Import] WebEngine.exe is missing; run tools\build_aorebirth_debug.cmd first.
    exit /b 1
)

pushd "%~dp0AORebirth\Built\Debug" >nul
if errorlevel 1 (
    echo [AORebirth WebCore Import] Failed to enter the WebEngine directory.
    exit /b 1
)

WebEngine.exe /import-webcore-assets "%ARCHIVE_PATH%" "%EXPECTED_VERSION%" "%PYTHON_EXE%" "%COMPATIBILITY_TOOL%"
set "IMPORT_EXIT=%ERRORLEVEL%"
popd >nul
exit /b %IMPORT_EXIT%

:Usage
echo Usage: import-webcore-assets.cmd ^<local-zip^> ^<exact-version^>
exit /b 2
