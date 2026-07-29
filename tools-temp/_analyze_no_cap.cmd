@echo off
set "CAP=%USERPROFILE%\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260729-081208"
echo === OUT lines in packets.hex.log ===
findstr /n /c:"OUT #" "%CAP%\packets.hex.log"
echo === OUT-N3 in events.log ===
findstr /n /c:"[OUT-N3]" "%CAP%\events.log"
echo === TeamRequest in events ===
findstr /n /i "TeamRequest TeamInvite" "%CAP%\events.log" "%CAP%\enemy-fight-events.log" "%CAP%\npc-interactions.log"
echo === session ===
type "%CAP%\capture-session.json"
