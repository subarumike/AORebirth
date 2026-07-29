@echo off
set "CAP=%USERPROFILE%\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260729-080834"
echo === session ===
type "%CAP%\capture-session.json"
echo.
echo === OUT packets around invite ===
findstr /i /n "OUT # OUT-N3 TeamRequest TeamInvite TeamMember" "%CAP%\packets.hex.log" "%CAP%\events.log"
echo.
echo === all OUT N3 types ===
findstr /n "OUT #\|\[OUT-N3\]" "%CAP%\packets.hex.log" "%CAP%\events.log"
