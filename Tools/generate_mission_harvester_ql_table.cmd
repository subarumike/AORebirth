@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1

cd /d "%~dp0.."
%AO_REBIRTH_PYTHON% Tools\generate_mission_harvester_ql_table.py %*
exit /b %errorlevel%
