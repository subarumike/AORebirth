@echo off
setlocal

cd /d "%~dp0.."
python tools\generate_mission_level_graph.py %*
exit /b %errorlevel%
