@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "FAILED=0"

call :CheckRequired ChatEngine.exe 6996 7012
call :CheckRequired LoginEngine.exe 7500
call :CheckRequired ZoneEngine.exe 7501
call :CheckOptional WebEngine.exe 8181

if not "%FAILED%"=="0" (
    echo [AORebirth Status] FAIL - one or more required engines or ports are unavailable.
    exit /b 1
)

echo [AORebirth Status] PASS - required engine processes and ports are available.
exit /b 0

:CheckRequired
call :CheckEngine required %~1 %~2 %~3
exit /b 0

:CheckOptional
call :CheckEngine optional %~1 %~2 %~3
exit /b 0

:CheckEngine
set "REQUIREMENT=%~1"
set "ENGINE=%~2"
set "RUNNING=no"
tasklist /FI "IMAGENAME eq %ENGINE%" /NH 2>nul | findstr /I /B /C:"%ENGINE%" >nul
if not errorlevel 1 set "RUNNING=yes"

set "PORT_REPORT="
set "PORT_FAILURE=0"
for %%P in (%~3 %~4) do (
    netstat -ano -p tcp | findstr /R /C:":%%P .*LISTENING" >nul
    if errorlevel 1 (
        set "PORT_REPORT=!PORT_REPORT! %%P=closed"
        set "PORT_FAILURE=1"
    ) else (
        set "PORT_REPORT=!PORT_REPORT! %%P=listening"
    )
)

echo [AORebirth Status] %ENGINE% requirement=%REQUIREMENT% running=%RUNNING% ports:%PORT_REPORT%

if /I "%REQUIREMENT%"=="required" (
    if /I "%RUNNING%"=="no" set "FAILED=1"
    if not "%PORT_FAILURE%"=="0" set "FAILED=1"
)
exit /b 0
