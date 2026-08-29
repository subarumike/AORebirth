@echo off
setlocal
if "%~1"=="" (
    echo Usage: %~nx0 ^<portable-atlas-zip^> [--check]
    exit /b 2
)
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1
pushd "%~dp0.."
%AO_REBIRTH_PYTHON% Tools\import_acg_development_placeholders.py %*
set "ACG_PLACEHOLDER_EXIT=%ERRORLEVEL%"
popd
exit /b %ACG_PLACEHOLDER_EXIT%
