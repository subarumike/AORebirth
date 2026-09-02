@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1

set "DAO_GUARD=%~dp0DaoArchitectureGuard\dao_architecture_guard.py"
set "DAO_MANIFEST=%~dp0DaoArchitectureGuard\known-violations.json"

%AO_REBIRTH_PYTHON% "%DAO_GUARD%" --self-test
if errorlevel 1 exit /b 1

%AO_REBIRTH_PYTHON% "%DAO_GUARD%" --root "%~dp0.." --manifest "%DAO_MANIFEST%"
exit /b %errorlevel%
