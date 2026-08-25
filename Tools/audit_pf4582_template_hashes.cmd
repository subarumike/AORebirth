@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1
pushd "%~dp0.."
if /I "%~1"=="--test" goto :test
%AO_REBIRTH_PYTHON% Tools\audit_pf4582_template_hashes.py %*
set "PF4582_AUDIT_EXIT=%ERRORLEVEL%"
popd
exit /b %PF4582_AUDIT_EXIT%

:test
%AO_REBIRTH_PYTHON% Tools\audit_pf4582_template_hashes.py --check
if errorlevel 1 goto :fail
%AO_REBIRTH_PYTHON% -m unittest Tools.tests.test_audit_pf4582_template_hashes
if errorlevel 1 goto :fail
popd
exit /b 0

:fail
set "PF4582_AUDIT_EXIT=%ERRORLEVEL%"
popd
exit /b %PF4582_AUDIT_EXIT%
