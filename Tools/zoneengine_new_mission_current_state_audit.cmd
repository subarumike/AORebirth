@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1
cd /d "%~dp0.."
%AO_REBIRTH_PYTHON% Tools\zoneengine_new_mission_current_state_audit.py %*
exit /b %errorlevel%
