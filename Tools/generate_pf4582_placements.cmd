@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1
pushd "%~dp0.."
%AO_REBIRTH_PYTHON% Tools\generate_pf4582_placements.py %*
set "PF4582_EXIT=%ERRORLEVEL%"
popd
exit /b %PF4582_EXIT%
