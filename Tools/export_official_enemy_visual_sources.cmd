@echo off
setlocal
cd /d "%~dp0.."
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools\export_official_enemy_visual_sources.ps1 %*
exit /b %ERRORLEVEL%
