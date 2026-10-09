@echo off
setlocal
cd /d "%~dp0..\.."
cmd /d /c MSBuild.exe Tools\MissionDestinationCatalog.Tests\MissionDestinationCatalog.Tests.csproj /t:Build /p:Configuration=Release /m:1 /nr:false /v:minimal
if errorlevel 1 exit /b 1
Tools\MissionDestinationCatalog.Tests\bin\Release\MissionDestinationCatalog.Tests.exe
exit /b %errorlevel%
