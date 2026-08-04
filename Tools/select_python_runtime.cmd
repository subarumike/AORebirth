@echo off
if defined AO_REBIRTH_PYTHON goto :verify
set "AO_REBIRTH_PYTHON=py -3.13 -B"

:verify
%AO_REBIRTH_PYTHON% -c "import sys;v=sys.version_info;raise SystemExit(0 if v[:2] == (3, 13) and v.micro >= 14 else 1)" >nul 2>nul
if errorlevel 1 (
    echo [AORebirth Python] FAIL - CPython 3.13.14 or newer 3.13 maintenance is required.
    exit /b 1
)
exit /b 0
