@echo off
setlocal
cd /d "%~dp0"

call tools-temp\start-aosharp-live-capture.cmd --title "Anarchy Online"
if errorlevel 1 (
    echo.
    echo AO capture failed to start. Review the failure message above.
    pause
)

endlocal
