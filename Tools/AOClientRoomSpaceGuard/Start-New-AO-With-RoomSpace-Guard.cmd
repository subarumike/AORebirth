@echo off
setlocal
set "GUARD=%~dp0AOClientRoomSpaceGuard.exe"
if not exist "%GUARD%" (
  echo Missing runtime guard: %GUARD%
  pause
  exit /b 1
)
start "AO RoomSpace Guard - New Client" /min "%GUARD%" --client-root "C:\Funcom\Anarchy Online" --wait-seconds 600
start "" /D "C:\Funcom\Anarchy Online" "C:\Funcom\Anarchy Online\Anarchy.exe"
