@echo off
setlocal

set "ROOT=%~dp0.."
set "TEST_SRC=%ROOT%\AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src"
set "TEST_PROJECT=%TEST_SRC%\SmokeLounge.AOtomation.Messaging.Tests\SmokeLounge.AOtomation.Messaging.Tests.csproj"
set "TEST_DLL=%ROOT%\AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\bin\test\Debug\SmokeLounge.AOtomation.Messaging.Tests.dll"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

if not exist "%VSWHERE%" (
    echo ERROR: Visual Studio Installer vswhere.exe was not found.
    exit /b 1
)

set "VSTEST="
for /f "usebackq delims=" %%I in (`"%VSWHERE%" -latest -products * -find Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe`) do (
    if not defined VSTEST set "VSTEST=%%I"
)

if not defined VSTEST (
    echo ERROR: vstest.console.exe was not found in the latest Visual Studio installation.
    exit /b 1
)

MSBuild.exe "%TEST_PROJECT%" /t:Build /p:Configuration=Debug /p:SolutionDir="%TEST_SRC%\\" /m:1 /nr:false /v:minimal
if errorlevel 1 exit /b %errorlevel%

"%VSTEST%" "%TEST_DLL%" %*
exit /b %errorlevel%
