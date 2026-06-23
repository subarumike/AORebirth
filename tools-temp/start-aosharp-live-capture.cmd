@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..") do set "REPO_ROOT=%%~fI"

set "INJECTOR_EXE=%REPO_ROOT%\tools-temp\AOSharpLiveInjector\bin\Debug\AOSharpLiveInjector.exe"
set "PLUGIN_DLL=%REPO_ROOT%\tools-temp\AOSharpLiveCapture\bin\Debug\AOSharpLiveCapture.dll"
set "CAPTURE_ROOT=%REPO_ROOT%\tools-temp\AOSharpLiveCapture\bin\Debug\captures"
set "LOG_PATH=%REPO_ROOT%\tools-temp\AOSharpLiveInjector\bin\Debug\AOSharpLiveInjector-start.log"
set "TARGET_SWITCH="
set "TARGET_VALUE="
set "LAUNCHER_VBS=%TEMP%\start-aosharp-live-capture-%RANDOM%%RANDOM%.vbs"

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

if defined PREVIOUS_CAPTURE (
    set "ACTIVE_CAPTURE_PATH=%CAPTURE_ROOT%\!PREVIOUS_CAPTURE!"
    set "ACTIVE_CAPTURE_HAS_PACKET_FILE="
    set "ACTIVE_CAPTURE_HAS_EVENT_FILE="
    set "ACTIVE_CAPTURE_IS_RUNNING="
    set "ACTIVE_CAPTURE_PID="
    set "ACTIVE_CAPTURE_PROCESS_RUNNING="
    if exist "!ACTIVE_CAPTURE_PATH!\packets.hex.log" (
        for %%F in ("!ACTIVE_CAPTURE_PATH!\packets.hex.log") do if %%~zF GTR 0 set "ACTIVE_CAPTURE_HAS_PACKET_FILE=1"
    )
    if exist "!ACTIVE_CAPTURE_PATH!\events.log" (
        for %%F in ("!ACTIVE_CAPTURE_PATH!\events.log") do if %%~zF GTR 0 set "ACTIVE_CAPTURE_HAS_EVENT_FILE=1"
    )
    if exist "!ACTIVE_CAPTURE_PATH!\capture_info.json" (
        findstr /C:"running" "!ACTIVE_CAPTURE_PATH!\capture_info.json" >nul 2>nul
        if not errorlevel 1 set "ACTIVE_CAPTURE_IS_RUNNING=1"
    )
    if exist "!ACTIVE_CAPTURE_PATH!\capture-session.json" (
        for /f "tokens=2 delims=:" %%P in ('findstr /C:"id" "!ACTIVE_CAPTURE_PATH!\capture-session.json" 2^>nul') do if not defined ACTIVE_CAPTURE_PID (
            set "ACTIVE_CAPTURE_PID=%%P"
            set "ACTIVE_CAPTURE_PID=!ACTIVE_CAPTURE_PID:,=!"
            set "ACTIVE_CAPTURE_PID=!ACTIVE_CAPTURE_PID: =!"
        )
    )
    if defined ACTIVE_CAPTURE_PID (
        tasklist /FI "PID eq !ACTIVE_CAPTURE_PID!" /FI "IMAGENAME eq AnarchyOnline.exe" 2>nul | findstr /I /C:"AnarchyOnline.exe" >nul 2>nul
        if not errorlevel 1 set "ACTIVE_CAPTURE_PROCESS_RUNNING=1"
    )
    if defined ACTIVE_CAPTURE_HAS_PACKET_FILE if defined ACTIVE_CAPTURE_HAS_EVENT_FILE if defined ACTIVE_CAPTURE_IS_RUNNING (
        echo SUCCESS: AOSharp live capture already active.
        echo CaptureOutputPath: "!ACTIVE_CAPTURE_PATH!"
        echo FailureLog: "%LOG_PATH%"
        exit /b 0
    )
    if defined ACTIVE_CAPTURE_PROCESS_RUNNING (
        echo SUCCESS: AOSharp live capture already loaded in PID !ACTIVE_CAPTURE_PID!.
        echo CaptureOutputPath: "!ACTIVE_CAPTURE_PATH!"
        echo FailureLog: "%LOG_PATH%"
        exit /b 0
    )
)

