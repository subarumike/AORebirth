@echo off
setlocal
pushd "%~dp0.."
python Tools\stress_generated_combat_pipeline.py %*
set "STRESS_EXIT=%ERRORLEVEL%"
popd
exit /b %STRESS_EXIT%
