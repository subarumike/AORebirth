@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..") do set "REPO_ROOT=%%~fI"

set "INJECTOR_EXE=%REPO_ROOT%\tools-temp\AOSharpLiveInjector\bin\Debug\AOSharpLiveInjector.exe"
set "PLUGIN_DLL=%REPO_ROOT%\tools-temp\AOSharpLiveCapture\bin\Debug\AOSharpLiveCapture.dll"
set "CAPTURE_ROOT=%REPO_ROOT%\tools-temp\AOSharpLiveCapture\bin\Debug\captures"
set "LOOT_CAPTURE_REQUEST=%REPO_ROOT%\tools-temp\AOSharpLiveCapture\bin\Debug\loot-10.request"
set "PF127_GEOMETRY_ONLY_REQUEST=%REPO_ROOT%\tools-temp\AOSharpLiveCapture\bin\Debug\pf127-geometry-only.request"
set "EXTERNAL_CONTROL_REQUEST=%REPO_ROOT%\tools-temp\AOSharpLiveCapture\bin\Debug\AOSharpLiveCapture.control"
set "EXTERNAL_CONTROL_PROCESSING=%EXTERNAL_CONTROL_REQUEST%.processing"
set "LOG_PATH=%REPO_ROOT%\tools-temp\AOSharpLiveInjector\bin\Debug\AOSharpLiveInjector-start.log"
set "TARGET_SWITCH="
set "TARGET_VALUE="
set "LOOT_CAPTURE_MODE="
set "PF127_GEOMETRY_ONLY_MODE="
set "LAUNCHER_VBS=%TEMP%\start-aosharp-live-capture-%RANDOM%%RANDOM%.vbs"

if "%~1"=="" goto usage

:parse
if "%~1"=="" goto parsed
if /I "%~1"=="--help" goto help
if /I "%~1"=="-h" goto help
if /I "%~1"=="--loot-10" (
    set "LOOT_CAPTURE_MODE=1"
    shift
    goto parse
)
if /I "%~1"=="--pf127-geometry-only" (
    set "PF127_GEOMETRY_ONLY_MODE=1"
    shift
    goto parse
)
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
if defined LOOT_CAPTURE_MODE if defined PF127_GEOMETRY_ONLY_MODE (
    echo FAILED: --loot-10 and --pf127-geometry-only are mutually exclusive.
    exit /b 2
)

if not exist "%INJECTOR_EXE%" (
    echo FAILED: AOSharpLiveInjector not found: "%INJECTOR_EXE%"
    exit /b 1
)

if not exist "%PLUGIN_DLL%" (
    echo FAILED: AOSharpLiveCapture plugin not found: "%PLUGIN_DLL%"
    exit /b 1
)

set "SELF_TEST_PLUGIN=%PLUGIN_DLL%.self-test-must-not-exist"
set "SELF_TEST_OUTPUT=%TEMP%\aosharp-live-injector-self-test-%RANDOM%%RANDOM%.txt"
if exist "%SELF_TEST_PLUGIN%" (
    echo FAILED: injector self-test sentinel unexpectedly exists: "%SELF_TEST_PLUGIN%"
    exit /b 1
)

"%INJECTOR_EXE%" --self-test --plugin "%SELF_TEST_PLUGIN%" > "%SELF_TEST_OUTPUT%" 2>&1
set "SELF_TEST_EXIT=!ERRORLEVEL!"
findstr /X /C:"PASS: capture-safe bootstrap provides fail-closed isolated capture chat commands without native GUI rewriting." "%SELF_TEST_OUTPUT%" >nul 2>nul
set "SELF_TEST_MATCH=!ERRORLEVEL!"
del /q "%SELF_TEST_OUTPUT%" >nul 2>nul
if not "!SELF_TEST_EXIT!"=="0" goto unsafe_injector
if not "!SELF_TEST_MATCH!"=="0" goto unsafe_injector

if not exist "%CAPTURE_ROOT%" mkdir "%CAPTURE_ROOT%" >nul 2>nul
if not exist "%CAPTURE_ROOT%" (
    echo FAILED: capture output root is not available: "%CAPTURE_ROOT%"
    exit /b 1
)

call :cleanup_pf127_geometry_request
if errorlevel 1 exit /b 1

for /f "delims=" %%D in ('dir /b /ad /o-d "%CAPTURE_ROOT%" 2^>nul') do (
    if not defined PREVIOUS_CAPTURE set "PREVIOUS_CAPTURE=%%D"
)

