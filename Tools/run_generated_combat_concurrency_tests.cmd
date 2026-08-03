@echo off
setlocal
pushd "%~dp0.."
python -m unittest Tools.tests.test_generated_artifact_transaction Tools.tests.test_generated_combat_pipeline
if errorlevel 1 goto :fail
call Tools\stress_generated_combat_pipeline.cmd --fixture-only
if errorlevel 1 goto :fail
popd
exit /b 0

:fail
set "TEST_EXIT=%ERRORLEVEL%"
popd
exit /b %TEST_EXIT%
