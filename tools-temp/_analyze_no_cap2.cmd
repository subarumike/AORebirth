@echo off
set "CAP=%USERPROFILE%\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260729-081208"
echo === packets around OUT invite ===
findstr /n /c:"06:12:1" "%CAP%\packets.hex.log"
echo === CharacterAction around invite ===
findstr /n /i "CharacterAction TeamRequest Action=168 Action=0xA8" "%CAP%\events.log"
