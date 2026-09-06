@echo off
setlocal
call Tools\select_python_runtime.cmd
if errorlevel 1 exit /b %errorlevel%
%AO_REBIRTH_PYTHON% -m unittest Tools.tests.test_mission_capture_wave_planner
exit /b %errorlevel%
