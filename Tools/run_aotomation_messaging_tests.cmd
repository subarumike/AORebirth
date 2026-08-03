@echo off
if not "%AO_REBIRTH_GENERATED_COMBAT_LEASE_DELEGATION%"=="" (
    python "%~dp0generated_combat_pipeline.py" --_validate-read-delegation
    if errorlevel 1 exit /b 1
    goto :generated_combat_read_lease_acquired
)
python "%~dp0generated_combat_pipeline.py" --run-read-lease -- "%ComSpec%" /d /c "%~f0" %*
exit /b %errorlevel%

:generated_combat_read_lease_acquired
setlocal

set "ROOT=%~dp0.."
set "TEST_SRC=%ROOT%\AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src"
set "TEST_PROJECT=%TEST_SRC%\SmokeLounge.AOtomation.Messaging.Tests\SmokeLounge.AOtomation.Messaging.Tests.csproj"
set "TEST_DLL=%ROOT%\AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\bin\test\Debug\SmokeLounge.AOtomation.Messaging.Tests.dll"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

set "VSTEST="
if exist "%VSWHERE%" (
    for /f "usebackq delims=" %%I in (`"%VSWHERE%" -latest -products * -find Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe`) do (
        if not defined VSTEST set "VSTEST=%%I"
    )
)

if not defined VSTEST (
    call "%ROOT%\tools\run_aotomation_messaging_direct_tests.cmd" %*
    if errorlevel 1 exit /b 1
    exit /b 0
)

MSBuild.exe "%TEST_PROJECT%" /t:Build /p:Configuration=Debug /p:SolutionDir="%TEST_SRC%\\" /m:1 /nr:false /v:minimal
if errorlevel 1 exit /b %errorlevel%

"%VSTEST%" "%TEST_DLL%" %*
exit /b %errorlevel%
