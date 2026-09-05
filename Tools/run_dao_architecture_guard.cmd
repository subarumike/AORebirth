@echo off
setlocal
if not "%~1"=="" if not "%~1"=="--mission-persistence-only" exit /b 2
if not "%~2"=="" exit /b 2
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1

set "DAO_GUARD=%~dp0DaoArchitectureGuard\dao_architecture_guard.py"
set "DAO_MANIFEST=%~dp0DaoArchitectureGuard\known-violations.json"

%AO_REBIRTH_PYTHON% "%DAO_GUARD%" --self-test
if errorlevel 1 exit /b 1

%AO_REBIRTH_PYTHON% "%DAO_GUARD%" --root "%~dp0.." --manifest "%DAO_MANIFEST%" %1
exit /b %errorlevel%
