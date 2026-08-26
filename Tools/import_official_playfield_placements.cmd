@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1
pushd "%~dp0.."
if /I "%~1"=="--test" goto :test
if /I "%~1"=="--write" goto :write
if /I "%~1"=="--check" goto :check
%AO_REBIRTH_PYTHON% Tools\import_official_playfield_placements.py %*
set "OFFICIAL_PLACEMENT_IMPORT_EXIT=%ERRORLEVEL%"
popd
exit /b %OFFICIAL_PLACEMENT_IMPORT_EXIT%

:write
%AO_REBIRTH_PYTHON% Tools\import_official_playfield_placements.py %*
if errorlevel 1 goto :fail
%AO_REBIRTH_PYTHON% Tools\aorebirth_playfield_reconciliation.py --write
if errorlevel 1 goto :fail
popd
exit /b 0

:check
%AO_REBIRTH_PYTHON% Tools\import_official_playfield_placements.py %*
if errorlevel 1 goto :fail
%AO_REBIRTH_PYTHON% Tools\aorebirth_playfield_reconciliation.py --check
if errorlevel 1 goto :fail
popd
exit /b 0

:test
%AO_REBIRTH_PYTHON% Tools\import_official_playfield_placements.py --check
if errorlevel 1 goto :fail
%AO_REBIRTH_PYTHON% -m unittest Tools.tests.test_import_official_playfield_placements
if errorlevel 1 goto :fail
%AO_REBIRTH_PYTHON% Tools\aorebirth_playfield_reconciliation.py --check
if errorlevel 1 goto :fail
%AO_REBIRTH_PYTHON% -m unittest Tools.tests.test_aorebirth_playfield_reconciliation
if errorlevel 1 goto :fail
popd
exit /b 0

:fail
set "OFFICIAL_PLACEMENT_IMPORT_EXIT=%ERRORLEVEL%"
popd
exit /b %OFFICIAL_PLACEMENT_IMPORT_EXIT%
