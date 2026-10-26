@echo off
setlocal EnableExtensions
call "%~dp0..\tools\select_python_runtime.cmd"
if errorlevel 1 exit /b 1
%AO_REBIRTH_PYTHON% "%~dp0ItemExpansionCatalog\normalize_historical_item_evidence.py" %*
exit /b %errorlevel%
