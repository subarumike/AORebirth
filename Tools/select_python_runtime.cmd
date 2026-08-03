@echo off
if defined AO_REBIRTH_PYTHON goto :verify
set "AO_REBIRTH_PYTHON=py -3.14"

:verify
%AO_REBIRTH_PYTHON% -c "import sys;raise SystemExit(0 if sys.version_info >= (3, 14) else 1)" >nul 2>nul
if errorlevel 1 (
    echo [AORebirth Python] FAIL - Python 3.14 or newer is required.
    exit /b 1
)
exit /b 0
