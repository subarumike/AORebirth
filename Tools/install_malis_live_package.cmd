@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1

cd /d "%~dp0.."
if "%MIKE_AOSHARP_RUNTIME%"=="" set "MIKE_AOSHARP_RUNTIME=D:\AOTools\ReadyToUse"
set "MALIS_WORK_ROOT=%CD%\tools-temp\MalisLiveBuild"
set "MALIS_PACKAGE_ROOT=%CD%\build-verify\MalisMissionLive"
set "MALIS_METADATA=%MALIS_WORK_ROOT%\compatibility-metadata.json"

%AO_REBIRTH_PYTHON% Tools\malis_live_build.py --check --runtime "%MIKE_AOSHARP_RUNTIME%" --work-root "%MALIS_WORK_ROOT%" --package-root "%MALIS_PACKAGE_ROOT%" --metadata "%MALIS_METADATA%"
if errorlevel 1 exit /b 1

%AO_REBIRTH_PYTHON% Tools\malis_live_build.py --install --runtime "%MIKE_AOSHARP_RUNTIME%" --work-root "%MALIS_WORK_ROOT%" --package-root "%MALIS_PACKAGE_ROOT%" --metadata "%MALIS_METADATA%"
if errorlevel 1 exit /b 1

echo MALIS_LIVE_INSTALL=PASS
exit /b 0
