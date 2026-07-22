@echo off
setlocal

cd /d "%~dp0.."
python tools-temp\AOSharpCaptureAnalyzer\extract_capture_backed_npc_combat.py %*
exit /b %errorlevel%
