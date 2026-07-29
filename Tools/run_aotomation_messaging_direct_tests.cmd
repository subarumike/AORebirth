@echo off
setlocal

set "ROOT=%~dp0.."
set "TEST_SRC=%ROOT%\AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src"
set "TEST_PROJECT=%TEST_SRC%\SmokeLounge.AOtomation.Messaging.Tests\SmokeLounge.AOtomation.Messaging.Tests.csproj"
set "TEST_DLL=%ROOT%\AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\bin\test\Debug\SmokeLounge.AOtomation.Messaging.Tests.dll"
set "RUNNER_SOURCE=%ROOT%\tools\AOtomationMessagingDirectTestRunner.cs"
set "RUNNER_EXE=%ROOT%\tools-temp\AORebirth.AOtomationMessagingDirectTestRunner.exe"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

dotnet msbuild "%TEST_PROJECT%" /t:Build /p:Configuration=Debug /p:SolutionDir="%TEST_SRC%\\" /m:1 /nr:false /v:minimal
if errorlevel 1 exit /b %errorlevel%

if not exist "%CSC%" (
    echo ERROR: .NET Framework C# compiler was not found at "%CSC%".
    exit /b 1
)

"%CSC%" /nologo /optimize+ /out:"%RUNNER_EXE%" "%RUNNER_SOURCE%"
if errorlevel 1 exit /b %errorlevel%

"%RUNNER_EXE%" "%TEST_DLL%" %*
if errorlevel 1 goto runner_failed
del /q "%RUNNER_EXE%" >nul 2>nul
exit /b 0

:runner_failed
del /q "%RUNNER_EXE%" >nul 2>nul
exit /b 1
