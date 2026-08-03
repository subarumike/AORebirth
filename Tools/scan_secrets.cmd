@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1
cd /d "%~dp0.."
%AO_REBIRTH_PYTHON% Tools\scan_secrets.py
exit /b %errorlevel%