if defined PREVIOUS_CAPTURE (
    set "ACTIVE_CAPTURE_PATH=%CAPTURE_ROOT%\!PREVIOUS_CAPTURE!"
    set "PRE_SAFE_SIZE="
    set "POST_SAFE_SIZE="
    if exist "!ACTIVE_CAPTURE_PATH!\pf127-safe-mode.log" (
        for %%F in ("!ACTIVE_CAPTURE_PATH!\pf127-safe-mode.log") do set "PRE_SAFE_SIZE=%%~zF"
        ping -n 3 127.0.0.1 >nul
        for %%F in ("!ACTIVE_CAPTURE_PATH!\pf127-safe-mode.log") do set "POST_SAFE_SIZE=%%~zF"
    )
    if defined PRE_SAFE_SIZE if defined POST_SAFE_SIZE if !POST_SAFE_SIZE! GTR !PRE_SAFE_SIZE! (
        if not defined PF127_GEOMETRY_ONLY_MODE (
            echo FAILED: PF127 geometry-only safe capture is active; a different mode requires a fresh client injection.
            exit /b 1
        )
        echo SUCCESS: PF127 geometry-only safe capture already active.
        echo CaptureOutputPath: "!ACTIVE_CAPTURE_PATH!"
        echo FailureLog: "%LOG_PATH%"
        exit /b 0
    )
    set "PRE_PACKET_SIZE="
    set "PRE_EVENT_SIZE="
    set "POST_PACKET_SIZE="
    set "POST_EVENT_SIZE="
    if exist "!ACTIVE_CAPTURE_PATH!\packets.hex.log" if exist "!ACTIVE_CAPTURE_PATH!\events.log" (
        for %%F in ("!ACTIVE_CAPTURE_PATH!\packets.hex.log") do set "PRE_PACKET_SIZE=%%~zF"
        for %%F in ("!ACTIVE_CAPTURE_PATH!\events.log") do set "PRE_EVENT_SIZE=%%~zF"
        ping -n 3 127.0.0.1 >nul
        for %%F in ("!ACTIVE_CAPTURE_PATH!\packets.hex.log") do set "POST_PACKET_SIZE=%%~zF"
        for %%F in ("!ACTIVE_CAPTURE_PATH!\events.log") do set "POST_EVENT_SIZE=%%~zF"
    )
    if defined PRE_PACKET_SIZE if defined PRE_EVENT_SIZE if defined POST_PACKET_SIZE if defined POST_EVENT_SIZE (
        if !POST_PACKET_SIZE! GTR !PRE_PACKET_SIZE! if !POST_EVENT_SIZE! GEQ !PRE_EVENT_SIZE! (
            if defined LOOT_CAPTURE_MODE (
                echo FAILED: --loot-10 requires a fresh capture injection; an existing capture is active.
                exit /b 1
            )
            if defined PF127_GEOMETRY_ONLY_MODE (
                echo FAILED: --pf127-geometry-only requires a fresh capture injection; the comprehensive capture is active.
                exit /b 1
            )
            echo SUCCESS: AOSharp live capture already active.
            echo CaptureOutputPath: "!ACTIVE_CAPTURE_PATH!"
            echo FailureLog: "%LOG_PATH%"
            exit /b 0
        )
        if !POST_EVENT_SIZE! GTR !PRE_EVENT_SIZE! if !POST_PACKET_SIZE! GEQ !PRE_PACKET_SIZE! (
            if defined LOOT_CAPTURE_MODE (
                echo FAILED: --loot-10 requires a fresh capture injection; an existing capture is active.
                exit /b 1
            )
            if defined PF127_GEOMETRY_ONLY_MODE (
                echo FAILED: --pf127-geometry-only requires a fresh capture injection; the comprehensive capture is active.
                exit /b 1
            )
            echo SUCCESS: AOSharp live capture already active.
            echo CaptureOutputPath: "!ACTIVE_CAPTURE_PATH!"
            echo FailureLog: "%LOG_PATH%"
            exit /b 0
        )
    )
)

call :cleanup_external_control
if errorlevel 1 exit /b 1

if not defined LOOT_CAPTURE_MODE if exist "%LOOT_CAPTURE_REQUEST%" del /q "%LOOT_CAPTURE_REQUEST%" >nul 2>nul
if defined LOOT_CAPTURE_MODE (
    > "%LOOT_CAPTURE_REQUEST%" echo loot-10
    if not exist "%LOOT_CAPTURE_REQUEST%" (
        echo FAILED: could not arm the ten-kill loot capture request: "%LOOT_CAPTURE_REQUEST%"
        exit /b 1
    )
)

if defined PF127_GEOMETRY_ONLY_MODE (
    > "%PF127_GEOMETRY_ONLY_REQUEST%" echo pf127-geometry-only
    if not exist "%PF127_GEOMETRY_ONLY_REQUEST%" (
        echo FAILED: could not arm the PF127 geometry-only safe capture request: "%PF127_GEOMETRY_ONLY_REQUEST%"
        exit /b 1
    )
    set "PF127_GEOMETRY_REQUEST_ARMED=1"
)

