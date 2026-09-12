@echo off
setlocal EnableExtensions
pushd "%~dp0.." >nul
if errorlevel 1 exit /b 1

dotnet test AORebirth\Server\ZoneEngine_New.Tests\ZoneEngine_New.Tests.csproj --configuration Debug --nologo
if errorlevel 1 goto :fail

rem Offline packaged-content/configuration checks never connect to a database or bind a listener.
AORebirth\Built\Debug\ZoneEngine_New\ZoneEngine_New.exe --validate-startup
if errorlevel 1 goto :fail

echo [ZoneEngine_New Acceptance] PASS
popd >nul
exit /b 0

:fail
echo [ZoneEngine_New Acceptance] FAIL
popd >nul
exit /b 1