if exist "%LOG_PATH%" del /q "%LOG_PATH%" >nul 2>nul

echo Command: "%INJECTOR_EXE%" --plugin "%PLUGIN_DLL%" --log "%LOG_PATH%" %TARGET_SWITCH% "%TARGET_VALUE%"

> "%LAUNCHER_VBS%" echo Set shell = CreateObject("WScript.Shell"^)
>> "%LAUNCHER_VBS%" echo command = Chr(34^) ^& WScript.Arguments(0^) ^& Chr(34^) ^& " --plugin " ^& Chr(34^) ^& WScript.Arguments(1^) ^& Chr(34^) ^& " --log " ^& Chr(34^) ^& WScript.Arguments(2^) ^& Chr(34^) ^& " " ^& WScript.Arguments(3^) ^& " " ^& Chr(34^) ^& WScript.Arguments(4^) ^& Chr(34^)
>> "%LAUNCHER_VBS%" echo WScript.Quit shell.Run(command, 2, False^)

wscript.exe "%LAUNCHER_VBS%" "%INJECTOR_EXE%" "%PLUGIN_DLL%" "%LOG_PATH%" "%TARGET_SWITCH%" "%TARGET_VALUE%" >nul 2>nul
set "LAUNCH_EXIT=%ERRORLEVEL%"
del /q "%LAUNCHER_VBS%" >nul 2>nul

if not "%LAUNCH_EXIT%"=="0" (
    echo FAILED: injector launch helper failed.
    echo FailureLog: "%LOG_PATH%"
    exit /b 1
)

timeout /t 3 /nobreak >nul

for /f "delims=" %%D in ('dir /b /ad /o-d "%CAPTURE_ROOT%" 2^>nul') do (
    if not defined LATEST_CAPTURE set "LATEST_CAPTURE=%%D"
)

if defined LATEST_CAPTURE (
    set "LATEST_CAPTURE_PATH=%CAPTURE_ROOT%\%LATEST_CAPTURE%"
    if exist "!LATEST_CAPTURE_PATH!\packets.hex.log" (
        for %%F in ("!LATEST_CAPTURE_PATH!\packets.hex.log") do if %%~zF GTR 0 set "CAPTURE_HAS_PACKET_FILE=1"
    )
    if exist "!LATEST_CAPTURE_PATH!\events.log" (
        for %%F in ("!LATEST_CAPTURE_PATH!\events.log") do if %%~zF GTR 0 set "CAPTURE_HAS_EVENT_FILE=1"
    )
    if defined CAPTURE_HAS_PACKET_FILE if defined CAPTURE_HAS_EVENT_FILE if /I not "!LATEST_CAPTURE!"=="!PREVIOUS_CAPTURE!" (
        echo SUCCESS: AOSharp live capture injected.
        echo CaptureOutputPath: "!LATEST_CAPTURE_PATH!"
        echo FailureLog: "%LOG_PATH%"
        exit /b 0
    )
)

if not exist "%LOG_PATH%" (
    echo FAILED: injector did not create the expected log.
    echo FailureLog: "%LOG_PATH%"
    exit /b 1
)

findstr /C:"Capture plugin injected. Keeping pipe open." "%LOG_PATH%" >nul
if not errorlevel 1 if defined LATEST_CAPTURE if /I not "%LATEST_CAPTURE%"=="%PREVIOUS_CAPTURE%" (
    echo SUCCESS: AOSharp live capture injected.
    echo CaptureOutputPath: "%CAPTURE_ROOT%\%LATEST_CAPTURE%"
    echo FailureLog: "%LOG_PATH%"
    exit /b 0
)

if not errorlevel 1 (
    echo FAILED: injector reported plugin load, but no new capture folder with packet output was created.
    echo CaptureOutputPath: "%CAPTURE_ROOT%"
    echo FailureLog: "%LOG_PATH%"
    exit /b 1
)

echo FAILED: AOSharp live capture did not report injection success or write capture packet files.
echo FailureLog: "%LOG_PATH%"
findstr /C:"ERROR:" "%LOG_PATH%" 2>nul
exit /b 1

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
