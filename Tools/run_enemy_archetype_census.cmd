@echo off
setlocal
cd /d "%~dp0.."

call Tools\export_official_enemy_visual_sources.cmd
if errorlevel 1 exit /b %ERRORLEVEL%

python -m unittest Tools.tests.test_enemy_archetype_census
if errorlevel 1 exit /b %ERRORLEVEL%

python Tools\enemy_archetype_census.py %*
exit /b %ERRORLEVEL%
