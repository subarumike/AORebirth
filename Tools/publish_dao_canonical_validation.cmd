@echo off
setlocal EnableExtensions
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1
%AO_REBIRTH_PYTHON% "%~dp0..\tools\dao_canonical_publisher.py" %*
exit /b %errorlevel%
