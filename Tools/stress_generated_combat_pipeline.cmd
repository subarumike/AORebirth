@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1
pushd "%~dp0.."
%AO_REBIRTH_PYTHON% Tools\stress_generated_combat_pipeline.py %*
set "STRESS_EXIT=%ERRORLEVEL%"
popd
exit /b %STRESS_EXIT%
