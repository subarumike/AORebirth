@echo off
setlocal
cd /d "%~dp0.."
python tools\enemy_catalog\extract_enemy_catalog.py %*
exit /b %errorlevel%
