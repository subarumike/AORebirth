@echo off
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1
%AO_REBIRTH_PYTHON% "%~dp0validate_capture_evidence_fixtures.py" %*
exit /b %errorlevel%
