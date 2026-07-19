@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
set "CONTROL_FILE=%SCRIPT_DIR%AOSharpLiveCapture\bin\Debug\AOSharpLiveCapture.control"
set "PROCESSING_FILE=%CONTROL_FILE%.processing"
set "PAYLOAD="

if "%~1"=="" goto usage
if /I "%~1"=="--help" goto help
if /I "%~1"=="-h" goto help

if /I "%~1"=="start" (
    if not "%~2"=="" goto usage
    set "PAYLOAD=start"
    goto write_request
)

if /I "%~1"=="stop" (
    if not "%~2"=="" goto usage
    set "PAYLOAD=stop"
    goto write_request
)

if /I "%~1"=="flush" (
    if not "%~2"=="" goto usage
    set "PAYLOAD=flush"
    goto write_request
)

if /I "%~1"=="snapshot" (
    if not "%~2"=="" goto usage
    set "PAYLOAD=snapshot"
    goto write_request
)

if /I "%~1"=="mark" goto collect_marker
goto usage

:collect_marker
shift
set "MARKER="

:collect_marker_loop
if "%~1"=="" goto marker_ready
if defined MARKER (
    set "MARKER=!MARKER! %~1"
) else (
    set "MARKER=%~1"
)
shift
goto collect_marker_loop

:marker_ready
if defined MARKER (
    set "PAYLOAD=mark !MARKER!"
) else (
    set "PAYLOAD=mark"
)

:write_request
if exist "%CONTROL_FILE%" (
    echo FAILED: an AOSharp capture-control request is already pending.
    echo RequestFile: "%CONTROL_FILE%"
    exit /b 1
)

if exist "%PROCESSING_FILE%" (
    echo FAILED: AOSharp is still processing the previous capture-control request.
    echo ProcessingFile: "%PROCESSING_FILE%"
    exit /b 1
)

set "TEMP_REQUEST=%CONTROL_FILE%.%RANDOM%%RANDOM%.tmp"
if exist "!TEMP_REQUEST!" (
    echo FAILED: temporary capture-control request already exists.
    exit /b 1
)

> "!TEMP_REQUEST!" echo(!PAYLOAD!
if not exist "!TEMP_REQUEST!" (
    echo FAILED: could not write the AOSharp capture-control request.
    exit /b 1
)

if exist "%CONTROL_FILE%" (
    del /q "!TEMP_REQUEST!" >nul 2>nul
    echo FAILED: an AOSharp capture-control request arrived before this request could be queued.
    exit /b 1
)

move /y "!TEMP_REQUEST!" "%CONTROL_FILE%" >nul
if errorlevel 1 (
    del /q "!TEMP_REQUEST!" >nul 2>nul
    echo FAILED: could not queue the AOSharp capture-control request.
    exit /b 1
)

echo SUCCESS: AOSharp capture-control request queued: !PAYLOAD!
exit /b 0

:usage
echo Usage: cmd /d /c tools-temp\control-aosharp-live-capture.cmd start
echo        cmd /d /c tools-temp\control-aosharp-live-capture.cmd stop
echo        cmd /d /c tools-temp\control-aosharp-live-capture.cmd mark "text"
echo        cmd /d /c tools-temp\control-aosharp-live-capture.cmd flush
echo        cmd /d /c tools-temp\control-aosharp-live-capture.cmd snapshot
exit /b 2

:help
echo Usage: cmd /d /c tools-temp\control-aosharp-live-capture.cmd start
echo        cmd /d /c tools-temp\control-aosharp-live-capture.cmd stop
echo        cmd /d /c tools-temp\control-aosharp-live-capture.cmd mark "text"
echo        cmd /d /c tools-temp\control-aosharp-live-capture.cmd flush
echo        cmd /d /c tools-temp\control-aosharp-live-capture.cmd snapshot
exit /b 0
