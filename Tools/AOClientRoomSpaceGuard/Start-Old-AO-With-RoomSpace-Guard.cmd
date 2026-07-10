@echo off
setlocal
set "GUARD=%~dp0AOClientRoomSpaceGuard.exe"
if not exist "%GUARD%" (
  echo Missing runtime guard: %GUARD%
  pause
  exit /b 1
)
start "AO RoomSpace Guard - Old Client" /min "%GUARD%" --client-root "D:\Funcom\Anarchy Online" --wait-seconds 600 --launch-client