set "POST_ARM_FAILURE_LOG=%LOG_PATH%"

if exist "%LOG_PATH%" del /q "%LOG_PATH%" >nul 2>nul

echo Command: "%INJECTOR_EXE%" --plugin "%PLUGIN_DLL%" --log "%LOG_PATH%" %TARGET_SWITCH% "%TARGET_VALUE%"

> "%LAUNCHER_VBS%" echo Set shell = CreateObject("WScript.Shell"^)
>> "%LAUNCHER_VBS%" echo command = Chr(34^) ^& WScript.Arguments(0^) ^& Chr(34^) ^& " --plugin " ^& Chr(34^) ^& WScript.Arguments(1^) ^& Chr(34^) ^& " --log " ^& Chr(34^) ^& WScript.Arguments(2^) ^& Chr(34^) ^& " " ^& WScript.Arguments(3^) ^& " " ^& Chr(34^) ^& WScript.Arguments(4^) ^& Chr(34^)
if defined PF127_GEOMETRY_ONLY_MODE (
    >> "%LAUNCHER_VBS%" echo WScript.Quit shell.Run(command, 2, True^)
) else (
    >> "%LAUNCHER_VBS%" echo WScript.Quit shell.Run(command, 2, False^)
)

wscript.exe "%LAUNCHER_VBS%" "%INJECTOR_EXE%" "%PLUGIN_DLL%" "%LOG_PATH%" "%TARGET_SWITCH%" "%TARGET_VALUE%" >nul 2>nul
set "LAUNCH_EXIT=%ERRORLEVEL%"
del /q "%LAUNCHER_VBS%" >nul 2>nul

if not "%LAUNCH_EXIT%"=="0" (
    set "POST_ARM_EXIT_CODE=1"
    set "POST_ARM_SUMMARY=FAILED: injector launch helper failed."
    goto post_arm_exit
)

ping -n 4 127.0.0.1 >nul

for /f "delims=" %%D in ('dir /b /ad /o-d "%CAPTURE_ROOT%" 2^>nul') do (
    if not defined LATEST_CAPTURE set "LATEST_CAPTURE=%%D"
)

if defined LATEST_CAPTURE (
    set "LATEST_CAPTURE_PATH=%CAPTURE_ROOT%\%LATEST_CAPTURE%"
    if defined PF127_GEOMETRY_ONLY_MODE if /I not "!LATEST_CAPTURE!"=="!PREVIOUS_CAPTURE!" if exist "!LATEST_CAPTURE_PATH!\pf127-safe-mode.log" (
        for %%F in ("!LATEST_CAPTURE_PATH!\pf127-safe-mode.log") do if %%~zF GTR 0 set "CAPTURE_HAS_SAFE_MODE_LOG=1"
    )
    if defined CAPTURE_HAS_SAFE_MODE_LOG (
        set "POST_ARM_EXIT_CODE=0"
        set "POST_ARM_SUMMARY=SUCCESS: PF127 geometry-only safe capture injected."
        set "POST_ARM_CAPTURE_PATH=!LATEST_CAPTURE_PATH!"
        goto post_arm_exit
    )
    if exist "!LATEST_CAPTURE_PATH!\packets.hex.log" (
        for %%F in ("!LATEST_CAPTURE_PATH!\packets.hex.log") do if %%~zF GTR 0 set "CAPTURE_HAS_PACKET_FILE=1"
    )
    if exist "!LATEST_CAPTURE_PATH!\events.log" (
        for %%F in ("!LATEST_CAPTURE_PATH!\events.log") do if %%~zF GTR 0 set "CAPTURE_HAS_EVENT_FILE=1"
    )
    if not defined PF127_GEOMETRY_ONLY_MODE if defined CAPTURE_HAS_PACKET_FILE if defined CAPTURE_HAS_EVENT_FILE if /I not "!LATEST_CAPTURE!"=="!PREVIOUS_CAPTURE!" (
        set "POST_ARM_EXIT_CODE=0"
        set "POST_ARM_SUMMARY=SUCCESS: AOSharp live capture injected."
        set "POST_ARM_CAPTURE_PATH=!LATEST_CAPTURE_PATH!"
        goto post_arm_exit
    )
)

if defined PF127_GEOMETRY_ONLY_MODE (
    set "POST_ARM_EXIT_CODE=1"
    set "POST_ARM_SUMMARY=FAILED: injector did not create a new PF127 geometry-only safe capture folder; the request was not confirmed consumed."
    set "POST_ARM_CAPTURE_PATH=%CAPTURE_ROOT%"
    goto post_arm_exit
)

if not exist "%LOG_PATH%" (
    set "POST_ARM_EXIT_CODE=1"
    set "POST_ARM_SUMMARY=FAILED: injector did not create the expected log."
    goto post_arm_exit
)

