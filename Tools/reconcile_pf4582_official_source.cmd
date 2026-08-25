@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1
pushd "%~dp0.."
if /I "%~1"=="--test" goto :test
%AO_REBIRTH_PYTHON% Tools\reconcile_pf4582_official_source.py %*
set "PF4582_OFFICIAL_EXIT=%ERRORLEVEL%"
popd
exit /b %PF4582_OFFICIAL_EXIT%

:test
%AO_REBIRTH_PYTHON% Tools\reconcile_pf4582_official_source.py --check
if errorlevel 1 goto :fail
%AO_REBIRTH_PYTHON% -m unittest Tools.tests.test_reconcile_pf4582_official_source
if errorlevel 1 goto :fail
popd
exit /b 0

:fail
set "PF4582_OFFICIAL_EXIT=%ERRORLEVEL%"
popd
exit /b %PF4582_OFFICIAL_EXIT%
