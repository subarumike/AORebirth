@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1
%AO_REBIRTH_PYTHON% "%~dp0..\LinuxBuild\Tools\test_linux_source_identity.py"
if errorlevel 1 exit /b 1
%AO_REBIRTH_PYTHON% "%~dp0..\LinuxBuild\Tools\test_placement_parity.py"
exit /b %errorlevel%
