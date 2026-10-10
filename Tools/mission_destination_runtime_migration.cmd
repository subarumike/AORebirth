@echo off
setlocal
cd /d "%~dp0.."
call Tools\select_python_runtime.cmd
if errorlevel 1 exit /b %errorlevel%
%AO_REBIRTH_PYTHON% Tools\mission_destination_runtime_migration.py %*
exit /b %errorlevel%
