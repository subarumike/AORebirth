@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1
pushd "%~dp0.."
%AO_REBIRTH_PYTHON% Tools\generate_pf4582_placements.py --check
if errorlevel 1 goto :fail
%AO_REBIRTH_PYTHON% -m unittest Tools.tests.test_generate_pf4582_placements
if errorlevel 1 goto :fail
popd
exit /b 0

:fail
set "PF4582_EXIT=%ERRORLEVEL%"
popd
exit /b %PF4582_EXIT%
