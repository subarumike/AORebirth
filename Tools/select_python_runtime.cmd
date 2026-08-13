@echo off
if defined AO_REBIRTH_PYTHON goto :verify
set "AO_REBIRTH_PYTHON_EXE="
for /f "usebackq delims=" %%I in (`py -3.12 -c "import sys;print(sys.executable)"`) do (
    if not defined AO_REBIRTH_PYTHON_EXE set "AO_REBIRTH_PYTHON_EXE=%%I"
)
if not defined AO_REBIRTH_PYTHON_EXE (
    echo [AORebirth Python] FAIL - CPython 3.12 runtime was not found.
    exit /b 1
)
if not exist "%AO_REBIRTH_PYTHON_EXE%" (
    echo [AORebirth Python] FAIL - selected CPython path does not exist: %AO_REBIRTH_PYTHON_EXE%
    exit /b 1
)
set "AO_REBIRTH_PYTHON="%AO_REBIRTH_PYTHON_EXE%" -B -X faulthandler"

:verify
%AO_REBIRTH_PYTHON% -c "import sys;v=sys.version_info;raise SystemExit(0 if v[:2] == (3, 12) and v.micro >= 10 else 1)" >nul 2>nul
if errorlevel 1 (
    echo [AORebirth Python] FAIL - CPython 3.12.10 or newer 3.12 maintenance is required.
    exit /b 1
)
if not defined AO_REBIRTH_PYTHON_DIAGNOSTIC_EMITTED (
    %AO_REBIRTH_PYTHON% -c "import sys,platform,faulthandler;print('[AORebirth Python] runtime=' + sys.executable + ' version=' + platform.python_version() + ' arch=' + platform.architecture()[0] + ' dontWriteBytecode=' + str(sys.dont_write_bytecode) + ' faulthandler=' + str(faulthandler.is_enabled()))"
    if errorlevel 1 exit /b 1
    set "AO_REBIRTH_PYTHON_DIAGNOSTIC_EMITTED=1"
)
exit /b 0