findstr /C:"Capture plugin injected." "%LOG_PATH%" >nul
if not errorlevel 1 if defined LATEST_CAPTURE if /I not "%LATEST_CAPTURE%"=="%PREVIOUS_CAPTURE%" (
    set "POST_ARM_EXIT_CODE=0"
    set "POST_ARM_SUMMARY=SUCCESS: AOSharp live capture injected."
    set "POST_ARM_CAPTURE_PATH=%CAPTURE_ROOT%\%LATEST_CAPTURE%"
    goto post_arm_exit
)

if not errorlevel 1 (
    set "POST_ARM_EXIT_CODE=1"
    set "POST_ARM_SUMMARY=FAILED: injector reported plugin load, but no new capture folder with packet output was created."
    set "POST_ARM_CAPTURE_PATH=%CAPTURE_ROOT%"
    goto post_arm_exit
)

set "POST_ARM_EXIT_CODE=1"
set "POST_ARM_SUMMARY=FAILED: AOSharp live capture did not report injection success or write capture packet files."
set "POST_ARM_SHOW_ERRORS=1"
goto post_arm_exit

:post_arm_exit
call :cleanup_pf127_geometry_request
if errorlevel 1 exit /b 1
if defined POST_ARM_SUMMARY echo %POST_ARM_SUMMARY%
if defined POST_ARM_CAPTURE_PATH echo CaptureOutputPath: "%POST_ARM_CAPTURE_PATH%"
if defined POST_ARM_FAILURE_LOG echo FailureLog: "%POST_ARM_FAILURE_LOG%"
if defined POST_ARM_SHOW_ERRORS if exist "%POST_ARM_FAILURE_LOG%" findstr /C:"ERROR:" "%POST_ARM_FAILURE_LOG%" 2>nul
exit /b %POST_ARM_EXIT_CODE%

:unsafe_injector
if exist "%SELF_TEST_OUTPUT%" del /q "%SELF_TEST_OUTPUT%" >nul 2>nul
echo FAILED: capture-safe injector self-test failed; refusing to inject.
echo RebuildCommand: cmd /d /c tools-temp\build-aosharp-live-injector.cmd
exit /b 1

:usage
echo Usage: cmd /d /c tools-temp\start-aosharp-live-capture.cmd --title "Anarchy Online"
echo    or: cmd /d /c tools-temp\start-aosharp-live-capture.cmd --pid 1234
echo Add --loot-10 to arm one-enemy ten-corpse loot validation without an in-game command.
echo Add --pf127-geometry-only only after the character is already inside and stable in Subway; it disables comprehensive packet and dynel callbacks.
echo This wrapper attaches to an already-running AO client. It does not launch the game/client.
exit /b 2

:help
echo Usage: cmd /d /c tools-temp\start-aosharp-live-capture.cmd --title "Anarchy Online"
echo    or: cmd /d /c tools-temp\start-aosharp-live-capture.cmd --pid 1234
echo Add --loot-10 to arm one-enemy ten-corpse loot validation without an in-game command.
echo Add --pf127-geometry-only only after the character is already inside and stable in Subway; it disables comprehensive packet and dynel callbacks.
echo This wrapper attaches to an already-running AO client. It does not launch the game/client.
exit /b 0

:cleanup_pf127_geometry_request
if not exist "%PF127_GEOMETRY_ONLY_REQUEST%" exit /b 0
del /q "%PF127_GEOMETRY_ONLY_REQUEST%" >nul 2>nul
if exist "%PF127_GEOMETRY_ONLY_REQUEST%" (
    echo FAILED: could not remove the PF127 geometry-only request marker; refusing to continue because a stale marker could activate safe mode later.
    echo RequestMarker: "%PF127_GEOMETRY_ONLY_REQUEST%"
    exit /b 1
)
exit /b 0

:cleanup_external_control
if exist "%EXTERNAL_CONTROL_REQUEST%" del /q "%EXTERNAL_CONTROL_REQUEST%" >nul 2>nul
if exist "%EXTERNAL_CONTROL_PROCESSING%" del /q "%EXTERNAL_CONTROL_PROCESSING%" >nul 2>nul
for %%F in ("%EXTERNAL_CONTROL_REQUEST%.*.tmp") do if exist "%%~fF" del /q "%%~fF" >nul 2>nul
if exist "%EXTERNAL_CONTROL_REQUEST%" goto external_control_cleanup_failed
if exist "%EXTERNAL_CONTROL_PROCESSING%" goto external_control_cleanup_failed
exit /b 0

:external_control_cleanup_failed
echo FAILED: could not clear stale AOSharp capture-control files; refusing to inject.
echo RequestFile: "%EXTERNAL_CONTROL_REQUEST%"
exit /b 1
