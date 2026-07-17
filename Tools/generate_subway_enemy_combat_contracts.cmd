@echo off
setlocal

cd /d "%~dp0.."
python tools-temp\AOSharpCaptureAnalyzer\analyze_subway_enemy_combat_contracts.py
exit /b %errorlevel%
