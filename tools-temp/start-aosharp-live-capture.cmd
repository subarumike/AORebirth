@echo off
setlocal EnableExtensions

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..") do set "REPO_ROOT=%%~fI"

set "INJECTOR_EXE=%REPO_ROOT%\tools-temp\AOSharpLiveInjector\bin\Debug\AOSharpLiveInjector.exe"
set "PLUGIN_DLL=%REPO_ROOT%\tools-temp\AOSharpLiveCapture\bin\Debug\AOSharpLiveCapture.dll"
set "CAPTURE_ROOT=%REPO_ROOT%\tools-temp\AOSharpLiveCapture\bin\Debug\captures"
set "LOG_PATH=%REPO_ROOT%\tools-temp\AOSharpLiveInjector\bin\Debug\AOSharpLiveInjector-start.log"
set "TARGET_SWITCH="
set "TARGET_VALUE="

if "%~1"=="" goto usage

:parse
if "%~1"=="" goto parsed
if /I "%~1"=="--help" goto help
if /I "%~1"=="-h" goto help
if /I "%~1"=="--pid" (
    if defined TARGET_SWITCH goto one_target
    if "%~2"=="" goto usage
    set "TARGET_SWITCH=--pid"
    set "TARGET_VALUE=%~2"
    shift
    shift
    goto parse
)
if /I "%~1"=="--title" (
    if defined TARGET_SWITCH goto one_target
    if "%~2"=="" goto usage
    set "TARGET_SWITCH=--title"
    set "TARGET_VALUE=%~2"
    shift
    shift
    goto parse
)
echo FAILED: unknown argument %~1
goto usage

:one_target
echo FAILED: pass only one target selector: --title or --pid.
exit /b 2

:parsed
if not defined TARGET_SWITCH goto usage

if not exist "%INJECTOR_EXE%" (
    echo FAILED: AOSharpLiveInjector not found: "%INJECTOR_EXE%"
    exit /b 1
)

if not exist "%PLUGIN_DLL%" (
    echo FAILED: AOSharpLiveCapture plugin not found: "%PLUGIN_DLL%"
    exit /b 1
)

if not exist "%CAPTURE_ROOT%" mkdir "%CAPTURE_ROOT%" >nul 2>nul
if not exist "%CAPTURE_ROOT%" (
    echo FAILED: capture output root is not available: "%CAPTURE_ROOT%"
    exit /b 1
)

for /f "delims=" %%D in ('dir /b /ad /o-d "%CAPTURE_ROOT%" 2^>nul') do (
    if not defined PREVIOUS_CAPTURE set "PREVIOUS_CAPTURE=%%D"
)

if exist "%LOG_PATH%" del /q "%LOG_PATH%" >nul 2>nul

echo Command: "%INJECTOR_EXE%" --plugin "%PLUGIN_DLL%" --log "%LOG_PATH%" %TARGET_SWITCH% "%TARGET_VALUE%"

start "AOSharpLiveInjector" /min "%INJECTOR_EXE%" --plugin "%PLUGIN_DLL%" --log "%LOG_PATH%" %TARGET_SWITCH% "%TARGET_VALUE%"
timeout /t 3 /nobreak >nul

if not exist "%LOG_PATH%" (
    echo FAILED: injector did not create the expected log.
    echo FailureLog: "%LOG_PATH%"
    exit /b 1
)

findstr /C:"Capture plugin injected. Keeping pipe open." "%LOG_PATH%" >nul
if errorlevel 1 (
    echo FAILED: AOSharp live capture did not report injection success.
    echo FailureLog: "%LOG_PATH%"
    findstr /C:"ERROR:" "%LOG_PATH%" 2>nul
    exit /b 1
)

for /f "delims=" %%D in ('dir /b /ad /o-d "%CAPTURE_ROOT%" 2^>nul') do (
    if not defined LATEST_CAPTURE set "LATEST_CAPTURE=%%D"
)

if defined LATEST_CAPTURE if /I not "%LATEST_CAPTURE%"=="%PREVIOUS_CAPTURE%" (
    echo SUCCESS: AOSharp live capture injected.
    echo CaptureOutputPath: "%CAPTURE_ROOT%\%LATEST_CAPTURE%"
    echo FailureLog: "%LOG_PATH%"
    exit /b 0
)

echo SUCCESS: AOSharp live capture injected.
echo CaptureOutputPath: "%CAPTURE_ROOT%"
echo FailureLog: "%LOG_PATH%"
exit /b 0

:usage
echo Usage: cmd /d /c tools-temp\start-aosharp-live-capture.cmd --title "Anarchy Online"
echo    or: cmd /d /c tools-temp\start-aosharp-live-capture.cmd --pid 1234
echo This wrapper attaches to an already-running AO client. It does not launch the game/client.
exit /b 2

:help
echo Usage: cmd /d /c tools-temp\start-aosharp-live-capture.cmd --title "Anarchy Online"
echo    or: cmd /d /c tools-temp\start-aosharp-live-capture.cmd --pid 1234
echo This wrapper attaches to an already-running AO client. It does not launch the game/client.
exit /b 0
