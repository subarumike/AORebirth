@echo off
setlocal
set "LOG=%USERPROFILE%\source\repos\AORebirth\tools-temp\_find_gui_team.txt"
echo. > "%LOG%"
echo === configs ===>>"%LOG%"
dir /s /b "%USERPROFILE%\source\repos\aosharp\*config.json" >>"%LOG%" 2>nul
echo === bootstrapper logs ===>>"%LOG%"
dir /s /b "%USERPROFILE%\*AOSharp.Bootstrapper*.txt" >>"%LOG%" 2>nul
echo === find Anarchy/GUI ===>>"%LOG%"
if exist "C:\Program Files (x86)\Funcom" dir /s /b "C:\Program Files (x86)\Funcom\*GUI.dll" >>"%LOG%" 2>nul
if exist "C:\Program Files\Funcom" dir /s /b "C:\Program Files\Funcom\*GUI.dll" >>"%LOG%" 2>nul
if exist "%USERPROFILE%\AppData\Local\Funcom" dir /s /b "%USERPROFILE%\AppData\Local\Funcom\*GUI.dll" >>"%LOG%" 2>nul
if exist "D:\Funcom" dir /s /b "D:\Funcom\*GUI.dll" >>"%LOG%" 2>nul
if exist "E:\Funcom" dir /s /b "E:\Funcom\*GUI.dll" >>"%LOG%" 2>nul
echo === dumpbin TooHigh nearby ===>>"%LOG%"
for /f "delims=" %%i in ('dir /s /b "%USERPROFILE%\AppData\Local\Funcom\*GUI.dll" 2^>nul') do (
  echo FILE %%i>>"%LOG%"
  dumpbin /exports "%%i" 2>nul | findstr /i "Team Join Too High Low XP Exp Invite" >>"%LOG%"
)
echo DONE>>"%LOG%"
type "%LOG%"
