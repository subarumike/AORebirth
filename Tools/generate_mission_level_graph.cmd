@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1

cd /d "%~dp0.."
%AO_REBIRTH_PYTHON% tools\generate_mission_level_graph.py %*
exit /b %errorlevel%
