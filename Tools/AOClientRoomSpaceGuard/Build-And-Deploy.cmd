@echo off
setlocal

set "TOOL_ROOT=%~dp0"
set "SOURCE=%TOOL_ROOT%Program.cs"
set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "DEPLOY_ROOT=%~1"
if not defined DEPLOY_ROOT set "DEPLOY_ROOT=C:\Funcom\AOClientRoomSpaceGuard"
set "TEMP_EXE=%TEMP%\AOClientRoomSpaceGuard-build-%RANDOM%-%RANDOM%.exe"

if not exist "%CSC%" (
  echo [RoomSpace Guard] Missing compiler: %CSC%
  exit /b 1
)

if not exist "%SOURCE%" (
  echo [RoomSpace Guard] Missing source: %SOURCE%
  exit /b 1
)

tasklist /FI "IMAGENAME eq AOClientRoomSpaceGuard.exe" /NH 2>nul | find /I "AOClientRoomSpaceGuard.exe" >nul
if not errorlevel 1 (
  echo [RoomSpace Guard] Close the running guarded AO client before deployment.
  exit /b 1
)

echo [RoomSpace Guard] Building x86 telemetry guard...
"%CSC%" /nologo /target:exe /platform:x86 /optimize+ /out:"%TEMP_EXE%" "%SOURCE%"
if errorlevel 1 goto :fail

echo [RoomSpace Guard] Running wrapper self-test...
"%TEMP_EXE%" --self-test
if errorlevel 1 goto :fail

echo [RoomSpace Guard] Validating new-client profile...
"%TEMP_EXE%" --client-root "C:\Funcom\Anarchy Online" --inspect
if errorlevel 1 goto :fail

echo [RoomSpace Guard] Validating old-client profile...
"%TEMP_EXE%" --client-root "D:\Funcom\Anarchy Online" --inspect
if errorlevel 1 goto :fail

if not exist "%DEPLOY_ROOT%" mkdir "%DEPLOY_ROOT%"
if errorlevel 1 goto :fail

copy /Y "%TEMP_EXE%" "%DEPLOY_ROOT%\AOClientRoomSpaceGuard.exe" >nul
if errorlevel 1 goto :fail
copy /Y "%TOOL_ROOT%Start-New-AO-With-RoomSpace-Guard.cmd" "%DEPLOY_ROOT%\Start-New-AO-With-RoomSpace-Guard.cmd" >nul
if errorlevel 1 goto :fail
copy /Y "%TOOL_ROOT%Start-Old-AO-With-RoomSpace-Guard.cmd" "%DEPLOY_ROOT%\Start-Old-AO-With-RoomSpace-Guard.cmd" >nul
if errorlevel 1 goto :fail
copy /Y "%TOOL_ROOT%README.md" "%DEPLOY_ROOT%\README.md" >nul
if errorlevel 1 goto :fail

echo [RoomSpace Guard] Verifying deployed executable...
"%DEPLOY_ROOT%\AOClientRoomSpaceGuard.exe" --self-test
if errorlevel 1 goto :fail

del /Q "%TEMP_EXE%" >nul 2>nul
echo [RoomSpace Guard] PASS deployed=%DEPLOY_ROOT%\AOClientRoomSpaceGuard.exe
exit /b 0

:fail
set "EXIT_CODE=%ERRORLEVEL%"
del /Q "%TEMP_EXE%" >nul 2>nul
echo [RoomSpace Guard] FAIL exitCode=%EXIT_CODE%
exit /b %EXIT_CODE%
