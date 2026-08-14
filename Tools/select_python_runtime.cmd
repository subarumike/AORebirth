@echo off
set "AO_REBIRTH_PORTABLE_PYTHON=C:\Tools\AORebirthPython\cpython-3.13.14-nuget-amd64\tools\python.exe"
if defined AO_REBIRTH_PYTHON_EXE (
    rem Reuse the already-validated executable path when this selector is called
    rem recursively from a wrapper that has already expanded AO_REBIRTH_PYTHON
    rem to include execution flags.
) else if defined AO_REBIRTH_PYTHON (
    set "AO_REBIRTH_PYTHON_EXE=%AO_REBIRTH_PYTHON%"
) else (
    call :try_python_candidate "%AO_REBIRTH_PORTABLE_PYTHON%"
    if not defined AO_REBIRTH_PYTHON_EXE for /f "delims=" %%I in ('py.exe -3.13 -c "import sys;print(sys.executable)" 2^>nul') do call :try_python_candidate "%%I"
    if not defined AO_REBIRTH_PYTHON_EXE for /f "delims=" %%I in ('where.exe python.exe 2^>nul') do call :try_python_candidate "%%I"
)
if not defined AO_REBIRTH_PYTHON_EXE (
    echo [AORebirth Python] FAIL - a supported 64-bit CPython 3.13.14 runtime was not found.
    echo [AORebirth Python] Use the recommended Windows installer or the CPython NuGet package, not the Windows embeddable package.
    echo [AORebirth Python] Install it, add it to PATH, or set AO_REBIRTH_PYTHON to its python.exe path.
    exit /b 1
)
if not exist "%AO_REBIRTH_PYTHON_EXE%" (
    echo [AORebirth Python] FAIL - selected CPython path does not exist: %AO_REBIRTH_PYTHON_EXE%
    echo [AORebirth Python] Set AO_REBIRTH_PYTHON to a 64-bit CPython 3.13.14 python.exe path.
    exit /b 1
)
"%AO_REBIRTH_PYTHON_EXE%" -B -X faulthandler -c "import platform,sys;v=sys.version_info;raise SystemExit(0 if platform.python_implementation() == 'CPython' and platform.architecture()[0] == '64bit' and v[:3] == (3, 13, 14) and sys.flags.isolated == 0 else 1)" >nul 2>nul
if errorlevel 1 (
    echo [AORebirth Python] FAIL - selected runtime must be non-embedded 64-bit CPython 3.13.14.
    echo [AORebirth Python] The Windows embeddable package is unsupported because its isolated module path blocks repository imports.
    exit /b 1
)
set "AO_REBIRTH_PYTHON="%AO_REBIRTH_PYTHON_EXE%" -B -X faulthandler"
if not defined AO_REBIRTH_PYTHON_DIAGNOSTIC_EMITTED (
    %AO_REBIRTH_PYTHON% -c "import sys,platform,faulthandler;print('[AORebirth Python] runtime=' + sys.executable + ' version=' + platform.python_version() + ' arch=' + platform.architecture()[0] + ' dontWriteBytecode=' + str(sys.dont_write_bytecode) + ' faulthandler=' + str(faulthandler.is_enabled()))"
    if errorlevel 1 exit /b 1
    set "AO_REBIRTH_PYTHON_DIAGNOSTIC_EMITTED=1"
)
exit /b 0

:try_python_candidate
if defined AO_REBIRTH_PYTHON_EXE exit /b 0
if not exist "%~1" exit /b 0
"%~1" -B -X faulthandler -c "import platform,sys;v=sys.version_info;raise SystemExit(0 if platform.python_implementation() == 'CPython' and platform.architecture()[0] == '64bit' and v[:3] == (3, 13, 14) and sys.flags.isolated == 0 else 1)" >nul 2>nul
if errorlevel 1 exit /b 0
set "AO_REBIRTH_PYTHON_EXE=%~1"
exit /b 0
